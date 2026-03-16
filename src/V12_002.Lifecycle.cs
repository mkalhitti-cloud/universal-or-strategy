// Build 971: V12_002 Lifecycle -- OnStateChange, OnConnectionStatusUpdate, OnMarketData
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
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
        #region OnStateChange

        protected override void OnStateChange()
        {
            State state = State;
            if (state != State.SetDefaults)
                RefreshActorOwnerThread();
            ProcessOnStateChange(state);
        }

        private void ProcessOnStateChange(State state)
        {
            if (state == State.SetDefaults)
            {
                _configureComplete = false;
                _dataLoadedComplete = false;
                Interlocked.Exchange(ref _startupReadinessLogEmitted, 0);
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
                IpcPort = 5001;
                IpcExposeSensitiveFleetIdentity = false;


                // V12.1101E: 5-target system with configurable runner selection
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

                // V8.6: MOMO defaults
                MOMOEnabled = true;
                MOMOStopPoints = 0.5;             // Fixed 0.5pt stop for MOMO trades

                // V8.7: FFMA defaults
                FFMAEnabled = true;
                FFMAEMADistance = 10.0;           // 10 points from 9 EMA
                FFMARSIOverbought = 80;
                FFMARSIOversold = 20;

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
                RmaProximityTicks = 2;
                RmaCancellationTicks = 4;
            }
            else if (state == State.Configure)
            {
                _configureComplete = false;
                _dataLoadedComplete = false;

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


                // IPC Queue
                ipcCommandQueue = new ConcurrentQueue<string>();
                connectedClients = new ConcurrentDictionary<int, IpcClientSession>(); // Build 935 [Fix-1]: prevent NullReferenceException in StopIpcServer

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

                _configureComplete = true;
            }
            else if (state == State.DataLoaded)
            {
                _dataLoadedComplete = false;

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

                int persistedTargetCount = Math.Max(0, Math.Min(5, ConfiguredTargetCount));
                if (persistedTargetCount >= 1)
                {
                    activeTargetCount = persistedTargetCount;
                }
                else
                {
                    // Backward compatibility for templates saved before ConfiguredTargetCount existed.
                    int loadedTargetCount = (Target1Value > 0 ? 1 : 0)
                                          + (Target2Value > 0 ? 1 : 0)
                                          + (Target3Value > 0 ? 1 : 0)
                                          + (Target4Value > 0 ? 1 : 0)
                                          + (Target5Value > 0 ? 1 : 0);
                    activeTargetCount = Math.Max(1, Math.Min(5, loadedTargetCount));
                    ConfiguredTargetCount = activeTargetCount;
                }

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
                
                // V8.7: Initialize RSI for FFMA trades
                rsiIndicator = this.RSI(14, 3);
                
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
                Print(string.Format("FFMA: Enabled={0} Distance={1}pt RSI={2}/{3}", FFMAEnabled, FFMAEMADistance, FFMARSIOversold, FFMARSIOverbought));
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

                _dataLoadedComplete = true;

                // V12.2 HEADLESS SAFETY: Start core services even if ChartControl is null (for background execution)
                // [Build 932]: Start IPC in DataLoaded so Control Surface connects even if market is closed/offline.
                StartIpcServer();
            }
            else if (state == State.Realtime)
            {
                Print("+--------------------------------------------------------------+");
                Print("|          [OK] BMad HARDENED DEPLOYMENT PROTOCOL ACTIVE       |");
                Print(string.Format("|          Build: {0,-10} |  Sync: ONE SOURCE OF TRUTH    |", BUILD_TAG));
                Print("+--------------------------------------------------------------+");

                if (EnableSIMA)
                {
                    // Route realtime SIMA startup through the actor queue so lifecycle state
                    // mutation and optional REAPER start stay ordered on the strategy thread.
                    Enqueue(ctx =>
                    {
                        ctx.EnumerateApexAccounts();
                        if (ctx.ReaperAuditEnabled)
                            ctx.StartReaperAudit();
                    });
                }

                // V10.3: Subscribe to external signals for multi-chart sync
                // SignalBroadcaster.OnExternalCommand += HandleExternalSignal;

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
            else if (state == State.Terminated)
            {
                _isTerminating = true;

                _configureComplete = false;
                _dataLoadedComplete = false;
                Interlocked.Exchange(ref _startupReadinessLogEmitted, 0);

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

                DrainQueuesForShutdown();

                // Stop IPC Server
                StopIpcServer();
                
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

        private void DrainQueuesForShutdown()
        {
            try
            {
                Print("[SHUTDOWN] Draining queues...");
                int ipcDrained = 0;
                if (ipcCommandQueue != null)
                {
                    while (ipcDrained < 100 && ipcCommandQueue.TryDequeue(out string _))
                    {
                        ipcDrained++;
                    }
                }

                int actorDrained = 0;
                while (actorDrained < 50 && _cmdQueue.TryDequeue(out StrategyCommand cmd))
                {
                    try { cmd.Execute(this); } catch { }
                    actorDrained++;
                }
                Print(string.Format("[SHUTDOWN] Drained {0} IPC cmds and {1} Actor cmds.", ipcDrained, actorDrained));
            }
            catch { }
        }

        #endregion

        #region OnConnectionStatusUpdate - Build 948: Mid-session re-adoption on Rithmic reconnect

        protected override void OnConnectionStatusUpdate(ConnectionStatusEventArgs connectionStatusUpdate)
        {
            base.OnConnectionStatusUpdate(connectionStatusUpdate);
            RefreshActorOwnerThread();

            ConnectionStatus status = connectionStatusUpdate.Status;
            bool enableSima = EnableSIMA;
            State strategyState = State;
            Enqueue(ctx => ctx.ProcessOnConnectionStatusUpdate(status, enableSima, strategyState));
        }

        private void ProcessOnConnectionStatusUpdate(ConnectionStatus status, bool enableSima, State strategyState)
        {
            if (!enableSima || strategyState != State.Realtime) return;

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
                try { Enqueue(ctx => ctx.HydrateWorkingOrdersFromBroker()); } catch { }
            }
        }

        #endregion

        #region OnMarketData - V10.1: Process IPC on every tick for real-time responsiveness

        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            RefreshActorOwnerThread();

            // Only process on primary instrument
            if (marketDataUpdate.MarketDataType == MarketDataType.Last)
            {
                if (!EnsureStartupReady(nameof(OnMarketData))) return;

                // Update last known price for real-time tracking
                lastKnownPrice = marketDataUpdate.Price;
                
                // Process IPC commands immediately on every tick
                // This ensures Remote App buttons work even outside session time
                ProcessIpcCommands();
            }
        }

        #endregion
    }
}
