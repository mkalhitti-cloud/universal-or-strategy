# W9-L6-001 Architecture Plan -- Hot-Path Exception Safety Fix

| Field | Value |
|-------|-------|
| **W9_ID** | W9-L6-001 |
| **Finding file** | `src/V12_002.IO.PathValidation.cs` |
| **Finding line** | 56 |
| **Violation** | `throw new ArgumentException` reachable from strategy thread via OnBarUpdate chain |
| **Fix strategy** | Option (c): replace throw with log + return null; add null-guard at strategy-thread call site |
| **Scan report** | `docs/brain/wave9-findings/W9-L6-001/scan.md` |
| **Status** | PLAN ONLY -- no edits made |

---

## 1. Fix Scope

Two files require changes:

| File | Change | Lines affected |
|------|--------|---------------|
| `src/V12_002.IO.PathValidation.cs` | Replace `throw new ArgumentException` with log + `return null` | 56-58 |
| `src/V12_002.UI.Compliance.cs` | Add `if (validCsvPath == null) return;` after line 147 | 148 (insert) |

All other call sites (lines 171, 232, 993 in Compliance.cs; all StickyState.cs sites) are already
inside `Task.Run` blocks with `catch` handlers that swallow all exceptions -- a `null` return
would cause an inner `NullReferenceException` that is caught and swallowed, identical behavior
to the current `ArgumentException`. No change needed at those sites.

---

## 2. Exact Before/After Diff -- src/V12_002.IO.PathValidation.cs

### BEFORE (lines 53-59, exact source)

```csharp
                // Guard: Null/empty check
                if (string.IsNullOrWhiteSpace(path))
                {
                    throw new ArgumentException(
                        string.Format("[IO_VALIDATION] Path cannot be null/empty for operation: {0}", operation)
                    );
                }
```

### AFTER

```csharp
                // Guard: Null/empty check
                if (string.IsNullOrWhiteSpace(path))
                {
                    NinjaTrader.Code.Output.Process("[IO_VALIDATION] Path cannot be null/empty for operation: " + operation, PrintTo.OutputTab1);
                    return null;
                }
```

**Change summary**: 3 lines replaced with 2 lines. No signature change. No XML doc change
(the `<exception cref="ArgumentException">` in the doc comment at line 49 should also be
removed as it is no longer accurate -- see Section 4).

---

## 3. Exact Before/After Diff -- src/V12_002.UI.Compliance.cs (line 147 site)

### BEFORE (lines 144-159, exact source)

```csharp
            try
            {
                // EPIC-7-QUALITY-010: Validate CSV path before checking existence
                string validCsvPath = PathValidation.ValidateAndCanonicalize(dailySummaryCsvPath, "CheckCSV");

                if (System.IO.File.Exists(validCsvPath))
                {
                    Interlocked.Exchange(ref _csvHeaderCreated, 1);
                    return;
                }
            }
            catch (SecurityException ex)
            {
                Print(string.Format("[IO_SECURITY] CSV path validation failed: {0}", ex.Message));
                return;
            }
```

### AFTER (add null-guard immediately after the `ValidateAndCanonicalize` call)

```csharp
            try
            {
                // EPIC-7-QUALITY-010: Validate CSV path before checking existence
                string validCsvPath = PathValidation.ValidateAndCanonicalize(dailySummaryCsvPath, "CheckCSV");
                if (validCsvPath == null)
                    return;

                if (System.IO.File.Exists(validCsvPath))
                {
                    Interlocked.Exchange(ref _csvHeaderCreated, 1);
                    return;
                }
            }
            catch (SecurityException ex)
            {
                Print(string.Format("[IO_SECURITY] CSV path validation failed: {0}", ex.Message));
                return;
            }
```

**Change summary**: 2 lines inserted after line 147. No logic change to existing code.

---

## 4. XML Doc Comment Cleanup (PathValidation.cs line 49)

### BEFORE (lines 49-50, exact source)

```csharp
            /// <exception cref="ArgumentException">Path is null or empty</exception>
            /// <exception cref="SecurityException">Path traversal detected</exception>
```

### AFTER

```csharp
            /// <exception cref="SecurityException">Path traversal detected</exception>
```

**Change summary**: Remove the `ArgumentException` doc tag -- the method no longer throws
`ArgumentException`. `SecurityException` remains. This is part of the same minimal fix.

---

## 5. Call Site Analysis -- Full Blast Radius

### 5a. src/V12_002.UI.Compliance.cs line 147 -- EnsureDailySummaryCsv (CheckCSV)

