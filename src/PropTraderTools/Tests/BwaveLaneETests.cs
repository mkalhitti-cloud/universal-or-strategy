// src/PropTraderTools/Tests/BwaveLaneETests.cs
// WAVE2-LANE-E -- Ticket E-1 structural existence tests for 6 extracted helpers.
// One [Fact] per helper: IsFlatOrMissing, IsFollowerSkip, LeaderName,
//   ResolveTick, ComputeExitPrices, NewQxOcoId.
// WAVE2-LANE-E-2 appended: IsNativeTargetOrder, IsPttTargetOrder, IsInvalidForcedTargets.
// xUnit only. No NUnit. No MSTest. ASCII-only identifiers.

using System;
using System.Reflection;
using Xunit;

namespace PropTraderTools.Tests
{
    public class BwaveLaneETests
    {
        private static readonly BindingFlags NonPublicStatic =
            BindingFlags.NonPublic | BindingFlags.Static;

        [Fact]
        public void PttQuickExit_IsFlatOrMissing_Exists()
        {
            var m = typeof(PttQuickExit).GetMethod("IsFlatOrMissing", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(3, m.GetParameters().Length);
            Assert.Equal(typeof(bool), m.ReturnType);
        }

        [Fact]
        public void PttQuickExit_IsFollowerSkip_Exists()
        {
            var m = typeof(PttQuickExit).GetMethod("IsFollowerSkip", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(2, m.GetParameters().Length);
            Assert.Equal(typeof(bool), m.ReturnType);
        }

        [Fact]
        public void PttQuickExit_LeaderName_Exists()
        {
            var m = typeof(PttQuickExit).GetMethod("LeaderName", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
            Assert.Equal(typeof(string), m.ReturnType);
        }

        [Fact]
        public void PttQuickExit_ResolveTick_Exists()
        {
            var m = typeof(PttQuickExit).GetMethod("ResolveTick", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
            Assert.Equal(typeof(double), m.ReturnType);
        }

        [Fact]
        public void PttQuickExit_ComputeExitPrices_Exists()
        {
            var m = typeof(PttQuickExit).GetMethod("ComputeExitPrices", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(4, m.GetParameters().Length);
            Assert.True(m.ReturnType.IsValueType);
        }

        [Fact]
        public void PttQuickExit_NewQxOcoId_Exists()
        {
            var m = typeof(PttQuickExit).GetMethod("NewQxOcoId", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(0, m.GetParameters().Length);
            Assert.Equal(typeof(string), m.ReturnType);
        }

        [Fact]
        public void PttGlobalQuickExit_IsNativeTargetOrder_Exists()
        {
            var m = typeof(PttGlobalQuickExit).GetMethod("IsNativeTargetOrder", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
            Assert.Equal(typeof(bool), m.ReturnType);
        }

        [Fact]
        public void PttGlobalQuickExit_IsPttTargetOrder_Exists()
        {
            var m = typeof(PttGlobalQuickExit).GetMethod("IsPttTargetOrder", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
            Assert.Equal(typeof(bool), m.ReturnType);
        }

        [Fact]
        public void PttGlobalQuickExit_IsInvalidForcedTargets_Exists()
        {
            var m = typeof(PttGlobalQuickExit).GetMethod(
                "IsInvalidForcedTargets",
                NonPublicStatic
            );
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
            Assert.Equal(typeof(bool), m.ReturnType);
        }


        [Fact]
        public void PttBreakEven_IsSnapshotTargetOrder_Exists()
        {
            var m = typeof(PttBreakEven).GetMethod("IsSnapshotTargetOrder", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(2, m.GetParameters().Length);
            Assert.Equal(typeof(bool), m.ReturnType);
        }

        [Fact]
        public void PttBreakEvenSwap_HasNoTargets_Exists()
        {
            var m = typeof(PttBreakEvenSwap).GetMethod("HasNoTargets", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(1, m.GetParameters().Length);
            Assert.Equal(typeof(bool), m.ReturnType);
        }

        [Fact]
        public void PttFlatten_FormatOrderPrice_Exists()
        {
            var m = typeof(PttFlatten).GetMethod("FormatOrderPrice", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(2, m.GetParameters().Length);
            Assert.Equal(typeof(string), m.ReturnType);
        }

        [Fact]
        public void PttTrim_FormatOrderPrice_Exists()
        {
            var m = typeof(PttTrim).GetMethod("FormatOrderPrice", NonPublicStatic);
            Assert.NotNull(m);
            Assert.Equal(2, m.GetParameters().Length);
            Assert.Equal(typeof(string), m.ReturnType);
        }
    }
}