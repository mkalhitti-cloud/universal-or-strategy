# B48-LaneA — Architecture Plan
**Block**: PTT-COPIER-B48 Lane A  
**Topic**: DW-B44-01 Test File Isolation — Move B*Tests.cs out of NT8 compile path  
**Status**: REVIEW_PASS pending  
**Date**: 2026-08-07  

---

## 1. Problem Statement

### Why test files in `src/PropTraderTools/` (flat) break F5

NinjaTrader 8 hosts its own Roslyn compiler instance. When NT8 loads an AddOn, it scans every `.cs` file at the **flat** registered AddOn path:

```
%USERPROFILE%\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\*.cs
```

This scan is **non-recursive** (flat glob only). NT8 has no knowledge of the dotnet SDK, xUnit NuGet packages, or the `PropTraderTools.csproj` file. It attempts to compile every `.cs` file it finds using its internal NT8 Roslyn host, which does NOT have xUnit or `Microsoft.NET.Test.Sdk` in scope.

**Result**: Every xUnit test file triggers:
- `CS0246: The type or namespace name 'Xunit' could not be found`
- `CS0246: The type or namespace name 'FactAttribute' could not be found`
- `CS0103: The name 'Assert' does not exist`

These are F5 compilation errors that block any NinjaScript AddOn load.

### Current broken state (Wave workspace)

| File | In `src/PropTraderTools/` | In `csproj <Compile>` | In `$DeployExcludes` |
|---|---|---|---|
| `CopyEngineTests.cs` | ✅ root | ✅ | ✅ |
| `B42Tests.cs` | ✅ root | ✅ | ✅ |
| `B43Tests.cs` | ✅ root | ✅ | ✅ |
| `B44Tests.cs` | ✅ root | ✅ | ✅ |
| `B45Tests.cs` | ✅ root | ✅ | ✅ |
| `B46Tests.cs` | ✅ root | ❌ **MISSING** | ❌ **MISSING** |
| `B47Tests.cs` | ❌ does not exist | ❌ | ❌ |

`B46Tests.cs` has **three independent bugs**:
1. Not in `$DeployExcludes` → `verify_links.ps1` reports MISSING → FAIL
2. Not in csproj `<Compile>` → no IntelliSense / build coverage
3. At root → if ever deployed (without fix 1), NT8 F5 would fail

The `$DeployExcludes` mechanism in `verify_links.ps1` prevents hard-linking named files to NT8. This has been working for B42–B45. The problem is incomplete coverage.

---

## 2. Option A Analysis — NT8 Subfolder Scan Behavior

### NT8 flat-scan behavior (confirmed — NT8-054)

NT8's Roslyn host scans `AddOns\PropTraderTools\*.cs` using a flat (non-recursive) pattern. Files located in a subdirectory such as `AddOns\PropTraderTools\Tests\B42Tests.cs` are **not compiled** by NT8.

This behavior has been confirmed and is documented in `docs/standards/NT8_COMPILER_RULES.md` (NT8-054: xUnit test files cause CS0246/CS0103 when present in the NT8 compile path).

### `verify_links.ps1` behavior with subfolders

The hard-link deploy script uses:
```powershell
Get-ChildItem $SrcPath -Filter "*.cs" -Recurse | Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' }
```

