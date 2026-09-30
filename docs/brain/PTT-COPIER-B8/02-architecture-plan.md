# PTT-COPIER-B8 Architecture Plan
**Status**: REVIEW_PASS
**Produced by**: PTT Architect (Phase 2)  
**Date**: 2026-07-08  
**Baseline**: B7 FINAL_PASS — 5 files, 27 passing [Fact] tests, all 7 scans green

---

## Section 1: B8 Scope Decision

### Deferred Items from B7 (Source: `docs/brain/PTT-COPIER-B7/06-deferred-backlog.md`)

| ID | Item | Priority | B8 Decision | Rationale |
|----|------|----------|-------------|-----------|
| DW-B7-01 | Per-account qty multiplier (1x/2x/3x) | P2 | **IN SCOPE** | Pure data model + UI extension; no NT8 lifecycle dependency. Self-contained change to CopyRule struct + CopyRuleDto + engine API + both surfaces. |
| DW-B7-02 | ATR dynamic sizing engine (AtrSizingEngine.cs, MarketData subscription) | P1 | **DEFERRED to B9** | Architectural incompatibility: `MarketData.Subscribe` and bar data access are only available on `NinjaScriptBase` subclasses (Strategy, Indicator). `AddOnBase` does NOT inherit from `NinjaScriptBase` — it has no `OnBarUpdate`, no `Data[]` series, no `MarketData` property. AtrSizingEngine cannot be built as a standalone class inside an AddOn; it requires an embedded Indicator host. B9 scope: design AtrSizingEngine.cs as a detached Indicator managed by the AddOn. B7 backlog itself listed target as "B8/B9", confirming this carry-forward was anticipated. |
| DW-B7-03 | FollowerAtmMode behavioral wiring (SendCopy switch + Window UI dropdown) | P2 | **IN SCOPE** | The `FollowerAtmMode` sealed record hierarchy was already scaffolded in B7. `FollowerAtmTemplates` field already exists on `CopyRule`. B8 wires the behavior: `SendCopy` dispatch switch + Panel per-follower dropdown + Window per-rule dropdown. |

### Spec Roadmap Items Tagged B8 (Source: `specs/002-trade-copier-spec.html`, Full Roadmap Priority Table)

The spec's Full Roadmap Priority Table assigns three additional items to Block B8. Each is explicitly adjudicated below:

| ID | Item | Priority | B8 Decision | Rationale |
|----|------|----------|-------------|-----------|
| SPEC-B8-04 | Click trader (chart-click entry) — spec: "Green border = armed. Click chart → limit order + copy." | P1 | **DEFERRED to B9** | Requires `ChartControl.MouseDown` event wiring on the NT8 chart surface and a new "armed" state indicator drawn on the chart — both require the AddOn to inject a rendering host into the active `ChartControl`. This is outside the AddOn entry-point scope for B8. The per-follower model being established by DW-B7-01 and DW-B7-03 in B8 is a prerequisite for click-trader's per-follower quantity and ATM mode dispatch. B9 scope: inject a lightweight `NinjaScript`-hosted overlay into the ChartControl and wire `MouseDown` → `TradeCopierEngine.DispatchCopy`. |
| SPEC-B8-05 | ATR box visualization on chart — spec: "Draw ATR stop/target zone around pending order. Green/red." | P2 | **DEFERRED to B9** | Directly depends on `AtrSizingEngine` (DW-B7-02) which is itself deferred to B9 due to the `MarketData.Subscribe` / `AddOnBase` incompatibility documented above. Visualization of ATR stop/target zones requires rolling ATR(14) values from `AtrSizingEngine` — cannot be rendered without the engine. No standalone chart-drawing work is valuable without ATR data to display. |
| SPEC-B8-06 | Full mirror mode (Mode 2) — spec: "Auto-copy all master order modifications to followers." | P2 | **DEFERRED to B9** | Mode 2 requires extending `_orderMap` to track the full lifecycle of every master order modification (`OnOrderUpdate` for `Change`, `Move`, `PartialFill`) and dispatching matching mutations to follower orders via `Account.Change` or re-submit. This is significant engine complexity — materially larger than DW-B7-01 + DW-B7-03 combined. The `FollowerAtmMode.Named` ATM wiring landed in B8 is a prerequisite (Mode 2 must respect per-follower ATM state). B9 scope: extend `_orderMap` with modification-event relay, add `MirrorOrderUpdate` method to engine. |

