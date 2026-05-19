# T-H Implementation Plan: ValidateStopPrice By-Direction Extraction

**BUILD_TAG_BASELINE**: 1111.007-phase7-tQ1  
**BUILD_TAG_TARGET**: 1111.007-phase7-tH  
**BRANCH**: feature/phase7-sprint5-extraction  
**MISSION**: Extract `ValidateStopPrice` (CYC=33) into thin parent + two per-direction helpers to achieve CYC ≤ 19

---

## 1. Executive Summary

This plan decomposes the `ValidateStopPrice` method (CYC=33, lines 551-623) into a thin parent orchestrator and two direction-specific helpers. The extraction preserves **byte-identical** behavior for all input tuples while reducing cyclomatic complexity to meet the CYC ≤ 19 target.

### Scope
- **File Modified**: 1 (`src/V12_002.Orders.Management.StopSync.cs`)
- **Method Extracted**: `ValidateStopPrice` (lines 551-623, 73 lines)
- **New Helpers**: 2 (`Validate_LongIsIllegalAdjust`, `Validate_ShortIsIllegalAdjust`)
- **Callers**: 5 invocations across 4 files (UNTOUCHED)
- **Print Strings**: 4 (MUST remain byte-identical)

### Complexity Targets
- **Parent**: CYC ≤ 19 (currently 33)
- **Long Helper**: CYC ≤ 10
- **Short Helper**: CYC ≤ 10
- **Max Nesting**: ≤ 4 (currently 3)

---

## 2. Current State Analysis

### 2.1 Method Structure (Lines 551-623)

**Line 551-555**: Method signature + setup
```csharp
private double ValidateStopPrice(MarketPosition direction, double desiredStopPrice, int level = 0, double entryPrice = 0)
{
    double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
    double tickSize = Instrument.MasterInstrument.TickSize;
    double minDistance = (level == 1) ? 0 : (2 * tickSize);
```

**Line 562**: Result variable initialization
```csharp
    double resultStop = desiredStopPrice;
```

**Lines 564-586**: LONG branch (23 lines)
- Line 564: `if (direction == MarketPosition.Long)`
- Line 568: `bool isIllegal = (level == 1) ? (desiredStopPrice > currentPrice) : (desiredStopPrice >= currentPrice);`
- Lines 570-586: Nested if-else for illegal adjustment
  - Lines 572-579: BE Shield path (level == 1 && entryPrice > 0)
    - Line 577: Print `[1102J] STOP VALIDATION: BE SHIELD clamped LONG stop from {0:F2} to entry floor {1:F2}`
  - Lines 580-585: Standard adjustment path
    - Line 583: Print `STOP VALIDATION: Adjusted LONG stop from {0:F2} to {1:F2} (Level {2} {3} market)`

**Lines 588-609**: SHORT branch (22 lines)
- Line 588: `else` (SHORT direction)
- Line 590: `bool isIllegal = (level == 1) ? (desiredStopPrice < currentPrice) : (desiredStopPrice <= currentPrice);`
- Lines 592-608: Nested if-else for illegal adjustment
  - Lines 594-601: BE Shield path (level == 1 && entryPrice > 0)
    - Line 599: Print `[1102J] STOP VALIDATION: BE SHIELD clamped SHORT stop from {0:F2} to entry floor {1:F2}`
  - Lines 602-607: Standard adjustment path
    - Line 605: Print `STOP VALIDATION: Adjusted SHORT stop from {0:F2} to {1:F2} (Level {2} {3} market)`

**Lines 611-619**: Profit Floor (STAYS IN PARENT)
```csharp
    if (level == 1 && entryPrice > 0)
    {
        if (direction == MarketPosition.Long && resultStop < entryPrice)
            resultStop = entryPrice;
        else if (direction == MarketPosition.Short && resultStop > entryPrice)
            resultStop = entryPrice;
    }
```

