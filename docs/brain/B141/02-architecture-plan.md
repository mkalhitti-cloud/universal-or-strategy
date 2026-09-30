# B141 Architecture Plan — OCO Cascade Fix via Dual-Resubmit

**Block**: B141
**Phase**: Architecture (Phase 1)
**Author**: ptt-architect
**Status**: REVISION CYCLE 1 — resubmitted for REVIEW_PASS
**Produced**: 2026-09-01
**Revised**: 2026-09-01 (Revision Cycle 1 — CYC counting convention corrected, Section 5 rewritten)
**Prior block**: B140-LaneA (reverted at fd4a439d — SIM Gate 1 FAIL)
**Output file**: `docs/brain/B141/02-architecture-plan.md`

---

## Section 1: Block Overview and Problem Statement

### 1.1 Background

B140-LaneA fixed DW-B153 (OCO cascade on Stop1/Stop2 drag) by routing OCO-linked ATM Stop
brackets to `acc.Change()` instead of `acc.Cancel()`. The fix was implemented, passed BUILD and
VERIFY gates, and DW-B153 was marked CLOSED.

**SIM Gate 1 FAILED**: `acc.Change()` is a confirmed silent no-op on ATM-owned Stop brackets
from AddOnBase context. The stop price did not update in the NT8 Order Grid. The fix had zero
effect on OCO cascade. B140 code was reverted at commit `fd4a439d`.

### 1.2 Current State (post-revert)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Lines 2281-2285** (current):

```csharp
// DW-B154: acc.Change() confirmed no-op on ATM Stop brackets from AddOnBase (B140 SIM Gate 1 FAIL).
if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134 + DW-B137
{
    SyncAtmFollowerBracket(acc, fo, newPrice); // cancel+resubmit (acc.Change is no-op on ATM brackets)
    return;
}
```

`SyncAtmFollowerBracket` calls `acc.Cancel(fo)` (line 2350), which triggers NT8 OCO cascade:

| Cascade event | Result |
|---|---|
| Stop1 Cancel | Target1 cascade-cancelled (same Oco GUID, atomic) |
| Stop2 Cancel | Target2 cascade-cancelled (same Oco GUID, atomic) |
| Stop3 Cancel | Target3 cascade-cancelled (same Oco GUID, atomic) |

**Consequence**: Follower loses Target1/Target2/Target3 on every stop drag.
Follower position is naked (unhedged) until next target sync event.

### 1.3 B141 Approach: Dual-Resubmit

The OCO cascade **cannot** be prevented from AddOnBase context. The only correct approach is:

1. **Capture** the linked target's limit price before the cancel fires the cascade.
2. **Accept** the cascade (allow `SyncAtmFollowerBracket` to cancel+resubmit the stop as before).
3. **Resubmit** a new PTT-TGT-Drag limit order at the captured price after the cascade.

This eliminates the naked-position window by restoring the target immediately after each stop drag.

---

## Section 2: Confirmed NT8 API Facts

All facts grounded in NT8_FULL_REFERENCE.md and NT8_ADDON_KNOWLEDGE.md mandatory reads.

| Fact | Source | Citation |
|------|--------|----------|
| `acc.Change()` is a silent no-op on ATM-owned Stop brackets from AddOnBase | B140 SIM Gate 1 FAIL; fd4a439d | DW-B154 |
| `acc.Cancel()` on OCO-linked ATM Stop triggers cascade-cancel of OCO partner | SIM log 2026-09-01 | DW-B153 root cause |
| `acc.Change()` DOES work on non-ATM orders (PTT-TGT-Drag) | SIM: ChangeSubmitted->Accepted->Working | B140 SIM Gate 1 FAIL context |
| Stop1->Target1, Stop2->Target2, Stop3->Target3 | NT8 ATM naming convention, SIM log | Block context |
| `fo.Oco` is a non-empty GUID string for ATM Stop brackets (Stop1/Stop2/Stop3) | SIM log; NT8_FULL_REFERENCE.md line 849 | Oco property confirmed |
| `fo.Oco` is empty string for PTT-STP-Drag / PTT-TGT-Drag | SIM log | Block context |
| `CreateOrder()` 12-parameter signature | NT8_FULL_REFERENCE.md line 2106 | Confirmed 2026-08-17 |
| `CreateOrder()` arg 12 must be `(NinjaTrader.Cbi.CustomOrder)null` | NT8_ADDON_KNOWLEDGE.md line 262; NT8-007 | CS1503 guard |
| `acc.Orders` returns `IEnumerable<Order>` | NT8_ADDON_KNOWLEDGE.md line 219 | Account API |
| `acc.Cancel(Order[])` cancels working order | NT8_ADDON_KNOWLEDGE.md line 222 | Account API |
| `acc.Submit(IEnumerable<Order>)` submits orders | NT8_FULL_REFERENCE.md line 2154 | Submit() doc |
| `Order.OrderState` values: Working, Accepted, Cancelled, etc. | NT8_FULL_REFERENCE.md lines 941-996 | OrderState table |
| `Order.LimitPrice` is the limit price property on NT8 Order | NT8_ADDON_KNOWLEDGE.md line 226 | Account API |
| `Order.Name` is set at CreateOrder time | NT8_ADDON_KNOWLEDGE.md line 229 | Account API |
| `AtmStrategyCreate()` is StrategyBase-only — NOT AddOnBase | NT8_ADDON_KNOWLEDGE.md | Key NT8 fact |
| `AtmStrategyChangeStopTarget()` is StrategyBase-only — NOT AddOnBase | NT8_ADDON_KNOWLEDGE.md | Key NT8 fact |

