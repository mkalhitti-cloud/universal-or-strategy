# Ticket C-03 Verification Report
**Ticket:** C-03 -- FollowerItem::BuildCheckItemTemplate
**File:** `src/PropTraderTools/TradeCopierPanel.cs` (READ-ONLY)
**Test File:** `tests/PropTraderTools.Tests/Wave1LaneCTests.cs`
**Verifier:** ptt-verifier (independent)
**Date:** 2026-09-07

---

## Check 1 — JS-021 lock() Scan

**Command:**
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "lock\("
```

**Result (all hits, independent run):**
- Line 1170: `// JS-021: no lock(). ...` (comment)
- Line 1222: `// JS-021: no lock(). ...` (comment)
- Line 1315: `// JS-021: no lock(). ...` (comment)
- Line 2345: `// JS-021: no lock(). ...` (comment)
- Line 2364: `// JS-021: no lock(). ...` (comment)
- Line 2376: `// JS-021: no lock(). ...` (comment)
- Line 2390: `// JS-021: no lock(). ...` (comment)
- Line 2407: `// JS-021: no lock(). ...` (comment)
- Line 2427: `// JS-021: no lock(). ...` (comment)

**Verdict:** ALL hits are in comments only. Zero live code-level `lock()`. **PASS**
**Engineer Layer 2 match:** Engineer reported "All hits are in comments only" -- CONFIRMED.

---

## Check 2 — lizard CCN Analysis

**Command:**
```
lizard src/PropTraderTools/TradeCopierPanel.cs --csv -x "*/bin/*" -x "*/obj/*"
```

**C-03 method results (independent run):**

| Method | CCN | Line Range |
|--------|-----|------------|
| `FollowerItem::BuildCheckItemTemplate` | 1 | 2346-2361 |
| `FollowerItem::BuildTemplateAccountNameColumn` | 1 | 2365-2373 |
| `FollowerItem::BuildTemplatePnlColumn` | 1 | 2377-2386 |
| `FollowerItem::BuildTemplateMultiplierColumn` | 1 | 2391-2403 |
| `FollowerItem::BuildTemplateAtmComboColumn` | 1 | 2408-2424 |
| `FollowerItem::BuildTemplateCheckBoxColumn` | 1 | 2428-2440 |

**All 6 methods CCN = 1 (<= 8 mandate).** **PASS**
**Engineer Layer 2 match:** Engineer reported all CCN=1 -- CONFIRMED.

---

## Check 3 — Helper Visibility / Class Membership

**Command:**
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "(private|public|protected|internal|static)\s+(FrameworkElementFactory|void|DataTemplate)\s+Build(Template|CheckItem)"
```

**Result:**
- Line 2346: `private DataTemplate BuildCheckItemTemplate()`
- Line 2365: `private FrameworkElementFactory BuildTemplateAccountNameColumn()`
- Line 2377: `private FrameworkElementFactory BuildTemplatePnlColumn()`
- Line 2391: `private FrameworkElementFactory BuildTemplateMultiplierColumn()`
- Line 2408: `private FrameworkElementFactory BuildTemplateAtmComboColumn()`
- Line 2428: `private FrameworkElementFactory BuildTemplateCheckBoxColumn()`

**Class context:** `private sealed class FollowerItem : INotifyPropertyChanged` at line 329.
All 6 methods are indented inside `FollowerItem` (8-space indent confirms nesting).

**Checks:**
- [x] All are `private` (not public, not protected, not internal)
- [x] All are instance methods (no `static` keyword)
- [x] All are on `FollowerItem` class

**PASS**

---

## Check 4 — No Dispatcher.InvokeAsync / Task.Run in Helpers

**Command:**
```
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "Dispatcher\.InvokeAsync|Task\.Run" |
  Where-Object { $_.LineNumber -ge 2346 -and $_.LineNumber -le 2440 }