It **recursively scans** `src/PropTraderTools/` including any subdirectory. If a `Tests\` subfolder exists, the script would discover `Tests\B42Tests.cs` etc., compute `$relPath = "Tests\B42Tests.cs"`, and attempt to create hard link at `NT8\...\PropTraderTools\Tests\B42Tests.cs`.

The existing `$DeployExcludes` check uses **`$_.Name`** (bare filename, not full path):
```powershell
if ($DeployExcludes -contains $_.Name) { ... return }
```

This means `$DeployExcludes` checks by filename regardless of subdirectory depth. `B42Tests.cs` in `Tests\` is still matched. **However**, B46Tests.cs is not in `$DeployExcludes`, so it would NOT be excluded — verify_links.ps1 would attempt to hard-link it to NT8.

### Two-layer defense-in-depth (chosen approach)

To be robust regardless of future filename additions, this block adds a **directory-based primary exclusion** to verify_links.ps1:

```powershell
# Layer 1: Skip entire Tests subfolder (xUnit files, never deployed to NT8)
if ($_.FullName -match '\\Tests\\') {
    Write-Host "SKIP     : $displayName  (Tests subfolder -- not deployed to NT8)" -ForegroundColor DarkGray
    $skipped++
    return
}
```

This check fires BEFORE the `$DeployExcludes` check for any file under `Tests\`. It is future-proof: any new block test file (B49Tests.cs, B50Tests.cs, etc.) placed in `Tests\` is automatically excluded from deployment without modifying `$DeployExcludes`.

`$DeployExcludes` also receives `"B46Tests.cs"` as a **defense-in-depth** entry — if a file is ever accidentally relocated back to root, it still won't be deployed.

---

## 3. Chosen Approach — Option A: Move to `Tests\` Subfolder

**Physical path**: `src/PropTraderTools/Tests/` (subfolder within the existing PropTraderTools directory)

**Rationale**:
- Keeps all files within the single MSBuild project scope (csproj in `src/PropTraderTools/`)
- csproj relative paths stay clean: `Tests\B42Tests.cs`
- `verify_links.ps1` path coverage unchanged (already recurses into subdirs)
- NT8 flat scan never sees files in `Tests\` subfolder
- `dotnet build` resolves `Tests\*.cs` files normally
- IDE IntelliSense continues to work (all files remain in csproj)

**CopyEngineTests.cs exception**: This file intentionally STAYS at `src/PropTraderTools/CopyEngineTests.cs` (root level). It references `CopyEngine`'s `private readonly struct CopyRule` nested type directly — only valid when both files compile as the same assembly. A separate test project cannot compile it due to CS0246 on the private nested type. It is already in `$DeployExcludes`. **No change to CopyEngineTests.cs handling.**

---

## 4. Architecture Decisions — Exact File Changes

### 4.1 New directory

```
CREATE: src/PropTraderTools/Tests/
```

No `.gitkeep` needed — the 5 moved files populate the directory.

---

### 4.2 File moves (Wave workspace)

Engineer MUST use `git mv` to preserve Git history:

```powershell
git mv src/PropTraderTools/B42Tests.cs src/PropTraderTools/Tests/B42Tests.cs
git mv src/PropTraderTools/B43Tests.cs src/PropTraderTools/Tests/B43Tests.cs
git mv src/PropTraderTools/B44Tests.cs src/PropTraderTools/Tests/B44Tests.cs
git mv src/PropTraderTools/B45Tests.cs src/PropTraderTools/Tests/B45Tests.cs
git mv src/PropTraderTools/B46Tests.cs src/PropTraderTools/Tests/B46Tests.cs
```

After move, `src/PropTraderTools/` contains **no** `B*Tests.cs` files at root. `CopyEngineTests.cs` remains at root.

---

### 4.3 `PropTraderTools.csproj` — exact changes

**File**: `src/PropTraderTools/PropTraderTools.csproj`

Change these four existing `<Compile>` entries (update path prefix):

```xml
<!-- BEFORE -->
<Compile Include="B42Tests.cs" />
<Compile Include="B43Tests.cs" />
<Compile Include="B44Tests.cs" />
<Compile Include="B45Tests.cs" />

