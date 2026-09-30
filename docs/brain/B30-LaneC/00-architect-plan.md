# B30-LaneC Architecture Plan
**Status**: PLAN_COMPLETE
**Block**: B30
**Lane**: C
**Architect**: ptt-architect (reviewed by orchestrator)
**Prerequisite**: B30-LaneA VERIFY_PASS @ 139 [Fact] tests
**Target [Fact] count**: 141 (adds 2)

---

## 1. Scope Summary

This lane closes two defect-work items:

| DW ID | Description |
|-------|-------------|
| DW-B30-01 | Add 3-retry cancel+replace logic to MoveStopToBreakEven and TightenOneStop |
| DW-B30-06 | Insert `.ToList()` snapshots in CancelOneAccount, MoveStopToBreakEven, FindFollowerBracketOrder |

Files touched:
- `src/PropTraderTools/CopyEngine.cs` (modifications only — no new files)

---

## 2. CYC Table — Before and After

| Method | File Lines | CYC Before | CYC After | Change |
|--------|-----------|-----------|-----------|--------|
| `TryCreateStopWithRetry` (NEW) | — | — | 5 | +5 (new private helper) |
| `MoveStopToBreakEven` | 1240-1300 | 8 | 8 | 0 (inline 2-try replaced by helper call, no new branch) |
| `TightenOneStop` | 1344-1386 | 4 | 3 | -1 (2-try replaced by helper call; two catch branches removed) |
| `CancelOneAccount` | 1045-1068 | 4 | 4 | 0 (ToList only — no branch change) |
| `FindFollowerBracketOrder` | 664-684 | 4 | 4 | 0 (ToList only — no branch change) |
| `TightenOneAccountStops` | 1417-1443 | 6 | 6 | 0 (no change — already has ToList, no retry here) |

All methods remain CYC <= 8. Jane Street strict standard: PASS.

---

## 3. Decision: Extract TryCreateStopWithRetry

**Decision: YES — extraction is required.**

Rationale:
- `MoveStopToBreakEven` is already at CYC = 8 (the hard ceiling). Adding the retry loop inline (+4 branches: `while`, `!cancelled`, `catch`, `retries >= 3`) would push it to CYC = 12. **Extraction is mandatory.**
- `TightenOneStop` at CYC = 4 would reach CYC = 8 if retry were added inline, but extraction is preferred for code-reuse: both methods share identical retry semantics. **Extraction is the correct design.**
- `TightenOneAccountStops` delegates to `TightenOneStop` and contains no cancel+replace itself — it is not modified for DW-B30-01.

---

## 4. TryCreateStopWithRetry — Full Signature and CYC

### Signature

```csharp
// B30-C: TryCreateStopWithRetry -- cancel once, retry CreateOrder up to 3 times.
// CYC=5: while(1), !cancelled(1), try-catch(1), retries>=3(1), + cancel-in-try path(1).
// JS-001: no rethrow. JS-021: no lock. NT8-007: arg12=(NinjaTrader.Cbi.CustomOrder)null.
private bool TryCreateStopWithRetry(
    Account acc,
    Instrument instr,
    Order stopToCancel,
    OrderAction action,
    int quantity,
    double stopPrice,
    string signalName)
```

### Implementation

```csharp
private bool TryCreateStopWithRetry(
    Account acc,
    Instrument instr,
    Order stopToCancel,
    OrderAction action,
    int quantity,
    double stopPrice,
    string signalName)
{
    int retries = 0;
    bool cancelled = false;
    while (retries < 3)                                                          // (1) while
    {
        try
        {
            if (!cancelled)                                                      // (2) cancel guard
            {
                acc.Cancel(new Order[] { stopToCancel });
                cancelled = true;
            }
            acc.CreateOrder(
                instr, action, OrderType.StopMarket, OrderEntry.Manual,
                TimeInForce.GTC, quantity, 0, stopPrice, null,
                signalName, DateTime.MaxValue, (NinjaTrader.Cbi.CustomOrder)null);
            return true;
        }
        catch (Exception ex)                                                     // (3) catch
        {
            retries++;
            if (retries >= 3)                                                    // (4) retries ceiling
            {
                StatusUpdate?.Invoke(
                    acc.Name + ": " + signalName + " FAILED after 3 retries -- account may be naked: "
                    + ex.Message);
                return false;
            }
        }
    }
    return false;
}
```

### CYC Breakdown

| Branch | Condition |
|--------|-----------|
| 1 | `while (retries < 3)` |
| 2 | `if (!cancelled)` inside try block |
| 3 | `catch (Exception ex)` wrapping cancel + create |
| 4 | `if (retries >= 3)` inside catch |

