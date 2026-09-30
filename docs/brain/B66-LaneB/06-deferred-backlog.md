# B66-LaneB Deferred Backlog

**Block**: B66-LaneB
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-13

---

## Closed This Block

### DW-B66-BE-01 — SubmitBeStop isLong direction race

**Priority**: P0 (live trading correctness)
**Status**: CLOSED — B66-LaneB Ticket-1
**Commit**: 78b55d8d

**Resolution**: `SubmitBeStop` (`CopyEngine.cs`) changed from 3-arg to 4-arg signature:

```csharp
// BEFORE (3-arg — races with NT8 position state)
internal void SubmitBeStop(Account acc, NinjaTrader.Cbi.Instrument instr, double bePrice)
{
    ...
    bool isLong = pos.MarketPosition == MarketPosition.Long;  // <-- race read
    ...
}

// AFTER (4-arg — direction passed at call-site snapshot time)
internal void SubmitBeStop(Account acc, NinjaTrader.Cbi.Instrument instr, double bePrice, bool isLong)
{
    ...
    OrderAction dir = isLong ? OrderAction.Sell : OrderAction.BuyToCover;  // uses parameter
    ...
}
```

Three call sites updated:
- `CopyEngine.RelayBe` (line 351): passes `e.IsLong` from `BeEventArgs`
- `CopyEngine.ArmAllPendingBe` (line 521): passes local `bool isLong` computed at line 516
- `PttGlobalBreakEven` production ctor lambda (line 35): `(acc, instr, price, lng) => SubmitBeStop(acc, instr, price, lng)`

`PttGlobalBreakEven._submitBeStop` delegate type updated from `Action<Account, Instrument, double>`
to `Action<Account, Instrument, double, bool>`. All 4 change sites in PttGlobalBreakEven.cs
(field, production ctor, test ctor, ExecuteOne call site) updated consistently.

**Root cause**: NT8_FULL_REFERENCE.md line 1721:
> "Changes to positions will not be reflected till at least the next OnBarUpdate() event after an order fill."

A fill between the `ArmAllPendingBe` position read and the `SubmitBeStop` body position read
caused `pos.MarketPosition` to return `Flat`, making `isLong=false` on a Long position. This
submitted a `BuyToCover` stop below market price, which NT8 correctly rejected.

**B65 precedent**: Identical race fixed in `TryDispatchLeaderFlat` by passing order direction
at call-site read time (CopyEngine.cs lines 651-654).

**Tests**: 5 xUnit [Fact] tests in `src/PropTraderTools/Tests/B66Tests.cs` (T_B66_BE_01..05):
- T_B66_BE_01: Long → Sell direction
- T_B66_BE_02: Short → BuyToCover direction
- T_B66_BE_03: Null account guard intact
- T_B66_BE_04: 4-arg delegate constructor compiles; Execute with empty accounts makes no calls
- T_B66_BE_05: BeEventArgs.IsLong stores and returns the correct value

**Scans**: All 7 scans PASS (Layer 2 + Layer 3 match). CYC=7 for SubmitBeStop (<= 8). Zero
lock(), throw new, return null in modified methods.

---

## New Deferred Items — B66-LaneB

None. B66-LaneB introduces no new deferred items beyond what B66-LaneA already opened
(DW-B66-BE-01-LANA, below).

---

## Carry-Forward Items From B66-LaneA (via B65-LaneA)

### DW-B66-BE-01-LANA — CancelQxBrackets now cancels PTT-BE-Stop during Quick Exit

**Priority**: P1
**Target block**: B67+ (Director confirmation required)
**Status**: OPEN — opened by B66-LaneA (05-final-review.md Section K)

**Description**: B66-LaneA's `IsQxCancelCandidate` helper (branch 4,
`StartsWith("PTT-BE-", StringComparison.Ordinal)`) means Quick Exit now cancels any live
`PTT-BE-Stop` orders on the account/instrument. This ensures a clean position exit but removes
breakeven stop protection at Quick Exit time.

**Action required**: Director must confirm that cancelling PTT-BE-* orders on Quick Exit is the
intended behavior. If NOT intended, branch (4) should be removed from `IsQxCancelCandidate`.

**Note on naming**: This item appears as `DW-B66-BE-01` in B66-LaneA's backlog. To avoid
confusion with B66-LaneB's `DW-B66-BE-01` (which is CLOSED), this carry-forward uses the
identifier `DW-B66-BE-01-LANA`.

---

### DW-B64-01 — B62 drag sync not working (HandleEntryChange not firing)

**Priority**: P0
**Target block**: B67+ (next available)
**Status**: OPEN — no change in B66-LaneB.

**Description**: From Director live testing after B62 deployment: `HandleEntryChange` is not
firing when a stop-limit entry is dragged on the leader account. The B62 implementation added
Gate C in `OnOrderUpdate` to detect entry price changes and call `HandleEntryChange` to propagate
drags to follower `PTT-Copy` orders. The mechanism is present in source but not activating in
live testing.

