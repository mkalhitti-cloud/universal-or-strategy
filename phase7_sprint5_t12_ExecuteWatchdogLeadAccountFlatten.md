# [Phase7-S5-T12] ExecuteWatchdogLeadAccountFlatten CYC Reduction

**Status**: Implementation Plan  
**BUILD_TAG Target**: `1111.007-phase7-t12`  
**Date**: 2026-05-13

---

## 1. Forensic Analysis

### 1.1 Current State
- **Location**: `src/V12_002.Safety.Watchdog.cs:138-211`
- **Current CYC**: 25 (per spec)
- **Current LOC**: 74 (actual: lines 138-211)
- **Max Nesting**: 4
- **Target CYC**: ≤19
- **Callers**: 1 Enqueue lambda at line 69: `Enqueue(ctx => ctx.ExecuteWatchdogLeadAccountFlatten())`

### 1.2 Method Structure Analysis

The method `ExecuteWatchdogLeadAccountFlatten` performs emergency flatten operations:

1. **Early-Return Guards** (Lines 140-150): 4 critical safety guards (INV-5.1)
   - `masterAccount == null` → return
   - `Instrument == null` → return
   - `_isTerminating` → return
   - `State != State.Realtime` → return
   - `!HasWatchdogLeadAccountWorkingOrder()` → reset stage & return
   - `!HasWatchdogLeadAccountExposure()` → return

2. **Flatten Scope Management** (Lines 152, 209-210): `EnterFlattenScope()` / `ExitFlattenScope()`

3. **Order Cancellation Block** (Lines 155-178): 
   - Build `ordersToCancel` list by iterating `masterAccount.Orders`
   - Filter by instrument and working states
   - Cancel each order via `CancelOrderOnAccount`
   - Print cancellation count

4. **Position Flattening Block** (Lines 180-198):
   - Iterate `masterAccount.Positions`
   - Filter by instrument and non-flat positions
   - Submit market orders to flatten (Long → Sell, Short → BuyToCover)
   - Print flatten results

5. **State Cleanup** (Lines 200-201): `SetExpectedPositionLocked` + `PublishUiSnapshot`

6. **Exception Handling** (Lines 203-208): Catch-all with Print

### 1.3 Complexity Drivers

Primary complexity sources (CYC=25):
- 4 early-return guards (+4)
- Try-catch block (+1)
- Order loop with nested conditionals (+8)
  - foreach (+1)
  - 2 null checks (+2)
  - instrument check (+1)
  - 5-way OrderState OR condition (+4)
- Order cancellation loop (+1)
- Position loop with nested conditionals (+8)
  - foreach (+1)
  - 2 null checks (+2)
  - instrument check (+1)
  - flat check (+1)
  - ternary for direction (+1)
  - null check for flattenOrder (+1)
  - else branch (+1)
- Count check (+1)

### 1.4 Extraction Strategy

**CRITICAL CONSTRAINT (INV-5.1)**: All 4 early-return guards MUST remain at the residual level as `return;` statements. They CANNOT be extracted into a `void` sub-helper that returns silently — that would invert the safety predicate.

**Approach**: Extract 2 sub-helpers following D-D3 (LOCKED signature policy):

1. **`CancelWatchdogWorkingOrders`** (CYC ~10): Extract order cancellation logic (lines 155-178)
2. **`FlattenWatchdogPositions`** (CYC ~10): Extract position flattening logic (lines 180-198)

**Residual**: Thin dispatcher (CYC ~7) that:
- Preserves all 4 early-return guards verbatim
- Calls `EnterFlattenScope()`
- Calls `CancelWatchdogWorkingOrders`
- Calls `FlattenWatchdogPositions`
- Calls state cleanup
- Calls `ExitFlattenScope()` in finally block

**Rationale**: The early-return guards are SAFETY-CRITICAL and must remain at the entry point. We extract the two major operational blocks (order cancellation and position flattening) which contain the bulk of the complexity.