### B8 Feature Summary

**B8 delivers 2 of 3 deferred items:**  
1. **DW-B7-01**: Per-account quantity multiplier — `int[] FollowerMultipliers` on `CopyRule`  
2. **DW-B7-03**: FollowerAtmMode behavior — `SendCopy` mode dispatch + UI selectors

**DW-B7-02 rolls to B9** with documented rationale (MarketData/AddOnBase incompatibility).

---

## Section 2: Architecture Overview

### How B8 Features Fit the Existing 5-File Structure

```
CopyEngine.cs  (pure logic singleton)
│
├─ CopyRule struct
│    ├─ [EXISTING] FollowerAccounts Account[]
│    ├─ [NEW B8]   FollowerMultipliers int[]   -- parallel to FollowerAccounts, default all 1s
│    ├─ [EXISTING] FollowerAtmTemplates ImmutableDictionary<string, FollowerAtmMode>
│    └─ Factory: CopyRule.Create(..., multipliers, atmTemplates)
│
├─ CopyRuleDto (persistence DTO)
│    ├─ [EXISTING] FollowerAccountNames string[]
│    ├─ [NEW B8]   FollowerMultipliers int[]   -- serialized parallel to account names
│    └─ [NEW B8]   FollowerAtmModeNames string[]  -- "Inherit" | "Market" | "Named:XXX"
│
├─ Engine API (public/internal)
│    ├─ [NEW B8] AddRule overload: (string, Account, Account[], int[], ImmutableDictionary<...>)
│    ├─ [NEW B8] SetFollowerMultiplier(string instrument, int followerIndex, int multiplier)
│    └─ [NEW B8] SetAtmMode(string instrument, string followerAccountName, FollowerAtmMode mode)
│
├─ Private helpers (new)
│    ├─ GetMultiplier(CopyRule, int index) -> int       (CYC=3, never returns <1)
│    ├─ GetAtmMode(CopyRule, string accountName) -> FollowerAtmMode  (CYC=2, never null)
│    └─ ParseAtmModeName(string) -> FollowerAtmMode     (CYC=3, for deserialization)
│    └─ AtmModeToString(FollowerAtmMode) -> string      (CYC=3, for serialization)
│
├─ Hot path (modified)
│    ├─ DispatchCopy: index-tracking loop + GetMultiplier + GetAtmMode per follower  (CYC=8)
│    └─ SendCopy: +mode parameter, mode dispatch (if/else if, no throw)  (CYC=5)
│
└─ Persistence (modified)
     ├─ RuleToDto: write FollowerMultipliers + FollowerAtmModeNames
     └─ DtoToRule: read with null-safety (backward compat with B6/B7 XML files)

TradeCopierPanel.cs  (ChartTrader injection surface)
├─ FollowerItem nested class
│    ├─ [NEW B8] Multiplier int { get; set; } = 1
│    └─ [NEW B8] AtmModeName string { get; set; } = "Inherit"
│
├─ BuildCheckItemTemplate (modified)
│    Row: [account name] [daily P&L] [mult TextBox w=30] [ATM ComboBox w=80] [checkmark]
│    Handlers: OnFollowerMultiplierChanged, OnFollowerAtmModeChanged
│
└─ OnApplyRule (modified)
     reads Multiplier + AtmModeName per selected follower
     builds int[] multipliers and ImmutableDictionary<string, FollowerAtmMode> atmMap
     calls engine.AddRule(instrument, master, followers, multipliers, atmMap)

TradeCopierWindow.cs  (standalone window surface)
├─ BuildRuleRow (modified)
│    Add Col 9: ATM mode ComboBox (one per rule, applies to all followers uniformly)
│    Items: {"Inherit", "Market", "Named"}
│
└─ OnRowApply (modified)
     reads ATM ComboBox + builds uniform atmMap for all followers in rule
     calls engine.AddRule with multipliers=null (all default to 1x) + atmMap
```

