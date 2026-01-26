# V9_004 Quick Reference Card
**For**: V9_004 WPF UI Agent Implementation
**Read Time**: 2 minutes
**Full Details**: `.agent/V9_004_ARCHITECTURE_GUIDE.md`

---

## The 5 Decisions (TL;DR)

| # | Question | Answer | Code Snippet |
|---|----------|--------|--------------|
| 1 | JSON file paths? | Hardcoded `.agent/SHARED_CONTEXT/` from exe dir | `Path.Combine(exeDir, "..", "..", "..", ".agent", "SHARED_CONTEXT")` |
| 2 | Missing files? | Auto-create mock files + show "Waiting for..." | `if (!File.Exists(path)) await CreateMockFileAsync(path);` |
| 3 | File reading? | Async `ReadAllTextAsync()` every 2 seconds | `await File.ReadAllTextAsync(path)` in polling loop |
| 4 | .NET version? | Keep .NET 6.0 (already in project) | `<TargetFramework>net6.0-windows</TargetFramework>` |
| 5 | TCP client now? | Yes, add empty structure with TODO comments | `CopyTradingTcpClient` class with placeholder methods |

---

## Phase 2 Implementation Checklist

### New Files to Create
- [ ] `FilePathManager.cs` - Path resolution
- [ ] `StatusFileManager.cs` - JSON I/O + mocking
- [ ] `StatusPoller.cs` - 2-second polling loop
- [ ] `CopyTradingTcpClient.cs` - TCP client structure
- [ ] `Models/TosRtdStatus.cs` - JSON model
- [ ] `Models/CopyTradingStatus.cs` - JSON model
- [ ] `Models/WpfUiStatus.cs` - JSON model

### Files to Modify
- [ ] `MainWindow.xaml.cs` - Start/stop polling, wire status updates
- [ ] `MainWindow.xaml` - Add status display section
- [ ] `App.xaml.cs` - Initialize FilePathManager

### Files to Create (Tests)
- [ ] `Tests/FilePathManagerTests.cs`
- [ ] `Tests/StatusFileManagerTests.cs`
- [ ] `Tests/StatusPollerTests.cs`

---

## Key Code Patterns

### FilePathManager Pattern
```csharp
public static class FilePathManager
{
    private static string GetBasePath()
    {
        string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string exeDir = System.IO.Path.GetDirectoryName(exePath);
        return System.IO.Path.Combine(exeDir, "..", "..", "..", ".agent", "SHARED_CONTEXT");
    }

    public static string TosRtdStatusPath =>
        Path.Combine(GetBasePath(), "V9_TOS_RTD_STATUS.json");
    public static string CopyTradingStatusPath =>
        Path.Combine(GetBasePath(), "V9_COPY_TRADING_STATUS.json");
    public static string WpfUiStatusPath =>
        Path.Combine(GetBasePath(), "V9_WPF_UI_STATUS.json");
}
```

### StatusPoller Pattern
```csharp
public class StatusPoller
{
    private CancellationTokenSource _pollingToken;
    private readonly int _pollIntervalMs = 2000;

    public void StartPolling()
    {
        _pollingToken = new CancellationTokenSource();
        _ = PollStatusFilesAsync(_pollingToken.Token);
    }

    private async Task PollStatusFilesAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var tosRtd = await StatusFileManager.ReadStatusFileAsync<TosRtdStatus>(
                    FilePathManager.TosRtdStatusPath);

                _uiWindow.Dispatcher.Invoke(() =>
                {
                    _uiWindow.UpdateFromStatusFiles(tosRtd, copyTrading, wpfUi);
                });

                await Task.Delay(_pollIntervalMs, token);
            }
            catch { }
        }
    }
}
```

### MainWindow Integration
```csharp
public MainWindow()
{
    InitializeComponent();

    // Start polling on window load
    _poller = new StatusPoller(this);
    _poller.StartPolling();

    Loaded += (s, e) => LogToFile("UI polling started");
}

protected override void OnClosed(EventArgs e)
{
    _poller?.StopPolling();  // Clean stop
    base.OnClosed(e);
}

public void UpdateFromStatusFiles(TosRtdStatus tos, CopyTradingStatus copy, WpfUiStatus ui)
{
    // Update LED colors
    TosStatusLed.Background = tos.IsConnected ? Brushes.Lime : Brushes.Orange;
    CopyStatusLed.Background = copy.IsActive ? Brushes.Lime : Brushes.Red;

    // Update messages
    TosMessageTxt.Text = tos.Message;
    CopyMessageTxt.Text = copy.Message;
}
```

---

## JSON Schemas to Expect

### From V9_001 (TOS RTD)
```json
{
  "IsConnected": true,
  "LastPrice": 4152.00,
  "Ema9": 4150.25,
  "Ema15": 4148.75,
  "LastUpdate": "2026-01-25T17:30:00",
  "Symbol": "/MES",
  "Message": "Connected"
}
```

