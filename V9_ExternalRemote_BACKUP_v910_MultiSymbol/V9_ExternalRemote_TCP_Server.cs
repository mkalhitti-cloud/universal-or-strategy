using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace V9_ExternalRemote
{
    public class V9_ExternalRemote_TCP_Server
    {
        private TcpListener _listener;
        private readonly ConnectionPool _connectionPool;
        private readonly PositionTracker _positionTracker;
        private bool _isRunning;
        private readonly int _port;
        private readonly System.Timers.Timer _syncTimer;
        private readonly Dictionary<string, double> _targetPositions = new Dictionary<string, double>();
        private readonly object _stateLock = new object();

        public V9_ExternalRemote_TCP_Server(int port = 5000)
        {
            _port = port;
            _connectionPool = new ConnectionPool();
            _positionTracker = new PositionTracker();
            _connectionPool.OnMessageReceived += HandleAccountMessage;

            // Start Sync Timer (Broadcasting state every 5 seconds)
            _syncTimer = new System.Timers.Timer(5000);
            _syncTimer.Elapsed += BroadcastSyncState;
            _syncTimer.AutoReset = true;
        }

        public void Start()
        {
            _isRunning = true;
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            Console.WriteLine($"V9 TCP Server started on port {_port}");
            
            _syncTimer.Start();

            Task.Run(AcceptConnections);
        }

        private void BroadcastSyncState(object sender, System.Timers.ElapsedEventArgs e)
        {
            lock (_stateLock)
            {
                foreach (var kvp in _targetPositions)
                {
                    // Protocol: SYNC_TARGET|SYMBOL|NET_QTY
                    // Example: SYNC_TARGET|MES|1
                    string syncMsg = $"SYNC_TARGET|{kvp.Key}|{kvp.Value}";
                    _connectionPool.Broadcast(syncMsg);
                }
            }
        }

        private async Task AcceptConnections()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    string accountId = $"APEX_{Guid.NewGuid().ToString().Substring(0, 8)}"; // Temporary ID assignment
                    Console.WriteLine($"New connection accepted: {accountId}");
                    _connectionPool.AddConnection(accountId, client);
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                        Console.WriteLine($"Error accepting connection: {ex.Message}");
                }
            }
        }

        public void ProcessSignal(string rawSignal)
        {
            var signal = SignalParser.Parse(rawSignal);
            if (!signal.IsValid)
            {
                Console.WriteLine($"Invalid signal received: {rawSignal}");
                return;
            }

            Console.WriteLine($"Processing Signal: {signal.Action} {signal.Symbol} x {signal.Quantity}");
            
            // Update Target State
            UpdateTargetState(signal);

            // Broadcast to all accounts
            _connectionPool.Broadcast(rawSignal);

            // Update position tracking (assumes price is handled separately or included in signal)
            // For now, using mock price for tracking
            double mockPrice = signal.Symbol == "MES" ? 5000 : 2000;
        }

        private void UpdateTargetState(Signal signal)
        {
            lock (_stateLock)
            {
                if (!_targetPositions.ContainsKey(signal.Symbol))
                    _targetPositions[signal.Symbol] = 0;

                switch (signal.Action)
                {
                    case TradingAction.LONG:
                        _targetPositions[signal.Symbol] += signal.Quantity;
                        break;
                    case TradingAction.SHORT:
                        _targetPositions[signal.Symbol] -= signal.Quantity;
                        break;
                    case TradingAction.CLOSE:
                        // Close usually means "Close THIS amount" not "Go Flat".
                        // Logic: If Net is Long, subtract. If Net is Short, add.
                        // However, standard "CLOSE" signal usually means "Reduce position".
                        // To be precise:
                        // If we are Long, we Sell to Close.
                        // If we are Short, we Buy to Close.
                        // For simplicity in this logic, assuming CLOSE reduces the absolute magnitude towards zero.
                        
                        double current = _targetPositions[signal.Symbol];
                        if (current > 0)
                            _targetPositions[signal.Symbol] = Math.Max(0, current - signal.Quantity);
                        else if (current < 0)
                            _targetPositions[signal.Symbol] = Math.Min(0, current + signal.Quantity);
                        break;
                    case TradingAction.MODIFY:
                        // "MODIFY|MES|0.5" -> Set absolute size? Or delta?
                        // The user spec said "Reduce MES to 0.5 contracts". 
                        // It implies "Set Absolute Quantity". 
                        // Let's assume MODIFY sets the absolute target quantity while keeping direction?
                        // Or maybe MODIFY is handled as a delta elsewhere?
                        // For SAFETY: Let's assume MODIFY sets the absolute target quantity for now to support the "Reduce to" language.
                        // BUT, to keep direction, we need to know direction.
                        // Actually, looking at Phase 1 spec: "MODIFY|MES|0.5" (Reduce MES to 0.5 contracts).
                        // This implies the Net Position should become 0.5 (Long) or -0.5 (Short).
                        // Let's defer MODIFY logic for strict Sync, or assume it matches current sign.
                        
                        if (_targetPositions[signal.Symbol] > 0)
                            _targetPositions[signal.Symbol] = Math.Abs(signal.Quantity);
                        else if (_targetPositions[signal.Symbol] < 0)
                            _targetPositions[signal.Symbol] = -Math.Abs(signal.Quantity);
                        break;
                }
                Console.WriteLine($"Updated Target State for {signal.Symbol}: {_targetPositions[signal.Symbol]}");
            }
        }
            
            // Note: In real implementation, we'd wait for execution confirmation from accounts
            // before updating position tracker logic, but for signal broadcast tracking:
            // _positionTracker.UpdatePosition("AGGREGATE", signal.Symbol, signal.Quantity, mockPrice);
        // Removed the extra closing brace here

        private void HandleAccountMessage(string accountId, string message)
        {
            Console.WriteLine($"Message from {accountId}: {message}");
            
            if (message.StartsWith("ACK|"))
            {
                // Handle heartbeat acknowledge
            }
            else if (message.Contains("|"))
            {
                // Likely execution report: "FILL|SYMBOL|QTY|PRICE"
                string[] parts = message.Split('|');
                if (parts[0] == "FILL" && parts.Length == 4)
                {
                    string symbol = parts[1];
                    if (double.TryParse(parts[2], out double qty) && double.TryParse(parts[3], out double price))
                    {
                        _positionTracker.UpdatePosition(accountId, symbol, qty, price);
                        _positionTracker.LogState();
                    }
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
        }
    }
}
