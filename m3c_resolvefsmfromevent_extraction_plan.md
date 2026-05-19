# M3-C: ResolveFsmFromEvent Extraction Plan
## FINAL God-Function Elimination - Phase 7 Completion

**Status:** PLAN-ONLY - Awaiting Director Approval  
**Build:** 1109  
**Date:** 2026-05-15  
**Agent:** Bob CLI (v12-engineer)

---

## Executive Summary

This is the **FINAL** method with CYC > 20 in the entire V12 codebase. After this extraction, Phase 7 complexity hardening will be **COMPLETE** with ZERO methods having CYC > 20.

**Target Method:**
- File: `src/V12_002.Symmetry.BracketFSM.cs`
- Method: `ResolveFsmFromEvent`
- Lines: 154-208 (55 lines)
- Current CYC: 22
- Current LOC: 55

**Mission:** Extract 3-tier FSM lookup strategy into focused handler methods while preserving exact resolution behavior.

---

## Current Implementation Analysis

### Method Purpose
`ResolveFsmFromEvent` resolves an `AccountEvent` to its corresponding `FollowerBracketFSM` using a 3-tier fallback strategy:

1. **Tier 1 (Primary):** O(1) OrderId map lookup via `_orderIdToFsmKey`
2. **Tier 2 (Secondary):** SignalName parsing and matching
3. **Tier 3 (Last Resort):** O(N) scan across all FSMs

### Complexity Breakdown

**Current Cyclomatic Complexity: 22**

Breakdown by tier:
- **Tier 1 (OrderId lookup):** CYC = 3
  - `if (!string.IsNullOrEmpty(evt.OrderId))` → +1
  - `if (_orderIdToFsmKey.TryGetValue(...))` → +1
  - Base complexity → +1
  
- **Tier 2 (SignalName parsing):** CYC = 5
  - `if (fsm == null && !string.IsNullOrEmpty(evt.SignalName))` → +2
  - `if (firstUnder >= 0 && firstUnder < evt.SignalName.Length - 1)` → +2
  - `if (_followerBrackets.TryGetValue(...))` → +1
  - `if (!string.IsNullOrEmpty(evt.OrderId))` (backfill) → +1

- **Tier 3 (O(N) scan):** CYC = 14
  - `if (fsm == null)` → +1
  - `foreach (var f in _followerBrackets.Values)` → +1
  - `if (f.AccountName != evt.AccountAlias) continue` → +1
  - `if (f.StopOrder != null && f.StopOrder.OrderId == evt.OrderId)` → +2
  - `for (int i = 0; i < 5; i++)` → +1
  - `if (f.Targets[i] != null && f.Targets[i].OrderId == evt.OrderId)` → +2
  - `if (foundT) break` → +1
  - `if (f.EntryOrder != null && f.EntryOrder.OrderId == evt.OrderId)` → +2
  - `if (fsm != null && !string.IsNullOrEmpty(evt.OrderId))` (backfill) → +1

**Total:** 3 + 5 + 14 = 22 CYC

### Critical Observations

1. **This is NOT FSM state transition logic** - it's a 3-tier lookup/resolution strategy
2. **No state mutations** - purely reads from dictionaries and returns FSM reference
3. **Thread-safe** - uses ConcurrentDictionary reads only
4. **Backfill pattern** - Tiers 2 and 3 populate `_orderIdToFsmKey` when successful
5. **Early exit optimization** - each tier checks `if (fsm == null)` before proceeding

---

## Extraction Strategy

### Approach: Tier-Based Handler Extraction

Extract each tier into a focused handler method. The residual router orchestrates the 3-tier fallback cascade.

**Key Principle:** Preserve exact lookup semantics and backfill behavior.

---

## Proposed Handler Methods

### Handler 1: `ResolveFsm_ByOrderId` (Tier 1)

**Purpose:** O(1) primary lookup via OrderId map

**Signature:**
```csharp
private FollowerBracketFSM ResolveFsm_ByOrderId(string orderId)
```

**Logic:**
- Guard: `if (string.IsNullOrEmpty(orderId)) return null;`
- Lookup: `_orderIdToFsmKey.TryGetValue(orderId, out var entryName)`
- Resolve: `_followerBrackets.TryGetValue(entryName, out fsm)`
- Return: `fsm` (or null)

**Expected CYC:** 3
- Guard check → +1
- TryGetValue → +1
- Base → +1

**LOC:** ~8 lines

---

### Handler 2: `ResolveFsm_BySignalName` (Tier 2)

**Purpose:** Secondary lookup via SignalName parsing with backfill

**Signature:**
```csharp
private FollowerBracketFSM ResolveFsm_BySignalName(string signalName, string orderId)
```

**Logic:**
- Guard: `if (string.IsNullOrEmpty(signalName)) return null;`
- Parse: Extract `fleetEntryName` from signal (e.g., "Stop_Fleet_Apex_1" → "Fleet_Apex_1")
- Lookup: `_followerBrackets.TryGetValue(fleetEntryName, out fsm)`
- Backfill: If found and `orderId` is valid, populate `_orderIdToFsmKey[orderId] = fleetEntryName`
- Return: `fsm` (or null)

