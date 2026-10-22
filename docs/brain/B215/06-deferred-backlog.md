# B215 Deferred Backlog

---

## Block B215 Lane B: DW-B212-01 Close + DW-B214-01 CYC Annotation Fix
## Date: 2026
## Produced by: ptt-plan-reviewer (Phase 5 Final Review)
## Status: FINAL_PASS issued

---

### Summary

This file is the authoritative deferred-item register for the B215 block. It appends a B215 Lane B
block to the deferred-backlog chain originating in B209, updated through B212 and B214.

B215 Lane B closed 2 long-standing deferred work items (DW-B212-01 and DW-B214-01) via comment-only
edits to CopyEngine.cs and TradeCopierPanel.cs. No logic changes were introduced. Both closures are
evidenced in ticket-lb1-completion.md and ticket-lb1-verification.md.

---

### B215 Lane B: Items CLOSED by This Pipeline

| ID | Item | Priority | Closure Evidence |
|----|------|----------|-----------------|
| DW-B212-01 | Trim(Instrument) RelayTrim path -- leader-skip investigation | P1 | CLOSED-NO-ACTION. TrimOneAccount calls acc.CreateOrder (PTT-Trim partial market sell) only. No CancelAllAccountOrders -> no ATM bracket cancel -> no NT8 ATM re-arm risk. Leader-skip guard NOT needed for Trim. 4-line comment added at CopyEngine.cs:5818-5821. Confirmed by ticket-lb1-completion.md (SCAN-6) and ticket-lb1-verification.md (content verification A + SCAN-6). |
| DW-B214-01 | CYC counting methodology inconsistency for TryResolveLeaderAccount | P2 | CLOSED. CYC annotation at TradeCopierPanel.cs:566 corrected from "CYC=6: chart null(1)..." to "CYC=8 (strict McCabe: 8 decision points; Ph4a path-counting=6, both pass JS-066 ceiling)." Both counts pass JS-066 ceiling. JS-002/JS-021 annotations at lines 567-568 preserved verbatim. Confirmed by ticket-lb1-completion.md (D2 result) and ticket-lb1-verification.md (content verification B). |

---

### B215 Lane B: Full Carry-Forward Table (Open Items)

All open items from B214/06-deferred-backlog.md carry forward. DW-B212-01 and DW-B214-01 are now
CLOSED. The table below reflects the current open-item state entering B216+.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B214-02 | 24 pre-existing test failures in pre-B214 test classes (B153, B155, DwB157, DwB168, B188/LaneF, B189, B195, B196). Not caused by B215-LaneB. Zero new failures introduced by comment-only LB1 edits. | P1 | B215-LaneA (in progress) | OPEN |
| DW-B214-LB-01 | B74LaneCTests.cs cap assertions (ReturnsTen/ReturnsNegTen) require manual update to expect 50/-50. File is gitignored; agent tools cannot write to it. Must be opened directly in NT8 project folder. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B214-LB-02 | B214Tests.cs B214T2Tests class (3 new [Fact] tests for UI-4/UI-5) must be created manually in NT8 project folder. File is gitignored. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B214-LB-03 | B73Tests.cs FormatGlobalBeBuffer assertions require manual update (gitignored). Both assert positive FormatGlobalBeBuffer output without "t" suffix; must assert "BE All +Xt" format. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B209-01 | Guardian script single-hunk false positive fix. verify_critical_symbols.ps1 reports false-positive GUARDIAN FAIL for DW-B209-02 (_instr2tBtn.Background). Script requires fix to exclude known pre-existing gap. | P2 | B216+ | OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap (_instr2tBtn.Background). Pre-existing cosmetic visual gap. Not introduced by B215-LaneB. | P2 | B216+ | OPEN |
| DW-B209-04 | specs/002-trade-copier-spec.html B209 block updates. Spec HTML not updated for B209 OCO + GTC guard features. Must be updated before production SIM R8. | P1 | B215-LaneC (in progress) | OPEN |
| DW-B211-01 | Direction-aware validation (warn/block for direction-inappropriate Ask/Bid). V2 design required: add position direction check and optional warning. Required before DW-B211-02. | P2 | Future block | OPEN |
| DW-B211-02 | Increase/Decrease target nudge buttons (depends DW-B211-01). Requires DW-B211-01 direction-handling resolved first. | P2 | Future block (after DW-B211-01) | OPEN |
| DW-B212-02 | PttMoveTargetAll.cs future role (retain as copier-aware API utility vs deprecate). Do NOT delete without DW-card authorization. | P2 | B216+ | OPEN |

---

