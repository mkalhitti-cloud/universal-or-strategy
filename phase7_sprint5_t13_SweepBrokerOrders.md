# [Phase7-S5-T13] SweepBrokerOrders Extraction Plan

**BUILD_TAG**: `1111.007-phase7-t13`  
**File**: `src/V12_002.SIMA.Lifecycle.cs`  
**Target Function**: `SweepBrokerOrders` (line 1082-1141)  
**Current Metrics**: CYC=28, LOC=60, Nesting=8  
**Target Metrics**: Residual CYC ≤19, Sub-helpers CYC ≤19

---

## 1. Forensic Analysis

### Current State
```
Function: SweepBrokerOrders(bool force)
- Lines: 1082-1141 (60 LOC)
- Cyclomatic Complexity: 28 (HIGH)
- Max Nesting Depth: 8
- Parameters: 1 (force: bool)
- Returns: int (brokerCancels count)
- Single Caller: CancelAllV12GtcOrders (line 1033)
```

### Complexity Drivers
1. **Prefix array initialization** (force-dependent): +2 branches
2. **Account iteration loop**: +1
3. **Fleet account filter**: +1
4. **Order iteration loop**: +1
5. **Instrument match check**: +1
6. **OrderState validation** (5 conditions): +5
7. **V12 prefix matching loop**: +1
8. **Prefix match check**: +1
9. **!force bracket protection block**: +1
10. **Bracket order detection** (8 StartsWith checks): +8
11. **Protected bracket Print**: nested condition
12. **Order cancellation try-catch**: +1

**Total Observed**: 28 CYC

### Extraction Strategy

The function has three distinct logical phases:

1. **Phase A: Prefix Selection** (lines 1085-1088)
   - Force-dependent prefix array initialization
   - CYC contribution: ~2

2. **Phase B: V12 Order Detection** (lines 1095-1104)
   - Prefix matching loop
   - CYC contribution: ~3

3. **Phase C: Bracket Protection Logic** (lines 1107-1122)
   - Soft-disable bracket exclusion
   - 8 StartsWith checks + Print
   - CYC contribution: ~10

**Extraction Plan**: Extract Phase C (bracket protection) into `ShouldProtectBracketOrder` helper, reducing residual CYC by ~10.

---

## 2. Implementation Plan

### 2.1 Extract Helper: `ShouldProtectBracketOrder`

**Purpose**: Encapsulate bracket order protection logic for soft-disable scenarios.

**Signature**:
```csharp
private bool ShouldProtectBracketOrder(string orderName, bool force)
```

**Logic**:
- If `force == true`, return `false` (no protection needed)
- Check if `orderName` starts with any bracket prefix:
  - `Stop_`, `S_`, `T1_`, `T2_`, `T3_`, `T4_`, `T5_`, `Target_`
- Return `true` if bracket order detected, `false` otherwise

**Metrics**: CYC ≤10 (1 base + 1 force check + 8 StartsWith)

### 2.2 Extract Helper: `IsV12OrderPrefix`

**Purpose**: Determine if an order name matches V12 prefixes.

**Signature**:
```csharp
private bool IsV12OrderPrefix(string orderName, string[] v12Prefixes)
```

**Logic**:
- Loop through `v12Prefixes` array
- Return `true` if `orderName.StartsWith(prefix, OrdinalIgnoreCase)`
- Return `false` if no match

**Metrics**: CYC ≤3 (1 base + 1 loop + 1 match check)

### 2.3 Refactored Residual: `SweepBrokerOrders`

**New Structure**:
```csharp
private int SweepBrokerOrders(bool force)
{
    int brokerCancels = 0;
    var v12Prefixes = force
        ? new[] { "Stop_", "S_", "T1_", "T2_", "T3_", "T4_", "T5_", "Fleet_", "RMA", "Trend", "MOMO", "OR", "RETEST", "FFMA" }
        : new[] { "Fleet_", "RMA", "Trend", "MOMO", "OR", "RETEST", "FFMA" };

    foreach (Account acct in Account.All)
    {
        if (!IsFleetAccount(acct)) continue;
        try
        {
            foreach (Order ord in acct.Orders.ToArray())
            {
                if (ord.Instrument?.FullName != Instrument?.FullName) continue;
                if (ord.OrderState != OrderState.Working    &&
                    ord.OrderState != OrderState.Accepted   &&
                    ord.OrderState != OrderState.Submitted  &&
                    ord.OrderState != OrderState.ChangePending &&
                    ord.OrderState != OrderState.ChangeSubmitted) continue;
                
                string ordName = ord.Name ?? string.Empty;
                if (!IsV12OrderPrefix(ordName, v12Prefixes)) continue;

                // [FIX-FF]: Bracket protection on soft disable
                if (ShouldProtectBracketOrder(ordName, force))
                {
                    Print(string.Format("[FIX-FF] Protected bracket order from sweep: {0} on {1}",
                        ordName, acct.Name));
                    continue;
                }

                try { acct.Cancel(new[] { ord }); brokerCancels++; } catch { }
            }
        }
        catch { }
    }
    return brokerCancels;
}
```

**Expected Metrics**: CYC ≤18 (28 - 10 bracket checks - 2 prefix loop = 16, plus helper calls)

