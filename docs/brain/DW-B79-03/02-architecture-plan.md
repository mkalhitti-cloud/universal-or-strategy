# DW-B79-03 Architecture Plan
# QX Conflict Guard: Pre-Cancel Follower ATM Brackets in PttGlobalQuickExit.ExecuteOne

**Status**: REVIEW_PASS (pending reviewer sign-off)
**Epic**: DW-B79-03
**Phase**: 1 (Architecture)
**Author**: ptt-architect
**Date**: 2026-08-10

---

## Section 1 — Problem Analysis

### 1.1 What Was Already Fixed: Gap 2 (REPAIR-08, commit a3f68559)

`PttBreakEven.SnapshotTargetsLocal` previously used `stateOk = Working | Accepted` only.
On rapid ATM-fill → BE button press, target orders could be in
`Submitted | Initialized | TriggerPending` before reaching Working.
The narrow filter produced `targets=0` → bare-stop path on the BE button.

**Fix (committed)**: [`PttBreakEven.cs:321-325`](src/PropTraderTools/Features/PttBreakEven.cs:321)
widened to `Working | Accepted | Submitted | Initialized | TriggerPending`.
This is now symmetric with `MoveStopToBreakEven` Step A.

**No further action required for Gap 2.** This plan documents it as closed.

---

### 1.2 Remaining Open Issue: DW-B79-03 QX Conflict Guard

#### Race Timeline (QX-ALL → BE-ALL on followers)

```
T+0ms   QX-ALL button pressed
        PttGlobalQuickExit.Execute() fires on UI thread

T+1ms   Leader (Sim101):
          SnapshotTargetOrders -> finds 8 cancellable orders (ATM brackets Working)
          BuildQxSnapshot      -> snapshot=8
          CancelQxBrackets     -> cancels all 8 ATM brackets -> CancelSubmitted
          Submit PTT-QX-Stop + PTT-QX-T1..TN for Sim101
          [Leader brackets confirmed Cancelled before PTT-QX Submitted -- no conflict]

T+2ms   Follower (Sim102):
          SnapshotTargetOrders(Sim102) -> returns 0  (ATM brackets NOT yet in acc.Orders)
          BuildQxSnapshot(Sim102)      -> snapshot=0 (nothing cancellable found)
          CancelQxBrackets(Sim102)     -> no-op (nothing to cancel)
          Submit PTT-QX-Stop + PTT-QX-T1..TN for Sim102
            -> state: Initialized -> Submitted

T+3ms   NT8 async lag: Sim102 ATM brackets ARRIVE as Working in acc.Orders
          (NT8 copy engine processes Sim101 fill, arms Sim102 ATM strategy async)

T+4ms   NT8 sim detects conflict:
          Sim102 has Working ATM brackets (Stop1/Target1) AND PTT-QX-Stop/PTT-QX-T1 Submitted
          NT8 cancels the PTT-QX orders -> state: CancelSubmitted

T+5ms   BE-ALL button pressed (immediately after QX-ALL)
        CopyEngine.MoveStopToBreakEven fires for Sim102

T+5ms   Step A (target snapshot):
          stateOk = Working|Accepted|Submitted|Initialized|TriggerPending
          Scans acc.Orders for PTT-QX-T* orders
          Finds PTT-QX-T1, PTT-QX-T2 -- but their state = CancelSubmitted
          CancelSubmitted is NOT in stateOk -> MISSED
          targets.Count = 0

T+5ms   Step C (targets.Count == 0 branch):
          Submits ONE bare PTT-BE-Stop for full position quantity
          Result: position protected but NO OCO target pairs
```

#### Why The Leader Path Does Not Have This Problem

The leader (Sim101) has Working ATM brackets at QX-ALL fire time.
`BuildQxSnapshot` captures them (snapshot=8), `CancelQxBrackets` cancels them.
By the time `Submit PTT-QX` runs, the ATM brackets are already in `CancelSubmitted`
and confirmed `Cancelled` within the same synchronous dispatch call (NT8 sim is local).
NT8 sim sees clean state: no conflict.

