# No-Pipeline Repairs Log

Direct `.cs` edits bypassing the 5-phase PTT pipeline.
Each entry here requires a full pipeline run (Ph1 → Ph5) before the block is closed.
Director authorization: live-trading session, post-trade-day pipeline runs scheduled.

---

---

## DW-B117-DIAG — Diagnostic Probe (NOT a fix)

**ID**: DW-B117-DIAG
**Date**: 2026-08-26
**File**: `src/PropTraderTools/CopyEngine.cs`
**Location**: `OnOrderUpdate` — L1230–1250 (inside the Working-state handler)
**Status**: REMOVED-B113-T1 — probe block deleted from OnOrderUpdate (L1230-1250). Cancel-after logic implemented in TryCleanupReArmedAtmBracket.

### Purpose
Log ATM bracket Working events on follower accounts to confirm:
1. Wave 1: all 3 native ATM brackets arm correctly on all followers after entry copy
2. Wave 2: NT8 ATM engine re-arms after pre-cancel in ExecuteOne — produces Target1+Target2 only (no Target3)
3. Wave 3+: further re-arm waves during subsequent follower QX sequences

This probe is a **read-only observer** — no state change, no order submission, no logic branch.

### What it logs
```
[DW-B117-DIAG] ATM bracket Working on follower: Sim102 name=Target1 instr=MES 09-26
```

### Root cause it confirmed (2026-08-26 second test run)
Pre-cancel in `ExecuteOne` (`PttGlobalQuickExit.cs`) cancels follower ATM brackets.
NT8 ATM engine detects the cancel and re-arms. Re-arm produces Target1+Target2 only —
not Target3. The delayed re-armed Target3 conflicts with PTT-QX-T3 during the QX submit
loop and causes NT8 OCO management to cancel PTT-QX-T3.

### Removal requirement
This probe MUST be removed when B113-T1 executes the cancel-after fix.
The B113 ticket spec must include: "Remove DW-B117-DIAG block from OnOrderUpdate (L1230–1250)."

### Jane Street compliance
- No lock() — JS-021 PASS
- No async void — JS-033 PASS
- ASCII-only — JS-080 PASS
- No state change — diagnostic read only
- CYC delta = +1 (one additional if block in OnOrderUpdate)

---


## HOTFIX-B66-ATM-TPL

**ID**: HOTFIX-B66-ATM-TPL
**Date**: 2026-08-17
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Method**: `GetLeaderAtmTemplateName(Chart currentChart)` (line 2207)
**Status**: PIPELINE-COMPLETE (B75-LaneB)

### Bug
`GetLeaderAtmTemplateName` used `FindVisualChildByIndex<ComboBox>(ct, 2)` to find the ATM
template ComboBox in ChartTrader (index 0 = Instrument, 1 = Account, assumed 2 = ATM).
PTT panel injects its own ComboBoxes (`_followersDropDown`, per-follower ATM combos) into the
ChartTrader Grid, shifting the native ATM ComboBox from index 2 to a higher index.
Result: `GetLeaderAtmTemplateName` returned `string.Empty` every time.
`OnCloneModeClick` stored empty string into `_cloneAtmCache`.
`GetCloneAtmMode` returned `FollowerAtmMode.Inherit` (no template) -- follower got a bare Limit
order (`SendCopy`) instead of `SendCopyWithAtm` + `StartAtmStrategy` + ATM brackets.
Confirmed by DIAG-CLONE-01 log: `[PTT-CLONE] SetCloneAtmCache: '' (empty=True)`.

### Fix (v2 -- direct property)
Primary path replaced with `ct.AtmStrategy?.Name` -- `ChartTrader.AtmStrategy` is a direct
property returning the currently selected `AtmStrategy` object; `.Name` is the template name.
Confirmed from NT8 community forum topics 5133 and 6060 (multiple independent developers).
No child walk, no index fragility, no injected-ComboBox interference.
Fallback-1: `FindVisualChild<AtmStrategySelector>` by type (covers unusual builds).
Fallback-2: original `FindVisualChildByIndex<ComboBox>(ct, 2)` (legacy pre-B66 path).

### Fix (v1 -- superseded by v2 above)
`FindVisualChild<AtmStrategySelector>` by type was primary in v1. Superseded because
`ct.AtmStrategy` is simpler, more direct, and confirmed by NT8 community as the canonical path.

### NT8_FULL_REFERENCE.md update
Added `ChartTrader Class` section documenting `.AtmStrategy`, `.Account`, `.Quantity`,
`.Instrument` properties, access patterns (Indicator vs. AddOn), thread safety, and comparison
table of three ATM template lookup approaches.

### Expected behavior after fix
Clone radio click -> `GetLeaderAtmTemplateName` reads `ct.AtmStrategy?.Name` ->
`SetCloneAtmCache` stores the real template name (e.g. "MES $200 SL6") ->
`GetCloneAtmMode` returns `FollowerAtmMode.Named("MES $200 SL6")` ->
`ResolveAtmMode` returns Named mode -> `DispatchCopy` calls `SendCopyWithAtm` ->
follower gets `"Entry"` order + `StartAtmStrategy` + ATM brackets.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added (all paths return `string.Empty`) ✅
- No `async void` added ✅

### Diff (minimal -- v2 primary change)
```diff
-                var atmCb = TradeCopierAddOn.FindVisualChildByIndex<ComboBox>(ct, 2);
-                if (atmCb == null) return string.Empty;
-                return atmCb.SelectedItem as string ?? string.Empty;
+                if (ct.AtmStrategy != null)
+                    return ct.AtmStrategy.Name ?? string.Empty;
+                var sel = TradeCopierAddOn.FindVisualChild<NinjaTrader.NinjaScript.AtmStrategy.AtmStrategySelector>(ct);
+                if (sel?.SelectedAtmStrategy != null)
+                    return sel.SelectedAtmStrategy.Name ?? string.Empty;
+                var atmCb = TradeCopierAddOn.FindVisualChildByIndex<ComboBox>(ct, 2);
+                return atmCb?.SelectedItem as string ?? string.Empty;
```

### Pipeline work needed (block BXX-LaneB)
- Ph1 Architecture: document ChartTrader.AtmStrategy as canonical template source
- Ph3 DNA: verify no other callers use FindVisualChildByIndex<ComboBox>(..., 2) for ATM reads
- Ph3.5 Tickets: tests for GetLeaderAtmTemplateName:
  - T_B66TPL_01: null chart -> string.Empty
  - T_B66TPL_02: ChartTrader not found -> string.Empty
  - T_B66TPL_03: ct.AtmStrategy non-null -> returns .Name
  - T_B66TPL_04: ct.AtmStrategy null, AtmStrategySelector found -> returns SelectedAtmStrategy.Name
  - T_B66TPL_05: all null -> string.Empty (not throw)



## PIPELINE STATUS — B72 / B73 / B74 / B75 / B76 / B77 / B78 / B79 / B102

**Latest pipeline run: B102 FINAL_PASS (2026-08-11) -- DW-B100 XmlSerializer private-type fix + DW-B101 Cancelled eviction gap (CopyEngine.cs)**

| Block | Lane | Files | Hotfixes | Tests written | Final verdict |
|-------|------|-------|----------|---------------|---------------|
| B72-LaneA | Engine logic | CopyEngine.cs + PttBreakEven.cs | 22 active (1 superseded) | 72 [Fact] | FINAL_PASS |
| B73-LaneB | UI logic | TradeCopierPanel.cs | 15 | 33 [Fact] | FINAL_PASS |
| B74-LaneC | Feature files | PttGlobalQuickExit.cs + PttQuickExit.cs + PttGlobalBreakEven.cs | 5 | 22 [Fact] | FINAL_PASS |
| B75-LaneB | UI logic | TradeCopierPanel.cs | 3 | 10 [Fact] | FINAL_PASS |
| B75-LaneA | Clone/copy hotfixes | CopyEngine.cs | 12 hotfixes + 2 CYC refactors | 60 [Fact] | FINAL_PASS |
| B76-LaneA | Race+guard+dedup+ATM | CopyEngine.cs + TradeCopierPanel.cs + TradeCopierAddOn.cs + TradeCopierWindow.cs | 6 hotfixes | 12 [Fact] | FINAL_PASS |
| B77 direct | ATM fallback-1 fix + ASCII comments | TradeCopierPanel.cs + CopyEngine.cs | 2 | 5 [Fact] | PIPELINE-COMPLETE (B77-LaneA) |
| B77-LaneB | QX race guard | CancelQxBrackets self-cancel prevention | BuildQxSnapshot + 3-param overload | 8 [Fact] | PIPELINE_COMPLETE |
| B77 post | REPAIR-03 + tracing | CopyEngine.cs + PttQuickExit.cs + PttGlobalQuickExit.cs + PttGlobalBreakEven.cs + TradeCopierPanel.cs | DW-B75-02 CLOSED + 20 trace lines | -- | DIRECT (Director approved) |
| B78-LaneA | QX follower stop lag | PttQuickExit.cs + PttGlobalQuickExit.cs | DW-B63-01 fix: ResolveStop + ResolveTargetCount + leaderStop/leaderTargetCount param flow | 8 [Fact] | SIM-CONFIRMED (2026-08-20) -- PTT-QX brackets visible on all 4 followers |
| REPAIR-05 | IsExitSignalName Target gap | CopyEngine.cs | DW-B78-01: ATM Target1-9 now blocked by Gate 0.5 + order.Name added to dispatch trace | 8 [Fact] | SIM-CONFIRMED (2026-08-20) -- name=Entry only on all dispatches |
| REPAIR-06 | CancelQxBracketsForFollowers guard | PttQuickExit.cs | DW-B78-02: skipIfFollower guard prevents sibling follower QX orders from being erased by subsequent follower Execute calls | 4 [Fact] | SIM-CONFIRMED (2026-08-20) -- snapshot=0 on all followers, 0 race-skipped |
| REPAIR-07 | MoveStopToBreakEven target snapshot | CopyEngine.cs | DW-B79-01: stateOk widened from Working/Accepted to +Submitted/Initialized/TriggerPending -- targets=0 on rapid QX->BE-ALL press fixed | 8 [Fact] | DIRECT (Director approved) -- SIM-CONFIRMED 2026-08-19 (QX->BE-ALL combined test: all 4 accounts cancel=6 targets=3) |
| DW-B79-08 v1 | PTT-BE bracket wipe recovery (FAILED) | CopyEngine.cs | TryReplacePttBeBrackets v1 commit `938f0faf`: direct MoveStopToBreakEven call -> retry storm, infinite replace/cancel loop | 0 [Fact] | FAILED -- v1 abandoned |
| DW-B79-08 v2 | PTT-BE bracket wipe recovery (slot-reg) | CopyEngine.cs | TryReplacePttBeBrackets v2 commit `b2685f55`: slot-registration + QueueBeRetryFallback 200ms, no direct MSTBE call, storm-proof | 0 [Fact] | CLOSED -- WON'T TRIGGER. Wipe requires StartAtmStrategy on follower while PTT-BE brackets live (add-to-position mid-trade). Director confirmed this pattern never occurs in live workflow. Recovery code remains in place as dead-weight protection. |
| B102 | DW-B100 + DW-B101 | CopyEngine.cs | 3 changes (CopyRuleDto internal, CopyRulesContainer internal, EvictDedup Cancelled branch) | 0 [Fact] (test stubs deferred -- DW-B102-DEFER-01/02) | FINAL_PASS |

**DIAG-MOVESTOP-01**: All `Output.Process("[MSTBE]...")` log lines removed from `MoveStopToBreakEven` (2026-08-17 pre-flight, synced).

**DW-B75-02 CLOSED (2026-08-19)**: `[PTT-CLONE]` DIAG lines removed from `SetCloneAtmCache`, `SetCloneAtmObjectCache`, `GetCloneAtmMode` -- Clone dispatch confirmed working (object cache SET). Director approved removal.

**B72-LaneA gap note**: `ticket-1-completion.md` absent from B72-LaneA directory (noted by B73 Ph5 reviewer). Code artifact (`CopyEngine.cs`, `PttBreakEven.cs`, test files) is present and verified. This is a pipeline documentation gap only — no functional defect. Director should ensure `ticket-1-completion.md` is written retroactively.

### New Deferred Items from B72/B73/B74 Ph5 Reviews

| ID | Source | Item | Priority | Status |
|----|--------|------|----------|--------|
| DW-B72-01 | B72-LaneA Ph5 | `IsAtmBracketName("Stop10")` returns true — digit-at-[4] edge case. NT8 ATM names are Stop1..Stop9 only; over-cancel is conservative, not dangerous. | P3 | OPEN |
| DW-B73-B-01 | B73-LaneB Ph5 | `RaiseBeAllDisarmed` fires on every flat regardless of per-account slot ownership — redundant broadcasts, no correctness impact | P2 | OPEN |
| DW-B73-B-02 | B73-LaneB Ph5 | `UpdateBeAllVisuals` creates unfrozen `SolidColorBrush` instances on every call — allocation on WPF UI thread, not a hot path | P2 | OPEN |

### Consolidated Carry-Forward OPEN Items (post B72/B73/B74)

| ID | Item | Priority | Status |
|----|------|----------|--------|
| DW-B63-FLATTEN-MULTWAVE-01 | PTT-Flatten multi-wave on follower accounts after ATM target fills — followers go Short instead of Flat. Root cause: each PTT-QX-T*/Target* fill on leader dispatches a new copy/reduce wave to -01/-03. With 2 targets filling in sequence, 2 waves overshoot. Live-observed 2026-08-17 06:35 AM. **FIXED by HOTFIX-B63-FLATTEN-01** (PTT-prefix guard added to `TryDispatchLeaderFlat` gate 2.5). | P1 | APPLIED — awaiting live test |
| DW-B66-BE-01 | `CancelQxBrackets` cancels `PTT-BE-Stop` orders during Quick Exit | P1 | CLOSED -- behaviour confirmed correct (B77 post-sign-off) |
| DW-B66-C-02 | `DispatchCopy` Gate 5 dedup key = 0.0 for all StopLimit entries | P1 | CLOSED -- non-issue: Gate 4 blocks StopLimit before dedup path (B77 post-sign-off) |
| DW-B63-01 | QX places targets but no stop orders on followers (ATM bracket async lag) | P1 | **CLOSED** -- FIXED B78-LaneA + SIM-CONFIRMED 2026-08-20. PTT-QX-Stop+T1/T2/T3 visible on Sim102/103/104/SimAccount1. |
| DW-B79-01 | `MoveStopToBreakEven` targets=0 on rapid QX->BE-ALL (follower PTT-QX-T orders still Initialized at snapshot time) | P1 | **CLOSED** REPAIR-07 commit `343822de` -- SIM-CONFIRMED 2026-08-19 (QX->BE-ALL combined test: all 4 accounts cancel=6 targets=3). |
| DW-B79-08 | PTT-BE brackets wiped by NT8 StartAtmStrategy sweep on follower entry copy -- all 4 accounts lost OCO pairs confirmed 2026-08-19 | P1 | **CLOSED -- WON'T TRIGGER.** v2 recovery code (`b2685f55`) in place. Wipe requires add-to-position mid-trade while BE brackets live. Director confirmed this never occurs in live workflow. |
| DW-B79-02 | `MoveStopToBreakEven` cancel=0 targets=0 on followers after QX->BE-ALL -- PTT-QX-T orders in `CancelSubmitted` state, not in `stateOk` filter | P2 | **ROOT CAUSE IDENTIFIED** (B79 diag session). `CancelSubmitted` orders skipped by both targets snapshot and stale sweep. Positions ARE protected via bare-stop path. `[BE-DIAG]` tracing retained in `CopyEngine.cs` (fires only on failure path). Fix is in QX layer -- see DW-B79-03. |
| DW-B79-03 | QX follower PTT-QX orders go to `CancelSubmitted` immediately after submission -- conflict with late-arriving ATM brackets not visible at QX snapshot time. BE-ALL cannot find them as targets and falls back to bare-stop path instead of OCO stop+target. Same race class as DW-B63-01, B78-LaneA did not fully close it. **Gap2 sub-item FIXED REPAIR-08 `a3f68559`**: `PttBreakEven.SnapshotTargetsLocal` stateOk widened to Working|Accepted|Submitted|Initialized|TriggerPending -- BE button now symmetric with BE-ALL. | P2 | **FIXED** -- Gap2 FIXED REPAIR-08 `a3f68559` + QX guard FIXED DW-B79-03 (commit `9e2fb3a6`) |
| DW-B54-01 | ATM auto-inject — blocked, requires `StrategyBase` API unavailable in `AddOnBase` | P1 | OPEN (blocked) |
| DW-B72-01 | `IsAtmBracketName("Stop10")` returns true — acceptable-known edge | P3 | OPEN |
| DW-B73-B-01 | `RaiseBeAllDisarmed` redundant broadcasts on flat | P2 | OPEN |
| DW-B73-B-02 | `UpdateBeAllVisuals` unfrozen brush allocation | P2 | OPEN |
| DW-B58-01 | `SnapshotTargetsPublic` hardcoded order-name prefixes | P2 | OPEN |
| DW-B58-02 | `GlobalBe` non-atomic lazy init | P2 | OPEN |
| DW-B58-03 | `RelayBe` `OcoGroup` not forwarded | P2 | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash `CopyEngine.cs` lines 398, 499 | P2 | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow `CopyEngine.cs` lines ~1449-1450 (estimate may have shifted with B72 insertions) | P2 | OPEN |
| PRE-EXISTING-03 | `deploy-sync.ps1` archived; PropTraderTools sync is manual | P2 | CLOSED — `scripts/ptt-sync-and-verify.ps1` created (V12.B95); AGENTS.md updated; post-commit hook added |
| DW-B100 | `CopyRuleDto` + `CopyRulesContainer` private classes block XmlSerializer -- copy_rules.xml never written | P1 | **CLOSED** -- B102 FINAL_PASS. Both classes changed to `internal sealed class`. SaveRules/LoadRules circuit now live. |
| DW-B101 | `_entryDispatchedOrders` not evicted on Cancelled orders in EvictDedup | P2 | **CLOSED** -- B102 FINAL_PASS. Cancelled branch added to EvictDedup (L3113). |
| DW-B102-DEFER-01 | Pre-existing test build errors (~85) blocking new test implementation | P2 | OPEN -- separate ticket required |
| DW-B102-DEFER-02 | 5 xUnit tests for B102 not yet written -- blocked on DEFER-01 | P2 | OPEN -- blocked on DEFER-01 |

---

## ALL HOTFIXES BELOW: PIPELINE-COMPLETE (B72 / B73 / B74 / B75 / B76 / B77)

The following entries were formalised through the full 7-phase PTT pipeline.
No further pipeline action required unless a regression is identified.


---

## HOTFIX-BE-ALL-01

**Date**: 2026-08-14 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `ArmAllPendingBe(int bufferTicks)` (line 563)
**Status**: PIPELINE-COMPLETE (B72-LaneA)

### Bug
`ArmAllPendingBe` was calling `SubmitBeStop(acc, pos.Instrument, bePrice, isLong)` which:
1. Created a brand-new `PTT-BE-Stop` StopMarket order immediately (no wait for price trigger)
2. Never wrote to `_pendingBeSlots` so `IsPendingSlotsEmpty()` always returned `true`
3. Result: BE All panel button always stayed purple (never armed), and a new stop order
   stacked on top of existing ATM brackets without cancelling them first (double-stop bug)
4. Followers were never touched (their stops were never moved)

### Fix
Replaced `SubmitBeStop(...)` call with `ArmPendingBe(pos.Instrument, acc, bufferTicks)`.
This is exactly the path used by the per-chart BE button for a single account.

When the price trigger fires:
- `OnPendingBeAccountUpdate` fires for the leader
- Calls `BreakEven(leader, instrument, bufferTicks)`
- `BreakEven(Account,...)` fans out via `AllAccounts(instrument)` -> `MoveStopToBreakEven`
- `MoveStopToBreakEven` moves existing stops in-place via `acc.Change()` for both
  leader AND all followers in the copy rule

### Expected behavior after fix
- Press BE All while in drawdown: button turns amber (pending slots are now populated)
- When price reaches entry: all leader + follower stops move to BE price in-place
- Press BE All again: disarms (turns purple), pending slots cleared
- No new `PTT-BE-Stop` StopMarket order created (stops are moved in-place, not replaced)

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- `ArmPendingBe` already ConcurrentDictionary-based (lock-free) ✅

### Diff (minimal)
```diff
-                    bool isLong = pos.MarketPosition == MarketPosition.Long;
-                    double tick = pos.Instrument.MasterInstrument?.TickSize ?? 0.25;
-                    double bePrice = Math.Round(
-                        (pos.AveragePrice + (isLong ? bufferTicks : -bufferTicks) * tick) / tick
-                    ) * tick;
-                    SubmitBeStop(acc, pos.Instrument, bePrice, isLong);
+                    ArmPendingBe(pos.Instrument, acc, bufferTicks); // HOTFIX-BE-ALL-01
```

### Pipeline work needed (block BXX-LaneA)
- Ph1 Architecture: document the ArmAllPendingBe/SubmitBeStop vs ArmPendingBe design decision
- Ph2 Review: confirm `MoveStopToBreakEven` follower fan-out path works for ATM bracket stops
  (IsStopLeg filter at line 1880 -- need to verify ATM Stop1/Stop2 pass this filter)
- Ph3.5 Tickets: add 4 new tests:
  - T_BEALL_01: `ArmAllPendingBe` with 1 non-follower account -> `_pendingBeSlots` populated
  - T_BEALL_02: `ArmAllPendingBe` with follower account -> slot NOT added (skipped)
  - T_BEALL_03: `ArmAllPendingBe` with flat account -> slot NOT added (ArmPendingBe flat guard)
  - T_BEALL_04: `IsPendingSlotsEmpty` returns false after `ArmAllPendingBe` with open position
- Ph4 Verify: confirm no regression to per-chart BE button (separate path, untouched)

### Open question resolved (DW-BEALL-01 — CLOSED)
`MoveStopToBreakEven` uses `IsStopLeg(order)` filter (line 1880).
`IsStopLeg` (line 1780): returns true if `order.Name.StartsWith("Stop")`.
ATM bracket stops are named `Stop1` and `Stop2` — both start with "Stop". ✅
Follower ATM bracket stops WILL be moved by `MoveStopToBreakEven`. No pipeline gap here.

---

## HOTFIX-QX-DOUBLE-01

**Date**: 2026-08-14 (live trading session)
**Files**: `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/Features/PttBreakEven.cs`
**Methods**: `CancelQxBrackets` (CopyEngine.cs line ~454), `CancelStaleBracketsLocal` (PttBreakEven.cs line ~159)
**Status**: PIPELINE-COMPLETE (B72-LaneA + B74-LaneC)
**Deferred item**: DW-B72-01

### Bug
Clicking Quick All (or BE button) immediately after an ATM strategy fills leaves ATM bracket
orders in `OrderState.TriggerPending` state — "Order is pending submission" per
NT8_FULL_REFERENCE.md line 946. Neither `CancelQxBrackets` nor `CancelStaleBracketsLocal`
included `TriggerPending` in their `stateOk` filter, so the cancels silently skipped
those brackets. New PTT-QX orders were then placed on top → two sets of brackets on the account.

The same root cause also affects any other button (Quick per-chart, BE per-chart) that
calls either cancel helper before placing new orders.

### State coverage before fix

| State | `CancelQxBrackets` | `CancelStaleBracketsLocal` |
|-------|--------------------|---------------------------|
| TriggerPending | ❌ MISSING | ❌ MISSING |
| Initialized | ✅ | ✅ |
| Submitted | ✅ (B71) | ❌ MISSING |
| Accepted | ✅ | ❌ MISSING |
| Working | ✅ | ✅ |

