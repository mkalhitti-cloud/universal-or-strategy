namespace Morpheus.MarketData.Rithmic
{
    public class CanonicalSymbol
    {
        public string Base { get; }
        public string Month { get; }
        public int Year { get; }

        private CanonicalSymbol(string baseSymbol, string month, int year)
        {
            Base = baseSymbol;
            Month = month;
            Year = year;
        }

        public static CanonicalSymbol Parse(string symbol)
        {
            return string.IsNullOrWhiteSpace(symbol) || symbol.Length < 4
                ? throw new ArgumentException("Invalid symbol format", nameof(symbol))
                : new CanonicalSymbol(
                symbol[..2],
                symbol.Substring(2, 1),
                2020 + int.Parse(symbol.Substring(3, 1))
            );
        }

        public override string ToString()
        {
            return $"{Base}{Month}{Year % 10}";
        }
    }
}
