using System;
using Xunit;
using FluentAssertions;
using Moq;
using Morpheus.ControlPlane.MarketData;

namespace Morpheus.ControlPlane.Tests
{
    public class MarketDataFeedNormalizerTests
    {
        [Fact]
        public void FeedNormalizerShouldMapRithmicInstrumentToCanonicalSymbol()
        {
            // Arrange
            var normalizer = new RithmicFeedNormalizer();

            // Act
            string canonical = normalizer.NormalizeSymbol("NQZ4", "CME");

            // Assert
            canonical.Should().Be("NQ");
        }

        [Fact]
        public void FeedNormalizerShouldThrowArgumentExceptionOnNullOrEmptySymbol()
        {
            var normalizer = new RithmicFeedNormalizer();

            Action act = () => normalizer.NormalizeSymbol("", "CME");

            act.Should().Throw<ArgumentException>();
        }
    }
}
