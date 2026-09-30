# W9-L6-001 Scan Report

| Field         | Value |
|---------------|-------|
| **W9_ID**     | W9-L6-001 |
| **File**      | `src/V12_002.IO.PathValidation.cs` |
| **Line**      | 56 (confirmed) |
| **Violation** | `throw new ArgumentException` — potential throw on hot path |
| **OKF Rule**  | Rule 5 — hot-path throw: wrap in try/catch returning bool/Result |
| **Status**    | **CONFIRMED** (violation present; reachability is NUANCED — see analysis) |

---

## 1. Violation Confirmed

The `throw new ArgumentException` at **line 56** is present and unmodified.

```csharp
// src/V12_002.IO.PathValidation.cs : lines 53-59
if (string.IsNullOrWhiteSpace(path))
{
    throw new ArgumentException(                                           // <-- LINE 56
        string.Format("[IO_VALIDATION] Path cannot be null/empty for operation: {0}", operation)
    );
}
```

**Exception type thrown:** `System.ArgumentException`

---

## 2. Full Body of `ValidateAndCanonicalize`

File: [`src/V12_002.IO.PathValidation.cs`](src/V12_002.IO.PathValidation.cs:51)

```csharp
public static string ValidateAndCanonicalize(string path, string operation)
{
    // Guard: Null/empty check
    if (string.IsNullOrWhiteSpace(path))
    {
        throw new ArgumentException(                                       // LINE 56
            string.Format("[IO_VALIDATION] Path cannot be null/empty for operation: {0}", operation)
        );
    }

    try
    {
        // Canonicalize: Resolve .., symlinks, and relative paths
        string canonical = Path.GetFullPath(path);

        // Security check: Ensure path stays within NinjaTrader 8 directory
        if (
            !canonical.StartsWith(_baseDirWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !canonical.Equals(_baseDir, StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new SecurityException(                                   // LINE 74
                string.Format(
                    "[IO_VALIDATION] Path traversal blocked for operation '{0}': {1} (canonical: {2}) is outside allowed base: {3}",
                    operation, path, canonical, _baseDir
                )
            );
        }

        return canonical;
    }
    catch (SecurityException)
    {
        throw;  // Re-throw security exceptions as-is
    }
    catch (Exception ex)
    {
        throw new SecurityException(                                       // LINE 95
            string.Format(
                "[IO_VALIDATION] Path validation failed for operation '{0}': {1} - {2}",
                operation, path, ex.Message
            ),
            ex
        );
    }
}
```

---

## 3. All Call Sites of `ValidateAndCanonicalize`

| File | Line | Operation String | Context |
|------|------|-----------------|---------|
| `V12_002.UI.Compliance.cs` | 147 | `"CheckCSV"` | `EnsureDailySummaryCsv` — strategy thread, first bar only |
| `V12_002.UI.Compliance.cs` | 171 | `"WriteCSV"` | `EnsureDailySummaryCsv` — inside `Task.Run` (off-thread) |
| `V12_002.UI.Compliance.cs` | 232 | `"AppendCSV"` | `AppendDailySummary` — inside `Task.Run` (off-thread) |
| `V12_002.UI.Compliance.cs` | 993 | `"WriteComplianceLog"` | Compliance log write — inside `Task.Run` or similar |
| `V12_002.StickyState.cs` | 70-72 | `"WriteState"`, `"WriteTempState"`, `"WriteBackupState"` | Sticky state write — off hot path |
| `V12_002.StickyState.cs` | 158 | `"ReadState"` | State load on startup |
| `V12_002.StickyState.cs` | 186 | various | Rollback path |
| `V12_002.StickyState.cs` | 265, 294 | `"ReadBackup"`, `"RollbackWrite"` | Recovery path |
| `V12_002.IO.PathValidation.cs` | 116 | (forwarded) | `ValidateDirectoryPath` — passthrough |

---

## 4. Caller Bodies (Hot-Path Chain)

### 4a. `EnsureDailySummaryCsv` — [`src/V12_002.UI.Compliance.cs:137`](src/V12_002.UI.Compliance.cs:137)

