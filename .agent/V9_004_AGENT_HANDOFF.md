# V9_004 Agent Handoff Document
**Date**: 2026-01-25
**Status**: READY FOR IMPLEMENTATION
**Model**: Claude (Gemini Flash recommended for cost efficiency)
**Task**: WPF UI Enhancements - Phase 2 Implementation

---

## Quick Start

You are taking over the V9_004 WPF UI Agent task. Architecture planning is complete. You now implement.

**Read these 3 documents in order:**
1. `.agent/V9_004_QUICK_REFERENCE.md` (2 min) - TL;DR of all decisions
2. `.agent/V9_004_ARCHITECTURE_GUIDE.md` (15 min) - Full details with code examples
3. This document (5 min) - Your implementation roadmap

**Expected Duration**: ~2 hours for Phase 2
**Success Metric**: All files created, tests passing, V9_WPF_UI_STATUS.json written

---

## What You're Doing

### Phase 2: WPF UI Enhancements (Your Job)

**Goal**: Integrate real-time status file monitoring into the V9_ExternalRemote dashboard

The UI will now display:
- ✅ TOS RTD Connection Status (from V9_001)
- ✅ Copy Trading Status (from V9_003)
- ✅ WPF UI System Status (from V9_004 itself)

**All without blocking the UI thread** (async file polling every 2 seconds)

---

## Files You Need to Create

### Core Implementation (7 files)

**1. FilePathManager.cs** (Path Resolution)
```csharp
// Location: V9_ExternalRemote/FilePathManager.cs
// Purpose: Resolve paths to .agent/SHARED_CONTEXT/ JSON files
// Key Method: GetBasePath() - traverse from executable directory
// Code Template: In ARCHITECTURE_GUIDE.md lines 47-69
// Effort: 10 min
```

**2. StatusFileManager.cs** (JSON I/O)
```csharp
// Location: V9_ExternalRemote/StatusFileManager.cs
// Purpose: Read/write JSON files, auto-create mocks if missing
// Key Methods:
//   - ReadStatusFileAsync<T>() - async JSON read with mock fallback
//   - CreateMockFileAsync() - auto-create sensible defaults
// Code Template: In ARCHITECTURE_GUIDE.md lines 102-167
// Effort: 20 min
```

**3. StatusPoller.cs** (Polling Loop)
```csharp
// Location: V9_ExternalRemote/StatusPoller.cs
// Purpose: Poll status files every 2 seconds, update UI
// Key Methods:
//   - StartPolling() - begin background polling task
//   - PollStatusFilesAsync() - async loop with 2-second delay
//   - StopPolling() - graceful shutdown
// Code Template: In ARCHITECTURE_GUIDE.md lines 185-233
// Effort: 15 min
```

**4. CopyTradingTcpClient.cs** (Phase 3 Placeholder)
```csharp
// Location: V9_ExternalRemote/CopyTradingTcpClient.cs
// Purpose: Placeholder for Phase 3 TCP client implementation
// Key Methods: ConnectAsync(), ListenForSignalsAsync(), SendHeartbeatAsync()
// Note: Throw NotImplementedException for now
// Code Template: In ARCHITECTURE_GUIDE.md lines 249-309
// Effort: 10 min
```

### Model Classes (3 files)

**5. Models/TosRtdStatus.cs**
```csharp
// Location: V9_ExternalRemote/Models/TosRtdStatus.cs
// JSON Fields: IsConnected, LastPrice, Ema9, Ema15, LastUpdate, Symbol, Message
// Template: In ARCHITECTURE_GUIDE.md lines 342-354
// Effort: 5 min
```

**6. Models/CopyTradingStatus.cs**
```csharp
// Location: V9_ExternalRemote/Models/CopyTradingStatus.cs
// JSON Fields: IsActive, ConnectedAccounts, Message, LastUpdate, AccountsList
// Template: In ARCHITECTURE_GUIDE.md lines 356-366
// Effort: 5 min
```

**7. Models/WpfUiStatus.cs**
```csharp
// Location: V9_ExternalRemote/Models/WpfUiStatus.cs
// JSON Fields: Status, Message, LastUpdate, TosConnected, CopyTradingActive
// Template: In ARCHITECTURE_GUIDE.md lines 368-375
// Effort: 5 min
```

### Files to Modify (2 files)

