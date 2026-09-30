# B43-LaneA — Architecture Plan
**Block:** PTT-COPIER-B43 (Per-Follower ATM Template ComboBox)
**Epic:** atm-template-picker
**Phase:** 1 — Architecture
**Status:** REVIEW_PASS
**Architect:** ptt-architect
**Prior block:** B42-LaneA (F5 GREEN 2026-08-05, BUILD_TAG `PTT-COPIER B42 | qx-be-interaction | 2026-08-05`)
**Spec requirement:** DW-B43-NAMED-TB-01 — eliminate Named ATM TextBox keyboard-bubbling defect

---

## 1. One-Liner

Replace the broken Named ATM TextBox and the confusing Inherit/Market/Named mode ComboBox with a
single ATM template ComboBox per follower row (Panel) and per rule row (Window), populated from
`NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyTemplates`, default = leader's current ChartTrader
template, per-follower override supported.

---

## 2. Defect Fixed

**DW-B43-NAMED-TB-01** — Root cause: keystrokes typed into an embedded WPF TextBox inside the NT8
chart panel host bubble up through the WPF visual tree to the chart's global KeyDown handler, which
intercepts any printable character and opens the NT8 instrument search popup. The Named TextBox has
no `PreviewKeyDown` handler to stop the bubbling.

**Fix:** Eliminate the TextBox entirely. Replace with a ComboBox (mouse-driven — no keyboard-bubbling
problem because ComboBox selection uses mouse clicks, not printable keystroke input).

---

## 3. Files Affected

| File | Change Type | Net Change |
|------|-------------|------------|
| `src/PropTraderTools/TradeCopierPanel.cs` | Modify | ~35 lines net |
| `src/PropTraderTools/TradeCopierWindow.cs` | Modify | ~-25 lines net |
| `src/PropTraderTools/B43Tests.cs` | NEW | ~90 lines |

**Files NOT touched (zero diff required):**
`CopyEngine.cs`, `PttContracts.cs`, `PttBus.cs`, `PTTFollowerStrategy.cs`, `TradeCopierAddOn.cs`,
all other `src/PropTraderTools/*.cs` files.

---

## 4. Component List and Method Signatures

### 4.1 TradeCopierPanel.cs — T1

#### 4.1.1 BuildCheckItemTemplate() — MODIFIED (existing method, ~L1498)

**Remove from FEF children:**
- `atmFactory` — FrameworkElementFactory(typeof(ComboBox)) wired to `OnFollowerAtmComboLoaded` +
  `OnFollowerAtmModeChanged_WithNamedBox` (B8/B9 pattern)
- `namedBoxFactory` — FrameworkElementFactory(typeof(TextBox)) with Collapsed visibility

**Add to FEF children (in place of the above, col 3 slot):**
- `atmTemplateFactory` — FrameworkElementFactory(typeof(ComboBox))
  - `atmTemplateFactory.SetValue(Grid.ColumnProperty, 3)` — col 3
  - `atmTemplateFactory.AddHandler(ComboBox.LoadedEvent, new RoutedEventHandler(OnFollowerAtmTemplateComboLoaded))`
  - `atmTemplateFactory.AddHandler(ComboBox.SelectionChangedEvent, new SelectionChangedEventHandler(OnFollowerAtmTemplateComboChanged))`

**Updated FEF AppendChild order:**
`nameFactory` (col 0) → `pnlFactory` (col 1) → `multFactory` (col 2) →
`atmTemplateFactory` (col 3) → `chkFactory` (col 4)

**Column property updates:**
- `chkFactory.SetValue(Grid.ColumnProperty, 4)` — was 5, now 4 (namedBox col removed)

#### 4.1.2 OnRowGridLoaded() — MODIFIED (existing method, ~L1569)

**Change:** 5 ColumnDefinitions (was 6 — namedBox col removed).

```
Col 0: Star width, MinWidth 80    -- account name
Col 1: 62px fixed                 -- daily P&L
Col 2: 30px fixed                 -- multiplier TextBox
Col 3: 120px fixed                -- ATM template ComboBox (was 80px; wider for template names)
Col 4: 20px fixed                 -- checkbox
```

