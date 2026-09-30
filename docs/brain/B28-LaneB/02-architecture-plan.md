# B28 Lane B — Architecture Plan
## Defect: DW-B28-02 | Leader-Account Overloads
**Status:** REVIEW_PENDING
**Author:** ptt-architect
**Block:** B28 | Lane: B | Phase: 2

---

## 1. Defect Summary and Root Cause

**DW-B28-02** (P1 HIGH): The Trim, Flatten, and Cancel buttons in TradeCopierPanel are
silent no-ops when the user has not previously clicked "Apply Rule".

**Root cause chain:**

```
OnTrimClick / OnFlattenClick / OnCancel2
  -> CopyEngine.Trim(Instrument) / Flatten(Instrument) / CancelPendingEntries(Instrument)
  -> AllAccounts(instrument)
  -> FindRule(instrument)              -- searches _rules ConcurrentBag
  -> _rules is EMPTY (Apply Rule never clicked)
  -> yield break  -> 0 accounts yielded
  -> foreach body never executes
  -> no CreateOrder / no Cancel issued
  -> button appears to work, does nothing
```

The `AllAccounts(Instrument)` path is rule-dependent by design: it fans out to follower
accounts defined in an active copy rule. Without a rule, there are no accounts to iterate.

**Key observation:** `_leaderAccount` is already stored at
[`TradeCopierPanel.cs:L120`](../../../universal-or-strategy/src/PropTraderTools/TradeCopierPanel.cs)
and is populated from `ChartTrader.Account` by `TradeCopierAddOn`. The same field is used
successfully by `BreakEven(_leaderAccount, _instrument, _beBuffer)` at
[`TradeCopierPanel.cs:L777`](../../../universal-or-strategy/src/PropTraderTools/TradeCopierPanel.cs)
since B24. The button handlers simply never forwarded it.

---

## 2. Architecture Decision — Option A: Leader-Account Overloads

**Decision: LOCKED (Director approved).**

Mirror the `BreakEven(Account leader, Instrument instrument, int bufferTicks)` pattern
confirmed at [`CopyEngine.cs:L1217`](../../../universal-or-strategy/src/PropTraderTools/CopyEngine.cs).

**Option A rationale:**
- Zero new fields, zero new state, zero new imports.
- Leader account bypasses `_rules` entirely — fires on the live chart account directly.
- `AllAccounts` fan-out is preserved for future multi-follower use once rules are applied.
- Null leader guard matches existing BreakEven pattern: `StatusUpdate + return` (not throw).
- 5 private helpers enforce single-responsibility and keep parent CYC <= 4.
- 5 new internal overloads produce a clean, symmetric API surface.

**Pattern template (from verified source):**

```csharp
// BreakEven(Account, Instrument, int) at CopyEngine.cs L1217
internal void BreakEven(Account leader, Instrument instrument, int bufferTicks)
{
    if (leader == null)
    {
        StatusUpdate?.Invoke("PTT-BE: leader null -- BE skipped");
        return;
    }
    MoveStopToBreakEven(leader, instrument, bufferTicks);   // leader direct
    foreach (var acc in AllAccounts(instrument))             // follower fan-out
    {
        if (acc == leader) continue;                         // dedup skip
        MoveStopToBreakEven(acc, instrument, bufferTicks);
    }
}
```

All 5 new overloads follow this exact structure, substituting the appropriate helper call.

---

## 3. Files Affected

| File | Change Type | Lines Changed (approx.) |
|------|-------------|------------------------|
| `src/PropTraderTools/CopyEngine.cs` | +10 methods (5 private helpers + 5 internal overloads) | ~130 new lines |
| `src/PropTraderTools/TradeCopierPanel.cs` | 3 call sites updated (5 lines) | 5 lines modified |
| `src/PropTraderTools/CopyEngineTests.cs` | +3 [Fact] reflection tests | ~45 new lines |

No other files are affected. No new .cs files. No .csproj changes. No namespace additions.

---

## 4. Implementation Steps

### STEP 1 — CopyEngine.cs: Extract 5 Private Helpers

