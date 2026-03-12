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
        public const string BUILD_TAG = "976";  // V12.976: UI & IPC Actorization (Phase 4 Complete)

        #region Variables

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
        private int maxContracts;  // V12.1101E [B-9]: Upper bound from MESMaximum/MGCMaximum -- prevents runaway ATR sizer

        // ATR Indicator for RMA
        private ATR atrIndicator;
        private double currentATR;
        private double lastKnownPrice;  // Track current price for UI events

        // V8.2: EMA indicators for TREND trades
        private EMA ema9;
        private EMA ema15;
        // V11: Additional EMAs for Telemetry & RMA Anchors
        private EMA ema30;
        private EMA ema65;
        private EMA ema200;

        // V11: Thread-safe Value Cache for UI Telemetry
        private double _ema9Val;

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

        // V12.Hardening: Execution dedup guard -- prevents double-decrement from OnOrderUpdate + OnExecutionUpdate
        private readonly HashSet<string> processedExecutionIds = new HashSet<string>();
        private readonly Queue<string> processedExecutionIdQueue = new Queue<string>(); // For bounded pruning
        // V12.1101E [F-08]: Secondary dedup cache when broker omits executionId.
        private readonly HashSet<string> processedExecutionFallbackKeys = new HashSet<string>();
        private readonly Queue<string> processedExecutionFallbackQueue = new Queue<string>(); // For bounded pruning
        // V12.Phase7 [GAP-4]: executionDeduplicateLock removed -- C-01 unified all dedup under stateLock
        private const int MaxProcessedExecutionIds = 500;

        // V12.Phase6 [CONCURRENCY-01]: Marshal broker-thread account execution events to strategy thread
        private struct QueuedAccountExecution { public Account Account; public ExecutionEventArgs EventArgs; }
        private readonly ConcurrentQueue<QueuedAccountExecution> _accountExecutionQueue = new ConcurrentQueue<QueuedAccountExecution>();
        // V12.1101E [TM-01]: Marshal broker-thread account order events to strategy thread.
        private struct QueuedAccountOrderUpdate { public Account Account; public OrderEventArgs EventArgs; }
        private readonly ConcurrentQueue<QueuedAccountOrderUpdate> _accountOrderQueue = new ConcurrentQueue<QueuedAccountOrderUpdate>();

        // [BUILD 948] Order adoption gate -- REAPER skips audit cycles until working orders have been re-adopted.
        private volatile bool _orderAdoptionComplete = false;

        // RMA Mode tracking
        private volatile bool isRMAModeActive;
        private volatile bool isRMAButtonClicked;  // One-shot mode from button

        // V8.2: TREND Mode tracking
        private volatile bool isTRENDModeActive;
        private bool pendingTRENDEntry;  // V8.2 FIX: Flag to execute TREND in OnBarUpdate when BarsInProgress=0
        private ConcurrentDictionary<string, string> linkedTRENDEntries;  // V8.30: Thread-safe - Links E1 and E2 by group ID

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

        private DateTime lastStopManagementTime; // V8.13: Stop management throttling (100ms)

        // V8.30: Circuit breaker state - prevents cascade when too many pending replacements
        private volatile int pendingReplacementCount = 0;
        private const int CIRCUIT_BREAKER_THRESHOLD = 5;
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

        // V12.962 INLINE ACTOR (Serializing Executor) -- replaces stateLock
        // All state mutations run inside Enqueue closures; _drainToken ensures serial execution.
        // Zero locks: no monitor is ever held across a broker call (CancelOrder/SubmitOrder).
        private abstract class StrategyCommand { public abstract void Execute(V12_002 ctx); }
        private sealed class DelegateCommand : StrategyCommand {
            private readonly Action<V12_002> _action;
            public DelegateCommand(Action<V12_002> action) => _action = action;
            public override void Execute(V12_002 ctx) => _action?.Invoke(ctx);
        }
        private readonly ConcurrentQueue<StrategyCommand> _cmdQueue = new ConcurrentQueue<StrategyCommand>();
        private volatile int _drainToken = 0;
        protected void Enqueue(Action<V12_002> action) {
            if (action == null) return;
            _cmdQueue.Enqueue(new DelegateCommand(action));
            TryDrain();
        }
        private void TryDrain() {
            if (Interlocked.CompareExchange(ref _drainToken, 1, 0) != 0) return;
            DrainActor();
        }
        // V12.963: Non-recursive drain -- prevents stack growth from immediate broker callbacks
        // (SubmitOrder/CancelOrder can re-trigger OnExecutionUpdate -> Enqueue -> TryDrain on same stack).
        // Instead of recursing, schedule a new drain cycle via TriggerCustomEvent.
        private void DrainActor() {
            try {
                StrategyCommand cmd;
                while (_cmdQueue.TryDequeue(out cmd)) {
                    try { cmd.Execute(this); }
                    catch (Exception ex) { Print("[V12_INLINE_ACTOR] " + ex); }
                }
            }
            finally {
                Interlocked.Exchange(ref _drainToken, 0);
                if (!_cmdQueue.IsEmpty)
                    TriggerCustomEvent(o => { if (Interlocked.CompareExchange(ref _drainToken, 1, 0) == 0) DrainActor(); }, null);
            }
        }
        private ConcurrentQueue<string> ipcCommandQueue;
        // V12.2: Multi-Client Support
        private ConcurrentDictionary<int, TcpClient> connectedClients;

        // V12 SIMA: Multi-Account Execution Engine
        private Thread reaperThread;
        private volatile bool isReaperRunning;
        private volatile bool isFlattenRunning; // V12.8: Guard to pause Reaper during flatten
        private ConcurrentDictionary<string, int> expectedPositions; // Build 1102U: Key = ExpKey(AccountName) = "AccountName_Instrument.FullName" -> Expected Quantity (+ long, - short)
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
        // Build 935: Tracks accounts with reserved expectedPositions whose follower dispatch is still syncing.
        // Key = ExpKey(accountName). Used to suppress false REAPER repairs and flat-clears during submit windows.
        private readonly ConcurrentDictionary<string, byte> _dispatchSyncPendingExpKeys = new ConcurrentDictionary<string, byte>(); // [B967-FIX-02]

        // Build 936 [FIX-1]: Async fleet dispatch -- defers acct.Submit() to TriggerCustomEvent pump cycles.
        // Each enqueued request is one account's Submit payload. PumpFleetDispatch() consumes one per cycle,
        // preventing the strategy thread from blocking for the full fleet Submit window (~7s for 5 accounts).
        private readonly ConcurrentQueue<FleetDispatchRequest> _pendingFleetDispatches
            = new ConcurrentQueue<FleetDispatchRequest>();
        private volatile int _pendingFleetDispatchCount = 0;

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
        private readonly object dailySummaryLock = new object();
        private readonly object stateLock = new object();

        // [BUILD 924 - Fix C] CIT suppression flag: set true during PropagateMasterPriceMove,
        // cleared in finally block. Prevents CIT from market-firing freshly resubmitted follower
        // limit entries before the propagation sync cycle completes.
        private volatile bool _propagationActive = false;

        // Build 947: Two-phase FSM for follower entry replace (ghost-order prevention)
        private enum FollowerReplaceState { Idle, PendingCancel, Submitting }

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
    }
}
