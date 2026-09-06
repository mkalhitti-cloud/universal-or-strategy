# Architecture Plan: DW-LB-GR-01 BE Retry Logic Bug Fix

**Block**: DW-LB-GR-01  
**Epic**: DW-LB-GR-01 BE Retry Logic Bug Fix  
**Phase**: 1 -- Architecture Plan
**Status**: REVIEW_PASS (cycle 2)
**Date**: 2026-09-07  
**Author**: ptt-architect  
**Source Finding**: Sentinel (Greptile), PR #47 post-merge review -- JS-100 / P1  

---

## § 1. Defect Root Cause

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Method**: `RegisterBeRetrySlotIfNeeded` (L6107-L6160)  
**Defect Line**: L6118  

### Variable Semantics at L6118

| Parameter | Type | Meaning | Source |
|-----------|------|---------|--------|
| `targetsCount` | `int` | `targets.Count` from `SnapshotBeTargets(acc, instrument)` -- the number of PTT/transitional target orders visible on **the follower account** being processed. Zero means the follower has no visible targets to protect. | Caller passes `targets.Count` directly |
| `leaderCount` | `int` | Return value of `CountLeaderTargets(instrument)` -- the number of Working native `Target1..9` limit orders on the **leader account**. Zero means the leader has no native targets (normal after fill or cancel). | Caller passes `CountLeaderTargets(instrument)` or hardcoded `0` |

### Why `leaderCount == 0` is Wrong

The condition at L6118 gates the "targets=0 path" -- the branch that arms a retry slot and queues a
`QueueBeRetryFallback` call. The intent is: "if the follower has no visible targets to protect, arm a
retry to wait for PTT orders to land."

The bug: `leaderCount` is used instead of `targetsCount`. This creates a false trigger scenario:

- **Scenario**: A follower has `targetsCount > 0` (it has working PTT/transitional targets to protect),
  but the leader has `leaderCount == 0` (its native `Target1..9` orders filled or were cancelled --
  a completely normal lifecycle event in NT8 trading).
- **What happens with the bug**: `leaderCount == 0` fires the retry-arming branch.
  The retry call cancels existing OCO protection on the follower that still has working PTT targets.
  This is the **spurious cancel** -- live protection is torn down on a follower that needed no retry.
- **What should happen**: `targetsCount == 0` is the correct gate. When `targetsCount > 0`, the follower
  has visible targets and does NOT need a retry. The method must not arm retry in this case.

### Caller Analysis

**Caller site 1** (L6026-6035, `targets.Count == 0` path):
```
RegisterBeRetrySlotIfNeeded(acc, instrument, bufferTicks, isRetry,
    targets.Count,   // targetsCount = 0 (empty snapshot)
    leaderCount: 0   // hardcoded 0 -- ARCHITECTURE LOCKED, stays
);
```
Both `targetsCount` and `leaderCount` are 0 at this call site. Bug is masked here -- both conditions
produce the same result. The fix has no behavioral change at this call site.

**Caller site 2** (L6038-6045, partial-targets path):
```
RegisterBeRetrySlotIfNeeded(acc, instrument, bufferTicks, isRetry,
    targets.Count,                    // targetsCount = 1..N (follower has visible targets)
    CountLeaderTargets(instrument)    // leaderCount = 0..N (independent of follower state)
);
```
When `leaderCount == 0` but `targetsCount > 0`: bug fires. After fix, `targetsCount > 0` correctly
suppresses the retry arm. **This is the production defect path.**

---

## § 2. Fix Description

**Change type**: Single-token variable rename in one predicate expression.  
**File**: `src/PropTraderTools/CopyEngine.cs`  
**Line**: L6118  

```csharp
// BEFORE (buggy):
if (leaderCount == 0) // (2) targets==0 path

// AFTER (correct):
if (targetsCount == 0) // (2) targets==0 path
```

**Secondary**: Update the method-header comment at L6104 to replace `leaderCount==0 branch(3)` with
`targetsCount==0 branch(3)` so the CYC annotation matches the corrected code.

