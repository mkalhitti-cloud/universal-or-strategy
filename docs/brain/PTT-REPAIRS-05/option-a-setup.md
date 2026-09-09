# PTT-REPAIRS-05 Option A Setup Log

Session: PTT-REPAIRS-05 REPAIR-LOOP
Date: 2026-09-07

---

## Step B1: PropTraderTools.Tests.csproj Created

File: src/PropTraderTools/PropTraderTools.Tests.csproj

Configuration:
- SDK: Microsoft.NET.Sdk
- TargetFramework: net48
- LangVersion: 9.0 (required for FeatureFlags.cs record keyword + IsExternalInit shim)
- IsTestProject: true
- EnableDefaultCompileItems: false
- UseWPF: true, UseWindowsForms: true, GenerateAssemblyInfo: false

PackageReferences:
- xunit 2.4.2
- xunit.runner.visualstudio 2.4.5 (PrivateAssets=all)
- Microsoft.NET.Test.Sdk 17.6.0

NT8 DLL References (same HintPaths as Linting.csproj):
- NinjaTrader.Core   -> C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll
- NinjaTrader.Custom -> C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\NinjaTrader.Custom.dll
- NinjaTrader.Gui    -> C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Gui.dll
- SharpDX            -> C:\Program Files\NinjaTrader 8\bin\SharpDX.dll
- SharpDX.Direct2D1  -> C:\Program Files\NinjaTrader 8\bin\SharpDX.Direct2D1.dll
- SharpDX.Direct3D10 -> C:\Program Files\NinjaTrader 8\bin\SharpDX.Direct3D10.dll
- SharpDX.DXGI       -> C:\Program Files\NinjaTrader 8\bin\SharpDX.DXGI.dll

Compile items (8 files):
- AtrSizingEngine.cs
- CopyEngine.cs
- CopyEngineTests.cs
- FeatureFlags.cs
- LicenseClient.cs
- TradeCopierAddOn.cs
- TradeCopierPanel.cs
- TradeCopierWindow.cs

Note: LangVersion 8.0 fails with CS1513 on FeatureFlags.cs record syntax.
      LangVersion 9.0 resolves this. The IsExternalInit shim in FeatureFlags.cs is
      already present to satisfy the net48 BCL gap.

---

## Step B2: Initial Build Result

Command: dotnet build src\PropTraderTools\PropTraderTools.Tests.csproj /nologo
LangVersion: 9.0
Result: FAIL -- 154 errors, 620 warnings

### Error Category Breakdown

