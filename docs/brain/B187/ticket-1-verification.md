# B187 Ticket 1 Verification Report (RE-VERIFY after Retry)
## ptt-verifier Phase 4b

**Date**: 2026-07-08 (retry re-verify)
**Verifier**: ptt-verifier
**Epic**: B187 -- QX Stop Snapshot Fix + DIAG Cleanup + CS0067 Suppress
**Ticket**: TICKET-1: DW-B187-QX-STOP-SNAP-01 -- leaderStop snapshot order fix [P1]
**Verdict**: VERIFY_PASS

---

## 1. Ordering Fix Verification (Independent Line-Number Evidence)

### Execute(Instrument) -- AC-2

`
Select-String -Path PttGlobalQuickExit.cs -Pattern "SnapshotStopPrice" | LineNumber
  Line 58:   double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);

Select-String -Path PttGlobalQuickExit.cs -Pattern "CancelPttBeOrders" | LineNumber
  Code-only first call in Execute(Instrument): Line 60
  (Line 31, 55, 142 are comment/doc references -- filtered as comments)
`

**Result**: SnapshotStopPrice (line 58) < CancelPttBeOrders code call (line 60) -- ORDERING CORRECT [PASS]

### Execute(List,Instrument) -- AC-3

`
SnapshotStopPrice call in Execute(List,Instrument): Line 143
CancelPttBeOrders call in Execute(List,Instrument): Line 144
(Line 142 is a comment -- filtered)
`

**Result**: SnapshotStopPrice (line 143) < CancelPttBeOrders code call (line 144) -- ORDERING CORRECT [PASS]

### DW-B126 Compliance -- AC-4 (SnapshotTargetOrders after CancelPttBeOrders)

`
First code call CancelPttBeOrders in Execute(Instrument): Line 60
First code call SnapshotTargetOrders in Execute(Instrument): Line 63
`

**Result**: SnapshotTargetOrders (line 63) > CancelPttBeOrders (line 60) -- DW-B126 MAINTAINED [PASS]

### DW-B126 N/A -- AC-5 (SnapshotTargetOrders absent from Execute(List,Instrument))

Source inspection lines 115-182 (Execute(List,Instrument) body):
- No SnapshotTargetOrders call in the second overload body.
- Line 106 (doc comment) mentions SnapshotTargetOrders only to say it is SKIPPED.
- forcedTargets used directly (caller-supplied). [PASS]

---

## 2. Seven-Scan Independent Results (Layer 3)

All scans run against the actual working-tree source files (B187 changes are uncommitted at this
stage; source modifications are in-tree). The git diff HEAD~1 HEAD reflects the previous B186-T3
commit; B187 changes are verified directly in the working-tree files.

| Scan | Command | My Result | Engineer Layer 2 | Match? |
|------|---------|-----------|-----------------|--------|
| SCAN-01 | lock() in new code in CopyEngineTests.cs, PttGlobalQuickExit.cs | 0 results | 0 results | YES |
| SCAN-02 | return null in new code (test file) | 0 results | 0 results | YES |
| SCAN-03 | async void in new code | 0 results | 0 results | YES |
| SCAN-04 | CYC manual count | See below | CYC=8 both methods | YES |
| SCAN-05 | Non-ASCII in modified source files | 0 results | 0 results | YES |
| SCAN-06 | throw new in new code | 0 results | 0 results | YES |
| SCAN-07 | dotnet build PropTraderTools.csproj | Build succeeded. 0 Warning(s). 0 Error(s). | 0 errors, 0 warnings | YES |

### SCAN-04 Manual CYC Count

**Execute(Instrument)** (lines 39-102):
Decision points: combined-gate || (1), acc-loop foreach (2), IsFollowerAccount continue (3),
pos-loop foreach (4), null/flat continue (5), instrument-filter continue (6),
NeedsLeaderFallbackFlatten if (7), ExecuteFollowers dispatch (8).
**CYC = 8** -- matches engineer report. [PASS]

**Execute(List,Instrument)** (lines 115-182):
Decision points: combined-gate || (1), null/empty guard (2), acc-loop foreach (3),
IsFollowerAccount continue (4), pos-loop foreach (5), null/flat/instr continue (6),
NeedsLeaderFallbackFlatten if (7), ExecuteFollowers dispatch (8).
**CYC = 8** -- matches engineer report. [PASS]