```csharp
private void EnsureDailySummaryCsv()
{
    if (string.IsNullOrEmpty(dailySummaryCsvPath))   // <-- GUARD: returns before calling ValidateAndCanonicalize
        return;
    if (Volatile.Read(ref _csvHeaderCreated) != 0)   // <-- GUARD: one-shot flag, early return after first call
        return;

    try
    {
        // LINE 147: ValidateAndCanonicalize called here — ONLY on first bar
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
        return;  // <-- SecurityException caught and printed; does NOT propagate
    }

    if (Interlocked.CompareExchange(ref _csvHeaderCreated, 1, 0) != 0)
        return;

    string _csvPath = dailySummaryCsvPath;
    Task.Run(() =>
    {
        try
        {
            // LINE 171: ValidateAndCanonicalize called inside Task.Run (off strategy thread)
            string validPath = PathValidation.ValidateAndCanonicalize(_csvPath, "WriteCSV");
            RetryHelper.ExecuteWithRetry(...);
        }
        catch (SecurityException ex) { Print(...); }
        catch (Exception ex) { Print(...); }
    });
}
```

**Key observation**: `dailySummaryCsvPath` is null-checked at line 139 before calling `ValidateAndCanonicalize`.
Therefore the `throw new ArgumentException` at line 56 **cannot be triggered** via `EnsureDailySummaryCsv`.

### 4b. `TrackTradeEntry` — [`src/V12_002.UI.Compliance.cs:71`](src/V12_002.UI.Compliance.cs:71)

```csharp
private void TrackTradeEntry(Account acct, Execution execution)
{
    if (!IsValidTradeExecution(acct, execution))
        return;
    if (execution.Order.OrderState != OrderState.Filled)
        return;

    OrderAction action = execution.Order.OrderAction;
    if (action != OrderAction.Buy && action != OrderAction.SellShort)
        return;

    if (EnableSIMA && !IsFleetAccount(acct))
        return;

    DateTime nowInZone = GetComplianceNow();
    EnsureAccountComplianceTracking(acct.Name, nowInZone);

    accountTradeCount.AddOrUpdate(acct.Name, 1, (k, v) => v + 1);
    accountDailyTradeCount.AddOrUpdate(acct.Name, 1, (k, v) => v + 1);

    int dayKey = GetTradingDayKey(nowInZone);
    var days = accountTradingDays.GetOrAdd(acct.Name, _ => new ConcurrentDictionary<int, byte>());
    days.TryAdd(dayKey, 1);
}
```

**Key observation**: `TrackTradeEntry` does **NOT** call `ValidateAndCanonicalize`. No throw risk.

### 4c. `AppendDailySummary` — [`src/V12_002.UI.Compliance.cs:193`](src/V12_002.UI.Compliance.cs:193)

```csharp
private void AppendDailySummary(DateTime summaryDate, string accountName, ...)
{
    if (string.IsNullOrEmpty(dailySummaryCsvPath))   // <-- GUARD
        return;

    // ... build line string ...

    EnsureDailySummaryCsv();   // one-shot flag ensures header exists

    string pathCopy = dailySummaryCsvPath;
    Task.Run(() =>
    {
        try
        {
            // LINE 232: ValidateAndCanonicalize inside Task.Run (off strategy thread)
            string validPath = PathValidation.ValidateAndCanonicalize(pathCopy, "AppendCSV");
            RetryHelper.ExecuteWithRetry(...);
        }
        catch
        { /* swallow -- daily summary is best-effort */ }
    });
}
```

**Key observation**: `ValidateAndCanonicalize` at line 232 runs inside `Task.Run` — **off the strategy thread**, not on the hot path. Even if it threw, the silent catch swallows it.

---

## 5. Hot-Path Call Chain

```
OnBarUpdate (BarUpdate.cs:257)
  └─> MaybeRunDailySummary (BarUpdate.cs:64)
        └─> MaybeFinalizeDailySummaries (Compliance.cs:271)  [throttled: 30-second guard]
              └─> FinalizeDailySummaryForAccount (Compliance.cs:247)
                    └─> AppendDailySummary (Compliance.cs:193)
                          └─> EnsureDailySummaryCsv (Compliance.cs:137)     [one-shot: _csvHeaderCreated flag]
                                └─> ValidateAndCanonicalize (PathValidation.cs:147)
                                      └─> THROW at line 56 IF path null/empty

OnExecutionUpdate (Callbacks.Execution.cs:244)
  └─> Enqueue(ProcessOnExecutionUpdate)
        └─> ProcessComplianceTracking (Callbacks.Execution.cs:588)
              └─> TrackTradeEntry (Compliance.cs:71)   <-- does NOT call ValidateAndCanonicalize
```

---

## 6. Hot-Path Classification

