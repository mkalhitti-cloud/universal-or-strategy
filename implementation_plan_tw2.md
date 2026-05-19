# T-W2: TryFindOrderInPosition Complexity Reduction - Zero-Drift Extraction Plan

**Mission ID**: T-W2  
**Target**: [`V12_002.Orders.Callbacks.AccountOrders.cs:217-232`](src/V12_002.Orders.Callbacks.AccountOrders.cs:217-232)  
**Current CYC**: 25  
**Target CYC**: ≤10 (parent), ≤4 (helper1), ≤3 (helper2)  
**Protocol**: Phase 7 Recursive Protocol Stage 2  
**Status**: PLANNING

---

## 1. EXECUTIVE SUMMARY

### 1.1 Current State Analysis

**Method**: `TryFindOrderInPosition` (lines 217-232)
- **Complexity**: CYC=25 (7 dictionary checks × 3-4 branches each)
- **Single Caller**: Line 719 in `ProcessQueuedAccountOrder`
- **Asymmetric Pattern Confirmed**:
  - Entry/Stop/T1: `(tracked == order || (tracked != null && tracked.OrderId == order.OrderId))`
  - T2-T5: `(tracked != null && tracked.OrderId == order.OrderId)` (NO ref-equality short-circuit)

**Existing Helper**: `OrdersMatchByRefOrId` (lines 234-238)
- Checks BOTH parameters for null
- NOT used by `TryFindOrderInPosition`
- Different semantics - CANNOT be reused per H10/Q-V3=C

### 1.2 Extraction Strategy

**Two-Helper Decomposition**:
1. **Helper 1**: `TryFindOrder_MatchesEntryStopOrT1` - Handles Entry/Stop/T1 with ref-equality short-circuit
2. **Helper 2**: `TryFindOrder_MatchesT2ThroughT5` - Handles T2-T5 with OrderId-only equality

**Zero-Drift Guarantee**: Sequential `if` structure preserves exact short-circuit order and predicate semantics.

---

## 2. HELPER SIGNATURES (AC1/AC2)

### 2.1 Helper 1: Entry/Stop/T1 Matcher

```csharp
// Build 935 [R-01]: Helper for Entry/Stop/T1 dictionary probes with ref-equality short-circuit.
// Returns true if 'order' matches the tracked order in 'dict' at 'entryKey' via reference OR OrderId.
// Asymmetric: checks ref-equality FIRST, then OrderId if tracked != null.
private bool TryFindOrder_MatchesEntryStopOrT1(
    ConcurrentDictionary<string, Order> dict,
    string entryKey,
    Order order)
{
    Order tracked;
    return dict.TryGetValue(entryKey, out tracked)
        && (tracked == order || (tracked != null && tracked.OrderId == order.OrderId));
}
```

**Complexity**: CYC=4
- Base: 1
- `TryGetValue` success: +1
- `tracked == order`: +1
- `tracked != null`: +1
- Total: 4

**Semantics**:
- NO null check on `order` parameter (preserves H10)
- Ref-equality short-circuit: `tracked == order` evaluated FIRST
- OrderId fallback: `tracked != null && tracked.OrderId == order.OrderId`
- Exact match to original Entry/Stop/T1 predicate

### 2.2 Helper 2: T2-T5 Matcher

```csharp
// Build 935 [R-01]: Helper for T2-T5 dictionary probes with OrderId-only equality.
// Returns true if 'order' matches the tracked order in 'dict' at 'entryKey' via OrderId ONLY.
// Asymmetric: NO ref-equality short-circuit (differs from Helper 1).
private bool TryFindOrder_MatchesT2ThroughT5(
    ConcurrentDictionary<string, Order> dict,
    string entryKey,
    Order order)
{
    Order tracked;
    return dict.TryGetValue(entryKey, out tracked)
        && tracked != null
        && tracked.OrderId == order.OrderId;
}
```

**Complexity**: CYC=3
- Base: 1
- `TryGetValue` success: +1
- `tracked != null`: +1
- Total: 3