**Expected CYC:** 5
- Guard check → +1
- IndexOf bounds check → +2
- TryGetValue → +1
- Backfill guard → +1

**LOC:** ~15 lines

**Critical:** Must preserve exact substring logic: `evt.SignalName.Substring(firstUnder + 1)`

---

### Handler 3: `ResolveFsm_ByScan` (Tier 3)

**Purpose:** Last-resort O(N) scan with backfill

**Signature:**
```csharp
private FollowerBracketFSM ResolveFsm_ByScan(string accountAlias, string orderId)
```

**Logic:**
- Guard: `if (string.IsNullOrEmpty(orderId)) return null;`
- Scan: `foreach (var f in _followerBrackets.Values)`
  - Filter: `if (f.AccountName != accountAlias) continue;`
  - Check StopOrder: `if (f.StopOrder != null && f.StopOrder.OrderId == orderId)`
  - Check Targets[0-4]: Loop through 5 targets
  - Check EntryOrder: `if (f.EntryOrder != null && f.EntryOrder.OrderId == orderId)`
- Backfill: If found, populate `_orderIdToFsmKey[orderId] = fsm.EntryName`
- Return: `fsm` (or null)

**Expected CYC:** 12
- Guard → +1
- foreach → +1
- Account filter → +1
- StopOrder check → +2
- for loop → +1
- Targets check → +2
- foundT check → +1
- EntryOrder check → +2
- Backfill guard → +1

**LOC:** ~25 lines

**Critical:** Must preserve exact scan order: StopOrder → Targets → EntryOrder

---

### Residual Router: `ResolveFsmFromEvent` (Orchestrator)

**Purpose:** Orchestrate 3-tier fallback cascade

**Signature:**
```csharp
private FollowerBracketFSM ResolveFsmFromEvent(AccountEvent evt)
```

**Logic:**
```csharp
private FollowerBracketFSM ResolveFsmFromEvent(AccountEvent evt)
{
    // Tier 1: O(1) OrderId lookup (primary)
    FollowerBracketFSM fsm = ResolveFsm_ByOrderId(evt.OrderId);
    if (fsm != null) return fsm;

    // Tier 2: SignalName parsing (secondary)
    fsm = ResolveFsm_BySignalName(evt.SignalName, evt.OrderId);
    if (fsm != null) return fsm;

    // Tier 3: O(N) scan (last resort)
    fsm = ResolveFsm_ByScan(evt.AccountAlias, evt.OrderId);
    return fsm;
}
```

**Expected CYC:** 3
- Tier 1 null check → +1
- Tier 2 null check → +1
- Base → +1

**LOC:** ~10 lines

---

## Complexity Summary

### Before Extraction
- **ResolveFsmFromEvent:** CYC = 22, LOC = 55

### After Extraction
- **ResolveFsm_ByOrderId:** CYC = 3, LOC = 8
- **ResolveFsm_BySignalName:** CYC = 5, LOC = 15
- **ResolveFsm_ByScan:** CYC = 12, LOC = 25
- **ResolveFsmFromEvent (router):** CYC = 3, LOC = 10

**Total CYC:** 3 + 5 + 12 + 3 = 23 (slight increase due to method boundaries, but all methods now ≤ 12)

**Residual CYC:** 3 ✅ (Target: ≤ 5)  
**Max Handler CYC:** 12 ✅ (Target: ≤ 12)

---

## Correctness Verification Strategy

### 1. Resolution Semantics Preservation

**Test Cases:**
- **T1:** OrderId exists in `_orderIdToFsmKey` → Tier 1 resolves
- **T2:** OrderId missing, SignalName valid → Tier 2 resolves + backfills
- **T3:** OrderId + SignalName missing → Tier 3 scans + backfills
- **T4:** No match found → Returns null
- **T5:** Multiple FSMs, correct account filtering → Tier 3 filters by `AccountAlias`

### 2. Backfill Behavior Verification

**Invariant:** After Tier 2 or Tier 3 success, `_orderIdToFsmKey[orderId]` must be populated

**Test:**
- First call: Tier 2/3 resolves + backfills
- Second call: Tier 1 resolves (O(1) fast path)

### 3. Scan Order Preservation (Tier 3)

**Critical:** Tier 3 must check in exact order: StopOrder → Targets[0-4] → EntryOrder

**Verification:**
- Create FSM with all order types
- Verify scan finds StopOrder first (if matching)
- Verify scan finds Targets before EntryOrder

### 4. Thread Safety

**Invariant:** All handlers use ConcurrentDictionary reads only (no locks)

**Verification:**
- No `lock()` statements
- Only `TryGetValue` and indexer writes (ConcurrentDictionary is thread-safe for writes)

---

## Risk Assessment

