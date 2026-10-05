# DW-B188-LEADER-STOP-FORWARD-01
## QAll2t / Quick ALL: leaderStop captured but not forwarded to leader ExecuteOne call

**Priority**: P1
**Reported**: B187 post-deploy validation, 2026-07-08
**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**Methods**: `Execute(Instrument)` line 94 and `Execute(List<...>, Instrument)` line 175

---

## Symptom

When the user clicks **BE ALL** first and then **QAll2t** (or Quick ALL):
- PTT-QX-T1 and PTT-QX-T2 submit correctly
- PTT-QX-Stop orders are **NOT submitted** -- brackets have no stop protection

The opposite sequence (QAll2t then BE ALL) works correctly.

Confirmed in `docs/brain/B187/output.md` lines 62-79:
```
[PTT-QX-ALL] CancelPttBeOrders: acc=Sim102 count=4
[PTT-QX-ALL] WaitForPttBeCancelled: acc=Sim102 completed
[PTT-QX-2T-ALL] leader: Sim102 MGC DEC26 qty=4 forcedTargetCount=2
[PTT-QX] stop resolved: 0 on Sim102    <-- BUG: leaderStop=4164.9 was captured but never passed
[PTT-QX] snapshot: 0 cancellable orders for MGC DEC26
PTT-QX-T1 Accepted, PTT-QX-T2 Accepted  <-- targets only, no stop
```

---

## Root Cause

B187 (DW-B187-QX-STOP-SNAP-01) correctly moved `leaderStop = SnapshotStopPrice(...)` BEFORE
`CancelPttBeOrders` in both Execute overloads. The capture is correct.

**The oversight**: `leaderStop` is forwarded to `ExecuteFollowers` but NOT to the leader's
own `ExecuteOne` call. Both overloads call `ExecuteOne` with only 4 arguments, omitting
`leaderStop`, which defaults to 0.

### Execute(List, Instrument) -- confirmed failing path

```csharp
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);  // line 140: captures 4164.9
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);               // line 141: cancels PTT-BE-Stop-*
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);          // line 142: waits

ExecuteOne(acc, pos.Instrument, ticks.t1, forcedTargets);                  // line 175: leaderStop=0 (BUG)
ExecuteFollowers(acc, pos, forcedTargets, ticks, leaderStop);              // line 176: leaderStop=4164.9 (correct)
```

Inside `PttQuickExit.Execute` (called by ExecuteOne):
```csharp
double snapshotStop = ResolveStop(SnapshotStopPrice(leader, instr), leaderStop);
//                    ResolveStop(0,                               0)
//                    = 0
// SubmitStopOrder guard: if (snapshotStop <= 0) return;  --> stop silently skipped
```

After BE ALL, the only Working stop orders are PTT-BE-Stop-*. `CancelPttBeOrders` cancels
them; `WaitForPttBeCancelled` confirms they are terminal. By the time `PttQuickExit.Execute`
calls `SnapshotStopPrice` internally, the order book has no Working/Accepted stops.
`ResolveStop` needs the pre-cancel `leaderStop` as its fallback -- but receives 0.

### Execute(Instrument) -- same structural bug at line 94

The same call `ExecuteOne(acc, pos.Instrument, ticks.t1, targets)` does not pass `leaderStop`.
This is masked when native ATM Stop1 survives (QAll2t first, no BE), but will fail identically
if BE ALL fires first and replaces native stops with PTT-BE-Stop-* before QX runs.

---

## Why BE ALL → QAll2t (no prior BE) works

When no BE orders exist:
- `CancelPttBeOrders` returns 0 (nothing cancelled)
- Native ATM Stop1 is still Working
- `SnapshotStopPrice` inside `PttQuickExit.Execute` finds Stop1 and returns its price
- `ResolveStop(4164.9, 0) = 4164.9` -- stop submitted correctly

The `leaderStop` forwarding gap only surfaces when PTT-BE-Stop-* are the only stops
and are cancelled before `PttQuickExit.Execute` runs its internal snapshot.

---

## Fix Design

Two one-line edits in `PttGlobalQuickExit.cs`. No CYC change. No new methods.

