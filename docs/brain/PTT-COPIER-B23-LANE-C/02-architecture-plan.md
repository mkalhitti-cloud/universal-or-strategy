# PTT-COPIER-B23-LANE-C — Architecture Plan
# Block:  PTT-COPIER-B23
# Lane:   C
# Defect: DW-B22-BE-TRIGGER-01 (P1)
# Status: REVIEW_FAIL_REVISION_1
# Date:   2026-07-16

---

## §1  Defect Summary and Root Cause

### Defect ID
`DW-B22-BE-TRIGGER-01` (P1)

### Symptom
BE button Armed mode never fires the stop move automatically, even when price visibly passes
the entry + buffer level. Director observed: clicked BE (Armed state), price moved past BE
level — nothing happened.

### Root Cause (confirmed from code)
`OnPendingBeAccountUpdate` in `CopyEngine.cs` (line 1350–1370) uses `UnrealizedProfitLoss`
as the trigger. The condition at line 1356 is:

```csharp
if (e.Value < 0)   // (3) threshold
    return;
```

This fires when `UnrealizedPnL >= 0` (i.e. account is at breakeven in dollar terms).

**The problem**: PA prop firm accounts deduct commission at entry. For 1 MES contract:
- Commission ≈ $2.50/side
- 1 MES tick = $1.25
- At entry price: UPnL = -$2.50 (commission already deducted)
- At entry + 2 ticks: UPnL = -$2.50 + $2.50 = $0.00 → fires (2 ticks above entry)
- At entry + 1 tick: UPnL = -$2.50 + $1.25 = -$1.25 → never fires

The trigger fires late (2+ ticks above intended level) and unreliably depending on commission
structure. Director's bufferTicks = 2 → trigger should fire at entry+2 ticks, but commission
means it fires at entry + 4 ticks (2 ticks to cover commission + 2 buffer).

**Correct trigger**: price-based. Fire when `Last.Price >= pos.AveragePrice + bufferTicks * tickSize`
(long) or `Last.Price <= pos.AveragePrice - bufferTicks * tickSize` (short).
This is immune to commission structure and fires exactly at the intended price level.

### Note on MoveStopToBreakEven
`MoveStopToBreakEven()` itself is correct — it moves the stop to
`pos.AveragePrice ± bufferTicks × tickSize`. Only the Armed trigger condition is wrong.

---

## §2  Fix Design — Price-Based Armed Trigger

### Strategy
Replace the `UnrealizedProfitLoss` watcher in `OnPendingBeAccountUpdate` with a
`MarketData` price comparison. Subscribe to `instrument.MarketData` instead of (or
in addition to) `AccountItemUpdate`, and fire when `Last.Price` crosses the
`entry ± buffer` threshold.

**However**: NT8 AddOn cannot subscribe to `instrument.MarketData` events on arbitrary
instruments from `CopyEngine` (no chart context). The cleaner solution is to keep
the `AccountItemUpdate` subscription but change the trigger condition to use the
**position average price + buffer × tickSize** comparison instead of raw dollar PnL.

### Revised Trigger in OnPendingBeAccountUpdate

```csharp
// OLD (line 1356):
if (e.Value < 0)   return;

// NEW:
// Price-based trigger: fire when position has moved bufferTicks past entry.
// Uses pos.AveragePrice + direction * bufferTicks * tickSize.
// Immune to commission structure (fires at correct price regardless of fee deductions).
var pos   = FindPosition(_pendingBeAccount, _pendingBeInstrument);
if (IsFlat(pos)) return;
double tickSize = _pendingBeInstrument?.MasterInstrument?.TickSize ?? 0.0;
if (tickSize <= 0) return;
bool isLong     = pos.MarketPosition == MarketPosition.Long;
double target   = pos.AveragePrice + (isLong ? 1.0 : -1.0) * _pendingBeBufferTicks * tickSize;
double last     = _pendingBeInstrument?.MarketData?.Last?.Price ?? 0.0;
if (last <= 0) return;
bool triggered  = isLong ? (last >= target) : (last <= target);
if (!triggered) return;
// Unsubscribe after firing to prevent repeat triggers.
acc?.AccountItemUpdate -= OnPendingBeAccountUpdate;
```

### CYC Impact
`OnPendingBeAccountUpdate`: 5 → 8 (replaces dollar-PnL trigger with price-based trigger).

**CYC = 8.** Branches counted (7 `if`-statements + method base = CYC 8):
(1) state check (`_pendingBeState`), (2) item filter (AccountItem type), (3) IsFlat guard,
(4) tickSize <= 0 guard, (5) last <= 0 guard, (6) triggered check, (7) CAS atomic swap.
`acc?.AccountItemUpdate` uses null-conditional operator — NOT a CYC branch (same convention
as ternaries per this project). `isLong` ternary in target calc also NOT a CYC branch.
Must not exceed CYC = 8. Engineer must count carefully before submitting.

### New [Fact] Tests Required (2)

**Test 1**: `PendingBe_Armed_FiresAtPriceTarget_Long`
- Arrange: arm BE with bufferTicks=2, instrument tickSize=0.25, long position at avg 5000.0
- Simulate AccountItemUpdate with Last.Price = 5000.50 (entry + 2 ticks)
- Assert: `BreakEven` fired (verify via StatusUpdate or PendingBeFired event)

**Test 2**: `PendingBe_Armed_DoesNotFireBelowTarget_Long`
- Arrange: same setup
- Simulate AccountItemUpdate with Last.Price = 5000.25 (entry + 1 tick, below target)
- Assert: `BreakEven` NOT fired

Note: These tests require mock-friendly access to `OnPendingBeAccountUpdate`. The existing
test pattern uses reflection to read fields and direct method invocation — follow same pattern.

---

## §3  Write-Set

| File | Path |
|------|------|
| `CopyEngine.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` |
| `CopyEngineTests.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` |

**DO NOT TOUCH**: `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`,
`AtrSizingEngine.cs`, any `.md` files.
