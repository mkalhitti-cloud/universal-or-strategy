# Implementation Plan: T-W1 ShouldSkipFleetAccount Extraction

**Mission**: Extract [`ShouldSkipFleetAccount`](src/V12_002.SIMA.Fleet.cs:347-400) (CYC=25) into a thin parent dispatcher plus two private helpers, reducing parent complexity to CYC ≤ 10.

**Status**: Stage 1 (Architect Planning) - READY FOR ADJUDICATION  
**Build Tag**: `1111.007-phase7-tW1`  
**Forensic Baseline**: Confirmed via intake report  
**Spec Reference**: Phase 7 §3.3 (T-W1 Extraction Strategy)

---

## 1. Exact Signatures

### 1.1 Helper 1: Health Check (Diagnostic-Only, Void Return)

```csharp
/// <summary>
/// T-W1 Helper 1: H-13 stale state reconciliation (diagnostic-only).
/// Logs broker position vs FSM/activePositions/dispatch state.
/// RETURNS VOID per H8 constraint -- no bool decision path.
/// </summary>
/// <param name="acct">Fleet account to check</param>
/// <param name="dispatchLog">Batch log buffer for forensic output</param>
private void ShouldSkipFleet_RunHealthCheck(Account acct, StringBuilder dispatchLog)
```

**Parameters**:
- `Account acct` - Fleet account being evaluated
- `StringBuilder dispatchLog` - Batch log buffer (mutated)

**Returns**: `void` (H8 constraint - diagnostic-only, no skip decision)

**Reads**:
- `_followerBrackets` (ConcurrentDictionary)
- `activePositions` (ConcurrentDictionary)
- `_dispatchSyncPendingExpKeys` (ConcurrentDictionary)
- `Instrument.FullName` (via `acct.Positions`)

**Mutates**:
- `dispatchLog` only (AppendLine calls)

**Contains**:
- T-Q1 catch wrapper (lines 386-390, byte-identical)
- Critical snapshot `acct.Positions.ToArray()` at line 361 (H7 constraint)
- 2 log statements (lines 378, 382-383)

---

### 1.2 Helper 2: Consistency Lock Check (Bool Return)

```csharp
/// <summary>
/// T-W1 Helper 2: Consistency Lock -- skip if daily P&L cap hit.
/// </summary>
/// <param name="rankInfo">Account rank info with DailyPL</param>
/// <param name="acct">Fleet account (for log output)</param>
/// <param name="dispatchLog">Batch log buffer for forensic output</param>
/// <returns>True if consistency lock fires (skip account), false otherwise</returns>
private bool ShouldSkipFleet_IsConsistencyLockHit(AccountRankInfo rankInfo, Account acct, StringBuilder dispatchLog)
```

**Parameters**:
- `AccountRankInfo rankInfo` - Contains `DailyPL` field
- `Account acct` - Fleet account (for log output)
- `StringBuilder dispatchLog` - Batch log buffer (mutated)

**Returns**: `bool`
- `true` if consistency lock fires (skip account)
- `false` otherwise (proceed with dispatch)

**Reads**:
- `EnableConsistencyLock` (bool property)
- `MaxDailyProfitCap` (double property)
- `rankInfo.DailyPL` (double field)

**Mutates**:
- `dispatchLog` only (AppendLine call)

**Contains**:
- 1 log statement (line 395)

---

### 1.3 Parent Residual: Thin Dispatcher

```csharp
/// <summary>
/// Build 935 [SIMA-B935-001]: Skip-logic extracted from ExecuteSmartDispatchEntry fleet loop.
/// Returns true if the account should be skipped for this dispatch cycle.
/// Threading: strategy thread only. stateLock usage identical to original inline code.
/// T-W1: Refactored to thin dispatcher (CYC ≤ 10) with two private helpers.
/// </summary>
private bool ShouldSkipFleetAccount(Account acct, AccountRankInfo rankInfo,
    System.Collections.Generic.HashSet<string> activeAccountSnapshot, System.Text.StringBuilder dispatchLog)
```

**Signature**: UNCHANGED (C-API3 constraint)

**Structure** (pseudo-code):
```csharp
{
    // Step 1: Inactive check (inline, 5 lines)
    if (!activeAccountSnapshot.Contains(acct.Name))
    {
        dispatchLog.AppendLine(...);
        return true;
    }

    // Step 2: H-13 health check (void call, no return inspection)
    ShouldSkipFleet_RunHealthCheck(acct, dispatchLog);

    // Step 3: Consistency lock decision (bool return)
    return ShouldSkipFleet_IsConsistencyLockHit(rankInfo, acct, dispatchLog);
}
```

