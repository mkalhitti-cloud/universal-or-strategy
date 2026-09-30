# Deferred Backlog -- Cumulative
# Last updated by: ptt-plan-reviewer (Phase 5)
# Block: B174
# Epic closed: DW-B174-ATR-INSTRUMENT-MISMATCH

---

## B174 Items

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B174-ATR-INSTRUMENT-MISMATCH | B174 | ATR widget BarsArray[0] hardcode — instrument-scan fix implemented. AtrCalc.Compute now scans BarsArray by instrument FullName; falls back to index-zero for single-instrument windows. AtrWidget._instrument field + SetInstrument added. TradeCopierPanel.SetInstrument now propagates to _widget. | P1 | **CLOSED -- B174 VERIFY_PASS** |
| DW-B174-SIM | B174 | SIM GATE: Director verifies separate-window ATR shows instrument-correct values post-B174. Steps: (1) Open two separate chart windows: one MES, one MGC. (2) Wait ~2s for ATR poll timer to fire on both panels. (3) Confirm MES panel shows MES ATR value and MGC panel shows MGC ATR value -- the two values must differ. Close upon Director SIM confirmation. | P1 | OPEN -- SIM pending |

---

## B173 Items (carried forward)

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B173-01 | B173 | SIM GATE: AtrWidget live ATR display. Root cause (AtrSizingEngine instantiated outside NinjaScript pipeline) eliminated by B173. AtrCalc.cs created with pure-static Compute overloads; AtrSizingEngine.cs deleted. | P1 | **CLOSED -- Director SIM confirmed live ATR showing** |

---

## Prior Open Items (carried forward from B172 / B171 / B170 / B169 / B168 / B167 / B159)

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B170-SIM | B170 | SIM GATE: Director drags target on leader -- confirm follower stop preserved (not missing) after drag. B171 fix deployed (unsuffixed stop name was root cause of stop-drag sync failure). SIM verification required; close upon Director SIM confirmation. | P0 | OPEN (PENDING SIM GATE) |
| DW-B169-K1 | B169 | SIM GATE: 3-stop simultaneous drag -- 0 missing follower stops | P0 | OPEN (partially verified -- Sim103/104 PASS, Sim102 blocked by B167-DEFER-02) |
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

**B174 changes summary:**
- `AtrCalc.cs` updated (97 -> 115 lines): ChartControl overload signature extended with optional
  `string instrumentFullName = null` parameter. Hardcoded `cc.BarsArray[0].Bars` replaced by
  instrument-name scan loop (CYC 5 -> 8). BarsArray[0] literal absent (SCAN-05 = 0).
- `TradeCopierPanel.cs`: `AtrWidget` inner class gains `_instrument` field (CBI.Instrument, null-
  initialized, UI-thread-only, JS-PUSH-01 documented) and `SetInstrument` method (CYC=1).
  `AtrWidget.OnTimerTick` updated to pass `_instrument?.FullName` as third arg to AtrCalc.Compute.
  Outer `TradeCopierPanel.SetInstrument` updated to call `_widget?.SetInstrument(instrument)`.
- `CopyEngineTests.cs`: `B174AtrCalcTests` class appended (lines 10261-10311) with 3 [Fact] tests.
  T_B174_T1_01: pure-array regression guard (ATR(3) = 5.5/3, exact). T_B174_T1_02: delegation
  path returns positive. T_B174_T1_03: empty/null instrumentFullName => hasName=false. All pass.
- ptt-sync.ps1: 18 linked, 0 errors. AtrCalc.cs linked.
- All 7 scans zero at T3 terminal state. dotnet build 0 errors. dotnet test 0 new failures.

**Highest-risk open items (updated post-B174):**
- `DW-B174-SIM` (P1): NEW -- requires Director SIM run to verify separate-window ATR correctness.
- `DW-B170-SIM` (P0): requires Director SIM run to verify stop-drag sync fix from B171.
- `B167-DEFER-02` (P1): follower ATM brackets never placed after leader fill when fo=NULL.
  Blocks DW-B169-K1 Sim102.
- `DW-B173-01` (P1): **CLOSED** -- Director SIM confirmed live ATR post-B173.
- `DW-B174-ATR-INSTRUMENT-MISMATCH` (P1): **CLOSED** -- B174 VERIFY_PASS.
