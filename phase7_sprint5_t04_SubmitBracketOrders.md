# Phase 7 Sprint 5 T04: SubmitBracketOrders Extraction Plan

**BUILD_TAG**: `1111.007-phase7-t4`  
**Target**: `SubmitBracketOrders` in `src/V12_002.Orders.Management.cs`  
**Current Metrics**: CYC=25, LOC=197 (lines 37-234)  
**Goal**: Residual CYC ≤19, all sub-helpers CYC ≤19, LOC ≥15

---

## 1. FORENSIC ANALYSIS

### 1.1 Current Structure
```
SubmitBracketOrders(string entryName, PositionInfo pos)
├─ Line 39: Early return guard (BracketSubmitted check)
├─ Lines 41-233: try block
│  ├─ Line 44: ValidateStopPrice call
│  ├─ Lines 46-54: Follower routing + OCO setup
│  ├─ Lines 56-106: Stop order submission (master/follower branching)
│  │  ├─ Lines 58-88: Follower stop path (CreateOrder + Submit + error handling)
│  │  ├─ Lines 90-96: Master stop path
│  │  └─ Lines 98-106: Null-guard + flatten fallback
│  ├─ Lines 108-179: Target loop (T1-T5)
│  │  ├─ Lines 111-123: Loop header + runner detection
│  │  ├─ Lines 125-137: Price validation + rounding
│  │  ├─ Lines 139-176: Target submission (master/follower branching)
│  │  └─ Line 178: Accumulate nonRunnerLimitQty
│  ├─ Line 181: Set CurrentStopPrice
│  ├─ Lines 183-193: Stop quantity audit
│  ├─ Lines 199-202: Follower bracket confirmation print
│  ├─ Lines 204-220: Bracket message construction
│  └─ Lines 222-228: Target sum verification
└─ Lines 230-233: catch block with ERROR print
```

### 1.2 Caller Sites (SOFT-LOCK - DO NOT MODIFY)
1. **Line 225** in `src/V12_002.Orders.Callbacks.cs`: `HandleEntryOrderFilled` - averageFillPrice=0 guard path
2. **Line 246** in `src/V12_002.Orders.Callbacks.cs`: `HandleEntryOrderFilled` - normal fill path

Both callers pass `(kvp.Key, pos)` - signature MUST remain stable.

### 1.3 Critical Invariants (BUILD 981 Protocol)

**INV-3.1**: Direct `stopOrders[entryName] = sOrd` write at line 78 (follower) and line 106 (master) is MANDATORY. DO NOT wrap in `Enqueue`.

**INV-3.2**: Direct `targetDict[entryName] = limitOrder` write at line 174 is MANDATORY. DO NOT wrap in `Enqueue`.

**INV-3.3**: Bracket submission order MUST be preserved:
1. `pos.BracketSubmitted` guard (line 39)
2. Stop `CreateOrder` + `Submit` (lines 62-79 follower, 93-94 master)
3. Target loop `CreateOrder` + `Submit` (lines 111-179)
4. Dictionary registrations (lines 78, 106, 174)

**INV-3.4**: `pos.BracketSubmitted = true` is currently MISSING (removed per comment at line 197). This is a KNOWN ISSUE - do NOT re-add during extraction.

**INV-3.5**: Verbatim print string: `"ERROR SubmitBracketOrders: "` at line 232.

### 1.4 Verbatim Print Baseline
```bash
grep -cn "ERROR SubmitBracketOrders" src/V12_002.Orders.Management.cs
# Expected: 1 match at line 232

grep -cn "BRACKET_FATAL" src/V12_002.Orders.Management.cs
# Expected: 3 matches (lines 68, 84, 102)

grep -cn "TARGET_SKIP" src/V12_002.Orders.Management.cs
# Expected: 1 match (line 128)

grep -cn "TARGET_WARN" src/V12_002.Orders.Management.cs
# Expected: 2 matches (lines 151, 170)

grep -cn "FORENSIC" src/V12_002.Orders.Management.cs
# Expected: 2 matches (lines 120, 136)

grep -cn "STOP_AUDIT" src/V12_002.Orders.Management.cs
# Expected: 2 matches (lines 186, 191)

grep -cn "938-BRACKET" src/V12_002.Orders.Management.cs
# Expected: 1 match (line 201)

grep -cn "BRACKET_WARN" src/V12_002.Orders.Management.cs
# Expected: 1 match (line 226)
```

