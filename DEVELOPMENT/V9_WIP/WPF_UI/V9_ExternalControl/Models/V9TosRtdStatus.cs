using System;

namespace V9_ExternalControl.Models
{
    public class V9TosRtdStatus
    {
        public bool IsConnected { get; set; }
        public double Ema9 { get; set; }
        public double Ema15 { get; set; }
        public double LastPrice { get; set; }
        public double OrHigh { get; set; }
        public double OrLow { get; set; }
        public DateTime LastUpdate { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