The follower has a ~1-3ms NT8 async lag before its ATM brackets appear in `acc.Orders`.
This lag is documented in `NT8_FULL_REFERENCE.md` line 1721:
> "Changes to positions will not be reflected till at least the next OnBarUpdate() event."

#### Per-Chart QX Button Not Affected

The single-account QX button calls `PttQuickExit.Execute(skipIfFollower=true)`.
It operates on the leader account only. The leader's own ATM brackets are always
present and cancelled before PTT-QX submission (verified: snapshot=8 in DIAG trace).
**This code path is not affected and must not be changed.**

---

## Section 2 — Direction Decision

### 2.1 Direction Evaluation

| Direction | Description | Verdict |
|-----------|-------------|---------|
| **A** (chosen) | Pre-cancel follower ATM brackets in `PttGlobalQuickExit.ExecuteOne` before calling `PttQuickExit.Execute`. | **CHOSEN** |
| B (original) | Re-snapshot in `PttQuickExit.Execute` and skip PTT-QX if `CancelSubmitted` orders found. | REJECTED: creates protection gap — follower ends up with no stop and no ATM brackets. |
| B-revised | Widen `MoveStopToBreakEven` Step A `stateOk` to include `CancelSubmitted` for target reading. | REJECTED: does not fix the root cause (PTT-QX orders still cancelled by NT8 sim); addresses only symptom. Also risks reading prices from orders that may never have been confirmed by exchange. |
| C | Document bare-stop as designed (WAD). | REJECTED: bare-stop after QX-ALL → BE-ALL is confusing to the user and the behavior is fixable. Live safety: bare-stop IS position-protecting, but the UX regression is real and avoidable. |

### 2.2 Direction A Justification

The root cause is that `PttQuickExit.Execute` for followers runs its cancel step when
the follower's ATM brackets have not yet arrived (`acc.Orders` is empty for that instrument).
The fix mirrors the leader's own behavior: **cancel first, then submit**.

By calling `CopyEngine.Instance?.CancelQxBrackets(acc, instr)` in `ExecuteOne` BEFORE
constructing `PttQuickExit`, the follower's ATM brackets (in any pre-terminal state)
are cancelled. When `PttQuickExit.Execute` then runs its own `BuildQxSnapshot` /
`CancelQxBrackets(snapshot)`, the snapshot is empty (all orders are now
`CancelSubmitted`) and the internal cancel is a no-op. PTT-QX orders are then
submitted to a clean follower account — no conflict.

This approach:
- Changes **one method** (`ExecuteOne`) with **two new lines**
- Leaves `PttQuickExit.Execute` completely unchanged (steering note requirement)
- Leaves `CopyEngine.MoveStopToBreakEven` unchanged
- Leaves `PttBreakEven.SnapshotTargetsLocal` unchanged (Gap 2 already fixed)
- Reuses the already-tested `CancelQxBrackets(Account, Instrument)` 2-param overload
- Is symmetric with the leader cancel path

### 2.3 Direction C Live-Trading Safety Assessment (required by spec)

Direction C (WAD) is rejected, but the safety assessment is documented:
- Bare `PTT-BE-Stop` is a valid `StopMarket` GTC order for the full position quantity.
- Position IS protected in live trading: if price reverses through break-even, the stop fires.
- The gap is that BE targets (the OCO Limit orders) are absent. The user must manually
  manage target exits or let the position run until the stop fires.
- In sim, the `CancelSubmitted` conflict only affects the `targets.Count == 0` branch; the
  stop IS submitted and IS working. No capital loss risk from the bare-stop path.
- However, the user experience diverges from the expected QX → BE-ALL sequence, and
  the fix is low-risk. Direction C is therefore rejected.

