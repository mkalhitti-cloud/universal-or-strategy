using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
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
        #region Enums

        public enum ORTimeframeType
        {
            Minutes_1 = 1,
            Minutes_2 = 2,
            Minutes_3 = 3,
            Minutes_5 = 5,
            Minutes_15 = 15,
            Minutes_30 = 30
        }

        public enum TargetMode
        {
            ATR,
            Ticks,
            Points,
            Runner
        }

        #endregion

        #region Properties

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Session Start", GroupName = "1. Session", Order = 1)]
        public DateTime SessionStart { get; set; }

        [NinjaScriptProperty]
        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Session End", GroupName = "1. Session", Order = 2)]
        public DateTime SessionEnd { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "OR Timeframe", GroupName = "1. Session", Order = 3)]
        public ORTimeframeType ORTimeframe { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Time Zone", GroupName = "1. Session", Order = 4)]
        public string SelectedTimeZone { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Risk Per Trade ($)", GroupName = "2. Risk", Order = 1)]
        public double RiskPerTrade { get; set; }

        /// <summary>REMOVED (Phase 10). Stub retained for workspace XML backward compat.</summary>
        [Browsable(false)]
        [System.Xml.Serialization.XmlIgnore]
        public double ReducedRiskPerTrade { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Risk Amount ($)", GroupName = "2. Risk", Order = 3)]
        public double MaxRiskAmount 
        { 
            get { return RiskPerTrade; } 
            set { RiskPerTrade = value; } 
        }

        [NinjaScriptProperty]
        [Display(Name = "Stop Threshold (Points)", GroupName = "2. Risk", Order = 4)]
        public double StopThresholdPoints { get; set; }

        /// <summary>SLIP-01: Points reserved as a slippage buffer when sizing follower contracts.
        /// Ensures follower dollar risk stays <= MaxRiskAmount even if entry fills at a worse price than master.
        /// Default = 1.0 pt. Set to 0 to disable.</summary>
        [NinjaScriptProperty]
        [Range(0, 10)]
        [Display(Name = "Slippage Cushion (pts)", GroupName = "2. Risk", Order = 5)]
        public double SlippageCushionPoints { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "MES Min Quantity", GroupName = "2. Risk", Order = 5)]
        public int MESMinimum { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "MES Max Quantity", GroupName = "2. Risk", Order = 6)]
        public int MESMaximum { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "MGC Min Quantity", GroupName = "2. Risk", Order = 7)]
        public int MGCMinimum { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "MGC Max Quantity", GroupName = "2. Risk", Order = 8)]
        public int MGCMaximum { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Target 1 Value", GroupName = "3. Targets", Order = 1)]
        public double Target1Value { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Target 2 Value", GroupName = "3. Targets", Order = 2)]
        public double Target2Value { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Target 3 Value", GroupName = "3. Targets", Order = 3)]
        public double Target3Value { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Target 4 Value", GroupName = "3. Targets", Order = 4)]
        public double Target4Value { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Target 5 Value", GroupName = "3. Targets", Order = 5)]
        public double Target5Value { get; set; }


        [NinjaScriptProperty]
        [Display(Name = "T1 Mode", GroupName = "3. Targets", Order = 11)]
        public TargetMode T1Type { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T2 Mode", GroupName = "3. Targets", Order = 12)]
        public TargetMode T2Type { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T3 Mode", GroupName = "3. Targets", Order = 13)]
        public TargetMode T3Type { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T4 Mode", GroupName = "3. Targets", Order = 14)]
        public TargetMode T4Type { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "T5 Mode", GroupName = "3. Targets", Order = 15)]
        public TargetMode T5Type { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Stop Multiplier", GroupName = "4. Stops", Order = 1)]
        public double StopMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min Stop (Points)", GroupName = "4. Stops", Order = 2)]
        public double MinimumStop { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Max Stop (Points)", GroupName = "4. Stops", Order = 3)]
        public double MaximumStop { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Break Even Trigger", GroupName = "5. Trailing", Order = 1)]
        public double BreakEvenTriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Break Even Offset (Ticks)", GroupName = "5. Trailing", Order = 2)]
        public int BreakEvenOffsetTicks { get; set; }  // Ticks above/below entry for BE stop

        [NinjaScriptProperty]
        [Display(Name = "Trail 1 Trigger", GroupName = "5. Trailing", Order = 3)]
        public double Trail1TriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 1 Distance", GroupName = "5. Trailing", Order = 4)]
        public double Trail1DistancePoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 2 Trigger", GroupName = "5. Trailing", Order = 5)]
        public double Trail2TriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 2 Distance", GroupName = "5. Trailing", Order = 6)]
        public double Trail2DistancePoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 3 Trigger", GroupName = "5. Trailing", Order = 7)]
        public double Trail3TriggerPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trail 3 Distance", GroupName = "5. Trailing", Order = 8)]
        public double Trail3DistancePoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Mid Line", GroupName = "6. Display", Order = 1)]
        public bool ShowMidLine { get; set; }

        [NinjaScriptProperty]
        [Range(0, 255)]
        [Display(Name = "Box Opacity", GroupName = "6. Display", Order = 2)]
        public int BoxOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RMA Enabled", GroupName = "7. RMA", Order = 1)]
        public bool RMAEnabled { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ATR Period", GroupName = "7. RMA", Order = 2)]
        public int RMAATRPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RMA Stop Multiplier", GroupName = "7. RMA", Order = 3)]
        public double RMAStopATRMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TREND Enabled", GroupName = "8. TREND", Order = 1)]
        public bool TRENDEnabled { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TREND E1 ATR Multiplier", GroupName = "8. TREND", Order = 2)]
        public double TRENDEntry1ATRMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "TREND E2 ATR Multiplier", GroupName = "8. TREND", Order = 3)]
        public double TRENDEntry2ATRMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RETEST Enabled", GroupName = "9. RETEST", Order = 1)]
        public bool RetestEnabled { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "RETEST ATR Multiplier", GroupName = "9. RETEST", Order = 2)]
        public double RetestATRMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MOMO Enabled", GroupName = "10. MOMO", Order = 1)]
        public bool MOMOEnabled { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "MOMO Stop (Points)", GroupName = "10. MOMO", Order = 2)]
        public double MOMOStopPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "FFMA Enabled", GroupName = "11. FFMA", Order = 1)]
        public bool FFMAEnabled { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "FFMA EMA Distance", GroupName = "11. FFMA", Order = 2)]
        public double FFMAEMADistance { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "FFMA RSI Overbought", GroupName = "11. FFMA", Order = 3)]
        public int FFMARSIOverbought { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "FFMA RSI Oversold", GroupName = "11. FFMA", Order = 4)]
        public int FFMARSIOversold { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable SIMA", GroupName = "12. SIMA", Order = 1)]
        public bool EnableSIMA { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Account Prefix", GroupName = "12. SIMA", Order = 2)]
        public string AccountPrefix { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "IPC Port", GroupName = "12. SIMA", Order = 3)]
        public int IpcPort { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Expose Fleet Identity Over IPC", Description = "When false (default), IPC uses aliases (F01/F02) instead of real account names.", GroupName = "12. SIMA", Order = 3)]
        public bool IpcExposeSensitiveFleetIdentity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Path B", GroupName = "12. SIMA", Order = 4)]
        public bool EnablePathB { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Auto Flatten Desync", GroupName = "12. SIMA", Order = 5)]
        public bool AutoFlattenDesync { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Path B Stop", GroupName = "12. SIMA", Order = 6)]
        public double PathBStopPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Path B Target", GroupName = "12. SIMA", Order = 7)]
        public double PathBTargetPoints { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Chase If Touch Points", GroupName = "12. SIMA", Order = 8)]
        public string ChaseIfTouchPoints { get; set; }

        [NinjaScriptProperty]
        [DefaultValue(true)]
        [Display(Name = "Reaper Audit Enabled", GroupName = "12. SIMA", Order = 9)]
        public bool ReaperAuditEnabled { get; set; }

        [NinjaScriptProperty]
        [Range(500, 60000)]
        [Display(Name = "Reaper Interval (ms)", GroupName = "12. SIMA", Order = 10)]
        public int ReaperIntervalMs { get; set; }

        // GHOST-FIX-2 [Build 922Z]: Grace window before REAPER fires emergency stop on naked position.
        // Build 1104.1 enforces a runtime minimum of 5 seconds to absorb follower bracket lag.
        // Stored values below 5 are clamped by REAPER at runtime.
        [NinjaScriptProperty]
        [Range(0, 10)]
        [Display(Name = "Naked Position Grace (sec)", Description = "Seconds REAPER waits before declaring a no-stop position a true emergency. Minimum: 5 (enforced). Prevents false EF_ during bracket confirmation lag.", GroupName = "12. SIMA", Order = 10)]
        public int NakedPositionGraceSec { get; set; }

        [NinjaScriptProperty]
        [Range(1, 50)]
        [Display(Name = "Repair Tick Fence", GroupName = "12. SIMA", Order = 11)]
        public int RepairTickFence { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Fleet Parity Multiplier", Description = "Lot-size scaling for followers (e.g. 10 for ES->MES)", GroupName = "12. SIMA", Order = 12)]
        public int FleetParityMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Shadow Mode", Description = "Followers auto-mirror leader stop moves and flattens", GroupName = "12. SIMA", Order = 13)]
        public bool ShadowModeEnabled { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Compliance Hub", GroupName = "13. Compliance", Order = 1)]
        public bool EnableComplianceHub { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Consistency Threshold (%)", GroupName = "13. Compliance", Order = 2)]
        public int ConsistencyThreshold { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Consistency Lock", GroupName = "13. Compliance", Order = 3)]
        public bool EnableConsistencyLock { get; set; }

        [NinjaScriptProperty]
        [Range(100, int.MaxValue)]
        [Display(Name = "Daily Profit Cap ($)", GroupName = "13. Compliance", Order = 4)]
        public double MaxDailyProfitCap { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Min Trading Days", GroupName = "13. Compliance", Order = 5)]
        public int PayoutMinTradingDays { get; set; }

        [NinjaScriptProperty]
        [Range(100, int.MaxValue)]
        [Display(Name = "Min Profit Payout ($)", GroupName = "13. Compliance", Order = 6)]
        public double PayoutMinProfit { get; set; }

        [NinjaScriptProperty]
        [Range(100, int.MaxValue)]
        [Display(Name = "Trailing Drawdown Limit ($)", GroupName = "13. Compliance", Order = 7)]
        public double TrailingDrawdownLimit { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "DD Warning Buffer ($)", GroupName = "13. Compliance", Order = 8)]
        public double TrailingDrawdownWarningBuffer { get; set; }

        #endregion

        #region RMA Intelligence Properties (Phase 9.2)

        [NinjaScriptProperty]
        [Display(Name = "Enable RMA Intelligence", GroupName = "14. RMA Intelligence", Order = 1)]
        public bool RmaIntelligenceEnabled { get; set; }




        [NinjaScriptProperty]
        [Display(Name = "Proximity Ticks", GroupName = "14. RMA Intelligence", Order = 2)]
        public int RmaProximityTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Cancellation Ticks", GroupName = "14. RMA Intelligence", Order = 3)]
        public int RmaCancellationTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Max Probe Count", Description = "Probe-and-retreat cycles before exhaustion cancellation.", GroupName = "14. RMA Intelligence", Order = 4)]
        public int RmaMaxProbeCount { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Exhaustion Cancel Enabled", Description = "Cancel orders after RmaMaxProbeCount probes without fill.", GroupName = "14. RMA Intelligence", Order = 5)]
        public bool RmaExhaustionEnabled { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Photon Affinity Bind", GroupName = "Photon Kernel", Order = 1)]
        public bool EnablePhotonAffinityBind { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "CPU Affinity Mask", GroupName = "Photon Kernel", Order = 2)]
        public int CpuAffinityMask { get; set; }

        #endregion
    }
}
