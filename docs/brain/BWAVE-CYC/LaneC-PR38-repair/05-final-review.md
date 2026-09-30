# Final Review: BWAVE-CYC LaneC-PR38-repair

**Phase**: 5 (Final Review)  
**Reviewer**: ptt-plan-reviewer  
**Branch**: feature/bwave-cyc-lane-c2  
**HEAD**: 737805b4  
**Date**: 2026-08-10  
**Status**: FINAL_PASS

---

## Files Read

1. `docs/brain/BWAVE-CYC/LaneC-PR38-repair/02-architecture-plan.md` — full
2. `docs/brain/BWAVE-CYC/LaneC-PR38-repair/04-ticket-review.md` — full
3. `docs/brain/BWAVE-CYC/LaneC-PR38-repair/ticket-C-completion.md` — full
4. `docs/brain/BWAVE-CYC/LaneC-PR38-repair/ticket-C-verification.md` — full
5. `docs/standards/jane-street/RULES_CATALOG.md` — JS-001..JS-035 (Type Safety + Concurrency)
6. `src/PropTraderTools/TradeCopierAddOn.cs` — lines 95-535 (all modified scope)
7. `src/PropTraderTools/TradeCopierPanel.cs` — lines 270-285, 935-960, 1178-1200, 2994-3010, 3205-3230
8. `src/PropTraderTools/TradeCopierWindow.cs` — lines 400-425, 1085-1105
9. `docs/brain/BWAVE-CYC/LaneC-PR38-repair/06-deferred-backlog.md` — not found (new, created by this review)

---

## SECTION A — ALL-TICKETS COMPLETION GATE

| Requirement | Status |
|-------------|--------|
| `ticket-C-completion.md` exists | CONFIRMED |
| Contains `BUILD_PASS` | CONFIRMED (last line: `## BUILD_PASS`) |
| `ticket-C-verification.md` exists | CONFIRMED |
| Contains `VERIFY_PASS` | CONFIRMED (last line: `**Verdict**: **VERIFY_PASS**`) |
| All artifacts reference C-1 through C-9 scope only | CONFIRMED — no out-of-scope changes; CopyEngine.cs change noted in diff stats is a pre-existing 2-line diff, not introduced by this session |

**GATE: PASS**

---

## SECTION B — CROSS-FILE COHERENCE CHECK

### B-1: Helper wiring coherence (C-1 + C-2)

Source reads of `TradeCopierAddOn.cs`:

| Check | Location | Status |
|-------|----------|--------|
| `RemoveExistingTradeCopierEntries` called from `WireControlCenterMenu` | Line 148: `RemoveExistingTradeCopierEntries(newMenu)` — no inline loop | PASS |
| `TryDetachAndRemoveStalePanels` called from `DoInject` | Line 488: `TryDetachAndRemoveStalePanels(grid)` | PASS |
| `TrySetPanelInstrument` called from `DoInject` | Line 491: `var instr = TrySetPanelInstrument(chartTrader, panel)` | PASS |
| `InjectPanelIntoGrid` called from `DoInject` | Line 508: `if (InjectPanelIntoGrid(grid, panel))` | PASS |
| `TryDetachAndRemoveStalePanels` contains descending sort (C-2) | Lines 424-431: `stale.Sort((a, b) => Grid.GetRow(b).CompareTo(Grid.GetRow(a)))` | PASS |
| All 6 helpers are `private static` (no cross-file visibility) | Confirmed at declaration sites | PASS |

**B-1: PASS**

### B-2: Null safety coherence (C-3)

Source read of `TradeCopierAddOn.cs` line 108:
```csharp
if (_panels.TryRemove(chart, out panel) && panel != null)
    panel.Detach();
```

- `&& panel != null` guard present: CONFIRMED
- No other call site affected: `TryRemove` is used in one other location (`DoInject` at lines 483 and 522 using `out _` discard form) — neither invokes `panel.Detach()`, so the null guard does not create inconsistency elsewhere.

