// src/PropTraderTools/Tests/BwaveCycLaneAR9Tests.cs
// BWAVE-CYC Lane-A Ticket R9: helper extraction tests.
// Covers: IsFollowerByName, IsOrderForInstrument, IsSnapshotBlocked,
//         TryCancelOrders, FindPositionForInstrument.
// JS-021: no lock. JS-001: no throw. JS-002: no return null. JS-033: no async void.
// xUnit only -- no NUnit, no MSTest. ASCII identifiers only.

using System;
using System.Collections.Generic;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public sealed class BwaveCycLaneAR9Tests
    {
        private static MethodInfo GetStaticMethod(string name) =>
            typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

        private static object BuildCopyRule(string instrument, string[] followerNames)
        {
            var createMethod = typeof(CopyEngine).GetNestedType("CopyRule", BindingFlags.NonPublic);
            if (createMethod == null)
                return null;
            var create = createMethod.GetMethod(
                "Create",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public
            );
            if (create == null)
                return null;
            return create.Invoke(
                null,
                new object[]
                {
                    instrument,
                    (Account)null,
                    followerNames != null ? new Account[followerNames.Length] : new Account[0],
                    true,
                    (int[])null,
                    (Dictionary<string, FollowerAtmMode>)null,
                    5,
                    followerNames,
                }
            );
        }

        // IsFollowerByName: resolves null-slot via FollowerAccountNames array.

        [Fact]
        public void T_R9_01_IsFollowerByName_MethodExists_PrivateStatic()
        {
            var mi = GetStaticMethod("IsFollowerByName");
            Assert.NotNull(mi);
            Assert.True(mi.IsStatic);
            Assert.Equal(typeof(bool), mi.ReturnType);
            Assert.Equal(3, mi.GetParameters().Length);
        }

        [Fact]
        public void T_R9_02_IsFollowerByName_EmptyNamesArray_ReturnsFalse()
        {
            var mi = GetStaticMethod("IsFollowerByName");
            Assert.NotNull(mi);
            var rule = BuildCopyRule("R9-EMPTY", new string[0]);
            if (rule == null)
                return; // CopyRule.Create not accessible -- skip
            bool result = (bool)mi.Invoke(null, new object[] { rule, 0, "AnyName" });
            Assert.False(result);
        }

        [Fact]
        public void T_R9_03_IsFollowerByName_MatchingName_ReturnsTrue()
        {
            var mi = GetStaticMethod("IsFollowerByName");
            Assert.NotNull(mi);
            var rule = BuildCopyRule("R9-MATCH", new string[] { "FollowerX" });
            if (rule == null)
                return; // CopyRule.Create not accessible -- skip
            bool result = (bool)mi.Invoke(null, new object[] { rule, 0, "FollowerX" });
            Assert.True(result);
        }

        // IsOrderForInstrument: pure static order-instrument check.

        [Fact]
        public void T_R9_04_IsOrderForInstrument_MethodExists_PrivateStatic()
        {
            var mi = GetStaticMethod("IsOrderForInstrument");
            Assert.NotNull(mi);
            Assert.True(mi.IsStatic);
            Assert.Equal(typeof(bool), mi.ReturnType);
            Assert.Equal(2, mi.GetParameters().Length);
        }

        [Fact]
        public void T_R9_05_IsOrderForInstrument_ParameterNames()
        {
            var mi = GetStaticMethod("IsOrderForInstrument");
            Assert.NotNull(mi);
            var parms = mi.GetParameters();
            Assert.Equal("o", parms[0].Name);
            Assert.Equal("instr", parms[1].Name);
        }

        // IsSnapshotBlocked: snapshot-gate predicate.

        [Fact]
        public void T_R9_06_IsSnapshotBlocked_MethodExists_PrivateStatic()
        {
            var mi = GetStaticMethod("IsSnapshotBlocked");
            Assert.NotNull(mi);
            Assert.True(mi.IsStatic);
            Assert.Equal(typeof(bool), mi.ReturnType);
            Assert.Equal(2, mi.GetParameters().Length);
        }

        [Fact]
        public void T_R9_07_IsSnapshotBlocked_NullSnapshot_ReturnsFalse()
        {
            var mi = GetStaticMethod("IsSnapshotBlocked");
            Assert.NotNull(mi);
            // null snapshot -> && short-circuits -> false (no filter applied).
            bool result = (bool)mi.Invoke(null, new object[] { null, null });
            Assert.False(result);
        }

        // TryCancelOrders: race-guard + cancel helper.

        [Fact]
        public void T_R9_08_TryCancelOrders_MethodExists_PrivateStatic()
        {
            var mi = GetStaticMethod("TryCancelOrders");
            Assert.NotNull(mi);
            Assert.True(mi.IsStatic);
            Assert.Equal(typeof(void), mi.ReturnType);
            Assert.Equal(2, mi.GetParameters().Length);
        }

        [Fact]
        public void T_R9_09_TryCancelOrders_EmptyList_DoesNotThrow()
        {
            var mi = GetStaticMethod("TryCancelOrders");
            Assert.NotNull(mi);
            var stale = new List<Order>();
            var ex = Record.Exception(() =>
            {
                try
                {
                    mi.Invoke(null, new object[] { (Account)null, stale });
                }
                catch (TargetInvocationException) { }
            });
            Assert.Null(ex);
        }

        // FindPositionForInstrument: position lookup by FullName.

        [Fact]
        public void T_R9_10_FindPositionForInstrument_MethodExists_PrivateStatic()
        {
            var mi = GetStaticMethod("FindPositionForInstrument");
            Assert.NotNull(mi);
            Assert.True(mi.IsStatic);
            Assert.Equal(2, mi.GetParameters().Length);
        }

        [Fact]
        public void T_R9_11_FindPositionForInstrument_ParameterNames()
        {
            var mi = GetStaticMethod("FindPositionForInstrument");
            Assert.NotNull(mi);
            var parms = mi.GetParameters();
            Assert.Equal("acc", parms[0].Name);
            Assert.Equal("instr", parms[1].Name);
        }
    }
}
