using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace V9_ExternalRemote
{
    [ComImport, Guid("A43788C1-D91B-11D3-8F39-00C04F365157"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IRtdUpdateEvent
    {
        void UpdateNotify();
        int HeartbeatInterval { get; set; }
        void Disconnect();
    }

    /// <summary>
    /// Specialized client for ThinkorSwim RTD (tos.rtd) using DYNAMIC binding to avoid GUID issues.
    /// </summary>
    public class TosRtdClient
    {
        private dynamic _rtdServer; // Use dynamic to bypass strict Interface casting
        private Dispatcher _dispatcher;
        private bool _isConnected;
        private Dictionary<int, string> _topicMap = new Dictionary<int, string>();
        private int _nextTopicId = 1;

        public event Action<string, object> OnDataUpdate;
        public event Action<bool> OnConnectionStatusChanged;

        public TosRtdClient(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            Log("Client initialized (Dynamic + Polling).");
        }

        public bool IsConnected => _isConnected;

        public void Start()
        {
            Log("Starting connection...");
            try
            {
                Type rtdType = Type.GetTypeFromProgID("tos.rtd");
                if (rtdType == null)
                {
                    Log("Error: tos.rtd ProgID not found.");
                    OnConnectionStatusChanged?.Invoke(false);
                    return;
                }

                _rtdServer = Activator.CreateInstance(rtdType);
                // Pass our specialized callback object
                _rtdServer.ServerStart(new RtdUpdateEvent(this));
                
                _isConnected = true;
                Log("Connected to RTD Server.");
                OnConnectionStatusChanged?.Invoke(true);

                // Polling Fallback: Force RefreshData every 2 seconds if callbacks fail
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(2);
                timer.Tick += (s, e) => {
                    if (_isConnected) TriggerUpdate(); 
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                Log("Start Error: " + ex.ToString());
                _isConnected = false;
                OnConnectionStatusChanged?.Invoke(false);
            }
        }

        public int Subscribe(string key, object[] topics)
        {
            try
            {
                if (!_isConnected || _rtdServer == null) return -1;

                int topicId = _nextTopicId++;
                string flatTopics = string.Join(", ", topics);
                Log($"Subscribing ID {topicId}: [{flatTopics}] -> Key: {key}");
                
                bool getNewValues = true;
                // MUST pass by ref for COM interop to work correctly with dynamic/IDispatch
                _rtdServer.ConnectData(topicId, ref topics, ref getNewValues);
                
                _topicMap[topicId] = key;
                return topicId;
            }
            catch (Exception ex)
            {
                Log($"Subscribe Error: " + ex.Message);
                return -1;
            }
        }

        public int Subscribe(string symbol, string field, string study = "")
        {
            // Legacy wrapper - redirects to raw
            object[] topics;
            if (!string.IsNullOrEmpty(study))
                topics = new object[] { symbol, study, field };
            else
                topics = new object[] { field, symbol };
            
            return Subscribe($"{symbol}:{field}", topics);
        }

        public void UnsubscribeAll()
        {
            Log("Unsubscribing all topics...");
            try
            {
                if (_rtdServer == null) return;
                foreach (var topic in _topicMap.Keys)
                {
                    try { _rtdServer.DisconnectData(topic); } catch { }
                }
                _topicMap.Clear();
                _nextTopicId = 1; // Reset topic IDs to prevent stale data overlap
            }
            catch (Exception ex)
            {
                Log("UnsubscribeAll Error: " + ex.Message);
            }
        }

        // Keep nested for access to private _client, but simpler
        [ComVisible(true)]
        public class RtdUpdateEvent : IRtdUpdateEvent
        {
            private TosRtdClient _client;
            public int HeartbeatInterval { get; set; } = 1000;
            public RtdUpdateEvent(TosRtdClient client) { _client = client; }
            public void UpdateNotify() { _client.TriggerUpdate(); }
            public void Disconnect() { }
        }

        public void TriggerUpdate()
        {
            _dispatcher.BeginInvoke(new Action(ProcessUpdates));
        }

        private void ProcessUpdates()
        {
            try
            {
                if (_rtdServer == null) return;

                int topicCount = 0;
                var data = _rtdServer.RefreshData(ref topicCount);

                if (topicCount > 0 && data != null)
                {
                    Log($"Processing {topicCount} updates..."); 
                    
                    // Handle multi-dimensional array from RTD
                    if (data is object[,] multiData)
                    {
                        for (int i = 0; i < topicCount; i++)
                        {
                            try 
                            {
                                object rawId = multiData[0, i];
                                if (rawId == null) continue;
                                int topicId = Convert.ToInt32(rawId);
                                object value = multiData[1, i];

                                string valueStr = value?.ToString() ?? "null";
                                Log($"Update ID {topicId} = {valueStr}");
                                
                                // SHOTGUN TEST HELPER: Log successful data (not N/A) to file
                                if (valueStr != "N/A" && valueStr != "null" && valueStr != "#N/A" && _topicMap.ContainsKey(topicId))
                                {
                                    string key = _topicMap[topicId];
                                    // Log every successful value for debugging
                                    Log($"RECV: {key} = {valueStr}");
                                }

                                if (_topicMap.ContainsKey(topicId))
                                    OnDataUpdate?.Invoke(_topicMap[topicId], value);
                            }
                            catch (Exception ex) { Log("Item Error 1: " + ex.Message); }
                        }
                    }
                    // Handle 1D array if returned differently (fallback)
                    else if (data is Array arr && arr.Rank == 2)
                    {
                         for (int i = 0; i < topicCount; i++)
                         {
                             try
                             {
                                 object rawId = arr.GetValue(0, i);
                                 if (rawId == null) continue;
                                 int topicId = Convert.ToInt32(rawId);
                                 object value = arr.GetValue(1, i);

                                 string valueStr = value?.ToString() ?? "null";
                                 Log($"Update ID {topicId} = {valueStr}");
                                 
                                 // SHOTGUN TEST HELPER: Log successful data (not N/A) to file
                                 if (valueStr != "N/A" && valueStr != "null" && valueStr != "#N/A" && _topicMap.ContainsKey(topicId))
                                 {
                                     string key = _topicMap[topicId];
                                     System.IO.File.AppendAllText("v9_shotgun_results.txt",
                                         $"✓ SUCCESS: {key} = {valueStr}\r\n");
                                 }

                                 if (_topicMap.ContainsKey(topicId))
                                    OnDataUpdate?.Invoke(_topicMap[topicId], value);
                             }
                             catch (Exception ex) { Log("Item Error 2: " + ex.Message); }
                         }
                    }
                    else
                    {
                        Log($"Unknown Data Type: {data.GetType().Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Refresh Error: " + ex.Message);
            }
        }

        public void Disconnect()
        {
            Log("Disconnecting...");
            _isConnected = false;
            OnConnectionStatusChanged?.Invoke(false);
        }

        public void Stop()
        {
            Log("Stopping server...");
            try 
            {
                UnsubscribeAll();
                _rtdServer?.ServerTerminate();
            } 
            catch {}
            _rtdServer = null;
            _isConnected = false;
        }

        public void Heartbeat()
        {
            try
            {
                if (_rtdServer != null) 
                {
                    int val = _rtdServer.Heartbeat();
                    Log("Heartbeat: " + val);
                }
            }
            catch (Exception ex) { Log("Heartbeat Error: " + ex.Message); }
        }

        public void Log(string msg)
        {
            try
            {
                System.IO.File.AppendAllText("v9_remote_log.txt", $"{DateTime.Now:HH:mm:ss.fff} | {msg}\r\n");
            }
            catch {}
        }
    }
}
