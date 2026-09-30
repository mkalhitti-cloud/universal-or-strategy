# Wave 7 Special Cases Analysis

**Date**: 2026-06-19  
**Purpose**: Identify methods requiring local execution or special handling

## Phase Count Correction

**CORRECTED**: Wave 7 has **10 phases**, not 9:
- Phase 0: Hotspot Analysis
- Phase 1: Scope Definition
- Phase 1.5: Scope Boundary Validation
- Phase 2: Architecture Planning
- Phase 3: DNA & PR Audit
- Phase 4: Ticket Generation
- Phase 4.5: Ticket Review (Jane Street validation gate)
- Phase 5: Ticket Execution
- Phase 5.V: Verification
- Phase 6: Final Review

**Lamport Event Count**: 170 epics × 10 phases × 10 events/phase = **17,000 events** (not 15,300)

## Special Case Analysis

### 1. .DLL Dependency Detection

**Files with potential .dll dependencies**:
- ❌ **NONE DETECTED** in the 170 methods

**Rationale**: All 170 methods are in `src/` C# files:
- SignalBroadcaster.cs
- V12_002.*.cs files

These are **pure C# source files** with no .dll dependencies that would block VM execution.

### 2. UTF-8 Encoding Requirements

**ALL 170 methods require UTF-8 encoding** (V12 DNA mandate):
- ✅ All files in `src/` MUST be UTF-8 (no BOM)
- ✅ Pre-push validation checks encoding
- ✅ Bob CLI enforces UTF-8 on all writes

**No special cases** - UTF-8 is universal requirement, not a special case.

### 3. xUnit Test Framework Requirements

**ALL 170 methods require xUnit tests** (V12 DNA mandate):
- ✅ ALWAYS generate xUnit tests ([Fact], Assert.Equal())
- ✅ NEVER use NUnit or MSTest
- ✅ Pre-push validation checks test framework

**No special cases** - xUnit is universal requirement, not a special case.

### 4. VM vs Local Execution

**Execution Model**: ALL 170 epics execute on VM (default)

**Local Fallback Triggers** (discovered during execution):
- .dll file references in method body
- Native interop (P/Invoke)
- File system dependencies outside src/

**Current Assessment**: No pre-identified local-only methods.

**Discovery Protocol**:
1. VM attempts execution
2. If .dll dependency detected → mark for local execution
3. Update roadmap with `execution_model: "local"`
4. Re-run locally

### 5. UI.Panel.* Methods (Potential Complexity)

**UI.Panel.* files**: 15 methods across 4 files
- V12_002.UI.Panel.Construction.cs: 3 methods
- V12_002.UI.Panel.Handlers.cs: 4 methods
- V12_002.UI.Panel.Helpers.cs: 3 methods
- V12_002.UI.Panel.StateSync.cs: 4 methods
- V12_002.UI.Panel.Lifecycle.cs: 0 methods (all CYC ≤ 8)

**Special Considerations**:
- WPF UI code (visual tree manipulation)
- May have complex dependencies
- Good candidates for pilot test

**Recommendation**: Include 1 UI.Panel method in pilot test to validate approach.

### 6. SIMA.Lifecycle.cs Methods (High Complexity)

**SIMA.Lifecycle.cs**: 11 methods (most in any single file)
- `HydrateFromOpenPositions` (CYC=31) - Priority 0
- `SweepBrokerOrders` (CYC=24) - Priority 0
- `HydrateWorkingOrdersFromBroker` (CYC=19) - Priority 0
- 8 more methods (CYC 10-17)

**Special Considerations**:
- Critical SIMA initialization code
- Complex state hydration logic
- High risk if broken

**Recommendation**: Include 1 SIMA.Lifecycle method in pilot test.

## Pilot Test Selection

**Recommended 3 Epics**:

1. **Low Complexity** (CYC 9-10):
   - `GetSubscriberCounts` (CYC=9, LOC=10) - SignalBroadcaster.cs
   - Simple, isolated, low risk

2. **Medium Complexity** (CYC 11-15):
   - `UpdatePanelState` (CYC=16, LOC=51) - V12_002.UI.Panel.StateSync.cs
   - UI code, tests WPF handling

3. **High Complexity** (CYC 21+):
   - `HydrateFromOpenPositions` (CYC=31, LOC=98) - V12_002.SIMA.Lifecycle.cs
   - Most complex method, critical path

## Summary

**Special Cases**: NONE requiring pre-identified local execution

**Universal Requirements**:
- ✅ UTF-8 encoding (all 170 methods)
- ✅ xUnit tests (all 170 methods)
- ✅ VM execution (default for all 170 methods)

**Lamport Events**: 17,000 (170 × 10 phases × 10 events)

**Pilot Test**: 3 epics (low/medium/high complexity)

---

**Next Step**: Generate roadmap with corrected phase count and pilot selection.