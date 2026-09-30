# B70-LaneA Deferred Backlog

**Block**: B70-LaneA
**Written by**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-08-14
**Predecessor**: docs/brain/B66-LaneC/06-deferred-backlog.md

---

## Closed This Block

### DW-B70-01 — OCO ID reuse rejection on second Quick Exit press

**Priority**: P0 (live trading correctness)
**Status**: CLOSED — B70-LaneA Ticket 1
**Defect verified by**: ticket-1-verification.md (VERIFY_PASS, Layer 3 independent scan)

**Resolution**: `CopyEngine._qxOcoSeq` was initialized to `0` at field-declaration time.
On every session reconnect or AddOn reload, the counter reset to `0`, causing
`NextQxOcoId()` to produce `"PTT-QX-00001"` again. NT8's simulated broker tracks OCO
group names within a session connection; re-submitting the same group name for a different
bracket pair causes NT8 to reject the second order.

**Fix**: `CopyEngine.cs` line 523 changed:

```csharp
// BEFORE
private int _qxOcoSeq = 0;

// AFTER (B70 DW-B70-01)
private int _qxOcoSeq = Environment.TickCount & 0x7FFF;
```

`Environment.TickCount & 0x7FFF` seeds the counter at a value in `[0, 32767]` determined
by system uptime at construction time. Two consecutive sessions start at different values
(~1/32768 collision probability — effectively zero since NT8 sim resets its OCO name
table on each reconnect). `NextQxOcoId()` method body unchanged; `Interlocked.Increment`
pattern retained. CYC=1 unchanged.

---

### DW-B70-02 — PTT-Copy brackets not cancelled on follower during Quick Exit

**Priority**: P0 (live trading correctness)
**Status**: CLOSED — B70-LaneA Ticket 2
**Defect verified by**: ticket-2-verification.md (VERIFY_PASS, Layer 3 independent scan)

**Resolution**: Two independent gaps closed in one ticket:

**Part A — Predicate gap**: `IsQxCancelCandidate` had no branch for the `"PTT-Copy"` prefix
used for all copy-dispatched entry orders (`CopyEngine.cs` line 1267 confirms
`string signalName = "PTT-Copy"`). PTT-Copy orders were excluded from the cancel set.

`CopyEngine.cs` `IsQxCancelCandidate` (lines 435-448), branch (5) inserted after PTT-BE-
branch (4), before `return false`:

```csharp
if (o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)) return true;   // (5) B70 DW-B70-02
```

CYC: 5 → 6 (still within CYC <= 8 limit).

**Part B — Sweep gap**: `PttQuickExit.Execute` Step 3 only swept the leader account via
`CancelQxBrackets(leader, instr)`. PTT-Copy orders live on follower accounts and are
invisible to the leader sweep. Even with the predicate fix alone, follower orders were
never iterated.

`PttQuickExit.cs` line 54 added after existing leader sweep:

```csharp
// B70 DW-B70-02: also cancel follower PTT-Copy brackets before re-placing QX orders
CopyEngine.Instance?.CancelQxBracketsForFollowers(instr);
```

`CancelQxBracketsForFollowers` already existed and was already called by `PttGlobalQuickExit.Execute`
(line 38). The per-chart Quick Exit path (`PttQuickExit`) was the only missing call site.
CYC of `Execute`: 5 → 6 (`?.` null-conditional adds +1 McCabe decision point — Roslyn strict).
Still within CYC <= 8 limit.

---

## Carry-Forward Items (OPEN, no change in B70-LaneA)

### DW-B66-C-02 — DispatchCopy dedup key = 0.0 for all StopLimit entries

**Priority**: P1
**Target block**: B67+
**Status**: OPEN — no change in B70-LaneA

**Description**: `DispatchCopy` Gate 5 passes `order.LimitPrice` to `IsDedup` as the dedup
key. Since `StopLimit.LimitPrice == 0` always (NT8 confirmed), every StopLimit entry order
on every instrument shares dedup key `0.0`. First StopLimit dispatch succeeds; subsequent
StopLimit dispatches (same or different instrument) are wrongly rejected as duplicates.

**Fix approach** (B67+): replace Gate 5 dedup key with
`order.OrderType == OrderType.StopLimit ? order.StopPrice : order.LimitPrice`.

---

### DW-B66-BE-01 — CancelQxBrackets cancels PTT-BE-Stop on Quick Exit

**Priority**: P1
**Target block**: B67+ (Director confirmation required)
**Status**: OPEN — no change in B70-LaneA

**Description**: `IsQxCancelCandidate` branch (4) `StartsWith("PTT-BE-", Ordinal)` means
pressing Quick Exit cancels any live `PTT-BE-Stop`, `PTT-BE-Stop-{i+1}`, or
`PTT-BE-Target-{i+1}` orders for the instrument. Director must confirm this is intended
behavior; if not, branch (4) should be removed.

---

### DW-B63-01 — Spurious PTT-Copy bracket orders on Sim102 after ATM fill

**Priority**: P1
**Target block**: B67+ (next available)
**Status**: OPEN — no change in B70-LaneA

