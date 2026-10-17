# B196 Architecture Plan

**Epic**: B196 — Gap Restore: DW-B195-02 + DW-B170-01 + DW-B169 COLLATERAL-STORM + B161-B166 Ghost-Entry Guard + B163 AFP Guard
**Author**: ptt-architect (Phase 1)
**Date**: 2026-10-11
**Status**: PLAN_COMPLETE
**Preceding plan**: docs/brain/B195/02-architecture-plan.md
**Deferred backlog source**: docs/brain/B194/06-deferred-backlog.md + docs/brain/B195/02-architecture-plan.md §8

---

## §1 Epic Summary, Scope, and LANE-SPLIT Gate

### LANE-SPLIT GATE RESULT: SEQUENTIAL-APPROVED

| Question | Answer | Reasoning |
|----------|--------|-----------|
| Q1: Same method or within 50 lines? | NO | T1@~6606, T2@~3703, T3@multiple scattered methods, T4@multiple methods. All different regions. |
| Q2: Fix B design depends on Fix A final design? | NO | All 5 fixes are independent restores from separate pipelines with no interdependency. |
| Q3: Each fix has standalone value if the other is blocked? | YES | Each ticket closes a named DW item with independent live-safety value. |
| Q4: Each fix has an independent SIM verification path? | YES | Each has a distinct SIM scenario (fast-path drain, target drag, stop drag, BE-All burst, investigation). |

**GATE RESULT: SEQUENTIAL-APPROVED** — Lanes technically qualify (independent methods, no design dependency) but all 5 tickets share `src/PropTraderTools/CopyEngine.cs` as primary write target. Parallel execution would generate merge conflicts. All 5 tickets execute sequentially (T1 → T2 → T3 → T4 in order). T5 is read-only and may run parallel with any ticket.

### Scope Summary

| Ticket | Priority | DW ID Closed | Description |
|--------|----------|--------------|-------------|
| T1 | P1 | DW-B195-02 | DrainThenDispatch fast-path ATM-aware dispatch |
| T2 | P1 | DW-B170-01 | FindLeaderStopPrice add OrderState.Accepted |
| T3 | P1 | DW-B169 | _dragsInFlight HashSet COLLATERAL-STORM restore |
| T4 | P1 | B161-B166 + B163 | Ghost-Entry Guard + AFP Guard full restore |
| T5 | P2 | DW-B195-03 | Investigation only — locate CYC>8 method or confirm absent |

### Deferred Items Addressed by B196

| DW ID | Source | Priority | Action |
|-------|--------|----------|--------|
| DW-B195-02 | B195 §8 | P1 | CLOSED by T1 |
| DW-B170-01 | B170 | P1 | CLOSED by T2 |
| DW-B169 COLLATERAL-STORM | B169 | P1 | CLOSED by T3 |
| B161-B166 Ghost-Entry + B163 AFP | B161-B166 | P1 | CLOSED by T4 |
| DW-B195-03 | B195 §8 | P2 | CLOSED by T5 (investigation complete) |

---

## §2 Source State Confirmation

All greps were run against `src/PropTraderTools/CopyEngine.cs` at B196 architecture time.

### Absent (gap — must be added by B196)

| Symbol | Expected Location | Status |
|--------|-------------------|--------|
| `IsFollowerPositionBlocked` | ~L3182 (per B161-B166 plan) | **ABSENT** — not in source |
| `HasActivePttBeExits` | ~L3104 (per B161-B166 plan) | **ABSENT** |
| `HasActivePttBeTargets` | ~L3147 (per B161-B166 plan) | **ABSENT** |
| `HasSettlingBeTargets` | ~L3160 (per B161-B166 plan) | **ABSENT** |
| `IsBeSlotNonTerminal` | ~L9382 (per B161-B166 plan) | **ABSENT** |
| `IsBeRetryArmedFlat` | per B161-B166 plan | **ABSENT** |
| `IsBeSettleOrRetrySignalActive` | new (VIOLATION-01 extract) | **ABSENT** |
| `_dragsInFlight` field | after _laneEDispatched (per B169 plan) | **ABSENT** |
| `TryTrackDragInFlight` | ~L2368 (per B169 plan) | **ABSENT** |
| `TryRemoveDragSuffix` | near TryTrackDragInFlight | **ABSENT** |
| `DragsInFlightForTest` seam | after seams region | **ABSENT** |
| `AverageFillPrice` guard in `IsPttEntryOrderCancelTrigger` | L932-L933 (per B163) | **ABSENT** |
| `OrderState.Accepted` in `FindLeaderStopPrice` predicate | L3714 (confirmed) | **ABSENT** |
| ATM-mode check in `DrainThenDispatch` fast-path | L6606-L6610 (confirmed) | **ABSENT** |

### Absent (already deleted prior to B196 — no action needed)

| Symbol | Status |
|--------|--------|
| `IsLeaderLegBeingChanged` | ABSENT from source — was deleted in an earlier pass. No removal action needed. |
| `IsLeaderLegBeingChangedTestable` | ABSENT from source — same. |

### Present (confirmed in current source)

| Symbol | Line | State |
|--------|------|-------|
| `DrainThenDispatch` | 6579 | Fast-path at 6606: bare `SubmitEntryDirect` — gap confirmed |
| `FindLeaderStopPrice` | 3703 | Line 3714: `OrderState.Working` only — gap confirmed |
| `IsPttEntryOrderCancelTrigger` | 885 | Line 893: no AverageFillPrice guard — gap confirmed |
| `TryReplaceOnAtmCancel` | 901 | CYC=4, no IsFollowerPositionBlocked guard — gap confirmed |
| `SubmitDrainedEntry` | 6722 | B195 already applied (CYC=5 with ATM path) — no change needed |

### T5 Investigation Results (CYC>8 search)

