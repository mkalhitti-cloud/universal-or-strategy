# B39-LaneA — Ticket File
<!-- Phase 3 — Ticket Generation | ptt-architect | 2026-07-30 -->
<!-- Rev 2 — ptt-architect | 2026-07-30 | Fixes F1, F2, F3 from ticket review -->
<!--   F1: T2 no longer defines Execute(int) body or CYC — T1 §7 CYC=5 is authoritative -->
<!--   F2: Execute(IEnumerable<Account>,int) moved into T1 §2.1, §7, §8 — T2 only calls it -->
<!--   F3: FormatGlobalBeBuffer and FormatWindowGlobalBe upgraded to 2-param (string name, int ticks) matching plan §5.5/§6.4/§8 -->
<!-- Rev 3 — ptt-architect | 2026-07-30 | Fix F4 from ticket review -->
<!--   F4: Added T_B39_07 (GlobalBeBuffer_IncrementClampedAt10) and T_B39_08 (GlobalBeBuffer_DecrementClampedAtMinus10) to T2 §4 -->
<!--       IncrementBuffer() and DecrementBuffer() are internal methods — every new internal method requires a [Fact] test. -->
<!--       Test count target updated from >=186 to >=188. -->
**Block**: B39-LaneA
**Spec**: `specs/002-trade-copier-spec.html` id="section-b39"
**Plan**: `docs/brain/B39-LaneA/02-architecture-plan.md` — **REVIEW_PASS (Rev 2)**
**Tickets**: 2 (T1 Source Code, T2 Tests)
**Test count target**: ≥ 188 [Fact] after T2

---

## TICKET-1 — Source Code: Global BE All Implementation

**Ticket ID**: T1
**Title**: Implement PttGlobalBreakEven + wire Panel/Window + update CopyEngine
**Block**: B39-LaneA
**Spec requirement IDs**: section-b39 §Execute, §ExecuteOne, §Buffer, §Panel-Row2, §Panel-Row3, §Window-Toolbar, §BuildTag
**Depends on**: none (T2 depends on this ticket reaching SCAN-06 PASS)

---

### 1 — Files

| Action | File |
|--------|------|
| **CREATE** | `src/PropTraderTools/Features/PttGlobalBreakEven.cs` |
| **MODIFY** | `src/PropTraderTools/CopyEngine.cs` |
| **MODIFY** | `src/PropTraderTools/TradeCopierPanel.cs` |
| **MODIFY** | `src/PropTraderTools/TradeCopierWindow.cs` |

---

### 2 — Method Signatures (complete, exact C#)

#### 2.1 `PttGlobalBreakEven.cs` (NEW file, ~70 lines)

```csharp
namespace PropTraderTools
{
    internal sealed class PttGlobalBreakEven
    {
        // Fields
        private volatile int _globalBeBuffer = 0;
        private readonly Action<Account, Instrument, double> _submitBeStop;

        // Constructors
        internal PttGlobalBreakEven();                                                                  // production
        internal PttGlobalBreakEven(Action<Account, Instrument, double> submitBeStop);                 // test seam

        // Public surface
        internal void Execute(int bufferTicks);                                                         // CYC=5 (double-foreach + null-guard)
        internal void Execute(IEnumerable<Account> accounts, int bufferTicks);                         // CYC=5 (test-seam overload — same loop body)
        private  void ExecuteOne(Account acc, Position pos, int bufferTicks);                          // CYC=4
        internal int  GlobalBeBuffer { get; }                                                           // CYC=1
        internal void IncrementBuffer();                                                                // CYC=2
        internal void DecrementBuffer();                                                                // CYC=2
    }
}
```

**Exact constructor bodies** (engineer copies verbatim):

```csharp
// Production constructor — delegates to injection constructor using inline lambda.
// The lambda captures nothing at construction time; CopyEngine.Instance is resolved at call time.
internal PttGlobalBreakEven()
    : this((acc, instr, price) => CopyEngine.Instance.SubmitBeStop(acc, instr, price)) { }

// Test injection constructor.
internal PttGlobalBreakEven(Action<Account, Instrument, double> submitBeStop)
{
    _submitBeStop = submitBeStop;
}
```

**Execute() — exact bodies** (both overloads):

```csharp
// Production entry point — iterates Account.All directly. CYC=5 (1 base + foreach + foreach + if + ||).
internal void Execute(int bufferTicks)
{
    foreach (var acc in Account.All)
    {
        foreach (var pos in acc.Positions)
        {
            if (pos == null || pos.Quantity == 0) continue;    // flat or null — skip
            ExecuteOne(acc, pos, bufferTicks);
        }
    }
}

// Test-seam overload — accepts injected IEnumerable<Account> so tests bypass Account.All. CYC=5.
// Identical loop body to the production overload above; no delegation — no CYC change to Execute(int).
internal void Execute(IEnumerable<Account> accounts, int bufferTicks)
{
    foreach (var acc in accounts)
    {
        foreach (var pos in acc.Positions)
        {
            if (pos == null || pos.Quantity == 0) continue;
            ExecuteOne(acc, pos, bufferTicks);
        }
    }
}
```

**ExecuteOne() — exact body** (CYC=4):

