# PTT-COPIER-B14 Architecture Plan
# Block: PTT-COPIER-B14
# Date: 2026-07-14
# Author: ptt-architect (Phase 1)
# Input: docs/brain/PTT-COPIER-B13/06-deferred-backlog.md (OPEN items)
# Status: PLAN_COMPLETE

---

## 1. Scope Summary

### In-scope (exactly 2 items)

| ID | Description | Priority | Ticket |
|----|-------------|----------|--------|
| DW-B12-DEFER-02 (original) | Auto-trail stop from BE CONNECTED state. Once the BE FSM reaches CONNECTED, automatically advance the stop order as price (UnrealizedPnL) moves further in profit. | P3 | T1 |
| DW-B12-DEFER-04 | Align CopyEngineTests.cs implemented test names with the contract names specified in 04-tickets.md §T1 §1.10 for audit trail clarity. | P3 | T2 |

### Shelved (do NOT implement in B14)

| ID | Description | Reason |
|----|-------------|--------|
| DW-B9-01 | ATR box visualization on chart canvas | Carry-forward from B9/B13; no canvas drawing scope in B14 |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset | BLOCKED on DW-B8-04 (price lookup unresolved) |
| DW-B12-DEFER-01 (original) | Full-panel mode expansion: Buy Ask/Sell Bid quick-entry buttons | No scope in B14 |
| DW-B8-04 | Fix click trader price lookup (Y-to-price axis conversion) | Pre-requisite for DW-B9-03; separate P2 item |

---

## 2. Item 1 Design — DW-B12-DEFER-02: Auto-Trail Stop from BE CONNECTED

### 2.1 Architectural Context

The BE FSM in B12 established three panel states: Idle → Armed → Connected.
`CopyEngine.ArmPendingBe` / `DisarmPendingBe` / `OnPendingBeAccountUpdate` implement
a **one-shot** trigger: the engine subscribes `AccountItemUpdate`, waits for
`UnrealizedProfitLoss >= 0`, then fires exactly once (CAS disarm) and raises `PendingBeFired`.

The auto-trail is a **continuous** watcher that takes over once CONNECTED:
- Stays subscribed to `AccountItemUpdate` until explicitly disarmed.
- On each tick where `UnrealizedPnL` beats the previously recorded high-water mark,
  advances the buffer by 1 tick and calls `BreakEven(instrument, newBuffer)`.
- Stores the high-water PnL as a `volatile long` (BitConverter encoding) per NT8-003
  (`volatile double` is banned — CS0677).

### 2.2 New Fields — CopyEngine.cs

Add after the existing `// B10 T2 -- Pending BE fields` block:

```csharp
// B14 T1 -- Auto-trail BE fields (volatile int state machine; JS-023; NT8-003).
// Pattern: mirrors ArmPendingBe/DisarmPendingBe release-fence protocol.
// _trailBeLastPnl: volatile long via BitConverter.DoubleToInt64Bits (NT8-003: volatile double banned).
private volatile int    _trailBeState        = 0;  // 0=Off, 1=Active
private volatile int    _trailBeBufferTicks   = 2;
private volatile long   _trailBeLastPnl       = 0L; // BitConverter.DoubleToInt64Bits(0.0)
private          Account    _trailBeAccount    = null; // single-writer UI thread
private          Instrument _trailBeInstrument = null; // single-writer UI thread
```

**Thread-safety rationale (identical to ArmPendingBe pattern):**
- `_trailBeState = 1` is written LAST in `ArmTrailBe`, establishing the volatile release fence over
  the preceding plain-ref writes to `_trailBeAccount` and `_trailBeInstrument`.
- `OnTrailBeAccountUpdate` reads `_trailBeState` FIRST (volatile acquire fence), ensuring
  the plain-ref reads of `_trailBeAccount` and `_trailBeInstrument` are ordered after.

### 2.3 New Methods — CopyEngine.cs

#### ArmTrailBe

