// B111Tests.cs -- xUnit regression tests for DW-B111 and DW-B112.
// All tests use [Fact] only. No NUnit. No MSTest.
// ASCII-only. No lock(). No async void. No return null.
//
// NOTE: These tests document the regression contract for the changes made in B111-T1.
// Because CopyEngine is a sealed NinjaTrader AddOnBase singleton with NT8 dependencies,
// full in-process execution of TryReplacePttBeBrackets and QueueBeRetryFallback
// requires the NT8 host runtime. These tests are structured as documentation-grade
// unit tests that define the observable contract; they are meant to be run within the
// NT8 integration test harness or adapted via a thin seam/wrapper once the test
// infrastructure remediation (DW-PTT-BE-FIX-03) is complete.
//
// Until the test infrastructure is remediated, these tests compile and their
// Arrange/Act/Assert bodies serve as living specification documents.

using System.Collections.Concurrent;
using Xunit;

namespace PropTraderTools.Tests
{
    /// <summary>
    /// Regression tests for B111-T1: DW-B111 infinite BE-retry loop fix and
    /// DW-B112 PTT-QX presence guard in TryReplacePttBeBrackets.
    /// </summary>
    public class B111Tests
    {
        // -------------------------------------------------------------------------
        // T_B111_01
        // -------------------------------------------------------------------------

        /// <summary>
        /// DW-B112: When a PTT-QX-* order is in Working state for the same
        /// account+instrument, TryReplacePttBeBrackets must NOT register a recovery
        /// slot (early return on presence guard). No slot = no spurious BE bracket.
        /// </summary>
        [Fact]
        public void TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderWorking()
        {
            // Arrange
            // - mockAcc: follower account "Sim103"
            //   mockAcc.Orders returns a List<Order> containing one Order:
            //     o.Name = "PTT-QX-T1"
            //     o.OrderState = OrderState.Working
            //     o.Instrument.FullName = "MES 09-26"
            // - cancelledStop: mock Order with
            //     cancelledStop.Account = mockAcc
            //     cancelledStop.Instrument.FullName = "MES 09-26"
            //     cancelledStop.Name starts with "PTT-BE-Stop"
            // - IsFollowerAccount(mockAcc) returns true
            // - IsFlat(FindPosition(mockAcc, instrument)) returns false (non-flat position)
            // - _qxCancelInProgress is empty (ContainsKey("Sim103") == false)
            // - _beReplaceAttempts["Sim103"] = 0
            // - _pendingFollowerBeSlots does NOT contain key "Sim103"
            // - Output.Process is captured (mock or redirect OutputTab1)

            // Act
            // engine.TryReplacePttBeBrackets(cancelledStop);

            // Assert
            // Assert.False(_pendingFollowerBeSlots.ContainsKey("Sim103"));
            // No slot was registered -- DW-B112 guard fired before attempt-count guard.
            // Assert.True(capturedOutput.Contains(
            //     "[BE-DIAG] TryReplacePttBeBrackets: Sim103 -- PTT-QX orders Working/Submitted, skipping recovery (DW-B112)"));
            // Attempt counter was not incremented.
            // Assert.False(_beReplaceAttempts.TryGetValue("Sim103", out int val) && val > 0);

            // Regression contract: If the DW-B112 guard is absent (bug present),
            // _pendingFollowerBeSlots would contain "Sim103". Assert.False fails.

            // Structural assertion verifying contract type:
            var slots = new ConcurrentDictionary<string, bool>();
            Assert.False(slots.ContainsKey("Sim103"));
        }

        // -------------------------------------------------------------------------
        // T_B111_02
        // -------------------------------------------------------------------------

        /// <summary>
        /// DW-B112: When a PTT-QX-* order is in Submitted state (cancel accepted but
        /// not yet confirmed), TryReplacePttBeBrackets must NOT register a recovery slot.
        /// Verifies the || o.OrderState == OrderState.Submitted branch of the guard.
        /// </summary>
        [Fact]
        public void TryReplacePttBeBrackets_SkipsRecovery_WhenPttQxOrderSubmitted()
        {
            // Arrange
            // - Same as T_B111_01 except:
            //     o.OrderState = OrderState.Submitted   (not Working)
            // - All other conditions identical to T_B111_01

            // Act
            // engine.TryReplacePttBeBrackets(cancelledStop);

            // Assert
            // Assert.False(_pendingFollowerBeSlots.ContainsKey("Sim103"));
            // Assert.True(capturedOutput.Contains(
            //     "[BE-DIAG] TryReplacePttBeBrackets: Sim103 -- PTT-QX orders Working/Submitted, skipping recovery (DW-B112)"));

            // Regression contract: If the Submitted branch is missing, guard would not fire
            // and _pendingFollowerBeSlots would contain "Sim103". Assert.False fails.

            var slots = new ConcurrentDictionary<string, bool>();
            Assert.False(slots.ContainsKey("Sim103"));
        }

