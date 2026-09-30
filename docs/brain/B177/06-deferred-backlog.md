# Deferred Backlog -- Cumulative
# Last updated by: ptt-plan-reviewer (Phase 5)
# Block: B177
# Epics closed this block: DW-B176-TEXTBOX-FIX, DW-B176-SIM, DW-B176-ATR-TEXTBOX-BROKEN
# New open items: DW-B177-CYC

---

## B177 Items

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B176-TEXTBOX-FIX | B177 | ATR% TextBox digit/period input broken in NT8 (search dialog opened on keypress instead of inserting character). B176-FIX applied manual caret injection at lines 3309-3344 of TradeCopierPanel.cs. Validated by B177-1. | P0 | **CLOSED -- B177-1 VERIFY_PASS + Director SIM PASS** |
| DW-B176-SIM | B177 | Director SIM PASS: ATR% TextBox typing (digits + period), Backspace, and ATR pts update all confirmed working after B176-FIX in NT8 environment. Confirmed pre-B177 by Director; cited in architecture plan Section 6. | P1 | **CLOSED -- Director SIM confirmed** |
| DW-B177-CYC | B177 | `OnAtrBoxPreviewKeyDown` (TradeCopierPanel.cs lines 3318-3344) CYC=13 exceeds JS-013 limit of 8 by 5 points. Source comment at lines 3316-3317 states "CYC=8" -- this is incorrect; authoritative JS-013 count is 13. Reduction plan: extract char-injection body (lines 3334-3343) into `private void InjectAtrChar(char c)` helper method. This reduces outer method to CYC<=8 and helper to CYC<=3. Requires ptt-sync.ps1 run post-edit. Source already deployed; Director SIM confirmed working. B177 is validate-only and cannot modify source. | P2 | OPEN -- carry forward to next block |

---

## B176 Items (carried forward)

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B176-ATR-TEXTBOX-BROKEN | B176 | ATR% TextBox non-functional after B175-2 (unconditional e.Handled=true in OnAtrBoxPreviewKeyDown suppressed all keyboard input including Backspace, Delete, and digit keys). B176-1 replaced method body with key-filtered guard. Pipeline output marked closed after B176-1 VERIFY_PASS, but Director SIM found still broken (B176 pipeline output was not the deployed final source). B177-1 re-validates final deployed source (B176-FIX, lines 3309-3344) as the definitive close record. Underlying epic fully closed. | P0 | **CLOSED -- B176-1 VERIFY_PASS (pipeline). B177-1 VERIFY_PASS is the definitive close record for the final deployed source.** |
| DW-B159-K5 | B176 | Stale cross-reference comment in CopyEngine.cs line 3202: "line 5507" corrected to "line 5526" (actual location of HasOpenPosition method). Comment-only, CYC delta = 0. | P3 | **CLOSED -- B176-2 VERIFY_PASS** |

---

## B175 Items (carried forward)

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B175-ATR-FORMULA | B175 | AtrCalc Wilder's ATR fix -- simple average replaced by Wilder's seed+smoothing (2*period warmup). ChartControl overload reads up to 2*period+1 bars via ternary readLen + ReadBarsToArrays helper. | P1 | **CLOSED -- B175-1 VERIFY_PASS** |
| DW-B175-ATR-TEXTBOX-FOCUS | B175 | ATR% TextBox PreviewKeyDown fix -- PreviewKeyDown subscription added in BuildAtrWidgetRows; OnAtrBoxPreviewKeyDown handler (B175-2). Superseded in correctness by B176-1 (original handler was architecturally inverted). | P1 | **CLOSED -- B175-2 VERIFY_PASS** |
| DW-B175-SIM | B175 | SIM GATE: Director verifies PTT ATR matches chart header ATR; TextBox typing works without search dialog. | P1 | **SUPERSEDED by DW-B176-SIM (now CLOSED)** |

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

## Prior Open Items (carried forward from B176 / B175 / B172 / B171 / B170 / B169 / B168 / B167 / B159)

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

**B177 changes summary:**
- No `.cs` files were created, modified, or deleted in B177.
- `src/PropTraderTools/TradeCopierPanel.cs` lines 3309-3344: READ ONLY (validate-only block).
- `src/PropTraderTools/AtrCalc.cs`: Untracked (`??`) -- B175 regression guard intact.
- ptt-sync.ps1: 18 linked, 0 errors (re-run confirmed in B177-1 even though no source changed).
- All 7 scan results PASS (SCAN-01 through SCAN-07). Layer 2 / Layer 3 cross-check: 0 discrepancies.

**Highest-risk open items (updated post-B177):**
- `DW-B170-SIM` (P0): requires Director SIM run to verify stop-drag sync fix from B171.
- `DW-B169-K1` (P0) / `DW-B169-K2` (P0): SIM gates pending; DW-B169-K1 Sim102 blocked by B167-DEFER-02.
- `B167-DEFER-02` (P1): follower ATM brackets never placed after leader fill when fo=NULL. Blocks DW-B169-K1 Sim102.
- `DW-B177-CYC` (P2): NEW -- CYC=13 in OnAtrBoxPreviewKeyDown exceeds JS-013 limit of 8. Extract InjectAtrChar(char c) helper in a future block to reduce outer method to CYC<=8.
