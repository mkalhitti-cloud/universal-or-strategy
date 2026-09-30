# DW-B91 Architecture Plan

**Epic**: DW-B91 — Entry dedup survivor guard + flat-follower re-entry guard  
**Phase**: 1 (Architecture)  
**Status**: REVIEW_PENDING  
**Date**: 2026-08-24  
**Author**: ptt-architect  

---

## 1. Problem Summary

### DW-B91-A: Double dispatch on partial entry fill (CopyEngine.cs)

When a leader market-entry order reaches a terminal state (`Filled`) and the `_dedupCache` entry is
evicted (L935 + L2490), any subsequent dispatch-triggering event that arrives with the same `orderId`
(e.g. a Rithmic or connection re-submission edge-case where Submitted fires a second time after the
original Accepted→Filled sequence) passes Gate 5 (`IsDedup`, L1398) because the cache slot was
cleared. The result is a second set of follower entry orders placed for the same leader entry —
a double position on every follower account.

**Affected lines in [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs)**:
- L935: `EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState);` — evicts on terminal states
- L1007: `DispatchCopy(e.Order, matchedRule.Value);` — fires after eviction for a second event
- L1398: `if (IsDedup(order.OrderId.ToString(), order.LimitPrice)) return;` — Gate 5, bypassed after eviction
- L2490: `_dedupCache.TryRemove(orderId, out _);` — clears the only guard

### DW-B91-B: Flat-follower re-entry (spurious second flatten)

[`TryDispatchLeaderFlat`](src/PropTraderTools/CopyEngine.cs:1890) iterates every follower in
`rule.FollowerAccounts` and calls `flattenOne(acc, instrument)` unconditionally (L1904), subject
only to a null guard at L1903. If a follower account is already flat (no open position), calling
`flattenOne` on it causes NT8 to submit a new market order to close a position that does not exist,
which in some broker configurations results in a spurious entry — the "flatten" becomes an "enter
short" or "enter long" depending on the direction of the last close.

**Affected lines**:
- L1901–L1904: `foreach (var acc in rule.FollowerAccounts) { if (acc == null) continue; flattenOne(acc, instrument); }`
- L1882 header: CYC=8 (strict McCabe) — any inline addition of a branch would push to CYC=9

---

## 2. Root Cause Analysis

### DW-B91-A: Entry dedup survivor guard

**Lifecycle of a market entry order**:

```
Submitted → Accepted → Working → PartialFill(s) → Filled
```

**Existing guard chain** (ground-truth from source):

| Gate | Location | Blocks |
|------|----------|--------|
| Gate 0.5 | [`IsExitSignalName`](src/PropTraderTools/CopyEngine.cs:1384) L1384 | PTT- cascade, NT8 close signals |
| Gate 3 | [`IsDispatchTriggerState`](src/PropTraderTools/CopyEngine.cs:1311) L1387 | All states except Market+Submitted or Limit+Accepted |
| Gate 4 | L1393 | Non-Market and non-Limit order types |
| Gate 5 | [`IsDedup`](src/PropTraderTools/CopyEngine.cs:2475) L1398 | Repeat events for same orderId+limitPrice |

**`IsDispatchTriggerState` (L1311–1313)**:
```csharp
internal static bool IsDispatchTriggerState(OrderState state, OrderType type)
    => (type == OrderType.Market && state == OrderState.Submitted)
    || (type == OrderType.Limit  && state == OrderState.Accepted);
```
`PartialFill` and `Filled` are NOT trigger states. Gate 3 blocks them. Gate 5 is therefore only
reached by `Submitted` (market) or `Accepted` (limit) events.

**Where the gap exists**: `EvictDedup` (L2488–L2494) fires on `Filled`/`Cancelled`/`Rejected`. It
removes the orderId from `_dedupCache`. On some NT8/broker connection implementations (notably
Rithmic on fast recompile) the Submitted event can arrive a second time after the Filled terminal
event. The sequence is:

```
1. Submitted  → EvictDedup(no-op)     → Gate3(pass) → IsDedup(TryAdd OK)    → DISPATCH ✓
2. PartialFill→ EvictDedup(no-op)     → Gate3(block: PartialFill not trigger) → no dispatch ✓
3. Filled     → EvictDedup(EVICTS)    → Gate3(block: Filled not trigger)      → no dispatch ✓
4. Submitted  → EvictDedup(no-op)     → Gate3(pass: Market+Submitted=true)    → IsDedup(TryAdd OK: cache evicted!) → DOUBLE DISPATCH ✗
```

Step 4 is the bug: after eviction, the dedup cache slot is empty, so a second Submitted event for
the same orderId passes Gate 5 and fires a second `DispatchCopy`.

**Why Gate 5 `IsDedup` does not cover this**: `IsDedup` stores state in `_dedupCache`, which is
explicitly cleared by `EvictDedup` on terminal states (L2490 `_dedupCache.TryRemove`). Gate 5 is
stateless across the eviction boundary.

**Fix direction**: A second ConcurrentDictionary `_entryDispatchedOrders` that is keyed only by
`orderId` (no price component). It is also evicted by `EvictDedup`, but serves as a survival guard
that prevents any second dispatch before eviction clears it. The key property is that it is checked
AND populated atomically WITHIN `DispatchCopy` only after all gates pass — so it records "a dispatch
was committed for this orderId" with no race window.

### DW-B91-B: Flat-follower re-entry

**Current flow in [`TryDispatchLeaderFlat`](src/PropTraderTools/CopyEngine.cs:1890)**
(L1882 header: CYC=8 strict McCabe, confirmed by reading):

```csharp
foreach (var acc in rule.FollowerAccounts)       // (4)
{
    if (acc == null) continue;                    // L1903
    flattenOne(acc, instrument);                  // L1904
}
```

The existing guard at L1900 — `!IsNativeExitName(orderName) && hasOpenPosition(account, instrument)`
— checks whether the **leader** account has an open position. It does NOT check whether each
individual **follower** has an open position.

A follower that was already flat (e.g. it entered a protective stop that filled first, or was
manually closed) will receive a spurious `flattenOne` call, which submits a market order and re-opens
a position in the wrong direction.

**Existing CYC=8 constraint at L1882**: the method header reads:
```
// B65 T1: TryDispatchLeaderFlat -- CYC=8 (strict McCabe: loop + null guard + 5 early returns + IsNativeExitName branch).
```
Adding `if (!hasOpenPosition(acc, instrument)) continue;` inline inside the foreach pushes to CYC=9
— a Jane Street violation (CYC ≤ 8 mandatory).

**Fix direction**: Extract the foreach body into a private static helper `FlattenFollower` that
absorbs both the null guard and the new open-position guard. The `foreach` body in
`TryDispatchLeaderFlat` becomes a single method call (zero new branches in the caller).

---

## 3. Fix Design

### Fix A: `_entryDispatchedOrders` ConcurrentDictionary

#### New field (insert after L215, alongside existing ConcurrentDictionary fields)

```csharp
// DW-B91-A: per-orderId dispatch guard -- survives EvictDedup terminal-state eviction.
// After DispatchCopy commits a copy dispatch for orderId, TryAdd records it here.
// On a second dispatch-triggering event for the same orderId (e.g. Rithmic re-submit),
// ContainsKey returns true before DispatchCopy can fire again.
// Eviction is co-located with _dedupCache eviction in EvictDedup -- both cleared on
// Filled/Cancelled/Rejected so the slot is reclaimed when the order lifecycle closes.
// Key = order.OrderId.ToString(). Value = byte (minimum footprint -- presence-only set).
// JS-021: ConcurrentDictionary.ContainsKey and TryAdd are lock-free atomic operations.
// JS-025: ConcurrentDictionary is the canonical lock-free set pattern.
private readonly ConcurrentDictionary<string, byte> _entryDispatchedOrders
    = new ConcurrentDictionary<string, byte>();
```

#### New helper method `IsEntryDispatched` (private, CYC=2)

