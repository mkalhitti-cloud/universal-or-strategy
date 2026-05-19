# V12 Photon Kernel Bug Bounty - Cluster S2 (Execution Engine)
**Auditor:** Gemini CLI
**Target:** `src/V12_002.Orders.*.cs`, `src/V12_002.Symmetry.*.cs`, `src/V12_002.Trailing.*.cs`

BUG-S2-001
Title: Build 981 Protocol Violation (Ghost Order Window in Bracket Submission)
Severity: Critical
Location: V12_002.Symmetry.Follower.cs.SymmetryGuardSubmitFollowerBracket
Root Cause: Uses `Enqueue` to write the newly created stop order to the `stopOrders` dictionary during follower bracket submission. This violates the strict Build 981 Protocol mandate which requires synchronous direct writes to `stopOrders` to prevent a ghost-order tracking window during shutdown races.
Evidence: `ordersToSubmit.Insert(0, stop); { var _fen966 = fleetEntryName; var _s966 = stop; Enqueue(ctx => { ctx.stopOrders[_fen966] = _s966; }); }`
Test Impact: Strategy shutdown / panic flatten tests during follower bracket initialization.

BUG-S2-002
Title: FSM State Leak & Unreachable Logic (Target Replace Cancel)
Severity: Critical
Location: V12_002.Orders.Callbacks.AccountOrders.cs.HandleMatchedFollowerOrder
Root Cause: The `HandleMatchedFollower_TargetReplaceCancel` FSM resolution method is erroneously nested inside the `if (entryOrders.TryGetValue(...) && entryOrder == order)` block. Since target orders will never match an entry order, this logic is logically unreachable. Consequently, the two-phase FSM for follower target replacements silently fails, leaks `FollowerTargetReplaceSpec` objects, and abandons the replacement target submission.
Evidence: `if (HandleMatchedFollower_TargetReplaceCancel(order))` is executed inside the `if (entryOrders.TryGetValue(...) && ...)` block.
Test Impact: Dynamic target adjustment (e.g., ChaseIfTouch or trailing target) tests on fleet followers.

BUG-S2-003
Title: O(N^2) Nested Loop & Unnecessary Broker Order Scan
Severity: High
Location: V12_002.Trailing.Breakeven.cs.FindTargetOrderForPosition & FindTargetOrderForAbsoluteMove
Root Cause: Iterates over the entire `searchAcct.Orders` collection inside a loop over `activePositions` instead of using the O(1) local dictionary lookup `GetTargetOrdersDictionary(targetNum).TryGetValue(...)`. This creates an O(N^2) performance cliff under load, bypassing the internal strategy state tracking in favor of a slow broker property enumeration.
Evidence: `foreach (Order order in searchAcct.Orders)` nested inside the caller's `foreach (var kvp in activePositions.ToArray())`.
Test Impact: High-volume / fleet scaling performance load tests.

BUG-S2-004
Title: Race Condition (TOCTOU) on ConcurrentDictionary Read
Severity: High
Location: V12_002.Orders.Management.StopSync.cs.UpdateStopQuantity & V12_002.Orders.Management.Flatten.cs.CancelUnfilledMasterEntries
Root Cause: Uses the `ContainsKey` check followed sequentially by the indexer access `[]` on a `ConcurrentDictionary`. If another thread (or an asynchronous broker callback) removes the key between the check and the access, a `KeyNotFoundException` is thrown, crashing the stop quantity update logic and potentially leaving the position under-protected.
Evidence: `if (!stopOrders.ContainsKey(entryName)) return;` ... followed by `Order currentStop = stopOrders[entryName];` without `TryGetValue`.
Test Impact: High-concurrency target-fill race tests and async flatten load tests.

BUG-S2-005
Title: Build 981 Protocol Violation (Ghost Order Window in Stop Replacement)
Severity: Critical
Location: V12_002.Trailing.StopUpdate.cs.CreateDirectStopOrder
Root Cause: Like BUG-S2-001, uses `Enqueue` to map the newly created stop replacement to `stopOrders`. The mandate explicitly forbids `Enqueue` for stop order registration. If flatten occurs before the actor mailbox drains, the unmapped stop becomes a ghost order at the broker.
Evidence: `{ var _en966 = entryName; var _ns966 = newStop; Enqueue(ctx => { ctx.stopOrders[_en966] = _ns966; }); }`
Test Impact: Shutdown/Flatten tests exactly at the moment of trailing stop step updates.