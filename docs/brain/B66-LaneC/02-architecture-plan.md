# B66-LaneC Architecture Plan (REWRITE)
**Bug**: DW-B64-01 (partial) -- StopLimit drag-sync fails for all three in-scope code paths
**File**: `docs/brain/B66-LaneC/02-architecture-plan.md`
**Status**: REVIEW_PENDING
**Rewrite reason**: NT8 indexed evidence (FIX-PM-02 / FIX-PM-02b) confirmed additional facts that
change scope vs. the prior version. DispatchCopy Gate 4+5 (formerly Defect 4) is **deferred** as
DW-B66-C-02. The drag-sync path (Defects 1-3) remains fully in-scope and is now precisely specified
using confirmed NT8 ground truth.

---

## 1. Summary

### What the defects are

`CopyEngine.cs` has three independent gates that together completely block the drag-sync pipeline
for `OrderType.StopLimit` leader entry orders:

1. **Gate C type guard** (`CopyEngine.cs` line 669) only accepts `OrderType.Limit`. `StopLimit`
   orders are rejected before `HandleEntryChange` is ever called.
2. **`FindFollowerEntryOrder`** (lines 986-988) only matches `Working + Limit + "PTT-Copy"`.
   StopLimit follower orders and orders in `Accepted` state are never found.
3. **`HandleEntryChange`** (lines 1007, 1024, 1030) reads `LimitPrice` and writes to `LimitPrice`
   unconditionally. For a StopLimit order, the dragged price is in `StopPrice` -- `LimitPrice` is
   always 0 (NT8 confirmed). The copy price change is silently lost.

### What the deferred item is

**DW-B66-C-02** (P1): `DispatchCopy` Gate 5 (line 805) passes `order.LimitPrice` to `IsDedup`.
Since StopLimit.LimitPrice == 0 always, every StopLimit entry shares dedup key 0.0. This causes
the second and subsequent StopLimit entry dispatches on any instrument to be wrongly deduped as
duplicates. **This is a separate defect affecting the initial copy-dispatch path, not the drag-sync
path.** It is deferred to B67+ (see Section 4 and `docs/brain/B66-LaneC/06-deferred-backlog.md`).

### What this block fixes

Three surgical corrections to `CopyEngine.cs` plus two new private static helper methods:

- Widen Gate C type guard to accept `StopLimit`; read price via `GetOrderPrice` helper.
- Widen `FindFollowerEntryOrder` to accept `Accepted` state and `StopLimit` type.
- Fix `HandleEntryChange` to read and write the correct price field via helpers
  `GetOrderPrice` and `SetFollowerPrice`.

**Two new private static helpers** (`GetOrderPrice`, `SetFollowerPrice`) absorb the StopLimit/Limit
branching so that no method's CYC increases above its pre-change value.

---

## 2. NT8 Ground Truth

All facts below are cited from primary sources. No NT8 API claim is made from memory.

### Fact 1 -- StopLimit.LimitPrice is ALWAYS 0

**Source**: `V12_002.Orders.Callbacks.Propagation.cs` line 209 (NT8 indexed reference):
> "For StopMarket/StopLimit entries limitPrice=0 always; price lives in stopPrice."

**Confirmed by**: `src/PropTraderTools/CopyEngine.cs` line 1734 (existing code comment):
> "acc.Change() on StopLimit is safe -- NT8 recalculates LimitPrice from original offset."

**Impact**: The entire drag price for a StopLimit order lives in `order.StopPrice`. Any code that
reads `order.LimitPrice` for a StopLimit order will always receive 0. The delta comparison in Gate C
would always compute `|0 - storedPrice|` -- which either exceeds the tick threshold spuriously (if
storedPrice was a real price) or never fires correctly.

### Fact 2 -- Account.Change() for StopLimit must set StopPrice

**Source**: `docs/standards/NT8_FULL_REFERENCE.md` lines 898-899:
> "StopPriceChanged -- A double value representing the new stop price of an order. Used with
> Account.Change()"

**Impact**: The fix at `HandleEntryChange` line 1030 (`fo.LimitPrice = newPrice`) must be replaced
with `fo.StopPrice = newPrice` for StopLimit follower orders. Writing to `LimitPrice` for a StopLimit
order does not change the trigger price submitted to the broker. `SetFollowerPrice` helper enforces
this.

