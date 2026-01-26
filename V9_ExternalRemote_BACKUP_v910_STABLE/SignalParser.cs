using System;

namespace V9_ExternalRemote
{
    public enum TradingAction
    {
        LONG,
        SHORT,
        CLOSE,
        MODIFY,
        UNKNOWN
    }

    public class Signal
    {
        public TradingAction Action { get; set; }
        public string Symbol { get; set; }
        public double Quantity { get; set; }
        public string Raw { get; set; }
        public bool IsValid { get; set; }
    }

    public static class SignalParser
    {
        /// <summary>
        /// Parses a signal string in the format "ACTION|SYMBOL|QUANTITY"
        /// Example: "LONG|MES|1"
        /// </summary>
        public static Signal Parse(string rawSignal)
        {
            var signal = new Signal { Raw = rawSignal, IsValid = false };

            if (string.IsNullOrWhiteSpace(rawSignal))
                return signal;

            try
            {
                string[] parts = rawSignal.Split('|');
                if (parts.Length != 3)
                    return signal;

                if (Enum.TryParse(parts[0], true, out TradingAction action))
                {
                    signal.Action = action;
                }
                else
                {
                    signal.Action = TradingAction.UNKNOWN;
                    return signal;
                }

                signal.Symbol = parts[1].ToUpper();

                if (double.TryParse(parts[2], out double quantity))
                {
                    signal.Quantity = quantity;
                    signal.IsValid = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing signal '{rawSignal}': {ex.Message}");
            }

            return signal;
        }
    }
}
