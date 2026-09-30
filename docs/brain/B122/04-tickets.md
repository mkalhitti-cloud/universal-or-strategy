# B122 Tickets — Build Remediation (DW-PTT-BE-FIX-03)

**Block**: B122
**Phase**: 3 (Ticket Generation)
**Source Plan**: docs/brain/B122/02-architecture-plan.md (REVIEW_PASS)
**Plan Review**: docs/brain/B122/02-plan-review.md (REVIEW_PASS)
**Date**: 2026-08-25
**Total Tickets**: 1

---

## Ticket 1 — B122-T1: Fix pre-existing build errors blocking test suite

### Spec Requirement IDs
- DW-PTT-BE-FIX-03 (from `docs/brain/B107/06-deferred-backlog.md`)

### Problem Statement

Two pre-existing defects block `dotnet build src/PropTraderTools/PropTraderTools.csproj`:

**Defect 1 (CONFIRMED BLOCKING — fix is MANDATORY)**:
`NU1101: Unable to find package SKGL.Extension. No packages exist with this id in source(s): nuget.org`

- **Root cause**: A bogus `<PackageReference Include="SKGL.Extension" Version="2.0.23" />` at
  [`src/PropTraderTools/PropTraderTools.csproj:85`](../../src/PropTraderTools/PropTraderTools.csproj:85)
  forces NuGet restore to attempt a download that cannot succeed. SKGL.Extension does not exist on NuGet.
- **Evidence**: `dotnet build` returns `1 Error(s)` before any compilation begins.
- **Non-issue**: The correct HintPath DLL reference already exists at lines 56-59:
  ```xml
  <Reference Include="SKGL.Extension">
    <HintPath>$(USERPROFILE)\Documents\NinjaTrader 8\bin\Custom\SKGL.Extension.dll</HintPath>
    <Private>false</Private>
  </Reference>
  ```
  The DLL reference is sufficient. The PackageReference is redundant and broken.

**Defect 2 (CONDITIONAL — verify after Defect 1 fix)**:
`CS0433: The type 'Globals' exists in both 'NinjaTrader.Core' and 'NinjaTrader.Custom'`

- This error cannot be observed until Defect 1 is fixed (restore failure blocks compilation).
- Current `<NoWarn>` at line 26 already suppresses `CS0436` (related type-conflict warning) but
  lacks `CS0433`. If `CS0433` appears in the post-fix build output, it must be suppressed.
- All `Globals` usages in production code are already fully qualified as
  `NinjaTrader.Core.Globals.UserDataDir` — no call-site `.cs` changes are needed.

---

### Files to Edit

**ONLY the following files may be touched. Engineer may NOT modify any other file.**

| File | Edit Type | Condition |
|------|-----------|-----------|
| [`src/PropTraderTools/PropTraderTools.csproj`](../../src/PropTraderTools/PropTraderTools.csproj) | XML line deletion | MANDATORY — Edit 1 |
| [`src/PropTraderTools/PropTraderTools.csproj`](../../src/PropTraderTools/PropTraderTools.csproj) | XML token append | CONDITIONAL — Edit 2, only if CS0433 fires |

**Hard bans (engineer contract — zero exceptions)**:
- DO NOT modify `CopyEngine.cs`
- DO NOT modify `LicenseClient.cs`
- DO NOT modify `TradeCopierAddOn.cs`
- DO NOT modify `TradeCopierWindow.cs`
- DO NOT modify `TradeCopierPanel.cs`
- DO NOT modify `CopyEngineTests.cs`
- DO NOT modify any file under `Tests/`
- DO NOT add or remove test methods from any test file
- DO NOT restructure any test file

---

### Method Signatures

**N/A** — This ticket contains no new methods. All changes are XML edits to
[`PropTraderTools.csproj`](../../src/PropTraderTools/PropTraderTools.csproj) only.

CYC delta = 0 by construction (no logic changes to any `.cs` file).

---

### Precise Edit Instructions

#### Edit 1 — PRIMARY (REQUIRED, no condition)

**File**: [`src/PropTraderTools/PropTraderTools.csproj`](../../src/PropTraderTools/PropTraderTools.csproj:85)
**Action**: Delete line 85 exactly.