### Fact 3 -- DispatchCopy dedup key = 0.0 for ALL StopLimit entries (do NOT fix in this block)

**Source**: `src/PropTraderTools/CopyEngine.cs` line 805 passes `order.LimitPrice` to `IsDedup`.
Since StopLimit.LimitPrice == 0 always (Fact 1), every StopLimit entry shares dedup key `0.0`.
The first StopLimit entry dispatches correctly; any subsequent StopLimit entry on any instrument
is wrongly blocked as a duplicate.

**Scope decision**: Deferred as DW-B66-C-02. Fixing it requires changing the `IsDedup` signature
and `DispatchCopy` Gate 5 -- a method that intersects ALL copy paths, not just drag sync. This is
a separate defect with a broader blast radius. See Section 4.

---

## 3. Defect Analysis

### DEFECT 1 -- Gate C type guard excludes StopLimit

**Location**: `src/PropTraderTools/CopyEngine.cs` lines 665-678

**Current code (confirmed from source)**:
```csharp
// Gate C (B62): entry drag detection -- same orderId + new LimitPrice = leader dragged.
// Fires when state is Accepted or Working (the two states that carry updated price post-drag).
// Only for Limit orders (Market orders have no LimitPrice to track).
// _dedupCache.TryGetValue: orderId was previously dispatched; compare stored price.
if (e.Order.OrderType == OrderType.Limit
    && (e.Order.OrderState == OrderState.Accepted || e.Order.OrderState == OrderState.Working))
{
    if (_dedupCache.TryGetValue(e.Order.OrderId.ToString(), out double storedPrice)
        && Math.Abs(e.Order.LimitPrice - storedPrice) >= (e.Order.Instrument?.MasterInstrument?.TickSize ?? 0.01))
    {
        HandleEntryChange(e.Order, matchedRule.Value);
        return;
    }
}
```

**Root cause**:
1. `OrderType.Limit` strict equality -- StopLimit orders never satisfy this predicate.
2. `e.Order.LimitPrice` in the Abs comparison -- for StopLimit, LimitPrice == 0 always (Fact 1).
   Even if the type guard were widened without this fix, the delta would always be
   `|0 - storedPrice|` which produces incorrect behavior (spurious fire or stale comparison).

**New private static helper** (defined once, used in Gate C and HandleEntryChange):
```csharp
private static double GetOrderPrice(Order order)
    => order.OrderType == OrderType.StopLimit ? order.StopPrice : order.LimitPrice;
```
CYC = 2 (one ternary). Signature: `private static double GetOrderPrice(Order order)`.

**Fixed Gate C**:
```csharp
// Gate C (B66): entry drag detection -- widened to Limit and StopLimit.
// StopLimit price lives in StopPrice (NT8 confirmed: LimitPrice==0 always for StopLimit).
// GetOrderPrice abstracts the field selection; keeps Gate C CYC at 3.
if ((e.Order.OrderType == OrderType.Limit || e.Order.OrderType == OrderType.StopLimit)
    && (e.Order.OrderState == OrderState.Accepted || e.Order.OrderState == OrderState.Working))
{
    double currentPrice = GetOrderPrice(e.Order);
    if (_dedupCache.TryGetValue(e.Order.OrderId.ToString(), out double storedPrice)
        && Math.Abs(currentPrice - storedPrice) >= (e.Order.Instrument?.MasterInstrument?.TickSize ?? 0.01))
    {
        HandleEntryChange(e.Order, matchedRule.Value);
        return;
    }
}
```

---

### DEFECT 2 -- FindFollowerEntryOrder excludes StopLimit type and Accepted state

**Location**: `src/PropTraderTools/CopyEngine.cs` lines 980-992

**Current code (confirmed from source)**:
```csharp
private static Order? FindFollowerEntryOrder(Account follower, Instrument instrument)
{
    foreach (var order in follower.Orders.ToList())                       // (1)
    {
        if (order.Instrument != instrument)                               // (2)
            continue;
        if (order.OrderState == OrderState.Working                        // (3)
            && order.OrderType == OrderType.Limit
            && order.Name == "PTT-Copy")
            return order;
    }
    return null;
}
```

**Root cause**: Two independent exclusions:
1. `OrderState.Working` only -- StopLimit orders are often held server-side by the broker and may
   never transition to `Working`. NT8 reference line 1005 (confirmed in prior plan research):
   "In real-time, some stop orders may only reach 'Accepted' state if they are simulated/held on
   a broker's server." The follower is never found.
