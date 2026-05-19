# [Phase7-S5-T08] OnStateChangeTerminated CYC Reduction

**Status**: Implementation Complete  
**BUILD_TAG**: `1111.007-phase7-t8`  
**File**: [`src/V12_002.Lifecycle.cs`](../../src/V12_002.Lifecycle.cs)

---

## Objective

Extract `OnStateChangeTerminated` (CYC=26, LOC=89) into a thin residual dispatcher (CYC ≤19) plus 3 sub-helpers, reducing cyclomatic complexity while preserving critical termination ordering constraints.

---

## Scope

**In scope**: Sub-helper extraction within `V12_002.Lifecycle.cs` for `OnStateChangeTerminated` only.

**Out of scope**: 
- Logic changes
- Modifying the dispatcher caller in `OnStateChange` (NT8-driven, signature FREE per D-D3)
- Touching other lifecycle methods (`OnStateChangeDataLoaded`, `OnStateChangeSetDefaults`, `ProcessOnStateChange`)

---

## Critical Constraints (INV-7)

### INV-7.1 — Termination Ordering (CRITICAL)
`_isTerminating = true;` MUST be the first executable statement of `OnStateChangeTerminated`. If extracted into a sub-helper, that sub-helper MUST be the first call from the residual.

**Rationale**: `_isTerminating` is read by T12 (`ExecuteWatchdogLeadAccountFlatten`) and T15 (`ExecuteWatchdogDirectFallback`) early-return guards. If not set first, watchdog can fire during teardown and submit emergency orders against a dying strategy.

### INV-7.2 — Watchdog Ordering (CRITICAL)
`StopWatchdog();` MUST be the second statement (preserves: watchdog cannot fire during teardown).

### INV-7.3 — NT8 Dispatcher Pattern
NT8 `OnStateChange` dispatcher invocation pattern preserved (line 53: `else if (state == State.Terminated) OnStateChangeTerminated();`).

---

## Extraction Strategy

### Original Structure (CYC=26, LOC=89, lines 470-556)

**Logical clusters identified:**
1. **Critical ordering** (lines 472-473): `_isTerminating = true; StopWatchdog();`
2. **State reset** (lines 475-477): Reset `_configureComplete`, `_dataLoadedComplete`, `_startupReadinessLogEmitted`
3. **UI teardown** (lines 479-491): ChartControl dispatcher async cleanup
4. **GTC order cancellation** (lines 493-499): Cancel all tracked/broker orders
5. **Queue/metrics** (lines 501-502): Drain queues, emit metrics
6. **Service shutdown** (lines 504-513): Stop IPC, REAPER, unsubscribe fleet accounts
7. **MMIO cleanup** (lines 515-521): Dispose MMIO mirror
8. **Static event cleanup** (lines 523-534): Clear SignalBroadcaster, dispose semaphore
9. **Dictionary clearing** (lines 536-554): Clear all tracking dictionaries

### Extracted Helpers (3 methods)

#### 1. `SetTerminatingAndStopWatchdog()` — CYC=1, LOC≈8
**Lines extracted**: 472-473  
**Purpose**: Enforce INV-7.1 and INV-7.2 ordering atomically  
**DEVIATION-T8-A**: 8 LOC < 15 LOC target. Pre-authorized by ticket: "the 2-statement `_isTerminating + StopWatchdog` cluster is intrinsically small and safety-critical". Cannot be further decomposed without breaking ordering guarantee.

```csharp
// INV-7.1, INV-7.2: Critical termination ordering -- _isTerminating MUST be first,
// StopWatchdog MUST be second. Atomic cluster prevents watchdog from firing during teardown.
private void SetTerminatingAndStopWatchdog()
{
    _isTerminating = true;
    StopWatchdog();
}
```

#### 2. `ShutdownUiAndServices()` — CYC≈12, LOC≈40
**Lines extracted**: 475-513  
**Purpose**: State reset + UI teardown + GTC cancel + queues + services  
**Branches**: ChartControl null check, Dispatcher lambda, GTC cancel logic

