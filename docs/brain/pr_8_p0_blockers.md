# PR #8 P0 CRITICAL BLOCKERS

**Status**: NOT SAFE TO MERGE (Greptile Confidence 1/5)
**Branch**: epic-7-quality-phase2-security
**Commit**: c642c60f

## Summary

PR #8 claims to fix 8 blockers from PR #7 audit, but **3 P0 issues remain**:
1. Build-breaking duplicate definitions (CS0111/CS0102)
2. Thread.Sleep still present (claimed fixed but unchanged)
3. Unbounded spawn flag reset still present (claimed fixed but unchanged)

---

## P0-1: CS0111/CS0102 Duplicate Definitions [BUILD BREAKING]

**Severity**: P0 (Compile Error)
**Files**: 
- `src/V12_002.Data.cs` lines 57-77
- `src/V12_002.Telemetry.cs` (same methods)

**Issue**:
Three methods defined in BOTH files (same partial class V12_002):
```csharp
// In BOTH Data.cs AND Telemetry.cs:
private void TrackStatePersistenceFailure()
private void TrackStateSecurityViolation()
private void TrackStateRollback()
```

Plus fields declared in both:
```csharp
private long _statePersistenceFailures = 0;
private long _stateSecurityViolations = 0;
```

**Semantic Mismatch**:
- Data.cs: `TrackStateRollback()` increments `_statePersistenceFailures`
- Telemetry.cs: `TrackStateRollback()` increments `_stateRollbacksExecuted`

**Fix**:
```diff
--- src/V12_002.Data.cs
+++ src/V12_002.Data.cs
@@ -14,8 +14,6 @@
 
         // V12.EPIC-7-QUALITY-007: State Persistence Error Handling Diagnostic Counters
-        private long _statePersistenceFailures = 0;
-        private long _stateSecurityViolations = 0;
         private long _stateCorruptionDetected = 0;
         private long _stateTempCleanupFailures = 0;
 
@@ -52,27 +50,6 @@
         }
 
-        // V12.EPIC-7-QUALITY-007: Helper methods for state persistence telemetry
-        /// <summary>
-        /// Tracks a state persistence failure (write/read/rollback failure).
-        /// </summary>
-        private void TrackStatePersistenceFailure()
-        {
-            Interlocked.Increment(ref _statePersistenceFailures);
-        }
-
-        /// <summary>
-        /// Tracks a state security violation (path traversal, unauthorized access).
-        /// </summary>
-        private void TrackStateSecurityViolation()
-        {
-            Interlocked.Increment(ref _stateSecurityViolations);
-        }
-
-        /// <summary>
-        /// Tracks a state rollback event (corruption detected, backup restored).
-        /// Rollback events are counted as persistence failures.
-        /// </summary>
-        private void TrackStateRollback()
-        {
-            Interlocked.Increment(ref _statePersistenceFailures);
-        }
-
         // Placeholder for missing Data logic.
```

**Verification**: Canonical implementations belong in Telemetry.cs only.

---

## P0-2: Thread.Sleep Still Present in RetryHelper

**Severity**: P0 (Jane Street Fail-Fast Violation)
**File**: `src/V12_002.IO.RetryHelper.cs` line 87
**PR Claim**: "P0-2: Remove Thread.Sleep retry loop in RetryHelper (Jane Street fail-fast)"
**Reality**: Thread.Sleep(delayMs) UNCHANGED from base branch

**Issue**:
```csharp
// Line 87 in RetryHelper.cs - STILL PRESENT
Thread.Sleep(delayMs);
```

Blocks bar-event thread up to 350ms (exponential backoff: 50ms → 100ms → 200ms).

**Fix**: Remove exponential backoff entirely, implement fail-fast:
```csharp
// BEFORE (current):
for (int attempt = 1; attempt <= maxAttempts; attempt++)
{
    try { return operation(); }
    catch (Exception ex) when (isRetryable(ex) && attempt < maxAttempts)
    {
        int delayMs = baseDelayMs * (1 << (attempt - 1));
        Thread.Sleep(delayMs); // ← BLOCKING
    }
}

// AFTER (fail-fast):
try 
{ 
    return operation(); 
}
catch (Exception ex)
{
    TrackIoRetryFailure();
    throw; // Fail immediately
}
```

**Jane Street Alignment**: No retry loops on hot path. Fail fast, propagate errors immediately.

---

## P0-3: Unbounded Task.Run Spawn Still Possible

**Severity**: P0 (Resource Exhaustion Risk)
**File**: `src/V12_002.UI.Compliance.cs` lines 179, 183
**PR Claim**: "P1-4: Removed flag reset on failure (lines 179, 183)"
**Reality**: Flag reset calls UNCHANGED from base branch

**Issue**:
```csharp
// Lines 179, 183 - STILL PRESENT
catch (SecurityException ex)
{
    Interlocked.Exchange(ref _csvHeaderCreated, 0); // ← RESETS FLAG
    // ...
}
catch (Exception ex)
{
    Interlocked.Exchange(ref _csvHeaderCreated, 0); // ← RESETS FLAG
    // ...
}
```

On persistent SecurityException (e.g., invalid path), every call to `AppendDailySummary()` spawns a fresh `Task.Run` via `EnsureDailySummaryCsv()`.

**Fix**: Remove both flag reset calls:
```diff
--- src/V12_002.UI.Compliance.cs
+++ src/V12_002.UI.Compliance.cs
@@ -176,7 +176,6 @@
             catch (SecurityException ex)
             {
-                Interlocked.Exchange(ref _csvHeaderCreated, 0);
                 Print($"[UI_COMPLIANCE] Security violation creating daily summary CSV: {ex.Message}");
             }
@@ -180,7 +179,6 @@
             catch (Exception ex)
             {
-                Interlocked.Exchange(ref _csvHeaderCreated, 0);
                 Print($"[UI_COMPLIANCE] Failed to create daily summary CSV: {ex.Message}");
             }
```

**Verification**: Flag must remain set (=1) after failure to prevent retry attempts.

---

## PROTOCOL HARDENING

**Root Cause**: PR Loop V2 Step 1 (Bot Forensics) did NOT extract inline Greptile comments.

**Gap**: `scripts/extract_pr_forensics.ps1` only reads top-level bot comments, not inline code review comments.

**Fix Required**: Update extraction script to parse inline comments from:
- Greptile (inline code reviews)
- CodeRabbit (inline suggestions)
- Sourcery (inline refactoring hints)

**New Script**: `scripts/extract_inline_comments.ps1`

---

## NEXT STEPS

1. Apply P0-1 fix (remove duplicates from Data.cs)
2. Apply P0-2 fix (remove Thread.Sleep from RetryHelper)
3. Apply P0-3 fix (remove flag resets from UI.Compliance)
4. Run local validation: `powershell -File .\scripts\pre_push_validation.ps1`
5. Push and re-run PR Loop Step 3 (Global Push & Monitor)

**Target**: Greptile Confidence ≥4/5 before merge