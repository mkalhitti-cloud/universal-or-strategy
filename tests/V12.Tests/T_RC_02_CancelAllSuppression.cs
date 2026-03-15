// V12.Tests -- T-RC-02: CANCEL_ALL Suppression Regression Suite
// Build 980 Proof-of-Work
// Proves: _fleetSuppressionMap B3/B4 gate seals the trailing-cancel race condition.
// ASCII-only log assertions.
using System;
using System.Linq;
using NinjaTrader.Cbi;
using Xunit;

namespace V12.Tests
{
    public class T_RC_02_CancelAllSuppression
    {
        // -----------------------------------------------------------
        // RC02-A: No suppression entry -- order must be processed
        // -----------------------------------------------------------
        [Fact]
        public void RC02_A_NoSuppressionMap_OrderProcessed()
        {
            var h = FsmHarness.Create();
            // Suppression map is empty -- gate is open
            bool processed = h.SimulateAccountOrderSuppression("Apex1", "Fleet_Apex1_OR_1");

            Assert.True(processed, "Gate should be OPEN when no suppression entry exists.");
            Assert.DoesNotContain(h.PrintLog, l => l.Contains("[SUPPRESSION]"));
        }

        // -----------------------------------------------------------
        // RC02-B: Active suppression (stamped now) -- order must be blocked
        // -----------------------------------------------------------
        [Fact]
        public void RC02_B_SuppressionActive_OrderBlocked()
        {
            var h = FsmHarness.Create();
            h.StampSuppression("Apex1"); // stamp = UtcNow

            bool processed = h.SimulateAccountOrderSuppression("Apex1", "Fleet_Apex1_OR_1");

            Assert.False(processed, "Gate should be CLOSED -- suppression active within 2s window.");
            Assert.Contains(h.PrintLog, l => l.Contains("[SUPPRESSION]") && l.Contains("B3/B4 Gate"));
        }

        // -----------------------------------------------------------
        // RC02-C: Suppression expired (> 2s ago) -- order must be processed
        // -----------------------------------------------------------
        [Fact]
        public void RC02_C_SuppressionExpired_OrderProcessed()
        {
            var h = FsmHarness.Create();
            // Stamp 3 seconds in the past
            long staleTimestamp = DateTime.UtcNow.Ticks - TimeSpan.TicksPerSecond * 3;
            h.StampSuppressionAt("Apex1", staleTimestamp);

            bool processed = h.SimulateAccountOrderSuppression("Apex1", "Fleet_Apex1_OR_1");

            Assert.True(processed, "Gate should be OPEN -- suppression window expired (3s > 2s TTL).");
            Assert.DoesNotContain(h.PrintLog, l => l.Contains("[SUPPRESSION]") && l.Contains("B3/B4 Gate"));
        }

        // -----------------------------------------------------------
        // RC02-D: FSM in PendingCancel, CANCEL_ALL fires AFTER gate pass.
        // Lambda must detect B4 gate, abort replacement, remove spec.
        // -----------------------------------------------------------
        [Fact]
        public void RC02_D_FsmPendingCancel_LambdaAborts_OnB4Gate()
        {
            var h = FsmHarness.Create();
            const string signal  = "Fleet_Apex1_OR_1";
            const string acct    = "Apex1";
            const string orderId = "order-abc-123";

            // Setup: FSM is in PendingCancel state, spec queued
            var spec = new FollowerReplaceSpec
            {
                State             = FollowerReplaceState.PendingCancel,
                CancellingOrderId = orderId,
                PendingQty        = 2,
                PendingLimitPrice = 4800.25,
                PendingStopPrice  = 4799.75,
                AccountName       = acct,
                SignalName        = signal,
                MasterSignalName  = "OR_1",
                EntryOrderType    = OrderType.StopLimit,
                IsStopType        = true
            };
            h._followerReplaceSpecs[signal] = spec;

            // CANCEL_ALL fires AFTER gate pass but BEFORE lambda executes
            // Stamp suppression immediately -- simulates race window
            h.StampSuppression(acct);

            // Simulate dispatching the replacement lambda
            h.SimulateFsmReplacementLambda(signal, acct, spec);

            // B4 gate should have aborted the lambda
            Assert.False(h.SubmitCalled, "SubmitFollowerReplacement must NOT be called when B4 gate fires.");
            Assert.False(h._followerReplaceSpecs.ContainsKey(signal),
                "Spec must be removed from _followerReplaceSpecs after B4 abort.");
            Assert.Contains(h.PrintLog, l => l.Contains("[SUPPRESSION-B4]") && l.Contains(signal));
        }

        // -----------------------------------------------------------
        // RC02-E: Different account suppressed -- other accounts unaffected
        // -----------------------------------------------------------
        [Fact]
        public void RC02_E_DifferentAccount_Unaffected()
        {
            var h = FsmHarness.Create();
            h.StampSuppression("Apex2"); // Only Apex2 suppressed

            bool processed = h.SimulateAccountOrderSuppression("Apex1", "Fleet_Apex1_OR_1");

            Assert.True(processed, "Apex1 gate should be OPEN -- suppression is only for Apex2.");
        }
    }
}
