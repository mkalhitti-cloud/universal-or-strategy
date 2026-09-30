# B29-LaneA Architecture Plan
**Block**: PTT-COPIER-B29 Lane A
**Role**: ptt-architect (read-only source verification)
**Date**: 2026-07-16
**Status**: PLAN_COMPLETE

---

## Summary

All 7 defects (DW-B29-01 through DW-B29-07) are confirmed present in the working tree.
[Fact] count = **138** (one more than the 137 target — see note below).
Zero P0 violations in new B29 code.

---

## Edit Locations Confirmed

### DW-B29-01 — ComputeLimitPx direction fix
**File**: `src/PropTraderTools/CopyEngine.cs:1011-1013`
**Status**: CONFIRMED
```
internal static double ComputeLimitPx(bool isLong, double ask, double bid, int exitBuffer, double tickSize)
    => isLong
        ? bid - exitBuffer * tickSize
        : ask + exitBuffer * tickSize;
```
Long exit anchors to **bid** (fills at/below market). Short exit anchors to **ask** (fills at/above market).
Previous defect: passive anchor (ask+buffer for long) placed limit ABOVE market — never filled.

---

### DW-B29-02 — MoveStopToBreakEven cancel+replace with StopMarket GTC
**File**: `src/PropTraderTools/CopyEngine.cs:1277-1293`
**Status**: CONFIRMED
```
acc.Cancel(new Order[] { order });          // line 1280
acc.CreateOrder(
    instrument, action, OrderType.StopMarket, OrderEntry.Manual,
    TimeInForce.GTC, order.Quantity, 0, newStop, null, "PTT-BE-Stop",  // line 1291
    DateTime.MaxValue, null);
```
Cancel+replace pattern. Order name `"PTT-BE-Stop"` carries PTT- prefix. TIF = GTC.

---

### DW-B29-03 — TrimOneAccount TimeInForce.GTC
**File**: `src/PropTraderTools/CopyEngine.cs:969`
**Status**: CONFIRMED
```
TimeInForce.GTC, trimQty, 0, 0, null, "PTT-Trim",
```
Was `.Day`. GTC prevents overnight futures orders from expiring mid-session.

---

### DW-B29-04 — IsBracketLeg PTT- prefix check removed
**File**: `src/PropTraderTools/CopyEngine.cs:1219-1228`
**Status**: CONFIRMED
```
private bool IsBracketLeg(Order order)
{
    return order.FromEntrySignal != null
        || (
            order.Name != null
            && (
                order.Name.StartsWith("Stop")
                || order.Name.StartsWith("Target")
            )
        );
}
```
PTT- prefix guard removed. PTT- exit orders (PTT-Trim, PTT-Flatten, PTT-BE-Stop, PTT-Tighten-Stop)
are NOT bracket legs — they are cancelable via the Cancel button. Copy-cascade prevention handled
by Gate 0.5 in DispatchCopy.

---

### DW-B29-05 — TightenOneStop cancel+replace with StopMarket GTC
**File**: `src/PropTraderTools/CopyEngine.cs:1394-1406`
**Status**: CONFIRMED
```
acc.Cancel(new Order[] { order });          // line 1394
acc.CreateOrder(
    order.Instrument, tightenAction, OrderType.StopMarket, OrderEntry.Manual,
    TimeInForce.GTC, order.Quantity, 0, targetPrice, null, "PTT-Tighten-Stop",  // line 1405
    DateTime.MaxValue, null);
```
Cancel+replace pattern. Order name `"PTT-Tighten-Stop"` carries PTT- prefix. TIF = GTC.

---

### DW-B29-06 — MirrorClose TimeInForce.GTC
**File**: `src/PropTraderTools/CopyEngine.cs:488`
**Status**: CONFIRMED
```
acc.CreateOrder(instr, action, OrderType.Market,
    OrderEntry.Manual, TimeInForce.GTC,  // B29 fix: GTC matches ATM bracket TIF
```
Was `.Day`. Comment confirms fix intent.

---

### DW-B29-07 — DispatchCopy/SendCopy TimeInForce.GTC
**File**: `src/PropTraderTools/CopyEngine.cs:780`
**Status**: CONFIRMED
```
TimeInForce.GTC,  // B29 fix: Day orders expire mid-session on overnight futures
```
Was `.Day`. GTC prevents expiry on overnight futures sessions.

---

## [Fact] Count

| Expected | Actual | Delta |
|----------|--------|-------|
| 137      | **138** | +1   |

**Note**: The actual [Fact] count is **138**, one more than the 137 stated in the task brief.
The extra test is `TrimLimit_Short_PlacesAboveAsk` (line 1477) — a valid B29 regression test for
the ComputeLimitPx short-exit direction. This is additive and correct; the spec count was off by one.
All 5 new B29 [Fact] tests confirmed at lines 1468-1516:

| Test Name | Line | Covers |
|-----------|------|--------|
| `TrimLimit_Long_PlacesBelowBid` | 1469 | DW-B29-01 long direction |
| `TrimLimit_Short_PlacesAboveAsk` | 1478 | DW-B29-01 short direction |
| `FlattenLimit_Long_PlacesBelowBid` | 1487 | DW-B29-01 long + buffer=2 |
| `FlattenLimit_Short_PlacesAboveAsk` | 1496 | DW-B29-01 short + buffer=2 |
| `TrimLimit_FallsBackToMarket_WhenAskIsZero` | 1505 | guard: ask=0, bid=0, exitBuffer=0 |

---

## P0 Violation Audit

### JS-021 lock() scan
```
Select-String CopyEngine.cs -Pattern "lock\("
  Line 598: // CYC=5: fo null(1), price delta(2), TrailPrice>0(3), isStop branch(4), try block(0).
  Line 1371: // CYC=3: null guard(1), alreadyTighter(2), try block(0).
```
Both matches are inside `// CYC=...` comments — NOT actual lock statements.
**Result: PASS — zero lock() calls in executable code.**

### JS-033 async void scan
```
Select-String CopyEngine.cs -Pattern "async void "
  (no output)
```
**Result: PASS — zero async void methods.**

### JS-002 return null scan (new B29 code only)
Four `return null` instances exist in the file:
- `line 683` — `FindOrder` helper (pre-existing, returns nullable Order)
- `line 1167` — `FindRule` null guard (pre-existing, returns nullable CopyRule)
- `line 1173` — `FindRule` loop fallthrough (pre-existing)
- `line 1235` — `FindPosition` loop fallthrough (pre-existing)

None of these lines are in the B29 change set (DW-B29-01 through DW-B29-07).
All are pre-existing finder utilities with nullable return types (`Order?`, `CopyRule?`, `Position?`).
**Result: PASS — zero return null in B29 new code.**

---

## Commit Message

```
feat(B29): 7 defects -- ComputeLimitPx, BE cancel+replace, TightenStop cancel+replace, IsBracketLeg PTT prefix, GTC everywhere [138 tests]
```

> Note: commit message updated from `[137 tests]` to `[138 tests]` to match actual count.

---

## Status

**PLAN_COMPLETE**

All 7 defects (DW-B29-01 through DW-B29-07) confirmed present at the documented file:line locations.
[Fact] count = 138 (one additive test beyond the 137 target — acceptable).
Zero P0 violations in B29 new code. Working tree is ready for engineer review and commit.
