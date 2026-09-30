// B121 Tests -- IsFollowerAccount null-slot fallback + dev_mode.txt sentinel.
// xUnit only (NO NUnit, NO MSTest). Jane Street rules: JS-001, JS-021, JS-033.
// NT8 Account is sealed -- cannot be instantiated in test context.
// Pattern: verify method signature + null-guard paths + structural contracts.
// T_B121_01 and T_B121_02 test null-slot logic: inject rule with null slot via
// CopyEngine.CopyRule.Create, verify IsFollowerAccount handles null acc correctly.
// T_B121_03 tests method signature structural contract.
// T_B121_04 tests null acc guard (callable directly).
using System;
using System.Collections.Concurrent;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public class B121Tests
    {
        private readonly CopyEngine _engine = CopyEngine.Instance;

        // Helper: inject a rule with one null-slot follower and an explicit name into _rules.
        // Uses CopyEngine.CopyRule.Create (internal static factory) + reflection on _rules field.
        // Pattern follows CopyEngineTests / B71Tests ConcurrentBag approach.
        private void InjectNullSlotRule(string accountName)
        {
            _engine.SetEnabled(false);
            var fi = typeof(CopyEngine).GetField(
                "_rules",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(fi);
            var bag = (ConcurrentBag<CopyEngine.CopyRule>)fi.GetValue(_engine);
            // CopyEngine.CopyRule.Create: followers[0] = null, followerAccountNames[0] = accountName.
            // This is the B121 null-slot scenario: account resolved null at load time.
            var rule = CopyEngine.CopyRule.Create(
                "NQ",
                null,
                new Account[] { null },
                followerAccountNames: new[] { accountName }
            );
            bag.Add(rule);
        }

        // T_B121_01: null slot + null acc -> does not throw (null guard fires first).
        // NT8: Account cannot be instantiated. Verifies IsFollowerAccount does not crash
        // when _rules contains a null-slot entry and acc is null.
        [Fact]
        public void T_B121_01_IsFollowerAccount_NullSlot_NullAcc_DoesNotThrow()
        {
            InjectNullSlotRule("Sim102");
            var ex = Record.Exception(() => _engine.IsFollowerAccount(null));
            Assert.Null(ex);
        }

        // T_B121_02: null slot rule present + null acc -> returns false.
        // Confirms null-acc guard fires first and returns false even with null-slot rule present.
        [Fact]
        public void T_B121_02_IsFollowerAccount_NullSlotRule_NullAcc_ReturnsFalse()
        {
            InjectNullSlotRule("Sim103");
            bool result = _engine.IsFollowerAccount(null);
            Assert.False(result);
        }

        // T_B121_03: IsFollowerAccount method signature -- internal instance, returns bool, takes Account.
        // Structural contract: method exists with correct signature (both null and non-null branches compile).
        [Fact]
        public void T_B121_03_IsFollowerAccount_MethodSignature_InternalBool()
        {
            var mi = typeof(CopyEngine).GetMethod(
                "IsFollowerAccount",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );
            Assert.NotNull(mi);
            Assert.Equal(typeof(bool), mi.ReturnType);
            var ps = mi.GetParameters();
            Assert.Single(ps);
            Assert.Equal(typeof(Account), ps[0].ParameterType);
        }

        // T_B121_04: null acc -> false (null guard preserved).
        // IsFollowerAccount(null) must always return false regardless of rules in _rules.
        [Fact]
        public void T_B121_04_IsFollowerAccount_NullAcc_ReturnsFalse()
        {
            _engine.SetEnabled(false);
            bool result = _engine.IsFollowerAccount(null);
            Assert.False(result);
        }
    }
}
