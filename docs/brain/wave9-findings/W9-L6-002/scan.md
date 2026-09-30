# W9-L6-002 Scan Report

| Field | Value |
|---|---|
| **W9_ID** | W9-L6-002 |
| **File** | `src/V12_002.IO.PathValidation.cs` |
| **Violation type** | `throw new SecurityException` on hot path |
| **OKF Rule** | Rule 5 — hot-path throw |
| **Status** | **CONFIRMED** |

---

## 1. Full Current Body of `ValidateAndCanonicalize`

File: [`src/V12_002.IO.PathValidation.cs`](../../../../src/V12_002.IO.PathValidation.cs)

```csharp
// lines 50–106
public static string ValidateAndCanonicalize(string path, string operation)
{
    // Guard: Null/empty check
    if (string.IsNullOrWhiteSpace(path))
    {
        NinjaTrader.Code.Output.Process(
            "[IO_VALIDATION] Path cannot be null/empty for operation: " + operation,
            PrintTo.OutputTab1
        );
        return null;
    }

    try
    {
        // Canonicalize: Resolve .., symlinks, and relative paths
        string canonical = Path.GetFullPath(path);

        // Security check: Ensure path stays within NinjaTrader 8 directory
        // Use trailing separator to prevent bypass via paths like "C:\NinjaTrader 8.1"
        // Allow exact match to base directory itself (for directory operations)
        if (
            !canonical.StartsWith(_baseDirWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !canonical.Equals(_baseDir, StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new SecurityException(                          // <-- LINE 75 (VIOLATION)
                string.Format(
                    "[IO_VALIDATION] Path traversal blocked for operation '{0}': {1} (canonical: {2}) is outside allowed base: {3}",
                    operation,
                    path,
                    canonical,
                    _baseDir
                )
            );
        }

        return canonical;
    }
    catch (SecurityException)
    {
        // Re-throw security exceptions as-is
        throw;
    }
    catch (Exception ex)
    {
        // Wrap other exceptions (e.g., invalid path characters)
        throw new SecurityException(                              // <-- LINE 96 (SECONDARY VIOLATION)
            string.Format(
                "[IO_VALIDATION] Path validation failed for operation '{0}': {1} - {2}",
                operation,
                path,
                ex.Message
            ),
            ex
        );
    }
}
```

---

## 2. Exact Line Numbers of SecurityException Throws

| Throw | Line | Expression |
|---|---|---|
| **Primary (path traversal)** | **75** | `throw new SecurityException(string.Format("[IO_VALIDATION] Path traversal blocked...", ...))` |
| **Secondary (exception wrap)** | **96** | `throw new SecurityException(string.Format("[IO_VALIDATION] Path validation failed...", ...), ex)` |

> **Note**: Line numbers shifted by +1 after the W9-L6-001 fix added a line. The register entry cited ~74; confirmed current is **line 75** (primary) and **line 96** (secondary).

---

## 3. Triggering Condition

