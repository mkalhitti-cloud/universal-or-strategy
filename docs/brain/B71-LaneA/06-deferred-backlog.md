# B71-LaneA Deferred Backlog

**Block**: B71-LaneA
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-13

---

## Closed This Block

### DW-B71-01 -- CancelQxBrackets misses ATM brackets in Submitted state

**Priority**: P1
**Status**: CLOSED -- B71-LaneA Ticket T1

**Resolution**: Added `|| o.OrderState == OrderState.Submitted` as the 4th branch of the `stateOk`
gate in `CopyEngine.CancelQxBrackets` (`CopyEngine.cs:463`). ATM bracket orders placed less than
~800ms before Quick Exit press are now caught. NT8 ground truth: `OrderState.Submitted` documented
at `NT8_FULL_REFERENCE.md:936-937`; `Account.Cancel()` places no documented restriction on order
state at cancel time (`NT8_FULL_REFERENCE.md:318-319`). CYC unchanged (Roslyn CFG: `||` branches
in a single bool assignment are one decision point). Verified by independent verifier: line 463
confirmed present in source.

---

### DW-B71-02 -- PttQuickExit.Execute fires on follower accounts (no guard)

**Priority**: P1
**Status**: CLOSED -- B71-LaneA Ticket T1

**Resolution**: Added optional parameter `bool skipIfFollower = true` to `PttQuickExit.Execute`
(`PttQuickExit.cs:34`). Inserted follower guard block at lines 49-59:
```csharp
if (skipIfFollower && CopyEngine.Instance?.IsFollowerAccount(leader) == true)
{
    NinjaTrader.Code.Output.Process(
        "PTT-QX: follower guard -- skip " + (leader != null ? leader.Name : "NULL"),
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    return;
}
```
All existing 4-argument call sites receive `skipIfFollower = true` by default (no call site
changes required). CYC: 6 -> 7 (within JS DNA limit of 8). Guard block position: after flat/null
skip (line 47), before Step 2 SnapshotStopPrice (line 62). Verified by independent verifier:
guard block confirmed at lines 49-59.

---

### DW-B71-04 -- PttGlobalQuickExit.Execute does not dispatch QX to follower accounts

**Priority**: P1
**Status**: CLOSED -- B71-LaneA Ticket T1

**Resolution**: Three sub-changes to `PttGlobalQuickExit.Execute`:

1. Removed the redundant `engine?.CancelQxBracketsForFollowers(pos.Instrument)` call (FIX 3a) --
   follower bracket cancel now occurs inside `ExecuteOne(follower, ...)` via `PttQuickExit.Execute`
   Step 3 (`CancelQxBrackets(follower, instr)`).

2. Added follower dispatch loop (FIX 3b, `PttGlobalQuickExit.cs:40-47`):
   ```csharp
   var rule = engine?.FindRule(pos.Instrument);
   if (rule != null)
       foreach (var follower in rule.Value.FollowerAccounts)
       {
           if (follower == null) continue;
           ExecuteOne(follower, pos.Instrument, ticks.t1, ticks.t2, skipIfFollower: false);
       }
   ```

3. Updated `ExecuteOne` signature to accept and forward `bool skipIfFollower = true` (FIX 3d/3e).

Required enabling changes:
- `CopyEngine.FindRule`: `private` -> `internal` (`CopyEngine.cs:1751`) to allow access from
  `PttGlobalQuickExit` (same assembly, different class).
- `CopyRule`: `private readonly struct` -> `internal readonly struct` (`CopyEngine.cs:177`) to
  satisfy CS0050 (return-type accessibility >= method accessibility). Minimal change; still
  restricts to assembly boundary.

CYC of `PttGlobalQuickExit.Execute`: 6 - 1 (removed null-propagation) + 3 (rule null-check,
follower foreach, follower null continue) = 8. Exactly at JS DNA limit. Verified by independent
verifier: all changes confirmed present.

---

## New Deferred Items -- B71-LaneA

### DW-B71-03 -- PttQuickExit.Execute line 67 calls CancelQxBracketsForFollowers on follower context (double-cancel path)

