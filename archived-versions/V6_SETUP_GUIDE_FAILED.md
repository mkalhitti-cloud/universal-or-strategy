# V6 Master/Slave Multi-Account Setup Guide

## Overview

The V6 Master/Slave architecture allows you to trade multiple accounts simultaneously with a single set of hotkeys. The **Master** calculates the Opening Range and generates trade signals, while **Slaves** execute orders on their assigned accounts independently.

## Architecture

```
┌─────────────────────────┐
│  UniversalORMasterV6    │  ← Your control panel (L/S/F hotkeys)
│  (Calculates OR)        │
│  (NO order submission)  │
└───────────┬─────────────┘
            │
            │ Broadcasts signals via SignalBroadcaster
            │
    ┌───────┴───────┬───────────┬───────────┬───────────┐
    │               │           │           │           │
    ▼               ▼           ▼           ▼           ▼
┌─────────┐   ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐
│ Slave 1 │   │ Slave 2 │ │ Slave 3 │ │ Slave 4 │ │ Slave 5 │
│ Apex1   │   │ Apex2   │ │ Apex3   │ │ Apex4   │ │ Apex5   │
└─────────┘   └─────────┘ └─────────┘ └─────────┘ └─────────┘
```

## Files Created

- **SignalBroadcaster.cs** - Event system for Master→Slave communication
- **UniversalORMasterV6.cs** - Master strategy (calculates OR, broadcasts signals)
- **UniversalORSlaveV6.cs** - Slave strategy (executes orders on assigned account)

## Step-by-Step Setup

### Step 1: Compile the Strategies

1. Open NinjaTrader 8
2. Go to **Tools → NinjaScript Editor**
3. In the editor, go to **Tools → Compile**
4. Verify no compilation errors
5. If successful, you'll see "Compiled successfully" message

> **Note:** All three files (SignalBroadcaster, Master, Slave) must compile together.

---

### Step 2: Configure Your Accounts

Ensure all your APEX accounts are connected in NinjaTrader:

1. Go to **Control Center → Connections**
2. Verify each Rithmic connection is **Connected** (green)
3. Note the exact account names (e.g., "Apex123456")

---

### Step 3: Load the Master Strategy

1. Open a chart for your instrument (e.g., MES 03-25)
2. Right-click chart → **Strategies**
3. Click **Add** → Select **UniversalORMasterV6**
4. Configure settings:
   - **Session Start**: 09:30 (for NY open)
   - **Session End**: 16:00
   - **OR Timeframe**: Minutes_5
   - **Time Zone**: Eastern
   - **Stop Multiplier**: 0.5
   - **Target 1 Multiplier**: 0.25
   - **Target 2 Multiplier**: 0.5
   - **RMA Enabled**: True
5. Click **OK** to enable

**Expected Output in Output window:**
```
UniversalORMasterV6 | MES | Tick: 0.25 | PV: $5
Session: 09:30 - 16:00 Eastern | OR: 5 min
MASTER MODE: Signals will be broadcast to slave strategies
MASTER REALTIME - Hotkeys: L=Long, S=Short, Shift+Click=RMA, F=Flatten
Subscribers: Trade=0, Trail=0, Target=0, Flatten=0, BE=0
```

> **Note:** Subscriber count is 0 because no slaves are loaded yet.

---

### Step 4: Load Slave Strategies (One Per Account)

For **EACH** account you want to trade:

1. Open a **NEW chart** for the same instrument (MES 03-25)
2. Right-click chart → **Strategies**
3. Click **Add** → Select **UniversalORSlaveV6**
4. Configure settings:
   - **Assigned Account Name**: "Apex123456" (EXACT account name)
   - **Risk Per Trade**: $200
   - **Reduced Risk Per Trade**: $200
   - **Stop Threshold Points**: 5.0
   - **Daily Loss Limit**: $500
   - **MES Min Contracts**: 1
   - **MES Max Contracts**: 30
   - **BE Trigger Points**: 2.0
   - **Trail 1 Trigger Points**: 3.0
   - **Trail 2 Trigger Points**: 4.0
   - **Trail 3 Trigger Points**: 5.0
5. Click **OK** to enable

**Expected Output for each slave:**
```
UniversalORSlaveV6 | Assigned Account: Apex123456 | MES | Tick: 0.25 | PV: $5
Risk: $200 | Daily Loss Limit: $500
SLAVE MODE: Listening for signals from Master
Account validated: Apex123456 | Connection: Connected
SLAVE REALTIME: Subscribed to Master signals | Account: Apex123456
Subscribers: Trade=1, Trail=1, Target=1, Flatten=1, BE=1
```

> **CRITICAL:** Each slave MUST have a unique account name. Do NOT assign two slaves to the same account.

---

### Step 5: Verify Setup

After loading 1 Master + 5 Slaves, check the Master's output window:

```
Subscribers: Trade=5, Trail=5, Target=5, Flatten=5, BE=5
```

This confirms all 5 slaves are listening for signals.

---

## Testing the System

### Test 1: Signal Broadcasting (Sim Mode)

1. Wait for OR to complete (5 minutes after 9:30 AM ET)
2. On the **Master chart**, press `L` key
3. Check Output window for all strategies

