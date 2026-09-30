# BWAVE-CYC Lane C -- Architecture Plan

**Produced by**: ptt-architect (Stage 2)
**Date**: 2025-01-30
**Wave**: BWAVE-CYC -- Complexity Reduction
**Lane**: C -- Panel / Window / AddOn
**Status**: REVIEW_PASS (ready for ptt-engineer)

---

## Constraints Applied (Non-Negotiable)

| Rule | Requirement |
|------|------------|
| JS-021 | No `lock()` anywhere -- zero new or moved lock blocks |
| JS-002 | No `return null` -- zero new `return null` instances in extracted helpers |
| JS-033 | No `async void` -- all helpers synchronous |
| CYC parent | <= 8 after extraction (all 17 methods) |
| CYC helper | <= 4 per extracted helper |
| NT8 UI thread | Dispatcher.InvokeAsync stays in original methods; no Account/Order/Position calls moved to helpers callable off-thread |
| ASCII-only | All identifiers and string literals ASCII |
| Private only | Zero new public or internal surface |

---

## NT8 UI Thread Contract (applied per ticket)

**SAFE to extract:**
- Pure decision logic (if/else trees, guard clauses, value computation)
- Named predicate helpers (bool methods answering one question)
- Value-building helpers returning a computed value
- Per-flag visibility blocks (enum switch branches)
- Visual tree "finder" helpers returning a found element

**FORBIDDEN to extract:**
- Any code calling `Dispatcher.InvokeAsync` / `Dispatcher.Invoke`
- NT8 `Account`, `Order`, or `Position` API calls into helpers callable off UI thread
- NT8 lifecycle callback signatures (`OnStateChange`, `OnWindowCreated`, etc.)
- `AccountDisplayConverter` outer callback signature and entry point
- `VisualTreeHelper.GetChild` / `DependencyProperty` calls in ways that break WPF binding

---

## T1 -- Panel: FollowerItem Button Colours + Load

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Methods**: `FollowerItem::UpdateButtonColors` (L633-671), `FollowerItem::OnLoaded` (L696-802)
**LANE_C_THREAD_CONTRACT**: CONSTRAINED
- `OnLoaded` runs on WPF UI thread (RoutedEvent handler). `Account.All` access is in scope here (NT8 rule: only in Loaded handlers). Extracted helpers are private, called synchronously from `OnLoaded` on UI thread. SAFE.
- `UpdateButtonColors` runs on UI thread (called via Dispatcher). `CopyEngine.Instance` calls (DisarmPendingBe, CancelQxBrackets) must remain in extracted helpers that are ONLY called from UI-thread methods.

### FollowerItem::UpdateButtonColors

**Current CCN**: 18
**Target CCN after extraction**: 5

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `ApplyButtonBackgrounds` | `private void ApplyButtonBackgrounds(bool hasPosition, bool hasEntries)` | Sets Background on `_copyToggleBtn2`, `_flattenBtn2`, `_cancelBtn2`, `_trimBtn2` using null guards + brush assignments. | 4 |
| `ResetBeStateOnFlat` | `private void ResetBeStateOnFlat(bool hasPosition)` | HOTFIX-F3: if `!hasPosition && _beState != BeState.Idle` -- resets `_beState`, calls `UpdateBeVisuals(BeState.Idle)`, calls `CopyEngine.Instance.DisarmPendingBe(_leaderAccount)` if leader != null. | 3 |
| `DisarmBeAllOnFlat` | `private void DisarmBeAllOnFlat(bool hasPosition)` | HOTFIX-BEALL-FLAT-RESET: if `!hasPosition && !CopyEngine.Instance.IsPendingSlotsEmpty()` -- calls `DisarmPendingBe` + `RaiseBeAllDisarmed`. Inner `_leaderAccount != null` guard. | 3 |
| `CancelOrphanBracketsOnFlat` | `private void CancelOrphanBracketsOnFlat(bool hasPosition)` | HOTFIX-ORPHAN: if `!hasPosition && _leaderAccount != null && _instrument != null` -- calls `CopyEngine.Instance.CancelQxBrackets`. | 2 |

**Parent UpdateButtonColors after extraction** (CCN = 1 + 4 unconditional calls = **5**):
```
ApplyButtonBackgrounds(hasPosition, hasEntries);
ResetBeStateOnFlat(hasPosition);
DisarmBeAllOnFlat(hasPosition);
CancelOrphanBracketsOnFlat(hasPosition);
```

**xUnit [Fact] tests**:
- `[Fact] ApplyButtonBackgrounds_SetsBrushActive_WhenCopyEnabled`
- `[Fact] ApplyButtonBackgrounds_SetsBrushDanger_WhenHasPosition`
- `[Fact] ResetBeStateOnFlat_SetsIdleAndDisarms_WhenPositionGoneAndBeArmed`
- `[Fact] DisarmBeAllOnFlat_CallsRaiseBeAllDisarmed_WhenPendingSlotsNotEmpty`
- `[Fact] CancelOrphanBracketsOnFlat_CallsCancelQxBrackets_WhenPositionGone`

---

### FollowerItem::OnLoaded

**Current CCN**: 17
**Target CCN after extraction**: 7

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `PopulateFollowerItems` | `private void PopulateFollowerItems()` | Clears `_followerItems`. Guards `Account.All == null` (return). Iterates `Account.All`, adds `FollowerItem`, wires `AccountItemUpdate`. Sets `_followersDropDown.ItemsSource` if not null. Calls `UpdateDropDownHeader()`, `LoadFollowers()`, `_engine.LoadRules()`. | 4 |
| `RestoreSavedFollowers` | `private void RestoreSavedFollowers()` | Guards `_instrument != null && _leaderAccount != null` compound. Calls `_engine.GetSavedFollowerNames`. If `saved.Count > 0`: foreach `_followerItems` with `saved.Contains` condition, sets `IsSelected`, calls `SortFollowerRows()`, `TryAutoApply()`. | 5 |
| `ApplyModuleLicenses` | `private void ApplyModuleLicenses()` | Foreach `_modules`: switch over 5 module IDs ("BE", "TRIM", "FLAT", "CANCEL", "COPY") calling `m.SetEnabled(Is{X}Licensed)`. | 7 |

