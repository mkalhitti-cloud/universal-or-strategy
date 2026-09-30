# B102 Tickets

## TICKET-B102-1: Fix DW-B100 (private DTO -> internal) + DW-B101 (Cancelled eviction)

**Block**: B102
**Lane**: LaneA
**File**: src/PropTraderTools/CopyEngine.cs
**Priority**: P1/P2 combined
**Spec Req IDs**: DW-B100, DW-B101
**Plan Reference**: docs/brain/B102/02-architecture-plan.md (REVIEW_PASS)

### Problem Statement
Two defects in CopyEngine.cs:
1. DW-B100: CopyRuleDto and CopyRulesContainer declared `private sealed class` -- XmlSerializer cannot reflect private nested types, so SaveRules() throws InvalidOperationException (swallowed silently), XML file is never written, and LoadRules() always finds no file to restore.
2. DW-B101: EvictDedup handles Cancelled for _dedupCache but does NOT clear _entryDispatchedOrders on Cancelled. TryEvictFollowerBeSlot has early-return guard `if (!isFilled && !isRejected) return` at L1394, which exits before _entryDispatchedOrders.Clear() for Cancelled orders. On Rithmic, if a broker recycles a numeric orderId after Cancelled, Gate 5 in DispatchCopy returns immediately (IsEntryDispatched=true), causing silent dispatch failure.

### Changes

**Change 1** -- L3872, exact text change:
```
BEFORE: private sealed class CopyRuleDto
AFTER:  internal sealed class CopyRuleDto
```

**Change 2** -- L3893, exact text change:
```
BEFORE: private sealed class CopyRulesContainer
AFTER:  internal sealed class CopyRulesContainer
```

**Change 3** -- EvictDedup method body (currently at L3102-L3114): After the `_dedupCache.TryRemove(orderId, out _);` line (currently L3111), insert:
```csharp
            if (state == OrderState.Cancelled)
                _entryDispatchedOrders.Clear(); // DW-B101: evict on Cancelled (Filled/Rejected handled by TryEvictFollowerBeSlot)
```
Also update the existing comment on the line after the TryRemove (currently L3112):
```
BEFORE: // DW-B91-A-v2: eviction moved to TryEvictFollowerBeSlot (position-flat).
        // Prevents partial-fill re-dispatch: Filled fires before Submitted re-submit on Rithmic.
AFTER:  // DW-B91-A-v2: eviction moved to TryEvictFollowerBeSlot (position-flat) for Filled/Rejected.
        // Prevents partial-fill re-dispatch: Filled fires before Submitted re-submit on Rithmic.
        // DW-B101: Cancelled eviction of _entryDispatchedOrders handled here (TryEvictFollowerBeSlot misses Cancelled).
```

### Method Signatures
No method signature changes. All three changes are internal to existing methods or access modifier changes.

### CYC Impact
- DW-B100 changes: CYC delta = 0 (access modifier change, no new branches)
- DW-B101 change: EvictDedup CYC 2 -> 3 (one new if-branch; still <= 8 PASS)

### JS-DNA Compliance (per RULES_CATALOG.md)
- SCAN-01 lock(): 0 new lock() -- ConcurrentDictionary.Clear() is lock-free PASS
- SCAN-02 async void: 0 new async void PASS
- SCAN-03 return null: 0 new return null PASS
- SCAN-04 throw new Exception: 0 new throw PASS
- SCAN-05 CYC: EvictDedup 2->3 (<=8) PASS
- SCAN-06 ASCII-only: no new string literals PASS
- SCAN-07 XmlSerializer on private: fixed by Changes 1+2 PASS

### Test Plan (xUnit [Fact] only -- NEVER NUnit/MSTest)

**T_B100_01** -- SaveRules writes XML file:
```
Arrange: construct CopyEngine, add 1 rule via OnApplyRule or _rules.Add
Act:     call SaveRules(tmpPath) where tmpPath is a temp file path
Assert:  File.Exists(tmpPath) == true
```

**T_B100_02** -- LoadRules restores state:
```
Arrange: construct CopyEngine, add 1 rule, call SaveRules(tmpPath) with CopyEnabled=true
Act:     construct fresh CopyEngine2, call LoadRules(tmpPath)
Assert:  CopyEngine2._isCopyEnabled == true
         CopyEngine2._rules.Count == 1
```

**T_B100_03** -- LoadRules missing file is no-op:
```
Arrange: (none)
Act:     construct CopyEngine, call LoadRules("nonexistent_B102_test.xml")
Assert:  no exception thrown
         _rules.Count == 0
```

**T_B101_01** -- EvictDedup Cancelled clears _entryDispatchedOrders:
```
Arrange: construct CopyEngine, seed _entryDispatchedOrders with orderId "TEST-001"
Act:     call EvictDedup("TEST-001", OrderState.Cancelled)
Assert:  _entryDispatchedOrders does NOT contain "TEST-001"
         (i.e. Clear() was called -- the dict is empty)
```

**T_B101_02** -- EvictDedup Filled does NOT clear via EvictDedup:
```
Arrange: construct CopyEngine, seed _entryDispatchedOrders with orderId "OTHER-002"
Act:     call EvictDedup("TEST-001", OrderState.Filled)
Assert:  _entryDispatchedOrders still contains "OTHER-002"
         (Filled eviction is position-flat path in TryEvictFollowerBeSlot, not EvictDedup)
```

### Forbidden
- Do NOT touch TradeCopierPanel.cs
- Do NOT remove catch(Exception) swallows
- Do NOT change any method signatures
- Do NOT add lock() anywhere
- Do NOT add any features beyond the 3 described changes
- Do NOT modify _persistenceLoaded guard

### Completion Criteria
- All 3 changes applied to CopyEngine.cs (verified by line + before/after text)
- Build: 0 errors, 0 warnings
- Sync: ptt-sync-and-verify.ps1 reports 0 MISMATCH
- Ticket-1-completion.md written with all 3 changes documented