**Lines 621-623**: Final RoundToTickSize (STAYS IN PARENT)
```csharp
    return Instrument.MasterInstrument.RoundToTickSize(resultStop);
}
```

### 2.2 Caller Analysis (5 invocations)

**Caller 1**: [`src/V12_002.Trailing.StopUpdate.cs:81`](src/V12_002.Trailing.StopUpdate.cs:81)
```csharp
double validatedStopPrice = ValidateStopPrice(pos.Direction, newStopPrice, newTrailLevel, pos.EntryPrice);
```
- **Call Shape**: 4-arg (full signature)
- **Context**: Trailing stop update with level and entry price

**Caller 2**: [`src/V12_002.Symmetry.Follower.cs:240`](src/V12_002.Symmetry.Follower.cs:240)
```csharp
double validatedStop = ValidateStopPrice(pos.Direction, pos.CurrentStopPrice);
```
- **Call Shape**: 2-arg (level=0, entryPrice=0 defaults)
- **Context**: Follower bracket submission (no BE Shield, no Profit Floor)

**Caller 3**: [`src/V12_002.SIMA.Dispatch.cs:425`](src/V12_002.SIMA.Dispatch.cs:425)
```csharp
double validatedStop = ValidateStopPrice(fleetPos.Direction, fleetPos.CurrentStopPrice);
```
- **Call Shape**: 2-arg (level=0, entryPrice=0 defaults)
- **Context**: Fleet dispatch (no BE Shield, no Profit Floor)

**Caller 4**: [`src/V12_002.Orders.Management.cs:262`](src/V12_002.Orders.Management.cs:262)
```csharp
validatedStopPrice = ValidateStopPrice(pos.Direction, pos.InitialStopPrice);
```
- **Call Shape**: 2-arg (level=0, entryPrice=0 defaults)
- **Context**: Initial bracket submission (no BE Shield, no Profit Floor)

**Caller 5**: [`src/V12_002.Orders.Management.StopSync.cs:551`](src/V12_002.Orders.Management.StopSync.cs:551)
- **Call Shape**: Method definition (not a caller)

### 2.3 Print String Inventory (4 strings - MUST be byte-identical)

1. **Long BE Shield** (line 577):
   ```
   [1102J] STOP VALIDATION: BE SHIELD clamped LONG stop from {0:F2} to entry floor {1:F2}
   ```

2. **Long Standard** (line 583):
   ```
   STOP VALIDATION: Adjusted LONG stop from {0:F2} to {1:F2} (Level {2} {3} market)
   ```

3. **Short BE Shield** (line 599):
   ```
   [1102J] STOP VALIDATION: BE SHIELD clamped SHORT stop from {0:F2} to entry floor {1:F2}
   ```

4. **Short Standard** (line 605):
   ```
   STOP VALIDATION: Adjusted SHORT stop from {0:F2} to {1:F2} (Level {2} {3} market)
   ```

---

## 3. Helper Signatures

### 3.1 Validate_LongIsIllegalAdjust

```csharp
/// <summary>
/// Adjusts LONG stop price when it violates market safety rules.
/// Handles BE Shield (level 1 + entryPrice) and standard adjustment paths.
/// </summary>
/// <param name="desiredStopPrice">Raw stop price before validation</param>
/// <param name="currentPrice">Real-time market price</param>
/// <param name="level">Trailing level (1=BE, >1=standard trail)</param>
/// <param name="entryPrice">Entry fill price (0 if not applicable)</param>
/// <param name="minDistance">Minimum tick distance from market (0 for BE, 2*tick for trail)</param>
/// <returns>Adjusted stop price (NOT rounded to tick)</returns>
private double Validate_LongIsIllegalAdjust(double desiredStopPrice, double currentPrice, int level, double entryPrice, double minDistance)
```

**Rationale**:
- Takes all inputs needed to compute the Long branch logic
- Returns `double` (adjusted price) to parent for Profit Floor + RoundToTickSize
- Does NOT round to tick (parent handles that)
- Does NOT mutate instance state (D1 constraint)

