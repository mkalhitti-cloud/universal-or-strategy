# W9-L6-003 Scan Report

**W9_ID**: W9-L6-003
**File**: src/V12_002.IO.PathValidation.cs
**Original Lines**: ~95 (third SecurityException throw in `ValidateAndCanonicalize`)
**Violation Type**: `throw new SecurityException` on hot path
**OKF Rule**: Rule 5 — hot-path throw → wrap in try/catch returning bool/Result
**Scan Date**: 2026-07-04
**Scanner**: Wave 9 Tier 3 Phase 1 Scanner

---

## Confirmation

**Status**: ALREADY_FIXED

```
$ grep -n "throw new" src/V12_002.IO.PathValidation.cs
(exit code 1 — zero matches)
```

`grep` returned exit code 1 (no lines matched), meaning **zero `throw new` statements remain** in
`src/V12_002.IO.PathValidation.cs`. The third SecurityException (originally at ~line 95) was
removed as part of the W9-L6-002 fix sweep that replaced both SecurityException throws in
`ValidateAndCanonicalize`.

**Conclusion**: W9-L6-003 is subsumed by the W9-L6-002 fix. No further action is needed in this
file.

---

## Blast Radius

None. The file is clean. No callers are affected.

---

## Full Inventory — Remaining `throw new` Across All src/ Files

The following is the complete post-W9-L6-002 inventory of `throw new` statements in `src/`.
Each is classified by hot-path status using the OKF Rule 5 criteria.

### Classification Legend

| Symbol | Meaning |
|--------|---------|
| ✅ COLD | Constructor / one-time init / `OnStateChange`. Not hot path. OKF: OK to throw. |
| ⚠️ REVIEW | Called from a recurring callback or order-event path. Requires deeper investigation. |

---

### 1. `src/SignalBroadcaster.cs` — lines 286, 303, 318

**Enclosing methods**: `BroadcastTradeSignal`, `BroadcastTrailUpdate`, `BroadcastTargetAction`

```csharp
// Line 286
public static void BroadcastTradeSignal(TradeSignal signal)
{
    if (string.IsNullOrEmpty(signal.SignalId))
        throw new ArgumentException("SignalId cannot be null or empty", nameof(signal));   // L286
    signal.Timestamp = DateTime.UtcNow;
    SafeInvoke(OnTradeSignal, signal);
}

// Line 303
public static void BroadcastTrailUpdate(TrailUpdateSignal update)
{
    if (string.IsNullOrEmpty(update.SignalId))
        throw new ArgumentException("SignalId cannot be null or empty", nameof(update));   // L303
    update.Timestamp = DateTime.UtcNow;
    SafeInvoke(OnTrailUpdate, update);
}

// Line 318
public static void BroadcastTargetAction(TargetActionSignal action)
{
    if (string.IsNullOrEmpty(action.SignalId))
        throw new ArgumentException("SignalId cannot be null or empty", nameof(action));   // L318
    action.Timestamp = DateTime.UtcNow;
    SafeInvoke(OnTargetAction, action);
}
```

**Assessment**: ⚠️ REVIEW — `BroadcastTradeSignal` is called by the master strategy on signal
dispatch (trading hot path). An unhandled `ArgumentException` here would crash the NinjaTrader
indicator thread. These are guard-on-entry throws; however per OKF Rule 5 they should return
`bool` / log instead of throwing if called from a hot path.

**OKF Fix Pattern**:
```csharp
public static bool BroadcastTradeSignal(TradeSignal signal)
{
    if (string.IsNullOrEmpty(signal.SignalId))
    {
        NinjaTrader.Code.Output.Process(
            "Error BroadcastTradeSignal: SignalId cannot be null or empty",
            PrintTo.OutputTab1);
        return false;
    }
    signal.Timestamp = DateTime.UtcNow;
    SafeInvoke(OnTradeSignal, signal);
    return true;
}
```
*Same pattern applies to `BroadcastTrailUpdate` and `BroadcastTargetAction`.*

> **Note**: These are **new findings** not tracked in the W9 register. They should be raised as
> W9-L6-NEW-001 / W9-L6-NEW-002 / W9-L6-NEW-003 entries.

---

### 2. `src/V12_002.Perf.LatencyHistogram.cs` — line 23

**Enclosing method**: `LatencyHistogram(string name)` constructor

```csharp
public LatencyHistogram(string name)
{
    _name = name ?? throw new ArgumentNullException(nameof(name));  // L23
    ...
}
```

**Assessment**: ✅ COLD — Constructor called once during strategy initialization (not hot path).
`ArgumentNullException` throw-expression is idiomatic C# null-guard in constructors.
OKF Rule 5 explicitly exempts non-hot-path throws. **No action required.**

---

### 3. `src/V12_002.Orders.Callbacks.cs` — line 232

**Enclosing method**: `HandleOrderState_Terminal(Order, OrderState, string)`

```csharp
private bool HandleOrderState_Terminal(Order order, OrderState orderState, string nativeError)
{
    if (orderState == OrderState.Rejected)
        return HandleOrderRejected(order, nativeError);
    else if (orderState == OrderState.Cancelled)
        return HandleOrderCancelled(order);

    // Correctness by construction: throw for unhandled terminal states
    throw new InvalidOperationException("Unhandled terminal state: " + orderState.ToString());  // L232
}
```

**Assessment**: ⚠️ REVIEW — This method IS called from the order-state callback path
(`OnOrderUpdate`). However the `throw` branch is a "correctness by construction" guard that fires
only if a **new, unhandled `OrderState` enum value** is added in a future NinjaTrader upgrade.
In practice this branch is never reached during normal operation. The comment documents this intent.

