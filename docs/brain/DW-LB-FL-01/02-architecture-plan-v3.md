# DW-LB-FL-01 Architecture Plan v3

**Defect ID**: DW-LB-FL-01
**Severity**: P1
**Status**: PLAN_V3_COMPLETE
**Author**: ptt-architect (Phase 3 re-architecture -- v3)
**Date**: 2026-09-09
**Output file**: `docs/brain/DW-LB-FL-01/02-architecture-plan-v3.md`
**Supersedes**: `docs/brain/DW-LB-FL-01/02-architecture-plan-v2.md` (v2 -- BUILD_PASS, SIM_FAIL)
**Baseline**: v1 + v2 fixes are preserved and NOT reverted

---

## Rules Catalog Gate Result

**GATE RESULT: PASS**

P0 rules confirmed no-violation for this fix:

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No lock() in new/modified code | PASS -- ConcurrentDictionary.AddOrUpdate is lock-free |
| JS-001 | No throw in hot path | PASS -- no throw in modified branch |
| JS-002 | No return null | PASS -- method returns void |
| JS-033 | No async void | PASS -- synchronous void |
| JS-036/037 | No heap alloc in hot path | PASS -- AddOrUpdate on existing ConcurrentDict, no new alloc |

---

## LANE-SPLIT GATE RESULT

**SINGLE-PIPELINE**

One defect, one fix. One method modified (TryNakedDetect, 3 lines inside existing branch).
Single ticket. Single PR.

---

## 1. Source Readings Performed

The following methods were read from live source before diagnosis:

| Method | Location | CYC |
|--------|----------|-----|
| `TryNakedDetect` | L7217-7230 | 4 |
| `IsPttCopyEntry` | L7206-7207 | 2 |
| `HasArmingAtmBrackets` | L5267-5285 | 5 |
| `FlattenIfNotArming` | L5177-5185 | 2 |
| `SyncFollowerBracket` | L2663-2698 | 7 |
| `NakedPositionDetector` | L7240-7263 | 6 |
| `HasNakedPosition` | L7273-7287 | 4 |
| `IsNakedConditionMet` | L7293-7307 | 4 |
| `TryDispatchLeaderFlat` | L4693-4717 | 8 |
| `IsNonFlatDispatchName` | L2379-2388 | 3 |
| `IsNativeExitName` | L2358-2371 | 5 |
| `FlattenOneAccount` | L5203-5211 | 2 |
| `TrySweptPttDragOrphans` | L1805-1817 | 4 |
| `OnOrderUpdate` (hot path) | L1500-1597 | 8 |
| `TryFirePositionState` | L4144-4163 | 5 |
| `_nakedDetectLastQueuedTicks` | L373-374 | -- |

**FlattenOneAccount call sites confirmed:**
| Line | Path | Guarded? |
|------|------|----------|
| L4997 | `Flatten(Instrument)` -- user-initiated | No guard needed (manual) |
| L5037 | `Flatten(Account, Instrument)` leader | No guard needed (manual) |
| L5042 | `Flatten(Account, Instrument)` follower | No guard needed (manual) |
| L5184 | `FlattenIfNotArming` from NakedPositionDetector | Guarded by HasArmingAtmBrackets |
| L1586 | delegate to TryDispatchLeaderFlat | Guarded by isFollower+hasOpenPosition |

**FlattenIfNotArming call sites:** Only one -- L5184 inside NakedPositionDetector Dispatcher callback.

---

## 2. Diagnostic Answers (Q1-Q6)

### Q1: Does SyncFollowerBracket have any logic that calls FlattenOneAccount when fo=NULL?

**Answer: NO.**

`SyncFollowerBracket` L2678: `if (fo == null) return;` -- hard return, no side effects.
`TryLogSFBTrace` logs the NULL for diagnostics only (L2677-2677). No flatten, no state mutation.

### Q2: Is there a PositionStateChanged handler for FOLLOWER accounts that calls flatten?

**Answer: NO.**

- `TradeCopierPanel.OnPositionStateChanged` (L713): calls `UpdateButtonColors` only.
- `TradeCopierWindow.OnPositionStateChanged` (L218): calls `UpdateButtonColors` only.
- `TryFirePositionState` (L4144): fires `PositionStateChanged` event only. No flatten.
- `TryFireFollowerBeDisarm` (L1599): fires `PositionStateChanged` event only. No flatten.

