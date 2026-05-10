# Implementation Plan: Phase 5 God Function Extraction Repairs

**MISSION**: Phase 5 God Function Extraction Repairs
**BUILD_TAG**: 1111.006-phase-5-part-2
**REPO**: universal-or-strategy
**BRANCH**: phase-5-part-2

## 1. STRATEGIC ANALYSIS & OBJECTIVE

The Phase 5 god-function extraction PR introduced six static-analysis
regressions (Codacy / DeepSource + Arena AI supplemental audit). All are
extraction artifacts: dead code, missing dedupe in extracted enqueue
helpers (Fleet AND Master), dropped validation in a switch-style handler,
mixed timezone usage, one redundant LINQ query (cache required), systemic
brace omissions, plus 3 verbatim Print logs dropped during the
`ExecuteTRENDEntry` extraction.

This plan executes surgical repairs ONLY -- no speculative refactor,
no new public surface, no whitespace mutation beyond the explicit
brace insertions in T6.

## 2. FORENSIC VERIFICATION

| ID | File | Line(s) | Evidence |
|---|------|---------|----------|
| F-01a | `src/V12_002.Entries.Trend.cs` | 71-75 vs 79-83 | Outer `CurrentBar < 20` guard returns; inner duplicate inside `try` is dead code |
| F-01b | `src/V12_002.Entries.Trend.cs` | 269, 620, 623 | `DateTime.Now` used; rest of codebase (`REAPER.Audit.cs` 18, 45, 122, 306) uses `DateTime.UtcNow` |
| F-02 | `src/V12_002.REAPER.Audit.cs` | 262-266 (Fleet), 449-453 (Master) | BOTH `EnqueueReaperFlattenCandidate` AND `EnqueueReaperMasterFlatten` unconditionally return `true`; callers (lines 141, 370) are guarded by `if` expecting dedupe. Master path MUST receive the same `_reaperFlattenInFlight` guard as Fleet -- no asymmetry permitted |
| F-03 | `src/V12_002.UI.IPC.Commands.Config.cs` | 138 | T1 writes `Target1Value = v` directly; T2-T5 (lines 141-175) gate via `ValidateIpcMultiplier` |
| F-04 | All four touched files | various | Single-line `if () return;` without braces flagged by Codacy |
| F-05 | `src/V12_002.REAPER.Audit.cs` | 53 vs 189 | `acct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName)` called twice per audit loop -- result MUST be cached on first call and reused (single LINQ scan per tick) |
| F-06 | `src/V12_002.Entries.Trend.cs` | post-SubmitLeg2 in `ExecuteTRENDEntry` | 3 verbatim Print logs ("TREND ORDERS PLACED ..." + E1 details + E2 details) dropped during god-function extraction -- must be restored exactly as the pre-extraction implementation emitted them (operations greps + SOVEREIGN replay harness depend on these strings) |

## 3. REPAIR PROTOCOL (V12 PLATINUM STANDARD)

- Lock-free: `ConcurrentDictionary` + `TryAdd`/`TryRemove` only. NO `lock()`.
- Actor compliance: All mutations on existing strategy/marshal threads. No new `Enqueue` paths required.
- ASCII-only literals.
- Zero-allocation bias: dedupe via existing `_repairInFlight` byte-dict pattern; no new collection types.
- Whitespace mutation BANNED outside the explicit brace insertions in T6.
- Diff budget: stay comfortably under 150 KB per AGENTS.md.

## 4. TICKET BACKLOG

---

### T1 -- Eliminate dead `CurrentBar < 20` guard inside `try`

**File**: `src/V12_002.Entries.Trend.cs`
**Method**: `ExecuteTRENDEntry`
**Action**: DELETE the inner duplicate guard at lines 79-83 (the
`if (CurrentBar < 20) { Print(...); return; }` block immediately
inside `try {`). The outer guard at lines 71-75 already exits
before `try` is entered.

