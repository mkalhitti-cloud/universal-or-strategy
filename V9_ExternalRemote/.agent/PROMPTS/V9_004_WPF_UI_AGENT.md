# V9_004 WPF UI AGENT PROMPT

**Use this prompt to enhance V9 external controls UI**

---

## YOU ARE: V9 WPF UI Enhancement Agent

**Role**: Build external control interface for V9 trading application

**Model**: Gemini Flash (escalate to Sonnet if complex)

**Workspace**: `V9_ExternalRemote/` (MainWindow.xaml and MainWindow.xaml.cs)

**Task ID**: V9_004

**Status**: PENDING (runs in parallel with V9_001, V9_003)

---

## CONTEXT - Read First (In Order)

1. `.agent/SHARED_CONTEXT/CURRENT_SESSION.md` - Current status
2. `.agent/V9_ARCHITECTURE.md` - System architecture
3. `V9_ExternalRemote/MainWindow.xaml` - Current UI layout
4. `V9_ExternalRemote/MainWindow.xaml.cs` - Current code-behind
5. `.agent/TASKS/MASTER_TASKS.json` - Task hierarchy

---

## YOUR MISSION

Enhance the V9 WPF UI to provide better visibility and control for trading operations.

**Current State**:
- Basic TOS RTD connection indicator
- Simple data display fields
- Minimal trading controls

**Target State**:
- Clear status indicators for all systems
- Real-time position tracking display
- Professional trading control buttons
- Account connection status
- Trade signal history log
- P&L dashboard

---

## PHASE 1: Analyze Current UI

### Review Current Layout
1. Open `MainWindow.xaml`
2. Document all current UI elements:
   - Grid structure
   - Labels and text boxes
   - Buttons (if any)
   - Status indicators

3. Open `MainWindow.xaml.cs`
4. Document all current code-behind:
   - Event handlers
   - Data binding logic
   - Status update methods

### Success Criteria
- [x] Understand current UI structure
- [x] Identify all existing controls
- [x] Map data flow from backend to UI

---

## PHASE 2: Design Enhanced UI Layout

### New UI Sections

**Section 1: System Status Bar** (Top of window)
```
┌─────────────────────────────────────────────────┐
│ TOS RTD: ●GREEN  |  Server: ●READY  |  Time: HH:MM:SS │
└─────────────────────────────────────────────────┘
```
- TOS RTD LED (updates real-time)
- Server status LED
- Current time/UTC
- Connection uptime

**Section 2: Connected Accounts Panel**
```
┌────────────────────────────┐
│ Connected Accounts: 20/20   │
├────────────────────────────┤
│ Account Status Grid:       │
│ Account 1  [●] Equity: $X  │
│ Account 2  [●] Equity: $X  │
│ ...                        │
│ Account 20 [●] Equity: $X  │
└────────────────────────────┘
```
- Show all 20 accounts
- Green dot = connected
- Red dot = disconnected
- Equity per account
- Total equity aggregated

**Section 3: Trading Controls**
```
┌─────────────────────────┐
│    Trading Controls      │
├─────────────────────────┤
│ Symbol: [__________]    │
│ Quantity: [____]        │
│                         │
│ [  LONG  ] [  SHORT  ]  │
│ [ FLATTEN] [ CLOSE ALL]│
└─────────────────────────┘
```
- Symbol input field
- Quantity input field
- LONG button (green)
- SHORT button (red)
- FLATTEN button (yellow)
- CLOSE ALL button (dark red)

**Section 4: Position Summary Dashboard**
```
┌────────────────────────────┐
│     Position Summary        │
├────────────────────────────┤
│ Total Contracts: 20        │
│ Net Position: +10 LONG     │
│ Avg Entry Price: $4520.50  │
│ Current Price: $4525.00    │
│                            │
│ Total P&L: +$1,250.00 ✓    │
│ Daily High: +$2,500.00     │
│ Daily Low: -$500.00        │
└────────────────────────────┘
```
- Total contracts across all accounts
- Net position (long/short)
- Average entry price
- Current market price
- Aggregate P&L (green if positive, red if negative)
- Daily high/low watermarks

**Section 5: Signal History Log**
```
┌──────────────────────────────────────┐
│        Signal History Log             │
├──────────────────────────────────────┤
│ 18:45:32 BROADCAST: LONG|MES|1       │
│ 18:45:31 RECEIVED: POSITION_SUMMARY  │
│ 18:45:30 RECEIVED: POSITION|Acc1...  │
│ 18:44:15 BROADCAST: SHORT|MES|2      │
│ 18:44:14 RECEIVED: HEARTBEAT         │
│ [Scroll to see more...]              │
└──────────────────────────────────────┘
```
- Timestamp for each event
- Event type (BROADCAST, RECEIVED, ERROR)
- Event details
- Auto-scrolling log
- Configurable history length (last 100 events)

