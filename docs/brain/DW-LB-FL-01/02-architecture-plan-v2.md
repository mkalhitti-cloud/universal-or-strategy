# DW-LB-FL-01 Architecture Plan v2

**Defect ID**: DW-LB-FL-01
**Severity**: P1
**Status**: PLAN_V2_COMPLETE
**Author**: ptt-architect (Phase 3 re-architecture)
**Date**: 2026-09-08
**Output file**: `docs/brain/DW-LB-FL-01/02-architecture-plan-v2.md`
**Supersedes**: `docs/brain/DW-LB-FL-01/02-architecture-plan.md` (v1 -- BUILD_PASS, SIM_FAIL)

---

## Rules Catalog Gate Result

**GATE RESULT: PASS**

P0 rules confirmed no-violation for this fix:

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No lock() in new/modified code | PASS -- static helpers, .ToList() snapshot, no shared state |
| JS-001 | No throw in hot path | PASS -- all new methods return bool or void |
| JS-002 | No return null | PASS -- all new methods return bool |
| JS-033 | No async void | PASS -- synchronous methods only |
| JS-036/037 | No heap alloc in hot path | PASS -- StartsWith is zero-alloc, no new collections |

---

## LANE-SPLIT GATE RESULT

**SINGLE-PIPELINE**

One defect, one fix. Two methods modified, one method added. Single ticket. Single PR.

---

## 1. Diagnosis of v1 Failure

### 1.1 What v1 Fixed

v1 placed `FlattenIfNotArming` as a guard in the `NakedPositionDetector` Dispatcher.InvokeAsync
callback. At callback execution time, `HasArmingAtmBrackets` scans `acc.Orders` for Working,
Submitted, Accepted, or TriggerPending ATM brackets. If found, the flatten is suppressed.

v1 ASSUMPTION: By the time the Dispatcher.InvokeAsync callback executes on the UI thread, NT8
has already placed the new ATM brackets in `acc.Orders` at Submitted state or higher.

### 1.2 Why v1 Failed in SIM

**v1 assumption is invalid for the entry-fill trigger path.**

`TryNakedDetect` (L7199) fires on ANY terminal order event on a follower account:

```csharp
private void TryNakedDetect(OrderEventArgs e)
{
    if (e.Order.OrderState != Filled && != Cancelled && != Rejected)
        return;
    if (!IsFollowerAccount(e.Order.Account))
        return;
    NakedPositionDetector(e.Order.Account);
}
```

When the new PTT-Copy entry fills on a follower (PTT-Copy:Filled), this fires with:
- `e.Order.OrderState == Filled` -- passes gate 1
- `IsFollowerAccount` -- passes gate 2
- `NakedPositionDetector` invoked

At the MOMENT of the PTT-Copy entry fill event:
- `acct.Positions` shows qty > 0 (position just opened) -- `HasNakedPosition` returns true
- `acct.Orders` contains NO Working/Submitted stops or targets -- brackets have NOT been
  armed yet (NT8 calls StartAtmStrategy AFTER dispatching the fill event)
- `IsNakedConditionMet` (L7273) sees no Working/Submitted stops/targets -- returns true
- `HasNakedPosition` = true -- `NakedPositionDetector` queues `FlattenIfNotArming` callback

By the time the Dispatcher.InvokeAsync callback drains from the queue:
- NT8 may have placed brackets (Working) -- `HasArmingAtmBrackets` = true -- fix works
- OR NT8 has NOT yet populated acc.Orders with the new brackets -- `HasArmingAtmBrackets`
  = false -- `FlattenOneAccount` fires -- **REVERSAL**

This is a DIFFERENT race than the one v1 targeted. v1 targeted the cancel-storm race
(last bracket cancel → naked window). The SIM failure exposes the entry-fill race
(entry fill → naked window before bracket arm).

**Both races produce the same false positive.** v1 covered only the cancel-storm path.

### 1.3 Evidence from SIM Logs

| Observation | What It Proves |
|-------------|----------------|
| "PTT-COPY confirmed for Sim102/103/104" | New entry dispatch succeeded |
| "Entry filled on all accounts; new ATM brackets appeared at END of order lists in Submitted state" | Brackets ARE Submitted eventually -- but fill event fired FIRST |
| "fo=NULL on EVERY bracket arm event (Stop1..Target3)" | SyncFollowerBracket can't find brackets during arm events -- confirms acc.Orders lag |
| "Account Data showed LONG 1 contract (reversed -- leader was SHORT)" | PTT-Flatten fired and closed the long; orphaned brackets caused re-open, OR flatten itself was the reversal |
| "PositionStateChanged hasPos=False fired BEFORE the new entry filled" | hasPos=False is UI-only (UpdateButtonColors); does NOT cause flatten |

