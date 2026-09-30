# B62-LaneA Deferred Backlog

**Block**: B62-LaneA
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-12

---

## New Deferred Items — B62

None. B62 review found no new violations, gaps, or out-of-scope risks that require a deferred item.

The stop-limit entry drag gap (plan Section 10 item 3 — `FindFollowerEntryOrder` matches
`OrderType.Limit` only, stop-limit entries not supported) is a documented, accepted out-of-scope
boundary. No DW item is opened: the feature is not in the spec baseline and the boundary is
intentional.

---

## Carry-Forward Items — B60/B59

### DW-B60-01 — Leader manual close does not close follower position

**Priority**: P1
**Target block**: B63 (or next available)
**Status**: OPEN

**Description**: When the leader closes their position manually via the Positions tab Close button
(NT8 order `Name="Close"`), Gate 0.5 (B59) correctly blocks that order from being forwarded as a
phantom copy to followers. However, after the leader goes Flat, the follower position remains open
and must be closed manually.

**Infrastructure already present**: `Flatten(Account leader, Instrument instrument)` at
`CopyEngine.cs:1135` already fans out `PTT-Flatten` market orders. `TryDispatchLeaderFlat` is
wired at line 651 in `OnOrderUpdate`. Verify current live status before opening new implementation
ticket — may already be partially addressed by B60 work.

**Confirmed from live logs** (2026-08-10 7:21 PM test): 18-second gap between leader flat and
follower manual close is unacceptable for live trading.

---

### DW-B59-02 — `IsExitSignalName` uses exact `"Rev"` match instead of prefix

**Priority**: P1
**Target block**: B63 (or next available)
**Status**: OPEN

**Description**: As-built `IsExitSignalName` uses `name == "Rev"` (exact equality). Orders named
`"Reversal"`, `"RevLong"`, or `"RevShort"` pass through Gate 0.5 and may be dispatched to
followers as phantom copies.

**Action required**: Confirm actual NT8 reversal order names against `NT8_FULL_REFERENCE.md` and
live NT8 testing. Widen to `name.StartsWith("Rev", StringComparison.Ordinal)` if NT8 uses longer
names. Add test cases for each variant (`"Reversal"`, `"RevLong"`, `"RevShort"`).

---

### DW-B58-01 — `SnapshotTargetsPublic` hardcoded order-name prefixes

**Priority**: P2
**Target block**: future
**Status**: OPEN

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and `PTT-TGT-`.
Future blocks adding new PTT-prefixed target order names must update this method or the snapshot
will miss them.

---

### DW-B58-02 — `GlobalBe` non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...`
(non-atomic). Currently safe — both callers (TradeCopierPanel, TradeCopierWindow) access
exclusively from the WPF UI thread. If a future block introduces a non-UI-thread caller,
`Interlocked.CompareExchange` will be required.

---

### DW-B58-03 — `RelayBe` does not forward `OcoGroup` from `BeEventArgs`

**Priority**: P2
**Target block**: future
**Status**: OPEN

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
`SubmitBeStop` generates its own `OcoId` via `NextQxOcoId()`. If a future block requires
correlated OcoId fan-out across accounts for a single BE event, a new `SubmitBeStop` overload
accepting an explicit `OcoGroup` will be needed.

---

### DW-B54-01 — ATM auto-inject (blocked)

**Priority**: P1
**Target block**: future (blocked — requires StrategyBase-level NT8 API unavailable in AddOnBase)
**Status**: OPEN — blocked on future block. No change in B62.

**Note**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per `NT8_FULL_REFERENCE.md`. The
`AddOnBase` (`TradeCopierAddOn`) cannot call this API. A companion `StrategyBase` add-in would be
required. Deferred indefinitely pending Director architectural decision.

---

### PRE-EXISTING-01 — Non-ASCII characters at CopyEngine.cs lines 398, 499

**Priority**: P2
**Status**: OPEN — pre-existing, not introduced by any B62 change.

**Note**: Line numbers shifted from B59 report (395/496) due to B59/B60/B62 insertions above
these lines. Same physical comment blocks (B56 BUILD-FIX stubs markers with em-dash Unicode).

---

### PRE-EXISTING-02 — Non-ASCII characters at CopyEngine.cs lines 1376, 1377

**Priority**: P2
**Status**: OPEN — pre-existing, not introduced by any B62 change.

**Note**: Line numbers shifted from B59 report (1256/1257) due to B59/B60/B62 insertions above
these lines. Same physical comment blocks (Unicode arrow characters in exit-order direction
comments).

---

### PRE-EXISTING-03 — `deploy-sync.ps1` archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN — pre-existing infrastructure state. `deploy-sync.ps1` is archived to
`archive/v12-reference/scripts/deploy-sync.ps1` and maps V12_002 strategy files, not
PropTraderTools AddOn files. Manual SHA-256 copy + `verify_links.ps1 -Fix` is the current
PropTraderTools deploy workflow. No change in B62.

---

## Closed Items This Block

### DW-B62-01 — Live entry drag sync + price-keyed dedup fix

**Priority**: P0 (spec requirement)
**Status**: CLOSED — commit `7cc079a6` (2026-08-12)
**Closed by**: B62-LaneA Ticket-1

**Resolution**: `_dedupCache` changed from `ConcurrentDictionary<string, long>` (timestamp) to
`ConcurrentDictionary<string, double>` (last dispatched `LimitPrice`). `IsDedup` body replaced with
price-keyed TryAdd-only logic (CYC=2). `EvictDedup` added for terminal-state eviction. Gate C
inserted in `OnOrderUpdate` to detect entry price changes. `HandleEntryChange` added to propagate
detected drags to follower `PTT-Copy` orders via `acc.Change()`. `FindFollowerEntryOrder` added as
helper. 5 xUnit `[Fact]` tests added (T_B62_01 through T_B62_05). All 7 changes present in
committed source. Verifier VERIFY_PASS.