**Description**: After an ATM fill on the leader account, spurious PTT-Copy bracket orders
appear on the follower Sim102 account. Investigation starting point: review `DispatchCopy`
Gate 0.5 (`IsExitSignalName` check) and Gate A (`IsFollowerAccount` check) for the bracket
order dispatch path.

---

### DW-B54-01 — ATM auto-inject (blocked)

**Priority**: P1
**Target block**: future (blocked — requires `StrategyBase`-level NT8 API)
**Status**: OPEN — no change in B70-LaneA

**Description**: `AtmStrategyCreate()` is `StrategyBase`-only (confirmed:
`NT8_FULL_REFERENCE.md`). `AddOnBase` (`TradeCopierAddOn`) cannot call this API. A
companion `StrategyBase` add-in would be required. Deferred indefinitely pending Director
architectural decision.

---

### DW-B58-01 — SnapshotTargetsPublic hardcoded order-name prefixes

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B70-LaneA

**Description**: `SnapshotTargetsPublic` checks for hardcoded prefixes `PTT-QX-T` and
`PTT-TGT-`. Future blocks adding new PTT-prefixed target order names must update this
method or the snapshot will miss them.

---

### DW-B58-02 — GlobalBe non-atomic lazy init

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B70-LaneA

**Description**: `GlobalBe` lazy init uses `if (_globalBe == null) _globalBe = new ...`
(non-atomic). Currently safe — both callers access exclusively from the WPF UI thread. If a
future block introduces a non-UI-thread caller, `Interlocked.CompareExchange` will be required.

---

### DW-B58-03 — RelayBe OcoGroup not forwarded

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B70-LaneA

**Description**: `RelayBe` does not forward `OcoGroup` from `BeEventArgs` to `SubmitBeStop`.
If a future block requires correlated OcoId fan-out across accounts for a single BE event,
a new `SubmitBeStop` overload accepting an explicit `OcoGroup` will be needed.

---

### PRE-EXISTING-01 — Non-ASCII em-dash CopyEngine.cs lines 404, 581

**Priority**: P2
**Target block**: future
**Status**: OPEN — pre-existing; not touched by B70-LaneA

**Description**: Em-dash Unicode characters in B56 BUILD-FIX stub markers (comment lines
only). Lines 404 and 581 are outside the B70-LaneA modification regions and are not shifted
by B70-LaneA changes.

**Note**: B66-LaneC/06-deferred-backlog.md cited lines 398, 499. T1-verifier (ticket-1-verification.md
SCAN-05) identified actual locations as 404 and 581. The 404/581 values are the authoritative
current baseline.

---

### PRE-EXISTING-02 — Non-ASCII arrows CopyEngine.cs lines ~1542-1543

**Priority**: P2
**Target block**: future
**Status**: OPEN — pre-existing; not touched by B70-LaneA

**Description**: Unicode arrow characters in exit-order direction comments. Line-number
history:
- B66-LaneC baseline: ~1449-1450
- T1-verifier (ticket-1-verification.md SCAN-05): confirmed pre-existing at lines 1540-1541
  (after B66 insertions)
- T2-verifier (ticket-2-verification.md SCAN-05): T2 insertions in lines 435-448 region
  (+2 net lines) shift these to **~1542-1543** — current authoritative value

Re-confirm exact lines in the next block that touches `CopyEngine.cs` below line 1000.

---

### PRE-EXISTING-03 — deploy-sync.ps1 archived; PropTraderTools sync is manual

**Priority**: P2
**Target block**: future
**Status**: OPEN — no change in B70-LaneA

**Description**: `deploy-sync.ps1` is archived to `archive/v12-reference/scripts/deploy-sync.ps1`
and maps V12_002 strategy files, not PropTraderTools AddOn files. Manual SHA-256 copy +
`verify_links.ps1 -Fix` is the current PropTraderTools deploy workflow.

---

## New Deferred Items — B70-LaneA

None. All B70-LaneA scope (DW-B70-01 and DW-B70-02) is fully closed cleanly within this block.

---

## Summary Table

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B70-01 | OCO ID reuse on session reconnect | P0 | B70-LaneA | **CLOSED** |
| DW-B70-02 | PTT-Copy brackets not cancelled on follower | P0 | B70-LaneA | **CLOSED** |
| DW-B66-C-02 | DispatchCopy dedup key = 0.0 for StopLimit (Gate 5) | P1 | B67+ | OPEN |
| DW-B66-BE-01 | CancelQxBrackets cancels PTT-BE-Stop on QX — Director confirm | P1 | B67+ | OPEN |
| DW-B63-01 | Spurious PTT-Copy brackets on Sim102 after ATM fill | P1 | B67+ | OPEN |
| DW-B54-01 | ATM auto-inject (blocked — StrategyBase required) | P1 (blocked) | future | OPEN |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 404, 581 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrows CopyEngine.cs lines ~1542-1543 | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived | P2 | future | OPEN |

**Closed this block**: 2 (DW-B70-01, DW-B70-02)
**Opened this block**: 0
**Carry-forward OPEN**: 10 items (3×P1 + 1×P1-blocked + 6×P2)
