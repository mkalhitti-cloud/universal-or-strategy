# TICKET M3-B: HandleFleetStopFill Extraction Plan

**Status:** PLAN-ONLY (Implementation Pending)  
**Target File:** `src/V12_002.UI.Compliance.cs`  
**Target Method:** `HandleFleetStopFill` (Lines 367-407)  
**Current Complexity:** CYC=21, LOC=29  
**Target Complexity:** Residual CYC ≤ 5, Helpers CYC ≤ 15

---

## 1. CURRENT METHOD ANALYSIS

### 1.1 Method Signature & Context
```csharp
private void HandleFleetStopFill(
    QueuedAccountExecution item, 
    Order ocoOrder, 
    Account ocoAcct, 
    string ocoName)
```

**Threading Context:**
- Called from `ProcessQueuedExecution_HandleFleetOCO` (line 465)
- Executes on **STRATEGY THREAD** (marshaled via `TriggerCustomEvent`)
- Original execution arrives on **BROKER THREAD** via `OnAccountExecutionUpdate`
- Thread-safe: Uses `ConcurrentDictionary` operations (TryRemove, TryGetValue)

**Callback Chain:**
```
OnAccountExecutionUpdate (BROKER THREAD)
  → _accountExecutionQueue.Enqueue
  → TriggerCustomEvent → ProcessAccountExecutionQueue (STRATEGY THREAD)
    → ProcessQueuedExecution
      → ProcessQueuedExecution_HandleFleetOCO
        → HandleFleetStopFill ← WE ARE HERE
```

### 1.2 Execution Flow Analysis

The method has **TWO DISTINCT PHASES** with critical ordering:

#### **PHASE 1: Cancel Orphaned Targets (Lines 369-383)**
```
FOR EACH order in ocoAcct.Orders.ToArray():
  IF order matches instrument AND is Working/Accepted:
    IF order.Name starts with "T1_", "T2_", "T3_", "T4_", or "T5_":
      CancelOrderOnAccount(order, ocoAcct)
      cancelledTargets++
IF cancelledTargets > 0:
  Print confirmation message
```

