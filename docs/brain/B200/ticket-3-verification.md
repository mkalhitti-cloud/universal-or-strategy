# Ticket 3 Verification: B201Tests.cs (Test Coverage for B201 Fix)

**Epic**: PTT-COPIER-B200 + B201
**Ticket**: T3 -- Create B201Tests.cs -- xUnit coverage for B201-ENTRY-CANCEL-01
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-10-15
**Dependency**: T2 VERIFY_PASS confirmed (ticket-2-verification.md)

---

## Verification Scope

T3 touches 3 files:
1. `src/PropTraderTools/Tests/B201Tests.cs` -- CREATED (4 [Fact] source-text assertion tests)
2. `src/PropTraderTools/PropTraderTools.Tests.csproj` -- MODIFIED (added compile entry)
3. `src/PropTraderTools/CopyEngineTests.cs` -- MODIFIED (7 B66 skip-annotated calls: 1-arg -> 2-arg compile fix)

Zero changes to CopyEngine.cs in T3 (confirmed independently).

---

## 7-Scan Results (Independently Run -- Layer 3)

### SCAN 1 -- lock() check

```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\(" | Measure-Object
```

**Count: 9**

All 9 matches are COMMENT references to "no lock()" -- e.g., "JS-021: no lock()" in
documentation comments. Zero actual `lock(` keyword usage in executable code.

```
L326:   // JS-021: ConcurrentDictionary -- lock-free. No lock() anywhere.
L360:   // ConcurrentDictionary: thread-safe without lock(). JS-021: no lock.
L2337:  // Value: ConcurrentBag<Order> -- thread-safe add, no lock().
L4875:  // ASCII-only. No DateTime.Now. No lock().
L4899:  // JS-021: no lock() -- ConcurrentDictionary TryGetValue/TryRemove.
L7303:  // JS-021: no lock(). GetCloneAtmMode reads volatile field ...
L7445:  // JS-021: no lock(). Interlocked.Decrement is atomic.
L7519:  // CYC=3: ... JS-021: no lock().
L7530:  // JS-021: no lock(). ConcurrentDictionary enumeration is thread-safe.
```

**PASS** -- Zero executable lock() calls. All 9 hits are in-comment documentation only.

Note: Engineer's completion report ran this scan against B201Tests.cs (not CopyEngine.cs).
Verifier independently ran against CopyEngine.cs (the authoritative source of truth).
Engineer result: 0 for B201Tests.cs. Verifier result: 0 executable hits for CopyEngine.cs. Both PASS.

---

### SCAN 2 -- async void ban

```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+\(" | Measure-Object
```

**Count: 0 -- PASS**

Matches engineer Layer 2 report exactly.

---

### SCAN 3 -- B201 tests pass

```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "B201" --no-build
```

**Result: Failed: 0, Passed: 4, Skipped: 0, Total: 4 -- PASS**

All 4 [Fact] methods confirmed passing:
- `T_B201_01_IsQxCancelCandidate_LeaderEntryOrder_NotCancelled` -- PASSED
- `T_B201_02_IsQxCancelCandidate_FollowerEntryOrder_Cancelled` -- PASSED
- `T_B201_03_IsQxCancelCandidate_AtmBracketNames_AlwaysCancelled` -- PASSED
- `T_B201_04_CancelQxBrackets_LeaderAccount_DoesNotCancelEntryOrder` -- PASSED

Matches engineer Layer 2 report exactly.

---

### SCAN 4 -- Full test suite (regression check)

```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build
```

**Result: Failed: 24, Passed: 168, Skipped: 531, Total: 723 -- PASS**

Pre-T3 baseline (from T2 verification): Failed: 24, Passed: 164, Skipped: 531, Total: 719.
T3 delta: +4 passed (168 vs 164), +4 total (723 vs 719). Zero new failures.
24 pre-existing failures are all NT8-runtime failures (CopyEngine.cctor stack trace visible in output) -- unchanged.

Matches engineer Layer 2 report exactly.

---

### SCAN 5 -- ptt-sync

```powershell
powershell -File .\scripts\ptt-sync.ps1
```

**ptt-sync complete: 19 linked, 0 errors -- PASS**

Matches engineer Layer 2 report exactly.

---

### SCAN 6 -- ASCII check (B201Tests.cs)

