# B60-LaneA Deferred Backlog

**Block**: B60-LaneA
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-10

---

## Section 1 -- B60 Block Summary

**Block B60-LaneA closed two P1 defects in commit `57b10313`**
(message: `fix(ptt): B60 -- leader-close propagation + Rev prefix fix [3 tests]`, branch: main, 2026-08-10):

| Item | Description | Resolution |
|------|-------------|------------|
| DW-B60-01 | Leader manual close does not close follower position | **CLOSED** -- new `TryDispatchLeaderFlat(Account, Instrument)` helper wired in `OnOrderUpdate` at `CopyEngine.cs:646` (after Cancelled block, before Gate B). Calls `Flatten(account, instrument)` when `HasOpenPosition` returns false and account is not a follower. CYC=3. |
| DW-B59-02 | `IsExitSignalName` uses exact `name == "Rev"` match instead of prefix | **CLOSED** -- line 733 changed to `name.StartsWith("Rev", StringComparison.Ordinal)`. Three xUnit [Fact] tests added: T_B60_Rev_01 ("Reversal"), T_B60_Rev_02 ("RevLong"), T_B60_Rev_03 ("RevShort"). |

Both items confirmed closed by ptt-verifier independent scan (VERIFY_PASS) and ptt-plan-reviewer final review (FINAL_PASS).

---

## Section 2 -- New Deferred Items from B60

**No new deferred items from B60.**

No defects, violations, or architectural gaps were discovered during B60 execution that require
a future block. The two minor annotation differences noted by the verifier (CYC counting methodology
and verify_links FIXED count) are non-material -- neither represents a code defect or rule violation.

---

## Section 3 -- Carry-Forward Items (unchanged from B59)

The following items were open in `docs/brain/B59-LaneA/06-deferred-backlog.md` and remain open
with no change in B60.

### DW-B58-01 -- `SnapshotTargetsPublic` hardcoded order-name prefixes

**Priority**: P2
**Target block**: future
**Status**: OPEN

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and `PTT-TGT-`.
Future blocks adding new PTT-prefixed target order names must update this method or the snapshot
will miss them.

---

### DW-B58-02 -- `GlobalBe` non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...` (non-atomic).
Currently safe -- both callers (TradeCopierPanel, TradeCopierWindow) access exclusively from the
WPF UI thread. If a future block introduces a non-UI-thread caller, `Interlocked.CompareExchange`
will be required.

---

### DW-B58-03 -- `RelayBe` does not forward `OcoGroup` from `BeEventArgs`

**Priority**: P2
**Target block**: future
**Status**: OPEN

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
`SubmitBeStop` generates its own `OcoId` via `NextQxOcoId()`. If a future block requires correlated
OcoId fan-out across accounts for a single BE event, a new `SubmitBeStop` overload accepting an
explicit `OcoGroup` will be needed.

---

### DW-B54-01 -- ATM auto-inject

**Priority**: P1
**Target block**: future (blocked -- requires StrategyBase-level NT8 API unavailable in AddOnBase)
**Status**: OPEN -- blocked on future block. No change in B60.

**Note**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per `docs/standards/NT8_FULL_REFERENCE.md`.
The AddOnBase (`TradeCopierAddOn`) cannot call this API. A companion StrategyBase add-in would be
required. Deferred indefinitely pending Director architectural decision.

---

### PRE-EXISTING-01 -- Non-ASCII characters at CopyEngine.cs lines 395, 496

**Priority**: P2
**Status**: OPEN -- pre-existing, not introduced by any B59 or B60 change.

---

### PRE-EXISTING-02 -- Non-ASCII characters at CopyEngine.cs lines 1256, 1257

**Priority**: P2
**Status**: OPEN -- pre-existing, not introduced by any B59 or B60 change.

---

### PRE-EXISTING-03 -- `deploy-sync.ps1` archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN -- pre-existing infrastructure state. `deploy-sync.ps1` is archived to
`archive/v12-reference/scripts/deploy-sync.ps1` and maps V12_002 strategy files, not
PropTraderTools AddOn files. Manual SHA-256 copy + `verify_links.ps1 -Fix` is the current
PropTraderTools deploy workflow. No change in B60.

---

## Summary Table

| ID | Description | Priority | Target | Status |
|----|-------------|----------|--------|--------|
| DW-B60-01 | Leader manual close does not close follower | P1 | B60 | **CLOSED** (57b10313) |
| DW-B59-02 | IsExitSignalName exact Rev match too narrow | P1 | B60 | **CLOSED** (57b10313) |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | OPEN |
| DW-B54-01 | ATM auto-inject (StrategyBase required) | P1 | future | OPEN (blocked) |
| PRE-EXISTING-01 | Non-ASCII CopyEngine.cs:395, 496 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII CopyEngine.cs:1256, 1257 | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived | P2 | future | OPEN |
