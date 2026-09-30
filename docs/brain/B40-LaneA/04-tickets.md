# B40-LaneA Implementation Tickets

**Block**: B40-LaneA — BE ALL Armed/Wait + OCO Collision Fix
**Plan status**: REVIEW_PASS
**Date**: 2026-07-30
**Architect**: ptt-architect

---

## Ticket Index

| Ticket | Title | Files | Closes |
|--------|-------|-------|--------|
| [T1](#t1) | Engine + OCO Fix | CopyEngine.cs, PttGlobalBreakEven.cs | DW-B39-OCO-01 (P0), DW-B39-BEHAVIOR-01 engine side |
| [T2](#t2) | UI Armed State Wiring | TradeCopierPanel.cs, TradeCopierWindow.cs | DW-B39-BEHAVIOR-01 UI side |
| [T3](#t3) | Tests T_B40_01–T_B40_15 | CopyEngineTests.cs | Test coverage for both defects |

**Wave workspace root**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`
**Hard-link sync command** (run after EVERY ticket): `powershell -File scripts\verify_links.ps1 -Fix`

---

<a name="t1"></a>
## T1 — Engine + OCO Fix

### Spec Requirements
- **DW-B39-OCO-01** (P0): OCO ID collision when two accounts share a 4-char account-name prefix
- **DW-B39-BEHAVIOR-01** (P1): Engine-side armed/wait state for BE ALL button

### Files to Modify

| File | Absolute Path |
|------|--------------|
| CopyEngine.cs | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` |
| PttGlobalBreakEven.cs | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttGlobalBreakEven.cs` |

---

### Method Signatures — All New and Modified

#### CopyEngine.cs — New Field (line ~135, near `_pendingBeSlots`)

```csharp
// B40: monotonic OCO sequence counter for ArmAllPendingBe immediate-fire path.
// JS-023: volatile int allowed. NT8-003: volatile double banned -- int is safe.
// Interlocked.Increment called once per ArmAllPendingBe invocation.
private volatile int _beAllOcoSeq = 0;
```

#### CopyEngine.cs — New Method 1 (add after `DisarmPendingBe`)

```csharp
// B40 -- IsPendingSlotsEmpty: returns true when no armed pending-BE slots remain.
// Called by OnPendingBeFiredDispatch to auto-reset _globalBeState to Idle.
// CYC=1: expression body. ConcurrentDictionary.IsEmpty is lock-free (JS-021).
internal bool IsPendingSlotsEmpty() => _pendingBeSlots.IsEmpty;
```

#### CopyEngine.cs — New Method 2 (add after `IsPendingSlotsEmpty`)

```csharp
// B40 -- ComputeBePrice: pure static BE price calculation. Tick-aligned.
// Long:  Math.Round((averageEntryPrice + bufferTicks * tickSize) / tickSize) * tickSize
// Short: Math.Round((averageEntryPrice - bufferTicks * tickSize) / tickSize) * tickSize
// CYC=2: base(1) + isLong ternary for direction(2).
// NT8-029: tick alignment via Math.Round. No lock(). No null return.
// internal (not private) to allow direct testing via [InternalsVisibleTo("CopyEngineTests")].
internal static double ComputeBePrice(Position pos, int bufferTicks)
{
    bool isLong = pos.MarketPosition == MarketPosition.Long;
    double tickSize = pos.Instrument.MasterInstrument.TickSize > 0
        ? pos.Instrument.MasterInstrument.TickSize
        : 0.25;
    double raw = isLong
        ? pos.AveragePrice + bufferTicks * tickSize
        : pos.AveragePrice - bufferTicks * tickSize;
    return Math.Round(raw / tickSize) * tickSize;
}
```

#### CopyEngine.cs — New Method 3 (add after `ComputeBePrice`)

```csharp
// B40 -- IsPriceAlreadyAtBeForAccount: checks if current market price has already
// crossed the BE threshold for a specific account/position.
// Long:  acc.Get(AccountItem.BidPrice) >= averagePrice + bufferTicks * tickSize
// Short: acc.Get(AccountItem.AskPrice) <= averagePrice - bufferTicks * tickSize
// Per-account API: each account uses its own live market data feed.
// CYC=4: null-guard for bid/ask(1), refPx<=0 guard(2), isLong direction(3),
//         long >= / short <= comparison(4).
// JS-021: no lock(). JS-002: returns bool. NT8-003: no volatile double.
private bool IsPriceAlreadyAtBeForAccount(Account acc, Position pos, int bufferTicks)
{
    bool isLong = pos.MarketPosition == MarketPosition.Long;
    double tickSize = pos.Instrument.MasterInstrument.TickSize > 0
        ? pos.Instrument.MasterInstrument.TickSize
        : 0.25;
    double bePrice = ComputeBePrice(pos, bufferTicks);
    double mktPx = isLong
        ? acc.Get(AccountItem.BidPrice, pos.Instrument)
        : acc.Get(AccountItem.AskPrice, pos.Instrument);
    if (mktPx <= 0) return false;
    return isLong ? mktPx >= bePrice : mktPx <= bePrice;
}
```

#### CopyEngine.cs — New Method 4 (add after `IsPriceAlreadyAtBeForAccount`)

```csharp
// B40 -- ArmAllPendingBe: arms all accounts with open positions for pending BE watcher.
// For accounts already past entry+buffer: calls SubmitBeStop immediately with global OCO prefix.
// For accounts not yet triggered: calls ArmPendingBe (subscribes AccountItemUpdate).
// Returns armedCount (accounts that entered Armed state; does NOT count immediate-fires).
// CYC=5: Account.All foreach(1), acc.Positions foreach(2), IsFlat guard(3),
//         IsPriceAlreadyAtBeForAccount branch(4), immediate vs arm branch(5).
// JS-021: no lock(). JS-002: returns int. NT8-021: Account.All post-init only.
internal int ArmAllPendingBe(int bufferTicks)
{
    int seq = System.Threading.Interlocked.Increment(ref _beAllOcoSeq);
    int armedCount = 0;
    int accIdx = 0;
    foreach (Account acc in Account.All)
    {
        foreach (Position pos in acc.Positions)
        {
            if (pos.MarketPosition == MarketPosition.Flat) continue;
            if (IsPriceAlreadyAtBeForAccount(acc, pos, bufferTicks))
            {
                double bePrice = ComputeBePrice(pos, bufferTicks);
                string ocoPrefix = PttGlobalBreakEven.BuildGlobalBeOcoId(seq, accIdx, 0);
                SubmitBeStop(acc, pos.Instrument, bePrice, ocoPrefix);
            }
            else
            {
                ArmPendingBe(pos.Instrument, acc, bufferTicks);
                armedCount++;
            }
        }
        accIdx++;
    }
    return armedCount;
}
```

#### CopyEngine.cs — Modified Method: `SubmitBeStop` (currently at line ~1573)

Add optional 4th parameter `string ocoOverride = null`. Inside the per-pair loop that builds the OCO group ID (currently at line ~1632), replace the existing OCO ID construction with the conditional below. All existing callers pass no 4th argument — the default `null` preserves B39 behavior exactly.

**Existing signature** (line 1573):
```csharp
internal void SubmitBeStop(Account leaderAcc, Instrument instr, double bePrice)
```

**New signature**:
```csharp
internal void SubmitBeStop(Account leaderAcc, Instrument instr, double bePrice, string ocoOverride = null)
```

**Change inside the per-pair loop** — find the line that builds `ocoId` (look for `PTT-BE-` + account name prefix + price key):
```csharp
// BEFORE (existing, ~line 1632):
string ocoId_i = "PTT-BE-" + leaderAcc.Name.Substring(0, 4) + "-" + (int)(bePrice / tickSize) + "-" + i;

// AFTER:
string ocoId_i = ocoOverride != null
    ? (ocoOverride + "-" + i)
    : ("PTT-BE-" + leaderAcc.Name.Substring(0, 4) + "-" + (int)(bePrice / tickSize) + "-" + i);
```

**CopyEngine.cs — Build Tag** (line 41):
Update the `Tag` constant from `"PTT-COPIER B39 | global-be-all | 2026-07-30"` to:
```csharp
internal const string Tag = "PTT-COPIER B40 | be-all-armed-oco-fix | 2026-07-30";
```

---

#### PttGlobalBreakEven.cs — New Field (after `_globalBeBuffer`)

```csharp
// B40: execution counter. Incremented in Execute() via Interlocked.Increment.
// JS-023: volatile int allowed. NT8-003: volatile double banned -- not used here.
// Used as a test assertion hook: test can verify Execute() was called N times.
private volatile int _ocoSeq = 0;
```

#### PttGlobalBreakEven.cs — New Method: `BuildGlobalBeOcoId`

Add this `internal static` method to the class body (location: after `GlobalBeBuffer` property):

```csharp
// B40 DW-B39-OCO-01 FIX: globally unique OCO group ID prefix for BE ALL path.
// Format: "PTT-BEG-{seq:D5}-{accIdx}-{pairIndex}"
// seq       = monotonic per-ArmAllPendingBe-call (Interlocked.Increment on _beAllOcoSeq in engine)
// accIdx    = index of account in the Account.All iteration (0, 1, 2...)
// pairIndex = i from the beTargets loop in SubmitBeStop (appended as ocoOverride+"-"+i in caller)
// CYC=1: pure expression. ASCII-only. No hex. No FontFamily.
// internal static so CopyEngine.ArmAllPendingBe can call it without circular dependency.
internal static string BuildGlobalBeOcoId(int seq, int accIdx, int pairIndex)
    => "PTT-BEG-" + seq.ToString("D5") + "-" + accIdx + "-" + pairIndex;
```

#### PttGlobalBreakEven.cs — Rewrite `Execute(int bufferTicks)` body

The existing `Execute(int bufferTicks)` at line 36 contains an inner loop that calls `ExecuteOne`. Replace the **entire body** of that overload only. Do NOT modify `Execute(IEnumerable<Account>, int)` (the test-seam overload at line 50).

```csharp
internal void Execute(int bufferTicks)
{
    System.Threading.Interlocked.Increment(ref _ocoSeq);
    CopyEngine.Instance.ArmAllPendingBe(bufferTicks);
}
```

`ExecuteOne`, `IncrementBuffer`, `DecrementBuffer`, `GlobalBeBuffer`: **UNCHANGED**.

---

### Step-by-Step Implementation Instructions

1. **Open `CopyEngine.cs`**.
   - Line 41: Update `Tag` constant to `"PTT-COPIER B40 | be-all-armed-oco-fix | 2026-07-30"`.
   - Line ~135: After the `_pendingBeSlots` field declaration, add `private volatile int _beAllOcoSeq = 0;`.
   - After `DisarmPendingBe` method: insert `IsPendingSlotsEmpty` (expression body, CYC=1).
   - After `IsPendingSlotsEmpty`: insert `ComputeBePrice` (CYC=2).
   - After `ComputeBePrice`: insert `IsPriceAlreadyAtBeForAccount` (CYC=4).
   - After `IsPriceAlreadyAtBeForAccount`: insert `ArmAllPendingBe` (CYC=5).
   - Line 1573: Add `string ocoOverride = null` as 4th optional parameter to `SubmitBeStop`.
   - Line ~1632: Replace OCO ID string construction inside the per-pair loop with the null-conditional shown above.

2. **Open `PttGlobalBreakEven.cs`**.
   - After `_globalBeBuffer` field: add `private volatile int _ocoSeq = 0;`.
   - After `GlobalBeBuffer` property: add `BuildGlobalBeOcoId` static method.
   - Replace the **body** of `Execute(int bufferTicks)` with the two-line body above.
   - Leave `Execute(IEnumerable<Account>, int)`, `ExecuteOne`, `IncrementBuffer`, `DecrementBuffer`, `GlobalBeBuffer` completely untouched.

3. **Run hard-link sync**: `powershell -File scripts\verify_links.ps1 -Fix`

4. **Run `dotnet build`** — expect 0 new errors (pre-existing AtrSizingEngine errors are out-of-scope per DW-B39-INFO-01).

---

### 7-Scan Checklist

Run each scan after implementing T1. All must pass before declaring T1 complete.

| # | Command | Expected Result | Enforcement |
|---|---------|-----------------|-------------|
| **SCAN-01** | `Select-String -Path "src/PropTraderTools/CopyEngine.cs","src/PropTraderTools/Features/PttGlobalBreakEven.cs" -Pattern "lock\("` | **0 matches** | JS-021: no lock() anywhere |
| **SCAN-02** | `Select-String -Path "src/PropTraderTools/CopyEngine.cs","src/PropTraderTools/Features/PttGlobalBreakEven.cs" -Pattern "async void "` | **0 matches** | JS-033: async void banned |
| **SCAN-03** | `Select-String -Path "src/PropTraderTools/CopyEngine.cs","src/PropTraderTools/Features/PttGlobalBreakEven.cs" -Pattern "return null;"` | **0 matches in new methods** | JS-002: no null returns |
| **SCAN-04** | `Select-String -Path "src/PropTraderTools/CopyEngine.cs","src/PropTraderTools/Features/PttGlobalBreakEven.cs" -Pattern "throw new "` | **0 matches in new methods** | JS-001: no throw in hot paths |
| **SCAN-05** | `python scripts/complexity_audit.py` | **0 violations** — CYC: IsPendingSlotsEmpty=1, ComputeBePrice=2, IsPriceAlreadyAtBeForAccount=4, ArmAllPendingBe=5, BuildGlobalBeOcoId=1; all ≤ 8 | Jane Street strict standard |
| **SCAN-06** | `dotnet build` | **0 new errors** (pre-existing AtrSizingEngine errors exempt per DW-B39-INFO-01) | Build gate |
| **SCAN-07** | `powershell -File scripts\verify_links.ps1` | **OK=11 DESYNC=0** | Hard-link integrity |

**[Fact] count after T1**: 202 (unchanged — tests are written in T3)

---

<a name="t2"></a>
## T2 — UI Armed State Wiring

### Spec Requirements
- **DW-B39-BEHAVIOR-01** (P1): UI-side armed/wait state for BE ALL button in Panel and Window

**Dependency**: T1 must be complete and building before starting T2. `CopyEngine.IsPendingSlotsEmpty()`, `CopyEngine.ArmAllPendingBe()`, and `CopyEngine.DisarmPendingBe()` must be present.

### Files to Modify

| File | Absolute Path |
|------|--------------|
| TradeCopierPanel.cs | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` |
| TradeCopierWindow.cs | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs` |

---

### Method Signatures — All New and Modified

#### TradeCopierPanel.cs — New Field (near `_beState`, line ~200)

```csharp
// B40: armed/wait state for the BE ALL button. Mirrors _beState for per-account BE+.
// UI-thread only. No volatile needed.
private BeState _globalBeState = BeState.Idle;
```

#### TradeCopierPanel.cs — Rewritten Method: `OnGlobalBeClick`

Replace the existing `OnGlobalBeClick` body entirely. The click handler is a synchronous void event handler — this is the NT8/WPF pattern for button click events and is NOT an `async void` violation (JS-033).

```csharp
// B40: OnGlobalBeClick -- armed/wait FSM for BE ALL. CYC=4.
// Idle->Armed: arm all pending; if at least one slot entered Armed state, turn amber.
// Armed->Idle (manual disarm): loop Account.All and DisarmPendingBe for each; turn purple.
// JS-021: no lock(). JS-033: synchronous void event handler -- not async void.
private void OnGlobalBeClick(object sender, RoutedEventArgs e)
{
    switch (_globalBeState)
    {
        case BeState.Idle:
            CopyEngine.Instance.GlobalBe.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
            if (!CopyEngine.Instance.IsPendingSlotsEmpty())
            {
                _globalBeState = BeState.Armed;
                UpdateBeAllVisuals(BeState.Armed);
            }
            break;
        case BeState.Armed:
            if (Account.All != null)
                foreach (var acc in Account.All)
                    CopyEngine.Instance.DisarmPendingBe(acc);
            _globalBeState = BeState.Idle;
            UpdateBeAllVisuals(BeState.Idle);
            break;
    }
}
```

#### TradeCopierPanel.cs — Updated Method: `OnPendingBeFiredDispatch`

Find the existing `OnPendingBeFiredDispatch` method (called when a pending BE slot fires). After the existing `OnBeConnected(instr, accountName)` call inside the `Dispatcher.InvokeAsync` lambda, add the auto-reset block:

```csharp
// B40: auto-reset _globalBeState when the last armed slot fires.
private void OnPendingBeFiredDispatch(string instr, string accountName)
{
    Dispatcher.InvokeAsync(() =>
    {
        OnBeConnected(instr, accountName);
        if (_globalBeState == BeState.Armed && CopyEngine.Instance.IsPendingSlotsEmpty())
        {
            _globalBeState = BeState.Idle;
            UpdateBeAllVisuals(BeState.Idle);
        }
    });
}
```

If the existing method body already contains the `Dispatcher.InvokeAsync` + `OnBeConnected` pattern, add only the `if (_globalBeState == BeState.Armed ...)` block inside the lambda after the existing call — do not duplicate the Dispatcher wrap.

#### TradeCopierPanel.cs — New Method: `UpdateBeAllVisuals`

Add after `OnPendingBeFiredDispatch`:

```csharp
// B40 -- UpdateBeAllVisuals: purple=Idle, amber=Armed. CYC=2.
// UI-thread only — no Dispatcher wrap needed (all callers are on UI thread).
// BrushPurple and BrushCaution are pre-defined Panel brush fields.
private void UpdateBeAllVisuals(BeState state)
{
    if (_globalBeBtn2 == null) return;
    _globalBeBtn2.Background = state == BeState.Idle ? BrushPurple : BrushCaution;
}
```

**Note**: Confirm the exact field name for the BE ALL button (`_globalBeBtn2` or another name) by searching the Panel for the button wired to `OnGlobalBeClick`. Use that field name.

#### TradeCopierPanel.cs — Updated Method: `Detach()`

After the existing `DisarmPendingBe(_leaderAccount)` call inside `Detach()`, add:

```csharp
// B40: disarm all accounts on detach (BE ALL global cleanup). NT8-043: no null-conditional compound.
if (Account.All != null)
    foreach (var acc in Account.All)
        CopyEngine.Instance.DisarmPendingBe(acc);
_globalBeState = BeState.Idle;
// No visual update here -- panel is being destroyed.
```

---

#### TradeCopierWindow.cs — New Field (near Window-level `_beState` or window state fields)

```csharp
// B40: armed/wait state for the BE ALL button in the window surface.
// UI-thread only. Mirrors TradeCopierPanel._globalBeState.
private BeState _windowGlobalBeState = BeState.Idle;
```

#### TradeCopierWindow.cs — Rewritten Method: `OnWindowGlobalBeClick`

```csharp
// B40: OnWindowGlobalBeClick -- armed/wait FSM. CYC=4. Mirror of Panel OnGlobalBeClick.
// JS-021: no lock(). JS-033: synchronous void event handler.
private void OnWindowGlobalBeClick(object sender, RoutedEventArgs e)
{
    switch (_windowGlobalBeState)
    {
        case BeState.Idle:
            CopyEngine.Instance.GlobalBe.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
            if (!CopyEngine.Instance.IsPendingSlotsEmpty())
            {
                _windowGlobalBeState = BeState.Armed;
                UpdateWindowBeAllVisuals(BeState.Armed);
            }
            break;
        case BeState.Armed:
            if (Account.All != null)
                foreach (var acc in Account.All)
                    CopyEngine.Instance.DisarmPendingBe(acc);
            _windowGlobalBeState = BeState.Idle;
            UpdateWindowBeAllVisuals(BeState.Idle);
            break;
    }
}
```

#### TradeCopierWindow.cs — New Method: `OnWindowPendingBeFiredDispatch`

The Window must subscribe to `CopyEngine.Instance.PendingBeFired` the same way the Panel does. In the Window constructor or `BuildUI` method, add:

```csharp
CopyEngine.Instance.PendingBeFired += OnWindowPendingBeFiredDispatch;
```

In the `Closed` event handler or window teardown, unsubscribe:

```csharp
CopyEngine.Instance.PendingBeFired -= OnWindowPendingBeFiredDispatch;
```

Add the handler method:

```csharp
// B40: auto-reset _windowGlobalBeState when last armed slot fires. Mirror of Panel handler.
private void OnWindowPendingBeFiredDispatch(string instr, string accountName)
{
    Dispatcher.InvokeAsync(() =>
    {
        if (_windowGlobalBeState == BeState.Armed && CopyEngine.Instance.IsPendingSlotsEmpty())
        {
            _windowGlobalBeState = BeState.Idle;
            UpdateWindowBeAllVisuals(BeState.Idle);
        }
    });
}
```

**Note**: If the Window already has an `OnWindowBeConnected` call inside a `PendingBeFired` subscription, add the armed-state check block inside the existing `Dispatcher.InvokeAsync` lambda rather than subscribing twice.

#### TradeCopierWindow.cs — New Method: `UpdateWindowBeAllVisuals`

```csharp
// B40 -- UpdateWindowBeAllVisuals: purple=Idle, amber=Armed. CYC=2.
// WBrushPurple exists at Window line 69. WBrushCaution exists at Window line 65.
// No new brush definitions needed.
private void UpdateWindowBeAllVisuals(BeState state)
{
    if (_windowGlobalBeBtn == null) return;
    _windowGlobalBeBtn.Background = state == BeState.Idle ? WBrushPurple : WBrushCaution;
}
```

**Note**: Confirm the exact field name for the BE ALL window button (`_windowGlobalBeBtn` or another name) by searching the Window for the button wired to `OnWindowGlobalBeClick`. Use that field name.

#### TradeCopierWindow.cs — Updated Window teardown / `Closed` handler

Add a DisarmPendingBe loop in the Window's close/teardown, mirroring Panel's `Detach()` update:

```csharp
// B40: disarm all accounts on window close (BE ALL global cleanup).
if (Account.All != null)
    foreach (var acc in Account.All)
        CopyEngine.Instance.DisarmPendingBe(acc);
_windowGlobalBeState = BeState.Idle;
```

---

### Step-by-Step Implementation Instructions

1. **Open `TradeCopierPanel.cs`**.
   - Line ~200: Add `private BeState _globalBeState = BeState.Idle;` near `_beState`.
   - Find `OnGlobalBeClick`: replace entire body with the switch/FSM shown above.
   - Find `OnPendingBeFiredDispatch`: add the armed-state auto-reset block inside the `Dispatcher.InvokeAsync` lambda after the existing `OnBeConnected` call.
   - Add `UpdateBeAllVisuals(BeState state)` method after `OnPendingBeFiredDispatch`.
   - Find `Detach()`: after `DisarmPendingBe(_leaderAccount)`, add the Account.All loop + `_globalBeState = BeState.Idle`.
   - Search for the exact button field name used with `OnGlobalBeClick` and confirm it matches `_globalBeBtn2`. If different, update `UpdateBeAllVisuals` accordingly.

2. **Open `TradeCopierWindow.cs`**.
   - Add `private BeState _windowGlobalBeState = BeState.Idle;` field.
   - Rewrite `OnWindowGlobalBeClick` body with the switch/FSM shown above.
   - Locate the constructor or `BuildUI` method and add the `PendingBeFired` subscription.
   - Locate the `Closed` or teardown handler and add the `PendingBeFired` unsubscribe + DisarmPendingBe loop.
   - Add `OnWindowPendingBeFiredDispatch` method.
   - Add `UpdateWindowBeAllVisuals(BeState state)` method.
   - Confirm exact button field name for the BE ALL window button (matches `_windowGlobalBeBtn`). Update method if different.

3. **Run hard-link sync**: `powershell -File scripts\verify_links.ps1 -Fix`

4. **Run `dotnet build`** — expect 0 new errors.

---

### 7-Scan Checklist

| # | Command | Expected Result | Enforcement |
|---|---------|-----------------|-------------|
| **SCAN-01** | `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs","src/PropTraderTools/TradeCopierWindow.cs" -Pattern "lock\("` | **0 matches in new/modified methods** | JS-021 |
| **SCAN-02** | `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs","src/PropTraderTools/TradeCopierWindow.cs" -Pattern "async void "` | **0 matches in new methods** | JS-033 |
| **SCAN-03** | `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs","src/PropTraderTools/TradeCopierWindow.cs" -Pattern "return null;"` | **0 matches in new methods** | JS-002 |
| **SCAN-04** | `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs","src/PropTraderTools/TradeCopierWindow.cs" -Pattern "throw new "` | **0 matches in new methods** | JS-001 |
| **SCAN-05** | `python scripts/complexity_audit.py` | **0 violations** — UpdateBeAllVisuals=2, OnGlobalBeClick=4, OnPendingBeFiredDispatch=2, UpdateWindowBeAllVisuals=2, OnWindowGlobalBeClick=4; all ≤ 8 | Jane Street strict standard |
| **SCAN-06** | `dotnet build` | **0 new errors** | Build gate |
| **SCAN-07** | `powershell -File scripts\verify_links.ps1` | **OK=11 DESYNC=0** | Hard-link integrity |

**[Fact] count after T2**: 202 (unchanged — tests are written in T3)

---

<a name="t3"></a>
## T3 — Tests T_B40_01–T_B40_15

### Spec Requirements
- Verify DW-B39-OCO-01 fix: `BuildGlobalBeOcoId` uniqueness guarantees and `SubmitBeStop` ocoOverride path
- Verify DW-B39-BEHAVIOR-01 fix: `ArmAllPendingBe` armed-count semantics and `IsPendingSlotsEmpty` auto-reset
- Verify pure-calculation correctness: `ComputeBePrice` (long + short), `IsPriceAlreadyAtBeForAccount`

**Dependency**: T1 must be complete. `IsPendingSlotsEmpty`, `ArmAllPendingBe`, `ComputeBePrice`, `BuildGlobalBeOcoId` must be present.

### File to Modify

| File | Absolute Path |
|------|--------------|
| CopyEngineTests.cs | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` |

---

### Test Isolation Strategy

- **`BuildGlobalBeOcoId`**: `internal static` — call directly from test.
- **`IsPendingSlotsEmpty`**: `internal` — call directly from test on a `CopyEngine` instance.
- **`ComputeBePrice`**: `internal static` (declared `internal` from T1) — call directly via `[InternalsVisibleTo("CopyEngineTests")]` which is already configured in the project.
- **`SubmitBeStop` ocoOverride**: tested via `CopyEngine.CreateForTest` with a captured OCO-ID hook; the test asserts the constructed string equals `ocoOverride + "-0"` for pair index 0.
- **`IsPriceAlreadyAtBeForAccount`**: `private` — tested indirectly via `ArmAllPendingBe` with fake market data above/below threshold. Direct testing via `PrivateObject` reflection if needed.
- **`ArmAllPendingBe`** tests use stub/fake `Account` and `Position` objects. The engine's test seam (a CopyEngine-level test constructor or `_testMode` flag injecting a fake account list) allows bypassing `Account.All`.

**Insert location**: Add all 15 `[Fact]` tests at the **end** of `CopyEngineTests.cs`, after the last B39 test (`T_B39_08` at line ~3885).

---

### [Fact] Test Definitions — T_B40_01 through T_B40_15

Each test is a separate `[Fact]` method with a `// <ID> - <description>` comment header.
Test numbering matches architecture plan Section 7 exactly (T_B40_01–T_B40_12 per plan, plus T_B40_13–T_B40_15 appended for coverage completeness).

#### T_B40_01 — `ArmAllPendingBe` no open positions → armedCount=0, slots empty

```
// T_B40_01 - ArmAllPendingBe: all accounts flat; armedCount==0; slots remain empty
[Fact]
public void T_B40_01_ArmAllPendingBe_AllFlat_ArmedCountZero()
{
    // Arrange: engine with fake accounts all having Flat positions
    var engine = CopyEngine.CreateForTest(flatAccountList: true);

    // Act
    int armed = engine.ArmAllPendingBe(2);

    // Assert
    Assert.Equal(0, armed);
    Assert.True(engine.IsPendingSlotsEmpty());
}
```

#### T_B40_02 — `ArmAllPendingBe` 2 accounts both past BE threshold → armedCount=0, fires twice

```
// T_B40_02 - ArmAllPendingBe: 2 accounts past BE threshold; SubmitBeStop called twice; armedCount==0
[Fact]
public void T_B40_02_ArmAllPendingBe_TwoPastThreshold_ImmediateFireTwice()
{
    int submitCallCount = 0;
    var engine = CopyEngine.CreateForTest(
        aboveThresholdAccountCount: 2,
        onSubmitBeStop: () => submitCallCount++);

    int armed = engine.ArmAllPendingBe(2);

    Assert.Equal(0, armed);
    Assert.Equal(2, submitCallCount);
    Assert.True(engine.IsPendingSlotsEmpty());
}
```

#### T_B40_03 — `ArmAllPendingBe` 2 accounts both below threshold → armedCount=2, slots non-empty

```
// T_B40_03 - ArmAllPendingBe: 2 accounts both below threshold; armedCount==2; slots non-empty
[Fact]
public void T_B40_03_ArmAllPendingBe_TwoDrawdownAccounts_ArmedCountTwo()
{
    var engine = CopyEngine.CreateForTest(belowThresholdAccountCount: 2);

    int armed = engine.ArmAllPendingBe(2);

    Assert.Equal(2, armed);
    Assert.False(engine.IsPendingSlotsEmpty());
}
```

#### T_B40_04 — `ArmAllPendingBe` mixed: 1 past + 1 not → armedCount=1, fires once

```
// T_B40_04 - ArmAllPendingBe: 1 below threshold (armed), 1 above threshold (fires); armedCount==1
[Fact]
public void T_B40_04_ArmAllPendingBe_MixedAccounts_ArmedCountOne()
{
    int submitCallCount = 0;
    var engine = CopyEngine.CreateForTest(
        belowThresholdAccountCount: 1,
        aboveThresholdAccountCount: 1,
        onSubmitBeStop: () => submitCallCount++);

    int armed = engine.ArmAllPendingBe(2);

    Assert.Equal(1, armed);
    Assert.Equal(1, submitCallCount);
    Assert.False(engine.IsPendingSlotsEmpty());
}
```

#### T_B40_05 — `IsPendingSlotsEmpty` baseline: fresh engine → true; after arm → false

```
// T_B40_05 - IsPendingSlotsEmpty: returns true on fresh engine; false after ArmPendingBe adds a slot
[Fact]
public void T_B40_05_IsPendingSlotsEmpty_FreshEngine_ThenArmed()
{
    var engine = CopyEngine.CreateForTest(belowThresholdAccountCount: 0);
    Assert.True(engine.IsPendingSlotsEmpty()); // fresh, no slots

    var engineWithSlot = CopyEngine.CreateForTest(belowThresholdAccountCount: 1);
    engineWithSlot.ArmAllPendingBe(2);
    Assert.False(engineWithSlot.IsPendingSlotsEmpty()); // slot added
}
```

#### T_B40_06 — `ComputeBePrice` long position: entry=100.0, buffer=2, tickSize=0.25 → 100.5

```
// T_B40_06 - ComputeBePrice: long position, entry=100.0, buffer=2 ticks, tickSize=0.25
//             Math.Round((100.0 + 2*0.25)/0.25)*0.25 == 100.5
[Fact]
public void T_B40_06_ComputeBePrice_Long_ReturnsCorrectBePrice()
{
    // ComputeBePrice is internal static -- callable directly via [InternalsVisibleTo]
    double result = CopyEngine.ComputeBePrice(
        MarketPosition.Long, averageEntryPrice: 100.0, bufferTicks: 2, tickSize: 0.25);

    Assert.Equal(100.5, result, precision: 10);
}
```

**Note**: `ComputeBePrice` takes a `Position pos` parameter in the production signature. For testability, the engineer should add an `internal static` overload (or refactor the body into a parameter-only helper) that accepts `(MarketPosition direction, double averageEntryPrice, int bufferTicks, double tickSize)` — values extractable from `pos`. This is the minimal test-seam addition. The production `ComputeBePrice(Position pos, int bufferTicks)` wraps this helper. If the existing overload already exposes those values directly, call it with a fake `Position` stub.

#### T_B40_07 — `ComputeBePrice` short position: entry=100.0, buffer=2, tickSize=0.25 → 99.5

```
// T_B40_07 - ComputeBePrice: short position, entry=100.0, buffer=2 ticks, tickSize=0.25
//             Math.Round((100.0 - 2*0.25)/0.25)*0.25 == 99.5
[Fact]
public void T_B40_07_ComputeBePrice_Short_ReturnsCorrectBePrice()
{
    double result = CopyEngine.ComputeBePrice(
        MarketPosition.Short, averageEntryPrice: 100.0, bufferTicks: 2, tickSize: 0.25);

    Assert.Equal(99.5, result, precision: 10);
}
```

#### T_B40_08 — `BuildGlobalBeOcoId` exact format: seq=5, accIdx=2, pairIndex=1 → "PTT-BEG-00005-2-1"

```
// T_B40_08 - BuildGlobalBeOcoId: seq=5, accIdx=2, pairIndex=1 → "PTT-BEG-00005-2-1"
[Fact]
public void T_B40_08_BuildGlobalBeOcoId_ExactFormat()
{
    string result = PttGlobalBreakEven.BuildGlobalBeOcoId(5, 2, 1);
    Assert.Equal("PTT-BEG-00005-2-1", result);
}
```

#### T_B40_09 — `BuildGlobalBeOcoId` same-seq, different accIdx → unique IDs

```
// T_B40_09 - BuildGlobalBeOcoId: same seq, different accIdx produces different strings
[Fact]
public void T_B40_09_BuildGlobalBeOcoId_SameSeqDifferentAccIdx_UniqueIds()
{
    string id0 = PttGlobalBreakEven.BuildGlobalBeOcoId(3, 0, 0);
    string id1 = PttGlobalBreakEven.BuildGlobalBeOcoId(3, 1, 0);
    Assert.NotEqual(id0, id1);
    Assert.StartsWith("PTT-BEG-", id0);
    Assert.StartsWith("PTT-BEG-", id1);
}
```

#### T_B40_10 — `SubmitBeStop` ocoOverride path: OCO ID uses ocoOverride+"-"+i

```
// T_B40_10 - SubmitBeStop ocoOverride: when ocoOverride="PTT-BEG-00001-0" is passed,
//             the OCO ID for pair 0 is "PTT-BEG-00001-0-0" (ocoOverride + "-" + 0)
[Fact]
public void T_B40_10_SubmitBeStop_OcoOverride_UsesOverridePlusIndex()
{
    // Arrange: capture the OCO ID string passed to CreateOrder via test hook
    string capturedOcoId = null;
    var engine = CopyEngine.CreateForTest(
        onCreateOrderOcoId: id => capturedOcoId = id);

    // Act: call SubmitBeStop with ocoOverride set
    engine.SubmitBeStop(
        leaderAcc: FakeAccount.Create("TestAcc"),
        instr: FakeInstrument.Create("MNQ"),
        bePrice: 100.0,
        ocoOverride: "PTT-BEG-00001-0");

    // Assert: first pair (i=0) uses ocoOverride + "-0"
    Assert.Equal("PTT-BEG-00001-0-0", capturedOcoId);
}
```

#### T_B40_11 — `IsPriceAlreadyAtBeForAccount` long, bid >= entry+buf → returns true (immediate fire)

```
// T_B40_11 - IsPriceAlreadyAtBeForAccount: long, entry=100.0, buf=2 ticks, tickSize=0.25,
//             bid=100.75 (>= bePrice=100.5) → returns true; ArmAllPendingBe fires immediately
[Fact]
public void T_B40_11_IsPriceAlreadyAtBe_Long_BidAboveThreshold_ReturnsTrue()
{
    // Test via ArmAllPendingBe with bid=100.75: account fires immediately, armedCount=0
    var engine = CopyEngine.CreateForTest(
        longAccountBid: 100.75, entry: 100.0, buffer: 2, tickSize: 0.25);

    int armed = engine.ArmAllPendingBe(2);

    Assert.Equal(0, armed); // fired immediately, not armed
}
```

#### T_B40_12 — `IsPriceAlreadyAtBeForAccount` long, bid < entry+buf → returns false (account armed)

```
// T_B40_12 - IsPriceAlreadyAtBeForAccount: long, entry=100.0, buf=2 ticks, tickSize=0.25,
//             bid=100.25 (< bePrice=100.5) → returns false; account enters Armed state
[Fact]
public void T_B40_12_IsPriceAlreadyAtBe_Long_BidBelowThreshold_ReturnsFalse()
{
    var engine = CopyEngine.CreateForTest(
        longAccountBid: 100.25, entry: 100.0, buffer: 2, tickSize: 0.25);

    int armed = engine.ArmAllPendingBe(2);

    Assert.Equal(1, armed); // not yet triggered, entered Armed state
}
```

#### T_B40_13 — `ArmAllPendingBe` single below-threshold account → armedCount=1, slot present

```
// T_B40_13 - ArmAllPendingBe: 1 account, position below threshold; armedCount==1; slot non-empty
[Fact]
public void T_B40_13_ArmAllPendingBe_OneDrawdownAccount_ArmedCountOne()
{
    // Arrange: one long account, entry=100.0, buffer=2, tickSize=0.25; mktPrice=99.0 (below 100.5)
    var engine = CopyEngine.CreateForTest(longAccountBelowThreshold: true);

    // Act
    int armed = engine.ArmAllPendingBe(2);

    // Assert
    Assert.Equal(1, armed);
    Assert.False(engine.IsPendingSlotsEmpty());
}
```

#### T_B40_14 — `IsPendingSlotsEmpty` after all slots fire → true

```
// T_B40_14 - IsPendingSlotsEmpty: after all armed slots fire, returns true
[Fact]
public void T_B40_14_IsPendingSlotsEmpty_AfterAllSlotsFire_ReturnsTrue()
{
    var engine = CopyEngine.CreateForTest(belowThresholdAccountCount: 1);
    engine.ArmAllPendingBe(2);
    Assert.False(engine.IsPendingSlotsEmpty()); // slot present

    // Simulate the slot firing by removing it via the test seam
    engine.SimulatePendingBeSlotFire();

    Assert.True(engine.IsPendingSlotsEmpty());
}
```

#### T_B40_15 — `IsPendingSlotsEmpty` one remaining → false

```
// T_B40_15 - IsPendingSlotsEmpty: one armed slot remaining (second account not yet fired) → false
[Fact]
public void T_B40_15_IsPendingSlotsEmpty_OneSlotRemaining_ReturnsFalse()
{
    var engine = CopyEngine.CreateForTest(belowThresholdAccountCount: 2);
    engine.ArmAllPendingBe(2);
    Assert.False(engine.IsPendingSlotsEmpty()); // both slots present

    // Fire only one slot
    engine.SimulatePendingBeSlotFire();

    Assert.False(engine.IsPendingSlotsEmpty()); // one slot still present
}
```

---

### Step-by-Step Implementation Instructions

1. **`ComputeBePrice` visibility**: T1 declares it `internal static` (already corrected in T1 above). No visibility change needed in T3.

2. **Add `ComputeBePrice` test-seam overload** (if needed): If `ComputeBePrice(Position pos, int bufferTicks)` cannot be called directly from a test without a live `Position` object, add an `internal static` overload:
   ```csharp
   internal static double ComputeBePrice(MarketPosition direction, double averageEntryPrice, int bufferTicks, double tickSize)
   {
       double raw = direction == MarketPosition.Long
           ? averageEntryPrice + bufferTicks * tickSize
           : averageEntryPrice - bufferTicks * tickSize;
       return Math.Round(raw / tickSize) * tickSize;
   }
   ```
   The production overload (`ComputeBePrice(Position pos, int bufferTicks)`) extracts these values and delegates to this overload. CYC unchanged.

3. **Check for `CopyEngine.CreateForTest` seam**: Search `CopyEngineTests.cs` for existing test factory/seam patterns. If a `CreateForTest` static method or a test constructor exists, use that pattern. If it does not exist, add an `internal static CopyEngine CreateForTest(...)` overload in `CopyEngine.cs` that accepts fake account/position data, an optional `Action onSubmitBeStop` delegate, and an optional `Action<string> onCreateOrderOcoId` delegate — this is the minimal seam needed to test `ArmAllPendingBe` and `SubmitBeStop ocoOverride` without `Account.All`.

4. **Check for `SimulatePendingBeSlotFire` seam**: Search existing tests for how prior B-series tests simulate slot completion. If a `TryRemovePendingSlot` or `SimulateFire` pattern exists, use it. If not, add `internal void SimulatePendingBeSlotFire(string accName = "TestAcc")` that calls `_pendingBeSlots.TryRemove(accName, out _)`.

5. **Append all 15 `[Fact]` tests** to `CopyEngineTests.cs` after `T_B39_08`. Add a section comment `// T_B40_01 through T_B40_15 -- B40 OCO Fix + BE ALL Armed State`.

6. **Run `dotnet test`** — all 217 `[Fact]` tests must pass.

7. **Run hard-link sync**: `powershell -File scripts\verify_links.ps1 -Fix`

---

### 7-Scan Checklist

| # | Command | Expected Result | Enforcement |
|---|---------|-----------------|-------------|
| **SCAN-01** | `Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "lock\("` | **0 matches in new tests** | JS-021 |
| **SCAN-02** | `Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "async void "` | **0 matches in new tests** | JS-033 |
| **SCAN-03** | `Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "return null;"` | **0 matches in new tests** | JS-002 |
| **SCAN-04** | `Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "throw new "` | **0 matches in new tests** | JS-001 |
| **SCAN-05** | `python scripts/complexity_audit.py` | **0 violations** — each `[Fact]` body is pure assertions, CYC=1 per test | Jane Street strict standard |
| **SCAN-06** | `dotnet test` | **217/217 [Fact] passing** (202 baseline + 15 new) | Full test gate |
| **SCAN-07** | `powershell -File scripts\verify_links.ps1` | **OK=11 DESYNC=0** | Hard-link integrity |

**[Fact] count after T3**: **217** (was 202; +15)

---

## Full Scan Summary (All Three Tickets Complete)

| Scan | All-Ticket Expected Result |
|------|---------------------------|
| SCAN-01 `lock(` | 0 hits in all modified files |
| SCAN-02 `async void` | 0 hits in all new methods |
| SCAN-03 `return null;` | 0 hits in new code |
| SCAN-04 `throw new` | 0 hits in new hot-path code |
| SCAN-05 CYC | Max=5 (`ArmAllPendingBe`); all 14 new methods ≤ 8 |
| SCAN-06 dotnet test | **217/217 [Fact] passing** |
| SCAN-07 verify_links.ps1 | OK=11 DESYNC=0 |

## Deferred Items Closed by B40

| Defect ID | Status After B40 |
|-----------|-----------------|
| DW-B39-OCO-01 (P0) OCO ID collision Sim101/Sim102 | CLOSED — `BuildGlobalBeOcoId` + `SubmitBeStop ocoOverride` |
| DW-B39-BEHAVIOR-01 (P1) BE ALL armed/wait missing | CLOSED — `ArmAllPendingBe` + Panel/Window FSM |
| DW-B39-OOS-03 (P2) armed state in `PttGlobalBreakEven` itself | PARTIALLY CLOSED — armed/wait implemented via per-account `ArmPendingBe` path; separate field in `PttGlobalBreakEven` not needed |

---

*Generated by ptt-architect | Phase 3 Ticket Generation | B40-LaneA | 2026-07-30*
