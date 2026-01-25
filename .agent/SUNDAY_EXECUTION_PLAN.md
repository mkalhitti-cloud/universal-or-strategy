# SUNDAY EXECUTION PLAN - V9 Testing & Development

**Date**: Sunday, January 25, 2026 (Reflecting today's date for execution)
**Market Opens**: 6 PM EST / 3 PM PST
**Coordinator**: Antigravity (Gemini Flash / Opus)

---

## TIMELINE

### 5:55 PM EST (5 minutes before market open)
- [ ] Open Antigravity
- [ ] Review `.agent/SHARED_CONTEXT/CURRENT_SESSION.md`
- [ ] Have all prompts ready to paste

### 6:00 PM EST (MARKET OPENS - T+0)
**SPAWN V9_001, V9_003, & V9_004 AGENTS SIMULTANEOUSLY**

1. **SPAWN V9_001 AGENT (TOS RTD Test)**
Paste this prompt into new Antigravity chat:
```
You are V9_001 Agent - Test TOS RTD Live Numbers

READ FIRST:
1. .agent/SHARED_CONTEXT/CURRENT_SESSION.md
2. .agent/SHARED_CONTEXT/V9_STATUS.json
3. .agent/V9_ARCHITECTURE.md

TASK: Verify TOS RTD Live Data Flow
Your goal is to build and run the `V9_ExternalRemote` project and verify that live market data is successfully populating the dashboard.

STEPS:
1. Build the Project:
   dotnet build V9_ExternalRemote/V9_ExternalRemote.csproj -c Release
2. Run the Executable:
   Navigate to V9_ExternalRemote/bin/Release/net6.0-windows/ and run V9_ExternalRemote.exe.
3. Check Connection Status (LED):
   - PASS: LED is GREEN.
   - FAIL: LED is RED.
4. Verify Indicator Updates:
   Check EMA9 and EMA15 fields for live numeric values.
5. Verify Price Updates:
   Check LAST price field for real-time tick updates.

EXPECTED RESULT if PASS:
- LED is GREEN.
- EMA9/15 show live numbers (not zero, not #N/A).
- LAST price updates constantly.

When done, update .agent/SHARED_CONTEXT/CURRENT_SESSION.md and V9_STATUS.json with results.
```

2. **SPAWN V9_003 AGENT (Copy Trading)**
Paste this prompt into new Antigravity chat:
```
You are V9_003 Agent - Copy Trading Multi-Account System

READ FIRST:
1. .agent/V9_ARCHITECTURE.md (the complete design)
2. .agent/V9_TCP_PROTOCOL.md (message format spec)
3. .agent/PROMPTS/V9_003_COPY_TRADING_AGENT.md (your instructions)
4. V9_ExternalRemote/MainWindow.xaml.cs (current code)

WORKSPACE: DEVELOPMENT/V9_WIP/COPY_TRADING/

TASK: Implement multi-account trading orchestration

PHASES (do in order):
1. Convert V9 to TCP SERVER (listens on 5000)
2. Create V9_CopyReceiver NinjaTrader strategy
3. Test signal broadcast to all 20 accounts
4. Verify position tracking

Expected output:
- Modified V9_ExternalRemote (SERVER mode)
- New V9_CopyReceiver.cs (NT strategy)
- Test results showing all 20 accounts executing

When done: Create .agent/SHARED_CONTEXT/V9_COPY_TRADING_STATUS.json
```

3. **SPAWN V9_004 AGENT (WPF UI)**
Paste this prompt into new Antigravity chat:
```
You are V9_004 Agent - WPF UI Controls

READ FIRST:
1. .agent/V9_ARCHITECTURE.md (system design)
2. V9_ExternalRemote/MainWindow.xaml (current UI)
3. V9_ExternalRemote/MainWindow.xaml.cs (current code)

WORKSPACE: DEVELOPMENT/V9_WIP/WPF_UI/

TASK: Enhance WPF UI for external control

DO NOT WAIT for V9_003 - work in parallel

FEATURES:
1. Add "Connected Accounts" display (shows N/20)
2. Add "Total P&L" dashboard
3. Add per-account P&L table
4. Add connection status indicator
5. Add signal history log

Focus on UI responsiveness and readability.
Account routing logic is V9_003's job.

When done: Create comprehensive UI documentation
```

### 6:05 PM - 6:30 PM EST
- V9_001 runs TOS RTD test
- V9_003 & V9_004 work in parallel
- Coordinator monitors all three agents

### 6:30 PM EST (RESULT DECISION POINT)

**If V9_001 PASSES** ✅
→ Go to "PARALLEL DEVELOPMENT PHASE" (continue V9_003 & V9_004)

**If V9_001 FAILS** ❌
→ Go to "DEBUGGING PHASE" (Spawn V9_002)

---

## SCENARIO A: V9_001 PASSES ✅

### 6:30 PM - 8:30 PM EST (2-3 hours)
- V9_003 implements copy trading
- V9_004 enhances UI
- Both run INDEPENDENTLY
- Coordinator monitors both
- Update CURRENT_SESSION.md every 30 minutes

### 8:30 PM EST
- Both agents should have Phase 1-2 complete
- Begin integration testing
- One click in V9 = check if all 20 accounts execute

---

## SCENARIO B: V9_001 FAILS ❌

### 6:30 PM EST - Spawn V9_002 Agent (Debugging)

Paste this prompt into new Antigravity chat:
```
You are V9_002 Agent - Debug TOS RTD Connection

READ FIRST:
- V9_STATUS.json (System status)
- TosRtdClient.cs (C# client for RTD)
- ExcelRtdReader.cs (Excel bridge reader)
- TOS_RTD_Bridge.xlsx (Excel bridge containing formulas)

TASK: Fix the TOS RTD connection if the V9_001 test agent fails.

DEBUG CHECKLIST:
1. ThinkorSwim (TOS): Running? RTD enabled?
2. Excel Bridge: Bridge.xlsx open? RTD formulas calculating (not #N/A)?
3. Connectivity: Can app read Excel cells? Is COM object tos.rtd responding?
4. Symbol Validation: Are symbols correctly formatted (e.g., /MES:CME)?

TROUBLESHOOTING:
- Path Verification: Ensure Excel file path in C# code is correct.
- Formula Check: Verify RTD formulas in CUSTOM4/CUSTOM6 if used.
- Restart Procedure: Restart TOS if COM object is unresponsive.
- Logging: Add detailed logging to TosRtdClient.cs for COM exceptions.
- Iterative Testing: Re-run V9_001 after each fix attempt.

When fixed, create a detailed bug report and provide fixed code.
```

### 6:30 PM - Until Fixed
- V9_002 debugs TOS RTD issue
- V9_003 & V9_004 continue their work (if possible)
- Once fixed: restart V9_001 test
- Report success back to coordinator

---

## COORDINATOR RESPONSIBILITIES

**Throughout the night**:

1. **Monitor Agents** (6:00 PM onward)
   - Track V9_001 (LED status, EMA values)
   - Track V9_003 (TCP server, account routing)
   - Track V9_004 (UI dashboard progress)

2. **Decision Making**
   - If V9_001 fails, immediately spawn V9_002
   - If V9_003/004 hit a blocker, facilitate communication

3. **Update Status Files**
   - `.agent/SHARED_CONTEXT/CURRENT_SESSION.md`
   - `.agent/SHARED_CONTEXT/V9_STATUS.json`
   - `.agent/SHARED_CONTEXT/V9_COPY_TRADING_STATUS.json`

4. **Coordinate Communication**
   - Keep all agents updated on each other's progress
   - Escalate issues if needed

---

## SUCCESS METRICS

### V9_001 (TOS RTD Test)
- [ ] TOS RTD LED turns GREEN
- [ ] EMA9 shows live number
- [ ] EMA15 shows live number
- [ ] LAST price updates
- [ ] Status: PASS

### V9_003 (Copy Trading)
- [ ] V9 converts to TCP SERVER
- [ ] V9_CopyReceiver created and tested
- [ ] One signal = 20 simultaneous orders
- [ ] Positions tracked per account
- [ ] P&L aggregated correctly

### V9_004 (UI)
- [ ] Connected accounts display works
- [ ] Total P&L dashboard functional
- [ ] Per-account P&L table visible
- [ ] Connection status indicator present
- [ ] Signal history log updating

---

## FILES TO CHECK BEFORE MARKET OPEN

- [ ] V9_ExternalRemote builds successfully
- [ ] TOS_RTD_Bridge.xlsx exists and is in right location
- [ ] All agent prompts are ready to paste
- [ ] CURRENT_SESSION.md is up to date
- [ ] Antigravity IDE is open and ready
- [ ] Network connectivity is stable

---

## HANDOFF NOTES

**If you need to pause and resume later:**

1. Update `.agent/SHARED_CONTEXT/CURRENT_SESSION.md` with current status.
2. Commit to git: `git commit -m "WIP: V9 development in progress"`
3. When resuming: Read CURRENT_SESSION.md first.

---

## IMPORTANT REMINDERS

1. **V8_22 is protected** - don't modify production.
2. **Test before deploy** - all V9 work is in DEVELOPMENT/.
3. **Monitor TOS RTD** - this is the critical path.
4. **Document everything** - every failure helps V9_002.
5. **Stay coordinated** - update CURRENT_SESSION.md frequently.

---

## GOOD LUCK!

Market opens in approximately **6 PM EST Sunday**.

The infrastructure is ready. The prompts are prepared. The architecture is documented.

Trigger the simultaneous spawn at 6:00 PM and let's go. 🚀
