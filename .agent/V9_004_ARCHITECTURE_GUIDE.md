# V9_004 Agent Architecture Guide
**Date**: 2026-01-25
**Status**: READY FOR IMPLEMENTATION
**Target**: WPF Dashboard UI Enhancements & Real-time Status Display

---

## Executive Summary

This document provides **definitive architectural decisions** for the V9_004 Agent implementation. All 5 key questions are answered with rationale and code examples.

**Quick Answers:**
- ✅ **JSON Paths**: Hardcoded to `.agent/SHARED_CONTEXT/` (relative from executable)
- ✅ **Error Handling**: Create mock files + show "Waiting for data..." in UI
- ✅ **File Reading**: Use async `File.ReadAllTextAsync()` (every 2 seconds)
- ✅ **.NET Version**: .NET 6.0 (already in project, modern, no NT8 lock-in)
- ✅ **TCP Client**: Add structure now with placeholder methods (prep for Phase 3)

---

## Question 1: File Path Handling

### Decision: Hardcoded Relative Paths from Executable Directory

```csharp
// In App.xaml.cs or a new FilePathManager.cs class
public static class FilePathManager
{
    // Resolve relative to executable directory, NOT working directory
    private static string GetBasePath()
    {
        string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string exeDir = System.IO.Path.GetDirectoryName(exePath);
        return System.IO.Path.Combine(exeDir, "..", "..", "..", ".agent", "SHARED_CONTEXT");
    }

    public static string TosRtdStatusPath => Path.Combine(GetBasePath(), "V9_TOS_RTD_STATUS.json");
    public static string CopyTradingStatusPath => Path.Combine(GetBasePath(), "V9_COPY_TRADING_STATUS.json");
    public static string WpfUiStatusPath => Path.Combine(GetBasePath(), "V9_WPF_UI_STATUS.json");
}
```

### Why This Approach?

| Approach | Pros | Cons | Decision |
|----------|------|------|----------|
| Hardcoded `.agent/SHARED_CONTEXT/` | ✅ Simple, shared by all agents | ❌ Breaks if folder structure changes | **CHOSEN** |
| Relative from executable | ✅ Works from any launch location | ⚠️ Requires path traversal | **CHOSEN** |
| Config file | ✅ Flexible, no recompile | ❌ Extra file to maintain | Not needed |

**Rationale**: All agents already use this structure. Hardcoding keeps it simple. If structure changes, we update all agents at once.

---

## Question 2: Error Handling for Missing Files

### Decision: Create Mock Files + UI Fallback Message

```csharp
public class StatusFileManager
{
    public static async Task<T> ReadStatusFileAsync<T>(string filePath) where T : class
    {
        try
        {
            // If file doesn't exist, create mock version
            if (!File.Exists(filePath))
            {
                LogToFile($"File not found, creating mock: {filePath}");
                await CreateMockFileAsync(filePath);
            }

            string json = await File.ReadAllTextAsync(filePath);
            return JsonConvert.DeserializeObject<T>(json) ?? CreateDefaultObject<T>();
        }
        catch (Exception ex)
        {
            LogToFile($"Error reading status file: {ex.Message}");
            return CreateDefaultObject<T>();
        }
    }

    private static async Task CreateMockFileAsync(string filePath)
    {
        // Create appropriate mock JSON based on file type
        string mockJson = filePath.Contains("TOS_RTD")
            ? CreateMockTosRtdJson()
            : filePath.Contains("COPY_TRADING")
            ? CreateMockCopyTradingJson()
            : CreateMockWpfUiJson();

        await File.WriteAllTextAsync(filePath, mockJson);
    }

    private static string CreateMockTosRtdJson() => JsonConvert.SerializeObject(new
    {
        IsConnected = false,
        Message = "Waiting for V9_001 Agent...",
        LastUpdate = DateTime.Now,
        Ema9 = "---",
        Ema15 = "---",
        LastPrice = "---"
    }, Formatting.Indented);

    private static string CreateMockCopyTradingJson() => JsonConvert.SerializeObject(new
    {
        IsActive = false,
        Message = "Waiting for V9_003 Agent...",
        ConnectedAccounts = 0,
        LastUpdate = DateTime.Now
    }, Formatting.Indented);

    private static string CreateMockWpfUiJson() => JsonConvert.SerializeObject(new
    {
        Status = "Initializing",
        Message = "Waiting for data...",
        LastUpdate = DateTime.Now
    }, Formatting.Indented);

    private static T CreateDefaultObject<T>() where T : class
    {
        // Return safe defaults based on type
        return Activator.CreateInstance<T>();
    }
}
```

