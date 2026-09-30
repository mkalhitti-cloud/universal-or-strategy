# B67-LaneB Deferred Backlog

**Block**: B67-LaneB
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-13

---

## Closed This Block

### DW-B67-02 — HandleEntryChange replace acc.Change() with cancel+CreateOrder+Submit

**Priority**: P0 (live trading correctness — Apex/Rithmic broker-side no-op)
**Status**: CLOSED — B67-LaneB Ticket-1
**Commit**: 5c95e416

**Resolution**: The `try { SetFollowerPrice(fo, newPrice); acc.Change(new Order[] { fo }); } catch` block
in `HandleEntryChange` (CopyEngine.cs) was replaced with the cancel+CreateOrder+Submit pattern.

Three changes applied:
1. **Change A** (comment block, lines 1067-1077): Added DW-B67-02 citation, @2Custom
   PropagateMasterEntryMove FIX-PM-02/FIX-PM-02b reference, NT8_FULL_REFERENCE.md lines 898-899
   citation, limitPx/stopPx logic summary, CYC=7 branch enumeration, JS-001/JS-021/JS-002 annotations.
2. **Change B** (_dedupCache, line 1094): `_dedupCache[key] = newPrice` replaced with
   `_dedupCache.TryRemove(key, out _)` — stale key evicted after cancel+resubmit so new follower
   order is re-keyed by DispatchCopy on its Accepted event.
3. **Change C** (try block, lines 1109-1129): acc.Cancel + acc.CreateOrder + acc.Submit pattern.
   StopLimit routing: limitPx=0, stopPx=newPrice (NT8_FULL_REFERENCE.md lines 898-899).
   Limit routing: limitPx=newPrice, stopPx=0. Submit guarded by `if (order != null)` (CYC branch 7).

5 xUnit [Fact] tests T_B67_B_01..T_B67_B_05 added (CopyEngineTests.cs lines 3479-3552).
All 7 scans returned 0 violations. SHA-256 deploy verified: source = destination hash 8D74310C...

---

## New Deferred Items — B67-LaneB

None.

---

## Carry-Forward Items (OPEN, from B66-LaneC)

### DW-B66-C-02 — DispatchCopy dedup key = 0.0 for all StopLimit entries

**Priority**: P1
**Target block**: B67+
**Status**: OPEN — not touched in B67-LaneB.
**Location**: `src/PropTraderTools/CopyEngine.cs` line ~832-835

**Description**: `DispatchCopy` Gate 5 passes `order.LimitPrice` to `IsDedup` as the
dedup key. Since `StopLimit.LimitPrice == 0` always (NT8 confirmed: Fact 1), every StopLimit
entry order on every instrument shares dedup key `0.0`. The first StopLimit entry dispatch
on any instrument succeeds; any subsequent StopLimit entry dispatch (same or different
instrument) is wrongly rejected as a duplicate. The initial copy-dispatch of a second (or
later) concurrent StopLimit entry silently fails.

**Root cause**: Gate 5 current code:
```csharp
// Gate 5: dedup -- reject duplicate event for same orderId
// B62: pass limitPrice as second arg (price-keyed dedup).
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
Alternatively extract a `GetDedupPrice(Order order)` helper if `DispatchCopy` CYC is
already >= 7. Mirrors `GetOrderPrice` (added in B66-LaneC).

**Defer rationale**: Scope creep risk (AGENTS.md Section 11). `DispatchCopy` Gate 5 and
the `IsDedup` signature intersect ALL copy paths. A separate PR with dedicated test
coverage is the safer approach.

---

### DW-B66-BE-01 — CancelQxBrackets cancels PTT-BE-Stop orders during Quick Exit

**Priority**: P1
**Target block**: B67+ (Director confirmation required)
**Status**: OPEN — no change in B67-LaneB.

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
**Status**: OPEN — no change in B67-LaneB.

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
**Status**: OPEN — no change in B67-LaneB.

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and
`PTT-TGT-`. Future blocks adding new PTT-prefixed target order names must update this
method or the snapshot will miss them.

---

### DW-B58-02 — GlobalBe non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B67-LaneB.

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...`
(non-atomic). Currently safe — both callers (TradeCopierPanel, TradeCopierWindow) access
exclusively from the WPF UI thread. If a future block introduces a non-UI-thread caller,
`Interlocked.CompareExchange` will be required.

