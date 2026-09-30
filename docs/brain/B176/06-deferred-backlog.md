# Deferred Backlog -- Cumulative
# Last updated by: ptt-plan-reviewer (Phase 5)
# Block: B176
# Epics closed: DW-B176-ATR-TEXTBOX-BROKEN, DW-B159-K5

---

## B176 Items

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B176-ATR-TEXTBOX-BROKEN | B176 | ATR% TextBox non-functional after B175-2 (unconditional e.Handled = true in OnAtrBoxPreviewKeyDown suppressed all keyboard input including Backspace, Delete, and digit keys). B176-1 replaced method body with key-filtered guard: e.Handled = true only for unmodified alpha/digit keys. CYC: 2 -> 5 (+3). | P0 | **CLOSED -- B176-1 VERIFY_PASS** |
| DW-B159-K5 | B176 | Stale cross-reference comment in CopyEngine.cs line 3202: "line 5507" corrected to "line 5526" (actual location of HasOpenPosition method, verified by grep). Comment-only, CYC delta = 0. | P3 | **CLOSED -- B176-2 VERIFY_PASS** |
| DW-B176-SIM | B176 | SIM GATE: Director verifies ATR% TextBox typing + Backspace work after B176-1; ATR pts unchanged (B175 regression check). Steps: (1) Open MES chart, wait ~2s for ATR timer. (2) Verify ATR pts non-zero. (3) Click ATR% TextBox, type "1.50" -- "1.50" appears. (4) Backspace twice -- "1." in box. (5) Type "25" -- "1.25" in box. (6) Click elsewhere -- normalizes, pts update. (7) Type again -- "5.00". Close upon Director confirmation. Supersedes DW-B175-SIM. | P1 | OPEN -- SIM pending post-B176 |

---

## B175 Items (carried forward)

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B175-ATR-FORMULA | B175 | AtrCalc Wilder's ATR fix -- simple average replaced by Wilder's seed+smoothing (2*period warmup). ChartControl overload reads up to 2*period+1 bars via ternary readLen + ReadBarsToArrays helper. | P1 | **CLOSED -- B175-1 VERIFY_PASS** |
| DW-B175-ATR-TEXTBOX-FOCUS | B175 | ATR% TextBox PreviewKeyDown fix -- PreviewKeyDown subscription added in BuildAtrWidgetRows; OnAtrBoxPreviewKeyDown handler (B175-2). Superseded in correctness by B176-1 (original handler was architecturally inverted). | P1 | **CLOSED -- B175-2 VERIFY_PASS** |
| DW-B175-SIM | B175 | SIM GATE: Director verifies PTT ATR matches chart header ATR; TextBox typing works without search dialog. | P1 | **SUPERSEDED by DW-B176-SIM** |

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

## Prior Open Items (carried forward from B175 / B172 / B171 / B170 / B169 / B168 / B167 / B159)

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

---

## Notes

**B176 changes summary:**
- `TradeCopierPanel.cs`: `OnAtrBoxPreviewKeyDown` body replaced (lines 3309-3331).
  Old: unconditional `e.Handled = true` when `IsKeyboardFocused`.
  New: `e.Handled = true` exclusively inside `(isAlpha || isDigit) && noMod` guard.
  CYC: 2 -> 5 (+3). Comment updated from B175 -> B176. apply_diff used.
- `CopyEngine.cs`: Line 3202 comment token corrected: `line 5507` -> `line 5526`.
  Comment-only. CYC delta = 0. apply_diff used.
- ptt-sync.ps1: 18 linked, 0 errors (run once after B176-2).
- All 14 scan results (7 per ticket) PASS. Layer 2 / Layer 3 cross-check: no discrepancies.
- AtrCalc.cs not touched (B175 Wilder's ATR regression guard intact).
- CopyEngineTests.cs not touched.

**Highest-risk open items (updated post-B176):**
- `DW-B176-SIM` (P1): NEW -- requires Director SIM run to verify ATR% TextBox typing works
  correctly after B176-1 fix, and to confirm B175 ATR formula is still correct (supersedes DW-B175-SIM).
- `DW-B170-SIM` (P0): requires Director SIM run to verify stop-drag sync fix from B171.
- `B167-DEFER-02` (P1): follower ATM brackets never placed after leader fill when fo=NULL.
  Blocks DW-B169-K1 Sim102.
