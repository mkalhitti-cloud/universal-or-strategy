# V9 TCP Protocol Specification

**Version**: 1.0
**Status**: Ready for Implementation
**Purpose**: Define communication between V9_ExternalRemote (SERVER) and V9_CopyReceiver (CLIENT)

---

## Overview

```
V9_ExternalRemote (TCP SERVER)
├── Listens on: 127.0.0.1:5000
├── Accepts: Multiple NT strategy connections
└── Broadcasts: Trading signals to all connected clients

V9_CopyReceiver (TCP CLIENT)
├── Connects to: 127.0.0.1:5000
├── Receives: Trading signals
├── Executes: Orders on all 20 accounts
└── Sends back: Position updates
```

---

## Message Format

**All messages are UTF-8 encoded text strings separated by pipe delimiter (|)**

```
Format: FIELD1|FIELD2|FIELD3|...|FIELDN
Example: LONG|MES|1|Account_Count:20
```

**Max message size**: 1024 bytes
**Encoding**: UTF-8
**Delimiter**: Pipe character (|)
**Line ending**: None (single message per send)
**Timeout**: 30 seconds (no heartbeat = disconnect)

---

## Message Types

### 1. CONNECTION PHASE

#### Client → Server: CONNECT
```
CONNECT|STRATEGY_NAME|ACCOUNT_COUNT:N|[OPTIONAL_METADATA]

Example: CONNECT|V9_CopyReceiver|Account_Count:20|Version:1.0

Fields:
- CONNECT: Message type (fixed)
- STRATEGY_NAME: Name of NT strategy (e.g., "V9_CopyReceiver")
- ACCOUNT_COUNT: Number of accounts this strategy manages
- OPTIONAL_METADATA: Additional info (version, subaccount list, etc.)

Server response: READY|Server_Version:1.0
```

#### Server → Client: READY
```
READY|SERVER_VERSION|[STATUS_INFO]

Example: READY|1.0|Broadcast_Ready:Yes

Fields:
- READY: Acknowledgment message (fixed)
- SERVER_VERSION: V9 app version
- STATUS_INFO: Additional status details
```

---

### 2. SIGNAL PHASE (Trading)

#### Server → Client: SIGNAL (One-way broadcast)
```
ACTION|SYMBOL|QUANTITY|[OPTIONS]

Examples:
- LONG|MES|1
- SHORT|NQ|2|TP:4700|SL:4650
- FLATTEN|ALL|0
- CLOSE_HALF|MES|0
- CLOSE_ONE|ES|0

Fields:
- ACTION: LONG, SHORT, FLATTEN, CLOSE_HALF, CLOSE_ONE, etc.
- SYMBOL: Futures root (MES, NQ, ES, GC, CL, YM, etc.)
- QUANTITY: Contracts per account
  - 0 = Close position (used with FLATTEN, CLOSE_HALF, etc.)
  - 1+ = Open position with this quantity
- OPTIONS: Optional parameters
  - TP:price = Take profit price
  - SL:price = Stop loss price
  - TIF:order_type = Time in force

Handling:
- Client receives signal
- Parses ACTION, SYMBOL, QUANTITY
- For each of 20 accounts:
  - Submit order with QUANTITY contracts
  - Use SYMBOL as the instrument
  - Store in _positionsByAccount
- Send back: POSITION updates (see below)

No acknowledgment needed from client (fire-and-forget)
```

---

### 3. POSITION UPDATE PHASE (Client → Server)

#### Client → Server: POSITION (Per-account updates)
```
POSITION|ACCOUNT_NAME|DIRECTION|QUANTITY|ENTRY_PRICE|PNL|[TIMESTAMP]

Example: POSITION|Account1|LONG|1|4520.50|+150.00|2026-01-27T18:05:30Z

Fields:
- POSITION: Message type (fixed)
- ACCOUNT_NAME: Name of the account (Account1, Account2, etc.)
- DIRECTION: LONG, SHORT, FLAT
- QUANTITY: Number of contracts held
- ENTRY_PRICE: Average entry price
- PNL: Profit/Loss in dollars (+150.00 or -75.50)
- TIMESTAMP: ISO8601 timestamp (optional)

Frequency:
- Send after every order execution
- Send every heartbeat interval (5 seconds)
```