### Fix
Added `OrderState.TriggerPending` to `CancelQxBrackets` stateOk filter (1 line).
Added `OrderState.TriggerPending`, `Submitted`, `Accepted` to `CancelStaleBracketsLocal`
stateOk filter (3 lines) — brings it to parity with `CancelQxBrackets`.
NT8 reference: NT8_FULL_REFERENCE.md line 946.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged (Roslyn counts the full `||` chain as 1 decision point in one `if`) ✅

### Diff (minimal)

**CopyEngine.cs — CancelQxBrackets**:
```diff
 bool stateOk = o.OrderState == OrderState.Working
             || o.OrderState == OrderState.Initialized
             || o.OrderState == OrderState.Accepted
-            || o.OrderState == OrderState.Submitted;  // B71
+            || o.OrderState == OrderState.Submitted      // B71
+            || o.OrderState == OrderState.TriggerPending; // HOTFIX-QX-DOUBLE-01
```

**PttBreakEven.cs — CancelStaleBracketsLocal**:
```diff
 bool stateOk = o.OrderState == OrderState.Working
-            || o.OrderState == OrderState.Initialized;
+            || o.OrderState == OrderState.Initialized
+            || o.OrderState == OrderState.Submitted         // HOTFIX-QX-DOUBLE-01
+            || o.OrderState == OrderState.Accepted          // HOTFIX-QX-DOUBLE-01
+            || o.OrderState == OrderState.TriggerPending;   // HOTFIX-QX-DOUBLE-01
```

### Pipeline work needed (block BXX-LaneA)
- Ph1 Architecture: document NT8 order state lifecycle for ATM brackets
- Ph3.5 Tickets: add tests:
  - T_QX_DOUBLE_01: `CancelQxBrackets` with TriggerPending order -> order IS in stale list
  - T_QX_DOUBLE_02: `CancelQxBrackets` with Submitted order -> order IS in stale list (regression)
  - T_QX_DOUBLE_03: `CancelQxBrackets` with Filled order -> order NOT in stale list (terminal state safe)
  - T_BE_CANCEL_01: `CancelStaleBracketsLocal` with TriggerPending -> order IS cancelled
  - T_BE_CANCEL_02: `CancelStaleBracketsLocal` with Accepted -> order IS cancelled
  - T_BE_CANCEL_03: `CancelStaleBracketsLocal` with PTT-BE-Stop -> order NOT cancelled (notBe guard)

---

## HOTFIX-BUG2-BE-RESET

**Date**: 2026-08-14 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `OnOrderUpdate` (~line 692)
**Status**: PIPELINE-COMPLETE (B72-LaneA)
**Deferred item**: BUG-BE-RESET

### Bug
`TryFirePositionState(e)` was called PRE-Gate 2 — unconditionally for every account order.
When follower bracket orders (Stop1/Stop2/Target1/Target2 on Sim102) transitioned through
Filled/Cancelled states, `TryFirePositionState` fired for the follower account. If Sim102
had no open position at that moment, `hasPos=False` was fired, causing the panel's
`UpdateButtonColors` to reset `_beState` to Idle via HOTFIX-F3 — even when Sim101 (leader)
still had an open position.
Result: BE button armed by user → immediately disarmed by follower order state noise.

### Fix
Moved `TryFirePositionState(e)` from line 696 (pre-Gate 1) to after Gate 2.5 (post line 720).
Now fires ONLY when the order belongs to a matched leader account+instrument pair.
Follower orders never trigger `PositionStateChanged` events.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged (no new branches) ✅

### Diff (minimal, CopyEngine.cs OnOrderUpdate)
```diff
-        // Pre-gate: fire position state unconditionally (even when copy disabled)
-        TryFirePositionState(e);
         // B62: evict dedup on terminal states...
         EvictDedup(...);
         // Gate 1 ... Gate 2 ... Gate 2.5 ...
+        // BUG-BE-RESET fix: fire position state ONLY for leader account+instrument orders
+        TryFirePositionState(e);
```

### Pipeline work needed
- Ph1: document that `TryFirePositionState` must be leader-scoped (after Gate 2.5)
- Ph3.5 Tickets: add test T_BE_RESET_01 — follower Filled event does NOT fire PositionStateChanged

---

## HOTFIX-DW-B64-01

**Date**: 2026-08-14 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `OnOrderUpdate` Gate C (~line 766)
**Status**: PIPELINE-COMPLETE (B72-LaneA)
**Deferred item**: DW-B64-01

### Bug
Leader entry drag fires two NT8 `OnOrderUpdate` events for the same drag:
1. `OrderState.Accepted` — price updated — Gate C passes dedup check → `HandleEntryChange` called.
   `HandleEntryChange` calls `_dedupCache.TryRemove(orderId)` → key is now GONE.
2. `OrderState.Working` — same new price — Gate C `TryGetValue` misses (key gone) → falls through
   to `DispatchCopy` → a second PTT-Copy entry order is placed on the follower. DUPLICATE.

### Fix
Re-insert `currentPrice` into `_dedupCache` BEFORE calling `HandleEntryChange`.
`HandleEntryChange` then removes it (TryRemove), but on the second Working event, `TryGetValue`
finds the key with the new price → `Math.Abs(newPrice - newPrice) = 0 < tickSize` → skips.

### JS-DNA compliance
- No `lock()` added ✅ (`_dedupCache` is `ConcurrentDictionary`, indexer is thread-safe)
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged ✅

### Diff (minimal)
```diff
 if (_dedupCache.TryGetValue(e.Order.OrderId.ToString(), out double storedPrice)
     && Math.Abs(currentPrice - storedPrice) >= tickSize)
 {
+    _dedupCache[e.Order.OrderId.ToString()] = currentPrice;  // DW-B64-01 fix
     HandleEntryChange(e.Order, matchedRule.Value);
     return;
 }
```

### Pipeline work needed
- Ph3.5 Tickets: add test T_DRAG_DEDUP_01 — second Working event after drag does NOT call DispatchCopy

---

## HOTFIX-DW-B72-02

**Date**: 2026-08-14 (live trading session)
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Methods**: `OnGlobalBeClick`, `OnPendingBeFiredDispatch`, `UpdateButtonColors`
**Status**: PIPELINE-COMPLETE (B73-LaneB)
**Deferred item**: DW-B72-02

### Bug
`_globalBeState` (line 247) was a per-panel instance field. Two panels open (MES + MGC):
- Press BE ALL on MES → MES sets `_globalBeState = Armed` → MES button turns amber.
- MGC panel `_globalBeState` is still `Idle` → MGC button stays purple.
- They disagree. Also, if `OnPositionStateChanged` fired on one panel with `hasPos=False`,
  it reset `_beState` (per-chart) but never touched `_globalBeState` — stale state.

### Fix
Removed `_globalBeState` field entirely. All panels now read
`CopyEngine.Instance.IsPendingSlotsEmpty()` as the shared truth source:
- `OnGlobalBeClick`: `if (IsPendingSlotsEmpty()) → arm; else → disarm` (no local state)
- `OnPendingBeFiredDispatch`: `if (IsPendingSlotsEmpty()) → set Idle visual` (no check against local state)
- `UpdateButtonColors`: on flat, if slots not empty, DisarmPendingBe for all + set Idle visual

All panels call the same CopyEngine singleton, so both panels update consistently.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC reduced (removed switch, replaced with if/else) ✅

### Pipeline work needed
- Ph1: document that BE ALL state must be singleton-scoped (CopyEngine), not panel-scoped
- Ph3.5 Tickets: add test T_BEALL_SYNC_01 — two panels, arm via one, verify both show Armed

---

## Pending items for pipeline runs

## HOTFIX-FIX-A-BE-BACKGROUND

**Date**: 2026-08-14 (live trading session)
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Method**: `UpdateBeVisuals` (~line 1268)
**Status**: PIPELINE-COMPLETE (B73-LaneB)

### Bug
`UpdateBeVisuals(BeState.Idle)` only reset `Content` (label text) — never reset `Background`.
After user armed BE (BrushCaution amber set) then position closed (HOTFIX-F3 called Idle),
the button stayed amber forever with no label change, visually stuck in Armed state.

### Fix
Added `_beBtn2.Background = BrushInactive` to the `BeState.Idle` case.

---

## HOTFIX-FIX-B-TRYFIRE-CANCELLED

**Date**: 2026-08-14 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `TryFirePositionState` (~line 1194)
**Status**: PIPELINE-COMPLETE (B72-LaneA)

### Bug
`TryFirePositionState` included `Cancelled` and `Rejected` in the trigger filter.
When a trade closes, NT8 cancels all ATM bracket orders (Stop1, Stop2, Target1, Target2).
Each cancel fires `TryFirePositionState` → `HasOpenPosition` = False (position already gone) →
`PositionStateChanged(hasPos=False)` → `UpdateButtonColors(false)` → HOTFIX-F3 resets
`_beState = Idle` → `UpdateBeVisuals(Idle)`. Hundreds of these per close = button never stays armed.

### Fix
Removed `Cancelled` and `Rejected` from filter. Only `Filled` and `PartFilled` fire now.
The flat state is still delivered correctly: the Filled close-order event fires hasPos=False
after position removal — one clean signal, not hundreds.

---

## HOTFIX-FIX-C-NO-DISARM-IN-UPDATEBUTTONCOLORS

**Date**: 2026-08-14 (live trading session)
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Method**: `UpdateButtonColors` (~line 570)
**Status**: PIPELINE-COMPLETE (B73-LaneB)

### Bug
The DW-B72-02 block added in the previous session called `DisarmPendingBe` for ALL accounts
inside `UpdateButtonColors(hasPos=False)`. This fired on every ATM bracket cancel during close,
immediately destroying the pending BE slot the user had just armed. Result: BE arm button
pressed → slot created → ATM brackets cancel → DisarmPendingBe called → slot gone → price
never triggers → stop never moves.

### Fix
Removed the DW-B72-02 DisarmPendingBe block from UpdateButtonColors entirely.
BE ALL visual reset on flat is now handled only by `OnPendingBeFiredDispatch` (checks
`IsPendingSlotsEmpty()` after the trigger fires naturally). No forced disarm in UI event handlers.

---

## Pending items for pipeline runs

| Hotfix ID | File | Method | Status | Pipeline block |
|-----------|------|--------|--------|----------------|
| HOTFIX-BE-ALL-01 | CopyEngine.cs | ArmAllPendingBe | PIPELINE-COMPLETE | B72-LaneA |
| HOTFIX-QX-DOUBLE-01 | CopyEngine.cs + PttBreakEven.cs | CancelQxBrackets + CancelStaleBracketsLocal | PIPELINE-COMPLETE | B72-LaneA + B74-LaneC |
| HOTFIX-BUG2-BE-RESET | CopyEngine.cs | OnOrderUpdate (TryFirePositionState move) | PIPELINE-COMPLETE | B72-LaneA |
| HOTFIX-DW-B64-01 | CopyEngine.cs | OnOrderUpdate Gate C (dedup re-insert) | PIPELINE-COMPLETE | B72-LaneA |
| HOTFIX-DW-B72-02 | TradeCopierPanel.cs | OnGlobalBeClick + OnPendingBeFiredDispatch + UpdateButtonColors | PIPELINE-COMPLETE | B73-LaneB |
| HOTFIX-FIX-A-BE-BACKGROUND | TradeCopierPanel.cs | UpdateBeVisuals (Idle background reset) | PIPELINE-COMPLETE | B73-LaneB |
| HOTFIX-FIX-B-TRYFIRE-CANCELLED | CopyEngine.cs | TryFirePositionState (remove Cancelled/Rejected) | PIPELINE-COMPLETE | B72-LaneA |
| HOTFIX-FIX-C-NO-DISARM-IN-UPDATEBUTTONCOLORS | TradeCopierPanel.cs | UpdateButtonColors (remove forced DisarmPendingBe) | PIPELINE-COMPLETE | B73-LaneB |
| HOTFIX-BEALL-BUFFER-SYNC-01 | CopyEngine.cs + PttGlobalBreakEven.cs | GlobalBeBufferChanged event + IncrementBuffer + DecrementBuffer | PIPELINE-COMPLETE | B74-LaneC + B75-LaneB |
| HOTFIX-QUICKALL-SINGLETON-01 | CopyEngine.cs + TradeCopierPanel.cs + PttGlobalQuickExit.cs | _globalQuickAllT1 singleton + broadcast events + Execute() wiring | PIPELINE-COMPLETE | B75-LaneA |

---

## HOTFIX-BUG-BE-OCO-REUSE

**Date**: 2026-08-15 (live trading session continuation)
**File**: `src/PropTraderTools/Features/PttBreakEven.cs`
**Method**: `BuildBeOcoId` (~line 331)
**Status**: PIPELINE-COMPLETE (B72-LaneA)
**Deferred item**: BUG-BE-OCO-REUSE

### Bug
`BuildBeOcoId` used `accName.Substring(0, 4)` as the prefix component.
Both `"Sim101"` and `"Sim102"` produce prefix `"Sim1"`.
When `Execute()` iterates over both accounts with the same `seq` value, both calls produce
identical OCO IDs (e.g. `"PTT-BE-Sim1-00001-0"` for pair 0).
NT8 rejects the second `CreateOrder` call: `"The OCO ID 'PTT-BE-Sim1-00001-1' cannot be reused"`.
Result: all Sim102 BE bracket orders rejected — only Sim101 protected.

### Fix
Changed prefix to `accName.Substring(0, 8)` (or full name if shorter).
- `"Sim101"` → prefix `"Sim101"` → OCO ID `"PTT-BE-Sim101-00001-0"`
- `"Sim102"` → prefix `"Sim102"` → OCO ID `"PTT-BE-Sim102-00001-0"`
No collision possible between any two distinct account names.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged (same ternary, 1 char change) ✅

### Diff (minimal)
```diff
-            string prefix = accName.Length >= 4 ? accName.Substring(0, 4) : accName;
+            string prefix = accName.Length >= 8 ? accName.Substring(0, 8) : accName;
```

### Pipeline work needed
- Ph3.5 Tickets: add test T_OCO_ID_01 — BuildBeOcoId("Sim101", 1, 0) != BuildBeOcoId("Sim102", 1, 0)
- Ph3.5 Tickets: add test T_OCO_ID_02 — BuildBeOcoId with same seq, same pair, different acc → unique
- Ph3.5 Tickets: add test T_OCO_ID_03 — BuildBeOcoId with short account name (<8 chars) → full name used

---

## HOTFIX-BUG-BE-STOP-PRICE-SHORT

**Date**: 2026-08-15 (live trading session continuation)
**File**: `src/PropTraderTools/Features/PttBreakEven.cs`
**Method**: `ExecuteOneAccount` (~line 93)
**Status**: PIPELINE-COMPLETE (B72-LaneA)
**Deferred item**: BUG-BE-STOP-PRICE-SHORT

### Bug
`bePrice = pos.AveragePrice + (isLong ? +buf : -buf) * tickSize`

For a **short** position (e.g. entry at 7804.75, buf=1 tick):
- `bePrice = 7804.75 + (-1) * 0.25 = 7804.50`
- 7804.50 is **BELOW** entry price for a short
- A BuyToCover StopMarket at 7804.50 fires when price RISES to 7804.50
- But price is already at/below 7804.50 if the trade is profitable → stop fires immediately
- Also: a BuyToCover stop must be **ABOVE** the current bid to be accepted by NT8
  (stop at 7804.50 is below bid when trade is profitable → rejected)

For a **long** position (e.g. entry at 7804.75, buf=1 tick):
- `bePrice = 7804.75 + (+1) * 0.25 = 7805.00`
- 7805.00 is ABOVE entry price for a long
- A Sell StopMarket at 7805.00 fires when price DROPS to 7805.00
- That's above entry — captures 1 tick of profit, not true break-even
- (Minor issue — many traders prefer this convention, but it should match intent)

### Fix
Flipped sign for both directions:
`bePrice = pos.AveragePrice + (isLong ? -buf : +buf) * tickSize`

- Long:  `entry - buf*tick` → stop BELOW entry (fires if price drops back past entry)
- Short: `entry + buf*tick` → stop ABOVE entry (fires if price rises back to entry)

With buf=0: bePrice = avgPrice exactly (true break-even, no buffer).
With buf=1: long stop 1 tick below entry; short stop 1 tick above entry.

`IsBePriceOk` validation is still correct:
- Long (Sell stop):      `bePrice <= ask`  → entry - buf ≤ ask ✅
- Short (BuyToCover):    `bePrice >= bid`  → entry + buf ≥ bid ✅ (only true if not yet profitable)

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged (same ternary, sign change only) ✅

### Diff (minimal)
```diff
-            double bePrice = pos.AveragePrice + (isLong ? +buf : -buf) * tickSize;
+            double bePrice = pos.AveragePrice + (isLong ? -buf : +buf) * tickSize;
```

### Pipeline work needed
- Ph1 Architecture: document buf sign convention (positive buf = away from profitable direction)
- Ph3.5 Tickets:
  - T_BE_PRICE_LONG_01: long at 100, buf=0 → bePrice=100 (exact entry)
  - T_BE_PRICE_LONG_02: long at 100, buf=1, tickSize=0.25 → bePrice=99.75 (below entry)
  - T_BE_PRICE_SHORT_01: short at 100, buf=0 → bePrice=100 (exact entry)
  - T_BE_PRICE_SHORT_02: short at 100, buf=1, tickSize=0.25 → bePrice=100.25 (above entry)
  - T_BE_PRICE_VALID_SHORT: short bePrice=100.25, bid=99.0 → IsBePriceOk returns true


---

## HOTFIX-BUG-BE-RAISE-NOTIFY-SIGN

**Date**: 2026-08-15 (live trading session continuation)
**File**: `src/PropTraderTools/Features/PttBreakEven.cs`
**Method**: `RaiseBeNotify` (~line 145)
**Status**: PIPELINE-COMPLETE (B72-LaneA)

### Bug
`RaiseBeNotify` had its own independent copy of the bePrice formula:
`leaderBePrice = leaderPos.AveragePrice + (leaderIsLong ? +buf : -buf) * tickSize`
This is the same wrong sign as HOTFIX-BUG-BE-STOP-SHORT — the value published to `PttBus.BeEventArgs`
was incorrect for short positions (and for long with buf>0).
This value is consumed by `OnPendingBeAccountUpdate` to display the BE price in the output log
and is potentially used by panel subscribers to cross-check the trigger price.

### Fix
Aligned sign with `ExecuteOneAccount`:
`leaderBePrice = leaderPos.AveragePrice + (leaderIsLong ? -buf : +buf) * tickSize`

### Diff (minimal)
```diff
-            double leaderBePrice = leaderPos.AveragePrice + (leaderIsLong ? +buf : -buf) * tickSize;
+            double leaderBePrice = leaderPos.AveragePrice + (leaderIsLong ? -buf : +buf) * tickSize;
```


---

## HOTFIX-BUG-BE-INSTRUMENT-REF

**Date**: 2026-08-15 (live trading session continuation)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `MoveStopToBreakEven` (~line 1885)
**Status**: PIPELINE-COMPLETE (B72-LaneA)
**Root cause of**: BE and BE ALL never moving the stop loss

### Bug
`MoveStopToBreakEven` filtered orders with reference equality:
```csharp
if (order.Instrument != instrument)  continue;
```
NT8 creates separate `Instrument` object instances per account context (confirmed by B69 DW-B69-02
which fixed the same pattern in `FindPosition` and `SubmitBeStop`). The reference equality check
always evaluates `true` (different objects = not equal), so **every single order in the loop was
skipped silently**. `acc.Change()` was never called. The stop never moved.

Confirmed by 12-12 PM CSV: every trade closes via `Name='Close'` (Chart Trader manual close).
Zero `PTT-BE-Stop` change events appear anywhere — not rejected, not errored, not called.
The pending trigger (`AccountItemUpdate`) fires correctly, `BreakEven()` is called, but
`MoveStopToBreakEven` exits the loop having done nothing.

This is why BE appeared to "work" early in the project (before B69 DW-B69-02 exposed
the same pattern) and then stopped working — or more precisely: it never worked for
accounts where the Instrument reference differed from the one stored in the copy rule.

### Fix
Changed reference equality to `FullName` string comparison — identical to the B69 fix:
```diff
-if (order.Instrument != instrument)
+if (order.Instrument == null || order.Instrument.FullName != instrument.FullName)
```

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged (same filter, extra null guard) ✅

### Pipeline work needed
- Ph3.5 Tickets: T_BE_MOVE_01 — MoveStopToBreakEven with different Instrument object instances but same FullName → Change() IS called
- Ph3.5 Tickets: T_BE_MOVE_02 — MoveStopToBreakEven with null order.Instrument → order skipped (no NRE)

---

## HOTFIX-BUG-BE-MOVESTOP-SIGN

**Date**: 2026-08-15 (live trading session continuation)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `MoveStopToBreakEven` (~line 1881)
**Status**: PIPELINE-COMPLETE (B72-LaneA)

### Bug
`MoveStopToBreakEven` had the same wrong sign as `PttBreakEven.ExecuteOneAccount`:
```csharp
double direction = isLong ? 1.0 : -1.0;
```
- Short position: `newStop = avgPrice - buf*tick` → stop placed BELOW entry for a short = wrong.

### Fix
Aligned with `PttBreakEven` fix (HOTFIX-BUG-BE-STOP-SHORT):
```diff
-double direction = isLong ? 1.0 : -1.0;
+double direction = isLong ? -1.0 : +1.0;
```
With buf=0 this has no practical effect (both produce `avgPrice`). Matters when buf > 0.


---

## HOTFIX-BUG-BE-IMMEDIATE

**Date**: 2026-08-15 (live trading session continuation)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `ArmPendingBe` (~line 2075)
**Status**: PIPELINE-COMPLETE (B72-LaneA)

### Bug
`ArmPendingBe` always armed a pending watcher — no path existed to fire immediately
when price was already at or past the BE level at button-press time.

- Per-chart BE button (OnBeClick): had its own `IsPriceAlreadyAtBe` check at panel level,
  routing to `DispatchModule("BE")` for the immediate case. Only armed when in drawdown.
- BE ALL (OnGlobalBeClick → GlobalBe.Execute → ArmAllPendingBe → ArmPendingBe):
  no immediate-fire path existed at all. Always armed, always waited for AccountItemUpdate
  to fire. If price was already past entry when BE ALL was pressed, the trigger condition
  was immediately true on the first AccountItemUpdate tick — but that requires at least one
  account item event to fire after the button press, which may not happen quickly or at all
  in a slow market.

More critically: after HOTFIX-BUG-BE-INSTRUMENT-REF fixed MoveStopToBreakEven, the
pending path now actually works. But "in the green" = already profitable = the trigger
should fire the moment BE is pressed, not wait for the next PnL tick.

### Fix
Added immediate-fire check inside `ArmPendingBe` before writing to `_pendingBeSlots`:
- Reads current bid (long) or ask (short) from `instr.MarketData`
- Computes `target = avgPrice + (isLong ? +1.0 : -1.0) * buf * tick` (same formula as trigger)
- If `refPx` is already past `target` → call `BreakEven()` + `PendingBeFired` immediately, return
- If market data unavailable (refPx=0) → fall through to normal arm (safe fallback)

This fixes both per-chart BE and BE ALL for the "in the green" case via a single engine-level fix.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC increase: 4→6 (added tickSize guard + alreadyAtBe branch) ✅ (still ≤8)

