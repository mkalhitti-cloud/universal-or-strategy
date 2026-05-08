// V12_002.StickyState.cs -- Build 1103: Total Persistence ("Sticky State")
// Persists all UI-sourced config to disk on every mutation.
// Hydrates on startup BEFORE IPC server starts.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region Sticky State Fields

        private string _stickyStatePath;        // Full path to .v12state file
        private volatile bool _stickyStateDirty; // Coalescing dirty flag
        private long _stickyWritePending;        // Interlocked gate: 0=idle, 1=write scheduled
        private const int STICKY_DEBOUNCE_MS = 50;

        #endregion

        #region Save -- Serialize + Atomic Write

        /// <summary>
        /// Marks state as dirty. A debounced async write will fire within 50ms.
        /// Safe to call from any thread (strategy thread via Enqueue, or IPC thread).
        /// </summary>
        private void MarkStickyDirty()
        {
            _stickyStateDirty = true;

            // Coalescing gate: only one pending write at a time
            if (Interlocked.CompareExchange(ref _stickyWritePending, 1, 0) == 0)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(STICKY_DEBOUNCE_MS);
                        _stickyStateDirty = false;
                        string payload = SerializeStickyState();
                        AtomicWriteFile(_stickyStatePath, payload);
                    }
                    catch (Exception ex)
                    {
                        Print("[STICKY] Save failed (best-effort): " + ex.Message);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _stickyWritePending, 0);
                        // If dirtied again during write, schedule another
                        if (_stickyStateDirty)
                            MarkStickyDirty();
                    }
                });
            }
        }

        /// <summary>
        /// Serializes ALL UI-sourced state into the .v12state INI format.
        /// Reads volatile fields -- safe because all are atomic-width or volatile.
        /// </summary>
        private string SerializeStickyState()
        {
            var sb = new StringBuilder(1024);
            SerializeSticky_WriteHeaderConfig(sb);
            SerializeSticky_WriteFleetAnchor(sb);
            SerializeSticky_WriteModeProfiles(sb);
            SerializeSticky_WritePositions(sb);
            return sb.ToString();
        }

        private void SerializeSticky_WriteHeaderConfig(StringBuilder sb)
        {
            // Header
            sb.AppendLine("# V12 StickyState v1");
            sb.AppendLine("# Symbol: " + (Instrument != null ? Instrument.FullName : "unknown"));
            sb.AppendLine("# Updated: " + DateTime.UtcNow.ToString("o"));
            sb.AppendLine("# Build: " + BUILD_TAG);
            sb.AppendLine();

            // [CONFIG]
            sb.AppendLine("[CONFIG]");
            string mode = "OR";
            if (isRMAModeActive) mode = "RMA";
            else if (isTRENDModeActive) mode = "TREND";
            else if (isRetestModeActive) mode = "RETEST";
            else if (isMOMOModeActive) mode = "MOMO";
            else if (isFFMAModeArmed) mode = "FFMA";
            sb.AppendLine("MODE=" + mode);
            sb.AppendLine("COUNT=" + activeTargetCount.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T1={0}", Target1Value));
            sb.AppendLine("T1TYPE=" + T1Type.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T2={0}", Target2Value));
            sb.AppendLine("T2TYPE=" + T2Type.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T3={0}", Target3Value));
            sb.AppendLine("T3TYPE=" + T3Type.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T4={0}", Target4Value));
            sb.AppendLine("T4TYPE=" + T4Type.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T5={0}", Target5Value));
            sb.AppendLine("T5TYPE=" + T5Type.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "STR={0}",
                isRMAModeActive ? RMAStopATRMultiplier : StopMultiplier));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "MAX={0}", MaxRiskAmount));
            sb.AppendLine("CIT=" + (ChaseIfTouchPoints ?? "0"));
            sb.AppendLine("TRMA=" + (isTrendRmaMode ? "1" : "0"));
            sb.AppendLine("RRMA=" + (isRetestRmaMode ? "1" : "0"));
            sb.AppendLine();
        }

        private void SerializeSticky_WriteFleetAnchor(StringBuilder sb)
        {
            // [FLEET]
            sb.AppendLine("[FLEET]");
            sb.AppendLine("LEADER=" + (_stickyLeaderAccount ?? ""));
            if (activeFleetAccounts != null)
            {
                foreach (var kvp in activeFleetAccounts.ToArray())
                    sb.AppendLine(kvp.Key + "=" + (kvp.Value ? "1" : "0"));
            }
            sb.AppendLine();

            // [ANCHOR]
            sb.AppendLine("[ANCHOR]");
            sb.AppendLine("TYPE=" + AnchorTypeToString(currentRmaAnchor));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "MNL_PRICE={0}", cachedMnlPrice));
            sb.AppendLine();
        }

        private void SerializeSticky_WriteModeProfiles(StringBuilder sb)
        {
            // Build 1106: [CONFIG_*] -- per-mode profile snapshots
            string activeMode = "OR";
            if (isRMAModeActive) activeMode = "RMA";
            else if (isTRENDModeActive) activeMode = "TREND";
            else if (isRetestModeActive) activeMode = "RETEST";
            else if (isMOMOModeActive) activeMode = "MOMO";
            else if (isFFMAModeArmed) activeMode = "FFMA";
            _modeProfiles[activeMode] = SnapshotCurrentConfig();

            foreach (var kvp in _modeProfiles.ToArray())
            {
                ModeConfigProfile p = kvp.Value;
                if (p == null) continue;
                sb.AppendLine("[CONFIG_" + kvp.Key + "]");
                sb.AppendLine("COUNT=" + p.TargetCount.ToString());
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T1={0}", p.T1));
                sb.AppendLine("T1TYPE=" + p.T1Type.ToString());
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T2={0}", p.T2));
                sb.AppendLine("T2TYPE=" + p.T2Type.ToString());
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T3={0}", p.T3));
                sb.AppendLine("T3TYPE=" + p.T3Type.ToString());
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T4={0}", p.T4));
                sb.AppendLine("T4TYPE=" + p.T4Type.ToString());
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T5={0}", p.T5));
                sb.AppendLine("T5TYPE=" + p.T5Type.ToString());
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "STR={0}", p.StopMult));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "MAX={0}", p.MaxRisk));
                sb.AppendLine();
            }
        }

        private void SerializeSticky_WritePositions(StringBuilder sb)
        {
            // [POSITIONS] -- trailing stop state for active positions
            sb.AppendLine("[POSITIONS]");
            sb.AppendLine("# key|extremePrice|trailLevel|beArmed|beTriggered|initialTargetCount");
            if (activePositions != null)
            {
                foreach (var kvp in activePositions.ToArray())
                {
                    var pi = kvp.Value;
                    if (pi == null || pi.PendingCleanup) continue;
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}|{1}|{2}|{3}|{4}|{5}",
                        kvp.Key,
                        pi.ExtremePriceSinceEntry,
                        pi.CurrentTrailLevel,
                        pi.ManualBreakevenArmed ? "1" : "0",
                        pi.ManualBreakevenTriggered ? "1" : "0",
                        pi.InitialTargetCount));
                }
            }
        }

        // Build 1106: Captures current global config into a mode-specific profile.
        private ModeConfigProfile SnapshotCurrentConfig()
        {
            return new ModeConfigProfile
            {
                TargetCount = activeTargetCount,
                T1 = Target1Value,
                T2 = Target2Value,
                T3 = Target3Value,
                T4 = Target4Value,
                T5 = Target5Value,
                T1Type = T1Type,
                T2Type = T2Type,
                T3Type = T3Type,
                T4Type = T4Type,
                T5Type = T5Type,
                StopMult = isRMAModeActive ? RMAStopATRMultiplier : StopMultiplier,
                MaxRisk = MaxRiskAmount
            };
        }

        // Build 1106: Hydrates global config from a mode-specific profile.
        private void HydrateFromProfile(ModeConfigProfile profile, string mode)
        {
            activeTargetCount = Math.Max(1, Math.Min(5, profile.TargetCount));
            Target1Value = profile.T1;
            Target2Value = profile.T2;
            Target3Value = profile.T3;
            Target4Value = profile.T4;
            Target5Value = profile.T5;
            T1Type = profile.T1Type;
            T2Type = profile.T2Type;
            T3Type = profile.T3Type;
            T4Type = profile.T4Type;
            T5Type = profile.T5Type;
            if (string.Equals(mode, "RMA", StringComparison.OrdinalIgnoreCase))
                RMAStopATRMultiplier = profile.StopMult;
            else
                StopMultiplier = profile.StopMult;
            MaxRiskAmount = profile.MaxRisk;
            RiskPerTrade = profile.MaxRisk;
        }

        private static string AnchorTypeToString(RmaAnchorType t)
        {
            switch (t)
            {
                case RmaAnchorType.Ema30:  return "EMA30";
                case RmaAnchorType.Ema65:  return "EMA65";
                case RmaAnchorType.Ema200: return "EMA200";
                case RmaAnchorType.OrHigh: return "OR_HIGH";
                case RmaAnchorType.OrLow:  return "OR_LOW";
                case RmaAnchorType.Manual: return "MANUAL";
                default: return "EMA65";
            }
        }

        /// <summary>
        /// Atomic file write: write to .tmp, then rename over target.
        /// Prevents corruption if process is killed mid-write.
        /// </summary>
        private void AtomicWriteFile(string targetPath, string content)
        {
            if (string.IsNullOrEmpty(targetPath)) return;
            string tmpPath = targetPath + ".tmp";
            System.IO.File.WriteAllText(tmpPath, content, Encoding.UTF8);
            // File.Move on Windows is atomic on NTFS when same volume
            if (System.IO.File.Exists(targetPath))
                System.IO.File.Delete(targetPath);
            System.IO.File.Move(tmpPath, targetPath);
        }

        #endregion

        #region Load -- Deserialize + Apply

        /// <summary>
        /// Loads persisted state from .v12state file and applies to runtime variables.
        /// Called ONCE in State.DataLoaded, BEFORE StartIpcServer().
        /// Returns true if state was successfully loaded.
        /// </summary>
        private bool LoadStickyState()
        {
            if (string.IsNullOrEmpty(_stickyStatePath))
                return false;

            if (!System.IO.File.Exists(_stickyStatePath))
            {
                Print("[STICKY] No persisted state found -- using defaults");
                return false;
            }

            try
            {
                string[] lines = LoadStickyState_ReadLines();
                string section = "";
                int appliedCount = 0;

                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                        continue;

                    // Section header
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        section = line.Substring(1, line.Length - 2).ToUpperInvariant();
                        continue;
                    }

                    appliedCount += LoadStickyState_DispatchSection(section, line);
                }

                return LoadStickyState_LogOutcome(appliedCount);
            }
            catch (Exception ex)
            {
                Print("[STICKY] Load failed (using defaults): " + ex.Message);
                return false;
            }
        }

        private string[] LoadStickyState_ReadLines()
        {
            return System.IO.File.ReadAllLines(_stickyStatePath, Encoding.UTF8);
        }

        private int LoadStickyState_DispatchSection(string section, string line)
        {
            if (section == "CONFIG")
            {
                return ApplyStickyConfig(line) ? 1 : 0;
            }
            else if (section.StartsWith("CONFIG_") && section.Length > 7)
            {
                // Build 1106: Per-mode profile section (e.g., CONFIG_OR, CONFIG_RMA)
                string profileMode = section.Substring(7);
                return ApplyStickyModeProfile(profileMode, line) ? 1 : 0;
            }
            else if (section == "FLEET")
            {
                return ApplyStickyFleet(line) ? 1 : 0;
            }
            else if (section == "ANCHOR")
            {
                return ApplyStickyAnchor(line) ? 1 : 0;
            }

            // [POSITIONS] deferred to EnrichTrailStateFromSticky()
            return 0;
        }

        private bool LoadStickyState_LogOutcome(int appliedCount)
        {
            Print(string.Format("[STICKY] Loaded {0} settings from {1}", appliedCount,
                System.IO.Path.GetFileName(_stickyStatePath)));
            return appliedCount > 0;
        }

        private bool ApplyStickyConfig(string line)
        {
            int eq = line.IndexOf('=');
            if (eq < 1) return false;
            string key = line.Substring(0, eq).ToUpperInvariant();
            string val = line.Substring(eq + 1);
            if (ApplyStickyConfig_ModeSafetyGate(key, val)) return true;
            if (ApplyStickyConfig_TargetValues(key, val)) return true;
            if (ApplyStickyConfig_TargetTypes(key, val)) return true;
            if (ApplyStickyConfig_RiskAndFlags(key, val)) return true;
            return false;
        }

        private bool ApplyStickyConfig_ModeSafetyGate(string key, string val)
        {
            switch (key)
            {
                case "MODE":
                    // Build 1108.002 SAFETY GATE: Click-trader modes never auto-rearm on startup.
                    isRMAModeActive = false; isRMAButtonClicked = false;
                    isRetestModeActive = false; isTRENDModeActive = false;
                    isMOMOModeActive = false; isFFMAModeArmed = false;
                    if (val != "OR")
                        Print(string.Format("[STICKY] MODE on disk was {0} -- forced to OR (safety gate)", val));
                    return true;

                default: return false;
            }
        }

        private bool ApplyStickyConfig_TargetValues(string key, string val)
        {
            switch (key)
            {
                case "COUNT":
                    if (int.TryParse(val, out int cnt))
                        activeTargetCount = Math.Max(1, Math.Min(5, cnt));
                    return true;

                case "T1":
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t1))
                        Target1Value = t1;
                    return true;
                case "T2":
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t2))
                        Target2Value = t2;
                    return true;
                case "T3":
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t3))
                        Target3Value = t3;
                    return true;
                case "T4":
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t4))
                        Target4Value = t4;
                    return true;
                case "T5":
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t5))
                        Target5Value = t5;
                    return true;

                default: return false;
            }
        }

        private bool ApplyStickyConfig_TargetTypes(string key, string val)
        {
            switch (key)
            {
                case "T1TYPE": T1Type = ParseTargetMode(val); return true;
                case "T2TYPE": T2Type = ParseTargetMode(val); return true;
                case "T3TYPE": T3Type = ParseTargetMode(val); return true;
                case "T4TYPE": T4Type = ParseTargetMode(val); return true;
                case "T5TYPE": T5Type = ParseTargetMode(val); return true;

                default: return false;
            }
        }

        private bool ApplyStickyConfig_RiskAndFlags(string key, string val)
        {
            switch (key)
            {
                case "STR":
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double str))
                    {
                        // Apply to whichever stop is active based on mode (MODE is parsed first)
                        if (isRMAModeActive)
                            RMAStopATRMultiplier = str;
                        else
                            StopMultiplier = str;
                    }
                    return true;

                case "MAX":
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double max))
                    {
                        MaxRiskAmount = max;
                        RiskPerTrade = max; // Sync legacy property
                    }
                    return true;

                case "CIT": ChaseIfTouchPoints = val; return true;
                case "TRMA": isTrendRmaMode = (val == "1"); return true;
                case "RRMA": isRetestRmaMode = (val == "1"); return true;

                default: return false;
            }
        }

        // Build 1106: Parses a single key=value line into a per-mode profile.
        private bool ApplyStickyModeProfile(string mode, string line)
        {
            int eq = line.IndexOf('=');
            if (eq < 1) return false;
            string key = line.Substring(0, eq).ToUpperInvariant();
            string val = line.Substring(eq + 1);

            ModeConfigProfile profile;
            if (!_modeProfiles.TryGetValue(mode, out profile))
            {
                profile = new ModeConfigProfile();
                _modeProfiles[mode] = profile;
            }

            if (ApplyStickyModeProfile_TargetValues(key, val, profile)) return true;
            if (ApplyStickyModeProfile_TargetTypes(key, val, profile)) return true;
            if (ApplyStickyModeProfile_Risk(key, val, profile)) return true;
            return false;
        }

        private bool ApplyStickyModeProfile_TargetValues(string key, string val, ModeConfigProfile profile)
        {
            switch (key)
            {
                case "COUNT":
                    if (int.TryParse(val, out int cnt))
                        profile.TargetCount = Math.Max(1, Math.Min(5, cnt));
                    return true;
                case "T1":
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t1))
                        profile.T1 = t1;
                    return true;
                case "T2":
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t2))
                        profile.T2 = t2;
                    return true;
                case "T3":
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t3))
                        profile.T3 = t3;
                    return true;
                case "T4":
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t4))
                        profile.T4 = t4;
                    return true;
                case "T5":
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t5))
                        profile.T5 = t5;
                    return true;

                default: return false;
            }
        }

        private bool ApplyStickyModeProfile_TargetTypes(string key, string val, ModeConfigProfile profile)
        {
            switch (key)
            {
                case "T1TYPE": profile.T1Type = ParseTargetMode(val); return true;
                case "T2TYPE": profile.T2Type = ParseTargetMode(val); return true;
                case "T3TYPE": profile.T3Type = ParseTargetMode(val); return true;
                case "T4TYPE": profile.T4Type = ParseTargetMode(val); return true;
                case "T5TYPE": profile.T5Type = ParseTargetMode(val); return true;

                default: return false;
            }
        }

        private bool ApplyStickyModeProfile_Risk(string key, string val, ModeConfigProfile profile)
        {
            switch (key)
            {
                case "STR":
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double str))
                        profile.StopMult = str;
                    return true;
                case "MAX":
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double max))
                        profile.MaxRisk = max;
                    return true;

                default: return false;
            }
        }

        private bool ApplyStickyFleet(string line)
        {
            int eq = line.IndexOf('=');
            if (eq < 1) return false;
            string key = line.Substring(0, eq);
            string val = line.Substring(eq + 1);

            if (key.ToUpperInvariant() == "LEADER")
            {
                _stickyLeaderAccount = val;
                return true;
            }

            // Account toggle: "Apex_F01_12345=1"
            // Stored for deferred application AFTER EnumerateApexAccounts() initializes the dict
            if (_pendingStickyFleetToggles == null)
                _pendingStickyFleetToggles = new Dictionary<string, bool>();
            _pendingStickyFleetToggles[key] = (val == "1");
            return true;
        }

        private bool ApplyStickyAnchor(string line)
        {
            int eq = line.IndexOf('=');
            if (eq < 1) return false;
            string key = line.Substring(0, eq).ToUpperInvariant();
            string val = line.Substring(eq + 1);

            if (key == "TYPE")
            {
                SetRmaAnchorFromIpc(val); // Reuse existing parser from V12_002.SIMA.cs:205-222
                return true;
            }
            if (key == "MNL_PRICE")
            {
                if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double p))
                    cachedMnlPrice = p;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Called from SIMA hydration (after Phase 3 in V12_002.SIMA.Lifecycle.cs)
        /// to enrich reconstructed activePositions with persisted trailing stop state.
        /// </summary>
        private void EnrichTrailStateFromSticky()
        {
            if (string.IsNullOrEmpty(_stickyStatePath) || !System.IO.File.Exists(_stickyStatePath))
                return;

            try
            {
                string[] lines = System.IO.File.ReadAllLines(_stickyStatePath, Encoding.UTF8);
                bool inPositions = false;
                int enriched = 0;

                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (line == "[POSITIONS]") { inPositions = true; continue; }
                    if (line.StartsWith("[")) { inPositions = false; continue; }
                    if (!inPositions || string.IsNullOrEmpty(line) || line.StartsWith("#"))
                        continue;

                    // Format: key|extremePrice|trailLevel|beArmed|beTriggered|initialTargetCount
                    string[] parts = line.Split('|');
                    if (parts.Length < 6) continue;

                    string posKey = parts[0];
                    PositionInfo pi;
                    if (!activePositions.TryGetValue(posKey, out pi)) continue;

                    if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double extreme))
                        pi.ExtremePriceSinceEntry = extreme;
                    if (int.TryParse(parts[2], out int trail))
                        pi.CurrentTrailLevel = trail;
                    pi.ManualBreakevenArmed = (parts[3] == "1");
                    pi.ManualBreakevenTriggered = (parts[4] == "1");
                    if (int.TryParse(parts[5], out int itc))
                        pi.InitialTargetCount = itc;

                    enriched++;
                }

                if (enriched > 0)
                    Print(string.Format("[STICKY] Enriched {0} position(s) with persisted trail state", enriched));
            }
            catch (Exception ex)
            {
                Print("[STICKY] Trail enrichment failed (positions use defaults): " + ex.Message);
            }
        }

        /// <summary>
        /// Applies persisted fleet toggles AFTER EnumerateApexAccounts() has
        /// initialized the activeFleetAccounts dictionary with all discovered accounts.
        /// One-shot: clears the temp dict after application.
        /// </summary>
        private void ApplyPendingStickyFleetToggles()
        {
            if (_pendingStickyFleetToggles == null || _pendingStickyFleetToggles.Count == 0)
                return;

            int applied = 0;
            foreach (var kvp in _pendingStickyFleetToggles)
            {
                if (activeFleetAccounts.ContainsKey(kvp.Key))
                {
                    activeFleetAccounts[kvp.Key] = kvp.Value;
                    applied++;
                }
            }

            Print(string.Format("[STICKY] Applied {0}/{1} persisted fleet toggles",
                applied, _pendingStickyFleetToggles.Count));
            _pendingStickyFleetToggles = null; // One-shot -- prevent double-apply
        }

        /// <summary>
        /// Parses TargetMode from string. Matches the IPC CONFIG handler logic.
        /// </summary>
        private static TargetMode ParseTargetMode(string val)
        {
            if (val == null) return TargetMode.ATR;
            string upper = val.ToUpperInvariant();
            if (upper == "ATR") return TargetMode.ATR;
            if (upper == "TICKS") return TargetMode.Ticks;
            if (upper == "POINTS") return TargetMode.Points;
            if (upper == "RUNNER") return TargetMode.Runner;
            return TargetMode.ATR;
        }

        #endregion
    }
}