**8. MainWindow.xaml.cs** (Update UI Logic)
```csharp
// Location: V9_ExternalRemote/MainWindow.xaml.cs (EXISTING FILE)
// Changes:
//   1. Add _poller field: private StatusPoller _poller;
//   2. In constructor: _poller = new StatusPoller(this); _poller.StartPolling();
//   3. Add UpdateFromStatusFiles() method to wire status updates
//   4. In OnClosed(): _poller?.StopPolling();
//   5. Add LED/message display elements for status
// Code Template: In ARCHITECTURE_GUIDE.md lines 233-246
// Effort: 20 min
```

**9. MainWindow.xaml** (Add UI Elements)
```xaml
<!-- Location: V9_ExternalRemote/MainWindow.xaml (EXISTING FILE) -->
<!-- Add to existing window:
  - Status LED Borders: TosStatusLed, CopyStatusLed, WpfStatusLed
  - Status Messages: TosMessageTxt, CopyMessageTxt, WpfMessageTxt
  - Colors: Green (connected), Orange (waiting), Red (error)
-->
<!-- Effort: 15 min -->
```

### Test Files (2 files - Optional but Recommended)

**10. Tests/FilePathManagerTests.cs**
```csharp
// Unit tests for FilePathManager path resolution
// Template: In ARCHITECTURE_GUIDE.md lines 444-451
// Effort: 15 min
```

**11. Tests/StatusPollerTests.cs**
```csharp
// Integration tests for polling + UI updates
// Template: In ARCHITECTURE_GUIDE.md lines 453-468
// Effort: 20 min
```

---

## Implementation Roadmap

### Step 1: Create Core Classes (50 min)
```
FilePathManager.cs       ✓ [████████]  10 min
StatusFileManager.cs     ✓ [████████]  20 min
StatusPoller.cs          ✓ [████████]  15 min
CopyTradingTcpClient.cs  ✓ [████████]  10 min
Models/ (3 files)        ✓ [████████]  15 min
─────────────────────────────────────────
Subtotal:                               70 min (with buffer)
```

### Step 2: Update MainWindow (35 min)
```
MainWindow.xaml.cs       ✓ [████████]  20 min
MainWindow.xaml          ✓ [████████]  15 min
─────────────────────────────────────────
Subtotal:                               35 min
```

### Step 3: Build & Test (30 min)
```
Build solution            ✓ [████████]   5 min
Fix compiler errors       ✓ [████████]  10 min
Run unit tests            ✓ [████████]  10 min
Manual UI test            ✓ [████████]   5 min
─────────────────────────────────────────
Subtotal:                               30 min
```

### Step 4: Completion (5 min)
```
Write V9_WPF_UI_STATUS.json
Commit to git
Update CURRENT_SESSION.md
```

**TOTAL PHASE 2**: ~140 min (~2.3 hours) ✓

---

## What Success Looks Like

### Build Success ✓
```
- No compiler errors
- No warnings in "UI Responsiveness" code paths
- Solution builds in Release configuration
```

### Functional Success ✓
```
1. Mock Files Created
   - On startup: V9_TOS_RTD_STATUS.json created (if missing)
   - On startup: V9_COPY_TRADING_STATUS.json created (if missing)
   - On startup: V9_WPF_UI_STATUS.json created (if missing)

2. Status Display Working
   - TOS LED shows Orange (waiting) → Lime (connected) when file updates
   - Copy LED shows Red (inactive) → Lime (active) when file updates
   - Messages update when files change (tested with manual file edits)

3. UI Responsive
   - Clicking buttons while polling doesn't freeze UI
   - Typing symbols while polling works smoothly
   - No cross-thread exceptions in log file (v9_remote_log.txt)

4. Polling Works
   - Reads files every 2 seconds (verify in logs)
   - File reads complete in <100ms (verify in logs)
   - Background task runs without blocking
```

### Test Success ✓
```
- FilePathManager tests pass (paths resolve correctly)
- StatusFileManager tests pass (JSON read/write + mock creation)
- StatusPoller tests pass (files trigger UI updates)
```

### Completion Success ✓
```
- V9_WPF_UI_STATUS.json written with:
  {
    "Status": "Ready",
    "Message": "Phase 2 implementation complete",
    "LastUpdate": "[current timestamp]",
    "TosConnected": false,
    "CopyTradingActive": false
  }
- All changes committed to git
- CURRENT_SESSION.md updated with completion
```

---

## Decision Reference (Already Made)

**Question 1: File Path Handling**
- ✅ Answer: Hardcoded `.agent/SHARED_CONTEXT/` from executable directory
- ✅ Code: `Path.Combine(exeDir, "..", "..", "..", ".agent", "SHARED_CONTEXT")`

**Question 2: Error Handling**
- ✅ Answer: Auto-create mock JSON + show "Waiting for..." UI message
- ✅ Code: `if (!File.Exists(path)) await CreateMockFileAsync(path);`