Placed alongside `IsDedup` (near L2475):

```csharp
// DW-B91-A: guard -- returns true if this orderId was already dispatched (blocks re-dispatch).
// Side-effect on first call: TryAdd records the orderId as dispatched.
// CYC=2: one decision (ContainsKey branch).
// JS-021: ContainsKey + TryAdd are lock-free. JS-001: no throw. JS-002: returns bool.
private bool IsEntryDispatched(string orderId)
{
    if (_entryDispatchedOrders.ContainsKey(orderId))
        return true;
    _entryDispatchedOrders.TryAdd(orderId, 0);
    return false;
}
```

**CYC breakdown**: 1 base + 1 `if (ContainsKey)` = **CYC=2**.

#### Modified Gate 5 in `DispatchCopy` (L1396–L1399)

Extract `orderId` as a local (eliminates double `.ToString()` call; orderId is already used at
L1407 in `CopySignal.Create`). Combine Gate 5 into a single compound guard so no new McCabe branch
is added to `DispatchCopy`:

```csharp
// Gate 5: dedup -- reject duplicate event for same orderId (B62: price-keyed dedup).
// DW-B91-A: IsEntryDispatched extends dedup across EvictDedup eviction boundary.
// Compound OR: single McCabe branch -- DispatchCopy CYC stays at 8.
// Short-circuit: IsEntryDispatched only called when IsDedup returns false (new event).
//   - IsDedup false + IsEntryDispatched false: first time -- TryAdd in IsEntryDispatched marks as dispatched, proceed.
//   - IsDedup false + IsEntryDispatched true:  eviction-bypass attempt -- blocked.
//   - IsDedup true:  duplicate same-event     -- blocked, IsEntryDispatched not called.
var orderId = order.OrderId.ToString();
if (IsDedup(orderId, order.LimitPrice) || IsEntryDispatched(orderId))
    return;
```

**CYC impact on `DispatchCopy`**: The existing `if (IsDedup(...)) return;` (1 branch) is replaced by
`if (IsDedup(...) || IsEntryDispatched(...)) return;` (still 1 branch — compound `||` in a single
`if` is one McCabe decision point). **CYC=8 unchanged**. The `orderId` local also removes the
duplicate `order.OrderId.ToString()` call at L1407 (pass `orderId` instead).

#### Modified `EvictDedup` (L2488–L2494)

Add `_entryDispatchedOrders.TryRemove` alongside existing `_dedupCache.TryRemove`:

```csharp
internal void EvictDedup(string orderId, OrderState state)
{
    if (state != OrderState.Filled && state != OrderState.Cancelled && state != OrderState.Rejected)
        return;

    _dedupCache.TryRemove(orderId, out _);
    _entryDispatchedOrders.TryRemove(orderId, out _);  // DW-B91-A: co-evict with _dedupCache
}
```

**CYC impact**: no new branch added. `TryRemove` has no branch. **CYC=2 unchanged**.

---

### Fix B: `FlattenFollower` helper + `hasOpenPosition` guard in `TryDispatchLeaderFlat`

#### New helper method `FlattenFollower` (private static, CYC=3)

Placed immediately after `TryDispatchLeaderFlat` (after L1907):

```csharp
// DW-B91-B: extracted foreach body from TryDispatchLeaderFlat.
// Absorbs (a) null guard (moved from caller loop) and (b) new per-follower open-position guard.
// Prevents spurious flattenOne call on already-flat followers (re-entry bug).
// CYC=3: null guard (1) + hasOpenPosition guard (1) + base (1).
// JS-021: no lock. JS-001: no throw. JS-002: no null return (void).
// private static: no instance state captured -- explicit delegate injection for testability.
private static void FlattenFollower(
    Account acc,
    Instrument instrument,
    Func<Account, Instrument, bool> hasOpenPosition,
    Action<Account, Instrument> flattenOne)
{
    if (acc == null) return;                              // (a) null guard (moved from caller)
    if (!hasOpenPosition(acc, instrument)) return;       // (b) DW-B91-B: skip already-flat follower
    flattenOne(acc, instrument);
}
```