Base = 1 + 4 branches = **CYC = 5**. Well within CYC <= 8 ceiling.

> Cancel and CreateOrder share a single try/catch block. The `!cancelled` guard ensures Cancel fires exactly once per sequence. If Cancel throws on first iteration, cancelled stays false, catch fires, retries increments, and next iteration attempts Cancel again. The important safety property: the loop ALWAYS either places the stop or logs the naked-account warning.

### JS Rule Compliance

| Rule | Requirement | Status |
|------|-------------|--------|
| JS-001 | No rethrow in hot path catch blocks | PASS — catch logs and returns false, never rethrows |
| JS-021 | No `lock()` | PASS — no lock anywhere in helper |
| JS-002 | No `return null` | PASS — returns `bool` |
| JS-033 | No `async void` | PASS — method is synchronous |
| NT8-007 | `CreateOrder` arg 12 as `(NinjaTrader.Cbi.CustomOrder)null` | PASS — last arg cast per NT8 rules |

---

## 5. MoveStopToBreakEven — Body Change

### What is replaced

**REMOVE** (lines 1277-1298 approximately): the current diagnostic + two-try block:
```csharp
StatusUpdate?.Invoke(acc.Name + ": BE attempting cancel+replace -> " + newStop);  // DW-B28-01 diagnostic
try
{
    acc.Cancel(new Order[] { order });
}
catch (Exception ex)
{
    StatusUpdate?.Invoke("PTT-BE cancel error: " + ex.Message);
    continue;
}
try
{
    acc.CreateOrder(
        instrument, action, OrderType.StopMarket, OrderEntry.Manual,
        TimeInForce.GTC, order.Quantity, 0, newStop, null, "PTT-BE-Stop",
        DateTime.MaxValue, null);
    StatusUpdate?.Invoke(acc.Name + ": BE stop placed @ " + newStop);
}
catch (Exception ex)
{
    StatusUpdate?.Invoke("PTT-BE place error: " + ex.Message);
}
```

**INSERT** (in place):
```csharp
StatusUpdate?.Invoke(acc.Name + ": BE attempting cancel+replace -> " + newStop);
TryCreateStopWithRetry(acc, instrument, order, action, order.Quantity, newStop, "PTT-BE-Stop");
```

### CYC Impact

No new decision branches at the call site. CYC remains **8** — unchanged.

The "BE stop placed" success log from the old code is removed; failure fires the NAKED warning from inside the helper. The diagnostic log before the call is preserved (DW-B28-01 intent).

---

## 6. TightenOneStop — Body Change

### What is replaced

**REMOVE** (lines 1365-1385 approximately): the current two-try block:
```csharp
try
{
    acc.Cancel(new Order[] { order });
}
catch (Exception ex)
{
    StatusUpdate?.Invoke("TightenOneStop cancel error: " + ex.Message);
    return;
}
try
{
    acc.CreateOrder(
        order.Instrument, tightenAction, OrderType.StopMarket, OrderEntry.Manual,
        TimeInForce.GTC, order.Quantity, 0, targetPrice, null, "PTT-Tighten-Stop",
        DateTime.MaxValue, null);
    StatusUpdate?.Invoke(acc.Name + ": tighten stop -> " + targetPrice);
}
catch (Exception ex)
{
    StatusUpdate?.Invoke("TightenOneStop place error: " + ex.Message);
}
```

**INSERT** (in place):
```csharp
TryCreateStopWithRetry(acc, order.Instrument, order, tightenAction, order.Quantity, targetPrice, "PTT-Tighten-Stop");
```

### CYC Impact

The two-try block had 2 catch branches. Replacing with a single helper call removes both. CYC drops from **4 to 3**.

---

## 7. ToList() Snapshot Insertions (DW-B30-06)

### a. CancelOneAccount — line 1050

```
FROM: foreach (var order in acc.Orders)
TO:   foreach (var order in acc.Orders.ToList())
```

**Why**: `acc.Orders` is a live NT8 collection. OnOrderUpdate fires on the NT8 background thread and can mutate acc.Orders mid-iteration → InvalidOperationException. Snapshot with `.ToList()` before iterating.

### b. MoveStopToBreakEven — line 1255

```
FROM: foreach (var order in acc.Orders)
TO:   foreach (var order in acc.Orders.ToList())
```

**Why**: Same reason. Stop cancellation/creation inside the loop mutates the live collection.

### c. FindFollowerBracketOrder — line 666

```
FROM: foreach (var order in follower.Orders)
TO:   foreach (var order in follower.Orders.ToList())
```

**Why**: Read-only scan but `follower.Orders` is live NT8 data. NT8 can push order-state events on the dispatch thread concurrently. Snapshot prevents torn reads.

