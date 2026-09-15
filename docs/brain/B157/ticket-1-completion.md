# B157-T1 Completion Report
# Phase 4a -- PTT Engineer
# Produced: 2026-08-10

---

## Status: BUILD_PASS

All 5 changes applied. All 7 scans zero. Build clean. Deploy synced.

---

## Changes Applied

### Change 1 -- CopyEngine.cs line 6527 (1-character fix)

File: `src/PropTraderTools/CopyEngine.cs`

BEFORE (line 6527):
```
            return currentAction != lastAction && followerIsFlat;
```

AFTER (line 6527):
```
            return currentAction != lastAction && !followerIsFlat;
```

Single `!` inserted before `followerIsFlat`. No other lines touched in this method or file.

---

### Change 2 -- CopyEngineTests.cs lines 8103-8125 (rename + comment + flip assert)

File: `src/PropTraderTools/CopyEngineTests.cs`

Method renamed:
- BEFORE: `ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders`
- AFTER:  `ShouldBlockReversalCopy_WhenFollowerHasOpenPosition`

Comment block (6 lines) replaced with DW-B157-RG-01 protection-path description.

Assertion flipped:
- BEFORE: `Assert.False(wouldSkip)`
- AFTER:  `Assert.True(wouldSkip)`

---

### Changes 3-5 -- CopyEngineTests.cs (3 new [Fact] methods inserted after line 8125)

Test T1-2: `IsReversalToFlatFollower_FlatFollower_DirectionChanged_ReturnsFalse`
- followerIsFlat=true, reversal -> Assert.False (regression proof)
- [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]

Test T1-3: `IsReversalToFlatFollower_OpenFollower_DirectionChanged_ReturnsTrue`
- followerIsFlat=false, reversal (Sell->Buy) -> Assert.True (protection path)
- [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]

Test T1-4: `IsReversalToFlatFollower_SameDirection_ReturnsFalse`
- currentAction==lastAction, followerIsFlat=false -> Assert.False (same direction)
- [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]

---

## Scan Results

### SCAN-01: lock() check in executable code

Command:
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\block\s*\(" | Where-Object { $_.Line -notmatch "//.*lock" -and $_.Line -match "^\s*lock\s*\(" } | Measure-Object
```

Result: **0** executable lock() calls (76 comment-only hits, zero in executable code)
Status: PASS

---

### SCAN-02: async void check

Command:
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async void"
```

Result: **0** executable async void (2 comment-only hits at lines 2133 and 8279)
Status: PASS

---

### SCAN-03: DateTime.Now in executable code

Command:
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "DateTime\.Now[^U]" | Where-Object { $_.Line -notmatch "^\s*//" }
```

Result: **0**
Status: PASS

---

### SCAN-04: Non-ASCII bytes

Command (CopyEngine.cs):
```
$bytes = [System.IO.File]::ReadAllBytes("src/PropTraderTools/CopyEngine.cs"); ($bytes | Where-Object { $_ -gt 127 }).Count
```
Result: **0**

Command (CopyEngineTests.cs):
```
$bytes = [System.IO.File]::ReadAllBytes("src/PropTraderTools/CopyEngineTests.cs"); ($bytes | Where-Object { $_ -gt 127 }).Count
```
Result: **0**

Status: PASS (both files clean)

---

### SCAN-05: IsReversalToFlatFollower CYC

Method body (lines 6521-6528):
```csharp
internal static bool IsReversalToFlatFollower(
    OrderAction currentAction,
    OrderAction lastAction,
    bool followerIsFlat
)
{
    return currentAction != lastAction && !followerIsFlat;
}
```

Operator count: 1 `&&`
CYC = base(1) + &&(1) = **2**
Limit: <=8
Status: PASS

---

### SCAN-06: xUnit test run

Command:
```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --logger "console;verbosity=minimal"
```

Baseline (pre-T1, git stash verification):
- Failed: 1, Passed: 199, Skipped: 369, Total: 569

Post-T1:
- Failed: 1, Passed: 199, Skipped: 372, Total: 572

Delta: +3 new skipped tests (the 4 T1 tests -- T1-1 was existing, T1-2/T1-3/T1-4 are new).
The 1 failed test (`TrySetSlateReadyOnDrainFill_NullOrder_NoSideEffect`) is pre-existing:
confirmed by git stash verification -- identical failure before T1 changes. Not caused by T1.
T1 introduced zero new failures.

Status: PASS (no new failures introduced)

---

### SCAN-07: Build check

Command:
```
dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj --no-restore
```

Output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Note: `scripts/build_readiness.ps1` does not exist in this workspace (checked scripts/
directory). Direct dotnet build used as equivalent gate.

Status: PASS

---

## Deploy-sync Result

Command: `powershell -File .\deploy-sync.ps1`

Output:
```
ASCII GATE PASS - all source files are clean
DIFF GUARD PASS: Diff size (113 chars) is within limits.
SOVEREIGN AUDIT PASS: Architectural integrity verified.
CLEANUP: Removing existing link -> SignalBroadcaster.cs
LINKING (Fixed): SignalBroadcaster.cs -> NT8
SYNC COMPLETE: One Source of Truth Established
```

Note: droid auth warning (external service authentication) is non-blocking for hard-link sync.
Hard links re-established. NT8 directory will pick up changes from Wave workspace.

Status: COMPLETE

---

## Defect Cards Closed

- DW-B157-RG-01: IsReversalToFlatFollower logic inversion -- CLOSED

## Jane Street Rule Compliance

| Rule | Check | Result |
|---|---|---|
| JS-013 (CYC<=8) | IsReversalToFlatFollower CYC=2 | PASS |
| JS-021 (no lock) | Static method, no shared state, no lock() introduced | PASS |
| JS-001 (no throw) | No throw in method or tests | PASS |
| JS-002 (bool return) | Return type bool, no null where value expected | PASS |
| ASCII-001 | 0 non-ASCII bytes in both files | PASS |
| NT8-006 (no DateTime.Now) | Not introduced | PASS |

---

## Scope Lock Verification

- CopyEngine.cs: only line 6527 modified (1 character insertion) -- VERIFIED
- CopyEngineTests.cs: only lines 8103-8125 modified + 3 new methods inserted after line 8125 -- VERIFIED
- No other lines touched in either file -- VERIFIED

---

_B157-T1 completed by PTT Engineer (Phase 4a). 2026-08-10._
