# V12 Event Lifecycle Dispatcher -- Phase 4
## BUILD TAG: Build-983-Phase4-Dispatcher
## MISSION: Extract ProcessOnStateChange into 5 dedicated handlers
## BRANCH: feature/phase-4-event-lifecycle
## AUTHORED BY: P3 ARCHITECT (Antigravity)
## STATUS: AWAITING DIRECTOR APPROVAL + P4 ARENA AUDIT

---

> SUPERSEDES: Build-982-Phase2-RAII (that mission is CLOSED -- P6 PASS 2026-05-04)
> PRIOR PLAN: All RAII work is complete. Zero open items from Build-982.

---

## MANDATORY PROTOCOL CHECKS

- [x] P3 authored -- no P1 prescriptions
- [x] BROKEN/FIXED code blocks present for every site
- [x] No phantom (empty) finally blocks
- [x] No lock() usage (BANNED)
- [x] ASCII-only string literals verified
- [x] Single file target (src/V12_002.Lifecycle.cs)

---

## FORENSIC FINDING

**Target**: `src/V12_002.Lifecycle.cs`, function `ProcessOnStateChange`
- **Lines**: 44-476 (432 lines)
- **Cyclomatic Complexity**: 91 (hotspot score 252 -- top ranked)
- **Structure**: One 432-line if-else-if chain across 5 lifecycle states

**Problem**: The function is a god-function. Any engineer editing any lifecycle
state must read 400+ lines to understand context. The five states
(SetDefaults/Configure/DataLoaded/Realtime/Terminated) have zero coupling --
each branch is completely independent.

**Fix**: Extract each branch into a dedicated private handler method.
`ProcessOnStateChange` becomes a 7-line pure dispatcher.
No logic changes. No functional risk. Pure structural extraction.

---

## TARGET: ProcessOnStateChange (current -- BROKEN)

Lines 44-476 of src/V12_002.Lifecycle.cs:

```csharp
private void ProcessOnStateChange(State state)
{
    if (state == State.SetDefaults)
    {
        // ... 125 lines of property defaults ...
    }
    else if (state == State.Configure)
    {
        // ... 80 lines of collection + Photon init ...
    }
    else if (state == State.DataLoaded)
    {
        // ... 101 lines of indicator init + services ...
    }
    else if (state == State.Realtime)
    {
        // ... 46 lines of SIMA + UI startup ...
    }
    else if (state == State.Terminated)
    {
        // ... 75 lines of cleanup + teardown ...
    }
}
```

## TARGET: ProcessOnStateChange (FIXED -- pure dispatcher)

```csharp
private void ProcessOnStateChange(State state)
{
    if      (state == State.SetDefaults) OnStateChangeSetDefaults();
    else if (state == State.Configure)   OnStateChangeConfigure();
    else if (state == State.DataLoaded)  OnStateChangeDataLoaded();
    else if (state == State.Realtime)    OnStateChangeRealtime();
    else if (state == State.Terminated)  OnStateChangeTerminated();
}
```

---

## ENGINEER EXECUTION INSTRUCTIONS

### Overview

Single file: `src/V12_002.Lifecycle.cs`

Current `ProcessOnStateChange` spans lines 44-476.
The 5 if-else branches must each be extracted into their own private method.
The extraction is a CUT operation -- no code is modified, only relocated.

---

### Step 1 -- Extract OnStateChangeSetDefaults

**Cut** the body of the `if (state == State.SetDefaults)` branch.
Lines 46-171 (the opening `{` is line 46, closing `}` is line 171).

**Create** this new private method immediately after line 503 (`#endregion`):