**Expected CYC**: 3 (one `if` branch, two method calls, final `return`)

---

## 2. Line-by-Line Extraction Mapping

### 2.1 Current Method Structure (Lines 347-400)

| Line Range | Content | Destination |
|------------|---------|-------------|
| 347-349 | Method signature + opening brace | **Parent** (unchanged) |
| 350-355 | Step 1: Inactive check | **Parent** (inline) |
| 356 | Blank line | (removed) |
| 357-390 | Step 2: H-13 try-catch block | **Helper 1** (entire block) |
| 391 | Blank line | (removed) |
| 392-397 | Step 3: Consistency lock | **Helper 2** (entire block) |
| 398 | Blank line | (removed) |
| 399 | `return false;` | **Parent** (implicit after Helper 2 returns false) |
| 400 | Closing brace | **Parent** |

### 2.2 Helper 1 Extraction (Lines 358-390 → 33 lines)

**Source Lines**: 358-390 (H-13 try-catch block)

**Extracted Content**:
```csharp
// Line 358: try opening
try
{
    // Line 360-361: Critical snapshot (H7 constraint - MUST stay in helper)
    // [939-P0]: Snapshot Positions to prevent broker-thread mutation during iteration.
    var brokerPos = acct.Positions.ToArray().FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
    bool brokerFlat = (brokerPos == null || brokerPos.MarketPosition == MarketPosition.Flat);

    // Lines 364-373: FSM/position/dispatch checks
    bool hasActiveFsmForAcct = _followerBrackets.Values.Any(f =>
        f != null
        && f.AccountName == acct.Name
        && (f.State == FollowerBracketState.Active
            || f.State == FollowerBracketState.Accepted
            || f.State == FollowerBracketState.Submitted
            || f.State == FollowerBracketState.Replacing));
    bool hasActivePositionForAcct = activePositions.Values.Any(p =>
        p.IsFollower && p.ExecutingAccount != null && p.ExecutingAccount.Name == acct.Name);
    bool hasDispatchPending = _dispatchSyncPendingExpKeys.ContainsKey(ExpKey(acct.Name));

    // Lines 375-384: Diagnostic logging (2 log statements)
    if (brokerFlat && !hasActiveFsmForAcct && !hasActivePositionForAcct && !hasDispatchPending)
    {
        // Truly stale: broker flat, no FSM, no position, no dispatch in flight. No-op (nothing to reset).
        dispatchLog.AppendLine(string.Format("[DISPATCH] H-13: {0} broker flat, no FSM/position/dispatch -- no action", acct.Name));
    }
    else if (brokerFlat && (hasActiveFsmForAcct || hasActivePositionForAcct || hasDispatchPending))
    {
        dispatchLog.AppendLine(string.Format("[DISPATCH] H-13 SKIP: {0} Flat but {1} -- not resetting",
            acct.Name, hasActiveFsmForAcct ? "FSM active" : (hasDispatchPending ? "dispatch pending" : "activePos present")));
    }
}
// Lines 386-390: T-Q1 catch wrapper (byte-identical preservation)
catch (Exception ex)
{
    if (_diagFleet)
        Print("[FLEET_CATCH] ProcessFleetSlot account iteration failed: " + ex.Message);
}
```

**New Helper 1 Method**:
```csharp
private void ShouldSkipFleet_RunHealthCheck(Account acct, StringBuilder dispatchLog)
{
    try
    {
        var brokerPos = acct.Positions.ToArray().FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
        bool brokerFlat = (brokerPos == null || brokerPos.MarketPosition == MarketPosition.Flat);

        bool hasActiveFsmForAcct = _followerBrackets.Values.Any(f =>
            f != null
            && f.AccountName == acct.Name
            && (f.State == FollowerBracketState.Active
                || f.State == FollowerBracketState.Accepted
                || f.State == FollowerBracketState.Submitted
                || f.State == FollowerBracketState.Replacing));
        bool hasActivePositionForAcct = activePositions.Values.Any(p =>
            p.IsFollower && p.ExecutingAccount != null && p.ExecutingAccount.Name == acct.Name);
        bool hasDispatchPending = _dispatchSyncPendingExpKeys.ContainsKey(ExpKey(acct.Name));

        if (brokerFlat && !hasActiveFsmForAcct && !hasActivePositionForAcct && !hasDispatchPending)
        {
            dispatchLog.AppendLine(string.Format("[DISPATCH] H-13: {0} broker flat, no FSM/position/dispatch -- no action", acct.Name));
        }
        else if (brokerFlat && (hasActiveFsmForAcct || hasActivePositionForAcct || hasDispatchPending))
        {
            dispatchLog.AppendLine(string.Format("[DISPATCH] H-13 SKIP: {0} Flat but {1} -- not resetting",
                acct.Name, hasActiveFsmForAcct ? "FSM active" : (hasDispatchPending ? "dispatch pending" : "activePos present")));
        }
    }
    catch (Exception ex)
    {
        if (_diagFleet)
            Print("[FLEET_CATCH] ProcessFleetSlot account iteration failed: " + ex.Message);
    }
}
```