### 1.5 Co-Residency Warning
**DO NOT TOUCH** in this commit:
- `ReconcileOrphanedOrders` (CYC=46) - Sprint 6 target
- `RemoveGhostOrderRef` (CYC=37) - Sprint 6 target
- `CleanupPosition` (CYC=33) - Sprint 6 target
- `FlattenAll` (CYC=41) - Sprint 6 target
- `FlattenPositionByName` (CYC=22) - Sprint 6 target

These share the `V12_002.Orders.Management.cs` partial class.

---

## 2. EXTRACTION STRATEGY

### 2.1 Proposed Sub-Helpers (5 functions)

#### H1: `ValidateBracketEntryGuard`
**Purpose**: Entry validation + early return logic  
**Lines**: 39, 44, 46-54  
**Estimated CYC**: 3  
**Estimated LOC**: 18  
**Signature**: `private bool ValidateBracketEntryGuard(string entryName, PositionInfo pos, out double validatedStopPrice, out bool isFollowerSubmit, out OrderAction bracketExitAction, out string bracketOcoId)`  
**Returns**: `false` if should abort (BracketSubmitted=true), `true` if should proceed  
**Extracts**:
- Line 39: `if (pos.BracketSubmitted) return;` → becomes `return false;`
- Line 44: `ValidateStopPrice` call
- Lines 46-54: Follower routing + OCO setup

#### H2: `SubmitStopOrderSafe`
**Purpose**: Stop order submission with master/follower branching + null-guard + flatten fallback  
**Lines**: 56-106  
**Estimated CYC**: 8  
**Estimated LOC**: 52  
**Signature**: `private Order SubmitStopOrderSafe(string entryName, PositionInfo pos, bool isFollowerSubmit, OrderAction bracketExitAction, double validatedStopPrice, string bracketOcoId)`  
**Returns**: `Order` (null on failure - caller must handle)  
**Extracts**:
- Lines 58-88: Follower stop path (CreateOrder + Submit + try/catch + emergency flatten)
- Lines 90-96: Master stop path
- Lines 98-106: Null-guard + flatten + dict registration

**CRITICAL**: Preserves direct `stopOrders[entryName] = sOrd` writes (INV-3.1).

#### H3: `SubmitTargetOrdersLoop`
**Purpose**: Target loop (T1-T5) with runner detection, price validation, master/follower submission  
**Lines**: 108-179  
**Estimated CYC**: 9  
**Estimated LOC**: 73  
**Signature**: `private void SubmitTargetOrdersLoop(string entryName, PositionInfo pos, bool isFollowerSubmit, OrderAction bracketExitAction, string bracketOcoId, out int nonRunnerLimitQty, out int runnerQty)`  
**Returns**: void (out params for qty tracking)  
**Extracts**:
- Lines 108-109: Initialize qty accumulators
- Lines 111-179: Full target loop with all branching

**CRITICAL**: Preserves direct `targetDict[entryName] = limitOrder` writes (INV-3.2).

#### H4: `AuditStopQuantityAndPrint`
**Purpose**: Stop quantity audit + bracket message construction + target sum verification  
**Lines**: 181-228  
**Estimated CYC**: 4  
**Estimated LOC**: 49  
**Signature**: `private void AuditStopQuantityAndPrint(string entryName, PositionInfo pos, Order stopOrder, double validatedStopPrice, int nonRunnerLimitQty, int runnerQty, bool isFollowerSubmit)`  
**Returns**: void  
**Extracts**:
- Line 181: Set `pos.CurrentStopPrice`
- Lines 183-193: Stop quantity audit
- Lines 199-202: Follower bracket confirmation
- Lines 204-220: Bracket message construction
- Lines 222-228: Target sum verification

#### H5: `LogBracketSubmissionError`
**Purpose**: Catch block error logging  
**Lines**: 230-233  
**Estimated CYC**: 1  
**Estimated LOC**: 5  
**Signature**: `private void LogBracketSubmissionError(Exception ex)`  
**Returns**: void  
**Extracts**:
- Line 232: Verbatim `"ERROR SubmitBracketOrders: "` print

