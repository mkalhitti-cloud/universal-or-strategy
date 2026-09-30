# W9-L4-002 Scan Report

**Status**: CONFIRMED

| Field | Value |
|-------|-------|
| W9_ID | W9-L4-002 |
| File | `src/V12_002.Orders.Callbacks.AccountOrders.cs` |
| Lines | 544–548 |
| Violation | LINQ `.Values.Any(f => ...)` on hot path |
| OKF Rule | Rule 7 — No LINQ on hot path |
| Priority | P1 |

---

## Violation Confirmed

```csharp
// src/V12_002.Orders.Callbacks.AccountOrders.cs:542–549
[System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining
)]
private bool IsAnyFollowerBracketActive(string acctName)
{
    return _followerBrackets.Values.Any(f =>   // ← line 544 — VIOLATION
        f != null
        && f.AccountName == acctName
        && (f.State == FollowerBracketState.Active || f.State == FollowerBracketState.Accepted)
    );
}
```

**Violation still present**: YES — confirmed by grep + read_file.

---

## Collection Type

`_followerBrackets` is declared in `src/V12_002.cs:829`:

```csharp
private readonly ConcurrentDictionary<string, FollowerBracketFSM> _followerBrackets =
    new ConcurrentDictionary<string, FollowerBracketFSM>();
```

`.Values` on `ConcurrentDictionary<string, FollowerBracketFSM>` returns `ICollection<FollowerBracketFSM>`.  
Calling `.Any(predicate)` on it:
- Allocates an `IEnumerator<FollowerBracketFSM>` on every call (heap alloc on hot path)
- Performs a full O(n) walk until the first match is found
- Pairs badly with `AggressiveInlining` — the allocation is inlined into the caller but cannot be elided by the JIT

---

## Hot Path Classification: YES

**Full call chain:**

```
acct.OrderUpdate (broker event, fires on every order state change)
  → OnAccountOrderUpdate(object sender, OrderEventArgs e)           :37
    → ProcessAccountOrder_EnqueueTerminalUpdate(...)                 (enqueues to _accountOrderQueue)
      → ProcessAccountOrderQueue()                                   :235 (drain loop, TriggerCustomEvent)
        → ProcessQueuedAccountOrder(item)                            :1123
          → DispatchMatchedFollowerResult(...)                       :1155
            → HandleMatchedFollowerOrder(...)                        :502
              → IsAnyFollowerBracketActive(acctName)                 :516  ← LINQ violation fires here
```

`OnAccountOrderUpdate` is a **broker-event callback** registered via:
```csharp
acct.OrderUpdate += OnAccountOrderUpdate;   // src/V12_002.SIMA.Lifecycle.cs:177
```
It fires on every order fill, cancel, and partial-fill for every subscribed fleet account.
Classification: **P1 hot path — confirmed**.

---

## Blast Radius

Single call site:

| Caller | File | Line |
|--------|------|------|
| `HandleMatchedFollowerOrder` | `src/V12_002.Orders.Callbacks.AccountOrders.cs` | 516 |

No other callers of `IsAnyFollowerBracketActive` exist in the codebase.

---

## NT8 API Context

Not applicable. `_followerBrackets` is a pure in-memory `ConcurrentDictionary` — no NinjaTrader API constraints on replacement.

---

## Recommended Fix

Replace the LINQ predicate with an explicit `foreach` loop and early return.  
**Minimal change — method body only, no signature or visibility changes.**

```csharp
// BEFORE (line 542–549)
[System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining
)]
private bool IsAnyFollowerBracketActive(string acctName)
{
    return _followerBrackets.Values.Any(f =>
        f != null
        && f.AccountName == acctName
        && (f.State == FollowerBracketState.Active || f.State == FollowerBracketState.Accepted)
    );
}

// AFTER — zero alloc, early return, same semantics
[System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining
)]
private bool IsAnyFollowerBracketActive(string acctName)
{
    foreach (var f in _followerBrackets.Values)
    {
        if (
            f != null
            && f.AccountName == acctName
            && (f.State == FollowerBracketState.Active || f.State == FollowerBracketState.Accepted)
        )
            return true;
    }
    return false;
}
```

**Why this is minimal:**
- No new methods, no new classes, no signature changes
- Semantically identical to the LINQ version — same short-circuit behaviour, same predicate logic
- Eliminates the `IEnumerator<FollowerBracketFSM>` heap allocation on every broker event callback
- `AggressiveInlining` is preserved — the loop body is now JIT-inlineable without a hidden enumerator closure

---

## Test Requirement

**YES** — new unit test stubs required.

```csharp
// xUnit stubs (file: tests/.../IsAnyFollowerBracketActiveTests.cs)

[Fact]
public void IsAnyFollowerBracketActive_ReturnsTrue_WhenMatchingActiveStateExists()
{
    // Arrange: _followerBrackets contains one FSM with AccountName="ACCT1", State=Active
    // Act: IsAnyFollowerBracketActive("ACCT1")
    // Assert: returns true
}

[Fact]
public void IsAnyFollowerBracketActive_ReturnsTrue_WhenMatchingAcceptedStateExists()
{
    // Arrange: _followerBrackets contains one FSM with AccountName="ACCT1", State=Accepted
    // Act: IsAnyFollowerBracketActive("ACCT1")
    // Assert: returns true
}

[Fact]
public void IsAnyFollowerBracketActive_ReturnsFalse_WhenNoAccountNameMatch()
{
    // Arrange: _followerBrackets contains FSMs for "ACCT2" only
    // Act: IsAnyFollowerBracketActive("ACCT1")
    // Assert: returns false
}

[Fact]
public void IsAnyFollowerBracketActive_ReturnsFalse_WhenStateIsNotActiveOrAccepted()
{
    // Arrange: _followerBrackets contains FSM with AccountName="ACCT1", State=Closed
    // Act: IsAnyFollowerBracketActive("ACCT1")
    // Assert: returns false
}

[Fact]
public void IsAnyFollowerBracketActive_ReturnsFalse_WhenDictionaryIsEmpty()
{
    // Arrange: _followerBrackets is empty
    // Act: IsAnyFollowerBracketActive("ACCT1")
    // Assert: returns false
}
```

---

## Summary

| Item | Detail |
|------|--------|
| Violation present | YES |
| File | `src/V12_002.Orders.Callbacks.AccountOrders.cs` |
| Exact lines | 544–548 |
| Method | `IsAnyFollowerBracketActive(string acctName)` |
| Collection | `ConcurrentDictionary<string, FollowerBracketFSM>._followerBrackets.Values` |
| Hot path | YES — fires inside `OnAccountOrderUpdate` broker callback chain |
| Blast radius | 1 call site — `HandleMatchedFollowerOrder` at line 516 |
| Fix type | Replace `.Values.Any(lambda)` with `foreach` + early return |
| Test required | YES — 5 xUnit stubs above |
