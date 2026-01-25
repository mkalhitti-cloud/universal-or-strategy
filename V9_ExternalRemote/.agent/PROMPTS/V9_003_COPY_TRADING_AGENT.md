# V9_003 COPY TRADING AGENT PROMPT

**Use this prompt when V9_001 PASSES (TOS RTD confirmed working)**

---

## YOU ARE: V9 Copy Trading Agent

**Role**: Build multi-account trading orchestration system for V9

**Model**: Opus 4.5 (or Gemini 3 Pro)

**Workspace**: `DEVELOPMENT/V9_WIP/COPY_TRADING/`

**Task ID**: V9_003

**Status**: PENDING (starts after V9_001 passes, or independently)

---

## CONTEXT - Read First (In Order)

1. `.agent/SHARED_CONTEXT/CURRENT_SESSION.md` - Current status
2. `.agent/V9_ARCHITECTURE.md` - The architecture you're implementing
3. `.agent/TASKS/MASTER_TASKS.json` - Full task hierarchy
4. `V9_ExternalRemote/MainWindow.xaml.cs` - Current WPF app structure
5. `.agent/SHARED_CONTEXT/V9_STATUS.json` - V9 status
6. `.agent/MCP_DELEGATION_GUIDE.md` - Cost-efficient file operation delegation (if in Opus)

---

## YOUR MISSION

Implement **Option C Architecture**: ONE NinjaTrader strategy that routes signals to 20 Apex accounts simultaneously.

**Architecture Overview**:
```
V9 App (TCP SERVER on port 5000)
    ↓
    Broadcasts: "LONG|MES|1"
    ↓
NT Strategy "V9_CopyReceiver" (TCP CLIENT)
    ↓ (routes to all 20 accounts)
    ├── Account 1 → Execute order
    ├── Account 2 → Execute order
    └── ... Account 20 → Execute order
```

---

## PHASE 1: Convert V9 to TCP SERVER

### Files to Modify
- `V9_ExternalRemote/MainWindow.xaml.cs`

### Tasks

**1. Flip TCP Role**
   - Current: Tries to CONNECT to Hub (127.0.0.1:5000)
   - Target: Becomes SERVER, listens on 5000, accepts NT connections

**2. Implement Server Listener**
   ```csharp
   private TcpListener _tcpListener;
   private List<TcpClient> _connectedClients = new();

   private void StartTcpServer()
   {
       _tcpListener = new TcpListener(IPAddress.Any, 5000);
       _tcpListener.Start();
       // Accept connections in background thread
   }
   ```

**3. Accept Client Connections**
   - Listen for incoming NT connections
   - When NT connects, add to _connectedClients list
   - Send acknowledgment: "READY|Server_Version:1.0"

**4. Broadcast Signals**
   ```csharp
   private async Task BroadcastSignal(string action, string symbol, int quantity)
   {
       string signal = $"{action}|{symbol}|{quantity}";
       foreach (var client in _connectedClients)
       {
           await SendToClient(client, signal);
       }
   }
   ```

**5. Update UI**
   - Show number of connected clients
   - Show "SERVER READY" status instead of "HUB CONNECTED"
   - Log all incoming connections

### Success Criteria
- [x] V9 listens on port 5000
- [x] Accepts NT connections
- [x] Displays "Connected Clients: N"
- [x] Can broadcast signal to all connected clients
- [x] Handles client disconnections gracefully

---

## PHASE 2: Create V9_CopyReceiver NinjaTrader Strategy

### New File
`DEVELOPMENT/V9_WIP/COPY_TRADING/V9_CopyReceiver.cs`

### Architecture
```csharp
public class V9_CopyReceiver : Strategy
{
    private TcpClient _client;
    private string _v9ServerIp = "127.0.0.1";
    private int _v9ServerPort = 5000;
    private Dictionary<string, int> _positionsByAccount = new();

    // Connection management
    // Signal reception
    // Multi-account order routing
    // Position tracking
    // Heartbeat logic
}
```

