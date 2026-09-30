# BWAVE-DW Lane C — Engineer Tickets

**Epic**: BWAVE-DW LaneC (Test Quality + StyleCop + ASCII Comments)
**Branch**: `feature/bwave-dw-lane-c`
**Brain Dir**: `docs/brain/BWAVE-DW/LaneC/`
**Phase**: 4 — Ticket Generation
**Author**: ptt-architect
**Source Plan**: `docs/brain/BWAVE-DW/LaneC/02-architecture-plan.md` (REVIEW_PASS)
**Date**: 2026-09-04

---

## SCOPE GATE

**ZERO production code is modified in this epic.**
All 8 files in scope are test files. `ptt-sync-and-verify.ps1` is NOT run. F5 in NinjaTrader 8 is NOT required.
Verification is `dotnet test` only.

**NT8 Sync Exclusion (applies to all 7 tickets)**:
`*Tests.cs` files are excluded from `ptt-sync-and-verify.ps1` deployment. F5 in NinjaTrader is NOT required.
Run `dotnet test` instead.

---

## EXECUTION ORDER

```
C-1 → C-2 → C-3 → C-4 → C-5 → C-6 → C-7
```

Execute tickets in order. C-1 establishes a CSharpier baseline that all later edits inherit.
C-3 must complete before C-4 (both touch the same file).

---

## TICKET C-1: SA1507/SA1508 StyleCop Cleanup

**Spec Requirements**: DW-LaneA-01, DW-LaneA-02, DW-LaneA-03, DW-LaneA-05

### Files Modified

- `src/PropTraderTools/CopyEngineTests.cs`
- `src/PropTraderTools/Tests/BwaveCycLaneCTests.cs`

### Method Signatures

No new methods added. This ticket is a whitespace-only formatting pass.

### What To Do

Run CSharpier to remove consecutive blank lines (SA1507) and closing braces preceded by blank
lines (SA1508). Known violation locations from the architecture plan:

| File | Approximate Line | Violation |
|------|-----------------|-----------|
| `CopyEngineTests.cs` | 6843 | SA1507 — consecutive blank lines |
| `CopyEngineTests.cs` | 6920 | SA1507 — consecutive blank lines |
| `CopyEngineTests.cs` | 6921 | SA1508 — closing brace preceded by blank line |
| `BwaveCycLaneCTests.cs` | 566 | SA1507 — consecutive blank lines |

**Step 1 — Format**:
```
dotnet csharpier format src/
```
If the `dotnet csharpier` global alias is not found, use the local tool runner:
```
dotnet tool run csharpier format src/
```

**Step 2 — Verify**:
```
dotnet csharpier check src/
```
Expected: exit code 0, zero violations in the two named files.

**No logic changes are permitted.** The diff must be whitespace-only (blank line additions/removals).

### Rule Constraints

- **AGENTS.md §2 Platinum Standard (CSharpier mandate)**: Source files must meet CSharpier
  formatting standard. CYC is unchanged — no new methods are added.
- **AGENTS.md §2 Platinum Standard (CYC <= 8 mandate)**: Not applicable; no methods modified.
- **AGENTS.md §2 ASCII-Only Compliance**: CSharpier does not alter comment content; no new
  non-ASCII bytes are introduced.

### Expected Test Names

No new `[Fact]` tests added.

### Acceptance Criteria

1. `dotnet csharpier check src/` exits 0.
2. Zero SA1507/SA1508 violations in `CopyEngineTests.cs` and `BwaveCycLaneCTests.cs`.
3. Diff is whitespace-only (no assertion or logic changes).
4. `dotnet test --filter "FullyQualifiedName~BwaveCycLaneCTests"` — all existing tests pass.

### SCAN CHECKLIST

