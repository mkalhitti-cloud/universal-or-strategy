# Phase 7 Sprint 5 T06: ExecuteRMAEntryV2 CYC Reduction

**Ticket**: [Phase7-S5-T06] ExecuteRMAEntryV2 (CYC=22 -> <20)

**Objective**: Extract `ExecuteRMAEntryV2` (CYC=22, LOC=315) into a thin residual dispatcher (CYC ≤19) plus 4 PascalCase sub-helpers.

**Status**: IN PROGRESS

---

## Analysis

### Current Structure (lines 250-565)
- **Total LOC**: 315
- **Current CYC**: 22
- **Target CYC**: ≤19

### Code Sections
1. **Lines 252-278**: Guards (flatten, contracts, price, metadata) - CYC ~5
2. **Lines 286-314**: Setup/Calculation (ATR, targets, distribution) - CYC ~3
3. **Lines 318-368**: Local account entry submission - CYC ~4
4. **Lines 385-534**: Fleet loop (per-account submission) - CYC ~10

### Critical Constraints (INV-4.3)
**ATOMICITY REQUIREMENT**: Per-account, the entry order MUST be registered in BOTH `entryOrders` AND `activePositions` dictionaries with key `accountName + "_RMA"` **inside the same sub-helper** as the `acct.CreateOrder` call.

Current atomicity blocks:
- **Local**: Lines 320-363 (CreateOrder → entryOrders → activePositions → expectedPositions)
- **Fleet**: Lines 421-477 (CreateOrder → activePositions → entryOrders → expectedPositions)

---

## Extraction Plan

### Helper 1: `ValidateRMAEntryGuards`
**Lines**: 252-278  
**Purpose**: Consolidate all entry validation guards  
**Returns**: `bool` (true = proceed, false = abort)  
**CYC**: ~5  
**LOC**: ~27

```csharp
private bool ValidateRMAEntryGuards(double price, int contracts, MarketPosition direction)
{
    // Flatten guard (INV-4.1)
    if (isFlattenRunning) return false;
    
    // Contracts guard
    if (contracts <= 0)
    {
        Print($"[RMA] ExecuteRMAEntryV2 received invalid contracts={contracts}. Aborting entry.");
        return false;
    }
    
    // Zero-price guard
    if (price <= 0)
    {
        Print($"[RMA V2] ABORT: price={price:F2} is zero or negative...");
        return false;
    }
    
    // MetadataGuard duplicate check
    string rmaSig = $"RMA_{direction}_{contracts}_{price:F2}";
    if (!MetadataGuardDuplicate(rmaSig, "RMA_V2"))
    {
        Print("[RMA V2] (!) Duplicate dispatch rejected by MetadataGuard");
        return false;
    }
    
    return true;
}
```

### Helper 2: `CalculateRMABracketPrices`
**Lines**: 286-305  
**Purpose**: Calculate all stop/target prices and distribution  
**Returns**: Struct with prices and quantities  
**CYC**: ~2  
**LOC**: ~35

```csharp
private struct RMABracketPrices
{
    public double StopPrice;
    public double T1Price, T2Price, T3Price, T4Price, T5Price;
    public int Rt1, Rt2, Rt3, Rt4, Rt5;
}

private RMABracketPrices CalculateRMABracketPrices(double price, MarketPosition direction, int qty)
{
    double stopDist = CalculateATRStopDistance(RMAStopATRMultiplier);
    double stopPrice = (direction == MarketPosition.Long) ? price - stopDist : price + stopDist;
    stopPrice = Instrument.MasterInstrument.RoundToTickSize(stopPrice);
    
    double t1Price = CalculateTargetPrice(direction, price, 1);
    double t2Price = CalculateTargetPrice(direction, price, 2);
    double t3Price = CalculateTargetPrice(direction, price, 3);
    double t4Price = CalculateTargetPrice(direction, price, 4);
    double t5Price = CalculateTargetPrice(direction, price, 5);
    
    int rt1, rt2, rt3, rt4, rt5;
    GetTargetDistribution(qty, out rt1, out rt2, out rt3, out rt4, out rt5);
    
    return new RMABracketPrices
    {
        StopPrice = stopPrice,
        T1Price = t1Price, T2Price = t2Price, T3Price = t3Price,
        T4Price = t4Price, T5Price = t5Price,
        Rt1 = rt1, Rt2 = rt2, Rt3 = rt3, Rt4 = rt4, Rt5 = rt5
    };
}
```

