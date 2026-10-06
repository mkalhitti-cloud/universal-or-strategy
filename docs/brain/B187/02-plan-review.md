# B187 Plan Review
## ptt-plan-reviewer Phase 2

**Date**: 2026-07-08
**Reviewer**: ptt-plan-reviewer
**Epic**: B187 — QX Stop Snapshot Fix + DIAG Cleanup + CS0067 Suppress
**Plan file**: `docs/brain/B187/02-architecture-plan.md`
**Spec file**: `c:/WSGTA/universal-or-strategy-director/specs/002-trade-copier-spec.html`
**Rules catalog**: `docs/standards/jane-street/RULES_CATALOG.md` (truncated at JS-041 — DW-B183-05; enforcement via DNA block)

---

## LANE-SPLIT GATE Compliance

**Gate result stated in plan**: YES — SINGLE-PIPELINE  
**Compliance check**:

| Question | Plan Answer | Gate Logic | Result |
|----------|-------------|------------|--------|
| Q1. Same method or within 50 lines? | YES | If Q1=YES → SINGLE-PIPELINE (lanes blocked) | ✓ Correct |
| Q2. Fix B design depends on Fix A final design? | NO | Does not affect gate outcome (Q1=YES already blocks lanes) | ✓ Documented |
| Q3. Each fix has standalone value? | YES | Required for SINGLE-PIPELINE if Q1=YES | ✓ |
| Q4. Each fix has independent SIM verification? | YES | Required for SINGLE-PIPELINE if Q1=YES | ✓ |

Gate result: SINGLE-PIPELINE stated and justified. Required gate result is present.

**LANE-SPLIT GATE: PASS**

---

## Violations Found

None.

---

## Per-Check Results

### Check 1 — All 3 tickets described with method-level change detail

| Ticket | Methods Named | Change Detail | Result |
|--------|---------------|---------------|--------|
| T1 | `Execute(Instrument)`, `Execute(List<(double,int)>, Instrument)` | Statement reorder shown with before/after code blocks; XML comment update specified | PASS |
| T2 | `Execute(Instrument)` (remove call+comment), `ExecuteFollowers(...)` (remove DIAG block + CYC comment update), `LogLeaderDiag` (delete entire method) | Exact lines to remove shown with code blocks; all 3 removal locations explicit | PASS |
| T3 | `CopyEngine.cs` line 423 — `PendingBeArmed` declaration | Before/after code with `#pragma warning disable CS0067` / `#pragma warning restore CS0067` wrapping | PASS |

**Check 1: PASS**

---

### Check 2 — T1: leaderStop capture moved BEFORE CancelPttBeOrders in BOTH overloads

**`Execute(Instrument)`**: Plan Section 3 shows:
```csharp
// Fixed order:
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);  // FIRST
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);              // SECOND
```
✓ leaderStop before CancelPttBeOrders.

**`Execute(List<(double Price, int Qty)>, Instrument)`**: Plan Section 3 shows:
```csharp
// Fixed order:
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);  // FIRST
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);              // SECOND
```
✓ leaderStop before CancelPttBeOrders in this overload too.

**Check 2: PASS**

---

### Check 3 — T1: SnapshotTargetOrders still called AFTER CancelPttBeOrders in Execute(Instrument) — DW-B126 compliance

Plan Section 3, fixed order for `Execute(Instrument)`:
```csharp
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
// PTT-BE-* are now terminal -- target snapshot sees clean order book.
var targets = SnapshotTargetOrders(acc, pos.Instrument);                   // AFTER cancel
```
✓ `SnapshotTargetOrders` remains after the cancel+wait sequence.

Plan Section 3 also contains an explicit DW-B126 Compliance Statement confirming this constraint is maintained.

Plan Section 3 also notes for `Execute(List, Instrument)`: "DW-B126 is N/A for this overload; forcedTargets are passed by the caller." ✓ Correct — no `SnapshotTargetOrders` in that overload, so DW-B126 is inapplicable.

Test T_B187_03 asserts `SnapshotTargetOrders` line index > `CancelPttBeOrders` line index in `Execute(Instrument)`. ✓

**Check 3: PASS**

---

### Check 4 — T1: CYC unchanged (Execute(Instrument)=8, Execute(List,Instrument)=8)

Plan Section 3, CYC Impact table:

