# B51-LaneA Tickets

**Block**: PTT-COPIER-B51  
**Lane**: A  
**Epic**: B51-LaneA  
**Date**: 2026-08-08  
**Status**: TICKETS_COMPLETE  
**Source plan**: `docs/brain/B51-LaneA/02-architecture-plan.md` (REVIEW_PASS)

---

## T1 — Fix multiplier TextBox visibility + ATM combo timing + build tag bump

### Spec Requirement IDs Satisfied

| ID         | Description                                           |
|------------|-------------------------------------------------------|
| DW-B51-01  | Multiplier TextBox column visible in follower rows    |
| DW-B51-02  | ATM dropdown reappears after checkbox tick in Clone mode |

---

### Files

| Action | File Path (Wave workspace)                                               |
|--------|--------------------------------------------------------------------------|
| MODIFY | `C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` |
| MODIFY | `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`        |

---

### Change 1 of 3 — DW-B51-01: Hide multiplier TextBox in template

**File**: `TradeCopierPanel.cs`  
**Method**: `BuildCheckItemTemplate()` — return type `DataTemplate`, no parameters  
**Location**: ~line 1890, immediately after `multFactory.AddHandler(...)` call  

**Exact insertion** — add one line after the existing `AddHandler`:

```csharp
// EXISTING (lines 1887-1890 — do not modify):
multFactory.AddHandler(TextBox.TextChangedEvent,
    new TextChangedEventHandler(OnFollowerMultiplierChanged));

// ADD THIS LINE (DW-B51-01):
multFactory.SetValue(FrameworkElement.VisibilityProperty, Visibility.Collapsed);
```

**JS rule constraints**:
- JS-021 (lock ban): No lock. PASS.
- JS-033 (async void ban): Not async. PASS.
- NT8-001..007: No banned patterns. PASS.

**CYC delta**: 0 (no branch added).

---

### Change 2 of 3 — DW-B51-02: Apply current mode to newly-loaded ATM combo

**File**: `TradeCopierPanel.cs`  
**Method**: `OnFollowerAtmTemplateComboLoaded(object sender, RoutedEventArgs e)` — void, event handler  
**Location**: ~lines 1973-1974, inside the `!_atmComboRefs.Contains(cb)` block  

**Exact replacement** — expand the single-statement if into a braced block with an additional check:

```csharp
// BEFORE (lines ~1973-1974):
if (!_atmComboRefs.Contains(cb))
    _atmComboRefs.Add(cb);   // B50: track combo for Clone visibility toggle

// AFTER (DW-B51-02):
if (!_atmComboRefs.Contains(cb))
{
    _atmComboRefs.Add(cb);   // B50: track combo for Clone visibility toggle
    // B51: apply current mode to newly-loaded combo (timing fix)
    if (CopyEngine.Instance.GetCopyMode() == CopyMode.Clone)
        cb.Visibility = Visibility.Collapsed;
}
```

**JS rule constraints**:
- JS-021 (lock ban): No lock. PASS.
- JS-033 (async void ban): This is a plain void event handler, not async. PASS.
- JS-001 (throw ban): No exception thrown. PASS.
- JS-002 (null return ban): No return statement. PASS.
- NT8-001..007: No banned patterns; `CopyEngine.Instance.GetCopyMode()` is PTT-internal. PASS.

**CYC delta**: +1. Previous CYC = 4. New CYC = 5. Constraint CYC <= 8: PASS.

**Threading note**: `OnFollowerAtmTemplateComboLoaded` fires on the WPF UI thread (RoutedEventHandler
contract). `cb.Visibility` is set on the UI thread — correct. No `Dispatcher.InvokeAsync` required.

---

### Change 3 of 3 — Build tag bump B50 -> B51

**File**: `CopyEngine.cs`  
**Location**: Line 41 — string literal  

```csharp
// BEFORE (line 41):
"PTT-COPIER B50 | clone-mode+be-color+test-fix | 2026-08-08"

// AFTER:
"PTT-COPIER B51 | ui-fixes | 2026-08-08"
```

One string replacement. Zero logic change. Zero CYC delta.

---

### No New Tests Rationale

