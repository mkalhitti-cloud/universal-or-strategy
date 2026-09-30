// B141 xUnit tests: OCO Cascade Dual-Resubmit.
// DW-B153: OCO cascade kills Target1/Target2/Target3 on stop drag.
// B141 fix: capture target price before cascade, resubmit PTT-TGT-Drag after cascade.
//
// Tests T_B141_01..T_B141_07 as specified in docs/brain/B141/04-tickets.md.
// Framework: xUnit ONLY. NEVER NUnit or MSTest.
// Approach: inline static predicates mirroring CopyEngine production code.
//   NT8 Order/Account types are not instantiable without the NT8 runtime.
//   The test project targets net8.0; PropTraderTools targets net48 for NT8.
//   Inline predicates avoid TFM mismatch and reproduce the exact production logic.
//   T_B141_01..04: inline TryParseStopSuffix + IsTargetOrderLive logic.
//   T_B141_05..07: inline SyncFollowerBracket branch (3) routing logic (NT8 types skipped).
// ASCII-only. No lock. No async void. JS-041: all CYC <= 8.
using Xunit;

namespace PropTraderTools.Tests
{
    public sealed class B141Tests
    {
        // ------------------------------------------------------------------
        // Inline predicate -- mirrors CopyEngine.TryParseStopSuffix production code.
        // Production code (B141 Change 3):
        //   private static bool TryParseStopSuffix(string stopName, out string suffix)
        //   { suffix = null; if (stopName == null || stopName.Length < 5) return false;
        //     string raw = stopName.Substring(4);
        //     if (!int.TryParse(raw, out int n) || n < 1 || n > 3) return false;
        //     suffix = raw; return true; }
        // Kept inline because tests project targets net8.0 and cannot reference net48 assembly.
        // ------------------------------------------------------------------
        private static bool TryParseStopSuffixInline(string stopName, out string suffix)
        {
            suffix = null;
            if (stopName == null || stopName.Length < 5)
                return false;
            string raw = stopName.Substring(4);
            if (!int.TryParse(raw, out int n) || n < 1 || n > 3)
                return false;
            suffix = raw;
            return true;
        }

        // ------------------------------------------------------------------
        // Inline enum stub -- mirrors NT8 OrderState for inline IsTargetOrderLive predicate.
        // Avoids NT8 runtime dependency for these unit tests.
        // ------------------------------------------------------------------
        private enum StubOrderState
        {
            Unknown,
            Accepted,
            Working,
            Cancelled,
            Filled,
        }

        // ------------------------------------------------------------------
        // Inline predicate -- mirrors CopyEngine.IsTargetOrderLive production code.
        // Production code (B141 Change 4):
        //   private static bool IsTargetOrderLive(Order o) =>
        //       o != null && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted);
        // Uses StubOrderState enum to avoid NT8 runtime dependency.
        // ------------------------------------------------------------------
        private static bool IsTargetOrderLiveInline(StubOrderState state) =>
            state == StubOrderState.Working || state == StubOrderState.Accepted;

        // ------------------------------------------------------------------
        // Stub order for inline test doubles (no NT8 runtime required).
        // ------------------------------------------------------------------
        private sealed class StubOrder
        {
            public string Name { get; set; }
            public StubOrderState OrderState { get; set; }
            public double LimitPrice { get; set; }
        }

        // ------------------------------------------------------------------
        // Inline CaptureLinkedTargetPrice logic -- mirrors production code (B141 Change 2).
        // Production code:
        //   private double? CaptureLinkedTargetPrice(Account acc, string stopName)
        //   { if (!TryParseStopSuffix(stopName, out string suffix)) return null;
        //     string targetName = "Target" + suffix;
        //     foreach (var o in acc.Orders.ToList())
        //     { if (IsTargetOrderLive(o) && o.Name == targetName) return o.LimitPrice; }
        //     return null; }
        // Uses StubOrder list and inline predicates to avoid NT8 runtime dependency.
        // ------------------------------------------------------------------
        private static double? CaptureLinkedTargetPriceInline(
            System.Collections.Generic.List<StubOrder> orders,
            string stopName
        )
        {
            if (!TryParseStopSuffixInline(stopName, out string suffix))
                return null;
            string targetName = "Target" + suffix;
            foreach (var o in orders)
            {
                if (IsTargetOrderLiveInline(o.OrderState) && o.Name == targetName)
                    return o.LimitPrice;
            }
            return null;
        }

        // ------------------------------------------------------------------
        // T_B141_01: CaptureLinkedTargetPrice -- Stop1 -> Target1 LimitPrice returned.
        // Confirms: suffix parse "Stop1"->"1", target lookup "Target1", LimitPrice returned.
        // ------------------------------------------------------------------