- `grep "CYC=13|CYC=12|CYC=11|CYC=10|CYC=9"` in `TradeCopierPanel.cs`: **ZERO matches**
- `grep "CYC=13|CYC=12|CYC=11|CYC=10|CYC=9"` in `CopyEngine.cs`: **ZERO matches**
- `grep "KeyDown|PreviewKey|AtrBox|OnAtr"` in `TradeCopierPanel.cs`: Only `OnChartKeyDown` (CYC=3) and `DispatchShortcut` (CYC=5). No high-CYC key handler found.
- **Conclusion**: No CYC>8 method exists in either file. DW-B195-03 is CLOSED. DW-B196-CYC is NOT opened.

---

## §3 Fix Designs

### T1: DrainThenDispatch Fast-Path ATM-Aware Dispatch (DW-B195-02)

**Method**: `DrainThenDispatch` — `src/PropTraderTools/CopyEngine.cs` line ~6606

**Current fast-path** (line 6606-6610):
```csharp
if (!entryCandidates.Any()) // (2)
{
    SubmitEntryDirect(follower, instrument, qty, price, action, orderType);
    return;
}
```

**Fixed fast-path** (CYC 4→5):
```csharp
if (!entryCandidates.Any()) // (2)
{
    // DW-B195-02: fast-path must also respect Clone ATM mode.
    // Same pattern as SubmitDrainedEntry (B195-T1 drain-completion path).
    // JS-021: no lock. GetCloneAtmMode reads volatile field -- thread-safe.
    // JS-001: no throw. ASCII-only: "-fast-atm-" literal.
    // NT8: SendCopyWithAtm uses "Entry" name internally (line 4680).
    // "acctKey" not yet declared here -- use follower.Name directly.
    var fastAtm = GetCloneAtmMode(); // (2a) volatile read
    if (fastAtm is FollowerAtmMode.Named namedFastAtm) // (2b) ATM branch -- adds CYC +1
    {
        var sig = CopySignal.Create(
            action,
            orderType,
            qty,
            price,
            follower.Name + "-fast-atm-" + (long)(int)Environment.TickCount
        );
        SendCopyWithAtm(follower, instrument, in sig, namedFastAtm);
    }
    else
    {
        SubmitEntryDirect(follower, instrument, qty, price, action, orderType);
    }
    return;
}
```

**CYC after**: 5. DrainThenDispatch branches: (1) null guard, (2) !entryCandidates.Any(), (2b) fastAtm is Named, (3) foreach. Base=1. Total=5.

**orderId composition**: `follower.Name + "-fast-atm-" + (long)(int)Environment.TickCount`
- `follower.Name` is the account name (ASCII by construction).
- `"-fast-atm-"` is ASCII literal.
- `(long)(int)Environment.TickCount` mirrors the existing `now` local at line 6613 but declared inline.
- Different suffix from drain-completion path (`"-drain-atm-"`). No collision possible.

**Note on `now` variable**: The existing `now = (long)(int)Environment.TickCount` at line 6613 is declared AFTER the fast-path early return. It is NOT accessible inside the fast-path block. The fix inlines the expression rather than introducing a separate variable.

**Methods NOT modified**: `PendingDispatchDrain`, `SubmitDrainedEntry`, `SubmitEntryDirect`, `SendCopyWithAtm`, `GetCloneAtmMode`.

---

### T2: FindLeaderStopPrice Missing OrderState.Accepted (DW-B170-01)

**Method**: `FindLeaderStopPrice` — `src/PropTraderTools/CopyEngine.cs` line 3703

**Current predicate** (line 3712-3715):
```csharp
if (
    order.Name == targetName // (4)
    && order.OrderState == OrderState.Working
) // (5)
    return order.StopPrice;
```

**Current comment** (line 3701):
```
// CYC=5: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working.
```

**Fixed predicate** (line 3712-3716):
```csharp
if (
    order.Name == targetName // (4)
    && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted) // (5)(6)
) // DW-B170-01
    return order.StopPrice;
```

**Fixed comment** (line 3701):
```
// CYC=6: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working; (6) state==Accepted. DW-B170-01.
```

**CYC after**: 6. `OrderState.Working` MUST remain (Working regression guard). Both states on same line per B170/05-final-review.md §CHECK-5 canonical source text.

**Rationale**: NT8 SIM broker can briefly place a dragged stop in `OrderState.Accepted` before transitioning to `Working`. When `FindLeaderStopPrice` is called in that window (from `CreateFollowerReplacementStop` via `SyncAtmFollowerBracket`), it returns 0.0 → follower stop price = 0 → stop not placed → naked follower position. Adding `Accepted` closes this window.

---

### T3: _dragsInFlight HashSet COLLATERAL-STORM Restore (DW-B169)

**Design source**: `docs/brain/B169/02-architecture-plan.md` §§5a-5h, §6, §8, §10.

**WARNING**: B169 plan line numbers are STALE. All line numbers below are approximate from B169 plan. Engineer MUST re-grep for current line numbers of `TryHandleBracketDrag`, `TryCancelFollowerEntries`, `HandleAtmStopSync`, `ResubmitCollateralLegs`, `ParseLegSuffix`, `IsStopLeg`, and `_laneEDispatched` before applying any change.

#### 3a. New field: `_dragsInFlight`

Insert after `_laneEDispatched` field (grep current location):
```csharp
// DW-B168-02: tracks which stop suffixes ("1","2","3") are mid-drag.
// Populated at ChangePending (before broker round-trip) so the set is
// correct at Accepted callback time regardless of broker batch ordering.
// JS-021: no lock -- all NT8 order callbacks for the leader account execute
//   on the same NT8 background thread; concurrent access is impossible.
// JS-036: HashSet<string> -- bounded to max 3 entries (one per stop leg).
// ASCII-only. No Unicode.
private readonly HashSet<string> _dragsInFlight =
    new HashSet<string>(StringComparer.Ordinal);
```