No logic structure changes. No method signature changes. No new branches. No new variables.  
CYC remains <=6 (unchanged branch count). JS-021 compliant (no lock added).

---

## § 3. Scope

| Axis | Value |
|------|-------|
| Files changed | 1 (`CopyEngine.cs`) |
| Methods changed | 1 (`RegisterBeRetrySlotIfNeeded`) |
| Tokens changed | 1 (`leaderCount` → `targetsCount` at L6118) |
| Comment updated | 1 (L6104: CYC annotation, same method) |
| Test file | `tests/PropTraderTools.Tests/RegisterBeRetrySlotIfNeededTests.cs` (new) |
| Interface changes | None |
| Signature changes | None |
| New dependencies | None |
| NT8 API changes | None |

---

## § 4. Test Plan

The engineer MUST write the following xUnit `[Fact]` tests in
`tests/PropTraderTools.Tests/RegisterBeRetrySlotIfNeededTests.cs`.

All tests exercise `RegisterBeRetrySlotIfNeeded` via the reflection seam pattern already established
in `BwaveRefactorLaneBTests.cs` (private method invocation). Tests use `MockAccount`,
`MockInstrument`, and `MockPosition` test doubles consistent with existing test infrastructure.

### Test 1 -- Spurious Arm Prevented (the BUG scenario)

```
[Fact]
RegisterBeRetrySlotIfNeeded_LeaderZero_TargetsNonZero_DoesNotArmRetry
```

**Preconditions**:
- `leaderCount = 0` (leader has no native targets -- post-fill/cancel, normal)
- `targetsCount = 2` (follower has 2 visible PTT targets to protect)
- `isRetry = false`
- Position: Long (non-flat)
- `IsFollowerAccount(acc) = true`

**Assert**: `_pendingFollowerBeSlots` does NOT contain `acc.Name` after the call.  
**Rationale**: This is the exact spurious-cancel scenario. Before the fix, `leaderCount == 0` armed
the retry. After the fix, `targetsCount == 2` correctly suppresses it. This test FAILS on the
pre-fix code and PASSES after the fix.

---

### Test 2 -- Correct Arm on Zero Follower Targets

```
[Fact]
RegisterBeRetrySlotIfNeeded_TargetsZero_LeaderNonZero_ArmsRetry
```

**Preconditions**:
- `leaderCount = 3` (leader has 3 native targets)
- `targetsCount = 0` (follower has no visible targets -- PTT orders not yet landed)
- `isRetry = false`
- Position: Long (non-flat)

**Assert**: `_pendingFollowerBeSlots` CONTAINS `acc.Name` after the call (retry slot registered).  
**Rationale**: Follower has no targets yet -- correct to arm retry. Both pre-fix and post-fix code
should pass this test (the fix does not change behavior for this input combination).

---

### Test 3 -- Partial-Targets Arm (DW-B79-07 path)

```
[Fact]
RegisterBeRetrySlotIfNeeded_PartialTargets_LeaderHasMore_ArmsRetry
```

**Preconditions**:
- `leaderCount = 3` (leader has 3 native targets)
- `targetsCount = 1` (follower has 1 of 3 PTT targets visible so far)
- `isRetry = false`
- Position: Long (non-flat)
- `IsFollowerAccount(acc) = true`

**Assert**: `_pendingFollowerBeSlots` CONTAINS `acc.Name` after the call (partial retry registered).  
**Rationale**: Follower is partially protected -- 2 pairs still outstanding. Retry must arm.
This test exercises the `targetsCount < leaderCount` partial-targets path (L6138-6143), unchanged
by the fix. Confirms the second branch is unaffected.

---

## § 5. Scan Requirements

All 6 scans MUST pass after the fix is applied. The engineer MUST run every scan and
confirm the stated result before marking the ticket complete.