Extract the per-account body of each existing loop into a private helper.
The parent method's `foreach` body is replaced with a single helper call.
This reduces parent CYC and makes the leader overloads in STEP 2 DRY.

#### Helper 1: `TrimOneAccount`

Extracted from [`CopyEngine.cs:L857-888`](../../../universal-or-strategy/src/PropTraderTools/CopyEngine.cs)
(the body of `Trim(Instrument)` loop).

```csharp
// B28 T1 -- TrimOneAccount: per-account market trim helper. CYC=3.
// (1) pos null||qty guard, (2) MarketPosition ternary, (3) try/catch CreateOrder.
// JS-001: no rethrow. JS-021: no lock. ASCII: PTT-Trim signal name.
private void TrimOneAccount(Account acc, Instrument instrument)
{
    var pos = FindPosition(acc, instrument);
    if (pos == null || pos.Quantity == 0)                                    // (1)
    {
        StatusUpdate?.Invoke(acc.Name + ": flat skip");
        return;
    }
    int trimQty = (int)Math.Ceiling(pos.Quantity / 2.0);
    var action = pos.MarketPosition == MarketPosition.Long                   // (2)
        ? OrderAction.Sell : OrderAction.BuyToCover;
    try                                                                       // (3)
    {
        acc.CreateOrder(
            instrument, action, OrderType.Market, OrderEntry.Manual,
            TimeInForce.Day, trimQty, 0, 0, null, "PTT-Trim",
            DateTime.MaxValue, null);
        StatusUpdate?.Invoke(acc.Name + ": trim " + trimQty);
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke("PTT-Trim error: " + ex.Message);
    }
}
```

**CYC = 3.** ✅

#### Helper 2: `FlattenOneAccount`

Extracted from [`CopyEngine.cs:L895-927`](../../../universal-or-strategy/src/PropTraderTools/CopyEngine.cs)
(the body of `Flatten(Instrument)` loop).

```csharp
// B28 T1 -- FlattenOneAccount: per-account market flatten helper. CYC=3.
// (1) pos null||qty guard, (2) action ternary, (3) try/catch CreateOrder.
private void FlattenOneAccount(Account acc, Instrument instrument)
{
    var pos = FindPosition(acc, instrument);
    if (pos == null || pos.Quantity == 0)                                    // (1)
    {
        StatusUpdate?.Invoke(acc.Name + ": flat skip");
        return;
    }
    var action = pos.MarketPosition == MarketPosition.Long                   // (2)
        ? OrderAction.Sell : OrderAction.BuyToCover;
    try                                                                       // (3)
    {
        acc.CreateOrder(
            instrument, action, OrderType.Market, OrderEntry.Manual,
            TimeInForce.Day, pos.Quantity, 0, 0, null, "PTT-Flatten",
            DateTime.MaxValue, null);
        StatusUpdate?.Invoke(acc.Name + ": flatten " + pos.Quantity);
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke("PTT-Flatten error: " + ex.Message);
    }
}
```

**CYC = 3.** ✅

#### Helper 3: `CancelOneAccount`

Extracted from [`CopyEngine.cs:L1024-1048`](../../../universal-or-strategy/src/PropTraderTools/CopyEngine.cs)
(the body of `CancelPendingEntries(Instrument)` loop).

```csharp
// B28 T1 -- CancelOneAccount: per-account pending cancel helper. CYC=4.
// (1) foreach orders, (2) instrument filter, (3) OrderState guard, (4) IsBracketLeg guard.
// Preserves B18 T3 fix: also cancels Initialized orders (DW-B18-CANCEL-01).
private void CancelOneAccount(Account acc, Instrument instrument)
{
    foreach (var order in acc.Orders)                                        // (1)
    {
        if (order.Instrument != instrument) continue;                        // (2)
        if (order.OrderState != OrderState.Working &&
            order.OrderState != OrderState.Initialized) continue;           // (3)
        if (IsBracketLeg(order)) continue;                                  // (4)
        try
        {
            acc.Cancel(new Order[] { order });
            StatusUpdate?.Invoke(acc.Name + ": entry pulled " + order.OrderId);
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke("PTT-Cancel error: " + ex.Message);
        }
    }
}
```

