# PTT-COPIER-B14 Implementation Tickets
# Block: PTT-COPIER-B14
# Date: 2026-07-14
# Author: ptt-architect (Phase 3)
# Input: docs/brain/PTT-COPIER-B14/02-architecture-plan.md (Status: REVIEW_PASS)
#        docs/brain/PTT-COPIER-B14/02-plan-review.md (Status: REVIEW_PASS)
# Wave Workspace: c:\WSGTA\universal-or-strategy\src\PropTraderTools\

---

## Spec Requirements Mapping

| Req ID | Description | Ticket |
|--------|-------------|--------|
| DW-B12-DEFER-02 (original) | Auto-trail stop from BE CONNECTED state | T1 |
| DW-B12-DEFER-04 | Align CopyEngineTests.cs test names to B12 §T1 §1.10 contract | T2 |

---

## T1 — DW-B12-DEFER-02: Auto-Trail Stop from BE CONNECTED State

**File(s):**
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`

**Spec req:** DW-B12-DEFER-02 (original) — Once BE FSM reaches CONNECTED, automatically advance
the stop order as UnrealizedPnL moves to a new high-water mark. Stays subscribed until explicitly
disarmed. Stop is NOT advanced backward (only on improvement).

---

### 1.1 New Fields — CopyEngine.cs

**Location:** Insert immediately after line 94, after the existing `// B10 T2 -- Pending BE fields`
block (after `private Instrument _pendingBeInstrument = null;`).

```csharp
// B14 T1 -- Auto-trail BE fields (volatile int state machine; JS-023; NT8-003).
// Pattern: mirrors ArmPendingBe/DisarmPendingBe release-fence protocol.
// _trailBeLastPnl: volatile long via BitConverter.DoubleToInt64Bits (NT8-003: volatile double banned).
private volatile int        _trailBeState        = 0;  // 0=Off, 1=Active
private volatile int        _trailBeBufferTicks   = 2;
private volatile long       _trailBeLastPnl       = 0L; // BitConverter.DoubleToInt64Bits(0.0)
private          Account    _trailBeAccount        = null; // single-writer UI thread
private          Instrument _trailBeInstrument     = null; // single-writer UI thread
```

**NT8 constraints:**
- NT8-003: `volatile double` is banned (CS0677). `_trailBeLastPnl` uses `volatile long` with
  `BitConverter.DoubleToInt64Bits` / `BitConverter.Int64BitsToDouble` for lossless double encoding.
- JS-023: All cross-thread fields are `volatile`. Plain refs (`_trailBeAccount`, `_trailBeInstrument`)
  are single-writer UI thread and protected by the volatile release fence from `_trailBeState = 1`.
- NT8-031: `using System.Threading;` is already present in CopyEngine.cs (added B10 T2). No new
  using directive needed.

**Thread-safety rationale:**
`_trailBeState = 1` is written LAST in `ArmTrailBe`, establishing a volatile release fence over the
preceding plain-ref writes to `_trailBeAccount` and `_trailBeInstrument`. `OnTrailBeAccountUpdate`
reads `_trailBeState` FIRST (volatile acquire fence), ensuring plain-ref reads are ordered after it.

---

### 1.2 ArmTrailBe — CopyEngine.cs

**Location:** Insert as a new `internal` method immediately after the existing `DisarmPendingBe`
method (after line ~1264).

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
    // Seed last-PnL to current UnrealizedPnL to avoid instant spurious advance on arm.
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

**CYC: 4. PASS ≤ 8.**

---

### 1.3 DisarmTrailBe — CopyEngine.cs

**Location:** Insert immediately after `ArmTrailBe`.

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

**CYC: 2. PASS ≤ 8.**

---

### 1.4 OnTrailBeAccountUpdate — CopyEngine.cs

**Location:** Insert immediately after `DisarmTrailBe` as a new `private` method.

