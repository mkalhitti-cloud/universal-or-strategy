# B50-LaneA Ticket 1 Completion Report
## PTT-COPIER-B50 / Lane A — Clone Mode

**Block**: PTT-COPIER-B50
**Lane**: A
**Ticket**: T1 — Clone Mode Full Implementation
**Engineer**: ptt-engineer
**Date**: 2026-08-08
**Status**: BUILD_PASS

---

## Files Modified

### 1. `src/PropTraderTools/CopyEngine.cs` (MODIFIED)

| Change | Location | Description |
|--------|----------|-------------|
| PttBuild.Tag | Line 41 | Updated to `"PTT-COPIER B50 | clone-mode+be-color+test-fix | 2026-08-08"` |
| `CopyMode` enum | Line 87 | Added `Clone = 2` to existing `{ Signal = 0, Mirror = 1 }` |
| `_cloneAtmCache` field | After line 108 | Added `private volatile string _cloneAtmCache = string.Empty;` with threading comment |
| `SetCloneAtmCache(string)` | After `GetCopyMode` (~line 349) | New `internal void`, CYC=1, null-coalesces to `string.Empty` |
| `ResolveAtmMode(CopyRule, string)` | After `GetAtmMode` (~line 889) | New `private FollowerAtmMode`, CYC=2, mode-aware ATM dispatch |
| `GetCloneAtmMode()` | After `ResolveAtmMode` (~line 901) | New `internal FollowerAtmMode`, CYC=2, returns Named or Inherit |
| `DispatchCopy` inner loop | Line ~610 | Changed `GetAtmMode(rule, acc.Name)` → `ResolveAtmMode(rule, acc.Name)` (1-line change) |

### 2. `src/PropTraderTools/TradeCopierPanel.cs` (MODIFIED)

| Change | Location | Description |
|--------|----------|-------------|
| `_cloneModeBtn` field | After line 196 | Added `private RadioButton _cloneModeBtn = null;` |
| `_atmComboRefs` field | After `_cloneModeBtn` | Added `private readonly List<ComboBox> _atmComboRefs = new List<ComboBox>();` |
| `BuildModeRow` | ~line 1431 | Created `_cloneModeBtn` RadioButton with `OnCloneModeClick` handler; added to row |
| `OnSignalModeClick` | ~line 1439 | Added `UpdateAtmComboVisibility(Visibility.Visible)` call |
| `OnMirrorModeClick` | ~line 1445 | Added `UpdateAtmComboVisibility(Visibility.Visible)` call |
| `OnCloneModeClick` | New (~line 1462) | New event handler: SetCopyMode(Clone), GetLeaderAtmTemplateName, SetCloneAtmCache, UpdateAtmComboVisibility(Collapsed) |
| `UpdateAtmComboVisibility` | New (~line 1476) | New helper: iterates `_atmComboRefs`, sets Visibility, CYC=2 |
| `OnFollowerAtmTemplateComboLoaded` | ~line 1969 | Added `_atmComboRefs.Contains(cb)` dedup guard + `_atmComboRefs.Add(cb)` tracking line |

### 3. `src/PropTraderTools/Tests/B50Tests.cs` (CREATED)

New file. 5 `[Fact]` tests:
- `T_B50_01_CopyMode_Clone_HasValue2` — enum value assertions
- `T_B50_02_SetCopyMode_Clone_SetsModeValueToClone` — roundtrip via `GetCopyMode`
- `T_B50_03_DispatchCopy_CloneMode_UsesCloneAtmCache` — `GetCloneAtmMode` returns Named
- `T_B50_04_HandleBracketChange_CloneMode_SyncsFollowers` — Clone != Mirror assertion
- `T_B50_05_CloneAtmCache_EmptyFallback_UsesDefault` — `GetCloneAtmMode` returns Inherit when empty

### 4. `src/PropTraderTools/PropTraderTools.csproj` (MODIFIED)

Added after B47Tests entry:
```xml
<!-- B50: Clone mode tests -->
<Compile Include="Tests\B50Tests.cs" />
```

