using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Immutable value-type snapshot of all gate conditions evaluated before
    /// dispatch begins. Passed by reference through the hot path to avoid
    /// copying eight fields repeatedly.
    ///
    /// ZERO-ALLOC: struct -- lives on the stack for the duration of one dispatch.
    /// NO-LOCKS: all fields written once by IDispatchGate.Evaluate() on the
    /// strategy thread; never mutated after construction.
    /// </summary>
    internal readonly struct GateContext
    {
        public readonly bool SimaEnabled;
        public readonly bool FlattenRunning;
        public readonly bool SemaphoreAcquired;
        public readonly bool IsDuplicate;
        public readonly string DispatchSignature;

        public GateContext(
            bool simaEnabled,
            bool flattenRunning,
            bool semaphoreAcquired,
            bool isDuplicate,
            string dispatchSignature)
        {
            SimaEnabled      = simaEnabled;
            FlattenRunning   = flattenRunning;
            SemaphoreAcquired = semaphoreAcquired;
            IsDuplicate      = isDuplicate;
            DispatchSignature = dispatchSignature;
        }

        /// <summary>
        /// True only when all gate conditions pass and dispatch may proceed.
        /// Called in the hot path -- intentionally inlined.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsOpen() =>
            SimaEnabled && !FlattenRunning && SemaphoreAcquired && !IsDuplicate;
    }

    /// <summary>
    /// Evaluates all pre-dispatch guard conditions and returns an immutable
    /// GateContext. The caller retains ownership of semaphore release.
    ///
    /// CONTRACT:
    ///   - Evaluate() MUST be called from the strategy thread.
    ///   - If GateContext.SemaphoreAcquired == true, the caller MUST call
    ///     ReleaseSemaphore() in a finally block -- always.
    ///   - If GateContext.SemaphoreAcquired == false, defer via
    ///     ScheduleDeferredDispatch() and return immediately.
    ///   - Evaluate() is NEVER called while holding any lock.
    ///
    /// NO-LOCKS: SemaphoreSlim.Wait(0) is non-blocking by design (Build 1109).
    /// </summary>
    internal interface IDispatchGate
    {
        /// <summary>
        /// Atomically evaluates all gate conditions in the correct order:
        ///   1. Non-blocking semaphore attempt (Wait(0))
        ///   2. EnableSIMA check
        ///   3. isFlattenRunning check
        ///   4. MetadataGuard duplicate fingerprint check
        ///
        /// Order is critical: semaphore must be first so that if subsequent
        /// checks fail, ReleaseSemaphore() can still be called in finally.
        /// </summary>
        /// <param name="tradeType">ASCII trade type token (e.g. "RMA", "TREND").</param>
        /// <param name="action">OrderAction for fingerprint construction.</param>
        /// <param name="quantity">Quantity for fingerprint construction.</param>
        /// <param name="entryPrice">Entry price for fingerprint construction.</param>
        /// <returns>Immutable GateContext; caller inspects IsOpen() before proceeding.</returns>
        GateContext Evaluate(
            string tradeType,
            OrderAction action,
            int quantity,
            double entryPrice);

        /// <summary>
        /// Releases the semaphore. Called unconditionally in the finally block
        /// of the dispatch orchestrator -- mirrors the existing [F-03] pattern.
        /// Safe to call even if Evaluate() returned SemaphoreAcquired==false
        /// because implementations MUST guard against double-release.
        /// </summary>
        void ReleaseSemaphore();

        /// <summary>
        /// Schedules a deferred retry via TriggerCustomEvent when the semaphore
        /// is contended. Mirrors the existing Build 1109 defer pattern.
        /// Implementations MUST NOT allocate closures on the hot path -- they
        /// capture the dispatch parameters by value in a DispatchCommand struct.
        /// </summary>
        void ScheduleDeferredDispatch(in DispatchCommand command);
    }

    /// <summary>
    /// Value-type carrier for all dispatch parameters. Replaces the six
    /// individual local variable captures (_defTradeType, _defAction, etc.)
    /// that the existing code creates on the heap during deferred retry.
    ///
    /// ZERO-ALLOC: struct. Boxed only once when passed to TriggerCustomEvent
    /// (pre-existing NinjaTrader constraint -- cannot be avoided).
    /// ASCII ONLY: TradeType is validated ASCII before construction.
    /// </summary>
    internal struct DispatchCommand
    {
        public string     TradeType;
        public OrderAction Action;
        public int        Quantity;
        public double     EntryPrice;
        public OrderType  EntryOrderType;
        public string[]   MasterEntryNames;

        /// <summary>
        /// Constructs a dispatch signature string for MetadataGuard deduplication.
        /// Format mirrors the existing: "SD_{tradeType}_{action}_{quantity}_{price:F2}"
        /// ASCII-only format string -- no Unicode format specifiers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string BuildSignature()
        {
            // string.Format is a cold-path call (once per dispatch, before hot loop).
            // Allocation here is pre-existing and accepted.
            return string.Format("SD_{0}_{1}_{2}_{3:F2}",
                TradeType, Action, Quantity, EntryPrice);
        }
    }

    /// <summary>
    /// Resolves the current FlattenedSubstrateState snapshot, triggering
    /// a freeze rebuild if the membrane is null or stale.
    ///
    /// CONTRACT:
    ///   - TryResolve() reads via Volatile.Read -- no lock.
    ///   - If the membrane is missing, FreezeManagementMembrane() is called
    ///     exactly once per dispatch attempt (mirrors existing pattern).
    ///   - The returned reference is a point-in-time snapshot. Callers MUST
    ///     NOT cache it across dispatch cycles.
    ///   - HOT-PATH SAFETY: IsActive() on the returned state is inlined by
    ///     the JIT; this interface never adds an additional call layer over
    ///     that method.
    /// </summary>
    internal interface IMembraneResolver
    {
        /// <summary>
        /// Attempts to resolve a valid membrane snapshot.
        /// Returns true and sets <paramref name="membrane"/> when available.
        /// Returns false when unavailable after one rebuild attempt --
        /// dispatch MUST abort when this returns false.
        /// </summary>
        bool TryResolve(out FlattenedSubstrateState membrane);

        /// <summary>
        /// Validates that accountIndex is within bounds for the given membrane.
        /// Inlined because it is called once per fleet account per dispatch.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsAccountIndexValid(FlattenedSubstrateState membrane, int accountIndex);
    }

    /// <summary>
    /// Immutable snapshot of fleet state taken once before the dispatch loop.
    /// Replaces the inline HashSet + int capture that existed between the
    /// stateLock comment (V12.Audit [Q3-002]) and the loop start.
    ///
    /// ZERO-ALLOC: The HashSet and List are allocated once per dispatch in the
    /// cold setup phase (pre-existing cost). The hot per-account loop reads
    /// from these snapshots without further allocation.
    /// NO-LOCKS: Snapshot is taken once; the HashSet is never mutated after
    /// construction, eliminating the UI/IPC race described in [Q3-002].
    /// </summary>
    internal readonly struct FleetContext
    {
        public readonly IReadOnlyList<AccountRankInfo> Fleet;
        public readonly IReadOnlyCollection<string>    ActiveAccountNames;
        public readonly int                            DispatchTargetCount;
        public readonly int                            ActiveCount;

        public FleetContext(
            IReadOnlyList<AccountRankInfo> fleet,
            IReadOnlyCollection<string> activeAccountNames,
            int dispatchTargetCount)
        {
            Fleet               = fleet;
            ActiveAccountNames  = activeAccountNames;
            DispatchTargetCount = dispatchTargetCount;
            ActiveCount         = activeAccountNames.Count;
        }

        /// <summary>
        /// True when the fleet contains at least one account and at least one
        /// active account. Mirrors the two [ERR] checks in the original code.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDispatchable() => Fleet.Count > 0 && ActiveCount > 0;

        /// <summary>
        /// True when there are accounts in the fleet but none are active.
        /// Distinct from IsDispatchable() because the original code logs
        /// a different warning for this case without aborting dispatch.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFleetButNoActive() => Fleet.Count > 0 && ActiveCount == 0;
    }

    /// <summary>
    /// Resolves a consistent FleetContext snapshot for one dispatch cycle.
    /// Implementations read activeFleetAccounts and activeTargetCount exactly
    /// once each, preventing the mid-loop IPC mutation race (FIX-B Build 1102Z).
    /// </summary>
    internal interface IFleetResolver
    {
        /// <summary>
        /// Returns an immutable FleetContext for this dispatch cycle.
        /// MUST be called from the strategy thread before the dispatch loop.
        /// The DispatchTargetCount is clamped to [1,5] as per the original:
        ///   Math.Max(1, Math.Min(5, activeTargetCount))
        /// </summary>
        FleetContext Resolve();
    }

    /// <summary>
    /// Resolves the membrane array index for a named account.
    /// Extracted from the inline TryGetValue + range check that appeared
    /// in both the market-entry and limit-entry branches of the original.
    ///
    /// CONTRACT:
    ///   - TryResolve() reads from FlattenedSubstrateState.AccountIndexByName
    ///     which is written only during FreezeManagementMembrane(). The
    ///     membrane reference is a Volatile.Read snapshot -- safe to read
    ///     without a lock on the dictionary because it is never mutated
    ///     after the freeze stamp is set.
    ///   - Returns false when the account name is missing or the index is
    ///     out of range. Caller MUST increment _skippedSlotCount and continue.
    /// </summary>
    internal interface IAccountIndexResolver
    {
        /// <summary>
        /// Attempts to resolve a valid array index for <paramref name="accountName"/>
        /// within the given membrane snapshot.
        /// </summary>
        /// <param name="membrane">Volatile.Read snapshot -- never null at call site.</param>
        /// <param name="accountName">Account name string -- ASCII.</param>
        /// <param name="accountIndex">Resolved index when return is true; -1 otherwise.</param>
        /// <returns>True when the index is valid and in-bounds.</returns>
        bool TryResolve(
            FlattenedSubstrateState membrane,
            string accountName,
            out int accountIndex);
    }
}