**Priority**: P2
**Target block**: B72+
**Status**: OPEN

**Description**: When `PttGlobalQuickExit.Execute` calls `ExecuteOne(follower, instr, t1, t2, skipIfFollower: false)`,
`PttQuickExit.Execute` Step 3 at line 67 invokes `CopyEngine.Instance?.CancelQxBracketsForFollowers(instr)`.
Since `FindRule(instr)` is keyed by instrument (not by whether the calling account is leader or
follower), `CancelQxBracketsForFollowers` will iterate the rule's followers and call
`CancelQxBrackets` for each -- including the follower account that is already being processed.
This results in a second cancel pass against follower accounts on every Global Quick Exit.

NT8 behavior: `Account.Cancel()` is idempotent for already-cancelled orders (broker rejects the
cancel; NT8 propagates an `OrderState.Cancelled` or silently no-ops). No duplicate orders are
created. Functionally harmless.

Architecturally: the double-cancel path is redundant and confusing. A future block should
consider passing context information to avoid the second pass, or restructuring Step 3 of
`PttQuickExit.Execute` to skip `CancelQxBracketsForFollowers` when acting as a follower.

**Fix approach** (B72+): Add `bool isFollowerContext = false` parameter to `PttQuickExit.Execute`
and guard the `CancelQxBracketsForFollowers` call with `if (!isFollowerContext)`. Alternatively,
extract `Step3Cancel` as a strategy parameter. Defer pending Director confirmation that the
double-cancel is acceptable at B71 baseline.

---

## Carry-Forward Items (OPEN, unchanged from B66-LaneC)

### DW-B66-BE-01 -- CancelQxBrackets cancels PTT-BE-Stop orders during Quick Exit

**Priority**: P1
**Target block**: B72+ (Director confirmation required)
**Status**: OPEN -- no change in B71-LaneA.

**Description**: The widened predicate in `IsQxCancelCandidate` (branch 4,
`StartsWith("PTT-BE-", StringComparison.Ordinal)`) means pressing Quick Exit now cancels any
live `PTT-BE-Stop`, `PTT-BE-Stop-{i+1}`, or `PTT-BE-Target-{i+1}` orders. Ensures clean
position exit but removes breakeven stop protection at Quick Exit time. Director must confirm
intended behavior. If NOT intended, branch (4) should be removed from `IsQxCancelCandidate`.

---

### DW-B66-C-02 -- DispatchCopy dedup key = 0.0 for all StopLimit entries

**Priority**: P1
**Target block**: B72+
**Status**: OPEN -- no change in B71-LaneA.

**Description**: `DispatchCopy` Gate 5 passes `order.LimitPrice` to `IsDedup`. Since
`StopLimit.LimitPrice == 0` always (NT8 confirmed), every StopLimit entry shares dedup key `0.0`.
First StopLimit dispatch on any instrument succeeds; subsequent StopLimit dispatches are wrongly
rejected as duplicates.

**Fix approach**: `double dedupPrice = order.OrderType == OrderType.StopLimit ? order.StopPrice : order.LimitPrice;`

---

### DW-B63-01 -- Spurious PTT-Copy bracket orders on Sim102 after ATM fill

**Priority**: P1
**Target block**: B72+
**Status**: OPEN -- no change in B71-LaneA.

**Description**: After an ATM fill on the leader account, spurious PTT-Copy bracket orders appear
on follower Sim102. Investigation starting point: `DispatchCopy` Gate 0.5 (`IsExitSignalName`),
Gate A (`IsFollowerAccount`), `IsWorkingBracket` state widening, and `_dedupCache` double-dispatch.

---

### DW-B54-01 -- ATM auto-inject (blocked)

**Priority**: P1
**Target block**: future (blocked -- StrategyBase required)
**Status**: OPEN (blocked) -- no change in B71-LaneA.

**Description**: `AtmStrategyCreate()` is `StrategyBase`-only per `NT8_FULL_REFERENCE.md`.
`AddOnBase` (`TradeCopierAddOn`) cannot call this API. A companion `StrategyBase` add-in would
be required. Deferred indefinitely pending Director architectural decision.