| Property | Value |
|----------|-------|
| Thread | Strategy thread (called from OnBarUpdate chain, first bar only) |
| Pre-null-check on path | YES -- `if (string.IsNullOrEmpty(dailySummaryCsvPath)) return;` at line 139 |
| Null-safe after fix? | NO -- `File.Exists(validCsvPath)` would throw NRE if null returned |
| **Action** | **ADD null-guard** -- see diff in Section 3 |

### 5b. src/V12_002.UI.Compliance.cs line 171 -- EnsureDailySummaryCsv (WriteCSV) -- Task.Run

| Property | Value |
|----------|-------|
| Thread | ThreadPool (Task.Run) |
| Pre-null-check on path | YES -- inherits outer `IsNullOrEmpty` guard |
| Null-safe after fix? | NO (File.WriteAllText(null) throws NRE) BUT caught by `catch (Exception ex)` at line 185 |
| **Action** | **NO CHANGE** -- exception is caught and logged; behavior unchanged |

### 5c. src/V12_002.UI.Compliance.cs line 232 -- AppendDailySummary (AppendCSV) -- Task.Run

| Property | Value |
|----------|-------|
| Thread | ThreadPool (Task.Run) |
| Pre-null-check on path | YES -- `IsNullOrEmpty` at line 204 |
| Null-safe after fix? | NO (File.AppendAllText(null) throws NRE) BUT caught by bare `catch` at line 241 |
| **Action** | **NO CHANGE** -- swallowed; behavior unchanged |

### 5d. src/V12_002.UI.Compliance.cs line 993 -- WriteComplianceJsonAsync (WriteComplianceLog) -- Task.Run

| Property | Value |
|----------|-------|
| Thread | ThreadPool (Task.Run) |
| Pre-null-check on path | YES -- `if (path != null)` at line 990 |
| Null-safe after fix? | NO (File.WriteAllText(null) throws NRE) BUT caught by bare `catch` at line 1001 |
| **Action** | **NO CHANGE** -- swallowed; behavior unchanged |

### 5e. src/V12_002.StickyState.cs lines 70-72, 158, 186, 265, 294

| Property | Value |
|----------|-------|
| Thread | Startup / writer thread (never strategy hot-path) |
| Pre-null-check on path | Paths derived from `_stickyStatePath + ".bak"` etc. -- non-null in practice |
| Null-safe after fix? | All within `try{}catch{}` blocks -- NRE would be caught |
| **Action** | **NO CHANGE** -- out of scope for this hot-path finding; off strategy thread |

---

## 6. Scope Verification

**Changes required**: 2 files, 3 diff hunks total:
1. `PathValidation.cs` lines 56-58: replace throw (3 lines -> 2 lines)
2. `PathValidation.cs` lines 49: remove ArgumentException doc tag (1 line removed)
3. `Compliance.cs` line 148: insert 2-line null-guard

**Lines touched**: 6 total across 2 files.
**No unrelated changes**: No whitespace mutation, no adjacent refactoring, no test file changes.
**Scope creep check**: PASS -- touches only reported violation and minimum required call site.

---

## 7. Jane Street OKF Alignment

| Rule | Requirement | Status |
|------|-------------|--------|
| Rule 5 (hot-path throw) | No throws from strategy thread | FIXED -- throw replaced with log + return |
| Rule 6 (CYC <= 8) | No CYC increase | PASS -- removing a branch reduces CYC by 1 |
| Rule 11 (ASCII only) | All new strings must be ASCII | PASS -- only ASCII characters used |
| Rule 12 (naming) | No naming convention changes | PASS -- no renaming |
| microsecond-eternity.md zero_alloc | No new allocations on hot path | PASS -- string concat replaces string.Format (same allocation class; hot path is already non-zero-alloc for this one-shot path) |

---

## 8. Test Requirement

Per scan.md section 10: no new test is strictly required -- the fix removes dead defensive code.

Recommended regression test (optional, not blocking):
```csharp
// tests/V12_Performance.Tests/Core/PathValidationTests.cs
[Fact]
public void ValidateAndCanonicalize_WhenPathIsNull_ReturnsNull()
{
    string result = PathValidation.ValidateAndCanonicalize(null, "test");
    Assert.Null(result);
}
```

This test verifies the null-return behavior and prevents regression to the old throw.

---

## 9. EXIT GATE Checklist

- [x] Exact before/after diff for `src/V12_002.IO.PathValidation.cs` line 56 -- Section 2
- [x] Exact before/after diff for `src/V12_002.IO.PathValidation.cs` line 49 (doc tag) -- Section 4
- [x] Exact before/after diff for `src/V12_002.UI.Compliance.cs` line 147 null-guard -- Section 3
- [x] All other call sites analyzed and confirmed safe without changes -- Section 5
- [x] No edits made -- plan only
- [x] Plan written to `docs/brain/wave9-findings/W9-L6-001/plan.md`