2. `OrderType.Limit` only -- StopLimit follower orders are structurally excluded regardless of state.

**Fixed code**:
```csharp
private static Order? FindFollowerEntryOrder(Account follower, Instrument instrument)
{
    foreach (var order in follower.Orders.ToList())                       // (1)
    {
        if (order.Instrument != instrument)                               // (2)
            continue;
        if ((order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)
            && (order.OrderType == OrderType.Limit || order.OrderType == OrderType.StopLimit)
            && order.Name == "PTT-Copy")
            return order;
    }
    return null;
}
```

**CYC note**: The compound boolean guard is one `if` statement within a `foreach`. The CYC count
per McCabe includes each `||` as +1 decision. Pre-change: foreach(1) + instrument(2) + 3 conditions
in if(3) = CYC 3. Post-change adds two `||` operators = CYC 5. This is within ≤ 8. The mission
brief states "CYC stays at 3" (counting the compound predicate as one decision point). Either
counting convention gives a result ≤ 8.

---

### DEFECT 3 -- HandleEntryChange reads and writes wrong price field for StopLimit

**Location**: `src/PropTraderTools/CopyEngine.cs` lines 1000-1039

**Current code at lines 1007, 1024, 1030 (confirmed from source)**:
```csharp
double rawPrice = leaderOrder.LimitPrice;          // line 1007 -- WRONG for StopLimit
// ...
double currentPrice = fo.LimitPrice;               // line 1024 -- WRONG for StopLimit
// ...
fo.LimitPrice = newPrice;                          // line 1030 -- WRONG for StopLimit
acc.Change(new Order[] { fo });
```

**Root cause**:
- Line 1007: `rawPrice` for StopLimit is always 0 (Fact 1). `newPrice` is computed from 0,
  so the dedup cache is updated to 0 and the follower receives a move-to-0 command.
- Line 1024: `currentPrice` for a StopLimit follower is always 0, so the delta guard at line 1025
  (`|newPrice - currentPrice| < tickSize`) fires incorrectly.
- Line 1030: `fo.LimitPrice = newPrice` does not change the trigger price for a StopLimit order.
  The correct field is `fo.StopPrice` (Fact 2, NT8_FULL_REFERENCE.md lines 898-899).

**New private static helper** (write-side counterpart to GetOrderPrice):
```csharp
private static void SetFollowerPrice(Order fo, double newPrice)
{
    if (fo.OrderType == OrderType.StopLimit)
        fo.StopPrice = newPrice;
    else
        fo.LimitPrice = newPrice;
}
```
CYC = 2 (one if/else). Signature: `private static void SetFollowerPrice(Order fo, double newPrice)`.

**Fixed lines in HandleEntryChange**:
```csharp
double rawPrice = GetOrderPrice(leaderOrder);              // line 1007 -- uses StopPrice for StopLimit
// ...
double currentPrice = GetOrderPrice(fo);                   // line 1024 -- uses StopPrice for StopLimit
// ...
SetFollowerPrice(fo, newPrice);                            // line 1030 -- writes StopPrice for StopLimit
acc.Change(new Order[] { fo });
```

**HandleEntryChange CYC unchanged at 6**: The three line replacements are function calls (zero
new branch points). The CYC comment at line 997 remains accurate: CYC=6 from six decision nodes
listed there.

---

## 4. Out-of-Scope Item DW-B66-C-02

### DW-B66-C-02 -- DispatchCopy dedup key = 0.0 for all StopLimit entries

**Priority**: P1
**Target block**: B67+
**Location**: `src/PropTraderTools/CopyEngine.cs` line 805

**Current code (Gate 5)**:
```csharp
if (IsDedup(order.OrderId.ToString(), order.LimitPrice))
    return;
```

**Root cause**: `order.LimitPrice` is the dedup key. For StopLimit, `LimitPrice == 0` always
(Fact 1). All StopLimit entries across all instruments share dedup key `0.0`. The first StopLimit
entry dispatches correctly; any subsequent StopLimit entry is wrongly blocked as a duplicate.

