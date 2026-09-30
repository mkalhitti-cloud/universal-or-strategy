# BWAVE-NEXT Lane B -- Final Review

## Header

- **Epic**: BWAVE-NEXT Lane B -- Cancel-Before-Dispatch Drain + Post-PR-42 Repairs
- **Reviewer**: ptt-plan-reviewer (Phase 5)
- **Date**: 2026-09-04
- **Status**: FINAL_PASS

---

## Summary

Two code tickets implemented and verified in BWAVE-NEXT Lane B:

| Ticket | DW Items | Description | Verdict |
|--------|----------|-------------|---------|
| T1 | DW-NEXT-A-07 + DW-NEXT-A-06 | ActiveOrders thread safety (`.ToList()`) + TickCount cast fix `(long)(int)` | VERIFY_PASS |
| T2 | DW-NEW-08 Option D | Cancel-before-dispatch drain: `PendingDispatchDrain`, `DrainThenDispatch`, `OnDrainCancelAck`, `SubmitDrainedEntry`, `SubmitEntryDirect`, `TryDrainWatchdog` | VERIFY_PASS |
| T3 | DW-NEXT-A-01 + DW-NEXT-A-02 | Documentation housekeeping | Director action only (no pipeline) |

Pre-requisite: LaneA commit `92a44332` merged to `main` during T1 execution (LaneA had FINAL_PASS).

---

## FR Checks

### FR-01 ALL VERIFY_PASS

**PASS**

- `ticket-1-verification.md` Status: VERIFY_PASS (header line 7 and Final Verdict)
- `ticket-2-verification.md` Status: VERIFY_PASS (header line 7 and Final Verdict)

Both contain explicit VERIFY_PASS at header and final verdict. Independent Layer 3 verification
performed for each ticket. All Layer 2 vs Layer 3 cross-checks confirmed with zero discrepancies
(T2) or only non-material line-number shift and pre-existing warning (T1).

---

### FR-02 SPEC COMPLETENESS T1

**PASS**

| AC | Requirement | Evidence |
|----|-------------|----------|
| ActiveOrders .ToList() | AMBIGUOUS-ADDED-TOLIST determination applied; `.ToList()` at end of ActiveOrders body | Verifier Step 1b: line 3441 confirmed. Return type `IEnumerable<Order>` unchanged. CYC=1. |
| (long)(int) cast | Applied at ALL TickCount-to-long reads in NakedPositionDetector | Verifier Step 1d: line 6439 confirmed. Only 1 hit in scope. No unpatched reads remain. |
| 2 [Fact] tests pass | `ActiveOrders_ThreadSafetyVerification` + `NakedDetector_DebounceField_UsesLongArithmetic` | Verifier Step 3b: Failed: 0, Passed: 2 |
| NT8 sync 18/18 OK | ptt-sync-and-verify.ps1 | Verifier Step 3c: 18/18 OK, 0 MISMATCH |

All T1 acceptance criteria from mission-brief + 04-tickets.md satisfied.

---

### FR-03 SPEC COMPLETENESS T2

**PASS**

| AC | Requirement | Evidence |
|----|-------------|----------|
| PendingDispatchDrain sealed class | All 9 required fields/properties | Verifier Step 7 AC-01: line 6650, 9 fields confirmed at lines 6652-6660 |
| _pendingDispatchDrains readonly | ConcurrentDictionary<string,PendingDispatchDrain>, StringComparer.Ordinal | Verifier Step 7 AC-02: line 379, after _nakedDetectLastQueuedTicks (line 373) |
| DrainThenDispatch | Present, CYC=4, Account.Cancel() only, logs [DRAIN] | Verifier SCAN-05: line 6496, CYC=4. No Account.Change(). [DRAIN] at line 6548 |
| OnDrainCancelAck | Present, CYC=3, sync void, Interlocked.Decrement | Verifier SCAN-05: line 6589, CYC=3. Interlocked.Decrement at line 6594 |
| SubmitDrainedEntry | Present, CYC<=3, TryRemove, calls SubmitEntryDirect | Verifier SCAN-05: line 6608, CYC=2 outer (3 incl. delegated). [DRAIN-SUBMIT] via SubmitEntryDirect |
| TryDrainWatchdog | Present, CYC=3, 2000L threshold, [DRAIN-TIMEOUT], no submit | Verifier SCAN-05: line 6629, CYC=3. >2000L at line 6637. [DRAIN-TIMEOUT] at line 6640. No submit call. |
| HandleEntryChange modified | Cancel+create+submit block replaced with DrainThenDispatch call, CYC=6 | Verifier Step 4: line 3717. CYC=6 (from 7). DrainThenDispatch at line 3750 |
| OnOrderUpdate modified | +1 drain-ack branch + unconditional TryDrainWatchdog, CYC=7 | Verifier Step 3: lines 1412-1416 (+1), line 1420 (watchdog). Both BEFORE Gate 1 |
| Log markers | [DRAIN], [DRAIN-SUBMIT], [DRAIN-TIMEOUT] all present | Verifier Step 1g: lines 6548, 6582, 6640 |
| 3 [Fact] tests pass | DrainThenDispatch, OnDrainCancelAck, DrainWatchdog | Verifier Step 6b: Failed: 0, Passed: 3 |
| NT8 sync 18/18 OK | ptt-sync-and-verify.ps1 | Verifier Step 6c: 18/18 OK, 0 MISMATCH |
| SIM gate DEFERRED | Documented non-blocking in ticket-2-completion.md | Verifier AC-16: PASS |