| Scan ID | Check | Command | Expected Result |
|---------|-------|---------|-----------------|
| SCAN-01 | No `lock()` | `grep -n "lock(" src/PropTraderTools/CopyEngineTests.cs src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | 0 results |
| SCAN-02 | No `async void` | `grep -n "async void" src/PropTraderTools/CopyEngineTests.cs src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | 0 results |
| SCAN-03 | No `return null` (new code) | `grep -n "return null" src/PropTraderTools/CopyEngineTests.cs src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | 0 new nulls introduced |
| SCAN-04 | No `throw new` (new code) | `grep -n "throw new" src/PropTraderTools/CopyEngineTests.cs src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | 0 new throws introduced |
| SCAN-05 | CYC <= 8 | No new methods added; CYC unchanged by whitespace edit | PASS |
| SCAN-06 | ASCII-only | Diff is whitespace-only; CSharpier does not alter comment bytes; no new non-ASCII introduced | PASS |
| SCAN-07 | xUnit only | `grep -n "using NUnit\|using MSTest\|\[Test\]\|\[TestMethod\]" src/PropTraderTools/CopyEngineTests.cs src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | 0 results |

---

## TICKET C-2: ASCII U+2500 in Comments

**Spec Requirements**: DW-LaneA-04

### Files Modified

- `src/PropTraderTools/CopyEngineTests.cs`
- `src/PropTraderTools/Tests/B46Tests.cs`
- `src/PropTraderTools/Tests/B47Tests.cs`

### Method Signatures

No new methods added. Comment-only change.

### What To Do

Replace all occurrences of Unicode character U+2500 (HORIZONTAL SCAN LINE `─`, UTF-8 bytes
`0xE2 0x94 0x80`) with ASCII dash `-`. These characters appear exclusively in comment
section-header separators such as:

```
// -----------------------------------------------------------
```
(was: `// ─────────────────────────────────────────────────────`)

**Step 1 — Scan (locate all occurrences before editing)**:
```powershell
Select-String -Path `
    "src\PropTraderTools\CopyEngineTests.cs", `
    "src\PropTraderTools\Tests\B46Tests.cs", `
    "src\PropTraderTools\Tests\B47Tests.cs" `
    -Pattern "\u2500" | Select-Object Path, LineNumber
```

**Step 2 — Replace per file**:
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

**Step 3 — Verify zero non-ASCII bytes remain**:
```powershell
foreach ($f in @(
    "src/PropTraderTools/CopyEngineTests.cs",
    "src/PropTraderTools/Tests/B46Tests.cs",
    "src/PropTraderTools/Tests/B47Tests.cs"
)) {
    $count = ([System.IO.File]::ReadAllBytes($f) | Where-Object { $_ -gt 127 }).Count
    Write-Host "$f : $count non-ASCII bytes"
}
```
Expected: 0 for all three files.

**Constraint**: No logic changes. No string literal changes. Only comment lines are affected.
Engineer MUST verify replacement does not touch any string literal or code token before committing.

### Rule Constraints

- **AGENTS.md §2 ASCII-Only Compliance**: All source bytes must be ASCII (<=127). This ticket
  exists to enforce that standard on these three files.
- **AGENTS.md §2 Platinum Standard (CYC <= 8 mandate)**: Not applicable; no methods modified.

### Expected Test Names

No new `[Fact]` tests added.

### Acceptance Criteria

1. Zero bytes with value > 127 remain in all 3 named files after the fix.
2. All comments that previously contained `─` now contain `-`.
3. No string literals or code tokens were altered (diff is comment-text-only).
4. `dotnet test --filter "FullyQualifiedName~B46Tests|FullyQualifiedName~B47Tests"` — all tests pass.

### SCAN CHECKLIST

| Scan ID | Check | Command | Expected Result |
|---------|-------|---------|-----------------|
| SCAN-01 | No `lock()` | `grep -n "lock("` in 3 files | 0 results |
| SCAN-02 | No `async void` | `grep -n "async void"` in 3 files | 0 results |
| SCAN-03 | No `return null` (new code) | `grep -n "return null"` in 3 files | 0 new nulls introduced |
| SCAN-04 | No `throw new` (new code) | `grep -n "throw new"` in 3 files | 0 new throws introduced |
| SCAN-05 | CYC <= 8 | No new methods added; CYC unchanged | PASS |
| SCAN-06 | ASCII-only (post-fix) | PowerShell byte scan (see Step 3 above) | 0 non-ASCII bytes in all 3 files |
| SCAN-07 | xUnit only | `grep -n "using NUnit\|using MSTest\|\[Test\]\|\[TestMethod\]"` in 3 files | 0 results |

> **SCAN-06 Note**: SCAN-06 is not waived for this ticket — instead, it is the primary acceptance
> criterion. The engineer is REMOVING non-ASCII bytes, so post-fix the byte count must be 0.

---

## TICKET C-3: Test Name Inversions — 5 Renames

**Spec Requirements**: DW-B37-02, DW-B37-04, DW-B37-06, DW-B37-07, DW-B37-08

### Files Modified

- `src/PropTraderTools/Tests/BwaveCycLaneBTests.cs`

### Method Signatures

No new methods added. Pure renames of 5 existing `[Fact]` test methods.

### What To Do

Rename the 5 methods listed below. **Read the `Assert.*` statements in each method body before
renaming. Confirm the new name matches what the assertion actually verifies. Do NOT change
any assertion — rename the method only.**