**Line to delete** (exact text — confirm with `read_file` before editing):
```xml
    <PackageReference Include="SKGL.Extension" Version="2.0.23" />
```

**Context** (surrounding lines that MUST remain untouched):
```xml
  <!-- lines 83-90 before edit -->
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.6.6" />
    <PackageReference Include="SKGL.Extension" Version="2.0.23" />    <!-- DELETE THIS LINE -->
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.7">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
```

**Result after edit** (the ItemGroup must look exactly like this):
```xml
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.6.6" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.7">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
```

**Rationale**: The HintPath `<Reference>` at lines 56-59 already provides the `SKGL.Extension.dll`
assembly to the compiler. `LicenseClient.cs` and all other consumers reference the type via that DLL.
The `PackageReference` is a duplicate that forces a failing NuGet restore. Deleting it removes the
blocking restore step while leaving the DLL reference intact.

---

#### Edit 2 — CONDITIONAL (apply ONLY if `CS0433` appears in post-Edit-1 build output)

**Trigger**: After Edit 1, run `dotnet build` and examine output. Apply Edit 2 if and ONLY if the
output contains the literal text `CS0433`.

**File**: [`src/PropTraderTools/PropTraderTools.csproj`](../../src/PropTraderTools/PropTraderTools.csproj:26)
**Action**: Append `;CS0433` to the `<NoWarn>` element at line 26.

**Before** (line 26 exact text):
```xml
    <NoWarn>MSB3245;MSB3246;CS0012;CS8632;CS0234;CS0246;CS0436</NoWarn>
```

**After**:
```xml
    <NoWarn>MSB3245;MSB3246;CS0012;CS8632;CS0234;CS0246;CS0436;CS0433</NoWarn>
```

**Rationale**: `CS0433` is the numeric counterpart to the already-suppressed `CS0436`. Both cover the
scenario where two referenced assemblies (`NinjaTrader.Core.dll` and `NinjaTrader.Custom.dll`) each
define a type named `Globals`. This project is LSP-only (see header comment); the `<NoWarn>` list
controls OmniSharp/IntelliSense compile, not NT8's production Roslyn host.

**If CS0433 does NOT appear**: Skip Edit 2 entirely. Do not add the suppression speculatively.

---

### Build Verification (MANDATORY — engineer must execute after each edit)

**After Edit 1 only**:
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 30
```
Expected: NuGet restore succeeds. Compilation begins. Error count drops dramatically (may reach 0).

**After all edits (final gate)**:
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 30
```
Expected output must contain:
```
Build succeeded.
    0 Error(s)
```

**Failure protocol**: If `Build succeeded. 0 Error(s)` is not achieved, engineer must report
remaining errors verbatim in `ticket-1-completion.md`. Do NOT attempt silent workarounds beyond
Edits 1 and 2 defined above. Escalate to architect with exact error output.

---

### Test Verification (after build succeeds)

```powershell
dotnet test src/PropTraderTools/PropTraderTools.csproj --no-build 2>&1 | Select-Object -Last 30
```

**Required passing files** (must appear in test output with passing status):
- `Tests/B120Tests.cs`
- `Tests/B119Tests.cs`
- `Tests/B118Tests.cs`

**Test count invariant**: Total passing test count must be >= count before this ticket.
No test methods may have been deleted. Verify by checking that none of the following file names
appear in the failure list:
- `B112Tests.cs`, `B113Tests.cs`, `B115Tests.cs`, `B116Tests.cs`, `B117Tests.cs`
- `B118Tests.cs`, `B119Tests.cs`, `B120Tests.cs`, `BgtmTests.cs`

---

### JS Rules Applicable to This Ticket

| Rule ID | Description | Applies? | Status |
|---------|-------------|----------|--------|
| JS-021 (P0) | No `lock()` | No `.cs` changes — existing baseline applies | PASS by construction |
| JS-001 (P0) | No throw in hot path | No `.cs` changes | PASS by construction |
| JS-033 (P0) | No `async void` | No `.cs` changes | PASS by construction |
| JS-002 (P0) | No `return null` | No `.cs` changes | PASS by construction |
| JS-051 (P1) | xUnit only (no NUnit/MSTest) | xUnit PackageReference retained at line 84 | PASS — verify in SCAN-06 |
| JS-066 (P1) | Diff < 10k chars | Single XML line deletion (~55 chars) | PASS — well within limit |
| JS-010 (P0) | Private constructors | No new types | N/A |
| JS-036 (P0) | Span / no new byte[] | No `.cs` changes | N/A |

