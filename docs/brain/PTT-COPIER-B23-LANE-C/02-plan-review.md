# PTT-COPIER-B23-LANE-C — Plan Review
# Block:  PTT-COPIER-B23
# Lane:   C
# Defect: DW-B22-BE-TRIGGER-01 (P1)
# Reviewer: ptt-plan-reviewer
# Review Cycle: 2 (Revision after REVIEW_FAIL Cycle 1)
# Date:   2026-07-16
# Result: REVIEW_PASS

---

## Cycle 1 Violation — Resolved

V-001 (Cycle 1): post-fix CYC = 9 because `if (acc != null)` guard was counted as an `if`-branch
but not listed in the plan's CYC table.

**Fix applied by architect**: replaced `if (acc != null) acc.AccountItemUpdate -= ...` with
`acc?.AccountItemUpdate -= ...` (null-conditional operator). Null-conditionals are not CYC branches
(same convention as ternaries per project standard). The branch is now gone from the method body,
reducing post-fix CYC from 9 to 8. Violation resolved.

---

## Checklist Results — Cycle 2

| # | Item | Result | Notes |
|---|------|--------|-------|
| 1 | OLD trigger `if (e.Value < 0) return;` absent from new block | **PASS** | Not present anywhere in revised §2 code block |
| 2 | NEW trigger uses FindPosition + IsFlat + MasterInstrument.TickSize + MarketData.Last.Price | **PASS** | All four present: `FindPosition(...)`, `IsFlat(pos)`, `?.MasterInstrument?.TickSize`, `?.MarketData?.Last?.Price` |
| 3 | CYC COUNT: 7 `if`-branches + method base = CYC 8, which is ≤ 8 limit | **PASS** | See detailed count below |
| 4 | AccountItem.UnrealizedProfitLoss filter KEPT | **PASS** | Retained as branch (2) in the revised block |
| 5 | NULL-CONDITIONAL CHAIN: `_pendingBeInstrument?.MarketData?.Last?.Price` 3-level chain | **PASS** | Present verbatim in §2: `double last = _pendingBeInstrument?.MarketData?.Last?.Price ?? 0.0;` |
| 6 | MoveStopToBreakEven NOT CHANGED (out of scope) | **PASS** | §1 states "MoveStopToBreakEven() is unchanged"; §3 write-set excludes it |
| 7 | JS P0 RULES: no lock(), no return null, no async void | **PASS** | No `lock(`, no `return null;`, no `async void` in revised code block |
| 8 | WRITE-SET BOUNDARY: only CopyEngine.cs + CopyEngineTests.cs | **PASS** | §3 states exactly "CopyEngine.cs + CopyEngineTests.cs only" |

---

## CYC Verification — Detailed Count

Method: `OnPendingBeAccountUpdate` (post-fix, revised plan)

`if`-statement enumeration (only `if` keywords count; ternaries and null-conditionals do NOT):

| # | Statement | Source |
|---|-----------|--------|
| 1 | `if (_pendingBeState != 1)` | state check — KEPT |
| 2 | `if (e.AccountItem != AccountItem.UnrealizedProfitLoss)` | item filter — KEPT |
| 3 | `if (IsFlat(pos)) return;` | flat guard — NEW |
| 4 | `if (tickSize <= 0) return;` | tickSize guard — NEW |
| 5 | `if (last <= 0) return;` | last price guard — NEW |
| 6 | `if (!triggered) return;` | triggered check — NEW |
| 7 | `if (Interlocked.CompareExchange(ref _pendingBeState, 0, 1) != 1)` | CAS swap — KEPT |

**NOT counted** (correct by project convention):
- `isLong ? ... : ...` — ternary expression, not an `if`-branch
- `acc?.AccountItemUpdate -= ...` — null-conditional operator, not an `if`-branch (this is the
  Cycle 1 fix; the old `if (acc != null) {...}` guard has been eliminated from the method body)

CYC = 1 (method base) + 7 (`if`-statements) = **8**. Exactly at the ≤ 8 limit. PASS.

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| Replace UPnL dollar-PnL trigger with price-based trigger | YES | §1, §2 Strategy |
| Price target = pos.AveragePrice ± bufferTicks × tickSize | YES | §2 Revised Trigger code block |
| Long and short directions both handled | YES | §2 `isLong` ternary |
| Immune to commission deduction on PA prop accounts | YES | §1 Root Cause |
| AccountItemUpdate subscription retained (no new subscribe path) | YES | §2 Strategy explanation |
| MoveStopToBreakEven() unchanged | YES | §1 note, §3 write-set |
| Two new [Fact] tests (FiresAtPriceTarget_Long, DoesNotFireBelowTarget_Long) | YES | §2 New [Fact] Tests Required |
| Write-set bounded to CopyEngine.cs + CopyEngineTests.cs | YES | §3 Write-Set |

---

## Result

REVIEW_PASS
