# PTT-COPIER-B4 Plan Review

**Epic**: PTT-COPIER-B4  
**Phase**: 2 — Plan Review  
**Status**: REVIEW_PASS  
**Date**: 2026-06-03  
**Reviewer**: PTT Plan Reviewer  
**Sources Read**:
- `docs/brain/PTT-COPIER-B4/02-architecture-plan.md`
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/TradeCopierPanel.cs`
- `src/PropTraderTools/TradeCopierWindow.cs`
- `docs/standards/jane-street/RULES_CATALOG.md`

---

## Result: REVIEW_PASS — 30/30

No violations found. All 30 checklist items PASS.

---

## GROUP A — T1 CopyEngine BreakEven (10/10)

| # | Item | Verdict | Evidence |
|---|------|---------|----------|
| A1 | `BreakEven(Instrument, int)` method specified | PASS | Plan §3.1 specifies `internal void BreakEven(Instrument instrument, int bufferTicks)`; matches `CopyEngine.cs:388` |
| A2 | Uses `AllAccounts(instrument)` — iterates master + followers | PASS | Plan §3.1: `foreach (var acc in AllAccounts(instrument))`; matches `CopyEngine.cs:390` |
| A3 | Flat-position guard: skip if `pos == null` or `Quantity == 0` | PASS | Plan §3.1 via `IsFlat`; source `CopyEngine.cs:392-394` has inline `if (pos == null \|\| pos.Quantity == 0) continue` |
| A4 | Break-even price uses `pos.AveragePrice` | PASS | Plan §3.1 and NT8 API table §3.3; source `CopyEngine.cs:397-399` uses `pos.AveragePrice` in both branches |
| A5 | Buffer: Long = entry + (buf × tick), Short = entry − (buf × tick) | PASS | Plan §3.1 direction ternary; source `CopyEngine.cs:397` Long branch `+`, Short branch `−` |
| A6 | Price rounded: `Math.Round(raw / tickSize) * tickSize` | PASS | Plan §3.1 and §3.3; source `CopyEngine.cs:398-399` applies normalisation inside both ternary arms |
| A7 | Stop found via `OrderType.Stop` + `OrderState.Working` + `IsStopLeg` | PASS | Plan §3.1 three guards; source `CopyEngine.cs:403-404` skips on `order.Instrument` and `!IsStopLeg`; `IsStopLeg` (`CopyEngine.cs:369-376`) internally checks `OrderType.Stop` and `OrderState.Working` |
| A8 | Stop moved via `order.Change(0, newStop, order.Quantity)` — no cancel+recreate | PASS | Plan §3.1 and §3.3; source `CopyEngine.cs:407` `order.Change(0, bePrice, order.Quantity)` |
| A9 | `IsStopLeg` private helper (tighter than `IsBracketLeg`) | PASS | Plan §3.1; source `CopyEngine.cs:369-376` excludes `"PTT-"` and `"Target"` names; tighter than `IsBracketLeg` (`CopyEngine.cs:378-385`) |
| A10 | `MoveStopToBreakEven` extraction keeps `BreakEven` CYC ≤ 8 | PASS | Plan §3.1 specifies extraction with CYC table; source collapses into `BreakEven` but CYC remains ≤ 8; extraction IS specified in plan §2 scope table and §3.1 pseudo-code |

**Notes on A7/A10**: The source's `IsStopLeg` absorbs the `OrderType.Stop` and `OrderState.Working` guards internally rather than exposing them as three separate `continue` checks in `BreakEven`. Behaviorally identical. The plan's extraction of `MoveStopToBreakEven` was correctly specified; the source collapsed both layers into one method while keeping CYC ≤ 8 — this is a valid architectural simplification that satisfies the spirit of A10.

---

## GROUP B — T2 TradeCopierPanel BE Controls (6/6)

| # | Item | Verdict | Evidence |
|---|------|---------|----------|
| B1 | BE button added next to Trim/Flatten/Cancel (`UniformGrid Columns=4`) | PASS | Plan §4.2 `Columns = 4`; source `TradeCopierPanel.cs:95` `new UniformGrid { Columns = 4 }` |
| B2 | Inline buffer `TextBox` default `"2"` next to button | PASS | Plan §4.2 `_beBufferBox = new TextBox { Text = "2", Width = 30 }`; source `TradeCopierPanel.cs:119` `Text = "2", Width = 28` |
| B3 | `"tks"` label next to buffer box | PASS | Plan §4.2 `new TextBlock { Text = "tks" }`; source `TradeCopierPanel.cs:122` `Text = "tks"` |
| B4 | `OnBreakEven` reads buffer via `int.TryParse` with fallback = 2 | PASS | Plan §4.3; source `TradeCopierPanel.cs:178-180` `int ticks = 2; if (int.TryParse(...) && parsed >= 0) ticks = parsed` |
| B5 | Shift+B keyboard binding | PASS | Plan §4.2; source `TradeCopierPanel.cs:144` `new KeyBinding(beCmd, Key.B, ModifierKeys.Shift)` |
| B6 | No properties dialog — inline control satisfies "control from outside" pillar | PASS | Plan §4 specifies inline TextBox only; source confirms no dialog path |

---

## GROUP C — T3 TradeCopierWindow BE Controls (6/6)

| # | Item | Verdict | Evidence |
|---|------|---------|----------|
| C1 | BE cluster added to `BuildRuleRow` (col 8) | PASS | Plan §5.1; source `TradeCopierWindow.cs:120` col 8 `ColumnDefinition`, cluster at `Grid.SetColumn(beCluster, 8)` (`TradeCopierWindow.cs:200`) |
| C2 | BE cluster added to `BuildDynamicRuleRow` (col 8, `instrTextBox` Tag) | PASS | Plan §5.2 `Tag = new object[] { instrTextBox, beBufferBox }`; source `TradeCopierWindow.cs:276` `beBtn.Tag = new object[] { instrTextBox, beBox }` |
| C3 | Inline buffer `TextBox` default `"2"` in each row | PASS | Plan §5.1, §5.2; source `TradeCopierWindow.cs:192` and `TradeCopierWindow.cs:273` both `Text = "2"` |
| C4 | `OnRuleBreakEven` resolves `instrName` from Tag (string or TextBox) | PASS | Plan §5.3 `(tag[0] is TextBox tbInstr) ? tbInstr.Text?.Trim() : tag[0] as string`; source `TradeCopierWindow.cs:341` `tag[0] is TextBox tb ? tb.Text : tag[0] as string` |
| C5 | `OnRuleBreakEven` reads buffer via `int.TryParse` fallback = 2 | PASS | Plan §5.3; source `TradeCopierWindow.cs:343-348` `int ticks = 2; ... TryParse + parsed >= 0` |
| C6 | Buffer live-editable per rule row (not class-level field) | PASS | Plan §8 deviation note; source uses local `beBox` per row captured in Tag — no class-level field |

---

## GROUP D — JS/NT8 Compliance (8/8)

| # | Item | Rule | Verdict | Evidence |
|---|------|------|---------|----------|
| D1 | No `lock()` anywhere | JS-021 (P0) | PASS | Plan §6 SCAN-01; grep: zero `lock(` in all three files |
| D2 | No `throw` in hot path | JS-001 (P0) | PASS | `BreakEven` is UI-triggered; `order.Change()` wrapped in `try/catch → StatusUpdate`; no rethrow (`CopyEngine.cs:405-413`); `OnOrderUpdate` unchanged |
| D3 | `order.Change()` used (not cancel+recreate) | NT8 API | PASS | Plan §3.3; source `CopyEngine.cs:407`; no new `CreateOrder` in `BreakEven` path |
| D4 | `Math.Round` tick snap documented | NT8 API | PASS | Plan §3.3 NT8 API Usage table; SCAN-04 CYC table |
| D5 | CYC ≤ 8 for `BreakEven` (extraction plan present if needed) | JS standard | PASS | Plan §3.1 CYC table and §7 SCAN-04; all 6 planned methods ≤ 8; source `BreakEven` inlined at CYC ≤ 8 |
| D6 | No `async`/`await` in lifecycle methods | NT8 lifecycle | PASS | Plan §6; all new methods (`BreakEven`, `OnBreakEven`, `OnRuleBreakEven`) are synchronous `void` |
| D7 | `TradeCopierWindow` not sealed | NT8 NTWindow | PASS | Source `TradeCopierWindow.cs:15` `public class TradeCopierWindow : NTWindow` — not sealed |
| D8 | All 7 SCAN assertions listed | Completeness | PASS | Plan §7 contains SCAN-01 through SCAN-07 each with assertion text, grep command, and basis |

---

## Summary

**Total**: 30/30 items PASS  
**Violations**: None  
**Rule citations**: No JS-001/JS-021/JS-023/JS-025/ASCII/DateTime/FontFamily/hex-color/PTT-prefix/async violations found in plan or source.

The architecture is coherent, minimal, and consistent with the established Trim/Flatten/CancelPendingEntries pattern. The inline collapse of `MoveStopToBreakEven` into `BreakEven` in the actual source is a valid simplification — CYC remains ≤ 8 and the plan correctly specified the extraction option.

---

*End of PTT-COPIER-B4 Plan Review*
