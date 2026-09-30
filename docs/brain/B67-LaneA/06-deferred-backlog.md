# B67-LaneA Deferred Backlog

**Block**: B67-LaneA
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-13

---

## Closed This Block

### DW-B67-01 — FlattenOneAccount cancel brackets before market order

**Priority**: P0 (live trading correctness)
**Status**: CLOSED — B67-LaneA Ticket-1
**Commits**: `48ff50e3` (CopyEngine.cs edits + CopyEngineTests.cs 4 new [Fact] methods)
**Commit message**: `fix(ptt): B67-LaneA DW-B67-01 cancel brackets before flatten [4 tests]`

**Resolution**: `CancelQxBrackets(acc, instrument)` inserted in `FlattenOneAccount` after the
`if (pos == null || pos.Quantity == 0)` early-return guard block and before `acc.CreateOrder`
(market order). Comment block updated to document DW-B67-01, NT8 precedent, CYC=4 breakdown,
and JS-021/JS-001/JS-002 citations. Caller-list comment on `CancelQxBrackets` updated to
include `FlattenOneAccount` reference. CYC raised from 3 to 4 (project convention).
4 xUnit `[Fact]` tests added to `CopyEngineTests.cs` (T_B67_01..T_B67_04).

Root cause fixed: follower ATM/QX bracket orders (Stop1/Stop2/Target1/Target2/PTT-QX-*/PTT-BE-*)
are now cancelled before the market flatten order is submitted, preventing the Rithmic/Apex
"Close operation failed. Operation timed out." broker rejection.

---

## Advisory Note — B67-LaneB

**DW-B67-02** is an active deferred item being addressed in the parallel **B67-LaneB** lane.
It was never part of B67-LaneA scope and is not an open item in this backlog. See
`docs/brain/B67-LaneB/` brain artifacts for DW-B67-02 status and resolution.

---

## Open Items — Carry-Forward (unchanged from B66-LaneC)

### DW-B66-C-02 — DispatchCopy dedup key = 0.0 for all StopLimit entries

**Priority**: P1
**Target block**: B67+
**Status**: OPEN — no change in B67-LaneA.
**Location**: `src/PropTraderTools/CopyEngine.cs` line ~832-835

**Description**: `DispatchCopy` Gate 5 passes `order.LimitPrice` to `IsDedup` as the dedup key.
Since `StopLimit.LimitPrice == 0` always (NT8 confirmed), every StopLimit entry order on every
instrument shares dedup key `0.0`. The first StopLimit entry dispatch on any instrument succeeds;
any subsequent StopLimit entry dispatch (same or different instrument) is wrongly rejected as a
duplicate.

**Fix approach** (B67+):
```csharp
double dedupPrice = order.OrderType == OrderType.StopLimit ? order.StopPrice : order.LimitPrice;
if (IsDedup(order.OrderId.ToString(), dedupPrice))
    return;
```

**Defer rationale**: Scope creep risk. `DispatchCopy` Gate 5 and `IsDedup` intersect ALL copy
paths. Changing them risks regressions in tested Limit and Market paths.

---

### DW-B66-BE-01 — CancelQxBrackets cancels PTT-BE-Stop orders during Quick Exit

**Priority**: P1
**Target block**: B67+ (Director confirmation required)
**Status**: OPEN — no change in B67-LaneA.

**Description**: The widened predicate in `IsQxCancelCandidate` (branch 4,
`StartsWith("PTT-BE-", StringComparison.Ordinal)`) means that pressing Quick Exit cancels any
live `PTT-BE-Stop`, `PTT-BE-Stop-{i+1}`, or `PTT-BE-Target-{i+1}` orders. This ensures clean
position exit but removes breakeven stop protection at the moment of Quick Exit.

**Action required**: Director must confirm that cancelling PTT-BE-* orders on Quick Exit is
the intended behavior. If NOT intended, branch (4) should be removed from `IsQxCancelCandidate`.

**Note**: B67-LaneA broadened the call surface for `CancelQxBrackets` (now also called from
`FlattenOneAccount`). If DW-B66-BE-01 requires removing PTT-BE-* from cancellation, that removal
applies to both `PttQuickExit.Execute` and `FlattenOneAccount` call sites.

---

### DW-B63-01 — Spurious PTT-Copy bracket orders on Sim102 after ATM fill

**Priority**: P1
**Target block**: B67+ (next available)
**Status**: OPEN — no change in B67-LaneA.

**Description**: After an ATM fill on the leader account, spurious PTT-Copy bracket orders appear
on the follower Sim102 account. These orders are not part of the intended copy cascade.

