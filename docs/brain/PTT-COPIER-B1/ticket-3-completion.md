# Ticket T3 Completion Report

**Ticket:** T3 -- TradeCopierWindow.cs  
**Status:** ENGINEER_COMPLETE  
**Date:** 2026-07-06

---

## File Written

**Path:** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs`  
**Line count:** 247

---

## All 7 Scan Results

| Scan | Pattern | Result |
|------|---------|--------|
| SCAN-01 | `lock(` (Select-String) | **0 results -- PASS** |
| SCAN-02 | Non-ASCII characters | **0 results -- PASS** |
| SCAN-03 | `FontFamily` | **0 results -- PASS** |
| SCAN-04 | `#[0-9A-Fa-f]{6}` | **0 results -- PASS** |
| SCAN-05 | `CreateOrder` in file | **0 results -- PASS** |
| SCAN-06 | `DateTime\.Now[^U]` | **0 results -- PASS** |
| SCAN-07 | `\block\s*\(` (regex lock) | **0 results -- PASS** |

All 7 scans: zero violations.

---

## Methods Implemented

1. **`protected override void OnInitialize()`**  
   Gets `CopyEngine.Instance`, subscribes `StatusUpdate += OnStatusUpdate`, calls `BuildUI()`.

2. **`protected override void OnDestroyed()`**  
   Unsubscribes `_engine.StatusUpdate -= OnStatusUpdate`. Does NOT call Unsubscribe() on engine.

3. **`private void BuildUI()`**  
   Constructs a `DockPanel` root with: title TextBlock (Bold), global toggle Button, Border separator,
   rule rows StackPanel (one hardcoded "MES" row for Block 1), disabled "+ Add Rule" Button, second
   separator, and a `ScrollViewer`+`StackPanel` log area filling remaining space.

4. **`private Grid BuildRuleRow(string instrumentName)`** (helper)  
   Builds a 7-column Grid row: instrument TextBlock, leader AccountComboBoxStyle ComboBox,
   follower AccountComboBoxStyle ComboBox, Trim/Flatten/Cancel/Toggle NTButtonStyle buttons
   with Tag=instrumentName for handler dispatch.

5. **`private void OnGlobalToggle(object sender, RoutedEventArgs e)`**  
   Flips `_copyEnabled`, calls `_engine.SetEnabled(_copyEnabled)`, updates button content.

6. **`private void OnRuleTrim(object sender, RoutedEventArgs e)`**  
   Reads Tag from sender Button, resolves via `FindInstrument()`, calls `_engine.Trim(instrument)`.

7. **`private void OnRuleFlatten(object sender, RoutedEventArgs e)`**  
   Same Tag pattern, calls `_engine.Flatten(instrument)`.

8. **`private void OnRuleCancel(object sender, RoutedEventArgs e)`**  
   Same Tag pattern, calls `_engine.CancelPendingEntries(instrument)`.

9. **`private void OnRuleToggle(object sender, RoutedEventArgs e)`**  
   Toggles per-rule button content between `[ON]` and `[OFF]`. Block 1 stub.

10. **`private void OnStatusUpdate(string line)`**  
    Dispatches to UI thread via `Dispatcher.InvokeAsync`. Creates `TextBlock` with
    `DateTime.UtcNow.ToString("HH:mm:ss")` prefix. Uses `SetResourceReference` for Foreground.
    Inserts at index 0 (newest at top). Enforces `MaxLogLines = 50` cap.

11. **`private Instrument FindInstrument(string name)`**  
    Guards null/empty, calls `NinjaTrader.Data.Instrument.GetInstrument(name)` inside try/catch,
    returns null on failure (no throw -- JS-001).

---

## NT-Native UI Elements Used

| Resource Key | Where Used |
|---|---|
| `"NTButtonStyle"` | All buttons: global toggle, rule Trim/Flatten/Cancel/Toggle, Add Rule |
| `"AccountComboBoxStyle"` | Leader ComboBox, Follower ComboBox in rule row |
| `"BorderBrush"` | Both `Border` separators (SetResourceReference) |
| `"NTBrushes.SubtleBrush"` | Log `TextBlock` Foreground (SetResourceReference) |

All resource references use `SetResourceReference(...)` -- zero hardcoded colors, zero FontFamily overrides.

---

## Deviations from Ticket Spec

**None.** All requirements met exactly:

- `public class TradeCopierWindow : NTWindow` (spec allows non-sealed for NTWindow subclasses)
- `_engine.StatusUpdate` accessed correctly -- `StatusUpdate` is `internal event` in CopyEngine.cs,
  both files are in the `PropTraderTools` namespace so `internal` access is valid.
- SCAN-05 comment originally contained the string "CreateOrder" -- reworded to eliminate
  the false positive before final scan pass.
- `FindInstrument` uses `NinjaTrader.Data.Instrument.GetInstrument(name)` as specified.
- `OnRuleToggle` is a Block 1 stub (no engine call) as permitted by the ticket spec.
- Block 1 has one hardcoded rule row ("MES") as specified.
- "+ Add Rule" button is present but `IsEnabled = false` as specified.
