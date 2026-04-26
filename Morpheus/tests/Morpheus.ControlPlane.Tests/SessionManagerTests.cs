using System;
using Xunit;
using FluentAssertions;
using Morpheus.ControlPlane.MarketData;

namespace Morpheus.ControlPlane.Tests
{
    public class SessionManagerTests
    {
        [Fact]
        public void SessionManagerShouldInitializeDisconnected()
        {
            var manager = new RithmicSessionManager();
            manager.IsConnected.Should().BeFalse();
        }

        [Fact]
        public void SessionManagerShouldTransitionToConnected()
        {
            var manager = new RithmicSessionManager();
            manager.Connect();
            manager.IsConnected.Should().BeTrue();
        }

        [Fact]
        public void SessionManagerShouldTransitionToDisconnected()
        {
            var manager = new RithmicSessionManager();
            manager.Connect();
            manager.Disconnect();
            manager.IsConnected.Should().BeFalse();
        }
    }
}
