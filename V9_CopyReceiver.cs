#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using System.Net.Sockets;
using System.Threading;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class V9_CopyReceiver : Strategy
	{
		private TcpClient client;
		private NetworkStream stream;
		private Thread receiveThread;
		private bool isRunning;
		private string serverIp = "127.0.0.1";
		private int serverPort = 5000;
		private readonly object lockObject = new object();

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Receives copy trading signals from V9_ExternalRemote and executes trades.";
				Name										= "V9_CopyReceiver";
				Calculate									= Calculate.OnBarClose;
				EntriesPerDirection							= 1;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.WaitUntilFlat;
				TimeInForce									= TimeInForce.Gtc;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade							= 20;
				// Disable this property for performance resistance in strategy analyzer or optimization
				IsInstantiatedOnEachOptimizationIteration	= true;
				
				ServerIp = "127.0.0.1";
				ServerPort = 5000;
			}
			else if (State == State.Realtime)
			{
				StartTcpClient();
			}
			else if (State == State.Terminated)
			{
				StopTcpClient();
			}
		}

		private void StartTcpClient()
		{
			try
			{
				isRunning = true;
				client = new TcpClient();
				client.BeginConnect(ServerIp, ServerPort, OnConnect, null);
				Print("V9_CopyReceiver: Attempting to connect to TCP Server...");
			}
			catch (Exception ex)
			{
				Print("V9_CopyReceiver: Connection Error: " + ex.Message);
			}
		}

		private void OnConnect(IAsyncResult ar)
		{
			try
			{
				client.EndConnect(ar);
				stream = client.GetStream();
				Print("V9_CopyReceiver: Connected to TCP Server " + ServerIp + ":" + ServerPort);

				receiveThread = new Thread(ReceiveMessages);
				receiveThread.IsBackground = true;
				receiveThread.Start();
			}
			catch (Exception ex)
			{
				Print("V9_CopyReceiver: Failed to connect: " + ex.Message);
				// Attempt reconnect logic could go here
			}
		}

		private void ReceiveMessages()
		{
			byte[] buffer = new byte[1024];
			while (isRunning && client != null && client.Connected)
			{
				try
				{
					int bytesRead = stream.Read(buffer, 0, buffer.Length);
					if (bytesRead == 0) break;

					string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
					ProcessMessage(message);
				}
				catch
				{
					break;
				}
			}
			Print("V9_CopyReceiver: Disconnected from server.");
			if (isRunning)
			{
				// Reconect logic
				Thread.Sleep(5000);
				StartTcpClient();
			}
		}

		private void ProcessMessage(string message)
		{
			if (string.IsNullOrEmpty(message)) return;

			// Support multiple messages in one packet if separated by newline
			string[] lines = message.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (var line in lines)
			{
				Print("V9_CopyReceiver: Received " + line);

				if (line.StartsWith("HEARTBEAT|"))
				{
					SendAck();
					continue;
				}
				else if (line.StartsWith("SYNC_TARGET|"))
				{
					// Format: SYNC_TARGET|SYMBOL|NET_QTY
					string[] syncParts = line.Split('|');
					if (syncParts.Length == 3)
					{
						string syncSymbol = syncParts[1];
						double targetQty = 0;
						if (double.TryParse(syncParts[2], out targetQty))
						{
							SyncPosition(syncSymbol, targetQty);
						}
					}
					continue;
				}

				ParseAndExecute(line);
			}
		}

		private void SyncPosition(string symbol, double targetQty)
		{
			// Verify symbol match
			if (Instrument.MasterInstrument.Name != symbol) return;

			// Calculate current net position
			double currentQty = 0;
			if (Position.MarketPosition == MarketPosition.Long)
				currentQty = Position.Quantity;
			else if (Position.MarketPosition == MarketPosition.Short)
				currentQty = -Position.Quantity;

			// Check for discrepancy
			// Use small epsilon for double comparison
			if (Math.Abs(currentQty - targetQty) < 0.001) return;

			Print(string.Format("V9_CopyReceiver: Sync Discrepancy detected. Target: {0}, Current: {1}. Reconciling...", targetQty, currentQty));

			double diff = targetQty - currentQty;

			// If diff > 0, we need to buy 'diff' amount
			// If diff < 0, we need to sell 'abs(diff)' amount
			
			// NinjaTrader OCO / Signal logic usually handles "Go To" by reversing if needed.
			// But since we are "Copy Receiving", simpler is better.
			// However, EnterLong/EnterShort usually are additive in some modes or separate.
			// We set EntryHandling = EntryHandling.AllEntries in OnStateChange, which implies additive.
			// But for SYNC, we want to simply place a market order for the difference.

			if (diff > 0)
			{
				EnterLong((int)Math.Abs(diff), "V9_SyncCorrection");
			}
			else if (diff < 0)
			{
				EnterShort((int)Math.Abs(diff), "V9_SyncCorrection");
			}
		}

		private void SendAck()
		{
			if (stream == null || !client.Connected) return;
			try
			{
				byte[] data = Encoding.UTF8.GetBytes("ACK|" + DateTime.Now.Ticks + "\n");
				stream.Write(data, 0, data.Length);
			}
			catch { }
		}

		private void ParseAndExecute(string signal)
		{
			// Format: "ACTION|SYMBOL|QUANTITY"
			string[] parts = signal.Split('|');
			if (parts.Length != 3) return;

			string action = parts[0].ToUpper();
			string symbol = parts[1].ToUpper();
			double qty = 0;
			
			if (!double.TryParse(parts[2], out qty)) return;

			// Handle order execution on UI thread/Main thread context if needed
			// NinjaTrader strategies are single threaded, but signals come from background thread
			// Use trigger or check in OnUpdate
			
			ExecuteTrade(action, symbol, (int)qty);
		}

		private void ExecuteTrade(string action, string symbol, int qty)
		{
			// Cross-instrument check if needed, but for now assuming MES/MGC
			if (Instrument.MasterInstrument.Name != symbol)
			{
				Print("V9_CopyReceiver: Signal instrument " + symbol + " does not match chart instrument " + Instrument.MasterInstrument.Name);
				// In a multi-account setup, we usually run one strategy per account/symbol
				// Or we use multi-instrument strategies.
				// For this version, we expect the strategy to be on the correct chart.
				return;
			}

			if (action == "LONG")
			{
				EnterLong(qty, "V9_CopyEntry");
			}
			else if (action == "SHORT")
			{
				EnterShort(qty, "V9_CopyEntry");
			}
			else if (action == "CLOSE")
			{
				ExitPosition("V9_CopyEntry", "V9_CopyExit");
			}
		}

		protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, DateTime time, int orderId, string orderName)
		{
			// Send fill back to server
			if (execution.Order != null && execution.Order.OrderState == OrderState.Filled)
			{
				string fillMsg = string.Format("FILL|{0}|{1}|{2}\n", Instrument.MasterInstrument.Name, quantity, price);
				if (stream != null && client.Connected)
				{
					try
					{
						byte[] data = Encoding.UTF8.GetBytes(fillMsg);
						stream.Write(data, 0, data.Length);
					}
					catch { }
				}
			}
		}

		private void StopTcpClient()
		{
			isRunning = false;
			if (client != null)
			{
				client.Close();
				client = null;
			}
		}

		#region Properties
		[NinjaScriptProperty]
		[Display(Name="Server IP", Order=1, GroupName="Parameters")]
		public string ServerIp
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Server Port", Order=2, GroupName="Parameters")]
		public int ServerPort
		{ get; set; }
		#endregion
	}
}