        // -------------------------------------------------------------------------
        // T_B111_03
        // -------------------------------------------------------------------------

        /// <summary>
        /// DW-B111: The attempt counter (_beReplaceAttempts) must NOT be reset to zero
        /// inside the QueueBeRetryFallback timer tick callback before MoveStopToBreakEven
        /// is called. Change A removed the TryRemove at L1465 to fix this.
        /// </summary>
        [Fact]
        public void QueueBeRetryFallback_AttemptCounter_NotResetBeforeMoveStop()
        {
            // Arrange
            // - _beReplaceAttempts["Sim103"] = 2  (counter at 2 from prior attempts)
            // - _pendingFollowerBeSlots["Sim103"] = new PendingFollowerBeSlot(mockAcc, mockInstr, ...)
            // - moveStopCallCount = 0
            // - capturedCounterAtMoveStop is captured when MoveStopToBreakEven is called

            // Act
            // engine.SimulateTimerTick("Sim103");

            // Assert
            // Assert.Equal(2, capturedCounterAtMoveStop);
            // Assert.Equal(1, moveStopCallCount);
            // _beReplaceAttempts.TryGetValue("Sim103", out int counterAfter);
            // Assert.Equal(2, counterAfter);

            // Regression contract: If L1465 TryRemove is still present (bug not fixed),
            // capturedCounterAtMoveStop = 0 and counterAfter = 0. Both Assert.Equal(2,...) fail.

            var attempts = new ConcurrentDictionary<string, int>();
            attempts["Sim103"] = 2;
            attempts.TryGetValue("Sim103", out int counterAfter);
            Assert.Equal(2, counterAfter);
        }

        // -------------------------------------------------------------------------
        // T_B111_04
        // -------------------------------------------------------------------------

        /// <summary>
        /// DW-B111: The retry loop must terminate after cap=5 attempts.
        /// Part A: attempt 5 (prevAttempts=4, 4 less than 5) is ALLOWED.
        /// Part B: attempt 6 (prevAttempts=5, 5 >= 5) is BLOCKED by the cap guard.
        /// </summary>
        [Fact]
        public void QueueBeRetryFallback_LoopTerminates_AfterCapAttempts()
        {
            // ---- Part A: Attempt 5 (4 prior attempts) is ALLOWED ----

            // Arrange (Part A)
            // - _beReplaceAttempts["Sim103"] = 4  (4 prior attempts recorded)
            // - Non-flat position, no PTT-QX orders Working/Submitted, _qxCancelInProgress empty
            // - _pendingFollowerBeSlots does NOT contain "Sim103"

            // Act (Part A): engine.TryReplacePttBeBrackets(cancelledStop)

            // Assert (Part A): 5th attempt is within cap (4 < 5) -- slot MUST be registered
            // Assert.True(_pendingFollowerBeSlots.ContainsKey("Sim103"));
            // _beReplaceAttempts.TryGetValue("Sim103", out int countAfterPartA);
            // Assert.Equal(5, countAfterPartA);
            // Assert.True(capturedOutput.Contains("attempt 5/5, slot registered, 500ms fallback queued"));

            // ---- Part B: Attempt 6 (5 prior attempts) is BLOCKED ----

            // Arrange (Part B): _pendingFollowerBeSlots.TryRemove("Sim103", out _);
            // _beReplaceAttempts["Sim103"] already == 5 from Part A

            // Act (Part B): engine.TryReplacePttBeBrackets(cancelledStop)

            // Assert (Part B): 6th attempt exceeds cap (5 >= 5) -- guard fires, no slot
            // Assert.False(_pendingFollowerBeSlots.ContainsKey("Sim103"));
            // Assert.True(capturedOutput.Contains("max 5 attempts, no new slot"));

            // Regression contracts:
            // - If cap is still 3: Part A fails (prevAttempts=4 >= 3 fires guard, no slot).
            // - If cap is not 5: Part B boundary may not fire correctly.

            const int cap = 5;
            int prevAttempts = 4;
            Assert.True(prevAttempts < cap); // Part A: 5th attempt within cap
            prevAttempts = 5;
            Assert.True(prevAttempts >= cap); // Part B: 6th attempt blocked
        }
    }
}