**CYC = 4.** ✅

#### Helper 4: `TrimOneAccountLimit`

Extracted from [`CopyEngine.cs:L952-977`](../../../universal-or-strategy/src/PropTraderTools/CopyEngine.cs)
(the body of `Trim(Instrument, int, double, double)` loop, after the guard branching in the parent).

```csharp
// B28 T1 -- TrimOneAccountLimit: per-account limit trim helper. CYC=3.
// (1) pos null||qty guard, (2) isLong ternary, (3) try/catch CreateOrder.
// NT8-007: arg12 = (NinjaTrader.Cbi.CustomOrder)null. NT8-014: PTT-TrimLimit signal.
private void TrimOneAccountLimit(Account acc, Instrument instrument,
    int exitBuffer, double ask, double bid)
{
    var pos = FindPosition(acc, instrument);
    if (pos == null || pos.Quantity == 0)                                    // (1)
    {
        StatusUpdate?.Invoke(acc.Name + ": flat skip");
        return;
    }
    int trimQty = (int)Math.Ceiling(pos.Quantity / 2.0);
    bool isLong = pos.MarketPosition == MarketPosition.Long;                 // (2)
    var action  = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
    double tickSize = instrument.MasterInstrument.TickSize;
    double limitPx  = ComputeLimitPx(isLong, ask, bid, exitBuffer, tickSize);
    try                                                                       // (3)
    {
        acc.CreateOrder(
            instrument, action, OrderType.Limit, OrderEntry.Manual,
            TimeInForce.Day, trimQty, limitPx, 0, null, "PTT-TrimLimit",
            DateTime.MaxValue, (NinjaTrader.Cbi.CustomOrder)null);
        StatusUpdate?.Invoke(acc.Name + ": trim-limit " + trimQty + " @ " + limitPx);
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke("PTT-TrimLimit error: " + ex.Message);
    }
}
```

**CYC = 3.** ✅

#### Helper 5: `FlattenOneAccountLimit`

Extracted from [`CopyEngine.cs:L992-1017`](../../../universal-or-strategy/src/PropTraderTools/CopyEngine.cs)
(the body of `Flatten(Instrument, int, double, double)` loop).

```csharp
// B28 T1 -- FlattenOneAccountLimit: per-account limit flatten helper. CYC=3.
// (1) pos null||qty guard, (2) isLong ternary, (3) try/catch CreateOrder.
// NT8-007: arg12 = (NinjaTrader.Cbi.CustomOrder)null. NT8-014: PTT-FlattenLimit signal.
private void FlattenOneAccountLimit(Account acc, Instrument instrument,
    int exitBuffer, double ask, double bid)
{
    var pos = FindPosition(acc, instrument);
    if (pos == null || pos.Quantity == 0)                                    // (1)
    {
        StatusUpdate?.Invoke(acc.Name + ": flat skip");
        return;
    }
    bool isLong = pos.MarketPosition == MarketPosition.Long;                 // (2)
    var action  = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
    double tickSize = instrument.MasterInstrument.TickSize;
    double limitPx  = ComputeLimitPx(isLong, ask, bid, exitBuffer, tickSize);
    try                                                                       // (3)
    {
        acc.CreateOrder(
            instrument, action, OrderType.Limit, OrderEntry.Manual,
            TimeInForce.Day, pos.Quantity, limitPx, 0, null, "PTT-FlattenLimit",
            DateTime.MaxValue, (NinjaTrader.Cbi.CustomOrder)null);
        StatusUpdate?.Invoke(acc.Name + ": flatten-limit " + pos.Quantity + " @ " + limitPx);
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke("PTT-FlattenLimit error: " + ex.Message);
    }
}
```

**CYC = 3.** ✅

**Parent methods after extraction:**

After STEP 1, each parent method's `foreach` body is replaced with a single helper call,
reducing parent CYC:

| Parent Method | Old CYC | New CYC (after extraction) |
|--------------|---------|---------------------------|
| `Trim(Instrument)` | ~5 | 2 (foreach + helper call) |
| `Flatten(Instrument)` | ~5 | 2 |
| `CancelPendingEntries(Instrument)` | ~5 | 2 |
| `Trim(Instrument, int, double, double)` | 6 | 4 (ask/bid guard + exitBuffer guard + foreach + helper) |
| `Flatten(Instrument, int, double, double)` | 6 | 4 |

---

### STEP 2 — CopyEngine.cs: Add 5 Leader-Account Overloads

Insert after `Flatten(Instrument)` at approximately L928.
All overloads mirror [`BreakEven(Account, Instrument, int)` at CopyEngine.cs:L1217](../../../universal-or-strategy/src/PropTraderTools/CopyEngine.cs).

#### Overload 1: `Trim(Account leader, Instrument instrument)`

```csharp
// B28 T1 -- Trim(Account,Instrument): fires leader directly, no rule needed. CYC=4.
// (1) null guard + StatusUpdate + return, (2) TrimOneAccount leader,
// (3) foreach AllAccounts, (4) acc==leader skip.
// JS-021: no lock. JS-002: null => StatusUpdate+return, not throw.
internal void Trim(Account leader, Instrument instrument)
{
    if (leader == null)                                                      // (1)
    {
        StatusUpdate?.Invoke("PTT-Trim: leader null -- skipping");
        return;
    }
    TrimOneAccount(leader, instrument);                                      // leader direct
    foreach (var acc in AllAccounts(instrument))                             // (3) fan-out
    {
        if (acc == leader) continue;                                         // (4) dedup
        TrimOneAccount(acc, instrument);
    }
}
```

**CYC = 4.** ✅

#### Overload 2: `Flatten(Account leader, Instrument instrument)`

```csharp
// B28 T1 -- Flatten(Account,Instrument): fires leader directly, no rule needed. CYC=4.
internal void Flatten(Account leader, Instrument instrument)
{
    if (leader == null)                                                      // (1)
    {
        StatusUpdate?.Invoke("PTT-Flatten: leader null -- skipping");
        return;
    }
    FlattenOneAccount(leader, instrument);
    foreach (var acc in AllAccounts(instrument))                             // (3)
    {
        if (acc == leader) continue;                                         // (4)
        FlattenOneAccount(acc, instrument);
    }
}
```

**CYC = 4.** ✅

#### Overload 3: `CancelPendingEntries(Account leader, Instrument instrument)`

```csharp
// B28 T1 -- CancelPendingEntries(Account,Instrument): fires leader directly. CYC=4.
internal void CancelPendingEntries(Account leader, Instrument instrument)
{
    if (leader == null)                                                      // (1)
    {
        StatusUpdate?.Invoke("PTT-Cancel: leader null -- skipping");
        return;
    }
    CancelOneAccount(leader, instrument);
    foreach (var acc in AllAccounts(instrument))                             // (3)
    {
        if (acc == leader) continue;                                         // (4)
        CancelOneAccount(acc, instrument);
    }
}
```

**CYC = 4.** ✅

#### Overload 4: `Trim(Account leader, Instrument instrument, int exitBuffer, double ask, double bid)`

```csharp
// B28 T1 -- Trim(Account,Instrument,int,double,double): limit trim with leader. CYC=5.
// (1) leader null guard, (2) ask/bid/buffer guard -> fallback to 2-arg overload,
// (3) TrimOneAccountLimit leader, (4) foreach AllAccounts, (5) acc==leader skip.
internal void Trim(Account leader, Instrument instrument,
    int exitBuffer, double ask, double bid)
{
    if (leader == null)                                                      // (1)
    {
        StatusUpdate?.Invoke("PTT-TrimLimit: leader null -- skipping");
        return;
    }
    if (ask <= 0 || bid <= 0 || exitBuffer == 0)                            // (2)
    {
        Trim(leader, instrument);
        return;
    }
    TrimOneAccountLimit(leader, instrument, exitBuffer, ask, bid);
    foreach (var acc in AllAccounts(instrument))                             // (4)
    {
        if (acc == leader) continue;                                         // (5)
        TrimOneAccountLimit(acc, instrument, exitBuffer, ask, bid);
    }
}
```