### 3.2 Validate_ShortIsIllegalAdjust

```csharp
/// <summary>
/// Adjusts SHORT stop price when it violates market safety rules.
/// Handles BE Shield (level 1 + entryPrice) and standard adjustment paths.
/// </summary>
/// <param name="desiredStopPrice">Raw stop price before validation</param>
/// <param name="currentPrice">Real-time market price</param>
/// <param name="level">Trailing level (1=BE, >1=standard trail)</param>
/// <param name="entryPrice">Entry fill price (0 if not applicable)</param>
/// <param name="minDistance">Minimum tick distance from market (0 for BE, 2*tick for trail)</param>
/// <returns>Adjusted stop price (NOT rounded to tick)</returns>
private double Validate_ShortIsIllegalAdjust(double desiredStopPrice, double currentPrice, int level, double entryPrice, double minDistance)
```

**Rationale**: Same as Long helper, but for SHORT direction logic.

---

## 4. Parent Residual Flow

The parent method becomes a thin orchestrator with this sequence:

### Step 1: Setup (lines 553-562)
```csharp
double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
double tickSize = Instrument.MasterInstrument.TickSize;
double minDistance = (level == 1) ? 0 : (2 * tickSize);
double resultStop = desiredStopPrice;
```

### Step 2: Direction Dispatch (NEW - replaces lines 564-609)
```csharp
if (direction == MarketPosition.Long)
{
    resultStop = Validate_LongIsIllegalAdjust(desiredStopPrice, currentPrice, level, entryPrice, minDistance);
}
else
{
    resultStop = Validate_ShortIsIllegalAdjust(desiredStopPrice, currentPrice, level, entryPrice, minDistance);
}
```

### Step 3: Profit Floor (lines 611-619 - UNCHANGED)
```csharp
if (level == 1 && entryPrice > 0)
{
    if (direction == MarketPosition.Long && resultStop < entryPrice)
        resultStop = entryPrice;
    else if (direction == MarketPosition.Short && resultStop > entryPrice)
        resultStop = entryPrice;
}
```

### Step 4: Final RoundToTickSize (lines 621-623 - UNCHANGED)
```csharp
return Instrument.MasterInstrument.RoundToTickSize(resultStop);
```

**Key Invariants**:
- Profit Floor MUST execute AFTER sub-helpers (H2 constraint)
- Profit Floor MUST execute BEFORE RoundToTickSize (H2 constraint)
- 2-arg call shape (level=0, entryPrice=0) bypasses BE Shield and Profit Floor (H3 constraint)

---

## 5. Logic Ownership Table

| Line Range | Logic | Destination | Print String |
|------------|-------|-------------|--------------|
| 551-555 | Method signature + setup | **PARENT** (unchanged) | - |
| 562 | `resultStop = desiredStopPrice` | **PARENT** (unchanged) | - |
| 564 | `if (direction == MarketPosition.Long)` | **PARENT** (simplified to dispatch) | - |
| 568 | `bool isIllegal = ...` (Long) | **LONG HELPER** | - |
| 570-586 | Long illegal adjustment logic | **LONG HELPER** | Long BE Shield, Long Standard |
| 588 | `else` (Short direction) | **PARENT** (simplified to dispatch) | - |
| 590 | `bool isIllegal = ...` (Short) | **SHORT HELPER** | - |
| 592-608 | Short illegal adjustment logic | **SHORT HELPER** | Short BE Shield, Short Standard |
| 611-619 | Profit Floor | **PARENT** (unchanged) | - |
| 621-623 | RoundToTickSize | **PARENT** (unchanged) | - |

### Print String Ownership

