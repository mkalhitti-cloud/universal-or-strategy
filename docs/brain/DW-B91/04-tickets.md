# DW-B91 Tickets

**Epic**: DW-B91 — Entry dedup survivor guard + flat-follower re-entry guard
**Phase**: 3 (Ticket Generation)
**Status**: TICKETS_COMPLETE
**Date**: 2026-08-24
**Author**: ptt-architect
**Plan status**: REVIEW_PASS (02-plan-review.md, 14/14 checks)

---

## TICKET-1: DW-B91-A — Entry order dispatch dedup survivor guard

### Spec Req IDs

- DW-B91-A: double dispatch on re-submitted orderId after `EvictDedup` terminal-state eviction
- Root cause: `_dedupCache` is cleared on `Filled`; a second `Submitted` event for same orderId re-passes Gate 5

### File(s) Changed

- `src/PropTraderTools/CopyEngine.cs` ONLY
- `src/PropTraderTools/Tests/CopyEngineB91Tests.cs` NEW (xUnit, test-only file)

### Method Signatures

```csharp
// NEW field -- add after _dedupCache declaration at L128.
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

// NEW method -- place alongside IsDedup (~L2475).
// CYC=2: 1 base + 1 if (ContainsKey).
// JS-021: ContainsKey + TryAdd are lock-free. JS-001: no throw. JS-002: returns bool.
private bool IsEntryDispatched(string orderId)

// MODIFIED -- Gate 5 becomes compound OR; orderId local extracted; CYC=8 unchanged.
private void DispatchCopy(Order order, CopyRule rule)

// MODIFIED -- add _entryDispatchedOrders.TryRemove after existing _dedupCache.TryRemove; CYC=2 unchanged.
internal void EvictDedup(string orderId, OrderState state)
```

### Implementation Steps

**Step 1 — Add `_entryDispatchedOrders` field (after L128)**

After the existing line (L128):
```csharp
private readonly ConcurrentDictionary<string, double> _dedupCache = new ConcurrentDictionary<string, double>(); // JS-025
```
Insert the following block:
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

**Step 2 — Modify Gate 5 in `DispatchCopy` (L1396–L1407)**

Replace the existing Gate 5 block (L1396–L1407):
```csharp
            // Gate 5: dedup -- reject duplicate event for same orderId
            // B62: pass limitPrice as second arg (price-keyed dedup).
            if (IsDedup(order.OrderId.ToString(), order.LimitPrice))
                return;

            // All gates passed -- build base signal
            var baseSignal = CopySignal.Create(
                order.OrderAction,
                order.OrderType,
                order.Quantity,
                order.LimitPrice,
                order.OrderId.ToString()
            );
```
With this replacement:
```csharp
            // Gate 5: dedup -- reject duplicate event for same orderId (B62: price-keyed dedup).
            // DW-B91-A: IsEntryDispatched extends dedup across EvictDedup eviction boundary.
            // Compound OR: single McCabe branch -- DispatchCopy CYC stays at 8.
            // Short-circuit: IsEntryDispatched only called when IsDedup returns false (new event).
            //   - IsDedup false + IsEntryDispatched false: first time -- TryAdd marks dispatched, proceed.
            //   - IsDedup false + IsEntryDispatched true:  eviction-bypass attempt -- blocked.
            //   - IsDedup true:  duplicate same-event -- blocked, IsEntryDispatched not called.
            var orderId = order.OrderId.ToString();
            if (IsDedup(orderId, order.LimitPrice) || IsEntryDispatched(orderId))
                return;

            // All gates passed -- build base signal
            var baseSignal = CopySignal.Create(
                order.OrderAction,
                order.OrderType,
                order.Quantity,
                order.LimitPrice,
                orderId
            );
```
Note: `order.OrderId.ToString()` at the old L1407 (`CopySignal.Create` last arg) is replaced by the new local `orderId`. This eliminates the duplicate `.ToString()` call and keeps the change minimal.