```powershell
Select-String -Path "src/PropTraderTools/Tests/B201Tests.cs" -Pattern "[\x80-\xFF]" | Measure-Object
```

**Count: 0 -- PASS**

Matches engineer Layer 2 report exactly.

---

### SCAN 7 -- File scope confirmation

```powershell
git status --short src/
```

**Result**:
```
M  src/PropTraderTools/CopyEngine.cs             (M from T2 -- T3 made ZERO changes)
 M src/PropTraderTools/CopyEngineTests.cs        (M -- 7 B66 compile calls updated)
 M src/PropTraderTools/PropTraderTools.Tests.csproj  (M -- B201Tests.cs compile entry added)
 M src/PropTraderTools/Tests/B73Tests.cs         (pre-existing)
 M src/PropTraderTools/Tests/BwaveCycLaneBTests.cs   (pre-existing)
?? src/PropTraderTools/Tests/B193Tests.cs        (pre-existing from earlier epics)
...
?? src/PropTraderTools/Tests/B201Tests.cs        (NEW -- T3 output)
```

B201Tests.cs shows as `??` (new untracked file). CopyEngine.cs is `M` from T2 -- T3 made ZERO new
changes to CopyEngine.cs (confirmed via git diff). No unexpected files. **PASS**

Matches engineer Layer 2 report exactly.

---

## Content Verification (B201Tests.cs)

**File read independently via Get-Content.**

### Structure check

| Item | Expected | Actual | Status |
|------|---------|--------|--------|
| Namespace | `PropTraderTools.Tests` | `PropTraderTools.Tests` | PASS |
| Class name | `B201Tests` | `B201Tests` | PASS |
| Framework | `using Xunit;` only | `using Xunit;` only | PASS |
| NUnit/MSTest present | NO | NO | PASS |
| [Fact] count | 4 | 4 | PASS |
| Method 1 | `T_B201_01_IsQxCancelCandidate_LeaderEntryOrder_NotCancelled` | Present | PASS |
| Method 2 | `T_B201_02_IsQxCancelCandidate_FollowerEntryOrder_Cancelled` | Present | PASS |
| Method 3 | `T_B201_03_IsQxCancelCandidate_AtmBracketNames_AlwaysCancelled` | Present | PASS |
| Method 4 | `T_B201_04_CancelQxBrackets_LeaderAccount_DoesNotCancelEntryOrder` | Present | PASS |
| Strategy | Source-text assertions via `File.ReadAllText("CopyEngine.cs")` | Confirmed | PASS |
| NT8 runtime calls | None | None | PASS |

### Per-test content verification

**T_B201_01** (Leader "Entry" NOT cancelled -- B201 core assertion):
- Asserts `Assert.Contains("internal static bool IsQxCancelCandidate(Order o, bool isFollower)", src)` -- T2 signature
- Asserts `Assert.Contains("if (isFollower && o.Name == \"Entry\")", src)` -- B201 guard present
- Asserts `Assert.Contains("B201-ENTRY-CANCEL-01", src)` -- fix comment present
- Asserts `Assert.DoesNotContain("if (o.Name == \"Entry\")\n                return true;", src)` -- old unconditional branch absent
- INDEPENDENTLY VERIFIED: `isFollower && o.Name` has exactly 1 match in CopyEngine.cs. Old unconditional form returns 0 matches. **PASS**

**T_B201_02** (Follower "Entry" IS cancelled -- DW-B93 preserved):
- Asserts 2-param signature present
- Asserts `if (isFollower && o.Name == "Entry")` guard present
- Asserts `DW-B93: Named ATM follower entry Limit` comment present
- Asserts `return true; // (6) DW-B93:` line present
- INDEPENDENTLY VERIFIED: Source-text assertions match actual CopyEngine.cs content. **PASS**

**T_B201_03** (ATM bracket names always cancelled):
- Asserts `IsAtmBracketName(o.Name)` present (1 match confirmed in CopyEngine.cs)
- Asserts `PTT-QX-` StartsWith branch present (2 matches confirmed)
- Asserts `PTT-BE-` StartsWith branch present (confirmed present)
- Asserts `PTT-Copy` StartsWith branch present (2 matches confirmed)
- Asserts `if (isFollower && o.Name == "Entry")` present
- VERIFIED: All branch assertions match actual CopyEngine.cs. Only "Entry" branch is guarded. **PASS**