**Semantics**:
- NO null check on `order` parameter (preserves H10)
- NO ref-equality check (preserves H9 asymmetry)
- OrderId-only equality: `tracked != null && tracked.OrderId == order.OrderId`
- Exact match to original T2-T5 predicate

---

## 3. PARENT RESIDUAL STRUCTURE (AC3)

### 3.1 Refactored Method

```csharp
// Build 935 [R-01]: Returns true if 'order' belongs to 'entryKey' position.
// Encapsulates the 7-way compound OR so the outer search loop stays trivial.
private bool TryFindOrderInPosition(Order order, string entryKey, out string matchedEntry)
{
    matchedEntry = null;
    
    // Sequential 7-step probe: preserves exact short-circuit order
    if (TryFindOrder_MatchesEntryStopOrT1(entryOrders, entryKey, order) ||
        TryFindOrder_MatchesEntryStopOrT1(stopOrders, entryKey, order) ||
        TryFindOrder_MatchesEntryStopOrT1(target1Orders, entryKey, order) ||
        TryFindOrder_MatchesT2ThroughT5(target2Orders, entryKey, order) ||
        TryFindOrder_MatchesT2ThroughT5(target3Orders, entryKey, order) ||
        TryFindOrder_MatchesT2ThroughT5(target4Orders, entryKey, order) ||
        TryFindOrder_MatchesT2ThroughT5(target5Orders, entryKey, order))
    {
        matchedEntry = entryKey;
        return true;
    }
    
    return false;
}
```

**Complexity**: CYC=8
- Base: 1
- 7 `||` branches: +7
- Total: 8 (≤10 ✓)

**Structure**:
- 7 sequential `if` conditions (NOT a loop or array)
- Exact dictionary order preserved: entry → stop → t1 → t2 → t3 → t4 → t5
- Short-circuit evaluation: stops at first match
- Single `matchedEntry` assignment after match found

---

## 4. PREDICATE LOGIC MAPPING

### 4.1 Original → Helper Mapping Table

| Dictionary | Original Predicate | Helper Call | Asymmetry |
|------------|-------------------|-------------|-----------|
| `entryOrders` | `(eOrder == order \|\| (eOrder != null && eOrder.OrderId == order.OrderId))` | `TryFindOrder_MatchesEntryStopOrT1(entryOrders, entryKey, order)` | Ref + OrderId |
| `stopOrders` | `(sOrder == order \|\| (sOrder != null && sOrder.OrderId == order.OrderId))` | `TryFindOrder_MatchesEntryStopOrT1(stopOrders, entryKey, order)` | Ref + OrderId |
| `target1Orders` | `(t1Order == order \|\| (t1Order != null && t1Order.OrderId == order.OrderId))` | `TryFindOrder_MatchesEntryStopOrT1(target1Orders, entryKey, order)` | Ref + OrderId |
| `target2Orders` | `(t2Order != null && t2Order.OrderId == order.OrderId)` | `TryFindOrder_MatchesT2ThroughT5(target2Orders, entryKey, order)` | OrderId ONLY |
| `target3Orders` | `(t3Order != null && t3Order.OrderId == order.OrderId)` | `TryFindOrder_MatchesT2ThroughT5(target3Orders, entryKey, order)` | OrderId ONLY |
| `target4Orders` | `(t4Order != null && t4Order.OrderId == order.OrderId)` | `TryFindOrder_MatchesT2ThroughT5(target4Orders, entryKey, order)` | OrderId ONLY |
| `target5Orders` | `(t5Order != null && t5Order.OrderId == order.OrderId)` | `TryFindOrder_MatchesT2ThroughT5(target5Orders, entryKey, order)` | OrderId ONLY |

### 4.2 Semantic Equivalence Proof

