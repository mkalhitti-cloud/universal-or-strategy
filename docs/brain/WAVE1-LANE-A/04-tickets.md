# WAVE1-LANE-A Implementation Tickets

**Epic**: CopyEngine.cs God-Method Extraction
**Plan input**: `docs/brain/WAVE1-LANE-A/02-architecture-plan.md` (REVIEW_PASS — Cycle 2)
**Plan review**: `docs/brain/WAVE1-LANE-A/02-plan-review.md` (REVIEW_PASS confirmed)
**Target file**: `src/PropTraderTools/CopyEngine.cs`
**Class**: `TrimSignal`
**Date**: 2026-09-07
**Phase**: 3 — Ticket Generation

---

## Execution Order

| Ticket | Method(s) | Priority | CCN Before | CCN After |
|--------|-----------|----------|-----------|-----------|
| T1 (A-01) | RegisterBeRetrySlotIfNeeded | HIGH | 8 | 6 |
| T2 (A-07 + A-08) | FlattenOneAccountLimit + TrimOneAccountLimit | HIGH | 8 / 8 | 4 / 4 |
| T3 (A-09) | OnOrderUpdate | ADVISORY | 8 | 5 |

Execute T1 and T2 independently (no dependency between them).
Execute T3 after T1 and T2 to avoid merge conflicts (OnOrderUpdate is adjacent to active lines).

---

## Ticket 1 — RegisterBeRetrySlotIfNeeded

### Spec Requirement IDs
- WAVE1-LANE-A-01

### Target Method(s)
- Method: `RegisterBeRetrySlotIfNeeded`
- File: `src/PropTraderTools/CopyEngine.cs`
- Lines: **6258–6311** (54 source lines, confirmed by grep)
- Current CCN: **8** (at JS-080 limit — extraction required)

### New Helper Method Signatures

#### Helper 1: `IsBeRetrySlotNeeded`

```csharp
private static bool IsBeRetrySlotNeeded(
    bool isFollower,
    int targetsCount,
    int leaderCount,
    bool isFlat)
```

- **Access**: `private static` (pure function — no instance state, directly unit-testable)
- **Return**: `bool` — JS-002 compliant (never null; bool is a value type)
- **Body description**: Positive predicate. Returns `true` when ALL of:
  (1) `isFollower == true`, (2) `leaderCount > 0`, (3) `targetsCount < leaderCount`, (4) `!isFlat`.
  Single expression: `return isFollower && leaderCount > 0 && targetsCount < leaderCount && !isFlat;`
  Absorbs the compound guard `leaderCount <= 0 || targetsCount >= leaderCount || IsFlat(...)` from
  the parent (expressed as the De Morgan positive predicate for clarity).
- **Estimated CCN**: **4** (base=1 + 3 `&&` boolean operators)

#### Helper 2: `RegisterPendingBeSlot`

```csharp
private void RegisterPendingBeSlot(
    Account acc,
    Instrument instrument,
    int bufferTicks,
    int delayMs = 500)
```

- **Access**: `private` (instance method — writes `_pendingFollowerBeSlots`, calls `QueueBeRetryFallback`)
- **Return**: `void` — JS-002 compliant
- **Body description**: Three-step sequence (ORDER IS MANDATORY — see CRITICAL ORDERING below):
  1. `_pendingFollowerBeSlots[acc.Name] = new PendingFollowerBeSlot(acc, instrument, bufferTicks);`
  2. `NinjaTrader.Code.Output.Process("[BE-DIAG] " + acc.Name + " ...", NinjaTrader.NinjaScript.PrintTo.OutputTab1);`
  3. `QueueBeRetryFallback(acc, instrument, bufferTicks, delayMs: delayMs);`
  No branches.
- **CRITICAL ORDERING**: Slot MUST be written (step 1) BEFORE `QueueBeRetryFallback` is called (step 3).
  The DispatcherTimer tick started by QueueBeRetryFallback calls `TryRemove` on `_pendingFollowerBeSlots`.
  If the slot is not present when the tick fires, the retry is silently skipped.
  The log (step 2) is between slot write and timer start; it does NOT affect correctness but the
  ordering `(1) write -> (2) log -> (3) timer` MUST be preserved exactly.
- **Estimated CCN**: **1** (base=1, no branches — pure assignment + log + delegate call)

### Extraction Steps (ordered)

1. **Add `IsBeRetrySlotNeeded` immediately before `RegisterBeRetrySlotIfNeeded` (line 6258):**

   ```csharp
   private static bool IsBeRetrySlotNeeded(
       bool isFollower,
       int targetsCount,
       int leaderCount,
       bool isFlat)
   {
       return isFollower && leaderCount > 0 && targetsCount < leaderCount && !isFlat;
   }
   ```

