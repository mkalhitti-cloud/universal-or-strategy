# B130 LaneA Plan Review

**Epic**: B130-LaneA
**Defect**: DW-B137 — IsAtmSTPOrder wrong name format
**Phase**: 2 (Plan Review)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-09-01
**Plan file**: docs/brain/B130/LaneA-02-architecture-plan.md

---

## Review Result: REVIEW_PASS

---

## A. Spec Alignment Check

| Requirement | Addressed? | Plan Section |
|---|---|---|
| Stop1/Stop2/Stop3 drag routes to cancel+resubmit (root cause) | YES | D.1 + B.Root Cause |
| Target1/Target2/Target3 drag routes to cancel+resubmit | YES | D.3 + C.2 |
| "Buy STP"/"Sell STP" EndsWith("STP") backward compat preserved | YES | D.1 AFTER block, J |
| Option A safety: no PTT CreateOrder calls with Stop*/Target* prefixes | YES | D.1 safety note + F |
| OQ-03 safety applies equally to target cancel+resubmit | YES | B.OQ-03 + F |
| PTT- prefix on new order name "PTT-TGT-Drag" | YES | C.3 + D.3 |
| Layer 1 (IsBracketLegStatic) already passes Stop1/Target1 — no change required | YES | B.Layer1 |
| IsStopLeg already handles Stop1 correctly (isStop=true) — no change required | YES | B.IsStopLeg |
| Section F traceability matrix present | YES | F |

**Finding**: All DW-B137 requirements fully traced. Both stop drag and target drag are addressed. Backward compat clause verified retained. **PASS.**

---

## B. Rules Catalog Compliance

| Rule | Check | Finding |
|---|---|---|
| JS-021: No lock() | No lock() in IsAtmSTPOrder, SyncFollowerBracket branch (3b), or SyncAtmFollowerTarget | PASS |
| JS-001: No throw in hot path | SyncAtmFollowerTarget Block A and Block B both use try/catch — no throw escapes to caller | PASS |
| JS-002: No return null from value-expected | All proposed returns are `return;` (void methods / expression-body bool). No null return. | PASS |
| JS-033: No async void | No async keyword in any proposed method signature | PASS |
| JS-036: No new byte[] heap alloc in hot path | `new Order[] { fo }` and `new[] { newTarget }` are identical to the existing SyncAtmFollowerBracket pattern (L2123, L2152). Pre-existing accepted pattern in this codebase. | PASS |

**Finding**: Zero Rules Catalog violations found in proposed changes. **PASS.**

---

## C. CYC Budget Verification

### IsAtmSTPOrder (D.1)

```
Expression body:
  order.Name != null
  && (order.Name.EndsWith("STP", ...) || order.Name.StartsWith("Stop", ...) || order.Name.StartsWith("Target", ...))
```

OR clauses within a single compound boolean expression are not McCabe decision nodes.
The expression body itself is 1 linear path.
**CYC = 1. Plan claims CYC=1. CORRECT. PASS (≤ 8).**

### SyncFollowerBracket after branch (3b) (D.2)

Decision nodes in current code (L2048–L2098):
1. `fo == null` (L2057)
2. `Math.Abs(newPrice - currentPrice) < tickSize` (L2061)
3. `isStop && IsAtmSTPOrder(fo)` (L2067) — branch (3)
4. `isStop && IsTrailingStop(fo)` (L2073) — branch (4)
5. `if (isStop)` inside try block (L2081) — branch (5)

Current CYC = 5 decisions + 1 = **6**. Consistent with plan's existing comment at L2044.

After adding branch (3b) `!isStop && IsAtmSTPOrder(fo)`:
6. New branch (3b) adds 1 decision node.

New CYC = 6 decisions + 1 = **7. Plan claims CYC=7. CORRECT. PASS (≤ 8).**

### SyncAtmFollowerTarget (D.3) — new method

Decision nodes:
1. `if (acc == null)` (guard 1)
2. `if (fo == null)` (guard 2)
3. `if (newTarget == null)` (Block B null check)

Note: try/catch blocks are NOT McCabe decision nodes. The catch clauses contain only StatusUpdate invocations — no branching.

CYC = 3 decisions + 1 = **4. Plan claims CYC=4. CORRECT. PASS (≤ 8).**

Minor observation: Plan's CYC comment labels "(3) Block A try-body" as a McCabe node — try/catch is not a McCabe branch. The final count CYC=4 is numerically correct. The comment label is imprecise but harmless; it does not affect the CYC result.

**All methods ≤ 8. PASS.**

---

## D. Safety Analysis

**Option A safety (StartsWith("Stop"/"Target"))**:
Plan states a grep of CopyEngine.cs confirmed 0 `CreateOrder` calls using "Stop" or "Target" prefixes. The only new PTT order name introduced is "PTT-TGT-Drag" — this starts with "PTT-", not "Stop" or "Target", so IsAtmSTPOrder correctly returns false for it. The Option A breadth is safe for this codebase. **PASS.**

