BUG-S4-001
Title: Race condition in AuditMaster_HandleNakedPosition (Missing Order Snapshot)
Severity: High
Location: V12_002.REAPER.Audit.cs.AuditMaster_HandleNakedPosition (line 490)
Root Cause: Account.Orders.Any(...) enumerates the NinjaTrader Account.Orders collection directly without taking a .ToArray() snapshot. The broker thread can modify this collection concurrently, throwing InvalidOperationException and killing the audit loop.
Evidence: bool masterHasWorkingStop = Account.Orders.Any(o => ...
Test Impact: Concurrency / Multithreading / Stress tests

BUG-S4-002
Title: Race condition in ProcessReaperFlatten_CancelWorkingOrders (Missing Order Snapshot)
Severity: High
Location: V12_002.REAPER.Audit.cs.ProcessReaperFlatten_CancelWorkingOrders (line 666)
Root Cause: foreach (Order order in targetAcct.Orders) enumerates the collection directly without a .ToArray() snapshot. Concurrent order updates will throw InvalidOperationException and abort the emergency flatten.
Evidence: foreach (Order order in targetAcct.Orders)
Test Impact: Concurrency / Multithreading / Stress tests

BUG-S4-003
Title: Race condition in ProcessReaperFlatten_ClosePositions (Missing Position Snapshot)
Severity: High
Location: V12_002.REAPER.Audit.cs.ProcessReaperFlatten_ClosePositions (line 688)
Root Cause: foreach (Position position in targetAcct.Positions) enumerates the collection directly without a .ToArray() snapshot. Concurrent position updates will throw InvalidOperationException and abort the emergency flatten.
Evidence: foreach (Position position in targetAcct.Positions)
Test Impact: Concurrency / Multithreading / Stress tests

BUG-S4-004
Title: Race conditions in Safety Watchdog position iteration (Missing Position Snapshots)
Severity: High
Location: V12_002.Safety.Watchdog.cs.HasWatchdogLeadAccountPosition, FlattenWatchdogPositions, FlattenDirectFallbackPositions (lines 99, 167, 274)
Root Cause: These methods enumerate masterAccount.Positions without calling .ToArray(). A concurrent position update from the broker will throw InvalidOperationException and crash the safety watchdog logic.
Evidence: foreach (Position position in masterAccount.Positions)
Test Impact: Concurrency / Multithreading / Stress tests

BUG-S4-005
Title: TOC-TOU Race Condition in In-Flight Guards (Re-entrancy flood risk)
Severity: High
Location: V12_002.REAPER.Audit.cs.EnqueueReaperRepairCandidate, EnqueueReaperNakedStopCandidate, EnqueueReaperMasterNakedStop
Root Cause: The code checks ContainsKey on the in-flight ConcurrentDictionary and then later calls TryAdd without checking the return value. Concurrent threads can both pass ContainsKey and queue duplicates, causing a re-entrancy flood. (Contrast with EnqueueReaperFlattenCandidate which correctly uses if (!_reaperFlattenInFlight.TryAdd(...))).
Evidence: alreadyInFlight = _repairInFlight.ContainsKey(repairKey); if (!alreadyInFlight) { ... _repairInFlight.TryAdd(repairKey, 0); }
Test Impact: Concurrency / Multithreading / Stress tests

BUG-S4-006
Title: Silent catch block swallowing exceptions in ExecuteReaperRepair
Severity: Med
Location: V12_002.REAPER.Repair.cs.ExecuteReaperRepair (line 257)
Root Cause: Empty catch { } block intentionally swallows exceptions without logging them, violating the V12 Platinum Standard (Sentry: All runtime errors MUST be captured...).
Evidence: catch { } around RemoveDrawObject
Test Impact: Architectural Review / Static Analysis

BUG-S4-007
Title: O(N^2) nested loops in AuditFleet_CalculateExpectedActual
Severity: Low
Location: V12_002.REAPER.Audit.cs.AuditFleet_CalculateExpectedActual (line 288)
Root Cause: The heartbeat audit iterates over Account.All (N accounts), and for each account calls AuditFleet_CalculateExpectedActual which linearly scans _followerBrackets.Values (M FSMs). Since M scales with N, this produces an O(N^2) hot path iteration.
Evidence: accountFsms = _followerBrackets.Values.Where(f => f.AccountName == acct.Name).ToList();
Test Impact: Performance / Scale tests