```csharp
// B14 T1 -- OnTrailBeAccountUpdate: continuous AccountItemUpdate callback for auto-trail.
// Fires on NT8 account background thread -- NO UI calls inside this method.
// CYC=5: state check(1), item filter(2), pnl improvement check(3),
//        CAS update _trailBeLastPnl(4), advance buffer + BreakEven(5).
// JS-021: no lock -- Interlocked.CompareExchange for atomic PnL high-water update.
// JS-001: BreakEven internally wraps acc.Change() in try/catch; no rethrow here.
// JS-002: all guard-path exits use bare return; -- no return null.
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

**CYC: 5. PASS ≤ 8.**

---

### 1.5 Modified OnBeConnected — TradeCopierPanel.cs

**Location:** Lines 745–752 in TradeCopierPanel.cs.

**BEFORE (exact source):**
```csharp
        // B12 T1 -- OnBeConnected: transitions ARMED -> CONNECTED. Replaces FlashBeFired from B10 T2.
        // Regular void -- invoked via Dispatcher.InvokeAsync(() => OnBeConnected(instr)) from
        // OnPendingBeFiredDispatch. Never async void. CYC=2: null guard(1) + state body(2).
        private void OnBeConnected(string instr)
        {
            if (_beBtn2 == null) return;                                              // (1)
            _beState = BeState.Connected;                                             // (2)
            UpdateBeVisuals(BeState.Connected);
            if (_instrument != null)
                _engine.BreakEven(_instrument, _beBuffer);
        }
```

**AFTER (exact replacement):**
```csharp
        // B12 T1 -- OnBeConnected: transitions ARMED -> CONNECTED. Replaces FlashBeFired from B10 T2.
        // B14 T1 -- ArmTrailBe call added after existing BreakEven call.
        // Regular void -- invoked via Dispatcher.InvokeAsync(() => OnBeConnected(instr)) from
        // OnPendingBeFiredDispatch. Never async void. CYC=3: null guard(1) + BreakEven guard(2) + trail guard(3).
        private void OnBeConnected(string instr)
        {
            if (_beBtn2 == null) return;                                              // (1)
            _beState = BeState.Connected;
            UpdateBeVisuals(BeState.Connected);
            if (_instrument != null)                                                  // (2)
                _engine.BreakEven(_instrument, _beBuffer);
            // B14 T1 -- arm continuous trail BE
            if (_instrument != null && _leaderAccount != null)                        // (3)
                _engine.ArmTrailBe(_instrument, _leaderAccount, _beBuffer);
        }
```

**CYC: 3 (was 2). PASS ≤ 8.**

**Change summary:** One new `if` block added after the existing `BreakEven` call. The null guard
on `_instrument` is already present (branch 2); the new guard also requires `_leaderAccount != null`
(one additional decision point = branch 3). No other lines change.

---

### 1.6 Modified OnBeClick Connected→Idle Case — TradeCopierPanel.cs

**Location:** Lines 704–708 in TradeCopierPanel.cs (the `case BeState.Connected:` block).

**BEFORE (exact source):**
```csharp
                case BeState.Connected:           // (5)
                    _engine.DisarmPendingBe();
                    _beState = BeState.Idle;
                    UpdateBeVisuals(BeState.Idle);
                    break;
```

**AFTER (exact replacement):**
```csharp
                case BeState.Connected:           // (5)
                    _engine.DisarmPendingBe();
                    _engine.DisarmTrailBe();          // B14 T1 -- disarm continuous trail
                    _beState = BeState.Idle;
                    UpdateBeVisuals(BeState.Idle);
                    break;
```

**CYC: 5 (unchanged). PASS ≤ 8.**

**Change summary:** One new line added (`_engine.DisarmTrailBe();`) between the existing
`DisarmPendingBe()` call and the `_beState = BeState.Idle;` assignment.

---

### 1.7 Cleanup Path Wiring — TradeCopierPanel.cs Detach()

**Location:** `public void Detach()` method, lines 286–299 in TradeCopierPanel.cs.

**BEFORE (exact source):**
```csharp
        public void Detach()
        {
            // B9 T2: unregister click trader before clearing state
            if (_currentChart != null)
                TradeCopierAddOn.UnregisterClickTrader(_currentChart);
            _engine.StatusUpdate              -= OnStatusUpdate;
            _engine.PositionStateChanged      -= OnPositionStateChanged;
            _engine.PendingBeFired            -= OnPendingBeFiredDispatch;
            foreach (var item in _followerItems)
                if (item.Account != null)
                    item.Account.AccountItemUpdate -= OnAccountItemUpdate;
            _instrument    = null;
            _leaderAccount = null;
        }