**LOC Deviation (DEVIATION-T12-A per D-S5)**: Method is 74 LOC, short of the 15-LOC minimum for sub-helpers. This is acceptable because:
- SAFETY-CRITICAL code requires preservation of guard structure
- Early-return guards cannot be extracted per INV-5.1
- Extraction still achieves CYC reduction goal

---

## 2. Implementation Plan

### 2.1 Sub-Helper 1: CancelWatchdogWorkingOrders

**Purpose**: Cancel all working orders for the master account on the current instrument.

**Signature**: 
```csharp
private void CancelWatchdogWorkingOrders(Account masterAccount, string instrumentName)
```

**Logic**:
- Create `List<Order> ordersToCancel`
- Loop through `masterAccount.Orders.ToArray()`
  - Skip if order/instrument null
  - Skip if instrument doesn't match
  - Add to list if OrderState is Working/Submitted/Accepted/ChangePending/ChangeSubmitted
- Loop through `ordersToCancel` and call `CancelOrderOnAccount`
- Print cancellation count if > 0

**Expected CYC**: ~10 (loop + nested conditionals + 5-way OR)

**Print Statements**: 1
- `"[WATCHDOG] Cancelled " + ordersToCancel.Count + " master order(s) on strategy thread."`

### 2.2 Sub-Helper 2: FlattenWatchdogPositions

**Purpose**: Flatten all non-flat positions for the master account on the current instrument.

**Signature**:
```csharp
private void FlattenWatchdogPositions(Account masterAccount, string instrumentName)
```

**Logic**:
- Loop through `masterAccount.Positions`
  - Skip if position/instrument null
  - Skip if instrument doesn't match
  - Skip if position is flat
  - Get quantity
  - Submit market order based on direction (Long → Sell, Short → BuyToCover)
  - Print result (null check for order)

**Expected CYC**: ~10 (loop + nested conditionals + ternary + null check)

**Print Statements**: 2
- `"[WATCHDOG] Strategy-thread master close returned null."`
- `"[WATCHDOG] Strategy-thread master close submitted: " + quantity + " on " + masterAccount.Name`

### 2.3 Residual Dispatcher

**Signature**: LOCKED (per D-D3) — Enqueue lambda site must compile unchanged
```csharp
private void ExecuteWatchdogLeadAccountFlatten()
```

**Logic**:
- **PRESERVE VERBATIM**: All 4 early-return guards (INV-5.1)
  - `if (masterAccount == null || Instrument == null || _isTerminating || State != State.Realtime) return;`
  - `if (!HasWatchdogLeadAccountWorkingOrder()) { Interlocked.Exchange(ref _watchdogStage, 0); return; }`
  - `if (!HasWatchdogLeadAccountExposure()) return;`
- `EnterFlattenScope();`
- `try` block:
  - Get `instrumentName`
  - Call `CancelWatchdogWorkingOrders(masterAccount, instrumentName)`
  - Call `FlattenWatchdogPositions(masterAccount, instrumentName)`
  - Call `SetExpectedPositionLocked(ExpKey(masterAccount.Name), 0)`
  - Call `PublishUiSnapshot()`
- `catch (Exception ex)`: Print error
- `finally`: `ExitFlattenScope()`

**Expected CYC**: ~7 (guards + try-catch)

**Print Statements**: 1
- `"[WATCHDOG] Strategy-thread emergency close failed: " + ex.Message`

---

## 3. Implementation Steps

### Step 1: Extract CancelWatchdogWorkingOrders
- Insert new method before `ExecuteWatchdogLeadAccountFlatten`
- Move order cancellation logic from lines 155-178
- Accept `masterAccount` and `instrumentName` as parameters
- Preserve Print statement verbatim

### Step 2: Extract FlattenWatchdogPositions
- Insert new method before `ExecuteWatchdogLeadAccountFlatten`
- Move position flattening logic from lines 180-198
- Accept `masterAccount` and `instrumentName` as parameters
- Preserve both Print statements verbatim

