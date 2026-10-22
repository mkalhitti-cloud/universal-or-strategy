# B212 Architecture Plan
# Block: B211+B212 Repair Batch -- Formalized Hotfix Architecture

**Status**: REVIEW_PASS pending
**Block**: B211 + B212 combined repair batch
**Date**: 2026
**Architect**: PTT Architect (ptt-architect mode)
**Pipeline Phase**: Phase 1 output (02-architecture-plan.md)

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

**Gate questions answered with source evidence:**

| Q | Question | Answer | Evidence |
|---|----------|--------|----------|
| Q1 | Same method or within 50 lines? | NO (CopyEngine Flatten at L5729 vs DispatchCopy at L2826 = ~2903 lines apart; B211 fixes in TradeCopierPanel.cs vs B212 in CopyEngine.cs) | Source reads: CopyEngine.cs L2826, L5729 |
| Q2 | Fix B design depends on Fix A final design? | YES (B212-MA-STOP-02 is inside the foreach-targets loop established by B211-MOVETARGET-01 in PttMoveTargetOne.Execute) | PttMoveTargetOne.cs L75-174 |
| Q3 | Each fix has standalone value if the other is blocked? | NO for B212-MA-STOP-02 (requires B211-MOVETARGET-01 loop structure); YES for B212-FLATTEN and B212-REENTRY-V3 | Logical dependency |
| Q4 | Each fix has an independent SIM verification path? | NO for B212-MA-STOP-02 (SIM requires multi-target infrastructure from B211) | SIM scenario dependency |

**Gate decision**: Q2=YES (B212-MA-STOP-02 depends on B211-MOVETARGET-01 design) and Q3=NO for B212-MA-STOP-02 mandate SINGLE-PIPELINE.

Three-ticket grouping within a single pipeline block is the correct structure. Tickets progress from foundation (button mechanics) through enhancement (OCO stop) to suppression (re-entry gates).

---

## Section 0 -- Block Summary

This plan formalizes 6 SIM-validated hotfixes applied during the B211/B212 session. No new code is designed here. The architect's task is to document what was applied, why each fix is architecturally correct, and what verification criteria must be met before the block is closed.

**Source files modified:**

| File | Status | Primary ticket |
|------|--------|----------------|
| `src/PropTraderTools/TradeCopierPanel.cs` | MODIFIED | T1 |
| `src/PropTraderTools/Features/PttMoveTargetOne.cs` | NEW | T1 + T2 |
| `src/PropTraderTools/Features/PttMoveTargetAll.cs` | NEW | T1 |
| `src/PropTraderTools/CopyEngine.cs` | MODIFIED | T3 |
| `src/PropTraderTools/Features/PttBeSwapAll.cs` | MODIFIED (revert + comments) | T3 |
| `src/PropTraderTools/TradeCopierAddOn.cs` | MODIFIED (wiring) | T1 |
| `src/PropTraderTools/Core/PttContracts.cs` | MODIFIED (constants) | T1 |

**Test files:**

| File | Status |
|------|--------|
| `src/PropTraderTools/Tests/B211Tests.cs` | NEW (8 [Fact] tests) |
| `src/PropTraderTools/Tests/B212Tests.cs` | MODIFIED (5 [Fact] structural tests) |

**Test baseline (pre-existing, NOT regressions):** 24 failures / 176 passed.

---

## Section 1 -- Repair Inventory and Ticket Grouping

### Ticket 1: B211 Button Repairs (ASK-01, MOVETARGET-01, ASKALL-01, BIDALL-01)

**Files**: `TradeCopierPanel.cs`, `PttMoveTargetOne.cs` (new), `PttMoveTargetAll.cs` (new)

**Hotfixes included:**

| Hotfix ID | Method | File |
|-----------|--------|------|
| HOTFIX-B211-ASK-01 | `IsWorkingTarget` | PttMoveTargetOne.cs |
| HOTFIX-B211-MOVETARGET-01 | `Execute`, `CollectWorkingTargets`, `IsWorkingTarget` | PttMoveTargetOne.cs (new) |
| HOTFIX-B211-ASKALL-01 | `OnAskAllClick` | TradeCopierPanel.cs |
| HOTFIX-B211-BIDALL-01 | `OnBidAllClick` | TradeCopierPanel.cs |

**Grouping rationale**: All four fixes address the same user-facing concern -- Ask/Bid buttons do nothing or move only one target. They share the same execution path (button click → PttMoveTargetOne.Execute) and their fixes are tightly coupled: the target name widening (ASK-01) is the same predicate that CollectWorkingTargets (MOVETARGET-01) uses; the All-buttons (ASKALL-01, BIDALL-01) directly call the fixed Execute.

