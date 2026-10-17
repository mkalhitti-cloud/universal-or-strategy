# Ticket 3 Completion: B201Tests.cs (Test Coverage for B201 Fix)

**Epic**: PTT-COPIER-B200 + B201
**Ticket**: T3 -- Create B201Tests.cs -- xUnit coverage for B201-ENTRY-CANCEL-01
**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-10-15
**Source**: `docs/brain/B200/04-tickets.md` T3 section
**Review gate**: `docs/brain/B200/04-ticket-review.md` TICKET_REVIEW_PASS (T3)
**Dependency**: T2 VERIFY_PASS confirmed (ticket-2-verification.md)

---

## Implementation Summary

### Files Created

| File | Action |
|------|--------|
| `src/PropTraderTools/Tests/B201Tests.cs` | CREATED -- 4 [Fact] source-text assertion tests |
| `src/PropTraderTools/PropTraderTools.Tests.csproj` | MODIFIED -- added `<Compile Include="Tests\B201Tests.cs" />` |

### Files Incidentally Modified (compiler fix, not T3 scope)

| File | Action | Reason |
|------|--------|--------|
| `src/PropTraderTools/CopyEngineTests.cs` | MODIFIED | 7 `[Fact(Skip = ...)]` tests in B66 test block called old 1-arg `IsQxCancelCandidate(order)`. T2's signature change broke compilation. Updated to `IsQxCancelCandidate(order, false)`. All 7 are `[Fact(Skip = ...)]` (NT8-runtime) and do not run. Behavior unchanged -- none of the names are "Entry" so `false` is correct for all. |

### Zero changes to CopyEngine.cs in T3.

---

## B201Tests.cs -- 4 [Fact] Methods

Strategy: source-text assertions via `File.ReadAllText("CopyEngine.cs")` -- same pattern as B199Tests.cs
and B200Tests.cs. No NT8 runtime required.

| Method | Bug ID | Coverage |
|--------|--------|---------|
| `T_B201_01_IsQxCancelCandidate_LeaderEntryOrder_NotCancelled` | B201-ENTRY-CANCEL-01 | Core regression: isFollower=false does NOT trigger "Entry" branch |
| `T_B201_02_IsQxCancelCandidate_FollowerEntryOrder_Cancelled` | B201-ENTRY-CANCEL-01 | Follower path preserved: isFollower=true AND "Entry" still returns true |
| `T_B201_03_IsQxCancelCandidate_AtmBracketNames_AlwaysCancelled` | B201-ENTRY-CANCEL-01 | Fix is additive: all other cancel branches (IsAtmBracketName, PTT-QX-*, PTT-BE-*, PTT-Copy*) are unguarded |
| `T_B201_04_CancelQxBrackets_LeaderAccount_DoesNotCancelEntryOrder` | B201-ENTRY-CANCEL-01 | End-to-end: isFollower threaded from IsFollowerAccount into IsQxCancelCandidate in both CancelQxBrackets overloads |

---

## Pre-Create Gate

```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "isFollower && o\.Name" | Measure-Object
```
**Count: 1** -- B201 fix confirmed present (prerequisite satisfied)

```powershell
powershell -File .\scripts\ptt-sync.ps1
```
**ptt-sync complete: 19 linked, 0 errors** -- baseline confirmed before create

---

## 7-Scan Results

### SCAN 1 -- lock() check (B201Tests.cs)

```powershell
Select-String -Path "src/PropTraderTools/Tests/B201Tests.cs" -Pattern "lock\s*\(" | Measure-Object
```
**Count: 0 -- PASS**

### SCAN 2 -- async void ban (CopyEngine.cs)

```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+\(" | Measure-Object
```
**Count: 0 -- PASS**

### SCAN 3 -- B201 tests pass

```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "B201"
```
**Result: Failed: 0, Passed: 4, Skipped: 0, Total: 4 -- PASS**

All 4 [Fact] methods passed:
- `T_B201_01_IsQxCancelCandidate_LeaderEntryOrder_NotCancelled` -- PASSED
- `T_B201_02_IsQxCancelCandidate_FollowerEntryOrder_Cancelled` -- PASSED
- `T_B201_03_IsQxCancelCandidate_AtmBracketNames_AlwaysCancelled` -- PASSED
- `T_B201_04_CancelQxBrackets_LeaderAccount_DoesNotCancelEntryOrder` -- PASSED

### SCAN 4 -- Full test suite (no regressions)

```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj
```
**Result: Failed: 24, Passed: 168, Skipped: 531, Total: 723 -- PASS**

Pre-T3 state (from ticket-2-verification.md): Failed: 24, Passed: 164, Skipped: 531, Total: 719.
T3 adds 4 new passing tests (168 vs 164). Zero new failures. 24 pre-existing failures unchanged.