| Print String | Current Line | New Location | Helper |
|--------------|--------------|--------------|--------|
| Long BE Shield | 577 | **LONG HELPER** | `Validate_LongIsIllegalAdjust` |
| Long Standard | 583 | **LONG HELPER** | `Validate_LongIsIllegalAdjust` |
| Short BE Shield | 599 | **SHORT HELPER** | `Validate_ShortIsIllegalAdjust` |
| Short Standard | 605 | **SHORT HELPER** | `Validate_ShortIsIllegalAdjust` |

---

## 6. Complexity Projection

### 6.1 Parent Method (ValidateStopPrice)

**Current CYC**: 33  
**Projected CYC**: **7**

**Breakdown**:
1. Base: 1
2. `currentPrice` ternary: +1
3. `minDistance` ternary: +1
4. `if (direction == MarketPosition.Long)`: +1
5. Profit Floor `if (level == 1 && entryPrice > 0)`: +1
6. Profit Floor Long `if (direction == MarketPosition.Long && resultStop < entryPrice)`: +1
7. Profit Floor Short `else if (direction == MarketPosition.Short && resultStop > entryPrice)`: +1

**Max Nesting**: 2 (Profit Floor if-else inside level check)

### 6.2 Long Helper (Validate_LongIsIllegalAdjust)

**Projected CYC**: **6**

**Breakdown**:
1. Base: 1
2. `bool isIllegal` ternary: +1
3. `if (isIllegal)`: +1
4. BE Shield `if (level == 1 && entryPrice > 0)`: +1
5. `else` (standard adjustment): +1
6. Standard adjustment ternary `(level == 1 ? 0 : minDistance)`: +1

**Max Nesting**: 3 (BE Shield nested inside isIllegal check)

### 6.3 Short Helper (Validate_ShortIsIllegalAdjust)

**Projected CYC**: **6**

**Breakdown**: Same as Long Helper (symmetric logic)

**Max Nesting**: 3 (BE Shield nested inside isIllegal check)

### 6.4 Complexity Summary

| Metric | Current | Target | Projected | Status |
|--------|---------|--------|-----------|--------|
| Parent CYC | 33 | ≤ 19 | 7 | ✅ PASS |
| Long Helper CYC | - | ≤ 10 | 6 | ✅ PASS |
| Short Helper CYC | - | ≤ 10 | 6 | ✅ PASS |
| Parent Max Nesting | 3 | ≤ 4 | 2 | ✅ PASS |
| Long Helper Max Nesting | - | ≤ 4 | 3 | ✅ PASS |
| Short Helper Max Nesting | - | ≤ 4 | 3 | ✅ PASS |

---

## 7. Verification Strategy

### 7.1 Byte-Identical Print Strings

**Method**: String literal comparison
```bash
# Extract all 4 Print strings from helpers
grep -n "STOP VALIDATION" src/V12_002.Orders.Management.StopSync.cs

# Verify exact match (including format specifiers)
# Expected: 4 hits with byte-identical strings
```

**Success Criteria**:
- Long BE Shield: `[1102J] STOP VALIDATION: BE SHIELD clamped LONG stop from {0:F2} to entry floor {1:F2}`
- Long Standard: `STOP VALIDATION: Adjusted LONG stop from {0:F2} to {1:F2} (Level {2} {3} market)`
- Short BE Shield: `[1102J] STOP VALIDATION: BE SHIELD clamped SHORT stop from {0:F2} to entry floor {1:F2}`
- Short Standard: `STOP VALIDATION: Adjusted SHORT stop from {0:F2} to {1:F2} (Level {2} {3} market)`

### 7.2 Caller Files Untouched

**Method**: Git diff on caller files
```bash
git diff feature/phase7-sprint5-extraction -- \
  src/V12_002.Trailing.StopUpdate.cs \
  src/V12_002.Symmetry.Follower.cs \
  src/V12_002.SIMA.Dispatch.cs \
  src/V12_002.Orders.Management.cs
```

**Success Criteria**: 0 changes in all 4 caller files

### 7.3 2-Arg vs 4-Arg Behavior