Both `BuildCheckItemTemplate()` and `OnFollowerAtmTemplateComboLoaded()` operate on WPF
`DependencyProperty` values and require a live WPF `Application` + `DispatcherFrame` + NT8
NinjaScript host to exercise. xUnit console runners do not provide a WPF application context.
Creating a heavyweight WPF test harness for a 1-line and 4-line change is disproportionate and
inconsistent with existing PTT test patterns. The SCAN-03 and SCAN-04 grep checks below provide
textual verification that the code changes were applied correctly.

---

### 7-Scan Checklist

Every scan must be run by the engineer from the Wave workspace root
(`C:\WSGTA\universal-or-strategy\`) before committing.

---

#### SCAN-01 — lock() check

```powershell
Select-String -Path "src\PropTraderTools\TradeCopierPanel.cs" -Pattern "lock\(" 
```

**Expected**: Zero matches in the modified regions (`BuildCheckItemTemplate` and
`OnFollowerAtmTemplateComboLoaded`). Any existing `lock(` elsewhere in the file is pre-existing
and out of scope for this ticket; do NOT fix it here (scope creep ban).

---

#### SCAN-02 — async void check

```powershell
Select-String -Path "src\PropTraderTools\TradeCopierPanel.cs" -Pattern "async void"
```

**Expected**: Zero new `async void` methods introduced by this ticket. Any pre-existing `async void`
event handlers are out of scope.

---

#### SCAN-03 — Multiplier TextBox hidden

```powershell
Select-String -Path "src\PropTraderTools\TradeCopierPanel.cs" -Pattern "Visibility\.Collapsed"
```

**Expected**: At least one match corresponding to the new `multFactory.SetValue` line inside
`BuildCheckItemTemplate()`. The engineer must confirm the match is on the `multFactory` line (not
only on a pre-existing line elsewhere in the file).

---

#### SCAN-04 — Clone ATM timing fix present

```powershell
Select-String -Path "src\PropTraderTools\TradeCopierPanel.cs" -Pattern "GetCopyMode|CopyMode\.Clone"
```

**Expected**: At least two new matches inside `OnFollowerAtmTemplateComboLoaded`:
  - One on the `CopyEngine.Instance.GetCopyMode()` call
  - One on the `CopyMode.Clone` comparison

---

#### SCAN-05 — Build gate

```powershell
# From Wave workspace root:
dotnet build "src\PropTraderTools\PropTraderTools.csproj"
```

**Expected**: `Build succeeded.` with `0 Error(s)`. Any warnings that pre-existed before this
ticket are acceptable; zero new warnings is the goal.

---

#### SCAN-06 — CYC check

Manual branch count for `OnFollowerAtmTemplateComboLoaded` after the fix:

| Branch # | Condition                               |
|----------|-----------------------------------------|
| 1        | method entry (base)                     |
| 2        | `sender is ComboBox cb`                 |
| 3        | `cb.Tag is ...` (existing guard, if any)|
| 4        | `!_atmComboRefs.Contains(cb)`           |
| 5        | `GetCopyMode() == CopyMode.Clone`  [NEW]|

**Expected**: CYC = 5. Constraint CYC <= 8: PASS.

---

#### SCAN-07 — Hard-link integrity

```powershell
powershell -File scripts\verify_links.ps1
```

**Expected**: `DESYNC=0 MISSING=0`. If the script reports desyncs, run:
```powershell
powershell -File scripts\verify_links.ps1 -Fix
```
and re-run until clean.

---

### Summary Table

| Item               | Detail                                                           |
|--------------------|------------------------------------------------------------------|
| Spec IDs           | DW-B51-01, DW-B51-02                                            |
| Files modified     | TradeCopierPanel.cs (2 edits), CopyEngine.cs (1 edit)           |
| Net lines added    | ~6 lines total                                                   |
| New tests          | None (WPF Dispatcher — no xUnit host)                           |
| CYC after fix      | BuildCheckItemTemplate: unchanged; OnFollowerAtmTemplateComboLoaded: 5 |
| P0 rules violated  | None                                                             |
| Dispatcher needed  | No — both edits are UI-thread-local                             |
| lock() introduced  | No                                                               |
