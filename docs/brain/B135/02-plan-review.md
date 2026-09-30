# B135 Plan Review

**Epic**: B135 -- Two-Ticket: DW-B146 (second drag fo=null) + DW-B134-OCO (PTT drag orphan sweep)
**Reviewer**: ptt-plan-reviewer
**Review Cycle**: Cycle 2 (post-Cycle-1 REVIEW_FAIL correction)
**Date**: 2026-09-07
**Input**: `docs/brain/B135/02-architecture-plan.md` (Cycle 2 corrected plan)

---

## Cycle 1 Summary (REVIEW_FAIL)

| ID | Rule | Description | Location in Plan | Status |
|----|------|-------------|-----------------|--------|
| V-01 | SPEC-COMPLETENESS (P0) | Section G Ticket 2 Tests had only 3 `[Fact]` entries; spec requires 5 (all 5 DW-B134-OCO scenarios) | Section G -- Ticket 2 Tests table | **CORRECTED in Cycle 2** |

---

## Cycle 2 Review

### V-01 Resolution Check

**Required**: ≥5 `[Fact]` entries in Section G Ticket 2 covering all 5 spec scenarios.

| # | `[Fact]` Name in Plan | Spec Scenario | Present? |
|---|-----------------------|---------------|---------|
| 1 | `T2_CancelPttDragOrphans_CancelsWorkingTgtDrag` | (a) flat event cancels PTT-TGT-Drag | YES |
| 2 | `T2_CancelPttDragOrphans_CancelsWorkingStpDrag` | (b) flat event cancels PTT-STP-Drag | YES |
| 3 | `T2_CancelPttDragOrphans_IgnoresNonPttOrders` | (c) flat does NOT cancel non-PTT Working orders (regression guard) | YES |
| 4 | `T2_TrySwept_PartialFill_NotFlat_DoesNotSweep` | (d) partial fill (qty > 0) does NOT trigger sweep | YES |
| 5 | `T2_CancelPttDragOrphans_ExceptionAbsorbed_NoRethrow` | (e) acc.Cancel exception absorbed gracefully | YES |

**Count**: 5 `[Fact]` entries. All 5 spec scenarios addressed.
**V-01 resolution**: RESOLVED.

---

### Full DNA Scan (Cycle 2)

#### Concurrency (JS-021, JS-023)

| Check | Finding | Verdict |
|-------|---------|---------|
| `lock()` anywhere in new/modified code | None found. `MatchesLeaderName` is static pure predicate. `TrySweptPttDragOrphans` / `CancelPttDragOrphansForAccount` use `acc.Orders.ToList()` (NT8 established pattern). | PASS |
| Monitor/Mutex/SemaphoreSlim for state | None. | PASS |
| UI update from off-thread without Dispatcher.InvokeAsync | No WPF/UI code in scope. No Dispatcher calls. | PASS |

#### Type Safety (JS-001, JS-002, JS-003)

| Check | Finding | Verdict |
|-------|---------|---------|
| throw in OnOrderUpdate / SendCopy / gate chain | `TrySweptPttDragOrphans`: no throw (void, guard returns). `CancelPttDragOrphansForAccount`: try/catch absorbs exception, no rethrow. `MatchesLeaderName`: no throw (returns bool). | PASS |
| null return where value expected | `FindFollowerBracketOrder` `return null` preserved at L2571 (existing nullable contract, Order? type). No new nullable returns. `MatchesLeaderName` returns bool (no null). | PASS |
| Magic string for discriminated state | "PTT-TGT-Drag" and "PTT-STP-Drag" are order-name constants (concrete values, not discriminated state keys). No empty-string state markers. | PASS |

#### Immutability (JS-008, JS-009)

| Check | Finding | Verdict |
|-------|---------|---------|
| Dictionary<K,V> for shared/thread-touched collection | None in new code. | PASS |
| Mutable fields on struct | No new structs. | PASS |
| SolidColorBrush not Freeze()d | No WPF in scope. | N/A |

#### Construction (JS-010)

| Check | Finding | Verdict |
|-------|---------|---------|
| Public constructor on singleton or signal struct | No new classes, singletons, or signal structs introduced. | PASS |

#### NT8 Hard Constraints