**Question 3: UI Responsiveness**
- ✅ Answer: Async `File.ReadAllTextAsync()` every 2 seconds
- ✅ Code: `await File.ReadAllTextAsync(path)` in polling loop

**Question 4: .NET Version**
- ✅ Answer: Keep .NET 6.0 (already in .csproj)
- ✅ Code: `<TargetFramework>net6.0-windows</TargetFramework>`

**Question 5: TCP Client Structure**
- ✅ Answer: Add empty class with Phase 3 placeholders
- ✅ Code: `CopyTradingTcpClient` with `NotImplementedException` methods

**DO NOT CHANGE THESE DECISIONS** - They're approved and locked in.

---

## Key Code Patterns

### Pattern 1: Path Resolution
```csharp
private static string GetBasePath()
{
    string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
    string exeDir = System.IO.Path.GetDirectoryName(exePath);
    return System.IO.Path.Combine(exeDir, "..", "..", "..", ".agent", "SHARED_CONTEXT");
}
```
**Why**: Exe-relative paths work from any launch directory

### Pattern 2: Async File Reading
```csharp
var tosRtd = await StatusFileManager.ReadStatusFileAsync<TosRtdStatus>(
    FilePathManager.TosRtdStatusPath);
```
**Why**: Non-blocking, keeps UI responsive

### Pattern 3: UI Thread Marshaling
```csharp
_uiWindow.Dispatcher.Invoke(() =>
{
    _uiWindow.UpdateFromStatusFiles(tosRtd, copyTrading, wpfUi);
});
```
**Why**: Prevents cross-thread exceptions in WPF

### Pattern 4: Graceful Shutdown
```csharp
public void StopPolling()
{
    _pollingToken?.Cancel();
}
```
**Why**: CancellationToken ensures clean exit of polling loop

---

## Dependencies & Imports

You already have:
- ✅ `System.Net.Sockets` (TcpClient)
- ✅ `System.Text` (Encoding)
- ✅ `System.Windows` (WPF)
- ✅ `System.Threading.Tasks` (async/await)
- ✅ `System.Collections.Generic` (Dictionary)

You need to add:
- ✅ `Newtonsoft.Json` (already in .csproj v13.0.1)
  - Use: `JsonConvert.DeserializeObject<T>(json)`

---

## Logging Strategy

Use existing pattern in MainWindow.cs:
```csharp
private void LogToFile(string msg)
{
    try
    {
        System.IO.File.AppendAllText(_logPath,
            $"{DateTime.Now:HH:mm:ss.fff} | {msg}\r\n");
    }
    catch { }
}
```

Log these events:
- `"StatusPoller: Started polling"`
- `"StatusPoller: Poll #1 - Read TOS RTD status"`
- `"StatusFileManager: Created mock V9_TOS_RTD_STATUS.json"`
- `"StatusPoller: File read completed in Xms"`
- `"StatusPoller: Stopped polling"`

---

## Testing Checklist

### Before Committing
- [ ] Solution compiles without errors
- [ ] All public methods have XML docs
- [ ] No hardcoded absolute paths (use FilePathManager)
- [ ] All file I/O is async (no blocking reads)
- [ ] UI updates use Dispatcher.Invoke()
- [ ] CancellationToken properly disposed
- [ ] Mock files auto-created on first run
- [ ] Status LEDs change color when files update
- [ ] No memory leaks (run for 5 min, check RAM)

### Manual Testing
```csharp
// Test 1: Mock file creation
Delete all V9_*_STATUS.json files
Run app
Verify 3 files auto-created ✓

// Test 2: Status display
Edit V9_TOS_RTD_STATUS.json manually: "IsConnected": true
Watch UI LED change from Orange → Green within 2 seconds ✓

// Test 3: UI responsiveness
While polling is running:
- Click buttons (Long/Short/Flatten) ✓
- Type symbols in input box ✓
- No freezing, no lag ✓

// Test 4: Polling interval
Check v9_remote_log.txt timestamps
Verify ~2 second gaps between polls ✓

// Test 5: Error handling
Corrupt JSON in V9_TOS_RTD_STATUS.json (add invalid character)
Verify app doesn't crash, uses fallback object ✓
```

---

## When You're Done

### Commit Checklist
- [ ] All files created/modified
- [ ] Tests passing
- [ ] v9_remote_log.txt shows clean polling
- [ ] V9_WPF_UI_STATUS.json written
- [ ] Git status clean (all changes staged)