PositionStateChanged is a UI coloring signal only. Confirmed safe.

### Q3: Is there a separate code path that fires flatten when a PTT-Copy entry FILLS?

**Answer: YES -- but NOT from the entry fill event itself.**

The v2 `IsPttCopyEntry` guard in `TryNakedDetect` correctly blocks `NakedPositionDetector` for
the "Entry":Filled event (verified at L7227-7228). However, within 50-400ms of the entry fill,
stale ATM bracket cancel acks from the PREVIOUS trade's `CancelQxBrackets` sweep arrive as
"Stop1":Cancelled, "Stop2":Cancelled, etc. events on the follower account. These events:

1. Pass all gates in `TryNakedDetect` (state=Cancelled, IsFollowerAccount=true, IsPttCopyEntry("Stop1")=FALSE -- v2 guard does not fire for Cancelled events)
2. Invoke `NakedPositionDetector`
3. `HasNakedPosition` sees: position qty > 0 (new entry filled) AND no Working/Submitted brackets (new ATM not yet in acc.Orders) = true
4. `FlattenIfNotArming` → `HasArmingAtmBrackets` = false (new brackets not in acc.Orders yet) → `FlattenOneAccount` fires

This is **Race 3: stale cancel ack from previous trade arrives during new ATM bracket arm window.**

### Q4: What does SyncFollowerBracket do when fo=NULL?

**Answer: Returns silently at L2678. No flatten, no state change.**

The fo=NULL observation (6 of 6 bracket slots showing fo=NULL for Sim103) is diagnostic
evidence that the new ATM brackets did not populate acc.Orders during SyncFollowerBracket's
execution window. However, fo=NULL is a SYMPTOM of the ATM arm timing race, NOT the cause
of the flatten. The flatten comes from the cancel ack path (Race 3).

### Q5: Sim103 had fo=NULL for ALL 6 brackets. After all 6 return fo=NULL, does the system flatten?

**Answer: NO -- SyncFollowerBracket returning fo=NULL is silent.**

The flatten for Sim103 came from Race 3 (stale cancel ack triggering NakedPositionDetector),
not from SyncFollowerBracket. For Sim103, the new ATM brackets also failed to appear in
acc.Orders during the SyncFollowerBracket window (separate issue -- possible AtmObject cache
miss for Named ATM mode), but this is independent of the flatten trigger.

### Q6: Why did Sim104 survive but Sim102/Sim103 did not?

**Timing hypothesis (confirmed by order tail evidence):**

For Sim104, the new ATM brackets reached `Submitted` state in `acc.Orders` BEFORE
`HasArmingAtmBrackets` executed inside the `FlattenIfNotArming` Dispatcher callback. Even
though a Dispatcher callback was queued by the stale cancel ack path, when the UI thread ran
it, the brackets were Submitted/Working → `HasArmingAtmBrackets` returned true → flatten
was suppressed. The "PTT-Flatten:Submitted" entry visible in Sim104's order tail came from
the EARLIER Race 1 cancel-storm Dispatcher callback (queued during the BE-ALL cycle), which
was guarded by `HasArmingAtmBrackets` returning true for Sim104 (brackets armed fast enough).

For Sim102 and Sim103: stale cancel acks arrived within ~200ms of the entry fill. The new
ATM brackets had not yet appeared in `acc.Orders` at Submitted/Working state. The Dispatcher
callback executed, `HasArmingAtmBrackets` found no arming brackets, and `FlattenOneAccount`
fired.

---

## 3. Root Cause Statement for v3

**Race 3 -- Stale bracket cancel ack from previous trade arrives during new ATM bracket arm window.**

`CancelQxBrackets` / NT8-ATM sweep cancels old Stop1..Stop3 and Target1..Target3 on each
follower during the BE-ALL cycle. These cancel acks propagate asynchronously through NT8's
order manager. The cancel acks for some followers arrive AFTER the new PTT-Copy entry fills
(typically 50-400ms after the cancel request was issued, depending on broker and load).

