// B53 DW-B53-01: COMPILE-TIME GATE. Class is inactive in production build.
// Define PTT_FOLLOWER_ACTIVE to restore the pre-B53 architecture.
// DO NOT DELETE this file -- NT8 AddOn import safety requires the file to exist.
// When PTT_FOLLOWER_ACTIVE is not defined (default), the class compiles away silently.
#if PTT_FOLLOWER_ACTIVE
// PTT-COPIER-B42 -- PttFollowerStrategy.cs
// PTT-COPIER-B45 T2: added StartBehavior = ImmediatelySubmit (DW-B45-FOLLOWER-STARTBEHAVIOR-02).
// Thin headless NinjaScript Strategy. One instance per follower account per instrument.
// Subscribes to PttBus.FillSignal at State.Realtime. Unsubscribes at State.Terminated.
// Calls AtmStrategyCreate on account+instrument match via virtual helper seams.
//
// NT8 constraints satisfied:
//   NT8-001: no init setters -- Strategy has no fields; all data from FillSignalEventArgs
//   NT8-003: no volatile fields
//   NT8-033: no async void
//   NT8-007: not applicable (ATM path, not CreateOrder)
//
// Jane Street constraints satisfied:
//   JS-001: no throw in hot path -- OnFillSignal has no throw; errors logged via Print()
//   JS-021: no lock() -- event += / -= on NT8 lifecycle thread (OnStateChange), raise from
//           CopyEngine dispatch thread. CLR delegate += / -= are atomic.
//   JS-033: no async void -- OnFillSignal is private void; OnBarUpdate is synchronous void.
//
// ARCH-BRACKET-03: AtmStrategyCreate() is available on StrategyBase only (confirmed 2026-08-05).
//                  This class derives from Strategy (which derives from StrategyBase) to gain access.
using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace PropTraderTools
{
    public class PttFollowerStrategy : Strategy
    {
        // CYC=4: 3 State branches (SetDefaults, Realtime, Terminated) + 1 implicit base
        // NT8-CS0176: State property vs State enum -- qualify enum with NinjaTrader.NinjaScript.State.
        // NT8-CS0176: Calculate property vs Calculate enum -- qualify with NinjaTrader.NinjaScript.Calculate.
        protected override void OnStateChange()
        {
            if (State == NinjaTrader.NinjaScript.State.SetDefaults)
            {
                Name = "PTTFollowerStrategy";
                Calculate = NinjaTrader.NinjaScript.Calculate.OnBarClose;
                BarsRequiredToTrade = 0;
                IsExitOnSessionCloseStrategy = false;
                StartBehavior = NinjaTrader.NinjaScript.StartBehavior.ImmediatelySubmit; // B45 T2: never pause on existing position
            }
            else if (State == NinjaTrader.NinjaScript.State.Realtime)
            {
                PttBus.FillSignal += OnFillSignal;
            }
            else if (State == NinjaTrader.NinjaScript.State.Terminated)
            {
                PttBus.FillSignal -= OnFillSignal;
            }
        }

        // CYC=1: required NT8 override. Empty -- this strategy acts only on PttBus.FillSignal.
        protected override void OnBarUpdate() { }

        // CYC=3: 2 early-return guards + 1 delegation to CallAtmStrategyCreate.
        // Uses virtual helpers for all 4 name values to enable test isolation without NT8 runtime.
        // JS-021: no lock. Fires on CopyEngine dispatch thread.
        private void OnFillSignal(FillSignalEventArgs args)
        {
            if (GetSignalAccountName(args) != GetStrategyAccountName())
                return;
            if (GetSignalInstrumentName(args) != GetStrategyInstrumentName())
                return;
            CallAtmStrategyCreate(args);
        }

        // CYC=2: (1) empty-template guard + (2) base AtmStrategyCreate call.
        // B46 T1: empty AtmTemplateName = Inherit mode (no ATM brackets requested).
        // Skip AtmStrategyCreate to avoid "Strategy template name parameter missing" error
        // which trips ErrorHandling=StopStrategy and kills the strategy after MaxRestarts.
        // JS-001: no throw. JS-002: no return null (void return). JS-021: no lock.
        protected virtual void CallAtmStrategyCreate(FillSignalEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.AtmTemplateName)) // branch (1): Inherit mode -- skip
                return;
            AtmStrategyCreate(
                args.OrderAction,
                OrderType.Market,
                0,
                0,
                TimeInForce.Gtc,
                args.EntryOrderId,
                args.AtmTemplateName,
                Guid.NewGuid().ToString("N").Substring(0, 8),
                (code, msg) =>
                {
                    if (code != ErrorCode.NoError)
                        Print("B46 ATM error: " + msg);
                }
            );
        }

        // CYC=1: virtual test seam -- returns this strategy's bound account name.
        // Production: Account.Name (NT8 bound property).
        // Test subclass: returns injected string value (no NT8 runtime needed).
        protected virtual string GetStrategyAccountName() => Account.Name;

        // CYC=1: virtual test seam -- returns this strategy's bound instrument full name.
        // Production: Instrument.FullName (NT8 bound property).
        // Test subclass: returns injected string value (no NT8 runtime needed).
        protected virtual string GetStrategyInstrumentName() => Instrument.FullName;

        // CYC=1: virtual test seam -- returns account name from the FillSignal args.
        // Production: args.Account?.Name (null-safe -- args.Account may be null in degenerate case).
        // Test subclass: returns injected string value so no real Account object is needed.
        protected virtual string GetSignalAccountName(FillSignalEventArgs args) =>
            args.Account != null ? args.Account.Name : null;

        // CYC=1: virtual test seam -- returns instrument full name from the FillSignal args.
        // Production: args.Instrument?.FullName (null-safe).
        // Test subclass: returns injected string value so no real Instrument object is needed.
        protected virtual string GetSignalInstrumentName(FillSignalEventArgs args) =>
            args.Instrument != null ? args.Instrument.FullName : null;
    }
}
#endif // PTT_FOLLOWER_ACTIVE