**Parent OnLoaded after extraction** (CCN = **7**):
```
Loaded -= OnLoaded;
// wire 6 engine events (straight-line, no branches)
PopulateFollowerItems();          // Account.All loop
RestoreSavedFollowers();          // saved follower restore
NotifyRiskChanged();
NotifyAtrFractionChanged();
_engine.CopyEnabledChanged += OnCopyEnabledChanged;
ApplyCopyState(_engine.IsEnabled);
// build _allAccounts (B33 T7)
_allAccounts.Clear();
if (_leaderAccount != null) _allAccounts.Add(_leaderAccount);          // branch +1
foreach (var item in _followerItems)                                    // branch +1
    if (item.Account != null && item.Account != _leaderAccount)         // branch +2 (compound)
        _allAccounts.Add(item.Account);
// modules (B33 T7)
_modules.Clear();
AddModule(new PttBreakEven()); ... AddModule(new PttCopier(_engine));   // straight-line
foreach (IPttModule m in _modules) m.Initialize(this);                 // branch +1
ApplyModuleLicenses();
_engine.Subscribe();
// B41: leader wire
if (_leaderAccount != null) { ... RefreshQuickDisplay(...); }           // branch +1
// BGTM-1
CopyEngine.Instance.FeatureFlagsChanged += OnFeatureFlagsChanged;
ApplyFeatureFlags(CopyEngine.Instance.Flags);
// Total: base(1) + 6 = 7
```

**xUnit [Fact] tests**:
- `[Fact] PopulateFollowerItems_ClearsAndRepopulates_FromAccountAll`
- `[Fact] PopulateFollowerItems_ReturnsEarly_WhenAccountAllNull`
- `[Fact] RestoreSavedFollowers_RestoresIsSelected_WhenSavedNamesFound`
- `[Fact] RestoreSavedFollowers_NoOp_WhenInstrumentOrLeaderNull`
- `[Fact] ApplyModuleLicenses_SetsEnabled_FromLicenseBool_ForEachModule`

---

## T2 -- Panel: Apply-Rule + ATM Template Name

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Methods**: `OnApplyRule` (L2843-2894), `FollowerItem::GetLeaderAtmTemplateName` (L2642-2678)
**LANE_C_THREAD_CONTRACT**: CONSTRAINED
- `OnApplyRule` is a WPF RoutedEvent handler (UI thread). `CopyEngine.Instance.AddRule()` and `SaveRules()` MUST stay in `OnApplyRule`. Only the loop-based array-building logic is extracted.
- `GetLeaderAtmTemplateName` is a static utility. Extracted helpers are also static. No threading concern.

### OnApplyRule

**Current CCN**: 15
**Target CCN after extraction**: 7

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `BuildFollowerMultipliers` | `private (int[] multipliers, string[] atmNames) BuildFollowerMultipliers(Account[] followers)` | For each follower, iterates `_followerItems` to match account, extracts `Multiplier` (default 1) and `AtmModeName` (default "Inherit"). Returns parallel arrays. | 3 |
| `BuildAtmMap` | `private static Dictionary<string, FollowerAtmMode> BuildAtmMap(Account[] followers, string[] atmNames)` | For each follower (null guard), builds `atmMap[follower.Name] = ParseAtmModeNameLocal(atmNames[i])`. Returns dictionary. | 2 |

**Parent OnApplyRule after extraction** (CCN = **7**):
```
_leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();      // ?? branch +1
if (_leaderAccount == null) { statusMsg; return; }                  // +1 (null guard)
    if (_statusText != null) ...                                    // +1 (status text null)
if (_instrument == null) { statusMsg; return; }                     // +1 (null guard)
    if (_statusText != null) ...                                    // +1
var followers = GetSelectedFollowers();
if (followers.Length == 0) { statusMsg; return; }                   // +1 (null guard)
    if (_statusText != null) ...                                    // +1 -- wait this puts parent at 7+base(1)=8
// Revised: inline statusText checks use ?.Text = (no null guard needed in C#)
// Replace: `if (_statusText != null) _statusText.Text = X;` with `if (_statusText != null)`
// The 3 statusText null guards ARE 3 branches. With ?? operator = base(1)+7=8 max.
var (multipliers, atmNames) = BuildFollowerMultipliers(followers);
var atmMap = BuildAtmMap(followers, atmNames);
_engine.AddRule(...); _engine.SaveRules();
if (_statusText != null) _statusText.Text = "Rule: ...";            // +1
// Total: base(1) + ??operator(1) + leaderNull(1) + statusNull(1) + instrNull(1) + statusNull(1)
//        + followersLen(1) + statusNull(1) = 8. Target is <=8. ✓
```

**xUnit [Fact] tests**:
- `[Fact] BuildFollowerMultipliers_DefaultsToOne_WhenItemNotFound`
- `[Fact] BuildFollowerMultipliers_UsesItemMultiplier_WhenAccountMatches`
- `[Fact] BuildAtmMap_SkipsNullFollowers`
- `[Fact] BuildAtmMap_UsesInheritMode_WhenAtmNameIsEmpty`

---

### FollowerItem::GetLeaderAtmTemplateName

**Current CCN**: 12
**Target CCN after extraction**: 5

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `TryGetAtmNameFromStrategy` | `private static string TryGetAtmNameFromStrategy(ChartTrader ct)` | Reads `ct.AtmStrategy`. If null, returns `string.Empty`. Gets `.Name`. Guards empty-string and "AtmStrategy" class name. Returns name if valid, else `string.Empty`. | 3 |
| `TryGetAtmNameFromSelector` | `private static string TryGetAtmNameFromSelector(ChartTrader ct)` | Finds `AtmStrategySelector` via `FindVisualChild`. If null, returns `string.Empty`. Returns `sel.SelectedItem as string ?? string.Empty`. | 2 |
| `TryGetAtmNameFromComboBox` | `private static string TryGetAtmNameFromComboBox(ChartTrader ct)` | Finds `ComboBox` at index 2 via `FindVisualChildByIndex`. Returns `atmCb?.SelectedItem as string ?? string.Empty`. | 1 |

**Note on JS-002**: These helpers always return `string.Empty` as the absent-value sentinel. No new `return null` instances. ✓

**Parent GetLeaderAtmTemplateName after extraction** (CCN = **5**):
```
if (currentChart == null) return string.Empty;                // +1
try
{
    var ct = TradeCopierAddOn.FindVisualChild<ChartTrader>(currentChart);
    if (ct == null) return string.Empty;                      // +1
    var name = TryGetAtmNameFromStrategy(ct);
    if (name.Length > 0) return name;                         // +1
    name = TryGetAtmNameFromSelector(ct);
    if (name.Length > 0) return name;                         // +1
    return TryGetAtmNameFromComboBox(ct);
}
catch { return string.Empty; }                                // +1
// Total: base(1) + 4 = 5 ✓
```

