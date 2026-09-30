# B43-LaneA — Implementation Tickets
**Block:** PTT-COPIER-B43 (Per-Follower ATM Template ComboBox)
**Epic:** atm-template-picker
**Phase:** 3 — Ticket Generation
**Source Plan:** `docs/brain/B43-LaneA/02-architecture-plan.md` — REVIEW_PASS
**Plan Review:** `docs/brain/B43-LaneA/02-plan-review.md` — REVIEW_PASS (Cycle 2, 2026-08-05)
**Architect:** ptt-architect
**Status:** TICKETS_COMPLETE

---

## Dependency Order

```
T1 (TradeCopierPanel.cs) ────┐
                             ├──> (parallel, independent) ──> T3 (B43Tests.cs — depends on T1+T2 complete)
T2 (TradeCopierWindow.cs) ───┘
```

- T1 and T2 share no new symbols and can be executed in parallel or in either order.
- T3 MUST execute after both T1 and T2 are complete. It references:
  - `TradeCopierWindow.ParseAtmTemplateSelection` (written in T2)
  - `TradeCopierPanel.GetLeaderAtmTemplateName` (written in T1; must be `internal static`)
  - `CopyEngine.ParseAtmModeName` (pre-existing, zero diff)

---

## TICKET T1 — TradeCopierPanel.cs: Replace ATM mode cluster with template ComboBox

**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Spec Requirement:** DW-B43-NAMED-TB-01 (eliminate Named ATM TextBox keyboard-bubbling defect)
**Plan Section:** §4.1 (all sub-sections)
**Net Change:** ~+35 lines (remove 3 old handlers + ~55 lines, add 4 new methods + ~90 lines)

---

### T1.1 — REMOVALS in BuildCheckItemTemplate() (~L1498)

Remove the following FEF (FrameworkElementFactory) elements and their wiring:

```
REMOVE: atmFactory (FrameworkElementFactory for ComboBox)
        - SetValue(Grid.ColumnProperty, 3)
        - Items "Inherit", "Market", "Named"
        - AddHandler(ComboBox.LoadedEvent,        OnFollowerAtmComboLoaded)
        - AddHandler(Selector.SelectionChangedEvent, OnFollowerAtmModeChanged_WithNamedBox)
        - AppendChild(atmFactory)

REMOVE: namedBoxFactory (FrameworkElementFactory for TextBox)
        - SetValue(VisibilityProperty, Visibility.Collapsed)
        - ToolTip = "ATM template name"
        - Width = 80
        - All .SetValue / .AddHandler calls on namedBoxFactory
        - AppendChild(namedBoxFactory)
```

Update `chkFactory.SetValue(Grid.ColumnProperty, ...)` from col 5 to col 4 (namedBox column removed).

---

### T1.2 — ADDITIONS in BuildCheckItemTemplate() (~L1498)

In place of the removed elements, add the following FEF block at col 3:

```csharp
// B43 T1: ATM template ComboBox (replaces Inherit/Market/Named ComboBox + namedBox TextBox).
// Col 3. Width=120 to accommodate template names. Wired via FEF LoadedEvent + SelectionChangedEvent.
// NT8-012: FEF AddHandler pattern for Loaded event -- mandatory for NT8 DataTemplate wiring.
var atmTemplateFactory = new FrameworkElementFactory(typeof(ComboBox));
atmTemplateFactory.SetValue(Grid.ColumnProperty,       3);
atmTemplateFactory.SetValue(ComboBox.WidthProperty,    120.0);
atmTemplateFactory.SetValue(ComboBox.MarginProperty,   new Thickness(2));
atmTemplateFactory.SetValue(ComboBox.ToolTipProperty,  "ATM template for this follower");
atmTemplateFactory.AddHandler(
    FrameworkElement.LoadedEvent,
    new RoutedEventHandler(OnFollowerAtmTemplateComboLoaded));
atmTemplateFactory.AddHandler(
    Selector.SelectionChangedEvent,
    new SelectionChangedEventHandler(OnFollowerAtmTemplateComboChanged));
```

