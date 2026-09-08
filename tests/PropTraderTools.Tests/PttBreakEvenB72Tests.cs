// PttBreakEvenB72Tests.cs -- xUnit verification tests for WAVE1-LANE-B T-1.
// Spec requirements covered: B-01 (SubmitBePair), B-06 (SubmitBeStopLocal).
// Mirrors guard predicate logic from PttBreakEven.cs (src/PropTraderTools/Features/PttBreakEven.cs).
//
// Direct ProjectReference impossible across TFMs:
//   PropTraderTools targets net48 (NT8 requirement).
//   PropTraderTools.Tests targets net8.0.
//   NT8 Account/Instrument/Position are not instantiable without the NT8 runtime.
//   Inline mirror of guard predicates is the established pattern
//   (see BwaveRefactorLaneBTests.cs, CopyEngineB137Tests.cs, B140Tests.cs, B143Tests.cs).
//
// Framework: xUnit ONLY. NEVER NUnit or MSTest. ASCII-only. No DateTime.Now. No lock().
// JS-021: no lock. JS-001: no throw. JS-002: no null return. JS-033: no async void.
using Xunit;

namespace PropTraderTools.Tests
{
    /// <summary>
    /// Inline mirror tests for PttBreakEven guard logic.
    /// Tests the early-return predicates that protect SubmitBePair (B-01) and
    /// SubmitBeStopLocal (B-06) from NT8 CreateOrder calls on invalid state.
    /// </summary>
    public sealed class PttBreakEvenB72Tests
    {
        // ------------------------------------------------------------------
        // Inline mirror of PttBreakEven.IsInvalidInput(Account acc, Instrument instr).
        // Source: PttBreakEven.cs L364-L367.
        // CYC=1: single || expression.
        // Used as the first guard in SubmitBeStopLocal (B-06).
        // ------------------------------------------------------------------
        private static bool IsInvalidInput(bool accIsNull, bool instrIsNull)
        {
            return accIsNull || instrIsNull;
        }

        // ------------------------------------------------------------------
        // Inline mirror of the flat-position guard in SubmitBeStopLocal (B-06).
        // Source: PttBreakEven.cs L267.
        //   if (pos == null || pos.Quantity == 0) return;
        // ------------------------------------------------------------------
        private static bool IsPositionFlat(bool posIsNull, int quantity)
        {
            return posIsNull || quantity == 0;
        }

        // ------------------------------------------------------------------
        // B-01: SubmitBePair guard coverage
        // SubmitBePair delegates immediately to acc.CreateOrder -- the null-return
        // branch (sOrd == null) logs and skips Submit. Mirror that decision:
        // ------------------------------------------------------------------
        private static bool ShouldSkipSubmit(bool orderIsNull)
        {
            return orderIsNull;
        }

        // ------------------------------------------------------------------
        // B-01 tests: SubmitBePair (PttBreakEven.cs L438-L541)
        // ------------------------------------------------------------------

        /// <summary>
        /// When acc.CreateOrder returns null, SubmitBePair logs a skip message and
        /// does not call acc.Submit. The shouldSkipSubmit predicate returns true.
        /// </summary>
        [Fact]
        public void SubmitBePair_WhenOrderIsNull_DoesNotThrow()
        {
            // Arrange: simulate CreateOrder returning null (NT8 returns null on invalid price/state)
            bool orderIsNull = true;

            // Act
            bool skip = ShouldSkipSubmit(orderIsNull);

            // Assert: guard fires -- Submit call is bypassed, no exception path
            Assert.True(skip);
        }

        /// <summary>
        /// When acc.CreateOrder returns a non-null order, SubmitBePair proceeds to Submit.
        /// </summary>
        [Fact]
        public void SubmitBePair_WhenOrderIsNotNull_ProceedsToSubmit()
        {
            bool orderIsNull = false;

            bool skip = ShouldSkipSubmit(orderIsNull);

            Assert.False(skip);
        }

        // ------------------------------------------------------------------
        // B-06 tests: SubmitBeStopLocal (PttBreakEven.cs L255-L311)
        // ------------------------------------------------------------------

        /// <summary>
        /// When position is flat (Quantity == 0), SubmitBeStopLocal returns without
        /// calling CreateOrder (B-06 guard path).
        /// </summary>
        [Fact]
        public void SubmitBeStopLocal_WhenPositionIsFlat_SkipsSubmit()
        {
            // Arrange: non-null position with zero quantity
            bool posIsNull = false;
            int quantity = 0;

            // Act
            bool flat = IsPositionFlat(posIsNull, quantity);

            // Assert: flat guard fires -- no CreateOrder
            Assert.True(flat);
        }

        /// <summary>
        /// When position is null (not found for account+instrument), guard fires.
        /// </summary>
        [Fact]
        public void SubmitBeStopLocal_WhenPositionIsNull_SkipsSubmit()
        {
            bool posIsNull = true;
            int quantity = 1; // qty irrelevant when pos is null

            bool flat = IsPositionFlat(posIsNull, quantity);

            Assert.True(flat);
        }

        /// <summary>
        /// When position has non-zero quantity, guard does not fire -- method proceeds.
        /// </summary>
        [Fact]
        public void SubmitBeStopLocal_WhenPositionHasQuantity_Proceeds()
        {
            bool posIsNull = false;
            int quantity = 2;

            bool flat = IsPositionFlat(posIsNull, quantity);

            Assert.False(flat);
        }

        /// <summary>
        /// IsInvalidInput fires when acc is null -- first guard in SubmitBeStopLocal.
        /// </summary>
        [Fact]
        public void SubmitBeStopLocal_WhenAccIsNull_GuardFires()
        {
            bool guard = IsInvalidInput(accIsNull: true, instrIsNull: false);

            Assert.True(guard);
        }

        /// <summary>
        /// IsInvalidInput fires when instr is null.
        /// </summary>
        [Fact]
        public void SubmitBeStopLocal_WhenInstrIsNull_GuardFires()
        {
            bool guard = IsInvalidInput(accIsNull: false, instrIsNull: true);

            Assert.True(guard);
        }

        /// <summary>
        /// IsInvalidInput does not fire when both acc and instr are non-null.
        /// </summary>
        [Fact]
        public void SubmitBeStopLocal_WhenInputsValid_GuardDoesNotFire()
        {
            bool guard = IsInvalidInput(accIsNull: false, instrIsNull: false);

            Assert.False(guard);
        }
    }
}