# B191 Architecture Plan
# QAll2t Per-Account Qty Fix (formalize) + DW-B187-01 Close

**Epic**: B191
**Author**: ptt-architect
**Phase**: 1 (Architecture)
**Status**: PLAN_COMPLETE
**Baseline commit**: 65c2b0b2 (B190-T1..T6)
**Fix commit**: 365b8779 (B191-DIRECT — T1 already live)

---

## LANE-SPLIT GATE RESULT

```
Q1. Same method or within 50 lines?
    T1 in Execute() lines 177-181, T3 in ExecuteFollowers() lines 216-221.
    Different methods, >50 lines apart. Q1=NO

Q2. Fix B design depends on Fix A final design?
    T3 (restore followerPosQty) does not depend on T1 (perAccTargets per-account loop). Q2=NO

Q3. Each fix has standalone value if the other is blocked?
    T1 standalone: yes (per-account qty correctness).
    T3 standalone: yes (closes DW-B187-01 dead path). Q3=YES

Q4. Each fix has an independent SIM verification path?
    T1: Build2TargetList arithmetic tests.
    T3: ResolveFollowerTargets behavior test (B116 filter). Q4=YES

LANE-SPLIT GATE RESULT: SINGLE-PIPELINE (same .cs file -- merge conflict prevention)
Both T1 and T3 edit PttGlobalQuickExit.cs. Per Parallel Lane Protocol:
"Two lanes writing to the same .cs file = merge conflict. Merge into one sequential lane first."
T1 is already committed (365b8779). T3 applies sequentially on top.
```

---

## 1. Root Cause Description

### DW-B191-QTY-01 — QAll2t Passed Leader Qty to All Accounts

**Affected method**: `Execute(List<(double Price, int Qty)> forcedTargets, Instrument instrument)`
**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`

**Trigger path**:
```
OnInstrQAll2tClick (TradeCopierPanel.cs:1743)
  -> TryResolve2TargetContext() -> Build2TargetList(leaderPos.Quantity)  // e.g. qty=20 for PAAPEX10
  -> new PttGlobalQuickExit().Execute(targets, _instrument)
     -> foreach (Account acc in Account.All)
          -> foreach (Position pos in acc.Positions)
               -> ExecuteOne(acc, ..., forcedTargets, ...)   // BUG: forcedTargets has LEADER qty
               -> ExecuteFollowers(acc, pos, forcedTargets, ...)  // BUG: same leader-sized list
