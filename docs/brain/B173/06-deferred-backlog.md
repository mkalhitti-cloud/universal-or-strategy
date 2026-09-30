# Deferred Backlog -- Cumulative
# Last updated by: ptt-plan-reviewer (Phase 5)
# Block: B173

---

## B173 Items

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B173-01 | B173 | SIM GATE: Director must verify AtrWidget displays live ATR data in NinjaTrader after B173 fix. Expected behavior: (1) panel opens with widget showing "-- pts" until first timer tick with bars loaded, (2) after first valid tick: widget transitions to "N pts" computed as `Math.Ceiling(ATR * fraction)` cast to int, where ATR is computed by `AtrCalc.Compute(ChartControl, 14)` reading live bar data. Root cause (AtrSizingEngine instantiated outside NinjaScript pipeline) eliminated by B173. This item supersedes DW-B172-01. Close upon Director SIM confirmation. | P1 | **CLOSED -- Director SIM confirmed live ATR showing** |

---

## B172 Items (carried forward)

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B172-01 | B172 | SIM GATE: Director must verify AtrWidget displays live ATR data in NinjaTrader. Expected: (1) panel opens with "-- pts", (2) after first bar: "N pts" computed as `Math.Ceiling(ATR * fraction)` cast to int, (3) LostFocus on ATR% TextBox clamps [0.10, 5.00] and recalculates. | P1 | **SUPERSEDED BY DW-B173-01** -- B173 fixed the root cause (AtrSizingEngine outside NinjaScript pipeline). |

---

## Prior Open Items (carried forward from B171 / B172)

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B170-SIM | B170 | SIM GATE: Director drags target on leader -- confirm follower stop preserved (not missing) after drag. DW-B170-01 live regression verify. B171 fix deployed (unsuffixed stop name was root cause of stop-drag sync failure). SIM verification required; close upon Director SIM confirmation per B171-orchestrator-prompt.md §SIM GATE. | P0 | OPEN (PENDING SIM GATE) |
| DW-B169-K1 | B169 | SIM GATE: 3-stop simultaneous drag -- 0 missing follower stops | P0 | OPEN (partially verified -- Sim103/104 PASS, Sim102 blocked by B167-DEFER-02) |
| DW-B169-K2 | B169 | SIM GATE: single-stop drag regression | P0 | OPEN |
| DW-B168-K1 | B168 | Superseded by DW-B169-K1/K2 | P0 | OPEN (superseded) |
| B167-DEFER-02 | B167 | fo=NULL: follower ATM brackets never placed after leader fill. Dedicated block required. | P1 | OPEN |
| B167-DEFER-01 | B167 | ECA-1 double-position race. Monitor at SIM. | P2 | OPEN |
| B167-DEFER-03 | B167 | Combined B161-B166 SIM gate pending | P1 | OPEN |
| B167-DEFER-04 | B167 | B167 SIM gate pending | P1 | OPEN |
| DW-B159-K2 | B159 | 14 xUnit tests | P2 | OPEN |
| DW-B159-K3 | B159 | Attempt cap hardcoded at 3 | P2 | OPEN |
| DW-B159-K4 | B159 | intendedBePrice sourcing | P2 | OPEN |
| DW-B159-K5 | B159 | Stale line comment | P3 | OPEN |

---

## Notes

**B173 changes summary:**
- `AtrSizingEngine.cs` deleted entirely (65 lines). Root cause of DW-B172-01.
- `AtrCalc.cs` created (97 lines): `internal static class AtrCalc` with two Compute overloads.
  Pure-array overload is NT8-independent (testable). ChartControl overload reads live bar data
  via `cc.BarsArray[0].Bars.GetHigh/GetLow/GetClose(barsAgo)`. Both overloads: JS-001 safe.
- `TradeCopierPanel.cs`: `_provider` field replaced by `_chartControl ChartControl`; `SetProvider(AtrSizingEngine)`
  replaced by `SetChartControl(ChartControl)`; `OnTimerTick` simplified from CYC=6 to CYC=3;
  `SetAtrProvider` replaced by `SetAtrChartControl` on outer class.
- `TradeCopierAddOn.cs`: WireNewPanel lines 397-398 changed from `new AtrSizingEngine() + SetAtrProvider`
  to `FindVisualChild<ChartControl>(chart) + SetAtrChartControl`.
- `CopyEngineTests.cs`: `B173AtrCalcTests` class appended with 2 [Fact] tests (no [Skip]).
  T-B173-T1-01: guard test (insufficient bars). T-B173-T1-02: known-value ATR(3) = 5.5/3.0.
  Both pass.
- ptt-sync: 18 linked, 0 errors. AtrCalc.cs linked. AtrSizingEngine.cs absent.
- All 7 scans zero at T3 terminal state.

**Highest-risk open items (updated):**
- `DW-B170-SIM` (P0): requires Director SIM run to verify stop-drag sync fix from B171.
- `B167-DEFER-02` (P1): follower ATM brackets never placed after leader fill when `fo = NULL`. Blocks DW-B169-K1 Sim102.
- `DW-B173-01` (P1): **CLOSED** -- Director SIM confirmed live ATR showing post-B173. Root cause fix verified in production.
