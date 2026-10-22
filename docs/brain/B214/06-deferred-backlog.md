# B214 Deferred Backlog

## Block: B214 -- B211 + B212 + B213 Repair Batch Validation (Lane A)
## Date: 2026
## Produced by: ptt-plan-reviewer (Phase 5 Final Review)
## Status: FINAL_PASS issued

---

## Summary

This file is the authoritative deferred-item register for the B214 block (B211+B212+B213 Repair
Batch Validation, Lane A). It supersedes the open-item list in `docs/brain/B212/06-deferred-backlog.md`
for all items originating at or before B212. That file remains as historical record; this file
carries all items forward.

---

## Block B214 Lane A: Items CLOSED by This Pipeline

| ID | Item | Closure Evidence |
|----|------|-----------------|
| DW-B212-03 | DIFF LIMIT compliance audit for B211+B212 combined PR | SCAN-7 (ticket-1-verification.md): `deploy-sync.ps1` DIFF GUARD = 3,134 chars < 10,000 limit. PASS. Both engineer (Layer 2) and verifier (Layer 3) independently confirmed. DW-B212-03 declared CLOSED in ticket-1-completion.md and independently verified in ticket-1-verification.md. |

---

## Block B214 Lane A: New Items

| ID | Item | Priority | Target Block | Notes |
|----|------|----------|--------------|-------|
| DW-B214-01 | CYC counting methodology inconsistency for `TryResolveLeaderAccount` | P2 | B215/future | Ph4a counted CYC=6 (project path-counting convention). Ph4b independent count: 8 strict McCabe decision points (if/foreach/is-pattern). CYC=8 still PASSES JS-066 (<=8 ceiling). No code violation. Recommend aligning codebase CYC comment annotations to strict McCabe before next complexity audit to prevent false EXACT-target failures. No code change needed unless strict count is used as the canonical standard. |
| DW-B214-02 | 24 pre-existing test failures in pre-B214 test classes | P1 | Dedicated block (B215 or separate test-debt block) | 24 failures confirmed in: B153IsPttQxTargetTests (6), B155ContractTests (2), DwB15701SlateReadyTests (2), DwB168_02Tests (4), LaneFTests/B188 (3), B189T4SpinnerTests (2), B195Tests (1), B196Tests (4). Not caused by any Lane A hotfix. Same set as prior session. Must be addressed in a dedicated test-debt remediation block before production push. |

---

## Block B214 Lane A: Items OPEN (carry forward to B215+)

All open items from B212/06-deferred-backlog.md are carried forward unchanged, plus new B214 items above.
DW-B212-03 is the sole item closed by this block.

| ID | Item | Priority | Target Block | Notes |
|----|------|----------|--------------|-------|
| DW-B209-01 | Guardian script single-hunk false positive fix | P2 | B215+ | `verify_critical_symbols.ps1` reports false-positive GUARDIAN FAIL for DW-B209-02 (`TradeCopierPanel._instr2tBtn.Background`). Confirmed in SCAN-7: only this known false positive fires. Guardian script requires fix to exclude known pre-existing gaps. Carried from B212. |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap (`_instr2tBtn.Background`) | P2 | B215+ | Pre-existing gap in verify_critical_symbols.ps1 check. Not introduced by B211/B212/B213/B214. Cosmetic visual fix. Carried from B212. |
| DW-B209-04 | specs/002-trade-copier-spec.html B209 block updates | P1 | Before SIM R8 | Spec HTML not updated for B209 OCO + GTC guard features. Must be updated before B209/B211 features ship to production. Carried from B212. |
| DW-B211-01 | Direction-aware validation (warn/block for direction-inappropriate Ask/Bid) | P2 | Future block | Ask/Bid fire unconditionally regardless of position direction. V2 design required: add position direction check and optional warning. Required before DW-B211-02. Carried from B212. |
| DW-B211-02 | Increase/Decrease target nudge buttons (depends DW-B211-01) | P2 | Future block (after DW-B211-01) | Nudge target +N / -N ticks from current price. Requires DW-B211-01 direction-handling resolved first. New PttNudgeTargetOne/All pair needed. Carried from B212. |
| DW-B212-01 | Trim(Instrument) RelayTrim path -- leader-skip investigation | P1 | B215+ | SOURCE CONFIRMED OPEN: `Trim(Instrument)` at CopyEngine.cs:5731 iterates `foreach (var acc in AllAccounts(instrument))` with NO leader guard. Same structure as pre-fix Flatten(Instrument). If TrimOneAccount -> CancelAllAccountOrders triggers NT8 ATM re-arm, same risk applies as pre-fix Flatten. Investigation approach: read TrimOneAccount body; check whether it calls CancelAllAccountOrders or a targeted partial-cancel. Carried from B212. |
| DW-B212-02 | PttMoveTargetAll.cs future role | P2 | B215+ | OnAskAllClick/OnBidAllClick bypass PttMoveTargetAll.Execute (direct _leaderAccount+_followerItems loop confirmed by B214 SCAN-6). PttMoveTargetAll.Execute still exists and calls AllAccounts/CopyEngine. Decide: retain as copier-aware API utility, or deprecate with explicit DW-card authorization. Do NOT delete without DW-card. Carried from B212. |
| DW-B214-01 | CYC counting methodology inconsistency (TryResolveLeaderAccount) | P2 | B215/future | See new items section above. |
| DW-B214-02 | 24 pre-existing test failures in pre-B214 test classes | P1 | Dedicated block | See new items section above. |