**For Entry/Stop/T1** (Helper 1):
```
Original: (tracked == order || (tracked != null && tracked.OrderId == order.OrderId))
Helper:   dict.TryGetValue(entryKey, out tracked) && (tracked == order || (tracked != null && tracked.OrderId == order.OrderId))

Equivalence: Original assumes TryGetValue succeeded (inline `out var`).
             Helper explicitly checks TryGetValue, then applies IDENTICAL predicate.
             ∴ Semantically equivalent when TryGetValue succeeds.
```

**For T2-T5** (Helper 2):
```
Original: (tracked != null && tracked.OrderId == order.OrderId)
Helper:   dict.TryGetValue(entryKey, out tracked) && tracked != null && tracked.OrderId == order.OrderId

Equivalence: Original assumes TryGetValue succeeded (inline `out var`).
             Helper explicitly checks TryGetValue, then applies IDENTICAL predicate.
             ∴ Semantically equivalent when TryGetValue succeeds.
```

**Short-Circuit Order Preservation**:
```
Original: if (A || B || C || D || E || F || G) { ... }
Refactor: if (H1(A) || H1(B) || H1(C) || H2(D) || H2(E) || H2(F) || H2(G)) { ... }

Where H1/H2 encapsulate the TryGetValue + predicate logic.
∴ Evaluation order IDENTICAL: stops at first true condition.
```

---

## 5. ZERO-DRIFT PROOF

### 5.1 Behavioral Equivalence Theorem

**Claim**: For every `(Order order, string entryKey)` tuple, the refactored method returns the SAME `(bool, matchedEntry)` pair as the original.

**Proof by Cases**:

**Case 1: Match in entryOrders**
- Original: `TryGetValue` succeeds, predicate `(eOrder == order || ...)` evaluates true → return `(true, entryKey)`
- Refactor: `TryFindOrder_MatchesEntryStopOrT1(entryOrders, ...)` returns true → return `(true, entryKey)`
- ∴ Equivalent ✓

**Case 2: No match in entryOrders, match in stopOrders**
- Original: First `TryGetValue` fails OR predicate false, second `TryGetValue` succeeds with true predicate → return `(true, entryKey)`
- Refactor: First helper returns false (short-circuit), second helper returns true → return `(true, entryKey)`
- ∴ Equivalent ✓

**Case 3: Match in target2Orders (asymmetric predicate)**
- Original: First 3 checks fail, `target2Orders.TryGetValue` succeeds with `(t2Order != null && t2Order.OrderId == order.OrderId)` → return `(true, entryKey)`
- Refactor: First 3 helpers return false, `TryFindOrder_MatchesT2ThroughT5(target2Orders, ...)` returns true → return `(true, entryKey)`
- ∴ Equivalent ✓

**Case 4: No match in any dictionary**
- Original: All 7 `TryGetValue` calls fail OR predicates false → return `(false, null)`
- Refactor: All 7 helper calls return false → return `(false, null)`
- ∴ Equivalent ✓

**Case 5: Multiple potential matches (short-circuit test)**
- Original: Stops at FIRST true condition in `||` chain
- Refactor: Stops at FIRST true helper call in `||` chain
- ∴ Evaluation order preserved ✓

### 5.2 Asymmetry Preservation Proof

**Entry/Stop/T1 Asymmetry**:
- Original: `(tracked == order || (tracked != null && tracked.OrderId == order.OrderId))`
- Helper 1: `(tracked == order || (tracked != null && tracked.OrderId == order.OrderId))`
- ∴ IDENTICAL logic, ref-equality short-circuit preserved ✓

**T2-T5 Asymmetry**:
- Original: `(tracked != null && tracked.OrderId == order.OrderId)` (NO ref-equality)
- Helper 2: `tracked != null && tracked.OrderId == order.OrderId` (NO ref-equality)
- ∴ IDENTICAL logic, NO ref-equality check ✓

**Critical Distinction**:
- Helper 1 checks `tracked == order` BEFORE `tracked.OrderId`
- Helper 2 checks ONLY `tracked.OrderId` (NO ref-equality)
- ∴ Asymmetry encoded in TWO separate helpers (H9 satisfied) ✓

---

## 6. COMPLEXITY ANALYSIS

### 6.1 Current Metrics

