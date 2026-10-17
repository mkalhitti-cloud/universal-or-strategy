# B198 PTT Pipeline — Orchestrator Prompt
**Created**: 2026-10-13 | **Diagnosis**: docs/brain/B198-diagnosis.md

Paste this entire prompt into a fresh `ptt-orchestrator` session.

---

## CONTEXT RESTORE (MANDATORY — READ BEFORE ANYTHING ELSE)

You are the B198 lane orchestrator. B120–B197 are complete. FINAL_PASS issued for B197 on 2026-10-12.

**Authoritative deferred backlog**: `docs/brain/B197/06-deferred-backlog.md`

**SIM test session confirmed the following bugs (2026-10-11)**:
- Leader=Sim101, Followers=Sim102/103/104, Clone mode, MGC DEC26, ATM template: MGC $400 SL4.
- ATM template: qty=10 total (2 groups: Target1=5 + Target2=5). Stop names in NT8 SIM: **SimStop1** and **SimStop2**.
- Bugs confirmed by output.md (`specs/output.md`) and screenshots.

**Pre-pipeline diagnosis** completed in copier-spec session. Read it before starting:
`docs/brain/B198-diagnosis.md`

Key pre-confirmed facts (do NOT re-investigate, embed directly in DW cards):

**MEGACOMMIT AUDIT RESULT**: SimStop support NEVER existed in any commit. No match in
`git log --all --oneline --grep="SimStop"` or `git diff 878a5b33~1 878a5b33 | grep SimStop`.
DW-B198-SIMSTP-01 is a NEW FIX (not a restore ticket).

**DW-B194-04 ALREADY IMPLEMENTED**: `_flattenFilledTicks` field exists at CopyEngine.cs line 382.
Debounce guard at FlattenOneAccount line 5363. Comment on line 381 explicitly says "DW-B194-04: closed."
Do NOT include a PTT-Flatten debounce ticket in B198. It is done.

**SIMSTP PREDICATE STATE** (verified in source, no SimStop anywhere):
- `IsAtmBracketName` line 858: only "Stop" + digit[4] and "Target" + digit[6]
- `IsNativeExitName` line 2564: "Close", "Flatten", StartsWith("Rev"), StartsWith("Exit")
- `IsBracketLeg` line 5880: `StartsWith("Stop")` || `StartsWith("Target")`
- `IsBracketLegStatic` line 5861: Stop/Target/PTT-/EndsWith(STP) — no SimStop
- `IsAtmSTPOrder` line 3044: STP/Stop/Target/PTT-STP-Drag-/PTT-TGT-Drag- — no SimStop
- `IsStopLeg` line 5848: FromEntrySignal/StartsWith("Stop")/EndsWith("STP") — no SimStop

**ADDITIONAL HARDCODINGS CONFIRMED (Director requirement: any N brackets)**:
- `TryParseStopSuffix` line 3382: `Substring(4)` → "SimStop1".Substring(4)="top1" → TryParse fails → SimStop drag broken
- `TryParseStopSuffix` line 3383: `n > 3` cap → Stop4/Stop5+ silently rejected → >3 groups broken
- `CaptureOtherLegTargetPrices` line 3350/3355: `new double[3]` + `i <= 3` → legs 4+ never captured
- `FindLeaderCollateralOrder` line 3409: builds "Stop"+suffix only → never finds "SimStop"+suffix leader orders
- T1 fix MUST address all of Part A (6 predicates) AND Part B (suffix parser + drag infra)

**QTY BUG PATH CONFIRMED**: `DispatchCopy`→`TryLaneDHook`→`TryLaneDDispatchFollower`→`SendCopyWithAtm`.
`scaledSig.Quantity = baseQty * mult` where `baseQty = order.Quantity` (leader Entry).
NT8 behavior of `StartAtmStrategy` with qty=10 on a 2-group template is NOT documented in
NT8_FULL_REFERENCE.md or NT8_ADDON_KNOWLEDGE.md. Architect must instrument and test.

