# B48-LaneA — Tickets
**Block**: PTT-COPIER-B48 Lane A
**Topic**: DW-B44-01 Test File Isolation — Move B*Tests.cs out of NT8 compile path
**Plan**: `docs/brain/B48-LaneA/02-architecture-plan.md` — REVIEW_PASS
**Generated**: 2026-08-07
**Engineer workspace**: `c:\WSGTA\universal-or-strategy`
**Director workspace**: `c:\WSGTA\universal-or-strategy-director`

---

## Spec Requirements Covered by this Ticket Set

| Req | Description |
|-----|-------------|
| R1 | Move B42–B46Tests.cs out of NT8 compile path (B47 deferred — does not exist) |
| R2 | F5 produces zero errors after change |
| R3 | Use `Tests\` subfolder — NT8 flat scan does not recurse into subdirs |
| R4 | Test files remain compilable by `dotnet build` |
| R5 | `verify_links.ps1` must not hard-link test files to NT8 bin |
| R6 | `PropTraderTools.csproj` references all moved files at new paths |
| R7 | No new P0 JS rule violations introduced |

---

## Ticket T1 — Create `Tests\` Subfolder and Move 5 Test Files

### Identity
- **Ticket ID**: T1
- **Title**: Create `Tests\` subfolder and move B42–B46Tests.cs using `git mv`
- **Spec Reqs**: R1, R2, R3
- **Workspace**: Wave (`c:\WSGTA\universal-or-strategy`)

### Pre-condition
All 5 files exist at the flat root of `src/PropTraderTools/`:
```
c:\WSGTA\universal-or-strategy\src\PropTraderTools\B42Tests.cs
c:\WSGTA\universal-or-strategy\src\PropTraderTools\B43Tests.cs
c:\WSGTA\universal-or-strategy\src\PropTraderTools\B44Tests.cs
c:\WSGTA\universal-or-strategy\src\PropTraderTools\B45Tests.cs
c:\WSGTA\universal-or-strategy\src\PropTraderTools\B46Tests.cs
```
The `Tests\` subfolder does NOT yet exist.
`CopyEngineTests.cs` remains at root throughout — do NOT touch it.

### Work: Exact Shell Commands (Wave workspace root)

Run ALL of the following in order from `c:\WSGTA\universal-or-strategy`:

```powershell
# Step 1: Create the Tests\ directory (git mv creates it automatically,
# but creating it first ensures git tracks the directory entry)
New-Item -ItemType Directory -Path "src\PropTraderTools\Tests" -Force