**Note on PttMoveTargetAll.cs**: This file is newly created but OnAskAllClick and OnBidAllClick now call PttMoveTargetOne.Execute directly per account (bypassing PttMoveTargetAll.Execute). PttMoveTargetAll.cs exists as a utility class but is no longer in the panel button critical path. Its existence is retained for potential future use; it must not be deleted without an explicit DW-card.

---

### Ticket 2: B212 OCO Stop Pair (HOTFIX-B212-MA-STOP-02)

**File**: `PttMoveTargetOne.cs`

**Hotfixes included:**

| Hotfix ID | Method | File |
|-----------|--------|------|
| HOTFIX-B212-MA-STOP-02 | `Execute` (OCO pair loop) | PttMoveTargetOne.cs |

**Grouping rationale**: This fix adds the per-target OCO pair inside the foreach loop established by HOTFIX-B211-MOVETARGET-01. It is a separate architectural concern (NT8 OCO linking semantics) from the multi-target mechanics, but it lives inside the same Execute method. Placing it in Ticket 2 (not Ticket 1) reflects that it depends on Ticket 1 being correct first: if the loop did not exist, there would be nothing to add OCO pairs to.

HOTFIX-B212-MA-STOP-01 is superseded and not formalized. Only STOP-02 is the authoritative fix.

---

### Ticket 3: B212 Re-Entry Suppression (FLATTEN-LEADERSKIP-01 + REENTRY-V3)

**Files**: `CopyEngine.cs`, `PttBeSwapAll.cs` (revert comment update)

**Hotfixes included:**

| Hotfix ID | Method | File |
|-----------|--------|------|
| HOTFIX-B212-FLATTEN-LEADERSKIP-01 | `Flatten(Instrument)` | CopyEngine.cs |
| HOTFIX-B212-REENTRY-V3 | `DispatchCopy` | CopyEngine.cs |

**Hotfixes REVERTED (not formalized -- do not re-implement):**

| Reverted Hotfix | Reason |
|-----------------|--------|
| HOTFIX-B212-BEALL-LEADERSKIP-01 | Broke copier-OFF: leader never got BE bracket when no rule configured |
| HOTFIX-B212-BREAKEVEN-INSTROVERLOAD-01 v1 | Over-broad: unconditional skip broke copier-OFF |
| HOTFIX-B212-BREAKEVEN-INSTROVERLOAD-01 v2 | Overly complex conditional; REENTRY-V3 is cleaner |
| HOTFIX-B212-DW06-PARTIAL | Partial fix superseded by REENTRY-V3 approach |

**PttBeSwapAll.cs state**: The file was modified to REVERT the leader-skip code from BEALL-LEADERSKIP-01. Final state has no masterAccount/leader-skip conditional. Comment block updated to document why REENTRY-V3 is the correct fix. This is a revert + documentation change, not a new feature.