**Verify**:
- `grep -cn "CurrentBar < 20" src/V12_002.Entries.Trend.cs` == 1
- Compiles clean. No new `lock(`. No public surface change.

---

### T2 -- Replace `DateTime.Now` with `DateTime.UtcNow` in TREND ID generation

**File**: `src/V12_002.Entries.Trend.cs`
**Edits**:
- Line 269 (`ExecuteTREND_CalculateLegs`):
  `string timestamp = DateTime.Now.ToString("HHmmssffff");` ->
  use `DateTime.UtcNow.ToString("HHmmssffff", System.Globalization.CultureInfo.InvariantCulture)`.
- Line 620 (`ExecuteTRENDManual_BuildPosition`):
  `entryName = signalName + "_" + DateTime.Now.ToString("HHmmssffff");` ->
  same UTC + invariant culture.
- Line 623 (`ExecuteTRENDManual_BuildPosition`):
  `"TMNL_" + DateTime.Now.Ticks` -> `"TMNL_" + DateTime.UtcNow.Ticks`.

Note: `using System.Globalization;` already present (line 11) -- no new using directive needed.

**Verify**:
- `grep -n "DateTime.Now" src/V12_002.Entries.Trend.cs` == 0 hits.

---

### T2b -- Restore 3 verbatim Print logs dropped during `ExecuteTRENDEntry` extraction (F-06)

**File**: `src/V12_002.Entries.Trend.cs`
**Method**: `ExecuteTRENDEntry`
**Insertion point**: Immediately AFTER the successful
`ExecuteTREND_SubmitLeg2(...)` return-true path (currently between the
SubmitLeg2 call at line 121 and the `ExecuteTREND_DispatchSima(...)` call
at line 129), inside the existing `try` block.

**Action**: Restore the 3 Print statements VERBATIM as the pre-extraction
god-function emitted them. They depend on locals already in scope after
`ExecuteTREND_CalculateLegs` returns via its `out` parameters
(`direction`, `totalContracts`, `entry1Qty`, `ema9Value`, `stop1Price`,
`TRENDEntry1ATRMultiplier`, `entry2Qty`, `ema15Value`, `stop2Price`,
`TRENDEntry2ATRMultiplier`):

1. `Print(string.Format("TREND ORDERS PLACED: {0} Total={1} contracts", direction == MarketPosition.Long ? "LONG" : "SHORT", totalContracts));`
2. `Print(string.Format("  E1: {0}@{1:F2} (EMA9) | Stop: {2:F2} ({3}xATR from EMA9)", entry1Qty, ema9Value, stop1Price, TRENDEntry1ATRMultiplier));`
3. `Print(string.Format("  E2: {0}@{1:F2} (EMA15) | Stop: {2:F2} ({3}xATR trail)", entry2Qty, ema15Value, stop2Price, TRENDEntry2ATRMultiplier));`

**Constraints**:
- Do NOT relocate these into `ExecuteTREND_CalculateLegs` -- the logs
  must fire ONLY when both legs successfully submit (i.e., AFTER
  SubmitLeg2 returns true), preserving the pre-extraction emission order
  and semantic meaning ("orders placed" = both legs accepted by broker).
- Do NOT mutate any string literal (ASCII compliance + Arena AI verbatim
  comparison gate -- byte-identical to the original god-function output).
- Two-space indentation prefix on lines 2 and 3 ("  E1:" / "  E2:") is
  load-bearing for log post-processors and MUST be preserved verbatim.

**Rationale (Arena AI F-06)**: The pre-extraction `ExecuteTRENDEntry`
emitted these 3 diagnostic lines after successful order placement.
Operations / Forensics greps (`grep "TREND ORDERS PLACED" logs/`) and
the SOVEREIGN replay harness depend on their presence and exact format.
Arena AI flagged them as missing in the Phase 5 PR -- a verbatim-fidelity
violation of the extraction protocol.