| Approx. Line | New Name (correct — matches assertion) |
|--------------|---------------------------------------|
| 433 | `IsPttBeRetryTriggerOrder_ReturnsTrue_WhenNameIsPttQxT` |
| 546 | `IsNativeExitName_ReturnsFalse_WhenNameIsTarget` |
| 707 | `ResolveMultipliers_ReturnsNull_WhenMultipliersNull` |
| 723 | `SelectRefPriceByDirection_ReturnsAsk_WhenLong` |
| 752 | `SelectRefPriceByDirection_ReturnsBid_WhenShort` |

The old (inverted) method names must be absent from the file after this ticket.

### Rule Constraints

- **AGENTS.md §2 Platinum Standard (xUnit-only test framework mandate)**: `[Fact]` attribute
  is retained on all 5 renamed methods. No NUnit or MSTest attributes introduced.
- **AGENTS.md §2 Platinum Standard (CYC <= 8 mandate)**: CYC unchanged — rename does not alter
  branching. Applies per architect plan verification.
- **AGENTS.md §2 ASCII-Only Compliance**: All 5 new method names use ASCII-only identifier
  characters. No Unicode in identifiers.

### Expected Test Names

The following 5 test method names must appear in `BwaveCycLaneBTests.cs` after the ticket:

1. `IsPttBeRetryTriggerOrder_ReturnsTrue_WhenNameIsPttQxT`
2. `IsNativeExitName_ReturnsFalse_WhenNameIsTarget`
3. `ResolveMultipliers_ReturnsNull_WhenMultipliersNull`
4. `SelectRefPriceByDirection_ReturnsAsk_WhenLong`
5. `SelectRefPriceByDirection_ReturnsBid_WhenShort`

### Acceptance Criteria

1. All 5 new method names are present in the file.
2. The original inverted method names are absent.
3. Method bodies are byte-for-byte identical to pre-rename (assertions unchanged).
4. `dotnet test --filter "FullyQualifiedName~BwaveCycLaneBTests"` — all 5 renamed tests pass.

### SCAN CHECKLIST

| Scan ID | Check | Command | Expected Result |
|---------|-------|---------|-----------------|
| SCAN-01 | No `lock()` | `grep -n "lock(" src/PropTraderTools/Tests/BwaveCycLaneBTests.cs` | 0 results |
| SCAN-02 | No `async void` | `grep -n "async void" src/PropTraderTools/Tests/BwaveCycLaneBTests.cs` | 0 results |
| SCAN-03 | No `return null` (new code) | `grep -n "return null" src/PropTraderTools/Tests/BwaveCycLaneBTests.cs` | 0 new nulls introduced |
| SCAN-04 | No `throw new` (new code) | `grep -n "throw new" src/PropTraderTools/Tests/BwaveCycLaneBTests.cs` | 0 new throws introduced |
| SCAN-05 | CYC <= 8 | Rename-only change; CYC unchanged; verify with `python scripts/complexity_audit.py` | PASS |
| SCAN-06 | ASCII-only | 5 new method names use ASCII identifiers only; no new non-ASCII bytes | PASS |
| SCAN-07 | xUnit only | `grep -n "using NUnit\|using MSTest\|\[Test\]\|\[TestMethod\]" src/PropTraderTools/Tests/BwaveCycLaneBTests.cs` | 0 results |

---

## TICKET C-4: Test Hardening — 3 Missing Execution Paths

**Spec Requirements**: DW-B37-01, DW-B37-03, DW-B37-05

### Files Modified

- `src/PropTraderTools/Tests/BwaveCycLaneBTests.cs`

### Method Signatures

No new methods. Existing test methods at approximately lines 142, 446, 697 are modified in-place.
New assertions or skip attributes are added to each.

### What To Do

Three test methods were flagged as missing execution-path coverage. For each, the engineer
applies one of two patterns — determined by inspecting the method body:

**Pattern A — NT8 host required (skip)**:
If the missing path requires a live `NinjaTrader.Cbi.Account`, an active strategy context, or
any NT8 runtime object that cannot be constructed in a unit test context:

```csharp
[Fact(Skip = "NT8-HOST-REQUIRED: <one-sentence reason describing the missing NT8 dependency>")]
public void ExistingMethodName()
{
    // ... existing body unchanged ...
}
```

**Pattern B — Pure logic, no NT8 dependency (expand)**:
If the missing branch is pure logic that can be exercised with controlled inputs:
Add the missing assertion. Keep the test method's CYC <= 4 (test methods must be simple).

**Per DW item**:

| DW Item | Approx. Line | Missing Path Description |
|---------|-------------|--------------------------|
| DW-B37-01 | 142 | `TryRecordBeTargetFill` — Order-based path not covered; check if `Account`/`Order` objects are required |
| DW-B37-03 | 446 | `TryFireFollowerBeRetry` — test calls predicate only; full method call not invoked; check NT8 dependency |
| DW-B37-05 | 697 | `CopyRule.Create` never called; normalization path not verified; check if purely constructable |

**Engineer instruction**: Inspect each method. If Pattern A applies, apply the skip attribute
and document the NT8 dependency in the skip message. If Pattern B applies, add the missing
assertion. Do not guess — read the method body.

**No new `lock()`, no new `throw new`, no new `return null`.** These rules apply to both
the test body additions and any inline helper expressions.

### Rule Constraints

- **AGENTS.md §2 Platinum Standard (xUnit-only test framework mandate)**: Use `[Fact]` or
  `[Fact(Skip = "...")]` only. `[Theory]`/`[InlineData]` acceptable if expanding a data-driven
  path. No NUnit `[Test]`, no MSTest `[TestMethod]`.
- **AGENTS.md §2 Platinum Standard (CYC <= 8 mandate)**: Any expanded test method CYC <= 4.
  Test methods must be simple and deterministic.
- **JS-001 (Result<T,E> / No exception throws)**: No new `throw new XxxException(...)` in
  test helpers or expanded assertions.
- **JS-002 (Option<T> / No return null)**: No `return null` in any new helper lambdas or
  local functions added during expansion.
- **JS-021 (No Lock)**: No `lock()` introduced in test bodies or helpers.

### Expected Test Names

No new `[Fact]` test method names added. The 3 existing methods at lines ~142, ~446, ~697 are
modified in-place (skip attribute added OR assertion expanded). The method names are not changed
by this ticket.

### Acceptance Criteria

1. All 3 methods (DW-B37-01, 03, 05) have either a `[Fact(Skip = "NT8-HOST-REQUIRED: ...")]`
   attribute with a human-readable documented reason, OR a new assertion covering the previously
   missing execution path.
2. Skip messages are descriptive and reference the NT8 host dependency.
3. If expanded (Pattern B): new assertions are deterministic and do not depend on NT8 runtime.
4. `dotnet test --filter "FullyQualifiedName~BwaveCycLaneBTests"` — each modified test reports
   `Pass` or `Skipped`. Zero `Failed` results.

### SCAN CHECKLIST

| Scan ID | Check | Command | Expected Result |
|---------|-------|---------|-----------------|
| SCAN-01 | No `lock()` | `grep -n "lock(" src/PropTraderTools/Tests/BwaveCycLaneBTests.cs` | 0 results |
| SCAN-02 | No `async void` | `grep -n "async void" src/PropTraderTools/Tests/BwaveCycLaneBTests.cs` | 0 results |
| SCAN-03 | No `return null` (new code) | `grep -n "return null" src/PropTraderTools/Tests/BwaveCycLaneBTests.cs` | 0 new nulls in added lines |
| SCAN-04 | No `throw new` (new code) | `grep -n "throw new" src/PropTraderTools/Tests/BwaveCycLaneBTests.cs` | 0 new throws in added lines |
| SCAN-05 | CYC <= 8 | Any expanded test method CYC <= 4; verify with `python scripts/complexity_audit.py` | PASS |
| SCAN-06 | ASCII-only | No new non-ASCII bytes in added lines | PASS |
| SCAN-07 | xUnit only | `grep -n "using NUnit\|using MSTest\|\[Test\]\|\[TestMethod\]" src/PropTraderTools/Tests/BwaveCycLaneBTests.cs` | 0 results |

---

## TICKET C-5: B76Tests.cs — IL-Scanning Fixes

**Spec Requirements**: DW-C39-11, DW-C39-12

### Files Modified

- `src/PropTraderTools/B76Tests.cs` (**ROOT level** — NOT in `Tests/` subdirectory)

### Method Signatures

No new public test methods added. The following existing methods are modified in-place:

- `T_B76_08` (line ~313) — MetadataToken comparison replaced with stable `MethodInfo` lookup
- `T_B76_02`, `T_B76_03`, `T_B76_04`, `T_B76_05`, `T_B76_06`, `T_B76_11` — IL opcode
  scanning loops replaced with behavioral assertions (or skipped with documented reason)

Any private helper methods introduced to support the refactored assertions must have
CYC <= 8 individually.

### What To Do

#### DW-C39-11: Fix `T_B76_08` — MetadataToken cross-assembly issue