### Design Decisions

**Per-follower vs per-rule ATM mode:**
- **Panel surface** (`TradeCopierPanel.cs`): per-follower ATM mode — each follower in the dropdown row gets its own ATM mode selector. This matches the competitor intelligence (Mode column per row).
- **Window surface** (`TradeCopierWindow.cs`): single ATM mode per rule — simpler UI consistent with the Window's role as a "set-and-forget" surface.

**Multiplier storage:**
- `int[] FollowerMultipliers` is stored parallel to `Account[] FollowerAccounts` (same index). This avoids a second dictionary and keeps `GetMultiplier(rule, index)` O(1).
- Panel surface collects multipliers per follower at Apply time.
- Window surface: multipliers default to `null` / all 1s (no per-follower multiplier UI on Window in B8).

**FollowerAtmMode.Named semantics:**
- When mode is `Named("TemplateName")`, `SendCopy` passes `TemplateName` as the `signalName` argument to `Account.CreateOrder`. NT8's ATM auto-attach mechanism matches signal names to configured ATM profiles. The user must pre-configure NT8's ATM auto-attach for the follower account.
- This is the correct AddOn-level approach — NT8 does not expose `AtmStrategy.Create` from outside NinjaScriptBase.

---

## Section 3: File-by-File Change Plan

### 3.1 `CopyEngine.cs` (~534 lines → ~620 lines)

**CopyRule struct — new fields and factory:**

```csharp
// NEW: parallel to FollowerAccounts[]
internal readonly int[] FollowerMultipliers;   // null = all 1s

// Updated private constructor (adds multipliers parameter):
private CopyRule(string instrument, Account master, Account[] followers, bool enabled,
                 int[] multipliers, ImmutableDictionary<string, FollowerAtmMode> atmTemplates)

// Updated factory (new optional params, backward compatible):
internal static CopyRule Create(
    string instrument,
    Account master,
    Account[] followers,
    bool enabled = true,
    int[] multipliers = null,
    ImmutableDictionary<string, FollowerAtmMode> atmTemplates = null)
```

**CopyRuleDto — new serialization fields:**

```csharp
public int[]    FollowerMultipliers    { get; set; } = new int[0];
public string[] FollowerAtmModeNames   { get; set; } = new string[0];
```

**Engine API additions:**

```csharp
// New overload (original 3-arg overload is preserved unchanged):
internal void AddRule(string instrument, Account master, Account[] followers,
                      int[] multipliers, ImmutableDictionary<string, FollowerAtmMode> atmMap)

// New API:
internal void SetFollowerMultiplier(string instrument, int followerIndex, int multiplier)
internal void SetAtmMode(string instrument, string followerAccountName, FollowerAtmMode mode)
```

**New private helpers:**

```csharp
// CYC=3: null guard + bounds guard + clamp
private static int GetMultiplier(CopyRule rule, int followerIndex)

// CYC=2: TryGetValue + fallback to Inherit
private static FollowerAtmMode GetAtmMode(CopyRule rule, string accountName)

// CYC=3: null/empty → Inherit; StartsWith("Named:") → Named; "Market" → Market; else Inherit
private static FollowerAtmMode ParseAtmModeName(string name)

// CYC=3: is Inherit; is Market; is Named n
private static string AtmModeToString(FollowerAtmMode mode)
```

**DispatchCopy (modified — index-tracking loop, CYC=8 at limit):**

```csharp
// Replace: foreach (var acc in rule.FollowerAccounts)
// With index-tracking loop:
int idx = 0;
foreach (var acc in rule.FollowerAccounts)
{
    if (acc == null) { idx++; continue; }
    if (!PassesDailyCapCheck(acc)) { idx++; continue; }
    int mult = GetMultiplier(rule, idx);
    var scaledSignal = CopySignal.Create(
        signal.Action, signal.Type,
        signal.Quantity * mult,
        signal.LimitPrice, signal.OrderId);
    var mode = GetAtmMode(rule, acc.Name);
    SendCopy(acc, order.Instrument, in scaledSignal, mode);
    idx++;
}
```