---

## B198 PIPELINE MANDATE (all phases mandatory — none skippable — none combinable)

ALL src/PropTraderTools/*.cs edits MUST go through the FULL 5-PHASE PTT PIPELINE:

```
Ph1  ptt-architect       → docs/brain/B198/02-architecture-plan.md
Ph2  ptt-plan-reviewer   → docs/brain/B198/02-plan-review.md        [REVIEW_PASS gate]
Ph3  ptt-architect       → docs/brain/B198/04-tickets.md
Ph3.5 ptt-ticket-reviewer → docs/brain/B198/04-ticket-review.md     [TICKET_REVIEW_PASS gate]
Ph4a ptt-engineer        → src .cs edits + docs/brain/B198/ticket-N-completion.md
Ph4b ptt-verifier        → docs/brain/B198/ticket-N-verification.md [VERIFY_PASS gate]
Ph5  ptt-plan-reviewer   → docs/brain/B198/05-final-review.md
                         + docs/brain/B198/06-deferred-backlog.md
```

ALL ARTIFACTS IN: `docs/brain/B198/`
NO SHORTCUTS. NO PHASE SKIPPING. NO PHASE COMBINING.

---

## B198 TICKET SCOPE (4 tickets — revised from prior context-restore)

### TICKET 1 — P0: SimStop Bracket Support — N-Bracket Generalisation (DW-B198-SIMSTP-01)

**Director requirement**: Fix must work for ANY number of ATM bracket groups (1, 2, 3, 4, 5...),
not only 2. The SIM test used 2 groups (SimStop1+SimStop2), but the fix must be N-general.

**Root cause confirmed**: NT8 SIM ATM template "MGC $400 SL4" uses stop order names "SimStop1"
and "SimStop2" (NOT "Stop1"/"Stop2"). All PTT bracket predicates use `StartsWith("Stop")` which
does NOT match "SimStop". This causes:
1. `IsNativeExitName("SimStop1") = false` → `DispatchCopy` fires for each SimStop fill → followers
   receive Sell copies → oversold → SHORT position → loop.
2. `IsAtmBracketName("SimStop1") = false` → gate consistency failures.
3. `IsWorkingBracket("SimStop1") = false` → log shows "IsWorkingBracket=False name=SimStop1".
4. Drag handling does not route SimStop via stop path.

**TWO-PART FIX REQUIRED**:

---

**PART A — 6 Predicate Fixes** (any-N safe: they use prefix match, not suffix bounds)

Each is a one-clause OR addition. CYC impact: +1 per method. All stay ≤8.

```
A. IsNativeExitName (line 2564) — CRITICAL for reverse fix:
   Add as new branch before `return false`:
     if (name.StartsWith("SimStop", StringComparison.Ordinal)) return true;
   Effect: TryDispatchLeaderFlat treats SimStop fills as native exit → DispatchCopy bypassed.
   This alone stops the double-Sell-copy → reverse entry cascade.

B. IsAtmBracketName (line 858) — gate consistency:
   Add to the compound OR expression:
     || (name.StartsWith("SimStop", StringComparison.Ordinal)
         && name.Length > 7 && char.IsDigit(name[7]))
   NOTE: char.IsDigit(name[7]) correctly matches SimStop1..SimStop9 (single-digit).
   For multi-digit groups (SimStop10+): char.IsDigit handles it — TryParse in
   TryParseStopSuffix handles the count. This guard is prefix-discriminator only.

C. IsBracketLeg (line 5880) — CancelOneAccount guard:
   Add to OR:
     || order.Name.StartsWith("SimStop", StringComparison.Ordinal)
   Effect: CancelOneAccount does NOT cancel SimStop bracket orders.

D. IsBracketLegStatic (line 5861) — static IsWorkingBracket:
   Add to OR:
     || order.Name.StartsWith("SimStop", StringComparison.Ordinal)
   Effect: IsWorkingBracket and HasWorkingEntries recognise SimStop brackets.

E. IsAtmSTPOrder (line 3044) — stop drag path:
   Add to OR:
     || order.Name.StartsWith("SimStop", StringComparison.Ordinal)
   Effect: HandleAtmStopSync routes SimStop via stop cancel+resubmit path.

F. IsStopLeg (line 5848) — TryHandleDrag routing:
   Add to OR:
     || (order.Name != null && order.Name.StartsWith("SimStop", StringComparison.Ordinal))
   Effect: TryHandleDrag classifies SimStop as a stop leg (not a target leg).
```

---

**PART B — Suffix Parsing and Drag Infrastructure Fixes** (hardcoded to "Stop" prefix + max 3 brackets)

The drag sync infrastructure has TWO independent hardcodings that break for SimStop AND for >3 groups.
Both must be fixed in T1 to satisfy the "any N" requirement.

**B1 — `TryParseStopSuffix` (line 3377-3387)**:

Current code:
```csharp
private static bool TryParseStopSuffix(string stopName, out string suffix)
{
    suffix = null;
    if (stopName == null || stopName.Length < 5)   // assumes prefix = 4 chars ("Stop")
        return false;
    string raw = stopName.Substring(4);            // always slices after "Stop"
    if (!int.TryParse(raw, out int n) || n < 1 || n > 3)  // caps at 3
        return false;
    suffix = raw;
    return true;
}
```

Problems:
- `Substring(4)` → "SimStop1".Substring(4) = "top1" → TryParse fails → SimStop drag is silently broken
- `n > 3` → Stop4, Stop5... are silently dropped → N > 3 bracket drag broken

Fix: support both "Stop" (prefix len 4) and "SimStop" (prefix len 7). Remove the n > 3 cap.
Replacement:
```csharp
private static bool TryParseStopSuffix(string stopName, out string suffix)
{
    suffix = null;
    if (string.IsNullOrEmpty(stopName))
        return false;
    int prefixLen;
    if (stopName.StartsWith("SimStop", StringComparison.Ordinal))
        prefixLen = 7;
    else if (stopName.StartsWith("Stop", StringComparison.Ordinal))
        prefixLen = 4;
    else
        return false;
    if (stopName.Length <= prefixLen)
        return false;
    string raw = stopName.Substring(prefixLen);
    if (!int.TryParse(raw, out int n) || n < 1)
        return false;
    suffix = raw;
    return true;
}
```
CYC change: 3 → 5. Stays ≤8. JS-021: no lock. JS-001: no throw.
NOTE: The `n > 3` cap is REMOVED. Callers that used suffix "1"/"2"/"3" still work — suffix
is a string key, and callers only loop to 3 elsewhere. See B2 below for those callers.

**B2 — `CaptureOtherLegTargetPrices` (line 3348-3371)**:

Current code hardcodes `for (int i = 1; i <= 3; i++)` and `new double[3]`.
With >3 bracket groups this silently drops all legs beyond leg 3.

Fix: replace the hardcoded cap with a dynamic approach. The correct maximum is the number of
active bracket orders on the account for the instrument. Architect must read the full method
body and determine the minimal change. Minimum acceptable fix:
- Replace `new double[3]` → `new double[MaxBracketLegs]` where `MaxBracketLegs = 9` (NT8 ATM max).
- Replace `i <= 3` → `i <= MaxBracketLegs`.
- Increase PTT-TGT-Drag-N lookup accordingly (currently only tries N=1..3).

`const int MaxBracketLegs = 9;` at method or class level — no magic number (JS-082 analog).
CYC change: 0 (loop bound is not a branch). Array size change only. Safe.

**B3 — `FindLeaderCollateralOrder` (line 3405-3410)**:

Builds `"Stop" + suffix` and `"Target" + suffix` to look up leader orders.
With SimStop ATM, leader orders are named "SimStop1" not "Stop1".
Fix: also try `"SimStop" + suffix` lookup if `"Stop" + suffix` is not found.
Architect must read the full method body (grep for it at line ~3405) and add a third search:
```csharp
// After existing Stop + Target lookup falls through:
string simStopName = "SimStop" + suffix;
// search acc.Orders for simStopName
```
CYC change: +1 (new if branch). Must stay ≤8. Check current CYC before adding.

**B4 — `CaptureLinkedTargetPrice` (called at line 3117)**:

Architect MUST grep for `CaptureLinkedTargetPrice` and read the method body.
It is called with `leaderOrder.Name` (e.g. "Stop1" or "SimStop1").
If it uses TryParseStopSuffix internally: the B1 fix above covers it automatically.
If it has its own StartsWith("Stop") hardcoding: add SimStop clause there too.
Confirm and document in the architecture plan.

---

**GREP MANDATE**: Architect MUST run this grep before finalising T1 scope:
```
grep -n "StartsWith(\"Stop" src/PropTraderTools/CopyEngine.cs
grep -n "Substring(4)"      src/PropTraderTools/CopyEngine.cs
grep -n "n > 3"             src/PropTraderTools/CopyEngine.cs
grep -n "i <= 3"            src/PropTraderTools/CopyEngine.cs
grep -n "double\[3\]"       src/PropTraderTools/CopyEngine.cs
grep -n "\"Stop\" +"        src/PropTraderTools/CopyEngine.cs
```
Any site not in the known list above that gates bracket behaviour MUST get a SimStop fix.

---

**Tests**: New `B198Tests.cs`. Test each predicate and the suffix parser with SimStop input
AND with suffix > 3 input.

Minimum test cases:
1. `IsAtmBracketName("SimStop1")` → true
2. `IsAtmBracketName("SimStop9")` → true
3. `IsAtmBracketName("SimStop")` → false (no digit)
4. `IsNativeExitName("SimStop1")` → true
5. `IsNativeExitName("SimStop99")` → true
6. `TryParseStopSuffix("SimStop1", out s)` → true, s="1"
7. `TryParseStopSuffix("SimStop2", out s)` → true, s="2"
8. `TryParseStopSuffix("SimStop4", out s)` → true, s="4" (was rejected before, n>3)
9. `TryParseStopSuffix("Stop4", out s)` → true, s="4" (was rejected before)
10. `TryParseStopSuffix("Stop3", out s)` → true, s="3" (regression: was passing)
11. `TryParseStopSuffix("SimTarget1", out s)` → false (SimTarget not a stop leg)
12. IsBracketLeg/IsBracketLegStatic/IsAtmSTPOrder/IsStopLeg — same pattern from B197Tests.cs.

`TryParseStopSuffix` is `private static` — use source-text assertion unless a testable wrapper
already exists. `IsAtmBracketName` and `IsNativeExitName` are `internal static` — call directly.

---

### TICKET 2 — P0: QTY Bug Investigation + Fix (DW-B198-QTY-01)

**Symptom**: Director SIM test 2026-10-11: Leader=Long 10, Followers=Long 5. Exact 2:1 ratio.
**Code path confirmed**: `DispatchCopy` → `TryLaneDHook` → `TryLaneDDispatchFollower` (line 2749-2756) →
`scaledSig.Quantity = baseQty * mult` → `SendCopyWithAtm` → `CreateOrder(qty=signal.Quantity)` →
`StartAtmStrategy(namedMode.AtmObject, order)`.

**OPEN QUESTION**: Does `StartAtmStrategy(qty=10, "MGC $400 SL4")` produce:
- H1: Position qty=10 (entry fills for 10, with 2 SimStop × 5 brackets each), OR
- H2: Position qty=5 (NT8 ignores passed qty, uses template's per-group qty=5 for the entry), OR
- H3: NT8 splits qty=10 into 2 × 5 and arm two groups (correct total=10, but entry fills per group)

**Architect MUST instrument before designing the fix**:
1. Add ONE log line to `SendCopyWithAtm` before the `CreateOrder` call:
   `StatusUpdate?.Invoke(acc.Name + ": ATM-send qty=" + signal.Quantity + " tmpl=" + namedMode.TemplateName);`
2. Add ONE log line after `CreateOrder`:
   `if (order != null) StatusUpdate?.Invoke(acc.Name + ": ATM-order qty=" + order.Quantity);`
3. Run ONE SIM entry with leader qty=10. Observe log.
4. Record the qty at each log point. This pins the exact conversion.

**Fix design depends on log evidence**:
- If qty=10 reaches CreateOrder BUT position shows 5 → NT8 StartAtmStrategy behavior.
  Fix: redesign to pass per-group qty (= totalQty / templateGroupCount). Requires reading
  template XML or accepting `_cloneAtmGroupCount` UI config field (new field, P2).
- If qty arrives as 5 at CreateOrder → upstream reduction. Find the exact reduction site
  and fix there.
- If qty=10 produces Long 10 in new SIM run → screenshot was a different scenario. Confirm
  and close DW-B198-QTY-01 as "cannot reproduce with instrumentation."

**Note**: This ticket may be split into T2a (instrument+diagnose) and T2b (implement fix)
if the architect determines the fix requires a new field or complex ATM template parsing.
Architect MUST propose the split to the plan reviewer in Ph2 if warranted.

---

### TICKET 3 — P1: fo=NULL on Drain Path (DW-B195-01)

**DW card**: DW-B195-01 (originally DW-B167-DEFER-02, open since 2026-09-18).

**Symptom**: `[TP4-SFB] acc=Sim102 fo=NULL followerOrders=[...]` in output.md line 95-96.
`SyncFollowerBracket` called with fo=NULL for Sim102/103. This means when a drain-path
entry fills, `PendingDispatchDrain` does not carry the follower entry order reference,
so `SyncFollowerBracket` cannot find the follower order.

**Fix**: In `PendingDispatchDrain` struct, add a nullable `Order FollowerOrder` field.
In `SubmitDrainedEntry`, after `SendCopyWithAtm` returns (the follower entry order),
store the created order in the drain payload. When `TrySetSlateReadyOnDrainFill` resolves
the drain, pass the stored `FollowerOrder` to `SyncFollowerBracket` instead of null.

**Architect MUST**:
1. Read the current `PendingDispatchDrain` struct definition (grep for it in CopyEngine.cs).
2. Read `SubmitDrainedEntry` (grep for it).
3. Read `TrySetSlateReadyOnDrainFill` (line 1070 per B197 landmark).
4. Read `SyncFollowerBracket` (grep for it) to understand what fo parameter is expected.
5. Design the minimal field addition that threads the order reference through.

**CYC constraint**: `SubmitDrainedEntry` and `TrySetSlateReadyOnDrainFill` must each remain ≤8.
If either is at ceiling, extract a helper before adding the new assignment branch.

---

### TICKET 4 — P2: ATM Template Dir Startup Fix (DW-B198-ATMDIR-01)

**Log evidence**: `[PTT-STARTUP-INFO] ATM template dir not found -- skipping Reverse check`
appears at output.md line 1 and line 520 (fires on every startup).

**Fix**: Find the startup code that constructs the ATM template XML directory path. Common
NT8 ATM template path: `Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) +
@"\NinjaTrader 8\templates\AtmStrategy\"`. Correct the path. Add:
- A warning log if dir not found after correction: `[PTT-WARN] ATM template dir missing: {path}`
- A warning log if any template has Reverse=true: `[PTT-WARN] ATM template {name} has Reverse=true — may cause unintended position reversal`

**CYC**: The startup method may be in TradeCopierPanel.cs or CopyEngine.cs. Architect must
grep for "ATM template dir" log string to find the caller site. CYC ≤ 8 applies.

---

## B198 ARTIFACTS CHECKLIST (all required before FINAL_PASS)

```
docs/brain/B198/
  02-architecture-plan.md   (Ph1 output — ptt-architect)
  02-plan-review.md         (Ph2 output — ptt-plan-reviewer, REVIEW_PASS)
  04-tickets.md             (Ph3 output — ptt-architect)
  04-ticket-review.md       (Ph3.5 output — ptt-ticket-reviewer, TICKET_REVIEW_PASS)
  ticket-1-completion.md    (Ph4a — ptt-engineer, T1)
  ticket-1-verification.md  (Ph4b — ptt-verifier, T1, VERIFY_PASS)
  ticket-2-completion.md    (Ph4a — ptt-engineer, T2)
  ticket-2-verification.md  (Ph4b — ptt-verifier, T2, VERIFY_PASS)
  ticket-3-completion.md    (Ph4a — ptt-engineer, T3)
  ticket-3-verification.md  (Ph4b — ptt-verifier, T3, VERIFY_PASS)
  ticket-4-completion.md    (Ph4a — ptt-engineer, T4)
  ticket-4-verification.md  (Ph4b — ptt-verifier, T4, VERIFY_PASS)
  05-final-review.md        (Ph5 — ptt-plan-reviewer)
  06-deferred-backlog.md    (Ph5 — ptt-plan-reviewer, supersedes B197 backlog)
```

---

## B198 SIM GATE (after FINAL_PASS, before marking B198 complete)

Record results in `docs/brain/B198/sim-gate-completion.md`.

Setup: Leader=Sim101, Followers=Sim102/103/104, Clone mode, MGC DEC26, MGC $400 SL4 template.

**SIM-1 (T1 — SimStop fix)**:
1. Place ATM entry qty=10.
2. SIM log must show ZERO `"IsWorkingBracket=False name=SimStop"` entries.
3. PASS: All 4 accounts Long N (whatever qty). ZERO reverse Short on any follower after stop trigger.
4. Let leader SimStop trigger. Confirm followers flatten cleanly. NO double Sell copy in log.
5. PTT-Flatten fires ≤ 1 time per follower after stop (not 20+).

**SIM-2 (T2 — QTY fix, if fix was possible)**:
1. Place ATM entry. Leader qty=10.
2. Confirm all followers have same position qty as leader (qty=10 if H1/H3 fix applied).
3. Log must show `"ATM-send qty=10"` reaching SendCopyWithAtm.
4. If QTY fix was deferred (new field not yet added): record the log evidence for the deferred fix design.

**SIM-3 (T3 — fo=NULL fix)**:
1. Drag leader entry 2-3x. `[DRAIN]` fires for all followers each drag.
2. Let leader fill. SIM log must show ZERO `"fo=NULL"` in TP4-SFB log lines.
3. All SyncFollowerBracket calls have `fo=<order>` (not fo=NULL).

**SIM-4 (T4 — ATM dir)**:
1. Restart PTT. Log must show ZERO `"ATM template dir not found"`.
2. If template has Reverse=true: `[PTT-WARN] ATM template ... Reverse=true` fires.

---

## DEFERRED WORK ITEMS CARRIED INTO B198 (do not reopen in pipeline)

The following are OPEN in B197 backlog and remain unaddressed by B198 scope:
- DW-B183-03 (P1): IsLaneEEligible CYC=8 frozen
- DW-B192-SINST-01 (P2): SetInstrument() CYC=8 frozen
- DW-B193-01 (P3): OnQuickAllBufferChanged old label strings
- DW-B197-SIM-01 (P1): B197 SIM gate not run
- All other items in docs/brain/B197/06-deferred-backlog.md

These carry forward unchanged into B198/06-deferred-backlog.md. Do NOT open new tickets for them.

---

## JANE STREET COMPLIANCE REMINDER

Before writing any ticket design:
1. Read `docs/standards/jane-street/RULES_CATALOG.md` (P0 gate)
2. Confirm zero P0 violations (lock(), async void, return null) in files touched
3. All new fields: `private readonly ConcurrentDictionary` — no lock()
4. All new method CYC ≤ 8 (JS-066 ceiling)
5. Tests: xUnit [Fact] only — no NUnit, no MSTest
6. ASCII-only in all C# string literals and log messages