**Parent (Original)**:
- CYC=25
- 7 dictionary checks × ~3.5 branches each
- Single 232-line method

### 6.2 Post-Extraction Metrics

**Helper 1**: `TryFindOrder_MatchesEntryStopOrT1`
- CYC=4 (1 base + 1 TryGetValue + 1 ref-check + 1 null-check)
- 6 lines
- Called 3 times (entry, stop, t1)

**Helper 2**: `TryFindOrder_MatchesT2ThroughT5`
- CYC=3 (1 base + 1 TryGetValue + 1 null-check)
- 5 lines
- Called 4 times (t2, t3, t4, t5)

**Parent (Refactored)**: `TryFindOrderInPosition`
- CYC=8 (1 base + 7 `||` branches)
- 16 lines
- Reduction: 25 → 8 (68% decrease) ✓

**Total Complexity**:
- Original: 25 (single method)
- Refactored: 8 (parent) + 4 (helper1) + 3 (helper2) = 15 (distributed)
- Net reduction: 40% ✓

### 6.3 Maintainability Gains

**Before**:
- 7 inline compound predicates
- Asymmetry hidden in predicate structure
- Difficult to verify correctness

**After**:
- 2 named helpers with clear semantics
- Asymmetry explicit in helper names/signatures
- Parent reduced to 7-line sequential probe
- Each helper independently testable

---

## 7. VERIFICATION GATES

### 7.1 Gate 1: Iteration Order Preservation

**Test**: Verify short-circuit evaluation order unchanged.

**Method**:
1. Instrument original method with trace logging before extraction
2. Run test suite capturing evaluation order for 100 test cases
3. Apply extraction
4. Re-run same test suite with trace logging
5. Diff evaluation order logs

**Pass Criteria**: 100% match on evaluation order for all test cases.

**Failure Action**: Rollback extraction, analyze order divergence.

### 7.2 Gate 2: Asymmetry Preservation

**Test**: Verify Entry/Stop/T1 use ref-equality, T2-T5 do NOT.

**Method**:
1. Create test case with `Order` instance where `tracked == order` (ref-equal) but `tracked.OrderId != order.OrderId` (ID-unequal)
2. Verify Entry/Stop/T1 return TRUE (ref-equality wins)
3. Verify T2-T5 return FALSE (no ref-equality check)

**Pass Criteria**: Entry/Stop/T1 match on ref-equality, T2-T5 do NOT.

**Failure Action**: Rollback extraction, review helper predicate logic.

### 7.3 Gate 3: No Helper Reuse

**Test**: Verify `OrdersMatchByRefOrId` NOT called by new helpers.

**Method**:
1. Search extracted code for `OrdersMatchByRefOrId` calls
2. Verify zero matches in helper implementations

**Pass Criteria**: Zero calls to `OrdersMatchByRefOrId` in helpers.

**Failure Action**: Rollback extraction, remove `OrdersMatchByRefOrId` dependency.

### 7.4 Gate 4: Caller Untouched

**Test**: Verify single caller at line 719 unchanged.

**Method**:
1. Capture line 719 signature before extraction: `if (TryFindOrderInPosition(order, kvp.Key, out matchedEntry))`
2. Apply extraction
3. Verify line 719 IDENTICAL (no parameter changes, no call-site modifications)

**Pass Criteria**: Line 719 byte-identical before/after extraction.

**Failure Action**: Rollback extraction, review signature preservation.

---

## 8. IMPLEMENTATION STEPS

### 8.1 Pre-Extraction Checklist

- [ ] Verify current CYC=25 via complexity audit
- [ ] Confirm single caller at line 719
- [ ] Snapshot `OrdersMatchByRefOrId` signature (lines 234-238)
- [ ] Run full test suite, capture baseline results
- [ ] Create git checkpoint: `git commit -m "PRE-EXTRACT: TryFindOrderInPosition baseline"`

### 8.2 Surgical Edit Sequence

**Edit 1**: Insert Helper 1 after line 232 (after `TryFindOrderInPosition`)