```

**The bug**: `TryResolve2TargetContext()` builds `forcedTargets` using the LEADER account's
position quantity (e.g. 20 for PAAPEX10). This list is then passed verbatim as the targets
argument to every account in the `foreach (Account acc in Account.All)` loop. A PA-01 account
holding 5 contracts received `{T1=10, T2=10}` targets — sized for a 20-contract account.

**Concrete example (live incident)**:
- PAAPEX10 leader: 20 contracts → `forcedTargets = {T1=10, T2=10}` (correct for PAAPEX10)
- PA-01 follower: 5 contracts → received `{T1=10, T2=10}` (WRONG — should be `{T1=3, T2=2}`)

---

## 2. T1 — Fix Rationale: perAccTargets Per-Account Loop

**Status**: COMMITTED at 365b8779. No new code required for Ph4a.

**Change location**: `Execute(List, Instrument)` — immediately after the `NeedsLeaderFallbackFlatten`
guard block, before `ExecuteOne` call.

**Before (pre-365b8779)**:
```csharp
ExecuteOne(acc, pos.Instrument, ticks.t1, forcedTargets, leaderStop: leaderStop);
ExecuteFollowers(acc, pos, forcedTargets, ticks, leaderStop);
```

**After (committed at 365b8779, lines 177-182)**:
```csharp
// B191-DIRECT: rebuild 2-target list from this account's actual position qty.
// forcedTargets was built from the leader panel's qty and must not be used verbatim
// for accounts with different contract sizes (e.g. PA-01 at 5 vs PAAPEX10 at 20).
var perAccTargets = TradeCopierPanel.Build2TargetList(pos.Quantity);
ExecuteOne(acc, pos.Instrument, ticks.t1, perAccTargets, leaderStop: leaderStop);
ExecuteFollowers(acc, pos, perAccTargets, ticks, leaderStop);
```

**Why `Build2TargetList(pos.Quantity)` is correct**:
- `pos` is the current account's position in the inner loop — `pos.Quantity` is THIS account's
  actual open contract count, not the leader panel's qty.
- `Build2TargetList(int totalQty)` at `TradeCopierPanel.cs:1241` is pure arithmetic:
  `t1Qty = (totalQty + 1) / 2; t2Qty = totalQty - t1Qty;` (ceiling-split, no NT8 dependency).
- Result: each account receives targets sized for its own position. Sum invariant preserved
  (`t1Qty + t2Qty == totalQty` always).

**Role of `forcedTargets` after T1**:
- `forcedTargets` remains the method parameter for two purposes only:
  1. Guard check at line 120: `if (forcedTargets == null || forcedTargets.Count < 2)` — validates
     caller intent before the loop begins (caller must have a valid target template).
  2. Diagnostic log at line 154: `"forcedTargets.Count"` — records caller-side sizing for audit.
- `forcedTargets` is NOT passed to `ExecuteOne` or `ExecuteFollowers` after T1.

**CYC impact on Execute()**: 0 new branches.
`var perAccTargets = TradeCopierPanel.Build2TargetList(pos.Quantity)` is a variable assignment
(straight-line statement). No conditional. No McCabe branch added.
CYC of Execute() = 8 (documented in file header comment), unchanged after T1.

**DW closed**: DW-B191-QTY-01 (new item, root cause of the Oct 6 live incident).

---

## 3. T2 — Test Coverage Strategy (pure arithmetic, no NT8 runtime)

**File**: `src/PropTraderTools/Tests/B191Tests.cs` (NEW)
**Framework**: xUnit only. No NT8 runtime. No `[Fact(Skip=...)]` on any test.

### Why no NT8 runtime required

`Build2TargetList(int totalQty)` at `TradeCopierPanel.cs:1241` is `internal static` and
performs pure integer arithmetic with no NT8 API calls. It can be invoked directly from
a test that references the same assembly. No `Account`, `Instrument`, or NinjaTrader
namespace objects are required.

The `Execute(List, Instrument)` overload existence check (T2-4) uses reflection to confirm
the method signature is present — no invocation, no NT8 runtime.

### Test oracles (exact expected values)

| Test | Input | Expected | Formula |
|------|-------|----------|---------|
| T2-1 | `Build2TargetList(5)` | `T1=3, T2=2` | `(5+1)/2=3`, `5-3=2` |
| T2-2 | `Build2TargetList(20)` | `T1=10, T2=10` | `(20+1)/2=10`, `20-10=10` |
| T2-3 | `Build2TargetList(5).Sum(t => t.Qty)` | `== 5` | sum invariant: no qty lost |
| T2-4 | reflection on `PttGlobalQuickExit` | overload `Execute(List, Instrument)` exists as `internal` | structural check |
| T2-5 | `Build2TargetList(1)` | `T1=1, T2=0` | `(1+1)/2=1`, `1-1=0` (boundary: single contract) |

### Arithmetic verification

- `Build2TargetList(5)`:  `t1=(5+1)/2=6/2=3`, `t2=5-3=2` → `{3,2}` ✓
- `Build2TargetList(20)`: `t1=(20+1)/2=21/2=10` (integer division), `t2=20-10=10` → `{10,10}` ✓
- `Build2TargetList(1)`:  `t1=(1+1)/2=2/2=1`, `t2=1-1=0` → `{1,0}` ✓
- Sum invariant: `t1 + t2 = ((totalQty+1)/2) + (totalQty - (totalQty+1)/2) = totalQty` ✓ (by construction)

### T2-4 reflection test rationale

Confirms the `internal void Execute(List<(double Price, int Qty)>, Instrument)` overload exists
and has not been accidentally renamed or removed. Uses `typeof(PttGlobalQuickExit).GetMethod(
"Execute", BindingFlags.NonPublic | BindingFlags.Instance, ...)` with parameter type array
`[typeof(List<(double, int)>), typeof(Instrument)]`. Asserts `mi != null`.

---

## 4. T3 — Fix Rationale: Restore followerPosQty (closes DW-B187-01)

**Affected method**: `ExecuteFollowers(Account, Position, List, (int t1, int t2), double)`
**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**Current state (pre-T3)**: hardcoded literal `0` passed as `followerPosQty` to `ResolveFollowerTargets`

### DW-B187-01 history

B187-T2 deleted the `[DW-B115-DIAG]` block in `ExecuteFollowers`. That block computed
`_fPosQty` (follower position qty) via a foreach over `follower.Positions`. The B187 architect's
plan stated `_fPosQty` was only used within the DIAG block — this was incorrect: `_fPosQty` was
also passed as arg 3 (`followerPosQty`) to `ResolveFollowerTargets` immediately after.

The B187 engineer made the minimal fix: replaced `_fPosQty` with literal `0`.

With `followerPosQty=0`, `ResolveFollowerTargets` short-circuits at:
```csharp
if (leaderTargets.Count == 0 || followerPosQty <= 0) return followerSnapshot;
```
bypassing `ScaleLeaderTargets`. The path was confirmed behaviorally safe at the time (the
dominant path — non-empty `followerSnapshot` — returns before this guard anyway). But it left
`ScaleLeaderTargets` permanently unreachable via the normal code path.

### T3 change

**Before (current, lines 216-221)**:
```csharp
followerTargets = ResolveFollowerTargets(
    followerTargets,
    targets,
    0,
    pos.Quantity
);
```

**After (T3 target)**:
```csharp
// B191-T3: restore followerPosQty from live position (DW-B187-01 fix).
// With followerPosQty>0, ScaleLeaderTargets fires when follower has no snapshot.
// Zero fallback preserved: flat follower -> ScaleLeaderTargets skipped -> empty list correct.
int followerPosQty = CopyEngine.Instance?.FindPositionPublic(follower, pos.Instrument)?.Quantity ?? 0;
followerTargets = ResolveFollowerTargets(
    followerTargets,
    targets,
    followerPosQty,
    pos.Quantity
);
```

### Why `FindPositionPublic` is the correct data source

- `FindPositionPublic(Account, Instrument)` at `CopyEngine.cs:5539` is `internal Position` — the
  same internal access level used by `PttBreakEvenSwap.cs:53` and `TradeCopierPanel.cs:1788`.
  Pattern is established and reviewed.
- It iterates `acc.Positions` (NT8 read-only enumeration, no lock required per JS-021).
- Returns `null` if no matching position — null-safe via `?.Quantity ?? 0` chain.
- `follower` is non-null (guarded by `if (follower == null) continue` at line 207).
- `pos.Instrument` is non-null (guarded by instrument null check at Execute() line 139).

### Why NOT `FindPosition` (private)

`FindPosition` is `private` to `CopyEngine`. `PttGlobalQuickExit` is a separate class and
cannot access it. `FindPositionPublic` is the established `internal` wrapper for exactly
this cross-class access pattern.

**CYC impact on ExecuteFollowers()**: 0 new branches.
`int followerPosQty = ...` is a variable assignment. The `?.` null-conditional operator and
`?? 0` null-coalescing operator are NOT McCabe branches — they are expressions, not conditional
statements. No new code path divergence is introduced.
CYC of ExecuteFollowers() = 4 (documented in file header), unchanged after T3.

**DW closed**: DW-B187-01 (P3, carried from B187 via B188, B189, B190).

---

## 5. DNA Audit

### JS-021: lock() ban

```
PttGlobalQuickExit.cs -- confirmed: zero lock() occurrences.
T1 adds: variable assignment (no lock)
T3 adds: variable assignment using null-safe method call (no lock)
PASS
```

### JS-033: async void ban

```
PttGlobalQuickExit.cs -- confirmed: zero async void occurrences.
T1: no new methods or async patterns
T3: no new methods or async patterns
ExecuteFollowers is synchronous void (not an event handler, not async)
PASS
```

### JS-001: no throw in hot paths

```
T3 null chain: CopyEngine.Instance?.FindPositionPublic(follower, pos.Instrument)?.Quantity ?? 0
  - CopyEngine.Instance: singleton, can be null (shutdown race). ?. propagates null safely.
  - FindPositionPublic: returns null on no-match (pre-existing contract). ?. propagates safely.
  - .Quantity: int (value type). On nullable int, ?. returns int?. ?? 0 unwraps to 0.
  - No NullReferenceException possible. No throw introduced.
