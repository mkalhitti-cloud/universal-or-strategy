# W9-L6-004 Scan Report

## Identity

| Field | Value |
|---|---|
| W9_ID | W9-L6-004 |
| File | `src/V12_002.Orders.Callbacks.cs` |
| Line (confirmed) | **232** |
| Violation | `throw new InvalidOperationException` on hot path |
| OKF Rule | Rule 5 — hot-path throw |
| Status | **CONFIRMED** |

---

## Violation — Exact Code

**Method**: `HandleOrderState_Terminal` (line 224–233)

```csharp
// src/V12_002.Orders.Callbacks.cs:224
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

**Exact throw expression (line 232)**:
```csharp
throw new InvalidOperationException("Unhandled terminal state: " + orderState.ToString());
```

**Condition that triggers it**:  
`ClassifyOrderState(orderState)` returned `OrderStateCategory.Terminal`, AND `orderState` is neither  
`OrderState.Rejected` nor `OrderState.Cancelled`. With the current `ClassifyOrderState` implementation  
(lines 281–290) this cannot be reached at runtime — `Terminal` is only returned for `Rejected` or  
`Cancelled`. However the structural throw remains:
- A future addition to `ClassifyOrderState` routing a new `OrderState` enum value to `Terminal`  
  would immediately hit this path.
- The OKF Rule 5 prohibition on hot-path throws applies regardless of current reachability.

---

## Call Chain

```
OnOrderUpdate (line 170)         [NT8 override — called on every order state change]
  └─ Enqueue(ctx => ctx.ProcessOnOrderUpdate(...))  (line 194)
       └─ ProcessOnOrderUpdate (line 292)
            └─ DispatchOrderState (line 313)
                 └─ HandleOrderState_Terminal (line 265)
                      └─ throw new InvalidOperationException  (line 232)  ← VIOLATION
```

### Chain Evidence

| Link | Lines |
|---|---|
| `OnOrderUpdate` captures primitives and enqueues | 170–194 |
| `Enqueue(ctx => ctx.ProcessOnOrderUpdate(...))` | 194 |
| `ProcessOnOrderUpdate` calls `DispatchOrderState` | 313–323 |
| `DispatchOrderState` calls `HandleOrderState_Terminal` | 265 |
| `HandleOrderState_Terminal` throws | 232 |

**Call chain CONFIRMED.**

---

## Exception Handling in the Chain

| Method | try/catch present? | Behaviour |
|---|---|---|
| `OnOrderUpdate` | NO | thin shell, only captures + enqueues |
| `ProcessOnOrderUpdate` | **YES** — lines 307–328 | `catch (Exception ex)` → `Print("ERROR OnOrderUpdate: " + ex.Message)` |
| `DispatchOrderState` | NO | bare call, no handler |
| `HandleOrderState_Terminal` | NO | throws unconditionally |

**Key finding**: `ProcessOnOrderUpdate` wraps the entire dispatch in `try/catch (Exception ex)` (line  
325) and logs via `Print(...)`. This means the `InvalidOperationException` thrown at line 232 is  
**caught and swallowed** at line 326–328 rather than crashing the strategy. However:

1. The throw still allocates a full `InvalidOperationException` object on the hot path (GC pressure).
2. `Print(...)` is a blocking NT8 UI call that must not be invoked from an actor drain.
3. The catch block itself qualifies as a **secondary violation** (silent catch per OKF Rule 5).
4. The throw pattern is an OKF Rule 5 violation regardless of the outer catch.

---

## Hot-Path Classification

| Criterion | Assessment |
|---|---|
| Called from NT8 `OnOrderUpdate` override? | **YES** |
| Called on every order state change? | **YES** — every fill, cancel, reject, working transition |
| GC-sensitive? | **YES** — `InvalidOperationException` allocation on throw path |
| Latency-instrumented? | YES — `LatencyProbe.Start()` at line 305 confirms hot-path intent |

**HOT-PATH CLASSIFICATION: CONFIRMED.**

---

## Blast Radius

Only `HandleOrderState_Terminal` changes. Call sites:

| Site | File | Line |
|---|---|---|
| `DispatchOrderState` | `src/V12_002.Orders.Callbacks.cs` | 265 |

`HandleOrderState_Terminal` is `private` — no external callers. Blast radius is **1 call site**  
within the same file.

---

## NT8 API Context

- `OnOrderUpdate` is a NinjaTrader 8 override — guaranteed to be called on every brokerage order  
  state transition including fills, rejects, and cancels. It is a high-frequency callback.
- NT8 `Print(...)` (line 327) is an async-safe UI write, but it is still a blocking call when  
  invoked inside the Enqueue actor drain.
- No NT8 API requires throwing inside `OnOrderUpdate`; the correct pattern is return-bool +  
  log via `NinjaTrader.Code.Output.Process(...)`.

---

## Recommended Fix

**Strategy**: Replace `throw` with a logged early-return (`bool` return false + OKF-compliant log).  
This eliminates the exception allocation on the hot path and is the minimal OKF-compliant change.

```csharp
// BEFORE (line 224–233)
private bool HandleOrderState_Terminal(Order order, OrderState orderState, string nativeError)
{
    if (orderState == OrderState.Rejected)
        return HandleOrderRejected(order, nativeError);
    else if (orderState == OrderState.Cancelled)
        return HandleOrderCancelled(order);

    throw new InvalidOperationException("Unhandled terminal state: " + orderState.ToString());
}

// AFTER — OKF Rule 5 compliant (hot-path throw eliminated)
private bool HandleOrderState_Terminal(Order order, OrderState orderState, string nativeError)
{
    if (orderState == OrderState.Rejected)
        return HandleOrderRejected(order, nativeError);
    if (orderState == OrderState.Cancelled)
        return HandleOrderCancelled(order);

    NinjaTrader.Code.Output.Process(
        "Error HandleOrderState_Terminal: unhandled terminal state " + orderState.ToString(),
        PrintTo.OutputTab1);
    return false;
}
```

**Changes**: 1 line replaced (throw → OKF log + return false). No new public API. No callers need  
to change — `DispatchOrderState` already handles `handled == false` at line 269.

---

## Test Requirement

**NO new test needed.** The existing actor drain path is covered by  
`tests/V12_Performance.Tests/Core/FSMActorTests.cs`. The fix changes only the error branch  
(currently unreachable at runtime) and the return value remains `bool`. No new observable  
behaviour is introduced on the happy path.

---

## Secondary Violation Note

`ProcessOnOrderUpdate` lines 325–328 contains a `catch (Exception ex)` that calls `Print(...)` — a  
**silent catch** per OKF Rule 5. This is a separate W9 entry concern and is **out of scope** for  
this W9-L6-004 fix. Engineer should file a separate L3/L5 entry or address in a dedicated pass.

---

*Scan completed by W9 Tier 3 Phase 1 Scanner. Status: CONFIRMED. Engineer may proceed.*
