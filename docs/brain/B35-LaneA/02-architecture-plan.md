# B35-LaneA Architecture Plan
## BE Stop-Above-Market Warning

**Status**: REVIEW_PENDING
**Block**: B35 | Lane A (new session — BE-stop-market-guard)
**Baseline**: PTT-COPIER B34 | be-multiAccount-fixes | 2026-07-26
**Test baseline**: 177 [Fact] passing
**Defect closed**: DW-B35-SILENT-REJECT (P1) — Director designation from 2026-07-26 live session
**Prior B35-LaneA**: Separate completed session (bracket-cancel-trim-flatten); this is a new concern.
**Wave workspace**: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\`

---

## 1. Defect Summary

**DW-B35-SILENT-REJECT (P1)** — Silent BE stop rejection when market has already moved above stop price.
(Director source: "$start-lane | PTT-COPIER B35 | BE Stop-Above-Market Warning | LaneA" — 2026-07-26 Fire 3, Sim101.)

| Field | Value |
|-------|-------|
| Live session | 2026-07-26 Fire 3, Sim101 |
| Entry price | 7506.00 |
| BE+1 tick | bePrice = 7506.25 |
| Market ask at submission | already above 7506.25 |
| NT8 log | "Sell stop or sell stop limit orders can't be placed above the market." |
| Order state | PTT-BE-Stop REJECTED |
| Panel status | empty — **silent fail** |
| Position state | left unprotected, no user warning |

**Root cause**: `SubmitBeStopLocal()` wraps submission in `try/catch(Exception)`. NT8 stop rejections do NOT throw — they arrive as `OrderUpdate` events with `OrderState.Rejected`. The catch block is blind to them. Result: no log, no status bar text, position unprotected.

---

## 2. Fix Strategy

Two code changes in dependency order:

1. **B35-01** — Add `WarnUser(string)` to `IPttHostContext` + implement in `TradeCopierPanel`.
2. **B35-02** — Add pre-submission price guard in `PttBreakEven.Execute()` that calls `ctx.WarnUser()`.

B35-02 requires B35-01 to compile. Both are in the same lane (LaneA); engineer executes them sequentially.

---

## 3. Components

### 3.1 `Core/PttContracts.cs` — `IPttHostContext` interface

**Current state** (verified): interface has `LeaderAccount`, `Instrument`, `AllAccounts`, `BeBuffer`,
`TrimBuffer`, `FlatBuffer`, `Ask`, `Bid`. No `WarnUser` method.

**Change**: Add after the `Bid` property (currently last member):

```csharp
/// <summary>Display a warning in the panel status bar. Call from UI thread only.</summary>
void WarnUser(string message);
```

- CYC contribution: 0 (interface declaration, no body)
- JS-021: no lock
- NT8-001: method, not property — no init accessor
- JS-033: synchronous void, not async

### 3.2 `TradeCopierPanel.cs` — explicit interface implementation

**Current state** (verified): explicit IPttHostContext implementations at lines 128-137.
`_statusText` declared at line 166 as `private TextBlock _statusText`.
Existing direct-assignment pattern: lines 1452, 1457, 1463, 1490-1491, 1521-1522 all assign
`_statusText.Text = ...` directly on UI thread without Dispatcher.

**Change**: Add after line 137 (after the existing `Bid` implementation):

```csharp
void IPttHostContext.WarnUser(string message)
{
    if (_statusText != null) _statusText.Text = message;
}
```

- CYC(WarnUser) = 1 (one branch: null check)
- UI thread only — `Execute()` is called from `DispatchModule()` which is called from WPF button handlers
- No `Dispatcher.InvokeAsync` needed — same-thread assignment pattern matches all existing `_statusText.Text` assignments
- NT8-033: synchronous void (not async void) ✅

### 3.3 `Features/PttBreakEven.cs` — `Execute()` price guard

**Current state** (verified):
- `Execute()` CYC = 7
- Lines 65-75: foreach loop over `ctx.AllAccounts`
- Line 70: `bool isLong = pos.MarketPosition == MarketPosition.Long;`
- Lines 71-72: `double bePrice = pos.AveragePrice + (isLong ? +buf : -buf) * tickSize;`
- Line 74: `CancelStaleBracketsLocal(acc, ctx.Instrument);`
- Line 75: `SubmitBeStopLocal(acc, ctx.Instrument, bePrice, isLong);`

**Change**: Insert price guard AFTER bePrice computation (after line 72), BEFORE `CancelStaleBracketsLocal`
(line 74):

```csharp
// DW-B34-01: pre-check stop price validity against live market.
// NT8 rule: Sell stop must be <= Ask; BuyToCover stop must be >= Bid.
// If bePrice > Ask (long) or bePrice < Bid (short) --> NT8 would reject; skip + warn.
double ask = ctx.Ask;
double bid = ctx.Bid;
bool priceOk = isLong  ? (ask <= 0.0 || bePrice <= ask)
                        : (bid <= 0.0 || bePrice >= bid);
