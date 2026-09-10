# 04-tickets.md
# Epic: PTT-REPAIRS-10-B7-TYPEINIT
# Phase: 3 -- Ticket Generation
# Status: TICKETS_COMPLETE

---

## T1 — Apply 5th NT8-Runtime Skip: LogDiagOrderCount Test

### 1. Spec Requirement IDs (Traceability)

| ID | Source |
|----|--------|
| DW-B7-01 | PTT-REPAIRS-07-NT8-BULK-SKIP / 06-deferred-backlog.md |
| DW-B7-01 (carried) | PTT-REPAIRS-09-OBFUSC-ATTR / 06-deferred-backlog.md |

Closing action: Final open item from DW-B7-01. After this ticket the deferred item is CLOSED.

---

### 2. File and Exact Line Number

**File**: `src/PropTraderTools/CopyEngineTests.cs`
**Line**: 6091
**Class**: `B79CancelRaceGuardTests`

---

### 3. Exact Before / After Text

**BEFORE** (current content at L6091):

```csharp
        [Fact]
        public void LogDiagOrderCount_ShouldLogCorrectCount_WhenOrdersExistForInstrument()
```

**AFTER** (replacement — attribute line only, method signature line unchanged):

```csharp
        [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]
        public void LogDiagOrderCount_ShouldLogCorrectCount_WhenOrdersExistForInstrument()
```

**Skip string (exact — no deviation permitted)**:
```
NT8-runtime: CopyEngine.cctor requires NT8 host
```

Only L6091 changes. L6092 (`public void LogDiagOrderCount_...`) is **not touched**.

---

### 4. Method Signatures Involved

**Test method (signature unchanged by this ticket)**:

```csharp
public void LogDiagOrderCount_ShouldLogCorrectCount_WhenOrdersExistForInstrument()
```

No parameters. No return value. No changes to the body (L6093-L6096). Attribute only.

**Helper referenced (read-only, not modified)**:

```csharp
private static System.Reflection.MethodInfo GetMethod(string name)
```

Located at approximately L5845-L5847. Not touched.

---

### 5. JS Rule Constraints

| Rule | Constraint | Applies To This Ticket |
|------|-----------|------------------------|
| JS-021 | No `lock()` anywhere | PASS — attribute change only; no concurrency construct added |
| JS-001 | No `throw` in dispatch / gate chain | PASS — no throw added |
| JS-003 | No magic string without named constant in production | N/A — test file only |
| NT8-ASCII | ASCII-only in all string literals | PASS — skip string is pure ASCII (verified char-by-char) |
| NT8-NO-DATETIME-NOW | No `DateTime.Now` | PASS — no datetime usage |
| NT8-NO-FONTFAMILY | No `FontFamily` | PASS — N/A |
| NT8-NO-HEX-COLOR | No hardcoded hex colors | PASS — N/A |
| NT8-CREATEORDER-PREFIX | All CreateOrder calls use `PTT-` prefix | PASS — no CreateOrder calls |
| CYC <= 8 | Cyclomatic complexity per method <= 8 | PASS — attribute does not alter control flow |
| SCOPE | No production `.cs` files touched | PASS — `CopyEngineTests.cs` is test-only |
| HARD-LINK | `deploy-sync.ps1` required after `src/` edits | NOT REQUIRED — test file only |

---

### 6. xUnit [Fact] Being Modified

**Test name**:
```
LogDiagOrderCount_ShouldLogCorrectCount_WhenOrdersExistForInstrument
```

**State transition**: ACTIVE → SKIPPED

**Current attribute** (L6091): `[Fact]`
**New attribute** (L6091): `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]`

**No new tests authored. No existing tests removed.**

Context: This is the 5th and final test in `B79CancelRaceGuardTests` requiring NT8-runtime skip.
The 4 already-skipped tests are at L5855, L5887, L5924, and L5951.

---

### 7. 7-SCAN CHECKLIST

Engineer must verify each scan passes before marking T1 complete.

---

#### SCAN-01: ASCII-Only