```csharp
// B14 T1 -- ArmTrailBe: arms the continuous trail watcher using acc.AccountItemUpdate.
// CYC=4: instr null(1), acc null(2), pos flat(3), arm write(4).
// Called on UI thread (from TradeCopierPanel.OnBeConnected via Dispatcher).
// _trailBeState volatile write (=1) is the release fence; plain ref writes precede it.
// JS-021: no lock -- Interlocked used in OnTrailBeAccountUpdate for PnL CAS.
// NT8-003: _trailBeLastPnl stored as volatile long (BitConverter.DoubleToInt64Bits).
internal void ArmTrailBe(Instrument instr, Account masterAcc, int bufferTicks)
{
    if (instr == null)                                    // (1)
        return;
    if (masterAcc == null)                                // (2)
        return;
    var pos = FindPosition(masterAcc, instr);
    if (IsFlat(pos))                                      // (3)
        return;
    // Seed last-PnL to current UnrealizedPnL (via Account.Get) to avoid instant spurious advance.
    double currentPnl = masterAcc.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar);
    if (currentPnl == double.MinValue) currentPnl = 0.0;
    _trailBeBufferTicks   = bufferTicks;                  // volatile int write
    _trailBeLastPnl       = BitConverter.DoubleToInt64Bits(currentPnl); // volatile long write (NT8-003)
    _trailBeInstrument    = instr;                        // plain ref write (UI thread)
    _trailBeAccount       = masterAcc;                    // plain ref write (UI thread)
    masterAcc.AccountItemUpdate += OnTrailBeAccountUpdate;
    _trailBeState         = 1;                            // (4) volatile int write -- release fence
}
```

CYC: 4. PASS ≤8.

#### DisarmTrailBe

```csharp
// B14 T1 -- DisarmTrailBe: disarms the trail watcher atomically.
// CYC=2: active CAS check(1), acc null guard(2).
// JS-021: no lock -- Interlocked.CompareExchange for atomic disarm.
// Idempotent: safe to call when already Off.
internal void DisarmTrailBe()
{
    if (Interlocked.CompareExchange(ref _trailBeState, 0, 1) != 1) // (1) only if Active
        return;
    var acc = _trailBeAccount;
    if (acc != null)                                      // (2)
        acc.AccountItemUpdate -= OnTrailBeAccountUpdate;
    _trailBeAccount    = null;
    _trailBeInstrument = null;
}
```

CYC: 2. PASS ≤8.

#### OnTrailBeAccountUpdate

```csharp
// B14 T1 -- OnTrailBeAccountUpdate: continuous AccountItemUpdate callback for auto-trail.
// Fires on NT8 account background thread -- NO UI calls inside this method.
// CYC=5: state check(1), item filter(2), pnl improvement check(3),
//        CAS update _trailBeLastPnl(4), advance buffer + BreakEven(5).
// JS-021: no lock -- Interlocked.Exchange for atomic PnL high-water update.
// JS-001: BreakEven internally wraps acc.Change() in try/catch; no rethrow here.
// NT8-003: _trailBeLastPnl is volatile long; conversion via BitConverter (not volatile double).
// STAYS SUBSCRIBED until DisarmTrailBe() is called -- unlike OnPendingBeAccountUpdate (one-shot).
private void OnTrailBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)
{
    if (_trailBeState != 1)                                         // (1) volatile int read
        return;
    if (e.AccountItem != AccountItem.UnrealizedProfitLoss)         // (2) filter
        return;
    double newPnl = e.Value;
    // (3) improvement check: new PnL must beat stored high-water mark
    double oldPnl = BitConverter.Int64BitsToDouble(
        Interlocked.Read(ref _trailBeLastPnl));
    if (newPnl <= oldPnl)                                           // (3)
        return;
    // (4) atomic high-water update: CAS to win the race (only one callback advances per tick)
    long newBits = BitConverter.DoubleToInt64Bits(newPnl);
    long oldBits = BitConverter.DoubleToInt64Bits(oldPnl);
    if (Interlocked.CompareExchange(ref _trailBeLastPnl, newBits, oldBits) != oldBits) // (4)
        return; // another concurrent callback already updated -- skip duplicate advance
    // (5) advance buffer by 1 tick and call BreakEven
    int newBuffer = Interlocked.Increment(ref _trailBeBufferTicks); // (5) atomic increment
    var instr = _trailBeInstrument;
    if (instr != null)
        BreakEven(instr, newBuffer);                                // BreakEven per-account via acc.Change()
}
```

CYC: 5. PASS ≤8.

### 2.4 Modified: TradeCopierPanel.cs — OnBeConnected

Add `engine.ArmTrailBe(...)` call after the existing `engine.BreakEven(...)` call:

```csharp
// B14 T1 -- after existing B12 BreakEven call, arm the continuous trail watcher.
if (_instrument != null && _leaderAccount != null)
    _engine.ArmTrailBe(_instrument, _leaderAccount, _beBuffer);
```

Full `OnBeConnected` body after modification:
```csharp
private async void OnBeConnected(string instr)
{
    if (_beBtn2 == null) return;                                   // (1)
    _beState = BeState.Connected;                                  // (2)
    UpdateBeVisuals(BeState.Connected);
    if (_instrument != null)
        _engine.BreakEven(_instrument, _beBuffer);
    // B14 T1 -- arm continuous trail BE
    if (_instrument != null && _leaderAccount != null)
        _engine.ArmTrailBe(_instrument, _leaderAccount, _beBuffer);
    await System.Threading.Tasks.Task.CompletedTask;
}
```

CYC remains 2 (additional null-guards are inside the single existing guarded block).
Actually: _instrument null check (already exists) + _leaderAccount null check = 1 new branch point.
New CYC = 3. Still PASS ≤8.

### 2.5 Modified: TradeCopierPanel.cs — OnBeClick (Connected → Idle case)

Add `engine.DisarmTrailBe()` call in the `case BeState.Connected:` block, alongside the existing `engine.DisarmPendingBe()`:

```csharp
case BeState.Connected:               // (5)
    _engine.DisarmPendingBe();
    _engine.DisarmTrailBe();          // B14 T1 -- disarm continuous trail
    _beState = BeState.Idle;
    UpdateBeVisuals(BeState.Idle);
    break;
```

CYC unchanged (5). PASS ≤8.

### 2.6 Cleanup: DisarmTrailBe on Panel Unload / Cleanup

The engineer MUST wire `_engine.DisarmTrailBe()` in the panel's unload/cleanup path —
the same location where `_engine.DisarmPendingBe()` is called on panel close/unload.
This prevents dangling `AccountItemUpdate` subscription if the panel is closed while
in CONNECTED state. The exact cleanup method name is `OnUnloaded` or `OnClosed` in
`TradeCopierPanel.cs` — the engineer must locate it and add the call.

### 2.7 xUnit Tests — T1 (CopyEngineTests.cs)

All tests use `[Fact]` (xUnit). All are guard-path and signature tests (NT8 runtime types
not available in test context).

#### T1-Test-A: ArmTrailBe_MethodExists_WithCorrectSignature
```csharp
[Fact]
public void ArmTrailBe_MethodExists_WithCorrectSignature()
// assert: typeof(CopyEngine).GetMethod("ArmTrailBe", NonPublic|Instance) != null
//         method.GetParameters().Length == 3
//         (Instrument, Account, int)
```

#### T1-Test-B: ArmTrailBe_NullInstrument_NoException
```csharp
[Fact]
public void ArmTrailBe_NullInstrument_NoException()
// act: _engine.ArmTrailBe(null, null, 2)
// assert: Record.Exception() == null
// _trailBeState (via reflection) == 0 (guard fires before arm write)
```

#### T1-Test-C: DisarmTrailBe_WhenNotArmed_NoException
```csharp
[Fact]
public void DisarmTrailBe_WhenNotArmed_NoException()
// act: _engine.DisarmTrailBe()   (never armed)
// assert: Record.Exception() == null
```

#### T1-Test-D: DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall
```csharp
[Fact]
public void DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall()
// act: _engine.DisarmTrailBe(); _engine.DisarmTrailBe();
// assert: Record.Exception() == null
```

#### T1-Test-E: TrailBe_BitConverter_PnlEncoding_RoundTrip
```csharp
[Fact]
public void TrailBe_BitConverter_PnlEncoding_RoundTrip()
// arrange: double pnl = 250.75
// act: long bits = BitConverter.DoubleToInt64Bits(pnl)
//      double recovered = BitConverter.Int64BitsToDouble(bits)
// assert: Assert.Equal(pnl, recovered)
// Verifies NT8-003 BitConverter pattern used by _trailBeLastPnl.
```

