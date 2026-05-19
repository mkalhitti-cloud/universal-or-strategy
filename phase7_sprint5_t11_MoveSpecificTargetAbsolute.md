# [Phase7-S5-T11] MoveSpecificTargetAbsolute CYC Reduction

**Status**: Implementation Plan  
**BUILD_TAG Target**: `1111.007-phase7-t11`  
**Date**: 2026-05-13

---

## 1. Forensic Analysis

### 1.1 Current State
- **Location**: `src/V12_002.Trailing.Breakeven.cs:294-381`
- **Current CYC**: 28 (actual measurement, higher than spec's 25)
- **Current LOC**: 88
- **Max Nesting**: 6
- **Target CYC**: ≤19
- **Callers**: 1 direct caller at `src/V12_002.UI.IPC.Commands.Fleet.cs:513`

### 1.2 Method Structure Analysis

The method `MoveSpecificTargetAbsolute` performs the following operations:

1. **Input Validation** (Lines 296-297): Validates targetNum range and absolutePrice
2. **Position Collection Check** (Line 297): Validates activePositions exists
3. **Position Loop** (Lines 299-380): Iterates through all active positions
   - Position state validation (Line 304)
   - **Target Order Lookup** (Lines 307-323): Finds the specific target order
   - **Price Rounding** (Line 330)
   - **Direction Safety Validation** (Lines 333-346): Validates price vs entry based on direction
   - **Order Modification** (Lines 348-377): Executes modification with master/follower branching
     - Follower: FSM-based two-phase replacement (Lines 352-370)
     - Master: Atomic ChangeOrder (Lines 373-376)
   - Error handling (Lines 378-380)

### 1.3 Complexity Drivers

Primary complexity sources (CYC=28):
- Outer foreach loop (+1)
- Multiple nested conditionals for validation (+6)
- Order search loop (+1)
- Direction-based price validation (+2)
- Master/Follower branching (+2)
- Try-catch block (+1)
- Multiple continue statements (+5)
- Nested if conditions within loops (+10)

### 1.4 Extraction Strategy

**Approach**: Extract 3 sub-helpers following D-D3 (FREE signature policy):

1. **`ValidateTargetMoveAbsoluteRequest`** (CYC ~4): Consolidate input validation
2. **`FindTargetOrderForAbsoluteMove`** (CYC ~8): Extract order lookup logic with loop
3. **`ExecuteTargetAbsoluteMove`** (CYC ~12): Handle direction validation + order modification

**Residual**: Thin dispatcher (CYC ~6) that orchestrates the three helpers within the position loop.

**Rationale**: The position loop must remain in the residual since it's the primary iteration structure. We extract the three major sub-operations within each iteration.

---

## 2. Implementation Plan

### 2.1 Sub-Helper 1: ValidateTargetMoveAbsoluteRequest

**Purpose**: Validate input parameters before processing.

**Signature**: 
```csharp
private bool ValidateTargetMoveAbsoluteRequest(int targetNum, double absolutePrice)
```

**Logic**:
- Check targetNum in range [1,5]
- Check absolutePrice > 0
- Check activePositions not null and not empty
- Return true if all valid, false otherwise

**Expected CYC**: ~4

### 2.2 Sub-Helper 2: FindTargetOrderForAbsoluteMove

**Purpose**: Locate the working target order for a given position and target number.

**Signature**:
```csharp
private Order FindTargetOrderForAbsoluteMove(
    PositionInfo pos, 
    string entryName, 
    int targetNum, 
    out Account searchAcct)
```

**Logic**:
- Build target order name
- Determine search account (follower vs master)
- Loop through orders to find matching working order
- Return order or null

**Expected CYC**: ~8 (includes loop and conditionals)

### 2.3 Sub-Helper 3: ExecuteTargetAbsoluteMove

**Purpose**: Validate direction safety and execute the order modification.

**Signature**:
```csharp
private bool ExecuteTargetAbsoluteMove(
    PositionInfo pos,
    Order targetOrder,
    int targetNum,
    double newPrice,
    string entryName,
    Account searchAcct)
```

**Logic**:
- Round price to tick size
- Validate direction safety (long: price > entry, short: price < entry)
- Branch on master vs follower
  - Follower: Queue FSM spec and cancel order
  - Master: Use ChangeOrder
- Handle exceptions
- Return true if successful, false if rejected

**Expected CYC**: ~12 (direction validation + master/follower branching + error handling)

### 2.4 Residual Dispatcher

**Signature**: Unchanged
```csharp
private void MoveSpecificTargetAbsolute(int targetNum, double absolutePrice)
```

**Logic**:
- Call ValidateTargetMoveAbsoluteRequest (early return if false)
- Loop through activePositions
  - Skip if position not valid
  - Call FindTargetOrderForAbsoluteMove
  - If order found, call ExecuteTargetAbsoluteMove

**Expected CYC**: ~6 (validation + loop + helper calls)

---

## 3. Implementation Steps

### Step 1: Extract ValidateTargetMoveAbsoluteRequest
- Insert new method before MoveSpecificTargetAbsolute
- Move validation logic from lines 296-297
- Add activePositions null/empty check

### Step 2: Extract FindTargetOrderForAbsoluteMove
- Insert new method before MoveSpecificTargetAbsolute
- Move order lookup logic from lines 307-323
- Return Order and output searchAcct

### Step 3: Extract ExecuteTargetAbsoluteMove
- Insert new method before MoveSpecificTargetAbsolute
- Move price rounding, direction validation, and modification logic (lines 330-380)
- Return bool for success/failure

### Step 4: Refactor Residual
- Replace extracted sections with helper calls
- Maintain position loop structure
- Preserve all Print statements and error messages

---

## 4. Invariants & Constraints

### 4.1 V12 DNA Cross-Cutting (INV-1.1 .. INV-1.5)
- **INV-1.1**: No `lock()` statements
- **INV-1.2**: ASCII-only string literals
- **INV-1.3**: No Unicode/emoji
- **INV-1.4**: Preserve all Print/AppendLine verbatim
- **INV-1.5**: Hard-link sync via `deploy-sync.ps1`

### 4.2 Signature Policy
- **D-D3 (FREE)**: Single caller allows signature changes if needed
- Current signature is clean and will be preserved

### 4.3 Sequencing
- **D-T1**: T11 commits AFTER T05 (sequential, same file)
- Must not modify T05's extracted helpers

---

## 5. Acceptance Criteria

### 5.1 Complexity Metrics
- [ ] Residual `MoveSpecificTargetAbsolute` CYC ≤19
- [ ] `ValidateTargetMoveAbsoluteRequest` CYC ≤19, LOC ≥15
- [ ] `FindTargetOrderForAbsoluteMove` CYC ≤19, LOC ≥15
- [ ] `ExecuteTargetAbsoluteMove` CYC ≤19, LOC ≥15
- [ ] `MoveSpecificTargetAbsolute` removed from "CYC > 20 remaining" list

### 5.2 Behavioral Preservation
- [ ] Caller at `src/V12_002.UI.IPC.Commands.Fleet.cs:513` unchanged
- [ ] All Print statement counts unchanged (grep verification)
- [ ] T05 extracted helpers untouched in diff
- [ ] Zero logic changes to absolute-price target moves

### 5.3 Build & Tag
- [ ] BUILD_TAG bumped to `1111.007-phase7-t11`
- [ ] Clean build with zero errors
- [ ] `deploy-sync.ps1` executed successfully

### 5.4 F5 Acceptance
"Open UI panel; trigger 'Move Target N to Price' IPC command; verify the specific target order moves to the absolute price specified; check Output for zero ERROR lines."

---

## 6. Verification Steps

1. **Pre-extraction baseline**:
   ```powershell
   # Count Print statements
   Select-String -Path src/V12_002.Trailing.Breakeven.cs -Pattern 'Print\(' | Measure-Object
   
   # Verify current CYC
   python scripts/v12_split.py --analyze src/V12_002.Trailing.Breakeven.cs
   ```

2. **Post-extraction verification**:
   ```powershell
   # Verify CYC reduction
   python scripts/v12_split.py --analyze src/V12_002.Trailing.Breakeven.cs
   
   # Verify Print count unchanged
   Select-String -Path src/V12_002.Trailing.Breakeven.cs -Pattern 'Print\(' | Measure-Object
   
   # Build
   powershell -File .\scripts\build_readiness.ps1
   
   # Sync hard links
   powershell -File .\deploy-sync.ps1
   ```

3. **F5 Test**: Launch NinjaTrader, open strategy UI, trigger absolute price move command

4. **Diff audit**: Verify T05 helpers not in diff, caller unchanged

5. **Sign-off**: Update acceptance report

---

## 7. Verbatim Print Assertions

Will be enumerated during forensic read. Expected count: ~6 Print statements in method.

---

## 8. Risk Assessment

**Risk Level**: LOW
- Single caller (FREE signature)
- Pure refactor, zero logic change
- Well-defined extraction boundaries
- No interaction with T05 helpers

**Mitigation**:
- Preserve exact Print messages
- Maintain position loop structure
- Test with F5 acceptance criterion

---

## 9. References

- **Analysis**: spec:807e80ce-4657-46c6-a10f-0338ea1a907b/ee6c7363-16b7-4be4-85d2-8a48a784743e §1.1 row T11
- **Approach**: spec:807e80ce-4657-46c6-a10f-0338ea1a907b/7d42f7da-0c65-4020-8b2d-40117382d136 §1.4 D-D3
- **T05 Context**: `docs/brain/phase7_sprint5_t05_ACCEPTANCE_REPORT.md`

---

**Status**: Ready for implementation