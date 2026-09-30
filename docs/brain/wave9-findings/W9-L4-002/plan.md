# W9-L4-002 Fix Plan

**Status**: READY TO EXECUTE
**Priority**: P1 (HOT PATH)
**OKF Rule**: Rule 7 -- zero allocations per call on hot-path methods

---

## Problem

`IsAnyFollowerBracketActive` calls `.Values.Any(lambda)` on a
`ConcurrentDictionary<string, FollowerBracketFSM>` on every broker order-event callback.

`ConcurrentDictionary.Values` returns an `ICollection<T>`. Calling `.Any(predicate)` on it
calls `Enumerable.Any<T>(IEnumerable<T>, Func<T,bool>)`, which:

1. Allocates an `IEnumerator<FollowerBracketFSM>` on the heap on every invocation.
2. Allocates a closure (`Func<T,bool>`) capturing `acctName`.
3. Is marked `[AggressiveInlining]` -- the allocation is inlined into every caller, making
   the heap pressure invisible in profiler traces.

The method is reachable via:

```
OnAccountOrderUpdate (broker event, fires per fill/cancel/partial-fill)
  -> ProcessAccountOrderQueue
    -> ProcessQueuedAccountOrder
      -> DispatchMatchedFollowerResult
        -> HandleMatchedFollowerOrder
          -> IsAnyFollowerBracketActive  <-- violation fires here
```

This is a **P1 hot path** under the OKF Rule 7 (microsecond-eternity.md): zero allocations per
call; no LINQ on hot-path methods.

---

## File

`src/V12_002.Orders.Callbacks.AccountOrders.cs`

---

## Exact Before (lines 539--549)

```csharp
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
```

---

## Exact After

```csharp
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

---

## Diff Summary

| | Before | After |
|-|--------|-------|
| Lines changed | 544--548 (body only) | 544--551 (body replaced) |
| Heap allocations per call | 2 (enumerator + closure) | 0 |
| Early-return semantics | preserved via `.Any()` | preserved via `return true` |
| Predicate logic | identical | identical |
| Method signature | unchanged | unchanged |
| `[AggressiveInlining]` | preserved | preserved |
| CYC delta | 1 (lambda) | 2 (foreach + if) -- stays well under CYC 8 |

---

## Semantics Preservation

The `foreach` loop is semantically identical to `.Any(predicate)`:
- Iterates `_followerBrackets.Values` in the same order.
- Returns `true` on the first element satisfying all three conditions.
- Returns `false` if no element matches (empty dict included).
- Null-safe: `f != null` guard is retained as the first condition.

`ConcurrentDictionary.Values` enumerator is snapshot-safe for reads -- no locking required,
consistent with the OKF Rule 1 (lock-free patterns). The fix does not introduce any lock.

---

## Blast Radius

| Caller | Line | Impact |
|--------|------|--------|
| `HandleMatchedFollowerOrder` | 516 | call site unchanged -- bool return type preserved |

No other callers. No signature change. No other files affected.

---

## Rationale

Per `docs/intel/jane-street/microsecond-eternity.md`, Rule 7:
> "Hot path = zero allocations per call. No LINQ, no new T() per call."

The `[AggressiveInlining]` attribute signals intent that this method is performance-critical.
Using LINQ inside an inlined hot-path method contradicts that intent: the JIT cannot elide the
enumerator or closure allocations even when inlining the call site.

The `foreach` replacement is the canonical zero-alloc equivalent. No new abstractions, no scope
creep, minimal diff.

---

## Build Impact

`none` -- method body change only, same return type, same signature.

---

## OKF Doc Read

`docs/intel/jane-street/microsecond-eternity.md` -- section: zero_alloc (hot path = no LINQ).

---

## Test Requirement

5 xUnit `[Fact]` stubs required (see `scan.md`):

| Test | Condition | Expected |
|------|-----------|----------|
| `IsAnyFollowerBracketActive_ReturnsTrue_WhenMatchingActiveStateExists` | dict has FSM for ACCT1, State=Active | `true` |
| `IsAnyFollowerBracketActive_ReturnsTrue_WhenMatchingAcceptedStateExists` | dict has FSM for ACCT1, State=Accepted | `true` |
| `IsAnyFollowerBracketActive_ReturnsFalse_WhenNoAccountNameMatch` | dict has FSMs for ACCT2 only | `false` |
| `IsAnyFollowerBracketActive_ReturnsFalse_WhenStateIsNotActiveOrAccepted` | dict has FSM for ACCT1, State=Closed | `false` |
| `IsAnyFollowerBracketActive_ReturnsFalse_WhenDictionaryIsEmpty` | dict is empty | `false` |

Test project: `tests/V12_Performance.Tests/`
Framework: xUnit `[Fact]` only -- NUnit/MSTest BANNED per OKF Rule 10.

---

## Execution Checklist

- [ ] Apply diff to `src/V12_002.Orders.Callbacks.AccountOrders.cs` lines 542--549
- [ ] Verify build passes: `dotnet build`
- [ ] Verify no new lint violations: `powershell -File scripts/lint.ps1`
- [ ] Add 5 xUnit tests
- [ ] Run tests: `dotnet test`
- [ ] Run pre-push validation: `powershell -File scripts/pre_push_validation.ps1 -Fast`
- [ ] Verify `grep -n "\.Values\.Any" src/V12_002.Orders.Callbacks.AccountOrders.cs` returns 0 matches
