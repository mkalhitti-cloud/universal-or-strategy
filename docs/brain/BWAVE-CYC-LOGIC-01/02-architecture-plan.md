# BWAVE-CYC-LOGIC-01 — Architecture Plan

**Status:** Pending Re-Review (Cycle 1 Revision — V-001 fixed, 7 CYC estimates corrected)
**Architect:** PTT Architect (Bob CLI, ptt-architect mode)
**Date:** 2026-01-01
**Epic:** BWAVE-CYC-LOGIC-01
**File:** `src/PropTraderTools/CopyEngine.cs`

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

**Analysis:**
- Q1. All 70 stubs in one file (`CopyEngine.cs`), spanning L7877–8199 (~323 lines). NOT within 50 lines → technically multiple lanes eligible.
- Q2. Cross-stub dependencies: NONE. Every stub either (a) implements a pure predicate, or (b) delegates to **existing production methods already in the file** (`FindPosition`, `IsFlat`, `RegisterPendingBeSlot`, `SyncAtmFollowerBracket`, `CancelExistingPttStpDrag`, `CreateAndSubmitCollateralStop`, etc.). No stub calls another stub from this epic.
- Q3. Each ticket group has standalone value — predicates can ship without actions and the build still passes. Tests are existence-only (reflection `.GetMethod != null`) or NT8-runtime-skipped.
- Q4. Each group has an independent SIM verification path (BE arming, bracket sync, drag orders, TaR6 events).

**RESULT: SINGLE-PIPELINE** — Five serial tickets (T1→T5), one file, zero cross-ticket dependencies. Parallel lanes are NOT warranted because the 70 stubs have no callers yet; they are declaration-ready contracts wired in follow-on epics.

---

## Data Model Summary

### `CopyRule` (readonly struct, L459-550)
| Field | Type | Purpose |
|---|---|---|
| `Instrument` | `string` | FullName of the instrument |
| `MasterAccount` | `Account` | Leader account |
| `FollowerAccounts` | `Account[]` | Follower accounts (may contain nulls = unresolved) |
| `FollowerAccountNames` | `string[]` | Parallel names for lazy-resolve |
| `FollowerMultipliers` | `int[]` | Per-follower qty multipliers |
| `FollowerAtmTemplates` | `Dictionary<string,FollowerAtmMode>` | ATM mode per follower |
| `TightenTicks` | `int` | Default 5 |

### `PendingBeSlot` (internal struct, L248-260)
| Field | Type | Purpose |
|---|---|---|
| `Account` | `Account` | Arming account |
| `Instrument` | `Instrument` | Arming instrument |
| `BufferTicks` | `int` | Buffer ticks for BE stop placement |

### `PendingFollowerBeSlot` (private struct, L297-309)
Same shape as `PendingBeSlot` but stored in `_pendingFollowerBeSlots` dict.

### Key ConcurrentDictionary fields
| Field | Key | Value | Purpose |
|---|---|---|---|
| `_pendingBeSlots` | `acc.Name` | `PendingBeSlot` | BE arming slots (armed, waiting for trigger) |
| `_pendingFollowerBeSlots` | `acc.Name` | `PendingFollowerBeSlot` | Follower BE retry deferred slots |
| `_beReplaceAttempts` | `acc.Name` | `int` | BE bracket replace attempt counter (cap=5) |
| `_filledBeTargetCount` | `acc.Name` | `int` | Count of PTT-BE-Target-* fills this slot |
| `_qxCancelInProgress` | `acc.Name` | `bool` | QX cancel intent guard |
| `_qxPendingFollowerCleanup` | `acc.Name` | `(Instrument, DateTime)` | Cancel-after cleanup TTL entries |
| `_rules` | — | `ConcurrentBag<CopyRule>` | All copy rules |

### Order naming conventions (from production code)
| Name | Type | Purpose |
|---|---|---|
| `"PTT-BE-Stop"` | StopMarket | Break-even stop placed by BE path |
| `"PTT-BE-Target-*"` | Limit | BE target (BE OCO pair) |
| `"PTT-QX-T*"` | Limit | QuickExit target replacement |
| `"PTT-STP-Drag"` / `"PTT-STP-Drag-N"` | StopMarket | Drag stop replacement |
| `"PTT-TGT-Drag"` / `"PTT-TGT-Drag-N"` | Limit | Drag target replacement |
| `"PTT-Copy"` | Limit/Market | Entry copy |
| `"Stop1"…"Stop9"` | StopMarket | Native NT8 ATM stop brackets |
| `"Target1"…"Target9"` | Limit | Native NT8 ATM target brackets |

---

## Per-Method Implementation Design

### Legend
- **CYC** = McCabe cyclomatic complexity (manual count per project standard)
- **NT8 API** = Whether the method calls NT8 runtime API (Account.Cancel / .CreateOrder / .Submit / .Change / Position / Instrument.MarketData)
- **Deferred** = Whether to defer due to NT8-host-only requirements

---

### GROUP A — B79 CancelRaceGuard Helpers (L7885–7979)

---

#### A-01 `TryFireImmediateBeIfAlreadyAtLevel`
**Signature:** `private bool TryFireImmediateBeIfAlreadyAtLevel(Account acc, Instrument instr, Order tgtOrder, bool isLong, double refPx, double tickSize)`
**Line:** 7886
**Logic:**
```csharp
if (tickSize <= 0) return false;           // (1)
if (refPx <= 0) return false;              // (2)
if (tgtOrder == null) return false;        // (3)
double target = tgtOrder.LimitPrice;
if (isLong ? refPx >= target : refPx <= target)  // (4) if + (5) ?:
{
    BreakEven(acc, instr, 0);
    return true;
}
return false;
```
**CYC:** 5
**NT8 API:** Yes — calls `BreakEven(acc, instr, int)` which calls `MoveStopToBreakEven` → `acc.CreateOrder` / `acc.Submit`.
**Deferred:** No (AddOnBase `BreakEven` path is confirmed).

---

#### A-02 `IsPendingBeTriggerMet`
**Signature:** `private bool IsPendingBeTriggerMet(Account acc, Instrument instr, bool isLong)`
**Line:** 7890
**Logic:**
```csharp
PendingBeSlot slot;
if (!_pendingBeSlots.TryGetValue(acc.Name, out slot)) return false;   // (1)
double bid = GetMarketBidPrice(instr);
double ask = GetMarketAskPrice(instr);
double refPrice = SelectBeRefPriceByDirection(isLong, bid, ask);
if (refPrice <= 0) return false;                                       // (2)
var pos = FindPosition(acc, instr);
if (pos == null || pos.Quantity == 0) return false;                    // (3) if + (4) ||
double tick = GetBeTickSize(instr);
if (tick <= 0) return false;                                           // (5)
double direction = isLong ? -1.0 : 1.0;                               // (6) ?:
double target = pos.AveragePrice + direction * slot.BufferTicks * tick;
return isLong ? refPrice >= target : refPrice <= target;               // (7) ?:
```
**CYC:** 8
**NT8 API:** Yes — reads `Instrument.MarketData` (via `GetMarketBidPrice`/`GetMarketAskPrice`), `acc.Positions` (via `FindPosition`).
**Deferred:** No.

---

#### A-03 `IsEligibleBeTargetOrder`
**Signature:** `private bool IsEligibleBeTargetOrder(Order order, Instrument instr)`
**Line:** 7894
**Logic:**
```csharp
if (order == null) return false;                                                   // (1)
if (order.Instrument?.FullName != instr?.FullName) return false;                   // (2)
if (order.OrderType != OrderType.Limit) return false;                              // (3)
return IsBeTargetSnapshotState(order);                                             // (4) delegates to Group C
```
**CYC:** 4
**NT8 API:** No (Order field reads only).
**Deferred:** No.
**Note:** Forward reference to `IsBeTargetSnapshotState` (Group C, same class). C# compiler handles this within same class.

---

#### A-04 `IsNativeAtmTargetOrder`
**Signature:** `private bool IsNativeAtmTargetOrder(Order order)`
**Line:** 7898
**Logic:**
```csharp
return order != null
    && order.Name != null
    && order.Name.StartsWith("Target", StringComparison.Ordinal)
    && order.Name.Length > 6
    && char.IsDigit(order.Name[6]);
```
**CYC:** 5 (1 base + 4 `&&` operators counted as branches per project McCabe standard).
**NT8 API:** No.
**Deferred:** No.