**Check**: `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngineTests.cs`

**Pass criterion**: Zero matches in `CopyEngineTests.cs`.

The skip string `NT8-runtime: CopyEngine.cctor requires NT8 host` contains only printable
ASCII characters (0x20-0x7E). No Unicode, no curly quotes, no em-dashes, no emoji.

**Expected result**: `grep` returns no output (exit code 1 = no match = PASS).

---

#### SCAN-02: lock() Free

**Check**: `grep -n "lock(" src/PropTraderTools/CopyEngineTests.cs`

**Pass criterion**: Zero matches.

The ticket adds one attribute line. No concurrency constructs are introduced.

**Expected result**: `grep` returns no output (exit code 1 = no match = PASS).

---

#### SCAN-03: No New throw

**Check**: diff the modified file against the pre-edit state; confirm only L6091 changed.

**Pass criterion**: `git diff src/PropTraderTools/CopyEngineTests.cs` shows exactly
one hunk — removal of `[Fact]` and addition of
`[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` at L6091.
No `throw` keyword appears in the diff.

**Expected result**: Diff hunk is 1 line removed, 1 line added. No `throw` in diff.

---

#### SCAN-04: CYC Unchanged

**Check**: Inspect cyclomatic complexity of
`LogDiagOrderCount_ShouldLogCorrectCount_WhenOrdersExistForInstrument`.

**Pass criterion**: CYC = 1 (before and after). Attribute decorators do not contribute
to cyclomatic complexity. Method body (L6093-L6096) is unchanged — single linear path,
no branches, no loops.

**Expected result**: Static analysis tool (or manual inspection) confirms CYC = 1.

---

#### SCAN-05: Build Clean

**Check**:
```
dotnet build
```

**Pass criterion**: Build completes with `0 Error(s)`.

`[Fact(Skip = "...")]` is a valid xUnit v2+ attribute accepting a `string` named parameter.
No syntax errors. No type-resolution errors.

**Expected result**: `Build succeeded.` with `0 Error(s)`.

---

#### SCAN-06: Test Counts

**Check**:
```
dotnet test --no-build
```

**Pass criterion**:
```
passed: 23
failed: 0
skipped: 491
total: 514
```

Baseline before this ticket: passed=24, failed=0, skipped=490, total=514.
After skip: one test moves from passed to skipped. Net change: passed -1, skipped +1.
Total remains 514.

Note: The orchestrator spec stated `passed=24, skipped=491` which sums to 515 (arithmetic
error). The correct mathematically consistent target is `passed=23, skipped=491, total=514`
as derived in the REVIEW_PASS plan (§7 NOTE). This ticket uses the correct target.

**Expected result**: xUnit runner output matches the criterion above exactly.

---

#### SCAN-07: Scope Check

**Check**:
```
git diff --name-only
```

**Pass criterion**: Output contains exactly one file: `src/PropTraderTools/CopyEngineTests.cs`.
No `src/PropTraderTools/CopyEngine.cs` or any other production file appears.
No `deploy-sync.ps1` invocation is required (test-only change).

**Expected result**: Single-file diff. No production source modified.

---

### 8. Implementation Steps (Engineer Contract)

1. Open `src/PropTraderTools/CopyEngineTests.cs`.
2. Navigate to L6091.
3. Verify current content is exactly: `        [Fact]`
   (8 spaces of indentation, then `[Fact]`).
4. Replace L6091 with:
   `        [Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]`
   (same 8-space indentation, skip string verbatim, no trailing space).
5. Save. Do NOT modify any other line.
6. Run all 7 scans. All must PASS before marking T1 complete.
7. No `deploy-sync.ps1` invocation. No `git add` of any other file.

---

### 9. Deferred Backlog Closure

| ID | Item | Action |
|----|------|--------|
| DW-B7-01 | B79CancelRaceGuardTests: 5th TypeInit test | CLOSED by T1 |

No new deferred work items introduced.

---

## Return Value

**TICKETS_COMPLETE**