```

**Result:** 0 hits in range 2346-2440.

All WPF construction is synchronous. No async dispatch patterns introduced. **PASS**

---

## Check 5 — Scope (CopyEngine.cs Unmodified by C-03)

**Command:**
```
git diff src/PropTraderTools/CopyEngine.cs | Select-String "BuildCheckItemTemplate|BuildTemplateAccountNameColumn|..."
```

**Result:** 0 hits. The CopyEngine.cs working-tree diff is from WAVE1-LANE-A work
(`TryResolveEnabledRule` helper, `SubmitLimitExitOrder` comment) -- entirely unrelated to C-03.
No C-03 helper names, no FollowerItem symbols appear in the CopyEngine.cs diff.

**Conclusion:** CopyEngine.cs was NOT touched by C-03. Scope boundary respected. **PASS**

---

## Check 6 — dotnet test Results

**Command:**
```
dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --no-build -v normal
```

**T_C03 test results (independent run):**
- `Passed` T_C03_01_BuildTemplateAccountNameColumn_ColumnIndex_IsZero
- `Passed` T_C03_02_BuildTemplatePnlColumn_TextAlignment_IsRight
- `Passed` T_C03_03_BuildTemplateMultiplierColumn_Visibility_IsCollapsed
- `Passed` T_C03_04_BuildTemplateAtmComboColumn_Width_Is120
- `Passed` T_C03_05_BuildTemplateCheckBoxColumn_ColumnIndex_IsFour

**Total suite result:** Failed: 0, Passed: 181, Skipped: 3, Total: 184

- T_C03_01 through T_C03_05: ALL PRESENT and PASSING **PASS**
- Floor check: 181 >= 127 **PASS**
- Engineer reported 157 passing; current total is 181 (24 additional tests from other tickets committed since). Discrepancy is additive (more tests, not fewer) -- no concern.

---

## Check 7 — JS Rule Scans

| Rule | Pattern | Result | Verdict |
|------|---------|--------|---------|
| JS-021 | `lock(` in code | 0 live hits (comments only) | PASS |
| JS-001 | `throw new` in TradeCopierPanel.cs | 0 hits | PASS |
| JS-002 | `return null` in range 2346-2440 (live code) | 0 hits (only in comments) | PASS |
| JS-033 | `async void` in live code | 0 hits (all in comments) | PASS |
| JS-080/CYC | CCN <= 8 for all 6 methods | All CCN=1 | PASS |
| JS-096 | No exception fallback (`throw`) | 0 hits | PASS |
| JS-066 | No new Unicode in range 2346-2440 | 0 non-ASCII characters | PASS |

All 7 JS rules: **PASS**

---

## Check 8 — Behaviour Preservation

**Source read:** `BuildCheckItemTemplate` body at lines 2346-2361 (independent read).

**Orchestration (actual):**
```csharp
private DataTemplate BuildCheckItemTemplate()
{
    var template = new DataTemplate(typeof(FollowerItem));
    var gridFactory = new FrameworkElementFactory(typeof(Grid));
    gridFactory.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler(OnRowGridLoaded));
    gridFactory.AppendChild(BuildTemplateAccountNameColumn()); // col 0 TextBlock
    gridFactory.AppendChild(BuildTemplatePnlColumn());         // col 1 TextBlock
    gridFactory.AppendChild(BuildTemplateMultiplierColumn());  // col 2 TextBox
    gridFactory.AppendChild(BuildTemplateAtmComboColumn());    // col 3 ComboBox
    gridFactory.AppendChild(BuildTemplateCheckBoxColumn());    // col 4 CheckBox
    template.VisualTree = gridFactory;
    return template;
}
```

**Spec requirement (04-tickets.md C-03):** 5 columns in order: AccountName(0), Pnl(1), Multiplier(2), AtmCombo(3), CheckBox(4). DataTemplate wraps Grid factory.

**Comparison:**
- [x] Column order identical (0→1→2→3→4)
- [x] DataTemplate returned (not null)
- [x] Grid factory as VisualTree
- [x] LoadedEvent handler preserved (OnRowGridLoaded)
- [x] Same 5 `AppendChild` calls, same order

**PASS**

---

## Comparison with Engineer Layer 2 Report

| Item | Engineer Layer 2 | Verifier Layer 3 | Match? |
|------|-----------------|-----------------|--------|
| BuildCheckItemTemplate CCN | 1 | 1 | YES |
| BuildTemplateAccountNameColumn CCN | 1 | 1 | YES |
| BuildTemplatePnlColumn CCN | 1 | 1 | YES |
| BuildTemplateMultiplierColumn CCN | 1 | 1 | YES |
| BuildTemplateAtmComboColumn CCN | 1 | 1 | YES |
| BuildTemplateCheckBoxColumn CCN | 1 | 1 | YES |
| lock() live hits | 0 | 0 | YES |
| throw new hits | 0 | 0 | YES |
| return null in helpers | 0 | 0 | YES |
| async void live hits | 0 | 0 | YES |
| All helpers private instance | YES | YES | YES |
| All helpers on FollowerItem | YES | YES | YES |
| T_C03_01..T_C03_05 present | YES | YES (all PASS) | YES |
| Total passing tests | 157 | 181 (+24 other tickets) | ADDITIVE |

**No discrepancies. All Layer 2 claims independently confirmed.**

---

## Final Summary

| Check | Result |
|-------|--------|
| Check 1: lock() | PASS |
| Check 2: lizard CCN <= 8 | PASS |
| Check 3: private instance on FollowerItem | PASS |
| Check 4: no async dispatch in helpers | PASS |
| Check 5: scope boundary respected | PASS |
| Check 6: T_C03_01..05 present + passing (181 total) | PASS |
| Check 7: JS-021/001/002/033/080/096/066 | PASS |
| Check 8: behaviour preservation | PASS |

**VERDICT: VERIFY_PASS**