---

## Full Item Table (Including Closed)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B209-01 | Guardian script single-hunk false positive | P2 | B215+ | OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap | P2 | B215+ | OPEN |
| DW-B209-03 | SIM R7 -- B209 OCO + GTC guard (combined B211 SIM) | P0 | -- | CLOSED (B212) |
| DW-B209-04 | specs/002-trade-copier-spec.html B209 updates | P1 | Before SIM R8 | OPEN |
| DW-B211-01 | Direction-aware validation | P2 | Future block | OPEN |
| DW-B211-02 | Target nudge buttons (depends DW-B211-01) | P2 | Future block | OPEN |
| DW-B211-03 | Canonical target identification (LAST PTT-* Working Limit exit) | P1 | -- | CLOSED (B211) |
| DW-B211-04 | GTC sweep race protection for PTT-MA-* (_maInProgress flag) | P1 | -- | CLOSED (B212, SIM-conditional) |
| DW-B211-05 | Shared vs independent buffers | P2 | -- | CLOSED (B211) |
| DW-B211-SIM-01 | SIM validation for B211 + B209 combined round | P0 | -- | CLOSED (B212) |
| DW-B211-SIM-02 | Evaluate DW-B211-04 GTC race empirically during SIM | P1 | -- | CLOSED (B212) |
| DW-B212-01 | Trim(Instrument) RelayTrim path -- leader-skip investigation | P1 | B215+ | OPEN |
| DW-B212-02 | PttMoveTargetAll.cs future role | P2 | B215+ | OPEN |
| DW-B212-03 | DIFF LIMIT compliance audit for B211+B212 PR | P1 | -- | **CLOSED (B214 Lane A)** |
| DW-B212-04 | DispatchCopy CYC remediation | -- | -- | CLOSED (B212, T3 Retry 1) |
| DW-B212-CYC-01 | PttMoveTargetOne.Execute CYC=14 | -- | -- | CLOSED (B212, T1 Retry 1) |
| DW-B214-01 | CYC counting methodology inconsistency (TryResolveLeaderAccount) | P2 | B215/future | OPEN |
| DW-B214-02 | 24 pre-existing test failures in pre-B214 test classes | P1 | Dedicated block | OPEN |

---

## Notes for Next Block

### Priority Order (open P1 items)

1. **DW-B214-02** (P1): 24 pre-existing test class failures (B153, B155, DwB157, DwB168, B188/LaneF,
   B189, B195, B196) must be addressed before B214/B211/B212 features can ship to production with
   confidence. A dedicated test-debt remediation block is recommended.

2. **DW-B212-01** (P1): Investigate `Trim(Instrument)` at CopyEngine.cs:5731 for same leader-skip
   need as Flatten(Instrument). Read TrimOneAccount body to determine if CancelAllAccountOrders
   is called (triggers NT8 ATM re-arm risk). If yes: apply same `.Where(acc => leader == null ||
   acc.Name != leader.Name)` fix as HF-08.

3. **DW-B209-04** (P1): Update specs/002-trade-copier-spec.html with B209 OCO + GTC guard features
   before production SIM R8.

### Background context for DW-B212-01 (unchanged from B212)

```
Trim(Instrument) at CopyEngine.cs:5731:
    internal void Trim(Instrument instrument)
    {
        if (!_flags.TrimFlatten) { ... return; }
        foreach (var acc in AllAccounts(instrument))
            TrimOneAccount(acc, instrument);
    }
```
No leader guard. Compare to Flatten(Instrument) which was fixed in B212 (HF-08).
The question is whether `TrimOneAccount(acc, instrument)` calls `CancelAllAccountOrders` or a
targeted partial-cancel that does NOT trigger NT8 ATM re-arm. If TrimOneAccount only cancels
Trim-specific orders (not the full ATM bracket), the risk may not apply.

### Background context for DW-B214-02 (test debt)

