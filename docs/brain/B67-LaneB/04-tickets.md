# B67-LaneB Tickets

## Ticket 1 — DW-B67-02: Replace acc.Change() with cancel+CreateOrder+Submit in HandleEntryChange

**Block**: B67-LaneB
**DW Item**: DW-B67-02 (P0)
**Spec Req IDs**: DW-B67-02, @2Custom-PropagateMasterEntryMove-FIX-PM-02, NT8_FULL_REFERENCE.md lines 898-899
**Files**:
  - src/PropTraderTools/CopyEngine.cs  (modify HandleEntryChange)
  - src/PropTraderTools/CopyEngineTests.cs  (add 5 tests)

### Problem
HandleEntryChange (CopyEngine.cs ~lines 1048-1087) calls acc.Change() on a Working pre-fill
entry order. On Apex/Rithmic this is a silent broker-side no-op. Leader drag is lost.
Confirmed live 2026-08-12.

### Method Signatures (unchanged)
```csharp
private void HandleEntryChange(Order leaderOrder, CopyRule rule)
```

### Implementation Steps

#### Step A — Update HandleEntryChange comment block (lines 1042-1047)
Replace the entire comment block above the method signature with:
```csharp
        // B62/B66-LaneC/B67-LaneB: sync a leader entry drag to all follower working PTT-Copy entry orders.
        // B67-LaneB: DW-B67-02 -- replaced acc.Change() with Cancel+CreateOrder+Submit.
        //   acc.Change() on Apex/Rithmic is a silent broker-side no-op for pre-fill entry orders.
        //   Pattern from @2Custom PropagateMasterEntryMove (FIX-PM-02, FIX-PM-02b).
        //   NT8_FULL_REFERENCE.md lines 898-899: StopLimit price in StopPrice, not LimitPrice.
        //   limitPx = fo.OrderType == StopLimit ? 0 : newPrice
        //   stopPx  = fo.OrderType == StopLimit ? newPrice : 0
        // Triggered by Gate C when leader's entry orderId is already in dedup cache but price changed.
        // CYC=7: instr null(1) + tickSize ternary(2) + foreach acc(3) + acc null(4)
        //   + fo null(5) + price delta guard(6) + order null guard in CreateOrder(7).
        // JS-001: no throw in hot path. JS-021: no lock. JS-002: void.
```

#### Step B — Update _dedupCache line (~line 1061)
Replace:
```csharp
            _dedupCache[leaderOrder.OrderId.ToString()] = newPrice;
```
With:
```csharp
            // B67-LaneB DW-B67-02: remove stale key after cancel+resubmit.
            // New entry will be re-keyed by DispatchCopy on the follower's Accepted event.
            // Do NOT insert newPrice under the old key after cancel+resubmit.
            _dedupCache.TryRemove(leaderOrder.OrderId.ToString(), out _);
```

#### Step C — Replace the try block inside the foreach (~lines 1076-1085)
Replace:
```csharp
                try
                {
                    SetFollowerPrice(fo, newPrice); // B66-LaneC: StopLimit -> fo.StopPrice (NT8_FULL_REFERENCE.md lines 898-899)
                    acc.Change(new Order[] { fo });
                    StatusUpdate?.Invoke(acc.Name + ": entry dragged -> " + newPrice);
                }
                catch (Exception ex)
                {
                    StatusUpdate?.Invoke(acc.Name + ": entry drag error: " + ex.Message);
                }
```
With:
```csharp
                // B67-LaneB DW-B67-02: Cancel+CreateOrder+Submit (acc.Change() is Apex/Rithmic no-op).
                // NT8_FULL_REFERENCE.md lines 898-899: StopLimit price in StopPrice not LimitPrice.
                double limitPx = fo.OrderType == OrderType.StopLimit ? 0.0 : newPrice; // (7a)
                double stopPx  = fo.OrderType == OrderType.StopLimit ? newPrice : 0.0; // (7b)
                acc.Cancel(new Order[] { fo });
                var order = acc.CreateOrder(
                    instrument,
                    fo.OrderAction,
                    fo.OrderType,
                    OrderEntry.Manual,
                    fo.TimeInForce,
                    fo.Quantity,
                    limitPx,
                    stopPx,
                    null,
                    fo.Name,
                    DateTime.MaxValue,
                    null);
                if (order != null)                                                       // (7)
                    acc.Submit(new[] { order });
                StatusUpdate?.Invoke(acc.Name + ": entry dragged -> " + newPrice);
```