### Step 3: Refactor Residual
- Keep all early-return guards at the top (UNCHANGED)
- Replace extracted sections with helper calls
- Maintain try-catch-finally structure
- Preserve exception Print statement

---

## 4. Invariants & Constraints

### 4.1 V12 DNA Cross-Cutting (INV-1.1 .. INV-1.5)
- **INV-1.1**: No `lock()` statements
- **INV-1.2**: ASCII-only string literals
- **INV-1.3**: No Unicode/emoji
- **INV-1.4**: Preserve all Print/AppendLine verbatim
- **INV-1.5**: Hard-link sync via `deploy-sync.ps1`

### 4.2 T12-Specific Invariants

**INV-5.1 — All 4 Early-Return Guards Preserved Verbatim**:
Each guard MUST remain a `return;` at the residual level (NOT extracted into a `void` sub-helper):
1. `masterAccount == null || Instrument == null || _isTerminating || State != State.Realtime`
2. `!HasWatchdogLeadAccountWorkingOrder()` (with stage reset)
3. `!HasWatchdogLeadAccountExposure()`

**INV-5.4 — Enqueue Lambda Site Preserved**:
```bash
grep -c "Enqueue(ctx => ctx.ExecuteWatchdogLeadAccountFlatten" src/V12_002.Safety.Watchdog.cs
# MUST equal 1
```

**INV-5.6 — Deadlock Detection Print Preserved**:
```bash
grep -cn "DEADLOCK DETECTED" src/V12_002.Safety.Watchdog.cs
# MUST equal 1 (at line 68)
```

### 4.3 Signature Policy
- **D-D3 (LOCKED)**: Enqueue lambda site at line 69 must continue compiling unchanged
- Signature: `private void ExecuteWatchdogLeadAccountFlatten()` — NO parameters, NO return value

### 4.4 LOC Deviation
- **DEVIATION-T12-A (per D-S5)**: 74 LOC short of target — document at EXTRACT-GATE
- Acceptable because early-return guards cannot be extracted per INV-5.1

---

## 5. Acceptance Criteria

### 5.1 Complexity Metrics
- [ ] Residual `ExecuteWatchdogLeadAccountFlatten` CYC ≤19 (target: ~7)
- [ ] `CancelWatchdogWorkingOrders` CYC ≤19 (target: ~10)
- [ ] `FlattenWatchdogPositions` CYC ≤19 (target: ~10)
- [ ] `ExecuteWatchdogLeadAccountFlatten` removed from "CYC > 20 remaining" list

### 5.2 Behavioral Preservation
- [ ] Enqueue lambda site at line 69 unchanged: `grep -c "Enqueue(ctx => ctx.ExecuteWatchdogLeadAccountFlatten" src/V12_002.Safety.Watchdog.cs` == 1
- [ ] Deadlock Print preserved: `grep -cn "DEADLOCK DETECTED" src/V12_002.Safety.Watchdog.cs` == 1
- [ ] All 4 Print statements preserved verbatim (total count: 4)
- [ ] All 4 early-return guards remain at residual level
- [ ] Zero logic changes to watchdog emergency flatten

### 5.3 Build & Tag
- [ ] BUILD_TAG bumped to `1111.007-phase7-t12`
- [ ] Clean build with zero errors
- [ ] `deploy-sync.ps1` executed successfully

### 5.4 F5 Acceptance
"Press F5; verify BUILD_TAG; manually verify watchdog timer behavior under normal operation (no emergency fire); verify zero `DEADLOCK DETECTED` Prints during normal trading; if a deadlock can be artificially induced (Director's call), verify stage-1 escalation fires correctly."

---

## 6. Verification Steps

