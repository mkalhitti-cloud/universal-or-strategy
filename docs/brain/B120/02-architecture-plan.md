# B120 Architecture Plan — DW-B129 Leader Fallback Flatten

**Block**: B120  
**Defect**: DW-B129 (P0)  
**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`  
**Status**: PLAN_COMPLETE  
**Date**: 2026-08-28  

---

## SECTION A — Problem Statement

`PttGlobalQuickExit.Execute()` fires for the leader account (`Sim101`) during Quick-Exit-All.
Block B118 (DW-B126) cancels PTT-BE-* orders and waits for terminal state before snapshotting
the order book. This is correct and prevents the BE/QX race condition.

However, when B118 successfully cancels PTT-BE-* orders (i.e., `_beCancelCount > 0`), the
order book after `WaitForPttBeCancelled` is completely empty:

- The original ATM bracket was already replaced by PTT-BE-* orders at BE-fire time.
- PTT-BE-* orders are now cancelled (terminal state).
- `SnapshotTargetOrders` returns `count=0`.
- `PttQuickExit.SnapshotStopPrice` returns `0` (no stop order in book).

`ExecuteOne` is then called with `targets=[]`, `leaderStop=0`. Inside `PttQuickExit.Execute`,
the resolved stop is `0` and target count is `0`. No PTT-QX order is submitted.

The leader account is left with an open position, no bracket, and no protection.
This requires manual intervention to close.

**Log evidence (2026-08-28 live gate)**:
```
[PTT-QX-ALL] CancelPttBeOrders: acc=Sim101 count=6
[PTT-QX-ALL] WaitForPttBeCancelled: acc=Sim101 completed
[DW-B115-DIAG] leader targets: Sim101 count=0 posQty=7
[PTT-QX] stop resolved: 0 on Sim101
[PTT-QX] snapshot: 0 cancellable orders for MES SEP26
[PTT-QX] cancel: 0 queued, 0 race-skipped on Sim101
<-- NO QX ORDER. LEADER LEFT OPEN. -->
```

---

## SECTION B — Root Cause Analysis

**Step-by-step failure chain on the leader path:**

1. **BE-fire replaces ATM bracket**: When `MoveStopToBreakEven` fires, it cancels the native
   ATM `Stop1`/`Target1..N` and submits `PTT-BE-Stop-*` / `PTT-BE-Target-*` in their place.
   The native ATM bracket no longer exists in the order book.

2. **B118 cancels PTT-BE-***: `CancelPttBeOrders(acc, MES)` finds 6 non-terminal PTT-BE-*
   orders and cancels them. `WaitForPttBeCancelled` polls until all 6 reach terminal state.
   The order book is now clean — no ATM bracket, no PTT-BE-* bracket.

3. **Clean slate does not equal protected**: `SnapshotTargetOrders` correctly returns an
   empty list (no Working/Accepted Limit orders). The clean slate is not a protected state —
   the leader has 7 open contracts with zero bracket protection.

4. **ExecuteOne no-op**: `PttQuickExit.Execute` receives `targets=[]`, `leaderStop=0`.
   Its internal snapshot finds no cancellable orders. It resolves stop=0. It submits no
   PTT-QX order. This is internally consistent — there is nothing to cancel and no stop
   price to anchor the new bracket. But the outcome is wrong: the position is naked.

5. **No fallback**: There is no guard after `SnapshotTargetOrders` that detects the
   "B118-cancelled + empty-book + open-position" state and takes protective action.

**Why followers are not affected by this bug**: Each follower uses a separate
`_fBeCancelCount` variable (line 99). Followers go through `ResolveFollowerTargets` which
can scale from leader targets when the follower snapshot is empty. This is a different
code path. B120 does not modify follower logic.

---

## SECTION C — Fix Architecture

### C1. New Helper: `NeedsLeaderFallbackFlatten`

```
internal static bool NeedsLeaderFallbackFlatten(
    int beCancelCount,
    int snapshotCount,
    int posQty)