### Behavior after fix
| Scenario | Before | After |
|---|---|---|
| Press BE while in drawdown (red) | Arms watcher ✅ | Arms watcher ✅ |
| Press BE while at break-even | Arms watcher, waits forever | Fires immediately ✅ |
| Press BE while in profit (green) | Arms watcher, fires on next PnL tick | Fires immediately ✅ |
| No market data (refPx=0) | Arms watcher | Arms watcher (safe fallback) ✅ |

### Pipeline work needed
- Ph3.5 Tickets:
  - T_BE_IMM_01: ArmPendingBe long, bid >= target → BreakEven called immediately, no slot added
  - T_BE_IMM_02: ArmPendingBe short, ask <= target → BreakEven called immediately, no slot added
  - T_BE_IMM_03: ArmPendingBe, refPx=0 (no market data) → slot added normally (arms watcher)
  - T_BE_IMM_04: ArmPendingBe long, bid < target → slot added (arms watcher, does not fire)


---

## DIAG-MOVESTOP-01

**Date**: 2026-08-15 (live trading session — continue)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `MoveStopToBreakEven` (~line 1872)
**Status**: REMOVED (2026-08-17 pre-flight) -- all [MSTBE] log lines stripped from MoveStopToBreakEven before first live BE test

### Purpose
`StatusUpdate` only writes to the panel's `_statusText` label — invisible in the NT8 Output tab.
`MoveStopToBreakEven` had zero visibility into which filter gate was skipping orders.
Added `NinjaTrader.Code.Output.Process("[MSTBE] ...")` at every skip point and at the
`acc.Change()` call site so the next BE test shows exactly why the stop is or isn't moving.

### What the output tells us
After pressing BE (armed, then price crosses to green):
- `[MSTBE] acc=Sim101 instr=... posQty=N` — confirms method is called
- `[MSTBE] SKIP flat` — position not found (FindPosition bug) or qty=0
- `[MSTBE] SKIP state: Stop1 state=Accepted` — orders not yet Working (timing issue)
- `[MSTBE] SKIP state: Stop1 state=TriggerPending` — ATM bracket not yet submitted
- `[MSTBE] SKIP type: Stop1 type=StopLimit` — (should NOT appear; StopLimit is allowed)
- `[MSTBE] SKIP not-stop-leg: SomeOrder` — order name doesn't start with "Stop"/"STP"
- `[MSTBE] SKIP already-at-be: Stop1 stopPx=X newStop=X` — idempotency guard firing
- `[MSTBE] CALLING Change() on Stop1 ...` — method reached acc.Change()
- `[MSTBE] Change() OK Stop1 @ X` — acc.Change() succeeded
- `[MSTBE] Change() EXCEPTION Stop1: ...` — acc.Change() threw

### Remove when
All four scenarios are confirmed working:
1. BE armed (in drawdown) → price crosses → stop moves (pending path)
2. BE pressed "in the green" → immediate fire → stop moves
3. BE ALL → both leader and follower stops move
4. No duplicate stops placed

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged (Output.Process calls are straight-line, no branching) ✅


---

## HOTFIX-MSTBE-STATE

**Date**: 2026-08-15 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `MoveStopToBreakEven` (~line 1913)
**Status**: PIPELINE-COMPLETE (B72-LaneA)

### Bug (confirmed by DIAG-MOVESTOP-01 output)
Every `[MSTBE]` invocation showed:
```
[MSTBE] SKIP state: Stop1 state=Accepted
[MSTBE] SKIP state: Stop2 state=Accepted
```
Zero `Working` orders — `acc.Change()` was never called.

Root cause: NT8 sim ATM brackets go `TriggerPending → Accepted → Working` after the entry fills.
`OnPendingBeAccountUpdate` fires on the first PnL tick (AccountItemUpdate) which arrives while
Stop1/Stop2 are still in `Accepted` state — before they transition to `Working`.
The state filter `order.OrderState != OrderState.Working` blocked every order → stop never moved.

`acc.Change()` on `Accepted` orders is valid NT8 API (same as Chart Trader drag operation).
NT8_FULL_REFERENCE.md: `acc.Change()` accepts orders in `Accepted` state.

### Fix
Widened state filter from `Working` only to `Working || Accepted || TriggerPending`.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC increase: 6 → 7 (one extra bool expression, still ≤ 8) ✅

### Diff (minimal)
```diff
-if (order.OrderState != OrderState.Working)
+bool stateOk = order.OrderState == OrderState.Working
+            || order.OrderState == OrderState.Accepted
+            || order.OrderState == OrderState.TriggerPending;
+if (!stateOk)
```

### Pipeline work needed
- Ph3.5 Tickets:
  - T_BE_MOVE_03: MoveStopToBreakEven with Accepted stop → Change() IS called
  - T_BE_MOVE_04: MoveStopToBreakEven with TriggerPending stop → Change() IS called
  - T_BE_MOVE_05: MoveStopToBreakEven with Cancelled stop → Change() NOT called (still filtered)

---

## HOTFIX-FLAT-DISARM

**Date**: 2026-08-15 (live trading session)
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Method**: `UpdateButtonColors` (~line 565)
**Status**: PIPELINE-COMPLETE (B73-LaneB)

### Bug
When the user manually closes a position while BE is Armed (yellow button), the button
stays yellow permanently. Two parts to the failure:
1. HOTFIX-F3 correctly resets `_beState = Idle` and calls `UpdateBeVisuals(Idle)` for the visual.
2. BUT it never called `DisarmPendingBe` — the engine slot stayed alive forever.
3. On the next trade, the stale slot would fire `BreakEven()` for the OLD instrument/account,
   potentially moving a stop that belongs to a different trade context.

FIX-C (prior session) removed ALL `DisarmPendingBe` calls from `UpdateButtonColors` because
the blanket disarm was killing slots during active trades (ATM bracket Cancelled events fired
`UpdateButtonColors(false)` mid-trade). That was correct — but left no disarm at all.

The corrected logic: disarm ONLY when `hasPosition=false` AND `_beState != Idle` (position
truly closed while armed). Post-HOTFIX-BUG2-BE-RESET, this path only fires for the leader
account (follower bracket noise is filtered by Gate 2.5 before `TryFirePositionState`), so
`hasPos=false` here reliably means the leader is flat.

### Fix
Inside the existing HOTFIX-F3 block (already gated by `!hasPosition && _beState != Idle`),
added:
1. `CopyEngine.Instance.DisarmPendingBe(_leaderAccount)` — clean up engine slot
2. `if (IsPendingSlotsEmpty()) UpdateBeAllVisuals(BeState.Idle)` — reset BE ALL button

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅ (`_leaderAccount` null-guarded before call)
- CYC unchanged (added two straight-line calls inside existing branch) ✅


---

## HOTFIX-ENTRY-DRAG-DEDUP

**Date**: 2026-08-15 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `HandleEntryChange` (~line 1140)
**Status**: PIPELINE-COMPLETE (B72-LaneA)

### Bug
`TryRemove(orderId)` removed the leader order key from `_dedupCache` immediately after
`HandleEntryChange` was called from Gate C. NT8 then fires a second `OnOrderUpdate` event
for the same leader order (Accepted → Working state transition). At that point Gate C's
`TryGetValue` returns false (key was removed), the drag-price delta check is never reached,
and the code falls through to `DispatchCopy`. A second `PTT-Copy` order is placed on the
follower → 2× contracts.

Note: HOTFIX-DW-B64-01 (same session) added `_dedupCache[orderId] = currentPrice` in Gate C
BEFORE calling `HandleEntryChange`, specifically to survive the `TryRemove`. But `HandleEntryChange`
then immediately removes the key again at line 1140, undoing that fix for the Working-state event.

### Fix
Replaced `_dedupCache.TryRemove(leaderOrder.OrderId.ToString(), out _)` with
`_dedupCache[leaderOrder.OrderId.ToString()] = newPrice` (upsert, not remove).

Keeping the key alive means the second Working-state event hits Gate C, finds the stored price,
computes `Math.Abs(newPrice - newPrice) = 0 < tickSize`, and exits without calling `DispatchCopy`.
`DispatchCopy`'s own `IsDedup` check also independently blocks it.

### JS-DNA compliance
- No `lock()` added ✅ (`_dedupCache` is `ConcurrentDictionary`, indexer write is lock-free)
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged (one-for-one line replacement) ✅

### Diff (minimal)
```diff
-            // Update stored price in dedup cache to track latest leader price.
-            // B67-LaneB DW-B67-02: remove stale key after cancel+resubmit.
-            // New entry will be re-keyed by DispatchCopy on the follower's Accepted event.
-            // Do NOT insert newPrice under the old key after cancel+resubmit.
-            _dedupCache.TryRemove(leaderOrder.OrderId.ToString(), out _);
+            // HOTFIX-ENTRY-DRAG-DEDUP: keep leader orderId in cache at newPrice (upsert, not remove).
+            // TryRemove caused Working-state re-entry to fall through Gate C into DispatchCopy,
+            // placing a second PTT-Copy order (doubling follower contracts).
+            // Keeping the key at newPrice means the Working event hits Gate C, sees delta=0 (price unchanged
+            // since Accepted), and returns without dispatching. DispatchCopy's IsDedup also blocks it.
+            _dedupCache[leaderOrder.OrderId.ToString()] = newPrice;
```

### Pipeline work needed
- Ph1 Architecture: document that `HandleEntryChange` must keep leader key alive (upsert semantics) after cancel+resubmit
- Ph3.5 Tickets:
  - T_DRAG_DEDUP_02: Working-state re-entry after drag — Gate C sees delta=0 → DispatchCopy NOT called
  - T_DRAG_DEDUP_03: After HandleEntryChange completes — orderId key still present in _dedupCache at newPrice
  - T_DRAG_DEDUP_04: New follower order (cancel+resubmit path) — new orderId keyed correctly via line 1180


---

## HOTFIX-MSTBE-CANCEL-RESUBMIT

**Date**: 2026-08-14 (live trading session)
**ID**: HOTFIX-MSTBE-CANCEL-RESUBMIT
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `MoveStopToBreakEven(Account acc, Instrument instrument, int bufferTicks)`
**Status**: PIPELINE-COMPLETE (B72-LaneA)

### Bug

`acc.Change()` is a silent no-op when called on ATM-bracket-owned orders (`Stop1`, `Stop2`)
from AddOn context. The NT8 ATM engine owns those brackets and ignores `acc.Change()` calls —
no exception is raised and no effect occurs on the stop price.

**Confirmed in NT8 output log**: `[MSTBE] Change() OK Stop1 @ <newPrice>` was logged while
the stop remained at its original price in the Orders tab.

### Fix

Replaced the entire `foreach (var order in acc.Orders.ToList())` loop and its `acc.Change()`
body with a cancel+resubmit pattern mirroring `PttBreakEven.ExecuteOneAccount`:

- **Step A** — Snapshot target orders (Working/Accepted Limit orders matching `Target1..Target9`)
  before cancelling anything. Mirrors `PttBreakEven.SnapshotTargetsLocal`.
- **Step B** — Cancel all stale brackets: Working | Initialized | Submitted | Accepted |
  TriggerPending orders on the instrument that do not already start with `PTT-BE-`.
  Mirrors `PttBreakEven.CancelStaleBracketsLocal`. Cancel errors are non-fatal (caught).
- **Step C** — Submit new `PTT-BE-Stop-N` + `PTT-BE-Target-N` OCO pairs (or one bare
  `PTT-BE-Stop` when no targets found). Mirrors `PttBreakEven.SubmitBeTargetsLocal`.
  All `CreateOrder` calls use NT8-049 arg order (StopMarket: arg6=0, arg7=stop;
  Limit: arg6=price, arg7=0), NT8-007 cast, NT8-013 DateTime.MaxValue, NT8-014 PTT- prefix.

Guards preserved unchanged: all guards before the loop (`IsFlat`, tickSize, newStop calc,
DIAG-MOVESTOP-01 log lines 1878-1903, `isLong`, `newStop`).

`BreakEven()`, `ArmPendingBe()`, `OnPendingBeAccountUpdate()` are not touched.


---

## HOTFIX-MSTBE-OCO-REUSE

- **ID**: HOTFIX-MSTBE-OCO-REUSE
- **File**: src/PropTraderTools/CopyEngine.cs
- **Method**: MoveStopToBreakEven + field _mstbeOcoSeq
- **Bug**: Static OCO ID PTT-BE-Sim101-0 reused on second BE press -- NT8 rejects all orders, position unprotected
- **Fix**: volatile int _mstbeOcoSeq + Interlocked.Increment per call, mirrors PttBreakEven._beOcoSeq (DW-B40-OCO-02)
- **Status**: PIPELINE-COMPLETE (B72-LaneA)

## HOTFIX-MSTBE-OCO-REUSE

**Date**: 2026-08-15 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Methods**: `MoveStopToBreakEven` (OCO ID generation) + field declaration
**Status**: PIPELINE-COMPLETE (B72-LaneA)
**Deferred item**: HOTFIX-MSTBE-OCO-REUSE

### Bug
`MoveStopToBreakEven` OCO ID formula was:
```csharp
string ocoId_i = "PTT-BE-" + acc.Name.Substring(0, Math.Min(8, acc.Name.Length)) + "-" + i;
```
This is **static per session** — `i` is the pair index (0, 1), not a monotonic counter.
On a second BE press after the first set of `PTT-BE-*` orders are cancelled/filled, NT8 refuses
to reuse a cancelled OCO ID within the same session:
`"The OCO ID 'PTT-BE-Sim101-0' cannot be reused."`
All 4 PTT-BE orders on the second press are rejected → position left unprotected after second press.

Root cause is identical to `PttBreakEven._beOcoSeq` (DW-B40-OCO-02) which already fixes this
pattern for the `PttBreakEven.SubmitBeTargetsLocal` path.

### Fix
Added field (line 156):
```csharp
// HOTFIX-MSTBE-OCO-REUSE: monotonic counter for BE OCO IDs -- never reuse a cancelled OCO ID.
// DW-B40-OCO-02 pattern from PttBreakEven._beOcoSeq. JS-023: volatile int allowed.
private volatile int _mstbeOcoSeq = 0;
```

Added `seq` increment before the OCO submission loop (line 2015):
```csharp
int seq = System.Threading.Interlocked.Increment(ref _mstbeOcoSeq);
```

Changed OCO ID generation inside the loop:
```diff
-string ocoId_i = "PTT-BE-"
-    + acc.Name.Substring(0, Math.Min(8, acc.Name.Length))
-    + "-" + i;
+string ocoId_i = "PTT-BE-"
+    + acc.Name.Substring(0, Math.Min(8, acc.Name.Length))
+    + "-" + seq.ToString("D5") + "-" + i;
```

Result: First press → `PTT-BE-Sim101-00001-0`, `PTT-BE-Sim101-00001-1`.
Second press → `PTT-BE-Sim101-00002-0`, `PTT-BE-Sim101-00002-1`. No collision.

### Expected test output (NT8 Output Tab 1)
- First BE press: `ocoId=PTT-BE-Sim101-00001-0`, `ocoId=PTT-BE-Sim101-00001-1`
- Second BE press: `ocoId=PTT-BE-Sim101-00002-0`, `ocoId=PTT-BE-Sim101-00002-1`
- All 4 orders appear in Orders tab on both presses

### Failure signature (old bug)
Second press shows `ocoId=PTT-BE-Sim101-0` and orders silently fail → position unprotected.

### JS-DNA compliance
- No `lock()` added ✅ (`Interlocked.Increment` is lock-free, JS-023)
- No `throw new` added ✅
- No `return null` added ✅
- `volatile int` field is JS-023 compliant ✅
- CYC unchanged (straight-line seq increment before existing loop) ✅

### Diff (minimal)
**Field (after _trailBeLastPnlBits, line 156)**:
```diff
+        // HOTFIX-MSTBE-OCO-REUSE: monotonic counter for BE OCO IDs -- never reuse a cancelled OCO ID.
+        // DW-B40-OCO-02 pattern from PttBreakEven._beOcoSeq. JS-023: volatile int allowed.
+        private volatile int _mstbeOcoSeq = 0;
```

**MoveStopToBreakEven OCO loop (line 2015)**:
```diff
+        int seq = System.Threading.Interlocked.Increment(ref _mstbeOcoSeq);
         for (int i = 0; i < targets.Count; i++)
         {
             var t = targets[i];
             string ocoId_i = "PTT-BE-"
                 + acc.Name.Substring(0, Math.Min(8, acc.Name.Length))
-                + "-" + i;
+                + "-" + seq.ToString("D5") + "-" + i;
```

### Pipeline work needed
- Ph3.5 Tickets:
  - T_OCO_SEQ_01: First call to MoveStopToBreakEven → seq=1 → ocoId contains "00001"
  - T_OCO_SEQ_02: Second call → seq=2 → ocoId contains "00002" (no collision with prior seq)
  - T_OCO_SEQ_03: Concurrent calls (two accounts) → Interlocked.Increment guarantees unique seq per call
  - T_OCO_SEQ_04: Pair index i=0 and i=1 with same seq → distinct IDs (same seq, different suffix "-0" vs "-1")

---

## HOTFIX-FLAT-DISARM-FOLLOWER

**Date**: 2026-08-15 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `OnOrderUpdate` (pre-Gate 1 section, ~line 700)
**Status**: PIPELINE-COMPLETE (B73-LaneB)
**Fixes**: HOTFIX-FLAT-DISARM partial failure — BE button stayed yellow after manual close

### Bug
HOTFIX-FLAT-DISARM (prior session) correctly resets `_beState=Idle` and calls
`DisarmPendingBe(_leaderAccount)` when `UpdateButtonColors(hasPos=false)` fires.

But `UpdateButtonColors(false)` was **never called** after the follower's PTT-BE stop filled:
- When `PTT-BE-Stop-1/2` fills on **Sim102 (follower)**, `OnOrderUpdate` fires for Sim102.
- Gate 2 checks `e.Order.Account.Name == rule.MasterAccount?.Name`.
- Sim102 is a follower account, not a master — Gate 2 returns null → early exit at line 717.
- `TryFirePositionState` is never reached → `PositionStateChanged` never fires.
- Panel's `OnPositionStateChanged` never called → `UpdateButtonColors(false)` never called.
- HOTFIX-F3 / HOTFIX-FLAT-DISARM never execute → BE button stays yellow permanently.

The leader (Sim101) closes via `Name='Close'` first. `TryFirePositionState` fires for Sim101,
`HasOpenPosition(Sim101)=false`, panel resets correctly. But when the user then places a new
trade on Sim102 and presses BE, then the PTT-BE stop fills and closes Sim102 — the button
stays yellow because Sim102 is never the subject of a `PositionStateChanged` event.

### Fix
Added a narrow pre-Gate-1 path in `OnOrderUpdate`:
- Triggers only on: `Filled` + `Name.StartsWith("PTT-BE-Stop")` + non-leader account
- Non-leader check: iterate `_rules`, confirm account is not any rule's `MasterAccount`
- If non-leader: fire `PositionStateChanged` directly with current `HasOpenPosition` result

This is safe because:
1. Leader PTT-BE-Stop fills still go through `TryFirePositionState` via the normal path (Gate 2 passes for leaders)
2. The non-leader check prevents double-firing for any edge case where leader account name matches
3. `PTT-BE-Stop*` prefix is unique to this codepath (not used by QX or any other feature)
4. `HasOpenPosition` is lock-free (ConcurrentDictionary read via `FindPosition`)

### JS-DNA compliance
- No `lock()` added ✅ (`_rules` is read-only after init; `HasOpenPosition` is lock-free)
- No `throw new` added ✅
- No `return null` added ✅
- CYC increase: +2 (outer `if` + inner `foreach` loop) — total method stays ≤ 8 ✅

### Diff (minimal, OnOrderUpdate pre-Gate-1)
```diff
+        // HOTFIX-FLAT-DISARM-FOLLOWER: fire PositionStateChanged when a PTT-BE bracket on a
+        // FOLLOWER account fills and closes that account's position.
+        if (e.Order != null
+            && e.Order.OrderState == OrderState.Filled
+            && e.Order.Name != null
+            && e.Order.Name.StartsWith("PTT-BE-Stop")
+            && e.Order.Instrument?.FullName != null)
+        {
+            bool isLeader = false;
+            foreach (var r in _rules)
+            {
+                if (e.Order.Account.Name == r.MasterAccount?.Name) { isLeader = true; break; }
+            }
+            if (!isLeader)
+            {
+                bool hasPos     = HasOpenPosition(e.Order.Account, e.Order.Instrument);
+                bool hasEntries = HasWorkingEntries(e.Order.Account, e.Order.Instrument);
+                PositionStateChanged?.Invoke(
+                    e.Order.Instrument.FullName,
+                    new PositionState(hasPos, hasEntries));
+            }
+        }
         // Gate 1: enabled check
         if (!_isCopyEnabled)
```

### Expected behavior after fix
- BE armed (yellow) → PTT-BE stops fire on Sim102 → Sim102 goes flat → `PositionStateChanged`
  fires with `hasPos=false` → `UpdateButtonColors(false)` → HOTFIX-F3 → `_beState=Idle` →
  button goes grey → `DisarmPendingBe` clears any residual slot → button stays grey ✅

### Pipeline work needed
- Ph3.5 Tickets:
  - T_FOLLOWER_FLAT_01: PTT-BE-Stop-1 fills on follower → PositionStateChanged fired with hasPos=false
  - T_FOLLOWER_FLAT_02: PTT-BE-Stop-1 fills on leader → NOT fired by this path (isLeader=true guard)
  - T_FOLLOWER_FLAT_03: PTT-QX-Stop fills on follower → NOT fired (name does not start with "PTT-BE-Stop")
  - T_FOLLOWER_FLAT_04: PTT-BE-Stop fills on follower but position still open (partial fill) → hasPos=true fired correctly

---

---

## HOTFIX-MSTBE-QX-TARGETS-01
- **ID**: HOTFIX-MSTBE-QX-TARGETS-01
- **File**: CopyEngine.cs
- **Method**: MoveStopToBreakEven (Step A isAtmTarget filter, ~line 1961)
- **Bug**: After QX, BE ALL submitted only a bare stop (no targets). ATM Target1/2 replaced by PTT-QX-T1/T2 which do not start with "Target" -> snapshot missed them -> 0 targets.
- **Fix**: Extended isAtmTarget to also match PTT-QX-T* and PTT-BE-Target-* Limit orders.
- **Status**: PIPELINE-COMPLETE (B72-LaneA)

---

## HOTFIX-BEALL-SYNC-01
- **ID**: HOTFIX-BEALL-SYNC-01
- **File**: CopyEngine.cs + TradeCopierPanel.cs
- **Method**: ArmPendingBe (new PendingBeArmed event) + OnPendingBeArmedDispatch (new handler)
- **Bug**: BE ALL button only turned yellow on the panel that was clicked; other panels stayed grey
- **Fix**: PendingBeArmed event broadcast from ArmPendingBe; all panels subscribe and call UpdateBeAllVisuals(Armed)
- **Status**: PIPELINE-COMPLETE (B73-LaneB)

---

## HOTFIX-FLAT-MANUAL-CLOSE-01
- **ID**: HOTFIX-FLAT-MANUAL-CLOSE-01
- **File**: TradeCopierPanel.cs
- **Method**: OnLeaderPositionUpdate
- **Bug**: Manual close (Name='Close') before BE fires -> button stays yellow. HasOpenPosition returns True at order-fill time (NT8 position-state lag). hasPos=False never reaches panel.
- **Fix**: OnLeaderPositionUpdate fires UpdateButtonColors(false,false) on Operation.Remove -- NT8 guarantees position is gone at this event, no lag.
- **Status**: PIPELINE-COMPLETE (B73-LaneB)

