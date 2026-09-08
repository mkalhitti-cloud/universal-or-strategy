# DW-LB-FL-01 Architecture Plan v4

**Defect ID**: DW-LB-FL-01
**Severity**: P1
**Status**: PLAN_V4_COMPLETE
**Author**: ptt-architect (Phase 3 re-architecture -- v4)
**Date**: 2026-09-09
**Output file**: `docs/brain/DW-LB-FL-01/02-architecture-plan-v4.md`
**Supersedes**: `docs/brain/DW-LB-FL-01/02-architecture-plan-v3.md` (v3 -- BUILD_PASS, SIM_FAIL)
**Baseline**: v1 + v2 + v3 fixes are preserved and NOT reverted

---

## Rules Catalog Gate Result

**GATE RESULT: PASS**

P0 rules confirmed no-violation for this fix:

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No lock() in new/modified code | PASS -- ConcurrentDictionary.TryGetValue is lock-free |
| JS-001 | No throw in hot path | PASS -- no throw in any modified code |
| JS-002 | No return null | PASS -- methods return void/bool |
| JS-033 | No async void | PASS -- synchronous void |
| JS-036/037 | No heap alloc in hot path | PASS -- TryGetValue is allocation-free |

---

## LANE-SPLIT GATE RESULT

**SINGLE-PIPELINE**

One defect, one fix. Two methods modified/added (FlattenIfNotArming + IsInEntryFillDebounceWindow).
Single ticket. Single PR.

---

## 1. Source Readings Performed

| Method | Location | CYC |
|--------|----------|-----|
| `FlattenIfNotArming` | L5177-5185 | 2 |
| `IsAccountFlattenable` | L5222-5236 | 4 |
| `HasInflightFlatten` | L5240-5252 | 4 |
| `HasArmingAtmBrackets` | L5267-5285 | 5 |
| `TryNakedDetect` | L7226-7245 | 4 (v3) |
| `IsPttCopyEntry` | L7206-7207 | 2 |
| `NakedPositionDetector` | L7255-7278 | 6 |
| `HasNakedPosition` | L7288-7302 | 4 |
| `IsNakedConditionMet` | L7308-7321 | 4 |
| `DispatchCopy` | L2395-2469 | 6 |
| `DispatchToFollower` | L2543-2582 | 3 |
| `ShouldSkipFollowerDispatch` | L2501-2508 | 2 |
| `TryCancelFollowerEntries` | L1977-1998 | 4 |
| `CancelScopedFollowerEntries` | L2026-2050 | 5 |
| `TryReplaceOnAtmCancel` | L885-894 | 4 |
| `ReplaceFollowerCopyOnAtmCancel` | L4261-4290 | 4 |
| `IsReplaceDispatchEligible` | L4336-4352 | 5 |
| `HasWorkingPttCopy` | L4761-4777 | 3 |
| `OnOrderUpdate` (hot path) | L1500-1597 | 8 |
| `FlattenOneAccount` | L5203-5211 | 2 |
| `DrainThenDispatch` | L7362-7410 | 4 |
| `PendingDispatchDrain` | L7582-7621 | -- |
| `_nakedDetectLastQueuedTicks` | L373-374 | -- |
| `_drainOwnedOrderIds` | L385-386 | -- |
| `_pendingDispatchDrains` | L379-380 | -- |

**Critical call chain confirmed:**
```
OnOrderUpdate (L1541) -> TryNakedDetect -> NakedPositionDetector -> Dispatcher.InvokeAsync(FlattenIfNotArming)
FlattenIfNotArming -> [v4: debounce check] -> HasArmingAtmBrackets -> FlattenOneAccount
FlattenOneAccount -> IsAccountFlattenable -> CancelAllAccountOrders -> SubmitMarketFlattenOrder("PTT-Flatten")
```

---

## 2. Diagnostic Answers (Q1-Q6)

### Q1: What triggered the Entry:Cancelled visible in the Sim102 log?