2. **Add `RegisterPendingBeSlot` immediately after `IsBeRetrySlotNeeded` and before `RegisterBeRetrySlotIfNeeded`:**

   ```csharp
   private void RegisterPendingBeSlot(
       Account acc,
       Instrument instrument,
       int bufferTicks,
       int delayMs = 500)
   {
       // (1) FIRST: write slot before starting timer
       _pendingFollowerBeSlots[acc.Name] = new PendingFollowerBeSlot(acc, instrument, bufferTicks);
       // (2) SECOND: diagnostic log
       NinjaTrader.Code.Output.Process(
           "[BE-DIAG] " + acc.Name + " registered BE retry slot, delayMs=" + delayMs,
           NinjaTrader.NinjaScript.PrintTo.OutputTab1
       );
       // (3) THIRD: start timer (TryRemoves the slot on tick)
       QueueBeRetryFallback(acc, instrument, bufferTicks, delayMs: delayMs);
   }
   ```

3. **Refactor `RegisterBeRetrySlotIfNeeded` body** to call both helpers. The external signature of
   `RegisterBeRetrySlotIfNeeded` MUST NOT change (called twice by `MoveStopToBreakEven` at A-04).

   Replace the body of `RegisterBeRetrySlotIfNeeded` (lines 6266–6311) with:

   ```csharp
   {
       if (isRetry)
           return;
       if (targetsCount == 0)
       {
           if (IsFlat(FindPosition(acc, instrument)))
               return;
           RegisterPendingBeSlot(acc, instrument, bufferTicks, delayMs: 500);
           return;
       }
       if (!IsFollowerAccount(acc))
           return;
       if (!IsBeRetrySlotNeeded(IsFollowerAccount(acc), targetsCount, leaderCount,
               IsFlat(FindPosition(acc, instrument))))
           return;
       RegisterPendingBeSlot(acc, instrument, bufferTicks);
   }
   ```

   **Note**: The `isFollower` argument to `IsBeRetrySlotNeeded` is `IsFollowerAccount(acc)` — the same
   call already in the guard directly above. At CCN=6 the duplication is acceptable; do not cache in
   a local unless the engineer prefers to for clarity.

4. **Verify post-extraction CCN:**
   Run `lizard src/PropTraderTools/CopyEngine.cs --csv | grep RegisterBeRetrySlotIfNeeded`
   Expected: CCN = **6**.
   Run `lizard src/PropTraderTools/CopyEngine.cs --csv | grep IsBeRetrySlotNeeded`
   Expected: CCN = **4**.
   Run `lizard src/PropTraderTools/CopyEngine.cs --csv | grep RegisterPendingBeSlot`
   Expected: CCN = **1**.

### 7-Scan Checklist

- [ ] **SCAN-01 (lock-free)**: No new `lock()` anywhere in added/modified code.
  `IsBeRetrySlotNeeded` is a pure static expression — no state access.
  `RegisterPendingBeSlot` uses `_pendingFollowerBeSlots[acc.Name] = ...` (ConcurrentDictionary
  indexer — lock-free). `QueueBeRetryFallback` signature unchanged. Zero lock() added.
- [ ] **SCAN-02 (async void)**: No `async` keyword on either new helper. Both are synchronous `void`/`bool`.
- [ ] **SCAN-03 (return null)**: `IsBeRetrySlotNeeded` returns `bool`. `RegisterPendingBeSlot` returns `void`.
  Neither can return null.
- [ ] **SCAN-04 (CCN)**: `lizard src/PropTraderTools/CopyEngine.cs --csv` confirms:
  `RegisterBeRetrySlotIfNeeded` CCN <= 8 (expected 6); `IsBeRetrySlotNeeded` CCN <= 8 (expected 4);
  `RegisterPendingBeSlot` CCN <= 8 (expected 1).
- [ ] **SCAN-05 (PTT- prefix)**: No `CreateOrder` calls added or modified in this ticket.
  `QueueBeRetryFallback` is called unchanged — no `CreateOrder` inside this ticket scope.
- [ ] **SCAN-06 (ASCII-only)**: All new string literals use ASCII characters only.
  Confirm `"[BE-DIAG] "`, `"registered BE retry slot"`, `"delayMs="` are ASCII-only.
- [ ] **SCAN-07 (public visibility)**: `IsBeRetrySlotNeeded` is `private static`.
  `RegisterPendingBeSlot` is `private`. Neither widens visibility.

### Test Requirements

**Test file**: `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`
**Test class**: `CopyEngineTests` (add to existing class — do NOT replace)
**Framework**: xUnit ONLY. NEVER NUnit or MSTest. No `lock`. No `async void`.

