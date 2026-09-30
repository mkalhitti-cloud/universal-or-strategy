# W9-L6-002 Plan -- Hot-path exception safety fix for SecurityException

**Status**: PLAN (no edits made)
**Finding**: W9-L6-002
**Scan report**: docs/brain/wave9-findings/W9-L6-002/scan.md
**OKF Rule**: Rule 5 (production-engineering-billions.md) -- no exception throws on hot-path-adjacent code
**OKF Rule**: Rule 6 (complexity-reduction.md) -- guard clauses + early returns

---

## Summary

`PathValidation.ValidateAndCanonicalize` currently throws `SecurityException` in two places:
1. Line 75: path-traversal guard (bad canonical path)
2. Line 96: wraps `Path.GetFullPath()` exceptions

Fix strategy: replace both throws with `Output.Process(log) + return null`.
Once null-return is in place, all callers must null-guard the return value before use.
StickyState.cs has two catch blocks that `throw;` security exceptions upward -- those must
become `return false` / `return null` to stop the propagation chain.

---

## File 1: src/V12_002.IO.PathValidation.cs

### Change 1A -- Remove "Fail-Fast: SecurityException thrown" doc comment (line 23)

**Before:**
```
        /// - Fail-Fast: SecurityException thrown on any violation
```

**After:**
```
        /// - Fail-Fast: returns null on any violation, caller must null-guard
```

### Change 1B -- Update ValidateAndCanonicalize XML summary (lines 44, 49)

**Before:**
```
            /// <summary>
            /// Validates and canonicalizes a file path.
            /// Throws SecurityException if path traversal is detected.
            /// </summary>
            /// <param name="path">Path to validate</param>
            /// <param name="operation">Operation name for logging (e.g., "WriteState", "ReadCSV")</param>
            /// <returns>Canonicalized safe path</returns>
            /// <exception cref="SecurityException">Path traversal detected</exception>
```

**After:**
```
            /// <summary>
            /// Validates and canonicalizes a file path.
            /// Returns null if path traversal is detected or path resolution fails.
            /// Caller must null-check the return value before use.
            /// </summary>
            /// <param name="path">Path to validate</param>
            /// <param name="operation">Operation name for logging (e.g., "WriteState", "ReadCSV")</param>
            /// <returns>Canonicalized safe path, or null if validation fails</returns>
```

### Change 1C -- Replace line-75 throw with log + return null

**Before (lines 75-84):**
```csharp
                        throw new SecurityException(
                            string.Format(
                                "[IO_VALIDATION] Path traversal blocked for operation '{0}': {1} (canonical: {2}) is outside allowed base: {3}",
                                operation,
                                path,
                                canonical,
                                _baseDir
                            )
                        );
```

**After:**
```csharp
                        NinjaTrader.Code.Output.Process(
                            "[IO_VALIDATION] Path traversal blocked for operation '"
                                + operation
                                + "': "
                                + path
                                + " (canonical: "
                                + canonical
                                + ") is outside allowed base: "
                                + _baseDir,
                            PrintTo.OutputTab1
                        );
                        return null;
```

### Change 1D -- Remove catch (SecurityException) re-throw block (lines 88-92)

This block only existed to pass line-75's SecurityException through the outer catch.
Once line 75 no longer throws, this catch block is dead code and must be removed.

**Before (lines 88-92):**
```csharp
                catch (SecurityException)
                {
                    // Re-throw security exceptions as-is
                    throw;
                }
```

**After:**
```
(removed entirely)
```

### Change 1E -- Replace line-96 throw with log + return null

**Before (lines 93-105):**
```csharp
                catch (Exception ex)
                {
                    // Wrap other exceptions (e.g., invalid path characters)
                    throw new SecurityException(
                        string.Format(
                            "[IO_VALIDATION] Path validation failed for operation '{0}': {1} - {2}",
                            operation,
                            path,
                            ex.Message
                        ),
                        ex
                    );
                }
```

**After:**
```csharp
                catch (Exception ex)
                {
                    NinjaTrader.Code.Output.Process(
                        "[IO_VALIDATION] Path validation failed for operation '"
                            + operation
                            + "': "
                            + path
                            + " - "
                            + ex.Message,
                        PrintTo.OutputTab1
                    );
                    return null;
                }
```

### Net result -- complete new ValidateAndCanonicalize body