### Phase 2A: Connection Management

**On Strategy Start**:
1. Connect to V9 on 127.0.0.1:5000
2. Send: "CONNECT|V9_CopyReceiver|Account_Count:20"
3. Wait for: "READY|Server_Version:1.0"
4. Add to log: "Connected to V9 Server"

**Connection Check**:
```csharp
protected override void OnBarClose()
{
    if (!_client.Connected)
    {
        ReconnectToV9();
    }
}
```

**Heartbeat** (every 5 bars):
```csharp
if (CurrentBar % 5 == 0)
{
    SendSignal("HEARTBEAT|V9_CopyReceiver");
}
```

### Phase 2B: Signal Reception

**Listen for Signals from V9**:
```csharp
private async Task ListenForSignals()
{
    while (_client.Connected)
    {
        byte[] buffer = new byte[256];
        int bytes = await _client.GetStream().ReadAsync(buffer, 0, buffer.Length);
        string signal = Encoding.UTF8.GetString(buffer, 0, bytes);
        ProcessSignal(signal);
    }
}

private void ProcessSignal(string signal)
{
    // Parse: "LONG|MES|1"
    string[] parts = signal.Split('|');
    string action = parts[0];     // LONG, SHORT, FLATTEN
    string symbol = parts[1];     // MES, NQ, ES
    int quantity = int.Parse(parts[2]);

    ExecuteOnAllAccounts(action, symbol, quantity);
}
```

### Phase 2C: Multi-Account Order Routing

**Critical**: Route to ALL 20 accounts simultaneously

```csharp
private void ExecuteOnAllAccounts(string action, string symbol, int quantity)
{
    var allAccounts = Account.All;  // NinjaTrader built-in

    foreach (var account in allAccounts)
    {
        if (action == "LONG")
        {
            EnterLong(account, quantity, $"V9_{symbol}_LONG");
        }
        else if (action == "SHORT")
        {
            EnterShort(account, quantity, $"V9_{symbol}_SHORT");
        }
        else if (action == "FLATTEN")
        {
            ExitLong(account, quantity, $"V9_FLATTEN");
            ExitShort(account, quantity, $"V9_FLATTEN");
        }

        // Track position
        _positionsByAccount[account.Name] = account.GetQuantityBySignalName($"V9_{symbol}_{action}");
    }
}
```

### Phase 2D: Position Tracking & Updates

**Send Position Updates Back to V9**:
```csharp
private void SendPositionUpdates()
{
    var allAccounts = Account.All;
    double totalPnL = 0;
    int totalContracts = 0;

    foreach (var account in allAccounts)
    {
        double accountPnL = account.Equity - account.Cash;
        int quantity = account.Position.Quantity;

        // Send to V9: "POSITION|Account1|LONG|1|4520.5|+150.00"
        string update = $"POSITION|{account.Name}|{GetPosition(account)}|{quantity}|{GetEntryPrice(account)}|{accountPnL:+0.00;-0.00}";
        SendSignal(update);

        totalPnL += accountPnL;
        totalContracts += Math.Abs(quantity);
    }

    // Send aggregate
    SendSignal($"POSITION_SUMMARY|20|{totalContracts}|{totalPnL:+0.00;-0.00}");
}
```

### Success Criteria
- [x] Strategy connects to V9 on startup
- [x] Receives signals from V9
- [x] Routes orders to all 20 accounts
- [x] Tracks positions per account
- [x] Sends updates back to V9
- [x] Maintains connection with heartbeat
- [x] Auto-reconnects if connection drops

---

## PHASE 3: Integration Testing

### Test Checklist
1. **Start V9 App**
   - Verify SERVER listening on 5000
   - Status should show "SERVER READY"

2. **Load V9_CopyReceiver in NT**
   - Verify connection to V9
   - Check log for "Connected to V9 Server"
   - Verify "Connected Clients: 1" in V9 app

3. **Send Test Signals**
   - Click LONG in V9 app
   - Verify all 20 accounts receive order
   - Check NT strategy log for 20 executions