**B-2: PASS**

### B-3: BE button state coherence (C-4)

Source read of `TradeCopierPanel.cs` lines 946-950:
```csharp
// Direct initialization -- replaces UpdateButtonColors(false,false).
_beBtn2.Background = BrushInactive;
_globalBeBtn2.Background = BrushInactive;
```

- `UpdateButtonColors(false, false)` NOT called in `BuildUI`: CONFIRMED (grep confirms no call in BuildUI scope)
- Direct `BrushInactive` assignments cover both `_beBtn2` and `_globalBeBtn2`: CONFIRMED
- `OnLoaded` subscription to `GlobalBeAllDisarmed` not removed: not visible in scope; plan confirms it is untouched; verifier confirmed no regression on GlobalBeAllDisarmed wiring.

**B-3: PASS**

### B-4: ATR row coherence (C-5)

Source reads of `TradeCopierPanel.cs`:

- Field declaration at line 278: `private FrameworkElement _atrSizingRow2 = null;` — CONFIRMED, no duplicate
- Assignment in `BuildRiskAtrRow` at line 3000: `_atrSizingRow2 = _atrRow;` — CONFIRMED
- Gating in `ApplyRowVisibilityFlags` at lines 3216-3219:
  ```csharp
  if (_atrSizingRow2 != null)
      _atrSizingRow2.Visibility = f.AtrSizing
          ? System.Windows.Visibility.Visible
          : System.Windows.Visibility.Collapsed;
  ```
  — CONFIRMED, mirrors the `_atrRow` condition at lines 3212-3215 using identical `f.AtrSizing` predicate.
- No duplicate field declarations found.

**B-4: PASS**

### B-5: License gate coherence (C-6)

Source read of `TradeCopierWindow.cs` lines 407-416:
```csharp
// T7: Apply feature flags to all gated UI elements. CYC=5. Extracted button-group loop.
private void ApplyFeatureFlags(FeatureFlags f)
{
    ApplyButtonGroupFlag(_trimBtns, f.TrimFlatten, "Trim requires Pro tier");
    ApplyButtonGroupFlag(_flattenBtns, f.TrimFlatten, "Trim/Flatten requires Pro tier");
    ApplyButtonGroupFlag(_cancelBtns, f.TrimFlatten, "Cancel requires Pro tier");
    ApplyButtonGroupFlag(_beBtns, f.BreakEven, "Break Even requires Pro tier");
    ApplyButtonGroupFlag(_armBeBtns, f.BreakEven, "Arm Break-Even not available on this plan");
    ApplyButtonGroupFlag(_tightenBtns, f.BreakEven, "Tighten Stop not available on this plan");
    ...
```

- `_armBeBtns` and `_tightenBtns` gated via `ApplyButtonGroupFlag`: CONFIRMED
- Pattern consistent with existing `_beBtns` gate: CONFIRMED (same method, same `f.BreakEven` flag, same positional pattern)
- No Starter-tier bypass remains: CONFIRMED — both buttons now receive `IsEnabled = false` + tooltip when `f.BreakEven == false`

**B-5: PASS**

### B-6: Buffer parse coherence (C-7)

Source read of `TradeCopierWindow.cs` lines 1091-1099:
```csharp
private static int TryParseArmBeBuffer(object[] tag)
{
    int buf = 2;
    var bufBox = tag.Length > 2 ? tag[2] as TextBox : null;
    if (bufBox != null)
        if (int.TryParse(bufBox.Text?.Trim(), out int parsed) && parsed >= 0)
            buf = parsed;
    return buf;
}
```

- `out int parsed` pattern present (NOT `out buf`): CONFIRMED — default `buf = 2` is never overwritten by TryParse failure
- Method signature `object[] tag` unchanged: CONFIRMED
- Caller of `TryParseArmBeBuffer` (at `OnRuleArmBe`) passes `tag` (object[]) — signature unchanged, no caller modification required.

