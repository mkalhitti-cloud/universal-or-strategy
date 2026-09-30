# ticket-1-verification.md -- BWAVE-NEXT LaneBRepair-R4 T1

**Verifier**: ptt-verifier
**Date**: 2026-09-05
**Branch**: bwave-next-lane-b
**Epic**: BWAVE-NEXT LaneBRepair-R4
**Ticket**: T1 -- R4-F1 STALE: Regression Guard Test
**Engineer Completion**: ticket-1-completion.md

---

## 1. SCOPE CONFIRMATION

T1 ONLY verified in this session. No other ticket completion files were read.

Files touched by engineer (from git status + independent verification):
| File | Action | Confirmed |
|------|--------|-----------|
| `src/PropTraderTools/Tests/BwaveNextLaneBRepairR4Tests.cs` | CREATED (compiled, test-only) | YES |
| `src/PropTraderTools/CopyEngineTests.cs` | APPENDED (non-compiled block, Condition="false") | YES |
| `src/PropTraderTools/PropTraderTools.csproj` | ONE LINE ADDED (to include Tests/BwaveNextLaneBRepairR4Tests.cs) | YES |
| `src/PropTraderTools/CopyEngine.cs` | UNTOUCHED | CONFIRMED via `git diff` (empty) |

`git diff src/PropTraderTools/CopyEngine.cs` produced **zero output** -- CopyEngine.cs is untouched.

---

## 2. STALE CONFIRMATION (Independent Source Read)

Verifier independently read `src/PropTraderTools/CopyEngine.cs` lines 6627-6652:

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

STALE CONFIRMED:
- Line 6641: `SubmitEntryDirect(...)` -- submit appears FIRST
- Line 6649: Comment "R3-F2: clear drain-owned IDs AFTER submit so IDs are preserved on submit failure." -- ordering intent documented
- Lines 6650-6651: `foreach (var id in payload.DrainedOrderIds)` -- cleanup appears AFTER submit
- No try/finally wrapper present (none needed -- ordering already correct)
- NO production code change was made to CopyEngine.cs

Locked items confirmed still present:
- `(long)(int)Environment.TickCount` at lines 6452, 6545, 6672 -- PRESERVED
- `.ToList()` pattern at lines 1754, 1993, 2786 -- PRESERVED

---

## 3. TEST VALIDITY

**Compiled test file**: `src/PropTraderTools/Tests/BwaveNextLaneBRepairR4Tests.cs`

Verified independently via `Get-Content`:

```csharp
// BWAVE-NEXT LaneBRepair-R4 T1 -- R4-F1 STALE regression guard.
// ...
using Xunit;

namespace PropTraderTools.Tests
{
    public class BwaveNextLaneBRepairR4Tests
    {
        [Fact]
        public void SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1()
        {
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
    }
}
```

Checklist:
- [x] `[Fact]` attribute present
- [x] `Assert.Contains("R3-F2: clear drain-owned IDs AFTER submit", sourceText, StringComparison.Ordinal)` present
- [x] Deterministic (no timing, no random)
- [x] xUnit only -- no NUnit/MSTest
- [x] Synchronous void -- no async

NOTE on deviation from ticket spec: The ticket prescribed `typeof(CopyEngine).Assembly.Location` for path resolution. The compiled test in `Tests/BwaveNextLaneBRepairR4Tests.cs` uses `AppDomain.CurrentDomain.BaseDirectory` walk-up instead, to avoid xUnit shadow-copy issues. The CopyEngineTests.cs non-compiled block uses the prescribed Assembly.Location approach. The walk-up approach is more robust and produces a PASS result. The key assertion (`Assert.Contains`) is identical in both. Verdict: ACCEPTABLE DEVIATION -- spirit of the test preserved, assertion unchanged.

**CopyEngineTests.cs appended block** (non-compiled, Condition="false" in .csproj):
- Line 4092-4115: Block appended to class body
- Uses `typeof(CopyEngine).Assembly.Location`-based path (original ticket prescription)
- Not compiled due to pre-existing Condition="false" on that ItemGroup
- Does not affect build or test execution

---

## 4. ALL 7 SCAN RESULTS (Layer 3 -- Independent)

### SCAN-01: lock() ban (JS-021)

Command: `Select-String -Path "src/PropTraderTools/*.cs" -Pattern "lock(" -SimpleMatch`
         `Select-String -Path "src/PropTraderTools/Tests/*.cs" -Pattern "lock(" -SimpleMatch`
Result: **No output -- 0 matches**
Layer 3 verdict: PASS

### SCAN-02: async void ban (JS-033)

Command: `Select-String -Path "src/PropTraderTools/Tests/BwaveNextLaneBRepairR4Tests.cs" -Pattern "async void " -SimpleMatch`
Raw output: Hit on line 4 -- "// xUnit only -- JS-051. No lock() -- JS-021. No async void -- JS-033."
This is a COMMENT, not an `async void` declaration.
Command: `Select-String -Path "src/PropTraderTools/Tests/BwaveNextLaneBRepairR4Tests.cs" -Pattern "async\s+void\s+\w+"`
Result: **No output -- 0 actual async void declarations**
Layer 3 verdict: PASS

