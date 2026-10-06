# B187 Architecture Plan
## ptt-architect Phase 1

**Date**: 2026-07-08
**Author**: ptt-architect
**Epic**: B187 — QX Stop Snapshot Fix + DIAG Cleanup + CS0067 Suppress
**Baseline**: 9e5d949f (B186-T3, last clean commit, NT8 compile verified)
**Status**: PLAN_COMPLETE

---

## 1. LANE-SPLIT GATE

| Question | Answer | Rationale |
|----------|--------|-----------|
| Q1. Same method or within 50 lines? | **YES** | T1 and T2 both modify `Execute(Instrument)`. T1 reorders lines ~56-62; T2 removes lines ~76-78 in the same method body (~20 lines apart). Same method = within 50 lines. |
| Q2. Fix B design depends on Fix A final design? | NO | T2 removes the `LogLeaderDiag` call regardless of where `leaderStop` appears. T2's design is not altered by T1's reorder. |
| Q3. Each fix has standalone value if the other is blocked? | YES | T1 alone closes a P1 bug (stop price = 0). T2 alone reduces log noise. T3 alone suppresses a build warning. |
| Q4. Each fix has an independent SIM verification path? | YES | T1: SIM sequence BE ALL → QAll2t, verify PTT-QX-Stop submitted. T2: grep absence of `DW-B115-DIAG` strings + dotnet build. T3: dotnet build 0 CS0067 warnings. |

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

Rationale: Q1 = YES fails the LANES condition. T1 and T2 touch the same method.
Execution order: T1 → T2 → T3 (sequential; T1 + T2 in same file, T3 in separate file).

---

## 2. Component List and Change Summary

### Files in Scope

| File | Tickets | Nature of Change |
|------|---------|-----------------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | T1, T2 | Statement reorder (T1) + deletion (T2) |
| `src/PropTraderTools/CopyEngine.cs` | T3 | Pragma wrap only |
| `src/PropTraderTools/CopyEngineTests.cs` | T1 (tests) | 4 new structural [Fact] tests |

### Deferred Items Closed by B187

| DW ID | Priority | Description | Closed By |
|-------|----------|-------------|-----------|
| DW-B187-QX-STOP-SNAP-01 | P1 | QX stop price = 0 when BE ALL fires before QX | T1 |
| DW-B186-DIAG-CLEANUP-01 | P3 | Remove [DW-B115-DIAG] log blocks from PttGlobalQuickExit | T2 |
| DW-B186-WARN-01 | P3 | CS0067 PendingBeArmed false-positive suppression | T3 |

---

## 3. Ticket T1 — DW-B187-QX-STOP-SNAP-01 [P1]

### Root Cause (confirmed from source)

`PttGlobalQuickExit.Execute(Instrument)` (line ~56) and `Execute(List<(double,int)>, Instrument)` (line ~140) both:
1. Call `CancelPttBeOrders(acc, pos.Instrument)` — cancels PTT-BE-Stop-* and PTT-BE-Target-* orders
2. Wait with `WaitForPttBeCancelled(...)` — brings PTT-BE-* to Cancelled state
3. **THEN** call `PttQuickExit.SnapshotStopPrice(acc, pos.Instrument)` — which scans for Working/Accepted StopMarket orders

When BE ALL fires first, the only stop reference is PTT-BE-Stop-*. After step 2, those orders are in Cancelled state, invisible to SnapshotStopPrice. It returns 0. `SubmitStopOrder` has guard `if (snapshotStop <= 0) return;` — stops are not submitted.

### Fix Design

Move `double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument)` to BEFORE `CancelPttBeOrders` in both overloads. The stop reference is captured while PTT-BE-Stop-* orders are still in Working state.

**DW-B126 constraint maintained**: `SnapshotTargetOrders` remains AFTER `CancelPttBeOrders + WaitForPttBeCancelled` in `Execute(Instrument)`. Only the stop snapshot moves earlier; the target snapshot is unaffected.

### Method-Level Change Descriptions

#### `Execute(Instrument)` — current lines ~56-62 (WRONG ORDER):

```csharp
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
// PTT-BE-* are now terminal -- snapshot sees clean order book.
var targets = SnapshotTargetOrders(acc, pos.Instrument);
// B78 DW-B63-01: snapshot leader stop BEFORE ExecuteOne cancels leader brackets.
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
var ticks = ResolveQuickTicks(pos.Instrument);
```

Fixed order:

```csharp
// B78 DW-B63-01 + DW-B187: capture leaderStop BEFORE CancelPttBeOrders.
// PTT-BE-Stop-* are the stop reference when BE ALL has fired first; cancel destroys them.
// SnapshotTargetOrders still runs after cancel (DW-B126 race prevention maintained).
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
// B118 DW-B126: cancel PTT-BE-* BEFORE SnapshotTargetOrders to eliminate BE/QX target race.
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
// PTT-BE-* are now terminal -- target snapshot sees clean order book.
var targets = SnapshotTargetOrders(acc, pos.Instrument);
var ticks = ResolveQuickTicks(pos.Instrument);
```

XML summary update: update the `B78 DW-B63-01` note to reference DW-B187 in the method's doc comment.

#### `Execute(List<(double Price, int Qty)>, Instrument)` — current lines ~140-143 (WRONG ORDER):

```csharp
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
var ticks = ResolveQuickTicks(pos.Instrument);
```

Fixed order:

```csharp
// B78 DW-B63-01 + DW-B187: capture leaderStop BEFORE CancelPttBeOrders.
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
var ticks = ResolveQuickTicks(pos.Instrument);
```

Note: `Execute(List, Instrument)` uses `forcedTargets` — it has no `SnapshotTargetOrders` call. DW-B126 is N/A for this overload; forcedTargets are passed by the caller and never derived from acc.Orders.

### DW-B126 Compliance Statement

`SnapshotTargetOrders(acc, pos.Instrument)` in `Execute(Instrument)` continues to execute AFTER `CancelPttBeOrders + WaitForPttBeCancelled`. Only `SnapshotStopPrice` moves before the cancel sequence. PTT-BE-Target-* orders are cancelled and terminal before the target snapshot runs — the DW-B126 race between BE targets and QX targets is fully prevented.

### CYC Impact — T1

| Method | Before | After | Delta |
|--------|--------|-------|-------|
| `Execute(Instrument)` | 8 | 8 | 0 — pure statement reorder, no new branches |
| `Execute(List, Instrument)` | 8 | 8 | 0 — pure statement reorder, no new branches |

No new conditional branches in either overload. CYC unchanged.

---

## 4. Ticket T2 — DW-B186-DIAG-CLEANUP-01 [P3]

### Method-Level Change Descriptions

#### `Execute(Instrument)` — remove 3 lines at ~line 76-78

Remove the 2-line comment + call:
```csharp
// DW-B115-DIAG: log leader per-target qty split for comparison against followers.
// Remove when DW-B115 root cause confirmed and fix applied.
LogLeaderDiag(acc, targets, pos.Quantity);
```

No other changes to this method in T2.

#### `ExecuteFollowers(...)` — remove DIAG block at ~lines 208-244

Remove the full DIAG block (after T1 is applied, line numbers will shift slightly):
```csharp
// DW-B115-DIAG: log follower position qty + per-target qty split.
// Determines: (A) whether follower ATM has same qty split as leader,
// or (B) whether followerTargets is empty/partial (DW-B120 async lag).
// Remove when DW-B115 root cause confirmed and fix applied.
int _fPosQty = 0;
foreach (NinjaTrader.Cbi.Position _p in follower.Positions) // (4)
{
    if (
        _p != null
        && _p.Instrument != null
        && _p.Instrument.FullName == pos.Instrument.FullName
    ) // (5)
    {
        _fPosQty = _p.Quantity;
        break;
    }
}
{
    var _sb = new System.Text.StringBuilder(
        "[DW-B115-DIAG] follower targets: "
    );
    _sb.Append(follower.Name);
    _sb.Append(" count=");
    _sb.Append(followerTargets.Count);
    _sb.Append(" posQty=");
    _sb.Append(_fPosQty);
    for (int _i = 0; _i < followerTargets.Count; _i++) // (6)
    {
        _sb.Append(" T");
        _sb.Append(_i + 1);
        _sb.Append("=");
        _sb.Append(followerTargets[_i].Qty);
    }
    NinjaTrader.Code.Output.Process(
        _sb.ToString(),
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
}
```

`_fPosQty` is referenced only in the DIAG block — no other usages. Safe to delete without replacement.

Also update `ExecuteFollowers` XML summary comment:

Old: `CYC=7: rule null-check(1), follower foreach(2), follower null continue(3), follower position foreach DIAG(4), DIAG _p null/instr guard(5), DIAG for-loop(6), delegate(7).`

New: `CYC=4: rule null-check(1), follower foreach(2), follower null continue(3), delegate(4).`

#### `LogLeaderDiag` — delete entire private static method (~lines 490-519)