**SendCopy (modified — adds mode parameter, CYC≈5):**

```csharp
private bool SendCopy(Account follower, Instrument instrument, in CopySignal signal, FollowerAtmMode mode)
{
    OrderType orderType    = signal.Type;
    double    limitPrice   = signal.LimitPrice;
    string    signalName   = "PTT-Copy";

    if (mode is FollowerAtmMode.Market)
    {
        orderType  = OrderType.Market;
        limitPrice = 0;
    }
    else if (mode is FollowerAtmMode.Named named)
    {
        signalName = named.TemplateName;
    }
    // FollowerAtmMode.Inherit: use original signal values unchanged

    try
    {
        follower.CreateOrder(instrument, signal.Action, orderType, OrderEntry.Manual,
            TimeInForce.Day, signal.Quantity, limitPrice, 0, null, signalName,
            DateTime.MaxValue, null);
        return true;
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke("PTT-Copy error: " + ex.Message);
        return false;
    }
}
```

**RuleToDto (modified):**

```csharp
// ADD after followerNames loop:
var mults = new int[rule.FollowerAccounts.Length];
for (int i = 0; i < rule.FollowerAccounts.Length; i++)
    mults[i] = (rule.FollowerMultipliers != null && i < rule.FollowerMultipliers.Length)
               ? rule.FollowerMultipliers[i] : 1;

var atmNames = new string[rule.FollowerAccounts.Length];
for (int i = 0; i < rule.FollowerAccounts.Length; i++)
{
    string accName = rule.FollowerAccounts[i] != null ? rule.FollowerAccounts[i].Name : string.Empty;
    FollowerAtmMode m;
    atmNames[i] = rule.FollowerAtmTemplates.TryGetValue(accName, out m)
                  ? AtmModeToString(m) : "Inherit";
}

return new CopyRuleDto
{
    // ... existing fields ...
    FollowerMultipliers  = mults,
    FollowerAtmModeNames = atmNames,
};
```

**DtoToRule (modified — null-safe for backward compat):**

```csharp
// AFTER resolving followers array, ADD:
int[] multipliers = null;
if (dto.FollowerMultipliers != null && dto.FollowerMultipliers.Length > 0)
    multipliers = dto.FollowerMultipliers;

var atmMap = ImmutableDictionary<string, FollowerAtmMode>.Empty;
if (dto.FollowerAtmModeNames != null)
{
    for (int i = 0; i < dto.FollowerAtmModeNames.Length && i < dto.FollowerAccountNames.Length; i++)
    {
        string accName = dto.FollowerAccountNames[i];
        if (!string.IsNullOrEmpty(accName))
            atmMap = atmMap.SetItem(accName, ParseAtmModeName(dto.FollowerAtmModeNames[i]));
    }
}
return CopyRule.Create(dto.InstrumentName, master, followers, dto.IsEnabled, multipliers, atmMap);
```

---

### 3.2 `TradeCopierPanel.cs` (~225 lines → ~285 lines)

**FollowerItem nested class (additions):**

```csharp
public int    Multiplier   { get; set; } = 1;          // default 1x
public string AtmModeName  { get; set; } = "Inherit";  // default Inherit
```

**BuildCheckItemTemplate (modified):**

Row layout after B8: `[account name] [daily P&L] [mult TextBox] [ATM ComboBox] [checkmark]`