### UI Display Logic

```csharp
private void UpdateStatusDisplay()
{
    // In MainWindow.xaml.cs
    if (!tosRtdStatus.IsConnected)
    {
        TosStatusLed.Background = Brushes.Orange;  // Yellow = Waiting
        TosMessageTxt.Text = "Waiting for TOS RTD connection...";
        TosMessageTxt.Foreground = Brushes.Yellow;
    }
    else
    {
        TosStatusLed.Background = Brushes.Lime;    // Green = Connected
        TosMessageTxt.Text = "Connected";
        TosMessageTxt.Foreground = Brushes.Lime;
    }
}
```

**Why This Approach?**
- ✅ Phase 1 testing works without other agents
- ✅ UI provides clear "Waiting for..." feedback
- ✅ Automatically preps files for Phase 2/3
- ✅ No silent failures

---

## Question 3: UI Responsiveness (File Reading)

### Decision: Use Async File Reading with 2-Second Polling

```csharp
public class StatusPoller
{
    private CancellationTokenSource _pollingToken;
    private readonly int _pollIntervalMs = 2000;  // 2 seconds
    private readonly MainWindow _uiWindow;

    public StatusPoller(MainWindow uiWindow)
    {
        _uiWindow = uiWindow;
    }

    public void StartPolling()
    {
        _pollingToken = new CancellationTokenSource();
        _ = PollStatusFilesAsync(_pollingToken.Token);
    }

    public void StopPolling()
    {
        _pollingToken?.Cancel();
    }

    private async Task PollStatusFilesAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Read all status files asynchronously (non-blocking)
                var tosRtd = await StatusFileManager.ReadStatusFileAsync<TosRtdStatus>(
                    FilePathManager.TosRtdStatusPath);

                var copyTrading = await StatusFileManager.ReadStatusFileAsync<CopyTradingStatus>(
                    FilePathManager.CopyTradingStatusPath);

                var wpfUi = await StatusFileManager.ReadStatusFileAsync<WpfUiStatus>(
                    FilePathManager.WpfUiStatusPath);

                // Update UI on main thread
                _uiWindow.Dispatcher.Invoke(() =>
                {
                    _uiWindow.UpdateFromStatusFiles(tosRtd, copyTrading, wpfUi);
                });

                // Wait 2 seconds before next poll
                await Task.Delay(_pollIntervalMs, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogToFile($"Polling error: {ex.Message}");
                await Task.Delay(_pollIntervalMs, token);
            }
        }
    }
}
```

### Why Async Instead of Sync?

| Approach | UI Thread | Memory | Best For |
|----------|-----------|--------|----------|
| Sync `File.ReadAllText()` | ❌ Blocks | Low | Single file, rare reads |
| Async `File.ReadAllTextAsync()` | ✅ Non-blocking | Low | Real-time polling (2sec) |
| Async with Task.Delay() | ✅ Responsive | Low | **CHOSEN** |

**JSON files are small (~1KB)**, so I/O is negligible. Async keeps UI responsive for other actions (clicking buttons, typing symbols).

---

## Question 4: .NET Version Selection

### Decision: Keep .NET 6.0 (Already in Project)

```xml
<!-- V9_ExternalRemote.csproj (CURRENT) -->
<TargetFramework>net6.0-windows</TargetFramework>
```

**Why .NET 6.0?**

| Factor | .NET 6.0 | .NET 8.0 | .NET Framework 4.8 |
|--------|----------|----------|-------------------|
| Modern | ✅ LTS (until Nov 2026) | ✅✅ Latest LTS | ❌ Legacy |
| Performance | ✅ Fast | ✅✅ Faster | ⚠️ Slower |
| NinjaTrader 8 compat | ✅ External (safe) | ✅ External (safe) | ⚠️ May lock in |
| Team familiarity | ✅ Already using | ⚠️ Newer | ⚠️ Older |
| Decision | **KEEP THIS** | Not needed yet | Avoid |

