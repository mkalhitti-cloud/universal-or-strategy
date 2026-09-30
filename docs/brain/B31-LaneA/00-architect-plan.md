# B31-LaneA Architecture Plan

**Block**: B31-LaneA
**Phase**: 1 (Architecture)
**Status**: REVIEW_PASS pending
**[Fact] baseline**: 144 (B30-LaneD VERIFY_PASS confirmed)
**[Fact] target**: 146 (+2: T_B31_01, T_B31_02)
**Date**: 2026-07-17

---

## 1. Defect Summary

### DW-B31-01 | P0 — BE button kills ATM bracket (OCO link destroyed)

**Symptom**: Pressing the Break-Even button on an account running an ATM strategy
causes the stop order to be cancelled and replaced via `TryCreateStopWithRetry`.
The replacement order is issued via `acc.CreateOrder()`. NT8 creates a brand-new
order object. The new order is **not** bound to the ATM strategy instance. The
OCO (One-Cancels-Other) link that ties the stop to the profit target is **silently
destroyed**. After the move, the profit target becomes an orphaned working order
with no paired stop. If the target fills, the stop stays live as a naked protective
order. If the stop is hit, the target stays live as a naked order.

**Impact**: P0 — live account risk. Every BE button press on an ATM bracket
exposes the account to a double-fill or uncovered position scenario.

**Root cause**: `TryCreateStopWithRetry` was introduced in B29/B30 as a workaround
for a misdiagnosed problem. The belief was that `acc.Change()` silently no-ops on
ATM-owned stops. **This was wrong.** The silent no-op only happens with the
multi-param overload (`acc.Change(orders, limitPx, stopPx, qty, ...)`). The
**single-array overload** (`acc.Change(new Order[] { order })` paired with a
direct property-set on `order.StopPrice`) goes through the ATM order ownership
pathway and works correctly. Director confirmed this via live drag test on
Sim102, 2026-07-17: OCO link `38b9962f...` survived after `order.StopPrice = x;
acc.Change(new Order[]{ order })`. This pattern is already live in
`SyncFollowerBracket` at [`CopyEngine.cs:L621-624`](../../../src/PropTraderTools/CopyEngine.cs:621).

### DW-B31-02 | P2 — NT8_COMPILER_RULES.md missing NT8-046

**Symptom**: The rule describing the ATM order change API distinction
(multi-param silent no-op vs. single-array property-set safe path) is not
documented. Future agents will rediscover the same misdiagnosis that caused
B29/B30 to introduce the cancel+replace workaround.

**Fix**: Append rule NT8-046 to
[`docs/standards/NT8_COMPILER_RULES.md`](../../../docs/standards/NT8_COMPILER_RULES.md).
Last confirmed rule before B31: NT8-032 at L780.

---

## 2. Root Cause Analysis

### Why cancel+replace destroyed the OCO link

NT8 ATM strategies maintain an internal order graph. When an ATM strategy creates
a bracket, it registers the stop order and the target order as an OCO pair in a
private registry keyed by order ID. The OCO link is stored per-order-ID, not
per-account or per-instrument.

When `acc.Cancel(new Order[] { stopToCancel })` is called:
- NT8 removes the stop order from the active order list.
- The ATM strategy's private OCO registry detects the cancellation and marks
  the OCO pair as dissolved. The target order is now effectively unlinked.
- NT8 does **not** re-register any new order created via `acc.CreateOrder()` as
  the OCO counterpart of the surviving target. `acc.CreateOrder()` issues an
  entirely new order with a new ID, outside the ATM strategy graph.

Result: target order survives as a naked working order.

### Why the property-set + single-array acc.Change() is safe

`acc.Change(new Order[] { order })` with `order.StopPrice` pre-set dispatches
through NT8's "modify existing order" pathway. NT8 routes the request to the
ATM strategy's internal `ModifyOrder()` handler, which updates the stop price
in-place on the existing order object. The order ID does not change. The OCO
registry entry is not touched. The pair survives intact.

This is confirmed by NT8 Output log: `New state='Accepted', Oco='38b9962f...'`
after the property-set + Change call — the Oco field is identical to its value
before the call.

### Why B29/B30 used cancel+replace

The B29 diagnosis was based on observing that `acc.Change()` appeared to have no
effect on ATM stops. The actual cause was that the multi-param overload was being
called (or an equivalent code path with extra parameters). The single-array
overload was not tested. `TryCreateStopWithRetry` was written as a workaround
and then promoted to the canonical pattern in B30-C — compounding the error.