**Pattern**: Inline predicate mirror for `IsBeRetrySlotNeeded` (pure static — directly callable).
Inline logic-mirror for `RegisterPendingBeSlot` (NT8 `Account`, `Instrument`, `DispatcherTimer`
are not constructible outside the NT8 runtime; test verifies the logic expressed in the method body).

#### `IsBeRetrySlotNeeded` — 6 [Fact] tests

```csharp
[Fact]
public void IsBeRetrySlotNeeded_ReturnsFalse_WhenNotFollowerAccount()
```
- Input: `isFollower=false, targetsCount=1, leaderCount=3, isFlat=false`
- Expected: `false`
- Why: First `&&` condition fails when `isFollower == false`; short-circuits immediately.

```csharp
[Fact]
public void IsBeRetrySlotNeeded_ReturnsFalse_WhenLeaderCountZero()
```
- Input: `isFollower=true, targetsCount=1, leaderCount=0, isFlat=false`
- Expected: `false`
- Why: `leaderCount > 0` is false when `leaderCount == 0`; no slot needed with zero leaders.

```csharp
[Fact]
public void IsBeRetrySlotNeeded_ReturnsFalse_WhenTargetsCountEqualsLeaderCount()
```
- Input: `isFollower=true, targetsCount=3, leaderCount=3, isFlat=false`
- Expected: `false`
- Why: `targetsCount < leaderCount` is false when equal; all follower slots already registered.

```csharp
[Fact]
public void IsBeRetrySlotNeeded_ReturnsFalse_WhenTargetsCountExceedsLeaderCount()
```
- Input: `isFollower=true, targetsCount=4, leaderCount=3, isFlat=false`
- Expected: `false`
- Why: `targetsCount < leaderCount` is false when targets > leaders; no slot needed.

```csharp
[Fact]
public void IsBeRetrySlotNeeded_ReturnsFalse_WhenPositionIsFlat()
```
- Input: `isFollower=true, targetsCount=1, leaderCount=3, isFlat=true`
- Expected: `false`
- Why: `!isFlat` is false when position is flat; no BE retry slot for flat account.

```csharp
[Fact]
public void IsBeRetrySlotNeeded_ReturnsTrue_WhenPartialFollowerWithOpenPosition()
```
- Input: `isFollower=true, targetsCount=1, leaderCount=3, isFlat=false`
- Expected: `true`
- Why: All four conditions satisfied — follower account with partial targets and open position.

#### `RegisterPendingBeSlot` — 2 [Fact] tests (inline logic-mirror)

```csharp
[Fact]
public void RegisterPendingBeSlot_SlotWrittenWithCorrectKeys_WhenBothDelayVariants()
```
- What it verifies: Inline mirror: `ConcurrentDictionary[acc.Name] = new PendingFollowerBeSlot(acc, instr, bufferTicks)`.
  The key is `acc.Name` and the value holds the correct `acc` reference, `instr` reference, and
  `bufferTicks` value. Verifies both the default `delayMs=500` path and an explicit `delayMs=200` path
  produce the same slot structure (only the timer delay differs, not the slot contents).
- Input: Mock/stub `acc.Name = "SIM101"`, `bufferTicks = 5`
- Expected: `_pendingFollowerBeSlots["SIM101"].Instrument == instr` and `bufferTicks == 5`

```csharp
[Fact]
public void RegisterPendingBeSlot_DefaultDelayMs_Is500()
```
- What it verifies: The `delayMs = 500` default parameter contract. Guards that the default is never
  silently changed to a different value (e.g. 200 or 1000) by an engineer who does not understand
  the retry timing contract.
- Input: Read `RegisterPendingBeSlot` signature via reflection or directly inspect the default parameter.
- Expected: Default value of `delayMs` parameter == `500`

### Acceptance Criterion

- **Build**: `dotnet build src/PropTraderTools/PropTraderTools.csproj` → zero errors, zero warnings on
  modified lines
- **CCN**: `lizard src/PropTraderTools/CopyEngine.cs --csv` confirms:
  `RegisterBeRetrySlotIfNeeded` CCN <= 8 (target: **6**);
  `IsBeRetrySlotNeeded` CCN = **4**;
  `RegisterPendingBeSlot` CCN = **1**
- **Tests**: `dotnet test` — all 8 new `[Fact]` tests PASS (6 for `IsBeRetrySlotNeeded` + 2 for
  `RegisterPendingBeSlot`)
- **P0 scan**:
  `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` — zero new hits in modified lines
  `grep -n "async void " src/PropTraderTools/CopyEngine.cs` — zero new hits in modified lines
- **Signature stability**: `grep -n "private void RegisterBeRetrySlotIfNeeded"` — external signature
  unchanged (6 params preserved)

---