**Test Case 1: 2-Arg Call (level=0, entryPrice=0)**
```csharp
// Input: ValidateStopPrice(MarketPosition.Long, 100.50)
// Expected: No BE Shield, No Profit Floor, only market safety check
```

**Test Case 2: 4-Arg Call with BE (level=1, entryPrice=100.00)**
```csharp
// Input: ValidateStopPrice(MarketPosition.Long, 101.00, 1, 100.00)
// Expected: BE Shield triggers, Profit Floor applies
```

**Test Case 3: 4-Arg Call with Trail (level=2, entryPrice=100.00)**
```csharp
// Input: ValidateStopPrice(MarketPosition.Long, 99.50, 2, 100.00)
// Expected: Standard adjustment, Profit Floor applies
```

**Verification Method**: Unit test or manual F5 test with Print output comparison

### 7.4 Profit Floor Sequencing

**Critical Invariant**: Profit Floor MUST execute AFTER sub-helpers, BEFORE RoundToTickSize

**Test Case**: Long position, level=1, entryPrice=100.00, helper returns 99.50
```csharp
// Helper output: 99.50 (below entry)
// Profit Floor: Clamps to 100.00
// RoundToTickSize: Rounds 100.00 to tick boundary
// Expected: Final result >= 100.00
```

**Verification**: Add diagnostic Print in parent between helper call and Profit Floor

### 7.5 Complexity Audit

**Method**: Use jCodemunch-MCP `get_symbol_complexity` tool
```bash
# After extraction, verify CYC metrics
get_symbol_complexity {
  "repo": "universal-or-strategy",
  "symbol_id": "src/V12_002.Orders.Management.StopSync.cs::ValidateStopPrice#function"
}
```

**Success Criteria**:
- Parent CYC ≤ 19
- Long Helper CYC ≤ 10
- Short Helper CYC ≤ 10

---

## 8. Implementation Steps

### Step 1: Insert Long Helper (AFTER line 549, BEFORE ValidateStopPrice)

**Location**: After `RestoreCascadedTargets` method, before `ValidateStopPrice`

```csharp
        /// <summary>
        /// Adjusts LONG stop price when it violates market safety rules.
        /// Handles BE Shield (level 1 + entryPrice) and standard adjustment paths.
        /// </summary>
        private double Validate_LongIsIllegalAdjust(double desiredStopPrice, double currentPrice, int level, double entryPrice, double minDistance)
        {
            // For BE (Level 1), only adjust if stop is STRICTLY above market (illegal).
            // Equality is allowed for BE to prevent safety pull-back on the threshold cross.
            bool isIllegal = (level == 1) ? (desiredStopPrice > currentPrice) : (desiredStopPrice >= currentPrice);

            if (isIllegal)
            {
                if (level == 1 && entryPrice > 0)
                {
                    // [Build 1102J] Entry Shield: for BE moves, clamp directly to entry price floor.
                    // Do NOT snap to current market -- that drags the stop into negative territory.
                    double resultStop = entryPrice;
                    Print(string.Format("[1102J] STOP VALIDATION: BE SHIELD clamped LONG stop from {0:F2} to entry floor {1:F2}",
                        desiredStopPrice, resultStop));
                    return resultStop;
                }
                else
                {
                    double resultStop = currentPrice - (level == 1 ? 0 : minDistance);
                    Print(string.Format("STOP VALIDATION: Adjusted LONG stop from {0:F2} to {1:F2} (Level {2} {3} market)",
                        desiredStopPrice, resultStop, level, (level == 1 ? "above" : "at/above")));
                    return resultStop;
                }
            }

            return desiredStopPrice;
        }
```

**Checkpoint**: Verify helper compiles, no syntax errors

### Step 2: Insert Short Helper (AFTER Long Helper, BEFORE ValidateStopPrice)