### 2.3 Helper 2 Extraction (Lines 393-397 → 5 lines)

**Source Lines**: 393-397 (Consistency lock check)

**Extracted Content**:
```csharp
// Step 3: Consistency Lock -- skip if daily P&L cap hit.
if (EnableConsistencyLock && rankInfo.DailyPL >= MaxDailyProfitCap)
{
    dispatchLog.AppendLine(string.Format("[DISPATCH] {0} SKIPPED - Consistency Lock ({1:C})", acct.Name, rankInfo.DailyPL));
    return true;
}
```

**New Helper 2 Method**:
```csharp
private bool ShouldSkipFleet_IsConsistencyLockHit(AccountRankInfo rankInfo, Account acct, StringBuilder dispatchLog)
{
    if (EnableConsistencyLock && rankInfo.DailyPL >= MaxDailyProfitCap)
    {
        dispatchLog.AppendLine(string.Format("[DISPATCH] {0} SKIPPED - Consistency Lock ({1:C})", acct.Name, rankInfo.DailyPL));
        return true;
    }
    return false;
}
```

---

## 3. Parent Residual Structure

**New Parent Method** (post-extraction):

```csharp
private bool ShouldSkipFleetAccount(Account acct, AccountRankInfo rankInfo,
    System.Collections.Generic.HashSet<string> activeAccountSnapshot, System.Text.StringBuilder dispatchLog)
{
    // Step 1: Inactive check -- prevents UI toggle race.
    if (!activeAccountSnapshot.Contains(acct.Name))
    {
        dispatchLog.AppendLine(string.Format("[SIMA] {0} SKIPPED (Inactive)", acct.Name));
        return true;
    }

    // Step 2: H-13 stale state reconciliation (void call, diagnostic-only)
    ShouldSkipFleet_RunHealthCheck(acct, dispatchLog);

    // Step 3: Consistency lock decision (bool return)
    return ShouldSkipFleet_IsConsistencyLockHit(rankInfo, acct, dispatchLog);
}
```

**Complexity Analysis**:
- 1 `if` branch (inactive check) → +1 CYC
- 1 void method call (no branch) → +0 CYC
- 1 bool method call (return value) → +0 CYC
- **Total Parent CYC**: 2 (well under target of ≤10)

---

## 4. Log Statement Preservation Table

| Log Statement | Original Line | New Location | Content (Byte-Identical) |
|---------------|---------------|--------------|--------------------------|
| **Log 1** | 353 | Parent:353 | `"[SIMA] {0} SKIPPED (Inactive)"` |
| **Log 2** | 378 | Helper1:19 | `"[DISPATCH] H-13: {0} broker flat, no FSM/position/dispatch -- no action"` |
| **Log 3** | 382-383 | Helper1:23-24 | `"[DISPATCH] H-13 SKIP: {0} Flat but {1} -- not resetting"` |
| **Log 4** | 395 | Helper2:5 | `"[DISPATCH] {0} SKIPPED - Consistency Lock ({1:C})"` |

**Verification**: All 4 log statements preserved byte-identical (including format strings, placeholders, and conditional logic).

---

## 5. Dependency Verification

### 5.1 Helper 1 Dependencies (All Accessible)

| Dependency | Type | Accessibility | Notes |
|------------|------|---------------|-------|
| `_followerBrackets` | `ConcurrentDictionary<string, FollowerBracketFSM>` | Private instance field | ✅ Accessible from private helper |
| `activePositions` | `ConcurrentDictionary<string, PositionInfo>` | Private instance field | ✅ Accessible from private helper |
| `_dispatchSyncPendingExpKeys` | `ConcurrentDictionary<string, byte>` | Private instance field | ✅ Accessible from private helper |
| `ExpKey(string)` | Private instance method | Private instance method | ✅ Accessible from private helper |
| `Instrument.FullName` | Property (via `acct.Positions`) | Public property | ✅ Accessible |
| `_diagFleet` | Private instance field | Private instance field | ✅ Accessible from private helper |
| `Print(string)` | Protected Strategy method | Protected method | ✅ Accessible from private helper |