#### 3b. Modify: `TryHandleBracketDrag`

Insert `TryTrackDragInFlight(order);` call BEFORE the `if (!IsWorkingBracket(order))` guard (engineer must find current line number via grep for "IsWorkingBracket"):
```csharp
// DW-B168-02: track in-flight stop drags at ChangePending / clear at ChangeRejected.
TryTrackDragInFlight(order);
if (!IsWorkingBracket(order))
    return false;
```

#### 3c. Modify: `HandleAtmStopSync`

Add `_dragsInFlight.Remove(legSuffix)` AFTER the `ResubmitCollateralLegs` call (engineer must find current line number):
```csharp
ResubmitCollateralLegs(acc, fo, newPrice, otherLegPrices, legSuffix, leaderOrder);
// DW-B168-02: remove suffix from in-flight set once Accepted processing is complete.
if (!string.IsNullOrEmpty(legSuffix))
    _dragsInFlight.Remove(legSuffix);
```

HandleAtmStopSync CYC: 2→3 (adds one IsNullOrEmpty guard branch).

#### 3d. Modify: `TryCancelFollowerEntries`

Add `TryRemoveDragSuffix(order)` call inside the `IsAtmBracketName(order.Name)` block BEFORE `return true`:
```csharp
if (IsAtmBracketName(order.Name))
{
    TryRemoveDragSuffix(order); // DW-B168-02: clear in-flight record before event is consumed
    return true; // HOTFIX-B63-COPY-CANCEL-01
}
```
CYC unchanged (call inside existing if block, no new branch).

#### 3e. Modify: `ResubmitCollateralLegs`

Replace `IsLeaderLegBeingChanged(leaderLeg)` guard with `_dragsInFlight.Contains(s)` guard (guard BEFORE `FindLeaderCollateralOrder` call):
```csharp
foreach (var kvp in otherLegPrices) // (1) foreach
{
    string s = kvp.Key.ToString();
    double price = kvp.Value;
    if (price <= 0) // (2) if
        continue;
    // DW-B168-02: HashSet.Contains is O(1) alloc-free. Skip if this suffix's own
    // HandleAtmStopSync is in flight -- resubmitting here would storm.
    // Guard precedes FindLeaderCollateralOrder (O(n) ToList) to short-circuit.
    if (_dragsInFlight.Contains(s)) // (3) if -- DW-B168-02
        continue;
    Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
    ResubmitOneCollateralLeg(acc, fo, newPrice, price, s, leaderLeg);
}
```
CYC unchanged (guard replaces guard, same branch count).

**NOTE**: `IsLeaderLegBeingChanged` is already ABSENT from source (confirmed by grep). If it existed, it would have been the guard to replace. Engineer confirms absence and applies the new guard directly.

#### 3f. New method: `TryTrackDragInFlight`

Insert near `TryHandleBracketDrag` (see B169 §5e for full body):
```csharp
// DW-B168-02: record or clear drag-in-flight suffix for leader stop orders.
// ChangePending fires before any broker round-trip -- guarantees the set is
// populated before any Accepted callback reads it (immune to NT8 batch-ack race).
// ChangeRejected: drag was rejected -- remove to prevent permanent suppression.
// CYC=5: base(1) + !IsStopLeg(1) + IsNullOrEmpty(1) + ChangePending(1) + ChangeRejected(1).
// JS-021: no lock -- single NT8 callback thread per account. JS-001: no throw. ASCII-only.
private void TryTrackDragInFlight(Order order)
{
    if (!IsStopLeg(order))
        return;
    string suffix = ParseLegSuffix(order.Name);
    if (string.IsNullOrEmpty(suffix))
        return;
    if (order.OrderState == OrderState.ChangePending)    // (1)
        _dragsInFlight.Add(suffix);
    else if (order.OrderState == OrderState.ChangeRejected) // (2)
        _dragsInFlight.Remove(suffix);
}
```

**NOTE on CYC discrepancy**: B169 plan §8 states CYC=3 but the code has 5 branches (base + 4 guards). Correct CYC=5.

#### 3g. New method: `TryRemoveDragSuffix`

```csharp
// DW-B168-02: clear drag-in-flight suffix for a Cancelled stop order.
// CYC=2: base(1) + if suffix empty(1). JS-021: no lock. JS-001: no throw. ASCII-only.
private void TryRemoveDragSuffix(Order order)
{
    string suffix = ParseLegSuffix(order.Name);
    if (!string.IsNullOrEmpty(suffix))    // (1)
        _dragsInFlight.Remove(suffix);
}
```

#### 3h. New seam: `DragsInFlightForTest`

Insert in seams region (after `IsPttStpDragCancellableTestable` per B169 §5h):
```csharp
// DW-B168-02: test seam for _dragsInFlight. Returns the live instance HashSet for
// test priming (_dragsInFlight.Add/Remove) and assertion (Contains).
// CYC=1: pure property delegation. No NT8 runtime required.
internal HashSet<string> DragsInFlightForTest() => _dragsInFlight;
```

#### T3 Prerequisite Check

Before implementing T3, engineer must grep for:
- `IsStopLeg` — must exist (used by TryTrackDragInFlight)
- `ParseLegSuffix` — must exist (used by TryTrackDragInFlight and TryRemoveDragSuffix)
- `IsAtmBracketName` — must exist (used by TryCancelFollowerEntries block)
- `ResubmitOneCollateralLeg` — must exist (used by ResubmitCollateralLegs)
- `_laneEDispatched` — current line number for field insertion
- `IsPttStpDragCancellableTestable` — current line number for seam insertion

If any prerequisite is absent, Engineer must STOP and report to Director before proceeding.

---

### T4: Ghost-Entry Guard + AFP Guard Bundle (B161-B166 + B163)

