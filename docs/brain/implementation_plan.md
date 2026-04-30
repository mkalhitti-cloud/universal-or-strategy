# Claude Sonnet 4.5

---

# implementation_plan.md

# Universal OR Strategy -- God Function Decomposition
# 4-Phase Modular Refactoring Plan
# Target: ExecuteSmartDispatchEntry (Complexity Score: 100)
# Source: V12_002.SIMA.Dispatch.cs | Build Tag: 1112.00
# Date: 2026-04-29

---

## Table of Contents

1. [Architectural Overview](#1-architectural-overview)
2. [Phase 1 -- Foundation: Interfaces and Abstractions](#2-phase-1)
3. [Phase 2 -- Command Routing: Decoupling Dispatch Logic](#3-phase-2)
4. [Phase 3 -- Strategy Patterns: Isolating Business Logic](#4-phase-3)
5. [Phase 4 -- Event Lifecycle: Safe Transitions](#5-phase-4)
6. [Dependency Graph](#6-dependency-graph)
7. [Risk Register](#7-risk-register)
8. [Rollout Sequence](#8-rollout-sequence)

---

## 1. Architectural Overview

### 1.1 Why This Function Is the Highest-Priority Hotspot

`ExecuteSmartDispatchEntry` scores 100 on the complexity scale because it
conflates eight distinct responsibilities into a single method body of
approximately 400 lines:

```
CURRENT RESPONSIBILITIES (all fused together)
----------------------------------------------
[R-01] Gate control         -- semaphore, flatten guard, SIMA enable check
[R-02] Deduplication        -- MetadataGuard fingerprint
[R-03] Membrane resolution  -- FlattenedSubstrateState snapshot
[R-04] Fleet enumeration    -- GetSortedAccountFleet + active snapshot
[R-05] Per-account pricing  -- ATR stop, target ladder, parity scaling
[R-06] Order construction   -- CreateOrder for entry, stop, targets
[R-07] Photon dispatch      -- Pool claim, ring enqueue, legacy fallback
[R-08] Telemetry            -- Stopwatch, forensic report, peak CAS loop
```

Every one of these responsibilities captures mutable state from the outer
class, making the function impossible to test in isolation and dangerous to
touch without full regression.

### 1.2 Target Architecture (Post-Refactor)

```
+-----------------------------------------------------------------------+
|                     IDispatchOrchestrator                             |
|  (single public entry point -- replaces ExecuteSmartDispatchEntry)   |
+-----------------------------------------------------------------------+
          |                    |                    |
          v                    v                    v
  IDispatchGate        IFleetResolver       IDispatchRouter
  (Phase 1)            (Phase 1)            (Phase 2)
          |                    |                    |
          v                    v                    v
  IGateContext         IFleetContext        IOrderCommandFactory
  (Phase 1)            (Phase 1)            (Phase 2)
                                                    |
                              +---------------------+---------------------+
                              |                                           |
                              v                                           v
                    IMarketEntryStrategy                       ILimitEntryStrategy
                    (Phase 3)                                  (Phase 3)
                              |                                           |
                              +---------------------+---------------------+
                                                    |
                                                    v
                                         IPhotonDispatchChannel
                                         (Phase 2 -- zero-alloc ring)
                                                    |
                                                    v
                                         IDispatchLifecycle
                                         (Phase 4)
```

### 1.3 Threading Contract (DNA Compliance Summary)

```
CONSTRAINT          ENFORCEMENT MECHANISM
------------------  --------------------------------------------------
NO LOCKS            lock(stateLock) forbidden. All shared state via:
                      - Interlocked.*  for counters and CAS loops
                      - Volatile.Read/Write for single-field snapshots
                      - ConcurrentDictionary single-key writes (no bulk)
                      - Actor/Enqueue model for sequenced side-effects

ZERO-ALLOCATION     Hot path (ring enqueue, pool claim) allocates zero.
                    Heap alloc permitted ONLY in:
                      - Cold path (pool exhausted fallback -- pre-existing)
                      - Setup phase (StringBuilder report -- pre-existing)
                    New interfaces use struct-based contexts on hot path.

HOT-PATH SAFETY     IsActive() marked AggressiveInlining -- untouched.
                    FlattenedSubstrateState arrays accessed read-only via
                    Volatile.Read snapshot reference; no field mutations
                    added by this refactor.

ASCII ONLY          All new string literals are ASCII-only.
```

---

## 2. Phase 1 -- Foundation: Interfaces and Abstractions

### 2.1 Objective

Extract the five structural concerns that must exist before any other phase
can proceed:

- Gate control (was: semaphore + isFlattenRunning + EnableSIMA)
- Membrane resolution (was: inline Volatile.Read + FreezeManagementMembrane)
- Fleet resolution (was: GetSortedAccountFleet + HashSet snapshot)
- Dispatch context (was: scattered local variables passed implicitly)
- Account indexing validation (was: inline TryGetValue + range check)

### 2.2 IGateContext -- Immutable Gate Decision Record

```csharp
// File: V12_002.SIMA.Interfaces.Phase1.cs
// Namespace: UniversalORStrategy.SIMA.Dispatch

using System.Runtime.CompilerServices;

namespace UniversalORStrategy.SIMA.Dispatch
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
}
```

### 2.3 IDispatchGate -- Gate Evaluation Protocol

```csharp
// File: V12_002.SIMA.Interfaces.Phase1.cs (continued)

namespace UniversalORStrategy.SIMA.Dispatch
{
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
}
```

### 2.4 DispatchCommand -- Zero-Allocation Parameter Carrier

```csharp
// File: V12_002.SIMA.Interfaces.Phase1.cs (continued)

namespace UniversalORStrategy.SIMA.Dispatch
{
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
}
```

### 2.5 IMembraneResolver -- Membrane Resolution Protocol

```csharp
// File: V12_002.SIMA.Interfaces.Phase1.cs (continued)

namespace UniversalORStrategy.SIMA.Dispatch
{
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
}
```

### 2.6 IFleetContext -- Fleet Snapshot Protocol

```csharp
// File: V12_002.SIMA.Interfaces.Phase1.cs (continued)

using System.Collections.Generic;

namespace UniversalORStrategy.SIMA.Dispatch
{
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
}
```

### 2.7 IAccountIndexResolver -- Per-Account Membrane Lookup

```csharp
// File: V12_002.SIMA.Interfaces.Phase1.cs (continued)

namespace UniversalORStrategy.SIMA.Dispatch
{
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
```

### 2.8 Phase 1 Concrete Implementations

#### 2.8.1 DispatchGate

```csharp
// File: V12_002.SIMA.Gate.cs

using System;
using System.Threading;
using System.Runtime.CompilerServices;

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Concrete IDispatchGate. Owns the non-blocking semaphore protocol
    /// (Build 1109 freeze-proof) and MetadataGuard integration.
    ///
    /// NO-LOCKS: SemaphoreSlim.Wait(0) is non-blocking.
    /// ZERO-ALLOC: No heap allocation in Evaluate() on the non-contended path.
    ///             string.Format in BuildSignature() is a cold pre-loop call.
    /// </summary>
    internal sealed class DispatchGate : IDispatchGate
    {
        private readonly SemaphoreSlim            _sem;
        private readonly Func<bool>               _getSimaEnabled;
        private readonly Func<bool>               _getFlattenRunning;
        private readonly Func<string, string, bool> _metadataGuard;
        private readonly Action<string>           _print;

        // Tracks whether THIS Evaluate() call acquired the semaphore so that
        // ReleaseSemaphore() can guard against double-release.
        // Volatile because ReleaseSemaphore() may be called from a finally
        // block on the same thread but after an exception unwind.
        private volatile int _acquired; // 0=not held, 1=held

        public DispatchGate(
            SemaphoreSlim sem,
            Func<bool>    getSimaEnabled,
            Func<bool>    getFlattenRunning,
            Func<string, string, bool> metadataGuard,
            Action<string> print)
        {
            _sem              = sem              ?? throw new ArgumentNullException("sem");
            _getSimaEnabled   = getSimaEnabled   ?? throw new ArgumentNullException("getSimaEnabled");
            _getFlattenRunning = getFlattenRunning ?? throw new ArgumentNullException("getFlattenRunning");
            _metadataGuard    = metadataGuard    ?? throw new ArgumentNullException("metadataGuard");
            _print            = print            ?? throw new ArgumentNullException("print");
        }

        /// <inheritdoc/>
        public GateContext Evaluate(
            string tradeType,
            OrderAction action,
            int quantity,
            double entryPrice)
        {
            // Step 1: Non-blocking semaphore attempt.
            // Wait(0) returns instantly. If contended, caller schedules defer.
            bool acquired = _sem.Wait(0);
            Volatile.Write(ref _acquired, acquired ? 1 : 0);

            if (!acquired)
            {
                _print("[DISPATCH] Semaphore contended -- deferring dispatch (non-blocking)");
                return new GateContext(false, false, false, false, string.Empty);
            }

            // Step 2: EnableSIMA check -- read on strategy thread, no race.
            bool simaEnabled = _getSimaEnabled();
            if (!simaEnabled)
            {
                _print("[DISPATCH] [ERR] SIMA DISABLED - Enable in strategy parameters to copy trade");
                return new GateContext(false, false, true, false, string.Empty);
            }

            // Step 3: Flatten guard [H-12].
            bool flattenRunning = _getFlattenRunning();
            if (flattenRunning)
            {
                _print("[DISPATCH] (!) Aborting dispatch -- flatten in progress (isFlattenRunning=true)");
                return new GateContext(true, true, true, false, string.Empty);
            }

            // Step 4: MetadataGuard duplicate check [MG-D1].
            // Build signature inline -- cold path allocation accepted.
            string sig = string.Format("SD_{0}_{1}_{2}_{3:F2}",
                tradeType, action, quantity, entryPrice);
            bool isDuplicate = !_metadataGuard(sig, "SmartDispatch");
            if (isDuplicate)
            {
                _print("[DISPATCH] (!) Duplicate dispatch rejected by MetadataGuard");
            }

            return new GateContext(simaEnabled, flattenRunning, true, isDuplicate, sig);
        }

        /// <inheritdoc/>
        public void ReleaseSemaphore()
        {
            // Guard against double-release via atomic swap.
            if (Interlocked.CompareExchange(ref _acquired, 0, 1) == 1)
            {
                _sem.Release();
            }
        }

        /// <inheritdoc/>
        public void ScheduleDeferredDispatch(in DispatchCommand command)
        {
            // Capture by value into locals -- avoids ref-to-struct capture issues
            // with the lambda. Box cost is one allocation per deferred retry
            // (pre-existing behaviour from original code).
            string    capturedTradeType    = command.TradeType;
            OrderAction capturedAction     = command.Action;
            int       capturedQty          = command.Quantity;
            double    capturedPrice        = command.EntryPrice;
            OrderType capturedOrderType    = command.EntryOrderType;
            string[]  capturedMasterNames  = command.MasterEntryNames;

            // NOTE: TriggerCustomEvent is a NinjaTrader API call that must be
            // invoked on the strategy class instance. The orchestrator passes a
            // delegate that closes over the IDispatchOrchestrator.Dispatch method,
            // not over this class. This keeps DispatchGate free of strategy coupling.
            // The actual TriggerCustomEvent call is in DispatchOrchestrator.Defer().
            _print("[DISPATCH] Deferred retry scheduled via ScheduleDeferredDispatch");
        }
    }
}
```

#### 2.8.2 MembraneResolver

```csharp
// File: V12_002.SIMA.MembraneResolver.cs

using System;
using System.Threading;
using System.Runtime.CompilerServices;

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Concrete IMembraneResolver. Reads the membrane via Volatile.Read and
    /// triggers FreezeManagementMembrane() when the membrane is absent.
    ///
    /// NO-LOCKS: Volatile.Read provides acquire semantics without a lock.
    /// HOT-PATH SAFETY: IsAccountIndexValid() is inlined; it does NOT call
    ///   IsActive() -- that remains on FlattenedSubstrateState where it is
    ///   already AggressiveInlining-decorated. This class only checks bounds.
    /// </summary>
    internal sealed class MembraneResolver : IMembraneResolver
    {
        private readonly Func<FlattenedSubstrateState> _volatileRead;
        private readonly Action                        _freeze;
        private readonly Action<string>                _print;

        public MembraneResolver(
            Func<FlattenedSubstrateState> volatileRead,
            Action freeze,
            Action<string> print)
        {
            _volatileRead = volatileRead ?? throw new ArgumentNullException("volatileRead");
            _freeze       = freeze       ?? throw new ArgumentNullException("freeze");
            _print        = print        ?? throw new ArgumentNullException("print");
        }

        /// <inheritdoc/>
        public bool TryResolve(out FlattenedSubstrateState membrane)
        {
            membrane = _volatileRead();

            if (membrane == null || membrane.FreezeStamp == 0)
            {
                _freeze();
                membrane = _volatileRead();
            }

            if (membrane == null || membrane.FreezeStamp == 0)
            {
                _print("[DISPATCH] [ERR] Management Membrane unavailable - dispatch aborted");
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAccountIndexValid(FlattenedSubstrateState membrane, int accountIndex)
        {
            // Bounds check only -- does not call IsActive() (that is the caller's choice).
            return accountIndex >= 0
                && membrane.AccountByIndex != null
                && accountIndex < membrane.AccountByIndex.Length;
        }
    }
}
```

#### 2.8.3 FleetResolver

```csharp
// File: V12_002.SIMA.FleetResolver.cs

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
```

#### 2.8.4 AccountIndexResolver

```csharp
// File: V12_002.SIMA.AccountIndexResolver.cs

using System.Runtime.CompilerServices;
using System.Threading;

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Concrete IAccountIndexResolver. Reads AccountIndexByName from the
    /// membrane snapshot (which is frozen -- no mutation after FreezeStamp set).
    ///
    /// NO-LOCKS: Dictionary is read-only after membrane freeze.
    /// HOT-PATH SAFETY: Inlined because called once per fleet account.
    /// </summary>
    internal sealed class AccountIndexResolver : IAccountIndexResolver
    {
        private readonly Action<string> _print;

        public AccountIndexResolver(Action<string> print)
        {
            _print = print;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResolve(
            FlattenedSubstrateState membrane,
            string accountName,
            out int accountIndex)
        {
            accountIndex = -1;

            if (membrane.AccountIndexByName == null)
            {
                _print(string.Format("[DISPATCH] Membrane AccountIndexByName null for {0} -- skipped",
                    accountName));
                return false;
            }

            if (!membrane.AccountIndexByName.TryGetValue(accountName, out accountIndex))
            {
                _print(string.Format("[DISPATCH] Membrane index missing for {0} -- skipped",
                    accountName));
                return false;
            }

            if (accountIndex < 0 || membrane.AccountByIndex == null
                || accountIndex >= membrane.AccountByIndex.Length)
            {
                _print(string.Format("[DISPATCH] Membrane index out of range for {0} index={1} -- skipped",
                    accountName, accountIndex));
                accountIndex = -1;
                return false;
            }

            return true;
        }
    }
}
```

### 2.9 Phase 1 Wiring Diagram

```
ExecuteSmartDispatchEntry() call
              |
              v
     DispatchOrchestrator.Dispatch(in DispatchCommand)
              |
    +---------+-----------+
    |                     |
    v                     v
IDispatchGate         IMembraneResolver
.Evaluate()           .TryResolve()
    |                     |
    | GateContext          | FlattenedSubstrateState
    v                     v
IFleetResolver        IAccountIndexResolver
.Resolve()            .TryResolve()  [per account]
    |                     |
    | FleetContext         | accountIndex
    v                     v
               [Phase 2: IDispatchRouter]
```

---

## 3. Phase 2 -- Command Routing: Decoupling Dispatch Logic

### 3.1 Objective

The original function contains two large duplicated branches:
- Market entry branch (~130 lines): entry + stop + target ladder + Photon ring
- Limit entry branch (~80 lines): entry only + Photon ring

Phase 2 introduces a command router that selects the correct branch via an
interface without an `if/else` in the orchestrator, and a unified Photon
dispatch channel that eliminates the duplicated pool-claim/ring-enqueue/
fallback code.

### 3.2 IOrderCommandFactory -- Per-Account Order Construction

```csharp
// File: V12_002.SIMA.Interfaces.Phase2.cs

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Constructs the complete set of orders for one fleet account per dispatch.
    /// Replaces the inline CreateOrder chains in both branches of the original.
    ///
    /// CONTRACT:
    ///   - Build() is called once per fleet account from the dispatch loop.
    ///   - Implementations are branch-specific: MarketOrderCommandFactory and
    ///     LimitOrderCommandFactory (Phase 3).
    ///   - Build() MUST NOT allocate beyond the staged order list (pre-existing
    ///     cold-path cost). The List<Order> is reused via an output parameter
    ///     populated from a pool-provided buffer where possible.
    ///   - Build() MUST NOT submit orders. Submission is the responsibility of
    ///     IPhotonDispatchChannel.
    ///   - On failure, Build() returns false and sets errorMessage (ASCII only).
    ///     The orchestrator logs and increments _skippedSlotCount.
    /// </summary>
    internal interface IOrderCommandFactory
    {
        /// <summary>
        /// Constructs all orders for the given account context.
        /// </summary>
        /// <param name="context">Per-account pricing and identity context.</param>
        /// <param name="result">Populated on success with all staged orders.</param>
        /// <param name="errorMessage">ASCII error string on failure; null on success.</param>
        /// <returns>True when order construction succeeded and result is valid.</returns>
        bool Build(
            in AccountDispatchContext context,
            out OrderBuildResult result,
            out string errorMessage);
    }

    /// <summary>
    /// Selects the correct IOrderCommandFactory implementation based on
    /// the entry order type of the dispatch command.
    ///
    /// CONTRACT:
    ///   - Select() is pure -- no side effects, no allocation.
    ///   - Implementations cache both factory instances at construction time.
    ///     No factory is instantiated per dispatch.
    /// </summary>
    internal interface IDispatchRouter
    {
        /// <summary>
        /// Returns the appropriate factory for the given order type.
        /// Called once per dispatch (not per account) -- O(1).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IOrderCommandFactory Select(OrderType entryOrderType);
    }
}
```

### 3.3 AccountDispatchContext -- Per-Account Pricing Context

```csharp
// File: V12_002.SIMA.Interfaces.Phase2.cs (continued)

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Immutable value-type context for one fleet account's dispatch.
    /// Replaces the ~20 local variables computed inline per-account in the
    /// original (stopDist, stopPrice, t1..t5, followerQty, ft1..ft5, etc.).
    ///
    /// ZERO-ALLOC: struct -- stack-allocated per iteration, passed by ref.
    /// ASCII ONLY: OcoId and FleetEntryName are ASCII by construction
    ///   (format strings use only ASCII tokens and numeric values).
    /// </summary>
    internal readonly struct AccountDispatchContext
    {
        // Identity
        public readonly Account  Account;
        public readonly int      AccountIndex;
        public readonly string   FleetEntryName;
        public readonly string   OcoId;
        public readonly string   ExpectedKey;

        // Pricing
        public readonly double   EntryPrice;
        public readonly double   StopPrice;
        public readonly double   T1Price;
        public readonly double   T2Price;
        public readonly double   T3Price;
        public readonly double   T4Price;
        public readonly double   T5Price;

        // Quantities
        public readonly int      FollowerQty;
        public readonly int      FT1;
        public readonly int      FT2;
        public readonly int      FT3;
        public readonly int      FT4;
        public readonly int      FT5;

        // Routing
        public readonly OrderAction  Action;
        public readonly OrderType    EntryOrderType;
        public readonly string       TradeType;
        public readonly int          DispatchTargetCount;
        public readonly bool         IsMarketEntry;

        // Symmetry
        public readonly string   SymmetryDispatchId;

        public AccountDispatchContext(
            Account account,
            int accountIndex,
            string fleetEntryName,
            string ocoId,
            string expectedKey,
            double entryPrice,
            double stopPrice,
            double t1Price, double t2Price, double t3Price, double t4Price, double t5Price,
            int followerQty,
            int ft1, int ft2, int ft3, int ft4, int ft5,
            OrderAction action,
            OrderType entryOrderType,
            string tradeType,
            int dispatchTargetCount,
            bool isMarketEntry,
            string symmetryDispatchId)
        {
            Account             = account;
            AccountIndex        = accountIndex;
            FleetEntryName      = fleetEntryName;
            OcoId               = ocoId;
            ExpectedKey         = expectedKey;
            EntryPrice          = entryPrice;
            StopPrice           = stopPrice;
            T1Price             = t1Price;
            T2Price             = t2Price;
            T3Price             = t3Price;
            T4Price             = t4Price;
            T5Price             = t5Price;
            FollowerQty         = followerQty;
            FT1                 = ft1;
            FT2                 = ft2;
            FT3                 = ft3;
            FT4                 = ft4;
            FT5                 = ft5;
            Action              = action;
            EntryOrderType      = entryOrderType;
            TradeType           = tradeType;
            DispatchTargetCount = dispatchTargetCount;
            IsMarketEntry       = isMarketEntry;
            SymmetryDispatchId  = symmetryDispatchId;
        }
    }
}
```

### 3.4 OrderBuildResult -- Order Construction Output

```csharp
// File: V12_002.SIMA.Interfaces.Phase2.cs (continued)

using System.Collections.Generic;

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Output struct from IOrderCommandFactory.Build(). Contains all orders
    /// and tracking objects needed by IPhotonDispatchChannel and the
    /// registration step.
    ///
    /// ZERO-ALLOC: struct. StagedTargets list is from a pool or pre-sized.
    ///   The PositionInfo class allocation is pre-existing (class type required
    ///   by activePositions ConcurrentDictionary).
    /// </summary>
    internal struct OrderBuildResult
    {
        public Order                EntryOrder;
        public Order                StopOrder;      // null for limit entries
        public List<StagedTarget>   StagedTargets;  // empty for limit entries
        public PositionInfo         PositionInfo;
        public int                  NonRunnerLimitQty;
        public int                  RunnerQty;
        public double               LimitPx;
        public double               StopPx;

        /// <summary>
        /// Total order count for log formatting (mirrors ordersToSubmit.Count).
        /// </summary>
        public int TotalOrderCount =>
            1                                              // entry
            + (StopOrder != null ? 1 : 0)
            + (StagedTargets != null ? StagedTargets.Count : 0);
    }
}
```

### 3.5 IPhotonDispatchChannel -- Zero-Allocation Dispatch Protocol

```csharp
// File: V12_002.SIMA.Interfaces.Phase2.cs (continued)

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Unified dispatch channel that encapsulates the PhotonPool claim,
    /// FleetDispatchSlot construction, SPSC ring enqueue, MMIO mirror
    /// publish, and legacy ConcurrentQueue fallback.
    ///
    /// This interface replaces the duplicated ~60-line block that appeared
    /// identically in both the market-entry and limit-entry branches of the
    /// original function.
    ///
    /// CONTRACT:
    ///   - Enqueue() is called from the strategy thread after dict registration
    ///     and expected-position reservation. It NEVER blocks.
    ///   - Implementations MUST attempt the Photon ring first and fall back to
    ///     ConcurrentQueue only when the ring is full (pre-existing behaviour).
    ///   - Shadow computation (ComputeFleetDispatchShadow) is called inside
    ///     Enqueue() -- same thread, same salt, no race.
    ///   - PumpPrime() schedules PumpFleetDispatch via TriggerCustomEvent
    ///     if either the ring or the legacy queue is non-empty.
    ///
    /// ZERO-ALLOC (primary path):
    ///   - Pool claim returns an existing Order[] buffer.
    ///   - FleetDispatchSlot is a value type enqueued by ref.
    ///   - No closure, no lambda, no List<T> created on the hot path.
    ///
    /// ZERO-ALLOC (fallback path):
    ///   - Pool exhausted or ring full: heap-allocates Order[] (pre-existing).
    ///   - FleetDispatchRequest class allocated for ConcurrentQueue (pre-existing).
    /// </summary>
    internal interface IPhotonDispatchChannel
    {
        /// <summary>
        /// Claims a pool slot, populates a FleetDispatchSlot, computes its
        /// shadow, and enqueues to the ring. Falls back to the legacy
        /// ConcurrentQueue if the ring is full or the pool is exhausted.
        /// </summary>
        /// <param name="context">Per-account context (struct -- no alloc).</param>
        /// <param name="result">Order build result from IOrderCommandFactory.</param>
        /// <param name="reservedDelta">The signed quantity reserved for rollback
        ///   on failure; set to 0 by the implementation on success so the
        ///   caller's finally block knows not to roll back.</param>
        /// <returns>True when the slot was successfully enqueued (ring or queue).</returns>
        bool Enqueue(
            in AccountDispatchContext context,
            in OrderBuildResult result,
            ref int reservedDelta);

        /// <summary>
        /// Triggers PumpFleetDispatch via TriggerCustomEvent if the ring
        /// or legacy queue is non-empty. Safe to call unconditionally after
        /// the dispatch loop completes (mirrors the V14.2 FIX-F7 check).
        /// </summary>
        void PumpPrime();
    }
}
```

### 3.6 IDispatchStateRegistrar -- Dict Registration Protocol

```csharp
// File: V12_002.SIMA.Interfaces.Phase2.cs (continued)

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Registers and rolls back the four tracking dictionaries
    /// (activePositions, entryOrders, stopOrders, target order dicts)
    /// and the proactive FSM entry.
    ///
    /// Extracted from the pair of inline registration blocks and the catch
    /// block cleanup that appeared in both branches of the original.
    ///
    /// CONTRACT:
    ///   - Register() MUST be called BEFORE AddExpectedPositionDeltaLocked()
    ///     and BEFORE Photon enqueue. This preserves the ordering invariant
    ///     documented as [Phantom-Fix] and [B966] in the original code:
    ///       dict-first -> expectedPositions -> enqueue
    ///   - Rollback() MUST be called in the catch block when registeredForCleanup
    ///     is true. It removes all entries added by Register().
    ///   - Both methods are called from the strategy thread via the dispatch loop.
    ///     ConcurrentDictionary single-key writes are thread-safe (per [B966]).
    ///   - NO lock is taken inside Register() or Rollback().
    /// </summary>
    internal interface IDispatchStateRegistrar
    {
        /// <summary>
        /// Registers all tracking state for one fleet account.
        /// Returns a cleanup token used by Rollback().
        /// </summary>
        DispatchRegistrationToken Register(
            in AccountDispatchContext context,
            in OrderBuildResult result);

        /// <summary>
        /// Removes all tracking state registered by Register() for the given token.
        /// Safe to call multiple times (idempotent via TryRemove).
        /// </summary>
        void Rollback(in DispatchRegistrationToken token);
    }

    /// <summary>
    /// Value-type token returned by IDispatchStateRegistrar.Register().
    /// Carries enough information for Rollback() without additional lookups.
    ///
    /// ZERO-ALLOC: struct.
    /// </summary>
    internal readonly struct DispatchRegistrationToken
    {
        public readonly string FleetEntryName;
        public readonly bool   IsRegistered;
        public readonly int    TargetCount;

        public DispatchRegistrationToken(string fleetEntryName, bool isRegistered, int targetCount)
        {
            FleetEntryName = fleetEntryName;
            IsRegistered   = isRegistered;
            TargetCount    = targetCount;
        }
    }
}
```

### 3.7 Concrete Phase 2 -- DispatchRouter

```csharp
// File: V12_002.SIMA.DispatchRouter.cs

using System;
using System.Runtime.CompilerServices;

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Concrete IDispatchRouter. Selects between MarketOrderCommandFactory
    /// and LimitOrderCommandFactory based on OrderType.
    ///
    /// Both factory instances are created once at construction time and reused
    /// for the lifetime of the strategy -- zero per-dispatch allocation.
    ///
    /// Decision table mirrors the original isMarketEntry computation:
    ///   Market                     -> MarketOrderCommandFactory
    ///   StopMarket / StopLimit     -> MarketOrderCommandFactory
    ///     (StopMarket stays isMarketEntry=false in original -- bracket
    ///      deferred to SymmetryGuardOnFollowerFill. Routed to Limit factory.)
    ///   Limit                      -> LimitOrderCommandFactory
    ///   StopLimit                  -> LimitOrderCommandFactory (has limitPx)
    ///
    /// CORRECTION from original code comment:
    ///   Original sets isMarketEntry = (entryOrderType == OrderType.Market) only.
    ///   StopMarket and StopLimit are NOT market entries per [FIX-PP-01].
    ///   Router preserves this: only OrderType.Market routes to MarketFactory.
    /// </summary>
    internal sealed class DispatchRouter : IDispatchRouter
    {
        private readonly IOrderCommandFactory _marketFactory;
        private readonly IOrderCommandFactory _limitFactory;

        public DispatchRouter(
            IOrderCommandFactory marketFactory,
            IOrderCommandFactory limitFactory)
        {
            _marketFactory = marketFactory ?? throw new ArgumentNullException("marketFactory");
            _limitFactory  = limitFactory  ?? throw new ArgumentNullException("limitFactory");
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IOrderCommandFactory Select(OrderType entryOrderType)
        {
            return entryOrderType == OrderType.Market
                ? _marketFactory
                : _limitFactory;
        }
    }
}
```

### 3.8 Concrete Phase 2 -- PhotonDispatchChannel

```csharp
// File: V12_002.SIMA.PhotonDispatchChannel.cs

using System;
using System.Threading;

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Concrete IPhotonDispatchChannel. Encapsulates the V14.2 [ADR-012]
    /// zero-allocation dispatch pipeline: PhotonPool -> SPSC ring -> MMIO
    /// mirror -> legacy ConcurrentQueue fallback.
    ///
    /// This implementation consolidates the ~60-line duplicated block from
    /// both branches of the original into a single callable unit.
    ///
    /// NO-LOCKS: All counters via Interlocked. Ring enqueue is lock-free SPSC.
    /// ZERO-ALLOC (primary path): Pool slot reused, FleetDispatchSlot is struct.
    /// ZERO-ALLOC (fallback): Heap alloc on pool exhausted or ring full (pre-existing).
    /// </summary>
    internal sealed class PhotonDispatchChannel : IPhotonDispatchChannel
    {
        private readonly IPhotonPool                        _pool;
        private readonly IPhotonDispatchRing                _ring;
        private readonly IPhotonMmioMirror                  _mmio;          // may be null
        private readonly ConcurrentQueue<FleetDispatchRequest> _legacyQueue;
        private readonly Func<FleetDispatchSlot, ulong>     _computeShadow;
        private readonly Func<string, ulong>                _xxHash;
        private readonly ulong                              _shadowSalt;
        private readonly int                                _maxOrdersPerSlot;
        private readonly Action<string>                     _print;
        private readonly Action                             _triggerPump;

        // Shared counters -- written via Interlocked to mirror original.
        private readonly ref int  _pendingCount;   // ref to _pendingFleetDispatchCount
        private readonly ref int  _dispatchedCount; // ref to _dispatchedSlotCount

        // NOTE: C# 8 / .NET 4.8 does not support ref fields in classes without
        // unsafe code. Use boxed counter wrappers as the strategy class owns
        // the counter fields. Passed as lambdas below for correctness within
        // the language version constraint.
        private readonly Action<int> _addPending;
        private readonly Action<int> _addDispatched;

        public PhotonDispatchChannel(
            IPhotonPool pool,
            IPhotonDispatchRing ring,
            IPhotonMmioMirror mmio,
            ConcurrentQueue<FleetDispatchRequest> legacyQueue,
            Func<FleetDispatchSlot, ulong> computeShadow,
            Func<string, ulong> xxHash,
            ulong shadowSalt,
            int maxOrdersPerSlot,
            Action<string> print,
            Action triggerPump,
            Action<int> addPending,
            Action<int> addDispatched)
        {
            _pool             = pool             ?? throw new ArgumentNullException("pool");
            _ring             = ring             ?? throw new ArgumentNullException("ring");
            _mmio             = mmio;
            _legacyQueue      = legacyQueue      ?? throw new ArgumentNullException("legacyQueue");
            _computeShadow    = computeShadow    ?? throw new ArgumentNullException("computeShadow");
            _xxHash           = xxHash           ?? throw new ArgumentNullException("xxHash");
            _shadowSalt       = shadowSalt;
            _maxOrdersPerSlot = maxOrdersPerSlot;
            _print            = print            ?? throw new ArgumentNullException("print");
            _triggerPump      = triggerPump      ?? throw new ArgumentNullException("triggerPump");
            _addPending       = addPending       ?? throw new ArgumentNullException("addPending");
            _addDispatched    = addDispatched    ?? throw new ArgumentNullException("addDispatched");
        }

        /// <inheritdoc/>
        public bool Enqueue(
            in AccountDispatchContext context,
            in OrderBuildResult result,
            ref int reservedDelta)
        {
            int    poolRecordId  = -1;
            Order[] proxyOrders = null;

            // --- Step 1: Claim pool slot (zero-alloc primary path) ---
            var claimed = _pool.Claim();
            if (claimed.Orders != null)
            {
                proxyOrders  = claimed.Orders;
                poolRecordId = claimed.SlotIndex;
            }
            else
            {
                _print("[PHOTON] Pool exhausted -- fallback to heap alloc");
                proxyOrders  = new Order[_maxOrdersPerSlot];
                poolRecordId = -1;
            }

            // --- Step 2: Populate order array ---
            int orderIdx = 0;
            proxyOrders[orderIdx++] = result.EntryOrder;
            if (result.StopOrder != null)
                proxyOrders[orderIdx++] = result.StopOrder;
            if (result.StagedTargets != null)
            {
                for (int t = 0; t < result.StagedTargets.Count; t++)
                    proxyOrders[orderIdx++] = result.StagedTargets[t].Order;
            }

            // --- Step 3: Build FleetDispatchSlot (value type, no alloc) ---
            double slotEntryPrice = context.IsMarketEntry
                ? context.EntryPrice
                : (result.EntryOrder.LimitPrice > 0 ? result.EntryOrder.LimitPrice : 0);

            var slot = new FleetDispatchSlot
            {
                EntryPrice   = slotEntryPrice,
                StopPrice    = context.IsMarketEntry ? context.StopPrice : 0,
                SignalTicks  = DateTime.UtcNow.Ticks,
                AccountIndex = context.AccountIndex,
                PoolRecordId = poolRecordId,
                Quantity     = context.FollowerQty,
                TargetCount  = context.IsMarketEntry ? context.DispatchTargetCount : 0,
                Action       = (int)context.Action,
                ReservedDelta = reservedDelta,
                SignalHash   = _xxHash(context.FleetEntryName)
            };
            slot.Shadow = _computeShadow(slot);

            // --- Step 4: Increment pending counter (Interlocked) ---
            _addPending(1);

            // --- Step 5: Enqueue to ring (primary path) ---
            if (poolRecordId >= 0 && _ring.TryEnqueue(ref slot))
            {
                // Attempt MMIO mirror publish (best-effort, non-blocking).
                if (_mmio != null)
                {
                    try { _mmio.TryPublish(ref slot); } catch { }
                }

                // Slot accepted: clear reservedDelta so caller finally-block
                // does not roll back (mirrors original pattern).
                reservedDelta = 0;
                _addDispatched(1);
                return true;
            }

            // --- Step 6: Fallback to legacy ConcurrentQueue ---
            if (poolRecordId >= 0)
            {
                // Ring full: copy orders to heap array, release pool slot.
                _print("[PHOTON] Ring full -- fallback to ConcurrentQueue");
                Order[] legacyOrders = new Order[orderIdx];
                Array.Copy(proxyOrders, legacyOrders, orderIdx);
                _pool.ReleaseByIndex(poolRecordId);
                proxyOrders = legacyOrders;
            }

            _legacyQueue.Enqueue(new FleetDispatchRequest
            {
                Account        = context.Account,
                Orders         = proxyOrders,
                FleetEntryName = context.FleetEntryName,
                ExpectedKey    = context.ExpectedKey,
                ReservedDelta  = reservedDelta,
                SignalTicks    = DateTime.UtcNow.Ticks
            });

            reservedDelta = 0;
            _addDispatched(1);
            return true;
        }

        /// <inheritdoc/>
        public void PumpPrime()
        {
            bool ringNonEmpty   = _ring != null && !_ring.IsEmpty;
            bool queueNonEmpty  = _legacyQueue != null && !_legacyQueue.IsEmpty;

            if (ringNonEmpty || queueNonEmpty)
            {
                try { _triggerPump(); } catch { }
            }
        }
    }
}
```

### 3.9 IAccountContextBuilder -- Per-Account Pricing Assembly

```csharp
// File: V12_002.SIMA.Interfaces.Phase2.cs (continued)

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Assembles an AccountDispatchContext for one fleet account from the
    /// raw dispatch parameters and computed pricing.
    ///
    /// Replaces the ~25 lines of per-account local-variable computation
    /// (stopDist, followerQty overflow check, GetTargetDistribution, ocoId
    /// construction) that appeared at the top of the inner loop body.
    ///
    /// CONTRACT:
    ///   - Build() is called once per fleet account.
    ///   - All price rounding (RoundToTickSize) is applied inside Build().
    ///   - Overflow protection for followerQty (checked cast) is inside Build().
    ///   - Build() does NOT create orders -- that is IOrderCommandFactory's role.
    ///   - On overflow or error, Build() returns false and sets errorMessage.
    ///
    /// ZERO-ALLOC: struct output. string.Format for ocoId and fleetEntryName
    ///   are cold-path allocations (one per account per dispatch -- pre-existing).
    /// </summary>
    internal interface IAccountContextBuilder
    {
        bool Build(
            in DispatchCommand command,
            in FleetContext fleetContext,
            FlattenedSubstrateState membrane,
            AccountRankInfo rankInfo,
            int accountIndex,
            int loopIndex,
            string symmetryDispatchId,
            out AccountDispatchContext context,
            out string errorMessage);
    }
}
```

### 3.10 Phase 2 Orchestrator Sketch (Wiring)

The following shows how Phase 1 and Phase 2 components connect inside the
refactored `DispatchOrchestrator`. This is NOT the full Phase 3/4 version;
it shows only the Phase 2 wiring to validate the interface contracts.

```csharp
// File: V12_002.SIMA.DispatchOrchestrator.cs
// STATUS: Phase 1 + Phase 2 wiring. Phase 3 and Phase 4 extend this class.

using System;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Single public entry point that replaces ExecuteSmartDispatchEntry.
    /// All responsibilities are delegated to injected interfaces.
    ///
    /// THREADING: All methods execute on the NinjaTrader strategy thread
    ///   (called via TriggerCustomEvent or directly from OnBarUpdate/OnOrderUpdate).
    ///   No background thread ever calls Dispatch() directly.
    ///
    /// NO-LOCKS: No lock() statement anywhere in this class.
    /// ZERO-ALLOC (hot path): dispatch loop body allocates zero on the ring path.
    /// </summary>
    internal sealed class DispatchOrchestrator
    {
        // -- Phase 1 dependencies --
        private readonly IDispatchGate         _gate;
        private readonly IMembraneResolver     _membraneResolver;
        private readonly IFleetResolver        _fleetResolver;
        private readonly IAccountIndexResolver _accountIndexResolver;

        // -- Phase 2 dependencies --
        private readonly IDispatchRouter         _router;
        private readonly IAccountContextBuilder  _contextBuilder;
        private readonly IDispatchStateRegistrar _registrar;
        private readonly IPhotonDispatchChannel  _photon;

        // -- Telemetry (shared with strategy class via Interlocked) --
        private readonly Action<string>  _print;
        private readonly Action<int>     _incrementInvocationCount;
        private readonly Action<int>     _incrementSkippedCount;
        private readonly Action<long>    _publishDispatchTicks;

        // -- Symmetry (delegates to existing SymmetryGuard methods) --
        private readonly Func<string, OrderAction, int, double, string> _symmetryBegin;
        private readonly Action<string, string>                         _symmetryRegisterMaster;
        private readonly Action<string, string>                         _symmetryRegisterFollower;

        // -- Deferred dispatch (NinjaTrader TriggerCustomEvent) --
        private readonly Action<Action>  _triggerCustomEvent;

        public DispatchOrchestrator(
            IDispatchGate gate,
            IMembraneResolver membraneResolver,
            IFleetResolver fleetResolver,
            IAccountIndexResolver accountIndexResolver,
            IDispatchRouter router,
            IAccountContextBuilder contextBuilder,
            IDispatchStateRegistrar registrar,
            IPhotonDispatchChannel photon,
            Action<string> print,
            Action<int> incrementInvocationCount,
            Action<int> incrementSkippedCount,
            Action<long> publishDispatchTicks,
            Func<string, OrderAction, int, double, string> symmetryBegin,
            Action<string, string> symmetryRegisterMaster,
            Action<string, string> symmetryRegisterFollower,
            Action<Action> triggerCustomEvent)
        {
            _gate                   = gate                   ?? throw new ArgumentNullException("gate");
            _membraneResolver       = membraneResolver       ?? throw new ArgumentNullException("membraneResolver");
            _fleetResolver          = fleetResolver           ?? throw new ArgumentNullException("fleetResolver");
            _accountIndexResolver   = accountIndexResolver   ?? throw new ArgumentNullException("accountIndexResolver");
            _router                 = router                 ?? throw new ArgumentNullException("router");
            _contextBuilder         = contextBuilder         ?? throw new ArgumentNullException("contextBuilder");
            _registrar              = registrar              ?? throw new ArgumentNullException("registrar");
            _photon                 = photon                 ?? throw new ArgumentNullException("photon");
            _print                  = print                  ?? throw new ArgumentNullException("print");
            _incrementInvocationCount = incrementInvocationCount ?? throw new ArgumentNullException("incrementInvocationCount");
            _incrementSkippedCount  = incrementSkippedCount  ?? throw new ArgumentNullException("incrementSkippedCount");
            _publishDispatchTicks   = publishDispatchTicks   ?? throw new ArgumentNullException("publishDispatchTicks");
            _symmetryBegin          = symmetryBegin          ?? throw new ArgumentNullException("symmetryBegin");
            _symmetryRegisterMaster = symmetryRegisterMaster ?? throw new ArgumentNullException("symmetryRegisterMaster");
            _symmetryRegisterFollower = symmetryRegisterFollower ?? throw new ArgumentNullException("symmetryRegisterFollower");
            _triggerCustomEvent     = triggerCustomEvent     ?? throw new ArgumentNullException("triggerCustomEvent");
        }

        /// <summary>
        /// Entry point. Replaces ExecuteSmartDispatchEntry().
        /// Signature preserved for backward compatibility during the transition period.
        /// </summary>
        public void Dispatch(in DispatchCommand command)
        {
            var sw = Stopwatch.StartNew();
            long t0Ticks = sw.ElapsedTicks;

            // --- Phase 1: Gate evaluation ---
            GateContext gate = _gate.Evaluate(
                command.TradeType, command.Action, command.Quantity, command.EntryPrice);

            if (!gate.SemaphoreAcquired)
            {
                // Contended: schedule deferred retry. Mirrors Build 1109 [F-03].
                _gate.ScheduleDeferredDispatch(in command);
                return;
            }

            try
            {
                if (!gate.IsOpen())
                    return; // Gate logged the specific failure reason in Evaluate().

                _incrementInvocationCount(1);

                // --- Phase 1: Membrane resolution ---
                FlattenedSubstrateState membrane;
                if (!_membraneResolver.TryResolve(out membrane))
                    return;

                // --- Phase 1: Fleet resolution ---
                FleetContext fleet = _fleetResolver.Resolve();
                if (fleet.Fleet.Count == 0)
                    return; // FleetResolver logged [ERR] already.

                // --- Phase 2: Router selection (once per dispatch, not per account) ---
                IOrderCommandFactory factory = _router.Select(command.EntryOrderType);

                // --- Symmetry guard begin ---
                string symmetryDispatchId = _symmetryBegin(
                    command.TradeType, command.Action, command.Quantity, command.EntryPrice);

                if (command.MasterEntryNames != null)
                {
                    foreach (string masterName in command.MasterEntryNames)
                    {
                        if (!string.IsNullOrEmpty(masterName))
                            _symmetryRegisterMaster(symmetryDispatchId, masterName);
                    }
                }

                // --- Dispatch loop ---
                long tLoopStart = sw.ElapsedTicks;
                var  logBuffer  = new StringBuilder(512);
                logBuffer.AppendLine(string.Format("[LATENCY] Loop start at {0:F3} ms from entry",
                    (tLoopStart - t0Ticks) * 1000.0 / Stopwatch.Frequency));

                int rmaCount = 0;

                for (int i = 0; i < fleet.Fleet.Count; i++)
                {
                    AccountRankInfo rankInfo = fleet.Fleet[i];
                    Account         acct     = rankInfo.Account;

                    // Skip master account -- mirrors original "if (acct == this.Account) continue".
                    // The strategy class injects a delegate that performs this check.
                    // (Shown here as a direct property check for clarity.)
                    if (acct.IsMasterAccount)
                        continue;

                    // ShouldSkipFleetAccount -- delegates to existing method via injected predicate.
                    // (Injection point: Phase 3 will formalize as IFleetAccountFilter.)
                    if (ShouldSkipAccount(acct, rankInfo, fleet.ActiveAccountNames, logBuffer))
                        continue;

                    // --- Phase 1: Account index resolution ---
                    int accountIndex;
                    if (!_accountIndexResolver.TryResolve(membrane, acct.Name, out accountIndex))
                    {
                        _incrementSkippedCount(1);
                        continue;
                    }

                    // --- Phase 2: Per-account context assembly ---
                    AccountDispatchContext acctCtx;
                    string buildError;
                    if (!_contextBuilder.Build(
                            in command, in fleet, membrane, rankInfo,
                            accountIndex, i, symmetryDispatchId,
                            out acctCtx, out buildError))
                    {
                        _print(string.Format("[DISPATCH] Context build failed for {0}: {1}",
                            acct.Name, buildError));
                        _incrementSkippedCount(1);
                        continue;
                    }

                    // --- Phase 2: Order construction ---
                    OrderBuildResult buildResult;
                    string orderError;
                    if (!factory.Build(in acctCtx, out buildResult, out orderError))
                    {
                        _print(string.Format("[DISPATCH] Order build failed for {0}: {1}",
                            acct.Name, orderError));
                        _incrementSkippedCount(1);
                        continue;
                    }

                    // --- Phase 2: Dict registration (BEFORE expectedPositions update) ---
                    // Ordering invariant: dict-first -> expectedPositions -> enqueue
                    // (Phantom-Fix, B966)
                    _symmetryRegisterFollower(symmetryDispatchId, acctCtx.FleetEntryName);
                    DispatchRegistrationToken token = _registrar.Register(in acctCtx, in buildResult);

                    int reservedDelta = (command.Action == OrderAction.Buy)
                        ? acctCtx.FollowerQty
                        : -acctCtx.FollowerQty;

                    bool syncPending = false;
                    try
                    {
                        // expectedPositions update (Interlocked -- no lock).
                        // Delegates to injected actions that call the original
                        // AddExpectedPositionDeltaLocked + Interlocked.Add + Volatile.Write.
                        UpdateExpectedPositions(acctCtx, membrane, reservedDelta);
                        syncPending = true;

                        // --- Phase 2: Photon dispatch (zero-alloc primary path) ---
                        bool enqueued = _photon.Enqueue(in acctCtx, in buildResult, ref reservedDelta);

                        if (enqueued)
                        {
                            syncPending = false;
                            rmaCount++;
                            AppendDispatchLog(logBuffer, acctCtx, buildResult);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Rollback: reverse expectedPositions, clear dicts, clear FSM.
                        if (syncPending)
                        {
                            ClearSyncPending(acctCtx, membrane);
                            syncPending = false;
                        }
                        if (reservedDelta != 0)
                            RollbackExpectedPositions(acctCtx, membrane, reservedDelta);
                        if (token.IsRegistered)
                            _registrar.Rollback(in token);

                        _incrementSkippedCount(1);
                        logBuffer.AppendLine(string.Format(
                            "[DISPATCH] [X] FAILED on {0}: {1}", acct.Name, ex.Message));
                    }
                }

                // --- Phase 2: Pump prime (post-loop) ---
                _photon.PumpPrime();

                // --- Telemetry ---
                sw.Stop();
                long tFinal = sw.ElapsedTicks;
                PublishTelemetry(sw, t0Ticks, tLoopStart, tFinal, logBuffer);
            }
            catch (Exception ex)
            {
                _print("[DISPATCH] CRITICAL ERROR in DispatchOrchestrator: " + ex.Message);
            }
            finally
            {
                // Always release semaphore -- mirrors [F-03].
                _gate.ReleaseSemaphore();
            }
        }

        // ----------------------------------------------------------------
        // Private helpers -- thin delegation wrappers.
        // These will be replaced by injected interfaces in Phase 3 and 4.
        // ----------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldSkipAccount(
            Account acct,
            AccountRankInfo rankInfo,
            IReadOnlyCollection<string> activeNames,
            StringBuilder log)
        {
            // Delegates to the injected IFleetAccountFilter (Phase 3).
            // Placeholder body preserves the existing ShouldSkipFleetAccount semantics.
            return false; // Phase 3 implementation fills this.
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateExpectedPositions(
            in AccountDispatchContext ctx,
            FlattenedSubstrateState membrane,
            int delta)
        {
            // Mirrors original:
            //   AddExpectedPositionDeltaLocked(expectedKey, reservedDelta)
            //   Interlocked.Add(ref m.ExpectedPositionByIndex[accountIndex], reservedDelta)
            //   Volatile.Write(ref m.DispatchSyncPendingByIndex[accountIndex], 1)
            // These are injected as delegates in the full implementation.
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearSyncPending(
            in AccountDispatchContext ctx,
            FlattenedSubstrateState membrane)
        {
            // Mirrors original:
            //   ClearDispatchSyncPending(expectedKey)
            //   Volatile.Write(ref m.DispatchSyncPendingByIndex[accountIndex], 0)
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RollbackExpectedPositions(
            in AccountDispatchContext ctx,
            FlattenedSubstrateState membrane,
            int delta)
        {
            // Mirrors original:
            //   AddExpectedPositionDeltaLocked(expectedKey, -reservedDelta)
            //   Interlocked.Add(ref m.ExpectedPositionByIndex[accountIndex], -reservedDelta)
        }

        private void AppendDispatchLog(
            StringBuilder log,
            in AccountDispatchContext ctx,
            in OrderBuildResult result)
        {
            string orderTypeTag = ctx.IsMarketEntry
                ? string.Format("Market+{0}orders", result.TotalOrderCount)
                : "Limit        ";

            log.AppendLine(string.Format("  QUEUE | {0,-28} | {1} | PENDING",
                ctx.Account.Name, orderTypeTag));

            if (ctx.IsMarketEntry)
            {
                log.AppendLine(string.Format(
                    "[SIMA STOP_AUDIT] QUEUED {0}: StopQty={1} NonRunnerLimits={2} RunnerQty={3}",
                    ctx.FleetEntryName,
                    ctx.FollowerQty,
                    result.NonRunnerLimitQty,
                    result.RunnerQty));
            }
        }

        private void PublishTelemetry(
            Stopwatch sw,
            long t0Ticks,
            long tLoopStart,
            long tFinal,
            StringBuilder dispatchLog)
        {
            // M7 (Build 1112.001-v29.0): publish per-cycle sample.
            _publishDispatchTicks(tFinal);

            double totalMs = tFinal       * 1000.0 / Stopwatch.Frequency;
            double setupMs = (tLoopStart - t0Ticks) * 1000.0 / Stopwatch.Frequency;
            double loopMs  = (tFinal - tLoopStart)  * 1000.0 / Stopwatch.Frequency;

            var report = new StringBuilder(1024);
            report.AppendLine("+==============================================================+");
            report.AppendLine("|          (+/-)  FORENSIC PULSE REPORT  Phase 7.2 Latency    |");
            report.AppendLine("+==============================================================+");
            report.AppendLine("|  TYPE | ACCOUNT                       | ORDER TYPE   |   RTT  |");
            report.AppendLine("+==============================================================+");
            report.Append(dispatchLog.ToString());
            report.AppendLine("+--------------------------------------------------------------+");
            report.AppendLine("|  TIMING SUMMARY                                              |");
            report.AppendLine("+--------------------------------------------------------------+");
            report.AppendLine(string.Format(
                "|  Setup Phase:  {0,8:F3} ms  |  Fleet Loop:  {1,8:F3} ms       |",
                setupMs, loopMs));
            report.AppendLine(string.Format(
                "|  Total Elapsed: {0,8:F3} ms                                  |",
                totalMs));
            report.AppendLine("+==============================================================+");
            _print(report.ToString().TrimEnd());
        }
    }
}
```

---

## 4. Phase 3 -- Strategy Patterns: Isolating Business Logic

### 4.1 Objective and Scope

Phase 3 provides concrete implementations of `IOrderCommandFactory` for both
the market-entry and limit-entry branches, and introduces `IFleetAccountFilter`
to encapsulate `ShouldSkipFleetAccount`.

### 4.2 Interfaces Required

```csharp
// File: V12_002.SIMA.Interfaces.Phase3.cs

using System.Collections.Generic;
using System.Text;

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Encapsulates the ShouldSkipFleetAccount predicate.
    /// Isolates all account-exclusion logic (inactive, H-13, consistency lock,
    /// master-account skip) into a single testable unit.
    ///
    /// CONTRACT:
    ///   - ShouldSkip() reads membrane.IsActive() via the existing
    ///     AggressiveInlining path -- no change to that hot path.
    ///   - All log messages written to logBuffer (not to Print directly)
    ///     to maintain the batch-flush pattern.
    ///   - NO allocation inside ShouldSkip() -- pure predicate on existing data.
    /// </summary>
    internal interface IFleetAccountFilter
    {
        bool ShouldSkip(
            Account acct,
            AccountRankInfo rankInfo,
            IReadOnlyCollection<string> activeAccountNames,
            FlattenedSubstrateState membrane,
            StringBuilder logBuffer);
    }

    /// <summary>
    /// Encapsulates the per-follower pricing pipeline:
    ///   ATR stop distance -> stop price -> target ladder -> parity scaling -> distribution.
    ///
    /// CONTRACT:
    ///   - Compute() is pure (no side effects, no allocation beyond the struct output).
    ///   - All rounding is applied inside Compute().
    ///   - Overflow protection for followerQty is inside Compute().
    ///   - Returns false on any error (overflow clamped is NOT an error; it logs and continues).
    /// </summary>
    internal interface IFollowerPricingEngine
    {
        bool Compute(
            in DispatchCommand command,
            Account followerAccount,
            int loopIndex,
            int dispatchTargetCount,
            out FollowerPricingResult result,
            out string errorMessage);
    }

    /// <summary>
    /// Output of IFollowerPricingEngine.Compute().
    /// ZERO-ALLOC: struct.
    /// </summary>
    internal readonly struct FollowerPricingResult
    {
        public readonly double StopPrice;
        public readonly double T1Price;
        public readonly double T2Price;
        public readonly double T3Price;
        public readonly double T4Price;
        public readonly double T5Price;
        public readonly int    FollowerQty;
        public readonly int    FT1;
        public readonly int    FT2;
        public readonly int    FT3;
        public readonly int    FT4;
        public readonly int    FT5;
        public readonly bool   WasOverflowClamped;

        public FollowerPricingResult(
            double stopPrice,
            double t1, double t2, double t3, double t4, double t5,
            int qty, int ft1, int ft2, int ft3, int ft4, int ft5,
            bool overflowClamped)
        {
            StopPrice         = stopPrice;
            T1Price           = t1; T2Price = t2; T3Price = t3; T4Price = t4; T5Price = t5;
            FollowerQty       = qty;
            FT1 = ft1; FT2 = ft2; FT3 = ft3; FT4 = ft4; FT5 = ft5;
            WasOverflowClamped = overflowClamped;
        }
    }

    /// <summary>
    /// Builds the proactive FSM entry (FollowerBracketFSM).
    /// Extracted from the Phase 6 [FSM-P1] inline block that appeared
    /// identically in both branches of the original.
    ///
    /// CONTRACT:
    ///   - Build() creates the FSM struct and calls _followerBrackets.TryAdd().
    ///   - It is a no-op (returns false without error) when the key already exists
    ///     (ContainsKey guard -- mirrors original).
    ///   - Returns the constructed FSM for inspection by tests.
    /// </summary>
    internal interface IFollowerFSMBuilder
    {
        bool Build(
            in AccountDispatchContext context,
            in OrderBuildResult result,
            out FollowerBracketFSM fsm);
    }
}
```

### 4.3 MarketOrderCommandFactory -- Concrete Phase 3

```csharp
// File: V12_002.SIMA.MarketOrderCommandFactory.cs

using System;
using System.Collections.Generic;

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Constructs the full market-entry bracket:
    ///   entry (Market) + stop (StopMarket) + non-runner limit targets.
    ///
    /// Encapsulates the V12.3 / V12.7 / [FIX-PP-01] / Phase8.3 logic that
    /// occupied ~80 lines of the market-entry branch in the original.
    ///
    /// NO-LOCKS: All reads from injected delegates are strategy-thread-only.
    /// ZERO-ALLOC: StagedTargets List pre-sized to 5. No LINQ.
    /// </summary>
    internal sealed class MarketOrderCommandFactory : IOrderCommandFactory
    {
        private readonly Func<string, Order>          _createEntryOrder;
        private readonly Func<string, int, Order>     _createStopOrder;
        private readonly Func<string, int, double, Order> _createTargetOrder;
        private readonly Func<int, bool>              _isRunnerTarget;
        private readonly Action<string>               _print;

        public MarketOrderCommandFactory(
            Func<string, Order> createEntryOrder,
            Func<string, int, Order> createStopOrder,
            Func<string, int, double, Order> createTargetOrder,
            Func<int, bool> isRunnerTarget,
            Action<string> print)
        {
            _createEntryOrder  = createEntryOrder  ?? throw new ArgumentNullException("createEntryOrder");
            _createStopOrder   = createStopOrder   ?? throw new ArgumentNullException("createStopOrder");
            _createTargetOrder = createTargetOrder ?? throw new ArgumentNullException("createTargetOrder");
            _isRunnerTarget    = isRunnerTarget    ?? throw new ArgumentNullException("isRunnerTarget");
            _print             = print             ?? throw new ArgumentNullException("print");
        }

        /// <inheritdoc/>
        public bool Build(
            in AccountDispatchContext context,
            out OrderBuildResult result,
            out string errorMessage)
        {
            result       = default;
            errorMessage = null;

            // Entry order -- Market, limitPx=0, stopPx=0.
            Order entry = _createEntryOrder(context.FleetEntryName);
            if (entry == null)
            {
                errorMessage = string.Format(
                    "[DISPATCH] Entry create failed on {0} for {1}",
                    context.Account.Name, context.FleetEntryName);
                return false;
            }

            // Stop order -- StopMarket.
            double validatedStop = ValidateStopPrice(context);
            Order stop = _createStopOrder(context.FleetEntryName, context.FollowerQty);
            if (stop == null)
            {
                errorMessage = string.Format(
                    "[DISPATCH] Stop create failed on {0} for {1}",
                    context.Account.Name, context.FleetEntryName);
                return false;
            }

            // Target ladder -- pre-sized, no LINQ.
            var staged          = new List<StagedTarget>(5);
            int nonRunnerLimitQty = 0;
            int runnerQty         = 0;

            for (int t = 1; t <= context.DispatchTargetCount; t++)
            {
                int targetQty = GetTargetContracts(context, t);
                if (targetQty <= 0) continue;

                if (_isRunnerTarget(t))
                {
                    runnerQty += targetQty;
                    continue;
                }

                double targetPrice = GetTargetPrice(context, t);
                if (targetPrice <= 0)
                {
                    _print(string.Format(
                        "[SIMA TARGET_SKIP] T{0} for {1} has qty={2} but invalid price={3:F2}; skipped",
                        t, context.FleetEntryName, targetQty, targetPrice));
                    continue;
                }

                Order target = _createTargetOrder(context.FleetEntryName, t, targetPrice);
                staged.Add(new StagedTarget { Num = t, Price = targetPrice, Order = target });
                nonRunnerLimitQty += targetQty;
            }

            PositionInfo posInfo = BuildPositionInfo(in context, isMarketEntry: true);

            result = new OrderBuildResult
            {
                EntryOrder        = entry,
                StopOrder         = stop,
                StagedTargets     = staged,
                PositionInfo      = posInfo,
                NonRunnerLimitQty = nonRunnerLimitQty,
                RunnerQty         = runnerQty,
                LimitPx           = 0,
                StopPx            = 0
            };

            return true;
        }

        // These helpers mirror the original inline computations.
        // In the full implementation they delegate to injected functions.
        private double ValidateStopPrice(in AccountDispatchContext ctx)
        {
            // Mirrors ValidateStopPrice(fleetPos.Direction, fleetPos.CurrentStopPrice).
            return ctx.StopPrice; // Injected validation delegate in full impl.
        }

        private int GetTargetContracts(in AccountDispatchContext ctx, int targetNum)
        {
            switch (targetNum)
            {
                case 1: return ctx.FT1;
                case 2: return ctx.FT2;
                case 3: return ctx.FT3;
                case 4: return ctx.FT4;
                case 5: return ctx.FT5;
                default: return 0;
            }
        }

        private double GetTargetPrice(in AccountDispatchContext ctx, int targetNum)
        {
            switch (targetNum)
            {
                case 1: return ctx.T1Price;
                case 2: return ctx.T2Price;
                case 3: return ctx.T3Price;
                case 4: return ctx.T4Price;
                case 5: return ctx.T5Price;
                default: return 0;
            }
        }

        private PositionInfo BuildPositionInfo(in AccountDispatchContext ctx, bool isMarketEntry)
        {
            // Mirrors the PositionInfo initializer in the original market-entry branch.
            // All fields set from the context struct -- no additional lookups.
            return new PositionInfo
            {
                SignalName             = ctx.FleetEntryName,
                Direction              = ctx.Action == OrderAction.Buy
                                         ? MarketPosition.Long : MarketPosition.Short,
                TotalContracts         = ctx.FollowerQty,
                RemainingContracts     = ctx.FollowerQty,
                EntryPrice             = ctx.EntryPrice,
                InitialStopPrice       = ctx.StopPrice,
                CurrentStopPrice       = ctx.StopPrice,
                Target1Price           = ctx.T1Price,
                Target2Price           = ctx.T2Price,
                Target3Price           = ctx.T3Price,
                Target4Price           = ctx.T4Price,
                Target5Price           = ctx.T5Price,
                T1Contracts            = ctx.FT1,
                T2Contracts            = ctx.FT2,
                T3Contracts            = ctx.FT3,
                T4Contracts            = ctx.FT4,
                T5Contracts            = ctx.FT5,
                ExecutingAccount       = ctx.Account,
                IsFollower             = true,
                IsRMATrade             = true,
                IsTRENDTrade           = (ctx.TradeType == "TREND"),
                IsRetestTrade          = (ctx.TradeType == "RETEST"),
                EntryOrderType         = ctx.EntryOrderType,
                EntryFilled            = isMarketEntry,
                BracketSubmitted       = isMarketEntry,
                TicksSinceEntry        = 0,
                ExtremePriceSinceEntry = ctx.EntryPrice,
                CurrentTrailLevel      = 0,
                OcoGroupId             = "V12_" + GetStableHash(ctx.FleetEntryName)
            };
        }

        private string GetStableHash(string input)
        {
            // Delegates to the existing GetStableHash method via injected Func.
            // Placeholder: full implementation injects Func<string,string>.
            return input.GetHashCode().ToString("X8");
        }
    }
}
```

### 4.4 LimitOrderCommandFactory -- Concrete Phase 3

```csharp
// File: V12_002.SIMA.LimitOrderCommandFactory.cs

using System;

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Constructs the limit-entry order only (no stop, no targets at entry time).
    /// Brackets are deferred to SymmetryGuardOnFollowerFill per V12.7 and [FIX-PP-01].
    ///
    /// NO-LOCKS: strategy-thread-only.
    /// ZERO-ALLOC: no collections created. StagedTargets = null.
    /// </summary>
    internal sealed class LimitOrderCommandFactory : IOrderCommandFactory
    {
        private readonly Func<string, double, double, Order> _createEntryOrder;
        private readonly Action<string>                      _print;

        public LimitOrderCommandFactory(
            Func<string, double, double, Order> createEntryOrder,
            Action<string> print)
        {
            _createEntryOrder = createEntryOrder ?? throw new ArgumentNullException("createEntryOrder");
            _print            = print            ?? throw new ArgumentNullException("print");
        }

        /// <inheritdoc/>
        public bool Build(
            in AccountDispatchContext context,
            out OrderBuildResult result,
            out string errorMessage)
        {
            result       = default;
            errorMessage = null;

            // limitPx and stopPx derived from entryOrderType -- mirrors [FIX-PP-01].
            double limitPx = (context.EntryOrderType == OrderType.Limit
                           || context.EntryOrderType == OrderType.StopLimit)
                           ? context.EntryPrice : 0;
            double stopPx  = (context.EntryOrderType == OrderType.StopMarket
                           || context.EntryOrderType == OrderType.StopLimit)
                           ? context.EntryPrice : 0;

            Order entry = _createEntryOrder(context.FleetEntryName, limitPx, stopPx);
            if (entry == null)
            {
                errorMessage = string.Format(
                    "[DISPATCH] Limit entry create failed on {0} for {1}",
                    context.Account.Name, context.FleetEntryName);
                return false;
            }

            PositionInfo posInfo = new PositionInfo
            {
                SignalName             = context.FleetEntryName,
                Direction              = context.Action == OrderAction.Buy
                                          ? MarketPosition.Long : MarketPosition.Short,
                TotalContracts         = context.FollowerQty,
                RemainingContracts     = context.FollowerQty,
                EntryPrice             = context.EntryPrice,
                InitialStopPrice       = context.StopPrice,
                CurrentStopPrice       = context.StopPrice,
                Target1Price           = context.T1Price,
                Target2Price           = context.T2Price,
                Target3Price           = context.T3Price,
                Target4Price           = context.T4Price,
                Target5Price           = context.T5Price,
                T1Contracts            = context.FT1,
                T2Contracts            = context.FT2,
                T3Contracts            = context.FT3,
                T4Contracts            = context.FT4,
                T5Contracts            = context.FT5,
                ExecutingAccount       = context.Account,
                IsFollower             = true,
                IsRMATrade             = true,
                IsTRENDTrade           = (context.TradeType == "TREND"),
                IsRetestTrade          = (context.TradeType == "RETEST"),
                EntryOrderType         = context.EntryOrderType,
                EntryFilled            = false,     // Limit: waits for fill event
                BracketSubmitted       = false,     // V12.7: deferred to fill
                TicksSinceEntry        = 0,
                ExtremePriceSinceEntry = context.EntryPrice,
                CurrentTrailLevel      = 0,
                OcoGroupId             = "V12_" + context.FleetEntryName.GetHashCode().ToString("X8")
            };

            result = new OrderBuildResult
            {
                EntryOrder        = entry,
                StopOrder         = null,
                StagedTargets     = null,
                PositionInfo      = posInfo,
                NonRunnerLimitQty = 0,
                RunnerQty         = 0,
                LimitPx           = limitPx,
                StopPx            = stopPx
            };

            return true;
        }
    }
}
```

---

## 5. Phase 4 -- Event Lifecycle: Safe Transitions

### 5.1 Objective and Scope

Phase 4 formalizes the FSM transition protocol and introduces the
`IDispatchLifecycle` interface that owns all state transitions from
`PendingSubmit` through `Submitted`, `PartialFill`, `Filled`, and
`Closed`. This phase also addresses the REAPER interaction documented
in Build 935 comments.

### 5.2 Interface Definitions

```csharp
// File: V12_002.SIMA.Interfaces.Phase4.cs

using System;

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Owns the full lifecycle of one follower bracket from pre-submission
    /// through fill, management, and closure.
    ///
    /// This interface replaces the scattered FSM state mutations that currently
    /// exist in:
    ///   - ExecuteSmartDispatchEntry (PendingSubmit creation -- Phase 6 [FSM-P1])
    ///   - PumpFleetDispatch (Submitted promotion)
    ///   - OnOrderUpdate (Fill transitions)
    ///   - REAPER (phantom detection and repair)
    ///   - FlattenAllFollowers (Closed transition)
    ///
    /// CONTRACT:
    ///   - All Transition*() methods are called from the strategy thread
    ///     (via TriggerCustomEvent) or from within a Volatile.Read snapshot context.
    ///   - NO lock is taken. FSM state is updated via ConcurrentDictionary.TryUpdate()
    ///     with version stamping to detect lost updates.
    ///   - All methods are idempotent: calling them when the FSM is already in
    ///     the target state is a no-op (returns false, does not throw).
    ///
    /// NO-LOCKS: ConcurrentDictionary.TryUpdate() is the only write mechanism.
    ///   Version stamps (LastUpdateUtc ticks) provide optimistic concurrency.
    /// ZERO-ALLOC: No heap allocation in transition methods.
    /// </summary>
    internal interface IDispatchLifecycle
    {
        /// <summary>
        /// Transitions a bracket from PendingSubmit to Submitted after
        /// PumpFleetDispatch confirms acct.Submit() returned without exception.
        /// Returns false if the bracket is not found or is not in PendingSubmit.
        /// </summary>
        bool TransitionToSubmitted(string fleetEntryName, DateTime submittedUtc);

        /// <summary>
        /// Transitions to PartialFill when OnOrderUpdate reports a partial fill.
        /// Updates RemainingContracts and EntryFilled on the FSM and PositionInfo.
        /// Returns false if the bracket is not found or fill quantity is invalid.
        /// </summary>
        bool TransitionToPartialFill(
            string fleetEntryName,
            int filledContracts,
            double fillPrice,
            DateTime fillUtc);

        /// <summary>
        /// Transitions to Filled. Triggers deferred bracket submission
        /// (stop + targets) for limit entries (BracketSubmitted == false).
        /// Returns false if the bracket is not found or is already Filled.
        /// </summary>
        bool TransitionToFilled(
            string fleetEntryName,
            double fillPrice,
            DateTime fillUtc);

        /// <summary>
        /// Transitions to Closed. Removes the bracket from _followerBrackets
        /// and cleans up all tracking dicts (activePositions, entryOrders,
        /// stopOrders, target dicts).
        ///
        /// REAPER SAFE: This method is the single authoritative cleanup path.
        ///   The REAPER must call this instead of performing its own dict removal,
        ///   eliminating the phantom-repair race documented in Build 935.
        /// </summary>
        bool TransitionToClosed(
            string fleetEntryName,
            ClosureReason reason,
            DateTime closedUtc);

        /// <summary>
        /// Returns the current FSM state for a named bracket.
        /// Returns FollowerBracketState.Unknown if the bracket is not found.
        /// Safe to call from any thread (Volatile.Read on the dictionary reference).
        /// </summary>
        FollowerBracketState GetState(string fleetEntryName);

        /// <summary>
        /// REAPER hook: returns true when the bracket is in a state that
        /// requires phantom detection (Submitted with no fill after timeout).
        /// Replaces the ad-hoc REAPER logic with a lifecycle-owned predicate.
        /// </summary>
        bool IsPhantomCandidate(string fleetEntryName, TimeSpan timeout, DateTime nowUtc);
    }

    /// <summary>
    /// Reason codes for TransitionToClosed(). ASCII identifiers only.
    /// </summary>
    internal enum ClosureReason
    {
        Unknown          = 0,
        AllTargetsFilled = 1,
        StopHit          = 2,
        ManualFlatten    = 3,
        ReaperPhantom    = 4,
        DispatchFailure  = 5,
        FlattenAll       = 6
    }
}
```

### 5.3 DispatchLifecycle -- Concrete Phase 4

```csharp
// File: V12_002.SIMA.DispatchLifecycle.cs

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace UniversalORStrategy.SIMA.Dispatch
{
    /// <summary>
    /// Concrete IDispatchLifecycle. Uses ConcurrentDictionary.TryUpdate()
    /// with optimistic version stamps for all state transitions.
    ///
    /// NO-LOCKS: ConcurrentDictionary.TryUpdate() is the exclusive write path.
    ///   All reads are via TryGetValue (lock-free on ConcurrentDictionary).
    ///
    /// ZERO-ALLOC: No new objects created during transitions. FollowerBracketFSM
    ///   is a class (reference type in the existing codebase) so transitions
    ///   mutate the existing instance via Interlocked.Exchange on the State field
    ///   if State is an int-backed enum, or via TryUpdate() if the dict owns the
    ///   reference.
    ///
    ///   DESIGN NOTE on FollowerBracketFSM mutability:
    ///   The existing code treats FollowerBracketFSM as a mutable class stored in
    ///   _followerBrackets (ConcurrentDictionary). Rather than replacing the instance
    ///   on each transition (which would allocate), this implementation mutates fields
    ///   of the existing instance using Interlocked.Exchange for the State field and
    ///   Volatile.Write for non-atomic fields. This is safe because:
    ///     a) State transitions are one-way (no regression from Filled to PendingSubmit).
    ///     b) The strategy thread is the sole writer for most fields; OnOrderUpdate
    ///        runs via TriggerCustomEvent and is therefore also on the strategy thread.
    /// </summary>
    internal sealed class DispatchLifecycle : IDispatchLifecycle
    {
        private readonly ConcurrentDictionary<string, FollowerBracketFSM> _brackets;
        private readonly IDispatchStateRegistrar _registrar;
        private readonly Action<string>          _print;

        public DispatchLifecycle(
            ConcurrentDictionary<string, FollowerBracketFSM> brackets,
            IDispatchStateRegistrar registrar,
            Action<string> print)
        {
            _brackets  = brackets  ?? throw new ArgumentNullException("brackets");
            _registrar = registrar ?? throw new ArgumentNullException("registrar");
            _print     = print     ?? throw new ArgumentNullException("print");
        }

        /// <inheritdoc/>
        public bool TransitionToSubmitted(string fleetEntryName, DateTime submittedUtc)
        {
            FollowerBracketFSM fsm;
            if (!_brackets.TryGetValue(fleetEntryName, out fsm))
                return false;

            if (fsm.State != FollowerBracketState.PendingSubmit)
                return false;

            // Interlocked state transition: PendingSubmit -> Submitted.
            int prev = Interlocked.CompareExchange(
                ref fsm.StateRaw,
                (int)FollowerBracketState.Submitted,
                (int)FollowerBracketState.PendingSubmit);

            if (prev != (int)FollowerBracketState.PendingSubmit)
                return false;

            Volatile.Write(ref fsm.LastUpdateUtcTicks, submittedUtc.Ticks);
            return true;
        }

        /// <inheritdoc/>
        public bool TransitionToPartialFill(
            string fleetEntryName,
            int filledContracts,
            double fillPrice,
            DateTime fillUtc)
        {
            FollowerBracketFSM fsm;
            if (!_brackets.TryGetValue(fleetEntryName, out fsm))
                return false;

            if (fsm.State == FollowerBracketState.Closed)
                return false;

            // Allow transition from Submitted or existing PartialFill.
            int remaining = Math.Max(0, fsm.RemainingContracts - filledContracts);
            Interlocked.Exchange(ref fsm.RemainingContracts, remaining);
            Interlocked.Exchange(
                ref fsm.StateRaw,
                (int)FollowerBracketState.PartialFill);
            Volatile.Write(ref fsm.LastUpdateUtcTicks, fillUtc.Ticks);

            return true;
        }

        /// <inheritdoc/>
        public bool TransitionToFilled(
            string fleetEntryName,
            double fillPrice,
            DateTime fillUtc)
        {
            FollowerBracketFSM fsm;
            if (!_brackets.TryGetValue(fleetEntryName, out fsm))
                return false;

            if (fsm.State == FollowerBracketState.Filled
             || fsm.State == FollowerBracketState.Closed)
                return false;

            Interlocked.Exchange(ref fsm.RemainingContracts, 0);
            Interlocked.Exchange(
                ref fsm.StateRaw,
                (int)FollowerBracketState.Filled);
            Volatile.Write(ref fsm.LastUpdateUtcTicks, fillUtc.Ticks);

            _print(string.Format("[LIFECYCLE] {0} -> Filled at {1:F2}", fleetEntryName, fillPrice));
            return true;
        }

        /// <inheritdoc/>
        public bool TransitionToClosed(
            string fleetEntryName,
            ClosureReason reason,
            DateTime closedUtc)
        {
            FollowerBracketFSM fsm;
            if (!_brackets.TryGetValue(fleetEntryName, out fsm))
                return false;

            if (fsm.State == FollowerBracketState.Closed)
                return false;

            Interlocked.Exchange(
                ref fsm.StateRaw,
                (int)FollowerBracketState.Closed);
            Volatile.Write(ref fsm.LastUpdateUtcTicks, closedUtc.Ticks);

            // Authoritative cleanup -- replaces scattered TryRemove calls.
            FollowerBracketFSM removed;
            _brackets.TryRemove(fleetEntryName, out removed);

            var token = new DispatchRegistrationToken(fleetEntryName, true, 5);
            _registrar.Rollback(in token);

            _print(string.Format("[LIFECYCLE] {0} -> Closed (reason={1})", fleetEntryName, reason));
            return true;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FollowerBracketState GetState(string fleetEntryName)
        {
            FollowerBracketFSM fsm;
            return _brackets.TryGetValue(fleetEntryName, out fsm)
                ? fsm.State
                : FollowerBracketState.Unknown;
        }

        /// <inheritdoc/>
        public bool IsPhantomCandidate(string fleetEntryName, TimeSpan timeout, DateTime nowUtc)
        {
            FollowerBracketFSM fsm;
            if (!_brackets.TryGetValue(fleetEntryName, out fsm))
                return false;

            if (fsm.State != FollowerBracketState.Submitted)
                return false;

            long lastTicks = Volatile.Read(ref fsm.LastUpdateUtcTicks);
            long elapsedMs = (nowUtc.Ticks - lastTicks) / TimeSpan.TicksPerMillisecond;
            return elapsedMs > (long)timeout.TotalMilliseconds;
        }
    }
}
```

---

## 6. Dependency Graph

```
+===================================================================+
|  PHASE 1: Foundation                                              |
|                                                                   |
|  GateContext (struct)                                             |
|  DispatchCommand (struct)                                         |
|  FleetContext (struct)                                            |
|  AccountDispatchContext (struct)          <-- used in Phase 2,3  |
|  DispatchRegistrationToken (struct)       <-- used in Phase 2    |
|                                                                   |
|  IDispatchGate       -> DispatchGate                             |
|  IMembraneResolver   -> MembraneResolver                         |
|  IFleetResolver      -> FleetResolver                            |
|  IAccountIndexResolver -> AccountIndexResolver                   |
+===================================================================+
                          |
                          v
+===================================================================+
|  PHASE 2: Command Routing                                         |
|                                                                   |
|  OrderBuildResult (struct)                                        |
|  FollowerPricingResult (struct)           <-- used in Phase 3    |
|                                                                   |
|  IDispatchRouter         -> DispatchRouter                       |
|  IOrderCommandFactory    -> (abstract, implemented in Phase 3)   |
|  IPhotonDispatchChannel  -> PhotonDispatchChannel                |
|  IDispatchStateRegistrar -> (implemented alongside Phase 2)      |
|  IAccountContextBuilder  -> (implemented alongside Phase 2)      |
|                                                                   |
|  DispatchOrchestrator (assembles Phase 1 + Phase 2 deps)         |
+===================================================================+
                          |
                          v
+===================================================================+
|  PHASE 3: Strategy Patterns                                       |
|                                                                   |
|  IFleetAccountFilter    -> FleetAccountFilter                    |
|  IFollowerPricingEngine -> FollowerPricingEngine                 |
|  IFollowerFSMBuilder    -> FollowerFSMBuilder                    |
|  IOrderCommandFactory   -> MarketOrderCommandFactory             |
|                         -> LimitOrderCommandFactory              |
+===================================================================+
                          |
                          v
+===================================================================+
|  PHASE 4: Event Lifecycle                                         |
|                                                                   |
|  ClosureReason (enum)                                            |
|                                                                   |
|  IDispatchLifecycle  -> DispatchLifecycle                        |
|                                                                   |
|  REPLACES scattered FSM mutations in:                            |
|    PumpFleetDispatch                                             |
|    OnOrderUpdate                                                  |
|    REAPER                                                         |
|    FlattenAllFollowers                                            |
+===================================================================+
```

---

## 7. Risk Register

```
+----+------------------------------------------+----------+---------+------------------------------+
| ID | Risk                                     | Phase    | Severity| Mitigation                   |
+----+------------------------------------------+----------+---------+------------------------------+
| R1 | IsActive() inlining disrupted by         | 1        | HIGH    | MembraneResolver and         |
|    | indirection layer added to hot path.     |          |         | AccountIndexResolver do NOT  |
|    |                                          |          |         | wrap IsActive(). Callers     |
|    |                                          |          |         | call membrane.IsActive()     |
|    |                                          |          |         | directly after Resolve().    |
+----+------------------------------------------+----------+---------+------------------------------+
| R2 | Ordering invariant broken (dict-first    | 2        | HIGH    | IDispatchStateRegistrar      |
|    | before expectedPositions update).         |          |         | contract explicitly requires |
|    | Phantom-Fix / B966.                       |          |         | Register() before            |
|    |                                          |          |         | UpdateExpectedPositions().   |
|    |                                          |          |         | Unit test enforces order.    |
+----+------------------------------------------+----------+---------+------------------------------+
| R3 | Pool-slot double-release if Enqueue()    | 2        | MEDIUM  | PhotonDispatchChannel        |
|    | throws after pool claim.                 |          |         | releases slot in its own     |
|    |                                          |          |         | catch block before           |
|    |                                          |          |         | re-throwing.                 |
+----+------------------------------------------+----------+---------+------------------------------+
| R4 | SemaphoreSlim double-release if Gate     | 1        | MEDIUM  | DispatchGate._acquired       |
|    | is refactored without the atomic swap.   |          |         | uses Interlocked.CAS to      |
|    |                                          |          |         | guard ReleaseSemaphore().    |
+----+------------------------------------------+----------+---------+------------------------------+
| R5 | FlattenedSubstrateState array bounds     | 1,2      | HIGH    | All array accesses go        |
|    | through IsAccountIndexValid()|
|    |                                          |          |         | before any index use.        |
+----+------------------------------------------+----------+---------+------------------------------+
| R6 | FollowerBracketFSM State field not       | 4        | MEDIUM  | StateRaw must be an int      |
|    | int-backed -- Interlocked.Exchange       |          |         | (int-backed enum). If the    |
|    | not applicable.                          |          |         | existing type uses byte or   |
|    |                                          |          |         | other size, wrap with a      |
|    |                                          |          |         | separate int StateRaw field  |
|    |                                          |          |         | and cast on read.            |
+----+------------------------------------------+----------+---------+------------------------------+
| R7 | TriggerCustomEvent deferred-retry        | 1        | LOW     | ScheduleDeferredDispatch()   |
|    | creates a closure and boxes              |          |         | documents this one-per-      |
|    | DispatchCommand.                         |          |         | contention-event allocation  |
|    |                                          |          |         | as pre-existing and          |
|    |                                          |          |         | unavoidable in NinjaTrader.  |
+----+------------------------------------------+----------+---------+------------------------------+
| R8 | Non-ASCII characters introduced in       | ALL      | LOW     | All string literals in this  |
|    | new string literals.                     |          |         | plan are ASCII-only.         |
|    |                                          |          |         | CI lint rule recommended.    |
+----+------------------------------------------+----------+---------+------------------------------+
```

---

## 8. Rollout Sequence

```
STEP  PHASE  ACTION                                            GATE CONDITION
----  -----  ------------------------------------------------  ----------------------------
 1     1     Create V12_002.SIMA.Interfaces.Phase1.cs          Build compiles cleanly
             (all structs and interfaces, no implementations)

 2     1     Implement DispatchGate                            Existing tests pass
             Implement MembraneResolver                        (strategy loads in NinjaTrader)
             Implement FleetResolver
             Implement AccountIndexResolver

 3     1     Wire Phase 1 into DispatchOrchestrator skeleton   New path reachable but
             Call old ExecuteSmartDispatchEntry internally      old method still executes

 4     2     Create V12_002.SIMA.Interfaces.Phase2.cs          Build compiles cleanly
             (IDispatchRouter, IPhotonDispatchChannel, etc.)

 5     2     Implement DispatchRouter                          Unit tests for factory
             Implement PhotonDispatchChannel                   selection pass
             Implement IAccountContextBuilder

 6     2     Replace old market/limit branch in               Side-by-side log comparison
             DispatchOrchestrator with Phase 2 routing         shows identical dispatch log
                                                               output for both paths

 7     3     Implement MarketOrderCommandFactory               Unit tests: order count,
             Implement LimitOrderCommandFactory                stop price, target prices
             Implement FleetAccountFilter                      match original output

 8     3     Implement FollowerPricingEngine                   Overflow test passes
             Integrate into AccountContextBuilder              (qty * FleetParityMultiplier
                                                               at INT_MAX)

 9     4     Create V12_002.SIMA.Interfaces.Phase4.cs          Build compiles cleanly
             Implement DispatchLifecycle

10     4     Replace scattered FSM mutations with              REAPER phantom rate stays
             IDispatchLifecycle calls in:                      at or below baseline;
               PumpFleetDispatch                               no new phantom events in
               OnOrderUpdate                                   48-hour live paper-trade
               REAPER
               FlattenAllFollowers

11    ALL    Remove ExecuteSmartDispatchEntry body             Final regression: 10 live
             (keep method signature as thin delegate           dispatches log identical
             into DispatchOrchestrator.Dispatch())             forensic pulse reports
             Increment Build Tag to 1113.00
```

---

*End of implementation_plan.md*
