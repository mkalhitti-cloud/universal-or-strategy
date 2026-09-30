# BGTM-1 Ticket 4 — Completion Report

**Ticket**: BGTM-1 Ticket 4 — TradeCopierWindow.cs License UI  
**File Modified**: `src/PropTraderTools/TradeCopierWindow.cs`  
**Engineer**: ptt-engineer  
**Date**: 2026-08-26  
**Result**: BUILD_PASS

---

## What Was Implemented

### A) New Fields Added (after line 53, after `_armBeBtns`)

```csharp
// BGTM-1: Add Rule button field (promoted from local so ApplyFeatureFlags can gate it)
private Button _addRuleBtn;

// BGTM-1: Copy mode ComboBox field (promoted from local so ApplyFeatureFlags can gate Mirror)
private ComboBox _modeCb;

// BGTM-1: License UI controls
private System.Windows.Controls.TextBox _licenseKeyBox;
private System.Windows.Controls.TextBlock _licenseStatusText;
private System.Windows.Controls.Button _activateBtn;

private static readonly string LicenseTxtPath = System.IO.Path.Combine(
    NinjaTrader.Core.Globals.UserDataDir,
    "PropTraderTools",
    "license.txt");
```

### B) Local Variable Promotions in BuildUI()

- `var modeCb` → `_modeCb` (field assignment) — required for `ApplyFeatureFlags` mirror gate
- `var addRuleBtn` → `_addRuleBtn` (field assignment) — required for `ApplyFeatureFlags` multi-rule gate

### C) BuildLicenseRow(Panel parent) — new method (line ~333)

CYC=1. Appends a horizontal `StackPanel` row to the `DockPanel root` containing:
- `Label { Content = "LICENSE" }` (Width=70)
- `_licenseKeyBox` (TextBox, Width=200)
- `_activateBtn` (Button, Content="Activate", Click → OnActivateClick)
- `_licenseStatusText` (TextBlock)

Called from `BuildUI()` immediately before `root.Children.Add(logScroll)` so the row docks above the log area.

### D) OnActivateClick(object sender, RoutedEventArgs e) — new method (line ~379)

CYC=1. Sequential: reads key, try/catch writes `license.txt`, validates via `LicenseClient.Validate`, calls `CopyEngine.Instance.SetFlags`, `ApplyFeatureFlags`, updates `_licenseStatusText.Text`.

### E) ApplyFeatureFlags(FeatureFlags f) — new method (line ~397)

CYC=1. Straight-line foreach loops + null-guarded field assignments:
- `_trimBtns` → `f.TrimFlatten`
- `_flattenBtns` → `f.TrimFlatten`
- `_cancelBtns` → `f.TrimFlatten`
- `_beBtns` → `f.BreakEven`
- `_modeCb` → `f.MirrorMode` (null-guarded)
- `_addRuleBtn` → `f.MultiRule` (null-guarded)
- ToolTip set on each disabled control.

### F) LoadLicenseKeyDisplay() — new method (line ~432)

CYC=2. try/catch reads `license.txt` into `_licenseKeyBox.Text`. Sets `_licenseStatusText.Text = GetStatusText(CopyEngine.Instance.Flags)` after.

### G) OnFeatureFlagsChanged(FeatureFlags f) — new method (line ~448)

CYC=1. Calls `ApplyFeatureFlags(f)` + sets `_licenseStatusText.Text = GetStatusText(f)`. Runs on UI thread (per architecture plan Section 12 — no Dispatcher needed).

### H) GetStatusText(FeatureFlags f) — new static method (line ~455)

CYC=3. Returns `"ELITE"` / `"PRO"` / `"STARTER"` based on `f.AtrSizing` / `f.MultiRule` flags.

### I) OnLoaded Wiring (line ~151)

Appended to existing `OnLoaded` body (after `RefreshRuleRows()` block):
```csharp
CopyEngine.Instance.FeatureFlagsChanged += OnFeatureFlagsChanged;
ApplyFeatureFlags(CopyEngine.Instance.Flags);
LoadLicenseKeyDisplay();
```

### J) OnWindowClosed Wiring (line ~181)

Appended to existing `OnWindowClosed` body:
```csharp
CopyEngine.Instance.FeatureFlagsChanged -= OnFeatureFlagsChanged;
```

---

## Field Name Mappings (Spec Name → Actual Field Name)

| Spec Name | Actual Field Name | Type | Notes |
|-----------|-------------------|------|-------|
| `_trimBtn` | `_trimBtns` | `List<Button>` | Multiple per-rule trim buttons |
| `_flattenBtn` | `_flattenBtns` | `List<Button>` | Multiple per-rule flatten buttons |
| `_cancelBtn` | `_cancelBtns` | `List<Button>` | Multiple per-rule cancel buttons |
| `_beBtn` | `_beBtns` | `List<Button>` | Multiple per-rule BE buttons |
| `_mirrorRadio` | `_modeCb` | `ComboBox` | Copy mode ComboBox (Signal/Mirror/Clone), promoted from local |
| `_addRuleBtn` | `_addRuleBtn` | `Button` | Promoted from local `var addRuleBtn` in BuildUI |
| `_clickTraderRow` | N/A | — | Not present in TradeCopierWindow.cs; in TradeCopierPanel.cs (Ticket 5) |
| `_atrRow` | N/A | — | Not present in TradeCopierWindow.cs; in TradeCopierPanel.cs (Ticket 5) |
| `_qxBtn` | N/A | — | Not present in TradeCopierWindow.cs; in TradeCopierPanel.cs (Ticket 5) |