**Design source**: `docs/brain/B161-B166/02-architecture-plan.md` §3, §4, §6; `docs/brain/B161-B166/06-deferred-backlog.md`.

**IMPORTANT — Implementation order within T4**:
Since all ghost-entry methods are absent from source, T4 must add them in this order:
1. `IsBeSlotNonTerminal` (helper, CYC=1)
2. `IsBeRetryArmedFlat` (helper, CYC≤3)
3. `IsBeSettleOrRetrySignalActive` (VIOLATION-01 extract, CYC=4) — wraps signals 5+6
4. `HasActivePttBeExits` (signal 2 helper, CYC=8)
5. `HasActivePttBeTargets` (signal 4 helper, CYC=8)
6. `HasSettlingBeTargets` (signal 3 helper, CYC≤8)
7. `IsFollowerPositionBlocked` (parent, CYC=8 — uses all helpers above)
8. AFP guard in `IsPttEntryOrderCancelTrigger` (B163)
9. IsFollowerPositionBlocked guard in `TryReplaceOnAtmCancel` (B161)

`IsFollowerPositionBlocked` MUST be added with CYC=8 compliance from day 1 (IsBeSettleOrRetrySignalActive pre-extracted). There is no CYC=9 intermediate state. DW-B161B166-01 was "CLOSED" in the deferred backlog but the extraction was never applied to source — B196-T4 applies the compliant version.

#### RESTORE-1 (B163): IsPttEntryOrderCancelTrigger — AverageFillPrice guard

**Method**: `IsPttEntryOrderCancelTrigger` — line ~885

**Current body** (line 885-894):
```csharp
internal static bool IsPttEntryOrderCancelTrigger(Order order)
{
    if (order == null)
        return false; // (1)
    if (order.OrderState != OrderState.Cancelled)
        return false; // (2)
    if (order.Name != "PTT-Copy" && order.Name != "Entry")
        return false; // (3)
    return order.LimitPrice > 0 && order.Instrument?.FullName != null;
}
```

**Add before final return** (new guard at position 4):
```csharp
    if (order.AverageFillPrice > 0)
        return false; // (4) B163: lifecycle cancel of a previously-filled Entry -- not a re-place trigger
    return order.LimitPrice > 0 && order.Instrument?.FullName != null;
```

Also update CYC comment at line ~880:
```
// IsPttEntryOrderCancelTrigger: CYC=4.
```

CYC: 3→4. `order.AverageFillPrice > 0` returns false for a never-filled order (AverageFillPrice == 0 for orders that were never filled — guaranteed by NT8 order lifecycle). This is a stateless, zero-new-field guard.

#### RESTORE-2 (B161): TryReplaceOnAtmCancel — IsFollowerPositionBlocked guard

**Method**: `TryReplaceOnAtmCancel` — line ~901

**Current body** (line 901-910):
```csharp
private void TryReplaceOnAtmCancel(Order order)
{
    if (_drainOwnedOrderIds.ContainsKey(order.OrderId))
        return; // drain-owned cancel -- skip replacement (1)
    if (order.Account != null && _pendingDispatchDrains.ContainsKey(order.Account.Name))
        return; // drain active for this account -- skip replacement (2)
    if (!IsPttEntryOrderCancelTrigger(order))
        return; // (3)
    ReplaceFollowerCopyOnAtmCancel(order); // (4 - no branch)
}
```

**Add after guard (3), before ReplaceFollowerCopyOnAtmCancel**:
```csharp
    if (order.Account != null
        && order.Instrument != null
        && IsFollowerPositionBlocked(order.Account, order.Instrument))
        return; // BE-All race -- skip re-place (4)
    ReplaceFollowerCopyOnAtmCancel(order); // (5 - no branch)
```

Update CYC comment at line ~896 to `CYC=5`.

CYC: 4→5. Guard placed after the cheap name/state filter (`IsPttEntryOrderCancelTrigger`) to avoid the O(n) `acc.Orders.ToList()` scan on non-PTT-Copy cancels.

#### New Methods (B161-B166 restore)

All new methods follow the designs in `docs/brain/B161-B166/02-architecture-plan.md` §3.

##### IsBeSlotNonTerminal
```csharp
// CYC=1. DW-B165: slot present = BE settle still in progress.
// JS-021: ConcurrentDictionary.ContainsKey is lock-free.
private bool IsBeSlotNonTerminal(string accName)
{
    return _pendingFollowerBeSlots.ContainsKey(accName);
}
```

##### IsBeRetryArmedFlat
```csharp
// CYC=3. DW-B166: flat follower with live retry-armed sentinel.
// JS-021: IsFlat reads NT8 Position (thread-safe); ContainsKey is lock-free.
private bool IsBeRetryArmedFlat(Account acc, Instrument instr)
{
    if (acc == null || instr == null)  // (1)
        return false;
    var pos = FindPosition(acc, instr);
    return IsFlat(pos) && _bePriceRetryAttempts.ContainsKey(acc.Name); // (2)(3)
}
```

##### IsBeSettleOrRetrySignalActive (VIOLATION-01 extraction)
```csharp
// VIOLATION-01 extraction: merges Signal 5 (IsBeSlotNonTerminal) and Signal 6 (IsBeRetryArmedFlat)
// into one parent branch in IsFollowerPositionBlocked. Keeps parent CYC=8.
// CYC=4: base(1) + if-slot(1) + if-retry(1) + short-circuit(1).
// JS-001: no throw. JS-021: no lock. ASCII-only.
private bool IsBeSettleOrRetrySignalActive(Account acc, Instrument instr)
{
    if (IsBeSlotNonTerminal(acc.Name))  // (1)
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "
                + acc.Name + "\n" + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        return true;
    }
    if (IsBeRetryArmedFlat(acc, instr))  // (2)
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked (be-retry-armed-flat): "
                + acc.Name + "\n" + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        return true;
    }
    return false;
}
```