#### T1-Test-F: TrailBe_InitialPnlSeed_NoBitsEqualForNegativeAnd0
```csharp
[Fact]
public void TrailBe_CasLogic_NewBitsGreaterThanOld_CasSucceeds()
// arrange: simulate CAS update pattern
//   oldPnl = 50.0; newPnl = 75.0
//   long oldBits = BitConverter.DoubleToInt64Bits(50.0)
//   long newBits = BitConverter.DoubleToInt64Bits(75.0)
// act: simulate long field; CAS oldBits -> newBits
//   long field = oldBits;
//   bool success = Interlocked.CompareExchange(ref field, newBits, oldBits) == oldBits;
// assert: success == true; field == newBits (improvement wins CAS)
// Verifies the CAS idiom used in OnTrailBeAccountUpdate branch (4).
```

CYC: all 6 tests = CYC 1 (no branch logic beyond null guard in T1-Test-B).

---

## 3. Item 2 Design — DW-B12-DEFER-04: Test Name Alignment

### 3.1 Problem Statement

The B12 §T1 §1.10 contract specified 5 test names. The engineer implemented the behaviors
under different names. The audit trail breaks — reviewers cannot match test names to spec.

This ticket is **rename-only** for 4 tests and **add-new** for 1 missing test.
**No existing test logic changes.** Only method names change.

### 3.2 Exact Mapping Table

| Contract Name (B12 §T1 §1.10) | Current Name (CopyEngineTests.cs) | Action |
|-------------------------------|----------------------------------|--------|
| `Trim_LimitOverload_LongPosition_EmitsSellLimitAtRefPlusTick` | `Trim_LongPosition_EmitsLimitSellAtBidPlusBuffer` | RENAME |
| `Trim_LimitOverload_ShortPosition_EmitsBuyToCoverLimitAtRefMinusTick` | *(not present)* | ADD NEW |
| `Flatten_LimitOverload_LongPosition_EmitsSellLimitFullQty` | `Flatten_LongPosition_EmitsLimitSellAtBidPlusBuffer` | RENAME |
| `Flatten_LimitOverload_ShortPosition_EmitsBuyToCoverLimitFullQty` | `Flatten_ShortPosition_EmitsLimitBuyAtAskMinusBuffer` | RENAME |
| `DispatchCopy_PttPrefixGate_SkipsOrderNamedPttTrimLimit` | `PttPrefixGate_SkipsDispatchForPttOrders` | RENAME |

### 3.3 Rename Strategy

For each of the 4 renames: change only the `public void <OldName>()` method declaration line.
**Do not change any test body, comments, or assertions.** CYC unchanged for all renames.

### 3.4 New Test: Trim_LimitOverload_ShortPosition_EmitsBuyToCoverLimitAtRefMinusTick

The B12 T-B12-03 test (`Trim_LongPosition_EmitsLimitSellAtBidPlusBuffer`) covers only the
long path with null instrument. The short path is not tested at all. This new test mirrors
T-B12-03 but documents the short direction:

```csharp
// T1-Test-2 (B14 T2 addition): Trim(Instrument, int, double) -- short-position limit buy path.
// Verifies the 3-arg Trim overload exists with correct signature and that the
// short direction (BuyToCover @ refPrice - exitBuffer*tickSize) exits cleanly on null instrument.
[Fact]
public void Trim_LimitOverload_ShortPosition_EmitsBuyToCoverLimitAtRefMinusTick()
{
    // Verify 3-arg overload exists (Instrument, int exitBuffer, double refPrice).
    var mi = typeof(CopyEngine).GetMethod(
        "Trim",
        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public,
        null,
        new[] { typeof(Instrument), typeof(int), typeof(double) },
        null);
    Assert.NotNull(mi);
    Assert.Equal(3, mi.GetParameters().Length);

    // Signal name used for the limit-buy path must start with "PTT-" (NT8 constraint).
    const string signalName = "PTT-TrimLimit";
    Assert.True(signalName.StartsWith("PTT-", StringComparison.Ordinal),
        "Trim limit signal name must start with PTT-");

    // Short: BuyToCover Limit @ refPrice - exitBuffer*tickSize.
    // null instrument -> AllAccounts returns empty -> no orders issued -> no exception.
    var ex = Record.Exception(() => _engine.Trim(null, 2, 100.0));
    Assert.Null(ex);
}
```

CYC: 1 (null instrument guard path, no explicit branch in test body). PASS.

---

## 4. Files Touched

### Wave Workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`)