**OQ-03 for target cancel+resubmit**:
Gate 2 (`FindMatchingRule`) evaluates whether the account is a master account. Follower accounts never match `rule.MasterAccount.Name`, so `TryCancelFollowerEntries` is unconditionally blocked for all follower orders, regardless of whether the order is a stop or target bracket. The OQ-03 safety guarantee is symmetric — it applies equally to `SyncAtmFollowerTarget`. **PASS.**

**OrderType.Limit for SyncAtmFollowerTarget**:
Plan uses `OrderType.Limit` with `arg6=limitPrice=newPrice`, `arg7=0` (stopPrice). This is the correct NT8 CreateOrder signature for a Limit order, mirroring the inverse of SyncAtmFollowerBracket (StopMarket: arg6=0, arg7=stopPrice). **PASS.**

**NT8-014 PTT- prefix**:
"PTT-TGT-Drag" satisfies the PTT- prefix requirement. **PASS.**

**ATmStrategyChangeStopTarget / AtmStrategyCreate not used**:
Plan correctly notes both are StrategyBase-only and are not referenced. **PASS.**

---

## E. Test Coverage

| Check | Finding |
|---|---|
| Test 1 name: `B130_DW137_Stop1NameRoutesToCancelResubmit` | Present (Section G) |
| Test 2 name: `B130_DW137_Target1NameRoutesCorrectly` | Present (Section G) |
| Backward compat assertions (Buy STP/Sell STP → true) | Present in Test 1 |
| False-case assertions (Entry, PTT-Copy → false) | Present in Test 1 |
| IsAtmSTPOrder is `internal static` — accessible without reflection | Confirmed (plan references L46 InternalsVisibleTo) |
| PTT-TGT-Drag and PTT-STP-Drag → false assertions in Test 2 | Present |
| B129Tests.cs must-still-pass coverage | Section J lists 3 of 6 B129 tests (DW134 group only) |

**Observation — Section J incomplete B129 backward-compat list**:
B129Tests.cs contains 6 [Fact] tests: 3 in DW134 group and 3 in DW135 group
(`B129_DW135_GuardClearedAfterLeaderFlat`, `B129_DW135_DW128ProtectionPreservedDuringRaceWindow`,
`B129_DW135_FirstEntryAfterRestartNotBlocked`). Section J lists only the 3 DW134 tests.
The 3 DW135 tests concern the entry-guard/race-window logic — none of the B130 changes touch that
code path. These tests will pass unchanged without any action. The omission is a documentation
incompleteness in Section J but does not represent a functional risk. Not a blocking violation.

**Finding**: Test coverage adequate. Both required [Fact] tests specified with correct names,
correct assertions, and correct test seam (internal static, no reflection needed). **PASS.**

---

## F. 7-Scan Checklist

| Scan | Present in Plan Section H? | Expected Result Stated? |
|---|---|---|
| SCAN-01: lock() | YES | 0 new matches in modified methods |
| SCAN-02: async void | YES | 0 results |
| SCAN-03: DateTime.Now | YES | 0 results |
| SCAN-04: Non-ASCII | YES | 0 results |
| SCAN-05: CYC audit | YES | All modified methods ≤ 8 |
| SCAN-06: PTT- prefix — checks PTT-TGT-Drag AND PTT-STP-Drag | YES | Matches in SyncAtmFollowerBracket + SyncAtmFollowerTarget |
| SCAN-07: Build | YES | 0 errors |

SCAN-06 explicitly checks for both `"PTT-TGT-Drag"` and `"PTT-STP-Drag"` in the pattern. **PASS.**

All 7 scans present with commands and expected results. **PASS.**

---

## G. Files Touched

| File | Operation | Correct? |
|---|---|---|
| `src/PropTraderTools/CopyEngine.cs` | Edit (3 targeted changes) | YES |
| `src/PropTraderTools/Tests/B130Tests.cs` | New file | YES |
| `src/PropTraderTools/PropTraderTools.csproj` | Edit (1 line add) | YES |

Exactly 3 files. csproj change is minimal (single `<Compile Include>` line). **PASS.**

---

## Violations Found

None.

---

## Reviewer Recommendation

REVIEW_PASS — plan is correct and complete. Proceed to Phase 3 (ticket generation).

**Summary of findings**:
- Root cause (IsAtmSTPOrder) correctly identified and fixed (D.1).
- Both stop drag and target drag paths fully addressed.
- Backward compat preserved.
- All JS rules checked: no violations.
- CYC counts verified: IsAtmSTPOrder=1, SyncFollowerBracket=7, SyncAtmFollowerTarget=4. All ≤ 8.
- Safety analysis (OQ-03, Option A grep, OrderType.Limit) correct.
- 7 scans present with correct expected results.
- Exactly 3 files touched.
- Non-blocking observation: Plan Section J lists 3 of 6 B129 backward-compat tests. DW135 group omitted but functionally unaffected by this change.
- Non-blocking observation: CYC comment for SyncAtmFollowerTarget labels "Block A try-body" as a McCabe node — imprecise but final count CYC=4 is correct.