---

#### A-05 `IsPttBeOrQxTargetOrder`
**Signature:** `private bool IsPttBeOrQxTargetOrder(Order order)`
**Line:** 7902
**Logic:**
```csharp
if (order == null || order.Name == null) return false;               // (1) if + (2) ||
return order.Name.StartsWith("PTT-BE-Target", StringComparison.Ordinal)   // no op
    || (order.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)       // (3) ||
        && order.Name.Length > 8                                            // (4) &&
        && char.IsDigit(order.Name[8]));                                    // (5) &&
```
**CYC:** 6
**NT8 API:** No.
**Deferred:** No.

---

#### A-06 `RegisterBeRetryIfNoTargets`
**Signature:** `private void RegisterBeRetryIfNoTargets(Account acc, Instrument instr, bool isRetry, int leaderCount)`
**Line:** 7906
**Logic:**
```csharp
RegisterBeRetrySlotIfNeeded(acc, instr, bufferTicks: 0, isRetry: isRetry,
    targetsCount: 0, leaderCount: leaderCount);
```
**CYC:** 1
**NT8 API:** Delegates to `RegisterBeRetrySlotIfNeeded` (existing L6472) which may call `QueueBeRetryFallback` → `Dispatcher.InvokeAsync`.
**Deferred:** No.
**Note:** `bufferTicks=0` is correct — matches pattern in `TryReplacePttBeBrackets` (L4611) where `new PendingFollowerBeSlot(acc, instr, 0)` is used when bufferTicks is unavailable.

---

#### A-07 `RegisterPartialTargetBeRetry`
**Signature:** `private void RegisterPartialTargetBeRetry(Account acc, Instrument instr, int targetsCount, int leaderCount)`
**Line:** 7910
**Logic:**
```csharp
RegisterBeRetrySlotIfNeeded(acc, instr, bufferTicks: 0, isRetry: false,
    targetsCount: targetsCount, leaderCount: leaderCount);
```
**CYC:** 1
**NT8 API:** Delegates to `RegisterBeRetrySlotIfNeeded`.
**Deferred:** No.

---

#### A-08 `CancelExistingStpDragOrders`
**Signature:** `private void CancelExistingStpDragOrders(Account acc, Instrument instr)`
**Line:** 7914
**Logic:**
```csharp
foreach (var o in acc.Orders.ToList())       // (1)
{
    if (!IsPttStpDragCancellable(o)) continue;  // (2) existing private static L3478
    if (o.Instrument?.FullName != instr?.FullName) continue; // (3)
    if (!o.Name.StartsWith("PTT-STP-Drag", StringComparison.Ordinal)) continue; // (4)
    try { acc.Cancel(new Order[] { o }); } catch { }
}
```
**CYC:** 4
**NT8 API:** Yes — `acc.Cancel`.
**Deferred:** No.

---

#### A-09 `CancelExistingTgtDragOrders`
**Signature:** `private void CancelExistingTgtDragOrders(Account acc, Instrument instr)`
**Line:** 7918
**Logic:** Identical shape to A-08 but filters on `"PTT-TGT-Drag"` prefix.
```csharp
foreach (var o in acc.Orders.ToList())
{
    bool stateOk = o.OrderState == OrderState.Working
        || o.OrderState == OrderState.Submitted
        || o.OrderState == OrderState.Accepted;
    if (!stateOk) continue;                                                        // (1)
    if (o.Instrument?.FullName != instr?.FullName) continue;                       // (2)
    if (!o.Name.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)) continue;   // (3)
    try { acc.Cancel(new Order[] { o }); } catch { }
}
```
**CYC:** 4 (3 continues = 3 branches + foreach = 4 total)
**NT8 API:** Yes — `acc.Cancel`.
**Deferred:** No.

---

#### A-10 `SubmitReplacementStopLeg`
**Signature:** `private void SubmitReplacementStopLeg(Account acc, Instrument instr, Order leaderOrder, double stopPrice)`
**Line:** 7922
**Logic:** Mirrors `CreateAndSubmitCollateralStop` (L3383) but takes instr directly.
```csharp
if (leaderOrder == null) return;  // (1)
try
{
    var o = acc.CreateOrder(instr, leaderOrder.OrderAction, OrderType.StopMarket,
        OrderEntry.Automated, TimeInForce.Day, leaderOrder.Quantity,
        0, stopPrice, string.Empty, "PTT-STP-Drag",
        NinjaTrader.Core.Globals.MaxDate, (NinjaTrader.Cbi.CustomOrder)null);
    if (o != null) acc.Submit(new[] { o });  // (2)
}
catch { }
```
**CYC:** 3
**NT8 API:** Yes — `acc.CreateOrder` + `acc.Submit`.
**Deferred:** No.
**NT8 Notes:** Order name `"PTT-STP-Drag"` (PTT- prefix required). `oco=""` (no OCO group). `OrderEntry.Automated`. `TimeInForce.Day`. `(CustomOrder)null` for last arg.

---

#### A-11 `SubmitReplacementTargetLeg`
**Signature:** `private void SubmitReplacementTargetLeg(Account acc, Instrument instr, Order leaderOrder, double targetPrice)`
**Line:** 7926
**Logic:** Mirrors `CreateAndSubmitCollateralTarget` (L3428) but takes `instr` directly.
```csharp
if (leaderOrder == null) return;  // (1)
try
{
    var o = acc.CreateOrder(instr, leaderOrder.OrderAction, OrderType.Limit,
        OrderEntry.Automated, TimeInForce.Day, leaderOrder.Quantity,
        targetPrice, 0, string.Empty, "PTT-TGT-Drag",
        NinjaTrader.Core.Globals.MaxDate, (NinjaTrader.Cbi.CustomOrder)null);
    if (o != null) acc.Submit(new[] { o });  // (2)
}
catch { }
```
**CYC:** 3
**NT8 API:** Yes — `acc.CreateOrder` + `acc.Submit`.
**Deferred:** No.

---

#### A-12 `IsReArmedAtmBracketCleanupRequired`
**Signature:** `private bool IsReArmedAtmBracketCleanupRequired(Order order, DateTime cutoff)`
**Line:** 7930
**Logic:** Delegates to existing `IsCleanupQxOrderOk` (L4721) + cutoff check.
```csharp
if (order == null) return false;                   // (1)
if (!IsCleanupQxOrderOk(order)) return false;      // (2) delegates to existing private static
return DateTime.UtcNow < cutoff;                   // (3)
```
**CYC:** 3
**NT8 API:** No (pure predicate using DateTime.UtcNow).
**Deferred:** No.

---

#### A-13 `FindMatchingNativeAtmBracket`
**Signature:** `private Order FindMatchingNativeAtmBracket(Account acc, Instrument instr, string namePrefix)`
**Line:** 7934
**Logic:**
```csharp
foreach (var o in acc.Orders.ToList())       // (1)
{
    if (o.Name == null || !o.Name.StartsWith(namePrefix, StringComparison.Ordinal)) continue; // (2)
    if (o.Instrument?.FullName != instr?.FullName) continue;    // (3)
    if (o.OrderState != OrderState.Working && o.OrderState != OrderState.Accepted) continue; // (4)
    return o;
}
return null;
```
**CYC:** 4
**NT8 API:** Yes — reads `acc.Orders`.
**Deferred:** No.

---

#### A-14 `TryFindRuleAndFollowerIndex` (revised — V-001 fix)
**Signature:** `private bool TryFindRuleAndFollowerIndex(Account acc, Instrument instr, out int followerIndex)`
**Line:** 7938
**Logic:** Calls new helper `IsFollowerAccountMatch` (A-14b) to keep CYC ≤ 8.
```csharp
followerIndex = -1;
foreach (var rule in _rules)                                              // (1)
{
    if (rule.Instrument != instr?.FullName) continue;                     // (2)
    for (int i = 0; i < rule.FollowerAccounts.Length; i++)                // (3)
    {
        if (!IsFollowerAccountMatch(rule.FollowerAccounts[i],
                rule.FollowerAccountNames, i, acc.Name)) continue;        // (4)
        followerIndex = i;
        return true;
    }
}
return false;
```
**CYC:** 5
**NT8 API:** No (iterates in-memory `_rules`).
**Deferred:** No.

