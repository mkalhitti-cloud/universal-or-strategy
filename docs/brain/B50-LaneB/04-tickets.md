# B50-LaneB Tickets — BE Button Color Fix

**Block**: PTT-COPIER-B50  
**Lane**: B — be-color-fix  
**Date**: 2026-08-08

---

## TICKET-1: Apply teal style to _beBtn2 and _globalBeBtn2 buttons

**Spec Req IDs**: B50-LaneB (BE +1 and BE ALL teal color parity with Quick buttons)  
**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`

### Description

Five surgical edits — UI brush values only. Zero logic changes. Zero new conditional branches.

---

### Edit 1 — `_beBtn2` construction (line 939)

**Current**:
```csharp
_beBtn2 = new Button { Content = FormatBuffer("BE", _beBuffer), Background = BrushInactive };
```

**Replace with**:
```csharp
_beBtn2 = new Button
{
    Content         = FormatBuffer("BE", _beBuffer),
    BorderBrush     = MakeBrush(13, 148, 136),
    Foreground      = MakeBrush(13, 148, 136),
    BorderThickness = new Thickness(2)
};
```

---

### Edit 2 — `_globalBeBtn2` construction (lines 962–968)

**Current**:
```csharp
_globalBeBtn2 = new Button
{
    Content         = FormatGlobalBeBuffer("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer),
    BorderBrush     = BrushPurple,
    Foreground      = BrushPurple,
    BorderThickness = new Thickness(2)
};
```

**Replace with**:
```csharp
_globalBeBtn2 = new Button
{
    Content         = FormatGlobalBeBuffer("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer),
    BorderBrush     = MakeBrush(13, 148, 136),
    Foreground      = MakeBrush(13, 148, 136),
    BorderThickness = new Thickness(2)
};
```

---

### Edit 3 — `UpdateButtonColors` line 564 — remove _beBtn2 background assignment

**Current**:
```csharp
if (_beBtn2 != null && _beState == BeState.Idle) _beBtn2.Background = hasPosition ? BrushActive : BrushInactive;
```

**Action**: Delete this line entirely.  
Teal-style buttons do not change background based on position state.

---

### Edit 4 — `UpdateBeVisuals` idle case line 1248

**Current**:
```csharp
case BeState.Idle:
    _beBtn2.Content    = FormatBuffer("BE", _beBuffer);
    _beBtn2.Background = BrushInactive;
    break;
```

**Replace with**:
```csharp
case BeState.Idle:
    _beBtn2.Content    = FormatBuffer("BE", _beBuffer);
    break;
```

**Action**: Remove `_beBtn2.Background = BrushInactive;` line only.

---

### Edit 5 — `UpdateBeAllVisuals` lines 854–858

**Current**:
```csharp
private void UpdateBeAllVisuals(BeState state)
{
    if (_globalBeBtn2 == null) return;
    _globalBeBtn2.Background = state == BeState.Idle ? BrushPurple : BrushCaution;
}
```

**Replace with**:
```csharp
private void UpdateBeAllVisuals(BeState state)
{
    if (_globalBeBtn2 == null) return;
    if (state == BeState.Idle)
    {
        _globalBeBtn2.BorderBrush = MakeBrush(13, 148, 136);
        _globalBeBtn2.Foreground  = MakeBrush(13, 148, 136);
        _globalBeBtn2.Background  = System.Windows.Media.Brushes.Transparent;
    }
    else
    {
        _globalBeBtn2.Background  = BrushCaution;
    }
}
```

**Note**: CYC remains 2 (one conditional branch = 2 paths). Same as before.

---

### 7-Scan Checklist (Engineer Contract)

| Scan | Command | Expected |
|------|---------|---------|
| SCAN-01 JS-021 lock() | `grep -n "lock(" TradeCopierPanel.cs` | 0 matches in modified regions |
| SCAN-02 JS-033 async void | `grep -n "async void" TradeCopierPanel.cs` | 0 new async void declarations |
| SCAN-03 BrushPurple on BE buttons eliminated | `grep -n "BrushPurple" TradeCopierPanel.cs` | _globalBeBtn2 construction no longer references BrushPurple |
| SCAN-04 BrushInactive background on _beBtn2 eliminated | `grep -n "BrushInactive" TradeCopierPanel.cs` | _beBtn2 construction and UpdateBeVisuals/UpdateButtonColors no longer set _beBtn2.Background=BrushInactive |
| SCAN-05 Teal values applied | `grep -n "13, 148, 136" TradeCopierPanel.cs` | >= 2 new matches (one _beBtn2, one _globalBeBtn2) |
| SCAN-06 Build gate | `dotnet build` from Wave workspace root | 0 errors |
| SCAN-07 Hard-link integrity | `powershell -File scripts\verify_links.ps1` | DESYNC=0 MISSING=0 |

---

### Acceptance Criteria

- [ ] `_beBtn2`: `Background = BrushInactive` REMOVED from construction
- [ ] `_beBtn2`: `BorderBrush = MakeBrush(13,148,136)` ADDED to construction
- [ ] `_beBtn2`: `Foreground = MakeBrush(13,148,136)` ADDED to construction
- [ ] `_beBtn2`: `BorderThickness = new Thickness(2)` SET in construction
- [ ] `_globalBeBtn2`: `BorderBrush` changed from `BrushPurple` to `MakeBrush(13,148,136)`
- [ ] `_globalBeBtn2`: `Foreground` changed from `BrushPurple` to `MakeBrush(13,148,136)`
- [ ] `UpdateButtonColors` line 564: `_beBtn2.Background` assignment removed
- [ ] `UpdateBeVisuals` idle case: `_beBtn2.Background = BrushInactive` removed
- [ ] `UpdateBeAllVisuals`: idle branch uses teal BorderBrush+Foreground instead of BrushPurple background
- [ ] All 7 scans PASS
- [ ] DESYNC=0 MISSING=0