| Check | Finding | Verdict |
|-------|---------|---------|
| async/await in OnInitialize/OnDestroyed/OnWindowCreated | None in new code. | PASS |
| Account.All in constructor | None. | PASS |
| sealed TradeCopierWindow | Not in scope. | N/A |
| FontFamily override | No WPF code in scope. | N/A |
| Hardcoded #RRGGBB hex | No hex color literals. | N/A |
| CreateOrder without PTT- prefix | Section H explicitly states "No new CreateOrder calls in B135." | N/A |
| DateTime.Now (not UtcNow) | No DateTime in new code. | PASS |
| AtmStrategyCreate in AddOnBase | Not used. | N/A |

#### Complexity (CYC ≤ 8)

| Method | CYC | Limit | Verdict |
|--------|-----|-------|---------|
| `MatchesLeaderName` (new) | 5 | 8 | PASS |
| `FindFollowerBracketOrder` list overload (modified) | 8 | 8 | PASS -- in-kind guard replacement (1-for-1) |
| `TrySweptPttDragOrphans` (new) | 5 | 8 | PASS |
| `CancelPttDragOrphansForAccount` (new) | 5 | 8 | PASS |
| `OnOrderUpdate` (call added) | 8 | 8 | PASS -- call adds 0 McCabe branches |

#### Spec Completeness

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| DW-B146: second drag fo=null fix | YES | Sections B.1, B.2, C |
| DW-B134-OCO: orphaned PTT-drag sweep on flat | YES | Sections B.3, B.4, B.5, D |
| DW-B147: rawPrice==newPrice early-return | DEFERRED with documented rationale (SyncAtmFollowerTarget CYC=8, guard would push to CYC=9) | Section E |
| T1 tests: 7 [Fact] covering MatchesLeaderName and second-drag integration | YES | Section G, Ticket 1 table |
| T2 tests: 5 [Fact] covering all 5 DW-B134-OCO spec scenarios | YES | Section G, Ticket 2 table |
| xUnit only (no NUnit, no MSTest) | YES | Section G header |
| PropTraderTools.csproj registration | YES | Section F + Section H |
| NT8 API confirmation for all new calls | YES | Section B.5 |
| Regression guard: 52 prior tests must remain green | YES | Section I |
| LANE-SPLIT gate | LANES-APPROVED | Section K |

---

### Minor Internal Inconsistency (Documentation Only -- Not a Violation)

Section H at line 403 states "10 xUnit `[Fact]` tests" -- a stale figure from Cycle 1 that was not updated when two Ticket 2 entries were added. The authoritative test tables in Section G show 7 (T1) + 5 (T2) = 12, and the Summary (line 478) correctly states "12 new `[Fact]` tests (7 T1 + 5 T2)". This is a documentation inconsistency only. It does not map to any JS-XXX rule or NT8 hard constraint and does not constitute a REVIEW_FAIL trigger. The ptt-engineer should update Section H to read "12 xUnit `[Fact]` tests" when writing tickets.

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| DW-B146 root cause identified (FindFollowerBracketOrder leaderName guard rejects PTT-TGT-Drag) | YES | B.1 |
| DW-B146 fix design: MatchesLeaderName helper extraction (mandatory -- CYC constraint) | YES | B.2, C |
| DW-B146 CYC stays = 8 after fix | YES | C CYC table |
| DW-B134-OCO root cause identified (oco="" → not in NT8 OCO group → survive flat) | YES | D, B.3 |
| DW-B134-OCO fix design: TrySweptPttDragOrphans + CancelPttDragOrphansForAccount | YES | D |
| DW-B134-OCO hook point: OnOrderUpdate pre-Gate-1 (Filled + follower + flat) | YES | B.4, D |
| DW-B134-OCO NT8 API confirmed: acc.Cancel(), acc.Orders, IsFlat, IsFollowerAccount | YES | B.5 |
| Scenario (a): flat cancels PTT-TGT-Drag | YES | G T2 entry 1 |
| Scenario (b): flat cancels PTT-STP-Drag | YES | G T2 entry 2 |
| Scenario (c): non-PTT Working orders NOT cancelled (regression) | YES | G T2 entry 3 |
| Scenario (d): partial fill does NOT trigger sweep | YES | G T2 entry 4 |
| Scenario (e): acc.Cancel exception absorbed | YES | G T2 entry 5 |

---

## Verdict

**REVIEW_PASS**

All Cycle 1 violations resolved. No new violations introduced by the Cycle 2 edit. Plan is cleared for Phase 3 (ticket generation).