### Primary throw (line 75)
**Trigger**: `canonical` path does NOT start with `_baseDirWithSeparator` AND does NOT equal `_baseDir`.
Specifically: any path that, after `Path.GetFullPath()` canonicalization, resolves **outside**
`%MyDocuments%\NinjaTrader 8\`. Examples:
- `../../secret.txt`
- `C:\Windows\System32\file.txt`
- `C:\NinjaTrader 8.1\evil.txt` (prevented by trailing-separator check)

### Secondary throw (line 96)
**Trigger**: `Path.GetFullPath(path)` throws any non-`SecurityException` — e.g., `ArgumentException` from null/illegal chars (though null is pre-filtered), `PathTooLongException`, etc.

---

## 4. Blast Radius — All Call Sites

### `src/V12_002.UI.Compliance.cs`

| Line | Call | Caller method | Exception handling |
|---|---|---|---|
| 147 | `ValidateAndCanonicalize(dailySummaryCsvPath, "CheckCSV")` | `EnsureDailySummaryCsv()` | `catch (SecurityException ex)` → logs + returns |
| 173 | `ValidateAndCanonicalize(_csvPath, "WriteCSV")` | `EnsureDailySummaryCsv()` → Task.Run | `catch (SecurityException ex)` → logs; `catch (Exception)` → logs |
| 234 | `ValidateAndCanonicalize(pathCopy, "AppendCSV")` | `AppendDailySummary()` → Task.Run | bare `catch { /* swallow */ }` |
| 997 | `ValidateAndCanonicalize(path, "WriteComplianceLog")` | `WriteComplianceJsonAsync()` → Task.Run | `catch (SecurityException ex)` → logs; bare `catch { /* swallow */ }` |

### `src/V12_002.StickyState.cs`

| Line | Call | Caller method | Exception handling |
|---|---|---|---|
| 70 | `ValidateAndCanonicalize(_stickyStatePath, "WriteState")` | `WriteSnapshotAtomic()` | `catch (SecurityException ex)` → logs + **re-throws**; `catch (Exception)` → returns false |
| 71 | `ValidateAndCanonicalize(tempPath, "WriteTempState")` | `WriteSnapshotAtomic()` | same |
| 72 | `ValidateAndCanonicalize(backupPath, "WriteBackupState")` | `WriteSnapshotAtomic()` | same |
| 158 | `ValidateAndCanonicalize(_stickyStatePath, "ReadState")` | `LoadStateSnapshot()` | `catch (SecurityException ex)` → logs + **re-throws**; `catch (Exception)` → returns null |
| 186 | `ValidateAndCanonicalize(_stickyStatePath, "ReadStateAfterRollback")` | `LoadStateSnapshot()` | same |
| 265 | `ValidateAndCanonicalize(backupPath, "ReadBackup")` | `RollbackToLastGoodState()` | (inherits caller's try/catch) |
| 294 | `ValidateAndCanonicalize(_stickyStatePath, "RollbackWrite")` | `RollbackToLastGoodState()` | (inherits caller's try/catch) |

### `src/V12_002.IO.PathValidation.cs`

| Line | Call | Caller method |
|---|---|---|
| 117 | `return ValidateAndCanonicalize(path, operation)` | `ValidateDirectoryPath()` — thin wrapper, no additional callers found |

---

## 5. Hot-Path Classification

### Direct NT8 hot-path callbacks

| Hot-path method | Chain to `ValidateAndCanonicalize`? |
|---|---|
| `OnBarUpdate()` | **NO** — sticky state `SaveStickyState()` is defined but **never called** from `OnBarUpdate`. No call site found. |
| `OnOrderUpdate()` | **NO** — no path through compliance or sticky state. |
| `OnExecutionUpdate()` | **INDIRECT** — see below. |
| `Dispatch*` / `ProcessOn*` | **INDIRECT** — see below. |

### Indirect chain via `OnExecutionUpdate`

```
OnExecutionUpdate()                       [broker thread]
  └─ Enqueue(ctx => ProcessOnExecutionUpdate(...))
       └─ ProcessOnExecutionUpdate()      [actor drain — strategy thread]
            └─ ProcessComplianceTracking()
                 └─ LogApexPerformance()
                      └─ WriteComplianceJsonAsync()    [Task.Run — async, NOT hot path]
                           └─ PathValidation.ValidateAndCanonicalize(path, "WriteComplianceLog")
```

**Key finding**: `WriteComplianceJsonAsync` always fires its I/O inside **`Task.Run`** — the `ValidateAndCanonicalize` call at line 997 executes on a **thread-pool thread**, not the actor/strategy thread. The `throw new SecurityException` at line 75 propagates to the `catch (SecurityException)` at line 1001 and is **swallowed there**. This call site is **NOT on the hot path**.

### Additional indirect chain via `ProcessAccountExecutionQueue`

```
(timer / custom event) → ProcessAccountExecutionQueue()  [strategy thread]
  └─ LogApexPerformance()
       └─ WriteComplianceJsonAsync()   [Task.Run — async]
            └─ ValidateAndCanonicalize(...)
