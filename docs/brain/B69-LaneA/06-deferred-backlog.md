# B69-LaneA Deferred Backlog

**Block**: B69-LaneA
**Written by**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-08-13

---

## Closed This Block

### DW-B69-01 — FlattenOneAccount: PTT-Copy orders not cancelled + market order never submitted

**Priority**: P0
**Status**: CLOSED — B69-LaneA Ticket-1
**File**: `src/PropTraderTools/CopyEngine.cs`

**Resolution**:
1. New method `CancelAllAccountOrders(Account acc, NinjaTrader.Cbi.Instrument instr)` inserted
   after line 470 (CYC=4). Cancels ALL active orders (Working, Initialized, Submitted, Accepted,
   ChangeSubmitted) for the instrument on the account — name-agnostic. Replaces the
   name-gated `CancelQxBrackets` call in the flatten path.
2. `FlattenOneAccount` line 1483: `CancelQxBrackets(acc, instrument)` replaced with
   `CancelAllAccountOrders(acc, instrument)`.
3. `FlattenOneAccount` lines 1487-1491: `CreateOrder` result captured in `var order`;
   `if (order != null) acc.Submit(new[] { order });` added — market order now transmitted
   to broker.
4. Stale comment at line 450 ("Also called by FlattenOneAccount") deleted.
Tests: T_B69_01, T_B69_02, T_B69_03, T_B69_04, T_B69_07.

---

### DW-B69-02 — SubmitBeStop + FindPosition: reference equality misses follower position

**Priority**: P1
**Status**: CLOSED — B69-LaneA Ticket-1
**File**: `src/PropTraderTools/CopyEngine.cs`

**Resolution**:
- `SubmitBeStop` line 512: `if (p.Instrument == instr)` replaced with
  `if (p.Instrument != null && p.Instrument.FullName == instr.FullName)`.
- `FindPosition` line 1778: `if (p.Instrument == instrument) return p;` replaced with
  `if (p.Instrument != null && p.Instrument.FullName == instrument.FullName) return p;`.
Both fixes use `FullName` string comparison (NT8_FULL_REFERENCE.md line 1926) with explicit
null-guard. Consistent with established pattern in `CancelQxBrackets` line 463.
Tests: T_B69_05.

---

### DW-B69-03 — HandleEntryChange: new orderId not in `_dedupCache` after resubmit

**Priority**: P1
**Status**: CLOSED — B69-LaneA Ticket-1
**File**: `src/PropTraderTools/CopyEngine.cs`

**Resolution**:
- After `acc.Submit(new[] { order })` inside `if (order != null)` block (line 1163):
  `_dedupCache[order.OrderId.ToString()] = newPrice;` added.
- Closes the race window between Submit and the NT8 Accepted event.
- Prevents `DispatchCopy` from treating the new follower order as a new leader event
  (double-copy guard). CYC delta = 0 (straight-line assignment inside existing block).
- Reference: PropagateFollowerEntryReplace Build 947 PendingCancel absorb pattern.
Tests: T_B69_06.

---

## New Deferred Items — B69-LaneA

### DOC-B69-01 — Stale B67-LaneB comment contradicts B69 `_dedupCache` preload

**Priority**: P2
**Target block**: future (docs-only, no code risk)
**Status**: OPEN

**Description**: Lines 1119-1122 in `CopyEngine.cs` still contain the old B67-LaneB comment:
"New entry will be re-keyed by DispatchCopy on the follower's Accepted event. Do NOT insert
newPrice under the old key after cancel+resubmit."

The B69 fix at line 1163 directly contradicts this comment by inserting exactly that preload.
The new B69 comment block (lines 1159-1162) documents the correct behavior and supersedes the
old text. Code is correct; only the stale comment creates documentation confusion.

**Fix**: Delete lines 1119-1122 (old B67-LaneB comment block) in the next block that touches
`HandleEntryChange`.

---

## Carry-Forward Items (OPEN, unchanged from B66-LaneC)

### DW-B66-C-02 — DispatchCopy Gate 5: dedup key = 0.0 for all StopLimit entries

**Priority**: P1
**Target block**: B70+ (next available)
**Status**: OPEN — no change in B69-LaneA.

**Description**: `DispatchCopy` Gate 5 passes `order.LimitPrice` to `IsDedup` as the dedup key.
Since `StopLimit.LimitPrice == 0` always (NT8 confirmed), every StopLimit entry order on every
instrument shares dedup key `0.0`. First StopLimit entry dispatch succeeds; any subsequent
concurrent StopLimit entry dispatch is wrongly rejected as a duplicate.

**Fix approach**:
```csharp
double dedupPrice = order.OrderType == OrderType.StopLimit ? order.StopPrice : order.LimitPrice;
if (IsDedup(order.OrderId.ToString(), dedupPrice))
    return;
```
Or extract `GetDedupPrice(Order order)` helper if `DispatchCopy` CYC is already >= 7.

---

### DW-B66-BE-01 — CancelQxBrackets cancels PTT-BE-Stop orders on Quick Exit

**Priority**: P1
**Target block**: B70+ (Director confirmation required)
**Status**: OPEN — no change in B69-LaneA.