# Step 2: Move each file with git mv to preserve git history
git mv src/PropTraderTools/B42Tests.cs src/PropTraderTools/Tests/B42Tests.cs
git mv src/PropTraderTools/B43Tests.cs src/PropTraderTools/Tests/B43Tests.cs
git mv src/PropTraderTools/B44Tests.cs src/PropTraderTools/Tests/B44Tests.cs
git mv src/PropTraderTools/B45Tests.cs src/PropTraderTools/Tests/B45Tests.cs
git mv src/PropTraderTools/B46Tests.cs src/PropTraderTools/Tests/B46Tests.cs
```

### Post-condition (verify before closing T1)

1. `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Tests\` directory exists.
2. All 5 `B*Tests.cs` files are present at `src/PropTraderTools/Tests/`.
3. `src/PropTraderTools/` root contains NO `B*Tests.cs` files (only `CopyEngineTests.cs`).
4. `git status` shows 5 renames, not deletions+additions (confirms git mv preserved history).

### NT8 interaction

NT8 scans `AddOns\PropTraderTools\*.cs` (flat glob, non-recursive). Files in `Tests\` are
invisible to NT8's Roslyn host. This is the mechanism (NT8-054) that resolves R2.

### JS Rule constraints

This ticket writes no C# source code. JS-021 (lock), JS-033 (async void), JS-001 (exceptions),
JS-002 (return null) do not apply. No threading concerns.

### xUnit tests

None — this ticket performs file moves only. Tests remain inside the moved files unchanged.

### 7-Scan Checklist (T1 — Engineer Contract)

| ID | Purpose | Command | Expected Result |
|----|---------|---------|----------------|
| SCAN-01 | No `lock()` in PropTraderTools .cs files | `grep -r "lock(" src\PropTraderTools --include="*.cs"` | **0 results** (no C# written in T1) |
| SCAN-02 | No `async void` in non-test .cs files | `grep -rn "async void " src\PropTraderTools --include="*.cs"` | **0 results** (no C# written in T1) |
| SCAN-03 | No NEW `return null` in changed files | `grep -rn "return null;" src\PropTraderTools --include="*.cs"` | No new results vs baseline (no C# written in T1) |
| SCAN-04 | NT8-054: Tests\ subfolder exists and is not in NT8 deploy path | `Get-ChildItem "src\PropTraderTools" -Filter "*Tests.cs"` → only `CopyEngineTests.cs` at root; `Get-ChildItem "src\PropTraderTools\Tests" -Filter "*.cs"` → 5 files | Root has NO `B*Tests.cs`; `Tests\` subfolder has 5 files |
| SCAN-05 | dotnet build 0 errors (pre-T2 — csproj paths not yet updated, so T1 alone may produce errors; run SCAN-05 only after T2 completes) | `dotnet build src\PropTraderTools\PropTraderTools.csproj` | Defer to T2 post-condition |
| SCAN-06 | verify_links.ps1 PASS (pre-T3 — may report MISSING for moved files; run SCAN-06 only after T3 completes) | `powershell -File scripts\verify_links.ps1` | Defer to T3 post-condition |
| SCAN-07 | Tests\ subfolder populated | `Get-ChildItem "src\PropTraderTools\Tests" -Filter "*.cs"` | **5 files** listed (B42Tests.cs through B46Tests.cs) |

---

## Ticket T2 — Update `PropTraderTools.csproj`

### Identity
- **Ticket ID**: T2
- **Title**: Update csproj — fix 4 path entries + add missing B46Tests.cs
- **Spec Reqs**: R4, R6
- **Workspace**: Wave (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj`)
- **Pre-condition**: T1 complete (Tests\ directory and moved files exist)

### Pre-condition
`PropTraderTools.csproj` currently has (verbatim, confirmed live):
```xml
<!-- B42: PTTFollowerStrategy + tests -->
<Compile Include="Features\PttFollowerStrategy.cs" />
<Compile Include="B42Tests.cs" />
<!-- B43: ATM template ComboBox tests -->
<Compile Include="B43Tests.cs" />
<!-- B44: Subscribe/Unsubscribe idempotency tests -->
<Compile Include="B44Tests.cs" />
<!-- B45: OnApplyRule late-resolve + PttFollowerStrategy StartBehavior tests -->
<Compile Include="B45Tests.cs" />
```
`B46Tests.cs` is ABSENT from the csproj entirely (known bug from B46 delivery).

### Work: Exact Changes to `PropTraderTools.csproj`

**Change 1** — Update the B42 section comment and path:

```xml
<!-- BEFORE -->
<!-- B42: PTTFollowerStrategy + tests -->
<Compile Include="Features\PttFollowerStrategy.cs" />
<Compile Include="B42Tests.cs" />

<!-- AFTER -->
<!-- B42: PTTFollowerStrategy (src) + B42Tests (Tests\) -->
<Compile Include="Features\PttFollowerStrategy.cs" />
<Compile Include="Tests\B42Tests.cs" />
```

**Change 2** — Update B43 path:

```xml
<!-- BEFORE -->
<Compile Include="B43Tests.cs" />

<!-- AFTER -->
<Compile Include="Tests\B43Tests.cs" />
```

**Change 3** — Update B44 path:

```xml
<!-- BEFORE -->
<Compile Include="B44Tests.cs" />

<!-- AFTER -->
<Compile Include="Tests\B44Tests.cs" />
```

