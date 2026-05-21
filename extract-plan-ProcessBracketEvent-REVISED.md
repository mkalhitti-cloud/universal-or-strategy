# EXTRACTION PLAN: ProcessBracketEvent (REVISED)
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

## STEP 2 -- RESPONSIBILITY DECOMPOSITION PLAN (REVISED)

### Current State
- **Complexity:** 47 CYC (CRITICAL - highest in Sprint 4)
- **LOC:** 58 lines
- **Responsibilities Identified:** 5 distinct blocks (1 lookup + 4 state handlers)

### Revised Method Structure Analysis

#### **Block 1: FSM Lookup (Lines 155-205) - 51 lines**
**Responsibility:** Resolve AccountEvent → FollowerBracketFSM via 3-tier lookup strategy
- Primary: O(1) OrderId map lookup
- Secondary: SignalName parsing and matching
- Tertiary: O(N) fallback scan across all FSMs
- Back-fill OrderId map when found via fallback

**Estimated CYC:** ~18-20

#### **Block 2: Accepted/Working Handler (Lines 215-219) - 5 lines**
**Responsibility:** Handle Accepted/Working state transitions
```csharp
case OrderState.Accepted:
case OrderState.Working:
    if (fsm.State == FollowerBracketState.Submitted || fsm.State == FollowerBracketState.PendingSubmit)
        fsm.State = FollowerBracketState.Accepted;
    break;
```
**Estimated CYC:** ~3-4  
**Decision:** TOO SMALL (< 15 LOC) - will be inlined into dispatcher

#### **Block 3: Filled/PartFilled Handler (Lines 221-238) - 18 lines**
**Responsibility:** Handle fill events with stop/target detection and contract tracking
```csharp
case OrderState.Filled:
case OrderState.PartFilled:
    bool isStop = !string.IsNullOrEmpty(evt.SignalName) && (evt.SignalName.StartsWith("Stop_") || evt.SignalName.StartsWith("S_"));
    bool isTarget = !string.IsNullOrEmpty(evt.SignalName) && (evt.SignalName.StartsWith("T1_") || evt.SignalName.StartsWith("T2_") || 
                     evt.SignalName.StartsWith("T3_") || evt.SignalName.StartsWith("T4_") || evt.SignalName.StartsWith("T5_"));

    if (isStop || isTarget)
    {
        fsm.RemainingContracts = Math.Max(0, fsm.RemainingContracts - Math.Max(0, evt.FilledQty));
        fsm.State = fsm.RemainingContracts <= 0 ? FollowerBracketState.Filled : FollowerBracketState.Active;
    }
    else if (fsm.State == FollowerBracketState.Accepted || fsm.State == FollowerBracketState.Submitted)
    {
        // Entry filled -> Bracket is now ACTIVE
        fsm.State = FollowerBracketState.Active;
    }
    break;
```
**Estimated CYC:** ~8-10  
**Decision:** EXTRACTABLE (18 LOC, meets 15 LOC minimum)

#### **Block 4: Cancelled Handler (Lines 240-250) - 11 lines**
**Responsibility:** Handle cancellation with Replacing-state special case
```csharp
case OrderState.Cancelled:
    if (fsm.State == FollowerBracketState.Replacing
        && string.Equals(fsm.ReplacingCancelOrderId, evt.OrderId, StringComparison.Ordinal))
    {
        Print("[FSM-C2] Replace-cycle cancel absorbed -- FSM stays Replacing");
    }
    else
    {
        fsm.State = FollowerBracketState.Cancelled;
    }
    break;
```
**Estimated CYC:** ~4-5  
**Decision:** TOO SMALL (11 LOC < 15 LOC minimum) - will be inlined into dispatcher

#### **Block 5: Rejected Handler (Lines 252-256) - 5 lines**
**Responsibility:** Handle rejection with error capture
```csharp
case OrderState.Rejected:
    fsm.State = FollowerBracketState.Rejected;
    fsm.LastBrokerError = evt.ErrorMessage;
    break;
```
**Estimated CYC:** ~2  
**Decision:** TOO SMALL (5 LOC < 15 LOC minimum) - will be inlined into dispatcher

#### **Block 6: Transition Logging (Lines 258-263) - 6 lines**
**Responsibility:** Log state transitions for Shadow Mode diagnostics
**Decision:** TOO SMALL (6 LOC < 15 LOC minimum) - will be inlined into dispatcher

---

### Revised Proposed Sub-Methods

| New Method | Responsibility | Estimated LOC | Extracted From Lines | Est. CYC |
|------------|---------------|---------------|---------------------|----------|
| **ResolveFsmFromEvent** | 3-tier FSM lookup (OrderId → SignalName → Scan) | ~50 | L155-L205 | 18-20 |
| **HandleFsmFilled** | Process fill events with stop/target detection | ~18 | L221-L238 | 8-10 |

**Note:** Only 2 methods meet the 15 LOC minimum threshold. All other state handlers are too small and will be inlined into the dispatcher.