**Description**: The widened predicate in `IsQxCancelCandidate` (branch 4,
`StartsWith("PTT-BE-", StringComparison.Ordinal)`) means Quick Exit cancels any live
`PTT-BE-Stop*` or `PTT-BE-Target*` orders. Director must confirm this is intended behavior.
If NOT intended, branch (4) should be removed from `IsQxCancelCandidate`.

---

### DW-B63-01 — Spurious PTT-Copy bracket orders on Sim102 after ATM fill

**Priority**: P1
**Target block**: B70+ (next available)
**Status**: OPEN — no change in B69-LaneA.

**Description**: After an ATM fill on the leader account, spurious PTT-Copy bracket orders
appear on the follower Sim102 account. Investigation starting point: `DispatchCopy` Gate 0.5
(`IsExitSignalName` check), Gate A (`IsFollowerAccount` check), and `_dedupCache` double-dispatch
via ConcurrentDictionary TryAdd semantics.

---

### DW-B54-01 — ATM auto-inject (blocked)

**Priority**: P1
**Target block**: future (blocked — requires StrategyBase-level NT8 API unavailable in AddOnBase)
**Status**: OPEN — blocked. No change in B69-LaneA.

**Description**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per
`NT8_FULL_REFERENCE.md`. The `AddOnBase` (`TradeCopierAddOn`) cannot call this API. A companion
`StrategyBase` add-in would be required. Deferred indefinitely pending Director architectural
decision.

---

### DW-B58-01 — SnapshotTargetsPublic hardcoded order-name prefixes

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B69-LaneA.

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and
`PTT-TGT-`. Future blocks adding new PTT-prefixed target order names must update this method
or the snapshot will miss them.

---

### DW-B58-02 — GlobalBe non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B69-LaneA.

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...`
(non-atomic). Currently safe — both callers access exclusively from the WPF UI thread. If
a future block introduces a non-UI-thread caller, `Interlocked.CompareExchange` will be
required.

---

### DW-B58-03 — RelayBe does not forward OcoGroup from BeEventArgs

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B69-LaneA.

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
`SubmitBeStop` generates its own `OcoId` via `NextQxOcoId()`. If a future block requires
correlated OcoId fan-out across accounts for a single BE event, a new `SubmitBeStop` overload
accepting an explicit `OcoGroup` will be needed.

---

### PRE-EXISTING-01 — Non-ASCII em-dash in B56 BUILD-FIX stub markers

**Priority**: P2
**Target block**: future
**Status**: OPEN — pre-existing. Not introduced by B69-LaneA.

**Description**: Em-dash Unicode characters in B56 BUILD-FIX stub markers at CopyEngine.cs
lines 404 and 580 (comment lines only). Confirmed by verifier SCAN-06.

---

### PRE-EXISTING-02 — Non-ASCII arrow chars in exit-order direction comments

**Priority**: P2
**Target block**: future
**Status**: OPEN — pre-existing. Not introduced by B69-LaneA.

**Description**: Unicode arrow characters in exit-order direction comments. Confirmed by
verifier SCAN-06 at lines 1539-1540 (B29 artifact). Next block touching CopyEngine.cs in
that region should fix these on a per-line basis.

---

### PRE-EXISTING-03 — deploy-sync.ps1 archived; PropTraderTools sync is manual

**Priority**: P2
**Target block**: future
**Status**: OPEN — pre-existing infrastructure state. No change in B69-LaneA.

**Description**: `deploy-sync.ps1` is archived and maps V12_002 strategy files only, not
PropTraderTools AddOn files. Current PropTraderTools deploy workflow is manual SHA-256 copy
verified per ticket completion checklist.

---

## Summary Table

| ID | Item | Priority | Target | Status |
|----|------|----------|--------|--------|
| DW-B69-01 | FlattenOneAccount: name-gated cancel + missing broker Submit | P0 | B69-LaneA | **CLOSED** |
| DW-B69-02 | SubmitBeStop + FindPosition: reference equality misses follower position | P1 | B69-LaneA | **CLOSED** |
| DW-B69-03 | HandleEntryChange: new orderId not in dedupCache — double-copy race | P1 | B69-LaneA | **CLOSED** |
| DOC-B69-01 | Stale B67-LaneB comment contradicts B69 dedupCache preload (doc-only) | P2 | future | OPEN |
| DW-B66-C-02 | DispatchCopy Gate 5: dedup key = 0.0 for all StopLimit entries | P1 | B70+ | OPEN |
| DW-B66-BE-01 | CancelQxBrackets cancels PTT-BE-Stop on Quick Exit — Director confirm | P1 | B70+ | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B70+ | OPEN |
| DW-B54-01 | ATM auto-inject — StrategyBase-only API, blocked in AddOnBase | P1 | future (blocked) | OPEN (blocked) |
| DW-B58-01 | SnapshotTargetsPublic hardcoded order-name prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded to SubmitBeStop | P2 | future | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 404, 580 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow chars CopyEngine.cs lines ~1539-1540 | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | future | OPEN |

**Closed this block**: 3 (DW-B69-01, DW-B69-02, DW-B69-03)
**Opened this block**: 1 (DOC-B69-01 — doc debt, no code risk)
**Carry-forward OPEN**: 11 items (3×P1 + 1×P1-blocked + 7×P2)