```csharp
// Build 935 [R-01]: Helper for Entry/Stop/T1 dictionary probes with ref-equality short-circuit.
// Returns true if 'order' matches the tracked order in 'dict' at 'entryKey' via reference OR OrderId.
// Asymmetric: checks ref-equality FIRST, then OrderId if tracked != null.
private bool TryFindOrder_MatchesEntryStopOrT1(
    ConcurrentDictionary<string, Order> dict,
    string entryKey,
    Order order)
{
    Order tracked;
    return dict.TryGetValue(entryKey, out tracked)
        && (tracked == order || (tracked != null && tracked.OrderId == order.OrderId));
}
```

**Edit 2**: Insert Helper 2 after Helper 1

```csharp
// Build 935 [R-01]: Helper for T2-T5 dictionary probes with OrderId-only equality.
// Returns true if 'order' matches the tracked order in 'dict' at 'entryKey' via OrderId ONLY.
// Asymmetric: NO ref-equality short-circuit (differs from Helper 1).
private bool TryFindOrder_MatchesT2ThroughT5(
    ConcurrentDictionary<string, Order> dict,
    string entryKey,
    Order order)
{
    Order tracked;
    return dict.TryGetValue(entryKey, out tracked)
        && tracked != null
        && tracked.OrderId == order.OrderId;
}
```

**Edit 3**: Replace `TryFindOrderInPosition` body (lines 219-231)

```csharp
private bool TryFindOrderInPosition(Order order, string entryKey, out string matchedEntry)
{
    matchedEntry = null;
    
    // Sequential 7-step probe: preserves exact short-circuit order
    if (TryFindOrder_MatchesEntryStopOrT1(entryOrders, entryKey, order) ||
        TryFindOrder_MatchesEntryStopOrT1(stopOrders, entryKey, order) ||
        TryFindOrder_MatchesEntryStopOrT1(target1Orders, entryKey, order) ||
        TryFindOrder_MatchesT2ThroughT5(target2Orders, entryKey, order) ||
        TryFindOrder_MatchesT2ThroughT5(target3Orders, entryKey, order) ||
        TryFindOrder_MatchesT2ThroughT5(target4Orders, entryKey, order) ||
        TryFindOrder_MatchesT2ThroughT5(target5Orders, entryKey, order))
    {
        matchedEntry = entryKey;
        return true;
    }
    
    return false;
}
```

### 8.3 Post-Extraction Checklist

- [ ] Verify line 719 unchanged (caller untouched)
- [ ] Verify `OrdersMatchByRefOrId` NOT called by helpers
- [ ] Run complexity audit: confirm CYC=8 (parent), CYC=4 (helper1), CYC=3 (helper2)
- [ ] Run full test suite: confirm zero regressions
- [ ] Execute Verification Gate 1 (iteration order)
- [ ] Execute Verification Gate 2 (asymmetry preservation)
- [ ] Execute Verification Gate 3 (no helper reuse)
- [ ] Execute Verification Gate 4 (caller untouched)
- [ ] Run `powershell -File .\deploy-sync.ps1` (hard-link sync)
- [ ] Verify diff under 150 KB limit
- [ ] Create git checkpoint: `git commit -m "POST-EXTRACT: TryFindOrderInPosition CYC 25→8"`

---

## 9. ROLLBACK PLAN

### 9.1 Rollback Triggers

**Immediate Rollback** if ANY of:
1. Verification Gate 1 fails (iteration order divergence)
2. Verification Gate 2 fails (asymmetry violation)
3. Verification Gate 3 fails (helper reuse detected)
4. Verification Gate 4 fails (caller modified)
5. Test suite regression (any test failure)
6. Diff exceeds 150 KB limit
7. Build failure after `deploy-sync.ps1`

### 9.2 Rollback Procedure

**Step 1**: Revert to pre-extraction checkpoint
```bash
git reset --hard HEAD~1  # Revert to PRE-EXTRACT commit
```