---

#### A-14b `IsFollowerAccountMatch` — **NEW METHOD (ADD, not fill)**
**Signature:** `private static bool IsFollowerAccountMatch(Account follower, string[] followerNames, int index, string accName)`
**Line:** Insert immediately after A-14 stub (approximately L7941), before A-15.
**Add stub:** Engineer must insert a new stubbed declaration with `[ObfuscationAttribute(Feature="rename", Exclude=true)]` then fill the body.
**Logic:**
```csharp
if (follower != null) return follower.Name == accName;    // (1)
if (followerNames == null) return false;                   // (2)
if (index >= followerNames.Length) return false;           // (3)
return followerNames[index] == accName;
```
**CYC:** 4 (1 base + 3 if branches)
**NT8 API:** No (pure static predicate on parameters).
**Deferred:** No.
**Note:** Private static; no instance state required. Extracts the compound `nameMatch` boolean from A-14 to satisfy CYC ≤ 8. Called only by `TryFindRuleAndFollowerIndex`.

---

#### A-15 `HasActiveQxOrdersForInstrument`
**Signature:** `private bool HasActiveQxOrdersForInstrument(Account acc, Instrument instr)`
**Line:** 7942
**Logic:** Mirrors `HasActiveQxOrders` (L4647) but uses `instr.FullName` comparison.
```csharp
return acc.Orders.ToList().Any(o =>
    o.Name != null
    && o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)              // (1)
    && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Submitted) // (2)
    && o.Instrument?.FullName == instr?.FullName                            // (3)
);
```
**CYC:** 3
**NT8 API:** Yes — reads `acc.Orders`.
**Deferred:** No.

---

#### A-16 `SyncAtmFollowerStopBracket`
**Signature:** `private void SyncAtmFollowerStopBracket(Account acc, Instrument instr, Order leaderStop, double capturedPrice)`
**Line:** 7946
**Logic:** Find the follower's stop bracket for this instrument and sync it to the leader's stop.
```csharp
if (leaderStop == null || capturedPrice <= 0) return;   // (1) if + (2) ||
string suffix = DeriveLeaderBracketIndex(leaderStop).ToString();
var fo = FindFollowerBracketOrder(acc, leaderStop.FromEntrySignalName, isStop: true, leaderStop.Name);
if (fo == null) return;                                  // (3)
SyncAtmFollowerBracket(acc, fo, capturedPrice, suffix, leaderStop);
```
**CYC:** 3
**NT8 API:** Yes — delegates to `SyncAtmFollowerBracket` which calls `acc.Cancel` + `acc.CreateOrder` + `acc.Submit`.
**Deferred:** No.

---

#### A-17 `CancelStaleTgtDragOrders`
**Signature:** `private void CancelStaleTgtDragOrders(Account acc, Instrument instr, string leaderName)`
**Line:** 7950
**Logic:** Cancel any Working PTT-TGT-Drag-* orders for this account+instrument.
```csharp
foreach (var o in acc.Orders.ToList())    // (1)
{
    if (o.OrderState != OrderState.Working) continue;                            // (2)
    if (o.Instrument?.FullName != instr?.FullName) continue;                     // (3)
    if (!o.Name.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)) continue; // (4)
    try { acc.Cancel(new Order[] { o }); } catch { }
}
```
**CYC:** 4
**NT8 API:** Yes — `acc.Cancel`.
**Deferred:** No.

---

#### A-18 `CreateAndSubmitReplacementTarget`
**Signature:** `private Order CreateAndSubmitReplacementTarget(Account acc, Instrument instr, Order leaderOrder, double price)`
**Line:** 7954
**Logic:** Like `SubmitReplacementTargetLeg` but returns the created Order.
```csharp
if (leaderOrder == null) return null;   // (1)
try
{
    var o = acc.CreateOrder(instr, leaderOrder.OrderAction, OrderType.Limit,
        OrderEntry.Automated, TimeInForce.Day, leaderOrder.Quantity,
        price, 0, string.Empty, "PTT-TGT-Drag",
        NinjaTrader.Core.Globals.MaxDate, (NinjaTrader.Cbi.CustomOrder)null);
    if (o != null) acc.Submit(new[] { o });  // (2)
    return o;
}
catch { return null; }
```
**CYC:** 3
**NT8 API:** Yes — `acc.CreateOrder` + `acc.Submit`.
**Deferred:** No.

---

#### A-19 `HasInFlightFlattenOrder`
**Signature:** `private bool HasInFlightFlattenOrder(Account acc, Instrument instr)`
**Line:** 7958
**Logic:** Check for any active PTT-Flatten or PTT-FlattenLimit order.
```csharp
return acc.Orders.ToList().Any(o =>
    (o.OrderState == OrderState.Working || o.OrderState == OrderState.Submitted
     || o.OrderState == OrderState.Accepted)                                           // (1)
    && o.Instrument?.FullName == instr?.FullName                                       // (2)
    && o.Name != null
    && (o.Name.StartsWith("PTT-Flatten", StringComparison.Ordinal)                    // (3)
        || o.Name.StartsWith("PTT-Trim", StringComparison.Ordinal))                    // (4)
);
```
**CYC:** 4
**NT8 API:** Yes — reads `acc.Orders`.
**Deferred:** No.

---

#### A-20 `IsPositionFlatOrMissing` (private static)
**Signature:** `private static bool IsPositionFlatOrMissing(NinjaTrader.Cbi.Position pos)`
**Line:** 7962
**Logic:**
```csharp
return pos == null || pos.MarketPosition == MarketPosition.Flat || pos.Quantity == 0;
```
**CYC:** 3
**NT8 API:** No.
**Deferred:** No.
**Note:** Currently returns `true` (stub default). Correct semantics: null position OR flat position = true.

---

#### A-21 `IsLeaderTargetOrder`
**Signature:** `private bool IsLeaderTargetOrder(Order order)`
**Line:** 7966
**Logic:** Working Limit order with Target+digit name.
```csharp
if (order == null) return false;                                  // (1)
if (order.OrderState != OrderState.Working) return false;         // (2)
if (order.OrderType != OrderType.Limit) return false;             // (3)
return HasValidTargetNameSuffix(order.Name);                      // (4) delegates to Group C
```
**CYC:** 4
**NT8 API:** No.
**Deferred:** No.

---

#### A-22 `ResubmitFollowerEntry`
**Signature:** `private void ResubmitFollowerEntry(Account acc, Instrument instr, Order leaderEntry, CopyRule rule)`
**Line:** 7970
**Logic:** Re-place a follower's cancelled entry at the leader's current limit price.
```csharp
if (leaderEntry == null || leaderEntry.LimitPrice <= 0) return;   // (1) if + (2) ||
int multIdx = FindFollowerSlotIndex(rule, acc.Name);
int mult = (multIdx >= 0 && rule.FollowerMultipliers != null       // (3) ?: + (4) && + (5) &&
    && multIdx < rule.FollowerMultipliers.Length)
    ? rule.FollowerMultipliers[multIdx] : 1;
if (mult <= 0) mult = 1;                                            // (6)
int qty = leaderEntry.Quantity * mult;
try
{
    var o = acc.CreateOrder(instr, leaderEntry.OrderAction, OrderType.Limit,
        OrderEntry.Manual, TimeInForce.Gtc, qty, leaderEntry.LimitPrice,
        0, null, "PTT-Copy",
        DateTime.MaxValue, (NinjaTrader.Cbi.CustomOrder)null);
    if (o == null) return;                                          // (7)
    // Pre-load dedup cache to prevent DispatchCopy re-dispatch on Working event.
    _dedupCache[o.OrderId.ToString()] = leaderEntry.LimitPrice;
    acc.Submit(new[] { o });
}
catch { }
```
**CYC:** 8
**NT8 API:** Yes — `acc.CreateOrder` + `acc.Submit`. Writes `_dedupCache`.
**Deferred:** No.

---

