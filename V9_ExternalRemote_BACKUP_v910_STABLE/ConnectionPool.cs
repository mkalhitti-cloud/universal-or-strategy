using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Linq;

namespace V9_ExternalRemote
{
    public class ApexConnection
    {
        public string AccountId { get; set; } = string.Empty;
        public TcpClient? Client { get; set; }
        public bool IsConnected => Client?.Connected ?? false;
        public DateTime LastHeartbeat { get; set; }
        public int ReconnectAttempts { get; set; }
    }

    public class ConnectionPool
    {
        private readonly List<ApexConnection> _connections = new();
        private readonly System.Timers.Timer _heartbeatTimer;
        private readonly object _lock = new();
        private const int MaxConnections = 20;

        public event Action<string, string>? OnMessageReceived;

        public ConnectionPool()
        {
            _heartbeatTimer = new System.Timers.Timer(5000);
            _heartbeatTimer.Elapsed += SendHeartbeats;
            _heartbeatTimer.AutoReset = true;
            _heartbeatTimer.Start();
        }

        public void AddConnection(string accountId, TcpClient client)
        {
            lock (_lock)
            {
                if (_connections.Count >= MaxConnections)
                {
                    Console.WriteLine("Max connections reached.");
                    return;
                }

                var conn = new ApexConnection
                {
                    AccountId = accountId,
                    Client = client,
                    LastHeartbeat = DateTime.Now
                };
                _connections.Add(conn);
                
                Task.Run(() => ListenToConnection(conn));
            }
        }

        private async Task ListenToConnection(ApexConnection conn)
        {
            byte[] buffer = new byte[1024];
            while (conn.IsConnected)
            {
                try
                {
                    var stream = conn.Client.GetStream();
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    OnMessageReceived?.Invoke(conn.AccountId, message);
                }
                catch
                {
                    break;
                }
            }
            Console.WriteLine($"Connection lost for account: {conn.AccountId}");
        }

        private void SendHeartbeats(object sender, ElapsedEventArgs e)
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                foreach (var conn in _connections)
                {
                    if (conn.IsConnected)
                    {
                        // Check if we haven't received an ACK in 10 seconds
                        if ((now - conn.LastHeartbeat).TotalSeconds > 10)
                        {
                            Console.WriteLine($"Connection timeout for {conn.AccountId}. 10s since last activity.");
                            HandleDisconnect(conn);
                            continue;
                        }

                        try
                        {
                            byte[] data = Encoding.UTF8.GetBytes($"HEARTBEAT|{now.Ticks}\n");
                            conn.Client.GetStream().Write(data, 0, data.Length);
                        }
                        catch
                        {
                            HandleDisconnect(conn);
                        }
                    }
                }
            }
        }

        private void HandleAccountMessage(string accountId, string message)
        {
            lock (_lock)
            {
                var conn = _connections.FirstOrDefault(c => c.AccountId == accountId);
                if (conn != null)
                {
                    conn.LastHeartbeat = DateTime.Now; // Update on ANY activity
                    
                    if (message.StartsWith("ACK|"))
                    {
                        // Explicit ACK received
                    }
                }
            }
            OnMessageReceived?.Invoke(accountId, message);
        }

        private void HandleDisconnect(ApexConnection conn)
        {
            try
            {
                conn.Client?.Close();
            }
            catch { }
            // Reconnection logic would go here (Exponential backoff)
            Console.WriteLine($"Disconnected {conn.AccountId}. Ready for reconnect.");
        }

        public void Broadcast(string message)
        {
            lock (_lock)
            {
                byte[] data = Encoding.UTF8.GetBytes(message + "\n");
                foreach (var conn in _connections)
                {
                    if (conn.IsConnected)
                    {
                        try
                        {
                            conn.Client.GetStream().Write(data, 0, data.Length);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to send to {conn.AccountId}: {ex.Message}");
                        }
                    }
                }
            }
        }
    }
}