**Problem**: `T_B76_08` resolves `Interlocked.Exchange` via `MetadataToken` comparison. Token
values differ per compilation unit and are not stable across assembly boundaries.

**Fix**: Replace the `MetadataToken`-based resolution with a direct `MethodInfo` lookup by
method name, declaring type, and exact parameter types. Example pattern:

```csharp
// STABLE: resolve by type + name + parameter types
var exchangeMethod = typeof(System.Threading.Interlocked)
    .GetMethod(
        "Exchange",
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
        null,
        new[] { typeof(int).MakeByRefType(), typeof(int) },
        null);
Assert.NotNull(exchangeMethod);
```

The engineer MUST inspect the actual `T_B76_08` body to determine the correct overload (the
exact `Type[]` array passed to `GetMethod`) before applying this pattern. The overload being
tested may differ from the int example above.

**Acceptance**: Zero `MetadataToken` comparisons remain in `B76Tests.cs`.

#### DW-C39-12: Fix `T_B76_02/03/04/05/06/11` — Replace fragile IL assertions

**Problem**: These tests assert on raw IL opcode sequences. IL-level assertions test
implementation artifact rather than behavioral contract; they break when JIT or compiler
changes code-gen.

**Fix decision per method**:
1. If the method under test can be called with controlled inputs (no NT8 host required):
   replace the IL scanning loop with a behavioral call-and-assert pattern:
   ```csharp
   // BEHAVIORAL (preferred):
   var result = MethodUnderTest(inputA, inputB);
   Assert.Equal(expectedValue, result);
   ```
2. If the method under test cannot be called without a live NT8 runtime context:
   ```csharp
   [Fact(Skip = "NT8-HOST-REQUIRED: behavioral assertion requires live NT8 runtime")]
   public void T_B76_XX()
   {
       // original body preserved as comment for reference
   }
   ```

When adding a Roslyn version dependency note is genuinely required (e.g. the IL test is kept
because behavior cannot be verified without it), add a comment on the assertion line:
```csharp
// IL assertion: depends on Roslyn codegen; valid for net8.0 target. Review on toolchain upgrade.
```

### Rule Constraints

- **AGENTS.md §2 Platinum Standard (xUnit-only test framework mandate)**: `[Fact]` or
  `[Fact(Skip = "...")]` only. Behavioral assertions preferred over IL-implementation assertions.
- **AGENTS.md §2 Platinum Standard (CYC <= 8 mandate)**: Any new helper method CYC <= 8.
- **JS-001 (No exception throws)**: No `throw new` in new helper methods.
- **JS-002 (No return null)**: No `return null` in new helper methods.
- **JS-021 (No Lock)**: No `lock()` in any added code.

### Expected Test Names

No new `[Fact]` method names. Existing methods `T_B76_08`, `T_B76_02`, `T_B76_03`, `T_B76_04`,
`T_B76_05`, `T_B76_06`, `T_B76_11` are modified in-place.

### Acceptance Criteria

1. **DW-C39-11**: `grep -n "MetadataToken" src/PropTraderTools/B76Tests.cs` returns 0 results.
2. **DW-C39-12**: `T_B76_02/03/04/05/06/11` use behavioral assertions OR have
   `[Fact(Skip = "NT8-HOST-REQUIRED: ...")]` applied with documented reason.
3. `dotnet test --filter "FullyQualifiedName~B76Tests"` — all tests `Pass` or `Skipped`.
   Zero `Failed` results.

### SCAN CHECKLIST

| Scan ID | Check | Command | Expected Result |
|---------|-------|---------|-----------------|
| SCAN-01 | No `lock()` | `grep -n "lock(" src/PropTraderTools/B76Tests.cs` | 0 results |
| SCAN-02 | No `async void` | `grep -n "async void" src/PropTraderTools/B76Tests.cs` | 0 results |
| SCAN-03 | No `return null` (new code) | `grep -n "return null" src/PropTraderTools/B76Tests.cs` | 0 new nulls in added lines |
| SCAN-04 | No `throw new` (new code) | `grep -n "throw new" src/PropTraderTools/B76Tests.cs` | 0 new throws in added lines |
| SCAN-05 | CYC <= 8 | All new/modified methods <= 8; verify with `python scripts/complexity_audit.py` | PASS |
| SCAN-06 | ASCII-only | No non-ASCII bytes in added lines | PASS |
| SCAN-07 | xUnit only | `grep -n "using NUnit\|using MSTest\|\[Test\]\|\[TestMethod\]" src/PropTraderTools/B76Tests.cs` | 0 results |

---