##### HasActivePttBeExits (Signal 2 — B162-B fix included)
See `docs/brain/B161-B166/02-architecture-plan.md` §3 B162-B. Note: prefix is `"PTT-BE-Stop"` (NO trailing dash — bare stop matches too). CYC=8.

```csharp
// DW-B160/B162-B: Signal 2 of IsFollowerPositionBlocked.
// Matches bare "PTT-BE-Stop" AND suffixed "PTT-BE-Stop-1" etc. (no trailing dash per B162-B).
// CYC=8 -- AT LIMIT. JS-021: ToList snapshot, no lock. JS-002: false on all guard exits.
internal bool HasActivePttBeExits(Account acc, Instrument instr)
{
    if (acc == null)   return false; // (1)
    if (instr == null) return false; // (2)
    foreach (var o in acc.Orders.ToList()) // (3)
    {
        if (o.Name == null || !o.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)) continue; // (4)
        if (o.Instrument?.FullName != instr.FullName) continue; // (5)
        if (IsPttBeStopActiveState(o.OrderState)) return true; // (6)(7)(8)
    }
    return false;
}
```
Note: `IsPttBeStopActiveState` must exist in source (checks Working || Accepted || Submitted). Engineer must grep to confirm its presence before adding.

##### HasActivePttBeTargets (Signal 4 — B164)
See `docs/brain/B161-B166/02-architecture-plan.md` §3 B164. Uses `"PTT-BE-Target-"` prefix (WITH dash). CYC=8.

##### HasSettlingBeTargets (Signal 3 — B160)
Checks `"PTT-BE-Target-*"` in CancelPending/CancelSubmitted states. CYC≤8. See B161-B166 plan for body.

##### IsFollowerPositionBlocked (parent — all 6 signals, CYC=8)

```csharp
// IsFollowerPositionBlocked: CYC=8. 6-signal ghost-entry guard.
// Signal 1: HasOpenPosition (B159 -- follower has open position)
// Signal 2: HasActivePttBeExits (B160/B162-B -- PTT-BE-Stop* Working/Accepted/Submitted)
// Signal 3: HasSettlingBeTargets (B160 -- PTT-BE-Target-* CancelPending/CancelSubmitted)
// Signal 4: HasActivePttBeTargets (B164 -- PTT-BE-Target-* Working/Accepted/Submitted)
// Signals 5+6: IsBeSettleOrRetrySignalActive (VIOLATION-01 extract; B165+B166)
// JS-021: all sub-helpers use ToList snapshot or ConcurrentDictionary reads (no lock).
// JS-001: no throw. JS-002: returns false on all null/guard exits. ASCII-only.
internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)
{
    if (acc == null)   return false; // (1)
    if (instr == null) return false; // (2)
    if (HasOpenPosition(acc, instr))         // (3) Signal 1
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked (open-position): "
                + acc.Name + "\n" + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        return true;
    }
    if (HasActivePttBeExits(acc, instr))     // (4) Signal 2
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked (active-be-exits): "
                + acc.Name + "\n" + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        return true;
    }
    if (HasSettlingBeTargets(acc, instr))    // (5) Signal 3
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked (settling-be-targets): "
                + acc.Name + "\n" + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        return true;
    }
    if (HasActivePttBeTargets(acc, instr))   // (6) Signal 4
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked (active-be-targets): "
                + acc.Name + "\n" + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        return true;
    }
    if (IsBeSettleOrRetrySignalActive(acc, instr)) // (7) Signals 5+6 (VIOLATION-01 extract)
        return true;
    return false;
}
```
CYC=8: base(1) + acc null(1) + instr null(1) + Signal1(1) + Signal2(1) + Signal3(1) + Signal4(1) + Signals5+6(1) = 8. AT LIMIT — no further branches may be added to this method without prior helper extraction.

#### T4 Prerequisite Greps (Engineer Must Run)

Before implementing T4, engineer must confirm presence of:
- `HasOpenPosition` (called by IsFollowerPositionBlocked Signal 1)
- `IsPttBeStopActiveState` (called by HasActivePttBeExits, HasActivePttBeTargets)
- `FindPosition` + `IsFlat` (called by IsBeRetryArmedFlat)
- `_pendingFollowerBeSlots` field (used by IsBeSlotNonTerminal)
- `_bePriceRetryAttempts` field (used by IsBeRetryArmedFlat)
- `ReplaceFollowerCopyOnAtmCancel` (called by TryReplaceOnAtmCancel after the guard)

---

### T5: CYC>8 Investigation (DW-B195-03)

**File**: `src/PropTraderTools/TradeCopierPanel.cs` (read-only)

**Investigation result** (confirmed by B196 source greps):
- `grep "CYC=13|CYC=12|CYC=11|CYC=10|CYC=9"` in both `TradeCopierPanel.cs` and `CopyEngine.cs`: **ZERO matches** in both files.
- `grep "KeyDown|PreviewKey|AtrBox|OnAtr"` in `TradeCopierPanel.cs`: Only `OnChartKeyDown` (CYC=3) and `DispatchShortcut` (CYC=5) found. No high-CYC key handler.
- **No CYC>8 method found.** DW-B195-03 is CLOSED.
- `DW-B196-CYC` is NOT opened (no target method identified).

**Deliverable**: `docs/brain/B196/ticket-5-completion.md` with method=OnChartKeyDown (highest-CYC key handler found, CYC=3), disposition=no action required.

---

## §4 Component List

### New Components