### Residual ProcessBracketEvent After Extraction
**Estimated Complexity:** 8-10 CYC  
**Role:** Dispatcher with inlined small handlers
- Call ResolveFsmFromEvent
- Guard: if fsm == null, return
- Guard: MetadataGuardFsmEvent check
- Store oldState
- Switch on evt.NewState:
  - Accepted/Working: inline (5 LOC)
  - Filled/PartFilled: call HandleFsmFilled
  - Cancelled: inline (11 LOC)
  - Rejected: inline (5 LOC)
- Inline transition logging (6 LOC)

**Estimated Structure:**
```csharp
private void ProcessBracketEvent(AccountEvent evt)
{
    FollowerBracketFSM fsm = ResolveFsmFromEvent(evt);
    if (fsm == null) return;
    if (!MetadataGuardFsmEvent(evt, fsm)) return;
    
    FollowerBracketState oldState = fsm.State;
    
    switch (evt.NewState)
    {
        case OrderState.Accepted:
        case OrderState.Working:
            if (fsm.State == FollowerBracketState.Submitted || fsm.State == FollowerBracketState.PendingSubmit)
                fsm.State = FollowerBracketState.Accepted;
            break;

        case OrderState.Filled:
        case OrderState.PartFilled:
            HandleFsmFilled(evt, fsm);
            break;

        case OrderState.Cancelled:
            if (fsm.State == FollowerBracketState.Replacing
                && string.Equals(fsm.ReplacingCancelOrderId, evt.OrderId, StringComparison.Ordinal))
            {
                Print("[FSM-C2] Replace-cycle cancel absorbed -- FSM stays Replacing");
            }
            else
            {
                fsm.State = FollowerBracketState.Cancelled;
            }
            break;

        case OrderState.Rejected:
            fsm.State = FollowerBracketState.Rejected;
            fsm.LastBrokerError = evt.ErrorMessage;
            break;
    }

    if (fsm.State != oldState)
    {
        fsm.LastUpdateUtc = DateTime.UtcNow;
        Print(string.Format("[FSM-SHADOW] {0} Transition: {1} -> {2} | Event={3} | Order={4}",
            fsm.EntryName, oldState, fsm.State, evt.NewState, evt.SignalName));
    }
}
```

**Estimated Dispatcher LOC:** ~40 lines (down from 58)  
**Estimated Dispatcher CYC:** 8-10 (down from 47)

---

## EXTRACTION CONSTRAINTS

### FSM Integrity Rules (NON-NEGOTIABLE)
1. ✅ **ALL state transitions must be preserved exactly** - no merging, reordering, or skipping
2. ✅ **Lookup strategy order must remain: OrderId → SignalName → Scan** - performance critical
3. ✅ **Back-fill logic must execute after fallback lookup** - maintains O(1) map integrity
4. ✅ **Replacing-state special case must remain in Cancelled handler** - prevents premature termination
5. ✅ **Contract quantity arithmetic must be atomic** - no intermediate state exposure
6. ✅ **Fill type detection (isStop/isTarget) must remain in HandleFsmFilled** - encapsulates fill logic

### V12 DNA Compliance
- ✅ No new `lock()` statements
- ✅ ASCII-only strings (already compliant)
- ✅ All state mutations use existing FSM pattern
- ✅ No signature changes (private methods only)

### Extraction Mechanics
- **Total Extracted LOC:** ~68 lines (2 sub-methods)
- **Split Strategy:** MANDATORY Python extractor (`v12_split.py`) - exceeds 50-line threshold
- **Target File:** Same file (`V12_002.Symmetry.BracketFSM.cs`) - file is 306 LOC, well under 1200 limit
- **Method Visibility:** All sub-methods `private`
- **Return Types:**
  - `ResolveFsmFromEvent`: `private FollowerBracketFSM`
  - `HandleFsmFilled`: `private void`

---

## RISK ASSESSMENT

### Complexity Reduction
- **Before:** 47 CYC (CRITICAL)
- **After:** 8-10 CYC (dispatcher) + 18 CYC (lookup) + 8 CYC (fill handler)
- **All sub-methods < 20 CYC** ✅
- **Dispatcher < 15 CYC** ✅

### Blast Radius
- **Callers:** 1 (DrainAccountMailbox)
- **Signature Change:** NONE
- **External Impact:** ZERO

### FSM Correctness
- **State Transition Preservation:** 100% (pure structural split)
- **Lookup Strategy Preservation:** 100% (exact code motion)
- **Fill Logic Encapsulation:** 100% (stop/target detection isolated)
- **Performance Impact:** ZERO (no algorithmic changes)

### Why Only 2 Extractions?
The Director's feedback requested per-state handlers, but analysis reveals:
- **Accepted handler:** 5 LOC (< 15 minimum) - too trivial to extract
- **Cancelled handler:** 11 LOC (< 15 minimum) - special case logic is concise
- **Rejected handler:** 5 LOC (< 15 minimum) - trivial assignment
- **Logging block:** 6 LOC (< 15 minimum) - diagnostic only

**Only HandleFsmFilled (18 LOC) meets the 15 LOC extraction threshold.** Extracting smaller handlers would add noise without clarity benefit, violating the "Simplicity First" principle from Karpathy protocols.

---

## [EXTRACT-GATE] 

**REVISED decomposition plan complete. Awaiting Director approval.**

**Director: Type "APPROVED" to proceed with extraction.**