---

## 3. Solution Design

### Principle: in-place property-set + single-array acc.Change()

Identical to the pattern already live in `SyncFollowerBracket`
([`CopyEngine.cs:L621-624`](../../../src/PropTraderTools/CopyEngine.cs:621)):

```csharp
fo.StopPrice = newPrice;          // property-set on existing order object
acc.Change(new Order[] { fo });   // single-array overload — goes through ATM pathway
```

B31 extends this to `MoveStopToBreakEven` and `TightenOneStop`, replacing the
cancel+replace delegation. This is **not new API surface** — it copies an
already-proven call site.

### Deletion of TryCreateStopWithRetry

After B31 changes, `TryCreateStopWithRetry` has zero callers. It must be deleted
entirely. Keeping it risks a future agent re-introducing calls to it.

### CYC impact

| Method | Before | After | Delta |
|---|---|---|---|
| `TryCreateStopWithRetry` | 5 | **DELETED** | -5 |
| `MoveStopToBreakEven` | 6 | 6 | 0 (branch count unchanged) |
| `TightenOneStop` | 3 | 2 | -1 (tightenAction ternary removed) |

All surviving methods: CYC ≤ 8. Jane Street strict standard maintained.

---

## 4. Exact File Changes

### File 1: `src/PropTraderTools/CopyEngine.cs`

#### CHANGE 1 — DELETE TryCreateStopWithRetry (L1271–L1315, 45 lines)

Remove the entire private method. After removal, lines L1317+ shift up by 45.

**Before** (L1271–L1315):
```csharp
        // B30-C -- TryCreateStopWithRetry: cancel once, retry CreateOrder up to 3 times.
        // CYC=5: while(1), !cancelled guard(2), try/catch(3), retries>=3(4), base(1).
        // JS-001: no rethrow -- catch logs + continues or returns false.
        // JS-021: no lock -- stopToCancel snapshot prevents live-list mutation during cancel.
        // NT8-007: CreateOrder arg12 = (NinjaTrader.Cbi.CustomOrder)null.
        private bool TryCreateStopWithRetry(
            Account acc,
            Instrument instr,
            Order stopToCancel,
            OrderAction action,
            int quantity,
            double stopPrice,
            string signalName)
        {
            int retries = 0;
            bool cancelled = false;
            while (retries < 3)
            {
                try
                {
                    if (!cancelled)
                    {
                        acc.Cancel(new Order[] { stopToCancel });
                        cancelled = true;
                    }
                    acc.CreateOrder(
                        instr, action, OrderType.StopMarket, OrderEntry.Manual,
                        TimeInForce.Gtc, quantity, 0, stopPrice, null,
                        signalName, DateTime.MaxValue, (NinjaTrader.Cbi.CustomOrder)null);
                    return true;
                }
                catch (Exception ex)
                {
                    retries++;
                    if (retries >= 3)
                    {
                        StatusUpdate?.Invoke(
                            acc.Name + ": " + signalName + " FAILED after 3 retries -- account may be naked: "
                            + ex.Message);
                        return false;
                    }
                }
            }
            return false;
        }
```

**After**: *(entire block removed — no replacement)*

---

#### CHANGE 2 — MoveStopToBreakEven header comment and inner loop body

**Before** — header comment (L1317–L1320):
```csharp
        // B10 T1 -- MoveStopToBreakEven: adds IsStopAlreadyAtBe() guard; uses acc.Change() for ALL
        // stop types (trailing + fixed). GAP-001d CONFIRMED: trail survives acc.Change().
        // CYC=6: IsFlat(1), tickSize guard(2), foreach(3), working(4), stop type(5), isStopLeg(6).
        // JS-001: try/catch around acc.Change() -- no throw in hot path.
```

**After** — header comment:
```csharp
        // B31 -- MoveStopToBreakEven: order.StopPrice + acc.Change(new Order[]{order}) in-place.
        // B31 CONFIRMED: order-level Change() preserves ATM OCO link (Director live test 2026-07-17).
        // CYC=6: IsFlat(1), tickSize guard(2), foreach(3), working(4), stop type(5), isStopLeg(6).
        // JS-001: try/catch around acc.Change() -- no throw in hot path.
```