Delete the method and its XML `<summary>` block entirely:
```csharp
/// <summary>
/// Build and append DW-B115-DIAG log string for leader targets.
/// ...
/// </summary>
private static void LogLeaderDiag(
    NinjaTrader.Cbi.Account acc,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    int posQty
)
{
    ...
}
```

This method has no callers after T2 removes the only call site in `Execute(Instrument)`. Deletion eliminates the dead method entirely.

### CYC Impact — T2

| Method | Before T2 | After T2 | Delta |
|--------|-----------|----------|-------|
| `Execute(Instrument)` | 8 (after T1) | 8 | 0 — LogLeaderDiag call is a statement, not a branch |
| `ExecuteFollowers` | 7 | 4 | -3 — removes branches (4) foreach, (5) guard, (6) for-loop |
| `LogLeaderDiag` | 2 | DELETED | method removed entirely |

CYC constraint satisfied for all remaining methods (≤ 8). ✓

---

## 5. Ticket T3 — DW-B186-WARN-01 [P3]

### Method-Level Change Description

#### `CopyEngine.cs` — wrap `PendingBeArmed` event declaration (line 423)

Current:
```csharp
internal event Action<string, string> PendingBeArmed; // HOTFIX-BEALL-SYNC-01: instr, accName
```

After T3:
```csharp
#pragma warning disable CS0067 // DW-B186-WARN-01: Roslyn false positive -- event IS subscribed by TradeCopierPanel
internal event Action<string, string> PendingBeArmed; // HOTFIX-BEALL-SYNC-01: instr, accName
#pragma warning restore CS0067
```

CS0067 is a Roslyn false positive. The event IS subscribed by `TradeCopierPanel.cs` (lines 771-772) and raised via the `DisarmPendingBe/RaiseBeAllDisarmed` path. Roslyn does not track event usage through external subscribers in a separate class.

### CYC Impact — T3

No methods modified. Pragma directives are compile-time only. CYC unchanged throughout CopyEngine.cs.

---

## 6. NinjaTrader 8 API Usage

| API | Usage | Context | Constraint |
|-----|-------|---------|------------|
| `Account.All` | Read in foreach loops | Execute(Instrument), Execute(List,Instrument) | NT8-021: safe — called from UI thread after Loaded handler |
| `PttQuickExit.SnapshotStopPrice(acc, instr)` | Internal PTT method (not NT8 public API) | Execute(Instrument), Execute(List,Instrument) — moved before CancelPttBeOrders | Returns `double`; scans acc.Orders for Working/Accepted StopMarket orders |
| `acc.Orders` | Iterated in SnapshotTargetOrders, SnapshotStopPrice | Existing; unchanged in B187 | UI thread safe |
| `acc.Positions` | Iterated in Execute loops | Existing; unchanged in B187 | UI thread safe |
| `NinjaTrader.Code.Output.Process` | Logging | Existing; DIAG calls removed in T2 | UI thread |

**NT8 Key Facts (B187-relevant):**
- `AtmStrategyChangeStopTarget()` — StrategyBase-only. NOT used in B187. ✓
- `AtmStrategyCreate()` — StrategyBase-only. NOT used in B187. ✓
- `Account.Change()` — NOT used in B187. ✓
- `Account.Cancel() + CreateOrder()` — invoked internally by `CancelPttBeOrders` (unchanged in B187). ✓

---

## 7. Threading Model

All three tickets operate exclusively on the UI thread:

- `Execute(Instrument)` and `Execute(List,Instrument)` are invoked from button click handlers in `TradeCopierPanel` (UI thread).
- T1 reorders statements within the same synchronous UI-thread execution context. No threading model change.
- T2 removes DIAG blocks that were also on the UI thread. No threading model change.
- T3 adds `#pragma` directives — compile-time only, zero runtime threading effect.

No `Dispatcher.InvokeAsync` changes. No `ConcurrentQueue` changes. No `lock()` anywhere (JS-021). ✓

---

## 8. Test Plan

### T1 — 4 Structural Tests in `CopyEngineTests.cs`

All four are `[Fact]` source-text ordering assertions. The test reads the source of `PttGlobalQuickExit.cs`, extracts the relevant method body, and verifies line ordering by scanning for specific string patterns.

| Test ID | Method | Assertion | Guard |
|---------|--------|-----------|-------|
| `T_B187_01` | `Execute(Instrument)` | Line index of `SnapshotStopPrice` < line index of `CancelPttBeOrders` | Confirms leaderStop captured before cancel |
| `T_B187_02` | `Execute(List, Instrument)` | Line index of `SnapshotStopPrice` < line index of `CancelPttBeOrders` | Same fix in the List overload |
| `T_B187_03` | `Execute(Instrument)` | Line index of `SnapshotTargetOrders` > line index of `CancelPttBeOrders` | DW-B126 guard — target snapshot still after cancel |
| `T_B187_04` | `Execute(List, Instrument)` | Source body does NOT contain `SnapshotTargetOrders` | DW-B126 guard — List overload correctly omits target snapshot (uses forcedTargets) |