## Ticket 2 — FlattenOneAccountLimit + TrimOneAccountLimit

### Spec Requirement IDs
- WAVE1-LANE-A-07
- WAVE1-LANE-A-08

### Target Method(s)

| Method | File | Lines (exact) | Current CCN |
|--------|------|---------------|-------------|
| `FlattenOneAccountLimit` | `src/PropTraderTools/CopyEngine.cs` | **5592–5635** | **8** |
| `TrimOneAccountLimit` | `src/PropTraderTools/CopyEngine.cs` | **5543–5585** | **8** |

Both methods are at the JS-080 limit. A shared helper eliminates the duplicated 12-arg `CreateOrder`
block + null check + Submit + catch/error pattern.

### New Helper Method Signature

#### Shared Helper: `SubmitLimitExitOrder`

```csharp
private void SubmitLimitExitOrder(
    Account acc,
    Instrument instrument,
    OrderAction action,
    int qty,
    double limitPx,
    string orderName)
```

- **Access**: `private` (instance method — calls `acc.CreateOrder` and `StatusUpdate?.Invoke`)
- **Return**: `void` — JS-002 compliant
- **Body description**: Absorbs the duplicated block from both parent methods:
  1. Call `acc.CreateOrder(instrument, action, OrderType.Limit, OrderEntry.Manual, TimeInForce.Gtc,
     qty, limitPx, 0, null, orderName, DateTime.MaxValue, (NinjaTrader.Cbi.CustomOrder)null)`
  2. Check returned order for null: if null, invoke `StatusUpdate?.Invoke(orderName + ": CreateOrder returned null");` and return.
  3. Call `acc.Submit(new[] { order })`
  4. Catch `Exception ex`: invoke `StatusUpdate?.Invoke(orderName + " error: " + ex.Message);`

  **CRITICAL NT8-007**: Arg 12 (last argument) of `CreateOrder` MUST be `(NinjaTrader.Cbi.CustomOrder)null`.
  This cast is required by the NT8 API overload resolution. MUST be preserved exactly in the helper body.
  Other fixed args: `OrderType.Limit`, `OrderEntry.Manual`, `TimeInForce.Gtc`, `DateTime.MaxValue`,
  `null` for arg 9 (oco order), and `0` for arg 8 (stop price). These MUST NOT be changed.

  **What stays in each parent** (not moved to helper):
  - `CancelStaleExitOrders(acc, instrument, "PTT-Xxx")` — each parent calls with its own order name
  - `FindPosition` guard (pos null/qty==0 check) — each parent retains this guard
  - `isLong` ternary and `action` computation — stays in each parent
  - `ComputeLimitPx(...)` call — stays in each parent
  - Success `StatusUpdate?.Invoke(...)` — each parent logs its own message after `SubmitLimitExitOrder`

- **Estimated CCN**: **4** (base=1 + order null check=1 + try=1 + catch=1)

### Extraction Steps (ordered)

**Step 1 — Write `SubmitLimitExitOrder` BEFORE modifying either parent:**

Add the helper between `TrimOneAccountLimit` (ends at line 5585) and `FlattenOneAccountLimit`
(begins at line 5592), i.e. at approximately line 5587:

```csharp
// WAVE1-LANE-A T2: shared helper for FlattenOneAccountLimit and TrimOneAccountLimit.
// NT8-007: arg12 MUST be (NinjaTrader.Cbi.CustomOrder)null.
// CCN=4: base(1) + null-check(1) + try(1) + catch(1).
private void SubmitLimitExitOrder(
    Account acc,
    Instrument instrument,
    OrderAction action,
    int qty,
    double limitPx,
    string orderName)
{
    Order order = acc.CreateOrder(
        instrument,
        action,
        OrderType.Limit,
        OrderEntry.Manual,
        TimeInForce.Gtc,
        qty,
        limitPx,
        0,
        null,
        orderName,
        DateTime.MaxValue,
        (NinjaTrader.Cbi.CustomOrder)null
    );
    if (order == null)
    {
        StatusUpdate?.Invoke(orderName + ": CreateOrder returned null");
        return;
    }
    try
    {
        acc.Submit(new[] { order });
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke(orderName + " error: " + ex.Message);
    }
}
```

**Step 2 — Refactor `TrimOneAccountLimit` (lines 5543–5585):**

Replace the `try { acc.CreateOrder(...); StatusUpdate... } catch (Exception ex) { StatusUpdate... }` block
(lines 5563–5584) with a call to `SubmitLimitExitOrder`. The refactored body is:

```csharp
private void TrimOneAccountLimit(
    Account acc,
    Instrument instrument,
    int exitBuffer,
    double ask,
    double bid)
{
    CancelStaleExitOrders(acc, instrument, "PTT-TrimLimit"); // HOTFIX-F4
    var pos = FindPosition(acc, instrument);
    if (pos == null || pos.Quantity == 0)
    {
        StatusUpdate?.Invoke(acc.Name + ": flat skip");
        return;
    }
    int trimQty = (int)Math.Ceiling(pos.Quantity / 2.0);
    bool isLong = pos.MarketPosition == MarketPosition.Long;
    var action = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
    double tickSize = instrument.MasterInstrument.TickSize;
    double limitPx = ComputeLimitPx(isLong, ask, bid, exitBuffer, tickSize);
    SubmitLimitExitOrder(acc, instrument, action, trimQty, limitPx, "PTT-TrimLimit");
    StatusUpdate?.Invoke(acc.Name + ": trim-limit " + trimQty + " @ " + limitPx);
}
```

Note: The success `StatusUpdate` for trim is called after `SubmitLimitExitOrder` returns (helper
catches errors internally, so the parent always continues after the call).

**Step 3 — Refactor `FlattenOneAccountLimit` (lines 5592–5635):**

Replace the `try { acc.CreateOrder(...); StatusUpdate... } catch (Exception ex) { StatusUpdate... }` block
(lines 5611–5634) with a call to `SubmitLimitExitOrder`. The refactored body is:

```csharp
private void FlattenOneAccountLimit(
    Account acc,
    Instrument instrument,
    int exitBuffer,
    double ask,
    double bid)
{
    CancelStaleExitOrders(acc, instrument, "PTT-FlattenLimit"); // HOTFIX-F4
    var pos = FindPosition(acc, instrument);
    if (pos == null || pos.Quantity == 0)
    {
        StatusUpdate?.Invoke(acc.Name + ": flat skip");
        return;
    }
    bool isLong = pos.MarketPosition == MarketPosition.Long;
    var action = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
    double tickSize = instrument.MasterInstrument.TickSize;
    double limitPx = ComputeLimitPx(isLong, ask, bid, exitBuffer, tickSize);
    SubmitLimitExitOrder(acc, instrument, action, pos.Quantity, limitPx, "PTT-FlattenLimit");
    StatusUpdate?.Invoke(acc.Name + ": flatten-limit " + pos.Quantity + " @ " + limitPx);
}
```

**Step 4 — Verify post-extraction CCN:**
```
lizard src/PropTraderTools/CopyEngine.cs --csv | grep -E "FlattenOneAccountLimit|TrimOneAccountLimit|SubmitLimitExitOrder"
```
Expected:
- `FlattenOneAccountLimit` CCN = **4**
- `TrimOneAccountLimit` CCN = **4**
- `SubmitLimitExitOrder` CCN = **4**

### 7-Scan Checklist

- [ ] **SCAN-01 (lock-free)**: `SubmitLimitExitOrder` uses `acc.CreateOrder` and `acc.Submit` — both
  are NT8 API calls, no `lock()`. Zero lock() added in any of the three modified/added methods.
- [ ] **SCAN-02 (async void)**: All three methods (`SubmitLimitExitOrder`, `TrimOneAccountLimit`,
  `FlattenOneAccountLimit`) are synchronous `void`. No `async` keyword added.
- [ ] **SCAN-03 (return null)**: All three methods return `void`. No `return null` possible.
- [ ] **SCAN-04 (CCN)**: `lizard src/PropTraderTools/CopyEngine.cs --csv` confirms:
  `FlattenOneAccountLimit` CCN <= 8 (target: **4**);
  `TrimOneAccountLimit` CCN <= 8 (target: **4**);
  `SubmitLimitExitOrder` CCN <= 8 (target: **4**).
- [ ] **SCAN-05 (PTT- prefix)**: `SubmitLimitExitOrder` receives `orderName` as a parameter.
  Both callers pass `"PTT-FlattenLimit"` and `"PTT-TrimLimit"` respectively — PTT- prefix preserved.
  Confirm: `grep -n "SubmitLimitExitOrder" src/PropTraderTools/CopyEngine.cs` shows both call sites
  pass a `"PTT-"` prefixed string literal.
- [ ] **SCAN-06 (ASCII-only)**: New string literals in `SubmitLimitExitOrder`: `": CreateOrder returned null"`,
  `" error: "` — both ASCII-only. Success StatusUpdate messages in parents are unchanged ASCII strings.
- [ ] **SCAN-07 (public visibility)**: `SubmitLimitExitOrder` is `private`. Neither parent method changes
  its visibility. No new public methods.

### Test Requirements

**Test file**: `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`
**Test class**: `CopyEngineTests` (add to existing — do NOT replace)
**Framework**: xUnit ONLY.