### Full Item Table (Including Closed -- All Blocks)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B209-01 | Guardian script single-hunk false positive | P2 | B216+ | OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap | P2 | B216+ | OPEN |
| DW-B209-03 | SIM R7 -- B209 OCO + GTC guard | P0 | -- | CLOSED (B212) |
| DW-B209-04 | specs/002-trade-copier-spec.html B209 updates | P1 | B215-LaneC (in progress) | OPEN |
| DW-B211-01 | Direction-aware validation | P2 | Future block | OPEN |
| DW-B211-02 | Target nudge buttons (depends DW-B211-01) | P2 | Future block | OPEN |
| DW-B211-03 | Canonical target identification | P1 | -- | CLOSED (B211) |
| DW-B211-04 | GTC sweep race protection (_maInProgress flag) | P1 | -- | CLOSED (B212, SIM-conditional) |
| DW-B211-05 | Shared vs independent buffers | P2 | -- | CLOSED (B211) |
| DW-B211-SIM-01 | SIM validation B211 + B209 combined round | P0 | -- | CLOSED (B212) |
| DW-B211-SIM-02 | Evaluate DW-B211-04 GTC race empirically during SIM | P1 | -- | CLOSED (B212) |
| DW-B212-01 | Trim(Instrument) leader-skip investigation | P1 | B215-LaneB | **CLOSED-NO-ACTION (B215-LaneB)** |
| DW-B212-02 | PttMoveTargetAll.cs future role | P2 | B216+ | OPEN |
| DW-B212-03 | DIFF LIMIT compliance audit for B211+B212 PR | P1 | -- | CLOSED (B214-LaneA) |
| DW-B212-04 | DispatchCopy CYC remediation | -- | -- | CLOSED (B212, T3 Retry 1) |
| DW-B212-CYC-01 | PttMoveTargetOne.Execute CYC=14 | -- | -- | CLOSED (B212, T1 Retry 1) |
| DW-B214-01 | CYC counting methodology inconsistency (TryResolveLeaderAccount) | P2 | B215-LaneB | **CLOSED (B215-LaneB)** |
| DW-B214-02 | 24 pre-existing test failures | P1 | B215-LaneA (in progress) | OPEN |
| DW-B214-LB-01 | B74LaneCTests.cs cap assertions (manual) | P1 | Manual | OPEN |
| DW-B214-LB-02 | B214Tests.cs B214T2Tests class (manual) | P1 | Manual | OPEN |
| DW-B214-LB-03 | B73Tests.cs FormatGlobalBeBuffer (manual) | P1 | Manual | OPEN |

---

### Priority Order for Next Block (B216+)

1. **DW-B214-02** (P1): 24 pre-existing test class failures -- dedicated remediation block (B215-LaneA in progress).
2. **DW-B214-LB-01/02/03** (P1 x3): Manual NT8 project folder test updates (gitignored files -- agent cannot write).
3. **DW-B209-04** (P1): specs HTML update before SIM R8 (B215-LaneC in progress).
4. **DW-B209-01/02** (P2): Guardian false positive + BrushTeal cosmetic.
5. **DW-B212-02** (P2): PttMoveTargetAll disposition decision.
6. **DW-B211-01/02** (P2): Direction-aware validation and nudge buttons (future feature block).

---

*B215 Lane B deferred block appended by ptt-plan-reviewer (Phase 5 Final Review).*
*Companion document: docs/brain/B215/05-final-review-lane-b.md.*
*FINAL_PASS gate: UNBLOCKED.*

---

## B215 Lane A: Test-Debt Remediation (DW-B214-02 Completion)
## Date: 2026
## Produced by: ptt-plan-reviewer (Phase 5 Final Review)
## Status: FINAL_PASS issued

### Summary

B215 Lane A closed DW-B214-02 (24 pre-existing test failures) for the agent-executable scope.
15 of 24 failures were resolved via tickets TA1–TA6 (VERIFY_PASS on all). The remaining 9
failures are in gitignored test files and require manual Director action (DW-B215-LA-04,
DW-B215-LA-07). No unauthorized deferrals were created.

Items CLOSED by this pipeline:
- DW-B215-LA-01: IsPttQxTarget added to CopyEngine.cs at line 1987 (CLOSED by TA1)
- DW-B214-02: agent-executable scope (15 failures) CLOSED

### B215 Lane A: Items Requiring Manual Director Action (OPEN)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B215-LA-04 | Fix DwB168_02Tests (4 failures) + B196Tests DrainThenDispatch (2 failures) + FindLeaderStopPrice (2 failures) in gitignored `src/PropTraderTools/Tests/B196Tests.cs`. Add `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` to all 6 DrainThenDispatch/DwB168_02 tests. Fix FindLeaderStopPrice_AcceptedStop_ReturnsPrice and FindLeaderStopPrice_WorkingStop_StillReturnsPrice: replace Assembly.Location relative path with `[CallerFilePath]` pattern or `AppDomain.CurrentDomain.BaseDirectory` navigation. Manual edit in NT8 project folder copy. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B215-LA-07 | Fix T1_SubmitDrainedEntry_AtmObjectNull_TemplatePresent_UsesNamedMode in gitignored `src/PropTraderTools/Tests/B195Tests.cs`. Add `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]`. Manual edit in NT8 project folder copy. | P1 | Manual (NT8 project folder) | OPEN |

### Final Suite State After B215 Lane A

```
dotnet test: Failed=9, Passed=207, Skipped=555, Total=771
```

All 9 remaining failures are covered by DW-B215-LA-04 (8) and DW-B215-LA-07 (1).
No unauthorized failures remain.

---