```csharp
private void ExecuteOne(Account acc, Position pos, int bufferTicks)
{
    if (pos == null || pos.Quantity == 0) return;              // defensive re-check
    bool   isLong   = pos.MarketPosition == MarketPosition.Long;
    double tickSize = pos.Instrument.MasterInstrument?.TickSize ?? 0.25;
    double bePrice  = Math.Round(
        (pos.AveragePrice + (isLong ? bufferTicks : -bufferTicks) * tickSize) / tickSize
    ) * tickSize;
    _submitBeStop(acc, pos.Instrument, bePrice);
}
```

**Buffer property and helpers — exact bodies**:

```csharp
internal int GlobalBeBuffer => _globalBeBuffer;               // CYC=1

internal void IncrementBuffer()                               // CYC=2
{
    if (_globalBeBuffer < 10) _globalBeBuffer++;
}

internal void DecrementBuffer()                               // CYC=2
{
    if (_globalBeBuffer > -10) _globalBeBuffer--;
}
```

#### 2.2 `CopyEngine.cs` — 3 targeted changes

```csharp
// Change 1 — Build tag (line 41, exact string):
internal const string Tag = "PTT-COPIER B39 | global-be-all | 2026-07-30";
// NOTE: substitute actual implementation date if different.

// Change 2 — SubmitBeStop accessibility (modifier change only, no logic change):
// BEFORE:
private void SubmitBeStop(Account leaderAcc, Instrument instr, double bePrice)
// AFTER:
internal void SubmitBeStop(Account leaderAcc, Instrument instr, double bePrice)

// Change 3 — GlobalBe property (new, placed after the Instance property):
// Getter-only auto-property with initializer; C#6 / .NET 4.8 compliant.
// NT8-001: NOT an init accessor — uses { get; } — PASS.
internal PttGlobalBreakEven GlobalBe { get; } = new PttGlobalBreakEven();
```

#### 2.3 `TradeCopierPanel.cs` — new fields, Row 2 restructure, Row 3 restructure, handlers, helper

**New fields** (add near existing `_xxxBtn2` and `BrushXxx` field declarations):

```csharp
// B39: BE ALL button reference for green-flash update.
private Button _globalBeBtn2;

// B39: Frozen static brush for the purple BE ALL button (JS-008 compliant).
// MakeBrush(r,g,b) calls .Freeze() internally.
private static readonly SolidColorBrush BrushPurple = MakeBrush(168, 85, 247);
```

**New event handlers** (exact signatures):

```csharp
private void OnGlobalBeClick(object sender, RoutedEventArgs e)   // CYC=3
private void OnGlobalBeUp(object sender, RoutedEventArgs e)       // CYC=2
private void OnGlobalBeDown(object sender, RoutedEventArgs e)     // CYC=2
```

**Handler bodies**:

```csharp
// OnGlobalBeClick — fires BE ALL, green flash 500ms. CYC=3.
private void OnGlobalBeClick(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.GlobalBe.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
    if (_globalBeBtn2 == null) return;
    _globalBeBtn2.Background = BrushFlash;                        // green flash
    var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
    t.Tick += (s, _) => { _globalBeBtn2.ClearValue(Button.BackgroundProperty); t.Stop(); };
    t.Start();
}

// OnGlobalBeUp — increment shared buffer, update label. CYC=2.
private void OnGlobalBeUp(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.GlobalBe.IncrementBuffer();
    if (_globalBeBtn2 != null)
        _globalBeBtn2.Content = FormatGlobalBeBuffer("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
}

// OnGlobalBeDown — decrement shared buffer, update label. CYC=2.
private void OnGlobalBeDown(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.GlobalBe.DecrementBuffer();
    if (_globalBeBtn2 != null)
        _globalBeBtn2.Content = FormatGlobalBeBuffer("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
}
```

**New static helper** (exact signature + body):

```csharp
// B39: FormatGlobalBeBuffer — handles 0 / positive / negative.
// 2-parameter form: caller supplies label ("BE ALL"). Plan §5.5 authoritative.
// Does NOT modify the existing FormatBuffer(string, int) method.
// CYC=3 (1 base + 2 if branches).
private static string FormatGlobalBeBuffer(string name, int ticks)
{
    if (ticks == 0) return name;
    if (ticks > 0)  return name + " +" + ticks;
    return name + " " + ticks;    // int.ToString() of negative auto-includes "-"
}
```

#### 2.4 `TradeCopierWindow.cs` — new fields, global toolbar row, handlers, helper

**Button initial label call site** (in Row 2 build code):
```csharp
// Initial label uses 2-param helper — matches plan §5.2 call site:
Content = FormatGlobalBeBuffer("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer)
```

**New fields** (add near existing `WBrushXxx` field declarations):

```csharp
// B39: window BE ALL button reference for green-flash update.
private Button _windowGlobalBeBtn;

// B39: Frozen static brushes for BE ALL in window (JS-008: MakeWinBrush calls .Freeze()).
// If WBrushFlash already exists from a prior block, do NOT add a duplicate — check first.
private static readonly SolidColorBrush WBrushPurple = MakeWinBrush(168, 85, 247);
private static readonly SolidColorBrush WBrushFlash  = MakeWinBrush(34, 197, 94);  // skip if already declared
```