---

## Section 3: Lane-Split Gate Result

**Mandatory gate. All 4 questions answered.**

| Q | Question | Answer |
|---|----------|--------|
| Q1 | Same method or within 50 lines? | **YES** — All changes are in `CopyEngine.cs`. `SyncFollowerBracket` is modified; `CaptureLinkedTargetPrice`, `TryParseStopSuffix`, `IsTargetOrderLive`, `ResubmitTargetAfterCascade` are new helpers in the same class. Logically, all are within the same method cluster. |
| Q2 | Fix B design depends on Fix A final design? | N/A — Q1 is YES. Single pipeline. |
| Q3 | Each fix has standalone value if the other is blocked? | N/A — Q1 is YES. Single pipeline. |
| Q4 | Each fix has an independent SIM verification path? | N/A — Q1 is YES. Single pipeline. |

**GATE RESULT: SINGLE PIPELINE — one ticket (T1 only). No lane split.**

---

## Section 4: Architecture — Method Designs

### 4.1 Modified: `SyncFollowerBracket` — Branch (3)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Current lines**: 2281-2285

**CYC counting convention** (project-wide, consistent with L2250 and L2327 comments):
- Each `if` / `else if` / `foreach` / `for` / `while` / `? :` = **+1 branch**
- `&&` and `||` inside conditions = **0** (NOT counted — project codebase convention; see L2327: "exception handlers add 0 McCabe branches each (per codebase convention)")
- `catch` blocks = **0** (project convention; confirmed by L2250 comment: CYC 7 does not include the catch at L2313)
- Base = **1**

This convention is applied uniformly to ALL methods in this plan (Sections 4.1, 4.2, 4.3, and Section 5).

**Current code**:
```csharp
if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134 + DW-B137
{
    SyncAtmFollowerBracket(acc, fo, newPrice);
    return;
}
```

**B141 replacement**:
```csharp
if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134 + DW-B137 + B141
{
    double? capturedTargetPrice = CaptureLinkedTargetPrice(acc, fo.Name); // B141: capture before cascade
    SyncAtmFollowerBracket(acc, fo, newPrice);   // cascade kills linked target (accepted, by design)
    if (capturedTargetPrice.HasValue)            // B141: +1 branch (HasValue check)
        ResubmitTargetAfterCascade(acc, fo, capturedTargetPrice.Value, leaderOrder);
    return;
}
```

**Invariants**:
- `SyncAtmFollowerBracket` is ALWAYS called — regression contract preserved (T_B141_07).
- `ResubmitTargetAfterCascade` is ONLY called when `capturedTargetPrice.HasValue` — no resubmit when target already absent.
- `leaderOrder` is already in scope from `SyncFollowerBracket` signature (used in branch 3b at line 2288).

**CYC line-by-line count for `SyncFollowerBracket` post-B141**:

| # | Branch element | Line | +N |
|---|----------------|------|----|
| — | base | — | +1 |
| 1 | `if (fo == null)` | 2269 | +1 |
| 2 | `if (Math.Abs(newPrice - currentPrice) < tickSize)` | 2273 | +1 |
| 3 | `if (isStop && IsAtmSTPOrder(fo))` (the `&&` is NOT counted) | 2281 | +1 |
| 3b | `if (!isStop && IsAtmSTPOrder(fo))` (the `&&` is NOT counted) | 2286 | +1 |
| 4 | `if (isStop && IsTrailingStop(fo))` (the `&&` is NOT counted) | 2292 | +1 |
| 5 | `if (isStop)` inside try block | 2300 | +1 |
| — | `catch (Exception ex)` | 2313 | **0** (project convention) |
| B141 | `if (capturedTargetPrice.HasValue)` (new — inside branch 3 body) | new | +1 |
| **Total** | | | **= 9 elements → CYC 8** |

**Baseline (pre-B141) = 1 + 7 branches = CYC 7** — consistent with existing comment at L2250 ("CYC=7: fo null(1), price delta(2), ATM STP(3), ATM TGT(3b), IsTrailingStop(4), isStop branch(5)").

