# Tickets: DW-LB-GR-01 BE Retry Logic Bug Fix

**Block**: DW-LB-GR-01
**Epic**: DW-LB-GR-01 BE Retry Logic Bug Fix
**Phase**: 3 -- Ticket Generation
**Status**: TICKETS_COMPLETE
**Date**: 2026-09-07
**Input**: docs/brain/DW-LB-GR-01/02-architecture-plan.md (REVIEW_PASS, cycle 2)
**Author**: ptt-architect

---

## T1 -- Fix RegisterBeRetrySlotIfNeeded Guard Condition

### Spec Requirement IDs

| ID | Description |
|----|-------------|
| DW-LB-GR-01 | BE retry guard uses wrong variable (`leaderCount` instead of `targetsCount`) causing spurious cancel of OCO protection on followers that still have working PTT targets. |
| JS-021 (P0) | No `lock()` anywhere in method bodies. `_pendingFollowerBeSlots` is `ConcurrentDictionary` -- lock-free. Fix must not introduce any lock. |
| JS-001 (P0) | No `throw` in hot paths. Fix introduces no new throw. |
| JS-002 (P0) | No `return null`. Fix introduces no null return. |
| JS-033 (P0) | No `async void` in method bodies. Fix introduces no async construct. |
| JS-100 (Sentinel P1) | Sentinel (Greptile) finding from PR #47 post-merge review -- incorrect predicate variable in guard condition. |

---

### File

```
src/PropTraderTools/CopyEngine.cs
```

**Method in scope**: `RegisterBeRetrySlotIfNeeded` (L6107-L6160)

---

### Change 1 -- Logic Fix (PRIMARY, MANDATORY)

**Location**: L6118

**OLD** (buggy):
```csharp
            if (leaderCount == 0) // (2) targets==0 path
```

**NEW** (correct):
```csharp
            if (targetsCount == 0) // (2) targets==0 path
```

**Why**: The condition at L6118 gates the "targets=0 path" -- the branch that arms a retry slot and
calls `QueueBeRetryFallback`. The semantic intent is: "if the **follower** has no visible PTT targets
to protect, arm a retry to wait for orders to land." The variable `leaderCount` (leader's native
`Target1..9` count) is semantically wrong here. Using it triggers the retry arm whenever the leader
has no native targets -- a normal post-fill/post-cancel state -- regardless of whether the follower
still has working PTT targets (`targetsCount > 0`). This tears down OCO protection spuriously.