---

### DW-B58-01 -- SnapshotTargetsPublic hardcoded order-name prefixes

**Priority**: P2
**Target block**: future
**Status**: OPEN -- no change in B71-LaneA.

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and `PTT-TGT-`.
Future blocks adding new PTT-prefixed target order names must update this method.

---

### DW-B58-02 -- GlobalBe non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN -- no change in B71-LaneA.

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...`
(non-atomic). Currently safe (both callers are WPF UI-thread only). If a future block introduces
a non-UI-thread caller, `Interlocked.CompareExchange` will be required.

---

### DW-B58-03 -- RelayBe does not forward OcoGroup from BeEventArgs

**Priority**: P2
**Target block**: future
**Status**: OPEN -- no change in B71-LaneA.

**Description**: `RelayBe` generates its own `OcoId` via `NextQxOcoId()` rather than forwarding
`OcoGroup` from `BeEventArgs`. If a future block requires correlated OcoId fan-out, a new
`SubmitBeStop` overload accepting an explicit `OcoGroup` will be needed.

---

### PRE-EXISTING-01 -- Non-ASCII em-dash at CopyEngine.cs lines 404, 584

**Priority**: P2
**Status**: OPEN -- pre-existing. Not introduced by B71-LaneA.

**Description**: Em-dash Unicode characters in B56 BUILD-FIX stub markers at comment lines 404
and 584. Verifier SCAN-01 confirmed exact lines (previously estimated as 398, 499; verifier
grep produced lines 404, 584 -- reflects line shifts from subsequent blocks). Out of scope for
B71-LaneA modifications.

---

### PRE-EXISTING-02 -- Non-ASCII arrow at CopyEngine.cs lines 1543, 1544

**Priority**: P2
**Status**: OPEN -- pre-existing. Not introduced by B71-LaneA.

**Description**: Unicode arrow characters in exit-order direction comments. Verifier SCAN-01
confirmed exact lines as 1543, 1544 (previously estimated as ~1449-1450; reflects line shifts
from B66-LaneC and B71-LaneA insertions).

---

### PRE-EXISTING-03 -- deploy-sync.ps1 archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN -- pre-existing infrastructure state. No change in B71-LaneA.

**Description**: `deploy-sync.ps1` is archived to `archive/v12-reference/scripts/deploy-sync.ps1`
and maps V12_002 strategy files, not PropTraderTools AddOn files. Manual SHA-256 copy +
`verify_links.ps1 -Fix` is the current PropTraderTools deploy workflow.

---

## Summary Table

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B71-01 | CancelQxBrackets misses ATM brackets in Submitted state | P1 | B71 | **CLOSED** |
| DW-B71-02 | PttQuickExit.Execute fires on follower accounts (no guard) | P1 | B71 | **CLOSED** |
| DW-B71-04 | PttGlobalQuickExit.Execute does not dispatch QX to follower accounts | P1 | B71 | **CLOSED** |
| DW-B71-03 | PttQuickExit.Execute line 67 calls CancelQxBracketsForFollowers on follower context (double-cancel) | P2 | B72+ | OPEN |
| DW-B66-BE-01 | CancelQxBrackets cancels PTT-BE-Stop on Quick Exit -- Director confirmation required | P1 | B72+ | OPEN |
| DW-B66-C-02 | DispatchCopy dedup key = 0.0 for all StopLimit entries (Gate 5 LimitPrice) | P1 | B72+ | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B72+ | OPEN |
| DW-B54-01 | ATM auto-inject (blocked -- StrategyBase required) | P1 | future (blocked) | OPEN (blocked) |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 404, 584 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines 1543, 1544 | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | future | OPEN |

**Closed this block**: 3 (DW-B71-01, DW-B71-02, DW-B71-04)
**Opened this block**: 1 (DW-B71-03 -- P2 double-cancel awareness)
**Carry-forward OPEN**: 10 items (3xP1 + 1xP1-blocked + 6xP2)
**Total tracked**: 14 items
