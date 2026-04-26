using Morpheus.MarketData.Rithmic;

namespace Morpheus.Tests
{
    public class CanonicalSymbolTests
    {
        [Fact]
        public void ParseValidSymbolShouldReturnCanonicalSymbol()
        {
            CanonicalSymbol symbol = CanonicalSymbol.Parse("NQZ4");
            Assert.Equal("NQ", symbol.Base);
            Assert.Equal("Z", symbol.Month);
            Assert.Equal(2024, symbol.Year);
            Assert.Equal("NQZ4", symbol.ToString());
        }
    }
}