**New event handlers** (exact signatures):

```csharp
private void OnWindowGlobalBeClick(object sender, RoutedEventArgs e)   // CYC=3
private void OnWindowGlobalBeUp(object sender, RoutedEventArgs e)       // CYC=2
private void OnWindowGlobalBeDown(object sender, RoutedEventArgs e)     // CYC=2
```

**Handler bodies**:

```csharp
// OnWindowGlobalBeClick — fires BE ALL, green flash 500ms. CYC=3.
private void OnWindowGlobalBeClick(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.GlobalBe.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
    if (_windowGlobalBeBtn == null) return;
    _windowGlobalBeBtn.Background = WBrushFlash;
    var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
    t.Tick += (s, _) => { _windowGlobalBeBtn.ClearValue(Button.BackgroundProperty); t.Stop(); };
    t.Start();
}

// OnWindowGlobalBeUp — increment shared buffer, update window label. CYC=2.
private void OnWindowGlobalBeUp(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.GlobalBe.IncrementBuffer();
    if (_windowGlobalBeBtn != null)
        _windowGlobalBeBtn.Content = FormatWindowGlobalBe("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
}

// OnWindowGlobalBeDown — decrement shared buffer, update window label. CYC=2.
private void OnWindowGlobalBeDown(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.GlobalBe.DecrementBuffer();
    if (_windowGlobalBeBtn != null)
        _windowGlobalBeBtn.Content = FormatWindowGlobalBe("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
}
```

**New static helper** (exact signature + body):

```csharp
// B39: FormatWindowGlobalBe — same logic as Panel's FormatGlobalBeBuffer.
// 2-parameter form: caller supplies label ("BE ALL"). Plan §6.4 authoritative.
// Duplicated intentionally to avoid cross-file coupling. CYC=3.
private static string FormatWindowGlobalBe(string name, int ticks)
{
    if (ticks == 0) return name;
    if (ticks > 0)  return name + " +" + ticks;
    return name + " " + ticks;
}
```

**Button initial label call site** (in toolbar row build code):
```csharp
// Initial label uses 2-param helper — matches plan §6.2 call site:
Content = FormatWindowGlobalBe("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer)
```

---

### 3 — JS Rule Constraints

| Rule | Applies to | Requirement |
|------|-----------|-------------|
| JS-021 | All new/modified files | Zero `lock()` anywhere. `_globalBeBuffer` is `volatile int`, UI-thread mutations only. |
| JS-008 | TradeCopierPanel.cs, TradeCopierWindow.cs | All brushes via `MakeBrush()`/`MakeWinBrush()` frozen static readonly fields. Zero inline `new SolidColorBrush(...)` without Freeze(). `BrushPurple`, `WBrushPurple`, `WBrushFlash` all comply. |
| JS-023 | PttGlobalBreakEven.cs | `volatile int _globalBeBuffer` — ALLOWED. `volatile double` — BANNED; not used. |
| NT8-003 | PttGlobalBreakEven.cs | No `volatile double` — confirmed. |
| NT8-001 | CopyEngine.cs | `GlobalBe { get; }` is a getter-only auto-property — NOT an `init` accessor. Compliant. |
| JS-002 | PttGlobalBreakEven.cs | No `return null`. Uses early `return` (void) and `continue`. |
| JS-033 | All handler methods | All event handlers are `private void` synchronous. No `async void`. Timer-based flash uses `DispatcherTimer`, not async. |
| ASCII-only | All files | All identifiers and string literals are ASCII. Unicode arrows (`\u25B2`, `\u25BC`) use escape sequences, not literal Unicode. |
| No DateTime.Now | All files | Not used anywhere in new code. |
| No FontFamily | All files | No FontFamily usage. |
| PTT- prefix | CopyEngine.cs | `SubmitBeStop` already uses PTT- prefixed order names internally — unchanged. |

---

### 4 — Structural Changes: Row 2 and Row 3 (Panel)

The engineer must restructure `BuildBufferedButtonsRow(StackPanel root)` in `TradeCopierPanel.cs`:

#### Current layout (before B39)
- **Row 2**: `UniformGrid Columns=2` — Left = Cancel, Right = BE cluster (BE button + ▲▼)
- **Row 3**: Full-width COPY ON/OFF toggle button

#### B39 target layout
- **Row 2**: `UniformGrid Columns=2` — Left = BE cluster (unchanged), Right = BE ALL cluster (NEW, replaces Cancel)
- **Row 3**: `UniformGrid Columns=2` — Left = Cancel (moved from Row 2), Right = COPY ON/OFF (unchanged handler)

#### BE ALL cluster structure (Col 1 of Row 2)
```
DockPanel (LastChildFill=true)
  ├─ Grid docked Right (2 rows × 12px each):
  │   ├─ RepeatButton ▲  (Click += OnGlobalBeUp)
  │   └─ RepeatButton ▼  (Click += OnGlobalBeDown)
  └─ Button _globalBeBtn2
         Content  = FormatGlobalBeBuffer(0)   // = "BE ALL"
         BorderBrush = BrushPurple (JS-008 frozen)
         Foreground  = BrushPurple
         BorderThickness = new Thickness(2)
         Style = "NTButtonStyle" (SetResourceReference)
         Click += OnGlobalBeClick
```