PASS
```

### JS-002: no return null (new sites)

```
T1: no new return sites.
T3: no new return sites.
FindPositionPublic null return is a pre-existing contract (CopyEngine.cs:5538 documents it).
PASS
```

### CYC impact analysis

| Method | CYC before B191 | T1 delta | T3 delta | CYC after B191 | Limit |
|--------|----------------|----------|----------|----------------|-------|
| Execute(List, Instrument) | 8 | +0 | n/a | 8 | 8 |
| ExecuteFollowers | 4 | n/a | +0 | 4 | 8 |
| Build2TargetList | 1 | n/a | n/a | 1 | 8 |

### ASCII-only

```
T1 new identifiers: perAccTargets -- ASCII. PASS.
T3 new identifiers: followerPosQty -- ASCII. PASS.
T1 new string literals: none.
T3 new string literals: none (comment text is ASCII-only).
PASS
```

### DateTime.Now ban

```
No DateTime usage introduced in T1 or T3. PASS.
```

### "PTT-" order name prefix

```
Neither T1 nor T3 creates orders. Not applicable. PASS.
```

---

## 6. Risk Analysis

### T1 behavioral verification

T1 changes the targets passed to `ExecuteOne` and `ExecuteFollowers` from `forcedTargets`
(leader-sized, constant across the loop) to `perAccTargets` (per-account sized, rebuilt each
iteration).

**Regression concern**: Could per-account sizing break follower dispatch?

Analysis:
- `ExecuteFollowers(acc, pos, perAccTargets, ticks, leaderStop)` receives `perAccTargets` as
  `targets`. Inside `ExecuteFollowers`, `targets` is passed to `ResolveFollowerTargets` as
  `leaderTargets` (arg 2). `ResolveFollowerTargets` uses `leaderTargets` only for:
  (a) count comparison with `followerSnapshot.Count` — a structural check, not a qty check.
  (b) `ScaleLeaderTargets(leaderTargets, followerPosQty, leaderPosQty)` — scales by ratio.
- The per-account `perAccTargets` has `Count == 2` always (same structure as before). The
  count comparison behavior is unchanged.
- B116 tests (ResolveFollowerTargets behavior) are unaffected — they test the function's
  internal logic, not the qty values in targets.

**Verdict**: Regression risk NONE. The targets structure (count=2, price=0.0 placeholders)
is identical before and after T1; only the Qty fields differ by design.

### T3 behavior change: follower has no snapshot AND has a non-flat position

This is the only behavioral change introduced by T3:

| Scenario | Pre-T3 | Post-T3 | Correct? |
|----------|--------|---------|---------|
| follower has snapshot (count matches leader count) | ResolveFollowerTargets returns snapshot (line 554) | SAME (first guard returns early before followerPosQty check) | n/a |
| follower snapshot is empty, follower IS flat | followerPosQty=0 → short-circuit → return empty snapshot | followerPosQty=0 (FindPositionPublic returns null → ?? 0) → same short-circuit | YES — unchanged |
| follower snapshot is empty, follower has OPEN POSITION | followerPosQty=0 → short-circuit → ScaleLeaderTargets SKIPPED (dead path) | followerPosQty=follower.Quantity → ScaleLeaderTargets FIRES | YES — CORRECT (dead path now active) |

**T3 regression risk: LOW.** The newly activated path (`ScaleLeaderTargets`) was dead code
confirmed in B187 — not a previously-passing path that could break. The flat-follower path
(the only other path via the guard) is entirely unchanged.

**B116 test impact**: B116 tests exercise `ResolveFollowerTargets` with explicit `followerPosQty`
parameters passed directly to the function. They do not test `ExecuteFollowers` end-to-end and
are unaffected by the local variable change in T3.

---

## 7. DW Items Closed by B191

### DW-B191-QTY-01 (NEW)

```
Priority: P0 (live incident — root cause)
Description: QAll2t (OnInstrQAll2tClick) passed forcedTargets — built from the leader panel's
  position qty — verbatim to all accounts in the Execute() per-account loop. Accounts with
  different contract sizes (e.g. PA-01 at 5c vs PAAPEX10 at 20c) received incorrectly-sized
  targets.
