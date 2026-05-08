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

        private string GetCurrentConfigMode()
        {
            if (isRMAModeActive) return "RMA";
            if (isTRENDModeActive) return "TREND";
            if (isRetestModeActive) return "RETEST";
            if (isMOMOModeActive) return "MOMO";
            if (isFFMAModeArmed) return "FFMA";
            return "OR";
        }

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
                ListenForRemote_StartLoopback();

                while (isIpcRunning)
                {
                    if (!ipcListener.Pending())
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    IpcClientSession session = ListenForRemote_AcceptClient();
                    ListenForRemote_SendInitialState(session);

                    // Handle client in a separate task
                    Task.Run(() => HandleClient(session));
                }
            }
            catch (Exception)
            {
                isIpcRunning = false;
                Print("[V12.2] IPC Listener Status: Stopped/Error");
            }
            finally
            {
                ListenForRemote_StopListener();
            }
        }

        private void ListenForRemote_StartLoopback()
        {
            ipcListener = new TcpListener(IPAddress.Loopback, IpcPort);
            ipcListener.Start();
        }

        private IpcClientSession ListenForRemote_AcceptClient()
        {
            // Accept new client
            TcpClient client = ipcListener.AcceptTcpClient();
            int clientId = Interlocked.Increment(ref _ipcClientIdSeed);
            IpcClientSession session = new IpcClientSession(clientId, client);
            connectedClients[clientId] = session;
            Print($"V12 IPC: New Client Connected [id={clientId}]");
            return session;
        }

        private void ListenForRemote_SendInitialState(IpcClientSession session)
        {
            // V12.13-D: Send REQUEST_FLEET_STATE directly to the newly connected client
            // (Previously called SendToExternalApp which connected back to port 5001 = self, causing infinite flood loop)
            try
            {
                byte[] reqBytes = Encoding.UTF8.GetBytes("REQUEST_FLEET_STATE|ALL\n");
                NetworkStream ns = session.Client.GetStream();
                ns.Write(reqBytes, 0, reqBytes.Length);
                ns.Flush();
                Print("V12 IPC: Sent REQUEST_FLEET_STATE to new client");
                if (!string.IsNullOrEmpty(_stickyLeaderAccount))
                {
                    byte[] leaderBytes = Encoding.UTF8.GetBytes("SET_LEADER_ACCOUNT|" + _stickyLeaderAccount + "\n");
                    ns.Write(leaderBytes, 0, leaderBytes.Length);
                    ns.Flush();
                    Print("V12 IPC: Sent leader sync to new client");
                }
            }
            catch (Exception ex)
            {
                Print("V12 IPC: Failed to send fleet state request: " + ex.Message);
            }
        }

        private void ListenForRemote_StopListener()
        {
            if (ipcListener != null)
            {
                try { ipcListener.Stop(); } catch { }
            }
        }

        private void HandleClient(IpcClientSession session)
        {
            try
            {
                using (NetworkStream stream = session.Stream)
                {
                    ProcessClientStream(session);
                }
            }
            catch (Exception ex)
            {
                Print("V12 IPC Client Error: " + ex.Message);
            }
            finally
            {
                if (connectedClients != null)
                    connectedClients.TryRemove(session.ClientId, out _);
                Print($"V12 IPC: Client Disconnected [id={session.ClientId}]");
                try { session.Client.Close(); } catch { }
            }
        }

        private void ProcessClientStream(IpcClientSession session)
        {
            int clientId = session.ClientId;
            TcpClient client = session.Client;
            NetworkStream stream = session.Stream;
            StringBuilder lineBuffer = new StringBuilder();
            byte[] buffer = new byte[4096];
            Decoder utf8Decoder = new UTF8Encoding(false, true).GetDecoder();
            char[] charBuf = new char[4096];

            while (isIpcRunning && client.Connected)
            {
                int bytesRead = ProcessClientStream_ReadChunk(stream, buffer);
                if (bytesRead < 0) continue;
                if (bytesRead == 0) break;

                if (!ProcessClientStream_DecodeUtf8(clientId, utf8Decoder, buffer, bytesRead, charBuf, out string chunk))
                    break;
                lineBuffer.Append(chunk);

                string[] lines = ProcessClientStream_ExtractLines(clientId, lineBuffer, out bool disconnectClient);
                if (disconnectClient)
                    break;
                if (lines == null) continue;
                foreach (string line in lines)
                {
                    ProcessClientStream_DispatchLine(session, line);
                }
            }
        }

        private int ProcessClientStream_ReadChunk(NetworkStream stream, byte[] buffer)
        {
            if (!stream.DataAvailable)
            {
                Thread.Sleep(50);
                return -1;
            }

            return stream.Read(buffer, 0, buffer.Length);
        }

        private bool ProcessClientStream_DecodeUtf8(
            int clientId,
            Decoder utf8Decoder,
            byte[] buffer,
            int bytesRead,
            char[] charBuf,
            out string chunk)
        {
            chunk = null;
            try
            {
                int charCount = utf8Decoder.GetChars(buffer, 0, bytesRead, charBuf, 0, false);
                chunk = new string(charBuf, 0, charCount);
                return true;
            }
            catch (DecoderFallbackException)
            {
                Interlocked.Increment(ref _ipcInvalidUtf8Count);
                Print($"V12 IPC: Invalid UTF-8 payload from client {clientId}; disconnecting.");
                return false;
            }
        }

        private string[] ProcessClientStream_ExtractLines(
            int clientId,
            StringBuilder lineBuffer,
            out bool disconnectClient)
        {
            disconnectClient = false;

            if (lineBuffer.Length > IpcMaxBufferedChars)
            {
                Print($"V12 IPC: Client {clientId} exceeded max buffered payload ({IpcMaxBufferedChars}); disconnecting.");
                disconnectClient = true;
                return null;
            }

            string accumulated = lineBuffer.ToString();
            int lastNewline = accumulated.LastIndexOf('\n');
            if (lastNewline < 0) return null;

            string completeLines = accumulated.Substring(0, lastNewline);
            lineBuffer.Clear();
            if (lastNewline + 1 < accumulated.Length)
            {
                lineBuffer.Append(accumulated.Substring(lastNewline + 1));
                if (lineBuffer.Length > IpcMaxBufferedChars)
                {
                    Print($"V12 IPC: Client {clientId} residue exceeded max buffered payload ({IpcMaxBufferedChars}); disconnecting.");
                    disconnectClient = true;
                    return null;
                }
            }

            return completeLines.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private void ProcessClientStream_DispatchLine(IpcClientSession session, string line)
        {
            HandleIncomingIpcLine(session, line);
        }

        private void HandleIncomingIpcLine(IpcClientSession session, string line)
        {
            int clientId = session.ClientId;
            NetworkStream stream = session.Stream;
            string message = line.Trim();
            if (string.IsNullOrEmpty(message)) return;

            if (HandleIncomingIpcLine_RespondLayout(stream, message))
                return;

            if (!HandleIncomingIpcLine_TryEnqueueCommand(clientId, message))
                return;

            HandleIncomingIpcLine_TriggerProcessing();
        }

        private bool HandleIncomingIpcLine_RespondLayout(NetworkStream stream, string message)
        {
            // Handle GET_LAYOUT (Synchronous Response to THIS client only)
            if (!message.StartsWith("GET_LAYOUT"))
                return false;

            // Build 935 [R-04]: Snapshot scalar state under lock; format string outside
            // to minimize critical section duration (removes string allocation from lock).
            string snapMode; double snapStop; int snapCount;
            double snapT1, snapT2, snapT3, snapT4, snapT5;
            TargetMode snapT1Type, snapT2Type, snapT3Type, snapT4Type, snapT5Type;
            string snapCit; string snapLeader; bool snapTrma, snapRrma;
            snapMode   = GetCurrentConfigMode();
            snapStop   = snapMode == "RMA" ? RMAStopATRMultiplier : StopMultiplier;
            snapCount  = activeTargetCount;
            snapT1     = Target1Value; snapT1Type = T1Type;
            snapT2     = Target2Value; snapT2Type = T2Type;
            snapT3     = Target3Value; snapT3Type = T3Type;
            snapT4     = Target4Value; snapT4Type = T4Type;
            snapT5     = Target5Value; snapT5Type = T5Type;
            snapCit    = ChaseIfTouchPoints ?? "0";
            snapLeader = _stickyLeaderAccount ?? string.Empty;
            snapTrma   = isTrendRmaMode;
            snapRrma   = isRetestRmaMode;
            string configResponse = string.Format(
                "CONFIG|{0}|MODE:{0};COUNT:{1};T1:{2};T1TYPE:{3};T2:{4};T2TYPE:{5};T3:{6};T3TYPE:{7};T4:{8};T4TYPE:{9};T5:{10};T5TYPE:{11};STR:{12};STRTYPE:ATR;MAX:{13};CIT:{14};OT:Limit;TRMA:{15};RRMA:{16};LEADER:{17};\n",
                snapMode, snapCount, snapT1, ToIpcTargetMode(snapT1Type),
                snapT2, ToIpcTargetMode(snapT2Type),
                snapT3, ToIpcTargetMode(snapT3Type),
                snapT4, ToIpcTargetMode(snapT4Type),
                snapT5, ToIpcTargetMode(snapT5Type),
                snapStop, MaxRiskAmount, snapCit,
                snapTrma ? "1" : "0", snapRrma ? "1" : "0", snapLeader);
            byte[] responseBytes = Encoding.UTF8.GetBytes(configResponse);
            stream.Write(responseBytes, 0, responseBytes.Length);
            stream.Flush();
            return true;
        }

        private bool HandleIncomingIpcLine_TryEnqueueCommand(int clientId, string message)
        {
            // Enqueue for processing
            if (!TryEnqueueIpcCommand(message, out string enqueueReason))
            {
                Print(string.Format("V12 IPC REJECT [client={0}] {1}: {2}", clientId, message, enqueueReason));
                return false;
            }
            Print(string.Format("V12.1 IPC ENQUEUE [client={0}] {1}", clientId, message));
            return true;
        }

        private void HandleIncomingIpcLine_TriggerProcessing()
        {
            // Trigger processing
            try
            {
                TriggerCustomEvent(o => ProcessIpcCommands(), null);
            }
            catch { }
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
                        try { kvp.Value.Client.Close(); } catch { }
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