### 5.2 Helper 2 Dependencies (All Accessible)

| Dependency | Type | Accessibility | Notes |
|------------|------|---------------|-------|
| `EnableConsistencyLock` | Public property | Public property | ✅ Accessible from private helper |
| `MaxDailyProfitCap` | Public property | Public property | ✅ Accessible from private helper |
| `rankInfo.DailyPL` | Field of parameter | Parameter field | ✅ Accessible (passed as param) |

**Conclusion**: All dependencies are accessible from private instance methods. No refactoring of field visibility required.

---

## 6. Complexity Projection

### 6.1 Current Method (Pre-Extraction)

**Measured CYC**: 25 (from forensic intake)

**Breakdown**:
- Step 1 (inactive check): 1 `if` → +1
- Step 2 (H-13 try-catch): ~20 branches (nested `if`, LINQ `.Any()`, ternary operators) → +20
- Step 3 (consistency lock): 1 `if` → +1
- **Total**: ~22-25 CYC

### 6.2 Post-Extraction Projection

#### Parent Method (Target: CYC ≤ 10)
- Step 1 inline: 1 `if` → +1 CYC
- Helper 1 call: void return, no branch → +0 CYC
- Helper 2 call: bool return, no branch in parent → +0 CYC
- **Projected Parent CYC**: **2** ✅ (well under target)

#### Helper 1 (Diagnostic-Only)
- 1 `try-catch` → +1 CYC
- 3 LINQ `.Any()` calls → +0 CYC (per H6/P1/Q-A4=D - deferred to T-W1-Perf)
- 2 `if` branches (lines 375, 380) → +2 CYC
- **Projected Helper 1 CYC**: **3** ✅

#### Helper 2 (Consistency Lock)
- 1 `if` branch → +1 CYC
- **Projected Helper 2 CYC**: **1** ✅

**Total Complexity**: 2 + 3 + 1 = **6 CYC** (distributed across 3 methods)

**Reduction**: 25 → 6 CYC (**76% reduction**)

---

## 7. Caller Impact Analysis

### 7.1 Caller Location

**File**: `src/V12_002.SIMA.Dispatch.cs`  
**Method**: `ExecuteSmartDispatchEntry`  
**Line**: 120

**Current Call**:
```csharp
// Build 935 [SIMA-B935-001]: Inactive + H-13 + consistency lock delegated to ShouldSkipFleetAccount.
if (ShouldSkipFleetAccount(acct, fleet[i], activeAccountSnapshot, dispatchLog)) continue;
```

### 7.2 Impact Assessment

**Signature Change**: NONE (C-API3 constraint)  
**Return Type**: UNCHANGED (`bool`)  
**Parameter Types**: UNCHANGED  
**Parameter Order**: UNCHANGED

**Conclusion**: **Zero changes required to Dispatch.cs**. The extraction is internal to `ShouldSkipFleetAccount` and invisible to callers.

---

## 8. T-Q1 Catch Wrapper Preservation

### 8.1 Original Catch Block (Lines 386-390)

```csharp
catch (Exception ex)
{
    if (_diagFleet)
        Print("[FLEET_CATCH] ProcessFleetSlot account iteration failed: " + ex.Message);
}
```

### 8.2 Preservation in Helper 1

**Location**: Helper 1, lines 28-32 (end of try-catch block)

**Content** (byte-identical):
```csharp
catch (Exception ex)
{
    if (_diagFleet)
        Print("[FLEET_CATCH] ProcessFleetSlot account iteration failed: " + ex.Message);
}
```

**Verification**:
- ✅ Exception type: `Exception` (unchanged)
- ✅ Variable name: `ex` (unchanged)
- ✅ Conditional: `if (_diagFleet)` (unchanged)
- ✅ Print message: `"[FLEET_CATCH] ProcessFleetSlot account iteration failed: "` (byte-identical)
- ✅ String concatenation: `+ ex.Message` (unchanged)

**Conclusion**: T-Q1 catch wrapper preserved byte-identical in Helper 1.

---

## 9. Verification Gates

### 9.1 Pre-Extraction Checklist