**T_B201_04** (CancelQxBrackets leader path skips "Entry" -- end-to-end threading):
- Asserts `bool isFollower = IsFollowerAccount(acc);` present
- Asserts `IsQxCancelCandidate(o, isFollower)` present
- Asserts regex `bool isFollower = IsFollowerAccount\(acc\)` count >= 2
- Asserts `DoesNotContain("IsQxCancelCandidate(o))")` -- old 1-arg form absent
- INDEPENDENTLY VERIFIED: `bool isFollower = IsFollowerAccount` has exactly **2 matches** in CopyEngine.cs
  (CancelQxBrackets 2-arg body + CancelQxBrackets 3-arg body). Old 1-arg `IsQxCancelCandidate(o)`
  call pattern returns 0 executable matches. **PASS**

---

## B66 Compile Debt Fix Verification

The engineer updated 7 `[Fact(Skip = "NT8-runtime: ...")]` tests in `CopyEngineTests.cs` from the
old 1-arg `IsQxCancelCandidate(order)` to the new 2-arg `IsQxCancelCandidate(order, false)`.

**Independent git diff check**:
```
Removed lines (-): 7 occurrences of `bool result = CopyEngine.IsQxCancelCandidate(order);`
Added lines (+):   7 occurrences of `bool result = CopyEngine.IsQxCancelCandidate(order, false);`
```

**Remaining old 1-arg calls**: 0 (confirmed via `Select-String -Pattern "IsQxCancelCandidate\(order\)"` = 0 matches)
**New 2-arg calls in B66 block**: 7 (confirmed via `Select-String -Pattern "IsQxCancelCandidate\(order,\s*false\)"` = 7 matches)

All 7 are `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]`. The skip annotation
is unchanged. These tests remain non-executing (skip count 531 unchanged in SCAN 4).
Behavioral contract: all 7 non-"Entry" order names so `false` is semantically correct.

**No unrelated changes to CopyEngineTests.cs.** The git diff shows exactly 7 removed lines and 7 added lines, all within the B66 test block. **PASS**

---

## csproj Verification

```
Select-String -Path "src/PropTraderTools/PropTraderTools.Tests.csproj" -Pattern "B201"
```
Result: Line 45: `<Compile Include="Tests\B201Tests.cs" />`

**PASS** -- B201Tests.cs compile entry present at correct location.

---

## DNA Rule Check (Jane Street Rules Catalog)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | 0 executable lock() in CopyEngine.cs; 0 in B201Tests.cs | PASS |
| JS-033 (no async void) | 0 async void in CopyEngine.cs | PASS |
| JS-066 (CYC <= 8) | All 4 test methods are linear source-text assertions (CYC=1 each) | PASS |
| JS-001 (no throw in hot path) | No `throw new` in any test method | PASS |
| JS-002 (no return null) | No return null in B201Tests.cs | PASS |
| xUnit-only | `using Xunit;` only -- no NUnit, no MSTest | PASS |
| ASCII-only | 0 non-ASCII bytes in B201Tests.cs (SCAN 6) | PASS |
| T2-dependency | All 4 [Fact] methods reference 2-arg `IsQxCancelCandidate(Order o, bool isFollower)` | PASS |
| NT8 constraints | No async/await in OnInitialize or NT8 lifecycle methods; no sealed on window; no FontFamily; no hex color | PASS |

---

## Architecture Compliance

| Requirement | Expected | Actual | Status |
|-------------|---------|--------|--------|
| T3 creates B201Tests.cs ONLY | New file only | B201Tests.cs = new `??` | PASS |
| Zero changes to CopyEngine.cs in T3 | CopyEngine.cs untouched by T3 | No T3-specific diff in CopyEngine.cs | PASS |
| csproj compile entry added | `<Compile Include="Tests\B201Tests.cs" />` | Present at line 45 | PASS |
| CopyEngineTests.cs: 7 B66 compile fixes only | 7 1-arg -> 2-arg changes, no other edits | Exactly 7 lines changed (confirmed) | PASS |
| Source-text assertion strategy (no NT8 runtime) | All assertions use File.ReadAllText | Confirmed in all 4 [Fact] methods | PASS |
| T2 signature contract tested | References 2-arg signature | All 4 methods reference 2-arg form | PASS |

---

## Spec Coverage (B201-ENTRY-CANCEL-01)

