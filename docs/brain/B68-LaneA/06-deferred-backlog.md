# B68-LaneA Deferred Backlog

**Block**: B68-LaneA
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-14

---

## Closed This Block

### DW-B68-01 -- Cancel follower stale brackets before PTT-QX and PTT-BE orders

**Priority**: P0 (live trading correctness)
**Status**: CLOSED -- B68-LaneA Ticket-1
**Confirmed live**: 2026-08-13

**Resolution**: Two independent defect paths were fixed in a single ticket:

1. **QX path (PttGlobalQuickExit.Execute)**: New helper `CancelQxBracketsForFollowers` added to
   `CopyEngine` (CopyEngine.cs:479-489, CYC=5). Called via `engine?.CancelQxBracketsForFollowers(pos.Instrument)`
   at PttGlobalQuickExit.cs:38 inside the leader position loop, BEFORE `ExecuteOne`. The helper uses
   `FindRule(instr)` to obtain the CopyRule and iterates `rule.Value.FollowerAccounts` directly,
   calling the existing `CancelQxBrackets(acc, instr)` on each non-null follower. Master account is
   not touched by this helper (it is handled by the existing PttQuickExit path). CYC of Execute:
   5 -> 6 (one new McCabe decision point for the `engine?.` null-conditional).

2. **BE path (CopyEngine.RelayBe)**: Foreach body expanded to call `CancelQxBrackets(acc, e.Instrument)`
   before `SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong)` for every account in `AllAccounts`.
   CYC unchanged at 2 (void call in loop body is not a McCabe decision point). All accounts
   (master and followers) now have stale ATM brackets cleared before the new BE stop is placed.

3. **Tests**: 6 xUnit [Fact] tests (T_B68_01..T_B68_06) in `src/PropTraderTools/Tests/B68Tests.cs`,
   registered in PropTraderTools.csproj line 122. All 7 scans (S1-S7) returned 0 new violations.

**Constraints honored**: PttQuickExit.Execute, IsQxCancelCandidate, IsAtmBracketName, and
CancelQxBrackets were not modified. No new NT8 API surface introduced.

---

## Carry-Forward Items (OPEN, unchanged from B66-LaneC)

### DW-B66-BE-01 -- CancelQxBrackets cancels PTT-BE-Stop orders during Quick Exit

**Priority**: P1
**Target block**: B67+ (Director confirmation required)
**Status**: OPEN -- no change in B68-LaneA.

**Description**: The widened predicate in `IsQxCancelCandidate` (branch 4,
`StartsWith("PTT-BE-", StringComparison.Ordinal)`) means that pressing Quick Exit will now
cancel any live `PTT-BE-Stop`, `PTT-BE-Stop-{i+1}`, or `PTT-BE-Target-{i+1}` orders on the
account for the instrument. This ensures a clean position exit but removes breakeven stop
protection at the moment of Quick Exit.

**Action required**: Director must confirm that cancelling PTT-BE-* orders on Quick Exit
is the intended behavior. If NOT intended, branch (4) should be removed from
`IsQxCancelCandidate`, retaining only: (1) null guard, (2) `IsAtmBracketName`,
(3) `PTT-QX-` prefix.

**PTT-BE-* order name variants in production**:

| Variant | Source |
|---------|--------|
| `"PTT-BE-Stop"` | PttBreakEven.cs:217, :374; CopyEngine.cs:496 |
| `"PTT-BE-Stop-1"`, `"PTT-BE-Stop-2"`, ... | PttBreakEven.cs:407 |
| `"PTT-BE-Target-1"`, `"PTT-BE-Target-2"`, ... | PttBreakEven.cs:446 |
| `"PTT-BE-XXXX-00001-0"` (OCO group ID) | PttBreakEven.cs:328 |

---

### DW-B66-C-02 -- DispatchCopy dedup key = 0.0 for all StopLimit entries

**Priority**: P1
**Target block**: B67+
**Status**: OPEN -- no change in B68-LaneA.
**Location**: `src/PropTraderTools/CopyEngine.cs` line ~832-835

**Description**: `DispatchCopy` Gate 5 passes `order.LimitPrice` to `IsDedup` as the
dedup key. Since `StopLimit.LimitPrice == 0` always (NT8 confirmed), every StopLimit entry
order on every instrument shares dedup key `0.0`. The first StopLimit entry dispatch on any
instrument succeeds; any subsequent StopLimit entry dispatch (same or different instrument)
is wrongly rejected as a duplicate. The initial copy-dispatch of a second (or later)
concurrent StopLimit entry silently fails.

**Root cause**: Gate 5 current code:
```csharp
if (IsDedup(order.OrderId.ToString(), order.LimitPrice))
    return;
```
`order.LimitPrice` is always 0 for StopLimit. All StopLimit entries share key 0.0.

**Fix approach** (B67+):
```csharp
double dedupPrice = order.OrderType == OrderType.StopLimit ? order.StopPrice : order.LimitPrice;
if (IsDedup(order.OrderId.ToString(), dedupPrice))
    return;
```

**Defer rationale**: `DispatchCopy` Gate 5 and `IsDedup` intersect ALL copy paths. Changing
them risks regressions in tested Limit and Market paths. A separate PR with dedicated test
coverage is the safer approach.

---

### DW-B63-01 -- Spurious PTT-Copy bracket orders on Sim102 after ATM fill