```csharp
private void OnStateChangeSetDefaults()
{
    _configureComplete = false;
    _dataLoadedComplete = false;
    Interlocked.Exchange(ref _startupReadinessLogEmitted, 0);
    ResetTelemetry();
    Description = "Universal OR Strategy V12.12 - Build " + BUILD_TAG;
    Name = "V12_002";
    Calculate = Calculate.OnPriceChange;
    EntriesPerDirection = 10;
    EntryHandling = EntryHandling.UniqueEntries;
    IsExitOnSessionCloseStrategy = false;
    IsFillLimitOnTouch = false;
    MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
    OrderFillResolution = OrderFillResolution.Standard;
    StartBehavior = StartBehavior.ImmediatelySubmit;
    TimeInForce = TimeInForce.Gtc;
    StopTargetHandling = StopTargetHandling.PerEntryExecution;
    IsUnmanaged = true;

    // Session defaults (NY Open)
    SessionStart = DateTime.Parse("09:30");
    SessionEnd = DateTime.Parse("16:00");
    ORTimeframe = ORTimeframeType.Minutes_5;
    SelectedTimeZone = "Eastern";

    // Risk defaults
    RiskPerTrade = 200;
    StopThresholdPoints = 5.0;
    SlippageCushionPoints = 1.0;
    MESMinimum = 1;
    MESMaximum = 30;
    MGCMinimum = 1;
    MGCMaximum = 15;

    // Stop defaults
    StopMultiplier = 0.5;
    MinimumStop = 4.0;
    MaximumStop = 15.0;
    IpcPort = 5001;
    IpcExposeSensitiveFleetIdentity = false;

    // V12.1101E: 5-target system
    Target1Value = 1.0;
    Target2Value = 0.5;
    Target3Value = 1.0;
    Target4Value = 1.5;
    Target5Value = 2.0;
    ConfiguredTargetCount = 5;
    T1Type = TargetMode.Points;
    T2Type = TargetMode.ATR;
    T3Type = TargetMode.ATR;
    T4Type = TargetMode.ATR;
    T5Type = TargetMode.Runner;

    // Trailing stop defaults
    BreakEvenTriggerPoints = 2.0;
    BreakEvenOffsetTicks = 2;
    Trail1TriggerPoints = 3.0;
    Trail1DistancePoints = 2.0;
    Trail2TriggerPoints = 4.0;
    Trail2DistancePoints = 1.5;
    Trail3TriggerPoints = 5.0;
    Trail3DistancePoints = 1.0;

    // Display
    ShowMidLine = true;
    BoxOpacity = 20;

    // RMA defaults
    RMAEnabled = true;
    RMAATRPeriod = 14;
    RMAStopATRMultiplier = 1.1;

    // TREND defaults
    TRENDEnabled = true;
    TRENDEntry1ATRMultiplier = 1.1;
    TRENDEntry2ATRMultiplier = 1.1;

    // RETEST defaults
    RetestEnabled = true;
    RetestATRMultiplier = 1.1;

    // MOMO defaults
    MOMOEnabled = true;
    MOMOStopPoints = 0.5;

    // FFMA defaults
    FFMAEnabled = true;
    FFMAEMADistance = 10.0;
    FFMARSIOverbought = 80;
    FFMARSIOversold = 20;

    // V12 SIMA defaults
    AccountPrefix = "Apex";
    EnableSIMA = false;
    ReaperAuditEnabled = true;
    ReaperIntervalMs = 1000;
    NakedPositionGraceSec = 5;
    EnablePathB = false;
    AutoFlattenDesync = false;
    RepairTickFence = 8;
    FleetParityMultiplier = 1;
    ShadowModeEnabled = false;
    PathBStopPoints = 10.0;
    PathBTargetPoints = 15.0;
    ChaseIfTouchPoints = "0";

    // Apex Compliance defaults
    EnableComplianceHub = true;
    ConsistencyThreshold = 30;
    EnableConsistencyLock = false;
    MaxDailyProfitCap = 1500;
    PayoutMinTradingDays = 10;
    PayoutMinProfit = 2600;
    TrailingDrawdownLimit = 2500;

    // RMA Intelligence defaults (Phase 9.2)
    RmaIntelligenceEnabled = false;
    RmaProximityTicks = 2;
    RmaCancellationTicks = 4;
    RmaMaxProbeCount = 3;
    RmaExhaustionEnabled = false;
    EnablePhotonAffinityBind = false;
    CpuAffinityMask = 0;
}
```

