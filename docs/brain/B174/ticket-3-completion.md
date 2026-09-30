# B174 Ticket 3 Completion Report
# Epic: DW-B174-ATR-INSTRUMENT-MISMATCH
# Ticket: B174-3 -- Tests: B174AtrCalcTests
# Phase 4a output: ptt-engineer
# Status: BUILD_PASS

---

## Ticket Scope

B174-3 ONLY. No other ticket code was read or modified.

---

## Implementation

**File modified:** `src/PropTraderTools/CopyEngineTests.cs`

**Edit method:** `apply_diff` only (no `write_file` on any .cs file). SCAN-06: CONFIRMED.

**Append point:** Inserted before line 10258 (the namespace closing `}`), after line 10256
(`}` closing `B173AtrCalcTests` class). New class occupies lines 10258-10311.

**Lines added:** 56 lines (class declaration + 3 [Fact] methods with comments)

**Class inserted:**
```
public class B174AtrCalcTests
```

**Methods inserted:**
1. `T_B174_T1_01_PureOverloadRegressionGuard` -- ATR(3) = 5.5/3 regression guard
2. `T_B174_T1_02_InstrumentScanDelegatesCorrectly` -- pure-array path returns > 0
3. `T_B174_T1_03_EmptyInstrumentNameTreatedAsNull` -- string.IsNullOrEmpty guard

All methods: 0 branches (CYC=1), no NT8 runtime dependency, no ChartControl mock required.

---

## dotnet build Result

Command: `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj`

Result: **Build succeeded. 0 Error(s). 0 Warning(s).**

(Pre-existing CS0436 warnings suppressed by project configuration -- same baseline as before B174-3.)

---

## dotnet test Result

Command: `dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build --filter "FullyQualifiedName~B174AtrCalcTests"`

Result: **Passed! -- Failed: 0, Passed: 3, Skipped: 0, Total: 3**

All 3 B174 tests pass:
- `T_B174_T1_01_PureOverloadRegressionGuard` -- PASS
- `T_B174_T1_02_InstrumentScanDelegatesCorrectly` -- PASS
- `T_B174_T1_03_EmptyInstrumentNameTreatedAsNull` -- PASS

Full suite (unfiltered): Failed: 8 (pre-existing DwB168_02Tests + DwB15701SlateReadyTests -- same as baseline),
Passed: 227, Skipped: 358, Total: 593. 0 new failures introduced by B174-3.

---

## 7-Scan Results

| # | Scan | Command / Check | Result | Verdict |
|---|------|-----------------|--------|---------|
| SCAN-01 | No lock() in added class | `Select-String -Pattern "lock\("` where LineNumber >= 10258 | 0 occurrences in B174AtrCalcTests | PASS |
| SCAN-02 | B174AtrCalcTests class present | `Select-String -Pattern "B174AtrCalcTests"` | 2 occurrences (class decl + header comment) | PASS |
| SCAN-03 | No non-ASCII characters | Regex `[^\x00-\x7F]` full file scan | 0 non-ASCII chars | PASS |
| SCAN-04 | No DateTime.Now / FontFamily in code | `Select-String -Pattern "DateTime\.Now|FontFamily"` -- 1 hit is comment in class header referencing the rule (not actual usage) | Comment-only, not API call | PASS |
| SCAN-05 | No [Skip] in added class | `Select-String -Pattern "\[Skip\]"` where LineNumber >= 10258 | 0 occurrences in B174AtrCalcTests | PASS |
| SCAN-06 | apply_diff used (no write_file on .cs) | Engineer confirmation | apply_diff used for insertion; write_file not used on any .cs | PASS |
| SCAN-07 | ptt-sync.ps1 run after all tickets | `powershell -File .\scripts\ptt-sync.ps1` | 18 linked, 0 errors | PASS |

---

## ptt-sync.ps1 Output

```
LINKED  AtrCalc.cs
LINKED  CopyEngine.cs
LINKED  FeatureFlags.cs
LINKED  LicenseClient.cs
LINKED  TradeCopierAddOn.cs
LINKED  TradeCopierPanel.cs
LINKED  TradeCopierWindow.cs
LINKED  Core\PttContracts.cs
LINKED  Features\PttBreakEven.cs
LINKED  Features\PttBreakEvenSwap.cs
LINKED  Features\PttCancel.cs
LINKED  Features\PttCopier.cs
LINKED  Features\PttFlatten.cs
LINKED  Features\PttFollowerStrategy.cs
LINKED  Features\PttGlobalBreakEven.cs
LINKED  Features\PttGlobalQuickExit.cs
LINKED  Features\PttQuickExit.cs
LINKED  Features\PttTrim.cs

ptt-sync complete: 18 linked, 0 errors
```

---

## Verdict

**BUILD_PASS**

*Authored by: ptt-engineer*
*Block: B174 -- DW-B174-ATR-INSTRUMENT-MISMATCH*
*Ticket: B174-3 (Tests: B174AtrCalcTests)*
*Phase: 4a (Implementation)*