```csharp
            /// <summary>
            /// Validates and canonicalizes a file path.
            /// Returns null if path traversal is detected or path resolution fails.
            /// Caller must null-check the return value before use.
            /// </summary>
            /// <param name="path">Path to validate</param>
            /// <param name="operation">Operation name for logging (e.g., "WriteState", "ReadCSV")</param>
            /// <returns>Canonicalized safe path, or null if validation fails</returns>
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
                        NinjaTrader.Code.Output.Process(
                            "[IO_VALIDATION] Path traversal blocked for operation '"
                                + operation
                                + "': "
                                + path
                                + " (canonical: "
                                + canonical
                                + ") is outside allowed base: "
                                + _baseDir,
                            PrintTo.OutputTab1
                        );
                        return null;
                    }

                    return canonical;
                }
                catch (Exception ex)
                {
                    NinjaTrader.Code.Output.Process(
                        "[IO_VALIDATION] Path validation failed for operation '"
                            + operation
                            + "': "
                            + path
                            + " - "
                            + ex.Message,
                        PrintTo.OutputTab1
                    );
                    return null;
                }
            }
```

Also update the class-level doc comment on line 23:

**Before:**
```
        /// - Fail-Fast: SecurityException thrown on any violation
```
**After:**
```
        /// - Fail-Fast: returns null on any violation, caller must null-guard
```

---

## File 2: src/V12_002.UI.Compliance.cs

### Existing null-guards (no change needed)

- **Line 147-149**: `validCsvPath` already has `if (validCsvPath == null) return;` -- OK

### Change 2A -- Add null-guard after line 173 (WriteCSV header Task.Run)

**Before (lines 173-180):**
```csharp
                    // EPIC-7-QUALITY-010: Validate path before write
                    string validPath = PathValidation.ValidateAndCanonicalize(_csvPath, "WriteCSV");

                    // EPIC-7-QUALITY-011: Retry logic for transient I/O failures
                    RetryHelper.ExecuteWithRetry(
                        () => System.IO.File.WriteAllText(validPath, _csvHeader + Environment.NewLine),
                        RetryHelper.IsTransientIOError,
                        "WriteCSVHeader"
                    );
```

**After:**
```csharp
                    // EPIC-7-QUALITY-010: Validate path before write
                    string validPath = PathValidation.ValidateAndCanonicalize(_csvPath, "WriteCSV");
                    if (validPath == null)
                        return;

                    // EPIC-7-QUALITY-011: Retry logic for transient I/O failures
                    RetryHelper.ExecuteWithRetry(
                        () => System.IO.File.WriteAllText(validPath, _csvHeader + Environment.NewLine),
                        RetryHelper.IsTransientIOError,
                        "WriteCSVHeader"
                    );
```

### Change 2B -- Remove now-dead catch (SecurityException) at line 182-186

Once ValidateAndCanonicalize never throws, the `catch (SecurityException)` in the WriteCSV
Task.Run block is dead. Remove it.

**Before (lines 182-186):**
```csharp
                catch (SecurityException ex)
                {
                    Print(string.Format("[IO_SECURITY] {0}", ex.Message));
                    // P0-3 FIX: Do NOT reset flag - prevents unbounded Task.Run spawn on persistent errors
                }
```

**After:**
```
(removed entirely -- the outer catch (Exception) on line 187 is sufficient)
```

### Change 2C -- Add null-guard after line 234 (AppendCSV Task.Run)

**Before (lines 234-241):**
```csharp
                    // EPIC-7-QUALITY-010: Validate path before append
                    string validPath = PathValidation.ValidateAndCanonicalize(pathCopy, "AppendCSV");

                    // EPIC-7-QUALITY-011: Retry logic for transient I/O failures
                    RetryHelper.ExecuteWithRetry(
                        () => System.IO.File.AppendAllText(validPath, lineCopy),
                        RetryHelper.IsTransientIOError,
                        "AppendCSVLine"
                    );
```

**After:**
```csharp
                    // EPIC-7-QUALITY-010: Validate path before append
                    string validPath = PathValidation.ValidateAndCanonicalize(pathCopy, "AppendCSV");
                    if (validPath == null)
                        return;

                    // EPIC-7-QUALITY-011: Retry logic for transient I/O failures
                    RetryHelper.ExecuteWithRetry(
                        () => System.IO.File.AppendAllText(validPath, lineCopy),
                        RetryHelper.IsTransientIOError,
                        "AppendCSVLine"
                    );
```

### Change 2D -- Add null-guard after line 1000 (WriteComplianceJsonAsync)