**Priority**: P1
**Target block**: B67+ (next available)
**Status**: OPEN -- no change in B68-LaneA.

**Description**: After an ATM fill on the leader account, spurious PTT-Copy bracket orders
appear on the follower Sim102 account. These orders are not part of the intended copy cascade.

**Investigation starting point**: Review `DispatchCopy` Gate 0.5 (`IsExitSignalName` check)
and Gate A (`IsFollowerAccount` check) for the bracket order dispatch path. Verify
`IsWorkingBracket` (B63 T1) is correctly widened to `Accepted` state so bracket orders are
detected before they transition to Working. Check the `_dedupCache` for double-dispatch via
ConcurrentDictionary TryAdd semantics vs. the prior timestamp dedup.

---

### DW-B58-01 -- SnapshotTargetsPublic hardcoded order-name prefixes

**Priority**: P2
**Target block**: future
**Status**: OPEN -- no change in B68-LaneA.

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and
`PTT-TGT-`. Future blocks adding new PTT-prefixed target order names must update this
method or the snapshot will miss them.

---

### DW-B58-02 -- GlobalBe non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN -- no change in B68-LaneA.

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...`
(non-atomic). Currently safe -- both callers (TradeCopierPanel, TradeCopierWindow) access
exclusively from the WPF UI thread. If a future block introduces a non-UI-thread caller,
`Interlocked.CompareExchange` will be required.

---

### DW-B58-03 -- RelayBe does not forward OcoGroup from BeEventArgs

**Priority**: P2
**Target block**: future
**Status**: OPEN -- no change in B68-LaneA.

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
`SubmitBeStop` generates its own `OcoId` via `NextQxOcoId()`. If a future block requires
correlated OcoId fan-out across accounts for a single BE event, a new `SubmitBeStop`
overload accepting an explicit `OcoGroup` will be needed.

---

### DW-B54-01 -- ATM auto-inject (blocked)

**Priority**: P1
**Target block**: future (blocked -- requires StrategyBase-level NT8 API unavailable in AddOnBase)
**Status**: OPEN -- blocked. No change in B68-LaneA.

**Description**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per
`NT8_FULL_REFERENCE.md`. The `AddOnBase` (`TradeCopierAddOn`) cannot call this API. A
companion `StrategyBase` add-in would be required. Deferred indefinitely pending Director
architectural decision.

---

### PRE-EXISTING-01 -- Non-ASCII characters at CopyEngine.cs lines 398, 499

**Priority**: P2
**Status**: OPEN -- pre-existing. Not introduced by B68-LaneA.

**Description**: Em-dash Unicode characters in B56 BUILD-FIX stub markers (comment lines
only). Lines 398 and 499 are above the B68-LaneA modification regions (lines 343-357 and
472-489) and were not shifted by B68-LaneA changes.

---

### PRE-EXISTING-02 -- Non-ASCII characters at CopyEngine.cs lines ~1449-1450

**Priority**: P2
**Status**: OPEN -- pre-existing. Not introduced by B68-LaneA.

**Description**: Unicode arrow characters in exit-order direction comments. The verifier
(`ticket-1-verification.md` SCAN-04) identified these at lines 1500, 1501 after all B68
changes are applied. B68-LaneA inserts ~17 net new lines in the 343-489 region; the pre-existing
arrow comment lines are now at ~1500-1501 (was ~1449-1450 in B66-LaneC). Re-confirm exact
line numbers in the next block that touches CopyEngine.cs below line 1000.

---

### PRE-EXISTING-03 -- deploy-sync.ps1 archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN -- pre-existing infrastructure state. No change in B68-LaneA.

**Description**: `deploy-sync.ps1` is archived to `archive/v12-reference/scripts/deploy-sync.ps1`
and maps V12_002 strategy files, not PropTraderTools AddOn files. Manual SHA-256 copy is
the current PropTraderTools deploy workflow (engineer runs Copy-Item + SHA-256 verify per
ticket deploy step).

---

## Summary Table

| ID | Item | Priority | Target | Status |
|----|------|----------|--------|--------|
| DW-B68-01 | Cancel follower stale brackets before PTT-QX and PTT-BE orders | P0 | B68-LaneA | **CLOSED** |
| DW-B64-01 | HandleEntryChange never fires for StopLimit entry orders | P0 | B66-LaneC | **CLOSED** (B66-LaneC) |
| DW-B66-01 | CancelQxBrackets missed ATM bracket names (Stop1/Stop2/Target1/Target2) | P0 | B66-LaneA | **CLOSED** (B66-LaneA) |
| DW-B66-BE-01 | CancelQxBrackets cancels PTT-BE-Stop on Quick Exit -- Director confirmation | P1 | B67+ | OPEN |
| DW-B66-C-02 | DispatchCopy dedup key = 0.0 for all StopLimit entries (Gate 5 LimitPrice) | P1 | B67+ | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B67+ | OPEN |
| DW-B54-01 | ATM auto-inject (blocked -- StrategyBase required) | P1 | future | OPEN (blocked) |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines ~1500-1501 (was ~1449-1450 in B66-LaneC) | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | future | OPEN |

**Closed this block**: 1 (DW-B68-01)
**Closed prior blocks**: 2 (DW-B64-01 at B66-LaneC; DW-B66-01 at B66-LaneA)
**Opened this block**: 0 (no new deferred items)
**Carry-forward OPEN**: 10 items (3xP1-open + 1xP1-blocked + 6xP2)
