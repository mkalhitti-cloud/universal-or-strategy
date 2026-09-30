# B61-LaneA Deferred Backlog

**Block**: B61-LaneA
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-10

---

## Section 1 -- B61 Block Summary

**Block B61-LaneA closed one P0 defect in commit `8a097ac8`**
(message: `fix(ptt): B61 -- TryDispatchLeaderFlat state guard + follower-only flatten [4 tests]`,
branch: main, 2026-08-10):

| Item | Description | Resolution |
|------|-------------|------------|
| DW-B61-01 | `TryDispatchLeaderFlat` fires on all OrderStates, calls leader-account `Flatten()` overload, has no CopyRule parameter | **CLOSED** -- method replaced with `private static bool TryDispatchLeaderFlat(Account, Instrument, OrderState, CopyRule, Func<Account,bool>, Func<Account,Instrument,bool>, Action<Account,Instrument>)`. State guard (Filled/Cancelled only), follower-only `foreach` loop via `rule.FollowerAccounts`, `Flatten(account, instrument)` removed. CYC=6. Call site at line 646 updated to pass `e.Order.OrderState` and `matchedRule.Value`. 4 xUnit tests (T_B61_01..T_B61_04). |

Confirmed closed by ptt-verifier independent scan (VERIFY_PASS) and ptt-plan-reviewer final review
(FINAL_PASS, FR-01..FR-14 all PASS).

**Deviation recorded**: Method implemented as `private static` rather than ticket-specified
`internal static` due to CS0051 (CopyRule is a `private readonly struct`). Behavioral contract
identical; testability preserved via reflection pattern.

---

## Section 2 -- New Deferred Items from B61

**No new deferred items from B61.**

No defects, violations, or architectural gaps were discovered during B61 execution that require
a future block. The `private static` vs `internal static` deviation is compiler-forced, not a
design gap. SCAN-07 non-executability is a pre-existing project constraint unchanged by B61.

---

## Section 3 -- Carry-Forward Items (unchanged from B60)

The following items were open in `docs/brain/B60-LaneA/06-deferred-backlog.md` and remain open
with no change in B61.

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
**Status**: OPEN -- blocked on future block. No change in B61.

**Note**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per `docs/standards/NT8_FULL_REFERENCE.md`.
The AddOnBase (`TradeCopierAddOn`) cannot call this API. A companion StrategyBase add-in would be
required. Deferred indefinitely pending Director architectural decision.

---

### PRE-EXISTING-01 -- Non-ASCII characters at CopyEngine.cs lines 395, 496

**Priority**: P2
**Status**: OPEN -- pre-existing, not introduced or modified by any B59, B60, or B61 change.

---

### PRE-EXISTING-02 -- Non-ASCII characters at CopyEngine.cs lines 1256, 1257

**Priority**: P2
**Status**: OPEN -- pre-existing, not introduced or modified by any B59, B60, or B61 change.

---

### PRE-EXISTING-03 -- `deploy-sync.ps1` archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN -- pre-existing infrastructure state. `deploy-sync.ps1` is archived to
`archive/v12-reference/scripts/deploy-sync.ps1` and maps V12_002 strategy files, not
PropTraderTools AddOn files. Manual SHA-256 copy + `verify_links.ps1 -Fix` is the current
PropTraderTools deploy workflow. No change in B61.

---

## Summary Table

| ID | Description | Priority | Target | Status |
|----|-------------|----------|--------|--------|
| DW-B61-01 | TryDispatchLeaderFlat state guard + follower-only flatten | P0 | B61 | **CLOSED** (8a097ac8) |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | OPEN |
| DW-B54-01 | ATM auto-inject (StrategyBase required) | P1 | future | OPEN (blocked) |
| PRE-EXISTING-01 | Non-ASCII CopyEngine.cs:395, 496 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII CopyEngine.cs:1256, 1257 | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived | P2 | future | OPEN |