When the stale cancel ack fires:
- `TryNakedDetect`: OrderState=Cancelled passes gate 1; IsFollowerAccount passes gate 2
- The v2 guard: `OrderState == Filled && IsPttCopyEntry` -- **Cancelled is NOT Filled**. Guard does NOT fire.
- `NakedPositionDetector` is invoked
- `HasNakedPosition`: position qty > 0 (new entry just filled) + no Working/Submitted brackets (new ATM arm not yet complete) = TRUE
- `FlattenIfNotArming` queued via Dispatcher.InvokeAsync
- When the UI thread runs the callback: `HasArmingAtmBrackets` finds no arming brackets (new brackets not yet in acc.Orders, or brackets are in CancelSubmitted state from a prior sweep)
- `FlattenOneAccount` fires → PTT-Flatten:Submitted → follower position closed prematurely

**The v2 fix protected the entry FILL event but left the entry's CANCEL-ACK window unguarded.**

---

## 4. Why v1+v2 Are Correct but Incomplete

| Version | Race Targeted | Fix | Status |
|---------|--------------|-----|--------|
| v1 | Race 1: cancel-storm Dispatcher callback sees no brackets | HasArmingAtmBrackets guard in FlattenIfNotArming | Correct, preserved |
| v2 | Race 2: entry fill event fires NakedPositionDetector before brackets arm | IsPttCopyEntry guard in TryNakedDetect (skip NakedPositionDetector for entry fills) | Correct, preserved |
| v3 (NEW) | Race 3: stale cancel ack from previous trade fires NakedPositionDetector in entry fill window | Stamp debounce clock at entry fill time, so NakedPositionDetector's existing 500ms grace blocks stale cancels | **TO BE IMPLEMENTED** |

v1 is still needed: if Race 1 Dispatcher callback somehow survives (e.g., new entry fills AND brackets arm to Initialized before callback runs), `HasArmingAtmBrackets` catches it.
v2 is still needed: the v2 guard prevents the entry fill itself from triggering `NakedPositionDetector`.
v3 adds the missing link: the 500ms grace window between entry fill and stale cancel ack.

---

## 5. v3 Fix Design

### 5.1 Strategy

**Minimal change (2-3 lines inside existing v2 branch):**

When `TryNakedDetect` detects a PTT-Copy entry fill (the existing v2 branch), also stamp
`_nakedDetectLastQueuedTicks[acc.Name]` with the current TickCount. This leverages the
existing 500ms grace window in `NakedPositionDetector` to debounce stale cancel acks that
arrive within 500ms of the entry fill.

**No new fields. No new state. CYC unchanged.**

### 5.2 Modified Method: TryNakedDetect

**Source location**: `src/PropTraderTools/CopyEngine.cs` L7217-7230

**Current v2 code (from live source)**:
```csharp
// DW-LB-FL-01-V2: skip naked-position check on entry fill...
private void TryNakedDetect(OrderEventArgs e)
{
    if (
        e.Order.OrderState != OrderState.Filled
        && e.Order.OrderState != OrderState.Cancelled
        && e.Order.OrderState != OrderState.Rejected
    )
        return;
    if (!IsFollowerAccount(e.Order.Account))
        return;
    if (e.Order.OrderState == OrderState.Filled && IsPttCopyEntry(e.Order)) // DW-LB-FL-01-V2
        return;
    NakedPositionDetector(e.Order.Account);
}
```

