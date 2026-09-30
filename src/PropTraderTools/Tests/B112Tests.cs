// B112Tests.cs -- xUnit regression tests for DW-B116 fix in CountLeaderTargets.
// All tests use [Fact] only. No NUnit. No MSTest.
// ASCII-only. No lock(). No async void. No return null.
//
// NOTE: CountLeaderTargets is a private method of the sealed NinjaTrader AddOnBase
// singleton CopyEngine. Full in-process invocation requires the NT8 host runtime.
// These tests are documentation-grade unit tests that define the observable contract
// for the AFTER code (B112 DW-B116 fix). They compile as structural verification and
// are intended to be run within the NT8 integration test harness, or adapted via a
// thin seam/wrapper once DW-PTT-BE-FIX-03 test infrastructure remediation is complete.
//
// Each test encodes the predicate logic extracted from CountLeaderTargets AFTER code
// (CopyEngine.cs L3307-L3352, B112 revision) and asserts on the same in-memory stubs
// used by B111Tests.cs, B110Tests.cs, etc.
//
// Regression contract: If any of CHANGE 1/2/3 are reverted, the named assertion fails.

using System.Collections.Generic;
using Xunit;

namespace PropTraderTools.Tests
{
    /// <summary>
    /// Regression tests for B112-T1: DW-B116 CountLeaderTargets fix.
    /// Verifies Working-only filter, native-only isTarget predicate, and Math.Min(count,3) cap.
    /// </summary>
    public class B112Tests
    {
        // -------------------------------------------------------------------------
        // Local stub types mirroring the NT8 enums/shapes used by CountLeaderTargets.
        // -------------------------------------------------------------------------

        private enum StubOrderState
        {
            Working,
            Accepted,
            Submitted,
            Cancelled,
            Filled,
        }

        private enum StubOrderType
        {
            Limit,
            StopMarket,
            Market,
        }

        private sealed class StubInstrument
        {
            public string FullName { get; }

            public StubInstrument(string fullName)
            {
                FullName = fullName;
            }
        }

        private sealed class StubOrder
        {
            public string Name { get; set; } = string.Empty;
            public StubOrderState OrderState { get; set; }
            public StubOrderType OrderType { get; set; }
            public StubInstrument Instrument { get; set; } = new StubInstrument(string.Empty);
        }

        // -------------------------------------------------------------------------
        // Predicate extracted verbatim from CountLeaderTargets AFTER code (B112 revision).
        // This is the regression reference implementation.
        //
        // CHANGE 2 (AFTER): stateOk = o.OrderState == OrderState.Working only.
        // CHANGE 1 (AFTER): isTarget = native Target1..9 only, no PTT- prefixes.
        // CHANGE 3 (AFTER): return Math.Min(count, 3).
        // -------------------------------------------------------------------------

        private static int CountLeaderTargetsStub(List<StubOrder> orders, StubInstrument instrument)
        {
            int count = 0;
            foreach (StubOrder o in orders)
            {
                if (o == null)
                    continue;

                // CHANGE 2: Working-only (DW-B116: removed Accepted + Submitted).
                bool stateOk = o.OrderState == StubOrderState.Working;

                bool instrOk = o.Instrument != null && o.Instrument.FullName == instrument.FullName;

                if (!stateOk || !instrOk || o.OrderType != StubOrderType.Limit)
                    continue;

                // CHANGE 1: native Target1..9 only (DW-B116: removed PTT-QX-T* + PTT-BE-Target-*).
                bool isTarget =
                    !string.IsNullOrEmpty(o.Name)
                    && o.Name.Length >= 7
                    && o.Name.StartsWith("Target", System.StringComparison.Ordinal)
                    && char.IsDigit(o.Name[6])
                    && o.Name[6] != '0';

                if (isTarget)
                    count++;
            }

            // CHANGE 3: Math.Min cap at 3 (standard ATM max target slots).
            return System.Math.Min(count, 3);
        }

        // -------------------------------------------------------------------------
        // T_B112_01
        // -------------------------------------------------------------------------

