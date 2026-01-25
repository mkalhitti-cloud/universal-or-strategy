# V9 Architecture - Option C (ONE Strategy, Multiple Accounts)

**Date**: 2026-01-25
**Status**: Ready for Implementation
**Target Market Open**: 2026-01-27 6 PM EST

---

## System Overview

```
┌─────────────────────────────────────────┐
│  V9_ExternalRemote (WPF App)            │
│  - Reads TOS RTD (live data)            │
│  - Generates signals (LONG/SHORT)       │
│  - TCP SERVER on port 5000              │
└─────────────────────────────────────────┘
              │
              │ TCP Signal Broadcast
              │ Format: "LONG|MES|1"
              │
┌─────────────────────────────────────────┐
│  NinjaTrader 8 (ONE Instance)           │
│  - ONE Strategy: "V9_CopyReceiver"      │
│  - Acts as TCP CLIENT                   │
│  - Connected to 20 Apex Accounts        │
│  - Routes signals to all accounts       │
└─────────────────────────────────────────┘
              │
        ┌─────┴─────┬────────┬──────────┐
        ▼           ▼        ▼          ▼
    Account 1   Account 2  Account 3 ... Account 20
    (executes)  (executes) (executes)   (executes)
```

---

## Component Responsibilities

### V9_ExternalRemote (WPF App)
**File**: `V9_ExternalRemote/MainWindow.xaml.cs`

**Current State**:
- ✅ TCP SERVER foundation exists
- ✅ TOS RTD reader exists
- ✅ UI buttons exist (LONG, SHORT, FLATTEN)
- ❌ SERVER mode not activated (currently CLIENT to Hub)

**V9_003 Implementation**:
1. Convert to TCP SERVER (listen on 5000)
2. Accept incoming client connections from NT
3. Maintain list of connected NT clients
4. Broadcast signals to all clients simultaneously
5. Receive position updates from NT clients
6. Display aggregate P&L across all accounts

**Signal Protocol**:
```
Client connects → V9 registers it
User clicks LONG → V9 broadcasts: "LONG|MES|1" to all clients
Client receives → NT executes order
NT confirms → V9 updates UI

Format: ACTION|SYMBOL|QUANTITY
- ACTION: LONG, SHORT, FLATTEN, CLOSE_HALF, etc.
- SYMBOL: MES, NQ, ES, etc.
- QUANTITY: 1, 2, etc. (contracts/shares per account)
```

---

### V9_CopyReceiver Strategy (NinjaTrader)
**New File**: `V9_CopyReceiver.cs` (create in NT)

**Responsibilities**:
1. TCP CLIENT - connects to V9 app on startup
2. Listens for signals from V9
3. Routes orders to ALL connected accounts
4. Tracks positions per account
5. Sends position updates back to V9
6. Handles connection loss gracefully

**Key Features**:
- Multi-account order submission (one click → 20 orders)
- Per-account position tracking
- Per-account P&L display
- Heartbeat to V9 (keep connection alive)
- Auto-reconnect if connection drops

**Signal Reception Flow**:
```
NT starts → Connects to V9 as CLIENT
V9 sends: "LONG|MES|1"
NT receives → For each account:
  - Submit LONG order with 1 contract
  - Track position in that account
  - Send back: "POSITION|Account1|LONG|1"
V9 receives → Updates UI dashboard
```

---

## TCP Protocol Specification

### Connection Phase
```
Client (NT) → Server (V9): CONNECT|V9_CopyReceiver|Account_Count:20
Server (V9) → Client (NT): READY|Server_Version:1.0
```

### Signal Phase (V9 → NT)
```
Format: ACTION|SYMBOL|QUANTITY|[OPTIONS]

Examples:
- "LONG|MES|1" → Go long 1 MES on all accounts
- "SHORT|ES|2" → Go short 2 ES on all accounts
- "FLATTEN|ALL|0" → Close all positions on all accounts
- "CLOSE_HALF|MES|0" → Close half positions on all accounts
```

### Position Update Phase (NT → V9)
```
Format: POSITION|ACCOUNT|ACTION|QUANTITY|ENTRY_PRICE|[P&L]

Example:
- "POSITION|Account1|LONG|1|4520.5|+150.00"
- "POSITION|Account2|LONG|1|4520.5|+140.00"
- "POSITION_SUMMARY|20|LONG|20|4520.5|+2800.00" (aggregate)
```

### Heartbeat (keep-alive)
```
Every 5 seconds:
NT → V9: "HEARTBEAT|V9_CopyReceiver"
V9 → NT: "HEARTBEAT_ACK"

If no heartbeat for 30 seconds → disconnect and reconnect
```

---

## Implementation Tasks (V9_003 Agent)

### Phase 1: V9 Server Conversion
1. Refactor MainWindow.xaml.cs to act as TCP SERVER
2. Listen on port 5000 (instead of connecting to Hub)
3. Accept multiple client connections
4. Maintain list of connected clients
5. Implement broadcast method (send to all clients)

### Phase 2: V9_CopyReceiver Strategy (NT)
1. Create new NinjaScript strategy file
2. Implement TCP CLIENT connection logic
3. Add signal reception handler
4. Add multi-account order routing
5. Add position tracking per account
6. Add P&L aggregation
7. Add heartbeat/reconnect logic

### Phase 3: Integration & Testing
1. Test V9 app as SERVER
2. Test NT strategy as CLIENT
3. Test signal broadcast (1 click = 20 orders)
4. Test position tracking
5. Test connection recovery
6. Test with all 20 accounts

### Phase 4: UI Enhancement
1. Add "Connected Accounts" display in V9
2. Add "Total P&L" dashboard
3. Add per-account P&L breakdown
4. Add connection status indicator
5. Add signal history log

---

## Dependencies & Blockers

**Dependencies**:
1. V9_001 PASSES (TOS RTD working)
2. V9_004 UI controls ready (buttons functional)

**Blockers**:
- None (can start after V9_001 passes)

**Critical Success Factors**:
- Reliable TCP connection management
- Simultaneous order execution across 20 accounts
- Position tracking accuracy
- Connection recovery without data loss

---

## Files to Create/Modify

**Modify**:
- `V9_ExternalRemote/MainWindow.xaml.cs` - Convert to SERVER mode
- `V9_ExternalRemote/MainWindow.xaml` - Add connection status, P&L display

**Create**:
- `V9_CopyReceiver.cs` - New NinjaTrader strategy
- `.agent/SHARED_CONTEXT/V9_COPY_TRADING_STATUS.json` - Track progress
- `DEVELOPMENT/V9_WIP/COPY_TRADING/V9_CopyReceiver.cs` - Working copy

---

## Success Criteria

- [x] Architecture documented
- [ ] V9 acts as TCP SERVER
- [ ] NT acts as TCP CLIENT
- [ ] One signal = 20 simultaneous orders
- [ ] Positions tracked per account
- [ ] P&L aggregated correctly
- [ ] Connection survives restarts
- [ ] All 20 accounts execute simultaneously

---

## Timeline

**Monday 6 PM EST (Market Opens)**:
- V9_001 tests TOS RTD

**If V9_001 PASSES**:
- V9_003 Agent begins SERVER/CLIENT implementation
- Target: 2-3 hours for Phase 1-2
- Target: 1 hour for Phase 3 testing

**If V9_001 FAILS**:
- V9_002 debugs TOS RTD
- V9_003 waits for V9_001 to pass before starting