**Answer: TryCancelFollowerEntries (leader Trade #1 cancelled by user).**

When the leader cancelled their first trade (Trade #1), `TryCancelFollowerEntries` fired in
`OnOrderUpdate`. `CancelScopedFollowerEntries(leaderOrderId1)` issued `acc.Cancel({followerEntry})`
for Sim102's "Entry" order. This produced `Entry:Cancelled` on Sim102.

Note: `CancelScopedFollowerEntries` does NOT write to `_drainOwnedOrderIds` -- this is a
direct `acc.Cancel()` call, not the Drain path.

### Q2: After Entry:Cancelled, what caused the new Entry:Filled on Sim102?

**Answer: DispatchCopy fired for the leader's second trade (Trade #2).**

After Trade #1 was cancelled, the leader entered a second trade (Trade #2). `DispatchCopy`
dispatched a new "Entry" to Sim102 via `SendCopyWithAtm`. That "Entry" filled on Sim102.

`TryReplaceOnAtmCancel` also fired for the Trade #1 `Entry:Cancelled` event. However,
`IsReplaceDispatchEligible` check (4) blocks replacement when `HasWorkingPttCopy(Sim102)=true`
(the Trade #2 "Entry" was already Working/Accepted). So the replacement path was blocked. ✓

### Q3: Why did Sim102 have TWO dispatch sequences in the log?

**Answer: Two separate leader trades -- not a retry mechanism.**

The first `[PTT-COPY] dispatch: Buy x7 -> Sim102` corresponds to Trade #1. The second
corresponds to Trade #2. The `_entryDispatchedOrders` and `_dedupCache` dedup guards
prevent double-dispatch for the SAME leader order. Different leader orders (different orderId)
both trigger DispatchCopy independently.

### Q4: When Trade #2 Entry filled on Sim102, what triggered PTT-Flatten:Submitted?

**Answer: A stale FlattenIfNotArming Dispatcher callback queued by NakedPositionDetector
during the PRIOR TRADE's cancel storm.**

This is Race 4. Full analysis in Section 3 below.

### Q5: Does HasInflightFlatten exist and does DispatchCopy check it?

**Answer: YES to HasInflightFlatten. NO to DispatchCopy checking it.**

`HasInflightFlatten` (L5240) is called by `IsAccountFlattenable` (L5224) inside `FlattenOneAccount`.
It checks whether a PTT-Flatten order is already in Submitted/Accepted/Working state -- this
is the in-flight duplicate-flatten guard. It does NOT gate DispatchCopy.

`DispatchCopy` has NO check for inflight flatten. The DispatchCopy path is:
Gate 0.5 (PTT- cascade) -> Gate 3 (dispatch trigger state) -> Gate 4 (order type) -> Gate 5 (dedup).
None of these gates check for an inflight flatten on follower accounts. This is by design --
the flatten is asynchronous and DispatchCopy has no visibility into it at dispatch time.

The key point: `HasInflightFlatten` is NOT the missing guard. The missing guard is inside
`FlattenIfNotArming` (the callback that fires the flatten), not in DispatchCopy.

### Q6: Is there a _flattenInFlight per-account variable? Does DispatchCopy check it?

**Answer: NO per-account volatile _flattenInFlight field. Inflight flatten is detected via
acc.Orders scan (HasInflightFlatten) only at FlattenOneAccount time, not at dispatch time.**

There is no `_flattenPending` or `_flattenInFlight` dictionary. The existing `HasInflightFlatten`
method scans `acc.Orders` for a PTT-Flatten order in Submitted/Accepted/Working state. This
check runs inside `IsAccountFlattenable` which runs inside `FlattenOneAccount` -- i.e., only
at the time the flatten is being executed, not before the entry is dispatched.

---

## 3. Root Cause Statement for Race 4

**Race 4 -- Stale FlattenIfNotArming Dispatcher callback survives across trade cycles and
fires during the next trade's ATM bracket arm window.**

### Mechanism

`NakedPositionDetector` (L7255) queues flatten callbacks via `Dispatcher.InvokeAsync`:
```csharp
Dispatcher.InvokeAsync(() => FlattenIfNotArming(acct, instr));
```

The WPF Dispatcher queue is FIFO. When the UI thread is busy (TP4-SFB log output, panel
rendering, chart updates), callbacks can queue up and execute seconds later.

**The v3 debounce stamp prevents NakedPositionDetector from QUEUING NEW callbacks** within
500ms of an entry fill. But v3 does NOT prevent an **already-queued callback** from executing.

### Cross-Trade Callback Survival

```
T_prev:  Prior trade cancel storm. FlattenIfNotArming callback queued in Dispatcher.
         v3 stamp NOT set at this point (no entry fill event on Sim102 yet for this cycle).

T0:      PTT-Flatten:Filled (prior trade closed). Sim102 flat.
         Stale cancel acks: Stop1:Cancelled, etc. Each fires NakedPositionDetector.
         HasNakedPosition: position=0 -> returns early. No NEW callbacks queued. (v3 debounce blocked by position=0)
         
T1:      Trade #1: DispatchCopy -> "Entry" (PTT-Copy limit / Named "Entry") submitted to Sim102.
         Sim102 still flat (entry pending, not filled).

T2:      Trade #1 cancelled. TryCancelFollowerEntries -> CancelScopedFollowerEntries -> Entry:Cancelled.
         TryNakedDetect: OrderState=Cancelled, v2 guard misses. NakedPositionDetector: position=0 -> returns early.

T3:      Trade #2: DispatchCopy fires. New "Entry" submitted to Sim102.

T4:      Entry:Filled (Trade #2). v3 TryNakedDetect stamps _nakedDetectLastQueuedTicks["Sim102"] = T4.
         ATM.StartAtmStrategy called. Brackets submitted to broker.
         In acc.Orders: Stop1/Stop2/Stop3/Target1/Target2/Target3 begin appearing.

T5:      UI thread runs the STALE FlattenIfNotArming callback from T_prev.
         (Dispatcher queue was backlogged; this callback waited from T_prev until T5)
         FlattenIfNotArming(Sim102, instr):
           HasArmingAtmBrackets(Sim102): scans acc.Orders for Stop/Target in arming states.
           -- If Stop1 not yet visible in acc.Orders (NT8 hasn't registered it yet): returns false.
           -- FlattenOneAccount fires -> CancelAllAccountOrders -> Stop1 transitions to CancelSubmitted.
           -- SubmitMarketFlattenOrder -> PTT-Flatten:Submitted.

T6+:     Stop1 ack arrives from broker: Stop1:CancelSubmitted (it was Working, now being cancelled).
         Target1:Working, Stop2:Accepted, Target2:Working (later acks from broker).
         PTT-Flatten:Submitted (already in queue at NT8).
         Stop3:Accepted, Target3:Working.
```

**The log matches this sequence exactly.**

### Why FlattenIfNotArming Did Not Check the Debounce Clock

`FlattenIfNotArming` contains only `HasArmingAtmBrackets` as its guard. v3 writes the
debounce clock in `TryNakedDetect` (NT8 background thread) and `NakedPositionDetector`
reads it before queuing. Neither of these affects an already-queued callback. The callback
closure captures `acct` and `instr` but NOT the debounce timestamp.

**This is the gap. The fix is to read the debounce clock INSIDE FlattenIfNotArming.**

---

## 4. Why Sim103 and Sim104 Survived

Both Sim103 and Sim104 show `Entry:Cancelled, Entry:Filled` in their log tails but with
clean bracket arming (`Stop1:Submitted, Target1:Submitted`).

Sim103 and Sim104 are at higher follower indices in the CopyRule (Sim102=index 0, Sim103=1,
Sim104=2). Their stale `FlattenIfNotArming` callbacks were queued slightly later (each
NakedPositionDetector fires per-account). By the time the UI thread ran their callbacks:

- **Option A**: Their callbacks ran BEFORE their Trade #2 entries filled (position=0 ->
  `FlattenOneAccount` skipped by `IsAccountFlattenable` position check). ✓

- **Option B**: Their callbacks ran AFTER their ATM brackets appeared in `acc.Orders` at
  `Initialized` state (fast broker ack for Sim103/Sim104 vs slow for Sim102). ✓

Sim102 was uniquely affected because its stale callback ran in a precise timing window:
AFTER its Trade #2 entry filled (position > 0) but BEFORE its ATM brackets appeared in
`acc.Orders` (NT8 registration lag). This narrow window is non-deterministic and varies
with UI thread load and broker ack latency.

---

## 5. Why v1+v2+v3 Are Correct but Incomplete

| Version | Race Targeted | Fix | Status |
|---------|--------------|-----|--------|
| v1 | Race 1: cancel-storm Dispatcher callback sees no brackets | HasArmingAtmBrackets guard in FlattenIfNotArming | Correct, preserved |
| v2 | Race 2: entry fill event fires NakedPositionDetector before brackets arm | IsPttCopyEntry guard in TryNakedDetect (skip entry fills) | Correct, preserved |
| v3 | Race 3: stale cancel ack from previous trade fires NakedPositionDetector in entry fill window | Stamp debounce clock at entry fill time (blocks new callback queues) | Correct, preserved |
| v4 (NEW) | Race 4: stale FlattenIfNotArming callback from a PRIOR TRADE cycle executes during current trade bracket arm window | Read debounce clock INSIDE FlattenIfNotArming callback | **TO BE IMPLEMENTED** |

v3 blocked Race 3 by preventing new NakedPositionDetector callbacks from being queued.
Race 4 is different: the callback was queued in an EARLIER trade cycle and survived through
multiple trade transitions in the WPF Dispatcher queue. The v3 debounce stamp was not yet
written (it was written at T4 for Trade #2) when the stale callback was queued (T_prev).
Reading the debounce clock INSIDE the callback closes the remaining gap.

---

## 6. v4 Fix Design

### 6.1 Strategy

**Minimal change (1 extracted helper + 2 lines inside existing FlattenIfNotArming):**

Extract `IsInEntryFillDebounceWindow(Account acct)` which reads `_nakedDetectLastQueuedTicks`
(the same dict written by v3). Add this as the FIRST guard in `FlattenIfNotArming`. A stale
callback that runs within 500ms of the last entry fill is suppressed before reaching
`HasArmingAtmBrackets` or `FlattenOneAccount`.

**No new fields. No new state. CYC delta: FlattenIfNotArming 2 -> 3 (+1 branch). New helper CYC=2.**

### 6.2 New Helper: IsInEntryFillDebounceWindow

```csharp
// DW-LB-FL-01-V4: returns true if acct is in the entry-fill bracket-arm window.
// Reads _nakedDetectLastQueuedTicks -- the debounce clock stamped by TryNakedDetect v3
// when an "Entry":Filled or "PTT-Copy":Filled event fires on this follower account.
// Returns false when no stamp exists (first trade, account reconnect) -- safe default.
// Called from FlattenIfNotArming to suppress stale Dispatcher callbacks from prior cycles.
// CYC=2: TryGetValue branch(1) + now-last comparison(1).
// JS-021: TryGetValue on ConcurrentDictionary is lock-free. JS-001: no throw. JS-002: returns bool.
// ASCII-only. No DateTime.Now (uses Environment.TickCount same as NakedPositionDetector L7263).
private bool IsInEntryFillDebounceWindow(Account acct)
{
    if (!_nakedDetectLastQueuedTicks.TryGetValue(acct.Name, out long last)) // (1)
        return false;
    long now = (long)(int)Environment.TickCount;
    return now - last < 500L; // (2)
}
```

### 6.3 Modified Method: FlattenIfNotArming

**Source location**: `src/PropTraderTools/CopyEngine.cs` L5177-5185

**Current v3/v1 code**:
```csharp
private void FlattenIfNotArming(Account acct, Instrument instr)
{
    if (HasArmingAtmBrackets(acct, instr))
    {
        StatusUpdate?.Invoke(acct.Name + ": flat-guard: bracket-arm skip");
        return;
    }
    FlattenOneAccount(acct, instr);
}
```

**v4 change** (add IsInEntryFillDebounceWindow as first guard):
```csharp
// DW-LB-FL-01-V4: stale-callback debounce guard added as first check.
// A FlattenIfNotArming callback queued by NakedPositionDetector during a PRIOR trade's
// cancel storm can survive in the WPF Dispatcher queue for seconds. When the UI thread
// runs it, HasArmingAtmBrackets may return false if new ATM brackets are not yet visible
// in acc.Orders (bracket arm window). Fix: if _nakedDetectLastQueuedTicks[acc.Name] was
// written within 500ms (same GraceMs as NakedPositionDetector), this account is in the
// entry-fill bracket-arm window -- suppress the flatten regardless of bracket state.
// Uses same debounce clock that TryNakedDetect v3 stamps on every "Entry":Filled event.
// CYC: 2 -> 3 (+1 branch for debounce check). PASS <= 8.
// JS-021: IsInEntryFillDebounceWindow uses ConcurrentDictionary.TryGetValue -- lock-free.
private void FlattenIfNotArming(Account acct, Instrument instr)
{
    if (IsInEntryFillDebounceWindow(acct)) // (1) DW-LB-FL-01-V4: stale callback guard
    {
        StatusUpdate?.Invoke(acct.Name + ": flat-guard: debounce-skip");
        return;
    }
    if (HasArmingAtmBrackets(acct, instr)) // (2)
    {
        StatusUpdate?.Invoke(acct.Name + ": flat-guard: bracket-arm skip");
        return;
    }
    FlattenOneAccount(acct, instr); // (3, no branch)
}
```

**McCabe**: CYC=3 (base 1 + IsInEntryFillDebounceWindow return branch + HasArmingAtmBrackets return branch). **PASS <= 8**.

### 6.4 How v4 Suppresses Race 4

```
T_prev:  NakedPositionDetector queues FlattenIfNotArming callback. Dispatcher queue backlogged.
T4:      Entry:Filled (Trade #2). v3 stamps _nakedDetectLastQueuedTicks["Sim102"] = T4.
T5:      UI thread runs stale FlattenIfNotArming callback from T_prev.
         IsInEntryFillDebounceWindow("Sim102"):
           TryGetValue -> last = T4
           now = T5, T5 - T4 = ~50-200ms < 500ms -> returns true
         -> "flat-guard: debounce-skip" logged
         -> return (no FlattenOneAccount, no PTT-Flatten) **FIXED**
T6+:     Brackets arm normally. Stop1:Working, Target1:Working, etc.
T500:    Debounce window expires. Normal NakedPositionDetector behavior resumes.
```

### 6.5 Interaction with v1+v2+v3

| Existing Guard | Location | Status in v4 |
|----------------|----------|-------------|
| v1: HasArmingAtmBrackets | FlattenIfNotArming (second guard) | PRESERVED -- still active as belt-and-suspenders |
| v2: IsPttCopyEntry | TryNakedDetect L7236 | PRESERVED -- unchanged |
| v3: _nakedDetectLastQueuedTicks stamp | TryNakedDetect L7240-7241 | PRESERVED -- v4 READS this same value |
| v4: IsInEntryFillDebounceWindow | FlattenIfNotArming (first guard) | NEW -- closes Race 4 |

v4 and v3 are symbiotic: v3 WRITES the debounce clock at entry fill time; v4 READS it inside
the callback. The two methods work on different threads (v3 runs on NT8 account bg thread; v4
runs on WPF UI thread) but access the same ConcurrentDictionary atomically.

### 6.6 Summary of Changes vs v1+v2+v3

| Method | Change | CYC Before | CYC After |
|--------|--------|------------|-----------|
| `FlattenIfNotArming` | v4: add IsInEntryFillDebounceWindow as first guard | 2 | 3 |
| `IsInEntryFillDebounceWindow` | NEW helper: TryGetValue + 500ms comparison | -- | 2 |
| `TryNakedDetect` | Unchanged (v3) | 4 | 4 |
| `IsPttCopyEntry` | Unchanged (v2) | 2 | 2 |
| `HasArmingAtmBrackets` | Unchanged (v1+v2 Initialized) | 5 | 5 |
| `NakedPositionDetector` | Unchanged | 6 | 6 |

**Files modified**: `src/PropTraderTools/CopyEngine.cs` (1 file only).

---

## 7. Preserved Invariants

| Guard | Location | Status |
|-------|----------|--------|
| v1 HasArmingAtmBrackets | FlattenIfNotArming (second guard) | PRESERVED |
| v2 IsPttCopyEntry entry-fill skip | TryNakedDetect L7236 | PRESERVED |
| v3 debounce clock stamp | TryNakedDetect L7240-7241 | PRESERVED (READ by v4) |
| HasInflightFlatten in-flight skip | IsAccountFlattenable L5224 | PRESERVED -- unchanged |
| DW-LB-FL-02 IsNativeExitOnFlatLeader | TryDispatchLeaderFlat L4710 | PRESERVED -- unchanged |
| DW-B94 IsNonFlatDispatchName | TryDispatchLeaderFlat L4708 | PRESERVED -- unchanged |
| DW-B65-01 bypass | TryDispatchLeaderFlat L4712 | PRESERVED -- unchanged |

---

## 8. Risk Assessment

### PRIMARY RISK: Legitimate naked position in entry-fill window (same as v2/v3)

**Scenario**: Entry fills. Brackets arm fully. A fill event closes all brackets within 500ms.
Position still open. A separate naked-position event fires within 500ms.
v4 debounce inside FlattenIfNotArming suppresses the naked detect for up to 500ms.

**Mitigation**: Same mitigation as v2/v3 (accepted risk). After 500ms, NakedPositionDetector
queues a fresh callback which runs without debounce suppression. HasArmingAtmBrackets is also
checked -- if brackets are closed, neither guard fires. Risk level: LOW.

### SECONDARY RISK: Debounce window too short for slow broker environments

**Scenario**: Entry fills on Sim102. ATM arm takes > 500ms (slow broker, high latency).
v4 debounce expires before ATM brackets appear in acc.Orders. Stale callback fires.
HasArmingAtmBrackets check: brackets still in Initialized state -> may suppress (v1).

**Mitigation**: HasArmingAtmBrackets includes Initialized state (from v2 hardening). Even in
slow-broker environments, CreateOrder creates an order in Initialized state immediately.
If the v4 debounce expires before brackets arm, v1 HasArmingAtmBrackets catches it.
Belt-and-suspenders: both guards must fail for the flatten to fire. Risk level: LOW.

### TERTIARY RISK: _nakedDetectLastQueuedTicks never cleared on account reconnect

**Scenario**: Same stale-timestamp risk identified in v3's risk assessment. Not worsened by v4.
Risk level: NEGLIGIBLE (same as v3). Deferred to DW-LB-FL-01-RECONNECT-CLEANUP.

### QUATERNARY RISK: v4 debounce suppresses a genuine flush during manual close sequence

**Scenario**: User manually closes brackets (not through PTT panel) then manually waits
< 500ms then NakedPositionDetector fires. v4 suppresses the naked detect.

**Mitigation**: FlattenIfNotArming is ONLY called from NakedPositionDetector's Dispatcher
callback. Manual flatten (Flatten button) calls FlattenOneAccount directly -- it does NOT
go through FlattenIfNotArming. Risk level: NONE for manual flatten.

---

## 9. Test Design

All tests are xUnit [Fact]. File: `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`.

### New Tests for v4

| # | Test Name | What It Asserts |
|---|-----------|-----------------|
| T24 | `FlattenIfNotArming_ReturnsWithoutFlatten_WhenDebounceActive` | When _nakedDetectLastQueuedTicks["Sim102"] = TickCount, FlattenIfNotArming suppresses flatten (no FlattenOneAccount call) |
| T25 | `FlattenIfNotArming_ProceedsToBracketCheck_WhenDebounceExpired` | When _nakedDetectLastQueuedTicks["Sim102"] = TickCount - 600, FlattenIfNotArming proceeds to HasArmingAtmBrackets check |
| T26 | `FlattenIfNotArming_ProceedsToFlatten_WhenNoDebounceStamp` | When _nakedDetectLastQueuedTicks has no entry for "Sim102", FlattenIfNotArming does not debounce-skip |
| T27 | `IsInEntryFillDebounceWindow_ReturnsTrue_WhenWithin500ms` | IsInEntryFillDebounceWindow returns true when last = TickCount (within 500ms) |
| T28 | `IsInEntryFillDebounceWindow_ReturnsFalse_WhenOver500ms` | IsInEntryFillDebounceWindow returns false when last = TickCount - 600 |
| T29 | `IsInEntryFillDebounceWindow_ReturnsFalse_WhenNoEntry` | IsInEntryFillDebounceWindow returns false when account not in dict |

### Regression Tests (T1-T23 remain valid, all required)

T1-T10 from v1 architecture plan remain required.
T11-T19 from v2 architecture plan remain required.
T20-T23 from v3 architecture plan remain required.
T24-T29 are the new v4 additions.

---

## 10. SIM Verification Steps for v4

### Step 1: Baseline -- single-trade entry (regression, no cancel)

1. Start fresh SIM session, Clone mode, 3 follower accounts (Sim102, Sim103, Sim104)
2. Leader enters SHORT (first trade, no prior BE-ALL cycle)
3. Verify all followers copy SHORT -- no PTT-Flatten during bracket arm
4. EXPECTED: "flat-guard: debounce-skip" does NOT appear (no prior debounce stamp exists)
5. EXPECTED: "flat-guard: bracket-arm skip" may appear if stale callback fires (v1 guard active)
6. EXPECTED: No PTT-Flatten in Output tab

### Step 2: BE-ALL cycle + second entry (v3 regression)

1. While followers SHORT, trigger BE-ALL
2. Verify all brackets cancel, positions close
3. Leader re-enters SHORT
4. PRIMARY CHECK: No PTT-Flatten during bracket arm window (same as v3 test)

### Step 3: TWO-TRADE sequence (KEY v4 TEST)

1. Leader enters Trade #1 (limit order on MES SEP26)
2. Wait for DispatchCopy to fire -- confirm [PTT-COPY] dispatch logged for Sim102/Sim103/Sim104
3. BEFORE Trade #1 fills: leader CANCELS Trade #1
4. Verify TryCancelFollowerEntries fires -- "Entry:Cancelled" visible on all followers
5. IMMEDIATELY after cancel: leader enters Trade #2
6. Verify DispatchCopy fires for Trade #2 -- new "Entry" dispatched to all followers
7. PRIMARY CHECK: Entry:Filled on all followers, brackets arm cleanly
   - EXPECTED: "flat-guard: debounce-skip" logged for any stale callbacks
   - EXPECTED: NO "PTT-Flatten:Submitted" on any follower
   - EXPECTED: Stop1:Working (not Stop1:CancelSubmitted) on all followers
   - FORBIDDEN: PTT-Flatten:Submitted alongside bracket arming
8. Verify Account Data panel: Sim102/Sim103/Sim104 all show Qty=7 (not Qty=0)

### Step 4: Repeat Step 3 x3 (stress test)

Repeat the Trade #1 cancel + Trade #2 sequence 3 times in the same SIM session.
All 3 repetitions must pass Step 3 criteria. Confirm "flat-guard: debounce-skip" appears
in Output tab when stale callbacks are correctly suppressed.

### Step 5: Legitimate naked position (safety net preserved)

1. While followers SHORT with Working brackets
2. Wait > 600ms after entry fill (debounce window expired)
3. Manually cancel ALL bracket orders from NT8 order window
4. KEY CHECK: PTT-Flatten fires on all followers correctly
   - EXPECTED: Log shows "PTT-Flatten" submitted and filled
   - EXPECTED: No "flat-guard: debounce-skip" (debounce expired)

### Step 6: Regression -- manual Flatten button

1. While followers have open positions + brackets
2. Press Flatten button in PTT panel
3. Verify immediate flatten -- NOT affected by debounce guards
   (Manual flatten calls FlattenOneAccount directly, not through FlattenIfNotArming)

---

## 11. Spec Requirement Traceability

| Symptom | v4 Fix Element | Covers |
|---------|----------------|--------|
| PTT-Flatten:Submitted during Trade #2 bracket arm (Sim102 only) | FlattenIfNotArming: IsInEntryFillDebounceWindow as first guard | Race 4: stale Dispatcher callback from prior trade cycle runs after Trade #2 entry fills but before ATM brackets visible in acc.Orders |
| Sim103/Sim104 survived with clean bracket arming | Race 4 is timing-dependent: their stale callbacks ran before Trade #2 entry filled (position=0 gate blocks flatten) | v4 prevents the timing-dependent failure for Sim102 |
| v1+v2+v3 guards preserved | IsInEntryFillDebounceWindow is NEW first guard; HasArmingAtmBrackets retained as second guard; v3 debounce stamp symbiotic with v4 read | All invariants preserved |
| Legitimate naked position must still flatten | Debounce=500ms; after 500ms, stale callbacks not suppressed; position=0 gate and HasArmingAtmBrackets still active | Scenario 5 (legitimate naked) verified |
| CYC <= 8 | FlattenIfNotArming CYC=3; IsInEntryFillDebounceWindow CYC=2 | PASS |
| No lock() | TryGetValue on ConcurrentDictionary is lock-free | PASS |
| No DateTime.Now | Environment.TickCount (same as NakedPositionDetector L7263) | PASS |
| ASCII-only | "flat-guard: debounce-skip" is ASCII | PASS |

---

## 12. Deferred Items (unchanged from v3)

| ID | Issue | Deferred To |
|----|-------|-------------|
| DW-LB-FL-01-ATM-ARM-FAILURE | Sim103 fo=NULL for all 6 brackets in v3 SIM: possible AtmObject cache miss causing Named ATM arm failure after BE-ALL cycle | Future ticket; separate from flatten fix |
| DW-LB-FL-01-RECONNECT-CLEANUP | Clear _nakedDetectLastQueuedTicks on LoadRules() to prevent stale timestamp after reconnect | Low risk; cleanup pass |

---

## Summary

**Root cause (Race 4)**: A `FlattenIfNotArming` callback queued by `NakedPositionDetector`
during a PRIOR TRADE's cancel storm survived in the WPF Dispatcher queue across multiple
trade transitions (Trade #1 dispatch/cancel) and executed on the UI thread AFTER Trade #2's
entry filled but BEFORE the new ATM brackets appeared in `acc.Orders`. `HasArmingAtmBrackets`
returned false (brackets not yet visible) -> `FlattenOneAccount` fired -> PTT-Flatten:Submitted.

**The v3 debounce prevented NEW callbacks from being queued** (after entry fill, NakedPositionDetector
is debounced for 500ms). But v3 could not suppress a callback that was ALREADY in the Dispatcher
queue from a prior cycle. The already-queued callback ran unconditionally.

**Fix**: Add `IsInEntryFillDebounceWindow(acct)` as the first guard in `FlattenIfNotArming`.
This reads `_nakedDetectLastQueuedTicks[acc.Name]` (the same dict v3 stamps on entry fill)
and returns true if within 500ms. A stale callback running within 500ms of the last entry
fill is suppressed before reaching `HasArmingAtmBrackets` or `FlattenOneAccount`.

**Why Sim103/Sim104 survived**: Their stale callbacks executed before their Trade #2 entries
filled (position=0 -> `IsAccountFlattenable` blocked `FlattenOneAccount`). Sim102's stale
callback executed in the narrow window after its Trade #2 entry filled but before its ATM
brackets were registered in `acc.Orders` -- a timing-dependent race that v4 eliminates
by making the debounce check position- and bracket-state-independent.

**Files modified**: `src/PropTraderTools/CopyEngine.cs` (1 file only).
**Methods changed**: `FlattenIfNotArming` (2 guards now: debounce + HasArmingAtmBrackets).
**Methods added**: `IsInEntryFillDebounceWindow` (CYC=2, 7 lines).
**CYC impact**: FlattenIfNotArming 2->3. IsInEntryFillDebounceWindow CYC=2. Both PASS <= 8.
**Tests added**: T24-T29 in `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`.
**P0 violations introduced**: None.