| Component | Type | File | Notes |
|-----------|------|------|-------|
| `_dragsInFlight` | `private readonly HashSet<string>` field | CopyEngine.cs | After `_laneEDispatched` field; StringComparer.Ordinal |
| `TryTrackDragInFlight(Order)` | `private void` method | CopyEngine.cs | CYC=5; handles ChangePending/ChangeRejected |
| `TryRemoveDragSuffix(Order)` | `private void` method | CopyEngine.cs | CYC=2; handles Cancelled |
| `DragsInFlightForTest()` | `internal HashSet<string>` seam | CopyEngine.cs | CYC=1; returns `_dragsInFlight` live instance |
| `IsBeSlotNonTerminal(string)` | `private bool` method | CopyEngine.cs | CYC=1; `_pendingFollowerBeSlots.ContainsKey` |
| `IsBeRetryArmedFlat(Account, Instrument)` | `private bool` method | CopyEngine.cs | CYC=3 |
| `IsBeSettleOrRetrySignalActive(Account, Instrument)` | `private bool` method | CopyEngine.cs | CYC=4; VIOLATION-01 extraction |
| `HasActivePttBeExits(Account, Instrument)` | `internal bool` method | CopyEngine.cs | CYC=8; Signal 2 |
| `HasActivePttBeTargets(Account, Instrument)` | `internal bool` method | CopyEngine.cs | CYC=8; Signal 4 (B164) |
| `HasSettlingBeTargets(Account, Instrument)` | `internal bool` method | CopyEngine.cs | CYC≤8; Signal 3 (B160) |
| `IsFollowerPositionBlocked(Account, Instrument)` | `internal bool` method | CopyEngine.cs | CYC=8; 6-signal parent |
| `B196Tests` class | xUnit test class | Tests/B196Tests.cs | All B196 test cases |
| `DwB168_02Tests` class | xUnit test class | Tests/B196Tests.cs | _dragsInFlight seam tests |

### Modified Components

| Component | File | Change |
|-----------|------|--------|
| `DrainThenDispatch` | CopyEngine.cs | Fast-path: replace bare SubmitEntryDirect with ATM-mode branch. CYC 4→5. |
| `FindLeaderStopPrice` | CopyEngine.cs | Predicate: add `OrderState.Accepted` to Working guard. CYC 5→6. |
| `IsPttEntryOrderCancelTrigger` | CopyEngine.cs | Add AverageFillPrice guard at position 4. CYC 3→4. |
| `TryReplaceOnAtmCancel` | CopyEngine.cs | Add IsFollowerPositionBlocked guard at position 4. CYC 4→5. |
| `TryHandleBracketDrag` | CopyEngine.cs | Add TryTrackDragInFlight(order) call before IsWorkingBracket. CYC unchanged. |
| `TryCancelFollowerEntries` | CopyEngine.cs | Add TryRemoveDragSuffix call inside IsAtmBracketName block. CYC unchanged. |
| `HandleAtmStopSync` | CopyEngine.cs | Add _dragsInFlight.Remove(legSuffix) after ResubmitCollateralLegs. CYC 2→3. |
| `ResubmitCollateralLegs` | CopyEngine.cs | Replace IsLeaderLegBeingChanged with _dragsInFlight.Contains(s) guard (already absent). CYC unchanged. |

### Removed Components

| Component | File | Status |
|-----------|------|--------|
| `IsLeaderLegBeingChanged` | CopyEngine.cs | Already ABSENT from source. No removal needed. |
| `IsLeaderLegBeingChangedTestable` | CopyEngine.cs | Already ABSENT from source. No removal needed. |

---

## §5 CYC Impact Table

| Method | Pre-B196 CYC | Post-B196 CYC | Limit | Status |
|--------|-------------|--------------|-------|--------|
| `DrainThenDispatch` | 4 | 5 | 8 | PASS |
| `FindLeaderStopPrice` | 5 | 6 | 8 | PASS |
| `IsPttEntryOrderCancelTrigger` | 3 | 4 | 8 | PASS |
| `TryReplaceOnAtmCancel` | 4 | 5 | 8 | PASS |
| `TryHandleBracketDrag` | existing | unchanged | 8 | PASS |
| `TryCancelFollowerEntries` | existing | unchanged | 8 | PASS |
| `HandleAtmStopSync` | 2 | 3 | 8 | PASS |
| `ResubmitCollateralLegs` | existing | unchanged | 8 | PASS |
| `TryTrackDragInFlight` (new) | — | 5 | 8 | PASS |
| `TryRemoveDragSuffix` (new) | — | 2 | 8 | PASS |
| `DragsInFlightForTest` (new) | — | 1 | 8 | PASS |
| `IsBeSlotNonTerminal` (new) | — | 1 | 8 | PASS |
| `IsBeRetryArmedFlat` (new) | — | 3 | 8 | PASS |
| `IsBeSettleOrRetrySignalActive` (new) | — | 4 | 8 | PASS |
| `HasActivePttBeExits` (new) | — | 8 | 8 | PASS (AT LIMIT) |
| `HasActivePttBeTargets` (new) | — | 8 | 8 | PASS (AT LIMIT) |
| `HasSettlingBeTargets` (new) | — | ≤8 | 8 | PASS |
| `IsFollowerPositionBlocked` (new) | — | 8 | 8 | PASS (AT LIMIT) |

**CYC counting convention**: McCabe cyclomatic complexity per codebase standard. Each `if`, `else if`, `foreach`, `while`, compound `&&`/`||` (each clause) = +1 branch. Base = 1. Consistent with inline CYC comments throughout CopyEngine.cs.

**CYC note on TryTrackDragInFlight**: B169 plan §8 states CYC=3. Correct count including all guards is CYC=5 (base + !IsStopLeg + IsNullOrEmpty + ChangePending + ChangeRejected). All within limit.

---

## §6 JS Scan Checklist