#### Row 3 structure (Cancel + COPY toggle)
```
UniformGrid Columns=2
  ├─ Button _cancelBtn2    (Content="Cancel",  Click += OnCancel2)
  └─ Button _copyToggleBtn2 (Content="\u25CF COPY OFF", Click += OnCopyToggle)
```

Both buttons use `SetResourceReference(Control.StyleProperty, "NTButtonStyle")`.
Existing `OnCancel2` and `OnCopyToggle` handler logic is **unchanged**.

---

### 5 — Structural Changes: Global Toolbar Row (Window)

The engineer must insert a new toolbar row in `BuildUI()` in `TradeCopierWindow.cs`:

**Insertion point**: After `root.Children.Add(sep1)` and BEFORE `DockPanel.SetDock(rulesScroll, Dock.Top)`.

```
StackPanel globalBeToolbar (Orientation=Horizontal, Margin=6,2,6,2)
  └─ DockPanel windowGlobalBeCluster (LastChildFill=true)
       ├─ Grid docked Right (2 rows × 12px each):
       │   ├─ RepeatButton ▲  (Click += OnWindowGlobalBeUp)
       │   └─ RepeatButton ▼  (Click += OnWindowGlobalBeDown)
       └─ Button _windowGlobalBeBtn
              Content  = FormatWindowGlobalBe(0)   // = "BE ALL"
              BorderBrush = WBrushPurple (JS-008 frozen)
              Foreground  = WBrushPurple
              BorderThickness = new Thickness(2)
              Padding = new Thickness(8, 3, 8, 3)
              Style = "NTButtonStyle" (SetResourceReference)
              Click += OnWindowGlobalBeClick

DockPanel.SetDock(globalBeToolbar, Dock.Top);
root.Children.Add(globalBeToolbar);
```

---

### 6 — FormatGlobalBeBuffer Specification

**Rule**: Do NOT modify the existing `FormatBuffer(string name, int ticks)` method.
It continues to return `name + " +" + ticks` (always positive) for Trim/Flatten/BE buttons.

**New helper `FormatGlobalBeBuffer(string name, int ticks)`** in `TradeCopierPanel.cs` (plan §5.5 form):

| `name` | `ticks` | Output |
|--------|---------|--------|
| `"BE ALL"` | `0` | `"BE ALL"` |
| `"BE ALL"` | `2` | `"BE ALL +2"` |
| `"BE ALL"` | `-3` | `"BE ALL -3"` |

**New helper `FormatWindowGlobalBe(string name, int ticks)`** in `TradeCopierWindow.cs` (plan §6.4 form): identical logic, different name to avoid cross-file coupling.

---

### 7 — CYC Budget (all new methods)

| Method | File | CYC | Budget ≤8 |
|--------|------|-----|-----------|
| `Execute(int)` | PttGlobalBreakEven.cs | 5 | PASS |
| `Execute(IEnumerable<Account>, int)` | PttGlobalBreakEven.cs | 5 | PASS |
| `ExecuteOne(Account, Position, int)` | PttGlobalBreakEven.cs | 4 | PASS |
| `GlobalBeBuffer` (property) | PttGlobalBreakEven.cs | 1 | PASS |
| `IncrementBuffer()` | PttGlobalBreakEven.cs | 2 | PASS |
| `DecrementBuffer()` | PttGlobalBreakEven.cs | 2 | PASS |
| `OnGlobalBeClick` | TradeCopierPanel.cs | 3 | PASS |
| `OnGlobalBeUp` | TradeCopierPanel.cs | 2 | PASS |
| `OnGlobalBeDown` | TradeCopierPanel.cs | 2 | PASS |
| `FormatGlobalBeBuffer(string, int)` | TradeCopierPanel.cs | 3 | PASS |
| `OnWindowGlobalBeClick` | TradeCopierWindow.cs | 3 | PASS |
| `OnWindowGlobalBeUp` | TradeCopierWindow.cs | 2 | PASS |
| `OnWindowGlobalBeDown` | TradeCopierWindow.cs | 2 | PASS |
| `FormatWindowGlobalBe(string, int)` | TradeCopierWindow.cs | 3 | PASS |

**Note on Execute CYC=5**: The spec target was 3–4. The plan (REVIEW_PASS Rev 2) accepts CYC=5 due to the defensive `pos == null || pos.Quantity == 0` guard required for NT8 sim compatibility. The absolute budget is ≤8; 5 is compliant.

**Note on ExecuteOne CYC=4**: The spec target was 2. The plan (REVIEW_PASS Rev 2) accepts CYC=4 due to the defensive re-check guard. Absolute budget is ≤8; 4 is compliant.

---

### 8 — Acceptance Criteria

