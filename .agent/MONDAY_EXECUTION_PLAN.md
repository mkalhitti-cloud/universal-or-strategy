# MONDAY EXECUTION PLAN - V9 Testing & Development

**Date**: Monday, January 27, 2026
**Market Opens**: 6 PM EST / 3 PM PST
**Coordinator**: Antigravity (Gemini Flash / Opus)

---

## TIMELINE

### 5:55 PM EST (5 minutes before market open)
- [ ] Open Antigravity
- [ ] Review `.agent/SHARED_CONTEXT/CURRENT_SESSION.md`
- [ ] Have all prompts ready to paste

### 6:00 PM EST (MARKET OPENS - T+0)
**SPAWN V9_001 AGENT (TOS RTD Test)**

Paste this prompt into new Antigravity chat:
```
You are V9_001 Agent - Test TOS RTD Live Numbers

READ FIRST:
1. .agent/SHARED_CONTEXT/CURRENT_SESSION.md
2. .agent/V9_STATUS.json
3. V9_ExternalRemote/MainWindow.xaml.cs

TASK: Test if V9 TOS RTD is working

STEPS:
1. Build V9_ExternalRemote project: dotnet build V9_ExternalRemote/V9_ExternalRemote.csproj -c Release
2. Run: V9_ExternalRemote/bin/Release/net6.0-windows/V9_Milestone_FINAL.exe
3. Check if TOS RTD LED turns green (live data connected)
4. Check if EMA9/EMA15 show live numbers (not --- or #N/A)
5. Check if LAST price updates in real-time
6. Report PASS or FAIL with details

EXPECTED RESULT if PASS:
- TOS RTD LED: GREEN
- EMA9: Shows live number (e.g., 4520.50)
- EMA15: Shows live number (e.g., 4521.00)
- LAST: Updates every tick
- Status: "TOS RTD WORKING"

EXPECTED RESULT if FAIL:
- TOS RTD LED: RED
- EMA9: Shows --- or #N/A
- Values not updating
- Report what's broken

When done, update .agent/SHARED_CONTEXT/CURRENT_SESSION.md with result.
```

### 6:05 PM - 6:30 PM EST (V9_001 testing window)
- V9_001 runs TOS RTD test
- Coordinator monitors progress
- If issues appear, note them for V9_002

### 6:30 PM EST (RESULT DECISION POINT)

**If V9_001 PASSES** ✅
→ Go to "PARALLEL DEVELOPMENT PHASE" (see below)

**If V9_001 FAILS** ❌
→ Go to "DEBUGGING PHASE" (see below)

---

## SCENARIO A: V9_001 PASSES ✅

### 6:30 PM EST - Spawn V9_003 & V9_004 Agents (PARALLEL)

**SPAWN V9_003 AGENT (Copy Trading)**

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

**SPAWN V9_004 AGENT (WPF UI)**

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
You are V9_002 Agent - TOS RTD Debugging

READ FIRST:
1. .agent/V9_STATUS.json
2. V9_ExternalRemote/TosRtdClient.cs (RTD connection code)
3. V9_ExternalRemote/ExcelRtdReader.cs (Excel bridge code)
4. TOS_RTD_Bridge.xlsx (the Excel file with RTD formulas)

WORKSPACE: DEVELOPMENT/V9_WIP/TOS_RTD_BRIDGE/

TASK: Fix TOS RTD connection

DEBUG CHECKLIST:
1. Is ThinkorSwim running and RTD enabled?
2. Is TOS_RTD_Bridge.xlsx open in Excel?
3. Are RTD formulas in Excel calculating (not showing #N/A)?
4. Can V9 app read Excel cells (check ExcelRtdReader)?
5. Is the RTD server responding (check tos.rtd COM object)?

STEPS:
1. Check Excel: Open TOS_RTD_Bridge.xlsx
   - Verify RTD formulas in CUSTOM4, CUSTOM6 cells
   - Check if they show numbers (not #N/A)
   - If #N/A → TOS RTD not responding → restart TOS

2. Check V9 code:
   - Trace ExcelRtdReader initialization
   - Add detailed logging
   - Verify Excel file path is correct
   - Test cell reads

3. Check TOS:
   - Restart ThinkorSwim
   - Verify RTD server is active
   - Test Excel → TOS connection first

4. Test again:
   - Run V9 app
   - Check TOS RTD LED
   - Check if numbers appear

When you fix it: Create detailed bug report
```

### 6:30 PM - Until Fixed
- V9_002 debugs TOS RTD issue
- Keep trying, document findings
- Once fixed: restart V9_001 test
- Report success back to coordinator

### Once V9_002 Fixes It
- Spawn V9_003 and V9_004 (same as Scenario A)
- Continue with parallel development

---

## COORDINATOR RESPONSIBILITIES

**Throughout the night**:

1. **Monitor V9_001 Test** (6:00-6:30 PM)
   - Watch for TOS RTD LED status
   - Check if EMA values appear
   - Record PASS or FAIL

2. **If PASS** (6:30 PM onward)
   - Spawn V9_003 (Copy Trading)
   - Spawn V9_004 (UI)
   - Monitor both in parallel
   - Update CURRENT_SESSION.md every 30 min

3. **If FAIL** (6:30 PM onward)
   - Spawn V9_002 (Debugging)
   - Let V9_002 investigate
   - Once fixed, spawn V9_003 & V9_004

4. **Update Status Files**
   - `.agent/SHARED_CONTEXT/CURRENT_SESSION.md`
   - `.agent/SHARED_CONTEXT/V9_STATUS.json`
   - `.agent/SHARED_CONTEXT/V9_COPY_TRADING_STATUS.json` (if V9_003 runs)

5. **Coordinate Communication**
   - Keep all agents updated on progress
   - Share blockers immediately
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

1. Update `.agent/SHARED_CONTEXT/CURRENT_SESSION.md` with:
   - What agent was working on what
   - Current status
   - What comes next
   - Any blockers

2. Commit to git: `git commit -m "WIP: V9 development in progress"`

3. When resuming: Read CURRENT_SESSION.md first

**If multiple agents need to work simultaneously:**
- Each gets their own Antigravity chat
- All read from shared context files
- Updates are automatically visible to all

---

## IMPORTANT REMINDERS

1. **V8_22 is protected** - don't modify production
2. **Test before deploy** - all V9 work is in DEVELOPMENT/
3. **Monitor TOS RTD** - this is the critical path
4. **Document everything** - every failure helps V9_002
5. **Stay coordinated** - update CURRENT_SESSION.md frequently

---

## GOOD LUCK!

Market opens in approximately **6 PM EST Monday**.

The infrastructure is ready. The prompts are prepared. The architecture is documented.

When market opens, trigger V9_001 and we'll know within 30 minutes if V9 is working.

You've got this. 🚀