#### A-23 `IsLeaderAccountForInstrument`
**Signature:** `private bool IsLeaderAccountForInstrument(Account acc, Instrument instr)`
**Line:** 7974
**Logic:**
```csharp
foreach (var rule in _rules)                                              // (1)
{
    if (rule.Instrument != instr?.FullName) continue;                     // (2)
    if (rule.MasterAccount?.Name == acc?.Name) return true;               // (3)
}
return false;
```
**CYC:** 3
**NT8 API:** No.
**Deferred:** No.

---

#### A-24 `CancelStaleCascadeTgtDrag`
**Signature:** `private void CancelStaleCascadeTgtDrag(Account acc, Instrument instr, string leaderName)`
**Line:** 7978
**Logic:** Cancel any Working PTT-TGT-Drag-* that does NOT belong to the current leader (stale from cascade).
```csharp
string suffix = string.IsNullOrEmpty(leaderName) ? string.Empty
    : ExtractLegSuffix(leaderName);                              // (1) ?:
foreach (var o in acc.Orders.ToList())                           // (2)
{
    if (o.OrderState != OrderState.Working) continue;            // (3)
    if (o.Instrument?.FullName != instr?.FullName) continue;     // (4)
    if (!o.Name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)) continue; // (5)
    if (!string.IsNullOrEmpty(suffix) && o.Name.EndsWith(suffix, StringComparison.Ordinal)) continue; // (6) if + (7) &&
    try { acc.Cancel(new Order[] { o }); } catch { }
}
```
**CYC:** 8
**NT8 API:** Yes — `acc.Cancel`.
**Deferred:** No.

---

### GROUP B — T1R1 BE Trigger/Arming Helpers (L7989–8040)

---

#### B-01 `GetMarketBidPrice`
**Signature:** `private double GetMarketBidPrice(Instrument instr)`
**Line:** 7990
**Logic:** `return instr?.MarketData?.Bid?.Price ?? 0.0;`
**CYC:** 1
**NT8 API:** Yes — reads `Instrument.MarketData.Bid.Price`.
**Deferred:** No.

---

#### B-02 `GetMarketAskPrice`
**Signature:** `private double GetMarketAskPrice(Instrument instr)`
**Line:** 7994
**Logic:** `return instr?.MarketData?.Ask?.Price ?? 0.0;`
**CYC:** 1
**NT8 API:** Yes — reads `Instrument.MarketData.Ask.Price`.
**Deferred:** No.

---

#### B-03 `GetBeTickSize`
**Signature:** `private double GetBeTickSize(Instrument instr)`
**Line:** 7998
**Logic:** `return instr?.MasterInstrument?.TickSize ?? 0.0;`
**CYC:** 1
**NT8 API:** Yes — reads `Instrument.MasterInstrument.TickSize`.
**Deferred:** No.

---

#### B-04 `SelectBeRefPriceByDirection` — **SKIP: already implemented**
**Line:** 8005–8008
Logic: `return isLong ? (bid > 0 ? bid : ask) : (ask > 0 ? ask : bid);` — **DO NOT TOUCH.**

---

#### B-05 `FireBeAndNotifyEvent`
**Signature:** `private void FireBeAndNotifyEvent(Account acc, Instrument instr, double bePrice, bool isLong)`
**Line:** 8011
**Logic:** Submit the BE stop then fire the pending-BE-fired notification event.
```csharp
SubmitBeStop(acc, instr, bePrice, isLong);
PendingBeFired?.Invoke(instr?.FullName ?? string.Empty, acc?.Name ?? string.Empty);
```
**CYC:** 1
**NT8 API:** Yes — delegates to `SubmitBeStop` (L1239) which calls `acc.CreateOrder` + `acc.Submit`.
**Deferred:** No.

---

#### B-06 `ShouldFireBeImmediately`
**Signature:** `private bool ShouldFireBeImmediately(Account acc, Instrument instr, double beTarget, bool isLong)`
**Line:** 8015
**Logic:**
```csharp
double bid = GetMarketBidPrice(instr);
double ask = GetMarketAskPrice(instr);
double refPrice = SelectBeRefPriceByDirection(isLong, bid, ask);
if (refPrice <= 0) return false;   // (1)
if (beTarget <= 0) return false;   // (2)
return isLong ? refPrice >= beTarget : refPrice <= beTarget;  // (3) ?:
```
**CYC:** 3
**NT8 API:** Yes — reads `Instrument.MarketData` via `GetMarketBidPrice`/`GetMarketAskPrice`.
**Deferred:** No.

---

#### B-07 `CompleteBeArming`
**Signature:** `private void CompleteBeArming(Account acc, Instrument instr, int bufferTicks)`
**Line:** 8019
**Logic:** Write the arming slot and fire the armed notification.
```csharp
_pendingBeSlots[acc.Name] = new PendingBeSlot(acc, instr, bufferTicks);
PendingBeArmed?.Invoke(instr?.FullName ?? string.Empty, acc?.Name ?? string.Empty);
```
**CYC:** 1
**NT8 API:** No direct NT8 API. Writes `_pendingBeSlots` (ConcurrentDictionary indexer = lock-free).
**Deferred:** No.

---

#### B-08 `TryClaimPendingBeSlot`
**Signature:** `private bool TryClaimPendingBeSlot(string accName, Instrument instr)`
**Line:** 8023
**Logic:** Atomically remove the BE slot for accName and verify it matches instr.
```csharp
PendingBeSlot slot;
if (!_pendingBeSlots.TryRemove(accName, out slot)) return false;     // (1)
return slot.Instrument?.FullName == instr?.FullName;                  // (2)
```
**CYC:** 2
**NT8 API:** No.
**Deferred:** No.

---

#### B-09 `GetSlotInstrumentName`
**Signature:** `private string GetSlotInstrumentName(string accName)`
**Line:** 8027
**Logic:**
```csharp
PendingBeSlot slot;
if (!_pendingBeSlots.TryGetValue(accName, out slot)) return string.Empty;  // (1)
return slot.Instrument?.FullName ?? string.Empty;
```
**CYC:** 1
**NT8 API:** No.
**Deferred:** No.

---

#### B-10 `GetSlotAccountName`
**Signature:** `private string GetSlotAccountName(string instrName)`
**Line:** 8031
**Logic:** Reverse lookup — find the first slot whose Instrument.FullName matches instrName.
```csharp
foreach (var kvp in _pendingBeSlots)                                       // (1)
{
    if (kvp.Value.Instrument?.FullName == instrName)                       // (2)
        return kvp.Value.Account?.Name ?? string.Empty;
}
return string.Empty;
```
**CYC:** 2
**NT8 API:** No.
**Deferred:** No.

---

#### B-11 `RaisePendingBeFiredEvent`
**Signature:** `private void RaisePendingBeFiredEvent(string instrName, string accName)`
**Line:** 8035
**Logic:** `PendingBeFired?.Invoke(instrName, accName);`
**CYC:** 1
**NT8 API:** No (fires event — subscribers marshal to UI thread internally per existing pattern).
**Deferred:** No.

---

#### B-12 `SettleAndFirePendingBe`
**Signature:** `private void SettleAndFirePendingBe(string accName, Instrument instr)`
**Line:** 8039
**Logic:**
```csharp
PendingBeSlot slot;
if (!_pendingBeSlots.TryRemove(accName, out slot)) return;               // (1)
if (IsFlat(FindPosition(slot.Account, instr))) return;                   // (2)
MoveStopToBreakEven(slot.Account, instr, slot.BufferTicks);              // (3)
```
**CYC:** 3
**NT8 API:** Yes — delegates to `MoveStopToBreakEven` (L6314) which orchestrates BE cancel+resubmit.
**Deferred:** No.

---

### GROUP C — TaR2 Target-Selection Helpers (L8048–8066)

---

#### C-01 `HasValidTargetNameSuffix`
**Signature:** `private bool HasValidTargetNameSuffix(string orderName)`
**Line:** 8049
**Logic:**
```csharp
return orderName != null
    && orderName.StartsWith("Target", StringComparison.Ordinal)
    && orderName.Length > 6
    && char.IsDigit(orderName[6]);
```
**CYC:** 5 (1 base + 4 `&&` operators per project McCabe standard).
**NT8 API:** No.
**Deferred:** No.

---

