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
        public void FeedNormalizerShouldMapESInstrumentToCanonicalSymbol()
        {
            var normalizer = new RithmicFeedNormalizer();
            string canonical = normalizer.NormalizeSymbol("ESM5", "CME");
            canonical.Should().Be("ES");
        }

        [Fact]
        public void FeedNormalizerShouldReturnOriginalSymbolIfNoMappingMatches()
        {
            var normalizer = new RithmicFeedNormalizer();
            string canonical = normalizer.NormalizeSymbol("GCZ4", "COMEX");
            canonical.Should().Be("GCZ4");
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