All T2 acceptance criteria from mission-brief + DW-NEW-08-naked-fill-race.md satisfied.

---

### FR-04 CROSS-FILE COHERENCE

**PASS**

T1 adds `.ToList()` inside `ActiveOrders`, materializing `acc.Orders` into a `List<Order>` snapshot.
T2's `DrainThenDispatch` calls `ActiveOrders(follower)` to build `entryCandidates`. Because T1 has
already applied `.ToList()`, `DrainThenDispatch` receives a pre-materialized snapshot — correct for
the `Any()` check and the `foreach` cancel loop. No double-enumeration risk. No concurrent
modification risk.

T2's `TryDrainWatchdog` uses `(long)(int)Environment.TickCount` — the same two-cast sequence fixed
in T1 Sub-B. This is architecturally consistent: both the naked-detect debounce (T1) and the drain
watchdog timestamp (T2) use identical wraparound-safe arithmetic.

T1 modifies only `ActiveOrders` body (line 3437-3441) and `NakedPositionDetector` cast (line 6439).
T2 modifies `HandleEntryChange` (line 3717) and `OnOrderUpdate` (line 1355+). These are distinct
method ranges with no overlapping edit zones. The sequential pipeline (T1 VERIFY_PASS → T2 begin)
prevented merge conflicts.

`_pendingDispatchDrains` field (T2, line 379) is inserted after `_nakedDetectLastQueuedTicks` (T1
baseline, line 373-374). Field placement is coherent — both are ConcurrentDictionary fields in the
same region.

No architectural inconsistency between T1 and T2 found.

---

### FR-05 NO NEW LOCK() IN SYSTEM

**PASS**

- T1 verifier SCAN-01: 0 results (CopyEngine.cs, non-comment lines)
- T2 verifier SCAN-01: 0 results (CopyEngine.cs, non-comment lines)
- T2 uses `ConcurrentDictionary` index operator (thread-safe overwrite) and `Interlocked.Decrement`
  for `PendingCancelCount`. Zero lock() invocations.

JS-021 compliant system-wide.

---

### FR-06 NT8 API COMPLIANCE SYSTEM-WIDE

**PASS**

T2 verifier Step 2 banned API scan:
```
Select-String -Pattern "Account\.Change\(|AtmStrategyCreate|AtmStrategyChangeStopTarget"
| Where-Object { $_.Line -notmatch "^\s*//" }
```
Result: 0 results (no executable code calls to any banned API).

T1 adds no NT8 API calls whatsoever (pure cast change + LINQ terminal operator).
T2 uses only: `Account.Cancel(Order[])`, `Account.CreateOrder(...)`, `Account.Submit(Order[])` —
all confirmed AddOnBase-available in NT8_FULL_REFERENCE.md.

---

### FR-07 CYC SYSTEM CHECK

**PASS**

All new and modified methods confirmed ≤8 by independent Layer 3 manual branch count:

| Method | CYC | Direction | Budget |
|--------|-----|-----------|--------|
| ActiveOrders (T1) | 1 | unchanged | <=8 ✅ |
| TryNakedDetect (T1, unchanged) | 3 | unchanged | <=8 ✅ |
| NakedPositionDetector (T1, cast only) | unchanged | unchanged | <=8 ✅ |
| DrainThenDispatch (T2 new) | 4 | new | <=8 ✅ |
| SubmitEntryDirect (T2 new) | 2 | new | <=8 ✅ |
| OnDrainCancelAck (T2 new) | 3 | new | <=8 ✅ |
| SubmitDrainedEntry (T2 new) | 2-3 | new | <=8 ✅ |
| TryDrainWatchdog (T2 new) | 3 | new | <=8 ✅ |
| HandleEntryChange (T2 modified) | 6 (from 7) | reduced | <=8 ✅ |
| OnOrderUpdate (T2 modified) | 7 (from 6) | +1 branch | <=8 ✅ |

All methods ≤8. CYC budget respected system-wide.

---

### FR-08 DEFERRED ITEMS IDENTIFIED

**PASS**

All deferred items enumerated in Section K (mandatory section) below and in 06-deferred-backlog.md.

Three open items carried forward:
1. DW-NEW-08-D-SIM: SIM gate for T2 cancel-before-dispatch drain (live NT8 required, non-blocking)
2. DW-NEXT-A-01: GraceMs calibration (Director action, first post-lane SIM session)
3. DW-NEXT-A-02: T3 NT8 sync verbatim output gap (Director action, documentation only)

---

### FR-09 NO SCOPE CREEP

**PASS**

Files touched in T1: `CopyEngine.cs`, `BwaveDwLaneATests.cs` only.
Files touched in T2: `CopyEngine.cs`, `BwaveNextLaneBTests.cs`, `PropTraderTools.csproj` only.

- `TradeCopierWindow.cs`: NOT touched. Confirmed.
- `AtrSizingEngine.cs`: NOT touched. Confirmed.
- DW-NEXT-A-03 (short positions): NOT touched. Confirmed excluded.
- DW-NEXT-A-04 (multi-instrument): NOT touched. Confirmed excluded.
- DW-NEXT-A-05 (entry misclassification edge case): NOT touched. Confirmed excluded.
- DW-RepairLC-01/02: NOT touched.
- T3 is Director-action documentation only; no engineer pipeline. No code changes.

Ticket reviewer TR-23 and scope lock sections in both tickets confirm zero scope creep.

---

### FR-10 BUILD STATE

**PASS**

- T1 build: "Build succeeded. 1 Warning(s). 0 Error(s)" — warning is pre-existing B131Tests.cs:165 xUnit2004 (unrelated to this lane, present in prior baselines).
- T2 build: "Build succeeded. 1 Warning(s). 0 Error(s)" — same pre-existing warning unchanged.
- Zero errors in both tickets. Zero new warnings introduced by T1 or T2.

---

### FR-11 NT8 SYNC

**PASS**

- T1: `=== SYNC + VERIFY: PASS (18 files confirmed) ===` — 18/18 OK, 0 MISMATCH.
- T2: `=== SYNC + VERIFY: PASS (18 files confirmed) ===` — 18/18 OK, 0 MISMATCH.
Both verified independently by Layer 3 verifier.

---

### FR-12 TEST COUNT

**PASS**

| File | Baseline | After T1 | After T2 | New Tests |
|------|----------|----------|----------|-----------|
| BwaveDwLaneATests.cs | 14 [Fact] | 16 [Fact] | 16 [Fact] | +2 (T1) |
| BwaveNextLaneBTests.cs | 0 (new file) | 0 | 3 [Fact] | +3 (T2) |

- T1 verifier SCAN-07 confirmed: 16 [Fact], 0 [Test] in BwaveDwLaneATests.cs.
- T2 verifier SCAN-07 confirmed: 3 [Fact] at lines 17, 54, 80 in BwaveNextLaneBTests.cs; 0 [Test].
- Total new tests this lane: 5 (2 T1 + 3 T2). Baseline was 14 [Fact] from T4/T5 LaneA.

---

### FR-13 PRIOR DEFERRED BACKLOG STATUS

**PASS**

Items carried from `docs/brain/BWAVE-NEXT/LaneA/06-deferred-backlog.md`:

| ID | Description | Status in Lane B |
|----|-------------|-----------------|
| DW-NEW-08-D | Layer 2 Option D cancel-before-dispatch drain | **CLOSED** -- Implemented in T2 (VERIFY_PASS 2026-09-04) |
| DW-NEXT-A-01 | GraceMs calibration note | **Still OPEN** -- Director action, no live NT8 session. Carries to post-lane SIM. |
| DW-NEXT-A-02 | T3 NT8 sync verbatim output gap | **Still OPEN** -- Director action, documentation only. Carries to housekeeping. |