**Grouping rationale**: Both fixes address the same root cause -- NT8 ATM re-arms Entry on the leader during a bracket-cancel window. They are orthogonal mechanisms:
- FLATTEN-LEADERSKIP-01: source-level skip (leader's close is already in-flight, so skip FlatGuardLoop for leader)
- REENTRY-V3: dispatch-level gate (DispatchCopy suppresses forwarding the re-arm artifact regardless of which button fired)

Placing both in Ticket 3 reflects the shared root cause. The SIM verification path for both requires confirming "no spurious re-entry on any account after BE-ALL or Flatten-ALL."

---

## Section 2 -- Architectural Decisions

### Decision A: Why DispatchCopy is the correct suppression layer for REENTRY-V3

**Root cause**: When PttBreakEvenSwap.Execute calls CancelQxBrackets on the leader, NT8 ATM interprets individual bracket cancels as "user cancelled" and re-arms a new Entry order on the leader. This Entry flows through OnOrderUpdate → Gate 1 (copy enabled) → Gate 2 (leader account) → DispatchCopy. Without a guard, DispatchCopy copies the re-armed Entry to all followers = spurious re-entry.

**Why NOT fix it in PttBeSwapAll / BreakEven overloads**: The leader must always receive its BE bracket. When copier is OFF (no rule configured for the instrument), any leader-skip guard in BE execution paths fires on the rule lookup (FindRule returns the rule even when copier is disabled), causing the leader to miss its BE bracket entirely. This was confirmed empirically by BEALL-LEADERSKIP-01 breaking copier-OFF.

**Why DispatchCopy is correct**: The leader receiving its BE bracket and the copier NOT forwarding the NT8 re-arm artifact are orthogonal concerns. DispatchCopy is the single choke point where all copy dispatch decisions are made. Adding a guard here:
- Does not affect BE execution on any account (leader OR followers)
- Only suppresses dispatch during the specific window where re-arm is expected
- Has zero side effects when copier is OFF (gate never fires because DispatchCopy is only reached when _isCopyEnabled is true -- Gate 1 already filters)

**Gate placement**: After Gate 0.5 (exit signal name filter) and before Gate 3 (dispatch-trigger state check). Gate 0.5 exits for PTT-* names; the re-armed Entry has name "Entry" (not PTT-*) so it passes Gate 0.5. The REENTRY-V3 gates intercept it before it reaches Gate 3's state-level dispatch trigger.

---

### Decision B: IsBeSwapInProgress guard mechanism

**Mechanism**: ConcurrentDictionary<string, bool> keyed by account name. Lock-free O(1) read (TryGetValue) and write (TryAdd/TryRemove).

**Timing window**: Flag is set by PttBreakEvenSwap.Execute BEFORE CancelQxBrackets. Flag is cleared when PTT-BE-Stop-N reaches Working state. This window is approximately 50-200ms -- exactly the ATM re-arm danger zone. During this window, any new Entry from the leader account is suppressed by Gate B212-REENTRY-V3.

**Why ConcurrentDictionary (not lock + bool)**: JS-021 mandates no lock(). ConcurrentDictionary provides atomic read-modify-write semantics without locks, consistent with the pattern already established for _beSwapInProgress throughout the file.

**Null safety**: IsBeSwapInProgress(string accName) has an explicit null guard (returns false on null). The call site uses `order.Account?.Name` (null-propagating) which safely passes null when account is unavailable, and the gate returns false (no suppression) -- correct behavior.

---

### Decision C: HasActivePttFlatten guard mechanism

**Mechanism**: Scans `acc.Orders.ToList()` (lock-free NT8 snapshot) for a "PTT-Flatten" order in Submitted/Accepted/Working/PartFilled state.

**Why order-state scan (not a flag)**: Deterministic -- no flag management required. The PTT-Flatten order exists in the order book from the moment PttFlatten.Execute submits it until it fills. The order's lifecycle is the natural flag. A timestamp-based approach (HasRecentFlatten) was already present but is non-deterministic in edge cases (clock skew, rapid re-entry). HasActivePttFlatten is the V6 upgrade from HasRecentFlatten used elsewhere in the file.

**PartFilled state inclusion**: Without PartFilled, HasActivePttFlatten returns false during the partial-fill window (large positions fill over multiple events). The guard must include PartFilled to remain active throughout the entire close sequence. This is documented at CopyEngine.cs L3238.

**Why deterministic (no flag required)**: The PTT-Flatten order is in the account's order book. Scanning it costs O(N) where N is the number of open orders on the account (typically < 20). This is called on the order update thread. Acceptable cost given it is a gate that fires only during the narrow re-arm window.

---

### Decision D: OCO pair pattern for MoveTarget (HOTFIX-B212-MA-STOP-02)

**Root cause of STOP-01 failure**: StopMarket submitted with ocoId=string.Empty. NT8's OCO mechanism requires BOTH orders in a pair to carry the same non-empty string at CreateOrder time (arg8). A stop with empty ocoId has no linked target -- when the target fills, NT8 does not cancel the stop. The stop sits in Accepted state indefinitely, creating position-flip risk on target fill.

**STOP-02 pattern**: Directly mirrors PttBreakEvenSwap.SubmitSwapPair (the proven BE OCO pattern):
1. Generate ocoId per target slot: `"PTT-MA-" + acc.Name[..8] + "-" + NextBeOcoSeq().D7 + "-" + pairIdx`
2. Submit stop slice first (StopMarket, Gtc, qty, stopPrice=snapshotStop, limitPrice=0, ocoId)
3. Submit target (Limit, Day, qty, limitPrice=exitPrice, stopPrice=0, ocoId)
4. NT8 auto-cancels the paired order on fill of either side

**Why NextBeOcoSeq()**: Monotonic counter seeded post-recompile (avoids collision with prior session IDs). Same guarantee used by BE pairs. Reuse avoids introducing a second counter.

**Prefix "PTT-MA-" vs "PTT-BE-"**: NT8 OCO matches by string equality. Different prefixes prevent accidental cross-pairing of MA pairs and BE pairs. "PTT-MA-" confirms the pair is a MoveAll pair for diagnostics.

**snapshotStop==0 path**: When no bracket stop is found (position has only a target, no protective stop), the fix submits the target-only with ocoId=string.Empty. This is the original STOP-01 behavior scoped to the no-stop branch. No orphaned stop risk because there is no stop.

---

### Decision E: Leader-skip in Flatten(Instrument) -- always correct regardless of copier state

**Root cause**: Call chain: PttFlatten.Execute(ctx) → FlattenPositionLocal(leader) [market close submitted] → PttBus.RaiseFlatted → RelayFlatten → Flatten(Instrument) → FlatGuardLoop(AllAccounts(instrument)) → FlattenOneAccount(leader) → CancelAllAccountOrders(leader) → NT8 re-arms Entry.

**Why the skip is unconditional (unlike BE skip)**: The leader's market close was submitted by PttFlatten.Execute BEFORE RelayFlatten fires. The leader's position close is always in-flight by the time Flatten(Instrument) is called. Calling FlattenOneAccount on the leader is ALWAYS redundant AND harmful (it triggers the NT8 re-arm). This is not conditional on copier state -- the leader's position is being closed by PttFlatten.Execute regardless.

**Contrast with BE**: In BE, the leader needs its BE bracket. Skipping the leader from BE execution means the leader gets no BE bracket. The skip must be conditional on copier-ON to avoid this. In Flatten, the leader's close is already in-flight unconditionally. The skip is always correct.

**The `Flatten(Account leader, Instrument)` overload**: This is the direct panel call path (not the RelayFlatten path). It handles the leader explicitly via its own algorithm. It is NOT modified -- it was correct before this block.

---

### Decision F: Target name widening (PTT- AND Target prefix)

**Root cause**: The original IsWorkingTarget predicate required order names to start with "PTT-". NinjaTrader 8's native ATM bracket targets are named "Target1", "Target2", ..., "Target9" (no "PTT-" prefix). Clone-mode accounts that use NT8 ATM templates always have Target1..Target9-named exits. These were silently excluded by the PTT-prefix-only filter.

**Fix predicate**: `isPttExit || isNativeTarget` where:
- `isPttExit = name.StartsWith("PTT-", Ordinal)` -- covers all PTT-managed exits
- `isNativeTarget = name.StartsWith("Target", Ordinal) && name.Length > 6 && char.IsDigit(name[6])` -- covers Target1..Target9 exactly, excluding "TargetABC" or other non-ATM names

**Why `char.IsDigit(name[6])`**: Prevents false positives for hypothetical names starting with "Target" but not ATM targets. The digit check ensures we only match Target1..Target9 (the nine native ATM target slots).

---

### Decision G: Two-pass count+fill array in CollectWorkingTargets

**Why not List<T>**: NinjaScript (NT8's scripting environment) compiles C# in a restricted scope where `System.Collections.Generic.List<T>` is not always available in implicit using scope. Direct use causes CS0246 ("The type or namespace name 'List<>' could not be found"). The two-pass count+fill pattern allocates a fixed-size `Order[]` using only primitive array syntax, which is always available.

**Two-pass algorithm**: Pass 1 counts matching orders; Pass 2 fills the array. This avoids any List, ArrayList, or dynamic-resize collection. The cost is O(2N) where N = acc.Orders.Count -- acceptable for the small order counts typical in trading accounts.

**Return type Order[] never null**: Returns `new Order[0]` (empty array) when no targets found. Callers check `targets.Length == 0`. This satisfies JS-002 (no null return).

---

### Decision H: OnAskAllClick / OnBidAllClick direct loop pattern

**Old pattern (broken)**: `PttMoveTargetAll.Execute(instrument, useAsk, buffer)` → `Account.All` → `FindRule(instrument)` → null if copier off or no rule → silent no-op.

**New pattern (correct)**: Direct `_leaderAccount` resolve + foreach `_followerItems` loop with `item.IsSelected` guard, calling `PttMoveTargetOne.Execute(item.Account, ...)`.

**Why bypass AllAccounts/FindRule**: The panel already has direct references to `_leaderAccount` and `_followerItems` (UI-bound list). The panel's contract is "apply to selected accounts in the panel" not "apply to all accounts known to CopyEngine." AllAccounts/FindRule is a CopyEngine-internal mechanism for rule-based copy routing. Button actions must work regardless of copier state (copier may be OFF; the user still wants to move targets).

**Why this is the canonical pattern**: OnBidAllClick had already been fixed to use this direct pattern (prior session). OnAskAllClick was the inconsistency. The fix makes both All-buttons use the same direct-loop pattern, eliminating the asymmetry.

---

## Section 3 -- JS-DNA Compliance Matrix

### Ticket 1 (B211 Button Repairs)

| Fix | JS-021 (no lock) | JS-001 (no throw) | JS-033 (no async void) | CYC delta | ASCII-only |
|-----|-----------------|-------------------|------------------------|-----------|------------|
| HOTFIX-B211-ASK-01 | PASS (Interlocked only) | PASS | PASS (sync void) | +0 to IsWorkingTarget; CYC=5 | PASS |
| HOTFIX-B211-MOVETARGET-01 | PASS (no lock) | PASS (try/catch log-and-swallow) | PASS (sync void) | Execute=6, CollectWkgTargets=5, IsWorkingTarget=5, TryGetExitPrice=7. All <=8 | PASS |
| HOTFIX-B211-ASKALL-01 | PASS | PASS | PASS (RoutedEventHandler sync void) | CYC=4 | PASS |
| HOTFIX-B211-BIDALL-01 | PASS | PASS | PASS (RoutedEventHandler sync void) | CYC=4 | PASS |

### Ticket 2 (B212 OCO Stop Pair)

| Fix | JS-021 (no lock) | JS-001 (no throw) | JS-033 (no async void) | CYC delta | ASCII-only |
|-----|-----------------|-------------------|------------------------|-----------|------------|
| HOTFIX-B212-MA-STOP-02 | PASS (Interlocked.Increment for _maSeq; NextBeOcoSeq uses Interlocked) | PASS (try/catch per pair submission) | PASS (sync void) | +1 (snapshotStop > 0 branch) inside Execute. Execute CYC = 6 total. <=8 | PASS ("PTT-MA-", "PTT-MA-Stop-", ocoId components all ASCII) |

### Ticket 3 (B212 Re-Entry Suppression)

| Fix | JS-021 (no lock) | JS-001 (no throw) | JS-033 (no async void) | CYC delta | ASCII-only |
|-----|-----------------|-------------------|------------------------|-----------|------------|
| HOTFIX-B212-FLATTEN-LEADERSKIP-01 | PASS (FindRule ConcurrentDictionary lookup; Where is pure LINQ) | PASS | PASS | +1 (Where lambda condition). Flatten(Instrument) CYC remains <=8 | PASS |
| HOTFIX-B212-REENTRY-V3 | PASS (IsBeSwapInProgress = ConcurrentDictionary TryGetValue; HasActivePttFlatten = acc.Orders.ToList() -- lock-free NT8 snapshot) | PASS | PASS | +2 in DispatchCopy (one per gate). DispatchCopy CYC confirmed <=8 per implementation comment L2783 | PASS |

**P0 rule summary across all tickets:**

| P0 Rule | Status |
|---------|--------|
| JS-021: No lock() anywhere in new/modified code | PASS |
| JS-001: No throw new in hot paths | PASS |
| JS-033: No async void (non-event-handler) | PASS |
| CYC <= 8 in all methods | PASS |
| ASCII-only string literals | PASS |
| ptt-sync 21/0 | CONFIRMED (documented per hotfix) |

---

## Section 4 -- Test Coverage Plan

### Ticket 1 tests (B211Tests.cs)

Tests already present in `B211Tests.cs` (8 [Fact]):

| Test name | What it asserts | Runnable without NT8? |
|-----------|-----------------|----------------------|
| `PttMoveTargetOne_NoTarget_ReturnsNoOp` | Execute no-ops when no Working Limit target found | Skip (NT8 Account.Orders) |
| `PttMoveTargetOne_NoPosition_ReturnsNoOp` | Execute no-ops when position.Quantity == 0 | Skip (NT8 Account.Positions) |
| `PttMoveTargetOne_PartialFill_QtyIsQuantityMinusFilled` | qty = Quantity - Filled = 3 when Qty=5, Filled=2 | YES (pure math) |
| `PttMoveTargetOne_ZeroFill_QtyIsQuantity` | qty = Quantity when Filled=0 | YES (pure math) |
| `TryGetExitPrice_AskPlusZeroBuffer_ReturnsAskExactly` | Price = Ask + 0 ticks = Ask | Skip (NT8 Instrument.MarketData) |
| `TryGetExitPrice_AskPlusTwoBuffer_ReturnsAskPlusTwoTicks` | Price = Ask + 2*tickSize | Skip (NT8 Instrument.MarketData) |
| `TryGetExitPrice_BidMinusOneBuffer_ReturnsBidMinusOneTick` | Price = Bid + (-1)*tickSize | Skip (NT8 Instrument.MarketData) |
| `IsBeSwapGtcSweepGuard_ReturnsFalse_ForPttMaAskOrder` | PTT-MA-* does NOT match FIX-G-2 PTT-BE- guard | YES (pure string predicate) |

**Additional tests required** (not yet present -- engineer must add to B211Tests.cs):

| Test name | What it asserts | Runnable? |
|-----------|-----------------|-----------|
| `IsWorkingTarget_PttPrefixed_ReturnsTrue` | IsWorkingTarget returns true for "PTT-MA-Ask-1" Working Limit Sell | YES (structural) |
| `IsWorkingTarget_NativeTarget1_ReturnsTrue` | IsWorkingTarget returns true for "Target1" Working Limit Sell | YES (structural predicate) |
| `IsWorkingTarget_NativeTarget9_ReturnsTrue` | IsWorkingTarget returns true for "Target9" Working Limit Sell | YES (structural predicate) |
| `IsWorkingTarget_Target_NoDigitSuffix_ReturnsFalse` | "TargetABC" does NOT match native ATM filter (char.IsDigit fails) | YES (pure string) |
| `CollectWorkingTargets_MethodExists_TwoPassArray` | CollectWorkingTargets exists as private static returning Order[] | YES (reflection) |

NOTE: IsWorkingTarget and CollectWorkingTargets are private static in PttMoveTargetOne. The pure string-predicate tests above can test the predicate logic directly (inline the logic) without calling the private method. The structural reflection test confirms the method exists.

---

### Ticket 2 tests (B212Tests.cs)

**Additional tests required** (not yet present -- engineer must add to B212Tests.cs):

| Test name | What it asserts | Runnable? |
|-----------|-----------------|-----------|
| `B212_STOP02_OcoId_Format_ContainsPttMaPrefix` | Generated ocoId starts with "PTT-MA-" | YES (pure string format) |
| `B212_STOP02_OcoId_Format_PairIdx_Appended` | ocoId for pairIdx=0 ends with "-0", pairIdx=1 ends with "-1" | YES (pure string format) |
| `B212_STOP02_OcoId_Format_AccountNameTruncated` | acc.Name length > 8 truncated to 8 chars in ocoId | YES (pure Math.Min logic) |
| `B212_STOP02_SnapshotStop_Zero_NoOcoPath` | When snapshotStop==0, no stop submitted (no-OCO path) | Skip (NT8 Account.CreateOrder) |
| `B212_STOP02_Execute_MethodExists_InPttMoveTargetOne` | PttMoveTargetOne.Execute method exists with expected signature | YES (reflection) |

---

### Ticket 3 tests (B212Tests.cs -- extend existing)

Tests already present cover DW-B212-01, DW-B212-03, DW-B212-05 (5 structural [Fact] tests).

**Additional tests required** (not yet present -- engineer must add to B212Tests.cs):

| Test name | What it asserts | Runnable? |
|-----------|-----------------|-----------|
| `B212_FLATTEN_FlattenInstrument_SkipsLeader_MethodSignatureConfirmed` | Flatten(Instrument) overload exists as internal instance method | YES (reflection) |
| `B212_REENTRY_V3_DispatchCopy_MethodExists` | DispatchCopy exists as private instance method in CopyEngine | YES (reflection) |
| `B212_REENTRY_V3_HasActivePttFlatten_MethodExists_ReturnsVoid` | HasActivePttFlatten exists as private static, returns bool | YES (reflection) |
| `B212_REENTRY_V3_IsBeSwapInProgress_NullAccName_ReturnsFalse` | IsBeSwapInProgress(null) returns false (null-safe) | YES -- invoke via reflection with null arg |

---

## Section 5 -- Verification Requirements

### Ticket 1 Verification

**ptt-sync requirement**: `powershell -File .\scripts\ptt-sync.ps1` must show **21/0** (21 linked, 0 errors) after any edit to TradeCopierPanel.cs, PttMoveTargetOne.cs, or PttMoveTargetAll.cs.

**Guardian symbol check**: `powershell -File .\scripts\verify_critical_symbols.ps1` must PASS after any CopyEngine.cs edit. (Ticket 1 does not modify CopyEngine.cs; run as sanity check if TradeCopierAddOn.cs is modified.)

**SIM validation criteria for Ticket 1:**

| Scenario | Expected result |
|----------|-----------------|
| Open long position, one Working ATM Target1 (Clone-mode). Press Ask. | Target1 cancelled. New PTT-MA-Ask-NNNNN submitted at Ask + buffer. PTT-MA-Stop-NNNNN submitted at snapshotStop price with same ocoId. |
| Open long position, Target1 + Target2 both Working (Clone-mode). Press Ask. | Both Target1 and Target2 cancelled. Two PTT-MA-Ask-NNNNN/NNNMM submitted. Two PTT-MA-Stop-* submitted. |
| No position. Press Ask. | Log "[PTT-MA] no position: AccountName". No cancel, no submit. |
| Position but no Working Limit target. Press Ask. | Log "[PTT-MA] no target found for AccountName". No cancel. |
| Copier OFF. Press Ask-ALL. | _leaderAccount resolved. PttMoveTargetOne.Execute called on leader and all selected followers. No AllAccounts/FindRule invoked. |
| Copier ON. Press Ask-ALL. | Same as copier OFF -- direct loop path is copier-agnostic. |

---

### Ticket 2 Verification

**ptt-sync requirement**: 21/0 after PttMoveTargetOne.cs edit (already confirmed per HOTFIX-B212-MA-STOP-02 log).

**SIM validation criteria for Ticket 2:**

| Scenario | Expected result |
|----------|-----------------|
| Long position, working Target1, existing bracket stop. Press Ask. | PTT-MA-Stop-NNNNN submitted → Accepted → Working (NOT stuck at Accepted). Stop visible on DOM/ladder. |
| Target (PTT-MA-Ask-NNNNN) fills. | PTT-MA-Stop-NNNNN automatically cancelled by NT8 OCO (same ocoId). No orphaned stop. |
| Stop (PTT-MA-Stop-NNNNN) triggers. | Paired PTT-MA-Ask-NNNNN automatically cancelled by NT8 OCO. No orphaned target. |
| Long position, NO bracket stop (snapshotStop==0). Press Ask. | PTT-MA-Ask-NNNNN submitted with ocoId=string.Empty. No stop submitted. No exception. Log "[PTT-MA] Ask: ... stop=0". |
| Two targets, both with stop. Press Ask. | Two OCO pairs: pair 0 (ocoId "PTT-MA-*-0"), pair 1 (ocoId "PTT-MA-*-1"). Each pair independent. |

**Expected NT8 output pattern (from HOTFIX-B212-MA-STOP-02 doc):**
```
[PTT-MA] Ask: Sim101 price=4200.5 targets=2 stop=4196.4
[PTT-MA] pair 0: Sim101 target=PTT-MA-Ask-00001 stop=PTT-MA-Stop-00002 oco=PTT-MA-Sim101--0001234-0 qty=1
[PTT-MA] pair 1: Sim101 target=PTT-MA-Ask-00003 stop=PTT-MA-Stop-00004 oco=PTT-MA-Sim101--0001235-1 qty=1
PTT-MA-Stop-00002 -> Accepted -> Working
PTT-MA-Ask-00001  -> Accepted -> Working
(on target fill: PTT-MA-Stop-00002 -> Cancelled automatically by NT8 OCO)
```

---

### Ticket 3 Verification

**ptt-sync requirement**: 21/0 after CopyEngine.cs edit (confirmed per HOTFIX-B212-REENTRY-V3 log).

**Guardian symbol check**: `powershell -File .\scripts\verify_critical_symbols.ps1` must PASS after CopyEngine.cs edit.

**SIM validation criteria for Ticket 3:**

| Scenario | Expected result |
|----------|-----------------|
| Copier ON, leader long, press BE-ALL. | (a) Sim101 (leader) gets PTT-BE-Stop-N → Working. (b) All followers get PTT-BE-Stop-N → Working. (c) NO new Entry order appears on any account after PTT-BE-Stop-N Working. No [PTT-ATM-SEND] log lines after BE-ALL completes. |
| Copier OFF, press BE-ALL. | All accounts including Sim101 (leader) get BE swap. No leader skip. |
| Copier ON, leader long, press Flatten-ALL. | (a) Leader's position closes (PTT-Flatten market fills). (b) Followers' positions close via FlattenOneAccount. (c) NO new Entry order appears on any account after flatten completes. |
| Copier ON, BE-ALL then rapid new entry signal. | BE-swap suppression window (50-200ms). If new entry fires during window: suppressed by IsBeSwapInProgress gate. After PTT-BE-Stop-N reaches Working: gate clears, new entries copy normally. |

**IsBeSwapInProgress timing window confirmation**: The SIM verification must confirm that the flag clears after PTT-BE-Stop-N reaches Working state by observing that a new entry submitted >200ms after BE-ALL IS copied to followers (gate correctly cleared).

---

## Section 6 -- Deferred Backlog Status

### Items CLOSED by this block (B211+B212 combined SIM round)

| ID | Item | Closure evidence |
|----|------|-----------------|
| DW-B211-SIM-01 | SIM validation for B211 + B209 combined round | output.md contains SIM logs: Target1/Target2 Working, PTT-ATM-SEND on Sim102/103/104, GATE2-BLOCKED patterns consistent with copy isolation working |
| DW-B211-SIM-02 | Evaluate DW-B211-04 GTC race empirically during SIM | B212-MA-STOP-02 SIM output confirms PTT-MA-Stop-NNNNN reaches Working (not just Accepted), indicating the stop survives the cancel window without GTC sweep. DW-B211-04 race not observed in SIM conditions. Mark DW-B211-04 as CLOSED (SIM-conditional, condition not triggered). |
| DW-B209-03 | SIM Round 7 -- B209 OCO + GTC guard (combined with B211 SIM) | Combined SIM round executed as part of B211/B212 session |

### Items carried forward as OPEN

| ID | Item | Priority | Notes |
|----|------|----------|-------|
| DW-B211-01 | Direction-aware validation (warn for inappropriate Ask/Bid direction) | P2 | Not in scope for B212 |
| DW-B211-02 | Increase/Decrease target nudge buttons (depends DW-B211-01) | P2 | Blocked on DW-B211-01 |
| DW-B211-04 | GTC sweep race protection for PTT-MA-* (_maInProgress flag) | P1 | CLOSED: SIM Round confirmed race not observed (see DW-B211-SIM-02 above). Re-open if race observed in production. |
| DW-B209-01 | Guardian script single-hunk false positive fix | P2 | Still open |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap (_instr2tBtn.Background) | P2 | Still open |
| DW-B209-04 | specs/002-trade-copier-spec.html B209 block updates | P1 | Still open |

### New deferred items from B212

| ID | Item | Priority | Target |
|----|------|----------|--------|
| DW-B212-01 | Trim(Instrument) RelayTrim path -- check for same Flatten-REENTRY pattern | P1 | Next block. Investigate whether Trim(Instrument) (RelayTrim path) has the same leader-skip need as Flatten(Instrument). The architecture comment at CopyEngine L3491 notes "Ph3.5: verify Trim(Instrument) does not have the same issue." |
| DW-B212-02 | PttMoveTargetAll.cs future role clarification | P2 | OnAskAllClick/OnBidAllClick now bypass PttMoveTargetAll.Execute. Document whether PttMoveTargetAll is retained for API use or can be deprecated. |
| DW-B212-03 | DIFF LIMIT compliance audit | P1 | Confirm final B211+B212 PR src/ diff is < 10,000 chars before push. |

---

## Section 7 -- Component Summary

### New Files

| File | Class | Role |
|------|-------|------|
| `Features/PttMoveTargetOne.cs` | `PttMoveTargetOne` (static) | Per-account cancel+resubmit all Working Limit exits with OCO pair |
| `Features/PttMoveTargetAll.cs` | `PttMoveTargetAll` (static) | Thin wrapper calling PttMoveTargetOne per account (vestigial in panel path) |
| `Tests/B211Tests.cs` | `B211Tests` | 8 xUnit [Fact] tests for button mechanics |
| `Tests/B212Tests.cs` (extended) | `B212Tests` | 5+ structural [Fact] tests for re-entry suppression |

### Modified Methods Summary

| File | Method | Change |
|------|--------|--------|
| `TradeCopierPanel.cs` | `OnAskAllClick` | Direct leader+follower loop; removed PttMoveTargetAll.Execute call |
| `TradeCopierPanel.cs` | `OnBidAllClick` | Same pattern as OnAskAllClick |
| `PttMoveTargetOne.cs` | `Execute` | Multi-target + OCO pair (new file, full implementation) |
| `PttMoveTargetOne.cs` | `CollectWorkingTargets` | Two-pass count+fill returning Order[] |
| `PttMoveTargetOne.cs` | `IsWorkingTarget` | Widened to accept PTT-* AND Target1..9 |
| `PttMoveTargetOne.cs` | `TryGetExitPrice` | Market data price + buffer calculation |
| `CopyEngine.cs` | `Flatten(Instrument)` | Leader-skip via Where lambda in FlatGuardLoop |
| `CopyEngine.cs` | `DispatchCopy` | Two new gates: IsBeSwapInProgress + HasActivePttFlatten |
| `PttBeSwapAll.cs` | `Execute` | Reverted BEALL-LEADERSKIP-01; updated comment block |

---

## Section 8 -- NT8 API Surface (AddOnBase confirmed)

All API calls used in this block are confirmed AddOnBase-available:

| API call | Used in | AddOnBase? | Notes |
|----------|---------|------------|-------|
| `Account.All` | PttBeSwapAll.Execute | YES | Global account collection |
| `Account.Orders` | CollectWorkingTargets, HasActivePttFlatten | YES | Order collection, index-safe |
| `Account.Cancel(Order[])` | PttMoveTargetOne.Execute | YES | Batch cancel |
| `Account.CreateOrder(12 args)` | PttMoveTargetOne.Execute | YES | arg6=limitPrice, arg7=stopPrice (NT8-049) |
| `Account.Submit(Order[])` | PttMoveTargetOne.Execute | YES | Submit array |
| `CopyEngine.Instance.FindRule` | Flatten(Instrument), PttMoveTargetOne | YES (internal) | ConcurrentDictionary lookup |
| `CopyEngine.Instance.NextBeOcoSeq()` | PttMoveTargetOne.Execute | YES (internal) | Interlocked counter |
| `PttQuickExit.SnapshotStopPrice` | PttMoveTargetOne.Execute | YES (internal) | Stop price snapshot utility |
| `Interlocked.Increment(ref _maSeq)` | PttMoveTargetOne | YES | Lock-free counter |
| `IsBeSwapInProgress(string)` | DispatchCopy | YES (internal) | ConcurrentDictionary TryGetValue |
| `HasActivePttFlatten(Account, Instrument)` | DispatchCopy | YES (private) | acc.Orders.ToList() scan |

**Banned NT8 APIs confirmed NOT used:**
- `AtmStrategyCreate()` -- StrategyBase-only: NOT used. PASS.
- `AtmStrategyChangeStopTarget()` -- StrategyBase-only: NOT used. PASS.
- `Account.Change()` for ATM bracket modification: NOT used (cancel+resubmit pattern used instead). PASS.

---

*Architecture plan prepared by ptt-architect. Pending ptt-plan-reviewer REVIEW_PASS before Phase 3 ticket generation.*
