# Mission Brief: Build 952 Modular Pruning (Final Approved)

## Context

Build 951.5 has accumulated significant complexity from modules that are no longer part of the active trading strategy: a TOS/IPC bridge (V9.1.8), three entry modules (MOMO, FFMA, OR), and a manual execution UI row. The goal is to surgically remove all of this "ghost" logic and reduce the strategy to 100% NT8-native RMA/Retest/Trend-only execution. The archived state is preserved in `feature/v12-legacy-archive`; all work targets a new branch `build/952-modular-pruning`.

## Execution Steps

### 1. Delete Files Outright

- `src/V12_002.UI.IPC.cs` (IPC Server)
- `src/V12_002.Entries.MOMO.cs` (MOMO logic)
- `src/V12_002.Entries.FFMA.cs` (FFMA logic)
- `src/V12_002.Entries.OR.cs` (OR entry execution - **OR box display is KEPT**)
- `validate_output.txt` (Repo root)

### 2. Core Strategy Edits (`src/V12_002.cs`)

- **Usings**: Remove `System.Net` and `System.Net.Sockets`.
- **Fields (IPC)**: Remove entire `// V9.1.8 IPC Integration` block (lines 191-199). Keep `stateLock`.
- **Fields (Entries)**: Remove MOMO/FFMA/ToS-Sync fields (lines 144-157). Keep `isLongArmed`/`isShortArmed`.
- **Audit Field**: Remove `IsMOMOTrade` / `IsFFMATrade` from `PositionInfo`.
- **Indis**: Remove `rsiIndicator` field and initialization.
- **OnStateChange**: Remove MOMO/FFMA defaults. Remove IPC queue initialization.
- **DataLoaded**: Remove `StartIpcServer()`. Remove FFMA print.
- **OR Print**: Update OR Complete print to remove `CalculateORStopDistance()` dependency.
- **OnBarUpdate**: Remove FFMA condition check.
- **Terminated**: Remove `StopIpcServer()`.
- **BUILD_TAG**: Update to `"952.0"`.

### 3. Properties Edits (`src/V12_002.Properties.cs`)

- **Groups**: Delete Group 10 (MOMO) and Group 11 (FFMA) entirely.
- **SIMA**: Remove `IpcPort` and `IpcExposeSensitiveFleetIdentity`.

### 4. Callback Edits (`src/V12_002.UI.Callbacks.cs`)

- **Usings**: Remove `System.Net` usings.
- **Buttons**: Remove OR entry button click subscriptions and lambdas.

### 5. Panel Edits (`src/V12_001.cs`)

- **UI Row**: Delete the Grid `row3` block (manualEntryRow) containing dropdown, price input, and [SUBMIT].
- **Submit_Click**: Delete the entire handler method.

## Key Safety Constraints

- **OR Box display is PRESERVED**: `sessionHigh`, `sessionLow`, `sessionMid`, `sessionRange`, `orComplete`, `DrawORBox()`, `ResetOR()` remain. The OR session-range data is still the visual reference anchor for RMA/Retest/Trend.
- **isInORWindow removal + safe refactor**: Replace the `if (!isInORWindow)` guard with `if (orStartDateTime == DateTime.MinValue)` (already the OR-not-yet-started sentinel). Remove `isInORWindow` true/false assignments and its field declaration.
- **SIMA core intact**: `EnableSIMA`, `REAPER`, and fleet dispatch logic remain untouched.

## Verification

1. **Compilation**: 0 errors in NT8.
2. **Dead-code scan**: Grep for removed symbols -- must return 0 hits.
3. **Visual Check**: OR Box must still draw; Row 3 must be gone.