**CYC = 5.** ✅

#### Overload 5: `Flatten(Account leader, Instrument instrument, int exitBuffer, double ask, double bid)`

```csharp
// B28 T1 -- Flatten(Account,Instrument,int,double,double): limit flatten with leader. CYC=5.
internal void Flatten(Account leader, Instrument instrument,
    int exitBuffer, double ask, double bid)
{
    if (leader == null)                                                      // (1)
    {
        StatusUpdate?.Invoke("PTT-FlattenLimit: leader null -- skipping");
        return;
    }
    if (ask <= 0 || bid <= 0 || exitBuffer == 0)                            // (2)
    {
        Flatten(leader, instrument);
        return;
    }
    FlattenOneAccountLimit(leader, instrument, exitBuffer, ask, bid);
    foreach (var acc in AllAccounts(instrument))                             // (4)
    {
        if (acc == leader) continue;                                         // (5)
        FlattenOneAccountLimit(acc, instrument, exitBuffer, ask, bid);
    }
}
```

**CYC = 5.** ✅

---

### STEP 3 — TradeCopierPanel.cs: Update 3 Call Sites

All changes pass `_leaderAccount` (field at `TradeCopierPanel.cs:L120`) to the new overloads.
No new fields, no new imports, no UI structure changes.

#### OnTrimClick (L739-742)

```csharp
// BEFORE:
if (ask <= 0 || bid <= 0 || _trimBuffer == 0)
    _engine.Trim(_instrument);
else
    _engine.Trim(_instrument, _trimBuffer, ask, bid);

// AFTER:
if (ask <= 0 || bid <= 0 || _trimBuffer == 0)
    _engine.Trim(_leaderAccount, _instrument);
else
    _engine.Trim(_leaderAccount, _instrument, _trimBuffer, ask, bid);
```

#### OnFlattenClick (L765-768)

```csharp
// BEFORE:
if (ask <= 0 || bid <= 0 || _flattenBuffer == 0)
    _engine.Flatten(_instrument);
else
    _engine.Flatten(_instrument, _flattenBuffer, ask, bid);

// AFTER:
if (ask <= 0 || bid <= 0 || _flattenBuffer == 0)
    _engine.Flatten(_leaderAccount, _instrument);
else
    _engine.Flatten(_leaderAccount, _instrument, _flattenBuffer, ask, bid);
```

#### OnCancel2 (L912)

```csharp
// BEFORE:
if (_instrument != null) _engine.CancelPendingEntries(_instrument);

// AFTER:
if (_instrument != null) _engine.CancelPendingEntries(_leaderAccount, _instrument);
```

---

## 5. CYC Analysis Summary

| Method | Location | CYC | Branches |
|--------|----------|-----|---------|
| `TrimOneAccount` | CopyEngine.cs (new private) | **3** | pos guard, action ternary, try/catch |
| `FlattenOneAccount` | CopyEngine.cs (new private) | **3** | pos guard, action ternary, try/catch |
| `CancelOneAccount` | CopyEngine.cs (new private) | **4** | foreach orders, instr filter, state guard, bracket guard |
| `TrimOneAccountLimit` | CopyEngine.cs (new private) | **3** | pos guard, isLong ternary, try/catch |
| `FlattenOneAccountLimit` | CopyEngine.cs (new private) | **3** | pos guard, isLong ternary, try/catch |
| `Trim(Account,Instrument)` | CopyEngine.cs (new internal) | **4** | null guard, leader call, foreach, leader skip |
| `Flatten(Account,Instrument)` | CopyEngine.cs (new internal) | **4** | null guard, leader call, foreach, leader skip |
| `CancelPendingEntries(Account,Instrument)` | CopyEngine.cs (new internal) | **4** | null guard, leader call, foreach, leader skip |
| `Trim(Account,Instrument,int,double,double)` | CopyEngine.cs (new internal) | **5** | null guard, ask/bid guard, leader call, foreach, leader skip |
| `Flatten(Account,Instrument,int,double,double)` | CopyEngine.cs (new internal) | **5** | null guard, ask/bid guard, leader call, foreach, leader skip |