Updated AppendChild order in BuildCheckItemTemplate:
```
nameFactory   (col 0)
pnlFactory    (col 1)
multFactory   (col 2)
atmTemplateFactory (col 3)  <-- NEW (replaces atmFactory + namedBoxFactory)
chkFactory    (col 4)       <-- was col 5, now col 4
```

---

### T1.3 — MODIFICATION: OnRowGridLoaded() (~L1569)

Change the number of ColumnDefinitions from **6 to 5**. Exact column layout:

```csharp
// B43 T1: 5 columns (was 6 -- namedBox col removed).
// Col 0: Star, MinWidth 80 -- account name
// Col 1: 62px fixed        -- daily P&L
// Col 2: 30px fixed        -- multiplier TextBox
// Col 3: 120px fixed       -- ATM template ComboBox (was 80px; wider for template names)
// Col 4: 20px fixed        -- checkbox
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 80 });
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
```

The existing idempotency guard `if (grid.ColumnDefinitions.Count > 0) return;` MUST remain.

---

### T1.4 — NEW METHOD: OnFollowerAtmTemplateComboLoaded

**Signature:** `private void OnFollowerAtmTemplateComboLoaded(object sender, RoutedEventArgs e)`
**CYC:** 4
**Rule constraints:** JS-021 (no lock), JS-002 (no return null), NT8-012 (Loaded event pattern),
NT8-019 (no async void — synchronous void only)

```csharp
// B43 T1: ATM template ComboBox Loaded handler.
// Fires on WPF UI thread (DataTemplate instantiation). No Dispatcher needed.
// Idempotency: Items.Count > 0 guard prevents double-population on re-layout.
// Populates: "(none)" sentinel + AtmStrategyTemplates list.
// Default: leader's current ChartTrader ATM template if found; else index 0.
// CYC=4: (1) null guard, (2) idempotency guard, (3) foreach loop, (4) leader-default branch.
// JS-021: no lock. JS-002: no return null. NT8-012: Loaded event via FEF AddHandler.
private void OnFollowerAtmTemplateComboLoaded(object sender, RoutedEventArgs e)
{
    var cb = sender as ComboBox;
    if (cb == null) return;                                // branch 1 -- null guard
    if (cb.Items.Count > 0) return;                       // branch 2 -- idempotency guard
    cb.Items.Add("(none)");
    string leaderTemplate = GetLeaderAtmTemplateName(_currentChart);
    int defaultIdx = 0;
    try
    {
        foreach (var t in NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyTemplates) // branch 3
        {
            cb.Items.Add(t.Name);
            if (t.Name == leaderTemplate)
                defaultIdx = cb.Items.Count - 1;          // branch 4 -- leader found
        }
    }
    catch
    {
        // AtmStrategyTemplates unavailable -- "(none)" only; fallback to filesystem if needed.
        // See plan §6.2 for filesystem fallback implementation if this catch fires at F5.
    }
    cb.SelectedIndex = defaultIdx;
}
```

**AtmStrategyTemplates filesystem fallback** (apply inside the catch block if NT8 API fails at F5):
```csharp
// Fallback: enumerate from NT8 ATM template filesystem path
string templateDir = System.IO.Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
    "NinjaTrader 8", "templates", "AtmStrategy");
if (System.IO.Directory.Exists(templateDir))
{
    foreach (var f in System.IO.Directory.GetFiles(templateDir, "*.xml"))
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(f);
        cb.Items.Add(name);
        if (name == leaderTemplate) defaultIdx = cb.Items.Count - 1;
    }
}
```
Document result in `NT8_COMPILER_RULES.md` if fallback is needed (new NT8 rule discovery).

---

### T1.5 — NEW METHOD: OnFollowerAtmTemplateComboChanged

**Signature:** `private void OnFollowerAtmTemplateComboChanged(object sender, SelectionChangedEventArgs e)`
**CYC:** 3
**Rule constraints:** JS-021 (no lock), JS-002 (no return null), JS-033 (no async void)