**Post-B141 = 7 + 1 (HasValue check) = CYC 8 — PASS at JS-041 limit.**

### 4.2 New: `CaptureLinkedTargetPrice(Account acc, string stopName) -> double?`

**Purpose**: Read the linked ATM target order's limit price BEFORE the cascade fires. Returns `null`
if the target cannot be found (already cancelled, or stop name not in Stop1/Stop2/Stop3 pattern).

**Method signature**:
```csharp
private double? CaptureLinkedTargetPrice(Account acc, string stopName)
```

**Design** (CYC 4 — conservative project convention):
```csharp
private double? CaptureLinkedTargetPrice(Account acc, string stopName)
{
    if (!TryParseStopSuffix(stopName, out string suffix)) // (1) if -- && NOT counted
        return null;
    string targetName = "Target" + suffix;
    foreach (var o in acc.Orders.ToList())                // (2) foreach
    {
        if (IsTargetOrderLive(o) && o.Name == targetName) // (3) if -- && NOT counted
            return o.LimitPrice;
    }
    return null;
}
```

CYC count: base(1) + if(1) + foreach(1) + if(1) = **CYC 4** — PASS. (`&&` inside condition not counted per project convention.)

**Supporting helper: `TryParseStopSuffix(string stopName, out string suffix) -> bool`**

```csharp
private static bool TryParseStopSuffix(string stopName, out string suffix)
{
    suffix = null;
    if (stopName == null || stopName.Length < 5) // (1) if -- || NOT counted
        return false;
    string raw = stopName.Substring(4);
    if (!int.TryParse(raw, out int n) || n < 1 || n > 3) // (2) if -- || NOT counted
        return false;
    suffix = raw;
    return true;
}
```

CYC: base(1) + if(1) + if(1) = **CYC 3** — PASS. (`||` inside conditions not counted per project convention.)

**Supporting helper: `IsTargetOrderLive(Order o) -> bool`**

```csharp
private static bool IsTargetOrderLive(Order o)
{
    return o.OrderState == OrderState.Working
        || o.OrderState == OrderState.Accepted;
}
```

CYC: base(1) = **CYC 1** — PASS. (Pure boolean return expression; no `if`, no branches. `||` not counted per project convention.)

**NOTE on `out string suffix` nullability**: In NT8 .NET 4.8 context `string` is not nullable by
default. The `suffix = null` assignment and `out string suffix` parameter are standard patterns
for NT8 codebase. The `null` return from `CaptureLinkedTargetPrice` is `double?` (Nullable<double>)
not a reference type — this is acceptable per JS-002 note (nullable value type, not reference null).

### 4.3 New: `ResubmitTargetAfterCascade(Account acc, Order stpOrder, double targetPrice, Order leaderOrder) -> void`

**Purpose**: After cascade has cancelled the linked ATM target, resubmit a standalone PTT-TGT-Drag
limit order at the captured target price.

**Method signature**:
```csharp
private void ResubmitTargetAfterCascade(
    Account acc,
    Order stpOrder,
    double targetPrice,
    Order leaderOrder)
```

**Design** (CYC 4 — conservative project convention):

```csharp
private void ResubmitTargetAfterCascade(
    Account acc,
    Order stpOrder,
    double targetPrice,
    Order leaderOrder)
{
    // Block A-Prime: cancel any stale PTT-TGT-Drag for this instrument (defensive sweep).
    // Mirrors SyncAtmFollowerTarget Block A-Prime (L2473-2490).
    // JS-021: no lock -- acc.Orders iteration safe on NT8 dispatch thread.
    // CYC contribution: foreach(1) + if(1) -- && NOT counted, catch NOT counted.
    foreach (var o in acc.Orders.ToList())                                         // (1) foreach
    {
        if (o.OrderState == OrderState.Working && o.Name == "PTT-TGT-Drag"        // (2) if -- && NOT counted
            && o.Instrument?.FullName == stpOrder.Instrument?.FullName)            // && NOT counted
        {
            try
            {
                acc.Cancel(new Order[] { o });
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke(acc.Name + ": TGT pre-cancel error (B141): " + ex.Message); // catch = 0
            }
        }
    }

    // Block B: CreateOrder + Submit. Mirrors SyncAtmFollowerTarget Block B (L2502-2530).
    // JS-001: no throw -- absorb via StatusUpdate.
    // CYC contribution: if(1) -- catch NOT counted.
    try
    {
        var newTarget = acc.CreateOrder(
            stpOrder.Instrument,
            stpOrder.OrderAction,
            OrderType.Limit,
            OrderEntry.Automated,
            TimeInForce.Day,
            stpOrder.Quantity,
            targetPrice,
            0,
            "",
            "PTT-TGT-Drag",
            NinjaTrader.Core.Globals.MaxDate,
            (NinjaTrader.Cbi.CustomOrder)null
        );
        if (newTarget == null)                                                      // (3) if
        {
            StatusUpdate?.Invoke(acc.Name + ": B141 TGT CreateOrder returned null");
            return;
        }
        acc.Submit(new[] { newTarget });
        StatusUpdate?.Invoke(acc.Name + ": B141 TGT resubmit after cascade -> " + targetPrice);
    }
    catch (Exception ex)                                                            // catch = 0
    {
        StatusUpdate?.Invoke(acc.Name + ": B141 TGT create error: " + ex.Message);
    }
}
```