#### Client → Server: POSITION_SUMMARY (Aggregate)
```
POSITION_SUMMARY|TOTAL_ACCOUNTS|TOTAL_QUANTITY|TOTAL_PNL|AVG_ENTRY|[CONTRACTS_BREAKDOWN]

Example: POSITION_SUMMARY|20|20|+2800.00|4520.50|LONG:20,SHORT:0

Fields:
- POSITION_SUMMARY: Message type (fixed)
- TOTAL_ACCOUNTS: Number of accounts holding position (20)
- TOTAL_QUANTITY: Sum of all contracts across all accounts
- TOTAL_PNL: Sum of all P&L across all accounts
- AVG_ENTRY: Weighted average entry price
- CONTRACTS_BREAKDOWN: Optional breakdown (LONG:20,SHORT:0)

Frequency:
- Send after every order execution
- Send every heartbeat interval
```

---

### 4. HEARTBEAT PHASE (Keep-alive)

#### Client → Server: HEARTBEAT
```
HEARTBEAT|STRATEGY_NAME|[STATUS]

Example: HEARTBEAT|V9_CopyReceiver|Connected:20_Accounts

Fields:
- HEARTBEAT: Message type (fixed)
- STRATEGY_NAME: Client strategy name
- STATUS: Optional status (connection health, account count, etc.)

Frequency: Every 5 seconds

Server response: HEARTBEAT_ACK
If no heartbeat for 30 seconds: Server disconnects client
If no heartbeat_ack for 30 seconds: Client reconnects to server
```

#### Server → Client: HEARTBEAT_ACK
```
HEARTBEAT_ACK|SERVER_TIME|[STATUS]

Example: HEARTBEAT_ACK|2026-01-27T18:05:30Z|All_Systems_OK

Fields:
- HEARTBEAT_ACK: Acknowledgment message (fixed)
- SERVER_TIME: Server's current time (ISO8601)
- STATUS: Server status
```

---

### 5. ERROR PHASE

#### Either direction: ERROR
```
ERROR|ERROR_CODE|ERROR_MESSAGE|[DETAILS]

Examples:
- ERROR|CONNECTION_LOST|Lost connection to V9 server|Reconnecting...
- ERROR|INVALID_SIGNAL|Unknown action type|Action: INVALID_ACTION
- ERROR|ACCOUNT_NOT_FOUND|Account does not exist|Account: Account99

Fields:
- ERROR: Message type (fixed)
- ERROR_CODE: Standard error code (see below)
- ERROR_MESSAGE: Human-readable error message
- DETAILS: Additional context or metadata

Handling:
- Log error
- If server error: Client may retry or reconnect
- If client error: Server logs but continues listening
```

---

## Error Codes

| Code | Meaning | Action |
|------|---------|--------|
| `CONNECTION_LOST` | Lost connection | Reconnect |
| `INVALID_SIGNAL` | Unknown action/symbol | Ignore, log |
| `ACCOUNT_NOT_FOUND` | Account doesn't exist | Log warning |
| `ORDER_REJECTED` | NT rejected order | Log, skip account |
| `TIMEOUT` | No response within 30s | Disconnect, reconnect |
| `PROTOCOL_ERROR` | Malformed message | Close connection |
| `SERVER_SHUTDOWN` | Server shutting down | Reconnect later |
| `VERSION_MISMATCH` | Protocol version incompatible | Upgrade |

---

## Example Session