### Fix 1: Execute(Instrument) line 94

```csharp
// BEFORE (bug):
ExecuteOne(acc, pos.Instrument, ticks.t1, targets);

// AFTER (fix):
ExecuteOne(acc, pos.Instrument, ticks.t1, targets, leaderStop: leaderStop);
```

### Fix 2: Execute(List, Instrument) line 175

```csharp
// BEFORE (bug):
ExecuteOne(acc, pos.Instrument, ticks.t1, forcedTargets);

// AFTER (fix):
ExecuteOne(acc, pos.Instrument, ticks.t1, forcedTargets, leaderStop: leaderStop);
```

Note: `skipIfFollower` stays at its default `true` (leader path). Named parameter syntax
used for clarity and to avoid accidental positional misalignment with `skipIfFollower`.

---

## Why This Is Correct

**BE ALL then QAll2t (currently broken)**:
- `leaderStop` = 4164.9 (captured from PTT-BE-Stop-1 before cancel)
- After fix: `ExecuteOne` passes `leaderStop=4164.9` to `PttQuickExit.Execute`
- `ResolveStop(0, 4164.9) = 4164.9` -- stop submitted correctly

**QAll2t then BE ALL (no regression)**:
- `leaderStop` = whatever `SnapshotStopPrice` returns (native Stop1 price, e.g. 4165.0)
- After fix: same value passed; `ResolveStop(4165.0, 4165.0)` or `ResolveStop(4165.0, X)` -- no change in behavior
- If `leaderStop` is 0 (flat, or no stop found): `ResolveStop(own, 0) = own` -- no regression

**DW-B126 compliance**: unaffected -- SnapshotTargetOrders still runs after CancelPttBeOrders.

**DW-B63-01 alignment**: this fix completes the intent of DW-B63-01 (capture leader stop before
cancel) by ensuring the captured value flows all the way to `PttQuickExit.Execute`'s
`ResolveStop` call, not just to `ExecuteFollowers`.

---

## CYC Impact

- Zero new branches introduced
- Pure argument addition to existing call sites
- `Execute(Instrument)`: CYC=8 unchanged
- `Execute(List, Instrument)`: CYC=8 unchanged
- `ExecuteOne`: CYC=2 unchanged
- `PttQuickExit.Execute`: CYC=7 unchanged

---

## Tests Required

| Test ID | Description |
|---------|-------------|
| T_B188_01 | Execute(List,Instrument): leaderStop forwarded to ExecuteOne (named param present at line 175) |
| T_B188_02 | Execute(Instrument): leaderStop forwarded to ExecuteOne (named param present at line 94) |
| T_B188_03 | Execute(List,Instrument): ExecuteFollowers also receives leaderStop (DW-B63-01 unchanged) |
| T_B188_04 | Execute(Instrument): ExecuteFollowers also receives leaderStop (DW-B63-01 unchanged) |

All 4 are structural source-read tests (same pattern as T_B187_01..04). No NT8 runtime needed.

---

## Relationship to B187

B187 (DW-B187-QX-STOP-SNAP-01) fixed the capture order -- necessary but not sufficient.
This DW fixes the forwarding gap -- completes the B187 intent.

B187 did not test that `leaderStop` flows into the leader ExecuteOne call; it only tested
structural order (SnapshotStopPrice line < CancelPttBeOrders line). The structural test passed
but the wiring test was missing. This DW adds that wiring and the corresponding tests.

---

## DNA Rule Check (preview)

| Rule | Check | Expected |
|------|-------|---------|
| JS-021 | No new lock() | PASS -- no locking introduced |
| JS-001 | No new throw | PASS -- no exceptions introduced |
| JS-002 | No return null | PASS -- void call sites |
| JS-033 | No async void | PASS -- synchronous void |
| JS-066 | CYC <= 8 | PASS -- 0 new branches |
| JS-081 | ASCII-only | PASS -- named param label `leaderStop` is ASCII |

---

*Written: 2026-07-08*
*Severity: P1 -- no stop protection when BE ALL precedes QX buttons (regression from B187)*
*Target block: B188*
*Author: copier-spec (direct-repair path, full pipeline to follow)*
