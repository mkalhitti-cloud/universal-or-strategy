# BWAVE-DW Lane C — Architecture Plan

**Epic**: BWAVE-DW LaneC (Test Quality + StyleCop + ASCII Comments)
**Brain Dir**: `docs/brain/BWAVE-DW/LaneC/`
**Phase**: 2 — Architecture Plan
**Author**: ptt-architect
**Status**: PLAN_COMPLETE
**Date**: 2026-09-04

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

**Gate questions answered**:

| Question | Answer | Evidence |
|----------|--------|----------|
| Q1. Same method or within 50 lines? | NO | 7 tickets span 8 distinct test files |
| Q2. Fix B design depends on Fix A final design? | NO | No inter-ticket design dependency |
| Q3. Each fix has standalone value if the other is blocked? | YES | Each ticket is self-contained |
| Q4. Each fix has an independent SIM verification path? | YES | `dotnet test --filter` per ticket |

**Decision**: Default applies (Q1=NO, Q2=NO). All 7 tickets execute as a SINGLE-PIPELINE in the
prescribed order. No lane split.

---

## Scope Declaration

**TEST-ONLY EPIC. Zero production code is modified.**

All 8 files in scope are test files:

| File | Ticket(s) |
|------|-----------|
| `src/PropTraderTools/CopyEngineTests.cs` | C-1, C-2 |
| `src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | C-1 |
| `src/PropTraderTools/Tests/B46Tests.cs` | C-2 |
| `src/PropTraderTools/Tests/B47Tests.cs` | C-2 |
| `src/PropTraderTools/Tests/BwaveCycLaneBTests.cs` | C-3, C-4 |
| `src/PropTraderTools/B76Tests.cs` | C-5 |
| `src/PropTraderTools/TradeCopierPanelB77Tests.cs` | C-6 |
| `src/PropTraderTools/TradeCopierPanelB75Tests.cs` | C-7 |

**NOTE**: `B76Tests.cs`, `TradeCopierPanelB75Tests.cs`, and `TradeCopierPanelB77Tests.cs` are at
the **ROOT** of `src/PropTraderTools/`, NOT in a `Tests/` subdirectory. All file path references
for these 3 files must omit the `Tests/` prefix.

**No F5 required** (see NT8 Sync Exclusion section below).

---

## Deferred Items Closed by This Plan

| DW Item | Status Before | Ticket |
|---------|--------------|--------|
| DW-LaneA-01 | OPEN | C-1 |
| DW-LaneA-02 | OPEN | C-1 |
| DW-LaneA-03 | OPEN | C-1 |
| DW-LaneA-04 | OPEN | C-2 |
| DW-LaneA-05 | OPEN | C-1 |
| DW-B37-01 | OPEN | C-4 |
| DW-B37-02 | OPEN | C-3 |
| DW-B37-03 | OPEN | C-4 |
| DW-B37-04 | OPEN | C-3 |
| DW-B37-05 | OPEN | C-4 |
| DW-B37-06 | OPEN | C-3 |
| DW-B37-07 | OPEN | C-3 |
| DW-B37-08 | OPEN | C-3 |
| DW-C39-11 | OPEN | C-5 |
| DW-C39-12 | OPEN | C-5 |
| DW-C39-13 | OPEN | C-6 |
| DW-C39-14 | OPEN | C-6 |
| DW-C39-15 | OPEN | C-7 |

---

## Execution Order and Rationale

```
C-1  (CSharpier format) → C-2 (ASCII fix) → C-3 (renames) → C-4 (hardening)
  → C-5 (B76 IL) → C-6 (B77 opcodes) → C-7 (B75 teardown)