```

**Semantics**: Returns `true` when ALL three conditions hold:
- `beCancelCount > 0` — B118 path was active (PTT-BE-* orders existed and were cancelled).
  When `_beCancelCount == 0`, the normal ATM bracket is intact and `ExecuteOne` handles the exit.
- `snapshotCount == 0` — The order book snapshot is empty after the B118 wait.
  This confirms the "replaced bracket + cancelled" scenario. If `snapshotCount > 0`,
  `ExecuteOne` can find its anchor and run normally.
- `posQty > 0` — The leader still has an open position. If the position is already flat
  (e.g. filled at target before QX fired), there is nothing to flatten.

**CYC**: 2 — one && chain (three predicates short-circuit; standard McCabe counts the
compound boolean as 1 decision + 1 implicit short-circuit = CYC=2).  
**JS-021**: No lock.  
**JS-001**: No throw.  
**JS-002**: Returns `bool` — no null.  
**JS-033**: Synchronous static — no async.  
**ASCII-only**: All identifiers and literals are ASCII.  

### C2. Fallback Flatten Block in `Execute()`

Inserted AFTER `SnapshotTargetOrders` (line 52) and BEFORE `ExecuteOne` (line 90):

```csharp
if (NeedsLeaderFallbackFlatten(_beCancelCount, targets.Count, pos.Quantity))
{
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-FLATTEN] leader fallback flatten: "
            + acc.Name + " " + pos.Instrument.FullName
            + " qty=" + pos.Quantity,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    acc.Flatten(pos.Instrument);
    continue;  // skip ExecuteOne -- Flatten handles the exit
}
```

**Behaviour**:
- When `NeedsLeaderFallbackFlatten` returns `true`: log, call `acc.Flatten(pos.Instrument)`,
  then `continue` to the next `pos` in the `foreach (Position pos in acc.Positions)` loop.
  `ExecuteOne` is not called. The leader position is closed at market by NT8 Flatten.
- When `NeedsLeaderFallbackFlatten` returns `false`: the `if` block is skipped entirely.
  Execution falls through to `ExecuteOne` exactly as before. Normal QX bracket swap fires.
  No change to the happy path.

**The `continue` is correct**: it exits the inner `foreach pos` iteration for the current
position and continues with the next. Since the leader typically has one position per
instrument at QX-ALL fire time, this effectively moves to the next account's position scan.

### C3. CYC Budget Analysis and `ExecuteFollowers` Extraction

**Problem**: The new `if (NeedsLeaderFallbackFlatten(...))` block adds 1 McCabe decision
point to `Execute()`. Current `Execute()` is annotated CYC=8 (8 decision points per the
codebase counting convention). Adding 1 branch → CYC=9 → JS-066 violation.

**Resolution — extract `ExecuteFollowers()`**:

Extract lines 92–167 of the current `Execute()` (the `rule != null` guard + follower
`foreach` + follower `null` check + `CancelPttBeOrders` / `WaitForPttBeCancelled` /
`SnapshotTargetOrders` / DIAG block / `ResolveFollowerTargets` / `ExecuteOne` calls)
into a new private method:

```
private void ExecuteFollowers(
    Account acc,
    Position pos,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    (int t1, int t2) ticks,
    double leaderStop)