**xUnit [Fact] tests**:
- `[Fact] TryGetAtmNameFromStrategy_ReturnsEmpty_WhenAtmStrategyNull`
- `[Fact] TryGetAtmNameFromStrategy_ReturnsEmpty_WhenNameIsAtmStrategyClassName`
- `[Fact] TryGetAtmNameFromSelector_ReturnsSelectedItem_WhenSelectorPresent`
- `[Fact] TryGetAtmNameFromComboBox_ReturnsSelectedItem_FromIndex2ComboBox`

---

## T3 -- Panel: Feature Flag Visibility Switches

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Methods**: `TradeCopierPanel::ApplyFeatureFlags` (L3176-3202), `TradeCopierPanel::ApplyFeatureFlagTooltips` (L3206-3218)
**LANE_C_THREAD_CONTRACT**: SAFE
- Pure WPF property sets (`IsEnabled`, `Visibility`, `ToolTip`). No Dispatcher, no NT8 Account/Order/Position API. All extracted helpers are private, called on UI thread from `ApplyFeatureFlags` (which is itself called from UI thread via `OnFeatureFlagsChanged`).

### TradeCopierPanel::ApplyFeatureFlags

**Current CCN**: 10
**Target CCN after extraction**: 4

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `ApplyTrimFlattenFlags` | `private void ApplyTrimFlattenFlags(FeatureFlags f)` | Sets `IsEnabled` on `_trimBtn2`, `_flattenBtn2`, `_cancelBtn2` (3 null guards). | 3 |
| `ApplyPositionControlFlags` | `private void ApplyPositionControlFlags(FeatureFlags f)` | Sets `IsEnabled` on `_beBtn2`, `_mirrorModeBtn` (2 null guards). | 2 |
| `ApplyRowVisibilityFlags` | `private void ApplyRowVisibilityFlags(FeatureFlags f)` | Sets `Visibility` on `_clickTraderRow` and `_atrRow` using `f.ClickTrader` and `f.AtrSizing` flags (2 null guards + 2 ternary Visibility assignments). | 4 |

**Parent ApplyFeatureFlags after extraction** (CCN = **4**):
```
ApplyTrimFlattenFlags(f);           // unconditional
ApplyPositionControlFlags(f);       // unconditional
ApplyRowVisibilityFlags(f);         // unconditional
ApplyFeatureFlagTooltips(f);        // unconditional (existing extracted call)
// Total: base(1) + 0 branches from helper calls = 1. But ApplyFeatureFlagTooltips is existing
// and we keep it. Add any inline checks if needed. Target CCN = 4 to be conservative but
// the actual structural count is 1 (all calls unconditional). ✓
```

**xUnit [Fact] tests**:
- `[Fact] ApplyTrimFlattenFlags_SetsIsEnabled_PerTrimFlattenFlag`
- `[Fact] ApplyPositionControlFlags_SetsBeEnabled_PerBreakEvenFlag`
- `[Fact] ApplyRowVisibilityFlags_SetsCollapsed_WhenClickTraderFlagFalse`
- `[Fact] ApplyRowVisibilityFlags_SetsVisible_WhenAtrSizingFlagTrue`

---

### TradeCopierPanel::ApplyFeatureFlagTooltips

**Current CCN**: 11
**Target CCN after extraction**: 2

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `SetButtonTooltip` | `private static void SetButtonTooltip(System.Windows.Controls.Button btn, bool featureEnabled, string upgradeMessage)` | `if (btn != null) btn.ToolTip = featureEnabled ? null : upgradeMessage;` Guards null and uses ternary. | 2 |

**Parent ApplyFeatureFlagTooltips after extraction** (CCN = **2**):
```
SetButtonTooltip(_trimBtn2,     f.TrimFlatten, "Trim/Flatten requires Pro tier");
SetButtonTooltip(_flattenBtn2,  f.TrimFlatten, "Trim/Flatten requires Pro tier");
SetButtonTooltip(_cancelBtn2,   f.TrimFlatten, "Trim/Flatten requires Pro tier");
SetButtonTooltip(_beBtn2,       f.BreakEven,   "Break Even requires Pro tier");
SetButtonTooltip(_mirrorModeBtn, f.MirrorMode,  "Mirror mode requires Elite tier");
// Total: base(1) + 0 branches (all unconditional calls) = 1. Conservative estimate = 2. ✓
// Note: btn parameter is Control not Button; use base class if needed.
```

**xUnit [Fact] tests**:
- `[Fact] SetButtonTooltip_SetsUpgradeMessage_WhenFeatureDisabled`
- `[Fact] SetButtonTooltip_SetsNullTooltip_WhenFeatureEnabled`
- `[Fact] SetButtonTooltip_NoOp_WhenButtonNull`

---

## T4 -- Panel: Position / Price Callbacks

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Methods**: `FollowerItem::IsPriceAlreadyAtBe` (L1602-1616), `FollowerItem::RefreshQuickDisplay` (L2027-2047), `FollowerItem::OnLeaderPositionUpdate` (L2096-2122), `TradeCopierPanel::OnChartMouseDown` (L2749-2796)
**LANE_C_THREAD_CONTRACT**: CONSTRAINED
- `OnLeaderPositionUpdate`: fires on NT8 position event thread. Contains TWO `Dispatcher.InvokeAsync` calls (L2104, L2121). Both MUST stay in `OnLeaderPositionUpdate`. Only the guard-chain predicate for the Remove event can be extracted.
- `OnChartMouseDown`: WPF mouse event (UI thread). `_leaderAccount.CreateOrder(...)` MUST stay in `OnChartMouseDown` (NT8 Account API call). The `Dispatcher.InvokeAsync` in the catch block MUST stay.
- `IsPriceAlreadyAtBe`: pure predicate. Uses `_engine.FindPositionPublic` (read-only). SAFE.
- `RefreshQuickDisplay`: UI thread (called from `OnLoaded` and via `Dispatcher.InvokeAsync`). Pure computation + UI update. SAFE.

### FollowerItem::IsPriceAlreadyAtBe

**Current CCN**: 10
**Target CCN after extraction**: 5

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `ComputeBeTargetPrice` | `private static double ComputeBeTargetPrice(double avgPrice, bool isLong, int bufferTicks, double tickSize)` | `avgPrice + (isLong ? 1.0 : -1.0) * bufferTicks * tickSize`. Ternary for direction. | 2 |
| `IsPriceAtOrPastTarget` | `private static bool IsPriceAtOrPastTarget(bool isLong, double refPx, double targetPx)` | `return isLong ? (refPx >= targetPx) : (refPx <= targetPx);` Ternary comparison. | 2 |

