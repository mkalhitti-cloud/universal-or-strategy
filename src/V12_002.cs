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

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        public const string BUILD_TAG = "952.0";  // V12.952.0: Modular pruning -- removed IPC/TOS bridge, MOMO, FFMA, OR entry modules

        #region Variables

        // OR tracking
        private double sessionHigh;
        private double sessionLow;
        private double sessionMid;
        private double sessionRange;
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
        private double _ema15Val;
        private double _ema30Val;
        private double _ema65Val;
        private double _ema200Val;
        private double _orHighVal;
        private double _orLowVal;

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

        // V11 Logic State
        private volatile bool isTrendRmaMode = false; // False = STD (All-in), True = RMA (9/15 Split)
        private volatile bool isRetestRmaMode = false; // V12: RETEST RMA toggle state

        // V12.2 Hybrid Sync: Logic State
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


        private readonly object stateLock = new object();  // V12.20: Atomic mode transitions

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
        private readonly HashSet<string> _dispatchSyncPendingExpKeys = new HashSet<string>();

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

        #region Position Info Class

        private class PositionInfo
        {
            public string SignalName;
            public MarketPosition Direction;
            public int TotalContracts;
            public int T1Contracts;   // v5.13: 20% - Fixed 1pt quick profit
            public int T2Contracts;   // v5.13: 30% - 0.5x ATR
            public int T3Contracts;   // v5.13: 30% - 1.0x ATR
            public int T4Contracts;
            public int T5Contracts;
            public int InitialTargetCount;   // Build 1102Y-V2 [U-03]: activeTargetCount snapshot at entry fill time
            public volatile int RemainingContracts; // V12.1101E [SK-08]: volatile -- written from OnOrderUpdate, OnExecutionUpdate, OnBarUpdate threads
            public double EntryPrice;
            public OrderType EntryOrderType = OrderType.Market;
            public double InitialStopPrice;
            public double CurrentStopPrice;
            public double Target1Price;  // v5.13: Fixed 1pt
            public double Target2Price;  // v5.13: 0.5x ATR
            public double Target3Price;  // v5.13: 1.0x ATR
            public double Target4Price;
            public double Target5Price;
            public bool EntryFilled;
            public bool T1Filled;
            public bool T2Filled;
            public bool T3Filled;       // v5.13: New flag
            public bool T4Filled;
            public bool T5Filled;
            public int T1FilledQuantity; // V12.1101E hardening: cumulative executed quantity for partial-fill-safe accounting
            public int T2FilledQuantity;
            public int T3FilledQuantity;
            public int T4FilledQuantity;
            public int T5FilledQuantity;
            public bool BracketSubmitted;
            public double ExtremePriceSinceEntry;
            public int CurrentTrailLevel;
            public bool IsRMATrade;  // Flag to identify RMA trades
            public bool ManualBreakevenArmed;  // Manual breakeven button clicked
            public bool ManualBreakevenTriggered;  // Manual breakeven has executed
            public int TicksSinceEntry;  // v5.13: Tick counter for frequency-based trailing

            // V8.2: TREND trade tracking
            public bool IsTRENDTrade;           // Flag for TREND trades
            public bool IsTRENDEntry1;          // True if this is the 9 EMA entry (1/3)
            public bool IsTRENDEntry2;          // True if this is the 15 EMA entry (2/3)
            public string LinkedTRENDGroup;    // Links Entry1 and Entry2 together
            public bool Entry1TrailActivated;  // V8.2: True when E1 switches from fixed stop to EMA9 trail

            // V8.4: RETEST trade tracking
            public bool IsRetestTrade;          // Flag for RETEST trades
            public bool RetestTrailActivated;   // V8.4: True when retest switches from fixed stop to 9 EMA trail

            // V12.1: SIMA Multi-Account tracking
            public Account ExecutingAccount;    // The account this position belongs to (null = Master)
            public bool IsFollower;             // True if this is a SIMA follower position

            // Build 936 [FIX-2]: Broker-level OCO group ID linking stop + all targets into a protective bracket.
            // Set at position creation using entryName hash -- deterministic within session.
            // Broker (Rithmic/Apex/Tradovate) uses this to cancel remaining orders when one fills,
            // protecting the position natively even during NT8 restarts.
            public string OcoGroupId;
        }

        private TargetMode GetTargetMode(int targetNumber)
        {
            switch (targetNumber)
            {
                case 1: return T1Type;
                case 2: return T2Type;
                case 3: return T3Type;
                case 4: return T4Type;
                case 5: return T5Type;
                default: return TargetMode.ATR;
            }
        }

        private bool IsRunnerTarget(int targetNumber)
        {
            return GetTargetMode(targetNumber) == TargetMode.Runner;
        }

        // Universal Ladder: single-arg magnitude lookup -- T(n)Value is the sole source of truth.
        private double GetConfiguredTargetMagnitude(int targetNumber)
        {
            switch (targetNumber)
            {
                case 1: return Target1Value;
                case 2: return Target2Value;
                case 3: return Target3Value;
                case 4: return Target4Value;
                case 5: return Target5Value;
                default: return 0.0;
            }
        }

        // Universal Ladder: single pricing oracle -- reads T(n)Type + Target(n)Value, no role branching.
        private double CalculateTargetPrice(MarketPosition direction, double entryPrice, int targetNumber)
        {
            TargetMode mode = GetTargetMode(targetNumber);
            if (mode == TargetMode.Runner) return Instrument.MasterInstrument.RoundToTickSize(entryPrice);

            double value = GetConfiguredTargetMagnitude(targetNumber);
            if (value <= 0)
            {
                Print($"[PRICE_GUARD] T{targetNumber} value={value:F4} is non-positive. Using MinimumStop fallback to prevent price inversion.");
                value = MinimumStop;
            }
            double offset;
            switch (mode)
            {
                case TargetMode.ATR:
                    offset = currentATR > 0 ? currentATR * value : value;
                    break;
                case TargetMode.Ticks:
                    offset = value * tickSize;
                    break;
                case TargetMode.Points:
                default:
                    offset = value;
                    break;
            }

            double rawPrice = direction == MarketPosition.Long
                ? entryPrice + offset
                : entryPrice - offset;
            return Instrument.MasterInstrument.RoundToTickSize(rawPrice);
        }

        /// <summary>
        /// Build 1102Y-V3 [LG-01]: Target Ladder Guard -- "The Staircase Rule."
        /// Iterates T1 ? T5 and ensures every rung is at least one tick FURTHER from entry
        /// than the rung before it. In low volatility the ATR-based T2 can be tighter than
        /// the fixed Scalp (T1), causing price inversion and incorrect order slotting.
        /// Call this after computing target prices and again after fill-price re-anchoring.
        /// Slots that are zero (unused/runner) are skipped.
        /// </summary>
        private void ApplyTargetLadderGuard(PositionInfo pos)
        {
            if (pos == null) return;
            bool isLong = pos.Direction == MarketPosition.Long;

            double[] prices = new double[]
            {
                pos.Target1Price, pos.Target2Price, pos.Target3Price,
                pos.Target4Price, pos.Target5Price
            };

            bool anyFixed = false;
            for (int i = 1; i < prices.Length; i++)
            {
                if (prices[i] <= 0) continue; // Skip unused/runner slots
                if (prices[i - 1] <= 0) continue; // Previous slot unused -- nothing to compare against

                double minValid = isLong
                    ? prices[i - 1] + tickSize
                    : prices[i - 1] - tickSize;

                bool inverted = isLong ? (prices[i] < minValid) : (prices[i] > minValid);
                if (inverted)
                {
                    Print(string.Format(
                        "[LADDER_GUARD] T{0}={1:F4} is inside T{2}={3:F4} for {4}. Pushing T{0} to {5:F4}.",
                        i + 1, prices[i], i, prices[i - 1], pos.SignalName, minValid));
                    prices[i] = Instrument.MasterInstrument.RoundToTickSize(minValid);
                    anyFixed = true;
                }
            }

            pos.Target1Price = prices[0];
            pos.Target2Price = prices[1];
            pos.Target3Price = prices[2];
            pos.Target4Price = prices[3];
            pos.Target5Price = prices[4];

            if (anyFixed)
                Print(string.Format("[LADDER_GUARD] Ladder corrected for {0}: T1={1:F4} T2={2:F4} T3={3:F4} T4={4:F4} T5={5:F4}",
                    pos.SignalName, pos.Target1Price, pos.Target2Price, pos.Target3Price, pos.Target4Price, pos.Target5Price));
        }

        // Universal Ladder: pure delegation -- T(n)Type dropdown drives all pricing for all trade types.
        private double CalculateTargetPriceFromPos(MarketPosition direction, double entryPrice, PositionInfo pos, int targetNumber)
        {
            return CalculateTargetPrice(direction, entryPrice, targetNumber);
        }

        private int GetTargetContracts(PositionInfo pos, int targetNumber)
        {
            switch (targetNumber)
            {
                case 1: return pos.T1Contracts;
                case 2: return pos.T2Contracts;
                case 3: return pos.T3Contracts;
                case 4: return pos.T4Contracts;
                case 5: return pos.T5Contracts;
                default: return 0;
            }
        }

        private double GetTargetPrice(PositionInfo pos, int targetNumber)
        {
            switch (targetNumber)
            {
                case 1: return pos.Target1Price;
                case 2: return pos.Target2Price;
                case 3: return pos.Target3Price;
                case 4: return pos.Target4Price;
                case 5: return pos.Target5Price;
                default: return 0.0;
            }
        }

        private bool IsTargetFilled(PositionInfo pos, int targetNumber)
        {
            switch (targetNumber)
            {
                case 1: return pos.T1Filled;
                case 2: return pos.T2Filled;
                case 3: return pos.T3Filled;
                case 4: return pos.T4Filled;
                case 5: return pos.T5Filled;
                default: return false;
            }
        }

        private void MarkTargetFilled(PositionInfo pos, int targetNumber)
        {
            switch (targetNumber)
            {
                case 1: pos.T1Filled = true; break;
                case 2: pos.T2Filled = true; break;
                case 3: pos.T3Filled = true; break;
                case 4: pos.T4Filled = true; break;
                case 5: pos.T5Filled = true; break;
            }
        }

        private int GetTargetFilledQuantity(PositionInfo pos, int targetNumber)
        {
            switch (targetNumber)
            {
                case 1: return pos.T1FilledQuantity;
                case 2: return pos.T2FilledQuantity;
                case 3: return pos.T3FilledQuantity;
                case 4: return pos.T4FilledQuantity;
                case 5: return pos.T5FilledQuantity;
                default: return 0;
            }
        }

        private void SetTargetFilledQuantity(PositionInfo pos, int targetNumber, int filledQuantity)
        {
            int safeQty = Math.Max(0, filledQuantity);
            switch (targetNumber)
            {
                case 1: pos.T1FilledQuantity = safeQty; break;
                case 2: pos.T2FilledQuantity = safeQty; break;
                case 3: pos.T3FilledQuantity = safeQty; break;
                case 4: pos.T4FilledQuantity = safeQty; break;
                case 5: pos.T5FilledQuantity = safeQty; break;
            }
        }

        // V8.11: Class to track pending stop replacements
        // V8.30: Added CreatedTime for timeout support
        private class PendingStopReplacement
        {
            public string EntryName;
            public int Quantity;
            public double StopPrice;
            public MarketPosition Direction;
            public Order OldOrder;  // Track the old order being cancelled
            public DateTime CreatedTime;  // V8.30: Timeout support - clean up stale replacements
            // Build 950: Bracket restoration -- populated before stop cancel is sent.
            public TargetSnapshot[] CapturedTargets;      // null if no Working targets at cancel time
            public bool             BracketRestorationNeeded; // true when CapturedTargets is non-null
        }

        // Build 951: Bracket-wide atomic replace spec for SIMA follower price moves.
        // All legs (Stop + Targets) share one freshOcoId. Resubmit fires only after ALL
        // pending cancels confirm (atomic bracket swap prevents "OCO ID reuse" rejection).
        private class BracketReplaceSpec
        {
            public string         EntryName;
            public string         FreshOcoId;           // V12_SYNC_ + 8-char GUID -- shared by ALL legs
            public MarketPosition Direction;
            public Account        ExecutingAccount;
            public DateTime       CreatedTime;
            // Build 951.1: Private sync root -- avoids lock(instance) anti-pattern (DeepSource CA2002).
            public readonly object SyncRoot = new object();
            // Build 951.1: Back-reference to PositionInfo -- updated to new stop price AFTER successful submit.
            public PositionInfo   PosRef;
            // Cancel tracking: OrderId set (removed as confirms arrive); count guards resubmit gate.
            public readonly HashSet<string> PendingOrderIds = new HashSet<string>();
            public int            RemainingCancels;     // Interlocked.Decrement; fires resubmit at <= 0
            public bool           GapFillDetected;      // Build 951.3: true when any leg FILLED during cancel-gap
            public bool           PositionClosed;       // Build 951.5: true when stop filled during cancel-gap (position now flat)
            // Replacement prices (updated in-place if a new move arrives while in-flight)
            public double         StopPrice;            // 0 = no active stop
            public int            StopQty;
            public readonly double[] TargetPrices = new double[5]; // [0]=T1..[4]=T5, 0 = leg absent
            public readonly int[]    TargetQtys   = new int[5];
        }

        private readonly ConcurrentDictionary<string, BracketReplaceSpec>
            _bracketReplaceSpecs = new ConcurrentDictionary<string, BracketReplaceSpec>();

        // V8.22: Thread-Safe UI Snapshot Struct
        // Decouples UI thread from Strategy thread to prevent "Collection moved" or race conditions
        public struct PositionDisplayInfo
        {
            public string TradeType;
            public string Direction;
            public double EntryPrice;
            public double StopPrice;
            public int RemainingContracts;
            public bool EntryFilled;
            public bool ManualBreakevenArmed;
            public bool ManualBreakevenTriggered;
        }

        // V12.12: Compliance snapshot for UI thread
        private struct ComplianceSnapshot
        {
            public bool Enabled;
            public bool HasAccounts;
            public string AccountName;
            public int TradeCount;
            public int UniqueDays;
            public double MaxDrawdown;
            public string ConsistencyText;
            public int ConsistencySeverity;
            public string PayoutText;
            public int PayoutSeverity;
            public string DrawdownText;
            public int DrawdownSeverity;
        }

        #endregion

        // V12.46: Enums, Properties, and TimeZoneConverter moved to Properties.cs

        #region OnStateChange

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Universal OR Strategy V12.12 - Build " + BUILD_TAG;
                Name = "V12_002";
                Calculate = Calculate.OnPriceChange;  // CRITICAL FIX: Updates on every price tick for real-time trailing
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
                ReducedRiskPerTrade = 200; // deprecated -- hidden in UI (RISK-01)
                StopThresholdPoints = 5.0;
                SlippageCushionPoints = 1.0; // SLIP-01: 1pt default cushion for follower slippage
                MESMinimum = 1;
                MESMaximum = 30;
                MGCMinimum = 1;
                MGCMaximum = 15;

                // Stop defaults
                StopMultiplier = 0.5;
                MinimumStop = 4.0;  // 1102Z-A F2: raised floor from 1.0 to 4.0 for current volatility
                MaximumStop = 15.0;  // V8.31: Increased from 8.0
                // V12.1101E: 5-target system with configurable runner selection
                Target1Value = 1.0;
                Target2Value = 0.5;
                Target3Value = 1.0;
                Target4Value = 1.5;
                Target5Value = 2.0;
                T1Type = TargetMode.Points;
                T2Type = TargetMode.ATR;
                T3Type = TargetMode.ATR;
                T4Type = TargetMode.ATR;
                T5Type = TargetMode.Runner;

                // Trailing stop defaults
                BreakEvenTriggerPoints = 2.0;
                BreakEvenOffsetTicks = 2;              // BE stop offset in ticks (0 = exact entry)
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

                // V8.2: TREND defaults (V8.31: E1 now uses ATR from live EMA9)
                TRENDEnabled = true;
                TRENDEntry1ATRMultiplier = 1.1;   // V8.31: 1.1x ATR stop from live 9 EMA (was fixed 2pt)
                TRENDEntry2ATRMultiplier = 1.1;   // 1.1x ATR trailing for 15 EMA entry

                // V8.4: RETEST defaults
                RetestEnabled = true;
                RetestATRMultiplier = 1.1;        // 1.1x ATR for both stop and trail

                // V12 SIMA defaults
                AccountPrefix = "Apex";
                EnableSIMA = false; // SAFETY: Default to OFF
                ReaperAuditEnabled = true;
                ReaperIntervalMs = 1000;          // 1 second audit cycle
                NakedPositionGraceSec = 3;        // GHOST-FIX-2 [922Z]: 3s grace before emergency stop on naked position
                EnablePathB = false;
                AutoFlattenDesync = false;
                RepairTickFence = 8;
                FleetParityMultiplier = 1; // V12.Phase8.7 [PARITY-01]: Set to 10 for ES?MES fleet parity
                PathBStopPoints = 10.0;
                PathBTargetPoints = 15.0;
                ChaseIfTouchPoints = "0";

                // Apex Compliance defaults
                EnableComplianceHub = true;
                ConsistencyThreshold = 30;
                EnableConsistencyLock = false;
                MaxDailyProfitCap = 1500; // Default $1500 cap for consistency
                PayoutMinTradingDays = 10;
                PayoutMinProfit = 2600; // Common Apex 50K payout threshold (adjust per account)
                TrailingDrawdownLimit = 2500; // Common Apex 50K trailing DD
                // RMA Intelligence defaults (Phase 9.2)
                RmaIntelligenceEnabled = false; // Default to isolated/OFF
                RmaExhaustionAtrMultiplier = 2.0;
                RmaStretchedCandleMultiplier = 1.0;
                RmaFreshCandleBufferAtr = 1.0;
                RmaProximityTicks = 2;
                RmaCancellationTicks = 4;
                RmaUseMtfConfluence = true;
            }
            else if (State == State.Configure)
            {
                // V8.30: Initialize thread-safe collections
                // ConcurrentDictionary(concurrencyLevel, initialCapacity)
                activePositions = new ConcurrentDictionary<string, PositionInfo>(2, 4);
                entryOrders = new ConcurrentDictionary<string, Order>(2, 4);
                stopOrders = new ConcurrentDictionary<string, Order>(2, 4);
                target1Orders = new ConcurrentDictionary<string, Order>(2, 4);
                target2Orders = new ConcurrentDictionary<string, Order>(2, 4);
                target3Orders = new ConcurrentDictionary<string, Order>(2, 4);  // v5.13
                target4Orders = new ConcurrentDictionary<string, Order>(2, 4);
                target5Orders = new ConcurrentDictionary<string, Order>(2, 4);

                // V8.2: TREND linked entries tracking
                // V8.30: Thread-safe dictionary
                linkedTRENDEntries = new ConcurrentDictionary<string, string>(2, 4);

                // V8.11: Initialize pending stop replacements tracking
                // V8.30: Thread-safe dictionary
                pendingStopReplacements = new ConcurrentDictionary<string, PendingStopReplacement>(2, 4);


                // V12 SIMA: Initialize expected positions tracking
                expectedPositions = new ConcurrentDictionary<string, int>(2, 20); // Up to 20 accounts

                // V12.1: Initialize Compliance Hub -- create log directory early (idempotent).
                // Build 935 [Fix-2/3]: Symbol-specific log paths and LogicAudit moved to DataLoaded.
                string logsDirInit = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTrader 8", "SIMA_Logs");
                if (!System.IO.Directory.Exists(logsDirInit)) System.IO.Directory.CreateDirectory(logsDirInit);

                // Add data series for MTF RMA Intelligence (Phase 9.2)
                AddDataSeries(BarsPeriodType.Minute, 5);  // Index 1 (Primary for ATR)
                AddDataSeries(BarsPeriodType.Minute, 10); // Index 2
                AddDataSeries(BarsPeriodType.Minute, 15); // Index 3

            }
            else if (State == State.DataLoaded)
            {
                tickSize = Instrument.MasterInstrument.TickSize;
                pointValue = Instrument.MasterInstrument.PointValue;
                lastKnownPrice = 0; // V11 FIX: Reset price on load to prevent stale data (e.g. MES->MGC switch)

                string symbol = Instrument.MasterInstrument.Name;
                if (symbol.Contains("MES") || symbol.Contains("ES"))
                {
                    minContracts = MESMinimum;
                    maxContracts = MESMaximum; // V12.1101E [B-9]: Upper bound for ATR sizer
                }
                else if (symbol.Contains("MGC") || symbol.Contains("GC"))
                {
                    minContracts = MGCMinimum;
                    maxContracts = MGCMaximum; // V12.1101E [B-9]
                }
                else
                {
                    minContracts = 1;
                    maxContracts = 20; // V12.1101E [B-9]: Conservative default for unknown instruments
                }

                // Universal Ladder: derive activeTargetCount from non-zero Target values at load time.
                int loadedTargetCount = (Target1Value > 0 ? 1 : 0)
                                      + (Target2Value > 0 ? 1 : 0)
                                      + (Target3Value > 0 ? 1 : 0)
                                      + (Target4Value > 0 ? 1 : 0)
                                      + (Target5Value > 0 ? 1 : 0);
                activeTargetCount = Math.Max(1, Math.Min(5, loadedTargetCount));

                // Initialize ATR indicator on 5-min bars (BarsArray[1])
                atrIndicator = this.ATR(BarsArray[1], RMAATRPeriod);

                // V8.2: Initialize EMA indicators for TREND trades
                // Using simple form - default is primary bars series
                ema9 = this.EMA(9);
                ema15 = this.EMA(15);
                // V11: Telemetry & Multi-Anchor EMAs
                ema30 = this.EMA(30);
                ema65 = this.EMA(65);
                ema200 = this.EMA(200);
                
                // V8.2 DEBUG: Verify EMA periods are correct
                Print(string.Format("EMA INIT DEBUG: ema9.Period={0} ema15.Period={1}", ema9.Period, ema15.Period));

                ResetOR();

                Print(string.Format("UniversalORStrategy V12.14 | {0} | Tick: {1} | PV: ${2}", symbol, tickSize, pointValue));
                Print(string.Format("Session: {0} - {1} {2} | OR: {3} min",
                    SessionStart.ToString("HH:mm"), SessionEnd.ToString("HH:mm"), SelectedTimeZone, (int)ORTimeframe));
                Print(string.Format("Targets: T1={0}({1}) T2={2}({3}) T3={4}({5}) T4={6}({7}) T5={8}({9}) | Stop={10}xOR",
                    Target1Value, T1Type, Target2Value, T2Type, Target3Value, T3Type, Target4Value, T4Type, Target5Value, T5Type, StopMultiplier));
                Print(string.Format("RMA: Enabled={0} ATR({1}) Stop={2}xATR",
                    RMAEnabled, RMAATRPeriod, RMAStopATRMultiplier));
                Print("V12.9 REPAIRED: Definitive Chart-Click Fix + Logic Refresh");
                Print(string.Format("TREND: Enabled={0} E1Stop={1}xATR E2Trail={2}xATR", TRENDEnabled, TRENDEntry1ATRMultiplier, TRENDEntry2ATRMultiplier));
                Print(string.Format("V12 SIMA: {0} | AccountPrefix: \"{1}\"", EnableSIMA ? "ENABLED - Fleet mode" : "DISABLED - Single account", AccountPrefix));

                // Build 935 [Fix-2]: Symbol-specific log paths prevent file-lock collisions
                // when MES and MCL instances run concurrently on the same machine.
                string logsDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTrader 8", "SIMA_Logs");
                complianceLogPath   = System.IO.Path.Combine(logsDir, $"ApexPerformance_{symbol}.json");
                dailySummaryCsvPath = System.IO.Path.Combine(logsDir, $"DailySummaries_{symbol}.csv");
                EnsureDailySummaryCsv();

                // Build 935 [Fix-3]: Run Risk Logic Audit here (DataLoaded) so instrument properties
                // (tickSize, pointValue, minContracts, maxContracts) are populated before audit runs.
                ExecuteRiskLogicAudit();

            }
            else if (State == State.Realtime)
            {
                Print("+--------------------------------------------------------------+");
                Print("|          [OK] BMad HARDENED DEPLOYMENT PROTOCOL ACTIVE       |");
                Print(string.Format("|          Build: {0,-10} |  Sync: ONE SOURCE OF TRUTH    |", BUILD_TAG));
                Print("+--------------------------------------------------------------+");

                if (EnableSIMA)
                {
                    EnumerateApexAccounts();
                    if (ReaperAuditEnabled)
                        StartReaperAudit();
                }

                // V10.3: Subscribe to external signals for multi-chart sync
                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(() =>
                    {
                        AttachHotkeys();
                        AttachChartClickHandler();
                        Print("REALTIME - Hotkeys: L=Long, S=Short, Shift+Click=RMA, F=Flatten");
                    });
                }
            }
            else if (State == State.Terminated)
            {
                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(() =>
                    {
                        DetachHotkeys();
                        DetachChartClickHandler();
                    });
                }

                // [BUILD 948] GTC Cancel Sweep -- cancel all tracked/broker V12 orders before teardown.
                // Must run while dicts are still populated and accounts still subscribed.
                // force=true: hard terminate, cancel regardless of open positions.
                CancelAllV12GtcOrders(true);

                // V12 SIMA: Stop Reaper audit thread
                StopReaperAudit();
                
                // V12.7: Always unsubscribe from account updates (subscribed for fleet bracket management)
                // V12.1101E [A-4]: Use shared UnsubscribeFromFleetAccounts() -- unconditional (no EnableSIMA guard)
                // to handle cases where flag was toggled OFF mid-session while handlers were still subscribed.
                UnsubscribeFromFleetAccounts();
                
                // V12.Phase7 [C-08]: Clear ALL static SignalBroadcaster event handlers on termination.
                // Static events survive instance disposal -- without this, dead instance handlers accumulate
                // and fire into garbage-collected strategy contexts on reload, causing phantom order submissions.
                SignalBroadcaster.ClearAllSubscribers();

                // V12.Phase7 [GAP-4]: Dispose SIMA toggle semaphore to release OS handle.
                _simaToggleSem?.Dispose();

                // Clear references
                activePositions?.Clear();
                entryOrders?.Clear();
                stopOrders?.Clear();
                target1Orders?.Clear();
                target2Orders?.Clear();
                target3Orders?.Clear();  // v5.13
                target4Orders?.Clear();
                target5Orders?.Clear();
                accountDailyProfit?.Clear();
                accountTotalProfit?.Clear();
                accountTradeCount?.Clear();
                accountDailyTradeCount?.Clear();
                accountEquityPeak?.Clear();
                accountMaxDrawdown?.Clear();
                accountTradingDays?.Clear();
                accountLastSummaryDate?.Clear();

            }
        }

        #endregion

        #region OnConnectionStatusUpdate - Build 948: Mid-session re-adoption on Rithmic reconnect

        protected override void OnConnectionStatusUpdate(ConnectionStatusEventArgs connectionStatusUpdate)
        {
            base.OnConnectionStatusUpdate(connectionStatusUpdate);

            if (!EnableSIMA || State != State.Realtime) return;

            ConnectionStatus status = connectionStatusUpdate.Status;

            if (status == ConnectionStatus.Disconnecting || status == ConnectionStatus.ConnectionLost)
            {
                // Gate REAPER until re-adoption completes after reconnect
                _orderAdoptionComplete = false;
                Print("[BUILD 948] Connection lost -- order adoption gate reset, REAPER paused.");
            }
            else if (status == ConnectionStatus.Connected)
            {
                // Re-adopt working orders after reconnect; runs on strategy thread via TriggerCustomEvent
                Print("[BUILD 948] Reconnected -- scheduling working order re-adoption.");
                try { TriggerCustomEvent(o => HydrateWorkingOrdersFromBroker(), null); } catch { }
            }
        }

        #endregion

        #region OnMarketData - V10.1: Process IPC on every tick for real-time responsiveness

        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            // Only process on primary instrument
            if (marketDataUpdate.MarketDataType == MarketDataType.Last)
            {
                // Update last known price for real-time tracking
                lastKnownPrice = marketDataUpdate.Price;
                
                // Process IPC commands immediately on every tick
                // This ensures Remote App buttons work even outside session time
                ProcessIpcCommands();
            }
        }

        #endregion

        #region OnBarUpdate

        protected override void OnBarUpdate()
        {
            // Only process primary series
            if (BarsInProgress != 0) return;
            if (CurrentBar < 5) return;

            try
            {
                // Update last known price for UI events
                lastKnownPrice = Close[0];

                // V12.12: Daily summary roll-over (throttled)
                if (EnableComplianceHub)
                {
                    DateTime nowInZone = GetComplianceNow();
                    if ((nowInZone - lastDailySummaryCheck).TotalSeconds >= 30)
                    {
                        List<Account> complianceAccounts = GetComplianceAccounts();
                        if (complianceAccounts.Count > 0)
                            MaybeFinalizeDailySummaries(nowInZone, complianceAccounts);
                    }
                }

                // V8.21: Reduced log volume - OR buildings and updates are handled via DrawORBox and UpdateDisplay

                // Process IPC Commands
                ProcessIpcCommands();

                // CIT Logic
                ManageCIT();

                // Monitor RMA Proximity and Exhaustion (Phase 9.2)
                MonitorRmaProximity();

                // V8.2 FIX: Process pending TREND entry (deferred from button click)
                if (pendingTRENDEntry)
                {
                    double trendDist   = CalculateTRENDStopDistance();
                    int trendContracts = CalculatePositionSize(trendDist);
                    ExecuteTRENDEntry(trendContracts);
                }

                // Update ATR value from 5-min bars
                if (BarsArray[1] != null && BarsArray[1].Count > RMAATRPeriod)
                {
                    currentATR = atrIndicator[0];
                }

                // V11: Update Telemetry Cache (Thread-safe for UI)
                _ema9Val = ema9[0];
                _ema15Val = ema15[0];
                _ema30Val = ema30[0];
                _ema65Val = ema65[0];
                _ema200Val = ema200[0];
                _orHighVal = sessionHigh;
                _orLowVal = sessionLow;

                // CRITICAL FIX: Convert from LOCAL timezone (PC) to selected timezone
                DateTime barTimeInZone = ConvertToSelectedTimeZone(Time[0]);
                TimeSpan currentTime = barTimeInZone.TimeOfDay;
                TimeSpan sessionStartTime = SessionStart.TimeOfDay;
                TimeSpan sessionEndTime = SessionEnd.TimeOfDay;

                // Calculate OR end time based on session start + timeframe
                TimeSpan orEndTime = sessionStartTime.Add(TimeSpan.FromMinutes((int)ORTimeframe));

                // Detect if session crosses midnight (e.g. 21:00 to 07:00)
                bool sessionCrossesMidnight = sessionEndTime < sessionStartTime;

                // V11: Draw MNL Anchor Line if active
                if (currentRmaAnchor == RmaAnchorType.Manual && cachedMnlPrice > 0)
                {
                    NinjaTrader.NinjaScript.DrawingTools.Draw.HorizontalLine(this, "MNL_Line", cachedMnlPrice, Brushes.Magenta, DashStyleHelper.Dash, 2);
                }
                else
                {
                    RemoveDrawObject("MNL_Line");
                }
                
                // Smart reset logic - only reset at NEW SESSION START
                bool shouldReset = false;

                if (sessionCrossesMidnight)
                {
                    // For overnight sessions: only reset at session start
                    if (currentTime >= sessionStartTime && currentTime < sessionStartTime.Add(TimeSpan.FromMinutes(10)))
                    {
                        if (barTimeInZone.Date != lastResetDate)
                        {
                            shouldReset = true;
                        }
                    }
                }
                else
                {
                    // For regular sessions: reset when date changes AFTER session ends
                    if (barTimeInZone.Date != lastResetDate && currentTime >= sessionStartTime)
                    {
                        shouldReset = true;
                    }
                }

                if (shouldReset)
                {
                    ResetOR();
                    lastResetDate = barTimeInZone.Date;
                    Print(string.Format("Session Reset: {0} at {1} {2}",
                        barTimeInZone.Date.ToShortDateString(), currentTime, SelectedTimeZone));
                }

                // Build OR during window
                if (currentTime > sessionStartTime && currentTime <= orEndTime)
                {
                    if (orStartDateTime == DateTime.MinValue)
                    {
                        Print(string.Format("OR WINDOW START: {0} (Bar time in {1})",
                            barTimeInZone.ToString("MM/dd/yyyy HH:mm:ss"), SelectedTimeZone));
                    }

                    sessionHigh = Math.Max(sessionHigh, High[0]);
                    sessionLow = Math.Min(sessionLow, Low[0]);
                    sessionRange = sessionHigh - sessionLow;
                    sessionMid = (sessionHigh + sessionLow) / 2.0;

                    if (orStartDateTime == DateTime.MinValue)
                    {
                        orStartDateTime = Time[0];
                        sessionStartDateTime = Time[0];
                        orStartBarIndex = CurrentBar;
                        Print(string.Format("OR Start tracked - Bar {0}", CurrentBar));
                    }
                }

                // Mark OR complete when the last bar of the window closes
                if (currentTime >= orEndTime && !orComplete && orStartBarIndex > 0)
                {
                    orComplete = true;
                    orEndDateTime = Time[0];
                    orEndBarIndex = CurrentBar;

                    Print(string.Format("OR COMPLETE at {0}: H={1:F2} L={2:F2} M={3:F2} R={4:F2}",
                        barTimeInZone.ToString("HH:mm:ss"), sessionHigh, sessionLow, sessionMid, sessionRange));
                    Print(string.Format("OR Complete: T1={0}({1}) T2={2}({3})",
                        Target1Value, T1Type, Target2Value, T2Type));

                    // V8.30: Always draw immediately when OR completes (important event)
                    DrawORBox();
                    lastDrawORBoxTime = DateTime.UtcNow;
                }

                // Update box if OR complete
                bool inActiveSession = false;
                if (sessionCrossesMidnight)
                {
                    inActiveSession = (currentTime >= sessionStartTime || currentTime <= sessionEndTime);
                }
                else
                {
                    inActiveSession = (currentTime >= sessionStartTime && currentTime <= sessionEndTime);
                }

                // V8.30: Throttle DrawORBox updates to prevent chart saturation
                if (orComplete && sessionHigh != double.MinValue && inActiveSession)
                {
                    if ((DateTime.UtcNow - lastDrawORBoxTime).TotalMilliseconds >= DRAW_ORBOX_THROTTLE_MS)
                    {
                        DrawORBox();
                        lastDrawORBoxTime = DateTime.UtcNow;
                    }
                }

                // Position sync check
                SyncPositionState();
                SymmetryGuardProcessPendingFollowerFills();

                // Manage trailing stops - NOW CALLED ON EVERY PRICE CHANGE!
                if (activePositions.Count > 0)
                {
                    ManageTrailingStops();
                    ManageCIT();
                }

                SyncPendingOrders();  // V12.30: Real-time sizing synchronization
            }
            catch (Exception ex)
            {
                Print("ERROR OnBarUpdate: " + ex.Message);
            }
        }

        #endregion

        #region Drawing - Box Instead of Rays

        private void DrawORBox()
        {
            if (sessionHigh == double.MinValue || sessionLow == double.MaxValue) return;
            if (orStartDateTime == DateTime.MinValue || orEndDateTime == DateTime.MinValue) return;

            try
            {
                int areaOpacity = BoxOpacity;

                DateTime orStartInZone = ConvertToSelectedTimeZone(orStartDateTime);
                TimeSpan sessionStartTime = SessionStart.TimeOfDay;
                TimeSpan sessionEndTime = SessionEnd.TimeOfDay;

                // Detect overnight session (e.g., 21:00 to 16:00)
                bool sessionCrossesMidnight = sessionEndTime < sessionStartTime;

                // Calculate session end date
                DateTime sessionEndInZone;
                if (sessionCrossesMidnight)
                {
                    // Overnight session: end time is NEXT day
                    sessionEndInZone = new DateTime(
                        orStartInZone.Year,
                        orStartInZone.Month,
                        orStartInZone.Day,
                        sessionEndTime.Hours,
                        sessionEndTime.Minutes,
                        sessionEndTime.Seconds
                    ).AddDays(1);  // ADD ONE DAY for overnight sessions!
                }
                else
                {
                    // Same-day session: end time is same day
                    sessionEndInZone = new DateTime(
                        orStartInZone.Year,
                        orStartInZone.Month,
                        orStartInZone.Day,
                        sessionEndTime.Hours,
                        sessionEndTime.Minutes,
                        sessionEndTime.Seconds
                    );
                }

                TimeZoneInfo targetZone;
                switch (SelectedTimeZone)
                {
                    case "Eastern":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                        break;
                    case "Central":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
                        break;
                    case "Mountain":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");
                        break;
                    case "Pacific":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                        break;
                    default:
                        targetZone = TimeZoneInfo.Local;
                        break;
                }

                DateTime boxEndTime = TimeZoneInfo.ConvertTime(sessionEndInZone, targetZone, TimeZoneInfo.Local);

                    Draw.Rectangle(this, "ORBox", false,
                    orStartDateTime, sessionHigh,
                    boxEndTime, sessionLow,
                    Brushes.DodgerBlue, Brushes.DodgerBlue, areaOpacity);

                if (ShowMidLine)
                {
                    Draw.Line(this, "ORMid", false,
                        orStartDateTime, sessionMid,
                        boxEndTime, sessionMid,
                        Brushes.Yellow, DashStyleHelper.Dash, 1);
                }
            }
            catch (Exception ex)
            {
                Print("ERROR DrawORBox: " + ex.Message);
            }
        }

        private void ResetOR()
        {
            sessionHigh = double.MinValue;
            sessionLow = double.MaxValue;
            sessionMid = 0;
            sessionRange = 0;
            orComplete = false;
            retestFiredThisSession = false;  // V12.1101E [B-2]: Reset RETEST latch at session start
            orStartDateTime = DateTime.MinValue;
            orEndDateTime = DateTime.MinValue;
            sessionStartDateTime = DateTime.MinValue;
            orStartBarIndex = 0;
            orEndBarIndex = 0;

            RemoveDrawObjects();
        }

        #endregion

        #region Helpers

        private DateTime ConvertToSelectedTimeZone(DateTime localTime)
        {
            try
            {
                TimeZoneInfo targetZone;
                switch (SelectedTimeZone)
                {
                    case "Eastern":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                        break;
                    case "Central":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
                        break;
                    case "Mountain":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");
                        break;
                    case "Pacific":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                        break;
                    case "UTC":
                        targetZone = TimeZoneInfo.Utc;
                        break;
                    default:
                        return localTime;
                }

                return TimeZoneInfo.ConvertTime(localTime, TimeZoneInfo.Local, targetZone);
            }
            catch (Exception ex)
            {
                Print("ERROR ConvertToSelectedTimeZone: " + ex.Message);
                return localTime;
            }
        }


        private void RemoveDrawObjects()
        {
            RemoveDrawObject("ORBox");
            RemoveDrawObject("ORMid");
        }

        // V12.1101Q [FIX-DRAW]: Ultimate fallback helper using 'object' to bypass namespace issues.
        private object GetDrawObject(string tag)
        {
            if (DrawObjects == null) return null;
            foreach (var o in DrawObjects)
            {
                if (o.Tag == tag) return o;
            }
            return null;
        }

        #endregion

        // V12.16: RMA, TREND, RETEST entry logic in Entries.cs


        // V12.16: Order Management, Trailing Stops, Position Sync moved to Orders.cs


        // V12.16: UI handlers moved to UI.cs


        // V12.16: Stop Management Helpers moved to Orders.cs


        // V12.16: IPC, Compliance, Position Sizing moved to UI.cs

    }
}
// V12.9 REPAIRED - Single-Instance Multi-Account Copy Trading Engine
