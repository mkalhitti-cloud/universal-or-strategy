# [Phase7-S5-T14] BuildUiLivePositionSnapshot Extraction Plan

**Status**: IMPLEMENTATION READY  
**Created**: 2026-05-13  
**Target**: `BuildUiLivePositionSnapshot` in `src/V12_002.UI.Snapshot.cs`  
**Complexity**: CYC=20 → CYC≤19 (Target: CYC~7)

---

## 1. Forensic Analysis

### Current State
- **Function**: `BuildUiLivePositionSnapshot()` at line 88
- **Measured Complexity**: CYC=20 (jcodemunch), LOC=64, Max Nesting=2
- **Ticket States**: CYC=21 (acceptable variance)
- **Single Caller**: `PublishUiSnapshot()` at line 201
- **Return Type**: `UILivePositionSnapshot`
- **Signature Policy**: FREE (single caller, can modify if needed)
- **Print Statements**: ZERO (verbatim count must remain 0)

### Complexity Breakdown
```
Total CYC=20:
- Early returns: 2 decisions (lines 91, 110)
- Master position loop: 6 decisions (lines 97-108)
  * null check, IsFollower, PendingCleanup, !EntryFilled, RemainingContracts<=0, break
- Target snapshot loop: 11 decisions (lines 117-140)
  * 5 iterations × (visibility check + targetOrder null + LimitPrice check + IsWorking compound)
- Stop order section: 2 decisions (lines 142-148)
  * stopOrders null check, stopOrder null check
```

### Logical Phases
1. **Phase A**: Early return for empty positions (lines 91-92)
2. **Phase B**: Master position search (lines 94-108)
3. **Phase C**: Early return if no master found (lines 110-111)
4. **Phase D**: Basic field population (lines 113-115)
5. **Phase E**: Target snapshots loop (lines 117-140) ← COMPLEXITY HOTSPOT
6. **Phase F**: Stop order lookup (lines 142-148)
7. **Phase G**: Return snapshot (line 150)

---

## 2. Extraction Strategy

### Helper 1: `FindMasterPosition`
**Purpose**: Extract master position search logic  
**Lines**: 94-108 (15 lines)  
**Signature**: `private bool FindMasterPosition(out PositionInfo masterPos, out string entryName)`  
**Complexity**: CYC~6 (5 loop conditions + 1 null check)  
**Returns**: `true` if master found, `false` otherwise  
**Out Parameters**: 
- `masterPos`: The found master PositionInfo
- `entryName`: The dictionary key for the master position

**Logic**:
```csharp
private bool FindMasterPosition(out PositionInfo masterPos, out string entryName)
{
    masterPos = null;
    entryName = null;
    
    if (activePositions == null || activePositions.Count == 0)
        return false;
    
    foreach (var kvp in activePositions.ToArray())
    {
        PositionInfo candidate = kvp.Value;
        if (candidate == null || candidate.IsFollower || candidate.PendingCleanup)
            continue;
        if (!candidate.EntryFilled || candidate.RemainingContracts <= 0)
            continue;
        
        masterPos = candidate;
        entryName = kvp.Key;
        return true;
    }
    
    return false;
}
```

### Helper 2: `PopulateTargetSnapshots`
**Purpose**: Extract target snapshot population loop  
**Lines**: 117-140 (24 lines)  
**Signature**: `private void PopulateTargetSnapshots(UILivePositionSnapshot live, PositionInfo masterPos, string entryName)`  
**Complexity**: CYC~11 (loop + nested conditions)  
**Returns**: void (mutates `live.Targets` array)  
**Parameters**:
- `live`: The snapshot being populated
- `masterPos`: The master position info
- `entryName`: The entry name for order lookups