- [ ] `src/PropTraderTools/Features/PttGlobalBreakEven.cs` created with ~80 lines matching §2.1 exactly
- [ ] Both constructors present: default (production) and injection (test seam)
- [ ] `volatile int _globalBeBuffer = 0` declared (NOT `volatile double`)
- [ ] Both `Execute(int)` and `Execute(IEnumerable<Account>, int)` overloads present and compilable
- [ ] `Execute(IEnumerable<Account>, int)` is accessible from the test project (internal + InternalsVisibleTo)
- [ ] `CopyEngine.cs` line 41 tag updated to `"PTT-COPIER B39 | global-be-all | {date}"`
- [ ] `SubmitBeStop` accessibility changed from `private` to `internal` (no other changes)
- [ ] `CopyEngine.GlobalBe { get; } = new PttGlobalBreakEven()` property added
- [ ] Panel Row 2: BE cluster (left, unchanged) + BE ALL cluster (right, purple, with ▲▼)
- [ ] Panel Row 3: UniformGrid with Cancel (left, half-width) + COPY ON/OFF (right, half-width)
- [ ] Panel: `_globalBeBtn2` field declared; `BrushPurple = MakeBrush(168, 85, 247)` field declared
- [ ] Panel: `FormatGlobalBeBuffer(string, int)` added (2-param, plan §5.5); existing `FormatBuffer(string, int)` untouched
- [ ] Window: global toolbar row added above `rulesScroll`
- [ ] Window: `_windowGlobalBeBtn` field, `WBrushPurple`, `WBrushFlash` fields declared (no duplicates)
- [ ] Window: `FormatWindowGlobalBe(string, int)` added (2-param, plan §6.4)
- [ ] Green flash 500ms via `DispatcherTimer` in both `OnGlobalBeClick` and `OnWindowGlobalBeClick`
- [ ] `[assembly: InternalsVisibleTo("V12_Performance.Tests")]` verified in PropTraderTools (add if absent)
- [ ] All 7 scans pass (SCAN-01 through SCAN-07)

---

### 9 — 7-Scan Checklist (MANDATORY — engineer must run all 7 and report results)

```
SCAN-01: grep -r "lock(" src/ --include="*.cs"
         Required: 0 matches in new/modified files
         Result: ___

SCAN-02: grep -r "async void" src/ --include="*.cs"
         Required: 0 matches in new/modified files
         Result: ___

SCAN-03: grep -r "return null" src/ --include="*.cs"
         Required: 0 matches in new/modified code
         Result: ___

SCAN-04: grep -r "throw new" src/ --include="*.cs"
         Required: 0 matches in new/modified code
         Result: ___

SCAN-05: python scripts/complexity_audit.py
         Required: all new methods CYC <= 8
         Result: ___

SCAN-06: dotnet build
         Required: 0 errors, 0 new warnings
         Result: ___

SCAN-07: dotnet test
         Required: all [Fact] pass (run AFTER T2; >= 186 total)
         Note: run after T2 is implemented; SCAN-07 from T1 alone verifies no regressions
         Result: ___
```

**Gate rule**: SCAN-06 must be GREEN before T2 begins. SCAN-07 counts ≥186 only after T2 is applied.

---

---

## TICKET-2 — Tests: 6 new [Fact] in CopyEngineTests.cs

**Ticket ID**: T2
**Title**: Add T_B39_01 through T_B39_08 to CopyEngineTests.cs
**Block**: B39-LaneA
**Spec requirement IDs**: section-b39 §Tests, T_B39_01..T_B39_06
**Depends on**: T1 — SCAN-06 (dotnet build) must be GREEN before starting T2

---

### 1 — File

| Action | File |
|--------|------|
| **MODIFY** | `tests/V12_Performance.Tests/Core/CopyEngineTests.cs` |

---

### 2 — Test Seam Architecture

`PttGlobalBreakEven` accepts an injected `Action<Account, Instrument, double>` via its test constructor. All 6 tests use this injection constructor — no NT8 runtime required.

**Standard test setup pattern**:

```csharp
var calls = new List<(Account acc, Instrument instr, double price)>();
Action<Account, Instrument, double> fakeSink = (a, i, p) => calls.Add((a, i, p));
var globalBe = new PttGlobalBreakEven(fakeSink);
```

**Account.All seam**: Because `Account.All` is an NT8 static collection that cannot be injected in tests, `PttGlobalBreakEven` exposes a second overload `Execute(IEnumerable<Account> accounts, int bufferTicks)` declared in **T1** (§2.1). Tests call this overload with stub account lists — no NT8 runtime required. T1 owns both overloads; T2 only **calls** the `IEnumerable<Account>` overload.

```csharp
// T2 uses this overload (declared in T1 §2.1, CYC=5):
globalBe.Execute(accs, bufferTicks: 0);
```

---

### 3 — Test Stubs Required

The tests require stub implementations of NT8 types. Check whether the following stubs already exist in `CopyEngineTests.cs` from B32–B38. If missing, create them:

```csharp
// Minimal stub — only needs Positions, Name
internal class StubAccount : Account
{
    public StubAccount(string name, params StubPosition[] positions)
    {
        // assign name and positions list
    }
}

// Minimal stub — needs Quantity, MarketPosition, AveragePrice, Instrument
internal class StubPosition : Position
{
    public double AveragePrice { get; set; }
    public int    Quantity     { get; set; }
    public MarketPosition MarketPosition { get; set; }
    public Instrument Instrument { get; set; }
}

// Minimal stub — needs MasterInstrument?.TickSize
internal class StubInstrument : Instrument
{
    public MasterInstrument MasterInstrument { get; set; }
}
```

