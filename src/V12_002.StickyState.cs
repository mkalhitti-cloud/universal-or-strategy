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

        private string _stickyStatePath; // Full path to .v12state file
        private volatile bool _stickyStateDirty; // Coalescing dirty flag
        private long _stickyWritePending; // Interlocked gate: 0=idle, 1=write scheduled
        private const int STICKY_DEBOUNCE_MS = 50;

        private readonly Services.IStickyStateService _stickyStateService;

        private class StickyStateLogger : Services.IStickyStateLogger
        {
            private readonly Action<string> _print;

            public StickyStateLogger(Action<string> print)
            {
                _print = print;
            }

            public void Log(string message)
            {
                _print(message);
            }
        }

        #endregion

        #region Save -- Serialize via Service

        /// <summary>
        /// Marks state as dirty. A debounced async write will fire within 50ms.
        /// Safe to call from any thread (strategy thread via Enqueue, or IPC thread).
        /// P1-FIX (Iteration 3): Enqueue snapshot capture to FSM/Actor thread to prevent race conditions.
        /// </summary>
        private void MarkStickyDirty()
        {
            _stickyStateDirty = true;

            // P2-FIX (Iteration 4): Check coalescing gate BEFORE enqueue to prevent queue flooding
            // Only enqueue if no write is pending - coalescing happens at enqueue time, not dequeue time
            if (Interlocked.CompareExchange(ref _stickyWritePending, 1, 0) == 0)
            {
                // P1-FIX: Enqueue snapshot building to strategy thread (FSM/Actor pattern)
                // This prevents torn reads when IPC thread calls this while collections are mutating
                Enqueue(state => state.BuildStickySnapshotAndScheduleWrite());
            }
        }

        /// <summary>
        /// P1-FIX (Iteration 3): Builds snapshot on strategy thread, then schedules async write.
        /// Called via Enqueue from MarkStickyDirty() to ensure thread-safe collection iteration.
        /// </summary>
        private void BuildStickySnapshotAndScheduleWrite()
        {
            // P2-FIX (Iteration 4): Gate moved to MarkStickyDirty() to prevent queue flooding
            // This method now always executes when dequeued
            {
                // P1-FIX: Snapshot now built on strategy thread (safe to iterate collections)

                // Map local ModeConfigProfile to Services.ModeConfigProfile
                var modeProfilesSnapshot = new Dictionary<string, Services.ModeConfigProfile>();
                foreach (var kvp in _modeProfiles)
                {
                    if (kvp.Value == null)
                        continue;
                    modeProfilesSnapshot[kvp.Key] = new Services.ModeConfigProfile
                    {
                        TargetCount = kvp.Value.TargetCount,
                        T1 = kvp.Value.T1,
                        T2 = kvp.Value.T2,
                        T3 = kvp.Value.T3,
                        T4 = kvp.Value.T4,
                        T5 = kvp.Value.T5,
                        T1Type = (Services.TargetMode)(int)kvp.Value.T1Type,
                        T2Type = (Services.TargetMode)(int)kvp.Value.T2Type,
                        T3Type = (Services.TargetMode)(int)kvp.Value.T3Type,
                        T4Type = (Services.TargetMode)(int)kvp.Value.T4Type,
                        T5Type = (Services.TargetMode)(int)kvp.Value.T5Type,
                        StopMult = kvp.Value.StopMult,
                        MaxRisk = kvp.Value.MaxRisk,
                    };
                }

                var activeFleetSnapshot =
                    activeFleetAccounts != null ? new Dictionary<string, bool>(activeFleetAccounts) : null;

                // Map local PositionInfo to Services.PositionTrailState
                var positionStatesSnapshot = new Dictionary<string, Services.PositionTrailState>();
                if (activePositions != null)
                {
                    foreach (var kvp in activePositions)
                    {
                        var pi = kvp.Value;
                        if (pi == null || pi.PendingCleanup)
                            continue;
                        positionStatesSnapshot[kvp.Key] = new Services.PositionTrailState
                        {
                            ExtremePriceSinceEntry = pi.ExtremePriceSinceEntry,
                            CurrentTrailLevel = pi.CurrentTrailLevel,
                            ManualBreakevenArmed = pi.ManualBreakevenArmed,
                            ManualBreakevenTriggered = pi.ManualBreakevenTriggered,
                            InitialTargetCount = pi.InitialTargetCount,
                        };
                    }
                }

                var snapshot = new Services.StickyStateSnapshot
                {
                    InstrumentFullName = Instrument != null ? Instrument.FullName : "unknown",
                    BuildTag = BUILD_TAG,
                    IsRMAModeActive = isRMAModeActive,
                    IsTRENDModeActive = isTRENDModeActive,
                    IsRetestModeActive = isRetestModeActive,
                    IsMOMOModeActive = isMOMOModeActive,
                    IsFFMAModeArmed = isFFMAModeArmed,
                    ActiveTargetCount = activeTargetCount,
                    Target1Value = Target1Value,
                    Target2Value = Target2Value,
                    Target3Value = Target3Value,
                    Target4Value = Target4Value,
                    Target5Value = Target5Value,
                    T1Type = (Services.TargetMode)(int)T1Type,
                    T2Type = (Services.TargetMode)(int)T2Type,
                    T3Type = (Services.TargetMode)(int)T3Type,
                    T4Type = (Services.TargetMode)(int)T4Type,
                    T5Type = (Services.TargetMode)(int)T5Type,
                    StopMultiplier = StopMultiplier,
                    RMAStopATRMultiplier = RMAStopATRMultiplier,
                    MaxRiskAmount = MaxRiskAmount,
                    ChaseIfTouchPoints = ChaseIfTouchPoints,
                    IsTrendRmaMode = isTrendRmaMode,
                    IsRetestRmaMode = isRetestRmaMode,
                    LeaderAccount = _stickyLeaderAccount,
                    FleetToggles = activeFleetSnapshot,
                    Anchor = (Services.RmaAnchorType)(int)currentRmaAnchor,
                    ManualPrice = cachedMnlPrice,
                    ModeProfiles = modeProfilesSnapshot,
                    PositionStates = positionStatesSnapshot,
                };

                // P2-FIX (Iteration 4): If service is null, schedule retry instead of dropping save
                if (_stickyStateService == null)
                {
                    Print("[STICKY] Service not initialized -- scheduling retry in 500ms");
                    Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(500); // Retry delay for transient initialization
                            if (_stickyStateService != null && _stickyStateDirty)
                            {
                                // Retry: re-enqueue to capture fresh snapshot
                                Enqueue(state => state.BuildStickySnapshotAndScheduleWrite());
                            }
                            else
                            {
                                Print("[STICKY] Service still null or state no longer dirty -- save abandoned");
                            }
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _stickyWritePending, 0);
                        }
                    });
                    return;
                }

                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(STICKY_DEBOUNCE_MS);
                        _stickyStateDirty = false;
                        _stickyStateService.Serialize(snapshot, _stickyStatePath);
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
                MaxRisk = MaxRiskAmount,
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

        #endregion

        #region Load -- Deserialize via Service

        /// <summary>
        /// Loads persisted state from .v12state file and applies to runtime variables.
        /// Called ONCE in State.DataLoaded, BEFORE StartIpcServer().
        /// Returns true if state was successfully loaded.
        /// </summary>
        // DeepSource: Suppress CS-R1140 - High complexity is intentional for comprehensive state hydration
        // This method performs exhaustive dictionary lookups for 20+ config values in a single pass.
        // Refactoring would fragment the hydration logic across multiple methods without reducing actual complexity.
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "DeepSource",
            "CS-R1140:Method has high cyclomatic complexity"
        )]
        private bool LoadStickyState()
        {
            if (string.IsNullOrEmpty(_stickyStatePath))
                return false;

            if (!System.IO.File.Exists(_stickyStatePath))
            {
                Print("[STICKY] No persisted state found -- using defaults");
                return false;
            }

            // P1-FIX: Guard against uninitialized service
            if (_stickyStateService == null)
            {
                Print("[STICKY] Service not initialized -- skipping load");
                return false;
            }

            try
            {
                var data = _stickyStateService.Deserialize(_stickyStatePath);
                if (data == null)
                    return false;

                // Apply config values
                if (data.ConfigValues.TryGetValue("COUNT", out object cntObj) && cntObj is int)
                {
                    int cnt = (int)cntObj;
                    activeTargetCount = Math.Max(1, Math.Min(5, cnt));
                }

                if (data.ConfigValues.TryGetValue("T1", out object t1Obj) && t1Obj is double)
                {
                    double t1 = (double)t1Obj;
                    Target1Value = t1;
                }
                if (data.ConfigValues.TryGetValue("T2", out object t2Obj) && t2Obj is double)
                {
                    double t2 = (double)t2Obj;
                    Target2Value = t2;
                }
                if (data.ConfigValues.TryGetValue("T3", out object t3Obj) && t3Obj is double)
                {
                    double t3 = (double)t3Obj;
                    Target3Value = t3;
                }
                if (data.ConfigValues.TryGetValue("T4", out object t4Obj) && t4Obj is double)
                {
                    double t4 = (double)t4Obj;
                    Target4Value = t4;
                }
                if (data.ConfigValues.TryGetValue("T5", out object t5Obj) && t5Obj is double)
                {
                    double t5 = (double)t5Obj;
                    Target5Value = t5;
                }

                if (data.ConfigValues.TryGetValue("T1TYPE", out object t1tObj) && t1tObj is Services.TargetMode)
                {
                    Services.TargetMode t1t = (Services.TargetMode)t1tObj;
                    T1Type = (TargetMode)(int)t1t;
                }
                if (data.ConfigValues.TryGetValue("T2TYPE", out object t2tObj) && t2tObj is Services.TargetMode)
                {
                    Services.TargetMode t2t = (Services.TargetMode)t2tObj;
                    T2Type = (TargetMode)(int)t2t;
                }
                if (data.ConfigValues.TryGetValue("T3TYPE", out object t3tObj) && t3tObj is Services.TargetMode)
                {
                    Services.TargetMode t3t = (Services.TargetMode)t3tObj;
                    T3Type = (TargetMode)(int)t3t;
                }
                if (data.ConfigValues.TryGetValue("T4TYPE", out object t4tObj) && t4tObj is Services.TargetMode)
                {
                    Services.TargetMode t4t = (Services.TargetMode)t4tObj;
                    T4Type = (TargetMode)(int)t4t;
                }
                if (data.ConfigValues.TryGetValue("T5TYPE", out object t5tObj) && t5tObj is Services.TargetMode)
                {
                    Services.TargetMode t5t = (Services.TargetMode)t5tObj;
                    T5Type = (TargetMode)(int)t5t;
                }

                if (data.ConfigValues.TryGetValue("STR", out object strObj) && strObj is double)
                {
                    double str = (double)strObj;
                    // Apply to whichever stop is active based on mode
                    if (isRMAModeActive)
                        RMAStopATRMultiplier = str;
                    else
                        StopMultiplier = str;
                }

                if (data.ConfigValues.TryGetValue("MAX", out object maxObj) && maxObj is double)
                {
                    double max = (double)maxObj;
                    MaxRiskAmount = max;
                    RiskPerTrade = max; // Sync legacy property
                }

                if (data.ConfigValues.TryGetValue("CIT", out object citObj) && citObj is string)
                {
                    string cit = (string)citObj;
                    ChaseIfTouchPoints = cit;
                }

                if (data.ConfigValues.TryGetValue("TRMA", out object trmaObj) && trmaObj is bool)
                {
                    bool trma = (bool)trmaObj;
                    isTrendRmaMode = trma;
                }

                if (data.ConfigValues.TryGetValue("RRMA", out object rrmaObj) && rrmaObj is bool)
                {
                    bool rrma = (bool)rrmaObj;
                    isRetestRmaMode = rrma;
                }

                // Apply profiles
                foreach (var kvp in data.ModeProfiles)
                {
                    var sProfile = kvp.Value;
                    if (sProfile == null)
                    {
                        continue;
                    }
                    var mode = kvp.Key;

                    ModeConfigProfile profile;
                    if (!_modeProfiles.TryGetValue(mode, out profile))
                    {
                        profile = new ModeConfigProfile();
                        _modeProfiles[mode] = profile;
                    }
                    profile.TargetCount = sProfile.TargetCount;
                    profile.T1 = sProfile.T1;
                    profile.T2 = sProfile.T2;
                    profile.T3 = sProfile.T3;
                    profile.T4 = sProfile.T4;
                    profile.T5 = sProfile.T5;
                    profile.T1Type = (TargetMode)(int)sProfile.T1Type;
                    profile.T2Type = (TargetMode)(int)sProfile.T2Type;
                    profile.T3Type = (TargetMode)(int)sProfile.T3Type;
                    profile.T4Type = (TargetMode)(int)sProfile.T4Type;
                    profile.T5Type = (TargetMode)(int)sProfile.T5Type;
                    profile.StopMult = sProfile.StopMult;
                    profile.MaxRisk = sProfile.MaxRisk;
                }

                // Apply fleet
                _stickyLeaderAccount = data.LeaderAccount;
                foreach (var kvp in data.FleetToggles)
                {
                    if (_pendingStickyFleetToggles == null)
                        _pendingStickyFleetToggles = new Dictionary<string, bool>();
                    _pendingStickyFleetToggles[kvp.Key] = kvp.Value;
                }

                // Apply anchor
                SetRmaAnchorFromIpc(data.Anchor.ToString());
                cachedMnlPrice = data.ManualPrice;

                return true;
            }
            catch (Exception ex)
            {
                Print("[STICKY] Load failed (using defaults): " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Called from SIMA hydration (after Phase 3 in V12_002.SIMA.Lifecycle.cs)
        /// to enrich reconstructed activePositions with persisted trailing stop state.
        /// </summary>
        private void EnrichTrailStateFromSticky()
        {
            if (string.IsNullOrEmpty(_stickyStatePath) || !System.IO.File.Exists(_stickyStatePath))
                return;

            // P1-FIX (Iteration 3): Guard against uninitialized service
            if (_stickyStateService == null)
            {
                Print("[STICKY] Service not initialized -- skipping trail enrichment");
                return;
            }

            try
            {
                var data = _stickyStateService.Deserialize(_stickyStatePath);
                if (data == null || data.PositionStates == null || data.PositionStates.Count == 0)
                    return;

                int enriched = 0;
                foreach (var kvp in data.PositionStates)
                {
                    string posKey = kvp.Key;
                    var state = kvp.Value;

                    PositionInfo pi;
                    if (!activePositions.TryGetValue(posKey, out pi))
                        continue;

                    pi.ExtremePriceSinceEntry = state.ExtremePriceSinceEntry;
                    pi.CurrentTrailLevel = state.CurrentTrailLevel;
                    pi.ManualBreakevenArmed = state.ManualBreakevenArmed;
                    pi.ManualBreakevenTriggered = state.ManualBreakevenTriggered;
                    pi.InitialTargetCount = state.InitialTargetCount;

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

            Print(
                string.Format(
                    "[STICKY] Applied {0}/{1} persisted fleet toggles",
                    applied,
                    _pendingStickyFleetToggles.Count
                )
            );
            _pendingStickyFleetToggles = null; // One-shot -- prevent double-apply
        }

        #endregion
    }
}

// Made with Bob