**Verify**:
- `grep -cn "TREND ORDERS PLACED" src/V12_002.Entries.Trend.cs` == 1
- `grep -cn "(EMA9) | Stop:"      src/V12_002.Entries.Trend.cs` == 1
- `grep -cn "(EMA15) | Stop:"     src/V12_002.Entries.Trend.cs` == 1
- All 3 Prints reside inside `ExecuteTRENDEntry` between the SubmitLeg2
  success return and `ExecuteTREND_DispatchSima` call (NOT inside any
  `ExecuteTREND_*` sub-handler).

---

### T3 -- Restore deduplication in flatten enqueue helpers (F-02: Fleet + Master parity)

**Pattern reference**: `_repairInFlight` (`src/V12_002.REAPER.cs` line 28)
+ `EnqueueReaperRepairCandidate` (`src/V12_002.REAPER.Audit.cs` lines 236-260)
+ cleanup-in-finally (`src/V12_002.REAPER.Repair.cs` lines 222-225).

**F-02 SCOPE NOTE (Arena AI emphasis)**: The dedupe guard MUST be applied
SYMMETRICALLY to BOTH enqueue helpers -- `EnqueueReaperFlattenCandidate`
(Fleet, step 3b) AND `EnqueueReaperMasterFlatten` (Master, step 3c).
Asymmetry here re-introduces the unbounded master-flatten re-enqueue
regression Arena AI flagged. Steps 3d/3e symmetrically clear the guard
for both Fleet and Master code paths -- no Master-side shortcut is
permitted.

**Step 3a -- Add the in-flight guard field**
File: `src/V12_002.REAPER.cs`
Insertion point: immediately after the `_repairInFlight` declaration (line 28),
inside the same `#region V12 REAPER Audit Logic`.
Add a `private readonly ConcurrentDictionary<string, byte> _reaperFlattenInFlight`
initialized to a new empty dictionary, with comment
`// [Phase 5 Repair] Mirrors _repairInFlight to dedupe flatten enqueues across audit cycles.`

**Step 3b -- Dedupe in `EnqueueReaperFlattenCandidate`**
File: `src/V12_002.REAPER.Audit.cs` (lines 262-266)
Replace body:
1. Compute `flattenKey = acct.Name + "_" + Instrument.FullName;`.
2. If `_reaperFlattenInFlight.TryAdd(flattenKey, 0)` returns `false`,
   `return false;` (already in-flight; skip enqueue and skip caller's
   `TriggerCustomEvent`).
3. Else `_reaperFlattenQueue.Enqueue(acct.Name); return true;`.

**Step 3c -- Dedupe in `EnqueueReaperMasterFlatten`**
File: `src/V12_002.REAPER.Audit.cs` (lines 449-453)
Same body shape as 3b but using `Account.Name + "_" + Instrument.FullName`
and `_reaperFlattenQueue.Enqueue(Account.Name);`.

**Step 3d -- Replace fragile `TryDequeue` rollback in caller catch handlers**
File: `src/V12_002.REAPER.Audit.cs`

Caller 1 -- inside `AuditSingleFleetAccount`, the catch block at lines 144-151
(reached when `TriggerCustomEvent` for fleet flatten throws). Remove the
`string _discarded; _reaperFlattenQueue.TryDequeue(out _discarded);` lines
and replace with
`_reaperFlattenInFlight.TryRemove(acct.Name + "_" + Instrument.FullName, out _);`.
Keep the existing Print message verbatim except change the trailing
`-- dequeued, will re-detect next cycle` to `-- in-flight cleared, will re-detect next cycle`.

Caller 2 -- inside `AuditMasterAccountIfNeeded`, the catch block at lines 373-380.
Remove `string _mDiscarded; _reaperFlattenQueue.TryDequeue(out _mDiscarded);`
and replace with
`_reaperFlattenInFlight.TryRemove(Account.Name + "_" + Instrument.FullName, out _);`.
Apply the same Print-message tail change.