**CRITICAL**: Preserves exact print string (INV-3.5).

### 2.2 Residual `SubmitBracketOrders`
**Estimated CYC**: 5 (guard call + 4 helper calls + catch)  
**Estimated LOC**: 25  
**Structure**:
```csharp
private void SubmitBracketOrders(string entryName, PositionInfo pos)
{
    if (!ValidateBracketEntryGuard(entryName, pos, out double validatedStopPrice, 
        out bool isFollowerSubmit, out OrderAction bracketExitAction, out string bracketOcoId))
        return;

    try
    {
        Order stopOrder = SubmitStopOrderSafe(entryName, pos, isFollowerSubmit, 
            bracketExitAction, validatedStopPrice, bracketOcoId);
        if (stopOrder == null) return; // Flatten already handled in helper

        SubmitTargetOrdersLoop(entryName, pos, isFollowerSubmit, bracketExitAction, 
            bracketOcoId, out int nonRunnerLimitQty, out int runnerQty);

        AuditStopQuantityAndPrint(entryName, pos, stopOrder, validatedStopPrice, 
            nonRunnerLimitQty, runnerQty, isFollowerSubmit);
    }
    catch (Exception ex)
    {
        LogBracketSubmissionError(ex);
    }
}
```

---

## 3. IMPLEMENTATION SEQUENCE

### Step 1: Baseline Verification
```bash
# Capture current metrics
python scripts/v12_split.py

# Capture verbatim print baseline
grep -cn "ERROR SubmitBracketOrders" src/V12_002.Orders.Management.cs > baseline_prints.txt
grep -cn "BRACKET_FATAL" src/V12_002.Orders.Management.cs >> baseline_prints.txt
grep -cn "TARGET_SKIP" src/V12_002.Orders.Management.cs >> baseline_prints.txt
grep -cn "TARGET_WARN" src/V12_002.Orders.Management.cs >> baseline_prints.txt
grep -cn "FORENSIC" src/V12_002.Orders.Management.cs >> baseline_prints.txt
grep -cn "STOP_AUDIT" src/V12_002.Orders.Management.cs >> baseline_prints.txt
grep -cn "938-BRACKET" src/V12_002.Orders.Management.cs >> baseline_prints.txt
grep -cn "BRACKET_WARN" src/V12_002.Orders.Management.cs >> baseline_prints.txt

# Verify caller sites unchanged
grep -n "SubmitBracketOrders" src/V12_002.Orders.Callbacks.cs
# Expected: lines 225, 246
```

### Step 2: Extract H5 (LogBracketSubmissionError)
**Rationale**: Simplest helper, establishes pattern, preserves INV-3.5.

**Action**:
1. Insert new method after line 234 (after current `SubmitBracketOrders` closing brace):
```csharp
private void LogBracketSubmissionError(Exception ex)
{
    Print("ERROR SubmitBracketOrders: " + ex.Message);
}
```

2. Replace lines 230-233 in `SubmitBracketOrders`:
```csharp
catch (Exception ex)
{
    LogBracketSubmissionError(ex);
}
```

**Verification**:
```bash
grep -cn "ERROR SubmitBracketOrders" src/V12_002.Orders.Management.cs
# Expected: 1 match (now in LogBracketSubmissionError)
```

### Step 3: Extract H1 (ValidateBracketEntryGuard)
**Rationale**: Entry guard logic, no dict writes, safe to extract early.

**Action**:
1. Insert new method after `LogBracketSubmissionError`:
```csharp
private bool ValidateBracketEntryGuard(string entryName, PositionInfo pos, 
    out double validatedStopPrice, out bool isFollowerSubmit, 
    out OrderAction bracketExitAction, out string bracketOcoId)
{
    validatedStopPrice = 0;
    isFollowerSubmit = false;
    bracketExitAction = OrderAction.Sell;
    bracketOcoId = string.Empty;

    if (pos.BracketSubmitted) return false;

    validatedStopPrice = ValidateStopPrice(pos.Direction, pos.InitialStopPrice);
    isFollowerSubmit = pos.IsFollower && pos.ExecutingAccount != null;
    bracketExitAction = pos.Direction == MarketPosition.Long
        ? OrderAction.Sell : OrderAction.BuyToCover;
    bracketOcoId = pos.OcoGroupId ?? string.Empty;

    return true;
}
```

