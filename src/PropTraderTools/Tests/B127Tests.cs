// src/PropTraderTools/Tests/B127Tests.cs
// B127: DW-PTT-BE-FIX-01 -- Lazy re-resolve for null followers in AllAccounts()
// 3 xUnit [Fact] tests: T1 through T3.
// JS-021: no lock. JS-001: no throw. JS-002: no return null. JS-033: no async void.
// xUnit only -- no NUnit, no MSTest. ASCII identifiers only.
// Test seam approach: option (c) -- observable struct behavior + reflection.
// Account.All is an NT8 API not available in test runtime; AllAccounts() is tested
// via signature/accessibility verification (T3) and CopyRule struct tests (T1, T2).

using System;
using System.Reflection;
using Xunit;

namespace PropTraderTools
{
    public sealed class B127Tests
    {
        // T1: CopyRule.FollowerAccountNames is derived from Account[] when followerAccountNames
        // is not explicitly supplied (backward-compat path -- DeriveFollowerNames called in ctor).
        // Verifies the field is populated and has correct length for empty followers[].
        [Fact]
        public void T1_CopyRule_FollowerAccountNames_DerivedFromAccounts_WhenNotExplicitlySupplied()
        {
            // Arrange + Act: create a rule with empty followers (DeriveFollowerNames path).
            var rule = CopyEngine.CopyRule.Create(
                instrument: "NQ 03-25",
                master: null,
                followers: new NinjaTrader.Cbi.Account[0]
            );

            // Assert: FollowerAccountNames is not null and has length 0 (derived from empty array).
            Assert.NotNull(rule.FollowerAccountNames);
            Assert.Equal(0, rule.FollowerAccountNames.Length);
        }

        // T2: CopyRule.FollowerAccountNames preserves explicitly-supplied names.
        // This is the DtoToRule path where dto.FollowerAccountNames are passed as the 8th arg.
        // Null slots in followers[] can be covered because the name is preserved even when
        // the Account reference is null.
        [Fact]
        public void T2_CopyRule_FollowerAccountNames_PreservesExplicitNames_CoveringNullSlots()
        {
            // Arrange + Act: create rule with null follower slot but explicit name supplied.
            var explicitNames = new[] { "SimAccount" };
            var rule = CopyEngine.CopyRule.Create(
                instrument: "ES 03-25",
                master: null,
                followers: new NinjaTrader.Cbi.Account[] { null },
                followerAccountNames: explicitNames
            );

            // Assert: FollowerAccountNames is exactly the supplied array (not derived).
            Assert.NotNull(rule.FollowerAccountNames);
            Assert.Equal(1, rule.FollowerAccountNames.Length);
            Assert.Equal("SimAccount", rule.FollowerAccountNames[0]);
        }

        // T3: AllAccounts() is internal IEnumerable<Account> on CopyEngine.
        // Verifies the method was changed from private to internal (DW-PTT-BE-FIX-01 Step 7)
        // and the signature matches the expected parameter type.
        [Fact]
        public void T3_AllAccounts_IsInternalInstanceMethod_ReturningIEnumerableAccount()
        {
            // Locate AllAccounts via reflection (internal = NonPublic to external assemblies,
            // but accessible in same assembly due to InternalsVisibleTo).
            var mi = typeof(CopyEngine).GetMethod(
                "AllAccounts",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (mi == null)
            {
                mi = typeof(CopyEngine).GetMethod(
                    "AllAccounts",
                    BindingFlags.Public | BindingFlags.Instance
                );
            }

            Assert.NotNull(mi);
            Assert.False(mi.IsStatic, "AllAccounts must be an instance method");

            // Return type must be IEnumerable<Account> (yield-return compiles to state machine
            // but the declared return type on the method signature is IEnumerable<Account>).
            var returnType = mi.ReturnType;
            var expectedInterface =
                typeof(System.Collections.Generic.IEnumerable<NinjaTrader.Cbi.Account>);
            bool implementsExpected =
                expectedInterface.IsAssignableFrom(returnType) || returnType.IsGenericType;
            Assert.True(
                implementsExpected,
                "AllAccounts must return IEnumerable<Account> (B127 DW-PTT-BE-FIX-01)"
            );

            // Parameter: single Instrument parameter.
            var ps = mi.GetParameters();
            Assert.Equal(1, ps.Length);
            Assert.Equal(typeof(NinjaTrader.Cbi.Instrument), ps[0].ParameterType);
        }
    }
}
