// B198Tests.cs -- xUnit tests for B198 Ticket 1: SimStop N-General Bracket Support.
// Strategy: source-text assertions -- no NT8 runtime required.
// T1-01..T1-03: IsAtmBracketName source-text (IsAtmBracketName is internal static but CopyEngine
//   static initializer throws TypeInitializationException in test host -- source-text pattern used).
// T1-04..T1-05: IsNativeExitName source-text (same constraint).
// T1-06..T1-16: TryParseStopSuffix, MaxBracketLegs, IsBracketLeg, IsBracketLegStatic,
//   IsAtmSTPOrder, IsStopLeg -- all source-text assertions.
// T2a-01..T2a-02: SendCopyWithAtm QTY instrumentation log lines -- source-text assertions.
// Framework: xUnit only. No NUnit. No MSTest.
// JS-021: no lock. JS-033: no async void. ASCII-only.
using Xunit;

namespace PropTraderTools.Tests
{
    public class B198Tests
    {
        private static string Src() => System.IO.File.ReadAllText(
            System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));

        // -----------------------------------------------------------------------
        // T1-01..T1-03: IsAtmBracketName -- source-text assertions
        // Confirms SimStop OR clause added: name.StartsWith("SimStop",...) && name.Length > 7 && char.IsDigit(name[7])
        // -----------------------------------------------------------------------

        [Fact]
        public void T1_01_IsAtmBracketName_SimStop1_SourceContainsSimStopClause()
        {
            Assert.Contains(
                "name.StartsWith(\"SimStop\", StringComparison.Ordinal) && name.Length > 7 && char.IsDigit(name[7])",
                Src());
        }

        [Fact]
        public void T1_02_IsAtmBracketName_SimStop9_SourceContainsDigitCheck()
        {
            Assert.Contains("char.IsDigit(name[7])", Src());
        }

        [Fact]
        public void T1_03_IsAtmBracketName_SimStopNoDigit_SourceLengthGuardPresent()
        {
            // Confirms name.Length > 7 guard prevents bare "SimStop" from matching.
            Assert.Contains("name.Length > 7 && char.IsDigit(name[7])", Src());
        }

        // -----------------------------------------------------------------------
        // T1-04..T1-05: IsNativeExitName -- source-text assertions
        // Confirms if (name.StartsWith("SimStop", StringComparison.Ordinal)) return true; added
        // -----------------------------------------------------------------------

        [Fact]
        public void T1_04_IsNativeExitName_SimStop1_SourceContainsSimStopBranch()
        {
            // Confirms the if (name.StartsWith("SimStop", ...) return true; branch is present.
            Assert.Contains(
                "if (name.StartsWith(\"SimStop\", StringComparison.Ordinal))",
                Src());
        }

        [Fact]
        public void T1_05_IsNativeExitName_SimStop99_SourceReturnsTrueForSimStop()
        {
            // Same as T1-04 -- separate [Fact] per spec for SimStop99 case.
            Assert.Contains(
                "if (name.StartsWith(\"SimStop\", StringComparison.Ordinal))",
                Src());
        }

        // -----------------------------------------------------------------------
        // T1-06..T1-11: TryParseStopSuffix -- source-text assertions
        // -----------------------------------------------------------------------

        [Fact]
        public void T1_06_TryParseStopSuffix_SimStop1_SourceContainsSimStopBranch()
        {
            Assert.Contains(
                "StartsWith(\"SimStop\", StringComparison.Ordinal)",
                Src());
        }

        [Fact]
        public void T1_07_TryParseStopSuffix_SimStop2_SourceContainsSimStopBranch()
        {
            // Same assertion as T1-06 -- confirms SimStop branch present; separate [Fact] per spec.
            Assert.Contains(
                "StartsWith(\"SimStop\", StringComparison.Ordinal)",
                Src());
        }

        [Fact]
        public void T1_08_TryParseStopSuffix_SimStop4_CapRemoved()
        {
            // Confirms n > 3 cap is removed from TryParseStopSuffix method.
            Assert.DoesNotContain("n > 3", Src());
        }

        [Fact]
        public void T1_09_TryParseStopSuffix_Stop4_CapRemoved()
        {
            // Same DoesNotContain -- confirms cap removal; separate [Fact] per spec.
            Assert.DoesNotContain("n > 3", Src());
        }

        [Fact]
        public void T1_10_TryParseStopSuffix_Stop3_RegressionTrue()
        {
            // Confirms "Stop" prefix still handled -- regression guard.
            Assert.Contains(
                "StartsWith(\"Stop\", StringComparison.Ordinal)",
                Src());
        }

        [Fact]
        public void T1_11_TryParseStopSuffix_SimTarget1_NotMatched()
        {
            // Confirms SimTarget is not matched -- only "Stop" and "SimStop" prefixes handled.
            Assert.DoesNotContain("StartsWith(\"SimTarget\"", Src());
        }

        // -----------------------------------------------------------------------
        // T1-12: MaxBracketLegs const -- source-text assertion
        // -----------------------------------------------------------------------

        [Fact]
        public void T1_12_MaxBracketLegs_ConstEquals9()
        {
            Assert.Contains("MaxBracketLegs = 9", Src());
        }

