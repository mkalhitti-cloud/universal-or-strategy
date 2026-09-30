# BGTM-1 Ticket 5 — Completion Report

**Ticket**: 5 — TradeCopierPanel.cs Feature-Flag Wiring
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Engineer**: ptt-engineer (PTT Phase 4a)
**Status**: BUILD_PASS

---

## What Was Implemented

### Field Mapping (Spec Name → Actual Field)

| Spec Name | Actual Field | Type | Found? | Notes |
|-----------|-------------|------|--------|-------|
| `_trimBtn` | `_trimBtn2` | `Button` | YES | L250 |
| `_flattenBtn` | `_flattenBtn2` | `Button` | YES | L251 |
| `_cancelBtn` | `_cancelBtn2` | `Button` | YES | L253 |
| `_beBtn` | `_beBtn2` | `Button` | YES | L252 |
| `_mirrorRadio` | `_mirrorModeBtn` | `RadioButton` | YES | L225 |
| `_addRuleBtn` | **NOT a field** | — | NO | `applyBtn` is a local var in `BuildUI()` with `Visibility.Collapsed`; no persistent field. Skipped per ticket instruction. |
| `_clickTraderRow` | `_clickTraderRow` | `StackPanel` | PROMOTED | Was local `var row` in `BuildClickTraderRow()`. Promoted to class field (L271-272) and assigned in build method. |
| `_atrRow` | `_atrRow` | `UniformGrid` | PROMOTED | Was local `var grid` in `BuildRiskAtrRow()`. Promoted to class field (L271-272) and assigned in build method. |
| `_qxBtn` | **NOT a field** | — | NO | `PttGlobalQuickExit` is an `IPttModule` — its QX button is module-owned, not a panel field. Gate guard is T6 scope (PttGlobalQuickExit.Execute()). Skipped per ticket instruction. |

### New Fields Added

```csharp
// BGTM-1: Feature-flag-gated row panels. Assigned in Build* methods; toggled in ApplyFeatureFlags.
private StackPanel _clickTraderRow = null;
private UniformGrid _atrRow = null;
```

Added after `_quickRowPanel` in the field block (line ~271).

### Promoted Local Variables

1. **`BuildClickTraderRow()`**: `var row = new StackPanel` → `_clickTraderRow = new StackPanel`
   - All `row.Children.Add(...)` → `_clickTraderRow.Children.Add(...)`
   - `row.Visibility = Visibility.Collapsed` → `_clickTraderRow.Visibility = Visibility.Collapsed`

2. **`BuildRiskAtrRow()`**: `var grid = new UniformGrid` → `_atrRow = new UniformGrid`
   - All `grid.Children.Add(...)` → `_atrRow.Children.Add(...)`
   - `root.Children.Add(grid)` → `root.Children.Add(_atrRow)`

### New Methods Added (lines ~3064-3115)

```csharp
internal void ApplyFeatureFlags(FeatureFlags f)      // CYC=1 — 5 IsEnabled + 2 Visibility + tooltip call
private void ApplyFeatureFlagTooltips(FeatureFlags f) // CYC=1 — 5 ToolTip assignments
private void OnFeatureFlagsChanged(FeatureFlags f)   // CYC=1 — delegates to ApplyFeatureFlags
```

### OnLoaded Wiring (appended to OnLoaded body, ~L794-795)

```csharp
CopyEngine.Instance.FeatureFlagsChanged += OnFeatureFlagsChanged;
ApplyFeatureFlags(CopyEngine.Instance.Flags);
```

### Detach() Unsubscription (appended to Detach() body, ~L618)

```csharp
CopyEngine.Instance.FeatureFlagsChanged -= OnFeatureFlagsChanged;
```

---

## 7-Scan Results

### SCAN-01 — `lock()` in new code
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "lock\s*\("
```
**RESULT: 0 hits** ✅

### SCAN-02 — `throw new` in new code
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "throw\s+new\s+"
```
**RESULT: 0 hits** ✅