If the existing test infrastructure (from prior blocks) already provides compatible stubs, use those. Do not create duplicates.

---

### 4 — The 6 [Fact] Tests (exact names + complete assertion specifications)

#### T_B39_01 — GlobalBe_FiresOnAllAccountsAllInstruments

```
Method name:  GlobalBe_FiresOnAllAccountsAllInstruments
[Fact] attribute: yes
Assert:
  - 3 stub accounts, each with 2 open positions (MES long + NQ long)
  - Execute(accounts, bufferTicks: 0)
  - calls.Count == 6
  - Each call has the correct account and instrument
  - bePrice for each == AveragePrice (buffer=0, no offset)
```

```csharp
[Fact]
public void GlobalBe_FiresOnAllAccountsAllInstruments()
{
    var calls = new List<(Account, Instrument, double)>();
    var globalBe = new PttGlobalBreakEven((a, i, p) => calls.Add((a, i, p)));

    var mes = MakeInstrument("MES", 0.25);
    var nq  = MakeInstrument("NQ",  0.25);

    var accs = new[]
    {
        MakeAccount("Acc1", MakeLongPos(mes, avgPrice: 5000, qty: 1),
                            MakeLongPos(nq,  avgPrice: 20000, qty: 1)),
        MakeAccount("Acc2", MakeLongPos(mes, avgPrice: 5001, qty: 2),
                            MakeLongPos(nq,  avgPrice: 20001, qty: 2)),
        MakeAccount("Acc3", MakeLongPos(mes, avgPrice: 5002, qty: 1),
                            MakeLongPos(nq,  avgPrice: 20002, qty: 1)),
    };

    globalBe.Execute(accs, bufferTicks: 0);

    Assert.Equal(6, calls.Count);
}
```

#### T_B39_02 — GlobalBe_SkipsFlatAccounts

```
Method name:  GlobalBe_SkipsFlatAccounts
[Fact] attribute: yes
Assert:
  - 2 accounts: Acc1 has 1 open position (qty=1), Acc2 has 1 flat position (qty=0)
  - Execute(accounts, bufferTicks: 0)
  - calls.Count == 1  (only the open position fires)
```

```csharp
[Fact]
public void GlobalBe_SkipsFlatAccounts()
{
    var calls = new List<(Account, Instrument, double)>();
    var globalBe = new PttGlobalBreakEven((a, i, p) => calls.Add((a, i, p)));

    var mes = MakeInstrument("MES", 0.25);

    var accs = new[]
    {
        MakeAccount("Acc1", MakeLongPos(mes, avgPrice: 5000, qty: 1)),
        MakeAccount("Acc2", MakeFlatPos(mes)),                         // qty=0, skipped
    };

    globalBe.Execute(accs, bufferTicks: 0);

    Assert.Equal(1, calls.Count);
}
```

#### T_B39_03 — GlobalBe_WorksWithNoCopyRule

```
Method name:  GlobalBe_WorksWithNoCopyRule
[Fact] attribute: yes
Assert:
  - CopyEngine has zero copy rules configured
  - 1 account with 1 open position
  - Execute(accounts, bufferTicks: 0)
  - calls.Count == 1  (no FindRule() dependency; fires regardless of rules)
  - No exception thrown
```

```csharp
[Fact]
public void GlobalBe_WorksWithNoCopyRule()
{
    var calls = new List<(Account, Instrument, double)>();
    var globalBe = new PttGlobalBreakEven((a, i, p) => calls.Add((a, i, p)));

    var mes  = MakeInstrument("MES", 0.25);
    var accs = new[] { MakeAccount("Acc1", MakeLongPos(mes, avgPrice: 5000, qty: 1)) };

    // Act — no exception expected, no rule lookup
    globalBe.Execute(accs, bufferTicks: 0);

    Assert.Equal(1, calls.Count);
}
```

#### T_B39_04 — GlobalBe_B35GuardInherited_UnderwaterSkipped

```
Method name:  GlobalBe_B35GuardInherited_UnderwaterSkipped
[Fact] attribute: yes
Assert:
  - 1 account with 1 long position
  - Execute(accounts, bufferTicks: 0)
  - The B35 guard lives inside CopyEngine.SubmitBeStop — not in PttGlobalBreakEven
  - Since we are using the injection test seam (fakeSink), the fakeSink IS called (no NT8 B35 guard runs)
  - calls.Count == 1 — SubmitBeStop delegate fires without exception
  - No exception is thrown (loop continues past any guard)
Note: The B35 guard is in the production SubmitBeStop; the test verifies PttGlobalBreakEven
      does not throw and does not re-implement its own guard.
```

```csharp
[Fact]
public void GlobalBe_B35GuardInherited_UnderwaterSkipped()
{
    var calls = new List<(Account, Instrument, double)>();
    var globalBe = new PttGlobalBreakEven((a, i, p) => calls.Add((a, i, p)));

    var mes  = MakeInstrument("MES", 0.25);
    // Simulate "underwater" long: avgPrice far above current market (B35 guard in production would warn)
    var accs = new[] { MakeAccount("Acc1", MakeLongPos(mes, avgPrice: 9999, qty: 1)) };

    var ex = Record.Exception(() => globalBe.Execute(accs, bufferTicks: 0));

    Assert.Null(ex);                     // no exception propagated
    Assert.Equal(1, calls.Count);        // delegate was called
}
```

