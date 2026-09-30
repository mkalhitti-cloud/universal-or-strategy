# B62-LaneA — Plan Review
# Live Entry Drag Sync + Price-Keyed Dedup Fix

**Block**: B62-LaneA
**Phase**: 2 (Plan Review)
**Reviewed by**: ptt-plan-reviewer
**Date**: 2026-08-11
**Input**: `docs/brain/B62-LaneA/02-architecture-plan.md`
**Source reads**: `CopyEngine.cs` lines 108-118, 600-665, 750-775, 860-935, 1448-1466
**Deferred read**: `docs/brain/B59-LaneA/06-deferred-backlog.md`

---

## Category 1 — Completeness vs Spec

### 1.1 All 7 Changes Documented
**PASS**
- Change 1: `_dedupCache` field type `long` → `double` — Section 3, Change 1.
- Change 2: `IsDedup` body replacement (price-keyed, no time expiry) — Section 3, Change 2.
- Change 3: `IsDedup` call site in `DispatchCopy` Gate 5 — Section 3, Change 3.
- Change 4: `EvictDedup` method addition — Section 3, Change 4.
- Change 5: `EvictDedup` wire-up in `OnOrderUpdate` pre-gate — Section 3, Change 5.
- Change 6: `FindFollowerEntryOrder` method addition — Section 3, Change 6.
- Change 7: `HandleEntryChange` method + Gate C in `OnOrderUpdate` — Section 3, Change 7A/7B.

All 7 changes are present and fully specified with Current/Required code blocks.

### 1.2 All 5 Test Specs Present
**PASS**
- T_B62_01 — `IsDedup_FirstCall_ReturnsFalse` — Section 7.
- T_B62_02 — `IsDedup_SecondCallSamePrice_ReturnsTrue` — Section 7.
- T_B62_03 — `EvictDedup_FilledState_RemovesEntry` — Section 7.
- T_B62_04 — `EvictDedup_WorkingState_DoesNotRemove` — Section 7.
- T_B62_05 — `EvictDedup_CancelledState_RemovesEntry` — Section 7.

### 1.3 NT8 API References Correct and Cited
**PASS**
`Account.Change(Order[])` cited as `NT8_FULL_REFERENCE.md` line 328-329.
`acc.Change(new Order[] { fo })` pattern confirmed in source at `CopyEngine.cs` line 871
(inside `SyncFollowerBracket`).

---

## Category 2 — NT8 API Correctness

### 2.1 `acc.Change(new Order[] { fo })` Pattern Matches SyncFollowerBracket
**PASS**
Source line 871: `acc.Change(new Order[] { fo });`
Plan Section 3 Change 7A uses identical convention.

### 2.2 `fo.LimitPrice = newPrice` Precedes `acc.Change()` Call
**PASS**
Source lines 870-871 (in `SyncFollowerBracket`):
```csharp
fo.LimitPrice = newPrice;
acc.Change(new Order[] { fo });
```
Plan `HandleEntryChange` body mirrors this exact sequence (plan lines 329-330).

### 2.3 Drag Event Sequence Matches Live Logs
**PASS**
Plan Section 2 documents the confirmed sequence (Change submitted → Accepted → Working) from
live logs 2026-08-11. Gate C is specified to fire on Accepted OR Working — both carry the new
price. The second event is a documented no-op (cache already updated to newPrice on first fire).

---

## Category 3 — Gate C Logic

### 3.1 Gate C Fires ONLY for `OrderType.Limit`
**PASS**
Plan Section 3 Change 7B: `if (e.Order.OrderType == OrderType.Limit ...)`

### 3.2 Gate C Fires for `Accepted` OR `Working`
**PASS**
Plan Section 3 Change 7B:
```csharp
&& (e.Order.OrderState == OrderState.Accepted || e.Order.OrderState == OrderState.Working)
```

### 3.3 Gate C Uses `_dedupCache.TryGetValue`
**PASS**
Plan Section 3 Change 7B:
```csharp
if (_dedupCache.TryGetValue(e.Order.OrderId.ToString(), out double storedPrice) ...
```
orderId must already be in cache (previously dispatched) for Gate C to fire.