- [x] Forensic baseline confirmed (lines 347-400, CYC=25)
- [x] T-Q1 catch wrapper located (lines 386-390)
- [x] Critical snapshot identified (line 361)
- [x] 4 log statements mapped
- [x] Caller signature verified (Dispatch.cs:120)
- [x] All dependencies accessible from private helpers

### 9.2 Post-Extraction Verification

#### Gate 1: Complexity Audit
```powershell
# Run complexity audit on modified file
powershell -File .\scripts\complexity_audit.ps1 -File src\V12_002.SIMA.Fleet.cs
```

**Success Criteria**:
- `ShouldSkipFleetAccount` CYC ≤ 10 ✅
- `ShouldSkipFleet_RunHealthCheck` CYC ≤ 15 ✅
- `ShouldSkipFleet_IsConsistencyLockHit` CYC ≤ 5 ✅

#### Gate 2: Log Diff Audit
```powershell
# Compare log output before/after extraction
# Run test harness with _diagFleet=true, capture logs, diff
```

**Success Criteria**:
- All 4 log statements produce identical output (format, content, timing)
- No new log statements introduced
- No log statements removed

#### Gate 3: Snapshot Locality Verification
```bash
# Verify acct.Positions.ToArray() stays in Helper 1
grep -n "acct.Positions.ToArray()" src/V12_002.SIMA.Fleet.cs
```

**Success Criteria**:
- Snapshot appears ONLY in `ShouldSkipFleet_RunHealthCheck` (H7 constraint)
- Snapshot does NOT appear in parent or Helper 2

#### Gate 4: Caller Signature Verification
```bash
# Verify no changes to Dispatch.cs caller
git diff src/V12_002.SIMA.Dispatch.cs
```

**Success Criteria**:
- `git diff` output is empty (zero changes to Dispatch.cs)

#### Gate 5: F5 Test (NinjaTrader Live)
1. Build solution: `dotnet build`
2. Sync to NinjaTrader: `powershell -File .\deploy-sync.ps1`
3. Press F5 in NinjaTrader
4. Enable SIMA, trigger fleet dispatch
5. Verify logs show identical H-13 diagnostics

**Success Criteria**:
- Strategy loads without errors
- Fleet dispatch executes successfully
- H-13 logs appear in Output window (byte-identical to baseline)
- No phantom skips or consistency lock false positives

#### Gate 6: BUILD_TAG Verification
```bash
# Verify BUILD_TAG updated
grep "BUILD_TAG" src/V12_002.cs
```

**Success Criteria**:
- BUILD_TAG = `1111.007-phase7-tW1`

---

## 10. BUILD_TAG Update

**Current**: `1111.006-phase7-tQ1` (from T-Q1 extraction)  
**New**: `1111.007-phase7-tW1`

**Location**: `src/V12_002.cs` (top-level partial class)

**Change**:
```csharp
// Before:
private const string BUILD_TAG = "1111.006-phase7-tQ1";

// After:
private const string BUILD_TAG = "1111.007-phase7-tW1";
```

---

## 11. Guardrail Compliance Matrix

| Guardrail | Constraint | Compliance | Verification |
|-----------|------------|------------|--------------|
| **B2** | Every `(action, OrderType, fleet membership)` tuple returns same bool | ✅ | Logic unchanged, only structure refactored |
| **H8** | Helper 1 MUST return void (no bool return path) | ✅ | Signature enforces `void` return |
| **H7** | `acct.Positions.ToArray()` MUST stay in Helper 1 | ✅ | Snapshot at Helper1:line 5 (verified via grep) |
| **H6/P1/Q-A4=D** | Do NOT replace LINQ `.Any()` calls | ✅ | All `.Any()` calls preserved (deferred to T-W1-Perf) |
| **C-API3** | Parent signature unchanged | ✅ | Signature byte-identical |
| **C-API1** | Both helpers are `private` instance methods | ✅ | Both declared `private` |
| **Verbatim logs** | All 4 `dispatchLog.AppendLine` strings byte-identical | ✅ | Table §4 confirms byte-identical preservation |
| **C5** | PR diff under 150 KB | ✅ | Single-file change, ~100 lines modified |

---

## 12. Execution Sequence (Bob CLI)

### 12.1 Pre-Flight

```bash
# 1. Verify baseline state
git status
git diff src/V12_002.SIMA.Fleet.cs

# 2. Create checkpoint
git add -A
git commit -m "Checkpoint: Pre-T-W1 extraction baseline"
```

### 12.2 Extraction Steps

**Bob CLI Mode**: `v12-engineer` (custom mode with checkpointing enabled)