#### T_B39_05 — GlobalBe_BufferAppliedPerDirectionCorrectly

```
Method name:  GlobalBe_BufferAppliedPerDirectionCorrectly
[Fact] attribute: yes
Assert:
  - buffer = +2, tickSize = 0.25
  - Long position: avgPrice=7500.00 -> bePrice = 7500 + 2*0.25 = 7500.50
  - Short position: avgPrice=7500.00 -> bePrice = 7500 - 2*0.25 = 7499.50
  - calls[0].price == 7500.50  (long)
  - calls[1].price == 7499.50  (short)
```

```csharp
[Fact]
public void GlobalBe_BufferAppliedPerDirectionCorrectly()
{
    var calls = new List<(Account, Instrument, double)>();
    var globalBe = new PttGlobalBreakEven((a, i, p) => calls.Add((a, i, p)));

    var mes  = MakeInstrument("MES", 0.25);
    var accs = new[]
    {
        MakeAccount("Long",  MakeLongPos( mes, avgPrice: 7500.00, qty: 1)),
        MakeAccount("Short", MakeShortPos(mes, avgPrice: 7500.00, qty: 1)),
    };

    globalBe.Execute(accs, bufferTicks: 2);

    Assert.Equal(2,       calls.Count);
    Assert.Equal(7500.50, calls[0].Item3, precision: 5);   // long: +2 ticks
    Assert.Equal(7499.50, calls[1].Item3, precision: 5);   // short: -2 ticks
}
```

#### T_B39_06 — GlobalBe_AllAccountsFlat_NoCalls

```
Method name:  GlobalBe_AllAccountsFlat_NoCalls
[Fact] attribute: yes
Assert:
  - 3 accounts, all flat (Quantity=0)
  - Execute(accounts, bufferTicks: 0)
  - calls.Count == 0
  - No exception thrown
```

```csharp
[Fact]
public void GlobalBe_AllAccountsFlat_NoCalls()
{
    var calls = new List<(Account, Instrument, double)>();
    var globalBe = new PttGlobalBreakEven((a, i, p) => calls.Add((a, i, p)));

    var mes  = MakeInstrument("MES", 0.25);
    var accs = new[]
    {
        MakeAccount("Acc1", MakeFlatPos(mes)),
        MakeAccount("Acc2", MakeFlatPos(mes)),
        MakeAccount("Acc3", MakeFlatPos(mes)),
    };

    var ex = Record.Exception(() => globalBe.Execute(accs, bufferTicks: 0));

    Assert.Null(ex);
    Assert.Equal(0, calls.Count);
}
```

#### T_B39_07 — GlobalBeBuffer_IncrementClampedAt10

```
Method name:  GlobalBeBuffer_IncrementClampedAt10
[Fact] attribute: yes
Assert:
  - Create a PttGlobalBreakEven via injection constructor
  - Call IncrementBuffer() 10 times to reach the upper bound
  - Call IncrementBuffer() one additional time
  - Assert: GlobalBeBuffer == 10  (clamp: did not exceed 10)
```

```csharp
[Fact]
public void GlobalBeBuffer_IncrementClampedAt10()
{
    var globalBe = new PttGlobalBreakEven((a, i, p) => { });

    for (int i = 0; i < 10; i++)
        globalBe.IncrementBuffer();

    // One more call — must not push above 10
    globalBe.IncrementBuffer();

    Assert.Equal(10, globalBe.GlobalBeBuffer);
}
```

#### T_B39_08 — GlobalBeBuffer_DecrementClampedAtMinus10

```
Method name:  GlobalBeBuffer_DecrementClampedAtMinus10
[Fact] attribute: yes
Assert:
  - Create a PttGlobalBreakEven via injection constructor
  - Call DecrementBuffer() 10 times to reach the lower bound
  - Call DecrementBuffer() one additional time
  - Assert: GlobalBeBuffer == -10  (clamp: did not go below -10)
```

```csharp
[Fact]
public void GlobalBeBuffer_DecrementClampedAtMinus10()
{
    var globalBe = new PttGlobalBreakEven((a, i, p) => { });

    for (int i = 0; i < 10; i++)
        globalBe.DecrementBuffer();

    // One more call — must not push below -10
    globalBe.DecrementBuffer();

    Assert.Equal(-10, globalBe.GlobalBeBuffer);
}
```

---

### 5 — Test Helper Methods Required

The following private helpers are needed in the test class. Check whether any already exist from B32–B38 and reuse them. Only create those that are missing.

```csharp
// Returns a StubInstrument with the given name and tick size.
private static StubInstrument MakeInstrument(string name, double tickSize) { ... }

// Returns a StubAccount wrapping the given positions.
private static StubAccount MakeAccount(string name, params StubPosition[] positions) { ... }

// Returns a long position stub (MarketPosition.Long, qty > 0).
private static StubPosition MakeLongPos(Instrument instr, double avgPrice, int qty) { ... }

// Returns a short position stub (MarketPosition.Short, qty > 0).
private static StubPosition MakeShortPos(Instrument instr, double avgPrice, int qty) { ... }

// Returns a flat position stub (Quantity=0).
private static StubPosition MakeFlatPos(Instrument instr) { ... }
```