<!-- AFTER -->
<Compile Include="Tests\B42Tests.cs" />
<Compile Include="Tests\B43Tests.cs" />
<Compile Include="Tests\B44Tests.cs" />
<Compile Include="Tests\B45Tests.cs" />
```

**Also ADD** the missing B46Tests.cs entry (was never in csproj — bug fix):

```xml
<!-- ADD (after B45Tests.cs entry) -->
<!-- B46: ATM empty guard + combo auto-select tests -->
<Compile Include="Tests\B46Tests.cs" />
```

The section comment for B42 should also be updated:
```xml
<!-- BEFORE -->
<!-- B42: PTTFollowerStrategy + tests -->
<Compile Include="Features\PttFollowerStrategy.cs" />
<Compile Include="Tests\B42Tests.cs" />   <!-- path already updated above -->

<!-- AFTER (comment only) -->
<!-- B42: PTTFollowerStrategy (src) + B42Tests (Tests\) -->
```

Full resulting test `<Compile>` block in the csproj (order preserved):

```xml
<!-- B42: PTTFollowerStrategy (src) + B42Tests (Tests\) -->
<Compile Include="Features\PttFollowerStrategy.cs" />
<Compile Include="Tests\B42Tests.cs" />
<!-- B43: ATM template ComboBox tests -->
<Compile Include="Tests\B43Tests.cs" />
<!-- B44: Subscribe/Unsubscribe idempotency tests -->
<Compile Include="Tests\B44Tests.cs" />
<!-- B45: OnApplyRule late-resolve + PttFollowerStrategy StartBehavior tests -->
<Compile Include="Tests\B45Tests.cs" />
<!-- B46: ATM empty guard + combo auto-select tests -->
<Compile Include="Tests\B46Tests.cs" />
```

---

### 4.4 `scripts/verify_links.ps1` — exact changes

**File**: `scripts/verify_links.ps1`

**Change 1** — add B46Tests.cs to `$DeployExcludes` (defense-in-depth):

```powershell
# BEFORE
$DeployExcludes = @("CopyEngineTests.cs", "B42Tests.cs", "B43Tests.cs", "B44Tests.cs", "B45Tests.cs")

# AFTER
$DeployExcludes = @("CopyEngineTests.cs", "B42Tests.cs", "B43Tests.cs", "B44Tests.cs", "B45Tests.cs", "B46Tests.cs")
```

**Change 2** — add directory-based primary skip, inserted BEFORE the existing `$DeployExcludes` check in the `ForEach-Object` body:

```powershell
# Skip the entire Tests\ subfolder (xUnit files -- never deployed to NT8, NT8 flat scan only)
if ($_.FullName -match '\\Tests\\') {
    Write-Host "SKIP     : $displayName  (Tests subfolder -- not deployed to NT8)" -ForegroundColor DarkGray
    $skipped++
    return
}
```

Insertion point: after the `$displayName = $relPath` assignment and BEFORE the existing `if ($DeployExcludes -contains $_.Name)` block.

**Complete resulting exclusion block** (showing both layers):

```powershell
$displayName = $relPath

# Layer 1: Skip entire Tests\ subfolder (xUnit files -- never deployed to NT8)
if ($_.FullName -match '\\Tests\\') {
    Write-Host "SKIP     : $displayName  (Tests subfolder -- not deployed to NT8)" -ForegroundColor DarkGray
    $skipped++
    return
}

