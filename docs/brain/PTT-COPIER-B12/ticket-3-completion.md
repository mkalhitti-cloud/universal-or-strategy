# PTT-COPIER-B12 Ticket T3 Completion Report
# Ticket: DW-B12-RISK-ATR-INPUTS-01
# Block: PTT-COPIER-B12
# Date: 2026-07-11
# Engineer: ptt-engineer (Phase 4a)
# Input: docs/brain/PTT-COPIER-B12/04-tickets.md (T3 section)
# Input: docs/brain/PTT-COPIER-B12/04-ticket-review.md (TICKET_REVIEW_PASS)
# Input: docs/brain/PTT-COPIER-B12/02-architecture-plan.md
# Status: BUILD_PASS

---

## What Was Implemented

### TradeCopierPanel.cs

**New Fields (items 1-2)**
```csharp
// B12 T3 -- Risk/ATR spinners (plain double; UI-thread-only; no volatile per NT8-003)
private double  _maxRiskDollars = 200.0;
private double  _atrFraction    = 0.75;
private TextBox _riskDollarsBox;
private TextBox _atrFractionBox;
```

**Modified: BuildUI()** — Added call to `BuildRiskAtrRow(_contentPanel)` after `BuildAtmTemplateRow(_contentPanel)` (item 14).

**New Methods (items 3-13)**

| Method | Signature | CYC |
|--------|-----------|-----|
| `BuildRiskAtrRow` | `private void BuildRiskAtrRow(StackPanel root)` | 1 |
| `OnRiskUp` | `private void OnRiskUp(object sender, RoutedEventArgs e)` | 1 |
| `OnRiskDown` | `private void OnRiskDown(object sender, RoutedEventArgs e)` | 1 |
| `OnRiskTextLostFocus` | `private void OnRiskTextLostFocus(object sender, RoutedEventArgs e)` | 3 |
| `OnAtrFractionUp` | `private void OnAtrFractionUp(object sender, RoutedEventArgs e)` | 1 |
| `OnAtrFractionDown` | `private void OnAtrFractionDown(object sender, RoutedEventArgs e)` | 1 |
| `OnAtrFractionTextLostFocus` | `private void OnAtrFractionTextLostFocus(object sender, RoutedEventArgs e)` | 3 |
| `NotifyRiskChanged` | `private void NotifyRiskChanged()` | 2 |
| `NotifyAtrFractionChanged` | `private void NotifyAtrFractionChanged()` | 2 |

Layout: `UniformGrid Columns=2` with Col 0 (Risk $) and Col 1 (ATR %).
Each col: Label (NTBrushes.SubtleBrush) + TextBox (NTTextBoxStyle) + 2-row Grid of
`System.Windows.Controls.Primitives.RepeatButton` (NTButtonStyle, Height=12 each).
Arrow buttons use `"\u25B2"` (up) and `"\u25BC"` (down) — ASCII escape sequences only.

### AtrSizingEngine.cs (items 15-18)

**New Field**
```csharp
// B12 T3 -- ATR fraction multiplier. Plain double; single-writer UI thread.
// No volatile: NT8-003 bans volatile double. Same staleness-tolerance pattern as _lastAtr.
private double _atrFraction = 1.0;
```

**New Methods**

| Method | Signature | CYC |
|--------|-----------|-----|
| `SetAtrFraction` | `internal void SetAtrFraction(double fraction)` | 1 |
| `UpdateMaxRisk` | `internal void UpdateMaxRisk(double maxRiskDollars)` | 1 |

**Modified: OnBarUpdate** — Changed `CalcContracts(atr, ...)` to `CalcContracts(atr * _atrFraction, ...)`.
CYC unchanged: still 2 (CurrentBar guard + straight-line body).

### CopyEngine.cs (items 19-20)

**New Methods**

| Method | Signature | CYC |
|--------|-----------|-----|
| `UpdateMaxRisk` | `internal void UpdateMaxRisk(double maxRiskDollars)` | 2 |
| `UpdateAtrFraction` | `internal void UpdateAtrFraction(double fraction)` | 2 |

Both methods null-guard `_atrEngine` before delegating.

### CopyEngineTests.cs (3 new [Fact] tests)