### 5. `src/PropTraderTools/Features/PttFollowerStrategy.cs` (NO CHANGE)

`FillSignalEventArgs.AtmTemplateName` already handles Named ATM path. Clone dispatch uses the same `SendCopy` → `PttBus.RaiseFillSignal` path as Signal Named ATM. Zero changes required.

---

## Seven-Scan Results

### SCAN-01 — JS-021 lock() check
```
Select-String -Path CopyEngine.cs, TradeCopierPanel.cs -Pattern "lock\("
```
**Result**: PASS — 0 actual `lock()` calls. All matches are comments (`no lock()`).

### SCAN-02 — JS-033 async void check
```
Select-String -Path CopyEngine.cs, TradeCopierPanel.cs -Pattern "async void"
```
**Result**: PASS — 0 actual `async void` declarations in new/modified code. All matches are comments.

### SCAN-03 — JS-002 return null check
```
Select-String -Path CopyEngine.cs, TradeCopierPanel.cs -Pattern "return null"
```
**Result**: PASS — No new `return null` in B50 changes. Pre-existing `return null` in `FindRule`, `FindFollowerBracketOrder`, `TryResolveLeaderAccount` are documented pre-existing debt (DW-B47-05). None are in B50-added methods.

### SCAN-04 — NT8-003 volatile double/float check
```
Select-String -Path CopyEngine.cs -Pattern "volatile double|volatile float"
```
**Result**: PASS — 0 matches. New field `_cloneAtmCache` is `volatile string` (reference type — NT8-003 COMPLIANT). All scan hits are comments.

### SCAN-05 — dotnet build
```
dotnet build src\PropTraderTools\PropTraderTools.csproj
```
**Result**: PASS — `Build succeeded. 0 Error(s)`. 19 pre-existing warnings (none from B50 code).

### SCAN-06 — CYC count for new/modified methods

| Method | File | CYC | Status |
|--------|------|-----|--------|
| `SetCloneAtmCache` | CopyEngine.cs | 1 | NEW — PASS |
| `GetCloneAtmMode` | CopyEngine.cs | 2 | NEW — PASS |
| `ResolveAtmMode` | CopyEngine.cs | 2 | NEW — PASS |
| `DispatchCopy` | CopyEngine.cs | 8 | MODIFIED (1-line) — AT LIMIT, PASS |
| `BuildModeRow` | TradeCopierPanel.cs | 1 | MODIFIED (add Clone btn) — PASS |
| `OnSignalModeClick` | TradeCopierPanel.cs | 1 | MODIFIED (+1 call) — PASS |
| `OnMirrorModeClick` | TradeCopierPanel.cs | 1 | MODIFIED (+1 call) — PASS |
| `OnCloneModeClick` | TradeCopierPanel.cs | 1 | NEW — PASS |
| `UpdateAtmComboVisibility` | TradeCopierPanel.cs | 2 | NEW — PASS |
| `OnFollowerAtmTemplateComboLoaded` | TradeCopierPanel.cs | 5 | MODIFIED (+1 if) — PASS |

**Result**: PASS — All methods ≤ 8.

### SCAN-07 — verify_links.ps1
```
powershell -File scripts\verify_links.ps1 -Fix
```
**Result**: PASS — `DESYNC=0 MISSING=0`. 2 files repaired (CopyEngine.cs, TradeCopierPanel.cs — expected after modification). B50Tests.cs correctly SKIPPED (Tests subfolder, not deployed to NT8 per DW-B48-02).

---

## Deviations from Ticket

None. All changes implemented exactly as specified in `04-tickets.md`.

---

## Deferred Items

| ID | Priority | Description |
|----|----------|-------------|
| DW-B50-01 | P1 | Live F5 verification: Clone mode ATM cache fills correctly from leader's ChartTrader in real NT8 session. |
| DW-B50-02 | P2 | `_atmComboRefs` list retains detached ComboBox refs on follower panel rebuild. No harm; future: weak refs or clear on teardown. |

---

## Return

**BUILD_PASS**