**All 10 new methods: CYC <= 8.** ✅

**Parent methods after extraction:**

| Method | Old CYC | New CYC |
|--------|---------|---------|
| `Trim(Instrument)` | 5 | **2** |
| `Flatten(Instrument)` | 5 | **2** |
| `CancelPendingEntries(Instrument)` | 5 | **2** |
| `Trim(Instrument,int,double,double)` | 6 | **4** |
| `Flatten(Instrument,int,double,double)` | 6 | **4** |

---

## 6. JS / NT8 Constraint Compliance

| Rule | Requirement | Status | Notes |
|------|-------------|--------|-------|
| JS-021 | No `lock()` anywhere | ✅ PASS | All new code uses foreach + direct calls. AllAccounts iterates ConcurrentBag (lock-free). |
| JS-001 | No throw in hot paths | ✅ PASS | All CreateOrder / Cancel calls wrapped in try/catch with StatusUpdate. No rethrow. |
| JS-002 | No `return null` | ✅ PASS | All new methods are `void`. Null leader path uses `StatusUpdate + return`. |
| JS-033 | No `async void` | ✅ PASS | All methods synchronous `void`. |
| JS-015 | No unvalidated strings | ✅ PASS | All string literals are ASCII PTT-prefixed signal names (constants). No string params. |
| NT8-001 | No `{ get; init; }` | ✅ PASS | All new symbols are methods, not properties. |
| NT8-002 | No `abstract/sealed record` | ✅ PASS | No records added. |
| NT8-003 | No `volatile double` | ✅ PASS | No volatile fields added. |
| NT8-004 | No `ImmutableDictionary` | ✅ PASS | No immutable collections added. |
| NT8-007 | Limit order arg12 = `(NinjaTrader.Cbi.CustomOrder)null` | ✅ PASS | Preserved in `TrimOneAccountLimit` and `FlattenOneAccountLimit`. |
| NT8-014 | Signal name = "PTT-..." prefix | ✅ PASS | PTT-Trim, PTT-Flatten, PTT-TrimLimit, PTT-FlattenLimit, PTT-Cancel all maintained. |
| ASCII | ASCII-only identifiers and string literals | ✅ PASS | All new identifiers are ASCII. No Unicode in string literals. |
| DateTime | No `DateTime.Now` | ✅ PASS | `DateTime.MaxValue` used as NT8 order expiry sentinel (unchanged). |

---

## 7. Seven-Scan Checklist

These 7 scans MUST all pass before ptt-verifier marks the ticket complete.
The engineer runs these after implementing all 3 steps.

```
SCAN-01: grep -n "lock(" src/PropTraderTools/CopyEngine.cs
         Expected: 0 results

SCAN-02: grep -n "async void " src/PropTraderTools/CopyEngine.cs
         Expected: 0 results

SCAN-03: grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs
         Expected: 0 results

SCAN-04: grep -n "_engine.Trim(_instrument)" src/PropTraderTools/TradeCopierPanel.cs
         Expected: 0 results (all call sites now pass _leaderAccount)

SCAN-05: grep -n "_engine.Flatten(_instrument)" src/PropTraderTools/TradeCopierPanel.cs
         Expected: 0 results (all call sites now pass _leaderAccount)

SCAN-06: grep -n "CancelPendingEntries(_instrument)" src/PropTraderTools/TradeCopierPanel.cs
         Expected: 0 results (call site now passes _leaderAccount)

SCAN-07: grep -c "\[Fact\]" src/PropTraderTools/CopyEngineTests.cs
         Expected: 138  (baseline 135 + 3 new tests)
```

---

## 8. [Fact] Test Specifications

Add to [`CopyEngineTests.cs`](../../../universal-or-strategy/src/PropTraderTools/CopyEngineTests.cs)
using reflection. No live NT8 session required — pure reflection, runs in xUnit isolation.

### T_B28_01 — Trim leader-account overload exists

