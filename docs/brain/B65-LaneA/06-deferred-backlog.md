# B65-LaneA Deferred Backlog

**Block**: B65-LaneA
**Written by**: ptt-plan-reviewer (Ph5)
**Date**: 2026-08-12

---

## Closed This Block

### DW-B65-01 (= DW-B60-01) — Leader manual close does not propagate to followers

**Priority**: P0 (live trading correctness)
**Status**: CLOSED — B65-LaneA Ticket-1

**Resolution**: `IsNativeExitName` helper (CopyEngine.cs lines 761-779, CYC=6) identifies native
NT8 exit order names (Close / Flatten / Rev* / Exit*) and returns `true`. Guard (3) in
`TryDispatchLeaderFlat` changed from:

```csharp
if (hasOpenPosition(account, instrument)) return false;  // OLD
```

to:

```csharp
if (!IsNativeExitName(orderName) && hasOpenPosition(account, instrument)) return false;  // B65
```

When the leader's close order is a native NT8 exit, `IsNativeExitName` returns `true`, the
short-circuit `!true && ...` evaluates to `false`, and the hasOpenPosition check is bypassed
entirely. Followers are flattened unconditionally.

**Root cause**: NT8_FULL_REFERENCE.md line 1721:
> "Changes to positions will not be reflected till at least the next OnBarUpdate() event after an order fill."

The position-race guard was blocking the close propagation when the position had already been
closed (fill delivered) but the NT8 position state had not yet updated.

**Commit**: B65-LaneA implementation (2026-08-12)

---

### DW-B59-02 — IsExitSignalName uses exact "Rev" match instead of prefix

**Priority**: P1
**Status**: CLOSED — confirmed already fixed in B60; no action required in B65.

**Evidence**: CopyEngine.cs line 756:
```csharp
if (name.StartsWith("Rev", StringComparison.Ordinal))         return true;
```
`StartsWith("Rev")` has been in production since B60. The B62 deferred backlog listed this as
OPEN because the fix was applied but not formally acknowledged in the deferred-backlog closure.
`IsNativeExitName` inherits the correct `StartsWith("Rev")` pattern from day 1 of B65.
No further action required.

---

## New Deferred Items — B65

### DW-B64-01 — B62 drag sync not working (HandleEntryChange not firing)

**Priority**: P0
**Target block**: B66+ (next available)
**Status**: OPEN

**Description**: From Director live testing after B62 deployment: `HandleEntryChange` is not
firing when a stop-limit entry is dragged on the leader account. The B62 implementation added
Gate C in `OnOrderUpdate` to detect entry price changes and call `HandleEntryChange` to propagate
drags to follower `PTT-Copy` orders. The mechanism is present in source but not activating in
live testing.

**Investigation starting point**: Verify Gate C conditions in `OnOrderUpdate` — check whether the
price-change detection condition (`limitPrice != storedPrice`) is being evaluated correctly for
the order type being dragged. Verify `_dedupCache` has an entry for the order being dragged
(otherwise Gate C short-circuits). Check `CopyEngineTests.cs` T_B62_04 for the expected price
comparison logic.

---

### DW-B63-01 — Spurious PTT-Copy bracket orders on Sim102 after ATM fill

**Priority**: P1
**Target block**: B66+ (next available)
**Status**: OPEN

**Description**: After an ATM fill on the leader account, spurious PTT-Copy bracket orders appear
on the follower Sim102 account. These orders are not part of the intended copy cascade.

**Investigation starting point**: Review `DispatchCopy` Gate 0.5 (`IsExitSignalName` check) and
Gate A (`IsFollowerAccount` check) for the bracket order dispatch path. Verify `IsWorkingBracket`
(B63 T1) is correctly widened to `Accepted` state so bracket orders are detected before they
transition to Working. Check the `_dedupCache` for double-dispatch via ConcurrentDictionary
TryAdd semantics vs. the prior timestamp dedup.

---

## Carry-Forward Items (OPEN, unchanged from B62)

### DW-B58-01 — SnapshotTargetsPublic hardcoded order-name prefixes

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B65.

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and `PTT-TGT-`.
Future blocks adding new PTT-prefixed target order names must update this method or the snapshot
will miss them.

---

### DW-B58-02 — GlobalBe non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B65.

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...`
(non-atomic). Currently safe — both callers (TradeCopierPanel, TradeCopierWindow) access
exclusively from the WPF UI thread. If a future block introduces a non-UI-thread caller,
`Interlocked.CompareExchange` will be required.

---

### DW-B58-03 — RelayBe does not forward OcoGroup from BeEventArgs

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B65.

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
`SubmitBeStop` generates its own `OcoId` via `NextQxOcoId()`. If a future block requires
correlated OcoId fan-out across accounts for a single BE event, a new `SubmitBeStop` overload
accepting an explicit `OcoGroup` will be needed.

---

### DW-B54-01 — ATM auto-inject (blocked)

**Priority**: P1
**Target block**: future (blocked — requires StrategyBase-level NT8 API unavailable in AddOnBase)
**Status**: OPEN — blocked. No change in B65.

**Description**: `AtmStrategyCreate()` is confirmed `StrategyBase`-only per
`NT8_FULL_REFERENCE.md`. The `AddOnBase` (`TradeCopierAddOn`) cannot call this API. A companion
`StrategyBase` add-in would be required. Deferred indefinitely pending Director architectural
decision.

---

### PRE-EXISTING-01 — Non-ASCII characters at CopyEngine.cs lines 398, 499

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B65.

**Description**: Em-dash Unicode characters in B56 BUILD-FIX stub markers (comment lines only).
Line numbers 398 and 499 are unchanged from B62 (B65 inserts `IsNativeExitName` after line 760;
no shift to lines above 760).

---

### PRE-EXISTING-02 — Non-ASCII characters at CopyEngine.cs lines 1401, 1402

**Priority**: P2
**Status**: OPEN — pre-existing. Not introduced by B65.

**Description**: Unicode arrow characters in exit-order direction comments. Line numbers shifted
from B62 baseline (1376, 1377) to **1401, 1402** due to B65 inserting ~25 lines for
`IsNativeExitName` (lines 761-779 + surrounding blank lines). Same physical comment blocks;
no new non-ASCII introduced.

---

### PRE-EXISTING-03 — deploy-sync.ps1 archived; PropTraderTools sync is manual

**Priority**: P2
**Status**: OPEN — pre-existing infrastructure state. No change in B65.

**Description**: `deploy-sync.ps1` is archived to `archive/v12-reference/scripts/deploy-sync.ps1`
and maps V12_002 strategy files, not PropTraderTools AddOn files. Manual SHA-256 copy +
`verify_links.ps1 -Fix` is the current PropTraderTools deploy workflow.

---

## Summary Table

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B65-01 | Leader manual close does not propagate to followers | P0 | B65 | **CLOSED** |
| DW-B59-02 | IsExitSignalName exact Rev match instead of prefix | P1 | — | **CLOSED** (B60) |
| DW-B64-01 | B62 drag sync — HandleEntryChange not firing | P0 | B66+ | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B66+ | OPEN |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | OPEN |
| DW-B54-01 | ATM auto-inject (blocked — StrategyBase required) | P1 | future | OPEN (blocked) |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines 1401-1402 | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived | P2 | future | OPEN |

**Closed this block**: 2 (DW-B65-01, DW-B59-02)
**Opened this block**: 2 (DW-B64-01, DW-B63-01)
**Carry-forward OPEN**: 9 items (1×P0, 2×P1, 6×P2)