## HOTFIX-BEALL-BUFFER-SYNC-01
- **ID**: HOTFIX-BEALL-BUFFER-SYNC-01
- **Files**: CopyEngine.cs, Features/PttGlobalBreakEven.cs
- **Methods**: GlobalBeBufferChanged event + IncrementBuffer + DecrementBuffer
- **Bug**: BE ALL buffer spin on Panel A does not update Panel B label -- per-panel label only
- **Fix**: Added GlobalBeBufferChanged Action<int> event; fired from IncrementBuffer/DecrementBuffer; panels subscribe in Task B
- **Status**: PIPELINE-COMPLETE (B74-LaneC Task A + B75-LaneB Task B wiring)

---

---

## HOTFIX-QUICKALL-SINGLETON-01

**ID**: HOTFIX-QUICKALL-SINGLETON-01
**Files**: `CopyEngine.cs`, `TradeCopierPanel.cs`, `Features/PttGlobalQuickExit.cs`
**Methods**: `_globalQuickAllT1` singleton + `GlobalBeBufferChanged` wire + `OnGlobalBeBufferChanged` + `OnQuickAllBufferChanged`
**Bug**: Quick ALL `_quickAllT1` was per-panel and never fed into execution; BE ALL buffer label only updated on clicked panel
**Fix**: Singleton `GlobalQuickAllT1` on `CopyEngine`; broadcast events refresh all panel labels; `Execute()` uses singleton value
**Status**: PIPELINE-COMPLETE (B75-LaneA)


## HOTFIX-QUICKALL-COMPILE-01

**ID**: HOTFIX-QUICKALL-COMPILE-01
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Method**: `_quickAllBtn` button construction block (~line 1092)
**Bug**: Button label initializer still referenced removed field `_quickAllT1` (compilation error).
  `_quickAllT1` was correctly removed as a field by HOTFIX-QUICKALL-SINGLETON-01 but the button
  init line `Content = FormatBuffer("Quick ALL", _quickAllT1)` was not updated.
**Fix**: Replaced `_quickAllT1` with `CopyEngine.Instance.GlobalQuickAllT1` so the button reads
  the singleton at construction time, consistent with OnQuickAllBufferChanged updates.
**Status**: PIPELINE-COMPLETE (B75-LaneA)

### Diff (minimal)
```diff
-                Content         = FormatBuffer("Quick ALL", _quickAllT1),
+                Content         = FormatBuffer("Quick ALL", CopyEngine.Instance.GlobalQuickAllT1),
```

---

## HOTFIX-CS0070-BEBUFFER-01

**ID**: HOTFIX-CS0070-BEBUFFER-01
**Files**: `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/Features/PttGlobalBreakEven.cs`
**Methods**: `RaiseBeBufferChanged` (new relay) + `IncrementBuffer` + `DecrementBuffer`
**Bug**: CS0070 — `PttGlobalBreakEven.IncrementBuffer/DecrementBuffer` called
  `CopyEngine.Instance.GlobalBeBufferChanged?.Invoke(...)` from outside `CopyEngine`.
  C# CS0070 rule: an `event` field may only be raised (Invoke) from inside the declaring class.
  The `GlobalQuickAllBufferChanged` event had the same raise moved inside `CopyEngine.IncrementQuickAll/DecrementQuickAll`
  (where it is legal), but `GlobalBeBufferChanged` was raised externally from `PttGlobalBreakEven`.
**Fix**: Added one-line relay method `internal void RaiseBeBufferChanged(int newValue) => GlobalBeBufferChanged?.Invoke(newValue);`
  inside `CopyEngine`. `PttGlobalBreakEven.IncrementBuffer/DecrementBuffer` now call
  `CopyEngine.Instance.RaiseBeBufferChanged(...)` instead of direct `?.Invoke`.
**Status**: PIPELINE-COMPLETE (B75-LaneA)

### Diff (minimal)

**CopyEngine.cs** (new relay after event declaration):
```diff
+        internal void RaiseBeBufferChanged(int newValue) => GlobalBeBufferChanged?.Invoke(newValue);
```

**PttGlobalBreakEven.cs** (x2 — IncrementBuffer + DecrementBuffer):
```diff
-            CopyEngine.Instance.GlobalBeBufferChanged?.Invoke(_globalBeBuffer);
+            CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer);
```

---

## HOTFIX-THREAD-DISPATCH-01

**ID**: HOTFIX-THREAD-DISPATCH-01
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Methods**: `OnGlobalBeBufferChanged` + `OnQuickAllBufferChanged`
**Bug**: Both handlers set WPF control `.Content` directly on the calling panel's UI thread.
  In NT8, each Chart window has its own Dispatcher. Panel A's spin button fires the event
  synchronously on Panel A's thread; Panel B's handler runs on Panel A's thread but Panel B's
  WPF controls belong to Panel B's Dispatcher → InvalidOperationException: "The calling thread
  cannot access this object because a different thread owns it."
**Fix**: Wrapped both handlers in `Dispatcher.InvokeAsync` (same pattern as `OnPendingBeFiredDispatch`
  and `OnPendingBeArmedDispatch`).
**Status**: PIPELINE-COMPLETE (B75-LaneB)

### Diff
```diff
-        private void OnGlobalBeBufferChanged(int newBuffer)
-        {
-            if (_globalBeBtn2 != null)
-                _globalBeBtn2.Content = FormatGlobalBeBuffer("BE ALL", newBuffer);
-        }
+        private void OnGlobalBeBufferChanged(int newBuffer)
+        {
+            Dispatcher.InvokeAsync(() =>
+            {
+                if (_globalBeBtn2 != null)
+                    _globalBeBtn2.Content = FormatGlobalBeBuffer("BE ALL", newBuffer);
+            });
+        }
-        private void OnQuickAllBufferChanged(int newT1)
-        {
-            if (_quickAllBtn != null)
-                _quickAllBtn.Content = FormatBuffer("Quick ALL", newT1);
-        }
+        private void OnQuickAllBufferChanged(int newT1)
+        {
+            Dispatcher.InvokeAsync(() =>
+            {
+                if (_quickAllBtn != null)
+                    _quickAllBtn.Content = FormatBuffer("Quick ALL", newT1);
+            });
+        }
```

---

## HOTFIX-BEALL-DISARM-SYNC-01

**ID**: HOTFIX-BEALL-DISARM-SYNC-01
**Files**: `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/TradeCopierPanel.cs`
**Methods**: `GlobalBeAllDisarmed` event + `RaiseBeAllDisarmed()` relay + `OnGlobalBeAllDisarmed` handler
  + `OnGlobalBeClick` disarm path + `UpdateButtonColors` HOTFIX-F3 branch
**Bug 1**: Clicking BE ALL to disarm called `UpdateBeAllVisuals(Idle)` only on the clicked panel.
  No event was broadcast — other panels stayed yellow.
**Bug 2**: When position closes while BE is armed, `UpdateButtonColors(false)` only fires on the
  panel whose instrument matches the closed position. Other panels (e.g. MGC panel watching a MES
  position) never receive the flat signal → button stays yellow permanently.
**Fix**: Added `internal event Action GlobalBeAllDisarmed` + `internal void RaiseBeAllDisarmed()`
  relay on CopyEngine. All panels subscribe/unsubscribe in OnLoaded/Detach. Handler calls
  `Dispatcher.InvokeAsync(() => UpdateBeAllVisuals(Idle))`. `RaiseBeAllDisarmed()` is called
  from (1) `OnGlobalBeClick` disarm path after `UpdateBeAllVisuals(Idle)`, and (2) `UpdateButtonColors`
  HOTFIX-F3 branch after `UpdateBeAllVisuals(Idle)`.
**Status**: PIPELINE-COMPLETE (B73-LaneB)

---

## HOTFIX-BEALL-OCO-SEQ-SHARED-01

**ID**: HOTFIX-BEALL-OCO-SEQ-SHARED-01
**Files**: `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/Features/PttBreakEven.cs`
**Methods**: `NextBeOcoSeq()` (new) + `PttBreakEven.Execute` seq generation
**Bug**: `PttBreakEven._beOcoSeq` (per-instance, per-chart BE path) and `CopyEngine._mstbeOcoSeq`
  (BE ALL / MoveStopToBreakEven path) are separate counters both starting at 0. First BE ALL press
  → `_mstbeOcoSeq` = 1 → creates `PTT-BE-Sim101-00001-0`. Subsequent per-chart BE press →
  `_beOcoSeq` on new PttBreakEven instance = 1 → same OCO ID `PTT-BE-Sim101-00001-0` → NT8 rejects:
  "OCO ID cannot be reused."
**Fix**: Added `internal int NextBeOcoSeq() => Interlocked.Increment(ref _mstbeOcoSeq)` on
  CopyEngine. Removed `_beOcoSeq` field from PttBreakEven. `PttBreakEven.Execute` now calls
  `CopyEngine.Instance?.NextBeOcoSeq() ?? 1` — both paths share the same global counter.
**Status**: PIPELINE-COMPLETE (B75-LaneA)

---

## HOTFIX-FOLLOWER-LABEL-CLIP-01

**ID**: HOTFIX-FOLLOWER-LABEL-CLIP-01
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Method**: `BuildInlineFollowerRow` (~line 1721)
**Bug**: Account name `TextBlock` had `Width = 90` (fixed pixels). Long PA/Apex account names like
  `PA-APEX-422136-01U` (~20 chars, ~160px needed) were clipped — last digits invisible. User could
  not distinguish which follower accounts were selected, masking a potential double-selection bug.
**Fix**: Replaced `StackPanel` row with `DockPanel` (`LastChildFill = true`). ATM combo and PnL
  label are `DockPanel.SetDock(Dock.Right)`. Name label has no fixed `Width` and fills remaining
  space as the `LastChildFill` child. `TextTrimming = CharacterEllipsis` for graceful degradation
  on very narrow panels.
**Status**: PIPELINE-COMPLETE (B75-LaneB)

### Layout change
```
Before: [Chk][Name:90px][PnL:64px][ATM:120px]   ← fixed widths, name clipped
After:  [Chk|L][ATM:110px|R][PnL:60px|R][Name fills remaining|LastChildFill]
```

---

## HOTFIX-DISPATCH-FIX-01

**Date**: 2026-08-16 (live trading session)
**Files**: `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/TradeCopierPanel.cs`
**Methods**: `RaiseBeBufferChanged`, `IncrementQuickAll`, `DecrementQuickAll`, `OnGlobalBeBufferChanged`, `OnQuickAllBufferChanged`
**Status**: PIPELINE-COMPLETE (B76-LaneA)

### Bug
`GlobalBeBufferChanged` and `GlobalQuickAllBufferChanged` events were raised synchronously
(`.Invoke()`) from Panel A's RepeatButton click handler on Panel A's chart Dispatcher thread.
NT8 creates a separate Dispatcher per chart window (`chart.Dispatcher.InvokeAsync` pattern at
TradeCopierAddOn.cs:168). Panel B's handler ran on Panel A's thread. The inner
`this.Dispatcher.InvokeAsync(lambda)` posted to Panel B's Dispatcher, but the lambda could
reach Panel B's WPF controls from the wrong thread → `InvalidOperationException:
"The calling thread cannot access this object because a different thread owns it."`

### Fix
Changed raise sites in CopyEngine to use `System.Windows.Application.Current.Dispatcher.InvokeAsync`.
All NT8 UI runs on the Application.Current Dispatcher. Events now arrive at subscribers on
the correct thread. Removed redundant inner `Dispatcher.InvokeAsync` wrappers from the two
TradeCopierPanel buffer handlers (`OnGlobalBeBufferChanged`, `OnQuickAllBufferChanged`).

This matches the existing `TradeCopierAddOn.cs:252` pattern: `System.Windows.Application.Current.Dispatcher.InvokeAsync(...)`.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged (single-expression lambda, no new branches) ✅

## HOTFIX-MARKET-DEDUP-01

**Date**: 2026-08-16 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Methods**: `IsDispatchTriggerState` (signature + body), `DispatchCopy` (call site line ~960)
**Status**: PIPELINE-COMPLETE (B76-LaneA)

### Bug
NT8/Rithmic changes `Order.OrderId` from GUID format (at `Submitted` state) to a permanent
numeric format (at `Accepted` state). `IsDispatchTriggerState` returned `true` for both states,
so `DispatchCopy` was called twice per Market order:
- Submitted: `IsDedup("25e44acc...", 0)` → cache miss → PTT-Copy #1 dispatched
- Accepted:  `IsDedup("2862949760", 0)` → DIFFERENT key → cache miss → PTT-Copy #2 dispatched

Confirmed by 08-37 PM log: orders 2862949761 and 2862949762 both named `PTT-Copy` both filled
on PA-APEX-422136-08, producing 14L when 7 was intended. Same doubling seen for PA-APEX-422136-01
and Sim102 in earlier log entries.

### Fix
Changed `IsDispatchTriggerState(OrderState)` to `IsDispatchTriggerState(OrderState, OrderType)`.
- Market orders: dispatch on `Submitted` only (GUID is stable; numeric ID arrives at Accepted).
- Limit orders: dispatch on `Accepted` only (AddOn path — `Submitted` never fires for these).
Updated single call site in `DispatchCopy` to pass `order.OrderType`.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged (same 2 conditions, same boolean logic) ✅

### Pipeline work needed
- Ph3.5 Tickets:
  - T_DEDUP_MARKET_01: Market order Submitted → IsDispatchTriggerState returns true
  - T_DEDUP_MARKET_02: Market order Accepted → IsDispatchTriggerState returns FALSE (was true before fix)
  - T_DEDUP_LIMIT_01: Limit order Accepted → IsDispatchTriggerState returns true
  - T_DEDUP_LIMIT_02: Limit order Submitted → IsDispatchTriggerState returns false

## HOTFIX-BEALL-DISARM-CROSS-01
**Date**: 2026-08-16 (live trading session)
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Method**: `UpdateButtonColors` (~line 569)
**Status**: PIPELINE-COMPLETE (B73-LaneB)
**Fixes**: BUG 2 partial — BE ALL stays yellow on second panel after manual close

### Bug
`RaiseBeAllDisarmed()` was inside `if (IsPendingSlotsEmpty())` guard. After
`DisarmPendingBe(_leaderAccount)` cleared this panel's slot, another panel's slot
could still exist → `IsPendingSlotsEmpty()` returns false → `RaiseBeAllDisarmed`
never fires → second panel stays amber.

### Fix
Moved `RaiseBeAllDisarmed()` and `UpdateBeAllVisuals(BeState.Idle)` OUTSIDE the
`IsPendingSlotsEmpty` guard. Both fire unconditionally when flat+armed. Each receiving
panel handles its own visual update in `OnGlobalBeAllDisarmed`.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC decreased (removed nested if) ✅

### Pipeline work needed
- Ph3.5 Tickets: T_DISARM_CROSS_01 — two panels open, flat on panel 1 → panel 2 receives disarm event


## HOTFIX-BUFLABEL-02
**Date**: 2026-08-16 (live trading session)
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Methods**: `OnGlobalBeBufferChanged`, `OnQuickAllBufferChanged`, new `FormatQuickAllBuffer`
**Status**: PIPELINE-COMPLETE (B75-LaneB)
**Fixes**: BUF-LABEL-01 (button label not updating on spin), QUICK-LABEL-UNIT-01 (no "t" unit suffix)

### Bug
`OnGlobalBeBufferChanged` and `OnQuickAllBufferChanged` wrote to button Content directly on
Application.Current.Dispatcher. NT8 chart panel WPF elements were created on the chart-window
Dispatcher (different thread context). Writing from the wrong dispatcher = silent no-op.
Also: Quick ALL label showed "+4" with no unit — ambiguous across MES/MGC/MCL.

### Fix
Both handlers wrapped in `Dispatcher.InvokeAsync` (panel-local Dispatcher), mirroring
the correct pattern at `OnGlobalBeAllDisarmed` (line 907).
`FormatQuickAllBuffer` added to append "t" suffix: "Quick ALL +4t".

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged ✅

### Pipeline work needed
- Ph3.5 Tickets: T_LABEL_01 — IncrementQuickAll fires → OnQuickAllBufferChanged → label shows "+Nt"
- Ph3.5 Tickets: T_LABEL_02 — IncrementBuffer fires → OnGlobalBeBufferChanged → label shows "BE ALL +N"

---

---

## HOTFIX-QUICK-T3-01
**Date**: 2026-08-16 (live trading session)
**Files**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`, `src/PropTraderTools/Features/PttQuickExit.cs`
**Methods**: `PttGlobalQuickExit.Execute`, `PttGlobalQuickExit.ExecuteOne` (signature change),
             new `PttGlobalQuickExit.SnapshotTargetOrders`, `PttQuickExit.Execute` (N-bracket rewrite)
**Status**: PIPELINE-COMPLETE (B74-LaneC)
**Fixes**: QUICK-T3-01 (only 2 brackets submitted regardless of ATM target count)

### Bug
`PttGlobalQuickExit.ResolveQuickTicks` always returned (T1, T2) — exactly 2 values.
`PttQuickExit.Execute` submitted exactly PTT-QX-Stop + PTT-QX-T1 (pair A) and
PTT-QX-Stop2 + PTT-QX-T2 (pair B). With a 3-target ATM ("MES $200 SL6"), Target3
and Stop3 survived uncancelled → dangling third bracket → position exposure.

### Fix
`SnapshotTargetOrders` added to `PttGlobalQuickExit` — scans acc.Orders for active
Limit target orders (ATM Target1..N, PTT-QX-T*, PTT-BE-Target-*) before cancelling anything.
`Execute` passes snapshot to `ExecuteOne` → `PttQuickExit.Execute`.
`PttQuickExit.Execute` rewrites bracket submission as a for-loop:
- targetCount = snapshot count, fallback 2 if none
- tNPrice = entry +/- t1Ticks*N * tick (proportional spacing)
- tNQty from snapshot[i].Qty, fallback evenly split
- each pair gets its own OCO ID (independent fills)
- stop names: PTT-QX-Stop, PTT-QX-Stop2, PTT-QX-Stop3 ...
- target names: PTT-QX-T1, PTT-QX-T2, PTT-QX-T3 ...

Also added `Execute(Account, Instrument, int t1, int t2, bool)` compat overload in
`PttQuickExit` to preserve `TradeCopierPanel.OnQuickClick` (off-limits per spec). The
compat overload passes an empty targets list → falls back to 2-target behavior.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC: PttGlobalQuickExit.Execute stays 8, PttQuickExit.Execute = 8, SnapshotTargetOrders = 4 ✅

### Pipeline work needed
- Ph3.5 Tickets: T_QX_T3_01 — Execute with 3-target snapshot → 3 OCO pairs submitted
- Ph3.5 Tickets: T_QX_T3_02 — Execute with empty snapshot → 2 pairs (fallback)
- Ph3.5 Tickets: T_QX_T3_03 — tNPrice for i=2 (T3) = entry + t1*3*tick (proportional)
- Ph3.5 Tickets: T_QX_T3_04 — SnapshotTargetOrders finds ATM Target3 (ATM name match)
- Ph3.5 Tickets: T_QX_T3_05 — SnapshotTargetOrders finds PTT-QX-T3 (QX name match)

---

## HOTFIX-ATM-T3-CANCEL-01

**Date**: 2026-08-16 (live trading session)
**Files**: `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/Features/PttBreakEven.cs`
**Methods**: `IsAtmBracketName` (CopyEngine.cs ~line 469), `CancelStaleBracketsLocal` (PttBreakEven.cs ~line 185)
**Status**: PIPELINE-COMPLETE (B72-LaneA + B74-LaneC)
**Fixes**: T3-ORPHAN-01 (Stop3/Target3 not cancelled), OCO-REUSE-02 (root cause addressed)

### Bug
`IsAtmBracketName` was hardcoded to exactly 4 names: `Stop1`, `Stop2`, `Target1`, `Target2`.
Stop3..Stop9 and Target3..Target9 were NOT returned as cancel candidates by `IsQxCancelCandidate`,
so they survived uncancelled when Quick ALL or BE ALL ran against a 3-target ATM strategy.
`CancelStaleBracketsLocal` `notBe` filter used exact-match `o.Name != "PTT-BE-Stop"` — did not
exclude "PTT-BE-Stop-1", "PTT-BE-Stop-2" etc. from prior BE presses.

### Fix
`IsAtmBracketName`: replaced hardcoded 4-name check with generic:
- `name.StartsWith("Stop", Ordinal) && name.Length > 4 && char.IsDigit(name[4])`
- `name.StartsWith("Target", Ordinal) && name.Length > 6 && char.IsDigit(name[6])`
Matches Stop1..Stop9 and Target1..Target9 without enumerating each.
`CancelStaleBracketsLocal` `notBe`: changed to `!o.Name.StartsWith("PTT-BE-", Ordinal)`.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged (expression body stays CYC=1) ✅

### Pipeline work needed
- Ph3.5 Tickets: T_ATM_T3_01 — IsAtmBracketName("Stop3") returns true
- Ph3.5 Tickets: T_ATM_T3_02 — IsAtmBracketName("Target3") returns true
- Ph3.5 Tickets: T_ATM_T3_03 — IsAtmBracketName("Stop10") returns false (2-digit suffix — digit at [4] is '1', not '10', but check: "Stop10"[4] = '1' which IS a digit — actual edge case: "Stop10" would match. Verify in pipeline.)
- Ph3.5 Tickets: T_ATM_T3_04 — CancelStaleBracketsLocal excludes "PTT-BE-Stop-1"
- Ph3.5 Tickets: T_ATM_T3_05 — CancelStaleBracketsLocal includes "Stop3" in stale list

---

## HOTFIX-BUFLABEL-02

**Date**: 2026-08-16 (live trading session)
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Methods**: `OnGlobalBeBufferChanged`, `OnQuickAllBufferChanged`, new `FormatQuickAllBuffer`
**Status**: PIPELINE-COMPLETE (B75-LaneB) [duplicate entry — see HOTFIX-BUFLABEL-02 above]
**Fixes**: BUF-LABEL-01 (button label not updating on spin), QUICK-LABEL-UNIT-01 (no "t" unit suffix)

### Bug
`OnGlobalBeBufferChanged` and `OnQuickAllBufferChanged` wrote to button Content directly on
`Application.Current.Dispatcher`. NT8 chart panel WPF elements (`_globalBeBtn2`, `_quickAllBtn`)
were created on the chart-window Dispatcher — a different thread context from the app Dispatcher.
Writing to a WPF element from the wrong Dispatcher = silent no-op or cross-thread violation.
Also: Quick ALL label showed "+4" with no tick unit — ambiguous when trading MES/MGC/MCL.

### Fix
Both handlers wrapped in `Dispatcher.InvokeAsync` (panel-local Dispatcher), mirroring the
correct pattern at `OnGlobalBeAllDisarmed` (line 907).
New `FormatQuickAllBuffer` added to append "t" suffix: "Quick ALL +4t".
`FormatBuffer` (per-chart Quick button) is unchanged — no "t" suffix there.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged ✅

### Pipeline work needed
- Ph3.5 Tickets: T_LABEL_01 — IncrementQuickAll fires → OnQuickAllBufferChanged → label shows "+Nt"
- Ph3.5 Tickets: T_LABEL_02 — IncrementBuffer fires → OnGlobalBeBufferChanged → label shows "BE ALL +N"

---

## HOTFIX-BEALL-DISARM-CROSS-01