```

**Order rationale**:
1. **C-1 first**: `dotnet csharpier format src/` establishes a clean formatting baseline across all
   test files. Running it first ensures no subsequent edit collides with a pre-existing whitespace
   violation that CSharpier would later flag. CSharpier does not modify comment content, so it is
   safe to run before the ASCII fix.
2. **C-2 second**: ASCII replacement is comment-content only. After CSharpier has run, the files
   are properly formatted. The U+2500 → `-` substitution is safe on the clean baseline.
3. **C-3 before C-4**: Both touch `BwaveCycLaneBTests.cs`. C-3 renames 5 test methods; C-4 hardens
   3 different test methods. Renaming first gives the engineer a stable method inventory before
   adding hardening logic.
4. **C-5, C-6, C-7**: Independent root-level files. No dependency between them. Sequential order
   chosen for predictability.

---

## TICKET C-1: SA1507/SA1508 StyleCop Cleanup

**Closes**: DW-LaneA-01, DW-LaneA-02, DW-LaneA-03, DW-LaneA-05

**Files**:
- `src/PropTraderTools/CopyEngineTests.cs` (lines 6843, 6920, 6921)
- `src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` (line 566)

**What to change**:
Run CSharpier format to remove:
- SA1507 — two or more consecutive blank lines (lines 6843, 6920 in CopyEngineTests.cs; line 566
  in BwaveCycLaneCTests.cs).
- SA1508 — closing brace preceded by blank line (line 6921 in CopyEngineTests.cs).

CSharpier tool invocation (choose one — both are equivalent):
```
dotnet csharpier format src/
```
OR (if global tool not found by name):
```
dotnet tool run csharpier format src/
```

Post-format verification:
```
dotnet csharpier check src/
```
Expected: exit code 0, zero violations reported for named files.

**Acceptance criteria**:
- `dotnet csharpier check src/` exits 0.
- Zero SA1507/SA1508 violations in `CopyEngineTests.cs` and `BwaveCycLaneCTests.cs`.
- No logic change in either file (diff is whitespace-only).

**JS Rules applied**:
- AGENTS.md §2 Platinum Standard (CYC <= 8 mandate): CYC unchanged — no new methods added.
- AGENTS.md §2 + Section 10 (CSharpier mandate): Source files meet CSharpier formatting standard.

### SCAN-01..07 for C-1

| Scan | Check | Command | Expected |
|------|-------|---------|----------|
| SCAN-01 | No lock() | `grep -n "lock(" src/PropTraderTools/CopyEngineTests.cs` | 0 results |
| SCAN-02 | No throw new | `grep -n "throw new" src/PropTraderTools/CopyEngineTests.cs` | 0 new throws |
| SCAN-03 | No return null | `grep -n "return null" src/PropTraderTools/CopyEngineTests.cs` | 0 new nulls |
| SCAN-04 | No async void | `grep -n "async void" src/PropTraderTools/CopyEngineTests.cs` | 0 results |
| SCAN-05 | CYC <= 8 | No new methods added; CYC unchanged | PASS |
| SCAN-06 | ASCII-only | Diff is whitespace-only; no new non-ASCII introduced | PASS |
| SCAN-07 | dotnet test | `dotnet test --filter "FullyQualifiedName~BwaveCycTaR6HelperTests"` | All pass |

---

## TICKET C-2: ASCII Compliance — U+2500 Box-Drawing in Comments

**Closes**: DW-LaneA-04

**Files**:
- `src/PropTraderTools/CopyEngineTests.cs` (line 5787 and surrounding area)
- `src/PropTraderTools/Tests/B46Tests.cs`
- `src/PropTraderTools/Tests/B47Tests.cs`

**What to change**:
Replace all occurrences of `U+2500` (HORIZONTAL SCAN LINE `─`, UTF-8 bytes `0xE2 0x94 0x80`) with
ASCII dash `-`. These characters appear exclusively in comment section-header separators, e.g.:
```
// ─────────────────────────────────────────────────────
```
becomes:
```
// -------------------------------------------------------
```

**Comment-only constraint**: No logic changes. No string literal changes. Only comment lines are
affected. The engineer MUST verify the replacement does not touch any string literal or code token.

Recommended approach — PowerShell targeted replace per file:
```powershell
$files = @(
    "src/PropTraderTools/CopyEngineTests.cs",
    "src/PropTraderTools/Tests/B46Tests.cs",
    "src/PropTraderTools/Tests/B47Tests.cs"
)
foreach ($f in $files) {
    $text = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
    $fixed = $text.Replace([char]0x2500, '-')
    [System.IO.File]::WriteAllText($f, $fixed, (New-Object System.Text.UTF8Encoding $false))
}
```

**Note**: U+2500 may appear as multi-byte UTF-8 sequence (E2 94 80). The PowerShell `[char]0x2500`
correctly matches the Unicode code point. Verify no false positives occur in string literals before
committing.

**Acceptance criteria**:
- Zero bytes with value > 127 remain in the 3 named files.
- PowerShell byte scan returns 0: `([System.IO.File]::ReadAllBytes($f) | Where-Object { $_ -gt 127 }).Count -eq 0`
- All comments that previously contained `─` now contain `-`.
- dotnet test passes for all 3 file test classes.

**JS Rules applied**:
- AGENTS.md §2 ASCII-Only Compliance: All source bytes must be ASCII.
- No JS numeric rules violated (comment-only change).

### SCAN-01..07 for C-2

| Scan | Check | Command | Expected |
|------|-------|---------|----------|
| SCAN-01 | No lock() | `grep -n "lock("` in 3 files | 0 results |
| SCAN-02 | No throw new | `grep -n "throw new"` in 3 files | 0 new throws |
| SCAN-03 | No return null | `grep -n "return null"` in 3 files | 0 new nulls |
| SCAN-04 | No async void | `grep -n "async void"` in 3 files | 0 results |
| SCAN-05 | CYC <= 8 | No new methods; CYC unchanged | PASS |
| SCAN-06 | ASCII-only | `([System.IO.File]::ReadAllBytes($f) | Where-Object { $_ -gt 127 }).Count -eq 0` | 0 for all 3 files |
| SCAN-07 | dotnet test | `dotnet test --filter "FullyQualifiedName~B46Tests|FullyQualifiedName~B47Tests"` | All pass |

---

## TICKET C-3: Test Name Inversions — 5 Renames

**Closes**: DW-B37-02, DW-B37-04, DW-B37-06, DW-B37-07, DW-B37-08

**File**: `src/PropTraderTools/Tests/BwaveCycLaneBTests.cs`

**What to change**:
Pure method renames — no assertion changes, no body modifications. The `[Fact]` attribute remains.
Five method names are inverted (Assert body says "returns true when X" but name says "false"):

| Line | Old Name (incorrect) | New Name (correct) |
|------|---------------------|--------------------|
| 433 | *(inverted — see DW-B37-02)* | `IsPttBeRetryTriggerOrder_ReturnsTrue_WhenNameIsPttQxT` |
| 546 | *(inverted — see DW-B37-04)* | `IsNativeExitName_ReturnsFalse_WhenNameIsTarget` |
| 707 | *(inverted — see DW-B37-06)* | `ResolveMultipliers_ReturnsNull_WhenMultipliersNull` |
| 723 | *(inverted — see DW-B37-07)* | `SelectRefPriceByDirection_ReturnsAsk_WhenLong` |
| 752 | *(inverted — see DW-B37-08)* | `SelectRefPriceByDirection_ReturnsBid_WhenShort` |

**Engineer instruction**: Read the actual `Assert.*` statements in each method body before renaming.
Confirm the new name matches what the assertion verifies. Do NOT change the assertion — rename only.

**Acceptance criteria**:
- All 5 new method names are present in the file.
- The old (inverted) method names are absent.
- Method bodies are byte-for-byte identical to pre-rename (assertions unchanged).
- dotnet test runs all 5 renamed tests and they pass.

**JS Rules applied**:
- AGENTS.md §2 Platinum Standard (xUnit-only test framework mandate): `[Fact]` attribute retained on all 5 renamed methods; xUnit standard preserved.
- AGENTS.md §2 Platinum Standard (CYC <= 8 mandate): CYC unchanged (rename does not alter branching).

### SCAN-01..07 for C-3

| Scan | Check | Command | Expected |
|------|-------|---------|----------|
| SCAN-01 | No lock() | `grep -n "lock(" Tests/BwaveCycLaneBTests.cs` | 0 results |
| SCAN-02 | No throw new | `grep -n "throw new" Tests/BwaveCycLaneBTests.cs` | 0 new throws |
| SCAN-03 | No return null | `grep -n "return null" Tests/BwaveCycLaneBTests.cs` | 0 new nulls |
| SCAN-04 | No async void | `grep -n "async void" Tests/BwaveCycLaneBTests.cs` | 0 results |
| SCAN-05 | CYC <= 8 | No new methods; rename only; CYC unchanged | PASS |
| SCAN-06 | ASCII-only | No new non-ASCII in rename edits | PASS |
| SCAN-07 | dotnet test | `dotnet test --filter "FullyQualifiedName~BwaveCycLaneBTests"` | All 5 renamed tests pass |

---

## TICKET C-4: Test Hardening — 3 Missing Execution Paths

**Closes**: DW-B37-01, DW-B37-03, DW-B37-05

**File**: `src/PropTraderTools/Tests/BwaveCycLaneBTests.cs`

**What to change**:
Three test methods (DW-B37-01, 03, 05) were flagged as missing execution-path coverage. Each
method either (a) tests a code path that requires NT8 host infrastructure, or (b) lacks an
assertion for a branch that exists in the production method.

**Decision tree per method**:
1. If the missing path requires NT8 host objects (e.g., `NinjaTrader.Cbi.Account`, live strategy
   context): Add `[Fact(Skip = "NT8-HOST-REQUIRED: <one-sentence reason>")]` to the method.
2. If the missing path is pure logic (no NT8 dependency): Add the missing assertion covering the
   branch. Keep CYC of the test method <= 4 (test methods must be simple and deterministic).

**Skip attribute format**:
```csharp
[Fact(Skip = "NT8-HOST-REQUIRED: requires live Account object from NinjaTrader runtime")]
```

**No new lock(), no new throw, no new return null.**

**Acceptance criteria**:
- DW-B37-01, 03, 05 methods each have either a `[Fact(Skip=...)]` attribute with documented reason
  OR a new assertion covering the previously missing execution path.
- If Skip: skip message is human-readable and references NT8 host dependency.
- If expanded: new assertion is deterministic and does not depend on NT8 runtime.
- dotnet test reports the skipped methods as `Skipped` (not `Failed`).

**JS Rules applied**:
- AGENTS.md §2 Platinum Standard (xUnit-only test framework mandate): `[Fact]` or `[Fact(Skip=...)]` only. No NUnit/MSTest skip equivalents.
- AGENTS.md §2 Platinum Standard (CYC <= 8 mandate): CYC of any expanded test method <= 4.
- JS-001 (Result<T,E>): No new exception throws in test helpers.

### SCAN-01..07 for C-4

| Scan | Check | Command | Expected |
|------|-------|---------|----------|
| SCAN-01 | No lock() | `grep -n "lock(" Tests/BwaveCycLaneBTests.cs` | 0 results |
| SCAN-02 | No throw new | `grep -n "throw new" Tests/BwaveCycLaneBTests.cs` | 0 new throws |
| SCAN-03 | No return null | `grep -n "return null" Tests/BwaveCycLaneBTests.cs` | 0 new nulls |
| SCAN-04 | No async void | `grep -n "async void" Tests/BwaveCycLaneBTests.cs` | 0 results |
| SCAN-05 | CYC <= 8 | Any expanded test method CYC <= 4 | PASS |
| SCAN-06 | ASCII-only | No new non-ASCII in added lines | PASS |
| SCAN-07 | dotnet test | `dotnet test --filter "FullyQualifiedName~BwaveCycLaneBTests"` | Pass or Skipped (no Fail) |

---

## TICKET C-5: B76Tests.cs — IL-Scanning Fixes

**Closes**: DW-C39-11, DW-C39-12

**File**: `src/PropTraderTools/B76Tests.cs` (ROOT level — NOT in `Tests/`)

**What to change**:

### DW-C39-11: MetadataToken cross-assembly issue for Interlocked.Exchange

**Problem**: The test resolves `Interlocked.Exchange` via `MetadataToken` which is not stable
across assembly boundaries (the token value differs per compilation unit).

**Fix**: Replace `MetadataToken`-based resolution with direct `MethodInfo` lookup:
```csharp
// BEFORE (fragile):
var method = typeof(SomeClass).GetMembers()
    .First(m => m.MetadataToken == someToken);