**Step 2**: Verify rollback success
```bash
git diff HEAD~1  # Should show zero diff
```

**Step 3**: Re-run test suite
```bash
powershell -File .\scripts\test_stress.ps1
```

**Step 4**: Document failure
- Capture failed verification gate output
- Log divergence details in `docs/brain/extraction_failure_tw2.md`
- Update ticket with failure analysis

**Step 5**: Escalate to Adjudicator
- Request Arena AI review of failed extraction
- Provide gate failure logs
- Await revised extraction strategy

### 9.3 Partial Rollback (Helper-Only)

If helpers are correct but parent refactor fails:

**Step 1**: Keep helpers, revert parent only
```bash
git checkout HEAD~1 -- src/V12_002.Orders.Callbacks.AccountOrders.cs
# Manually re-apply helper insertions (Edit 1 & 2)
# Keep original parent body (lines 219-231)
```

**Step 2**: Verify helpers unused
- Confirm zero calls to new helpers
- Helpers remain as "dead code" for future use

**Step 3**: Document partial state
- Update ticket: "Helpers extracted, parent refactor deferred"
- Create follow-up ticket for parent refactor retry

---

## 10. RISK ASSESSMENT

### 10.1 High-Risk Areas

**Risk 1**: Short-circuit order divergence
- **Likelihood**: LOW (sequential `||` structure preserves order)
- **Impact**: HIGH (could match wrong dictionary)
- **Mitigation**: Verification Gate 1 with trace logging

**Risk 2**: Asymmetry violation
- **Likelihood**: LOW (two separate helpers encode asymmetry)
- **Impact**: CRITICAL (T2-T5 would incorrectly match on ref-equality)
- **Mitigation**: Verification Gate 2 with ref-equality test case

**Risk 3**: Null-check addition
- **Likelihood**: MEDIUM (common refactoring mistake)
- **Impact**: HIGH (changes behavior, violates H10)
- **Mitigation**: Code review, explicit "NO null check" comments in helpers

**Risk 4**: Helper reuse
- **Likelihood**: LOW (explicit constraint in plan)
- **Impact**: MEDIUM (wrong null-check semantics)
- **Mitigation**: Verification Gate 3 with grep search

### 10.2 Medium-Risk Areas

**Risk 5**: Diff bloat
- **Likelihood**: LOW (3 surgical edits, ~40 lines added)
- **Impact**: MEDIUM (exceeds 150 KB limit)
- **Mitigation**: Pre-check diff size before commit

**Risk 6**: Test suite regression
- **Likelihood**: LOW (zero logic drift by design)
- **Impact**: HIGH (blocks merge)
- **Mitigation**: Full test suite run in post-extraction checklist

### 10.3 Low-Risk Areas