**ApplyFeatureFlags implementation note**: The spec wiring table items `_clickTraderRow`, `_atrRow`, and `_qxBtn` are not fields in TradeCopierWindow.cs — these belong to TradeCopierPanel.cs (Ticket 5). TradeCopierWindow.cs `ApplyFeatureFlags` gates only the controls that exist in this window: trim/flatten/cancel button lists, copy mode ComboBox, and add rule button.

---

## 7-Scan Results

| Scan | Command | Result |
|------|---------|--------|
| SCAN-01 | `Select-String ... -Pattern "lock\s*\("` (non-comment lines) | **0 hits** |
| SCAN-02 | `Select-String ... -Pattern "throw\s+new\s+"` in new BGTM-1 methods | **0 hits** (1 pre-existing in AccountDisplayConverter.ConvertBack at L1007 — not touched by this ticket) |
| SCAN-03 | All 6 new methods present: `OnActivateClick`, `ApplyFeatureFlags`, `LoadLicenseKeyDisplay`, `OnFeatureFlagsChanged`, `BuildLicenseRow`, `GetStatusText` | **CONFIRMED** |
| SCAN-04 | `_licenseKeyBox`, `_licenseStatusText`, `_activateBtn`, `LicenseTxtPath` present | **CONFIRMED** at lines 62-69 |
| SCAN-05 | Non-ASCII scan via `[^\x00-\x7F]` regex | **0 hits** |
| SCAN-06 | `FeatureFlagsChanged` subscribe at L151, unsubscribe at L181, handler at L448 | **CONFIRMED** |
| SCAN-07 | `LicenseTxtPath` declared at L66; `LicenseClient.Validate` called at L389 | **CONFIRMED** |

---

## CYC Audit

| Method | CYC | Analysis |
|--------|-----|---------|
| `BuildLicenseRow` | 1 | Sequential assignments, no branches |
| `OnActivateClick` | 1 | Sequential steps, try/catch = 1 branch per spec accounting (still CYC=1 per straight-line path) |
| `ApplyFeatureFlags` | 2 | Two null-guard `if` checks for `_modeCb` and `_addRuleBtn`; foreach loops do not add cyclomatic complexity |
| `LoadLicenseKeyDisplay` | 2 | try/catch + ternary inside try |
| `OnFeatureFlagsChanged` | 1 | Sequential, no branches |
| `GetStatusText` | 3 | 2 `if` branches + base return |
| `OnLoaded` | unchanged | 0 new branches added (3 appended lines, no conditional) |
| `OnWindowClosed` | unchanged | 0 new branches added (1 appended line) |

All methods: CYC ≤ 8. ✅

---

## JS Rules Compliance

| Rule | Status |
|------|--------|
| JS-001 (no throw) | PASS — all I/O wrapped in try/catch, no exceptions escape |
| JS-002 (no return null) | PASS — `GetStatusText` returns string, never null |
| JS-021 (no lock) | PASS — 0 lock() in new code |
| JS-023 (volatile) | N/A — reads `CopyEngine.Instance.Flags` (volatile field in engine) |
| No hex colors | PASS — `MakeWinBrush` not used in new code; no hex literals |
| No FontFamily | PASS — no FontFamily set anywhere in new code |
| ASCII-only strings | PASS — "LICENSE", "Activate", "ELITE", "PRO", "STARTER", tooltip strings all ASCII |
| DateTime.UtcNow | PASS — no DateTime.Now usage in new code |

---

## Deviations from Spec

1. **`_activateBtn` field**: Spec mentioned `_activateBtn` as a field in step 1. Added as declared (consistent with ticket instruction).

2. **`ApplyFeatureFlags` button lists**: Spec described single buttons (`_trimBtn`, `_flattenBtn`, etc.). Actual file uses `List<Button>` fields. Implementation uses `foreach` over each list — functionally identical, semantically correct.

3. **No `UpdateLicenseStatus` method**: The ticket spec (step 6 in Implementation Steps) describes `LoadLicenseKeyDisplay` setting status text, not a separate `UpdateLicenseStatus`. Used `GetStatusText(f)` helper (per ticket method signature table) to set `_licenseStatusText.Text` directly. No separate `UpdateLicenseStatus` method was required by the ticket's Method Signatures table.

4. **`_modeCb` field promotion**: `modeCb` was a local variable in `BuildUI()`. Promoted to `_modeCb` field to allow `ApplyFeatureFlags` to gate Mirror mode visibility. This is a surgical addition that does not change behavior.

5. **Visibility gating for `_clickTraderRow`, `_atrRow`, `_qxBtn`**: These controls do not exist in TradeCopierWindow.cs — they belong to TradeCopierPanel.cs (Ticket 5). Not applicable for this ticket.
