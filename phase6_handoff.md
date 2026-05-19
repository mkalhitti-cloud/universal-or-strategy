# ENGINEER MISSION: Phase 6 -- FSM Promotion & MetadataGuard Integration

# Authority: P5 Director via P3 Architect

# Target: V12_002 NinjaTrader 8 Strategy (.NET 4.8 / C# 8.0)

# Scope: 11 surgical FIND/REPLACE edits across 5 files. Zero new files

## CRITICAL DNA RULES (VIOLATION = BUILD REJECTION)

- ZERO lock(stateLock) anywhere in new code
- ASCII-ONLY in all string literals (no emoji, curly quotes, em-dashes, Unicode arrows)
- Phantom-Fix ordering: Dict -> SyncPending -> FSM -> ExpectedPositions -> Enqueue
- ConcurrentDictionary for all shared state (Actor model)
- No raw Cancel+Submit (not applicable here but verify no regression)

## EXECUTION ORDER: Apply edits E1 through E11 sequentially

---

### E1 -- src/V12_002.SIMA.cs

### FleetDispatchRequest: Add SignalTicks field

### Location: struct FleetDispatchRequest (lines 62-69)

FIND:

```csharp
        private struct FleetDispatchRequest
        {
            public Account Account;
            public Order[] Orders;
            public string FleetEntryName;
            public string ExpectedKey;
            public int ReservedDelta;
        }
```

REPLACE:

```csharp
        private struct FleetDispatchRequest
        {
            public Account Account;
            public Order[] Orders;
            public string FleetEntryName;
            public string ExpectedKey;
            public int ReservedDelta;
            public long SignalTicks; // Phase 6 [MG-T1]: UTC ticks at enqueue for stale dispatch detection
        }
```

---

### E2 -- src/V12_002.Symmetry.BracketFSM.cs

### GetFsmExpectedPosition: Include PendingSubmit in state filter

### Location: GetFsmExpectedPosition method (lines 280-284)

FIND:

```csharp
                if (f.State == FollowerBracketState.Active
                    || f.State == FollowerBracketState.Accepted
                    || f.State == FollowerBracketState.Submitted
                    || f.State == FollowerBracketState.Replacing
                    || f.State == FollowerBracketState.Modifying)
```

REPLACE:

```csharp
                if (f.State == FollowerBracketState.Active
                    || f.State == FollowerBracketState.Accepted
                    || f.State == FollowerBracketState.Submitted
                    || f.State == FollowerBracketState.PendingSubmit
                    || f.State == FollowerBracketState.Replacing
                    || f.State == FollowerBracketState.Modifying)
```

---

### E3 -- src/V12_002.SIMA.Dispatch.cs

### ExecuteSmartDispatchEntry: MetadataGuard duplicate check at top

### Location: After flatten guard (line 74), before GetSortedAccountFleet (line 76)

FIND:

```csharp
                }

                List<AccountRankInfo> fleet = GetSortedAccountFleet();
```

REPLACE:

```csharp
                }

                // Phase 6 [MG-D1]: MetadataGuard -- reject duplicate dispatch signals.
                // Composite fingerprint prevents the same trade from dispatching twice within 10s.
                string dispatchSig = string.Format("SD_{0}_{1}_{2}_{3:F2}", tradeType, action, quantity, entryPrice);
                if (!MetadataGuardDuplicate(dispatchSig, "SmartDispatch"))
                {
                    Print("[DISPATCH] (!) Duplicate dispatch rejected by MetadataGuard");
                    return;
                }

                List<AccountRankInfo> fleet = GetSortedAccountFleet();
```

CONTEXT: The FIND block is the closing brace of the isFlattenRunning guard (line 74)
followed by GetSortedAccountFleet. Match the FIRST occurrence of this pattern in
ExecuteSmartDispatchEntry.

---

### E4 -- src/V12_002.SIMA.Dispatch.cs

### ExecuteSmartDispatchEntry (Market path): Proactive FSM creation before enqueue

### Location: Between syncPending=true (line 318) and reservedDelta (line 320)

FIND:

```csharp
                            syncPending = true;

                            // Build 935: Reserve follower-sized expected quantity only.
                            reservedDelta = (action == OrderAction.Buy) ? followerQty : -followerQty;
                            AddExpectedPositionDeltaLocked(expectedKey, reservedDelta);
```

REPLACE:

```csharp
                            syncPending = true;

                            // Phase 6 [FSM-P1]: Proactive FSM -- eliminates Gap of Unknowing
                            // between enqueue and PumpFleetDispatch. State = PendingSubmit until
                            // pump promotes to Submitted after successful acct.Submit().
                            if (!_followerBrackets.ContainsKey(fleetEntryName))
                            {
                                var proFsm = new FollowerBracketFSM
                                {
                                    AccountName = acct.Name,
                                    EntryName = fleetEntryName,
                                    State = FollowerBracketState.PendingSubmit,
                                    RemainingContracts = followerQty,
                                    EntryOrder = entry,
                                    ExpectedEntryPrice = entry.LimitPrice > 0 ? entry.LimitPrice : 0,
                                    StopOrder = stop,
                                    ExpectedStopPrice = stop != null ? stop.StopPrice : 0,
                                    OcoGroupId = ocoId,
                                    LastUpdateUtc = DateTime.UtcNow
                                };
                                foreach (var st in stagedTargets)
                                {
                                    if (st.Num >= 1 && st.Num <= 5)
                                    {
                                        proFsm.Targets[st.Num - 1] = st.Order;
                                        proFsm.ExpectedTargetPrices[st.Num - 1] = st.Price;
                                    }
                                }
                                _followerBrackets.TryAdd(fleetEntryName, proFsm);
                            }

                            // Build 935: Reserve follower-sized expected quantity only.
                            reservedDelta = (action == OrderAction.Buy) ? followerQty : -followerQty;
                            AddExpectedPositionDeltaLocked(expectedKey, reservedDelta);
```

---

### E4b -- src/V12_002.SIMA.Dispatch.cs

### ExecuteSmartDispatchEntry (Market path): Add SignalTicks to enqueue

### Location: Market path enqueue (lines 328-335)

FIND:

```csharp
                            Interlocked.Increment(ref _pendingFleetDispatchCount);
                            _pendingFleetDispatches.Enqueue(new FleetDispatchRequest
                            {
                                Account    = acct,
                                Orders     = ordersToSubmit.ToArray(),
                                FleetEntryName = fleetEntryName,
                                ExpectedKey    = expectedKey,
                                ReservedDelta  = reservedDelta
                            });
```

REPLACE:

```csharp
                            Interlocked.Increment(ref _pendingFleetDispatchCount);
                            _pendingFleetDispatches.Enqueue(new FleetDispatchRequest
                            {
                                Account    = acct,
                                Orders     = ordersToSubmit.ToArray(),
                                FleetEntryName = fleetEntryName,
                                ExpectedKey    = expectedKey,
                                ReservedDelta  = reservedDelta,
                                SignalTicks    = DateTime.UtcNow.Ticks
                            });
```

---

### E5 -- src/V12_002.SIMA.Dispatch.cs

### ExecuteSmartDispatchEntry (Limit path): Proactive FSM + SignalTicks

### Location: Limit path, between syncPending (line 358) and reservedDelta (line 360)

FIND:

```csharp
                            syncPending = true;

                            reservedDelta = (action == OrderAction.Buy) ? followerQty : -followerQty;
                            AddExpectedPositionDeltaLocked(expectedKey, reservedDelta);

                            // [Build 936 FIX-1]: Enqueue for async TriggerCustomEvent pump instead of blocking Submit.
                            Interlocked.Increment(ref _pendingFleetDispatchCount);
                            _pendingFleetDispatches.Enqueue(new FleetDispatchRequest
                            {
                                Account    = acct,
                                Orders     = new[] { entry },
                                FleetEntryName = fleetEntryName,
                                ExpectedKey    = expectedKey,
                                ReservedDelta  = reservedDelta
                            });
```

REPLACE:

```csharp
                            syncPending = true;

                            // Phase 6 [FSM-P1]: Proactive FSM for limit entry (entry-only, no brackets).
                            if (!_followerBrackets.ContainsKey(fleetEntryName))
                            {
                                var proFsm = new FollowerBracketFSM
                                {
                                    AccountName = acct.Name,
                                    EntryName = fleetEntryName,
                                    State = FollowerBracketState.PendingSubmit,
                                    RemainingContracts = followerQty,
                                    EntryOrder = entry,
                                    ExpectedEntryPrice = entry.LimitPrice > 0 ? entry.LimitPrice : 0,
                                    LastUpdateUtc = DateTime.UtcNow
                                };
                                _followerBrackets.TryAdd(fleetEntryName, proFsm);
                            }

                            reservedDelta = (action == OrderAction.Buy) ? followerQty : -followerQty;
                            AddExpectedPositionDeltaLocked(expectedKey, reservedDelta);

                            // [Build 936 FIX-1]: Enqueue for async TriggerCustomEvent pump instead of blocking Submit.
                            Interlocked.Increment(ref _pendingFleetDispatchCount);
                            _pendingFleetDispatches.Enqueue(new FleetDispatchRequest
                            {
                                Account    = acct,
                                Orders     = new[] { entry },
                                FleetEntryName = fleetEntryName,
                                ExpectedKey    = expectedKey,
                                ReservedDelta  = reservedDelta,
                                SignalTicks    = DateTime.UtcNow.Ticks
                            });
```

---

### E6 -- src/V12_002.SIMA.Dispatch.cs

### ExecuteSmartDispatchEntry catch: Add FSM cleanup

### Location: Per-account catch block (lines 394-406)

FIND:

```csharp
                        if (registeredForCleanup)
                        {
                            // V12.Phase8 [F-01]: Full tracking-dict cleanup on Submit failure.
                            activePositions.TryRemove(fleetEntryName, out _);
                            entryOrders.TryRemove(fleetEntryName, out _);
                            stopOrders.TryRemove(fleetEntryName, out _);
                            for (int tNum = 1; tNum <= 5; tNum++)
                            {
                                var targetDict = GetTargetOrdersDictionary(tNum);
                                if (targetDict != null)
                                    targetDict.TryRemove(fleetEntryName, out _);
                            }
                        }
```

REPLACE:

```csharp
                        if (registeredForCleanup)
                        {
                            // V12.Phase8 [F-01]: Full tracking-dict cleanup on Submit failure.
                            activePositions.TryRemove(fleetEntryName, out _);
                            entryOrders.TryRemove(fleetEntryName, out _);
                            stopOrders.TryRemove(fleetEntryName, out _);
                            for (int tNum = 1; tNum <= 5; tNum++)
                            {
                                var targetDict = GetTargetOrdersDictionary(tNum);
                                if (targetDict != null)
                                    targetDict.TryRemove(fleetEntryName, out _);
                            }
                        }
                        // Phase 6: Clean up proactive FSM on dispatch failure (no-op if not yet created)
                        _followerBrackets.TryRemove(fleetEntryName, out _);
```

---

### E7 -- src/V12_002.SIMA.Fleet.cs

### PumpFleetDispatch: Timestamp guard + FSM state promotion (TWO insertions)

#### E7a: Stale dispatch rejection (after try-open, before FSM creation)

FIND:

```csharp
            bool syncCleared = false;
            try
            {
                // Phase 2 [D1]: Initialize FollowerBracketFSM for Shadow Mode
                if (!_followerBrackets.ContainsKey(req.FleetEntryName))
```

REPLACE:

```csharp
            bool syncCleared = false;
            try
            {
                // Phase 6 [MG-T1]: Reject stale queued dispatch (enqueued > 5s ago)
                if (req.SignalTicks > 0 && !MetadataGuardTimestamp(req.SignalTicks, "Pump:" + req.FleetEntryName))
                {
                    ClearDispatchSyncPending(req.ExpectedKey);
                    syncCleared = true;
                    if (req.ReservedDelta != 0)
                        AddExpectedPositionDeltaLocked(req.ExpectedKey, -req.ReservedDelta);
                    activePositions.TryRemove(req.FleetEntryName, out _);
                    entryOrders.TryRemove(req.FleetEntryName, out _);
                    stopOrders.TryRemove(req.FleetEntryName, out _);
                    for (int tNum = 1; tNum <= 5; tNum++)
                    {
                        var td = GetTargetOrdersDictionary(tNum);
                        if (td != null) td.TryRemove(req.FleetEntryName, out _);
                    }
                    _followerBrackets.TryRemove(req.FleetEntryName, out _);
                    Print(string.Format("[PUMP] STALE dispatch rejected for {0} -- rolled back", req.FleetEntryName));
                    return;
                }

                // Phase 2 [D1]: Initialize FollowerBracketFSM for Shadow Mode
                if (!_followerBrackets.ContainsKey(req.FleetEntryName))
```