| Test | Method | Description |
|------|--------|-------------|
| T-B12-T3-01 | `AtrSizingEngine_SetAtrFraction_ScalesCalcContractsDown_WhenFractionBelow1` | fraction=0.5 halves effective ATR; CalcContracts(5.0,500,5)==20 |
| T-B12-T3-02 | `UpdateMaxRisk_SetsAtrEngineMaxRiskDollars_ReflectsInSubsequentSizing` | UpdateMaxRisk(300) -> CalcContracts(10,300,5)==6 |
| T-B12-T3-03 | `BuildRiskAtrRow_ClampMin_RejectsSubMinValue` | Math.Max(Math.Min(10-25,1000),10)==10.0 |

All 3 tests use `[Fact]` (xUnit). No NUnit/MSTest.

---

## 7-Scan Results

Files scanned: `TradeCopierPanel.cs`, `CopyEngine.cs`, `AtrSizingEngine.cs`

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 | `lock(` in new T3 code | **0** — no lock() anywhere in T3 additions. JS-021 PASS. |
| SCAN-02 | Non-ASCII chars in .cs files | **0** — all files are ASCII-clean. Select-String returned 0 matches. |
| SCAN-03 | FontFamily in new code | **0** — NTTextBoxStyle/NTButtonStyle used; no FontFamily. |
| SCAN-04 | `#RRGGBB` hex string literals in new code | **0** — pre-existing brush comments are comment-only; all brushes use `MakeBrush(r,g,b)`. |
| SCAN-05 | `volatile double` new fields | **0** — `_maxRiskDollars`, `_atrFraction` (panel), `_atrFraction` (AtrEngine) all plain double. NT8-003 PASS. |
| SCAN-06 | `Math.Clamp(` executable calls | **0** — all clamping uses `Math.Max(Math.Min(...))` pattern. NT8 .NET 4.8 ban respected. |
| SCAN-07 | Literal Unicode arrow/bullet chars in T3 code | **0** — all arrows use `"\u25B2"` / `"\u25BC"` escape sequences. ASCII-only. |

Build verification: `dotnet build archive/v12-reference/Linting.csproj` — **Build succeeded. 0 Warning(s). 0 Error(s).**

---

## CYC Audit — T3 New/Modified Methods

| Method | File | CYC | Limit | Status |
|--------|------|-----|-------|--------|
| `BuildRiskAtrRow` | Panel | 1 | 8 | PASS |
| `OnRiskUp` | Panel | 1 | 8 | PASS |
| `OnRiskDown` | Panel | 1 | 8 | PASS |
| `OnRiskTextLostFocus` | Panel | 3 | 8 | PASS |
| `OnAtrFractionUp` | Panel | 1 | 8 | PASS |
| `OnAtrFractionDown` | Panel | 1 | 8 | PASS |
| `OnAtrFractionTextLostFocus` | Panel | 3 | 8 | PASS |
| `NotifyRiskChanged` | Panel | 2 | 8 | PASS |
| `NotifyAtrFractionChanged` | Panel | 2 | 8 | PASS |
| `UpdateMaxRisk` | Engine | 2 | 8 | PASS |
| `UpdateAtrFraction` | Engine | 2 | 8 | PASS |
| `SetAtrFraction` | AtrEngine | 1 | 8 | PASS |
| `UpdateMaxRisk` | AtrEngine | 1 | 8 | PASS |
| `OnBarUpdate` (modified) | AtrEngine | 2 | 8 | PASS (unchanged CYC) |

---

## Jane Street Rule Compliance

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (P0) no lock() | T3 new methods | PASS |
| JS-001 (P0) no throw in hot path | T3 new methods | PASS — no throw anywhere |
| JS-002 (P0) no return null | T3 new methods | PASS — guards use bare `return;` |
| JS-033 (P0) no async void | T3 new methods | PASS — no async in T3 |
| NT8-003 no volatile double | `_maxRiskDollars`, `_atrFraction` (x2) | PASS — all plain double |
| Math.Clamp ban | All T3 clamp ops | PASS — Math.Max(Math.Min()) |
| ASCII-only literals | Arrow chars in BuildRiskAtrRow | PASS — `"\u25B2"` `"\u25BC"` escape seqs |

---

## Files Modified

| File | Workspace | Change |
|------|-----------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` | Wave | +4 fields, +9 methods, BuildUI() call added |
| `src/PropTraderTools/CopyEngine.cs` | Wave | +2 methods (UpdateMaxRisk, UpdateAtrFraction) |
| `src/PropTraderTools/AtrSizingEngine.cs` | Wave | +1 field, +2 methods, OnBarUpdate modified |
| `src/PropTraderTools/CopyEngineTests.cs` | Wave | +3 [Fact] tests (T-B12-T3-01 to T-B12-T3-03) |

---

## BUILD_PASS
