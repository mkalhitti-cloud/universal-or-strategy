# Ticket 1 Completion Report: DW-LB-GR-01 BE Retry Logic Bug Fix

**Block**: DW-LB-GR-01
**Ticket**: T1 -- Fix RegisterBeRetrySlotIfNeeded Guard Condition
**Phase**: 4a -- Engineer
**Date**: 2026-09-07
**Engineer**: ptt-engineer
**Scope**: Ticket 1 ONLY (DW-LB-GR-01)

---

## Rules Catalog P0 Gate

**Gate result: PASS**

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (P0) | No `lock()` in method bodies. Fix introduces no lock. `_pendingFollowerBeSlots` is `ConcurrentDictionary`. | PASS |
| JS-001 (P0) | No `throw new XxxException` in hot paths. Fix introduces no throw. | PASS |
| JS-002 (P0) | No `return null`. Method is `void`. Fix introduces no null return. | PASS |
| JS-033 (P0) | No `async void`. Fix is a synchronous 1-token rename. | PASS |

---

## Change Applied

### Change 1 -- Logic Fix (PRIMARY, MANDATORY)

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Line**: 6118

**OLD** (buggy):
```csharp
            if (leaderCount == 0) // (2) targets==0 path
```

**NEW** (correct):
```csharp
            if (targetsCount == 0) // (2) targets==0 path
```

**Token change count**: 1 (`leaderCount` -> `targetsCount` on L6118 only).