### SCAN-03: return null ban (JS-002)

Command: `Select-String -Path "src/PropTraderTools/Tests/BwaveNextLaneBRepairR4Tests.cs" -Pattern "return null;" -SimpleMatch`
Result: **No output -- 0 matches**
Layer 3 verdict: PASS

### SCAN-04: ASCII-only (JS-004)

Command: PowerShell byte-level scan of BwaveNextLaneBRepairR4Tests.cs
Result: PASS: All bytes are ASCII (<=127). Count: 2129 bytes total.
Layer 3 verdict: PASS

### SCAN-05: AtmStrategyChangeStopTarget ban

Command: `Select-String -Path "src/PropTraderTools/*.cs","src/PropTraderTools/Tests/*.cs" -Pattern "AtmStrategyChangeStopTarget" -SimpleMatch`
Result:
  src\PropTraderTools\CopyEngine.cs:6441: // NT8 bans: no Account.Change(), no AtmStrategyCreate(), no AtmStrategyChangeStopTarget().
  src\PropTraderTools\CopyEngine.cs:6576: // NO Account.Change(). NO AtmStrategyCreate(). NO AtmStrategyChangeStopTarget()
Both hits are COMMENTS ONLY in CopyEngine.cs (pre-existing, unchanged).
New test file: 0 mentions.
Layer 3 verdict: PASS

### SCAN-06: CYC <= 8

`SubmitDrainedEntry` (CopyEngine.cs, UNCHANGED): CYC=4 per line 6628-6629 comment.
  Git diff confirms ZERO changes to CopyEngine.cs.

New test `SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1`:
  Manual count: base(1) + for loop(1) + if File.Exists(1) + if parent==null||parent==dir(1+1) = 5
  Within budget (<=8).
  Note: Engineer reported CYC=4. Verifier counts CYC=5 (includes || operator as decision point).
  Immaterial: both are well within <=8. No violation.

complexity_audit.py not found at scripts/complexity_audit.py (script missing from repo).
Manual count used as fallback. Result: PASS

Layer 3 verdict: PASS

### SCAN-07: Build (0 errors)

Command: `dotnet build src/PropTraderTools/ --no-incremental`
Result:
  Build succeeded.
  src\PropTraderTools\Tests\B131Tests.cs(165,13): warning xUnit2004: Do not use Assert.Equal() to check for boolean conditions. Use Assert.True instead.
  1 Warning(s) [pre-existing B131Tests.cs line 165 -- not introduced by T1]
  0 Error(s)
Layer 3 verdict: PASS

---

## 5. LAYER 2 vs LAYER 3 COMPARISON

| Scan | Layer 2 (Engineer Self-Report) | Layer 3 (Verifier Independent) | Match |
|------|-------------------------------|-------------------------------|-------|
| SCAN-01 lock() | 0 matches | 0 matches | MATCH |
| SCAN-02 async void | 0 declarations (comment-only) | 0 declarations (comment-only confirmed) | MATCH |
| SCAN-03 return null | 0 in new code | 0 in new code | MATCH |
| SCAN-04 ASCII-only | PASS | PASS (2129 bytes, all <=127) | MATCH |
| SCAN-05 AtmStrategyChangeStopTarget | 2 in CopyEngine.cs (comments), 0 new | 2 in CopyEngine.cs (comments), 0 new | MATCH |
| SCAN-06 CYC | Test=CYC4, SubmitDrainedEntry=CYC4 | Test=CYC5 (||), SubmitDrainedEntry=CYC4 | MINOR DISCREPANCY (immaterial -- both <=8) |
| SCAN-07 Build | 0 errors, 1 pre-existing warning | 0 errors, 1 pre-existing warning (B131Tests.cs:165) | MATCH |

DISCREPANCY ASSESSMENT:
- SCAN-06 CYC minor discrepancy: Engineer counted CYC=4 for new test, verifier counts CYC=5 
  (|| operator contributes 1 additional decision point per McCabe). Both are within <=8 budget.
  No violation. Not a VERIFY_FAIL condition.

CONCLUSION: All 7 scans match between Layer 2 and Layer 3 in substance. No material discrepancies.

---

## 6. TEST EXECUTION (Layer 3)

### T1 Test

Command: `dotnet test src/PropTraderTools/ --filter "FullyQualifiedName~SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1" --no-build`
Result:
  Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 138 ms - PropTraderTools.dll (net48)
Full name: PropTraderTools.Tests.BwaveNextLaneBRepairR4Tests.SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1
Verdict: PASS

### Regression Suite

Command: `dotnet test src/PropTraderTools/ --filter "FullyQualifiedName~DrainThenDispatch|...|FindFollowerEntryOrder" --no-build`
Result:
  Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 2 s - PropTraderTools.dll (net48)
Verdict: PASS -- 0 regressions introduced

---

## 7. NT8 SYNC (Layer 3)

Command: `powershell -File scripts\ptt-sync-and-verify.ps1`
Result:
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

0 MISMATCH lines. Test file correctly excluded from NT8 sync.
NEXT STEP: Press F5 in NinjaTrader 8 to recompile.