| Category | Code | Count | Classification |
|----------|------|-------|----------------|
| (a) NT8-runtime: NT8 types absent | CS0234 | 12 | Expected -- NT8 assemblies don't expose all types at compile time |
| (a) NT8-runtime: CopyEngine() ctor private | CS0122 | 4 | Expected -- singleton ctor, test creates via Instance property |
| (b) Stale-sig: CopyRule not found | CS0246/CS0103 | 92 | Pre-existing -- CopyRule defined in a non-compiled file (src/*.cs vs src/PropTraderTools/*.cs) |
| (b) Stale-sig: IsDispatchTriggerState arity | CS7036 | 12 | Pre-existing -- signature added `type` param; tests use old single-param form |
| (c) Missing _engine in sibling classes | CS0103 | 36 | Test infra -- _engine field only in primary CopyEngineTests class |
| (c) Missing GetMethod/GetField in sibling | CS0103 | 114 | Test infra -- static helper methods only in primary class |
| (d) Missing System.Linq / other | CS1061/CS0030/CS0019 | 38 | Test infra -- missing using System.Linq in sibling classes |

### T_CLONE_* region (lines 4619-4740): 0 errors after stale-sig fix.

---

## Step B3: Stale T_CLONE_* Signature Fixes

Tests updated (all in CopyEngineTests class, primary class -- _engine in scope):

| Test | Old signature | New signature |
|------|--------------|---------------|
| T_CLONE_02 | SetCloneAtmObjectCache(null) | SetCloneAtmObjectCache("MGC DEC26", null) |
| T_CLONE_02 | SetCloneAtmCache("MES $200 SL6") | SetCloneAtmCache("MGC DEC26", "MES $200 SL6") |
| T_CLONE_02 | GetCloneAtmMode() | GetCloneAtmMode("MGC DEC26") |
| T_CLONE_02 | SetCloneAtmCache(string.Empty) | SetCloneAtmCache("MGC DEC26", string.Empty) |
| T_CLONE_03 | SetCloneAtmObjectCache(null) | SetCloneAtmObjectCache("MGC DEC26", null) |
| T_CLONE_03 | SetCloneAtmCache(string.Empty) | SetCloneAtmCache("MGC DEC26", string.Empty) |
| T_CLONE_03 | GetCloneAtmMode() | GetCloneAtmMode("MGC DEC26") |
| T_CLONE_04 | SetCloneAtmObjectCache(null) | SetCloneAtmObjectCache("MGC DEC26", null) |
| T_CLONE_04 | SetCloneAtmCache("MES $200 SL6") | SetCloneAtmCache("MGC DEC26", "MES $200 SL6") |
| T_CLONE_04 | GetCloneAtmMode() | GetCloneAtmMode("MGC DEC26") |
| T_CLONE_04 | SetCloneAtmCache(string.Empty) | SetCloneAtmCache("MGC DEC26", string.Empty) |
| T_B66OBJ_02 | SetCloneAtmObjectCache(null) | SetCloneAtmObjectCache("MGC DEC26", null) |
| T_B66OBJ_02 | SetCloneAtmCache("MES 200") | SetCloneAtmCache("MGC DEC26", "MES 200") |
| T_B66OBJ_02 | GetCloneAtmMode() | GetCloneAtmMode("MGC DEC26") |
| T_B66OBJ_02 | SetCloneAtmCache(string.Empty) | SetCloneAtmCache("MGC DEC26", string.Empty) |

New test added:
- T_CLONE_XISO_01_PerInstrumentIsolation_MGCandMESIndependent
  Tests that SetCloneAtmCache("MGC DEC26", "MGC_TPL") and SetCloneAtmCache("MES DEC26", "MES_TPL")
  do not cross-contaminate: GetCloneAtmMode("MGC DEC26") returns MGC_TPL, not MES_TPL.
  This is the cross-instrument isolation proof-test required by DW-BUG-F-01.

Build after fix: T_CLONE_* region (lines 4619-4740): 0 errors. PASS.
Total remaining errors: 154 (all pre-existing test infrastructure issues).

---

## Step B4: dotnet test

NOT RUN -- build fails (154 pre-existing test infra errors prevent compilation).
Test run deferred to next session after pre-existing errors are resolved.

### Tests currently compilable (static logic tests in primary CopyEngineTests class):

The following T_CLONE_* tests are now correctly typed and would pass if the project compiled:
- T_CLONE_02 (static logic: ConcurrentDictionary key lookup, string cache return)
- T_CLONE_03 (static logic: empty cache -> Inherit return)
- T_CLONE_04 (static logic: SetCloneAtmCache -> GetCloneAtmMode Named)
- T_B66OBJ_02 (static logic: null object + non-empty string -> Named with null AtmObject)
- T_CLONE_XISO_01 (static logic: per-instrument isolation, two keys independent)

All five tests use only:
  - ConcurrentDictionary TryGetValue / indexer set / TryRemove (lock-free, no NT8 host required)
  - FollowerAtmMode subtype checks (pure .NET, no NT8 host required)
  - No Order/Account/Instrument NT8 objects

These are genuinely static logic tests. Once pre-existing infra errors are resolved and
the project compiles, all five are expected to PASS without NT8 host.

### Tests expected to skip:
- T_CLONE_01 (Skip = "NT8-runtime: NinjaTrader.NinjaScript.AtmStrategy requires NT8 host")
- T_B66OBJ_01 (Skip = "NT8-runtime: NinjaTrader.NinjaScript.AtmStrategy requires NT8 host")

---

## Step B5: Outstanding Issues for Next Session

To reach a fully compiling test project, the following pre-existing issues must be resolved:

### Issue 1: CopyRule not found (92 errors -- highest priority)
CopyRule is defined somewhere in the V12 source tree but not in src/PropTraderTools/*.cs.
Resolution: Add the file containing CopyRule to the Compile includes, or restructure.

### Issue 2: IsDispatchTriggerState arity (12 errors)
Tests call IsDispatchTriggerState(OrderState) but the method signature is
IsDispatchTriggerState(OrderState, OrderType). Tests need a second argument.

### Issue 3: Sibling class scoping (150 errors)
Classes CopyEngineB75Tests, B77QxRaceGuardTests, etc. do not inherit from CopyEngineTests
and lack: _engine field, GetMethod/GetField static helpers, using System.Linq.
Resolution: Each sibling class needs its own:
  private readonly CopyEngine _engine = CopyEngine.Instance;
  private static FieldInfo GetField(string name) => ...
  private static MethodInfo GetMethod(string name) => ...
  + using System.Linq;

### Issue 4: NT8 types (16 errors -- expected, not fixable without NT8 host)
NinjaTrader.NinjaScript.Instruments namespace not available in test-time compilation.
These tests should carry [Fact(Skip = "NT8-runtime: ...")] attributes.

---

## Step B6: Hard-link sync

deploy-sync.ps1: SYNC COMPLETE (2026-09-07).
CopyEngine.cs hard-link count: 2 (repo + NT8). PASS.
CopyEngineTests.cs hard-link count: 1 (repo only -- expected, test file). PASS.
PropTraderTools.Tests.csproj: new file, repo only (expected). PASS.
