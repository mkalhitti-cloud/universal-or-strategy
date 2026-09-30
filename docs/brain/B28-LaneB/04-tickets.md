# B28 Lane B — Tickets
## Defect: DW-B28-02 | Leader-Account Overloads
**Block:** B28 | Lane: B | Phase: 4 (Ticket Generation)
**Status:** TICKETS_COMPLETE
**Author:** ptt-architect
**Plan source:** `docs/brain/B28-LaneB/02-architecture-plan.md` (REVIEW_PASS)

---

# TICKET 1 — DW-B28-02: Leader-Account Overloads (Complete Fix)

## 1. Spec Requirement IDs

| Requirement | Description |
|-------------|-------------|
| **DW-B28-02** | Trim / Flatten / Cancel buttons are silent no-ops when `_rules` is empty (user never clicked "Apply Rule"). Buttons must operate on the leader account directly. |
| **Spec §Trim** | Trim button must halve open position on the leader account regardless of copy-rule state. |
| **Spec §Flatten** | Flatten button must close full position on the leader account regardless of copy-rule state. |
| **Spec §Cancel** | Cancel (OnCancel2) must cancel Working and Initialized pending entries on the leader account regardless of copy-rule state. |

**Root cause chain (for engineer context):**

```
OnTrimClick / OnFlattenClick / OnCancel2
  -> CopyEngine.Trim(Instrument) / Flatten(Instrument) / CancelPendingEntries(Instrument)
  -> AllAccounts(instrument)
  -> FindRule(instrument)        -- searches _rules ConcurrentBag
  -> _rules is EMPTY             -- Apply Rule never clicked
  -> yield break -> 0 accounts  -> foreach body never executes
  -> no CreateOrder / Cancel issued -> silent no-op
```

**Fix:** Add 5 private helpers + 5 internal leader-account overloads to `CopyEngine.cs`,
update 3 call sites in `TradeCopierPanel.cs`. Pattern mirrors `BreakEven(Account, Instrument, int)`
at [`CopyEngine.cs:L1217`](../../../universal-or-strategy/src/PropTraderTools/CopyEngine.cs).

---

## 2. Files to Modify

| File | Workspace Path | Change |
|------|---------------|--------|
| `CopyEngine.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` | +10 methods (~130 lines) |
| `TradeCopierPanel.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` | 3 call sites (5 lines) |
| `CopyEngineTests.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` | +3 `[Fact]` tests (~45 lines) |

**No other files.** No new `.cs` files. No `.csproj` changes. No namespace additions.

---

## 3. Method Signatures

### STEP 1 — 5 Private Helpers (CopyEngine.cs)

Insert these private helpers near the existing `Trim`/`Flatten`/`CancelPendingEntries` methods.
Each helper encapsulates the per-account body of its parent's `foreach` loop.

```csharp
private void TrimOneAccount(Account acc, Instrument instrument)

private void FlattenOneAccount(Account acc, Instrument instrument)

private void CancelOneAccount(Account acc, Instrument instrument)

private void TrimOneAccountLimit(Account acc, Instrument instrument,
    int exitBuffer, double ask, double bid)

private void FlattenOneAccountLimit(Account acc, Instrument instrument,
    int exitBuffer, double ask, double bid)
```

### STEP 2 — 5 Internal Leader-Account Overloads (CopyEngine.cs)

Insert after `Flatten(Instrument)` (~L928). Mirror the `BreakEven(Account, Instrument, int)` pattern.

```csharp
internal void Trim(Account leader, Instrument instrument)

internal void Flatten(Account leader, Instrument instrument)

internal void CancelPendingEntries(Account leader, Instrument instrument)

internal void Trim(Account leader, Instrument instrument,
    int exitBuffer, double ask, double bid)

internal void Flatten(Account leader, Instrument instrument,
    int exitBuffer, double ask, double bid)
```

### STEP 3 — TradeCopierPanel.cs Panel Call-Site Updates

```csharp
// OnTrimClick  (L739-742): pass _leaderAccount as first arg to both Trim overloads
// OnFlattenClick (L765-768): pass _leaderAccount as first arg to both Flatten overloads
// OnCancel2 (L912): pass _leaderAccount as first arg to CancelPendingEntries
```

---

## 4. Full Method Bodies

### 4.1 Private Helper Bodies

#### `TrimOneAccount`

