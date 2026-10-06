# B191 Ticket-3 Completion Note
# T3 -- Restore followerPosQty in ExecuteFollowers (closes DW-B187-01)

**Epic**: B191
**Ticket**: T3
**Engineer**: ptt-engineer
**Phase**: 4a (Implementation)
**Status**: BUILD_PASS

---

## Ticket Scope

T3 is a surgical edit to `ExecuteFollowers` in `PttGlobalQuickExit.cs`.
Two changes only:
1. Add one new `int followerPosQty` local variable declaration BEFORE the `ResolveFollowerTargets` call.
2. Change the third argument to `ResolveFollowerTargets` from literal `0` to `followerPosQty`.

No other lines in the file were changed.

---

## Files Written

```
src/PropTraderTools/Features/PttGlobalQuickExit.cs  (EDITED -- surgical change only)
```

---

## FindPositionPublic Verification

Before editing, `CopyEngine.cs` was searched for `FindPositionPublic`:

```
CopyEngine.cs:5539  internal Position FindPositionPublic(Account acc, Instrument instrument) =>
                        FindPosition(acc, instrument);
```

Confirmed:
- `FindPositionPublic` EXISTS as an `internal` method (line 5539)
- Signature: `internal Position FindPositionPublic(Account acc, Instrument instrument)`
- Returns: `Position` (with `.Quantity` property) or `null` (pre-existing contract per comment at line 5537)
- `FindPositionPublic` (internal wrapper) was used -- NOT `FindPosition` (private)

---

## Exact Lines Changed

### Before (lines 215-221, pre-T3)

```csharp
                    // DW-B124: when follower snapshot is empty (BE-ALL consumed native brackets),
                    // derive qty array from leader snapshot scaled by posQty ratio.
                    // Prevents CalcTNQty arithmetic fallback from wrong tranche split.
                    followerTargets = ResolveFollowerTargets(
                        followerTargets,
                        targets,
                        0,
                        pos.Quantity
                    );
```

### After (lines 215-225, post-T3)

```csharp
                    // DW-B124: when follower snapshot is empty (BE-ALL consumed native brackets),
                    // derive qty array from leader snapshot scaled by posQty ratio.
                    // Prevents CalcTNQty arithmetic fallback from wrong tranche split.
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

**Changes**: +4 lines (3 comment + 1 variable declaration), arg 3 of `ResolveFollowerTargets` changed from `0` to `followerPosQty`.

---

## CYC Analysis for ExecuteFollowers

### Before T3: CYC = 4

Branch count (McCabe):
```
Base path:                                              = 1
(1) if (rule != null)                                   +1 = 2
(2) foreach (var follower in rule.Value.FollowerAccounts) +1 = 3
(3) if (follower == null) continue                      +1 = 4
(4) ExecuteOne(...) delegate call                         = 4 (call, not branch)
```

CYC before T3 = 4

### Delta introduced by T3

```
Line added: int followerPosQty = CopyEngine.Instance?.FindPositionPublic(follower, pos.Instrument)?.Quantity ?? 0;
  This is a variable assignment expression statement.
  The ?. (null-conditional) and ?? (null-coalescing) operators are EXPRESSIONS, NOT McCabe branches.
  They do not create independent code paths for McCabe complexity counting.
  McCabe delta = 0.

Argument changed: 0 -> followerPosQty inside existing method call.
  No new statement. No new branch. McCabe delta = 0.
```

### After T3: CYC = 4 (unchanged, well within JS-066 limit of 8)

---

## Scan Results

### SCAN-01 -- lock() check in PttGlobalQuickExit.cs
```
Command: Select-String -Pattern "lock\(" src/PropTraderTools/Features/PttGlobalQuickExit.cs
Output:  (no output)
Result:  0 results
PASS
```

### SCAN-02 -- async void check in PttGlobalQuickExit.cs
```
Command: Select-String -Pattern "async void " src/PropTraderTools/Features/PttGlobalQuickExit.cs
Output:  (no output)
Result:  0 results
PASS
```

### SCAN-03 -- followerPosQty present on assignment line and ResolveFollowerTargets call
```
Command: Select-String -Pattern "followerPosQty" src/PropTraderTools/Features/PttGlobalQuickExit.cs
Output (relevant lines):
  Line 216: // B191-T3: restore followerPosQty from live position (DW-B187-01 fix).
  Line 217: // With followerPosQty>0, ScaleLeaderTargets fires when follower has no snapshot.
  Line 219: int followerPosQty = CopyEngine.Instance?.FindPositionPublic(follower, pos.Instrument)?.Quantity ?? 0;
  Line 223: followerPosQty,

Assignment line (219): uses FindPositionPublic (internal wrapper) -- NOT FindPosition (private). CONFIRMED.
ResolveFollowerTargets arg 3 (line 223): followerPosQty -- NOT literal 0. CONFIRMED.
PASS
```

### SCAN-04 -- ExecuteFollowers CYC <= 4
```
ExecuteFollowers CYC before T3: 4
Delta introduced by T3:
  - Variable assignment statement: `int followerPosQty = ...`
    ?. and ?? are expressions, NOT McCabe branches. Delta = 0.
  - Argument substitution 0 -> followerPosQty: no new statement, no new branch. Delta = 0.
