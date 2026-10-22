# B216 Deferred Backlog

---

## Block B216: Ask/Bid Pattern-B Conversion + Cancel/CancelAll Buttons
## Date: 2026
## Produced by: ptt-plan-reviewer (Phase 5 Final Review)
## Status: FINAL_PASS issued

---

### Summary

This file is the authoritative deferred-item register for the B216 block. It appends a B216 block
to the deferred-backlog chain originating in B209, updated through B212, B214, and B215.

B216 closed 3 deferred work items:
- DW-B216-01 Part A: null-leader diagnostic log on OnAskClick and OnBidClick (T-A1 + T-B1)
- DW-B216-01 Part B: Pattern B conversion of OnAskAllClick and OnBidAllClick (T-A1 + T-B1)
- DW-B211-01: direction-aware validation concern (CLOSED-RESOLVED -- Pattern B makes direction guard
  unnecessary for the move-target semantic; Ask targets ask price regardless of position direction;
  Bid targets bid price regardless of position direction)

Additionally, B216 delivered new features not from the deferred backlog:
- Cancel + Cancel All button row in the main (non-collapsible) panel area (T-C1)
- PttCancelAll.cs standalone static module (T-C1)

No new deferred work items were introduced by B216.

---

### B216: Items CLOSED by This Pipeline

| ID | Item | Priority | Closure Evidence |
|----|------|----------|-----------------|
| DW-B216-01 Part A | Null-leader diagnostic log in OnAskClick / OnBidClick. OnAskClick/OnBidClick silently returned when `_leaderAccount` was null after TryResolveLeaderAccount(). Added `NinjaTrader.Code.Output.Process("[PTT-MA] Ask: leader null -- check account combo on chart trader", OutputTab1)` before `return;` in each. | P1 | CLOSED by T-A1 (OnAskClick, TradeCopierPanel.cs:1525-1530) and T-B1 (OnBidClick, TradeCopierPanel.cs:1618-1623). VERIFY_PASS on both tickets. |
| DW-B216-01 Part B | OnAskAllClick + OnBidAllClick Pattern B conversion. Both handlers previously required `_leaderAccount` (Pattern A) and silently no-oped when copier was off. Converted to Pattern B: iterate `Account.All`, filter by `HasNonFlatPosition`, log outcome. | P1 | CLOSED by T-A1 (OnAskAllClick, TradeCopierPanel.cs:1539-1559) and T-B1 (OnBidAllClick, TradeCopierPanel.cs:1632-1652). VERIFY_PASS on both tickets. |
| DW-B211-01 | Direction-aware validation (warn/block for direction-inappropriate Ask/Bid). Pattern B conversion resolves the underlying concern: each All-button now acts on accounts with a non-flat position (any direction), applies the Ask or Bid target move semantically. Adding a direction filter was the proposed validation; Pattern B makes that filter unnecessary for the move-target semantic -- the operation is valid for any position direction. | P2 | CLOSED-RESOLVED by T-A1 + T-B1. Confirmed by orchestrator-prompt line 21: "DW-B211-01 is CLOSED-RESOLVED". Architecture plan line 79 updated status to OPEN (predates CLOSED-RESOLVED determination). Final reviewer confirms CLOSED-RESOLVED status. |

---

### B216: Full Carry-Forward Table (Open Items)

All open items from B215/06-deferred-backlog.md carry forward. DW-B216-01 (Part A + Part B) and
DW-B211-01 are now CLOSED. The table below reflects the current open-item state entering B217+.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B215-LA-04 | Fix DwB168_02Tests (4 failures) + B196Tests DrainThenDispatch (2 failures) + FindLeaderStopPrice (2 failures) in gitignored `src/PropTraderTools/Tests/B196Tests.cs`. Add `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` to all 6 DrainThenDispatch/DwB168_02 tests. Fix FindLeaderStopPrice tests path resolution. Manual edit in NT8 project folder copy only. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B215-LA-07 | Fix T1_SubmitDrainedEntry_AtmObjectNull_TemplatePresent_UsesNamedMode in gitignored `src/PropTraderTools/Tests/B195Tests.cs`. Add `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]`. Manual edit in NT8 project folder copy only. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B214-02 | 24 pre-existing test failures. Agent-executable scope (15 failures) was closed by B215-Lane-A. Remaining 9 failures are covered by DW-B215-LA-04 (8) and DW-B215-LA-07 (1). Not caused by B216. Final suite state: Failed=9, Passed=213, Skipped=555, Total=777. | P1 | Manual (NT8 project folder) | OPEN (manual portion only) |
| DW-B214-LB-01 | B74LaneCTests.cs cap assertions (ReturnsTen/ReturnsNegTen) require manual update to expect 50/-50. File is gitignored; agent tools cannot write to it. Must be opened directly in NT8 project folder. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B214-LB-02 | B214Tests.cs B214T2Tests class (3 new [Fact] tests for UI-4/UI-5) must be created manually in NT8 project folder. File is gitignored. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B214-LB-03 | B73Tests.cs FormatGlobalBeBuffer assertions require manual update (gitignored). Both assert positive FormatGlobalBeBuffer output without "t" suffix; must assert "BE All +Xt" format. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B209-01 | Guardian script single-hunk false positive fix. verify_critical_symbols.ps1 reports false-positive GUARDIAN FAIL for DW-B209-02 (_instr2tBtn.Background). Script requires fix to exclude known pre-existing gap. Not introduced by B216. | P2 | B217+ | OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap (_instr2tBtn.Background). Pre-existing cosmetic visual gap. Not introduced by B216. | P2 | B217+ | OPEN |
| DW-B211-02 | Increase/Decrease target nudge buttons. DW-B211-01 is now CLOSED-RESOLVED (Pattern B resolution). DW-B211-02 depends on DW-B211-01 design; with DW-B211-01 resolved via Pattern B, the nudge button feature may now be re-evaluated independently in a future block. | P2 | Future block | OPEN |
| DW-B212-02 | PttMoveTargetAll.cs future role (retain as copier-aware API utility vs deprecate). Pattern B conversion in B216 means OnAskAllClick/OnBidAllClick no longer call PttMoveTargetAll.Execute. Do NOT delete without DW-card authorization. | P2 | B217+ | OPEN |