Closed by: T1 (commit 365b8779, PttGlobalQuickExit.cs lines 177-182)
Closing artifact: ticket-1-completion.md + ticket-1-verification.md
```

### DW-B187-01 (P3, carried from B187)

```
Priority: P3
Description: followerPosQty=0 hardcode in ExecuteFollowers caused ResolveFollowerTargets to
  permanently short-circuit, leaving ScaleLeaderTargets as dead code. The correct behavior
  is to pass the follower's actual live position qty so ScaleLeaderTargets fires when
  follower has an open position but no snapshot (the original DW-B124 intent).
Closed by: T3 (PttGlobalQuickExit.cs ExecuteFollowers, one new line + one changed argument)
Closing artifact: ticket-3-completion.md + ticket-3-verification.md
```

---

## 8. DW Items Carried Forward

### DW-B183-03: IsLaneEEligible CYC=8 Frozen

```
Priority: P1
Description: IsLaneEEligible in CopyEngine.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to IsLaneEEligible without prior helper extraction is a P1 JS-066 violation.
  Not modified in B183-B191.
Blocking: No (current CYC is at limit, not over; blocks only future branch additions)
Target Block: When Lane E is next modified
Status: OPEN (carried forward from B183 via B184, B185, B186, B187, B188, B189, B190, B191)
Owner: Director
```

---

## 9. Component List

| Component | File | Role in B191 |
|-----------|------|-------------|
| `PttGlobalQuickExit` | `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | T1 (Execute, committed), T3 (ExecuteFollowers, new edit) |
| `TradeCopierPanel.Build2TargetList` | `src/PropTraderTools/TradeCopierPanel.cs` | Existing static method, called by T1 fix. No change. |
| `CopyEngine.FindPositionPublic` | `src/PropTraderTools/CopyEngine.cs` | Existing internal method, called by T3 fix. No change. |
| `B191Tests` | `src/PropTraderTools/Tests/B191Tests.cs` | T2: new xUnit test class (5 [Fact] methods) |