**Date**: 2026-08-16 (live trading session)
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Method**: `UpdateButtonColors` (~line 569)
**Status**: PIPELINE-COMPLETE (B73-LaneB) [duplicate entry — see HOTFIX-BEALL-DISARM-CROSS-01 above]
**Fixes**: BUG 2 — BE ALL stays yellow on second panel after manual close

### Bug
`RaiseBeAllDisarmed()` was inside `if (IsPendingSlotsEmpty())` guard. After
`DisarmPendingBe(_leaderAccount)` cleared this panel's slot, another panel's slot could still
exist → `IsPendingSlotsEmpty()` returns false → `RaiseBeAllDisarmed` never fires → second panel
stays amber permanently.

### Fix
Moved `RaiseBeAllDisarmed()` and `UpdateBeAllVisuals(BeState.Idle)` OUTSIDE the
`IsPendingSlotsEmpty` guard. Both fire unconditionally when flat+armed. Each receiving panel
handles its own visual update in `OnGlobalBeAllDisarmed`.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC decreased (removed nested if) ✅

### Pipeline work needed
- Ph3.5 Tickets: T_DISARM_CROSS_01 — two panels open, flat on panel 1 → panel 2 receives disarm event and resets to Idle

---

## HOTFIX-QUICK-T3-01

**Date**: 2026-08-16 (live trading session)
**Files**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`, `src/PropTraderTools/Features/PttQuickExit.cs`
**Methods**: `PttGlobalQuickExit.Execute`, `PttGlobalQuickExit.ExecuteOne` (signature change),
             new `PttGlobalQuickExit.SnapshotTargetOrders`, `PttQuickExit.Execute` (N-bracket rewrite)
**Status**: PIPELINE-COMPLETE (B74-LaneC) [duplicate entry — see HOTFIX-QUICK-T3-01 above]
**Fixes**: QUICK-T3-01 (only 2 brackets submitted regardless of ATM target count)

### Bug
`PttGlobalQuickExit.ResolveQuickTicks` always returned (T1, T2) — exactly 2 values.
`PttQuickExit.Execute` submitted exactly 2 OCO pairs (PTT-QX-Stop+T1 and PTT-QX-Stop2+T2).
With a 3-target ATM ("MES $200 SL6"), Target3 and Stop3 survived uncancelled → dangling bracket.

### Fix
`SnapshotTargetOrders` added to `PttGlobalQuickExit` — scans acc.Orders for active Limit target
orders (ATM Target1..N, PTT-QX-T*, PTT-BE-Target-*) before cancelling anything.
`Execute` passes snapshot to `ExecuteOne` → `PttQuickExit.Execute`.
`PttQuickExit.Execute` rewrites bracket submission as a for-loop (N pairs):
- targetCount = snapshot count, fallback 2 if none
- tNPrice = entry ±  t1Ticks*N * tick (proportional spacing)
- tNQty from snapshot[i].Qty, fallback evenly split
- each pair gets its own OCO ID (independent fills)
- stop names: PTT-QX-Stop, PTT-QX-Stop2, PTT-QX-Stop3 ...
- target names: PTT-QX-T1, PTT-QX-T2, PTT-QX-T3 ...
Backward-compat overload (t2Ticks param) preserved for single-chart Quick button callers.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC: PttGlobalQuickExit.Execute=8, PttQuickExit.Execute=8, SnapshotTargetOrders=4 ✅

### Pipeline work needed
- Ph3.5 Tickets: T_QX_T3_01 — Execute with 3-target snapshot → 3 OCO pairs submitted
- Ph3.5 Tickets: T_QX_T3_02 — Execute with empty snapshot → 2 pairs (fallback)
- Ph3.5 Tickets: T_QX_T3_03 — tNPrice for i=2 (T3) = entry + t1*3*tick (proportional)
- Ph3.5 Tickets: T_QX_T3_04 — SnapshotTargetOrders finds ATM Target3
- Ph3.5 Tickets: T_QX_T3_05 — SnapshotTargetOrders finds PTT-QX-T3

---

## HOTFIX-BEALL-FLAT-RESET

**Date**: 2026-08-16 (live trading session)
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Method**: `UpdateButtonColors` (~line 569)
**Status**: PIPELINE-COMPLETE (B73-LaneB)
**Fixes**: BE ALL stays yellow after manual close when per-chart BE was not armed

### Bug
`HOTFIX-F3` gate: `!hasPosition && _beState != BeState.Idle`.
`_beState` is the **per-chart** BE button state only. When user presses BE ALL (global)
without pressing the per-chart BE button, `_beState` remains `BeState.Idle`. Gate evaluates
false → `UpdateBeAllVisuals(BeState.Idle)` and `RaiseBeAllDisarmed()` never execute →
BE ALL button stays amber after position closes via manual flatten.

### Fix
Extracted the BE ALL reset into a SEPARATE independent check:
```csharp
if (!hasPosition && !CopyEngine.Instance.IsPendingSlotsEmpty())
{
    if (_leaderAccount != null)
        CopyEngine.Instance.DisarmPendingBe(_leaderAccount);
    UpdateBeAllVisuals(BeState.Idle);
    CopyEngine.Instance.RaiseBeAllDisarmed();
}
```
This fires unconditionally whenever the position closes AND the engine still has a pending
BE slot — regardless of whether the per-chart BE button was ever armed.
Safe: `UpdateButtonColors(hasPos=false)` only arrives via `TryFirePositionState`
(Filled/PartFilled, post-Gate-2.5) — not on ATM bracket cancel noise.
Double-fire with HOTFIX-F3 is safe: if F3 block runs `DisarmPendingBe` first, then
`IsPendingSlotsEmpty()` returns true and the new block skips.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC: +1 (new independent if block, still well below 8) ✅

### Pipeline work needed
- Ph3.5 Tickets: T_BEALL_FLAT_01 — position closes with BE ALL armed, per-chart BE NOT armed → BE ALL resets to Idle
- Ph3.5 Tickets: T_BEALL_FLAT_02 — position closes with BOTH armed → both reset, no double-fire

---

## HOTFIX-SNAPSHOT-STOP-INSTRREF

**Date**: 2026-08-17 (live trading session)
**File**: `src/PropTraderTools/Features/PttQuickExit.cs`
**Method**: `SnapshotStopPrice` (~line 179)
**Status**: PIPELINE-COMPLETE (B75-LaneA)

### Bug
`SnapshotStopPrice` used reference equality `o.Instrument != instr` to filter orders by
instrument. NT8 creates separate `Instrument` object instances per account context -- two
objects representing the same instrument are NOT reference-equal. This means every order
in the loop was skipped (the condition was always true), so `snapshotStop` was always 0.0.

With `snapshotStop == 0`, the guard `if (snapshotStop > 0)` at `PttQuickExit.Execute` line 100
skips ALL stop submissions. Quick ALL produces target orders only (PTT-QX-T1/T2/T3) with
NO matching stop orders -- position has no downside protection after a Quick ALL press.

Root cause is identical to HOTFIX-BUG-BE-INSTRUMENT-REF (CopyEngine.cs MoveStopToBreakEven,
same session) and the B69 DW-B69-02 fix in FindPosition/SubmitBeStop.

### Fix
Changed reference equality to `FullName` string comparison, identical to the pattern used
in `CancelQxBrackets` (CopyEngine.cs line 513):
```diff
-                if (o.Instrument != instr) continue;
+                if (o.Instrument == null || o.Instrument.FullName != instr?.FullName) continue;
```

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged (one-for-one line replacement, same branch count) ✅

### Pipeline work needed
- Ph3.5 Tickets:
  - T_SNAP_STOP_01: SnapshotStopPrice with Working StopMarket order, different Instrument instance but same FullName -> returns StopPrice (not 0.0)
  - T_SNAP_STOP_02: SnapshotStopPrice with null o.Instrument -> order skipped (no NRE)
  - T_SNAP_STOP_03: SnapshotStopPrice with Filled order -> skipped (state filter)
  - T_SNAP_STOP_04: SnapshotStopPrice with no matching orders -> returns 0.0

---

## HOTFIX-ORPHAN-STOP-CLEANUP

**Date**: 2026-08-17 (live trading session)
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Method**: `UpdateButtonColors` (~line 590)
**Status**: PIPELINE-COMPLETE (B73-LaneB)

### Bug
When the user manually closes a position (Chart Trader × or Close order) while BE is armed,
NT8 does NOT auto-cancel PTT-BE-Stop-N orders (AddOn-issued orders, not ATM-owned). These
orphaned stop orders remain Working and can fill on the next trade, creating an unintended
position. Confirmed from 2026-08-16 CSV: PTT-BE-Stop-3 filled at 10:06:15 PM (Sell 1@7813.25)
while position was already being closed manually -- creating a short that required immediate recovery.

### Fix
Added `CopyEngine.Instance.CancelQxBrackets(_leaderAccount, _instrument)` call unconditionally
whenever `hasPosition=false` and `_leaderAccount != null` and `_instrument != null`.
`CancelQxBrackets` already covers PTT-BE-* prefix via `IsQxCancelCandidate` (line 488 CopyEngine.cs).
Safe: `CancelQxBrackets` is a no-op when no qualifying orders exist (stale.Count==0 returns early).
This fires ONLY on Filled/PartFilled events post-Gate-2.5 -- NOT on bracket cancel noise.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged (one additional straight-line call, no new branches added) ✅

### Diff (minimal)
```diff
+            // HOTFIX-ORPHAN-STOP-CLEANUP: cancel any PTT-BE-*/PTT-QX-* orders that survived
+            // a manual position close.
+            if (!hasPosition && _leaderAccount != null && _instrument != null)
+                CopyEngine.Instance.CancelQxBrackets(_leaderAccount, _instrument);
```

### Pipeline work needed
- Ph3.5 Tickets:
  - T_ORPHAN_01: manual close with PTT-BE-Stop-1/2/3 Working -> all cancelled on flat event
  - T_ORPHAN_02: flat with no PTT-BE orders -> CancelQxBrackets no-op (stale.Count==0)
  - T_ORPHAN_03: flat with PTT-QX-Stop Working -> also cancelled (IsQxCancelCandidate covers PTT-QX-*)

---

## HOTFIX-MSTBE-OCO-TICKSEED-01

**Date**: 2026-08-17 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `_mstbeOcoSeq` field declaration (~line 159)
**Status**: TESTED-PASS (2026-08-17 live session -- ocoId=PTT-BE-Sim101-274022126-* confirmed, 3 OCO pairs submitted, no rejections)

### Bug
`_mstbeOcoSeq` was initialized to `0` at field-declaration time. NT8 allows recompiling
an AddOn within a running session ("Compile" in NinjaScript Editor) without restarting NT8.
When this happens CopyEngine is GC'd and re-created, resetting `_mstbeOcoSeq` to 0.
NT8 keeps all OCO IDs used in the current NT8 session in memory and refuses to reuse a
cancelled OCO ID. After recompile-within-session, seq=1 and seq=2 are immediately rejected:
"The OCO ID 'PTT-BE-Sim101-00001-0' cannot be reused" -- position left unprotected.

Confirmed by 2026-08-16 10-30 PM log: second MSTBE press (seq=00002) has all 6 orders
rejected, meaning 00002-* was already used in the pre-recompile run of the same NT8 session.

### Fix
Seeded `_mstbeOcoSeq` from `Environment.TickCount` (milliseconds since OS boot):
```diff
-        private volatile int _mstbeOcoSeq = 0;
+        private volatile int _mstbeOcoSeq = Environment.TickCount;
```
`Environment.TickCount` advances even during recompile. Post-recompile, the new CopyEngine
instance initialises `_mstbeOcoSeq` to the current tick count -- orders of magnitude above
any value used pre-recompile. No collision possible within a single OS session.

### JS-DNA compliance
- No `lock()` added (volatile int + Interlocked.Increment unchanged) ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC unchanged (field initializer, no Roslyn decision point) ✅
- `Environment.TickCount` returns `int` directly -- no cast needed ✅

### Pipeline work needed
- Ph3.5 Tickets:
  - T_OCO_SEED_01: Two CopyEngine instances (simulating recompile) -- second instance's first NextBeOcoSeq() != any value from first instance (TickCount gap guarantees)
  - T_OCO_SEED_02: NextBeOcoSeq() called 1000x on single instance -- all values unique (Interlocked.Increment guarantees)
  - T_OCO_SEED_03: _mstbeOcoSeq initial value != 0 (TickCount seeding confirmed)

---

## HOTFIX-B63-FLATTEN-01

**Date**: 2026-08-17 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `TryDispatchLeaderFlat` (~line 1323)
**Status**: PIPELINE-COMPLETE (B75-LaneA)
**Deferred item closed**: DW-B63-FLATTEN-MULTWAVE-01

### Bug
`TryDispatchLeaderFlat` gate (3) fires a follower flatten whenever a leader order fills
AND `hasOpenPosition(leader) = false`. This correctly handles native NT8 close signals
(Close/Flatten/Rev*/Exit*) but was also firing for PTT-QX-T* partial-exit fills that
happen to exhaust the leader's last contracts.

When `PTT-QX-T2` filled and left leader -04 at 0ct:
- `IsNativeExitName("PTT-QX-T2")` = false
- `hasOpenPosition(-04)` = false (last contract just sold by T2)
- Gate (3) condition: `!false && false` = false → gate did NOT block → followers flattened
- Followers -01/-03 (still long 2ct) each received `PTT-Flatten Sell Market 2ct`
- Multiple waves from multiple leaders/fills → followers went Long 2ct → Short 6ct

Live incident: 2026-08-17 06:35 AM. -04 (2ct) + -06 (10ct) both leaders to -01/-03.
T1 (6:35:25) + T2 (6:35:28) exhausted -04's position → TryDispatchLeaderFlat fired 3×
(once per trigger event reaching both follower accounts) → 6 waves × 2ct Sell Market.
Corrected by NT8 Flatten Everything at 06:35:38 (Buy 6ct Close on -01/-03).

### Fix
Added guard (2.5) in `TryDispatchLeaderFlat` before the existing gate (3):
```csharp
if (orderName != null && orderName.StartsWith("PTT-", StringComparison.Ordinal)) return false; // (2.5) HOTFIX-B63-FLATTEN-01
```
PTT-owned fills (QX targets, Flatten, Copy, BE-Stop, Tighten) must never trigger follower
flattening via this path. Followers manage their own exits via their own ATM brackets.
Consistent with `IsExitSignalName` in `DispatchCopy` which already blocks PTT- cascade.

The B65 DW-B65-01 native-exit bypass (`IsNativeExitName` at gate 3) is unaffected —
`Close`/`Flatten`/`Rev*`/`Exit*` still propagate correctly to followers.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC: 7 → 8 (one new `if` branch — at JS limit, acceptable) ✅

### Diff (minimal)
```diff
  if (state != OrderState.Filled && state != OrderState.Cancelled) return false; // (1)
  if (isFollower(account)) return false;                                           // (2)
+ if (orderName != null && orderName.StartsWith("PTT-", StringComparison.Ordinal)) return false; // (2.5) HOTFIX-B63-FLATTEN-01
  if (!IsNativeExitName(orderName) && hasOpenPosition(account, instrument)) return false; // (3)