## TICKET C-6: B77Tests.cs — Opcode and Helper-Scan Fixes

**Spec Requirements**: DW-C39-13, DW-C39-14

### Files Modified

- `src/PropTraderTools/TradeCopierPanelB77Tests.cs` (**ROOT level** — NOT in `Tests/` subdirectory)

### Method Signatures

No new public test methods added. The following existing methods are modified in-place:

- `T_B77_TPL_05` (line ~155) — wrong opcode `ldstr` (0x72) replaced with `ldsfld` (0x7E)
- `T_B77_TPL_04` (line ~101) — IL scan of wrong method body replaced with behavioral assertion
  or skip

Any private helper methods introduced must have CYC <= 8 individually.

### What To Do

#### DW-C39-13: Fix `T_B77_TPL_05` — ldstr vs ldsfld opcode

**Problem**: The test scans for `OpCodes.Ldstr` (0x72) to detect `return string.Empty`, but
the C# compiler emits `OpCodes.Ldsfld` (0x7E) for a `string.Empty` static field reference.
The `Ldstr` scan never matches, so the test does not guard against `return null` regressions.

**Fix**: Replace `OpCodes.Ldstr` with `OpCodes.Ldsfld` in the assertion. If the assertion also
checks the operand, verify the operand resolves to the `string.Empty` field token:

```csharp
// BEFORE (wrong opcode — never matches string.Empty):
Assert.Contains(OpCodes.Ldstr, GetOpcodes(method));

// AFTER (correct opcode for static field reference):
Assert.Contains(OpCodes.Ldsfld, GetOpcodes(method));
```

If the existing helper already extracts operand field tokens, additionally verify the token:
```csharp
var emptyFieldToken = typeof(string)
    .GetField("Empty", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
    .MetadataToken;
// then assert the ldsfld instruction has this operand token
```

**Acceptance for DW-C39-13**: `T_B77_TPL_05` would fail if the production method's
`return string.Empty` were replaced with `return null`.

#### DW-C39-14: Fix `T_B77_TPL_04` — Wrong scan target

**Problem**: `T_B77_TPL_04` scans `GetLeaderAtmTemplateName` body for
`get_SelectedAtmStrategy`, but the actual call is in the helper `TryGetAtmNameFromSelector`.
The scan of the wrong method body means the guard never fires.

**Fix options (engineer chooses based on method accessibility)**:

**Option A** — Scan the correct method body:
```csharp
// Scan TryGetAtmNameFromSelector IL instead:
var helper = typeof(SUT).GetMethod(
    "TryGetAtmNameFromSelector",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
// ... existing IL scan logic applied to this method body ...
```

**Option B** — Behavioral assertion (preferred if `TryGetAtmNameFromSelector` is accessible):
```csharp
// Call the production method with controlled input, verify fallback return value:
var result = SUT.GetLeaderAtmTemplateName(/* controlled input that forces fallback */);
Assert.Equal("ExpectedFallbackValue", result);
```

**Option C** — Skip if NT8 host required:
```csharp
[Fact(Skip = "NT8-HOST-REQUIRED: TryGetAtmNameFromSelector requires live selector context")]
public void T_B77_TPL_04()
{
    // ...
}
```

**Acceptance for DW-C39-14**: `T_B77_TPL_04` would fail if `get_SelectedAtmStrategy` were
reintroduced into the hot path OR the test is explicitly skipped with documented NT8 reason.

### Rule Constraints

- **AGENTS.md §2 Platinum Standard (xUnit-only test framework mandate)**: `[Fact]` or
  `[Fact(Skip = "...")]` only. Behavioral assertions preferred.
- **AGENTS.md §2 Platinum Standard (CYC <= 8 mandate)**: Any new helper method CYC <= 8.
- **JS-001 (No exception throws)**: No `throw new` in new code.
- **JS-002 (No return null)**: No `return null` in new helper methods.
- **JS-021 (No Lock)**: No `lock()` in any added code.

### Expected Test Names

No new `[Fact]` method names. `T_B77_TPL_05` and `T_B77_TPL_04` are modified in-place.

### Acceptance Criteria

1. **DW-C39-13**: `T_B77_TPL_05` uses `OpCodes.Ldsfld` (not `OpCodes.Ldstr`) in its opcode
   assertion. Test would fail if production code replaced `string.Empty` with `null`.
2. **DW-C39-14**: `T_B77_TPL_04` scans the correct method body OR uses a behavioral assertion
   OR has `[Fact(Skip = "NT8-HOST-REQUIRED: ...")]` with documented reason.