*B215 Lane A deferred block appended by ptt-plan-reviewer (Phase 5 Final Review).*
*Companion document: docs/brain/B215/05-final-review-lane-a.md.*
*FINAL_PASS gate: UNBLOCKED.*

---

## B215 Lane C: DW-B209-04 Spec HTML Update
## Date: 2026
## Produced by: ptt-plan-reviewer (Phase 5 Final Review)
## Status: FINAL_PASS issued

### Summary

B215 Lane C closed DW-B209-04 (spec HTML update for B209 OCO + GTC guard features) via ticket
LC1. LC1 added a new HTML section (`<div class="section" id="section-b209-b214">`) to
`specs/002-trade-copier-spec.html` (lines 44339-44480), documenting all 6 feature groups from
B209 through B214 Lane B. No .cs, .csproj, or test files were modified. Zero regression
introduced. All 7 scans PASS (SCAN-3/4/5 N/A for HTML-only lane).

---

### B215 Lane C: Items CLOSED by This Pipeline

| ID | Item | Priority | Closure Evidence |
|----|------|----------|-----------------|
| DW-B209-04 | specs/002-trade-copier-spec.html B209 block updates. Spec HTML not updated for B209 OCO + GTC guard features. Must be updated before production SIM R8. | P1 | CLOSED by LC1. New HTML section id="section-b209-b214" (lines 44339-44480) documents FG-1 through FG-6: B209 OCO Guard, B209 GTC Guard, B211 Ask/Bid buttons, B212 re-entry suppression guards, B213 multi-chart fix, B214 Lane B UI changes. SCAN-1 through SCAN-7 all PASS. VERIFY_PASS (Layer 2 + Layer 3 confirmed). |

---

### B215 Lane C: Full Carry-Forward Table (Open Items)

All open items from B215 Lane B carry forward. DW-B209-04 is now CLOSED.
The table below reflects the current open-item state entering B216+.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B215-LA-04 | Fix DwB168_02Tests (4 failures) + B196Tests DrainThenDispatch (2 failures) + FindLeaderStopPrice (2 failures) in gitignored src/PropTraderTools/Tests/B196Tests.cs. Add [Fact(Skip = "NT8-runtime...")] to DrainThenDispatch/DwB168_02 tests. Fix FindLeaderStopPrice path resolution. Manual edit in NT8 project folder copy. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B215-LA-07 | Fix T1_SubmitDrainedEntry_AtmObjectNull_TemplatePresent_UsesNamedMode in gitignored src/PropTraderTools/Tests/B195Tests.cs. Add [Fact(Skip = "NT8-runtime...")]. Manual edit in NT8 project folder copy. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B214-LB-01 | B74LaneCTests.cs cap assertions (ReturnsTen/ReturnsNegTen) require manual update to expect 50/-50. File is gitignored; agent tools cannot write to it. Must be opened directly in NT8 project folder. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B214-LB-02 | B214Tests.cs B214T2Tests class (3 new [Fact] tests for UI-4/UI-5) must be created manually in NT8 project folder. File is gitignored. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B214-LB-03 | B73Tests.cs FormatGlobalBeBuffer assertions require manual update (gitignored). Both assert positive FormatGlobalBeBuffer output without "t" suffix; must assert "BE All +Xt" format. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B209-01 | Guardian script single-hunk false positive fix. verify_critical_symbols.ps1 reports false-positive GUARDIAN FAIL for DW-B209-02 (_instr2tBtn.Background). Script requires fix to exclude known pre-existing gap. | P2 | B216+ | OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap (_instr2tBtn.Background). Pre-existing cosmetic visual gap. Not introduced by B215-LaneC. | P2 | B216+ | OPEN |
| DW-B211-01 | Direction-aware validation (warn/block for direction-inappropriate Ask/Bid). V2 design required: add position direction check and optional warning. Required before DW-B211-02. | P2 | Future block | OPEN |
| DW-B211-02 | Increase/Decrease target nudge buttons (depends DW-B211-01). Requires DW-B211-01 direction-handling resolved first. | P2 | Future block (after DW-B211-01) | OPEN |
| DW-B212-02 | PttMoveTargetAll.cs future role (retain as copier-aware API utility vs deprecate). Do NOT delete without DW-card authorization. | P2 | B216+ | OPEN |

---

### Priority Order for Next Block (B216+)

1. **DW-B215-LA-04 + DW-B215-LA-07** (P1 x2): Manual NT8 project folder test fixes for remaining 9 test failures (gitignored files -- agent cannot write).
2. **DW-B214-LB-01/02/03** (P1 x3): Manual NT8 project folder test updates (gitignored files -- agent cannot write).
3. **DW-B209-01/02** (P2): Guardian false positive fix + BrushTeal cosmetic.
4. **DW-B212-02** (P2): PttMoveTargetAll disposition decision.
5. **DW-B211-01/02** (P2): Direction-aware validation and nudge buttons (future feature block).

---

*B215 Lane C deferred block appended by ptt-plan-reviewer (Phase 5 Final Review).*
*Companion document: docs/brain/B215/05-final-review-lane-c.md.*
*FINAL_PASS gate: UNBLOCKED.*
