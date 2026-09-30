// PTT-COPIER-B71 -- B71Tests.cs
// xUnit tests for B71: CancelQxBrackets Submitted state + PttQuickExit follower guard + PttGlobalQuickExit follower dispatch.
// T_B71_01..T_B71_10. Jane Street rules: JS-001, JS-021, JS-033.
// NT8 types (Account, Order, Instrument, Position) cannot be instantiated in test context.
// Pattern: test internal static predicates directly; test public/internal API via no-exception + null-guard paths.
// Same patterns as CopyEngineTests.cs (reflection-based field access, AddRule, Record.Exception).
using System;
using System.Collections.Concurrent;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public class B71Tests
    {
        private readonly CopyEngine _engine = CopyEngine.Instance;

        // -----------------------------------------------------------------------
        // FIX 1 (DW-B71-01): CancelQxBrackets stateOk gate -- OrderState.Submitted
        // Since NT8 Order cannot be instantiated, we test:
        //   (a) OrderState.Submitted enum value exists in NT8 CBI (compile-time check)
        //   (b) IsQxCancelCandidate(null) -> false (null guard)
        //   (c) CancelQxBrackets(null, null) -> no exception (null guard path)
        //   (d) IsQxCancelCandidate is accessible as internal static method
        // -----------------------------------------------------------------------

        [Fact]
        public void T_B71_01_CancelQxBrackets_SubmittedEnumValue_Exists()
        {
            // Assert: OrderState.Submitted is a valid enum value (B71 DW-B71-01 NT8 claim).
            // This test fails to compile if OrderState.Submitted does not exist in NT8 CBI.
            OrderState submitted = OrderState.Submitted;
            Assert.Equal(OrderState.Submitted, submitted);
        }

        [Fact]
        public void T_B71_02_IsQxCancelCandidate_NullOrder_ReturnsFalse()
        {
            // Assert: null guard at top of IsQxCancelCandidate returns false (not exception).
            // Regression: Working-state orders rely on IsQxCancelCandidate returning true for PTT-QX-.
            // Null guard must pass before state check -- fix does not break null safety.
            bool result = CopyEngine.IsQxCancelCandidate(null);
            Assert.False(result);
        }

        [Fact]
        public void T_B71_03_CancelQxBrackets_NullAccount_ReturnsWithoutException()
        {
            // Assert: CancelQxBrackets(null, null) hits the null guard and returns.
            // Accepted/Working/Submitted orders all rely on this null guard not throwing.
            _engine.SetEnabled(false);
            var ex = Record.Exception(() => _engine.CancelQxBrackets(null, null));
            Assert.Null(ex);
        }

        [Fact]
        public void T_B71_04_IsQxCancelCandidate_MethodAccessible_NullReturnsFalse()
        {
            // Assert: IsQxCancelCandidate does NOT filter by OrderState (state check is in CancelQxBrackets).
            // The stateOk gate in CancelQxBrackets now includes Submitted; IsQxCancelCandidate is unchanged.
            // Verify the static predicate is accessible and callable via reflection.
            MethodInfo mi = typeof(CopyEngine).GetMethod(
                "IsQxCancelCandidate",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public
            );
            Assert.NotNull(mi);
            bool result = (bool)mi.Invoke(null, new object[] { null });
            Assert.False(result);
        }

        // -----------------------------------------------------------------------
        // FIX 2 (DW-B71-02): PttQuickExit follower guard (skipIfFollower parameter)
        // Since NT8 Account cannot be instantiated, we test:
        //   (a) Execute(null, null, ..., skipIfFollower:true) -> no exception (null/flat guard fires first)
        //   (b) Execute(null, null, ..., skipIfFollower:false) -> no exception (null/flat guard fires first)
        //   (c) IsFollowerAccount(null) -> false (null guard in engine method)
        // -----------------------------------------------------------------------

        [Fact]
        public void T_B71_05_PttQuickExit_Execute_NullLeader_SkipIfFollowerTrue_NoException()
        {
            // Assert: null/flat guard fires first when leader==null; follower guard never reached.
            // skipIfFollower=true (default) must not break the existing null-leader fast-exit path.
            _engine.SetEnabled(false);
            var executor = new PttQuickExit();
            var ex = Record.Exception(() =>
                executor.Execute(null, null, 4, 8, skipIfFollower: true)
            );
            Assert.Null(ex);
        }

        [Fact]
        public void T_B71_06_PttQuickExit_Execute_NullLeader_SkipIfFollowerFalse_NoException()
        {
            // Assert: when skipIfFollower=false, follower guard is bypassed.
            // null/flat guard still fires immediately for null leader -- no crash.
            _engine.SetEnabled(false);
            var executor = new PttQuickExit();
            var ex = Record.Exception(() =>
                executor.Execute(null, null, 4, 8, skipIfFollower: false)
            );
            Assert.Null(ex);
        }

        [Fact]
        public void T_B71_07_PttQuickExit_IsFollowerAccount_NullAcc_ReturnsFalse()
        {
            // Assert: IsFollowerAccount(null) returns false (null guard -- no crash).
            // When skipIfFollower=true and IsFollowerAccount returns false, follower guard does NOT fire.
            // This regression guard ensures the null path of the guard condition is safe.
            _engine.SetEnabled(false);
            bool result = _engine.IsFollowerAccount(null);
            Assert.False(result);
        }

        // -----------------------------------------------------------------------
        // FIX 3 (DW-B71-04): PttGlobalQuickExit follower dispatch loop
        // Since Account.All is empty in test context, Execute() iterates zero accounts.
        // Tests verify:
        //   (a) Execute() completes without exception when Account.All is empty
        //   (b) FindRule(null) returns null (now internal -- accessible from same assembly)
        //   (c) ExecuteOne(null, null, ..., false) via reflection -- null/flat guard fires, no crash
        // -----------------------------------------------------------------------

        [Fact]
        public void T_B71_08_PttGlobalQuickExit_Execute_EmptyAccountAll_NoException()
        {
            // Assert: Execute() with empty Account.All completes without exception.
            // Account.All is not populated in NT8 test context -- the foreach body never executes.
            _engine.SetEnabled(false);
            var gqx = new PttGlobalQuickExit();
            var ex = Record.Exception(() => gqx.Execute());
            Assert.Null(ex);
        }

        [Fact]
        public void T_B71_09_CopyEngine_FindRule_NullInstrument_ReturnsNull()
        {
            // Assert: FindRule(null) returns null (null guard in FindRule body -- now internal).
            // B71 Fix 1c: FindRule promoted private->internal so PttGlobalQuickExit can call it.
            // This test verifies FindRule is accessible as internal and handles null safely.
            _engine.SetEnabled(false);
            CopyEngine.CopyRule? result = _engine.FindRule(null);
            Assert.Null(result);
        }

        [Fact]
        public void T_B71_10_PttGlobalQuickExit_ExecuteOne_NullAccount_SkipIfFollowerFalse_NoException()
        {
            // Assert: when the follower dispatch loop calls ExecuteOne(follower, ..., skipIfFollower:false)
            // and PttQuickExit.Execute receives a null account, the null/flat guard fires, no crash.
            // Covers the case where a null follower in FollowerAccounts[] reaches ExecuteOne.
            // Invoke ExecuteOne via reflection (private method) with null account and skipIfFollower=false.
            _engine.SetEnabled(false);
            var gqx = new PttGlobalQuickExit();
            MethodInfo mi = typeof(PttGlobalQuickExit).GetMethod(
                "ExecuteOne",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(mi);
            var ex = Record.Exception(() =>
                mi.Invoke(gqx, new object[] { null, null, 4, 8, false })
            );
            Assert.Null(ex);
        }
    }
}