Remove the line adding the old 80px Col 4 (namedBox). Keep all other col definitions. Update
checkbox column from index 5 to index 4 if referenced (it is set via FEF SetValue above, not
in OnRowGridLoaded directly, but the count must match the FEF children that reference columns).

#### 4.1.3 OnFollowerAtmTemplateComboLoaded — NEW

```csharp
// B43 T1: ATM template ComboBox Loaded handler.
// Fires on WPF UI thread (DataTemplate instantiation). No Dispatcher needed.
// Idempotency: items.Count > 0 guard prevents double-population on re-layout.
// Populates: "(none)" sentinel + AtmStrategyTemplates list.
// Default: leader's current ChartTrader ATM template if found; else index 0.
// CYC=4: (1) null guard, (2) idempotency guard, (3) foreach loop, (4) leader-default branch.
// JS-021: no lock. JS-002: no return null (writes AtmModeName or falls through).
// NT8-012: Loaded event pattern (FEF constraint -- see B10-UI-01).
private void OnFollowerAtmTemplateComboLoaded(object sender, RoutedEventArgs e)
```

**Signature:** `private void OnFollowerAtmTemplateComboLoaded(object sender, RoutedEventArgs e)`
**CYC:** 4
**Logic:**
1. `if (cb == null) return` — branch 1
2. `if (cb.Items.Count > 0) return` — idempotency guard, branch 2
3. `cb.Items.Add("(none)")`
4. `string leaderTemplate = GetLeaderAtmTemplateName(_currentChart)` — read current default
5. `int defaultIdx = 0`
6. `foreach (var t in NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyTemplates)` — branch 3 (loop)
   - `cb.Items.Add(t.Name)`
   - `if (t.Name == leaderTemplate) defaultIdx = cb.Items.Count - 1` — sets default when matched
7. `cb.SelectedIndex = defaultIdx` — branch 4: leader found (defaultIdx > 0) or fallback to 0

#### 4.1.4 OnFollowerAtmTemplateComboChanged — NEW

```csharp
// B43 T1: ATM template ComboBox SelectionChanged handler.
// Fires on WPF UI thread. Writes item.AtmModeName in "Inherit" or "Named:templateName" format.
// Serialization format preserved: CopyEngine.ParseAtmModeName handles these unchanged.
// CYC=3: (1) cb null guard, (2) item null guard, (3) "(none)" branch.
// JS-021: no lock. JS-002: no return null.
private void OnFollowerAtmTemplateComboChanged(object sender, SelectionChangedEventArgs e)
```

**Signature:** `private void OnFollowerAtmTemplateComboChanged(object sender, SelectionChangedEventArgs e)`
**CYC:** 3
**Logic:**
1. `var cb = sender as ComboBox; if (cb == null) return` — branch 1
2. `var item = cb.DataContext as FollowerItem ?? FindAncestorDataContext<FollowerItem>(cb); if (item == null) return` — branch 2
3. `var sel = cb.SelectedItem as string ?? string.Empty`
4. `item.AtmModeName = (sel == "(none)" || sel.Length == 0) ? "Inherit" : "Named:" + sel` — branch 3

#### 4.1.5 GetLeaderAtmTemplateName — NEW (internal static)

```csharp
// B43 T1: Reads the ATM template name currently selected in ChartTrader for the given chart.
// Static so it can be called from tests without WPF instantiation.
// Walking: chart -> FindVisualChild<ChartTrader> -> walk ComboBoxes -> find ATM-strategy selector.
// ATM selector heuristic: ComboBox whose SelectedItem is a string (not Account, not Instrument).
// Returns string.Empty on any null/exception -- never throws.
// CYC=3: (1) chart null guard, (2) chartTrader null guard, (3) ComboBox found/not found.
// NT8-008: Chart.ChartControl does not exist -- use FindVisualChild<ChartTrader> instead.
// NT8-041: Reflection on ChartControl.Charts fails -- visual tree walk is the safe path.
internal static string GetLeaderAtmTemplateName(Chart currentChart)
```

