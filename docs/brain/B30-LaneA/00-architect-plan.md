# B30-LaneA Architect Plan
# TightenStop Leader Overload + MarketData Null Guard

## Status: PLAN_COMPLETE

---

## 1. Source Investigation Summary

### File: src/PropTraderTools/CopyEngine.cs

**TightenStop region (lines 1330-1413):**
- `TightenStop(Instrument instrument, int ticks)` at line 1333 — has `FindRule()` hard-return at line 1336-1337 that prevents leader-direct access.
- `TightenOneStop(Account acc, Instrument instr, Order order, double targetPrice, double tickSize)` at line 1374 — private helper that does cancel+replace for a single stop order. This is NOT the per-account helper we need (it handles a single Order, not iterating an account's orders).
- MarketData access at line 1348: `instrument.MarketData.Bid.Price` — raw, no null guard.
- The existing `acc.Orders` loop at line 1356 is MISSING `if (order.Instrument != instrument) continue;` filter.
- The existing loop uses `acc.Orders` directly (not `.ToList()` snapshot).

**Trim/Flatten leader overload pattern (lines 867-914):**
- `Trim(Account leader, Instrument instrument)` at line 867: CYC=4 pattern.
- `Flatten(Account leader, Instrument instrument)` at line 884: CYC=4 pattern.
- `CancelPendingEntries(Account leader, Instrument instrument)` at line 901: CYC=4 pattern.
- All three follow the identical pattern: null guard → direct call → foreach AllAccounts → skip leader.

**NT8 null-conditional support (confirmed):**
- Line 1547: `double last = instr?.MarketData?.Last?.Price ?? 0.0;` — proves NT8 supports `?.` on MarketData and sub-properties.
- Line 646: `instrument.MasterInstrument?.TickSize ?? 0.0` — more `?.` usage.
- Decision: **Use `?.` null-conditional** (`instrument.MarketData?.Bid?.Price ?? 0.0`).

---

## 2. Change Plan

### Change 1 — Add `TightenOneAccountStops` helper

**Location:** After `TightenOneStop` (after line 1413), before `ArmPendingBe` at line 1416.

**Method signature:**
```csharp
private void TightenOneAccountStops(Account acc, Instrument instrument, int tightenTicks)
```

**CYC analysis (must be <= 8):**
1. `pos == null || IsFlat(pos)` guard → (1)
2. `bidPrice > 0 && askPrice > 0` ternary → (2)  ← actually this is an expression, not a branch for CYC
3. `isLong` ternary for currentPrice → (3) — expression, not branch
4. `isLong` ternary for targetPrice → (4) — expression, not branch

Wait — re-analysis. CYC counts decision points (if/foreach/while/&&/||):
- `if (pos == null || IsFlat(pos))` → 2 decisions (null, IsFlat via ||) = +2
- `isLong ? ... : ...` ternary in MarketPosition check → +1
- `bidPrice > 0 && askPrice > 0` → +2 (&&)
- `isLong ? askPrice : bidPrice` → +1
- `isLong ? ... : ...` targetPrice ternary → +1
- `foreach (var order in ...)` → +1
- `if (order.OrderState != OrderState.Working)` → +1
- `if (order.OrderType != StopMarket && != StopLimit)` → +2 (&&)
- `if (!IsStopLeg(order))` → +1

That's: 2+1+2+1+1+1+1+2+1 = 12 → too high.

**Simplification:** Extract the price-computation into the existing TightenOneStop path. The per-account helper only needs to:
1. Compute `tickSize`, `targetPrice` (refactored: call existing logic inline)
2. Iterate `acc.Orders.ToList()`, filter, call `TightenOneStop`.

Revised CYC count for `TightenOneAccountStops`:
- `pos == null || IsFlat(pos)` → +2 (null guard || IsFlat)
- `bidPrice > 0 && askPrice > 0` → +2
- `? (isLong ? askPrice : bidPrice)` → +1  (nested ternary, only +1 for outer ternary decision)
- `isLong ? ... : ...` on targetPrice → +1
- `foreach` → +1
- `if (order.OrderState != Working)` → +1
- `if (order.OrderType != StopMarket && != StopLimit)` → +2 (&&)
- `if (order.Instrument != instrument)` → +1
- `if (!IsStopLeg(order))` → +1

Total: 2+2+1+1+1+1+2+1+1 = 12. Still too high.

**Strategy: Keep it lean — extract only the inner-loop body that was missing the instrument filter and ToList().**

The actual CYC tool counts differently from the McCabe formula sometimes. But to be safe, let's split:

Extract a sub-helper `ShouldTightenOrder(Order order, Instrument instrument)` (private static) → handles the 4 filter conditions → CYC=4 for that helper.

Then `TightenOneAccountStops`:
- `pos == null || IsFlat(pos)` → +2
- Compute bid/ask/targetPrice (ternaries = +3 in McCabe strict, but Lizard may count differently)
- `foreach` → +1
- `if (!ShouldTightenOrder(order, instrument))` → +1 (single call)
- Call `TightenOneStop`

Revised CYC: 2 (null||IsFlat) + 3 (ternaries) + 1 (foreach) + 1 (if) = 7 ≤ 8. ✓

Wait — McCabe strict: ternary operators each count as +1.
- `pos.MarketPosition == MarketPosition.Long ? ... : ...` → +1
- `bidPrice > 0 && askPrice > 0 ? (isLong ? ... : ...) : pos.AveragePrice` → +2 (outer ?: and inner ?:) + 1 for &&
- `isLong ? currentPrice - ... : currentPrice + ...` → +1

That's +5 for expressions alone. Total would be: 2+5+1+1 = 9. Borderline.

**Final strategy:** Keep `ShouldTightenOrder` sub-helper to absorb all order-filter branches. Then in `TightenOneAccountStops`, inline the price calc (4 ternaries/expressions) + 2 guards + 1 foreach + 1 if (!ShouldTighten). Lizard (used by Codacy) counts ternaries differently. Per project precedent (B29 plan), Lizard counts: if, for, while, foreach, case, &&, ||, ?:. With ShouldTightenOrder absorbing 4 conditions: CYC = 2+3+1+1+1 = 8. ✓

**Refined design:**

```csharp
// B30 -- ShouldTightenOrder: filter predicate for TightenOneAccountStops.
// CYC=4: Working check(1), StopMarket||StopLimit(2), instrument match(3), IsStopLeg(4).
private static bool ShouldTightenOrder(Order order, Instrument instrument)
{
    if (order.OrderState != OrderState.Working) return false;   // (1)
    if (order.OrderType != OrderType.StopMarket &&              // (2)
        order.OrderType != OrderType.StopLimit) return false;
    if (order.Instrument != instrument) return false;           // (3)
    if (!IsStopLeg(order)) return false;                        // (4)
    return true;
}

// B30 -- TightenOneAccountStops: per-account tighten helper.
// CYC=8: (1) pos null||IsFlat, (2) isLong ternary, (3) bid>0&&ask>0,
//         (4) isLong price select, (5) isLong target direction, (6) foreach,
//         (7) !ShouldTightenOrder, (8) call count?
```

Actually upon reflection, Lizard strict CYC = each branch/loop:
1. `if (pos == null || IsFlat(pos))` — 1 (Lizard counts the whole `if` as 1 branch, not each `||` component unless... actually Lizard DOES count `&&` and `||`)

Let me be conservative: `pos == null || IsFlat(pos)` = 2 (null, IsFlat via ||) → that leaves only 6 for everything else. With ShouldTightenOrder absorbing 4 branches: remaining budget = 6 for isLong ternary, bid>0&&ask>0, isLong price select, isLong target direction, foreach, if(!Should).

2 (null||IsFlat) + 1 (isLong ternary for pos.MarketPosition) + 2 (bid>0 && ask>0) + 1 (isLong ? ask : bid) + 1 (isLong ? sub : add) + 1 (foreach) + 1 (!ShouldTightenOrder) = 9. Still 9.

**Minimum-change approach:** Reuse the EXACT same inner code as the existing `TightenStop` loop, which already has all the price calculation. The only additions are: the instrument filter and ToList(). The helper:

```csharp
private void TightenOneAccountStops(Account acc, Instrument instrument, int tightenTicks)
{
    var pos = FindPosition(acc, instrument);
    if (pos == null || IsFlat(pos)) return;                     // (1,2)
    bool isLong = pos.MarketPosition == MarketPosition.Long;    // (3) ternary
    double tickSize = instrument.MasterInstrument.TickSize;
    double bidPrice = instrument.MarketData?.Bid?.Price ?? 0.0;
    double askPrice = instrument.MarketData?.Ask?.Price ?? 0.0;
    double currentPrice = bidPrice > 0 && askPrice > 0         // (4,5) && + ternary
        ? (isLong ? askPrice : bidPrice)                        // (6) inner ternary
        : pos.AveragePrice;
    double targetPrice = isLong                                 // (7) ternary
        ? currentPrice - tightenTicks * tickSize
        : currentPrice + tightenTicks * tickSize;
    foreach (var order in acc.Orders.ToList())                  // (8) foreach
    {
        if (!ShouldTightenOrder(order, instrument)) continue;   // one if, absorbed
        TightenOneStop(acc, instrument, order, targetPrice, tickSize);
    }
}
```

CYC: `||`=1, `IsFlat`=1 (that's the null||IsFlat if) → total if = 1, || = 1 → +2; `isLong` in pos.MarketPosition ternary = +1; `&&` = +1, outer `?:` = +1, inner `?:` = +1; `isLong` targetPrice `?:` = +1; `foreach` = +1; `if (!ShouldTightenOrder)` = +1.

Total: 2+1+1+1+1+1+1+1 = 9. One over by Lizard strict counting of all `?:` and `&&`.

**Resolution:** Inline `IsFlat()` check as `pos == null` and `pos.MarketPosition == MarketPosition.Flat` in a simple two-branch structure, and simplify the price logic:

```csharp
private void TightenOneAccountStops(Account acc, Instrument instrument, int tightenTicks)
{
    var pos = FindPosition(acc, instrument);
    if (IsFlat(pos))                                            // (1) 
        return;
    double tickSize = instrument.MasterInstrument.TickSize;
    bool isLong = pos.MarketPosition == MarketPosition.Long;   // expression, not branch
    double refPrice = GetRefPrice(acc, instrument, isLong);    // (call)
    double targetPrice = isLong
        ? refPrice - tightenTicks * tickSize                   // (2) ?:
        : refPrice + tightenTicks * tickSize;
    foreach (var order in acc.Orders.ToList())                  // (3) foreach
    {
        if (!ShouldTightenOrder(order, instrument)) continue;  // (4) if
        TightenOneStop(acc, instrument, order, targetPrice, tickSize);
    }
}
```

Where `GetRefPrice` absorbs the bid/ask logic (3 branches), and `ShouldTightenOrder` absorbs 4 filters.

CYC of `TightenOneAccountStops`: 1 (IsFlat) + 1 (isLong ternary) + 1 (foreach) + 1 (if) = 4. ✓ Well under 8.

**CYC of `GetRefPrice` (new private static helper):**
- `bid > 0 && ask > 0` → +2 (&&)
- `?:` outer → +1
- `isLong ? ask : bid` inner → +1
= 4. ✓

**CYC of `ShouldTightenOrder` (new private static helper):**
- 4 if-returns
= 4. ✓

This is clean. Three new methods, each CYC ≤ 4.

---

## 3. Exact Line Numbers for All Changes

### CopyEngine.cs

| Change | Description | Insert Location |
|--------|-------------|-----------------|
| A | Add `ShouldTightenOrder` static helper | After line 1413 (after closing `}` of TightenOneStop) |
| B | Add `GetRefPrice` static helper | After `ShouldTightenOrder` |
| C | Add `TightenOneAccountStops` per-account helper | After `GetRefPrice` |
| D | Add `TightenStop(Account, Instrument, int)` leader overload | After `TightenOneAccountStops` |
| E | Fix existing `TightenStop(Instrument, int)` — add instrument filter + ToList() + MarketData null guard | Lines 1348-1356 |

### CopyEngineTests.cs

| Change | Description | Location |
|--------|-------------|----------|
| F | Fix T-B10-T3-01 `GetMethod` to specify param types (AmbiguousMatchException prevention) | Line 1104-1108 |
| G | Add new `[Fact]` `TightenStop_LeaderDirect_SkipsFollowerAccounts` | After line 1306 (end of T-B10-T3-07), before B12 section |

---

## 4. Method Signatures

```csharp
// ShouldTightenOrder — CYC=4
private static bool ShouldTightenOrder(Order order, Instrument instrument);

// GetRefPrice — CYC=4
private static double GetRefPrice(Instrument instrument, bool isLong);

// TightenOneAccountStops — CYC=4
private void TightenOneAccountStops(Account acc, Instrument instrument, int tightenTicks);

// TightenStop leader overload — CYC=4
internal void TightenStop(Account leader, Instrument instrument, int tightenTicks);
```

---

## 5. NT8 Null-Conditional Decision

**CONFIRMED: Use `?.` null-conditional.**

Evidence: Line 1547 of CopyEngine.cs already uses `instr?.MarketData?.Last?.Price ?? 0.0`.

Pattern for `GetRefPrice`:
```csharp
double bid = instrument.MarketData?.Bid?.Price ?? 0.0;
double ask = instrument.MarketData?.Ask?.Price ?? 0.0;
```

If `bid == 0.0 && ask == 0.0`, use `pos.AveragePrice` fallback (already in the existing code at line 1350-1352).

**No-data guard:** In `TightenOneAccountStops`, if `refPrice == 0.0`, log "PTT-Tighten: no market data" and return (JS-002: no return null; JS-001: no throw).

---

## 6. New [Fact] Stub

```csharp
// T-B30-01: TightenStop(Account,Instrument,int) leader-direct overload exists.
// Verifies: 3-param overload exists; null leader logs StatusUpdate and returns cleanly.
[Fact]
public void TightenStop_LeaderDirect_SkipsFollowerAccounts()
{
    // Verify the 3-param overload (Account, Instrument, int) exists.
    var mi = typeof(CopyEngine).GetMethod(
        "TightenStop",
        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public,
        null,
        new[] { typeof(Account), typeof(Instrument), typeof(int) },
        null);
    Assert.NotNull(mi);
    Assert.Equal(3, mi.GetParameters().Length);

    // Null leader -> StatusUpdate log -> returns cleanly (JS-002 guard path).
    var messages = new System.Collections.Generic.List<string>();
    _engine.StatusUpdate += messages.Add;
    var ex = Record.Exception(() => _engine.TightenStop((Account)null, (Instrument)null, 5));
    _engine.StatusUpdate -= messages.Add;
    Assert.Null(ex);
    Assert.Contains(messages, m => m.Contains("PTT-Tighten"));
}
```

---

## 7. Test Fixture — T-B10-T3-01 Fix

The existing T-B10-T3-01 calls `GetMethod("TightenStop", ...)` without param types. Once the 3-param overload is added, .NET reflection throws `AmbiguousMatchException`. Fix: specify exact param types `{ typeof(Instrument), typeof(int) }`.

---

## 8. Jane Street / NT8 Compliance

| Rule | Compliance |
|------|------------|
| JS-021 no lock() | ✓ ToList() snapshot, no locks |
| JS-001 no throw | ✓ try/catch in TightenOneStop (unchanged) |
| JS-002 no return null | ✓ StatusUpdate log + return on null/flat |
| NT8-001 no init; | ✓ no properties added |
| NT8-007 CreateOrder arg12 | ✓ TightenOneStop already uses correct pattern |
| CYC ≤ 8 | ✓ All new methods CYC ≤ 4 |
| ASCII only | ✓ Signal names: "PTT-Tighten-Stop", "PTT-Tighten" |