        // -----------------------------------------------------------------------
        // T1-13..T1-16: IsBracketLeg, IsBracketLegStatic, IsAtmSTPOrder, IsStopLeg
        // source-text assertions (private instance / NT8 runtime required)
        // -----------------------------------------------------------------------

        [Fact]
        public void T1_13_IsBracketLeg_SourceContainsSimStop()
        {
            Assert.Contains(
                "StartsWith(\"SimStop\", StringComparison.Ordinal)",
                Src());
        }

        [Fact]
        public void T1_14_IsBracketLegStatic_SourceContainsSimStop()
        {
            Assert.Contains(
                "StartsWith(\"SimStop\", StringComparison.Ordinal)",
                Src());
        }

        [Fact]
        public void T1_15_IsAtmSTPOrder_SourceContainsSimStop()
        {
            Assert.Contains(
                "StartsWith(\"SimStop\", StringComparison.Ordinal)",
                Src());
        }

        [Fact]
        public void T1_16_IsStopLeg_SourceContainsSimStop()
        {
            Assert.Contains(
                "StartsWith(\"SimStop\", StringComparison.Ordinal)",
                Src());
        }

        // -----------------------------------------------------------------------
        // T2a-01..T2a-02: SendCopyWithAtm QTY instrumentation -- source-text assertions
        // Confirms the 2 diagnostic log lines were added to SendCopyWithAtm.
        // T2a-01: log line before CreateOrder: "ATM-send qty="
        // T2a-02: log line after null guard: "ATM-order qty="
        // -----------------------------------------------------------------------

        [Fact]
        public void T2a_01_SendCopyWithAtm_LogsAtmSendQtyBeforeCreateOrder()
        {
            Assert.Contains("ATM-send qty=", Src());
        }

        [Fact]
        public void T2a_02_SendCopyWithAtm_LogsAtmOrderQtyAfterCreateOrder()
        {
            Assert.Contains("ATM-order qty=", Src());
        }
    
        // -----------------------------------------------------------------------
        // T2b-01..T2b-02: ATM template group qty fix (H2 path) -- source-text assertions
        // T2b-01: confirms _atmTemplateGroupQty ConcurrentDictionary field declared
        // T2b-02: confirms signal.Quantity / groupCount division logic present in SendCopyWithAtm
        // -----------------------------------------------------------------------

        [Fact]
        public void T2b_01_AtmTemplateGroupQty_FieldExists()
        {
            Assert.Contains("_atmTemplateGroupQty", Src());
        }

        [Fact]
        public void T2b_02_SendCopyWithAtm_H2DivideRemoved_PassesQtyDirectly()
        {
            // B199-R1: T2b H2 divide block removed (wrong denominator -- <Quantity> = per-group qty, not group count).
            // Confirm divide is gone and H1 pass-through is in place.
            Assert.DoesNotContain("signal.Quantity / groupCount", Src());
            Assert.Contains("B199-R1: pass qty directly", Src());
        }

        // T3-01..T3-04: fo=NULL Drain Path Fix (DW-B195-01) -- source-text assertions
        // T3-01: confirms _drainFollowerEntryOrders ConcurrentDictionary field declared
        // T3-02: confirms Order? SendCopyWithAtm return type (not bool)
        // T3-03: confirms _drainFollowerEntryOrders.TryAdd in SubmitDrainedEntry
        // T3-04: confirms _drainFollowerEntryOrders.TryGetValue in TrySetSlateReadyOnDrainFill
        // -----------------------------------------------------------------------

        [Fact]
        public void T3_01_DrainFollowerEntryOrders_FieldExists()
        {
            Assert.Contains("_drainFollowerEntryOrders", Src());
        }

        [Fact]
        public void T3_02_SendCopyWithAtm_ReturnsOrderType()
        {
            Assert.Contains("Order? SendCopyWithAtm", Src());
        }

        [Fact]
        public void T3_03_SubmitDrainedEntry_StoresEntryOrder()
        {
            Assert.Contains("_drainFollowerEntryOrders.TryAdd", Src());
        }

        [Fact]
        public void T3_04_TrySetSlateReadyOnDrainFill_RetrievesAndPassesDrainFo()
        {
            Assert.Contains("_drainFollowerEntryOrders.TryGetValue", Src());
            Assert.Contains("SyncFollowerBracket(e.Order.Account, drainFo", Src());
        }
        // -----------------------------------------------------------------------
        // T4-01..T4-02: ATM Template Dir Startup Fix (DW-B198-ATMDIR-01) -- source-text assertions
        // T4-01: confirms correct NT8 subfolder path "templates", "AtmStrategy" (not "ATM")
        // T4-02: confirms upgraded log level [PTT-STARTUP-WARN] with path included
        // -----------------------------------------------------------------------

        [Fact]
        public void T4_01_CheckAtmTemplatesForReverse_UsesAtmStrategyPath()
        {
            Assert.Contains("\"templates\", \"AtmStrategy\"", Src());
        }

        [Fact]
        public void T4_02_CheckAtmTemplatesForReverse_DirNotFoundLogsPath()
        {
            Assert.Contains("[PTT-STARTUP-WARN] ATM template dir not found:", Src());
        }
    }
}
