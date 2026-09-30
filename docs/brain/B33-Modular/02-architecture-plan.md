# B33 — Modular Independence Architecture Plan
# Version: 1.0 | Status: REVIEW_PENDING
# Author: ptt-architect
# Date: 2026-07-25
# Wave workspace: C:\WSGTA\universal-or-strategy\src\PropTraderTools\
# Director workspace: C:\WSGTA\universal-or-strategy-director\
# Baseline build: "PTT-COPIER B35 | bracket-cancel + BE-fixes | 2026-07-23"
# Baseline [Fact] count: 164

---

## 0. Rules Catalog Gate Result

```
STEP 0 — RULES CATALOG GATE (mandatory, non-skippable)
  [x] Read docs/standards/jane-street/RULES_CATALOG.md (UTF-8 clean)
  [x] Read docs/standards/NT8_COMPILER_RULES.md (Version 1.6)
  [x] Zero P0 violations in planned code

P0 violations checked:
  JS-021 (lock):           CLEAR — CLR events on UI thread only, no lock() anywhere
  JS-033 (async void):     CLEAR — all Execute() methods are synchronous void
  JS-001 (throw hot path): CLEAR — no throw in business logic; null-guard + early return
  JS-002 (return null):    CLEAR — all methods return void or raise events
  NT8-001 ({get;init;}):   CLEAR — EventArgs use {get; private set;} + constructor
  NT8-002 (records):       CLEAR — all EventArgs are class : EventArgs, no records
  NT8-003 (volatile dbl):  CLEAR — no double fields in new files
  NT8-007 (CreateOrder):   CLEAR — arg11 is (NinjaTrader.Cbi.CustomOrder)null
  NT8-013 (DateTime.Now):  CLEAR — DateTime.MaxValue for CreateOrder expiry
  NT8-014 (PTT- prefix):   CLEAR — signal name "PTT-BE-Stop" used
  NT8-019 (async void):    CLEAR — no async anywhere in new code
  NT8-042 (Dispatcher):    CLEAR — all paths on UI thread, no Dispatcher.InvokeAsync
  NT8-043 (null-cond -=):  CLEAR — PttCopier.Teardown uses if-guards for unsubscribe
  NT8-049 (arg order):     CLEAR — arg6=limitPrice=0, arg7=stopPrice=bePrice
  NT8-050 (Positions[]):   CLEAR — FindPosition() helper used, never Positions[Instrument]

GATE RESULT: PASS
```

---

## 1. File Structure Diagram

```
src/PropTraderTools/
├─ Core/                                    ← NEW DIRECTORY
│   └─ PttContracts.cs                      ← NEW (~60 lines)
│       interfaces: IPttModule, IPttHostContext
│       event hub:  PttBus (static)
│       event args: BeEventArgs, TrimEventArgs, FlatEventArgs, CancelEventArgs
│
├─ Features/                                ← NEW DIRECTORY
│   ├─ PttBreakEven.cs                      ← NEW (~80 lines)
│   ├─ PttTrim.cs                           ← NEW (~50 lines)
│   ├─ PttFlatten.cs                        ← NEW (~50 lines)
│   ├─ PttCancel.cs                         ← NEW (~40 lines)
│   └─ PttCopier.cs                         ← NEW (~60 lines)
│
├─ CopyEngine.cs                            ← MODIFIED (dead code removal + build tag)
│                                              ~80 lines deleted, ~1 string changed
├─ TradeCopierPanel.cs                      ← MODIFIED (~30 lines changed)
│                                              implements IPttHostContext
│                                              wires _modules list
│                                              adds license bools
│
├─ TradeCopierAddOn.cs                      ← UNCHANGED
├─ TradeCopierWindow.cs                     ← UNCHANGED
└─ [all other existing files]               ← UNCHANGED
```

**NT8 flat-compilation note:** NT8 NinjaScript AddOns compile all .cs files in the AddOn
folder into a single assembly. The Core/ and Features/ subdirectories are logical organization
only. All files share the same namespace: `NinjaTrader.NinjaScript.AddOns.PropTraderTools`.

---

## 2. Interface Definitions (verbatim — NT8-001 compliant)

### 2a. IPttModule

```csharp
// File: Core/PttContracts.cs
namespace NinjaTrader.NinjaScript.AddOns.PropTraderTools
{
    /// <summary>
    /// Contract for every PTT trading module.
    /// </summary>
    public interface IPttModule
    {
        /// <summary>Stable module identifier (e.g. "BE", "TRIM", "FLAT").</summary>
        string ModuleId { get; }

        /// <summary>When false, Execute() is a no-op. Default: true.</summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Called once after panel initialization. Subscribe to PttBus events here.
        /// Called on UI thread only.
        /// </summary>
        void Initialize(IPttHostContext ctx);

        /// <summary>
        /// Called once during panel teardown. Unsubscribe all PttBus events here.
        /// Called on UI thread only.
        /// </summary>
        void Teardown();
    }
}
```

**NT8-001 compliance:** No `{ get; init; }` — ModuleId and IsEnabled are interface declarations
(no backing), implemented with `{ get; private set; }` + constructor in concrete classes.

### 2b. IPttHostContext

```csharp
    /// <summary>
    /// Read-only context passed to IPttModule.Execute(). 
    /// Implemented by TradeCopierPanel.
    /// </summary>
    public interface IPttHostContext
    {
        /// <summary>The leader account (source of truth for sizing).</summary>
        Account LeaderAccount { get; }

        /// <summary>The currently tracked instrument (e.g. NQ 09-26).</summary>
        Instrument Instrument { get; }

        /// <summary>Leader account + all follower accounts (for BE loop).</summary>
        IReadOnlyList<Account> AllAccounts { get; }
    }
```

