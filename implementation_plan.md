# Implementation Plan - Phase 7 Sprint 5 (T03)
**Mission**: Hardening `ExecuteSmartDispatchEntry` via surgical extraction.
**Target**: `src/V12_002.SIMA.Dispatch.cs`
**DNA Gate**: CYC < 20, LOC >= 15, Zero-Locks, ASCII-Only.

## Stage P3.5: Plannotator Surgical Brief

### Target 1: The Limit Branch Extraction
**Action**: Replace the inlined `else` block in `ExecuteSmartDispatchEntry` with a call to the new helper.
**Note**: `ocoId` is intentionally dropped from the signature (DEVIATION-T3-A).

**TargetContent** (starting around line 156):
```csharp
                        else
                        {
                            // V12.Phantom-Fix [FIX-1]: Register tracking dicts BEFORE updating expectedPositions.
                            // REAPER runs on a background thread; if it fires between the expectedPositions
                            // update and the dict commit (the old T1->T3 race), it observes non-zero expected
                            // with no entry in entryOrders -> hasWorkingEntry=false -> phantom repair queued.
                            // Registering dicts first guarantees REAPER always finds the blocking entry.
                            // B966: Enqueue NOT applied -- ordering invariant: dict BEFORE expectedPositions update (Phantom-Fix).
                            // ConcurrentDictionary single-writes are thread-safe here.
                            activePositions[fleetEntryName] = fleetPos;
                            entryOrders[fleetEntryName] = entry; // V12.3: Track entry for CIT chase
                            registeredForCleanup = true;
                            MarkDispatchSyncPending(expectedKey);
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

                            int _poolSlotIndexLmt = -1;
                            Order[] _proxyOrdersLmt = null;
                            {
                                var _claimedLmt = _photonPool.Claim();
                                if (_claimedLmt.Orders != null)
                                {
                                    _proxyOrdersLmt = _claimedLmt.Orders;
                                    _poolSlotIndexLmt = _claimedLmt.SlotIndex;
                                }
                                else
                                {
                                    _proxyOrdersLmt = new Order[MaxOrdersPerSlot];
                                    _poolSlotIndexLmt = -1;
                                }
                            }
                            _proxyOrdersLmt[0] = entry;

                            if (_poolSlotIndexLmt >= 0)
                            {
                                _photonSideband[_poolSlotIndexLmt].Account        = acct;
                                _photonSideband[_poolSlotIndexLmt].FleetEntryName = fleetEntryName;
                                _photonSideband[_poolSlotIndexLmt].ExpectedKey    = expectedKey;
                                Thread.MemoryBarrier();
                            }

                            FleetDispatchSlot _slotLmt = new FleetDispatchSlot
                            {
                                EntryPrice    = entry.LimitPrice > 0 ? entry.LimitPrice : 0,
                                StopPrice     = 0,
                                SignalTicks   = DateTime.UtcNow.Ticks,
                                PoolSlotIndex = _poolSlotIndexLmt,
                                OrderCount    = 1,
                                Quantity      = followerQty,
                                TargetCount   = 0,
                                Action        = (int)action,
                                ReservedDelta = reservedDelta
                            };
                            _slotLmt.Shadow = ComputeFleetDispatchShadow(ref _slotLmt, _photonShadowSalt);

                            Interlocked.Increment(ref _pendingFleetDispatchCount);

                            if (_poolSlotIndexLmt >= 0 && _photonDispatchRing.TryEnqueue(ref _slotLmt))
                            {
                                if (_poolSlotIndexLmt >= 0 && _photonMmioMirror != null)
                                {
                                    try { _photonMmioMirror.TryPublish(ref _slotLmt); } catch { }
                                }
                            }
                            else
                            {
                                if (_poolSlotIndexLmt >= 0)
                                {
                                    Order[] legacyOrdersLmt = new Order[] { entry };
                                    _photonPool.ReleaseByIndex(_poolSlotIndexLmt);
                                    _photonSideband[_poolSlotIndexLmt] = default(FleetDispatchSideband);
                                    _proxyOrdersLmt = legacyOrdersLmt;
                                }
                                _pendingFleetDispatches.Enqueue(new FleetDispatchRequest
                                {
                                    Account = acct,
                                    Orders = _proxyOrdersLmt,
                                    FleetEntryName = fleetEntryName,
                                    ExpectedKey = expectedKey,
                                    ReservedDelta = reservedDelta,
                                    SignalTicks = DateTime.UtcNow.Ticks
                                });
                            }
                            syncPending         = false;
                            reservedDelta       = 0;
                            registeredForCleanup = false;

                            dispatchLog.AppendLine(string.Format("  QUEUE | {0,-28} | Limit        | PENDING",
                                acct.Name));
                        }
```

**ReplacementContent**:
```csharp
                        else
                        {
                            Dispatch_PublishLimitEntryToPhoton(
                                tradeType, action, quantity, entryPrice, entryOrderType, acct, i, symmetryDispatchId,
                                fleetPos, entry, fleetEntryName, expectedKey, followerQty, ft1, ft2, ft3, ft4, ft5,
                                stopPrice, t1TargetPrice, t2TargetPrice, t3TargetPrice, t4TargetPrice, t5TargetPrice,
                                dispatchTargetCount,
                                dispatchLog,
                                ref syncPending,
                                ref reservedDelta,
                                ref registeredForCleanup);
                        }
```

