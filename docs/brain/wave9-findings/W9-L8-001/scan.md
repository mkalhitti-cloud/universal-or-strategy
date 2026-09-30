# W9-L8-001 Scan: ProcessOnStateChange

## File
`src/V12_002.Lifecycle.cs`

## Method Source

```csharp
private void ProcessOnStateChange(State state)
{
    switch (state)
    {
        case State.SetDefaults:
            HandleSetDefaults();
            break;
        case State.Configure:
            HandleConfigure();
            break;
        case State.DataLoaded:
            HandleDataLoaded();
            break;
        case State.Realtime:
            HandleRealtime();
            break;
        case State.Terminated:
            HandleTerminated();
            break;
    }
}
```

Lines 44–64 of `src/V12_002.Lifecycle.cs`.

## CYC

**6**

Calculation:
- Base: 1
- `case State.SetDefaults`: +1
- `case State.Configure`: +1
- `case State.DataLoaded`: +1
- `case State.Realtime`: +1
- `case State.Terminated`: +1
- No `&&` / `||` / `if` / `else if` conditions inside the method body
- **Total: 1 + 5 = 6**

## Key Type

`NinjaTrader.Cbi.State` — a NinjaTrader framework **enum**.

Enum values dispatched: `SetDefaults`, `Configure`, `DataLoaded`, `Realtime`, `Terminated`.

## Shared Context

`this` — the `V12_002` partial class instance (which extends `NinjaTrader.NinjaScript.Strategies.Strategy`).

All five handler methods (`HandleSetDefaults`, `HandleConfigure`, `HandleDataLoaded`, `HandleRealtime`, `HandleTerminated`) are private instance methods on `this`. They read and mutate shared instance state including fields such as `_configureComplete`, `_dataLoadedComplete`, `_isTerminating`, `atrIndicator`, `ema9`, `ipcCommandQueue`, etc.

The `state` parameter itself is the sole value captured from the caller (`OnStateChange`), snapshotted before entering the method.

## Dispatch Catalog

| # | Key (condition) | Handler Code |
|---|-----------------|--------------|
| 1 | `case State.SetDefaults` | `HandleSetDefaults();` — sets strategy metadata (Description, Name, Calculate, session/risk/stop/target/trailing/display/compliance defaults), resets `_configureComplete`, `_dataLoadedComplete`, clears telemetry, resets `_startupReadinessLogEmitted` to 0 via `Interlocked.Exchange` |
| 2 | `case State.Configure` | `HandleConfigure();` — allocates all `ConcurrentDictionary` collections (`activePositions`, `entryOrders`, `stopOrders`, `target1Orders`..`target5Orders`, `linkedTRENDEntries`, `pendingStopReplacements`), initializes IPC queue + client dict, calls `InitializeIpcHardening()`, allocates `expectedPositions`, creates `PhotonOrderPool` + `SPSCRing` + sideband + shadow salt, validates `FleetDispatchSlot` layout invariant (size=64, shadowOffset=56), calls `InitializeMmioMirror()`, pre-allocates `ExecutionIdRing`s, creates `SIMA_Logs` directory, adds 3 MTF `AddDataSeries` calls (5/10/15-min bars), sets `_configureComplete = true` |
| 3 | `case State.DataLoaded` | `HandleDataLoaded();` — captures `tickSize`/`pointValue`/`lastKnownPrice`, calls `InitializeInstrumentSettings()` + `InitializeTargetConfiguration()`, initializes `atrIndicator`/`ema9`/`ema15`/`ema30`/`ema65`/`ema200`/`rsiIndicator`, calls `ResetOR()`, prints diagnostics for symbol/session/targets/RMA/TREND/FFMA/SIMA, builds `complianceLogPath` + `dailySummaryCsvPath`, calls `EnsureDailySummaryCsv()` + `ExecuteRiskLogicAudit()`, sets `_dataLoadedComplete = true`, loads sticky state via `LoadStickyState()`, starts IPC server, calls `TouchStrategyHeartbeat()` + `PublishUiSnapshot()` |
| 4 | `case State.Realtime` | `HandleRealtime();` — prints deployment banner, calls `TouchStrategyHeartbeat()` + `PublishUiSnapshot()` + `StartWatchdog()`, conditionally (if `EnableSIMA`) enqueues actor lambda to call `EnumerateApexAccounts()` and optionally `StartReaperAudit()`, calls `AttachUiComponents()` (which defers hotkey/chart-click attach + panel creation/refresh to the WPF dispatcher) |
| 5 | `case State.Terminated` | `HandleTerminated();` — sets `_isTerminating = true`, stops watchdog, resets `_configureComplete`/`_dataLoadedComplete`/`_startupReadinessLogEmitted`, tears down chart UI + MMIO mirror, calls `CancelAllV12GtcOrders(false)`, drains IPC + actor queues, stops IPC server, stops Reaper audit, unsubscribes fleet accounts, clears `SignalBroadcaster` static subscribers, disposes `_simaToggleSem`, clears all order/position/profit/risk/account dictionaries |

## Notes for Fix Agent

`ProcessOnStateChange` itself is **not** the complexity hotspot — it is a clean dispatcher (CYC 6) that delegates entirely to five well-named private handler methods. The OKF violation (if any) for `W9-L8-001` resides in one or more of those delegates:

| Handler | Est. CYC | Primary concern |
|---------|----------|-----------------|
| `HandleSetDefaults` (L66–188) | ~1 | Many assignments; no branches — CYC = 1 |
| `HandleConfigure` (L400–481) | ~3 | One `if` (dir exists check) + one `if` (layout invariant) |
| `HandleDataLoaded` (L504–628) | ~4 | Three `if` branches (sticky loaded, `stickyLoaded` print, etc.) |
| `HandleRealtime` (L671–697) | ~3 | One `if (EnableSIMA)` + one inner `if (ReaperAuditEnabled)` |
| `HandleTerminated` (L190–237) | ~1 | No branches inside the method body itself |

`ProcessOnStateChange` is a textbook FSM dispatch table and is already OKF-compliant for complexity. The W9-L8-001 ticket should target the handlers themselves if wave-level complexity audit flagged this region.