2. Replace lines 39-54 in `SubmitBracketOrders`:
```csharp
if (!ValidateBracketEntryGuard(entryName, pos, out double validatedStopPrice, 
    out bool isFollowerSubmit, out OrderAction bracketExitAction, out string bracketOcoId))
    return;
```

**Verification**:
```bash
python scripts/v12_split.py
# ValidateBracketEntryGuard: CYC should be ~3, LOC ~18
```

### Step 4: Extract H2 (SubmitStopOrderSafe)
**Rationale**: Critical path with INV-3.1 compliance, must preserve direct dict writes.

**Action**:
1. Insert new method after `ValidateBracketEntryGuard`:
```csharp
private Order SubmitStopOrderSafe(string entryName, PositionInfo pos, 
    bool isFollowerSubmit, OrderAction bracketExitAction, 
    double validatedStopPrice, string bracketOcoId)
{
    Order stopOrder;
    if (isFollowerSubmit)
    {
        string stopSig = SymmetryTrim("Stop_" + entryName, 40);
        Order sOrd = pos.ExecutingAccount.CreateOrder(
            Instrument, bracketExitAction, OrderType.StopMarket, TimeInForce.Gtc,
            pos.TotalContracts, 0, validatedStopPrice, bracketOcoId, stopSig, null);
        if (sOrd == null)
        {
            Print(string.Format("[BRACKET_FATAL] Follower stop CreateOrder returned null for {0}. Flattening.", entryName));
            FlattenPositionByName(entryName);
            return null;
        }
        try
        {
            stopOrders[entryName] = sOrd; // BUILD 981: Pre-register for sweep visibility
            pos.ExecutingAccount.Submit(new[] { sOrd });
        }
        catch (Exception submitEx)
        {
            Order _junk; stopOrders.TryRemove(entryName, out _junk);
            Print(string.Format("[BRACKET_FATAL] Follower stop Submit THREW for {0}: {1}. Emergency flattening.", entryName, submitEx.Message));
            EmergencyFlattenSingleFleetAccount(pos.ExecutingAccount);
            return null;
        }
        stopOrder = sOrd;
    }
    else
    {
        string stopSig = "Stop_" + entryName;
        Order sOrd = Account.CreateOrder(Instrument, bracketExitAction, OrderType.StopMarket, TimeInForce.Gtc, pos.TotalContracts, 0, validatedStopPrice, bracketOcoId, stopSig, null);
        if (sOrd != null) Account.Submit(new[] { sOrd });
        stopOrder = sOrd;
    }

    if (stopOrder == null)
    {
        Print(string.Format("[BRACKET_FATAL] Stop order submission returned null for {0}. Flattening.", entryName));
        FlattenPositionByName(entryName);
        return null;
    }
    stopOrders[entryName] = stopOrder;
    return stopOrder;
}
```

2. Replace lines 56-106 in `SubmitBracketOrders`:
```csharp
Order stopOrder = SubmitStopOrderSafe(entryName, pos, isFollowerSubmit, 
    bracketExitAction, validatedStopPrice, bracketOcoId);
if (stopOrder == null) return;
```

**Verification**:
```bash
grep -cn "BRACKET_FATAL" src/V12_002.Orders.Management.cs
# Expected: 3 matches (all in SubmitStopOrderSafe)

grep -cn "stopOrders\[" src/V12_002.Orders.Management.cs
# Verify direct writes preserved (no Enqueue wrapper)
```

### Step 5: Extract H3 (SubmitTargetOrdersLoop)
**Rationale**: Target loop with INV-3.2 compliance, must preserve direct dict writes.

