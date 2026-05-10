// <copyright file="V12_002.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// V12.12 FLEET SYMMETRY & SAFETY HARDENING - Single-Instance Multi-Account Copy Trading Engine
// Based on UniversalORStrategyV10_3.cs (BUILD 1702)
// SIMA Architecture: One strategy instance on Master account broadcasts to all Apex accounts
//
// SAFETY: This file was auto-generated. Original V10_3 file unchanged.
//
// Key Features:
//   - Account Loop execution (Account.All iteration)
//   - IPC command distribution to multiple accounts
//   - Reaper Audit thread for position verification
//   - [SIMA] logging prefix for all multi-account operations
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;  // V8.30: Thread-safe collections
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;  // V8.30: For .Values.Contains() on ConcurrentDictionary
using System.Text;
using System.Globalization;
using System.Threading;  // V8.30: For Interlocked operations
using System.Threading.Tasks; // V12.2: For Task.Run in async operations
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;  // V11: For UniformGrid
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;  // V11: For Ellipse in header
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;
using System.Net;
using System.Net.Sockets;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        public const string BUILD_TAG = "1111.006-phase-6-complete";  // PR76 confirmed: D1 drain overflow log, D2 ExpKey null guard, D3 semaphore finally, D6 reconnect catch

        public class UILiveTargetSnapshot
        {
            public bool IsVisible;
            public double Price;
            public int RemainingContracts;
            public bool IsWorking;
        }

        public class UILivePositionSnapshot
        {
            public bool HasLivePosition;
            public string EntryName;
            public MarketPosition Direction;
            public double StopPrice;
            public UILiveTargetSnapshot[] Targets = new[]
            {
                new UILiveTargetSnapshot(),
                new UILiveTargetSnapshot(),
                new UILiveTargetSnapshot(),
                new UILiveTargetSnapshot(),
                new UILiveTargetSnapshot()
            };
        }

        public class UIComplianceSnapshot
        {
            public string AccountName;
            public double DailyProfit;
            public double TotalProfit;
            public int TradeCount;
            public int UniqueDays;
            public double MaxDrawdown;
            public double PayoutMinProfit;
            public double TrailingDrawdownLimit;
        }

        public class UIConfigSnapshot
        {
            public double Target1Value;
            public double Target2Value;
            public double Target3Value;
            public double Target4Value;
            public double Target5Value;
            public TargetMode Target1Type;
            public TargetMode Target2Type;
            public TargetMode Target3Type;
            public TargetMode Target4Type;
            public TargetMode Target5Type;
            public double StopValue;
            public double MaxRiskValue;
            public string ChaseIfTouchPoints;
        }

        public class UIStateSnapshot
        {
            public double EmaValue;
            public double AtrValue;
            public string StatusMessage;
            public long LastUpdateTicks;
            public double LastPrice;
            public MarketPosition MasterMarketPosition;
            public string Mode;
            public int TargetCount;
            public bool IsRmaModeActive;
            public bool IsTrendRmaMode;
            public bool IsRetestRmaMode;
            public int ConfigRevision;
            public double OrHigh;
            public double OrLow;
            public double OrRange;
            public double Ema9Value;
            public double Ema15Value;
            public double Ema30Value;
            public double Ema65Value;
            public double Ema200Value;
            public UIConfigSnapshot Config = new UIConfigSnapshot();
            public UIComplianceSnapshot Compliance = new UIComplianceSnapshot();
            public UILivePositionSnapshot LivePosition = new UILivePositionSnapshot();
        }

        #region Variables
        private volatile bool _isTerminating = false;

        // OR tracking
        private double sessionHigh;
        private double sessionLow;
        private double sessionMid;
        private double sessionRange;
        private bool isInORWindow;
        private bool orComplete;
        private volatile bool retestFiredThisSession;  // V12.1101E [B-2]: Latch -- prevent multiple RETEST entries per session | V12.Phase8 [F-06]: volatile for cross-thread visibility
        private DateTime orStartDateTime;
        private DateTime orEndDateTime;
        private DateTime sessionStartDateTime;
        private DateTime lastResetDate;
        private int orStartBarIndex;
        private int orEndBarIndex;

        // Instrument info
        private double tickSize;
        private double pointValue;
        private int minContracts;
        private int activeTargetCount = 1; // V12.Phase8.3: Dashboard target count (1--5). Isolated from minContracts to prevent risk floor corruption.
        private int ConfiguredTargetCount { get { return activeTargetCount; } set { activeTargetCount = value; } } // B981 Alias for legacy files
        private int maxContracts;  // V12.1101E [B-9]: Upper bound from MESMaximum/MGCMaximum -- prevents runaway ATR sizer

        // ATR Indicator for RMA
        private ATR atrIndicator;
        private double currentATR;
        // Cross-thread price cache. Strategy callbacks write it; UI/WPF readers access it atomically.
        private long _lastKnownPriceBits = BitConverter.DoubleToInt64Bits(0.0);
        private double lastKnownPrice
        {
            get { return BitConverter.Int64BitsToDouble(Interlocked.Read(ref _lastKnownPriceBits)); }
            set { Interlocked.Exchange(ref _lastKnownPriceBits, BitConverter.DoubleToInt64Bits(value)); }
        }

        // V8.2: EMA indicators for TREND trades
        private EMA ema9;
        private EMA ema15;
        // V11: Additional EMAs for Telemetry & RMA Anchors
        private EMA ema30;
        private EMA ema65;
        private EMA ema200;

        // V11: Thread-safe Value Cache for UI Telemetry
        private volatile float _ema9Val;
        private volatile UIStateSnapshot _uiSnapshot = new UIStateSnapshot();
        private long _strategyHeartbeatTicks;
        private int _uiConfigRevision = 1;

        // V8.7: RSI indicator for FFMA trades
        private RSI rsiIndicator;

        // V12.2: ATR Sizing & Risk Management (MaxRiskAmount is Properties.cs passthrough to RiskPerTrade)
        private ConcurrentDictionary<string, bool> activeFleetAccounts = new ConcurrentDictionary<string, bool>();

        // Position tracking - multi-target system
        // V8.30: Replaced Dictionary with ConcurrentDictionary for thread-safe access
        private ConcurrentDictionary<string, PositionInfo> activePositions;
        private ConcurrentDictionary<string, Order> entryOrders;
        private ConcurrentDictionary<string, Order> stopOrders;
        private ConcurrentDictionary<string, Order> target1Orders;
        private ConcurrentDictionary<string, Order> target2Orders;
        private ConcurrentDictionary<string, Order> target3Orders;  // v5.13: New T3 orders
        private ConcurrentDictionary<string, Order> target4Orders;
        private ConcurrentDictionary<string, Order> target5Orders;

        // V8.11: Track pending stop replacements to fix duplicate stop bug
        // V8.30: Replaced Dictionary with ConcurrentDictionary for thread-safe access
        private ConcurrentDictionary<string, PendingStopReplacement> pendingStopReplacements;

        // V14.2 Sovereign Photon [ADR-011]: Lock-free execution ID dedup rings
        // Zero string allocation. FNV-1a 64-bit hash with O(1) open-addressing lookup.
        // Ring capacity 512 (was 500), table capacity 1024 (load factor < 0.5).
        private ExecutionIdRing _executionIdRing;
        private ExecutionIdRing _executionIdFallbackRing;

        // V12.Phase6 [CONCURRENCY-01]: Marshal broker-thread account execution events to strategy thread
        private struct QueuedAccountExecution { public Account Account; public ExecutionEventArgs EventArgs; }
        private readonly ConcurrentQueue<QueuedAccountExecution> _accountExecutionQueue = new ConcurrentQueue<QueuedAccountExecution>();
        // V12.1101E [TM-01]: Marshal broker-thread account order events to strategy thread.
        private struct QueuedAccountOrderUpdate { public Account Account; public OrderEventArgs EventArgs; }
        private readonly ConcurrentQueue<QueuedAccountOrderUpdate> _accountOrderQueue = new ConcurrentQueue<QueuedAccountOrderUpdate>();

        // [BUILD 984] Order adoption gate -- REAPER skips audit cycles until working orders have been re-adopted.
        private volatile bool _orderAdoptionComplete = false;

        // RMA Mode tracking
        private volatile bool isRMAModeActive;
        private volatile bool isRMAButtonClicked;  // One-shot mode from button
        private volatile bool _chartHoverRedActive; // Build 1108.002: Red border gate for click-trader hover

        // V8.2: TREND Mode tracking
        private volatile bool isTRENDModeActive;
        private bool pendingTRENDEntry;  // V8.2 FIX: Flag to execute TREND in OnBarUpdate when BarsInProgress=0
        private ConcurrentDictionary<string, string> linkedTRENDEntries;  // V8.30: Thread-safe - Links E1 and E2 by group ID

        // V12 PERFORMANCE / ADR-019: Locks are BANNED. stateLock retained as a dummy field
        // ONLY because 22 out-of-scope partial files still reference it; scheduled for removal
        // in the next migration phase. Legacy CSV-header lock removed (DNA audit violation cleared).
        private readonly object stateLock = new object();

        // ADR-019: One-shot guard replacing the legacy CSV-header lock around file creation.
        // 0 = not yet ensured, 1 = header ensured (or file pre-existed). Reset to 0 on I/O failure
        // so the next caller can retry. Read/written exclusively via Interlocked.
        private int _dailySummaryHeaderEnsured = 0;

        // V8.4: RETEST Mode tracking
        private volatile bool isRetestModeActive;

        // V8.6: MOMO Mode tracking
        private volatile bool isMOMOModeActive;

        // V8.7: FFMA Mode tracking (Far From Moving Average)
        private volatile bool isFFMAModeArmed;

        // V11 Logic State
        private volatile bool isTrendRmaMode = false; // False = STD (All-in), True = RMA (9/15 Split)
        private volatile bool isRetestRmaMode = false; // V12: RETEST RMA toggle state

        // V12.2 Hybrid Sync: Logic State
        private volatile bool isTosSyncMode = false;
        private bool isLongArmed = false;
        private bool isShortArmed = false;
        private DateTime lastArmedTime = DateTime.MinValue;

        // V11: RMA Anchor Logic
        public enum RmaAnchorType { Ema30, Ema65, Ema200, OrHigh, OrLow, Manual }
        private RmaAnchorType currentRmaAnchor = RmaAnchorType.Ema65; // Default to 65
        // V12.1101E [D-02]: Removed unused V11 manual-anchor remnants (lastMnlPrice, isMnlArmed).
        private double cachedMnlPrice = 0; // Thread-safe cache
        // Build 1103: Sticky State persistence
        private string _stickyLeaderAccount;                        // Persisted leader account name
        private Dictionary<string, bool> _pendingStickyFleetToggles; // Deferred fleet toggles (applied after enumeration)

        private DateTime lastStopManagementTime; // V8.13: Stop management throttling (100ms)

        // V8.30: Circuit breaker state - prevents cascade when too many pending replacements
        private volatile int pendingReplacementCount = 0;
        private const int CIRCUIT_BREAKER_THRESHOLD = 5;
        private const int STALE_PENDING_FAST_PATH_SEC = 3;  // Build 1104.2: staleness threshold for pending stop replacements
        private volatile bool circuitBreakerActive = false;
        private long circuitBreakerActivatedTicks = 0; // V12.Phase8 [F-07]: long with Volatile barriers for cross-thread visibility
        private DateTime circuitBreakerActivatedTime
        {
            get { return new DateTime(Volatile.Read(ref circuitBreakerActivatedTicks)); }
            set { Volatile.Write(ref circuitBreakerActivatedTicks, value.Ticks); }
        }

        // V8.30: DrawORBox throttling - prevents chart update saturation
        private DateTime lastDrawORBoxTime = DateTime.MinValue;
        private const int DRAW_ORBOX_THROTTLE_MS = 200;

        // V8.30: Adaptive throttling based on tick frequency
        private int tickCountInLastSecond = 0;
        private DateTime lastTickCountReset = DateTime.MinValue;
        private int adaptiveThrottleMs = 100;


        // V9.1.8 IPC Integration
        private TcpListener ipcListener;
        private Thread ipcThread;
        private volatile bool isIpcRunning;

        // V12.962 INLINE ACTOR (Ordered Actor Thread)
        // All state mutations run inside Enqueue closures; _drainToken ensures serial execution.
        // Actor work must stay on the strategy thread even when enqueued from UI/broker/reaper threads.
        // Zero locks: no monitor is ever held across a broker call (CancelOrder/SubmitOrder).
        private abstract class StrategyCommand { public abstract void Execute(V12_002 ctx); }
        private sealed class DelegateCommand : StrategyCommand {
            private readonly Action<V12_002> _action;
            public DelegateCommand(Action<V12_002> action) => _action = action;
            public override void Execute(V12_002 ctx) => _action?.Invoke(ctx);
        }
        private readonly ConcurrentQueue<StrategyCommand> _cmdQueue = new ConcurrentQueue<StrategyCommand>();
        private volatile int _drainToken = 0;
        private volatile int _actorOwnerThreadId = 0;
        private volatile int _actorWakeScheduled = 0;
        private const int MaxBrokerCallsPerCycle = 5;
        private const int MaxActorDurationMs = 10;
        private int _actorCycleSequence = 0;
        private int _activeActorCycleId = 0;
        private int _actorBrokerCallsThisCycle = 0;
        private volatile int _actorYieldRequested = 0;
        private string _actorYieldReason = string.Empty;
        private string _actorYieldDetail = string.Empty;
        // Build 1109 [FREEZE-PROOF]: Chunked flatten queue -- one account per TriggerCustomEvent cycle.
        // Mirrors PumpFleetDispatch pattern. Prevents multi-second strategy thread freeze during fleet flatten.
        private readonly ConcurrentQueue<FlattenWorkItem> _pendingFlattenOps
            = new ConcurrentQueue<FlattenWorkItem>();

        private struct FlattenWorkItem
        {
            public Account Account;
            public bool CancelOnly;        // true = cancel orders only, no market close
            public bool ZombieSweepOnly;   // true = only cancel zombie targets (EMERGENCY_STOP_, T1_-T5_)
            public bool IsMaster;          // true = use SubmitOrderUnmanaged; false = use Account.Submit
            public string Source;          // logging tag
        }
        // V14.2 Sovereign Photon [ADR-012]: Zero-allocation fleet dispatch infrastructure
        private PhotonOrderPool _photonPool;
        private SPSCRing<FleetDispatchSlot> _photonDispatchRing;
        private MmioDispatchMirror _photonMmioMirror; // v28.0 -- optional MMIO write-through; may be null if CreateOrOpen throws
        // Diagnostic: CRC16 verification failures (defense-in-depth -- see Ring.cs notes)
        private long _photonCrcFailures = 0;
        private readonly System.Diagnostics.Stopwatch _actorCycleStopwatch = new System.Diagnostics.Stopwatch();
        private volatile bool _configureComplete = false;
        private volatile bool _dataLoadedComplete = false;
        private int _startupReadinessLogEmitted = 0;
        protected void Enqueue(Action<V12_002> action) {
            if (action == null) return;
            _cmdQueue.Enqueue(new DelegateCommand(action));
            if (IsActorThread())
                TryDrain();
            else
                ScheduleActorDrain();
        }
        private bool IsActorThread() {
            int actorThreadId = Volatile.Read(ref _actorOwnerThreadId);
            return actorThreadId != 0 && Thread.CurrentThread.ManagedThreadId == actorThreadId;
        }
        private void RefreshActorOwnerThread() {
            Interlocked.Exchange(ref _actorOwnerThreadId, Thread.CurrentThread.ManagedThreadId);
        }
        private bool EnsureStartupReady(string callbackName) {
            if (_configureComplete && _dataLoadedComplete)
                return true;

            if (Interlocked.CompareExchange(ref _startupReadinessLogEmitted, 1, 0) == 0) {
                StringBuilder missingPhases = new StringBuilder();
                if (!_configureComplete)
                    missingPhases.Append("Configure");
                if (!_dataLoadedComplete) {
                    if (missingPhases.Length > 0)
                        missingPhases.Append(", ");
                    missingPhases.Append("DataLoaded");
                }

                Print(string.Format(
                    "[BUILD 976 STARTUP GUARD] {0} skipped until initialization completes. State={1} Thread={2} Missing={3}",
                    callbackName,
                    State,
                    Thread.CurrentThread.ManagedThreadId,
                    missingPhases.ToString()));
            }

            return false;
        }
        private void ScheduleActorDrain() {
            if (Interlocked.CompareExchange(ref _actorWakeScheduled, 1, 0) != 0) return;
            try {
                TriggerCustomEvent(o => {
                    Interlocked.Exchange(ref _actorWakeScheduled, 0);
                    TryDrain();
                }, null);
            }
            catch (Exception ex) {
                Interlocked.Exchange(ref _actorWakeScheduled, 0);
                Print("[V12_INLINE_ACTOR] schedule failed: " + ex.Message);
            }
        }
        private void TryDrain() {
            if (Interlocked.CompareExchange(ref _drainToken, 1, 0) != 0) return;
            DrainActor();
        }
        private void BeginActorCycle()
        {
            _activeActorCycleId = Interlocked.Increment(ref _actorCycleSequence);
            _actorBrokerCallsThisCycle = 0;
            Interlocked.Exchange(ref _actorYieldRequested, 0);
            _actorYieldReason = string.Empty;
            _actorYieldDetail = string.Empty;
            _actorCycleStopwatch.Restart();
        }
        private string GetActorBudgetQueueState()
        {
            return string.Format(
                "actorQueue={0} repairQueue={1} flattenQueue={2} nakedStopQueue={3}",
                _cmdQueue.Count,
                _reaperRepairQueue.Count,
                _reaperFlattenQueue.Count,
                _reaperNakedStopQueue.Count);
        }
        private void RequestActorYield(string reason, string detail = null)
        {
            if (Interlocked.CompareExchange(ref _actorYieldRequested, 1, 0) != 0)
                return;

            _actorYieldReason = reason ?? string.Empty;
            _actorYieldDetail = detail ?? string.Empty;
            Print(string.Format(
                "[ACTOR_BUDGET] cycle={0} reason={1} elapsedMs={2} brokerCalls={3} remainingActorQueue={4} detail={5} state={6}",
                _activeActorCycleId,
                _actorYieldReason,
                _actorCycleStopwatch.ElapsedMilliseconds,
                _actorBrokerCallsThisCycle,
                _cmdQueue.Count,
                _actorYieldDetail,
                GetActorBudgetQueueState()));
        }
        private bool TryYieldActorForTime(string scope, string detail)
        {
            if (Volatile.Read(ref _actorYieldRequested) != 0)
                return true;

            if (_actorCycleStopwatch.ElapsedMilliseconds < MaxActorDurationMs)
                return false;

            RequestActorYield("time", string.Format("{0}:{1}", scope, detail));
            return true;
        }
        private bool TryConsumeActorBrokerCall(string scope, string detail)
        {
            int nextCall = _actorBrokerCallsThisCycle + 1;
            if (nextCall > MaxBrokerCallsPerCycle)
            {
                RequestActorYield("broker", string.Format("{0}:{1}", scope, detail));
                return false;
            }

            _actorBrokerCallsThisCycle = nextCall;
            return true;
        }
        // V12.963: Non-recursive drain -- prevents stack growth from immediate broker callbacks
        // (SubmitOrder/CancelOrder can re-trigger OnExecutionUpdate -> Enqueue -> TryDrain on same stack).
        // Instead of recursing, schedule a new drain cycle via TriggerCustomEvent.
        private void DrainActor() {
            RefreshActorOwnerThread();
            TouchStrategyHeartbeat();
            // Build 1109 [FREEZE-PROOF]: Early warning for queue saturation
            int _actorQd = _cmdQueue.Count;
            if (_actorQd > 100)
                Print("[ACTOR_WARN] Queue depth=" + _actorQd + " -- possible backlog");
            BeginActorCycle();
            try {
                StrategyCommand cmd;
                while (_cmdQueue.TryDequeue(out cmd)) {
                    try { cmd.Execute(this); }
                    catch (Exception ex) { Print("[V12_INLINE_ACTOR] " + ex); }
                    if (Volatile.Read(ref _actorYieldRequested) != 0)
                        break;
                    if (_actorCycleStopwatch.ElapsedMilliseconds >= MaxActorDurationMs)
                    {
                        RequestActorYield("time", "post-command");
                        break;
                    }
                }
            }
            finally {
                _actorCycleStopwatch.Stop();
                Interlocked.Exchange(ref _drainToken, 0);
                if (!_cmdQueue.IsEmpty)
                    ScheduleActorDrain();
            }
        }
        private sealed class IpcClientSession
        {
            public readonly int ClientId;
            public readonly TcpClient Client;
            public readonly NetworkStream Stream;
            public readonly ConcurrentQueue<byte[]> OutboundQueue = new ConcurrentQueue<byte[]>();
            public readonly SemaphoreSlim OutboundSignal = new SemaphoreSlim(0);
            public readonly CancellationTokenSource CancelSource = new CancellationTokenSource();
            public Task WriterTask;
            public int QueuedMessageCount;
            private int _closed;

            public IpcClientSession(int clientId, TcpClient client)
            {
                ClientId = clientId;
                Client = client;
                Stream = client.GetStream();
            }

            public bool IsClosed => Volatile.Read(ref _closed) != 0;

            public bool TryMarkClosed()
            {
                return Interlocked.CompareExchange(ref _closed, 1, 0) == 0;
            }
        }

        private ConcurrentQueue<string> ipcCommandQueue;
        // V12.2: Multi-Client Support
        private ConcurrentDictionary<int, IpcClientSession> connectedClients;

        // V12 SIMA: Multi-Account Execution Engine
        private System.Timers.Timer _reaperTimer;
        private System.Threading.Timer _watchdogTimer;
        private int _watchdogStage = 0; // 0=idle, 1=enqueued, 2=direct fallback fired
        private volatile bool isFlattenRunning; // V12.8: Guard to pause Reaper during flatten
        private volatile int _flattenScopeDepth = 0;
        /// <summary>
        /// [DEPRECATED for follower REAPER audit -- Build 1105] Master account audit still reads this dictionary.
        /// Follower REAPER truth is owned by FollowerBracketFSM via GetFsmExpectedPosition().
        /// Legacy follower expectedPositions writes remain transitional outside this phase.
        /// </summary>
        private ConcurrentDictionary<string, int> expectedPositions; // Build 1102U: Key = ExpKey(AccountName)
        private int simaAccountCount = 0; // Cached count of detected Apex accounts
        private DateTime lastReaperLog = DateTime.MinValue;

        // V12.Phase6 [UNSUB-TRACK]: Deterministic unsubscribe -- tracks which accounts have active event handlers
        private readonly HashSet<string> _subscribedAccountNames = new HashSet<string>();


        // V12.Phase7 [H-10]: Mutex guard for SIMA enable/disable transitions -- prevents partial state
        // if two enable/disable calls interleave (e.g. IPC toggle while UI toggle in progress).
        private readonly SemaphoreSlim _simaToggleSem = new SemaphoreSlim(1, 1);
        // V12.Audit [H-10]: Tracks a toggle that could not complete due to semaphore timeout.
        // ApplySimaState retries the pending toggle at the top of its next invocation.
        private volatile bool _simaTogglePending = false;
        private volatile int _accountOrderPumpScheduled = 0;
        private volatile int _accountOrderPumpDeferredWhileFlatten = 0;
        private volatile int _accountExecutionPumpScheduled = 0;
        private volatile int _accountExecutionPumpDeferredWhileFlatten = 0;
        // Build 935: Tracks accounts with reserved expectedPositions whose follower dispatch is still syncing.
        // Key = ExpKey(accountName). Used to suppress false REAPER repairs and flat-clears during submit windows.
        private readonly ConcurrentDictionary<string, byte> _dispatchSyncPendingExpKeys = new ConcurrentDictionary<string, byte>(); // [B967-FIX-02]
        // Build 1105: Shadow Mode -- leader-follower autonomous propagation
        private readonly ConcurrentDictionary<string, double> _leaderLastStopPrice =
            new ConcurrentDictionary<string, double>();
        private volatile bool _leaderWasInPosition = false;

        private void EnterFlattenScope()
        {
            Interlocked.Increment(ref _flattenScopeDepth);
            isFlattenRunning = true;
        }

        private void ExitFlattenScope()
        {
            int depth = Interlocked.Decrement(ref _flattenScopeDepth);
            if (depth > 0)
                return;

            Interlocked.Exchange(ref _flattenScopeDepth, 0);
            isFlattenRunning = false;
            ResumeBufferedAccountCallbackPumps();
        }

        private void ResumeBufferedAccountCallbackPumps()
        {
            ResumeAccountOrderQueuePump();
            ResumeAccountExecutionQueuePump();
        }

        // Build 936 [FIX-1]: Async fleet dispatch -- defers acct.Submit() to TriggerCustomEvent pump cycles.
        // Each enqueued request is one account's Submit payload. PumpFleetDispatch() consumes one per cycle,
        // preventing the strategy thread from blocking for the full fleet Submit window (~7s for 5 accounts).
        private readonly ConcurrentQueue<FleetDispatchRequest> _pendingFleetDispatches
            = new ConcurrentQueue<FleetDispatchRequest>();
        private volatile int _pendingFleetDispatchCount = 0;
        // D7: _dispatchInvocationCount / _dispatchPeakElapsedTicks / _dispatchTotalElapsedTicks
        // removed (Build-983). Fields were declared but never wired into EmitMetricsSummary.
        // Re-introduce if/when FleetDispatch performance telemetry is instrumented (M5).

        // REAP-01: UTC ticks captured each time expectedPositions is set to a non-zero value.
        // REAPER uses this to suppress false "Critical Desync" alerts within a 5-second grace window
        // after a fresh master entry is submitted (broker-side fill confirmation lags expectedPositions).
        private long _lastExpectedPositionSetTicks = 0;
        private const long ReaperFillGraceTicks = 5L * TimeSpan.TicksPerSecond; // 5-second grace window

        // V12.1 SIMA Internal (ReaperAuditEnabled, ReaperIntervalMs now in Properties.cs)

        // V12.1: Apex Compliance Tracking
        private ConcurrentDictionary<string, double> accountDailyProfit = new ConcurrentDictionary<string, double>();
        private ConcurrentDictionary<string, double> accountTotalProfit = new ConcurrentDictionary<string, double>();
        private string complianceLogPath;
        private DateTime lastComplianceLog = DateTime.MinValue;
        private ConcurrentDictionary<string, int> accountTradeCount = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<string, int> accountDailyTradeCount = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<string, double> accountEquityPeak = new ConcurrentDictionary<string, double>();
        private ConcurrentDictionary<string, double> accountMaxDrawdown = new ConcurrentDictionary<string, double>();
        private ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> accountTradingDays = new ConcurrentDictionary<string, ConcurrentDictionary<int, byte>>();
        private ConcurrentDictionary<string, DateTime> accountLastSummaryDate = new ConcurrentDictionary<string, DateTime>();
        private string dailySummaryCsvPath;
        private DateTime lastDailySummaryCheck = DateTime.MinValue;
        
        // [BUILD 924 - Fix C] CIT suppression flag: set true during PropagateMasterPriceMove,
        // cleared in finally block. Prevents CIT from market-firing freshly resubmitted follower
        // limit entries before the propagation sync cycle completes.
        private volatile bool _propagationActive = false;

        // Build 947: Two-phase FSM for follower entry replace (ghost-order prevention)
        private enum FollowerReplaceState { Idle, PendingCancel, Submitting, SubmitFailed }

        private class FollowerReplaceSpec
        {
            public FollowerReplaceState State;
            public string CancellingOrderId;
            public int    PendingQty;
            public double PendingPrice;
            public string AccountName;
            public string SignalName;
            public string MasterSignalName;
            public OrderAction EntryAction;    // captured from pos.Direction at spec creation
            public OrderType   EntryOrderType; // captured from fEntry.OrderType at spec creation
            public bool        IsStopType;     // true when EntryOrderType is StopMarket or StopLimit
            public string LastSubmitError;
        }

        private readonly ConcurrentDictionary<string, FollowerReplaceSpec>
            _followerReplaceSpecs = new ConcurrentDictionary<string, FollowerReplaceSpec>();

        // B957/C1: Two-phase FSM for follower TARGET order replacement (same pattern as entry replace FSM).
        // Replaces the banned Cancel+Submit anti-pattern in MoveSpecificTarget follower path.
        private class FollowerTargetReplaceSpec
        {
            public string      EntryName;
            public int         TargetNum;
            public double      NewTargetPrice;
            public int         Quantity;
            public OrderAction ExitAction;
            public Account     TargetAccount;
            public string      CancellingOrderId; // matched by order ID in OnAccountOrderUpdate
        }
        private readonly ConcurrentDictionary<string, FollowerTargetReplaceSpec>
            _followerTargetReplaceSpecs = new ConcurrentDictionary<string, FollowerTargetReplaceSpec>();

        // Build 1106: Per-mode config profile for sticky memory across mode switches.
        // Each mode (OR, RMA, RETEST, TREND, MOMO, FFMA) stores its own target/risk snapshot.
        // Snapshot on mode-out, hydrate on mode-in.
        private class ModeConfigProfile
        {
            public int TargetCount = 1;
            public double T1, T2, T3, T4, T5;
            public TargetMode T1Type, T2Type, T3Type, T4Type, T5Type;
            public double StopMult;
            public double MaxRisk;
        }

        private readonly ConcurrentDictionary<string, ModeConfigProfile> _modeProfiles
            = new ConcurrentDictionary<string, ModeConfigProfile>();

        // Phase 2: Follower Bracket FSMs (Shadow Mode)
        private readonly ConcurrentDictionary<string, FollowerBracketFSM>
            _followerBrackets = new ConcurrentDictionary<string, FollowerBracketFSM>();

        // Phase 2: Actor Mailbox for account events
        private readonly ConcurrentQueue<AccountEvent>
            _accountMailbox = new ConcurrentQueue<AccountEvent>();

        // Phase 3: O(1) lookup for FSM events
        private readonly ConcurrentDictionary<string, string>
            _orderIdToFsmKey = new ConcurrentDictionary<string, string>();

        // [BUILD 949] CIT one-shot guard: tracks keys that have already been nudged.
        // Prevents re-nudging on subsequent bars after the first limit move.
        private readonly ConcurrentDictionary<string, bool> _citNudgedKeys
            = new ConcurrentDictionary<string, bool>();

        // Build 950: Target snapshot for OCO cascade detection during stop replacement.
        private class TargetSnapshot
        {
            public int    TargetNum;     // 1-5
            public double Price;         // LimitPrice at snapshot time
            public int    Qty;           // Quantity at snapshot time
            public Order  CapturedOrder; // Order ref -- check .OrderState for cascade detection
        }

        #endregion

        #region Fleet Helpers

        private bool IsFleetAccount(Account acct)
            => acct != null && acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0;

        #endregion

        #region SIMA Method Wrappers (Legacy Compatibility)
        // Maps legacy method names to the B966 'Locked' extraction variants.
        private void AddExpectedPositionDelta(string accountName, int delta) => AddExpectedPositionDeltaLocked(accountName, delta);
        private void SetExpectedPosition(string accountName, int value) => SetExpectedPositionLocked(accountName, value);
        private void AddOrUpdateExpectedPosition(string accountName, int addValue, Func<int, int> updateExisting) => AddOrUpdateExpectedPositionLocked(accountName, addValue, updateExisting);
        private void ApplySimaState(bool enabled) => ProcessApplySimaState(enabled);
        #endregion

        #region Queue Pumps

        private void ResumeAccountOrderQueuePump()
        {
            if (!_accountOrderQueue.IsEmpty)
                try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
        }

        private void ResumeAccountExecutionQueuePump()
        {
            if (!_accountExecutionQueue.IsEmpty)
                try { TriggerCustomEvent(o => ProcessAccountExecutionQueue(), null); } catch { }
        }

        #endregion
    }
}