| File | Ticket | Change Type |
|------|--------|-------------|
| `CopyEngine.cs` | T1 | Add 5 fields + 3 methods (ArmTrailBe, DisarmTrailBe, OnTrailBeAccountUpdate) |
| `TradeCopierPanel.cs` | T1 | Modify OnBeConnected (add ArmTrailBe call); modify OnBeClick Connected case (add DisarmTrailBe); modify cleanup/unload path (add DisarmTrailBe) |
| `CopyEngineTests.cs` | T1 + T2 | T1: Add 6 new test methods. T2: 4 method renames + 1 new test method. Total new methods: 7 |

### Director Workspace (`c:\WSGTA\universal-or-strategy-director\`) — PLAN OUTPUT ONLY

| File | Action |
|------|--------|
| `docs/brain/PTT-COPIER-B14/02-architecture-plan.md` | THIS FILE (write by ptt-architect) |
| `docs/brain/PTT-COPIER-B14/04-tickets.md` | Written in Phase 3 |

---

## 5. CYC Summary — All New / Modified Methods

| Method | File | Old CYC | New CYC | Limit | Status |
|--------|------|---------|---------|-------|--------|
| `ArmTrailBe` | CopyEngine.cs | n/a | 4 | 8 | PASS |
| `DisarmTrailBe` | CopyEngine.cs | n/a | 2 | 8 | PASS |
| `OnTrailBeAccountUpdate` | CopyEngine.cs | n/a | 5 | 8 | PASS |
| `OnBeConnected` | TradeCopierPanel.cs | 2 | 3 | 8 | PASS |
| `OnBeClick` | TradeCopierPanel.cs | 5 | 5 | 8 | PASS (unchanged) |
| 6 new xUnit tests (T1) | CopyEngineTests.cs | n/a | 1 each | 8 | PASS |
| 4 renamed tests (T2) | CopyEngineTests.cs | 1 each | 1 each | 8 | PASS (renames only) |
| 1 new xUnit test (T2) | CopyEngineTests.cs | n/a | 1 | 8 | PASS |

---

## 6. NT8 Constraints Applied

| Constraint | Rule | Application in B14 |
|------------|------|-------------------|
| `volatile double` BANNED | NT8-003 | `_trailBeLastPnl` is `volatile long`; encoded via `BitConverter.DoubleToInt64Bits` |
| `lock()` BANNED | NT8-018 / JS-021 | `Interlocked.CompareExchange` for disarm CAS; `Interlocked.Exchange` / `Interlocked.Read` for PnL |
| `async void` BANNED except event handlers | NT8-019 / JS-033 | `OnTrailBeAccountUpdate` is plain `void` (account bg thread). `OnBeConnected` is `async void` invoked via `Dispatcher.InvokeAsync` — PERMITTED |
| `Interlocked` requires `using System.Threading` | NT8-031 | `using System.Threading;` already present in CopyEngine.cs (B10 T2). No new using needed. |
| `order.TrailPrice` does not exist | NT8-026 | Trail detection uses `OrderType.StopMarket` via `IsTrailingStop()` (existing method). No `TrailPrice` reference. |
| CreateOrder arg 12 is `CustomOrder` | NT8-007 | Not applicable — trail uses `acc.Change()` only; no `CreateOrder` in trail methods. |
| `DateTime.MaxValue` for GTC | NT8-013 | Not applicable — no new `CreateOrder` calls. |
| Signal name must start with `"PTT-"` | NT8-014 | Not applicable — no new order signals. |
| `SolidColorBrush.Freeze()` | NT8-020 | No new brushes in B14. Existing `BrushConnected` (B12 T1) already frozen via `MakeBrush()`. |
| `Math.Clamp` BANNED | NT8-034 | Not used. No clamp operations in trail methods. |

---

## 7. Data Flow Summary