```csharp
// B43 T1: ATM template ComboBox SelectionChanged handler.
// Fires on WPF UI thread. Writes item.AtmModeName in "Inherit" or "Named:templateName" format.
// Serialization format UNCHANGED -- CopyEngine.ParseAtmModeName parses both unchanged.
// CYC=3: (1) cb null guard, (2) item null guard, (3) "(none)" branch.
// JS-021: no lock. JS-002: no return null (guard-returns only -- not returning null values).
private void OnFollowerAtmTemplateComboChanged(object sender, SelectionChangedEventArgs e)
{
    var cb = sender as ComboBox;
    if (cb == null) return;                                          // branch 1 -- guard
    var item = (cb.DataContext as FollowerItem)
               ?? FindAncestorDataContext<FollowerItem>(cb);
    if (item == null) return;                                        // branch 2 -- guard
    var sel = cb.SelectedItem as string ?? string.Empty;
    item.AtmModeName = (sel == "(none)" || sel.Length == 0)         // branch 3
        ? "Inherit"
        : "Named:" + sel;
}
```

---

### T1.6 — NEW METHOD: GetLeaderAtmTemplateName (internal static)

**Signature:** `internal static string GetLeaderAtmTemplateName(Chart currentChart)`
**CYC:** 4
**Rule constraints:** JS-021 (no lock), JS-002 (returns string.Empty — never null),
NT8-008 (Chart.ChartControl banned — use FindVisualChild), NT8-041 (no reflection on Charts)

```csharp
// B43 T1: Reads the ATM template name currently selected in ChartTrader for the given chart.
// Internal static for testability (T_B43_04 calls with null -- no WPF instantiation required).
// NT8-008: Chart.ChartControl does not exist -- use FindVisualChild<ChartTrader> instead.
// NT8-041: Reflection on ChartControl.Charts fails -- visual tree walk only.
// ATM ComboBox: index 2 in ChartTrader (index 0 = Instrument, index 1 = Account per B18).
// Returns string.Empty on any null/exception -- NEVER throws, NEVER returns null.
// CYC=4: (1) chart null, (2) ChartTrader null, (3) ComboBox found/not, (4) catch.
internal static string GetLeaderAtmTemplateName(Chart currentChart)
{
    if (currentChart == null) return string.Empty;                   // branch 1 -- null guard
    try
    {
        var ct = TradeCopierAddOn.FindVisualChild<ChartTrader>(currentChart);
        if (ct == null) return string.Empty;                         // branch 2 -- null guard
        var atmCb = TradeCopierAddOn.FindVisualChildByIndex<ComboBox>(ct, 2);
        if (atmCb == null) return string.Empty;                      // branch 3 -- not found
        return atmCb.SelectedItem as string ?? string.Empty;
    }
    catch { return string.Empty; }                                   // branch 4 -- API exception
}
```

---

### T1.7 — NEW METHOD: FindAncestorDataContext<T> (private static)

**Signature:** `private static T FindAncestorDataContext<T>(DependencyObject child) where T : class`
**CYC:** 3
**Rule constraints:** JS-021 (no lock), JS-002 (returns default(T) — not return null)

```csharp
// B43 T1: Walks the visual tree UPWARD from child, returning the DataContext of the first
// ancestor whose DataContext is of type T. Fallback for FEF-instantiated templates where
// DataContext is set on an ancestor Grid rather than the leaf control directly.
// CYC=3: (1) child null guard, (2) while loop, (3) DataContext cast match.
// JS-021: no lock. JS-002: returns default(T) -- not return null.
// VisualTreeHelper.GetParent: must be called on WPF UI thread. Called only from UI-thread handlers.
private static T FindAncestorDataContext<T>(DependencyObject child) where T : class
{
    if (child == null) return default(T);                            // branch 1 -- null guard
    var parent = VisualTreeHelper.GetParent(child);
    while (parent != null)                                           // branch 2 -- loop
    {
        var fe = parent as FrameworkElement;
        if (fe?.DataContext is T ctx) return ctx;                    // branch 3 -- match found
        parent = VisualTreeHelper.GetParent(parent);
    }
    return default(T);
}
```

---

### T1.8 — HANDLERS REMOVED (delete entirely)

The following three methods are **dead code after B43** and MUST be removed from TradeCopierPanel.cs:

| Method | Approximate Location | Reason |
|--------|---------------------|--------|
| `OnFollowerAtmComboLoaded` | ~L1600 | Wired to removed atmFactory; dead after B43 |
| `OnFollowerAtmModeChanged_WithNamedBox` | ~L1625 | Wired to removed atmFactory; dead after B43 |
| `OnFollowerAtmModeChanged` | ~L1611 | Wired to removed atmFactory (B8 variant); dead after B43 |

---

### T1.9 — VERIFY OnApplyRule (zero diff expected)

Locate `OnApplyRule` in TradeCopierPanel.cs. Confirm it reads `item.AtmModeName` and calls
`ParseAtmModeNameLocal(item.AtmModeName)`. If confirmed, **no change needed** — zero diff on
this method. If it reads a tag slot instead, update to read `item.AtmModeName`.

---

### T1 — 7-SCAN CHECKLIST (engineer must run all 7, report zero hits)

```
SCAN-01: grep "lock("            in TradeCopierPanel.cs (new/modified code only) → zero results
SCAN-02: grep "async void"       in TradeCopierPanel.cs (new/modified code only) → zero results
SCAN-03: grep "return null"      in TradeCopierPanel.cs (new/modified code only) → zero results
         NOTE: FindAncestorDataContext uses return default(T) -- not return null. PASS.
SCAN-04: CYC audit (new/modified methods):
           OnFollowerAtmTemplateComboLoaded  CYC <= 4
           OnFollowerAtmTemplateComboChanged CYC <= 3
           GetLeaderAtmTemplateName          CYC <= 4
           FindAncestorDataContext<T>        CYC <= 3
SCAN-05: grep "init;"            in TradeCopierPanel.cs → zero results (no init accessors)
SCAN-06: grep "volatile double"  in TradeCopierPanel.cs → zero results
SCAN-07: grep "async void" (NT8-033 belt-and-suspenders, same as SCAN-02) → zero results
```

**Additional acceptance checks for T1:**
- `grep -n "OnFollowerAtmComboLoaded\|OnFollowerAtmModeChanged_WithNamedBox\|OnFollowerAtmModeChanged" src/PropTraderTools/TradeCopierPanel.cs` → zero results (removed handlers are gone)
- `grep -n "namedBoxFactory" src/PropTraderTools/TradeCopierPanel.cs` → zero results
- `ColumnDefinitions.Count == 5` in OnRowGridLoaded (was 6)

---

## TICKET T2 — TradeCopierWindow.cs: Replace ATM mode cluster with template ComboBox

**File:** `src/PropTraderTools/TradeCopierWindow.cs`
**Spec Requirement:** DW-B43-NAMED-TB-01 (eliminate Named ATM TextBox keyboard-bubbling defect)
**Plan Section:** §4.2 (all sub-sections)
**Net Change:** ~-25 lines (two sites modified: BuildRuleRow ~L314 and BuildDynamicRuleRow ~L477)

Changes apply identically in **both** `BuildRuleRow()` AND `BuildDynamicRuleRow()`. Do not miss
either site — the keyboard-bubbling defect exists in both the static and dynamic rule row paths.

---

### T2.1 — REMOVALS in BuildRuleRow() (~L314)

```
REMOVE: var atmCb = new ComboBox { Width = 80, Margin = ... }
REMOVE: atmCb.Items.Add("Inherit")
REMOVE: atmCb.Items.Add("Market")
REMOVE: atmCb.Items.Add("Named")
REMOVE: atmCb.SelectedIndex = 0
REMOVE: var namedBox = new TextBox { Width = 80, Visibility = Visibility.Collapsed, ... }
REMOVE: atmCb.SelectionChanged += (s, e) => { ... } (lambda showing/hiding namedBox)
REMOVE: var atmColPanel = new StackPanel { ... }
REMOVE: atmColPanel.Children.Add(atmCb)
REMOVE: atmColPanel.Children.Add(namedBox)
REMOVE: Grid.SetColumn(atmColPanel, 9); grid.Children.Add(atmColPanel)
REMOVE: applyBtn.Tag = new object[] { instrumentName, leaderCb, followerLb, atmCb, namedBox }  (5-element)
```