**Branching Structure:**
- Outer loop: `foreach` over account orders
- Branch 1: Instrument match check (`o.Instrument?.FullName != Instrument?.FullName`)
- Branch 2: Order state check (`o.OrderState != Working/Accepted`)
- Branch 3: Target name prefix check (5 StartsWith conditions OR'd together)
- Branch 4: Print guard (`cancelledTargets > 0`)

**Cyclomatic Complexity Breakdown (Phase 1):**
- Base: 1
- `foreach` loop: +1
- `if (o == null)`: +1
- `if (o.Instrument?.FullName != Instrument?.FullName)`: +1
- `if (o.OrderState != Working && o.OrderState != Accepted)`: +2 (compound)
- `if (o.Name != null)`: +1
- `if (o.Name.StartsWith("T1_"))`: +1
- `|| o.Name.StartsWith("T2_")`: +1
- `|| o.Name.StartsWith("T3_")`: +1
- `|| o.Name.StartsWith("T4_")`: +1
- `|| o.Name.StartsWith("T5_")`: +1
- `if (cancelledTargets > 0)`: +1
- **Phase 1 Subtotal: CYC = 13**

#### **PHASE 2: Update Position State (Lines 385-406)**
```
_nakedPositionFirstSeen.TryRemove(ocoAcct.Name, out _)

Extract entry key from ocoName (strip "Stop_" prefix + trailing segment)

IF activePositions.TryGetValue(ocoEntryKey, out ocoPos) AND ocoPos != null:
  stopQty = execution.Quantity
  ocoPos.RemainingContracts -= stopQty
  
  IF ocoPos.RemainingContracts <= 0:
    stopOrders.TryRemove(ocoEntryKey, out _)
    IF pendingStopReplacements.TryRemove(ocoEntryKey, out _):
      Interlocked.Decrement(ref pendingReplacementCount)
    activePositions.TryRemove(ocoEntryKey, out _)
    entryOrders.TryRemove(ocoEntryKey, out _)
    SymmetryGuardForgetEntry(ocoEntryKey)
    Print full-close message
```

**Branching Structure:**
- Entry key extraction: 2 substring operations (deterministic, no branches)
- Branch 5: `if (!string.IsNullOrEmpty(ocoEntryKey) && activePositions.TryGetValue(...) && ocoPos != null)`: +3 (compound AND)
- Branch 6: `if (ocoPos.RemainingContracts <= 0)`: +1
- Branch 7: `if (pendingStopReplacements.TryRemove(...))`: +1

**Cyclomatic Complexity Breakdown (Phase 2):**
- Entry key extraction: 0 (no branches)
- `if (!string.IsNullOrEmpty && TryGetValue && != null)`: +3
- `if (RemainingContracts <= 0)`: +1
- `if (pendingStopReplacements.TryRemove)`: +1
- **Phase 2 Subtotal: CYC = 5**

**Total Method CYC: 1 (base) + 13 (Phase 1) + 5 (Phase 2) = 19**  
*(Note: Reported CYC=21 likely includes additional tool-specific counting rules)*

### 1.3 Critical Ordering Constraints

**CONSTRAINT 1: Phase Ordering (NON-NEGOTIABLE)**
- Phase 1 (cancel targets) MUST complete BEFORE Phase 2 (update position state)
- Rationale: Prevents race where position is removed from `activePositions` while target cancellations are still iterating

**CONSTRAINT 2: Dictionary Operation Ordering (Phase 2)**
```
Order of operations when RemainingContracts <= 0:
1. stopOrders.TryRemove          ← Remove stop first
2. pendingStopReplacements check ← Clean pending replacements
3. activePositions.TryRemove     ← Remove position metadata
4. entryOrders.TryRemove         ← Remove entry order
5. SymmetryGuardForgetEntry      ← Clean symmetry tracking
```
**Rationale:** This ordering prevents other threads from observing inconsistent state (e.g., position exists but stop is missing).

**CONSTRAINT 3: Atomic Quantity Update**
```csharp
int stopQty = Math.Max(0, item.EventArgs.Execution.Quantity);
ocoPos.RemainingContracts = Math.Max(0, ocoPos.RemainingContracts - stopQty);
```
- `RemainingContracts` is marked `volatile` in `PositionInfo` (line 47 of PositionInfo.cs)
- Update must be atomic to prevent torn reads from other threads (OnBarUpdate, ManageTrail)

**CONSTRAINT 4: Account-Specific Cancellation**
- `CancelOrderOnAccount(o, ocoAcct)` uses the **executing account** from the fill event
- Critical for SIMA fleet: each follower account has its own order set
- Cannot use `CancelOrder(o)` which defaults to `this.Account`

---

## 2. EXTRACTION STRATEGY

### 2.1 Identified Helper Methods

#### **Helper 1: CancelOrphanedTargets**
```csharp
private int CancelOrphanedTargets(Account account)
```
**Purpose:** Cancel all working target orders (T1-T5) for the specified account  
**Responsibility:** Phase 1 logic extraction  
**Returns:** Count of cancelled targets  
**Expected CYC:** 11 (loop + 5 prefix checks + guards)

**Signature Rationale:**
- Takes `Account` instead of full execution context (minimal coupling)
- Returns `int` for logging (preserves existing behavior)
- No `Order` or `string ocoName` needed (self-contained scan)

#### **Helper 2: ExtractEntryKeyFromStopName**
```csharp
private string ExtractEntryKeyFromStopName(string stopOrderName)
```
**Purpose:** Parse entry key from stop order name (strip "Stop_" prefix + trailing segment)  
**Responsibility:** Entry key extraction logic  
**Returns:** Entry key string (empty if invalid)  
**Expected CYC:** 3 (length check + 2 substring ops)

**Signature Rationale:**
- Pure function (no side effects)
- Reusable across other stop-handling methods
- Encapsulates the "strip prefix + strip trailing underscore segment" pattern

#### **Helper 3: FinalizeStopFilledPosition**
```csharp
private void FinalizeStopFilledPosition(
    string entryKey, 
    PositionInfo pos, 
    int filledQuantity)
```
**Purpose:** Update position state after stop fill, clean up if fully closed  
**Responsibility:** Phase 2 logic extraction  
**Returns:** void (state mutation)  
**Expected CYC:** 4 (TryGetValue guard + RemainingContracts check + pendingReplacements check)

**Signature Rationale:**
- Takes pre-validated `PositionInfo` (caller already did TryGetValue)
- `filledQuantity` explicit parameter (no execution context coupling)
- Encapsulates the "decrement → check zero → cleanup" pattern

### 2.2 Residual Router

```csharp
private void HandleFleetStopFill(
    QueuedAccountExecution item, 
    Order ocoOrder, 
    Account ocoAcct, 
    string ocoName)
{
    // Phase 1: Cancel orphaned targets
    int cancelledTargets = CancelOrphanedTargets(ocoAcct);
    if (cancelledTargets > 0)
        Print(string.Format("[1104.1 OCO] Fleet {0}: stop filled -- cancelled {1} orphaned targets.",
            ocoAcct.Name, cancelledTargets));

    // Phase 2: Update position state
    _nakedPositionFirstSeen.TryRemove(ocoAcct.Name, out _);

    string ocoEntryKey = ExtractEntryKeyFromStopName(ocoName);
    if (string.IsNullOrEmpty(ocoEntryKey)) return;

    PositionInfo ocoPos;
    if (!activePositions.TryGetValue(ocoEntryKey, out ocoPos) || ocoPos == null) return;

    int stopQty = Math.Max(0, item.EventArgs.Execution.Quantity);
    FinalizeStopFilledPosition(ocoEntryKey, ocoPos, stopQty);
}
```

**Residual CYC:** 1 (base) + 1 (if cancelledTargets) + 1 (if IsNullOrEmpty) + 1 (if TryGetValue) + 1 (if ocoPos == null) = **5** ✓

---

## 3. DETAILED HELPER SPECIFICATIONS

### 3.1 Helper 1: CancelOrphanedTargets

```csharp
/// <summary>
/// Cancel all working target orders (T1-T5) for the specified fleet account.
/// Called when a stop order fills to prevent orphaned profit targets.
/// </summary>
/// <param name="account">The fleet account whose targets should be cancelled</param>
/// <returns>Count of cancelled target orders</returns>
private int CancelOrphanedTargets(Account account)
{
    int cancelledTargets = 0;
    foreach (Order o in account.Orders.ToArray())
    {
        if (o == null || o.Instrument?.FullName != Instrument?.FullName) continue;
        if (o.OrderState != OrderState.Working && o.OrderState != OrderState.Accepted) continue;
        if (o.Name != null && (o.Name.StartsWith("T1_") || o.Name.StartsWith("T2_") ||
            o.Name.StartsWith("T3_") || o.Name.StartsWith("T4_") || o.Name.StartsWith("T5_")))
        {
            CancelOrderOnAccount(o, account);
            cancelledTargets++;
        }
    }
    return cancelledTargets;
}
```

**Complexity Analysis:**
- Base: 1
- `foreach`: +1
- `if (o == null)`: +1
- `if (o.Instrument?.FullName != Instrument?.FullName)`: +1
- `if (o.OrderState != Working && o.OrderState != Accepted)`: +2
- `if (o.Name != null)`: +1
- `if (o.Name.StartsWith("T1_"))`: +1
- `|| o.Name.StartsWith("T2_")`: +1
- `|| o.Name.StartsWith("T3_")`: +1
- `|| o.Name.StartsWith("T4_")`: +1
- `|| o.Name.StartsWith("T5_")`: +1
- **Total CYC: 13** (within ≤15 limit) ✓

**Threading Safety:**
- `account.Orders.ToArray()` creates snapshot (prevents collection modification exceptions)
- `CancelOrderOnAccount` is thread-safe (uses NinjaTrader's internal locking)
- No shared state mutation (only returns count)

**Zero Allocations:**
- `ToArray()` allocates, but unavoidable for thread-safe iteration
- No string allocations (all StartsWith checks are on existing strings)

### 3.2 Helper 2: ExtractEntryKeyFromStopName

```csharp
/// <summary>
/// Extract the entry key from a stop order name by stripping the "Stop_" prefix
/// and removing the trailing account-specific segment (after last underscore).
/// Example: "Stop_MOMO_1234_Sim101" -> "MOMO_1234"
/// </summary>
/// <param name="stopOrderName">The stop order name (e.g., "Stop_MOMO_1234_Sim101")</param>
/// <returns>Entry key string, or empty string if invalid</returns>
private string ExtractEntryKeyFromStopName(string stopOrderName)
{
    if (string.IsNullOrEmpty(stopOrderName) || stopOrderName.Length <= 5) 
        return string.Empty;

    string ocoEntryKey = stopOrderName.Substring(5); // Strip "Stop_"
    int ocoLastUnderscore = ocoEntryKey.LastIndexOf('_');
    if (ocoLastUnderscore > 0)
        ocoEntryKey = ocoEntryKey.Substring(0, ocoLastUnderscore);

    return ocoEntryKey;
}
```

**Complexity Analysis:**
- Base: 1
- `if (IsNullOrEmpty || Length <= 5)`: +2
- `if (ocoLastUnderscore > 0)`: +1
- **Total CYC: 4** (well within ≤15 limit) ✓

**Threading Safety:**
- Pure function (no shared state access)
- String operations are immutable (thread-safe)

**Zero Allocations:**
- `Substring` allocates new strings (unavoidable for string manipulation)
- Could be optimized with `Span<char>` in future, but not critical path

### 3.3 Helper 3: FinalizeStopFilledPosition

```csharp
/// <summary>
/// Update position state after a stop order fill. Decrements RemainingContracts
/// and performs full cleanup if position is fully closed.
/// </summary>
/// <param name="entryKey">The position entry key</param>
/// <param name="pos">The PositionInfo struct (pre-validated, non-null)</param>
/// <param name="filledQuantity">Quantity filled by the stop order</param>
private void FinalizeStopFilledPosition(string entryKey, PositionInfo pos, int filledQuantity)
{
    int stopQty = Math.Max(0, filledQuantity);
    pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - stopQty);

    if (pos.RemainingContracts <= 0)
    {
        stopOrders.TryRemove(entryKey, out _);
        if (pendingStopReplacements.TryRemove(entryKey, out _))
            Interlocked.Decrement(ref pendingReplacementCount);
        activePositions.TryRemove(entryKey, out _);
        entryOrders.TryRemove(entryKey, out _);
        SymmetryGuardForgetEntry(entryKey);
        Print(string.Format("[1104.1 OCO] Fleet position {0} fully closed by stop.", entryKey));
    }
}
```

**Complexity Analysis:**
- Base: 1
- `if (pos.RemainingContracts <= 0)`: +1
- `if (pendingStopReplacements.TryRemove)`: +1
- **Total CYC: 3** (well within ≤15 limit) ✓

**Threading Safety:**
- `pos.RemainingContracts` is `volatile` (atomic reads/writes)
- `ConcurrentDictionary.TryRemove` is thread-safe
- `Interlocked.Decrement` is atomic
- Ordering of TryRemove operations prevents inconsistent state observation

**Critical Ordering Preserved:**
1. `stopOrders.TryRemove` first (prevents new stop submissions)
2. `pendingStopReplacements` cleanup (prevents replacement attempts)
3. `activePositions.TryRemove` (removes position metadata)
4. `entryOrders.TryRemove` (removes entry order tracking)
5. `SymmetryGuardForgetEntry` (cleans symmetry tracking)

**Zero Allocations:**
- `Math.Max` is inlined (no allocation)
- `TryRemove` operations reuse existing dictionary infrastructure
- `Print` allocates string, but only on full-close path (rare)

---

## 4. COMPLEXITY ESTIMATES

### 4.1 Pre-Extraction
- **HandleFleetStopFill:** CYC=21, LOC=29

### 4.2 Post-Extraction
- **HandleFleetStopFill (Residual):** CYC=5, LOC=15
- **CancelOrphanedTargets:** CYC=13, LOC=12
- **ExtractEntryKeyFromStopName:** CYC=4, LOC=8
- **FinalizeStopFilledPosition:** CYC=3, LOC=12

**Total Post-Extraction CYC:** 5 + 13 + 4 + 3 = **25**  
**CYC Increase:** +4 (acceptable for improved maintainability)

**Compliance Check:**
- ✓ Residual CYC ≤ 5
- ✓ All helpers CYC ≤ 15
- ✓ Zero logic change
- ✓ Exact execution sequence preserved

---

## 5. THREADING & STATE MUTATION ANALYSIS

### 5.1 Thread Safety Guarantees

**Current Threading Model:**
- **Broker Thread:** `OnAccountExecutionUpdate` enqueues work
- **Strategy Thread:** `ProcessAccountExecutionQueue` drains queue and calls `HandleFleetStopFill`
- **Concurrency:** Multiple broker threads may enqueue simultaneously, but strategy thread processes serially

**Thread-Safe Operations:**
1. `ConcurrentDictionary.TryRemove` (atomic)
2. `ConcurrentDictionary.TryGetValue` (atomic)
3. `Interlocked.Decrement` (atomic)
4. `pos.RemainingContracts` (volatile field, atomic read/write)

**Non-Thread-Safe Operations (Strategy Thread Only):**
1. `account.Orders.ToArray()` (NinjaTrader internal locking)
2. `CancelOrderOnAccount` (NinjaTrader internal locking)
3. `SymmetryGuardForgetEntry` (assumes strategy thread)

### 5.2 State Mutation Concerns

**Shared State Modified:**
1. `_nakedPositionFirstSeen` (ConcurrentDictionary) - TryRemove is atomic
2. `stopOrders` (ConcurrentDictionary) - TryRemove is atomic
3. `pendingStopReplacements` (ConcurrentDictionary) - TryRemove is atomic
4. `activePositions` (ConcurrentDictionary) - TryRemove is atomic
5. `entryOrders` (ConcurrentDictionary) - TryRemove is atomic
6. `pendingReplacementCount` (int) - Interlocked.Decrement is atomic
7. `pos.RemainingContracts` (volatile int) - atomic read/write

**No New Allocations:**
- Extraction does not introduce new heap allocations beyond existing `Substring` calls
- `ToArray()` snapshot already exists in current implementation

**No Lock Introduction:**
- All operations remain lock-free (ConcurrentDictionary + Interlocked + volatile)
- Preserves V12 DNA: "Lock-Free Actor Pattern"

### 5.3 Enqueue Strategy Verification

**Current Implementation:** Direct dictionary writes (no Enqueue)
```csharp
stopOrders.TryRemove(ocoEntryKey, out _);
activePositions.TryRemove(ocoEntryKey, out _);
entryOrders.TryRemove(ocoEntryKey, out _);
```

**Rationale for No Enqueue:**
- Already executing on strategy thread (marshaled via `TriggerCustomEvent`)
- `TryRemove` operations are atomic and thread-safe
- No risk of cross-thread mutation (broker thread only enqueues, never mutates)

**Extraction Preserves This:**
- `FinalizeStopFilledPosition` maintains direct TryRemove calls
- No Enqueue wrapper needed (already on correct thread)

---

## 6. RISK ASSESSMENT

### 6.1 Low Risk Factors ✓
1. **Pure Extraction:** No logic changes, only code movement
2. **Preserved Ordering:** Phase 1 → Phase 2 sequence maintained
3. **Atomic Operations:** All dictionary ops remain atomic
4. **Thread Model:** No changes to threading strategy
5. **Zero New Allocations:** No new heap pressure

### 6.2 Medium Risk Factors ⚠️
1. **Helper Signature Design:** Must ensure correct parameter passing
   - **Mitigation:** Explicit parameters (no implicit `this` state)
2. **Entry Key Extraction:** String manipulation edge cases
   - **Mitigation:** Guard against null/empty, length checks
3. **Partial Fill Handling:** `RemainingContracts` arithmetic
   - **Mitigation:** `Math.Max(0, ...)` guards prevent negative values

### 6.3 High Risk Factors ❌
**NONE IDENTIFIED**

### 6.4 Verification Strategy

**Pre-Implementation Checks:**
1. ✓ Residual CYC ≤ 5
2. ✓ Helper CYC ≤ 15
3. ✓ No new locks introduced
4. ✓ No new allocations (beyond existing Substring)
5. ✓ Execution order preserved

**Post-Implementation Verification:**
1. **Unit Test:** Simulate stop fill with 5 working targets → verify all cancelled
2. **Unit Test:** Simulate partial stop fill → verify `RemainingContracts` decremented correctly
3. **Unit Test:** Simulate full stop fill → verify all dictionaries cleaned up
4. **Integration Test:** Run with live fleet accounts → verify no orphaned targets
5. **Stress Test:** Rapid stop fills → verify no race conditions or double-cleanup

**Acceptance Criteria:**
- [ ] All unit tests pass
- [ ] Integration test shows zero orphaned targets after stop fills
- [ ] Stress test shows zero exceptions or state corruption
- [ ] CYC metrics match estimates (Residual=5, Helpers≤15)
- [ ] Zero new compiler warnings
- [ ] `deploy-sync.ps1` succeeds (hard-link integrity)

---

## 7. IMPLEMENTATION PSEUDO-CODE

### 7.1 Step-by-Step Extraction

**Step 1: Create Helper 1 (CancelOrphanedTargets)**
```csharp
// Add to V12_002.UI.Compliance.cs after HandleFleetStopFill
private int CancelOrphanedTargets(Account account)
{
    int cancelledTargets = 0;
    foreach (Order o in account.Orders.ToArray())
    {
        if (o == null || o.Instrument?.FullName != Instrument?.FullName) continue;
        if (o.OrderState != OrderState.Working && o.OrderState != OrderState.Accepted) continue;
        if (o.Name != null && (o.Name.StartsWith("T1_") || o.Name.StartsWith("T2_") ||
            o.Name.StartsWith("T3_") || o.Name.StartsWith("T4_") || o.Name.StartsWith("T5_")))
        {
            CancelOrderOnAccount(o, account);
            cancelledTargets++;
        }
    }
    return cancelledTargets;
}
```

**Step 2: Create Helper 2 (ExtractEntryKeyFromStopName)**
```csharp
// Add to V12_002.UI.Compliance.cs after CancelOrphanedTargets
private string ExtractEntryKeyFromStopName(string stopOrderName)
{
    if (string.IsNullOrEmpty(stopOrderName) || stopOrderName.Length <= 5) 
        return string.Empty;

    string ocoEntryKey = stopOrderName.Substring(5);
    int ocoLastUnderscore = ocoEntryKey.LastIndexOf('_');
    if (ocoLastUnderscore > 0)
        ocoEntryKey = ocoEntryKey.Substring(0, ocoLastUnderscore);

    return ocoEntryKey;
}
```

**Step 3: Create Helper 3 (FinalizeStopFilledPosition)**
```csharp
// Add to V12_002.UI.Compliance.cs after ExtractEntryKeyFromStopName
private void FinalizeStopFilledPosition(string entryKey, PositionInfo pos, int filledQuantity)
{
    int stopQty = Math.Max(0, filledQuantity);
    pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - stopQty);

    if (pos.RemainingContracts <= 0)
    {
        stopOrders.TryRemove(entryKey, out _);
        if (pendingStopReplacements.TryRemove(entryKey, out _))
            Interlocked.Decrement(ref pendingReplacementCount);
        activePositions.TryRemove(entryKey, out _);
        entryOrders.TryRemove(entryKey, out _);
        SymmetryGuardForgetEntry(entryKey);
        Print(string.Format("[1104.1 OCO] Fleet position {0} fully closed by stop.", entryKey));
    }
}
```

**Step 4: Replace HandleFleetStopFill Body**
```csharp
private void HandleFleetStopFill(QueuedAccountExecution item, Order ocoOrder, Account ocoAcct, string ocoName)
{
    // Phase 1: Cancel orphaned targets
    int cancelledTargets = CancelOrphanedTargets(ocoAcct);
    if (cancelledTargets > 0)
        Print(string.Format("[1104.1 OCO] Fleet {0}: stop filled -- cancelled {1} orphaned targets.",
            ocoAcct.Name, cancelledTargets));

    // Phase 2: Update position state
    _nakedPositionFirstSeen.TryRemove(ocoAcct.Name, out _);

    string ocoEntryKey = ExtractEntryKeyFromStopName(ocoName);
    if (string.IsNullOrEmpty(ocoEntryKey)) return;

    PositionInfo ocoPos;
    if (!activePositions.TryGetValue(ocoEntryKey, out ocoPos) || ocoPos == null) return;

    int stopQty = Math.Max(0, item.EventArgs.Execution.Quantity);
    FinalizeStopFilledPosition(ocoEntryKey, ocoPos, stopQty);
}
```

---

## 8. FINAL CHECKLIST

### 8.1 Platinum Standard Compliance
- [x] **Correctness by Construction:** Entry key extraction guards against null/empty
- [x] **Lock-Free Actor Pattern:** No locks introduced, all ConcurrentDictionary ops preserved
- [x] **ASCII-Only:** No Unicode characters in code or comments
- [x] **Zero New Allocations:** Only existing `Substring` calls (unavoidable)
- [x] **Thread Isolation:** Already on strategy thread, no Enqueue needed
- [x] **Residual CYC ≤ 5:** Achieved (CYC=5)
- [x] **Helper CYC ≤ 15:** All helpers within limit (13, 4, 3)

### 8.2 V12 DNA Preservation
- [x] **Execution Order:** Phase 1 → Phase 2 sequence preserved
- [x] **Dictionary Cleanup Order:** stopOrders → pendingReplacements → activePositions → entryOrders → SymmetryGuard
- [x] **Atomic Operations:** All TryRemove/TryGetValue remain atomic
- [x] **Volatile Field:** `pos.RemainingContracts` update preserved
- [x] **Account-Specific Cancellation:** `CancelOrderOnAccount(o, account)` preserved

### 8.3 Implementation Readiness
- [x] Helper signatures designed
- [x] Complexity estimates calculated
- [x] Threading analysis complete
- [x] Risk assessment complete
- [x] Verification strategy defined
- [x] Pseudo-code provided

---

## 9. CONCLUSION

**Extraction Feasibility:** ✅ **APPROVED FOR IMPLEMENTATION**

**Key Insights:**
1. Method has clear two-phase structure (cancel targets → update position)
2. Phase ordering is critical and must be preserved
3. All operations are already thread-safe (ConcurrentDictionary + volatile)
4. No Enqueue needed (already on strategy thread)
5. Extraction reduces residual complexity from CYC=21 to CYC=5

**Next Steps:**
1. Switch to Code mode (`/mode code` or Bob CLI `v12-engineer`)
2. Implement helpers in order: CancelOrphanedTargets → ExtractEntryKeyFromStopName → FinalizeStopFilledPosition
3. Replace HandleFleetStopFill body with residual router
4. Run `powershell -File .\deploy-sync.ps1` to sync hard links
5. Execute verification tests (unit → integration → stress)

**Estimated Implementation Time:** 15-20 minutes (straightforward extraction, no logic changes)

---

**Plan Status:** COMPLETE ✓  
**Architect:** Bob (Plan Mode)  
**Date:** 2026-05-15  
**Build Target:** V12.44+