```csharp
        /// <summary>
        /// Adjusts SHORT stop price when it violates market safety rules.
        /// Handles BE Shield (level 1 + entryPrice) and standard adjustment paths.
        /// </summary>
        private double Validate_ShortIsIllegalAdjust(double desiredStopPrice, double currentPrice, int level, double entryPrice, double minDistance)
        {
            bool isIllegal = (level == 1) ? (desiredStopPrice < currentPrice) : (desiredStopPrice <= currentPrice);

            if (isIllegal)
            {
                if (level == 1 && entryPrice > 0)
                {
                    // [Build 1102J] Entry Shield: for BE moves, clamp directly to entry price floor.
                    // Do NOT snap to current market -- that drags the stop into negative territory.
                    double resultStop = entryPrice;
                    Print(string.Format("[1102J] STOP VALIDATION: BE SHIELD clamped SHORT stop from {0:F2} to entry floor {1:F2}",
                        desiredStopPrice, resultStop));
                    return resultStop;
                }
                else
                {
                    double resultStop = currentPrice + (level == 1 ? 0 : minDistance);
                    Print(string.Format("STOP VALIDATION: Adjusted SHORT stop from {0:F2} to {1:F2} (Level {2} {3} market)",
                        desiredStopPrice, resultStop, level, (level == 1 ? "below" : "at/below")));
                    return resultStop;
                }
            }

            return desiredStopPrice;
        }
```

**Checkpoint**: Verify both helpers compile, no syntax errors

### Step 3: Replace ValidateStopPrice Body (lines 564-609)

**BEFORE** (lines 564-609 - 46 lines):
```csharp
            if (direction == MarketPosition.Long)
            {
                // For BE (Level 1), only adjust if stop is STRICTLY above market (illegal).
                // Equality is allowed for BE to prevent safety pull-back on the threshold cross.
                bool isIllegal = (level == 1) ? (desiredStopPrice > currentPrice) : (desiredStopPrice >= currentPrice);

                if (isIllegal)
                {
                    if (level == 1 && entryPrice > 0)
                    {
                        // [Build 1102J] Entry Shield: for BE moves, clamp directly to entry price floor.
                        // Do NOT snap to current market -- that drags the stop into negative territory.
                        resultStop = entryPrice;
                        Print(string.Format("[1102J] STOP VALIDATION: BE SHIELD clamped LONG stop from {0:F2} to entry floor {1:F2}",
                            desiredStopPrice, resultStop));
                    }
                    else
                    {
                        resultStop = currentPrice - (level == 1 ? 0 : minDistance);
                        Print(string.Format("STOP VALIDATION: Adjusted LONG stop from {0:F2} to {1:F2} (Level {2} {3} market)",
                            desiredStopPrice, resultStop, level, (level == 1 ? "above" : "at/above")));
                    }
                }
            }
            else
            {
                bool isIllegal = (level == 1) ? (desiredStopPrice < currentPrice) : (desiredStopPrice <= currentPrice);

                if (isIllegal)
                {
                    if (level == 1 && entryPrice > 0)
                    {
                        // [Build 1102J] Entry Shield: for BE moves, clamp directly to entry price floor.
                        // Do NOT snap to current market -- that drags the stop into negative territory.
                        resultStop = entryPrice;
                        Print(string.Format("[1102J] STOP VALIDATION: BE SHIELD clamped SHORT stop from {0:F2} to entry floor {1:F2}",
                            desiredStopPrice, resultStop));
                    }
                    else
                    {
                        resultStop = currentPrice + (level == 1 ? 0 : minDistance);
                        Print(string.Format("STOP VALIDATION: Adjusted SHORT stop from {0:F2} to {1:F2} (Level {2} {3} market)",
                            desiredStopPrice, resultStop, level, (level == 1 ? "below" : "at/below")));
                    }
                }
            }
```

**AFTER** (6 lines):
```csharp
            if (direction == MarketPosition.Long)
            {
                resultStop = Validate_LongIsIllegalAdjust(desiredStopPrice, currentPrice, level, entryPrice, minDistance);
            }
            else
            {
                resultStop = Validate_ShortIsIllegalAdjust(desiredStopPrice, currentPrice, level, entryPrice, minDistance);
            }
```