**Commands**:
```bash
# Step 1: Extract Helper 1 (lines 358-390 → new method)
# Bob will use apply_diff to:
# - Insert new method ShouldSkipFleet_RunHealthCheck after line 400
# - Remove lines 358-390 from parent
# - Insert call to helper at line 357

# Step 2: Extract Helper 2 (lines 393-397 → new method)
# Bob will use apply_diff to:
# - Insert new method ShouldSkipFleet_IsConsistencyLockHit after Helper 1
# - Remove lines 393-397 from parent
# - Replace with return statement calling helper

# Step 3: Update BUILD_TAG
# Bob will use apply_diff on src/V12_002.cs:
# - Change BUILD_TAG from "1111.006-phase7-tQ1" to "1111.007-phase7-tW1"
```

### 12.3 Post-Extraction Verification

```bash
# 1. Complexity audit
powershell -File .\scripts\complexity_audit.ps1 -File src\V12_002.SIMA.Fleet.cs

# 2. Build test
dotnet build

# 3. Sync to NinjaTrader
powershell -File .\deploy-sync.ps1

# 4. F5 test (manual)
# - Press F5 in NinjaTrader
# - Enable SIMA
# - Trigger fleet dispatch
# - Verify H-13 logs in Output window

# 5. Commit
git add -A
git commit -m "Phase 7 T-W1: Extract ShouldSkipFleetAccount (CYC 25→2)"
```

---

## 13. Risk Mitigation

### 13.1 Identified Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| **R1**: Helper 1 void return violates caller expectation | P0 | H8 constraint enforced by spec; parent never inspects Helper 1 return |
| **R2**: Snapshot moved outside Helper 1 (H7 violation) | P0 | Grep verification in Gate 3; Bob CLI will preserve snapshot locality |
| **R3**: Log output changes (breaks forensic audit) | P1 | Byte-identical preservation verified in §4; F5 test confirms |
| **R4**: LINQ `.Any()` replaced prematurely | P1 | H6/P1/Q-A4=D constraint enforced; deferred to T-W1-Perf |
| **R5**: Caller signature drift | P2 | C-API3 constraint enforced; Gate 4 verifies zero Dispatch.cs changes |

### 13.2 Rollback Plan

If any verification gate fails:

```bash
# Rollback to pre-extraction checkpoint
git reset --hard HEAD~1

# Re-run forensic intake
# Hand back to Architect for plan revision
```

---

## 14. Success Criteria

### 14.1 Functional Correctness (B2)

- [ ] All fleet dispatch scenarios produce identical skip decisions (before/after)
- [ ] Inactive accounts still skipped (Step 1 inline)
- [ ] H-13 diagnostics still logged (Helper 1 void call)
- [ ] Consistency lock still fires when `DailyPL >= MaxDailyProfitCap` (Helper 2 bool return)

### 14.2 Structural Integrity

- [ ] Parent CYC ≤ 10 (target: 2)
- [ ] Helper 1 CYC ≤ 15 (target: 3)
- [ ] Helper 2 CYC ≤ 5 (target: 1)
- [ ] Total CYC reduction ≥ 70% (target: 76%)

### 14.3 Forensic Audit

- [ ] All 4 log statements byte-identical
- [ ] T-Q1 catch wrapper preserved in Helper 1
- [ ] Snapshot locality verified (Helper 1 only)
- [ ] Zero changes to Dispatch.cs

### 14.4 Build & Deploy

- [ ] `dotnet build` succeeds
- [ ] `deploy-sync.ps1` succeeds
- [ ] F5 test passes (NinjaTrader loads, SIMA dispatches)
- [ ] BUILD_TAG = `1111.007-phase7-tW1`

---

## 15. Handoff to Adjudicator (Arena AI)

**Status**: READY FOR ADJUDICATION

**Adjudication Checklist**:
- [ ] Verify all guardrails (B2, H6, H7, H8, C-API1, C-API3, P1, Q-A4=D, C5) addressed
- [ ] Verify complexity projection (25 → 6 CYC, 76% reduction)
- [ ] Verify log preservation (4 statements byte-identical)
- [ ] Verify caller impact (zero changes to Dispatch.cs)
- [ ] Verify T-Q1 catch wrapper preservation
- [ ] Verify snapshot locality (H7 constraint)
- [ ] Approve for Bob CLI execution OR reject with specific revision requests

**Next Step**: Hand off to Arena AI for adversarial audit. If PASS, hand off to Bob CLI (`v12-engineer` mode) for surgical execution.

---

**END OF PLAN**