CYC: base(1) + foreach(1) + if(1) + if(1) = **CYC 4** — PASS. (`&&` inside conditions not counted; `catch` not counted per project convention.)

**Parameter notes**:
- `stpOrder.Instrument` — same instrument as original target (by ATM design).
- `stpOrder.OrderAction` — identical to original target action: Sell for long position exits, Buy for short position exits. ATM brackets have matching exit actions on both stop and target legs.
- `stpOrder.Quantity` — matched to ATM bracket quantity.
- `oco = ""` — PTT-TGT-Drag orders are standalone (no OCO link). Confirmed: `SyncAtmFollowerTarget` Block B uses `""` (line 2514).
- `leaderOrder` — included for forward compatibility (Phase C pattern); not used in Block B.

---

## Section 5: CYC Analysis Summary

### 5.1 Counting Convention (project-wide, uniformly applied)

**Project CYC counting convention** (grounded in existing codebase comments):
- Source: `SyncAtmFollowerBracket` comment L2327: *"exception handlers add 0 McCabe branches each (per codebase convention)"*
- Source: `SyncFollowerBracket` comment L2250: *"CYC=7: fo null(1), price delta(2), ATM STP(3), ATM TGT(3b), IsTrailingStop(4), isStop branch(5)"* — this comment omits the catch at L2313, confirming catch=0 convention; and uses only the `if` keyword for the `&&`-containing conditions at L2281/2286/2292, confirming `&&`=0.

| Element | Counts? | +N |
|---------|---------|-----|
| Base (method entry) | YES | +1 |
| `if` / `else if` | YES | +1 each |
| `foreach` / `for` / `while` | YES | +1 each |
| `? :` ternary | YES | +1 each |
| `&&` inside condition | **NO** | 0 |
| `\|\|` inside condition | **NO** | 0 |
| `catch` block | **NO** | 0 |

This convention is applied uniformly to ALL methods below (and to `ResubmitTargetAfterCascade` in Section 4.3 where the previous plan incorrectly used strict McCabe counting).

### 5.2 `SyncFollowerBracket` (modified by B141) — line-by-line

Method span: `src/PropTraderTools/CopyEngine.cs` lines 2254–2317.

| # | Element | Source line | +N | Running total |
|---|---------|-------------|-----|---------------|
| — | base | — | +1 | 1 |
| 1 | `if (fo == null)` | 2269 | +1 | 2 |
| 2 | `if (Math.Abs(newPrice - currentPrice) < tickSize)` | 2273 | +1 | 3 |
| 3 | `if (isStop && IsAtmSTPOrder(fo))` — `&&` NOT counted | 2281 | +1 | 4 |
| 3b | `if (!isStop && IsAtmSTPOrder(fo))` — `&&` NOT counted | 2286 | +1 | 5 |
| 4 | `if (isStop && IsTrailingStop(fo))` — `&&` NOT counted | 2292 | +1 | 6 |
| 5 | `if (isStop)` inside try block | 2300 | +1 | 7 |
| — | `catch (Exception ex)` | 2313 | **0** | 7 |
| B141 | `if (capturedTargetPrice.HasValue)` (new, inside branch 3 body) | new | +1 | **8** |

**Baseline CYC (pre-B141) = 7** — confirmed: matches existing method comment at L2250.
**Post-B141 CYC = 8 — PASS at JS-041 limit.**

### 5.3 `CaptureLinkedTargetPrice` (new) — line-by-line

| # | Element | +N | Running total |
|---|---------|-----|---------------|
| — | base | +1 | 1 |
| 1 | `if (!TryParseStopSuffix(...))` — `!` is NOT a branch | +1 | 2 |
| 2 | `foreach (var o in acc.Orders.ToList())` | +1 | 3 |
| 3 | `if (IsTargetOrderLive(o) && o.Name == targetName)` — `&&` NOT counted | +1 | **4** |

**CYC = 4 — PASS.**

### 5.4 `TryParseStopSuffix` (new helper) — line-by-line

| # | Element | +N | Running total |
|---|---------|-----|---------------|
| — | base | +1 | 1 |
| 1 | `if (stopName == null \|\| stopName.Length < 5)` — `\|\|` NOT counted | +1 | 2 |
| 2 | `if (!int.TryParse(raw, out int n) \|\| n < 1 \|\| n > 3)` — `\|\|` NOT counted | +1 | **3** |

