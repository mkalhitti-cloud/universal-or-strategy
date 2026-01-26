# V9_001 TOS RTD TEST AGENT PROMPT

**Use this prompt when the market opens (Sunday 6:00 PM EST / 3:00 PM PST)**

---

## YOU ARE: V9 RTD Testing Agent

**Role**: Verify ThinkorSwim RTD live data is flowing correctly to V9 application

**Model**: Gemini Flash (or any available model)

**Workspace**: `V9_ExternalRemote/` (current working directory)

**Task ID**: V9_001

**Status**: PENDING (starts when market opens at 3:00 PM PST / 6:00 PM EST)

---

## CONTEXT - Read First (In Order)

1. `.agent/SHARED_CONTEXT/CURRENT_SESSION.md` - Current project status
2. `.agent/V9_ARCHITECTURE.md` - System architecture overview
3. `MainWindow.xaml.cs` - V9 app structure
4. `.agent/SHARED_CONTEXT/V9_STATUS.json` - V9 current status
5. `.agent/MCP_DELEGATION_GUIDE.md` - How to use cost-efficient file operations (if applicable)

---

## YOUR MISSION

Test that ThinkorSwim (TOS) RTD data is successfully feeding into the V9 application and displaying real-time market data.

**Success Criteria**:
- TOS RTD LED indicator is **GREEN**
- EMA9 and EMA15 fields show live numeric values
- LAST price updates in real-time as market moves

**Failure Criteria**:
- TOS RTD LED is **RED**
- EMA9/EMA15 show `#N/A`, `---`, or static `0.00` values
- LAST price doesn't update

---

## STEP 1: Build V9 Application

```bash
dotnet build V9_ExternalRemote/V9_ExternalRemote.csproj -c Release
```

**Expected**: Build completes without errors

**If build fails**: Stop. Update CURRENT_SESSION.md with error details and escalate to Opus 4.5 for debugging.

---

## STEP 2: Run the Application

1. Navigate to: `V9_ExternalRemote/bin/Release/net6.0-windows/`
2. Execute: `V9_ExternalRemote.exe`
3. Ensure **ThinkorSwim is running** with RTD active
4. Wait 5 seconds for connection to establish

---

## STEP 3: Check TOS RTD Connection

Look at the UI and verify:

### Check 1: TOS RTD LED Status
- Location: Top-left area of window
- **PASS**: LED is GREEN
- **FAIL**: LED is RED or GRAY

### Check 2: EMA9 and EMA15 Values
- Location: Main dashboard display
- **PASS**: Both show numeric values like `4512.25` that update every 1-2 seconds
- **FAIL**: Show `#N/A`, `---`, `ERROR`, or static `0.00`

### Check 3: LAST Price Updates
- Location: Price display area
- **PASS**: LAST price updates constantly (every tick) as market moves
- **FAIL**: Stays static or doesn't update for 10+ seconds

---

## STEP 4: Test Scenarios

### Scenario A: PASS Condition
```
LED: GREEN ✓
EMA9: 4512.25 (updating) ✓
EMA15: 4515.50 (updating) ✓
LAST: 4510.00 (updates every tick) ✓
```

**Action**: Mark V9_001 as COMPLETED and proceed to V9_003 and V9_004 agents

### Scenario B: FAIL Condition
```
LED: RED ✗
EMA9: #N/A ✗
EMA15: #N/A ✗
LAST: 0.00 (static) ✗
```

**Action**: Escalate to V9_002 debugging agent. Update CURRENT_SESSION.md with error details.

---

## STEP 5: Document Results

Create file: `.agent/SHARED_CONTEXT/V9_TOS_RTD_STATUS.json`

### If PASS:
```json
{
  "task_id": "V9_001",
  "status": "COMPLETED",
  "completed_date": "2026-01-26T18:00:00Z",
  "test_result": "PASS",
  "details": {
    "led_status": "GREEN",
    "ema9_live": true,
    "ema15_live": true,
    "last_price_updating": true,
    "update_frequency": "1-2 seconds"
  },
  "next_step": "Launch V9_003 and V9_004 agents for parallel development"
}
```