---

## 3. DNA Rule Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock()) | Source scan: 0 lock() in modified files | PASS |
| JS-001 (no throw new) | Source scan: 0 throw new in modified code | PASS |
| JS-002 (no return null) | Source scan: 0 return null in modified code | PASS |
| JS-033 (no async void) | Source scan: 0 async void in modified code | PASS |
| JS-066 (CYC <= 8) | SCAN-04: CYC=8 both methods | PASS |
| JS-081 (ASCII-only) | Source scan: 0 non-ASCII in new code | PASS |
| DW-B126 (SnapshotTargetOrders after cancel) | Line 63 > Line 60 | PASS |
| NT8-021 (Account.All after Loaded) | Threading model unchanged | PASS |
| NT8 DateTime.UtcNow | DateTime.UtcNow used (lines 364, 675) -- no DateTime.Now | PASS |
| NT8 FontFamily | 0 new FontFamily assignments (existing comment refs in other files) | PASS |
| NT8 hex color string | 0 new hex color strings in modified files | PASS |
| NT8 CreateOrder PTT- prefix | 0 new CreateOrder calls | PASS |
| SCAN-07 FULL (no async/await in init) | Structural tests only, no NT8 init methods modified | PASS |

---

## 4. [CallerFilePath] Path Fix -- Confirmed

The engineer added GetQxSourcePath() helper to CopyEngineTests.cs at line 8919:

`csharp
private static string GetQxSourcePath(
    [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "")
{
    string dir = System.IO.Path.GetDirectoryName(callerFile) ?? string.Empty;
    return System.IO.Path.Combine(dir, "Features", "PttGlobalQuickExit.cs");
}
`

- [System.Runtime.CompilerServices.CallerFilePath] attribute confirmed at line 8920.
- The attribute is captured at build time by the compiler to the absolute path of
  CopyEngineTests.cs. Shadow-copy (xUnit moving the DLL to %TEMP%) does NOT move the
  source file, so the path remains valid at test runtime.
- All 4 T_B187_* test methods call GetQxSourcePath() instead of the broken
  Assembly.GetExecutingAssembly().Location + relative navigation from prior VERIFY_FAIL.
- Fix is correct and addresses the root cause identified in ticket-1-verification.md. [PASS]

---

## 5. Comment-Line Guard -- Confirmed

Three of the four test methods (T_B187_01, T_B187_02, T_B187_03) include the comment-line guard:

`csharp
bool isComment = lines[i].TrimStart().StartsWith("//");
if (snapshotLine < 0 && !isComment && lines[i].Contains("SnapshotStopPrice"))
    snapshotLine = i;
if (cancelLine < 0 && !isComment && lines[i].Contains("CancelPttBeOrders"))
    cancelLine = i;
`

This correctly skips lines beginning with //, preventing the false match on:
`
// B78 DW-B63-01 + DW-B187: capture leaderStop BEFORE CancelPttBeOrders.
`
(lines 55 and 142 in PttGlobalQuickExit.cs).

T_B187_04 does not need the guard (it only checks for absence of SnapshotTargetOrders --
the assertion already fails correctly if SnapshotTargetOrders appears in any line).

Comment-line guard logic confirmed correct. [PASS]

---

## 6. Tests -- CopyEngineTests.cs

### Tests Present

All 4 [Fact] tests confirmed present:

| Test | Line | Present |
|------|------|---------|
| T_B187_01_Execute_Instrument_SnapshotStopPrice_BeforeCancelPttBeOrders | 8960 | YES |
| T_B187_02_Execute_List_Instrument_SnapshotStopPrice_BeforeCancelPttBeOrders | 9014 | YES |
| T_B187_03_Execute_Instrument_SnapshotTargetOrders_AfterCancelPttBeOrders | 9069 | YES |
| T_B187_04_Execute_List_Instrument_DoesNotContainSnapshotTargetOrders | 9122 | YES |

### Test Execution Results -- PASS

`
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "T_B187_"

Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 145 ms
`

**All 4 T_B187_* tests PASS.** AC-6 met. [PASS]

---

## 7. Architecture Compliance

