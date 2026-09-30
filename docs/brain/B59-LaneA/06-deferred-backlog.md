# B59-LaneA Deferred Backlog

**Block**: B59-LaneA
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-10
**Amended**: 2026-08-10 (Director session — DW-B60-01 added from live test observation)

---

## New Deferred Items — B60 (from live test 2026-08-10)

### DW-B60-01 — Leader manual close does not close follower position

**Priority**: P1
**Target block**: B60
**Status**: OPEN

**Description**: When the leader closes their position manually via the Positions tab Close button
(NT8 generates an order with Name=`"Close"`), Gate 0.5 (B59) now correctly blocks that order from
being forwarded as a phantom copy to followers. However, after the leader goes Flat, the follower
position remains open and must be closed manually.

**Root cause**: The copier has entry propagation (leader enters → follower enters) but no exit
propagation for manual close. Mirror mode has `MirrorClose` (fires on bracket leg fill), but
Signal/Clone mode has no position-close hook for manual leader flattening.

**Infrastructure already present** (no new patterns needed):
- `PositionStateChanged` event fires from `TryFirePositionState` in `OnOrderUpdate` on every
  Filled/PartFilled/Cancelled — already fires before Gate 1 (copy-enabled check). The `hasPos`
  field correctly tracks leader-flat transitions.
- `Flatten(Account leader, Instrument instrument)` at `CopyEngine.cs:1135` already fans out
  `PTT-Flatten` market orders to all follower accounts for an instrument.
- `IsFollowerAccount(Account)` at `CopyEngine.cs:400` exists to guard against recursion.

**Wire-up required**: Detect `leader hasPos → false` (leader went Flat for a matched rule
instrument) inside the copy-enabled, rule-matched path. Call `Flatten(leaderAccount, instrument)`
which will fan out to all follower accounts. Do NOT fire if copy is disabled or no matching rule
exists. Do NOT fire if the account triggering the flat is a follower (would recurse).

**Confirmed from live logs** (2026-08-10 7:21 PM test):
- Leader `Sim101` closed @ 7:21:06 — `PositionStateChanged hasPos=False` fired.
- Follower `Sim102` position (20L) remained open until manual `"Position grid close position"` at 7:21:24.
- 18-second gap is unacceptable for live trading: follower is unprotected for those 18 seconds.

---

## New Deferred Items — B59

### DW-B59-02 — `IsExitSignalName` uses exact `"Rev"` match instead of prefix

**Priority**: P1
**Target block**: B60
**Status**: OPEN

**Description**: The architecture plan specified `name.StartsWith("Rev", StringComparison.Ordinal)` to
block all NT8 reversal order names (e.g. "Reversal", "RevLong", "RevShort"). The as-built
implementation uses `name == "Rev"` (exact equality). Only an order literally named `"Rev"` is
blocked. Orders named "Reversal", "RevLong", or "RevShort" will pass through Gate 0.5 and may
be dispatched to followers as phantom copies.

**Action required**: Confirm actual NT8 reversal order names against `NT8_FULL_REFERENCE.md` and
live NT8 testing. If any reversal order name differs from the exact string `"Rev"`, widen the match
to `name.StartsWith("Rev", StringComparison.Ordinal)` and add corresponding test cases. Add
`"Reversal"`, `"RevLong"`, `"RevShort"` test cases.

---

## Carry-Forward Items — B58

### DW-B58-01 — `SnapshotTargetsPublic` hardcoded order-name prefixes

**Priority**: P2
**Target block**: future
**Status**: OPEN

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and `PTT-TGT-`.
Future blocks adding new PTT-prefixed target order names must update this method or the snapshot
will miss them.

### DW-B58-02 — `GlobalBe` non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...` (non-atomic).
Currently safe — both callers (TradeCopierPanel, TradeCopierWindow) access exclusively from the
WPF UI thread. If a future block introduces a non-UI-thread caller, `Interlocked.CompareExchange`
will be required.

### DW-B58-03 — `RelayBe` does not forward `OcoGroup` from `BeEventArgs`

**Priority**: P2
**Target block**: future
**Status**: OPEN

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
`SubmitBeStop` generates its own `OcoId` via `NextQxOcoId()`. If a future block requires correlated
OcoId fan-out across accounts for a single BE event, a new `SubmitBeStop` overload accepting an
explicit `OcoGroup` will be needed.

---

## Closed Items This Block

### DW-B59-01 — Gate 0.5 does not block NT8 built-in exit orders

**Priority**: P0
**Status**: **CLOSED** (commit fac65246, 2026-08-10)
**Closed by**: B59-LaneA Ticket-1 — `IsExitSignalName` helper + Gate 0.5 replacement

**Original description**: Gate 0.5 in `DispatchCopy` only blocked orders whose `Name` begins
with `"PTT-"`. NT8 built-in exit orders (Close button → `"Close"`, Flatten → `"Flatten"`,
reversal → `"Rev"`, exit signals → `"Exit..."` prefix) passed through Gate 0.5 and were
dispatched to followers as phantom copies.

**Resolution**: New `internal static bool IsExitSignalName(string name)` helper inserted at
`CopyEngine.cs:724`. Gate 0.5 replaced with `if (IsExitSignalName(order.Name)) return;` at
`CopyEngine.cs:745`. Covers: PTT- own signals, Close, Flatten, Rev (exact), Exit prefix.
7 xUnit `[Fact]` tests added (T_B59_01 through T_B59_07). CYC=6 (≤ 8 limit).

---

### DW-B57-01 — (prior open item)

**Priority**: P1
**Status**: **CLOSED** — confirmed working in live test 2026-08-10

---

## Long-Running Open Items

### DW-B54-01 — ATM auto-inject

**Priority**: P1
**Target block**: future (blocked — requires StrategyBase-level NT8 API unavailable in AddOnBase)
**Status**: OPEN — blocked on future block. No change in B59.

**Note**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per NT8_FULL_REFERENCE.md. The
AddOnBase (`TradeCopierAddOn`) cannot call this API. A companion StrategyBase add-in would be
required. Deferred indefinitely pending Director architectural decision.

---

## Pre-Existing Items (Unchanged)

### PRE-EXISTING-01 — Non-ASCII characters at CopyEngine.cs lines 395, 496

**Priority**: P2
**Status**: OPEN — pre-existing, not introduced by any B59 change.

### PRE-EXISTING-02 — Non-ASCII characters at CopyEngine.cs lines 1256, 1257

**Priority**: P2
**Status**: OPEN — pre-existing, not introduced by any B59 change.

### PRE-EXISTING-03 — `deploy-sync.ps1` archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN — pre-existing infrastructure state. `deploy-sync.ps1` is archived to
`archive/v12-reference/scripts/deploy-sync.ps1` and maps V12_002 strategy files, not
PropTraderTools AddOn files. Manual SHA-256 copy + `verify_links.ps1 -Fix` is the current
PropTraderTools deploy workflow. No change in B59.
