// src/PropTraderTools/Tests/BwaveRefactorLaneCTests.cs
// BWAVE-REFACTOR LaneC -- structural existence tests.
// 1 [Fact] per extracted helper. Reflection-based only -- no behavioral test.
// xUnit only. No NUnit. No MSTest. ASCII-only identifiers.

using System;
using System.Reflection;
using Xunit;

namespace PropTraderTools.Tests
{
    public class BwaveRefactorLaneCTests
    {
        // C-1: PttQuickExit helpers
        [Fact]
        public void PttQuickExit_SubmitStopOrder_Exists()
        {
            var m = typeof(PttQuickExit).GetMethod(
                "SubmitStopOrder",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(m);
            Assert.Equal(7, m.GetParameters().Length);
        }

        [Fact]
        public void PttQuickExit_SubmitTargetOrder_Exists()
        {
            var m = typeof(PttQuickExit).GetMethod(
                "SubmitTargetOrder",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(m);
            Assert.Equal(7, m.GetParameters().Length);
        }

        [Fact]
        public void PttQuickExit_SubmitQxOcoPair_Exists()
        {
            var m = typeof(PttQuickExit).GetMethod(
                "SubmitQxOcoPair",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(m);
            Assert.Equal(12, m.GetParameters().Length);
        }

        // C-1: PttGlobalQuickExit helpers
        [Fact]
        public void PttGlobalQuickExit_IsTargetOrder_Exists()
        {
            var m = typeof(PttGlobalQuickExit).GetMethod(
                "IsTargetOrder",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(2, m.GetParameters().Length);
        }

        [Fact]
        public void PttGlobalQuickExit_DeduplicateByPrice_Exists()
        {
            var m = typeof(PttGlobalQuickExit).GetMethod(
                "DeduplicateByPrice",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
        }

        [Fact]
        public void PttGlobalQuickExit_LogLeaderDiag_Exists()
        {
            var m = typeof(PttGlobalQuickExit).GetMethod(
                "LogLeaderDiag",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(3, m.GetParameters().Length);
        }

        [Fact]
        public void PttGlobalQuickExit_IsNonTerminalForInstr_Exists()
        {
            var m = typeof(PttGlobalQuickExit).GetMethod(
                "IsNonTerminalForInstr",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(2, m.GetParameters().Length);
        }

        // C-1: PttBreakEven helpers
        [Fact(Skip="B180-2: PttBreakEven deleted")]
        public void PttBreakEven_IsCancellableState_Exists()
        {
            var m = Type.GetType("PropTraderTools.PttBreakEven").GetMethod(
                "IsCancellableState",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
        }

        [Fact(Skip="B180-2: PttBreakEven deleted")]
        public void PttBreakEven_IsStaleOrder_Exists()
        {
            var m = Type.GetType("PropTraderTools.PttBreakEven").GetMethod(
                "IsStaleOrder",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(2, m.GetParameters().Length);
        }

        [Fact(Skip="B180-2: PttBreakEven deleted")]
        public void PttBreakEven_SubmitBareStop_Exists()
        {
            var m = Type.GetType("PropTraderTools.PttBreakEven").GetMethod(
                "SubmitBareStop",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(4, m.GetParameters().Length);
        }

        [Fact(Skip="B180-2: PttBreakEven deleted")]
        public void PttBreakEven_SubmitBePair_Exists()
        {
            var m = Type.GetType("PropTraderTools.PttBreakEven").GetMethod(
                "SubmitBePair",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(7, m.GetParameters().Length);
        }

        [Fact(Skip="B180-2: PttBreakEven deleted")]
        public void PttBreakEven_IsSnapshotEligibleState_Exists()
        {
            var m = Type.GetType("PropTraderTools.PttBreakEven").GetMethod(
                "IsSnapshotEligibleState",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
        }

        [Fact(Skip="B180-2: PttBreakEven deleted")]
        public void PttBreakEven_IsInvalidInput_Exists()
        {
            var m = Type.GetType("PropTraderTools.PttBreakEven").GetMethod(
                "IsInvalidInput",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(2, m.GetParameters().Length);
        }

        [Fact(Skip="B180-2: PttBreakEven deleted")]
        public void PttBreakEven_SafeName_Exists()
        {
            var m = Type.GetType("PropTraderTools.PttBreakEven").GetMethod(
                "SafeName",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
        }

        // C-2: PttBreakEvenSwap helpers
        [Fact]
        public void PttBreakEvenSwap_SubmitBareStopSwap_Exists()
        {
            var m = typeof(PttBreakEvenSwap).GetMethod(
                "SubmitBareStopSwap",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(6, m.GetParameters().Length);
        }

        [Fact]
        public void PttBreakEvenSwap_SubmitSwapPair_Exists()
        {
            var m = typeof(PttBreakEvenSwap).GetMethod(
                "SubmitSwapPair",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(8, m.GetParameters().Length);
        }

        // C-2: PttTrim helpers
        [Fact]
        public void PttTrim_ResolveOrderParams_Exists()
        {
            var m = typeof(PttTrim).GetMethod(
                "ResolveOrderParams",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(5, m.GetParameters().Length);
        }

        // C-2: PttFlatten helpers
        [Fact]
        public void PttFlatten_ResolveOrderParams_Exists()
        {
            var m = typeof(PttFlatten).GetMethod(
                "ResolveOrderParams",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(5, m.GetParameters().Length);
        }

        // C-2: PttCancel helpers
        [Fact]
        public void PttCancel_IsWorkingEntryOrder_Exists()
        {
            var m = typeof(PttCancel).GetMethod(
                "IsWorkingEntryOrder",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(m);
            Assert.Equal(2, m.GetParameters().Length);
        }
    }
}