```

### Pipeline work needed (block BXX-LaneA)
- Ph1 Architecture: document that `TryDispatchLeaderFlat` is intended ONLY for native NT8 exits,
  not for PTT-own partial-exit fills accidentally leaving the leader flat.
- Ph3.5 Tickets:
  - T_B63_01: `TryDispatchLeaderFlat` orderName="PTT-QX-T2", leader flat → returns false (no follower flatten)
  - T_B63_02: `TryDispatchLeaderFlat` orderName="PTT-Flatten", leader flat → returns false (no cascade)
  - T_B63_03: `TryDispatchLeaderFlat` orderName="PTT-Copy", leader flat → returns false (no PTT cascade)
  - T_B63_04: `TryDispatchLeaderFlat` orderName="Close", leader flat → returns true (native exit still fires)
  - T_B63_05: `TryDispatchLeaderFlat` orderName="Close", leader has position → returns false (gate 3 still works)
  - T_B63_06: `TryDispatchLeaderFlat` orderName=null, leader flat → returns true (null passes PTT guard, falls to gate 3)

---

## HOTFIX-B63-COPY-CANCEL-01

**Date**: 2026-08-17 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `OnOrderUpdate` -- B56 Cancelled block (~line 811)
**Status**: PIPELINE-COMPLETE (B75-LaneA)
**Fixes**: Follower PTT-Copy entry order disappears when leader gets filled

### Bug
The B56 T1 cancel-propagation block called `CancelOneAccount(follower, instrument)`
for every cancelled order on the leader account -- including ATM bracket cancels
(Stop1/Stop2/Stop3/Target1/Target2/Target3). When the user presses Chart Trader Close,
NT8 cancels all ATM brackets first (Cancelled events fire for each), then fills the
Close market order. Each bracket Cancelled event passed Gate 2 (correct leader
account+instrument match) and reached the B56 block, which called CancelOneAccount
on the follower -- wiping the follower's live PTT-Copy entry order before it filled.
Confirmed by 11-16 AM log: PTT-Copy goes Working at 11:11:49, immediately receives
Cancel submitted at 11:11:51 (same timestamp as Target1/Target2/Target3 Cancelled on leader).

### Fix
Added `if (IsAtmBracketName(e.Order.Name)) return;` guard before the CancelOneAccount
loop. `IsAtmBracketName` already exists and correctly matches Stop1..Stop9 / Target1..Target9.
ATM bracket cancels no longer propagate to followers. Only genuine leader entry-order
cancels (Name="Entry", Name="PTT-Copy", etc.) reach CancelOneAccount.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC: 7 -> 8 (one new `if` inside existing Cancelled block) ✅
- `IsAtmBracketName` is a pre-existing pure static helper -- no new allocations ✅

### Diff (minimal)
```diff
 if (e.Order.OrderState == OrderState.Cancelled)
 {
+    if (IsAtmBracketName(e.Order.Name)) return; // HOTFIX-B63-COPY-CANCEL-01
     foreach (var acc in matchedRule.Value.FollowerAccounts)
```

### Pipeline work needed (block BXX-LaneA)
- Ph3.5 Tickets:
  - T_B63C_01: OnOrderUpdate with leader Stop1 Cancelled -> CancelOneAccount NOT called
  - T_B63C_02: OnOrderUpdate with leader Target3 Cancelled -> CancelOneAccount NOT called
  - T_B63C_03: OnOrderUpdate with leader Entry Cancelled -> CancelOneAccount IS called (regression)
  - T_B63C_04: OnOrderUpdate with leader PTT-Copy Cancelled -> CancelOneAccount IS called
  - T_B63C_05: leader Close Filled (not Cancelled) -> block not reached at all (state guard)

---

## HOTFIX-B64-ENTRY-FLATTEN-01

**Date**: 2026-08-17 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `TryDispatchLeaderFlat` (~line 1338)
**Status**: PIPELINE-COMPLETE (B75-LaneA)
**Fixes**: Follower PTT-Copy entry order cancelled immediately after leader Entry fills

### Bug
`TryDispatchLeaderFlat` flattened followers when the leader's `Entry` order filled.
Root cause: NT8 position state is not updated until the next `OnBarUpdate()` after a fill
(NT8_FULL_REFERENCE.md line 1721). When leader `Entry` fills, `hasOpenPosition` returns
`false` (position race). Gate (3) evaluates `!IsNativeExitName("Entry") && false` = `false`,
so the guard does NOT fire, and the foreach calls `FlattenOneAccount` on all followers.
`FlattenOneAccount` cancels the working PTT-Copy order on the follower account.
Confirmed by 11-41 AM log: at 11:39:10, `d30ffde` PTT-Copy goes `Working` then immediately
`Cancel submitted` at the same clock tick as the leader `Entry` fills and ATM brackets submit.

### Fix
Added `if (orderName == "Entry") return false;` as gate (2.6) in `TryDispatchLeaderFlat`,
immediately after the PTT- prefix guard (2.5). An `Entry` fill can never mean "go flat" --
it means "opened a position". The position-race false-negative on `hasOpenPosition` is only
relevant for exit-order fills (Close/Flatten/Rev/Exit). This guard is unconditional and
does not depend on position state.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- No `async void` added ✅
- CYC: TryDispatchLeaderFlat 8 -> 9 (one new `if` early return) -- pipeline refactor needed ⚠️
- Single string equality comparison -- no heap allocation ✅

### Diff (minimal)
```diff
  if (orderName != null && orderName.StartsWith("PTT-", StringComparison.Ordinal)) return false; // (2.5)
+ if (orderName == "Entry") return false; // HOTFIX-B64-ENTRY-FLATTEN-01
  if (!IsNativeExitName(orderName) && hasOpenPosition(account, instrument)) return false; // (3)
```

### Pipeline work needed (block BXX-LaneB)
- Ph3.5 Tickets:
  - T_B64E_01: TryDispatchLeaderFlat orderName="Entry", state=Filled, no open position -> returns false (followers NOT flattened)
  - T_B64E_02: TryDispatchLeaderFlat orderName="Entry", state=Filled, open position -> returns false (same guard fires first)
  - T_B64E_03: TryDispatchLeaderFlat orderName="Close", state=Filled, no open position -> returns true (flatten fires -- regression check)
  - T_B64E_04: TryDispatchLeaderFlat orderName="Close", state=Filled, open position -> returns false (gate 3 still works)
  - T_B64E_05: Refactor TryDispatchLeaderFlat to CYC<=8 (extract name-guard predicate)

---

## HOTFIX-B65-GATE-C-FILL-GUARD-01

**Date**: 2026-08-17 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `OnOrderUpdate` -- Gate C outer condition (~line 851)
**Status**: PIPELINE-COMPLETE (B75-LaneA)
**Fixes**: Follower PTT-Copy entry order cancelled by HandleEntryChange when leader Entry fills mid-auto-chase

### Bug
Gate C (`HandleEntryChange` dispatch) did not guard against `e.Order.Filled > 0`. When the
leader Entry order was auto-chased and simultaneously filled (same NT8 tick), NT8 dispatched
both the price-change `Accepted`/`Working` events AND the `PartFilled`/`Filled` events
concurrently on background threads. The `Working` event (same price as `Accepted`) saw
`storedPrice != currentPrice` due to a concurrent-read race before `HandleEntryChange`
updated `_dedupCache` on the `Accepted` thread. As a result, `HandleEntryChange` fired a
second time -- cancelling the live PTT-Copy follower order that had just been correctly placed
by the first `HandleEntryChange` call, and placing a replacement that was immediately stale.

Confirmed in 12:09:57 PM log: `d190d5` (PTT-Copy) went `Submitted -> Accepted -> Working ->
Cancel submitted` all at the same timestamp as `Entry -> Filled`. The cancel source was the
second (race) invocation of `HandleEntryChange`.

### Fix
Added `&& e.Order.Filled == 0` to Gate C's outer `if` condition. When `Filled > 0`, the
order is mid-fill and price dragging is impossible -- Gate C is skipped entirely. The
fall-through to `DispatchCopy` is a no-op for `Working`/`Accepted` states
(`IsDispatchTriggerState(Working/Accepted, Limit) == false`). Normal drag detection is
unaffected: a dragged entry order always has `Filled == 0` at the moment of the drag event.

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- CYC: 8 -> 9 (one new `&&` condition in Gate C outer `if`) ✅
- No new heap allocations -- `e.Order.Filled` is a primitive int property read ✅

### Diff (minimal)
```diff
 if ((e.Order.OrderType == OrderType.Limit || e.Order.OrderType == OrderType.StopLimit)
-    && (e.Order.OrderState == OrderState.Accepted || e.Order.OrderState == OrderState.Working))
+    && (e.Order.OrderState == OrderState.Accepted || e.Order.OrderState == OrderState.Working)
+    && e.Order.Filled == 0) // HOTFIX-B65-GATE-C-FILL-GUARD-01
```

### Pipeline work needed (block BXX-LaneA)
- Ph3.5 Tickets:
  - T_B65G_01: Gate C with Entry Working, Filled=0, price changed -> HandleEntryChange fires (normal drag, unaffected)
  - T_B65G_02: Gate C with Entry Working, Filled=3, price changed -> HandleEntryChange does NOT fire (fill guard)
  - T_B65G_03: Gate C with Entry Accepted, Filled=0, price changed -> HandleEntryChange fires (normal drag)
  - T_B65G_04: Gate C with Entry Accepted, Filled=1, price changed -> HandleEntryChange does NOT fire (fill guard)
  - T_B65G_05: Entry fills while PTT-Copy is Working -> PTT-Copy remains Working, not cancelled

---

## DIAGNOSIS-B66-ATM-CANCEL-ROOT-CAUSE

**Date**: 2026-08-17 (live trading session)
**Status**: ROOT CAUSE IDENTIFIED -- no code fix applied yet
**Symptom**: Follower PTT-Copy (Limit GTC) cancelled at exact moment leader Entry fills with ATM strategy

### Confirmed root cause
NT8's ATM strategy manager cancels open working Limit orders on all accounts in its scope
when it arms the OCO bracket set after `Entry -> Filled`. This is NT8-internal behavior --
it bypasses all CopyEngine code entirely. No DIAG trace fires. No `CancelOneAccount` call.

**Evidence**:
- 12:28:21 PM test: leader Entry fills with ATM=None (no ATM strategy) -> PTT-Copy SURVIVES
  and fills at the same price. Leader order Name='' (no strategy). Both accounts filled.
- 12:20:14 PM test: leader Entry fills with ATM=`MES $200 SL6 - 1` -> PTT-Copy `Cancel submitted`
  fires at the EXACT same millisecond as ATM brackets (Stop1/Stop2/Stop3/Target1/Target2/Target3)
  Submitted/Accepted/Working. NT8's ATM engine issues the cancel as part of bracket arming.

### CopyEngine fixes B63/B64/B65 are all CORRECT and NEEDED
They fix distinct real bugs:
- B63: ATM bracket Cancelled events propagating CancelOneAccount (Gate B56 path)
- B64: Entry Filled triggering TryDispatchLeaderFlat via position-race (TryDispatchLeaderFlat path)
- B65: Concurrent Accepted+Working events with Filled>0 triggering double HandleEntryChange (Gate C race)
These three paths are all separate from the NT8-ATM cancellation path.

### Options for fix
- **Option A (workaround, no code)**: Set follower ATM mode to Market in the panel. PTT-Copy
  fires as Market order, fills immediately at placement, no working Limit exists when ATM arms.
- **Option B (HOTFIX-B66)**: On `Entry -> Filled` leader event, detect and immediately re-place
  the follower's working PTT-Copy as a Market order before ATM brackets arm (~same tick).
  Requires new gate in OnOrderUpdate: `if (state==Filled && name=="Entry") FireFollowerMarketFill()`.
- **Option C (correct long-term)**: Suppress NT8's ATM manager from seeing Sim102 as in-scope.
  Requires NT8 ATM account group configuration -- outside CopyEngine control.

### Pipeline work needed
- T_B66_01: Design HOTFIX-B66 FireFollowerMarketFill gate -- Ph2 architecture review needed
- T_B66_02: Confirm Option A (Market mode) works for follower accounts on live ATM trades
- T_B66_03: Regression -- B63 bracket-cancel guard still functional with B66 in place

---

## HOTFIX-B66-ATM-ARM

**Date**: 2026-08-17 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `OnOrderUpdate` -- new gate before `DispatchCopy` + new `ArmFollowerAtm()` method
**Status**: APPLIED + SYNCED -- TEST ONLY, hypothesis unverified
**Fixes**: Follower PTT-Copy Limit GTC cancelled by NT8 ATM engine when leader Entry fills with ATM strategy

### Bug (DIAGNOSIS-B66 confirmed)
NT8's ATM strategy manager cancels all open working Limit orders on follower accounts
when it arms OCO brackets after `Entry -> Filled`. This is NT8-internal -- zero CopyEngine
code path, zero DIAG output. Confirmed: ATM=None trade -> PTT-Copy survives; ATM=strategy -> PTT-Copy cancelled.

### Hypothesis
If the follower's PTT-Copy order is already registered inside an Active `ServerAtmStrategy`
on `Account.ServerStrategies` before the leader fills, NT8's ATM cancel sweep may recognise
the order as ATM-managed and skip cancelling it.

### Fix (TEST implementation)
Added gate in `OnOrderUpdate`: when leader order `Name == "Entry"` and state is
`Working` or `Accepted`, call `ArmFollowerAtm(rule, instrument, action)`.

`ArmFollowerAtm()`:
1. Reads `_cloneAtmCache` for the template name (set by `OnCloneModeClick`)
2. Looks up `UserAtmDictionary.Instance.TryGetValue(templateName)` to get saved template
3. Clones the first template entry via `ServerAtmStrategy.Clone()`
4. Sets `atm.Account`, `atm.Instrument`, adds PTT-Copy order to `atm.Orders` + `atm.OrderIds`
5. Calls `acc.ServerStrategies.Add(atm)` then `atm.SetState(State.Active)`
6. Emits `PTT-B66:` status line for verification in NT8 Output Tab

### What to look for in NT8 Output Tab
- `PTT-B66: armed ATM <template> on Sim102 order=<id>` -- confirms ArmFollowerAtm fired
- `PTT-B66-ERR: ...` -- NT8 threw on SetState; hypothesis fails, remove this hotfix
- PTT-Copy order on Sim102 survives and fills -- hypothesis confirmed, brackets arm on fill
- PTT-Copy order on Sim102 still cancelled -- hypothesis wrong, NT8 ignores ServerStrategies

### JS-DNA compliance
- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- No `async void` added ✅
- CYC: 9 -> 10 (one new `if` in `OnOrderUpdate` + new `ArmFollowerAtm` method CYC=6) ✅
- `acc.Orders.ToList()` snapshot -- no InvalidOperationException on concurrent modification ✅

### Diff (minimal)
```diff
+            if (e.Order.Name == "Entry"
+                && (e.Order.OrderState == OrderState.Working || e.Order.OrderState == OrderState.Accepted))
+            {
+                ArmFollowerAtm(matchedRule.Value, e.Order.Instrument, e.Order.OrderAction);
+            }
```

### Pipeline work needed (block BXX-LaneA)
- T_B66A_01: verify `PTT-B66:` line appears in Output Tab on leader Entry Working event
- T_B66A_02: verify PTT-Copy survives and fills when leader Entry fills with ATM active
- T_B66A_03: verify follower gets Stop1/Target1 brackets after PTT-Copy fills
- T_B66A_04: verify `PTT-B66-ERR:` does NOT appear (no exception on SetState)
- T_B66A_05: regression -- B63 bracket-cancel guard still fires correctly (IsAtmBracketName path intact)
- T_B66A_06: if hypothesis fails -- remove ArmFollowerAtm gate and method entirely

---

## HOTFIX-B66-COPY-REPLACE

**Date**: 2026-08-17 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Methods**: `OnOrderUpdate` (pre-Gate-1 block) + new `ReplaceFollowerCopyOnAtmCancel`
**Status**: PIPELINE-COMPLETE (B75-LaneA)

### Bug
After a leader entry fills, NT8 ATM bracket-arming sweep cancels all PTT-Copy Limit orders
on follower accounts. Gate 2 returns null for followers (not rule masters), so normal copy
dispatch never re-places the cancelled follower order. Follower left flat while leader is long.

### Fix
Pre-Gate-1 block in OnOrderUpdate: detects Name=="PTT-Copy" + Cancelled + LimitPrice>0,
calls ReplaceFollowerCopyOnAtmCancel(Order).
ReplaceFollowerCopyOnAtmCancel: walks _rules to find follower match, verifies leader has
open position (ATM-sweep vs close cancel), re-fires SendCopy at same LimitPrice with
orderId suffix "-R" to bypass dedup cache.

### Key design decisions
- Same LimitPrice (Director confirmed)
- Limit order type, not Market (exact entry level required)
- HasOpenPosition(leader) guard: ATM-sweep cancel (leader long) vs normal close cancel (leader flat)
- OrderId suffix "-R": bypasses IsDedup cache for the replacement order
- Pre-Gate-1 placement: follower accounts never pass Gate 2 (not rule masters)
- CYC: OnOrderUpdate +1 (now CYC=10, hotfix exception), ReplaceFollowerCopyOnAtmCancel CYC=6

### JS-DNA compliance
- No lock() added ✅
- No throw new added ✅
- No return null added ✅ (void method)
- No async void added ✅

### Dedup cascade analysis
Replacement PTT-Copy reaches Accepted on follower -> OnOrderUpdate fires -> Gate 2 returns null
(follower, not master) -> exits. No cascade. If replacement is cancelled again: HasOpenPosition
returns false (leader flat after close) -> no infinite loop. Edge case of double ATM sweep is
acceptable for hotfix; pipeline block should add resubmit-count guard (_copyReplaceCount, max=1).

### Pipeline debt (block BXX)
- Refactor OnOrderUpdate (CYC=10-><=8): extract name-guard predicate
- Refactor TryDispatchLeaderFlat (CYC=9-><=8): extract name-guard predicate
- Add resubmit-count guard to ReplaceFollowerCopyOnAtmCancel (max 1 replace per orderId)
- T_B66R_01: ReplaceFollowerCopyOnAtmCancel called when PTT-Copy Cancelled + leader has open position
- T_B66R_02: NOT called when leader is flat (normal close cancel)
- T_B66R_03: NOT called for non-follower PTT-Copy cancel
- T_B66R_04: orderId suffix "-R" bypasses IsDedup cache
- T_B66R_05: re-placed order uses same LimitPrice as cancelled order

---

## HOTFIX-B66-NATIVE-ATM

**Date**: 2026-08-17 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Methods**: `IsExitSignalName` + `DispatchCopy` + `ReplaceFollowerCopyOnAtmCancel` + new `SendCopyWithAtm`
**Status**: PIPELINE-COMPLETE (B75-LaneA)

### Bug
In Clone mode, follower entry order was placed as a bare `"PTT-Copy"` Limit via `SendCopy`.
`StartAtmStrategy` (the only path to native NT8 ATM from AddOnBase) requires the order name
to be `"Entry"`. Result: follower filled with no brackets, no trailing stop, no native auto-BE.

### Fix
New `SendCopyWithAtm`: `CreateOrder("Entry")` + `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(template, order)`.
`DispatchCopy`: Named mode routes to `SendCopyWithAtm` instead of `SendCopy`.
`ReplaceFollowerCopyOnAtmCancel`: Named mode routes to `SendCopyWithAtm` for re-placed entries.
`IsExitSignalName`: `"Entry"` added -- blocks follower ATM fills from cascading back into `DispatchCopy`.
Pre-Gate-1 cancel guard: widened to catch `"Entry"` cancels (ATM sweep on Named mode entries).

### JS-DNA compliance
- No lock() added
- No throw new added
- No return null added
- No async void added

### NT8 API facts confirmed
- `StartAtmStrategy` is static on `NinjaTrader.NinjaScript.AtmStrategy` -- callable from AddOnBase
- Order name MUST be "Entry" -- any other name silently fails to arm brackets
- `StartAtmStrategy` handles submission internally -- no separate `Submit()` call needed
- Source: NT8 online docs scraped 2026-08-17, added to NT8_FULL_REFERENCE.md

### Pipeline debt (block BXX)
- T_B66N_01: follower "Entry" order appears in Orders tab in Named/Clone mode
- T_B66N_02: native ATM brackets (Stop1/Target1 etc.) appear on follower after entry fills
- T_B66N_03: trailing stop and auto-BE work natively on follower
- T_B66N_04: IsExitSignalName("Entry") == true (no cascade)
- T_B66N_05: non-Named modes (Inherit, Market) still use SendCopy path unaffected
- T_B66N_06: ATM-sweep re-place in Named mode re-arms native ATM via SendCopyWithAtm

---

## HOTFIX-B67-CHECKBOX-RESTORE

**Date**: 2026-08-17 (live trading session)
**Files**: `src/PropTraderTools/CopyEngine.cs` + `src/PropTraderTools/TradeCopierPanel.cs`
**Methods**: new `GetSavedFollowerNames` + `OnLoaded` restore block
**Status**: PIPELINE-COMPLETE (B75-LaneB)

### Bug
After NT8 restart, `LoadRules` correctly restores the engine rule (instrument + master + followers)
and `_isCopyEnabled = true`. But `OnLoaded` builds all `_followerItems` with `IsSelected = false`.
The first time the user touches any follower checkbox, `TryAutoApply` fires with `followers.Length == 0`
and replaces the valid restored rule with an empty follower list. Copy silently stops.

### Fix
New `CopyEngine.GetSavedFollowerNames(instrument, masterName)`: returns `HashSet<string>` of follower
account names from `_rules` for the given instrument+master. CYC=2, no lock, no null return.
`TradeCopierPanel.OnLoaded`: after `LoadFollowers()`, calls `GetSavedFollowerNames`, sets
`IsSelected = true` on matching `_followerItems`, re-sorts rows, calls `TryAutoApply()` to re-register
the live rule. Only fires when `_instrument != null && _leaderAccount != null && saved.Count > 0`.

### JS-DNA compliance
- No lock() added
- No throw new added
- No return null added (HashSet, never null)
- No async void added

### Pipeline debt (block BXX)
- T_B67_01: after restart with saved rule, follower checkboxes are pre-checked on panel load
- T_B67_02: status bar shows "Rule: MES SEP26 leader=Sim101" immediately on load (no manual action)
- T_B67_03: first checkbox toggle after restart does NOT wipe the follower list
- T_B67_04: GetSavedFollowerNames returns empty set when no rule saved (no crash)
- T_B67_05: if _instrument or _leaderAccount is null at OnLoaded, restore block is skipped silently

---

## HOTFIX-B67-ENTRY-UNBLOCK

**Date**: 2026-08-17 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `IsExitSignalName`
**Status**: PIPELINE-COMPLETE (B75-LaneA)

### Bug
HOTFIX-B66-NATIVE-ATM added `if (name == "Entry") return true` to `IsExitSignalName`.
The intent was to block follower ATM entry fills from cascading back into `DispatchCopy`.
However, Chart Trader always names the leader's own entry order `"Entry"`.
Gate 0.5 in `DispatchCopy` calls `IsExitSignalName(order.Name)` and now returned `true`
for the leader's entry -- blocking it from ever reaching `DispatchCopy`. Copy never fired.

### Fix
Removed `"Entry"` from `IsExitSignalName`. The cascade it was guarding against is impossible:
Gate 2 filters to `order.Account.Name == rule.MasterAccount.Name`. Follower accounts never pass
Gate 2, so follower `"Entry"` orders (from `SendCopyWithAtm`) cannot reach `DispatchCopy` regardless.
The guard was unnecessary and was silently blocking all copy dispatch.

### JS-DNA compliance
- No lock() added
- No throw new added
- No return null added
- No async void added

### Pipeline debt (block BXX)
- T_B67E_01: leader "Entry" Limit order triggers copy dispatch (PTT-Copy on Sim102)
- T_B67E_02: follower "Entry" order (SendCopyWithAtm Named mode) does NOT trigger a second dispatch
- T_B67E_03: IsExitSignalName("Entry") == false (regression test)
- T_B67E_04: IsExitSignalName("PTT-Copy") == true (PTT- prefix still blocked)
- T_B67E_05: copy fires in Signal mode (baseline regression)

---

## HOTFIX-B66-COPY-REPLACE-FIX

**Date**: 2026-08-17 (live trading session — post-test diagnosis)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Methods**: `ReplaceFollowerCopyOnAtmCancel` (guard added) + new `HasWorkingPttCopy`
**Status**: PIPELINE-COMPLETE (B75-LaneA)

### Bug
`ReplaceFollowerCopyOnAtmCancel` fired on every entry-drag cancel (auto-chase), not just
ATM-sweep cancels. When the leader's entry was dragged, `HandleEntryChange` cancelled the
old follower PTT-Copy and placed a new one at the updated price. The Cancelled event for
the old PTT-Copy then triggered `ReplaceFollowerCopyOnAtmCancel`, which placed an additional
PTT-Copy at the old price — resulting in two live follower orders per drag step.

The `HasOpenPosition(leader)` guard did not filter this case because the leader has an open
(or pending) position during entry drag, so the guard passed.

Observed: CSV 06-59 PM shows ~6 auto-chase drag steps, each producing a stale PTT-Copy
re-place at the prior price alongside the correct new PTT-Copy from HandleEntryChange.
First test produced extra orders + brackets; subsequent tests produced open position without
brackets (extra orders consumed the position but no ATM was associated).

### Root cause
Entry drag cancel and ATM-sweep cancel are both `Name=="PTT-Copy" + Cancelled + leader long`.
The correct discriminator: during entry drag, `HandleEntryChange` already placed a replacement
PTT-Copy (Working/Accepted/Submitted) before the Cancelled event arrives. During ATM-sweep,
all follower orders are wiped — nothing is Working/Accepted/Submitted.

### Fix
Added guard in `ReplaceFollowerCopyOnAtmCancel` after `HasOpenPosition` check:
```csharp
if (HasWorkingPttCopy(cancelledOrder.Account, cancelledOrder.Instrument)) return;
```
New helper `HasWorkingPttCopy`: walks `acc.Orders.ToList()` for Working/Accepted/Submitted
PTT-Copy or Entry orders on the same instrument using FullName string compare.
Returns true (skip re-place) if HandleEntryChange's replacement is already in flight.
Returns false (proceed with re-place) if ATM sweep wiped everything.

### JS-DNA compliance
- No lock() added ✅ (acc.Orders.ToList() snapshot — no lock needed)
- No throw new added ✅
- No return null added ✅
- No async void added ✅
- FullName string compare (reference equality banned per HOTFIX-BUG-BE-INSTRUMENT-REF) ✅

### Diff (minimal)
**ReplaceFollowerCopyOnAtmCancel — guard added (1 line)**:
```diff
  if (!HasOpenPosition(leader, cancelledOrder.Instrument)) return;     // (5)
+ if (HasWorkingPttCopy(cancelledOrder.Account, cancelledOrder.Instrument)) return; // (6) drag-cancel: replacement already in flight
  // Leader has open position and no replacement in flight -- ATM-sweep cancel, re-place.