---

## 3. Guardrails & Constraints

### 3.1 V12 DNA Invariants
- **INV-1.1**: ASCII-only strings (already compliant)
- **INV-1.2**: No lock() statements (not applicable - no shared state)
- **INV-1.3**: Atomic operations (not applicable - local accumulator)
- **INV-1.4**: No Unicode/emoji (already compliant)
- **INV-1.5**: Hard-link sync required post-edit

### 3.2 Signature Policy
- **Status**: FREE (single caller, returns int)
- **Action**: Signature unchanged, caller unchanged

### 3.3 Co-Residency Warning (H8)
**DO NOT TOUCH** in this commit:
- `HydrateFSMsFromWorkingOrders` (line 969, CYC=72)
- `AdoptFleetWorkingOrders` (line 309, CYC=36)
- T07 extracted helpers: `AdoptMasterWorkingOrders` and related

### 3.4 LOC Deviation Pre-Flag (DEVIATION-T13-A)
- Original: 60 LOC
- Expected after extraction: ~45 LOC residual + ~15 LOC helpers = 60 total
- Sub-helpers may be 10-15 LOC (acceptable per D-S5)

### 3.5 Sequencing
- **Dependency**: T13 commits AFTER T07 (sequential, same file)
- **Reason**: Avoid merge conflicts per D-T1

---

## 4. Verification Criteria

### 4.1 Complexity Metrics
- [ ] Residual `SweepBrokerOrders`: CYC ≤19
- [ ] `ShouldProtectBracketOrder`: CYC ≤10
- [ ] `IsV12OrderPrefix`: CYC ≤3
- [ ] All helpers: LOC ≥10 (modulo DEVIATION-T13-A)

### 4.2 Behavioral Invariants
- [ ] Caller `CancelAllV12GtcOrders` compiles unchanged
- [ ] All verbatim Print/AppendLine counts unchanged
- [ ] `SweepBrokerOrders` no longer in "CYC > 20 remaining" list

### 4.3 Co-Residency Check
- [ ] `HydrateFSMsFromWorkingOrders` untouched in diff
- [ ] `AdoptFleetWorkingOrders` untouched in diff
- [ ] T07 helpers untouched in diff

### 4.4 Build & Sync
- [ ] `powershell -File .\scripts\build_readiness.ps1` passes
- [ ] `powershell -File .\deploy-sync.ps1` succeeds
- [ ] BUILD_TAG bumped to `1111.007-phase7-t13`

### 4.5 F5 Acceptance
**Test**: Press F5 in NinjaTrader, trigger `CancelAllV12GtcOrders` via panel "Cancel All" command.

**Expected**:
- Output shows broker-cancel count
- Per-order log lines appear
- Count matches actually-cancelled orders
- Protected bracket orders show `[FIX-FF]` messages

---

## 5. Implementation Steps

### Step 1: Extract `IsV12OrderPrefix`
- Insert after line 1141 (end of `SweepBrokerOrders`)
- Implement prefix matching loop
- Verify CYC ≤3

### Step 2: Extract `ShouldProtectBracketOrder`
- Insert after `IsV12OrderPrefix`
- Implement bracket detection with 8 StartsWith checks
- Include Print statement for protected orders
- Verify CYC ≤10

### Step 3: Refactor Residual `SweepBrokerOrders`
- Replace inline prefix loop with `IsV12OrderPrefix` call
- Replace bracket protection block with `ShouldProtectBracketOrder` call
- Preserve all comments, especially `[FIX-FF]`
- Verify CYC ≤19

### Step 4: Verify & Build
- Run complexity audit
- Check co-resident functions untouched
- Execute build_readiness.ps1
- Execute deploy-sync.ps1

### Step 5: F5 Test
- Load strategy in NinjaTrader
- Trigger "Cancel All" command
- Verify output logs
- Confirm bracket protection works

### Step 6: Documentation
- Update BUILD_TAG to `1111.007-phase7-t13`
- Create acceptance report
- Update Living Document Registry

---

## 6. Verbatim Print Assertions

**Current Print Statements** (to be preserved):
1. Line 1117: `"[FIX-FF] Protected bracket order from sweep: {0} on {1}"`

**Post-Extraction**:
- Print statement moves to `ShouldProtectBracketOrder` helper
- Exact format and parameters preserved
- Grep count must remain 1

---

## 7. Risk Assessment

**Risk Level**: LOW
- Single caller with FREE signature policy
- Pure extraction, zero behavior change
- Startup/cleanup path (not hot path)
- Clear logical boundaries for extraction

**Mitigation**:
- Preserve all comments verbatim
- Maintain exact Print format
- Verify co-resident functions untouched
- F5 test before sign-off

---

## 8. Success Criteria Summary

1. ✅ Residual CYC ≤19
2. ✅ Helper CYCs ≤19 each
3. ✅ Caller unchanged
4. ✅ Co-resident functions untouched
5. ✅ All Prints preserved
6. ✅ Build passes
7. ✅ F5 test passes
8. ✅ BUILD_TAG bumped

---

**Status**: READY FOR IMPLEMENTATION  
**Estimated Effort**: 30 minutes  
**Complexity**: MEDIUM (clear extraction boundaries, low risk)