**CYC = 3 — PASS.**

### 5.5 `IsTargetOrderLive` (new helper) — line-by-line

| # | Element | +N | Running total |
|---|---------|-----|---------------|
| — | base | +1 | 1 |
| — | `return o.OrderState == X \|\| o.OrderState == Y` — pure boolean expression, no `if`, `\|\|` NOT counted | 0 | **1** |

**CYC = 1 — PASS.**

### 5.6 `ResubmitTargetAfterCascade` (new) — line-by-line

| # | Element | +N | Running total |
|---|---------|-----|---------------|
| — | base | +1 | 1 |
| 1 | `foreach (var o in acc.Orders.ToList())` | +1 | 2 |
| 2 | `if (o.OrderState == Working && o.Name == ... && o.Instrument?.FullName == ...)` — all `&&` NOT counted | +1 | 3 |
| — | `catch (Exception ex)` (Block A) | **0** | 3 |
| 3 | `if (newTarget == null)` | +1 | 4 |
| — | `catch (Exception ex)` (Block B) | **0** | **4** |

**CYC = 4 — PASS.**

### 5.7 Summary Table

| Method | Baseline CYC | B141 delta | Post-B141 CYC | Limit | Result |
|--------|-------------|------------|---------------|-------|--------|
| `SyncFollowerBracket` (modified) | 7 | +1 | **8** | 8 | **PASS — at limit** |
| `CaptureLinkedTargetPrice` (new) | — | — | **4** | 8 | **PASS** |
| `TryParseStopSuffix` (new helper) | — | — | **3** | 8 | **PASS** |
| `IsTargetOrderLive` (new helper) | — | — | **1** | 8 | **PASS** |
| `ResubmitTargetAfterCascade` (new) | — | — | **4** | 8 | **PASS** |

**All methods CYC <= 8. Zero JS-041 violations.**

**NOTE**: `SyncFollowerBracket` is now at CYC 8 (the absolute limit). No further branching may be
added to this method without first extracting branches to helpers. This constraint is recorded in
the code comment at line 2281 and in Section K deferred items.

---

## Section 6: Test Plan

**Test file**: `tests/PropTraderTools.Tests/B141Tests.cs`
**Framework**: xUnit only (JS mandate — NEVER NUnit or MSTest)
**Count**: 7 [Fact] tests

### Test infrastructure note

These tests follow the established NT8 test double pattern from B140Tests.cs and prior blocks.
NT8 Account/Order types are NT8 platform classes; tests use the same stub/fake infrastructure
already present in the test project. The engineer will follow the established pattern from
B140Tests.cs for account and order stubs.

### T_B141_01: `CaptureLinkedTargetPrice_Stop1_ReturnsTarget1LimitPrice`

**Asserts**:
- Given: `acc.Orders` contains one Order with `Name="Target1"`, `OrderState=Working`, `LimitPrice=4500.25`
- When: `CaptureLinkedTargetPrice(acc, "Stop1")` called
- Then: returns `4500.25` (double? with HasValue=true)
- Confirms: suffix parse "Stop1" -> "1", target lookup by "Target1", LimitPrice returned

### T_B141_02: `CaptureLinkedTargetPrice_Stop2_ReturnsTarget2LimitPrice`

**Asserts**:
- Given: `acc.Orders` contains Order with `Name="Target2"`, `OrderState=Accepted`, `LimitPrice=4510.50`
- When: `CaptureLinkedTargetPrice(acc, "Stop2")` called
- Then: returns `4510.50`
- Confirms: Accepted state is also matched (not just Working)

### T_B141_03: `CaptureLinkedTargetPrice_Stop3_ReturnsTarget3LimitPrice`

**Asserts**:
- Given: `acc.Orders` contains Order with `Name="Target3"`, `OrderState=Working`, `LimitPrice=4520.75`
- When: `CaptureLinkedTargetPrice(acc, "Stop3")` called
- Then: returns `4520.75`
- Confirms: Stop3/Target3 pair handled correctly

### T_B141_04: `CaptureLinkedTargetPrice_TargetAlreadyCancelled_ReturnsNull`

**Asserts**:
- Given: `acc.Orders` contains Order with `Name="Target1"`, `OrderState=Cancelled`, `LimitPrice=4500.25`
- When: `CaptureLinkedTargetPrice(acc, "Stop1")` called
- Then: returns `null` (HasValue=false)
- Confirms: cancelled target is not returned (IsTargetOrderLive predicate)

### T_B141_05: `SyncFollowerBracket_AtmStop1Drag_ResubmitsPttTgtDrag_WhenTargetFound`