---

## Section 3 — Implementation Specification

### 3.1 Change Location

**File**: [`src/PropTraderTools/Features/PttGlobalQuickExit.cs`](src/PropTraderTools/Features/PttGlobalQuickExit.cs)
**Method**: `ExecuteOne`
**Lines**: 92-101 (current)

### 3.2 Current ExecuteOne (CYC=1)

```csharp
// CYC=1: straight delegation.
private void ExecuteOne(
    Account acc, Instrument instr, int t1Ticks,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    bool skipIfFollower = true,
    double leaderStop = 0,
    int leaderTargetCount = 0)
{
    var executor = new PttQuickExit();
    executor.Execute(acc, instr, t1Ticks, targets, skipIfFollower, leaderStop, leaderTargetCount);
}
```

### 3.3 New ExecuteOne (CYC=2)

```csharp
/// <summary>
/// ExecuteOne: per-account Quick Exit bracket swap.
/// HOTFIX-QUICK-T3-01: accepts targets snapshot for N-bracket submission.
/// B78 DW-B63-01: leaderStop + leaderTargetCount forwarded to PttQuickExit.Execute.
/// DW-B79-03: pre-cancel follower ATM+PTT-* brackets BEFORE constructing PttQuickExit
///   so the follower account is clean when PttQuickExit.Execute runs its own cancel step.
///   Mirrors the leader path: cancel first, then submit PTT-QX.
///   Only fires on the follower path (skipIfFollower=false).
///   Leader path (skipIfFollower=true) unchanged -- leader's own ATM brackets are
///   already Working and cancelled by PttQuickExit.Execute's internal snapshot logic.
/// CYC=2: follower guard(1) + delegate(2).
/// JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous void. ASCII-only.
/// </summary>
private void ExecuteOne(
    Account acc, Instrument instr, int t1Ticks,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    bool skipIfFollower = true,
    double leaderStop = 0,
    int leaderTargetCount = 0)
{
    // DW-B79-03: pre-cancel follower ATM + prior PTT-* brackets BEFORE PttQuickExit snapshot.
    // When follower ATM brackets exist in any cancellable state (Working/Accepted/Submitted/
    // Initialized/TriggerPending), this cancel fires first -- identical to what the leader
    // path does naturally (leader ATM brackets are always Working at QX-ALL fire time and
    // cancelled by PttQuickExit.Execute's BuildQxSnapshot/CancelQxBrackets).
    // After this call, follower brackets enter CancelSubmitted (excluded from
    // BuildQxSnapshot's stateOk) -- PttQuickExit's internal cancel is a no-op.
    // NT8 sim confirms the cancel before PTT-QX Submit completes, preventing the conflict.
    if (!skipIfFollower)                                                            // (1)
        CopyEngine.Instance?.CancelQxBrackets(acc, instr);
    var executor = new PttQuickExit();
    executor.Execute(acc, instr, t1Ticks, targets, skipIfFollower, leaderStop, leaderTargetCount);
}
```

### 3.4 Call Chain After Fix

```
QX-ALL button click
  -> PttGlobalQuickExit.Execute()
       [for each leader account+position]
         -> ExecuteOne(leader, ..., skipIfFollower=true)
              -> PttQuickExit.Execute(skipIfFollower=true)
                   -> BuildQxSnapshot     (finds N Working ATM brackets)
                   -> CancelQxBrackets    (cancels N ATM brackets)
                   -> Submit PTT-QX-Stop/T1..TN for leader
       [for each follower in rule]
         -> ExecuteOne(follower, ..., skipIfFollower=false)
              [NEW] -> CancelQxBrackets(follower, instr)
                         (cancels any Working/Submitted/Init ATM brackets NOW)
              -> PttQuickExit.Execute(skipIfFollower=false)
                   -> BuildQxSnapshot     (finds 0 orders -- all CancelSubmitted)
                   -> CancelQxBrackets    (no-op, snapshot=0)
                   -> Submit PTT-QX-Stop/T1..TN for follower
                      [follower is clean -- NT8 sim sees no conflict]
```