```csharp
// Add AFTER pnlFactory, BEFORE chkFactory:

// [3] Multiplier TextBox (width=30, default "1")
var multFactory = new FrameworkElementFactory(typeof(TextBox));
multFactory.SetValue(TextBox.WidthProperty, 30.0);
multFactory.SetValue(TextBox.TextProperty, "1");
multFactory.SetValue(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center);
multFactory.SetValue(TextBox.MarginProperty, new Thickness(0, 0, 4, 0));
multFactory.AddHandler(TextBox.TextChangedEvent,
    new TextChangedEventHandler(OnFollowerMultiplierChanged));

// [4] ATM Mode ComboBox (width=80)
var atmFactory = new FrameworkElementFactory(typeof(ComboBox));
atmFactory.SetValue(ComboBox.WidthProperty, 80.0);
atmFactory.SetValue(ComboBox.MarginProperty, new Thickness(0, 0, 4, 0));
// ItemsSource set in OnFollowerAtmLoaded handler after combobox loads
atmFactory.AddHandler(ComboBox.LoadedEvent,
    new RoutedEventHandler(OnFollowerAtmComboLoaded));
atmFactory.AddHandler(ComboBox.SelectionChangedEvent,
    new SelectionChangedEventHandler(OnFollowerAtmModeChanged));
```

**New event handlers in TradeCopierPanel:**

```csharp
private void OnFollowerMultiplierChanged(object sender, TextChangedEventArgs e)
// Finds FollowerItem via sender.DataContext, parses int, clamps to [1, 10], sets item.Multiplier

private void OnFollowerAtmComboLoaded(object sender, RoutedEventArgs e)
// Populates ComboBox.ItemsSource = new[] {"Inherit","Market","Named"}, sets SelectedIndex=0

private void OnFollowerAtmModeChanged(object sender, SelectionChangedEventArgs e)
// Finds FollowerItem via sender.DataContext, sets item.AtmModeName = selected string
```

**OnApplyRule (modified):**

```csharp
// AFTER collecting followers list, ADD:
var multipliers = new int[followers.Count];
var atmNames = new string[followers.Count];
for (int i = 0; i < followers.Count; i++)
{
    // find matching FollowerItem
    foreach (var item in _followerItems)
    {
        if (item.Account == followers[i])
        {
            multipliers[i] = item.Multiplier > 0 ? item.Multiplier : 1;
            atmNames[i]    = item.AtmModeName ?? "Inherit";
            break;
        }
    }
}

// Build ImmutableDictionary<string, FollowerAtmMode>:
var atmMap = ImmutableDictionary<string, FollowerAtmMode>.Empty;
for (int i = 0; i < followers.Count; i++)
    atmMap = atmMap.SetItem(followers[i].Name,
        ParseAtmModeNameLocal(atmNames[i]));

_engine.AddRule(_instrument.FullName, _leaderAccount, followers.ToArray(), multipliers, atmMap);
```

Note: `ParseAtmModeNameLocal` is a private static helper on TradeCopierPanel that mirrors `CopyEngine.ParseAtmModeName` — keeps the Panel self-contained without exposing CopyEngine internals.

---

### 3.3 `TradeCopierWindow.cs` (~392 lines → ~430 lines)

**BuildRuleRow (modified — adds Col 9: ATM mode):**

New column definition added after the BE cluster (Col 8):
```csharp
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Col 9: ATM mode
```

ATM ComboBox in Col 9:
```csharp
var atmCb = new ComboBox
{
    ItemsSource   = new[] { "Inherit", "Market", "Named" },
    SelectedIndex = 0,
    Width         = 80,
    Margin        = new Thickness(2)
};
Grid.SetColumn(atmCb, 9);
grid.Children.Add(atmCb);
```

**BuildDynamicRuleRow (same modification).**

**OnRowApply (modified):**

```csharp
// Extend tag array to include atmCb:
applyBtn.Tag = new object[] { instrumentName, leaderCb, followerLb, atmCb };

// In OnRowApply handler, ADD after building followers list:
var atmCbTag = tag.Length > 3 ? tag[3] as ComboBox : null;
string atmModeName = atmCbTag?.SelectedItem as string ?? "Inherit";

var atmMap = ImmutableDictionary<string, FollowerAtmMode>.Empty;
foreach (var acc in followers)
{
    var mode = ParseAtmModeNameWindow(atmModeName);  // private static helper
    atmMap = atmMap.SetItem(acc.Name, mode);
}
_engine.AddRule(name, leader, followers.ToArray(), null, atmMap);
// multipliers=null means all followers get 1x (Window surface doesn't expose per-follower multiplier)
```

**`ParseAtmModeNameWindow` private static helper** — mirrors Panel version. CYC=3.