**Verify Step 1**: The SetDefaults branch in ProcessOnStateChange is now
a single-line call: `if (state == State.SetDefaults) OnStateChangeSetDefaults();`

---

### Step 2 -- Extract OnStateChangeConfigure

**Cut** lines 172-252 (Configure branch body).

**Create** immediately after OnStateChangeSetDefaults:

```csharp
private void OnStateChangeConfigure()
{
    _configureComplete = false;
    _dataLoadedComplete = false;

    activePositions = new ConcurrentDictionary<string, PositionInfo>(2, 4);
    entryOrders = new ConcurrentDictionary<string, Order>(2, 4);
    stopOrders = new ConcurrentDictionary<string, Order>(2, 4);
    target1Orders = new ConcurrentDictionary<string, Order>(2, 4);
    target2Orders = new ConcurrentDictionary<string, Order>(2, 4);
    target3Orders = new ConcurrentDictionary<string, Order>(2, 4);
    target4Orders = new ConcurrentDictionary<string, Order>(2, 4);
    target5Orders = new ConcurrentDictionary<string, Order>(2, 4);
    linkedTRENDEntries = new ConcurrentDictionary<string, string>(2, 4);
    pendingStopReplacements = new ConcurrentDictionary<string, PendingStopReplacement>(2, 4);
    ipcCommandQueue = new ConcurrentQueue<string>();
    connectedClients = new ConcurrentDictionary<int, IpcClientSession>();
    expectedPositions = new ConcurrentDictionary<string, int>(2, 20);

    // v28.0 Sovereign Photon [ADR-012 + ADR-016]
    _photonPool = new PhotonOrderPool(PhotonPoolCapacity);
    _photonDispatchRing = new SPSCRing<FleetDispatchSlot>(PhotonPoolCapacity);
    _photonSideband = new FleetDispatchSideband[PhotonPoolCapacity];
    _photonShadowSalt = unchecked((ulong)Guid.NewGuid().GetHashCode() * 0x9E3779B97F4A7C15UL);

    // Static assert: Shadow must be the last 8 bytes of FleetDispatchSlot (ADR-016)
    {
        int _slotSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FleetDispatchSlot));
        int _shadowOffset = System.Runtime.InteropServices.Marshal.OffsetOf(typeof(FleetDispatchSlot), "Shadow").ToInt32();
        if (_slotSize != 64 || _shadowOffset != 56)
        {
            throw new InvalidOperationException(string.Format(
                "FleetDispatchSlot layout invariant violated: size={0}, shadowOffset={1}; expected size=64, offset=56",
                _slotSize, _shadowOffset));
        }
    }

    // Optional MMIO mirror
    try
    {
        string _mmfName = "V12_FleetDispatch_" + System.Diagnostics.Process.GetCurrentProcess().Id.ToString() + "_" + _photonShadowSalt.ToString("X16");
        _photonMmioMirror = new MmioDispatchMirror(_mmfName, PhotonPoolCapacity, 64, _photonShadowSalt);
        Print(string.Format("[PHOTON MMIO] mirror online: {0}", _mmfName));
    }
    catch (Exception _mmioEx)
    {
        _photonMmioMirror = null;
        Print("[PHOTON MMIO] mirror unavailable (hot path unaffected): " + _mmioEx.Message);
    }

    _executionIdRing = new ExecutionIdRing(512, 1024);
    _executionIdFallbackRing = new ExecutionIdRing(512, 1024);

    // V12.1: Initialize Compliance Hub log directory
    string logsDirInit = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTrader 8", "SIMA_Logs");
    if (!System.IO.Directory.Exists(logsDirInit)) System.IO.Directory.CreateDirectory(logsDirInit);

    // MTF RMA Intelligence data series (Phase 9.2)
    AddDataSeries(BarsPeriodType.Minute, 5);
    AddDataSeries(BarsPeriodType.Minute, 10);
    AddDataSeries(BarsPeriodType.Minute, 15);

    _configureComplete = true;
}
```