**PositionStateChanged is NOT a flatten trigger.** Both `TradeCopierPanel.OnPositionStateChanged`
(L713) and `TradeCopierWindow.OnPositionStateChanged` (L218) call only `UpdateButtonColors`.
The reversal originates entirely from `NakedPositionDetector` → `FlattenIfNotArming` path.

---

## 2. Q1-Q4 Diagnostic Answers

### Q1: Does HasArmingAtmBrackets include OrderState.Initialized?

**Answer: NO.** Current states (L5271-5275): Working, Submitted, Accepted, TriggerPending.
`Initialized` is absent.

**Impact**: If NT8 has placed bracket orders in `acc.Orders` at `Initialized` state (just
after `CreateOrder`, before `Submit` is called), `HasArmingAtmBrackets` returns false
despite brackets being present. This is a secondary gap; the primary gap is the entry-fill
trigger path where no bracket orders exist in `acc.Orders` at all.

### Q2: Are there other FlattenOneAccount call sites NOT guarded?

**Answer: NO unguarded sites in the NakedPositionDetector path.**

All `FlattenOneAccount` call sites:

| Location | Call Site | Guarded? | Intentional? |
|----------|-----------|----------|--------------|
| L4997 | `Flatten(Instrument)` -- user-initiated | No guard needed | YES -- manual |
| L5037 | `Flatten(Account, Instrument)` leader | No guard needed | YES -- manual |
| L5042 | `Flatten(Account, Instrument)` followers | No guard needed | YES -- manual |
| L5184 | `FlattenIfNotArming` | Guarded by HasArmingAtmBrackets | YES -- v1 fix |
| L1586 | delegate passed to TryDispatchLeaderFlat | No guard needed | YES -- DW-B65-01 |

The unguarded call sites are intentional user/leader-initiated flattens. The v1 fix
correctly guarded only the NakedPositionDetector path.

### Q3: Is PositionStateChanged wired to a separate flatten path?

**Answer: NO.**

- `TradeCopierPanel.OnPositionStateChanged` (L713): calls `UpdateButtonColors` only
- `TradeCopierWindow.OnPositionStateChanged` (L218): calls `UpdateButtonColors` only

`PositionStateChanged hasPos=False` is a UI coloring signal only. It does not trigger
any flatten, cancel, or order dispatch. This path is confirmed safe.

### Q4: Does HasNakedPosition return stale true before new position is stable?

**Answer: YES -- this is the ROOT CAUSE.**

`HasNakedPosition` correctly returns true when: position qty > 0 AND
`IsNakedConditionMet` finds no Working/Submitted stops/targets.

At the moment of PTT-Copy:Filled event:
- `acct.Positions` qty > 0 -- entry just filled -- CORRECT
- `acct.Orders` contains no Working/Submitted stops/targets -- brackets NOT YET ARMED

`IsNakedConditionMet` (L7279) checks only `Working` and `Submitted`:
```csharp
if (o.OrderState != OrderState.Working && o.OrderState != OrderState.Submitted)
    continue;
```

Even if `Accepted` and `Initialized` were added here, it would not help: at entry fill
time, NO bracket orders exist in `acc.Orders` at all. The brackets are created by NT8's
StartAtmStrategy which runs AFTER the fill event dispatches.

`HasNakedPosition` returns `true` legitimately (position exists, no brackets yet) --
but it is NOT a naked position; it is a transient state during bracket arming.

---

## 3. Root Cause Statement

**Two independent races produce false-positive `HasNakedPosition`:**

**Race 1 (v1 targeted -- cancel-storm)**: BE-ALL cycle cancels all brackets for all
followers. Last cancel ack fires `TryNakedDetect`. `HasNakedPosition` = true (position
exists, all brackets cancelled). `Dispatcher.InvokeAsync` callback queued. New entry
fills and brackets arm BEFORE callback executes. v1 fix guards this correctly IF brackets
are in acc.Orders before the callback runs.

