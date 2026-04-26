using System;

namespace Morpheus.ControlPlane.MarketData
{
    public class RithmicSessionManager
    {
        public bool IsConnected { get; private set; }

        public void Connect()
        {
            IsConnected = true;
        }

        public void Disconnect()
        {
            IsConnected = false;
        }
    }
}