// AFTER (stable):
var method = typeof(System.Threading.Interlocked)
    .GetMethod(
        "Exchange",
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
        null,
        new[] { typeof(int).MakeByRefType(), typeof(int) },
        null);
Assert.NotNull(method);
```
The engineer must inspect the actual usage site in B76Tests.cs and apply the equivalent stable
lookup matching the actual overload being tested.

### DW-C39-12: Replace fragile IL assertions with behavioral assertions

**Problem**: Tests assert on raw IL opcode sequences, which break when the JIT or compiler changes
code-gen. IL-level assertions are not behavioral: they test implementation artifact, not contract.

**Fix**: Replace IL opcode scanning loops with behavioral assertions. Call the method under test
with controlled inputs and assert on the return value or observable side-effect.

Pattern to follow:
```csharp
// BEFORE (fragile IL scan):
var opcodes = GetILOpcodes(method);
Assert.Contains(OpCodes.Ldsfld, opcodes);

// AFTER (behavioral):
var result = MethodUnderTest(inputA, inputB);
Assert.Equal(expectedValue, result);
```

If the method under test cannot be called without NT8 host context, use
`[Fact(Skip = "NT8-HOST-REQUIRED: behavioral assertion requires live NT8 runtime")]`.

**Acceptance criteria**:
- DW-C39-11: Zero `MetadataToken` comparisons in B76Tests.cs after fix.
- DW-C39-12: Zero raw IL opcode-scanning loops in B76Tests.cs after fix (or `[Fact(Skip=...)]`
  applied to tests that cannot be converted without NT8 host).
- dotnet test B76Tests passes (or Skip reported, not Fail).

**JS Rules applied**:
- AGENTS.md §2 Platinum Standard (xUnit-only test framework mandate): `[Fact]` or `[Fact(Skip=...)]`. Behavioral assertions over implementation assertions.
- AGENTS.md §2 Platinum Standard (CYC <= 8 mandate): Any new helper method CYC <= 8.
- JS-001: No new throw in helper methods.
- JS-002: No return null in helper methods.

### SCAN-01..07 for C-5

| Scan | Check | Command | Expected |
|------|-------|---------|----------|
| SCAN-01 | No lock() | `grep -n "lock(" B76Tests.cs` | 0 results |
| SCAN-02 | No throw new | `grep -n "throw new" B76Tests.cs` | 0 new throws |
| SCAN-03 | No return null | `grep -n "return null" B76Tests.cs` | 0 new nulls |
| SCAN-04 | No async void | `grep -n "async void" B76Tests.cs` | 0 results |
| SCAN-05 | CYC <= 8 | All new/modified methods <= 8 branches | PASS |
| SCAN-06 | ASCII-only | No non-ASCII in added lines | PASS |
| SCAN-07 | dotnet test | `dotnet test --filter "FullyQualifiedName~B76Tests"` | Pass or Skipped |

---

## TICKET C-6: B77Tests.cs — Opcode and Helper-Scan Fixes

**Closes**: DW-C39-13, DW-C39-14

**File**: `src/PropTraderTools/TradeCopierPanelB77Tests.cs` (ROOT level — NOT in `Tests/`)

**What to change**:

### DW-C39-13: Change ldstr scan to ldsfld for string.Empty

**Problem**: Test scans for `OpCodes.Ldstr` to detect string.Empty usage, but `string.Empty` is a
static field — the compiler emits `OpCodes.Ldsfld`, not `OpCodes.Ldstr`. The scan never matches.

**Fix**: Change the opcode being scanned from `OpCodes.Ldstr` to `OpCodes.Ldsfld` in the IL
scanning assertion. If the assertion is also checking the operand, verify the operand resolves to
`string.Empty` via the field token (`typeof(string).GetField("Empty")`).

```csharp
// BEFORE (wrong opcode):
Assert.Contains(OpCodes.Ldstr, GetOpcodes(method));