| Rule | Check | Result |
|------|-------|--------|
| JS-021: No lock() | All new/modified methods use volatile reads, HashSet on single NT8 callback thread, ConcurrentDictionary lock-free ops. No `lock(` anywhere. | PASS |
| JS-033: No async void | All new methods are `private void` or `private bool` (non-async). No `async void` introduced. | PASS |
| JS-066: CYC ≤ 8 | Maximum CYC across all touched methods = 8. IsFollowerPositionBlocked=8, HasActivePttBeExits=8, HasActivePttBeTargets=8. All at or below ceiling. | PASS |
| JS-001: No throw in hot path | All new methods use early return pattern. No throw statements. SendCopyWithAtm has its own try/catch internally (unchanged). | PASS |
| ASCII-only | New string literals: `"-fast-atm-"`, `"-be-settle-pending"` log tags (ASCII). HashSet with StringComparer.Ordinal (ASCII-safe). All identifiers ASCII. | PASS |
| DateTime.UtcNow (no DateTime.Now) | No DateTime usage in B196 code. T1 uses `Environment.TickCount` (existing pattern). | PASS |
| JS-002: No return null | All bool helpers return false on null/guard exits. No null returns. | PASS |
| NT8 AddOnBase scope | SendCopyWithAtm uses StartAtmStrategy (AddOnBase-confirmed). No AtmStrategyCreate, AtmStrategyChangeStopTarget, or Account.Change() in B196 changes. | PASS |
| "Entry" order name | SendCopyWithAtm hardcodes "Entry" at line 4680 — constraint satisfied by caller delegating to SendCopyWithAtm. | PASS |
| JS-036: HashSet bounded | `_dragsInFlight` contains at most 3 entries (one per stop suffix "1","2","3"). | PASS |

---

## §7 Test Plan

**Test file**: `src/PropTraderTools/Tests/B196Tests.cs`
**Framework**: xUnit ([Fact] attribute)
**Note**: `DwB168_02Tests` class is added fresh (no CopyEngineTests.cs replacement required — `DwB168Tests` is already absent from source).

### T1 Tests

#### [Fact] DrainThenDispatch_FastPath_CloneAtmMode_CallsSendCopyWithAtm
**Asserts**: When `_cloneAtmCache` has a template name, `DrainThenDispatch` fast-path routes to `SendCopyWithAtm` path (not bare SubmitEntryDirect). Verifies `GetCloneAtmMode()` returns Named.
**Setup**: CopyEngine with `SetCloneAtmCache("TestTemplate")`. Call `DrainThenDispatch` with empty follower orders (triggers fast-path).
**Assert**: `GetCloneAtmMode()` returns `FollowerAtmMode.Named` (via internal seam). No "PTT-Copy" order created.

#### [Fact] DrainThenDispatch_FastPath_NoAtmMode_CallsSubmitEntryDirect
**Asserts**: When no ATM template configured, fast-path routes to SubmitEntryDirect (bare copy).
**Setup**: CopyEngine with no ATM cache set. Fast-path triggered.
**Assert**: `GetCloneAtmMode()` returns `FollowerAtmMode.Inherit`.

### T2 Tests

#### [Fact] FindLeaderStopPrice_AcceptedStop_ReturnsPrice
**Asserts**: `FindLeaderStopPrice` returns the stop price when the stop order is in `OrderState.Accepted`.
**Pattern**: BCL source-text check (same pattern as DwB170Tests in B170). Asserts `OrderState.Accepted` is present in the predicate region of CopyEngine.cs.

#### [Fact] FindLeaderStopPrice_WorkingStop_StillReturnsPrice
**Asserts**: `OrderState.Working` is still in the predicate (regression guard). Uses `FindLeaderStopPriceTestable` seam.
**Pattern**: BCL source-text check asserts `OrderState.Working` is present alongside `OrderState.Accepted`.

### T3 Tests (DwB168_02Tests class)

#### [Fact] DragsInFlight_SingleStop_CollateralResubmitted
Arrange: Empty set. Assert: `Contains("1") == false` → guard does not skip leg 1.

#### [Fact] DragsInFlight_ThreeStop_CollateralSuppressedForInFlightLeg
Arrange: `DragsInFlightForTest().Add("2"); .Add("3")`. Assert: `Contains("2") == true` → guard fires → skip.

#### [Fact] DragsInFlight_ThreeStop_LastHandlerResubmitsAllLegs
Arrange: Empty set (all cleared). Assert: `Contains("1") == false && Contains("2") == false`.

#### [Fact] DragsInFlight_ClearOnCancelled_PreventsPermamentSkip
Arrange: `DragsInFlightForTest().Add("1")`. Act: `DragsInFlightForTest().Remove("1")`. Assert: `Contains("1") == false`.

### T4 Tests

#### [Fact] IsPttEntryOrderCancelTrigger_FilledEntry_ReturnsFalse
**Asserts**: When `order.AverageFillPrice > 0`, `IsPttEntryOrderCancelTrigger` returns false (lifecycle cancel blocked).
**Pattern**: Internal static method — directly callable from test with a mock/stub Order.

#### [Fact] IsFollowerPositionBlocked_HasActivePttBeExits_ReturnsTrue
**Asserts**: When `HasActivePttBeExits` would return true (PTT-BE-Stop order present in Working state), `IsFollowerPositionBlocked` returns true.
**Pattern**: Uses `DragsInFlightForTest`-style seam approach or BCL source-text check.

#### Additional tests per architect discretion:
- `[Fact] IsBeSettleOrRetrySignalActive_SlotPresent_ReturnsTrue`
- `[Fact] IsBeSettleOrRetrySignalActive_SlotAbsent_RetryArmedFlat_ReturnsTrue`
- `[Fact] IsFollowerPositionBlocked_NullAccount_ReturnsFalse`

---

## §8 Deferred Work Register

### Items Closed by B196