---

### T2.2 — ADDITIONS in BuildRuleRow() (~L314)

Replace removed block with:

```csharp
// B43 T2: ATM template ComboBox -- Col 9. Replaces atmCb + namedBox + atmColPanel.
// Width=120 to accommodate template names. First item "(none)" = Inherit fallback.
// No TextBox: eliminates DW-B43-NAMED-TB-01 keyboard-bubbling defect.
// try/catch: AtmStrategyTemplates may be unavailable before first F5 -- graceful fallback.
var atmTemplateCb = new ComboBox { Width = 120, Margin = new Thickness(2),
    ToolTip = "ATM template for this follower" };
atmTemplateCb.Items.Add("(none)");
try
{
    foreach (var t in NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyTemplates)
        atmTemplateCb.Items.Add(t.Name);
}
catch { /* NT8 API unavailable -- "(none)" only; document in NT8_COMPILER_RULES if fired */ }
atmTemplateCb.SelectedIndex = 0;
Grid.SetColumn(atmTemplateCb, 9);
grid.Children.Add(atmTemplateCb);
```

Update applyBtn.Tag to **4-element** (remove namedBox slot):
```csharp
applyBtn.Tag = new object[] { instrumentName, leaderCb, followerLb, atmTemplateCb };
```

---

### T2.3 — REMOVALS in BuildDynamicRuleRow() (~L477)

Same removals as T2.1, using the local variable names present in BuildDynamicRuleRow
(which may differ, e.g., `instrTextBox` instead of `instrumentName`). Remove the full
atmCb + namedBox + StackPanel cluster including the SelectionChanged lambda and Tag wiring.

---

### T2.4 — ADDITIONS in BuildDynamicRuleRow() (~L477)

Same additions as T2.2, using `atmTemplateCbDyn` as the local variable name:

```csharp
// B43 T2: Dynamic rule row ATM template ComboBox -- Col 9.
var atmTemplateCbDyn = new ComboBox { Width = 120, Margin = new Thickness(2),
    ToolTip = "ATM template for this follower" };
atmTemplateCbDyn.Items.Add("(none)");
try
{
    foreach (var t in NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyTemplates)
        atmTemplateCbDyn.Items.Add(t.Name);
}
catch { /* NT8 API unavailable -- "(none)" only */ }
atmTemplateCbDyn.SelectedIndex = 0;
Grid.SetColumn(atmTemplateCbDyn, 9);
grid.Children.Add(atmTemplateCbDyn);
```

Update applyBtn.Tag to **4-element**:
```csharp
applyBtn.Tag = new object[] { instrTextBox, leaderCb, followerLb, atmTemplateCbDyn };
```

---

### T2.5 — NEW METHOD: ParseAtmTemplateSelection (internal static)

**Signature:** `internal static FollowerAtmMode ParseAtmTemplateSelection(string sel)`
**CYC:** 2
**Rule constraints:** JS-021 (no lock), JS-002 (returns concrete FollowerAtmMode — never null),
JS-033 (no async void — pure static function)

```csharp
// B43 T2: Converts ATM template ComboBox selection to FollowerAtmMode.
// "(none)", null, or empty string -> Inherit; any other value -> Named(sel).
// Internal static for testability from B43Tests.cs (same assembly -- InternalsVisibleTo not needed).
// CYC=2: (1) none/null/empty -> Inherit, (2) else -> Named.
// JS-002: no return null -- always returns a concrete FollowerAtmMode subclass.
// JS-021: no lock. Pure function -- no state, no thread dependency.
internal static FollowerAtmMode ParseAtmTemplateSelection(string sel)
{
    if (string.IsNullOrEmpty(sel) || sel == "(none)")               // branch 1
        return new FollowerAtmMode.Inherit();
    return new FollowerAtmMode.Named(sel);                          // branch 2
}
```

---

### T2.6 — MODIFICATION: OnRowApply() (~L810)