4. **Position Tracking**
   - Verify positions show in V9 app per account
   - Verify aggregate P&L displayed
   - Verify individual P&L per account

5. **Stress Test**
   - Click LONG, then SHORT multiple times
   - Verify rapid execution across all accounts
   - Monitor for any missed orders

6. **Connection Recovery**
   - Close V9 app
   - Verify NT strategy attempts reconnect
   - Restart V9
   - Verify NT reconnects automatically

### Success Criteria
- [x] One V9 signal = 20 simultaneous NT orders
- [x] Positions track correctly per account
- [x] P&L aggregates correctly
- [x] Connection survives restarts
- [x] No missed signals or orders

---

## PHASE 4: UI Enhancements (Optional)

If time permits, enhance V9 UI:
1. Add "Connected Accounts" display
2. Add "Total P&L" dashboard
3. Add per-account P&L table
4. Add connection status indicator
5. Add signal history log

---

## DELIVERABLES

When complete, create:

**File 1**: `V9_CopyReceiver.cs`
- Complete NinjaTrader strategy
- All phases implemented
- Full comments and documentation

**File 2**: `.agent/SHARED_CONTEXT/V9_COPY_TRADING_STATUS.json`
```json
{
  "task_id": "V9_003",
  "status": "COMPLETED",
  "completed_date": "2026-01-27T18:00:00Z",
  "components": {
    "v9_server": "READY",
    "copy_receiver_strategy": "TESTED",
    "multi_account_routing": "WORKING",
    "position_tracking": "VERIFIED"
  },
  "test_results": {
    "signal_broadcast": "20/20 accounts",
    "position_sync": "100% accuracy",
    "connection_recovery": "SUCCESS"
  }
}
```

**File 3**: `DEVELOPMENT/V9_WIP/COPY_TRADING/TEST_RESULTS.md`
- Document test outcomes
- List any issues found
- Recommendations for production

---

## IMPORTANT NOTES

1. **Safety First**: Never test with real money initially
2. **Account Sync**: All 20 accounts must execute within 100ms
3. **Position Tracking**: Must track quantity per account accurately
4. **Connection**: Should survive NT/V9 restarts without manual intervention
5. **Logging**: Log every signal, order, position update for debugging

---

## IF YOU GET STUCK

1. Check `.agent/V9_ARCHITECTURE.md` for reference
2. Check `V9_ExternalRemote/MainWindow.xaml.cs` for current structure
3. Check NinjaTrader documentation for multi-account APIs
4. Update `.agent/SHARED_CONTEXT/CURRENT_SESSION.md` with blocker
5. Ask coordinator for help (read CURRENT_SESSION.md to find active agents)

---

## WHEN DONE

1. Mark task V9_003 as COMPLETED in `.agent/TASKS/MASTER_TASKS.json`
2. Update `.agent/SHARED_CONTEXT/CURRENT_SESSION.md` with completion status
3. Commit to git: `git commit -m "feat: Implement V9 copy trading multi-account routing (V9_003)"`
4. Don't push yet - coordinator will review and promote

---

## WHEN YOU'RE DONE (CRITICAL)

### 1. Update .agent/SHARED_CONTEXT/CURRENT_SESSION.md

Add a section with your completion report:
- Task ID and name
- Status (COMPLETED, BLOCKED, IN_PROGRESS)
- Results or findings
- Any blockers or issues
- What should happen next

### 2. Update Your Task Status File

Create/update: `.agent/SHARED_CONTEXT/V9_COPY_TRADING_STATUS.json`

Include:
- task_id
- status (COMPLETED, BLOCKED, IN_PROGRESS)
- completed_date (ISO8601 timestamp)
- results (specific outcomes)
- blockers (any issues)
- next_steps (what comes next)

### 3. Commit Your Changes

```bash
git add .
git commit -m "feat: Implement V9 copy trading - multi-account routing (V9_003)"
```

DO NOT PUSH - coordinator will review and promote.

---

**Good luck! This is a critical component of V9.**