### Target 2: Insertion of Helper Method
**Action**: Insert the new helper method at the end of the `Dispatch` region.

**Insertion Point**: After the `Dispatch_PublishMarketBracketToPhoton` method (around line 717).

**Content**:
```csharp
        /// <summary>
        /// [V12-T03] Extraction of Limit branch for Photon ring dispatch.
        /// Zero-allocation, thread-safe (DNA Rule 2). Signature drops ocoId (DEVIATION-T3-A).
        /// </summary>
        private void Dispatch_PublishLimitEntryToPhoton(
            string tradeType, OrderAction action, int quantity, double entryPrice, OrderType entryOrderType,
            Account acct, int i, string symmetryDispatchId, PositionInfo fleetPos, Order entry,
            string fleetEntryName, string expectedKey, int followerQty, int ft1, int ft2, int ft3, int ft4, int ft5,
            double stopPrice, double t1TargetPrice, double t2TargetPrice, double t3TargetPrice, double t4TargetPrice, double t5TargetPrice,
            int dispatchTargetCount, StringBuilder dispatchLog,
            ref bool syncPending, ref int reservedDelta, ref bool registeredForCleanup)
        {
            // V12.Phantom-Fix [FIX-1]: Register tracking dicts BEFORE updating expectedPositions.
            // REAPER runs on a background thread; if it fires between the expectedPositions
            // update and the dict commit (the old T1->T3 race), it observes non-zero expected
            // with no entry in entryOrders -> hasWorkingEntry=false -> phantom repair queued.
            // Registering dicts first guarantees REAPER always finds the blocking entry.
            // B966: Enqueue NOT applied -- ordering invariant: dict BEFORE expectedPositions update (Phantom-Fix).
            // ConcurrentDictionary single-writes are thread-safe here.
            activePositions[fleetEntryName] = fleetPos;
            entryOrders[fleetEntryName] = entry; // V12.3: Track entry for CIT chase
            registeredForCleanup = true;
            MarkDispatchSyncPending(expectedKey);
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

            int _poolSlotIndexLmt = -1;
            Order[] _proxyOrdersLmt = null;
            {
                var _claimedLmt = _photonPool.Claim();
                if (_claimedLmt.Orders != null)
                {
                    _proxyOrdersLmt = _claimedLmt.Orders;
                    _poolSlotIndexLmt = _claimedLmt.SlotIndex;
                }
                else
                {
                    _proxyOrdersLmt = new Order[MaxOrdersPerSlot];
                    _poolSlotIndexLmt = -1;
                }
            }
            _proxyOrdersLmt[0] = entry;

            if (_poolSlotIndexLmt >= 0)
            {
                _photonSideband[_poolSlotIndexLmt].Account        = acct;
                _photonSideband[_poolSlotIndexLmt].FleetEntryName = fleetEntryName;
                _photonSideband[_poolSlotIndexLmt].ExpectedKey    = expectedKey;
                Thread.MemoryBarrier();
            }

            FleetDispatchSlot _slotLmt = new FleetDispatchSlot
            {
                EntryPrice    = entry.LimitPrice > 0 ? entry.LimitPrice : 0,
                StopPrice     = 0,
                SignalTicks   = DateTime.UtcNow.Ticks,
                PoolSlotIndex = _poolSlotIndexLmt,
                OrderCount    = 1,
                Quantity      = followerQty,
                TargetCount   = 0,
                Action        = (int)action,
                ReservedDelta = reservedDelta
            };
            _slotLmt.Shadow = ComputeFleetDispatchShadow(ref _slotLmt, _photonShadowSalt);

            Interlocked.Increment(ref _pendingFleetDispatchCount);

            if (_poolSlotIndexLmt >= 0 && _photonDispatchRing.TryEnqueue(ref _slotLmt))
            {
                if (_photonMmioMirror != null)
                {
                    try { _photonMmioMirror.TryPublish(ref _slotLmt); } catch { }
                }
            }
            else
            {
                if (_poolSlotIndexLmt >= 0)
                {
                    Order[] legacyOrdersLmt = new Order[] { entry };
                    _photonPool.ReleaseByIndex(_poolSlotIndexLmt);
                    _photonSideband[_poolSlotIndexLmt] = default(FleetDispatchSideband);
                    _proxyOrdersLmt = legacyOrdersLmt;
                }
                _pendingFleetDispatches.Enqueue(new FleetDispatchRequest
                {
                    Account = acct,
                    Orders = _proxyOrdersLmt,
                    FleetEntryName = fleetEntryName,
                    ExpectedKey = expectedKey,
                    ReservedDelta = reservedDelta,
                    SignalTicks = DateTime.UtcNow.Ticks
                });
            }
            syncPending         = false;
            reservedDelta       = 0;
            registeredForCleanup = false;

            dispatchLog.AppendLine(string.Format("  QUEUE | {0,-28} | Limit        | PENDING",
                acct.Name));
        }
```

## Stage P5: Verification & Deploy
1. **CYC Audit**: Run `python scripts/complexity_audit.py` -> Verify CYC < 20.
2. **MemoryBarrier Count**: Verify exactly 1 `Thread.MemoryBarrier()` in the new helper.
3. **Hard-Link Sync**: Run `powershell -File .\deploy-sync.ps1`.
4. **NinjaTrader Gate**: Press F5 and verify `BUILD_TAG` 1111.007.