Note: (7a) and (7b) are NOT separate CYC branches — they are pre-computations for a single conditional expression. Only the `if (order != null)` on line marked (7) is the CYC=7 branch.

#### Step D — Verify acc.Change() is GONE from HandleEntryChange
Run: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "acc\.Change" | Where-Object { $_.LineNumber -ge 1048 -and $_.LineNumber -le 1090 }
Expected result: no matches in that line range.
Remaining acc.Change() calls (in SyncFollowerBracket, MoveStopToBreakEven, TightenOneStop) are UNTOUCHED.

### Tests to Add (CopyEngineTests.cs — after T_B66_07 at line 3342, before closing braces at lines 3349-3350)

```csharp
        // ─── B67-LaneB: DW-B67-02 HandleEntryChange cancel+CreateOrder+Submit ────────────

        [Fact]
        public void T_B67_B_01_HandleEntryChange_calls_Cancel_not_Change()
        {
            // Arrange: follower account with 1 Working Limit entry order at price 100.
            // Verify acc.Cancel is called and acc.Change is NOT called.
            // Uses reflection to invoke private HandleEntryChange.
            var (engine, rule, mockAcc) = MakeEngineWithFollowerOrder(
                OrderType.Limit, OrderState.Working, entryPrice: 100.0, leaderNewPrice: 105.0);
            var method = typeof(CopyEngine).GetMethod("HandleEntryChange",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var leaderOrder = MakeLeaderOrder(OrderType.Limit, 105.0, rule.LeaderAccount);
            method!.Invoke(engine, new object[] { leaderOrder, rule });
            Assert.True(mockAcc.CancelCalled, "Cancel must be called for Working Limit entry order");
            Assert.False(mockAcc.ChangeCalled, "Change must NOT be called (Apex/Rithmic no-op)");
        }

        [Fact]
        public void T_B67_B_02_HandleEntryChange_calls_CreateOrder_with_newPrice()
        {
            // Arrange: Limit entry at 100. Leader drags to 105.
            // Verify CreateOrder was called with limitPx=105 (Limit type).
            var (engine, rule, mockAcc) = MakeEngineWithFollowerOrder(
                OrderType.Limit, OrderState.Working, entryPrice: 100.0, leaderNewPrice: 105.0);
            var method = typeof(CopyEngine).GetMethod("HandleEntryChange",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var leaderOrder = MakeLeaderOrder(OrderType.Limit, 105.0, rule.LeaderAccount);
            method!.Invoke(engine, new object[] { leaderOrder, rule });
            Assert.True(mockAcc.CreateOrderCalled, "CreateOrder must be called");
            Assert.Equal(105.0, mockAcc.LastCreateOrderLimitPx, precision: 5);
            Assert.Equal(0.0,   mockAcc.LastCreateOrderStopPx,  precision: 5);
        }

        [Fact]
        public void T_B67_B_03_HandleEntryChange_StopLimit_uses_StopPrice()
        {
            // Arrange: follower StopLimit entry at StopPrice=100. Leader drags to 98.
            // Verify CreateOrder called with stopPx=98, limitPx=0.
            // NT8_FULL_REFERENCE.md lines 898-899.
            var (engine, rule, mockAcc) = MakeEngineWithFollowerOrder(
                OrderType.StopLimit, OrderState.Working, entryPrice: 100.0, leaderNewPrice: 98.0);
            var method = typeof(CopyEngine).GetMethod("HandleEntryChange",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var leaderOrder = MakeLeaderOrder(OrderType.StopLimit, 98.0, rule.LeaderAccount);
            method!.Invoke(engine, new object[] { leaderOrder, rule });
            Assert.True(mockAcc.CreateOrderCalled, "CreateOrder must be called for StopLimit");
            Assert.Equal(98.0, mockAcc.LastCreateOrderStopPx,  precision: 5);
            Assert.Equal(0.0,  mockAcc.LastCreateOrderLimitPx, precision: 5);
        }

        [Fact]
        public void T_B67_B_04_HandleEntryChange_price_within_tick_noOp()
        {
            // Arrange: follower entry at 100. Leader newPrice = 100 + (tickSize * 0.5).
            // tickSize = 0.25 (ES). Delta = 0.125 < 0.25. Guard fires. No cancel, no CreateOrder.
            var (engine, rule, mockAcc) = MakeEngineWithFollowerOrder(
                OrderType.Limit, OrderState.Working, entryPrice: 100.0, leaderNewPrice: 100.125);
            var method = typeof(CopyEngine).GetMethod("HandleEntryChange",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var leaderOrder = MakeLeaderOrder(OrderType.Limit, 100.125, rule.LeaderAccount);
            method!.Invoke(engine, new object[] { leaderOrder, rule });
            Assert.False(mockAcc.CancelCalled,      "Cancel must NOT be called within tick delta");
            Assert.False(mockAcc.CreateOrderCalled, "CreateOrder must NOT be called within tick delta");
        }

        [Fact]
        public void T_B67_B_05_HandleEntryChange_null_follower_order_skip()
        {
            // Arrange: follower account has no Working Limit/StopLimit PTT-Copy order.
            // Verify no cancel, no CreateOrder.
            var (engine, rule, mockAcc) = MakeEngineWithNoFollowerOrder(leaderNewPrice: 105.0);
            var method = typeof(CopyEngine).GetMethod("HandleEntryChange",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var leaderOrder = MakeLeaderOrder(OrderType.Limit, 105.0, rule.LeaderAccount);
            method!.Invoke(engine, new object[] { leaderOrder, rule });
            Assert.False(mockAcc.CancelCalled,      "Cancel must NOT be called when no follower order found");
            Assert.False(mockAcc.CreateOrderCalled, "CreateOrder must NOT be called when no follower order found");
        }
```