#### E7b: FSM state promotion (after syncCleared=true, before OrderId registration)

FIND:

```csharp
                syncCleared = true;

                // Phase 3 [Step 3]: Register all order IDs for O(1) FSM lookup
```

REPLACE:

```csharp
                syncCleared = true;

                // Phase 6 [FSM-P2]: Promote pre-created FSM from PendingSubmit to Submitted
                FollowerBracketFSM pFsm;
                if (_followerBrackets.TryGetValue(req.FleetEntryName, out pFsm)
                    && pFsm != null
                    && pFsm.State == FollowerBracketState.PendingSubmit)
                {
                    pFsm.State = FollowerBracketState.Submitted;
                    pFsm.LastUpdateUtc = DateTime.UtcNow;
                }

                // Phase 3 [Step 3]: Register all order IDs for O(1) FSM lookup
```

---

### E8 -- src/V12_002.SIMA.Fleet.cs

### PumpFleetDispatch catch: Add FSM cleanup

### Location: Catch block, after target dict loop (lines 146-152)

FIND:

```csharp
                for (int tNum = 1; tNum <= 5; tNum++)
                {
                    var targetDict = GetTargetOrdersDictionary(tNum);
                    if (targetDict != null)
                        targetDict.TryRemove(req.FleetEntryName, out _);
                }
            }
```

REPLACE:

```csharp
                for (int tNum = 1; tNum <= 5; tNum++)
                {
                    var targetDict = GetTargetOrdersDictionary(tNum);
                    if (targetDict != null)
                        targetDict.TryRemove(req.FleetEntryName, out _);
                }
                // Phase 6: Clean up proactive FSM on Submit failure
                _followerBrackets.TryRemove(req.FleetEntryName, out _);
            }
```

CONTEXT: This is inside PumpFleetDispatch catch block. The closing brace is
the catch block closing brace, NOT the for-loop closing brace.

---

### E9 -- src/V12_002.SIMA.Execution.cs

### ExecuteRMAEntryV2: MetadataGuard duplicate check at top

### Location: After price validation (line 206), before try block (line 208)

FIND:

```csharp
            }

            try
```

REPLACE:

```csharp
            }

            // Phase 6 [MG-D2]: MetadataGuard -- reject duplicate RMA dispatch signals.
            string rmaSig = string.Format("RMA_{0}_{1}_{2:F2}", direction, contracts, price);
            if (!MetadataGuardDuplicate(rmaSig, "RMA_V2"))
            {
                Print("[RMA V2] (!) Duplicate dispatch rejected by MetadataGuard");
                return;
            }

            try
```

CONTEXT: The closing brace before "try" is the price<=0 validation guard.
This is inside ExecuteRMAEntryV2, NOT ExecuteMultiAccountMarket/Bracket.

---

### E10 -- src/V12_002.SIMA.Execution.cs

### ExecuteRMAEntryV2 (Fleet path): Proactive FSM + OrderId registration

### Location: Between entryOrders registration (line 397) and reservedDelta (line 402)

FIND:

```csharp
                        entryOrders[fleetKey] = fEntry;               // REAPER hasWorkingEntry check reads these

                        MarkDispatchSyncPending(expectedKey);
                        syncPending = true;

                        reservedDelta = (direction == MarketPosition.Long) ? qty : -qty;
                        AddExpectedPositionDeltaLocked(expectedKey, reservedDelta); // SECOND: expectedPositions

                        acct.Submit(new[] { fEntry }); // LAST ??" stateLock not held here
                        ClearDispatchSyncPending(expectedKey);
                        syncPending = false;
```

REPLACE:

```csharp
                        entryOrders[fleetKey] = fEntry;               // REAPER hasWorkingEntry check reads these

                        MarkDispatchSyncPending(expectedKey);
                        syncPending = true;

                        // Phase 6 [FSM-P3]: Proactive FSM for RMA V2 fleet entries.
                        // Entry-only (brackets deferred until fill via SymmetryGuard).
                        // State = Submitted (direct submit, no pump queue).
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
                        AddExpectedPositionDeltaLocked(expectedKey, reservedDelta); // SECOND: expectedPositions

                        acct.Submit(new[] { fEntry }); // LAST -- stateLock not held here

                        // Phase 6 [FSM-P3]: Register OrderId for O(1) FSM lookup (populated by Submit)
                        if (fEntry != null && !string.IsNullOrEmpty(fEntry.OrderId))
                            _orderIdToFsmKey[fEntry.OrderId] = fleetKey;

                        ClearDispatchSyncPending(expectedKey);
                        syncPending = false;
```