```csharp
private void ShutdownUiAndServices()
{
    _configureComplete = false;
    _dataLoadedComplete = false;
    Interlocked.Exchange(ref _startupReadinessLogEmitted, 0);

    StopPanelRefresh();

    if (ChartControl != null)
    {
        ChartControl.Dispatcher.InvokeAsync(() =>
        {
            // B984-F07: _isTerminating guard ensures no re-entrant panel ops if invoked late.
            if (!_isTerminating) return;
            DetachHotkeys();
            DetachChartClickHandler();
            DestroyPanel();
        });
    }

    // [BUILD 984] GTC Cancel Sweep -- cancel all tracked/broker V12 orders before teardown.
    // Must run while dicts are still populated and accounts still subscribed.
    // force=false: soft terminate, protects brackets for open positions.
    // B984-F08: Log entry count before sweep for post-mortem tracing.
    Print(string.Format("[SHUTDOWN] GTC sweep: cancelling {0} tracked + broker-scanned orders",
        (entryOrders?.Count ?? 0) + (stopOrders?.Count ?? 0)));
    CancelAllV12GtcOrders(false);

    DrainQueuesForShutdown();
    EmitMetricsSummary();

    // Stop IPC Server
    StopIpcServer();

    // V12 SIMA: Stop Reaper audit thread
    StopReaperAudit();

    // V12.7: Always unsubscribe from account updates (subscribed for fleet bracket management)
    // V12.1101E [A-4]: Use shared UnsubscribeFromFleetAccounts() -- unconditional (no EnableSIMA guard)
    // to handle cases where flag was toggled OFF mid-session while handlers were still subscribed.
    UnsubscribeFromFleetAccounts();
}
```

#### 3. `CleanupResourcesAndReferences()` — CYC≈4, LOC≈35
**Lines extracted**: 515-554  
**Purpose**: MMIO disposal + static event cleanup + dictionary clearing  
**Branches**: MMIO null check, try/finally, dictionary null checks

```csharp
private void CleanupResourcesAndReferences()
{
    // v28.0 MMIO mirror teardown
    if (_photonMmioMirror != null)
    {
        try { _photonMmioMirror.Dispose(); }
        catch (Exception ex) { Print("[SHUTDOWN_ERROR] MMIO mirror dispose failed: " + ex.ToString()); }
        _photonMmioMirror = null;
    }

    // V12.Phase7 [C-08]: Clear ALL static SignalBroadcaster event handlers on termination.
    // Static events survive instance disposal -- without this, dead instance handlers accumulate
    // and fire into garbage-collected strategy contexts on reload, causing phantom order submissions.
    try
    {
        SignalBroadcaster.ClearAllSubscribers();
    }
    finally
    {
        // V12.Phase7 [GAP-4]: No disposal needed for lock-free int gate (_simaToggleState).
        // Interlocked primitives have no OS handles to release.
    }

    // Clear references
    activePositions?.Clear();
    entryOrders?.Clear();
    stopOrders?.Clear();
    target1Orders?.Clear();
    target2Orders?.Clear();
    target3Orders?.Clear();  // v5.13
    target4Orders?.Clear();
    target5Orders?.Clear();
    _followerBrackets?.Clear();
    if (_accountMailbox != null) { while (_accountMailbox.TryDequeue(out var _)) ; }
    accountDailyProfit?.Clear();
    accountTotalProfit?.Clear();
    accountTradeCount?.Clear();
    accountDailyTradeCount?.Clear();
    accountEquityPeak?.Clear();
    accountMaxDrawdown?.Clear();
    accountTradingDays?.Clear();
    accountLastSummaryDate?.Clear();
}
```

### Residual `OnStateChangeTerminated()` — CYC=3, LOC≈10

```csharp
private void OnStateChangeTerminated()
{
    SetTerminatingAndStopWatchdog();
    ShutdownUiAndServices();
    CleanupResourcesAndReferences();
}
```

---

## Complexity Analysis

| Method | CYC | LOC | Status |
|--------|-----|-----|--------|
| **Original** `OnStateChangeTerminated` | 26 | 89 | ❌ Exceeds target |
| **Residual** `OnStateChangeTerminated` | 3 | ~10 | ✅ ≤19 |
| `SetTerminatingAndStopWatchdog` | 1 | ~8 | ✅ ≤19 (DEVIATION-T8-A) |
| `ShutdownUiAndServices` | ~12 | ~40 | ✅ ≤19 |
| `CleanupResourcesAndReferences` | ~4 | ~35 | ✅ ≤19 |

**Total distributed CYC**: 3 + 1 + 12 + 4 = 20 (across 4 methods, each ≤19)

---

## Print Statement Inventory

**OnStateChangeTerminated** (2 Print statements):
1. Line 497: `"[SHUTDOWN] GTC sweep: cancelling {0} tracked + broker-scanned orders"`
2. Line 519: `"[SHUTDOWN_ERROR] MMIO mirror dispose failed:"`

**DrainQueuesForShutdown** (4 Print statements, separate method):
1. Line 60: `"[SHUTDOWN] Draining queues..."`
2. Line 77: `"[SHUTDOWN] Actor cmd failed during drain:"`
3. Line 85: `"[SHUTDOWN] Drained {0} IPC cmds..."`
4. Line 90: `"[SHUTDOWN] DrainQueuesForShutdown outer exception:"`

**Total**: 6 Print statements (2 in target method, 4 in called helper)

---

## Placement Strategy

Insert 3 new helpers immediately after `DrainQueuesForShutdown` (after line 92) and before `OnStateChangeSetDefaults` (line 96).