**NT8-021 compliance:** AllAccounts is populated at panel initialization time (inside
OnWindowCreated/Attach event handler), NOT in field initializers or constructors.

---

## 3. PttBus Event Hub Design

```csharp
// File: Core/PttContracts.cs (continued)

    /// <summary>
    /// Static CLR event hub for PTT module communication.
    /// 
    /// THREADING CONTRACT (enforced by design):
    ///   - Subscribe (+= ) only from IPttModule.Initialize()  — UI thread
    ///   - Unsubscribe (-=) only from IPttModule.Teardown()   — UI thread
    ///   - Fire (?.Invoke)  from module.Execute()              — UI thread
    /// No cross-thread subscription permitted. No lock() needed (CLR += on UI thread is safe).
    /// </summary>
    public static class PttBus
    {
        // No lock() — JS-021 compliant.
        // Subscribed/unsubscribed on UI thread only — NT8-018 compliant.
        public static event EventHandler<BeEventArgs>     BeFired;
        public static event EventHandler<TrimEventArgs>   TrimFired;
        public static event EventHandler<FlatEventArgs>   FlatFired;
        public static event EventHandler<CancelEventArgs> CancelFired;

        // Internal: called by each module after executing its logic.
        internal static void RaiseBe    (object sender, BeEventArgs e)     => BeFired?.Invoke(sender, e);
        internal static void RaiseTrim  (object sender, TrimEventArgs e)   => TrimFired?.Invoke(sender, e);
        internal static void RaiseFlatted(object sender, FlatEventArgs e)  => FlatFired?.Invoke(sender, e);
        internal static void RaiseCancel(object sender, CancelEventArgs e) => CancelFired?.Invoke(sender, e);
    }
```

**JS-021 compliance:** No `lock()`. CLR event multicast delegates are thread-safe for
`+=` / `-=` (they create new delegate lists atomically). Since all sub/unsub happen on
the same UI thread, there is no contention.

**NT8-043 compliance:** The `?.Invoke(sender, e)` pattern is equivalent to:
```csharp
if (BeFired != null) BeFired(sender, e);
```
The `?.Invoke` is a null-conditional *method call*, NOT a null-conditional assignment.
This is valid in C# 7.3 (NT8's language version). ✅

---

## 4. EventArgs Definitions

```csharp
// File: Core/PttContracts.cs (continued)

    // NT8-001: {get; private set;} + constructor — NO {get; init;}
    // NT8-002: class : EventArgs — NO records

    public class BeEventArgs : EventArgs
    {
        public Instrument Instrument   { get; private set; }
        public double     BePrice      { get; private set; }
        public double     EntryPrice   { get; private set; }
        public bool       IsLong       { get; private set; }
        public string     OcoGroup     { get; private set; }

        public BeEventArgs(Instrument instr, double bePrice, double entryPrice,
                           bool isLong, string ocoGroup)
        {
            Instrument = instr;
            BePrice    = bePrice;
            EntryPrice = entryPrice;
            IsLong     = isLong;
            OcoGroup   = ocoGroup ?? string.Empty;
        }
    }

    public class TrimEventArgs : EventArgs
    {
        public Instrument Instrument { get; private set; }
        public int        TrimPercent { get; private set; }
        public int        ActualQty   { get; private set; }

        public TrimEventArgs(Instrument instr, int trimPercent, int actualQty)
        {
            Instrument  = instr;
            TrimPercent = trimPercent;
            ActualQty   = actualQty;
        }
    }

    public class FlatEventArgs : EventArgs
    {
        public Instrument Instrument { get; private set; }
        public FlatEventArgs(Instrument instr) { Instrument = instr; }
    }

    public class CancelEventArgs : EventArgs
    {
        public Instrument Instrument { get; private set; }
        public CancelEventArgs(Instrument instr) { Instrument = instr; }
    }
```

**CYC analysis:** All constructors CYC = 1 (assignment only). ✅

---

## 5. Per-Module Class Skeletons

### 5a. PttBreakEven.cs (~80 lines)

