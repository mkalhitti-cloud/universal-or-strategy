# B122 Plan Review

**Block**: B122
**Phase**: 2 (Plan Review)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-25
**Input**: docs/brain/B122/02-architecture-plan.md
**Result**: **REVIEW_PASS**

---

## Checklist Results

### R1 — Root cause identification (SKGL PackageReference)

| Finding | Status |
|---------|--------|
| Plan claims `<PackageReference Include="SKGL.Extension" Version="2.0.23" />` at csproj line 85 | VERIFIED — confirmed in `src/PropTraderTools/PropTraderTools.csproj:85` |
| Plan claims duplicate DLL HintPath Reference at lines 56-59 | VERIFIED — `<Reference Include="SKGL.Extension"><HintPath>...</HintPath></Reference>` at lines 56-59 |
| NU1101 error description matches structure | VERIFIED — PackageReference forces NuGet restore; SKGL.Extension does not exist on nuget.org |

**R1: PASS**

---

### R2 — CS0433 fix strategy

| Finding | Status |
|---------|--------|
| Plan states existing `<NoWarn>` at line 26 = `MSB3245;MSB3246;CS0012;CS8632;CS0234;CS0246;CS0436` | VERIFIED — csproj line 26 matches exactly |
| Fix is conditional on CS0433 appearing after Defect 1 fix | SOUND — avoids unnecessary suppression |
| All `Globals` usages are `NinjaTrader.Core.Globals.UserDataDir` (fully qualified) | SOUND — no call-site .cs change required |
| Fix uses `<NoWarn>` extension (project-file only) | CORRECT — no .cs file changes planned |

**R2: PASS**

---

### R3 — CopyEngineTests.cs strategy

| Finding | Status |
|---------|--------|
| Plan does NOT propose any changes to `CopyEngineTests.cs` | VERIFIED — ticket constraints explicitly ban .cs modifications |
| Plan does NOT propose any test deletions | VERIFIED — Section F constraints state "DO NOT add or remove test methods" |
| Plan does NOT restructure `CopyEngineTests.cs` | VERIFIED — constraints explicit |
| `CopyEngineTests.cs` confirmed in csproj `<Compile>` list | VERIFIED — `PropTraderTools.csproj:101` |
| File uses xUnit `[Fact]` reflection-based stubs (confirmed header L1-100) | VERIFIED — uses `BindingFlags.NonPublic`, xUnit `[Fact]`, no MSTest/NUnit |

**R3: PASS**

---

### R4 — RULES_CATALOG.md compliance

| Rule | Description | Assessment |
|------|-------------|------------|
| JS-021 | No `lock()` | PASS — no .cs changes; existing CopyEngine.cs L4086-4097 uses lock-free `AddOrUpdate` (confirmed) |
| JS-001 | No throw in hot path | PASS — no .cs changes |
| JS-033 | No `async void` | PASS — no .cs changes |
| JS-002 | No null return for missing values | PASS — no .cs changes |
| JS-051 | xUnit only | PASS — xUnit PackageReference retained; CopyEngineTests.cs confirmed xUnit |
| JS-066 | Diff < 10k chars | PASS — single XML line removal |

**P0 violations introduced: ZERO**
**P0 violations in scoped files: ZERO** (CopyEngine.cs L4080-4110 reviewed — lock-free ConcurrentDictionary usage; no `lock()`)

**R4: PASS**

---

### R5 — Success criteria completeness

| Criterion | Present in Plan |
|-----------|----------------|
| 0 errors, `Build succeeded` | YES — Section D item 1 |
| 0 new warnings | YES — Section D item 1 |
| B120/B119/B118Tests.cs structurally intact | YES — Section D item 2 |
| Test count >= current | YES — Section D item 3 |
| CYC unchanged (no logic modification) | YES — Section D item 4 |
| SKGL.Extension.dll resolved via HintPath after fix | YES — Section D item 5 |

**R5: PASS**

---

### R6 — Section F 7-scan checklist

| Scan | Command | Gate | Present |
|------|---------|------|---------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools --include="*.cs"` | 0 results | YES |
| SCAN-02 | `grep -rn "async void " src/PropTraderTools --include="*.cs"` | 0 new results | YES |
| SCAN-03 | Diff shows only `PropTraderTools.csproj` | .cs files untouched | YES |
| SCAN-04 | `dotnet build 2>&1 \| grep -c "error"` | must return 0 | YES |
| SCAN-05 | All `Globals.` usages remain `NinjaTrader.Core.Globals.UserDataDir` | fully qualified | YES |
| SCAN-06 | xUnit PackageReference still present in csproj | still present | YES |
| SCAN-07 | SKGL.Extension HintPath Reference still present | HintPath Reference untouched | YES |

All 7 scans present with correct gates.

**R6: PASS**

---

### R7 — Single-ticket scope appropriateness

Both changes (mandatory: remove 1 XML line; conditional: append 1 token to `<NoWarn>`) target the same file `PropTraderTools.csproj`. No .cs file is touched. Single ticket is minimal and appropriate.

**R7: PASS**

---

## Spec Coverage Matrix

| Requirement (DW-PTT-BE-FIX-03) | Addressed | Plan Section |
|---------------------------------|-----------|--------------|
| Remove bogus SKGL.Extension PackageReference | YES | Section B, Section F Step 1 |
| Restore build to 0 errors | YES | Section D item 1 |
| Handle CS0433 Globals ambiguity (conditional) | YES | Section C, Section F Step 3 |
| Preserve all existing tests | YES | Section D items 2–3, Section F constraints |
| No .cs file modifications | YES | Section F constraints (hard bans) |

---

## Violations Found

**NONE.**

---

## Result

**REVIEW_PASS**

The plan is minimal, correct, and fully compliant with RULES_CATALOG.md. All findings are verified against source files. No P0 or P1 violations are introduced. The 7-scan checklist is complete with correct gates.