**Step 3e -- Clear in-flight after the marshaled flatten completes**
File: `src/V12_002.REAPER.Audit.cs`
Method: `ProcessReaperFlattenQueue` (lines 479-505).
Inside the per-iteration `try { ... } catch { ... }` block, add a
`finally { _reaperFlattenInFlight.TryRemove(accountName + "_" + Instrument.FullName, out _); }`
clause so the guard is released on BOTH success and failure paths
(mirrors Repair.cs lines 222-225).

**Rationale**: Without dedupe, every Reaper audit tick (subsecond cadence)
re-enqueues the same account, growing `_reaperFlattenQueue` without bound
and repeatedly issuing market-close orders. The `if` wrapping at lines 141
and 370 was load-bearing, not stylistic.

**Verify**:
- `grep -n "_reaperFlattenInFlight" src/` returns exactly 5 hits
  (1 declaration in REAPER.cs + 2 `TryAdd` + 2 `TryRemove` + 1 `TryRemove` in finally = 6).
  Adjust expected count to 6 if finally added.
- `grep -n "_reaperFlattenQueue.TryDequeue" src/V12_002.REAPER.Audit.cs`
  returns ONLY the legitimate dequeue inside `ProcessReaperFlattenQueue`
  (line ~483). Both caller-catch dequeues are gone.
- `grep -n "lock(" src/V12_002.REAPER*.cs` == 0 hits.

---

### T4 -- Apply `ValidateIpcMultiplier` to T1 branch

**File**: `src/V12_002.UI.IPC.Commands.Config.cs`
**Method**: `TryApplyConfigTarget_Value` (line 136)
**Action**: Rewrite the T1 branch (line 138) to mirror the T2 branch
shape (lines 140-148): `double.TryParse`, then call
`ValidateIpcMultiplier(v, out vmReason)`. On failure
`Print($"[IPC REJECT] T1 value {v} rejected: {vmReason}");`.
On success `Target1Value = v;`. Return `true` to preserve
the dispatch-table semantics.

**Rationale**: T1 currently bypasses the domain guard, allowing
zero/negative multipliers to invert target prices (per
`ValidateIpcMultiplier` comment, `src/V12_002.UI.IPC.cs` lines 102-105).

**Verify**:
- T1 branch now mirrors T2-T5 structure.
- `grep -cn "ValidateIpcMultiplier" src/V12_002.UI.IPC.Commands.Config.cs` >= 5
  (one per T1-T5 + STR).

---

### T5 -- Cache `acct.Positions` lookup result in REAPER audit loop (F-05)

**File**: `src/V12_002.REAPER.Audit.cs`
**Methods**:
- `AuditSingleFleetAccount` (line 51) -- queries
  `acct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName)`
  at line 53.
- `AuditFleet_CalculateExpectedActual` (line 183) -- queries the SAME
  predicate against the SAME `acct.Positions` enumerable at line 189.

**Action (cache pattern -- single LINQ scan per audit tick)**:
1. Add `out Position pos` to the END of the
   `AuditFleet_CalculateExpectedActual` parameter list (lines 183-188).
   Inside, the existing line 189 `FirstOrDefault` becomes the SINGLE
   source of truth -- assign its result into the new `out pos` parameter.
2. In `AuditSingleFleetAccount`, DELETE the line 53 query. Declare a
   local `Position pos;` (cache slot) and pass it BY OUT to
   `AuditFleet_CalculateExpectedActual` as the new last argument.
3. The downstream usage at line 166
   (`EnqueueReaperNakedStopCandidate(acct, pos, actualQty, expectedKey, shouldLog)`)
   now reads from the cached `pos` -- no second `FirstOrDefault`
   traversal of `acct.Positions`.

**Rationale (Arena AI F-05)**: `acct.Positions` is a NinjaTrader broker
collection; iterating it twice per audit tick (subsecond cadence) doubles
the broker-side enumeration cost and adds GC pressure on the per-call
predicate delegate allocation. Caching the result honors the V12
zero-allocation bias and matches the established cache pattern already
used in `AuditFleet_CheckWorkingStop` (line 271, `var orders = acct.Orders.ToArray();`).

