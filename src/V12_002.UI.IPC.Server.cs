// Build 971: UI.IPC.Server -- StartIpcServer, ListenForRemote, HandleClient, ProcessClientStream, HandleIncomingIpcLine, StopIpcServer
// V12 UI.IPC Module (Extracted)
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;
using System.Net;
using System.Net.Sockets;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region IPC Server

        private void StartIpcServer()
        {
            if (isIpcRunning) return;

            try
            {
                StopIpcServer(); // Ensure clean start

                isIpcRunning = true;
                ipcCommandQueue = new ConcurrentQueue<string>();
                Interlocked.Exchange(ref ipcQueuedCommandCount, 0);

                // V12.2: Multi-Client Support
                // connectedClients = new ConcurrentDictionary<int, TcpClient>(); // Moved to class member declaration

                ipcThread = new Thread(ListenForRemote);
                ipcThread.IsBackground = true;
                ipcThread.Name = "V10_IPC_Server";
                ipcThread.Start();

                Print(string.Format("IPC SERVER SUCCESS: Listening on 127.0.0.1:{0} (Multi-Client)", IpcPort));
            }
            catch (Exception ex)
            {
                Print("ERROR StartIpcServer: " + ex.Message);
            }
        }

        private void ListenForRemote()
        {
            try
            {
                ipcListener = new TcpListener(IPAddress.Loopback, IpcPort);
                ipcListener.Start();

                while (isIpcRunning)
                {
                    if (!ipcListener.Pending())
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    // Accept new client
                    TcpClient client = ipcListener.AcceptTcpClient();
                    int clientId = Interlocked.Increment(ref _ipcClientIdSeed);
                    connectedClients[clientId] = client;
                    Print($"V12 IPC: New Client Connected [id={clientId}]");

                    // V12.13-D: Send REQUEST_FLEET_STATE directly to the newly connected client
                    // (Previously called SendToExternalApp which connected back to port 5001 = self, causing infinite flood loop)
                    try
                    {
                        byte[] reqBytes = Encoding.UTF8.GetBytes("REQUEST_FLEET_STATE|ALL\n");
                        NetworkStream ns = client.GetStream();
                        ns.Write(reqBytes, 0, reqBytes.Length);
                        ns.Flush();
                        Print("V12 IPC: Sent REQUEST_FLEET_STATE to new client");
                    }
                    catch (Exception ex)
                    {
                        Print("V12 IPC: Failed to send fleet state request: " + ex.Message);
                    }

                    // Handle client in a separate task
                    Task.Run(() => HandleClient(clientId, client));
                }
            }
            catch (Exception)
            {
                isIpcRunning = false;
                Print("[V12.2] IPC Listener Status: Stopped/Error");
            }
            finally
            {
                if (ipcListener != null)
                {
                    try { ipcListener.Stop(); } catch { }
                }
            }
        }

        private void HandleClient(int clientId, TcpClient client)
        {
            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    ProcessClientStream(clientId, client, stream);
                }
            }
            catch (Exception ex)
            {
                Print("V12 IPC Client Error: " + ex.Message);
            }
            finally
            {
                if (connectedClients != null)
                    connectedClients.TryRemove(clientId, out _);
                Print($"V12 IPC: Client Disconnected [id={clientId}]");
                try { client.Close(); } catch { }
            }
        }

        private void ProcessClientStream(int clientId, TcpClient client, NetworkStream stream)
        {
            StringBuilder lineBuffer = new StringBuilder();
            byte[] buffer = new byte[4096];
            Decoder utf8Decoder = new UTF8Encoding(false, true).GetDecoder();
            char[] charBuf = new char[4096];

            while (isIpcRunning && client.Connected)
            {
                if (!stream.DataAvailable)
                {
                    Thread.Sleep(50);
                    continue;
                }

                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string chunk;
                try
                {
                    int charCount = utf8Decoder.GetChars(buffer, 0, bytesRead, charBuf, 0, false);
                    chunk = new string(charBuf, 0, charCount);
                }
                catch (DecoderFallbackException)
                {
                    Interlocked.Increment(ref _ipcInvalidUtf8Count);
                    Print($"V12 IPC: Invalid UTF-8 payload from client {clientId}; disconnecting.");
                    break;
                }
                lineBuffer.Append(chunk);

                if (lineBuffer.Length > IpcMaxBufferedChars)
                {
                    Print($"V12 IPC: Client {clientId} exceeded max buffered payload ({IpcMaxBufferedChars}); disconnecting.");
                    break;
                }

                string accumulated = lineBuffer.ToString();
                int lastNewline = accumulated.LastIndexOf('\n');
                if (lastNewline < 0) continue;

                string completeLines = accumulated.Substring(0, lastNewline);
                lineBuffer.Clear();
                if (lastNewline + 1 < accumulated.Length)
                {
                    lineBuffer.Append(accumulated.Substring(lastNewline + 1));
                    if (lineBuffer.Length > IpcMaxBufferedChars)
                    {
                        Print($"V12 IPC: Client {clientId} residue exceeded max buffered payload ({IpcMaxBufferedChars}); disconnecting.");
                        break;
                    }
                }

                string[] lines = completeLines.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    HandleIncomingIpcLine(clientId, stream, line);
                }
            }
        }

        private void HandleIncomingIpcLine(int clientId, NetworkStream stream, string line)
        {
            string message = line.Trim();
            if (string.IsNullOrEmpty(message)) return;

            // Handle GET_LAYOUT (client-scoped response via actor snapshot)
            if (message.StartsWith("GET_LAYOUT", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    TriggerCustomEvent(o => Enqueue(ctx => ctx.SendLayoutResponseToClient(clientId, stream)), null);
                }
                catch (Exception ex)
                {
                    Print(string.Format("V12 IPC REJECT [client={0}] GET_LAYOUT scheduling failed: {1}", clientId, ex.Message));
                }
                return;
            }

            // Enqueue for processing
            if (!TryEnqueueIpcCommand(message, out string enqueueReason))
            {
                Print(string.Format("V12 IPC REJECT [client={0}] {1}: {2}", clientId, message, enqueueReason));
                return;
            }
            Print(string.Format("V12.1 IPC ENQUEUE [client={0}] {1}", clientId, message));

            // Trigger processing
            try
            {
                TriggerCustomEvent(o => ProcessIpcCommands(), null);
            }
            catch { }
        }

        private void SendLayoutResponseToClient(int clientId, NetworkStream stream)
        {
            string snapMode = isRMAModeActive ? "RMA" : "OR";
            double snapStop = isRMAModeActive ? RMAStopATRMultiplier : StopMultiplier;
            int snapCount = activeTargetCount;
            double snapT1 = Target1Value;
            double snapT2 = Target2Value;
            double snapT3 = Target3Value;
            double snapT4 = Target4Value;
            double snapT5 = Target5Value;
            TargetMode snapT1Type = T1Type;
            TargetMode snapT2Type = T2Type;
            TargetMode snapT3Type = T3Type;
            TargetMode snapT4Type = T4Type;
            TargetMode snapT5Type = T5Type;
            string snapCit = ChaseIfTouchPoints ?? "0";
            bool snapTrma = isTrendRmaMode;
            bool snapRrma = isRetestRmaMode;

            string configResponse = string.Format(
                "CONFIG|{0}|COUNT:{1};T1:{2};T1TYPE:{3};T2:{4};T2TYPE:{5};T3:{6};T3TYPE:{7};T4:{8};T4TYPE:{9};T5:{10};T5TYPE:{11};STR:{12};STRTYPE:ATR;MAX:{13};CIT:{14};OT:Limit;TRMA:{15};RRMA:{16};\n",
                snapMode, snapCount, snapT1, ToIpcTargetMode(snapT1Type),
                snapT2, ToIpcTargetMode(snapT2Type),
                snapT3, ToIpcTargetMode(snapT3Type),
                snapT4, ToIpcTargetMode(snapT4Type),
                snapT5, ToIpcTargetMode(snapT5Type),
                snapStop, MaxRiskAmount, snapCit,
                snapTrma ? "1" : "0", snapRrma ? "1" : "0");

            SendIpcResponseToClient(clientId, stream, configResponse);
        }

        private void SendIpcResponseToClient(int clientId, NetworkStream stream, string response)
        {
            if (stream == null || string.IsNullOrEmpty(response)) return;

            try
            {
                if (!stream.CanWrite) return;

                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                stream.Write(responseBytes, 0, responseBytes.Length);
                stream.Flush();
            }
            catch (Exception ex)
            {
                Print(string.Format("V12 IPC: Failed to send client response [client={0}]: {1}", clientId, ex.Message));
                if (connectedClients != null && connectedClients.TryRemove(clientId, out var staleClient))
                {
                    try { staleClient.Close(); } catch { }
                }
            }
        }

        private void StopIpcServer()
        {
            try
            {
                isIpcRunning = false;
                if (ipcListener != null)
                {
                    ipcListener.Stop();
                    ipcListener = null;
                }
                if (ipcThread != null && ipcThread.IsAlive)
                {
                    ipcThread.Join(500);
                }

                if (connectedClients != null)
                {
                    foreach (var kvp in connectedClients.ToArray())
                    {
                        try { kvp.Value.Close(); } catch { }
                    }
                    connectedClients.Clear();
                }
                Interlocked.Exchange(ref ipcQueuedCommandCount, 0);
            }
            catch { }
        }


        #endregion
    }
}
