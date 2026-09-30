# PR #10 Round 5 Fix Queue

**Date**: 2026-06-01  
**Commit**: a4d0384  
**Branch**: feature/src-phase7-new3-dispatch  
**Target PHS**: 100/100

---

## Priority Queue

### P1 CRITICAL: Fix Race Condition in Stop-Resize Logic

**File**: [`V12_002.Orders.Management.StopSync.cs:343-379`](../../src/V12_002.Orders.Management.StopSync.cs:343-379)

**Issue**: `UpdateStopQuantity_HandleStalePending` returns `false` in two distinct cases:
1. Fresh pending (age < threshold) → Update quantity, early exit ✅ CORRECT
2. TryRemove race (age > threshold, removal failed) → Should retry, NOT abort ❌ WRONG

**Impact**: Position's stop quantity left unsynced when concurrent purge occurs, leaving position over-exposed.

**Root Cause**: Extraction collapsed two control flows into single boolean return.

**Jane Street Violation**: Fail-Fast Isolation - race condition silently swallowed.

---

## Recommended Fix: Option A (Enum-Based)

**Why Enum?**
- Type-safe: Compiler enforces exhaustive handling
- Self-documenting: `StaleCheckResult.RaceLost` is clearer than `false`
- Aligns with V12 DNA: "Make illegal states unrepresentable"

### Step 1: Define Enum

**Location**: Before `UpdateStopQuantity_HandleStalePending` (line ~342)

```csharp
/// <summary>
/// [Phase 7 NEW-2] Result of stale pending check
/// </summary>
private enum StaleCheckResult
{
    /// <summary>Pending is fresh (age < threshold), quantity updated, early exit required</summary>
    Fresh,
    
    /// <summary>Stale pending successfully purged, re-initiate stop resize</summary>
    Purged,
    
    /// <summary>TryRemove failed (race condition), retry re-initiate</summary>
    RaceLost
}
```

### Step 2: Update Helper Signature

**Change**:
```csharp
// OLD:
private bool UpdateStopQuantity_HandleStalePending(...)

// NEW:
private StaleCheckResult UpdateStopQuantity_HandleStalePending(...)
```

### Step 3: Update Helper Body

**Lines 343-379** - Replace entire function:

```csharp
/// <summary>
/// [Phase 7 NEW-2] Helper: Handle stale pending replacement detection and purge
/// Extracted from UpdateStopQuantity to reduce complexity (CYC 25->15)
/// </summary>
/// <returns>StaleCheckResult indicating whether to re-initiate, early exit, or retry</returns>
private StaleCheckResult UpdateStopQuantity_HandleStalePending(
    string entryName,
    PendingStopReplacement existingPendingQty,
    int remainingContracts
)
{
    // Build 1104.2: Staleness fast-path -- purge stale pending and re-initiate
    double pendingAgeSeconds = (DateTime.UtcNow - existingPendingQty.CreatedTime).TotalSeconds;
    if (pendingAgeSeconds > STALE_PENDING_FAST_PATH_SEC)
    {
        if (pendingStopReplacements.TryRemove(entryName, out _))
        {
            Interlocked.Decrement(ref pendingReplacementCount);
            Print(
                string.Format(
                    "[1104.2] Stale pending purged for {0} ({1:F1}s). Re-initiating stop resize.",
                    entryName,
                    pendingAgeSeconds
                )
            );
            return StaleCheckResult.Purged; // Stale removed, re-initiate
        }
        
        // TryRemove failed (race condition) - another thread purged it
        Print(
            string.Format(
                "[1104.2] TryRemove race detected for {0}. Retrying stop resize.",
                entryName
            )
        );
        return StaleCheckResult.RaceLost; // Retry re-initiate
    }
    else
    {
        existingPendingQty.Quantity = remainingContracts;
        Print(
            string.Format(
                "V8.31: Updated existing pending replacement for {0} to {1} contracts",
                entryName,
                remainingContracts
            )
        );
        return StaleCheckResult.Fresh; // Signal early return
    }
}
```