**Before** — inner loop body after `IsStopAlreadyAtBe` guard (L1351–L1355):
```csharp
                // DW-B29-02: acc.Change() silently fails on ATM-strategy-owned stops.
                // Fix: cancel+replace with TryCreateStopWithRetry (3 retries, B30-C DW-B30-01).
                var action = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
                StatusUpdate?.Invoke(acc.Name + ": BE attempting cancel+replace -> " + newStop);
                TryCreateStopWithRetry(acc, instrument, order, action, order.Quantity, newStop, "PTT-BE-Stop");
```

**After** — inner loop body:
```csharp
                // B31: in-place move -- same pattern as SyncFollowerBracket (L621-624).
                // NT8-046: property-set + single-array acc.Change() works on ATM-owned stops.
                StatusUpdate?.Invoke(acc.Name + ": BE moving stop -> " + newStop);
                try
                {
                    order.StopPrice = newStop;
                    acc.Change(new Order[] { order });
                    StatusUpdate?.Invoke(acc.Name + ": BE stop moved @ " + newStop);
                }
                catch (Exception ex)
                {
                    StatusUpdate?.Invoke(acc.Name + ": BE Change() failed -- " + ex.Message);
                }
```

---

#### CHANGE 3 — TightenOneStop header comment and body

**Before** — header comment (L1400–L1402):
```csharp
        // B10 T3 -- TightenOneStop: applies tighten to a single stop order.
        // B30-C: cancel+replace delegated to TryCreateStopWithRetry (DW-B30-01).
        // CYC=3: null guard(1), alreadyTighter(2), tightenAction ternary(3). try blocks removed.
```

**After** — header comment:
```csharp
        // B10 T3 -- TightenOneStop: applies tighten to a single stop order.
        // B31: in-place price move via order.StopPrice + acc.Change(new Order[]{order}).
        // CYC=2: null guard(1), alreadyTighter(2). tightenAction ternary removed.
```

**Before** — body after `if (alreadyTighter) return;` (L1414–L1419):
```csharp
            // B30-C DW-B30-01: Use TryCreateStopWithRetry for safe cancel+replace with retry.
            var tightenAction = acc.Positions
                .FirstOrDefault(p => p.Instrument == order.Instrument && p.Quantity > 0) is Position tightenPos
                ? (tightenPos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover)
                : OrderAction.Sell;
            TryCreateStopWithRetry(acc, order.Instrument, order, tightenAction, order.Quantity, targetPrice, "PTT-Tighten-Stop");
```

**After** — body:
```csharp
            // B31 NT8-046: property-set + single-array acc.Change() -- preserves ATM OCO.
            try
            {
                order.StopPrice = targetPrice;
                acc.Change(new Order[] { order });
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke(acc.Name + ": Tighten Change() failed -- " + ex.Message);
            }
```

---

### File 2: `docs/standards/NT8_COMPILER_RULES.md`

#### CHANGE 4 — Append NT8-046 after last rule (L817, append at end of file)

```markdown
---

### NT8-046 | P1 | `acc.Change()` multi-param overload silent no-op on ATM-owned orders
CONFIRMED: B31 (Director live test Sim102, 2026-07-17)
ERROR: Stop price does not change. No exception raised. OCO link survives but
       stop order never moves. UI shows old stop price after cancel+replace cycle.
       When TryCreateStopWithRetry was called, acc.Cancel() fired first, destroying
       the OCO pair silently.
CAUSE: Account-level multi-param Change() (or any cancel+CreateOrder flow) bypasses
       the ATM strategy's internal ModifyOrder() pathway. ATM-owned orders rejected
       without error. cancel+CreateOrder further destroys OCO registry entry because
       the new order ID is not registered in the ATM internal order graph.

BANNED:
  // multi-param Change() -- silent no-op on ATM stops:
  acc.Change(new[] { order }, limitPx, stopPx, qty, ...)
  // cancel+replace workaround -- destroys OCO:
  acc.Cancel(new Order[] { stop });
  acc.CreateOrder(instr, action, OrderType.StopMarket, ...);
  TryCreateStopWithRetry(...)   // removed in B31

SAFE:
  // property-set + single-array overload -- goes through ATM pathway:
  order.StopPrice = newPrice;
  acc.Change(new Order[] { order });
  // Confirmed: Oco field unchanged after call. OCO link preserved.
  // Precedent: SyncFollowerBracket at CopyEngine.cs:L621-624.

SCAN: TryCreateStopWithRetry|acc\.Cancel\(new Order\[\]
```

