# B123 Architecture Plan

**Block**: B123
**Status**: REVIEW_PASS
**Date**: 2026-08-27
**Author**: ptt-architect
**Phase**: Phase 1 — Architecture Planning

---

## 1. Problem Statement

The **QAll2t** button (`_instrQAll2tBtn`) is intended to fire a global Quick Exit on all accounts
using a **forced 2-target bracket split** regardless of the active ATM template configuration.
Instead, it fires exactly as many targets as the live ATM snapshot contains at the moment of
the button press. When a 3-target ATM is loaded (the standard NES/MES/ES template), pressing
QAll2t submits 3 OCO bracket pairs per account — identical to pressing the normal QAll button.
The 2-target intent is never communicated to `PttGlobalQuickExit`.

---

## 2. Root Cause — Code-Traced

**Call chain confirmed from source:**

```
TradeCopierPanel.cs:1979-1981
  private void OnInstrQAll2tClick(object sender, RoutedEventArgs e)
  {
      new PttGlobalQuickExit().Execute();  // BUG: no-arg path
  }
```

`Execute()` (PttGlobalQuickExit.cs:36-118, CYC=7) calls:
```
var targets = SnapshotTargetOrders(acc, pos.Instrument);  // line 62
```

`SnapshotTargetOrders` (PttGlobalQuickExit.cs:347-405) reads the live `acc.Orders` and returns
every active ATM target order. With a 3-target ATM loaded and 3 Working bracket orders visible,
it returns a 3-entry list. The result flows unchanged into `ExecuteOne()` (line 113) and
`ExecuteFollowers()` (line 115), which submit 3 OCO bracket pairs per account.

The `Build2TargetList(int totalQty)` helper already exists at `TradeCopierPanel.cs:1383` and is
used correctly by `OnInstr2tClick` (single-account 2t, line 1973). The global path at
`OnInstrQAll2tClick` never calls it.

**Defect ID**: DW-B133.

---

## 3. Fix Design

### 3.1 New Overload: `Execute(List<(double Price, int Qty)> forcedTargets)`