**Change 4** — Update B45 path:

```xml
<!-- BEFORE -->
<Compile Include="B45Tests.cs" />

<!-- AFTER -->
<Compile Include="Tests\B45Tests.cs" />
```

**Change 5** — Add missing B46Tests.cs entry (immediately after the B45 line):

```xml
<!-- ADD after Tests\B45Tests.cs line -->
<!-- B46: ATM empty guard + combo auto-select tests -->
<Compile Include="Tests\B46Tests.cs" />
```

**Full resulting test `<Compile>` block** (engineer must match this exactly):

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

### Post-condition (verify before closing T2)

1. `Select-String -Path "src\PropTraderTools\PropTraderTools.csproj" -Pattern '"B4[2-9]Tests\.cs"'` → **0 matches** (no root-level paths remain).
2. `Select-String -Path "src\PropTraderTools\PropTraderTools.csproj" -Pattern 'Tests\\B4[2-9]Tests'` → **5 matches** (B42, B43, B44, B45, B46).
3. `dotnet build src\PropTraderTools\PropTraderTools.csproj` → **exit code 0**, 0 errors.

### JS Rule constraints

csproj is XML — no C# source written. JS rules do not apply.

### xUnit tests

None — this ticket edits XML only. Verification of correctness is `dotnet build` exit 0.

### 7-Scan Checklist (T2 — Engineer Contract)