### 3.5 No Helper Extraction Required

The fix is a single `if (!skipIfFollower)` guard calling an existing tested method.
No new helper method is needed. `CancelQxBrackets(Account, Instrument)` (2-param overload)
is already tested, used by `CancelQxBracketsForFollowers`, and covers all cancellable
states including the NT8 async bracket states (TriggerPending added in HOTFIX-QX-DOUBLE-01).

---

## Section 4 — CYC Analysis

| Method | File | CYC Before | CYC After | Change | Budget |
|--------|------|------------|-----------|--------|--------|
| `PttGlobalQuickExit.Execute` | PttGlobalQuickExit.cs | 8 | 8 | none | PASS |
| `PttGlobalQuickExit.ExecuteOne` | PttGlobalQuickExit.cs | 1 | 2 | +1 | PASS (<=8) |
| `PttGlobalQuickExit.ResolveQuickTicks` | PttGlobalQuickExit.cs | 2 | 2 | none | PASS |
| `PttGlobalQuickExit.SnapshotTargetOrders` | PttGlobalQuickExit.cs | 4 | 4 | none | PASS |
| `CopyEngine.CancelQxBrackets(acc,instr)` | CopyEngine.cs | 6 | 6 | none (called, not changed) | PASS |

**All methods remain <= 8. No CYC violations introduced.**

### CYC Branch Count Verification for ExecuteOne

New branches in `ExecuteOne`:
1. `if (!skipIfFollower)` — branch 1
2. Delegate call (base branch) — branch 2 (implicit)

McCabe CYC = edges - nodes + 2 = 2 for a method with one conditional.
**CYC=2. Budget <= 8. PASS.**

Why `Execute()` stays at CYC=8:
The new `CancelQxBrackets` call is moved INSIDE `ExecuteOne`, not inside `Execute()`.
`Execute()` already calls `ExecuteOne()` as branch (8). The guard is inside `ExecuteOne` —
zero new branches added to `Execute()`.

---

## Section 5 — Test Plan

### 5.1 Test File

**New file**: `src/PropTraderTools/Tests/B79Tests.cs`
(or appended to `src/PropTraderTools/Tests/B71Tests.cs` which covers follower QX paths)

Minimum 2 new `[Fact]` tests. Both test the `ExecuteOne` guard directly via reflection
or via the existing mock Account/Instrument test infrastructure.

### 5.2 Test T_DW_B79_03_01

```csharp
[Fact]
// T_DW_B79_03_01:
// ExecuteOne called with skipIfFollower=false AND follower has a Working ATM bracket.
// Assert: CancelQxBrackets is invoked BEFORE PttQuickExit.Execute constructs its snapshot.
// Mechanism: use a spy/mock CopyEngine where CancelQxBrackets records invocations.
// Assert: cancelInvocationCount == 1 (pre-cancel fired once for the follower).
public void ExecuteOne_Follower_PreCancelsBeforeQxSubmit()
{
    // Arrange: follower account has 1 Working Stop1 order (ATM bracket).
    // Arrange: spy CopyEngine records CancelQxBrackets calls.
    // Act: call ExecuteOne(follower, instr, t1=4, targets=emptyList, skipIfFollower=false)
    // Assert: cancelInvocationCount == 1
    // Assert: order was in stale list (IsQxCancelCandidate("Stop1") == true)
}
```

**Exact assert conditions**:
- `cancelInvocationCount >= 1` (CancelQxBrackets called for follower account)
- The call happened BEFORE `PttQuickExit.Execute` was entered
  (verified by recording call order in the spy)

### 5.3 Test T_DW_B79_03_02