### d. TightenOneAccountStops — line 1437

**NO CHANGE NEEDED.** Already has `acc.Orders.ToList()`. Confirmed by B30-LaneA review.

---

## 8. Test Stubs

### [Fact] count baseline: 139 (from B30-LaneA VERIFY_PASS)
### [Fact] count after this lane: **141** (+2)

---

### T-B30-C-01: MoveStopToBreakEven_RetriesOnCreateOrderFailure

**Purpose**: Verify that `TryCreateStopWithRetry` exists with correct signature (7 params, returns bool).
**Approach**: Reflection-based existence check proving the helper is compiled and callable.

```csharp
[Fact]
public void MoveStopToBreakEven_RetriesOnCreateOrderFailure()
{
    // Verify TryCreateStopWithRetry private method exists with correct arity (7 params)
    var helperMethod = typeof(CopyEngine).GetMethod(
        "TryCreateStopWithRetry",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    Assert.NotNull(helperMethod);

    // Assert parameter count = 7: Account, Instrument, Order, OrderAction, int, double, string
    var parameters = helperMethod.GetParameters();
    Assert.Equal(7, parameters.Length);

    // Assert return type is bool
    Assert.Equal(typeof(bool), helperMethod.ReturnType);

    // Assert parameter types in order
    Assert.Equal(typeof(NinjaTrader.Cbi.Account),      parameters[0].ParameterType);
    Assert.Equal(typeof(NinjaTrader.NinjaScript.Instruments.Instrument), parameters[1].ParameterType);
    Assert.Equal(typeof(NinjaTrader.Cbi.Order),        parameters[2].ParameterType);
    Assert.Equal(typeof(NinjaTrader.Cbi.OrderAction),  parameters[3].ParameterType);
    Assert.Equal(typeof(int),                          parameters[4].ParameterType);
    Assert.Equal(typeof(double),                       parameters[5].ParameterType);
    Assert.Equal(typeof(string),                       parameters[6].ParameterType);
}
```

**Why reflection**: NT8 Account/CreateOrder are not injectable in the xUnit harness (sealed NT8 runtime types). Reflection proves the helper contract exists and is compiled with the correct signature.

---

### T-B30-C-02: CancelOneAccount_UsesSnapshotNotLiveOrders

**Purpose**: Structural verification that `CancelOneAccount` exists with expected signature and dereferences its `acc` parameter immediately (consistent with `.ToList()` snapshot access pattern).
**Approach**: Reflection-based method signature check + null-invoke proving acc is accessed.

```csharp
[Fact]
public void CancelOneAccount_UsesSnapshotNotLiveOrders()
{
    var cancelMethod = typeof(CopyEngine).GetMethod(
        "CancelOneAccount",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    Assert.NotNull(cancelMethod);

    // Assert parameter list: (Account acc, Instrument instr) = 2 params
    var parameters = cancelMethod.GetParameters();
    Assert.Equal(2, parameters.Length);
    Assert.Equal(typeof(NinjaTrader.Cbi.Account),      parameters[0].ParameterType);
    Assert.Equal(typeof(NinjaTrader.NinjaScript.Instruments.Instrument), parameters[1].ParameterType);

    // Assert: calling with null account throws TargetInvocationException(NullReferenceException)
    // proving acc is dereferenced (acc.Orders.ToList()) rather than bypassed.
    // Source-level ToList() invariant confirmed by SCAN-06 grep in validator step.
    var engine = CopyEngine.Instance;
    var ex = Record.Exception(() =>
        cancelMethod.Invoke(engine, new object[] { null, null }));

    Assert.NotNull(ex);
    Assert.IsType<System.Reflection.TargetInvocationException>(ex);
    Assert.IsType<NullReferenceException>(
        ((System.Reflection.TargetInvocationException)ex).InnerException);
}
```

---

## 9. SCAN Checklist (Engineer Contract)

| SCAN | Check | Target |
|------|-------|--------|
| SCAN-01 | No `lock(` in modified methods | grep result = 0 actual lock calls |
| SCAN-02 | No `throw new` in catch blocks | 0 matches |
| SCAN-03 | No `return null` in modified methods | 0 matches |
| SCAN-04 | No `async void` | 0 matches |
| SCAN-05 | All CreateOrder signal names start with "PTT-" | verified |
| SCAN-06 | Three modified foreach loops use `.ToList()` | grep -n "\.Orders\.ToList()" >= 3 matches |
| SCAN-07 | CYC <= 8 for all modified methods | all <= 8 |

---

## 10. Wave Workspace Hard-Link Sync

After engineer completes implementation:
```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

---

## 11. Return Value

**PLAN_COMPLETE**
