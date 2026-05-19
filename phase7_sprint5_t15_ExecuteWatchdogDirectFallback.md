# [Phase7-S5-T15] ExecuteWatchdogDirectFallback Extraction Plan

**Status**: IMPLEMENTATION READY  
**Created**: 2026-05-13  
**Target**: `ExecuteWatchdogDirectFallback` in `src/V12_002.Safety.Watchdog.cs`  
**Objective**: Reduce CYC from 20 → ≤19 via sub-helper extraction

---

## 1. Forensic Analysis

### 1.1 Current Metrics (jCodemunch)
```
Function: ExecuteWatchdogDirectFallback
File: src/V12_002.Safety.Watchdog.cs
Line: 221-298 (78 lines)
Cyclomatic Complexity: 20 (ticket says 21, jcodemunch measures 20)
Max Nesting Depth: 5
Parameters: 0
Assessment: HIGH
```

### 1.2 Caller Context (CRITICAL - INV-5.3)
**Single Caller**: `OnWatchdogTimer` at line 84-88
```csharp
if (Interlocked.CompareExchange(ref _watchdogStage, 2, 1) == 1)
{
    Print("[WATCHDOG] Escalating to direct master close fallback.");
    ExecuteWatchdogDirectFallback();
}
```

**CRITICAL CONSTRAINT (INV-5.3)**: The `Interlocked.CompareExchange(ref _watchdogStage, 2, 1)` gate MUST remain at the call site. Moving it inside `ExecuteWatchdogDirectFallback` would break the single-fire guarantee under timer race conditions.

### 1.3 Function Structure Analysis

**Early-Return Guards (INV-5.2 - MUST preserve verbatim at residual level)**:
- Line 223-225: `if (masterAccount == null || Instrument == null) return;`
- Line 226-230: `if (!HasWatchdogLeadAccountWorkingOrder()) { Interlocked.Exchange(ref _watchdogStage, 0); return; }`

**Logical Phases**:
1. **Phase A: Order Cancellation** (lines 232-257, CYC≈8)
   - Collect working orders for instrument
   - Cancel via `masterAccount.Cancel()`
   - Print cancellation count

2. **Phase B: Position Flattening** (lines 259-291, CYC≈10)
   - Iterate positions for instrument
   - Create market close orders
   - Submit via `masterAccount.Submit()`
   - Print submission details

3. **Phase C: Exception Handling** (lines 292-297, CYC≈2)
   - Catch block resets stage to 1
   - Prints failure message

### 1.4 Verbatim Print Baseline
```
Total Print statements in file: 14
Critical escalation message (line 86): "[WATCHDOG] Escalating to direct master close fallback."
Target function Print statements:
  - Line 256: "[WATCHDOG] Direct fallback cancelled " + ordersToCancel.Count + " master order(s)."
  - Line 285: "[WATCHDOG] Direct fallback CreateOrder returned null."
  - Line 290: "[WATCHDOG] Direct fallback close submitted: " + position.Quantity + " on " + masterAccount.Name
  - Line 296: "[WATCHDOG] Direct fallback failed: " + ex.Message
```

### 1.5 Co-Residency Warning
**T12 Extracted Helpers** (DO NOT TOUCH):
- `CancelWatchdogWorkingOrders` (lines 138-163)
- `FlattenWatchdogPositions` (lines 165-186)
- `ExecuteWatchdogLeadAccountFlatten` (lines 188-219)

---

## 2. Extraction Strategy

### 2.1 Signature Policy: SOFT-LOCK
- Default: Preserve `private void ExecuteWatchdogDirectFallback()`
- Single caller allows signature changes IF safety-justified
- **Decision**: PRESERVE signature (no parameters needed, helpers can access class state)

### 2.2 Helper Extraction Plan

#### Helper 1: `CancelDirectFallbackOrders`
**Purpose**: Collect and cancel working orders  
**Signature**: `private void CancelDirectFallbackOrders(Account masterAccount, string instrumentName)`  
**Returns**: void (mutates state, prints count)  
**Lines**: ~26 LOC  
**Estimated CYC**: 8  
**Logic**:
- Create `List<Order> ordersToCancel`
- Iterate `masterAccount.Orders.ToArray()`
- Filter by instrument, check working states
- Call `masterAccount.Cancel()` if count > 0
- Print cancellation count

