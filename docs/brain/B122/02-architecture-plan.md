# B122 Architecture Plan — Build Remediation (DW-PTT-BE-FIX-03)

**Block**: B122
**Phase**: 1 (Architecture)
**Status**: REVIEW_PENDING
**Date**: 2026-08-25
**Closes**: DW-PTT-BE-FIX-03 (pre-existing build errors — dedicated remediation block)

---

## Section A — Problem Statement

Two defects block `dotnet build src/PropTraderTools/PropTraderTools.csproj`:

### Defect 1 (CONFIRMED — BLOCKING): Bogus NuGet PackageReference for SKGL.Extension

**File**: [`src/PropTraderTools/PropTraderTools.csproj`](../../src/PropTraderTools/PropTraderTools.csproj:85)
**Line**: 85

```xml
<PackageReference Include="SKGL.Extension" Version="2.0.23" />
```

**Symptom**: `error NU1101: Unable to find package SKGL.Extension. No packages exist with this id in source(s): nuget.org`

**Root cause**: The BGTM-1 commit that introduced `LicenseClient.cs` added SKGL.Extension twice:
1. Correctly, as a local DLL HintPath reference (lines 56-59 — already present, works):
   ```xml
   <Reference Include="SKGL.Extension">
     <HintPath>$(USERPROFILE)\Documents\NinjaTrader 8\bin\Custom\SKGL.Extension.dll</HintPath>
     <Private>false</Private>
   </Reference>
   ```
2. Incorrectly, as a NuGet `<PackageReference>` (line 85 — does not exist on NuGet.org, blocks restore).

**Impact**: The `PackageReference` forces NuGet restore to run. Restore fails with NU1101 before any
compilation begins. The build is 100% blocked regardless of whether the source files are correct.

**Evidence**: Running `dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1` returns:
```
error NU1101: Unable to find package SKGL.Extension. No packages exist with this id
Build FAILED. 0 Warning(s), 1 Error(s). Time Elapsed 00:00:01.79
```

---

### Defect 2 (CONDITIONAL — verify after Defect 1 fix): CS0433 Globals type ambiguity

**File**: [`src/PropTraderTools/PropTraderTools.csproj`](../../src/PropTraderTools/PropTraderTools.csproj:26)
**Line**: 26 (the `<NoWarn>` property)

**Background**: `DW-PTT-BE-FIX-03` (B107 deferred backlog) documented:
> "CS0433 Globals ambiguity at CopyEngine.cs:L3350 (now L4188 after code additions)"

**Current source state**: All four usages of `Globals` in production code are already fully qualified:
| File | Line | Expression |
|------|------|------------|
| [`CopyEngine.cs`](../../src/PropTraderTools/CopyEngine.cs:4188) | 4188 | `NinjaTrader.Core.Globals.UserDataDir` |
| [`LicenseClient.cs`](../../src/PropTraderTools/LicenseClient.cs:51) | 51 | `NinjaTrader.Core.Globals.UserDataDir` |
| [`TradeCopierWindow.cs`](../../src/PropTraderTools/TradeCopierWindow.cs:67) | 67 | `NinjaTrader.Core.Globals.UserDataDir` |
| [`TradeCopierAddOn.cs`](../../src/PropTraderTools/TradeCopierAddOn.cs:634) | 634 | `NinjaTrader.Core.Globals.UserDataDir` |

**Why CS0433 fires despite full qualification**: `CS0433` is a type-level error, not a call-site error.
It fires when both `NinjaTrader.Core.dll` AND `NinjaTrader.Custom.dll` each define a type named `Globals`
in a shared or conflicting namespace. The C# compiler reports the conflict even when the call site is
fully qualified. This is the same class of problem already handled by `CS0436` in the existing `<NoWarn>`.

**NT8_FULL_REFERENCE.md confirmation** (line 2120): `Core.Globals.MaxDate` — confirming
`NinjaTrader.Core.Globals` is the canonical NT8 Globals class.

**Current `<NoWarn>`** (line 26):
```xml
<NoWarn>MSB3245;MSB3246;CS0012;CS8632;CS0234;CS0246;CS0436</NoWarn>
```
`CS0433` is absent. `CS0436` (related type-conflict warning) is already suppressed.

---

## Section B — Fix Strategy: Defect 1 (PackageReference removal)

**File to edit**: `src/PropTraderTools/PropTraderTools.csproj`

**Exact change**: Remove lines 84-89 (the entire PackageReference ItemGroup entry):
```xml
<PackageReference Include="SKGL.Extension" Version="2.0.23" />
```

**Context**: This element sits inside the `<ItemGroup>` block at lines 83-90 that also contains
`xunit` and `xunit.runner.visualstudio`. Remove only the SKGL.Extension line.

**After the change, the ItemGroup becomes**:
```xml
<ItemGroup>
  <PackageReference Include="xunit" Version="2.6.6" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.5.7">
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
</ItemGroup>
```

**Risk assessment**: Zero. The `<Reference Include="SKGL.Extension"><HintPath>...</HintPath></Reference>`
entry already provides the assembly to the compiler. LicenseClient.cs compiles against the DLL.
Removing the PackageReference does not remove the assembly reference — it only removes the broken NuGet
restore requirement.

**Verification**: After this change, `dotnet build` restore phase must succeed.

---

## Section C — Fix Strategy: Defect 2 (CS0433 NoWarn — conditional)

**File to edit**: `src/PropTraderTools/PropTraderTools.csproj`

**Trigger condition**: After Defect 1 fix, engineer runs `dotnet build`. If `CS0433` appears in output,
apply this fix. If it does NOT appear, skip this step entirely.

**Change**: Add `CS0433` to the `<NoWarn>` property (line 26):