---

### 3.4 `CopyEngineTests.cs` (~345 lines → ~480 lines)

Ten new [Fact] tests added:

```csharp
[Fact] T-B8-01: AddRule_WithMultipliers_StoresCorrectMultipliers
[Fact] T-B8-02: GetMultiplier_OutOfRangeIndex_ReturnsOne
[Fact] T-B8-03: GetMultiplier_ValidIndex_ReturnsStoredValue
[Fact] T-B8-04: GetMultiplier_NullMultiplierArray_ReturnsOne
[Fact] T-B8-05: FollowerAtmMode_AllVariants_NoException
[Fact] T-B8-06: GetAtmMode_NoEntry_ReturnsInherit
[Fact] T-B8-07: GetAtmMode_WithNamedEntry_ReturnsNamedMode
[Fact] T-B8-08: SaveLoad_RoundTrip_PreservesMultipliers
[Fact] T-B8-09: SaveLoad_RoundTrip_PreservesAtmModeNames
[Fact] T-B8-10: DtoToRule_NullMultipliers_DoesNotThrow
```

---

### 3.5 `TradeCopierAddOn.cs` — NO CHANGES

B8 features are pure engine/UI additions. The AddOn injection mechanism is unchanged.

---

## Section 4: New Files

**None.** DW-B7-02 (AtrSizingEngine.cs) deferred to B9. All B8 work fits within the existing 5-file structure.

---

## Section 5: Integration Constraints

### NT8 Lifecycle Rules (observed in this plan)

| Constraint | B8 Status |
|-----------|-----------|
| No `async/await` in NT8 lifecycle methods | ✅ No new lifecycle methods |
| Off-thread UI → `Dispatcher.InvokeAsync` | ✅ No new off-thread UI calls; existing pattern unchanged |
| `Account.All` only in `Loaded` handlers | ✅ No new Account.All access; `DtoToRule` called from `LoadRules()` from `OnLoaded` |
| `TradeCopierWindow` must NOT be sealed | ✅ No `sealed` modifier added |
| `MarketData.Subscribe` only in `Realtime` state | ✅ Not used (DW-B7-02 deferred) |
| No `async void` except unreplaceable NT8 handlers | ✅ No new async void |

### CopyRule Struct Immutability

`CopyRule` is a `private readonly struct`. The B8 `int[] FollowerMultipliers` field is `readonly`, populated only through the factory method. Arrays are reference types — the struct holds a reference, but the factory does NOT expose a setter. Mutation of the array after creation is possible in theory but no caller does so. This is an accepted pattern matching the existing `Account[] FollowerAccounts` field.

The `ConcurrentBag<CopyRule> _rules` field reassignment pattern (used by `SetRuleEnabled`, `SetFollowerMultiplier`, `SetAtmMode`) is pre-existing since B3. It is NOT atomic. All three write operations are called from the NT8 UI thread; `OnOrderUpdate` reads `_rules` from a background thread. This is a pre-existing design limitation carried forward — B8 does not introduce new races.

### ImmutableDictionary.SetItem (thread safety)

`ImmutableDictionary<string, FollowerAtmMode>.SetItem(key, value)` returns a NEW dictionary without mutating the existing one. The `FollowerAtmTemplates` field on `CopyRule` holds a reference that is replaced only when a new `CopyRule` is created via `SetAtmMode`. Thread safety is provided by the same ConcurrentBag rebuild pattern — not by the ImmutableDictionary itself.

### FollowerAtmMode.Named and NT8 ATM Auto-Attach

When `Named("TemplateName")` mode is active, `SendCopy` passes `TemplateName` as the `signalName` to `Account.CreateOrder`. For NT8 to automatically apply the named ATM template:
1. The follower account must have ATM Auto-Attach configured in NT8 Connections settings.
2. The ATM template name must exactly match the signal name.

This is a user-configuration requirement documented in the UI label. The AddOn cannot call `AtmStrategy.Create` from outside `NinjaScriptBase`.

---

## Section 6: JS Rule Constraints per Feature

### DW-B7-01 (Per-account qty multiplier)