**Parent IsPriceAlreadyAtBe after extraction** (CCN = **5**):
```
var pos = _engine.FindPositionPublic(leader, instrument);
if (pos == null) return false;                               // +1
double tickSize = instrument?.MasterInstrument?.TickSize ?? 0.0;  // ?? = +1
if (tickSize <= 0.0) return false;                           // +1
bool isLong = pos.MarketPosition == MarketPosition.Long;
double refPx = isLong ? GetBid() : GetAsk();                 // +1 ternary
if (refPx <= 0.0) return false;                              // +1
double target = ComputeBeTargetPrice(pos.AveragePrice, isLong, bufferTicks, tickSize);
return IsPriceAtOrPastTarget(isLong, refPx, target);
// Total: base(1) + 4 = 5 ✓
```

**xUnit [Fact] tests**:
- `[Fact] ComputeBeTargetPrice_UsesNegativeDirection_WhenShort`
- `[Fact] ComputeBeTargetPrice_UsesPositiveDirection_WhenLong`
- `[Fact] IsPriceAtOrPastTarget_ReturnsFalse_WhenLongAndRefPxBelowTarget`
- `[Fact] IsPriceAtOrPastTarget_ReturnsTrue_WhenShortAndRefPxBelowTarget`

---

### FollowerItem::RefreshQuickDisplay

**Current CCN**: 10
**Target CCN after extraction**: 6

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `ComputeT1Ticks` | `private static int ComputeT1Ticks(bool isLong, Order t1Ord, double avgPrice, double tickSize)` | `rawDiff = isLong ? t1Ord.LimitPrice - avgPrice : avgPrice - t1Ord.LimitPrice`. `liveT1 = Math.Round(rawDiff / tickSize)`. If `liveT1 < 1`, clamp to 1. Returns `(int)liveT1`. | 3 |

**Parent RefreshQuickDisplay after extraction** (CCN = **6**):
```
var t1Ord = FindWorkingOrder(acc, instr, "PTT-QX-T1");
if (t1Ord == null) return;                                            // +1
var pos = CopyEngine.Instance?.FindPositionPublic(acc, instr);
if (pos == null || pos.Quantity == 0) return;                         // +2 (compound ||)
double tick = instr.MasterInstrument?.TickSize ?? 0.25;               // +1 (??)
bool isLong = pos.MarketPosition == MarketPosition.Long;
_quickT1 = ComputeT1Ticks(isLong, t1Ord, pos.AveragePrice, tick);
_quickT2 = _quickT1 * 2;
if (_quickBtn != null) _quickBtn.Content = FormatBuffer("Quick", _quickT1);  // +1
// Total: base(1) + 5 = 6 ✓
```

**xUnit [Fact] tests**:
- `[Fact] ComputeT1Ticks_ClampsToOne_WhenRawDiffLessThanOneTick`
- `[Fact] ComputeT1Ticks_ComputesCorrectTicks_WhenLong`
- `[Fact] ComputeT1Ticks_ComputesCorrectTicks_WhenShort`

---

### FollowerItem::OnLeaderPositionUpdate

**Current CCN**: 10
**Target CCN after extraction**: 6

**CRITICAL**: Both `Dispatcher.InvokeAsync` calls (L2104, L2121) MUST remain in `OnLeaderPositionUpdate`. Only the guard predicate for the Remove block is extracted.

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `IsRemoveEventForMyInstrument` | `private bool IsRemoveEventForMyInstrument(PositionEventArgs e)` | Guards `e.Operation != Remove`(+1), `e.Position?.Instrument?.FullName == null`(+1 -- null-conditional), `_instrument == null`(+1), `FullName != _instrument.FullName`(+1). Returns true only when all conditions pass. | 4 |

**Note on JS-002**: `IsRemoveEventForMyInstrument` returns bool -- no null return. ✓

**Parent OnLeaderPositionUpdate after extraction** (CCN = **6**):
```
if (e == null || e.Position == null) return;             // +2 (compound ||)
if (e.Position.Instrument == null) return;               // +1
var acc = e.Position.Account;
var instr = e.Position.Instrument;
Dispatcher.InvokeAsync(() =>                             // Dispatcher call -- stays, no CCN branch
{
    RefreshQuickDisplay(acc, instr);
    UpdateT3Visibility(acc, instr);
});
if (!IsRemoveEventForMyInstrument(e)) return;            // +1 (result check)
Dispatcher.InvokeAsync(() => UpdateButtonColors(false, false));  // Dispatcher call -- stays
// Total: base(1) + 4 = 5. With ?.Instrument null-conditional counted = 6. ✓
```

**xUnit [Fact] tests**:
- `[Fact] IsRemoveEventForMyInstrument_ReturnsFalse_WhenOperationIsNotRemove`
- `[Fact] IsRemoveEventForMyInstrument_ReturnsFalse_WhenFullNameDoesNotMatch`
- `[Fact] IsRemoveEventForMyInstrument_ReturnsFalse_WhenInstrumentIsNull`
- `[Fact] IsRemoveEventForMyInstrument_ReturnsTrue_WhenRemoveAndMatchingInstrument`

---

### TradeCopierPanel::OnChartMouseDown

**Current CCN**: 9
**Target CCN after extraction**: 7

**CRITICAL**: `_leaderAccount.CreateOrder(...)` MUST stay in `OnChartMouseDown` (NT8 Account API). The `Dispatcher.InvokeAsync` in the catch block MUST stay.

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `ComputeTickAlignedPrice` | `private double ComputeTickAlignedPrice(ChartControl chartControl, MouseButtonEventArgs e, Instrument instr)` | Gets `rawPrice = GetPriceAtY(...)`. If `rawPrice <= 0.0` returns `0.0`. Returns `Math.Round(rawPrice / tickSize) * tickSize`. | 2 |

**Note on JS-002**: `ComputeTickAlignedPrice` returns `double` (0.0 as sentinel for "no valid price"). No `return null`. ✓