| Method | Before | After | Delta |
|--------|--------|-------|-------|
| `Execute(Instrument)` | 8 | 8 | 0 |
| `Execute(List, Instrument)` | 8 | 8 | 0 |

Justification: pure statement reorder, no new conditional branches. ✓

**Check 4: PASS**

---

### Check 5 — T2: All 3 removal locations specified

Plan Section 4 specifies exactly three removal sites:

| Location | Described | Result |
|----------|-----------|--------|
| `Execute(Instrument)` — 2-line comment + `LogLeaderDiag(acc, targets, pos.Quantity)` call at ~line 76-78 | ✓ Code block shown | PASS |
| `ExecuteFollowers(...)` — full DIAG block (~lines 208-244) including `_fPosQty`, `foreach`, inner `if`, `StringBuilder` loop | ✓ Code block shown | PASS |
| `LogLeaderDiag` private static method (~lines 490-519) — full method + XML summary deleted | ✓ Code block shown | PASS |

All 3 sites explicitly described with code blocks.

**Check 5: PASS**

---

### Check 6 — T2: ExecuteFollowers CYC comment update 7→4 specified

Plan Section 4 specifies:

> Old: `CYC=7: rule null-check(1), follower foreach(2), follower null continue(3), follower position foreach DIAG(4), DIAG _p null/instr guard(5), DIAG for-loop(6), delegate(7).`
>
> New: `CYC=4: rule null-check(1), follower foreach(2), follower null continue(3), delegate(4).`

The CYC delta table shows 7→4.

**Spec discrepancy noted (non-blocking)**: The spec HTML at line 43000 says "CYC delta: ExecuteFollowers 7→5 (removes (4)(5)(6) branches from diagnostic loop)." The spec text says 7→5, but the arithmetic of removing 3 numbered branches (4)(5)(6) from CYC=7 yields 7-3=4, not 5. The plan's CYC=4 is arithmetically correct and internally consistent with its own branch count. The spec has a typo ("7→5" when the deletion described yields 4). The plan's value of 4 is correct.

**Check 6: PASS** (plan is internally consistent and arithmetically correct; spec has a typo, plan wins)

---

### Check 7 — T3: #pragma disable/restore CS0067 wrapping PendingBeArmed

Plan Section 5:

```csharp
#pragma warning disable CS0067 // DW-B186-WARN-01: Roslyn false positive -- event IS subscribed by TradeCopierPanel
internal event Action<string, string> PendingBeArmed; // HOTFIX-BEALL-SYNC-01: instr, accName
#pragma warning restore CS0067
```

Both `#pragma warning disable CS0067` and `#pragma warning restore CS0067` are present, wrapping the event declaration. ✓

**Check 7: PASS**

---

### Check 8 — Execution order: T1 → T2 → T3 sequential (T1+T2 share PttGlobalQuickExit.cs)

Plan Section 9:

```
T1 → VERIFY_PASS(T1) gate → T2 → VERIFY_PASS(T2) gate → T3 → VERIFY_PASS(T3) gate
```

"T1 must complete before T2 begins (same file; T1 modifies lines that T2's removals are adjacent to — sequential prevents merge conflicts)."

T3 noted as independent but mandated sequential by SINGLE-PIPELINE gate. ✓

**Check 8: PASS**

---

### Check 9 — 4 structural tests for T1 specified (T_B187_01..04)

Plan Section 8 test table:

| Test ID | Method | Assertion | Guard |
|---------|--------|-----------|-------|
| `T_B187_01` | `Execute(Instrument)` | `SnapshotStopPrice` index < `CancelPttBeOrders` index | Stop before cancel |
| `T_B187_02` | `Execute(List, Instrument)` | `SnapshotStopPrice` index < `CancelPttBeOrders` index | Same fix, List overload |
| `T_B187_03` | `Execute(Instrument)` | `SnapshotTargetOrders` index > `CancelPttBeOrders` index | DW-B126 guard |
| `T_B187_04` | `Execute(List, Instrument)` | Source does NOT contain `SnapshotTargetOrders` | DW-B126 — List overload correctly omits it |

All 4 test IDs present. All are `[Fact]` xUnit source-text ordering assertions. Implementation pattern specified. File location and class specified. ✓

**Check 9: PASS**

---

### Check 10 — 0 tests for T2, 0 tests for T3

Plan Section 8:
- T2: "0 New Tests. Pure deletion. No new test coverage required." ✓
- T3: "0 New Tests. AC for T3 is `dotnet build 0 warnings`. SCAN-07 covers this." ✓

