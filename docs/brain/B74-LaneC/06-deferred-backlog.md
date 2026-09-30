# B74-LaneC Deferred Backlog

**Block**: B74-LaneC
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-17
**Prior block**: B66-LaneC

---

## Closed This Block

None. B74-LaneC is a retrospective hotfix block focused on 5 self-contained production fixes.
No previously-open DW items were addressed in this block.

---

## New Deferred Items — B74-LaneC

None. All 5 hotfixes (B74-C-01 through B74-C-05) are complete, self-contained fixes.
No new deferred work is introduced.

**Observation (not a new DW item)**: B74-C-04 adds `PTT-BE-Target-` recognition to
`PttGlobalQuickExit.SnapshotTargetOrders`. `CopyEngine.SnapshotTargetsPublic` (the DW-B58-01
scope method) was not modified and still lacks the `PTT-BE-Target-` prefix. This confirms
DW-B58-01 remains OPEN. It is not a regression introduced by B74-LaneC.

---

## Carry-Forward Items (OPEN, from B66-LaneC)

All 9 OPEN items from `docs/brain/B66-LaneC/06-deferred-backlog.md` carry forward unchanged.

### DW-B66-C-02 — DispatchCopy dedup key = 0.0 for all StopLimit entries

**Priority**: P1
**Target block**: B75+ (next available after B74-LaneC)
**Status**: OPEN — not addressed in B74-LaneC.
**Location**: `src/PropTraderTools/CopyEngine.cs` line ~832-835

**Description**: `DispatchCopy` Gate 5 passes `order.LimitPrice` to `IsDedup` as the
dedup key. Since `StopLimit.LimitPrice == 0` always (NT8 confirmed), every StopLimit entry
order on every instrument shares dedup key `0.0`. The first StopLimit entry dispatch on any
instrument succeeds; any subsequent StopLimit entry dispatch (same or different instrument)
is wrongly rejected as a duplicate.

**Fix approach** (B75+):
```csharp
double dedupPrice = order.OrderType == OrderType.StopLimit ? order.StopPrice : order.LimitPrice;
if (IsDedup(order.OrderId.ToString(), dedupPrice))
    return;
```

---

### DW-B66-BE-01 — CancelQxBrackets cancels PTT-BE-Stop orders during Quick Exit

**Priority**: P1
**Target block**: B75+ (Director confirmation required)
**Status**: OPEN — no change in B74-LaneC.

**Description**: The widened predicate in `IsQxCancelCandidate` (branch 4,
`StartsWith("PTT-BE-", StringComparison.Ordinal)`) means that pressing Quick Exit cancels
any live `PTT-BE-Stop`, `PTT-BE-Stop-{i+1}`, or `PTT-BE-Target-{i+1}` orders.
Director must confirm this is the intended behavior. If NOT intended, branch (4) should be
removed from `IsQxCancelCandidate`.

---

### DW-B63-01 — Spurious PTT-Copy bracket orders on Sim102 after ATM fill

**Priority**: P1
**Target block**: B75+ (next available)
**Status**: OPEN — no change in B74-LaneC.

**Description**: After an ATM fill on the leader account, spurious PTT-Copy bracket orders
appear on the follower Sim102 account. Investigation starting point: review `DispatchCopy`
Gate 0.5 (`IsExitSignalName`) and Gate A (`IsFollowerAccount`) for the bracket order dispatch
path. Verify `IsWorkingBracket` is correctly widened to `Accepted` state.

---

### DW-B54-01 — ATM auto-inject (blocked)

**Priority**: P1
**Target block**: future (blocked — requires StrategyBase-level NT8 API unavailable in AddOnBase)
**Status**: OPEN — blocked. No change in B74-LaneC.

**Description**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per
`NT8_FULL_REFERENCE.md`. The `AddOnBase` (`TradeCopierAddOn`) cannot call this API. A
companion `StrategyBase` add-in would be required. Deferred indefinitely pending Director
architectural decision.

---

### DW-B58-01 — SnapshotTargetsPublic hardcoded order-name prefixes

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B74-LaneC.

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and
`PTT-TGT-`. B74-C-04 adds `PTT-BE-Target-` to `PttGlobalQuickExit.SnapshotTargetOrders` but
`CopyEngine.SnapshotTargetsPublic` was not updated. Future blocks adding new PTT-prefixed
target order names must update `SnapshotTargetsPublic` or the snapshot will miss them.

---

### DW-B58-02 — GlobalBe non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B74-LaneC.

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...`
(non-atomic). Currently safe — both callers (TradeCopierPanel, TradeCopierWindow) access
exclusively from the WPF UI thread. If a future block introduces a non-UI-thread caller,
`Interlocked.CompareExchange` will be required.

---

### DW-B58-03 — RelayBe does not forward OcoGroup from BeEventArgs

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B74-LaneC.

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
`SubmitBeStop` generates its own `OcoId` via `NextQxOcoId()`. If a future block requires
correlated OcoId fan-out across accounts for a single BE event, a new `SubmitBeStop` overload
accepting an explicit `OcoGroup` will be needed.

---

### PRE-EXISTING-01 — Non-ASCII characters at CopyEngine.cs lines 398, 499

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B74-LaneC.

**Description**: Em-dash Unicode characters in B56 BUILD-FIX stub markers (comment lines
only). Lines 398 and 499 are not in the B74-LaneC modification region and are not shifted
by B74-LaneC changes (no production .cs files modified in this block).

---

### PRE-EXISTING-02 — Non-ASCII characters at CopyEngine.cs lines ~1449-1450

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B74-LaneC.

**Description**: Unicode arrow characters in exit-order direction comments. B74-LaneC does
not modify CopyEngine.cs, so line numbers are unchanged from B66-LaneC estimate (~1449-1450).
Re-confirm exact lines in the next block that touches CopyEngine.cs below line 1000.

---

### PRE-EXISTING-03 — deploy-sync.ps1 archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN — pre-existing infrastructure state. No change in B74-LaneC.

**Description**: `deploy-sync.ps1` is archived to `archive/v12-reference/scripts/deploy-sync.ps1`
and maps V12_002 strategy files, not PropTraderTools AddOn files. Manual SHA-256 copy +
`verify_links.ps1 -Fix` is the current PropTraderTools deploy workflow. B74-LaneC used
`scripts\sync-ptt-to-nt8.ps1` (Copied: 0, correct for retrospective-only block).

---

## Summary Table

| ID | Item | Priority | Target | Status |
|----|------|----------|--------|--------|
| DW-B66-C-02 | DispatchCopy Gate 5 dedup key = 0.0 for StopLimit entries (Gate 5 LimitPrice) | P1 | B75+ | OPEN |
| DW-B66-BE-01 | CancelQxBrackets cancels PTT-BE-Stop on Quick Exit — Director confirmation | P1 | B75+ | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B75+ | OPEN |
| DW-B54-01 | ATM auto-inject (blocked — StrategyBase required) | P1 | future | OPEN (blocked) |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines ~1449-1450 | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | future | OPEN |

**Closed this block**: 0
**New items opened this block**: 0
**Total OPEN carry-forward**: 10 items (3×P1 + 1×P1-blocked + 6×P2)
