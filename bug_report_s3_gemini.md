# V12 Photon Kernel Bug Bounty - Cluster S3 (UI & Photon IO)

**BUG-S3-001**
Title: Race condition in UI Panel Refresh Guard
Severity: Med
Location: V12_002.UI.Panel.Lifecycle.cs.OnPanelRefreshElapsed
Root Cause: The synchronization guard uses a non-atomic two-step check and set (`Volatile.Read` followed by `Interlocked.Exchange`). If multiple timer ticks or threads evaluate `Volatile.Read` simultaneously, they will all proceed to queue dispatcher tasks, defeating the freeze-proof protection mechanism.
Evidence: 
```csharp
if (Volatile.Read(ref _panelUpdateInProgress) != 0) return;
try {
    if (ChartControl != null) {
        Interlocked.Exchange(ref _panelUpdateInProgress, 1);
        ChartControl.Dispatcher.InvokeAsync(() => ...
```
Test Impact: Concurrency test under high timer frequency or system stress.

**BUG-S3-002**
Title: Use-after-free / NullRef on IPC Listener Shutdown
Severity: High
Location: V12_002.UI.IPC.Server.cs.ListenForRemote
Root Cause: `StopIpcServer` nulls `ipcListener` without taking any locks, while the background thread running `ListenForRemote` polls `ipcListener.Pending()` in a tight loop. This creates a race condition leading to a `NullReferenceException` (the managed equivalent of use-after-free) on shutdown.
Evidence: `while (isIpcRunning) { if (!ipcListener.Pending()) ... }` executes concurrently with `ipcListener.Stop(); ipcListener = null;` in `StopIpcServer`.
Test Impact: Shutdown/restart IPC server test while the listener thread is actively polling.

**BUG-S3-003**
Title: Ghost Order Window during CANCEL_ALL
Severity: Critical
Location: V12_002.UI.IPC.Commands.Fleet.cs.CancelAll_CleanupUnfilledPositions
Root Cause: Local memory states (`activePositions`, `entryOrders`) are synchronously wiped via `CleanupPosition(kvp.Key)` for unfilled orders, but the actual broker cancellation (`CancelOrderSafe`) happens asynchronously. If a fill execution arrives from the broker before the cancellation is processed, the system has no local state to map it to, creating an unmanaged orphaned ghost order.
Evidence: `if (!kvp.Value.EntryFilled) { CleanupPosition(kvp.Key); ... }`
Test Impact: Fill-during-cancel race simulation (latency injection between command dispatch and broker ack).

**BUG-S3-004**
Title: Target Order Dictionaries FSM State Leak
Severity: Med
Location: V12_002.UI.Compliance.cs.FinalizeStopFilledPosition
Root Cause: When a fleet position is fully closed by a stop order, the cleanup logic manually removes the entry from `stopOrders`, `pendingStopReplacements`, `activePositions`, and `entryOrders`. However, it completely fails to remove the entry from `target1Orders`, `target2Orders`, `target3Orders`, `target4Orders`, and `target5Orders`. These `ConcurrentDictionary` collections will leak target order state references indefinitely.
Evidence: `activePositions.TryRemove(entryKey, out _);` is executed without corresponding `target1Orders.TryRemove(entryKey, out _);` calls.
Test Impact: Long-running memory profiling and FSM state validation after multiple sequential stop-outs.

**BUG-S3-005**
Title: O(N*D) Exponential Memory Allocation in Visual Tree Search
Severity: High
Location: V12_002.UI.Panel.Construction.cs.FindAllButtonsByText
Root Cause: The method recursively traverses the WPF visual tree. At every single node, it allocates a new `List<Button>` and uses `AddRange(FindAllButtonsByText(...))` which copies all elements aggregated from its subtrees. For a deep and wide visual tree like NinjaTrader's `ChartControl`, this causes massive $O(N \cdot D)$ memory allocations and extreme GC pressure during panel construction fallbacks.
Evidence: `var list = new List<Button>(); ... list.AddRange(FindAllButtonsByText(child, text)); return list;`
Test Impact: CPU/GC profiling during panel construction or fallback placement on complex chart windows.

**BUG-S3-006**
Title: Non-ASCII String Literals in Print Statements
Severity: Low
Location: V12_002.UI.IPC.Commands.Misc.cs.HandleFleet_DiagFleet and V12_002.UI.Callbacks.cs.ExecuteTarget_Market
Root Cause: Use of non-ASCII characters (likely checkmarks `✓` or arrows `→`, rendered as non-ASCII bytes or `?` in decoded source) inside `Print()` formatting string literals. This violates the CRITICAL ASCII rule established in the V12 Platinum Standard to avoid compiler/runtime encoding risks.
Evidence: `Print($"[DIAG]   {acct.Name} -> {(isActive ? "? ACTIVE" : "[X] INACTIVE")}");` and `Print(string.Format("? {0} MARKET FILL: ..."));`
Test Impact: Static analysis / ASCII compiler gate test.