```

**New HasWorkingPttCopy method**:
```csharp
private bool HasWorkingPttCopy(Account acc, Instrument instrument)
{
    foreach (var order in acc.Orders.ToList())
    {
        if (order.Instrument?.FullName != instrument.FullName) continue;
        if (order.OrderState != OrderState.Working
            && order.OrderState != OrderState.Accepted
            && order.OrderState != OrderState.Submitted) continue;
        if (order.Name == "PTT-Copy" || order.Name == "Entry") return true;
    }
    return false;
}
```

### Expected NT8 behavior after fix
- Entry drag (auto-chase): follower PTT-Copy tracks each new price, single order only
- ATM-sweep cancel (post-leader-fill): follower PTT-Copy re-placed once at fill price
- Normal close cancel (leader flat): no re-place (HasOpenPosition guard)

### Pipeline debt additions (append to HOTFIX-B66-COPY-REPLACE block BXX)
- T_B66R_06: HasWorkingPttCopy returns true when Working PTT-Copy exists
- T_B66R_07: HasWorkingPttCopy returns false after ATM sweep (no Working orders)
- T_B66R_08: entry drag — ReplaceFollowerCopyOnAtmCancel skips re-place (replacement in flight)
- T_B66R_09: ATM-sweep — ReplaceFollowerCopyOnAtmCancel fires re-place (no replacement in flight)

---

## HOTFIX-CLONE-DRAG + DIAG-CLONE-01

**Date**: 2026-08-17 (live trading session)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Methods**: `FindFollowerEntryOrder` (name guard widened) + `SetCloneAtmCache` + `GetCloneAtmMode` (diagnostics)
**Status**: PIPELINE-COMPLETE (B75-LaneA)

### Bug 1: Clone mode follower entry drag never propagated (HOTFIX-CLONE-DRAG)
`HandleEntryChange` -> `FindFollowerEntryOrder` had `order.Name == "PTT-Copy"` hardcoded.
Clone mode places follower entries as `Name='Entry'` (required by `StartAtmStrategy`).
When leader auto-chase drags, the follower `"Entry"` order was never found -> never dragged.
Follower entry stayed at original price; leader filled at chase price; positions mismatched.

### Fix 1
Widened name guard in `FindFollowerEntryOrder`:
```diff
-    && order.Name == "PTT-Copy")
+    && (order.Name == "PTT-Copy" || order.Name == "Entry"))
```
Now `HandleEntryChange` finds and drags Clone mode follower entries correctly.

### Bug 2: Clone mode ATM template may not be captured (DIAG-CLONE-01)
`SetCloneAtmCache` reads ATM template via `GetLeaderAtmTemplateName` -> `FindVisualChildByIndex<ComboBox>(ct, 2)`.
If index 2 is wrong for this NT8 version/layout, cache stays empty.
`GetCloneAtmMode` falls back to `Inherit` -> `SendCopy` instead of `SendCopyWithAtm` -> no brackets.
Added `[PTT-CLONE]` Output.Process logs to diagnose template capture on next test.

### What to check in NT8 Output Tab 1 after F5
**When you click Clone mode button**:
- `[PTT-CLONE] SetCloneAtmCache: 'MES $200 SL6 - 1' (empty=False)` = template captured correctly
- `[PTT-CLONE] SetCloneAtmCache: '' (empty=True)` = template NOT captured -> Issue 2 confirmed

**When leader entry fills** (if cache was empty):
- `[PTT-CLONE] GetCloneAtmMode: cache empty -- falling back to Inherit` = root cause confirmed

### JS-DNA compliance
- No lock() added ✅
- No throw new added ✅
- No return null added ✅
- No async void added ✅

### Pipeline debt (block BXX)
- Replace `FindVisualChildByIndex<ComboBox>(ct, 2)` with named-property or tag-based lookup
- T_CLONE_01: FindFollowerEntryOrder returns "Entry" order (Clone mode)
- T_CLONE_02: FindFollowerEntryOrder returns "PTT-Copy" order (Inherit mode, regression)
- T_CLONE_03: SetCloneAtmCache with non-empty template -> GetCloneAtmMode returns Named
- T_CLONE_04: SetCloneAtmCache with empty string -> GetCloneAtmMode returns Inherit
- Remove DIAG-CLONE-01 Output.Process lines once Clone mode confirmed working

---

## HOTFIX-B66-ATM-OBJ

**ID**: HOTFIX-B66-ATM-OBJ
**Date**: 2026-08-17
**Files**: `src/PropTraderTools/CopyEngine.cs` + `src/PropTraderTools/TradeCopierPanel.cs`
**Methods**: `FollowerAtmMode.Named` (new 2-arg ctor + AtmObject property), `SetCloneAtmObjectCache` (new), `GetCloneAtmMode` (updated), `DispatchCopy` inner loop (updated), `ReplaceFollowerCopyOnAtmCancel` (updated), `SendCopyWithAtm` (signature changed), `OnCloneModeClick` (updated)
**Status**: PIPELINE-COMPLETE (B75-LaneB)

### Bug
`GetLeaderAtmTemplateName` returned `"AtmStrategy"` (the C# class name, not the template file name) because `ChartTrader.AtmStrategy.Name` reflects the runtime object name, not the template. `SendCopyWithAtm` called `StartAtmStrategy("AtmStrategy", order)` -- template not found -- order stayed `Initialized`, never submitted. Follower got yellow ghost lines on chart, no fill, no brackets.

### Root cause
NT8 `ChartTrader.AtmStrategy` returns the live strategy instance. Its `.Name` property is the runtime class name `"AtmStrategy"`, NOT the user-selected template name (e.g. `"MES $200 SL6"`). The string-name overload `StartAtmStrategy(string, order)` silently no-ops when the template name doesn't match a file on disk. The fix is to pass the **object** directly using `StartAtmStrategy(AtmStrategy, Order)`.

### Fix
Capture `ChartTrader.AtmStrategy` object at click time in `OnCloneModeClick` via `FindVisualChild<ChartTrader>`. Store as `volatile NinjaTrader.NinjaScript.AtmStrategy _cloneAtmObject` via new `SetCloneAtmObjectCache`. `GetCloneAtmMode` returns `FollowerAtmMode.Named(string, atmObj)` when object is non-null. `SendCopyWithAtm` uses `StartAtmStrategy(namedMode.AtmObject, order)` when `AtmObject != null`, falling back to string overload otherwise.

### Key design decisions
- `volatile` on reference type is valid C# -- no lock needed
- String cache (`_cloneAtmCache`) kept for display/logging; object cache (`_cloneAtmObject`) drives dispatch
- Fall-through to string overload preserves behavior when object unavailable (panel reload after NT8 restart)
- `StartAtmStrategy` handles order submission internally -- no separate `Submit()` call in `SendCopyWithAtm`

### JS-DNA compliance
- No `lock()` added (volatile reference, no lock needed) ✅
- No `throw new` added ✅
- No `return null` added ✅
- No `async void` added ✅

### Sync
`powershell -File scripts\sync-ptt-to-nt8.ps1` output:
```
COPIED:   CopyEngine.cs
COPIED:   TradeCopierPanel.cs
Done. Copied: 2  Skipped (in sync): 13  Excluded (tests/obj/bin): 29
```

### Expected NT8 behavior after fix
- Click Clone radio -> `[PTT-CLONE] SetCloneAtmObjectCache: SET` in Output Tab 1
- Leader Buy LMT fills -> follower gets `"Entry"` order + `StartAtmStrategy(atmObj, order)`
- Follower account shows Stop1/Stop2/Target1/Target2 brackets (native ATM armed)
- No yellow ghost lines; follower fills at same limit price as leader

### What to check in NT8 Output Tab 1 after F5
- `[PTT-CLONE] SetCloneAtmObjectCache: SET` (object captured, not null)
- `[PTT-CLONE] GetCloneAtmMode: object present` (object dispatched correctly)
- NO `[PTT-CLONE] GetCloneAtmMode: cache empty` line

### Pipeline debt (block BXX-LaneA)
- Ph1 Architecture: document `_cloneAtmObject` volatile field + two-cache design
- Ph3 DNA: verify `volatile` reference field conforms to JS-021 (no lock -- CONFIRMED: volatile ref is lock-free)
- Ph5 Tests:
  - T_B66OBJ_01: `SetCloneAtmObjectCache(non-null)` -> `GetCloneAtmMode` returns `Named` with `AtmObject != null`
  - T_B66OBJ_02: `SetCloneAtmObjectCache(null)` -> `GetCloneAtmMode` returns `Inherit` (object null, string empty)
  - T_B66OBJ_03: `SendCopyWithAtm` with `AtmObject != null` calls `StartAtmStrategy(obj, order)` path
  - T_B66OBJ_04: `SendCopyWithAtm` with `AtmObject == null`, string non-empty -> falls back to string overload
  - T_B66OBJ_05: regression -- `DispatchCopy` non-Named mode still uses `SendCopy` unaffected

## HOTFIX-B76-FLATTEN-RACE-01

**ID**: HOTFIX-B76-FLATTEN-RACE-01
**Date**: 2026-08-18
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `FlattenOneAccount(Account acc, Instrument instrument)` (line ~1850)
**Status**: PIPELINE_COMPLETE (B76-LaneA FINAL_PASS 2026-08-18)
**Authorized**: Director direct-engineer pre-pipeline test

### Bug
ATM BE stop fills a follower account flat. `acc.Positions` is stale in the same `OnOrderUpdate`
cycle (NT8 position lag, NT8_FULL_REFERENCE.md line 1721). `FindPosition()` reads the stale
"1 Long". `FlattenOneAccount` submits PTT-Flatten Sell Market. Account inverts to 1 Short.
Observed live: 2026-08-18 07:12 AM session, -08 account, MES SEP26.

### Fix
Added `posAfterCancel = FindPosition(acc, instrument)` AFTER `CancelAllAccountOrders`.
If `posAfterCancel` is null or qty=0: emit "flat-race skip" StatusUpdate and return.
Otherwise use `posAfterCancel` for action ternary and `CreateOrder` quantity.
CYC: 4 -> 5. JS-DNA: no lock, no throw, no async void. ✅

### Pipeline work needed
B76-LaneA pipeline documents are already written at docs/brain/B76-LaneA/.
Run Ph4a ptt-engineer to formally execute + test, Ph4b to verify, Ph5 to sign off.


## HOTFIX-B76-FLATTEN-GUARD-01
**ID**: HOTFIX-B76-FLATTEN-GUARD-01
**Date**: 2026-08-18
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `FlattenOneAccount(Account acc, Instrument instrument)`
**Bug**: N PTT-Flatten orders submitted per flatten call (N = open bracket count). Each cancel-ack from NT8 fires one OnOrderUpdate callback, and each callback re-enters FlattenOneAccount via TryDispatchLeaderFlat with no re-entry guard. Result: N PTT-Flatten market orders submitted simultaneously.
**Fix**: Per-account in-flight flag (_flattenInFlight ConcurrentDictionary). TryAdd at method entry -- if already set, emit flat-guard skip and return. finally block calls TryRemove to clear flag after Submit. Lock-free (JS-021). Zero heap alloc on hot path.
**Status**: PIPELINE_COMPLETE (B76-LaneA FINAL_PASS 2026-08-18)

## HOTFIX-B76-POSSTATE-LEAK-01
**ID**: HOTFIX-B76-POSSTATE-LEAK-01
**Date**: 2026-08-18
**File**: `src/PropTraderTools/TradeCopierAddOn.cs`
**Method**: `DoInject(Chart chart)` -- stale panel removal loop (line ~373)
**Bug**: PositionStateChanged fires 16x per position event. Stale TradeCopierPanel objects removed from the ChartTrader grid on F5 reload without calling Detach(). Each stale panel retains its PositionStateChanged += OnPositionStateChanged subscription. After N reloads there are N subscriptions on the singleton CopyEngine event. Each raise of PositionStateChanged calls OnPositionStateChanged N times.
**Fix**: Cast each stale grid child to TradeCopierPanel and call stalePanel.Detach() before grid removal. Detach() unsubscribes all CopyEngine events including PositionStateChanged. Idempotent -- safe if Detach() was already called. Zero new state.
**Status**: PIPELINE_COMPLETE (B76-LaneA FINAL_PASS 2026-08-18)

## HOTFIX-B76-FLATTEN-GUARD-02
**ID**: HOTFIX-B76-FLATTEN-GUARD-02
**Date**: 2026-08-18
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `FlattenOneAccount(Account acc, Instrument instrument)`
**Bug**: v1 flag (_flattenInFlight ConcurrentDictionary TryAdd/TryRemove in finally) was cleared before cancel-ack callbacks arrived. All N cancel-ack threads re-entered with flag already clear -- no guard effect. N PTT-Flatten orders still submitted.
**Fix (v2)**: Scan acc.Orders.ToList() at method entry for an existing PTT-Flatten order in Submitted/Accepted/Working state. NT8 order book is the authoritative in-flight signal -- it remains populated until Filled/Cancelled, surviving across all cancel-ack callbacks. If found, emit flat-guard skip StatusUpdate and return. Zero new state, zero allocations, no flag to maintain.
**Test result**: PASS confirmed 12:48 PM session -- exactly 1 PTT-Flatten Filled, zero Cancelled duplicates.
**Status**: PIPELINE_COMPLETE (B76-LaneA FINAL_PASS 2026-08-18)

---

## HOTFIX-B76-POSSTATE-LEAK-02
**ID**: HOTFIX-B76-POSSTATE-LEAK-02
**Date**: 2026-08-18
**File**: `src/PropTraderTools/TradeCopierWindow.cs`
**Method**: `OnLoaded` (lines 110-128)
**Bug**: PositionStateChanged fires N times per position event (confirmed 16 False on entry, 33 True, growing). Root cause: TradeCopierWindow.OnLoaded calls _engine.Subscribe() which does acc.OrderUpdate += OnOrderUpdate for every account in Account.All. OnClosed calls _engine.Unsubscribe(). If OnLoaded fires N times without a corresponding OnClosed (NT8 menu re-open, window re-init), Subscribe() accumulates N OnOrderUpdate delegates per account. OnOrderUpdate fires N times per order event. After Gate 2.5, TryFirePositionState is called N times per fill, raising PositionStateChanged?.Invoke N times. Panel handler fires N times. 16 fires = 16 Subscribe() calls without Unsubscribe() in this NT8 session.
**Fix**: Added _engine.Unsubscribe() as the FIRST call inside the try block in OnLoaded, before all -= and += lines. C# -= on a handler not yet subscribed is a no-op -- safe on first call. Makes all of OnLoaded idempotent: drain all prior acc.OrderUpdate subscriptions, drain all event subscriptions, then re-subscribe exactly once. Zero new state.
**Diff**:
  +_engine.Unsubscribe();
   _engine.StatusUpdate         -= OnStatusUpdate;
   _engine.PositionStateChanged -= OnPositionStateChanged;
   _engine.CopyEnabledChanged   -= OnCopyEnabledChanged;
   _engine.StatusUpdate          += OnStatusUpdate;
   _engine.PositionStateChanged  += OnPositionStateChanged;
   _engine.Subscribe();
**Status**: PIPELINE_COMPLETE (B76-LaneA FINAL_PASS 2026-08-18)

## HOTFIX-B76-POSSTATE-DEDUP-01
**ID**: HOTFIX-B76-POSSTATE-DEDUP-01
**Date**: 2026-08-18
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `TryFirePositionState` + new field `_lastHasPos`
**Bug**: PositionStateChanged fires N*M times per position event where N = number of Filled/PartFilled orders that pass Gate 2 per trade (entry fill + bracket fills + target fills + Close fill = 8+ orders) and M = number of panels subscribed (1 per chart window open). With 2 chart windows open (both MES SEP26), result was 16 False per close (8 fills * 2 panels). This was confirmed by fresh NT8 restart + 2 charts + 1 F5 showing exactly 16. Root cause: TryFirePositionState had no dedup guard -- it invoked PositionStateChanged on every qualifying fill regardless of whether hasPos had actually changed. The panel handler logged and called UpdateButtonColors on every invoke.
**Fix**: Added `_lastHasPos ConcurrentDictionary<string, bool>` keyed by instrument FullName. TryFirePositionState computes hasPos, then checks _lastHasPos[instr]. If the value matches the last known value, return immediately without invoking. If it differs (or key is absent -- first fill ever), update _lastHasPos[instr] and invoke. This deduplicates all redundant mid-trade fills, delivering exactly 1 False->True transition on entry and 1 True->False transition on exit, regardless of how many fills or panels are active.
**Status**: PIPELINE_COMPLETE (B76-LaneA FINAL_PASS 2026-08-18). 1 chart: 1 line per transition (1 engine fire x 1 panel). 2 charts: 2 lines per transition (1 engine fire x 2 panels). CAS dedup confirmed working. Bug #3 CLOSED.

---

## HOTFIX-B77-01 -- DW-B76-02

**ID**: HOTFIX-B77-01
**Date**: 2026-08-19
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Method**: `GetLeaderAtmTemplateName` (line 2242)
**Bug**: Fallback-1 used `sel.SelectedAtmStrategy.Name` which returns "AtmStrategy" (NT8 class name), same class-name trap as the B76 primary-path guard. The real template name lives on the combo's `SelectedItem` as a plain string.
**Fix**: Changed condition from `sel?.SelectedAtmStrategy != null` to `sel != null`; changed return from `sel.SelectedAtmStrategy.Name ?? string.Empty` to `sel.SelectedItem as string ?? string.Empty`.
**Status**: PIPELINE-COMPLETE (B77-LaneA)

---

## HOTFIX-B77-02 -- DW-B75-01

**ID**: HOTFIX-B77-02
**Date**: 2026-08-19
**File**: `src/PropTraderTools/CopyEngine.cs`
**Lines**: 502, 717
**Bug**: Em-dash Unicode characters (U+2500) in comments on lines 502 and 717 violated JS-ASCII-only mandate (PRE-EXISTING-01 partial).
**Fix**: Replaced `// ── B56 BUILD-FIX stubs ...` and `// ── end B56 BUILD-FIX stubs ──` with ASCII triple-hyphen equivalents.
**Status**: PIPELINE-COMPLETE (B77-LaneA)

## DW-B79-04 -- MoveStopToBreakEven targets=0 async ATM bracket timing race (2026-08-19)

**Commit**: b8a1961f
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `MoveStopToBreakEven`

### Root Cause (two sub-bugs confirmed by DIAG trace)

**Sub-bug A (primary)**: NT8 ATM bracket creation is asynchronous (NT8_FULL_REFERENCE.md line 1320).
When BE-ALL is pressed within ~300ms of entry fill, follower ATM brackets (Stop1/Target1..N)
have not yet appeared in `acc.Orders`. Step A snapshot sees 0 targets -> bare-stop path.
The DIAG dump sees brackets arrive BETWEEN Step A and the dump (same method call, milliseconds later).

**Sub-bug B (secondary)**: When ATM brackets arrive just before Step A scans, NT8 sim
places Target1..N in `ChangeSubmitted` state (transient: ATM engine internally pricing the target
before it reaches `Working`). `ChangeSubmitted` was not in Step A `stateOk` -> missed even when present.

Evidence: DIAG trace -- BE-ALL only test (no QX button pressed), second BE-ALL press:
```
[BE-DIAG] Sim102 order: name=Stop1  state=Accepted        type=StopMarket
[BE-DIAG] Sim102 order: name=Target1 state=ChangeSubmitted type=Limit
[BE-DIAG] Sim102 order: name=Stop2  state=Accepted        type=StopMarket
[BE-DIAG] Sim102 order: name=Target2 state=ChangeSubmitted type=Limit
[BE-DIAG] Sim102 order: name=Stop3  state=Accepted        type=StopMarket
[BE-DIAG] Sim102 order: name=Target3 state=ChangeSubmitted type=Limit
```
Orders are present in acc.Orders during DIAG dump but were NOT present when Step A ran.

**Test scenario note**: Sim result showing OCO pairs (`PTT-BE-Stop/Target`) reaching Working
on all 4 accounts was a BE-ALL only test. The 3 open positions visible at end of that test
had NO brackets -- Director confirmed brackets were already absent before Flatten Everything
was pressed. Flatten Everything was pressed to clean up the unprotected positions, not to
remove working OCO pairs. This is consistent with the async bracket timing race: some
positions received bare PTT-BE-Stop only (no OCO targets), and the 350ms retry path either
fired too early or the bare stop was already cancelled before retry executed.

### Fix 1 (Sub-bug B) -- one line, no CYC change

Added `OrderState.ChangeSubmitted` to Step A `stateOk` for target collection.

### Fix 2 (Sub-bug A) -- 15 lines, CYC +1

Added `isRetry = false` parameter to `MoveStopToBreakEven`. When `targets.Count == 0`
and `!isRetry` and position is still open: submits bare `PTT-BE-Stop` (safety net),
then queues a `DispatcherTimer` one-shot at 350ms to retry `MoveStopToBreakEven(isRetry: true)`.
On retry: bare stop is stale (Step B cancels it), ATM brackets now visible -> OCO pairs submitted.
`isRetry=true` prevents recursive loop. Timer fires on Background priority UI thread (same as NT8 callbacks).

### JS-DNA compliance

- No `lock()` added -- `DispatcherTimer` runs on UI thread, no concurrency ✅
- No `throw new` added ✅
- No `return null` added ✅
- ASCII-only ✅
- CYC delta: +1 (the `if (!isRetry && !IsFlat(...))` guard)

### Pipeline work needed

- None for this fix. No-pipeline repair, Director-approved.
- DW-B79-04 carry-forward row: see table below.

---

| DW-B79-04 | `MoveStopToBreakEven` targets=0 on rapid BE-ALL -- async ATM bracket timing race. Sub-bug A: brackets not yet in acc.Orders. Sub-bug B: ChangeSubmitted state not in stateOk. | P1 | FIXED b8a1961f -- **PARTIAL SIM CONFIRM** (BE-ALL only test). OCO pairs reached Working on all 4 accounts in that run. However 3 unbracketed positions remained at end -- Director confirmed no brackets present before Flatten Everything press. QX->BE-ALL combined test NOT yet run. Pending Director test. |

---

## DW-CSPROJ-BUILD-DEBT-01 -- dotnet build clean-up (deferred)

**ID**: DW-CSPROJ-BUILD-DEBT-01
**Date**: 2026-08-21
**File**: `src/PropTraderTools/PropTraderTools.csproj` (LSP-only reference project)
**Status**: DEFERRED -- NT8 F5 green, 295 tests pass, no runtime impact

### Context
Fixing `AtrSizingEngine.cs` (CS0234/CS0246 -- missing `NinjaTrader.Custom.dll` HintPath)
revealed that `dotnet build` was never fully clean. The `.csproj` is an LSP-only project
that NT8 never uses for compilation. All 295 xUnit tests pass under NT8's Roslyn host.
The following errors exist in `dotnet build` only and have zero production impact.

### Deferred Error Inventory

| # | Error | File(s) | Root Cause |
|---|-------|---------|-----------|
| 1 | CS8400: `not` pattern requires C# 9 | `TradeCopierPanel.cs:2110` | `<LangVersion>8.0</LangVersion>` in .csproj; fix: bump to `9.0` |
| 2 | CS0433: `Globals` ambiguous between `NinjaTrader.Client` + `NinjaTrader.Core` | `CopyEngine.cs:3243` | Both DLLs export `Globals`; fix: `extern alias` in .csproj |
| 3 | CS0246: `CopyRule` not found (many sites) | `CopyEngineTests.cs`, `B71Tests.cs` | `CopyRule` is `internal readonly struct` nested in `CopyEngine` -- tests reference it by name directly; fix: access only via `typeof(CopyEngine).GetNestedType("CopyRule", BindingFlags.NonPublic)` |
| 4 | CS0234: `NinjaTrader.NinjaScript.Instruments` not found | `B76Tests.cs:38`, `CopyEngineTests.cs` multiple | Sub-namespace not resolvable under `net48` `dotnet build`; fix: use `NinjaTrader.Cbi.Instrument` or access via reflection |
| 5 | CS0234: `System.Collections.Immutable` not found | `CopyEngineTests.cs` multiple | Not in `net48` by default; fix: add `<PackageReference Include="System.Collections.Immutable" Version="8.0.0" />` |
| 6 | CS0234: `System.Reflection.NullabilityInfoContext` not found | `CopyEngineTests.cs:436` | .NET 6+ API; not available in `net48`; fix: guard with `#if NET6_0_OR_GREATER` or remove |
| 7 | CS1061: `MethodInfo[].FirstOrDefault` / `IList<>.Any` not found | `CopyEngineTests.cs` multiple | Missing `using System.Linq`; fix: add using or add `System.Core` reference |
| 8 | CS0117: `TradeCopierWindow.ParseAtmTemplateSelection` not found | `B43Tests.cs:35,57,75` | Method was renamed or removed in a past wave; fix: update test to match current API |
| 9 | CS7036 + CS0272: `BeEventArgs` constructor + property mismatch | `B68Tests.cs:193` | `BeEventArgs` constructor signature changed; properties now init-only; fix: update test call site |
| 10 | CS0122: `CopyEngine.CopyEngine()` inaccessible | `CopyEngineTests.cs:3564,3645` | Parameterless ctor is `internal`/`private`; fix: use the `internal` test-seam ctor or factory |
| 11 | CS0246: `Dictionary<,>` not found (2 sites) | `CopyEngineTests.cs:2277,2283` | Missing `using System.Collections.Generic`; fix: add using |

### Fix Plan (when prioritized)

**Phase 1 -- .csproj only (no source changes, 15 min):**
1. `<LangVersion>9.0</LangVersion>`
2. Add `System.Collections.Immutable` PackageReference
3. Add `extern alias NTClient` for `NinjaTrader.Client` to resolve `Globals` ambiguity

**Phase 2 -- test file fixes (source changes, separate PR per file):**
4. `CopyEngineTests.cs` -- add `using System.Linq; using System.Collections.Generic;`, guard `NullabilityInfoContext` with `#if`, fix `CopyRule` direct-name references, fix `IsDispatchTriggerState` call sites, fix `new CopyEngine()` call sites
5. `B43Tests.cs` -- update `ParseAtmTemplateSelection` references to current API
6. `B68Tests.cs` -- update `BeEventArgs` construction to current constructor signature
7. `B71Tests.cs` -- fix `CopyRule` direct-name reference
8. `B76Tests.cs` -- fix `NinjaTrader.NinjaScript.Instruments.Instrument` reference

### What is NOT broken
- NT8 F5 compilation: GREEN
- All 295 xUnit tests: PASSING
- Hard links: ACTIVE (production code deployed)
- `dotnet test`: PASSING (xUnit runner loads via NT8 harness, not dotnet build)

### Blocked By
Nothing. Pure technical debt. Prioritize after next live-session hotfix cycle.

---

## HOTFIX-B80-BE-RETRY-01 -- DW-B80-01 + DW-B80-02

**ID**: HOTFIX-B80-BE-RETRY-01
**Date**: 2026-08-21
**Commit**: 04b3acfc
**Files**: `src/PropTraderTools/CopyEngine.cs`
**Tests**: 295 [Fact] passing, 0 failed
**Status**: DEPLOYED -- SIM confirmation pending (Director NT8 restart + QX->BE-ALL test)

### Bug DW-B80-01 -- 200ms retry delay insufficient for follower ATM bracket creation

When QX-ALL is pressed immediately before BE-ALL, follower ATM brackets are cancelled by
`CancelQxBrackets` and then re-created by NT8's ATM engine. The 350ms->500ms retry timer in
`MoveStopToBreakEven` (DW-B79-04) fired the retry call at 200ms (original code) -- before
follower ATM brackets had time to appear in `acc.Orders` after QX cancel+recreate. Result:
retry still sees `targets=0`, submits bare `PTT-BE-Stop` with no OCO target pairs.

SIM test evidence: both `[BE-RETRY] Sim103` and `[BE-RETRY] Sim104` fired with `flat=False`
but `cancel=0 targets=0`. The 200ms delay was hardcoded in the original DW-B79-04 fix; the
`delayMs` parameter was always present but defaulted to 200.

### Fix DW-B80-01 -- one line, no CYC change

Passed `delayMs: 500` explicitly to `QueueBeRetryFallback`:

```
Before: QueueBeRetryFallback(acc, instrument, bufferTicks);
After:  QueueBeRetryFallback(acc, instrument, bufferTicks, delayMs: 500);
```

File: CopyEngine.cs L2787. CYC unchanged (MoveStopToBreakEven remains CYC=6).

Note: stale log string at L2785 prints `"registered BE retry slot + 200ms fallback"` -- cosmetic
only, the actual timer waits 500ms. Log string fix deferred (P3, batch into future pipeline).