---

### Step 3 -- Extract OnStateChangeDataLoaded

**Cut** lines 253-353 (DataLoaded branch body).

**Create** immediately after OnStateChangeConfigure:

```csharp
private void OnStateChangeDataLoaded()
{
    _dataLoadedComplete = false;

    tickSize = Instrument.MasterInstrument.TickSize;
    pointValue = Instrument.MasterInstrument.PointValue;
    lastKnownPrice = 0;

    string symbol = Instrument.MasterInstrument.Name;
    if (symbol.Contains("MES") || symbol.Contains("ES"))
    {
        minContracts = MESMinimum;
        maxContracts = MESMaximum;
    }
    else if (symbol.Contains("MGC") || symbol.Contains("GC"))
    {
        minContracts = MGCMinimum;
        maxContracts = MGCMaximum;
    }
    else
    {
        minContracts = 1;
        maxContracts = 20;
    }

    int persistedTargetCount = Math.Max(0, Math.Min(5, ConfiguredTargetCount));
    if (persistedTargetCount >= 1)
    {
        activeTargetCount = persistedTargetCount;
    }
    else
    {
        int loadedTargetCount = (Target1Value > 0 ? 1 : 0)
                              + (Target2Value > 0 ? 1 : 0)
                              + (Target3Value > 0 ? 1 : 0)
                              + (Target4Value > 0 ? 1 : 0)
                              + (Target5Value > 0 ? 1 : 0);
        activeTargetCount = Math.Max(1, Math.Min(5, loadedTargetCount));
        ConfiguredTargetCount = activeTargetCount;
    }

    atrIndicator = this.ATR(BarsArray[1], RMAATRPeriod);
    ema9  = this.EMA(9);
    ema15 = this.EMA(15);
    ema30 = this.EMA(30);
    ema65 = this.EMA(65);
    ema200 = this.EMA(200);
    rsiIndicator = this.RSI(14, 3);

    Print(string.Format("EMA INIT DEBUG: ema9.Period={0} ema15.Period={1}", ema9.Period, ema15.Period));

    ResetOR();

    Print(string.Format("UniversalORStrategy {0} | {1} | Tick: {2} | PV: ${3}", BUILD_TAG, symbol, tickSize, pointValue));
    Print(string.Format("Session: {0} - {1} {2} | OR: {3} min",
        SessionStart.ToString("HH:mm"), SessionEnd.ToString("HH:mm"), SelectedTimeZone, (int)ORTimeframe));
    Print(string.Format("Targets: T1={0}({1}) T2={2}({3}) T3={4}({5}) T4={6}({7}) T5={8}({9}) | Stop={10}xOR",
        Target1Value, T1Type, Target2Value, T2Type, Target3Value, T3Type, Target4Value, T4Type, Target5Value, T5Type, StopMultiplier));
    Print(string.Format("RMA: Enabled={0} ATR({1}) Stop={2}xATR", RMAEnabled, RMAATRPeriod, RMAStopATRMultiplier));
    Print(string.Format("{0} REPAIRED: Definitive Chart-Click Fix + Logic Refresh", BUILD_TAG));
    Print(string.Format("TREND: Enabled={0} E1Stop={1}xATR E2Trail={2}xATR", TRENDEnabled, TRENDEntry1ATRMultiplier, TRENDEntry2ATRMultiplier));
    Print(string.Format("FFMA: Enabled={0} Distance={1}pt RSI={2}/{3}", FFMAEnabled, FFMAEMADistance, FFMARSIOversold, FFMARSIOverbought));
    Print(string.Format("V12 SIMA: {0} | AccountPrefix: \"{1}\"", EnableSIMA ? "ENABLED - Fleet mode" : "DISABLED - Single account", AccountPrefix));

    string logsDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTrader 8", "SIMA_Logs");
    complianceLogPath   = System.IO.Path.Combine(logsDir, $"ApexPerformance_{symbol}.json");
    dailySummaryCsvPath = System.IO.Path.Combine(logsDir, $"DailySummaries_{symbol}.csv");
    EnsureDailySummaryCsv();

    ExecuteRiskLogicAudit();

    _dataLoadedComplete = true;

    _stickyStatePath = System.IO.Path.Combine(logsDir, string.Format("StickyState_{0}.v12state", symbol));
    bool stickyLoaded = LoadStickyState();
    if (stickyLoaded)
        Print("[STICKY] Persisted state hydrated -- GET_LAYOUT will serve last-synced config");

    StartIpcServer();
    TouchStrategyHeartbeat();
    PublishUiSnapshot();
}
```

