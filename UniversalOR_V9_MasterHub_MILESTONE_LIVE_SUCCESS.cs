// FULL CONTENT OF UniversalOR_V9_MasterHub.cs at Milestone LIVE SUCCESS
// Restored from Commit 48bfa7e
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class UniversalOR_V9_MasterHub_MILESTONE_LIVE_SUCCESS : Strategy
    {
        private string accountPrefix = "Apex";
        private int ipcPort = 5000;
        private TcpListener listener;
        private Thread serverThread;
        private bool isRunning;
        private readonly object commandLock = new object();
        private Queue<string> commandQueue = new Queue<string>();

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "V9 Master Hub - MILESTONE: LIVE SUCCESS";
                Name = "UniversalOR_V9_MasterHub_MILESTONE_LIVE_SUCCESS";
                Calculate = Calculate.OnEachTick;
                IsUnmanaged = true;
                AccountPrefix = "Apex";
                IpcPort = 5000;
            }
            else if (State == State.DataLoaded)
            {
                StartIpcServer();
            }
            else if (State == State.Terminated)
            {
                StopIpcServer();
            }
        }

        protected override void OnBarUpdate()
        {
            ProcessCommandQueue();
        }

        private void StartIpcServer()
        {
            isRunning = true;
            serverThread = new Thread(ListenForRemote) { IsBackground = true, Name = "V9_IPC_Server" };
            serverThread.Start();
            Print("V9 HUB MILESTONE: Server started on port " + IpcPort);
        }

        private void StopIpcServer()
        {
            isRunning = false;
            listener?.Stop();
            serverThread?.Join(1000);
        }

        private void ListenForRemote()
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, IpcPort);
                listener.Start();
                while (isRunning)
                {
                    if (!listener.Pending()) { Thread.Sleep(10); continue; }
                    using (TcpClient client = listener.AcceptTcpClient())
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        lock (commandLock) { commandQueue.Enqueue(message); }
                    }
                }
            } catch { }
        }

        private void ProcessCommandQueue()
        {
            string command = null;
            lock (commandLock) { if (commandQueue.Count > 0) command = commandQueue.Dequeue(); }
            if (command != null)
            {
                Print("V9 HUB MILESTONE: Command: " + command);
                string[] parts = command.Split('|');
                if (parts.Length < 2) return;
                string action = parts[0];
                string symbol = parts[1];
                if (action == "LONG") ExecuteOrder(symbol, OrderAction.Buy);
                else if (action == "SHORT") ExecuteOrder(symbol, OrderAction.SellShort);
            }
        }

        private void ExecuteOrder(string symbol, OrderAction action)
        {
            Instrument instrument = Instrument.GetInstrument(symbol);
            if (instrument == null) return;
            foreach (Account account in Account.All)
            {
                if (account.Name.Contains(AccountPrefix))
                {
                    Order order = account.CreateOrder(instrument, action, OrderType.Market, TimeInForce.Gtc, 1, 0, 0, "", "V9_Milestone", null);
                    account.Submit(new[] { order });
                }
            }
        }
    }
}