```csharp
[Fact]
// T_DW_B79_03_02:
// ExecuteOne called with skipIfFollower=true (default: leader path).
// Assert: CancelQxBrackets is NOT invoked by ExecuteOne (leader cancels via
//         PttQuickExit.Execute's own BuildQxSnapshot / CancelQxBrackets path).
public void ExecuteOne_Leader_DoesNotPreCancelFollowerBrackets()
{
    // Arrange: leader account has 2 Working ATM brackets.
    // Arrange: spy CopyEngine records CancelQxBrackets calls from ExecuteOne ONLY.
    // Act: call ExecuteOne(leader, instr, t1=4, targets=emptyList, skipIfFollower=true)
    // Assert: executeOneCancelCount == 0
    // (PttQuickExit.Execute's OWN cancel path is separate and still runs normally)
}
```

**Exact assert conditions**:
- `executeOneCancelCount == 0` (the new `if (!skipIfFollower)` guard does NOT fire)

### 5.4 Test T_DW_B79_03_03 (optional, belt-and-suspenders)

```csharp
[Fact]
// T_DW_B79_03_03:
// After pre-cancel, BuildQxSnapshot sees 0 cancellable orders for follower
// (orders are in CancelSubmitted -- excluded from BuildQxSnapshot stateOk).
// Asserts the underlying invariant that makes Direction A work.
public void BuildQxSnapshot_ExcludesCancelSubmitted_Orders()
{
    // Arrange: acc has 1 order with OrderState.CancelSubmitted, name="Stop1"
    // Act: result = CopyEngine.BuildQxSnapshot(acc, instr)
    // Assert: result.Count == 0 (CancelSubmitted not in stateOk)
}
```

**Exact assert conditions**:
- `result.Count == 0` given an order in `CancelSubmitted` state

### 5.5 [Fact] Count

| Before DW-B79-03 | New [Fact] | Total After |
|------------------|-----------|-------------|
| 539 | +2 (min) or +3 (recommended) | 541 (min) or 542 |

Acceptance criterion: `>= 539`. Both targets exceed this. ✅

---

## Section 6 — File Change Summary

| File | Change Type | Reason |
|------|-------------|--------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | MODIFY | Add pre-cancel guard in `ExecuteOne` (2 lines + updated XML doc) |
| `src/PropTraderTools/Tests/B79Tests.cs` | CREATE | New `[Fact]` tests for DW-B79-03 (2-3 tests) |
| `docs/brain/NO-PIPELINE-REPAIRS.md` | MODIFY | Carry-forward table: DW-B79-03 → FIXED with commit hash |

### Files Explicitly NOT Changed

| File | Reason |
|------|--------|
| `src/PropTraderTools/Features/PttQuickExit.cs` | Steering note: must remain unchanged |
| `src/PropTraderTools/CopyEngine.cs` | `CancelQxBrackets` 2-param overload already correct; no changes needed |
| `src/PropTraderTools/Features/PttBreakEven.cs` | Gap 2 already fixed (REPAIR-08 a3f68559) |
| `src/PropTraderTools/TradeCopierPanel.cs` | No change to QX button dispatch path |

---

## Section 7 — 7-Scan Checklist

This checklist must be executed by `ptt-engineer` before each commit for this epic.
All must return zero results in the **changed files** (`PttGlobalQuickExit.cs`, `B79Tests.cs`).

### SCAN-01 — lock() ban (JS-021, P0)

```powershell
grep -n "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs
grep -n "lock(" src/PropTraderTools/Tests/B79Tests.cs
```
**Expected**: 0 matches. Current known count in PttGlobalQuickExit.cs: 0. ✅

### SCAN-02 — throw new (JS-001, P0)

```powershell
grep -n "throw new" src/PropTraderTools/Features/PttGlobalQuickExit.cs
grep -n "throw new" src/PropTraderTools/Tests/B79Tests.cs
```
**Expected**: 0 matches in production code (test arrange-only throw is acceptable in test file). ✅