### From V9_003 (Copy Trading)
```json
{
  "IsActive": true,
  "ConnectedAccounts": 3,
  "Message": "Copy Trading Active - 3 accounts",
  "LastUpdate": "2026-01-25T17:30:00",
  "AccountsList": ["Account1", "Account2", "Account3"]
}
```

### To Write (V9_004)
```json
{
  "Status": "Ready",
  "Message": "All systems operational",
  "LastUpdate": "2026-01-25T17:30:00",
  "TosConnected": true,
  "CopyTradingActive": false
}
```

---

## Testing Basics

### Unit Test Template
```csharp
[TestClass]
public class FilePathManagerTests
{
    [TestMethod]
    public void TosRtdStatusPath_IsValid()
    {
        string path = FilePathManager.TosRtdStatusPath;
        Assert.IsTrue(path.Contains(".agent/SHARED_CONTEXT"));
        Assert.IsTrue(path.EndsWith("V9_TOS_RTD_STATUS.json"));
    }
}
```

### Integration Test Template
```csharp
[TestMethod]
public async Task StatusPoller_UpdatesUI_WhenFileChanges()
{
    var poller = new StatusPoller(_mainWindow);
    poller.StartPolling();

    await Task.Delay(2500);  // Wait for poll

    // Write test JSON
    File.WriteAllText(FilePathManager.TosRtdStatusPath, testJson);

    await Task.Delay(2500);  // Wait for next poll

    Assert.IsTrue(_mainWindow.TosStatusLed.Background.Equals(Brushes.Lime));
}
```

---

## Gotchas & Tips

### ✅ DO
- Use `Dispatcher.Invoke()` for all UI updates from background threads
- Dispose `CancellationTokenSource` in `OnClosed()`
- Create `Models/` folder for JSON model classes
- Use `JsonConvert.DeserializeObject<T>()` from Newtonsoft.Json
- Handle missing files gracefully (create mocks)
- Log to file (`v9_remote_log.txt`) for debugging

### ❌ DON'T
- Don't sync read files (use async)
- Don't block UI thread with file I/O
- Don't forget to call `StopPolling()` on window close
- Don't hardcode absolute paths (use GetBasePath())
- Don't ignore JSON parsing errors (return defaults)
- Don't create multiple `StatusPoller` instances

---

## Performance Targets

| Metric | Target | Check With |
|--------|--------|-----------|
| File read time | <100ms | Stopwatch in tests |
| Polling interval | 2 seconds | Task.Delay(2000) |
| UI update latency | <200ms | Timestamp in log |
| Memory usage | <50MB | Task Manager |
| No memory leaks | 0 leaks | Run for 10 min, watch RAM |

---

## When to Call V9_004 "Done"

✅ Checklist for Completion:

1. **File Integration Works**
   - FilePathManager resolves paths correctly
   - StatusFileManager reads/writes JSON
   - Mock files auto-created on startup

2. **Polling Works**
   - StatusPoller starts on MainWindow load
   - Reads files every 2 seconds (async)
   - Updates UI without freezing

3. **UI Updates Work**
   - Status LEDs change color (green/orange/red)
   - Messages update when files change
   - No cross-thread errors in logs

4. **Tests Pass**
   - Unit tests: FilePathManager, StatusFileManager
   - Integration tests: Polling + file changes
   - No test failures

5. **Code Quality**
   - No warnings in build
   - Proper exception handling
   - Clean up resources on close

6. **Status File Written**
   - V9_WPF_UI_STATUS.json created
   - Status = "Ready"
   - Message = "Phase 2 implementation complete"

---

## Emergency Contacts

| Issue | Solution |
|-------|----------|
| Path not resolving | Check git directory structure, use `GetBasePath()` with traversal |
| JSON parse errors | Log error, return default object, check schema |
| UI freezing | Ensure all file I/O is async, check for blocking calls |
| Memory leak | Verify `CancellationToken` disposal, check event unsubscription |
| File not found | Already handled - mock file created automatically |
| Cross-thread error | Use `Dispatcher.Invoke()` for all UI updates from tasks |

---

## Quick Links

| Document | Purpose |
|----------|---------|
| `.agent/V9_004_ARCHITECTURE_GUIDE.md` | Full architecture decisions (562 lines) |
| `.agent/V9_004_IMPLEMENTATION_SUMMARY.md` | Phase timeline & success criteria |
| `.agent/SHARED_CONTEXT/CURRENT_SESSION.md` | Session status & agent coordination |
| `.agent/TASKS/MASTER_TASKS.json` | Task dependencies |

---

**Created**: 2026-01-25
**For**: V9_004 WPF UI Agent (Phase 2 Implementation)
**Status**: READY FOR IMPLEMENTATION