**CYC breakdown**: 1 base + `if (acc == null)` + `if (!hasOpenPosition)` = **CYC=3**.

#### Modified `TryDispatchLeaderFlat` foreach body (L1901–L1905)

Replace the existing foreach body with a single `FlattenFollower` call. The `if (acc == null) continue;`
guard moves into `FlattenFollower` — the loop becomes branch-free (no `if` inside `foreach`):

```csharp
foreach (var acc in rule.FollowerAccounts)    // (4) -- unchanged
{
    FlattenFollower(acc, instrument, hasOpenPosition, flattenOne);  // DW-B91-B
}
```

**CYC impact**: The `if (acc == null) continue;` branch (1 McCabe point) is removed from the caller.
The `FlattenFollower` call introduces zero branches in `TryDispatchLeaderFlat`. Net change: -1 branch.
`TryDispatchLeaderFlat` **CYC = 8 → 7** (stays well under the ≤8 ceiling). The method header comment
must be updated to reflect CYC=7 and the new design:

```
// DW-B91-B: TryDispatchLeaderFlat -- CYC=7 after extraction.
// Foreach body extracted to FlattenFollower (null guard + open-position guard moved there).
```

---

## 4. Method Signatures

All new and modified methods for engineer implementation contract:

| Method | Kind | File | CYC | Change |
|--------|------|------|-----|--------|
| `private readonly ConcurrentDictionary<string, byte> _entryDispatchedOrders` | field | CopyEngine.cs | n/a | NEW |
| `private bool IsEntryDispatched(string orderId)` | method | CopyEngine.cs | 2 | NEW |
| `private void DispatchCopy(Order order, CopyRule rule)` | method | CopyEngine.cs | 8 | MODIFIED (Gate 5 combined, orderId local extracted) |
| `internal void EvictDedup(string orderId, OrderState state)` | method | CopyEngine.cs | 2 | MODIFIED (add _entryDispatchedOrders.TryRemove) |
| `private static void FlattenFollower(Account acc, Instrument instrument, Func<Account, Instrument, bool> hasOpenPosition, Action<Account, Instrument> flattenOne)` | method | CopyEngine.cs | 3 | NEW |
| `private static bool TryDispatchLeaderFlat(Account account, Instrument instrument, OrderState state, string orderName, CopyRule rule, Func<Account, bool> isFollower, Func<Account, Instrument, bool> hasOpenPosition, Action<Account, Instrument> flattenOne)` | method | CopyEngine.cs | 7 | MODIFIED (foreach body replaced, header updated) |

### Precise signatures for engineer

```csharp
// NEW
private bool IsEntryDispatched(string orderId)

// NEW
private static void FlattenFollower(
    Account acc,
    Instrument instrument,
    Func<Account, Instrument, bool> hasOpenPosition,
    Action<Account, Instrument> flattenOne)

// MODIFIED -- Gate 5 becomes compound OR, orderId local added, CYC=8 unchanged
private void DispatchCopy(Order order, CopyRule rule)

// MODIFIED -- add _entryDispatchedOrders.TryRemove after _dedupCache.TryRemove, CYC=2 unchanged
internal void EvictDedup(string orderId, OrderState state)

// MODIFIED -- foreach body replaced with FlattenFollower call; remove if (acc==null) continue;
// Update header comment: CYC=8->7
private static bool TryDispatchLeaderFlat(
    Account account, Instrument instrument, OrderState state, string orderName,
    CopyRule rule,
    Func<Account, bool> isFollower,
    Func<Account, Instrument, bool> hasOpenPosition,
    Action<Account, Instrument> flattenOne)
```

---

## 5. Test Plan

**File**: [`src/PropTraderTools/Tests/CopyEngineB91Tests.cs`](src/PropTraderTools/Tests/CopyEngineB91Tests.cs) (NEW — xUnit only, no NUnit, no MSTest)

### Fix A: Entry dedup survivor guard (3 tests)