**Signature:** `internal static string GetLeaderAtmTemplateName(Chart currentChart)`
**Return:** `string` — selected ATM template name, or `string.Empty` if not found/null chart
**CYC:** 4
**Logic:**
1. `if (currentChart == null) return string.Empty` — branch 1
2. `var ct = TradeCopierAddOn.FindVisualChild<ChartTrader>(currentChart); if (ct == null) return string.Empty` — branch 2
3. Wrap in try/catch; walk visual tree of `ct` for all ComboBoxes whose `SelectedItem is string s && s.Length > 0 && !(SelectedItem is NinjaTrader.Cbi.Account)`:
   - If found → return `s`
   - Else → return `string.Empty` — branch 3
4. Catch → return `string.Empty`

**ATM ComboBox identification heuristic:** The ChartTrader visual tree contains ComboBoxes for:
- Index 0: Instrument (SelectedItem is Instrument)
- Index 1: Account (SelectedItem is Account)
- Index 2+: ATM strategy template selector (SelectedItem is string or null)

Use `FindVisualChildByIndex<ComboBox>(ct, 2)` as the direct approach. If SelectedItem is null
(no template selected), return `string.Empty`. This is a deterministic index-based approach
(same as FindAccountComboBox at index 1) and is safer than type-guessing.

#### 4.1.6 FindAncestorDataContext<T> — NEW (private static)

```csharp
// B43 T1: Walks the visual tree UPWARD from child, returning the DataContext of the first
// ancestor whose DataContext is of type T. Fallback for FEF-instantiated templates where
// DataContext may be set on an ancestor Grid rather than the control directly.
// CYC=3: (1) child null guard, (2) while loop, (3) DataContext cast check.
// JS-021: no lock. JS-002: returns default(T) -- no return null.
private static T FindAncestorDataContext<T>(DependencyObject child) where T : class
```

**Signature:** `private static T FindAncestorDataContext<T>(DependencyObject child) where T : class`
**Return:** `T` or `default(T)` (null for class T — acceptable for visual tree walkers; callers null-guard)
**CYC:** 3
**Logic:**
1. `if (child == null) return default(T)` — branch 1
2. `var parent = VisualTreeHelper.GetParent(child)`
3. `while (parent != null)` — branch 2 (loop)
   - `if (parent is FrameworkElement fe && fe.DataContext is T ctx) return ctx` — branch 3
   - `parent = VisualTreeHelper.GetParent(parent)`
4. `return default(T)`

#### 4.1.7 Handlers REMOVED

The following methods are **removed entirely** from TradeCopierPanel.cs:

| Method | Location | Reason |
|--------|----------|--------|
| `OnFollowerAtmComboLoaded` | ~L1600 | Wired to removed atmFactory; dead after B43 |
| `OnFollowerAtmModeChanged_WithNamedBox` | ~L1625 | Wired to removed atmFactory; dead after B43 |
| `OnFollowerAtmModeChanged` | ~L1611 | Wired to removed atmFactory (B8 variant); dead after B43 |

#### 4.1.8 OnApplyRule — NO CHANGE

Reads `item.AtmModeName` and calls `ParseAtmModeNameLocal(item.AtmModeName)`. The format
"Inherit" / "Named:templateName" is unchanged. Zero diff on this method.

---

### 4.2 TradeCopierWindow.cs — T2

Changes applied in BOTH `BuildRuleRow()` (~L314) and `BuildDynamicRuleRow()` (~L477).

#### 4.2.1 BuildRuleRow() — MODIFIED

**Remove:**
- `var atmCb = new ComboBox { Width = 80 }` + `.Items.Add("Inherit"/"Market"/"Named")` + `SelectedIndex = 0`
- `var namedBox = new TextBox { Width = 80, Visibility.Collapsed }` + `atmCb.SelectionChanged` lambda
- `var atmColPanel = new StackPanel` + `atmColPanel.Children.Add(atmCb)` + `atmColPanel.Children.Add(namedBox)` + `Grid.SetColumn(atmColPanel, 9)` + `grid.Children.Add(atmColPanel)`
- `applyBtn.Tag = new object[] { instrumentName, leaderCb, followerLb, atmCb, namedBox }` — 5-element tag

