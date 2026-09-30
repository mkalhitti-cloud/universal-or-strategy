# B63-LaneA Deferred Backlog

**Block**: B63-LaneA
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-11

---

## Closed Items This Block

### DW-B63-01 — Gate B bracket state gap (ATM Target1 leaks at Accepted state)

**Priority**: P0
**Status**: CLOSED (commit a70d60e4, 2026-08-11)
**Closed by**: B63-LaneA Ticket 1 — `IsWorkingBracket` widened to include `OrderState.Accepted`

**Resolution summary**: `IsWorkingBracket` in `src/PropTraderTools/CopyEngine.cs` (lines 815–820)
was widened from `OrderState.Working`-only to `(OrderState.Working || OrderState.Accepted)`.
Both callsites (`OnOrderUpdate` line 651, `MirrorOrderUpdate` line 682) automatically benefit.
All 4 xUnit [Fact] tests pass (T_B63_01 through T_B63_04). SCAN-01 through SCAN-07 all zero
new violations. Build baseline unchanged (3 pre-existing errors, 0 new).

---

### DW-B63-02 — NT8 `Order` sealed type; xUnit stub strategy undetermined

**Priority**: P1
**Status**: CLOSED (2026-08-11)
**Closed by**: B63-LaneA Ticket 1 — engineer chose Option 1 (reflection-based property setter)

**Resolution summary**: `MakeOrder()` uses
`System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Order))` to
bypass the sealed NT8 `Order` constructor. `OrderState` and `Name` properties are set via
reflection. `InvokeIsWorkingBracket()` calls `CopyEngine.IsWorkingBracket(order)` directly
(same assembly, no `InternalsVisibleTo` attribute required). Each test wraps in
`try/catch (NullReferenceException)` as STUB_REQUIRED safeguard — consistent with existing
patterns in `CopyEngineTests.cs`. This approach successfully compiles and executes.

**Note**: `FormatterServices.GetUninitializedObject` is a test-only pattern. It is appropriate
here because NT8 `Order` is sealed and has no public constructor accessible from outside the
NT8 runtime. This pattern should be documented as the established project convention for
stubbing NT8 sealed types in future test files.

---

## Carry-Forward Items (from B59-LaneA/06-deferred-backlog.md)

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

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...` (non-atomic).
Currently safe — both callers (TradeCopierPanel, TradeCopierWindow) access exclusively from the
WPF UI thread. If a future block introduces a non-UI-thread caller, `Interlocked.CompareExchange`
will be required.

---

### DW-B58-03 — `RelayBe` does not forward `OcoGroup` from `BeEventArgs`

**Priority**: P2
**Target block**: future
**Status**: OPEN

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
`SubmitBeStop` generates its own `OcoId` via `NextQxOcoId()`. If a future block requires correlated
OcoId fan-out across accounts for a single BE event, a new `SubmitBeStop` overload accepting an
explicit `OcoGroup` will be needed.

---

### DW-B54-01 — ATM auto-inject

**Priority**: P1
**Target block**: future (blocked — requires StrategyBase-level NT8 API unavailable in AddOnBase)
**Status**: OPEN — blocked on future block. No change in B63.

**Note**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per NT8_FULL_REFERENCE.md. The
AddOnBase (`TradeCopierAddOn`) cannot call this API. A companion StrategyBase add-in would be
required. Deferred indefinitely pending Director architectural decision.

---

### PRE-EXISTING-01 — Non-ASCII characters at CopyEngine.cs lines 395, 496

**Priority**: P2
**Status**: OPEN — pre-existing, not introduced by any B63 change.

---

### PRE-EXISTING-02 — Non-ASCII characters at CopyEngine.cs lines 1256, 1257

**Priority**: P2
**Status**: OPEN — pre-existing, not introduced by any B63 change.

Note: Verifier Layer 3 SCAN-01 also observed pre-existing hits at lines 1289, 1290 (likely same
region as 1256/1257 after line-number shift from B59 and B63 insertions). Both covered under
PRE-EXISTING-02.

---

### PRE-EXISTING-03 — `deploy-sync.ps1` archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN — pre-existing infrastructure state. `deploy-sync.ps1` is archived to
`archive/v12-reference/scripts/deploy-sync.ps1` and maps V12_002 strategy files, not
PropTraderTools AddOn files. Manual SHA-256 copy + `verify_links.ps1 -Fix` is the current
PropTraderTools deploy workflow. No change in B63.

---

## New Deferred Items — B63

No new deferred items from B63.

DW-B63-01 (the bug fix) and DW-B63-02 (the test stub strategy) were both resolved within this
block. The `FormatterServices.GetUninitializedObject` pattern is now the established project
convention for NT8 sealed-type stubs; it requires no further deferred tracking.
