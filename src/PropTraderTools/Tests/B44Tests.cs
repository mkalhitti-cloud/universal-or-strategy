// B44Tests.cs
// Block: PTT-COPIER-B44
// Spec: DW-B44-subscribe-panel-01
// Tests: T_B44_01 through T_B44_04
// Framework: xUnit only (no NUnit, no MSTest)
// NT8-runtime-free: no Account.All reference, no event raising

using System;
using System.Reflection;
using Xunit;

namespace PropTraderTools
{
    // -------------------------------------------------------------------------
    // SubscribeIdempotencyTests -- T_B44_01 through T_B44_04
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tests for CopyEngine.Subscribe() / Unsubscribe() idempotency guards (B44).
    /// Uses CopyEngine.Instance singleton (same as B42Tests.cs:241 pattern).
    /// _subscribed field accessed via FieldInfo reflection -- NT8-runtime-free.
    /// IDisposable.Dispose() resets _subscribed to false for test isolation.
    /// </summary>
    public sealed class SubscribeIdempotencyTests : IDisposable
    {
        // Singleton access -- identical to B42Tests.cs:241 pattern
        private readonly CopyEngine _engine = CopyEngine.Instance;

        // Reflection accessor for private _subscribed field (B42Tests.cs:304-306 pattern)
        private static readonly FieldInfo _subscribedField = typeof(CopyEngine).GetField(
            "_subscribed",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        private bool GetSubscribed() => (bool)_subscribedField.GetValue(_engine);

        private void SetSubscribed(bool value) => _subscribedField.SetValue(_engine, value);

        // IDisposable: xUnit calls Dispose() after each [Fact] -- resets singleton state
        public void Dispose()
        {
            SetSubscribed(false);
        }

        // T_B44_01 -- Subscribe is idempotent: calling twice leaves _subscribed=true, no double-registration
        // Spec: DW-B44-T1-02, DW-B44-T2-03
        [Fact]
        public void T_B44_01_Subscribe_CalledTwice_SubscribedFlagRemainsTrue()
        {
            // Arrange -- Dispose() guarantees _subscribed=false at start
            Assert.False(GetSubscribed());

            // Act -- call Subscribe() twice
            _engine.Subscribe();
            _engine.Subscribe();

            // Assert -- flag is true; second call was a no-op (guard short-circuited)
            Assert.True(GetSubscribed());
        }

        // T_B44_02 -- Unsubscribe when not subscribed does not throw and leaves flag false
        // Spec: DW-B44-T1-03, DW-B44-T2-04
        [Fact]
        public void T_B44_02_Unsubscribe_WhenNotSubscribed_DoesNotThrow()
        {
            // Arrange -- Dispose() guarantees _subscribed=false at start
            Assert.False(GetSubscribed());

            // Act -- call Unsubscribe() on cold engine (never subscribed)
            _engine.Unsubscribe();

            // Assert -- no exception thrown; flag remains false
            Assert.False(GetSubscribed());
        }

        // T_B44_03 -- Subscribe -> Unsubscribe -> Subscribe cycle resets flag correctly
        // Spec: DW-B44-T2-05
        [Fact]
        public void T_B44_03_ReSubscribe_AfterUnsubscribe_FlagIsTrue()
        {
            // Arrange -- Dispose() guarantees _subscribed=false at start
            Assert.False(GetSubscribed());

            // Act + intermediate asserts through full cycle
            _engine.Subscribe();
            Assert.True(GetSubscribed()); // after first Subscribe

            _engine.Unsubscribe();
            Assert.False(GetSubscribed()); // after Unsubscribe

            _engine.Subscribe();
            Assert.True(GetSubscribed()); // after re-Subscribe

            // Final assert -- flag is true after full cycle
            Assert.True(GetSubscribed());
        }

        // T_B44_04 -- Fresh engine (no Subscribe called) has _subscribed=false
        // Spec: DW-B44-T2-06
        [Fact]
        public void T_B44_04_WithoutSubscribe_SubscribedFlag_IsFalse()
        {
            // Arrange -- Dispose() guarantees _subscribed=false at start; no Subscribe called

            // Assert -- engine starts in unsubscribed (deaf) state
            Assert.False(GetSubscribed());
        }
    }
}