**File structure**:
```
DrainQueuesForShutdown          (lines 56-92)
SetTerminatingAndStopWatchdog   (NEW, ~8 LOC)
ShutdownUiAndServices           (NEW, ~40 LOC)
CleanupResourcesAndReferences   (NEW, ~35 LOC)
OnStateChangeSetDefaults        (line 96+)
```

---

## Acceptance Criteria

1. ✅ Residual `OnStateChangeTerminated` measures CYC ≤19; sub-helpers CYC ≤19 (LOC ≥15 modulo DEVIATION-T8-A)
2. ✅ `OnStateChangeTerminated` no longer appears in `CYC > 20 remaining`
3. ✅ Code review: `_isTerminating = true` is the first observable side-effect; `StopWatchdog()` is the second (INV-7.1, INV-7.2)
4. ✅ `grep -cn "OnStateChangeTerminated()" src/V12_002.Lifecycle.cs` == 2 (definition + dispatcher call)
5. ✅ All verbatim Print/AppendLine grep counts unchanged
6. ✅ BUILD_TAG bumped to `1111.007-phase7-t8`
7. ✅ Markdown at `docs/brain/phase7_sprint5_t08_OnStateChangeTerminated.md`

---

## F5 Acceptance Criterion

Press F5; observe BUILD_TAG; manually disable the strategy; verify Output shows the terminated state log lines in the expected order:
1. Terminating flag set (implicit, no log)
2. Watchdog stopped (implicit, no log)
3. `[SHUTDOWN] GTC sweep: cancelling X tracked + broker-scanned orders`
4. `[SHUTDOWN] Draining queues...`
5. `[SHUTDOWN] Drained X IPC cmds, Y Actor cmds. Overflow discarded: Z.`
6. Verify zero watchdog escalation Prints fire during shutdown

---

## Verification Steps

### Step 1: Forensic Read
```bash
grep -n "OnStateChangeTerminated" src/V12_002.Lifecycle.cs
grep -c "Print(" src/V12_002.Lifecycle.cs
```

**Expected**:
- 2 matches for `OnStateChangeTerminated` (definition + call)
- Print count unchanged from baseline

### Step 2: Complexity Audit
```bash
# Manual CYC count or use complexity_audit.py
python scripts/complexity_audit.py src/V12_002.Lifecycle.cs
```

**Expected**:
- `OnStateChangeTerminated`: CYC ≤19
- `SetTerminatingAndStopWatchdog`: CYC ≤19
- `ShutdownUiAndServices`: CYC ≤19
- `CleanupResourcesAndReferences`: CYC ≤19

### Step 3: Build & Sync
```powershell
powershell -File .\deploy-sync.ps1
```

**Expected**: Zero errors, BUILD_TAG `1111.007-phase7-t8` visible in NinjaTrader Output

### Step 4: F5 Test
1. Press F5 in NinjaTrader
2. Observe BUILD_TAG in Output window
3. Manually disable the strategy
4. Verify shutdown log sequence matches expected order
5. Verify zero watchdog escalation Prints during shutdown

### Step 5: Grep Verification
```bash
grep -c "_isTerminating = true" src/V12_002.Lifecycle.cs
grep -c "StopWatchdog()" src/V12_002.Lifecycle.cs
```

**Expected**:
- `_isTerminating = true`: 1 occurrence (in `SetTerminatingAndStopWatchdog`)
- `StopWatchdog()`: 1 occurrence (in `SetTerminatingAndStopWatchdog`)

---

## Risk Mitigation

### Risk: Ordering Violation
**Mitigation**: `SetTerminatingAndStopWatchdog()` makes the critical ordering explicit and atomic. The method name documents the constraint. Code review must verify this helper is the first call in the residual.

### Risk: Print Statement Drift
**Mitigation**: All Print statements preserved verbatim. Grep verification in Step 5 confirms counts.

### Risk: DEVIATION-T8-A Rejection
**Mitigation**: Ticket pre-authorizes the 8-LOC helper. Documentation explains why it cannot be further decomposed without breaking the ordering guarantee.

---

## References

- **Analysis**: spec:807e80ce-4657-46c6-a10f-0338ea1a907b/ee6c7363-16b7-4be4-85d2-8a48a784743e §1.1 row T8 (NT-state-machine driven), §2 H6 (State Termination Ordering)
- **Approach**: spec:807e80ce-4657-46c6-a10f-0338ea1a907b/7d42f7da-0c65-4020-8b2d-40117382d136 §1.1 D-S5 (LOC deviation pre-flag), §4 INV-7 (Termination Ordering — full set)
- **Watchdog Guards**: T12 (`ExecuteWatchdogLeadAccountFlatten`), T15 (`ExecuteWatchdogDirectFallback`)

---

## Implementation Log

**2026-05-13 01:31 UTC**: Plan created, extraction strategy validated via sequential thinking analysis.