# Role: V9_002 Agent - Debug TOS RTD Connection

## Context
You are a debugging expert specializing in real-time data (RTD) connections between ThinkorSwim (TOS) and C# applications via Excel.

### Context Files to Read:
- `V9_STATUS.json` (System status)
- `TosRtdClient.cs` (C# client for RTD)
- `ExcelRtdReader.cs` (Excel bridge reader)
- `TOS_RTD_Bridge.xlsx` (Excel bridge containing formulas)

## Workspace
`DEVELOPMENT/V9_WIP/TOS_RTD_BRIDGE/`

## Task
Fix the TOS RTD connection if the `V9_001` test agent fails (i.e., data is not flowing or is invalid). This agent starts automatically if `V9_001` reports a failure.

## Debug Checklist
1. **ThinkorSwim (TOS)**:
   - Is TOS running?
   - Is RTD enabled in TOS settings?
2. **Excel Bridge**:
   - Is `TOS_RTD_Bridge.xlsx` open in Excel?
   - Are the RTD formulas calculating, or do they show `#N/A`? (Check `LAST`, `BID`, `ASK`).
3. **Connectivity**:
   - Can the V9 application read cells from the Excel file?
   - Is the COM object `tos.rtd` responding to requests?
4. **Symbol Validation**:
   - Are the symbols correctly formatted for RTD (e.g., `/MES:CME`, `/MGC:COMEX`)?

## Troubleshooting Steps
- **Path Verification**: Ensure the Excel file path in the C# code matches the actual location of `TOS_RTD_Bridge.xlsx`.
- **Formula Check**: Verify that RTD formulas are correctly placed in `CUSTOM4` and `CUSTOM6` columns in TOS if those are being used.
- **Restart Procedure**: Restart ThinkorSwim if the `tos.rtd` COM object remains unresponsive.
- **Logging**: Add detailed logging to `TosRtdClient.cs` to capture specific COM exceptions or data timeouts.
- **Iterative Testing**: Re-run the `V9_001` test agent or a local connection test after each fix attempt.

## Deliverables
- **Bug Report**: A detailed summary of what was causing the connection failure.
- **Fixed Code**: Compliable C# code or updated Excel bridge configuration.

---

## WHEN YOU'RE DONE (CRITICAL)

Before closing this conversation, you MUST do these three things:

### 1. Update .agent/SHARED_CONTEXT/CURRENT_SESSION.md

Add a section with your completion report:
- Task ID and name
- Status (COMPLETED, BLOCKED, IN_PROGRESS)
- Results or findings
- Any blockers or issues
- What should happen next

### 2. Update Your Task Status File

Create/update the appropriate JSON file:
- V9_001: .agent/SHARED_CONTEXT/V9_TOS_RTD_STATUS.json
- V9_002: .agent/SHARED_CONTEXT/V9_TOS_RTD_STATUS.json (same file, update with debug results)
- V9_003: .agent/SHARED_CONTEXT/V9_COPY_TRADING_STATUS.json
- V9_004: .agent/SHARED_CONTEXT/V9_WPF_UI_STATUS.json

Include:
- task_id
- status (COMPLETED, BLOCKED, IN_PROGRESS)
- completed_date (ISO8601 timestamp)
- results (specific outcomes)
- blockers (any issues)
- next_steps (what comes next)

### 3. Commit Your Changes

If code was modified:
```bash
git add .
git commit -m "feat: [Task name] - [status]"
```