Per OKF Rule 5: "Non-hot-path throws OK." This path is only reached on programmer error (unhandled
enum extension), not during normal trading. The method already returns `bool` so adding a log+return
path is a minor enhancement but not a blocking violation.

**Conservative recommendation**: Convert the unreachable `throw` to a log + `return false` for
robustness under future NT8 SDK upgrades:
```csharp
NinjaTrader.Code.Output.Process(
    "Error HandleOrderState_Terminal: Unhandled terminal state " + orderState,
    PrintTo.OutputTab1);
return false;
```

> **Note**: Lower priority than SignalBroadcaster findings. Classify as W9-L6-NEW-004 if desired.

---

### 4. `src/V12_002.Photon.MmioMirror.cs` — lines 56, 58

**Enclosing method**: `MmioDispatchMirror(string, int, int, ulong)` constructor

```csharp
public MmioDispatchMirror(string name, int capacity, int slotSize, ulong salt)
{
    if (capacity < 2 || (capacity & (capacity - 1)) != 0)
        throw new ArgumentException("Capacity must be power of 2", "capacity");         // L56
    if (slotSize <= 0 || (slotSize & 7) != 0)
        throw new ArgumentException("Slot size must be a positive multiple of 8", "slotSize"); // L58
    ...
}
```

**Assessment**: ✅ COLD — Constructor-only, called once during `HandleConfigure()` (strategy
init). Not hot path. **No action required.**

---

### 5. `src/V12_002.Photon.Ring.cs` — line 55

**Enclosing method**: `SPSCRing<T>(int)` constructor

```csharp
public SPSCRing(int capacityPowerOf2)
{
    if (capacityPowerOf2 < 2 || (capacityPowerOf2 & (capacityPowerOf2 - 1)) != 0)
        throw new ArgumentException("Capacity must be power of 2", "capacityPowerOf2");  // L55
    ...
}
```

**Assessment**: ✅ COLD — Constructor-only, called once during strategy init. **No action required.**

---

### 6. `src/V12_002.Photon.Pool.cs` — line 227

**Enclosing method**: `PhotonHashIndex(int, int)` constructor (approximate)

```csharp
if ((tableCapacity & (tableCapacity - 1)) != 0)
    throw new ArgumentException("Table capacity must be power of 2");  // L227
```

**Assessment**: ✅ COLD — Constructor parameter validation, called once during strategy init.
**No action required.**

---

### 7. `src/V12_002.Lifecycle.cs` — line 463

**Enclosing method**: `HandleConfigure()` (private, called from `OnStateChange` → `Configure`)

```csharp
if (_slotSize != 64 || _shadowOffset != 56)
{
    throw new InvalidOperationException(
        string.Format(
            "FleetDispatchSlot layout invariant violated: size={0}, shadowOffset={1}; ...",
            _slotSize, _shadowOffset));  // L463-469
}
```

**Assessment**: ✅ COLD — This is a compile-time structural assertion that fires only if
`FleetDispatchSlot` memory layout has changed (impossible at runtime without a code change). It is
inside `HandleConfigure()`, which is the one-time strategy configuration callback. Not a hot path.
**No action required.**

---

## Summary Table

| File | Line(s) | Method | Hot Path? | Status | Action |
|------|---------|--------|-----------|--------|--------|
| `V12_002.IO.PathValidation.cs` | — | `ValidateAndCanonicalize` | YES | ✅ **ALREADY_FIXED** | None |
| `SignalBroadcaster.cs` | 286, 303, 318 | `Broadcast*` methods | **YES** | ⚠️ NEW FINDING | Raise W9-L6-NEW-001/002/003 |
| `V12_002.Perf.LatencyHistogram.cs` | 23 | `.ctor` | NO | ✅ OK | None |
| `V12_002.Orders.Callbacks.cs` | 232 | `HandleOrderState_Terminal` | Marginal | ⚠️ LOW RISK | Optional W9-L6-NEW-004 |
| `V12_002.Photon.MmioMirror.cs` | 56, 58 | `.ctor` | NO | ✅ OK | None |
| `V12_002.Photon.Ring.cs` | 55 | `.ctor` | NO | ✅ OK | None |
| `V12_002.Photon.Pool.cs` | 227 | `.ctor` | NO | ✅ OK | None |
| `V12_002.Lifecycle.cs` | 463 | `HandleConfigure` | NO | ✅ OK | None |

---

## NT8 API Context

- `BroadcastTradeSignal` et al. in `SignalBroadcaster.cs` are called from the strategy's
  `OnBarUpdate`/`OnMarketData` path (or equivalent signal-dispatch logic). NinjaTrader indicator
  threads do not catch unhandled exceptions from user code — an uncaught `ArgumentException`
  would surface as a "Strategy stopped due to unhandled exception" error in the Output tab.
- Constructor-level throws (Photon, LatencyHistogram, Lifecycle) are safe because NT8 wraps
  `OnStateChange(State.Configure)` in a try/catch that surfaces errors gracefully.

---

## Recommended Next Steps

1. **W9-L6-003**: Mark **ALREADY_FIXED** in the W9 register. No engineer action needed.
2. **New findings** (SignalBroadcaster.cs lines 286, 303, 318): Add to W9 register as
   `W9-L6-NEW-001`, `W9-L6-NEW-002`, `W9-L6-NEW-003`. These are genuine hot-path throws requiring
   the OKF Rule 5 `log+return false` fix pattern.
3. **V12_002.Orders.Callbacks.cs line 232**: Optional low-priority entry `W9-L6-NEW-004` if
   defensive hardening of the unreachable branch is desired.

---

## Test Requirement

**NO** — PathValidation.cs has no remaining throws. The W9-L6-002 fix is already covered by
existing path-validation tests. No new test stub required for this entry.