ExecuteFollowers CYC after T3: 4 (unchanged)
Expected: CYC <= 4. Actual: CYC = 4.
PASS
```

### SCAN-05 -- dotnet build
```
Command: dotnet build src/PropTraderTools/PropTraderTools.csproj
Output:
  Determining projects to restore...
  All projects are up-to-date for restore.
  PropTraderTools -> ...\bin\Debug\PropTraderTools.dll

  Build succeeded.
      0 Warning(s)
      0 Error(s)

  Time Elapsed 00:00:02.00
Result: 0 errors, 0 warnings
PASS
```

### SCAN-06 -- dotnet test B116/B120/B123/B128 regression
```
Command: dotnet test src/PropTraderTools/PropTraderTools.csproj
         --filter "FullyQualifiedName~B116|FullyQualifiedName~B120|FullyQualifiedName~B123|FullyQualifiedName~B128"
Output:
  Failed:  2, Passed: 17, Skipped: 0, Total: 19, Duration: 655 ms

Failures:
  B123Tests.T_B123_04_ForcedOverload_Exists     -- Assert.NotNull() Failure: Value is null
  B123Tests.T_B123_05_NoArgOverload_StillExists -- Assert.NotNull() Failure: Value is null

Pre-existence confirmed: these same 2 B123 reflection test failures are recorded in
ticket-1-completion.md SCAN-06 (pre-existing before T1; not introduced by T1 or T3).
These tests were already failing at the parent commit of 365b8779.
17 non-B123 tests (B116, B120, B128 suite) all PASS.
T3 introduced zero regressions.
PASS (pre-existing failures, not introduced by T3)
```

### SCAN-07 -- ptt-sync.ps1
```
Command: powershell -File .\scripts\ptt-sync.ps1
Output:
  LINKED  CopyEngine.cs
  LINKED  FeatureFlags.cs
  LINKED  LicenseClient.cs
  LINKED  PttTickMath.cs
  LINKED  TradeCopierAddOn.cs
  LINKED  TradeCopierPanel.cs
  LINKED  TradeCopierWindow.cs
  LINKED  Core\PttContracts.cs
  LINKED  Features\PttBeSwapAll.cs
  LINKED  Features\PttBeSwapOne.cs
  LINKED  Features\PttBreakEvenSwap.cs
  LINKED  Features\PttCancel.cs
  LINKED  Features\PttCopier.cs
  LINKED  Features\PttFlatten.cs
  LINKED  Features\PttFollowerStrategy.cs
  LINKED  Features\PttGlobalBreakEven.cs
  LINKED  Features\PttGlobalQuickExit.cs
  LINKED  Features\PttQuickExit.cs
  LINKED  Features\PttTrim.cs
  ptt-sync complete: 19 linked, 0 errors
Result: 19 linked, 0 errors
PASS
```

---

## Scan Summary Table

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 | lock() in PttGlobalQuickExit.cs | PASS (0 results) |
| SCAN-02 | async void in PttGlobalQuickExit.cs | PASS (0 results) |
| SCAN-03 | followerPosQty on assignment + ResolveFollowerTargets arg 3 | PASS (FindPositionPublic confirmed, not literal 0) |
| SCAN-04 | ExecuteFollowers CYC = 4, delta = 0 from T3 | PASS (4 branches, 0 new from T3) |
| SCAN-05 | dotnet build | PASS (0 errors, 0 warnings) |
| SCAN-06 | dotnet test B116/B120/B123/B128 regression | PASS (2 pre-existing B123 failures; 0 regressions from T3) |
| SCAN-07 | ptt-sync.ps1 | PASS (19 linked, 0 errors) |

---

## DW Closure

```
DW-B187-01 (P3, carried from B187 via B188, B189, B190): CLOSED

Root cause: B187-T2 deleted the [DW-B115-DIAG] block that computed _fPosQty.
  The B187 plan incorrectly stated _fPosQty was only used within that block.
  In fact, _fPosQty was also passed as followerPosQty (arg 3) to ResolveFollowerTargets.
  The B187 engineer made the minimal fix: replaced _fPosQty with literal 0.
  With followerPosQty=0, ResolveFollowerTargets short-circuits before ScaleLeaderTargets,
  leaving ScaleLeaderTargets permanently unreachable.

Fix applied: int followerPosQty = CopyEngine.Instance?.FindPositionPublic(follower, pos.Instrument)
             ?.Quantity ?? 0
  Data source: same path used by PttBreakEvenSwap.cs:53 and TradeCopierPanel.cs:1788.
  ?? 0 fallback preserved: flat follower (Quantity=0) -> ResolveFollowerTargets short-circuits
  correctly -> empty list returned.

Closing artifact: this ticket-3-completion.md.
```

---

## Status

**BUILD_PASS**

All 7 scans pass. Surgical edit applied: 1 new variable declaration line + 1 argument substitution.
`FindPositionPublic` (internal wrapper) confirmed -- not `FindPosition` (private).
ptt-sync: 19 linked, 0 errors. dotnet build: 0 errors, 0 warnings.
DW-B187-01: CLOSED.

---

*Author: ptt-engineer*
*Epic: B191*
*Ticket: T3*
*Phase: 4a (Implementation)*