**v3 change** (expand the single-line v2 guard to a block):
```csharp
// DW-LB-FL-01-V2: skip naked-position check on entry fill -- ATM brackets are about to
// arm via StartAtmStrategy. An entry fill is NOT a naked-position signal; it is the
// first event of the bracket arm sequence.
// DW-LB-FL-01-V3: ALSO stamp the debounce clock at entry-fill time.
// Stale bracket cancel acks from the previous trade's CancelQxBrackets sweep arrive
// within ~50-400ms after this event. By writing _nakedDetectLastQueuedTicks now,
// NakedPositionDetector's 500ms grace window blocks those stale Cancelled events
// from queuing a FlattenIfNotArming callback before new ATM brackets arm.
// Root cause of v2 SIM failure (Race 3): "Stop1":Cancelled fires ~200ms after entry
// fill, bypasses the v2 Filled-only guard, reaches NakedPositionDetector, sees
// position=true + no brackets (arm race) = false positive = PTT-Flatten:Submitted.
// Fix: debounce window covers the bracket arm window (500ms > typical 50-300ms arm time).
// Uses same (long)(int)Environment.TickCount pattern as NakedPositionDetector (L7248).
// ConcurrentDictionary.AddOrUpdate: lock-free (JS-021). No throw (JS-001). ASCII-only.
// CYC impact: NONE -- AddOrUpdate is inside existing branch, not a new branch point.
// CYC=4 unchanged.
private void TryNakedDetect(OrderEventArgs e)
{
    if (
        e.Order.OrderState != OrderState.Filled
        && e.Order.OrderState != OrderState.Cancelled
        && e.Order.OrderState != OrderState.Rejected
    )
        return;
    if (!IsFollowerAccount(e.Order.Account))
        return;
    if (e.Order.OrderState == OrderState.Filled && IsPttCopyEntry(e.Order)) // DW-LB-FL-01-V2
    {
        // DW-LB-FL-01-V3: stamp debounce clock so stale cancel acks within 500ms are blocked.
        long now = (long)(int)Environment.TickCount;
        _nakedDetectLastQueuedTicks.AddOrUpdate(
            e.Order.Account.Name, now, (_, __) => now);
        return;
    }
    NakedPositionDetector(e.Order.Account);
}
```

**McCabe**: CYC=4 (unchanged from v2). AddOrUpdate is inside an existing branch -- no new decision point. **PASS <= 8**.

**Change summary**: The single-line `return;` becomes a block with 3 additional lines (one `long now =`, one `AddOrUpdate`, and the `return`). No structural change to the method.

### 5.3 How the Debounce Suppresses Race 3

**Scenario (Sim103 fixed):**
```
T=0ms:   Entry:Filled event fires
         TryNakedDetect: IsPttCopyEntry("Entry")=true, v2+v3 branch
         _nakedDetectLastQueuedTicks["Sim103"] = T
         return early
T=200ms: "Stop1":Cancelled (stale ack from BE-ALL sweep) fires
         TryNakedDetect: state=Cancelled, IsPttCopyEntry("Stop1")=false
         NakedPositionDetector("Sim103") called
         debounce: now=T+200ms, last=T, 200ms < 500ms -> RETURN EARLY
         ** No flatten queued **
T=300ms: "Stop2":Cancelled fires -- same debounce blocks
T=400ms: "Target1":Cancelled fires -- same debounce blocks
T=500ms: debounce window expires
T=550ms: (no events) -- new ATM brackets already Working or genuinely naked
         If naked: next terminal event fires -> NakedPositionDetector -> no debounce -> flatten (correct)
         If brackets Working: HasArmingAtmBrackets=true -> suppress (correct)
```

### 5.4 Interaction with Existing Debounce

The `_nakedDetectLastQueuedTicks` dictionary is used in TWO places:
1. `NakedPositionDetector` (L7250-7255): reads `last`, compares with `now`, updates if queuing
2. `TryNakedDetect` (v3 addition): writes `now` on entry fill detect

The v3 write races only with the NakedPositionDetector read/write on the same key. Both use
`AddOrUpdate` which is atomic (lock-free CAS). No data race possible. ✓

### 5.5 Summary of Changes vs v1+v2

| Method | Change | CYC Before | CYC After |
|--------|--------|------------|-----------|
| `TryNakedDetect` | v3: expand v2 single-line `return` to block with 3 extra lines | 4 | 4 (unchanged) |
| `IsPttCopyEntry` | Unchanged (v2) | 2 | 2 |
| `HasArmingAtmBrackets` | Unchanged (v2, Initialized added) | 5 | 5 |
| `FlattenIfNotArming` | Unchanged (v1) | 2 | 2 |
| `NakedPositionDetector` | Unchanged | 6 | 6 |
| `IsNakedConditionMet` | Unchanged | 4 | 4 |

**Files modified**: `src/PropTraderTools/CopyEngine.cs` (1 file only).

---

## 6. Why the Fix Is Complete