**Race 2 (NEW -- entry-fill trigger, v1 DID NOT TARGET)**: New PTT-Copy entry fills on
follower. `TryNakedDetect` fires on the fill event (Filled state triggers the gate).
`HasNakedPosition` = true (position just opened, brackets not yet armed). Dispatcher
callback queued. NT8 starts arming brackets. Dispatcher callback may execute BEFORE
brackets appear in acc.Orders. `HasArmingAtmBrackets` returns false. `FlattenOneAccount`
fires. Reversal.

**Race 2 is the root cause of the SIM failure.** The v1 fix was bypassed because it
guarded the Dispatcher callback AFTER the brackets should have appeared, but the Race 2
trigger fires at entry fill -- the earliest possible moment -- before NT8 even starts
bracket arming.

---

## 4. v2 Fix Design

### 4.1 Strategy

**Primary fix (new)**: Guard `TryNakedDetect` itself. If the triggering order is a
PTT-Copy entry fill (not a bracket fill, not a flatten fill), skip `NakedPositionDetector`.
An entry fill is not a naked-position signal; it is the start of an ATM bracket arm
sequence. NT8 will arm brackets shortly; no intervention needed.

**Secondary fix (hardening)**: Add `OrderState.Initialized` to `HasArmingAtmBrackets`
state check. This closes the gap identified in v1 Section 6 (DW-LB-FL-01-STATE-COVERAGE).
It catches the case where the Dispatcher callback from Race 1 runs while brackets are in
the Initialized state window (CreateOrder called, Submit not yet called).

### 4.2 New Method: IsPttCopyEntry

```csharp
// DW-LB-FL-01-V2: returns true if order is a PTT-Copy follower entry order.
// PTT-Copy = standard copy mode entry. "Entry" = Named ATM mode (Clone mode uses "Entry").
// Source: SendCopy L4794 (PTT-Copy), IsQxCancelCandidate L912 ("Entry" as Named ATM entry).
// Used by TryNakedDetect to skip NakedPositionDetector on entry fills -- an entry fill is
// NOT a naked-position signal; it is the first event of the ATM bracket arm sequence.
// CYC=2: base(1) + Name=="Entry"(1). JS-021: no lock (static). JS-001: no throw.
// JS-002: returns bool. ASCII-only.
private static bool IsPttCopyEntry(Order o) =>
    o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal) || o.Name == "Entry";
```

**McCabe**: CYC=2 (base + OR branch). **PASS <= 8**.

**Note**: This replicates the exact predicate at L875 (`IsPttEntryOrderCancelTrigger`) and
L4032 (HandleEntryChange) and L4773 (HasWorkingEntries) where "PTT-Copy" OR "Entry" is the
canonical entry order name pair. No new naming assumption introduced.

### 4.3 Modified Method: TryNakedDetect

**Current code (L7199-7210)**:
```csharp
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
    NakedPositionDetector(e.Order.Account);
}
```

**v2 code**:
```csharp
// DW-LB-FL-01-V2: skip naked-position check on entry fill -- ATM brackets are about to
// arm via StartAtmStrategy. An entry fill does NOT indicate a naked position; it is the
// first event of the bracket arm sequence. Firing NakedPositionDetector here creates a
// race: position exists but brackets not yet in acc.Orders -> false positive -> reversal.
// IsPttCopyEntry covers "PTT-Copy" (standard mode) and "Entry" (Named ATM mode).
// Does NOT skip on bracket fills or flatten fills -- those remain valid naked-detect triggers.
// CYC=4: original CYC=3 + 1 new branch. JS-021: no lock. JS-001: no throw. ASCII-only.
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

**McCabe**: CYC=4 (was 3, +1 branch for entry-fill skip). **PASS <= 8**.

**Change**: One guard added. One new static method extracted. `NakedPositionDetector` is
unchanged. `FlattenIfNotArming` is unchanged. `HasArmingAtmBrackets` receives secondary
hardening (see 4.4).

### 4.4 Modified Method: HasArmingAtmBrackets (secondary hardening)

**Current stateActive check (L5271-5275)**:
```csharp
bool stateActive =
    o.OrderState == OrderState.Working
    || o.OrderState == OrderState.Submitted
    || o.OrderState == OrderState.Accepted
    || o.OrderState == OrderState.TriggerPending;