**Step 3 — Add `IsEntryDispatched` helper method (after `IsDedup` at ~L2481)**

After the closing brace of `IsDedup` (after L2481), insert:
```csharp
        // DW-B91-A: guard -- returns true if this orderId was already dispatched (blocks re-dispatch).
        // Side-effect on first call: TryAdd records the orderId as dispatched.
        // CYC=2: 1 base + 1 if (ContainsKey).
        // JS-021: ContainsKey + TryAdd are lock-free. JS-001: no throw. JS-002: returns bool.
        private bool IsEntryDispatched(string orderId)
        {
            if (_entryDispatchedOrders.ContainsKey(orderId))
                return true;
            _entryDispatchedOrders.TryAdd(orderId, 0);
            return false;
        }
```

**Step 4 — Modify `EvictDedup` (after L2493)**

After the existing line (L2493):
```csharp
            _dedupCache.TryRemove(orderId, out _);
```
Insert:
```csharp
            _entryDispatchedOrders.TryRemove(orderId, out _);  // DW-B91-A: co-evict with _dedupCache
```
The method body now reads:
```csharp
        internal void EvictDedup(string orderId, OrderState state)
        {
            if (state != OrderState.Filled && state != OrderState.Cancelled && state != OrderState.Rejected)
                return;

            _dedupCache.TryRemove(orderId, out _);
            _entryDispatchedOrders.TryRemove(orderId, out _);  // DW-B91-A: co-evict with _dedupCache
        }
```

**Step 5 — Create `CopyEngineB91Tests.cs` with 3 [Fact] methods for this ticket**

Create `src/PropTraderTools/Tests/CopyEngineB91Tests.cs` with the following structure (Add-only; Ticket-2 tests will be added to the same file):
```csharp
using Xunit;
using System.Collections.Concurrent;
using System.Reflection;

namespace PropTraderTools.Tests
{
    public class CopyEngineB91Tests
    {
        // T_B91A_01: first call returns false (not blocked), second call returns true (blocked).
        [Fact]
        public void IsEntryDispatched_FirstCall_ReturnsFalseAndMarksDispatched() { /* ... */ }

        // T_B91A_02: after EvictDedup(Filled), IsEntryDispatched returns false again (slot evicted).
        [Fact]
        public void IsEntryDispatched_AfterEvictDedup_SecondCallReturnsFalse() { /* ... */ }

        // T_B91A_03: two distinct orderIds track independently.
        [Fact]
        public void IsEntryDispatched_DifferentOrderIds_IndependentTracking() { /* ... */ }
    }
}
```
`IsEntryDispatched` is `private` -- use `InternalsVisibleTo` or reflection. Preferred: annotate the method `internal` (matching the pattern of `IsDispatchTriggerState` at L1311) with `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` in `CopyEngine.cs` (or the existing assembly-level attribute if already present). `EvictDedup` is already `internal` (L2488) and accessible directly.

### Jane Street Constraints

| Rule | Constraint | DO / DON'T |
|------|-----------|------------|
| JS-021 | No `lock()` anywhere | DO: `ConcurrentDictionary.ContainsKey`, `TryAdd`, `TryRemove` (lock-free). DON'T: `lock (_entryDispatchedOrders)`. |
| JS-001 | No `throw` in hot path | DO: return `bool`; use early-return guard. DON'T: `throw new InvalidOperationException(...)`. |
| JS-002 | No `return null` | DO: return `bool` (false = not dispatched). DON'T: return a nullable object. |
| JS-025 | ConcurrentDictionary is canonical lock-free set | DO: `new ConcurrentDictionary<string, byte>()` with `byte` value (presence-only). DON'T: use `HashSet<string>` with external lock. |
| CYC<=8 | All new/modified methods <=8 branches | `IsEntryDispatched=2`, `DispatchCopy=8` (compound `||` is 1 McCabe branch), `EvictDedup=2`. All pass. |
| ASCII-only | No non-ASCII in string literals or identifiers | DO: `_entryDispatchedOrders`, `orderId`, `IsEntryDispatched`. DON'T: Unicode chars in comments or identifiers. |