| Classifier | Result | Reasoning |
|------------|--------|-----------|
| **Reachable from OnBarUpdate?** | YES (technically) | Via `MaybeRunDailySummary` → `MaybeFinalizeDailySummaries` → `AppendDailySummary` → `EnsureDailySummaryCsv` |
| **Reachable from OnExecutionUpdate?** | NO | `TrackTradeEntry` has no I/O path |
| **Is line 56 actually reachable?** | **NO** in practice | Every caller null-checks `dailySummaryCsvPath` before calling `ValidateAndCanonicalize` |
| **Can line 56 throw on strategy thread?** | NO | `EnsureDailySummaryCsv:139` guards with `string.IsNullOrEmpty(dailySummaryCsvPath)` before calling `ValidateAndCanonicalize` |
| **Hot-path throw verdict** | **LOW — practically unreachable** | Path is pre-validated by all callers; `ArgumentException` guard at line 56 is defensive dead code from current callers' perspective |

**OKF Rule 5 technical classification**: The throw at line 56 is a **guard clause**, not a hot-path throw in the traditional sense. The method itself is a security utility. However, since the method is callable from the strategy thread (via `EnsureDailySummaryCsv` on first bar), the **theoretical** violation stands — any future caller that passes a null/empty path from the hot path would throw uncaught.

---

## 7. Blast Radius

Files that call `ValidateAndCanonicalize` and would be affected by a signature change:

| File | Call Sites | Affected? |
|------|-----------|-----------|
| `src/V12_002.UI.Compliance.cs` | Lines 147, 171, 232, 993 | YES |
| `src/V12_002.StickyState.cs` | Lines 70, 71, 72, 158, 186, 265, 294 | YES |
| `src/V12_002.IO.PathValidation.cs` | Line 116 (ValidateDirectoryPath → passthrough) | YES |

Total: **3 files, 11 call sites**.

---

## 8. NT8 API Context

Not applicable. `ValidateAndCanonicalize` is a pure utility (no NT8 API calls). The `Path.GetFullPath` and `string.IsNullOrWhiteSpace` are BCL-only.

---

## 9. Recommended Fix (Minimal)

**Strategy**: Change the method signature to return `bool`/`Result` instead of throwing, OR change the null/empty guard to return `null` (and let callers handle).

Per OKF Rule 5 for hot-path throw:
> wrap in try/catch returning bool/Result. Non-hot-path throws OK.

However, since the `ArgumentException` at line 56 is **already unreachable** from all current callers (all callers pre-validate the path), the **minimal fix** is:

**Option A (preferred — minimal, no API change):**
Replace the `throw new ArgumentException` with a `return null` and update callers to null-check. This eliminates the throw entirely.

```csharp
// BEFORE (line 53-59):
if (string.IsNullOrWhiteSpace(path))
{
    throw new ArgumentException(
        string.Format("[IO_VALIDATION] Path cannot be null/empty for operation: {0}", operation)
    );
}

// AFTER:
if (string.IsNullOrWhiteSpace(path))
    return null;
```

Callers that receive `null` already have null-check patterns (SecurityException catch). `EnsureDailySummaryCsv` at line 147 would then need:
```csharp
string validCsvPath = PathValidation.ValidateAndCanonicalize(dailySummaryCsvPath, "CheckCSV");
if (validCsvPath == null) return;
```

**Option B (alternative — bool result pattern):**
Change return type to `(bool success, string path)` tuple. More invasive — 11 call sites to update.

**Recommendation: Option A** — null return with caller null-guard. Minimal diff, 1 line changed in PathValidation + 1 guard per caller site that reaches line 147 on strategy thread (only `EnsureDailySummaryCsv:147`).

---

## 10. Test Requirement

**NO** — no new test needed. The fix is a guard clause change from throw to null-return. Existing callers already handle the null/empty path before calling. The `SecurityException` path (lines 74-103) remains unchanged and is tested by the path traversal scenario.

If Option A is adopted, a single xUnit test verifying that `ValidateAndCanonicalize(null, "test")` returns `null` (instead of throwing) would serve as a regression guard.

---

## 11. Summary

| Item | Finding |
|------|---------|
| Violation present | **YES** — `throw new ArgumentException` at line 56 |
| Violation reachable from hot path | **NO** — all strategy-thread callers null-check path before calling |
| Callers that would propagate the throw | None currently — all callers have pre-guards |
| Exception type | `System.ArgumentException` |
| Fix complexity | **Minimal** — 1-line change in PathValidation + null-guard in EnsureDailySummaryCsv:147 |
| Blast radius | 3 files, 11 call sites (but only 1 on strategy thread: Compliance.cs:147) |
| Test required | Optional regression test for null-return behavior |