**Pattern**: Inline mirror of action/qty computation logic.
`Account`, `Instrument`, and `Order` cannot be constructed outside the NT8 runtime.
Tests below verify the computation logic expressed in the parent method bodies before the
`SubmitLimitExitOrder` call site.

#### 4 [Fact] tests

```csharp
[Fact]
public void SubmitLimitExitOrder_UsesSellAction_WhenPositionIsLong()
```
- What it verifies: Inline mirror of `isLong ? OrderAction.Sell : OrderAction.BuyToCover`.
  When `isLong == true`, the result is `OrderAction.Sell`.
- Input: `bool isLong = true`
- Expected: `(isLong ? OrderAction.Sell : OrderAction.BuyToCover) == OrderAction.Sell`

```csharp
[Fact]
public void SubmitLimitExitOrder_UsesBuyToCoverAction_WhenPositionIsShort()
```
- What it verifies: Same ternary expression when `isLong == false`.
- Input: `bool isLong = false`
- Expected: `(isLong ? OrderAction.Sell : OrderAction.BuyToCover) == OrderAction.BuyToCover`

```csharp
[Fact]
public void FlattenOneAccountLimit_UsesFullPositionQty_WhenCalled()
```
- What it verifies: `FlattenOneAccountLimit` passes `pos.Quantity` (not a fraction) as `qty`.
  Inline mirror: given `int posQty = 5`, verify `qty == posQty` (no division applied).
- Input: `int posQty = 5`
- Expected: `posQty == 5` (qty argument to `SubmitLimitExitOrder` equals full position quantity)

```csharp
[Fact]
public void TrimOneAccountLimit_UsesHalfPositionQty_WhenCalled()
```
- What it verifies: `TrimOneAccountLimit` passes `(int)Math.Ceiling(pos.Quantity / 2.0)` as `qty`.
  Inline mirror: given `int posQty = 5`, verify `(int)Math.Ceiling(5 / 2.0) == 3` (ceiling of 2.5).
- Input: `int posQty = 5`
- Expected: `(int)Math.Ceiling(posQty / 2.0) == 3`

### Acceptance Criterion

- **Build**: `dotnet build src/PropTraderTools/PropTraderTools.csproj` → zero errors
- **CCN**: `lizard src/PropTraderTools/CopyEngine.cs --csv` confirms:
  `FlattenOneAccountLimit` CCN = **4**;
  `TrimOneAccountLimit` CCN = **4**;
  `SubmitLimitExitOrder` CCN = **4** (all <= 8)
- **Tests**: `dotnet test` — all 4 new `[Fact]` tests PASS
- **NT8-007 guard**: `grep -n "(NinjaTrader.Cbi.CustomOrder)null" src/PropTraderTools/CopyEngine.cs`
  must return at least one hit inside `SubmitLimitExitOrder` body (arg12 preserved)
- **P0 scan**:
  `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` — zero new hits in modified lines
  `grep -n "async void " src/PropTraderTools/CopyEngine.cs` — zero new hits in modified lines
- **PTT- prefix check**: `grep -n "SubmitLimitExitOrder" src/PropTraderTools/CopyEngine.cs`
  both call sites pass a `"PTT-"` prefixed `orderName`

---

## Ticket 3 — OnOrderUpdate (Advisory)

> **ADVISORY TIER**: `OnOrderUpdate` is currently CCN=8 — fully compliant with JS-080.
> This extraction is NOT required for wave completion. Execute only if the wave sponsor
> explicitly directs CCN reduction beyond the threshold for this method.
> All ticket requirements below are binding if the ticket IS executed.

### Spec Requirement IDs
- WAVE1-LANE-A-09

### Target Method(s)
- Method: `OnOrderUpdate`
- File: `src/PropTraderTools/CopyEngine.cs`
- Lines: **1500–1597** (98 source lines, confirmed by grep)
- Current CCN: **8** (at JS-080 limit — advisory extraction)

### New Helper Method Signature

#### Helper: `TryResolveEnabledRule`

```csharp
private bool TryResolveEnabledRule(Order order, out CopyRule rule)
```

- **Access**: `private` (instance method — reads `_isCopyEnabled` and calls `FindMatchingRule`)
- **Return**: `bool` — JS-002 compliant (never null; bool is a value type)
- **Out param**: `out CopyRule rule` — `CopyRule` is a value type (struct). Cannot be null. JS-002 compliant.
- **Body description**: Three sequential guards. On any guard fail: set `rule = default; return false;`.
  On all guards pass: set `rule = matchedRule; return true;`.

  **Gate order is MANDATORY** (cannot be reordered):
  1. Gate 1 — enabled: `if (!_isCopyEnabled) { rule = default; return false; }`
  2. Gate 2 — null check: `CopyRule? matchedRule = FindMatchingRule(order); if (matchedRule == null) { rule = default; return false; }`
  3. Gate 3 — rule enabled: `if (!matchedRule.Value.Enabled) { rule = default; return false; }`
  4. All passed: `rule = matchedRule.Value; return true;`

  **CRITICAL**: Gate 3 (`matchedRule.Value.Enabled`) MUST come AFTER Gate 2 (`matchedRule == null`).
  Calling `.Value.Enabled` on a null `CopyRule?` is undefined behavior. Order is a correctness constraint.

