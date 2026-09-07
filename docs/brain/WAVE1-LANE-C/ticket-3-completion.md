# Ticket C-03 Completion Report
**Ticket:** C-03 -- FollowerItem::BuildCheckItemTemplate
**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Test File:** `tests/PropTraderTools.Tests/Wave1LaneCTests.cs` (appended)
**Engineer:** ptt-engineer
**Date:** 2026-09-07

---

## Pre-Edit CCN Baseline

| Method | CCN Before | Lines (pre-edit) |
|--------|-----------|------------------|
| `BuildCheckItemTemplate` | 61 | 2345-2420 |

---

## Helpers Extracted

All helpers are private instance methods on `FollowerItem` class in `TradeCopierPanel.cs`.
No static helpers, no new classes, no new files.

| Helper | CCN After | Line Range (post-edit) | Return Type |
|--------|----------|------------------------|-------------|
| `BuildTemplateAccountNameColumn()` | 1 | 2365-2373 | `FrameworkElementFactory` |
| `BuildTemplatePnlColumn()` | 1 | 2377-2386 | `FrameworkElementFactory` |
| `BuildTemplateMultiplierColumn()` | 1 | 2391-2403 | `FrameworkElementFactory` |
| `BuildTemplateAtmComboColumn()` | 1 | 2408-2424 | `FrameworkElementFactory` |
| `BuildTemplateCheckBoxColumn()` | 1 | 2428-2440 | `FrameworkElementFactory` |

**Parent `BuildCheckItemTemplate` CCN after:** 1

---

## Tests Added

File: `tests/PropTraderTools.Tests/Wave1LaneCTests.cs` (appended to existing class)

| Test Name | Description |
|-----------|-------------|
| `T_C03_01_BuildTemplateAccountNameColumn_ColumnIndex_IsZero` | Verifies factory column index == 0 (TextBlock at col 0) |
| `T_C03_02_BuildTemplatePnlColumn_TextAlignment_IsRight` | Verifies TextAlignment.Right (== 2) |
| `T_C03_03_BuildTemplateMultiplierColumn_Visibility_IsCollapsed` | Verifies Visibility.Collapsed (== 2) |
| `T_C03_04_BuildTemplateAtmComboColumn_Width_Is120` | Verifies Width == 120.0 |
| `T_C03_05_BuildTemplateCheckBoxColumn_ColumnIndex_IsFour` | Verifies factory column index == 4 (CheckBox at col 4) |

Test run result: **157 Passed, 0 Failed, 3 Skipped** (>= 127 floor: PASS)

---

## 7-Scan Results

### Scan 1 -- JS-021 lock()
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "lock\("
```
**Result:** All hits are in comments only (JS-021 compliance notes). Zero code-level lock() usage. **PASS**

### Scan 2 -- JS-001 throw new
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "throw new"
```
**Result:** 0 hits. **PASS**

### Scan 3 -- JS-002 return null
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "return null"
```
**Result:** Pre-existing hits at lines 505, 565, 570, 574, 1977, 1987 (unchanged, not in C-03 helpers). Zero new return null in any C-03 helper. **PASS**

### Scan 4 -- JS-033 async void
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "async void"
```
**Result:** All hits are in comments only. Zero code-level async void. **PASS**

### Scan 5 -- CYC (lizard)
```
lizard src/PropTraderTools/TradeCopierPanel.cs --csv -x "*/bin/*" -x "*/obj/*"
```
**Result:**
| Method | CCN |
|--------|-----|
| `BuildCheckItemTemplate` | 1 |
| `BuildTemplateAccountNameColumn` | 1 |
| `BuildTemplatePnlColumn` | 1 |
| `BuildTemplateMultiplierColumn` | 1 |
| `BuildTemplateAtmComboColumn` | 1 |
| `BuildTemplateCheckBoxColumn` | 1 |
All <= 8. **PASS**

### Scan 6 -- throw (non-comment)
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "throw " | Where-Object { $_.Line -notmatch "//" }
```
**Result:** 0 hits. **PASS**

### Scan 7 -- dotnet build
```
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
**Result:** Build succeeded. 0 Error(s). **PASS**

---

## Summary

- **Pre-edit CCN:** `BuildCheckItemTemplate` = 61
- **Post-edit CCN:** `BuildCheckItemTemplate` = 1, all 5 helpers = 1
- **CCN reduction:** 60 points (61 -> 1)
- **Helpers extracted:** 5 private instance methods, all CCN = 1
- **Tests added:** 5 [Fact] tests (T_C03_01 through T_C03_05)
- **Total tests passing:** 157 (floor: 127)
- **Build:** 0 errors
- **All 7 scans:** PASS

BUILD_PASS