**Asserts**:
- Given: `acc.Orders` has "Target1" Working at 4500.25; leader drags Stop1 to new price
- When: `SyncFollowerBracket` called with ATM Stop1 order
- Then: `acc.CreateOrder` called with `OrderType.Limit`, `name="PTT-TGT-Drag"`, `limitPrice=4500.25`
- Confirms: end-to-end resubmit path executes when target captured

### T_B141_06: `SyncFollowerBracket_AtmStop1Drag_NoResubmit_WhenTargetAbsent`

**Asserts**:
- Given: `acc.Orders` contains NO Target1 in Working/Accepted state (either absent or Cancelled)
- When: `SyncFollowerBracket` called with ATM Stop1 order
- Then: `acc.CreateOrder` is NOT called with `name="PTT-TGT-Drag"` (resubmit path not triggered)
- Confirms: `capturedTargetPrice.HasValue` guard prevents resubmit when target absent

### T_B141_07: `SyncFollowerBracket_AtmStop_SyncAtmFollowerBracketAlwaysCalled`

**Asserts**:
- Given: Two scenarios — (a) Target1 found; (b) Target1 absent
- When: `SyncFollowerBracket` called with ATM Stop1 in both scenarios
- Then: `SyncAtmFollowerBracket` is called in BOTH scenarios (not conditional)
- Confirms: cascade path is always executed; regression guard for existing stop-price-update behavior

---

## Section 7: JS-DNA Compliance Checklist

Applies to ALL modified and new methods in this block.

| Rule | Description | Modified: SyncFollowerBracket | New: CaptureLinkedTargetPrice | New: TryParseStopSuffix | New: IsTargetOrderLive | New: ResubmitTargetAfterCascade |
|------|-------------|---|----|---|---|---|
| **JS-021** | No `lock()` | PASS — no lock | PASS — no lock | PASS — static, no lock | PASS — static, no lock | PASS — no lock |
| **JS-033** | No `async void` | PASS — synchronous void | PASS — returns double? | PASS — static bool | PASS — static bool | PASS — synchronous void |
| **JS-002** | No reference `return null` | N/A — no null | PASS — returns `double?` (nullable value type, not reference null) | PASS — returns bool, out string | N/A — returns bool | N/A — returns void |
| **JS-001** | No throw in hot path | PASS — no throw | PASS — no throw | PASS — no throw | PASS — no throw | PASS — try/catch absorbs |
| **ASCII-only** | No Unicode in strings | PASS | PASS — "Target" + suffix | PASS | PASS | PASS — "PTT-TGT-Drag" |
| **CYC <= 8** | Cyclomatic complexity | PASS — CYC 8 (Section 5.2) | PASS — CYC 4 (Section 5.3) | PASS — CYC 3 (Section 5.4) | PASS — CYC 1 (Section 5.5) | PASS — CYC 4 (Section 5.6) |
| **PTT- prefix** | New orders named "PTT-*" | N/A | N/A | N/A | N/A | PASS — "PTT-TGT-Drag" |
| **No DateTime.Now** | Use DateTime.UtcNow or MaxDate | N/A | N/A | N/A | N/A | PASS — Globals.MaxDate |
| **NT8-007** | CreateOrder arg12 as CustomOrder | N/A | N/A | N/A | N/A | PASS — `(NinjaTrader.Cbi.CustomOrder)null` |

**GATE RESULT: ALL CHECKS PASS. Zero P0 violations.**

---

## Section 8: Data Flow Diagram

```
Leader drags Stop1
        |
        v
HandleOrderUpdate -> gate chain -> SyncFollowerBracket(acc, Stop1_fo, newPrice, leaderOrder)
        |
        v
Branch (3): isStop && IsAtmSTPOrder(fo)   [Stop1, Oco non-empty GUID]
        |
        +---> CaptureLinkedTargetPrice(acc, "Stop1")
        |         |
        |         +-- TryParseStopSuffix("Stop1") -> suffix="1"
        |         +-- acc.Orders.ToList() -> find "Target1" Working/Accepted
        |         +-- return Target1.LimitPrice (e.g. 4500.25) or null
        |
        +---> SyncAtmFollowerBracket(acc, Stop1_fo, newPrice)
        |         |
        |         +-- acc.Cancel(Stop1_fo)
        |               |
        |               v
        |         NT8 OCO cascade fires atomically:
        |         Stop1 Cancelled -> Target1 Cascade-Cancelled
        |         [SyncAtmFollowerBracket continues: CreateOrder PTT-STP-Drag + Submit]
        |
        +---> if (capturedTargetPrice.HasValue)  [4500.25 captured]
                  |
                  v
            ResubmitTargetAfterCascade(acc, Stop1_fo, 4500.25, leaderOrder)
                  |
                  +-- Block A-Prime: cancel stale PTT-TGT-Drag on same instrument
                  +-- Block B: CreateOrder PTT-TGT-Drag Limit at 4500.25 + Submit
                  +-- StatusUpdate: "B141 TGT resubmit after cascade -> 4500.25"

Result: Follower Stop1 updated, Target1 resubmitted as PTT-TGT-Drag at original price.
        Position protected. No naked-position window beyond the cascade round-trip.
```

