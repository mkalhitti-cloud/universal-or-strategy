# B66-LaneC Deferred Backlog

**Block**: B66-LaneC
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-13

---

## Closed This Block

### DW-B64-01 — HandleEntryChange never fires for StopLimit entry orders

**Priority**: P0 (live trading correctness)
**Status**: CLOSED — B66-LaneC Ticket-1
**Commits**: `d6002b95` (CopyEngine.cs changes), `5ebbf8b6` (CopyEngineB66Tests.cs)

**Resolution**: Three independent defects in the CopyEngine.cs drag-sync pipeline were fixed,
plus two new private static helper methods were added:

1. **Defect 1 — Gate C type guard** (CopyEngine.cs line ~697): widened from
   `OrderType.Limit` to `(OrderType.Limit || OrderType.StopLimit)`. Price comparison
   changed from direct `e.Order.LimitPrice` to `GetOrderPrice(e.Order)` via new
   `currentPrice` local variable. NT8 ground truth: StopLimit.LimitPrice == 0 always;
   drag price lives in StopPrice.

2. **Defect 2 — FindFollowerEntryOrder** (CopyEngine.cs lines ~1034-1036): state guard
   widened from `OrderState.Working` only to `(OrderState.Working || OrderState.Accepted)`.
   Type guard widened from `OrderType.Limit` only to `(OrderType.Limit || OrderType.StopLimit)`.
   NT8 ground truth: broker-simulated StopLimit orders may stay in Accepted state permanently
   (NT8_FULL_REFERENCE.md line 1005).

3. **Defect 3 — HandleEntryChange** (CopyEngine.cs lines ~1055, ~1072, ~1078): three direct
   `LimitPrice` reads/writes replaced — `leaderOrder.LimitPrice` → `GetOrderPrice(leaderOrder)`;
   `fo.LimitPrice` (read) → `GetOrderPrice(fo)`; `fo.LimitPrice = newPrice` → `SetFollowerPrice(fo, newPrice)`.
   NT8 ground truth: Account.Change() for StopLimit must set StopPrice, not LimitPrice
   (NT8_FULL_REFERENCE.md lines 898-899).

4. **Helper `GetOrderPrice`** (CopyEngine.cs lines ~1008-1009, CYC=2): returns
   `order.StopPrice` for StopLimit orders, `order.LimitPrice` for all others. Pure
   ternary one-liner. Used in Gate C and HandleEntryChange (3 call sites).

5. **Helper `SetFollowerPrice`** (CopyEngine.cs lines ~1016-1022, CYC=2): sets
   `fo.StopPrice = newPrice` for StopLimit follower orders, `fo.LimitPrice = newPrice`
   for all others. Replaces the single direct assignment in HandleEntryChange.

8 xUnit [Fact] tests (T_B66_C_01..T_B66_C_08 in `CopyEngineB66Tests.cs`) exercise all
paths including both helpers. All 7 scans (S1-S7) returned 0 violations in new/modified code.

---

## New Deferred Items — B66-LaneC

### DW-B66-C-02 — DispatchCopy dedup key = 0.0 for all StopLimit entries

**Priority**: P1
**Target block**: B67+
**Status**: OPEN
**Location**: `src/PropTraderTools/CopyEngine.cs` line ~832-835

**Description**: `DispatchCopy` Gate 5 passes `order.LimitPrice` to `IsDedup` as the
dedup key. Since `StopLimit.LimitPrice == 0` always (NT8 confirmed: Fact 1 from plan
Section 2), every StopLimit entry order on every instrument shares dedup key `0.0`. The
first StopLimit entry dispatch on any instrument succeeds; any subsequent StopLimit entry
dispatch (same or different instrument) is wrongly rejected as a duplicate. The initial
copy-dispatch of a second (or later) concurrent StopLimit entry silently fails.

**Root cause**: Gate 5 current code (verified unchanged in ticket-1-verification.md lines 256-261):
```csharp
// Gate 5: dedup -- reject duplicate event for same orderId
// B62: pass limitPrice as second arg (price-keyed dedup).
if (IsDedup(order.OrderId.ToString(), order.LimitPrice))
    return;
```
`order.LimitPrice` is always 0 for StopLimit. All StopLimit entries share key 0.0.

**Impact**: With Defects 1-3 now fixed (DW-B64-01 closed), drag-sync works correctly for
StopLimit orders. However, the initial copy-dispatch of a second concurrent StopLimit entry
on any instrument will silently fail due to this dedup collision. The trader is affected
only on the second (or later) concurrent StopLimit entry across accounts.

**Fix approach** (B67+):
```csharp
// Replace the IsDedup call at Gate 5:
double dedupPrice = order.OrderType == OrderType.StopLimit ? order.StopPrice : order.LimitPrice;
if (IsDedup(order.OrderId.ToString(), dedupPrice))
    return;
```
Alternatively extract a `GetDedupPrice(Order order)` helper if `DispatchCopy` CYC is
already >= 7. The approach mirrors `GetOrderPrice` (added in B66-LaneC for HandleEntryChange).

**Defer rationale**: Scope creep risk (AGENTS.md Section 11). `DispatchCopy` Gate 5 and
the `IsDedup` signature intersect ALL copy paths (Market, Limit, StopLimit, StopMarket).
Changing them risks regressions in tested Limit and Market paths. The drag-sync fixes
(Defects 1-3) are independent and shipped without this fix. A separate PR with dedicated
test coverage is the safer approach. The blast radius of touching `DispatchCopy` is
significantly larger than the single-method HandleEntryChange scope of DW-B64-01.