```

**v2 stateActive check (add Initialized)**:
```csharp
bool stateActive =
    o.OrderState == OrderState.Initialized       // DW-LB-FL-01-V2 belt+suspenders
    || o.OrderState == OrderState.Working
    || o.OrderState == OrderState.Submitted
    || o.OrderState == OrderState.Accepted
    || o.OrderState == OrderState.TriggerPending;
```

**McCabe**: CYC=5 (unchanged -- compound bool assigned to local variable counts as 1 branch
in McCabe regardless of OR count; same pattern as v1 comment). **PASS <= 8**.

**Purpose**: If Race 1 Dispatcher callback runs during the Initialized window (bracket
CreateOrder called, Submit not yet called), the callback correctly suppresses the flatten.
Belt-and-suspenders for the cancel-storm path. Does not address Race 2 (which is prevented
by the primary fix in TryNakedDetect).

### 4.5 Summary of Changes

| Method | Change | CYC Before | CYC After |
|--------|--------|------------|-----------|
| `TryNakedDetect` | +1 entry-fill skip guard, calls new `IsPttCopyEntry` | 3 | 4 |
| `IsPttCopyEntry` | NEW static method | -- | 2 |
| `HasArmingAtmBrackets` | +1 state: Initialized added to stateActive compound | 5 | 5 |
| `FlattenIfNotArming` | Unchanged | 2 | 2 |
| `NakedPositionDetector` | Unchanged | 6 | 6 |
| `IsNakedConditionMet` | Unchanged | 4 | 4 |

**Files modified**: `src/PropTraderTools/CopyEngine.cs` (1 file only).

---

## 5. Why This Fix Is Complete

**Scenario 1 -- Race 1 (cancel-storm, BE-ALL cycle)**:
- CancelQxBrackets fires → 4 cancel acks → last cancel: TryNakedDetect fires on Cancelled event
- `IsPttCopyEntry(cancelledBracket)` = false (bracket name is "Stop1"/"Target1", not "PTT-Copy")
- NakedPositionDetector still fires -- correct (this is the cancel-storm naked path)
- Dispatcher callback: HasArmingAtmBrackets now includes Initialized + Working + Submitted + Accepted + TriggerPending
- If new entry fills and brackets arm before callback: HasArmingAtmBrackets = true → skip ✓
- If brackets are in Initialized state: HasArmingAtmBrackets = true → skip ✓ (new secondary fix)

**Scenario 2 -- Race 2 (entry fill, NEW path)**:
- PTT-Copy:Filled event fires → TryNakedDetect → IsPttCopyEntry = true → return immediately
- NakedPositionDetector is NOT invoked for entry fills → no Dispatcher callback queued
- Brackets arm normally → no false PTT-Flatten → CORRECT ✓

**Scenario 3 -- Legitimate naked position (orphaned)**:
- Position exists, all brackets Filled or Cancelled
- Non-entry terminal event fires TryNakedDetect (e.g. a PTT-STP-Drag fill, or a bracket Filled)
- IsPttCopyEntry returns false (not an entry) → NakedPositionDetector fires
- HasNakedPosition = true (position exists, no Working/Submitted stops/targets)
- FlattenIfNotArming: HasArmingAtmBrackets = false (all brackets terminal) → FlattenOneAccount ✓

**Scenario 4 -- Legitimate naked position triggered by entry fill (edge case)**:
- An entry fills with NO ATM configured (user forgot to set ATM on the copier rule)
- TryNakedDetect fires on PTT-Copy:Filled → IsPttCopyEntry = true → return early
- Position is genuinely naked but NakedPositionDetector skips it for this event
- MITIGATION: The NEXT terminal order event on this account (any cancel, fill, reject) will
  trigger TryNakedDetect → IsPttCopyEntry = false → NakedPositionDetector fires → detects
  naked position. The 500ms debounce window also ensures re-evaluation.
- ACCEPTABLE RISK: The window where a truly naked entry is missed is bounded to the time
  between entry fill and the next terminal order event on the account. In practice, broker
  acks for the entry (PartFilled events, position margin acks) generate additional events.

---

## 6. Preserved Invariants

| Guard | Location | Status |
|-------|----------|--------|
| DW-B65-01 bypass (IsNativeExitName) | TryDispatchLeaderFlat L4712 | PRESERVED -- unchanged code path |
| DW-LB-FL-02 (IsNativeExitOnFlatLeader) | TryDispatchLeaderFlat L4710 | PRESERVED -- unchanged code path |
| PTT-Flatten in-flight guard (HasInflightFlatten) | IsAccountFlattenable L5204 | PRESERVED -- unchanged |
| DW-B94 (IsNonFlatDispatchName) | TryDispatchLeaderFlat L4708 | PRESERVED -- unchanged |
| v1 FlattenIfNotArming guard | NakedPositionDetector L7241 | PRESERVED -- unchanged, still active for non-entry triggers |

---

## 7. Risk Assessment

### PRIMARY RISK: Legitimate naked entry with no ATM

**Scenario**: Follower entry fills, no ATM template configured, position is genuinely naked.
The new guard skips NakedPositionDetector on the fill event. The position remains unprotected
until the next terminal order event fires NakedPositionDetector.

**Mitigation**:
- Any subsequent order event on the account (even an unrelated one) will re-trigger TryNakedDetect
- The 500ms debounce resets after the entry-fill event (no callback was queued for it)
- In clone/copy mode, a position without ATM is an operational error, not a software defect
- The NakedPositionDetector is a SAFETY NET for legitimate naked positions -- it is not the
  primary protection mechanism (ATM brackets ARE the primary protection)

**Risk level**: LOW. Acceptable trade-off to prevent false PTT-Flatten reversal.

### SECONDARY RISK: "Entry" name collision

**Scenario**: A non-follower entry order named "Entry" exists on a follower account and fills.
IsPttCopyEntry returns true, NakedPositionDetector is skipped.

**Mitigation**:
- `IsFollowerAccount` guard (L7207) already validates the account is a follower BEFORE
  TryNakedDetect would reach IsPttCopyEntry check
- Named ATM mode uses "Entry" ONLY for copy entries (L4780: "signalName is ALWAYS PTT-Copy
  for ALL modes"; "Entry" is the broker-side name for Named ATM follower entries)
- A genuine third-party "Entry" order on a follower account is operationally impossible in
  the copier's designed usage

**Risk level**: NEGLIGIBLE.

### TERTIARY RISK: Debounce interaction

**Scenario**: Race 1 Dispatcher callback is ALREADY in the Dispatcher queue when Race 2
entry-fill event fires. The primary fix skips queuing a NEW callback for Race 2, but the
old Race 1 callback still runs. HasArmingAtmBrackets (with Initialized added) guards it.

**Mitigation**: Secondary fix (Initialized state) covers this exactly. Both fixes are
required and complementary. Race 1 callback is guarded by HasArmingAtmBrackets. Race 2 is
pre-guarded by TryNakedDetect entry-fill skip.

**Risk level**: COVERED by combined primary + secondary fix.

---

## 8. Test Design

All tests are xUnit [Fact] unit tests. File: `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`.

### New Tests for v2

| # | Test Name | What It Asserts |
|---|-----------|-----------------|
| T11 | `IsPttCopyEntry_ReturnsTrue_ForPttCopyName` | Order with Name="PTT-Copy" returns true |
| T12 | `IsPttCopyEntry_ReturnsTrue_ForEntryName` | Order with Name="Entry" returns true |
| T13 | `IsPttCopyEntry_ReturnsFalse_ForStop1` | Order with Name="Stop1" returns false |
| T14 | `IsPttCopyEntry_ReturnsFalse_ForPttFlatten` | Order with Name="PTT-Flatten" returns false |
| T15 | `IsPttCopyEntry_ReturnsFalse_ForPttQxT` | Order with Name="PTT-QX-T1" returns false |
| T16 | `TryNakedDetect_SkipsDetector_WhenEntryFills` | PTT-Copy Filled on follower -> NakedPositionDetector not invoked |
| T17 | `TryNakedDetect_InvokesDetector_WhenBracketCancels` | Stop1 Cancelled on follower -> NakedPositionDetector invoked |
| T18 | `TryNakedDetect_InvokesDetector_WhenNonEntryFills` | Stop1 Filled on follower -> NakedPositionDetector invoked |
| T19 | `HasArmingAtmBrackets_ReturnsTrue_WhenStop1IsInitialized` | Stop1 in Initialized state returns true (v2 hardening) |

### Regression Tests from v1 (T1-T10 remain valid, unchanged)

T1-T10 from v1 architecture plan remain required. T3-T5 (Working/Accepted/TriggerPending)
and T19 (Initialized) now cover all five active states in HasArmingAtmBrackets.

---

## 9. SIM Verification Steps

### Step 1: Baseline -- confirm no regression on first entry

1. Start fresh SIM session, Clone mode, 3 follower accounts
2. Leader enters SHORT
3. Verify followers go SHORT (not LONG) -- no reversal
4. Verify no PTT-Flatten fires in the Output tab during bracket arming
5. Verify "flat-guard: bracket-arm skip" does NOT appear in log (should not appear on first
   entry -- the entry-fill guard prevents NakedPositionDetector from running at all)

### Step 2: BE-ALL cycle + second entry

1. While followers SHORT, trigger BE-ALL
2. Verify all brackets cancel, positions close
3. Wait for leader re-entry SHORT
4. Verify PTT-COPY dispatched on all followers
5. **KEY CHECK**: Monitor Output tab during bracket arming window
   - EXPECTED: NO "PTT-Flatten" log lines for followers during Stop1..Target3 arming
   - EXPECTED: NO reversal -- followers remain SHORT (matching leader)
   - EXPECTED: "flat-guard: bracket-arm skip" may appear in log if Race 1 callback fires (benign)
6. After brackets arm, verify account shows SHORT position with Working brackets

### Step 3: Legitimate naked position test

1. While followers SHORT, manually cancel all bracket orders from NT8 order window
2. Wait 600ms (debounce window + margin)
3. **KEY CHECK**: Verify PTT-Flatten fires and closes the SHORT position correctly
   - EXPECTED: Log shows "PTT-Flatten" submitted and filled on all followers
   - EXPECTED: Followers go flat (position closed)

### Step 4: Regression -- manual Flatten button

1. While followers have open positions with brackets
2. Press Flatten button in PTT panel
3. Verify all followers flatten immediately without interference from new guards

---

## 10. Spec Requirement Traceability

| Symptom | v2 Fix Element | Covers |
|---------|----------------|--------|
| PTT-Flatten fires on second entry after BE-ALL cycle | TryNakedDetect: skip on PTT-Copy fill (primary) | Race 2: entry fill triggers NakedPositionDetector before brackets arm |
| HasArmingAtmBrackets returns false during brief Initialized window | HasArmingAtmBrackets: add Initialized (secondary) | Race 1: Dispatcher callback runs during CreateOrder->Submit gap |
| Legitimate naked position must still flatten | IsPttCopyEntry only skips on entry fills; all other terminal events still invoke NakedPositionDetector | Scenario 3 verified |
| DW-B65-01 bypass preserved | Fix is in TryNakedDetect, not in TryDispatchLeaderFlat path | DW-B65-01 path unchanged |
| CYC <= 8 on all modified methods | TryNakedDetect: 4, IsPttCopyEntry: 2, HasArmingAtmBrackets: 5 | All PASS |
| No lock() | static methods, .ToList() snapshot, StartsWith is zero-alloc | PASS |
| PTT- prefix on all order names | IsPttCopyEntry checks "PTT-Copy" (canonical v1 name per L4794) | PASS |

---

## Summary

**Root cause**: `TryNakedDetect` fires on every terminal order event on follower accounts,
including the PTT-Copy entry fill. At the moment of entry fill, the position exists but ATM
brackets have not yet been placed in `acc.Orders`. `HasNakedPosition` correctly sees a
position with no stops/targets -- but this is transient, not a real naked position. The
`FlattenIfNotArming` guard from v1 cannot help because it runs AFTER the brackets should
have appeared, but the entry-fill event is the EARLIEST event in the sequence -- before NT8
even starts `StartAtmStrategy`.

**Primary fix**: Add one guard in `TryNakedDetect`: if the triggering order is a PTT-Copy
entry fill, return immediately without invoking `NakedPositionDetector`. Entry fills are not
naked-position signals; they are the first event of the bracket arm sequence.

**Secondary fix**: Add `OrderState.Initialized` to `HasArmingAtmBrackets` state check.
Closes the gap flagged in v1 Section 6 for the cancel-storm Dispatcher callback path.

**Files modified**: `src/PropTraderTools/CopyEngine.cs` (1 file only).
**Methods changed**: `TryNakedDetect` (L7199), `HasArmingAtmBrackets` (L5265).
**Methods added**: `IsPttCopyEntry` (new static, adjacent to TryNakedDetect, ~L7198).
**Tests added**: T11-T19 in `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`.
**P0 violations introduced**: None.
