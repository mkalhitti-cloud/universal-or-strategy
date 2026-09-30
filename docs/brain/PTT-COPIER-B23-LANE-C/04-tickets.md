# PTT-COPIER-B23-LANE-C — Ticket File
# Block:  PTT-COPIER-B23
# Lane:   C
# Defect: DW-B22-BE-TRIGGER-01 (P1)
# Status: TICKETS_COMPLETE
# Date:   2026-07-16

---

## Preamble

**Source plan**: `docs/brain/PTT-COPIER-B23-LANE-C/02-architecture-plan.md`
**Spec requirement**: `DW-B22-BE-TRIGGER-01` (P1) — Armed BE trigger fires on
`UnrealizedProfitLoss >= 0` (dollar PnL). PA prop accounts deduct commission at entry,
so UPnL is negative even when price is past the entry + buffer level. Trigger never fires
at the intended price level.
**xUnit baseline entering this ticket**: 122 `[Fact]` tests (or higher if Lanes A/B ran first).
**xUnit count after ticket**: baseline + 2 (net +2).
**Tickets in this lane**: 1

---

## T1 — Replace Dollar-PnL Armed Trigger With Price-Based Trigger

### Spec Requirement Satisfied
`DW-B22-BE-TRIGGER-01` — change `OnPendingBeAccountUpdate` trigger from
`e.Value >= 0` (dollar PnL) to `Last.Price >= entry + bufferTicks × tickSize` (price).

### Write-Set

| File | Absolute path |
|------|---------------|
| `CopyEngine.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` |
| `CopyEngineTests.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` |

**DO NOT TOUCH**: `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`,
`AtrSizingEngine.cs`, any `.md` files.

---

### Edit A — CopyEngine.cs: OnPendingBeAccountUpdate trigger condition

**Find this exact block** (around lines 1350–1370):

```csharp
        private void OnPendingBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)
        {
            if (_pendingBeState != 1)                                          // (1) volatile int read
                return;
            if (e.AccountItem != AccountItem.UnrealizedProfitLoss)            // (2) filter
                return;
            if (e.Value < 0)                                                   // (3) threshold
                return;
            // (4) CAS disarm: only ONE concurrent callback wins the Armed->Inactive transition
            if (Interlocked.CompareExchange(ref _pendingBeState, 0, 1) != 1)
                return;
            var acc   = _pendingBeAccount;
            var instr = _pendingBeInstrument;
            var buf   = _pendingBeBufferTicks;
            if (acc != null)
                acc.AccountItemUpdate -= OnPendingBeAccountUpdate;
            _pendingBeAccount    = null;
            _pendingBeInstrument = null;
            BreakEven(instr, buf);                                            // (5) fire BE via acc.Change()
            PendingBeFired?.Invoke(instr?.FullName ?? string.Empty);
        }
```

**Replace with**:

```csharp
        // B23 T1 (DW-B22-BE-TRIGGER-01): price-based trigger replaces dollar-PnL trigger.
        // Dollar PnL unreliable on PA accounts -- commission deducted at entry makes UPnL
        // negative even when price is past entry + buffer. Price comparison is immune to fees.
        // CYC=8: state(1), item filter(2), pos flat(3), tickSize(4), last<=0(5), triggered(6), CAS(7).
        // acc?.AccountItemUpdate null-conditional is NOT a CYC branch (same convention as ternaries).
        private void OnPendingBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)
        {
            if (_pendingBeState != 1)                                          // (1) volatile int read
                return;
            if (e.AccountItem != AccountItem.UnrealizedProfitLoss)            // (2) filter
                return;
            // (3-6) Price-based trigger: fire when Last.Price reaches entry + bufferTicks * tickSize.
            var pos = FindPosition(_pendingBeAccount, _pendingBeInstrument);
            if (IsFlat(pos))                                                   // (3)
                return;
            double tickSize = _pendingBeInstrument?.MasterInstrument?.TickSize ?? 0.0;
            if (tickSize <= 0.0)                                               // (4)
                return;
            double last = _pendingBeInstrument?.MarketData?.Last?.Price ?? 0.0;
            if (last <= 0.0)                                                   // (5)
                return;
            bool isLong  = pos.MarketPosition == MarketPosition.Long;
            double target = pos.AveragePrice
                + (isLong ? 1.0 : -1.0) * _pendingBeBufferTicks * tickSize;
            bool triggered = isLong ? (last >= target) : (last <= target);
            if (!triggered)                                                    // (6)
                return;
            // (7) CAS disarm: only ONE concurrent callback wins the Armed->Inactive transition
            if (Interlocked.CompareExchange(ref _pendingBeState, 0, 1) != 1)  // (7)
                return;
            var acc   = _pendingBeAccount;
            var instr = _pendingBeInstrument;
            var buf   = _pendingBeBufferTicks;
            acc?.AccountItemUpdate -= OnPendingBeAccountUpdate;                // null-conditional (no CYC branch)
            _pendingBeAccount    = null;
            _pendingBeInstrument = null;
            BreakEven(instr, buf);
            PendingBeFired?.Invoke(instr?.FullName ?? string.Empty);
        }
```

**Constraints**:
- `_pendingBeInstrument?.MarketData?.Last?.Price` — null-conditional chain. NT8-032 pattern.
- CYC = 7. Must not exceed 8. Count: (1) state, (2) item filter, (3) IsFlat, (4) tickSize,
  (5) last<=0, (6) triggered, (7) CAS. Ternary in `target` and `triggered` are expressions
  not branches — they do NOT add CYC.
- `IsFlat` is already a helper in `CopyEngine.cs` — do not redefine.
- `FindPosition` is already a helper in `CopyEngine.cs` — do not redefine.
- `acc?.AccountItemUpdate -= OnPendingBeAccountUpdate;` — null-conditional replaces `if (acc != null)` guard. Not a CYC branch.
- `_pendingBeInstrument` and `_pendingBeAccount` are plain refs — safe to read on callback
  thread (volatile `_pendingBeState` check at (1) acts as load barrier per B10 architecture).
- The `AccountItem.UnrealizedProfitLoss` filter at (2) stays — it ensures the callback only
  runs on PnL ticks, which is frequent enough to catch the price move promptly.

---

### New [Fact] Tests — CopyEngineTests.cs (2 tests)

**Test 1**: `PendingBe_Armed_FiresAtPriceTarget_Long`

```csharp
        [Fact]
        public void PendingBe_Armed_FiresAtPriceTarget_Long()
        {
            // Arrange: arm with bufferTicks=2, long position at avg 5000.00, tickSize=0.25.
            // Target = 5000.00 + 2 * 0.25 = 5000.50.
            var engine = new CopyEngine();
            bool fired = false;
            engine.PendingBeFired += _ => { fired = true; };

            var instr = new StubInstrument("MES", 0.25, lastPrice: 5000.50);
            var acc   = new StubAccount("Sim101");
            acc.SetPosition("MES", MarketPosition.Long, avgPrice: 5000.00, qty: 1);

            engine.ArmPendingBe(instr, acc, bufferTicks: 2);

            // Act: simulate AccountItemUpdate with any UPnL value (even negative -- must not matter).
            engine.SimulateAccountItemUpdate(acc, AccountItem.UnrealizedProfitLoss, -1.25);

            // Assert: BE fired even though UPnL was negative.
            Assert.True(fired);
        }
```

**Test 2**: `PendingBe_Armed_DoesNotFireBelowTarget_Long`