| Scan ID | Command | Required Result | Notes |
|---------|---------|-----------------|-------|
| SCAN-1 | `lizard src/PropTraderTools/CopyEngine.cs --CCN 8` | Warning count: 0 | Fix does not add branches; CYC of `RegisterBeRetrySlotIfNeeded` remains 6. |
| SCAN-2 | `grep -rn "lock\s*(" src/PropTraderTools/CopyEngine.cs` | 0 actual `lock()` in method bodies | `_pendingFollowerBeSlots` is `ConcurrentDictionary` -- lock-free. JS-021. |
| SCAN-3 | `grep -rn "async\s*void" src/PropTraderTools/CopyEngine.cs` | 0 `async void` in method bodies | Fix introduces no async construct. JS-033. |
| SCAN-4 | ASCII-only check -- 0 bytes > 127 in changed lines | 0 non-ASCII bytes | Fix is a pure ASCII token rename (`leaderCount` -> `targetsCount`). |
| SCAN-5 | `dotnet build` | 0 errors | Compile gate -- mandatory for any `.cs` change. |
| SCAN-6 | `dotnet test` | All prior tests still pass (no regression); 3 new `[Fact]` tests pass | Test runner gate -- mandatory. |

---

## § 6. Out-of-Scope

The following are explicitly NOT changing in this block:

| Item | Rationale |
|------|-----------|
| Caller site 1 (`leaderCount: 0` hardcoded at L6034) | Architecture locked. `leaderCount=0` at this call site is correct by design. The method signature is unchanged. |
| `CountLeaderTargets(instrument)` | Unchanged. Not part of the defect. |
| `SnapshotBeTargets(acc, instrument)` | Unchanged. |
| `QueueBeRetryFallback` | Unchanged. |
| `PttBreakEvenSwap.Execute` | Unchanged. |
| `MoveStopToBreakEven` | Unchanged (the caller). |
| `_pendingFollowerBeSlots` type (`ConcurrentDictionary<string, byte>`) | Architecture locked. Already correct as per DW notes. |
| `_drainOwnedOrderIds` | Unchanged. Not touched by this block. |
| `(long)(int)Environment.TickCount` | Architecture locked. .NET 4.8 pattern. Unchanged. |
| `ActiveOrders .ToList()` | Deferred DW-NEXT-A-07. Not in scope. |
| `DW-LB-AQ-01`, `DW-LB-AQ-02`, `DW-LB-AQ-03`, `DW-LB-AQ-04`, `DW-LB-CA-01` | Other deferred backlog items. Separate blocks. |
| Any `Account.Change()`, `AtmStrategyCreate()`, `AtmStrategyChangeStopTarget()` usage | Not present in scope. Architecture locked -- these are not AddOnBase patterns. |

---

## § 7. LANE-SPLIT GATE RESULT

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

Determination: The defect is in a single method (`RegisterBeRetrySlotIfNeeded`) at a single line
(L6118). The fix is within 50 lines. No parallel execution tracks required. One ticket, one engineer,
sequential execution.

---

## § 8. Architecture Lock Acknowledgement

The following architecture decisions are locked and were not re-investigated:

| Item | Lock Reason |
|------|-------------|
| `(long)(int)Environment.TickCount` | .NET 4.8, confirmed correct. Architecture locked. |
| `ActiveOrders .ToList()` | Deferred DW-NEXT-A-07. Architecture locked: stays. |
| `_drainOwnedOrderIds ConcurrentDictionary<string, byte>` | NT8 `OrderId` is `string`. Architecture locked. |
| NT8 AddOnBase API surface | `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` only. `Account.Change()`, `AtmStrategyCreate()`, `AtmStrategyChangeStopTarget()` are StrategyBase-only or silent no-ops -- NOT used. |
| CopyEngine.cs CCN baseline | 366 methods, AvgCCN=4.0, zero CCN>8. Fix does not change this baseline. |
| Caller `leaderCount: 0` hardcode at L6034 | Architecture locked. The two call sites are intentionally differentiated. |

---

*Plan status: PLAN_COMPLETE*