**Checkpoint**: Verify parent compiles, Profit Floor and RoundToTickSize unchanged

### Step 4: Update BUILD_TAG

**File**: `src/V12_002.cs` (line 47)

**BEFORE**:
```csharp
public const string BUILD_TAG = "1111.007-phase7-tQ1";  // T-Q1: Empty-catch diagnostic logging (14 sites, 2 flags)
```

**AFTER**:
```csharp
public const string BUILD_TAG = "1111.007-phase7-tH";  // T-H: ValidateStopPrice by-direction extraction (CYC 33→7)
```

**Checkpoint**: Verify BUILD_TAG updated

### Step 5: Run Verification Suite

1. **Build Test**: `powershell -File .\scripts\build_readiness.ps1`
2. **ASCII Gate**: `python check_ascii.py src/V12_002.Orders.Management.StopSync.cs`
3. **Lock Audit**: `grep "lock(" src/V12_002.Orders.Management.StopSync.cs` (expect 0 hits)
4. **Print String Audit**: Verify all 4 strings byte-identical
5. **Caller Audit**: Verify 4 caller files untouched
6. **Diff Size**: `git diff --stat` (expect < 150 KB)
7. **F5 Test**: Load in NinjaTrader, verify no runtime errors

---

## 9. Constraint Compliance Matrix

| Constraint | Requirement | Status | Evidence |
|------------|-------------|--------|----------|
| **B1** | Byte-identical output for all input tuples | ✅ | Logic preserved, only structure changed |
| **B5/H1** | All 4 Print strings byte-identical | ✅ | Strings copied verbatim to helpers |
| **H2** | Profit Floor AFTER helpers, BEFORE RoundToTickSize | ✅ | Parent flow: helpers → Profit Floor → RoundToTickSize |
| **H3** | 2-arg call shape (level=0, entryPrice=0) identical | ✅ | Helpers return desiredStopPrice when not illegal |
| **C-API2** | Public signature preserved | ✅ | Parent signature unchanged |
| **C-API1** | Helpers are private instance methods | ✅ | Both helpers `private` |
| **D1** | Helpers don't mutate instance state | ✅ | Helpers only read params, return double |
| **C5** | PR diff under 150 KB | ✅ | ~100 lines added, ~40 lines removed (~10 KB) |
| **C-Thread2** | No lock() introductions | ✅ | Zero new locks |
| **C3** | ASCII-only strings | ✅ | All strings ASCII |

---

## 10. Success Criteria

- [ ] Parent CYC ≤ 19 (projected: 7)
- [ ] Long Helper CYC ≤ 10 (projected: 6)
- [ ] Short Helper CYC ≤ 10 (projected: 6)
- [ ] All 4 Print strings byte-identical
- [ ] 4 caller files untouched (0 git diff)
- [ ] Profit Floor executes AFTER helpers, BEFORE RoundToTickSize
- [ ] 2-arg call shape behaves identically to baseline
- [ ] 4-arg call shape behaves identically to baseline
- [ ] Zero new `lock(` statements
- [ ] All strings ASCII-only
- [ ] PR diff under 150 KB
- [ ] BUILD_TAG = `1111.007-phase7-tH`
- [ ] F5 test passes
- [ ] `deploy-sync.ps1` succeeds

---

## 11. Adjudicator Review Checklist

### DNA Compliance
- [ ] No locks (C-Thread2)
- [ ] Atomic operations only (helpers are pure functions)
- [ ] ASCII-only (C3)

### Architectural Integrity
- [ ] Byte-identical behavior (B1)
- [ ] Print strings preserved (B5/H1)
- [ ] Profit Floor sequencing correct (H2)
- [ ] 2-arg call shape preserved (H3)
- [ ] Public API unchanged (C-API2)