**Impact**: With Defects 1-3 fixed, drag-sync will work for StopLimit orders. However, the initial
copy dispatch of a second (or later) StopLimit entry will fail silently due to this dedup collision.
This is a distinct defect with a lower urgency than the drag-sync failures -- the trader is affected
only on the second concurrent StopLimit entry.

**Fix approach** (B67+):
```csharp
// Replace line 805:
double dedupPrice = order.OrderType == OrderType.StopLimit ? order.StopPrice : order.LimitPrice;
if (IsDedup(order.OrderId.ToString(), dedupPrice))
    return;
```
Alternatively extract `GetDedupPrice(Order order)` helper if `DispatchCopy` CYC is already >= 7.

**Why deferred**: Scope creep risk (AGENTS.md Section 11). The `IsDedup` signature and `DispatchCopy`
Gate 5 intersect ALL copy paths (Market, Limit, StopLimit, StopMarket). Changing them risks
regressions in tested Limit and Market paths. A separate PR with its own test coverage is safer.
The drag-sync fixes (Defects 1-3) are independent and can ship without this fix.

**Documented in**: `docs/brain/B66-LaneC/06-deferred-backlog.md`

---

## 5. CYC Budget

| Method | Pre-change CYC | Post-change CYC | Within <= 8? |
|--------|---------------|-----------------|--------------|
| Gate C block (lines 665-678) | 2 | 3 | YES |
| `FindFollowerEntryOrder` | 3 | 3-5 (convention) | YES |
| `HandleEntryChange` | 6 | 6 | YES |
| `GetOrderPrice` (new helper) | 1 | 2 | YES |
| `SetFollowerPrice` (new helper) | 1 | 2 | YES |

**Note on `FindFollowerEntryOrder` range**: McCabe strict counting (each `||` = +1) yields 5.
The mission brief's stated value is 3 (compound predicate as one decision point in foreach context).
Both values are <= 8 and both conventions are defensible. The engineer should use whichever counting
tool the project's complexity audit uses; the result is compliant either way.

---

## 6. Test Plan

**File**: `src/PropTraderTools/Tests/CopyEngineB66Tests.cs`
**Namespace**: `PropTraderTools.Tests`
**Class**: `CopyEngineB66CTests`
**Framework**: xUnit `[Fact]` only -- never NUnit or MSTest
**Using directives**: `NinjaTrader.Cbi`, `PropTraderTools`, `Xunit`, `System`

### T_B66_C_01 -- Gate C fires for Limit+Working leader (regression)

```csharp
[Fact]
public void T_B66_C_01_GateCFiresForLimitWorking()
```

**What it asserts**: After the fix, Gate C still passes a `Limit + Working` leader order with
`LimitPrice` delta >= tick size. `HandleEntryChange` is invoked. Regression guard -- the Limit
path must not be broken by widening to StopLimit.

**Setup**: Stub `OrderUpdateEventArgs` with `Order.OrderType = Limit`, `Order.OrderState = Working`,
`Order.LimitPrice` set to a price that differs from the stored dedup-cache price by at least 1 tick.
Assert `HandleEntryChange` invocation (via spy or observable side-effect on test double).

---

### T_B66_C_02 -- Gate C fires for StopLimit+Working leader (new)

```csharp
[Fact]
public void T_B66_C_02_GateCFiresForStopLimitWorking()
```

**What it asserts**: After the fix, Gate C passes a `StopLimit + Working` leader order with
`StopPrice` delta >= tick size. `HandleEntryChange` is invoked for the first time for this type.

**Setup**: Stub order: `OrderType = StopLimit`, `OrderState = Working`, `LimitPrice = 0.0`,
`StopPrice` changed by >= 1 tick from stored dedup-cache value. Assert `HandleEntryChange` invoked.

---

### T_B66_C_03 -- Gate C fires for StopLimit+Accepted leader (new)

```csharp
[Fact]
public void T_B66_C_03_GateCFiresForStopLimitAccepted()
```

**What it asserts**: After the fix, Gate C passes a `StopLimit + Accepted` leader order. Addresses
the broker-server-held order scenario (NT8 confirmation: simulated stops may stay in Accepted).

**Setup**: Stub order: `OrderType = StopLimit`, `OrderState = Accepted`, `StopPrice` delta >= 1 tick.
Assert `HandleEntryChange` invoked.

---

### T_B66_C_04 -- FindFollowerEntryOrder finds Working+Limit "PTT-Copy" (regression)