---

### Step 4 -- Extract OnStateChangeRealtime

**Cut** lines 354-399 (Realtime branch body).

**Create** immediately after OnStateChangeDataLoaded:

```csharp
private void OnStateChangeRealtime()
{
    Print("+--------------------------------------------------------------+");
    Print("|          [OK] BMad HARDENED DEPLOYMENT PROTOCOL ACTIVE       |");
    Print(string.Format("|          Build: {0,-10} |  Sync: ONE SOURCE OF TRUTH    |", BUILD_TAG));
    Print("+--------------------------------------------------------------+");
    TouchStrategyHeartbeat();
    PublishUiSnapshot();
    StartWatchdog();

    if (EnableSIMA)
    {
        Enqueue(ctx =>
        {
            ctx.EnumerateApexAccounts();
            if (ctx.ReaperAuditEnabled)
                ctx.StartReaperAudit();
        });
    }

    if (ChartControl != null)
    {
        ChartControl.Dispatcher.InvokeAsync(() =>
        {
            if (_isTerminating) return;
            AttachHotkeys();
            AttachChartClickHandler();
        }, System.Windows.Threading.DispatcherPriority.Normal);

        ChartControl.Dispatcher.InvokeAsync(() =>
        {
            if (_isTerminating) return;
            CreatePanel();
            StartPanelRefresh();
            Print("REALTIME - Hotkeys: L=Long, S=Short, Shift+Click=RMA, F=Flatten");
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }
}
```

---

### Step 5 -- Extract OnStateChangeTerminated

**Cut** lines 400-475 (Terminated branch body).

**Create** immediately after OnStateChangeRealtime:

```csharp
private void OnStateChangeTerminated()
{
    _isTerminating = true;
    StopWatchdog();

    _configureComplete = false;
    _dataLoadedComplete = false;
    Interlocked.Exchange(ref _startupReadinessLogEmitted, 0);

    StopPanelRefresh();

    if (ChartControl != null)
    {
        ChartControl.Dispatcher.InvokeAsync(() =>
        {
            DetachHotkeys();
            DetachChartClickHandler();
            DestroyPanel();
        });
    }

    // [BUILD 948] GTC Cancel Sweep
    CancelAllV12GtcOrders(false);
    DrainQueuesForShutdown();
    EmitMetricsSummary();

    StopIpcServer();
    StopReaperAudit();
    UnsubscribeFromFleetAccounts();

    // v28.0 MMIO mirror teardown
    if (_photonMmioMirror != null)
    {
        try { _photonMmioMirror.Dispose(); } catch { }
        _photonMmioMirror = null;
    }

    SignalBroadcaster.ClearAllSubscribers();
    _simaToggleSem?.Dispose();

    activePositions?.Clear();
    entryOrders?.Clear();
    stopOrders?.Clear();
    target1Orders?.Clear();
    target2Orders?.Clear();
    target3Orders?.Clear();
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

---

### Step 6 -- Replace ProcessOnStateChange with dispatcher

Replace the entire body of `ProcessOnStateChange` (lines 44-476) with:

```csharp
private void ProcessOnStateChange(State state)
{
    if      (state == State.SetDefaults) OnStateChangeSetDefaults();
    else if (state == State.Configure)   OnStateChangeConfigure();
    else if (state == State.DataLoaded)  OnStateChangeDataLoaded();
    else if (state == State.Realtime)    OnStateChangeRealtime();
    else if (state == State.Terminated)  OnStateChangeTerminated();
}
```

---

## VERIFY CRITERIA (mandatory before handoff)

```powershell
# 1. ProcessOnStateChange must now be 7 lines
Select-String -Path src\V12_002.Lifecycle.cs -Pattern "private void ProcessOnStateChange"

