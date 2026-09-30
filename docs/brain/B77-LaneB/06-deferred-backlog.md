# PTT Deferred Work Backlog

---

## Block B78 -- QX Follower Stop Lag (B78-LaneA) + ATM Target Dispatch Gap (REPAIR-05)

**Block completed**: 2026-08-20
**Files in scope**: PttQuickExit.cs, PttGlobalQuickExit.cs, CopyEngine.cs

### New Items from B78

| DW-ID | Description | Priority | Status |
|-------|-------------|----------|--------|
| DW-B78-01 | ATM Target1..Target9 bracket orders were not filtered by `IsExitSignalName` (Gate 0.5). They are Limit type (pass Gate 4) and had no name guard. On `[PTT-COPY] dispatch: Sell x7 mode=Named` appearing before QX fires: leader's ATM profit target (Sell Limit "Target1") was dispatched to followers in Named ATM mode, closing their Long positions before QX brackets could be placed. **FIXED REPAIR-05**: added `name.StartsWith("Target") && char.IsDigit(name[6])` guard to `IsExitSignalName`. Stop1-Stop9 are StopMarket type and already blocked by Gate 4 -- no change needed there. | P1 | **CLOSED** commit `c8c3538c` -- SIM-CONFIRMED 2026-08-20: name=Entry only on all 4 follower dispatches |
| DW-B78-02 | `CancelQxBracketsForFollowers` was called unconditionally in `PttQuickExit.Execute`. When `skipIfFollower=false` (follower execution path), each follower's Execute call ran `CancelQxBracketsForFollowers`, which silently erased every previous follower's just-submitted PTT-QX orders. Net result: all QX orders submitted then immediately cancelled before NT8 could process them. **FIXED REPAIR-06**: added `if (skipIfFollower)` guard. Follower path now only calls `CancelQxBrackets(this follower, instr, snapshot)` -- not cancel-all. Execute CYC 7->8. | P1 | **CLOSED** commit `587e9af1` -- SIM-CONFIRMED 2026-08-20: snapshot=0 on all followers, 0 race-skipped, brackets visible |

### Resolved Items from B78

| DW-ID | Description | Priority | Resolved-In | Status |
|-------|-------------|----------|-------------|--------|
| DW-B63-01 | QX places targets but no stop orders on followers (ATM bracket async lag). `snapshotStop=0` because follower ATM brackets not yet in `acc.Orders` at QX time. | P1 | B78-LaneA | **CLOSED** commit `21e4aaa4` -- SIM-CONFIRMED 2026-08-20. PTT-QX-Stop+T1/T2/T3 visible on Sim102/103/104/SimAccount1. leaderStop=7732.75 resolved correctly on all 4 followers. |


This file is maintained by ptt-plan-reviewer (Phase 5). Each block appends its own
section. Items carry forward until resolved or explicitly closed by Director.

---

## Block B77-LaneB -- QX Race Guard (BuildQxSnapshot + 3-param CancelQxBrackets)

**Block completed**: 2026-08-19
**Pipeline verdict**: FINAL_PASS
**Files in scope**: CopyEngine.cs, PttQuickExit.cs, CopyEngineTests.cs

### New Items from B77-LaneB

| DW-ID | Description | Priority | Carry-From | Status |
|-------|-------------|----------|------------|--------|
| (none) | No new deferred items introduced by B77-LaneB. | -- | -- | -- |

### Resolved Items from B77-LaneB

| DW-ID | Description | Priority | Resolved-In | Status |
|-------|-------------|----------|-------------|--------|
| DW-B76-03 | QX self-cancellation race on 8-contract accounts. PTT-QX-Stop/Stop2/Stop3 submitted+accepted at 6:48:54, then Cancel submitted at 6:48:55 with Filled=0. Hypothesis confirmed: CancelQxBrackets sweep caught newly-submitted PTT-QX orders as collateral. | P1 | B77-LaneB | **RESOLVED** -- BuildQxSnapshot captures pre-submit order set; 3-param CancelQxBrackets skips orders not in snapshot. |