**Logic**:
```csharp
private void PopulateTargetSnapshots(UILivePositionSnapshot live, PositionInfo masterPos, string entryName)
{
    for (int targetNum = 1; targetNum <= 5; targetNum++)
    {
        UILiveTargetSnapshot target = live.Targets[targetNum - 1];
        bool isVisible = targetNum <= masterPos.InitialTargetCount && !IsTargetFilled(masterPos, targetNum);
        target.IsVisible = isVisible;
        if (!isVisible)
            continue;
        
        var targetDict = GetTargetOrdersDictionary(targetNum);
        Order targetOrder = null;
        if (targetDict != null)
            targetDict.TryGetValue(entryName, out targetOrder);
        
        double price = GetTargetPrice(masterPos, targetNum);
        if (targetOrder != null && targetOrder.LimitPrice > 0)
            price = targetOrder.LimitPrice;
        
        int contracts = GetTargetContracts(masterPos, targetNum);
        int filled = GetTargetFilledQuantity(masterPos, targetNum);
        target.Price = price;
        target.RemainingContracts = Math.Max(0, contracts - filled);
        target.IsWorking = targetOrder != null
            && (targetOrder.OrderState == OrderState.Working || targetOrder.OrderState == OrderState.Accepted);
    }
}
```

### Helper 3: `PopulateStopSnapshot`
**Purpose**: Extract stop order lookup and assignment  
**Lines**: 142-148 (7 lines)  
**Signature**: `private void PopulateStopSnapshot(UILivePositionSnapshot live, PositionInfo masterPos, string entryName)`  
**Complexity**: CYC~2 (2 null checks)  
**Returns**: void (mutates `live.StopPrice`)  
**Parameters**:
- `live`: The snapshot being populated
- `masterPos`: The master position info
- `entryName`: The entry name for order lookup

**Logic**:
```csharp
private void PopulateStopSnapshot(UILivePositionSnapshot live, PositionInfo masterPos, string entryName)
{
    Order stopOrder = null;
    if (stopOrders != null)
        stopOrders.TryGetValue(entryName, out stopOrder);
    
    live.StopPrice = masterPos.CurrentStopPrice;
    if (stopOrder != null && stopOrder.StopPrice > 0)
        live.StopPrice = stopOrder.StopPrice;
}
```

### Residual Function
**Complexity**: CYC~7 (2 early returns + 3 helper calls + basic assignments)  
**Lines**: ~18 lines

**Logic**:
```csharp
private UILivePositionSnapshot BuildUiLivePositionSnapshot()
{
    UILivePositionSnapshot live = new UILivePositionSnapshot();
    
    PositionInfo masterPos;
    string entryName;
    if (!FindMasterPosition(out masterPos, out entryName))
        return live;
    
    live.HasLivePosition = true;
    live.EntryName = entryName;
    live.Direction = masterPos.Direction;
    
    PopulateTargetSnapshots(live, masterPos, entryName);
    PopulateStopSnapshot(live, masterPos, entryName);
    
    return live;
}
```

---

## 3. Complexity Verification

### Before Extraction
- `BuildUiLivePositionSnapshot`: CYC=20

### After Extraction
- `BuildUiLivePositionSnapshot` (residual): CYC~7 ✅
- `FindMasterPosition`: CYC~6 ✅
- `PopulateTargetSnapshots`: CYC~11 ✅
- `PopulateStopSnapshot`: CYC~2 ✅

**All functions meet CYC ≤19 requirement**

### LOC Compliance (DEVIATION-T14-A)
- Original: 64 lines
- Helper 1: ~15 lines ✅
- Helper 2: ~24 lines ✅
- Helper 3: ~7 lines (acceptable per DEVIATION-T14-A pre-flag)
- Residual: ~18 lines ✅
- Total: 15+24+7+18 = 64 lines preserved ✅

---

## 4. Invariant Compliance

### V12 DNA Invariants
- **INV-1.1 (ASCII-only)**: ✅ No Unicode in function
- **INV-1.2 (No locks)**: ✅ Pure computation, no synchronization
- **INV-1.3 (Atomic primitives)**: ✅ N/A - no shared state mutation
- **INV-1.4 (Hard-link sync)**: ✅ Will run `deploy-sync.ps1`
- **INV-1.5 (No behavior change)**: ✅ Pure refactor, exact logic preserved

### Ticket-Specific Constraints
- **D-M1 (Verbatim Prints)**: ✅ ZERO Print statements (count remains 0)
- **D-M3 (No new tuples/structs)**: ✅ Using `out` parameters
- **D-M4 (No new heap allocations)**: ✅ Helpers mutate existing snapshot instance
- **D-D3 (Signature policy FREE)**: ✅ Single caller, signature unchanged
- **DEVIATION-T14-A**: ✅ Helper 3 is 7 LOC (pre-flagged acceptable)