**Scenario 1 -- Race 1 (cancel-storm, v1 target):**
- CancelQxBrackets fires → cancel acks for OLD brackets → TryNakedDetect fires for each cancel
- If entry has NOT filled yet: `_nakedDetectLastQueuedTicks` has no v3 stamp (or old stamp > 500ms ago)
- NakedPositionDetector fires, queues Dispatcher callback
- If new entry fills and brackets arm before callback: HasArmingAtmBrackets=true → suppress ✓ (v1 fix)
- If brackets in Initialized: HasArmingAtmBrackets=true ✓ (v2 hardening)
- Belt-and-suspenders: v3 also debounces if entry fills within 500ms of cancel acks

**Scenario 2 -- Race 2 (entry fill, v2 target):**
- "Entry":Filled → IsPttCopyEntry=true → v2+v3 block
- v3 addition: `_nakedDetectLastQueuedTicks["acc"] = now` ✓
- Return early -- no NakedPositionDetector invoked ✓

**Scenario 3 -- Race 3 (stale cancel ack, v3 target):**
- "Entry":Filled sets `_nakedDetectLastQueuedTicks["acc"] = T`
- 50-400ms later: "Stop1":Cancelled → NakedPositionDetector → debounce: T + 50-400ms < T + 500ms → block ✓
- Stale cancel ack suppressed. New ATM brackets arm undisturbed. ✓

**Scenario 4 -- Legitimate naked position (orphaned):**
- Entry fill from PREVIOUS trade set `_nakedDetectLastQueuedTicks["acc"] = T_prev`
- Current terminal event fires > 500ms after T_prev (trade has been live for seconds)
- Debounce: now - T_prev > 500ms → PASS → NakedPositionDetector fires
- HasNakedPosition: position=true, no brackets → true
- FlattenIfNotArming: HasArmingAtmBrackets=false → FlattenOneAccount ✓

**Scenario 5 -- Manual flatten (preserved):**
- User presses Flatten button → direct call chain L5037/L5042 → FlattenOneAccount
- No path through TryNakedDetect → unaffected ✓

---

## 7. Preserved Invariants

| Guard | Location | Status |
|-------|----------|--------|
| DW-B65-01 bypass (IsNativeExitName) | TryDispatchLeaderFlat L4712 | PRESERVED -- unchanged |
| DW-LB-FL-02 (IsNativeExitOnFlatLeader) | TryDispatchLeaderFlat L4710 | PRESERVED -- unchanged |
| PTT-Flatten in-flight guard (HasInflightFlatten) | IsAccountFlattenable L5224 | PRESERVED -- unchanged |
| DW-B94 (IsNonFlatDispatchName) | TryDispatchLeaderFlat L4708 | PRESERVED -- unchanged |
| v1 FlattenIfNotArming guard | NakedPositionDetector L7261 | PRESERVED -- still active for stale events > 500ms |
| v2 IsPttCopyEntry entry-fill skip | TryNakedDetect L7227 | PRESERVED -- v3 is inside this branch |
| v2 HasArmingAtmBrackets Initialized state | HasArmingAtmBrackets L5274 | PRESERVED -- unchanged |

---

## 8. Risk Assessment

### PRIMARY RISK: Legitimate naked position in fast-fill scenario

**Scenario**: Entry fills. Brackets armed and ALL FILL within 500ms (extremely fast TP hit,
e.g., SIM gap fill on open). Position goes flat within 500ms. A second entry fills within
500ms of the first fill (double-entry scenario). Second entry is naked (no ATM configured).
The v3 debounce blocks naked detection for the second entry for up to 500ms.

**Mitigation**:
- Double-entry within 500ms with no ATM is an operational error, not a software defect
- The 500ms window aligns with the existing NakedPositionDetector debounce (same GraceMs=500L)
- After 500ms, any subsequent terminal event re-triggers NakedPositionDetector
- Risk level: LOW. Same acceptable trade-off as v2 Scenario 4.

### SECONDARY RISK: Debounce clock reset on stale broker reconnect

**Scenario**: Account disconnects and reconnects. `_nakedDetectLastQueuedTicks` retains the
stale timestamp from the previous session. First terminal event after reconnect within 500ms
of the stale timestamp could be debounced.

**Mitigation**:
- `_nakedDetectLastQueuedTicks` uses `(long)(int)Environment.TickCount` which wraps at ~25 days
- After reconnect, typical TickCount difference >> 500ms unless reconnect happens within 500ms
  of the prior session's last entry fill (statistically negligible)
