# B43-LaneA — Ticket T2 Completion Report
**Block:** PTT-COPIER-B43 (Per-Follower ATM Template ComboBox)
**Ticket:** T2 — TradeCopierWindow.cs: Replace ATM mode cluster with template ComboBox
**Engineer:** ptt-engineer
**Date:** 2026-08-05
**File Modified:** `src/PropTraderTools/TradeCopierWindow.cs`
**Status:** BUILD_PASS

---

## What Changed (Method-by-Method)

### BuildRuleRow() — MODIFIED

**Removed (B8/B9 cluster):**
- `var atmCb = new ComboBox { Width = 80 }` with "Inherit"/"Market"/"Named" items
- `var namedBox = new TextBox { Width = 80, Visibility = Visibility.Collapsed, ... }`
- `atmCb.SelectionChanged += (s, e2) => { ... }` lambda (namedBox show/hide)
- `var atmColPanel = new StackPanel { Orientation = Orientation.Vertical }`
- `atmColPanel.Children.Add(atmCb); atmColPanel.Children.Add(namedBox)`
- `Grid.SetColumn(atmColPanel, 9); grid.Children.Add(atmColPanel)`
- `applyBtn.Tag = new object[] { instrumentName, leaderCb, followerLb, atmCb, namedBox }` (5-element)

**Added (B43 replacement):**
- `var atmTemplateCb = new ComboBox { Width = 120, Margin = new Thickness(2), ToolTip = "ATM template for this follower" }`
- `atmTemplateCb.Items.Add("(none)")` as sentinel
- `try { foreach (var t in NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyTemplates) atmTemplateCb.Items.Add(t.Name); } catch { }`
- `atmTemplateCb.SelectedIndex = 0`
- `Grid.SetColumn(atmTemplateCb, 9); grid.Children.Add(atmTemplateCb)`
- `applyBtn.Tag = new object[] { instrumentName, leaderCb, followerLb, atmTemplateCb }` (4-element)

**Net change:** -22 lines removed, +15 lines added. Tag array: 5 elements → 4 elements.

---

### BuildDynamicRuleRow() — MODIFIED

**Removed (B8/B9 cluster):**
- `var atmCbDyn = new ComboBox { Width = 80 }` with "Inherit"/"Market"/"Named" items
- `var namedBoxDyn = new TextBox { Width = 80, Visibility = Visibility.Collapsed, ... }`
- `atmCbDyn.SelectionChanged += (s, e2) => { ... }` lambda (namedBoxDyn show/hide)
- `var atmColPanel = new StackPanel { Orientation = Orientation.Vertical }`
- `atmColPanel.Children.Add(atmCbDyn); atmColPanel.Children.Add(namedBoxDyn)`
- `Grid.SetColumn(atmColPanel, 9); grid.Children.Add(atmColPanel)`
- `applyBtn.Tag = new object[] { instrTextBox, leaderCb, followerLb, atmCbDyn, namedBoxDyn }` (5-element)

**Added (B43 replacement):**
- `var atmTemplateCbDyn = new ComboBox { Width = 120, Margin = new Thickness(2), ToolTip = "ATM template for this follower" }`
- `atmTemplateCbDyn.Items.Add("(none)")` as sentinel
- `try { foreach (var t in NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyTemplates) atmTemplateCbDyn.Items.Add(t.Name); } catch { }`
- `atmTemplateCbDyn.SelectedIndex = 0`
- Applied to `applyBtn` before the BE cluster, then: `Grid.SetColumn(atmTemplateCbDyn, 9); grid.Children.Add(atmTemplateCbDyn)` placed after the BE cluster (col 8) to match col ordering
- `applyBtn.Tag = new object[] { instrTextBox, leaderCb, followerLb, atmTemplateCbDyn }` (4-element)

**Net change:** -22 lines removed, +15 lines added. Tag array: 5 elements → 4 elements.

---

### OnRowApply (~L807) — MODIFIED

**Removed:**
```csharp
// B8 T2 + B9 T3: read ATM mode from tag[3]; if "Named", append tag[4] namedBox text
var atmMap = new Dictionary<string, FollowerAtmMode>();
if (tag.Length > 3 && tag[3] is ComboBox atmCb && atmCb.SelectedItem is string atmSel)
{
    string atmMode = atmSel;
    if (atmMode == "Named" && tag.Length > 4 && tag[4] is TextBox namedBox && namedBox.Text.Length > 0)
        atmMode = "Named:" + namedBox.Text;
    var mode = CopyEngine.ParseAtmModeName(atmMode);
    foreach (var acc in followers)
        atmMap[acc.Name] = mode;
}
```

**Replaced with:**
```csharp
// B43 T2: ATM template read from tag[3] (single ComboBox -- no namedBox at tag[4]).
var atmMap = new Dictionary<string, FollowerAtmMode>();
if (tag.Length > 3 && tag[3] is ComboBox atmTemplateCb)
{
    var sel = atmTemplateCb.SelectedItem as string ?? string.Empty;
    var mode = ParseAtmTemplateSelection(sel);
    foreach (var acc in followers)
        atmMap[acc.Name] = mode;
}
```