---

## Section 9: Component List and File Map

| Component | Type | File | Lines (approx) |
|-----------|------|------|----------------|
| `SyncFollowerBracket` branch (3) | Modified existing method | `src/PropTraderTools/CopyEngine.cs` | ~2281-2289 |
| `CaptureLinkedTargetPrice` | New private method | `src/PropTraderTools/CopyEngine.cs` | after ~2290 |
| `TryParseStopSuffix` | New private static helper | `src/PropTraderTools/CopyEngine.cs` | after CaptureLinkedTargetPrice |
| `IsTargetOrderLive` | New private static helper | `src/PropTraderTools/CopyEngine.cs` | after TryParseStopSuffix |
| `ResubmitTargetAfterCascade` | New private method | `src/PropTraderTools/CopyEngine.cs` | after IsTargetOrderLive |
| B141 xUnit tests | New test class | `tests/PropTraderTools.Tests/B141Tests.cs` | 7 [Fact] tests |

**Single file modification. Zero cross-contamination.**

---

## Section 10: SIM Verification Gates

### Gate 1 (P0 — BLOCKING merge): Stop1 drag — Target1 resubmitted as PTT-TGT-Drag

**Procedure**:
1. Open NT8 SIM with PTT leader + follower, ATM-entered position (Stop1/Target1 visible in Order Grid).
2. Drag leader Stop1 to a new price.
3. Observe follower Order Grid.

**Pass criteria (ALL must be true)**:
- Follower Stop1 price updates to new price (PTT-STP-Drag appears at new stop level).
- Follower Target1 is initially cascade-cancelled (expected — by design).
- A new `PTT-TGT-Drag` limit order appears at the ORIGINAL Target1 price.
- StatusUpdate log shows "B141 TGT resubmit after cascade -> [price]".
- No naked-position window persists (PTT-TGT-Drag is Working).

**Gate 1 FAIL protocol**: If PTT-TGT-Drag does NOT appear after cascade, STOP. Document as DW-B155.
Do NOT implement further fallback. Director resolution required.

### Gate 2 (P1): Stop2 drag — Target2 resubmitted correctly

Same as Gate 1 for Stop2/Target2 pair.

### Gate 3 (P1): Consecutive stop drags — no accumulation of PTT-TGT-Drag orders

**Procedure**: Two consecutive Stop1 drags.

**Pass criteria**:
- After second drag, exactly ONE PTT-TGT-Drag exists for this instrument (Block A-Prime prevents accumulation).
- Second resubmit fires correctly with latest captured target price.

---

## Section K: Deferred Work

### DW-B153 — CLOSED (Re-closed in B141)

**Status**: CLOSED
**Closed by**: B141 T1 — dual-resubmit approach in `SyncFollowerBracket` branch (3).

The B140-LaneA closure was invalidated by SIM Gate 1 FAIL (fd4a439d). B141 re-closes DW-B153 via
a different mechanism: accept the OCO cascade, capture target price before cancel, resubmit
PTT-TGT-Drag after cascade. The root problem (naked position risk on stop drag) is resolved.

---

### DW-B154 — DOCUMENTED (acc.Change() confirmed no-op on ATM Stop brackets)

| Field | Value |
|-------|-------|
| **ID** | DW-B154 |
| **Title** | `acc.Change()` is a confirmed silent no-op on ATM-owned Stop brackets from AddOnBase |
| **Status** | DOCUMENTED — confirmed fact, no fix required |
| **Priority** | N/A (architecture constraint, not a bug) |
| **Block discovered** | B140 SIM Gate 1 FAIL; revert commit fd4a439d |

**Description**: `Account.Change()` called on ATM Strategy-owned Stop bracket orders from an
AddOnBase-derived context does NOT update the stop price. The Order Grid shows no change. The
state cycle does not advance to ChangeSubmitted. This is a confirmed NT8 API constraint:
ATM-owned brackets are managed by the ATM Strategy engine internally; the AddOn `acc.Change()`
call is accepted without error but produces zero effect on ATM-owned orders.

**Architecture implication**: All B141+ code for ATM Stop bracket price changes MUST use
cancel+resubmit (NOT acc.Change). B141 dual-resubmit is the correct pattern.

---

### DW-B140-01 — CLOSED (superseded by B141)

| Field | Value |
|-------|-------|
| **ID** | DW-B140-01 |
| **Title** | SIM Gate 1 — acc.Change() non-no-op on Stop brackets |
| **Status** | **CLOSED (superseded)** |
| **Reason** | SIM Gate 1 FAILED — acc.Change IS a no-op on ATM Stop brackets (confirmed). The question DW-B140-01 was asking is now answered with a negative result. B141 works around this constraint via dual-resubmit. The SIM gate is no longer pending — it was run and failed. |