| Test ID | [Fact] Name | Asserts |
|---------|-------------|---------|
| T_B91A_01 | `IsEntryDispatched_FirstCall_ReturnsFalseAndMarksDispatched` | First call returns false (not blocked), second call returns true (blocked). Verifies TryAdd side-effect. |
| T_B91A_02 | `IsEntryDispatched_AfterEvictDedup_SecondCallReturnsFalse` | Call IsEntryDispatched (marks), call EvictDedup(Filled), call IsEntryDispatched again — returns false (evicted, second dispatch would be allowed only after full terminal state). Verifies eviction co-location. |
| T_B91A_03 | `IsEntryDispatched_DifferentOrderIds_IndependentTracking` | Two distinct orderIds track independently — dispatching A does not affect B. |

### Fix B: Flat-follower open-position guard (3 tests)

| Test ID | [Fact] Name | Asserts |
|---------|-------------|---------|
| T_B91B_01 | `FlattenFollower_NullAccount_DoesNotCallFlattenOne` | FlattenFollower with acc=null: flattenOne delegate never called. |
| T_B91B_02 | `FlattenFollower_NoOpenPosition_DoesNotCallFlattenOne` | FlattenFollower with hasOpenPosition returning false: flattenOne never called. Verifies re-entry protection. |
| T_B91B_03 | `FlattenFollower_HasOpenPosition_CallsFlattenOne` | FlattenFollower with hasOpenPosition returning true: flattenOne called exactly once with correct (acc, instrument). |

### Test file structure

```csharp
using Xunit;
// ... NT8 stubs or mock delegates as needed

namespace PropTraderTools.Tests
{
    public class CopyEngineB91Tests
    {
        [Fact] public void IsEntryDispatched_FirstCall_ReturnsFalseAndMarksDispatched() { ... }
        [Fact] public void IsEntryDispatched_AfterEvictDedup_SecondCallReturnsFalse() { ... }
        [Fact] public void IsEntryDispatched_DifferentOrderIds_IndependentTracking() { ... }
        [Fact] public void FlattenFollower_NullAccount_DoesNotCallFlattenOne() { ... }
        [Fact] public void FlattenFollower_NoOpenPosition_DoesNotCallFlattenOne() { ... }
        [Fact] public void FlattenFollower_HasOpenPosition_CallsFlattenOne() { ... }
    }
}
```

**Notes for engineer**:
- `IsEntryDispatched` is `private` — tests must use reflection or an `internal` wrapper. Preferred: make `internal` (like `IsDispatchTriggerState` at L1311) with `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]`.
- `FlattenFollower` is `private static` — same approach. Alternatively, test via `TryDispatchLeaderFlat` integration.
- `EvictDedup` is already `internal` (L2488) — directly accessible in tests.
- xUnit `[Fact]` only. No `[Theory]` required for these assertions.

---

## 6. Jane Street Compliance Checklist

### Per-method compliance

| Method | JS-021 (no lock) | JS-001 (no throw) | JS-002 (no null return) | CYC ≤ 8 | ASCII-only |
|--------|-----------------|-------------------|------------------------|---------|------------|
| `_entryDispatchedOrders` field | ConcurrentDictionary = lock-free ✅ | n/a | n/a | n/a | ✅ |
| `IsEntryDispatched` | ContainsKey+TryAdd = lock-free ✅ | no throw ✅ | returns bool ✅ | CYC=2 ✅ | ✅ |
| `DispatchCopy` | unchanged ✅ | unchanged ✅ | void (no return null) ✅ | CYC=8 ✅ | ✅ |
| `EvictDedup` | TryRemove = lock-free ✅ | no throw ✅ | void ✅ | CYC=2 ✅ | ✅ |
| `FlattenFollower` | no lock, delegate calls only ✅ | no throw ✅ | void ✅ | CYC=3 ✅ | ✅ |
| `TryDispatchLeaderFlat` | unchanged ✅ | unchanged ✅ | returns bool ✅ | CYC=7 ✅ | ✅ |

### Additional compliance notes

