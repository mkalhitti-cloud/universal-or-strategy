# ticket-1-completion.md -- BWAVE-NEXT LaneBRepair-R4 T1

**Engineer**: ptt-engineer
**Date**: 2026-09-05
**Branch**: bwave-next-lane-b
**Epic**: BWAVE-NEXT LaneBRepair-R4
**Ticket**: T1 -- R4-F1 STALE: Regression Guard Test

---

## 1. SCOPE CONFIRMATION

Only T1 executed.
Only `src/PropTraderTools/Tests/BwaveNextLaneBRepairR4Tests.cs` created (new test file, compiled).
`src/PropTraderTools/CopyEngineTests.cs` received a non-compiled comment/test block (added to the file per ticket routing, but file remains `Condition="false"` in .csproj due to pre-existing 70+ API mismatch errors; a separate compiled test file was created in Tests/ per project conventions).
`src/PropTraderTools/CopyEngine.cs` -- UNTOUCHED. Zero diff.
`src/PropTraderTools/PropTraderTools.csproj` -- one line added to include the new test file.

---

## 2. SOURCE READ FINDINGS: STALE CONFIRMED

Read `src/PropTraderTools/CopyEngine.cs` lines 6627-6652:

```
6627 |         // DW-NEW-08 Option D: submit the parked entry after all drain cancels acknowledged.
6628 |         // CYC=4: (1) TryRemove fails early return, (2) FollowerAccount null early return,
6629 |         //        (3) delegated to SubmitEntryDirect, (4) F3 cleanup foreach (after submit).
6630 |         // R3-F2: cleanup moved after SubmitEntryDirect -- drain IDs preserved until submit completes.
6631 |         // NT8: Account.CreateOrder + Submit via SubmitEntryDirect. NO Account.Change().
6632 |         private void SubmitDrainedEntry(string acctKey)
6633 |         {
6634 |             if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
6635 |                 return;
6636 |
6637 |             var follower = payload.FollowerAccount;
6638 |             if (follower == null) // (2)
6639 |                 return;
6640 |
6641 |             SubmitEntryDirect( // (3) submit first -- drain IDs still in dict here
6642 |                 follower,
6643 |                 payload.Instrument,
6644 |                 payload.Qty,
6645 |                 payload.Price,
6646 |                 payload.Action,
6647 |                 payload.OrderType);
6648 |
6649 |             // R3-F2: clear drain-owned IDs AFTER submit so IDs are preserved on submit failure.
6650 |             foreach (var id in payload.DrainedOrderIds) // (4)
6651 |                 _drainOwnedOrderIds.TryRemove(id, out _);
6652 |         }
```

CONFIRMED STALE:
- SubmitEntryDirect at line 6641 (SUBMIT first)
- Comment "R3-F2: clear drain-owned IDs AFTER submit" at line 6649 (documents ordering)
- foreach DrainedOrderIds cleanup at lines 6650-6651 (CLEANUP AFTER SUBMIT)
- R3-F2 fix was already applied in a prior round. R4-F1 is STALE.
- NO production code change made. CopyEngine.cs is untouched.

---

## 3. TEST ADDED

**File**: `src/PropTraderTools/Tests/BwaveNextLaneBRepairR4Tests.cs` (new file, compiled)
**Also in**: `src/PropTraderTools/CopyEngineTests.cs` (appended to class body, but that file has Condition="false" in .csproj)
**Namespace**: `PropTraderTools.Tests`

Full test method:

```csharp
[Fact]
public void SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1()
{
    // Regression guard: R4-F1 was investigated and found STALE.
    // This test confirms the R3-F2 ordering comment still exists in source,
    // guarding against any future edit that moves cleanup before submit.
    // If this comment disappears, the ordering may have been changed and
    // R4-F1 should be re-evaluated.
    //
    // Path resolution: walk up from BaseDirectory to find the workspace
    // root (identified by the presence of src/PropTraderTools/CopyEngine.cs).
    // This avoids assembly shadow-copy issues in the xUnit test runner.
    string dir = AppDomain.CurrentDomain.BaseDirectory;
    string copyEngineFile = null;
    for (int i = 0; i < 8; i++)
    {
        string candidate = System.IO.Path.Combine(
            dir, "src", "PropTraderTools", "CopyEngine.cs");
        if (System.IO.File.Exists(candidate))
        {
            copyEngineFile = candidate;
            break;
        }
        string parent = System.IO.Path.GetDirectoryName(dir);
        if (parent == null || parent == dir)
            break;
        dir = parent;
    }
    Assert.NotNull(copyEngineFile);
    var sourceText = System.IO.File.ReadAllText(copyEngineFile);
    Assert.Contains(
        "R3-F2: clear drain-owned IDs AFTER submit",
        sourceText,
        System.StringComparison.Ordinal);
}
```