**Add:**
```csharp
// B43 T2: ATM template ComboBox -- Col 9. Replaces atmCb+namedBox+atmColPanel.
// ItemsSource = AtmStrategyTemplates string list. First item "(none)" = Inherit fallback.
// No TextBox: eliminates DW-B43-NAMED-TB-01 keyboard-bubbling defect.
var atmTemplateCb = new ComboBox { Width = 120, Margin = new Thickness(2) };
atmTemplateCb.Items.Add("(none)");
try
{
    foreach (var t in NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyTemplates)
        atmTemplateCb.Items.Add(t.Name);
}
catch { /* NT8 API unavailable -- only "(none)" will show */ }
atmTemplateCb.SelectedIndex = 0;
Grid.SetColumn(atmTemplateCb, 9);
grid.Children.Add(atmTemplateCb);
```

**Update applyBtn.Tag** (4-element, remove namedBox slot):
```csharp
applyBtn.Tag = new object[] { instrumentName, leaderCb, followerLb, atmTemplateCb };
```

#### 4.2.2 BuildDynamicRuleRow() — MODIFIED

Same removals and additions as BuildRuleRow, using `atmTemplateCbDyn` as the local variable name.
`applyBtn.Tag = new object[] { instrTextBox, leaderCb, followerLb, atmTemplateCbDyn }`

#### 4.2.3 ParseAtmTemplateSelection — NEW (internal static)

```csharp
// B43 T2: Converts ATM template ComboBox selection to FollowerAtmMode.
// "(none)", null, or empty string -> Inherit; any other value -> Named(sel).
// Internal static for testability from B43Tests.cs (same assembly).
// CYC=2: (1) none/null/empty branch -> Inherit, (2) else -> Named.
// JS-002: no return null -- always returns a concrete FollowerAtmMode subclass.
// JS-021: no lock. Pure function.
internal static FollowerAtmMode ParseAtmTemplateSelection(string sel)
```

**Signature:** `internal static FollowerAtmMode ParseAtmTemplateSelection(string sel)`
**Return:** `FollowerAtmMode` — never null
**CYC:** 2
**Logic:**
1. `if (string.IsNullOrEmpty(sel) || sel == "(none)") return new FollowerAtmMode.Inherit()` — branch 1
2. `return new FollowerAtmMode.Named(sel)` — branch 2

#### 4.2.4 OnRowApply() — MODIFIED (~L810)

**Remove:** tag[3] as ComboBox atmCb + `atmSel` + `atmMode == "Named"` sub-branch + `tag[4]` namedBox read.

**Replace with:**
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

**CYC after change:** ≤5 (was ≤5 with Named sub-branch; now ≤4 — one branch removed)

---

### 4.3 B43Tests.cs — T3 (NEW FILE)

**Path:** `src/PropTraderTools/B43Tests.cs`
**Convention:** Same as B42Tests.cs — xUnit [Fact], no NUnit, no MSTest (per TEST_FRAMEWORK_PROTOCOL.md)

#### 5 [Fact] methods:

**T_B43_01: `OnRowApply_TemplateSelected_ProducesNamedMode`**
```
TradeCopierWindow.ParseAtmTemplateSelection("MES $200")
Assert: result is FollowerAtmMode.Named n && n.TemplateName == "MES $200"
```

**T_B43_02: `OnRowApply_NoneSelected_ProducesInheritMode`**
```
TradeCopierWindow.ParseAtmTemplateSelection("(none)")
Assert: result is FollowerAtmMode.Inherit
```

**T_B43_03: `OnRowApply_NullSelected_ProducesInheritMode`**
```
TradeCopierWindow.ParseAtmTemplateSelection(null)
Assert: result is FollowerAtmMode.Inherit
```

**T_B43_04: `GetLeaderAtmTemplateName_NullChart_ReturnsEmptyString`**
```
TradeCopierPanel.GetLeaderAtmTemplateName(null)
Assert: result == string.Empty
Note: GetLeaderAtmTemplateName is internal static -- no WPF instantiation required.
```

**T_B43_05: `ParseAtmModeName_RoundTrip_BackwardCompat`**
```
CopyEngine.ParseAtmModeName("Named:MES $200")
Assert: result is FollowerAtmMode.Named n && n.TemplateName == "MES $200"
CopyEngine.ParseAtmModeName("Inherit")
Assert: result is FollowerAtmMode.Inherit
Note: Verifies serialization format written by OnFollowerAtmTemplateComboChanged ("Named:x")
      is correctly parsed by the unchanged CopyEngine round-trip.
```