```

`ExecuteFollowers()` captures `CopyEngine.Instance` internally (same pattern as
`ResolveQuickTicks()`). The engine is not a parameter.

**`Execute()` CYC after extraction**:

| # | Decision point | Type |
|---|----------------|------|
| 1 | `foreach (Account acc in Account.All)` | loop |
| 2 | `if (engine != null && engine.IsFollowerAccount(acc))` | if (compound) |
| 3 | `foreach (Position pos in acc.Positions)` | loop |
| 4 | `if (pos == null \|\| pos.Quantity == 0)` | if (compound) |
| 5 | `if (NeedsLeaderFallbackFlatten(...))` | if (new, B120) |
| 6 | `for (int _i = 0; ...)` in DW-B115-DIAG leader block | loop |

CYC = 6 decisions + 1 = **CYC=7** (including DW-B115-DIAG for-loop).  
When the DW-B115-DIAG block is eventually removed, CYC drops to **CYC=6**.  
Both are well within JS-066 CYC ≤ 8.

**`ExecuteFollowers()` CYC**:

| # | Decision point | Type |
|---|----------------|------|
| 1 | `if (rule != null)` | if |
| 2 | `foreach (var follower in rule.Value.FollowerAccounts)` | loop |
| 3 | `if (follower == null) continue` | if |
| 4 | `foreach (NinjaTrader.Cbi.Position _p in follower.Positions)` | loop (DIAG) |
| 5 | `if (_p != null && _p.Instrument != null && ...)` | if (DIAG) |
| 6 | `for (int _i = 0; ...)` in follower DIAG block | loop (DIAG) |

CYC = 6 decisions + 1 = **CYC=7** (including DIAG).  
Core logic without DIAG blocks: 3 decisions + 1 = **CYC=4**.  
Both within JS-066 CYC ≤ 8. ✅

**`NeedsLeaderFallbackFlatten()` CYC**: **CYC=2** (single `&&` chain expression, no branches).

---

## SECTION D — Follower Path Analysis

The follower path is **not affected** by B120.

- The leader uses `_beCancelCount` (local variable, line 49).
- Each follower uses `_fBeCancelCount` (separate local variable, per-follower, line 99).
- `NeedsLeaderFallbackFlatten` is called only on the leader path with `_beCancelCount`.
- Follower accounts are processed in `ExecuteFollowers()`. They use `ResolveFollowerTargets`
  which already handles the empty-snapshot case by scaling from leader targets (DW-B124 / DW-B125).
- No `NeedsLeaderFallbackFlatten` check is inserted on the follower path.
- No `acc.Flatten` call is made for follower accounts.

This is correct: followers may have a different position size and their empty-snapshot
handling via `ResolveFollowerTargets` is already working. The naked-position scenario
is specific to the leader where PTT-BE-* replaced the original ATM bracket.

---

## SECTION E — NT8 API Confirmation

**Method**: `Account.Flatten(Instrument instrument)`  
**Source**: NT8_FULL_REFERENCE.md (confirmed by spec DW-B129 directive)  
**Behaviour**: Closes all open positions for the specified instrument on the account.
Submits a market order to exit all contracts. Handles any position size.  
**Thread**: UI thread (same as all other calls in `Execute()`).  
**Submit()**: Not required — `Flatten` is a direct method that does not follow the
`CreateOrder/Submit` pattern. The NT8 runtime handles the market exit internally.  
**Caller**: `acc` (the leader account). `pos.Instrument` is passed to scope the flatten
to the specific instrument (e.g. MES SEP26), not all instruments on the account.

---

## SECTION F — Test Plan

**Test file**: `src/PropTraderTools/Tests/B120Tests.cs`  
**Framework**: xUnit only (per `docs/protocol/TEST_FRAMEWORK_PROTOCOL.md`).  
**Target method**: `PttGlobalQuickExit.NeedsLeaderFallbackFlatten(int, int, int)`

### F1 — True path (B118 active + empty snapshot + open position)

```csharp
[Fact]
public void NeedsLeaderFallbackFlatten_BECancelledAndEmptySnapshotAndOpenPos_ReturnsTrue()
{
    bool result = PttGlobalQuickExit.NeedsLeaderFallbackFlatten(
        beCancelCount: 1,
        snapshotCount: 0,
        posQty: 7);
    Assert.True(result);
}
```

**Asserts**: All three conditions satisfied (`beCancelCount>0`, `snapshotCount==0`,
`posQty>0`) → method returns `true`. Flatten fallback should fire.

### F2 — False path: normal path (no BE orders)

```csharp
[Fact]
public void NeedsLeaderFallbackFlatten_NoBECancelCount_ReturnsFalse()
{
    bool result = PttGlobalQuickExit.NeedsLeaderFallbackFlatten(
        beCancelCount: 0,
        snapshotCount: 0,
        posQty: 7);
    Assert.False(result);
}
```

**Asserts**: `beCancelCount==0` (normal path — no PTT-BE-* orders existed) → returns
`false`. `ExecuteOne` runs as before. Flatten does NOT fire on the normal path.

### F3 — False path: targets present (QX runs normally)

```csharp
[Fact]
public void NeedsLeaderFallbackFlatten_BECancelledButSnapshotNonEmpty_ReturnsFalse()
{
    bool result = PttGlobalQuickExit.NeedsLeaderFallbackFlatten(
        beCancelCount: 1,
        snapshotCount: 3,
        posQty: 7);
    Assert.False(result);
}
```

**Asserts**: `snapshotCount==3` (order book has targets after B118 wait) → returns `false`.
`ExecuteOne` runs with the 3 targets. Flatten does NOT fire when orders are available.

---

## SECTION G — 7-Scan Checklist Pre-Assessment

| # | Rule | Check | Result |
|---|------|-------|--------|
| SCAN-01 | JS-021 — `lock()` ban | No `lock()` in new code | PASS |
| SCAN-02 | JS-033 — `async void` ban | No `async` keyword in new code | PASS |
| SCAN-03 | JS-066 — CYC ≤ 8 | `Execute()` CYC=7, `ExecuteFollowers()` CYC=7, `NeedsLeaderFallbackFlatten` CYC=2 | PASS |
| SCAN-04 | JS-001 — no `throw` | No `throw` in new code | PASS |
| SCAN-05 | JS-002 — no `null` return | `NeedsLeaderFallbackFlatten` returns `bool`; `ExecuteFollowers` returns `void` | PASS |
| SCAN-06 | ASCII-only | All new string literals, identifiers, and log tags are ASCII-only | PASS |
| SCAN-07 | NT8 API | `Account.Flatten(Instrument)` confirmed in NT8_FULL_REFERENCE.md; no Submit() needed | PASS |

---

## SECTION H — Deployed State Invariants

After B120 lands, `PttGlobalQuickExit.cs` MUST satisfy all of the following:

| Invariant | Location | Origin |
|-----------|----------|--------|
| `CancelPttBeOrders(acc, pos.Instrument)` on leader path | `Execute()` ~L49 | B118 DW-B126 — unchanged |
| `WaitForPttBeCancelled(acc, ...)` on leader path | `Execute()` ~L50 | B118 DW-B126 — unchanged |
| `NeedsLeaderFallbackFlatten` check + `acc.Flatten` + `continue` | `Execute()` — new, between L52 and L90 | B120 DW-B129 |
| `ExecuteOne(acc, ...)` on normal leader path (only when snapshotCount > 0 or beCancelCount == 0) | `Execute()` ~L90 | Existing — unchanged |
| `ExecuteFollowers(acc, pos, targets, ticks, leaderStop)` call replacing inline follower block | `Execute()` after ExecuteOne | B120 — CYC budget extraction |
| Follower `CancelPttBeOrders` + `WaitForPttBeCancelled` inside `ExecuteFollowers()` | `ExecuteFollowers()` | B118 DW-B126 — moved, not changed |
| `ScaleLeaderTargets` method | unchanged | Existing |
| `ResolveFollowerTargets` method | unchanged | DW-B124 / DW-B125 |
| `SnapshotTargetOrders` dedup by LimitPrice | unchanged | DW-B123 |
| `NeedsLeaderFallbackFlatten(int, int, int): bool` — `internal static` | new method | B120 DW-B129 |
| `ExecuteFollowers(Account, Position, List<...>, (int,int), double): void` — `private` | new method | B120 — CYC budget |

**No changes to**:
- `PttQuickExit.cs`
- `CopyEngine.cs`
- Any other file

---

## Summary

B120 fixes DW-B129 by inserting a single guarded fallback before `ExecuteOne` on the leader
path. When B118 cancels PTT-BE-* orders AND the order book snapshot is empty AND the leader
still holds an open position, `acc.Flatten(pos.Instrument)` closes the position at market
and `continue` skips `ExecuteOne`. The normal path (no BE cancellation, or snapshot has
targets) is completely unaffected.

To keep `Execute()` within CYC ≤ 8, the follower dispatch block (lines 92–167) is extracted
into `private void ExecuteFollowers(...)`. After extraction, `Execute()` CYC=7 and
`ExecuteFollowers()` CYC=7 (including DW-B115-DIAG loops; core logic is CYC=4).

All seven JS rule scans pass. `Account.Flatten` is confirmed NT8-valid. Three xUnit
`[Fact]` tests cover the true path and both false paths of `NeedsLeaderFallbackFlatten`.