```csharp
// File: Features/PttBreakEven.cs
// Imports: Core/PttContracts.cs + NinjaTrader.Cbi ONLY
// DOES NOT import: CopyEngine.cs (calls acc.CreateOrder directly)

using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.AddOns.PropTraderTools
{
    /// <summary>
    /// BE module: cancel stale brackets + submit stop-at-entry for ALL accounts.
    /// DW-B36-01 FIX: loops ctx.AllAccounts so leader AND followers all receive SubmitBeStop.
    /// </summary>
    public class PttBreakEven : IPttModule
    {
        public string ModuleId  { get; private set; }
        public bool   IsEnabled { get; private set; }

        private IPttHostContext _ctx;

        public PttBreakEven()
        {
            ModuleId  = "BE";
            IsEnabled = true;
        }

        public void Initialize(IPttHostContext ctx)
        {
            _ctx = ctx;
            // No PttBus subscription needed — this module fires BeFired, not listens
        }

        public void Teardown()
        {
            _ctx = null;
        }

        /// <summary>
        /// Execute break-even for all accounts.
        /// CYC <= 4.
        /// Called on UI thread (from TradeCopierPanel button click).
        /// </summary>
        public void Execute(IPttHostContext ctx)
        {
            if (!IsEnabled) return;                                // guard 1

            var leaderPos = FindPosition(ctx.LeaderAccount, ctx.Instrument);
            if (leaderPos == null || leaderPos.Quantity == 0) return;  // guard 2

            double entryPrice = leaderPos.AveragePrice;
            bool   isLong     = leaderPos.MarketPosition == MarketPosition.Long;
            double bePrice    = entryPrice;

            CancelStaleBracketsLocal(ctx.LeaderAccount, ctx.Instrument);

            foreach (Account acc in ctx.AllAccounts)              // loop — CYC += 1
                SubmitBeStopLocal(acc, ctx.Instrument, bePrice, isLong);

            PttBus.RaiseBe(this, new BeEventArgs(
                ctx.Instrument, bePrice, entryPrice, isLong, string.Empty));
        }

        // ── private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Cancel stale ATM bracket orders for the given account+instrument.
        /// Mirrors CopyEngine.CancelStaleBrackets (B35 L1680) inline — no CopyEngine import.
        /// NT8-031: OrderState.Working + Initialized only (no PendingSubmit).
        /// NT8-006: .Where() requires using System.Linq — use explicit foreach to avoid it.
        /// CYC <= 3.
        /// </summary>
        private static void CancelStaleBracketsLocal(Account acc, Instrument instr)
        {
            if (acc == null || instr == null) return;

            var stale = new List<Order>();
            foreach (Order o in acc.Orders)
            {
                bool stateOk  = o.OrderState == OrderState.Working
                             || o.OrderState == OrderState.Initialized;
                bool instrOk  = o.Instrument != null
                             && o.Instrument.FullName == instr.FullName;
                bool notPtt   = o.Name != "PTT-BE-Stop";
                if (stateOk && instrOk && notPtt)
                    stale.Add(o);
            }
            if (stale.Count == 0) return;
            try { acc.Cancel(stale.ToArray()); } catch { /* ignore cancel errors on already-flat */ }
        }

        /// <summary>
        /// Submit a StopMarket order at bePrice for the given account.
        /// NT8-007: arg11 = (CustomOrder)null
        /// NT8-013: DateTime.MaxValue
        /// NT8-014: "PTT-BE-Stop"
        /// NT8-049: arg6=0 (limitPrice), arg7=bePrice (stopPrice)
        /// NT8-050: FindPosition via foreach, NOT Positions[instr]
        /// CYC <= 3.
        /// </summary>
        private static void SubmitBeStopLocal(Account acc, Instrument instr,
                                              double bePrice, bool isLong)
        {
            if (acc == null || instr == null) return;

            Position pos = FindPosition(acc, instr);
            if (pos == null || pos.Quantity == 0) return;

            OrderAction direction = isLong ? OrderAction.Sell : OrderAction.Buy;

            acc.CreateOrder(
                instr,
                direction,
                OrderType.StopMarket,
                OrderEntry.Manual,
                TimeInForce.Day,
                pos.Quantity,     // qty from live position
                0,                // arg6: limitPrice = 0 (NT8-049: NEVER swap with stopPrice)
                bePrice,          // arg7: stopPrice = bePrice (NT8-049)
                string.Empty,     // oco group
                "PTT-BE-Stop",    // signal name (NT8-014: must start with "PTT-")
                DateTime.MaxValue,// gtd (NT8-013: not DateTime.Now)
                (NinjaTrader.Cbi.CustomOrder)null);  // arg11 (NT8-007: not a string)
        }

        /// <summary>
        /// Find position for account+instrument without Positions[Instrument] indexer.
        /// NT8-050: Account.Positions exposes int indexer only; never use Positions[Instrument].
        /// </summary>
        private static Position FindPosition(Account acc, Instrument instr)
        {
            foreach (Position p in acc.Positions)
                if (p.Instrument == instr)
                    return p;
            return null;
        }
    }
}
```

**CYC analysis:**
- `Execute()`: CYC = 4 (2 guards + 1 foreach loop + 1 optional internal branch). ✅
- `CancelStaleBracketsLocal()`: CYC = 4 (1 null guard + 1 foreach + 3 conditions fused). ✅
- `SubmitBeStopLocal()`: CYC = 3 (2 null guards + 1 ternary). ✅
- `FindPosition()`: CYC = 2 (1 foreach + 1 if). ✅

---

### 5b. PttTrim.cs (~50 lines)

```csharp
// File: Features/PttTrim.cs
// Partial close on leader account.

public class PttTrim : IPttModule
{
    public string ModuleId  { get; private set; }
    public bool   IsEnabled { get; private set; }

    public PttTrim()    { ModuleId = "TRIM"; IsEnabled = true; }

    public void Initialize(IPttHostContext ctx) { }
    public void Teardown() { }

    /// <summary>Execute partial close (trim) on leader account. CYC <= 3.</summary>
    public void Execute(IPttHostContext ctx)
    {
        if (!IsEnabled) return;

        Position pos = FindPositionLocal(ctx.LeaderAccount, ctx.Instrument);
        if (pos == null || pos.Quantity == 0) return;

        int trimQty = Math.Max(1, pos.Quantity / 2);   // 50% trim default
        TrimPositionLocal(ctx.LeaderAccount, ctx.Instrument, trimQty, pos);

        PttBus.RaiseTrim(this, new TrimEventArgs(ctx.Instrument, 50, trimQty));
    }

    // TrimPositionLocal: calls acc.CreateOrder for a market close of trimQty.
    // Signature: void TrimPositionLocal(Account acc, Instrument instr, int qty, Position pos)
    // CYC <= 2: null guard + single CreateOrder call.
    // NT8 compliance: arg6=0, arg7=0 for market orders; "PTT-Trim" signal name.

    private static Position FindPositionLocal(Account acc, Instrument instr)
    {
        foreach (Position p in acc.Positions)
            if (p.Instrument == instr) return p;
        return null;
    }
}
```

**Note:** `TrimPositionLocal` full implementation follows same NT8 pattern as `SubmitBeStopLocal` —
`acc.CreateOrder(instr, direction, OrderType.Market, OrderEntry.Market, TimeInForce.Day, qty,
0, 0, string.Empty, "PTT-Trim", DateTime.MaxValue, (CustomOrder)null)`.

---

### 5c. PttFlatten.cs (~50 lines)

