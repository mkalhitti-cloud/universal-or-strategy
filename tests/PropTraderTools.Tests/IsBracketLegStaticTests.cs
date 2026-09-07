// IsBracketLegStaticTests.cs
// xUnit regression tests for IsBracketLegStatic (DW-LB-SFB-01 defect fix).
// Source mirrored: CopyEngine.cs L5799-5812, commit 1086d9fd.
// PropTraderTools.Tests targets net8.0; PropTraderTools targets net48 (NT8 requirement).
// Direct ProjectReference is impossible across TFMs -- inline mirror is the established pattern
// (see B143Tests.cs, CopyEngineB137Tests.cs, B140Tests.cs). IsBracketLegStatic is private
// static in CopyEngine so it cannot be called directly from the test project.
// Framework: xUnit ONLY. NEVER NUnit or MSTest.
using Xunit;

namespace PropTraderTools.Tests
{
    public sealed class IsBracketLegStaticTests
    {
        // ------------------------------------------------------------------
        // Inline mirror -- exact logic of IsBracketLegStatic post-fix (commit 1086d9fd).
        // Source confirmed: CopyEngine.cs L5799-5812.
        // Decompose Order into (string? name, bool hasEntrySignal):
        //   name            = order.Name
        //   hasEntrySignal  = (order.FromEntrySignal != null)
        // If this mirror drifts from production, update it and all affected tests.
        // ------------------------------------------------------------------
        private static bool IsBracketLegStatic(string? name, bool hasEntrySignal)
        {
            if (hasEntrySignal) return true;
            if (name == null) return false;
            return name.StartsWith("Stop", System.StringComparison.Ordinal)
                || name.StartsWith("Target", System.StringComparison.Ordinal)
                || name.StartsWith("PTT-STP-Drag-", System.StringComparison.Ordinal)
                || name.StartsWith("PTT-TGT-Drag-", System.StringComparison.Ordinal)
                || name.EndsWith("STP", System.StringComparison.OrdinalIgnoreCase);
        }

        // ------------------------------------------------------------------
        // T1: PTT-STP-Drag-1 -> true
        // Drag stop replacement is a legitimate bracket leg.
        // StartsWith("PTT-STP-Drag-", Ordinal) clause fires.
        // REQ-DW-LB-SFB-01-4
        // ------------------------------------------------------------------
        [Fact]
        public void PTT_STP_Drag_1_ReturnsTrue()
        {
            Assert.True(IsBracketLegStatic("PTT-STP-Drag-1", false));
        }

        // ------------------------------------------------------------------
        // T2: PTT-TGT-Drag-1 -> true
        // Drag target replacement is a legitimate bracket leg.
        // StartsWith("PTT-TGT-Drag-", Ordinal) clause fires.
        // REQ-DW-LB-SFB-01-5
        // ------------------------------------------------------------------
        [Fact]
        public void PTT_TGT_Drag_1_ReturnsTrue()
        {
            Assert.True(IsBracketLegStatic("PTT-TGT-Drag-1", false));
        }

        // ------------------------------------------------------------------
        // T3: PTT-BE-Stop-1 -> false  [KEY REGRESSION GUARD]
        // Pre-fix: StartsWith("PTT-") matched this and returned true, causing
        // HandleBracketChange to fire on PTT-BE-Stop Working events -> PTT-Flatten storm.
        // Post-fix: no clause matches. Must return false.
        // REQ-DW-LB-SFB-01-1
        // ------------------------------------------------------------------
        [Fact]
        public void PTT_BE_Stop_1_ReturnsFalse()
        {
            Assert.False(IsBracketLegStatic("PTT-BE-Stop-1", false));
        }

        // ------------------------------------------------------------------
        // T4: PTT-Flatten -> false  [KEY REGRESSION GUARD]
        // Pre-fix: StartsWith("PTT-") matched this and returned true.
        // Post-fix: no clause matches. Must return false.
        // REQ-DW-LB-SFB-01-2
        // ------------------------------------------------------------------
        [Fact]
        public void PTT_Flatten_ReturnsFalse()
        {
            Assert.False(IsBracketLegStatic("PTT-Flatten", false));
        }

        // ------------------------------------------------------------------
        // T5: PTT-Tighten-Stop -> false  [REGRESSION GUARD]
        // Pre-fix: StartsWith("PTT-") matched this and returned true.
        // Post-fix: no clause matches. Must return false.
        // REQ-DW-LB-SFB-01-3
        // ------------------------------------------------------------------
        [Fact]
        public void PTT_Tighten_Stop_ReturnsFalse()
        {
            Assert.False(IsBracketLegStatic("PTT-Tighten-Stop", false));
        }

        // ------------------------------------------------------------------
        // T6: Stop1 -> true
        // Core NT8 ATM bracket stop name. StartsWith("Stop") clause fires.
        // REQ-DW-LB-SFB-01-6
        // ------------------------------------------------------------------
        [Fact]
        public void Stop1_ReturnsTrue()
        {
            Assert.True(IsBracketLegStatic("Stop1", false));
        }

        // ------------------------------------------------------------------
        // T7: Target1 -> true
        // Core NT8 ATM bracket target name. StartsWith("Target") clause fires.
        // REQ-DW-LB-SFB-01-7
        // ------------------------------------------------------------------
        [Fact]
        public void Target1_ReturnsTrue()
        {
            Assert.True(IsBracketLegStatic("Target1", false));
        }

        // ------------------------------------------------------------------
        // T8: Buy STP -> true
        // NT8 bracket pattern from DW-B134. EndsWith("STP", OrdinalIgnoreCase) fires.
        // REQ-DW-LB-SFB-01-8
        // ------------------------------------------------------------------
        [Fact]
        public void Buy_STP_ReturnsTrue()
        {
            Assert.True(IsBracketLegStatic("Buy STP", false));
        }

        // ------------------------------------------------------------------
        // T9: Entry -> false
        // Entry orders are never bracket legs. No clause matches.
        // REQ-DW-LB-SFB-01-10
        // ------------------------------------------------------------------
        [Fact]
        public void Entry_ReturnsFalse()
        {
            Assert.False(IsBracketLegStatic("Entry", false));
        }

        // ------------------------------------------------------------------
        // T10: null name -> false
        // Null name guard: name == null check returns false before any StartsWith.
        // REQ-DW-LB-SFB-01-9
        // ------------------------------------------------------------------
        [Fact]
        public void NullName_ReturnsFalse()
        {
            Assert.False(IsBracketLegStatic(null, false));
        }

        // ------------------------------------------------------------------
        // T11: null order analog -> false
        // Represents an Order with no FromEntrySignal and null Name.
        // Same input as T10 but documented separately to capture both scenarios
        // named in the spec (null name guard + null order guard).
        // REQ-DW-LB-SFB-01-9
        // ------------------------------------------------------------------
        [Fact]
        public void NullOrderAnalog_ReturnsFalse()
        {
            Assert.False(IsBracketLegStatic(null, false));
        }
    }
}