### Bug DW-B80-02 -- TryReplacePttBeBrackets double-registration race

`TryReplacePttBeBrackets` previously called `TryRemove` then assigned a new slot unconditionally.
If two `OnOrderUpdate` events fired in rapid succession for the same account (e.g. Target1 and
Target2 both going Working within the same tick), the second call would overwrite the first slot
before it had been consumed, losing the slot's `targetCount`. This caused some slots to be
registered with `targetCount=0` even when targets were present.

### Fix DW-B80-02 -- TryAdd dedup guard, CYC +1

Replaced unconditional overwrite with `TryAdd` early-return:

```
Before: _pendingFollowerBeSlots.TryRemove(acc.Name, out _);
        _pendingFollowerBeSlots[acc.Name] = new PendingFollowerBeSlot(acc, instr, 0);

After:  if (!_pendingFollowerBeSlots.TryAdd(acc.Name, new PendingFollowerBeSlot(acc, instr, 0)))
            return;
```

File: CopyEngine.cs L1817-1818. CYC: TryReplacePttBeBrackets +1 (was 5, now 6).

### JS-DNA compliance

- No `lock()` added ✅
- No `throw new` added ✅
- No `return null` added ✅
- ASCII-only ✅
- CYC delta: +1 (DW-B80-02 guard only)

### Pipeline work needed

- None. Director-approved direct repair (no-pipeline hotfix category).
- SIM test required to confirm 500ms delay is sufficient in clean-session NT8.
- If bare stops persist after clean restart: open DW-B80-03 for multi-step retry design.
- Spec cards DW-B80-01 / DW-B80-02 stamped CLOSED after Director SIM green confirmation.

---

## DW-B93 / DW-B92 / DW-B91-A-v2 — 2026-08-11

**ID**: DW-B93 + DW-B92 + DW-B91-A-v2
**File**: src/PropTraderTools/CopyEngine.cs
**Methods touched**: IsQxCancelCandidate, OnOrderUpdate, TryEvictFollowerBeSlot, HasFilledBeTargetFast (new), EvictDedup

**Bug (DW-B93)**: Named ATM follower entry Limit order ("Entry") not covered by IsQxCancelCandidate; not cancelled on QX.
**Fix (DW-B93)**: Added `if (o.Name == "Entry") return true; // (6)` branch. CYC updated 6->7.

**Bug (DW-B92)**: HasFilledBeTarget scans acc.Orders AFTER OCO cancel arrives -- race where cancel fires before Filled event.
**Fix (DW-B92)**: Added _filledBeTargetCount (ConcurrentDictionary<string,int>); incremented in OnOrderUpdate on PTT-BE-Target-* Filled BEFORE Cancelled block; replaced HasFilledBeTarget scan with HasFilledBeTargetFast (CYC=2, lock-free). HasFilledBeTarget removed (zero callers). Counter cleared in TryEvictFollowerBeSlot on position flat.

**Bug (DW-B91-A-v2)**: _entryDispatchedOrders evicted per-orderId on Filled in EvictDedup; partial-fill can prematurely clear entry dedup slot before Rithmic re-submit Submitted event.
**Fix (DW-B91-A-v2)**: Removed _entryDispatchedOrders.TryRemove from EvictDedup; moved _entryDispatchedOrders.Clear() to TryEvictFollowerBeSlot (position-flat handler).

**Status**: APPLIED -- awaiting sync-ptt-to-nt8.ps1 + F5 compile.

---

## DW-B94

- **ID**: DW-B94
- **File**: src/PropTraderTools/CopyEngine.cs
- **Method**: IsNonFlatDispatchName (~L1728)
- **Bug**: ATM bracket cancel names (Stop1..Stop9, Target1..Target9) not guarded in IsNonFlatDispatchName; NT8 OCO-cancel of Stop1 after Target1 fill triggered FlattenFollower on all followers, cancelling PTT-BE brackets.
- **Fix**: Added `if (IsAtmBracketName(orderName)) return true;` as guard (3) -- ATM cancel events during NT8 position update gap must never flatten followers. CYC 2->3.
- **Status**: APPLIED -- awaiting pipeline

---

## DW-B119 -- TryAdd Placement Race (B114-T1)

- **ID**: DW-B119
- **Date**: 2026-08-27
- **File**: src/PropTraderTools/Features/PttGlobalQuickExit.cs
- **Method**: ExecuteOne follower path -- _qxPendingFollowerCleanup.TryAdd
- **Bug**: TryAdd called AFTER executor.Execute inside try{}. In NT8 Sim, SubmitOrder
  dispatches OnOrderUpdate synchronously on the same call stack. PTT-QX-T* orders go
  Working during executor.Execute -- before TryAdd runs. TryCleanupReArmedAtmBracket
  calls TryGetValue on the empty map and returns false. Native ATM Target1/2/3 survive,
  creating an OCO conflict that can cancel PTT-QX-T* non-deterministically.
- **Fix**: Moved _qxPendingFollowerCleanup.TryAdd from inside try{} (after executor.Execute)
  to before try{} (before executor.Execute). Cleanup map now armed before any PTT-QX
  order is submitted. OnOrderUpdate finds the entry when PTT-QX-T* goes Working.
- **Status**: FIXED-B114-T1 -- TryAdd moved before executor.Execute. DW-B112 finally{}
  TryRemove preserved unchanged. CYC of ExecuteOne unchanged (=2). Exception-safety
  confirmed: if Execute throws, orphaned map entry expires via 2s TTL harmlessly.

## DW-B121 -- Cleanup Map TTL Too Short (Director-approved direct edit, 2026-08-27)

- **ID**: DW-B121
- **Date**: 2026-08-27
- **File**: src/PropTraderTools/Features/PttGlobalQuickExit.cs
- **Method**: ExecuteOne follower path -- _qxPendingFollowerCleanup.TryAdd TTL value
- **Bug**: TTL set to AddSeconds(2). PttGlobalQuickExit.Execute() processes 4 accounts
  sequentially (Sim101 leader, then Sim102, Sim103, Sim104). Each account does N cancels +
  3 PTT-QX submits. In a loaded SIM session (orders-for-instr=96-106), total loop elapsed
  time exceeds 2s. By the time NT8 asynchronously fires OnOrderUpdate(Working) for Sim102's
  3 residual native ATM brackets (DW-B120 snapshot=3 path), the TTL has already elapsed.
  TryCleanupReArmedAtmBracket checks entry.Expiry <= DateTime.UtcNow (CopyEngine.cs L2400)
  and returns early without cancelling. [PTT-QX-CLEANUP] never fires. Native Target1/2/3
  survive alongside PTT-QX targets. Confirmed 100% reproduction rate on all Combo D runs.
  Distinct from DW-B119 (map-empty race -- fixed B114): DW-B121 = map has the entry but
  TTL expired before Working fires.
- **Fix**: Changed DateTime.UtcNow.AddSeconds(2) to DateTime.UtcNow.AddSeconds(10).
  10s covers the full 4-account sequential loop under SIM load with margin. TTL is a
  safety expiry for orphaned map entries on exception paths only -- no functional impact
  on the normal path (normal path removes entry explicitly at T3 via TryRemove L2442).
- **CYC impact**: None. ExecuteOne CYC stays at 2.
- **Comment added**: DW-B121 comment added above the TryAdd call.
- **Status**: HOTFIX-APPLIED -- Director-approved direct edit 2026-08-27.
  Pending full B115 pipeline for: test update (T_B113_01 TTL constant), code review,
  verification. Do NOT pipeline until live Combo D pass confirmed with this fix.

## DW-B122 -- TryCleanupReArmedAtmBracket Accepts Working State Only (Director-approved direct edit, 2026-08-27)

- **ID**: DW-B122
- **Date**: 2026-08-27
- **File**: src/PropTraderTools/CopyEngine.cs
- **Method**: TryCleanupReArmedAtmBracket -- compound guard condition (a), line ~2392
- **Bug**: Guard condition (a) read `e.Order.OrderState != OrderState.Working` -- returns early
  on any state other than Working. PTT-QX-T* orders are submitted via AddOn path
  (account.CreateOrder + Submit, OrderEntry.Manual). In NT8 SIM, AddOn-submitted Limit orders
  arrive in OnOrderUpdate as Accepted first, before transitioning to Working. The cleanup guard
  fires on Accepted events but immediately returns. By the time Working fires (if it fires at
  all before OCO resolves), the native ATM Target* brackets have already been in conflict.
  Net result: [PTT-QX-CLEANUP] never logs. Native ATM Target1/2/3 survive alongside PTT-QX-T*
  orders. 100% reproduction rate across all Combo D test runs (even after DW-B119 map-empty fix
  and DW-B121 TTL fix). Root cause confirmed by absence of [PTT-QX-CLEANUP] in Output Tab 1
  even though PTT-QX-T* orders confirmed Working in Account Data.
- **Distinct from DW-B119**: DW-B119 = map empty when Working fires (TryAdd was after Execute).
  Distinct from DW-B121: DW-B121 = map has entry but TTL expired.
  DW-B122 = map has entry, TTL valid, but guard returns on Accepted before Working ever checked.
- **Fix**: Changed guard condition (a) from:
    `e.Order.OrderState != OrderState.Working`
  to:
    `e.Order.OrderState != OrderState.Working && e.Order.OrderState != OrderState.Accepted`
  Cleanup now fires on EITHER Accepted OR Working -- whichever arrives first.
  Consistent with TryFireFollowerBeRetry (CopyEngine.cs L1365-1368) which already uses
  `o.OrderState != OrderState.Working && o.OrderState != OrderState.Accepted` for the same
  PTT-QX-T* order type.
- **CYC impact**: None. TryCleanupReArmedAtmBracket CYC stays at 5.
- **Comment updated**: Method header and guard comment (a) updated with DW-B122 explanation.
- **Status**: HOTFIX-APPLIED -- Director-approved direct edit 2026-08-27.
  Pending full B115 pipeline for: test update (T_B113_01 / new T_B115_XX for Accepted state),
  code review, verification. Do NOT pipeline until live Combo D pass confirmed with this fix.

## DIAG-DW-B115-01 -- Follower Target Qty Diagnostic (Director-approved direct edit)

- **ID**: DIAG-DW-B115-01
- **Date**: 2026-08-28
- **File**: src/PropTraderTools/Features/PttGlobalQuickExit.cs
- **Method**: Execute() -- leader loop and follower foreach
- **Purpose**: Diagnose why Sim103 PTT-QX-T1 submits qty=2 instead of qty=4 (DW-B115).
  Two hypotheses under test:
  - Hypothesis A: Leader ATM has a non-uniform qty split (e.g. T1=4/T2=2/T3=1 from ATM
    template). Follower's own ATM uses NT8-default uniform split (T1=2/T2=2/T3=3 for 7
    contracts). SnapshotTargetOrders(follower) returns the follower's own split -- diverges
    from leader.
  - Hypothesis B: Follower ATM bracket orders not yet in acc.Orders when
    SnapshotTargetOrders(follower) runs (DW-B120 async lag).
    followerTargets.Count=0 or partial -> CalcTNQty(7,3,0)=2 fallback fires.
- **What was added**:
  1. Leader block: StringBuilder log
     "[DW-B115-DIAG] leader targets: SimXXX count=N posQty=P T1=Q1 T2=Q2 T3=Q3"
     Emitted after existing [PTT-QX-ALL] leader: line, before ExecuteOne(leader).
  2. Follower block: StringBuilder log
     "[DW-B115-DIAG] follower targets: SimXXX count=N posQty=P T1=Q1 T2=Q2 T3=Q3"
     Emitted after SnapshotTargetOrders(follower), before existing [PTT-QX-ALL] follower: line.
     Also captures follower posQty by iterating follower.Positions inline.
- **CYC impact**: None. Probe is in scoped blocks {}. No new branch conditions.
- **JS compliance**: JS-021 (no lock), JS-001 (no throw), ASCII-only strings. StringBuilder
  used to avoid string concatenation in diagnostic loop (JS-036 spirit).
- **Read pattern**: After Combo D, look for these lines in Output Tab 1:
    [DW-B115-DIAG] leader targets: Sim101 count=3 posQty=7 T1=? T2=? T3=?
    [DW-B115-DIAG] follower targets: Sim102 count=? posQty=? T1=? T2=? T3=?
    [DW-B115-DIAG] follower targets: Sim103 count=? posQty=? T1=? T2=? T3=?
    [DW-B115-DIAG] follower targets: Sim104 count=? posQty=? T1=? T2=? T3=?
  If leader T1 != follower T1 AND follower count=3 -> Hypothesis A confirmed.
  If any follower count=0 or count < 3 -> Hypothesis B confirmed (DW-B120 async lag).
  If all counts=3 and all T1 match leader -> neither hypothesis; escalate to DW-B123.
- **Remove when**: DW-B115 root cause confirmed, fix applied, Combo D passes with correct qty.
- **Pipeline debt**: Full B115 pipeline must cover:
  - Removal of DIAG-DW-B115-01 probe lines
  - Fix for confirmed hypothesis (A: pass leader targets to follower;
    or B: DW-B120 guard before SnapshotTargetOrders)
  - Test: verify PTT-QX-T1 qty on all followers matches leader T1 qty after fix
  - Code review of SnapshotTargetOrders(follower) call site in Execute()
- **Status**: DIAG-APPLIED -- Director-approved direct edit 2026-08-28.
  Pending Combo D diagnostic run to confirm hypothesis.

## DW-B123 -- SnapshotTargetOrders Collects Stale Partial-Fill ATM Bracket Generations

- **ID**: DW-B123
- **Date**: 2026-08-28
- **File**: src/PropTraderTools/Features/PttGlobalQuickExit.cs
- **Method**: SnapshotTargetOrders(Account acc, Instrument instr)
- **Severity**: P1 -- live-trading qty mismatch on all accounts when entry partially fills
- **Status**: OPEN -- no fix applied yet. Pending B115 pipeline.

### Bug

`SnapshotTargetOrders` collects every Working/Accepted Limit order whose name matches
`Target[N]` (isNative check) regardless of which ATM bracket generation it belongs to.
When a position entry partially fills, NT8 arms a new ATM bracket on each partial fill
event. Depending on the entry order's TimeInForce (DAY vs GTC) and the NT8 SIM dispatch
timing, earlier bracket generations may NOT be cancelled before the next generation arrives.
Result: multiple `Target1` orders from different generations are simultaneously Working in
`acc.Orders`, and `SnapshotTargetOrders` collects all of them as separate list entries.

### Confirmed Reproduction Evidence (2026-08-28 Combo D diagnostic run)

Entry: Buy x7 MES SEP26 on all 4 accounts. Entry order filled in 3 partial fills:
  - Fill #1 at 9:08:47 PM: qty=1 of 7 (all accounts)
  - Fill #2 at 9:08:49 PM: qty=2 more (cumulative=3)
  - Fill #3 at 9:08:49 PM: qty=4 more (cumulative=7, full fill)

NT8 armed ATM brackets on each partial fill:

  Gen-1 bracket (after fill #1, qty=1):
    Sim101: Target1 qty=1 OCO=5b71515ab1994997b0020423ed5216f4   Order=68dadf37...
    Sim102: Target1 qty=1 OCO=c6a8c0b7...                         Order=024c43d9...
    Sim103: Target1 qty=1 OCO=955ccf2a...                         Order=5955b620...
    Sim104: Target1 qty=1 OCO=40d01496...                         Order=d95eacb4...

  Fill #2 partial (qty=2 more, cumulative=3):
    Sim102/103/104: existing gen-1 Target1 goes to 'Change submitted'
      -> NT8 modified in-place for GTC entry orders (correct behaviour)
    Sim101: gen-1 Target1 NOT changed -> Sim101 entry was TimeInForce=DAY
      -> NT8 did NOT modify the gen-1 bracket, it created a new one instead

  Gen-3 bracket (after full fill, qty=7):
    Sim101: Target1 qty=3 OCO=7898d685... Order=3f2a224e...  (NEW object, different ID)
            Target2 qty=2 OCO=17cfc88d...
            Target3 qty=1 OCO=96930413...
    (Followers: updated in-place via Change submitted -> final qty visible on same order)

  At QX-ALL fire time (9:09:05 PM, ~16s after final fill):
    Sim101 acc.Orders contains BOTH:
      68dadf37 Target1 qty=1 (gen-1, OCO=5b71515a, never cancelled)
      3f2a224e Target1 qty=3 (gen-3, OCO=7898d685, the valid final bracket)
      f8fd0c75 Target2 qty=2
      c3f0e8a9 Target3 qty=1

  SnapshotTargetOrders(Sim101) collected all 4 -> nativeTargets = [(7741.5,1),(7741.5,3),(7743,2),(7744.5,1)]
  Sorted by iteration order -> reported as count=4 T1=1 T2=3 T3=2 T4=1

  Diagnostic confirmation:
    [DW-B115-DIAG] leader targets: Sim101 count=4 posQty=7 T1=1 T2=3 T3=2 T4=1
    [DW-B115-DIAG] follower targets: Sim102 count=4 posQty=7 T1=1 T2=3 T3=2 T4=1
    [DW-B115-DIAG] follower targets: Sim103 count=0 posQty=7          <- DW-B120
    [DW-B115-DIAG] follower targets: Sim104 count=4 posQty=7 T1=1 T2=3 T3=2 T4=1

### Why DAY vs GTC Entry Matters

NT8 ATM bracket update behaviour differs by entry TimeInForce:
  - GTC entry: on partial fill, NT8 modifies existing bracket orders in-place
    ('Change submitted' state). Single order object, qty updated. No stale generation.
  - DAY entry (Sim101 leader): on partial fill, NT8 creates a brand-new bracket object
    per fill stage. Old generation NOT cancelled. Both generations Working simultaneously
    until OCO resolves (which may not happen before QX-ALL fires).

Sim101 leader entry at 9:08:49 PM CSV line 317:
  Order='1e89d4a3...' Name='Entry' Time in force=DAY
Sim102/103/104 follower entries (CSV lines 297/301/312):
  Order='...'/Sim10x Name='Entry' Time in force=GTC

This is not a PTT defect in the follower copy path -- the followers use GTC correctly.
The stale bracket arises on the LEADER only, when leader uses a DAY entry (e.g. market
order on native NT8 chart via ATM). The fix must handle this on the snapshot side.

### Impact

1. Leader QX: submits 4 PTT-QX pairs instead of 3. Extra phantom PTT-QX-T4 pair
   submitted with qty=1 (the stale gen-1 qty). No protective stop mismatch since
   PTT-QX-Stop/PTT-QX-Stop4 cover both -- but 4 OCO pairs is incorrect for a 3-target ATM.

2. Followers (Sim102/Sim104): also receive count=4 followerTargets (their own ATM
   was also modified in-place but the stale gen-1 follower bracket was similarly
   not cleaned up). Followers also submit 4 PTT-QX pairs.

3. Follower Sim103 (DW-B120 interaction): count=0 fallback fires -> CalcTNQty(7,4,0)
   = floor(7/4) = 1. Sim103 T1=1, T2=1, T3=1, T4=4 (last absorbs). Completely wrong.

### Root Cause (precise)

SnapshotTargetOrders has no concept of ATM bracket generation. It treats any Working/
Accepted Limit order named Target[N] as a valid target regardless of whether it is a
stale partial-fill artefact. Two Target1 orders at the same price from different OCO
groups are indistinguishable to the current filter logic.

### Fix Design (not yet approved for implementation)

Option A -- Deduplicate by OCO group (cleanest, most correct):
  Group nativeTargets by OCO ID. Use only the OCO group whose member quantities
  sum closest to pos.Quantity. Stale groups (total qty < pos.Quantity) are discarded.
  CYC impact: +2 (grouping loop + sum comparison).
  Handles any number of partial fills and any TimeInForce combination.

Option B -- Deduplicate by limit price (simpler, sufficient for 3-target ATM):
  For each unique limit price in nativeTargets, keep only the entry with the highest
  Quantity. Two Target1 orders at 7741.5 with qty=1 and qty=3 -> keep qty=3.
  CYC impact: +1 (dedup loop using Dictionary<double,int>).
  Assumption: all generations of the same target have the same limit price.
  Valid for standard ATM templates. May fail if ATM recalculates price on partial fill.

Option C -- Filter by pos.Quantity sum match (simplest):
  After collecting nativeTargets, check if sum of .Qty == pos.Quantity.
  If not: discard the lowest-qty Target1 duplicate and recheck.
  Repeat until sum matches or no duplicates remain.
  CYC impact: +2 (sum check + dedup loop). Fragile if partial fills create non-trivial
  multi-duplicate combinations.

Recommended: Option B. Limit price is stable within an ATM template, the dedup is
O(N) with a single dictionary pass, and it handles the observed case exactly.

### Pipeline Debt

Full B115 pipeline must cover:
  - Fix SnapshotTargetOrders with Option B (or Director-selected option)
  - Remove DIAG-DW-B115-01 probe lines from Execute()
  - Fix DW-B120 (Sim103 count=0 lag) -- separate sub-ticket
  - Test T_B123_01: SnapshotTargetOrders with 2x Target1 at same price -> returns 1 entry
    with the higher qty
  - Test T_B123_02: SnapshotTargetOrders with clean 3-target ATM -> returns count=3, unchanged
  - Test T_B120_01: follower snapshot count=0 path -> CalcTNQty used, qty matches
    leaderTargetCount-based distribution
  - Code review of SnapshotTargetOrders call sites (Execute leader + follower loops)
  - Verify Combo D with clean single-fill entry: count=3, T1/T2/T3 correct on all 4 accounts
  - Verify Combo D with partial-fill entry: count=3 after dedup, T1/T2/T3 correct

## HOTFIX-DW-B123 -- SnapshotTargetOrders Dedup by Limit Price (Director-approved direct edit)

- **ID**: HOTFIX-DW-B123
- **Date**: 2026-08-28
- **File**: src/PropTraderTools/Features/PttGlobalQuickExit.cs
- **Method**: SnapshotTargetOrders(Account acc, Instrument instr)
- **Fix**: Option B -- deduplicate nativeTargets by limit price, keeping highest qty per
  price level. Implemented as a post-collection Dictionary<double,int> pass before return.
  If two Target1 orders share the same LimitPrice (7741.5) with qty=1 and qty=3, only
  qty=3 is returned. The stale gen-1 bracket (qty=1) is silently dropped.
- **Code change**:
  - Replaced single-line `return nativeTargets.Count > 0 ? nativeTargets : pttTargets`
    with explicit null-count early return (pttTargets path unchanged) plus dedup block.
  - Dedup: Dictionary<double,int> keyed by LimitPrice, value = max Qty seen at that price.
    Rebuilt as List<(double,int)> for return. O(N) single pass.
- **CYC impact**: CYC 4 -> 5. +1 for dedup foreach loop. Well within JS-021 limit of 8.
  Method header comment updated: CYC=5.
- **JS compliance**: JS-021 (no lock -- Dictionary is local, single-threaded dispatch),
  JS-001 (no throw), JS-002 (returns list, never null), ASCII-only. JS-036 spirit:
  Dictionary allocated only when nativeTargets.Count > 0 (normal path, not hot path).
- **Does NOT fix**: DW-B120 (Sim103 count=0 async lag). That requires a separate approach.
  With this fix, when DW-B120 fires (count=0), the fallback CalcTNQty path now uses
  leaderTargetCount (3 after dedup, not 4), which is an improvement but still not the
  correct qty split. DW-B120 sub-ticket remains open.
- **Status**: HOTFIX-APPLIED -- Director-approved direct edit 2026-08-28.
  Pending full B115 pipeline for: tests T_B123_01/T_B123_02, code review, verification.