- Class: PttGlobalQuickExit (internal sealed) -- correct namespace PropTraderTools. [PASS]
- Execute(Instrument) and Execute(List<(double,int)>, Instrument) -- both present, correct signatures. [PASS]
- XML summary comment at line 31 updated to reference "DW-B63-01 + DW-B187" per ticket. [PASS]
- LogLeaderDiag method still present (lines 499-522) -- T2 (DIAG cleanup) has NOT been applied. This is correct per T1 scope. [PASS]
- DW-B115-DIAG blocks in Execute and ExecuteFollowers still present -- T2 scope only. [PASS]
- No unrelated changes: source modification is only the 2 statement reorders + XML comment update + 4 tests.

---

## 8. Layer 2 vs Layer 3 Cross-Check

| Item | Engineer Layer 2 Report | My Layer 3 | Discrepancy |
|------|------------------------|------------|-------------|
| SnapshotStopPrice line in Execute(Instrument) | 58 | 58 | MATCH |
| CancelPttBeOrders code line in Execute(Instrument) | 60 | 60 | MATCH |
| SnapshotTargetOrders line in Execute(Instrument) | 63 | 63 | MATCH |
| SnapshotStopPrice line in Execute(List,Instrument) | 143 | 143 | MATCH |
| CancelPttBeOrders code line in Execute(List,Instrument) | 144 | 144 | MATCH |
| SCAN-01 lock() | 0 | 0 | MATCH |
| SCAN-02 return null | 0 | 0 | MATCH |
| SCAN-03 async void | 0 | 0 | MATCH |
| SCAN-04 CYC Execute(Instrument) | 8 | 8 | MATCH |
| SCAN-04 CYC Execute(List,Instrument) | 8 | 8 | MATCH |
| SCAN-05 non-ASCII | 0 | 0 | MATCH |
| SCAN-06 throw new | 0 | 0 | MATCH |
| SCAN-07 build | 0 errors, 0 warnings | 0 errors, 0 warnings | MATCH |
| T_B187_01..04 pass | PASS (4/4) | PASS (4/4) | MATCH |
| [CallerFilePath] fix | Confirmed in completion report | Confirmed at line 8920 | MATCH |
| Comment-line guard | Confirmed in completion report | Confirmed at lines 8995-8999, 9050-9054, 9103-9107 | MATCH |

**No discrepancies between Layer 2 and Layer 3.** Previous VERIFY_FAIL discrepancy (test failures) is fully resolved.

---

## 9. Guardian + ptt-sync Results

`
GUARDIAN: GUARDIAN PASS -- all symbols present. Hunk=0 Del=0 Add=0
ptt-sync: ptt-sync complete: 19 linked, 0 errors
`

Both pass. [PASS]

---

## 10. Summary

| Check | Result |
|-------|--------|
| AC-2: SnapshotStopPrice < CancelPttBeOrders in Execute(Instrument) | PASS (line 58 < 60) |
| AC-3: SnapshotStopPrice < CancelPttBeOrders in Execute(List,Instrument) | PASS (line 143 < 144) |
| AC-4: SnapshotTargetOrders > CancelPttBeOrders in Execute(Instrument) | PASS (line 63 > 60) |
| AC-5: SnapshotTargetOrders absent from Execute(List,Instrument) body | PASS |
| AC-6: All 4 T_B187_* tests pass | PASS -- 4/4 pass |
| AC-7: SCAN-07 build passes | PASS -- 0 errors, 0 warnings |
| SCAN-01: no new lock() | PASS |
| SCAN-02: no new return null | PASS |
| SCAN-03: no new async void | PASS |
| SCAN-04: CYC <= 8 both methods | PASS |
| SCAN-05: no non-ASCII | PASS |
| SCAN-06: no new throw new | PASS |
| [CallerFilePath] fix confirmed | PASS |
| Comment-line guard confirmed | PASS |
| Guardian PASS | PASS |
| ptt-sync 19 linked, 0 errors | PASS |

---

## 11. Violations

**None.**

All checks pass. The previous VERIFY_FAIL is fully resolved. The [CallerFilePath] fix correctly
addresses the xUnit shadow-copy path resolution failure. The comment-line guard correctly prevents
false-positive matches on comment lines containing method names.

---

## 12. Verdict

**VERIFY_PASS**

*Written by: ptt-verifier*
*Epic: B187*
*Ticket: 1 (DW-B187-QX-STOP-SNAP-01)*
*Phase: 4b (Verification -- RE-VERIFY after retry)*
*Return: VERIFY_PASS*