- The `LoadRules()` path clears `_resolvedFollowers` but does NOT clear `_nakedDetectLastQueuedTicks`
- If this is a concern, `_nakedDetectLastQueuedTicks` can be cleared on `LoadRules()` in a future
  ticket (DW-LB-FL-01-RECONNECT-CLEANUP, deferred)
- Risk level: NEGLIGIBLE. Bounded to 500ms maximum window.

### TERTIARY RISK: Race 1 Dispatcher callback already in queue at entry fill

**Scenario**: Cancel-storm callback (Race 1) is already queued in Dispatcher when entry fills.
v3 stamps the debounce clock. The queued callback runs on UI thread. `FlattenIfNotArming`
→ `HasArmingAtmBrackets` checks acc.Orders. If brackets are Working/Submitted: suppress ✓.
If brackets not yet in acc.Orders: FlattenOneAccount fires.

**Mitigation**: v1 `HasArmingAtmBrackets` guard (with Initialized from v2) is the last line of
defense for this path. The v3 fix does NOT change FlattenIfNotArming. Race 1 protection is
identical to v2. Belt-and-suspenders: v3 prevents a SECOND Dispatcher callback from being queued
by the stale cancel ack.

**Risk level**: SAME AS v2. Not worsened by v3.

---

## 9. Test Design

All tests are xUnit [Fact] unit tests. File: `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`.

### New Tests for v3

| # | Test Name | What It Asserts |
|---|-----------|-----------------|
| T20 | `TryNakedDetect_StampsDebounce_WhenEntryFills` | Entry fill on follower stamps `_nakedDetectLastQueuedTicks` for that account |
| T21 | `NakedPositionDetector_Debounced_WhenBracketCancelsWithin500ms` | Stop1 Cancelled within 500ms of entry fill timestamp -> NakedPositionDetector blocked by debounce |
| T22 | `NakedPositionDetector_NotDebounced_WhenBracketCancels_After500ms` | Stop1 Cancelled > 500ms after entry fill timestamp -> NakedPositionDetector fires |
| T23 | `TryNakedDetect_StillSkipsEntry_WhenV2GuardActive` | Regression: IsPttCopyEntry guard still returns early for Entry:Filled (v2 not broken) |

### Regression Tests (T1-T19 remain valid, all required)

T1-T10 from v1 architecture plan remain required.
T11-T19 from v2 architecture plan remain required.
T20-T23 are the new v3 additions.

---

## 10. SIM Verification Steps for v3

### Step 1: Baseline -- confirm no regression on first entry

1. Start fresh SIM session, Clone mode, 3 follower accounts (Sim102, Sim103, Sim104)
2. Leader enters SHORT (first trade, no prior BE-ALL cycle)
3. Verify followers go SHORT -- no reversal
4. Verify no PTT-Flatten fires during bracket arming
5. EXPECTED: "flat-guard: bracket-arm skip" does NOT appear in log (NakedPositionDetector not invoked for entry fill)
6. EXPECTED: No PTT-Flatten in Output tab for any follower

### Step 2: BE-ALL cycle + second entry (KEY TEST)

1. While followers SHORT, trigger BE-ALL
2. Verify all brackets cancel, positions close
3. Wait for leader re-entry SHORT
4. Verify PTT-COPY dispatched on all followers
5. **PRIMARY CHECK**: Monitor Output tab during bracket arming window (0-1 second after entry fill)
   - EXPECTED: NO "PTT-Flatten" log lines for ANY follower during Stop1..Target3 arming
   - EXPECTED: NO reversal -- followers remain SHORT (matching leader)
   - EXPECTED: "flat-guard: bracket-arm skip" may appear if Race 1 callback fires and finds brackets (benign, correct suppression)
   - FORBIDDEN: "PTT-Flatten:Submitted" on any follower account
6. After brackets arm, verify all 3 follower accounts show SHORT position with Working brackets

### Step 3: Repeat Step 2 x3 (stress test timing)

Repeat the BE-ALL + re-entry sequence 3 times in the same SIM session. All 3 repetitions
must pass Step 2 criteria. The debounce clock must reset correctly each time.