```

Same conclusion: async, off hot path.

### StickyState chain

`LoadStickyState()` / `SaveStickyState()` — called from `Lifecycle.cs:624` during **strategy initialization**, not from `OnBarUpdate` / `OnExecutionUpdate`. `SaveStickyState` has **zero callers** outside of its own definition. Both are initialization-time / non-hot-path.

### Verdict

> **The `throw new SecurityException` at line 75 is NOT directly on a tick-processing hot path.**
> All call sites that reach `ValidateAndCanonicalize` are either:
> (a) inside `Task.Run` fire-and-forget async blocks (Compliance CSV/JSON writers), or
> (b) inside strategy initialization (`LoadStickyState`) or periodic state flush (`SaveStickyState` — effectively dead code with zero runtime callers), or
> (c) inside `WriteSnapshotAtomic` which is called only from `SaveStickyState` (dead call chain).
>
> **However**, the violation remains: the method *throws* rather than returning a `bool`/`Result`. If any future caller invokes it synchronously on the strategy thread, the exception will unwind the actor. The OKF Rule 5 mandate applies regardless of current hot-path usage — the correct pattern is to return `null`/`bool` and log rather than throw (matching what the `null/empty` guard at line 53–60 already does).

---

## 6. Recommended Fix (Minimal)

**Strategy**: Convert both throws to log-and-return-null, matching the existing null-guard pattern at line 52–60. Do **not** change the method signature; callers already check for `null` return.

```csharp
// PRIMARY throw (line 75) — REPLACE with:
NinjaTrader.Code.Output.Process(
    string.Format(
        "[IO_VALIDATION] Path traversal blocked for operation '{0}': {1} (canonical: {2}) is outside allowed base: {3}",
        operation, path, canonical, _baseDir),
    PrintTo.OutputTab1);
return null;

// SECONDARY throw (line 96, inside catch (Exception ex)) — REPLACE with:
NinjaTrader.Code.Output.Process(
    string.Format(
        "[IO_VALIDATION] Path validation failed for operation '{0}': {1} - {2}",
        operation, path, ex.Message),
    PrintTo.OutputTab1);
return null;
```

**Side effects on callers**:
- `EnsureDailySummaryCsv` (L147): already checks `if (validCsvPath == null) return;` ✅
- `EnsureDailySummaryCsv` Task.Run (L173): **must add** null guard before `RetryHelper.ExecuteWithRetry(...)` ⚠️
- `AppendDailySummary` Task.Run (L234): **must add** null guard before `RetryHelper.ExecuteWithRetry(...)` ⚠️
- `WriteComplianceJsonAsync` Task.Run (L997): **must add** null guard before `File.WriteAllText(validPath, ...)` ⚠️
- `WriteSnapshotAtomic` (L70-72): removes SecurityException `catch`+re-throw path; `catch (Exception)` still returns `false` ✅
- `LoadStateSnapshot` (L158,186): removes SecurityException `catch`+re-throw path; `catch (Exception)` returns `null` ✅

**Callers that currently `catch (SecurityException)` and re-throw** (`WriteSnapshotAtomic`, `LoadStateSnapshot`) will need that `catch (SecurityException)` block removed after the fix since the exception can no longer propagate.

---

## 7. Test Requirement

**YES** — existing callers in StickyState have no unit tests for the path-traversal return-null path.

**Stub** (xUnit):
```csharp
// xunit-tests/W9-L6-002/ValidateAndCanonicalize_PathTraversal_ReturnsNull.cs
[Fact]
public void ValidateAndCanonicalize_PathTraversal_ReturnsNull()
{
    // Arrange: a path that resolves outside NinjaTrader 8 sandbox
    string evil = Path.Combine(Path.GetTempPath(), "evil.txt");

    // Act
    string result = PathValidation.ValidateAndCanonicalize(evil, "TestOp");

    // Assert
    Assert.Null(result);
}

[Fact]
public void ValidateAndCanonicalize_ValidPath_ReturnsCanonical()
{
    // Arrange: a path inside sandbox
    string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    string inside = Path.Combine(docs, "NinjaTrader 8", "test.txt");

    // Act
    string result = PathValidation.ValidateAndCanonicalize(inside, "TestOp");

    // Assert
    Assert.NotNull(result);
    Assert.Equal(Path.GetFullPath(inside), result);
}
```

---

## 8. OKF Rule Alignment

| Rule | Requirement | Current | After Fix |
|---|---|---|---|
| Rule 5 — hot-path throw | Non-hot-path throws OK; hot-path must return bool/Result | Throws `SecurityException` (not hot-path but risky) | Returns `null`, logs via `NinjaTrader.Code.Output.Process` |
| Rule 5 — silent catch | Log with method name + `ex.Message` | Callers log correctly | No change needed in callers |