---

## Carry-Forward Items (OPEN, unchanged from B66-LaneA)

### DW-B66-BE-01 — CancelQxBrackets cancels PTT-BE-Stop orders during Quick Exit

**Priority**: P1
**Target block**: B67+ (Director confirmation required)
**Status**: OPEN — opened by B66-LaneA; no change in B66-LaneC.

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

### DW-B63-01 — Spurious PTT-Copy bracket orders on Sim102 after ATM fill

**Priority**: P1
**Target block**: B67+ (next available)
**Status**: OPEN — no change in B66-LaneC.

**Description**: After an ATM fill on the leader account, spurious PTT-Copy bracket orders
appear on the follower Sim102 account. These orders are not part of the intended copy cascade.

**Investigation starting point**: Review `DispatchCopy` Gate 0.5 (`IsExitSignalName` check)
and Gate A (`IsFollowerAccount` check) for the bracket order dispatch path. Verify
`IsWorkingBracket` (B63 T1) is correctly widened to `Accepted` state so bracket orders are
detected before they transition to Working. Check the `_dedupCache` for double-dispatch via
ConcurrentDictionary TryAdd semantics vs. the prior timestamp dedup.

---

### DW-B58-01 — SnapshotTargetsPublic hardcoded order-name prefixes

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B66-LaneC.

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and
`PTT-TGT-`. Future blocks adding new PTT-prefixed target order names must update this
method or the snapshot will miss them.

---

### DW-B58-02 — GlobalBe non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B66-LaneC.

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...`
(non-atomic). Currently safe — both callers (TradeCopierPanel, TradeCopierWindow) access
exclusively from the WPF UI thread. If a future block introduces a non-UI-thread caller,
`Interlocked.CompareExchange` will be required.

---

### DW-B58-03 — RelayBe does not forward OcoGroup from BeEventArgs

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B66-LaneC.

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
`SubmitBeStop` generates its own `OcoId` via `NextQxOcoId()`. If a future block requires
correlated OcoId fan-out across accounts for a single BE event, a new `SubmitBeStop`
overload accepting an explicit `OcoGroup` will be needed.

---

### DW-B54-01 — ATM auto-inject (blocked)

**Priority**: P1
**Target block**: future (blocked — requires StrategyBase-level NT8 API unavailable in AddOnBase)
**Status**: OPEN — blocked. No change in B66-LaneC.

**Description**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per
`NT8_FULL_REFERENCE.md`. The `AddOnBase` (`TradeCopierAddOn`) cannot call this API. A
companion `StrategyBase` add-in would be required. Deferred indefinitely pending Director
architectural decision.

---

### PRE-EXISTING-01 — Non-ASCII characters at CopyEngine.cs lines 398, 499

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B66-LaneC.

**Description**: Em-dash Unicode characters in B56 BUILD-FIX stub markers (comment lines
only). Lines 398 and 499 are above the B66-LaneC modification region (lines 692+) and are
not shifted by B66-LaneC changes.

---

### PRE-EXISTING-02 — Non-ASCII characters at CopyEngine.cs lines ~1449-1450

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B66-LaneC.

**Description**: Unicode arrow characters in exit-order direction comments. The verifier
(`ticket-1-verification.md` SCAN 5) identified these at lines 1449, 1450 after all B66
lanes are applied. B66-LaneC inserts ~27 net new lines in the 1004-1087 region, shifting
all subsequent lines. The ~1449-1450 estimate from the verifier is the most current value;
re-confirm exact lines in the next block that touches CopyEngine.cs below line 1000.

---

### PRE-EXISTING-03 — deploy-sync.ps1 archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN — pre-existing infrastructure state. No change in B66-LaneC.

**Description**: `deploy-sync.ps1` is archived to `archive/v12-reference/scripts/deploy-sync.ps1`
and maps V12_002 strategy files, not PropTraderTools AddOn files. Manual SHA-256 copy +
`verify_links.ps1 -Fix` is the current PropTraderTools deploy workflow.

---

## Summary Table

| ID | Item | Priority | Target | Status |
|----|------|----------|--------|--------|
| DW-B64-01 | HandleEntryChange never fires for StopLimit entry orders | P0 | B66-LaneC | **CLOSED** |
| DW-B66-01 | CancelQxBrackets missed ATM bracket names (Stop1/Stop2/Target1/Target2) | P0 | B66-LaneA | **CLOSED** (B66-LaneA) |
| DW-B66-BE-01 | CancelQxBrackets cancels PTT-BE-Stop on Quick Exit — Director confirmation | P1 | B67+ | OPEN |
| DW-B66-C-02 | DispatchCopy dedup key = 0.0 for all StopLimit entries (Gate 5 LimitPrice) | P1 | B67+ | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B67+ | OPEN |
| DW-B54-01 | ATM auto-inject (blocked — StrategyBase required) | P1 | future | OPEN (blocked) |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines ~1449-1450 | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | future | OPEN |

**Closed this block**: 1 (DW-B64-01)
**Closed prior block (B66-LaneA)**: 1 (DW-B66-01 — confirmed in B66-LaneA/06-deferred-backlog.md)
**Opened this block**: 1 (DW-B66-C-02)
**Carry-forward OPEN**: 9 items (3×P1 + 1×P1-blocked + 5×P2)