Before:
```xml
<NoWarn>MSB3245;MSB3246;CS0012;CS8632;CS0234;CS0246;CS0436</NoWarn>
```

After:
```xml
<NoWarn>MSB3245;MSB3246;CS0012;CS8632;CS0234;CS0246;CS0436;CS0433</NoWarn>
```

**Rationale**: CS0433 is the numeric counterpart to CS0436 for imported-assembly type conflicts.
Both cover the scenario where two referenced assemblies export identically-named types. CS0436 is
already suppressed; CS0433 should be treated identically. This is a project-level LSP suppression —
the project is for OmniSharp/IntelliSense only, not for MSBuild production builds.

**Call-site correctness**: All `Globals` usages use `NinjaTrader.Core.Globals.UserDataDir` (fully
qualified). No call-site change is needed in any .cs file.

---

## Section D — Success Criteria

1. `dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1` produces:
   - **0 errors**
   - **0 new warnings** (pre-existing suppressed warnings are acceptable)
   - `Build succeeded` in the output

2. Tests in `Tests/B120Tests.cs`, `Tests/B119Tests.cs`, `Tests/B118Tests.cs` remain structurally
   intact (no deletions, no method changes, no new compilation errors in those files).

3. Test count `>= current passing count` — no test methods deleted from any file.

4. CYC unchanged — no logic modifications to any C# source file. This is a project file change only.

5. `SKGL.Extension.dll` assembly is still resolved at build time via the HintPath Reference (verify
   by checking that LicenseClient.cs compiles without CS0246 or CS0012 on SKM.V3.* types).

---

## Section E — Rules Catalog Compliance

**Applicable rules for B122 scope**:

| Rule | Description | Status |
|------|-------------|--------|
| JS-021 | No `lock()` | PASS — no .cs changes |
| JS-001 | No throw in hot path | PASS — no .cs changes |
| JS-033 | No `async void` | PASS — no .cs changes |
| JS-051 | xUnit only (no NUnit/MSTest) | PASS — xUnit PackageReference unchanged |
| JS-066/Code Review | Diff < 10k chars | PASS — removing 1 XML line |

**P0 violations introduced**: ZERO

**P0 violations resolved**: 
- The `SKGL.Extension PackageReference` was causing a build failure that prevented ANY P0 violation
  scanning. After this fix, the build pipeline is unblocked and scanning can proceed.

**NoWarn note**: Adding `CS0433` to `<NoWarn>` follows the existing project pattern for LSP-only
build infrastructure. It does not suppress any warning in the NT8 production compile (which is done
by NT8's internal Roslyn host, not MSBuild).

---

## Section F — Ticket Plan

### Ticket 1: Remove bogus SKGL.Extension PackageReference + verify CS0433

**Single ticket covers all fixes.**

**File**: `src/PropTraderTools/PropTraderTools.csproj`

**Step 1 — Primary fix (mandatory)**:
- Remove `<PackageReference Include="SKGL.Extension" Version="2.0.23" />` from the xUnit ItemGroup
- This is a single-line deletion
- Source evidence: csproj line 85 (added by BGTM-1 commit, git diff confirmed)

**Step 2 — Verify build after step 1**:
- Run: `dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | head -80`
- Expected: restore succeeds, compilation begins

**Step 3 — Conditional CS0433 fix (apply only if build output shows CS0433)**:
- Add `CS0433` to `<NoWarn>` list at line 26
- Re-run build to confirm 0 errors

**Step 4 — Verify test files compile**:
- Confirm `Tests/B120Tests.cs`, `B119Tests.cs`, `B118Tests.cs` produce no new errors
- Confirm `CopyEngineTests.cs` (5772 lines) compiles without errors (reflection-based stubs handle
  NT8 sealed types via `FormatterServices.GetUninitializedObject` — already correct)

**Constraints (hard bans for engineer)**:
- DO NOT modify any `.cs` file
- DO NOT add or remove test methods from `CopyEngineTests.cs` or any `Tests/*.cs` file
- DO NOT restructure `CopyEngineTests.cs`
- DO NOT touch `CopyEngine.cs`, `LicenseClient.cs`, `TradeCopierAddOn.cs`, `TradeCopierWindow.cs`

**SCAN-01**: `grep -r "lock(" src/PropTraderTools --include="*.cs"` — must return 0 results (no change, existing baseline must hold)
**SCAN-02**: `grep -rn "async void " src/PropTraderTools --include="*.cs"` — must return 0 new results
**SCAN-03**: No `.cs` file touched — diff must show only `PropTraderTools.csproj`
**SCAN-04**: `dotnet build 2>&1 | grep -c "error"` — must return 0
**SCAN-05**: All `Globals.` usages remain `NinjaTrader.Core.Globals.UserDataDir` (fully qualified)
**SCAN-06**: xUnit PackageReference still present in csproj
**SCAN-07**: `<Reference Include="SKGL.Extension"><HintPath>...</HintPath></Reference>` still present in csproj (DLL reference untouched)

---

## Appendix: Commit History Evidence

The SKGL.Extension PackageReference was introduced by the BGTM-1 commit. Git diff against the prior
state of the csproj confirms this was a net-new addition, not a modification of the existing DLL
Reference. The DLL Reference (lines 56-59) predates the BGTM-1 commit and is correct.

The DW-PTT-BE-FIX-03 item from B107/06-deferred-backlog.md states:
> "Pre-existing errors in CopyEngineTests.cs stub infrastructure (83 errors) plus CS0433 Globals
> ambiguity at CopyEngine.cs:L3350. Confirmed pre-existing, unrelated to B107."

Current analysis: The 83-error count and CS0433 are no longer independently verifiable because the
PackageReference blocker prevents reaching compilation. After Defect 1 fix, if any CopyEngineTests.cs
errors surface, engineer investigates stub types only — no test method changes.