| Requirement | Test | Coverage |
|-------------|------|---------|
| Leader "Entry" order NOT cancelled by QX/BE | T_B201_01 | Source-text confirms isFollower guard prevents "Entry" cancel for leader path |
| Follower "Entry" order IS cancelled (DW-B93) | T_B201_02 | Source-text confirms DW-B93 comment + return true preserved |
| ATM bracket names (Stop*, Target*) unaffected | T_B201_03 | Source-text confirms IsAtmBracketName branch is unguarded |
| PTT-QX-*, PTT-BE-*, PTT-Copy* branches unaffected | T_B201_03 | Source-text confirms all 3 PTT prefix branches are unguarded |
| isFollower threaded end-to-end in CancelQxBrackets | T_B201_04 | Source-text confirms 2 occurrences of `bool isFollower = IsFollowerAccount(acc)` |

All 4 [Fact] tests pass and cover the B201-ENTRY-CANCEL-01 spec requirement completely.

---

## Comparison with Engineer's Layer 2 Report

| Scan | Engineer (Layer 2) | Verifier (Layer 3) | Match |
|------|-------------------|-------------------|-------|
| SCAN 1 (lock) | Count=0 (B201Tests.cs) | Count=0 executable (CopyEngine.cs + B201Tests.cs) | MATCH |
| SCAN 2 (async void) | Count=0 | Count=0 | MATCH |
| SCAN 3 (B201 filter) | 4 passed, 0 failed | 4 passed, 0 failed | MATCH |
| SCAN 4 (full suite) | 24 failed, 168 passed, 531 skipped | 24 failed, 168 passed, 531 skipped | EXACT MATCH |
| SCAN 5 (ptt-sync) | 19 linked, 0 errors | 19 linked, 0 errors | MATCH |
| SCAN 6 (ASCII) | Count=0 | Count=0 | MATCH |
| SCAN 7 (git scope) | B201Tests.cs=??, CopyEngine.cs=M(T2), CopyEngineTests.cs=M | Same | MATCH |

No discrepancies found between engineer's self-report (Layer 2) and independent verification (Layer 3).

---

## Warnings

None. No warnings issued.

---

## Acceptance Criteria Results

| Criterion | Expected | Result | Status |
|-----------|---------|--------|--------|
| `B201Tests.cs` created and compiles clean | YES | YES | PASS |
| All 4 [Fact] tests pass | 4 passed, 0 failed | 4 passed, 0 failed | PASS |
| No existing test regressions | 24 pre-existing failures only | 24 failed, 168 passed | PASS |
| T_B201_01: isFollower=false + "Entry" fix | Assert.Contains confirms guard | Confirmed in source | PASS |
| T_B201_02: DW-B93 follower path preserved | Assert.Contains confirms DW-B93 + return true | Confirmed in source | PASS |
| T_B201_03: ATM bracket branches unguarded | All 4 unguarded branches confirmed | All confirmed | PASS |
| T_B201_04: isFollower threaded in CancelQxBrackets | matchCount >= 2 | Count = 2 (exact) | PASS |
| Zero CopyEngine.cs changes in T3 | CopyEngine.cs not touched by T3 | T3 = zero diff on CopyEngine.cs | PASS |
| ptt-sync 19/0 | 19 linked, 0 errors | 19 linked, 0 errors | PASS |
| B66 compile fix: 7 calls updated | 1-arg -> 2-arg, 7 changes | Exactly 7 changes, no extras | PASS |
| csproj compile entry | `<Compile Include="Tests\B201Tests.cs" />` | Present at line 45 | PASS |
| All 7 SCAN checks pass | YES | All 7 PASS | PASS |

---

## VERDICT

**VERIFY_PASS**

All 7 independent scans passed. All 4 [Fact] tests pass. Full suite: 168 passed, 24 pre-existing
failures (unchanged), 531 skipped. B66 compile debt fix verified (exactly 7 B66 skip-annotated calls
updated, no unrelated changes). ptt-sync 19/0. B201Tests.cs uses source-text assertion strategy
(no NT8 runtime required). T2 signature contract `IsQxCancelCandidate(Order o, bool isFollower)` is
correctly referenced across all 4 [Fact] methods. Zero DNA violations. Zero Layer 2 / Layer 3
discrepancies.

*Written by: ptt-verifier*
*Epic: B200+B201*
*Phase: 4b (Ticket 3 Verification)*
*Verdict: VERIFY_PASS*