### Final Commit Message
```
docs: Implement V9_004 WPF UI Phase 2 - Real-time Status Integration

- Added FilePathManager for path resolution to .agent/SHARED_CONTEXT/
- Added StatusFileManager for JSON I/O with auto-mocking
- Added StatusPoller for 2-second async file polling
- Added CopyTradingTcpClient placeholder for Phase 3
- Created TosRtdStatus, CopyTradingStatus, WpfUiStatus models
- Updated MainWindow to display real-time status from JSON files
- Mock files auto-created if missing (Phase 1 testing support)
- UI remains responsive during polling (async/await pattern)
- Comprehensive unit and integration tests included

Phase 2 Success Criteria Met:
✓ All status files monitored and displayed
✓ UI remains responsive (2-second polling, no blocking)
✓ Mock files auto-created for Phase 1 testing
✓ Status display shows "Waiting for..." initially
✓ LEDs change color when files update
✓ Tests passing, zero memory leaks
✓ V9_WPF_UI_STATUS.json written on completion

Co-Authored-By: Claude <noreply@anthropic.com>
```

### Update Session
Edit `.agent/SHARED_CONTEXT/CURRENT_SESSION.md`:
```markdown
## V9_004 Implementation (COMPLETED)

**Task**: Implement WPF UI Phase 2 real-time status integration
**Status**: ✅ COMPLETE
**Files Created**: FilePathManager, StatusFileManager, StatusPoller, CopyTradingTcpClient, 3 Models
**Tests**: Unit + Integration tests passing
**UI Updates**: Status LEDs + messages wired to JSON file monitoring
**Duration**: ~2.3 hours
**Next**: Ready for Phase 3 (V9_003 Copy Trading integration)
```

---

## Architecture Reference

### File Relationships
```
FilePathManager
    ↓
StatusFileManager (uses FilePathManager.TosRtdStatusPath)
    ↓
StatusPoller (uses StatusFileManager.ReadStatusFileAsync<T>)
    ↓
MainWindow (calls poller.StartPolling() in constructor)
    ↓
CopyTradingTcpClient (placeholder for Phase 3)
```

### Data Flow
```
.agent/SHARED_CONTEXT/V9_TOS_RTD_STATUS.json
    ↓ (StatusPoller reads every 2 sec)
StatusFileManager.ReadStatusFileAsync<TosRtdStatus>()
    ↓ (Dispatched to UI thread)
MainWindow.UpdateFromStatusFiles()
    ↓ (Updates WPF properties)
TosStatusLed.Background = Brushes.Lime (or Orange/Red)
TosMessageTxt.Text = "Connected"
```

---

## Emergency Contacts

| Issue | Solution |
|-------|----------|
| Path traversal not working | Verify `.` is executable dir. Check git structure. Use `GetBasePath()` with logging. |
| JSON parse error | Log the JSON string. Check it matches schema in ARCHITECTURE_GUIDE.md. Return fallback object. |
| UI freezing during polling | Ensure ALL file I/O is async. No blocking `File.ReadAllText()`. Check for missing `await`. |
| Cross-thread exception | All UI updates must use `Dispatcher.Invoke()`. Check MainWindow.xaml.cs for violations. |
| Memory leak | Verify `_pollingToken?.Cancel()` called in OnClosed(). Check event handlers unsubscribed. |
| Tests fail | Run individually. Check assertions match expected behavior. Verify file paths in test. |

---

## Related Documents

- `.agent/V9_004_ARCHITECTURE_GUIDE.md` - Full architecture (562 lines)
- `.agent/V9_004_IMPLEMENTATION_SUMMARY.md` - Phase timeline (267 lines)
- `.agent/V9_004_QUICK_REFERENCE.md` - TL;DR reference (304 lines)
- `.agent/SHARED_CONTEXT/CURRENT_SESSION.md` - Session status
- `.agent/TASKS/MASTER_TASKS.json` - Task dependencies

---

## Success Criteria Summary

✅ **Build**: No errors, no warnings
✅ **Files**: 7 new + 2 modified + 2 tests
✅ **Functionality**: Status polling, UI updates, async pattern
✅ **Testing**: Unit tests + integration tests passing
✅ **Performance**: <100ms file reads, 2-second polling interval
✅ **UI**: Responsive, no freezing, cross-thread safe
✅ **Completion**: V9_WPF_UI_STATUS.json written, git committed
✅ **Session**: CURRENT_SESSION.md updated

---

**Prepared By**: Claude Haiku 4.5
**Date**: 2026-01-25
**Status**: HANDOFF READY
**Next Agent**: V9_004 WPF UI Implementation Agent (Phase 2)

You have everything you need. Begin implementation now.
