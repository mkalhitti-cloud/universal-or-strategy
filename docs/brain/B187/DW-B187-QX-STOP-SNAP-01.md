# DW-B187-QX-STOP-SNAP-01
## QAll2t / Quick ALL: stop price lost when BE ALL fires before QX

**Priority**: P1
**Reported**: B186 NT8 validation, 2026-07-08
**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**Methods**: `Execute(Instrument)` and `Execute(List<...>, Instrument)`

---

## Symptom

When the user clicks **BE ALL** first and then **QAll2t** (or Quick ALL):
- PTT-QX-T1 and PTT-QX-T2 submit correctly
- PTT-QX-Stop orders are **NOT submitted** -- brackets have no stop protection

The opposite sequence (QAll2t → BE ALL) works correctly.

Confirmed in `docs/brain/B186/output.md` lines 114-130 (Sim102 MGC) and 132-148 (Sim101 MES):
```
[PTT-QX-ALL] CancelPttBeOrders: acc=Sim102 count=4
[PTT-QX-ALL] WaitForPttBeCancelled: acc=Sim102 completed
[PTT-QX] stop resolved: 0 on Sim102       <-- BUG: should be 4176.6 (the PTT-BE-Stop price)
```

---

## Root Cause

In `PttGlobalQuickExit.Execute(Instrument)` (line ~56-61) and
`PttGlobalQuickExit.Execute(List, Instrument)` (line ~140-142):

```csharp
// CURRENT (WRONG ORDER):
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);       // cancels PTT-BE-Stop-* orders
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);  // waits until they are Cancelled
// ...
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
// ^ searches for Working/Accepted StopMarket orders -- finds NONE because PTT-BE-Stop-* are now Cancelled
// ^ returns 0
```

`PttQuickExit.SnapshotStopPrice` looks for orders in `Working || Accepted` state with
`OrderType.StopMarket || OrderType.StopLimit`. After `WaitForPttBeCancelled` completes,
the PTT-BE-Stop-* orders are in `Cancelled` state -- no longer visible to the snapshot.

The original ATM Stop1 was already cancelled by `PttBreakEvenSwap` (which called
`CancelQxBrackets` -- cancels ALL bracket orders including ATM Stop1/Stop2/Target1/Target2).

So after BE ALL: the only stop reference is PTT-BE-Stop-*.
After QX fires CancelPttBeOrders+Wait: that reference is gone.
SnapshotStopPrice returns 0.
`SubmitStopOrder` has guard: `if (snapshotStop <= 0) return;` -- stops are skipped.

---

## Fix Design

Move `leaderStop = PttQuickExit.SnapshotStopPrice(...)` to **before** `CancelPttBeOrders`
in both Execute overloads.

The DW-B126 requirement (cancel PTT-BE-* before SnapshotTargetOrders) is maintained --
only the stop snapshot moves earlier. The target snapshot still runs after cancel.

### Execute(Instrument) fix

```csharp
// FIXED ORDER:
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);  // <-- MOVED UP
// B118 DW-B126: cancel PTT-BE-* BEFORE snapshot to eliminate BE/QX race.
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
// PTT-BE-* are now terminal -- snapshot sees clean order book.
var targets = SnapshotTargetOrders(acc, pos.Instrument);
// leaderStop already captured above (before cancel)
var ticks = ResolveQuickTicks(pos.Instrument);
```

### Execute(List, Instrument) fix

```csharp
// FIXED ORDER:
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);  // <-- MOVED UP
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
var ticks = ResolveQuickTicks(pos.Instrument);
```

---

## Why This Is Correct

**QAll2t → BE ALL (already working)**:
- No PTT-BE-* orders exist when QX fires first
- SnapshotStopPrice before or after CancelPttBeOrders: same result (ATM Stop1 is in Working state)
- No regression

**BE ALL → QAll2t (currently broken)**:
- PTT-BE-Stop-1 and PTT-BE-Stop-2 are Working when QX fires
- SnapshotStopPrice BEFORE cancel: finds PTT-BE-Stop-1, returns its stop price (e.g. 4176.6) -- CORRECT
- SnapshotStopPrice AFTER cancel: finds nothing, returns 0 -- BUG

**DW-B126 compliance**:
- SnapshotTargetOrders still runs AFTER CancelPttBeOrders+Wait
- PTT-BE-Target-* orders will not inflate the target snapshot -- DW-B126 race is still prevented

---

## CYC Impact

- No new branches introduced
- Pure statement reorder -- CYC unchanged
- `Execute(Instrument)`: stays at CYC=8
- `Execute(List, Instrument)`: stays at CYC=8

---

## Tests Required

| Test ID | Description |
|---------|-------------|
| T_B187_01 | BE ALL then QAll2t: snapshotStop > 0 (PTT-QX-Stop submitted) |
| T_B187_02 | QAll2t then BE ALL: snapshotStop > 0 (no regression) |
| T_B187_03 | BE ALL then Quick ALL: snapshotStop > 0 (same root fix, Execute(Instrument) path) |
| T_B187_04 | Quick ALL then BE ALL: snapshotStop > 0 (no regression) |

---

## Pattern Reference

B78 DW-B63-01 established: capture leaderStop BEFORE ExecuteOne cancels leader brackets.
This DW extends that intent: capture leaderStop BEFORE CancelPttBeOrders too (not just before ExecuteOne).

The comment on the existing code says "capture leaderStop BEFORE ExecuteOne cancels leader brackets"
-- that was correct as far as it went, but CancelPttBeOrders is an additional earlier cancel that
was added later (B118 DW-B126) without updating the leaderStop capture order.

---

## B186 Connection

This bug was not introduced by B186. It pre-exists (the `leaderStop` capture order predates B186).
B186 exposed it by adding instrument filtering -- the instrument-scoped testing made the
BE ALL → QAll2t sequence testable in isolation for the first time.

DW-B186-DIAG-CLEANUP-01 (B187 deferred item) is unrelated to this bug.

---

*Written: 2026-07-08*
*Severity: P1 -- no stop protection when BE ALL precedes QX buttons*
*Target block: B187*