**Expected:**
- **Master:** `MASTER BROADCAST: ORLong @ 5025.00 | Stop: 5020.00 | T1: 5026.25 | T2: 5027.50`
- **Slave 1:** `SLAVE RECEIVED: ORLong_093500 | Long @ 5025.00`
- **Slave 1:** `ENTRY ORDER SUBMITTED: 3 @ 5025.00 | Account: Apex1`
- **Slave 2:** `SLAVE RECEIVED: ORLong_093500 | Long @ 5025.00`
- **Slave 2:** `ENTRY ORDER SUBMITTED: 3 @ 5025.00 | Account: Apex2`
- (Same for Slaves 3, 4, 5)

4. Check **Orders** tab in Control Center
   - Should see 5 entry orders (one per account)

---

### Test 2: Entry Fill & Bracket Submission

1. Allow entry orders to fill
2. Check Output window

**Expected for each slave:**
```
ENTRY FILLED: ORLong_093500 | 3 @ 5025.25 | Account: Apex1
BRACKET ORDERS SUBMITTED: Stop @ 5020.00 | T1: 1@5026.50 | T2: 1@5027.75
```

3. Check **Orders** tab
   - Each account should have: 1 stop order, 1 T1 order, 1 T2 order

---

### Test 3: Flatten All

1. On the **Master chart**, press `F` key
2. Check Output window

**Expected:**
- **Master:** `MASTER BROADCAST: Flatten all positions`
- **All Slaves:** `FLATTEN SIGNAL RECEIVED: Master flatten command`
- **All Slaves:** `FLATTEN: ORLong_093500 | 3 contracts`

3. Verify all positions closed in all accounts

---

### Test 4: Failure Isolation

1. Disconnect one slave's account (e.g., Apex3)
   - Go to **Control Center → Connections**
   - Right-click Apex3's connection → **Disconnect**

2. Press `L` on Master chart

**Expected:**
- **Slave 3:** `BLOCKED: Account disconnected - signal ignored`
- **Slaves 1, 2, 4, 5:** Orders submit normally

3. Reconnect Apex3
4. Verify Slave 3 can receive next signal

---

## Daily Workflow

### Morning Setup (Before Market Open)

1. Ensure all APEX accounts connected
2. Load Master strategy on primary chart
3. Load 5 Slave strategies (one per account)
4. Verify subscriber counts: `Subscribers: Trade=5, ...`
5. Wait for OR to complete

### During Trading

- **Use Master chart ONLY for hotkeys**
- Press `L` for long, `S` for short, `F` to flatten
- Monitor slave Output windows for fills
- Check Orders tab to verify all accounts executing

### End of Day

1. Press `F` on Master to flatten all accounts
2. Disable all strategies
3. Review P&L per account in Account Performance

---

## Troubleshooting

### "Account 'Apex123' not found"

**Problem:** Slave can't find the assigned account.

**Solution:**
1. Check exact account name in Control Center
2. Update Slave's "Assigned Account Name" property (case-sensitive)
3. Re-enable slave strategy

---

### "Subscribers: Trade=0"

**Problem:** Slaves not receiving signals.

**Solution:**
1. Ensure slaves are enabled AFTER master
2. Check slave Output for "SLAVE REALTIME: Subscribed to Master signals"
3. If missing, disable and re-enable slave

---

### "CRITICAL: Stop order failed - EMERGENCY FLATTEN"

**Problem:** Bracket order submission failed after entry fill.

**Solution:**
1. This is EXPECTED behavior - slave auto-flattened to protect position
2. Check Rithmic connection for that account
3. Review Output window for error details
4. If recurring, check account margin/permissions

---

### One Slave Not Executing

**Problem:** 4 slaves work, 1 doesn't.

**Solution:**
1. Check that slave's Output window for errors
2. Verify account connection status
3. Check daily loss limit not exceeded
4. Restart just that slave (disable/enable)

---

## Key Differences from V5

| Feature | V5 (Single Account) | V6 (Master/Slave) |
|---------|---------------------|-------------------|
| **Hotkeys** | On strategy chart | On Master chart only |
| **Order Submission** | Direct from strategy | Master→Signal→Slaves |
| **Failure Impact** | Entire strategy stops | Only affected slave stops |
| **Position Tracking** | Single dictionary | Per-slave dictionaries |
| **Setup Complexity** | 1 strategy instance | 1 master + N slaves |
| **Scalability** | 1 account | Unlimited accounts |

---

## Safety Features

### Emergency Flatten
If a slave's bracket order fails after entry fill, it immediately submits a market order to close the position.

### Daily Loss Limits
Each slave tracks its own P&L and stops trading if daily loss limit is reached.

### Connection Monitoring
Slaves check account connection status before executing signals.

### Error Isolation
If one slave crashes, others continue trading normally.

---

## Next Steps

1. **Test with 2 Sim accounts first**
2. **Verify all signals broadcast correctly**
3. **Test failure scenarios (disconnect, etc.)**
4. **Graduate to 3-5 APEX accounts**
5. **Monitor for 1 week before scaling to 10+ accounts**

---

## Support

If you encounter issues:
1. Check Output window for all strategies
2. Review this guide's Troubleshooting section
3. Verify account connections in Control Center
4. Test with Sim accounts before live trading