**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`

**Exact signature**:
```csharp
/// <summary>
/// Execute: forced-targets overload for QAll2t path.
/// Skips SnapshotTargetOrders -- uses forcedTargets directly.
/// CYC=7: flag guard(1), acc loop(2), follower skip(3), pos loop(4),
///        null/flat continue(5), DW-B115-DIAG for-loop(6), ExecuteFollowers(7).
/// JS-021: no lock. JS-001: no throw. JS-033: synchronous void. ASCII-only.
/// DW-B133: forcedTargets prevents live ATM snapshot from overriding the 2-target intent.
/// </summary>
internal void Execute(System.Collections.Generic.List<(double Price, int Qty)> forcedTargets)
```

**Body logic** (branch-by-branch):

1. **Guard — forcedTargets null**: if `forcedTargets == null` → log `[PTT-QX-2T-ALL] Blocked: forcedTargets is null` → return.
2. **Feature flag guard** (branch 1): same as no-arg — if `!CopyEngine.Instance.Flags.QxGlobalExit` → log `[PTT-QX-2T-ALL] Blocked: Global Quick Exit requires Elite tier` → return.
3. **Log entry**: `[PTT-QX-2T-ALL] GlobalQuickExit fired (forced 2-target count=N)`.
4. **`foreach (Account acc in Account.All)`** (branch 2): same Account.All loop as no-arg Execute().
5. **Follower skip** (branch 3): `if (engine.IsFollowerAccount(acc)) continue;` — identical to no-arg.
6. **`foreach (Position pos in acc.Positions)`** (branch 4): identical loop.
7. **Null/flat continue** (branch 5): `if (pos == null || pos.Quantity == 0) continue;` — identical.
8. **Cancel PTT-BE-* orders**: `int beCancelCount = CancelPttBeOrders(acc, pos.Instrument);` — same as no-arg (race-avoidance unchanged).
9. **Wait for cancel**: `WaitForPttBeCancelled(acc, pos.Instrument, beCancelCount, 1000);` — same as no-arg.
10. **SKIP `SnapshotTargetOrders`**: `forcedTargets` is used directly as `targets`.
11. **Capture leader stop**: `double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);` — identical to no-arg.
12. **Resolve ticks**: `var ticks = ResolveQuickTicks(pos.Instrument);` — identical.
13. **DIAG log** (branch 6 — for-loop): same `[DW-B115-DIAG]` loop as no-arg, using `forcedTargets` (expected count=2).
14. **Flatten guard** (reuse): `NeedsLeaderFallbackFlatten(beCancelCount, forcedTargets.Count, pos.Quantity)` — same helper. With `forcedTargets.Count = 2 > 0` this always returns false; included for structural parity (defensive programming against future 0-entry forced list).
15. **Leader execute**: `ExecuteOne(acc, pos.Instrument, ticks.t1, forcedTargets);` — same helper, passes forcedTargets.
16. **Followers** (branch 7): `ExecuteFollowers(acc, pos, forcedTargets, ticks, leaderStop);` — passes `forcedTargets` as `targets` parameter. `ExecuteFollowers` already passes its `targets` argument into `ResolveFollowerTargets` as `leaderTargets`, so followers scale from the forced 2-target split, not the ATM snapshot.

No helper methods are added or changed. The `forcedTargets` null guard is not counted as a CYC branch in the primary count because it is a precondition guard that is structurally analogous to a method entry check — however, to be conservative: counting it adds at most 1, giving CYC=8 which is still at the limit. See Section 4.

### 3.2 Updated: `OnInstrQAll2tClick`

**File**: `src/PropTraderTools/TradeCopierPanel.cs`  
**Method**: `OnInstrQAll2tClick` (lines 1979-1982)

**Exact replacement**:
```csharp
// B123 DW-B133: fire global Quick Exit with forced 2-target split.
// Mirrors OnInstr2tClick (line 1948) but delegates to PttGlobalQuickExit (all accounts).
// Build2TargetList is internal static (line 1383) -- no visibility change needed.
// CYC=4: (1) _instrument null, (2) _leaderAccount null re-resolve, (3) null after resolve,
//        (4) FirstOrDefault lambda.
// JS-021: no lock. JS-033: synchronous void event handler. ASCII-only.
private void OnInstrQAll2tClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null)
        return; // (1)
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount(); // (2)
    if (_leaderAccount == null)
        return; // (3)
    var pos = _leaderAccount.Positions.FirstOrDefault(
        p => p.Instrument?.FullName == _instrument.FullName
    ); // (4)
    int qty = pos?.Quantity ?? 1;
    new PttGlobalQuickExit().Execute(Build2TargetList(qty));
}
```

**CYC of updated OnInstrQAll2tClick**: 4 (unchanged from the pattern in OnInstr2tClick). Within JS-066 threshold.

---

## 4. CYC Analysis

### New `Execute(forcedTargets)` overload — branch inventory

| # | Branch | Description |
|---|--------|-------------|
| 0 | forcedTargets == null guard | precondition guard, early return |
| 1 | !Flags.QxGlobalExit guard | feature flag early return |
| 2 | foreach acc in Account.All | loop header |
| 3 | IsFollowerAccount continue | skip guard |
| 4 | foreach pos in acc.Positions | loop header |
| 5 | pos == null \|\| pos.Quantity == 0 continue | null/flat guard |
| 6 | for-loop (DW-B115-DIAG) | diagnostic for-loop header |
| 7 | NeedsLeaderFallbackFlatten | flatten guard |

**Conservative count (all branches)**: CYC = 8. Exactly at the JS-066 ceiling.  
**Without precondition guard (branch 0)**: CYC = 7.

**Decision**: Count branch 0 (the null-guard on `forcedTargets`) as a branch for maximum
conservatism. CYC = 8. This is at the Jane Street strict limit (≤ 8). **No extraction needed.**

**Rationale for keeping the DIAG for-loop**: The DW-B115-DIAG log is valuable on the QAll2t path
specifically — it confirms `count=2` per account, which is the primary observable evidence that
the forced-targets fix is working. Removing it would save 1 CYC branch but eliminate observability
on this path. CYC=8 ≤ 8 is compliant.

If a future reviewer requires CYC ≤ 7, the DIAG for-loop can be extracted to `LogDiagTargets(acc.Name, forcedTargets, pos.Quantity)` — a pure logging helper with no state. This extraction is deferred (DW-B133-01 below).

---

## 5. Files Changed

| File | Section Changed | Change Type |
|------|----------------|-------------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | New `Execute(List<(double,int)> forcedTargets)` method added after existing `Execute()` | Additive (new method) |
| `src/PropTraderTools/TradeCopierPanel.cs` | `OnInstrQAll2tClick` body replaced (4 lines → 13 lines) | Change (1 method only) |

**NOT modified** (confirmed):
- `PttQuickExit.cs` — no change
- `CopyEngine.cs` — no change
- `PttBreakEven.cs`, `PttCancel.cs`, `PttFlatten.cs`, `PttTrim.cs` — no change
- Any test file compiled by default — new `B123Tests.cs` is an addition (not a modification)

---

## 6. Test Coverage Plan

**Test file**: `src/PropTraderTools/Tests/B123Tests.cs`  
All tests are `[Fact]`, xUnit only. No NUnit, no MSTest.

### T_B123_01 — `T_B123_01_Build2TargetList_7Qty_CeilingT1_FloorT2`

**Spec**: With 7-contract position, QAll2t should fire T1=4, T2=3.

**Arrange**: No setup needed. `Build2TargetList` is `internal static`.

**Act**: `var targets = TradeCopierPanel.Build2TargetList(7);`

**Assert**:
1. `Assert.Equal(2, targets.Count)` — exactly 2 targets.
2. `Assert.Equal(4, targets[0].Qty)` — T1 is ceiling: (7+1)/2 = 4.
3. `Assert.Equal(3, targets[1].Qty)` — T2 is floor: 7-4 = 3.

**CYC**: 1. No branches.

---

### T_B123_02 — `T_B123_02_Build2TargetList_6Qty_EqualSplit`

**Spec**: With 6-contract position, QAll2t should fire T1=3, T2=3.

**Act**: `var targets = TradeCopierPanel.Build2TargetList(6);`

**Assert**:
1. `Assert.Equal(2, targets.Count)` — exactly 2 targets.
2. `Assert.Equal(3, targets[0].Qty)` — T1: (6+1)/2 = 3.
3. `Assert.Equal(3, targets[1].Qty)` — T2: 6-3 = 3.

**CYC**: 1. No branches.

---

### T_B123_03 — `T_B123_03_Build2TargetList_AlwaysReturns2Entries_Qty1Through9`

**Spec**: Forced 2-target list always has exactly 2 entries for any position size 1-9.
Confirms no path through `Build2TargetList` returns 3 targets (guards against regression).

**Act**: Loop qty = 1 to 9, call `Build2TargetList(qty)` for each.

**Assert** (per iteration):
1. `Assert.Equal(2, targets.Count)` — count is always exactly 2.
2. `Assert.Equal(qty, targets[0].Qty + targets[1].Qty)` — split sums to total qty.
3. `Assert.True(targets[0].Qty >= targets[1].Qty)` — T1 >= T2 (ceiling is always first).

**CYC**: 2 (one for-loop header + method entry). ≤ 8.

---

### T_B123_04 — `T_B123_04_PttGlobalQuickExit_ForcedTargetsOverload_Exists`

**Spec**: Confirms the new Execute overload was actually added (method-contract existence check).
Prevents silent rollback to the no-arg-only state.

**Arrange**: `var type = typeof(PttGlobalQuickExit);`

**Act**:
```csharp
var mi = type.GetMethod(
    "Execute",
    BindingFlags.NonPublic | BindingFlags.Instance,
    null,
    new[] { typeof(System.Collections.Generic.List<(double Price, int Qty)>) },
    null
);
```

**Assert**:
1. `Assert.NotNull(mi)` — overload exists.
2. `Assert.Equal(typeof(void), mi.ReturnType)` — returns void (not null, not Task).

**CYC**: 1. No branches.

---

### T_B123_05 — `T_B123_05_NoArgOverload_StillExists`

**Spec**: Confirms the original no-arg `Execute()` overload on `PttGlobalQuickExit` was NOT
removed or replaced by the additive change in B123. The no-arg path is the code path for the
normal QAll button — its accidental removal would be a silent regression with no compile error.

**Arrange**: `var type = typeof(PttGlobalQuickExit);`

**Act**:
```csharp
var mi = type.GetMethod(
    "Execute",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
    null,
    System.Type.EmptyTypes,
    null
);
```

**Assert**:
1. `Assert.NotNull(mi)` — the zero-parameter Execute overload exists on the type.

**CYC**: 1. No branches.

---

## 7. Risk and Regression Analysis

### 7.1 Existing No-Arg `Execute()` Path (Normal QAll Button)

**Risk**: None. The no-arg `Execute()` is UNCHANGED in body and signature. Any code calling
`new PttGlobalQuickExit().Execute()` without arguments continues to resolve via the no-arg
overload. C# overload resolution: `Execute()` always resolves to the no-arg method;
`Execute(someList)` resolves to the new overload. No ambiguity.

**Verification**: SCAN-01 (grep for any call sites of PttGlobalQuickExit.Execute() that might
be affected) is added to the 7-scan contract.

### 7.2 ExecuteFollowers with `forcedTargets` as `leaderTargets`

**Risk**: `ResolveFollowerTargets(followerSnapshot, leaderTargets, fPosQty, leaderPosQty)` is
called with `leaderTargets = forcedTargets` (count=2). If `followerSnapshot.Count == 2` (matches
leader count), the follower snapshot is used directly — correct behavior. If follower snapshot is
empty or partial (count < 2), `ScaleLeaderTargets(forcedTargets, fPosQty, leaderPosQty)` is
called, producing a 2-entry scaled list. In both cases the follower exits with exactly 2 targets.

**No regression**: `ResolveFollowerTargets` and `ScaleLeaderTargets` are UNCHANGED. They already
handle 2-entry lists correctly (no hardcoded N assumption).

### 7.3 NeedsLeaderFallbackFlatten on Forced Path

**Behavior**: `NeedsLeaderFallbackFlatten(beCancelCount, forcedTargets.Count=2, posQty)` will
return false because `snapshotCount = forcedTargets.Count = 2 > 0`. The flatten path is never
taken on the forced-targets path (correct — if we have a forced 2-target list, we can always
attempt the bracket swap).

**Edge case**: If `forcedTargets` is passed as an empty list (0 entries), the flatten guard could
fire if BE orders were also cancelled. The null/empty guard at the top of the overload (returning
early on null) handles the null case. An empty list is technically valid input but would produce
0 brackets. This is acceptable — the upstream caller `OnInstrQAll2tClick` always calls
`Build2TargetList(qty >= 1)` which always returns 2 entries.

### 7.4 3-Target ATM Active During QAll2t Press

**Behavior**: The new overload skips `SnapshotTargetOrders()`. Even with 3 Working ATM Target
brackets visible, the forced-targets list (2 entries) is used. The existing ATM brackets are
handled by `PttQuickExit.Execute` internally (it cancels existing brackets before submitting new
ones). The `CancelPttBeOrders + WaitForPttBeCancelled` call in the new overload still fires to
handle any PTT-BE-* orders on the account before the bracket swap. No change to order-cancel
behavior.

---

## 8. Section K — Deferred Items

### DW-B133-01 — DIAG for-loop extraction (optional CYC reduction)

**Priority**: P3 (cosmetic — CYC=8 is at limit but compliant)  
**Description**: The DW-B115-DIAG for-loop in `Execute(forcedTargets)` adds 1 CYC branch
(total = 8). Extracting it to `private static void LogLeaderDiag(string accName, List<(double, int)> targets, int posQty)` would reduce the overload to CYC=7. Currently deferred because CYC=8 is within the JS-066 limit.  
**Unblocked by**: Any block that performs a CYC reduction pass on `PttGlobalQuickExit.cs`.  
**Target block**: TBD.

### DW-B133-SIM-01 — Live SIM gate: QAll2t 7-contract position

**Priority**: P0 (Director-owned)  
**Description**: Press QAll2t with a 7-contract position on leader + 3 follower accounts.
Verify Output tab shows `[PTT-QX-2T-ALL]` on each leader account, `count=2` in DIAG lines,
and exactly 2 OCO bracket pairs per account (T1=4, T2=3).  
**Deferred to**: Director after F5 compilation gate passes.

### DW-B133-SIM-02 — Live SIM gate: QAll button still fires 3 targets (no regression)

**Priority**: P1 (Director-owned)  
**Description**: After B123 is deployed, press the normal QAll button with a 3-target ATM.
Confirm Output tab shows `[PTT-QX-ALL] GlobalQuickExit fired` (no `2T` tag) and 3 OCO pairs
are submitted. Confirms no-arg `Execute()` path is unaffected.  
**Deferred to**: Same SIM session as DW-B133-SIM-01.

---

## 9. Dependencies

| Dependency | Status | Notes |
|------------|--------|-------|
| Build clean (0 errors, 0 warnings) | REQUIRED | Run `dotnet build --no-incremental` before and after |
| `Build2TargetList` accessible | CONFIRMED | `internal static` at TradeCopierPanel.cs:1383 — no change needed |
| `ExecuteFollowers` accepts `List<(double,int)>` | CONFIRMED | Signature verified from source: line 128-133 |
| `ResolveFollowerTargets` handles 2-entry leaderTargets | CONFIRMED | No N assumption in implementation |
| xUnit 2.x | REQUIRED | Only xUnit — no NUnit, no MSTest (JS-051..065) |

---

## 10. JS Rules Applicable

| Rule | Category | Applicability |
|------|----------|---------------|
| JS-001 | Type Safety | No throw anywhere in new overload or updated click handler |
| JS-002 | Type Safety | forcedTargets null guard returns early (not throws); void return (N/A) |
| JS-021 | Concurrency | P0 — no `lock()` anywhere in PttGlobalQuickExit.cs or TradeCopierPanel.cs changes |
| JS-033 | Concurrency | New Execute(forcedTargets) is synchronous `internal void` — not `async void` |
| JS-051 | Testing | `[Fact]` on every test method in B123Tests.cs |
| JS-053 | Testing | `Assert.Equal`, `Assert.NotNull`, `Assert.True` — no custom assertion wrappers |
| JS-066 | Code Review | CYC <= 8 per new method — Execute(forcedTargets) = 8, OnInstrQAll2tClick = 4 |

---

## 11. Scans Required — 7-Scan Contract

| Scan | Command | Expected Result |
|------|---------|-----------------|
| SCAN-01 | `grep -n "lock\s*(" src/PropTraderTools/Features/PttGlobalQuickExit.cs src/PropTraderTools/TradeCopierPanel.cs src/PropTraderTools/Tests/B123Tests.cs` | 0 matches |
| SCAN-02 | `grep -n "async\s\+void" src/PropTraderTools/Features/PttGlobalQuickExit.cs src/PropTraderTools/TradeCopierPanel.cs` | 0 matches |
| SCAN-03 | `grep -n "return\s\+null" src/PropTraderTools/Features/PttGlobalQuickExit.cs` | 0 matches (new overload returns void) |
| SCAN-04 | `python scripts/complexity_audit.py src/PropTraderTools/Features/PttGlobalQuickExit.cs src/PropTraderTools/Tests/B123Tests.cs` | All methods CYC <= 8 |
| SCAN-05 | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Features/PttGlobalQuickExit.cs src/PropTraderTools/TradeCopierPanel.cs src/PropTraderTools/Tests/B123Tests.cs` | 0 matches (ASCII-only) |
| SCAN-06 | `grep -n "\[Test\]\|\[TestMethod\]\|using NUnit\|using Microsoft.VisualStudio" src/PropTraderTools/Tests/B123Tests.cs` | 0 matches (xUnit only) |
| SCAN-07 | `dotnet build src/PropTraderTools/PropTraderTools.csproj --no-incremental` | Build succeeded. 0 Error(s). 0 Warning(s). |