---

## 8. DISMISSED FINDINGS CONFIRMED (11 items)

All 11 dismissed findings recorded in completion.md are confirmed. None was inadvertently implemented.

| ID | Finding | Disposition | Implemented? |
|----|---------|-------------|--------------|
| CR5-outside-1 | Drain ID/instrument scoping | DW-NEXT-B-01. DISMISSED | NO |
| CR5-outside-2 | ATM mode/template preservation in payload | DW-NEXT-B-02. DISMISSED | NO |
| CR5-outside-3 | TryDrainWatchdog independent trigger | Advisory. DISMISSED | NO |
| CR5-dup-1 | Order.Name null guard | NT8 guarantees non-null. DISMISSED | NO |
| CR5-dup-2 | OnOrderUpdate helper extraction CYC | DW-NEXT-B-04. DISMISSED | NO |
| CR5-dup-3 | _followerReplaceSpecs FSM | Scope creep. DISMISSED | NO |
| CR5-dup-4 | Hot-path heap alloc removal | DW-NEXT-A-07. DISMISSED | NO |
| CR5-test-1 | Test PascalCase no underscores | Project convention. DISMISSED | NO |
| CR5-test-2 | Test parameter type assertions | Advisory. DISMISSED | NO |
| DW-lock-1 | Watchdog resubmit vs drop | Director-locked. DISMISSED | NO |
| DW-net-1 | TickCount64 usage | .NET 4.8. DISMISSED | NO |

No new DW- items generated by R4. Deferred items DW-NEXT-B-01..B-04 remain OPEN (carried forward, unchanged).

---

## 9. SCOPE ENFORCEMENT CHECKS

| Check | Expected | Actual | Pass? |
|-------|----------|--------|-------|
| CopyEngine.cs zero diff | git diff = empty | Empty (verified independently) | PASS |
| (long)(int)Environment.TickCount preserved | Present at >=1 location | Lines 6452, 6545, 6672 | PASS |
| .ToList() on ActiveOrders preserved | Present | Lines 1754, 1993, 2786 | PASS |
| Watchdog drop-on-timeout unchanged | No resubmit logic | CopyEngine.cs untouched | PASS |
| try/finally NOT applied | STALE finding -- no try/finally | Confirmed absent | PASS |
| No new production files touched | Test files only | Confirmed | PASS |

---

## 10. ARCHITECTURE COMPLIANCE

| Requirement | Status |
|-------------|--------|
| R4-F1 STALE finding recorded with line-number evidence | PASS (lines 6641, 6649-6651) |
| Zero production code changes | PASS (git diff confirms) |
| One [Fact] regression guard test added | PASS |
| Test asserts R3-F2 ordering comment exists | PASS |
| Build 0 errors | PASS |
| All prior tests pass | PASS (11/11 regression suite) |
| xUnit [Fact] only | PASS |
| JS-021 no lock() | PASS |
| JS-033 no async void | PASS |
| JS-002 no return null | PASS |
| JS-004 ASCII-only | PASS |

---

## 11. DNA RULE VIOLATIONS FOUND

None.

Full DNA checklist:
| DNA Rule | Pattern | Result |
|----------|---------|--------|
| JS-021: lock() anywhere | Select-String lock( | 0 matches -- PASS |
| JS-001: throw new Exception in hot paths | N/A (test uses Assert, not throw) | PASS |
| JS-002: return null | 0 in new code | PASS |
| JS-033: async void | 0 declarations | PASS |
| JS-004: ASCII-only | All bytes <=127 | PASS |
| NT8: AtmStrategyChangeStopTarget | 0 in new code (2 pre-existing comments) | PASS |
| NT8: sealed on TradeCopierWindow | N/A (no Window changes) | N/A |
| NT8: FontFamily= in WPF | N/A (no XAML/WPF changes) | N/A |
| NT8: hex color #RRGGBB | N/A (no UI changes) | N/A |
| NT8: DateTime.Now | N/A (no production code changes) | N/A |
| NT8: CreateOrder PTT- prefix | N/A (no CreateOrder calls in new code) | N/A |

---

## 12. FINAL VERDICT

**VERIFY_PASS**

Rationale:
- T1 scope: CONFIRMED. Only test file added, CopyEngine.cs untouched (git diff empty).
- Stale confirmation: INDEPENDENT READ confirms SubmitEntryDirect at line 6641 precedes
  foreach DrainedOrderIds at line 6650. Comment "R3-F2: clear drain-owned IDs AFTER submit"
  at line 6649 documents intent. R4-F1 is STALE.
- All 7 scans: PASS (Layer 3 independent run).
- Layer 2 vs Layer 3: All scans match in substance. One immaterial CYC discrepancy (4 vs 5)
  -- both within <=8 budget, not a violation.
- Test execution: PASS (1/1, 138 ms).
- Regression suite: PASS (11/11, 0 regressions).
- NT8 sync: PASS (0 MISMATCH, 18 files OK).
- Dismissed findings: All 11 confirmed, none inadvertently implemented.
- DNA rules: 0 violations found.

**NEXT STEP (director action required)**: Press F5 in NinjaTrader 8 to recompile.