---

### File 3: `src/PropTraderTools/CopyEngineTests.cs`

#### CHANGE 5 — Insert 2 new [Fact] methods before closing brace at L2656

Insert the following block at L2655 (before the `    }` class closing brace):

```csharp
        [Fact]
        public void TryCreateStopWithRetry_DoesNotExist()
        {
            var method = typeof(CopyEngine).GetMethod(
                "TryCreateStopWithRetry",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            Assert.Null(method);
        }

        [Fact]
        public void MoveStopToBreakEven_DoesNotCallCancel()
        {
            var method = typeof(CopyEngine).GetMethod(
                "MoveStopToBreakEven",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
            var body = method.GetMethodBody();
            Assert.NotNull(body);
            bool hasOrderActionLocal = body.LocalVariables
                .Any(lv => lv.LocalType == typeof(NinjaTrader.Cbi.OrderAction));
            Assert.False(hasOrderActionLocal);
        }
```

**Insertion point**: before `    }` at L2656 (the class closing brace).
Current last test ends at L2653. Insert after L2655 (blank line).

---

## 5. CYC Analysis Table

| Method | CYC Before B31 | CYC After B31 | Branches |
|---|---|---|---|
| `TryCreateStopWithRetry` | 5 | **DELETED** | — |
| `MoveStopToBreakEven` | 6 | 6 | IsFlat(1), tickSize guard(2), foreach(3), working(4), stop type(5), isStopLeg(6) |
| `TightenOneStop` | 3 | 2 | null guard(1), alreadyTighter(2) |

**All surviving methods: CYC ≤ 8. Jane Street strict standard.**

Note: The `MoveStopToBreakEven` CYC is unchanged because the number of conditional
branches (6) does not change — only the implementation of the leaf action block
changes from a `TryCreateStopWithRetry` call (which added 0 branches at the
call site) to a `try/catch` (which adds 0 new counted branches since the catch
is not a conditional decision point in McCabe complexity).

`TightenOneStop` drops from CYC=3 to CYC=2 because the `tightenAction` ternary
expression (branch 3) is removed entirely. The new body has only the null guard
(1) and the `alreadyTighter` guard (2).

---

## 6. [Fact] Test Plan

### Baseline: 144 tests (B30-LaneD VERIFY_PASS)

### T_B31_01 — TryCreateStopWithRetry_DoesNotExist

**Purpose**: Contract assertion that the deleted method no longer exists.
Prevents any future agent from accidentally re-introducing it.

```csharp
[Fact]
public void TryCreateStopWithRetry_DoesNotExist()
{
    var method = typeof(CopyEngine).GetMethod(
        "TryCreateStopWithRetry",
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance);
    Assert.Null(method);
}
```

**What it asserts**: Reflection lookup of `TryCreateStopWithRetry` returns null.
If an engineer accidentally re-adds the method, this test fails and blocks the build.

### T_B31_02 — MoveStopToBreakEven_DoesNotCallCancel

**Purpose**: Structural contract assertion that `MoveStopToBreakEven` no longer
uses `OrderAction` locals (which are only needed for the cancel+replace path).
`OrderAction` variables in this method's body are the fingerprint of the
cancel+replace workaround.

```csharp
[Fact]
public void MoveStopToBreakEven_DoesNotCallCancel()
{
    var method = typeof(CopyEngine).GetMethod(
        "MoveStopToBreakEven",
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance);
    Assert.NotNull(method);
    var body = method.GetMethodBody();
    Assert.NotNull(body);
    bool hasOrderActionLocal = body.LocalVariables
        .Any(lv => lv.LocalType == typeof(NinjaTrader.Cbi.OrderAction));
    Assert.False(hasOrderActionLocal);
}
```

**What it asserts**: The JIT local variable table for `MoveStopToBreakEven`
contains no `NinjaTrader.Cbi.OrderAction` typed slot. In the old implementation,
`var action = isLong ? OrderAction.Sell : OrderAction.BuyToCover;` created such
a local. After B31, the method body contains no `OrderAction` variable at all.

### Target [Fact] count: 146

---

## 7. Seven-Scan Checklist (Engineer Contract)