### Helper 3: `SubmitLocalRMAEntry`
**Lines**: 318-368  
**Purpose**: ATOMIC submission for local account  
**Returns**: `bool` (success/failure)  
**CYC**: ~4  
**LOC**: ~50

**PRESERVES INV-4.3**: CreateOrder + entryOrders + activePositions in same method

```csharp
private bool SubmitLocalRMAEntry(
    string baseSignal, OrderAction entryAction, int qty, double price,
    MarketPosition direction, RMABracketPrices prices, string symmetryDispatchId)
{
    string localKey = baseSignal;
    Order entryOrder = SubmitOrderUnmanaged(0, entryAction, OrderType.Limit, qty, price, 0, "", localKey);
    
    if (entryOrder != null)
    {
        SymmetryGuardRegisterMasterEntry(symmetryDispatchId, localKey);
        entryOrders[localKey] = entryOrder;
        
        PositionInfo pos = new PositionInfo
        {
            SignalName = localKey,
            Direction = direction,
            TotalContracts = qty,
            T1Contracts = prices.Rt1,
            T2Contracts = prices.Rt2,
            T3Contracts = prices.Rt3,
            T4Contracts = prices.Rt4,
            T5Contracts = prices.Rt5,
            RemainingContracts = qty,
            EntryPrice = price,
            InitialStopPrice = prices.StopPrice,
            CurrentStopPrice = prices.StopPrice,
            Target1Price = prices.T1Price,
            Target2Price = prices.T2Price,
            Target3Price = prices.T3Price,
            Target4Price = prices.T4Price,
            Target5Price = prices.T5Price,
            EntryOrderType = OrderType.Limit,
            EntryFilled = false,
            BracketSubmitted = false,
            IsRMATrade = true
        };
        activePositions[localKey] = pos;
        
        int localDelta = (direction == MarketPosition.Long) ? qty : -qty;
        AddExpectedPositionDeltaLocked(ExpKey(Account.Name), localDelta);
        Print($"[SIMA] Master expectedPositions updated: {Account.Name} delta={localDelta}");
        Print($"[SIMA RMA V2] LOCAL ENTRY ONLY (Limit): {localKey} | Brackets deferred until fill");
        return true;
    }
    else
    {
        Print("[SIMA RMA V2] ERROR: Local entry returned null");
        return false;
    }
}
```

### Helper 4: `ProcessSingleFleetRMAAccount`
**Lines**: 410-533  
**Purpose**: ATOMIC submission for one fleet account  
**Returns**: `bool` (success/failure)  
**CYC**: ~8  
**LOC**: ~125

**PRESERVES INV-4.3**: CreateOrder + activePositions + entryOrders in same method