**Note**: `--no-incremental` is mandatory per DW-B122-04. An incremental build may return a false green from a stale cached DLL.

---

## 12. Artifacts

| Artifact | Type | Path | Notes |
|----------|------|------|-------|
| Architecture plan | This document | `docs/brain/B123/02-architecture-plan.md` | Phase 1 output |
| New method | Production change | `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Additive: new Execute(forcedTargets) overload |
| Method change | Production change | `src/PropTraderTools/TradeCopierPanel.cs` | OnInstrQAll2tClick body only |
| New test file | Test addition | `src/PropTraderTools/Tests/B123Tests.cs` | 5 [Fact] methods; replaces prior B123Tests.cs |
| Ticket completion | Engineer output | `docs/brain/B123/ticket-1-completion.md` | Written by ptt-engineer after SCAN-07 green |

---

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Overload vs separate class | Overload on same class | Additive; no new class needed; C# overload resolution is unambiguous |
| Skip SnapshotTargetOrders | Yes — use forcedTargets directly | Root cause is snapshot reading the wrong count; skipping it eliminates the defect path |
| Follower path | Pass forcedTargets as leaderTargets to ExecuteFollowers | ResolveFollowerTargets already handles 2-entry leaderTargets correctly; no helper change needed |
| DIAG for-loop included | Yes — keeps CYC=8 | Observability value outweighs 1 extra CYC branch; still ≤ 8 |
| CYC = 8 acceptable | Yes — at limit but compliant | JS-066 threshold is ≤ 8; extraction deferred as DW-B133-01 |
| Tests use Build2TargetList | Yes — internal static, no reflection needed | Build2TargetList is pure math, no NT8 runtime dependency; direct call is simpler and faster |