**Remove** the old 5-element tag read pattern:
```csharp
// REMOVE this block:
if (tag.Length > 3 && tag[3] is ComboBox atmCb && atmCb.SelectedItem is string atmSel)
{
    string atmMode = atmSel;
    if (atmMode == "Named" && tag.Length > 4 && tag[4] is TextBox namedBox
        && namedBox.Text.Length > 0)
        atmMode = "Named:" + namedBox.Text;
    var mode = CopyEngine.ParseAtmModeName(atmMode);
    foreach (var acc in followers)
        atmMap[acc.Name] = mode;
}
```

**Replace with** the 4-element tag read pattern:
```csharp
// B43 T2: ATM template read from tag[3] (single ComboBox -- namedBox at tag[4] removed).
// ParseAtmTemplateSelection: CYC=2, no lock, no return null.
if (tag.Length > 3 && tag[3] is ComboBox atmTemplateCb)
{
    var sel = atmTemplateCb.SelectedItem as string ?? string.Empty;
    var mode = ParseAtmTemplateSelection(sel);
    foreach (var acc in followers)
        atmMap[acc.Name] = mode;
}
```

**CYC after change:** OnRowApply <= 4 (one sub-branch removed vs prior <= 5).

---

### T2 — 7-SCAN CHECKLIST (engineer must run all 7, report zero hits)

```
SCAN-01: grep "lock("            in TradeCopierWindow.cs (new/modified code only) → zero results
SCAN-02: grep "async void"       in TradeCopierWindow.cs (new/modified code only) → zero results
SCAN-03: grep "return null"      in TradeCopierWindow.cs (new/modified code only) → zero results
         NOTE: ParseAtmTemplateSelection returns FollowerAtmMode.Inherit() or .Named() -- never null. PASS.
SCAN-04: CYC audit (new/modified methods):
           ParseAtmTemplateSelection  CYC <= 2
           OnRowApply (updated)       CYC <= 4
SCAN-05: grep "init;"            in TradeCopierWindow.cs → zero results (no init accessors)
SCAN-06: grep "volatile double"  in TradeCopierWindow.cs → zero results
SCAN-07: grep "async void" (NT8-033 belt-and-suspenders, same as SCAN-02) → zero results
```

**Additional acceptance checks for T2:**
- `grep -n "namedBox\|atmCb\|atmColPanel" src/PropTraderTools/TradeCopierWindow.cs` → zero results (removed elements gone)
- `grep -n '"Inherit"\|"Market"\|"Named"' src/PropTraderTools/TradeCopierWindow.cs` → zero results in ATM column section (these strings no longer appear as ComboBox items)
- Both `BuildRuleRow` and `BuildDynamicRuleRow` use 4-element `applyBtn.Tag` arrays
- `OnRowApply` reads `tag[3]` as ComboBox (not StackPanel) and calls `ParseAtmTemplateSelection`

---

## TICKET T3 — B43Tests.cs: New xUnit test file (5 [Fact] methods)

**File:** `src/PropTraderTools/B43Tests.cs` (NEW FILE — alongside CopyEngineTests.cs)
**Spec Requirement:** DW-B43-NAMED-TB-01 (test coverage for new ATM template selection logic)
**Plan Section:** §4.3
**Framework:** xUnit ONLY. No NUnit. No MSTest.
  - BANNED: `using NUnit.Framework;`
  - BANNED: `using Microsoft.VisualStudio.TestTools.UnitTesting;`
  - BANNED: `[Test]`, `[TestMethod]`, `[TestFixture]`, `[TestClass]`
  - REQUIRED: `using Xunit;` and `[Fact]` on every test method

**Prerequisite:** T1 and T2 must be complete before T3 is written/compiled.
- `TradeCopierWindow.ParseAtmTemplateSelection` (T2) must exist as `internal static`
- `TradeCopierPanel.GetLeaderAtmTemplateName` (T1) must exist as `internal static`

---

### T3.1 — File Preamble

```csharp
// B43Tests.cs -- xUnit tests for B43 ATM template ComboBox changes.
// Framework: xUnit ONLY per TEST_FRAMEWORK_PROTOCOL.md.
// Tests T_B43_01 through T_B43_05.
using System;
using Xunit;
using NinjaTrader.Custom.AddOns.PttCopier;  // adjust namespace to match existing test files
```