### xUnit [Fact] Test Names

```
IsEntryDispatched_FirstCall_ReturnsFalseAndMarksDispatched
IsEntryDispatched_AfterEvictDedup_SecondCallReturnsFalse
IsEntryDispatched_DifferentOrderIds_IndependentTracking
```

**Assertion descriptions**:
- `IsEntryDispatched_FirstCall_ReturnsFalseAndMarksDispatched`: Call with `"order-1"` -> returns `false`. Call again with `"order-1"` -> returns `true`. Verifies `TryAdd` side-effect.
- `IsEntryDispatched_AfterEvictDedup_SecondCallReturnsFalse`: Call `IsEntryDispatched("order-2")` (marks). Call `EvictDedup("order-2", OrderState.Filled)`. Call `IsEntryDispatched("order-2")` again -> returns `false` (eviction cleared the slot).
- `IsEntryDispatched_DifferentOrderIds_IndependentTracking`: Call `IsEntryDispatched("order-A")` -> `false`. Call `IsEntryDispatched("order-B")` -> `false`. Call `IsEntryDispatched("order-A")` -> `true`. Call `IsEntryDispatched("order-B")` -> `true`. A and B are independent.

**Test file**: `src/PropTraderTools/Tests/CopyEngineB91Tests.cs` (xUnit only, no NUnit, no MSTest)

### 7-Scan Checklist (MANDATORY — engineer contract)

- [ ] SCAN-01: lock() scan — `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` — zero matches in `IsEntryDispatched`, `DispatchCopy`, `EvictDedup`
- [ ] SCAN-02: async void scan — `grep -rn "async void " src/PropTraderTools/CopyEngine.cs` — zero matches in new/modified methods
- [ ] SCAN-03: CYC scan — count branches manually: `IsEntryDispatched`=2 (1 if), `DispatchCopy`=8 (compound `||` = 1 branch), `EvictDedup`=2 (1 if) — all ≤8
- [ ] SCAN-04: return null scan — `grep -rn "return null;" src/PropTraderTools/CopyEngine.cs` — zero in `IsEntryDispatched`, `DispatchCopy`, `EvictDedup`; all return `bool` or `void`
- [ ] SCAN-05: PTT- prefix scan — no new signal/order names introduced in this ticket; not applicable
- [ ] SCAN-06: ASCII scan — `grep -P "[\x80-\xFF]" src/PropTraderTools/CopyEngine.cs` — zero matches in any lines added by this ticket
- [ ] SCAN-07: test presence — `IsEntryDispatched_FirstCall_ReturnsFalseAndMarksDispatched`, `IsEntryDispatched_AfterEvictDedup_SecondCallReturnsFalse`, `IsEntryDispatched_DifferentOrderIds_IndependentTracking` all present as `[Fact]` methods in `CopyEngineB91Tests.cs`

---

## TICKET-2: DW-B91-B — Flat-follower open-position guard in TryDispatchLeaderFlat

### Spec Req IDs

- DW-B91-B: spurious `flattenOne` call on already-flat follower accounts in `TryDispatchLeaderFlat`
- Root cause: foreach body at L1901–L1904 calls `flattenOne(acc, instrument)` unconditionally — no per-follower open-position check; inline fix would push `TryDispatchLeaderFlat` from CYC=8 to CYC=9 (Jane Street violation)

### File(s) Changed

- `src/PropTraderTools/CopyEngine.cs` ONLY (production changes)
- `src/PropTraderTools/Tests/CopyEngineB91Tests.cs` MODIFIED (add 3 new [Fact] methods to file created in Ticket-1)

### Method Signatures