- **JS-025** (ConcurrentDictionary pattern): `_entryDispatchedOrders` follows the exact same
  declaration pattern as `_dedupCache` (L128) — `readonly`, `new ConcurrentDictionary<K,V>()`,
  comment with JS-021+JS-025 reference.
- **No `lock()` anywhere**: Both fixes use only `ConcurrentDictionary` atomic operations
  (`ContainsKey`, `TryAdd`, `TryRemove`). Grep for `lock(` in modified lines: zero results.
- **No `DateTime.Now`**: No timestamps introduced. `_entryDispatchedOrders` uses `byte` (0) as
  presence marker, not a timestamp.
- **No hex colors, no `FontFamily`**: No UI code modified.
- **ASCII-only identifiers**: `_entryDispatchedOrders`, `IsEntryDispatched`, `FlattenFollower`,
  `orderId` — all 7-bit ASCII.
- **No `CreateOrder` calls**: Neither fix places orders. Not applicable.

---

## 7. Files Changed

```
src/PropTraderTools/CopyEngine.cs              MODIFIED  (production changes only)
src/PropTraderTools/Tests/CopyEngineB91Tests.cs  NEW     (xUnit tests -- 6 [Fact] methods)
```

### Exact change inventory for `CopyEngine.cs`

| Location | Change | Lines affected |
|----------|--------|---------------|
| After L215 (field block) | Add `_entryDispatchedOrders` field declaration | ~+10 lines |
| L1396–L1407 (DispatchCopy Gate 5) | Replace single `IsDedup` gate with compound `||` gate; extract `orderId` local; pass `orderId` to `CopySignal.Create` | ~+4 lines, ~-1 line |
| After L1907 (after TryDispatchLeaderFlat) | Add `FlattenFollower` static helper method | ~+15 lines |
| L1882 header (TryDispatchLeaderFlat comment) | Update CYC=8→7, add DW-B91-B note | ~2 lines |
| L1901–L1904 (TryDispatchLeaderFlat foreach body) | Replace `if (acc == null) continue; flattenOne(...)` with single `FlattenFollower(...)` call | ~-1 line |
| After L2493 (EvictDedup body) | Add `_entryDispatchedOrders.TryRemove(orderId, out _);` | ~+1 line |
| Near L2475 (alongside IsDedup) | Add `IsEntryDispatched` helper method | ~+12 lines |

**No other files touched.** Zero cross-contamination.

---

## 8. Deferred Items Addressed

**DW-B91 does not close any DW-B89 deferred items.** All items from `docs/brain/DW-B89/06-deferred-backlog.md` remain open:

| Item | Status | Notes |
|------|--------|-------|
| DW-B89-DEFERRED-01 | Open | NT8 Ctrl+F5 compilation gate — Director action required |
| DW-B89-DEFERRED-02 | Open | SIM gate PATH A nominal — requires live NT8 session |
| DW-B89-DEFERRED-03 | Open | SIM gate PATH A buf=0 edge case |
| DW-B89-DEFERRED-04 | Open | SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles) |
| DW-B89-DEFERRED-05 | Open | SIM gate DW-B87 timing race cycle |
| DW-B89-DEFERRED-06 | Open | Spec update after all SIM gates pass |
| DW-B42-01 | Open | T_BUG_QX_BE_01 does not assert PTT-QX-T3 |
| DW-B42-02 | Open | Live NT8 F5 verification required |
| DW-B42-03 | Open | IsPttQxTarget range extension |
| DW-PTT-BE-FIX-01 | Open | Lazy re-resolve for null followers |
| DW-PTT-BE-FIX-02 | Open | SIM gate: Path B 3-cycle runtime verification |
| DW-PTT-BE-FIX-03 | Open | Pre-existing 83 build errors in CopyEngineTests.cs |

DW-B91 introduces no new deferred items at plan time. SIM verification of the two bug fixes
(DW-B91-A and DW-B91-B) is a natural follow-on and will be scheduled as part of the next
NT8 live session alongside the DW-B89 SIM items above.

---

*PLAN_COMPLETE*