---

## 10. Method Signatures

**Unchanged signatures (context only)**:

```csharp
// PttGlobalQuickExit.cs:114
internal void Execute(
    System.Collections.Generic.List<(double Price, int Qty)> forcedTargets,
    Instrument instrument
)

// PttGlobalQuickExit.cs:194
private void ExecuteFollowers(
    Account acc,
    Position pos,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    (int t1, int t2) ticks,
    double leaderStop
)

// PttGlobalQuickExit.cs:537
internal static System.Collections.Generic.List<(double Price, int Qty)> ResolveFollowerTargets(
    System.Collections.Generic.List<(double Price, int Qty)> followerSnapshot,
    System.Collections.Generic.List<(double Price, int Qty)> leaderTargets,
    int followerPosQty,
    int leaderPosQty
)

// TradeCopierPanel.cs:1241
internal static List<(double Price, int Qty)> Build2TargetList(int totalQty)

// CopyEngine.cs:5539
internal Position FindPositionPublic(Account acc, Instrument instrument)
```

**New B191Tests.cs class**:

```csharp
// B191Tests.cs
public class B191Tests
{
    [Fact] public void Build2TargetList_Qty5_ReturnsT1_3_T2_2()
    [Fact] public void Build2TargetList_Qty20_ReturnsT1_10_T2_10()
    [Fact] public void Build2TargetList_Qty5_SumEqualsInput()
    [Fact] public void Execute_ListInstrument_Overload_ExistsAsInternal()
    [Fact] public void Build2TargetList_Qty1_ReturnsT1_1_T2_0()
}
```

---

## 11. NT8 API Usage Summary

| API | Access Level | B191 Usage | AddOn-safe? |
|-----|-------------|-----------|-------------|
| `Account.All` | NT8 static | Pre-existing in Execute() — NOT modified by B191 | YES |
| `acc.Positions` | NT8 instance | Inside `FindPositionPublic` — pre-existing, NOT modified | YES |
| `Account.CreateOrder` | NT8 instance | In `ExecuteOne` — NOT modified by B191 | YES |
| `AtmStrategyCreate` | NT8 StrategyBase ONLY | NOT used anywhere in B191 | n/a |
| `AtmStrategyChangeStopTarget` | NT8 StrategyBase ONLY | NOT used anywhere in B191 | n/a |

---

## 12. Pipeline Summary

| Ticket | File(s) | Ph4a Action | Test Gate |
|--------|---------|------------|----------|
| T1 | PttGlobalQuickExit.cs (committed) | Write ticket-1-completion.md only | Build 0E 0W; SCAN-01/02 pass |
| T2 | B191Tests.cs (new) | Create test file; dotnet test | 5 passed, 0 failed, 0 skipped |
| T3 | PttGlobalQuickExit.cs (new edit) | Add 1 line + change 1 arg; dotnet build + B116 filter | Build 0E 0W; B116 all pass; ptt-sync 19 linked |

---

*Author: ptt-architect*
*Epic: B191*
*Phase: 1 (Architecture)*
*Preceding backlog: docs/brain/B190/06-deferred-backlog.md*
