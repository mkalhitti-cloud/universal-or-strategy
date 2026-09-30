# B128 Ticket 1 Completion Report

## Result: BUILD_PASS

---

## Changes Made

| CHANGE | File | Description |
|--------|------|-------------|
| CHANGE 1 | `src/PropTraderTools/TradeCopierPanel.cs` ~L268 | Added 4 new fields: `_instrQxBtn`, `_instrBeBtn`, `_instrRowPanel`, `_instrQxT1=4` after `_quickRowPanel` field block |
| CHANGE 2 | `src/PropTraderTools/TradeCopierPanel.cs` ~L920 | Inserted `BuildInstrRow()` call + `root.Children.Add(_instrRowPanel)` between `_beRowPanel` and `_quickRowPanel` in `BuildCopierButtons()` |
| CHANGE 3 | `src/PropTraderTools/TradeCopierPanel.cs` ~L1354 | Added `BuildInstrRow()` method (CYC=1) after `BuildQuickRow` region closing brace |
| CHANGE 4 | `src/PropTraderTools/TradeCopierPanel.cs` ~L1415 | Added `ComputeInstrSplit()` internal static method (CYC=1) immediately after `BuildInstrRow()` |
| CHANGE 5 | `src/PropTraderTools/TradeCopierPanel.cs` ~L1976 | Added `OnInstrQxClick` handler (CYC=3) after `OnQuickDown` |
| CHANGE 6 | `src/PropTraderTools/TradeCopierPanel.cs` ~L1998 | Added `OnInstrQxUp` handler (CYC=2) after `OnInstrQxClick` |
| CHANGE 7 | `src/PropTraderTools/TradeCopierPanel.cs` ~L2006 | Added `OnInstrQxDown` handler (CYC=2) after `OnInstrQxUp` |
| CHANGE 8 | `src/PropTraderTools/TradeCopierPanel.cs` ~L2018 | Added `OnInstrBeClick` handler (CYC=3) after `OnInstrQxDown` |
| CHANGE 9 | `src/PropTraderTools/Tests/B128Tests.cs` | Created new file: 4 xUnit [Fact] tests for `ComputeInstrSplit` |
| CHANGE 10 | `src/PropTraderTools/PropTraderTools.csproj` | Added `<Compile Include="Tests\B128Tests.cs" />` to explicit compile list |

---

## Scan Results (Layer 2 — Engineer Self-Report)

| Scan | Command | Result | Pass? |
|------|---------|--------|-------|
| SCAN-01 ASCII | PowerShell byte scan on TradeCopierPanel.cs + B128Tests.cs | 0 non-ASCII bytes in new code (pre-existing `\u25B2`/`\u25BC` in earlier code, not introduced by B128) | PASS |
| SCAN-02 lock() | `Select-String -Pattern "lock\(" src\PropTraderTools\TradeCopierPanel.cs` | 1 match at L1449 — is a comment string "no lock()" not actual lock usage. 0 lock() in new methods. | PASS |
| SCAN-03 async void | `Select-String -Pattern "async void " src\PropTraderTools\TradeCopierPanel.cs` | 3 matches — all in comments ("not async void" explanations). 0 actual async void declarations in new code. | PASS |
| SCAN-04 return null | `Select-String ... return null; ... lines 1349-2050` | 0 matches in new methods. | PASS |
| SCAN-05 build | `dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1` | `Build succeeded. 0 Warning(s). 0 Error(s).` | PASS |
| SCAN-06 CYC | Manual verification (complexity_audit.py not present) | BuildInstrRow=1, ComputeInstrSplit=1, OnInstrQxClick=3, OnInstrQxUp=2, OnInstrQxDown=2, OnInstrBeClick=3. All <= 8. | PASS |
| SCAN-07 tests | `dotnet test ... --filter "FullyQualifiedName~B128Tests"` | `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4` | PASS |

---

## Acceptance Criteria Check

| ID | Criterion | Status |
|----|-----------|--------|
| AC-01 | `_instrRowPanel` appears in `root.Children` AFTER `_beRowPanel` and BEFORE `_quickRowPanel` | PASS — `BuildCopierButtons()`: beRowPanel → BuildInstrRow() + instrRowPanel → quickRowPanel |
| AC-02 | `"[PTT-QX-INSTR]"` log prefix present in `OnInstrQxClick` | PASS — present in `NinjaTrader.Code.Output.Process(...)` call |
| AC-03 | `"[PTT-BE-INSTR]"` log prefix present in `OnInstrBeClick` | PASS — present in `NinjaTrader.Code.Output.Process(...)` call |
| AC-04 | `_engine.ArmPendingBe` (not `CopyEngine.Instance?.GlobalBe?.ArmPendingBe`) | PASS — `_engine.ArmPendingBe(_instrument, _leaderAccount, _beBuffer)` |
| AC-05 | `ComputeInstrSplit` is `internal static` | PASS — `internal static (int t1, int t2) ComputeInstrSplit(int instrQxT1)` |
| AC-06 | All 4 `[Fact]` tests pass | PASS — 4 passed, 0 failed |
| AC-07 | SCAN-01 through SCAN-07 all zero/pass | PASS — all 7 scans passed |

---

## Pre-existing Issues (DW-PTT-BE-FIX-03 known)

- `CopyEngineTests.cs` is excluded from MSBuild compile (`Condition="false"`) due to 70+ pre-existing API mismatch errors from prior sessions. This is a known pre-existing issue (DW-PTT-BE-FIX-03), not introduced by B128.
- `B43Tests.cs` is also excluded (`Condition="false"`) for pre-existing reasons. Not related to B128.
- `complexity_audit.py` is not present at `scripts/complexity_audit.py`. CYC reported via manual count.

---

## Notes

- `B128Tests.cs` uses `using Xunit;` only (no `using NinjaTrader.NinjaScript.AddOns;` — that namespace is NT8-runtime-only and does not resolve in MSBuild). `TradeCopierPanel` is in `namespace PropTraderTools`, accessible from `PropTraderTools.Tests` without a using directive (same assembly, parent namespace). This matches the pattern used by all other panel test files (B75, B76, etc.).
- `PropTraderTools.csproj` uses `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>` with an explicit Compile list. `Tests\B128Tests.cs` was added to the list.
- All 4 tests verify `ComputeInstrSplit` arithmetic: (4,5,1,7) -> ((2,2),(3,2),(1,0),(4,3)). Results match ticket spec exactly.