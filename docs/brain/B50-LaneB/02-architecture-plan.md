# B50-LaneB Architecture Plan — BE Button Color Fix

**Block**: PTT-COPIER-B50  
**Lane**: B — be-color-fix  
**Date**: 2026-08-08  
**Status**: PLAN_COMPLETE

---

## 1. Scope

UI-only change to `TradeCopierPanel.cs`. Zero logic changes.  
Two buttons receive new brush values to match the Quick button teal style.

**File in scope**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`

---

## 2. Current State (Confirmed from Source)

### `_beBtn2` — line 939
```csharp
_beBtn2 = new Button { Content = FormatBuffer("BE", _beBuffer), Background = BrushInactive };
```
- Has grey `Background = BrushInactive` fill
- Has NO `BorderBrush`, NO `Foreground`, NO `BorderThickness` set at construction

### `_globalBeBtn2` — lines 962–968
```csharp
_globalBeBtn2 = new Button
{
    Content         = FormatGlobalBeBuffer("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer),
    BorderBrush     = BrushPurple,
    Foreground      = BrushPurple,
    BorderThickness = new Thickness(2)
};
```
- Has purple `BorderBrush = BrushPurple` and `Foreground = BrushPurple`

### `UpdateButtonColors` — line 564
```csharp
if (_beBtn2 != null && _beState == BeState.Idle) _beBtn2.Background = hasPosition ? BrushActive : BrushInactive;
```
- Idle-state fallback sets Background to BrushInactive — must be removed (teal buttons have no filled background)

### `UpdateBeVisuals` — lines 1241–1255
```csharp
case BeState.Idle:
    _beBtn2.Content    = FormatBuffer("BE", _beBuffer);
    _beBtn2.Background = BrushInactive;    // ← sets grey fill on idle
    break;
```
- Sets `Background = BrushInactive` in idle case — must be removed

### `UpdateBeAllVisuals` — lines 854–858
```csharp
_globalBeBtn2.Background = state == BeState.Idle ? BrushPurple : BrushCaution;
```
- Sets `Background = BrushPurple` for idle state on _globalBeBtn2  
- The teal style has no filled background; this needs correction:  
  idle → set BorderBrush + Foreground to teal (no filled background)  
  armed → BrushCaution background can remain (armed state feedback)

---

## 3. Target State

### `_beBtn2` construction (line 939)
```csharp
_beBtn2 = new Button
{
    Content         = FormatBuffer("BE", _beBuffer),
    BorderBrush     = MakeBrush(13, 148, 136),
    Foreground      = MakeBrush(13, 148, 136),
    BorderThickness = new Thickness(2)
};
```

### `_globalBeBtn2` construction (lines 962–968)
```csharp
_globalBeBtn2 = new Button
{
    Content         = FormatGlobalBeBuffer("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer),
    BorderBrush     = MakeBrush(13, 148, 136),
    Foreground      = MakeBrush(13, 148, 136),
    BorderThickness = new Thickness(2)
};
```

### `UpdateButtonColors` line 564 — remove _beBtn2 background assignment
The line:
```csharp
if (_beBtn2 != null && _beState == BeState.Idle) _beBtn2.Background = hasPosition ? BrushActive : BrushInactive;
```
**Remove entirely** — teal-border buttons do not change background based on position state.

### `UpdateBeVisuals` idle case (line 1248)
Remove:
```csharp
_beBtn2.Background = BrushInactive;
```

### `UpdateBeAllVisuals` (line 857)
Change idle branch from BrushPurple background to teal border + foreground:
```csharp
private void UpdateBeAllVisuals(BeState state)
{
    if (_globalBeBtn2 == null) return;
    if (state == BeState.Idle)
    {
        _globalBeBtn2.BorderBrush  = MakeBrush(13, 148, 136);
        _globalBeBtn2.Foreground   = MakeBrush(13, 148, 136);
        _globalBeBtn2.Background   = System.Windows.Media.Brushes.Transparent;
    }
    else
    {
        _globalBeBtn2.Background   = BrushCaution;
    }
}
```

**Note**: CYC stays at 2 (one branch in switch/if = 2 paths). No new conditional logic added.

---

## 4. Change Summary

| Location | Line | Change Type | Detail |
|----------|------|------------|--------|
| `_beBtn2` construction | 939 | Modify | Replace `Background=BrushInactive` with teal BorderBrush+Foreground+BorderThickness |
| `_globalBeBtn2` construction | 965 | Modify | Change `BrushPurple` → `MakeBrush(13,148,136)` for BorderBrush + Foreground |
| `UpdateButtonColors` | 564 | Delete line | Remove `_beBtn2.Background` assignment |
| `UpdateBeVisuals` idle case | 1248 | Delete line | Remove `_beBtn2.Background = BrushInactive` |
| `UpdateBeAllVisuals` | 857 | Modify | Replace Background=BrushPurple with teal BorderBrush+Foreground+transparent Background |

Total: 5 targeted line-level edits. Zero logic changes. CYC unchanged throughout.

---

## 5. Rules Catalog Gate

| Rule | Status |
|------|--------|
| JS-021 lock() | No lock() added — PASS |
| JS-033 async void | No async void added — PASS |
| JS-002 return null | No return null added — PASS |
| JS-001 throw exception | No exceptions added — PASS |
| CYC <= 8 | No new branches — PASS |
| NT8-001 init accessor | Not used — PASS |
| NT8-002 abstract record | Not used — PASS |
| NT8-003 volatile | Not used — PASS |

---

## 6. Test Strategy

No xUnit tests required. UI brush values are not testable without a live WPF thread.  
Validation: Director F5 visual acceptance — BE +1 and BE ALL show teal border matching Quick buttons.