| Rule | Constraint | B8 Compliance |
|------|-----------|---------------|
| JS-021 No lock() | `SetFollowerMultiplier` uses ConcurrentBag rebuild (no lock) | ✅ |
| JS-025 ConcurrentDict/Queue | `_rules` is `ConcurrentBag<CopyRule>` | ✅ |
| JS-003 Readonly struct | `CopyRule.FollowerMultipliers` is `readonly int[]` | ✅ |
| JS-008 Immutable data | `FollowerMultipliers` set only via factory | ✅ |
| JS-001 No throw in hot path | `GetMultiplier` returns int, never throws | ✅ |
| JS-002 No return null | `GetMultiplier` returns int (no null possible) | ✅ |
| SCAN-05 DateTime.UtcNow | No DateTime.Now introduced | ✅ |
| SCAN-07 No hex | No hex strings introduced | ✅ |

### DW-B7-03 (FollowerAtmMode behavioral wiring)

| Rule | Constraint | B8 Compliance |
|------|-----------|---------------|
| JS-021 No lock() | `SetAtmMode` uses ConcurrentBag rebuild (no lock) | ✅ |
| JS-003 Sealed hierarchy | `FollowerAtmMode` sealed record hierarchy (B7-scaffolded) | ✅ |
| JS-009 ImmutableDictionary | `FollowerAtmTemplates` is `ImmutableDictionary` | ✅ |
| JS-001 No throw in hot path | `GetAtmMode` and `SendCopy` dispatch: no throw | ✅ |
| JS-002 No return null | `GetAtmMode` returns `new FollowerAtmMode.Inherit()` as default | ✅ |
| SCAN-02 No throw new | mode dispatch uses `if/else if` — no throw in discard arm | ✅ |
| SCAN-04 No Dictionary< | `ImmutableDictionary` used, not `Dictionary<` | ✅ |
| SCAN-07 No hex | No hex color strings | ✅ |

### Serialization (both features)

| Rule | Constraint | B8 Compliance |
|------|-----------|---------------|
| Backward compat | `DtoToRule` handles null `FollowerMultipliers` and `FollowerAtmModeNames` | ✅ |
| No throw | `DtoToRule` null-guards all new fields before access | ✅ |
| No Dictionary< | `CopyRuleDto` uses `int[]` and `string[]` (not `Dictionary`) | ✅ |

---

## Section 7: Test Strategy

### Regression Protection (27 existing [Fact] tests)

All existing tests are protected by these API stability guarantees:
1. `AddRule(string, Account, Account[])` 3-arg overload is **preserved unchanged**
2. `CopyRule.Create` original signature preserved — new params are `optional` (default null)
3. `DispatchCopy` private method signature unchanged: `(Order order, CopyRule rule)` — 2 params — T-B7-01 reflection test continues to pass
4. `SendCopy` is private, called only from `DispatchCopy` — no test invokes it by reflection
5. All persistence tests use `overridePath` temp files — unaffected by DTO extensions
6. `SetRuleEnabled`, `SetEnabled`, `SetDailyCapFloor`, `Subscribe`, `Unsubscribe` unchanged

### New [Fact] Tests (10 total — target: 37 tests after B8)

| Test | Covers | Method(s) |
|------|--------|-----------|
| T-B8-01: `AddRule_WithMultipliers_StoresCorrectMultipliers` | DW-B7-01 storage | `AddRule` new overload + `GetField("_rules")` |
| T-B8-02: `GetMultiplier_OutOfRangeIndex_ReturnsOne` | DW-B7-01 bounds safety | `GetMultiplier` via reflection |
| T-B8-03: `GetMultiplier_ValidIndex_ReturnsStoredValue` | DW-B7-01 retrieval | `GetMultiplier` via reflection |
| T-B8-04: `GetMultiplier_NullMultiplierArray_ReturnsOne` | DW-B7-01 null safety | `GetMultiplier` via reflection |
| T-B8-05: `FollowerAtmMode_AllVariants_NoException` | DW-B7-03 type safety | `FollowerAtmMode` constructors |
| T-B8-06: `GetAtmMode_NoEntry_ReturnsInherit` | DW-B7-03 default | `GetAtmMode` via reflection |
| T-B8-07: `GetAtmMode_WithNamedEntry_ReturnsNamedMode` | DW-B7-03 retrieval | `GetAtmMode` via reflection |
| T-B8-08: `SaveLoad_RoundTrip_PreservesMultipliers` | Persistence + DW-B7-01 | `SaveRules` + `LoadRules` |
| T-B8-09: `SaveLoad_RoundTrip_PreservesAtmModeNames` | Persistence + DW-B7-03 | `SaveRules` + `LoadRules` |
| T-B8-10: `DtoToRule_NullMultipliers_DoesNotThrow` | Backward compat | `DtoToRule` via reflection |

