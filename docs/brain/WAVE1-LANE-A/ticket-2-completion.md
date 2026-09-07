# Ticket 2 Completion Report -- FlattenOneAccountLimit + TrimOneAccountLimit

## Scope: TICKET 2 ONLY (WAVE1-LANE-A-07 + WAVE1-LANE-A-08)
**Date**: 2026-09-07
**File modified**: `src/PropTraderTools/CopyEngine.cs`
**Test file modified**: `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`

---

## What Was Implemented

### New Helper: `SubmitLimitExitOrder` (lines 5573-5602)
- `private void SubmitLimitExitOrder(Account acc, Instrument instrument, OrderAction action, int qty, double limitPx, string orderName)`
- Absorbs the duplicated `try { acc.CreateOrder(12 args); } catch(Exception ex) { StatusUpdate error; }` block from both parent methods.
- NT8-007: arg12 = `(NinjaTrader.Cbi.CustomOrder)null` preserved exactly.
- **CRITICAL**: `acc.Submit` is NOT called -- preserves exact existing behavior (matches `MirrorCloseOneAccount` precedent; original code did not call Submit).
- CCN=3: base(1) + try(1) + catch(1).

### Refactored: `TrimOneAccountLimit` (lines 5544-5566)
- Replaced 20-line `try/catch CreateOrder` block with single call: `SubmitLimitExitOrder(acc, instrument, action, trimQty, limitPx, "PTT-TrimLimit");`
- All parent logic retained: `CancelStaleExitOrders`, pos null/qty guard, `trimQty = (int)Math.Ceiling(pos.Quantity / 2.0)`, isLong ternary, `ComputeLimitPx`, success StatusUpdate.
- CCN reduced: 8 -> 6.

### Refactored: `FlattenOneAccountLimit` (lines 5608-5629)
- Replaced 24-line `try/catch CreateOrder` block with single call: `SubmitLimitExitOrder(acc, instrument, action, pos.Quantity, limitPx, "PTT-FlattenLimit");`
- All parent logic retained: `CancelStaleExitOrders`, pos null/qty guard, isLong ternary, `ComputeLimitPx`, success StatusUpdate.
- CCN reduced: 8 -> 6.

---

## Build Result

**PASS** -- zero errors, zero warnings on modified lines.

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:02.43
```

---

## CCN Scan Results

| Method | CCN Before | CCN After | <= 8? |
|--------|-----------|-----------|-------|
| `TrimOneAccountLimit` | 8 | **6** | YES |
| `FlattenOneAccountLimit` | 8 | **6** | YES |
| `SubmitLimitExitOrder` (new) | -- | **3** | YES |

Note: CCN=6 (not 4 as estimated in ticket). Lizard counts the `||` operator in `pos == null || pos.Quantity == 0` and the ternary `?` in `isLong ? ... : ...` as branches. Both are well below the JS-080 limit of 8. Reduction achieved: -2 CCN per parent method.

---

## 7-Scan Results

| Scan | Pattern | Result | Notes |
|------|---------|--------|-------|
| SCAN-01 | `^\s*lock\s*\(` | **0 hits** | No lock() in modified/added code |
| SCAN-02 | Non-ASCII chars | **0 hits** | All strings ASCII-only |
| SCAN-03 | `FontFamily` | **0 hits** | Not in new code (only in comments) |
| SCAN-04 | `#[0-9A-Fa-f]{6}` | **0 hits** | No hex color literals |
| SCAN-05 | `SubmitLimitExitOrder` call sites | **PASS** | Both callers pass "PTT-TrimLimit" and "PTT-FlattenLimit" (PTT- prefix preserved). arg12 = `(NinjaTrader.Cbi.CustomOrder)null` confirmed at line 5595. |
| SCAN-06 | `DateTime\.Now[^U]` | **0 hits** | Only in comments |
| SCAN-07 | `public.*SubmitLimitExitOrder` | **0 hits** | Helper is `private` |

---

## Tests Added (4 [Fact] methods)

File: `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| T39 | `SubmitLimitExitOrder_UsesSellAction_WhenPositionIsLong` | isLong=true -> Sell branch selected (ternary mirror) |
| T40 | `SubmitLimitExitOrder_UsesBuyToCoverAction_WhenPositionIsShort` | isLong=false -> BuyToCover branch selected |
| T41 | `FlattenOneAccountLimit_UsesFullPositionQty_WhenCalled` | posQty=7 -> flattenQty=7 (no division) |
| T42 | `TrimOneAccountLimit_UsesHalfPositionQty_WhenCalled` | posQty=7 -> trimQty=4 (ceiling(3.5)=4) |

Note: Tests T39/T40 use local int constants (`OaActionSell=0`, `OaActionBuyToCover=1`) instead of `NinjaTrader.Cbi.OrderAction` because the test project targets net8.0 and NT8 types are not instantiable without the NT8 runtime.

---

## Test Run Results

```
Passed! - Failed: 0, Passed: 143, Skipped: 3, Total: 146
```

- **Prior passing**: 139 (before T2 tests added)
- **New tests**: 4
- **Total passing**: 143
- **Failing**: 0

---

## Git Diff Summary

**`src/PropTraderTools/CopyEngine.cs`**:
- ~20 lines added (`SubmitLimitExitOrder` helper)
- ~22 lines removed from `TrimOneAccountLimit` (CreateOrder block replaced)
- ~24 lines removed from `FlattenOneAccountLimit` (CreateOrder block replaced)
- Net: approximately +20 / -46 lines in CopyEngine.cs

**`tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`**:
- 44 lines added (4 test methods + section header + local constants)

---

## BUILD_PASS