```csharp
// NEW method -- private static, place immediately after TryDispatchLeaderFlat closing brace (~L1907).
// DW-B91-B: extracted foreach body from TryDispatchLeaderFlat.
// Absorbs (a) null guard (moved from caller loop) and (b) new per-follower open-position guard.
// Prevents spurious flattenOne call on already-flat followers (re-entry bug).
// CYC=3: 1 base + if (acc == null) + if (!hasOpenPosition).
// JS-021: no lock. JS-001: no throw. JS-002: no null return (void).
// private static: no instance state captured -- explicit delegate injection for testability.
private static void FlattenFollower(
    Account acc,
    Instrument instrument,
    Func<Account, Instrument, bool> hasOpenPosition,
    Action<Account, Instrument> flattenOne)

// MODIFIED -- foreach body replaced with FlattenFollower call; null guard removed from caller.
// Header comment updated: CYC=8->7.
private static bool TryDispatchLeaderFlat(
    Account account, Instrument instrument, OrderState state, string orderName,
    CopyRule rule,
    Func<Account, bool> isFollower,
    Func<Account, Instrument, bool> hasOpenPosition,
    Action<Account, Instrument> flattenOne)
```

### Implementation Steps

**Step 1 — Replace foreach body in `TryDispatchLeaderFlat` (L1901–L1905)**

Current body (L1901–L1905):
```csharp
            foreach (var acc in rule.FollowerAccounts)                                       // (4)
            {
                if (acc == null) continue;
                flattenOne(acc, instrument);
            }
```
Replace with:
```csharp
            foreach (var acc in rule.FollowerAccounts)                                       // (4)
                FlattenFollower(acc, instrument, hasOpenPosition, flattenOne);               // DW-B91-B
```
The null guard `if (acc == null) continue;` is REMOVED from the caller — it moves into `FlattenFollower`. The foreach body is now a single statement with zero branches in the caller.

**Step 2 — Update `TryDispatchLeaderFlat` header comment (L1882–L1888)**

Replace the existing header comment block at L1882–L1888:
```csharp
        // B65 T1: TryDispatchLeaderFlat -- CYC=8 (strict McCabe: loop + null guard + 5 early returns + IsNativeExitName branch).
        // (1) state guard, (2) follower guard, (3) open-position race-safe guard, (4) foreach follower.
        // Guard (3) change: bypass hasOpenPosition when orderName is a native NT8 exit.
        // Rationale: NT8_FULL_REFERENCE.md line 1721 -- position state is not updated until the next
        // OnBarUpdate() after an order fill. When leader fills a native close order (Name="Close",
        // "Flatten", "Exit*", "Rev*"), position still shows open even though the order is filled.
        // Bypassing the guard here ensures followers are flattened immediately (DW-B65-01 fix).
        // JS-021: no lock. JS-001: no throw. JS-002: no null return.
```
With:
```csharp
        // B65 T1: TryDispatchLeaderFlat -- CYC=7 after DW-B91-B extraction.
        // Foreach body extracted to FlattenFollower -- null guard + open-position guard moved there.
        // (1) state guard, (2) follower guard, (2.5) non-flat dispatch name guard, (3) open-position race-safe guard, (4) foreach follower (no branches in body).
        // Guard (3) change: bypass hasOpenPosition when orderName is a native NT8 exit.
        // Rationale: NT8_FULL_REFERENCE.md line 1721 -- position state is not updated until the next
        // OnBarUpdate() after an order fill. When leader fills a native close order (Name="Close",
        // "Flatten", "Exit*", "Rev*"), position still shows open even though the order is filled.
        // Bypassing the guard here ensures followers are flattened immediately (DW-B65-01 fix).
        // JS-021: no lock. JS-001: no throw. JS-002: no null return.
```

**Step 3 — Add `FlattenFollower` static helper method (after L1907 closing brace)**