#### Helper 2: `FlattenDirectFallbackPositions`
**Purpose**: Create and submit market close orders for positions  
**Signature**: `private void FlattenDirectFallbackPositions(Account masterAccount, string instrumentName)`  
**Returns**: void (mutates state, prints submissions)  
**Lines**: ~33 LOC  
**Estimated CYC**: 10  
**Logic**:
- Iterate `masterAccount.Positions`
- Filter by instrument, skip flat positions
- Determine close action (Sell/BuyToCover)
- Create order via `masterAccount.CreateOrder()`
- Submit via `masterAccount.Submit()`
- Print submission details or null warnings

#### Residual: `ExecuteWatchdogDirectFallback`
**Estimated CYC**: 2 (guards + try/catch)  
**Lines**: ~18 LOC  
**Logic**:
- Early-return guards (verbatim from original)
- try block:
  - Get instrumentName
  - Call `CancelDirectFallbackOrders(masterAccount, instrumentName)`
  - Call `FlattenDirectFallbackPositions(masterAccount, instrumentName)`
- catch block:
  - Reset stage to 1
  - Print failure message

### 2.3 DEVIATION-T15-A Pre-Flag
**LOC Deviation**: 78-line target → helpers ~26+33=59 LOC + residual ~18 LOC = 77 LOC total  
**Justification**: Minimal overhead due to signature preservation and no new allocations  
**Status**: ACCEPTABLE per D-S5 (short-target extraction)

---

## 3. Safety Invariants (ZERO-TOLERANCE)

### INV-1.1: ASCII-Only
- All string literals remain ASCII (no Unicode, emoji, curly quotes)
- Verified: All existing strings are ASCII-compliant

### INV-1.2: No New Locks
- No `lock()` statements introduced
- Existing `Interlocked` operations preserved

### INV-1.3: Atomic Operations
- `Interlocked.Exchange(ref _watchdogStage, 0)` preserved in early-return guard
- `Interlocked.Exchange(ref _watchdogStage, 1)` preserved in catch block

### INV-1.4: Hard-Link Sync
- Must run `deploy-sync.ps1` after changes

### INV-1.5: Verbatim Print Preservation
- All 4 Print statements in target function preserved verbatim
- Total file Print count: 14 (unchanged)

### INV-5.2: Early-Return Guards (SAFETY-CRITICAL)
**MUST preserve verbatim at residual level**:
```csharp
if (masterAccount == null || Instrument == null)
    return;
if (!HasWatchdogLeadAccountWorkingOrder())
{
    Interlocked.Exchange(ref _watchdogStage, 0);
    return;
}
```
**Rationale**: Same predicate-inversion risk as T12. Guards protect against null-ref and unnecessary work.

### INV-5.3: Interlocked Gate Location (CRITICAL)
**The `Interlocked.CompareExchange(ref _watchdogStage, 2, 1)` gate MUST remain at the call site (line 84).**  
**DO NOT move inside `ExecuteWatchdogDirectFallback`.**  
**Rationale**: Moving the gate inside would break single-fire guarantee under timer race. The gate ensures only one thread transitions stage 1→2 and executes the fallback.

### INV-5.5: Escalation Print (VERBATIM)
**Line 86**: `Print("[WATCHDOG] Escalating to direct master close fallback.");`  
**Must remain at call site, count=1 after extraction**

---

## 4. Implementation Steps

### Step 1: Extract `CancelDirectFallbackOrders`
- Place after `ExecuteWatchdogDirectFallback` (line 299+)
- Copy lines 234-257 logic
- Add signature: `private void CancelDirectFallbackOrders(Account masterAccount, string instrumentName)`
- Preserve Print statement verbatim

### Step 2: Extract `FlattenDirectFallbackPositions`
- Place after `CancelDirectFallbackOrders`
- Copy lines 259-291 logic
- Add signature: `private void FlattenDirectFallbackPositions(Account masterAccount, string instrumentName)`
- Preserve both Print statements verbatim