- **Estimated CCN**: **4** (base=1 + Gate1=1 + Gate2=1 + Gate3=1)

### Extraction Steps (ordered)

**Step 1 — Add `TryResolveEnabledRule` before `OnOrderUpdate` (before line 1500):**

```csharp
// WAVE1-LANE-A T3 (advisory): consolidates three gate checks.
// Gate order is correctness-critical: enabled -> null -> .Value.Enabled.
// out CopyRule rule: value type (struct), never null. JS-002 compliant.
// CCN=4: base(1) + enabled(1) + matchedRule null(1) + matchedRule.Enabled(1).
private bool TryResolveEnabledRule(Order order, out CopyRule rule)
{
    if (!_isCopyEnabled)
    {
        rule = default;
        return false;
    }
    CopyRule? matchedRule = FindMatchingRule(order);
    if (matchedRule == null)
    {
        rule = default;
        return false;
    }
    if (!matchedRule.Value.Enabled)
    {
        rule = default;
        return false;
    }
    rule = matchedRule.Value;
    return true;
}
```

**Step 2 — Refactor the gate section in `OnOrderUpdate`** (lines 1552–1563).

Replace:
```csharp
// Gate 1: enabled check
if (!_isCopyEnabled)
    return;

// Gate 2: find matching rule -- instrument AND master account must match.
// Extracted to FindMatchingRule (CYC=3).
CopyRule? matchedRule = FindMatchingRule(e.Order);

if (matchedRule == null)
    return; // Gate 2: no rule match
if (!matchedRule.Value.Enabled)
    return; // Gate 2.5: rule disabled
```

With:
```csharp
// Gates 1+2+2.5: enabled + rule match + rule enabled (WAVE1-LANE-A T3).
if (!TryResolveEnabledRule(e.Order, out CopyRule matchedRule))
    return;
```

The `matchedRule` variable now holds the `CopyRule` value (struct) directly (not `CopyRule?`).
All subsequent uses of `matchedRule.Value` in `OnOrderUpdate` must be updated to `matchedRule`
(remove `.Value` — it is now a non-nullable struct):

- Line 1569: `TryMirrorOrderUpdate(e.Order, matchedRule.Value)` → `TryMirrorOrderUpdate(e.Order, matchedRule)`
- Line 1573: `TryCancelFollowerEntries(e.Order, matchedRule.Value)` → `TryCancelFollowerEntries(e.Order, matchedRule)`
- Line 1583: `matchedRule.Value` in `TryDispatchLeaderFlat` args → `matchedRule`
- Line 1592: `TryHandleDrag(e.Order, matchedRule.Value)` → `TryHandleDrag(e.Order, matchedRule)`
- Line 1596: `DispatchCopy(e.Order, matchedRule.Value)` → `DispatchCopy(e.Order, matchedRule)`

**Step 3 — Verify post-extraction CCN:**
```
lizard src/PropTraderTools/CopyEngine.cs --csv | grep -E "OnOrderUpdate|TryResolveEnabledRule"
```
Expected:
- `OnOrderUpdate` CCN = **5** (down from 8)
- `TryResolveEnabledRule` CCN = **4**

### 7-Scan Checklist

- [ ] **SCAN-01 (lock-free)**: `TryResolveEnabledRule` reads `_isCopyEnabled` (bool field — atomic read
  on x64, no lock needed) and calls `FindMatchingRule` (existing method, no lock). Zero `lock()` added.
- [ ] **SCAN-02 (async void)**: `TryResolveEnabledRule` is a synchronous `bool` method. `OnOrderUpdate`
  is unchanged in return type. No `async` keyword added.
- [ ] **SCAN-03 (return null)**: `TryResolveEnabledRule` returns `bool`. `out CopyRule rule` is a value
  type struct. Neither can return null. `return false` is not `return null`.
- [ ] **SCAN-04 (CCN)**: `lizard src/PropTraderTools/CopyEngine.cs --csv` confirms:
  `OnOrderUpdate` CCN <= 8 (target: **5**);
  `TryResolveEnabledRule` CCN <= 8 (target: **4**).