// AFTER (correct opcode for static field):
Assert.Contains(OpCodes.Ldsfld, GetOpcodes(method));
```

### DW-C39-14: Replace TryGetAtmNameFromSelector scan or use behavioral assertion

**Problem**: IL scan for `TryGetAtmNameFromSelector` is fragile (method may be inlined or renamed).

**Fix**: If `TryGetAtmNameFromSelector` is a public method accessible from test code:
```csharp
// Behavioral assertion:
var result = SomeHelper.TryGetAtmNameFromSelector(testInput, out var atmName);
Assert.True(result);
Assert.Equal("ExpectedAtmName", atmName);
```

If the method is inaccessible or requires NT8 host context, apply
`[Fact(Skip = "NT8-HOST-REQUIRED: TryGetAtmNameFromSelector requires live selector context")]`.

**Acceptance criteria**:
- DW-C39-13: The `Ldstr` opcode is replaced with `Ldsfld` in the string.Empty scan assertion.
- DW-C39-14: The `TryGetAtmNameFromSelector` scan uses a behavioral assertion OR has a documented
  `[Fact(Skip=...)]` with NT8-HOST-REQUIRED reason.
- dotnet test B77Tests passes (or Skip reported, not Fail).

**JS Rules applied**:
- AGENTS.md §2 Platinum Standard (xUnit-only test framework mandate): `[Fact]` or `[Fact(Skip=...)]`. Behavioral over IL-implementation assertions.
- AGENTS.md §2 Platinum Standard (CYC <= 8 mandate): Any new helper method CYC <= 8.
- JS-001: No new throw.
- JS-002: No return null.

### SCAN-01..07 for C-6

| Scan | Check | Command | Expected |
|------|-------|---------|----------|
| SCAN-01 | No lock() | `grep -n "lock(" TradeCopierPanelB77Tests.cs` | 0 results |
| SCAN-02 | No throw new | `grep -n "throw new" TradeCopierPanelB77Tests.cs` | 0 new throws |
| SCAN-03 | No return null | `grep -n "return null" TradeCopierPanelB77Tests.cs` | 0 new nulls |
| SCAN-04 | No async void | `grep -n "async void" TradeCopierPanelB77Tests.cs` | 0 results |
| SCAN-05 | CYC <= 8 | All new/modified methods <= 8 branches | PASS |
| SCAN-06 | ASCII-only | No non-ASCII in added lines | PASS |
| SCAN-07 | dotnet test | `dotnet test --filter "FullyQualifiedName~TradeCopierPanelB77Tests"` | Pass or Skipped |

---

## TICKET C-7: B75Tests.cs — Singleton Mutation Teardown

**Closes**: DW-C39-15

**File**: `src/PropTraderTools/TradeCopierPanelB75Tests.cs` (ROOT level — NOT in `Tests/`)

**What to change**:
The test method that mutates `CopyEngine.Instance` (or an equivalent singleton static field) must
restore the original value after the test completes, regardless of whether the assertion passes or
throws. Use `try/finally` to guarantee teardown.

**Pattern**:
```csharp
[Fact]
public void SomeTest_ThatMutatesSingleton()
{
    var original = CopyEngine.Instance;  // save
    try
    {
        CopyEngine.Instance = new TestDouble();  // mutate
        // ... assertion(s) ...
        Assert.Equal(expected, actual);
    }
    finally
    {
        CopyEngine.Instance = original;  // restore unconditionally
    }
}
```

**Constraints**:
- No lock() in the try/finally block.
- The finally block must ONLY restore the saved reference. No logic in finally.
- If `CopyEngine.Instance` is a `get`-only property (no setter), the engineer must document this
  and apply `[Fact(Skip = "DW-C39-15: CopyEngine.Instance has no setter — teardown not possible")]`.

**Acceptance criteria**:
- The mutating test method has a `try/finally` block.
- The `finally` block restores the original `CopyEngine.Instance` (or equivalent singleton).
- Running `dotnet test --filter TradeCopierPanelB75Tests` twice in sequence produces identical
  results (no singleton pollution between runs).
- No lock() in the modified method.

**JS Rules applied**:
- JS-021 (No Lock): `try/finally` teardown uses no lock.
- AGENTS.md §2 Platinum Standard (xUnit-only test framework mandate): `[Fact]` retained. Teardown pattern is xUnit-idiomatic.
- AGENTS.md §2 Platinum Standard (CYC <= 8 mandate): CYC of modified test method <= 3 (try block + assertion + finally restore).

### SCAN-01..07 for C-7

| Scan | Check | Command | Expected |
|------|-------|---------|----------|
| SCAN-01 | No lock() | `grep -n "lock(" TradeCopierPanelB75Tests.cs` | 0 results |
| SCAN-02 | No throw new | `grep -n "throw new" TradeCopierPanelB75Tests.cs` | 0 new throws |
| SCAN-03 | No return null | `grep -n "return null" TradeCopierPanelB75Tests.cs` | 0 new nulls |
| SCAN-04 | No async void | `grep -n "async void" TradeCopierPanelB75Tests.cs` | 0 results |
| SCAN-05 | CYC <= 8 | Modified test method CYC <= 3 | PASS |
| SCAN-06 | ASCII-only | No non-ASCII in added lines | PASS |
| SCAN-07 | dotnet test | `dotnet test --filter "FullyQualifiedName~TradeCopierPanelB75Tests"` twice | Identical results both runs |

---

## NT8 Sync Exclusion

**F5 IS NOT REQUIRED FOR THIS EPIC.**

Rationale:
- Zero production `.cs` files are modified.
- `ptt-sync-and-verify.ps1` copies production source to NT8 and verifies checksums. Since no
  production source changes, there is nothing to sync.
- NinjaTrader 8 compilation (F5) is only required when production files change.

**Do NOT run**: `powershell -File scripts\ptt-sync-and-verify.ps1`
**Do NOT press**: F5 in NinjaTrader 8

This exclusion is intentional and correct. Any agent that runs the sync script for this epic is
operating outside scope.

---

## Verification Strategy

All verification is via `dotnet test`. No NT8 SIM gate required.

### Per-Ticket Verification Commands

| Ticket | Filter Command | Expected Result |
|--------|---------------|----------------|
| C-1 | `dotnet csharpier check src/` | Exit 0, no violations |
| C-2 | PowerShell byte scan (see ticket) + `dotnet test --filter B46Tests` | 0 non-ASCII bytes; tests pass |
| C-3 | `dotnet test --filter "FullyQualifiedName~BwaveCycLaneBTests"` | 5 renamed tests pass |
| C-4 | `dotnet test --filter "FullyQualifiedName~BwaveCycLaneBTests"` | 3 tests pass or Skipped |
| C-5 | `dotnet test --filter "FullyQualifiedName~B76Tests"` | Pass or Skipped (no Fail) |
| C-6 | `dotnet test --filter "FullyQualifiedName~TradeCopierPanelB77Tests"` | Pass or Skipped (no Fail) |
| C-7 | `dotnet test --filter "FullyQualifiedName~TradeCopierPanelB75Tests"` (run twice) | Identical results |

### Final Verification (after all 7 tickets)

```
dotnet build src/PropTraderTools.sln
dotnet test src/PropTraderTools.sln
```

Expected: Build succeeds. All tests pass or are marked `Skipped` with `NT8-HOST-REQUIRED`.
Zero test `Failed` results.

---

## Summary

| Ticket | DW Items | File(s) | Change Type | CYC Impact | F5 Needed |
|--------|----------|---------|-------------|-----------|-----------|
| C-1 | LaneA-01/02/03/05 | CopyEngineTests.cs, BwaveCycLaneCTests.cs | Whitespace (CSharpier) | None | No |
| C-2 | LaneA-04 | CopyEngineTests.cs, B46Tests.cs, B47Tests.cs | Comment bytes | None | No |
| C-3 | B37-02/04/06/07/08 | BwaveCycLaneBTests.cs | Method renames | None | No |
| C-4 | B37-01/03/05 | BwaveCycLaneBTests.cs | Skip attrs or assertions | <= 4 | No |
| C-5 | C39-11/12 | B76Tests.cs | IL fix + behavioral | <= 8 | No |
| C-6 | C39-13/14 | TradeCopierPanelB77Tests.cs | Opcode + behavioral | <= 8 | No |
| C-7 | C39-15 | TradeCopierPanelB75Tests.cs | try/finally teardown | <= 3 | No |

**Production code modified**: NONE
**NT8 sync required**: NO
**F5 required**: NO
**Verification**: `dotnet test` only

---

*ptt-architect | BWAVE-DW LaneC | 2026-09-04*