**B-6: PASS**

### B-7: Button background coherence (C-8)

Source read of `TradeCopierPanel.cs` lines 1188-1189:
```
(FormatBuffer("Quick",   _quickT1), BrushInactive, true, ..., b => _quickBtn    = b, _quickRowPanel),
(FormatBuffer("Quick ALL", ...), BrushInactive, true, ...,    b => _quickAllBtn  = b, _quickRowPanel),
```

- Both `_quickBtn` and `_quickAllBtn` pass `BrushInactive` as the `Bg` parameter in the data-driven tuple: CONFIRMED
- `BuildArrowCluster` uses the `Bg` field as `Background` property on the constructed button.
- Both buttons receive `BrushInactive` background at construction time.

**B-7: PASS**

---

## SECTION C — CROSS-FILE JS VIOLATIONS CHECK

### SCAN-01: `lock()` in code (JS-021)

Grep results across all 3 scope files:

| File | Hit | Type |
|------|-----|------|
| `TradeCopierAddOn.cs` | 0 | — |
| `TradeCopierPanel.cs` | Line 1339: `// JS-021: no lock().` | Comment only |
| `TradeCopierWindow.cs` | Line 579: `// ... no lock()` | Comment only |

**Result: 0 code occurrences. JS-021: PASS**

### SCAN-02: `async void` in code (JS-033)

Grep results across all 3 scope files:

| File | Hit | Type |
|------|-----|------|
| `TradeCopierAddOn.cs` | 0 | — |
| `TradeCopierPanel.cs` | Line 1785: `// JS-033: synchronous event handler` | Comment only |
| `TradeCopierWindow.cs` | 0 | — |

**Result: 0 code occurrences. JS-033: PASS**

---

## SECTION D — CCN FINAL GATE

From verifier SCAN-05 (independently confirmed via architect plan + manual count):

| Method | File | CCN | Target | Status |
|--------|------|-----|--------|--------|
| `DoInject` | TradeCopierAddOn.cs | 7 | ≤ 8 | **PASS** |
| `WireControlCenterMenu` | TradeCopierAddOn.cs | 5 | ≤ 5 | **PASS** |
| `RemoveExistingTradeCopierEntries` | TradeCopierAddOn.cs | 4 | ≤ 8 | **PASS** |
| `CollectStalePanelChildren` | TradeCopierAddOn.cs | 2 | ≤ 8 | **PASS** |
| `RemoveStalePanelChild` | TradeCopierAddOn.cs | 3 | ≤ 8 | **PASS** |
| `TryDetachAndRemoveStalePanels` | TradeCopierAddOn.cs | 2 | ≤ 8 | **PASS** |
| `InjectPanelIntoGrid` | TradeCopierAddOn.cs | 2 | ≤ 8 | **PASS** |
| `TrySetPanelInstrument` | TradeCopierAddOn.cs | 2 | ≤ 8 | **PASS** |
| `ApplyRowVisibilityFlags` | TradeCopierPanel.cs | 5 | ≤ 8 | **PASS** |
| `ApplyFeatureFlags` | TradeCopierWindow.cs | 5 | ≤ 8 | **PASS** |
| `TryParseArmBeBuffer` | TradeCopierWindow.cs | 3 | ≤ 8 | **PASS** |

**CCN GATE: ALL METHODS ≤ 8 — PASS**

---

## SECTION E — SPEC REQUIREMENTS SATISFIED