3. `dotnet test --filter "FullyQualifiedName~TradeCopierPanelB77Tests"` — all tests `Pass`
   or `Skipped`. Zero `Failed` results.

### SCAN CHECKLIST

| Scan ID | Check | Command | Expected Result |
|---------|-------|---------|-----------------|
| SCAN-01 | No `lock()` | `grep -n "lock(" src/PropTraderTools/TradeCopierPanelB77Tests.cs` | 0 results |
| SCAN-02 | No `async void` | `grep -n "async void" src/PropTraderTools/TradeCopierPanelB77Tests.cs` | 0 results |
| SCAN-03 | No `return null` (new code) | `grep -n "return null" src/PropTraderTools/TradeCopierPanelB77Tests.cs` | 0 new nulls in added lines |
| SCAN-04 | No `throw new` (new code) | `grep -n "throw new" src/PropTraderTools/TradeCopierPanelB77Tests.cs` | 0 new throws in added lines |
| SCAN-05 | CYC <= 8 | All new/modified methods <= 8; verify with `python scripts/complexity_audit.py` | PASS |
| SCAN-06 | ASCII-only | No non-ASCII bytes in added lines | PASS |
| SCAN-07 | xUnit only | `grep -n "using NUnit\|using MSTest\|\[Test\]\|\[TestMethod\]" src/PropTraderTools/TradeCopierPanelB77Tests.cs` | 0 results |

---

## TICKET C-7: B75Tests.cs — Singleton Mutation Teardown

**Spec Requirements**: DW-C39-15

### Files Modified

- `src/PropTraderTools/TradeCopierPanelB75Tests.cs` (**ROOT level** — NOT in `Tests/` subdirectory)

### Method Signatures

No new methods added. The existing method `T_B66OBJ_P02` (approximately line 257) is modified
in-place to wrap its body in a `try/finally` that captures and restores `CopyEngine.Instance`
state before mutation and after (unconditionally).

### What To Do

**Problem**: `T_B66OBJ_P02` calls `CopyEngine.Instance.SetCloneAtmObjectCache(null)` and
`CopyEngine.Instance.SetCloneAtmCache("")` without restoring the original values after the
test. This causes singleton pollution: subsequent test runs in the same process may observe
mutated state from a prior run.

**Fix — Capture, mutate, restore pattern**:
```csharp
[Fact]
public void T_B66OBJ_P02()
{
    var origObj = CopyEngine.Instance.GetCloneAtmObjectCache();
    var origStr = CopyEngine.Instance.GetCloneAtmCache();
    try
    {
        // --- existing test body (SetCloneAtmObjectCache / SetCloneAtmCache calls) ---
        CopyEngine.Instance.SetCloneAtmObjectCache(null);
        CopyEngine.Instance.SetCloneAtmCache("");
        // ... existing assertions ...
    }
    finally
    {
        CopyEngine.Instance.SetCloneAtmObjectCache(origObj);
        CopyEngine.Instance.SetCloneAtmCache(origStr);
    }
}
```

**If getters `GetCloneAtmObjectCache()` / `GetCloneAtmCache()` do not exist**:
The engineer must choose one of these approaches:
- **Option A**: Add the minimal getter methods on `CopyEngine` (public, returning the backing
  field directly — no logic). This is the preferred approach if `CopyEngine` is accessible.
- **Option B**: Use reflection to read the backing field:
  ```csharp
  var origObj = (SomeType)typeof(CopyEngine)
      .GetField("_cloneAtmObjectCache", BindingFlags.NonPublic | BindingFlags.Instance)
      .GetValue(CopyEngine.Instance);
  ```
- **Option C**: If `CopyEngine.Instance` is a `get`-only property with no setter and no
  accessible mutator, apply:
  ```csharp
  [Fact(Skip = "DW-C39-15: CopyEngine.Instance has no setter — teardown not possible without production change")]
  public void T_B66OBJ_P02() { /* ... */ }
  ```

**Constraints**:
- No `lock()` in the `try/finally` block.
- The `finally` block MUST contain only the restore statements. No logic, no assertions in `finally`.
- CYC of the modified test method must remain <= 3 (the `try` block counts as one branch; one
  assertion is effectively linear; `finally` is unconditional).

### Rule Constraints

- **JS-021 (No Lock)**: The `try/finally` teardown uses no `lock()`. P0 CRITICAL — zero tolerance.
- **AGENTS.md §2 Platinum Standard (xUnit-only test framework mandate)**: `[Fact]` is retained.
  The teardown pattern is xUnit-idiomatic (`IDisposable`-less inline try/finally).
