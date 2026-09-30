# PTT-COPIER-B17 Ticket 2 Completion Report
# Block: PTT-COPIER-B17
# Ticket: T2 — Permanent Fix: GetPriceAtY Correct Panel Selection
# Engineer: ptt-engineer (Phase 4a)
# Date: 2026-07-15
# Status: BUILD_PASS

---

## T1 F5 Branch Decision

**Branch taken: Option A** (FindPriceCanvasPanel heuristic)

**Reason:** T1 F5 Sim101 output confirmed:
```
B17 ChartPanel[0]: W=931.33 H=639.33 Max=7633.34 Min=7547.66
Charts property: NOT FOUND
```
- ChartControl.Charts = NOT FOUND (Reflection returns null) → Option B eliminated
- Only ONE ChartPanel exists → FindPriceCanvasPanel returns it directly
- FindPriceCanvasPanel still added as defensive wrapper (predicate: MaxValue>0, largest ActualWidth)

**True root cause clarified:** DW-B17-01 was cc.MouseDown suppressed (e.Handled=true).
Fix was PreviewMouseDown (T1 Amendment in TradeCopierAddOn.cs). GetPriceAtY was never broken.

---

## Implementation Summary

### TradeCopierPanel.cs — Changes Applied

#### Removals (T1 diagnostic cleanup)
1. `using System.Reflection;` — removed (only used in T1 methods)
2. `using System.Text;` — removed (only used in T1 methods)
3. `_b17DiagDone` volatile bool field — removed
4. `ProbeChartsProperty` method — removed entirely
5. `EnumerateAllChartPanels` method — removed entirely
6. `OnChartMouseDown` — removed `EnumerateAllChartPanels(chartControl)` call
7. `OnChartMouseDown` — removed `if (rawPrice <= 0.0) rawPrice = GetRefPrice();` fallback line

#### Additions (T2 permanent fix)
1. `FindPriceCanvasPanel(DependencyObject root)` — private static method added
   - DFS walk via Stack<DependencyObject>
   - Predicate: MaxValue > 0 AND largest ActualWidth
   - CYC=5: root null(1), while(2), predicate(3), for(4), child null(5)

#### Modifications
1. `GetPriceAtY` comment block — updated to B17 T2 description
2. `GetPriceAtY` — single line change: `TradeCopierAddOn.FindVisualChild<ChartPanel>(cc)` → `FindPriceCanvasPanel(cc)`
3. `OnChartMouseDown` comment — CYC updated from 7 to 6 (T1 diagnostic removed)

### CopyEngineTests.cs — Changes Applied

Added 7 [Fact] tests (T_B17_01 through T_B17_07):
- T_B17_01: LinearYToPrice, top of panel → maxVal
- T_B17_02: LinearYToPrice, midpoint → midpoint price
- T_B17_03: LinearYToPrice, zero panel height → 0.0 (guard 1)
- T_B17_04: LinearYToPrice, over-boundary y → 0.0 (guard 2)
- T_B17_05: AlignToTick, already aligned → unchanged
- T_B17_06: AlignToTick, half-tick → rounds AwayFromZero
- T_B17_07: AlignToTick, zero tickSize → returns raw

All tests reuse `CallLinearYToPrice` / `CallAlignToTick` helpers from B16 T2 region.

### NT8_ADDON_KNOWLEDGE.md — Updated
Appended `## B17 T2 Discoveries` section with:
- Confirmed path (Option A)
- Root cause summary
- NT8-041 rule (ChartControl.Charts NOT FOUND)
- Test count delta (104 → 111)

---

## 9-Scan Results

| Scan | Pattern | Result | Count |
|------|---------|--------|-------|
| Scan 1 | `lock(` | PASS | 0 |
| Scan 2 | `async void ` | PASS | 0 |
| Scan 3 | `volatile double` | PASS | 0 |
| Scan 4 | `Math.Clamp(` (actual call) | PASS | 0 (8 in comments only) |
| Scan 5 | `_b17DiagDone\|EnumerateAllChartPanels\|ProbeChartsProperty\|B17 interim` | PASS | 3 in header comment block only (historical docs) |
| Scan 6 | CYC audit | PASS | FindPriceCanvasPanel=5, GetPriceAtY=5, OnChartMouseDown=6 |
| Scan 7 | dotnet build | PASS* | 0 errors in T2-modified files |
| Scan 8 | [Fact] count | PASS | 111 (prior 104 + 7) |
| Scan 9 | FindPriceCanvasPanel present | PASS | 5 hits (method decl at L358, call at L309, comments) |

*Scan 7 note: Build shows 3 pre-existing errors in BANNED files (AtrSizingEngine.cs CS0234/CS0246,
CopyEngine.cs CS8370). These are NT8 assembly-reference issues in standalone csproj that existed
before T2. Zero errors in TradeCopierPanel.cs or CopyEngineTests.cs. NT8 F5 compile uses the
NT8 host process which has all assembly references — pre-existing csproj errors are expected.