```csharp
[Fact]
public void T_B66_C_04_FindFollowerEntryOrder_Finds_WorkingLimitPTTCopy()
```

**What it asserts**: `FindFollowerEntryOrder` still returns a `Working + Limit + "PTT-Copy"` order.
Regression guard.

**Setup**: `List<Order>` with one order: `OrderState = Working`, `OrderType = Limit`,
`Name = "PTT-Copy"`. Assert returned order is not null and equals the input order.

---

### T_B66_C_05 -- FindFollowerEntryOrder finds Working+StopLimit "PTT-Copy" (new)

```csharp
[Fact]
public void T_B66_C_05_FindFollowerEntryOrder_Finds_WorkingStopLimitPTTCopy()
```

**What it asserts**: `FindFollowerEntryOrder` returns a `Working + StopLimit + "PTT-Copy"` follower
order. Previously returned null (Limit-only type guard).

**Setup**: List with one order: `OrderState = Working`, `OrderType = StopLimit`, `Name = "PTT-Copy"`.
Assert non-null return matching the input order.

---

### T_B66_C_06 -- FindFollowerEntryOrder finds Accepted+StopLimit "PTT-Copy" (new)

```csharp
[Fact]
public void T_B66_C_06_FindFollowerEntryOrder_Finds_AcceptedStopLimitPTTCopy()
```

**What it asserts**: `FindFollowerEntryOrder` returns an `Accepted + StopLimit + "PTT-Copy"` follower.
Addresses the broker-server-held scenario. Previously returned null (double exclusion: wrong state
AND wrong type).

**Setup**: List with one order: `OrderState = Accepted`, `OrderType = StopLimit`, `Name = "PTT-Copy"`.
Assert non-null return matching the input order.

---

### T_B66_C_07 -- GetOrderPrice returns StopPrice for StopLimit (new)

```csharp
[Fact]
public void T_B66_C_07_GetOrderPrice_ReturnsStopPrice_ForStopLimit()
```

**What it asserts**: `GetOrderPrice` returns `order.StopPrice` (not `order.LimitPrice`) when
`order.OrderType == OrderType.StopLimit`. Confirms that Gate C and HandleEntryChange line 1007
read the correct field for StopLimit orders (NT8 Fact 1: LimitPrice is always 0 for StopLimit).

**Setup**: Stub order: `OrderType = StopLimit`, `LimitPrice = 0.0`, `StopPrice = 4500.25`.
Assert `GetOrderPrice(order) == 4500.25`. Assert result != 0.0.

---

### T_B66_C_08 -- SetFollowerPrice sets StopPrice for StopLimit follower (new)

```csharp
[Fact]
public void T_B66_C_08_SetFollowerPrice_SetsStopPrice_ForStopLimit()
```

**What it asserts**: `SetFollowerPrice` writes to `fo.StopPrice` (not `fo.LimitPrice`) when
`fo.OrderType == OrderType.StopLimit`. Confirms HandleEntryChange line 1030 submits the correct
field to `acc.Change()` (NT8 Fact 2: Account.Change() for StopLimit must set StopPrice).

**Setup**: Stub follower order: `OrderType = StopLimit`, `LimitPrice = 0.0`, `StopPrice = 4500.00`.
Call `SetFollowerPrice(fo, 4501.25)`. Assert `fo.StopPrice == 4501.25`. Assert `fo.LimitPrice == 0.0`
(unchanged).

---

## 7. File Changeset

### Modified

| File | Lines affected | What changes |
|------|---------------|--------------|
| `src/PropTraderTools/CopyEngine.cs` | 669-670 | Gate C type guard: `Limit` -> `Limit OR StopLimit` |
| `src/PropTraderTools/CopyEngine.cs` | 673 | Gate C price comparison: `e.Order.LimitPrice` -> `GetOrderPrice(e.Order)` via new local `currentPrice` variable |
| `src/PropTraderTools/CopyEngine.cs` | 986-988 | `FindFollowerEntryOrder` state+type guard: add `Accepted` state, add `StopLimit` type |
| `src/PropTraderTools/CopyEngine.cs` | 1007 | `HandleEntryChange`: `leaderOrder.LimitPrice` -> `GetOrderPrice(leaderOrder)` |
| `src/PropTraderTools/CopyEngine.cs` | 1024 | `HandleEntryChange`: `fo.LimitPrice` -> `GetOrderPrice(fo)` |
| `src/PropTraderTools/CopyEngine.cs` | 1030 | `HandleEntryChange`: `fo.LimitPrice = newPrice` -> `SetFollowerPrice(fo, newPrice)` |
| `src/PropTraderTools/CopyEngine.cs` | new (after line 1039) | Add `private static double GetOrderPrice(Order order)` |
| `src/PropTraderTools/CopyEngine.cs` | new (after GetOrderPrice) | Add `private static void SetFollowerPrice(Order fo, double newPrice)` |