### 6.1 Pre-extraction Baseline
```powershell
# Count Print statements
Select-String -Path src/V12_002.Safety.Watchdog.cs -Pattern 'Print\(' | Measure-Object

# Verify Enqueue site
Select-String -Path src/V12_002.Safety.Watchdog.cs -Pattern 'Enqueue\(ctx => ctx\.ExecuteWatchdogLeadAccountFlatten'

# Verify DEADLOCK DETECTED
Select-String -Path src/V12_002.Safety.Watchdog.cs -Pattern 'DEADLOCK DETECTED'
```

### 6.2 Post-extraction Verification
```powershell
# Verify CYC reduction (manual inspection or complexity tool)
# Verify Print count unchanged
Select-String -Path src/V12_002.Safety.Watchdog.cs -Pattern 'Print\(' | Measure-Object

# Verify Enqueue site unchanged
Select-String -Path src/V12_002.Safety.Watchdog.cs -Pattern 'Enqueue\(ctx => ctx\.ExecuteWatchdogLeadAccountFlatten'

# Verify DEADLOCK DETECTED unchanged
Select-String -Path src/V12_002.Safety.Watchdog.cs -Pattern 'DEADLOCK DETECTED'

# Build
powershell -File .\scripts\build_readiness.ps1

# Sync hard links
powershell -File .\deploy-sync.ps1
```

### 6.3 F5 Test
Launch NinjaTrader, verify BUILD_TAG, observe watchdog timer behavior under normal operation.

### 6.4 Diff Audit
Verify `ExecuteWatchdogDirectFallback` (T15) not in diff, Enqueue site unchanged.

### 6.5 Sign-off
Update acceptance report.

---

## 7. Verbatim Print Assertions

**Total Print Statements in Method**: 4

1. Line 178: `"[WATCHDOG] Cancelled " + ordersToCancel.Count + " master order(s) on strategy thread."`
2. Line 195: `"[WATCHDOG] Strategy-thread master close returned null."`
3. Line 197: `"[WATCHDOG] Strategy-thread master close submitted: " + quantity + " on " + masterAccount.Name`
4. Line 205: `"[WATCHDOG] Strategy-thread emergency close failed: " + ex.Message`

**Verification Commands**:
```bash
grep -cn "Cancelled.*master order" src/V12_002.Safety.Watchdog.cs  # == 1
grep -cn "Strategy-thread master close returned null" src/V12_002.Safety.Watchdog.cs  # == 1
grep -cn "Strategy-thread master close submitted" src/V12_002.Safety.Watchdog.cs  # == 1
grep -cn "Strategy-thread emergency close failed" src/V12_002.Safety.Watchdog.cs  # == 1
```

---

## 8. Risk Assessment

**Risk Level**: MEDIUM (SAFETY-CRITICAL code)

**Risk Factors**:
1. **SAFETY-CRITICAL**: Watchdog emergency flatten is last-resort deadlock recovery
2. **Signature LOCKED**: Enqueue lambda site must compile unchanged (Q8-strict applies)
3. **Early-Return Guards**: Must remain at residual level per INV-5.1
4. **Two-Stage Escalation**: Must not interfere with stage-1 → stage-2 transition

**Mitigation**:
- Preserve exact Print messages (4 total)
- Maintain early-return guard structure verbatim
- Test with F5 acceptance criterion
- Extra read-aloud of residual at EXTRACT-GATE per Director's brief

---

## 9. References

- **Analysis**: spec:807e80ce-4657-46c6-a10f-0338ea1a907b/ee6c7363-16b7-4be4-85d2-8a48a784743e §1.1 row T12
- **Approach**: spec:807e80ce-4657-46c6-a10f-0338ea1a907b/7d42f7da-0c65-4020-8b2d-40117382d136 §1.4 D-D3 (LOCKED)
- **Invariants**: spec:807e80ce-4657-46c6-a10f-0338ea1a907b/7d42f7da-0c65-4020-8b2d-40117382d136 §4 INV-5

---

**Status**: Ready for implementation