```

**AFTER (exact replacement):**
```csharp
        public void Detach()
        {
            // B9 T2: unregister click trader before clearing state
            if (_currentChart != null)
                TradeCopierAddOn.UnregisterClickTrader(_currentChart);
            _engine.StatusUpdate              -= OnStatusUpdate;
            _engine.PositionStateChanged      -= OnPositionStateChanged;
            _engine.PendingBeFired            -= OnPendingBeFiredDispatch;
            _engine.DisarmTrailBe();              // B14 T1 -- guard dangling AccountItemUpdate subscription
            foreach (var item in _followerItems)
                if (item.Account != null)
                    item.Account.AccountItemUpdate -= OnAccountItemUpdate;
            _instrument    = null;
            _leaderAccount = null;
        }
```

**Change summary:** One new line added (`_engine.DisarmTrailBe();`) after unsubscribing from
`PendingBeFired` and before the follower item loop. This prevents a dangling `AccountItemUpdate`
subscription if the panel is closed or detached while BE is in CONNECTED state. `DisarmTrailBe()`
is idempotent — safe to call even if the trail was never armed.

---

### 1.8 xUnit Tests — CopyEngineTests.cs (6 new [Fact] methods)

All 6 tests use `[Fact]` (xUnit). All are guard-path and signature/encoding tests — NT8 runtime
types (live Account, Instrument) are not available in the test runner. Add these 6 methods to the
`CopyEngineTests` class.

**Section header to insert before the first new test:**
```csharp
        // =====================================================================
        // B14 T1: ArmTrailBe / DisarmTrailBe / OnTrailBeAccountUpdate tests
        //         (T-B14-T1-A through T-B14-T1-F)
        // Spec req: DW-B12-DEFER-02 (original)
        // =====================================================================