#### C-02 `SelectBeTargetList`
**Signature:** `private System.Collections.Generic.IList<Order> SelectBeTargetList(Account acc, Instrument instr)`
**Line:** 8053
**Logic:** Collect all BE-eligible target orders (mirrors `SnapshotBeTargets` inner logic).
```csharp
var result = new System.Collections.Generic.List<Order>();
foreach (Order o in acc.Orders.ToList())                                  // (1)
{
    if (!IsEligibleBeTargetOrder(o, instr)) continue;                     // (2)
    if (IsNativeAtmTargetOrder(o) || IsPttBeOrQxTargetOrder(o))          // (3)
        result.Add(o);
}
return result;
```
**CYC:** 3
**NT8 API:** Yes — reads `acc.Orders`.
**Deferred:** No.

---

#### C-03 `IsBeTargetActiveState`
**Signature:** `private bool IsBeTargetActiveState(Order order)`
**Line:** 8057
**Logic:**
```csharp
return order != null
    && (order.OrderState == OrderState.Working
        || order.OrderState == OrderState.Accepted);
```
**CYC:** 2
**NT8 API:** No.
**Deferred:** No.

---

#### C-04 `IsBeTargetPendingChangeState`
**Signature:** `private bool IsBeTargetPendingChangeState(Order order)`
**Line:** 8061
**Logic:**
```csharp
return order != null
    && (order.OrderState == OrderState.ChangeSubmitted
        || order.OrderState == OrderState.ChangePending);
```
**CYC:** 2
**NT8 API:** No.
**Deferred:** No.

---

#### C-05 `IsBeTargetSnapshotState`
**Signature:** `private bool IsBeTargetSnapshotState(Order order)`
**Line:** 8065
**Logic:** Union of active and pending-change states.
```csharp
return IsBeTargetActiveState(order) || IsBeTargetPendingChangeState(order);
```
**CYC:** 1
**NT8 API:** No.
**Deferred:** No.

---

### GROUP D — TaR3 Sync/Drag/Bracket and BE-Retry Helpers (L8075–8169)

---

#### D-01 `TrySyncAtmBrackets`
**Signature:** `private bool TrySyncAtmBrackets(Order leaderOrder, Account followerAcc, CopyRule rule)`
**Line:** 8076
**Logic:** Find the follower bracket and sync it via existing `SyncAtmFollowerBracket` / `SyncAtmFollowerTarget`.
```csharp
if (leaderOrder == null || followerAcc == null) return false;       // (1) if + (2) ||
bool isStop = IsAtmSTPOrder(leaderOrder)                            // (3) &&
    && leaderOrder.OrderType != OrderType.Limit;
var fo = FindFollowerBracketOrder(followerAcc,
    leaderOrder.FromEntrySignalName, isStop, leaderOrder.Name);
if (fo == null) return false;                                        // (4)
string suffix = DeriveLeaderBracketIndex(leaderOrder).ToString();
if (isStop)                                                          // (5)
    SyncAtmFollowerBracket(followerAcc, fo, leaderOrder.StopPrice, suffix, leaderOrder);
else
    SyncAtmFollowerTarget(followerAcc, fo, leaderOrder.LimitPrice, leaderOrder);
return true;
```
**CYC:** 6
**NT8 API:** Yes — delegates to `SyncAtmFollowerBracket` / `SyncAtmFollowerTarget` (acc.Cancel + acc.CreateOrder + acc.Submit).
**Deferred:** No.

---

#### D-02 `TrySkipTrailingStop`
**Signature:** `private bool TrySkipTrailingStop(Order leaderOrder, Account followerAcc, CopyRule rule)`
**Line:** 8080
**Logic:** Skip sync when the leader stop is already a PTT-generated drag replacement.
```csharp
if (leaderOrder == null) return false;                                  // (1)
return leaderOrder.Name != null
    && (leaderOrder.Name.StartsWith("PTT-STP-Drag-", StringComparison.Ordinal)  // (2)
        || leaderOrder.Name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal));
```
**CYC:** 2
**NT8 API:** No.
**Deferred:** No.

---

#### D-03 `SyncStandardBracket`
**Signature:** `private void SyncStandardBracket(Order leaderOrder, Account followerAcc, Instrument instr, CopyRule rule)`
**Line:** 8084
**Logic:** Delegate to the appropriate sync helper based on order type.
```csharp
if (leaderOrder == null || followerAcc == null) return;              // (1)
bool isStop = leaderOrder.OrderType == OrderType.StopMarket
    || leaderOrder.OrderType == OrderType.StopLimit;                 // expression
var fo = FindFollowerBracketOrder(followerAcc,
    leaderOrder.FromEntrySignalName, isStop, leaderOrder.Name);
if (fo == null) return;                                               // (2)
string suffix = DeriveLeaderBracketIndex(leaderOrder).ToString();
if (isStop)                                                           // (3)
    SyncAtmFollowerBracket(followerAcc, fo, leaderOrder.StopPrice, suffix, leaderOrder);
else                                                                  // (4)
    SyncAtmFollowerTarget(followerAcc, fo, leaderOrder.LimitPrice, leaderOrder);
```
**CYC:** 4
**NT8 API:** Yes — delegates to `SyncAtmFollowerBracket` / `SyncAtmFollowerTarget`.
**Deferred:** No.

---

#### D-04 `IsPttTgtDragOrder`
**Signature:** `private bool IsPttTgtDragOrder(Order order)`
**Line:** 8088
**Logic:** `return order?.Name != null && order.Name.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal);`
**CYC:** 2
**NT8 API:** No.
**Deferred:** No.

---

#### D-05 `IsAtmTgtOrder`
**Signature:** `private bool IsAtmTgtOrder(Order order)`
**Line:** 8092
**Logic:** Identical semantics to `IsNativeAtmTargetOrder` (A-04).
```csharp
return order?.Name != null
    && order.Name.StartsWith("Target", StringComparison.Ordinal)
    && order.Name.Length > 6
    && char.IsDigit(order.Name[6]);
```
**CYC:** 4
**NT8 API:** No.
**Deferred:** No.

---

#### D-06 `IsBePendingTargetOrder`
**Signature:** `private bool IsBePendingTargetOrder(Order order)`
**Line:** 8096
**Logic:** Mirrors `IsPttBeRetryTriggerOrder` (L1711).
```csharp
return IsPttQxTargetOrder(order) || IsNativeAtmBeRetryTarget(order);
```
**CYC:** 1
**NT8 API:** No.
**Deferred:** No.

---

#### D-07 `IsPttBeStopRejected`
**Signature:** `private bool IsPttBeStopRejected(Order order)`
**Line:** 8100
**Logic:** Mirrors the rejected sub-condition of `IsEvictTriggerState` (L1795).
```csharp
return order != null
    && order.OrderState == OrderState.Rejected
    && order.Name == "PTT-BE-Stop";
```
**CYC:** 3
**NT8 API:** No.
**Deferred:** No.

---

#### D-08 `IsPttDragOrderCancellable`
**Signature:** `private bool IsPttDragOrderCancellable(Order order, Instrument instr)`
**Line:** 8104
**Logic:** Extended version of `IsPttDragOrphanCancellable` (L1859) that also matches numbered drag variants.
```csharp
if (order == null) return false;                                         // (1)
if (order.OrderState != OrderState.Working) return false;                // (2)
if (order.Instrument?.FullName != instr?.FullName) return false;         // (3)
return order.Name != null
    && (order.Name == "PTT-TGT-Drag"
        || order.Name == "PTT-STP-Drag"
        || order.Name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)  // (4)
        || order.Name.StartsWith("PTT-STP-Drag-", StringComparison.Ordinal)); // (5)
```
**CYC:** 5
**NT8 API:** No.
**Deferred:** No.

---

#### D-09 `IsPttQxTargetOrder`
**Signature:** `private bool IsPttQxTargetOrder(Order order)`
**Line:** 8108
**Logic:**
```csharp
return order?.Name != null
    && order.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
    && order.Name.Length > 8
    && char.IsDigit(order.Name[8]);
```
**CYC:** 4
**NT8 API:** No.
**Deferred:** No.

---