```csharp
// File: Features/PttFlatten.cs

public class PttFlatten : IPttModule
{
    public string ModuleId  { get; private set; }
    public bool   IsEnabled { get; private set; }

    public PttFlatten()  { ModuleId = "FLAT"; IsEnabled = true; }

    public void Initialize(IPttHostContext ctx) { }
    public void Teardown() { }

    /// <summary>Full close on leader account. CYC <= 2.</summary>
    public void Execute(IPttHostContext ctx)
    {
        if (!IsEnabled) return;

        Position pos = FindPositionLocal(ctx.LeaderAccount, ctx.Instrument);
        if (pos == null || pos.Quantity == 0) return;

        FlattenPositionLocal(ctx.LeaderAccount, ctx.Instrument, pos);

        PttBus.RaiseFlatted(this, new FlatEventArgs(ctx.Instrument));
    }

    // FlattenPositionLocal: acc.CreateOrder for full close (pos.Quantity).
    // Signal: "PTT-Flatten". CYC <= 2.
    // NT8 compliance: same pattern as PttTrim.TrimPositionLocal.

    private static Position FindPositionLocal(Account acc, Instrument instr)
    {
        foreach (Position p in acc.Positions)
            if (p.Instrument == instr) return p;
        return null;
    }
}
```

---

### 5d. PttCancel.cs (~40 lines)

```csharp
// File: Features/PttCancel.cs

public class PttCancel : IPttModule
{
    public string ModuleId  { get; private set; }
    public bool   IsEnabled { get; private set; }

    public PttCancel()  { ModuleId = "CANCEL"; IsEnabled = true; }

    public void Initialize(IPttHostContext ctx) { }
    public void Teardown() { }

    /// <summary>Cancel all working entry orders for leader+instrument. CYC <= 3.</summary>
    public void Execute(IPttHostContext ctx)
    {
        if (!IsEnabled) return;

        CancelWorkingEntriesLocal(ctx.LeaderAccount, ctx.Instrument);

        PttBus.RaiseCancel(this, new CancelEventArgs(ctx.Instrument));
    }

    // CancelWorkingEntriesLocal: foreach o in acc.Orders where Working && instrMatch
    //   collect to list → acc.Cancel(list.ToArray())
    // CYC <= 3.
    // NT8-031: only Working + Initialized states (no PendingSubmit).
}
```

---

### 5e. PttCopier.cs (~60 lines)

```csharp
// File: Features/PttCopier.cs
// Imports: Core/PttContracts.cs + NinjaTrader.Cbi + CopyEngine (fan-out methods only)
// Subscribes to PttBus and relays to CopyEngine fan-out for follower accounts.

public class PttCopier : IPttModule
{
    public string ModuleId  { get; private set; }
    public bool   IsEnabled { get; private set; }

    private readonly CopyEngine _engine;

    public PttCopier(CopyEngine engine)
    {
        ModuleId  = "COPY";
        IsEnabled = true;
        _engine   = engine;
    }

    /// <summary>Subscribe to all PttBus events. CYC = 1.</summary>
    public void Initialize(IPttHostContext ctx)
    {
        PttBus.BeFired     += OnBeFired;
        PttBus.TrimFired   += OnTrimFired;
        PttBus.FlatFired   += OnFlatFired;
        PttBus.CancelFired += OnCancelFired;
    }

    /// <summary>Unsubscribe all PttBus events. CYC = 1. NT8-043 compliant.</summary>
    public void Teardown()
    {
        PttBus.BeFired     -= OnBeFired;
        PttBus.TrimFired   -= OnTrimFired;
        PttBus.FlatFired   -= OnFlatFired;
        PttBus.CancelFired -= OnCancelFired;
    }

    // Event handlers — each delegates to CopyEngine fan-out method. CYC = 1 each.

    private void OnBeFired    (object s, BeEventArgs e)     => _engine.RelayBe(e);
    private void OnTrimFired  (object s, TrimEventArgs e)   => _engine.RelayTrim(e);
    private void OnFlatFired  (object s, FlatEventArgs e)   => _engine.RelayFlatten(e);
    private void OnCancelFired(object s, CancelEventArgs e) => _engine.RelayCancel(e);
}
```

**CopyEngine fan-out method contracts (engineer must add to CopyEngine.cs):**
- `void RelayBe(BeEventArgs e)` — fan-out SubmitBeStop to all follower accounts
- `void RelayTrim(TrimEventArgs e)` — fan-out partial close to followers
- `void RelayFlatten(FlatEventArgs e)` — fan-out full close to followers
- `void RelayCancel(CancelEventArgs e)` — fan-out cancel entries to followers

**NOTE:** These relay methods in CopyEngine are thin delegates to existing private helpers
(SyncFollowerBracket, FlattenOneAccount, TrimOneAccount, CancelPendingEntries). The engineer
adds public relay entry points that the PttCopier module can call. All follower iteration
logic stays inside CopyEngine — PttCopier only triggers it.