**Investigation starting point**: Verify Gate C conditions in `OnOrderUpdate` — check whether the
price-change detection condition (`limitPrice != storedPrice`) is being evaluated correctly for
the order type being dragged. Verify `_dedupCache` has an entry for the order being dragged
(otherwise Gate C short-circuits). Check `CopyEngineTests.cs` T_B62_04 for the expected price
comparison logic.

---

### DW-B63-01 — Spurious PTT-Copy bracket orders on Sim102 after ATM fill

**Priority**: P1
**Target block**: B67+ (next available)
**Status**: OPEN — no change in B66-LaneB.

**Description**: After an ATM fill on the leader account, spurious PTT-Copy bracket orders appear
on the follower Sim102 account. These orders are not part of the intended copy cascade.

**Investigation starting point**: Review `DispatchCopy` Gate 0.5 (`IsExitSignalName` check) and
Gate A (`IsFollowerAccount` check) for the bracket order dispatch path. Verify `IsWorkingBracket`
(B63 T1) is correctly widened to `Accepted` state so bracket orders are detected before they
transition to Working. Check the `_dedupCache` for double-dispatch via ConcurrentDictionary
TryAdd semantics vs. the prior timestamp dedup.

---

### DW-B58-01 — SnapshotTargetsPublic hardcoded order-name prefixes

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B66-LaneB.

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and `PTT-TGT-`.
Future blocks adding new PTT-prefixed target order names must update this method or the snapshot
will miss them.

---

### DW-B58-02 — GlobalBe non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B66-LaneB.

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...`
(non-atomic). Currently safe — both callers (TradeCopierPanel, TradeCopierWindow) access
exclusively from the WPF UI thread. If a future block introduces a non-UI-thread caller,
`Interlocked.CompareExchange` will be required.

---

### DW-B58-03 — RelayBe does not forward OcoGroup from BeEventArgs

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B66-LaneB.

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
`SubmitBeStop` generates its own `OcoId` via `NextQxOcoId()`. If a future block requires
correlated OcoId fan-out across accounts for a single BE event, a new `SubmitBeStop` overload
accepting an explicit `OcoGroup` will be needed.

---

### DW-B54-01 — ATM auto-inject (blocked)

**Priority**: P1
**Target block**: future (blocked — requires StrategyBase-level NT8 API unavailable in AddOnBase)
**Status**: OPEN — blocked. No change in B66-LaneB.

**Description**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per
`NT8_FULL_REFERENCE.md`. The `AddOnBase` (`TradeCopierAddOn`) cannot call this API. A companion
`StrategyBase` add-in would be required. Deferred indefinitely pending Director architectural
decision.

---

### PRE-EXISTING-01 — Non-ASCII characters at CopyEngine.cs lines ~398, ~499

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B66-LaneB.

**Description**: Em-dash Unicode characters in B56 BUILD-FIX stub markers (comment lines only).
B66-LaneB inserts code in the 473-524 region; lines 398/499 are unaffected.

---

### PRE-EXISTING-02 — Non-ASCII characters at CopyEngine.cs lines ~1418-1419 (estimate)

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B66-LaneB.

**Description**: Unicode arrow characters in exit-order direction comments. Line number estimate:
- B65 baseline: lines 1401-1402
- B66-LaneA inserted ~21 lines at 423-441: new estimate ~1422-1423
- B66-LaneB net delta ~+3 lines at 473-524: new estimate ~1425-1426

**Note**: Exact line numbers should be re-confirmed by the next block touching CopyEngine.cs.
The physical comment blocks are unchanged; no new non-ASCII introduced by either B66 lane.

---

### PRE-EXISTING-03 — deploy-sync.ps1 archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN — pre-existing infrastructure state. No change in B66-LaneB.

**Description**: `deploy-sync.ps1` is archived to `archive/v12-reference/scripts/deploy-sync.ps1`
and maps V12_002 strategy files, not PropTraderTools AddOn files. Manual SHA-256 copy +
`verify_links.ps1 -Fix` is the current PropTraderTools deploy workflow.

---

## Summary Table

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B66-BE-01 | SubmitBeStop direction race (isLong parameter fix) | P0 | B66-LaneB | **CLOSED** |
| DW-B66-01 | CancelQxBrackets missed ATM bracket names (B66-LaneA) | P0 | B66-LaneA | **CLOSED** |
| DW-B64-01 | B62 drag sync — HandleEntryChange not firing | P0 | B67+ | OPEN |
| DW-B66-BE-01-LANA | CancelQxBrackets cancels PTT-BE-Stop on Quick Exit — Director confirm | P1 | B67+ | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B67+ | OPEN |
| DW-B54-01 | ATM auto-inject (blocked — StrategyBase required) | P1 | future (blocked) | OPEN |
| DW-B58-01 | SnapshotTargetsPublic hardcoded order-name prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines ~398, ~499 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines ~1425-1426 (estimate) | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | future | OPEN |

**Closed this block**: 1 (DW-B66-BE-01)
**Confirmed closed by parallel lane B66-LaneA**: 1 (DW-B66-01)
**Carry-forward OPEN**: 10 items (1xP0 + 3xP1 + 1xP1-blocked + 5xP2)