Verify the correct namespace by checking the `using` block in the existing `CopyEngineTests.cs`
(or B42Tests.cs) and match exactly.

---

### T3.2 — Class Declaration

```csharp
public class B43Tests
{
    // ... [Fact] methods below
}
```

---

### T3.3 — [Fact] T_B43_01: OnRowApply_TemplateSelected_ProducesNamedMode

```csharp
[Fact]
public void OnRowApply_TemplateSelected_ProducesNamedMode()
{
    // Arrange
    string sel = "MES $200";

    // Act
    var result = TradeCopierWindow.ParseAtmTemplateSelection(sel);

    // Assert
    Assert.IsType<FollowerAtmMode.Named>(result);
    var named = (FollowerAtmMode.Named)result;
    Assert.Equal("MES $200", named.TemplateName);
}
```

**What it asserts:** A non-empty, non-"(none)" selection maps to `FollowerAtmMode.Named` with
`TemplateName` equal to the input string.

---

### T3.4 — [Fact] T_B43_02: OnRowApply_NoneSelected_ProducesInheritMode

```csharp
[Fact]
public void OnRowApply_NoneSelected_ProducesInheritMode()
{
    // Arrange
    string sel = "(none)";

    // Act
    var result = TradeCopierWindow.ParseAtmTemplateSelection(sel);

    // Assert
    Assert.IsType<FollowerAtmMode.Inherit>(result);
}
```

**What it asserts:** The sentinel string `"(none)"` maps to `FollowerAtmMode.Inherit`.

---

### T3.5 — [Fact] T_B43_03: OnRowApply_NullSelected_ProducesInheritMode

```csharp
[Fact]
public void OnRowApply_NullSelected_ProducesInheritMode()
{
    // Arrange
    string sel = null;

    // Act
    var result = TradeCopierWindow.ParseAtmTemplateSelection(sel);

    // Assert
    Assert.IsType<FollowerAtmMode.Inherit>(result);
}
```

**What it asserts:** A null argument maps to `FollowerAtmMode.Inherit` (JS-002 compliance
and `string.IsNullOrEmpty` guard confirmed).

---

### T3.6 — [Fact] T_B43_04: GetLeaderAtmTemplateName_NullChart_ReturnsEmptyString

```csharp
[Fact]
public void GetLeaderAtmTemplateName_NullChart_ReturnsEmptyString()
{
    // Act
    // GetLeaderAtmTemplateName is internal static -- no WPF instantiation required.
    // The first branch (chart == null) fires immediately and returns string.Empty.
    string result = TradeCopierPanel.GetLeaderAtmTemplateName(null);

    // Assert
    Assert.Equal(string.Empty, result);
}
```

**What it asserts:** Null chart input returns `string.Empty` without throwing (branch 1 of
`GetLeaderAtmTemplateName`). Confirms the null guard and JS-002 compliance.

**Note for engineer:** `GetLeaderAtmTemplateName` MUST be `internal static` (plan §4.1.5 confirms).
If the compiler cannot find the method from the test file, confirm the method accessibility is
`internal` (not `private`) and both files are in the same assembly.

---

### T3.7 — [Fact] T_B43_05: ParseAtmModeName_RoundTrip_BackwardCompat

```csharp
[Fact]
public void ParseAtmModeName_RoundTrip_BackwardCompat()
{
    // Arrange/Act/Assert: Named round-trip
    // CopyEngine is UNTOUCHED by B43. This test confirms existing serialization still works
    // after B43 because OnFollowerAtmTemplateComboChanged writes "Named:templateName" format
    // which must be parseable by the unchanged CopyEngine.
    var parsedNamed = CopyEngine.ParseAtmModeName("Named:MES $200");
    Assert.IsType<FollowerAtmMode.Named>(parsedNamed);
    Assert.Equal("MES $200", ((FollowerAtmMode.Named)parsedNamed).TemplateName);

    // Arrange/Act/Assert: Inherit round-trip
    var parsedInherit = CopyEngine.ParseAtmModeName("Inherit");
    Assert.IsType<FollowerAtmMode.Inherit>(parsedInherit);
}
```

