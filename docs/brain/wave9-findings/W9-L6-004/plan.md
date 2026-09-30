# W9-L6-004 Plan -- InvalidOperationException hot-path safety fix

## Finding Summary

| Field | Value |
|-------|-------|
| Finding ID | W9-L6-004 |
| File | `src/V12_002.Orders.Callbacks.cs` |
| Line | 232 |
| Violation | `throw new InvalidOperationException` on hot path from OnOrderUpdate |
| Rule | microsecond-eternity.md -- zero allocations per call on hot path |
| Fix option | (c) -- return false with diagnostic log instead of throwing |

---

## EXIT GATE RESULTS

- [x] **Confirmed return type**: `private bool HandleOrderState_Terminal(...)` -- returns `bool`
- [x] **Exact before/after diff**: see section below
- [x] **No edits made** -- plan only
- [x] **Plan written to**: `docs/brain/wave9-findings/W9-L6-004/plan.md`

---

## Confirmed Method Signature

```csharp
// src/V12_002.Orders.Callbacks.cs line 224
private bool HandleOrderState_Terminal(Order order, OrderState orderState, string nativeError)
```

---

## Full Current Method Body (lines 224-233)

```csharp
private bool HandleOrderState_Terminal(Order order, OrderState orderState, string nativeError)
{
    if (orderState == OrderState.Rejected)
        return HandleOrderRejected(order, nativeError);
    else if (orderState == OrderState.Cancelled)
        return HandleOrderCancelled(order);

    // Correctness by construction: throw for unhandled terminal states
    throw new InvalidOperationException("Unhandled terminal state: " + orderState.ToString());
}
```

---

## Exact Before/After Diff

**File**: `src/V12_002.Orders.Callbacks.cs`
**Lines affected**: 231-232 (2 lines -> 3 lines after change)

### BEFORE (exact, verbatim)
```
            // Correctness by construction: throw for unhandled terminal states
            throw new InvalidOperationException("Unhandled terminal state: " + orderState.ToString());
```

### AFTER
```
            // Unreachable branch -- ClassifyOrderState only routes Rejected/Cancelled here
            NinjaTrader.Code.Output.Process("Error HandleOrderState_Terminal: unhandled terminal state " + orderState.ToString(), PrintTo.OutputTab1);
            return false;
```

### Full method after fix
```csharp
private bool HandleOrderState_Terminal(Order order, OrderState orderState, string nativeError)
{
    if (orderState == OrderState.Rejected)
        return HandleOrderRejected(order, nativeError);
    else if (orderState == OrderState.Cancelled)
        return HandleOrderCancelled(order);

    // Unreachable branch -- ClassifyOrderState only routes Rejected/Cancelled here
    NinjaTrader.Code.Output.Process("Error HandleOrderState_Terminal: unhandled terminal state " + orderState.ToString(), PrintTo.OutputTab1);
    return false;
}
```

---

## Other throw new Statements in Method

**None.** `grep "throw new" src/V12_002.Orders.Callbacks.cs` returns exactly 1 match: line 232.
The fix eliminates the only `throw new` in the entire file.

---

## Caller Analysis -- No Changes Required

### Caller 1: DispatchOrderState (lines 247-271)
```csharp
else if (category == OrderStateCategory.Terminal)
    handled = HandleOrderState_Terminal(order, orderState, nativeError);
// ...
if (!handled && IsTerminalState(orderState))
    RemoveGhostOrderRef(order, orderState.ToString().ToUpper());
```
- `handled` stores the `bool` return. If `false` is returned (the new path), the guard at line 269
  calls `RemoveGhostOrderRef` -- the correct safe cleanup for an unknown terminal state.
- **No change needed.**

### Caller 2: ProcessOnOrderUpdate (lines 292-335)
```csharp
try
{
    DispatchOrderState(...);
}
catch (Exception ex)
{
    Print("ERROR OnOrderUpdate: " + ex.Message);
}
```
- Currently the `catch` swallows the `InvalidOperationException`. After the fix, no exception is
  thrown. The catch block remains a valid safety net for other unexpected errors.
- **No change needed.**

---

## Rationale

Per `docs/intel/jane-street/microsecond-eternity.md` (zero_alloc rule):
> Hot path = zero allocations per call. No LINQ, no new T() per call.

`throw new InvalidOperationException(...)` allocates an exception object and performs string
concatenation on every invocation. Even though the path is currently unreachable per
`ClassifyOrderState` logic (which only routes `Rejected|Cancelled` to the Terminal category),
the compiler does not know this, and a future logic change could make it reachable under load.

`NinjaTrader.Code.Output.Process(...)` is cold-path I/O -- correct for a diagnostic fallback.
`return false` triggers the existing `RemoveGhostOrderRef` ghost-cleanup guard in `DispatchOrderState`.

---

## Build Impact

`none` -- no API surface change, no signature change, behavior on all reachable paths unchanged.

---

## OKF Doc Read

`docs/intel/jane-street/microsecond-eternity.md` -- section: `zero_alloc`