### Risk 1: Backfill Logic Duplication
**Severity:** LOW  
**Mitigation:** Handlers 2 and 3 both backfill `_orderIdToFsmKey`. Logic is identical (2 lines). Acceptable duplication for clarity.

### Risk 2: Tier 3 Scan Order Change
**Severity:** MEDIUM  
**Mitigation:** Document exact scan order in handler. Add unit test to verify order.

### Risk 3: SignalName Parsing Edge Cases
**Severity:** LOW  
**Mitigation:** Preserve exact substring logic: `IndexOf('_')` + bounds check + `Substring(firstUnder + 1)`

### Risk 4: Null Reference in Tier 3
**Severity:** LOW  
**Mitigation:** All null checks preserved: `f.StopOrder != null`, `f.Targets[i] != null`, `f.EntryOrder != null`

---

## Implementation Checklist

### Phase 1: Handler Extraction
- [ ] Extract `ResolveFsm_ByOrderId` (Tier 1)
- [ ] Extract `ResolveFsm_BySignalName` (Tier 2)
- [ ] Extract `ResolveFsm_ByScan` (Tier 3)
- [ ] Verify each handler compiles independently

### Phase 2: Router Refactor
- [ ] Refactor `ResolveFsmFromEvent` to call handlers
- [ ] Verify residual CYC ≤ 5
- [ ] Verify all handlers CYC ≤ 12

### Phase 3: Verification
- [ ] Run complexity audit: `python scripts/complexity_audit.py`
- [ ] Verify ZERO methods with CYC > 20
- [ ] Run build: `powershell -File .\scripts\build_readiness.ps1`
- [ ] Run stress test: `powershell -File .\scripts\test_stress.ps1`

### Phase 4: Behavioral Testing
- [ ] Test T1: Tier 1 resolution (OrderId hit)
- [ ] Test T2: Tier 2 resolution (SignalName hit + backfill)
- [ ] Test T3: Tier 3 resolution (scan + backfill)
- [ ] Test T4: No match (returns null)
- [ ] Test T5: Account filtering (Tier 3)
- [ ] Verify backfill behavior (second call uses Tier 1)

### Phase 5: Integration
- [ ] Sync to NinjaTrader: `powershell -File .\deploy-sync.ps1`
- [ ] F5 in NinjaTrader (verify no runtime errors)
- [ ] Verify BUILD_TAG in logs

---

## Success Criteria

1. ✅ **Residual CYC ≤ 5:** Router method has CYC = 3
2. ✅ **Handler CYC ≤ 12:** Max handler CYC = 12 (Tier 3)
3. ✅ **Zero CYC > 20:** This is the FINAL god-function
4. ✅ **Exact Resolution Semantics:** All test cases pass
5. ✅ **Backfill Behavior Preserved:** Tier 2/3 populate `_orderIdToFsmKey`
6. ✅ **Thread Safety:** No locks, ConcurrentDictionary only
7. ✅ **Build Success:** No compilation errors
8. ✅ **Runtime Verification:** F5 in NinjaTrader succeeds

---

## Post-Extraction Metrics

### Complexity Audit (Expected)
```
PHASE 7 COMPLEXITY HARDENING: COMPLETE
========================================
Total Methods: ~450
Methods with CYC > 20: 0 ✅
Methods with CYC > 15: ~5
Methods with CYC > 10: ~25
Average CYC: ~4.2
```

### Phase 7 Completion Status
- **M3-A:** HandleTextBoxKeyInput ✅ (Completed)
- **M3-B:** HandleFleetStopFill ✅ (Completed)
- **M3-C:** ResolveFsmFromEvent ⏳ (This ticket)

**After M3-C:** Phase 7 = 100% COMPLETE 🎉

---

## Notes for Engineer

1. **This is NOT FSM state transition logic** - it's a lookup/resolution strategy. Do NOT confuse with `ProcessBracketEvent` (which handles state transitions).

2. **Preserve exact semantics:**
   - Tier 1: O(1) fast path
   - Tier 2: SignalName parsing with exact substring logic
   - Tier 3: O(N) scan with exact order (StopOrder → Targets → EntryOrder)

3. **Backfill is critical:** Tiers 2 and 3 must populate `_orderIdToFsmKey` on success to enable future Tier 1 hits.

4. **No allocations:** All handlers return existing FSM references or null. Zero new objects.

5. **ASCII-only:** No Unicode in comments or strings.

6. **This is the FINAL god-function.** After this extraction, Phase 7 is COMPLETE and the codebase will have ZERO methods with CYC > 20. This is a historic milestone for V12.

---

## Approval Gate

**Director Sign-off Required:**
- [ ] Extraction strategy approved
- [ ] Handler signatures approved
- [ ] Complexity targets approved (Residual ≤ 5, Handlers ≤ 12)
- [ ] Verification strategy approved

**Next Step:** Switch to Code mode for implementation (Bob CLI or Codex CLI)

---

**END OF PLAN**