# EXTRACTION PLAN: ProcessBracketEvent
**File:** src/V12_002.Symmetry.BracketFSM.cs  
**Method:** ProcessBracketEvent  
**Build Tag:** 1111.007-phase7-t11  
**Sprint:** 4, Target 11 of 23

---

## STEP 1 -- FORENSIC ANALYSIS COMPLETE

### 1a. Target Method Analysis
- **Location:** Lines 151-264 in [`V12_002.Symmetry.BracketFSM.cs`](src/V12_002.Symmetry.BracketFSM.cs)
- **Current Complexity:** 47 CYC (CRITICAL)
- **Current LOC:** 58 lines
- **Status:** FSM CRITICAL - M5 Dispatch Candidate

### 1b. jCodemunch Structural Scan
- **Graph Status:** Updated (1179 nodes, 2711 edges, 109 communities)
- **Community:** Part of Community 10 (BracketFSM cluster, cohesion 0.08)
- **God Node Risk:** V12_002 class is a god node (49 edges), but ProcessBracketEvent itself is not cross-community

### 1c. Blast Radius
- **Direct Caller:** [`DrainAccountMailbox()`](src/V12_002.Symmetry.BracketFSM.cs) (line 97)
- **Indirect Callers:** OnBarUpdate, OnOrderUpdate via TriggerCustomEvent
- **External Dependencies:** NONE - internal FSM dispatcher only
- **Signature Change Risk:** LOW - private method, single caller

---

## STEP 2 -- RESPONSIBILITY DECOMPOSITION PLAN

### Current State
- **Complexity:** 47 CYC (CRITICAL - highest in Sprint 4)
- **LOC:** 58 lines
- **Responsibilities Identified:** 3 distinct FSM phases

### Method Structure Analysis

The method has THREE clear responsibility blocks:

#### **Block 1: FSM Lookup (Lines 155-205) - 51 lines**
**Responsibility:** Resolve AccountEvent → FollowerBracketFSM via 3-tier lookup strategy
- Primary: O(1) OrderId map lookup
- Secondary: SignalName parsing and matching
- Tertiary: O(N) fallback scan across all FSMs
- Back-fill OrderId map when found via fallback

**Complexity Drivers:**
- 3 nested lookup strategies (if-else chain)
- String parsing for SignalName extraction
- Nested loops for O(N) scan (foreach + for loop)
- Conditional back-fill logic

**Estimated CYC:** ~18-20

#### **Block 2: State Transition Logic (Lines 210-256) - 47 lines**
**Responsibility:** Execute FSM state transitions based on OrderState events
- Handle Accepted/Working → Accepted transition
- Handle Filled/PartFilled → Active/Filled transitions (with contract tracking)
- Handle Cancelled → Cancelled (with Replacing-state special case)
- Handle Rejected → Rejected (with error capture)

**Complexity Drivers:**
- Switch statement on evt.NewState (4 cases)
- Nested conditionals for fill type detection (isStop, isTarget)
- String prefix matching for signal name parsing
- Special-case logic for Replacing state
- Contract quantity arithmetic

**Estimated CYC:** ~22-25

#### **Block 3: Transition Logging (Lines 258-263) - 6 lines**
**Responsibility:** Log state transitions for Shadow Mode diagnostics
- Compare old vs new state
- Update LastUpdateUtc timestamp
- Print formatted transition message

**Estimated CYC:** ~2-3

---

### Proposed Sub-Methods

| New Method | Responsibility | Estimated LOC | Extracted From Lines | Est. CYC |
|------------|---------------|---------------|---------------------|----------|
| **ResolveFsmFromEvent** | 3-tier FSM lookup (OrderId → SignalName → Scan) | ~50 | L155-L205 | 18-20 |
| **TransitionFsmState** | Execute state transitions based on OrderState | ~45 | L210-L256 | 22-25 |
| **LogFsmTransition** | Log state change for diagnostics | ~6 | L258-L263 | 2-3 |

### Residual ProcessBracketEvent After Extraction
**Estimated Complexity:** 5-7 CYC  
**Role:** Pure dispatcher
- Call ResolveFsmFromEvent
- Guard: if fsm == null, return
- Guard: MetadataGuardFsmEvent check
- Call TransitionFsmState
- Call LogFsmTransition

**Estimated Structure:**
```csharp
private void ProcessBracketEvent(AccountEvent evt)
{
    FollowerBracketFSM fsm = ResolveFsmFromEvent(evt);
    if (fsm == null) return;
    if (!MetadataGuardFsmEvent(evt, fsm)) return;
    
    FollowerBracketState oldState = fsm.State;
    TransitionFsmState(evt, fsm);
    LogFsmTransition(fsm, oldState, evt);
}
```

---

## EXTRACTION CONSTRAINTS

### FSM Integrity Rules (NON-NEGOTIABLE)
1. ✅ **ALL state transitions must be preserved exactly** - no merging, reordering, or skipping
2. ✅ **Lookup strategy order must remain: OrderId → SignalName → Scan** - performance critical
3. ✅ **Back-fill logic must execute after fallback lookup** - maintains O(1) map integrity
4. ✅ **Replacing-state special case must remain in Cancelled handler** - prevents premature termination
5. ✅ **Contract quantity arithmetic must be atomic** - no intermediate state exposure

### V12 DNA Compliance
- ✅ No new `lock()` statements
- ✅ ASCII-only strings (already compliant)
- ✅ All state mutations use existing FSM pattern
- ✅ No signature changes (private methods only)

### Extraction Mechanics
- **Total Extracted LOC:** ~101 lines (3 sub-methods)
- **Split Strategy:** MANDATORY Python extractor (`v12_split.py`) - exceeds 50-line threshold
- **Target File:** Same file (`V12_002.Symmetry.BracketFSM.cs`) - file is 306 LOC, well under 1200 limit
- **Method Visibility:** All sub-methods `private`
- **Return Types:**
  - `ResolveFsmFromEvent`: `private FollowerBracketFSM`
  - `TransitionFsmState`: `private void`
  - `LogFsmTransition`: `private void`

---

## RISK ASSESSMENT

### Complexity Reduction
- **Before:** 47 CYC (CRITICAL)
- **After:** 5-7 CYC (dispatcher) + 18 CYC (lookup) + 22 CYC (transition) + 2 CYC (logging)
- **All sub-methods < 25 CYC** ✅
- **Dispatcher < 10 CYC** ✅

### Blast Radius
- **Callers:** 1 (DrainAccountMailbox)
- **Signature Change:** NONE
- **External Impact:** ZERO

### FSM Correctness
- **State Transition Preservation:** 100% (pure structural split)
- **Lookup Strategy Preservation:** 100% (exact code motion)
- **Performance Impact:** ZERO (no algorithmic changes)

---

## [EXTRACT-GATE] 

**Decomposition plan complete. Awaiting Director approval.**

**Director: Type "APPROVED" to proceed with extraction.**