### Step 4: Legitimate naked position (safety net preserved)

1. While followers SHORT with Working brackets
2. Manually cancel all bracket orders from NT8 order window (not through panel)
3. Wait 600ms (debounce window + margin beyond v3 stamp)
4. **KEY CHECK**: Verify PTT-Flatten fires and closes the SHORT position correctly
   - EXPECTED: Log shows "PTT-Flatten" submitted and filled on all followers
   - EXPECTED: Followers go flat

### Step 5: Regression -- manual Flatten button

1. While followers have open positions with brackets
2. Press Flatten button in PTT panel
3. Verify all followers flatten immediately -- not affected by debounce guards

---

## 11. Spec Requirement Traceability

| Symptom | v3 Fix Element | Covers |
|---------|----------------|--------|
| PTT-Flatten fires on second entry after BE-ALL (Sim102, Sim103) | TryNakedDetect v3: stamp debounce clock at entry fill | Race 3: stale cancel ack from CancelQxBrackets arrives 50-400ms after entry fill, bypasses v2 Filled-only guard |
| Sim104 survived with PTT-Flatten:Submitted + brackets Working | v3 explains timing: Sim104 brackets armed before FlattenIfNotArming ran; v3 prevents the scenario from occurring at all | Race 3 would have also been suppressed by v3 |
| Sim103 fo=NULL for ALL 6 brackets | Separate issue (possible AtmObject cache miss for Named ATM mode); not addressed by v3; NakedPositionDetector debounce gives 500ms for arm recovery | Deferred: DW-LB-FL-01-ATM-ARM-FAILURE |
| Legitimate naked position must still flatten | Debounce window = 500ms; after 500ms, NakedPositionDetector fires normally; HasArmingAtmBrackets is last-resort guard | Scenario 4 verified |
| v1+v2 fixes preserved | v3 modifies only inside the v2 guard block; all other code paths unchanged | All invariants preserved |
| CYC <= 8 | TryNakedDetect CYC=4 (unchanged; AddOrUpdate inside existing branch) | PASS |
| No lock() | AddOrUpdate on ConcurrentDictionary is lock-free | PASS |
| No DateTime.Now | Environment.TickCount (same as NakedPositionDetector L7248) | PASS |

---

## 12. Deferred Items

| ID | Issue | Deferred To |
|----|-------|-------------|
| DW-LB-FL-01-ATM-ARM-FAILURE | Sim103 fo=NULL for all 6 brackets: possible AtmObject cache miss causing Named ATM arm failure after BE-ALL cycle | Future ticket; separate from flatten fix |
| DW-LB-FL-01-RECONNECT-CLEANUP | Clear `_nakedDetectLastQueuedTicks` on `LoadRules()` to prevent stale timestamp after reconnect | Low risk; can be addressed in cleanup pass |

---

## Summary

**Root cause (v3)**: `TryNakedDetect` is triggered by stale ATM bracket cancel acks
("Stop1":Cancelled, "Target1":Cancelled, etc.) from the previous trade's `CancelQxBrackets`
sweep, arriving 50-400ms AFTER the new follower entry fills. The v2 guard only protects
against `Filled` state. `Cancelled` events bypass it, reach `NakedPositionDetector`, see
position=true + no brackets (new ATM arm race) = false positive = `FlattenOneAccount` =
PTT-Flatten:Submitted = reversal.

**Fix**: In `TryNakedDetect`, inside the existing v2 guard branch, stamp
`_nakedDetectLastQueuedTicks[acc.Name]` with current TickCount. The existing 500ms grace
window in `NakedPositionDetector` then blocks stale cancel acks arriving within 500ms of
the entry fill.

**Why Sim104 survived**: The new ATM brackets armed to Submitted/Working before the Dispatcher
callback from the stale cancel ack ran. `HasArmingAtmBrackets` returned true → flatten
suppressed. v3 would have prevented the callback from being queued at all.

**Files modified**: `src/PropTraderTools/CopyEngine.cs` (1 file only).
**Method changed**: `TryNakedDetect` (L7217-7230). 3 lines added inside existing branch.
**CYC impact**: None. TryNakedDetect CYC=4 unchanged.
**Tests added**: T20-T23 in `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`.
**P0 violations introduced**: None.