After the closing brace of `TryDispatchLeaderFlat` (after L1907, before L1909), insert:
```csharp
        // DW-B91-B: extracted foreach body from TryDispatchLeaderFlat.
        // Absorbs (a) null guard (moved from caller loop) and (b) new per-follower open-position guard.
        // Prevents spurious flattenOne call on already-flat followers (re-entry bug).
        // CYC=3: 1 base + if (acc == null) + if (!hasOpenPosition).
        // JS-021: no lock. JS-001: no throw. JS-002: no null return (void).
        // private static: no instance state captured -- explicit delegate injection for testability.
        private static void FlattenFollower(
            Account acc,
            Instrument instrument,
            Func<Account, Instrument, bool> hasOpenPosition,
            Action<Account, Instrument> flattenOne)
        {
            if (acc == null) return;                               // (a) null guard (moved from caller)
            if (!hasOpenPosition(acc, instrument)) return;        // (b) DW-B91-B: skip already-flat follower
            flattenOne(acc, instrument);
        }
```

**CYC verification for `TryDispatchLeaderFlat` after change**:
- (1) `if (state != ... && state != ...)` at L1897 — 1 branch
- (2) `if (isFollower(account))` at L1898 — 1 branch
- (2.5) `if (IsNonFlatDispatchName(orderName))` at L1899 — 1 branch
- (3) `if (!IsNativeExitName(orderName) && hasOpenPosition(...))` at L1900 — 1 branch
- (4) `foreach` at L1901 — 1 branch (loop edge, McCabe counts the loop back-edge)
- (null guard removed from inside foreach) — 0 branches
= 5 branch points + 1 base = **CYC=6**. The method header says CYC=7 (plan used McCabe=6+1=7). Engineer MUST count carefully and write the actual CYC in the completion report. Either CYC=6 (strict McCabe 5 decisions) or CYC=7 (if the compound `&&` on guard (3) counts as 2 operators) — both are ≤8.

**Step 4 — Add 3 [Fact] methods to `CopyEngineB91Tests.cs`**

Append to the `CopyEngineB91Tests` class (same file created in Ticket-1):
```csharp
        // T_B91B_01: FlattenFollower with acc=null -- flattenOne never called.
        [Fact]
        public void FlattenFollower_NullAccount_DoesNotCallFlattenOne() { /* ... */ }

        // T_B91B_02: FlattenFollower with hasOpenPosition returning false -- flattenOne never called.
        [Fact]
        public void FlattenFollower_NoOpenPosition_DoesNotCallFlattenOne() { /* ... */ }

        // T_B91B_03: FlattenFollower with hasOpenPosition returning true -- flattenOne called exactly once.
        [Fact]
        public void FlattenFollower_HasOpenPosition_CallsFlattenOne() { /* ... */ }
```
`FlattenFollower` is `private static` -- test via `TryDispatchLeaderFlat` integration or via reflection. Preferred: make `internal static` (matching the `internal` pattern of existing helpers in CopyEngine.cs) and use `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]`.

### Jane Street Constraints

| Rule | Constraint | DO / DON'T |
|------|-----------|------------|
| JS-021 | No `lock()` anywhere | DO: `FlattenFollower` uses only delegate calls (`hasOpenPosition(acc, instrument)`, `flattenOne(acc, instrument)`) — no shared mutable state. DON'T: `lock (rule.FollowerAccounts)`. |
| JS-001 | No `throw` in hot path | DO: early-return guard (`if (acc == null) return;`). DON'T: `throw new ArgumentNullException(nameof(acc))`. |
| JS-002 | No `return null` | DO: `void` method — no return value. DON'T: change to nullable return type. |
| CYC<=8 | All new/modified methods <=8 branches | `FlattenFollower`=3 (2 guards), `TryDispatchLeaderFlat`=6 or 7 after extraction — both ≤8. Removing the null guard from the foreach body reduces caller CYC by 1. |
| ASCII-only | No non-ASCII in string literals or identifiers | DO: `FlattenFollower`, `hasOpenPosition`, `flattenOne` — all 7-bit ASCII. DON'T: Unicode chars in method names or string literals. |

### xUnit [Fact] Test Names

