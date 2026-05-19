# V12 Photon Kernel Bug Bounty - S6 (Signals & Entries) Audit

BUG-S6-001
Title: Asynchronous Add vs Synchronous Remove Race Condition in Retest Rollback
Severity: Critical
Location: V12_002.Entries.Retest.cs.ExecuteRetestEntry and ExecuteRetestManualEntry
Root Cause: Position is added to `activePositions` asynchronously via `Enqueue` prior to submitting the entry order. If the broker submission fails (returns null), the rollback logic attempts to synchronously remove the position using `activePositions.TryRemove()`. Because the addition was queued to the actor, the synchronous removal fails to find the key, and the subsequent execution of the queued addition permanently leaks the ghost position into the FSM state.
Evidence: `{ var _en966 = entryName; var _p966 = pos; Enqueue(ctx => { ctx.activePositions[_en966] = _p966; }); }` followed by `activePositions.TryRemove(entryName, out _);` on submission failure.
Test Impact: Stress/load tests forcing order submission rejections (e.g., simulated disconnects or margin limits) will trigger permanent FSM corruption, failing position tracking assertions.

BUG-S6-002
Title: V12 Platinum Standard Violation (Shared State Mutation Without Enqueue)
Severity: Medium
Location: V12_002.Entries.Trend.cs.ExecuteTREND_SubmitLeg2 and V12_002.Entries.RMA.cs.SubmitTrendSplitBrackets
Root Cause: The `linkedTRENDEntries` dictionary is mutated directly (synchronously) when setting and removing partnership references for Trend split legs. The V12 FSM model mandates that all internal state mutations route through the Actor/Enqueue pattern to ensure sequential consistency, with direct writes explicitly restricted to `stopOrders` during bracket submission.
Evidence: `linkedTRENDEntries[entry1Name] = entry2Name;` and `linkedTRENDEntries.TryRemove(entry1Name, out removedPartner);` executed directly.
Test Impact: Architectural conformance tests / static code audits verifying FSM state access patterns.

BUG-S6-003
Title: Race Condition on Shared Sync Flags (isLongArmed / isShortArmed)
Severity: High
Location: V12_002.Entries.OR.cs.ExecuteLong and ExecuteShort
Root Cause: The ToS manual sync interception logic mutates shared boolean state flags (`isLongArmed`, `isShortArmed`) and timestamps (`lastArmedTime`) directly without atomic guards or routing through the `Enqueue` FSM. Concurrent invocations (e.g., from network threads and UI threads) can produce race conditions, causing missed entries or double-click bypass failures.
Evidence: `isLongArmed = true; isShortArmed = false; lastArmedTime = DateTime.Now;` modified directly.
Test Impact: Concurrency simulation tests firing simulated UI clicks alongside network packets.

BUG-S6-004
Title: Unsafe Direct Mutation of PositionInfo State (FSM State Leak)
Severity: High
Location: V12_002.Entries.RMA.cs.MonitorRmaProximity
Root Cause: The `MonitorRmaProximity` method directly mutates properties of the `PositionInfo` object (`ClosestApproachTicks`, `WasInProximity`, `ProximityProbeCount`) without dispatching the mutation to the Actor queue (`Enqueue`). This violates the FSM concurrency model and creates race conditions if the actor thread concurrently accesses or mutates the same `PositionInfo` object.
Evidence: `pos.WasInProximity = true; pos.ProximityProbeCount++;` modified directly.
Test Impact: Multi-threaded fuzzing of FSM state transitions during active RMA proximity zones.