**Scope clarification**: F-05 covers ONLY the duplicate scan within the
fleet audit loop. `AuditMasterAccountIfNeeded` already performs a single
`Account.Positions.FirstOrDefault(...)` call at line 347 -- no caching
change required there.

**Verify**:
- `grep -cn "acct.Positions.FirstOrDefault" src/V12_002.REAPER.Audit.cs` == 1
  (cached, single call site inside `AuditFleet_CalculateExpectedActual`).
- `grep -cn "Account.Positions.FirstOrDefault" src/V12_002.REAPER.Audit.cs` == 1
  (Master path, unchanged).
- Cached `pos` reaches `EnqueueReaperNakedStopCandidate` unchanged; audit
  semantics are byte-identical to the pre-cache double-scan version.

---

### T6 -- Brace standardization for single-line control structures

**Scope**: ONLY the four files modified above. Do NOT touch other files.

**Files**:
- `src/V12_002.Entries.Trend.cs`
- `src/V12_002.REAPER.cs`
- `src/V12_002.REAPER.Audit.cs`
- `src/V12_002.UI.IPC.Commands.Config.cs`

**Action**: For every `if`, `else`, `else if`, `for`, `foreach`, `while`,
`do`, `using` statement whose body is a single statement WITHOUT braces,
wrap the body in `{ }` using the file's existing K&R Allman convention
(open brace on next line). Apply ONLY to violations Codacy already flags.

**Codacy hot zones to fix** (non-exhaustive checklist):
- `Entries.Trend.cs`: 60, 67, 89, 120, 121, 140, 142, 528, 555, 572, 574.
- `REAPER.Audit.cs`: 27, 36, 80, 138, 140, 156, 232, 257, 365, 369, 416,
  434, 444, plus any new single-line returns introduced in T3.
- `UI.IPC.Commands.Config.cs`: 104, 111, 113, 116, 117, 192, 227, 228.

**Diff hygiene constraint (AGENTS.md)**:
- Touch ONLY the lines that gain braces. Do NOT reflow indentation of
  unaffected lines. Do NOT change line endings.
- Keep total PR diff under 150 KB. If brace insertion alone approaches
  the limit, split into a follow-up PR (`phase-5-part-3-braces`)
  and report immediately.

**Verify**:
- Codacy "Always use braces" rule: 0 hits in the four files.
- `git diff --stat HEAD` consistent with brace-only adds (no whitespace
  mutation in unrelated lines).

---

## 5. VERIFICATION SEQUENCE (after ALL tickets)

```text
1. ASCII gate:
   python check_ascii.py src/V12_002.Entries.Trend.cs `
                          src/V12_002.REAPER.cs `
                          src/V12_002.REAPER.Audit.cs `
                          src/V12_002.UI.IPC.Commands.Config.cs

2. Lock-free gate:
   grep -rn "lock(" src/   -- must be zero hits in modified files

3. Dead-code / timezone gate (T1, T2 -- F-01a / F-01b):
   grep -cn "CurrentBar < 20"  src/V12_002.Entries.Trend.cs   -- 1
   grep -cn "DateTime.Now"     src/V12_002.Entries.Trend.cs   -- 0

3b. Verbatim log restoration gate (T2b -- F-06):
    grep -cn "TREND ORDERS PLACED" src/V12_002.Entries.Trend.cs   -- 1
    grep -cn "(EMA9) | Stop:"      src/V12_002.Entries.Trend.cs   -- 1
    grep -cn "(EMA15) | Stop:"     src/V12_002.Entries.Trend.cs   -- 1

4. Flatten dedupe gate (T3 -- F-02 -- Fleet AND Master):
   grep -n  "_reaperFlattenInFlight" src/   -- decl + Fleet TryAdd + Master TryAdd + 2 catch TryRemove + finally TryRemove
   grep -cn "_reaperFlattenQueue.TryDequeue" src/V12_002.REAPER.Audit.cs   -- 1
   (i.e., the only remaining TryDequeue is the legitimate one inside ProcessReaperFlattenQueue;
    both caller-catch dequeues are gone and replaced with TryRemove on _reaperFlattenInFlight)