**Why**: The condition at L6118 gates the "targets=0 path" -- the branch that arms a retry slot
and calls `QueueBeRetryFallback`. The semantic intent is: "if the follower has no visible PTT
targets to protect, arm a retry." Using `leaderCount` (leader's native Target1..9 count) was wrong --
it triggered retry arm whenever the leader had no native targets (normal post-fill state), regardless
of whether the follower still had working PTT targets (`targetsCount > 0`). This tore down OCO
protection spuriously. `targetsCount` (follower's visible target order count from `SnapshotBeTargets`)
is the correct gate.

---

### Change 2 -- Comment Update (SECONDARY)

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Line**: 6104

**OLD**:
```
        // CYC<=6: isRetry(1) + IsFlat(2) + leaderCount==0 branch(3) + IsFollowerAccount(4)
```

**NEW**:
```
        // CYC<=6: isRetry(1) + IsFlat(2) + targetsCount==0 branch(3) + IsFollowerAccount(4)
```

**Why**: CYC annotation now describes the corrected predicate. Comment-only, no logic change.

---

## Architecture Locks Confirmed Untouched

| Item | Location | Status |
|------|----------|--------|
| Method signature of `RegisterBeRetrySlotIfNeeded` | L6107-L6114 | UNCHANGED |
| Caller site 1 -- `leaderCount: 0` hardcode | L6026-6035 | UNCHANGED |
| Caller site 2 -- `CountLeaderTargets(instrument)` arg | L6038-6045 | UNCHANGED |
| L6139: `leaderCount <= 0` (partial-targets branch guard) | L6138-6143 | UNCHANGED |

---

## Tests Added

**Test file**: `tests/PropTraderTools.Tests/RegisterBeRetrySlotIfNeededTests.cs` (new file)  
**Approach**: Inline predicate mirror (established project pattern, matching `CopyEngineB137Tests.cs`,
`BwaveRefactorLaneBTests.cs`, `CopyEngineBreakEvenFollowerTests.cs`).

NT8 `Account`/`Instrument`/`Position` are not instantiable without the NT8 runtime. Test project
targets `net8.0`; `PropTraderTools` targets `net48`. No `ProjectReference` possible. Inline
`RegisterBeRetryWouldArmInline(isRetry, isFlat, isFollower, targetsCount, leaderCount)` mirrors
the exact guard logic of the production method.

**No seam added to production code**: Inline mirror pattern chosen. No CYC regression.

### Test 1
```csharp
[Fact]
public void RegisterBeRetrySlotIfNeeded_LeaderZeroTargetsNonZero_DoesNotArmRetry()
```
- Bug scenario: `targetsCount=2`, `leaderCount=0`, `isRetry=false`, `isFollower=true`, `isFlat=false`
- Before fix: `leaderCount==0` was TRUE -> would arm spuriously. After fix: `targetsCount==0` is FALSE -> does NOT arm.
- **Assert**: `wouldArm == false` -- PASS

### Test 2
```csharp
[Fact]
public void RegisterBeRetrySlotIfNeeded_TargetsZeroLeaderNonZero_ArmsRetry()
```
- Correct arm: `targetsCount=0`, `leaderCount=3`, `isRetry=false`, `isFollower=true`, `isFlat=false`
- **Assert**: `wouldArm == true` -- PASS

### Test 3
```csharp
[Fact]
public void RegisterBeRetrySlotIfNeeded_PartialTargets_ArmsRetry()
```
- Partial-targets (DW-B79-07 path): `targetsCount=1`, `leaderCount=3`, `isRetry=false`, `isFollower=true`, `isFlat=false`
- **Assert**: `wouldArm == true` -- PASS

---

## Scan Results

### SCAN-1: lizard CCN check

**Command**: `python -m lizard src/PropTraderTools/CopyEngine.cs --CCN 8`

**Result**:
```
No thresholds exceeded (cyclomatic_complexity > 8 or length > 1000 or nloc > 1000000 or parameter_count > 100)
Total nloc   Avg.NLOC  AvgCCN  Avg.token   Fun Cnt  Warning cnt   Fun Rt   nloc Rt
      4923      12.9     4.0       66.4      366            0      0.00    0.00
```

`RegisterBeRetrySlotIfNeeded` CCN = 6 (confirmed from lizard output: `54 8 198 6 54`).  
**Warning count: 0. PASS.**

---

### SCAN-2: lock() check

**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\("`

**Result**: All matches are comment text only (e.g., `// JS-021: ... lock-free. No lock() anywhere.`).  
**0 `lock(` in method bodies. PASS.**

---

### SCAN-3: async void check

**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void"`

**Result**: Both matches are comment text only (e.g., `// JS-033: Tick is not async void`).  
**0 `async void` in method bodies. PASS.**

---

### SCAN-4: ASCII check

**Command**: `[System.IO.File]::ReadAllBytes(...) | Where-Object { $_ -gt 127 }`

**Result**:
```
Non-ASCII byte count: 0
```
**0 non-ASCII bytes. PASS.**

---

### SCAN-5: dotnet build

**Command**: `dotnet build src/PropTraderTools/PropTraderTools.csproj`

**Result**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```
**0 errors. PASS.**

---

### SCAN-6: dotnet test

**Command**: `dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj`

**Result**:
```
Passed!  - Failed: 0, Passed: 66, Skipped: 3, Total: 69, Duration: 49 ms
```

Prior test count: 63 passing. New count: 66 passing (+3 new tests). 0 regressions. 3 skips are pre-existing.  
**All prior tests pass + 3 new [Fact] tests pass. PASS.**

---

### SCAN-7: NT8 sync

**Command**: `powershell -File scripts\ptt-sync-and-verify.ps1`

**Result**:
```
OK       CopyEngine.cs
OK       TradeCopierPanel.cs
OK       TradeCopierWindow.cs
OK       Core\PttContracts.cs
OK       Features\PttBreakEven.cs
OK       Features\PttBreakEvenSwap.cs
OK       Features\PttCancel.cs
OK       Features\PttCopier.cs
OK       Features\PttFlatten.cs
OK       Features\PttFollowerStrategy.cs
OK       Features\PttGlobalBreakEven.cs
OK       Features\PttGlobalQuickExit.cs
OK       Features\PttQuickExit.cs
OK       Features\PttTrim.cs

=== SYNC + VERIFY: PASS (18 files confirmed) ===
```
**0 MISMATCH lines. PASS.**

**Next step (mandatory)**: Press F5 in NinjaTrader 8 to recompile.

---

## Summary of All 7 Scans

| Scan | Command | Result | Status |
|------|---------|--------|--------|
| SCAN-1 | lizard CCN check | 0 warnings, CCN=6 for `RegisterBeRetrySlotIfNeeded` | PASS |
| SCAN-2 | lock() in method bodies | 0 (all hits in comments) | PASS |
| SCAN-3 | async void in method bodies | 0 (all hits in comments) | PASS |
| SCAN-4 | ASCII bytes > 127 | 0 | PASS |
| SCAN-5 | dotnet build | 0 errors | PASS |
| SCAN-6 | dotnet test | 66 passed, 0 failed (+3 new) | PASS |
| SCAN-7 | ptt-sync-and-verify | 0 MISMATCH, 18 files OK | PASS |

---

## Completion Checklist

- [x] L6118: `leaderCount == 0` changed to `targetsCount == 0`
- [x] L6104: CYC comment updated (secondary change applied)
- [x] TEST 1 `RegisterBeRetrySlotIfNeeded_LeaderZeroTargetsNonZero_DoesNotArmRetry` passes
- [x] TEST 2 `RegisterBeRetrySlotIfNeeded_TargetsZeroLeaderNonZero_ArmsRetry` passes
- [x] TEST 3 `RegisterBeRetrySlotIfNeeded_PartialTargets_ArmsRetry` passes
- [x] SCAN-1 through SCAN-7: all pass, zero violations
- [ ] F5 in NT8: green compile (manual step, pending user action)

---

## BUILD_PASS