**Rationale**:
- ✅ Already in the .csproj file
- ✅ No breaking changes needed
- ✅ Fast enough for UI polling (2 sec intervals)
- ✅ Upgrade to .NET 8.0 later if performance needed
- ✅ Keeps external to NinjaTrader (no lock-in)

---

## Question 5: TCP Client Structure Now vs. Phase 3

### Decision: Add Structure Now with Placeholder Methods

```csharp
/// <summary>
/// Phase 3 placeholder: TCP client for copy trading signal reception
/// Currently empty. Will be populated when V9_003 Agent completes.
/// </summary>
public class CopyTradingTcpClient
{
    private TcpClient _client;
    private NetworkStream _stream;
    private readonly string _serverIp;
    private readonly int _serverPort;
    private bool _isConnected;

    public event EventHandler<TradeSignalEventArgs> OnSignalReceived;
    public event EventHandler<ConnectionStateChangedEventArgs> OnConnectionChanged;

    public CopyTradingTcpClient(string serverIp = "127.0.0.1", int serverPort = 5000)
    {
        _serverIp = serverIp;
        _serverPort = serverPort;
        _isConnected = false;
    }

    /// <summary>
    /// [Phase 3] Connect to copy trading hub server
    /// </summary>
    public async Task ConnectAsync()
    {
        // TODO: Implement Phase 3
        throw new NotImplementedException("Phase 3: Copy Trading TCP Connection");
    }

    /// <summary>
    /// [Phase 3] Receive and parse trade signals
    /// </summary>
    public async Task ListenForSignalsAsync(CancellationToken token)
    {
        // TODO: Implement Phase 3
        throw new NotImplementedException("Phase 3: Signal parsing and dispatch");
    }

    /// <summary>
    /// [Phase 3] Send heartbeat to server
    /// </summary>
    public async Task SendHeartbeatAsync()
    {
        // TODO: Implement Phase 3
        throw new NotImplementedException("Phase 3: Heartbeat logic");
    }

    public bool IsConnected => _isConnected;

    public void Disconnect()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _isConnected = false;
        OnConnectionChanged?.Invoke(this, new ConnectionStateChangedEventArgs(false));
    }
}

// Event args for Phase 3
public class TradeSignalEventArgs : EventArgs
{
    public string Symbol { get; set; }
    public string Action { get; set; }  // LONG, SHORT, FLATTEN
    public int Quantity { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public class ConnectionStateChangedEventArgs : EventArgs
{
    public bool IsConnected { get; set; }

    public ConnectionStateChangedEventArgs(bool connected)
    {
        IsConnected = connected;
    }
}
```

### Why Add Structure Now?

| Approach | Phase 2 Effort | Phase 3 Integration | Decision |
|----------|----------------|-------------------|----------|
| Add empty structure now | +10 min | ✅ Cleaner integration | **CHOSEN** |
| Leave blank, add Phase 3 | -10 min | ⚠️ Disrupts UI logic later | Not preferred |

**Benefits**:
- ✅ UI can wire event handlers early
- ✅ Clear interface for Phase 3 implementation
- ✅ No refactoring needed when Phase 3 starts
- ✅ Status display ready for "Copy Trading Connected" indicator

---

## Implementation Roadmap

### Phase 2: UI Enhancements (V9_004 Agent)

**Week 1-2: File Integration**
1. Add `FilePathManager.cs` - path resolution
2. Add `StatusFileManager.cs` - JSON I/O + mocking
3. Add `StatusPoller.cs` - 2-second polling
4. Add `CopyTradingTcpClient.cs` - Phase 3 placeholder
5. Update `MainWindow.xaml.cs`:
   - Add status display section
   - Start polling in constructor
   - Stop polling in Dispose
6. Test with mock data (no V9_001/V9_003 running)

**Success Criteria**:
- ✅ Status display shows "Waiting for..." when files missing
- ✅ Status display updates when files exist
- ✅ UI remains responsive (no freezes)
- ✅ Mock files created automatically
- ✅ Async file reads every 2 seconds

---

### Phase 3: Copy Trading Integration (V9_003 Agent)

**Week 3-4: Copy Trading**
1. V9_003 Agent creates `V9_COPY_TRADING_STATUS.json`
2. V9_004 updates UI status from this file
3. Implement `CopyTradingTcpClient.ConnectAsync()`
4. Wire up signal handlers
5. Test live with accounts in Sim mode

---

### Phase 4: V8 Monitoring (V8_MONITOR Agent)