**Check 10: PASS**

---

### Check 11 — DNA constraints listed: JS-021, JS-001, JS-002, JS-033, JS-066, ASCII-only

Plan Section 11 DNA Constraints table:

| Rule Cited | Present | Notes |
|------------|---------|-------|
| JS-001 | ✓ | No `throw new` in hot paths |
| JS-002 | ✓ | No `return null` |
| JS-021 | ✓ | No `lock()` |
| JS-033 | ✓ | No `async void` |
| JS-066 | ✓ | CYC <= 8 per method |
| JS-081 (ASCII-only) | ✓ | Listed in table |

The review checklist requires JS-021, JS-001, JS-002, JS-033, JS-066, and ASCII-only. All 6 are present. JS-081 (ASCII-only) is cited even though the catalog file is truncated at JS-041 — the rule is enforced via DNA block. ✓

NT8-021 (Account.All threading constraint) is also addressed in Section 6 as a bonus. ✓

**Check 11: PASS**

---

### Check 12 — 7-scan checklist referenced

Plan Section 10 contains the full 7-scan checklist table:

| Scan | Check | Applies To |
|------|-------|-----------|
| SCAN-01 | No new `lock()` | T1, T2, T3 |
| SCAN-02 | No new `return null` | T1, T2, T3 |
| SCAN-03 | No new `async void` | T1, T2, T3 |
| SCAN-04 | CYC manual count per method | T1, T2 |
| SCAN-05 | No non-ASCII characters | T1, T2, T3 |
| SCAN-06 | No new `throw new` in hot paths | T1, T2, T3 |
| SCAN-07 | `dotnet build` exits 0 errors, 0 warnings | T1, T2, T3 |

All 7 scans present and mapped to ticket applicability. ✓

**Check 12: PASS**

---

### Check 13 — DW items closed listed

Plan Section 2 (Deferred Items Closed by B187) table:

| DW ID | Priority | Closed By |
|-------|----------|-----------|
| DW-B187-QX-STOP-SNAP-01 | P1 | T1 |
| DW-B186-DIAG-CLEANUP-01 | P3 | T2 |
| DW-B186-WARN-01 | P3 | T3 |

All three required items present. Spec confirms DW-B186-DIAG-CLEANUP-01 and DW-B186-WARN-01 as "DEFERRED — B187+" at lines 42996 and 43008. DW-B187-QX-STOP-SNAP-01 is a new defect identified and addressed within B187's scope. ✓

**Check 13: PASS**

---

## Spec Alignment

### T1 Root Cause Verification

The spec (section DW-B129, line 33495+) documents the analogous prior bug DW-B129 where `CancelPttBeOrders + WaitForPttBeCancelled` emptied the order book before `SnapshotTargetOrders`, resulting in `stop=0`. The fix in B120 was a fallback flatten, not the root-cause capture. The B187 bug (DW-B187-QX-STOP-SNAP-01) is the symmetric variant: when BE ALL fires first, the only stop reference is `PTT-BE-Stop-*`. After `CancelPttBeOrders` in the QX path, those orders are Cancelled and invisible to `SnapshotStopPrice`. Result: `leaderStop=0`, guard `if (snapshotStop <= 0) return;` prevents stop submission.

The plan's fix — move `leaderStop = SnapshotStopPrice(...)` before `CancelPttBeOrders` in both overloads — correctly addresses the root cause by capturing the stop price while `PTT-BE-Stop-*` orders are still in Working/Accepted state.

**T1 root cause correctly identified and addressed: PASS**

### T2 — Pure deletion, no behavioral change

Confirmed. T2 removes diagnostic log scaffolding. The `_fPosQty` variable is used only within the DIAG block (plan confirms "no other usages"). `LogLeaderDiag` has no callers after the only call site is removed. Pure deletion with no functional code touched.

**T2 spec alignment: PASS**

### T3 — Pragma-only, no behavioral change

Confirmed. Adding `#pragma warning disable/restore CS0067` is compile-time metadata only. No runtime behavior change. The spec at line 43011 confirms this is a warning-only item ("no runtime impact").

**T3 spec alignment: PASS**

---

## DNA Rule Scan (Hardcoded Rules)