```csharp
// B28 T1 -- TrimOneAccount: per-account market trim. CYC=3.
// (1) pos null/qty guard, (2) action ternary, (3) try/catch CreateOrder.
// JS-001: no rethrow. JS-021: no lock. ASCII: PTT-Trim signal name.
private void TrimOneAccount(Account acc, Instrument instrument)
{
    var pos = FindPosition(acc, instrument);
    if (pos == null || pos.Quantity == 0)                               // (1)
    {
        StatusUpdate?.Invoke(acc.Name + ": flat skip");
        return;
    }
    int trimQty = (int)Math.Ceiling(pos.Quantity / 2.0);
    var action = pos.MarketPosition == MarketPosition.Long              // (2)
        ? OrderAction.Sell : OrderAction.BuyToCover;
    try                                                                  // (3)
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

#### `FlattenOneAccount`

```csharp
// B28 T1 -- FlattenOneAccount: per-account market flatten. CYC=3.
// (1) pos null/qty guard, (2) action ternary, (3) try/catch CreateOrder.
private void FlattenOneAccount(Account acc, Instrument instrument)
{
    var pos = FindPosition(acc, instrument);
    if (pos == null || pos.Quantity == 0)                               // (1)
    {
        StatusUpdate?.Invoke(acc.Name + ": flat skip");
        return;
    }
    var action = pos.MarketPosition == MarketPosition.Long              // (2)
        ? OrderAction.Sell : OrderAction.BuyToCover;
    try                                                                  // (3)
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

#### `CancelOneAccount`

```csharp
// B28 T1 -- CancelOneAccount: per-account pending cancel. CYC=4.
// (1) foreach orders, (2) instrument filter, (3) OrderState guard, (4) IsBracketLeg guard.
// Preserves B18 T3 fix: also cancels Initialized orders (DW-B18-CANCEL-01).
private void CancelOneAccount(Account acc, Instrument instrument)
{
    foreach (var order in acc.Orders)                                   // (1)
    {
        if (order.Instrument != instrument) continue;                   // (2)
        if (order.OrderState != OrderState.Working &&
            order.OrderState != OrderState.Initialized) continue;      // (3)
        if (IsBracketLeg(order)) continue;                             // (4)
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

#### `TrimOneAccountLimit`

```csharp
// B28 T1 -- TrimOneAccountLimit: per-account limit trim. CYC=3.
// (1) pos null/qty guard, (2) isLong ternary, (3) try/catch CreateOrder.
// NT8-007: arg12 = (NinjaTrader.Cbi.CustomOrder)null.
private void TrimOneAccountLimit(Account acc, Instrument instrument,
    int exitBuffer, double ask, double bid)
{
    var pos = FindPosition(acc, instrument);
    if (pos == null || pos.Quantity == 0)                               // (1)
    {
        StatusUpdate?.Invoke(acc.Name + ": flat skip");
        return;
    }
    int trimQty = (int)Math.Ceiling(pos.Quantity / 2.0);
    bool isLong = pos.MarketPosition == MarketPosition.Long;            // (2)
    var action  = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
    double tickSize = instrument.MasterInstrument.TickSize;
    double limitPx  = ComputeLimitPx(isLong, ask, bid, exitBuffer, tickSize);
    try                                                                  // (3)
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

#### `FlattenOneAccountLimit`

```csharp
// B28 T1 -- FlattenOneAccountLimit: per-account limit flatten. CYC=3.
// (1) pos null/qty guard, (2) isLong ternary, (3) try/catch CreateOrder.
// NT8-007: arg12 = (NinjaTrader.Cbi.CustomOrder)null.
private void FlattenOneAccountLimit(Account acc, Instrument instrument,
    int exitBuffer, double ask, double bid)
{
    var pos = FindPosition(acc, instrument);
    if (pos == null || pos.Quantity == 0)                               // (1)
    {
        StatusUpdate?.Invoke(acc.Name + ": flat skip");
        return;
    }
    bool isLong = pos.MarketPosition == MarketPosition.Long;            // (2)
    var action  = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
    double tickSize = instrument.MasterInstrument.TickSize;
    double limitPx  = ComputeLimitPx(isLong, ask, bid, exitBuffer, tickSize);
    try                                                                  // (3)
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

### 4.2 Internal Leader-Account Overload Bodies

#### `Trim(Account leader, Instrument instrument)`

```csharp
// B28 T1 -- Trim(Account,Instrument): fires leader directly, no rule needed. CYC=4.
// (1) null guard + StatusUpdate + return, (2) leader direct call,
// (3) foreach AllAccounts fan-out, (4) acc==leader dedup skip.
// JS-021: no lock. JS-002: null => StatusUpdate+return (not throw).
internal void Trim(Account leader, Instrument instrument)
{
    if (leader == null)                                                  // (1)
    {
        StatusUpdate?.Invoke("PTT-Trim: leader null -- skipping");
        return;
    }
    TrimOneAccount(leader, instrument);                                  // (2) leader direct
    foreach (var acc in AllAccounts(instrument))                        // (3) fan-out
    {
        if (acc == leader) continue;                                     // (4) dedup
        TrimOneAccount(acc, instrument);
    }
}
```

#### `Flatten(Account leader, Instrument instrument)`

```csharp
// B28 T1 -- Flatten(Account,Instrument): fires leader directly, no rule needed. CYC=4.
internal void Flatten(Account leader, Instrument instrument)
{
    if (leader == null)                                                  // (1)
    {
        StatusUpdate?.Invoke("PTT-Flatten: leader null -- skipping");
        return;
    }
    FlattenOneAccount(leader, instrument);
    foreach (var acc in AllAccounts(instrument))                        // (3)
    {
        if (acc == leader) continue;                                     // (4)
        FlattenOneAccount(acc, instrument);
    }
}
```

#### `CancelPendingEntries(Account leader, Instrument instrument)`

```csharp
// B28 T1 -- CancelPendingEntries(Account,Instrument): fires leader directly. CYC=4.
internal void CancelPendingEntries(Account leader, Instrument instrument)
{
    if (leader == null)                                                  // (1)
    {
        StatusUpdate?.Invoke("PTT-Cancel: leader null -- skipping");
        return;
    }
    CancelOneAccount(leader, instrument);
    foreach (var acc in AllAccounts(instrument))                        // (3)
    {
        if (acc == leader) continue;                                     // (4)
        CancelOneAccount(acc, instrument);
    }
}
```

#### `Trim(Account leader, Instrument instrument, int exitBuffer, double ask, double bid)`

```csharp
// B28 T1 -- Trim(Account,Instrument,int,double,double): limit trim with leader. CYC=5.
// (1) leader null guard, (2) ask/bid/buffer guard -> fallback to 2-arg overload,
// (3) leader direct call, (4) foreach AllAccounts, (5) acc==leader dedup skip.
internal void Trim(Account leader, Instrument instrument,
    int exitBuffer, double ask, double bid)
{
    if (leader == null)                                                  // (1)
    {
        StatusUpdate?.Invoke("PTT-TrimLimit: leader null -- skipping");
        return;
    }
    if (ask <= 0 || bid <= 0 || exitBuffer == 0)                       // (2)
    {
        Trim(leader, instrument);
        return;
    }
    TrimOneAccountLimit(leader, instrument, exitBuffer, ask, bid);      // (3)
    foreach (var acc in AllAccounts(instrument))                        // (4)
    {
        if (acc == leader) continue;                                     // (5)
        TrimOneAccountLimit(acc, instrument, exitBuffer, ask, bid);
    }
}
```

#### `Flatten(Account leader, Instrument instrument, int exitBuffer, double ask, double bid)`

```csharp
// B28 T1 -- Flatten(Account,Instrument,int,double,double): limit flatten with leader. CYC=5.
internal void Flatten(Account leader, Instrument instrument,
    int exitBuffer, double ask, double bid)
{
    if (leader == null)                                                  // (1)
    {
        StatusUpdate?.Invoke("PTT-FlattenLimit: leader null -- skipping");
        return;
    }
    if (ask <= 0 || bid <= 0 || exitBuffer == 0)                       // (2)
    {
        Flatten(leader, instrument);
        return;
    }
    FlattenOneAccountLimit(leader, instrument, exitBuffer, ask, bid);
    foreach (var acc in AllAccounts(instrument))                        // (4)
    {
        if (acc == leader) continue;                                     // (5)
        FlattenOneAccountLimit(acc, instrument, exitBuffer, ask, bid);
    }
}
```

---

## 5. Exact Before/After for Each Panel Call Site

### 5.1 `OnTrimClick` — `TradeCopierPanel.cs` L739-742

**BEFORE:**
```csharp
if (ask <= 0 || bid <= 0 || _trimBuffer == 0)
    _engine.Trim(_instrument);
else
    _engine.Trim(_instrument, _trimBuffer, ask, bid);
```

**AFTER:**
```csharp
if (ask <= 0 || bid <= 0 || _trimBuffer == 0)
    _engine.Trim(_leaderAccount, _instrument);
else
    _engine.Trim(_leaderAccount, _instrument, _trimBuffer, ask, bid);
```

### 5.2 `OnFlattenClick` — `TradeCopierPanel.cs` L765-768

**BEFORE:**
```csharp
if (ask <= 0 || bid <= 0 || _flattenBuffer == 0)
    _engine.Flatten(_instrument);
else
    _engine.Flatten(_instrument, _flattenBuffer, ask, bid);
```

**AFTER:**
```csharp
if (ask <= 0 || bid <= 0 || _flattenBuffer == 0)
    _engine.Flatten(_leaderAccount, _instrument);
else
    _engine.Flatten(_leaderAccount, _instrument, _flattenBuffer, ask, bid);
```

### 5.3 `OnCancel2` — `TradeCopierPanel.cs` L912

**BEFORE:**
```csharp
if (_instrument != null) _engine.CancelPendingEntries(_instrument);
```

**AFTER:**
```csharp
if (_instrument != null) _engine.CancelPendingEntries(_leaderAccount, _instrument);
```

**Reference field:** `_leaderAccount` is declared at `TradeCopierPanel.cs:L120`.
It is already used successfully by `BreakEven(_leaderAccount, _instrument, _beBuffer)` at
`TradeCopierPanel.cs:L777`. No new field needed.

---

## 6. [Fact] Test Specifications

Add all three tests to
[`CopyEngineTests.cs`](../../../universal-or-strategy/src/PropTraderTools/CopyEngineTests.cs).
Tests use reflection — no live NT8 session required. xUnit only (no NUnit, no MSTest).

**Note:** `BindingFlags.NonPublic | BindingFlags.Instance` returns both `private` and `internal`
symbols. The `internal` overloads added in STEP 2 are correctly resolved by these tests.

### T_B28_01 — Trim leader-account overload exists

```csharp
[Fact]
public void T_B28_01_Trim_LeaderOverload_Exists()
{
    // Verify CopyEngine.Trim(Account, Instrument) overload was added by B28.
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

---

## 7. JS Rule and NT8 Constraint Table

### Jane Street Rules

| Rule | Requirement | Applies To | Status |
|------|-------------|-----------|--------|
| **JS-021** | No `lock()` anywhere | All new methods in `CopyEngine.cs` | `foreach` + direct calls only. `AllAccounts` iterates `ConcurrentBag` (lock-free). ✅ |
| **JS-001** | No `throw` in hot paths | All new methods | All `CreateOrder`/`Cancel` calls wrapped in `try/catch` with `StatusUpdate`. No rethrow. ✅ |
| **JS-002** | No `return null` for missing values | All new methods | All new methods are `void`. Null leader path uses `StatusUpdate + return`. ✅ |
| **JS-033** | No `async void` (non-event-handler) | All new methods | All methods synchronous `void`. ✅ |
| **JS-015** | No unvalidated string types crossing boundaries | All string literals | ASCII PTT-prefixed signal names. No string params. ✅ |

### NT8 Compiler Rules

| Rule | Requirement | Applies To | Status |
|------|-------------|-----------|--------|
| **NT8-001** | No `{ get; init; }` | All new symbols | New symbols are methods, not properties. ✅ |
| **NT8-002** | No `abstract/sealed record` | New types | No new types added. ✅ |
| **NT8-003** | No `volatile double` | New fields | No new fields added. ✅ |
| **NT8-004** | No `ImmutableDictionary` / `System.Collections.Immutable` | New collections | No new collections. ✅ |
| **NT8-007** | `CreateOrder` arg 12 = `(NinjaTrader.Cbi.CustomOrder)null` for Limit orders | `TrimOneAccountLimit`, `FlattenOneAccountLimit` | Cast preserved in both limit helpers. ✅ |
| **NT8-014** | Signal name = `"PTT-..."` prefix | All `CreateOrder` calls | `PTT-Trim`, `PTT-Flatten`, `PTT-TrimLimit`, `PTT-FlattenLimit`, `PTT-Cancel` maintained. ✅ |
| **ASCII** | ASCII-only identifiers and string literals | All new code | All new identifiers and string literals are ASCII. No Unicode. ✅ |
| **DateTime** | No `DateTime.Now` — use `DateTime.UtcNow` or NT8 sentinel | All `CreateOrder` calls | `DateTime.MaxValue` used as NT8 order expiry sentinel (unchanged). ✅ |

### CYC Budget

| Method | CYC | Branches |
|--------|-----|---------|
| `TrimOneAccount` | **3** | pos guard, action ternary, try/catch |
| `FlattenOneAccount` | **3** | pos guard, action ternary, try/catch |
| `CancelOneAccount` | **4** | foreach, instr filter, state guard, bracket guard |
| `TrimOneAccountLimit` | **3** | pos guard, isLong ternary, try/catch |
| `FlattenOneAccountLimit` | **3** | pos guard, isLong ternary, try/catch |
| `Trim(Account,Instrument)` | **4** | null guard, leader call, foreach, leader skip |
| `Flatten(Account,Instrument)` | **4** | null guard, leader call, foreach, leader skip |
| `CancelPendingEntries(Account,Instrument)` | **4** | null guard, leader call, foreach, leader skip |
| `Trim(Account,Instrument,int,double,double)` | **5** | null guard, ask/bid guard, leader call, foreach, leader skip |
| `Flatten(Account,Instrument,int,double,double)` | **5** | null guard, ask/bid guard, leader call, foreach, leader skip |

**All 10 new methods: CYC <= 8.** ✅

---

## 8. Seven-Scan Checklist (SCAN-01 through SCAN-07)

The engineer MUST run all 7 scans and drive every result to ZERO before declaring BUILD_PASS.
These are run from the Wave workspace root (`c:\WSGTA\universal-or-strategy\`).

```
SCAN-01: grep -n "lock(" src/PropTraderTools/CopyEngine.cs
         Expected: 0 results
         Rationale: JS-021 hard ban — no lock() anywhere.

SCAN-02: grep -n "async void " src/PropTraderTools/CopyEngine.cs
         Expected: 0 results
         Rationale: JS-033 hard ban — no async void in non-event-handlers.

SCAN-03: grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs
         Expected: 0 results
         Rationale: JS-021 hard ban — no lock() anywhere.

SCAN-04: grep -n "Trim(_instrument)" src/PropTraderTools/TradeCopierPanel.cs
         Expected: 0 results
         Rationale: Old single-arg call site must be gone — replaced with leader overload.

SCAN-05: grep -n "Flatten(_instrument)" src/PropTraderTools/TradeCopierPanel.cs
         Expected: 0 results
         Rationale: Old single-arg call site must be gone — replaced with leader overload.

SCAN-06: grep -n "CancelPendingEntries(_instrument)" src/PropTraderTools/TradeCopierPanel.cs
         Expected: 0 results
         Rationale: Old single-arg call site must be gone — replaced with leader overload.

SCAN-07: grep -c "\[Fact\]" src/PropTraderTools/CopyEngineTests.cs
         Expected: 138  (baseline 135 + 3 new T_B28_01/02/03 tests)
```

---

## 9. Success Criteria

The ticket is complete when ALL of the following are verified:

| Criterion | How to Verify |
|-----------|--------------|
| SCAN-01 through SCAN-07 all pass at target value | Run 7-scan checklist above |
| `[Fact]` count = **138** | `grep -c "\[Fact\]" CopyEngineTests.cs` |
| T_B28_01, T_B28_02, T_B28_03 all pass | `dotnet test` green |
| No new `lock()` in modified files | SCAN-01, SCAN-03 |
| No new `async void` | SCAN-02 |
| No new `return null` | Manual review of new void methods |
| No new `throw` (bare rethrow in hot path) | Manual review — only `catch (Exception ex)` with StatusUpdate |
| CYC <= 8 for all 10 new methods | `python scripts/complexity_audit.py` |
| NT8 F5 compilation green | F5 in NinjaTrader — zero errors, zero warnings |
| Trim button fires on leader account with empty _rules | Manual test: press Trim before Apply Rule |
| Flatten button fires on leader account with empty _rules | Manual test: press Flatten before Apply Rule |
| Cancel button cancels leader account pending entries with empty _rules | Manual test: press Cancel with pending entry |
| null `_leaderAccount` produces StatusUpdate message, not silent no-op | Check panel status line when leader is null |

---

## 10. Threading Model (Engineer Reference)

- All three panel handlers (`OnTrimClick`, `OnFlattenClick`, `OnCancel2`) execute on the
  NT8 WPF dispatch thread.
- All new `CopyEngine` methods are synchronous `void` — no `Task`, no `async`, no background threads.
- `AllAccounts(Instrument)` iterates `_rules` (a `ConcurrentBag`) — lock-free, snapshot-safe
  for `foreach`.
- `acc.CreateOrder` and `acc.Cancel` are NT8 API calls safe to invoke from the dispatch thread.
- **No `Dispatcher.InvokeAsync` needed** inside `CopyEngine.cs`. The Panel handlers already run
  on the correct thread — confirmed by the existing `BreakEven(_leaderAccount, ...)` call at
  `TradeCopierPanel.cs:L777` which uses the identical pattern without additional dispatch.

---

*Tickets written by ptt-architect. Engineer: implement STEP 1 -> STEP 2 -> STEP 3 in order.*
*Run 7-scan checklist after all steps complete. Return BUILD_PASS to ptt-verifier.*