---

## 5. Data Flow

```
[ChartTrader visual tree]
        |
        v
GetLeaderAtmTemplateName(_currentChart)  -- reads NT8 ATM selector by visual tree index
        |
        v  (returns template name string or string.Empty)
        |
[AtmStrategyTemplates API] --------> OnFollowerAtmTemplateComboLoaded
        |                                  |
        |  populates items list            |  fires on UI thread (DataTemplate Loaded event)
        |                                  |  CYC=4: null+idempotency+loop+default branch
        v                                  v
   ComboBox shows "(none)" + template names + default selection
        |
        | user selects template (mouse only -- no keyboard bubbling)
        v
OnFollowerAtmTemplateComboChanged
   item.AtmModeName = "Inherit" | "Named:templateName"    CYC=3
        |
        v
OnApplyRule reads item.AtmModeName
        |
        v  (unchanged path -- B42 and prior)
ParseAtmModeNameLocal("Named:templateName") -> FollowerAtmMode.Named
        |
        v
CopyEngine.AddRule / SyncFollowerBracket (unchanged)
```

**Window path (TradeCopierWindow):**
```
atmTemplateCb.SelectedItem (string)
        |
        v
ParseAtmTemplateSelection(sel) -> FollowerAtmMode.Inherit | .Named   CYC=2
        |
        v
_engine.AddRule(name, leader, followers, multipliers, atmMap) (unchanged)
```

---

## 6. NinjaTrader 8 API Usage

### 6.1 AtmStrategyTemplates

**Primary API:** `NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyTemplates`
- Returns a collection of ATM strategy template objects, each with a `Name` property (string).
- Accessed on UI thread (safe -- NT8 ATM API is UI-thread accessible).
- **F5 VERIFY required:** If CS0117/CS0246 at F5 time, use filesystem fallback (see §6.2).

**Usage site:** `OnFollowerAtmTemplateComboLoaded` (Panel) and `BuildRuleRow`/`BuildDynamicRuleRow` (Window)

### 6.2 AtmStrategyTemplates Filesystem Fallback

If `NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyTemplates` fails to compile at F5:

```csharp
// Fallback: enumerate from NT8 template filesystem path
string templateDir = System.IO.Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
    "NinjaTrader 8", "templates", "AtmStrategy");
if (System.IO.Directory.Exists(templateDir))
{
    foreach (var f in System.IO.Directory.GetFiles(templateDir, "*.xml"))
        cb.Items.Add(System.IO.Path.GetFileNameWithoutExtension(f));
}
```

Requires: `using System.IO;` at file top (if not already present). Using System already present.

**Engineer decision:** Attempt NT8 API first. Document result in NT8_COMPILER_RULES if fallback needed.

### 6.3 ChartTrader ATM Selector (GetLeaderAtmTemplateName)

**NT8-008 applies:** `Chart.ChartControl` does not exist. Use `TradeCopierAddOn.FindVisualChild<ChartTrader>(chart)` instead.

**NT8-041 applies:** `ChartControl.Charts` not accessible via reflection. Visual tree walk is the safe path.

**ATM ComboBox identification:** Use `TradeCopierAddOn.FindVisualChildByIndex<ComboBox>(ct, 2)` — index 2 is the ATM strategy selector in ChartTrader (index 0 = Instrument, index 1 = Account per B18 discovery). Wrap in try/catch returning `string.Empty` on any exception.

---

## 7. Threading Model

All B43 methods execute on the **WPF UI thread**. No cross-thread access. No Dispatcher.InvokeAsync needed.

| Method | Thread | Justification |
|--------|--------|---------------|
| `OnFollowerAtmTemplateComboLoaded` | UI | WPF DataTemplate Loaded event fires on UI thread |
| `OnFollowerAtmTemplateComboChanged` | UI | ComboBox SelectionChanged fires on UI thread |
| `GetLeaderAtmTemplateName` | UI | Caller (OnFollowerAtmTemplateComboLoaded) is on UI thread; VisualTreeHelper requires UI thread |
| `FindAncestorDataContext<T>` | UI | VisualTreeHelper.GetParent requires UI thread; called from UI-thread handlers only |
| `ParseAtmTemplateSelection` | Any | Pure static function, no state, no thread dependency |
| `OnRowApply` (updated) | UI | Button.Click fires on UI thread |