```csharp
[Fact]
public void T_B28_01_Trim_LeaderOverload_Exists()
{
    // Verify CopyEngine.Trim(Account, Instrument) overload was added by B28.
    // Uses reflection to avoid requiring a live NT8 session.
    var methods = typeof(CopyEngine).GetMethods(
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var overload = methods.FirstOrDefault(m =>
        m.Name == "Trim" &&
        m.GetParameters().Length == 2 &&
        m.GetParameters()[0].ParameterType == typeof(NinjaTrader.Cbi.Account) &&
        m.GetParameters()[1].ParameterType == typeof(NinjaTrader.NinjaScript.Instruments.Instrument));
    Assert.NotNull(overload);
}
```

### T_B28_02 — Flatten leader-account overload exists

```csharp
[Fact]
public void T_B28_02_Flatten_LeaderOverload_Exists()
{
    var methods = typeof(CopyEngine).GetMethods(
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var overload = methods.FirstOrDefault(m =>
        m.Name == "Flatten" &&
        m.GetParameters().Length == 2 &&
        m.GetParameters()[0].ParameterType == typeof(NinjaTrader.Cbi.Account) &&
        m.GetParameters()[1].ParameterType == typeof(NinjaTrader.NinjaScript.Instruments.Instrument));
    Assert.NotNull(overload);
}
```

### T_B28_03 — CancelPendingEntries leader-account overload exists

```csharp
[Fact]
public void T_B28_03_CancelPendingEntries_LeaderOverload_Exists()
{
    var methods = typeof(CopyEngine).GetMethods(
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var overload = methods.FirstOrDefault(m =>
        m.Name == "CancelPendingEntries" &&
        m.GetParameters().Length == 2 &&
        m.GetParameters()[0].ParameterType == typeof(NinjaTrader.Cbi.Account) &&
        m.GetParameters()[1].ParameterType == typeof(NinjaTrader.NinjaScript.Instruments.Instrument));
    Assert.NotNull(overload);
}
```

**Note on BindingFlags:** The new overloads are `internal` on CopyEngine.
Reflection with `BindingFlags.NonPublic | BindingFlags.Instance` returns both `private` and
`internal` methods. Tests T_B28_01/02/03 will find `internal` overloads correctly.

---

## 9. Success Criteria

| Criterion | Verification |
|-----------|-------------|
| Trim button executes market/limit order on leader account | Manual test: press Trim with no active rule |
| Flatten button executes market/limit order on leader account | Manual test: press Flatten with no active rule |
| Cancel button cancels working/initialized orders on leader account | Manual test: press Cancel with pending entry |
| null _leaderAccount produces StatusUpdate, not silent no-op or exception | Verify StatusUpdate message in panel status line |
| SCAN-01..06: zero results for all grep patterns | Run 7-scan checklist post-implementation |
| SCAN-07: [Fact] count == 138 | `grep -c "\[Fact\]" CopyEngineTests.cs` |
| T_B28_01, T_B28_02, T_B28_03 all pass | `dotnet test` green |
| NT8 F5 compilation green | F5 in NinjaTrader with no errors |
| No new CYC > 8 in CopyEngine.cs | `python scripts/complexity_audit.py` |
| No lock() introduced | SCAN-01, SCAN-03 |

---

## 10. Threading Model

All new methods are **synchronous void** called from NT8 UI event handlers.

- `OnTrimClick`, `OnFlattenClick`, `OnCancel2` are NT8 WPF routed event handlers — they
  execute on the NT8 UI/dispatch thread.
- `CopyEngine.Trim/Flatten/CancelPendingEntries` are synchronous — no `Task`, no `async`,
  no background threads.
- `AllAccounts(Instrument)` iterates `_rules` (a `ConcurrentBag`) — lock-free, snapshot-safe
  for `foreach`.
- `acc.CreateOrder` and `acc.Cancel` are NT8 API calls safe to invoke from the dispatch thread.
- No `Dispatcher.InvokeAsync` needed inside `CopyEngine.cs`. The Panel handlers already run
  on the correct thread (confirmed by existing `BreakEven(_leaderAccount, ...)` call at L777
  which uses the identical pattern without additional dispatch).

---

*Plan written by ptt-architect. Awaiting ptt-plan-reviewer.*