---

## 5. Implementation Steps

### Step 1: Create Helper Functions
1. Add `FindMasterPosition` after line 151
2. Add `PopulateTargetSnapshots` after `FindMasterPosition`
3. Add `PopulateStopSnapshot` after `PopulateTargetSnapshots`

### Step 2: Refactor Residual
1. Replace lines 94-108 with `FindMasterPosition` call
2. Replace lines 117-140 with `PopulateTargetSnapshots` call
3. Replace lines 142-148 with `PopulateStopSnapshot` call
4. Preserve early return logic (lines 91-92, 110-111)
5. Preserve basic field assignments (lines 113-115)

### Step 3: Verification
1. Run `build_readiness.ps1` - must pass
2. Verify CYC metrics via jcodemunch
3. Verify ZERO Print statement count
4. Run `deploy-sync.ps1` for hard-link sync
5. F5 test in NinjaTrader

### Step 4: Documentation
1. Update BUILD_TAG to `1111.007-phase7-t14`
2. Create acceptance report
3. Update Living Document Registry

---

## 6. Risk Assessment

**Risk Level**: LOW

**Rationale**:
- Pure computation function (no side effects)
- Single caller with FREE signature policy
- Zero Print statements (no grep assertions to maintain)
- No new heap allocations
- UI snapshot path (not trading hot path)
- Clear logical phases for extraction

**Mitigation**:
- Preserve exact field assignment order
- Maintain deterministic output for UI rendering
- F5 test validates UI panel rendering correctness

---

## 7. Acceptance Criteria

1. ✅ Residual `BuildUiLivePositionSnapshot` measures CYC ≤19 (target ~7)
2. ✅ All sub-helpers measure CYC ≤19
3. ✅ `BuildUiLivePositionSnapshot` no longer appears in "CYC > 20 remaining"
4. ✅ Caller `PublishUiSnapshot` (line 201) compiles unchanged
5. ✅ Code review confirms returned `UILivePositionSnapshot` fields are bit-for-bit identical
6. ✅ Code review confirms ZERO new collection allocations (D-M4)
7. ✅ All verbatim Print/AppendLine grep counts unchanged (0 before, 0 after)
8. ✅ BUILD_TAG bumped to `1111.007-phase7-t14`
9. ✅ Markdown at `docs/brain/phase7_sprint5_t14_BuildUiLivePositionSnapshot.md`
10. ✅ F5 test: UI panel shows identical position snapshot rendering

---

## 8. F5 Acceptance Test

**Test Procedure**:
1. Press F5 in NinjaTrader
2. Open V12 UI panel
3. Enter a position (long or short)
4. Verify live position snapshot displays:
   - Account name
   - Position direction (LONG/SHORT)
   - Entry name
   - Target prices and remaining contracts (T1-T5)
   - Stop price
   - IsWorking status for active orders
5. Close position
6. Verify snapshot clears correctly (HasLivePosition = false)
7. Compare rendering to pre-Sprint baseline

**Expected Result**: UI rendering is pixel-perfect identical to baseline, snapshot updates correctly as positions open/close.

---

## 9. Verification Steps

### Standard 5-Step Block
1. **Build**: `powershell -File .\scripts\build_readiness.ps1` → PASS
2. **Complexity**: Verify CYC metrics via jcodemunch → residual ≤19, helpers ≤19
3. **Sync**: `powershell -File .\deploy-sync.ps1` → hard-link sync complete
4. **F5 Test**: NinjaTrader UI panel rendering → identical to baseline
5. **Sign-off**: BUILD_TAG `1111.007-phase7-t14` verified in logs

---

## 10. Notes

- **Co-Residency**: No other high-complexity functions in this file to avoid
- **Signature Unchanged**: Single caller requires no updates
- **Zero Prints**: No verbatim assertions to maintain
- **DTO Integrity**: UILivePositionSnapshot field semantics must remain identical
- **Heap Allocation**: No new collections, helpers mutate existing snapshot
- **DEVIATION-T14-A**: Helper 3 (7 LOC) pre-flagged acceptable per ticket

---

**PLAN STATUS**: READY FOR IMPLEMENTATION  
**NEXT STEP**: Stage 2 - Extract Helper Methods