---

## Block B76-LaneA -- FlattenOneAccount Race+Guard + PositionState Dedup + ATM Class-Name Fix

**Block completed**: 2026-08-18
**Pipeline verdict**: FINAL_PASS
**Files in scope**: CopyEngine.cs, TradeCopierPanel.cs, TradeCopierAddOn.cs, TradeCopierWindow.cs

### New Items from B76-LaneA (carried forward -- NOT re-opened)

| ID | Item | Priority | Status |
|----|------|----------|--------|
| DW-B76-01 | NT8 popup "Cancellation rejected -- Order is complete" on ATM teardown. NT8-internal behavior; no code fix possible without hooking NT8 internals. Document as confirmed NT8 behavior. | P3 | OPEN (doc only) |
| DW-B76-02 | GetLeaderAtmTemplateName Fallback-1 reads wrong property. `AtmStrategySelector.SelectedAtmStrategy.Name` returns "AtmStrategy" (class name). Fix: replace Fallback-1 body with `return sel?.SelectedItem as string ?? string.Empty`. | P1 | OPEN |

### Carried Items from Prior Blocks (OPEN)

| ID | Source | Item | Priority | Status |
|----|--------|------|----------|--------|
| DW-B75-01 | B75-LaneA | Non-ASCII em-dash/arrow in CopyEngine.cs (lines 202, 203, 493, 697 approx). | P2 | OPEN |
| DW-B75-02 | B75-LaneA | [PTT-CLONE] diagnostic Output.Process lines. Remove after Clone live confirm. | P2 | CLOSED (B77 post -- Director approved) |
| DW-B75-03 | B75-LaneA | 14 NT8-runtime-bound tests marked [Fact(Skip="NT8-runtime")]. | P2 | OPEN |
| DW-B75-04 | B75-LaneA | HasWorkingPttCopy no retry counter guard. | P2 | OPEN |
| DW-B66-BE-01 | B66/B74 | CancelQxBrackets cancels PTT-BE-Stop during QX. Director confirmation required. | P1 | CLOSED -- behaviour confirmed correct. Live PTT-BE-Stop orders cancelled+replaced by QX (intended). Armed-only slots live in _pendingBeSlots (in-memory), never in acc.Orders, so QX cannot touch them. Both paths work as designed. |
| DW-B66-C-02 | B66/B74 | DispatchCopy Gate 5 dedup key = 0.0 for StopLimit. | P1 | CLOSED -- non-issue. DispatchCopy Gate 4 blocks OrderType.StopLimit before IsDedup is reached. StopLimit orders never enter the dedup path. |
| DW-B63-01 | B63/B74 | QX places targets but NO stop orders on followers (ATM bracket async lag). | P1 | **CLOSED** -- FIXED B78-LaneA commit `21e4aaa4` + SIM-CONFIRMED 2026-08-20. PTT-QX brackets visible on all 4 followers. |
| DW-B54-01 | B54 | ATM auto-inject blocked: AtmStrategyCreate() is StrategyBase-only. | P1 | OPEN (blocked) |
| DW-B72-01 | B72-LaneA | IsAtmBracketName("Stop10") returns true -- known edge. | P3 | OPEN |
| DW-B73-B-01 | B73-LaneB | RaiseBeAllDisarmed redundant broadcasts on flat. | P2 | OPEN |
| DW-B73-B-02 | B73-LaneB | UpdateBeAllVisuals unfrozen SolidColorBrush instances. | P2 | OPEN |
| DW-B58-01 | B58 | SnapshotTargetsPublic hardcoded order-name prefixes. | P2 | OPEN |
| DW-B58-02 | B58 | GlobalBe non-atomic lazy init. | P2 | OPEN |
| DW-B58-03 | B58 | RelayBe OcoGroup not forwarded. | P2 | OPEN |
| PRE-EXISTING-03 | pre-B72 | deploy-sync.ps1 archived; sync is manual. | P2 | OPEN |

---

*Append-only. New blocks add a new ## section above this footer.*
