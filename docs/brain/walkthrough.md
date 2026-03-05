# Walkthrough: Phase 5 Modularization

The project has successfully transitioned from a monolith architecture to a **Module-Based Architecture**. This partitioning allows for higher-precision AI audits and faster maintenance.

## Changes Made

### 📁 UI Monolith Partitioned
The 2,246-line `UniversalORStrategyV12_002_Dev.UI.cs` has been split into:
- [UI.IPC.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.UI.IPC.cs): TCP Server, Client Handlers, and Command Dispatcher.
- [UI.Compliance.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.UI.Compliance.cs): Apex Compliance Hub, P/L tracking, and Health Logging.
- [UI.Sizing.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.UI.Sizing.cs): ATR Auto-Sizing Engine and Target Distribution logic.
- [UI.Callbacks.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.UI.Callbacks.cs): Hotkeys and Chart UI events.

### 📁 Orders Monolith Partitioned
The 2,024-line `UniversalORStrategyV12_002_Dev.Orders.cs` has been split into:
- [Orders.Callbacks.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.Orders.Callbacks.cs): Core event handlers (`OnOrderUpdate`, `OnPositionUpdate`).
- [Orders.Management.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.Orders.Management.cs): Bracket submission, Cleanup, and Orphan reconciliation.

## Phase 7: Concurrency Hardening (The Final Green Light)
The strategy has been hardened against thread-race conditions:
- **Callback Marshalling**: `OnAccountExecutionUpdate` now enqueues events and processes them on the strategy thread via `TriggerCustomEvent`.
- **Broker Hydration**: The system now reads live positions from the broker on startup to prevent "Phantom Desyncs."
- **Tick Standard**: All entry and bracket prices are now strictly rounded to `RoundToTickSize`.