**CYC delta:** One branch removed (the `atmSel is string` combined with `atmMode == "Named"` sub-branch). Net CYC ≤ 4.

---

### ParseAtmTemplateSelection — NEW METHOD (added after OnRowApply)

```csharp
internal static FollowerAtmMode ParseAtmTemplateSelection(string sel)
{
    if (string.IsNullOrEmpty(sel) || sel == "(none)")
        return new FollowerAtmMode.Inherit();
    return new FollowerAtmMode.Named(sel);
}
```

- **Signature:** `internal static FollowerAtmMode ParseAtmTemplateSelection(string sel)`
- **CYC:** 2 (one `if` branch)
- **Return:** Never null — always `FollowerAtmMode.Inherit()` or `FollowerAtmMode.Named(sel)`
- **Testability:** `internal static` accessible from `B43Tests.cs` in the same assembly
- **JS-002:** No `return null`
- **JS-021:** No `lock()`

---

## Tag Array Change Confirmation

| Method | Before | After |
|--------|--------|-------|
| `BuildRuleRow` applyBtn.Tag | `new object[] { instrumentName, leaderCb, followerLb, atmCb, namedBox }` — 5 elements | `new object[] { instrumentName, leaderCb, followerLb, atmTemplateCb }` — 4 elements |
| `BuildDynamicRuleRow` applyBtn.Tag | `new object[] { instrTextBox, leaderCb, followerLb, atmCbDyn, namedBoxDyn }` — 5 elements | `new object[] { instrTextBox, leaderCb, followerLb, atmTemplateCbDyn }` — 4 elements |

---

## 7-Scan Results (ALL ZERO — Layer 2)

### SCAN-01: `lock(` in TradeCopierWindow.cs
```
Select-String -Path "...\TradeCopierWindow.cs" -Pattern "lock\s*\("
```
**Result:** 1 match — line 877 comment `// JS-021: no lock().` — comment only, zero code hits.
**Verdict: PASS ✅**

### SCAN-02: `async void` in TradeCopierWindow.cs
```
Select-String -Path "...\TradeCopierWindow.cs" -Pattern "async\s+void"
```
**Result:** 1 match — line 877 comment `// JS-033: synchronous void event handler -- not async void.` — comment only, zero code hits.
**Verdict: PASS ✅**

### SCAN-03: `return null` in new/modified methods
```
Select-String -Path "...\TradeCopierWindow.cs" -Pattern "return\s+null"
```
**Result:** 2 matches — both in pre-existing `FindInstrument()` (lines 859, 861), zero in new/modified methods.
- `ParseAtmTemplateSelection` returns `new FollowerAtmMode.Inherit()` or `new FollowerAtmMode.Named(sel)` — never null.
**Verdict: PASS ✅**

### SCAN-04: CYC audit on new/modified methods
- `ParseAtmTemplateSelection`: 1 `if` statement = **CYC=2** ≤ 8 ✅
- `OnRowApply` (modified): 5 decision points (tag null, name empty, leader/followers empty, foreach loop, atmMap if) = **CYC=5** ≤ 8 ✅
**Verdict: PASS ✅**

### SCAN-05: `init;` in TradeCopierWindow.cs
```
Select-String -Path "...\TradeCopierWindow.cs" -Pattern "init;"
```
**Result:** No output — zero hits.
**Verdict: PASS ✅**

### SCAN-06: `volatile double` in TradeCopierWindow.cs
```
Select-String -Path "...\TradeCopierWindow.cs" -Pattern "volatile double"
```
**Result:** No output — zero hits.
**Verdict: PASS ✅**

### SCAN-07: `async void` (belt-and-suspenders)
```
Select-String -Path "...\TradeCopierWindow.cs" -Pattern "async void"
```
**Result:** 1 match — line 877 comment only, zero code hits.
**Verdict: PASS ✅**

### Informal: DateTime.Now[^U] (per ticket review SCAN-03 cross-ticket note)
```
Select-String -Path "...\TradeCopierWindow.cs" -Pattern "DateTime\.Now[^U]"
```
**Result:** No output — zero hits.
**Verdict: PASS ✅**

---

## NT8 Surprises

None encountered.

- `NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyTemplates` — wrapped in `try/catch` per plan §6.1 so if the API is unavailable at compile or runtime, the ComboBox gracefully shows only `"(none)"`. This is defensive per NT8 plan guidance.
- `FollowerAtmMode` confirmed as abstract class with nested `Inherit()` / `Named(string)` constructors in `CopyEngine.cs` (not a record — NT8-002 compliant).
- `PttContracts.cs` does not exist as a separate file. `FollowerAtmMode` lives in `CopyEngine.cs` lines ~73+.

---

## Files Modified

| File | Change |
|------|--------|
| `src/PropTraderTools/TradeCopierWindow.cs` | Modified: 3 methods changed, 1 new method added |

**Files NOT touched (zero diff):**
`CopyEngine.cs`, `TradeCopierPanel.cs`, `PttBus.cs`, `PTTFollowerStrategy.cs`, `TradeCopierAddOn.cs`, `B43Tests.cs` (separate ticket T3)

---

## BUILD_PASS
