using System;
using System.Collections.Generic;
using System.Linq;

namespace V9_CopyTrading
{
    public class AccountPosition
    {
        public string AccountName { get; set; }
        public string Symbol { get; set; }
        public double Quantity { get; set; } // Positive = Long, Negative = Short
        public double AverageEntryPrice { get; set; }
        public double CurrentPrice { get; set; }
        public double UnrealizedPnL { get; set; }
        public double CommissionPaid { get; set; }

        public void UpdatePnL(double tickValue = 5.0) // Default for MES
        {
            if (Quantity == 0)
            {
                UnrealizedPnL = 0;
                return;
            }

            // Simple P&L calc: (Current - Entry) * Quantity * TickValue/TickSize
            // Assuming MES/MGC standard for now.
            UnrealizedPnL = (CurrentPrice - AverageEntryPrice) * Quantity * (tickValue / 0.25); 
        }
    }

    public class PositionTracker
    {
        private readonly Dictionary<string, List<AccountPosition>> _accountPositions = new();
        private readonly object _lock = new();

        public void UpdatePosition(string account, string symbol, double quantityChange, double price)
        {
            lock (_lock)
            {
                if (!_accountPositions.ContainsKey(account))
                    _accountPositions[account] = new List<AccountPosition>();

                var pos = _accountPositions[account].FirstOrDefault(p => p.Symbol == symbol);
                if (pos == null)
                {
                    pos = new AccountPosition { AccountName = account, Symbol = symbol, Quantity = 0 };
                    _accountPositions[account].Add(pos);
                }

                // Simple average price calculation for simplicity in this phase
                if (quantityChange > 0 && pos.Quantity >= 0) // Adding to long
                {
                    pos.AverageEntryPrice = ((pos.Quantity * pos.AverageEntryPrice) + (quantityChange * price)) / (pos.Quantity + quantityChange);
                }
                else if (quantityChange < 0 && pos.Quantity <= 0) // Adding to short
                {
                    pos.AverageEntryPrice = ((Math.Abs(pos.Quantity) * pos.AverageEntryPrice) + (Math.Abs(quantityChange) * price)) / (Math.Abs(pos.Quantity) + Math.Abs(quantityChange));
                }
                
                pos.Quantity += quantityChange;
                pos.CurrentPrice = price;
                pos.UpdatePnL();
            }
        }

        public double GetTotalPnL()
        {
            lock (_lock)
            {
                return _accountPositions.Values.SelectMany(v => v).Sum(p => p.UnrealizedPnL);
            }
        }

        public int GetProfitableAccountCount()
        {
            lock (_lock)
            {
                return _accountPositions.Keys.Count(acc => _accountPositions[acc].Sum(p => p.UnrealizedPnL) > 0);
            }
        }

        public void LogState()
        {
            lock (_lock)
            {
                Console.WriteLine($"--- Aggregate P&L: {GetTotalPnL():C2} ---");
                foreach (var acc in _accountPositions)
                {
                    double accPnL = acc.Value.Sum(p => p.UnrealizedPnL);
                    Console.WriteLine($"Account {acc.Key}: {accPnL:C2}");
                }
            }
        }
    }
}