## Phase 7: Entries Monolith Partition (V12.Phase7)
The 1,806-line `Entries.cs` has been surgically partitioned into 6 mode-specific entry nodes:
- [Entries.FFMA.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.Entries.FFMA.cs): CheckFFMAConditions, ExecuteFFMAEntry, DeactivateFFMAMode, ExecuteFFMALimitEntry, ExecuteFFMAManualMarketEntry (5 methods)
- [Entries.OR.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.Entries.OR.cs): ExecuteLong, ExecuteShort, EnterORPosition, CalculateORStopDistance (4 methods)
- [Entries.RMA.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.Entries.RMA.cs): ExecuteTrendSplitEntry, GetRmaAnchorPrice, ExecuteRMAEntry, ExecuteRMAEntryCustom, ActivateRMAMode, DeactivateRMAMode (6 methods)
- [Entries.MOMO.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.Entries.MOMO.cs): ExecuteMOMOEntry, ActivateMOMOMode, DeactivateMOMOMode (3 methods)
- [Entries.Trend.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.Entries.Trend.cs): ExecuteTRENDEntry, CreateTRENDPosition, ActivateTRENDMode, DeactivateTRENDMode, ExecuteTRENDManualEntry (5 methods)
- [Entries.Retest.cs](file:///C:/WSGTA/universal-or-strategy/src/UniversalORStrategyV12_002_Dev.Entries.Retest.cs): ExecuteRetestEntry, ActivateRetestMode, DeactivateRetestMode, ExecuteRetestManualEntry (4 methods)

`Entries.cs` reduced to an empty partial class stub — all 27 entry methods distributed with 1:1 logic parity. Method audit confirmed zero duplicates and zero omissions.

## Verification Results
- **File Geometry**: Verified 22 total source files in `src/` (16 original + 6 new entry nodes).
- **Method Audit**: 27 unique entry methods across 6 node files — no duplicates, no omissions.
- **Integrity Check**: Partial class structure preserved across all 20 partial nodes.
- **Git State**: Commit `103628c` confirms pre-partition baseline is versioned.
- **Compilation**: ✅ **PASSED** (User confirmed F5 in NinjaTrader).
- **Stability Rating**: Pending compile verification.

## Next Steps
1. **Compile**: Press F5 in NinjaTrader to verify Phase 7 partial class structure compiles.
2. **Live Deployment**: Deploy to a single PA account for "Live Smoke Test."
3. **Performance Audit**: Begin tracking P/L symmetry across the 20-account fleet.

## Build 950: OCO Cascade Fix -- Resilient Bracket Replacement FSM

### Problem
When UpdateStopOrder cancelled a follower stop for BE/trail replacement, broker-native OCO
(OcoGroupId shared across stop + all targets) auto-cancelled T1/T2/T3. Simultaneously,
follower stop-cancel events arrive via OnAccountOrderUpdate -- which only checked
_followerReplaceSpecs (entry FSM), NOT pendingStopReplacements. Result: no new stop for
followers, no targets either. V8.30 5-second timeout eventually fired an emergency stop but
with no OCO group and no targets -- naked bracket.

### Fix: Two-Part Resilient Bracket Replacement FSM

**Part 1 -- Follower stop black hole (HandleMatchedFollowerOrder):**
Added pendingStopReplacements lookup in HandleMatchedFollowerOrder (Orders.Callbacks.cs).
When a follower stop cancel matches OldOrder, CreateNewStopOrder is called immediately --
same logic as HandleOrderCancelled does for master accounts.

**Part 2 -- OCO cascade target restoration (RestoreCascadedTargets):**
Extended PendingStopReplacement with CapturedTargets[] (TargetSnapshot array: TargetNum,
Price, Qty, Order ref). Populated in UpdateStopOrder before cancel is issued.
After new stop is created (on any path: normal callback, follower callback, V8.30 timeout),
RestoreCascadedTargets() is scheduled via TriggerCustomEvent. It checks each captured
Order.OrderState -- if Cancelled, the target was OCO-cascade-killed and is re-submitted
with the same OcoGroupId and same price/qty.

**Part 3 -- CreateNewStopOrder OcoGroupId fix:**
New stop now includes pos.OcoGroupId so it re-enters the broker OCO bracket. Restored
targets also use OcoGroupId -- full bracket linkage is restored.

### Files Changed
- src/V12_002.cs -- TargetSnapshot class, PendingStopReplacement extended, BUILD_TAG = "950"
- src/V12_002.Trailing.cs -- target snapshot in UpdateStopOrder, restore in V8.30 timeout
- src/V12_002.Orders.Callbacks.cs -- follower stop handler + master bracket restore
- src/V12_002.Orders.Management.cs -- RestoreCascadedTargets(), CreateNewStopOrder OcoId fix

### Verification
1. Sim session: Enter 4-contract position, verify bracket (Stop + T1/T2/T3) all Working
2. Send BE_CUSTOM via IPC -- confirm logs show "[B950] Target T1 restored", "[B950] Target T2 restored"
3. Confirm new stop in stopOrders[entryName], new targets in target1Orders/target2Orders
4. REAPER must NOT fire emergency stop (no naked position)
5. Let T1 fill -- confirm stop reduces to 3 contracts, T2/T3 still Working
6. Let stop fill -- confirm remaining targets cancelled by existing manual OCO loop

## Build 951.4: OCO Cascade Fix + P1 Follower Cancel Routing in UpdateStopQuantity

### Problem

Two bugs in `UpdateStopQuantity` (Orders.Management.cs) that survived the 951.1-3 remediation wave.
Both affect the target-fill -> stop-size-reduction path.

**Bug 1 -- OCO Cascade (missing target snapshot):**
When T1 fills, `UpdateStopQuantity` creates a `PendingStopReplacement` and cancels the stop
for qty reduction. It never populated `CapturedTargets` or `BracketRestorationNeeded`. The broker
OCO cascade cancels remaining targets (T2-T5) in response to the stop cancel, but since
`BracketRestorationNeeded = false`, `RestoreCascadedTargets` is never called. All remaining
targets were permanently lost after the first target fill.

Build 950 fixed the identical problem in `UpdateStopOrder` (trailing/BE path). The
`UpdateStopQuantity` (target-fill path) was missed in that wave.

**Bug 2 -- P1 Logic (wrong cancel API for followers):**
`CancelOrder(currentStop)` (line 450 in 951.3) is the NinjaScript-managed cancel API. It only
works for orders submitted via `SubmitOrderUnmanaged()` (master account). Fleet follower stops
are submitted via `acct.Submit()` (broker API). Calling `CancelOrder()` on a follower stop
silently does nothing -- the pending replacement was registered but the old stop was never
cancelled at the broker, so the confirmation callback never fired and the replacement stop
was never created. Result: follower stop remained at full size after T1 fill.

Build 925 P1 established this routing pattern in `RequestStopCancelLifecycleSafe`.
`UpdateStopQuantity` was not updated at that time.

### Fix

Both fixes applied surgically to `UpdateStopQuantity` in Orders.Management.cs:

**Part 1 -- Target snapshot (OCO cascade):**
After `pendingStopReplacements.TryAdd()` and before the cancel call, added a Build 950-style
target snapshot block that populates `newPending.CapturedTargets` and sets
`newPending.BracketRestorationNeeded = true`. The existing `HandleOrderCancelled` and
`HandleMatchedFollowerOrder` callbacks already call `RestoreCascadedTargets` when
`BracketRestorationNeeded && CapturedTargets != null` -- no changes needed there.

**Part 2 -- P1 cancel routing:**
Replaced `CancelOrder(currentStop)` with an account-aware branch:
- Follower (`pos.IsFollower && pos.ExecutingAccount != null`): `pos.ExecutingAccount.Cancel(new[] { currentStop })`
- Master/local: `CancelOrder(currentStop)` (unchanged)

### Files Changed

- src/V12_002.Orders.Management.cs -- UpdateStopQuantity: target snapshot block + P1 cancel routing
- src/V12_002.cs -- BUILD_TAG = "951.4"

### Verification

1. Compile: F5 in NinjaTrader -- zero errors expected
2. T1 Fill -> T2/T3 Restore (OCO cascade):
   - Enter 4-contract position (T1=1, T2=1, T3=1, stop=4 contracts)
   - Let T1 fill -- confirm logs: "[B950] Target T2 restored", "[B950] Target T3 restored"
   - Confirm stopOrders holds a 3-contract replacement stop
3. Follower Stop Reduce (P1 routing):
   - Enable SIMA, enter position on follower fleet account
   - Let T1 fill -- confirm follower stop cancel is routed via ExecutingAccount.Cancel()
   - Confirm 3-contract replacement stop appears in follower account
4. Master account regression: T1 fill on master -- CancelOrder() still used (not Account.Cancel)