`targetsCount` (the follower's visible target order count from `SnapshotBeTargets`) is the correct
gate. When `targetsCount > 0` the follower has live targets and does NOT need a retry.

**Token change count**: 1 (`leaderCount` -> `targetsCount` at L6118 only).

---

### Change 2 -- Comment Update (SECONDARY, OPTIONAL but RECOMMENDED)

**Location**: L6104 (method-header CYC annotation)

**OLD**:
```
        // CYC<=6: isRetry(1) + IsFlat(2) + leaderCount==0 branch(3) + IsFollowerAccount(4)
```

**NEW**:
```
        // CYC<=6: isRetry(1) + IsFlat(2) + targetsCount==0 branch(3) + IsFollowerAccount(4)
```

**Why**: The CYC annotation now describes the corrected predicate. No logic change -- comment only.

---

### Architecture Lock (DO NOT CHANGE)

The following items are architecture-locked. The engineer MUST NOT touch them.

| Item | Location | Lock Reason |
|------|----------|-------------|
| Method signature of `RegisterBeRetrySlotIfNeeded` | L6107-L6114 | No parameter changes. |
| Caller site 1 -- `leaderCount: 0` hardcode | L6026-6035 | Both `targetsCount` and `leaderCount` are 0 at this site; bug is masked here. Fix has no behavioral change at this call site. The `0` stays. |
| Caller site 2 -- `CountLeaderTargets(instrument)` arg | L6038-6045 | Production defect path. After fix, `targetsCount > 0` correctly suppresses retry arm. Caller unchanged. |
| `CountLeaderTargets` method | Not in scope | Unchanged. |
| `SnapshotBeTargets` method | Not in scope | Unchanged. |
| `QueueBeRetryFallback` method | Not in scope | Unchanged. |
| `_pendingFollowerBeSlots` type (`ConcurrentDictionary<string, byte>`) | Not in scope | Already correct. Lock-free. |
| L6139: `leaderCount <= 0` (partial-targets branch guard) | L6138-6143 | Different branch. MUST NOT change. |
| `(long)(int)Environment.TickCount` pattern elsewhere | Not in scope | .NET 4.8 pattern. Architecture locked. |
| `ActiveOrders .ToList()` | Not in scope | Deferred DW-NEXT-A-07. Architecture locked: stays. |

---

### xUnit Tests

**Test file**: `tests/PropTraderTools.Tests/RegisterBeRetrySlotIfNeededTests.cs`
(new file; alternatively append to `BwaveRefactorLaneBTests.cs` if the engineer determines
it reduces seam duplication -- see **Test Seam Note** below)

**Test framework**: xUnit `[Fact]` only. NEVER use NUnit or MSTest.

---

#### TEST 1 -- Bug Scenario (MUST fail before fix, MUST pass after fix)

```csharp
[Fact]
public void RegisterBeRetrySlotIfNeeded_LeaderZeroTargetsNonZero_DoesNotArmRetry()
```

**Preconditions**:
- `targetsCount = 2` (follower has 2 visible PTT/transitional targets to protect)
- `leaderCount = 0` (leader's native targets filled or cancelled -- normal lifecycle)
- `isRetry = false`
- Account is a follower account (`IsFollowerAccount(acc) == true`)
- Position is Long (non-flat) so `IsFlat` returns `false`

**Action**: Invoke `RegisterBeRetrySlotIfNeeded(acc, instrument, bufferTicks, isRetry: false, targetsCount: 2, leaderCount: 0)`.

**Assert**: `_pendingFollowerBeSlots` does NOT contain an entry keyed to `acc.Name` after the call.

**Rationale**: This is the exact spurious-cancel scenario. Before the fix, `leaderCount == 0` was
`true` so the retry-arming block executed, cancelling OCO protection on a follower that still had
2 working PTT targets. After the fix, `targetsCount == 0` is `false` (targetsCount is 2), so the
branch is skipped and `_pendingFollowerBeSlots` is not populated. This test is the regression guard
for the defect.

---

#### TEST 2 -- Correct Arm on Zero Follower Targets

```csharp
[Fact]
public void RegisterBeRetrySlotIfNeeded_TargetsZeroLeaderNonZero_ArmsRetry()
```

**Preconditions**:
- `targetsCount = 0` (follower has no visible targets -- PTT orders not yet landed)
- `leaderCount = 3` (leader has 3 native targets)
- `isRetry = false`
- Position is Long (non-flat) so `IsFlat` returns `false`

**Action**: Invoke `RegisterBeRetrySlotIfNeeded(acc, instrument, bufferTicks, isRetry: false, targetsCount: 0, leaderCount: 3)`.

**Assert**: `_pendingFollowerBeSlots` CONTAINS an entry keyed to `acc.Name` after the call (retry slot registered).

**Rationale**: Follower has no targets yet; correct to arm retry. Both pre-fix and post-fix code
pass this test -- confirms the fix does not regress the intended arm path.

---

#### TEST 3 -- Partial-Targets Arm (DW-B79-07 path)

```csharp
[Fact]
public void RegisterBeRetrySlotIfNeeded_PartialTargets_ArmsRetry()
```

**Preconditions**:
- `targetsCount = 1` (follower has 1 of 3 PTT targets visible so far)
- `leaderCount = 3` (leader has 3 native targets)
- `isRetry = false`
- Account is a follower account (`IsFollowerAccount(acc) == true`)
- Position is Long (non-flat) so `IsFlat` returns `false`

**Action**: Invoke `RegisterBeRetrySlotIfNeeded(acc, instrument, bufferTicks, isRetry: false, targetsCount: 1, leaderCount: 3)`.

**Assert**: `_pendingFollowerBeSlots` CONTAINS an entry keyed to `acc.Name` after the call (partial retry registered).

**Rationale**: Follower is partially protected -- 2 target pairs still outstanding (`targetsCount <
leaderCount`). Retry must arm. This exercises the `targetsCount < leaderCount` partial-targets path
(L6138-6143), which is unchanged by the fix. Confirms the second branch is unaffected.

---

#### Test Seam Note

`RegisterBeRetrySlotIfNeeded` is `private`. The engineer MUST use one of these two approaches:

**Option A -- Reflection (preferred if already established)**:
Use the private-method reflection seam pattern already established in
`BwaveRefactorLaneBTests.cs`. Example:
```csharp
var method = typeof(CopyEngine).GetMethod(
    "RegisterBeRetrySlotIfNeeded",
    BindingFlags.NonPublic | BindingFlags.Instance);
method.Invoke(_engine, new object[] { acc, instrument, bufferTicks, false, targetsCount, leaderCount });
```

**Option B -- Internal test-seam method**:
Add an `internal` forwarder following the `IsNativeLeaderTargetTestable` pattern at L5830.
`InternalsVisibleTo` is already declared at L46, so `[assembly: InternalsVisibleTo(...)]` is
already in scope. The forwarder MUST be a pure delegation (no logic), and MUST NOT add a new
branch (CCN must stay unchanged).

The engineer chooses whichever option avoids CCN regression. If Option B is chosen, the forwarder
counts as part of this ticket and must appear in the SCAN-1 check result.

---

### 7-Scan Checklist

The engineer MUST run every scan in order, confirm the stated result, and include scan output
in the ticket completion report. A single failure blocks the ticket.

| Scan ID | Command | Required Result |
|---------|---------|-----------------|
| SCAN-1 | `lizard src/PropTraderTools/CopyEngine.cs --CCN 8` | Warning count: 0. Fix adds no branches; `RegisterBeRetrySlotIfNeeded` CYC remains 6. If Option B test seam is chosen, confirm seam forwarder CYC = 1. |
| SCAN-2 | `Select-String -Pattern "lock\s*(" src/PropTraderTools/CopyEngine.cs` | 0 results in method bodies. `_pendingFollowerBeSlots` is `ConcurrentDictionary` -- assignment is lock-free. JS-021. |
| SCAN-3 | `Select-String -Pattern "async\s*void" src/PropTraderTools/CopyEngine.cs` | 0 results in method bodies. Fix introduces no async construct. JS-033. |
| SCAN-4 | ASCII-only: `[System.IO.File]::ReadAllBytes("src/PropTraderTools/CopyEngine.cs") \| Where-Object { $_ -gt 127 }` -- count = 0 | 0 bytes > 127 in changed lines. Fix is a pure ASCII token rename. JS-004 / mode rule. |
| SCAN-5 | `dotnet build` | 0 errors. Mandatory compile gate for any `.cs` change. |
| SCAN-6 | `dotnet test` | All prior tests pass (zero regression) + 3 new `[Fact]` tests pass. |
| SCAN-7 | `powershell -File scripts\ptt-sync-and-verify.ps1` | 0 MISMATCH lines. NT8 sync gate: copies `.cs` to NT8 bin and MD5-verifies all files match. |

---

### NT8 Sync Gate (mandatory after any `.cs` change)

After SCAN-5 and SCAN-6 pass, run:
```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```
Confirm: 0 MISMATCH lines in output. Then press **F5** in NinjaTrader 8 to recompile. Green = done.

---

### Completion Criteria

The engineer marks T1 complete when ALL of the following are true:

- [ ] L6118: `leaderCount == 0` changed to `targetsCount == 0`
- [ ] L6104: CYC comment updated (if secondary change applied)
- [ ] TEST 1 `RegisterBeRetrySlotIfNeeded_LeaderZeroTargetsNonZero_DoesNotArmRetry` passes
- [ ] TEST 2 `RegisterBeRetrySlotIfNeeded_TargetsZeroLeaderNonZero_ArmsRetry` passes
- [ ] TEST 3 `RegisterBeRetrySlotIfNeeded_PartialTargets_ArmsRetry` passes
- [ ] SCAN-1 through SCAN-7: all pass, zero violations
- [ ] F5 in NT8: green compile

---

*Ticket status: TICKETS_COMPLETE*
