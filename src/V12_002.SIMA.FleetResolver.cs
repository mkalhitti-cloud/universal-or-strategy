using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Concrete IFleetResolver. Takes a single consistent snapshot of
    /// activeFleetAccounts and activeTargetCount before the dispatch loop,
    /// eliminating the UI/IPC mid-loop mutation races documented in
    /// [Q3-002] and FIX-B [Build 1102Z].
    ///
    /// NO-LOCKS: Reads from ConcurrentDictionary via LINQ snapshot.
    ///   activeTargetCount is read via Volatile.Read for acquire semantics.
    ///   The resulting FleetContext is immutable after construction.
    /// ZERO-ALLOC: HashSet and List are allocated once per dispatch (cold
    ///   setup phase). The hot loop reads IReadOnlyCollection<string>.Contains()
    ///   which is O(1) on HashSet with no allocation.
    /// </summary>
    internal sealed class FleetResolver : IFleetResolver
    {
        private readonly Func<IEnumerable<KeyValuePair<string, bool>>> _getActiveFleet;
        private readonly Func<IReadOnlyList<AccountRankInfo>>           _getSortedFleet;
        private readonly Func<int>                                      _getActiveTargetCount;
        private readonly Action<string>                                 _print;

        private const int MinTargetCount = 1;
        private const int MaxTargetCount = 5;

        public FleetResolver(
            Func<IEnumerable<KeyValuePair<string, bool>>> getActiveFleet,
            Func<IReadOnlyList<AccountRankInfo>>          getSortedFleet,
            Func<int>                                     getActiveTargetCount,
            Action<string>                                print)
        {
            _getActiveFleet      = getActiveFleet      ?? throw new ArgumentNullException("getActiveFleet");
            _getSortedFleet      = getSortedFleet      ?? throw new ArgumentNullException("getSortedFleet");
            _getActiveTargetCount = getActiveTargetCount ?? throw new ArgumentNullException("getActiveTargetCount");
            _print               = print               ?? throw new ArgumentNullException("print");
        }

        /// <inheritdoc/>
        public FleetContext Resolve()
        {
            // Snapshot activeFleetAccounts -- single read, no lock.
            var activeNames = new HashSet<string>(
                _getActiveFleet()
                    .Where(kvp => kvp.Value)
                    .Select(kvp => kvp.Key));

            // Clamp target count once -- mirrors Math.Max(1,Math.Min(5,...)).
            int rawCount      = _getActiveTargetCount();
            int targetCount   = rawCount < MinTargetCount ? MinTargetCount
                              : rawCount > MaxTargetCount ? MaxTargetCount
                              : rawCount;

            IReadOnlyList<AccountRankInfo> fleet = _getSortedFleet();

            var ctx = new FleetContext(fleet, activeNames, targetCount);

            _print(string.Format("[DISPATCH] Fleet: {0} total accounts | {1} ACTIVE in Fleet Manager",
                fleet.Count, ctx.ActiveCount));

            if (fleet.Count == 0)
                _print("[DISPATCH] [ERR] NO APEX ACCOUNTS DETECTED - Check AccountPrefix setting");

            if (ctx.HasFleetButNoActive())
                _print("[DISPATCH] [ERR] NO ACCOUNTS ENABLED - Toggle accounts ON in Fleet Manager panel");

            return ctx;
        }
    }
}