**Action**:
1. Insert new method after `SubmitStopOrderSafe`:
```csharp
private void SubmitTargetOrdersLoop(string entryName, PositionInfo pos, 
    bool isFollowerSubmit, OrderAction bracketExitAction, string bracketOcoId, 
    out int nonRunnerLimitQty, out int runnerQty)
{
    nonRunnerLimitQty = 0;
    runnerQty = 0;

    for (int targetNum = 1; targetNum <= 5; targetNum++)
    {
        int targetQty = GetTargetContracts(pos, targetNum);
        if (targetQty <= 0) continue;

        if (IsRunnerTarget(targetNum))
        {
            runnerQty += targetQty;
            Print(string.Format("[FORENSIC] T{0} {1}: Runner qty={2} -- limit SKIPPED",
                targetNum, entryName, targetQty));
            continue;
        }

        double targetPrice = GetTargetPrice(pos, targetNum);
        if (targetPrice <= 0)
        {
            Print(string.Format("[TARGET_SKIP] T{0} for {1} has qty={2} but invalid price={3:F2}; skipped",
                targetNum, entryName, targetQty, targetPrice));
            continue;
        }

        targetPrice = Instrument.MasterInstrument.RoundToTickSize(targetPrice);

        Print(string.Format("[FORENSIC] T{0} {1}: qty={2} price={3:F2} submitting limit",
            targetNum, entryName, targetQty, targetPrice));

        Order limitOrder;
        if (isFollowerSubmit)
        {
            string targetSig = SymmetryTrim("T" + targetNum + "_" + entryName, 40);
            Order tOrd = pos.ExecutingAccount.CreateOrder(
                Instrument, bracketExitAction, OrderType.Limit, TimeInForce.Gtc,
                targetQty, targetPrice, 0, bracketOcoId, targetSig, null);
            if (tOrd != null)
                pos.ExecutingAccount.Submit(new[] { tOrd });
            else
                Print(string.Format("[TARGET_WARN] Follower target T{0} CreateOrder returned null for {1}.", targetNum, entryName));
            limitOrder = tOrd;
        }
        else
        {
            string targetSig = "T" + targetNum + "_" + entryName;
            Order tOrd = Account.CreateOrder(Instrument, bracketExitAction, OrderType.Limit, TimeInForce.Gtc, targetQty, targetPrice, 0, bracketOcoId, targetSig, null);
            if (tOrd != null) Account.Submit(new[] { tOrd });
            limitOrder = tOrd;
        }

        var targetDict = GetTargetOrdersDictionary(targetNum);
        if (targetDict != null)
        {
            if (limitOrder == null)
            {
                Print(string.Format("[TARGET_WARN] Target {0} order submission returned null for {1}. Target tracking disabled.", targetNum, entryName));
            }
            else
            {
                targetDict[entryName] = limitOrder;
            }
        }

        nonRunnerLimitQty += targetQty;
    }
}
```

2. Replace lines 108-179 in `SubmitBracketOrders`:
```csharp
SubmitTargetOrdersLoop(entryName, pos, isFollowerSubmit, bracketExitAction, 
    bracketOcoId, out int nonRunnerLimitQty, out int runnerQty);
```

**Verification**:
```bash
grep -cn "FORENSIC" src/V12_002.Orders.Management.cs
# Expected: 2 matches (both in SubmitTargetOrdersLoop)

grep -cn "TARGET_SKIP" src/V12_002.Orders.Management.cs
# Expected: 1 match (in SubmitTargetOrdersLoop)

grep -cn "TARGET_WARN" src/V12_002.Orders.Management.cs
# Expected: 2 matches (both in SubmitTargetOrdersLoop)

grep -cn "targetDict\[" src/V12_002.Orders.Management.cs
# Verify direct writes preserved (no Enqueue wrapper)
```

### Step 6: Extract H4 (AuditStopQuantityAndPrint)
**Rationale**: Final audit + print logic, no dict writes, safe to extract last.