| Rule | Check | Plan Status |
|------|-------|-------------|
| JS-021: No `lock()` | Plan explicitly states "No lock anywhere in changes" (Section 7, Section 11) | PASS |
| JS-001: No `throw new` in hot paths | No new exceptions introduced in any ticket | PASS |
| JS-002: No `return null` | No return null introduced | PASS |
| JS-033: No `async void` (non-event-handler) | All modified methods are synchronous void | PASS |
| JS-066: CYC <= 8 | Execute=8 (both overloads, unchanged), ExecuteFollowers=4 after T2 | PASS |
| JS-081: ASCII-only identifiers and strings | All new comments/strings are plain ASCII (no Unicode, no curly quotes) | PASS |
| NT8: async/await in lifecycle hooks | Not applicable — no lifecycle hook changes | N/A |
| NT8: Account.All in constructor | Not applicable — no constructor changes | N/A |
| NT8: sealed TradeCopierWindow | Not applicable — no window changes | N/A |
| NT8: FontFamily override | Not applicable | N/A |
| NT8: Hardcoded #RRGGBB hex | Not applicable | N/A |
| NT8: CreateOrder without PTT- prefix | Not applicable — no new order submissions | N/A |
| NT8: DateTime.Now | Not applicable | N/A |
| AtmStrategyCreate / AtmStrategyChangeStopTarget | Correctly flagged as StrategyBase-only, NOT used (Section 6) | PASS |
| Dispatcher.InvokeAsync | All execution is on UI thread — not needed; threading model unchanged (Section 7) | PASS |

---

## Observations (Non-Blocking, Informational)

1. **Spec typo acknowledged**: Spec line 43000 says "ExecuteFollowers 7→5" but the plan correctly computes 7→4 (removing 3 numbered branches (4)(5)(6) from CYC=7). The plan is arithmetically correct. This is a documentation error in the spec, not in the plan.

2. **DW-B183-05 carry-forward acknowledged**: The RULES_CATALOG.md file on disk is truncated at JS-041. Plan correctly cites JS-081 (ASCII-only) using the rule ID that is not present in the truncated file. Enforcement operates via the hardcoded DNA block. This is a known open item (DW-B183-05, P1) and does not impair plan review.

3. **DW-B187-QX-STOP-SNAP-01 not pre-documented in spec**: The spec HTML does not contain a DW-B187-QX-STOP-SNAP-01 entry. The defect is newly identified in this block. This is expected — the spec's B186 section concludes with DW-B186 items only. The plan correctly introduces the new DW ID. The spec HTML update (adding DW-B187-QX-STOP-SNAP-01 to the spec) is a Director task and is not blocking for B187 pipeline execution.

4. **Open deferred items carry-forward**: Section 12 correctly lists all 6 open items from `docs/brain/B186/06-deferred-backlog.md` that are not addressed by B187. The carry-forward list matches the B186 deferred backlog. ✓

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|------------|--------------|
| DW-B186-DIAG-CLEANUP-01 (spec line 42996) — remove LogLeaderDiag call + helper + ExecuteFollowers DIAG block | YES | Section 4 (T2) |
| DW-B186-WARN-01 (spec line 43008) — CS0067 PendingBeArmed suppression | YES | Section 5 (T3) |
| DW-B187-QX-STOP-SNAP-01 — QX stop price = 0 when BE ALL fires before QX | YES — root cause fixed by moving leaderStop capture before CancelPttBeOrders | Section 3 (T1) |
| DW-B126 constraint preserved (SnapshotTargetOrders after cancel) | YES | Section 3, explicit compliance statement |
| CYC <= 8 for all modified methods | YES | Sections 3, 4, 5 + Section 11 |
| No new lock(), async void, return null, throw new | YES | Sections 7, 11 |
| 7-scan checklist | YES | Section 10 |
| xUnit [Fact] tests for T1 (4 tests) | YES | Section 8 |
| T2 and T3 zero tests | YES | Section 8 |
| Execution order T1 → T2 → T3 | YES | Section 9 |

---

## Final Verdict

**REVIEW_PASS**

All 13 checklist items passed. No DNA violations found. No missing spec requirements. The lane-split gate is correctly applied (SINGLE-PIPELINE, Q1=YES). The plan is complete, internally consistent, and correctly addresses all three deferred items from B186.

The plan is approved for Phase 3 (ticket generation).

---

*Written by: ptt-plan-reviewer*
*Epic: B187*
*Phase: 2 (Plan Review)*
*Return: REVIEW_PASS*