### 3.4 Price Delta Guard Uses `>=` (not `>`)
**PASS**
Plan Section 3 Change 7B:
```csharp
&& Math.Abs(e.Order.LimitPrice - storedPrice) >= (e.Order.Instrument?.MasterInstrument?.TickSize ?? 0.01)
```
Exactly-one-tick delta correctly triggers drag sync. The fallback `0.01` (non-zero) is safe —
prevents spurious Gate C fire on instruments with unknown tickSize. Not a violation.

### 3.5 Gate C Returns After `HandleEntryChange`
**PASS**
Plan Section 3 Change 7B: `HandleEntryChange(e.Order, matchedRule.Value); return;`
Does NOT fall through to `DispatchCopy`.

---

## Category 4 — `_dedupCache` Semantic Change

### 4.1 Field Type Changed `long` → `double`
**PASS**
Source line 112 confirmed: `private readonly ConcurrentDictionary<string, long> _dedupCache = ...`
Plan Change 1 specifies exact replacement to `ConcurrentDictionary<string, double>`.

### 4.2 Old Time-Based Expiry Loop REMOVED from IsDedup
**PASS**
Source lines 1448-1465 confirm the current `IsDedup` has the `foreach` pruning loop and
`DateTime.UtcNow.Ticks` expiry. Plan Change 2 specifies a replacement body with no loop, no
timestamp usage. The comment explicitly states: "10-second pruning loop is deleted entirely".

### 4.3 New `IsDedup` Stores `limitPrice` (not timestamp)
**PASS**
Plan Change 2 new body: `if (!_dedupCache.TryAdd(orderId, limitPrice)) return true;`
Stores the `double limitPrice` parameter value directly.

### 4.4 `IsDedup` Call Site Passes `order.LimitPrice`
**PASS**
Plan Change 3: `if (IsDedup(order.OrderId.ToString(), order.LimitPrice)) return;`
Source line 763 confirmed current: `if (IsDedup(order.OrderId.ToString()))` — change is minimal
and correct.

---

## Category 5 — `EvictDedup`

### 5.1 `EvictDedup` Is `internal`
**PASS**
Plan Change 4: `internal void EvictDedup(string orderId, OrderState state)`

### 5.2 Evicts Only on Filled, Cancelled, Rejected
**PASS**
Plan Change 4:
```csharp
if (state != OrderState.Filled && state != OrderState.Cancelled && state != OrderState.Rejected)
    return;
```
Working, Accepted, ChangeSubmitted all cause early return (no eviction).

### 5.3 Wired in `OnOrderUpdate` PRE-GATE (before Gate 1)
**PASS**
Plan Change 5 inserts `EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState)` immediately
after `TryFirePositionState(e)`, before `if (!_isCopyEnabled) return;`. Source lines 602-607
confirm this is the correct pre-gate insertion point.

### 5.4 CYC = 2
**PASS**
One decision point: the terminal-state guard (`if (state != Filled && state != Cancelled && state
!= Rejected)`). `TryRemove` has no branch. CYC = 2.

---

## Category 6 — `FindFollowerEntryOrder`

### 6.1 Returns `Order?` (nullable)
**PASS**
Plan Change 6: `private static Order? FindFollowerEntryOrder(Account follower, Instrument instrument)`

### 6.2 Matches `Name == "PTT-Copy"` AND `OrderType.Limit` AND `OrderState.Working`
**PASS**
Plan Change 6:
```csharp
if (order.OrderState == OrderState.Working
    && order.OrderType == OrderType.Limit
    && order.Name == "PTT-Copy")
    return order;
```

### 6.3 Matches by `instrument` Object (not string name)
**PASS**
Plan Change 6: `if (order.Instrument != instrument) continue;`
Uses object reference comparison (NT8 `Instrument` object identity), not string name.

### 6.4 CYC = 3
**PASS**
Three decision points: `foreach` (1), `order.Instrument != instrument` guard (2), compound
`Working && Limit && Name` guard (3). CYC = 3.

---

## Category 7 — `HandleEntryChange`