| ID | Purpose | Command | Expected Result |
|----|---------|---------|----------------|
| SCAN-01 | No `lock()` in PropTraderTools .cs files | `grep -r "lock(" src\PropTraderTools --include="*.cs"` | **0 results** (no C# written in T2) |
| SCAN-02 | No `async void` in non-test .cs files | `grep -rn "async void " src\PropTraderTools --include="*.cs"` | **0 results** (no C# written in T2) |
| SCAN-03 | No NEW `return null` in changed files | `grep -rn "return null;" src\PropTraderTools --include="*.cs"` | No new results vs baseline (no C# written in T2) |
| SCAN-04 | NT8-054: csproj has `Tests\` paths and NO root-level `B*Tests.cs` paths | `Select-String -Path "src\PropTraderTools\PropTraderTools.csproj" -Pattern '"B4[2-9]Tests\.cs"'` → 0 matches; `Select-String -Path "src\PropTraderTools\PropTraderTools.csproj" -Pattern 'Tests\\B4[2-9]Tests'` → 5 matches | 0 root paths, 5 `Tests\` paths |
| SCAN-05 | dotnet build 0 errors | `dotnet build src\PropTraderTools\PropTraderTools.csproj` | **exit code 0**, 0 errors |
| SCAN-06 | verify_links.ps1 PASS (pre-T3 — may report MISSING; run SCAN-06 only after T3 completes) | `powershell -File scripts\verify_links.ps1` | Defer to T3 post-condition |
| SCAN-07 | Tests\ subfolder still populated (T1 confirmed, unchanged by T2) | `Get-ChildItem "src\PropTraderTools\Tests" -Filter "*.cs"` | **5 files** listed |

---

## Ticket T3 — Update `scripts/verify_links.ps1`

### Identity
- **Ticket ID**: T3
- **Title**: Update verify_links.ps1 — add Tests\ directory skip + B46Tests.cs to DeployExcludes
- **Spec Reqs**: R5, R7
- **Workspace**: Wave (`c:\WSGTA\universal-or-strategy\scripts\verify_links.ps1`)
- **Pre-condition**: T1 complete (Tests\ directory exists — the skip logic only fires when files are found there)

### Pre-condition
`verify_links.ps1` line 9 currently reads (confirmed live):

```powershell
$DeployExcludes = @("CopyEngineTests.cs", "B42Tests.cs", "B43Tests.cs", "B44Tests.cs", "B45Tests.cs")
```

The `ForEach-Object` body currently begins with:

```powershell
$displayName = $relPath

# Skip non-deployable files (test files etc.)
if ($DeployExcludes -contains $_.Name) {
```

There is NO directory-based skip block. `B46Tests.cs` is NOT in `$DeployExcludes`.

### Work: Exact Changes to `verify_links.ps1`

**Change 1** — Add `B46Tests.cs` to `$DeployExcludes` (defense-in-depth):

```powershell
# BEFORE (line 9)
$DeployExcludes = @("CopyEngineTests.cs", "B42Tests.cs", "B43Tests.cs", "B44Tests.cs", "B45Tests.cs")

# AFTER
$DeployExcludes = @("CopyEngineTests.cs", "B42Tests.cs", "B43Tests.cs", "B44Tests.cs", "B45Tests.cs", "B46Tests.cs")
```

**Change 2** — Insert directory-based primary skip block AFTER the `$displayName = $relPath` line
and BEFORE the existing `if ($DeployExcludes -contains $_.Name)` check:

```powershell
# BEFORE
    $displayName = $relPath

    # Skip non-deployable files (test files etc.)
    if ($DeployExcludes -contains $_.Name) {

# AFTER
    $displayName = $relPath

    # Layer 1: Skip entire Tests\ subfolder (xUnit files -- never deployed to NT8, NT8 flat scan only)
    if ($_.FullName -match '\\Tests\\') {
        Write-Host "SKIP     : $displayName  (Tests subfolder -- not deployed to NT8)" -ForegroundColor DarkGray
        $skipped++
        return
    }

    # Layer 2: Skip individual named test files at root (defense-in-depth)
    if ($DeployExcludes -contains $_.Name) {
```

**Complete resulting exclusion block** (engineer must match this exactly):

```powershell
    $displayName = $relPath

    # Layer 1: Skip entire Tests\ subfolder (xUnit files -- never deployed to NT8, NT8 flat scan only)
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

### Post-condition (verify before closing T3)

1. `Select-String -Path "scripts\verify_links.ps1" -Pattern '\\\\Tests\\\\'` → **>=1 match**.
2. `Select-String -Path "scripts\verify_links.ps1" -Pattern '"B46Tests\.cs"'` → **>=1 match**.
3. `powershell -File scripts\verify_links.ps1` → exits with **PASS** line, exit code 0.
4. The SKIP counter in the summary output reflects the 5 moved test files (they are found in `Tests\` and skipped by Layer 1).
5. No NT8 bin path contains any `B*Tests.cs` file after the run.

### JS Rule constraints

PowerShell only — no C# written. SCAN-06 mandates zero `lock\s*\(` matches in this file.
The inserted block contains no lock(). ASCII-only strings used throughout.

### xUnit tests

None — this ticket edits a PowerShell deploy script. Correctness verified by SCAN-06 and
SCAN-07 (verify_links.ps1 PASS).

### 7-Scan Checklist (T3 — Engineer Contract)

| ID | Purpose | Command | Expected Result |
|----|---------|---------|----------------|
| SCAN-01 | No `lock()` in PropTraderTools .cs files | `grep -r "lock(" src\PropTraderTools --include="*.cs"` | **0 results** (no C# written in T3) |
| SCAN-02 | No `async void` in non-test .cs files | `grep -rn "async void " src\PropTraderTools --include="*.cs"` | **0 results** (no C# written in T3) |
| SCAN-03 | No NEW `return null` in changed files | `grep -rn "return null;" src\PropTraderTools --include="*.cs"` | No new results vs baseline (no C# written in T3) |
| SCAN-04 | NT8-054: verify_links.ps1 has directory skip and B46Tests.cs in DeployExcludes | (a) `Select-String -Path "scripts\verify_links.ps1" -Pattern '\\\\Tests\\\\'` → >=1 match; (b) `Select-String -Path "scripts\verify_links.ps1" -Pattern '"B46Tests\.cs"'` → >=1 match; (c) `powershell -File scripts\verify_links.ps1` → PASS, no MISSING for B4[2-6]Tests.cs | All three sub-checks PASS |
| SCAN-05 | dotnet build 0 errors (csproj updated in T2, confirm still green) | `dotnet build src\PropTraderTools\PropTraderTools.csproj` | **exit code 0**, 0 errors |
| SCAN-06 | verify_links.ps1 PASS | `powershell -File scripts\verify_links.ps1` | **PASS** line printed, exit code 0 |
| SCAN-07 | Tests\ subfolder populated (T1 confirmed, unchanged by T3) | `Get-ChildItem "src\PropTraderTools\Tests" -Filter "*.cs"` | **5 files** listed |

---

## Ticket T4 — Append B48 Knowledge Block to `NT8_ADDON_KNOWLEDGE.md`

### Identity
- **Ticket ID**: T4
- **Title**: Append B48 section to `NT8_ADDON_KNOWLEDGE.md` documenting `Tests\` subfolder convention
- **Spec Reqs**: R1 (documentation), R5 (convention for future blocks)
- **Workspace**: Director (`c:\WSGTA\universal-or-strategy-director\docs\standards\NT8_ADDON_KNOWLEDGE.md`)
- **Pre-condition**: T1, T2, T3 all complete and verified (document the finished state, not an in-progress state)

### Pre-condition
`docs/standards/NT8_ADDON_KNOWLEDGE.md` exists in the Director workspace.
The file currently ends after the B47 section (or the most recent block section appended).
There is NO `## B48` section yet.

### Work: Content to Append

Append the following block verbatim to the END of `docs/standards/NT8_ADDON_KNOWLEDGE.md`:

```markdown
---

## B48 Discoveries (2026-08-07)

### NT8-054 Enforcement — Tests\ Subfolder Pattern (ESTABLISHED B48)

**Problem**: xUnit test files placed at the flat root of `src/PropTraderTools/` are compiled
by NT8's Roslyn host, which has no xUnit NuGet packages in scope. This produces CS0246 and
CS0103 errors at F5, blocking AddOn load. See NT8-054 in NT8_COMPILER_RULES.md.

**Solution**: All B*Tests.cs files (except CopyEngineTests.cs -- see below) live in the
`src/PropTraderTools/Tests/` subfolder. NT8 scans `AddOns\PropTraderTools\*.cs` using a
flat (non-recursive) glob. Files in `Tests\` are invisible to NT8.

### Convention for Future Blocks (B49+)

Any new block test file (e.g., B49Tests.cs) MUST:
1. Be created at `src/PropTraderTools/Tests/B49Tests.cs` (NOT at the flat root).
2. Have a `<Compile Include="Tests\B49Tests.cs" />` entry added to `PropTraderTools.csproj`.
3. NOT be added to `$DeployExcludes` in `verify_links.ps1` -- the directory-based Layer 1
   skip covers all files in `Tests\` automatically.

### CopyEngineTests.cs -- Permanent Root Exception

`CopyEngineTests.cs` MUST remain at `src/PropTraderTools/CopyEngineTests.cs` permanently.
It accesses `CopyEngine`'s `private readonly struct CopyRule` nested type directly.
This type is only accessible when both files compile in the same assembly.
A separate test project would fail with CS0246 on the private nested type.
`CopyEngineTests.cs` is excluded from NT8 deployment via `$DeployExcludes` in verify_links.ps1.
Do NOT move it to `Tests\`.

### Two-Layer Exclusion in verify_links.ps1

`scripts/verify_links.ps1` uses a two-layer defense to prevent test files reaching NT8:

**Layer 1** (directory-based, primary, future-proof):
```powershell
if ($_.FullName -match '\\Tests\\') {
    Write-Host "SKIP     : $displayName  (Tests subfolder -- not deployed to NT8)" -ForegroundColor DarkGray
    $skipped++
    return
}
```
This fires for ANY file under `Tests\`, regardless of filename. New block test files
(B49Tests.cs, B50Tests.cs, ...) are automatically excluded without any change to the script.

**Layer 2** (filename-based, defense-in-depth, catches accidental root placement):
```powershell
$DeployExcludes = @("CopyEngineTests.cs", "B42Tests.cs", "B43Tests.cs", "B44Tests.cs",
                    "B45Tests.cs", "B46Tests.cs")
if ($DeployExcludes -contains $_.Name) { ... $skipped++; return }
```
Protects against any file being accidentally relocated back to the flat root.

### B48 Files Changed

| File | Change Type | Location |
|------|-------------|----------|
| `src/PropTraderTools/Tests/B42Tests.cs` | MOVED (git mv from root) | Wave workspace |
| `src/PropTraderTools/Tests/B43Tests.cs` | MOVED (git mv from root) | Wave workspace |
| `src/PropTraderTools/Tests/B44Tests.cs` | MOVED (git mv from root) | Wave workspace |
| `src/PropTraderTools/Tests/B45Tests.cs` | MOVED (git mv from root) | Wave workspace |
| `src/PropTraderTools/Tests/B46Tests.cs` | MOVED (git mv from root) | Wave workspace |
| `PropTraderTools.csproj` | EDITED (4 path updates + 1 add for B46) | Wave workspace |
| `scripts/verify_links.ps1` | EDITED (Layer 1 block + B46 to DeployExcludes) | Wave workspace |
| `CopyEngineTests.cs` | NO CHANGE (stays at root) | Wave workspace |
| `NT8_COMPILER_RULES.md` | NO CHANGE (NT8-054 already accurate) | Director workspace |

### Deferred Items from B48

| DW ID | Description | Owner |
|-------|-------------|-------|
| DW-B44-01 (sub-item 2) | CopyEngineTests.cs 60 compile errors prevent dotnet test | Future block |
| DW-B47-01 | B47Tests.cs creation + csproj entry | Lane C |
| Future | Add `<Compile Include="Tests\B47Tests.cs" />` when B47Tests.cs is delivered | Whoever delivers B47Tests.cs |
```

### Post-condition (verify before closing T4)

1. `Select-String -Path "docs\standards\NT8_ADDON_KNOWLEDGE.md" -Pattern "^## B48"` → **1 match**.
2. File is UTF-8 no-BOM (no wide-character encoding introduced).
3. The appended content is complete and syntactically correct Markdown.

### JS Rule constraints

Markdown documentation — no C# written. No JS rules apply.

### xUnit tests

None — this ticket appends documentation only.

### 7-Scan Checklist (T4 — Engineer Contract)

| ID | Purpose | Command | Expected Result |
|----|---------|---------|----------------|
| SCAN-01 | No `lock()` in PropTraderTools .cs files | `grep -r "lock(" src\PropTraderTools --include="*.cs"` | **0 results** (no C# written in T4) |
| SCAN-02 | No `async void` in non-test .cs files | `grep -rn "async void " src\PropTraderTools --include="*.cs"` | **0 results** (no C# written in T4) |
| SCAN-03 | No NEW `return null` in changed files | `grep -rn "return null;" src\PropTraderTools --include="*.cs"` | No new results vs baseline (no C# written in T4) |
| SCAN-04 | NT8-054: NT8_ADDON_KNOWLEDGE.md has B48 section (confirms Tests\ convention documented) | `Select-String -Path "docs\standards\NT8_ADDON_KNOWLEDGE.md" -Pattern "^## B48"` | **1 match** |
| SCAN-05 | dotnet build 0 errors (T1+T2+T3 already verified; T4 touches no .cs/.csproj) | `dotnet build src\PropTraderTools\PropTraderTools.csproj` | **exit code 0**, 0 errors |
| SCAN-06 | verify_links.ps1 PASS (T3 verified; T4 touches no .ps1 files) | `powershell -File scripts\verify_links.ps1` | **PASS** line printed, exit code 0 |
| SCAN-07 | Tests\ subfolder populated (T1 confirmed, unchanged by T4) | `Get-ChildItem "src\PropTraderTools\Tests" -Filter "*.cs"` | **5 files** listed |

---

## Global 7-Scan Checklist (All Tickets — Engineer Must Run After T1+T2+T3+T4 Complete)

The ptt-verifier runs ALL seven scans from the Wave workspace root
(`c:\WSGTA\universal-or-strategy`) before declaring the block green.

| ID | Purpose | Command | Expected Result |
|----|---------|---------|----------------|
| SCAN-01 | No `lock()` in PropTraderTools .cs files | `grep -r "lock(" c:\WSGTA\universal-or-strategy\src\PropTraderTools --include="*.cs"` | **0 results** |
| SCAN-02 | No `async void` in non-test .cs files | `grep -rn "async void " c:\WSGTA\universal-or-strategy\src\PropTraderTools --include="*.cs"` | **0 results** (existing DW-B47-05 only if any) |
| SCAN-03 | No NEW `return null` in changed files | `grep -rn "return null;" c:\WSGTA\universal-or-strategy\src\PropTraderTools --include="*.cs"` | No new results vs baseline (pre-existing only) |
| SCAN-04 | NT8-054: B42–B46 test files NOT in NT8 deploy path | (a) `Select-String -Path "scripts\verify_links.ps1" -Pattern '\\\\Tests\\\\'` → >=1 match; (b) `Select-String -Path "scripts\verify_links.ps1" -Pattern '"B46Tests\.cs"'` → >=1 match; (c) `powershell -File scripts\verify_links.ps1` → PASS, no MISSING for B4[2-6]Tests.cs | All three sub-checks PASS |
| SCAN-05 | dotnet build 0 errors | `dotnet build c:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj` | **exit code 0**, 0 errors |
| SCAN-06 | verify_links.ps1 PASS | `powershell -File c:\WSGTA\universal-or-strategy\scripts\verify_links.ps1` | **PASS** line printed, exit code 0 |
| SCAN-07 | Tests\ subfolder populated | `Get-ChildItem "c:\WSGTA\universal-or-strategy\src\PropTraderTools\Tests" -Filter "*.cs"` | **5 files** listed (B42Tests.cs through B46Tests.cs) |

---

## Acceptance Criteria Cross-Reference

| AC | Criterion | Verified by |
|----|-----------|------------|
| AC-01 | F5 in NinjaTrader produces zero errors | Manual F5 after verify_links.ps1 -Fix |
| AC-02 | No `B*Tests.cs` at root of `src/PropTraderTools/` | `Get-ChildItem src\PropTraderTools -Filter "*Tests.cs"` → only `CopyEngineTests.cs` |
| AC-03 | All 5 test files present in `Tests\` subfolder | SCAN-07 |
| AC-04 | csproj has `Tests\B46Tests.cs` entry | `Select-String -Path "*.csproj" -Pattern "Tests\\\\B46Tests"` → 1 match |
| AC-05 | csproj has no root-level `B*Tests.cs` entry | `Select-String -Path "*.csproj" -Pattern '"B4[2-9]Tests.cs"'` → 0 matches |
| AC-06 | verify_links.ps1 PASS | SCAN-06 |
| AC-07 | verify_links.ps1 contains `\\Tests\\` skip | SCAN-04(a) |
| AC-08 | verify_links.ps1 contains B46Tests.cs | SCAN-04(b) |
| AC-09 | dotnet build passes | SCAN-05 |
| AC-10 | NT8_ADDON_KNOWLEDGE.md has B48 section | T4 post-condition check |

---

## Ticket Execution Order

```
T1 (git mv 5 files)
  └── T2 (csproj path updates)     ← depends on T1 (Tests\ must exist for dotnet build)
  └── T3 (verify_links.ps1 edits)  ← depends on T1 (Tests\ must exist for PASS result)
       └── T4 (NT8_ADDON_KNOWLEDGE.md append)  ← depends on T1+T2+T3 all verified green
```

T2 and T3 may be executed in parallel after T1. T4 must be last.

---

**TICKETS_COMPLETE**