**Week 5: Health Checks**
1. Read `V8_MONITOR_STATUS.json`
2. Display V8 health in UI
3. Alert if V8 stops trading

---

## Code Organization

```
V9_ExternalRemote/
├── MainWindow.xaml.cs              (existing, enhance)
├── MainWindow.xaml                 (existing, add status section)
├── App.xaml.cs                     (existing, start polling)
├── TosRtdClient.cs                 (existing)
├── V9_ExternalRemote_TCP_Server.cs (existing)
├── FilePathManager.cs              (NEW: path resolution)
├── StatusFileManager.cs            (NEW: JSON I/O)
├── StatusPoller.cs                 (NEW: 2-second polling)
├── CopyTradingTcpClient.cs         (NEW: Phase 3 placeholder)
├── Models/
│   ├── TosRtdStatus.cs             (NEW: JSON models)
│   ├── CopyTradingStatus.cs
│   └── WpfUiStatus.cs
└── V9_ExternalRemote.csproj        (existing, no changes)
```

---

## JSON File Schemas

### V9_TOS_RTD_STATUS.json (From V9_001 Agent)
```json
{
  "IsConnected": true,
  "Ema9": 4150.25,
  "Ema15": 4148.75,
  "LastPrice": 4152.00,
  "LastUpdate": "2026-01-25T17:30:00",
  "Symbol": "/MES",
  "Message": "Connected"
}
```

### V9_COPY_TRADING_STATUS.json (From V9_003 Agent)
```json
{
  "IsActive": true,
  "ConnectedAccounts": 3,
  "Message": "Copy Trading Active - 3 accounts linked",
  "LastUpdate": "2026-01-25T17:30:00",
  "AccountsList": ["Account1", "Account2", "Account3"]
}
```

### V9_WPF_UI_STATUS.json (From V9_004 Agent)
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

## Testing Strategy

### Unit Tests (Phase 2)

```csharp
[TestClass]
public class FilePathManagerTests
{
    [TestMethod]
    public void TosRtdStatusPath_ReturnsValidPath()
    {
        string path = FilePathManager.TosRtdStatusPath;
        Assert.IsTrue(path.Contains(".agent/SHARED_CONTEXT"));
    }
}

[TestClass]
public class StatusFileManagerTests
{
    [TestMethod]
    public async Task CreateMockFile_WhenNotExists()
    {
        // Arrange
        string testPath = Path.GetTempFileName();
        File.Delete(testPath);

        // Act
        var status = await StatusFileManager.ReadStatusFileAsync<TosRtdStatus>(testPath);

        // Assert
        Assert.IsNotNull(status);
        Assert.IsTrue(File.Exists(testPath));
    }
}
```

### Integration Tests (Phase 2)

```csharp
[TestMethod]
public async Task StatusPoller_UpdatesUIWhenFileChanges()
{
    // Arrange
    var poller = new StatusPoller(_mainWindow);
    poller.StartPolling();

    // Act
    await Task.Delay(2500);  // Wait for 2 polls
    File.WriteAllText(FilePathManager.TosRtdStatusPath, mockJson);
    await Task.Delay(2500);  // Wait for next poll

    // Assert
    Assert.IsTrue(_mainWindow.TosStatusLed.Background.Equals(Brushes.Lime));
}
```

---

## Success Metrics

| Metric | Target | Phase |
|--------|--------|-------|
| File polling latency | <100ms | 2 |
| UI responsiveness | No freezes | 2 |
| Memory usage | <50MB | 2 |
| Auto-mock files | 100% on startup | 2 |
| TCP connection (Phase 3) | N/A | 3 |

---

## Rollback Plan

If issues arise:

1. **File path errors**: Check directory structure in git
2. **JSON parsing errors**: Revert to mock, check schema
3. **UI freezes**: Reduce poll interval or check for blocking calls
4. **Memory leaks**: Ensure `CancellationToken` disposal in `StatusPoller`

---

## Approval Checklist

- [ ] File path approach approved
- [ ] Error handling strategy approved
- [ ] Async file reading approved
- [ ] .NET version confirmed
- [ ] TCP client structure approved
- [ ] Ready to spawn V9_004 Agent

---

**Created By**: Claude Haiku 4.5
**For**: Universal OR Strategy V9 Development
**Next Step**: Provide this document to V9_004 Agent with approval