```
TIME    DIRECTION    MESSAGE
-----   ---------    -------

18:05:00  Client→Server  CONNECT|V9_CopyReceiver|Account_Count:20|Version:1.0
18:05:01  Server→Client  READY|1.0|Broadcast_Ready:Yes

18:05:10  Server→Client  LONG|MES|1

18:05:11  Client→Server  POSITION|Account1|LONG|1|4520.50|+0.00|2026-01-27T18:05:11Z
18:05:11  Client→Server  POSITION|Account2|LONG|1|4520.50|+10.00|2026-01-27T18:05:11Z
18:05:11  Client→Server  POSITION|Account3|LONG|1|4520.50|-5.00|2026-01-27T18:05:11Z
          ... (16 more accounts)
18:05:11  Client→Server  POSITION_SUMMARY|20|20|+2800.00|4520.50|LONG:20,SHORT:0

18:05:15  Client→Server  HEARTBEAT|V9_CopyReceiver|Connected:20_Accounts
18:05:15  Server→Client  HEARTBEAT_ACK|2026-01-27T18:05:15Z|All_Systems_OK

18:05:20  Server→Client  SHORT|ES|2

18:05:21  Client→Server  POSITION|Account1|SHORT|2|4580.00|+150.00|2026-01-27T18:05:21Z
          ... (19 more accounts)
18:05:21  Client→Server  POSITION_SUMMARY|20|40|+3200.00|4550.25|LONG:20,SHORT:20

18:05:30  Server→Client  FLATTEN|ALL|0

18:05:31  Client→Server  POSITION|Account1|FLAT|0|0.00|+150.00|2026-01-27T18:05:31Z
          ... (19 more accounts)
18:05:31  Client→Server  POSITION_SUMMARY|0|0|+3200.00|0.00|LONG:0,SHORT:0
```

---

## Implementation Notes

### Server (V9_ExternalRemote)

1. **Listener Thread**
   ```csharp
   while (serverRunning)
   {
       TcpClient client = tcpListener.AcceptTcpClient();
       connectedClients.Add(client);
       StartClientHandler(client);
   }
   ```

2. **Broadcast Method**
   ```csharp
   async Task BroadcastSignal(string signal)
   {
       foreach (var client in connectedClients)
       {
           if (client.Connected)
               await SendMessage(client, signal);
       }
   }
   ```

3. **Receive Position Updates**
   ```csharp
   async Task ReceiveFromClient(TcpClient client)
   {
       while (client.Connected)
       {
           string message = await ReadMessage(client);
           if (message.StartsWith("POSITION"))
               UpdateUI_PositionDisplay(message);
       }
   }
   ```

### Client (V9_CopyReceiver Strategy)

1. **Connection**
   ```csharp
   protected override void OnStateChange()
   {
       if (State == State.SetDefaults)
       {
           // Initialize connection settings
       }
       else if (State == State.Configure)
       {
           ConnectToV9Server();
       }
   }
   ```

2. **Signal Reception**
   ```csharp
   private async Task ListenForSignals()
   {
       while (client.Connected)
       {
           string signal = await ReadMessage(client);
           ProcessSignal(signal);
       }
   }
   ```

3. **Order Execution**
   ```csharp
   private void ProcessSignal(string signal)
   {
       var parts = signal.Split('|');
       string action = parts[0];
       string symbol = parts[1];
       int quantity = int.Parse(parts[2]);

       foreach (var account in Account.All)
       {
           // Execute order on each account
           ExecuteOrder(account, action, symbol, quantity);
       }
   }
   ```

---

## Testing Checklist

- [ ] Server listens on 127.0.0.1:5000
- [ ] Client connects without errors
- [ ] CONNECT/READY handshake completes
- [ ] Server receives HEARTBEAT every 5 seconds
- [ ] Server sends HEARTBEAT_ACK
- [ ] Server broadcasts signal to client
- [ ] Client executes order on all 20 accounts
- [ ] Client sends POSITION updates
- [ ] Client sends POSITION_SUMMARY
- [ ] Server displays position in UI
- [ ] Connection survives 10+ signal cycles
- [ ] Client reconnects if server crashes
- [ ] Server handles client disconnect gracefully
- [ ] Error messages logged correctly

---

## Performance Requirements

| Metric | Requirement |
|--------|-------------|
| Signal Broadcast Latency | < 100ms to all 20 accounts |
| Position Update Frequency | Every 5 seconds (heartbeat) |
| Connection Timeout | 30 seconds idle = disconnect |
| Max Message Size | 1024 bytes |
| Max Connected Clients | 1 (only 1 NT strategy) |
| Message Throughput | 10+ signals/second |

---

## Security Notes

- **No authentication**: Assumes trusted local network (127.0.0.1)
- **No encryption**: All communication in plaintext (local only)
- **Input validation**: Server should validate all client messages
- **DoS protection**: Server should limit message frequency if needed

---

## Future Extensions

- Multi-client support (multiple NT instances)
- Account-specific signal routing
- Advanced order types (stop-limit, OCO, etc.)
- Real-time P&L streaming
- Historical trade logging
- Performance metrics tracking

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-01-25 | Initial specification |
