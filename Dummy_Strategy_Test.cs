using System;
using NinjaTrader.Gui;
using NinjaTrader.NinjaScript;
using NinjaTrader.Data;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class Dummy_Strategy_Test : Strategy
    {
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "DummyStrategy";
            }
        }
    }
}

