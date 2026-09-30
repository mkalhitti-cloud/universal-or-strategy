# W9-L6-005 Scan Report

| Field | Value |
|---|---|
| **W9_ID** | W9-L6-005 |
| **File** | `src/SignalBroadcaster.cs` |
| **Line (registered)** | ~286 |
| **Line (confirmed)** | **286** |
| **Violation type** | `throw new` on hot path |
| **OKF Rule** | Rule 5 — Hot-path throw |
| **Status** | **CONFIRMED** |

---

## 1. Violation Confirmed

The `throw new` statement at **line 286** is still present.

```csharp
// src/SignalBroadcaster.cs : lines 281-293
public static void BroadcastTradeSignal(TradeSignal signal)
{
    // Struct validation: Check for uninitialized/default state
    if (string.IsNullOrEmpty(signal.SignalId))
    {
        throw new ArgumentException("SignalId cannot be null or empty", nameof(signal)); // LINE 286
    }

    signal.Timestamp = DateTime.UtcNow;

    // V12.Phase6: Safe per-handler invocation with subscriber isolation
    SafeInvoke(OnTradeSignal, signal);
}
```

- **Exception type**: `System.ArgumentException`
- **Full throw expression**: `throw new ArgumentException("SignalId cannot be null or empty", nameof(signal));`
- **Trigger condition**: `signal.SignalId` is null or empty (i.e., caller passed an uninitialized `TradeSignal` struct)

> **Note**: The same pattern appears in two sibling methods:
> - Line 303: `BroadcastTrailUpdate` — `throw new ArgumentException("SignalId cannot be null or empty", nameof(update));`
> - Line 318: `BroadcastTargetAction` — `throw new ArgumentException("SignalId cannot be null or empty", nameof(action));`
>
> W9-L6-003 also flagged these three as a group (raised as W9-L6-NEW-001/002/003).
> W9-L6-005 is the official register entry for `BroadcastTradeSignal` specifically.

---

## 2. Blast Radius — Callers

**grep results across all `*.cs` files**: `BroadcastTradeSignal` has **zero live callers**.

| Call Site | File | Status |
|---|---|---|
| Declaration | `src/SignalBroadcaster.cs:281` | Definition only |
| `ClearAllSubscribers()` only | `src/V12_002.Lifecycle.cs:234` | Teardown only, not a broadcast call |
| Commented reference | `src/V12_002.Lifecycle.cs:725` | `// SignalBroadcaster.OnExternalCommand += HandleExternalSignal;` |

**Key architectural note** (`src/V12_002.Entries.RMA.cs:35-37`):

```
// V12 SIMA: BroadcastEntrySignal and V8 Copy Trading region removed.
// Trade copying is replaced by direct Account.All iteration in ExecuteSmartDispatchEntry.
// SignalBroadcaster is retained for ClearAllSubscribers teardown (Lifecycle.cs).
```

The master-strategy copy-trading feature that would have called `BroadcastTradeSignal` was **removed** during V12 SIMA migration. `SignalBroadcaster` is now kept alive solely for its `ClearAllSubscribers()` teardown.

---

## 3. Hot-Path Classification

| Question | Answer |
|---|---|
| Called from `OnBarUpdate`? | **No** — zero callers |
| Called from `OnExecutionUpdate`? | **No** |
| Called from `OnOrderUpdate`? | **No** |
| Called from any `Dispatch*`? | **No** |
| Live callers exist? | **No** |

The method is **latent dead code** for the current codebase. It cannot be reached from any hot path, because the call site was removed in V12 SIMA. 

**Risk classification**: The throw is a latent violation — not actively on the hot path today, but the method signature is `public static void`, and any future caller from within `OnBarUpdate` or a dispatch chain would inherit the unhandled `ArgumentException`.

---

## 4. NT8 API Context

Not relevant. This is a pure C# guard check with no NinjaTrader API dependency.

---

## 5. Recommended Fix (Minimal)

Per **OKF Rule 5** (hot-path throw → return `bool`/`Result`, log via `Output.Process`):

Change the return type from `void` to `bool`. Replace the `throw` with a log-and-return.

```csharp
// BEFORE (line 281-293)
public static void BroadcastTradeSignal(TradeSignal signal)
{
    if (string.IsNullOrEmpty(signal.SignalId))
    {
        throw new ArgumentException("SignalId cannot be null or empty", nameof(signal));
    }
    signal.Timestamp = DateTime.UtcNow;
    SafeInvoke(OnTradeSignal, signal);
}

// AFTER
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

**Same minimal fix applies to sibling methods** (tracked as W9-L6-003):
- `BroadcastTrailUpdate` (line 298-308)
- `BroadcastTargetAction` (line 313-323)

**Blast radius of the fix**: Zero live callers means **no call sites need updating**. The return type change from `void` → `bool` is non-breaking in the current codebase.

---

## 6. Test Requirement

| Question | Answer |
|---|---|
| Existing test covers this? | No — no test file exercises `SignalBroadcaster` |
| New test needed? | YES (stub below) |

```csharp
// xUnit stub — add to tests/V12_Performance.Tests/SignalBroadcasterTests.cs
[Fact]
public void BroadcastTradeSignal_EmptySignalId_ReturnsFalse_DoesNotThrow()
{
    var signal = new SignalBroadcaster.TradeSignal { SignalId = "" };
    var result = SignalBroadcaster.BroadcastTradeSignal(signal);
    Assert.False(result);
}

[Fact]
public void BroadcastTradeSignal_ValidSignalId_ReturnsTrue()
{
    var signal = new SignalBroadcaster.TradeSignal { SignalId = "TEST-001" };
    var result = SignalBroadcaster.BroadcastTradeSignal(signal);
    Assert.True(result);
}
```

---

## 7. Summary

| Field | Value |
|---|---|
| Violation present | ✅ YES — line 286 |
| Exception type | `ArgumentException` |
| Condition | `signal.SignalId` null or empty |
| Live callers | **None** (dead code — V12 SIMA removed call sites) |
| Hot-path risk | Latent: zero current exposure, but `public static` means future re-introduction is risky |
| Fix complexity | Trivial — change `void` → `bool`, replace `throw` with `Output.Process` + `return false` |
| Caller update needed | **None** (zero callers) |
| Test required | YES (xUnit stub provided above) |