The 24 failures are stable across sessions (same 8 class names, same counts). They predate B211/B212/B213
entirely. They are NOT caused by any of the 10 Lane A hotfixes. The most important to address are:
- B153IsPttQxTargetTests (6 failures) -- QX targeting logic
- B196Tests (4 failures) -- unknown (requires investigation)
- DwB168_02Tests (4 failures) -- slate-ready conditions
- LaneFTests (3 failures) -- Lane F eligibility

---

*Deferred backlog produced by ptt-plan-reviewer (Phase 5 Final Review). Companion to 05-final-review-lane-a.md.*
*B214 Lane A: FINAL_PASS issued. This file is required for FINAL_PASS gate; gate is UNBLOCKED.*

---

## Block B214 Lane B: UI Label + Buffer Cap Pipeline
## Date: 2026
## Produced by: ptt-plan-reviewer (Phase 5 Final Review)
## Status: FINAL_PASS issued

---

### B214 Lane B: Spec Requirements -- PIPELINE_COMPLETE

| Req | Description | Ticket | Status |
|-----|-------------|--------|--------|
| UI-1 | Button label casing "ALL" -> "All" (7 callsites in TradeCopierPanel.cs) | T1 | PIPELINE_COMPLETE |
| UI-2 | Zero suppression in FormatBuffer and FormatGlobalBeBuffer | T1 | PIPELINE_COMPLETE |
| UI-3 | "t" tick suffix on all buffer numbers | T1 | PIPELINE_COMPLETE |
| UI-4 | IncrementBuffer ceiling raised from 10 to 50 (PttGlobalBreakEven.cs line 18) | T2 | PIPELINE_COMPLETE |
| UI-5 | DecrementBuffer floor raised from -10 to -50 (PttGlobalBreakEven.cs line 25) | T2 | PIPELINE_COMPLETE |

All 5 spec requirements: PIPELINE_COMPLETE. Both BUILD_PASS and VERIFY_PASS confirmed for T1 and T2.

---

### B214 Lane B: New Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B214-LB-01 | B74LaneCTests.cs cap assertions (ReturnsTen/ReturnsNegTen) require manual update to expect 50/-50. File is gitignored; agent tools cannot write to it. Must be opened directly in NT8 project folder. Method names: GlobalBeBuffer_ReflectionSet_AtCeiling_ReturnsTen and GlobalBeBuffer_ReflectionSet_AtFloor_ReturnsNegTen. | P1 | Manual (NT8 project folder) | OPEN |
| DW-B214-LB-02 | B214Tests.cs B214T2Tests class (3 new [Fact] tests for UI-4/UI-5) must be created manually in NT8 project folder. Tests: T_B214_T2_01_IncrementBuffer_AtNewCeiling_DoesNotExceed50, T_B214_T2_02_DecrementBuffer_AtNewFloor_DoesNotGoBelow50, T_B214_T2_03_IncrementBuffer_From49_Reaches50. File is gitignored (agent cannot write). | P1 | Manual (NT8 project folder) | OPEN |
| DW-B214-LB-03 | B73Tests.cs (gitignored) contains T_BUF_BE_01_FormatGlobalBeBuffer_WithPositiveTicks_ReturnsCorrectLabel and T_LABEL_02_FormatGlobalBeBuffer_WithFiveTicks_ReturnsCorrectLabel. Both assert positive FormatGlobalBeBuffer output without "t" suffix. Must be manually updated in NT8 project folder to assert "BE All +Xt" format. | P1 | Manual (NT8 project folder) | OPEN |

---

### B214 Lane B: Carry-Forward Open Items (unchanged from Lane A)

All open items from B214 Lane A carry forward unchanged into Lane B. No Lane A items were closed by Lane B work.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B214-01 | CYC counting methodology inconsistency (TryResolveLeaderAccount) | P2 | B215/future | OPEN |
| DW-B214-02 | 24 pre-existing test failures in pre-B214 test classes | P1 | Dedicated block (B215+) | OPEN |
| DW-B209-01 | Guardian script single-hunk false positive fix | P2 | B215+ | OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap (_instr2tBtn.Background) | P2 | B215+ | OPEN |
| DW-B209-04 | specs/002-trade-copier-spec.html B209 block updates | P1 | Before SIM R8 | OPEN |
| DW-B211-01 | Direction-aware validation for Ask/Bid | P2 | Future block | OPEN |
| DW-B211-02 | Target nudge buttons (depends DW-B211-01) | P2 | Future block | OPEN |
| DW-B212-01 | Trim(Instrument) RelayTrim path -- leader-skip investigation | P1 | B215+ | OPEN |
| DW-B212-02 | PttMoveTargetAll.cs future role | P2 | B215+ | OPEN |

*B214 Lane B deferred block appended by ptt-plan-reviewer (Phase 5 Final Review).*
*Companion document: docs/brain/B214/05-final-review-lane-b.md.*
*FINAL_PASS gate: UNBLOCKED.*