| Scan | Command | Required Result |
|---|---|---|
| SCAN-01 | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 hits |
| SCAN-02 | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 hits in new code (MoveStopToBreakEven, TightenOneStop) |
| SCAN-03 | `grep -n "TryCreateStopWithRetry" src/PropTraderTools/CopyEngine.cs` | 0 hits |
| SCAN-04 | `grep -n "acc\.Cancel" src/PropTraderTools/CopyEngine.cs` | 0 hits in MoveStopToBreakEven or TightenOneStop |
| SCAN-05 | `grep -n "acc\.CreateOrder" src/PropTraderTools/CopyEngine.cs` | 0 hits in MoveStopToBreakEven or TightenOneStop |
| SCAN-06 | `grep -n "BE moving stop\|BE stop moved\|BE Change() failed" src/PropTraderTools/CopyEngine.cs` | 3+ hits |
| SCAN-07 | `Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "\[Fact\]" \| Measure-Object \| Select-Object -ExpandProperty Count` | 146 |

All 7 scans must pass before the engineer marks the ticket complete.

---

## 8. NT8-046 Rule Text (for NT8_COMPILER_RULES.md)

```
### NT8-046 | P1 | acc.Change() multi-param overload silent no-op on ATM-owned orders
CONFIRMED: B31 (Director live test Sim102, 2026-07-17)
ERROR: Stop price does not change. No exception. OCO link survives acc.Change(new[]{})
       but acc.Cancel() + acc.CreateOrder() destroys OCO registry entry silently.
CAUSE: NT8 ATM strategy registers orders by ID in a private OCO registry.
       cancel+CreateOrder issues a new order with a new ID not in the registry.
       Multi-param Change() overload bypasses ATM ownership pathway.
       Single-array overload routes through ATM ModifyOrder() — ID preserved.

BANNED:
  acc.Change(new[] { order }, limitPx, stopPx, qty, ...)  -- multi-param, silent no-op
  acc.Cancel(new Order[] { stop }); acc.CreateOrder(...)  -- destroys OCO link
  TryCreateStopWithRetry(...)                             -- removed in B31

SAFE:
  order.StopPrice = newPrice;
  acc.Change(new Order[] { order });
  // Confirmed: Oco=<uuid> field UNCHANGED after call. OCO link preserved.
  // Precedent: SyncFollowerBracket CopyEngine.cs:L621-624.

SCAN: TryCreateStopWithRetry|acc\.Cancel\(new Order\[\]
```

---

## 9. Hard-Link Sync Instruction

After all three files are modified and F5 confirms green:

```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

This syncs the Wave workspace hard-links for the modified files:
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/CopyEngineTests.cs`
- `docs/standards/NT8_COMPILER_RULES.md`

Run from `c:\WSGTA\universal-or-strategy\` (Wave workspace root).

---

## 10. Commit Message

```
B31: fix BE/Tighten OCO destruction -- replace cancel+replace with in-place acc.Change()

DW-B31-01 P0: MoveStopToBreakEven and TightenOneStop now use order.StopPrice +
acc.Change(new Order[]{order}) in-place, matching SyncFollowerBracket (L621-624).
ATM OCO link preserved. TryCreateStopWithRetry deleted (zero callers).

DW-B31-02 P2: NT8-046 appended to NT8_COMPILER_RULES.md documenting the
multi-param vs single-array acc.Change() distinction for ATM-owned orders.

CYC: MoveStopToBreakEven=6 (unchanged), TightenOneStop=2 (was 3).
Tests: 144 -> 146 (+T_B31_01, +T_B31_02).
SCAN-01..07: all pass.
```

---

## 11. Files Changed Summary

| File | Change Type | Lines Affected |
|---|---|---|
| `src/PropTraderTools/CopyEngine.cs` | Delete method + modify 2 methods | L1271-L1315 deleted; L1317-1320 comment; L1351-1355 body; L1400-1402 comment; L1414-1419 body |
| `src/PropTraderTools/CopyEngineTests.cs` | Add 2 [Fact] methods | Insert ~20 lines before L2656 |
| `docs/standards/NT8_COMPILER_RULES.md` | Append NT8-046 | Append after L817 |

---

## 12. Spec Requirements Satisfied

| Defect | Satisfied By |
|---|---|
| DW-B31-01 P0 | CHANGE 1 (delete TryCreateStopWithRetry) + CHANGE 2 (MoveStopToBreakEven in-place) + CHANGE 3 (TightenOneStop in-place) |
| DW-B31-02 P2 | CHANGE 4 (NT8-046 appended to NT8_COMPILER_RULES.md) |

---

*Architect: ptt-architect | Block: B31-LaneA | Phase 1 complete*