# 2. All 5 handlers must exist
Select-String -Path src\V12_002.Lifecycle.cs -Pattern "private void OnStateChange"
# Expected: 5 matches

# 3. No lock() introduced
Select-String -Path src\V12_002.Lifecycle.cs -Pattern "lock\s*\(" | Where-Object { $_ -notmatch "^\s*//" }
# Expected: 0 matches

# 4. No empty try/finally introduced
Select-String -Path src\V12_002.Lifecycle.cs -Pattern "try { }"
# Expected: 0 matches

# 5. File still compiles -- all referenced members remain in scope (visual scan)
```

---

## ANTI-PATTERNS (ENGINEER MUST AVOID)

1. DO NOT modify any logic -- this is EXTRACTION ONLY.
2. DO NOT reorder lines within each handler.
3. DO NOT change variable names or access modifiers.
4. DO NOT introduce lock() -- BANNED per V12 DNA.
5. DO NOT add try/finally where none existed -- they are NOT needed here.
6. NEVER use emoji, curly quotes, or em-dashes in any Print() string.

---

## DIRECTOR'S HANDOFF BLOCK FOR CODEX ENGINEER

```
MISSION: Build-983-Phase4-Dispatcher -- Event Lifecycle Dispatcher Scaffold
PLAN:    docs/brain/implementation_plan.md
BRANCH:  feature/phase-4-event-lifecycle
REPO:    https://github.com/mkalhitti-cloud/universal-or-strategy/tree/feature/phase-4-event-lifecycle
SHELL:   Use PowerShell Select-String for all verify steps (not grep).

SURGICAL TARGET: src/V12_002.Lifecycle.cs (SINGLE FILE)

OPERATION: Extract ProcessOnStateChange into 5 dedicated private handlers.
This is a PURE STRUCTURAL EXTRACTION. No logic changes. No new functionality.

STEP SEQUENCE (execute in order):

1. Read the entire current src/V12_002.Lifecycle.cs into context.
   The function ProcessOnStateChange is at lines 44-476.

2. CREATE 5 new private void methods immediately after line 503 (end of #endregion):
   - private void OnStateChangeSetDefaults()  -- body = lines 46-171
   - private void OnStateChangeConfigure()    -- body = lines 172-252
   - private void OnStateChangeDataLoaded()   -- body = lines 253-353
   - private void OnStateChangeRealtime()     -- body = lines 354-399
   - private void OnStateChangeTerminated()   -- body = lines 400-475
   The exact code for each method is in the implementation plan above.

3. REPLACE the entire body of ProcessOnStateChange (lines 44-476) with:

   private void ProcessOnStateChange(State state)
   {
       if      (state == State.SetDefaults) OnStateChangeSetDefaults();
       else if (state == State.Configure)   OnStateChangeConfigure();
       else if (state == State.DataLoaded)  OnStateChangeDataLoaded();
       else if (state == State.Realtime)    OnStateChangeRealtime();
       else if (state == State.Terminated)  OnStateChangeTerminated();
   }

SELF-AUDIT BEFORE HANDOFF (MANDATORY):
   Select-String -Path src\V12_002.Lifecycle.cs -Pattern "private void OnStateChange"
   -- must return 5 matches (the 5 new handler methods)

   Select-String -Path src\V12_002.Lifecycle.cs -Pattern "lock\s*\("
   -- must return 0 matches

   Select-String -Path src\V12_002.Lifecycle.cs -Pattern "try { }"
   -- must return 0 matches

POST-EDIT SEQUENCE (MANDATORY):
   powershell -File .\deploy-sync.ps1
   Tell Director: Press F5 in NinjaTrader. Verify BUILD_TAG banner.
   Verify banner reads: Build-983-Phase4-Dispatcher
```