### XAML Design Guidelines
- Use modern Material Design colors
- Group related controls with borders/frames
- Use consistent button styling
- Implement proper spacing and padding
- Make LEDs large and visible
- Use color coding (green=good, red=bad, yellow=warning)

---

## PHASE 2A: XAML Implementation

### Files to Create/Modify
- `MainWindow.xaml` - UI layout
- `MainWindow.xaml.cs` - Event handlers and data binding

### Key XAML Elements

```xaml
<!-- Status Bar -->
<StackPanel Orientation="Horizontal" Background="#F0F0F0" Padding="10">
    <Border Width="20" Height="20" CornerRadius="10" Background="Green" x:Name="LedTosRtd"/>
    <TextBlock Margin="10,0,0,0">TOS RTD</TextBlock>
    <!-- Repeat for other indicators -->
</StackPanel>

<!-- Account List -->
<ListBox x:Name="AccountsList" ItemsSource="{Binding Accounts}">
    <ListBox.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <Border Width="12" Height="12" CornerRadius="6" Background="{Binding StatusColor}"/>
                <TextBlock Text="{Binding Name}" Margin="10,0,0,0"/>
                <TextBlock Text="{Binding Equity, StringFormat='${0:N0}'}" Margin="20,0,0,0"/>
            </StackPanel>
        </DataTemplate>
    </ListBox.ItemTemplate>
</ListBox>

<!-- Trading Buttons -->
<Button Content="LONG" Click="OnLongClicked" Foreground="White" Background="Green" Padding="20,10"/>
<Button Content="SHORT" Click="OnShortClicked" Foreground="White" Background="Red" Padding="20,10"/>
```

### Success Criteria
- [x] All UI sections render without errors
- [x] Proper spacing and alignment
- [x] Colors match design specification
- [x] All controls are accessible

---

## PHASE 2B: Code-Behind Implementation

### Update MainWindow.xaml.cs

**Add Properties for Data Binding**:
```csharp
public ObservableCollection<AccountStatus> Accounts { get; set; }
public string CurrentPrice { get; set; }
public string TotalPnL { get; set; }
public int ConnectedAccountCount { get; set; }

public event PropertyChangedEventHandler PropertyChanged;
```

**Add Event Handlers**:
```csharp
private void OnLongClicked(object sender, RoutedEventArgs e)
{
    string symbol = SymbolTextBox.Text;
    int quantity = int.Parse(QuantityTextBox.Text);
    BroadcastSignal("LONG", symbol, quantity);
    LogSignal($"BROADCAST: LONG|{symbol}|{quantity}");
}

private void OnShortClicked(object sender, RoutedEventArgs e)
{
    string symbol = SymbolTextBox.Text;
    int quantity = int.Parse(QuantityTextBox.Text);
    BroadcastSignal("SHORT", symbol, quantity);
    LogSignal($"BROADCAST: SHORT|{symbol}|{quantity}");
}

private void OnFlattenClicked(object sender, RoutedEventArgs e)
{
    BroadcastSignal("FLATTEN", "", 0);
    LogSignal("BROADCAST: FLATTEN");
}
```

**Add Status Update Methods**:
```csharp
public void UpdateTosRtdStatus(bool connected)
{
    LedTosRtd.Background = connected ? Brushes.Green : Brushes.Red;
    TosRtdStatusText.Text = connected ? "CONNECTED" : "DISCONNECTED";
}

public void UpdateAccountStatus(string accountName, bool connected, decimal equity)
{
    var account = Accounts.FirstOrDefault(a => a.Name == accountName);
    if (account != null)
    {
        account.Connected = connected;
        account.Equity = equity;
    }
    ConnectedAccountCount = Accounts.Count(a => a.Connected);
}

public void UpdatePositionSummary(int totalContracts, decimal pnl, decimal entryPrice, decimal currentPrice)
{
    TotalContractsText.Text = totalContracts.ToString();
    TotalPnL = pnl.ToString("C");
    CurrentPriceText.Text = currentPrice.ToString("N2");
    AvgEntryPriceText.Text = entryPrice.ToString("N2");

    // Color code P&L
    var pnlBrush = pnl >= 0 ? Brushes.Green : Brushes.Red;
    TotalPnLText.Foreground = pnlBrush;
}

public void LogSignal(string message)
{
    string timestamp = DateTime.Now.ToString("HH:mm:ss");
    SignalHistoryLog.AppendText($"{timestamp} {message}\n");
    SignalHistoryLog.ScrollToEnd();
}
```

### Success Criteria
- [x] All event handlers work correctly
- [x] Data binding updates UI in real-time
- [x] Status updates are accurate
- [x] Signal logging works

---

## PHASE 3: Visual Polish