#### D-10 `IsNativeAtmBeRetryTarget`
**Signature:** `private bool IsNativeAtmBeRetryTarget(Order order)`
**Line:** 8112
**Logic:** Same semantics as `IsAtmTgtOrder` — a native ATM Target1..9 order that can serve as a retry trigger.
```csharp
return order?.Name != null
    && order.Name.StartsWith("Target", StringComparison.Ordinal)
    && order.Name.Length > 6
    && char.IsDigit(order.Name[6]);
```
**CYC:** 4
**NT8 API:** No.
**Deferred:** No.

---

#### D-11 `IsBeRetryEligibleOrderState`
**Signature:** `private bool IsBeRetryEligibleOrderState(Order order)`
**Line:** 8116
**Logic:** Mirrors `IsBeRetryStateWorking` (L1727).
```csharp
return order != null
    && (order.OrderState == OrderState.Working
        || order.OrderState == OrderState.Accepted);
```
**CYC:** 3
**NT8 API:** No.
**Deferred:** No.

---

#### D-12 `IsBeRetryOrderInvalid`
**Signature:** `private bool IsBeRetryOrderInvalid(Order order)`
**Line:** 8120
**Logic:** Inverse of `IsBeRetryOrderValid` (L1705).
```csharp
return order == null || order.Name == null || order.Account == null;
```
**CYC:** 3
**NT8 API:** No.
**Deferred:** No.

---

#### D-13 `IsBeSlotNonTerminal`
**Signature:** `private bool IsBeSlotNonTerminal(string accName)`
**Line:** 8124
**Logic:** Check whether a pending follower BE slot still exists (not yet consumed or expired).
```csharp
return _pendingFollowerBeSlots.ContainsKey(accName);
```
**CYC:** 1
**NT8 API:** No.
**Deferred:** No.

---

#### D-14 `IsBeFilledWithOpenPosition`
**Signature:** `private bool IsBeFilledWithOpenPosition(Account acc, Instrument instr)`
**Line:** 8128
**Logic:** Returns true when at least one PTT-BE-Target-* has filled AND the position is still open.
```csharp
int count;
_filledBeTargetCount.TryGetValue(acc.Name, out count);
if (count <= 0) return false;                                           // (1)
return !IsFlat(FindPosition(acc, instr));                               // (2)
```
**CYC:** 2
**NT8 API:** Yes — reads `acc.Positions` (via `FindPosition`).
**Deferred:** No.

---

#### D-15 `IsPttDragOrderName`
**Signature:** `private bool IsPttDragOrderName(string orderName)`
**Line:** 8132
**Logic:**
```csharp
return orderName != null
    && (orderName.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)
        || orderName.StartsWith("PTT-STP-Drag", StringComparison.Ordinal));
```
**CYC:** 3
**NT8 API:** No.
**Deferred:** No.

---

#### D-16 `IsDragInstrumentMatch`
**Signature:** `private bool IsDragInstrumentMatch(Order order, Instrument instr)`
**Line:** 8136
**Logic:**
```csharp
return order?.Instrument?.FullName == instr?.FullName;
```
**CYC:** 1
**NT8 API:** No.
**Deferred:** No.

---

#### D-17 `IsQxTOrderStateValid`
**Signature:** `private bool IsQxTOrderStateValid(Order order)`
**Line:** 8140
**Logic:** Mirrors the state portion of `IsCleanupQxOrderOk` (L4722).
```csharp
return order != null
    && (order.OrderState == OrderState.Working
        || order.OrderState == OrderState.Accepted);
```
**CYC:** 3
**NT8 API:** No.
**Deferred:** No.

---

#### D-18 `IsQxTBracketNameValid`
**Signature:** `private bool IsQxTBracketNameValid(Order order)`
**Line:** 8144
**Logic:** Mirrors the name portion of `IsCleanupQxOrderOk` (L4725-4728).
```csharp
return order?.Name != null
    && order.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
    && order.Name.Length >= 9
    && char.IsDigit(order.Name[8]);
```
**CYC:** 4
**NT8 API:** No.
**Deferred:** No.

---

#### D-19 `TryGetCleanupEntryForFollower`
**Signature:** `private bool TryGetCleanupEntryForFollower(string followerAccName, out object entry)`
**Line:** 8148
**Logic:** Retrieve the `_qxPendingFollowerCleanup` tuple and return it as a boxed `object` (type-erasure for test isolation).
```csharp
(Instrument Instr, DateTime Expiry) tuple;
if (_qxPendingFollowerCleanup.TryGetValue(followerAccName, out tuple))  // (1)
{
    entry = tuple;
    return true;
}
entry = null;
return false;
```
**CYC:** 2
**NT8 API:** No.
**Deferred:** No.

---