**No volatile fields modified in B43.** `_currentChart` is read (not written) by `GetLeaderAtmTemplateName`
— it is a `volatile` field set on the UI thread, safe to read on UI thread. ✅

**NT8-042 check:** No `Dispatcher.InvokeAsync` calls added in B43. Existing `this.Dispatcher.InvokeAsync`
in `OnStatusUpdate` is the panel's own WPF dispatcher (safe — not the banned NT8 path). ✅

---

## 8. NT8 Compiler Rules Applied

| Rule | Applies To | Verdict |
|------|------------|---------|
| NT8-001 (`init` banned) | No new properties with `init` | PASS |
| NT8-002 (record banned) | No records — all abstract class pattern per FollowerAtmMode existing design | PASS |
| NT8-006 (`using System.Linq` required for `.Any()`) | Not used in B43 | N/A |
| NT8-012 (FEF Loaded event required) | `OnFollowerAtmTemplateComboLoaded` wired to `ComboBox.LoadedEvent` | PASS |
| NT8-018 / JS-021 (`lock()` banned) | No lock() anywhere in B43 | PASS |
| NT8-019 / JS-033 (`async void` banned) | All handlers are synchronous void | PASS |
| NT8-028 (hex color ban) | No new SolidColorBrush in B43 | N/A |
| NT8-041 (reflection on Charts) | Not attempted; visual tree walk used | PASS |
| NT8-042 (`Dispatcher.InvokeAsync` ban) | No new InvokeAsync calls in B43 | PASS |
| NT8-044 (`StringComparison` needs `using System`) | `using System` already at top of TradeCopierPanel.cs | PASS |
| JS-002 (`return null` ban) | `GetLeaderAtmTemplateName` returns `string.Empty`; `ParseAtmTemplateSelection` returns concrete FollowerAtmMode instance | PASS |

---

## 9. Jane Street Rules Pre-check

| Rule | Applied Where | Verdict |
|------|---------------|---------|
| JS-021 (`lock()` banned) | No lock() in any new method | PASS |
| JS-001 (throw in dispatch banned) | No throw in any new method; try/catch pattern used for API guard | PASS |
| JS-002 (`return null` banned) | `GetLeaderAtmTemplateName` → `string.Empty`; `ParseAtmTemplateSelection` → concrete subclass | PASS |
| JS-033 (`async void` banned) | All handlers synchronous void | PASS |
| ASCII-only | All string literals are ASCII; "(none)", "Inherit", "Named:" prefixes are ASCII | PASS |

---

## 10. CYC Budget

| Method | File | CYC | Budget ≤8 |
|--------|------|-----|-----------|
| `OnFollowerAtmTemplateComboLoaded` | Panel | 4 | ✅ |
| `OnFollowerAtmTemplateComboChanged` | Panel | 3 | ✅ |
| `GetLeaderAtmTemplateName` | Panel | 4 | ✅ |
| `FindAncestorDataContext<T>` | Panel | 3 | ✅ |
| `ParseAtmTemplateSelection` | Window | 2 | ✅ |
| `OnRowApply` (updated) | Window | ≤4 | ✅ |

---

## 11. Serialization Compatibility

**AtmModeName field format (FollowerItem) — UNCHANGED:**
- `"Inherit"` — use Inherit mode (no ATM management override)
- `"Named:templateName"` — use Named ATM template override
- `"Market"` — use Market order override (NOT exposed via B43 UI but still parsed by engine)

**ParseAtmModeNameLocal / CopyEngine.ParseAtmModeName — ZERO DIFF:**
Both parse functions remain unchanged. Existing saved rules in "Named:xxx" format will continue
to load and function correctly (backward compatibility).

**"Market" mode removal from UI:** The Inherit/Market/Named ComboBox is removed. Users can no
longer select "Market" via the UI. Existing rules serialized with "Market" continue to function
via ParseAtmModeName in CopyEngine. This is intentional — Market mode was unused in practice.

---

## 12. Deferred Backlog (carry-forward from B42)