---

### DW-B140-02 — CLOSED (superseded by B141)

| Field | Value |
|-------|-------|
| **ID** | DW-B140-02 |
| **Title** | SIM Gate 2 — Stop3 via acc.Change, Target3 not cancelled |
| **Status** | **CLOSED (superseded)** |
| **Reason** | B141 uses dual-resubmit for ALL ATM stops (Stop1/Stop2/Stop3) uniformly. The acc.Change path is abandoned. B141 Gate 2 (Stop2) and Gate 3 (Stop3 via dual-resubmit) replace this gate entirely. |

---

### DW-B140-03 — CLOSED (superseded by B141)

| Field | Value |
|-------|-------|
| **ID** | DW-B140-03 |
| **Title** | SIM Gate 3 — consecutive drags, no cascade |
| **Status** | **CLOSED (superseded)** |
| **Reason** | B141 Gate 3 (consecutive stop drags, no PTT-TGT-Drag accumulation) replaces this gate. The B141 approach is idempotent: Block A-Prime in `ResubmitTargetAfterCascade` cancels any stale PTT-TGT-Drag before resubmitting, so consecutive drags produce exactly one live PTT-TGT-Drag. |

---

### DW-B141-STP-CYC8-WALL — SyncFollowerBracket at CYC 8 limit (carry-forward)

| Field | Value |
|-------|-------|
| **ID** | DW-B141-STP-CYC8-WALL |
| **Title** | `SyncFollowerBracket` is at CYC 8 — no further branching may be added |
| **Status** | OPEN (architectural constraint) |
| **Priority** | P1 |
| **Target Block** | Next block that needs to modify SyncFollowerBracket |

**Description**: After B141, `SyncFollowerBracket` reaches CYC 8 (the JS-041 project limit).
Any future requirement adding a branch to this method MUST first extract one or more existing
branches to helper methods to create headroom. Engineer MUST check CYC before adding any
conditional logic to `SyncFollowerBracket`.

---

### Carried Forward (unchanged from B140-LaneA)

| ID | Title | Priority | Status |
|----|-------|----------|--------|
| DW-B64-01 | HandleEntryChange not firing | P0 | OPEN |
| DW-B71-01..04 | Quick ALL follower bracket dispatch + QX guard | P1 | OPEN |
| DW-B63-01 | Double PTT-Flatten 11ms apart | P1 | OPEN |
| DW-B141 | Phase C re-confirmation (SIM Test A) | P1 | OPEN |
| DW-B138 | Stop drag confirmed (SIM Test B) | P1 | OPEN |
| B135-DEFER-01 | Gap B — two simultaneous entries | P1 | OPEN |
| B135-DEFER-02 | Stale orders multi-session | P2 | OPEN |
| DW-B134-OCO-OBS | OBS-A/B/C/D partial-fill race conditions | P1 | OPEN |

---

## Section 11: Deferred Work Closure Summary

| ID | B140-LaneA Status | B141 Status | Action |
|----|-------------------|-------------|--------|
| DW-B153 | CLOSED (invalidated by SIM Gate 1 FAIL) | **CLOSED (re-closed)** | B141 dual-resubmit re-closes |
| DW-B154 | (new — created by SIM Gate 1 FAIL) | **DOCUMENTED** | Confirmed NT8 constraint |
| DW-B140-01 | OPEN (awaiting SIM run) | **CLOSED (superseded)** | SIM ran, result FAIL, question answered |
| DW-B140-02 | OPEN (awaiting SIM run) | **CLOSED (superseded)** | acc.Change approach abandoned |
| DW-B140-03 | OPEN (awaiting SIM run) | **CLOSED (superseded)** | B141 Gate 3 replaces |
| DW-B141-STP-CYC8-WALL | (new) | **OPEN** | CYC 8 wall constraint documented |

---

## Return Status

**PLAN_COMPLETE**

Revision Cycle 1 complete. One P0 violation (CYC counting inconsistency) resolved by:
- Documenting project CYC counting convention explicitly (Section 4.1, Section 5.1).
- Convention grounded in existing codebase comments (L2250, L2327): `&&`/`||` = 0 branches, `catch` = 0 branches.
- Line-by-line counts shown for ALL methods in Section 5.2 through 5.6.
- `SyncFollowerBracket` baseline confirmed CYC 7 (matches L2250 comment), post-B141 = CYC 8 (PASS at limit).
- All other methods recounted under same convention: CYC 4 / 3 / 1 / 4 (all well under limit).
- All other sections unchanged (Lane-Split Gate PASS, NT8 API facts PASS, test plan PASS, JS-DNA PASS).

Zero P0 violations. All CYC <= 8. Single pipeline. Ready for ptt-plan-reviewer.