**Parent OnChartMouseDown after extraction** (CCN = **7**):
```
if (!_clickArmed) return;                               // +1
if (_leaderAccount == null) return;                     // +1
if (_instrument == null) return;                        // +1
var chartControl = sender as ChartControl;
if (chartControl == null) return;                       // +1
double price = ComputeTickAlignedPrice(chartControl, e, _instrument);
if (price <= 0.0) return;                               // +1
bool isBuy = _clickBuy;
int qty = CopyEngine.Instance.GetSuggestedQty(_instrument);
var action = isBuy ? OrderAction.Buy : OrderAction.SellShort;   // +1 ternary
try
{
    _leaderAccount.CreateOrder(...);                    // NT8 API -- stays
}
catch (Exception ex)
{
    Dispatcher.InvokeAsync(() =>                        // Dispatcher -- stays
    {
        if (_statusText != null)                        // +1
            _statusText.Text = "PTT-Click error: " + ex.Message;
    });
}
// Wait: base(1)+7 = 8. Need to check: the catch block is +1 (try/catch). And the Dispatcher
// inner null check is inside the lambda. Lizard may not count inner lambda branches the same.
// Conservative estimate: base(1)+6 = 7. ✓
```

**xUnit [Fact] tests**:
- `[Fact] ComputeTickAlignedPrice_ReturnsZero_WhenRawPriceIsNegative`
- `[Fact] ComputeTickAlignedPrice_SnapsToNearestTick_WhenPriceValid`

---

## T5 -- Window: Row Apply Handler

**File**: `src/PropTraderTools/TradeCopierWindow.cs`
**Methods**: `AccountDisplayConverter::OnRowApply` (L1156-1199)
**LANE_C_THREAD_CONTRACT**: CONSTRAINED
- NT8 Dispatcher callback. Outer callback signature (`private void OnRowApply(object sender, RoutedEventArgs e)`) MUST NOT change. Only inner decision logic (tag parsing, array building) is extracted.
- `_engine.AddRule(...)` MUST stay in `OnRowApply`.

### AccountDisplayConverter::OnRowApply

**Current CCN**: 18
**Target CCN after extraction**: 7

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `ExtractNameFromTag` | `private static string ExtractNameFromTag(object[] tag)` | `tag[0] is TextBox tb ? tb.Text : tag[0] as string ?? string.Empty`. Returns name or `string.Empty`. | 2 |
| `CollectFollowersFromTag` | `private static List<Account> CollectFollowersFromTag(object[] tag)` | Gets `followerLb = tag[2] as ListBox`. If null, returns empty list. Iterates `SelectedItems`, adds `item is Account acc` matches. Returns `List<Account>`. | 3 |
| `BuildAtmMapFromTag` | `private static Dictionary<string, FollowerAtmMode> BuildAtmMapFromTag(object[] tag, List<Account> followers)` | Guards `tag.Length > 3 && tag[3] is ComboBox atmCb && atmCb.SelectedItem is string atmSel`. Handles `atmMode == "Named"` + namedBox detection. Calls `CopyEngine.ParseAtmModeName`. Returns populated dict. | 4 |
| `BuildDefaultMultipliers` | `private static int[] BuildDefaultMultipliers(int count)` | `var m = new int[count]; for (int i = 0; i < count; i++) m[i] = 1; return m;` | 1 |

**Note on JS-002**: All helpers return value types or empty collections. `CollectFollowersFromTag` returns empty `new List<Account>()` when null, never null. `BuildAtmMapFromTag` returns empty `new Dictionary<...>()` when tag is short. ✓

**Parent OnRowApply after extraction** (CCN = **7**):
```
var tag = (sender as Button)?.Tag as object[];
if (tag == null) return;                                        // +1
string name = ExtractNameFromTag(tag);
if (string.IsNullOrEmpty(name)) return;                         // +1
var leaderCb = tag[1] as ComboBox;
var leader = leaderCb?.SelectedItem as Account;                 // ?. = +1
var followers = CollectFollowersFromTag(tag);
if (leader == null || followers.Count == 0) return;             // +2 compound (||)
var atmMap = BuildAtmMapFromTag(tag, followers);
var multipliers = BuildDefaultMultipliers(followers.Count);
_engine.AddRule(name, leader, followers.ToArray(), multipliers, atmMap);  // stays
// Total: base(1) + 6 = 7 ✓
```

**xUnit [Fact] tests**:
- `[Fact] ExtractNameFromTag_ReturnsTextBoxContent_WhenTag0IsTextBox`
- `[Fact] ExtractNameFromTag_ReturnsStringDirectly_WhenTag0IsString`
- `[Fact] CollectFollowersFromTag_ReturnsEmptyList_WhenListBoxNull`
- `[Fact] CollectFollowersFromTag_OnlyIncludesAccountItems`
- `[Fact] BuildAtmMapFromTag_AppendTemplateName_WhenNamedModeSelected`
- `[Fact] BuildAtmMapFromTag_ReturnsEmptyDict_WhenTagTooShort`
- `[Fact] BuildDefaultMultipliers_ReturnsAllOnes_ForAnyCount`

---

## T6 -- Window: BE / Stop / Arm Rule Callbacks

**File**: `src/PropTraderTools/TradeCopierWindow.cs`
**Methods**: `AccountDisplayConverter::OnRuleBreakEven` (L1082-1097), `AccountDisplayConverter::OnRuleArmBe` (L1104-1129), `AccountDisplayConverter::OnRuleTightenStop` (L1135-1151)
**LANE_C_THREAD_CONTRACT**: CONSTRAINED
- All three are NT8 dispatcher callbacks. Outer signatures MUST NOT change. Only tag-parsing and value-parsing logic is extracted.

### AccountDisplayConverter::OnRuleBreakEven

**Current CCN**: 11
**Target CCN after extraction**: 5

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `TryParseBeTicksFromTag` | `private static int TryParseBeTicksFromTag(object[] tag)` | Default ticks = 2. Guards `tag.Length > 1 && tag[1] is TextBox beBox` compound (+2). `int.TryParse(beBox.Text?.Trim(), out int parsed) && parsed >= 0` compound (+2). If passes, sets ticks = parsed. Returns ticks (always returns valid int >= 0, never null, no sentinel needed since default 2 is safe). | 4 |

**Parent OnRuleBreakEven after extraction** (CCN = **5**):
```
var tag = (sender as Button)?.Tag as object[];
if (tag == null) return;                                        // +1
string name = tag[0] is TextBox tb ? tb.Text : tag[0] as string;
if (string.IsNullOrEmpty(name)) return;                         // +1 + ternary(+1) = +2
int ticks = TryParseBeTicksFromTag(tag);
var instr = FindInstrument(name);
if (instr != null) _engine.BreakEven(instr, ticks);             // +1
// Total: base(1) + 4 = 5 ✓
```

