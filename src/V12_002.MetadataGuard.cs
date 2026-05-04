using System;
using System.Collections.Concurrent;
using System.Linq;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        private const long MetadataMaxCommandAgeMs = 5000;
        private const long MetadataMaxEventAgeMs = 30000;

        private readonly ConcurrentDictionary<string, DateTime> _processedCommandIds =
            new ConcurrentDictionary<string, DateTime>();

        private bool MetadataGuardTimestamp(long eventTicks, string context)
        {
            try
            {
                if (eventTicks <= 0) return true;

                long ageTicks = DateTime.UtcNow.Ticks - eventTicks;
                if (ageTicks <= 0) return true;

                long maxAgeTicks = MetadataMaxCommandAgeMs * TimeSpan.TicksPerMillisecond;
                if (ageTicks > maxAgeTicks)
                {
                    double ageMs = ageTicks / (double) TimeSpan.TicksPerMillisecond;
                    Print(string.Format("[METADATA-G1] STALE {0}: age={1:F0}ms > max={2}ms -- rejected",
                        context, ageMs, MetadataMaxCommandAgeMs));
                    return false;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }

        private bool MetadataGuardCommandTimestamp(long senderTicks, string context)
        {
            try
            {
                if (senderTicks <= 0)
                {
                    Print(string.Format("[IPC-G1] WARN no-ts {0}: pass (fail-open)", context));
                    return true;
                }

                return MetadataGuardTimestamp(senderTicks, string.Format("IPC:{0}", context));
            }
            catch
            {
                return true;
            }
        }

        private bool MetadataGuardEventAge(long eventTicks, string context)
        {
            try
            {
                if (eventTicks <= 0) return true;

                long ageTicks = DateTime.UtcNow.Ticks - eventTicks;
                if (ageTicks <= 0) return true;

                long maxAgeTicks = MetadataMaxEventAgeMs * TimeSpan.TicksPerMillisecond;
                if (ageTicks > maxAgeTicks)
                {
                    double ageMs = ageTicks / (double) TimeSpan.TicksPerMillisecond;
                    Print(string.Format("[METADATA-G1b] STALE {0}: age={1:F0}ms > max={2}ms -- rejected",
                        context, ageMs, MetadataMaxEventAgeMs));
                    return false;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }

        private bool MetadataGuardStateCompatibility(FollowerBracketState currentState, OrderState incomingEvent, string context)
        {
            try
            {
                if (currentState == FollowerBracketState.Filled
                    || currentState == FollowerBracketState.Cancelled
                    || currentState == FollowerBracketState.Rejected)
                {
                    Print(string.Format("[METADATA-G2] Terminal FSM {0} received {1} -- rejected",
                        currentState, incomingEvent));
                    return false;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }

        private bool MetadataGuardDuplicate(string commandId, string context)
        {
            try
            {
                if (string.IsNullOrEmpty(commandId)) return true;

                DateTime nowUtc = DateTime.UtcNow;
                DateTime pruneBefore = nowUtc.AddMilliseconds(-MetadataMaxCommandAgeMs * 2);
                foreach (var kvp in _processedCommandIds.ToArray())
                {
                    if (kvp.Value < pruneBefore)
                        _processedCommandIds.TryRemove(kvp.Key, out _);
                }

                if (_processedCommandIds.TryAdd(commandId, nowUtc))
                {
                    return true;
                }

                Print(string.Format("[METADATA-G3] DUPLICATE command {0} for {1} -- rejected", commandId, context));
                return false;
            }
            catch
            {
                return true;
            }
        }

        private bool MetadataGuardRepairAuthorized(string accountName, string context)
        {
            try
            {
                bool hasActiveFsm = _followerBrackets.Values.Any(f =>
                    f != null
                    && f.AccountName == accountName
                    && f.State == FollowerBracketState.Active);

                if (hasActiveFsm)
                {
                    Print(string.Format("[METADATA-G4] Repair suppressed for {0}: FSM Active (self-healed)", accountName));
                    return false;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }

        private bool MetadataGuardFsmEvent(AccountEvent evt, FollowerBracketFSM fsm)
        {
            string context = fsm != null && !string.IsNullOrEmpty(fsm.EntryName)
                ? fsm.EntryName
                : (!string.IsNullOrEmpty(evt.SignalName) ? evt.SignalName : "FSM");

            if (evt.TimestampTicks > 0 && !MetadataGuardEventAge(evt.TimestampTicks, context))
                return false;

            if (fsm != null && !MetadataGuardStateCompatibility(fsm.State, evt.NewState, context))
                return false;

            return true;
        }
    }
}