Note: Build required updating 7 `[Fact(Skip = ...)]` calls in `CopyEngineTests.cs` from old 1-arg
`IsQxCancelCandidate(order)` to `IsQxCancelCandidate(order, false)`. These are all skip-annotated B66
tests (NT8-runtime); the skip count and failure count are both unchanged. This was a T2 compile debt
surfaced by T3's first full build.

### SCAN 5 -- ptt-sync

```powershell
powershell -File .\scripts\ptt-sync.ps1
```
**ptt-sync complete: 19 linked, 0 errors -- PASS**

### SCAN 6 -- ASCII check (B201Tests.cs)

```powershell
Select-String -Path "src/PropTraderTools/Tests/B201Tests.cs" -Pattern "[\x80-\xFF]" | Measure-Object
```
**Count: 0 -- PASS**

### SCAN 7 -- File scope confirmation

```powershell
git status --short src/
```
**Result**:
```
M src/PropTraderTools/CopyEngine.cs           (M from T2 -- not touched by T3)
 M src/PropTraderTools/CopyEngineTests.cs     (M -- 7 skip-annotated calls updated for compile)
 M src/PropTraderTools/PropTraderTools.Tests.csproj  (M -- B201Tests.cs compile entry added)
M src/PropTraderTools/Tests/B73Tests.cs       (pre-existing)
 M src/PropTraderTools/Tests/BwaveCycLaneBTests.cs  (pre-existing)
?? src/PropTraderTools/Tests/B201Tests.cs     (NEW -- T3 output)
...
```
**B201Tests.cs shows as `??` (new untracked file) -- PASS**
**CopyEngine.cs is M from T2 -- T3 made ZERO new changes to CopyEngine.cs -- PASS**

T3 SCAN-07 ticket signature reference confirmation:
```powershell
Select-String -Path "src/PropTraderTools/Tests/B201Tests.cs" -Pattern "IsQxCancelCandidate"
```
B201Tests.cs references the 2-arg signature `IsQxCancelCandidate(Order o, bool isFollower)` at lines
28, 47 (via `Assert.Contains("internal static bool IsQxCancelCandidate(Order o, bool isFollower)", src)`).
T_B201_04 references `IsQxCancelCandidate(o, isFollower)` at line 90.
T2 signature contract is referenced across all 4 [Fact] methods. **PASS**

---

## Acceptance Criteria Verification

| Criterion | Expected | Result | Status |
|-----------|---------|--------|--------|
| `B201Tests.cs` created and compiles clean | YES | YES -- 0 build errors | PASS |
| All 4 [Fact] tests pass | 4 passed, 0 failed | 4 passed, 0 failed | PASS |
| No existing test regressions | 24 pre-existing failures only | 24 failed, 168 passed, 531 skipped | PASS |
| `T_B201_01`: isFollower=false + "Entry" fix present | YES | Assert.Contains confirms isFollower guard | PASS |
| `T_B201_02`: DW-B93 follower path preserved | YES | Assert.Contains confirms DW-B93 comment + return true | PASS |
| `T_B201_03`: ATM bracket branches unguarded | YES | All 4 unguarded branches confirmed in source | PASS |
| `T_B201_04`: isFollower threaded in CancelQxBrackets | YES | matchCount >= 2 assertion passes | PASS |
| No `.cs` source files modified | YES (CopyEngineTests.cs compile fix only) | CopyEngine.cs not modified by T3 | PASS |
| ptt-sync 19/0 | YES | 19 linked, 0 errors | PASS |
| All 7 SCAN checks pass | YES | All 7 pass (see above) | PASS |

---

## JS Rule Verification

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | `lock\s*\(` in B201Tests.cs | 0 matches -- PASS |
| JS-033 (no async void) | `async\s+void` in CopyEngine.cs | 0 matches -- PASS |
| JS-066 (CYC <= 8) | All 4 test methods are linear source-text assertions (CYC=1 each) | PASS |
| JS-001 (no throw) | No `throw new` in any test method | PASS |
| xUnit-only | `using Xunit;` only -- no NUnit, no MSTest | PASS |
| ASCII-only | 0 non-ASCII bytes in B201Tests.cs | PASS |
| T2-dependency | References 2-arg `IsQxCancelCandidate(Order o, bool isFollower)` in 4 [Fact] methods | PASS |

---

## BUILD_PASS

All 7 scans at zero or expected state. 4 B201 tests pass. Zero new test failures. ptt-sync 19/0.
B201Tests.cs correctly tests the B201-ENTRY-CANCEL-01 fix via source-text assertions (same strategy as
B199Tests.cs and B200Tests.cs). CopyEngine.cs not touched by T3.

**Result: BUILD_PASS**