- **AGENTS.md §2 Platinum Standard (CYC <= 8 mandate)**: CYC of the modified `T_B66OBJ_P02`
  method <= 3 after the wrap.
- **JS-001 (No exception throws)**: No `throw new` introduced.
- **JS-002 (No return null)**: No `return null` in the `finally` block or helper expressions.

### Expected Test Names

No new `[Fact]` test method names. `T_B66OBJ_P02` is modified in-place.

### Acceptance Criteria

1. `T_B66OBJ_P02` has a `try/finally` block.
2. The `finally` block restores `CopyEngine.Instance` state (both `CloneAtmObjectCache` and
   `CloneAtmCache`) unconditionally.
3. Running `dotnet test --filter "FullyQualifiedName~TradeCopierPanelB75Tests"` twice in
   sequence produces identical results (no singleton pollution between runs).
4. No `lock()` in the modified method.
5. CYC of `T_B66OBJ_P02` <= 3.

### SCAN CHECKLIST

| Scan ID | Check | Command | Expected Result |
|---------|-------|---------|-----------------|
| SCAN-01 | No `lock()` | `grep -n "lock(" src/PropTraderTools/TradeCopierPanelB75Tests.cs` | 0 results — P0 CRITICAL |
| SCAN-02 | No `async void` | `grep -n "async void" src/PropTraderTools/TradeCopierPanelB75Tests.cs` | 0 results |
| SCAN-03 | No `return null` (new code) | `grep -n "return null" src/PropTraderTools/TradeCopierPanelB75Tests.cs` | 0 new nulls in added lines |
| SCAN-04 | No `throw new` (new code) | `grep -n "throw new" src/PropTraderTools/TradeCopierPanelB75Tests.cs` | 0 new throws in added lines |
| SCAN-05 | CYC <= 8 | `T_B66OBJ_P02` CYC <= 3 after wrap; verify with `python scripts/complexity_audit.py` | PASS |
| SCAN-06 | ASCII-only | No non-ASCII bytes in added lines | PASS |
| SCAN-07 | xUnit only | `grep -n "using NUnit\|using MSTest\|\[Test\]\|\[TestMethod\]" src/PropTraderTools/TradeCopierPanelB75Tests.cs` | 0 results |

---

## FINAL VERIFICATION (after all 7 tickets)

```powershell
# 1. CSharpier clean
dotnet csharpier check src/

# 2. Full build
dotnet build src/PropTraderTools.sln

# 3. Full test suite
dotnet test src/PropTraderTools.sln
```

Expected:
- `dotnet csharpier check` — exit code 0.
- `dotnet build` — exit code 0, zero errors.
- `dotnet test` — zero `Failed`. All results are `Pass` or `Skipped`.
  Any `Skipped` result MUST carry `NT8-HOST-REQUIRED` or `DW-C39-15` in its skip message.

**NT8 sync**: NOT required. No production `.cs` files were modified.
**F5 in NinjaTrader 8**: NOT required.

---

## DW ITEM CLOSURE MAP

| DW Item | Closed By | File |
|---------|-----------|------|
| DW-LaneA-01 | C-1 | CopyEngineTests.cs |
| DW-LaneA-02 | C-1 | CopyEngineTests.cs |
| DW-LaneA-03 | C-1 | CopyEngineTests.cs |
| DW-LaneA-04 | C-2 | CopyEngineTests.cs, B46Tests.cs, B47Tests.cs |
| DW-LaneA-05 | C-1 | BwaveCycLaneCTests.cs |
| DW-B37-01 | C-4 | BwaveCycLaneBTests.cs |
| DW-B37-02 | C-3 | BwaveCycLaneBTests.cs |
| DW-B37-03 | C-4 | BwaveCycLaneBTests.cs |
| DW-B37-04 | C-3 | BwaveCycLaneBTests.cs |
| DW-B37-05 | C-4 | BwaveCycLaneBTests.cs |
| DW-B37-06 | C-3 | BwaveCycLaneBTests.cs |
| DW-B37-07 | C-3 | BwaveCycLaneBTests.cs |
| DW-B37-08 | C-3 | BwaveCycLaneBTests.cs |
| DW-C39-11 | C-5 | B76Tests.cs |
| DW-C39-12 | C-5 | B76Tests.cs |
| DW-C39-13 | C-6 | TradeCopierPanelB77Tests.cs |
| DW-C39-14 | C-6 | TradeCopierPanelB77Tests.cs |
| DW-C39-15 | C-7 | TradeCopierPanelB75Tests.cs |

---

*ptt-architect | BWAVE-DW LaneC | 2026-09-04*