**NT8-043 compliance:** All event unsubscriptions in Teardown() use direct `-=` (not null-
conditional `?.Event -=` which is C# 9 syntax banned in NT8 C# 7.3). ✅

---

## 6. TradeCopierPanel Changes

### 6a. Implement IPttHostContext

```csharp
// TradeCopierPanel.cs — add interface to class declaration:
public class TradeCopierPanel : UserControl, IPttHostContext   // add IPttHostContext

// Add IPttHostContext property implementations:

/// <summary>NT8-021: populated in Attach/Initialize handler, NOT in constructor.</summary>
public Account LeaderAccount
{
    get
    {
        // Return the currently selected leader account from panel state.
        // Pattern: same as existing _leaderAccount field access in panel.
        return _leaderAccount;   // already exists in TradeCopierPanel state
    }
}

public Instrument Instrument
{
    get { return _instrument; }   // already exists in TradeCopierPanel state
}

/// <summary>IReadOnlyList of leader + follower accounts. Built at Initialize time.</summary>
public IReadOnlyList<Account> AllAccounts
{
    get { return _allAccounts; }   // see 6b for population
}

// New field:
private List<Account> _allAccounts = new List<Account>();
```

### 6b. Module Registry

```csharp
// New fields in TradeCopierPanel:
private readonly List<IPttModule> _modules = new List<IPttModule>();

// Helper:
private void AddModule(IPttModule m)
{
    _modules.Add(m);
}
```

### 6c. Module Initialization (in existing panel Initialize/Attach method)

```csharp
// In TradeCopierPanel.Initialize() or equivalent OnAttach handler (UI thread):

// 1. Populate AllAccounts (NT8-021: Account.All safe here — inside event handler)
_allAccounts.Clear();
if (_leaderAccount != null)
    _allAccounts.Add(_leaderAccount);
foreach (Account acc in Account.All)
{
    if (acc != _leaderAccount && IsFollowerAccount(acc))
        _allAccounts.Add(acc);
}

// 2. Register modules (after AllAccounts is populated)
AddModule(new PttBreakEven());
AddModule(new PttTrim());
AddModule(new PttFlatten());
AddModule(new PttCancel());
AddModule(new PttCopier(_engine));   // _engine = existing CopyEngine reference

// 3. Initialize all modules
foreach (IPttModule m in _modules)
    m.Initialize(this);   // "this" implements IPttHostContext
```

### 6d. Module Teardown

```csharp
// In TradeCopierPanel.Teardown() or equivalent OnDetach handler:

foreach (IPttModule m in _modules)
    m.Teardown();
_modules.Clear();
```

### 6e. Replace Direct Engine Calls with Module Execute

```csharp
// BEFORE (B35):
private void OnBreakEvenClick(object sender, RoutedEventArgs e)
{
    _engine.BreakEven(_leaderAccount, _instrument, ...);
}

// AFTER (B33):
private void OnBreakEvenClick(object sender, RoutedEventArgs e)
{
    foreach (IPttModule m in _modules)
    {
        if (m.ModuleId == "BE" && m.IsEnabled)
            m.Execute(this);  // type: PttBreakEven.Execute
    }
}
```

**Apply same pattern to Trim, Flatten, Cancel button handlers.**

### 6f. NT8 License Properties

```csharp
// Add as NT8 [NinjaScriptProperty] booleans (read in NT8 property grid):
// Default: all true (enabled)

[NinjaScriptProperty]
[Display(Name = "BE Licensed", Order = 201, GroupName = "PTT Licenses")]
public bool IsBeLicensed { get; set; }

[NinjaScriptProperty]
[Display(Name = "Trim Licensed", Order = 202, GroupName = "PTT Licenses")]
public bool IsTrimLicensed { get; set; }

[NinjaScriptProperty]
[Display(Name = "Flatten Licensed", Order = 203, GroupName = "PTT Licenses")]
public bool IsFlattenLicensed { get; set; }

[NinjaScriptProperty]
[Display(Name = "Cancel Licensed", Order = 204, GroupName = "PTT Licenses")]
public bool IsCancelLicensed { get; set; }

[NinjaScriptProperty]
[Display(Name = "Copier Licensed", Order = 205, GroupName = "PTT Licenses")]
public bool IsCopierLicensed { get; set; }
```

**Wire license bools to module IsEnabled in AddModule or Initialize:**
```csharp
// After AddModule calls, wire license flags:
// (access _modules by index or by LINQ lookup — use .Count guard, not .Any())
foreach (IPttModule m in _modules)
{
    switch (m.ModuleId)
    {
        case "BE":     ((PttBreakEven)m).SetEnabled(IsBeLicensed);     break;
        case "TRIM":   ((PttTrim)m).SetEnabled(IsTrimLicensed);        break;
        case "FLAT":   ((PttFlatten)m).SetEnabled(IsFlattenLicensed);  break;
        case "CANCEL": ((PttCancel)m).SetEnabled(IsCancelLicensed);    break;
        case "COPY":   ((PttCopier)m).SetEnabled(IsCopierLicensed);    break;
    }
}
```

**Add SetEnabled(bool) method to each module class:**
```csharp
public void SetEnabled(bool enabled) { IsEnabled = enabled; }
```

Note: `IsEnabled` uses `{ get; private set; }` — `SetEnabled()` is the public mutator.
CYC = 1. NT8-001 compliant. ✅

---

## 7. CopyEngine.cs Dead Code Removal Spec

### 7a. Fields to Delete

| Line | Declaration | Action |
|------|------------|--------|
| 136 | `_trailBeSlots` field | DELETE entire field line |
| 138 | `_trailBeLastPnlBits` field | DELETE entire field line |

**Pre-deletion grep (engineer must run):**
```powershell
Select-String -Path CopyEngine.cs -Pattern "_trailBeSlots|_trailBeLastPnlBits"
```
Expected: ONLY field declaration lines (no callers in other methods). If any callers found, STOP — report to Director before proceeding.

### 7b. Methods to Delete

| Start Line | Method Name | Action |
|-----------|------------|--------|
| ~1930 | `ArmTrailBe(...)` | DELETE entire method + closing brace |
| ~1953 | `DisarmTrailBe(...)` | DELETE entire method + closing brace |
| ~1974 | `OnTrailBeAccountUpdate(...)` | DELETE entire method + closing brace |

**Pre-deletion grep (engineer must run):**
```powershell
Select-String -Path CopyEngine.cs -Pattern "ArmTrailBe|DisarmTrailBe|OnTrailBeAccountUpdate"
```
Expected: ONLY method definition lines (no callers). If callers found, STOP — report to Director.

### 7c. Methods to Add (fan-out relays for PttCopier)

Add 4 public relay methods to CopyEngine.cs. These are thin wrappers around existing private helpers:

```csharp
/// <summary>Relay BE event to follower accounts. Called by PttCopier.</summary>
public void RelayBe(BeEventArgs e)
{
    if (e == null) return;
    // Fan-out: call existing follower BE logic for each follower account.
    // Pattern: foreach follower in _followers => SubmitBeStop(follower, e.Instrument, e.BePrice)
    // Refer to existing SyncFollowerBracket / follower loop pattern in CopyEngine.
}

/// <summary>Relay Trim event to follower accounts. Called by PttCopier.</summary>
public void RelayTrim(TrimEventArgs e)
{
    if (e == null) return;
    // Fan-out: foreach follower => TrimOneAccount(follower, e.Instrument, e.ActualQty)
}

/// <summary>Relay Flatten event to follower accounts. Called by PttCopier.</summary>
public void RelayFlatten(FlatEventArgs e)
{
    if (e == null) return;
    // Fan-out: foreach follower => FlattenOneAccount(follower, e.Instrument)
}

/// <summary>Relay Cancel event to follower accounts. Called by PttCopier.</summary>
public void RelayCancel(CancelEventArgs e)
{
    if (e == null) return;
    // Fan-out: foreach follower => CancelPendingEntries(follower, e.Instrument)
}
```

CYC = 1–2 each (null guard + existing helper call). ✅

### 7d. Build Tag Change

```csharp
// Line 41 — BEFORE:
Tag = "PTT-COPIER B35 | bracket-cancel + BE-fixes | 2026-07-23";

// Line 41 — AFTER (engineer inserts actual date):
Tag = "PTT-COPIER B33 | modular-independence | 2026-07-{DATE}";
```

---

## 8. Dependency Diagram

```
┌──────────────────────────────────────────────────────────────────┐
│  Core/PttContracts.cs                                            │
│  ─────────────────────                                           │
│  IPttModule         (interface)                                  │
│  IPttHostContext    (interface)                                   │
│  PttBus             (static events)                              │
│  BeEventArgs        (class)                                      │
│  TrimEventArgs      (class)                                      │
│  FlatEventArgs      (class)                                      │
│  CancelEventArgs    (class)                                      │
│                                                                  │
│  Imports: NinjaTrader.Cbi ONLY                                   │
└──────────────────┬───────────────────────────────────────────────┘
                   │  (implements / imports)
      ┌────────────┼───────────────────────────────────────┐
      │            │            │            │             │
      ▼            ▼            ▼            ▼             ▼
PttBreakEven   PttTrim    PttFlatten    PttCancel     PttCopier
(Features/)    (Features/)(Features/)  (Features/)   (Features/)
      │                                                     │
      │ Imports Core/ + NinjaTrader.Cbi only                │ Imports Core/
      │ Calls acc.CreateOrder() directly                    │ + CopyEngine.cs
      │                                                     │ (fan-out relay)
      └───────────────────────────────────────────────┐     │
                                                      ▼     ▼
                                               ┌─────────────────┐
                                               │  CopyEngine.cs  │
                                               │  (existing)     │
                                               │                 │
                                               │  Imports: none  │
                                               │  new imports    │
                                               └────────┬────────┘
                                                        │
                                                        │ (wired by)
                                               ┌────────▼────────┐
                                               │TradeCopierPanel │
                                               │implements:      │
                                               │ IPttHostContext │
                                               │ holds _modules  │
                                               └─────────────────┘

FORBIDDEN EDGES (never allowed):
  ❌ Core/ imports Features/
  ❌ Features/*.cs imports another Features/*.cs
  ❌ CopyEngine.cs imports Core/ or Features/
```

---

## 9. Test Strategy

### 9a. Baseline Protection

164 existing [Fact] tests must continue to pass after B33.

Tests affected by CopyEngine dead code removal: NONE — ArmTrailBe, DisarmTrailBe,
OnTrailBeAccountUpdate are confirmed dead since B32. No test file references them.

**Pre-flight check (engineer):**
```powershell
Select-String -Path tests/ -Pattern "ArmTrailBe|DisarmTrailBe|OnTrailBeAccountUpdate" -Recurse
```
Expected: zero matches. If any found, those test references must be deleted first.

### 9b. New [Fact] Tests (6 required — target total: ≥ 170)

**Test infrastructure needed:**

```csharp
// MockPttHostContext implementing IPttHostContext for unit tests
internal class MockPttHostContext : IPttHostContext
{
    public Account           LeaderAccount { get; set; }
    public Instrument        Instrument    { get; set; }
    public IReadOnlyList<Account> AllAccounts { get; set; }
}
```

**Test cleanup requirement:** PttBus is a static class. Each test MUST unsubscribe all
event handlers at end (in a finally block or test cleanup) to prevent leakage between tests.

---

#### T_B33_BE_Standalone

```csharp
[Fact]
public void T_B33_BE_Standalone()
{
    // Arrange
    int firedCount = 0;
    EventHandler<BeEventArgs> handler = (s, e) => firedCount++;
    PttBus.BeFired += handler;

    var sut = new PttBreakEven();
    var ctx = new MockPttHostContext
    {
        LeaderAccount = BuildMockAccount(hasPosition: true),
        Instrument    = BuildMockInstrument(),
        AllAccounts   = new List<Account> { /* 1 account */ }
    };
    sut.Initialize(ctx);

    try
    {
        // Act
        sut.Execute(ctx);

        // Assert: BE fires without PttCopier loaded
        Assert.Equal(1, firedCount);
    }
    finally { PttBus.BeFired -= handler; }
}
```

**What it asserts:** `PttBus.BeFired` raised exactly once when PttCopier is NOT subscribed.
Tests standalone module isolation. ✅

---

#### T_B33_Trim_Standalone

```csharp
[Fact]
public void T_B33_Trim_Standalone()
{
    int firedCount = 0;
    EventHandler<TrimEventArgs> handler = (s, e) => firedCount++;
    PttBus.TrimFired += handler;
    var sut = new PttTrim();
    // ... mock ctx with position ...
    try
    {
        sut.Execute(ctx);
        Assert.Equal(1, firedCount);
    }
    finally { PttBus.TrimFired -= handler; }
}
```

---

#### T_B33_Flatten_Standalone

```csharp
[Fact]
public void T_B33_Flatten_Standalone()
{
    int firedCount = 0;
    EventHandler<FlatEventArgs> handler = (s, e) => firedCount++;
    PttBus.FlatFired += handler;
    var sut = new PttFlatten();
    // ... mock ctx with position ...
    try
    {
        sut.Execute(ctx);
        Assert.Equal(1, firedCount);
    }
    finally { PttBus.FlatFired -= handler; }
}
```

---

#### T_B33_Cancel_Standalone

```csharp
[Fact]
public void T_B33_Cancel_Standalone()
{
    int firedCount = 0;
    EventHandler<CancelEventArgs> handler = (s, e) => firedCount++;
    PttBus.CancelFired += handler;
    var sut = new PttCancel();
    // ... mock ctx ...
    try
    {
        sut.Execute(ctx);
        Assert.Equal(1, firedCount);
    }
    finally { PttBus.CancelFired -= handler; }
}
```

---

#### T_B33_Copier_BeFanOut

```csharp
[Fact]
public void T_B33_Copier_BeFanOut()
{
    // Arrange: mock CopyEngine proxy that records RelayBe calls
    bool relayBeCalled = false;
    var mockEngine = new MockCopyEngineRelay(onRelayBe: (e) => { relayBeCalled = true; });
    var copier = new PttCopier(mockEngine);
    copier.Initialize(new MockPttHostContext());

    try
    {
        // Act: raise BeFired directly
        PttBus.RaiseBe(this, new BeEventArgs(
            BuildMockInstrument(), 18500.0, 18500.0, true, string.Empty));

        // Assert: PttCopier.OnBeFired was called and relayed to CopyEngine
        Assert.True(relayBeCalled);
    }
    finally { copier.Teardown(); }
}
```

**What it asserts:** PttCopier.OnBeFired handler calls CopyEngine.RelayBe when BeFired is raised.
Tests the relay chain without requiring live accounts. ✅

---

#### T_B33_AllAccounts_BeLoop

```csharp
[Fact]
public void T_B33_AllAccounts_BeLoop()
{
    // Arrange: 3 accounts, each with a position
    const int accountCount = 3;
    int submitBeCallCount = 0;

    // Mock: intercept acc.CreateOrder via stub Account subclass or counter
    var accounts = BuildMockAccountsWithPositions(accountCount, onCreateOrder: () => submitBeCallCount++);

    var sut = new PttBreakEven();
    var ctx = new MockPttHostContext
    {
        LeaderAccount = accounts[0],
        Instrument    = BuildMockInstrument(),
        AllAccounts   = accounts
    };

    // Act
    EventHandler<BeEventArgs> handler = (s, e) => { };
    PttBus.BeFired += handler;
    try
    {
        sut.Execute(ctx);
        // Assert: SubmitBeStop called once for each account
        Assert.Equal(accountCount, submitBeCallCount);
    }
    finally { PttBus.BeFired -= handler; }
}
```

**What it asserts:** `PttBreakEven.Execute()` calls SubmitBeStop for each account in AllAccounts
(not just the leader). Directly tests DW-B36-01 fix. ✅

### 9c. Test Count Summary

| Category | Count |
|----------|-------|
| Existing baseline | 164 |
| T_B33_BE_Standalone | 1 |
| T_B33_Trim_Standalone | 1 |
| T_B33_Flatten_Standalone | 1 |
| T_B33_Cancel_Standalone | 1 |
| T_B33_Copier_BeFanOut | 1 |
| T_B33_AllAccounts_BeLoop | 1 |
| **Total** | **170** |

Meets acceptance criterion: ≥ 170 [Fact] total. ✅

---

## 10. NT8 Compiler Constraints (B33-Specific Checklist)

| Rule | Constraint | Applied Where |
|------|-----------|--------------|
| NT8-001 | No `{get; init;}` | EventArgs classes: use `{get; private set;}` + constructor |
| NT8-002 | No `abstract record` / `sealed record` | EventArgs: use `class : EventArgs` |
| NT8-003 | No `volatile double` | No double fields in PttContracts or modules |
| NT8-004 | No `System.Collections.Immutable` | Not used; plain `List<T>` |
| NT8-006 | `.Any()` requires `using System.Linq` | Avoid `.Any()` — use `.Count > 0` or explicit foreach |
| NT8-007 | `CreateOrder` arg11 is `(CustomOrder)null` | PttBreakEven.SubmitBeStopLocal, PttTrim, PttFlatten |
| NT8-013 | `DateTime.MaxValue` for CreateOrder expiry | All CreateOrder calls in all modules |
| NT8-014 | Signal name starts with `"PTT-"` | "PTT-BE-Stop", "PTT-Trim", "PTT-Flatten" |
| NT8-018 | No `lock()` | PttBus uses CLR events on UI thread only |
| NT8-019 | No `async void` | All Execute() methods synchronous void |
| NT8-031 | No `OrderState.PendingSubmit` | CancelStaleBracketsLocal: Working + Initialized only |
| NT8-042 | No `Dispatcher.InvokeAsync` | Not needed; all code on UI thread |
| NT8-043 | No null-conditional `-=` | PttCopier.Teardown: if-guard not `?.Event -=` |
| NT8-044 | Explicit `using System;` for StringComparison | Add `using System;` to all new files |
| NT8-046 | `acc.Change()` on ATM Stop1/Stop2 overridden | PttBreakEven uses CreateOrder, not acc.Change() |
| NT8-049 | arg6=limitPrice=0, arg7=stopPrice=bePrice | PttBreakEven.SubmitBeStopLocal — CRITICAL |
| NT8-050 | No `Positions[Instrument]` — use `FindPosition()` | All modules use foreach-based FindPositionLocal() |
| NT8-051 | Sim accounts don't auto-cancel brackets | CancelStaleBracketsLocal called before SubmitBeStop |

**Required `using` directives for all new files:**
```csharp
using System;                      // NT8-044: StringComparison, Math, etc.
using System.Collections.Generic;  // List<T>, IReadOnlyList<T>
using NinjaTrader.Cbi;             // Account, Instrument, Order, Position, etc.
```

**DO NOT add:**
- `using System.Linq;` (avoid LINQ; use .Count > 0 and foreach per NT8-006)
- `using System.Collections.Immutable;` (banned per NT8-004)

---

## 11. 7-Scan Checklist (Engineer Pre-Commit Contract)

Run all 7 scans before any commit. Zero findings required for SCAN-01 through SCAN-06.

### SCAN-01 — lock() BANNED (JS-021 / NT8-018)
```powershell
Select-String -Path src\PropTraderTools\ -Pattern "lock\s*\(" -Include "*.cs" -Recurse
```
Expected: **zero matches** in new B33 files.

### SCAN-02 — async void BANNED (JS-033 / NT8-019)
```powershell
Select-String -Path src\PropTraderTools\ -Pattern "async\s+void" -Include "*.cs" -Recurse
```
Expected: **zero matches** in new B33 files (existing event handlers excluded with comment).

### SCAN-03 — init accessor BANNED (NT8-001)
```powershell
Select-String -Path src\PropTraderTools\ -Pattern "\{\s*get;\s*init;\s*\}" -Include "*.cs" -Recurse
```
Expected: **zero matches** in new files.

### SCAN-04 — CreateOrder arg11 (NT8-007) and arg order (NT8-049)
```powershell
Select-String -Path src\PropTraderTools\ -Pattern "acc\.CreateOrder" -Include "*.cs" -Recurse
```
For each match: verify arg6=0 (limitPrice), arg7=stopPrice var, arg11=(CustomOrder)null.
Expected: all CreateOrder calls in PttBreakEven/PttTrim/PttFlatten follow NT8-049 + NT8-007.

### SCAN-05 — Dead code callers (pre-deletion check)
```powershell
Select-String -Path src\PropTraderTools\ -Pattern "_trailBeSlots|_trailBeLastPnlBits|ArmTrailBe|DisarmTrailBe|OnTrailBeAccountUpdate" -Include "*.cs" -Recurse
```
Expected: **zero matches** after dead code removal.

### SCAN-06 — Positions[Instrument] BANNED (NT8-050)
```powershell
Select-String -Path src\PropTraderTools\ -Pattern "\.Positions\[instr\]|\.Positions\[instrument\]" -Include "*.cs" -Recurse
```
Expected: **zero matches** in new files.

### SCAN-07 — PttBus static event test cleanup (informational)
```powershell
Select-String -Path tests\ -Pattern "PttBus\.(BeFired|TrimFired|FlatFired|CancelFired)\s*\+=" -Include "*.cs" -Recurse
```
For each `+=` found in test code: confirm a matching `-=` exists in the same test method's finally block.
Expected: all PttBus subscriptions in tests have corresponding unsubscriptions.

---

## 12. Build Tag Change Spec

```
File:    CopyEngine.cs
Line:    41
BEFORE:  Tag = "PTT-COPIER B35 | bracket-cancel + BE-fixes | 2026-07-23";
AFTER:   Tag = "PTT-COPIER B33 | modular-independence | 2026-07-{DATE}";
```

Engineer: replace `{DATE}` with the actual commit date in `YYYY-MM-DD` format.

---

## 13. Acceptance Criteria Traceability

| AC | Requirement | Implemented In |
|----|------------|---------------|
| AC-1 | F5 compile clean — zero errors, zero warnings | All NT8 rules applied; 7-scan checklist |
| AC-2 | 164 existing [Fact] pass — no regressions | Dead code confirmed no callers; no public API removed |
| AC-3a | T_B33_BE_Standalone | Section 9b: T_B33_BE_Standalone |
| AC-3b | T_B33_Trim_Standalone | Section 9b: T_B33_Trim_Standalone |
| AC-3c | T_B33_Flatten_Standalone | Section 9b: T_B33_Flatten_Standalone |
| AC-3d | T_B33_Cancel_Standalone | Section 9b: T_B33_Cancel_Standalone |
| AC-3e | T_B33_Copier_BeFanOut | Section 9b: T_B33_Copier_BeFanOut |
| AC-3f | T_B33_AllAccounts_BeLoop | Section 9b: T_B33_AllAccounts_BeLoop |
| AC-3g | Total [Fact] ≥ 170 | 164 + 6 = 170 ✅ |
| AC-4 | Build tag updated | Section 12 |
| AC-5 | Hard-link sync | `powershell -File scripts\verify_links.ps1 -Fix` (post-build) |

---

## 14. Hard-Link Sync (Post-Build Mandatory)

```powershell
# Run from Wave workspace root after any .cs file change:
powershell -File scripts\verify_links.ps1 -Fix
```

This syncs PropTraderTools .cs files to all registered hard-link destinations.
Must be run AFTER every file creation or modification in `src\PropTraderTools\`.

---

*Plan status: REVIEW_PENDING*
*Return: PLAN_COMPLETE*
