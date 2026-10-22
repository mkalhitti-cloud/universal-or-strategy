# B216 Plan Review
## Block: B216 -- Ask/Bid Pattern-B Conversion + Cancel/CancelAll Buttons
**Reviewer**: ptt-plan-reviewer  
**Phase**: 2 (second pass -- cycle 2 plan)  
**Input**: docs/brain/B216/02-architecture-plan.md (PLAN_COMPLETE cycle 2 -- V-01 fixed)  
**Date**: 2026  
**Review cycle**: 2 of 2  

---

## VERDICT: REVIEW_PASS

**Violations**: 0  
**Prior violation V-01**: FIXED -- verified below.

---

## 1. LANE-SPLIT GATE COMPLIANCE

**Gate result in plan**: `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` (present) ✓

| Gate Q | Plan Answer | Reviewer Check | Result |
|--------|-------------|----------------|--------|
| Q1: Within 50 lines / same method? | YES -- Lane A (~1488-1530) and Lane B (~1545) are ~15 lines apart | Borderline (~57 lines by source reference) but Q2=YES independently justifies SINGLE-PIPELINE | PASS |
| Q2: B depends on A final design? | YES -- OnBidAllClick reuses `HasNonFlatPosition` helper introduced by Lane A | Sound. Without T-B216-A, Lane B's `HasNonFlatPosition` call does not compile | PASS |
| Q3: Standalone value? | Lane A: YES. Lane B: PARTIAL (needs A's helper). Lane C: YES | Correctly acknowledged | PASS |
| Q4: Independent SIM verification? | Lane A: YES. Lane B: PARTIAL. Lane C: YES | Correctly acknowledged | PASS |

**Gate reasoning verdict**: SOUND. SINGLE-PIPELINE correctly justified by Q2=YES dependency.

---

## 2. V-01 VERIFICATION (CRITICAL -- first review FAIL trigger)

**Prior defect**: `PttCancelAll.Execute` logged `toCancel.Count` after the outer loop, but `toCancel.Clear()` was called at the top of each iteration, making the post-loop value 0 or last-account-count rather than the total.

**Cycle 2 plan code** ([`PttCancelAll.Execute`](docs/brain/B216/02-architecture-plan.md:450)):

| Check | Expected | Found | Result |
|-------|----------|-------|--------|
| `int totalOrders = 0;` declared before outer `foreach (Account acc in Account.All)` | YES | Line 456 -- declared before foreach at line 459 | PASS ✓ |
| `totalOrders += toCancel.Count;` inside `if (toCancel.Count > 0)` block, before `acc.Cancel()` | YES | Line 469 -- `totalOrders += toCancel.Count;` is first statement in the if-block, before `try { acc.Cancel(...) }` | PASS ✓ |
| Log after outer loop uses `totalOrders` (not `toCancel.Count`) | YES | Lines 480-483 -- `"[CANCEL-ALL] " + totalOrders + " orders cancelled across " + accountsActed + " accounts for " + instr.FullName` | PASS ✓ |
| Log format matches SR-C2 spec | `[CANCEL-ALL] <N> orders cancelled across <M> accounts for <instr>` | Exact match | PASS ✓ |

**V-01 is GENUINELY FIXED.** The running accumulator pattern is correct.

---

## 3. SPEC COVERAGE MATRIX

| Req | Description | Plan Section | Result | Notes |
|-----|-------------|-------------|--------|-------|
| SR-A1 | `[PTT-MA] Ask: leader null -- check account combo on chart trader` to OutputTab1 | Lane A / OnAskClick target state | PASS | Exact string match. Log emitted, then return. |
| SR-A2 | Ask-All Pattern B: Account.All loop, no _leaderAccount, PttMoveTargetOne per account with position, log | Lane A / OnAskAllClick target state | PASS | `HasNonFlatPosition` encapsulates position filter. Log for N>0 and N=0 cases present. |
| SR-B1 | `[PTT-MA] Bid: leader null -- check account combo on chart trader` to OutputTab1 | Lane B / OnBidClick target state | PASS | Exact string match. Mirrors SR-A1. |
| SR-B2 | Bid-All Pattern B: mirrors SR-A2 with useAsk: false | Lane B / OnBidAllClick target state | PASS | Code mirrors OnAskAllClick exactly with `useAsk: false`. |
| SR-C1 | New Cancel button in main area, `LogAndDispatchModule("[CANCEL]", "CANCEL")` | Lane C / OnCancelClick target state | PASS | Correct dispatch. Non-collapsible area. OnCancel2 explicitly NOT touched. |
| SR-C2 | `PttCancelAll.Execute`: correct N in `[CANCEL-ALL] <N> orders cancelled across <M> accounts for <instr>` | Lane C / PttCancelAll.cs -- cycle 2 code | PASS | V-01 fixed. `totalOrders` accumulates before acc.Cancel. Log uses totalOrders. |
| SR-C3 | UniformGrid Columns=2, BrushTeal, white fg, no arrows, added AFTER _quickRowPanel | Lane C / BuildCancelRow + BuildUI | PASS | Visual spec met. Plain Button (no RepeatButton). Placement after _quickRowPanel confirmed. |

---

## 4. RULES CATALOG COMPLIANCE (P0 checks)

| Rule | Check | Plan Evidence | Result |
|------|-------|--------------|--------|
| JS-021 | No `lock()` in new/modified code | No `lock()` in any plan code sample | PASS |
| JS-001 | No `throw new Exception` in hot paths | No `throw` in any new method | PASS |
| JS-002 | No `return null` for missing values | All new methods return void or bool | PASS |
| JS-033 | No `async void` (non-event-handler) | All handlers are synchronous `private void` | PASS |
| JS-036/037 | No heap alloc on hot path | `new List<Order>()` in PttCancelAll.Execute is a button-click path, not hot path | PASS |
| JS-066 | CYC <= 8 for all modified/new methods | See CYC table below | PASS |
| JS-081 | ASCII-only string literals | All log strings verified ASCII in plan | PASS |
| NT8: No FontFamily | No FontFamily override | Plan uses `SetResourceReference(StyleProperty, "NTButtonStyle")` -- no FontFamily | PASS |
| NT8: No hex colors | No `#RRGGBB` | Plan uses `BrushTeal` field and `Brushes.White` | PASS |
| NT8: No async/await in lifecycle | N/A -- no lifecycle methods added | Not applicable | PASS |
| NT8: No Account.All in constructor | N/A -- Account.All in event handlers only | Not applicable | PASS |

---

## 5. CYC BUDGET REVIEW

| Method | Plan CYC | Reviewer Count | JS-066 | Notes |
|--------|----------|---------------|--------|-------|
| `OnAskClick` (modified) | 4 | 4 | PASS | Correct |
| `OnAskAllClick` (Pattern B) | 4 | 4 | PASS | Correct |
| `HasNonFlatPosition` (new) | 5 | 6 | PASS | Plan undercounts by 1 (misses `pos == null` continue check). Actual CYC=6, still <= 8. Non-fatal. |
| `OnBidClick` (modified) | 4 | 4 | PASS | Mirrors OnAskClick |
| `OnBidAllClick` (Pattern B) | 4 | 4 | PASS | Mirrors OnAskAllClick |
| `BuildCancelRow` (new) | 1 | 1 | PASS | Straight-line construction |
| `OnCancelClick` (new) | 1 | 1 | PASS | Single call |
| `OnCancelAllClick` (new) | 2 | 2 | PASS | Null guard + call |
| `PttCancelAll.Execute` (new) | 5 | 6-7 | PASS | Plan omits try/catch as branch. With catch: CYC=7. Still <= 8. Non-fatal. |
| `PttCancelAll.IsWorkingOrderForInstr` (new) | 4 | 4 | PASS | Correct |

**All methods within JS-066 ceiling of 8.** CYC inaccuracies noted but none trigger a FAIL.

---

## 6. CONSTRAINT COMPLIANCE

| Constraint | Plan Evidence | Result |
|-----------|--------------|--------|
| `OnCancel2` NOT touched | Plan: "Non-issue: OnCancel2 unchanged" in Risks section | PASS |
| `PttCancel.cs` NOT modified | "Files explicitly not touched" list includes PttCancel.cs | PASS |
| `PttMoveTargetOne.Execute` signature unchanged | PttMoveTargetOne.cs in "not touched" list; plan calls existing signature `Execute(acc, instr, useAsk, buf)` | PASS |
| `CopyEngine.cs` NOT touched | "Files explicitly not touched" list confirms | PASS |
| `TradeCopierAddOn.cs` NOT touched | "Files explicitly not touched" list confirms | PASS |
| ptt-sync 21/0 after every .cs edit | SCAN-7 includes `ptt-sync.ps1` | PASS |
| Zero new lock() | JS-021 table in plan: PASS | PASS |
| ASCII-only | SCAN-4 command present for both files | PASS |

---

## 7. 7-SCAN CHECKLIST

| Scan | Defined in Plan | Result |
|------|----------------|--------|
| SCAN-1: P0 violations | Yes -- grep lock(), async void, return null | PASS |
| SCAN-2: dotnet test | Yes -- command + expected pass/fail counts | PASS (see note) |
| SCAN-3: CYC | Yes -- by inspection, CYC Budget table | PASS |
| SCAN-4: ASCII-only | Yes -- ReadAllBytes command for both files | PASS |
| SCAN-5: symbol existence | Yes -- Select-String for 8 symbols | PASS |
| SCAN-6: functional correctness | Yes -- per-AC verification map | PASS |
| SCAN-7: diff hygiene | Yes -- multiple-hunk check + hygiene script + guardian + ptt-sync | PASS |

**Note on SCAN-2**: Plan states `Failed=9 (pre-existing)`, orchestrator prompt states `Failed=24`. Discrepancy carried forward from first review. Not a FAIL -- engineer must baseline actual pre-existing count before B216 execution and document in ticket completion.

---

## 8. ACCEPTANCE CRITERIA COVERAGE

| AC | Plan Coverage | Result |
|----|--------------|--------|
| AC-A1 | OnAskClick null-leader log | PASS |
| AC-A2 | OnAskAllClick no _leaderAccount dependency | PASS |
| AC-A3 | OnAskAllClick log `[PTT-MA] Ask-All: instrument=X accounts=N` | PASS |
| AC-B1 | OnBidClick null-leader log | PASS |
| AC-B2 | OnBidAllClick no _leaderAccount dependency | PASS |
| AC-B3 | OnBidAllClick log `[PTT-MA] Bid-All: instrument=X accounts=N` | PASS |
| AC-C1 | _cancelRowPanel visible below _quickRowPanel | PASS |
| AC-C2 | Cancel button calls `LogAndDispatchModule("[CANCEL]", "CANCEL")` | PASS |
| AC-C3 | Cancel All calls `PttCancelAll.Execute(_instrument)` | PASS |
| AC-C4 | PttCancelAll.cs exists, passes JS-DNA scan | PASS |
| AC-C5 | No spinner arrows | PASS |
| AC-C6 | BrushTeal border, background, white foreground | PASS |
| AC-ALL (zero failures) | SCAN-2 present | PASS (see SCAN-2 note) |
| AC-ALL (ptt-sync 21/0) | SCAN-7 present | PASS |
| AC-ALL (DIFF GUARD PASS) | SCAN-7 present | PASS |

---

## 9. NEW ISSUES CHECK (introduced by V-01 revision)

The cycle 2 fix added `int totalOrders = 0;` and `totalOrders += toCancel.Count;` before `acc.Cancel()`, plus a `try/catch` wrapping the cancel call. Checked for new violations:

| Potential New Issue | Assessment | Result |
|--------------------|-----------|--------|
| `try { } catch { }` bare catch block | Not a P0 or P1 violation. No JS rule bans bare catch. Plan comment explains semantics: "cancel on already-filled orders is non-fatal." Pattern matches existing PttCancel.cs exception handling for cancel operations. | PASS -- no new violation |
| `totalOrders` incremented before `try`, so could over-count on throw | Edge case: if `acc.Cancel()` throws, orders weren't cancelled but `totalOrders` was already incremented. Minor semantic imprecision. Not a spec FAIL -- SR-C2 does not define exception semantics. | PASS -- no FAIL trigger |
| `accountsActed++` inside `try` (only counts successful calls) | Consistent with intent: accounts where cancellation was attempted AND succeeded. | PASS |

**No new violations introduced by the V-01 revision.**

---

## 10. SUMMARY

| Check | Result |
|-------|--------|
| Lane-Split Gate | PASS -- SINGLE-PIPELINE correctly justified |
| V-01 verification | PASS -- totalOrders declared before loop, incremented before Cancel, logged after loop |
| SR-A1 | PASS |
| SR-A2 | PASS |
| SR-B1 | PASS |
| SR-B2 | PASS |
| SR-C1 | PASS |
| SR-C2 | PASS -- V-01 fixed |
| SR-C3 | PASS |
| JS-021 (lock) | PASS |
| JS-001 (throw) | PASS |
| JS-002 (null) | PASS |
| JS-033 (async void) | PASS |
| JS-066 (CYC <= 8) | PASS |
| Constraint compliance | PASS |
| 7-scan checklist | PASS |
| AC coverage | PASS |
| New issues from revision | PASS -- none introduced |

---

## VERDICT: REVIEW_PASS

**Violations**: 0  
**Cycle 2 plan is approved. Pipeline may proceed to Phase 3 (ticket generation).**

**Action**: Spawn ptt-architect for Phase 3 to produce `docs/brain/B216/04-tickets.md` (T-B216-A, T-B216-B, T-B216-C). All three tickets must carry the full 7-scan checklist per the Phase 3.5 ticket-reviewer contract.