#### D-20 `IsCleanupEntryCurrentAndMatching`
**Signature:** `private bool IsCleanupEntryCurrentAndMatching(object entry, Order order)`
**Line:** 8152
**Logic:** Unbox the tuple and verify TTL + instrument match.
```csharp
if (entry == null || order == null) return false;                        // (1)
if (!(entry is ValueTuple<Instrument, DateTime>)) return false;          // (2)
var t = (ValueTuple<Instrument, DateTime>)entry;
return DateTime.UtcNow < t.Item2                                         // (3)
    && t.Item1?.FullName == order.Instrument?.FullName;                   // (4)
```
**CYC:** 4
**NT8 API:** No.
**Deferred:** No.
**Note:** `.NET 4.8` supports `ValueTuple<T1,T2>` and `is`-pattern for value tuples (C# 7.0 in .NET 4.8). The `ValueTuple` struct type matches the `(Instrument, DateTime)` tuple syntax.

---

#### D-21 `SendAtmCancelReplace`
**Signature:** `private void SendAtmCancelReplace(Account acc, Order order, double newPrice)`
**Line:** 8156
**Logic:** Cancel the existing order then create a replacement at the new price (cancel+resubmit = AddOnBase pattern for ATM brackets; acc.Change is a silent no-op on ATM-owned brackets).
```csharp
if (order == null || newPrice <= 0) return;   // (1)
try { acc.Cancel(new Order[] { order }); } catch { }
bool isStop = order.OrderType == OrderType.StopMarket
    || order.OrderType == OrderType.StopLimit;                   // (2)
string suffix = DeriveLeaderBracketIndex(order).ToString();
if (string.IsNullOrEmpty(suffix) || suffix == "0") suffix = "1";
if (isStop)
    SyncAtmFollowerBracket(acc, order, newPrice, suffix, null);
else
    SyncAtmFollowerTarget(acc, order, newPrice, null);
```
**CYC:** 3
**NT8 API:** Yes — `acc.Cancel` + delegates to `SyncAtmFollowerBracket`/`SyncAtmFollowerTarget`.
**Deferred:** No.

---

#### D-22 `TryMatchFollowerInRule`
**Signature:** `private bool TryMatchFollowerInRule(Account acc, Instrument instr, out int followerIndex)`
**Line:** 8160
**Logic:** Same as `TryFindRuleAndFollowerIndex` (A-14) — finds follower index in the matching rule.
```csharp
followerIndex = -1;
foreach (var rule in _rules)                                               // (1)
{
    if (rule.Instrument != instr?.FullName) continue;                      // (2)
    int idx = FindFollowerSlotIndex(rule, acc?.Name ?? string.Empty);      // (3)
    if (idx >= 0) { followerIndex = idx; return true; }                    // (4)
}
return false;
```
**CYC:** 4
**NT8 API:** No.
**Deferred:** No.

---

#### D-23 `IsBeReplaceTargetValid`
**Signature:** `private bool IsBeReplaceTargetValid(Order order)`
**Line:** 8164
**Logic:** A BE replace target is valid if it is in a live or pending-change state and is a Limit order.
```csharp
if (order == null) return false;                                          // (1)
if (order.OrderType != OrderType.Limit) return false;                     // (2)
return order.OrderState == OrderState.Working                             // (3)
    || order.OrderState == OrderState.Accepted
    || order.OrderState == OrderState.ChangeSubmitted;
```
**CYC:** 4
**NT8 API:** No.
**Deferred:** No.

---

#### D-24 `TryIncrementBeReplaceAttempt`
**Signature:** `private bool TryIncrementBeReplaceAttempt(string accName)`
**Line:** 8168
**Logic:** Increment `_beReplaceAttempts` counter and return false if cap (5) exceeded.
```csharp
int current;
_beReplaceAttempts.TryGetValue(accName, out current);
if (current >= 5) return false;                 // (1) matches TryReplacePttBeBrackets cap L4598
_beReplaceAttempts[accName] = current + 1;
return true;
```
**CYC:** 2
**NT8 API:** No.
**Deferred:** No.

---

### GROUP E — TaR6 Static Predicates + Instance Helpers (L8181–8199)

---

#### E-01 `IsBracketOrderLiveState` (private static)
**Signature:** `private static bool IsBracketOrderLiveState(Order order)`
**Line:** 8182
**Logic:**
```csharp
return order != null
    && (order.OrderState == OrderState.Working
        || order.OrderState == OrderState.Accepted
        || order.OrderState == OrderState.Submitted
        || order.OrderState == OrderState.ChangeSubmitted);
```
**CYC:** 4
**NT8 API:** No.
**Deferred:** No.

---

#### E-02 `MatchesPttReplacementName` (private static)
**Signature:** `private static bool MatchesPttReplacementName(string leaderName, string suffix, string followerName)`
**Line:** 8186
**Logic:** Check if `followerName` is the PTT drag replacement for the leader's bracket at `suffix`.
```csharp
if (string.IsNullOrEmpty(suffix) || string.IsNullOrEmpty(followerName)) return false;  // (1)
return followerName == "PTT-STP-Drag-" + suffix                                         // (2)
    || followerName == "PTT-TGT-Drag-" + suffix;                                        // (3)
```
**CYC:** 3
**NT8 API:** No.
**Deferred:** No.
**Note:** `leaderName` parameter is reserved for future extension (e.g., determining STP vs TGT disambiguation) but is not needed for current logic — suffix+followerName is sufficient.

---

#### E-03 `LogHbcDiag`
**Signature:** `private void LogHbcDiag(Order leaderOrder, Order followerOrder, CopyRule rule, double price, string tag)`
**Line:** 8190
**Logic:** Diagnostic trace for HandleBracketChange execution.
```csharp
if (!_diagnosticMode) return;   // (1) B132 LaneB gate
NinjaTrader.Code.Output.Process(
    "[HBC-DIAG] " + (tag ?? string.Empty)
    + " leader=" + (leaderOrder?.Name ?? "null")
    + " fo=" + (followerOrder?.Name ?? "null")
    + " rule=" + (rule.Instrument ?? string.Empty)
    + " price=" + price.ToString("F2"),
    NinjaTrader.NinjaScript.PrintTo.OutputTab1
);
```
**CYC:** 2
**NT8 API:** Yes — calls `NinjaTrader.Code.Output.Process`.
**Deferred:** No.

---

#### E-04 `ExecuteStopDragOrder`
**Signature:** `private void ExecuteStopDragOrder(Account acc, Instrument instr, Order leaderOrder, double stopPrice, CopyRule rule)`
**Line:** 8194
**Logic:** Create/sync a PTT-STP-Drag stop order for the follower account (the core action of HandleBracketChange stop-drag path).
```csharp
if (leaderOrder == null || stopPrice <= 0) return;                     // (1)
int idx = DeriveLeaderBracketIndex(leaderOrder);
string suffix = idx > 0 ? idx.ToString() : "1";
var fo = FindFollowerBracketOrder(acc,
    leaderOrder.FromEntrySignalName, isStop: true, leaderOrder.Name);
if (fo != null)                                                         // (2)
    SyncAtmFollowerBracket(acc, fo, stopPrice, suffix, leaderOrder);
else
    CreateAndSubmitCollateralStop(acc, leaderOrder, stopPrice, suffix, leaderOrder);
```
**CYC:** 3
**NT8 API:** Yes — delegates to `SyncAtmFollowerBracket` (acc.Cancel + CreateOrder + Submit) or `CreateAndSubmitCollateralStop`.
**Deferred:** No.
**Note:** `CreateAndSubmitCollateralStop` signature: `(Account acc, Order fo, double newPrice, string suffix, Order leaderLeg)` — `leaderOrder` passed for both `fo` and `leaderLeg` in the no-existing-fo case. This is safe: `fo.Instrument` = `leaderOrder.Instrument`, `fo.OrderAction` = `leaderOrder.OrderAction`.

---

#### E-05 `IsOrderEventProcessable` (private static)
**Signature:** `private static bool IsOrderEventProcessable(NinjaTrader.Cbi.OrderEventArgs e)`
**Line:** 8197
**Logic:**
```csharp
return e != null
    && e.Order != null
    && e.Order.Instrument != null
    && e.Order.Account != null;
```
**CYC:** 4
**NT8 API:** No (field reads only on `OrderEventArgs`).
**Deferred:** No.

---

## Deferred Items

**None.** All 69 pre-existing stubs (excluding already-implemented `SelectBeRefPriceByDirection`) can be implemented using APIs available on AddOnBase. Additionally, 1 new method (A-14b `IsFollowerAccountMatch`) must be added by the engineer. No NT8 host is required for the implementations — NT8 objects are passed as parameters by the callers.

| ID | Method | Reason | Status |
|---|---|---|---|
| — | `SelectBeRefPriceByDirection` | Already implemented at L8005 | Skip |

---

## Ticket Breakdown

### T1 — Group A Predicates + BE Trigger Predicates
**File:** `src/PropTraderTools/CopyEngine.cs`
**Methods (14):**
- A-20 `IsPositionFlatOrMissing` (L7962) — static
- A-04 `IsNativeAtmTargetOrder` (L7898)
- A-05 `IsPttBeOrQxTargetOrder` (L7902)
- A-03 `IsEligibleBeTargetOrder` (L7894) — forward ref to C-05
- A-21 `IsLeaderTargetOrder` (L7966) — forward ref to C-01
- A-12 `IsReArmedAtmBracketCleanupRequired` (L7930)
- A-13 `FindMatchingNativeAtmBracket` (L7934)
- A-14 `TryFindRuleAndFollowerIndex` (L7938) — revised, calls A-14b
- **A-14b `IsFollowerAccountMatch` — NEW ADD** (insert after L7939, before L7941)
- A-15 `HasActiveQxOrdersForInstrument` (L7942)
- A-19 `HasInFlightFlattenOrder` (L7958)
- A-23 `IsLeaderAccountForInstrument` (L7974)
- A-01 `TryFireImmediateBeIfAlreadyAtLevel` (L7886)
- A-02 `IsPendingBeTriggerMet` (L7890)

**Engineer note for A-14b:** This method does NOT have a pre-existing stub. The engineer must insert a new `[ObfuscationAttribute(Feature="rename", Exclude=true)]` + `private static bool IsFollowerAccountMatch(...)` declaration with body immediately after the A-14 stub at L7939. All downstream line numbers in this file shift by ~4 lines after insertion.

**Tests covered:** `B79CancelRaceGuardTests` T1/T2/T4/T6/T7 existence checks + `BwaveCycT1R1BeHelperTests` T1/T2 existence checks.
**Prerequisite:** T4 must be committed before T1 is deployed to NT8 host (to resolve `HasValidTargetNameSuffix`/`IsBeTargetSnapshotState` forward references). However both T1 and T4 compile independently.

---

### T2 — Group A Actions
**File:** `src/PropTraderTools/CopyEngine.cs`
**Methods (11):**
- A-06 `RegisterBeRetryIfNoTargets` (L7906)
- A-07 `RegisterPartialTargetBeRetry` (L7910)
- A-08 `CancelExistingStpDragOrders` (L7914)
- A-09 `CancelExistingTgtDragOrders` (L7918)
- A-10 `SubmitReplacementStopLeg` (L7922)
- A-11 `SubmitReplacementTargetLeg` (L7926)
- A-16 `SyncAtmFollowerStopBracket` (L7946)
- A-17 `CancelStaleTgtDragOrders` (L7950)
- A-18 `CreateAndSubmitReplacementTarget` (L7954)
- A-22 `ResubmitFollowerEntry` (L7970)
- A-24 `CancelStaleCascadeTgtDrag` (L7978)

**Tests covered:** `B79CancelRaceGuardTests` T3/T5/T7 existence checks.
**Prerequisite:** None (all delegate to existing production methods).

---

### T3 — Group B BE Arming Helpers
**File:** `src/PropTraderTools/CopyEngine.cs`
**Methods (11):**
- B-01 `GetMarketBidPrice` (L7990)
- B-02 `GetMarketAskPrice` (L7994)
- B-03 `GetBeTickSize` (L7998)
- B-05 `FireBeAndNotifyEvent` (L8011)
- B-06 `ShouldFireBeImmediately` (L8015)
- B-07 `CompleteBeArming` (L8019)
- B-08 `TryClaimPendingBeSlot` (L8023)
- B-09 `GetSlotInstrumentName` (L8027)
- B-10 `GetSlotAccountName` (L8031)
- B-11 `RaisePendingBeFiredEvent` (L8035)
- B-12 `SettleAndFirePendingBe` (L8039)

**Tests covered:** `BwaveCycT1R1BeHelperTests` all existence checks.
**Prerequisite:** None.

---

### T4 — Group C + Group D Predicates
**File:** `src/PropTraderTools/CopyEngine.cs`
**Methods (20):**
*Group C (5):*
- C-01 `HasValidTargetNameSuffix` (L8049)
- C-02 `SelectBeTargetList` (L8053)
- C-03 `IsBeTargetActiveState` (L8057)
- C-04 `IsBeTargetPendingChangeState` (L8061)
- C-05 `IsBeTargetSnapshotState` (L8065)

*Group D predicates (15):*
- D-04 `IsPttTgtDragOrder` (L8088)
- D-05 `IsAtmTgtOrder` (L8092)
- D-06 `IsBePendingTargetOrder` (L8096)
- D-07 `IsPttBeStopRejected` (L8100)
- D-08 `IsPttDragOrderCancellable` (L8104)
- D-09 `IsPttQxTargetOrder` (L8108)
- D-10 `IsNativeAtmBeRetryTarget` (L8112)
- D-11 `IsBeRetryEligibleOrderState` (L8116)
- D-12 `IsBeRetryOrderInvalid` (L8120)
- D-13 `IsBeSlotNonTerminal` (L8124)
- D-14 `IsBeFilledWithOpenPosition` (L8128)
- D-15 `IsPttDragOrderName` (L8132)
- D-16 `IsDragInstrumentMatch` (L8136)
- D-17 `IsQxTOrderStateValid` (L8140)
- D-18 `IsQxTBracketNameValid` (L8144)

**Tests covered:** `BwaveCycTaR2HelperTests` + `BwaveCycTaR3HelperTests` (TA-R4) existence checks.
**Prerequisite:** None.

---

### T5 — Group D Actions + Group E
**File:** `src/PropTraderTools/CopyEngine.cs`
**Methods (14):**
*Group D actions (9):*
- D-01 `TrySyncAtmBrackets` (L8076)
- D-02 `TrySkipTrailingStop` (L8080)
- D-03 `SyncStandardBracket` (L8084)
- D-19 `TryGetCleanupEntryForFollower` (L8148)
- D-20 `IsCleanupEntryCurrentAndMatching` (L8152)
- D-21 `SendAtmCancelReplace` (L8156)
- D-22 `TryMatchFollowerInRule` (L8160)
- D-23 `IsBeReplaceTargetValid` (L8164)
- D-24 `TryIncrementBeReplaceAttempt` (L8168)

*Group E (5):*
- E-01 `IsBracketOrderLiveState` (L8182)
- E-02 `MatchesPttReplacementName` (L8186)
- E-03 `LogHbcDiag` (L8190)
- E-04 `ExecuteStopDragOrder` (L8194)
- E-05 `IsOrderEventProcessable` (L8197)

**Tests covered:** `BwaveCycTaR3HelperTests` (TA-R3/R5) + `BwaveCycTaR6HelperTests` existence checks.
**Prerequisite:** None.

---

## 7-Scan Checklist Template (per ticket)

Each ticket must pass all 7 scans before engineer marks complete:

| Scan | Check | Applies To |
|---|---|---|
| **SCAN-01** | `grep -r "lock(" src/` returns zero matches in changed file | All tickets |
| **SCAN-02** | No `DateTime.Now` in changed methods; only `DateTime.UtcNow` | T1 (A-12), T5 (D-20) |
| **SCAN-03** | No `throw` statement in any stub body; all NT8 API calls in `try/catch` | T1 (A-01), T2 (A-08..A-11,A-16..A-18,A-22,A-24), T3 (B-05,B-12), T4 (C-02), T5 (D-01..D-03,D-21,E-03,E-04) |
| **SCAN-04** | All new string literals are ASCII-only (no Unicode, no curly quotes) | All tickets |
| **SCAN-05** | All `acc.CreateOrder` calls use `"PTT-"` prefixed order name | T2 (A-10,A-11,A-18,A-22), T3 (B-05), T5 (E-04) |
| **SCAN-06** | Max McCabe CYC ≤ 8 per method (manual count per `CYC_METHODOLOGY.md`) | All tickets |
| **SCAN-07** | `dotnet test` from repo root reports `Failed: 0` after ticket | All tickets |

---

## Component Summary

| Component | Class | File | Role |
|---|---|---|---|
| 69 stub implementations + 1 new method (A-14b) | `CopyEngine` | `src/PropTraderTools/CopyEngine.cs` | Fill 69 stub bodies + ADD IsFollowerAccountMatch |
| Existing production methods used | `CopyEngine` | same file | `FindPosition`, `IsFlat`, `RegisterBeRetrySlotIfNeeded`, `SyncAtmFollowerBracket`, `SyncAtmFollowerTarget`, `CreateAndSubmitCollateralStop`, `FindFollowerBracketOrder`, `DeriveLeaderBracketIndex`, `IsCleanupQxOrderOk`, `IsPttStpDragCancellable`, `ExtractLegSuffix`, `FindFollowerSlotIndex`, `BreakEven`, `MoveStopToBreakEven`, `SubmitBeStop`, `SelectBeRefPriceByDirection` |
| Test file | `CopyEngineTests.cs` | same dir | **READ-ONLY. DO NOT MODIFY.** All tests are `[Fact]` existence checks (reflection) or NT8-runtime-skipped. |

---

## NT8 API Usage Summary

| API | Source | Stubs That Use It |
|---|---|---|
| `Account.Cancel(Order[])` | AddOnBase | A-08, A-09, A-13*, A-17, A-24, D-21 |
| `Account.CreateOrder(...)` | AddOnBase | A-10, A-11, A-18, A-22, B-05* |
| `Account.Submit(Order[])` | AddOnBase | A-10, A-11, A-18, A-22 |
| `Account.Orders` | AddOnBase | A-13, A-15, A-19, C-02 |
| `Account.Positions` (via FindPosition) | AddOnBase | A-01*, A-02, B-12, D-14 |
| `Instrument.MarketData.Bid/Ask.Price` | AddOnBase | B-01, B-02 |
| `Instrument.MasterInstrument.TickSize` | AddOnBase | B-03 |

*via delegate call to existing method

**NOT used (StrategyBase-only):**
- `AtmStrategyChangeStopTarget()` — StrategyBase ONLY
- `AtmStrategyCreate()` — StrategyBase ONLY

---

## Threading Model

All 69 stubs + A-14b are **synchronous helper methods** with no `async` / `await`. They may be called from:
- NT8 order-update background thread (`OnOrderUpdate` path)
- NT8 account-update background thread (`OnPendingBeAccountUpdate` path)
- WPF UI thread (panel button handlers)

**No `Dispatcher.InvokeAsync` is needed** in any stub. Stubs that fire events (`B-11 RaisePendingBeFiredEvent`, `B-07 CompleteBeArming`) invoke `PendingBeFired` / `PendingBeArmed` directly — the panel subscribers already marshal to UI thread internally (existing pattern confirmed at L409-412).

**Lock-free state access:** All `_pendingBeSlots`, `_pendingFollowerBeSlots`, `_beReplaceAttempts`, `_filledBeTargetCount`, `_qxPendingFollowerCleanup` accesses use `ConcurrentDictionary.TryGetValue` / `TryRemove` / `TryAdd` / indexer — all lock-free per JS-021.

---

## Hard-Link Sync Requirement

After every `src/` edit: `powershell -File .\deploy-sync.ps1` must be run to re-synchronize NinjaTrader hard links. This applies after each ticket is committed.