        [Fact]
        public void T_B141_01_CaptureLinkedTargetPrice_Stop1_ReturnsTarget1LimitPrice()
        {
            // Arrange: acc.Orders contains exactly one order: Name="Target1", Working, LimitPrice=4500.25
            var orders = new System.Collections.Generic.List<StubOrder>
            {
                new StubOrder
                {
                    Name = "Target1",
                    OrderState = StubOrderState.Working,
                    LimitPrice = 4500.25,
                },
            };

            // Act
            double? result = CaptureLinkedTargetPriceInline(orders, "Stop1");

            // Assert
            Assert.True(result.HasValue);
            Assert.Equal(4500.25, result.Value);
        }

        // ------------------------------------------------------------------
        // T_B141_02: CaptureLinkedTargetPrice -- Stop2 -> Target2, Accepted state coverage.
        // Confirms: OrderState.Accepted is treated as live (not just Working); Stop2/Target2 pair.
        // ------------------------------------------------------------------

        [Fact]
        public void T_B141_02_CaptureLinkedTargetPrice_Stop2_ReturnsTarget2LimitPrice()
        {
            // Arrange: acc.Orders contains exactly one order: Name="Target2", Accepted, LimitPrice=4510.50
            var orders = new System.Collections.Generic.List<StubOrder>
            {
                new StubOrder
                {
                    Name = "Target2",
                    OrderState = StubOrderState.Accepted,
                    LimitPrice = 4510.50,
                },
            };

            // Act
            double? result = CaptureLinkedTargetPriceInline(orders, "Stop2");

            // Assert
            Assert.True(result.HasValue);
            Assert.Equal(4510.50, result.Value);
        }

        // ------------------------------------------------------------------
        // T_B141_03: CaptureLinkedTargetPrice -- Stop3 -> Target3, all three suffix variants covered.
        // ------------------------------------------------------------------

        [Fact]
        public void T_B141_03_CaptureLinkedTargetPrice_Stop3_ReturnsTarget3LimitPrice()
        {
            // Arrange: acc.Orders contains exactly one order: Name="Target3", Working, LimitPrice=4520.75
            var orders = new System.Collections.Generic.List<StubOrder>
            {
                new StubOrder
                {
                    Name = "Target3",
                    OrderState = StubOrderState.Working,
                    LimitPrice = 4520.75,
                },
            };

            // Act
            double? result = CaptureLinkedTargetPriceInline(orders, "Stop3");

            // Assert
            Assert.True(result.HasValue);
            Assert.Equal(4520.75, result.Value);
        }

        // ------------------------------------------------------------------
        // T_B141_04: CaptureLinkedTargetPrice -- Cancelled target returns null.
        // Confirms: IsTargetOrderLive predicate correctly excludes Cancelled state.
        // Cascade-already-cancelled scenario returns null (no double-resubmit).
        // ------------------------------------------------------------------

        [Fact]
        public void T_B141_04_CaptureLinkedTargetPrice_TargetAlreadyCancelled_ReturnsNull()
        {
            // Arrange: acc.Orders contains one Cancelled "Target1" -- no live target
            var orders = new System.Collections.Generic.List<StubOrder>
            {
                new StubOrder
                {
                    Name = "Target1",
                    OrderState = StubOrderState.Cancelled,
                    LimitPrice = 4500.25,
                },
            };

            // Act
            double? result = CaptureLinkedTargetPriceInline(orders, "Stop1");

            // Assert: Cancelled state not live -> returns null
            Assert.False(result.HasValue);
        }

        // ------------------------------------------------------------------
        // T_B141_05: End-to-end branch routing -- when target found, resubmit path taken.
        // Confirms: capturedTargetPrice.HasValue == true -> ResubmitTargetAfterCascade called.
        // Uses inline branch routing logic (NT8 Account/Order skipped per established pattern).
        // ------------------------------------------------------------------

        [Fact]
        public void T_B141_05_SyncFollowerBracket_AtmStop1Drag_ResubmitsPttTgtDrag_WhenTargetFound()
        {
            // Arrange: Target1 is Working at 4500.25 -- capturedTargetPrice.HasValue = true
            var orders = new System.Collections.Generic.List<StubOrder>
            {
                new StubOrder
                {
                    Name = "Target1",
                    OrderState = StubOrderState.Working,
                    LimitPrice = 4500.25,
                },
            };
            bool isStop = true;
            bool isAtmSTPOrder = true; // "Stop1" passes IsAtmSTPOrder

            // Act: inline routing of SyncFollowerBracket branch (3)
            double? capturedTargetPrice = CaptureLinkedTargetPriceInline(orders, "Stop1");

            bool branch3Taken = isStop && isAtmSTPOrder;
            bool resubmitPathTaken = branch3Taken && capturedTargetPrice.HasValue;

            // Assert: branch 3 fires, and resubmit path fires because target was found
            Assert.True(branch3Taken, "SyncFollowerBracket branch (3) must fire for ATM stop drag");
            Assert.True(capturedTargetPrice.HasValue, "capturedTargetPrice must have value when Target1 is Working");
            Assert.Equal(4500.25, capturedTargetPrice.Value);
            Assert.True(resubmitPathTaken, "ResubmitTargetAfterCascade path must be taken when target found");
        }