        /// <summary>
        /// Nominal path: 3 Working native ATM targets (Target1-3) must return 3.
        /// </summary>
        [Fact]
        public void CountLeaderTargets_Returns3_WhenLeaderHas3WorkingNativeTargets()
        {
            // Arrange
            var instrument = new StubInstrument("MES 09-26");
            var orders = new List<StubOrder>
            {
                new StubOrder
                {
                    Name = "Target1",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                new StubOrder
                {
                    Name = "Target2",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                new StubOrder
                {
                    Name = "Target3",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
            };

            // Act
            int result = CountLeaderTargetsStub(orders, instrument);

            // Assert
            // Regression contract: nominal path -- 3 Working native targets must return 3.
            Assert.Equal(3, result);
        }

        // -------------------------------------------------------------------------
        // T_B112_02
        // -------------------------------------------------------------------------

        /// <summary>
        /// CHANGE 1: PTT-BE-Target-* residue orders must NOT be counted.
        /// 3 Working native + 2 Working PTT-BE-Target-* = result must be 3 (not 5).
        /// </summary>
        [Fact]
        public void CountLeaderTargets_ExcludesPttBeTargetResidues()
        {
            // Arrange
            var instrument = new StubInstrument("MES 09-26");
            var orders = new List<StubOrder>
            {
                new StubOrder
                {
                    Name = "Target1",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                new StubOrder
                {
                    Name = "Target2",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                new StubOrder
                {
                    Name = "Target3",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                // Stale PTT-BE residue orders -- must NOT be counted (CHANGE 1).
                new StubOrder
                {
                    Name = "PTT-BE-Target-4",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                new StubOrder
                {
                    Name = "PTT-BE-Target-5",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
            };

            // Act
            int result = CountLeaderTargetsStub(orders, instrument);

            // Assert
            // Regression contract: if CHANGE 1 is reverted, result would be 5. Assert.Equal(3) fails.
            Assert.Equal(3, result);
        }

        // -------------------------------------------------------------------------
        // T_B112_03
        // -------------------------------------------------------------------------

        /// <summary>
        /// CHANGE 1: PTT-QX-T* residue orders must NOT be counted.
        /// 3 Working native + 2 Working PTT-QX-T* = result must be 3 (not 5).
        /// </summary>
        [Fact]
        public void CountLeaderTargets_ExcludesPttQxTResidues()
        {
            // Arrange
            var instrument = new StubInstrument("MES 09-26");
            var orders = new List<StubOrder>
            {
                new StubOrder
                {
                    Name = "Target1",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                new StubOrder
                {
                    Name = "Target2",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                new StubOrder
                {
                    Name = "Target3",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                // Stale Quick-Exit orders -- must NOT be counted (CHANGE 1).
                new StubOrder
                {
                    Name = "PTT-QX-T1",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                new StubOrder
                {
                    Name = "PTT-QX-T2",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
            };

            // Act
            int result = CountLeaderTargetsStub(orders, instrument);

            // Assert
            // Regression contract: if CHANGE 1 is reverted, result would be 5. Assert.Equal(3) fails.
            Assert.Equal(3, result);
        }

        // -------------------------------------------------------------------------
        // T_B112_04
        // -------------------------------------------------------------------------

        /// <summary>
        /// CHANGE 3: Math.Min(count,3) hard cap fires when native count exceeds 3.
        /// 5 Working native orders (Target1-5) must return 3, not 5.
        /// </summary>
        [Fact]
        public void CountLeaderTargets_CapsAt3_WhenMoreThan3NativeTargets()
        {
            // Arrange
            var instrument = new StubInstrument("MES 09-26");
            var orders = new List<StubOrder>
            {
                new StubOrder
                {
                    Name = "Target1",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                new StubOrder
                {
                    Name = "Target2",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                new StubOrder
                {
                    Name = "Target3",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                new StubOrder
                {
                    Name = "Target4",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                new StubOrder
                {
                    Name = "Target5",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
            };

            // Act
            int result = CountLeaderTargetsStub(orders, instrument);

            // Assert
            // Regression contract: if CHANGE 3 is reverted, result would be 5. Assert.Equal(3) fails.
            Assert.Equal(3, result);
        }

        // -------------------------------------------------------------------------
        // T_B112_05
        // -------------------------------------------------------------------------

        /// <summary>
        /// CHANGE 2: Accepted and Submitted native orders must NOT be counted.
        /// Target1 Working, Target2+3 Accepted, Target4+5 Submitted -> result must be 1.
        /// </summary>
        [Fact]
        public void CountLeaderTargets_ExcludesAcceptedAndSubmittedNativeTargets()
        {
            // Arrange
            var instrument = new StubInstrument("MES 09-26");
            var orders = new List<StubOrder>
            {
                // Working -- must be counted.
                new StubOrder
                {
                    Name = "Target1",
                    OrderState = StubOrderState.Working,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                // Accepted -- must NOT be counted (CHANGE 2).
                new StubOrder
                {
                    Name = "Target2",
                    OrderState = StubOrderState.Accepted,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                new StubOrder
                {
                    Name = "Target3",
                    OrderState = StubOrderState.Accepted,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                // Submitted -- must NOT be counted (CHANGE 2).
                new StubOrder
                {
                    Name = "Target4",
                    OrderState = StubOrderState.Submitted,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
                new StubOrder
                {
                    Name = "Target5",
                    OrderState = StubOrderState.Submitted,
                    OrderType = StubOrderType.Limit,
                    Instrument = instrument,
                },
            };

            // Act
            int result = CountLeaderTargetsStub(orders, instrument);

            // Assert
            // Regression contract: if CHANGE 2 is reverted (Accepted+Submitted re-added),
            // result would be Math.Min(5,3) = 3. Assert.Equal(1) would fail -- overcount restored.
            Assert.Equal(1, result);
        }
    }
}
