using System.Collections.Generic;

namespace V9_ExternalControl.Models
{
    public class V9CopyTradingStatus
    {
        public int AccountsConnected { get; set; }
        public bool IsActive { get; set; }
        public decimal AggregatePnL { get; set; }
        public bool Heartbeat { get; set; }
        public int ErrorCount { get; set; }
        public List<string> AccountsList { get; set; } = new List<string>();
        public List<CopyAccountInfo> TopAccounts { get; set; } = new List<CopyAccountInfo>();
    }

    public class CopyAccountInfo
    {
        public string AccountName { get; set; } = string.Empty;
        public decimal PnL { get; set; }
        public bool IsConnected { get; set; }
    }
}