```csharp
private bool ProcessSingleFleetRMAAccount(
    Account acct, string baseSignal, OrderAction entryAction, int qty, double price,
    MarketPosition direction, RMABracketPrices prices, string symmetryDispatchId,
    StringBuilder dispatchLog)
{
    // Fleet active check
    if (!activeFleetAccounts.TryGetValue(acct.Name, out bool isActive) || !isActive)
    {
        dispatchLog.AppendLine($"  SKIP | {acct.Name,-28} | Inactive");
        return false;
    }
    
    // Consistency Lock
    if (EnableConsistencyLock)
    {
        double dailyPL = acct.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
        if (dailyPL >= MaxDailyProfitCap)
        {
            dispatchLog.AppendLine($"  SKIP | {acct.Name,-28} | ConsistencyLock ${dailyPL:F2}");
            return false;
        }
    }
    
    string fleetKey = acct.Name + "_RMA_" + baseSignal;
    string expectedKey = ExpKey(acct.Name);
    int reservedDelta = 0;
    bool syncPending = false;
    
    try
    {
        SymmetryGuardRegisterFollower(symmetryDispatchId, fleetKey);
        string ocoId = fleetKey;
        
        Order fEntry = acct.CreateOrder(Instrument, entryAction, OrderType.Limit,
            TimeInForce.Gtc, qty, price, 0, ocoId, fleetKey, null);
        
        if (fEntry == null)
        {
            dispatchLog.AppendLine($"  FAIL | {acct.Name,-28} | CreateOrder returned null");
            return false;
        }
        
        // ATOMIC: Register dicts BEFORE expectedPositions (INV-4.3)
        PositionInfo fleetFollowerPos = new PositionInfo
        {
            SignalName = fleetKey,
            Direction = direction,
            TotalContracts = qty,
            RemainingContracts = qty,
            EntryPrice = price,
            InitialStopPrice = prices.StopPrice,
            CurrentStopPrice = prices.StopPrice,
            Target1Price = prices.T1Price,
            Target2Price = prices.T2Price,
            Target3Price = prices.T3Price,
            Target4Price = prices.T4Price,
            Target5Price = prices.T5Price,
            T1Contracts = prices.Rt1,
            T2Contracts = prices.Rt2,
            T3Contracts = prices.Rt3,
            T4Contracts = prices.Rt4,
            T5Contracts = prices.Rt5,
            EntryOrderType = OrderType.Limit,
            EntryFilled = false,
            IsRMATrade = true,
            IsFollower = true,
            ExecutingAccount = acct,
            BracketSubmitted = false,
            ExtremePriceSinceEntry = price,
            CurrentTrailLevel = 0,
            OcoGroupId = "V12_" + GetStableHash(fleetKey),
        };
        activePositions[fleetKey] = fleetFollowerPos;
        entryOrders[fleetKey] = fEntry;
        
        MarkDispatchSyncPending(expectedKey);
        syncPending = true;
        
        // FSM registration
        if (!_followerBrackets.ContainsKey(fleetKey))
        {
            var rmaFsm = new FollowerBracketFSM
            {
                AccountName = acct.Name,
                EntryName = fleetKey,
                State = FollowerBracketState.Submitted,
                RemainingContracts = qty,
                EntryOrder = fEntry,
                ExpectedEntryPrice = price,
                LastUpdateUtc = DateTime.UtcNow
            };
            _followerBrackets.TryAdd(fleetKey, rmaFsm);
        }
        
        reservedDelta = (direction == MarketPosition.Long) ? qty : -qty;
        AddExpectedPositionDeltaLocked(expectedKey, reservedDelta);
        
        acct.Submit(new[] { fEntry });
        
        if (fEntry != null && !string.IsNullOrEmpty(fEntry.OrderId))
            _orderIdToFsmKey[fEntry.OrderId] = fleetKey;
        
        ClearDispatchSyncPending(expectedKey);
        syncPending = false;
        
        dispatchLog.AppendLine($"    OK | {acct.Name,-28} | Limit RMA    | submitted");
        return true;
    }
    catch (Exception ex)
    {
        if (syncPending)
        {
            ClearDispatchSyncPending(expectedKey);
            syncPending = false;
        }
        
        // Full rollback
        if (reservedDelta != 0)
            AddExpectedPositionDeltaLocked(expectedKey, -reservedDelta);
        activePositions.TryRemove(fleetKey, out _);
        entryOrders.TryRemove(fleetKey, out _);
        _followerBrackets.TryRemove(fleetKey, out _);
        dispatchLog.AppendLine($"  FAIL | {acct.Name,-28} | {ex.Message}");
        return false;
    }
}
```

### Residual `ExecuteRMAEntryV2`
**CYC**: ~6 (orchestration only)  
**LOC**: ~80