```
[User clicks BE button — Idle]
    Panel.OnBeClick → engine.ArmPendingBe(instr, masterAcc, beBuffer)
    Panel._beState = Armed

[UnrealizedPnL >= 0 on master account — NT8 account bg thread]
    CopyEngine.OnPendingBeAccountUpdate → CAS disarm → BreakEven(instr, buf) → PendingBeFired event

[Panel receives PendingBeFired — via Dispatcher.InvokeAsync]
    Panel.OnBeConnected():
        _beState = BeState.Connected
        UpdateBeVisuals(Connected)
        engine.BreakEven(_instrument, _beBuffer)
        engine.ArmTrailBe(_instrument, _leaderAccount, _beBuffer)  [B14 NEW]

[UnrealizedPnL improves on every tick — NT8 account bg thread]
    CopyEngine.OnTrailBeAccountUpdate():
        (1) state check: _trailBeState == 1
        (2) filter: AccountItem.UnrealizedProfitLoss only
        (3) improvement check: newPnl > oldPnl (BitConverter decode)
        (4) CAS update: Interlocked.CompareExchange(_trailBeLastPnl, newBits, oldBits)
        (5) advance: Interlocked.Increment(_trailBeBufferTicks) → BreakEven(instr, newBuf)

[User clicks BE button — Connected state]
    Panel.OnBeClick case Connected→Idle:
        engine.DisarmPendingBe()
        engine.DisarmTrailBe()         [B14 NEW]
        _beState = Idle
        UpdateBeVisuals(Idle)

[Panel unload/close]
    Existing cleanup path +:
        engine.DisarmTrailBe()         [B14 NEW — guard dangling subscription]
```

---

## 8. Scan Checklist (7 Scans)

Files touched in Wave workspace: `CopyEngine.cs`, `TradeCopierPanel.cs`, `CopyEngineTests.cs`

| Scan | Check | Expected | Rule |
|------|-------|----------|------|
| SCAN-01 | `grep -n "lock(" CopyEngine.cs TradeCopierPanel.cs` | 0 results in new/modified code | JS-021 P0 |
| SCAN-02 | `grep -n "async void " CopyEngine.cs` | 0 results (OnTrailBeAccountUpdate is plain void) | JS-033 P0 |
| SCAN-03 | `grep -n "return null" CopyEngine.cs TradeCopierPanel.cs` new methods | 0 results — early returns use bare `return;` | JS-002 P0 |
| SCAN-04 | CYC audit: ArmTrailBe, DisarmTrailBe, OnTrailBeAccountUpdate, OnBeConnected | All <= 8 (4, 2, 5, 3) | CYC gate |
| SCAN-05 | `grep -n "volatile double\|volatile long" CopyEngine.cs` new fields | `_trailBeLastPnl` is `volatile long` (PASS), not `volatile double` | NT8-003 |
| SCAN-06 | `grep -n "Math.Clamp" CopyEngine.cs TradeCopierPanel.cs` | 0 results | NT8-034 |
| SCAN-07 | `grep -n "BitConverter.Int64BitsToDouble\|BitConverter.DoubleToInt64Bits" CopyEngine.cs` | Present in ArmTrailBe and OnTrailBeAccountUpdate | NT8-003 compliance |

---

## 9. Jane Street Rule Summary

| Rule | Scope | B14 Status |
|------|-------|------------|
| JS-021 (P0) no `lock()` | All new methods | PASS — Interlocked.CompareExchange, Interlocked.Read, Interlocked.Increment everywhere |
| JS-001 (P0) no throw in hot path | OnTrailBeAccountUpdate | PASS — BreakEven wraps acc.Change() in try/catch internally; OnTrailBeAccountUpdate has no try/catch of its own (zero added throws) |
| JS-002 (P0) no `return null` | All new methods | PASS — all guards use bare `return;` |
| JS-033 (P0) no `async void` except event handlers | New engine methods | PASS — OnTrailBeAccountUpdate is plain `void`; no new async void |
| JS-023 (P1) cross-thread fields must be volatile | All new CopyEngine fields | PASS — _trailBeState, _trailBeBufferTicks, _trailBeLastPnl are volatile; plain refs are single-writer UI thread with volatile release fence |
| JS-008 (P1) SolidColorBrush frozen | Panel | PASS — no new brushes in B14 |

---

## 10. Backlog Ledger

| ID | Description | B14 Action | Next |
|----|-------------|------------|------|
| DW-B12-DEFER-02 (original) | Auto-trail BE from CONNECTED state | CLOSED in B14 T1 | — |
| DW-B12-DEFER-04 | Align test names to contract | CLOSED in B14 T2 | — |
| DW-B8-04 | Click trader price lookup stub (0.0) | REMAINS OPEN | B15+ |
| DW-B9-01 | ATR box visualization on chart canvas | REMAINS SHELVED | B15+ |
| DW-B9-03 | Click trader Bid+1/Ask-1 offset (BLOCKED on DW-B8-04) | REMAINS SHELVED | B15+ |
| DW-B12-DEFER-01 (original) | Full-panel mode expansion | REMAINS SHELVED | B15+ |

---

PLAN_COMPLETE