# Layer 2: Skip individual named test files at root (defense-in-depth)
if ($DeployExcludes -contains $_.Name) {
    Write-Host "SKIP     : $displayName  (test file -- not deployed to NT8)" -ForegroundColor DarkGray
    $skipped++
    return
}
```

---

### 4.5 `docs/standards/NT8_ADDON_KNOWLEDGE.md` — append

Append a new `## B48` section documenting:
- The `Tests\` subfolder pattern for NT8 isolation
- The two-layer exclusion approach in `verify_links.ps1`
- The convention for future blocks (B49+): new `*Tests.cs` files go to `Tests\` and get a `<Compile Include="Tests\B*Tests.cs" />` entry; no need to update `$DeployExcludes`
- Note: `CopyEngineTests.cs` remains at root permanently (private type access requirement)

---

### 4.6 `docs/standards/NT8_COMPILER_RULES.md` — NO CHANGE

NT8-054 already accurately documents: xUnit files cause CS0246/CS0103 in the NT8 compile path; the fix is `$DeployExcludes` in `verify_links.ps1`. The subfolder approach is a deployment convention, not a new compiler rule. No NT8-054 update needed.

---

## 5. Acceptance Criteria

| # | Criterion | Verification Command |
|---|---|---|
| AC-01 | F5 in NinjaTrader produces zero errors | Manual F5 after `verify_links.ps1 -Fix` |
| AC-02 | No `B*Tests.cs` at root of `src/PropTraderTools/` | `Get-ChildItem src\PropTraderTools -Filter "*Tests.cs"` → only `CopyEngineTests.cs` |
| AC-03 | All 5 test files present in `Tests\` subfolder | `Get-ChildItem src\PropTraderTools\Tests -Filter "*.cs"` → 5 files |
| AC-04 | csproj has `Tests\B46Tests.cs` entry | `Select-String -Path "*.csproj" -Pattern "Tests\\\\B46Tests"` → 1 match |
| AC-05 | csproj has no root-level `B*Tests.cs` entry | `Select-String -Path "*.csproj" -Pattern '"B4[2-9]Tests.cs"'` → 0 matches |
| AC-06 | verify_links.ps1 PASS | `powershell -File scripts\verify_links.ps1` → exit 0, PASS line |
| AC-07 | verify_links.ps1 contains `\\Tests\\` skip | `Select-String -Path "scripts\verify_links.ps1" -Pattern "\\\\Tests\\\\"` → 1 match |
| AC-08 | verify_links.ps1 contains B46Tests.cs | `Select-String -Path "scripts\verify_links.ps1" -Pattern '"B46Tests.cs"'` → 1 match |
| AC-09 | dotnet build passes | `dotnet build src\PropTraderTools\PropTraderTools.csproj` → exit 0 |
| AC-10 | NT8_ADDON_KNOWLEDGE.md has B48 section | `Select-String -Path "docs\standards\NT8_ADDON_KNOWLEDGE.md" -Pattern "^## B48"` → 1 match |

---

## 6. Seven-Scan Checklist (Engineer Contract — SCAN-01 through SCAN-07)

These scans MUST return zero violations after the engineer's work is complete. The ptt-verifier runs all seven before declaring the ticket green.

| ID | Scan Purpose | Command | Expected Result |
|---|---|---|---|
| SCAN-01 | No test files at root of PropTraderTools | `Get-ChildItem "src\PropTraderTools" -Filter "*Tests.cs" \| Where-Object {$_.FullName -notmatch '\\Tests\\'} \| Where-Object {$_.Name -ne 'CopyEngineTests.cs'}` | 0 results |
| SCAN-02 | csproj has no root-level B*Tests.cs entries | `Select-String -Path "src\PropTraderTools\PropTraderTools.csproj" -Pattern '"B4[2-9]Tests\.cs"'` | 0 matches |
| SCAN-03 | csproj has all 5 Tests\ entries | `Select-String -Path "src\PropTraderTools\PropTraderTools.csproj" -Pattern 'Tests\\B4[2-9]Tests'` | 5 matches |
| SCAN-04 | verify_links.ps1 has directory-based skip | `Select-String -Path "scripts\verify_links.ps1" -Pattern '\\\\Tests\\\\'` | >=1 match |
| SCAN-05 | verify_links.ps1 has B46Tests.cs in DeployExcludes | `Select-String -Path "scripts\verify_links.ps1" -Pattern '"B46Tests\.cs"'` | >=1 match |
| SCAN-06 | No lock() in changed PowerShell files | `Select-String -Path "scripts\verify_links.ps1" -Pattern 'lock\s*\('` | 0 matches |
| SCAN-07 | dotnet build exit code 0 | `dotnet build src\PropTraderTools\PropTraderTools.csproj 2>&1; $LASTEXITCODE` | exit code 0 |

---

## 7. Out of Scope

The following items are **explicitly NOT addressed** by this block:

| Item | Status | Responsible |
|---|---|---|
| Fix `CopyEngineTests.cs` 60 compile errors (CS0246 `CopyRule`, CS0234 `System.Collections.Immutable`, CS0433 `Globals`, CS0246 `DisarmTrailBe`) | Open — DW-B44-01 sub-item 2 | Future block |
| Enable `dotnet test` to execute CopyEngine tests | Blocked by above | Future block |
| Create `B47Tests.cs` | Open — DW-B47-01 | Lane C |
| Add `<Compile Include="Tests\B47Tests.cs" />` to csproj | Deferred — B47Tests.cs does not exist | Whoever delivers B47Tests.cs |
| Move `CopyEngineTests.cs` to `Tests\` subfolder | WILL NOT DO — private type access requirement | Never |

---

## 8. Deferred Items from Previous Blocks

| DW ID | Source Block | Description | Still Open? |
|---|---|---|---|
| DW-B44-01 (sub-item 2) | B44-LaneA | `CopyEngineTests.cs` 60 accumulated errors prevent `dotnet test` | **YES** — unaddressed |
| DW-B47-01 | B47-LaneA (planned) | `B47Tests.cs` creation for block-specific test coverage | **YES** — Lane C responsible |
| Future | B48-LaneA | Add `<Compile Include="Tests\B47Tests.cs" />` when B47Tests.cs is delivered | Conditional on DW-B47-01 |

### Convention for future blocks (B49+)

Any new block test file (e.g., `B49Tests.cs`) MUST:
1. Be created directly at `src/PropTraderTools/Tests/B49Tests.cs`
2. Have a `<Compile Include="Tests\B49Tests.cs" />` entry added to `PropTraderTools.csproj`
3. NOT be added to `$DeployExcludes` (the `\\Tests\\` directory-based exclusion covers it automatically)

This convention is documented in `NT8_ADDON_KNOWLEDGE.md` B48 section.

---

## 9. Component Summary

| Component | Change Type | File Path (Wave workspace) |
|---|---|---|
| Tests subfolder | NEW DIRECTORY | `src/PropTraderTools/Tests/` |
| B42Tests.cs | MOVE (git mv) | `src/PropTraderTools/Tests/B42Tests.cs` |
| B43Tests.cs | MOVE (git mv) | `src/PropTraderTools/Tests/B43Tests.cs` |
| B44Tests.cs | MOVE (git mv) | `src/PropTraderTools/Tests/B44Tests.cs` |
| B45Tests.cs | MOVE (git mv) | `src/PropTraderTools/Tests/B45Tests.cs` |
| B46Tests.cs | MOVE (git mv) | `src/PropTraderTools/Tests/B46Tests.cs` |
| PropTraderTools.csproj | EDIT (4 path updates + 1 add) | `src/PropTraderTools/PropTraderTools.csproj` |
| verify_links.ps1 | EDIT (1 array entry + 1 if block) | `scripts/verify_links.ps1` |
| NT8_ADDON_KNOWLEDGE.md | APPEND (B48 section) | `docs/standards/NT8_ADDON_KNOWLEDGE.md` |
| NT8_COMPILER_RULES.md | NO CHANGE | — |
| CopyEngineTests.cs | NO CHANGE (stays at root) | `src/PropTraderTools/CopyEngineTests.cs` |

---

## 10. Threading Model

No threading concerns. This block contains:
- File move operations (OS/git level)
- XML edits (MSBuild csproj)
- PowerShell script edits (verify_links.ps1)
- Markdown documentation

No `lock()`, no `async void`, no `Dispatcher.InvokeAsync`, no `ConcurrentQueue`. JS Rules JS-021, JS-033, JS-001 are not applicable — no C# source code is written in this block.

---

**PLAN_COMPLETE**