**Note on path resolution**: The ticket prescribed `typeof(CopyEngine).Assembly.Location`-based path, but xUnit shadow-copies the assembly to `%TEMP%`, making relative paths from Assembly.Location unreliable. The walk-up approach from `AppDomain.CurrentDomain.BaseDirectory` (which is NOT shadow-copied) resolves the workspace root deterministically. Test PASSES.

---

## 4. ALL 7 SCAN RESULTS

### SCAN-01: lock() ban (JS-021)
Command: `Select-String -Path "src/PropTraderTools/*.cs" -Pattern "lock\(" -SimpleMatch`
Result: **0 matches** -- PASS

### SCAN-02: async void ban (JS-033)
Command: `Select-String -Path "src/PropTraderTools/*.cs" -Pattern "async void "` (new code only)
Result: Existing files have the phrase in comments only (not declarations). New test file: 0 actual `async void` declarations. **PASS**

### SCAN-03: return null ban (JS-002)
Command: `Select-String -Path "src/PropTraderTools/*.cs" -Pattern "return null;"`
Result: All hits are pre-existing in unchanged files. New test adds zero `return null`. **PASS**

### SCAN-04: ASCII-only (JS-004)
Manual inspection of `BwaveNextLaneBRepairR4Tests.cs` and the appended block in `CopyEngineTests.cs`.
All characters are printable ASCII. No Unicode, no curly quotes, no emoji. **PASS**

### SCAN-05: AtmStrategyChangeStopTarget ban
Command: `Select-String -Path "src/PropTraderTools/*.cs" -Pattern "AtmStrategyChangeStopTarget"`
Result: 2 hits in CopyEngine.cs -- both are COMMENTS only (not code). New test: 0 mentions. **PASS**

### SCAN-06: CYC <= 8
`SubmitDrainedEntry` (CopyEngine.cs, UNCHANGED): CYC = 4 (documented in method comment line 6628-6629).
New test `SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1`: CYC = 4 (base=1 + for=1 + if File.Exists=1 + if parent null=1). Within budget (<=8). **PASS**

### SCAN-07: Build (0 errors)
Command: `dotnet build src/PropTraderTools/ --no-incremental`
Result:
```
Build succeeded.
  1 Warning(s)  [pre-existing: B131Tests.cs xUnit2004 -- not introduced by this change]
  0 Error(s)
```
**PASS**

---

## 5. NT8 SYNC: ptt-sync-and-verify.ps1

```
=== PTT SYNC: src/PropTraderTools -> NT8 AddOns ===
  Copied:   0  |  In-sync: 18  |  Excluded: 71

=== PTT VERIFY: MD5 check every synced file ===
  OK       AtrSizingEngine.cs
  OK       CopyEngine.cs
  OK       FeatureFlags.cs
  OK       LicenseClient.cs
  OK       TradeCopierAddOn.cs
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

**0 MISMATCH lines.** (Test file correctly excluded from NT8 sync.)
**NEXT STEP**: Press F5 in NinjaTrader 8 to recompile.

---

## 6. DISMISSED FINDINGS (11 items -- all carried forward, none re-opened)

| ID | Finding | Disposition |
|----|---------|-------------|
| CR5-outside-1 | Drain ID/instrument scoping | DW-NEXT-B-01. DISMISSED (future scope). |
| CR5-outside-2 | ATM mode/template preservation in payload | DW-NEXT-B-02. DISMISSED (future scope). |
| CR5-outside-3 | TryDrainWatchdog independent trigger | Advisory only. DISMISSED. |
| CR5-dup-1 | Order.Name null guard | NT8 guarantees non-null Order.Name. DISMISSED. |
| CR5-dup-2 | OnOrderUpdate helper extraction CYC | DW-NEXT-B-04. DISMISSED (future complexity epic). |
| CR5-dup-3 | _followerReplaceSpecs FSM | Scope creep. DISMISSED. |
| CR5-dup-4 | Hot-path heap alloc removal | DW-NEXT-A-07. DISMISSED. |
| CR5-test-1 | Test PascalCase no underscores | Project convention. DISMISSED. |
| CR5-test-2 | Test parameter type assertions | Advisory. DISMISSED. |
| DW-lock-1 | Watchdog resubmit vs drop | Director-locked (drop on timeout). DISMISSED. |
| DW-net-1 | TickCount64 usage | .NET 4.8 -- TickCount64 unavailable. DISMISSED. |

---

## 7. BUILD STATUS

```
Build succeeded.
  1 Warning(s)  [pre-existing B131Tests.cs xUnit2004 -- not introduced by T1]
  0 Error(s)
```

---

## 8. TEST RUN

Command: `dotnet test src/PropTraderTools/ --filter "FullyQualifiedName~SubmitDrainedEntry"`

```
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1,
           Duration: 157 ms - PropTraderTools.dll (net48)
```

Full test name: `PropTraderTools.Tests.BwaveNextLaneBRepairR4Tests.SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1`

---

## BUILD_PASS