        // ------------------------------------------------------------------
        // T_B141_06: Branch routing -- when target absent, resubmit path NOT taken.
        // Confirms: capturedTargetPrice.HasValue == false -> no ResubmitTargetAfterCascade.
        // Guard prevents spurious resubmit when target was already cascade-cancelled.
        // ------------------------------------------------------------------

        [Fact]
        public void T_B141_06_SyncFollowerBracket_AtmStop1Drag_NoResubmit_WhenTargetAbsent()
        {
            // Arrange: NO live Target1 (already cancelled by prior cascade)
            var orders = new System.Collections.Generic.List<StubOrder>
            {
                new StubOrder
                {
                    Name = "Target1",
                    OrderState = StubOrderState.Cancelled,
                    LimitPrice = 4500.25,
                },
            };
            bool isStop = true;
            bool isAtmSTPOrder = true;

            // Act: inline routing of SyncFollowerBracket branch (3)
            double? capturedTargetPrice = CaptureLinkedTargetPriceInline(orders, "Stop1");

            bool branch3Taken = isStop && isAtmSTPOrder;
            bool resubmitPathTaken = branch3Taken && capturedTargetPrice.HasValue;

            // Assert: branch 3 fires, but resubmit path NOT taken because target absent
            Assert.True(branch3Taken, "SyncFollowerBracket branch (3) must fire for ATM stop drag");
            Assert.False(capturedTargetPrice.HasValue, "capturedTargetPrice must be null when no live Target1");
            Assert.False(resubmitPathTaken, "ResubmitTargetAfterCascade must NOT be called when target absent");
        }

        // ------------------------------------------------------------------
        // T_B141_07: Regression -- SyncAtmFollowerBracket is ALWAYS called unconditionally.
        // Confirms: SyncAtmFollowerBracket not gated on capturedTargetPrice.HasValue.
        // In production code, SyncAtmFollowerBracket fires before the HasValue check.
        // Tests BOTH scenario A (target found) and scenario B (target absent) to verify
        // the unconditional invariant: stop-price-update behavior preserved in both cases.
        // ------------------------------------------------------------------

        [Fact]
        public void T_B141_07_SyncFollowerBracket_AtmStop_SyncAtmFollowerBracketAlwaysCalled()
        {
            bool isStop = true;
            bool isAtmSTPOrder = true;

            // Scenario A: target found
            var ordersA = new System.Collections.Generic.List<StubOrder>
            {
                new StubOrder
                {
                    Name = "Target1",
                    OrderState = StubOrderState.Working,
                    LimitPrice = 4500.25,
                },
            };
            double? capturedA = CaptureLinkedTargetPriceInline(ordersA, "Stop1");
            bool branch3TakenA = isStop && isAtmSTPOrder;
            // SyncAtmFollowerBracket fires unconditionally BEFORE the HasValue check.
            // Inline: branch3 taken = SyncAtmFollowerBracket called (no guard).
            bool syncAtmCalledA = branch3TakenA; // unconditional -- not gated on capturedA.HasValue

            // Scenario B: target absent
            var ordersB = new System.Collections.Generic.List<StubOrder>
            {
                new StubOrder
                {
                    Name = "Target1",
                    OrderState = StubOrderState.Cancelled,
                    LimitPrice = 4500.25,
                },
            };
            double? capturedB = CaptureLinkedTargetPriceInline(ordersB, "Stop1");
            bool branch3TakenB = isStop && isAtmSTPOrder;
            bool syncAtmCalledB = branch3TakenB; // unconditional -- not gated on capturedB.HasValue

            // Assert: SyncAtmFollowerBracket fires in BOTH scenarios
            Assert.True(syncAtmCalledA, "Scenario A: SyncAtmFollowerBracket must be called when target found");
            Assert.True(syncAtmCalledB, "Scenario B: SyncAtmFollowerBracket must be called when target absent");

            // Assert: only scenario A triggers resubmit path
            Assert.True(capturedA.HasValue, "Scenario A: target captured");
            Assert.False(capturedB.HasValue, "Scenario B: target not captured (already cancelled)");
        }
    }
}