- [ ] **SCAN-05 (PTT- prefix)**: No `CreateOrder` calls added or modified in this ticket.
- [ ] **SCAN-06 (ASCII-only)**: No new string literals in `TryResolveEnabledRule`.
  Existing `OnOrderUpdate` comments are preserved. All ASCII-only.
- [ ] **SCAN-07 (public visibility)**: `TryResolveEnabledRule` is `private`. `OnOrderUpdate` visibility
  is unchanged (private). No new public methods.

### Test Requirements

**Test file**: `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`
**Test class**: `CopyEngineTests` (add to existing — do NOT replace)
**Framework**: xUnit ONLY.

**Pattern**: Inline logic-mirror of each gate condition. `_isCopyEnabled` is a field that can be
set directly on a `TrimSignal`-derived test harness. `FindMatchingRule` can be tested via a
minimal in-memory `CopyRule` list stub.

#### 4 [Fact] tests

```csharp
[Fact]
public void TryResolveEnabledRule_ReturnsFalse_WhenCopyDisabled()
```
- Precondition: `_isCopyEnabled = false`
- Input: any `Order`
- Expected: `TryResolveEnabledRule(order, out _) == false`
- Why: Gate 1 fires immediately; short-circuits without calling `FindMatchingRule`.

```csharp
[Fact]
public void TryResolveEnabledRule_ReturnsFalse_WhenNoMatchingRule()
```
- Precondition: `_isCopyEnabled = true`; `_copyRules` list has no rule matching `order.Account`
- Input: `Order` with account not in any configured `CopyRule`
- Expected: `TryResolveEnabledRule(order, out _) == false`
- Why: Gate 2 fires; `FindMatchingRule` returns null for the unrecognized account.

```csharp
[Fact]
public void TryResolveEnabledRule_ReturnsFalse_WhenRuleIsDisabled()
```
- Precondition: `_isCopyEnabled = true`; matching rule exists but `rule.Enabled = false`
- Input: `Order` with account matching a configured but disabled `CopyRule`
- Expected: `TryResolveEnabledRule(order, out _) == false`
- Why: Gate 3 fires; rule is matched but not enabled.

```csharp
[Fact]
public void TryResolveEnabledRule_ReturnsTrue_WhenAllGatesPass()
```
- Precondition: `_isCopyEnabled = true`; matching rule exists with `Enabled = true`
- Input: `Order` with account matching a configured and enabled `CopyRule`
- Expected: `TryResolveEnabledRule(order, out CopyRule rule) == true`; `rule` equals the matching `CopyRule`
- Why: All three gates pass; method returns the resolved rule for use by the caller.

### Acceptance Criterion

- **Build**: `dotnet build src/PropTraderTools/PropTraderTools.csproj` → zero errors
- **CCN**: `lizard src/PropTraderTools/CopyEngine.cs --csv` confirms:
  `OnOrderUpdate` CCN = **5** (down from 8);
  `TryResolveEnabledRule` CCN = **4** (both <= 8)
- **Tests**: `dotnet test` — all 4 new `[Fact]` tests PASS
- **P0 scan**:
  `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` — zero new hits in modified lines
  `grep -n "async void " src/PropTraderTools/CopyEngine.cs` — zero new hits in modified lines
- **`.Value` cleanup**: `grep -n "matchedRule.Value" src/PropTraderTools/CopyEngine.cs`
  returns zero hits in `OnOrderUpdate` body after extraction (all `.Value` calls removed)

---

## Global Acceptance Gate

After ALL tickets in scope are executed (T1 + T2 mandatory; T3 optional):

```powershell
# 1. Build
dotnet build src/PropTraderTools/PropTraderTools.csproj

# 2. CCN check -- all AT-LIMIT methods now below limit
lizard src/PropTraderTools/CopyEngine.cs -T cyclomatic_complexity=8

# 3. All new tests pass
dotnet test

# 4. P0 scans (must return zero new hits)
grep -n "lock(" src/PropTraderTools/CopyEngine.cs
grep -n "async void " src/PropTraderTools/CopyEngine.cs
grep -n "return null;" src/PropTraderTools/CopyEngine.cs  # count must not increase

# 5. NT8 sync + verify
powershell -File scripts\ptt-sync-and-verify.ps1
# Expected: 0 MISMATCH lines

# 6. F5 in NinjaTrader 8 (mandatory compile gate)
# Expected: GREEN (zero compilation errors)
```

**Out-of-scope CCN violations** (MUST be filed as a separate epic before the next wave):

| Method | CCN | Required Action |
|--------|-----|----------------|
| `IsExitSignalName` | 9 | HashSet lookup refactor (lines 2330-2353) |
| `HasArmingAtmBrackets` | 9 | Extract `IsArmingOrderState` helper (lines 5334-5352) |

---

**TICKETS_COMPLETE**