### 7-Scan Checklist (MANDATORY — engineer must confirm all 7 before BUILD_PASS)

| Scan | Command | Expected Result |
|------|---------|-----------------|
| S1 lock( | Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\(" | 0 results in new/changed lines |
| S2 throw new | Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw new" | 0 results in new/changed lines |
| S3 acc.Change in HandleEntryChange | Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "acc\.Change" \| Where-Object { $_.LineNumber -ge 1048 -and $_.LineNumber -le 1100 } | 0 results |
| S4 CYC | python scripts/complexity_audit.py | HandleEntryChange CYC = 7 |
| S5 non-ASCII | [byte scan on modified lines] | 0 non-ASCII chars in new/changed code |
| S6 build | dotnet build src/PropTraderTools | 0 errors |
| S7 tests | dotnet test src/PropTraderTools | All T_B67_B_01..05 pass, 0 failures |

### NT8 API Constraints
- acc.CreateOrder() signature: (Instrument, OrderAction, OrderType, OrderEntry, TimeInForce, int qty, double limitPrice, double stopPrice, string oco, string name, DateTime gtd, string templateName)
- NT8_FULL_REFERENCE.md lines 898-899: StopLimit price in StopPrice, not LimitPrice
- acc.Cancel() and acc.Submit() are Account methods (not Strategy methods)

### Deploy Step (MANDATORY before BUILD_PASS)
```powershell
Copy-Item "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" `
  "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs" -Force
$hash1 = (Get-FileHash "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs").Hash
$hash2 = (Get-FileHash "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs").Hash
Write-Host "Source:      $hash1"
Write-Host "Destination: $hash2"
if ($hash1 -eq $hash2) { Write-Host "SHA-256 MATCH: PASS" } else { Write-Host "SHA-256 MISMATCH: FAIL" }
```
Report both hashes and PASS/FAIL in ticket-1-completion.md.