```csharp
        [Fact]
        public void PendingBe_Armed_DoesNotFireBelowTarget_Long()
        {
            // Arrange: same setup but Last.Price = 5000.25 (1 tick, below target of 5000.50).
            var engine = new CopyEngine();
            bool fired = false;
            engine.PendingBeFired += _ => { fired = true; };

            var instr = new StubInstrument("MES", 0.25, lastPrice: 5000.25);
            var acc   = new StubAccount("Sim101");
            acc.SetPosition("MES", MarketPosition.Long, avgPrice: 5000.00, qty: 1);

            engine.ArmPendingBe(instr, acc, bufferTicks: 2);

            // Act: UPnL positive -- old trigger would fire, new one must NOT.
            engine.SimulateAccountItemUpdate(acc, AccountItem.UnrealizedProfitLoss, 1.25);

            // Assert: BE must NOT have fired (price not yet at target).
            Assert.False(fired);
        }
```

**Note for engineer**: `StubInstrument` and `StubAccount` are existing test helpers.
`StubInstrument` may need a `lastPrice` parameter added — add it if not present.
`SimulateAccountItemUpdate` may need to be added as a test helper on `CopyEngine` or invoked
via reflection on `OnPendingBeAccountUpdate`. Follow the existing test pattern for invoking
private callbacks (reflection or internal test hook). The key assertion is `fired` / `!fired`.

---

### 7-Scan Checklist

**SCAN-01 — JS-021: No `lock()`**
```powershell
Select-String -Path "CopyEngine.cs","CopyEngineTests.cs" -Pattern "lock\s*\("
```
Expected: **0 new matches**.

**SCAN-02 — JS-033: No `async void`**
```powershell
Select-String -Path "CopyEngine.cs","CopyEngineTests.cs" -Pattern "async void "
```
Expected: **0 matches**.

**SCAN-03 — JS-002: No new `return null`**
```powershell
Select-String -Path "CopyEngine.cs" -Pattern "return null"
```
Expected: no new `return null` in changed method.

**SCAN-04 — NT8-003: No `volatile double`**
```powershell
Select-String -Path "CopyEngine.cs" -Pattern "volatile double"
```
Expected: **0 matches**.

**SCAN-05 — Price trigger present: old dollar-PnL trigger removed**
```powershell
Select-String -Path "CopyEngine.cs" -Pattern "e\.Value < 0"
```
Expected: **0 matches** in `OnPendingBeAccountUpdate`. Old `if (e.Value < 0) return;` must
be gone. If it still appears, the edit was not applied.

**SCAN-06 — CYC: OnPendingBeAccountUpdate ≤ 8**
Manual count: state(1), item(2), IsFlat(3), tickSize(4), last(5), triggered(6), CAS(7) = 7 if-branches + method base = CYC 8.
Expected: CYC = 8. Ternaries and null-conditionals (?.`) are NOT branches. ≤ 8 limit satisfied.

**SCAN-07 — Test framework: No NUnit / MSTest**
```powershell
Select-String -Path "CopyEngineTests.cs" -Pattern "\[Test\]|\[TestMethod\]|NUnit|MSTest"
```
Expected: **0 matches**.

---

### Success Criteria

| # | Criterion | Verification |
|---|-----------|--------------|
| 1 | Old `if (e.Value < 0) return;` removed from `OnPendingBeAccountUpdate` | SCAN-05 returns 0 matches |
| 2 | Price-based trigger present (`last >= target` for long, `last <= target` for short) | Read `CopyEngine.cs` |
| 3 | `FindPosition` + `IsFlat` + `MasterInstrument.TickSize` + `MarketData.Last.Price` all used | Read file |
| 4 | CYC = 7 on `OnPendingBeAccountUpdate` | SCAN-06 manual count |
| 5 | 2 new `[Fact]` tests added | Read `CopyEngineTests.cs` — both methods present |
| 6 | `[Fact]` count = baseline + 2 | `Select-String -Pattern "\[Fact\]" CopyEngineTests.cs \| Measure-Object` |
| 7 | All 7 scans pass (0 violations) | Run SCAN-01 through SCAN-07 |
| 8 | `dotnet build` passes 0 errors | Run in `c:\WSGTA\universal-or-strategy` |

---

## TICKETS_COMPLETE
