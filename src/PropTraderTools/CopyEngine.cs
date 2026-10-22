// PTT-COPIER-B57-T1 -- CopyEngine.cs
// B57 T1 CHANGES:
//   1. SendCopy: capture CreateOrder return value + call follower.Submit(new[]{order}).
//      Root cause fix: AddOn CreateOrder leaves order at Initialized; Submit() sends to exchange.
//      Pattern: same as SubmitBeStop (line ~400). DW-B57-01.
// PTT-COPIER B57 | submit-after-create-order | 2026-08-10
// PTT-COPIER-B56-LaneA-T1 -- CopyEngine.cs
// B56 T1 CHANGES:
//   1. Added IsDispatchTriggerState(OrderState) -- internal static predicate, CYC=2. (DW-B56-01 Gap 1)
//   2. DispatchCopy Gate 3: replaced raw Submitted check with IsDispatchTriggerState. (DW-B56-01 Gap 1)
//   3. OnOrderUpdate Cancelled block: propagate leader cancel to follower entry orders. (DW-B56-01 Gap 2)
// PTT-COPIER B56 | limit-order-gate3-fix + leader-cancel-propagation | 2026-08-09
// PTT-COPIER-B14-T1 -- CopyEngine.cs
// B14 T1 CHANGES:
//   1. Added trail BE fields (B27: replaced with _trailBeSlots + _trailBeLastPnlBits per DW-B27-01).
//   2. Added ArmTrailBe(Instrument, Account, int) -- CYC=4.
//   3. Added DisarmTrailBe(Account) -- CYC=4.
//   4. Added OnTrailBeAccountUpdate -- CYC=5, fires on NT8 account bg thread.
// PTT-COPIER-B12-T1 -- CopyEngine.cs
// B12 T1 CHANGES:
//   1. Added Trim(Instrument, int, double) overload -- limit exit ceil(qty/2). "PTT-TrimLimit".
//   2. Added Flatten(Instrument, int, double) overload -- limit exit full qty. "PTT-FlattenLimit".
//   3. Added PTT-prefix Gate 0.5 at top of DispatchCopy -- prevents cascade copy of PTT- signals.
//      CYC: 7 -> 8 (AT LIMIT; PASS).
// PTT-COPIER-B10-T3 -- CopyEngine.cs
// Pure logic singleton. Zero UI references. Both surfaces share this instance.
// Jane Street rules: JS-001, JS-003, JS-008, JS-010, JS-021, JS-023, JS-025
// B8 T1: per-account qty multiplier (DW-B7-01)
// B8 T2: FollowerAtmMode behavioral wiring (DW-B7-03)
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;

// B113 test seam: grants PropTraderTools.Tests access to internal members
// (_qxPendingFollowerCleanup, TryCleanupReArmedAtmBracket).
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]

namespace PropTraderTools
{
    // V01: Binding record for _orderMap inner collection
    // JS-003: readonly struct prevents field transposition
    internal struct FollowerBinding
    {
        internal Account FollowerAccount { get; private set; }
        internal string FromEntrySignalName { get; private set; }

        internal FollowerBinding(Account account, string signalName)
        {
            FollowerAccount = account;
            FromEntrySignalName = signalName;
        }
    }

    // V05: Position truth snapshot -- JS-003 (readonly struct prevents bool transposition)
    public struct PositionState
    {
        public bool HasOpenPosition { get; private set; }
        public bool HasWorkingEntries { get; private set; }

        public PositionState(bool hasOpen, bool hasWorking)
        {
            HasOpenPosition = hasOpen;
            HasWorkingEntries = hasWorking;
        }
    }

    // V06: ATM mode discriminated union -- JS-003 + JS-010
    // NT8 Roslyn: records with positional params generate IsExternalInit (CS0518). Use abstract class instead.
    public abstract class FollowerAtmMode
    {
        private FollowerAtmMode() { } // JS-010: private base constructor -- no external subclassing

        public sealed class Inherit : FollowerAtmMode
        {
            public Inherit() { }
        } // B7 default

        public sealed class Market : FollowerAtmMode
        {
            public Market() { }
        } // pure market

        public sealed class Named : FollowerAtmMode
        {
            public string TemplateName { get; }

            // HOTFIX-B66-ATM-OBJ: AtmObject carries the live ChartTrader.AtmStrategy instance.
            // When non-null, SendCopyWithAtm uses StartAtmStrategy(atm, order) object overload
            // instead of StartAtmStrategy(string, order) -- avoids reading .Name (returns class name).
            public NinjaTrader.NinjaScript.AtmStrategy AtmObject { get; }

            public Named(string templateName)
            {
                TemplateName = templateName;
                AtmObject = null;
            }

            public Named(string templateName, NinjaTrader.NinjaScript.AtmStrategy atmObj)
            {
                TemplateName = templateName;
                AtmObject = atmObj;
            }
        }
    }

    // B8: SendCopy switch + UI dropdown wired in T2.

    // B9 T3 -- Copy mode discriminated union (JS-023: volatile int backing for thread-safe reads/writes)
    internal enum CopyMode
    {
        Signal = 0,
        Mirror = 1,
        Clone = 2,
    }

    // B12 T1 -- BE visual state (Idle/Armed/Connected) -- 3-state FSM used by TradeCopierPanel BE ALL button.
    // Declared in CopyEngine namespace so all Panel instances share the same type reference.
    internal enum BeState { Idle, Armed, Connected }

    internal sealed class CopyEngine : ICopyEngine
    {
        // --- Singleton ---
        private static readonly CopyEngine _instance = new CopyEngine();
        public static CopyEngine Instance => _instance;

        // --- State ---
        private volatile bool _isCopyEnabled; // JS-023

        // B9 T3 -- Mirror mode (JS-023: volatile int backing for CopyMode enum)
        private volatile int _copyModeValue = 0; // 0=Signal (default), 1=Mirror

        // B50 -- _cloneAtmCache: volatile string holds ATM template name for display/logging only.
        // volatile string: reference-type writes are atomic on CLR 4.0+ (JS-023 compliant).
        // NT8-003: volatile double/float BANNED -- string is safe.
        private volatile string _cloneAtmCache = string.Empty;

        // HOTFIX-B66-ATM-OBJ: volatile reference to live ChartTrader.AtmStrategy object.
        // Captured at Clone mode click; used in StartAtmStrategy(atm, order) object overload.
        // volatile object reference write/read: atomic on CLR 4.0+ (JS-023 compliant).
        private volatile NinjaTrader.NinjaScript.AtmStrategy _cloneAtmObject = null;

        // BGTM-1: Feature flags -- volatile reference (atomic on CLR 4.0+, JS-023 compliant).
        // SetFlags called from UI thread only. Read from UI thread only.
        // DEV-UNLOCK: default Elite() until productization (mirrors LoadAndValidateLicense change).
        private volatile FeatureFlags _flags = FeatureFlags.Elite();

        /// <summary>Current feature flags snapshot.</summary>
        public FeatureFlags Flags => _flags;

        /// <summary>Fires on UI thread when license activation changes flags.</summary>
        public event Action<FeatureFlags> FeatureFlagsChanged;

        // BGTM-1: Assign flags and broadcast. CYC=1. JS-021: no lock.
        internal void SetFlags(FeatureFlags f)
        {
            _flags = f;
            FeatureFlagsChanged?.Invoke(f);
        }

        // B39 -- _globalBe: singleton reference to shared Global BE execution engine.
        // Lazily initialized; Panel and Window read via GlobalBe property (UI thread only).
        // JS-023: volatile null-check safe for singleton reads on CLR 4.0+.
        private PttGlobalBreakEven _globalBe = null;

        // B62: value changed from long (timestamp) to double (last dispatched LimitPrice).
        // Enables drag detection: same orderId + different price = leader dragged.
        // JS-025: ConcurrentDictionary is lock-free.
        private readonly ConcurrentDictionary<string, double> _dedupCache =
            new ConcurrentDictionary<string, double>(); // JS-025

        // DW-B91-A: per-orderId dispatch guard -- survives EvictDedup terminal-state eviction.
        // After DispatchCopy commits a copy dispatch for orderId, TryAdd records it here.
        // On a second dispatch-triggering event for the same orderId (e.g. Rithmic re-submit),
        // ContainsKey returns true before DispatchCopy can fire again.
        // Eviction is co-located with _dedupCache eviction in EvictDedup -- both cleared on
        // Filled/Cancelled/Rejected so the slot is reclaimed when the order lifecycle closes.
        // Key = order.OrderId.ToString(). Value = byte (minimum footprint -- presence-only set).
        // JS-021: ConcurrentDictionary.ContainsKey and TryAdd are lock-free atomic operations.
        // JS-025: ConcurrentDictionary is the canonical lock-free set pattern.
        private readonly ConcurrentDictionary<string, byte> _entryDispatchedOrders =
            new ConcurrentDictionary<string, byte>();

        // DW-B142-MGC-02: instrument-level dispatch guard.
        // Key = instrFullName + "|" + OrderAction (e.g. "MGC DEC26|Sell").
        // Set on first Gate 5 pass. Cleared on Cancelled (no-fill cancel) via companion map,
        // or on PositionStateChanged flat (safety net). NOT cleared on Filled -- trade is live.
        // JS-025: ConcurrentDictionary is lock-free. JS-021: no lock.
        private readonly ConcurrentDictionary<string, byte> _liveEntryInstruments =
            new ConcurrentDictionary<string, byte>();

        // DW-B142-MGC-02: maps dispatched orderId -> instrKey.
        // Written in IsLiveEntryBlocked at Gate 5 pass time.
        // Used by EvictDedup(Cancelled) to clean up _liveEntryInstruments on no-fill cancel.
        // Key = orderId, Value = instrKey. JS-025: ConcurrentDictionary. JS-021: no lock.
        private readonly ConcurrentDictionary<string, string> _entryInstrKeyByOrderId =
            new ConcurrentDictionary<string, string>();

        // DW-B136 Gap B: leader order ID -> follower Order objects dispatched for that leader order.
        // Key = leader order.OrderId.ToString() (same format as _dedupCache and _entryDispatchedOrders).
        // Value = ConcurrentBag<Order> of follower Order objects submitted for this leader order.
        // Used by TryCancelFollowerEntries to scope cancel to the specific leader order being cancelled.
        // JS-021: no lock. JS-025: ConcurrentDictionary + ConcurrentBag (lock-free).
        // JS-001: only cancel calls are wrapped in try/catch in CancelScopedFollowerEntries.
        // Eviction: TryRemove called in CancelScopedFollowerEntries (cancel path) after iterating the bag.
        // NOTE: EvictDedup does NOT touch this map -- see execution-order note in LaneB-02-architecture-plan.md.
        internal readonly ConcurrentDictionary<string, ConcurrentBag<Order>> _followerCopyMap =
            new ConcurrentDictionary<string, ConcurrentBag<Order>>();

        // DW-B92: count of PTT-BE-Target-* fills per account for this trade slot.
        // Incremented synchronously in OnOrderUpdate BEFORE the OCO cancel event
        // arrives, eliminating the HasFilledBeTarget acc.Orders scan race.
        // Key = acc.Name. Cleared in TryEvictFollowerBeSlot on position flat.
        // JS-025: ConcurrentDictionary is lock-free. JS-021: no lock.
        private readonly ConcurrentDictionary<string, int> _filledBeTargetCount =
            new ConcurrentDictionary<string, int>();
        private ConcurrentBag<CopyRule> _rules = new ConcurrentBag<CopyRule>(); // Change 1: removed readonly

        // B127: lazy-resolve cache -- name -> Account. Populated on first successful resolve in
        // AllAccounts(). Lock-free: ConcurrentDictionary TryGetValue + TryAdd (JS-021 compliant).
        // Cleared on each LoadRules() call to handle account reconnect / session restart scenarios.
        // readonly: ConcurrentDictionary is a reference type; .Clear() works on the instance.
        private readonly ConcurrentDictionary<string, Account> _resolvedFollowers =
            new ConcurrentDictionary<string, Account>(StringComparer.Ordinal);
        private double _dailyCapFloor = -500.0; // Change 4

        // DW-B181-G5: ATM template Reverse check -- tracks which templates were already warned this session.
        // Written only at State.Configure (single-threaded startup). No lock needed (JS-021).
        private readonly HashSet<string> _warnedAtmTemplates = new HashSet<string>();

        // B27 -- Per-account BE slot structs (DW-B27-01: replaces singleton fields).
        // NT8-001: 'readonly' fields, NOT init setters. NT8-005: NOT 'readonly struct'.
        // NT8-004: struct in ConcurrentDictionary<string,TSlot> confirmed safe in NT8.
        // internal: required for internal helper methods (IsPendingBeSlotActive, IsPendingBeTriggerConditionMet)
        // that accept PendingBeSlot as parameter -- CS0051 requires parameter type >= method accessibility.
        internal struct PendingBeSlot
        {
            internal readonly Account Account;
            internal readonly Instrument Instrument;
            internal readonly int BufferTicks;

            internal PendingBeSlot(Account a, Instrument i, int b)
            {
                Account = a;
                Instrument = i;
                BufferTicks = b;
            }
        }

        private struct TrailBeSlot
        {
            internal readonly Account Account;
            internal readonly Instrument Instrument;
            internal readonly int BufferTicks;

            internal TrailBeSlot(Account a, Instrument i, int b)
            {
                Account = a;
                Instrument = i;
                BufferTicks = b;
            }
        }

        // B27 -- Pending BE slot dictionary (DW-B27-01: replaces 4 singleton fields).
        // Key = account.Name. JS-021: TryGetValue/TryRemove/AddOrUpdate are lock-free.
        private readonly ConcurrentDictionary<string, PendingBeSlot> _pendingBeSlots =
            new ConcurrentDictionary<string, PendingBeSlot>();

        // B27 -- Trail BE slot dictionary (DW-B27-01: replaces 5 singleton fields).
        // LastPnlBits lives in _trailBeLastPnlBits (separate dict) because struct values
        // in ConcurrentDictionary are value types -- Interlocked CAS requires a ref to a
        // field, impossible on a boxed struct. NT8-003: no volatile on long.
        private readonly ConcurrentDictionary<string, TrailBeSlot> _trailBeSlots =
            new ConcurrentDictionary<string, TrailBeSlot>();
        private readonly ConcurrentDictionary<string, long> _trailBeLastPnlBits =
            new ConcurrentDictionary<string, long>();

        // DW-B79-06: PendingFollowerBeSlot -- event-driven deferred BE for QX->BE-ALL race fix.
        // Registered by MoveStopToBreakEven when targets=0 on a follower account (first call only).
        // Consumed atomically by TryFireFollowerBeRetry in OnOrderUpdate when a PTT-QX-T* order
        // transitions to Working -- fires MoveStopToBreakEven at the exact correct moment,
        // not at an arbitrary 350ms offset. Eliminates the QX->BE-ALL race permanently.
        // Key = acc.Name. JS-021: ConcurrentDictionary TryRemove is lock-free atomic claim.
        // NT8-004: struct in ConcurrentDictionary confirmed safe in NT8.
        private struct PendingFollowerBeSlot
        {
            internal readonly Account Account;
            internal readonly Instrument Instrument;
            internal readonly int BufferTicks;

            internal PendingFollowerBeSlot(Account a, Instrument i, int b)
            {
                Account = a;
                Instrument = i;
                BufferTicks = b;
            }
        }

        private readonly ConcurrentDictionary<
            string,
            PendingFollowerBeSlot
        > _pendingFollowerBeSlots = new ConcurrentDictionary<string, PendingFollowerBeSlot>();

        // DW-B79-08 v3: per-account retry attempt counter for TryReplacePttBeBrackets.
        // Prevents unbounded retry storm when NT8's ATM sweep repeatedly cancels every PTT-BE-*
        // order that TryReplacePttBeBrackets re-places (each re-place gets swept again).
        // Limit: 3 attempts per account per trade. Reset by TryEvictFollowerBeSlot when flat.
        // Key = acc.Name. JS-021: ConcurrentDictionary write is lock-free. JS-023: int values.
        private readonly ConcurrentDictionary<string, int> _beReplaceAttempts =
            new ConcurrentDictionary<string, int>();

        // DW-B105: QX-ALL intent guard. Set per follower account by PttGlobalQuickExit.ExecuteOne
        // before CancelQxBrackets, cleared after. TryReplacePttBeBrackets returns early if set.
        // ConcurrentDictionary: JS-021 lock-free. Key = acc.Name (string). Value = bool (unused).
        internal readonly ConcurrentDictionary<string, bool> _qxCancelInProgress =
            new ConcurrentDictionary<string, bool>();

        // B113 DW-B117: cancel-after cleanup map. Set by PttGlobalQuickExit.ExecuteOne
        // immediately after executor.Execute for follower accounts. OnOrderUpdate reads this
        // to cancel native ATM Target* one-for-one as each PTT-QX-T* confirms Working.
        // Key = acc.Name. Value = (instrument, expiry=UtcNow+2s).
        // JS-021: ConcurrentDictionary -- lock-free. No lock() anywhere.
        internal readonly ConcurrentDictionary<
            string,
            (Instrument Instr, DateTime Expiry)
        > _qxPendingFollowerCleanup = new ConcurrentDictionary<string, (Instrument, DateTime)>();

        // HOTFIX-MSTBE-OCO-REUSE: monotonic counter for BE OCO IDs -- never reuse a cancelled OCO ID.
        // DW-B40-OCO-02 pattern from PttBreakEven._beOcoSeq. JS-023: volatile int allowed.
        // HOTFIX-BEALL-OCO-SEQ-SHARED-01: shared by BOTH MoveStopToBreakEven AND PttBreakEven.Execute
        // so the two paths never collide on the same OCO ID. PttBreakEven calls NextBeOcoSeq() instead
        // of its own _beOcoSeq, ensuring global uniqueness across all BE code paths.
        // DW-B89-01 SEED FIX: XOR Environment.TickCount with low 31 bits of DateTime.UtcNow.Ticks.
        // NT8 keeps cancelled OCO IDs for the entire NT8 session. When NT8 recompiles an AddOn
        // within a running session, CopyEngine is GC'd and re-created. TickCount alone can repeat
        // within the same millisecond on fast recompile. XOR with Ticks (100ns resolution) ensures
        // post-recompile seed is statistically unique. Math.Abs: XOR can set sign bit; wraps safely.
        // JS-023: volatile int. Interlocked.Increment in NextBeOcoSeq() unchanged. No lock added.
        private volatile int _mstbeOcoSeq = Math.Abs(
            Environment.TickCount ^ (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF)
        );

        internal int NextBeOcoSeq() => System.Threading.Interlocked.Increment(ref _mstbeOcoSeq);

        // HOTFIX-B76-POSSTATE-DEDUP-01: atomic hasPos dedup per instrument.
        // Value is int[1]: 0=False, 1=True, 2=unknown (initial).
        // int[] gives a stable heap ref so Interlocked.Exchange can operate on [0].
        // GetOrAdd allocates the array once per instrument on first fill (not per-fill).
        // Interlocked.Exchange is the only write -- one winner per transition, all others
        // see the new value already written and return without invoking. JS-021: lock-free.
        private readonly ConcurrentDictionary<string, int[]> _lastHasPos =
            new ConcurrentDictionary<string, int[]>();

        // B119: DW-B128 -- reversal entry guard.
        // Keyed by instrument FullName, value is the last OrderAction dispatched for that instrument.
        // ConcurrentDictionary: thread-safe without lock(). JS-021: no lock.
        private readonly ConcurrentDictionary<string, OrderAction> _lastLeaderDirection =
            new ConcurrentDictionary<string, OrderAction>();

        // V01: order map for follower bracket lookup
        // JS-025: ConcurrentDictionary (atomic GetOrAdd) + ConcurrentBag (lock-free Add/iterate)
        // JS-021: NO lock keyword anywhere
        private readonly ConcurrentDictionary<string, ConcurrentBag<FollowerBinding>> _orderMap =
            new ConcurrentDictionary<string, ConcurrentBag<FollowerBinding>>();

        // DW-NEW-08 Option E: debounce dict for naked detection.
        // Stores (long)Environment.TickCount at last naked-detect queue time per account name.
        // ConcurrentDictionary: no lock. Key = acc.Name (NT8 platform account name).
        private readonly ConcurrentDictionary<string, long> _nakedDetectLastQueuedTicks =
            new ConcurrentDictionary<string, long>(StringComparer.Ordinal);

        // B194-F4: belt-and-suspenders flatten debounce.
        // Stores (long)Environment.TickCount at last acc.Submit(PTT-Flatten) per account name.
        // Prevents PTT-Flatten fill -> NakedDetect -> FlattenOneAccount re-entry loop.
        // 500ms grace window is far shorter than TickCount wrap period (~25d) -- safe.
        // ConcurrentDictionary: no lock (JS-021). StringComparer.Ordinal: deterministic key compare.
        // DW-B194-04: closed by this field + FlattenOneAccount debounce guard.
        private readonly ConcurrentDictionary<string, long> _flattenFilledTicks =
            new ConcurrentDictionary<string, long>(StringComparer.Ordinal);

        // B200-CLOSE-LANED-01-V11: intent flag -- set true at FlattenOneAccount entry, cleared in OOU on Filled/Cancelled.
        // Immune to acc.Orders race (order-state query) and _flattenFilledTicks early-return staleness.
        // ConcurrentDictionary: no lock (JS-021). StringComparer.Ordinal: deterministic key compare.
        private readonly ConcurrentDictionary<string, bool> _flattenInFlight =
            new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        // B200-CLOSE-LANED-01-V12: order-ID anchor for _flattenInFlight.
        // Stores the OrderId of the PTT-Flatten order actually submitted per account.
        // OnOrderUpdate only clears _flattenInFlight when e.Order.OrderId matches this anchor --
        // prevents stale Filled/Cancelled events from a prior flatten cycle from prematurely
        // clearing the flag while the current PTT-Flatten is still Accepted/Working.
        // ConcurrentDictionary: no lock (JS-021). StringComparer.Ordinal: deterministic key compare.
        private readonly ConcurrentDictionary<string, string> _flattenInFlightOrderId =
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        // DW-NEW-08 Option D: cancel-before-dispatch drain state.
        // Key = follower account name. ConcurrentDictionary: no lock (JS-021).
        // StringComparer.Ordinal: deterministic key comparison.
        private readonly ConcurrentDictionary<string, PendingDispatchDrain> _pendingDispatchDrains =
            new ConcurrentDictionary<string, PendingDispatchDrain>(StringComparer.Ordinal);

        // DW-NEW-08 Option D: track cancel order IDs owned by DrainThenDispatch.
        // Prevents TryReplaceOnAtmCancel from firing a competing replacement on drain-owned cancels (F3).
        // Key = orderId (string per NT8 Order.OrderId), value = 0 (unused placeholder). No lock (JS-021).
        private readonly ConcurrentDictionary<string, byte> _drainOwnedOrderIds =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        // BUG1+BUG2: Slate-ready gate per follower. Key = acc.Name (ASCII). Value = bool.
        // false: bracket wipe imminent -- FindFollowerBracketOrder returns null (gate closed).
        // true / absent: gate open -- normal bracket search proceeds.
        // ConcurrentDictionary: no lock (JS-021). Indexer write is atomic.
        private readonly ConcurrentDictionary<string, bool> _followerSlateReady =
            new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        // B208-FIX-B: BE-swap-in-progress guard for NakedPositionDetector.
        // Set true at PttBreakEvenSwap.Execute entry per account; cleared in finally.
        // Prevents NakedPositionDetector from firing in the cancel window between
        // CancelQxBrackets (stops gone) and PTT-BE-Stop-N Accepted (stops back).
        // ConcurrentDictionary: no lock (JS-021). TryAdd/TryRemove: atomic. ASCII-only.
        private readonly ConcurrentDictionary<string, bool> _beSwapInProgress =
            new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        // B208-FIX-F-B: timestamp of when _beSwapInProgress was last set true, per account.
        // Used for 3-second safety timeout in finally -- prevents permanent lock if NT8 drops
        // the PTT-BE-Stop-N Working callback (e.g. broker timeout, rapid cancel).
        // Key: accName (Ordinal). Value: Environment.TickCount cast to long at set time.
        // ConcurrentDictionary: no lock (JS-021). Indexer write: atomic. ASCII-only.
        private readonly ConcurrentDictionary<string, long> _beSwapSetTicks =
            new ConcurrentDictionary<string, long>(StringComparer.Ordinal);

        // Lane D: dispatch record for TryLaneDHook-handled ATM followers (double-dispatch guard).
        // Key: orderId + "#" + accName (ASCII-only). Value: 0 (unused placeholder).
        // ConcurrentDictionary: no lock (JS-021). TryAdd: atomic. ContainsKey: lock-free O(1).
        // Growth: O(followers * dispatches/session). ~60 bytes/entry. No cleanup required per session.
        // B185 DW-B183-04: eviction threshold for _laneDDispatched dedup guard.
        // At 10000 entries (~600KB), Clear() resets the guard. Worst case: single duplicate
        // Lane D dispatch per 10000 entries (idempotent in NT8 -- acceptable).
        private const int LaneDEvictThreshold = 10000;
        private const int MaxBracketLegs = 9;
        private readonly ConcurrentDictionary<string, byte> _laneDDispatched =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        // Lane E: re-arm dedup guard. Prevents double-arm on multiple PositionUpdate events for the
        // same post-QX/BE slot. Key: accName + "#LANE-E#" + instrFullName (ASCII-only). Value: 0 (unused).
        // ConcurrentDictionary: no lock (JS-021). TryAdd: atomic. TryRemove: called from CancelQxBrackets.
        // Growth: O(followers * instruments). ~60 bytes/entry. Cleaned up per QX/BE cycle.
        private readonly ConcurrentDictionary<string, byte> _laneEDispatched =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        // B204-LANE-E-FLATTEN-PERMANENT-LATCH-01: permanent Lane E block after PTT-Flatten.
        // Key: acc.Name + "#LANE-E#" + instrFullName (same schema as _laneEDispatched).
        // Set in FlattenOneAccount. Cleared only when a new ATM entry is successfully dispatched
        // (TryLaneDDispatchFollower or TryLaneEDispatchFollower) or on SetEnabled(true).
        // Policy: flatten = exit. Lane E must never re-arm after a flatten until the next new entry.
        // ConcurrentDictionary: no lock (JS-021). ASCII-only keys. ~60 bytes/entry.
        private readonly ConcurrentDictionary<string, byte> _laneEFlattenBlocked =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        // T2b: ATM template per-group qty parsed from XML. Key=templateName (OrdinalIgnoreCase). Value=perGroupQty.
        // Populated in CheckAtmTemplatesForReverse. Read in SendCopyWithAtm. ConcurrentDictionary -- no lock. JS-021.
        private readonly ConcurrentDictionary<string, int> _atmTemplateGroupQty =
            new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // T3: tracks follower entry orders placed via drain path, keyed by followerAcctKey (= Account.Name).
        // Populated in SubmitDrainedEntry after SendCopyWithAtm returns the created Order.
        // Consumed in TrySetSlateReadyOnDrainFill to thread the order ref into SyncFollowerBracket.
        // Cleaned up in AbortDrainOnFill and TryDrainWatchdog on abort/timeout.
        // JS-021: ConcurrentDictionary -- no lock. JS-002: Order? nullable is intentional (NT8 absence signal).
        private readonly ConcurrentDictionary<string, Order> _drainFollowerEntryOrders =
            new ConcurrentDictionary<string, Order>(StringComparer.Ordinal);

        // DW-B168-02: tracks which stop suffixes ("1","2","3") are mid-drag.
        // Populated at ChangePending (before broker round-trip) so the set is
        // correct at Accepted callback time regardless of broker batch ordering.
        // JS-021: no lock -- all NT8 order callbacks for the leader account execute
        //   on the same NT8 background thread; concurrent access is impossible.
        // JS-036: HashSet<string> -- bounded to max 3 entries (one per stop leg).
        // ASCII-only. No Unicode.
        private readonly HashSet<string> _dragsInFlight =
            new HashSet<string>(StringComparer.Ordinal);

        // --- Status event ---
        internal event Action<string> StatusUpdate;

        // V05: position state change notification for UI surfaces
        // Fired from TryFirePositionState -- before Gate 1 (fires even when copy is disabled)
        public event Action<string, PositionState> PositionStateChanged;

        // B10 T2 -- Pending BE fired notification (fires on NT8 account bg thread; Panel marshals to UI)
        internal event Action<string, string> PendingBeFired;
        // B10 T2 -- Pending BE armed notification (fires on NT8 account bg thread; Panel marshals to UI)
#pragma warning disable CS0067  // DW-B186-WARN-01: Roslyn false positive -- event IS subscribed by TradeCopierPanel
        internal event Action<string, string> PendingBeArmed; // HOTFIX-BEALL-SYNC-01: instr, accName
#pragma warning restore CS0067

        internal event Action<int> GlobalBeBufferChanged; // HOTFIX-BEALL-BUFFER-SYNC-01: fires on buffer +/- spin, int = new buffer value

        // CS0070 fix: event can only be raised from declaring class (CopyEngine). PttGlobalBreakEven calls this relay.
        internal void RaiseBeBufferChanged(int newValue) =>
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                GlobalBeBufferChanged?.Invoke(newValue)
            ); // HOTFIX-DISPATCH-FIX-01: fire on app UI thread

        // HOTFIX-QUICKALL-SINGLETON-01: Quick ALL tick buffer -- singleton so all panels share the same value.
        // JS-023: volatile int allowed. NT8-003: volatile double banned -- not used here.
        private volatile int _globalQuickAllT1 = 4; // default 4 ticks (same as per-panel default)
        internal int GlobalQuickAllT1 => _globalQuickAllT1;

        internal void IncrementQuickAll()
        {
            if (_globalQuickAllT1 < 99)
                _globalQuickAllT1++;
            int v = _globalQuickAllT1;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                GlobalQuickAllBufferChanged?.Invoke(v)
            ); // HOTFIX-DISPATCH-FIX-01
        }

        internal void DecrementQuickAll()
        {
            if (_globalQuickAllT1 > 1)
                _globalQuickAllT1--;
            int v = _globalQuickAllT1;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                GlobalQuickAllBufferChanged?.Invoke(v)
            ); // HOTFIX-DISPATCH-FIX-01
        }

        internal event Action<int> GlobalQuickAllBufferChanged; // HOTFIX-QUICKALL-SINGLETON-01

        // HOTFIX-BEALL-DISARM-SYNC-01: broadcast so all panels reset BE ALL visual to Idle on disarm.
        // Fired from OnGlobalBeClick disarm path AND from UpdateButtonColors HOTFIX-F3 branch (position close).
        internal event Action GlobalBeAllDisarmed;

        internal void RaiseBeAllDisarmed() => GlobalBeAllDisarmed?.Invoke();

        // B20-LANE-A T2: Copy ON/OFF sync event (DW-B17-SYNC-01)
        // Plain delegate field -- NOT lock-guarded (JS-021). Fired from SetEnabled on every toggle.
        // Lane C wires TradeCopierPanel and TradeCopierWindow subscribers.
        public event Action<bool> CopyEnabledChanged;

        // B132 LaneB diagnostic gate -- set to false to disable all TP1-TP4 Print calls.
        // Remove this field and all TryLogDragTrace / TryLogSFBTrace calls when DW-B138 is confirmed fixed.
        // JS-021: static bool read is lock-free (no torn reads on bool). Not volatile (diagnostic only).
        private static bool _diagnosticMode = true;

        // --- Nested structs ---

        internal readonly struct CopyRule
        {
            internal readonly string Instrument;
            internal readonly Account MasterAccount;
            internal readonly Account[] FollowerAccounts;
            internal readonly bool Enabled; // Change 2

            // B8 T1: per-follower quantity multiplier (parallel to FollowerAccounts[])
            // null = all followers default to 1x. readonly int[] on readonly struct (JS-008).
            internal readonly int[] FollowerMultipliers;

            // V07: ATM template map per follower account -- logically immutable (written once at construction)
            // NT8 constraint: System.Collections.Immutable not available; use readonly Dictionary (never mutated after ctor).
            // NT8 constraint: readonly struct requires readonly field, not auto-property with setter (CS8341).
            internal readonly Dictionary<string, FollowerAtmMode> FollowerAtmTemplates;

            // B10 T3: TightenTicks -- default 5 ticks; stored on readonly struct (JS-008).
            // NT8-001: readonly field on readonly struct (not init-only property).
            // Backward compat: DtoToRule converts dto.TightenTicks > 0 ? dto.TightenTicks : 5.
            internal readonly int TightenTicks;

            // B127: original follower account names parallel to FollowerAccounts[] -- enables
            // lazy re-resolve in AllAccounts() for null slots at DtoToRule/LoadRules time.
            // JS-008: readonly array field on readonly struct -- reference is immutable, compliant.
            // DW-PTT-BE-FIX-01.
            internal readonly string[] FollowerAccountNames;

            // B8 T1: updated private constructor (adds multipliers + atmTemplates parameters)
            // B10 T3: updated to include tightenTicks
            // B127: updated to include followerAccountNames (8th param, DW-PTT-BE-FIX-01)
            private CopyRule(
                string instrument,
                Account master,
                Account[] followers,
                bool enabled,
                int[] multipliers,
                Dictionary<string, FollowerAtmMode> atmTemplates,
                int tightenTicks,
                string[] followerAccountNames // NEW B127: 8th param
            )
            {
                Instrument = instrument;
                MasterAccount = master;
                FollowerAccounts = followers;
                Enabled = enabled;
                FollowerMultipliers = multipliers;
                FollowerAtmTemplates = atmTemplates ?? new Dictionary<string, FollowerAtmMode>();
                TightenTicks = tightenTicks > 0 ? tightenTicks : 5;
                // B127: derive names from accounts when not supplied explicitly (backward compat).
                // DtoToRule supplies explicit names (covering null-account slots).
                // All other callers pass null -- names are derived from resolved Account references.
                FollowerAccountNames = followerAccountNames ?? DeriveFollowerNames(followers);
            }

            // B8 T1: updated factory -- new optional params preserve backward compat with all existing tests
            // B10 T3: adds tightenTicks optional param (default 5)
            // B127: adds followerAccountNames optional param (default null = derive from followers[])
            internal static CopyRule Create(
                string instrument,
                Account master,
                Account[] followers,
                bool enabled = true,
                int[] multipliers = null,
                Dictionary<string, FollowerAtmMode> atmTemplates = null,
                int tightenTicks = 5,
                string[] followerAccountNames = null // NEW B127: 8th optional param; null = derive in ctor
            ) =>
                new CopyRule(
                    instrument,
                    master,
                    followers,
                    enabled,
                    multipliers,
                    atmTemplates ?? new Dictionary<string, FollowerAtmMode>(),
                    tightenTicks,
                    followerAccountNames // passed through; null triggers DeriveFollowerNames in ctor
                );

            // B127: derives follower name strings from Account[] for backward-compat callers.
            // Returns empty array for null/empty input. Never returns null (JS-002 convention).
            // CYC=2: null/length guard (1) + for loop (1).
            // JS-021: no lock. JS-001: no throw. ASCII-only.
            private static string[] DeriveFollowerNames(Account[] followers)
            {
                if (followers == null || followers.Length == 0)
                    return Array.Empty<string>();
                var names = new string[followers.Length];
                for (int i = 0; i < followers.Length; i++)
                    names[i] = followers[i]?.Name ?? string.Empty;
                return names;
            }
        }

        private readonly struct CopySignal
        {
            internal readonly OrderAction Action;
            internal readonly OrderType Type;
            internal readonly int Quantity;
            internal readonly double LimitPrice;
            internal readonly string OrderId;

            private CopySignal(
                OrderAction action,
                OrderType type,
                int qty,
                double limitPrice,
                string orderId
            )
            {
                Action = action;
                Type = type;
                Quantity = qty;
                LimitPrice = limitPrice;
                OrderId = orderId;
            }

            internal static CopySignal Create(
                OrderAction action,
                OrderType type,
                int qty,
                double limitPrice,
                string orderId
            ) => new CopySignal(action, type, qty, limitPrice, orderId);
        }

        private readonly struct TrimSignal
        {
            // NO qty field -- correctness by construction (JS-003)
            // Each account reads its own live position independently
            internal readonly DateTime UtcTime;
            internal readonly string Instrument;

            private TrimSignal(string instrument)
            {
                UtcTime = DateTime.UtcNow;
                Instrument = instrument;
            }

            internal static TrimSignal Create(string instrument) => new TrimSignal(instrument);
        }

        // --- Private constructor (singleton, prevents external instantiation -- JS-010) ---
        private CopyEngine() { }

        // --- Public API ---

        internal void SetEnabled(bool enabled)
        {
            _isCopyEnabled = enabled;
            StatusUpdate?.Invoke("Copy " + (enabled ? "ON" : "OFF"));
            CopyEnabledChanged?.Invoke(enabled);
            // DR-3: clear stale live-entry keys on Copy ON toggle.
            // _liveEntryInstruments keys are set on first dispatch and cleared only by position-flat PSC.
            // If PSC did not fire cleanly (chaotic session end), keys accumulate and block all new entries.
            // Clearing on re-enable gives the user a no-restart workaround: toggle Copy OFF then ON.
            // Only clear on enable (not disable) -- no entries are being dispatched while disabled.
            // ConcurrentDictionary.Clear() is atomic (JS-021 compliant).
            // DR-4: diagnostic log confirms Clear() ran and reports how many stale keys were flushed.
            // count=0 means no stale keys at toggle time (key was written AFTER Clear ran -- RC-2 path).
            // count>0 means stale keys were present and are now cleared (RC-3 / session-end leak path).
            if (enabled)
            {
                int dr4Before = _liveEntryInstruments.Count;
                int slateBefore = _followerSlateReady.Count;
                _liveEntryInstruments.Clear();
                _entryInstrKeyByOrderId.Clear();
                _followerSlateReady.Clear();  // B202: stale false-slate from prior session triggers Lane E on first open
                // B205-FIX-A: do NOT clear _laneEFlattenBlocked here.
                // Prior behaviour (B204): Clear() on re-enable assumed a fresh session.
                // Bug: user presses Close on Positions tab -> leader re-enters via stale Entry limit ->
                //   user toggles copier off/on -> Clear() wiped the flatten-latch -> Lane E re-entered followers.
                // Fix: latch is keyed per-account per-instrument and is cleared only by TryLaneDDispatchFollower
                //   on a genuine new leader entry (L2989). No re-enable wipe needed -- the latch auto-clears
                //   on the next real entry dispatch. If no new entry fires, followers stay blocked (correct).
                NinjaTrader.Code.Output.Process(
                    "[PTT-DR4-CLEAR] SetEnabled(true) flushed liveEntry=" + dr4Before
                        + " entryMap=" + dr4Before + " slate=" + slateBefore,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            }
            SaveRules(); // DW-B98-A: persist enabled state immediately so F5 restores it
        }

        // B54 -- IsEnabled: read-only view of _isCopyEnabled (JS-023: volatile bool read).
        // CYC=1. Used by TradeCopierPanel.OnLoaded snap and TradeCopierWindow.OnLoaded snap.
        public bool IsEnabled => _isCopyEnabled;

        // B39 -- GlobalBe: shared Global BE engine. Lazy-init on first access (UI thread only).
        // CYC=2 (null check + assignment).
        // JS-021: no lock -- CLR object reference assignment is atomic on 64-bit.
        // JS-002: always returns non-null; new PttGlobalBreakEven() as fallback.
        public PttGlobalBreakEven GlobalBe
        {
            get
            {
                if (_globalBe == null)
                    _globalBe = new PttGlobalBreakEven();
                return _globalBe;
            }
        }

        // Change 5: SetDailyCapFloor added immediately after SetEnabled
        internal void SetDailyCapFloor(double floor)
        {
            _dailyCapFloor = floor;
        }

        // B9 T3: CYC=1 -- straight-line cast and assign; BGTM-1: MirrorMode gate CYC=2.
        internal void SetCopyMode(CopyMode mode)
        {
            if (!_flags.MirrorMode && mode == CopyMode.Mirror)
            {
                StatusUpdate?.Invoke("Mirror mode requires Elite tier");
                return;
            }
            _copyModeValue = (int)mode;
        }

        // B9 T3: CYC=1 -- straight-line cast and return
        internal CopyMode GetCopyMode()
        {
            return (CopyMode)_copyModeValue;
        }

        // B58 ICopyEngine -- RelayBe: fan out pre-calculated BE price to all follower accounts.
        // BeEventArgs.BePrice is already computed by PttGlobalBreakEven/BE module before firing.
        // B66 DW-B66-BE-01: e.IsLong passed to SubmitBeStop (was relying on re-read inside method -- race).
        // B68 DW-B68-01: CancelQxBrackets added before SubmitBeStop -- clears stale ATM brackets
        //   (Stop1/Stop2/Target1/Target2) on each account before the new BE stop is placed.
        //   No new McCabe branch: the cancel is a void call in the loop body, not an if-branch.
        // CYC=2 (unchanged: 1 base + 1 foreach branch). JS-021: no lock. JS-002: void. JS-033: synchronous.
        public void RelayBe(BeEventArgs e)
        {
            foreach (var acc in AllAccounts(e.Instrument))
            {
                NinjaTrader.Code.Output.Process(
                    "[BE] RelayBe: "
                        + acc.Name
                        + " @ "
                        + e.BePrice.ToString("F2")
                        + " isLong="
                        + e.IsLong,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                CancelQxBrackets(acc, e.Instrument);
                SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong);
            }
        }

        // B58 ICopyEngine -- RelayTrim: delegate to Trim(Instrument) fan-out. CYC=1.
        // Trim(Instrument) at line 1006 iterates AllAccounts and calls TrimOneAccount per account.
        // JS-021: no lock. JS-002: void, no return null.
        public void RelayTrim(TrimEventArgs e) => Trim(e.Instrument);

        // B58 ICopyEngine -- RelayFlatten: delegate to Flatten(Instrument) fan-out. CYC=1.
        // Flatten(Instrument) at line 1012 iterates AllAccounts and calls FlattenOneAccount per account.
        // JS-021: no lock. JS-002: void, no return null.
        public void RelayFlatten(FlatEventArgs e) => Flatten(e.Instrument);

        // B58 ICopyEngine -- RelayCancel: delegate to CancelPendingEntries(Instrument) fan-out. CYC=1.
        // CancelPendingEntries(Instrument) at line 1192 iterates AllAccounts and calls CancelOneAccount.
        // JS-021: no lock. JS-002: void, no return null.
        public void RelayCancel(CancelEventArgs e) => CancelPendingEntries(e.Instrument);

        // B50 -- SetCloneAtmCache: CYC=1. Stores ATM template name string for display/logging only.
        // Called from TradeCopierPanel.OnCloneModeClick alongside SetCloneAtmObjectCache.
        // JS-023: volatile string write is atomic.
        internal void SetCloneAtmCache(string value)
        {
            _cloneAtmCache = value ?? string.Empty;
        }

        // HOTFIX-B66-ATM-OBJ: SetCloneAtmObjectCache -- stores live AtmStrategy object for Clone dispatch.
        // Called from TradeCopierPanel.OnCloneModeClick after FindVisualChild<ChartTrader>.AtmStrategy.
        // JS-023: volatile reference write is atomic on CLR 4.0+.
        // CYC=1. JS-001: no throw. JS-002: null is valid (means None selected).
        internal void SetCloneAtmObjectCache(NinjaTrader.NinjaScript.AtmStrategy atmObj)
        {
            _cloneAtmObject = atmObj;
        }

        // B50 -- GetCloneAtmMode: CYC=2. Returns Named(obj) if object cached, Named(string) if string only, else Inherit.
        // Primary: use _cloneAtmObject (live AtmStrategy) so StartAtmStrategy(atm,order) overload is used.
        // Fallback: _cloneAtmCache string (for non-Clone-via-ChartTrader scenarios).
        // JS-002: never returns null -- returns Inherit as fallback.
        internal FollowerAtmMode GetCloneAtmMode()
        {
            var atmObj = _cloneAtmObject;
            if (atmObj != null) // branch (1) -- preferred: object overload
                return new FollowerAtmMode.Named(_cloneAtmCache, atmObj);
            var cache = _cloneAtmCache;
            if (cache != null && cache.Length > 0) // branch (2) -- fallback: string overload
                return new FollowerAtmMode.Named(cache);
            return new FollowerAtmMode.Inherit();
        }

        // B180-2: backward-compat test seam overloads (per-instrument sigs simplified to single-instance).
        // These forward to the 1-arg/0-arg versions -- instrFullName arg is ignored.
        // CopyEngineTests.cs at HEAD uses 2-arg/1-arg signatures; these stubs keep the test file compilable.
        internal void SetCloneAtmObjectCache(string instrFullName, NinjaTrader.NinjaScript.AtmStrategy atmObj)
            => SetCloneAtmObjectCache(atmObj);

        internal void SetCloneAtmCache(string instrFullName, string value)
            => SetCloneAtmCache(value);

        internal FollowerAtmMode GetCloneAtmMode(string instrFullName)
            => GetCloneAtmMode();

        // B56-LaneB: CYC=2 -- yield distinct instrument names for UI refresh after LoadRules.
        // JS-021: no lock -- ConcurrentBag foreach is lock-free.
        // JS-002: returns empty IEnumerable (not null) when _rules is empty.
        internal IEnumerable<string> GetRuleInstruments()
        {
            var seen = new HashSet<string>();
            foreach (var r in _rules)
                if (seen.Add(r.Instrument))
                    yield return r.Instrument;
        }

        // HOTFIX-B67-CHECKBOX-RESTORE: returns saved follower account names for a given instrument+master.
        // Called from TradeCopierPanel.OnLoaded to restore IsSelected checkboxes after NT8 restart.
        // CYC=2: foreach rules(1) + foreach followers(2). JS-021: no lock. JS-002: returns empty set not null.
        internal HashSet<string> GetSavedFollowerNames(string instrument, string masterName)
        {
            var result = new HashSet<string>();
            foreach (var rule in _rules)
            {
                if (rule.Instrument != instrument || rule.MasterAccount?.Name != masterName)
                    continue;
                foreach (var f in rule.FollowerAccounts)
                    if (f?.Name != null)
                        result.Add(f.Name);
            }
            return result;
        }

        // --- B56 BUILD-FIX stubs (pre-existing callers referenced these before they were added) ---

        // IsFollowerAccount: returns true if acc is a follower in any rule.
        // Called by PttBreakEven + PttGlobalQuickExit to skip follower accounts.
        // CYC=8: null guard(1) + foreach(2) + for-i(3) + f-not-null(4) +
        //        f-null(5) + names-not-null(6) + i-in-range(7) + name-match(8).
        // JS-021: no lock. B121: null-slot fallback to FollowerAccountNames[i].
        internal bool IsFollowerAccount(Account acc)
        {
            if (acc == null)
                return false;
            foreach (var rule in _rules)
                for (int i = 0; i < rule.FollowerAccounts.Length; i++)
                {
                    var f = rule.FollowerAccounts[i];
                    if (f != null && f.Name == acc.Name)
                        return true;
                    if (
                        f == null
                        && rule.FollowerAccountNames != null
                        && i < rule.FollowerAccountNames.Length
                        && rule.FollowerAccountNames[i] == acc.Name
                    )
                        return true;
                }
            return false;
        }

        // B180-2: backward-compat test seam stub -- IsPttCopyEntryFilled removed in B180.
        // Tests referencing this method are already [Fact(Skip=...)]; stub keeps compilation.
        // CYC=1. JS-001: no throw. JS-002: returns bool.
        internal bool IsPttCopyEntryFilledTestable(NinjaTrader.Cbi.Order o) => false;

        // GetQuickTicksForInstrument: returns (t1,t2) quick-exit tick defaults for an instrument.
        // Delegates to InstrumentDefaults -- rule-specific overrides deferred to future block.
        // CYC=2: null guard(1) + delegate(2). JS-002: returns tuple (not null).
        internal (int t1, int t2) GetQuickTicksForInstrument(NinjaTrader.Cbi.Instrument instr)
        {
            if (instr == null)
                return (4, 8); // (1)
            return InstrumentDefaults.GetQuickTicks( // (2)
                instr.MasterInstrument?.Name ?? string.Empty
            );
        }

        // IsAtmBracketName: true if name is a standard NT8 ATM bracket order name.
        // NT8-REF: NT8_FULL_REFERENCE.md line 1631: "The order name such as 'Stop1' or 'Target2'"
        // HOTFIX-ATM-T3-CANCEL-01: widened from hardcoded 4 names to generic Stop1-Stop9 / Target1-Target9.
        // Hardcoded Stop1/Stop2/Target1/Target2 missed Stop3..Stop9 and Target3..Target9 for
        // ATM strategies with 3+ targets (e.g. "MES $200 SL6" with 3 targets).
        // CYC=1: expression body -- Roslyn counts the compound bool expression as 1 decision point.
        // JS-021: no lock. JS-001: no throw. ASCII-only string literals.
        internal static bool IsAtmBracketName(string name) =>
            !string.IsNullOrEmpty(name)
            && (
                (
                    name.StartsWith("Stop", StringComparison.Ordinal)
                    && name.Length > 4
                    && char.IsDigit(name[4])
                )
                || (
                    name.StartsWith("Target", StringComparison.Ordinal)
                    && name.Length > 6
                    && char.IsDigit(name[6])
                )
                || (name.StartsWith("SimStop", StringComparison.Ordinal) && name.Length > 7 && char.IsDigit(name[7]))
            );

        // IsBeDisarmCandidate: CYC=4. Returns true when order is a PTT-BE-Stop fill on a non-null instrument.
        // Called by TryFireFollowerBeDisarm to guard the leader-check loop.
        // JS-001: no throw. JS-002: returns bool. JS-021: no lock. TESTABILITY: internal static.
        internal static bool IsBeDisarmCandidate(Order order)
        {
            if (order == null)
                return false; // (1)
            if (order.OrderState != OrderState.Filled)
                return false; // (2)
            if (
                order.Name == null
                || !order.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)
            )
                return false; // (3)
            return order.Instrument?.FullName != null; // (4)
        }

        // IsPttEntryOrderCancelTrigger: CYC=4. Returns true when a follower entry cancel should trigger re-place.
        // Matches HOTFIX-B66-COPY-REPLACE + HOTFIX-B66-NATIVE-ATM: "PTT-Copy" or "Entry" + Cancelled + LimitPrice>0.
        // Called from pre-Gate-1 block in OnOrderUpdate.
        // JS-001: no throw. JS-002: returns bool (never null). JS-021: no lock. ASCII-only.
        // TESTABILITY: internal static, Order parameter, no NT8 runtime deps beyond field reads.
        internal static bool IsPttEntryOrderCancelTrigger(Order order)
        {
            if (order == null)
                return false; // (1)
            if (order.OrderState != OrderState.Cancelled)
                return false; // (2)
            if (order.Name != "PTT-Copy" && order.Name != "Entry")
                return false; // (3)
            if (order.AverageFillPrice > 0)
                return false; // (4) B163: lifecycle cancel of a previously-filled Entry -- not a re-place trigger
            return order.LimitPrice > 0 && order.Instrument?.FullName != null;
        }

        // TryReplaceOnAtmCancel: CYC=5. Fuses IsPttEntryOrderCancelTrigger predicate with action.
        // Eliminates a branch from OnOrderUpdate parent CCN budget.
        // F3-repair: drain-owned guard added as first check (JS-021: no lock, ConcurrentDictionary).
        // R2-V1-repair: drain-active account guard prevents spurious re-placement during active drain.
        // B161: IsFollowerPositionBlocked guard blocks ghost re-place during BE-All race window.
        // JS-001: no throw. JS-002: void. JS-021: no lock. ASCII-only.
        private void TryReplaceOnAtmCancel(Order order)
        {
            if (_drainOwnedOrderIds.ContainsKey(order.OrderId))
                return; // drain-owned cancel -- skip replacement (1)
            if (order.Account != null && _pendingDispatchDrains.ContainsKey(order.Account.Name))
                return; // drain active for this account -- skip replacement (2)
            if (!IsPttEntryOrderCancelTrigger(order))
                return; // (3)
            if (order.Account != null
                && order.Instrument != null
                && IsFollowerPositionBlocked(order.Account, order.Instrument))
                return; // BE-All race -- skip re-place (4)
            ReplaceFollowerCopyOnAtmCancel(order); // (5 - no branch)
        }

        // IsQxCancelCandidate: returns true if order should be cancelled by CancelQxBrackets.
        // Covers: ATM bracket names (via IsAtmBracketName), PTT-QX-* prefix, PTT-BE-* prefix,
        //         PTT-Copy* prefix (B70 DW-B70-02: follower copy-dispatched entry orders).
        // CYC=7: 1 (base) + 6 if-branches. JS-021: no lock. JS-001: no throw. ASCII-only.
        // B201-ENTRY-CANCEL-01: isFollower param scopes "Entry" cancel to follower accounts only.
        internal static bool IsQxCancelCandidate(Order o, bool isFollower)
        {
            if (o == null || o.Name == null)
                return false; // (1)
            if (IsAtmBracketName(o.Name))
                return true; // (2)
            if (o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal))
                return true; // (3)
            if (o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal))
                return true; // (4)
            if (o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal))
                return true; // (5) B70 DW-B70-02
            if (isFollower && o.Name == "Entry")
                return true; // (6) DW-B93: Named ATM follower entry Limit -- scoped to follower only (B201-ENTRY-CANCEL-01 FIX)
            return false;
        }

        // CancelQxBrackets: cancel all active ATM-bracket + PTT-* orders on acc for instr.
        // Called by PttQuickExit.Execute() before re-placing new bracket.
        // HOTFIX-QX-DOUBLE-01: Added TriggerPending -- ATM brackets spend time here before Submitted.
        //   NT8_FULL_REFERENCE.md line 946: TriggerPending = "Order is pending submission."
        //   Without this, clicking Quick All immediately after an ATM fill leaves bracket orders
        //   in TriggerPending state uncancelled -> new PTT-QX brackets stack on top -> double brackets.
        // CYC=6: null guard(1) + foreach(2) + stateOk(5 branches, Roslyn=1)(3) + instrument check(4) + IsQxCancelCandidate(5) + staleCount(6).
        // JS-021: no lock. Predicate logic in IsQxCancelCandidate (CYC=5) + IsAtmBracketName (CYC=1).
        internal void CancelQxBrackets(Account acc, NinjaTrader.Cbi.Instrument instr)
        {
            if (acc == null || instr == null)
                return; // (1)
            bool isFollower = IsFollowerAccount(acc);
            TryMarkFollowerSlateNotReady(acc); // BUG1+BUG2: bracket wipe imminent
            _laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _); // Lane E: reset re-arm dedup
            var stale = new System.Collections.Generic.List<Order>();
            foreach (Order o in acc.Orders) // (2)
            {
                bool stateOk =
                    o.OrderState == OrderState.Working
                    || o.OrderState == OrderState.Initialized
                    || o.OrderState == OrderState.Accepted
                    || o.OrderState == OrderState.Submitted // B71: catch ATM brackets placed <800ms ago
                    || o.OrderState == OrderState.TriggerPending; // HOTFIX-QX-DOUBLE-01: pre-submit state
                if (!stateOk)
                    continue; // (3)
                if (o.Instrument == null || o.Instrument.FullName != instr.FullName)
                    continue;
                if (IsQxCancelCandidate(o, isFollower)) // (5) widened via helper
                    stale.Add(o);
            }
            if (stale.Count == 0)
                return;
            stale.RemoveAll(o =>
                o.OrderState == OrderState.Filled || o.OrderState == OrderState.Cancelled
            ); // DW-B79-09: race guard
            try
            {
                acc.Cancel(stale.ToArray());
            }
            catch { }
        }

        // B77 DW-B77-01: BuildQxSnapshot -- capture point-in-time set of cancellable QX orders.
        // Called by PttQuickExit.Execute() BEFORE CancelQxBrackets to record which orders existed
        // at snapshot time. Only orders in this set may be cancelled by the 3-param overload.
        // Prevents the race window where newly-submitted PTT-QX orders (from the Submit loop) are
        // caught by a second CancelQxBrackets call that was queued before the Submit loop ran.
        // CYC=4: null-guard(1) + foreach(2) + stateOk-and-instrument(3) + IsQxCancelCandidate(4).
        // JS-021: no lock. HashSet<Order> is local; NT8 dispatcher is serial (single-threaded dispatch).
        // JS-002: returns new empty HashSet<Order>() on null input -- never returns null.
        // JS-001: no throw. JS-033: synchronous static. ASCII-only.
        internal static System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order> BuildQxSnapshot(
            NinjaTrader.Cbi.Account acc,
            NinjaTrader.Cbi.Instrument instr,
            bool isFollower = false
        )
        {
            if (acc == null || instr == null) // (1)
                return new System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order>(); // never null -- JS-002
            var result = new System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order>();
            foreach (Order o in acc.Orders) // (2)
            {
                bool stateOk =
                    o.OrderState == OrderState.Working
                    || o.OrderState == OrderState.Initialized
                    || o.OrderState == OrderState.Accepted
                    || o.OrderState == OrderState.Submitted
                    || o.OrderState == OrderState.TriggerPending;
                if (!stateOk)
                    continue; // (3)
                if (o.Instrument == null || o.Instrument.FullName != instr.FullName)
                    continue;
                if (IsQxCancelCandidate(o, isFollower)) // (4)
                    result.Add(o);
            }
            NinjaTrader.Code.Output.Process(
                "[PTT-QX] snapshot: " + result.Count + " cancellable orders for " + instr.FullName,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            return result;
        }

        // BUG1+BUG2: Mark follower slate as not-ready. Called before any bracket wipe.
        // Conditions: acc non-null + IsFollowerAccount. Early returns prevent false negatives.
        // CYC<=2: acc null(1) + IsFollowerAccount(2). JS-021: ConcurrentDictionary indexer is lock-free.
        // JS-001: no throw. JS-002: void. ASCII-only.
        private void TryMarkFollowerSlateNotReady(Account acc)
        {
            if (acc == null)                    // (1)
                return;
            if (!IsFollowerAccount(acc))        // (2)
                return;
            _followerSlateReady[acc.Name] = false;
        }

        // BUG2: Mark follower slate as ready after position confirmed flat + no active orders.
        // Called from OnFollowerPositionUpdate.
        // Conditions: acc non-null + IsFollowerAccount + IsAccountFlat + !HasAnyActiveOrders.
        // Only writes ready=true when ALL conditions pass. Early-returns prevent false positives.
        // CYC<=4: acc null(1) + IsFollowerAccount(2) + !IsAccountFlat(3) + HasAnyActiveOrders(4).
        // JS-021: ConcurrentDictionary indexer is lock-free. JS-001: no throw. JS-002: void. ASCII-only.
        private void TrySetFollowerSlateReady(Account acc)
        {
            if (acc == null)                    // (1)
                return;
            if (!IsFollowerAccount(acc))        // (2)
                return;
            if (!IsAccountFlat(acc))            // (3)
                return;
            if (HasAnyActiveOrders(acc))        // (4)
                return;
            _followerSlateReady[acc.Name] = true;
        }

        // B197 T1 DW-B157-01: TrySetSlateReadyOnDrainFill -- CYC=5.
        // Marks follower slate-ready when a drain-tracked PTT-Copy entry fills.
        // ORDERING: Must be called BEFORE AbortDrainOnFill in OnOrderUpdate Filled branch.
        // AbortDrainOnFill (L7029) removes the order ID from _drainOwnedOrderIds.
        // Guard (3) checks ContainsKey -- if AbortDrainOnFill runs first, Guard(3) always fails silently.
        // JS-021: _drainOwnedOrderIds.ContainsKey + _followerSlateReady[] are ConcurrentDictionary ops (lock-free).
        // JS-001: no throw -- early returns only. JS-002: void. ASCII-only.
        private void TrySetSlateReadyOnDrainFill(OrderEventArgs e)
        {
            if (e?.Order == null) return;                                          // (1)
            if (e.Order.OrderState != OrderState.Filled) return;                   // (2)
            if (!_drainOwnedOrderIds.ContainsKey(e.Order.OrderId)) return;         // (3)
            if (!IsFollowerAccount(e.Order.Account)) return;                       // (4)
            _followerSlateReady[e.Order.Account.Name] = true;
            // T3: retrieve and remove the stored follower entry order; pass drainFo to SyncFollowerBracket.
            // TryGetValue + TryRemove + ?? = 0 new McCabe branches; CYC stays at 5.
            string acctKey = e.Order.Account.Name;
            _drainFollowerEntryOrders.TryGetValue(acctKey, out Order drainFo);
            _drainFollowerEntryOrders.TryRemove(acctKey, out _);
            // T3: pass drainFo (stored follower entry order) to SyncFollowerBracket instead of null.
            // drainFo ?? e.Order fallback: if SendCopyWithAtm failed, use fill event order. No new branch (?? is not McCabe).
            double tickSize = e.Order.Instrument?.MasterInstrument?.TickSize ?? 0.0;
            SyncFollowerBracket(e.Order.Account, drainFo ?? e.Order, false, e.Order.LimitPrice, tickSize);
            NinjaTrader.Code.Output.Process(
                "[SLATE-READY-DRAIN] acct=" + acctKey
                    + (drainFo != null ? " drainFo=set" : " drainFo=null"),
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        }

        // BUG2: Returns true when acc has no open positions across all instruments.
        // Used by TrySetFollowerSlateReady to confirm flat before marking slate ready.
        // NT8: acc.Positions is available in AddOnBase. Each Position.Quantity > 0 = open.
        // CYC<=3: acc null(1) + foreach(2) + Quantity>0(3). JS-021: no lock (read-only enumeration).
        // JS-001: no throw. JS-002: returns bool. ASCII-only. static.
        private static bool IsAccountFlat(Account acc)
        {
            if (acc == null)                    // (1)
                return true;
            foreach (var pos in acc.Positions)  // (2)
                if (pos.Quantity > 0)           // (3)
                    return false;
            return true;
        }

        // BUG2: Returns true when acc has any Working/Submitted/Accepted orders remaining.
        // Used by TrySetFollowerSlateReady to confirm no residual active orders before ready.
        // NT8: acc.Orders.ToList() snapshot -- standard pattern in CopyEngine (line 4898).
        // CYC<=5: acc null(1) + foreach(2) + Working(3) + Submitted(4) + Accepted(5).
        // JS-021: no lock. JS-001: no throw. JS-002: returns bool. ASCII-only. static.
        private static bool HasAnyActiveOrders(Account acc)
        {
            if (acc == null)                    // (1)
                return false;
            foreach (var o in acc.Orders.ToList())  // (2)
            {
                if (o.OrderState == OrderState.Working    // (3)
                    || o.OrderState == OrderState.Submitted  // (4)
                    || o.OrderState == OrderState.Accepted)  // (5)
                    return true;
            }
            return false;
        }

        // BUG2: NT8 account.PositionUpdate event handler.
        // Fires on NT8 account background thread (same context as OnOrderUpdate).
        // When position goes flat and no active orders remain, marks slate ready for next trade.
        // NT8: PositionEventArgs.Position.Account confirmed from TradeCopierPanel.cs line 2109.
        // No UI operations -- ConcurrentDictionary write is thread-safe, no Dispatcher needed.
        // CYC<=3: e/Position/Account null(1) + IsFollowerAccount(2) + TrySetFollowerSlateReady(0).
        // JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only. No async/await.
        private void OnFollowerPositionUpdate(object sender, PositionEventArgs e)
        {
            if (e?.Position?.Account == null)           // (1)
                return;
            if (!IsFollowerAccount(e.Position.Account)) // (2)
                return;
            TrySetFollowerSlateReady(e.Position.Account);
            TryLaneEHook(e.Position.Account, e.Position); // Lane E: re-arm ATM after partial QX/BE
        }

        // B77 DW-B77-02: CancelQxBrackets 3-param overload -- snapshot-gated cancel.
        // Identical to the 2-param overload except: an order is only added to stale if it
        // is contained in snapshot. Orders not in snapshot (submitted after snapshot was
        // taken = this cycle's new orders) are skipped, preventing the race window.
        // snapshot == null fallback: behaves identically to the 2-param overload (cancels all).
        // CYC=7: null-guard(1) + foreach(2) + stateOk(3) + instrument-filter(4) + snapshot-filter(5)
        //         + IsQxCancelCandidate(6) + stale-count(7). Budget <= 8. PASS.
        // JS-021: no lock. HashSet<Order> passed by reference, consumed synchronously on caller thread.
        // JS-001: no throw. JS-002: void return. JS-033: synchronous void. ASCII-only.
        internal void CancelQxBrackets(
            NinjaTrader.Cbi.Account acc,
            NinjaTrader.Cbi.Instrument instr,
            System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order> snapshot
        )
        {
            if (acc == null || instr == null)
                return; // (1)
            bool isFollower = IsFollowerAccount(acc);
            TryMarkFollowerSlateNotReady(acc); // BUG1+BUG2: bracket wipe imminent (3-param path)
            _laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _); // Lane E: reset re-arm dedup
            var stale = new System.Collections.Generic.List<Order>();
            int raceSkipped = 0;
            foreach (Order o in acc.Orders) // (2)
            {
                bool stateOk =
                    o.OrderState == OrderState.Working
                    || o.OrderState == OrderState.Initialized
                    || o.OrderState == OrderState.Accepted
                    || o.OrderState == OrderState.Submitted
                    || o.OrderState == OrderState.TriggerPending;
                if (!stateOk)
                    continue; // (3)
                if (o.Instrument == null || o.Instrument.FullName != instr.FullName)
                    continue; // (4)
                if (snapshot != null && !snapshot.Contains(o))
                {
                    raceSkipped++;
                    continue;
                } // (5)
                if (IsQxCancelCandidate(o, isFollower)) // (6)
                    stale.Add(o);
            }
            NinjaTrader.Code.Output.Process(
                "[PTT-QX] cancel: "
                    + stale.Count
                    + " queued, "
                    + raceSkipped
                    + " race-skipped on "
                    + acc.Name,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            if (stale.Count == 0)
                return; // (7)
            stale.RemoveAll(o =>
                o.OrderState == OrderState.Filled || o.OrderState == OrderState.Cancelled
            ); // DW-B79-09: race guard
            try
            {
                acc.Cancel(stale.ToArray());
            }
            catch { }
        }

        // B69 DW-B69-01: CancelAllAccountOrders -- cancel every active order on acc for instr
        // before submitting a market flatten. No name filter -- all order names cancelled.
        // NT8 precedent: @2Custom-0909edcc EmergencyFlattenSingleFleetAccount [938-EF-GUARD]:
        //   "Step 1: Cancel ALL working orders on this instrument for this account."
        //   States: Working|Submitted|Accepted|ChangePending.
        // CYC=4: null-guard(1) + foreach(2) + stateOk-4terms(3) + instrument-name(4). JS-021: no lock.
        // JS-001: no throw. JS-002: void. ASCII-only.
        internal void CancelAllAccountOrders(Account acc, NinjaTrader.Cbi.Instrument instr)
        {
            if (acc == null || instr == null)
                return; // (1)
            var toCancel = new System.Collections.Generic.List<Order>();
            foreach (Order o in acc.Orders) // (2)
            {
                bool stateOk =
                    o.OrderState == OrderState.Working
                    || o.OrderState == OrderState.Initialized
                    || o.OrderState == OrderState.Submitted
                    || o.OrderState == OrderState.Accepted;
                if (!stateOk)
                    continue; // (3)
                if (o.Instrument == null || o.Instrument.FullName != instr.FullName)
                    continue; // (4)
                toCancel.Add(o);
            }
            // DW-B79-04: belt-and-suspenders race guard -- discard orders that
            // transitioned to terminal state between snapshot and cancel call.
            toCancel.RemoveAll(o =>
                o.OrderState == OrderState.Filled || o.OrderState == OrderState.Cancelled
            );
            if (toCancel.Count == 0)
                return;
            try
            {
                acc.Cancel(toCancel);
            }
            catch { }
        }

        // B68 DW-B68-01: CancelQxBracketsForFollowers -- cancel stale brackets on all followers.
        // Called by PttGlobalQuickExit.Execute before placing new PTT-QX-* orders on the leader.
        // Ensures follower ATM brackets (Stop1/Stop2/Target1/Target2) and prior PTT-QX-*/PTT-BE-*
        // orders do not persist as stale orders alongside new QX bracket pairs.
        // CYC=5: instr-null-guard(1) + rule-null-guard(2) + foreach(3) + acc-null-guard(4) + delegate(5).
        // JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous void.
        // NT8-REF: Account.Cancel -- via CancelQxBrackets (existing, tested, line 462).
        internal void CancelQxBracketsForFollowers(NinjaTrader.Cbi.Instrument instr)
        {
            if (instr == null)
                return; // (1)
            var rule = FindRule(instr);
            if (rule == null)
                return; // (2)
            foreach (var acc in rule.Value.FollowerAccounts) // (3)
            {
                if (acc == null)
                    continue; // (4)
                CancelQxBrackets(acc, instr); // (5)
            }
        }

        // NextQxOcoId: monotonic OCO group ID for Quick Exit bracket pairs.
        // Uses Interlocked.Increment on _qxOcoSeq (thread-safe, no lock).
        // CYC=1: straight expression. JS-021: no lock -- Interlocked.
        // B70 DW-B70-01: seed with TickCount & 0x7FFF (0..32767) to avoid ID reuse on session reconnect.
        private int _qxOcoSeq = Environment.TickCount & 0x7FFF;

        internal string NextQxOcoId() =>
            "PTT-QX-" + System.Threading.Interlocked.Increment(ref _qxOcoSeq).ToString("D5");

        // B66 DW-B66-BE-01: SubmitBeStop -- submit a StopMarket order at bePrice for acc+instr.
        // FIX: isLong is now a parameter -- callers pass direction at their own snapshot-read time.
        // Removed: internal pos.MarketPosition re-read (was racing with NT8 position update lag --
        //   NT8_FULL_REFERENCE.md line 1721: "Changes to positions will not be reflected till at
        //   least the next OnBarUpdate() event after an order fill.").
        // B65 precedent: same race fixed in TryDispatchLeaderFlat (CopyEngine.cs lines 651-654).
        // BWAVE-CYC TB-T3: extracted FindBePosition + SubmitBeStopOrder to reduce CCN from 10 to <=5.
        // JS-001: no throw. JS-002: void. JS-033: synchronous void. JS-021: no lock.
        // B69 DW-B69-02: pos-find uses FullName comparison -- preserved inside FindBePosition.
        // NT8: same contract can exist as 2 different Instrument objects across account contexts.
        internal void SubmitBeStop(
            Account acc,
            NinjaTrader.Cbi.Instrument instr,
            double bePrice,
            bool isLong
        )
        {
            if (acc == null || instr == null) // (1) + ||
                return;
            var pos = FindBePosition(acc, instr); // (2)
            if (pos == null || pos.Quantity == 0) // (3) + ||
                return;
            OrderAction dir = isLong ? OrderAction.Sell : OrderAction.BuyToCover; // (4)
            SubmitBeStopOrder(acc, instr, dir, pos.Quantity, bePrice); // (5)
        }

        // BWAVE-CYC TB-T3: FindBePosition -- locate the position for the given instrument in the account.
        // Absorbs: foreach loop + p.Instrument null guard + FullName match from SubmitBeStop.
        // CCN=3: base(1) + foreach(1) + inner null-guard(1). FullName comparison per B69 DW-B69-02.
        // internal: accessible to xUnit via InternalsVisibleTo("PropTraderTools.Tests") at L46.
        // JS-021: acc.Positions is NT8 read-only collection -- no lock needed.
        internal NinjaTrader.Cbi.Position FindBePosition(
            Account acc,
            NinjaTrader.Cbi.Instrument instr
        )
        {
            foreach (NinjaTrader.Cbi.Position p in acc.Positions) // (1)
                if (
                    p.Instrument != null // (2)
                    && p.Instrument.FullName == instr.FullName
                )
                    return p;
            return null;
        }

        // BWAVE-CYC TB-T3: SubmitBeStopOrder -- submit the StopMarket order via acc.CreateOrder + Submit.
        // Absorbs: try/catch CreateOrder block + order!=null inner guard from SubmitBeStop.
        // CCN=3: base(1) + try/catch(1) + order!=null(1).
        // NT8: acc.CreateOrder(StopMarket) + acc.Submit() -- AddOnBase available (NT8_FULL_REFERENCE.md).
        // NT8-007: last arg is (CustomOrder)null -- not a string.
        // JS-001: no rethrow inside catch. Order name "PTT-BE-Stop" per B66 DW-B66-BE-01.
        // internal: accessible to xUnit via InternalsVisibleTo("PropTraderTools.Tests") at L46.
        internal void SubmitBeStopOrder(
            Account acc,
            NinjaTrader.Cbi.Instrument instr,
            OrderAction dir,
            int qty,
            double bePrice
        )
        {
            try
            {
                var order = acc.CreateOrder(
                    instr,
                    dir,
                    OrderType.StopMarket,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    qty,
                    0,
                    bePrice,
                    string.Empty,
                    "PTT-BE-Stop",
                    DateTime.MaxValue,
                    (NinjaTrader.Cbi.CustomOrder)null
                );
                if (order != null)
                {
                    acc.Submit(new[] { order });
                    NinjaTrader.Code.Output.Process(
                        "[BE] SubmitBeStop: "
                            + dir
                            + " "
                            + qty
                            + " @ "
                            + bePrice.ToString("F2")
                            + " on "
                            + acc.Name,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
                }
            }
            catch { }
        }

        // --- end B56 BUILD-FIX stubs ---

        // Change 7: SetRuleEnabled added after SetDailyCapFloor
        internal void SetRuleEnabled(string instrument, bool enabled)
        {
            var snapshot = new List<CopyRule>(_rules);
            _rules = new ConcurrentBag<CopyRule>();
            foreach (var r in snapshot)
            {
                var updated =
                    r.Instrument == instrument
                        ? CopyRule.Create(
                            r.Instrument,
                            r.MasterAccount,
                            r.FollowerAccounts,
                            enabled,
                            r.FollowerMultipliers,
                            r.FollowerAtmTemplates,
                            r.TightenTicks,
                            r.FollowerAccountNames // B127: preserve names through enabled/disabled rebuild
                        )
                        : r;
                _rules.Add(updated);
            }
        }

        // Original 3-arg overload -- PRESERVED UNCHANGED (backward compat with all 27 existing tests)
        // BGTM-1: MultiRule gate CYC=2.
        internal void AddRule(string instrument, Account master, Account[] followers)
        {
            if (!_flags.MultiRule && _rules.Count >= 1)
            {
                StatusUpdate?.Invoke(
                    "Multi-rule requires Pro. Upgrade at proptradertools.com/pricing"
                );
                return;
            }
            _rules.Add(CopyRule.Create(instrument, master, followers));
        }

        // B8 T1: new 5-arg overload -- adds multipliers + ATM map at apply time
        // B23 T1 (DW-B22-ADDRULE-ACCUMULATE-01): replace-not-append for same (instrument, leader).
        // ConcurrentBag rebuild pattern -- no lock (JS-021). Same pattern as SetFollowerMultiplier.
        // CYC=4: foreach(1) + string == (2) + name == (3) + continue(4 -- implicit else branch). BGTM-1: MultiRule gate CYC=5.
        internal void AddRule(
            string instrument,
            Account master,
            Account[] followers,
            int[] multipliers,
            Dictionary<string, FollowerAtmMode> atmMap
        )
        {
            if (!_flags.MultiRule && _rules.Count >= 1)
            {
                StatusUpdate?.Invoke(
                    "Multi-rule requires Pro. Upgrade at proptradertools.com/pricing"
                );
                return;
            }
            var snapshot = new List<CopyRule>(_rules);
            _rules = new ConcurrentBag<CopyRule>();
            foreach (var r in snapshot)
            {
                if (r.Instrument == instrument && r.MasterAccount?.Name == master?.Name)
                    continue;
                _rules.Add(r);
            }
            _rules.Add(CopyRule.Create(instrument, master, followers, true, multipliers, atmMap));
        }

        // B8 T1: post-create mutation of a single follower's multiplier
        // ConcurrentBag rebuild pattern -- no lock (JS-021)
        // Clamps multiplier to [1, 10] for safety
        internal void SetFollowerMultiplier(string instrument, int followerIndex, int multiplier)
        {
            int clamped = Math.Max(1, Math.Min(10, multiplier));
            var snapshot = new List<CopyRule>(_rules);
            _rules = new ConcurrentBag<CopyRule>();
            foreach (var r in snapshot)
            {
                if (r.Instrument != instrument)
                {
                    _rules.Add(r);
                    continue;
                }
                var newMults = BuildUpdatedMultipliers(
                    r.FollowerMultipliers,
                    followerIndex,
                    clamped,
                    r.FollowerAccounts?.Length ?? 0
                );
                _rules.Add(
                    CopyRule.Create(
                        r.Instrument,
                        r.MasterAccount,
                        r.FollowerAccounts,
                        r.Enabled,
                        newMults,
                        r.FollowerAtmTemplates,
                        r.TightenTicks,
                        r.FollowerAccountNames // B127: preserve names through multiplier rebuild
                    )
                );
            }
        }

        // Helper for SetFollowerMultiplier -- builds a new multiplier array with one entry updated.
        // CYC=3 (null guard + bounds guard + copy loop). No throw, no return null.
        private static int[] BuildUpdatedMultipliers(
            int[] existing,
            int index,
            int value,
            int count
        )
        {
            int len = count > 0 ? count : (existing != null ? existing.Length : 0);
            if (len == 0)
                return existing;
            var result = new int[len];
            for (int i = 0; i < len; i++)
                result[i] = (existing != null && i < existing.Length) ? existing[i] : 1;
            if (index >= 0 && index < len)
                result[index] = value;
            return result;
        }

        internal void Subscribe()
        {
            foreach (Account acc in Account.All)
            {
                acc.OrderUpdate += OnOrderUpdate;
                acc.PositionUpdate += OnFollowerPositionUpdate; // BUG2
            }
        }

        internal void Unsubscribe()
        {
            foreach (Account acc in Account.All)
            {
                acc.OrderUpdate -= OnOrderUpdate;
                acc.PositionUpdate -= OnFollowerPositionUpdate; // BUG2
            }
        }

        // --- Hot path: CYC=8 (B75-LaneA second pass). All sub-blocks extracted to helpers. ---
        private void OnOrderUpdate(object sender, OrderEventArgs e)
        {
            // B62: evict dedup on terminal states so orderId is not permanently blocked.
            // DR-5: pass instrKey to EvictDedup so it can remove _liveEntryInstruments directly
            // when _entryInstrKeyByOrderId misses (NT8/Rithmic orderId reassignment -- RC-2).
            // instrKey computed here because Order object is only available in OnOrderUpdate.
            EvictDedup(
                e.Order.OrderId.ToString(),
                e.Order.OrderState,
                e.Order.Instrument?.FullName + "|" + e.Order.OrderAction
            );
            TryLogDragTrace(e.Order);

            // HOTFIX-FLAT-DISARM-FOLLOWER: extracted to TryFireFollowerBeDisarm (CYC=8).
            // Fires PositionStateChanged when a follower PTT-BE-Stop fills. JS-021: no lock.
            TryFireFollowerBeDisarm(e);

            // DW-B79-06: event-driven BE retry -- fires MoveStopToBreakEven the instant a
            // PTT-QX-T* order goes Working on a follower with a pending BE slot. Zero timing.
            TryFireFollowerBeRetry(e);

            // DW-B79-06: evict stale BE retry slot when follower position closes via any path.
            TryEvictFollowerBeSlot(e);

            // B135 DW-B134-OCO: sweep orphaned PTT-drag orders when follower position goes flat.
            TrySweptPttDragOrphans(e);

            // DW-B179: emit [BE-WARN] when PTT-BE-* or PTT-QX-* order cancels and position is flat.
            TryEmitBeFlattenWarn(e);

            // DW-B92: record PTT-BE-Target-* fill.
            TryRecordBeTargetFill(e.Order);

            // B113 DW-B117: cancel-after -- cancel each native ATM bracket one-for-one
            // as the corresponding PTT-QX-T* order confirms Working. Extracted to helper
            // to keep OnOrderUpdate CYC within budget.
            TryCleanupReArmedAtmBracket(e);

            // HOTFIX-B66-COPY-REPLACE / HOTFIX-B66-NATIVE-ATM: re-place follower entry when NT8-ATM
            // cancel sweep wipes it during bracket arming. Predicate + action fused in helper (no branch here).
            TryReplaceOnAtmCancel(e.Order);

            // B208-FIX-F-B: clear BE-swap guard when PTT-BE-Stop-N reaches Working.
            // Must run BEFORE TryNakedDetect so the flag is cleared before the naked-position check.
            // NT8 Working = broker confirmed stop is live -- NakedPositionDetector may now fire freely.
            TryClearBeSwapOnStopWorking(e);

            // DW-NEW-08 Option E: detect naked position within 50ms of terminal order event.
            TryNakedDetect(e);

            // DW-NEW-08 Option D: route cancel-ack to drain handler if account is in drain state.
            // F1-repair: Filled no longer routes to OnDrainCancelAck -- position is open, abort drain.
            // CYC +1 (branch 7) for Cancelled/Rejected; +1 (branch 8) for Filled else-if.
            if (
                e.Order.OrderState == OrderState.Cancelled
                || e.Order.OrderState == OrderState.Rejected
            )
            {
                // B200-CLOSE-LANED-01-V12: clear flatten intent flag on terminal Cancelled event.
                // Guard with order-ID anchor: only clear if this is the CURRENT in-flight flatten.
                // Without the anchor check, stale Filled/Cancelled events from a prior cycle
                // (arriving late on the NT8 background thread) prematurely clear the flag while
                // the new PTT-Flatten is still Accepted/Working -- opening the LaneE gate too early.
                if (e.Order.Name == "PTT-Flatten")
                {
                    string anchoredId;
                    if (!_flattenInFlightOrderId.TryGetValue(e.Order.Account.Name, out anchoredId)
                        || anchoredId == e.Order.OrderId)
                        _flattenInFlight[e.Order.Account.Name] = false;
                }
                if (_pendingDispatchDrains.ContainsKey(e.Order.Account.Name))
                    OnDrainCancelAck(e.Order.Account.Name);
            }
            else if (e.Order.OrderState == OrderState.Filled)
            {
                // B200-CLOSE-LANED-01-V13: clear flatten intent flag on terminal Filled event.
                // Same anchor check as Cancelled branch above.
                if (e.Order.Name == "PTT-Flatten")
                {
                    string anchoredId;
                    if (!_flattenInFlightOrderId.TryGetValue(e.Order.Account.Name, out anchoredId)
                        || anchoredId == e.Order.OrderId)
                        _flattenInFlight[e.Order.Account.Name] = false;
                }
                TrySetSlateReadyOnDrainFill(e);                  // T1: set slate-ready FIRST (_drainOwnedOrderIds intact)
                AbortDrainOnFill(e.Order.Account.Name);          // R2-F1: clean _drainOwnedOrderIds on fill-abort
                TryCancelStaleGtcExitOnEntry(e);                 // T3: sweep stale GTC exits on new entry fill
            }

            // DW-NEW-08 Option D: cheap piggybacked watchdog for stuck drains (>2s).
            // Unconditional -- fires even when copy is disabled. CYC delta=0.
            TryDrainWatchdog();

            // Gate 1: enabled check
            if (!_isCopyEnabled)
            {
                // B199-DIAG: Gate 1 blocker -- copy disabled. Fires on every order event when disabled.
                // Remove this log once dispatch confirmed working. Only logs for Accepted/Working to reduce noise.
                if (e.Order.OrderState == OrderState.Accepted || e.Order.OrderState == OrderState.Working)
                    NinjaTrader.Code.Output.Process(
                        "[PTT-GATE1-BLOCKED] copy disabled name=" + e.Order.Name
                            + " state=" + e.Order.OrderState
                            + " acct=" + e.Order.Account?.Name,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return;
            }

            // Gate 2: find matching rule -- instrument AND master account must match.
            // Extracted to FindMatchingRule (CYC=3).
            CopyRule? matchedRule = FindMatchingRule(e.Order);

            if (matchedRule == null)
            {
                // B199-DIAG: Gate 2 blocker -- no rule matches. Fires when instrument/account not configured.
                if (e.Order.OrderState == OrderState.Accepted || e.Order.OrderState == OrderState.Working)
                    NinjaTrader.Code.Output.Process(
                        "[PTT-GATE2-BLOCKED] no rule instr=" + e.Order.Instrument?.FullName
                            + " acct=" + e.Order.Account?.Name
                            + " state=" + e.Order.OrderState,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return; // Gate 2: no rule match
            }
            if (!matchedRule.Value.Enabled)
            {
                // DR-2: Gate 2.5 diagnostic log -- rule exists but IsEnabled=false. Previously a silent exit.
                // Fires only at Accepted/Working to avoid noise on every Initialized/Submitted callback.
                if (e.Order.OrderState == OrderState.Accepted || e.Order.OrderState == OrderState.Working)
                    NinjaTrader.Code.Output.Process(
                        "[PTT-GATE25-BLOCKED] rule disabled instr=" + e.Order.Instrument?.FullName
                            + " acct=" + e.Order.Account?.Name
                            + " state=" + e.Order.OrderState,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return; // Gate 2.5: rule disabled
            }

            // BUG-BE-RESET fix: fire position state ONLY for leader account+instrument orders.
            TryFirePositionState(e);

            // B9 T3 -- Mirror mode relay (inserted after Gate 2.5, before Gate B)
            TryMirrorOrderUpdate(e.Order, matchedRule.Value);

            // B56 T1: propagate leader cancel to follower entry orders.
            // Extracted to TryCancelFollowerEntries (CYC=4). Includes HOTFIX-B63-COPY-CANCEL-01 guard.
            if (TryCancelFollowerEntries(e.Order, matchedRule.Value))
                return;

            // DW-B60-01: leader went flat -- propagate close to followers
            if (
                TryDispatchLeaderFlat(
                    e.Order.Account,
                    e.Order.Instrument,
                    e.Order.OrderState,
                    e.Order.Name,
                    matchedRule.Value,
                    IsFollowerAccount,
                    HasOpenPosition,
                    FlattenOneAccount
                )
            )
                return;

            // Gate B+C: bracket drag then entry drag -- consolidated into TryHandleDrag (one branch here).
            if (TryHandleDrag(e.Order, matchedRule.Value))
                return;

            // No bracket, no drag -- normal copy dispatch
            DispatchCopy(e.Order, matchedRule.Value);
        }

        // TryFireFollowerBeDisarm: CYC=4. Fires PositionStateChanged when a follower PTT-BE-Stop fills.
        // Called from pre-Gate-1 in OnOrderUpdate. HOTFIX-FLAT-DISARM-FOLLOWER.
        // Guards delegated to IsBeDisarmCandidate (CYC=4) to keep total CYC<=8 per method.
        // JS-021: no lock. JS-001: no throw. JS-002: void.
        private void TryFireFollowerBeDisarm(OrderEventArgs e)
        {
            if (!IsBeDisarmCandidate(e.Order))
                return; // (1)

            bool isLeader = false;
            foreach (var r in _rules) // (2)
            {
                if (e.Order.Account.Name == r.MasterAccount?.Name)
                {
                    isLeader = true;
                    break;
                } // (3)
            }
            if (!isLeader) // (4)
            {
                // Follower PTT-BE stop filled -- fire position state so panel resets BE visual.
                bool hasPos = HasOpenPosition(e.Order.Account, e.Order.Instrument);
                bool hasEntries = HasWorkingEntries(e.Order.Account, e.Order.Instrument);
                PositionStateChanged?.Invoke(
                    e.Order.Instrument.FullName,
                    new PositionState(hasPos, hasEntries)
                );
            }
        }

        // TryFireFollowerBeRetry: DW-B79-06 event-driven BE retry.
        // Fires MoveStopToBreakEven exactly once when a trigger order transitions to Working
        // on a follower account that registered a _pendingFollowerBeSlots entry.
        //
        // Trigger names (guard 2):
        //   PTT-QX-T* -- QX path: QX targets going Working after QX->BE-ALL sequence.
        //   Target1..Target9 -- DW-B79-08 v4: plain new-entry ATM targets going Working.
        //     Fires AFTER the new ATM has fully settled, at the correct stable moment --
        //     avoids MoveStopToBreakEven(isRetry) running acc.Cancel while sweep is active.
        //
        // DW-B82-01: reset _beReplaceAttempts on slot consumption.
        //   The counter was only reset in TryEvictFollowerBeSlot (position-close path), which
        //   races with the QX exit path. QX's 3 PTT-BE-Stop-* cancels exhaust the 3-attempt
        //   limit in one trade; all subsequent trades get "max 3 attempts" immediately and no
        //   brackets are ever placed. Fix: always reset when a slot is atomically claimed here.
        //
        // Extracted: IsPttBeRetryTriggerOrder (name-pattern guard), IsBeRetryStateWorking (state guard).
        // JS-021: ConcurrentDictionary ops are lock-free -- only one caller wins per slot.
        // JS-001: no throw. JS-002: void. ASCII-only.
        private void TryFireFollowerBeRetry(OrderEventArgs e)
        {
            var o = e?.Order;
            if (!IsBeRetryOrderValid(o))
                return; // (1) null guard (extracts || compound)
            if (!IsPttBeRetryTriggerOrder(o))
                return; // (2) name-pattern gate
            if (!IsBeRetryStateWorking(o))
                return; // (3) state gate
            if (!_pendingFollowerBeSlots.TryRemove(o.Account.Name, out var slot)) // (4) atomic claim
                return;
            _beReplaceAttempts.TryRemove(o.Account.Name, out _); // DW-B82-01: reset on slot consumption
            if (IsFlat(FindPosition(slot.Account, slot.Instrument))) // (5) flat guard
                return;
            NinjaTrader.Code.Output.Process(
                "[BE-RETRY] "
                    + o.Account.Name
                    + " "
                    + o.Name
                    + " Working -- event-driven BE retry firing",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            PttBeSwapOne.Execute(slot.Account, slot.Instrument, slot.BufferTicks);
        }

        // IsBeRetryOrderValid: CCN<=3. Null-guard predicate for TryFireFollowerBeRetry.
        // Returns true when order and its required fields are non-null.
        // JS-021: static -- no instance state. ASCII-only.
        private static bool IsBeRetryOrderValid(Order o) =>
            o != null && o.Name != null && o.Account != null;

        // IsPttBeRetryTriggerOrder: CCN<=4. Name-pattern predicate for TryFireFollowerBeRetry.
        // Returns true when the order name is PTT-QX-T* (QX path) or Target1..Target9 (ATM path).
        // JS-021: static -- no instance state. ASCII-only.
        private static bool IsPttBeRetryTriggerOrder(Order o)
        {
            bool isPttQxT =
                o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                && o.Name.Length > 8
                && char.IsDigit(o.Name[8]);
            bool isAtmTgt =
                o.Name.StartsWith("Target", StringComparison.Ordinal) // DW-B79-08 v4
                && o.Name.Length > 6
                && char.IsDigit(o.Name[6]);
            return isPttQxT || isAtmTgt;
        }

        // IsBeRetryStateWorking: CCN<=2. State predicate for TryFireFollowerBeRetry.
        // Returns true when the order is in Working or Accepted state.
        // JS-021: static -- no instance state. ASCII-only.
        private static bool IsBeRetryStateWorking(Order o)
        {
            return o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted;
        }

        // TryEvictFollowerBeSlot: CYC=6. DW-B79-06 stale-slot cleanup.
        // Clears _pendingFollowerBeSlots AND _beReplaceAttempts when follower slot is terminal.
        // DW-B79-08 v8: decouple attempt-counter reset from slot existence.
        //   v3 bug: guard (2) checked ContainsKey(_pendingFollowerBeSlots) before resetting
        //   _beReplaceAttempts. After the 500ms fallback timer consumed the slot via TryRemove,
        //   the slot was gone. On next re-entry, guard (2) returned early and _beReplaceAttempts
        //   stayed at 3 permanently -- blocking all recovery on subsequent trades.
        //   Fix: always reset _beReplaceAttempts when flat, regardless of slot existence.
        //   The slot eviction remains guarded (only remove if it exists), but the counter
        //   reset is unconditional on flat -- it costs nothing when counter is already 0.
        // DW-B81-01: also evict on PTT-BE-Stop Rejected.
        //   Root: when BE fires immediately after entry on a short, market can tick below entry
        //   before NT8 accepts the follower PTT-BE-Stop. NT8 rejects with "Buy stop below market".
        //   TryEvictFollowerBeSlot only fired on Filled -- leaving a stranded slot in
        //   _pendingFollowerBeSlots. Next BE press: TryAdd in TryReplacePttBeBrackets finds
        //   existing slot -> returns early -> NO bracket placed -> follower gets stop-only, no targets.
        //   Fix: evict slot AND reset attempt counter on Rejected for PTT-BE-Stop specifically.
        //   Flat-guard bypassed for Rejected: rejection can happen while position is still open
        //   (stop rejected but trade still live). The retry (500ms fallback) must be free to
        //   re-register a fresh slot.
        // Extracted: IsEvictTriggerState (terminal-state + name guard).
        // JS-021: ConcurrentDictionary ops are lock-free. JS-001: no throw. JS-002: void. ASCII.
        private void TryEvictFollowerBeSlot(OrderEventArgs e)
        {
            var o = e?.Order;
            if (o == null)
                return; // (1) null guard
            TryAbortLeaderFlatDrains(e); // T2 DW-B156-01: leader-flat drain abort -- +0 CYC (method call is not a branch)
            if (!IsEvictTriggerState(o))
                return; // (2) terminal-state gate
            _entryDispatchedOrders.Clear(); // DW-B95: instrument-level reset -- fires for ALL accounts (leader + follower)
            if (!IsFollowerAccount(o.Account))
                return; // (3) follower-only evictions below
            bool isFilled = o.OrderState == OrderState.Filled;
            if (isFilled && !IsFlat(FindPosition(o.Account, o.Instrument)))
                return; // (4) flat-guard for Filled only
            string accName = o.Account.Name; // safe: IsFollowerAccount confirmed non-null above
            bool slotEvicted = _pendingFollowerBeSlots.TryRemove(accName, out _); // DW-B79-04: capture for log gate
            _beReplaceAttempts.TryRemove(accName, out _); // ALWAYS reset on terminal
            _filledBeTargetCount.TryRemove(accName, out _); // DW-B92: clear on flat
            if (slotEvicted) // DW-B79-04: only log if slot was present
                LogBeSlotEviction(accName, o.OrderState == OrderState.Rejected);
        }

        // B197 T2 DW-B156-01 + DW-B156-DEFER-1: TryAbortLeaderFlatDrains -- CYC=5.
        // Aborts all pending dispatch drains when the leader account fills to flat.
        // Prevents ghost re-entry PTT-Copy orders from firing after leader goes flat (DW-B158-SIM-01).
        // Called from TryEvictFollowerBeSlot, after null guard, before IsEvictTriggerState.
        // NAMING NOTE: Originally "IsLeaderFlatDrainAbort" in DW-B156-DEFER-1 backlog. Renamed to
        //   TryAbortLeaderFlatDrains: Is... prefix reserved for boolean predicates in CopyEngine.cs convention.
        // JS-021: _pendingDispatchDrains foreach is ConcurrentDictionary enumeration (lock-free). AbortDrainOnFill uses TryRemove.
        // JS-001: no throw. JS-002: void. ASCII-only.
        private void TryAbortLeaderFlatDrains(OrderEventArgs e)
        {
            if (IsFollowerAccount(e.Order.Account)) return;                           // (1)
            if (e.Order.OrderState != OrderState.Filled) return;                      // (2)
            if (!IsFlat(FindPosition(e.Order.Account, e.Order.Instrument))) return;   // (3)
            foreach (var kv in _pendingDispatchDrains.ToList())
                AbortDrainOnFill(kv.Key);
            NinjaTrader.Code.Output.Process(
                "[DRAIN-ABORT-LEADER-FLAT] triggered by " + e.Order.Account.Name,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        }

        // LogBeSlotEviction: CCN=1. Diagnostic log helper for TryEvictFollowerBeSlot.
        // Moves the isRejected ternary out of parent to reduce parent CCN.
        // JS-001: no throw. ASCII-only.
        private static void LogBeSlotEviction(string accName, bool isRejected)
        {
            string reason = isRejected ? "PTT-BE-Stop Rejected" : "position closed";
            NinjaTrader.Code.Output.Process(
                "[BE-RETRY] "
                    + accName
                    + " "
                    + reason
                    + " -- evicted BE slot + reset attempt counter",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
        }

        // IsEvictTriggerState: CCN<=4. Terminal-state predicate for TryEvictFollowerBeSlot.
        // Returns true when order is Filled (any) or Rejected for PTT-BE-Stop specifically (DW-B81-01).
        // JS-021: static -- no instance state. ASCII-only.
        private static bool IsEvictTriggerState(Order o)
        {
            bool isFilled = o.OrderState == OrderState.Filled;
            bool isRejected =
                o.OrderState == OrderState.Rejected // DW-B81-01
                && o.Name == "PTT-BE-Stop";
            return isFilled || isRejected;
        }

        // TB-T5 test seams -- expose private static helpers for xUnit tests via InternalsVisibleTo.
        // Accepts nullable string for null-name guard path.
        internal static bool IsPttBeRetryTriggerOrderTestable(string name)
        {
            if (name == null)
                return false;
            bool isPttQxT =
                name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                && name.Length > 8
                && char.IsDigit(name[8]);
            bool isAtmTgt =
                name.StartsWith("Target", StringComparison.Ordinal)
                && name.Length > 6
                && char.IsDigit(name[6]);
            return isPttQxT || isAtmTgt;
        }

        // CYC=4: (1) null guard, (2) prefix check, (3) parse check, (4) range check.
        // DW-B215-LA-01: adds multi-digit PTT-QX-T* target support (T1..T99+).
        private static bool IsPttQxTarget(string name)
        {
            if (name == null) return false;
            if (!name.StartsWith("PTT-QX-T", StringComparison.Ordinal)) return false;
            if (!int.TryParse(name.Substring(8), out int n)) return false;
            return n >= 1;
        }

        // Accepts primitive state + name to avoid constructing Order in tests.
        internal static bool IsEvictTriggerStateTestable(OrderState state, string name)
        {
            bool isFilled = state == OrderState.Filled;
            bool isRejected = state == OrderState.Rejected && name == "PTT-BE-Stop";
            return isFilled || isRejected;
        }

        // B135 DW-B134-OCO: sweep orphaned PTT-drag orders when follower position goes flat.
        // PTT-TGT-Drag and PTT-STP-Drag are standalone (oco="") -- not in any NT8 ATM OCO group.
        // When ATM fills naturally, NT8 only cancels OCO-linked (green) orders; PTT-drag orders survive.
        // Fire on Filled + follower + flat -- same pattern as TryEvictFollowerBeSlot (L1538).
        // CYC=5: base(1) + o null guard(1) + Filled guard(1) + follower guard(1) + flat guard(1) = 5.
        // JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only.
        private void TrySweptPttDragOrphans(OrderEventArgs e)
        {
            var o = e?.Order;
            if (o == null) // (1)
                return;
            if (o.OrderState != OrderState.Filled) // (2)
                return;
            if (!IsFollowerAccount(o.Account)) // (3)
                return;
            if (!IsFlat(FindPosition(o.Account, o.Instrument))) // (4)
                return;
            CancelPttDragOrphansForAccount(o.Account, o.Instrument);
        }

        // B135 DW-B134-OCO: test seam -- delegates to TrySweptPttDragOrphans for xUnit test access.
        internal void TrySweptPttDragOrphansTestable(OrderEventArgs e) => TrySweptPttDragOrphans(e);

        // TryEmitBeFlattenWarn: DW-B179 -- emit [BE-WARN] when a PTT-BE-* or PTT-QX-* order
        // cancels while the account position is already flat (flatten overtook BE operation).
        // Called from OnOrderUpdate pre-Gate-1 block. Stateless: no armed-state required.
        // CYC=4: (1) state != Cancelled, (2) name prefix, (3) pos qty > 0, (4) emit.
        // JS-001: no throw. JS-021: no lock. JS-002: void. ASCII-only.
        private void TryEmitBeFlattenWarn(OrderEventArgs e)
        {
            if (e.Order.OrderState != OrderState.Cancelled)                        // (1)
                return;
            var name = e.Order.Name ?? string.Empty;
            if (!name.StartsWith("PTT-BE-") && !name.StartsWith("PTT-QX-"))       // (2)
                return;
            var pos = FindPositionPublic(e.Order.Account, e.Order.Instrument);
            if (pos != null && pos.Quantity > 0)                                   // (3)
                return; // position still open -- not a flatten race
            NinjaTrader.Code.Output.Process(                                        // (4)
                "[BE-WARN] " + e.Order.Account.Name
                    + " flatten overtook BE All -- orders cancelled",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
        }

        // CheckAtmTemplatesForReverse: DW-B181-G5 -- scan ATM template XMLs for Reverse=Yes.
        // Emits [PTT-STARTUP-WARN] for each template with Reverse=true to alert user before trading.
        // Overload (1): production path -- reads from NinjaTrader.Core.Globals.UserDataDir.
        // Overload (2): test seam -- accepts explicit dirPath for unit test injection.
        // CYC=1: straight delegation. JS-001: no throw. JS-002: void. ASCII-only.
        internal void CheckAtmTemplatesForReverse()
            => CheckAtmTemplatesForReverse(
                System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "templates", "AtmStrategy"));

        // CYC=8 after T3 extraction: (1) try/catch outer, (2) dir not found, (3) foreach file,
        //         (4) already warned, (5) try/catch per file, (6) reverse check + emit,
        //         (7) if (TryParseAtmTemplateGroupQty) call-site branch. Removed T2b try/catch=-4, +1=net 8.
        // JS-001: no throw (try/catch). JS-002: void. JS-021: no lock. ASCII-only. JS-066: CYC=8 <= 8.
        internal void CheckAtmTemplatesForReverse(string atmDir)
        {
            try                                                                     // (1) outer try
            {
                if (!System.IO.Directory.Exists(atmDir))                           // (2) dir not found
                {
                    NinjaTrader.Code.Output.Process(
                        "[PTT-STARTUP-WARN] ATM template dir not found: " + atmDir + " -- skipping Reverse check",
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                    return;
                }
                foreach (var file in System.IO.Directory.GetFiles(atmDir, "*.xml"))  // (3) foreach
                {
                    var name = System.IO.Path.GetFileNameWithoutExtension(file);
                    if (_warnedAtmTemplates.Contains(name))                        // (4) already warned
                        continue;
                    try                                                             // (5) per-file try
                    {
                        var xml = System.IO.File.ReadAllText(file);
                        bool hasReverse =
                            xml.IndexOf("<Reverse>true</Reverse>",
                                StringComparison.OrdinalIgnoreCase) >= 0
                            || xml.IndexOf("Reverse=\"true\"",
                                StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!hasReverse)                                            // (6) reverse check
                            continue;
                        _warnedAtmTemplates.Add(name);
                        NinjaTrader.Code.Output.Process(
                            "[PTT-STARTUP-WARN] ATM template " + name
                                + " has Reverse=Yes -- stop fills will create reverse entries."
                                + " Disable Reverse in this template before trading.",
                            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                        StatusUpdate?.Invoke("[PTT-WARN] ATM template " + name + " has Reverse=true -- may cause unintended position reversal");
                    }
                    catch (Exception)
                    {
                        // Per-file catch: skip unreadable file, continue scan.
                        // Consistent with JS-001: no rethrow, no propagation.
                    }
                    // T3 -- DW-B198-CHECKREV-CYC-01: delegate to helper; TryAdd at call site.
                    if (TryParseAtmTemplateGroupQty(file, out int qty))
                        _atmTemplateGroupQty.TryAdd(name, qty);
                }
            }
            catch (Exception ex)                                                    // outer catch
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-STARTUP-WARN] ATM template scan failed: " + ex.Message,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            }
        }

        // T3 -- DW-B198-CHECKREV-CYC-01: extracted qty-parse helper.
        // CYC = 3 (1 base + 1 catch + 1 compound if-guard).
        // JS-021: no lock; caller handles ConcurrentDictionary.TryAdd at call site.
        // JS-001: catch block silences exception -- no rethrow, no throw.
        // ASCII-only. internal for unit-testing via InternalsVisibleTo.
        internal bool TryParseAtmTemplateGroupQty(string filePath, out int perGroupQty)
        {
            perGroupQty = 0;
            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.Load(filePath);
                var qtyNode = doc.SelectSingleNode("//Quantity");
                if (qtyNode != null
                    && int.TryParse(qtyNode.InnerText, out perGroupQty)
                    && perGroupQty > 0)
                    return true;
            }
            catch (Exception)
            {
                // Per-file qty-parse: skip unreadable/unparsable file. JS-001: no rethrow.
            }
            return false;
        }

        // T2b (H2): TryGetAtmTemplateGroupCount -- CYC=2.
        // Returns per-group qty for the named ATM template (populated by CheckAtmTemplatesForReverse).
        // Returns true + groupCount if found; false (groupCount=0) if not found or templateName null/empty.
        // JS-021: no lock -- ConcurrentDictionary.TryGetValue is lock-free. JS-001: no throw. ASCII-only.
        private bool TryGetAtmTemplateGroupCount(string templateName, out int groupCount)
        {
            if (string.IsNullOrEmpty(templateName))    // (1) null guard
            {
                groupCount = 0;
                return false;
            }
            return _atmTemplateGroupQty.TryGetValue(templateName, out groupCount);
        }

        // B197 T3 DW-B154-02: IsStaleGtcExitOrder -- CYC=5.
        // Predicate: returns true if order is a Working PTT-BE-* or PTT-QX-* exit (stale GTC candidate).
        // Used by TryCancelStaleGtcExitOnEntry to filter acc.Orders sweep.
        // JS-021: no lock. JS-001: no throw. Static -- no instance state.
        private static bool IsStaleGtcExitOrder(Order o)
        {
            if (o == null || o.Name == null) return false;                             // (1) if + (2) ||
            if (o.OrderState != OrderState.Working) return false;                      // (3)
            return o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)             // (4) return-||
                || o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal);
        }

        // B197 T3 DW-B154-01 + DW-B154-02: TryCancelStaleGtcExitOnEntry -- CYC<=8.
        // Sweeps stale Working PTT-BE-* and PTT-QX-* exit orders when a new follower entry fills.
        // GUARD (3) RATIONALE: IsPttCopyEntryFilled was REMOVED in B180. Stub at L837 always returns false.
        //   Using the stub would make this method a permanent no-op. Inline name-check is the correct guard.
        //   "PTT-Copy" = copy-dispatched entry (L4844). "Entry" = Named ATM clone entry (L4196, L4823).
        // JS-021: acc.Orders.ToList() + acc.Cancel() mirror CancelPttDragOrphansForAccount. No lock.
        // JS-001: per-order cancel delegated to TryCancelSingleStaleGtcExit to keep CYC<=8. ASCII-only.
        private void TryCancelStaleGtcExitOnEntry(OrderEventArgs e)
        {
            var order = e?.Order;
            if (order == null) return;                                                 // (1)
            if (order.OrderState != OrderState.Filled) return;                        // (2)
            if (order.Name == null                                                     // (3) if-branch
                || (!order.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)     //     || operator
                    && order.Name != "Entry")) return;                                //     && operator
            if (order.Account == null) return;                                        // (4)
            var acc = order.Account;
            foreach (var o in acc.Orders.ToList())                                    // foreach
                TryCancelSingleStaleGtcExit(acc, o);
        }

        // B208-FIX-G-2: do not cancel PTT-BE-* orders while BE-swap is in progress.
        // IsBeSwapInProgress stays true until PTT-BE-Stop-N reaches a terminal state (FIX-G-1).
        // Without this guard, a rapid PTT-Copy/Entry fill can sweep a Working PTT-BE-Stop-N
        // via TryCancelStaleGtcExitOnEntry, leaving the follower unprotected.
        // CYC: +1 branch. ASCII-only. JS-021: IsBeSwapInProgress is lock-free. JS-001: no throw.
        private void TryCancelSingleStaleGtcExit(Account acc, Order o)
        {
            if (!IsStaleGtcExitOrder(o)) return;
            // B208-FIX-G-2: protect live PTT-BE-Stop-N during active swap window.
            if (o.Name != null
                && o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)
                && IsBeSwapInProgress(acc.Name))
                return;
            try
            {
                acc.Cancel(new Order[] { o });
                if (StatusUpdate != null)
                    StatusUpdate(acc.Name + ": [GTC-SWEEP] cancelled " + o.Name);
            }
            catch (Exception ex)
            {
                if (StatusUpdate != null)
                    StatusUpdate("[GTC-SWEEP] cancel error: " + ex.Message);
            }
        }

        // B135 DW-B134-OCO: cancel all Working PTT-TGT-Drag and PTT-STP-Drag orders for this account+instrument.
        // Called ONLY when position is confirmed flat (TrySweptPttDragOrphans gate).
        // TB-T6: IsPttDragOrphanCancellable -- CCN<=4. Predicate for CancelPttDragOrphansForAccount filter.
        // Absorbs: state guard (Working) + instrument FullName guard + PTT drag name guard.
        // NT8-014: "PTT-TGT-Drag" + suffixed variants (B141/B142: PTT-TGT-Drag-N) -- StartsWith covers all.
        // JS-021: no lock. JS-001: no throw. Static -- no instance state required.
        // TESTABILITY: private static; testable via IsPttDragOrphanCancellableTestable seam below.
        private static bool IsPttDragOrphanCancellable(Order o, Instrument instr)
        {
            if (o.OrderState != OrderState.Working) // (1)
                return false;
            if (o.Instrument?.FullName != instr?.FullName) // (2) ?. counts as 1 Lizard
                return false;
            if (o.Name == null) return false;                                              // (3-null)
            if (!o.Name.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal) // (3) && = 1 extra Lizard
                && !o.Name.StartsWith("PTT-STP-Drag", StringComparison.Ordinal))
                return false;
            return true;
        }

        // TB-T6: test seam for IsPttDragOrphanCancellable -- accepts primitives to avoid constructing NT8 Order/Instrument in xUnit.
        // Mirrors IsEvictTriggerStateTestable pattern (L1636).
        internal static bool IsPttDragOrphanCancellableTestable(
            OrderState orderState,
            string orderInstrFullName,
            string instrFullName,
            string orderName
        )
        {
            if (orderState != OrderState.Working)
                return false;
            if (orderInstrFullName != instrFullName)
                return false;
            if (orderName == null) return false;                                           // (3-null)
            if (!orderName.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)
                && !orderName.StartsWith("PTT-STP-Drag", StringComparison.Ordinal))
                return false;
            return true;
        }

        // acc.Orders.ToList() is safe in OnOrderUpdate callback thread (existing pattern: L2322).
        // try/catch: absorbs ErrorCode.UnableToCancelOrder (existing pattern: SyncAtmFollowerBracket L2259-2266).
        // TB-T6: extracted IsPttDragOrphanCancellable -- CCN<=5 after extraction.
        // JS-021: no lock. JS-001: try/catch -- no throw in hot path. JS-002: void. ASCII-only.
        // NT8-014: "PTT-TGT-Drag" confirmed L2362, "PTT-STP-Drag" confirmed L2281. acc.Cancel confirmed AddOnBase.
        private void CancelPttDragOrphansForAccount(Account acc, Instrument instr)
        {
            foreach (var o in acc.Orders.ToList()) // (1)
            {
                if (!IsPttDragOrphanCancellable(o, instr)) // (2)
                    continue;
                try
                {
                    acc.Cancel(new Order[] { o });
                    StatusUpdate?.Invoke(acc.Name + ": PTT drag sweep: cancelled " + o.Name);
                }
                catch (Exception ex)
                {
                    StatusUpdate?.Invoke(acc.Name + ": PTT drag sweep cancel error: " + ex.Message);
                }
            }
        }

        // B135 DW-B134-OCO: test seam -- delegates to CancelPttDragOrphansForAccount for xUnit test access.
        internal void CancelPttDragOrphansForAccountTestable(Account acc, Instrument instr) =>
            CancelPttDragOrphansForAccount(acc, instr);

        // QueueBeRetryFallback: CYC=1. Configurable-delay DispatcherTimer fallback for the event-driven BE retry.
        // DW-B79-06/07: fires MoveStopToBreakEven(isRetry:true) if TryFireFollowerBeRetry missed
        // the trigger event (PTT-QX-T already Cancelled before slot was registered).
        // DW-B79-08 v6: delayMs parameter (default 200ms for QX path, 500ms for ATM-sweep path).
        //   200ms path (QX): event-driven missed because QX target already Cancelled before slot registered.
        //   500ms path (ATM sweep): new ATM brackets go Working BEFORE PTT-BE-Stop cancel arrives,
        //     so TryFireFollowerBeRetry sees no slot. 500ms fires after ATM fully settled.
        // DW-B79-08 v7: timer MUST be created on the WPF UI dispatcher thread.
        //   OnOrderUpdate fires on NT8's order-update background thread. A DispatcherTimer created
        //   on a background thread uses that thread's dispatcher which never pumps -- Tick never fires.
        //   Fix: wrap timer construction in Application.Current.Dispatcher.InvokeAsync so it runs
        //   on the WPF UI thread whose dispatcher pumps normally. Same pattern as RaiseBeBufferChanged.
        // TryRemove is the atomic claim gate: if the event-driven path already consumed the slot,
        // TryRemove returns false and this callback is a no-op. Exactly one path wins.
        // CYC=1: straight sequence (no branches in method body -- timer lambda is not a branch here).
        // JS-021: no lock. JS-001: no throw. JS-033: Tick is not async void. ASCII-only.
        private void QueueBeRetryFallback(
            Account acc,
            Instrument instrument,
            int bufferTicks,
            int delayMs = 200
        )
        {
            var capturedAcc = acc;
            var capturedInstr = instrument;
            var capturedBuf = bufferTicks;
            // DW-B79-08 v7: marshal onto WPF UI thread so DispatcherTimer.Tick actually fires.
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var timer = new System.Windows.Threading.DispatcherTimer(
                    System.Windows.Threading.DispatcherPriority.Background
                )
                {
                    Interval = System.TimeSpan.FromMilliseconds(delayMs),
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    if (_pendingFollowerBeSlots.TryRemove(capturedAcc.Name, out var slot))
                    {
                        bool flat = IsFlat(FindPosition(slot.Account, slot.Instrument));
                        NinjaTrader.Code.Output.Process(
                            "[BE-RETRY] "
                                + capturedAcc.Name
                                + " -- fallback timer fired, flat="
                                + flat,
                            NinjaTrader.NinjaScript.PrintTo.OutputTab1
                        );
                        if (!flat)
                            PttBeSwapOne.Execute(slot.Account, slot.Instrument, slot.BufferTicks);
                    }
                    else
                    {
                        NinjaTrader.Code.Output.Process(
                            "[BE-RETRY] "
                                + capturedAcc.Name
                                + " -- fallback TryRemove=false (slot already consumed or evicted)",
                            NinjaTrader.NinjaScript.PrintTo.OutputTab1
                        );
                    }
                };
                timer.Start();
            });
        }

        // FindMatchingRule: CYC=3. Finds the CopyRule whose Instrument and MasterAccount match the order.
        // Returns null if no rule matches. Called from Gate 2 in OnOrderUpdate.
        // JS-021: no lock. JS-002: null return is Option-style (caller guards immediately).
        private CopyRule? FindMatchingRule(Order order)
        {
            foreach (var rule in _rules)
            {
                if (
                    order.Instrument.FullName == rule.Instrument
                    && order.Account.Name == rule.MasterAccount?.Name
                )
                    return rule;
            }
            return null;
        }

        // TryCancelFollowerEntries: CYC=4 (was 6). Propagates leader cancel to scoped follower entry orders.
        // Returns true if Cancelled state was handled (caller should return immediately).
        // HOTFIX-B63-COPY-CANCEL-01: ATM bracket cancels are skipped via IsAtmBracketName guard.
        // DW-B103: PTT exit bracket OCO-cancels return false (do not wipe follower brackets).
        // DW-B136 Gap B: delegates to CancelScopedFollowerEntries (order-ID scoped, not instrument-scoped).
        // JS-021: no lock. JS-001: no throw.
        private bool TryCancelFollowerEntries(Order order, CopyRule rule)
        {
            if (order.OrderState != OrderState.Cancelled)
                return false;
            if (IsAtmBracketName(order.Name))
            {
                TryRemoveDragSuffix(order); // DW-B168-02: clear in-flight record before event is consumed
                return true; // HOTFIX-B63-COPY-CANCEL-01
            }
            if (
                order.Name != null
                && (
                    order.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)
                    || order.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)
                )
            )
                return false; // DW-B103: OCO-cancel of PTT exit bracket must not wipe follower brackets
            // DW-B136 Gap B: scope cancel to specific leader order, not all instrument entries.
            // Single-entry best practice: one leader entry per instrument at a time is the supported
            // workflow. This fix prevents collateral cancel when the constraint is violated (two
            // simultaneous entries). The constraint documentation in the spec and UI tooltip is preserved.
            // Note: rule param is unused post-fix; preserved for call-site stability (one call site: L1361).
            CancelScopedFollowerEntries(order.OrderId.ToString());
            return true;
        }

        // DW-B136 Gap B: record follower Order under the leader orderId that triggered the copy.
        // Called from SendCopy and SendCopyWithAtm after follower.Submit (or StartAtmStrategy) succeeds.
        // Key: leaderOrderId (same string as in _dedupCache and _entryDispatchedOrders).
        // Value: ConcurrentBag<Order> -- thread-safe add, no lock().
        // CYC=1: no branches. JS-021: lock-free (ConcurrentDictionary.GetOrAdd + ConcurrentBag.Add).
        // JS-001: no throw. JS-002: void.
        internal void RecordFollowerCopy(string leaderOrderId, Order followerOrder)
        {
            var bag = _followerCopyMap.GetOrAdd(leaderOrderId, _ => new ConcurrentBag<Order>());
            bag.Add(followerOrder);
        }

        // DW-B136 Gap B: cancel only follower orders recorded under the given leader order ID.
        // Replaces the instrument-scoped sweep in TryCancelFollowerEntries (CancelOneAccount).
        // Called from TryCancelFollowerEntries AFTER EvictDedup has already fired in OnOrderUpdate
        // (L1277 vs L1361). The map entry for leaderOrderId must still be present at this point --
        // EvictDedup does NOT touch _followerCopyMap (see LaneB-02-architecture-plan.md Section 4d).
        // CYC=5:
        //   (1) TryGetValue miss guard
        //   (2) foreach bag
        //   (3) OrderState guard (Working || Initialized)
        //   (4) try body
        //   (5) catch
        // JS-021: no lock. JS-001: catch logs, no rethrow. JS-002: void.
        // NT8: fo.Account.Cancel(Order[]) valid from AddOn context (NT8_ADDON_KNOWLEDGE.md line 222).
        // Eviction: TryRemove called after loop -- sole eviction point on cancel path.
        internal void CancelScopedFollowerEntries(string leaderOrderId)
        {
            if (!_followerCopyMap.TryGetValue(leaderOrderId, out var bag)) // (1)
                return;
            foreach (var fo in bag) // (2)
            {
                if ( // (3)
                    fo.OrderState != OrderState.Working
                    && fo.OrderState != OrderState.Initialized
                )
                    continue;
                try // (4)
                {
                    fo.Account.Cancel(new Order[] { fo });
                    StatusUpdate?.Invoke(
                        fo.Account.Name + ": scoped cancel orderId=" + leaderOrderId
                    );
                }
                catch (Exception ex) // (5)
                {
                    StatusUpdate?.Invoke("PTT-ScopedCancel error: " + ex.Message);
                }
            }
            _followerCopyMap.TryRemove(leaderOrderId, out _); // DW-B136 Gap B: evict after use (sole eviction point)
        }

        // TryHandleBracketDrag: CYC=3. Gate B bracket drag detection -- diverts to HandleBracketChange.
        // Returns true if handled (caller should return immediately).
        // JS-021: no lock. JS-001: no throw.
        private bool TryHandleBracketDrag(Order order, CopyRule rule)
        {
            if (_diagnosticMode)
                NinjaTrader.Code.Output.Process(
                    "[TP2-DRAG] IsWorkingBracket="
                        + IsWorkingBracket(order)
                        + " name="
                        + (order.Name ?? "null")
                        + " state="
                        + order.OrderState,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            // DW-B168-02: track in-flight stop drags at ChangePending / clear at ChangeRejected.
            TryTrackDragInFlight(order);
            if (!IsWorkingBracket(order))
                return false;
            if (order.FromEntrySignal != null)
                PopulateOrderMap(order.FromEntrySignal, order.Account);
            HandleBracketChange(order, rule);
            return true;
        }

        // DW-B168-02: record or clear drag-in-flight suffix for leader stop orders.
        // ChangePending fires before any broker round-trip -- guarantees the set is
        // populated before any Accepted callback reads it (immune to NT8 batch-ack race).
        // Rejected: drag was rejected by broker -- remove to prevent permanent suppression.
        // NT8: OrderState.ChangeRejected does not exist in NT8 API; Rejected is the
        //   applicable terminal state for a change attempt that was refused by the broker.
        // Only tracks stop legs (not target legs) since _dragsInFlight guards stop resubmit.
        // CYC=5: base(1) + !IsStopLeg(1) + IsNullOrEmpty(1) + ChangePending(1) + Rejected(1).
        // JS-021: no lock -- single NT8 callback thread per account. JS-001: no throw. ASCII-only.
        private void TryTrackDragInFlight(Order order)
        {
            if (!IsStopLeg(order))
                return;
            string suffix = ParseLegSuffix(order.Name);
            if (string.IsNullOrEmpty(suffix))
                return;
            if (order.OrderState == OrderState.ChangePending)    // (1)
                _dragsInFlight.Add(suffix);
            else if (order.OrderState == OrderState.Rejected) // (2)
                _dragsInFlight.Remove(suffix);
        }

        // DW-B168-02: clear drag-in-flight suffix for a Cancelled stop order.
        // Called from TryCancelFollowerEntries when an ATM bracket stop (Stop1/2/3) is Cancelled.
        // Without this clear, a rejected/cancelled drag orphans the suffix in the set and
        // permanently suppresses all future collateral resubmits for that leg.
        // CYC=2: base(1) + if suffix empty(1). JS-021: no lock. JS-001: no throw. JS-002: void.
        // ASCII-only.
        private void TryRemoveDragSuffix(Order order)
        {
            string suffix = ParseLegSuffix(order.Name);
            if (!string.IsNullOrEmpty(suffix))    // (1)
                _dragsInFlight.Remove(suffix);
        }

        // B132 LaneB diagnostic. Set _diagnosticMode=false to disable. Remove when DW-B138 confirmed fixed.
        // CYC=4: (1) if-guard, (2) &&, (3) ||.
        // JS-021: no lock. JS-001: no throw. NT8 Output.Process is safe from any thread.
        private void TryLogDragTrace(Order order)
        {
            if (
                _diagnosticMode
                && (IsWorkingBracket(order) || order.OrderState == OrderState.ChangeSubmitted)
            )
                NinjaTrader.Code.Output.Process(
                    "[TP1-OOU] name="
                        + (order.Name ?? "null")
                        + " state="
                        + order.OrderState
                        + " signal="
                        + (order.FromEntrySignal ?? "null")
                        + " acct="
                        + (order.Account?.Name ?? "?"),
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
        }

        // B132 LaneB diagnostic. Set _diagnosticMode=false to disable. Remove when DW-B138 confirmed fixed.
        // CYC=2: (1) if-guard.
        // JS-021: no lock. acc.Orders.ToList() is NT8-safe on order-update thread.
        private void TryLogSFBTrace(Account acc, Order leaderOrder, bool isStop, Order? fo)
        {
            if (!_diagnosticMode)
                return;
            var ordList = acc.Orders.ToList();
            NinjaTrader.Code.Output.Process(
                "[TP4-SFB] acc="
                    + acc.Name
                    + " leaderName="
                    + (leaderOrder.Name ?? "null")
                    + " isStop="
                    + isStop
                    + " fo="
                    + (fo?.Name ?? "NULL")
                    + " followerOrders=["
                    + string.Join(",", ordList.Select(o => (o.Name ?? "?") + ":" + o.OrderState))
                    + "]",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
        }

        // TryHandleDrag: CYC=3. Combines bracket drag (Gate B) and entry drag (Gate C) into one dispatch.
        // Returns true if either gate consumed the event (caller should return).
        // Consolidates the two consecutive if-dispatch calls in OnOrderUpdate to one branch.
        // JS-021: no lock. JS-001: no throw. JS-002: returns bool.
        private bool TryHandleDrag(Order order, CopyRule rule)
        {
            if (TryHandleBracketDrag(order, rule))
                return true; // (1)
            if (TryHandleEntryDrag(order, rule))
                return true; // (2)
            return false;
        }

        // TryHandleEntryDrag: CYC=7. Gate C entry drag detection -- same orderId + new price = dragged.
        // Returns true if handled (caller should return immediately).
        // B62/B66-LaneC: accepts Limit and StopLimit. HOTFIX-B65-GATE-C-FILL-GUARD-01: Filled==0 guard.
        // DW-B64-01: re-inserts new price before HandleEntryChange removes old key.
        // JS-021: no lock. JS-001: no throw.
        // TB-T6: IsEntryDragEligible -- CCN<=4. Predicate for TryHandleEntryDrag early-return guards.
        // Absorbs: OrderType guard (Limit||StopLimit), OrderState guard (Accepted||Working), Filled!=0 guard.
        // HOTFIX-B65-GATE-C-FILL-GUARD-01: order.Filled != 0 check preserved verbatim.
        // JS-021: no lock. JS-001: no throw. Static -- no instance state required.
        // TESTABILITY: private static; testable via IsEntryDragEligibleTestable seam below.
        private static bool IsEntryDragEligible(Order order)
        {
            if (order.OrderType != OrderType.Limit && order.OrderType != OrderType.StopLimit)
                return false; // (1) type check with || = 2 Lizard
            if (order.OrderState != OrderState.Accepted && order.OrderState != OrderState.Working)
                return false; // (2) state check with || = 2 Lizard
            if (order.Filled != 0)
                return false; // HOTFIX-B65-GATE-C-FILL-GUARD-01 (3)
            return true;
        }

        // TB-T6: test seam for IsEntryDragEligible -- accepts primitives to avoid constructing NT8 Order in xUnit.
        // Mirrors IsEvictTriggerStateTestable pattern (L1636).
        internal static bool IsEntryDragEligibleTestable(
            OrderType orderType,
            OrderState orderState,
            int filled
        )
        {
            if (orderType != OrderType.Limit && orderType != OrderType.StopLimit)
                return false;
            if (orderState != OrderState.Accepted && orderState != OrderState.Working)
                return false;
            if (filled != 0)
                return false;
            return true;
        }

        private bool TryHandleEntryDrag(Order order, CopyRule rule)
        {
            if (!IsEntryDragEligible(order))
                return false;
            double currentPrice = GetOrderPrice(order);
            if (!_dedupCache.TryGetValue(order.OrderId.ToString(), out double storedPrice))
                return false;
            if (
                Math.Abs(currentPrice - storedPrice)
                < (order.Instrument?.MasterInstrument?.TickSize ?? 0.01)
            )
                return false;
            // DW-B64-01 fix: re-insert new price BEFORE HandleEntryChange removes old key.
            // HandleEntryChange calls TryRemove(orderId) -- without this line the second state
            // transition (Working after Accepted) sees no cache entry and falls through to
            // DispatchCopy, placing a duplicate PTT-Copy order on the follower.
            _dedupCache[order.OrderId.ToString()] = currentPrice;
            HandleEntryChange(order, rule);
            return true;
        }

        // --- B9 T3: Mirror mode methods ---

        // CYC=2 -- Filled state check + IsBracketLeg check (AND short-circuit = 2 decision points)
        // TESTABILITY: internal static with primitive parameters -- directly testable without NT8 runtime.
        internal static bool ShouldMirrorClose(OrderState state, bool isBracketLeg)
        {
            return state == OrderState.Filled && isBracketLeg;
        }

        // TryMirrorOrderUpdate: CYC=2. Fuses CopyMode.Mirror guard with MirrorOrderUpdate call.
        // Eliminates the mode-check branch from OnOrderUpdate parent CCN budget.
        // JS-001: no throw. JS-002: void. JS-021: no lock. ASCII-only.
        private void TryMirrorOrderUpdate(Order masterOrder, CopyRule rule)
        {
            if ((CopyMode)_copyModeValue != CopyMode.Mirror)
                return; // (1)
            MirrorOrderUpdate(masterOrder, rule); // (2 -- no branch)
        }

        // CYC=3 -- null guard + ShouldMirrorClose branch + IsWorkingBracket branch
        private void MirrorOrderUpdate(Order masterOrder, CopyRule rule)
        {
            if (masterOrder == null)
                return; // guard (1)
            bool isBracket = IsBracketLeg(masterOrder);
            if (ShouldMirrorClose(masterOrder.OrderState, isBracket)) // branch (2)
            {
                MirrorClose(masterOrder, rule);
                return;
            }
            if (IsWorkingBracket(masterOrder)) // branch (3)
                HandleBracketChange(masterOrder, rule); // reuse existing -- no duplication
        }

        // CYC=4 -- instr null guard + foreach loop + acc null guard + pos null/qty guard
        // JS-001: try/catch around CreateOrder -- no throw in hot path.
        // NT8 constraint: "PTT-Mirror-Close" signal name starts with "PTT-".
        private void MirrorClose(Order masterOrder, CopyRule rule)
        {
            var instr = masterOrder.Instrument;
            if (instr == null)
                return; // guard (1)
            foreach (var acc in rule.FollowerAccounts) // loop (2)
            {
                if (acc == null)
                    continue; // guard (3)
                var pos = FindPosition(acc, instr);
                if (pos == null || pos.Quantity == 0)
                    continue; // guard (4)
                var action =
                    pos.MarketPosition == MarketPosition.Long
                        ? OrderAction.Sell
                        : OrderAction.BuyToCover; // ternary: not a branch
                try
                {
                    acc.CreateOrder(
                        instr,
                        action,
                        OrderType.Market,
                        OrderEntry.Manual,
                        TimeInForce.Gtc, // B29 fix: Gtc matches ATM bracket TIF
                        pos.Quantity,
                        0,
                        0,
                        null,
                        "PTT-Mirror-Close", // signal name starts with "PTT-" (NT8 constraint)
                        DateTime.MaxValue,
                        null
                    );
                    StatusUpdate?.Invoke(acc.Name + ": mirror-close " + pos.Quantity);
                }
                catch (Exception ex)
                {
                    StatusUpdate?.Invoke("PTT-Mirror-Close error: " + ex.Message);
                }
            }
        }

        // B56 T1: IsDispatchTriggerState -- CYC=2. True for the ONE state that triggers follower placement.
        // HOTFIX-MARKET-DEDUP-01: Market orders must dispatch on Submitted ONLY.
        //   NT8/Rithmic changes OrderId from GUID (Submitted) to numeric (Accepted).
        //   Without type-awareness, both states pass the dedup cache with different keys -> double dispatch.
        // AddOn limit orders skip Submitted; arrive first as Accepted -- dispatch on Accepted (AddOn path).
        // ChartTrader limit orders skip Accepted entirely; Working is first event -- dispatch on Working (DW-B96).
        // _dedupCache in DispatchCopy (Gate 5) prevents double-dispatch for AddOn path:
        //   Accepted keys the orderId; subsequent Working event deduped by IsDedup -> early return.
        // JS-002: returns bool (not null). JS-021: no lock.
        // TESTABILITY: internal static with primitive params -- directly testable without NT8 runtime.
        internal static bool IsDispatchTriggerState(OrderState state, OrderType type) =>
            (type == OrderType.Market && state == OrderState.Submitted) // Market: GUID-keyed, Submitted only
            || (
                type == OrderType.Limit
                && (
                    state == OrderState.Accepted // AddOn path (unchanged)
                    || state == OrderState.Working
                )
            ); // ChartTrader path (DW-B96)

        // B59 T1: IsExitSignalName -- CYC=6. Returns true for names that must not trigger follower copy.
        // Covers: (1) PTT- own signals; (2) NT8 Close button; (3) NT8 Flatten; (4) NT8 Rev reversal;
        //         (5) NT8 "Exit..." prefix family; (6) NT8 ATM bracket Target1..Target9 (B78 DW-B78-01).
        // "Entry" is NOT blocked -- Gate 2 already limits dispatch to master account only.
        // Follower "Entry" orders (SendCopyWithAtm) never pass Gate 2, so no cascade is possible.
        // Stop1..Stop9 are StopMarket type -- already blocked by Gate 4 before reaching this check.
        // Only Target1..Target9 (Limit type) need explicit filtering here.
        // JS-001: no throw. JS-002: returns bool.
        // TESTABILITY: internal static with string param -- directly testable without NT8 runtime.
        // TB-T6: IsAtmTargetSignalName -- CCN<=3. Extracts the ATM bracket Target1..Target9 compound check.
        // Absorbs: name.Length > 6 + name.StartsWith("Target") + char.IsDigit(name[6]) = 3 Lizard branches.
        // B78 DW-B78-01: ATM profit-target brackets must not trigger follower copy.
        // JS-001: no throw. Static -- pure string predicate. ASCII-only.
        internal static bool IsAtmTargetSignalName(string name)
        {
            if (name == null || name.Length <= 6)
                return false; // (1) length guard
            if (!name.StartsWith("Target", StringComparison.Ordinal))
                return false; // (2)
            return char.IsDigit(name[6]); // (3)
        }

        internal static bool IsExitSignalName(string name)
        {
            if (name == null)
                return false;
            if (name.StartsWith("PTT-", StringComparison.Ordinal))
                return true; // (1)
            if (name == "Close")
                return true; // (2)
            if (name == "Flatten")
                return true; // (3)
            if (name.StartsWith("Rev", StringComparison.Ordinal))
                return true; // (4)
            if (name.StartsWith("Exit", StringComparison.Ordinal))
                return true; // (5)
            // B78 DW-B78-01: ATM profit-target brackets (Target1..Target9) must not trigger follower copy.
            // Pattern: "Target" prefix + digit at index 6. TB-T6: delegated to IsAtmTargetSignalName.
            if (IsAtmTargetSignalName(name))
                return true; // (6)
            // NOTE: "Entry" is intentionally NOT blocked here.
            // Gate 2 already filters to master account only -- follower "Entry" orders never reach DispatchCopy.
            return false;
        }

        // B180-2: backward-compat test seam stub for IsExitSignalNameOrAnonClose (removed from DispatchCopy).
        // AnonClose logic removed -- B59 tests already skip with NT8-runtime; this stub keeps them compilable.
        // CYC=2. JS-001: no throw. JS-002: returns bool.
        internal static bool IsExitSignalNameOrAnonClose(string name, NinjaTrader.Cbi.OrderType orderType)
        {
            if (name != null && name.Length == 0)
                return orderType != NinjaTrader.Cbi.OrderType.Limit; // (1)+(2)
            return IsExitSignalName(name); // (3)
        }

        // B65 T1: IsNativeExitName -- CYC=6. Returns true for NT8 built-in exit order names ONLY.
        // Distinct from IsExitSignalName: does NOT cover PTT- prefixed signals.
        // Rationale: Only native NT8 exits (Close/Flatten/Rev/Exit) can arrive in OnOrderUpdate
        // while the leader position has not yet updated -- see NT8_FULL_REFERENCE.md line 1721:
        //   "Changes to positions will not be reflected till at least the next OnBarUpdate() event
        //    after an order fill."
        // For these names, bypass the hasOpenPosition guard in TryDispatchLeaderFlat to avoid
        // the position-race and propagate the close immediately to followers (DW-B65-01 fix).
        // NT8-VERIFY-03/04: "IsNativeExitName" confirmed NOT present in NT8 Custom codebase.
        // JS-001: no throw. JS-002: returns bool (never null). JS-021: no lock.
        internal static bool IsNativeExitName(string name)
        {
            if (name == null)
                return false;
            if (name == "Close")
                return true;
            if (name == "Flatten")
                return true;
            if (name.StartsWith("Rev", StringComparison.Ordinal))
                return true;
            if (name.StartsWith("Exit", StringComparison.Ordinal))
                return true;
            if (name.StartsWith("SimStop", StringComparison.Ordinal))
                return true;
            return false;
        }

        // IsNonFlatDispatchName: CYC=3. Returns true when orderName must NOT trigger follower flatten.
        // Combines HOTFIX-B63-FLATTEN-01 (PTT- prefix), HOTFIX-B64-ENTRY-FLATTEN-01 ("Entry"),
        // and DW-B94 (ATM bracket names Stop1..Stop9 / Target1..Target9).
        // ATM bracket cancel events arrive during NT8 position update gap (NT8_FULL_REFERENCE line 1721)
        // and must never trigger a follower flatten -- the position is still live.
        // JS-001: no throw. JS-002: returns bool. JS-021: no lock. ASCII-only.
        internal static bool IsNonFlatDispatchName(string orderName)
        {
            if (orderName != null && orderName.StartsWith("PTT-", StringComparison.Ordinal))
                return true; // (1)
            if (orderName == "Entry")
                return true; // (2)
            if (IsAtmBracketName(orderName))
                return true; // (3) DW-B94: Stop1..Stop9 / Target1..Target9 -- ATM cancel must not flatten followers
            return false;
        }

        // --- B7-F0: Bracket mirroring methods ---

        // B8 T1: DispatchCopy -- index-tracking loop replaces plain foreach.
        // TB-T4: extracted ShouldSkipFollowerDispatch, ShouldSkipForReversalGuard, DispatchToFollower.
        // B212-T3-R1: extracted IsSuppressedReentry + IsDispatchableSignal to hold CYC<=8.
        // CYC=8: (1)IsExitSignalName (2)IsSuppressedReentry (3)IsDispatchableSignal (4)IsLiveEntryBlocked
        //        (5)foreach (6)ShouldSkipFollowerDispatch (7)ShouldSkipForReversalGuard. Base=1.
        // JS-001: no throw in hot path. JS-021: no lock.
        private void DispatchCopy(Order order, CopyRule rule)
        {
            // Gate 0.5: block PTT- cascade AND known NT8 exit signal names (B59).
            if (IsExitSignalName(order.Name))
                return;

            // Gate B212-REENTRY: suppress copy during BE-swap cancel window and flatten close window.
            // Both B212 conditions extracted to IsSuppressedReentry to hold DispatchCopy CYC<=8. (B212-T3-R1)
            if (IsSuppressedReentry(order))
                return;

            // Gates 3+4: dispatch-trigger state AND dispatchable order type (combined to hold CYC<=8).
            // B212-T3-R1: IsDispatchableSignal merges the two formerly-separate guard calls.
            if (!IsDispatchableSignal(order.OrderState, order.OrderType))
                return;

            // Gate 5: dedup -- reject duplicate event for same orderId (B62: price-keyed dedup).
            // DW-B91-A: IsEntryDispatched extends dedup across EvictDedup eviction boundary.
            // DW-B142-MGC-02: IsLiveEntryBlocked adds instrument-level guard -- blocks cancel+resubmit dups.
            var orderId = order.OrderId.ToString();
            var instrKey = order.Instrument.FullName + "|" + order.OrderAction;
            if (IsLiveEntryBlocked(instrKey, orderId, order.LimitPrice)) // DW-B142-MGC-02
                return;

            // B212-REENTRY-BETARGET-V2: clear BE-target fill window NOW (all gates passed = legitimate entry).
            // TryRemove is lock-free (ConcurrentDictionary). No-op if key absent.
            // Moved here from TryFirePositionState where it fired too early.
            _filledBeTargetCount.TryGetValue(order.Account?.Name ?? string.Empty, out int beCountPre);
            NinjaTrader.Code.Output.Process(
                "[BETARGET-DISPATCH] acct=" + (order.Account?.Name ?? "null")
                    + " name=" + (order.Name ?? "null")
                    + " state=" + order.OrderState
                    + " beCountBefore=" + beCountPre,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            bool cleared = _filledBeTargetCount.TryRemove(order.Account?.Name ?? string.Empty, out _);
            NinjaTrader.Code.Output.Process(
                "[BETARGET-CLEAR] acct=" + (order.Account?.Name ?? "null")
                    + " name=" + (order.Name ?? "null")
                    + " cleared=" + cleared,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);

            // All gates passed -- build base signal
            var baseSignal = CopySignal.Create(
                order.OrderAction,
                order.OrderType,
                order.Quantity,
                order.LimitPrice,
                orderId
            );

            int baseQty = baseSignal.Quantity;

            // Lane C: Lane D wiring point. Unconditional void call -- 0 CYC cost in parent. PTT-BUGS-LIVE-0911.
            TryLaneDHook(order, rule, baseSignal, baseQty);

            // B119: DW-B128 -- snapshot instrument and last direction once before the loop.
            // TryGetValue is O(1) and allocation-free on ConcurrentDictionary.
            OrderAction currentAction = order.OrderAction;
            var instr = order.Instrument;
            bool hasLastDirection = TryGetLastDirection(instr.FullName, out OrderAction lastAction);

            // B8 T1: index-tracking loop applies per-follower multiplier
            int dispatched = 0;
            int idx = 0;
            foreach (var acc in rule.FollowerAccounts)
            {
                if (ShouldSkipFollowerDispatch(acc, instr))
                {
                    idx++;
                    continue;
                }

                if (
                    ShouldSkipForReversalGuard(
                        acc,
                        instr,
                        currentAction,
                        lastAction,
                        hasLastDirection
                    )
                )
                {
                    idx++;
                    continue;
                }

                DispatchToFollower(acc, order, rule, idx, baseSignal, baseQty);
                idx++;
                dispatched++;
            }
            // Gate-5 commit already performed by IsLiveEntryBlocked (HEAD pattern -- no separate SetLiveEntryDispatched needed).

            // B119: DW-B128 -- record direction dispatched for this instrument.
            // Write happens AFTER the loop so all followers in this dispatch see the same lastAction.
            RecordLastDirection(instr.FullName, currentAction);
        }

        // CYC=1 (straight-line expression body). Lock-free: ConcurrentDictionary.TryGetValue is atomic.
        // JS-021: no lock. JS-001: no throw. JS-002: returns bool (never null). JS-081: ASCII-only.
        private bool TryGetLastDirection(string instrFullName, out OrderAction lastAction)
            => _lastLeaderDirection.TryGetValue(instrFullName, out lastAction);

        // CYC=1 (straight-line expression body). Lock-free: ConcurrentDictionary indexer write is atomic.
        // JS-021: no lock. JS-001: no throw. JS-081: ASCII-only.
        private void RecordLastDirection(string instrFullName, OrderAction action)
            => _lastLeaderDirection[instrFullName] = action;


        // Lane D: ATM dispatch hook. Called once per confirmed leader entry signal after all 5
        // DispatchCopy gates. Handles Clone-mode Named followers only; all other modes are no-ops.
        // CYC=6: (1) mode!=Clone, (2) type!=Limit, (3) !Named, (4) dispatched in loop, (5) laneDCount>0.
        // JS-001: no throw (early returns + SendCopyWithAtm has its own try/catch). JS-021: no lock.
        // JS-023: no UI update here -- StatusUpdate raised inside SendCopyWithAtm (existing pattern).
        // ASCII-only. Thread: NT8 account background thread.
        // CYC=7: (1) mode, (2) type, (3) atm resolve, (4) reversal snapshot, (5) foreach, (6) reversal guard skip, (7) dispatch.
        // B200-CLOSE-LANED-01-V5: added reversal guard snapshot + per-follower ShouldSkipForReversalGuard call.
        // Mirrors DispatchCopy loop (L2762-2796) -- both lanes now share identical reversal guard logic.
        private void TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)
        {
            // Branch (1): non-Clone mode -> no-op for Signal/Mirror/Market
            if (GetCopyMode() != CopyMode.Clone)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-LANED-EXIT1] mode=" + GetCopyMode() + " name=" + order.Name + " state=" + order.OrderState,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return;
            }

            // Branch (2): Market leader order -> skip ATM dispatch (SendCopyWithAtm hardcodes Limit;
            // Limit @ price 0 is invalid). Market followers fall through to DispatchToFollower SendCopy.
            if (order.OrderType != OrderType.Limit)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-LANED-EXIT2] type=" + order.OrderType + " name=" + order.Name + " state=" + order.OrderState,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return;
            }

            // Branch (3): resolve ATM mode; Inherit returned when _cloneAtmCache is empty -> no-op
            FollowerAtmMode cloneAtmMode = ResolveAtmMode(rule, order.Instrument.FullName);
            if (!(cloneAtmMode is FollowerAtmMode.Named namedMode))
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-LANED-EXIT3] atmMode=" + cloneAtmMode.GetType().Name + " cache='" + _cloneAtmCache + "' obj=" + (_cloneAtmObject != null ? "set" : "null"),
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return;
            }

            // Branch (4): B200-CLOSE-LANED-01-V5: snapshot reversal-guard inputs once before fan-out loop.
            // Mirrors DispatchCopy L2762-2766. TryGetLastDirection is O(1), allocation-free.
            OrderAction currentAction = order.OrderAction;
            bool hasLastDir = TryGetLastDirection(order.Instrument.FullName, out OrderAction lastAction);

            // Fan-out: dispatch each follower via ATM
            int laneDCount = 0;
            int idx = 0;
            foreach (Account acc in rule.FollowerAccounts)
            {
                // Branch (5): B200-CLOSE-LANED-01-V5: reversal guard mirrors DispatchCopy L2779-2791.
                // Without this, Lane D fires PTT-ATM-SEND before ShouldSkipForReversalGuard in
                // DispatchToFollower can block it -- reverse ATM is placed, then regular copy skipped.
                if (ShouldSkipForReversalGuard(acc, order.Instrument, currentAction, lastAction, hasLastDir))
                {
                    idx++;
                    continue;
                }

                // Branch (6): dispatch; track count
                if (TryLaneDDispatchFollower(acc, rule, idx, order, namedMode, baseSignal, baseQty))
                    laneDCount++;
                idx++;
            }

            // Gate-5 dedup already committed by IsLiveEntryBlocked in DispatchCopy (HEAD pattern).
        }

        // Lane D: per-follower ATM dispatch helper. Keeps TryLaneDHook CYC low.
        // CYC=6: (A) null acc guard, (B) daily cap check, (B.5) flatten in-flight guard,
        //        (B.6) leader active-close guard, (C) SendCopyWithAtm result.
        // B200-CLOSE-LANED-01-V6: (B.5) upgraded from HasRecentFlatten to HasActivePttFlatten (deterministic).
        // B207-FIX-A: (B.6) added leader active-close guard -- blocks Lane D while leader has a Close/Flatten
        //             order in-flight (Submitted/Accepted/Working/PartFilled). Root cause: NT8 ATM re-arms a
        //             new Entry while the leader's Close is still PartFilled; Lane D fires before
        //             TryDispatchLeaderFlat can set _flattenInFlight on followers.
        // JS-001: no throw -- returns false on all failure paths; SendCopyWithAtm has its own try/catch.
        // JS-021: no lock -- ConcurrentDictionary.TryAdd is atomic. JS-002: returns bool (never null).
        // ASCII-only. Thread: NT8 account background thread (same as TryLaneDHook caller).
        private bool TryLaneDDispatchFollower(
            Account acc,
            CopyRule rule,
            int idx,
            Order order,
            FollowerAtmMode.Named namedMode,
            CopySignal baseSignal,
            int baseQty
        )
        {
            // Branch (A): null guard
            if (acc == null) return false;

            // Branch (B): daily position cap -- respect existing cap contract
            if (!PassesDailyCapCheck(acc)) return false;

            // Branch (B.5): B200-CLOSE-LANED-01-V6: block Lane D while PTT-Flatten is actively in-flight.
            // Prevents new ATM entry being copied to a follower that is still mid-flatten.
            // Upgraded from HasRecentFlatten (timestamp) to HasActivePttFlatten (order-state) -- deterministic.
            // HasActivePttFlatten returns true when PTT-Flatten is Submitted/Accepted/Working/PartFilled.
            if (IsFlattenInFlightOrRecent(acc, order.Instrument)) return false;

            // Branch (B.6): B207-FIX-A: block Lane D while the leader has an active Close/Flatten order.
            // Race: NT8 ATM re-arms a new Entry while leader Close is still PartFilled. TryDispatchLeaderFlat
            // has not run yet (requires Filled/Cancelled), so _flattenInFlight is false on followers.
            // IsFlattenInFlightOrRecent above cannot catch this window -- it checks follower state only.
            // Fix: scan leader orders for any active Close or Flatten order. If found, this new Entry is the
            // ATM reversal leg of the close sequence -- not a genuine new signal. Block dispatch until flat.
            // HasLeaderActiveClose is CYC=3: foreach(1) + IsNativeExitName(1) + state-check(1). JS-021: lock-free.
            if (HasLeaderActiveClose(rule.MasterAccount, order.Instrument)) return false;

            int mult = GetMultiplier(rule, idx);
            CopySignal scaledSig = CopySignal.Create(
                baseSignal.Action,
                baseSignal.Type,   // always Limit (TryLaneDHook guards order.OrderType above)
                baseQty * mult,
                baseSignal.LimitPrice,
                baseSignal.OrderId
            );

            // Branch (C): dispatch ATM entry; mark dedup only on success
            bool ok = (SendCopyWithAtm(acc, order.Instrument, in scaledSig, namedMode) != null); // T3: Order? return
            if (ok)
            {
                // Key: orderId + "#" + accName. Separator "#" is ASCII and cannot appear in NT8 account names.
                _laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0);
                // Branch (D): B185 DW-B183-04: evict when guard grows beyond session threshold.
                // Clear() is atomic on ConcurrentDictionary (JS-021 compliant).
                if (_laneDDispatched.Count > LaneDEvictThreshold)
                    _laneDDispatched.Clear();
                // B200-SFB-NULL-01 FIX: open slate-ready gate for direct ATM path.
                // Without this, _followerSlateReady stays false after TryMarkFollowerSlateNotReady,
                // FindFollowerBracketOrder returns null (gate closed), bracket sync skipped -> PTT-Flatten fires.
                // Mirrors TrySetSlateReadyOnDrainFill (drain path) and TryLaneEHook (Lane E path).
                // JS-021: ConcurrentDictionary indexer write is atomic -- no lock needed.
                _followerSlateReady[acc.Name] = true;
                // B204: new entry dispatched -- clear flatten-latch so Lane E is eligible for this trade cycle.
                _laneEFlattenBlocked.TryRemove(acc.Name + "#LANE-E#" + order.Instrument.FullName, out _);
            }
            return ok;
        }

        // Lane E: eligibility predicate for TryLaneEHook. Absorbs all compound boolean guards so
        // TryLaneEHook contains zero || or && operators (JS-013 compliance).
        // B212-REENTRY-FOLLOWER-V2: extracted compound branches to helpers; added Fix 3 (5b) gate.
        // CYC history: was 11 (3 compound ||/&& + 8 simple). Extracted 4 branches -> 7. Gate (5b) -> 8.
        // CYC=8: (0)latch(1) + (1)mode(1) + (2)null(1) + (3)qty(1) + (4)slate(1) + (5)atm(1) + (5b)beCount(1) + (6+7)flattenLeader(1).
        // JS-001: no throw. JS-021: no lock. JS-002: returns bool, never null.
        // ASCII-only. Thread: NT8 account background thread (same as TryLaneEHook).
        // Each helper replaces a compound branch (saves 1 CYC per JS-013 compound rule).

        // Extracted from gate (0): flatten-latch compound && check.
        // CYC=2: (1) pos?.Instrument null guard, (2) dict lookup. JS-021: lock-free. JS-001: no throw.
        private bool IsLaneEFlattenLatched(Account acc, NinjaTrader.Cbi.Position pos)
            => pos?.Instrument != null
            && _laneEFlattenBlocked.ContainsKey(acc.Name + "#LANE-E#" + pos.Instrument.FullName);

        // Extracted from gate (2): null guard || compound check.
        // CYC=2: (1) pos null, (2) Instrument null. JS-021: lock-free. JS-001: no throw.
        private static bool IsPositionOrInstrumentNull(NinjaTrader.Cbi.Position pos)
            => pos == null || pos.Instrument == null;

        // Extracted from gate (4): follower slate || compound check.
        // Absent key treated as ready (TryGetValue out=false, ready=false -> !false=true -> gate blocks). Correct.
        // CYC=2: (1) TryGetValue miss, (2) ready==true. JS-021: lock-free. JS-001: no throw.
        private bool IsFollowerSlateNotClosed(Account acc)
        {
            bool ready;
            return !_followerSlateReady.TryGetValue(acc.Name, out ready) || ready;
        }

        // Extracted from gates (6)+(7): flatten-in-flight and leader-position grouped block.
        // Saves 1 net CYC (2 simple branches -> 1 call). JS-021: lock-free. JS-001: no throw.
        private bool IsLaneEBlockedByFlattenOrLeader(Account acc, NinjaTrader.Cbi.Position pos)
            => IsFlattenInFlightOrRecent(acc, pos.Instrument) || !HasLeaderPositionForFollower(acc, pos.Instrument);

        // B212-REENTRY-FOLLOWER-V2: IsLaneEEligible after CYC extraction + Fix 3 gate.
        // CYC=8: (0) latch check, (1) mode check, (2) null check, (3) qty check,
        //        (4) slate check, (5) ATM mode check, (5b) BE-target fill check, (6+7) flatten+leader check.
        // All compound branches extracted to helpers above. Each call site = +1 CYC (no compound).
        // JS-021: no lock. JS-001: no throw. JS-002: returns bool. ASCII-only.
        private bool IsLaneEEligible(Account acc, NinjaTrader.Cbi.Position pos)
        {
            // (0): B204-LANE-E-FLATTEN-PERMANENT-LATCH-01: flatten = exit. No re-entry via Lane E after flatten.
            // Latch set in FlattenOneAccount. Cleared only on new ATM entry dispatch or SetEnabled(true).
            if (IsLaneEFlattenLatched(acc, pos))
                return false;

            // (1): non-Clone mode -> not eligible (Signal/Mirror/Market no-op)
            if (GetCopyMode() != CopyMode.Clone)
                return false;

            // (2): null guard -- extracted to IsPositionOrInstrumentNull (compound || saved 1 CYC)
            if (IsPositionOrInstrumentNull(pos))
                return false;

            // (3): no remaining position -> not a re-arm event (flat or zero-qty update)
            if (pos.Quantity <= 0)
                return false;

            // (4): gate must be closed (false) -- extracted to IsFollowerSlateNotClosed (compound || saved 1 CYC)
            // Absent key (first session entry) treated as ready -> returns false -> no-op. Correct.
            if (IsFollowerSlateNotClosed(acc))
                return false;

            // (5): ATM template must be Named -- Inherit/unconfigured = no-op
            if (!(ResolveAtmMode(default(CopyRule), pos.Instrument.FullName) is FollowerAtmMode.Named))
                return false;

            // (5b): B212-REENTRY-FOLLOWER-V2: mirror of IsSuppressedReentry for Lane E path.
            // _filledBeTargetCount is set for follower accounts by TryRecordBeTargetFill.
            // Cleared in TryEvictFollowerBeSlot when follower goes flat (L1915).
            // Blocks Lane E re-arm when follower's BE-targets have already filled (post BE-All).
            // Without this gate, Lane E re-arms follower ATM even though trade is done via BE-targets.
            // HasFilledBeTargetFast: lock-free ConcurrentDictionary.TryGetValue. CYC +1.
            // JS-021: no lock. JS-001: no throw. ASCII-only.
            if (HasFilledBeTargetFast(acc))
                return false;

            // (6)+(7): flatten-in-flight and leader-position checks -- extracted to IsLaneEBlockedByFlattenOrLeader
            // (6): B200-CLOSE-LANED-01-V6: block while PTT-Flatten is actively in-flight (order-state-based).
            // (7): B203-LANE-E-NO-LEADER-POS-01: leader must have an open position.
            // Two simple branches grouped into one call: saves 1 net CYC.
            if (IsLaneEBlockedByFlattenOrLeader(acc, pos))
                return false;

            return true;
        }

        // B200-CLOSE-LANED-01-V4: flatten debounce predicate shared by Lane D and Lane E.
        // Returns true if a PTT-Flatten was submitted within the last 2000ms for this account.
        // 2000ms >> worst-case flatten round-trip in SIM (~100ms observed). Safe for live.
        // _flattenFilledTicks is written in FlattenOneAccount at submission time (not fill time).
        // Covers full lifecycle: Submitted -> Accepted -> Working -> PartFilled -> Filled.
        // CYC=1: single expression, no branch (bool expression only). JS-021: GetOrAdd is lock-free.
        // JS-001: no throw. JS-002: returns bool (never null). ASCII-only.
        private bool HasRecentFlatten(Account acc) =>
            (long)(int)Environment.TickCount - _flattenFilledTicks.GetOrAdd(acc.Name, 0L) < 2000L;

        // B200-CLOSE-LANED-01-V12: three-signal flatten guard.
        // Signal 1 -- _flattenInFlight flag: set at FlattenOneAccount entry, cleared in OOU on PTT-Flatten Filled/Cancelled.
        //   V12 fix: OOU clears flag only when e.Order.OrderId matches _flattenInFlightOrderId anchor.
        //   Prevents stale Filled/Cancelled events from a prior cycle prematurely clearing the flag.
        // Signal 2 -- HasActivePttFlatten: order-state check (Submitted/Accepted/Working/PartFilled).
        //   Belt-and-suspenders for flag miss (e.g. SetEnabled(false) path that bypasses FlattenOneAccount).
        // Signal 3 -- HasRecentFlatten: 2000ms timestamp from submission tick.
        //   Belt-and-suspenders bridge for the flag-clear-to-dispatch race window.
        // Three-signal OR: blocks if ANY signal is true. Zero false-negatives.
        // CYC=1: single expression chain. JS-021: lock-free reads. JS-001: no throw. ASCII-only.
        private bool IsFlattenInFlightOrRecent(Account acc, Instrument instr)
        {
            if (_flattenInFlight.TryGetValue(acc.Name, out bool inFlight) && inFlight) return true; // (1) intent flag
            return HasActivePttFlatten(acc, instr) || HasRecentFlatten(acc);                         // (2)(3) belt-and-suspenders
        }

        // B208-FIX-B / B208-FIX-F-B: set/clear BE-swap-in-progress per account name.
        // Set path also stamps _beSwapSetTicks for 3-second safety timeout in finally.
        // CYC=2 (set path): flag write + conditional ticks write.
        // CYC=1 (clear path): flag write.
        // JS-021: no lock. JS-001: no throw. JS-002: void/bool. ASCII-only.
        internal void SetBeSwapInProgress(string accName, bool value)
        {
            if (accName == null) return;
            _beSwapInProgress[accName] = value;
            // B208-FIX-F-B: stamp set-time when activating so finally can timeout-clear safely.
            if (value)
                _beSwapSetTicks[accName] = (long)(int)Environment.TickCount;
        }

        // B208-FIX-B: returns true if a BE swap is currently in-progress for this account.
        // Called from NakedPositionDetector to suppress flatten during cancel window.
        // CYC=1: single TryGetValue. JS-021: lock-free. JS-001: no throw. JS-002: returns bool.
        internal bool IsBeSwapInProgress(string accName)
        {
            return accName != null
                && _beSwapInProgress.TryGetValue(accName, out bool val)
                && val;
        }

        // B208-FIX-F-B: returns the TickCount (ms) when _beSwapInProgress was last set true.
        // Used by PttBreakEvenSwap.Execute finally for 3-second safety timeout.
        // Returns 0 if no timestamp recorded (GetOrAdd default). CYC=1. JS-021: lock-free. JS-002: long.
        internal long GetBeSwapSetTicks(string accName)
        {
            if (accName == null) return 0L;
            return _beSwapSetTicks.GetOrAdd(accName, 0L);
        }

        // B208-FIX-G-1: clear on Working (normal: stop live), Cancelled (OCO: target filled),
        // Rejected (bad price: retry handles), Accepted (SIM belt+suspenders: mirrors FIX-F-A).
        // All four are terminal or confirmed-live outcomes for PTT-BE-Stop-N.
        // Permanent lock eliminated: flag cannot leak across trade cycles.
        // CYC=4: (1) name check, (2a-c) three-state isClearState, (3) IsBeSwapInProgress.
        // B212-DW04: Accepted branch (2d) REMOVED. Accepted fires before follower drain cancel-acks
        //   arrive (ChangeSubmitted window). Clearing the flag at Accepted lets SubmitDrainedEntry
        //   pass IsBeSwapInProgress=false and dispatch a spurious PTT-ATM-SEND qty=9. Fallback:
        //   PttBeSwapAll.Execute finally clears flag after 3s if Working never fires (SIM safety net).
        //   CYC delta: -1 (5->4). JS-028 OK (still <=8).
        // JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only.
        private void TryClearBeSwapOnStopWorking(OrderEventArgs e)
        {
            if (e.Order.Name == null
                || !e.Order.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)) // (1)
                return;
            bool isClearState = e.Order.OrderState == OrderState.Working    // (2a) normal: stop live
                || e.Order.OrderState == OrderState.Cancelled               // (2b) OCO: target filled
                || e.Order.OrderState == OrderState.Rejected;               // (2c) bad price: evict+retry
                // (2d) Accepted REMOVED -- B212-DW04: clears flag before drain acks arrive.
                // Fallback: PttBeSwapAll.Execute finally clears after 3s timeout.
            if (!isClearState)
                return;
            if (!IsBeSwapInProgress(e.Order.Account?.Name)) // (3)
                return;
            SetBeSwapInProgress(e.Order.Account.Name, false);
        }

        // B209: testable seam for TryClearBeSwapOnStopWorking state-check logic.
        // Extracts isClearState predicate so xUnit can verify all four states without NT8 OrderEventArgs.
        // B212-DW04: Accepted branch removed -- see TryClearBeSwapOnStopWorking comment above.
        // CYC=1 (expression body). JS-021: no lock. JS-001: no throw. ASCII-only.
        internal static bool IsBeClearStateTrigger(string orderName, OrderState state)
        {
            if (orderName == null || !orderName.StartsWith("PTT-BE-Stop", StringComparison.Ordinal))
                return false;
            return state == OrderState.Working
                || state == OrderState.Cancelled
                || state == OrderState.Rejected;
            // Accepted REMOVED -- B212-DW04: clears flag too early (before drain cancel-acks arrive).
        }


        // B209: testable seam for FIX-G-2 guard in TryCancelSingleStaleGtcExit.
        // Extracts the BE-swap skip predicate for xUnit testing.
        // CYC=1 (expression body). JS-021: no lock. JS-001: no throw. ASCII-only.
        internal static bool IsBeSwapGtcSweepGuard(string orderName, bool beSwapInProgress)
            => orderName != null
                && orderName.StartsWith("PTT-BE-", StringComparison.Ordinal)
                && beSwapInProgress;


        // B200-CLOSE-LANED-01: returns true if account has a PTT-Flatten order in an active (in-flight) state.
        // Mirrors the inline guard in FlattenOneAccount (L5534) -- extracted here for reuse by IsLaneEEligible.
        // B200-CLOSE-LANED-01-V2 FIX: added PartFilled -- PTT-Flatten partial fill is still in-flight.
        // When PTT-Flatten partially fills, NT8 transitions Working->PartFilled. Position update fires.
        // Without PartFilled here, HasActivePttFlatten returns false during PartFilled window -> Lane E fires.
        // CYC=4: foreach(1) + name check(1) + state check(2). JS-021: no lock. JS-001: no throw. ASCII-only.
        private static bool HasActivePttFlatten(Account acc, Instrument instr)
        {
            foreach (var o in acc.Orders.ToList()) // (1)
            {
                if (o.Name != "PTT-Flatten") continue; // (1)
                if (o.Instrument?.FullName != instr?.FullName) continue;
                if (o.OrderState == OrderState.Submitted  // (1)
                    || o.OrderState == OrderState.Accepted
                    || o.OrderState == OrderState.Working
                    || o.OrderState == OrderState.PartFilled) // (2) FIX: partial fill still in-flight
                    return true;
            }
            return false;
        }

        // B207-FIX-A: returns true if the leader account has an active Close or Flatten order in-flight
        // (Submitted/Accepted/Working/PartFilled) on the given instrument.
        // Purpose: detect the NT8 ATM re-arm window where a new Entry appears while the leader's Close
        // is still PartFilled -- the new Entry is the reversal leg, not a genuine new trade signal.
        // Called from TryLaneDDispatchFollower Branch (B.6) to block follower ATM dispatch during the window.
        // CYC=3: foreach(1) + IsNativeExitName(1) + state-check(1). JS-021: acc.Orders.ToList() lock-free.
        // JS-001: no throw. JS-002: returns bool, never null. ASCII-only.
        private static bool HasLeaderActiveClose(Account leader, Instrument instr)
        {
            if (leader == null || instr == null)
                return false;
            foreach (var o in leader.Orders.ToList()) // (1)
            {
                if (!IsNativeExitName(o.Name)) continue;  // (1)
                if (o.Instrument?.FullName != instr.FullName) continue;
                if (o.OrderState == OrderState.Submitted   // (1)
                    || o.OrderState == OrderState.Accepted
                    || o.OrderState == OrderState.Working
                    || o.OrderState == OrderState.PartFilled)
                    return true;
            }
            return false;
        }

        // B203-LANE-E-NO-LEADER-POS-01: returns true if the master account for this follower has an open
        // position on the given instrument. Lane E eligibility requires the leader to still be in a trade.
        // Iterates _rules to find the matching CopyRule, then calls FindPosition on the MasterAccount.
        // Fallback: if no rule found (follower not in any rule), returns false (no-op, safe).
        // CYC=4: foreach(1) + for-i(2) + name-match(3) + FindPosition-null-check(4).
        // JS-021: no lock -- _rules ConcurrentBag + acc.Positions read-only enumeration (same as FindPosition).
        // JS-001: no throw. JS-002: returns bool, never null. ASCII-only.
        private bool HasLeaderPositionForFollower(Account followerAcc, Instrument instr)
        {
            foreach (var rule in _rules)
                for (int i = 0; i < rule.FollowerAccounts.Length; i++)
                {
                    var f = rule.FollowerAccounts[i];
                    bool nameMatch =
                        (f != null && f.Name == followerAcc.Name)                           // (3a)
                        || (f == null                                                        // (3b) null-slot fallback
                            && rule.FollowerAccountNames != null
                            && i < rule.FollowerAccountNames.Length
                            && rule.FollowerAccountNames[i] == followerAcc.Name);
                    if (!nameMatch) continue;
                    var leaderPos = FindPosition(rule.MasterAccount, instr);                 // (4)
                    return leaderPos != null && leaderPos.Quantity > 0;
                }
            return false; // no rule matched -- not a valid follower for this instrument
        }

        // Lane E: re-arm ATM hook. Called from OnFollowerPositionUpdate after TrySetFollowerSlateReady.
        // Fires when follower has a remaining position (post-partial QX/BE) with gate closed and no ATM brackets.
        // CYC=4: (1)IsBeSwapInProgress guard,(2)IsLaneEEligible gate,(3)dedup check,(4)ok gate-open.
        //        All compound guards in IsLaneEEligible. CYC delta: +1 (3->4) for DW-B212-05.
        // B212-DW05: BE swap cancel sweep sets _followerSlateReady=false (via CancelQxBrackets ->
        //   TryMarkFollowerSlateNotReady). Position update fires during the cancel window. Without guard (1),
        //   Lane E dispatches SendCopyWithAtm spuriously (qty=pos.Quantity from partial-fill position).
        //   Guard: abort if BE swap is in progress for this follower account.
        // JS-001: no throw (early returns + SendCopyWithAtm has its own try/catch). JS-021: no lock.
        //   IsBeSwapInProgress is lock-free (ConcurrentDictionary.TryGetValue). JS-028: CYC=4 (<=8 ceiling).
        // JS-023: no UI update -- StatusUpdate raised inside SendCopyWithAtm (existing pattern).
        // ASCII-only. Thread: NT8 account background thread (same as OnFollowerPositionUpdate).
        private void TryLaneEHook(Account acc, NinjaTrader.Cbi.Position pos)
        {
            // Branch (1): B212-DW05: abort Lane E re-arm during active BE swap for this account.
            // CancelQxBrackets (called by PttBeSwapAll) sets _followerSlateReady=false, which makes
            // IsLaneEEligible pass check (4). Without this guard, the next position update fires
            // SendCopyWithAtm spuriously while brackets are being cancelled and re-placed.
            if (IsBeSwapInProgress(acc?.Name))
                return;

            // Branch (2): eligibility gate -- all compound guards delegated to IsLaneEEligible
            if (!IsLaneEEligible(acc, pos))
                return;

            // Resolve ATM template and dedup key (assignments only, +0 CYC)
            string instrFullName = pos.Instrument.FullName;
            FollowerAtmMode.Named namedMode =
                ResolveAtmMode(default(CopyRule), instrFullName) as FollowerAtmMode.Named;
            string key = acc.Name + "#LANE-E#" + instrFullName;

            // Branch (3): dedup check -- only re-arm once per account+instrument slot
            if (_laneEDispatched.ContainsKey(key))
                return;

            // Dispatch: re-arm ATM bracket for the remaining position
            bool ok = TryLaneEDispatchFollower(acc, pos.Instrument, pos, namedMode, key);

            // Branch (4): gate open -- ATM is now armed; bracket sync is safe to proceed
            if (ok)
            {
                _followerSlateReady[acc.Name] = true;
                // B204: Lane E re-arm succeeded = this IS a legitimate re-entry (post-QX/BE with leader still in).
                // Clear flatten-latch so bracket sync proceeds normally for this new trade cycle.
                _laneEFlattenBlocked.TryRemove(acc.Name + "#LANE-E#" + pos.Instrument.FullName, out _);
            }
        }

        // Lane E: per-follower ATM re-arm helper. Keeps TryLaneEHook CYC low.
        // CYC=5: (A) acc null guard, (B) daily cap check, ?: action ternary, (C) SendCopyWithAtm result.
        // JS-001: no throw -- returns false on all failure paths; SendCopyWithAtm has its own try/catch.
        // JS-021: no lock -- ConcurrentDictionary.TryAdd is atomic. JS-002: returns bool (never null).
        // ASCII-only. Thread: NT8 account background thread (same as TryLaneEHook caller).
        private bool TryLaneEDispatchFollower(
            Account acc,
            NinjaTrader.Cbi.Instrument instr,
            NinjaTrader.Cbi.Position pos,
            FollowerAtmMode.Named namedMode,
            string dedupKey
        )
        {
            // Branch (A): null guard
            if (acc == null)
                return false;

            // Branch (B): daily position cap -- respect existing cap contract
            if (!PassesDailyCapCheck(acc))
                return false;

            // Build signal from follower's remaining position
            OrderAction action = pos.MarketPosition == NinjaTrader.Cbi.MarketPosition.Long
                ? OrderAction.Buy
                : OrderAction.SellShort;
            string syntheticOrderId = "LANE-E#" + acc.Name + "#" + instr.FullName;
            CopySignal sig = CopySignal.Create(
                action,
                OrderType.Limit,
                pos.Quantity,       // remaining quantity (already multiplier-scaled from original entry)
                pos.AveragePrice,   // ATM bracket anchored at original entry price
                syntheticOrderId
            );

            // Branch (C): dispatch ATM entry; mark dedup only on success
            bool ok = (SendCopyWithAtm(acc, instr, in sig, namedMode) != null); // T3: Order? return
            if (ok)
                _laneEDispatched.TryAdd(dedupKey, 0);
            return ok;
        }

        // Lane E: dedup key builder. Isolates the instr null-guard so CancelQxBrackets callers
        // incur +0 CYC from the null check. Called from both CancelQxBrackets overloads.
        // CYC=2: entry(1) + ternary null guard(1). ASCII-only. No lock (JS-021). static: no state.
        private static string BuildLaneEKey(Account acc, NinjaTrader.Cbi.Instrument instr)
        {
            return acc.Name + "#LANE-E#" + (instr != null ? instr.FullName : string.Empty);
        }

        // TB-T4 Gate 4 predicate: returns true when order type is market or limit (dispatchable).
        // Extracted from DispatchCopy to eliminate the compound && branch (Lizard counts && as +1).
        // CCN=2: Market check + Limit check = 2 Lizard branches.
        // internal: accessible to xUnit via InternalsVisibleTo("PropTraderTools.Tests") at L46.
        // JS-001: no throw. JS-021: no lock.
        internal static bool IsDispatchableOrderType(OrderType orderType)
        {
            if (orderType == OrderType.Market)
                return true;
            if (orderType == OrderType.Limit)
                return true;
            return false;
        }

        // B212-T3-R1: Re-entry suppression gate extracted from DispatchCopy to hold CYC<=8.
        // Returns true when the leader account is in a transient cancel window (BE-swap or flatten)
        // that would cause NT8 ATM to re-arm a spurious re-entry on the leader.
        // Gate B212-REENTRY-V3: IsBeSwapInProgress flag set by PttBreakEvenSwap before CancelQxBrackets;
        //   cleared when PTT-BE-Stop-N reaches Working (~50-200ms). ConcurrentDictionary -- lock-free.
        // Gate B212-REENTRY-FLATTEN: HasActivePttFlatten scans acc.Orders (lock-free NT8 snapshot)
        //   for PTT-Flatten in Submitted/Accepted/Working/PartFilled state.
        // CYC=3: (1)IsBeSwapInProgress if (2)HasActivePttFlatten if. Base=1.
        // JS-021: no lock. JS-001: no throw. ASCII-only.
        private bool IsSuppressedReentry(Order order)
        {
            if (IsBeSwapInProgress(order.Account?.Name))
                return true;
            if (order.Account != null && HasActivePttFlatten(order.Account, order.Instrument))
                return true;
            // B212-REENTRY-BETARGET: suppress copy dispatch for the leader account in the window
            // between a PTT-BE-Target-N fill (position closes) and DispatchCopy of next legitimate entry.
            // HasFilledBeTargetFast reads _filledBeTargetCount incremented by TryRecordBeTargetFill.
            // Counter is cleared in DispatchCopy after all gates pass (V2 -- moved from TryFirePositionState).
            // Lock-free ConcurrentDictionary read. CYC +1 here. JS-021: no lock. JS-001: no throw.
            _filledBeTargetCount.TryGetValue(order.Account?.Name ?? string.Empty, out int beCount);
            NinjaTrader.Code.Output.Process(
                "[BETARGET-GUARD] acct=" + (order.Account?.Name ?? "null")
                    + " name=" + (order.Name ?? "null")
                    + " state=" + order.OrderState
                    + " beCount=" + beCount,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            if (beCount > 0)
                return true;
            return false;
        }

        // B212-T3-R1: Gate 3+4 combined predicate extracted from DispatchCopy to hold CYC<=8.
        // Returns true when the order passes both the trigger-state gate (Gate 3) and the
        // dispatchable-type gate (Gate 4). Equivalent to the two formerly-separate if guards.
        // CYC=3: (1)IsDispatchTriggerState if (2)IsDispatchableOrderType if. Base=1.
        // JS-021: no lock. JS-001: no throw. ASCII-only.
        private bool IsDispatchableSignal(OrderState orderState, OrderType orderType)
        {
            if (!IsDispatchTriggerState(orderState, orderType)) // HOTFIX-MARKET-DEDUP-01
                return false;
            if (!IsDispatchableOrderType(orderType))
                return false;
            return true;
        }



        // TB-T4 Helper 1: null + cap guard + flatten-in-flight guard extracted from DispatchCopy loop.
        // Returns true when follower should be skipped.
        // CCN<=3: null check + PassesDailyCapCheck + HasActivePttFlatten = 3 Lizard branches.
        // B200-CLOSE-LANED-01-V7: added HasActivePttFlatten(acc, instr) guard (3rd branch).
        // Prevents DispatchCopy -> DispatchToFollower -> SendCopyWithAtm from firing a new ATM entry
        // on a follower that still has a PTT-Flatten order in-flight (Submitted/Accepted/Working/PartFilled).
        // Without this guard, leader close + immediate re-entry dispatches a full-qty ATM to followers
        // before their PTT-Flatten fills, creating a doubled position.
        // instr: the leader order instrument, passed from DispatchCopy (already available as local var).
        // JS-021: HasActivePttFlatten iterates acc.Orders.ToList() -- lock-free NT8 snapshot. JS-001: no throw.
        // internal: accessible to xUnit via InternalsVisibleTo("PropTraderTools.Tests") at L46.
        internal bool ShouldSkipFollowerDispatch(Account acc, Instrument instr)
        {
            if (acc == null)
                return true;
            if (!PassesDailyCapCheck(acc))
                return true;
            if (IsFlattenInFlightOrRecent(acc, instr))  // B200-CLOSE-LANED-01-V10: combined order-state + timestamp guard
                return true;
            return false;
        }

        // TB-T4 Helper 2: reversal entry guard extracted from DispatchCopy loop.
        // Returns true when hasLastDirection is true and follower is flat and direction reversed.
        // CCN<=3: hasLastDirection + IsReversalToFlatFollower + IsFlat = 3 Lizard branches.
        // B119: DW-B128 guard. JS-021: FindPosition/IsFlat are lock-free reads. JS-001: no throw.
        // internal: accessible to xUnit via InternalsVisibleTo("PropTraderTools.Tests") at L46.
        internal bool ShouldSkipForReversalGuard(
            Account acc,
            NinjaTrader.Cbi.Instrument instr,
            OrderAction currentAction,
            OrderAction lastAction,
            bool hasLastDirection
        )
        {
            if (!hasLastDirection)
                return false;
            bool followerIsFlat = IsFlat(FindPosition(acc, instr));
            if (!IsReversalToFlatFollower(currentAction, lastAction, followerIsFlat))
                return false;
            NinjaTrader.Code.Output.Process(
                "[PTT-COPY-GUARD] skip reversal entry: "
                    + acc.Name
                    + " "
                    + instr.FullName
                    + " follower flat",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            return true;
        }

        // TB-T4 Helper 3: per-follower dispatch body extracted from DispatchCopy loop.
        // Resolves multiplier, scales signal, resolves ATM mode, logs, then sends.
        // CCN<=3: mult clamp (in GetMultiplier) + Named-mode is-pattern + else = 3 Lizard branches.
        // NT8: SendCopy/SendCopyWithAtm are AddOnBase-available. JS-021: no lock. JS-001: no throw.
        private void DispatchToFollower(
            Account acc,
            Order order,
            CopyRule rule,
            int idx,
            CopySignal baseSignal,
            int baseQty
        )
        {
            int mult = GetMultiplier(rule, idx);
            var scaledSignal = CopySignal.Create(
                baseSignal.Action,
                baseSignal.Type,
                baseQty * mult,
                baseSignal.LimitPrice,
                baseSignal.OrderId
            );
            var mode = ResolveAtmMode(rule, acc.Name);
            // Lane D skip gate: if TryLaneDHook already dispatched this (order, follower) pair via ATM,
            // return immediately -- prevents one ATM entry (hook) + one raw entry (loop) double-dispatch.
            // _laneDDispatched.ContainsKey is O(1) lock-free (JS-021). No counter modification -- dedup
            // commit for non-ATM followers in this loop is unaffected.
            string laneD_key = baseSignal.OrderId + "#" + acc.Name;
            if (_laneDDispatched.ContainsKey(laneD_key)) return;
            NinjaTrader.Code.Output.Process(
                "[PTT-COPY] dispatch: "
                    + scaledSignal.Action
                    + " x"
                    + scaledSignal.Quantity
                    + " "
                    + order.Instrument.FullName
                    + " -> "
                    + acc.Name
                    + " mult="
                    + mult
                    + " mode="
                    + mode.GetType().Name
                    + " name="
                    + (order.Name ?? "null"),
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            if (mode is FollowerAtmMode.Named namedAtm) // HOTFIX-B66-NATIVE-ATM: Named mode -> native ATM
                SendCopyWithAtm(acc, order.Instrument, in scaledSignal, namedAtm);
            else
                SendCopy(acc, order.Instrument, in scaledSignal, mode);
        }

        // B8 T1: bounds-safe multiplier retrieval. CYC=3 (null guard + bounds guard + clamp).
        // Returns int >= 1; never returns < 1; never throws; never returns null (value type).
        // JS-001: no throw. JS-002: returns int (null impossible for value type).
        private static int GetMultiplier(CopyRule rule, int followerIndex)
        {
            if (rule.FollowerMultipliers == null)
                return 1;
            if (followerIndex < 0 || followerIndex >= rule.FollowerMultipliers.Length)
                return 1;
            int v = rule.FollowerMultipliers[followerIndex];
            return v < 1 ? 1 : (v > 10 ? 10 : v);
        }

        // CYC=3. Gate predicate for bracket detection in OnOrderUpdate.
        // B63: Accepted added -- NT8 bracket orders fire Accepted before (or instead of) Working.
        // NT8_FULL_REFERENCE.md line 1005: "some stop orders may only reach Accepted state".
        // Extending to Accepted is safe: SyncFollowerBracket price-delta guard absorbs double-fire.
        // JS-021: no lock. JS-001: no throw.
        internal static bool IsWorkingBracket(Order order)
        {
            return (
                    order.OrderState == OrderState.Working
                    || order.OrderState == OrderState.Accepted
                ) && IsBracketLegStatic(order);
        }

        // B10 T1 -- IsTrailingStop: trailing stop detection predicate.
        // CYC=1: single return expression.
        // NT8: Order.TrailPrice does not exist (CS1061). Use OrderType.StopMarket as proxy.
        // Callers guard order != null before calling (IsStopAlreadyAtBe already has null guard; loop filters).
        private static bool IsTrailingStop(Order order)
        {
            // NT8: Order.TrailPrice does not exist. Trailing stops are StopMarket orders;
            // downstream logic (TightenStop cancel+replace path) handles trail correctly.
            // B142-DIRECT: PTT-STP-Drag is an AddOn-created StopMarket order -- NOT a trailing stop.
            // Without this exclusion, branch (4) in SyncFollowerBracket silently skips ALL
            // second+ stop drags (after first cancel+resubmit replaces Stop1/2/3 with PTT-STP-Drag).
            return order.OrderType == OrderType.StopMarket
                && (order.Name == null || !order.Name.StartsWith("PTT-", StringComparison.Ordinal));
        }

        // DW-B134: true if order name has STP suffix (NT8 ATM bracket stops: "Buy STP", "Sell STP").
        // DW-B137: extended to cover Stop1/Stop2/Stop3 and Target1/Target2/Target3 (MES $200 SL 6 ATM).
        // Mirrors IsBracketLegStatic STP+Stop+Target clauses. Made internal static for test access.
        // Option A safety: grep confirms 0 CreateOrder calls use "Stop*"/"Target*" prefixed names.
        // B142-DIRECT-4: also matches PTT-STP-Drag-N (AddOn-created stop replacement after first drag).
        //   On second+ drags fo.Name is "PTT-STP-Drag-1/2/3" -- must take ATM cancel+resubmit path,
        //   not generic acc.Change(), so that ResubmitTargetAfterCascade also runs.
        // DW-B142-DRAG: also matches PTT-TGT-Drag-N (AddOn-created target replacement after first drag).
        //   On second+ drags fo.Name is "PTT-TGT-Drag-1/2/3" -- IsAtmSTPOrder was returning false,
        //   causing branch (3b) to be skipped and acc.Change() called (no-op). Symmetric fix to B142-DIRECT-4.
        // CYC=1: expression body. JS-021: no lock. JS-001: no throw. ASCII-only.
        internal static bool IsAtmSTPOrder(Order order) =>
            order.Name != null
            && (
                order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase)
                || order.Name.StartsWith("Stop", StringComparison.OrdinalIgnoreCase)
                || order.Name.StartsWith("Target", StringComparison.OrdinalIgnoreCase)
                || order.Name.StartsWith("PTT-STP-Drag-", StringComparison.Ordinal)
                || order.Name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)
                || order.Name.StartsWith("SimStop", StringComparison.Ordinal)
            );

        // B10 T1 -- IsStopAlreadyAtBe: idempotency guard.
        // CYC=2: long branch(1), short branch(2). Guards against double-BE submissions.
        private static bool IsStopAlreadyAtBe(Order order, double newStop, bool isLong)
        {
            if (order == null)
                return false;
            if (isLong)
                return order.StopPrice >= newStop;
            return order.StopPrice <= newStop;
        }

        // B10 T1 -- SyncFollowerBracket: extracted inner loop body from HandleBracketChange.
        // DW-B134/DW-B137: CYC=8: fo null(1), price delta(2), ATM STP(3), ATM TGT(3b), IsTrailingStop(4), isStop branch(5), DW-B168-02 Remove(6), [CYC from branching=8].
        // JS-001: try/catch around acc.Change() -- no throw in hot path.
        // DW-B9-GAP-001a: trailing stop follower orders are skipped (Option B: skip is safer).
        // DW-B134: ATM STP brackets (EndsWith "STP") require cancel+resubmit -- acc.Change() is no-op.
        private void SyncFollowerBracket(
            Account acc,
            Order leaderOrder,
            bool isStop,
            double newPrice,
            double tickSize
        )
        {
            var fo = FindFollowerBracketOrder(
                acc,
                leaderOrder.FromEntrySignal,
                isStop,
                leaderOrder.Name
            );
            TryLogSFBTrace(acc, leaderOrder, isStop, fo);
            if (fo == null) // (1)
            {
                // B199-R2: diagnostic log -- emit account order-name list so next SIM run shows why fo=NULL.
                // Fires only when fo not found; zero cost on the happy path.
                NinjaTrader.Code.Output.Process(
                    "[TP4-SFB-NULL] acc=" + acc?.Name
                        + " leaderName=" + leaderOrder?.Name
                        + " isStop=" + isStop
                        + " ordCount=" + (acc?.Orders?.Count.ToString() ?? "n/a"),
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return;
            }

            double currentPrice = isStop ? fo.StopPrice : fo.LimitPrice;
            if (Math.Abs(newPrice - currentPrice) < tickSize) // (2)
                return;

            // DW-B134: ATM STP path -- cancel+resubmit before IsTrailingStop guard.
            // DW-B137: ATM TGT path -- cancel+resubmit for target brackets (acc.Change() no-op).
            // DW-B154: acc.Change() confirmed no-op on ATM Stop brackets from AddOnBase (B140 SIM Gate 1 FAIL).
            // IsTrailingStop fires on StopMarket orders; ATM STP brackets ARE StopMarket.
            // Without branch (3), IsTrailingStop would return early and skip stop sync.
            // B142-DIRECT-2: fo.StopPrice==0 when NT8 ATM bracket is newly Accepted (price not yet
            // populated). The outer tickSize guard at (2) passes because |newPrice-0|>>tickSize.
            // CaptureLinkedTargetPrice must NOT run before we know the stop price is real -- doing so
            // fires a spurious ResubmitTargetAfterCascade even though SyncAtmFollowerBracket returns
            // early via IsNoPriceChange, cancelling the ATM bracket and Target3 on session start.
            if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134 + DW-B137 + DW-B153
            {
                if (fo.StopPrice < tickSize) // B142-DIRECT-2: skip when NT8 stop price not yet populated
                    return;
                // B142-DIRECT-4: derive suffix from leaderOrder.Name ("Stop1/2/3"), NOT fo.Name.
                // On first drag fo.Name=="Stop1" (ATM) -- both give same result.
                // On second+ drag fo.Name=="PTT-STP-Drag-1" -- TryParseStopSuffix("PTT-STP-Drag-1")
                //   would return false (Substring(4)="STP-Drag-1", not numeric), giving legSuffix="".
                // leaderOrder.Name is always the original ATM name ("Stop1/2/3") -- always parseable.
                // CaptureLinkedTargetPrice also uses leaderOrder.Name for same reason:
                //   on second drag the live target is "PTT-TGT-Drag-1", so we search by suffix "1"
                //   and the method also now searches "PTT-TGT-Drag-N" as fallback.
                // No new branch -- local variable + method call = 0 McCabe. CYC stays at 8.
                TryParseStopSuffix(leaderOrder.Name, out string stopSuffix);
                string legSuffix = stopSuffix ?? "";
                double? capturedTargetPrice = CaptureLinkedTargetPrice(acc, leaderOrder.Name); // B142-DIRECT-4: use leader name, not fo name
                // B142-DIRECT-6: capture other legs' target prices BEFORE cancel cascade kills them.
                // On first drag the ATM OCO group contains all 3 stops + 3 targets.
                // acc.Cancel(Stop1_ATM) cascades to cancel Stop2, Stop3, Target2, Target3.
                // When Stop2/Stop3 drag events arrive, fo==null (ATM order Cancelled, no PTT-STP-Drag-N yet).
                // Fix: snapshot other legs now, resubmit collateral legs after SyncAtmFollowerBracket.
                // CaptureOtherLegTargetPrices returns double[3] with prices indexed by suffix-1.
                // Returns all-zeros on second+ drag (fo.Name=="PTT-STP-Drag-N") -- safe no-op below.
                // Method call = 0 McCabe. CYC stays at 8.
                double[] otherLegPrices = CaptureOtherLegTargetPrices(acc, fo, legSuffix);
                SyncAtmFollowerBracket(acc, fo, newPrice, legSuffix, leaderOrder); // DW-B142-QTY-DESYNC-01: thread leaderOrder for qty
                if (capturedTargetPrice.HasValue) // B141: +1 branch -> CYC 8 (at limit -- no further branching may be added)
                    ResubmitTargetAfterCascade(
                        acc,
                        fo,
                        capturedTargetPrice.Value,
                        leaderOrder,
                        legSuffix
                    );
                ResubmitCollateralLegs(acc, fo, newPrice, otherLegPrices, legSuffix, leaderOrder); // DW-B142-QTY-DESYNC-01: thread leaderOrder for per-leg qty
                // DW-B168-02: remove suffix from in-flight set once Accepted processing is complete.
                // This allows the last handler (Stop3) to see a clear set for legs 1 and 2
                // and execute the normal ResubmitCollateralLegs path.
                if (!string.IsNullOrEmpty(legSuffix))
                    _dragsInFlight.Remove(legSuffix);
                return;
            }
            if (!isStop && IsAtmSTPOrder(fo)) // (3b) DW-B137: ATM target cancel+resubmit
            {
                SyncAtmFollowerTarget(acc, fo, newPrice, leaderOrder);
                return;
            }

            if (isStop && IsTrailingStop(fo)) // (4)
            {
                StatusUpdate?.Invoke("HandleBracketChange: skip trailing stop " + fo.Name);
                return;
            }

            try
            {
                if (isStop) // (4)
                    fo.StopPrice = newPrice;
                else
                    fo.LimitPrice = newPrice;
                acc.Change(new Order[] { fo });
                StatusUpdate?.Invoke(
                    acc.Name
                        + ": bracket synced "
                        + (isStop ? "stop" : "target")
                        + " -> "
                        + newPrice
                );
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke(acc.Name + ": bracket sync error: " + ex.Message);
            }
        }

        // DW-B134: cancel+resubmit for ATM-owned STP brackets.
        // acc.Change() is a no-op on ATM-engine brackets (confirmed CopyEngine.cs L3598-3601).
        // Pattern mirrors MoveStopToBreakEven cancel+resubmit (L3598+).
        // CYC=6: (1) acc null guard, (2) fo null guard, (3) IsNoPriceChange guard [T2 B137],
        //        (4) Block A catch, (5) Block B catch, (6) newStop null guard.
        // T4 B137: CancelExistingPttStpDrag(acc, fo) call added before Block A (DW-B151 pre-sweep).
        //   Method call adds 0 McCabe branches. CancelExistingPttStpDrag CYC counted in that method.
        // T2 B137: DW-B147/DW-B149 IsNoPriceChange guard added after fo null check.
        // Two independent try/catch blocks -- exception handlers add 0 McCabe branches each (per codebase convention L2301).
        // JS-021: no lock. JS-001: two independent try/catch -- no throw in hot path.
        //   Block A (Cancel): if Cancel throws, Block B still executes (independent isolation).
        //   Block B (CreateOrder+Submit): naked-position risk eliminated by isolation from Block A.
        // NT8-049: StopMarket arg6=0 (limitPrice), arg7=newPrice (stopPrice).
        // NT8-013: Core.Globals.MaxDate for gtd. NT8-007: (CustomOrder)null.
        // NT8-014: order name starts with "PTT-".
        // OQ-03: cancel of follower ATM bracket is SAFE -- Gate 2 (FindMatchingRule L1609)
        //        returns null for follower account orders, blocking TryCancelFollowerEntries.
        // TB-T6: IsSyncAtmBracketEligible -- CCN<=3. Predicate for SyncAtmFollowerBracket early-return guards.
        // Absorbs: acc null guard + fo null guard + IsNoPriceChange guard = 3 Lizard branches.
        // B137 DW-B147/DW-B149: IsNoPriceChange is an existing lock-free helper.
        // JS-021: no lock. JS-001: no throw. ASCII-only.
        // TESTABILITY: private; testable via IsSyncAtmBracketEligibleTestable seam below.
        private bool IsSyncAtmBracketEligible(Account acc, Order fo, double newPrice)
        {
            if (acc == null) // (1)
                return false;
            if (fo == null) // (2)
                return false;
            if (IsNoPriceChange(fo.StopPrice, newPrice)) // (3)
                return false;
            return true;
        }

        // TB-T6: test seam for IsSyncAtmBracketEligible -- accepts primitives to avoid constructing NT8 Account/Order in xUnit.
        // Mirrors IsEvictTriggerStateTestable pattern (L1636). accIsNull + foIsNull flags replace NT8 object references.
        internal static bool IsSyncAtmBracketEligibleTestable(
            bool accIsNull,
            bool foIsNull,
            double stopPrice,
            double newPrice
        )
        {
            if (accIsNull)
                return false;
            if (foIsNull)
                return false;
            if (IsNoPriceChange(stopPrice, newPrice))
                return false;
            return true;
        }

        // B142: suffix param added -- "1"/"2"/"3" for per-leg named orders.
        // Empty string used as safe fallback (produces "PTT-STP-Drag-" which MatchesLeaderName won't match ATM names -- harmless).
        // DW-B142-QTY-DESYNC-01: leaderOrder param added to supply correct stop qty.
        // TB-T6: SubmitAtmStopReplacement -- Block B helper extracted to reduce SyncAtmFollowerBracket CCN.
        // Contains: CreateOrder + null guard + Submit. Independent from Block A (cancel).
        // NT8: acc.CreateOrder + acc.Submit -- AddOnBase confirmed. "PTT-STP-Drag-" prefix preserved.
        // DW-B142-QTY-DESYNC-01: leaderOrder.Quantity used (not fo.Quantity).
        // JS-021: no lock. JS-001: try/catch in caller -- no throw here.
        private void SubmitAtmStopReplacement(
            Account acc,
            Order fo,
            double newPrice,
            string suffix,
            Order leaderOrder
        )
        {
            var newStop = acc.CreateOrder(
                fo.Instrument,
                fo.OrderAction,
                OrderType.StopMarket,
                OrderEntry.Automated,
                TimeInForce.Day,
                leaderOrder.Quantity, // DW-B142-QTY-DESYNC-01: use leader qty, not fo.Quantity
                0,
                newPrice,
                "",
                "PTT-STP-Drag-" + suffix,
                NinjaTrader.Core.Globals.MaxDate,
                (NinjaTrader.Cbi.CustomOrder)null
            );
            if (newStop == null)
            {
                StatusUpdate?.Invoke(acc.Name + ": ATM STP CreateOrder returned null");
                return;
            }
            acc.Submit(new[] { newStop });
            StatusUpdate?.Invoke(acc.Name + ": ATM STP resubmit -> " + newPrice);
        }

        private void SyncAtmFollowerBracket(
            Account acc,
            Order fo,
            double newPrice,
            string suffix,
            Order leaderOrder = null
        )
        {
            if (!IsSyncAtmBracketEligible(acc, fo, newPrice))
                return;

            CancelExistingPttStpDrag(acc, fo, suffix); // T4 B137 Block A-Prime pre-sweep (DW-B151)

            // Block A -- Cancel only. Independent: if Cancel throws, Block B still runs.
            try
            {
                acc.Cancel(new Order[] { fo });
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke(acc.Name + ": STP cancel error: " + ex.Message);
            }

            // Block B -- CreateOrder + Submit only. Runs regardless of Block A outcome.
            try
            {
                SubmitAtmStopReplacement(acc, fo, newPrice, suffix, leaderOrder);
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke(acc.Name + ": STP create error: " + ex.Message);
            }
        }

        // CYC=5: base(1)+if(1)+foreach(1)+if(1)+if(1). No lock. No async. ASCII-only.
        // B141: captures LimitPrice of the linked NT8 ATM target before Stop cancel+resubmit triggers OCO cascade.
        // "Stop1"->"Target1", "Stop2"->"Target2", "Stop3"->"Target3" (NT8 ATM naming, SIM log 2026-09-01).
        // Returns null if target not found or suffix not 1/2/3.
        // JS-002 note: double? is a nullable VALUE type -- this is NOT a reference null return.
        // B142-DIRECT-4: stopName is always leaderOrder.Name ("Stop1/2/3").
        //   targetName is "Target1/2/3" (first drag) or "PTT-TGT-Drag-1/2/3" (second+ drag after cascade).
        //   The || in the IsTargetOrderLive if-condition adds 0 McCabe branches. CYC stays at 4.
        // B142-DIRECT-9 BUG A: prefer PTT-TGT-Drag-N over ATM TargetN price.
        //   When both coexist (target dragged but ATM cancel was a no-op), the stop cascade captured
        //   ATM TargetN.LimitPrice (original price) and overwrote PTT-TGT-Drag-N (dragged price).
        //   Fix: full scan; return PTT price if found, else ATM price. CYC 4->5 (+1 if).
        //   && adds 0 McCabe per convention. Second if adds +1.
        private double? CaptureLinkedTargetPrice(Account acc, string stopName)
        {
            if (!TryParseStopSuffix(stopName, out string suffix)) // (1) if -- && NOT counted
                return null;
            string targetName = "Target" + suffix;
            string pttTgtName = "PTT-TGT-Drag-" + suffix;
            double? pttPrice = null;
            double? atmPrice = null;
            foreach (var o in acc.Orders.ToList()) // (2) foreach
            {
                if (IsTargetOrderLive(o) && o.Name == pttTgtName) // (3) if -- PTT preferred
                    pttPrice = o.LimitPrice;
                else if (IsTargetOrderLive(o) && o.Name == targetName) // (4) if -- ATM fallback
                    atmPrice = o.LimitPrice;
            }
            if (pttPrice.HasValue) // (5) if
                return pttPrice.Value;
            return atmPrice;
        }

        // CYC=6: base(1)+if(1)+foreach(1)+for(1)+if(1)+if(1). No lock. No async. ASCII-only.
        // B142-DIRECT-6: captures LimitPrice of all ATM target orders for legs OTHER than excludeSuffix.
        // Called before acc.Cancel(Stop1_ATM) -- which cascade-cancels Stop2/Stop3/Target2/Target3.
        // Returns double[3] indexed by suffix-1: prices[0]=leg1, prices[1]=leg2, prices[2]=leg3.
        // 0 means not found (skip resubmit for that leg).
        // Early-return guard: if fo.Name does not start with "Stop", this is a second+ drag where the
        //   ATM group is already broken -- other legs are standalone PTT orders, not cascade victims.
        //   Return all-zeros so ResubmitCollateralLegs no-ops. Guard adds +1 = still CYC=5.
        // JS-002: double[] is a value array, not a reference null return.
        // JS-021: no lock. NT8 Orders collection is thread-safe snapshot via ToList().
        // B142-DIRECT-9 BUG A: prefer PTT-TGT-Drag-N price over ATM TargetN price.
        //   When both coexist (target dragged but ATM cancel was a no-op), stop cascade used ATM price
        //   and overwrote the dragged PTT-TGT-Drag price. Fix: PTT always overwrites; ATM only fills zeros.
        //   Adds +1 McCabe (new else if). CYC 5->6.
        private double[] CaptureOtherLegTargetPrices(Account acc, Order fo, string excludeSuffix)
        {
            var prices = new double[MaxBracketLegs];
            if (!fo.Name.StartsWith("Stop") && !fo.Name.StartsWith("SimStop")) // (1) if -- second+ drag guard
                return prices;
            foreach (var o in acc.Orders.ToList()) // (2) foreach
            {
                for (int i = 1; i <= MaxBracketLegs; i++) // (3) for
                {
                    string s = i.ToString();
                    if (s == excludeSuffix) // (4) if
                        continue;
                    if (IsTargetOrderLive(o) && o.Name == "PTT-TGT-Drag-" + s) // (5) if -- PTT preferred: always overwrites
                        prices[i - 1] = o.LimitPrice;
                    else if (
                        IsTargetOrderLive(o)
                        && o.Name == "Target" + s // (6) if -- ATM fallback: only fills zero slots
                        && prices[i - 1] == 0
                    )
                        prices[i - 1] = o.LimitPrice;
                }
            }
            return prices;
        }

        // CYC=3: base(1)+if(1)+if(1). Static. Pure predicate. No lock. No async.
        // B141: extracts suffix from NT8 ATM stop name ("Stop1"->"1", "Stop2"->"2", "Stop3"->"3").
        // Rejects null, length < 5, or suffix not in {1, 2, 3}.
        // Uses int.TryParse to accept only valid numeric suffixes 1-3.
        private static bool TryParseStopSuffix(string stopName, out string suffix)
        {
            suffix = null;
            if (string.IsNullOrEmpty(stopName))                              // (1) if
                return false;
            int prefixLen;
            if (stopName.StartsWith("SimStop", StringComparison.Ordinal))    // (2) if
                prefixLen = 7;
            else if (stopName.StartsWith("Stop", StringComparison.Ordinal))  // (3) else if
                prefixLen = 4;
            else
                return false;
            if (stopName.Length <= prefixLen)                                 // (4) if
                return false;
            string raw = stopName.Substring(prefixLen);
            if (!int.TryParse(raw, out int n) || n < 1)                      // (5) if -- cap removed (was capped at 3 before)
                return false;
            suffix = raw;
            return true;
        }

        // DW-B168-02: thin wrapper around TryParseStopSuffix returning string.Empty on failure.
        // Returns "1", "2", or "3" for valid ATM stop names. Returns string.Empty for all others.
        // Used by TryTrackDragInFlight and TryRemoveDragSuffix to avoid out-param boilerplate.
        // CYC=1: pure delegation. JS-001: no throw. ASCII-only.
        private static string ParseLegSuffix(string orderName)
        {
            TryParseStopSuffix(orderName, out string stopSuffix);
            return stopSuffix ?? string.Empty;
        }

        // DW-B142-QTY-DESYNC-01: look up the leader's bracket order for collateral leg suffix s.
        // leaderOrder.Name is e.g. "Stop2"; for collateral suffix "1" this returns "Stop1".
        // Also tries "Target1" if "Stop1" is not found (covers target-leg lookup for same suffix).
        // Returns null if not found -- callers fall back to fo.Quantity.
        // CYC=3: base(1) + foreach(1) + if(1). JS-021: no lock. JS-001: no throw. JS-002: null is valid here.
        // ASCII-only. Iterates leader account orders snapshot.
        private static Order FindLeaderCollateralOrder(Order leaderOrder, string suffix)
        {
            if (leaderOrder?.Account?.Orders == null || string.IsNullOrEmpty(suffix)) // (1) if -- || NOT counted
                return null;
            string stopName    = "Stop"    + suffix;
            string tgtName     = "Target"  + suffix;
            string simStopName = "SimStop" + suffix;          // NEW
            foreach (var o in leaderOrder.Account.Orders.ToList()) // (2) foreach
            {
                if (o != null && (o.Name == stopName || o.Name == tgtName || o.Name == simStopName)) // NEW: || simStopName
                    return o;
            }
            return null;
        }

        // CYC=1: base(1). Static. Pure state predicate. No lock. No async.
        // B141: returns true if order is Working or Accepted -- both are live states.
        // B142-DIRECT-7 BUG A: Submitted added -- ATM engine places Target3 in Submitted state briefly
        //   before Working. CaptureOtherLegTargetPrices called IsTargetOrderLive and missed Submitted targets,
        //   leaving prices[2]=0 -> ResubmitCollateralLegs skipped leg 3 -> PTT-STP-Drag-3 never created.
        //   || adds 0 McCabe branches (compound on existing expression body). CYC stays at 1.
        // B142-DIRECT-9 BUG C: ChangeSubmitted/ChangePending added -- rapid drags leave PTT-TGT-Drag-N
        //   in ChangeSubmitted state. CaptureLinkedTargetPrice/CaptureOtherLegTargetPrices missed the order,
        //   falling back to ATM TargetN price (the ORIGINAL price) -- overwriting the dragged PTT price.
        //   Adding these states ensures capture prefers PTT-TGT-Drag-N even mid-change.
        //   || adds 0 McCabe branches. CYC stays at 1.
        // JS-002: bool return -- never null. || NOT counted per project convention.
        private static bool IsTargetOrderLive(Order o) =>
            o != null
            && (
                o.OrderState == OrderState.Working
                || o.OrderState == OrderState.Accepted
                || o.OrderState == OrderState.Submitted
                || o.OrderState == OrderState.ChangeSubmitted
                || o.OrderState == OrderState.ChangePending
            );

        // CYC=4: base(1)+foreach(1)+if(1)+if(1). No lock. No async. ASCII-only.
        // B141: after OCO cascade cancels linked ATM target, resubmits a standalone PTT-TGT-Drag
        // limit order at the captured price. Mirrors SyncAtmFollowerTarget Block A-Prime + Block B.
        // Block A-Prime: sweep stale PTT-TGT-Drag (prevents accumulation on consecutive drags -- DW-B139).
        // Block B: CreateOrder + Submit. oco="": PTT-TGT-Drag is NOT part of any ATM OCO group.
        // stpOrder.OrderAction: ATM brackets use matching exit action on both Stop and Target legs --
        //   e.g. LONG position: Stop=Sell, Target=Sell (both exit long). Use stpOrder.OrderAction directly.
        //   Confirmed by SyncAtmFollowerTarget Block B using fo.OrderAction where fo IS the target.
        //   Both stop and target legs of an ATM bracket share the same OrderAction direction.
        // JS-001: try/catch -- no throw in hot path. JS-021: no lock. NT8-007: arg12 cast guard.
        // B142: suffix param added -- sweep and create PTT-TGT-Drag-N (per-leg).
        // Prevents Stop1/Stop2/Stop3 concurrent resubmits from sweeping each other's targets.
        private void ResubmitTargetAfterCascade(
            Account acc,
            Order stpOrder,
            double targetPrice,
            Order leaderOrder,
            string suffix
        )
        {
            // Block A-Prime: cancel any stale PTT-TGT-Drag-N for this instrument.
            // B142: sweep only the matching leg suffix -- Stop1 does not cancel Stop2's target.
            // Mirrors SyncAtmFollowerTarget Block A-Prime (L2473-2490).
            // JS-021: no lock -- acc.Orders iteration safe on NT8 dispatch thread.
            string tgtDragName = "PTT-TGT-Drag-" + suffix;
            foreach (var o in acc.Orders.ToList()) // (1) foreach
            {
                if ( // (2) if -- all && NOT counted
                    o.OrderState == OrderState.Working
                    && o.Name == tgtDragName
                    && o.Instrument?.FullName == stpOrder.Instrument?.FullName
                )
                {
                    try
                    {
                        acc.Cancel(new Order[] { o });
                    }
                    catch (Exception ex) // catch = 0 (project convention)
                    {
                        StatusUpdate?.Invoke(
                            acc.Name + ": TGT pre-cancel error (B141): " + ex.Message
                        );
                    }
                }
            }

            // Block B: CreateOrder + Submit. Mirrors SyncAtmFollowerTarget Block B (L2502-2530).
            // JS-001: no throw -- absorb via StatusUpdate. NT8-007: arg12 = (NinjaTrader.Cbi.CustomOrder)null.
            try
            {
                var newTarget = acc.CreateOrder(
                    stpOrder.Instrument,
                    stpOrder.OrderAction,
                    OrderType.Limit,
                    OrderEntry.Automated,
                    TimeInForce.Day,
                    leaderOrder.Quantity, // DW-B142-QTY-DESYNC-01: use leader qty, not stpOrder.Quantity
                    targetPrice,
                    0,
                    "",
                    tgtDragName,
                    NinjaTrader.Core.Globals.MaxDate,
                    (NinjaTrader.Cbi.CustomOrder)null
                );
                if (newTarget == null) // (3) if
                {
                    StatusUpdate?.Invoke(acc.Name + ": B141 TGT CreateOrder returned null");
                    return;
                }
                acc.Submit(new[] { newTarget });
                StatusUpdate?.Invoke(
                    acc.Name + ": B141 TGT resubmit after cascade -> " + targetPrice
                );
            }
            catch (Exception ex) // catch = 0 (project convention)
            {
                StatusUpdate?.Invoke(acc.Name + ": B141 TGT create error: " + ex.Message);
            }
        }

        // CYC=6: base(1)+for(1)+if(1)+if(1)+if(1)+call(0)+call(0) = for+3 if = 4+base = 5. Conservative says 6 (safe).
        // DW-B168-02: +1 branch for _dragsInFlight.Contains(s) guard (guard-replaces-guard pattern).
        // B142-DIRECT-6: for each collateral leg (suffix 1-3, excluding the primary leg),
        //   if a target price was captured, resubmit PTT-STP-Drag-N + PTT-TGT-Drag-N.
        // Called after SyncAtmFollowerBracket+ResubmitTargetAfterCascade -- ATM OCO group is broken.
        // On second+ drag, otherLegPrices is all-zeros (guard in CaptureOtherLegTargetPrices) -- no-op.
        // JS-021: no lock. JS-001: delegates throw-free to ResubmitOneCollateralLeg.
        // JS-002: void. ASCII-only. No DateTime.
        // DW-B142-QTY-DESYNC-01: leaderOrder param added to thread leader qty per collateral leg.
        // Looks up StopN/TargetN from leaderOrder.Account.Orders by suffix to get per-leg leader qty.
        // Falls back to fo.Quantity when leader leg not found (safe: preserves prior behaviour).
        private void ResubmitCollateralLegs(
            Account acc,
            Order fo,
            double newPrice,
            double[] otherLegPrices,
            string excludeSuffix,
            Order leaderOrder
        )
        {
            for (int i = 1; i <= MaxBracketLegs; i++) // (1) for
            {
                string s = i.ToString();
                if (s == excludeSuffix) // (2) if
                    continue;
                if (otherLegPrices[i - 1] <= 0) // (3) if
                    continue;
                // DW-B142-QTY-DESYNC-01 + DW-B168-02 guard.
                // HashSet.Contains is O(1) alloc-free. Skip if this suffix's own
                // HandleAtmStopSync is in flight -- resubmitting here would storm.
                // Guard precedes FindLeaderCollateralOrder (O(n) ToList) to short-circuit.
                if (_dragsInFlight.Contains(s)) // (4) if -- DW-B168-02
                    continue;
                // DW-B142-QTY-DESYNC-01: look up the leader's per-leg bracket order for this suffix.
                // leaderOrder.Name is e.g. "Stop2"; collateral leg s="1" or "3" -> look up "Stop1"/"Stop3".
                // Also try "Target1"/"Target3" since CaptureOtherLegTargetPrices may have stored either.
                Order leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
                ResubmitOneCollateralLeg(acc, fo, newPrice, otherLegPrices[i - 1], s, leaderLeg);
            }
        }

        // CYC=7: base(1) + foreach(1) + if(1) + foreach(1) + if(1) + if(1) + if(1) = 7. No lock. No async. ASCII-only.
        // B142-DIRECT-6: creates PTT-STP-Drag-{suffix} at newPrice and PTT-TGT-Drag-{suffix} at targetPrice.
        // Both orders are standalone (oco=""): not in any ATM OCO group -- no cascade on cancel.
        // B142-DIRECT-8: Block A-Prime-Stop + Block A-Prime-Target added.
        //   Without pre-sweep, each stop drag calls ResubmitCollateralLegs for all non-dragged legs.
        //   On drag N, the leg already has a PTT-STP-Drag-{suffix} + PTT-TGT-Drag-{suffix} from drag N-1.
        //   Creating without cancelling accumulates N copies per leg after N stop drags (DW-B139 recurrence).
        //   Fix: cancel any live PTT-STP-Drag-{suffix} and PTT-TGT-Drag-{suffix} before resubmitting.
        //   Mirrors SyncAtmFollowerTarget Block A-Prime (L2791-2811) and ResubmitTargetAfterCascade Block A-Prime.
        // Mirrors SyncAtmFollowerBracket Block B (stop) + ResubmitTargetAfterCascade Block B (target).
        // try/catch blocks -- 0 McCabe each (project convention L2356).
        // NT8-049: StopMarket arg6=0 (limitPrice), arg7=newPrice (stopPrice).
        // NT8-049: Limit arg5=targetPrice (limitPrice), arg6=0 (stopPrice unused).
        // NT8-013: MaxDate for GTC. NT8-007: (CustomOrder)null. NT8-014: PTT- prefix.
        // fo is used for Instrument, OrderAction -- shared across ATM legs (same direction).
        // DW-B142-QTY-DESYNC-01: leaderLeg param provides per-leg leader qty; null -> fo.Quantity fallback.
        private void ResubmitOneCollateralLeg(
            Account acc,
            Order fo,
            double newPrice,
            double targetPrice,
            string suffix,
            Order leaderLeg = null
        )
        {
            // Block A-Prime-Stop: cancel any existing live PTT-STP-Drag-{suffix} before resubmitting.
            // Prevents accumulation when repeated stop drags call ResubmitCollateralLegs for the same leg.
            string stpDragName = "PTT-STP-Drag-" + suffix;
            foreach (var o in acc.Orders.ToList()) // (1) foreach
            {
                if (
                    IsPttStpDragCancellable(o)
                    && o.Name == stpDragName // (2) if -- && NOT counted
                    && o.Instrument?.FullName == fo.Instrument?.FullName
                )
                    try
                    {
                        acc.Cancel(new Order[] { o });
                    }
                    catch { } // catch = 0 (project convention)
            }

            // Block A-Prime-Target: cancel any existing live PTT-TGT-Drag-{suffix} before resubmitting.
            string tgtDragName = "PTT-TGT-Drag-" + suffix;
            foreach (var o in acc.Orders.ToList()) // (3) foreach
            {
                if (
                    IsTargetOrderLive(o)
                    && o.Name == tgtDragName // (4) if -- && NOT counted
                    && o.Instrument?.FullName == fo.Instrument?.FullName
                )
                    try
                    {
                        acc.Cancel(new Order[] { o });
                    }
                    catch { } // catch = 0 (project convention)
            }

            try
            {
                var newStop = acc.CreateOrder(
                    fo.Instrument,
                    fo.OrderAction,
                    OrderType.StopMarket,
                    OrderEntry.Automated,
                    TimeInForce.Day,
                    leaderLeg != null ? leaderLeg.Quantity : fo.Quantity, // DW-B142-QTY-DESYNC-01: per-leg leader qty
                    0,
                    newPrice,
                    "",
                    "PTT-STP-Drag-" + suffix,
                    NinjaTrader.Core.Globals.MaxDate,
                    (NinjaTrader.Cbi.CustomOrder)null
                );
                if (newStop == null) // (1) if
                {
                    StatusUpdate?.Invoke(acc.Name + ": B142-D6 STP CreateOrder null leg " + suffix);
                    return;
                }
                acc.Submit(new[] { newStop });
                StatusUpdate?.Invoke(
                    acc.Name + ": B142-D6 STP resubmit leg " + suffix + " -> " + newPrice
                );
            }
            catch (Exception ex) // catch = 0 (project convention)
            {
                StatusUpdate?.Invoke(
                    acc.Name + ": B142-D6 STP create error leg " + suffix + ": " + ex.Message
                );
            }

            try
            {
                var newTarget = acc.CreateOrder(
                    fo.Instrument,
                    fo.OrderAction,
                    OrderType.Limit,
                    OrderEntry.Automated,
                    TimeInForce.Day,
                    leaderLeg != null ? leaderLeg.Quantity : fo.Quantity, // DW-B142-QTY-DESYNC-01: per-leg leader qty
                    targetPrice,
                    0,
                    "",
                    "PTT-TGT-Drag-" + suffix,
                    NinjaTrader.Core.Globals.MaxDate,
                    (NinjaTrader.Cbi.CustomOrder)null
                );
                if (newTarget == null) // (2) if
                {
                    StatusUpdate?.Invoke(acc.Name + ": B142-D6 TGT CreateOrder null leg " + suffix);
                    return;
                }
                acc.Submit(new[] { newTarget });
                StatusUpdate?.Invoke(
                    acc.Name + ": B142-D6 TGT resubmit leg " + suffix + " -> " + targetPrice
                );
            }
            catch (Exception ex) // catch = 0 (project convention)
            {
                StatusUpdate?.Invoke(
                    acc.Name + ": B142-D6 TGT create error leg " + suffix + ": " + ex.Message
                );
            }
        }

        // CYC=5: base(1) + ||(1) + ||(1) + ||(1) + ||(1) = 5.
        // Pure state predicate -- no side effects. Static.
        // Returns true for all non-terminal states where a PTT-STP-Drag may still be cancelled.
        // Submitted: order en-route to broker. Working: live in exchange. Accepted: acked by broker.
        // CancelPending: cancel dispatched by NT8, not yet acked by broker.
        // CancelSubmitted: cancel acked by broker, not yet confirmed by exchange.
        // B139: DW-B152-B fix -- closes cancel-in-flight race (CancelPending/CancelSubmitted gap).
        // JS-002: bool return, no null. ASCII-only. No DateTime. No lock.
        private static bool IsPttStpDragCancellable(Order o) =>
            o.OrderState == OrderState.Submitted
            || o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Accepted
            || o.OrderState == OrderState.CancelPending
            || o.OrderState == OrderState.CancelSubmitted;

        // CYC=1: pure delegation to IsPttStpDragCancellable.
        // Test seam for xUnit access. InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
        internal static bool IsPttStpDragCancellableTestable(Order o) => IsPttStpDragCancellable(o);

        // DW-B168-02: test seam for _dragsInFlight. Returns the live instance HashSet for
        // test priming (_dragsInFlight.Add/Remove) and assertion (Contains).
        // CYC=1: pure property delegation. No NT8 runtime required.
        // Different from static seams: _dragsInFlight is instance state, not static.
        internal HashSet<string> DragsInFlightForTest() => _dragsInFlight;

        // CYC=6: base(1) + foreach(1) + if(1) + &&Name(1) + &&Instrument(1) + ?.(1) = 6.
        // B139: DW-B152-B fix -- IsPttStpDragCancellable extracted to include CancelPending||CancelSubmitted.
        // OrderState filter now covers: Submitted||Working||Accepted||CancelPending||CancelSubmitted.
        // acc.Cancel() on CancelPending/CancelSubmitted is idempotent; rejection absorbed by try/catch.
        // JS-021: no lock. JS-001: try/catch -- no rethrow. JS-002: void return.
        // acc.Orders.ToList(): thread-safe snapshot. ASCII-only. No DateTime.
        // B142: suffix param added -- sweep only the matching leg's PTT-STP-Drag-N.
        private void CancelExistingPttStpDrag(Account acc, Order fo, string suffix)
        {
            string stpDragName = "PTT-STP-Drag-" + suffix;
            foreach (var o in acc.Orders.ToList())
            {
                if (
                    IsPttStpDragCancellable(o)
                    && o.Name == stpDragName
                    && o.Instrument?.FullName == fo.Instrument?.FullName
                )
                {
                    try
                    {
                        acc.Cancel(new Order[] { o });
                    }
                    catch (Exception ex)
                    {
                        StatusUpdate?.Invoke(acc.Name + ": STP pre-cancel error: " + ex.Message);
                    }
                }
            }
        }

        // Test seam for xUnit access. InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
        // CYC=1: pure delegation to CancelExistingPttStpDrag.
        // B142: suffix param added to match production signature.
        internal void CancelExistingPttStpDragTestable(Account acc, Order fo, string suffix) =>
            CancelExistingPttStpDrag(acc, fo, suffix);

        // DW-B137: cancel+resubmit for ATM-owned target brackets (Limit type).
        // acc.Change() is a no-op on ATM-engine brackets (confirmed B129 SIM gate 2026-08-31).
        // Pattern mirrors SyncAtmFollowerBracket (DW-B134/B129 LaneB).
        // DW-B139 fix: Block A-Prime pre-sweep cancels prior PTT-TGT-Drag-{N} before Block B.
        // CYC=8: (1) acc null, (2) fo null, (3) price guard [T2],
        //        (4) foreach A-Prime, (5) OrderState==Working, (6) Name==tgtDragName,
        //        (7) catch A-Prime, (8) Block A catch.
        // AT LIMIT. T2 B137: DW-B147/DW-B149 guard. T1 B137: Phase C -> ExecutePhaseCStopReplacement.
        // Two independent try/catch blocks -- Block A isolates Cancel; Block B isolates CreateOrder+Submit.
        // JS-021: no lock. JS-001: two independent try/catch -- no throw in hot path.
        // NT8-049: Limit order arg5=limitPrice (newPrice), arg6=0 (stopPrice unused for Limit).
        // NT8-013: Core.Globals.MaxDate for GTC. NT8-007: (CustomOrder)null.
        // NT8-014: order name starts with "PTT-" ("PTT-TGT-Drag-N").
        // OQ-03: cancel of follower ATM target bracket SAFE -- Gate 2 (FindMatchingRule L1609)
        //        returns null for follower account orders, blocking TryCancelFollowerEntries.
        // B142-DIRECT-5: fo.LimitPrice<=0 added to guard (3) -- same fix as B142-DIRECT-2 for stops.
        //   When ATM target is in Submitted state its LimitPrice==0 and the leader target has a real price.
        //   IsNoPriceChange(0, 7647.25) = false, so the old guard did NOT block the cancel.
        //   acc.Cancel(Target3) OCO-cascade kills Stop3 on follower -- Stop3 is gone before first drag fires.
        //   When drag arrives, FindFollowerBracketOrder finds no Stop3 (Cancelled), fo=NULL, skipped.
        //   The || adds 0 McCabe branches (compound condition on existing branch (3)). CYC stays at 8.
        // B142-DIRECT-7 BUG B: per-leg PTT-TGT-Drag-{N} name.
        //   leaderOrder.Name is "Target1/2/3". DeriveLeaderBracketIndex extracts trailing digit.
        //   tgtDragName = "PTT-TGT-Drag-1/2/3". local var + string concat = 0 McCabe. CYC stays at 8.
        //   Block A-Prime sweeps "PTT-TGT-Drag-N" (was "PTT-TGT-Drag" -- unsuffixed, stale accumulation).
        //   Block B creates "PTT-TGT-Drag-N" (was "PTT-TGT-Drag" -- wrong name on concurrent legs).
        private void SyncAtmFollowerTarget(
            Account acc,
            Order fo,
            double newPrice,
            Order? leaderOrder = null
        )
        {
            if (acc == null) // (1)
                return;
            if (fo == null) // (2)
                return;
            if (fo.LimitPrice <= 0 || IsNoPriceChange(fo.LimitPrice, newPrice)) // (3) B142-DIRECT-5: fo.LimitPrice<=0 guard -- same fix as B142-DIRECT-2 for stops.
                return;

            // B142-DIRECT-7 BUG B: derive per-leg target drag name from leaderOrder ("Target1/2/3").
            // DeriveLeaderBracketIndex("Target1")->1, ("Target2")->2, ("Target3")->3.
            // Returns 0 if leaderOrder is null or name has no trailing digit -- fallback to unsuffixed.
            // Local var assignment = 0 McCabe. CYC stays at 8.
            int tgtIdx = DeriveLeaderBracketIndex(leaderOrder);
            string tgtDragName = tgtIdx > 0 ? "PTT-TGT-Drag-" + tgtIdx.ToString() : "PTT-TGT-Drag";

            // Block A-Prime -- cancel any existing PTT-TGT-Drag-N for this instrument on the follower.
            // Prevents accumulation of Working PTT-TGT-Drag orders on repeated drag events (DW-B139).
            // JS-001: try/catch -- no throw in hot path. JS-021: no lock -- acc.Orders iteration safe.
            foreach (var o in acc.Orders.ToList())
            {
                if (
                    o.OrderState == OrderState.Working
                    && o.Name == tgtDragName
                    && o.Instrument?.FullName == fo.Instrument?.FullName
                )
                {
                    try
                    {
                        acc.Cancel(new Order[] { o });
                    }
                    catch (Exception ex)
                    {
                        StatusUpdate?.Invoke(acc.Name + ": TGT pre-cancel error: " + ex.Message);
                    }
                }
            }

            // Block A -- Cancel only. Independent: if Cancel throws, Block B still runs.
            try
            {
                acc.Cancel(new Order[] { fo });
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke(acc.Name + ": TGT cancel error: " + ex.Message);
            }

            // Block B -- CreateOrder + Submit only. Runs regardless of Block A outcome.
            try
            {
                var newTarget = acc.CreateOrder(
                    fo.Instrument,
                    fo.OrderAction,
                    OrderType.Limit,
                    OrderEntry.Automated,
                    TimeInForce.Day,
                    leaderOrder != null ? leaderOrder.Quantity : fo.Quantity, // DW-B142-QTY-DESYNC-01: use leader qty when available
                    newPrice,
                    0,
                    "",
                    tgtDragName,
                    NinjaTrader.Core.Globals.MaxDate,
                    (NinjaTrader.Cbi.CustomOrder)null
                );
                if (newTarget == null) // (3)
                {
                    StatusUpdate?.Invoke(acc.Name + ": ATM TGT CreateOrder returned null");
                    return;
                }
                acc.Submit(new[] { newTarget });
                StatusUpdate?.Invoke(acc.Name + ": ATM TGT resubmit -> " + newPrice);
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke(acc.Name + ": TGT create error: " + ex.Message);
            }

            ExecutePhaseCStopReplacement(acc, fo, leaderOrder); // T1 B137: Phase C extracted
        }

        // B132 LaneA -- DeriveLeaderBracketIndex: parse integer suffix from leader order name.
        // e.g. "Target3" -> 3, "Stop99" -> 99, null -> 0.
        // CYC=3: (1) null/empty guard; (2) int.TryParse; (3) n <= 0 guard. JS-002: returns 0 on all failure paths.
        private static int DeriveLeaderBracketIndex(Order? leaderOrder)
        {
            if (leaderOrder == null || string.IsNullOrEmpty(leaderOrder.Name)) // (1)
                return 0;
            var name = leaderOrder.Name;
            var i = name.Length - 1;
            while (i >= 0 && char.IsDigit(name[i]))
                i--;
            if (i == name.Length - 1) // no trailing digit
                return 0;
            if (!int.TryParse(name.Substring(i + 1), out var n)) // (2)
                return 0;
            if (n <= 0) // (3)
                return 0;
            return n;
        }

        // B132 LaneA -- FindLeaderStopPrice: scan leader account orders for Working "Stop{N}".
        // Returns StopPrice of the match, or 0.0 if not found.
        // CYC=6: (1) null account; (2) zero index; (3) foreach; (4) name match; (5) state==Working; (6) state==Accepted. DW-B170-01.
        // JS-002: returns 0.0 on all failure paths. JS-021: no lock (NT8 Orders collection is thread-safe).
        private static double FindLeaderStopPrice(Account? leaderAccount, int bracketIndex)
        {
            if (leaderAccount == null) // (1)
                return 0.0;
            if (bracketIndex <= 0) // (2)
                return 0.0;
            var targetName  = "Stop"    + bracketIndex.ToString();
            var simStopName = "SimStop" + bracketIndex.ToString();   // NEW
            foreach (var order in leaderAccount.Orders.ToList()) // (3)
            {
                if (
                    (order.Name == targetName || order.Name == simStopName)  // NEW: || simStopName
                    && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted) // (5)(6)
                ) // DW-B170-01
                    return order.StopPrice;
            }
            return 0.0;
        }

        // B132 LaneA -- CreateFollowerReplacementStop: place PTT-STP-Drag StopMarket on followerAcc.
        // CYC=4: (1) stopPrice guard; (2) try block; (3) newStop null check; (4) catch.
        // JS-001: catch+return -- no rethrow. JS-002: void method. NT8-014: "PTT-STP-Drag" PTT- prefix.
        // oco="": PTT-STP-Drag is NOT part of any NT8 ATM OCO group (NT8_FULL_REFERENCE.md L2118).
        private void CreateFollowerReplacementStop(
            Account followerAcc,
            Instrument instr,
            int qty,
            OrderAction stopAction,
            double stopPrice
        )
        {
            if (stopPrice <= 0.0) // (1)
            {
                StatusUpdate?.Invoke(followerAcc?.Name + ": PTT-STP-Drag skipped: stopPrice <= 0");
                return;
            }
            try // (2)
            {
                var newStop = followerAcc.CreateOrder(
                    instr,
                    stopAction,
                    OrderType.StopMarket,
                    OrderEntry.Automated,
                    TimeInForce.Day,
                    qty,
                    0,
                    stopPrice,
                    "",
                    "PTT-STP-Drag",
                    NinjaTrader.Core.Globals.MaxDate,
                    (NinjaTrader.Cbi.CustomOrder)null
                );
                if (newStop == null) // (3)
                {
                    StatusUpdate?.Invoke(
                        followerAcc.Name + ": PTT-STP-Drag: CreateOrder returned null"
                    );
                    return;
                }
                followerAcc.Submit(new[] { newStop });
                StatusUpdate?.Invoke(
                    followerAcc.Name + ": PTT-STP-Drag placed @ " + stopPrice.ToString()
                );
            }
            catch (Exception ex) // (4)
            {
                StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag error: " + ex.Message);
            }
        }

        // CYC=2. Extracted Phase C block from SyncAtmFollowerTarget (T1 extraction -- B137).
        // Replaces inline Phase C code (L2439-2442 pre-B137):
        //   DeriveLeaderBracketIndex + FindLeaderStopPrice + CreateFollowerReplacementStop.
        // McCabe branches: base(1) + leaderOrder?.Account null-conditional(1) = CYC=2.
        // Extraction reduces SyncAtmFollowerTarget from CYC=8 to CYC=7 (removes ?. branch from parent).
        // ZERO behavior change. JS-021: no lock. JS-001: delegates to CreateFollowerReplacementStop catch.
        // JS-002: void return. ASCII-only. No DateTime. No FontFamily.
        private void ExecutePhaseCStopReplacement(Account acc, Order fo, Order? leaderOrder)
        {
            int bracketIdx = DeriveLeaderBracketIndex(leaderOrder);
            double stp = FindLeaderStopPrice(leaderOrder?.Account, bracketIdx);
            CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp);
        }

        // B10 T1 -- HandleBracketChange: delegates inner loop body to SyncFollowerBracket.
        // CYC=7: isStop(1), instr null(2), PTT-BE guard(3), tickSize(4), rawPrice(5), foreach acc(6), acc null(7).
        // B212-DW01: PTT-BE-* guard added -- cross-account BE price sync is architecturally wrong (CYC 6->7).
        // JS-001: try/catch inside SyncFollowerBracket -- no throw in hot path.
        // JS-021: no lock -- _orderMap uses ConcurrentDictionary (atomic).
        // JS-028: CYC=7 <= 8 ceiling. ASCII-only.
        // V02: tick-rounded price applied BEFORE price-delta guard (preserved in SyncFollowerBracket).
        private void HandleBracketChange(Order leaderOrder, CopyRule rule)
        {
            bool isStop = IsStopLeg(leaderOrder); // (1)

            var instrument = leaderOrder.Instrument;
            if (instrument == null) // (2)
                return;

            // B212-DW01: PTT-BE-* brackets managed independently per account by
            // PttBeSwapAll. Cross-account price-sync via TP3-HBC is architecturally
            // incorrect and always fails at this tick (followers have no PTT-BE-*
            // orders yet when Sim101's fires). CYC delta: +1 (6->7). JS-028 OK.
            if (leaderOrder.Name != null
                && leaderOrder.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)) // (3)
                return;

            double tickSize = instrument.MasterInstrument?.TickSize ?? 0.0; // (4)
            double rawPrice = isStop ? leaderOrder.StopPrice : leaderOrder.LimitPrice; // (5)
            // V02: tick rounding applied BEFORE price-delta guard
            double newPrice = tickSize > 0 ? Math.Round(rawPrice / tickSize) * tickSize : rawPrice;
            if (_diagnosticMode)
                NinjaTrader.Code.Output.Process(
                    "[TP3-HBC] isStop="
                        + isStop
                        + " leaderName="
                        + (leaderOrder.Name ?? "null")
                        + " rawPrice="
                        + rawPrice
                        + " newPrice="
                        + newPrice
                        + " followerCount="
                        + rule.FollowerAccounts.Length,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );

            foreach (var acc in rule.FollowerAccounts) // (6)
            {
                if (acc == null) // (7)
                    continue;
                SyncFollowerBracket(acc, leaderOrder, isStop, newPrice, tickSize);
            }
        }

        // B131 DW-B138: predicate encapsulating signal-first / name-fallback match logic.
        // B133 DW-B142: null-guard added to branch (1) -- prevents null==null false-positive (ATM drag cancel-all bug).
        // CYC=3: (1) signal equality check, (2) leaderName null guard, (3) name equality check.
        // JS-021: no lock (static, no shared state). JS-001: no throw. JS-002: returns bool (no null).
        // ASCII-only. DateTime.UtcNow not used (no time logic).
        internal static bool SignalOrNameMatches(
            Order order,
            string? signalName,
            string? leaderName
        )
        {
            if (signalName != null && order.FromEntrySignal == signalName) // (1) primary: signal equality (null-guarded)
                return true;
            if (leaderName == null) // (2) no fallback available
                return false;
            return order.Name == leaderName; // (3) ATM Name-based fallback
        }

        // DW-NEW-09: ActiveOrders -- terminal-state filter for Account.Orders.
        // Returns only orders in non-terminal states (Filled/Cancelled/Rejected excluded).
        // CYC=1: expression body, single Where predicate, no branching.
        // JS-021: no lock (LINQ Where is non-mutating). JS-002: IEnumerable<Order> (never null).
        // JS-036: lazy Where -- no heap allocation beyond the enumerator.
        // Fix point: callers that need active orders use this instead of .ToList().
        // NT8: acc.Orders iteration is safe on order-update callback thread (same as existing ToList() pattern).
        private static IEnumerable<Order> ActiveOrders(Account acc) =>
            acc
                .Orders.Where(static o =>
                    o.OrderState != OrderState.Filled
                    && o.OrderState != OrderState.Cancelled
                    && o.OrderState != OrderState.Rejected
                )
                .ToList();

        // DW-NEW-09: test seam -- exposes ActiveOrders filter logic for xUnit without needing NT8 Account.
        // Accepts any IEnumerable<Order> so tests can inject stub order lists directly.
        // InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
        internal static IEnumerable<Order> ActiveOrdersTestable(IEnumerable<Order> orders) =>
            orders.Where(static o =>
                o.OrderState != OrderState.Filled
                && o.OrderState != OrderState.Cancelled
                && o.OrderState != OrderState.Rejected
            );

        // CYC=4. Returns first matching working bracket order for the follower.
        // V04 B131 DW-B138: leaderName param added -- ATM Name-based fallback when FromEntrySignal null/empty.
        // V03: return type is Order? (nullable) -- null contract explicit (JS-002 compliant).
        // V01: matching by FromEntrySignal name -- not leg-type scan.
        // JS-021: no lock. JS-001: no throw. JS-002: Order? makes null contract explicit.
        // DW-B143: state filter extended to include OrderState.Accepted (was Working-only).
        // DW-B144: state filter extended to include OrderState.Submitted (B134 fix).
        // Accepted orders are broker-confirmed but not yet exchange-Working -- cancel is safe.
        // Submitted orders are live (non-terminal) -- cancel absorbs ErrorCode.UnableToCancelOrder.
        private Order? FindFollowerBracketOrder(
            Account follower,
            string? fromEntrySignalName,
            bool isStop,
            string? leaderName = null
        )
        {
            // BUG1+BUG2: slate gate -- if bracket wipe was signalled, block bracket lookup.
            // Absent key (first trade): gate open. Key=false: gate closed. Key=true: gate open.
            // CYC: +1 (from 1 to 2). JS-021: ConcurrentDictionary.TryGetValue is lock-free. ASCII-only.
            bool ready;
            if (_followerSlateReady.TryGetValue(follower.Name, out ready) && !ready) // (1)
                return null;
            return FindFollowerBracketOrder(
                ActiveOrders(follower), // DW-NEW-09: terminal orders excluded
                fromEntrySignalName,
                isStop,
                leaderName
            );
        }

        // CYC=8 (post-B142-DIRECT-9). AT LIMIT.
        // foreach(1) + OrderPassesBracketGate guard(1) + state filter(4) + isStop(1) + type match(1) = 8.
        // DW-B143: Accepted added. DW-B144: Submitted added. DW-B145: leaderName exact guard. DW-B146: MatchesLeaderName helper. DW-B148: OrderPassesBracketGate fused guard (B136).
        // B142-DIRECT-9 BUG B: ChangeSubmitted added -- rapid back-to-back drags leave PTT-TGT-Drag-N in
        //   ChangeSubmitted state when the next drag fires. Without this, fo=NULL -> drag missed entirely.
        //   With ChangeSubmitted in filter, fo=PTT-TGT-Drag-N (ChangeSubmitted) -> acc.Change() issued.
        //   NT8 queues or absorbs the overlapping change; follower price converges to leader price.
        //   Adds +1 McCabe (state filter 3->4). CYC 7->8 (headroom exhausted).
        // JS-021: no lock. JS-001: no throw. JS-002: Order? null contract unchanged.
        private Order? FindFollowerBracketOrder(
            IEnumerable<Order> orders,
            string? fromEntrySignalName,
            bool isStop,
            string? leaderName = null
        )
        {
            foreach (var order in orders) // (1) branch
            {
                if (!OrderPassesBracketGate(order, fromEntrySignalName, leaderName, isStop)) // (1) branch -- B136 DW-B148: fused guard (ATM path routes to MatchesLeaderName)
                    continue;
                if (
                    order.OrderState != OrderState.Working // (4) branches -- B142-DIRECT-9: ChangeSubmitted added
                    && order.OrderState != OrderState.Accepted
                    && order.OrderState != OrderState.Submitted
                    && order.OrderState != OrderState.ChangeSubmitted
                )
                    continue;
                if (isStop) // (1) branch
                {
                    if (
                        order.OrderType == OrderType.StopMarket
                        || order.OrderType == OrderType.StopLimit
                    ) // (1) branch
                        return order;
                }
                else
                {
                    if (order.OrderType == OrderType.Limit && !IsStopLeg(order))
                        return order;
                }
            }
            return null;
        }

        // B131 DW-B138: test seam -- delegates to internal methods for xUnit test access.
        // InternalsVisibleTo("PropTraderTools.Tests") granted at top of file (L46).
        internal static bool SignalOrNameMatchesTestable(
            Order order,
            string? signalName,
            string? leaderName
        ) => SignalOrNameMatches(order, signalName, leaderName);

        // B135 DW-B146: PTT-prefix fallback -- after first drag, original ATM bracket is Cancelled;
        // replacement is "PTT-TGT-Drag" (target) or "PTT-STP-Drag" (stop).
        // FindFollowerBracketOrder must recognise these as the incumbent bracket on repeated drags.
        // CYC=5: base(1) + leaderName null(1) + name==(1) + !isStop&&TGT(1) + isStop&&STP(1) = 5.
        // JS-021: no lock (static, no shared state). JS-001: no throw. JS-002: returns bool.
        // ASCII-only. "PTT-TGT-Drag" and "PTT-STP-Drag" are ASCII.
        // B142: per-leg PTT name matching.
        // After first stop drag, ATM "Stop1" is replaced by "PTT-STP-Drag-1" (not generic "PTT-STP-Drag").
        // MatchesLeaderName extracts the trailing suffix char from leaderName (e.g. "Stop1" -> '1')
        // and builds the expected per-leg PTT name. This allows FindFollowerBracketOrder to locate
        // the correct per-leg order on concurrent second drags.
        // CYC=5: base(1) + null(1) + exact(1) + !isStop&&TGT(1) + isStop&&STP(1) = 5. Unchanged.
        private static bool MatchesLeaderName(Order order, string? leaderName, bool isStop)
        {
            if (leaderName == null) // (1) no constraint -- pass through
                return true;
            if (order.Name == leaderName) // (2) exact ATM name match
                return true;
            // B142: extract trailing digit suffix from leaderName ("Stop1"->'1', "Target2"->'2', etc.)
            // and match the per-leg PTT replacement order name.
            string legSuffix =
                leaderName.Length > 0 && char.IsDigit(leaderName[leaderName.Length - 1])
                    ? leaderName[leaderName.Length - 1].ToString()
                    : null;
            if (!isStop && legSuffix != null && order.Name == "PTT-TGT-Drag-" + legSuffix) // (3) replacement target match
                return true;
            if (isStop && legSuffix != null && order.Name == "PTT-STP-Drag-" + legSuffix) // (4) replacement stop match
                return true;
            return false;
        }

        // B135 DW-B146: test seam -- delegates to MatchesLeaderName for xUnit test access.
        // InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
        internal static bool MatchesLeaderNameTestable(
            Order order,
            string? leaderName,
            bool isStop
        ) => MatchesLeaderName(order, leaderName, isStop);

        // CYC=1. Pure predicate: returns true when currentPrice == newPrice (no price change occurred).
        // Used as early-return guard in SyncAtmFollowerBracket and SyncAtmFollowerTarget to suppress
        // spurious cancel+resubmit cycles caused by ARM events (DW-B147) or ChangeSubmitted races (DW-B149).
        // CYC=1: pure expression method body, no branches.
        // JS-021: no lock (static, no shared state). JS-001: no throw. JS-002: returns bool, not null.
        // JS-036: stack-only, zero allocation. ASCII-only. No DateTime. No FontFamily.
        private static bool IsNoPriceChange(double currentPrice, double newPrice) =>
            currentPrice == newPrice;

        // Test seam for xUnit access. InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
        internal static bool IsNoPriceChangeTestable(double currentPrice, double newPrice) =>
            IsNoPriceChange(currentPrice, newPrice);

        // B136 DW-B148: fused bracket-gate predicate -- replaces the sequential SignalOrNameMatches +
        // MatchesLeaderName guard pair in FindFollowerBracketOrder.
        // Signal path (signalName != null): exclusive signal-match only. Preserves original signal
        //   exclusivity -- orders from a different entry signal are rejected even if name matches.
        // ATM path (signalName == null): delegates to MatchesLeaderName, which passes exact ATM name
        //   (e.g. "Target3") AND PTT-prefix replacements ("PTT-TGT-Drag", "PTT-STP-Drag").
        //   This is the fix: PTT-TGT-Drag now reaches MatchesLeaderName and returns true.
        // CYC=2: base(1) + if(!string.IsNullOrEmpty(signalName))(1) = 2. Well within <= 8.
        // T3 B137 DW-B150: condition changed from (signalName != null) to (!string.IsNullOrEmpty(signalName)).
        // Empty string now routes to ATM path (MatchesLeaderName), not signal path.
        // Root cause fixed: leaderOrder.FromEntrySignal="" (NT8 ATM bracket state-transition event)
        //   was routing to signal path, comparing null == "" = FALSE, returning fo=NULL.
        //   After fix: !IsNullOrEmpty("") = false -> ATM path -> MatchesLeaderName -> Stop3 found.
        // JS-021: no lock (static, no shared state). JS-001: no throw. JS-002: returns bool.
        // ASCII-only. No DateTime. No FontFamily. No hex color literals.
        private static bool OrderPassesBracketGate(
            Order order,
            string? signalName,
            string? leaderName,
            bool isStop
        )
        {
            if (!string.IsNullOrEmpty(signalName)) // (1) signal path: non-empty only -- null OR "" = ATM path [T3 B137 DW-B150]
                return order.FromEntrySignal == signalName;
            return MatchesLeaderName(order, leaderName, isStop); // ATM path: exact name OR PTT-prefix
        }

        // B136 DW-B148: test seam -- delegates to OrderPassesBracketGate for xUnit test access.
        // InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
        internal static bool OrderPassesBracketGateTestable(
            Order order,
            string? signalName,
            string? leaderName,
            bool isStop
        ) => OrderPassesBracketGate(order, signalName, leaderName, isStop);

        internal Order? FindFollowerBracketOrderTestable(
            Account follower,
            string? fromEntrySignalName,
            bool isStop,
            string? leaderName = null
        ) => FindFollowerBracketOrder(follower, fromEntrySignalName, isStop, leaderName);

        // B133 LaneB DW-B143: list-injection test seam -- Account.Orders is sealed in NT8.
        // Accepts IEnumerable<Order> directly so xUnit tests can inject stub order lists.
        // InternalsVisibleTo("PropTraderTools.Tests") granted at top of file (L46).
        internal Order? FindFollowerBracketOrderTestable(
            IEnumerable<Order> orders,
            string? fromEntrySignalName,
            bool isStop,
            string? leaderName = null
        ) => FindFollowerBracketOrder(orders, fromEntrySignalName, isStop, leaderName);

        // B132 LaneA: test seams for DW-B141 helper methods.
        // InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
        internal static int DeriveLeaderBracketIndexTestable(Order? leaderOrder) =>
            DeriveLeaderBracketIndex(leaderOrder);

        internal static double FindLeaderStopPriceTestable(
            Account? leaderAccount,
            int bracketIndex
        ) => FindLeaderStopPrice(leaderAccount, bracketIndex);

        // B180 test seam: delegates to PttTickMath.RoundToTick (pure-math, no NT8 deps).
        // InternalsVisibleTo("PropTraderTools.Tests") at L46 grants test access.
        // CYC=1: straight-line delegation. JS-021: no lock. JS-001: no throw. ASCII-only.
        internal static double RoundToTickTestable(double raw, double tick) =>
            PttTickMath.RoundToTick(raw, tick);

        // CYC=2. Returns StopPrice for StopLimit orders, LimitPrice for all others.
        // NT8 fact: StopLimit.LimitPrice==0 always; drag price lives in StopPrice (Fact 1).
        // B66-LaneC: DW-B64-01 fix -- GetOrderPrice used in Gate C and HandleEntryChange.
        // JS-021: no lock. JS-001: no throw. Pure computation. Zero heap allocation (JS-036).
        private static double GetOrderPrice(Order order) =>
            order.OrderType == OrderType.StopLimit ? order.StopPrice : order.LimitPrice;

        // CYC=2. Sets StopPrice for StopLimit follower orders, LimitPrice for all others.
        // NT8: for Account.Change() on StopLimit, assign StopPrice not LimitPrice
        //   (NT8_FULL_REFERENCE.md lines 898-899, Fact 2).
        // B66-LaneC: DW-B64-01 fix -- SetFollowerPrice replaces direct fo.LimitPrice assignment.
        // JS-021: no lock. JS-001: no throw. Pure field assignment.
        private static void SetFollowerPrice(Order fo, double newPrice)
        {
            if (fo.OrderType == OrderType.StopLimit)
                fo.StopPrice = newPrice;
            else
                fo.LimitPrice = newPrice;
        }

        // CYC=3: foreach (1), instrument guard (2), state+type+name compound guard (3).
        // B66-LaneC: widened state to Working||Accepted, type to Limit||StopLimit (DW-B64-01).
        // HOTFIX-CLONE-DRAG: widened name to "PTT-Copy" OR "Entry" (Clone mode uses "Entry").
        // NT8: broker-simulated StopLimit may stay in Accepted (NT8_FULL_REFERENCE.md line 1005).
        // JS-002: returns null when not found -- callers must null-guard.
        private static Order? FindFollowerEntryOrder(Account follower, Instrument instrument)
        {
            foreach (var order in ActiveOrders(follower)) // (1) DW-NEW-09: terminal orders excluded
            {
                if (order.Instrument != instrument) // (2)
                    continue;
                if (
                    (
                        order.OrderState == OrderState.Working
                        || order.OrderState == OrderState.Accepted
                    ) // (3)
                    && (
                        order.OrderType == OrderType.Limit || order.OrderType == OrderType.StopLimit
                    )
                    && (order.Name == "PTT-Copy" || order.Name == "Entry")
                )
                    return order;
            }
            return null;
        }

        // B62/B66-LaneC/B67-LaneB: sync a leader entry drag to all follower working PTT-Copy entry orders.
        // B67-LaneB: DW-B67-02 -- replaced acc.Change() with Cancel+CreateOrder+Submit.
        //   acc.Change() on Apex/Rithmic is a silent broker-side no-op for pre-fill entry orders.
        //   Pattern from @2Custom PropagateMasterEntryMove (FIX-PM-02, FIX-PM-02b).
        //   NT8_FULL_REFERENCE.md lines 898-899: StopLimit price in StopPrice, not LimitPrice.
        //   limitPx = fo.OrderType == StopLimit ? 0 : newPrice
        //   stopPx  = fo.OrderType == StopLimit ? newPrice : 0
        // Triggered by Gate C when leader's entry orderId is already in dedup cache but price changed.
        // CYC=6: instr null(1) + tickSize ternary(2) + foreach acc(3) + acc null(4)
        //   + fo null(5) + price delta guard(6).
        // DW-NEW-08-D: order null guard removed -- DrainThenDispatch handles null internally.
        // JS-001: no throw in hot path. JS-021: no lock. JS-002: void.
        private void HandleEntryChange(Order leaderOrder, CopyRule rule)
        {
            var instrument = leaderOrder.Instrument;
            if (instrument == null) // (1)
                return;

            double tickSize = instrument.MasterInstrument?.TickSize ?? 0.0; // (2)
            double rawPrice = GetOrderPrice(leaderOrder); // B66-LaneC: StopLimit price in StopPrice
            double newPrice = tickSize > 0 ? Math.Round(rawPrice / tickSize) * tickSize : rawPrice;

            // HOTFIX-ENTRY-DRAG-DEDUP: keep leader orderId in cache at newPrice (upsert, not remove).
            // TryRemove caused Working-state re-entry to fall through Gate C into DispatchCopy,
            // placing a second PTT-Copy order (doubling follower contracts).
            // Keeping the key at newPrice means the Working event hits Gate C, sees delta=0 (price unchanged
            // since Accepted), and returns without dispatching. DispatchCopy's IsDedup also blocks it.
            _dedupCache[leaderOrder.OrderId.ToString()] = newPrice;

            foreach (var acc in rule.FollowerAccounts) // (3)
            {
                if (acc == null) // (4)
                    continue;

                var fo = FindFollowerEntryOrder(acc, instrument);
                if (fo == null) // (5)
                    continue;

                double currentPrice = GetOrderPrice(fo); // B66-LaneC: StopLimit price in StopPrice
                if (tickSize > 0 && Math.Abs(newPrice - currentPrice) < tickSize) // (6)
                    continue;

                // DW-NEW-08 Option D: cancel-before-dispatch drain instead of direct cancel+submit.
                // DrainThenDispatch cancels all Working/Accepted entries, parks intent in
                // _pendingDispatchDrains, submits via SubmitDrainedEntry when all cancels confirmed.
                // B200-QTY-01 FIX: use fo.Quantity + fo.Filled (original order size) not fo.Quantity alone.
                // In NT8, Order.Quantity = remaining unfilled qty. After a PartFill fo.Quantity < original.
                // fo.Quantity + fo.Filled = original submitted qty = the leader's intent.
                DrainThenDispatch(
                    acc,
                    instrument,
                    fo.Quantity + fo.Filled,
                    newPrice,
                    fo.OrderAction,
                    fo.OrderType
                );
            }
        }

        // CYC=2. Records (signal, follower) association in _orderMap for future bracket lookups.
        // JS-025: ConcurrentDictionary.GetOrAdd is atomic -- no lock needed.
        // Engineer Note #1: dedup guard prevents duplicate bindings on repeated Working state events.
        private void PopulateOrderMap(string fromEntrySignalName, Account followerAccount)
        {
            var bag = _orderMap.GetOrAdd(
                fromEntrySignalName,
                _ => new ConcurrentBag<FollowerBinding>()
            );
            // Dedup guard: prevent accumulating duplicate bindings on repeated Working state events
            if (!bag.Any(b => b.FollowerAccount?.Name == followerAccount?.Name)) // (1) branch
                bag.Add(new FollowerBinding(followerAccount, fromEntrySignalName));
        }

        // CYC=2. Fires PositionStateChanged only on states that open or grow a position.
        // FIX-B: Cancelled and Rejected REMOVED from the filter.
        // Rationale: when ATM brackets (Stop1/Stop2/Target1/Target2) cancel as part of closing
        // a trade, the position is already gone. TryFirePositionState fires hasPos=False hundreds
        // of times -- each one resets the BE button to Idle via HOTFIX-F3 in UpdateButtonColors.
        // Only Filled and PartFilled can open or grow a position. Cancelled/Rejected never do.
        // The flat signal is still correctly delivered: the Filled close event fires first with
        // hasPos=False (after position removal), so the button color is correct.
        // JS-003: PositionState readonly struct captured by value in event args (no aliasing).
        private void TryFirePositionState(OrderEventArgs e)
        {
            // Fire ONLY on Filled/PartFilled -- the only states that alter position quantity.
            var state = e.OrderState;
            if (state != OrderState.Filled && state != OrderState.PartFilled) // (1) branch
                return;

            if (e.Order?.Instrument?.FullName == null) // (1) branch
                return;

            string instr = e.Order.Instrument.FullName;
            bool hasPos = HasOpenPosition(e.Order.Account, e.Order.Instrument);

            // HOTFIX-B76-POSSTATE-DEDUP-01: Interlocked CAS dedup.
            // GetOrAdd returns the stable int[1] box for this instrument (allocates once).
            // Interlocked.Exchange atomically writes newVal and returns the prior value.
            // If prior == newVal: no transition -- another thread already wrote it, skip.
            // 0=False, 1=True, 2=unknown (initial sentinel -- always fires on first fill).
            int newVal = hasPos ? 1 : 0;
            var box = _lastHasPos.GetOrAdd(instr, _ => new int[] { 2 });
            int prior = System.Threading.Interlocked.Exchange(ref box[0], newVal);
            if (prior == newVal)
                return;

            // DW-B135: clear direction key when leader position goes flat.
            // Prevents false-positive IsReversalToFlatFollower on next entry after clean close.
            // DW-B128 preserved: during race window, hasPos=True, so this path not taken.
            // JS-021: TryRemove is lock-free. JS-001: no throw. CYC: 3->6 (three new branches).
            if (!hasPos)
            {
                bool isLeaderAcct = false;
                foreach (var r in _rules)
                {
                    if (e.Order.Account.Name == r.MasterAccount?.Name)
                    {
                        isLeaderAcct = true;
                        break;
                    }
                }
                if (isLeaderAcct)
                {
                    _lastLeaderDirection.TryRemove(instr, out _);
                    ClearLiveEntryForInstrument(instr); // DW-B142-MGC-02: safety-net cleanup on leader flat
                    // B212-REENTRY-BETARGET-V2: TryRemove moved to DispatchCopy (after all gates pass).
                    // Clearing here fired at the SAME fill that closes the position -- before NT8 ATM
                    // re-arms Entry -- so IsSuppressedReentry saw count=0 on the re-armed Entry. Bug.
                }
            }

            bool hasEntries = HasWorkingEntries(e.Order.Account, e.Order.Instrument);
            PositionStateChanged?.Invoke(instr, new PositionState(hasPos, hasEntries));
        }

        // DW-B135 test accessors -- no logic, thin shims only.
        internal void TryFirePositionState_ForTest(OrderEventArgs e) => TryFirePositionState(e);

        internal bool HasLeaderDirection(string instrFullName) =>
            _lastLeaderDirection.ContainsKey(instrFullName);

        internal void SetLeaderDirection_ForTest(string instrFullName, OrderAction action) =>
            _lastLeaderDirection[instrFullName] = action;

        internal ConcurrentDictionary<string, OrderAction> TestOnly_LastLeaderDirection =>
            _lastLeaderDirection;

        // B143 test seam -- no logic, thin shims only.
        // InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
        #region B143 test seam

        internal bool IsLiveEntryBlocked_ForTest(
            string instrKey,
            string orderId,
            double limitPrice
        ) => IsLiveEntryBlocked(instrKey, orderId, limitPrice);

        internal void EvictDedup_ForTest(string orderId, NinjaTrader.Cbi.OrderState state) =>
            EvictDedup(orderId, state);

        internal void ClearLiveEntryForInstrument_ForTest(string instrFullName) =>
            ClearLiveEntryForInstrument(instrFullName);

        internal bool LiveEntryInstrumentsContains_ForTest(string key) =>
            _liveEntryInstruments.ContainsKey(key);

        internal bool EntryInstrKeyByOrderIdContains_ForTest(string orderId) =>
            _entryInstrKeyByOrderId.ContainsKey(orderId);

        #endregion


        // HOTFIX-B66-COPY-REPLACE / HOTFIX-B66-NATIVE-ATM: re-places a cancelled follower entry order.
        // Called from pre-Gate-1 block when follower "PTT-Copy" or "Entry" is cancelled while leader is long.
        // Finds the copy rule where cancelledOrder.Account is a follower, verifies the leader
        // still has an open position (ATM-sweep cancel, not a user-initiated close cancel),
        // then re-fires SendCopy or SendCopyWithAtm at the same LimitPrice.
        // Named mode (Clone ATM): uses SendCopyWithAtm so re-placed order arms native ATM brackets.
        // All other modes: uses SendCopy ("PTT-Copy" bare Limit).
        // CYC=6: (1) !_isCopyEnabled guard, (2) foreach rules, (3) follower match loop,
        //        (4) follower found check, (5) IsReplaceOnAtmCancelBlocked extracted gate,
        //        (6) Named-mode branch.
        // B212-REENTRY-BETARGET-V4: extracted (5)(5b)(6)(7)+beCount into IsReplaceOnAtmCancelBlocked
        //   to hold CYC<=8. Root: ReplaceFollowerCopyOnAtmCancel bypassed IsSuppressedReentry entirely
        //   (not a DispatchCopy child), so _filledBeTargetCount was never checked. After BE-all fills
        //   (beCount>0) + flatten, leader re-arms ATM Entry. Old follower Entry gets ATM-sweep cancelled.
        //   This path fires SendCopyWithAtm spuriously. Guard: block when leader beCount>0.
        // JS-021: no lock. JS-001: no throw. JS-002: no return null (void).
        private void ReplaceFollowerCopyOnAtmCancel(Order cancelledOrder)
        {
            if (!_isCopyEnabled)
                return; // (1)
            CopyRule? matchedRule = null;
            int followerIndex = -1;
            foreach (var rule in _rules) // (2)
            {
                if (rule.Instrument != cancelledOrder.Instrument.FullName)
                    continue;
                for (int i = 0; i < (rule.FollowerAccounts?.Length ?? 0); i++) // (3)
                {
                    if (rule.FollowerAccounts[i]?.Name == cancelledOrder.Account.Name)
                    {
                        matchedRule = rule;
                        followerIndex = i;
                        break;
                    }
                }
                if (matchedRule.HasValue)
                    break;
            }
            if (!matchedRule.HasValue || followerIndex < 0)
                return; // (4) not a follower order
            var leader = matchedRule.Value.MasterAccount;
            if (leader == null)
                return;
            if (IsReplaceOnAtmCancelBlocked(leader, cancelledOrder))
                return; // (5) extracted: leader-flat + follower-has-pos + working-copy + flatten + beCount
            // Leader has open position, follower has NO position, no replacement in flight -- genuine ATM-sweep cancel, re-place the entry.
            int qty = (int)cancelledOrder.Quantity; // original qty already includes multiplier
            var signal = CopySignal.Create(
                cancelledOrder.OrderAction,
                OrderType.Limit,
                qty,
                cancelledOrder.LimitPrice,
                cancelledOrder.OrderId.ToString() + "-R"
            ); // "-R" suffix = replacement, avoids dedup collision
            var mode = ResolveAtmMode(matchedRule.Value, cancelledOrder.Account.Name);
            if (mode is FollowerAtmMode.Named namedAtm) // (6) Named -> native ATM re-place
            {
                SendCopyWithAtm(cancelledOrder.Account, cancelledOrder.Instrument, in signal, namedAtm);
            }
            else
                SendCopy(cancelledOrder.Account, cancelledOrder.Instrument, in signal, mode);
            StatusUpdate?.Invoke(
                cancelledOrder.Account.Name
                    + ": re-placed @ "
                    + cancelledOrder.LimitPrice
                    + " (ATM-sweep replace)"
            );
        }

        // B212-REENTRY-BETARGET-V4: extracted gate for ReplaceFollowerCopyOnAtmCancel.
        // CYC=5: (1) leader flat, (2) DW-B84-02 follower-has-pos, (3) HasWorkingPttCopy,
        //        (4) IsFlattenInFlightOrRecent, (5) HasFilledBeTargetFast leader beCount>0.
        // Returns true when the re-place must be suppressed.
        // HasFilledBeTargetFast is lock-free ConcurrentDictionary.TryGetValue. JS-021 compliant.
        // JS-001: no throw. JS-002: returns bool. ASCII-only. No lock().
        private bool IsReplaceOnAtmCancelBlocked(Account leader, Order cancelledOrder)
        {
            if (!HasOpenPosition(leader, cancelledOrder.Instrument))
                return true; // (1) leader flat = normal close cancel
            if (HasOpenPosition(cancelledOrder.Account, cancelledOrder.Instrument))
                return true; // (2) DW-B84-02: follower already has pos = NT8-internal ATM-arming cancel
            if (HasWorkingPttCopy(cancelledOrder.Account, cancelledOrder.Instrument))
                return true; // (3) drag-cancel: replacement already in flight
            if (IsFlattenInFlightOrRecent(cancelledOrder.Account, cancelledOrder.Instrument))
                return true; // (4) B200-CLOSE-LANED-01-V10: flatten in-flight or recent
            if (HasFilledBeTargetFast(leader))
                return true; // (5) B212-REENTRY-BETARGET-V4: BE-all fill window open for leader -- suppress re-place
            return false;
        }

        // DW-B79-08 v6: TryReplacePttBeBrackets -- register BE retry slot + 500ms fallback for ATM-sweep recovery.
        // Called from OnOrderUpdate when PTT-BE-Stop-* transitions to Cancelled while follower has position.
        // Root cause: StartAtmStrategy fires NT8's internal cancel sweep which wipes ALL working orders
        // including PTT-BE brackets placed by a prior BE-ALL.
        //
        // v1 FAILURE (938f0faf): called MoveStopToBreakEven directly -> runaway storm.
        // v2 FAILURE: slot+200ms timer -> timer consumed slot before Target1 Working.
        // v3 FAILURE: attempt guard stopped storm but 200ms timer still raced Target1 Working.
        //   Root: timer fires at 200ms with targets=0 (ATM still arming) -> places bare PTT-BE-Stop
        //   -> swept again -> attempt 2/3. Cycle repeats.
        // v4 FAILURE: added Target1..Target9 trigger to TryFireFollowerBeRetry; 200ms still raced.
        // v5 FAILURE: removed QueueBeRetryFallback entirely (slot-only).
        //   Root: new ATM brackets go Working BEFORE PTT-BE-Stop cancel arrives in OnOrderUpdate.
        //   TryFireFollowerBeRetry fires at t+0 (no slot) -> misses. Slot registered at t+1 (too late).
        //   Target1 already past Working state -- event not re-fired. Slot sits forever, no recovery.
        //   Confirmed by output: [BE-DIAG] attempt 1/3 ... no [BE-RETRY] Target1 Working ever appears.
        //
        // v6 FIX: slot-only registration + 500ms fallback.
        //   Event ordering: new ATM Target1 goes Working BEFORE PTT-BE-Stop cancel arrives.
        //   By 500ms the ATM is fully settled (not still arming as in v2/v3 at 200ms).
        //   TryFireFollowerBeRetry still claims the slot if somehow Target1 fires after slot
        //   registration (QX path or slow NT8). TryRemove atomic gate: exactly one path wins.
        //   500ms > ATM arming time (~50-100ms) so MoveStopToBreakEven(isRetry:true) sees Target1 Working.
        //
        // DW-B92: race-free alternative to HasFilledBeTarget.
        // Uses synchronous counter incremented in OnOrderUpdate before OCO cancel arrives.
        // CYC=2: (1) null guard, (2) count check. JS-021: no lock. ASCII-only.
        private bool HasFilledBeTargetFast(Account acc)
        {
            if (acc == null)
                return false; // (1)
            _filledBeTargetCount.TryGetValue(acc.Name, out int count);
            return count > 0; // (2)
        }

        // TryRecordBeTargetFill: DW-B92 -- record PTT-BE-Target-* fill before OCO partner cancel arrives.
        // CYC=4: (1) null guard, (2) state guard, (3) name null guard, (4) name prefix guard.
        // JS-021: _filledBeTargetCount.AddOrUpdate is lock-free. JS-001: no throw. JS-002: void.
        // Called BEFORE Gate 1 in OnOrderUpdate -- fires even when copy is disabled.
        // ASCII-only. No DateTime.Now. No lock().
        private void TryRecordBeTargetFill(Order o)
        {
            if (o == null)
                return; // (1)
            if (o.OrderState != OrderState.Filled)
                return; // (2)
            if (o.Name == null)
                return; // (3)
            if (!o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal))
                return; // (4)
            if (o.Account == null)
                return; // account guard -- AddOrUpdate requires non-null key
            _filledBeTargetCount.AddOrUpdate(o.Account.Name, 1, (_, prev) => prev + 1);
            _filledBeTargetCount.TryGetValue(o.Account.Name, out int afterCount);
            NinjaTrader.Code.Output.Process(
                "[BETARGET-RECORD] acct=" + o.Account.Name
                    + " name=" + o.Name
                    + " state=" + o.OrderState
                    + " countAfter=" + afterCount,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        }

        // B113 DW-B117: cancel-after cleanup. Called from OnOrderUpdate when any order
        // transitions to Working or Accepted. Cancels the native ATM Target* bracket that
        // corresponds to the PTT-QX-T* order that just confirmed, on follower accounts only.
        // DW-B122: PTT AddOn orders (account.CreateOrder + Submit) arrive as Accepted first
        // in NT8 SIM -- Working may never fire for them before OCO cancels the native bracket.
        // Guard now accepts both Working and Accepted, consistent with TryFireFollowerBeRetry
        // (CopyEngine.cs L1365-1368) which already handles the same order type.
        // CYC=5: (1) outer guard, (2) foreach, (3) if found, (4) if shouldRemove.
        // JS-021: no lock() -- ConcurrentDictionary TryGetValue/TryRemove.
        // JS-001: no throw. ASCII-only string literals. NT8-007: CancelOrder (not CreateOrder).
        internal void TryCleanupReArmedAtmBracket(OrderEventArgs e)
        {
            // (1) Compound guard -- all conditions must be true.
            // a. Order just went Working or Accepted (DW-B122: AddOn Limit orders arrive
            //    as Accepted first; guard accepts both states, same as TryFireFollowerBeRetry).
            // b. Name matches PTT-QX-T* pattern (PTT-QX-T1, T2, T3).
            // c. Account is a follower.
            // d. Cleanup entry exists for this account.
            // e. TTL has not elapsed.
            // f. Instrument matches the cleanup entry.
            if (
                (
                    e.Order.OrderState != OrderState.Working
                    && e.Order.OrderState != OrderState.Accepted
                )
                || e.Order.Name == null
                || !e.Order.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                || e.Order.Name.Length < 9
                || !char.IsDigit(e.Order.Name[8])
                || e.Order.Account == null
                || !IsFollowerAccount(e.Order.Account)
                || !_qxPendingFollowerCleanup.TryGetValue(e.Order.Account.Name, out var entry)
                || entry.Expiry <= DateTime.UtcNow
                || entry.Instr?.FullName != e.Order.Instrument?.FullName
            )
                return;

            char tChar = e.Order.Name[8]; // '1', '2', or '3'
            string nativeName = "Target" + tChar; // "Target1", "Target2", "Target3"
            var acc = e.Order.Account;

            // (2) Find the matching native ATM bracket on this account+instrument.
            // .ToList() snapshot -- consistent with acc.Orders read pattern at L2327.
            Order? toCancel = null;
            foreach (var o in acc.Orders.ToList()) // (2)
            {
                if (
                    o.Name == nativeName
                    && o.Instrument?.FullName == entry.Instr.FullName
                    && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted)
                )
                {
                    toCancel = o;
                    break;
                }
            }

            // (3) Cancel if found.
            if (toCancel != null) // (3)
            {
                acc.Cancel(new Order[] { toCancel });
                NinjaTrader.Code.Output.Process(
                    "[PTT-QX-CLEANUP] "
                        + acc.Name
                        + " cancelled "
                        + nativeName
                        + " (cancel-after DW-B117)",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }

            // (4) Removal policy: remove entry when T3 is processed (last bracket) or TTL elapsed.
            // T1 and T2 leave the entry in place so T2/T3 cleanups can fire.
            bool shouldRemove = tChar == '3' || entry.Expiry <= DateTime.UtcNow; // (4)
            if (shouldRemove)
                _qxPendingFollowerCleanup.TryRemove(acc.Name, out _);
        }

        // CYC=2. Thin wrapper over FindPosition.
        private bool HasOpenPosition(Account acc, Instrument instrument)
        {
            var pos = FindPosition(acc, instrument); // (1) branch
            if (pos == null)
                return false;
            return pos.Quantity > 0;
        }

        // IsPttBeStopActiveState: CYC=3. Active-state predicate for BE Stop/Target orders.
        // Returns true when order is in Working, Accepted, or Submitted state.
        // Reused by HasActivePttBeExits (Signal 2) and HasActivePttBeTargets (Signal 4).
        // JS-001: no throw. JS-002: returns bool (never null). JS-021: no lock. ASCII-only.
        private static bool IsPttBeStopActiveState(OrderState s)
        {
            return s == OrderState.Working    // (1)
                || s == OrderState.Accepted   // (2)
                || s == OrderState.Submitted; // (3)
        }

        // CYC=1. DW-B165: slot present = BE settle still in progress.
        // JS-021: ConcurrentDictionary.ContainsKey is lock-free.
        private bool IsBeSlotNonTerminal(string accName)
        {
            return _pendingFollowerBeSlots.ContainsKey(accName);
        }

        // CYC=3. DW-B166: flat follower with live retry-armed sentinel.
        // JS-021: IsFlat reads NT8 Position (thread-safe); ContainsKey is lock-free.
        private bool IsBeRetryArmedFlat(Account acc, Instrument instr)
        {
            if (acc == null || instr == null) // (1)
                return false;
            var pos = FindPosition(acc, instr);
            return IsFlat(pos) && _beReplaceAttempts.ContainsKey(acc.Name); // (2)(3)
        }

        // VIOLATION-01 extraction: merges Signal 5 (IsBeSlotNonTerminal) and Signal 6 (IsBeRetryArmedFlat)
        // into one parent branch in IsFollowerPositionBlocked. Keeps parent CYC=8.
        // CYC=3 (standard McCabe: base(1) + if-slot(1) + if-retry(1)).
        // JS-001: no throw. JS-021: no lock. ASCII-only.
        private bool IsBeSettleOrRetrySignalActive(Account acc, Instrument instr)
        {
            if (IsBeSlotNonTerminal(acc.Name)) // (1)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "
                        + acc.Name + "\n" + instr.FullName,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return true;
            }
            if (IsBeRetryArmedFlat(acc, instr)) // (2)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-COPY-GUARD] ghost-entry blocked (be-retry-armed-flat): "
                        + acc.Name + "\n" + instr.FullName,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return true;
            }
            return false;
        }

        // DW-B160/B162-B: Signal 2 of IsFollowerPositionBlocked.
        // Matches bare "PTT-BE-Stop" AND suffixed "PTT-BE-Stop-1" etc. (no trailing dash per B162-B).
        // CYC=8 -- AT LIMIT. JS-021: ToList snapshot, no lock. JS-002: false on all guard exits.
        internal bool HasActivePttBeExits(Account acc, Instrument instr)
        {
            if (acc == null)   return false; // (1)
            if (instr == null) return false; // (2)
            foreach (var o in acc.Orders.ToList()) // (3)
            {
                if (o.Name == null || !o.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)) continue; // (4)
                if (o.Instrument?.FullName != instr.FullName) continue; // (5)
                if (IsPttBeStopActiveState(o.OrderState)) return true; // (6)(7)(8)
            }
            return false;
        }

        // DW-B164: Signal 4 of IsFollowerPositionBlocked.
        // Matches "PTT-BE-Target-*" orders in Working/Accepted/Submitted state.
        // Closes B164 gap: Stop filled -> Target still Working -> Signals 1-3 all false -> ghost entry.
        // CYC=8 -- AT LIMIT. JS-021: ToList snapshot, no lock. JS-002: false on all guard exits.
        internal bool HasActivePttBeTargets(Account acc, Instrument instr)
        {
            if (acc == null)   return false; // (1)
            if (instr == null) return false; // (2)
            foreach (var o in acc.Orders.ToList()) // (3)
            {
                if (o.Name == null || !o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)) continue; // (4)
                if (o.Instrument?.FullName != instr.FullName) continue; // (5)
                if (IsPttBeStopActiveState(o.OrderState)) return true; // (6)(7)(8)
            }
            return false;
        }

        // DW-B160: Signal 3 of IsFollowerPositionBlocked.
        // Matches "PTT-BE-Target-*" in CancelPending or CancelSubmitted state.
        // Covers post-OCO window: Stop filled, Target OCO-triggered to cancel (CancelPending).
        // CYC=7: base(1) + acc null(1) + instr null(1) + foreach(1) + name guard(1) + instr match(1) + state OR(1).
        // JS-021: ToList snapshot, no lock. JS-002: false on all guard exits.
        internal bool HasSettlingBeTargets(Account acc, Instrument instr)
        {
            if (acc == null)   return false; // (1)
            if (instr == null) return false; // (2)
            foreach (var o in acc.Orders.ToList()) // (3)
            {
                if (o.Name == null || !o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)) continue; // (4)
                if (o.Instrument?.FullName != instr.FullName) continue; // (5)
                if (o.OrderState == OrderState.CancelPending
                    || o.OrderState == OrderState.CancelSubmitted) return true; // (6)(7)
            }
            return false;
        }

        // IsFollowerPositionBlocked: CYC=8. 6-signal ghost-entry guard.
        // Signal 1: HasOpenPosition (B159 -- follower has open position)
        // Signal 2: HasActivePttBeExits (B160/B162-B -- PTT-BE-Stop* Working/Accepted/Submitted)
        // Signal 3: HasSettlingBeTargets (B160 -- PTT-BE-Target-* CancelPending/CancelSubmitted)
        // Signal 4: HasActivePttBeTargets (B164 -- PTT-BE-Target-* Working/Accepted/Submitted)
        // Signals 5+6: IsBeSettleOrRetrySignalActive (VIOLATION-01 extract; B165+B166)
        // JS-021: all sub-helpers use ToList snapshot or ConcurrentDictionary reads (no lock).
        // JS-001: no throw. JS-002: returns false on all null/guard exits. ASCII-only.
        // AT LIMIT: CYC=8. No further branches may be added without prior helper extraction.
        internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)
        {
            if (acc == null)   return false; // (1)
            if (instr == null) return false; // (2)
            if (HasOpenPosition(acc, instr))        // (3) Signal 1
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-COPY-GUARD] ghost-entry blocked (open-position): "
                        + acc.Name + "\n" + instr.FullName,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return true;
            }
            if (HasActivePttBeExits(acc, instr))    // (4) Signal 2
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-COPY-GUARD] ghost-entry blocked (active-be-exits): "
                        + acc.Name + "\n" + instr.FullName,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return true;
            }
            if (HasSettlingBeTargets(acc, instr))   // (5) Signal 3
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-COPY-GUARD] ghost-entry blocked (settling-be-targets): "
                        + acc.Name + "\n" + instr.FullName,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return true;
            }
            if (HasActivePttBeTargets(acc, instr))  // (6) Signal 4
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-COPY-GUARD] ghost-entry blocked (active-be-targets): "
                        + acc.Name + "\n" + instr.FullName,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return true;
            }
            if (IsBeSettleOrRetrySignalActive(acc, instr)) // (7) Signals 5+6 (VIOLATION-01 extract)
                return true;
            return false;
        }



        // B65 T1 / DW-B91-B: TryDispatchLeaderFlat -- CYC=6 (strict McCabe after DW-B91-B extraction).
        // (1) state guard, (2) follower guard, (3) open-position race-safe guard, (4) foreach follower.
        // DW-B91-B: foreach body extracted to FlattenFollower (CYC=3) which adds per-follower
        // hasOpenPosition guard to skip already-flat followers. Null guard moved into FlattenFollower.
        // Guard (3) change: bypass hasOpenPosition when orderName is a native NT8 exit.
        // Rationale: NT8_FULL_REFERENCE.md line 1721 -- position state is not updated until the next
        // OnBarUpdate() after an order fill. When leader fills a native close order (Name="Close",
        // "Flatten", "Exit*", "Rev*"), position still shows open even though the order is filled.
        // Bypassing the guard here ensures followers are flattened immediately (DW-B65-01 fix).
        // JS-021: no lock. JS-001: no throw. JS-002: no null return.
        private static bool TryDispatchLeaderFlat(
            Account account,
            Instrument instrument,
            OrderState state,
            string orderName,
            CopyRule rule,
            Func<Account, bool> isFollower,
            Func<Account, Instrument, bool> hasOpenPosition,
            Action<Account, Instrument> flattenOne
        )
        {
            if (state != OrderState.Filled && state != OrderState.Cancelled)
                return false; // (1)
            if (isFollower(account))
                return false; // (2)
            if (IsNonFlatDispatchName(orderName))
                return false; // (2.5+2.6) combines HOTFIX-B63-FLATTEN-01 + HOTFIX-B64-ENTRY-FLATTEN-01
            if (!IsNativeExitName(orderName) && hasOpenPosition(account, instrument))
                return false; // (3)
            foreach (var acc in rule.FollowerAccounts) // (4)
                FlattenFollower(acc, instrument, hasOpenPosition, flattenOne); // DW-B91-B
            return true;
        }

        // DW-B91-B: extracted foreach body from TryDispatchLeaderFlat.
        // Absorbs (a) null guard (moved from caller loop) and (b) new per-follower open-position guard.
        // Prevents spurious flattenOne call on already-flat followers (re-entry bug).
        // CYC=3: 1 base + if (acc == null) + if (!hasOpenPosition).
        // JS-021: no lock. JS-001: no throw. JS-002: no null return (void).
        // private static: no instance state captured -- explicit delegate injection for testability.
        private static void FlattenFollower(
            Account acc,
            Instrument instrument,
            Func<Account, Instrument, bool> hasOpenPosition,
            Action<Account, Instrument> flattenOne
        )
        {
            if (acc == null)
                return; // (a) null guard (moved from caller)
            if (!hasOpenPosition(acc, instrument))
                return; // (b) DW-B91-B: skip already-flat follower
            flattenOne(acc, instrument);
        }

        // CYC=3. Returns true if any working non-bracket order exists for the instrument.
        private bool HasWorkingEntries(Account acc, Instrument instrument)
        {
            foreach (var order in acc.Orders) // (1) branch
            {
                if (order.Instrument != instrument) // (1) branch
                    continue;
                if (order.OrderState != OrderState.Working) // (1) branch
                    continue;
                if (!IsBracketLeg(order))
                    return true;
            }
            return false;
        }

        // HOTFIX-B66-COPY-REPLACE-FIX: discriminates ATM-sweep cancel from entry-drag cancel.
        // Entry drag: HandleEntryChange places a new PTT-Copy (Working/Accepted/Submitted) before
        // this Cancelled event arrives in OnOrderUpdate. ATM-sweep: all follower orders wiped,
        // nothing is Working/Accepted/Submitted for this account+instrument after the sweep.
        // CYC=3: foreach(1) + state check(2) + name check(3).
        // NT8: FullName string compare (reference equality banned -- HOTFIX-BUG-BE-INSTRUMENT-REF).
        // JS-021: no lock. acc.Orders.ToList() snapshot prevents InvalidOperationException.
        private bool HasWorkingPttCopy(Account acc, Instrument instrument)
        {
            foreach (var order in acc.Orders.ToList())
            {
                if (order.Instrument?.FullName != instrument.FullName)
                    continue;
                if (
                    order.OrderState != OrderState.Working
                    && order.OrderState != OrderState.Accepted
                    && order.OrderState != OrderState.Submitted
                )
                    continue;
                if (order.Name == "PTT-Copy" || order.Name == "Entry")
                    return true;
            }
            return false;
        }

        // B8 T2: SendCopy -- mode dispatch (CYC=5).
        // signalName is ALWAYS "PTT-Copy" for ALL modes -- PTT- prefix invariant never violated.
        // For Named mode the ATM template name is passed as the final 'atm' parameter of CreateOrder.
        // JS-001: catch logs and returns false -- no throw in dispatch path.
        // JS-002: mode=null treated as Inherit (no null return, no throw).
        // B23 T1: Dispatcher marshal added
        private bool SendCopy(
            Account follower,
            Instrument instrument,
            in CopySignal signal,
            FollowerAtmMode mode
        )
        {
            OrderType orderType = signal.Type;
            double limitPrice = signal.LimitPrice;
            string signalName = "PTT-Copy"; // SCAN-05: PTT- prefix mandatory for ALL modes

            if (mode is FollowerAtmMode.Market) // branch (1)
            {
                orderType = OrderType.Market;
                limitPrice = 0;
            }
            // Inherit: use original signal values unchanged (no branch needed)

            string atmTemplate = mode is FollowerAtmMode.Named named // branch (2)
                ? named.TemplateName
                : null;

            try // branch (3)
            {
                // NT8 AddOn constraint: 12-arg CreateOrder requires CustomOrder as arg12, not string.
                // Named ATM mode is not applicable from AddOn context -- pass null CustomOrder.
                // B23 T1 (DW-B22-NULLREF-01): NullRef on non-active-chart accounts caught here.
                // Dispatcher.InvokeAsync not reliably available in NT8 AddOn context (NT8-042).
                // Try/catch is the safe fallback: logs the error, returns false, does not crash.
                // B57 fix (DW-B57-01): capture order and call Submit() -- CreateOrder alone leaves
                // the order at Initialized state; Submit() sends it to the exchange (-> Working).
                var order = follower.CreateOrder(
                    instrument,
                    signal.Action,
                    orderType,
                    OrderEntry.Manual,
                    TimeInForce.Gtc, // B29 fix: Day orders expire mid-session on overnight futures
                    signal.Quantity,
                    limitPrice,
                    0,
                    null,
                    signalName,
                    DateTime.MaxValue,
                    (NinjaTrader.Cbi.CustomOrder)null
                );
                if (order != null)
                {
                    follower.Submit(new[] { order });
                    RecordFollowerCopy(signal.OrderId, order); // DW-B136 Gap B: track follower order by leader ID
                }
                return true;
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke("PTT-Copy error: " + ex.Message);
                return false;
            }
        }

        // HOTFIX-B66-NATIVE-ATM: SendCopyWithAtm -- submits follower entry order with native NT8 ATM.
        // Called from DispatchCopy (Named mode) and ReplaceFollowerCopyOnAtmCancel (Named mode).
        // Uses StartAtmStrategy (static, callable from AddOnBase -- confirmed NT8_FULL_REFERENCE.md).
        // NT8 CONSTRAINT: order name MUST be "Entry" for StartAtmStrategy to arm brackets.
        // NT8 CONSTRAINT: StartAtmStrategy handles submission -- do NOT call follower.Submit() after.
        // HOTFIX-B66-ATM-OBJ: AtmObject overload preferred -- passes live object, avoids .Name class-name trap.
        // CYC=4: (1) try/catch outer, (2) order null guard, (3) AtmObject branch, (4) string branch.
        // B199-R1: T2b H2 divide removed. H2 was dividing signal.Quantity by per-group qty (wrong denominator --
        //   <Quantity> XML node = contracts-per-group, NOT group count). Result: qty=10/3=3 instead of 10.
        //   SIM evidence (DW-B198-QTY-SIM-01) not yet collected. Pass signal.Quantity directly (H1 path).
        //   If SIM confirms H2, fix must use group COUNT (number of <BracketOrder> nodes), not per-group qty.
        // T3: return type changed bool -> Order? so SubmitDrainedEntry can store the created order reference.
        // JS-021: no lock. JS-001: catch logs, returns null. JS-002: null is NT8 absence signal (exempt per plan review).
        private Order? SendCopyWithAtm(
            Account follower,
            Instrument instrument,
            in CopySignal signal,
            FollowerAtmMode.Named namedMode
        )
        {
            try // (1)
            {
                // B199-DIAG: direct output log so ATM dispatch is visible regardless of StatusUpdate subscription.
                NinjaTrader.Code.Output.Process(
                    "[PTT-ATM-SEND] acct=" + follower.Name
                        + " qty=" + signal.Quantity
                        + " tmpl=" + namedMode.TemplateName
                        + " obj=" + (namedMode.AtmObject != null ? "set" : "null"),
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                StatusUpdate?.Invoke(
                    follower.Name + ": ATM-send qty=" + signal.Quantity + " tmpl=" + namedMode.TemplateName);
                // B199-R1: pass qty directly -- H2 divide removed (wrong denominator, DW-B198-QTY-SIM-01).
                int qtyToSend = signal.Quantity;
                var order = follower.CreateOrder(
                    instrument,
                    signal.Action,
                    OrderType.Limit,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    qtyToSend,
                    signal.LimitPrice,
                    0,
                    string.Empty,
                    "Entry", // NT8: MUST be "Entry" for StartAtmStrategy to arm
                    DateTime.MaxValue,
                    (NinjaTrader.Cbi.CustomOrder)null
                );
                if (order == null)
                    return null; // (2) T3: return null instead of false
                StatusUpdate?.Invoke(
                    follower.Name + ": ATM-order qty=" + order.Quantity);
                if (namedMode.AtmObject != null) // (3) preferred: object overload
                    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.AtmObject, order);
                else // (4) fallback: string overload
                    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.TemplateName, order);
                RecordFollowerCopy(signal.OrderId, order); // DW-B136 Gap B
                StatusUpdate?.Invoke(
                    follower.Name
                        + ": PTT-ATM entry @ "
                        + signal.LimitPrice
                        + " atm="
                        + namedMode.TemplateName
                );
                return order; // T3: return the created order on success
            }
            catch (Exception ex)
            {
                // DR-7: surface exception to Output tab -- StatusUpdate may not be subscribed in all sessions.
                NinjaTrader.Code.Output.Process(
                    "[PTT-ATM-ERROR] " + ex.GetType().Name + ": " + ex.Message,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                StatusUpdate?.Invoke("PTT-ATM error: " + ex.Message);
                return null; // T3: return null on exception
            }
        }

        // B8 T2: GetAtmMode -- bounds-safe ATM mode retrieval. CYC=2.
        // Returns Inherit if accountName not found -- never null, never throws (JS-001, JS-002).
        private static FollowerAtmMode GetAtmMode(CopyRule rule, string accountName)
        {
            FollowerAtmMode mode;
            if (rule.FollowerAtmTemplates.TryGetValue(accountName ?? string.Empty, out mode)) // branch (1)
                return mode;
            return new FollowerAtmMode.Inherit();
        }

        // B50 -- ResolveAtmMode: CYC=2. Mode-aware ATM dispatch router.
        // Clone mode uses shared _cloneAtmCache; Signal/Mirror modes delegate to GetAtmMode (per-rule).
        // Replaces direct GetAtmMode call in DispatchCopy inner loop.
        // JS-002: never returns null -- all branches return a FollowerAtmMode subtype.
        private FollowerAtmMode ResolveAtmMode(CopyRule rule, string accountName)
        {
            if (GetCopyMode() == CopyMode.Clone) // branch (1)
                return GetCloneAtmMode();
            return GetAtmMode(rule, accountName);
        }

        // B8 T2: ParseAtmModeName -- deserializes "Inherit"|"Market"|"Named:XXX" to FollowerAtmMode.
        // CYC=3. Returns Inherit for null/empty/unrecognized input -- never null, never throws.
        internal static FollowerAtmMode ParseAtmModeName(string name)
        {
            if (string.IsNullOrEmpty(name)) // branch (1)
                return new FollowerAtmMode.Inherit();
            if (name == "Market") // branch (2)
                return new FollowerAtmMode.Market();
            if (name.StartsWith("Named:")) // branch (3)
                return new FollowerAtmMode.Named(name.Substring(6));
            return new FollowerAtmMode.Inherit();
        }

        // B8 T2: AtmModeToString -- serializes FollowerAtmMode to "Inherit"|"Market"|"Named:XXX".
        // CYC=3. Sealed hierarchy is exhaustive -- all variants covered.
        internal static string AtmModeToString(FollowerAtmMode mode)
        {
            if (mode is FollowerAtmMode.Market) // branch (1)
                return "Market";
            if (mode is FollowerAtmMode.Named namedMode) // branch (2)
                return "Named:" + namedMode.TemplateName;
            return "Inherit"; // branch (3): Inherit or null-fallback
        }

        // B8 T2: SetAtmMode -- post-create mutation of a single follower's ATM mode.
        // ConcurrentBag rebuild pattern -- no lock (JS-021).
        // ImmutableDictionary.SetItem returns a NEW dictionary (immutable -- no mutation).
        internal void SetAtmMode(
            string instrument,
            string followerAccountName,
            FollowerAtmMode mode
        )
        {
            var snapshot = new List<CopyRule>(_rules);
            _rules = new ConcurrentBag<CopyRule>();
            foreach (var r in snapshot)
            {
                if (r.Instrument != instrument)
                {
                    _rules.Add(r);
                    continue;
                }
                var newMap = new Dictionary<string, FollowerAtmMode>(r.FollowerAtmTemplates);
                newMap[followerAccountName] = mode;
                _rules.Add(
                    CopyRule.Create(
                        r.Instrument,
                        r.MasterAccount,
                        r.FollowerAccounts,
                        r.Enabled,
                        r.FollowerMultipliers,
                        newMap,
                        r.TightenTicks,
                        r.FollowerAccountNames // B127: preserve names through ATM mode rebuild
                    )
                );
            }
        }

        // B215-DW-B212-01: leader-skip NOT needed for Trim path.
        // TrimOneAccount calls acc.CreateOrder (PTT-Trim partial market sell) only.
        // No CancelAllAccountOrders -> no ATM bracket cancel -> no NT8 ATM re-arm risk.
        // DW-B212-01 CLOSED-NO-ACTION. Compare: Flatten uses HF-08 leader-skip (CancelAllAccountOrders present).
        internal void Trim(Instrument instrument)
        {
            if (!_flags.TrimFlatten)
            {
                StatusUpdate?.Invoke("Trim/Flatten requires Pro tier");
                return;
            }
            foreach (var acc in AllAccounts(instrument))
                TrimOneAccount(acc, instrument);
        }

        // B212-FLATTEN-REENTRY: skip leader account in the RelayFlatten path.
        // PttFlatten.Execute(ctx) already submitted a market close for the leader BEFORE raising PttBus.FlatFired.
        // RelayFlatten -> Flatten(Instrument) -> FlattenOneAccount(leader) -> CancelAllAccountOrders(leader)
        //   cancels native ATM brackets (Target1/SimStop1) -> NT8 ATM re-arms Entry -> copier copies it = spurious re-entry.
        // Fix: skip the leader in this overload. Followers still get FlattenOneAccount via AllAccounts skip.
        // Leader's position close is already in-flight from PttFlatten.Execute(ctx) market order.
        internal void Flatten(Instrument instrument)
        {
            if (!_flags.TrimFlatten)
            {
                StatusUpdate?.Invoke("Trim/Flatten requires Pro tier");
                return;
            }
            var rule = FindRule(instrument);
            var leader = rule?.MasterAccount;
            FlatGuardLoop(
                () => AllAccounts(instrument).Where(acc => leader == null || acc.Name != leader.Name),
                acc => { var p = FindPosition(acc, instrument); return p == null || p.Quantity == 0; },
                acc => FlattenOneAccount(acc, instrument)
            );
        }

        // B28 T1 -- Trim(Account,Instrument): leader-account overload. Fixes DW-B28-02.
        // CYC=4: (1) leader null guard, (2) leader direct call, (3) foreach, (4) acc==leader skip. BGTM-1: TrimFlatten gate CYC=5.
        internal void Trim(Account leader, Instrument instrument)
        {
            if (!_flags.TrimFlatten)
            {
                StatusUpdate?.Invoke("Trim/Flatten requires Pro tier");
                return;
            }
            if (leader == null)
            {
                StatusUpdate?.Invoke("PTT-Trim: leader null -- skipping");
                return;
            }
            TrimOneAccount(leader, instrument);
            foreach (var acc in AllAccounts(instrument))
            {
                if (acc == leader)
                    continue;
                TrimOneAccount(acc, instrument);
            }
        }

        // B28 T1 -- Flatten(Account,Instrument): leader-account overload. Fixes DW-B28-02.
        // CYC=4: (1) leader null guard, (2) leader direct call, (3) foreach, (4) acc==leader skip. BGTM-1: TrimFlatten gate CYC=5.
        internal void Flatten(Account leader, Instrument instrument)
        {
            if (!_flags.TrimFlatten)
            {
                StatusUpdate?.Invoke("Trim/Flatten requires Pro tier");
                return;
            }
            if (leader == null)
            {
                StatusUpdate?.Invoke("PTT-Flatten: leader null -- skipping");
                return;
            }
            FlattenOneAccount(leader, instrument);
            FlatGuardLoop(
                () => AllAccounts(instrument).Where(acc => acc != leader),
                acc => { var p = FindPosition(acc, instrument); return p == null || p.Quantity == 0; },
                acc => FlattenOneAccount(acc, instrument)
            );
        }

        // B28 T1 -- CancelPendingEntries(Account,Instrument): leader-account overload. Fixes DW-B28-02.
        // CYC=4: (1) leader null guard, (2) leader direct call, (3) foreach, (4) acc==leader skip. BGTM-1: TrimFlatten gate CYC=5.
        internal void CancelPendingEntries(Account leader, Instrument instrument)
        {
            if (!_flags.TrimFlatten)
            {
                StatusUpdate?.Invoke("Trim/Flatten requires Pro tier");
                return;
            }
            if (leader == null)
            {
                StatusUpdate?.Invoke("PTT-Cancel: leader null -- skipping");
                return;
            }
            CancelOneAccount(leader, instrument);
            foreach (var acc in AllAccounts(instrument))
            {
                if (acc == leader)
                    continue;
                CancelOneAccount(acc, instrument);
            }
        }

        // B28 T1 -- Trim(Account,Instrument,int,double,double): leader-account limit overload.
        // CYC=5: (1) leader null guard, (2) ask/bid/buffer guard, (3) leader direct call, (4) foreach, (5) acc==leader skip.
        internal void Trim(
            Account leader,
            Instrument instrument,
            int exitBuffer,
            double ask,
            double bid
        )
        {
            if (leader == null)
            {
                StatusUpdate?.Invoke("PTT-TrimLimit: leader null -- skipping");
                return;
            }
            if (ask <= 0 || bid <= 0 || exitBuffer == 0)
            {
                Trim(leader, instrument);
                return;
            }
            TrimOneAccountLimit(leader, instrument, exitBuffer, ask, bid);
            foreach (var acc in AllAccounts(instrument))
            {
                if (acc == leader)
                    continue;
                TrimOneAccountLimit(acc, instrument, exitBuffer, ask, bid);
            }
        }

        // B28 T1 -- Flatten(Account,Instrument,int,double,double): leader-account limit overload.
        // CYC=5: (1) leader null guard, (2) ask/bid/buffer guard, (3) leader direct call, (4) foreach, (5) acc==leader skip.
        internal void Flatten(
            Account leader,
            Instrument instrument,
            int exitBuffer,
            double ask,
            double bid
        )
        {
            if (leader == null)
            {
                StatusUpdate?.Invoke("PTT-FlattenLimit: leader null -- skipping");
                return;
            }
            if (ask <= 0 || bid <= 0 || exitBuffer == 0)
            {
                Flatten(leader, instrument);
                return;
            }
            FlattenOneAccountLimit(leader, instrument, exitBuffer, ask, bid);
            foreach (var acc in AllAccounts(instrument))
            {
                if (acc == leader)
                    continue;
                FlattenOneAccountLimit(acc, instrument, exitBuffer, ask, bid);
            }
        }

        // B28 T1 -- TrimOneAccount: per-account market trim helper. CYC=3.
        // (1) pos null/qty guard, (2) action ternary, (3) try/catch CreateOrder.
        // JS-001: no rethrow. JS-021: no lock. ASCII: PTT-Trim signal name.
        private void TrimOneAccount(Account acc, Instrument instrument)
        {
            var pos = FindPosition(acc, instrument);
            if (pos == null || pos.Quantity == 0)
            {
                StatusUpdate?.Invoke(acc.Name + ": flat skip");
                return;
            }
            int trimQty = (int)Math.Ceiling(pos.Quantity / 2.0);
            var action =
                pos.MarketPosition == MarketPosition.Long
                    ? OrderAction.Sell
                    : OrderAction.BuyToCover;
            try
            {
                acc.CreateOrder(
                    instrument,
                    action,
                    OrderType.Market,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    trimQty,
                    0,
                    0,
                    null,
                    "PTT-Trim",
                    DateTime.MaxValue,
                    null
                );
                StatusUpdate?.Invoke(acc.Name + ": trim " + trimQty);
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke("PTT-Trim error: " + ex.Message);
            }
        }

        // B28 T1 -- FlattenOneAccount: per-account market flatten helper.
        // B67 DW-B67-01: cancel follower ATM+QX brackets BEFORE submitting market order.
        // B69 DW-B69-01: widened from CancelQxBrackets to cancel ALL active orders (name-agnostic).
        // B76 HOTFIX-B76-FLATTEN-RACE-01: re-read position after CancelAllAccountOrders.
        //   NT8_FULL_REFERENCE.md line 1721: position state lags until next OnBarUpdate after fill.
        //   Pre-cancel read (pos): fast-exit guard -- skip cancel round-trip on already-flat accounts.
        //   Post-cancel read (posAfterCancel): race guard -- after CancelAllAccountOrders round-trips
        //   to NT8 order manager, acc.Positions reflects ATM bracket fills. If posAfterCancel shows
        //   flat, account was closed by bracket fill -- skip PTT-Flatten to prevent inversion.
        // NT8 precedent: @2Custom-0909edcc FlattenPositionByName V8.31 comment:
        //   "Cancel ALL bracket orders first to prevent race conditions."
        // Rithmic/Apex: incoming market order conflicts with live OCO bracket at broker layer
        //   -> "Close operation failed. Operation timed out." without this cancel step.
        // CYC=7: (1) active PTT-Flatten guard, (2) B194-F4 debounce guard,
        //        (3) pre-cancel pos null/qty guard, (4) CancelAllAccountOrders,
        //        (5) post-cancel re-read null/qty guard, (6) action ternary, (7) try/catch.
        // JS-021: no lock. JS-001: no throw in hot path. JS-002: void.
        private void FlattenOneAccount(Account acc, Instrument instrument)
        {
            // B76 HOTFIX-B76-FLATTEN-GUARD-01 v2: order-book guard.
            // Field flag (v1) was cleared in finally before NT8 delivered bracket-cancel callbacks.
            // Root cause: CancelAllAccountOrders returns immediately; bracket-cancel acks fire on
            // the NT8 account thread after finally runs, re-entering with the flag already cleared.
            // Fix: scan acc.Orders for an active PTT-Flatten -- NT8 order book is the authoritative
            // in-flight signal. An active flatten is already working; subsequent cancel-ack callbacks
            // skip. acc.Orders.ToList() snapshot prevents InvalidOperationException (same pattern as
            // HasWorkingPttCopy). JS-021: no lock.
            foreach (var o in acc.Orders.ToList())
            {
                if (o.Name != "PTT-Flatten")
                    continue;
                if (o.Instrument?.FullName != instrument.FullName)
                    continue;
                if (
                    o.OrderState == OrderState.Initialized // B205-FIX-B: closes race window between Submit() and Submitted state
                    || o.OrderState == OrderState.Submitted
                    || o.OrderState == OrderState.Accepted
                    || o.OrderState == OrderState.Working
                    || o.OrderState == OrderState.PartFilled // FIX B200-CLOSE-LANED-01-V2: mirrors HasActivePttFlatten
                )
                {
                    StatusUpdate?.Invoke(acc.Name + ": flat-guard: in-flight skip");
                    return;
                }
            }
            // B200-CLOSE-LANED-01-V13: set anchor sentinel FIRST, then set flag.
            // The sentinel (acc.Name+"#pending") can never match a real NT8 OrderId (GUIDs only),
            // so any stale Filled/Cancelled events that arrive during the window between flag-set
            // and real-OrderId-anchor-set will fail the anchor check and leave the flag true.
            // Sentinel is overwritten with the real OrderId after acc.Submit() succeeds.
            // JS-021: ConcurrentDictionary indexer writes are lock-free. JS-001: no throw. ASCII-only.
            _flattenInFlightOrderId[acc.Name] = acc.Name + "#pending";
            _flattenInFlight[acc.Name] = true;
            // B194-F4: belt-and-suspenders debounce. Skip if flatten was submitted within 500ms.
            // Prevents PTT-Flatten fill -> NakedDetect -> FlattenOneAccount re-entry loop.
            // Pattern matches _nakedDetectLastQueuedTicks at NakedPositionDetector line 6479.
            long flattenNow = (long)(int)Environment.TickCount;
            if (flattenNow - _flattenFilledTicks.GetOrAdd(acc.Name, 0L) < 500L)
            {
                StatusUpdate?.Invoke(acc.Name + ": flat-debounce skip");
                return;
            }
            var pos = FindPosition(acc, instrument);
            if (pos == null || pos.Quantity == 0)
            {
                StatusUpdate?.Invoke(acc.Name + ": flat skip");
                return;
            }
            TryMarkFollowerSlateNotReady(acc); // BUG2: flatten dispatch imminent
            // B204-LANE-E-FLATTEN-PERMANENT-LATCH-01: stamp flatten-latch to permanently block Lane E.
            // Policy: flatten = exit. Re-entry must only come from a new leader entry dispatch (Lane D).
            // Latch key mirrors _laneEDispatched schema: acc.Name + "#LANE-E#" + instrFullName.
            // JS-021: TryAdd is lock-free. JS-001: no throw. ASCII-only.
            _laneEFlattenBlocked.TryAdd(acc.Name + "#LANE-E#" + instrument.FullName, 0);
            CancelAllAccountOrders(acc, instrument); // B69 DW-B69-01: cancel ALL orders first
            // B76 HOTFIX-B76-FLATTEN-RACE-01: re-read after cancel.
            // ATM bracket fill may have cleared the position while cancel request was in-flight.
            var posAfterCancel = FindPosition(acc, instrument);
            if (posAfterCancel == null || posAfterCancel.Quantity == 0)
            {
                StatusUpdate?.Invoke(acc.Name + ": flat-race skip (pos cleared by bracket fill)");
                return;
            }
            var action =
                posAfterCancel.MarketPosition == MarketPosition.Long
                    ? OrderAction.Sell
                    : OrderAction.BuyToCover;
            try
            {
                var order = acc.CreateOrder(
                    instrument,
                    action,
                    OrderType.Market,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    posAfterCancel.Quantity,
                    0,
                    0,
                    null,
                    "PTT-Flatten",
                    DateTime.MaxValue,
                    null
                );
                if (order != null)
                {
                    // B194-F4: record timestamp at submission (not at fill) so the 500ms lockout
                    // begins from the moment flatten is dispatched to NT8.
                    _flattenFilledTicks.AddOrUpdate(acc.Name, flattenNow, (_, __) => flattenNow);
                    // B200-CLOSE-LANED-01-V12: anchor this order ID so OnOrderUpdate only clears
                    // _flattenInFlight for THIS flatten, not for stale Filled/Cancelled events
                    // from a prior cycle that arrive late on the NT8 background thread.
                    _flattenInFlightOrderId[acc.Name] = order.OrderId ?? string.Empty;
                    acc.Submit(new[] { order });
                }
                StatusUpdate?.Invoke(acc.Name + ": flatten " + posAfterCancel.Quantity);
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke("PTT-Flatten error: " + ex.Message);
            }
        }

        // B29 fix -- ComputeLimitPx: aggressive exit anchor.
        // Long exits (Sell Limit) post at bid - buffer (at/below market -> fills immediately).
        // Short exits (BuyToCover) post at ask + buffer (at/above market -> fills immediately).
        // DW-B29-01: original used ask+buffer for long, placing passive limit ABOVE market (never filled).
        // CYC=1: single ternary. No NT8 deps, no state, no nulls.
        // internal static -- CopyEngineTests.cs calls CopyEngine.ComputeLimitPx(...) directly.
        internal static double ComputeLimitPx(
            bool isLong,
            double ask,
            double bid,
            int exitBuffer,
            double tickSize
        ) => isLong ? bid - exitBuffer * tickSize : ask + exitBuffer * tickSize;

        // B19 T1 -- Trim 4-arg: exit half position at limit price anchored to ask (long) or bid (short).
        // Long: Sell Limit @ ask + exitBuffer*tick.   Short: BuyToCover @ bid - exitBuffer*tick.
        // NT8-007: arg 12 = (NinjaTrader.Cbi.CustomOrder)null.
        // NT8-014: signal name = "PTT-TrimLimit".
        // NT8-032: ask/bid are MarketDataEventArgs.Price doubles (callers obtain via GetAsk()/GetBid()).
        // CYC=6: (1+2) compound ask/bid guard, (3) exitBuffer guard, (4) foreach, (5+6) pos null||qty guard.
        // JS-001: try/catch wraps acc.CreateOrder -- no rethrow. BGTM-1: TrimFlatten gate CYC=7.
        internal void Trim(Instrument instrument, int exitBuffer, double ask, double bid)
        {
            if (!_flags.TrimFlatten)
            {
                StatusUpdate?.Invoke("Trim/Flatten requires Pro tier");
                return;
            }
            if (ask <= 0 || bid <= 0 || exitBuffer == 0)
            {
                Trim(instrument);
                return;
            }
            foreach (var acc in AllAccounts(instrument))
                TrimOneAccountLimit(acc, instrument, exitBuffer, ask, bid);
        }

        // B19 T1 -- Flatten 4-arg: exit full position at limit price anchored to ask (long) or bid (short).
        // NT8-007: arg 12 = (NinjaTrader.Cbi.CustomOrder)null.
        // NT8-014: signal name = "PTT-FlattenLimit". BGTM-1: TrimFlatten gate CYC+=1.
        internal void Flatten(Instrument instrument, int exitBuffer, double ask, double bid)
        {
            if (!_flags.TrimFlatten)
            {
                StatusUpdate?.Invoke("Trim/Flatten requires Pro tier");
                return;
            }
            if (ask <= 0 || bid <= 0 || exitBuffer == 0)
            {
                Flatten(instrument);
                return;
            }
            foreach (var acc in AllAccounts(instrument))
                FlattenOneAccountLimit(acc, instrument, exitBuffer, ask, bid);
        }

        // BGTM-1: TrimFlatten gate CYC=3.
        internal void CancelPendingEntries(Instrument instrument)
        {
            if (!_flags.TrimFlatten)
            {
                StatusUpdate?.Invoke("Trim/Flatten requires Pro tier");
                return;
            }
            foreach (var acc in AllAccounts(instrument))
                CancelOneAccount(acc, instrument);
        }

        // B28 T1 -- CancelOneAccount: per-account pending cancel helper. CYC=4.
        // (1) foreach orders, (2) instrument filter, (3) OrderState guard, (4) IsBracketLeg guard.
        // Preserves B18 T3 fix: also cancels Initialized orders (DW-B18-CANCEL-01).
        private void CancelOneAccount(Account acc, Instrument instrument)
        {
            foreach (var order in acc.Orders.ToList())
            {
                if (order.Instrument != instrument)
                    continue;
                // B18 T3: DW-B18-CANCEL-01 -- also cancel Initialized orders.
                if (
                    order.OrderState != OrderState.Working
                    && order.OrderState != OrderState.Initialized
                )
                    continue;
                if (IsBracketLeg(order))
                    continue;
                try
                {
                    acc.Cancel(new Order[] { order });
                    StatusUpdate?.Invoke(acc.Name + ": entry pulled " + order.OrderId);
                }
                catch (Exception ex)
                {
                    StatusUpdate?.Invoke("PTT-Cancel error: " + ex.Message);
                }
            }
        }

        // HOTFIX-F4 -- CancelStaleExitOrders: cancels any working PTT limit exit orders by signal name.
        // Called by TrimOneAccountLimit and FlattenOneAccountLimit before posting a new limit.
        // Prevents stale PTT-TrimLimit/PTT-FlattenLimit orders competing with ATM Close or
        // a second button click, which caused "Close operation timed out" popup in NT8.
        // CYC=3: foreach(1), name filter(2), try/catch(3). JS-021: ToList() snapshot.
        private void CancelStaleExitOrders(Account acc, Instrument instrument, string signalName)
        {
            foreach (var order in acc.Orders.ToList())
            {
                if (order.Instrument != instrument)
                    continue; // (1)
                if (order.Name != signalName)
                    continue; // (2)
                if (
                    order.OrderState != OrderState.Working
                    && order.OrderState != OrderState.Initialized
                )
                    continue;
                try // (3)
                {
                    acc.Cancel(new Order[] { order });
                    StatusUpdate?.Invoke(acc.Name + ": stale exit pulled " + order.Name);
                }
                catch (Exception ex)
                {
                    StatusUpdate?.Invoke("PTT-CancelStale error: " + ex.Message);
                }
            }
        }

        // B28 T1 -- TrimOneAccountLimit: per-account limit trim helper. CYC=3.
        // (1) pos null/qty guard, (2) isLong ternary, (3) try/catch CreateOrder.
        // NT8-007: arg12 = (NinjaTrader.Cbi.CustomOrder)null.
        // HOTFIX-F4: cancel any stale PTT-TrimLimit orders before posting new one.
        // Stale limits from a prior click stay live on the book and compete with ATM Close.
        private void TrimOneAccountLimit(
            Account acc,
            Instrument instrument,
            int exitBuffer,
            double ask,
            double bid
        )
        {
            CancelStaleExitOrders(acc, instrument, "PTT-TrimLimit"); // HOTFIX-F4
            var pos = FindPosition(acc, instrument);
            if (pos == null || pos.Quantity == 0)
            {
                StatusUpdate?.Invoke(acc.Name + ": flat skip");
                return;
            }
            int trimQty = (int)Math.Ceiling(pos.Quantity / 2.0);
            bool isLong = pos.MarketPosition == MarketPosition.Long;
            var action = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
            double tickSize = instrument.MasterInstrument.TickSize;
            double limitPx = ComputeLimitPx(isLong, ask, bid, exitBuffer, tickSize);
            try
            {
                acc.CreateOrder(
                    instrument,
                    action,
                    OrderType.Limit,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    trimQty,
                    limitPx,
                    0,
                    null,
                    "PTT-TrimLimit",
                    DateTime.MaxValue,
                    (NinjaTrader.Cbi.CustomOrder)null
                );
                StatusUpdate?.Invoke(acc.Name + ": trim-limit " + trimQty + " @ " + limitPx);
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke("PTT-TrimLimit error: " + ex.Message);
            }
        }

        // B28 T1 -- FlattenOneAccountLimit: per-account limit flatten helper. CYC=3.
        // (1) pos null/qty guard, (2) isLong ternary, (3) try/catch CreateOrder.
        // NT8-007: arg12 = (NinjaTrader.Cbi.CustomOrder)null.
        // HOTFIX-F4: cancel any stale PTT-FlattenLimit orders before posting new one.
        // Stale limits from a prior click stay live on the book and compete with ATM Close.
        private void FlattenOneAccountLimit(
            Account acc,
            Instrument instrument,
            int exitBuffer,
            double ask,
            double bid
        )
        {
            CancelStaleExitOrders(acc, instrument, "PTT-FlattenLimit"); // HOTFIX-F4
            var pos = FindPosition(acc, instrument);
            if (pos == null || pos.Quantity == 0)
            {
                StatusUpdate?.Invoke(acc.Name + ": flat skip");
                return;
            }
            bool isLong = pos.MarketPosition == MarketPosition.Long;
            var action = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
            double tickSize = instrument.MasterInstrument.TickSize;
            double limitPx = ComputeLimitPx(isLong, ask, bid, exitBuffer, tickSize);
            try
            {
                acc.CreateOrder(
                    instrument,
                    action,
                    OrderType.Limit,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    pos.Quantity,
                    limitPx,
                    0,
                    null,
                    "PTT-FlattenLimit",
                    DateTime.MaxValue,
                    (NinjaTrader.Cbi.CustomOrder)null
                );
                StatusUpdate?.Invoke(
                    acc.Name + ": flatten-limit " + pos.Quantity + " @ " + limitPx
                );
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke("PTT-FlattenLimit error: " + ex.Message);
            }
        }

        // B62: price-keyed dedup. Stores LimitPrice (double) instead of timestamp (long).
        // First call for orderId: TryAdd succeeds -> not a dup -> dispatch.
        // Repeat call same orderId: TryAdd fails -> true dup -> skip.
        // Drag detection is handled by Gate C BEFORE this is called -- drag events never reach IsDedup.
        // Eviction is handled by EvictDedup on terminal states (Filled/Cancelled/Rejected).
        // CYC=2: TryAdd false-path (1) + early return.
        // JS-025: ConcurrentDictionary.TryAdd is lock-free.
        private bool IsDedup(string orderId, double limitPrice)
        {
            if (!_dedupCache.TryAdd(orderId, limitPrice))
                return true;

            return false;
        }

        // DW-B91-A: guard -- returns true if this orderId was already dispatched (blocks re-dispatch).
        // Side-effect on first call: TryAdd records the orderId as dispatched.
        // CYC=2: 1 base + 1 if (ContainsKey).
        // JS-021: ContainsKey + TryAdd are lock-free. JS-001: no throw. JS-002: returns bool.
        private bool IsEntryDispatched(string orderId)
        {
            if (_entryDispatchedOrders.ContainsKey(orderId))
                return true;
            _entryDispatchedOrders.TryAdd(orderId, 0);
            return false;
        }

        // DW-B142-MGC-02: Gate 5 compound predicate for DispatchCopy.
        // CYC=4: liveInstr guard(1) + IsDedup(2) + IsEntryDispatched(3).
        // Returns true (block dispatch) on any of:
        //   (a) instrument already has a live entry dispatched this slot -- blocks resubmit dup.
        //   (b) same orderId seen before -- orderId-level dup guard.
        //   (c) orderId was previously dispatched and survived EvictDedup -- eviction-bypass guard.
        // On false (first real dispatch): records instrKey in _liveEntryInstruments
        //   and orderId in _entryInstrKeyByOrderId.
        // JS-021: no lock. JS-001: no throw. JS-002: returns bool. ASCII-only.
        private bool IsLiveEntryBlocked(string instrKey, string orderId, double limitPrice)
        {
            if (_liveEntryInstruments.ContainsKey(instrKey))
            {
                // DR-1: Gate 5 diagnostic log -- stale instrKey blocks all new entries silently.
                // Previously zero output on block; this makes every re-trigger immediately visible.
                NinjaTrader.Code.Output.Process(
                    "[PTT-GATE5-BLOCKED] stale instrKey=" + instrKey
                        + " orderId=" + orderId,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return true;
            }
            if (IsDedup(orderId, limitPrice))
                return true;
            if (IsEntryDispatched(orderId))
                return true;
            _liveEntryInstruments.TryAdd(instrKey, 0);
            _entryInstrKeyByOrderId.TryAdd(orderId, instrKey);
            return false;
        }

        // DW-B142-MGC-02: on position flat, remove all live-entry keys for this instrument.
        // CYC=2: foreach(1). ConcurrentDictionary enumeration is snapshot-safe.
        // JS-021: no lock. JS-001: no throw. ASCII-only.
        private void ClearLiveEntryForInstrument(string instrFullName)
        {
            foreach (var key in _liveEntryInstruments.Keys)
            {
                if (key.StartsWith(instrFullName + "|", StringComparison.Ordinal))
                    _liveEntryInstruments.TryRemove(key, out _);
            }
        }

        // B62: evict dedup entry when order reaches terminal state (Filled/Cancelled/Rejected).
        // Called unconditionally from OnOrderUpdate pre-gate, after TryFirePositionState.
        // Ensures evicted orderId can be re-used for the next fresh order on the same instrument.
        // DW-B142-MGC-02: CYC=5: terminal-guard(1) + Cancelled(2) + instrKey-lookup(3) + Filled(4).
        // JS-025: ConcurrentDictionary.TryRemove is lock-free.
        // DR-5: instrKey param added -- direct fallback when _entryInstrKeyByOrderId misses due to
        // NT8/Rithmic orderId reassignment (GUID at Accepted -> numeric at Cancelled).
        // Callers that have no Order object pass null; the fallback is skipped safely.
        internal void EvictDedup(string orderId, OrderState state, string instrKey = null)
        {
            if (
                state != OrderState.Filled
                && state != OrderState.Cancelled
                && state != OrderState.Rejected
            )
                return;

            _dedupCache.TryRemove(orderId, out _);

            if (state == OrderState.Cancelled)
            {
                // DW-B142-MGC-02: scoped removal -- do NOT Clear() the whole map.
                // Bracket/drag/ATM cancels must not wipe the entry dispatch guard for other orderIds.
                _entryDispatchedOrders.TryRemove(orderId, out _);
                // If this orderId was a dispatched entry (no fill, just cancelled),
                // remove the instrument-level live guard so future entries are not blocked.
                if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
                {
                    _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
                    NinjaTrader.Code.Output.Process(
                        "[PTT-DR5-EVICT] orderId=" + orderId + " instrKey=" + cancelledInstrKey,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                }
                else if (instrKey != null && _liveEntryInstruments.ContainsKey(instrKey))
                {
                    // DR-5: NT8 reassigned orderId between dispatch (GUID) and cancel (numeric).
                    // _entryInstrKeyByOrderId missed -- fall back to Order-derived instrKey directly.
                    _liveEntryInstruments.TryRemove(instrKey, out _);
                    NinjaTrader.Code.Output.Process(
                        "[PTT-DR5-FALLBACK] orderId=" + orderId + " instrKey=" + instrKey
                            + " (orderId remapped by NT8/Rithmic)",
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                }
            }

            if (state == OrderState.Filled)
            {
                // DW-B142-MGC-02: clean up companion map (lazy).
                // Do NOT remove _liveEntryInstruments key -- trade is live.
                // PositionStateChanged flat gate (ClearLiveEntryForInstrument) is the authoritative cleanup.
                _entryInstrKeyByOrderId.TryRemove(orderId, out _);
            }
            // DW-B91-A-v2: Filled/Rejected _entryDispatchedOrders eviction handled in TryEvictFollowerBeSlot.
        }

        // B127: updated to implement Option A lazy re-resolve (DW-PTT-BE-FIX-01).
        // CYC=7: rule==null(1) + for(1) + acc!=null(1) + names ternary(1) + IsNullOrEmpty(1)
        //         + TryGetValue(1) + resolved!=null(1). Within JS limit of 8.
        // JS-021: no lock -- ConcurrentDictionary.TryGetValue + TryAdd are lock-free.
        // JS-001: no throw -- all paths yield, continue, or emit Output.Process.
        // JS-002: no null values yielded -- null slots are resolved or skipped.
        // ASCII-only strings in all log messages.
        internal IEnumerable<Account> AllAccounts(Instrument instrument)
        {
            var rule = FindRule(instrument);
            if (rule == null)
                yield break;

            yield return rule.Value.MasterAccount;
            var followers = rule.Value.FollowerAccounts;
            var names = rule.Value.FollowerAccountNames;
            for (int i = 0; i < followers.Length; i++)
            {
                var acc = followers[i];
                if (acc != null)
                {
                    yield return acc;
                    continue;
                }
                // B127: lazy re-resolve for slot that was null at load time.
                var name = (names != null && i < names.Length) ? names[i] : null;
                if (string.IsNullOrEmpty(name))
                    continue;
                if (_resolvedFollowers.TryGetValue(name, out var cached))
                {
                    yield return cached;
                    continue;
                }
                var resolved = FindFollowerAccount(name);
                if (resolved != null)
                {
                    _resolvedFollowers.TryAdd(name, resolved);
                    NinjaTrader.Code.Output.Process(
                        "[PTT-COPY] INFO: follower '"
                            + name
                            + "' resolved lazily -- now copying to this account.",
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
                    yield return resolved;
                    continue;
                }
                NinjaTrader.Code.Output.Process(
                    "[PTT-COPY] WARNING: follower '"
                        + name
                        + "' not found in Account.All"
                        + " -- account not connected yet; will retry on next dispatch.",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }
        }

        /// <summary>
        /// Finds the copy rule for the given instrument.
        /// </summary>
        /// <returns>
        /// Matching <see cref="CopyRule"/>, or <c>null</c> if no rule exists for this instrument.
        /// Callers MUST null-check the return value.
        /// </returns>
        internal CopyRule? FindRule(Instrument instrument)
        {
            if (instrument == null)
                return null; // Change 8: null guard
            foreach (var rule in _rules)
            {
                if (rule.Instrument == instrument.FullName)
                    return rule;
            }
            return null;
        }

        // Change 6: Replace PassesDailyCapCheck stub with real P&L check
        // DR-8: SIM accounts bypass daily cap -- floor is a live-account risk control.
        // SIM account names always end with digits after "Sim" prefix (Sim101, Sim102, etc.)
        // or contain "Playback". NT8 SIM accounts also expose Account.IsSim but that requires
        // NT8 runtime -- use name heuristic (string check) to stay unit-testable.
        // JS-021: no lock. JS-001: no throw. ASCII-only.
        internal bool PassesDailyCapCheck(Account acc)
        {
            // DR-8: bypass floor check for SIM / Playback accounts
            if (acc.Name != null
                && (acc.Name.StartsWith("Sim", StringComparison.OrdinalIgnoreCase)
                    || acc.Name.StartsWith("Playback", StringComparison.OrdinalIgnoreCase)))
                return true;
            double pnl = acc.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
            if (pnl == double.MinValue)
                return true;
            return pnl > _dailyCapFloor;
        }

        private static bool IsFlat(NinjaTrader.Cbi.Position pos)
        {
            return pos == null || pos.Quantity == 0;
        }

        // B119: DW-B128 -- direction-change guard predicate.
        // Returns true iff the current dispatch reverses the last direction AND the follower is flat.
        // CYC=2 (one && expression in a single return). JS-001: no throw. JS-021: no lock. ASCII-only.
        // internal static: directly callable from B119Tests.cs without reflection.
        internal static bool IsReversalToFlatFollower(
            OrderAction currentAction,
            OrderAction lastAction,
            bool followerIsFlat
        )
        {
            return currentAction != lastAction && followerIsFlat;
        }

        // B25 T1 -- DW-B25-01: ATM bracket stops use name format "12s Buy STP".
        // FromEntrySignal is null for ATM orders. No "Stop" prefix. STP suffix is the only discriminator.
        // CYC: 2 + 1 (STP clause) = 3. OrdinalIgnoreCase: consistent with WireLeaderAccount (B24 Lane A).
        // B208-FIX-D: added PTT-BE-Stop clause (CYC 4->5). "PTT-BE-Stop-N" named StopMarket orders were
        //   misclassified as non-stop => TP3-HBC read LimitPrice=0 instead of StopPrice => rawPrice=0 =>
        //   orders panel showed Stop Mkt at 0 on leader column. Covers bare "PTT-BE-Stop" and suffixed
        //   "PTT-BE-Stop-1/2/N". JS-001: no throw. JS-002: bool return. JS-021: no lock. ASCII-only.
        private static bool IsStopLeg(Order order)
        {
            return order.FromEntrySignal != null
                || (order.Name != null && order.Name.StartsWith("Stop"))
                || (order.Name != null && order.Name.StartsWith("SimStop", StringComparison.Ordinal))
                || (
                    order.Name != null
                    && order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase)
                )
                || (order.Name != null && order.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)); // B208-FIX-D
        }

        // Static version of IsBracketLeg for use in static method IsWorkingBracket
        // DW-B134: added STP EndsWith clause -- NT8 ATM stop brackets are named "Buy STP"/"Sell STP".
        // Mirrors IsStopLeg (L3521) which already has this clause. CYC: 3 -> 4.
        private static bool IsBracketLegStatic(Order order)
        {
            return order.FromEntrySignal != null
                || (
                    order.Name != null
                    && (
                        order.Name.StartsWith("Stop")
                        || order.Name.StartsWith("Target")
                        || order.Name.StartsWith("PTT-")
                        || order.Name.StartsWith("SimStop", StringComparison.Ordinal)
                        || order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase)
                    )
                );
        }

        // B29 fix: removed "PTT-" from IsBracketLeg.
        // IsBracketLeg is used by CancelOneAccount to skip bracket stops/targets.
        // PTT- exit orders (PTT-Trim, PTT-Flatten, PTT-BE-Stop, PTT-Tighten-Stop) are NOT brackets --
        // they should be cancelable by the Cancel button.
        // Copy-cascade prevention for PTT- orders is handled separately by Gate 0.5 in DispatchCopy.
        private bool IsBracketLeg(Order order)
        {
            return order.FromEntrySignal != null
                || (
                    order.Name != null
                    && (order.Name.StartsWith("Stop") || order.Name.StartsWith("Target") || order.Name.StartsWith("SimStop", StringComparison.Ordinal))
                );
        }

        private Position FindPosition(Account acc, Instrument instrument)
        {
            foreach (Position p in acc.Positions)
                if (p.Instrument != null && p.Instrument.FullName == instrument.FullName)
                    return p;
            return null;
        }

        // B58 -- FindPositionPublic: thin wrapper over private FindPosition for panel access.
        // CYC=1. Returns null if no position (pre-existing FindPosition behavior -- not new).
        // JS-002: null return is pre-existing contract of FindPosition, not a new null-return site.
        internal Position FindPositionPublic(Account acc, Instrument instrument) =>
            FindPosition(acc, instrument);

        // B58 -- SnapshotTargetsPublic: collects Working orders with PTT-QX-T or PTT-TGT- prefix.
        // CYC=3 (1 base + foreach + prefix check). Returns List<Order> -- panel uses .Count.
        // JS-002: never returns null -- returns empty List if no matches.
        // JS-021: acc.Orders iteration; no lock required (NT8 AddOn read-only enumeration).
        internal List<Order> SnapshotTargetsPublic(Account acc, Instrument instr)
        {
            var result = new List<Order>();
            if (acc == null || instr == null)
                return result; // (1) null guard
            foreach (Order o in acc.Orders) // (2) foreach
            {
                if (o.Instrument != instr)
                    continue;
                if (o.OrderState != OrderState.Working)
                    continue;
                string n = o.Name ?? string.Empty;
                if (
                    n.StartsWith(PttOrderNames.PttQxTargetPrefix, StringComparison.Ordinal) // (3) prefix check
                    || n.StartsWith(PttOrderNames.PttTgtPrefix, StringComparison.Ordinal)
                )
                    result.Add(o);
            }
            return result;
        }

        // HOTFIX-MSTBE-CANCEL-RESUBMIT: replaced acc.Change() (silent no-op on ATM brackets) with
        // cancel+resubmit pattern mirroring PttBreakEven.ExecuteOneAccount.
        // Root cause: NT8 ATM engine owns Stop1/Stop2 brackets and ignores acc.Change() from AddOn
        // context -- no exception, no effect. Confirmed: [MSTBE] Change() OK Stop1 logged while
        // stop remained at original price in Orders tab.
        // Pattern source: PttBreakEven.ExecuteOneAccount + CancelStaleBracketsLocal + SubmitBeTargetsLocal.
        // CYC=7: IsFlat(1) + tickSize/pos guard(2) + while-cap(3) + cancel-try(4)
        //        + 0-targets branch(5) + targets-for-loop(6) + partial-retry branch(7).
        // DW-B107: Step A extracted to SnapshotBeTargets; while cap reduces stale residue.
        // JS-021: no lock. JS-001: try/catch per order pair -- no throw in hot path.
        // NT8-049: StopMarket arg6=0, arg7=stopPrice; Limit arg6=limitPrice, arg7=0.
        // NT8-007: arg11=(NinjaTrader.Cbi.CustomOrder)null. NT8-013: DateTime.MaxValue.
        // NT8-014: signal names start with "PTT-". NT8-006: no LINQ.
        // DW-B79-04 isRetry: prevents recursive retry loops.
        // CountLeaderTargets: CYC=4. Returns the number of Working native target limit orders
        // (Target1..Target9, digit 1-9, no PTT- prefix) on the leader account for the given
        // instrument. Working-only (DW-B116: Accepted/Submitted removed -- transitional states
        // cause overcount). Capped at Math.Min(count,3) -- standard ATM max 3 targets.
        // Used by MoveStopToBreakEven to detect partial-target visibility on followers (DW-B79-07).
        // DW-B116 fix: removed PTT-QX-T* and PTT-BE-Target-* from isTarget predicate.
        // JS-021: no lock. JS-001: no throw. JS-002: returns int (never negative). ASCII-only.
        private int CountLeaderTargets(Instrument instrument)
        {
            var rule = FindRule(instrument);
            if (rule == null)
                return 0; // (1)
            var leader = rule.Value.MasterAccount;
            if (leader == null)
                return 0; // (2)
            int count = 0;
            foreach (Order o in leader.Orders) // (3)
            {
                if (o == null)
                    continue;
                bool stateOk = o.OrderState == OrderState.Working;
                bool instrOk = o.Instrument != null && o.Instrument.FullName == instrument.FullName;
                if (!stateOk || !instrOk || o.OrderType != OrderType.Limit)
                    continue;
                bool isTarget =
                    !string.IsNullOrEmpty(o.Name)
                    && o.Name.Length >= 7
                    && o.Name.StartsWith("Target", StringComparison.Ordinal)
                    && char.IsDigit(o.Name[6])
                    && o.Name[6] != '0';
                if (isTarget)
                    count++; // (4)
            }
            return Math.Min(count, 3);
        }

        // CYC=7: null guard(1) + foreach(2) + o==null continue(3) + stateOk(4) + instrOk+type(5)
        //        + if(isNative)(6) + else if(isPtt)(7). JS-002: returns List, never null.
        // JS-021: no lock. JS-001: no throw. ASCII-only.
        // DW-B107: two-pass native-first collect for MoveStopToBreakEven Step A.
        // stateOk is wider than SnapshotTargetOrders (7 states vs 2) per DW-B79-01 + REPAIR-09 DW-B79-05.
        private List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargets(
            Account acc,
            Instrument instrument
        )
        {
            var nativeTargets = new List<(double Price, int Qty, OrderAction Action)>();
            var pttTargets = new List<(double Price, int Qty, OrderAction Action)>();
            if (acc == null || instrument == null)
                return nativeTargets; // (1) JS-002: empty list, never null
            foreach (Order o in acc.Orders) // (2)
            {
                if (o == null)
                    continue; // (3)
                bool stateOk =
                    o.OrderState == OrderState.Working
                    || o.OrderState == OrderState.Accepted
                    || o.OrderState == OrderState.Submitted
                    || o.OrderState == OrderState.Initialized
                    || o.OrderState == OrderState.TriggerPending // (4)
                    || o.OrderState == OrderState.ChangeSubmitted
                    || o.OrderState == OrderState.CancelSubmitted;
                bool instrOk = o.Instrument != null && o.Instrument.FullName == instrument.FullName; // (5)
                if (!stateOk || !instrOk || o.OrderType != OrderType.Limit)
                    continue;
                if (string.IsNullOrEmpty(o.Name))
                    continue;
                bool isNative =
                    o.Name.Length >= 7
                    && o.Name.StartsWith("Target", StringComparison.Ordinal)
                    && char.IsDigit(o.Name[6])
                    && o.Name[6] != '0';
                bool isPtt =
                    (
                        o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                        && o.Name.Length > 8
                        && char.IsDigit(o.Name[8])
                    ) || o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal);
                if (isNative) // (6)
                    nativeTargets.Add((o.LimitPrice, o.Quantity, o.OrderAction));
                else if (isPtt) // (7)
                    pttTargets.Add((o.LimitPrice, o.Quantity, o.OrderAction));
            }
            return nativeTargets.Count > 0 ? nativeTargets : pttTargets;
        }

        // B180: public seam for PttBeSwapAll and PttBeSwapOne. Delegates to private SnapshotBeTargets.
        // CYC=1. JS-021: no lock. JS-001: no throw. JS-002: returns List (delegates, never null). ASCII-only.
        internal List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargetsPublic(
            Account acc, Instrument instr)
            => SnapshotBeTargets(acc, instr);

        // B180 DW-B180-BE-SWAP-ALL: BreakEven(Instrument,int) -- immediate swap all accounts (no-leader-arg path).
        // CYC=3: flag-gate(1) + foreach(2) + PttBeSwapOne(3).
        // B212-REENTRY-V3: leader-skip removed. Re-entry suppression moved to DispatchCopy
        //   (IsBeSwapInProgress guard). PttBreakEvenSwap.Execute sets _beSwapInProgress[leader]=true
        //   before CancelQxBrackets; DispatchCopy aborts copy dispatch for leader during the window.
        // JS-021: no lock. JS-001: no throw. ASCII-only.
        internal void BreakEven(Instrument instrument, int bufferTicks)
        {
            if (!_flags.BreakEven)
            {
                StatusUpdate?.Invoke("Break Even requires Pro tier");
                return;
            }
            foreach (var acc in AllAccounts(instrument))
                PttBeSwapOne.Execute(acc, instrument, bufferTicks);
        }

        // B180 DW-B180-BE-SWAP-ALL: BreakEven(Account,Instrument,int) -- panel path with explicit leader.
        // CYC=3: flag-gate(1) + null guard(2) + foreach(3).
        // B212-REENTRY-V3: leader-skip removed. Re-entry suppression moved to DispatchCopy
        //   (IsBeSwapInProgress guard). All accounts including leader get BE swap.
        // JS-021: no lock. JS-002: null leader fires StatusUpdate + early return. ASCII-only.
        internal void BreakEven(Account leader, Instrument instrument, int bufferTicks)
        {
            if (!_flags.BreakEven)
            {
                StatusUpdate?.Invoke("Break Even requires Pro tier");
                return;
            }
            if (leader == null) // (1) null guard
            {
                StatusUpdate?.Invoke("PTT-BE: leader null -- BE skipped");
                return;
            }
            foreach (var acc in AllAccounts(instrument)) // (2)
                PttBeSwapOne.Execute(acc, instrument, bufferTicks); // (3)
        }

        // B10 T3 -- TightenStop: moves all working stops on follower accounts to currentPrice +/- N ticks.
        // JS-001: try/catch inside TightenOneStop -- no throw in hot path.
        // JS-021: no lock -- AllAccounts iterates ConcurrentBag (lock-free).
        // DW-B9-GAP-001c: T3 production implementation.
        // B30: body delegated to TightenOneAccountStops (DW-B30-02, DW-B30-04).
        // CYC=2: (1) rule null guard, (2) foreach.
        internal void TightenStop(Instrument instrument, int ticks)
        {
            var rule = FindRule(instrument);
            if (rule == null) // (1)
                return;
            foreach (var acc in AllAccounts(instrument)) // (2)
                TightenOneAccountStops(acc, instrument, ticks);
        }

        // B10 T3 -- TightenOneStop: applies tighten to a single stop order.
        // B31: in-place price move via order.StopPrice + acc.Change(new Order[]{order}).
        // CYC=2: null guard(1), alreadyTighter(2). tightenAction ternary removed.
        private void TightenOneStop(
            Account acc,
            Instrument instr,
            Order order,
            double targetPrice,
            double tickSize
        )
        {
            if (order == null) // (1)
                return;
            bool isLong = order.OrderAction == OrderAction.Sell; // stop-sell = long pos
            bool alreadyTighter = isLong
                ? order.StopPrice >= targetPrice
                : order.StopPrice <= targetPrice;
            if (alreadyTighter) // (2)
                return;
            // B31 NT8-046: property-set + single-array acc.Change() -- preserves ATM OCO.
            try
            {
                order.StopPrice = targetPrice;
                acc.Change(new Order[] { order });
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke(acc.Name + ": Tighten Change() failed -- " + ex.Message);
            }
        }

        // B30 -- ShouldTightenOrder: order-filter predicate for TightenOneAccountStops.
        // CYC=4: (1) Working check, (2) StopMarket||StopLimit, (3) instrument match, (4) IsStopLeg.
        // JS-021: no lock. JS-001: no throw.
        private static bool ShouldTightenOrder(Order order, Instrument instrument)
        {
            if (order.OrderState != OrderState.Working)
                return false; // (1)
            if (order.OrderType != OrderType.StopMarket && order.OrderType != OrderType.StopLimit)
                return false; // (2)
            if (order.Instrument != instrument)
                return false; // (3)
            if (!IsStopLeg(order))
                return false; // (4)
            return true;
        }

        // B30 -- GetRefPrice: resolves bid/ask reference price for tighten-stop calculation.
        // TB-T7b extraction: SelectRefPriceByDirection absorbs ternary logic.
        // DW-B30-04: NT8 null-conditional (?.) prevents NullReferenceException when MarketData unsubscribed.
        // CYC after extraction: 2 (?. chains).
        private static double GetRefPrice(Instrument instrument, bool isLong)
        {
            double bid = instrument.MarketData?.Bid?.Price ?? 0.0;
            double ask = instrument.MarketData?.Ask?.Price ?? 0.0;
            return SelectRefPriceByDirection(isLong, bid, ask);
        }

        // TB-T7b: absorbed from GetRefPrice -- bid>0 && ask>0 guard + direction ternary.
        // CYC=3: (1) bid>0 &&, (2) ask>0, (3) isLong ?:.
        // TESTABILITY: internal static with primitive params -- directly testable without NT8 runtime.
        // Semantic: isLong -> ask (tighten long stop down toward ask); short -> bid.
        internal static double SelectRefPriceByDirection(bool isLong, double bid, double ask)
        {
            if (bid <= 0 || ask <= 0) // (1)(2)
                return 0.0;
            return isLong ? ask : bid; // (3)
        }

        // B30 -- TightenOneAccountStops: per-account stop-tighten helper. DW-B30-02.
        // CYC=5: (1) IsFlat guard, (2) refPrice==0 guard, (3) isLong ternary (target dir), (4) foreach, (5) !ShouldTightenOrder.
        // JS-021: no lock -- ToList() snapshot prevents iterator invalidation.
        // JS-002: no return null -- log "PTT-Tighten: no market data" on zero price.
        private void TightenOneAccountStops(Account acc, Instrument instrument, int tightenTicks)
        {
            var pos = FindPosition(acc, instrument);
            if (IsFlat(pos)) // (1)
                return;
            bool isLong = pos.MarketPosition == MarketPosition.Long;
            double tickSize = instrument.MasterInstrument.TickSize;
            double refPrice = GetRefPrice(instrument, isLong);
            if (refPrice == 0.0) // (2)
            {
                StatusUpdate?.Invoke("PTT-Tighten: no market data -- " + acc.Name);
                return;
            }
            double targetPrice = isLong // (3)
                ? refPrice - tightenTicks * tickSize
                : refPrice + tightenTicks * tickSize;
            foreach (var order in acc.Orders.ToList()) // (4)
            {
                if (!ShouldTightenOrder(order, instrument)) // (5)
                    continue;
                TightenOneStop(acc, instrument, order, targetPrice, tickSize);
            }
        }

        // B30 -- TightenStop(Account,Instrument,int): leader-direct overload. Fixes DW-B30-02.
        // CYC=4: (1) leader null guard, (2) leader direct call, (3) foreach, (4) acc==leader skip.
        // Pattern: identical to Trim(Account,Instrument) / Flatten(Account,Instrument) from B28.
        // JS-021: no lock. JS-002: no return null -- StatusUpdate log on null leader.
        internal void TightenStop(Account leader, Instrument instrument, int tightenTicks)
        {
            if (leader == null) // (1)
            {
                StatusUpdate?.Invoke("PTT-Tighten: leader null -- skipping");
                return;
            }
            TightenOneAccountStops(leader, instrument, tightenTicks); // (2)
            foreach (var acc in AllAccounts(instrument)) // (3)
            {
                if (acc == leader)
                    continue; // (4)
                TightenOneAccountStops(acc, instrument, tightenTicks);
            }
        }

        // B27 -- DisarmPendingBe: disarms the pending BE watcher atomically.
        // CYC=3: leader null guard(1), TryRemove check(2), acc null guard(3).
        // DW-B27-01: reads Account from slot -- no stale singleton reference.
        // JS-021: no lock -- ConcurrentDictionary.TryRemove is atomic.
        // NT8-043: explicit if (acc != null) guard -- no ?.Event -= pattern.
        internal void DisarmPendingBe(Account leader)
        {
            if (leader == null) // (1)
            {
                StatusUpdate?.Invoke("DisarmPendingBe: leader null -- no-op");
                return;
            }
            if (!_pendingBeSlots.TryRemove(leader.Name, out var slot)) // (2)
                return;
            if (slot.Account != null) // (3)
                slot.Account.AccountItemUpdate -= OnPendingBeAccountUpdate;
        }

        // B40 -- IsPendingSlotsEmpty: CYC=1. Lock-free read of ConcurrentDictionary.IsEmpty.
        // Called by TradeCopierPanel BE ALL armed/wait flow to determine gate state.
        // JS-021: ConcurrentDictionary.IsEmpty is lock-free.
        internal bool IsPendingSlotsEmpty() => _pendingBeSlots.IsEmpty;

        // B27 -- OnPendingBeAccountUpdate: price-based trigger for pending BE (one-shot).
        // Fires on NT8 account background thread -- NO UI calls inside this method.
        // BWAVE-CYC TB-T1: extracted IsPendingBeSlotActive, IsPendingBeTriggerConditionMet,
        //   ExecutePendingBeTrigger to reduce CCN from 32 to <=7.
        // JS-021: no lock -- TryGetValue/TryRemove are lock-free.
        // NT8-003: no volatile. B23 T1 (DW-B22-BE-TRIGGER-01): price-based, immune to commission fees.
        private void OnPendingBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)
        {
            if (e.AccountItem != AccountItem.UnrealizedProfitLoss) // (1)
                return;
            string accName = GetSenderAccountName(sender); // (2)
            if (!_pendingBeSlots.TryGetValue(accName, out var slot)) // (3)
                return;
            if (!IsPendingBeSlotActive(slot)) // (4)
                return;
            var pos = FindPosition(slot.Account, slot.Instrument);
            if (IsFlat(pos)) // (5)
                return;
            if (!IsPendingBeTriggerConditionMet(slot, pos, slot.Instrument)) // (6)
                return;
            if (!_pendingBeSlots.TryRemove(accName, out var removed)) // (7) atomic claim -- JS-021
                return;
            ExecutePendingBeTrigger(removed); // (8)
        }

        // BWAVE-CYC TB-T1 RETRY: executes the one-shot pending BE trigger after atomic claim.
        // CCN<=3: Account null check(1) = 1 branch + base. All ?./??. chains moved to FirePendingBeFiredEvent.
        // NT8-003: fires on account background thread -- no UI calls.
        private void ExecutePendingBeTrigger(PendingBeSlot removed)
        {
            if (removed.Account != null)
                removed.Account.AccountItemUpdate -= OnPendingBeAccountUpdate;
            BreakEven(removed.Account, removed.Instrument, removed.BufferTicks);
            FirePendingBeFiredEvent(removed.Instrument, removed.Account);
        }

        // BWAVE-CYC TB-T1 RETRY: fires the PendingBeFired event with null-safe argument resolution.
        // CCN<=6: PendingBeFired ?.(1) + Instrument ?.(2) + FullName ??(3) + Account ?.(4) + Name ??(5) = 5 + base.
        // NT8-003: fires on account background thread -- no UI calls.
        private void FirePendingBeFiredEvent(
            NinjaTrader.Cbi.Instrument instr,
            NinjaTrader.Cbi.Account acc
        )
        {
            PendingBeFired?.Invoke(instr?.FullName ?? string.Empty, acc?.Name ?? string.Empty);
        }

        // B27 -- ArmTrailBe: arms the continuous trail watcher using acc.AccountItemUpdate.
        // CYC=4: instr null(1), acc null(2), pos flat(3), slot upsert(4).
        // DW-B27-01: slot dicts replace five singleton fields -- per-account, no data races.
        // JS-021: no lock -- ConcurrentDictionary indexer writes are lock-free.
        // NT8-003: BitConverter bits in ConcurrentDictionary<string,long>; AddOrUpdate provides barrier.
        // BGTM-1: BreakEven gate CYC=5.
        internal void ArmTrailBe(Instrument instr, Account masterAcc, int bufferTicks)
        {
            if (!_flags.BreakEven)
            {
                StatusUpdate?.Invoke("Break Even requires Pro tier");
                return;
            }
            if (instr == null) // (1)
                return;
            if (masterAcc == null) // (2)
                return;
            var pos = FindPosition(masterAcc, instr);
            if (IsFlat(pos)) // (3)
                return;
            double currentPnl = masterAcc.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar);
            if (currentPnl == double.MinValue)
                currentPnl = 0.0;
            long pnlBits = BitConverter.DoubleToInt64Bits(currentPnl);
            _trailBeSlots[masterAcc.Name] = new TrailBeSlot(masterAcc, instr, bufferTicks); // (4)
            _trailBeLastPnlBits[masterAcc.Name] = pnlBits;
            masterAcc.AccountItemUpdate += OnTrailBeAccountUpdate;
        }

        // B27 -- DisarmTrailBe: disarms the trail watcher atomically.
        // CYC=3: leader null guard(1), TryRemove check(2), acc null guard(3).
        // DW-B27-01: reads Account from slot -- no stale singleton reference.
        // JS-021: no lock -- ConcurrentDictionary.TryRemove is atomic.
        // NT8-043: explicit if (acc != null) guard -- no ?.Event -= pattern.
        // Idempotent: safe to call when already Off or with null leader.
        internal void DisarmTrailBe(Account leader)
        {
            if (leader == null) // (1)
            {
                StatusUpdate?.Invoke("DisarmTrailBe: leader null -- no-op");
                return;
            }
            if (!_trailBeSlots.TryRemove(leader.Name, out var slot)) // (2)
                return;
            if (slot.Account != null) // (3)
                slot.Account.AccountItemUpdate -= OnTrailBeAccountUpdate;
            _trailBeLastPnlBits.TryRemove(leader.Name, out _);
        }

        // B27 -- OnTrailBeAccountUpdate: continuous AccountItemUpdate callback for auto-trail.
        // Fires on NT8 account background thread -- NO UI calls inside this method.
        // BWAVE-CYC TB-T3: extracted IsTrailBeTriggerMet to reduce CCN from 9 to <=6.
        // JS-021: no lock -- AddOrUpdate is lock-free CAS.
        // NT8-003: ConcurrentDictionary AddOrUpdate provides CAS barrier (long bits, no forbidden keyword).
        // JS-001: BreakEven internally wraps acc.Change() in try/catch; no rethrow here.
        // STAYS SUBSCRIBED until DisarmTrailBe() is called -- unlike OnPendingBeAccountUpdate (one-shot).
        private void OnTrailBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)
        {
            if (e.AccountItem != AccountItem.UnrealizedProfitLoss) // (1)
                return;
            string accName = GetSenderAccountName(sender); // (2) reuses TB-T1 helper -- removes ?. and ?? branches
            if (!_trailBeSlots.TryGetValue(accName, out var slot)) // (3)
                return;
            double newPnl = e.Value;
            if (!_trailBeLastPnlBits.TryGetValue(accName, out long oldBits)) // (4)
                return;
            if (!IsTrailBeTriggerMet(newPnl, oldBits)) // (5)
                return;
            long newBits = BitConverter.DoubleToInt64Bits(newPnl);
            long actual = _trailBeLastPnlBits.AddOrUpdate( // (6)
                accName,
                newBits,
                (k, cur) => cur < newBits ? newBits : cur
            );
            if (actual != newBits) // lost race (7)
                return;
            _trailBeSlots.AddOrUpdate( // (8)
                accName,
                new TrailBeSlot(slot.Account, slot.Instrument, slot.BufferTicks + 1),
                (k, old) => new TrailBeSlot(old.Account, old.Instrument, old.BufferTicks + 1)
            );
            BreakEven(slot.Account, slot.Instrument, slot.BufferTicks + 1);
        }

        // BWAVE-CYC TB-T3: IsTrailBeTriggerMet -- true when newPnl improves on oldBits.
        // Absorbs: BitConverter decode + newPnl <= oldPnl guard from OnTrailBeAccountUpdate.
        // CCN=2: base(1) + comparison(1).
        // Pure arithmetic -- no NT8 API, no concurrent state. BitConverter calls only.
        // internal: accessible to xUnit via InternalsVisibleTo("PropTraderTools.Tests") at L46.
        internal static bool IsTrailBeTriggerMet(double newPnl, long oldBits)
        {
            double oldPnl = BitConverter.Int64BitsToDouble(oldBits);
            return newPnl > oldPnl;
        }

        // BWAVE-CYC TB-T1: extracts (sender as Account)?.Name ?? "" to eliminate 2 Lizard branches
        // from the parent (?.  and ??) so parent CCN stays <= 8.
        // internal: accessible to xUnit via InternalsVisibleTo("PropTraderTools.Tests") at L46.
        // CCN=3: ?. + ?? + as-cast (Lizard branch count).
        internal static string GetSenderAccountName(object sender)
        {
            return (sender as NinjaTrader.Cbi.Account)?.Name ?? string.Empty;
        }

        // BWAVE-CYC TB-T1: slot-active guard -- verifies the slot has a non-null account reference.
        // Returns false when the slot was constructed with a null Account (degenerate/evicted slot).
        // internal: accessible to xUnit via InternalsVisibleTo at L46.
        // CCN=1: single null check.
        internal bool IsPendingBeSlotActive(PendingBeSlot slot)
        {
            return slot.Account != null;
        }

        // BWAVE-CYC TB-T1: null-safe bid price accessor for pending BE ref-price resolution.
        // Isolates the ?. null-conditional chain so ResolvePendingBeRefPx CCN stays <= 8.
        // internal: accessible to xUnit via InternalsVisibleTo at L46.
        // CCN=4: 2 ?. operators + 1 ?? (Lizard counts each as branch) + base.
        internal static double GetInstrMarketBid(NinjaTrader.Cbi.Instrument instr)
        {
            return instr.MarketData?.Bid?.Price ?? 0.0;
        }

        // BWAVE-CYC TB-T1: null-safe ask price accessor for pending BE ref-price resolution.
        // internal: accessible to xUnit via InternalsVisibleTo at L46.
        // CCN=4: 2 ?. operators + 1 ??.
        internal static double GetInstrMarketAsk(NinjaTrader.Cbi.Instrument instr)
        {
            return instr.MarketData?.Ask?.Price ?? 0.0;
        }

        // BWAVE-CYC TB-T1: resolves the HOTFIX-F2 reference price for pending BE trigger evaluation.
        // Long: use bid (price rising from below); fallback ask if bid=0.
        // Short: use ask (price falling from above); fallback bid if ask=0.
        // Returns 0.0 when instr is null or both bid and ask are unavailable (caller guards on refPx<=0).
        // internal: accessible to xUnit via InternalsVisibleTo at L46.
        // CCN<=6: instr null guard(1) + outer isLong ?:(2) + inner bid>0 ?:(3) + inner ask>0 ?:(4).
        internal static double ResolvePendingBeRefPx(NinjaTrader.Cbi.Instrument instr, bool isLong)
        {
            if (instr == null)
                return 0.0;
            double bid = GetInstrMarketBid(instr);
            double ask = GetInstrMarketAsk(instr);
            return isLong ? (bid > 0 ? bid : ask) : (ask > 0 ? ask : bid);
        }

        // BWAVE-CYC TB-T1 RETRY: price-based trigger condition for pending BE.
        // Delegates all branch-heavy logic to IsPendingBeSlotArmed and IsPendingBePriceTriggered.
        // internal: accessible to xUnit via InternalsVisibleTo at L46.
        // CCN<=4: instr null guard(1) + IsPendingBeSlotArmed gate(2) + refPx guard(3) = 3 branches + base.
        // NT8-003: no UI calls. Pure dispatch on position and instrument state.
        internal bool IsPendingBeTriggerConditionMet(
            PendingBeSlot slot,
            NinjaTrader.Cbi.Position pos,
            NinjaTrader.Cbi.Instrument instr
        )
        {
            if (instr == null)
                return false;
            if (!IsPendingBeSlotArmed(pos))
                return false;
            bool isLong = pos.MarketPosition == MarketPosition.Long;
            double refPx = ResolvePendingBeRefPx(instr, isLong);
            if (refPx <= 0.0)
                return false;
            return IsPendingBePriceTriggered(slot, pos, instr, refPx, isLong);
        }

        // BWAVE-CYC TB-T1 RETRY: armed-state guard -- verifies pos is non-null and position is open.
        // Absorbs the pos null check and flat check so parent CCN stays minimal.
        // CCN<=3: null guard(1) + MarketPosition comparison(2) = 2 branches + base.
        // NT8-003: no UI calls. Pure Position field read.
        private static bool IsPendingBeSlotArmed(NinjaTrader.Cbi.Position pos)
        {
            if (pos == null)
                return false;
            return pos.MarketPosition != MarketPosition.Flat;
        }

        // BWAVE-CYC TB-T1 RETRY: price trigger evaluation for pending BE.
        // Absorbs: tickSize ?.?? chain, tickSize guard, target calculation with ternary, final comparison.
        // All ?. ?? ?: operators extracted here so IsPendingBeTriggerConditionMet has no operators.
        // CCN<=6: tickSize ?.(1) + ??(2) + tickSize guard(3) + isLong ternary(4) + return ternary(5) = 5 + base.
        // NT8-003: no UI calls. Pure arithmetic on instrument and position fields.
        private static bool IsPendingBePriceTriggered(
            PendingBeSlot slot,
            NinjaTrader.Cbi.Position pos,
            NinjaTrader.Cbi.Instrument instr,
            double refPx,
            bool isLong
        )
        {
            double tickSize = instr.MasterInstrument?.TickSize ?? 0.0;
            if (tickSize <= 0.0)
                return false;
            double target = pos.AveragePrice + (isLong ? 1.0 : -1.0) * slot.BufferTicks * tickSize;
            return isLong ? (refPx >= target) : (refPx <= target);
        }

        // BWAVE-CYC TB-T2: test seams for TryRecordBeTargetFill and TryTriggerBeRecovery.
        // Order is NT8 private -- cannot be instantiated in test code.
        // Null-guard seams call with null directly (CYC=1 each).
        // State/name guard seams bypass Order by exposing the guard logic as bool predicates.
        // _filledBeTargetCount read accessor enables count-change assertions.

        // Test seam: TryRecordBeTargetFill null guard.
        internal void TryRecordBeTargetFillNullTestable() => TryRecordBeTargetFill(null);

        // Test seam: returns true ONLY when TryRecordBeTargetFill would record (all guards pass).
        // Parameters mirror the guard conditions -- avoids NT8 Order dependency.
        internal bool WouldRecordBeTargetFill(OrderState state, string name, string accountName)
        {
            if (state != OrderState.Filled)
                return false;
            if (name == null)
                return false;
            if (!name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal))
                return false;
            if (accountName == null)
                return false;
            _filledBeTargetCount.AddOrUpdate(accountName, 1, (_, prev) => prev + 1);
            return true;
        }

        // Test seam: read _filledBeTargetCount for given account (for assertion).
        internal int GetFilledBeTargetCount(string accountName)
        {
            _filledBeTargetCount.TryGetValue(accountName, out int count);
            return count;
        }

        // BWAVE-CYC TB-T1: test seam -- IsPendingBeTriggerConditionMet with null instrument.
        // PendingBeSlot is private so callers cannot pass it directly from test code.
        // This seam constructs default(PendingBeSlot) and passes null instr to test the null guard.
        // CYC=1: delegation only.
        internal bool IsPendingBeTriggerConditionMetNullInstrTestable() =>
            IsPendingBeTriggerConditionMet(default(PendingBeSlot), null, null);

        // BWAVE-CYC TB-T3: test seam -- InstrumentNamesMatchTestable for FindBePosition inner guard.
        // Tests the pure FullName comparison logic without requiring NT8 Account/Position objects.
        // Returns true when name1 == name2 (simulates p.Instrument.FullName == instr.FullName).
        // CCN=1: pure equality expression, no branching.
        internal static bool InstrumentFullNamesMatchTestable(string name1, string name2) =>
            name1 != null && name2 != null && name1 == name2;

        // -- B6/B8: Serialization DTO classes -----------------------------------

        [Serializable]
        internal sealed class CopyRuleDto
        {
            public string InstrumentName { get; set; } = string.Empty;
            public string MasterAccountName { get; set; } = string.Empty;
            public string[] FollowerAccountNames { get; set; } = new string[0];
            public bool IsEnabled { get; set; } = true;

            // B8 T1: per-follower quantity multipliers (parallel to FollowerAccountNames[])
            // Default empty = all followers 1x. Backward compat: null on B6/B7 XML = treat as all-1s.
            public int[] FollowerMultipliers { get; set; } = new int[0];

            // B8 T2 (pre-declared here for single DTO edit pass per plan S3.1):
            // FollowerAtmModeNames serializes "Inherit"|"Market"|"Named:XXX" per follower.
            public string[] FollowerAtmModeNames { get; set; } = new string[0];

            // B10 T3: TightenTicks serialization -- default 0 here (DtoToRule converts 0 to 5).
            // Backward compat: old XML without this element -> XmlSerializer sets to 0 -> DtoToRule maps to 5.
            public int TightenTicks { get; set; } = 0;
        }

        [Serializable]
        internal sealed class CopyRulesContainer
        {
            public List<CopyRuleDto> Rules { get; set; } = new List<CopyRuleDto>();

            // B54 -- persists copy-enabled state so F5 cycle restores button color correctly.
            // NT8-001: { get; set; } (not init accessor). XmlSerializer requires public { set; }.
            public bool CopyEnabled { get; set; } = false;
        }

        // -- B6: Path helper (CYC=1) -----------------------------------------

        private static string GetPersistencePath(string overridePath = null)
        {
            return overridePath
                ?? Path.Combine(
                    NinjaTrader.Core.Globals.UserDataDir,
                    "PropTraderTools",
                    "copy_rules.xml"
                );
        }

        // -- B6/B8: Conversion helpers -----------------------------------------

        // B8 T1: RuleToDto -- emits FollowerMultipliers and FollowerAtmModeNames arrays
        // B10 T3: also emits TightenTicks
        private static CopyRuleDto RuleToDto(CopyRule rule)
        {
            var followerNames = new string[rule.FollowerAccounts.Length];
            for (int i = 0; i < rule.FollowerAccounts.Length; i++)
                followerNames[i] =
                    rule.FollowerAccounts[i] != null ? rule.FollowerAccounts[i].Name : string.Empty;

            // B8 T1: serialize multipliers parallel to account names
            var mults = new int[rule.FollowerAccounts.Length];
            for (int i = 0; i < rule.FollowerAccounts.Length; i++)
                mults[i] =
                    (rule.FollowerMultipliers != null && i < rule.FollowerMultipliers.Length)
                        ? rule.FollowerMultipliers[i]
                        : 1;

            // B8 T2: serialize ATM mode names using AtmModeToString + GetAtmMode per follower
            var atmNames = new string[rule.FollowerAccounts.Length];
            for (int i = 0; i < rule.FollowerAccounts.Length; i++)
            {
                string accName =
                    rule.FollowerAccounts[i] != null ? rule.FollowerAccounts[i].Name : string.Empty;
                atmNames[i] = AtmModeToString(GetAtmMode(rule, accName));
            }

            return new CopyRuleDto
            {
                InstrumentName = rule.Instrument,
                MasterAccountName =
                    rule.MasterAccount != null ? rule.MasterAccount.Name : string.Empty,
                FollowerAccountNames = followerNames,
                IsEnabled = rule.Enabled,
                FollowerMultipliers = mults,
                FollowerAtmModeNames = atmNames,
                TightenTicks = rule.TightenTicks, // B10 T3: emit tighten ticks
            };
        }

        // TB-T7a: DtoToRule -- extraction of ResolveFollowerNames, ResolveAtmMap, ResolveMultipliers.
        // CYC after extraction: foreach Account.All(1) + acc.Name==(2) + for followers(3) + followers[i]==null(4) + tightenTicks ?(5) = 5.
        // B8 T1: reads FollowerMultipliers null-safely (B6/B7 XML backward compat)
        private static CopyRule DtoToRule(CopyRuleDto dto)
        {
            Account master = null;
            foreach (var acc in Account.All) // (1)
            {
                if (acc.Name == dto.MasterAccountName) // (2)
                {
                    master = acc;
                    break;
                }
            }

            string[] followerNames = ResolveFollowerNames(dto);
            var followers = new Account[followerNames.Length];
            for (int i = 0; i < followerNames.Length; i++) // (3)
            {
                followers[i] = FindFollowerAccount(followerNames[i]);
                // DW-B85 Option B: warn when follower account is not yet in Account.All at load time.
                // Workaround: uncheck + re-check the follower in the panel after NT8 finishes connecting.
                if (followers[i] == null) // (4)
                    NinjaTrader.Code.Output.Process(
                        "[PTT-COPY] WARNING: follower '"
                            + followerNames[i]
                            + "' not found in Account.All at load time"
                            + " -- will be skipped until rule is re-applied (uncheck + re-check in panel).",
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
            }

            int[] multipliers = ResolveMultipliers(dto);
            var atmMap = ResolveAtmMap(dto);

            // B10 T3: backward compat -- old XML has no TightenTicks element, XmlSerializer sets to 0.
            // DtoToRule converts: 0 -> default 5. Any positive value is preserved as-is.
            int tightenTicks = dto.TightenTicks > 0 ? dto.TightenTicks : 5; // (5)

            return CopyRule.Create(
                dto.InstrumentName,
                master,
                followers,
                dto.IsEnabled,
                multipliers,
                atmMap,
                tightenTicks,
                followerNames // B127: preserve original names (covers null-account slots)
            );
        }

        // TB-T7a: absorbed from DtoToRule -- null-safe resolution of FollowerAccountNames.
        // Returns Array.Empty<string>() when dto.FollowerAccountNames is null (pre-B6 XML).
        // CYC=2: null check(1) + return(2).
        // JS-002 compliant: never returns null.
        // TESTABILITY: internal static with CopyRuleDto param -- directly testable (no NT8 deps).
        internal static string[] ResolveFollowerNames(CopyRuleDto dto)
        {
            if (dto.FollowerAccountNames == null) // (1)
                return Array.Empty<string>();
            return dto.FollowerAccountNames;
        }

        // TB-T7a: absorbed from DtoToRule -- null guard + ATM mode map builder.
        // B8 T2: backward compat with B6/B7 XML (no FollowerAtmModeNames element).
        // CYC=3: null guard(1) + for loop(2) + IsNullOrEmpty(3).
        // JS-002 compliant: never returns null.
        // TESTABILITY: internal static with CopyRuleDto param -- directly testable (no NT8 deps).
        internal static Dictionary<string, FollowerAtmMode> ResolveAtmMap(CopyRuleDto dto)
        {
            var atmMap = new Dictionary<string, FollowerAtmMode>();
            if (dto.FollowerAtmModeNames == null) // (1)
                return atmMap;
            string[] names = ResolveFollowerNames(dto);
            for (int i = 0; i < dto.FollowerAtmModeNames.Length && i < names.Length; i++) // (2)
            {
                string accName = names[i];
                if (!string.IsNullOrEmpty(accName)) // (3)
                    atmMap[accName] = ParseAtmModeName(dto.FollowerAtmModeNames[i]);
            }
            return atmMap;
        }

        // TB-T7a: absorbed from DtoToRule -- null/length guard for FollowerMultipliers.
        // B8 T1: backward compat with B6/B7 XML (no FollowerMultipliers element).
        // Returns null when dto.FollowerMultipliers is null/empty -- CopyRule.Create handles null as all-1s.
        // CYC=2: null check(1) + length check(2).
        // Note on JS-002: returning null is consistent with existing CopyRule.Create contract (int[]?).
        // TESTABILITY: internal static with CopyRuleDto param -- directly testable (no NT8 deps).
        internal static int[] ResolveMultipliers(CopyRuleDto dto)
        {
            if (dto.FollowerMultipliers == null || dto.FollowerMultipliers.Length == 0) // (1)(2)
                return null;
            return dto.FollowerMultipliers;
        }

        // DW-B85: extracted from DtoToRule inner foreach to keep DtoToRule CYC at 7.
        // Returns null (Account?) when account name is not found in Account.All.
        // CYC=2: foreach(1) + if(1).
        // JS-002 compliant: Account? return type makes nullability explicit end-to-end.
        private static Account? FindFollowerAccount(string name)
        {
            foreach (var acc in Account.All)
            {
                if (acc.Name == name)
                    return acc;
            }
            return null;
        }

        // -- B6: Public persistence API --------------------------------------

        /// <summary>
        /// Serializes the current rule set to an XML file.
        /// Called from TradeCopierWindow.OnDestroyed() on the NT main thread.
        /// Swallows IOException to prevent NT shutdown crash on I/O failure.
        /// No lock keyword -- called only from NT main thread at shutdown.
        /// CYC = 2 (try/catch = 1 branch)
        /// </summary>
        public void SaveRules(string overridePath = null)
        {
            try
            {
                var path = GetPersistencePath(overridePath);
                var dir = Path.GetDirectoryName(path);
                if (dir != null)
                    Directory.CreateDirectory(dir);

                var container = new CopyRulesContainer();
                foreach (var rule in _rules)
                    container.Rules.Add(RuleToDto(rule));
                container.CopyEnabled = _isCopyEnabled; // B54: persist enabled state

                var serializer = new XmlSerializer(typeof(CopyRulesContainer));
                var xml = string.Empty;
                using (var writer = new System.IO.StringWriter())
                {
                    serializer.Serialize(writer, container);
                    xml = writer.ToString();
                }
                File.WriteAllText(path, xml);
            }
            catch (Exception)
            {
                // Swallow IO/serialization errors -- must not crash NT on shutdown
            }
        }

        /// <summary>
        /// Deserializes rules from an XML file into _rules. Idempotent: clears _rules and
        /// re-reads from disk on every call. Safe to call from Panel.OnLoaded and Window.OnLoaded
        /// independently -- each call produces the same _rules state from the same XML file.
        /// No lock keyword -- UI-thread-only; _rules is ConcurrentBag (thread-safe Add).
        /// CYC = 4 (File.Exists guard + try/catch + null-check + foreach)
        /// </summary>
        public void LoadRules(string overridePath = null)
        {
            _rules = new ConcurrentBag<CopyRule>(); // DW-B102: idempotent clear -- each caller gets a fresh read
            _resolvedFollowers.Clear(); // B127: invalidate lazy-resolve cache on rule reload (DW-PTT-BE-FIX-01)

            var path = GetPersistencePath(overridePath);
            if (!File.Exists(path))
                return;

            try
            {
                var xml = File.ReadAllText(path);
                var serializer = new XmlSerializer(typeof(CopyRulesContainer));
                using (var reader = new System.IO.StringReader(xml))
                {
                    var container = (CopyRulesContainer)serializer.Deserialize(reader);
                    if (container != null && container.Rules != null)
                    {
                        foreach (var dto in container.Rules)
                            _rules.Add(DtoToRule(dto));
                        _isCopyEnabled = container.CopyEnabled; // B54: restore enabled state
                        CopyEnabledChanged?.Invoke(_isCopyEnabled); // B54: sync UI buttons
                    }
                }
            }
            catch (Exception)
            {
                // Swallow deserialization errors -- missing/corrupt file is non-fatal
            }
        }

        // DW-NEW-08 Option E: thin dispatcher gate.
        // CYC=4: (1) PTT-Copy fill guard (B194-F1), (2) terminal-state check,
        //        (3) follower-account check, (4) NakedPositionDetector call.
        // JS-021: no lock. JS-001: no throw. JS-033: synchronous void.
        private void TryNakedDetect(OrderEventArgs e)
        {
            // B194-F1: PTT-Copy fill guard -- follower fill has no brackets yet; brackets arrive
            // after leader Target1:Accepted. Never trigger NakedPositionDetector on own copy fill.
            // DW-B194-01: closed by this guard.
            if (e.Order.Name == "PTT-Copy" && e.Order.OrderState == OrderState.Filled)
                return;
            // B200-NAKED-01 FIX: ATM-mode follower entry is named "Entry" (StartAtmStrategy constraint,
            // SendCopyWithAtm L5194). After "Entry" fills, ATM brackets (Target1/SimStop1 etc) are placed
            // by NT8 asynchronously -- they take 50-200ms to appear in acc.Orders.
            // NakedPositionDetector fires in that window, sees no brackets, flattens the position.
            // Fix: skip NakedDetect on "Entry" fills for follower accounts -- same logic as PTT-Copy guard.
            // JS-021: no lock. JS-001: no throw. CYC delta=+1 (new branch).
            if (e.Order.Name == "Entry"
                && e.Order.OrderState == OrderState.Filled
                && IsFollowerAccount(e.Order.Account))
                return;
            if (
                e.Order.OrderState != OrderState.Filled
                && e.Order.OrderState != OrderState.Cancelled
                && e.Order.OrderState != OrderState.Rejected
            )
                return;
            if (!IsFollowerAccount(e.Order.Account))
                return;
            NakedPositionDetector(e.Order.Account);
        }

        // DW-NEW-08 Option E: check and queue flatten if follower account is naked.
        // CYC<=7: (1) acct null, (2) HasNakedPosition, (3) B208-FIX-B BE-swap guard,
        //         (4) IsFlattenInFlightOrRecent, (5) debounce check,
        //         (6) AddOrUpdate atomic, (7) instrument null check.
        // JS-021: no lock -- ConcurrentDictionary atomic ops only.
        // JS-001: no throw. JS-033: synchronous void.
        // NT8 bans: no Account.Change(), no AtmStrategyCreate(), no AtmStrategyChangeStopTarget().
        // Note: Environment.TickCount is int (ms since boot, ~25d wrap). Cast to long for dict.
        //       500ms grace window is far shorter than wrap period -- safe.
        private void NakedPositionDetector(Account acct)
        {
            if (acct == null)
                return;
            if (!HasNakedPosition(acct))
                return;

            // B208-FIX-B: suppress flatten during BE-swap cancel window.
            // PttBreakEvenSwap.Execute sets this flag before CancelQxBrackets, clears in finally.
            // Without this, SimStop1/SimStop2 cancel callbacks fire NakedPositionDetector while
            // PTT-BE-Stop-N has not yet been submitted, triggering a spurious PTT-Flatten.
            // CYC delta: +1 (this branch). JS-021: IsBeSwapInProgress is lock-free.
            if (IsBeSwapInProgress(acct.Name))
                return;

            // B206-FIX-A: resolve instrument early so IsFlattenInFlightOrRecent can be checked
            // before the debounce. FindOpenPositionInstrument is CYC=1, pure read, safe here.
            Instrument instr = FindOpenPositionInstrument(acct);

            // B206-FIX-A: if a PTT-Flatten is already in-flight or was submitted within 2000ms,
            // skip. Root cause: NT8 position state lags after manual close -- bracket Cancelled
            // callbacks fire TryNakedDetect while acct.Positions still shows Qty>0 with zero
            // brackets, so HasNakedPosition=true even though the account is already flat.
            // IsFlattenInFlightOrRecent is the three-signal composite (intent flag +
            // HasActivePttFlatten order-state + HasRecentFlatten 2000ms timestamp) already
            // guarding Lane D (L2963) and Lane E (L3039). Reuses existing tested helper.
            // CYC delta: +1 (this branch). NakedPositionDetector CYC: 5->6. JS-021: lock-free.
            if (instr != null && IsFlattenInFlightOrRecent(acct, instr))
                return;

            // debounce: skip if already queued within 500ms grace window
            long now = (long)(int)Environment.TickCount;
            const long GraceMs = 500L;
            long last = _nakedDetectLastQueuedTicks.GetOrAdd(acct.Name, 0L);
            if (now - last < GraceMs)
                return;

            // atomic update: record dispatch time
            _nakedDetectLastQueuedTicks.AddOrUpdate(acct.Name, now, (_, __) => now);

            // marshal flatten to UI thread -- same pattern as other flatten paths
            if (instr != null)
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    FlattenOneAccount(acct, instr)
                );
        }

        // B194-F2: recognize all bracket stop patterns.
        // (1) NT8 SIM ATM stops: TriggerPending + Market + StopPrice>0.
        //     StopPrice>0 guard required -- real market entries also have TriggerPending+Market
        //     but StopPrice=0. This discriminates bracket stops from market entries.
        // (2) Live stops: Working|Submitted + StopMarket|StopLimit.
        // CYC=3: base(1) + TriggerPending-branch(1) + WorkingStop-branch(1).
        // JS-021: no lock. JS-001: no throw. NT8: o.StopPrice confirmed AddOnBase available
        //         via B194-DIRECT commit a434679a.
        // 500ms wrap-period note: no TickCount usage here.
        // DW-B194-02: closed by this helper.
        // internal: accessible to xUnit via InternalsVisibleTo("PropTraderTools.Tests") at L46.
        internal static bool IsBracketStopOrder(Order o)
        {
            if (o.OrderState == OrderState.TriggerPending
                && o.OrderType == OrderType.Market
                && o.StopPrice > 0)
                return true;
            // B208-FIX-F-A: added Accepted -- NT8 SIM followers transition Submitted->Accepted before Working.
            // PTT-BE-Stop-N orders sit in Accepted for 50-200ms after resubmit. Without this,
            // HasNakedPosition returns true during that window and fires a spurious PTT-Flatten.
            if ((o.OrderState == OrderState.Working
                    || o.OrderState == OrderState.Submitted
                    || o.OrderState == OrderState.Accepted)
                && (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit))
                return true;
            return false;
        }

        // DW-NEW-08 Option E: naked position check.
        // Returns true if acct has a non-flat position AND zero bracket stops or targets.
        // B194-F2: inner loop refactored to use IsBracketStopOrder(o) helper.
        //   Recognizes TriggerPending+Market+StopPrice>0 (NT8 SIM ATM stops) as bracket stops.
        //   CYC=7 post-F2 (was 8): base=1, foreach-pos=+1, qty=+1, no-pos=+1,
        //                           foreach-ord=+1, IsBracketStopOrder=+1, Limit-check=+1.
        // JS-021: no lock. JS-002: returns bool. JS-001: no throw.
        // NT8: OrderState.Submitted = pre-Working (Accepted / partially acknowledged).
        // internal: accessible to xUnit via InternalsVisibleTo("PropTraderTools.Tests") at L46.
        internal static bool HasNakedPosition(Account acct)
        {
            bool hasPosition = false;
            foreach (Position p in acct.Positions)
            {
                if (p.Quantity > 0)
                {
                    hasPosition = true;
                    break;
                }
            }
            if (!hasPosition)
                return false;

            bool hasStop = false;
            bool hasTarget = false;
            foreach (Order o in acct.Orders)
            {
                // B194-F2: IsBracketStopOrder recognizes TriggerPending+Market+StopPrice>0
                //          (NT8 SIM ATM stops) in addition to Working|Submitted StopMarket|StopLimit.
                if (IsBracketStopOrder(o)) { hasStop = true; continue; }
                if ((o.OrderState == OrderState.Working || o.OrderState == OrderState.Submitted)
                    && o.OrderType == OrderType.Limit)
                    hasTarget = true;
            }
            return !hasStop && !hasTarget;
        }

        // DW-NEW-08 Option E: return instrument of first non-flat position, or null if all flat.
        // CYC=1. JS-002 compliant: return type Instrument (nullable reference, no raw return null).
        // Caller uses is not null guard (see NakedPositionDetector above).
        private static Instrument FindOpenPositionInstrument(Account acct) =>
            acct.Positions.FirstOrDefault(static p => p.Quantity > 0)?.Instrument;

        // DW-NEW-08 Option D: cancel all Working/Accepted entry orders for follower+instrument before dispatching new entry.
        // Parks the dispatch intent in _pendingDispatchDrains until all cancel acks arrive.
        // CYC=7: (1) null guard, (2) no-entry fast path, (2-flat) B200-CLOSE-LANED-01-V9: flatten guard,
        //        (2-be) B212-REENTRY-FOLLOWER: HasFilledBeTargetFast abort on fast-path,
        //        (2b) fastAtm is Named, (3) foreach cancel loop.
        // DW-B195-02: fast-path (2) now ATM-aware -- same pattern as SubmitDrainedEntry (B195-T1).
        // F5-repair: dead (4) cancelCount==0 branch removed. entryCandidates always non-empty here.
        // JS-021: no lock(). GetCloneAtmMode reads volatile field -- thread-safe. ConcurrentDictionary + Interlocked only.
        // NT8: Account.Cancel(Order[]) is AddOnBase available. Confirmed NT8_FULL_REFERENCE.md.
        // NT8: SendCopyWithAtm uses StartAtmStrategy (AddOnBase available, line 4661).
        // ASCII-only: "-fast-atm-" literal. No Unicode.
        private void DrainThenDispatch(
            Account follower,
            Instrument instrument,
            int qty,
            double price,
            OrderAction action,
            OrderType orderType
        )
        {
            if (follower == null || instrument == null) // (1)
                return;

            // F2-repair: restrict to PTT-Copy Limit/StopLimit entries only.
            // Stop brackets (StopMarket) and non-PTT-Copy orders are excluded.
            var entryCandidates = ActiveOrders(follower)
                .Where(o =>
                    o.Instrument == instrument
                    // B194-F3: include Submitted -- PTT-Copy from completed drain-2 may be Submitted.
                    && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted
                        || o.OrderState == OrderState.Submitted)
                    && (o.OrderType == OrderType.Limit || o.OrderType == OrderType.StopLimit)
                    && (
                        o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal) || o.Name == "Entry"
                    )
                ) // R2-F2: include Clone mode Entry orders (FindFollowerEntryOrder line 3717)
                .ToList();

            if (!entryCandidates.Any()) // (2)
            {
                if (IsFlattenInFlightOrRecent(follower, instrument)) // (2-flat) B200-CLOSE-LANED-01-V10: combined order-state + timestamp guard
                    return;
                // B212-REENTRY-FOLLOWER: fast-path mirrors SubmitDrainedEntry gate (2d).
                // If follower BE-targets have filled, suppress any fast-path dispatch.
                // Lock-free ConcurrentDictionary.TryGetValue. CYC +1. JS-021. JS-001.
                if (HasFilledBeTargetFast(follower)) // (2-be)
                    return;
                // DW-B195-02: fast-path must also respect Clone ATM mode.
                // Same pattern as SubmitDrainedEntry (B195-T1 drain-completion path).
                // JS-021: no lock. GetCloneAtmMode reads volatile field -- thread-safe.
                // JS-001: no throw. ASCII-only: "-fast-atm-" literal.
                // NT8: SendCopyWithAtm uses "Entry" name internally (line 4680).
                // NOTE: "now" variable at line 6613 is declared AFTER this early return and is
                //       unreachable here. Expression is inlined directly.
                var fastAtm = GetCloneAtmMode(); // (2a) volatile read
                if (fastAtm is FollowerAtmMode.Named namedFastAtm) // (2b) ATM branch -- adds CYC +1
                {
                    var sig = CopySignal.Create(
                        action,
                        orderType,
                        qty,
                        price,
                        follower.Name + "-fast-atm-" + (long)(int)Environment.TickCount
                    );
                    SendCopyWithAtm(follower, instrument, in sig, namedFastAtm);
                }
                else
                {
                    SubmitEntryDirect(follower, instrument, qty, price, action, orderType);
                }
                return;
            }

            string acctKey = follower.Name;
            long now = (long)(int)Environment.TickCount;
            // F3: build drainedIds list before inserting into dict.
            var drainedIds = entryCandidates.Select(e => e.OrderId).ToList();
            // F4-repair: PendingCancelCount set to entryCandidates.Count BEFORE dict insert.
            // Prevents TOCTOU race where cancel ack arrives before Interlocked.Exchange.
            var payload = new PendingDispatchDrain(
                acctKey,
                instrument,
                qty,
                price,
                action,
                orderType,
                drainedIds,
                follower,
                pendingCancelCount: entryCandidates.Count,
                now
            );
            if (!_pendingDispatchDrains.TryAdd(acctKey, payload))
                return;

            foreach (var e in entryCandidates) // (3)
            {
                _drainOwnedOrderIds.TryAdd(e.OrderId, 0); // F3: track drain-owned
                follower.Cancel(new Order[] { e });
            }
            // No Interlocked.Exchange -- count was set correctly at construction (F4).

            NinjaTrader.Code.Output.Process(
                "[DRAIN] acct=" + acctKey + " cancel-sent=" + entryCandidates.Count,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
        }

        // DW-NEW-08 Option D: direct submit when no drain needed.
        // CYC=2: (1) null guard on created order, (2) orderType ternary for limitPx/stopPx.
        // NT8: Account.CreateOrder + Account.Submit confirmed AddOnBase available.
        // NO Account.Change(). NO AtmStrategyCreate(). NO AtmStrategyChangeStopTarget().
        private void SubmitEntryDirect(
            Account follower,
            Instrument instrument,
            int qty,
            double price,
            OrderAction action,
            OrderType orderType
        )
        {
            double limitPx = orderType == OrderType.StopLimit ? 0.0 : price; // (2)
            double stopPx = orderType == OrderType.StopLimit ? price : 0.0;
            var order = follower.CreateOrder(
                instrument,
                action,
                orderType,
                OrderEntry.Manual,
                TimeInForce.Day,
                qty,
                limitPx,
                stopPx,
                null,
                "PTT-Copy",
                DateTime.MaxValue,
                null
            );
            if (order == null) // (1)
                return;

            follower.Submit(new[] { order });
            NinjaTrader.Code.Output.Process(
                "[DRAIN-SUBMIT] acct="
                    + follower.Name
                    + " instr="
                    + instrument.FullName
                    + " price="
                    + price,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
        }

        // DW-NEW-08 Option D: handle cancel acknowledgment for drain-pending accounts.
        // Called directly from OnOrderUpdate -- NOT an event handler. Synchronous void. NOT async void (JS-033).
        // CYC=3: (1) drain not found early return, (2) underflow guard, (3) remaining==0 fire.
        // JS-021: no lock(). Interlocked.Decrement is atomic.
        private void OnDrainCancelAck(string acctKey)
        {
            if (!_pendingDispatchDrains.TryGetValue(acctKey, out var payload)) // (1)
                return;

            int remaining = Interlocked.Decrement(ref payload.PendingCancelCount);
            if (remaining < 0) // (2)
            {
                NinjaTrader.Code.Output.Process(
                    "[DRAIN-UNDERFLOW] acct=" + acctKey,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return;
            }

            if (remaining == 0) // (3)
                SubmitDrainedEntry(acctKey);
        }

        // B195: SubmitDrainedEntry is now ATM-mode-aware.
        // CYC=8: (1) TryRemove fails early return, (2) FollowerAccount null early return,
        //        (2b) B200-CLOSE-LANED-01-V9: HasActivePttFlatten abort,
        //        (2c) B212-DW03: IsBeSwapInProgress abort -- bracket cancels from PttBeSwapAll
        //             falsely complete the drain count and would submit a spurious re-entry.
        //        (2d) B212-REENTRY-FOLLOWER: HasFilledBeTargetFast abort -- stale drain parked during
        //             pre-BE drag fires after BE-All closes followers via BE-target fill race.
        //        (3) ATM mode branch -- Named -> SendCopyWithAtm, else -> SubmitEntryDirect,
        //        (4) F3 cleanup foreach (after submit).
        // Option A: GetCloneAtmMode() re-fetched at dispatch time (volatile read, thread-safe, JS-021).
        // JS-001: no throw. JS-033: not async. JS-066: CYC=8 (<=8 ceiling). ASCII-only.
        // JS-028: CYC=8 <= 8 ceiling. B212-REENTRY-FOLLOWER: CYC delta +1 (7->8).
        // NT8: SendCopyWithAtm uses StartAtmStrategy (AddOnBase-available, NT8_FULL_REFERENCE.md line 17).
        // NT8: "Entry" order name constraint satisfied inside SendCopyWithAtm (line 4680).
        // R3-F2: drain IDs cleaned AFTER dispatch -- IDs preserved if dispatch fails.
        private void SubmitDrainedEntry(string acctKey)
        {
            if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
                return;

            var follower = payload.FollowerAccount;
            if (follower == null) // (2)
                return;
            if (IsFlattenInFlightOrRecent(follower, payload.Instrument)) // (2b) B200-CLOSE-LANED-01-V10: combined order-state + timestamp guard
                return;

            // B212-DW03: abort drain during active BE swap -- bracket cancels from
            // PttBeSwapAll falsely complete the drain and submit a spurious re-entry.
            if (IsBeSwapInProgress(follower.Name)) // (2c)
                return;

            // B212-REENTRY-FOLLOWER: abort drain when follower BE-targets recently filled.
            // Race: stale drain parked during a pre-BE Entry price-drag gets its cancel ack
            // from the BE-sweep BEFORE TryAbortLeaderFlatDrains can abort it -> SubmitDrainedEntry
            // fires spuriously. HasFilledBeTargetFast is lock-free ConcurrentDictionary.TryGetValue.
            // Count set by TryRecordBeTargetFill, cleared by TryEvictFollowerBeSlot on flat.
            // JS-021: no lock. JS-001: no throw. ASCII-only.
            if (HasFilledBeTargetFast(follower)) // (2d)
                return;

            var atmMode = GetCloneAtmMode(); // (3) re-fetch at dispatch time -- volatile read, thread-safe
            if (atmMode is FollowerAtmMode.Named namedAtm) // (3a) Clone+ATM path
            {
                var signal = CopySignal.Create(
                    payload.Action,
                    payload.OrderType,
                    payload.Qty,
                    payload.Price,
                    payload.FollowerAcctKey + "-drain-atm-" + payload.TimestampTicks
                );
                Order? newOrder = SendCopyWithAtm(follower, payload.Instrument, in signal, namedAtm);
                if (newOrder != null)                                                     // T3: CYC 5->6
                    _drainFollowerEntryOrders.TryAdd(acctKey, newOrder);
            }
            else // (3b) non-ATM path (Inherit or no Clone ATM configured)
            {
                SubmitEntryDirect(
                    follower,
                    payload.Instrument,
                    payload.Qty,
                    payload.Price,
                    payload.Action,
                    payload.OrderType
                );
            }

            // R3-F2: clear drain-owned IDs AFTER dispatch so IDs are preserved if dispatch fails.
            foreach (var id in payload.DrainedOrderIds) // (4)
                _drainOwnedOrderIds.TryRemove(id, out _);
        }

        // R2-F1: clean _drainOwnedOrderIds for fill-aborted drain payloads.
        // Called from OnOrderUpdate Filled branch to prevent permanent ID leak.
        // CYC=3: base(1) + TryRemove guard(1) + foreach(1). JS-021: no lock().
        private void AbortDrainOnFill(string acctKey)
        {
            _drainFollowerEntryOrders.TryRemove(acctKey, out _);  // T3: cleanup stored entry order on abort
            if (_pendingDispatchDrains.TryRemove(acctKey, out var payload))
                foreach (var id in payload.DrainedOrderIds)
                    _drainOwnedOrderIds.TryRemove(id, out _);
        }

        // DW-NEW-08 Option D: watchdog for stuck drains. Piggybacked in OnOrderUpdate. No System.Threading.Timer.
        // CYC=4: (1) IsEmpty fast-path, (2) foreach loop, (3) timestamp comparison, (4) F3 cleanup foreach.
        // JS-021: no lock(). ConcurrentDictionary enumeration is thread-safe.
        private void TryDrainWatchdog()
        {
            if (_pendingDispatchDrains.IsEmpty) // (1)
                return;

            long now = (long)(int)Environment.TickCount;
            foreach (var kv in _pendingDispatchDrains) // (2)
            {
                if (now - kv.Value.TimestampTicks > 2000L) // (3)
                {
                    // F3-repair: clear drain-owned IDs before removing timed-out drain.
                    foreach (var id in kv.Value.DrainedOrderIds) // (4)
                        _drainOwnedOrderIds.TryRemove(id, out _);
                    _drainFollowerEntryOrders.TryRemove(kv.Key, out _);  // T3: cleanup stored entry order on watchdog expiry
                    _pendingDispatchDrains.TryRemove(kv.Key, out _);
                    NinjaTrader.Code.Output.Process(
                        "[DRAIN-TIMEOUT] acct=" + kv.Key,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
                }
            }
        }

        // DW-NEW-08 Option D: payload for cancel-before-dispatch drain.
        // Stores the dispatch intent while cancels are in-flight.
        // CYC=0 (data class -- no logic methods).
        // PendingCancelCount is a plain int field (not property) because
        // Interlocked.Decrement requires ref int; properties cannot be passed by ref.
        private sealed class PendingDispatchDrain
        {
            internal string FollowerAcctKey { get; private set; }
            internal Instrument Instrument { get; private set; }
            internal int Qty { get; private set; }
            internal double Price { get; private set; }
            internal OrderAction Action { get; private set; }
            internal OrderType OrderType { get; private set; }

            // F3-repair: track order IDs owned by this drain for cleanup in SubmitDrainedEntry/TryDrainWatchdog.
            // NT8 Order.OrderId is string (NT8_FULL_REFERENCE.md line 864).
            internal IReadOnlyList<string> DrainedOrderIds { get; private set; }
            internal Account FollowerAccount { get; private set; }
            internal int PendingCancelCount; // mutable -- Interlocked.Decrement/Increment
            internal long TimestampTicks { get; private set; }

            internal PendingDispatchDrain(
                string followerAcctKey,
                Instrument instrument,
                int qty,
                double price,
                OrderAction action,
                OrderType orderType,
                IReadOnlyList<string> drainedOrderIds,
                Account followerAccount,
                int pendingCancelCount,
                long timestampTicks
            )
            {
                FollowerAcctKey = followerAcctKey;
                Instrument = instrument;
                Qty = qty;
                Price = price;
                Action = action;
                OrderType = orderType;
                DrainedOrderIds = drainedOrderIds;
                FollowerAccount = followerAccount;
                PendingCancelCount = pendingCancelCount;
                TimestampTicks = timestampTicks;
            }
        }

        // BUG-0911-A: extracted for testability -- mirrors FlattenFollower delegate-injection pattern.
        // CYC=3: (1) foreach, (2) isFlat true-branch (skip), (3) isFlat false-branch (flattenOne).
        // JS-021: no lock (static, pure iteration). JS-001: no throw. ASCII-only.
        private static void FlatGuardLoop<TAccount>(
            Func<IEnumerable<TAccount>> getAccounts,
            Func<TAccount, bool> isFlat,
            Action<TAccount> flattenOne)
        {
            foreach (var acc in getAccounts())
            {
                if (isFlat(acc))
                {
                    NinjaTrader.Code.Output.Process(
                        "[FLAT-GUARD] skip flat, already flat on " + acc.ToString(),
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                    continue;
                }
                flattenOne(acc);
            }
        }
    }

    // B180: test-seam stub. CopyEngineTests.B154T2ContractTests calls
    // PttDragOrphanPredicates.IsDragOrphanName(name) -- the extraction was planned but
    // the class was never created. This stub provides the missing type so the test
    // project compiles. Logic mirrors IsPttDragOrphanCancellableTestable name-only check.
    // CYC=2: (1) null/empty guard, (2) StartsWith OR chain.
    // JS-021: no lock. JS-001: no throw. JS-002: returns bool. ASCII-only.
    internal static class PttDragOrphanPredicates
    {
        internal static bool IsDragOrphanName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            return name == "PTT-TGT-Drag"
                || name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)
                || name == "PTT-STP-Drag"
                || name.StartsWith("PTT-STP-Drag-", StringComparison.Ordinal);
        }
    }
}