**What it asserts:** The "Named:x" and "Inherit" serialization strings written by
`OnFollowerAtmTemplateComboChanged` and `OnFollowerAtmTemplateComboLoaded` are correctly parsed
by the unchanged `CopyEngine.ParseAtmModeName`, confirming backward compatibility with rules
saved before B43.

---

### T3 — 7-SCAN CHECKLIST (engineer must run all 7, report zero hits)

```
SCAN-01: grep "lock("            in B43Tests.cs → zero results
SCAN-02: grep "async void"       in B43Tests.cs → zero results
SCAN-03: grep "return null"      in B43Tests.cs → zero results
SCAN-04: CYC audit — all 5 [Fact] methods = CYC 1 (straight-line bodies, no branches)
SCAN-05: grep "init;"            in B43Tests.cs → zero results (no init accessors)
SCAN-06: grep "volatile double"  in B43Tests.cs → zero results
SCAN-07: grep "async void" (NT8-033 belt-and-suspenders, same as SCAN-02) → zero results
```

**Framework compliance check:**
```
grep "NUnit\|TestMethod\|TestFixture\|TestClass\|MSTest" B43Tests.cs → zero results
grep "\[Fact\]" B43Tests.cs → exactly 5 results
```

---

## Global Acceptance Criteria (all 3 tickets complete)

| # | Criterion | Verification Method |
|---|-----------|---------------------|
| AC-01 | `dotnet build`: zero errors, zero new warnings | `ctx_shell("dotnet build ...")` |
| AC-02 | `dotnet test`: 5/5 B43 [Fact] GREEN, all prior [Fact] still GREEN | `ctx_shell("dotnet test ...")` |
| AC-03 | No `TextBox` named `namedBox`/`namedBoxDyn`/`namedBoxFactory` in Panel or Window | grep scan |
| AC-04 | No ComboBox items `"Inherit"`, `"Market"`, `"Named"` in ATM column code (Panel/Window) | grep scan |
| AC-05 | `CopyEngine.cs` diff = 0 lines changed | `git diff src/PropTraderTools/CopyEngine.cs` |
| AC-06 | `OnRowGridLoaded` in Panel adds exactly 5 ColumnDefinitions (was 6) | code review |
| AC-07 | `applyBtn.Tag` in BOTH BuildRuleRow and BuildDynamicRuleRow is 4-element array | code review |
| AC-08 | Three old Panel handlers removed: `OnFollowerAtmComboLoaded`, `OnFollowerAtmModeChanged_WithNamedBox`, `OnFollowerAtmModeChanged` | grep scan |
| AC-09 | `GetLeaderAtmTemplateName` is `internal static` (visible from test file, no WPF required) | code review + T_B43_04 GREEN |
| AC-10 | `ParseAtmTemplateSelection` is `internal static` in TradeCopierWindow.cs | code review + T_B43_01..03 GREEN |
| AC-11 | BUILD_TAG updated to `PTT-COPIER B43 \| atm-template-picker \| <date>` | grep BUILD_TAG |
| AC-12 | `verify_links.ps1 -Fix` passes (hard-link sync for Wave workspace) | `powershell -File scripts\verify_links.ps1 -Fix` |

---

## Deferred Backlog (carry-forward from B42 — zero diff in B43)

| ID | Priority | Description | Carry Status |
|----|----------|-------------|--------------|
| DW-B42-01 | P2 | T_BUG_QX_BE_01 does not assert PTT-QX-T3 | Carry to B44 |
| DW-B42-02 | P1 | Live NT8 F5 verification of Quick All / BE All sequences | Carry to B44 |
| DW-B42-03 | P2 | IsPttQxTarget range extension for T4/T5 slots | Carry to B44 |
| DW-B42-04 | P2 | PttContracts.cs L254 comment NT8-NEW -> NT8-005 | Carry to B44 (PttContracts.cs NOT touched in B43) |
| DW-B42-05 | P1 | Live F5 verification of PTTFollowerStrategy ATM bracket spawn | Carry to B44 |

---

*Tickets written by ptt-architect from REVIEW_PASS plan. Ready for ptt-engineer execution.*