```csharp
private void ExecuteRMAEntryV2(double price, MarketPosition direction, int contracts)
{
    // Helper 1: Guards
    if (!ValidateRMAEntryGuards(price, contracts, direction))
        return;
    
    var sw = Stopwatch.StartNew();
    long t0Ticks = sw.ElapsedTicks;
    
    try
    {
        // Helper 2: Calculate prices
        RMABracketPrices prices = CalculateRMABracketPrices(price, direction, contracts);
        
        string baseSignal = "RMA_" + DateTime.Now.Ticks;
        OrderAction entryAction = (direction == MarketPosition.Long) ? OrderAction.Buy : OrderAction.SellShort;
        string symmetryDispatchId = SymmetryGuardBeginDispatch("RMA", entryAction, contracts, price);
        
        long tSetupDoneTicks = sw.ElapsedTicks;
        
        Print($"[SIMA RMA V2] {direction} @ {price} | Stop: {prices.StopPrice} | T1: {prices.T1Price} | T2: {prices.T2Price} | T3: {prices.T3Price} | T4: {prices.T4Price} | T5: {prices.T5Price} | Qty: {contracts}");
        
        // Helper 3: Local entry
        SubmitLocalRMAEntry(baseSignal, entryAction, contracts, price, direction, prices, symmetryDispatchId);
        
        // Fleet dispatch
        if (!EnableSIMA)
        {
            Print("[SIMA RMA V2] [ERR] EnableSIMA is FALSE - Fleet dispatch SKIPPED...");
            return;
        }
        
        int fleetOk = 0;
        int fleetSkip = 0;
        long tLoopStartTicks = sw.ElapsedTicks;
        var dispatchLog = new StringBuilder(512);
        
        foreach (Account acct in Account.All)
        {
            if (!IsFleetAccount(acct)) continue;
            if (acct == this.Account) continue;
            
            // Helper 4: Process fleet account
            if (ProcessSingleFleetRMAAccount(acct, baseSignal, entryAction, contracts, price,
                direction, prices, symmetryDispatchId, dispatchLog))
            {
                fleetOk++;
            }
            else
            {
                fleetSkip++;
            }
        }
        
        // Timing report (unchanged)
        sw.Stop();
        long tFinalTicks = sw.ElapsedTicks;
        double totalMs = tFinalTicks * 1000.0 / Stopwatch.Frequency;
        double setupMs = (tSetupDoneTicks - t0Ticks) * 1000.0 / Stopwatch.Frequency;
        double localMs = (tLoopStartTicks - tSetupDoneTicks) * 1000.0 / Stopwatch.Frequency;
        double loopMs = (tFinalTicks - tLoopStartTicks) * 1000.0 / Stopwatch.Frequency;
        
        var report = new StringBuilder(1024);
        report.AppendLine("+==============================================================+");
        report.AppendLine("|       FORENSIC PULSE REPORT  Phase 9 RMA ENTRY V2            |");
        report.AppendLine("+==============================================================+");
        report.AppendLine("|  TYPE | ACCOUNT                       | ORDER TYPE   | STATUS |");
        report.AppendLine("+==============================================================+");
        report.Append(dispatchLog.ToString());
        report.AppendLine("+--------------------------------------------------------------+");
        report.AppendLine($"|  FLEET: {fleetOk} dispatched, {fleetSkip} skipped");
        report.AppendLine("+--------------------------------------------------------------+");
        report.AppendLine("|  TIMING SUMMARY (4-phase)                                    |");
        report.AppendLine("+--------------------------------------------------------------+");
        report.AppendLine($"|  Setup+Calc:   {setupMs,8:F3} ms  |  Local Acct:  {localMs,8:F3} ms       |");
        report.AppendLine($"|  Fleet Loop:   {loopMs,8:F3} ms  |  Total:       {totalMs,8:F3} ms       |");
        report.AppendLine("+==============================================================+");
        Print(report.ToString().TrimEnd());
    }
    catch (Exception ex)
    {
        Print($"[SIMA RMA V2] ERROR: {ex.Message}");
    }
}
```

---

## Verification Checklist

- [ ] Residual CYC ≤19
- [ ] All 4 helpers CYC ≤19, LOC ≥15
- [ ] INV-4.3 atomicity preserved (CreateOrder + dicts in same helper)
- [ ] INV-4.1 flatten guard remains first statement
- [ ] Enqueue call sites unchanged (2 total)
- [ ] All Print/AppendLine counts match
- [ ] `python scripts/v12_split.py` passes
- [ ] BUILD_TAG = 1111.007-phase7-t6
- [ ] F5 test: RMA entry works in NinjaTrader

---

## Implementation Notes

**Key Design Decision**: The 4 helpers are designed to maintain the exact atomicity contract (INV-4.3) by keeping CreateOrder + dictionary registration + expectedPositions update in the same method scope. This prevents the REAPER race condition that was fixed in Build 923B.

**Signature Lock**: The method signature `ExecuteRMAEntryV2(double price, MarketPosition direction, int contracts)` remains unchanged to preserve Enqueue closure capture compatibility.

**Zero Behavior Change**: All logic, guards, calculations, and error handling remain identical. Only the code organization changes.