**Action**:
1. Insert new method after `SubmitTargetOrdersLoop`:
```csharp
private void AuditStopQuantityAndPrint(string entryName, PositionInfo pos, 
    Order stopOrder, double validatedStopPrice, int nonRunnerLimitQty, 
    int runnerQty, bool isFollowerSubmit)
{
    pos.CurrentStopPrice = validatedStopPrice;

    if (stopOrder != null && stopOrder.Quantity != pos.TotalContracts)
    {
        Print(string.Format("[STOP_AUDIT] MISMATCH {0}: StopQty={1} Total={2}",
            entryName, stopOrder.Quantity, pos.TotalContracts));
    }
    else
    {
        Print(string.Format("[STOP_AUDIT] OK {0}: StopQty={1} NonRunnerLimits={2} RunnerQty={3}",
            entryName, pos.TotalContracts, nonRunnerLimitQty, runnerQty));
    }

    if (isFollowerSubmit)
        Print(string.Format("[938-BRACKET] Follower bracket submitted: {0} T1={1:F2} Stop={2:F2}",
            entryName, pos.Target1Price, validatedStopPrice));

    StringBuilder bracketMsg = new StringBuilder();
    string tradeType = pos.IsRMATrade ? "RMA" : "OR";
    bracketMsg.AppendFormat("{0} BRACKET V12.1101E: Stop@{1:F2}", tradeType, validatedStopPrice);
    for (int targetNum = 1; targetNum <= 5; targetNum++)
    {
        int targetQty = GetTargetContracts(pos, targetNum);
        if (targetQty <= 0) continue;

        bool isRunnerSlot = IsRunnerTarget(targetNum);

        if (isRunnerSlot)
            bracketMsg.AppendFormat(" | T{0}:{1}@trail", targetNum, targetQty);
        else
            bracketMsg.AppendFormat(" | T{0}:{1}@{2:F2}", targetNum, targetQty, GetTargetPrice(pos, targetNum));
    }

    Print(bracketMsg.ToString());

    int _targetSum = nonRunnerLimitQty + runnerQty;
    if (_targetSum != pos.TotalContracts)
    {
        Print(string.Format("[BRACKET_WARN] Target sum mismatch for {0}: targets={1} totalContracts={2}. Distribution may have lost contracts.",
            entryName, _targetSum, pos.TotalContracts));
    }
}
```

2. Replace lines 181-228 in `SubmitBracketOrders`:
```csharp
AuditStopQuantityAndPrint(entryName, pos, stopOrder, validatedStopPrice, 
    nonRunnerLimitQty, runnerQty, isFollowerSubmit);
```

**Verification**:
```bash
grep -cn "STOP_AUDIT" src/V12_002.Orders.Management.cs
# Expected: 2 matches (both in AuditStopQuantityAndPrint)

grep -cn "938-BRACKET" src/V12_002.Orders.Management.cs
# Expected: 1 match (in AuditStopQuantityAndPrint)

grep -cn "BRACKET_WARN" src/V12_002.Orders.Management.cs
# Expected: 1 match (in AuditStopQuantityAndPrint)
```

### Step 7: Final Residual Verification
**Action**:
```bash
python scripts/v12_split.py
# SubmitBracketOrders: CYC should be ≤19, LOC ~25
# ValidateBracketEntryGuard: CYC ~3, LOC ~18
# SubmitStopOrderSafe: CYC ~8, LOC ~52
# SubmitTargetOrdersLoop: CYC ~9, LOC ~73
# AuditStopQuantityAndPrint: CYC ~4, LOC ~49
# LogBracketSubmissionError: CYC ~1, LOC ~5

# Verify all sub-helpers meet LOC ≥15 threshold
```

### Step 8: Caller Site Verification
**Action**:
```bash
grep -n "SubmitBracketOrders" src/V12_002.Orders.Callbacks.cs
# Expected: lines 225, 246 (unchanged)

# Verify signature stability
grep -A2 "private void SubmitBracketOrders" src/V12_002.Orders.Management.cs
# Expected: (string entryName, PositionInfo pos)
```

### Step 9: Print Baseline Verification
**Action**:
```bash
# Compare against baseline captured in Step 1
grep -cn "ERROR SubmitBracketOrders" src/V12_002.Orders.Management.cs
grep -cn "BRACKET_FATAL" src/V12_002.Orders.Management.cs
grep -cn "TARGET_SKIP" src/V12_002.Orders.Management.cs
grep -cn "TARGET_WARN" src/V12_002.Orders.Management.cs
grep -cn "FORENSIC" src/V12_002.Orders.Management.cs
grep -cn "STOP_AUDIT" src/V12_002.Orders.Management.cs
grep -cn "938-BRACKET" src/V12_002.Orders.Management.cs
grep -cn "BRACKET_WARN" src/V12_002.Orders.Management.cs

# All counts MUST match baseline
```

### Step 10: Build & Deploy
**Action**:
```bash
powershell -File .\scripts\build_readiness.ps1
# Expected: Clean build, no errors

powershell -File .\deploy-sync.ps1
# Expected: Hard-link sync successful
```

---

## 4. ACCEPTANCE CRITERIA CHECKLIST