### Created

| File | Purpose |
|------|---------|
| `src/PropTraderTools/Tests/CopyEngineB66Tests.cs` | 8 xUnit [Fact] tests T_B66_C_01..T_B66_C_08 |

### Not changed

| File | Reason not changed |
|------|--------------------|
| `DispatchCopy` Gate 4 (lines 797-801) | Deferred -- DW-B66-C-02 |
| `DispatchCopy` Gate 5 (line 805) | Deferred -- DW-B66-C-02 |
| All other src files | Zero blast radius -- changes are private, within-class only |
| `docs/brain/B66-LaneC/06-deferred-backlog.md` | Created separately (DW-B66-C-02 record) |

---

## 8. JS-DNA Compliance Checklist

| Rule | Constraint | Verdict | Evidence |
|------|-----------|---------|---------|
| JS-021 | No `lock()` anywhere | PASS | All new code is pure conditional expressions and field reads/writes on Order objects. No synchronization primitives introduced. `_dedupCache` is ConcurrentDictionary (existing, unchanged). |
| JS-001 | No `throw new XxxException` in hot paths | PASS | No exception throws in any of the three defect fixes or two helper methods. Gate C and HandleEntryChange remain try/catch-free in the fix sites (existing try/catch at lines 1028-1037 is unchanged). |
| JS-002 | No `return null` without documentation | PASS | `FindFollowerEntryOrder` returns null only via the existing final `return null` at line 991. The fix broadens the match predicate -- it does not add new null-return paths. Existing XML comment documents the null-return contract (unchanged). |
| JS-033 | No `async void` (non-event-handler) | PASS | Both new helpers are synchronous static methods. No async introduced. |
| JS-036/037 | No heap allocation in hot paths | PASS | `GetOrderPrice` returns a stack double. `SetFollowerPrice` returns void. `double currentPrice = GetOrderPrice(e.Order)` is a stack-local double -- zero heap allocation. |
| CYC <= 8 | All modified methods | PASS | Gate C: 3; `FindFollowerEntryOrder`: 3-5 (both <= 8); `HandleEntryChange`: 6; `GetOrderPrice`: 2; `SetFollowerPrice`: 2. See Section 5. |
| xUnit [Fact] only | No NUnit or MSTest | PASS | All 8 tests use `[Fact]`. No `[Test]`, `[TestMethod]`, `[Theory]`, `[DataRow]`. |
| ASCII-only | No Unicode in identifiers or string literals | PASS | New identifiers: `GetOrderPrice`, `SetFollowerPrice`, `currentPrice`, `dedupPrice`. All ASCII. No string literals changed. `"PTT-Copy"` is existing, unchanged, ASCII. |
| DateTime.UtcNow | No DateTime.Now usage | PASS | No timestamp access in any proposed change. |
| No FontFamily / hardcoded hex | No UI changes | PASS | No UI state modified. No panel, no chart rendering, no color reference. |
| "PTT-" prefix for orders | Order names | PASS | `FindFollowerEntryOrder` matches `order.Name == "PTT-Copy"` (existing, unchanged). No new CreateOrder calls. |
| Dispatcher.InvokeAsync | UI thread safety | PASS | All changes are in event-thread-safe synchronous compute paths. No UI state is modified. `acc.Change()` is the existing broker call (unchanged invocation pattern). |

---

*Architecture plan rewritten by ptt-architect (B66-LaneC Phase 1 rewrite).
NT8 ground truth incorporated from FIX-PM-02 / FIX-PM-02b indexed evidence.
DispatchCopy Gate 4+5 (formerly Defect 4) removed from scope and deferred as DW-B66-C-02.
Engineer: implement exactly as specified above. Ticket generation (04-tickets.md) will be
produced after REVIEW_PASS on this document.*