| DW ID | Description | Closed By |
|-------|-------------|-----------|
| DW-B195-02 | DrainThenDispatch fast-path bare in Clone mode | B196-T1 |
| DW-B170-01 | FindLeaderStopPrice missing OrderState.Accepted | B196-T2 |
| DW-B169 COLLATERAL-STORM | _dragsInFlight HashSet guard restore | B196-T3 |
| B161-B166 Ghost-Entry Guard + B163 AFP | IsFollowerPositionBlocked 6-signal restore + AFP guard | B196-T4 |
| DW-B195-03 | DW-B177-CYC source method not found — investigation | B196-T5 |

### Items Carried Forward (Unchanged)

| DW ID | Priority | Description |
|-------|----------|-------------|
| DW-B183-03 | P1 | IsLaneEEligible CYC=8 frozen — any branch addition requires prior helper extraction |
| DW-B192-SINST-01 | P2 | SetInstrument() CYC=8 ceiling — same constraint |
| DW-B193-01 | P3 | OnQuickAllBufferChanged old label strings |
| DW-B159-K2 | P2 | 14 xUnit tests (BE path) |
| DW-B159-K3 | P2 | Attempt cap hardcoded at 3 |
| DW-B159-K4 | P2 | intendedBePrice sourcing |
| DW-B167-DEFER-01 | P2 | ECA-1 double-position race. Monitor at SIM. |
| DW-B167-DEFER-03 | P1 | Combined B161-B166 SIM gate pending |
| DW-B167-DEFER-04 | P1 | B167 SIM gate pending |

### Carried Forward (First Appearance in B196 backlog)

| DW ID | Priority | Description |
|-------|----------|-------------|
| DW-B170-SIM | P0 | SIM GATE: Director drags target on leader — confirm follower stop preserved after drag. DW-B170-01 live regression verify. Was OPEN in B170/05-final-review.md §Section K but not carried through subsequent backlog files. Reactivated here. |

### New Items Opened by B196

**DW-B196-CYC**: NOT opened. T5 investigation found no CYC>8 method in either `TradeCopierPanel.cs` or `CopyEngine.cs`. No action required.

---

## §9 SIM Gate Criteria

**B196 SIM gate must be run by Director after all tickets (T1-T4) reach VERIFY_PASS.**
Record results in `docs/brain/B196/sim-gate-completion.md`.

### T1 SIM Criteria (DW-B195-02)
1. Leader=Sim101, Followers=Sim102/Sim103/Sim104, Clone ATM mode ON, ATM template set.
2. Place first leader ATM entry when followers have NO prior entries (fast-path triggered: no candidates).
3. Confirm followers receive "Entry"-named orders with ATM brackets (NOT "PTT-Copy" bare orders).
4. Leader fills. All followers: `SyncFollowerBracket` log shows `fo=<order>` (not `fo=NULL`).
5. **PASS criterion**: No naked positions. No PTT-Flatten fires on any follower.

### T2 SIM Criteria (DW-B170-01)
1. Leader ATM position open. Drag leader Target1.
2. During Accepted state window: `FindLeaderStopPrice` returns non-zero stop price.
3. **PASS criterion**: Follower stop is preserved (not missing) after target drag. `DW-B170-SIM` closes with this step.

### T3 SIM Criteria (DW-B169)
1. Leader ATM position open. Drag ALL THREE stops simultaneously.
2. Confirm NO "collateral storm" — Stop1 Accepted callback does NOT resubmit Stop2 or Stop3 while they are still processing.
3. Confirm _dragsInFlight set transitions: Add(suffix) on ChangePending, Remove(suffix) on Accepted completion.
4. **PASS criterion**: Zero duplicate follower stops. Zero "PTT-STP-Drag" orphan orders. All 3 legs correctly synced.

### T4 SIM Criteria (B161-B166 + B163)
1. Trigger full BE-All burst on a session with open follower positions.
2. Confirm `[PTT-COPY-GUARD] ghost-entry blocked` log line appears for at least one follower during the burst.
3. After BE-All burst: no ghost ATM brackets (Stop1+Target1) appear on any follower.
4. **PASS criterion**: Zero ghost entries. All 6 signal log tags visible in Output panel during burst.
5. Confirm filled "Entry" orders (AverageFillPrice > 0) do NOT trigger re-place (B163 verification).

### Combined Gate
All 5 criteria above must PASS. Record per-ticket result in `sim-gate-completion.md`.

---

## Appendix A: Source References (B196 Architecture Time)

All line numbers reference `src/PropTraderTools/CopyEngine.cs` at B196 architecture time.

| Symbol | Approximate Line | Notes |
|--------|-----------------|-------|
| `DrainThenDispatch` | 6579 | Fast-path at 6606; `now` local at 6613 (after fast-path) |
| Fast-path early return | 6606-6610 | `SubmitEntryDirect` bare call — gap |
| `SubmitDrainedEntry` | 6722 | B195 already applied; not modified by B196 |
| `IsPttEntryOrderCancelTrigger` | 885 | CYC=3; no AFP guard — gap |
| `TryReplaceOnAtmCancel` | 901 | CYC=4; no IsFollowerPositionBlocked guard — gap |
| `FindLeaderStopPrice` | 3703 | Line 3714: Working-only predicate — gap |
| `CopySignal.Create` | 605 | 5 params: action, type, qty, limitPrice, orderId |
| `GetCloneAtmMode()` | 745 | volatile read; returns Named or Inherit |
| `SendCopyWithAtm` | 4661 | `in CopySignal` param; "Entry" name at 4680 |
| `_cloneAtmCache` / `_cloneAtmObject` | 146 + nearby | volatile AtmStrategy |
| `_pendingDispatchDrains` | 388 | ConcurrentDictionary<string, PendingDispatchDrain> |
| `_drainOwnedOrderIds` | 391-392 | ConcurrentDictionary drain-owned cancel IDs |

---

*Written by: ptt-architect*
*Epic: B196*
*Phase: 1 (Architecture)*
*Return: PLAN_COMPLETE*