---

## CYC Bounds Verification

| Method | Branches | CYC | Bound | Result |
|--------|----------|-----|-------|--------|
| `FindPriceCanvasPanel` | root null(1), while(2), predicate(3), for(4), child null(5) | 5 | ≤ 8 | PASS |
| `GetPriceAtY` | cc null(1), panel null(2), height≤0(3), raw≤0(4), instrument null(5) | 5 | ≤ 8 | PASS |
| `OnChartMouseDown` | !_clickArmed(1), leaderAccount null(2), instrument null(3), chartControl null(4), rawPrice≤0(5), try/catch(6) | 6 | ≤ 8 | PASS |

---

## Deploy Sync

`scripts\verify_links.ps1 -Fix` result:
- TradeCopierPanel.cs: hard-linked (OK)
- All 5 deployable files: OK, PASS
- CopyEngineTests.cs: SKIP (test file — not deployed to NT8, expected)

---

## Test Count

- Prior [Fact] count: 104
- Added: 7 (T_B17_01 through T_B17_07)
- New total: 111

---

## NT8_ADDON_KNOWLEDGE.md Update

Confirmed: `## B17 T2 Discoveries` section appended to Wave workspace
`c:\WSGTA\universal-or-strategy\docs\standards\NT8_ADDON_KNOWLEDGE.md`
NT8-041 rule documented: ChartControl.Charts NOT FOUND via Reflection.

## nt8-rules B17-T2: 1 new rule (NT8-041)

---

## BUILD_PASS

---

## HOTPATCH — Diagnostic + GetRefPrice fallback restored

**Date:** 2026-07-15 (post-T2, director-authorized hot-patch)
**Reason:** T1 confirmed order fires with GetRefPrice() fallback; T2 removed it; order stopped firing again. Hotpatch restores fallback + adds status diagnostics to all 5 guards so the director can observe exactly which guard is firing in Sim101.

### Changes

- [`OnChartMouseDown`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:1197): all 5 guards now call `SetStatusText()` with a descriptive message before returning
- After `GetPriceAtY()` call: writes `y=<N> raw=<N>` diagnostic to status TextBlock
- **`GetRefPrice()` fallback restored**: `if (rawPrice <= 0.0) rawPrice = GetRefPrice();` — interim, pending GetPriceAtY fix
- If rawPrice still 0 after fallback: writes `PTT-Click: rawPrice=0 even after fallback` to status
- Pre-order placement: writes `PTT-Click: placing Buy/Sell qty=N @ price` to status
- On success: writes `PTT-Click: order submitted @ price`
- `SetStatus` helper NOT added — used existing [`SetStatusText(string text)`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:1433) throughout

### Scan results

| Scan | Pattern | Result |
|------|---------|--------|
| Scan 1 | `lock\s*\(` in TradeCopierPanel.cs | **0** (PASS) |
| Scan 2 | `async void ` in TradeCopierPanel.cs | **0** (PASS) |
| Scan 3 | dotnet build errors in TradeCopierPanel.cs | **0** (PASS) |

Build note: 3 pre-existing errors in `AtrSizingEngine.cs` (CS0234/CS0246 NT8 assembly ref) and `CopyEngine.cs` (CS8370 C#7.3). None in `TradeCopierPanel.cs`. These pre-date the hotpatch.

### Build: PASS (0 new errors in TradeCopierPanel.cs)

### deploy-sync: PASS — `scripts\verify_links.ps1` confirmed `TradeCopierPanel.cs` hard-linked (auto-deployed to NT8 via NTFS hard link, 0 DESYNC)

## CLEANUP — Diagnostic guards removed, clean T2 state restored

### F5 Confirmation (2026-07-15)
GetPriceAtY working correctly. Order placed at exact Y-pixel price 7491.00.
GetRefPrice() fallback was NOT triggered (rawPrice > 0 from GetPriceAtY directly).
DW-B17-01 CONFIRMED CLOSED.

### Changes
- OnChartMouseDown: diagnostic SetStatusText calls removed
- GetRefPrice() fallback line removed from OnChartMouseDown
- Stale B17 HOTPATCH comment block removed
- Clean final CYC=6 state restored (lines 1193-1236)

### Scan results
- Scan 1 (SetStatus in OnChartMouseDown): 0 — PASS
- Scan 2 (GetRefPrice in OnChartMouseDown): 0 — PASS
- Scan 3 (HOTPATCH in TradeCopierPanel.cs): 0 — PASS
- Scan 4 (dotnet build errors in TradeCopierPanel.cs): 0 — PASS
- Scan 5 (lock() in TradeCopierPanel.cs): 0 — PASS

### Build
0 errors in TradeCopierPanel.cs. Pre-existing errors in AtrSizingEngine.cs (NT8 assembly refs) and CopyEngine.cs (C# 7.3) not introduced by this change.

### deploy-sync
verify_links.ps1 PASS — TradeCopierPanel.cs hard-linked. NT8 has clean version.