| ID | Priority | Description | Status |
|----|----------|-------------|--------|
| DW-B42-01 | P2 | T_BUG_QX_BE_01 does not assert PTT-QX-T3 | Carry to B44 |
| DW-B42-02 | P1 | Live NT8 F5 verification of Quick All / BE All sequences | Carry to B44 |
| DW-B42-03 | P2 | IsPttQxTarget range extension for T4/T5 slots | Carry to B44 |
| DW-B42-04 | P2 | PttContracts.cs L254 comment NT8-NEW → NT8-005 | Carry to B44 (PttContracts.cs NOT touched in B43) |
| DW-B42-05 | P1 | Live F5 verification of PTTFollowerStrategy ATM bracket spawn | Carry to B44 |

---

## 13. Dependency Order

```
T1 (Panel) ────┐
               ├──> (parallel) ──> T3 (Tests, depends on T1+T2 complete)
T2 (Window) ───┘
```

- T1 and T2 are **independent** — no shared new symbols, no shared new methods.
- T3 depends on:
  - `TradeCopierWindow.ParseAtmTemplateSelection` (T2 — must be written first)
  - `TradeCopierPanel.GetLeaderAtmTemplateName` (T1 — must be `internal static`, no WPF instantiation in tests)
  - `CopyEngine.ParseAtmModeName` (pre-existing, unchanged)

---

## 14. Acceptance Criteria

| # | Criterion | Verification |
|---|-----------|--------------|
| AC-01 | `dotnet build`: zero errors, zero new warnings | `ctx_shell("dotnet build ...")` |
| AC-02 | `dotnet test`: 5/5 [Fact] GREEN, all prior [Fact] still GREEN | `ctx_shell("dotnet test ...")` |
| AC-03 | SCAN-01: `grep -r "lock(" src/ --include="*.cs"` → zero hits in changed files | Manual scan |
| AC-04 | SCAN-02: `grep -rn "async void " src/ --include="*.cs"` → zero hits in changed files | Manual scan |
| AC-05 | No `TextBox` named `namedBox` / `namedBoxDyn` / `namedBoxFactory` in Panel or Window | Manual scan |
| AC-06 | No `ComboBox` items `"Inherit"`, `"Market"`, `"Named"` in Panel/Window ATM column factory | Manual scan |
| AC-07 | `CopyEngine.cs` diff = 0 lines changed | `git diff src/PropTraderTools/CopyEngine.cs` |
| AC-08 | `OnRowGridLoaded` adds exactly 5 ColumnDefinitions (was 6) | Code review |
| AC-09 | `applyBtn.Tag` in both BuildRuleRow and BuildDynamicRuleRow is 4-element array | Code review |
| AC-10 | BUILD_TAG updated to `PTT-COPIER B43 \| atm-template-picker \| <date>` | Grep BUILD_TAG |

---

## 15. BUILD_TAG

```
PTT-COPIER B43 | atm-template-picker | <date>
```

Update in: `src/PropTraderTools/TradeCopierAddOn.cs` (or wherever BUILD_TAG is defined — search `PTT-COPIER B42`).

---

## 16. 7-Scan Engineer Contract

| Scan ID | Pattern | Expected | Applies To |
|---------|---------|----------|------------|
| SCAN-01 | `lock\s*\(` | 0 hits in new/modified methods | Panel, Window |
| SCAN-02 | `async\s+void\s+\w+\(` | 0 hits in new/modified methods | Panel, Window |
| SCAN-03 | `DateTime\.Now[^U]` | 0 hits in new/modified methods | Panel, Window |
| SCAN-04 | `return\s+null\s*;` | 0 hits (string.Empty / default(T) used instead) | Panel, Window |
| SCAN-05 | `OnFollowerAtmComboLoaded\|OnFollowerAtmModeChanged` | 0 hits (handlers fully removed) | Panel |
| SCAN-06 | `"#[0-9A-Fa-f]{6}"` | 0 hits in new code | Panel, Window |
| SCAN-07 | CYC ≤ 8 for all new/modified methods | max CYC = 4 (confirmed in §10) | Panel, Window |

---

*Plan written by ptt-architect. Ready for ptt-plan-reviewer review.*