### 7.1 Tick-Rounds Price BEFORE Comparing to Current Follower Price
**PASS**
Plan Change 7A: tick rounding (`Math.Round(rawPrice / tickSize) * tickSize`) is computed as
`newPrice` before the foreach loop. The per-account price-delta guard uses `newPrice` vs
`currentPrice` (follower's current `fo.LimitPrice`). Rounding precedes comparison.

### 7.2 Updates `_dedupCache[orderId]` with New Price BEFORE Looping Followers
**PASS**
Plan Change 7A:
```csharp
// Update stored price in dedup cache to track latest leader price.
_dedupCache[leaderOrder.OrderId.ToString()] = newPrice;

foreach (var acc in rule.FollowerAccounts)  // (4)
```
Cache update is explicitly placed before the `foreach`.

### 7.3 `try/catch` Around `acc.Change()`
**PASS**
Plan Change 7A wraps `fo.LimitPrice = newPrice; acc.Change(new Order[] { fo });` in `try/catch
(Exception ex)`. No `throw` propagates (JS-001).

### 7.4 No `lock()` Anywhere
**PASS**
No `lock` keyword appears in any new method in the plan. `_dedupCache` access uses
`ConcurrentDictionary` lock-free operations throughout.

### 7.5 CYC ≤ 8
**PASS** (annotation minor discrepancy — not a violation)
The plan CYC comment states `CYC=5` and enumerates five branch labels (1)-(5). However, the
actual code body contains six decision points:
  1. `instrument == null` guard
  2. `tickSize > 0` in tick-rounding ternary
  3. `foreach (var acc ...)`
  4. `acc == null` guard (labeled `(5)` in comment)
  5. `fo == null` guard (unlabeled in comment — missing from the annotation)
  6. `tickSize > 0 && Math.Abs(...)` price-delta guard (labeled `(3)` in comment)

Actual CYC = 6. The CYC annotation in the plan comment underestimates by 1 (omits the
`fo == null` branch). However, CYC = 6 ≤ 8 (JS limit), so this is **not a violation**.
The annotation numbering is cosmetically inconsistent but does not represent a compliance
failure. Engineer should correct the comment during implementation.

---

## Category 8 — Out-of-Scope Clarity

### 8.1 Plan Explicitly States What B62 Does NOT Do
**PASS**
Section 10 ("Out of Scope") enumerates 10 explicit exclusions:
1. Bracket order drag (Gate B / B10 path, unchanged)
2. Market order drag (NT8 doesn't support it)
3. Stop-limit entry drag (only `OrderType.Limit` in Gate C)
4. ATM strategy entry drag (DW-B54-01, blocked)
5. `IsExitSignalName` prefix fix (DW-B59-02, separate)
6. Leader manual close propagation (DW-B60-01, `TryDispatchLeaderFlat` already wired)
7. UI components (no UI changes)
8. Test infrastructure changes (5 new tests only, no new test projects)
9. Dedup cache serialization / persistence (in-memory only)
10. Multiple simultaneous drags (orderId uniqueness in NT8 handles this)

---

## Category 9 — Deferred Items (Carry-Forward)

### 9.1 All Open DW Items from B59-LaneA/06-deferred-backlog.md Carried Forward
**PASS**

B59-LaneA/06-deferred-backlog.md contains the following OPEN items:
- DW-B60-01 — Leader manual close does not close follower position (P1)
- DW-B59-02 — `IsExitSignalName` exact match instead of prefix (P1)
- DW-B58-01 — `SnapshotTargetsPublic` hardcoded order-name prefixes (P2)
- DW-B58-02 — `GlobalBe` non-atomic lazy init (P2)
- DW-B58-03 — `RelayBe` does not forward `OcoGroup` (P2)
- DW-B54-01 — ATM auto-inject (P1, blocked)
- PRE-EXISTING-01 — Non-ASCII at lines 395, 496 (P2)
- PRE-EXISTING-02 — Non-ASCII at lines 1256, 1257 (P2)
- PRE-EXISTING-03 — `deploy-sync.ps1` archived (P2)

Plan Section 9 carries forward all 9 items. Each carries correct priority, status
("OPEN — not addressed in B62"), and target block references.

Note: B62 Section 9 notes DW-B60-01 for potential closure verification (confirms
`TryDispatchLeaderFlat` is wired at line 646 per source read). This is an informational note,
not a scope change — verification is correctly deferred to B63 review.

---

## Category 10 — Jane Street Rules

### 10.1 JS-021: No `lock()` Anywhere in New Code
**PASS**
No `lock` statement appears in any new method specified in the plan:
`HandleEntryChange`, `FindFollowerEntryOrder`, `EvictDedup`, new `IsDedup`, Gate C inline block.
`_dedupCache` uses `ConcurrentDictionary` exclusively (lock-free: TryAdd, TryRemove, TryGetValue,
indexer assignment).

### 10.2 JS-001: No `throw new` in Hot Path
**PASS**
`HandleEntryChange` wraps `acc.Change()` in `try/catch (Exception ex)` and invokes
`StatusUpdate?.Invoke(...)` on catch — no rethrow, no new exception propagation.
No other new method uses `throw`.

### 10.3 JS-002: All Nullable Returns Are Explicitly `Order?`
**PASS**
`FindFollowerEntryOrder` declares return type `Order?`. Call site in `HandleEntryChange` uses
`if (fo == null) continue;` null guard. Contract is explicit and honored.

### 10.4 CYC ≤ 8 for Every New Method
**PASS**
| Method | Plan CYC | Actual CYC | Limit | Result |
|--------|----------|------------|-------|--------|
| `IsDedup` (new) | 2 | 2 | 8 | PASS |
| `EvictDedup` | 2 | 2 | 8 | PASS |
| `FindFollowerEntryOrder` | 3 | 3 | 8 | PASS |
| `HandleEntryChange` | 5 | 6 | 8 | PASS |
| Gate C inline | 2 | 2 | 8 | PASS |

### 10.5 ASCII-Only: No Unicode in String Literals
**PASS**
All new string literals in the plan use ASCII only:
- `"PTT-Copy"` — ASCII
- `"entry dragged -> "` — uses hyphen-minus (0x2D) and greater-than (0x3E), not Unicode arrows
- `"entry drag error: "` — ASCII
No emoji, no curly quotes, no box-drawing characters in new code.

Pre-existing non-ASCII at lines 395, 496, 1256, 1257 (confirmed in source) are carried forward
as PRE-EXISTING-01 and PRE-EXISTING-02. These are not introduced by B62.

### 10.6 xUnit Only: All Tests Use `[Fact]`
**PASS**
Plan Section 7 specifies: "Framework: xUnit [Fact] only. No NUnit, no MSTest." All 5 test
specs (T_B62_01 through T_B62_05) are `[Fact]`-based.

---

## Summary

| Category | Items | PASS | FAIL |
|----------|-------|------|------|
| 1 — Completeness vs Spec | 3 | 3 | 0 |
| 2 — NT8 API Correctness | 3 | 3 | 0 |
| 3 — Gate C Logic | 5 | 5 | 0 |
| 4 — `_dedupCache` Semantic Change | 4 | 4 | 0 |
| 5 — `EvictDedup` | 4 | 4 | 0 |
| 6 — `FindFollowerEntryOrder` | 4 | 4 | 0 |
| 7 — `HandleEntryChange` | 5 | 5 | 0 |
| 8 — Out-of-Scope Clarity | 1 | 1 | 0 |
| 9 — Deferred Items | 1 | 1 | 0 |
| 10 — Jane Street Rules | 6 | 6 | 0 |
| **TOTAL** | **36** | **36** | **0** |

### Engineer Notes (non-blocking, correct during implementation)

1. **`HandleEntryChange` CYC annotation**: Plan comment says `CYC=5` but actual decision count
   is 6 (the `fo == null` guard is unlabeled in the CYC annotation). Actual CYC=6 ≤ 8 — no
   violation. Engineer should update the comment to `CYC=6` and add the missing `// (5)` or
   `// (6)` annotation on the `if (fo == null) continue;` line.

2. **`HandleEntryChange` branch-number ordering**: Comment labels are (1), (2), (4), (5), (3)
   in code order. After correcting the CYC count, renumber sequentially in code-flow order
   for readability.

---

REVIEW_PASS
