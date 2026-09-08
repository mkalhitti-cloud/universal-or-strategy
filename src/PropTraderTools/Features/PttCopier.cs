// C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCopier.cs
// B33 -- PttCopier module: relay PttBus events to CopyEngine follower fan-out.
// IPttModule implementation. ModuleId = "COPY".
// Subscribes to all 4 PttBus events in Initialize(). Unsubscribes all in Teardown().
// Dependencies: Core/PttContracts.cs + NinjaTrader.Cbi + ICopyEngine (CopyEngine in T8).
// T6-TEST-01 fix: accepts ICopyEngine so tests can inject MockCopyEngineRelay.
// JS-021: no lock. JS-033: synchronous void. NT8-043: direct -= in Teardown (no null-conditional -=).

using NinjaTrader.Cbi;

namespace PropTraderTools
{
    /// <summary>
    /// Copier module. Subscribes to PttBus and relays each event to CopyEngine
    /// follower fan-out methods. No logic here -- pure relay.
    /// Teardown() unsubscribes all 4 events -- no memory leaks.
    /// </summary>
    public class PttCopier : IPttModule
    {
        public string ModuleId { get; private set; }
        public bool IsEnabled { get; private set; }

        private readonly ICopyEngine _engine;

        public PttCopier(ICopyEngine engine)
        {
            ModuleId = "COPY";
            IsEnabled = true;
            _engine = engine;
        }

        /// <summary>Set enabled state (wired by TradeCopierPanel license bool). CYC=1.</summary>
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <summary>
        /// Subscribe to all 4 PttBus events. Called on UI thread only.
        /// CYC=1. NT8-043: += is direct assignment -- no null-conditional.
        /// JS-021: no lock -- CLR event += on single thread is atomic.
        /// </summary>
        public void Initialize(IPttHostContext ctx)
        {
            PttBus.BeFired += OnBeFired;
            PttBus.TrimFired += OnTrimFired;
            PttBus.FlatFired += OnFlatFired;
            PttBus.CancelFired += OnCancelFired;
        }

        /// <summary>
        /// No-op. PttCopier is event-driven (PttBus handlers), not Execute-driven.
        /// Execute is never called by DispatchModule for ModuleId="COPY". CYC=1.
        /// </summary>
        public void Execute(IPttHostContext ctx) { }

        /// <summary>
        /// Unsubscribe all 4 PttBus events. Called on UI thread only.
        /// CYC=1. NT8-043: direct -= NOT null-conditional -= (C# 9 syntax banned in NT8).
        /// </summary>
        public void Teardown()
        {
            PttBus.BeFired -= OnBeFired;
            PttBus.TrimFired -= OnTrimFired;
            PttBus.FlatFired -= OnFlatFired;
            PttBus.CancelFired -= OnCancelFired;
        }

        // ---------------------------------------------------------------------
        // PttBus event handlers -- each relays to CopyEngine fan-out. CYC=1 each.
        // ---------------------------------------------------------------------

        private void OnBeFired(object sender, BeEventArgs e)
        {
            if (_engine != null)
                _engine.RelayBe(e);
        }

        private void OnTrimFired(object sender, TrimEventArgs e)
        {
            if (_engine != null)
                _engine.RelayTrim(e);
        }

        private void OnFlatFired(object sender, FlatEventArgs e)
        {
            if (_engine != null)
                _engine.RelayFlatten(e);
        }

        private void OnCancelFired(object sender, CancelEventArgs e)
        {
            if (_engine != null)
                _engine.RelayCancel(e);
        }
    }
}