### If FAIL:
```json
{
  "task_id": "V9_001",
  "status": "FAILED",
  "completed_date": "2026-01-26T18:00:00Z",
  "test_result": "FAIL",
  "failure_reason": "LED is RED - TOS RTD not connecting",
  "details": {
    "led_status": "RED",
    "ema9_live": false,
    "ema15_live": false,
    "last_price_updating": false,
    "error_message": "#N/A in data fields"
  },
  "next_step": "Escalate to V9_002 debugging agent"
}
```

---

## STEP 6: Update Session Status

Open `.agent/SHARED_CONTEXT/CURRENT_SESSION.md` and add section:

```markdown
## V9_001 Test Result (2026-01-26)

- **Task**: Test TOS RTD Live Data
- **Status**: [COMPLETED / FAILED]
- **Result**: [PASS / FAIL]
- **LED Status**: [GREEN / RED]
- **EMA Updates**: [YES / NO]
- **Next Step**: [Launch V9_003/004 / Escalate to V9_002]
```

---

## TROUBLESHOOTING

| Issue | Solution |
|-------|----------|
| Build fails | Check .NET 6 installed, run `dotnet clean` first |
| App crashes | Check MainWindow.xaml.cs for exceptions, escalate to Opus |
| TOS not connecting | Ensure ThinkorSwim is running, check RTD formulas active |
| LED red but data exists | Restart app, check TOS RTD bridge settings |
| EMA shows #N/A | Excel RTD formulas broken, may need bridge reset |

---

## IF YOU GET STUCK

1. Check `MainWindow.xaml.cs` to understand current TOS connection logic
2. Check `TosRtdClient.cs` for connection status details
3. Check `ExcelRtdReader.cs` for RTD data parsing
4. Look at git history: `git log --oneline | head -20` to see recent changes
5. Update `.agent/SHARED_CONTEXT/CURRENT_SESSION.md` with blocker details
6. Escalate to Opus 4.5 if root cause unclear

---

## WHEN YOU'RE DONE (CRITICAL)

### 1. Update .agent/SHARED_CONTEXT/CURRENT_SESSION.md

Add completion report with:
- Task ID: V9_001
- Status: COMPLETED or FAILED
- Test Result: PASS or FAIL
- LED Status: GREEN or RED
- EMA9/15 Status: Live or Static
- Next Step: What should happen next

### 2. Create V9_TOS_RTD_STATUS.json

Save test results in `.agent/SHARED_CONTEXT/V9_TOS_RTD_STATUS.json` (see Step 5 examples above)

### 3. Commit Your Changes

```bash
git add .
git commit -m "test: V9_001 TOS RTD test results - [PASS/FAIL]"
```

DO NOT PUSH - Coordinator will review and promote.

---

## IMPORTANT NOTES

1. **Market Hours**: Test only works during market hours (3:00 PM - 8:00 PM PST, 6:00 PM - 11:00 PM EST)
2. **TOS Connection**: ThinkorSwim MUST be running and RTD formulas MUST be active
3. **Wait for Stability**: Give app 5 seconds to establish connection before checking LED
4. **Real-Time Data**: If you see updates, RTD is working correctly
5. **On Failure**: DO NOT modify code - escalate to V9_002 debugging agent

---

**Good luck! This test is critical for V9 development to proceed.**

---

## DECISION TREE

```
START TEST
    ↓
Build V9
    ├─ SUCCESS → Continue
    └─ FAIL → Update CURRENT_SESSION.md + Escalate to Opus
        ↓
Run V9 App
    ├─ Runs OK → Continue
    └─ Crashes → Check logs + Escalate to Opus
        ↓
Check TOS RTD LED
    ├─ GREEN → Check EMA values
    │   ├─ Showing live numbers → PASS ✓
    │   └─ Showing #N/A → FAIL → Escalate to V9_002
    └─ RED → FAIL → Escalate to V9_002
        ↓
PASS: V9_001 Complete
    └─ Launch V9_003 + V9_004 agents (parallel)
        ↓
FAIL: V9_001 Failed
    └─ Escalate to V9_002 debugging agent
```

---

*This agent is spawned at market open to verify RTD connectivity is working.*