```

#### T-B14-T1-A: ArmTrailBe_MethodExists_WithCorrectSignature

```csharp
        // T-B14-T1-A: ArmTrailBe exists as internal instance method with 3 parameters
        // (Instrument, Account, int). Guards against accidental removal or signature drift.
        // CYC=1.
        [Fact]
        public void ArmTrailBe_MethodExists_WithCorrectSignature()
        {
            var mi = typeof(CopyEngine).GetMethod(
                "ArmTrailBe",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(mi);
            Assert.Equal(3, mi.GetParameters().Length);
            // param[0] = Instrument, param[1] = Account, param[2] = int (bufferTicks)
            Assert.Equal(typeof(NinjaTrader.Cbi.Instrument), mi.GetParameters()[0].ParameterType);
            Assert.Equal(typeof(NinjaTrader.Cbi.Account),    mi.GetParameters()[1].ParameterType);
            Assert.Equal(typeof(int),                         mi.GetParameters()[2].ParameterType);
        }
```

CYC: 1. PASS.

#### T-B14-T1-B: ArmTrailBe_NullInstrument_NoException

```csharp
        // T-B14-T1-B: ArmTrailBe with null instrument hits guard (1) and returns cleanly.
        // _trailBeState (via reflection) must remain 0 -- arm write is never reached.
        // CYC=1.
        [Fact]
        public void ArmTrailBe_NullInstrument_NoException()
        {
            // Act
            var ex = Record.Exception(() => _engine.ArmTrailBe(null, null, 2));

            // Assert: no exception thrown
            Assert.Null(ex);

            // Assert: _trailBeState == 0 (guard fires before arm write)
            var fi = typeof(CopyEngine).GetField(
                "_trailBeState",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(fi);
            int state = (int)fi.GetValue(_engine);
            Assert.Equal(0, state);
        }
```

CYC: 1. PASS.

#### T-B14-T1-C: DisarmTrailBe_WhenNotArmed_NoException

```csharp
        // T-B14-T1-C: DisarmTrailBe called when never armed must not throw.
        // Verifies idempotent guard: CAS on _trailBeState=0 returns 0 (not 1) -> early return.
        // CYC=1.
        [Fact]
        public void DisarmTrailBe_WhenNotArmed_NoException()
        {
            // Ensure state is Off before calling
            var fi = typeof(CopyEngine).GetField(
                "_trailBeState",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(fi);
            fi.SetValue(_engine, 0); // force Off

            var ex = Record.Exception(() => _engine.DisarmTrailBe());
            Assert.Null(ex);
        }
```

CYC: 1. PASS.

#### T-B14-T1-D: DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall

```csharp
        // T-B14-T1-D: calling DisarmTrailBe twice must not throw on either call.
        // Verifies the CAS guard prevents double-unsubscribe (second CAS misses, returns early).
        // CYC=1.
        [Fact]
        public void DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall()
        {
            var ex = Record.Exception(() =>
            {
                _engine.DisarmTrailBe();
                _engine.DisarmTrailBe();
            });
            Assert.Null(ex);
        }
```

CYC: 1. PASS.

#### T-B14-T1-E: TrailBe_BitConverter_PnlEncoding_RoundTrip

```csharp
        // T-B14-T1-E: BitConverter round-trip for _trailBeLastPnl encoding.
        // Verifies that BitConverter.DoubleToInt64Bits(x) followed by
        // BitConverter.Int64BitsToDouble(bits) recovers the original value exactly.
        // This is the NT8-003 pattern used by ArmTrailBe and OnTrailBeAccountUpdate.
        // CYC=1.
        [Fact]
        public void TrailBe_BitConverter_PnlEncoding_RoundTrip()
        {
            double pnl      = 250.75;
            long   bits     = BitConverter.DoubleToInt64Bits(pnl);
            double recovered = BitConverter.Int64BitsToDouble(bits);
            Assert.Equal(pnl, recovered);
        }
```

CYC: 1. PASS.

#### T-B14-T1-F: TrailBe_CasLogic_NewBitsGreaterThanOld_CasSucceeds

```csharp
        // T-B14-T1-F: verifies the CAS idiom used in OnTrailBeAccountUpdate branch (4).
        // Simulates the high-water update: oldPnl=50.0, newPnl=75.0.
        // CAS from oldBits -> newBits must succeed (returns oldBits) and field must equal newBits.
        // CYC=1.
        [Fact]
        public void TrailBe_CasLogic_NewBitsGreaterThanOld_CasSucceeds()
        {
            double oldPnl  = 50.0;
            double newPnl  = 75.0;
            long   oldBits = BitConverter.DoubleToInt64Bits(oldPnl);
            long   newBits = BitConverter.DoubleToInt64Bits(newPnl);

            // Simulate: long field = oldBits; attempt CAS oldBits -> newBits
            long field   = oldBits;
            bool success = Interlocked.CompareExchange(ref field, newBits, oldBits) == oldBits;

            Assert.True(success,  "CAS must succeed when field equals expected (improvement wins)");
            Assert.Equal(newBits, field);
        }
```

CYC: 1. PASS.

---

### 1.9 SCAN CHECKLIST — Ticket 1

Engineer MUST run each of the following before marking T1 complete.

| Scan | Command | Expected Result | Rule |
|------|---------|-----------------|------|
| SCAN-01 | `grep -n "lock(" CopyEngine.cs TradeCopierPanel.cs` (new/modified code only) | 0 results in all new and modified methods | JS-021 P0 |
| SCAN-02 | `grep -n "async void " CopyEngine.cs` | 0 results — `OnTrailBeAccountUpdate` is plain `void` | JS-033 P0 |
| SCAN-03 | `grep -n "return null" CopyEngine.cs TradeCopierPanel.cs` (new/modified methods) | 0 results — all guard exits use bare `return;` | JS-002 P0 |
| SCAN-04 | Manual CYC audit of: `ArmTrailBe` (4), `DisarmTrailBe` (2), `OnTrailBeAccountUpdate` (5), `OnBeConnected` (3) | All ≤ 8 | CYC gate |
| SCAN-05 | `grep -n "volatile double\|volatile long" CopyEngine.cs` | `_trailBeLastPnl` is `volatile long` — PASS; no `volatile double` in new fields | NT8-003 |
| SCAN-06 | `grep -n "Math.Clamp" CopyEngine.cs TradeCopierPanel.cs` | 0 results in new/modified code | NT8-034 |
| SCAN-07 | `grep -n "BitConverter.Int64BitsToDouble\|BitConverter.DoubleToInt64Bits" CopyEngine.cs` | Present in both `ArmTrailBe` and `OnTrailBeAccountUpdate` | NT8-003 compliance |

---

## T2 — DW-B12-DEFER-04: Test Name Alignment

**File(s):**
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`

**Spec req:** DW-B12-DEFER-04 — Align CopyEngineTests.cs implemented test names with the
5 contract names from B12 §T1 §1.10. Closes the audit-trail gap. 4 renames + 1 new test.

---

### 2.1 Exact Mapping Table

| # | Contract Name (B12 §T1 §1.10) | Current Name in CopyEngineTests.cs | Action | Source Line (approx) |
|---|-------------------------------|-------------------------------------|--------|----------------------|
| 1 | `Trim_LimitOverload_LongPosition_EmitsSellLimitAtRefPlusTick` | `Trim_LongPosition_EmitsLimitSellAtBidPlusBuffer` | **RENAME** | ~1363 |
| 2 | `Trim_LimitOverload_ShortPosition_EmitsBuyToCoverLimitAtRefMinusTick` | *(not present — absent from file)* | **ADD NEW** | after #1 |
| 3 | `Flatten_LimitOverload_LongPosition_EmitsSellLimitFullQty` | `Flatten_LongPosition_EmitsLimitSellAtBidPlusBuffer` | **RENAME** | ~1317 |
| 4 | `Flatten_LimitOverload_ShortPosition_EmitsBuyToCoverLimitFullQty` | `Flatten_ShortPosition_EmitsLimitBuyAtAskMinusBuffer` | **RENAME** | ~1343 |
| 5 | `DispatchCopy_PttPrefixGate_SkipsOrderNamedPttTrimLimit` | `PttPrefixGate_SkipsDispatchForPttOrders` | **RENAME** | ~1389 |

**Summary:** 4 renames + 1 new test = 5 total. All 5 B12 §1.10 contract names covered.

---

### 2.2 Rename Instructions — Exact BEFORE/AFTER Declaration Lines

**Rule: Only the `public void <MethodName>()` declaration line changes. Test bodies, comments,
assertions, and spacing are PRESERVED EXACTLY.**

#### Rename #1: Trim long-position test

**BEFORE (exact declaration line ~1363):**
```csharp
        public void Trim_LongPosition_EmitsLimitSellAtBidPlusBuffer()
```

**AFTER:**
```csharp
        public void Trim_LimitOverload_LongPosition_EmitsSellLimitAtRefPlusTick()
```

#### Rename #3: Flatten long-position test

**BEFORE (exact declaration line ~1317):**
```csharp
        public void Flatten_LongPosition_EmitsLimitSellAtBidPlusBuffer()
```

**AFTER:**
```csharp
        public void Flatten_LimitOverload_LongPosition_EmitsSellLimitFullQty()
```

#### Rename #4: Flatten short-position test

**BEFORE (exact declaration line ~1343):**
```csharp
        public void Flatten_ShortPosition_EmitsLimitBuyAtAskMinusBuffer()
```

**AFTER:**
```csharp
        public void Flatten_LimitOverload_ShortPosition_EmitsBuyToCoverLimitFullQty()
```

#### Rename #5: PttPrefixGate test

**BEFORE (exact declaration line ~1389):**
```csharp
        public void PttPrefixGate_SkipsDispatchForPttOrders()
```

**AFTER:**
```csharp
        public void DispatchCopy_PttPrefixGate_SkipsOrderNamedPttTrimLimit()
```

---

### 2.3 New Test Body — Trim_LimitOverload_ShortPosition_EmitsBuyToCoverLimitAtRefMinusTick

**Location:** Insert as a new `[Fact]` method immediately after the renamed
`Trim_LimitOverload_LongPosition_EmitsSellLimitAtRefPlusTick` test (~line 1384 after rename).

```csharp
        // T1-Test-2 (B14 T2 addition): Trim(Instrument, int, double) -- short-position limit buy path.
        // Verifies the 3-arg Trim overload exists with correct signature and that the
        // short direction (BuyToCover @ refPrice - exitBuffer*tickSize) exits cleanly on null instrument.
        // CYC=1 (no branch logic in test body).
        // Spec req: DW-B12-DEFER-04 -- covers previously untested short-direction Trim path.
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

CYC: 1 (no branch logic). PASS.

---

### 2.4 SCAN CHECKLIST — Ticket 2

Engineer MUST run each of the following before marking T2 complete.

| Scan | Command / Check | Expected Result | Rule |
|------|-----------------|-----------------|------|
| SCAN-01 | Confirm no `lock(` introduced in CopyEngineTests.cs edits | 0 new `lock(` in test file | JS-021 P0 |
| SCAN-02 | Confirm no `async void ` methods added | 0 new `async void` in test file | JS-033 P0 |
| SCAN-03 | Confirm no `return null;` added | 0 in new test body | JS-002 P0 |
| SCAN-04 | CYC audit: 4 renamed tests (CYC unchanged = 1 each), 1 new test (CYC=1) | All ≤ 8 (renames carry over existing CYC) | CYC gate |
| SCAN-05 | Grep: confirm 5 contract names now exist in file | `grep -c "Trim_LimitOverload_LongPosition\|Trim_LimitOverload_ShortPosition\|Flatten_LimitOverload_LongPosition\|Flatten_LimitOverload_ShortPosition\|DispatchCopy_PttPrefixGate" CopyEngineTests.cs` returns 5 | DW-B12-DEFER-04 contract |
| SCAN-06 | Grep: confirm old names are gone | `grep "Trim_LongPosition_EmitsLimitSell\|Flatten_LongPosition_EmitsLimitSell\|Flatten_ShortPosition_EmitsLimitBuy\|PttPrefixGate_SkipsDispatchForPttOrders" CopyEngineTests.cs` returns 0 | Audit trail integrity |
| SCAN-07 | Confirm new test uses `[Fact]` attribute (xUnit) — not `[Test]` (NUnit) or `[TestMethod]` (MSTest) | `grep "\[Fact\]" CopyEngineTests.cs` includes all 7 B14 test method lines | Test framework mandate |

---

## File Change Summary

| File | Ticket | Net Change |
|------|--------|------------|
| `CopyEngine.cs` | T1 | +5 fields, +3 methods (ArmTrailBe, DisarmTrailBe, OnTrailBeAccountUpdate) |
| `TradeCopierPanel.cs` | T1 | Modify `OnBeConnected` (+3 lines); modify `OnBeClick Connected` case (+1 line); modify `Detach()` (+1 line) |
| `CopyEngineTests.cs` | T1+T2 | T1: +6 new `[Fact]` test methods + 1 section header comment. T2: 4 method declaration renames + 1 new `[Fact]` test method. Total new methods: 7. |

---

## CYC Summary — All New / Modified Methods

| Method | File | CYC | Limit | Status |
|--------|------|-----|-------|--------|
| `ArmTrailBe` | CopyEngine.cs | 4 | 8 | PASS |
| `DisarmTrailBe` | CopyEngine.cs | 2 | 8 | PASS |
| `OnTrailBeAccountUpdate` | CopyEngine.cs | 5 | 8 | PASS |
| `OnBeConnected` | TradeCopierPanel.cs | 3 (was 2) | 8 | PASS |
| `OnBeClick` | TradeCopierPanel.cs | 5 (unchanged) | 8 | PASS |
| `Detach` | TradeCopierPanel.cs | unchanged | 8 | PASS |
| 6 new xUnit tests (T1) | CopyEngineTests.cs | 1 each | 8 | PASS |
| 4 renamed tests (T2) | CopyEngineTests.cs | 1 each (unchanged) | 8 | PASS (rename only) |
| 1 new xUnit test (T2) | CopyEngineTests.cs | 1 | 8 | PASS |

---

## NT8 Constraints Reference (T1)

| Rule | Application |
|------|-------------|
| NT8-003 | `_trailBeLastPnl` is `volatile long`; encoded via `BitConverter.DoubleToInt64Bits` / `BitConverter.Int64BitsToDouble`. No `volatile double` anywhere. |
| NT8-018 / JS-021 | No `lock()` — `Interlocked.CompareExchange`, `Interlocked.Read`, `Interlocked.Increment` used throughout. |
| NT8-019 / JS-033 | `OnTrailBeAccountUpdate` is plain `void` (background thread callback). `OnBeConnected` is plain `void` invoked via `Dispatcher.InvokeAsync` — not `async void`. |
| NT8-026 | No `order.TrailPrice` — does not exist in NT8. Trail uses `BreakEven` + `acc.Change()` on the existing stop order. |
| NT8-031 | `using System.Threading;` already present in CopyEngine.cs (B10 T2). No new using needed. |
| NT8-007 | Not applicable — no new `CreateOrder` calls in trail methods. |
| NT8-013 | Not applicable — no new `CreateOrder` calls. |
| NT8-014 | Not applicable — no new order signal names. |
| NT8-020 | No new brushes in B14. `BrushConnected` already frozen via `MakeBrush()` (B12 T1). |
| NT8-034 | `Math.Clamp` not used in any new or modified method. |

---

TICKETS_COMPLETE