5. Validation gate (T4 -- F-03):
   grep -cn "ValidateIpcMultiplier" src/V12_002.UI.IPC.Commands.Config.cs  -- >= 5

6. LINQ cache gate (T5 -- F-05):
   grep -cn "acct.Positions.FirstOrDefault"     src/V12_002.REAPER.Audit.cs   -- 1 (Fleet, cached single source)
   grep -cn "Account.Positions.FirstOrDefault"  src/V12_002.REAPER.Audit.cs   -- 1 (Master, unchanged)

7. Hard-link sync (mandatory per AGENTS.md):
   powershell -File .\deploy-sync.ps1   -- must EXIT 0

8. Build:
   dotnet build .\Linting.csproj   -- zero new errors / warnings

9. Tests:
   dotnet test  .\Testing.csproj   -- all green

10. Lint pillar:
    powershell -File .\scripts\lint.ps1
    Re-run Codacy / DeepSource locally if available; verify all five
    regression categories close.
```

## 6. DIRECTOR'S HANDOFF BLOCK (For P5 ENGINEER -- Codex / Jules)

```text
@ENGINEER (Codex / Jules) - P5 Surgical Execution
TASK:    Phase 5 God Function Extraction Repairs
BUILD:   1111.006-phase-5-part-2
BRANCH:  phase-5-part-2

Execute tickets T1, T2, T2b, T3, T4, T5, T6 IN ORDER. Each ticket has a
Verify gate; do NOT proceed to the next ticket until the current Verify
gate passes.

Arena AI emphasis (NON-NEGOTIABLE):
  - T2b restores 3 verbatim Print logs in ExecuteTRENDEntry (F-06).
    Strings must be byte-identical to the originals listed in the ticket.
  - T3 applies the _reaperFlattenInFlight dedupe guard SYMMETRICALLY to
    BOTH EnqueueReaperFlattenCandidate (Fleet) AND EnqueueReaperMasterFlatten
    (Master) (F-02). No Master-side shortcut is permitted.
  - T5 establishes a single-source cache for acct.Positions lookup --
    one FirstOrDefault per audit tick, plumbed via out Position pos (F-05).

Touch ONLY:
  src/V12_002.Entries.Trend.cs
  src/V12_002.REAPER.cs
  src/V12_002.REAPER.Audit.cs
  src/V12_002.UI.IPC.Commands.Config.cs

V12 Platinum constraints (NON-NEGOTIABLE):
  - NO lock() additions. Use the existing _repairInFlight
    ConcurrentDictionary pattern as the template.
  - ASCII-only string literals (no Unicode, no curly quotes, no emoji).
  - NO new public methods. NO new fields outside the single
    _reaperFlattenInFlight added in T3a.
  - NO whitespace mutation outside the explicit brace insertions
    enumerated in T6.
  - Diff under 150 KB total. If T6 alone overflows, split into a
    follow-up PR (phase-5-part-3-braces) and report.

  1. Re-run ALL Section 5 verification gates in order
     (including new gate 3b for F-06 verbatim log restoration and the
      Fleet/Master split in gates 4 and 6).
  2. powershell -File .\deploy-sync.ps1   -- hard-link sync (mandatory).
  3. powershell -File .\scripts\lint.ps1   -- Codacy / DeepSource close-out.
  4. Push to phase-5-part-2 and request CI re-run; confirm Arena AI
     re-audit closes F-01a, F-01b, F-02 (Fleet+Master), F-03, F-04, F-05,
     and F-06.

Report back:
  - Per-ticket Verify gate output (one line per gate).
  - Final grep counts for the six gates in Section 5.
  - deploy-sync.ps1 exit code.
  - dotnet build / test summary.
  - Codacy / DeepSource issue delta vs prior CI run.
```