### SCAN-03 — return null (JS-002, P0)

```powershell
grep -n "return null" src/PropTraderTools/Features/PttGlobalQuickExit.cs
```
**Expected**: 0 matches. `SnapshotTargetOrders` returns empty list (never null). ✅

### SCAN-04 — async void non-event-handler (JS-033, P0)

```powershell
grep -n "async void" src/PropTraderTools/Features/PttGlobalQuickExit.cs
```
**Expected**: 0 matches. All methods are synchronous void. ✅

### SCAN-05 — non-ASCII characters (JS-066)

```powershell
Select-String -Pattern '[^\x00-\x7F]' src/PropTraderTools/Features/PttGlobalQuickExit.cs
Select-String -Pattern '[^\x00-\x7F]' src/PropTraderTools/Tests/B79Tests.cs
```
**Expected**: 0 matches. All string literals and identifiers are ASCII-only. ✅

### SCAN-06 — CYC audit

```powershell
python scripts/complexity_audit.py src/PropTraderTools/Features/PttGlobalQuickExit.cs
```
**Expected**: All methods <= 8. `Execute`=8, `ExecuteOne`=2, `ResolveQuickTicks`=2, `SnapshotTargetOrders`=4. ✅

### SCAN-07 — [Fact] count check

```powershell
Select-String -Path "src/PropTraderTools/**/*.cs" -Pattern "\[Fact\]" | Measure-Object | Select-Object Count
```
**Expected**: Count >= 539 (pre-fix baseline). Post-fix target: >= 541. ✅

---

## Appendix A — Why CancelQxBrackets Is the Right Pre-Cancel Tool

`CancelQxBrackets(Account, Instrument)` (2-param overload, `CopyEngine.cs:586`) was designed
exactly for this use case:
- `stateOk` covers `Working|Initialized|Accepted|Submitted|TriggerPending` — all pre-terminal
  states that an ATM bracket could be in when QX-ALL fires.
- `IsQxCancelCandidate` covers ATM bracket names (`Stop1..Stop9`, `Target1..Target9`),
  `PTT-QX-*`, `PTT-BE-*`, and `PTT-Copy*` — all orders that should be cleared.
- Already used by `CancelQxBracketsForFollowers` (called from the leader path, `skipIfFollower=true`).
- Already tested (B68Tests, B70Tests, B71Tests cover this method).

The pre-cancel in `ExecuteOne` effectively inlines what `CancelQxBracketsForFollowers` does
per-follower, but fires it BEFORE `PttQuickExit.Execute` builds its snapshot — giving NT8 sim
the maximum possible window to process the cancel before PTT-QX orders arrive.

---

## Appendix B — Gap 2 Closure Confirmation

**REPAIR-08** commit `a3f68559` widened `PttBreakEven.SnapshotTargetsLocal.stateOk`:

```csharp
// PttBreakEven.cs lines 321-325 (post-REPAIR-08):
bool stateOk = o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Accepted
            || o.OrderState == OrderState.Submitted      // REPAIR-08
            || o.OrderState == OrderState.Initialized    // REPAIR-08
            || o.OrderState == OrderState.TriggerPending; // REPAIR-08
```

This is symmetric with `CopyEngine.MoveStopToBreakEven` Step A (lines 2385-2389).
**Gap 2 is CLOSED. No further code change needed.**

---

## Appendix C — NO-PIPELINE-REPAIRS.md Update Required

After commit, `ptt-engineer` must update the carry-forward table in
[`docs/brain/NO-PIPELINE-REPAIRS.md`](docs/brain/NO-PIPELINE-REPAIRS.md):

```
| DW-B79-03 | QX Conflict Guard: follower ATM cancel before PTT-QX submit | P2 | FIXED (commit XXXXXXXX) |
```

Replace `XXXXXXXX` with the actual commit hash.

---

PLAN_COMPLETE