NOTE: The original line "acct.Submit(new[] { fEntry }); // LAST ??" contains a
corrupted comment suffix. Replace with clean ASCII: "// LAST -- stateLock not held here"

---

### E11 -- src/V12_002.SIMA.Execution.cs

### ExecuteRMAEntryV2 catch: Add FSM cleanup

### Location: Per-account catch block (lines 424-427)

FIND:

```csharp
                        activePositions.TryRemove(fleetKey, out _);
                        entryOrders.TryRemove(fleetKey, out _);
                        Print($"[SIMA RMA V2] FAIL {acct.Name}: {ex.Message}");
```

REPLACE:

```csharp
                        activePositions.TryRemove(fleetKey, out _);
                        entryOrders.TryRemove(fleetKey, out _);
                        // Phase 6: Clean up proactive FSM on dispatch failure
                        _followerBrackets.TryRemove(fleetKey, out _);
                        Print($"[SIMA RMA V2] FAIL {acct.Name}: {ex.Message}");
```

---

## SELF-AUDIT CHECKLIST (MANDATORY BEFORE COMMIT)

1. COMPILE: Zero errors, zero warnings.

2. LOCK AUDIT:

   ```
   grep -rn "lock(stateLock)" src/V12_002.SIMA.Dispatch.cs src/V12_002.SIMA.Fleet.cs src/V12_002.SIMA.Execution.cs src/V12_002.SIMA.cs src/V12_002.Symmetry.BracketFSM.cs
   ```

   EXPECTED: Zero hits.

3. ASCII SCAN:

   ```
   python check_ascii.py src/V12_002.SIMA.Dispatch.cs src/V12_002.SIMA.Fleet.cs src/V12_002.SIMA.Execution.cs src/V12_002.SIMA.cs src/V12_002.Symmetry.BracketFSM.cs
   ```

   EXPECTED: Zero violations.

4. FSM CREATION SITES:

   ```
   grep -n "new FollowerBracketFSM" src/V12_002.SIMA.Dispatch.cs src/V12_002.SIMA.Fleet.cs src/V12_002.SIMA.Execution.cs src/V12_002.Symmetry.Follower.cs src/V12_002.SIMA.Lifecycle.cs src/V12_002.Orders.Callbacks.Propagation.cs
   ```

   EXPECTED: Hits in ALL 6 files (Dispatch.cs should have 2: Market + Limit).

5. METADATAGUARD IN DISPATCH:

   ```
   grep -n "MetadataGuard" src/V12_002.SIMA.Dispatch.cs src/V12_002.SIMA.Execution.cs src/V12_002.SIMA.Fleet.cs
   ```

   EXPECTED: MetadataGuardDuplicate in Dispatch.cs + Execution.cs; MetadataGuardTimestamp in Fleet.cs.

6. PENDINGSUBMIT IN EXPECTED POSITION:

   ```
   grep -n "PendingSubmit" src/V12_002.Symmetry.BracketFSM.cs
   ```

   EXPECTED: Hit in GetFsmExpectedPosition state filter (line ~283).

7. SIGNALTICKS FIELD:

   ```
   grep -n "SignalTicks" src/V12_002.SIMA.cs src/V12_002.SIMA.Dispatch.cs src/V12_002.SIMA.Fleet.cs
   ```

   EXPECTED: Declaration in SIMA.cs, assignment in Dispatch.cs (2 hits), read in Fleet.cs.

8. CATCH BLOCK SYMMETRY:

   ```
   grep -n "_followerBrackets.TryRemove" src/V12_002.SIMA.Dispatch.cs src/V12_002.SIMA.Fleet.cs src/V12_002.SIMA.Execution.cs
   ```

   EXPECTED: Hits in ALL three files (catch blocks).

## COMMIT

```powershell
git add src/V12_002.SIMA.cs src/V12_002.Symmetry.BracketFSM.cs src/V12_002.SIMA.Dispatch.cs src/V12_002.SIMA.Fleet.cs src/V12_002.SIMA.Execution.cs
git commit -m "feat(phase-6): proactive FSM promotion + MetadataGuard dispatch integration"
```