---

## Cross-File Coherence Analysis

### T1 → T2 Inheritance

T1 finalizes the `ActiveOrders` body with `.ToList()`. T2's `DrainThenDispatch` inherits this
decision — when `DrainThenDispatch` calls `ActiveOrders(follower)`, the result is already a
materialized `List<Order>`. The `.Any()` call and subsequent `foreach` cancel loop therefore
operate on a stable snapshot, not a live enumerable. This is architecturally correct and was
explicitly documented in the plan (Section 2: "T2 inherits T1's ActiveOrders thread-safety
determination").

### Shared Cast Pattern

Both `NakedPositionDetector` (T1 fix at line 6439) and `TryDrainWatchdog` (T2 at ~line 6633)
use `(long)(int)Environment.TickCount`. This is the correct 24.9-day wraparound-safe pattern
applied uniformly across both debounce mechanisms.

### OnOrderUpdate Layering

T1 does not modify `OnOrderUpdate` directly. T2 adds two items at the pre-Gate-1 area (drain-ack
routing at lines 1412-1416 and unconditional `TryDrainWatchdog()` at line 1420). The sequential
pipeline ensured T1's clean baseline was the starting point for T2's OnOrderUpdate edits. CYC=7
post-T2, within budget.

### No Shared Mutable State Collisions

`_nakedDetectLastQueuedTicks` (T1 baseline, line 373) and `_pendingDispatchDrains` (T2, line 379)
are independent `ConcurrentDictionary` fields. Neither method set reads the other's dictionary.
`DrainThenDispatch` reads `ActiveOrders` (materialized via T1). `TryNakedDetect` reads
`_nakedDetectLastQueuedTicks`. No cross-field access, no shared mutation risk.

---

## Section K -- Deferred Work (MANDATORY SECTION)

| ID | Description | Priority | Target Block | Status |
|----|-------------|----------|--------------|--------|
| DW-NEXT-LANEB-01 | **SIM gate for T2 cancel-before-dispatch drain.** Live NT8 SIM session required. Evidence to collect: (1) NT8 output log shows [DRAIN] cancel-sent=N before [DRAIN-SUBMIT] on every dispatch cycle; (2) NT8 output log shows [DRAIN-TIMEOUT] if a cancel is unacknowledged for >2s; (3) Under 14+ drag cycles, follower ends every cycle with either flat OR Entry:Filled + brackets. Non-blocking for code VERIFY_PASS. | P1 | Director action / first post-lane SIM session | OPEN |
| DW-NEXT-A-01 | **GraceMs calibration.** After first live or SIM session with NakedPositionDetector active (T4 from PR #42), monitor [NAKED-DETECT] log lines. If false fires observed, increase GraceMs constant (currently 500ms). If naked positions slip through, decrease. Document calibration result in ticket-4-completion.md follow-up note. | P1 | Director action / first post-lane SIM/live session | OPEN |
| DW-NEXT-A-02 | **T3 NT8 sync verbatim output gap.** ticket-3-completion.md (Lane A) documents expected ptt-sync-and-verify.ps1 format but omits actual verbatim run output. Director to re-run sync against current CopyEngine.cs and append verbatim output to ticket-3-completion.md. Documentation defect only -- no functional risk. | P2 | Director review / BWAVE-NEXT housekeeping | OPEN |

---

## Items Closed This Lane

| ID | Description | Closed By |
|----|-------------|-----------|
| DW-NEW-08-D | Layer 2 Option D cancel-before-dispatch drain | T2 (VERIFY_PASS 2026-09-04) |

---

## Final Verdict

**FINAL_PASS**

All 13 FR checks PASS. Both required output files written. Section K present with 3 open deferred
items. Zero Jane Street rule violations. Zero NT8 API violations. Zero CYC budget violations.
Zero scope creep. All spec requirements satisfied end-to-end. DW-NEW-08-D closed.

---

*Final review written: 2026-09-04 | ptt-plan-reviewer | BWAVE-NEXT Lane B Phase 5*
*Artifacts read: 02-architecture-plan.md, 02-plan-review.md, 04-tickets.md, 04-ticket-review.md,*
*ticket-1-verification.md (VERIFY_PASS), ticket-2-verification.md (VERIFY_PASS),*
*LaneB-mission-brief.md, DW-NEW-08-naked-fill-race.md, LaneA/06-deferred-backlog.md,*
*docs/standards/jane-street/RULES_CATALOG.md*