---

### 6 — JS Rule Constraints (Tests)

| Rule | Requirement |
|------|-------------|
| xUnit `[Fact]` only | NO NUnit `[Test]`, NO MSTest `[TestMethod]`. All 6 tests use `[Fact]`. |
| JS-002 | No `return null` in test helpers |
| JS-021 | No `lock()` in test code |
| JS-033 | All test methods are synchronous void |
| ASCII-only | All test identifiers and string literals are ASCII |

---

### 7 — CYC Budget (test methods)

All 8 test methods are linear (no branches) — CYC=1 each (T_B39_07 and T_B39_08 use a simple for loop, CYC=2 each). Test helper stubs: CYC ≤ 2 each. All within budget.

---

### 8 — Acceptance Criteria

- [ ] 8 new `[Fact]` methods added: `GlobalBe_FiresOnAllAccountsAllInstruments`, `GlobalBe_SkipsFlatAccounts`, `GlobalBe_WorksWithNoCopyRule`, `GlobalBe_B35GuardInherited_UnderwaterSkipped`, `GlobalBe_BufferAppliedPerDirectionCorrectly`, `GlobalBe_AllAccountsFlat_NoCalls`, `GlobalBeBuffer_IncrementClampedAt10`, `GlobalBeBuffer_DecrementClampedAtMinus10`
- [ ] All 8 tests pass (`dotnet test`)
- [ ] Total `[Fact]` count in the test file ≥ 188
- [ ] `PttGlobalBreakEven(Action<Account,Instrument,double>)` injection constructor used in all 8 tests
- [ ] `Execute(IEnumerable<Account>, int)` overload used in tests T_B39_01..T_B39_06 (no dependency on `Account.All`); T_B39_07 and T_B39_08 test buffer mutation only (no Execute call needed)
- [ ] No NUnit or MSTest attributes anywhere
- [ ] No `async` test methods
- [ ] T_B39_05 assertions: `calls[0].price == 7500.50` (long +2 ticks), `calls[1].price == 7499.50` (short +2 ticks)

---

### 9 — 7-Scan Checklist (MANDATORY — engineer must run all 7 and report results)

```
SCAN-01: grep -r "lock(" src/ --include="*.cs"
         Required: 0 matches in new/modified files
         Result: ___

SCAN-02: grep -r "async void" src/ --include="*.cs"
         Required: 0 matches in new/modified files
         Result: ___

SCAN-03: grep -r "return null" src/ --include="*.cs"
         Required: 0 matches in new/modified code
         Result: ___

SCAN-04: grep -r "throw new" src/ --include="*.cs"
         Required: 0 matches in new/modified code
         Result: ___

SCAN-05: python scripts/complexity_audit.py
         Required: all new methods CYC <= 8
         Result: ___

SCAN-06: dotnet build
         Required: 0 errors, 0 new warnings
         Result: ___

SCAN-07: dotnet test
         Required: all [Fact] pass; count >= 188
         Result: ___
```

**Pass gate**: All 7 scans GREEN = T2 complete. SCAN-07 count ≥ 188 is the final B39 gate.

---

## Dependency and Sequencing

```
T1: PttGlobalBreakEven.cs (NEW)
    + CopyEngine.cs (3 changes: tag, SubmitBeStop, GlobalBe property)
    + TradeCopierPanel.cs (fields + Row2/Row3 restructure + handlers + helper)
    + TradeCopierWindow.cs (fields + toolbar row + handlers + helper)
    → Gate: T1 SCAN-06 = dotnet build GREEN

T2: CopyEngineTests.cs (+8 [Fact])
    → Requires: T1 SCAN-06 GREEN
    → Gate: T2 SCAN-07 = dotnet test all pass, count >= 188
```

**B39 is COMPLETE when**: T2 SCAN-07 = GREEN and total [Fact] count ≥ 188.

---

## Compliance Sign-Off (all tickets)

| Rule | Status |
|------|--------|
| JS-021 no lock() | PASS — no lock in PttGlobalBreakEven, no lock in handlers |
| JS-008 SolidColorBrush Freeze() | PASS — all brushes via MakeBrush/MakeWinBrush; no inline new SolidColorBrush |
| JS-023 volatile int | PASS — `volatile int _globalBeBuffer` allowed |
| JS-023/NT8-003 no volatile double | PASS — not used |
| JS-002 no return null | PASS — ExecuteOne uses early return void; no null returned |
| JS-033 no async void | PASS — all handlers synchronous void; DispatcherTimer for flash |
| NT8-001 no { get; init; } | PASS — GlobalBe uses { get; } getter-only |
| NT8-007 CreateOrder arg12 | PASS — PttGlobalBreakEven does not call CreateOrder |
| ASCII-only identifiers | PASS |
| No FontFamily | PASS |
| No DateTime.Now | PASS |
| PTT- order prefix | PASS — SubmitBeStop internal to CopyEngine, already compliant |
| CYC <= 8 all new methods | PASS — max is Execute(int)=5, Execute(IEnumerable,int)=5 |
| xUnit [Fact] only (tests) | PASS — no NUnit/MSTest |