### Test Data Helpers

All tests use the existing pattern:
- `_engine.SetEnabled(false)` to prevent hot path from running
- `GetField(name)` / `GetMethod(name)` reflection helpers already in `CopyEngineTests`
- `Record.Exception(() => ...)` for no-throw assertions
- Temp file paths via `Path.GetTempPath() + Guid.NewGuid()` for persistence tests

---

## Section 8: 7-Scan Checklist

| Scan | Command | B8 Impact | Projected Result |
|------|---------|-----------|-----------------|
| SCAN-01 | `grep -r "lock(" src/` | No lock() added anywhere in B8 | ✅ ZERO |
| SCAN-02 | `grep -r "throw new" src/` (dispatch methods) | `SendCopy` uses `if/else if` — no throw; `GetAtmMode` returns default Inherit — no throw | ✅ ZERO |
| SCAN-03 | `grep -r "return null" src/` | `GetMultiplier` returns int; `GetAtmMode` returns Inherit; `ParseAtmModeName` returns Inherit as default. No new `return null`. | ✅ ZERO |
| SCAN-04 | `grep -r "Dictionary<" src/` | No new `Dictionary<` — `ImmutableDictionary` only; `CopyRuleDto` uses `int[]` and `string[]` | ✅ ZERO |
| SCAN-05 | `grep -r "DateTime.Now" src/` | No `DateTime.Now` added | ✅ ZERO |
| SCAN-06 | `grep -r "async void" src/` | No new async methods | ✅ ZERO |
| SCAN-07 | `grep -rE "#[0-9A-Fa-f]{6}" src/` | No hex strings added; ATM mode names are plain strings; multiplier values are ints | ✅ ZERO |

### Pre-existing SCAN-03 Note

`CopyEngine.cs` contains `return null;` in `FindRule`, `FindPosition`, `FindFollowerBracketOrder`, `AllAccounts`, and `TradeCopierWindow.cs` / `TradeCopierPanel.cs` contain `return null` in `FindInstrument` etc. These are pre-existing and established — B8 adds no new `return null` occurrences.

---

## Section 9: Deferred Backlog Update (B8 → B9)

| ID | Item | Priority | Status After B8 | Target |
|----|------|----------|-----------------|--------|
| DW-B7-01 | Per-account qty multiplier | P2 | **CLOSED in B8** | — |
| DW-B7-02 | ATR dynamic sizing engine | P1 | **OPEN → B9** | B9 |
| DW-B7-03 | FollowerAtmMode behavioral wiring | P2 | **CLOSED in B8** | — |

**New B8 deferred items:**

| ID | Item | Priority | Target |
|----|------|----------|--------|
| DW-B8-01 | AtrSizingEngine.cs — design as detached Indicator managed by AddOn; MarketData.Subscribe in OnStateChange(Realtime); rolling ATR(14) calculation; ATR-scaled quantity applied to CopySignal | P1 | B9 |
| DW-B8-02 | Window surface per-follower multiplier — Window currently applies multiplier=1x to all followers; a per-row multiplier TextBox in BuildRuleRow is a UX improvement | P3 | B9 |
| DW-B8-03 | "Named" ATM mode inline template name input — currently the ComboBox has "Named" as a static item; user needs a way to type the template name in UI (TextBox that appears on "Named" selection) | P2 | B9 |
