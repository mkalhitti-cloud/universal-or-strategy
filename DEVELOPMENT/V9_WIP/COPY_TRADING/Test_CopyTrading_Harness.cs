using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using V9_CopyTrading;
using System.Collections.Generic;

namespace V9_CopyTrading_Tests
{
    public class Test_CopyTrading_Harness
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== Starting V9 Copy Trading Test Harness (Phase 3 Sync) ===");

            // 1. Start Server
            var server = new V9_ExternalRemote_TCP_Server(5000);
            server.Start();
            await Task.Delay(1000);

            // 2. Simulate Client Connection
            var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", 5000);
            Console.WriteLine("Mock Client connected.");
            
            // Start listening in background
            var cts = new CancellationTokenSource();
            _ = ListenAsMockClient(client, "CLIENT", cts.Token);

            await Task.Delay(1000);

            // 3. Send Signal to create Target State: LONG 2
            Console.WriteLine("\n--- Broadcasting Signal: LONG MES 2 ---");
            server.ProcessSignal("LONG|MES|2");

            // Wait for broadcasting and processing
            await Task.Delay(2000);

            // 4. Verify Client received Signal
            // (Output captured in console)

            // 5. Simulate Client Missed Signal / Reconnected
            // The Server should be sending SYNC_TARGET every 5 seconds.
            // We wait for the next Sync message.
            Console.WriteLine("\n--- Waiting for SYNC_TARGET (approx 5s) ---");
            
            // Wait enough time for the 5s timer to trigger
            await Task.Delay(6000); 

            // 6. Cleanup
            cts.Cancel();
            server.Stop();
            client.Close();
            
            Console.WriteLine("\n=== Test Harness Complete ===");
        }

        private static async Task ListenAsMockClient(TcpClient client, string name, CancellationToken token)
        {
            byte[] buffer = new byte[1024];
            while (client.Connected && !token.IsCancellationRequested)
            {
                try
                {
                    if (client.Available > 0)
                    {
                        var stream = client.GetStream();
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                        if (bytesRead == 0) break;
                        string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        
                        string[] lines = msg.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            Console.WriteLine($"[{name}] Received: {line}");

                            if (line.StartsWith("HEARTBEAT|"))
                            {
                                byte[] ack = Encoding.UTF8.GetBytes($"ACK|{DateTime.Now.Ticks}\n");
                                client.GetStream().Write(ack, 0, ack.Length);
                            }
                            else if (line.StartsWith("SYNC_TARGET|"))
                            {
                                Console.WriteLine($"[{name}] >>> SYNC EVENT DETECTED: {line}");
                            }
                        }
                    }
                    await Task.Delay(10);
                }
                catch { break; }
            }
        }
    }
}