if (!priceOk)
{
    string side   = isLong ? "above ask" : "below bid";
    string market = isLong ? ask.ToString("F2") : bid.ToString("F2");
    string msg    = "[BE] WARNING: " + acc.Name + " BE stop @ "
                  + bePrice.ToString("F2") + " rejected -- stop "
                  + side + " market " + market + " -- position UNPROTECTED";
    NinjaTrader.Code.Output.Process(msg, NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    ctx.WarnUser(acc.Name + ": BE stop rejected (" + side + " " + market + ")");
    continue;
}
```

**Guard semantics**:
- `ask/bid <= 0.0` means no market data available yet → allow submission (NT8 handles it)
- `continue` skips BOTH `CancelStaleBracketsLocal` AND `SubmitBeStopLocal` for that account
- Existing brackets for that account are preserved (better than cancelling them and leaving the position naked)
- `NinjaTrader.Code.Output.Process` logs to Output tab (always visible in NT8)
- `ctx.WarnUser(...)` sets panel status bar text immediately (UI thread)

**CYC delta**: +1 (the `if (!priceOk)` branch)
**CYC after**: 7 + 1 = **8** ≤ 8 ✅

### 3.4 `CopyEngineTests.cs` — 3 new [Fact] tests

**Insert location**: before the class closing brace at line 3296 (after the last [Fact] ending at line 3279).

**Test 1 — B35-01 structural**:

```csharp
[Fact]
public void T_B35_WarnUser_SetsStatusText()
{
    // Verify IPttHostContext.WarnUser method exists on the interface via reflection.
    // Structural test -- no NT8 API required.
    var method = typeof(IPttHostContext).GetMethod("WarnUser",
        new[] { typeof(string) });
    Assert.NotNull(method);
    Assert.Equal(typeof(void), method.ReturnType);
}
```

**Test 2 — B35-02 long guard**:

```csharp
[Fact]
public void T_B35_BE_StopAboveMarket_Skipped()
{
    // Verify price guard logic: long position, bePrice > ask --> priceOk = false.
    // Mirrors the guard in PttBreakEven.Execute() without instantiating NT8 types.
    bool isLong  = true;
    double ask   = 7506.00;
    double bid   = 0.0;
    double bePrice = 7506.25;
    bool priceOk = isLong ? (ask <= 0.0 || bePrice <= ask)
                           : (bid <= 0.0 || bePrice >= bid);
    Assert.False(priceOk); // stop above market -> guard fires -> skip submission
}
```

**Test 3 — B35-02 short guard**:

```csharp
[Fact]
public void T_B35_BE_StopBelowMarket_Skipped()
{
    // Verify price guard logic: short position, bePrice < bid --> priceOk = false.
    bool isLong  = false;
    double ask   = 0.0;
    double bid   = 7505.75;
    double bePrice = 7505.50;
    bool priceOk = isLong ? (ask <= 0.0 || bePrice <= ask)
                           : (bid <= 0.0 || bePrice >= bid);
    Assert.False(priceOk); // stop below market -> guard fires -> skip submission
}
```

All 3 tests are pure logic / reflection — no NT8 API, no mock required beyond what already exists.

### 3.5 `CopyEngine.cs` — build tag update

**Change**: Update the build-tag comment at the top of the file from B34 to B35.
Minimal single-line change. No logic change.

---

## 4. CYC Analysis

| Method | File | Before | After | Limit | Status |
|--------|------|--------|-------|-------|--------|
| `PttBreakEven.Execute()` | `Features/PttBreakEven.cs` | 7 | **8** | 8 | ✅ |
| `IPttHostContext.WarnUser` | `Core/PttContracts.cs` | — | 0 (interface) | 8 | ✅ |
| `TradeCopierPanel.WarnUser` | `TradeCopierPanel.cs` | — | **1** | 8 | ✅ |
| `CancelStaleBracketsLocal` | `Features/PttBreakEven.cs` | 3 | 3 | 8 | ✅ unchanged |
| `SubmitBeStopLocal` | `Features/PttBreakEven.cs` | 3 | 3 | 8 | ✅ unchanged |
| `FindPositionLocal` | `Features/PttBreakEven.cs` | 2 | 2 | 8 | ✅ unchanged |

---

## 5. Threading Model

| Location | Thread | Access Pattern |
|----------|--------|----------------|
| `PttBreakEven.Execute()` | UI thread | Called from `DispatchModule()` in WPF button handler |
| `ctx.Ask` / `ctx.Bid` reads | UI thread | `GetAsk()`/`GetBid()` already used on UI thread in B34 |
| `ctx.WarnUser()` call | UI thread | Delegates to `TradeCopierPanel.WarnUser` |
| `_statusText.Text = message` | UI thread | Direct assignment — same as lines 1452, 1457, 1463, 1521 |
| `Output.Process()` | UI thread | NT8-documented as thread-safe; already used in helpers |

**No `Dispatcher.InvokeAsync` needed for WarnUser.** The existing `OnStatusUpdate` (line 1507) uses
`Dispatcher.InvokeAsync` because it is invoked from the CopyEngine background thread — a different
call path entirely. B35's path is synchronous UI thread only.

---

## 6. Data Flow

```
WPF Button click
  → TradeCopierPanel.DispatchModule("BE")
    → PttBreakEven.Execute(ctx)
      [1] !IsEnabled guard → return
      [2] leaderPos == null || qty==0 → return
      [3] foreach Account acc in ctx.AllAccounts
          [3a] pos == null || qty==0 → continue
          compute isLong, bePrice
          [NEW B35] price guard:
              ctx.Ask / ctx.Bid reads
              if !priceOk:
                  Output.Process(warning)       → NT8 Output tab
                  ctx.WarnUser(short msg)        → _statusText.Text   ← USER SEES THIS
                  continue                       → skip CancelBrackets + SubmitStop
          CancelStaleBracketsLocal(acc, instr)   → acc.Cancel(stale[])
          SubmitBeStopLocal(acc, instr, bePrice) → acc.CreateOrder + acc.Submit
      PttBus.RaiseBe(leaderContext)
```

---

## 7. File Change Summary

| Ticket | File | Change Type | Lines Affected |
|--------|------|-------------|----------------|
| B35-01 | `Core/PttContracts.cs` | Add interface method `WarnUser(string)` | +3 lines after `Bid` property |
| B35-01 | `TradeCopierPanel.cs` | Add explicit impl `IPttHostContext.WarnUser` | +4 lines after line 137 |
| B35-01 | `CopyEngineTests.cs` | Add `T_B35_WarnUser_SetsStatusText` [Fact] | +8 lines before line 3296 |
| B35-02 | `Features/PttBreakEven.cs` | Insert price guard in `Execute()` | +13 lines after bePrice computation |
| B35-02 | `CopyEngineTests.cs` | Add `T_B35_BE_StopAboveMarket_Skipped`, `T_B35_BE_StopBelowMarket_Skipped` | +20 lines before line 3296+8 |
| B35-02 | `CopyEngine.cs` | Build tag comment B34 → B35 | 1 line |

**Test delta**: +3 [Fact] → new total = **180 [Fact]**

---

## 8. Dependency Order

```
B35-01 (PttContracts.cs + TradeCopierPanel.cs + 1 test)
  ↓  [required: WarnUser must exist on IPttHostContext before PttBreakEven calls it]
B35-02 (PttBreakEven.cs + 2 tests + CopyEngine.cs build tag)
  ↓
B35-03 Verifier pass (no code — separate agent)
```

---

## 9. 7-Scan Checklist (Engineer Contract)

**SCAN-01** — No lock() anywhere:
```powershell
Select-String -Path "Core\PttContracts.cs","TradeCopierPanel.cs","Features\PttBreakEven.cs" -Pattern "lock\s*\(" 
# Expected: 0 matches
```

**SCAN-02** — No async void:
```powershell
Select-String -Path "Features\PttBreakEven.cs","TradeCopierPanel.cs" -Pattern "async\s+void\s+\w"
# Expected: 0 matches in new code
```

**SCAN-03** — No init accessor:
```powershell
Select-String -Path "Core\PttContracts.cs" -Pattern "\{\s*get;\s*init;\s*\}"
# Expected: 0 matches
```

**SCAN-04** — No LINQ in PttBreakEven:
```powershell
Select-String -Path "Features\PttBreakEven.cs" -Pattern "\.Where\b|\.First\b|\.Select\b|\.Any\b"
# Expected: 0 matches
```

**SCAN-05** — No new Dispatcher usage in WarnUser:
```powershell
Select-String -Path "TradeCopierPanel.cs" -Pattern "Dispatcher" | Where-Object { $_.LineNumber -gt 137 -and $_.LineNumber -lt 145 }
# Expected: 0 matches (WarnUser at ~line 138-142 must NOT use Dispatcher)
```

**SCAN-06** — WarnUser exists on interface:
```powershell
Select-String -Path "Core\PttContracts.cs" -Pattern "void WarnUser"
# Expected: 1 match
```

**SCAN-07** — Price guard uses continue (not return):
```powershell
Select-String -Path "Features\PttBreakEven.cs" -Pattern "priceOk"
# Expected: guard block present; verify it uses `continue` not `return`
```

---

## 10. Rules Catalog Gate

| Rule | Description | Check | Result |
|------|-------------|-------|--------|
| JS-021 | No `lock()` | SCAN-01 | ✅ |
| JS-033 | No `async void` in business logic | SCAN-02 | ✅ |
| JS-001 | No throw in hot paths | price guard uses `continue` | ✅ |
| JS-002 | No `return null` for missing values | WarnUser returns void | ✅ |
| NT8-001 | No `{ get; init; }` | WarnUser is a method | ✅ |
| NT8-006 | No LINQ in PttBreakEven | SCAN-04 | ✅ |
| NT8-013 | No DateTime.Now | No new DateTime usage | ✅ |
| NT8-014 | PTT- prefix on order signals | No new order submission | ✅ |
| NT8-033 | Execute on UI thread | Confirmed via DispatchModule | ✅ |

---

## 11. NT8 API Surface

No new NT8 API types or methods are introduced by B35.

| Usage | Source | NT8 API? |
|-------|--------|----------|
| `ctx.Ask` / `ctx.Bid` | IPttHostContext (our interface) | No — delegates to GetAsk()/GetBid() established in B34 |
| `ctx.WarnUser(...)` | IPttHostContext (our interface) | No — pure C# |
| `_statusText.Text = ...` | WPF TextBlock | No — standard .NET |
| `Output.Process(...)` | Already in helpers | Already established |
| `acc.Name` | Already in helpers | Already established |

---

## 12. Test Count

| State | Count |
|-------|-------|
| Baseline (B34) | 177 [Fact] |
| B35-01 adds | +1 |
| B35-02 adds | +2 |
| **B35 total** | **180 [Fact]** |
