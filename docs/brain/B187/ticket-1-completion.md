# B187 Ticket 1 Completion Report (RETRY)
## ptt-engineer Phase 4a -- RETRY after VERIFY_FAIL

**Date**: 2026-07-08 (retry)
**Engineer**: ptt-engineer
**Epic**: B187 -- QX Stop Snapshot Fix + DIAG Cleanup + CS0067 Suppress
**Ticket**: TICKET-1: DW-B187-QX-STOP-SNAP-01 -- leaderStop snapshot order fix [P1]
**Status**: BUILD_PASS

---

## Retry Scope

The VERIFY_FAIL was caused by a single defect: all 4 T_B187_* tests failed with
`System.IO.DirectoryNotFoundException` due to xUnit shadow-copy path resolution.

The .cs source reorder fix in `PttGlobalQuickExit.cs` was confirmed correct by Layer 3:
- SnapshotStopPrice line 58 < CancelPttBeOrders line 60 in Execute(Instrument) PASS
- SnapshotStopPrice line 143 < CancelPttBeOrders line 144 in Execute(List,Instrument) PASS
- SnapshotTargetOrders line 63 > CancelPttBeOrders line 60 -- DW-B126 maintained PASS

**Source change in this retry**: `CopyEngineTests.cs` only. `PttGlobalQuickExit.cs` unchanged.

---

## What Was Fixed

### Root Cause (from VERIFY_FAIL report)
All 4 tests used `System.Reflection.Assembly.GetExecutingAssembly().Location` with `../../..`
navigation. xUnit 2.x on net48 shadow-copies assemblies to `%TEMP%\{guid}\`, causing the
relative navigation to fail with `DirectoryNotFoundException`.

### Fix Applied

**1. Path resolution -- all 4 T_B187_* tests**

Replaced `Assembly.GetExecutingAssembly().Location` + 3-hop navigation with a
`[CallerFilePath]` helper method. `[CallerFilePath]` is captured by the compiler at build time
(the actual source file path `...\src\PropTraderTools\CopyEngineTests.cs`), which survives
xUnit shadow-copy because shadow-copy only moves the compiled assembly, not the source files.

Added to `LaneFTests` class (after `GetStaticMethod` helper, before T_B187_01 comment):

```csharp
private static string GetQxSourcePath(
    [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "")
{
    string dir = System.IO.Path.GetDirectoryName(callerFile) ?? string.Empty;
    return System.IO.Path.Combine(dir, "Features", "PttGlobalQuickExit.cs");
}
```

All 4 test methods now call `GetQxSourcePath()` instead of the broken path construction.

**2. Comment-line false positive -- T_B187_01, T_B187_02, T_B187_03**

The source file contains a comment on the line before `CancelPttBeOrders`:
```
// B78 DW-B63-01 + DW-B187: capture leaderStop BEFORE CancelPttBeOrders.
```
The search for `lines[i].Contains("CancelPttBeOrders")` was matching this comment, causing
tests T_B187_01 and T_B187_02 to find `CancelPttBeOrders` at the comment line (55/142) before
`SnapshotStopPrice` (58/143), reversing the expected ordering and failing the assertion.

Fix: Added `bool isComment = lines[i].TrimStart().StartsWith("//")` guard to all 3 search
loops. Non-code lines are skipped, so only actual call sites are matched.

---

## Files Modified

- `src/PropTraderTools/CopyEngineTests.cs`
  - Added `GetQxSourcePath()` helper method to `LaneFTests` class
  - T_B187_01: replaced path construction + added `isComment` filter
  - T_B187_02: replaced path construction + added `isComment` filter
  - T_B187_03: replaced path construction + added `isComment` filter
  - T_B187_04: replaced path construction (no comment filter needed -- `SnapshotTargetOrders` absent from list overload body)

---

## Test Results

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "T_B187_"

Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 137 ms
```

All 4 T_B187_* tests PASS.

---

## 7-Scan Results

All scans run against `git diff HEAD~1 HEAD -- src/` (new code only).

| Scan | Command | Result |
|------|---------|--------|
| SCAN-01 | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "lock\("` | **0 results** |
| SCAN-02 | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "return null"` | **0 results** |
| SCAN-03 | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "async void "` | **0 results** |
| SCAN-04 | CYC unchanged -- only CopyEngineTests.cs modified; PttGlobalQuickExit.cs untouched | **CYC=8 both methods (inherited from original)** |
| SCAN-05 | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "[^\x00-\x7F]"` | **0 results** |
| SCAN-06 | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "throw new"` | **0 results** |
| SCAN-07 | `dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 \| Select-Object -Last 10` | **Build succeeded. 0 Warning(s). 0 Error(s).** |

All 7 scans: ZERO violations.

---

## Acceptance Criteria Status

| AC | Description | Result |
|----|-------------|--------|
| AC-2 | SnapshotStopPrice < CancelPttBeOrders in Execute(Instrument) | PASS (line 58 < 60, confirmed Layer 3) |
| AC-3 | SnapshotStopPrice < CancelPttBeOrders in Execute(List,Instrument) | PASS (line 143 < 144, confirmed Layer 3) |
| AC-4 | SnapshotTargetOrders > CancelPttBeOrders in Execute(Instrument) | PASS (line 63 > 60, confirmed Layer 3) |
| AC-5 | SnapshotTargetOrders absent from Execute(List,Instrument) | PASS (confirmed Layer 3) |
| AC-6 | All 4 T_B187_* tests pass | **PASS -- 4/4 pass (this retry)** |
| AC-7 | SCAN-07 build passes | PASS -- 0 errors, 0 warnings |

---

## Return

**BUILD_PASS**