**Risk 7**: Build failure
- **Likelihood**: VERY LOW (no new dependencies, valid C# syntax)
- **Impact**: MEDIUM (blocks deployment)
- **Mitigation**: `deploy-sync.ps1` in post-extraction checklist

**Risk 8**: Caller modification
- **Likelihood**: VERY LOW (explicit constraint, Verification Gate 4)
- **Impact**: LOW (easy to detect and fix)
- **Mitigation**: Byte-identical check on line 719

---

## 11. SUCCESS CRITERIA

### 11.1 Functional Requirements

- [ ] **F1**: For every `(Order, entryKey)` tuple, return SAME `(bool, matchedEntry)` as original
- [ ] **F2**: Short-circuit evaluation order IDENTICAL to original
- [ ] **F3**: Entry/Stop/T1 use ref-equality short-circuit
- [ ] **F4**: T2-T5 use OrderId-only equality (NO ref-equality)
- [ ] **F5**: Single caller at line 719 UNCHANGED

### 11.2 Non-Functional Requirements

- [ ] **NF1**: Parent CYC ≤ 10 (target: 8)
- [ ] **NF2**: Helper 1 CYC ≤ 4 (target: 4)
- [ ] **NF3**: Helper 2 CYC ≤ 3 (target: 3)
- [ ] **NF4**: Zero test suite regressions
- [ ] **NF5**: Diff under 150 KB limit
- [ ] **NF6**: Build success after `deploy-sync.ps1`
- [ ] **NF7**: Zero calls to `OrdersMatchByRefOrId` in helpers
- [ ] **NF8**: No `order != null` guard added anywhere

### 11.3 Documentation Requirements

- [ ] **D1**: Helper 1 comment explains ref-equality short-circuit
- [ ] **D2**: Helper 2 comment explains NO ref-equality (asymmetry)
- [ ] **D3**: Parent comment references Build 935 [R-01]
- [ ] **D4**: Extraction logged in git commit message
- [ ] **D5**: Complexity reduction documented in ticket

---

## 12. APPENDIX A: DIFF PREVIEW

### 12.1 Estimated Diff Size

**Lines Added**: ~40
- Helper 1: 11 lines (signature + body + comment)
- Helper 2: 10 lines (signature + body + comment)
- Parent refactor: 19 lines (new body)

**Lines Removed**: ~13
- Original parent body: 13 lines (lines 219-231)

**Net Change**: +27 lines

**Estimated Diff**: ~2 KB (well under 150 KB limit ✓)

### 12.2 File Structure After Extraction

```
Lines 217-218: TryFindOrderInPosition signature + opening brace
Lines 219-234: TryFindOrderInPosition refactored body (16 lines)
Lines 235-245: TryFindOrder_MatchesEntryStopOrT1 (11 lines)
Lines 246-255: TryFindOrder_MatchesT2ThroughT5 (10 lines)
Lines 256-258: OrdersMatchByRefOrId (unchanged, 3 lines)
```

**Total Method Block**: 42 lines (217-258)
- Original: 22 lines (217-238)
- Refactored: 42 lines (217-258)
- Growth: +20 lines (acceptable for 68% complexity reduction)

---

## 13. APPENDIX B: ALTERNATIVE APPROACHES (REJECTED)

### 13.1 Single Helper with Flag Parameter

**Approach**: One helper with `bool checkRefEquality` parameter.

**Rejection Reason**: Violates "Make illegal states unrepresentable" principle. Flag parameter allows caller to pass wrong value, creating runtime bug risk. Two separate helpers encode asymmetry in TYPE SYSTEM, making misuse impossible.

### 13.2 Loop-Based Iteration

**Approach**: Array of dictionaries + loop instead of 7 sequential `if` statements.

**Rejection Reason**: Violates B7 (preserve short-circuit order). Loop would require index-based dispatch to select correct helper, adding complexity. Sequential `if` structure is clearer and preserves exact evaluation order.

### 13.3 Reuse OrdersMatchByRefOrId

**Approach**: Call existing `OrdersMatchByRefOrId` helper for Entry/Stop/T1.

**Rejection Reason**: Violates H10/Q-V3=C. `OrdersMatchByRefOrId` checks BOTH parameters for null (`trackedOrder != null && order != null`). Original predicate does NOT check `order != null`. Reusing would add null guard, changing behavior.

### 13.4 Three Helpers (Entry, Stop/T1, T2-T5)

**Approach**: Separate helper for Entry, separate for Stop/T1, separate for T2-T5.

**Rejection Reason**: Over-engineering. Entry and Stop/T1 have IDENTICAL predicate logic. Two helpers (ref+OrderId vs OrderId-only) are sufficient to encode asymmetry. Three helpers would add unnecessary code duplication.

---

## 14. SIGN-OFF

**Architect**: Traycer (Frontier Mode)  
**Reviewed By**: [Pending Adjudicator Review]  
**Approved By**: [Pending Director Sign-off]  

**Plan Status**: READY FOR STAGE 3 (DNA & PR AUDIT)

**Next Steps**:
1. Submit plan to Arena AI for adversarial audit (Stage 3)
2. Address any audit findings
3. Obtain Director approval
4. Hand off to Bob CLI (`v12-engineer`) for Stage 4 execution

---

**END OF PLAN**