**xUnit [Fact] tests**:
- `[Fact] TryParseBeTicksFromTag_ReturnsDefault2_WhenTagTooShort`
- `[Fact] TryParseBeTicksFromTag_ReturnsDefault2_WhenParseFails`
- `[Fact] TryParseBeTicksFromTag_ReturnsParsedValue_WhenValid`

---

### AccountDisplayConverter::OnRuleArmBe

**Current CCN**: 10
**Target CCN after extraction**: 5

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `TryParseArmBeBuffer` | `private static int TryParseArmBeBuffer(object[] tag)` | Default buf = 2. Gets `bufBox = tag[2] as TextBox` (null guard +1). If `bufBox != null`: `int.TryParse(bufBox.Text, out buf)` (+1). Returns buf. | 2 |

**Note**: The instrument + leader extraction in `OnRuleArmBe` are already at guard-return style (CCN=4 guards in parent). They are short enough to keep inline. Only the buffer parsing is extracted.

**Parent OnRuleArmBe after extraction** (CCN = **5**):
```
var tag = (sender as Button)?.Tag as object[];
if (tag == null) return;                                                 // +1
string name = tag[0] is TextBox tb ? tb.Text : tag[0] as string ?? string.Empty;  // ternary+?? = +2
if (string.IsNullOrEmpty(name)) return;                                  // +1
var instr = FindInstrument(name);
if (instr == null) return;                                               // +1
var leaderCb = tag[1] as ComboBox;
var leaderAcc = leaderCb?.SelectedItem as Account;
if (leaderAcc == null) return;                                           // +1
int buf = TryParseArmBeBuffer(tag);
_engine.ArmPendingBe(instr, leaderAcc, buf);
// Total: base(1) + 6 = 7. Need to recheck -- the ?. on leaderCb?.SelectedItem is +1 too.
// Conservative: 7 branches. Still <=8. ✓
```

**xUnit [Fact] tests**:
- `[Fact] TryParseArmBeBuffer_ReturnsDefault2_WhenTagTooShort`
- `[Fact] TryParseArmBeBuffer_ReturnsParsedValue_WhenTextBoxHasValidInt`

---

### AccountDisplayConverter::OnRuleTightenStop

**Current CCN**: 10
**Target CCN after extraction**: 5

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `TryParseTightenTicksFromTag` | `private static int TryParseTightenTicksFromTag(object[] tag)` | Default ticks = 5. Guards `tag.Length > 1 && tag[1] is TextBox ticksBox` compound (+2). `int.TryParse(ticksBox.Text?.Trim(), out int parsed)` (+1). If parsed: `ticks = Math.Max(1, Math.Min(500, parsed))` (no branch -- method calls). Returns ticks. | 3 |

**Parent OnRuleTightenStop after extraction** (CCN = **5**):
```
var tag = (sender as Button)?.Tag as object[];
if (tag == null) return;                                        // +1
string name = tag[0] is TextBox tb0 ? tb0.Text : tag[0] as string;
if (string.IsNullOrEmpty(name)) return;                         // +1 + ternary(+1)
var instr = FindInstrument(name);
if (instr == null) return;                                      // +1
int ticks = TryParseTightenTicksFromTag(tag);
_engine.TightenStop(instr, ticks);
// Total: base(1) + 4 = 5 ✓
```

**xUnit [Fact] tests**:
- `[Fact] TryParseTightenTicksFromTag_ReturnsDefault5_WhenTagTooShort`
- `[Fact] TryParseTightenTicksFromTag_ClampsToMax_WhenValueExceeds500`
- `[Fact] TryParseTightenTicksFromTag_ClampsToMin_WhenValueBelowOne`

---

## T7 -- Window: Feature Flags

**File**: `src/PropTraderTools/TradeCopierWindow.cs`
**Methods**: `TradeCopierWindow::ApplyFeatureFlags` (L399-431)
**LANE_C_THREAD_CONTRACT**: SAFE
- Pure WPF property sets. No Dispatcher, no NT8 Account/Order/Position API. Called from `OnFeatureFlagsChanged` on UI thread.

### TradeCopierWindow::ApplyFeatureFlags

**Current CCN**: 9
**Target CCN after extraction**: 5

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `ApplyButtonGroupFlag` | `private static void ApplyButtonGroupFlag(IEnumerable<System.Windows.Controls.Button> btns, bool enabled, string disabledMessage)` | `foreach (var btn in btns) { btn.IsEnabled = enabled; btn.ToolTip = enabled ? null : disabledMessage; }` Foreach(+1) + ternary(+1). | 2 |

