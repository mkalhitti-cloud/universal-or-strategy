# W9-L6-005 / W9-L6-006 / W9-L6-007 -- Batch Fix Plan
## Replace ArgumentException throws with log + early return

**Covers**: W9-L6-005 (line 286), W9-L6-006 (line 303), W9-L6-007 (line 318)
**File**: `src/SignalBroadcaster.cs`
**Status**: PLAN ONLY -- no edits made yet

---

## Return Type Decision

**Keep `void` for all three methods.**

Rationale:
- Zero live callers exist -- no call sites to update.
- Changing to `bool` expands the public API contract with no benefit.
- `void` produces the minimal diff: 1 line removed + 2 lines added per method.
- Per OKF microsecond-eternity.md zero_alloc: the log call is already on the
  non-hot path (guard branch). No new allocation on the valid execution path.
- Per OKF production-engineering-billions.md: throwing from a public entry
  point before `SafeInvoke` bypasses subscriber isolation. Log + return is
  the correct production pattern.

---

## W9-L6-005 -- BroadcastTradeSignal (line 286)

### BEFORE (lines 281-293)
```csharp
public static void BroadcastTradeSignal(TradeSignal signal)
{
    // Struct validation: Check for uninitialized/default state
    if (string.IsNullOrEmpty(signal.SignalId))
    {
        throw new ArgumentException("SignalId cannot be null or empty", nameof(signal));
    }

    signal.Timestamp = DateTime.UtcNow;

    // V12.Phase6: Safe per-handler invocation with subscriber isolation
    SafeInvoke(OnTradeSignal, signal);
}
```

### AFTER
```csharp
public static void BroadcastTradeSignal(TradeSignal signal)
{
    // Struct validation: Check for uninitialized/default state
    if (string.IsNullOrEmpty(signal.SignalId))
    {
        NinjaTrader.Code.Output.Process("Error BroadcastTradeSignal: SignalId cannot be null or empty", PrintTo.OutputTab1);
        return;
    }

    signal.Timestamp = DateTime.UtcNow;

    // V12.Phase6: Safe per-handler invocation with subscriber isolation
    SafeInvoke(OnTradeSignal, signal);
}
```

### Exact search/replace
**Remove** (line 286):
```
        throw new ArgumentException("SignalId cannot be null or empty", nameof(signal));
```
**Insert in its place**:
```
        NinjaTrader.Code.Output.Process("Error BroadcastTradeSignal: SignalId cannot be null or empty", PrintTo.OutputTab1);
        return;
```

---

## W9-L6-006 -- BroadcastTrailUpdate (line 303)

### BEFORE (lines 298-308)
```csharp
public static void BroadcastTrailUpdate(TrailUpdateSignal update)
{
    // Struct validation: Check for uninitialized/default state
    if (string.IsNullOrEmpty(update.SignalId))
    {
        throw new ArgumentException("SignalId cannot be null or empty", nameof(update));
    }

    update.Timestamp = DateTime.UtcNow;
    SafeInvoke(OnTrailUpdate, update);
}
```

### AFTER
```csharp
public static void BroadcastTrailUpdate(TrailUpdateSignal update)
{
    // Struct validation: Check for uninitialized/default state
    if (string.IsNullOrEmpty(update.SignalId))
    {
        NinjaTrader.Code.Output.Process("Error BroadcastTrailUpdate: SignalId cannot be null or empty", PrintTo.OutputTab1);
        return;
    }

    update.Timestamp = DateTime.UtcNow;
    SafeInvoke(OnTrailUpdate, update);
}
```

### Exact search/replace
**Remove** (line 303):
```
        throw new ArgumentException("SignalId cannot be null or empty", nameof(update));
```
**Insert in its place**:
```
        NinjaTrader.Code.Output.Process("Error BroadcastTrailUpdate: SignalId cannot be null or empty", PrintTo.OutputTab1);
        return;
```

---

## W9-L6-007 -- BroadcastTargetAction (line 318)

### BEFORE (lines 313-323)
```csharp
public static void BroadcastTargetAction(TargetActionSignal action)
{
    // Struct validation: Check for uninitialized/default state
    if (string.IsNullOrEmpty(action.SignalId))
    {
        throw new ArgumentException("SignalId cannot be null or empty", nameof(action));
    }

    action.Timestamp = DateTime.UtcNow;
    SafeInvoke(OnTargetAction, action);
}
```

### AFTER
```csharp
public static void BroadcastTargetAction(TargetActionSignal action)
{
    // Struct validation: Check for uninitialized/default state
    if (string.IsNullOrEmpty(action.SignalId))
    {
        NinjaTrader.Code.Output.Process("Error BroadcastTargetAction: SignalId cannot be null or empty", PrintTo.OutputTab1);
        return;
    }

    action.Timestamp = DateTime.UtcNow;
    SafeInvoke(OnTargetAction, action);
}
```

### Exact search/replace
**Remove** (line 318):
```
        throw new ArgumentException("SignalId cannot be null or empty", nameof(action));
```
**Insert in its place**:
```
        NinjaTrader.Code.Output.Process("Error BroadcastTargetAction: SignalId cannot be null or empty", PrintTo.OutputTab1);
        return;
```

---

## Implementation Instructions (for executor)

Apply all three changes in a single `apply_diff` call to `src/SignalBroadcaster.cs`:

1. **Line 286**: replace `throw new ArgumentException(...)` with log + return (BroadcastTradeSignal)
2. **Line 303**: replace `throw new ArgumentException(...)` with log + return (BroadcastTrailUpdate)
3. **Line 318**: replace `throw new ArgumentException(...)` with log + return (BroadcastTargetAction)

No method signatures change. No callers change. No other lines touched.

After applying:
- Run `dotnet build` -- must produce 0 errors, 0 new warnings.
- Run `grep -n "throw new ArgumentException" src/SignalBroadcaster.cs` -- must return 0 results.
- No new `lock()` introduced.
- No `DateTime.Now` introduced (existing `DateTime.UtcNow` at lines 289, 306, 321 unchanged).
- All three log strings are ASCII-only.

---

## OKF References
- `production-engineering-billions.md`: rate_limiting -- exceptions at public API entry bypass safety gates.
- `microsecond-eternity.md`: zero_alloc -- no new allocation on the valid (non-guard) hot path.
- Rule 11 (ASCII only): all log strings are ASCII.
- Rule 3 (FSM determinism): `DateTime.UtcNow` on lines 289/306/321 is pre-existing and correct.