### Step 3: Refactor Residual
- Keep early-return guards verbatim (lines 223-230)
- Replace Phase A with: `CancelDirectFallbackOrders(masterAccount, instrumentName);`
- Replace Phase B with: `FlattenDirectFallbackPositions(masterAccount, instrumentName);`
- Preserve try/catch structure
- Preserve catch block logic verbatim

### Step 4: Verify Invariants
- Check ASCII compliance
- Verify Print count: 14 (unchanged)
- Verify escalation Print at line 86 (unchanged)
- Verify Interlocked gate at call site (unchanged)
- Verify early-return guards at residual level

### Step 5: Complexity Verification
- Run jcodemunch complexity analysis
- Target: Residual CYC ≤19, helpers CYC ≤19
- Expected: Residual CYC=2, Helper1 CYC=8, Helper2 CYC=10

---

## 5. Acceptance Criteria

1. ✅ Residual `ExecuteWatchdogDirectFallback` measures CYC ≤19
2. ✅ Sub-helpers `CancelDirectFallbackOrders` and `FlattenDirectFallbackPositions` measure CYC ≤19
3. ✅ Function removed from "CYC > 20" list
4. ✅ Early-return guards remain at residual level as verbatim `return;` statements
5. ✅ `Interlocked.CompareExchange(ref _watchdogStage, 2, 1)` gate remains at call site (line 84)
6. ✅ Escalation Print at line 86 unchanged (count=1)
7. ✅ Total Print count in file: 14 (unchanged)
8. ✅ T12's extracted helpers untouched in diff
9. ✅ BUILD_TAG bumped to `1111.007-phase7-t15`
10. ✅ F5 test: BUILD_TAG verified, watchdog stage-2 fires exactly once under artificial deadlock

---

## 6. Risk Assessment

**Risk Level**: HIGH (SAFETY-CRITICAL)  
**Mitigation**:
- Zero behavior change (pure refactor)
- Preserve all early-return guards verbatim
- Preserve Interlocked gate at call site
- Preserve all Print statements verbatim
- Sequential commit after T12 (avoid merge conflict)
- F5 test with artificial deadlock trigger

**Blast Radius**: Minimal (single caller, timer-thread execution, not on trading hot path)

---

## 7. Verification Plan

### Build Verification
```powershell
powershell -File .\scripts\build_readiness.ps1
```

### Complexity Verification
```
jcodemunch get_symbol_complexity --repo universal-or-strategy --symbol_id "src/V12_002.Safety.Watchdog.cs::V12_002.ExecuteWatchdogDirectFallback#method"
jcodemunch get_symbol_complexity --repo universal-or-strategy --symbol_id "src/V12_002.Safety.Watchdog.cs::V12_002.CancelDirectFallbackOrders#method"
jcodemunch get_symbol_complexity --repo universal-or-strategy --symbol_id "src/V12_002.Safety.Watchdog.cs::V12_002.FlattenDirectFallbackPositions#method"
```

### Print Verification
```powershell
Select-String -Path "src/V12_002.Safety.Watchdog.cs" -Pattern "Print\(" | Measure-Object | Select-Object -ExpandProperty Count
# Expected: 14

Select-String -Path "src/V12_002.Safety.Watchdog.cs" -Pattern "Escalating to direct master close fallback"
# Expected: 1 match at line 86
```

### Hard-Link Sync
```powershell
powershell -File .\deploy-sync.ps1
```

### F5 Test
1. Press F5 in NinjaTrader
2. Verify BUILD_TAG `1111.007-phase7-t15` in Output
3. Normal operation: Verify watchdog stage remains 0 (no deadlock)
4. Artificial deadlock (if Director approves): Verify stage-2 fires exactly once, escalation Print appears exactly once

---

## 8. Rollback Plan

If extraction fails:
1. Revert `src/V12_002.Safety.Watchdog.cs` to pre-T15 state
2. Revert BUILD_TAG to `1111.007-phase7-t14`
3. Run `deploy-sync.ps1`
4. Document failure in `docs/brain/phase7_sprint5_t15_ROLLBACK.md`

---

**EXTRACT-GATE**: APPROVED for implementation  
**Architect**: Claude Opus 4.7  
**Safety Review**: PASS (all INV-5.x constraints documented and mitigated)