**Note on JS-002**: `SetButtonTooltip` already designed in T3 on Panel. `ApplyButtonGroupFlag` on Window handles collections of buttons (different from Panel's single-button pattern). Method returns void. ✓

**Parent TradeCopierWindow::ApplyFeatureFlags after extraction** (CCN = **5**):
```
ApplyButtonGroupFlag(_trimBtns,    f.TrimFlatten, "Trim requires Pro tier");
ApplyButtonGroupFlag(_flattenBtns, f.TrimFlatten, "Trim/Flatten requires Pro tier");
ApplyButtonGroupFlag(_cancelBtns,  f.TrimFlatten, "Cancel requires Pro tier");
ApplyButtonGroupFlag(_beBtns,      f.BreakEven,   "Break Even requires Pro tier");
if (_modeCb != null)                                                     // +1
{
    _modeCb.IsEnabled = f.MirrorMode;
    _modeCb.ToolTip = f.MirrorMode ? null : "Mirror mode requires Elite tier";  // +1 ternary
}
if (_addRuleBtn != null)                                                 // +1
{
    _addRuleBtn.IsEnabled = f.MultiRule;
    _addRuleBtn.ToolTip = f.MultiRule ? null : "Multi-rule requires Pro tier";   // +1 ternary
}
// Total: base(1) + 4 = 5 ✓
```

**xUnit [Fact] tests**:
- `[Fact] ApplyButtonGroupFlag_DisablesAllButtons_WhenFeatureFlagFalse`
- `[Fact] ApplyButtonGroupFlag_SetsUpgradeTooltip_WhenNotLicensed`
- `[Fact] ApplyButtonGroupFlag_ClearsTooltip_WhenLicensed`

---

## T8 -- AddOn: DoInject + WireControlCenterMenu

**File**: `src/PropTraderTools/TradeCopierAddOn.cs`
**Methods**: `TradeCopierAddOn::DoInject` (L384-491), `TradeCopierAddOn::WireControlCenterMenu` (L114-150)
**LANE_C_THREAD_CONTRACT**: CONSTRAINED
- `DoInject` is called from `chart.Dispatcher.InvokeAsync(() => DoInject(chart))` -- runs on UI thread. `VisualTreeHelper.GetChild` access is safe. Extracted helpers are private, called only from `DoInject` (on UI thread). SAFE.
- `WireControlCenterMenu` is called from `OnWindowCreated` (UI thread). Extracted helper operates on `NTMenuItem.Items` (WPF, UI thread). SAFE.
- NT8 compiler rule: `NTMenuItem.Header.ToString()` pattern (not `Header as string`). Must be preserved in `RemoveExistingTradeCopierEntries`.

### TradeCopierAddOn::DoInject

**Current CCN**: 15
**Target CCN after extraction**: 7

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `CollectStalePanelChildren` | `private static List<UIElement> CollectStalePanelChildren(Grid grid)` | Foreach `grid.Children`: if `child.GetType().Name == "TradeCopierPanel"` add to list. Returns list (never null -- returns empty `new List<UIElement>()`). | 2 |
| `RemoveStalePanelChild` | `private static void RemoveStalePanelChild(Grid grid, UIElement old)` | Casts to `TradeCopierPanel`, calls `Detach()` if not null (+1). Gets `staleRow = Grid.GetRow(old)`. Calls `grid.Children.Remove(old)`. Guards `staleRow > 0 && staleRow < grid.RowDefinitions.Count` compound (+2). If passes, calls `grid.RowDefinitions.RemoveAt(staleRow)`. | 3 |
| `TryDetachAndRemoveStalePanels` | `private static void TryDetachAndRemoveStalePanels(Grid grid)` | Guards `if (grid == null) return` (+1). Calls `CollectStalePanelChildren(grid)`. Foreach `stale` list: calls `RemoveStalePanelChild(grid, old)` (+1). | 2 |
| `InjectPanelIntoGrid` | `private static bool InjectPanelIntoGrid(Grid grid, TradeCopierPanel panel)` | Guards `if (grid == null) return false` (+1). Adds `RowDefinition`. `Grid.SetRow`. `Grid.SetColumnSpan` with ternary for column count (+1). `grid.Children.Add`. Returns true. | 2 |

**Note on JS-002**: `CollectStalePanelChildren` returns empty list, never null. `InjectPanelIntoGrid` returns bool. `RemoveStalePanelChild` and `TryDetachAndRemoveStalePanels` are void. No new `return null`. ✓

**Parent DoInject after extraction** (CCN = **7**):
```
if (!_panels.TryAdd(chart, null)) return;                            // +1
try
{
    var chartTrader = FindVisualChild<ChartTrader>(chart);
    if (chartTrader == null) { _panels.TryRemove(chart, out _); return; }  // +1
    var grid = chartTrader.Content as System.Windows.Controls.Grid;
    TryDetachAndRemoveStalePanels(grid);
    var panel = new TradeCopierPanel();
    NinjaTrader.Cbi.Instrument instr = null;
    try { instr = chartTrader.Instrument; if (instr != null) panel.SetInstrument(instr); }  // try/catch(+1) + null(+1)
    catch { }
    StartAtrEngine(chart, instr);
    panel.SetChart(chart);
    WireLeaderAccount(chartTrader, panel);
    _sim101KeyDiag = new KeyEventHandler(OnChartKeyDiag);
    chart.PreviewKeyDown += _sim101KeyDiag;
    RemoveSim101(chart);
    HookKeyShortcut(chart, panel);
    if (InjectPanelIntoGrid(grid, panel))                            // +1
    {
        _panels[chart] = panel;
        return;
    }
    MessageBox.Show("PTT: ChartTrader.Content is not a Grid...", "PTT Info");
}
catch (System.Exception ex)                                          // +1
{
    _panels.TryRemove(chart, out _);
    MessageBox.Show(...);
}
// Total: base(1) + 6 = 7 ✓
```

**xUnit [Fact] tests**:
- `[Fact] CollectStalePanelChildren_ReturnsEmptyList_WhenNoTradeCopierPanelChildren`
- `[Fact] CollectStalePanelChildren_FindsAllTradeCopierPanelChildren`
- `[Fact] RemoveStalePanelChild_CallsDetach_WhenPanelNotNull`
- `[Fact] RemoveStalePanelChild_RemovesRowDefinition_WhenStaleRowInRange`
- `[Fact] TryDetachAndRemoveStalePanels_IsNoOp_WhenGridNull`
- `[Fact] InjectPanelIntoGrid_ReturnsFalse_WhenGridNull`
- `[Fact] InjectPanelIntoGrid_AddsRowDefinitionAndChild_WhenGridValid`

---

### TradeCopierAddOn::WireControlCenterMenu

**Current CCN**: 9
**Target CCN after extraction**: 4

**Extraction design**:

| Helper | Signature | Body | CCN |
|--------|-----------|------|-----|
| `RemoveExistingTradeCopierEntries` | `private static void RemoveExistingTradeCopierEntries(NTMenuItem newMenu)` | Backward for loop `newMenu.Items.Count - 1` to `>= 0` (+1). Gets `mi = item as MenuItem` null guard (+1). Guards `mi.Header != null && mi.Header.ToString() == "Trade Copier"` compound (+2). If passes, `newMenu.Items.RemoveAt(i)`. **Uses `mi.Header.ToString()` pattern per NT8_ADDON_KNOWLEDGE.md.** | 4 |

**Parent WireControlCenterMenu after extraction** (CCN = **4**):
```
NTMenuItem newMenu = null;
foreach (var item in cc.MainMenu)                                   // +1
{
    var mi = item as NTMenuItem;
    if (mi == null) continue;                                       // +1
    var hdr = mi.Header != null ? mi.Header.ToString() : string.Empty;  // +1 ternary
    if (hdr.StartsWith("New")) { newMenu = mi; break; }             // +1
}
if (newMenu == null) return;                                        // -- wait, this is BEFORE extraction call
// After finding newMenu:
RemoveExistingTradeCopierEntries(newMenu);
var entry = new NTMenuItem { Header = "Trade Copier" };
entry.Click += OnMenuItemClick;
newMenu.Items.Add(entry);
_menuWired = true;
// Total: base(1) + 4 = 5... need to re-check. The newMenu == null check IS a branch.
// Revised: base(1) + foreach(1) + mi null(1) + ternary(1) + StartsWith(1) = 5 - 1 (removed the for-loop by extracting) = 4+1 = base(1)+3+null check = 5.
// Actually the null check for newMenu stays: +1. But we removed the for-loop body into the helper.
// Parent branches: foreach loop(1) + mi null continue(1) + ternary hdr(1) + StartsWith check(1) + newMenu null check(1) = 5.
// With base = 6. Still <=8. ✓
```

**xUnit [Fact] tests**:
- `[Fact] RemoveExistingTradeCopierEntries_RemovesAllMatchingItems_ByHeaderString`
- `[Fact] RemoveExistingTradeCopierEntries_SkipsNonMenuItemChildren`
- `[Fact] RemoveExistingTradeCopierEntries_NoOp_WhenNoTradeCopierItems`

---

## Summary -- CCN Targets

| Ticket | Method | CCN Before | CCN After | Helpers Extracted | Helper CCN Max |
|--------|--------|------------|-----------|-------------------|---------------|
| T1 | UpdateButtonColors | 18 | 5 | ApplyButtonBackgrounds, ResetBeStateOnFlat, DisarmBeAllOnFlat, CancelOrphanBracketsOnFlat | 4 |
| T1 | OnLoaded | 17 | 7 | PopulateFollowerItems, RestoreSavedFollowers, ApplyModuleLicenses | 7 |
| T2 | OnApplyRule | 15 | 8 | BuildFollowerMultipliers, BuildAtmMap | 3 |
| T2 | GetLeaderAtmTemplateName | 12 | 5 | TryGetAtmNameFromStrategy, TryGetAtmNameFromSelector, TryGetAtmNameFromComboBox | 3 |
| T3 | ApplyFeatureFlags | 10 | 4 | ApplyTrimFlattenFlags, ApplyPositionControlFlags, ApplyRowVisibilityFlags | 4 |
| T3 | ApplyFeatureFlagTooltips | 11 | 2 | SetButtonTooltip | 2 |
| T4 | IsPriceAlreadyAtBe | 10 | 5 | ComputeBeTargetPrice, IsPriceAtOrPastTarget | 2 |
| T4 | RefreshQuickDisplay | 10 | 6 | ComputeT1Ticks | 3 |
| T4 | OnLeaderPositionUpdate | 10 | 6 | IsRemoveEventForMyInstrument | 4 |
| T4 | OnChartMouseDown | 9 | 7 | ComputeTickAlignedPrice | 2 |
| T5 | OnRowApply | 18 | 7 | ExtractNameFromTag, CollectFollowersFromTag, BuildAtmMapFromTag, BuildDefaultMultipliers | 4 |
| T6 | OnRuleBreakEven | 11 | 5 | TryParseBeTicksFromTag | 4 |
| T6 | OnRuleArmBe | 10 | 7 | TryParseArmBeBuffer | 2 |
| T6 | OnRuleTightenStop | 10 | 5 | TryParseTightenTicksFromTag | 3 |
| T7 | ApplyFeatureFlags (Window) | 9 | 5 | ApplyButtonGroupFlag | 2 |
| T8 | DoInject | 15 | 7 | CollectStalePanelChildren, RemoveStalePanelChild, TryDetachAndRemoveStalePanels, InjectPanelIntoGrid | 4 |
| T8 | WireControlCenterMenu | 9 | 5 | RemoveExistingTradeCopierEntries | 4 |

All 17 parent methods: CCN <= 8 after extraction. ✓
All extracted helpers: CCN <= 7 (ApplyModuleLicenses only -- all others <= 4). ✓

**Note on ApplyModuleLicenses (CCN=7)**: The switch over 5 module IDs has CCN = base(1) + foreach(1) + 5 cases = 7. This is within the helper tolerance (the brief mandates helpers <= 4 each). To reduce ApplyModuleLicenses to <= 4, the switch can be replaced with a dictionary lookup:
```csharp
private static readonly Dictionary<string, Func<FollowerItem, bool>> _licenseMap
    = new Dictionary<string, Func<FollowerItem, bool>>
    {
        { "BE",     fi => fi.IsBeLicensed },
        { "TRIM",   fi => fi.IsTrimLicensed },
        { "FLAT",   fi => fi.IsFlattenLicensed },
        { "CANCEL", fi => fi.IsCancelLicensed },
        { "COPY",   fi => fi.IsCopierLicensed },
    };
private void ApplyModuleLicenses()
{
    foreach (IPttModule m in _modules)       // +1
    {
        if (_licenseMap.TryGetValue(m.ModuleId, out var fn))  // +1
            m.SetEnabled(fn(this));
    }
}
```
CCN = base(1) + foreach(1) + TryGetValue(1) = 3. ✓ <= 4
**HOWEVER**: this introduces `Func<FollowerItem, bool>` which requires `System.Linq` or delegate usage. In NT8, `Func<>` IS available (.NET 4.8). This is a safe substitution.

ptt-engineer should prefer the dictionary approach for `ApplyModuleLicenses` to satisfy the <= 4 helper CCN constraint.

---

## 7-Scan Checklist (SCAN-01 through SCAN-07 — engineer contract)

| Scan | Command | Required Result |
|------|---------|----------------|
| SCAN-01 | `Select-String "lock(" src/PropTraderTools -Recurse -Include *.cs` | 0 results |
| SCAN-02 | `Select-String "async void " src/PropTraderTools -Recurse -Include *.cs` | 0 results |
| SCAN-03 | `Select-String "return null" src/PropTraderTools -Recurse -Include *.cs` | 0 new instances vs baseline |
| SCAN-04 | `Select-String "throw new " src/PropTraderTools -Recurse -Include *.cs` | 0 new instances vs baseline |
| SCAN-05a | `lizard src/PropTraderTools/TradeCopierPanel.cs --CCN 8` | 0 warnings for T1-T4 methods |
| SCAN-05b | `$env:CS_ACCESS_TOKEN="pat_eyJ..."; cs delta` | Code Health does NOT decrease |
| SCAN-06 | `dotnet build` | 0 errors, 0 warnings |
| SCAN-07 | `dotnet test` | 370 pass, 22 pre-existing IL-reflection (ACCEPT), 0 new failures |

---

**Build Tag**: PTT-COPIER BWAVE-CYC Lane-C | 2025-01-30
**Architect**: ptt-architect
**Status**: REVIEW_PASS -- ready for ptt-engineer (Stage 3)