### SCAN-03 — ApplyFeatureFlags + OnFeatureFlagsChanged present
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "ApplyFeatureFlags|OnFeatureFlagsChanged"
```
**RESULT: 8 hits** ✅
- L269: comment reference
- L618: `FeatureFlagsChanged -= OnFeatureFlagsChanged` (Detach)
- L794: `FeatureFlagsChanged += OnFeatureFlagsChanged` (OnLoaded)
- L795: `ApplyFeatureFlags(CopyEngine.Instance.Flags)` (OnLoaded initial call)
- L3065, L3066: method definition ApplyFeatureFlags
- L3095, L3096: method definition ApplyFeatureFlagTooltips
- L3112: method definition OnFeatureFlagsChanged
- L3114: call site within OnFeatureFlagsChanged

### SCAN-04 — FeatureFlagsChanged event wired
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "FeatureFlagsChanged"
```
**RESULT: 5 hits** ✅
- L618: unsubscribe in Detach()
- L794: subscribe in OnLoaded
- L3065: comment
- L3110: comment
- L3112: handler method signature

### SCAN-05 — Non-ASCII characters in new code
```
(new code lines 3063-3117 only — all ASCII-only)
```
**RESULT: 0 non-ASCII in new BGTM-1 code** ✅
- 3 pre-existing non-ASCII bytes at lines ~2888-2893 (`\u25B2`/`\u25BC` arrow chars in ATR spinner RepeatButtons — pre-existing, not introduced by this ticket)

### SCAN-06 — IsEnabled/Visibility feature-flag wiring
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "IsEnabled|Visibility" (filtered for flag terms)
```
**RESULT: All wiring present** ✅
- L3070: `_trimBtn2.IsEnabled = f.TrimFlatten`
- L3072: `_flattenBtn2.IsEnabled = f.TrimFlatten`
- L3074: `_cancelBtn2.IsEnabled = f.TrimFlatten`
- L3077: `_beBtn2.IsEnabled = f.BreakEven`
- L3080: `_mirrorModeBtn.IsEnabled = f.MirrorMode`
- L3083-3085: `_clickTraderRow.Visibility = f.ClickTrader ? Visible : Collapsed`
- L3088-3090: `_atrRow.Visibility = f.AtrSizing ? Visible : Collapsed`

### SCAN-07 — Detach present and wired
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "Detach|Unloaded"
```
**RESULT: Detach() at L571 confirmed** ✅
- BGTM-1 unsubscription appended at L618

---

## CYC Audit

| Method | CYC | Note |
|--------|-----|------|
| `ApplyFeatureFlags` | 1 | 7 null guards + 5 IsEnabled + 2 Visibility (ternary operators do not add CYC) + helper call |
| `ApplyFeatureFlagTooltips` | 1 | 5 null guards + 5 ToolTip assignments (ternary operators do not add CYC) |
| `OnFeatureFlagsChanged` | 1 | single delegation call, no branches |
| `OnLoaded` (after additions) | unchanged | 2 lines appended, no new branches |
| `Detach` (after additions) | unchanged | 2 lines appended, no new branches |

---

## Elements NOT Wired (with explanation)

1. **`_addRuleBtn`** (`applyBtn` local): The "Add Followers" button (`var applyBtn`) is constructed as a local variable in `BuildUI()` with `Visibility.Collapsed` permanently. It has no persistent field. Per ticket instruction: "don't create stub fields. Only wire what actually exists." Skipped. The multi-rule gate guard already exists in `CopyEngine.AddRule()` (T2).

2. **`_qxBtn`** (QX button): The Global Quick Exit button is owned by the `PttGlobalQuickExit` module, not by `TradeCopierPanel`. The gate guard for `QxGlobalExit` is implemented in `PttGlobalQuickExit.Execute()` in Ticket 6 scope. Skipped per ticket instruction.

---

## JS Rules Compliance

| Rule | Status |
|------|--------|
| JS-021 (no lock) | PASS — no `lock()` in any new code |
| JS-001 (no throw) | PASS — all new methods are void assignments, no exceptions |
| CYC <= 8 | PASS — all 3 new methods at CYC=1 |
| No hex colors | PASS — `Visibility.Visible/Collapsed` enum only, no brush literals |
| ASCII-only | PASS — all new string literals ASCII |

---

## Return Value

**BUILD_PASS**
