# Role: V9_001 Agent - Test TOS RTD Live Numbers

## Context
You are a testing agent responsible for verifying the real-time data (RTD) flow from ThinkorSwim (TOS) to the V9 External Remote application. This test is critical to ensuring the reliability of the V9 architecture before moving further into development.

### Required Context Files
Before starting, read these files to understand the current project state:
1. [.agent/SHARED_CONTEXT/CURRENT_SESSION.md](file:///c:/Users/Mohammed%20Khalid/OneDrive/Desktop/WSGTA/Github/universal-or-strategy/.agent/SHARED_CONTEXT/CURRENT_SESSION.md)
2. [.agent/SHARED_CONTEXT/V9_STATUS.json](file:///c:/Users/Mohammed%20Khalid/OneDrive/Desktop/WSGTA/Github/universal-or-strategy/.agent/SHARED_CONTEXT/V9_STATUS.json)
3. [.agent/V9_ARCHITECTURE.md](file:///c:/Users/Mohammed%20Khalid/OneDrive/Desktop/WSGTA/Github/universal-or-strategy/.agent/V9_ARCHITECTURE.md)

## Timing
> [!IMPORTANT]
> This agent should be executed when the market opens (Monday 6:00 PM EST / 3:00 PM PST) to ensure live data is streaming through RTD.

## Task: Verify TOS RTD Live Data Flow
Your goal is to build and run the `V9_ExternalRemote` project and verify that live market data is successfully populating the dashboard.

### Step-by-Step Testing Procedure

1.  **Build the Project**:
    - Open the terminal in the root directory.
    - Run the build command:
      ```powershell
      dotnet build V9_ExternalRemote/V9_ExternalRemote.csproj -c Release
      ```

2.  **Run the Executable**:
    - Navigate to the output directory: `V9_ExternalRemote/bin/Release/net6.0-windows/`
    - Run `V9_ExternalRemote.exe`.
    - Ensure ThinkorSwim (TOS) is running and the RTD formulas are active (Excel bridge may be required depending on current V9 implementation).

3.  **Check Connection Status (LED)**:
    - Observe the "TOS RTD" LED indicator on the UI.
    - **PASS Criteria**: LED is **GREEN**.
    - **FAIL Criteria**: LED is **RED**.

4.  **Verify Indicator Updates**:
    - Check the **EMA9** and **EMA15** fields.
    - Verify they show numeric values that update as the market moves.

5.  **Verify Price Updates**:
    - Check the **LAST** price field.
    - Verify it updates in real-time at tick speed.

## Expected Results

### PASS Scenario
- LED is GREEN.
- EMA9/15 show live numbers (not zero, not #N/A).
- LAST price updates constantly.

### FAIL Scenario
- LED is RED.
- Values show `#N/A`, `---`, or remain static at `0.00`.
- Data does not update even with the market open.

## Deliverables
Upon completion of the test, update the following files with your results:
1.  **Update [CURRENT_SESSION.md](file:///c:/Users/Mohammed%20Khalid/OneDrive/Desktop/WSGTA/Github/universal-or-strategy/.agent/SHARED_CONTEXT/CURRENT_SESSION.md)**: Record the test outcome in the "Development Status (V9)" section.
2.  **Update [V9_STATUS.json](file:///c:/Users/Mohammed%20Khalid/OneDrive/Desktop/WSGTA/Github/universal-or-strategy/.agent/SHARED_CONTEXT/V9_STATUS.json)**: Update the `current_state` and `v9_candidates` status based on which version was tested and the result.