**Investigation starting point**: Review `DispatchCopy` Gate 0.5 (`IsExitSignalName` check) and
Gate A (`IsFollowerAccount` check) for the bracket order dispatch path. Verify `IsWorkingBracket`
(B63 T1) is correctly widened to `Accepted` state so bracket orders are detected before they
transition to Working. Check the `_dedupCache` for double-dispatch.

---

### DW-B54-01 — ATM auto-inject (blocked)

**Priority**: P1
**Target block**: future (blocked — requires StrategyBase-level NT8 API unavailable in AddOnBase)
**Status**: OPEN — blocked. No change in B67-LaneA.

**Description**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per
`NT8_FULL_REFERENCE.md`. The `AddOnBase` (`TradeCopierAddOn`) cannot call this API. A companion
`StrategyBase` add-in would be required. Deferred indefinitely pending Director architectural
decision.

---

### DW-B58-01 — SnapshotTargetsPublic hardcoded order-name prefixes

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B67-LaneA.

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and `PTT-TGT-`.
Future blocks adding new PTT-prefixed target order names must update this method or the snapshot
will miss them.

---

### DW-B58-02 — GlobalBe non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B67-LaneA.

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...`
(non-atomic). Currently safe — both callers (TradeCopierPanel, TradeCopierWindow) access
exclusively from the WPF UI thread. If a future block introduces a non-UI-thread caller,
`Interlocked.CompareExchange` will be required.

---

### DW-B58-03 — RelayBe does not forward OcoGroup from BeEventArgs

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B67-LaneA.

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
`SubmitBeStop` generates its own `OcoId` via `NextQxOcoId()`. If a future block requires
correlated OcoId fan-out across accounts for a single BE event, a new `SubmitBeStop` overload
accepting an explicit `OcoGroup` will be needed.

---

### PRE-EXISTING-01 — Non-ASCII characters at CopyEngine.cs lines 398, 499

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B67-LaneA.

**Description**: Em-dash Unicode characters in B56 BUILD-FIX stub markers (comment lines only).
Lines 398 and 499 are outside the B67-LaneA modification regions (lines 443-450, 1467-1497)
and are not shifted by B67-LaneA changes.

---

### PRE-EXISTING-02 — Non-ASCII characters at CopyEngine.cs lines ~1476-1477

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B67-LaneA.

**Description**: Unicode arrow characters in exit-order direction comments. After B67-LaneA
additions (7-line comment block at FlattenOneAccount), the verifier VS3 confirmed these at
lines 404, 551, 1500, 1501 in the current file. The engineer reported them at 399, 527, 1476,
1477 at commit time (48ff50e3). The verifier's line numbers reflect the file state after
B67-LaneB commit 5c95e416. The PRE-EXISTING-02 line reference is updated to ~1476-1477 per
the most recent verifier reading. Re-confirm exact lines in the next block that touches
CopyEngine.cs below line 1000.

---

### PRE-EXISTING-03 — deploy-sync.ps1 archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN — pre-existing infrastructure state. No change in B67-LaneA.

**Description**: `deploy-sync.ps1` is archived to `archive/v12-reference/scripts/deploy-sync.ps1`
and maps V12_002 strategy files, not PropTraderTools AddOn files. Manual SHA-256 copy is the
current PropTraderTools deploy workflow.

---

## Summary Table

| ID | Item | Priority | Target | Status |
|----|------|----------|--------|--------|
| DW-B67-01 | FlattenOneAccount cancel brackets before market order | P0 | B67-LaneA | **CLOSED** |
| DW-B66-C-02 | DispatchCopy dedup key = 0.0 for all StopLimit entries (Gate 5 LimitPrice) | P1 | B67+ | OPEN |
| DW-B66-BE-01 | CancelQxBrackets cancels PTT-BE-Stop on Quick Exit — Director confirmation | P1 | B67+ | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B67+ | OPEN |
| DW-B54-01 | ATM auto-inject (blocked — StrategyBase required) | P1 | future | OPEN (blocked) |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines ~1476-1477 (updated per B67-LaneA verifier) | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | future | OPEN |

**Closed this block (B67-LaneA)**: 1 (DW-B67-01)
**Closed prior block (B66-LaneC)**: 1 (DW-B64-01 — confirmed in B66-LaneC/06-deferred-backlog.md)
**Advisory (parallel lane)**: DW-B67-02 — active in B67-LaneB (not B67-LaneA)
**Carry-forward OPEN**: 10 items (3×P1 + 1×P1-blocked + 3×P2 + 3×PRE-EXISTING)