- [ ] **AC1**: Residual `SubmitBracketOrders` measures CYC ≤19
- [ ] **AC2**: All sub-helpers measure CYC ≤19 and LOC ≥15:
  - [ ] `ValidateBracketEntryGuard`: CYC ~3, LOC ~18
  - [ ] `SubmitStopOrderSafe`: CYC ~8, LOC ~52
  - [ ] `SubmitTargetOrdersLoop`: CYC ~9, LOC ~73
  - [ ] `AuditStopQuantityAndPrint`: CYC ~4, LOC ~49
  - [ ] `LogBracketSubmissionError`: CYC ~1, LOC ~5
- [ ] **AC3**: `SubmitBracketOrders` no longer appears in `CYC > 20 remaining` list
- [ ] **AC4**: Code review confirms ZERO `Enqueue(...)` wrapping around `stopOrders[*] =` or `targetOrders[*] =` writes (INV-3.1/3.2)
- [ ] **AC5**: Code review confirms bracket-submission ordering preserved (INV-3.3)
- [ ] **AC6**: Both caller sites at `src/V12_002.Orders.Callbacks.cs` lines 225, 246 unchanged
- [ ] **AC7**: `grep -cn "ERROR SubmitBracketOrders" src/V12_002.Orders.Management.cs` == 1
- [ ] **AC8**: All verbatim print baselines match (BRACKET_FATAL=3, TARGET_SKIP=1, TARGET_WARN=2, FORENSIC=2, STOP_AUDIT=2, 938-BRACKET=1, BRACKET_WARN=1)
- [ ] **AC9**: BUILD_TAG bumped to `1111.007-phase7-t4`
- [ ] **AC10**: This markdown saved at `docs/brain/phase7_sprint5_t04_SubmitBracketOrders.md`

---

## 5. F5 ACCEPTANCE TEST

**Scenario**: Trigger any RMA entry; verify stop and 5 target orders appear on chart for master account; check Output for zero ERROR SubmitBracketOrders lines.

**Steps**:
1. Load V12_002 in NinjaTrader with BUILD_TAG `1111.007-phase7-t4`
2. Enable RMA mode
3. Wait for RMA entry signal
4. Verify on chart:
   - 1 stop order at calculated stop price
   - Up to 5 target orders (T1-T5) at calculated target prices
   - Runner targets show as trailing stops (not limit orders)
5. Check Output window:
   - `[STOP_AUDIT] OK` message present
   - `RMA BRACKET V12.1101E: Stop@...` message present
   - ZERO `ERROR SubmitBracketOrders` lines
   - ZERO `[BRACKET_FATAL]` lines

**Pass Criteria**: All orders submitted successfully, no errors in Output.

---

## 6. ROLLBACK PLAN

If extraction fails or introduces regressions:

1. **Revert commit**: `git revert HEAD`
2. **Restore BUILD_TAG**: `1111.006-phase7-t3`
3. **Re-sync**: `powershell -File .\deploy-sync.ps1`
4. **Verify**: F5 in NinjaTrader, confirm original behavior

---

## 7. NOTES

### 7.1 Why SOFT-LOCK Signature?
Per Approach §1.4 D-D3, Director Option A confirmed: default position is preserve signature unless safety-impact justification exists. Both callers pass `(kvp.Key, pos)` - no safety reason to change.

### 7.2 Why No `pos.BracketSubmitted = true`?
Comment at line 197 indicates this was intentionally removed in a prior fix (Task 5). Do NOT re-add during extraction - this is a KNOWN ISSUE tracked separately.

### 7.3 Why 5 Sub-Helpers?
- H1: Entry guard (CYC=3) - too simple to split further
- H2: Stop submission (CYC=8) - master/follower branching + error handling
- H3: Target loop (CYC=9) - 5-iteration loop with runner detection
- H4: Audit + print (CYC=4) - final verification logic
- H5: Error log (CYC=1) - preserves INV-3.5 verbatim print

Total residual CYC = 5 (guard call + 4 helper calls + catch) - well under 19 threshold.

### 7.4 Co-Residency Risk Mitigation
This extraction touches ONLY `SubmitBracketOrders` (lines 37-234). All other functions in `V12_002.Orders.Management.cs` remain untouched. Sprint 6 will handle the remaining god-functions.

---

**END OF EXTRACTION PLAN**