### Step 4: Update Caller

**Lines 542-550** - Replace:

```csharp
// OLD:
bool shouldReInitiate = UpdateStopQuantity_HandleStalePending(
    entryName,
    existingPendingQty,
    pos.RemainingContracts
);
if (!shouldReInitiate)
{
    return;
}

// NEW:
var staleResult = UpdateStopQuantity_HandleStalePending(
    entryName,
    existingPendingQty,
    pos.RemainingContracts
);
if (staleResult == StaleCheckResult.Fresh)
{
    return; // Early exit - pending is fresh, quantity updated
}
// Both Purged and RaceLost fall through to create new replacement
```

---

## Verification Steps

### 1. Build Check
```powershell
dotnet build src/V12_002.csproj
```
**Expected**: Zero errors

### 2. Complexity Audit
```powershell
python scripts/complexity_audit.py
```
**Expected**: `UpdateStopQuantity` remains at CYC ≤ 15

### 3. Race Condition Test (Manual)
- Set breakpoint at line 353 (`if (pendingStopReplacements.TryRemove(...)`)
- Simulate concurrent purge by manually removing entry from another thread
- Verify `StaleCheckResult.RaceLost` path executes
- Verify new replacement is created (does NOT abort)

### 4. Pre-Push Validation
```powershell
powershell -File .\scripts\pre_push_validation.ps1 -Fast
```
**Expected**: All checks pass

### 5. PR Loop
```powershell
droid /pr-loop 10
```
**Expected**: PHS 100/100 (all bots pass)

---

## Alternative: Option B (Out Parameter)

**If enum approach is rejected**, use out parameter:

```csharp
private bool UpdateStopQuantity_HandleStalePending(
    string entryName,
    PendingStopReplacement existingPendingQty,
    int remainingContracts,
    out bool shouldRetry
)
{
    shouldRetry = false;
    double pendingAgeSeconds = (DateTime.UtcNow - existingPendingQty.CreatedTime).TotalSeconds;
    if (pendingAgeSeconds > STALE_PENDING_FAST_PATH_SEC)
    {
        if (pendingStopReplacements.TryRemove(entryName, out _))
        {
            Interlocked.Decrement(ref pendingReplacementCount);
            Print("[1104.2] Stale pending purged. Re-initiating stop resize.");
            return true; // Stale removed, re-initiate
        }
        shouldRetry = true; // TryRemove failed, retry
        Print("[1104.2] TryRemove race detected. Retrying...");
        return true; // Re-initiate (retry)
    }
    else
    {
        existingPendingQty.Quantity = remainingContracts;
        Print("V8.31: Updated existing pending replacement");
        return false; // Signal early return
    }
}

// Caller:
bool shouldReInitiate = UpdateStopQuantity_HandleStalePending(
    entryName,
    existingPendingQty,
    pos.RemainingContracts,
    out bool shouldRetry
);
if (!shouldReInitiate)
{
    return; // Early exit
}
// Fall through to create new replacement
```

**Drawback**: Less type-safe, `shouldRetry` parameter is redundant (always `true` when `shouldReInitiate` is `true`).

---

## Diff Size Estimate

**Lines Changed**: ~30 (enum definition + helper body + caller)  
**Files Modified**: 1 (`V12_002.Orders.Management.StopSync.cs`)  
**Complexity Impact**: None (CYC unchanged)  
**Risk**: Low (surgical fix, no new logic)

---

## Jane Street Alignment

✅ **Fail-Fast Isolation**: Race condition now retried instead of silently swallowed  
✅ **Make Illegal States Unrepresentable**: Enum prevents "boolean blindness"  
✅ **Cognitive Simplicity**: Three-value enum is clearer than boolean + out parameter  

---

## Status

**Ready for Round 6 (Local Repair)**  
**Delegate to**: Bob CLI (`v12-engineer`)  
**Expected Duration**: 10 minutes (surgical fix)  
**Expected PHS After Fix**: 100/100

---

**Last Updated**: 2026-06-01  
**Next**: Round 6 - Implement enum-based fix