### Styling Improvements
1. Use consistent fonts (Segoe UI, 11pt for regular)
2. Apply bold font to headers (14pt)
3. Use color-coded text (green for positive, red for negative)
4. Add icons for trading buttons (if possible)
5. Implement smooth LED animations

### Layout Improvements
1. Use Grid with proper row/column definitions
2. Center content appropriately
3. Add visual separators between sections
4. Implement responsive sizing
5. Add tooltips to buttons explaining their function

### Example Styling
```xaml
<Style x:Key="HeaderText" TargetType="TextBlock">
    <Setter Property="FontSize" Value="14"/>
    <Setter Property="FontWeight" Value="Bold"/>
    <Setter Property="Foreground" Value="#333333"/>
    <Setter Property="Margin" Value="0,0,0,10"/>
</Style>

<Style x:Key="GreenButton" TargetType="Button">
    <Setter Property="Background" Value="#4CAF50"/>
    <Setter Property="Foreground" Value="White"/>
    <Setter Property="Padding" Value="15,8"/>
    <Setter Property="FontWeight" Value="Bold"/>
</Style>
```

### Success Criteria
- [x] UI looks professional
- [x] Colors are consistent
- [x] Text is readable
- [x] Layout is balanced

---

## PHASE 4: Testing

### Test Scenarios
1. **Status Indicators**
   - Verify TOS RTD LED changes color correctly
   - Verify Server status updates
   - Check timestamp accuracy

2. **Account Display**
   - Verify all 20 accounts show
   - Check status colors update
   - Verify equity displays correctly

3. **Trading Controls**
   - Click LONG button → should broadcast signal
   - Click SHORT button → should broadcast signal
   - Click FLATTEN button → should broadcast signal
   - Verify logging shows each action

4. **Position Dashboard**
   - Verify P&L displays correctly
   - Check that positive P&L is green
   - Check that negative P&L is red
   - Verify totals are accurate

5. **Signal History**
   - Verify all signals are logged
   - Check timestamp accuracy
   - Verify auto-scrolling works
   - Verify history doesn't grow unbounded

### Success Criteria
- [x] All buttons trigger correct actions
- [x] Display updates reflect actual state
- [x] Colors change appropriately
- [x] No UI freezing or crashes
- [x] Performance is smooth

---

## DELIVERABLES

When complete, create:

**File 1**: Update `MainWindow.xaml`
- Complete UI layout with all sections
- Proper styling and color scheme
- All controls properly named

**File 2**: Update `MainWindow.xaml.cs`
- All event handlers implemented
- All display update methods
- Signal logging functionality

**File 3**: `.agent/SHARED_CONTEXT/V9_WPF_UI_STATUS.json`
```json
{
  "task_id": "V9_004",
  "status": "COMPLETED",
  "completed_date": "2026-01-27T18:00:00Z",
  "components": {
    "status_bar": "IMPLEMENTED",
    "account_panel": "IMPLEMENTED",
    "trading_controls": "IMPLEMENTED",
    "position_dashboard": "IMPLEMENTED",
    "signal_log": "IMPLEMENTED"
  },
  "ui_features": {
    "status_indicators": true,
    "account_tracking": true,
    "real_time_updates": true,
    "signal_history": true,
    "color_coding": true
  }
}
```

---

## IMPORTANT NOTES

1. **Don't Break Existing Functionality**: Ensure TOS RTD and other existing features still work
2. **Data Binding**: Use MVVM pattern for clean separation of concerns
3. **Performance**: Keep UI responsive with async updates where needed
4. **Testing**: Test with simulated data before connecting to real systems
5. **Documentation**: Comment complex UI logic

---

## IF YOU GET STUCK

1. Check WPF documentation for control styling
2. Review XAML syntax in existing codebase
3. Check `MainWindow.xaml.cs` for existing patterns
4. Update `.agent/SHARED_CONTEXT/CURRENT_SESSION.md` with blocker
5. Escalate to Sonnet 4.5 if design is complex

---

## WHEN YOU'RE DONE (CRITICAL)

### 1. Update .agent/SHARED_CONTEXT/CURRENT_SESSION.md

Add completion report:
- Task ID: V9_004
- Status: COMPLETED or BLOCKED
- UI sections completed
- Testing results
- Any blockers

### 2. Update Your Task Status File

Create: `.agent/SHARED_CONTEXT/V9_WPF_UI_STATUS.json` (see template above)

Include:
- task_id
- status
- completed_date
- components implemented
- ui_features list

### 3. Commit Your Changes

```bash
git add .
git commit -m "feat: Enhance V9 WPF UI with real-time controls and dashboards (V9_004)"
```

DO NOT PUSH - coordinator will review and promote.

---

**Good luck! This UI enhancement will make V9 much more user-friendly.**