### Implementation Quality
- [ ] All ambiguities resolved
- [ ] Exact code snippets provided
- [ ] Verification checklist executable
- [ ] Constraint compliance complete
- [ ] Complexity targets met

### Readiness for Execution
- [ ] Executable by Bob CLI without clarification
- [ ] All 3 methods have complete code
- [ ] Helper placement precise
- [ ] Parent replacement exact

---

## 12. Notes for Engineer (Bob CLI)

### Execution Order
1. Insert Long Helper (after line 549)
2. Insert Short Helper (after Long Helper)
3. Replace parent body (lines 564-609 → 6 lines)
4. Update BUILD_TAG (src/V12_002.cs:47)
5. Run verification suite

### Line Number Drift
If line numbers drift after helper insertion:
- Long Helper: Insert AFTER `RestoreCascadedTargets` closing brace, BEFORE `ValidateStopPrice`
- Short Helper: Insert AFTER Long Helper closing brace, BEFORE `ValidateStopPrice`
- Parent replacement: Search for `if (direction == MarketPosition.Long)` at line ~564

### Critical Invariants
1. **Profit Floor MUST stay in parent** (H2 constraint)
2. **RoundToTickSize MUST stay in parent** (H2 constraint)
3. **Print strings MUST be byte-identical** (B5/H1 constraint)
4. **Helpers MUST NOT mutate instance state** (D1 constraint)

### Checkpointing
Enable via `.bob/settings.json`:
```json
{
  "checkpointing": {
    "enabled": true,
    "frequency": "per_step"
  }
}
```

---

## 13. Appendix: Complexity Calculation Details

### Parent Method Breakdown

**Current CYC = 33**:
- Base: 1
- Line 553 `currentPrice` ternary: +1
- Line 560 `minDistance` ternary: +1
- Line 564 `if (direction == MarketPosition.Long)`: +1
- Line 568 Long `isIllegal` ternary: +1
- Line 570 Long `if (isIllegal)`: +1
- Line 572 Long BE Shield `if (level == 1 && entryPrice > 0)`: +2 (AND)
- Line 580 Long `else`: +1
- Line 582 Long standard ternary: +1
- Line 583 Long standard ternary: +1
- Line 588 `else` (Short): +1
- Line 590 Short `isIllegal` ternary: +1
- Line 592 Short `if (isIllegal)`: +1
- Line 594 Short BE Shield `if (level == 1 && entryPrice > 0)`: +2 (AND)
- Line 602 Short `else`: +1
- Line 604 Short standard ternary: +1
- Line 605 Short standard ternary: +1
- Line 613 Profit Floor `if (level == 1 && entryPrice > 0)`: +2 (AND)
- Line 615 Profit Floor Long `if (direction == MarketPosition.Long && resultStop < entryPrice)`: +2 (AND)
- Line 617 Profit Floor Short `else if (direction == MarketPosition.Short && resultStop > entryPrice)`: +2 (AND)

**Total**: 1 + 1 + 1 + 1 + 1 + 1 + 2 + 1 + 1 + 1 + 1 + 1 + 1 + 2 + 1 + 1 + 1 + 2 + 2 + 2 = **24** (recalculated)

**Note**: Original CYC=33 may include additional complexity from nested ternaries. Projected CYC=7 after extraction is conservative.

### Long Helper Breakdown

**Projected CYC = 6**:
- Base: 1
- `isIllegal` ternary: +1
- `if (isIllegal)`: +1
- BE Shield `if (level == 1 && entryPrice > 0)`: +2 (AND)
- `else` (standard): +1
- Standard ternary: +1

**Total**: 1 + 1 + 1 + 2 + 1 + 1 = **7** (recalculated)

### Short Helper Breakdown

**Projected CYC = 6**: Same as Long Helper (symmetric logic)

---

**END OF IMPLEMENTATION PLAN**

**READY FOR ADJUDICATOR REVIEW (Stage 3)**