**Before (lines 997-1001):**
```csharp
                    if (path != null)
                    {
                        // EPIC-7-QUALITY-010: Validate compliance log path
                        string validPath = PathValidation.ValidateAndCanonicalize(path, "WriteComplianceLog");
                        System.IO.File.WriteAllText(validPath, jsonPayload);
                    }
```

**After:**
```csharp
                    if (path != null)
                    {
                        // EPIC-7-QUALITY-010: Validate compliance log path
                        string validPath = PathValidation.ValidateAndCanonicalize(path, "WriteComplianceLog");
                        if (validPath == null)
                            return;
                        System.IO.File.WriteAllText(validPath, jsonPayload);
                    }
```

### Change 2E -- Remove now-dead catch (SecurityException) at line 1004-1007

**Before (lines 1004-1007):**
```csharp
                catch (SecurityException ex)
                {
                    Print(string.Format("[IO_SECURITY] {0}", ex.Message));
                }
```

**After:**
```
(removed entirely -- catch block is dead once ValidateAndCanonicalize never throws)
```

---

## File 3: src/V12_002.StickyState.cs

### Change 3A -- Replace `throw;` at line 120 with `return false`

This is inside `TrySaveSnapshot` (bool return). The method's callers expect false on failure.

**Before (lines 115-121):**
```csharp
            catch (SecurityException ex)
            {
                // EPIC-7-QUALITY-010: Log security violations
                TrackStateSecurityViolation();
                Print(string.Format("[IO_SECURITY] {0}", ex.Message));
                throw; // Re-throw to fail-fast
            }
```

**After:**
```csharp
            catch (SecurityException ex)
            {
                // EPIC-7-QUALITY-010: Log security violations
                TrackStateSecurityViolation();
                Print(string.Format("[IO_SECURITY] {0}", ex.Message));
                return false;
            }
```

### Change 3B -- Replace `throw;` at line 210 with `return null`

This is inside `TryLoadSnapshot` (returns a snapshot object or null). Propagating the
exception upward would crash the caller; `return null` is the correct failure sentinel.

**Before (lines 205-211):**
```csharp
            catch (SecurityException ex)
            {
                // EPIC-7-QUALITY-010: Log security violations
                TrackStateSecurityViolation();
                Print(string.Format("[IO_SECURITY] {0}", ex.Message));
                throw; // Re-throw to fail-fast
            }
```

**After:**
```csharp
            catch (SecurityException ex)
            {
                // EPIC-7-QUALITY-010: Log security violations
                TrackStateSecurityViolation();
                Print(string.Format("[IO_SECURITY] {0}", ex.Message));
                return null;
            }
```

### Change 3C -- Line 311 (Rollback) already returns false -- NO CHANGE NEEDED

```csharp
            catch (SecurityException ex)
            {
                TrackStateSecurityViolation();
                Print(string.Format("[STICKY] Rollback security violation: {0}", ex.Message));
                return false;  // already correct
            }
```

---

## Exit gate checklist

- [x] Exact before/after diff for line 75 SecurityException throw (Change 1C)
- [x] Exact before/after diff for line 96 SecurityException throw (Change 1E)
- [x] Diff to remove now-dead `catch (SecurityException) { throw; }` at line 88 (Change 1D)
- [x] Diff to update class-level + method-level doc comments (Changes 1A, 1B)
- [x] Before/after diffs for Compliance.cs null-guards (Changes 2A, 2C, 2D)
- [x] Before/after diffs for Compliance.cs dead SecurityException catch removal (Changes 2B, 2E)
- [x] Before/after diffs for StickyState re-throws replaced with return (Changes 3A, 3B)
- [x] Line 311 StickyState already returns false -- confirmed no change needed
- [x] No edits made -- plan only
- [x] Plan written to: docs/brain/wave9-findings/W9-L6-002/plan.md

---

## Execution order (for Phase 5 worker)

1. Edit `src/V12_002.IO.PathValidation.cs` (all 1A-1E changes in one apply_diff call)
2. Edit `src/V12_002.UI.Compliance.cs` (all 2A-2E changes in one apply_diff call)
3. Edit `src/V12_002.StickyState.cs` (changes 3A + 3B in one apply_diff call)
4. Run `dotnet build` -- verify zero errors
5. Run `grep -r "throw new SecurityException" src/` -- must return 0 results
6. Run `grep -r "catch (SecurityException)" src/` -- verify only expected legitimate catches remain