---

### Full Item Table (Including Closed -- All Blocks Through B216)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B209-01 | Guardian script single-hunk false positive | P2 | B217+ | OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap | P2 | B217+ | OPEN |
| DW-B209-03 | SIM R7 -- B209 OCO + GTC guard | P0 | -- | CLOSED (B212) |
| DW-B209-04 | specs/002-trade-copier-spec.html B209 updates | P1 | -- | **CLOSED (B215-LaneC)** |
| DW-B211-01 | Direction-aware validation | P2 | -- | **CLOSED-RESOLVED (B216)** |
| DW-B211-02 | Target nudge buttons (depends DW-B211-01) | P2 | Future block | OPEN |
| DW-B211-03 | Canonical target identification | P1 | -- | CLOSED (B211) |
| DW-B211-04 | GTC sweep race protection (_maInProgress flag) | P1 | -- | CLOSED (B212, SIM-conditional) |
| DW-B211-05 | Shared vs independent buffers | P2 | -- | CLOSED (B211) |
| DW-B211-SIM-01 | SIM validation B211 + B209 combined round | P0 | -- | CLOSED (B212) |
| DW-B211-SIM-02 | Evaluate DW-B211-04 GTC race empirically during SIM | P1 | -- | CLOSED (B212) |
| DW-B212-01 | Trim(Instrument) leader-skip investigation | P1 | -- | CLOSED-NO-ACTION (B215-LaneB) |
| DW-B212-02 | PttMoveTargetAll.cs future role | P2 | B217+ | OPEN |
| DW-B212-03 | DIFF LIMIT compliance audit for B211+B212 PR | P1 | -- | CLOSED (B214-LaneA) |
| DW-B212-04 | DispatchCopy CYC remediation | -- | -- | CLOSED (B212, T3 Retry 1) |
| DW-B212-CYC-01 | PttMoveTargetOne.Execute CYC=14 | -- | -- | CLOSED (B212, T1 Retry 1) |
| DW-B214-01 | CYC counting methodology inconsistency (TryResolveLeaderAccount) | P2 | -- | CLOSED (B215-LaneB) |
| DW-B214-02 | 24 pre-existing test failures (agent scope) | P1 | Manual | OPEN (manual portion only) |
| DW-B214-LB-01 | B74LaneCTests.cs cap assertions (manual) | P1 | Manual | OPEN |
| DW-B214-LB-02 | B214Tests.cs B214T2Tests class (manual) | P1 | Manual | OPEN |
| DW-B214-LB-03 | B73Tests.cs FormatGlobalBeBuffer (manual) | P1 | Manual | OPEN |
| DW-B215-LA-04 | Fix DwB168_02Tests + B196Tests (gitignored, manual) | P1 | Manual | OPEN |
| DW-B215-LA-07 | Fix B195Tests T1_SubmitDrained... (gitignored, manual) | P1 | Manual | OPEN |
| DW-B216-01 Part A | Null-leader diagnostic log Ask + Bid | P1 | -- | **CLOSED (B216)** |
| DW-B216-01 Part B | Ask-All + Bid-All Pattern B conversion | P1 | -- | **CLOSED (B216)** |

---

### Priority Order for Next Block (B217+)

1. **DW-B215-LA-04 + DW-B215-LA-07** (P1 x2): Manual NT8 project folder test fixes for remaining 9 test failures (gitignored files -- agent cannot write). Resolves DW-B214-02 fully.
2. **DW-B214-LB-01/02/03** (P1 x3): Manual NT8 project folder test updates (gitignored files -- agent cannot write).
3. **DW-B209-01/02** (P2): Guardian false positive fix + BrushTeal cosmetic.
4. **DW-B212-02** (P2): PttMoveTargetAll.cs disposition decision (context: B216 Pattern B conversion means it is no longer called by Ask-All/Bid-All -- re-evaluate its role).
5. **DW-B211-02** (P2): Target nudge buttons re-evaluated now that DW-B211-01 is CLOSED-RESOLVED.

---

*B216 deferred block produced by ptt-plan-reviewer (Phase 5 Final Review).*
*Companion document: docs/brain/B216/05-final-review.md.*
*FINAL_PASS gate: UNBLOCKED.*