**Implementation pattern** (structural test, xUnit [Fact]):
- Read `PttGlobalQuickExit.cs` as string array of lines
- Locate each method body by scanning for the method signature
- Within each method body, find the first line matching the search string
- Assert ordering constraint using `Assert.True(lineA < lineB)` or `Assert.DoesNotContain`

Test file location: `src/PropTraderTools/CopyEngineTests.cs`
Test class: existing test class (append to existing file)
Test naming convention: `[Fact] public void T_B187_XX_<description>()`

### T2 — 0 New Tests

Pure deletion. No new test coverage required.
Engineer MAY add a negative structural test to confirm `LogLeaderDiag` is absent from the compiled file — but this is optional, not required.

### T3 — 0 New Tests

AC for T3 is `dotnet build 0 warnings`. SCAN-07 covers this.

---

## 9. Execution Order

```
T1: PttGlobalQuickExit.cs — reorder leaderStop before CancelPttBeOrders (both overloads)
    + update XML summary comment in Execute(Instrument) to reference DW-B187
    + 4 structural tests in CopyEngineTests.cs
    -> VERIFY_PASS (T1) gate

T2: PttGlobalQuickExit.cs — remove all [DW-B115-DIAG] artifacts
    + update ExecuteFollowers CYC comment 7 -> 4
    -> VERIFY_PASS (T2) gate

T3: CopyEngine.cs — wrap PendingBeArmed with #pragma warning disable/restore CS0067
    -> VERIFY_PASS (T3) gate
```

T1 must complete before T2 begins (same file; T1 modifies lines that T2's removals are adjacent to — sequential prevents merge conflicts).
T3 is independent and could run in parallel with T1+T2, but SINGLE-PIPELINE mandates sequential execution.

---

## 10. 7-Scan Checklist Reference

Every ticket must include and pass all 7 scans before VERIFY_PASS:

| Scan | Check | Applies To |
|------|-------|-----------|
| SCAN-01 | No new `lock()` in modified files | T1, T2, T3 |
| SCAN-02 | No new `return null` in modified files | T1, T2, T3 |
| SCAN-03 | No new `async void` in modified files | T1, T2, T3 |
| SCAN-04 | CYC manual count per modified method: Execute(Instrument)=8, Execute(List,Instrument)=8, ExecuteFollowers=4 | T1, T2 |
| SCAN-05 | No non-ASCII characters in new string literals or comments | T1, T2, T3 |
| SCAN-06 | No new `throw new` in hot paths | T1, T2, T3 |
| SCAN-07 | `dotnet build` exits 0 errors, 0 warnings | T1, T2, T3 (T3 specifically eliminates CS0067) |

---

## 11. DNA Constraints Summary

| Rule | Constraint | B187 Status |
|------|-----------|-------------|
| JS-001 | No `throw new` in hot paths | ✓ No new exceptions |
| JS-002 | No `return null` | ✓ No return null introduced |
| JS-021 | No `lock()` | ✓ No lock anywhere in changes |
| JS-033 | No `async void` | ✓ All modified methods are synchronous void |
| JS-066 | CYC <= 8 per method | ✓ Execute=8, ExecuteFollowers=4 after T2 |
| JS-081 | ASCII-only identifiers and string literals | ✓ All new comments/strings are ASCII |
| NT8-021 | Account.All safe after Loaded handler on UI thread | ✓ Unchanged threading model |

---

## 12. Open Deferred Items (Carried Forward — Not Addressed by B187)

| DW ID | Priority | Description |
|-------|----------|-------------|
| DW-B183-05 | P1 | RULES_CATALOG.md truncated at JS-041 |
| DW-B183-03 | P1 | IsLaneEEligible CYC=8 frozen |
| DW-B185-BE-ALL-SCOPE-01 | P2 | Spec HTML update (B185 instrument scope) |
| DW-B183-01 | P2 | xUnit tests for Slate Gate methods |
| DW-B183-02 | P2 | xUnit test for FlatGuardLoop |
| DW-B184-04 | P3 | Retained for traceability (superseded by DW-B186-WARN-01, closed by T3) |

---

*Written by: ptt-architect*
*Epic: B187*
*Phase: 1 (Architecture Plan)*
*Return: PLAN_COMPLETE*