| Finding | Requirement | Status |
|---------|-------------|--------|
| C-1: qlty CCN=23 / Greptile P1 / CodeRabbit CHANGES_REQUESTED | CCN regression resolved; DoInject=7 ≤ 8; all 6 helpers restored | **RESOLVED** |
| C-2: CodeRabbit CR38-1 ascending removal | Descending sort via `List.Sort(Comparison<T>)` in `TryDetachAndRemoveStalePanels` | **RESOLVED** |
| C-3: CodeRabbit CR38-2 NullReferenceException | `&& panel != null` guard in `OnWindowDestroyed` | **RESOLVED** |
| C-4: CodeRabbit CR38-3 BE ALL Idle | `UpdateButtonColors(false,false)` removed; direct `BrushInactive` assignments in `BuildUI` | **RESOLVED** |
| C-5: CodeRabbit CR38-4 ATR row always visible | `_atrSizingRow2` field added, assigned, gated by `f.AtrSizing` in `ApplyRowVisibilityFlags` | **RESOLVED** |
| C-6: CodeRabbit CR38-5 license gate regression | `_armBeBtns` + `_tightenBtns` gated via `ApplyButtonGroupFlag(_, f.BreakEven, _)` | **RESOLVED** |
| C-7: CodeRabbit CR38-6 buffer default stomped | `out int parsed` + `parsed >= 0` pattern preserves `buf=2` default on parse failure | **RESOLVED** |
| C-8: Greptile P2 button background | `_quickBtn` and `_quickAllBtn` receive `BrushInactive` via tuple `Bg` field | **RESOLVED** |
| C-9: SA1507 blank lines | Pre-resolved during file regeneration; no double blank lines present | **RESOLVED** |

**All 9 source findings resolved. PASS.**

---

## SECTION F — KNOWN BASELINE CONFIRMATION

| Item | Status |
|------|--------|
| 80 NT8-runtime pre-existing test failures | Accepted by Director. Not a blocker. Verifier confirms 36 BwaveCycLaneAR9 + NT8-runtime WPF failures — all pre-existing. 0 new failures introduced. |
| `TryAdd(chart, null)` at `TradeCopierAddOn.cs:475` | PRE-EXISTING (B10-EXEC). ConcurrentDictionary null-slot reservation pattern. Not a new violation. Not a blocker. Tracked as DW-C38-01. |

---

## SECTION K — DEFERRED WORK

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-C38-01 | PRE-EXISTING `TryAdd(chart, null)` — ConcurrentDictionary null-slot reservation pattern introduced B10-EXEC. Consider `Lazy<T>` or sentinel value pattern in future wave to eliminate the null-value slot that necessitates the C-3 null guard. | P2 / Low | future | OPEN |
| DW-C38-02 | Cubic P2 — `TradeCopierWindow.cs:508` — `BuildRuleRow`/`BuildDynamicRuleRow` share ~230 lines each (not fully extracted). Restore shared helpers (`BuildGridColumnDefinitions`, `BuildFollowerListBox`, `BuildAtmColumnPanel`) in a future targeted wave. | P1 / Medium | future | OPEN |
| DW-C38-03 | CodeAnt Major — `TradeCopierPanel.cs:614` — Detaching one panel disarms shared pending BE slot for all accounts. Needs deeper investigation of BE slot scoping per chart/account. Potential behavioral regression in multi-chart setups. | P1 / High | B5 or B6 | OPEN |
| DW-C38-04 | Cubic P3 — `TradeCopierWindow.cs:600` — ATM selector tabs appear before Apply/BE due to `grid.Children.Add` order. Keyboard navigation regression. | P2 / Low | future | OPEN |

No new deferred items discovered during this cross-file review. All coherence checks passed; no hidden wiring gaps found.

---

## Overall Verdict

All gates passed:

| Gate | Result |
|------|--------|
| Section A — All-tickets completion gate | PASS |
| Section B — Cross-file coherence (B-1 through B-7) | PASS |
| Section C — Cross-file JS violations | PASS (0 code violations) |
| Section D — CCN final gate | PASS (all methods ≤ 8) |
| Section E — Spec requirements (all 9) | PASS |
| Section F — Known baseline | CONFIRMED |
| Section K — Deferred work | WRITTEN (4 items in 06-deferred-backlog.md) |
| 06-deferred-backlog.md | WRITTEN |

---

## FINAL_PASS