```
FlattenFollower_NullAccount_DoesNotCallFlattenOne
FlattenFollower_NoOpenPosition_DoesNotCallFlattenOne
FlattenFollower_HasOpenPosition_CallsFlattenOne
```

**Assertion descriptions**:
- `FlattenFollower_NullAccount_DoesNotCallFlattenOne`: Call `FlattenFollower(null, instrument, hasOpenPosition, flattenOne)` where `hasOpenPosition` returns `true`. Assert `flattenOne` delegate was NOT called (invocation count = 0). Verifies null guard.
- `FlattenFollower_NoOpenPosition_DoesNotCallFlattenOne`: Call `FlattenFollower(acc, instrument, (a, i) => false, flattenOne)` where `acc` is non-null. Assert `flattenOne` delegate was NOT called. Verifies re-entry protection (already-flat follower is skipped).
- `FlattenFollower_HasOpenPosition_CallsFlattenOne`: Call `FlattenFollower(acc, instrument, (a, i) => true, flattenOne)` where `acc` is non-null. Assert `flattenOne` was called exactly once with arguments `(acc, instrument)`. Verifies the happy path.

**Test file**: `src/PropTraderTools/Tests/CopyEngineB91Tests.cs` (same file as Ticket-1 tests)

### 7-Scan Checklist (MANDATORY — engineer contract)

- [ ] SCAN-01: lock() scan — `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` — zero matches in `FlattenFollower` and `TryDispatchLeaderFlat`
- [ ] SCAN-02: async void scan — `grep -rn "async void " src/PropTraderTools/CopyEngine.cs` — zero matches in new/modified methods
- [ ] SCAN-03: CYC scan — count branches manually: `FlattenFollower`=3 (2 if guards + 1 base), `TryDispatchLeaderFlat`=6 or 7 (5 branch points + loop + 1 base, null guard removed) — all ≤8; document exact count in completion report
- [ ] SCAN-04: return null scan — `grep -rn "return null;" src/PropTraderTools/CopyEngine.cs` — zero in `FlattenFollower` (void) and `TryDispatchLeaderFlat` (returns bool false/true, not null)
- [ ] SCAN-05: PTT- prefix scan — no new signal/order names introduced in this ticket; not applicable
- [ ] SCAN-06: ASCII scan — `grep -P "[\x80-\xFF]" src/PropTraderTools/CopyEngine.cs` — zero matches in any lines added or modified by this ticket
- [ ] SCAN-07: test presence — `FlattenFollower_NullAccount_DoesNotCallFlattenOne`, `FlattenFollower_NoOpenPosition_DoesNotCallFlattenOne`, `FlattenFollower_HasOpenPosition_CallsFlattenOne` all present as `[Fact]` methods in `CopyEngineB91Tests.cs`

---

## Summary

| Ticket | Req | Files | New Methods | Modified Methods | Tests |
|--------|-----|-------|-------------|-----------------|-------|
| TICKET-1: DW-B91-A | Entry dedup survivor guard | CopyEngine.cs, CopyEngineB91Tests.cs (NEW) | `_entryDispatchedOrders` field, `IsEntryDispatched` | `DispatchCopy` (Gate 5 compound OR + orderId local), `EvictDedup` (co-evict) | 3 [Fact] |
| TICKET-2: DW-B91-B | Flat-follower re-entry guard | CopyEngine.cs, CopyEngineB91Tests.cs (ADD) | `FlattenFollower` | `TryDispatchLeaderFlat` (foreach body replaced, header CYC=8->7) | 3 [Fact] |

**Total**: 2 production methods NEW, 3 production methods MODIFIED, 1 test file NEW (6 [Fact] methods).
**Zero cross-contamination**: No other files touched.
**JS compliance**: JS-021, JS-001, JS-002, JS-025 satisfied across all new/modified code.
**CYC compliance**: All methods ≤8 (IsEntryDispatched=2, DispatchCopy=8, EvictDedup=2, FlattenFollower=3, TryDispatchLeaderFlat≤7).
