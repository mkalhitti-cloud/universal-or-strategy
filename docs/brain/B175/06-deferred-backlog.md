# Deferred Backlog -- Cumulative
# Last updated by: ptt-plan-reviewer (Phase 5)
# Block: B175
# Epics closed: DW-B175-ATR-FORMULA, DW-B175-ATR-TEXTBOX-FOCUS

---

## B175 Items

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B175-ATR-FORMULA | B175 | AtrCalc Wilder's ATR fix -- simple average replaced by Wilder's seed+smoothing (2*period warmup). ChartControl overload reads up to 2*period+1 bars via ternary readLen + ReadBarsToArrays helper. | P1 | **CLOSED -- B175-1 VERIFY_PASS** |
| DW-B175-ATR-TEXTBOX-FOCUS | B175 | ATR% TextBox PreviewKeyDown fix -- PreviewKeyDown subscription added in BuildAtrWidgetRows; OnAtrBoxPreviewKeyDown handler sets e.Handled=true when IsKeyboardFocused, blocking NT8 chart-level interceptor. | P1 | **CLOSED -- B175-2 VERIFY_PASS** |
| DW-B175-SIM | B175 | SIM GATE: Director verifies PTT ATR matches chart header ATR; TextBox typing works without search dialog. Steps: (1) Open chart with >28 bars. (2) Wait ~2s for timer tick. (3) Confirm widget pts approx equals NT8 ATR * fraction. (4) Click ATR% box, type a number -- verify no search dialog. Close upon Director SIM confirmation. | P1 | OPEN -- SIM pending post-B175 |

---

## B174 Items (carried forward)

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B174-ATR-INSTRUMENT-MISMATCH | B174 | ATR widget BarsArray[0] hardcode -- instrument-scan fix implemented. | P1 | **CLOSED -- B174 VERIFY_PASS** |
| DW-B174-SIM | B174 | SIM GATE: Director verifies separate-window ATR shows instrument-correct values post-B174. | P1 | **CLOSED -- Director confirmed B174 scan working** |

---

## B173 Items (carried forward)

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B173-01 | B173 | SIM GATE: AtrWidget live ATR display. Root cause (AtrSizingEngine instantiated outside NinjaScript pipeline) eliminated by B173. | P1 | **CLOSED -- Director SIM confirmed** |

---

## Prior Open Items (carried forward from B172 / B171 / B170 / B169 / B168 / B167 / B159)

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B170-SIM | B170 | SIM GATE: Director drags target on leader -- confirm follower stop preserved (not missing) after drag. B171 fix deployed (unsuffixed stop name was root cause of stop-drag sync failure). SIM verification required; close upon Director SIM confirmation. | P0 | OPEN (PENDING SIM GATE) |
| DW-B169-K1 | B169 | SIM GATE: 3-stop simultaneous drag -- 0 missing follower stops | P0 | OPEN (partially verified; Sim102 blocked by B167-DEFER-02) |
| DW-B169-K2 | B169 | SIM GATE: single-stop drag regression | P0 | OPEN |
| DW-B168-K1 | B168 | Superseded by DW-B169-K1/K2 | P0 | OPEN (superseded) |
| B167-DEFER-02 | B167 | fo=NULL: follower ATM brackets never placed after leader fill. Dedicated block required. Blocks DW-B169-K1 Sim102. | P1 | OPEN |
| B167-DEFER-01 | B167 | ECA-1 double-position race. Monitor at SIM. | P2 | OPEN |
| B167-DEFER-03 | B167 | Combined B161-B166 SIM gate pending | P1 | OPEN |
| B167-DEFER-04 | B167 | B167 SIM gate pending | P1 | OPEN |
| DW-B159-K2 | B159 | 14 xUnit tests | P2 | OPEN |
| DW-B159-K3 | B159 | Attempt cap hardcoded at 3 | P2 | OPEN |
| DW-B159-K4 | B159 | intendedBePrice sourcing | P2 | OPEN |
| DW-B159-K5 | B159 | Stale line comment | P3 | OPEN |

---

## Notes

**B175 changes summary:**
- `AtrCalc.cs` (141 lines final): `Compute(double[])` Wilder's formula replaces simple avg.
  `Compute(ChartControl)` bar-read block replaced by ternary readLen + ReadBarsToArrays call.
  New private static `ReadBarsToArrays` helper added (oldest-first array fill, CYC=2).
  sum/period absent. CYC: Compute(double[])=8, Compute(ChartControl)=8, ReadBarsToArrays=2.
- `TradeCopierPanel.cs`: AtrWidget.BuildAtrWidgetRows gains
  `_atrFractionBox.PreviewKeyDown += OnAtrBoxPreviewKeyDown;` (line 3213).
  New private void `OnAtrBoxPreviewKeyDown` added (lines 3312-3316), CYC=2.
  Guard: `_atrFractionBox != null && _atrFractionBox.IsKeyboardFocused` before e.Handled=true.
- `CopyEngineTests.cs`: `B175AtrCalcTests` class appended (lines 10318-10422) with 4 [Fact]
  tests. T1-01: constant TR identity. T1-02: spike Wilder's > simple avg. T1-03: short-history
  fallback. T1-04: B174 regression guard (7/3). All 4 pass.
- ptt-sync.ps1: 18 linked, 0 errors (run after B175-2).
- All 7 scans zero at T3 terminal state. dotnet build 0 errors. dotnet test 0 new failures.

**Highest-risk open items (updated post-B175):**
- `DW-B175-SIM` (P1): NEW -- requires Director SIM run to verify ATR formula correctness
  and TextBox focus fix in live NT8 environment.
- `DW-B170-SIM` (P0): requires Director SIM run to verify stop-drag sync fix from B171.
- `B167-DEFER-02` (P1): follower ATM brackets never placed after leader fill when fo=NULL.
  Blocks DW-B169-K1 Sim102.