**P0 violations introduced by this ticket**: ZERO.

---

### 7-Scan Checklist (ENGINEER CONTRACT — all 7 must reach zero before BUILD_PASS)

Engineer MUST run each scan, capture the output, and include it in `ticket-1-completion.md`.
A scan result of "0 results" (or "no lines found") satisfies the gate.

---

**SCAN-01 — No `lock()` in modified files (JS-021)**
```powershell
Select-String -Path "src/PropTraderTools/PropTraderTools.csproj" -Pattern "lock\("
```
Gate: 0 results.
Note: `.csproj` is XML — no C# `lock()` can exist. Command confirms clean edit.

---

**SCAN-02 — No `async void` in modified files (JS-033)**
```powershell
Select-String -Path "src/PropTraderTools/PropTraderTools.csproj" -Pattern "async void"
```
Gate: 0 results.
Note: `.csproj` is XML — no C# syntax. Confirms only the intended XML was changed.

---

**SCAN-03 — Diff shows only `PropTraderTools.csproj` (no `.cs` file touched)**
```powershell
git diff --name-only
```
Gate: Output must list `src/PropTraderTools/PropTraderTools.csproj` only.
Any `.cs` file in the diff output = SCAN-03 FAIL. Stop and escalate.

---

**SCAN-04 — Build produces 0 errors (JS-066)**
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-String -Pattern "Error\(s\)"
```
Gate: Output line must read `0 Error(s)`.
Example passing output: `    0 Warning(s)` / `    0 Error(s)` / `Build succeeded.`

---

**SCAN-05 — All `Globals` usages remain fully qualified (no call-site regression)**
```powershell
Select-String -Path "src/PropTraderTools/*.cs","src/PropTraderTools/**/*.cs" -Pattern "Globals\." -Recurse |
  Where-Object { $_.Line -notmatch "NinjaTrader\.Core\.Globals\." -and $_.Line -notmatch "NinjaTrader\.Custom\.Globals\." }
```
Gate: 0 results (all `Globals.` references are namespace-qualified).
This confirms no call-site was silently un-qualified by any edit.

---

**SCAN-06 — xUnit PackageReference still present in csproj (JS-051)**
```powershell
Select-String -Path "src/PropTraderTools/PropTraderTools.csproj" -Pattern "xunit"
```
Gate: Must return at least 2 lines containing `xunit` (the `xunit` and `xunit.runner.visualstudio`
PackageReferences). If 0 lines returned, the xUnit references were accidentally deleted — FAIL.

---

**SCAN-07 — SKGL.Extension HintPath Reference still present (DLL reference untouched)**
```powershell
Select-String -Path "src/PropTraderTools/PropTraderTools.csproj" -Pattern "SKGL\.Extension"
```
Gate: Must return exactly 1 line containing `SKGL.Extension` after Edit 1 (the HintPath Reference
at line 56). If 0 lines: HintPath was deleted — FAIL. If 2 lines: PackageReference was not removed — FAIL.

---

### Completion Artifact

Engineer writes: `docs/brain/B122/ticket-1-completion.md`

This file is the verification contract. It MUST include all of the following:

1. **Exact edits made**: for each edit, provide `file:line`, the old text, and the new text.
2. **Build output after Edit 1**: verbatim last 30 lines of `dotnet build`.
3. **Build output after all edits**: verbatim last 30 lines. Must show `0 Error(s)`.
4. **7-scan results**: one section per scan (SCAN-01 through SCAN-07), each showing the command
   run and its output. All must show zero/passing.
5. **Test run output**: verbatim last 30 lines of `dotnet test --no-build`.
6. **Status line** (final line of the file):
   - `Status: BUILD_PASS` — if `0 Error(s)` and all 7 scans pass.
   - `Status: BUILD_FAIL: <reason>` — if any gate fails; include exact error text.

---

*End of B122 Ticket 1*
