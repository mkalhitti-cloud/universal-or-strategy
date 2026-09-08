// C:\WSGTA\universal-or-strategy\src\PropTraderTools\Core\PttContracts.cs
// B33 -- Modular Independence: shared contracts, event hub, event args
// NT8-044: using System required -- NT8 does not auto-inject System namespace

using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    // -------------------------------------------------------------------------
    // INTERFACES
    // -------------------------------------------------------------------------

    /// <summary>
    /// Contract for every PTT trading module.
    /// Initialize() on panel attach; Teardown() on panel detach.
    /// All calls on UI thread only.
    /// </summary>
    public interface IPttModule
    {
        /// <summary>Stable module identifier ("BE", "TRIM", "FLAT", "CANCEL", "COPY").</summary>
        string ModuleId { get; }

        /// <summary>When false, Execute() is a no-op. Controlled by license bool.</summary>
        bool IsEnabled { get; }

        /// <summary>Subscribe to PttBus events here. UI thread only.</summary>
        void Initialize(IPttHostContext ctx);

        /// <summary>Unsubscribe all PttBus events here. UI thread only.</summary>
        void Teardown();

        /// <summary>Perform the module's action using the supplied context. UI thread only.</summary>
        void Execute(IPttHostContext ctx);

        /// <summary>Enable or disable this module (wired to license bool). CYC=1.</summary>
        void SetEnabled(bool enabled);
    }

    /// <summary>
    /// Read-only trading context passed to module Execute() calls.
    /// Implemented by TradeCopierPanel (T7).
    /// NT8-021: AllAccounts populated in panel Initialize handler, NOT in constructor.
    /// </summary>
    public interface IPttHostContext
    {
        /// <summary>Leader account (source of truth for sizing and position reads).</summary>
        Account LeaderAccount { get; }

        /// <summary>Currently tracked instrument (e.g. NQ 09-26).</summary>
        Instrument Instrument { get; }

        /// <summary>Leader + all follower accounts. Built at panel init time.</summary>
        IReadOnlyList<Account> AllAccounts { get; }

        // B34 additions -- buffer props and live market quote.
        /// <summary>Break-even buffer in ticks. From TradeCopierPanel._beBuffer.</summary>
        int BeBuffer { get; }

        /// <summary>Trim buffer in ticks. From TradeCopierPanel._trimBuffer.</summary>
        int TrimBuffer { get; }

        /// <summary>Flatten buffer in ticks. From TradeCopierPanel._flattenBuffer.</summary>
        int FlatBuffer { get; }

        /// <summary>Current ask price from instrument market data. Returns 0.0 if no quote.</summary>
        double Ask { get; }

        /// <summary>Current bid price from instrument market data. Returns 0.0 if no quote.</summary>
        double Bid { get; }

        /// <summary>Display a warning in the panel status bar. Call from UI thread only.</summary>
        void WarnUser(string message);
    }

    /// <summary>
    /// Relay contract for CopyEngine -- the 4 public methods PttCopier calls.
    /// Implemented by CopyEngine (T8). Used as PttCopier constructor param so tests
    /// can inject MockCopyEngineRelay : ICopyEngine without subclassing CopyEngine.
    /// T6-TEST-01 fix: ICopyEngine enables testable constructor injection.
    /// NT8: interface members are void -- no LINQ, no async, no init accessors.
    /// </summary>
    public interface ICopyEngine
    {
        /// <summary>Fan out BE stop to all follower accounts.</summary>
        void RelayBe(BeEventArgs e);

        /// <summary>Fan out trim to all follower accounts.</summary>
        void RelayTrim(TrimEventArgs e);

        /// <summary>Fan out flatten to all follower accounts.</summary>
        void RelayFlatten(FlatEventArgs e);

        /// <summary>Fan out cancel entries to all follower accounts.</summary>
        void RelayCancel(CancelEventArgs e);
    }

    // -------------------------------------------------------------------------
    // EVENT HUB
    // -------------------------------------------------------------------------

    /// <summary>
    /// Static CLR event hub for PTT module communication.
    ///
    /// THREADING CONTRACT:
    ///   Subscribe (+= ) only from IPttModule.Initialize()  -- UI thread
    ///   Unsubscribe (-=) only from IPttModule.Teardown()   -- UI thread
    ///   Fire (?.Invoke) from module.Execute()              -- UI thread
    ///
    /// JS-021: No lock needed. CLR += / -= are atomic (new delegate list).
    ///         All sub/unsub on same UI thread -- zero contention.
    /// NT8-043: local-copy-then-null-check pattern used in Raise* methods.
    ///          Avoids any null-conditional assignment edge cases on C# 7.3.
    /// </summary>
    public static class PttBus
    {
        public static event EventHandler<BeEventArgs> BeFired;
        public static event EventHandler<TrimEventArgs> TrimFired;
        public static event EventHandler<FlatEventArgs> FlatFired;
        public static event EventHandler<CancelEventArgs> CancelFired;
        internal static event EventHandler<QuickExitEventArgs> QuickExitFired;

        internal static void RaiseBe(object sender, BeEventArgs e)
        {
            var h = BeFired;
            if (h != null)
                h(sender, e);
        }

        internal static void RaiseTrim(object sender, TrimEventArgs e)
        {
            var h = TrimFired;
            if (h != null)
                h(sender, e);
        }

        internal static void RaiseFlatted(object sender, FlatEventArgs e)
        {
            var h = FlatFired;
            if (h != null)
                h(sender, e);
        }

        internal static void RaiseCancel(object sender, CancelEventArgs e)
        {
            var h = CancelFired;
            if (h != null)
                h(sender, e);
        }

        internal static void RaiseQuickExit(object sender, QuickExitEventArgs e)
        {
            var h = QuickExitFired;
            if (h != null)
                h(sender, e);
        }

        // B42: Action<T> (not EventHandler<T>) because FillSignalEventArgs is a readonly struct,
        // not an EventArgs subclass. JS-021: CLR += / -= are atomic -- no lock needed.
        // PttFollowerStrategy (separate NT8 compilation unit) subscribes at State.Realtime.
        public static event Action<FillSignalEventArgs> FillSignal;

        // B42: NT8-043 local-copy-then-null-check pattern. CYC=2. JS-021: no lock.
        public static void RaiseFillSignal(FillSignalEventArgs args)
        {
            var h = FillSignal;
            if (h != null)
                h(args);
        }
    }

    // -------------------------------------------------------------------------
    // EVENT ARGS
    // NT8-001: use {get; private set;} + constructor -- init accessor is BANNED in NT8
    // NT8-002: class : EventArgs -- NO records
    // -------------------------------------------------------------------------

    public class BeEventArgs : EventArgs
    {
        public Instrument Instrument { get; private set; }
        public double BePrice { get; private set; }
        public double EntryPrice { get; private set; }
        public bool IsLong { get; private set; }
        public string OcoGroup { get; private set; }

        public BeEventArgs(
            Instrument instr,
            double bePrice,
            double entryPrice,
            bool isLong,
            string ocoGroup
        )
        {
            Instrument = instr;
            BePrice = bePrice;
            EntryPrice = entryPrice;
            IsLong = isLong;
            OcoGroup = ocoGroup ?? string.Empty;
        }
    }

    public class TrimEventArgs : EventArgs
    {
        public Instrument Instrument { get; private set; }
        public int TrimPercent { get; private set; }
        public int ActualQty { get; private set; }

        public TrimEventArgs(Instrument instr, int trimPercent, int actualQty)
        {
            Instrument = instr;
            TrimPercent = trimPercent;
            ActualQty = actualQty;
        }
    }

    public class FlatEventArgs : EventArgs
    {
        public Instrument Instrument { get; private set; }

        public FlatEventArgs(Instrument instr)
        {
            Instrument = instr;
        }
    }

    public class CancelEventArgs : EventArgs
    {
        public Instrument Instrument { get; private set; }

        public CancelEventArgs(Instrument instr)
        {
            Instrument = instr;
        }
    }

    // B41: QuickExitEventArgs -- 7 fields. NT8-001: private set (no init). NT8-002: sealed class.
    // Card B: TickSize field enables TradeCopierWindow back-calc without polling.
    public sealed class QuickExitEventArgs : EventArgs
    {
        public Instrument Instrument { get; private set; }
        public double EntryPrice { get; private set; }
        public double T1Price { get; private set; }
        public double T2Price { get; private set; }
        public bool IsLong { get; private set; }
        public string OcoId { get; private set; }
        public double TickSize { get; private set; }

        public QuickExitEventArgs(
            Instrument instr,
            double entryPrice,
            double t1Price,
            double t2Price,
            bool isLong,
            string ocoId,
            double tickSize
        )
        {
            Instrument = instr;
            EntryPrice = entryPrice;
            T1Price = t1Price;
            T2Price = t2Price;
            IsLong = isLong;
            OcoId = ocoId ?? string.Empty;
            TickSize = tickSize;
        }
    }

    // B42: FillSignalEventArgs -- carries fill data from CopyEngine to PttFollowerStrategy.
    // NT8-001: { get; private set; } + constructor (init accessor BANNED in NT8).
    // NT8-NEW: 'readonly struct' + { get; private set; } = CS8341 in NT8 Roslyn (auto-props must be
    //          readonly in readonly struct). Fix: drop 'readonly' keyword from struct declaration.
    //          External immutability preserved by private set; on all 6 properties.
    // JS-010: private constructor + public static Create() factory (signal struct rule).
    // NT8-002: struct (not record) -- NT8 compiler bans abstract/sealed records.
    public struct FillSignalEventArgs
    {
        public Account Account { get; private set; }
        public Instrument Instrument { get; private set; }
        public string AtmTemplateName { get; private set; }
        public OrderAction OrderAction { get; private set; }
        public int Quantity { get; private set; }
        public string EntryOrderId { get; private set; }

        private FillSignalEventArgs(
            Account account,
            Instrument instrument,
            string atmTemplateName,
            OrderAction orderAction,
            int quantity,
            string entryOrderId
        )
        {
            Account = account;
            Instrument = instrument;
            AtmTemplateName = atmTemplateName ?? string.Empty;
            OrderAction = orderAction;
            Quantity = quantity;
            EntryOrderId = entryOrderId ?? string.Empty;
        }

        // JS-010: smart constructor -- only valid construction path.
        public static FillSignalEventArgs Create(
            Account account,
            Instrument instrument,
            string atmTemplateName,
            OrderAction orderAction,
            int quantity,
            string entryOrderId
        ) =>
            new FillSignalEventArgs(
                account,
                instrument,
                atmTemplateName,
                orderAction,
                quantity,
                entryOrderId
            );
    }

    // -------------------------------------------------------------------------
    // ORDER NAME PREFIXES
    // -------------------------------------------------------------------------

    /// <summary>
    /// Compile-time constants for PTT order name prefixes.
    /// Used by SnapshotTargetsPublic and related order-filtering methods.
    /// ASCII-only. JS-066: no branching -- CYC impact = 0.
    /// </summary>
    internal static class PttOrderNames
    {
        /// <summary>Quick-exit target order name prefix. "PTT-QX-T"</summary>
        internal const string PttQxTargetPrefix = "PTT-QX-T";

        /// <summary>Target order name prefix. "PTT-TGT-"</summary>
        internal const string PttTgtPrefix = "PTT-TGT-";

        /// <summary>Break-even target order name prefix. "PTT-BE-Target-"
        /// Defined for completeness -- used in PttBreakEven.cs and
        /// PttGlobalQuickExit.cs (constantification of those callers is
        /// deferred to a future block per B126 scope constraint).</summary>
        internal const string PttBeTargetPrefix = "PTT-BE-Target-";
    }
}
