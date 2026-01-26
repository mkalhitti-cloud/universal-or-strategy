using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using V9_ExternalControl.Models;

namespace V9_ExternalControl.Services
{
    public class StatusFileReader
    {
        // Define paths relative to repo root assuming the executable runs from bin/Debug/net8.0-windows/
        // Ideally these should be absolute or passed in. 
        // User requested hardcoding to .agent/SHARED_CONTEXT
        // We will try to find the repo root by walking up from the executable.
        
        private string _tosStatusPath;
        private string _copyStatusPath;

        public StatusFileReader(string repoRoot)
        {
             _tosStatusPath = Path.Combine(repoRoot, ".agent", "SHARED_CONTEXT", "V9_TOS_RTD_STATUS.json");
             _copyStatusPath = Path.Combine(repoRoot, ".agent", "SHARED_CONTEXT", "V9_COPY_TRADING_STATUS.json");
        }

        public async Task<V9TosRtdStatus> ReadTosRtdStatusAsync()
        {
            try
            {
                if (!File.Exists(_tosStatusPath)) return null;

                string json = await File.ReadAllTextAsync(_tosStatusPath);
                return JsonConvert.DeserializeObject<V9TosRtdStatus>(json);
            }
            catch (Exception)
            {
                // Log error if needed, for now return null to indicate failure
                return null;
            }
        }

        public async Task<V9CopyTradingStatus> ReadCopyTradingStatusAsync()
        {
            try
            {
                if (!File.Exists(_copyStatusPath)) return null;

                string json = await File.ReadAllTextAsync(_copyStatusPath);
                return JsonConvert.DeserializeObject<V9CopyTradingStatus>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