---

### DW-B58-03 — RelayBe does not forward OcoGroup from BeEventArgs

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B67-LaneB.

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
`SubmitBeStop` generates its own `OcoId` via `NextQxOcoId()`. If a future block requires
correlated OcoId fan-out across accounts for a single BE event, a new `SubmitBeStop`
overload accepting an explicit `OcoGroup` will be needed.

---

### DW-B54-01 — ATM auto-inject (blocked)

**Priority**: P1
**Target block**: future (blocked — requires StrategyBase-level NT8 API unavailable in AddOnBase)
**Status**: OPEN — blocked. No change in B67-LaneB.

**Description**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per
`NT8_FULL_REFERENCE.md`. The `AddOnBase` (`TradeCopierAddOn`) cannot call this API. A
companion `StrategyBase` add-in would be required. Deferred indefinitely pending Director
architectural decision.

---

### PRE-EXISTING-01 — Non-ASCII characters at CopyEngine.cs lines 398, 499

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B67-LaneB.

**Description**: Em-dash Unicode characters in B56 BUILD-FIX stub markers (comment lines
only). Lines 398 and 499 are above the B67-LaneB modification region (lines 1067+) and are
not shifted by B67-LaneB changes.

---

### PRE-EXISTING-02 — Non-ASCII characters at CopyEngine.cs lines ~1463-1464

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B67-LaneB.

**Description**: Unicode arrow characters in exit-order direction comments. B66-LaneC
estimated these at ~1449-1450 (after ~27 net new lines in the 1004-1087 region). B67-LaneB
inserts ~14 net new lines in the 1067-1131 region (comment block expansion from 6 to 11
lines +5, dedupCache 1 to 4 lines +3, try-block removal and replacement net ~+6). Updated
estimate: ~1463-1464. Re-confirm exact lines in the next block that touches CopyEngine.cs
below line 1000.

---

### PRE-EXISTING-03 — deploy-sync.ps1 archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN — pre-existing infrastructure state. No change in B67-LaneB.

**Description**: `deploy-sync.ps1` is archived to `archive/v12-reference/scripts/deploy-sync.ps1`
and maps V12_002 strategy files, not PropTraderTools AddOn files. Manual SHA-256 copy +
`verify_links.ps1 -Fix` is the current PropTraderTools deploy workflow. B67-LaneB used the
manual copy protocol successfully (SHA-256 MATCH confirmed in ticket-1-completion.md).

---

## Summary Table

| ID | Item | Priority | Target | Status |
|----|------|----------|--------|--------|
| DW-B67-02 | HandleEntryChange replace acc.Change() with cancel+CreateOrder+Submit | P0 | B67-LaneB | **CLOSED** — 5c95e416 |
| DW-B66-C-02 | DispatchCopy Gate 5 dedup key = 0.0 for all StopLimit entries | P1 | B67+ | OPEN |
| DW-B66-BE-01 | CancelQxBrackets cancels PTT-BE-Stop orders on Quick Exit — Director confirmation | P1 | B67+ | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B67+ | OPEN |
| DW-B54-01 | ATM auto-inject (blocked — StrategyBase required) | P1 | future (blocked) | OPEN (blocked) |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines ~1463-1464 (updated from ~1449-1450) | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | future | OPEN |

**Closed this block**: 1 (DW-B67-02)
**Opened this block**: 0
**Carry-forward OPEN**: 9 items (3×P1 + 1×P1-blocked + 5×P2)
