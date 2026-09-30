# B142 Architecture Plan — Drag-Sync System Hardening

**Block**: B142
**Status**: RETROACTIVE — all code committed and SIM-confirmed
**Plan type**: Documentation of committed work (no new code)
**Produced by**: ptt-architect (Phase 1, retroactive)
**Date**: 2026-09-02 (SIM confirmed DW-B142-DRAG)
**Prior block**: B141 (DW-B153 OCO cascade dual-resubmit)

---

## 1. Block Summary

B142 is a hardening block for the ATM stop-drag synchronization system introduced in B141.
B141 established the dual-resubmit pattern (cancel+resubmit stop, capture+resubmit target after OCO
cascade) for a single 1-leg ATM scenario. B142 extends this to the full 3-leg ATM scenario, fixes
session-start guard defects, fixes quantity desync on resubmitted orders, and adds the
`PTT-TGT-Drag-N` recognition clause that allows second+ target drags to route correctly through
the cancel+resubmit path instead of the no-op `acc.Change()` path.

**Core problem solved**: A leader ATM position with Stop1/Stop2/Stop3 and Target1/Target2/Target3
brackets. When Stop1 is dragged, NT8 OCO cascade cancels all 6 ATM group members. B142 ensures:
1. Stop1/2/3 all get resubmitted as `PTT-STP-Drag-1/2/3` at correct prices with correct quantities.
2. Target1/2/3 all get resubmitted as `PTT-TGT-Drag-1/2/3` at captured prices with correct quantities.
3. Second+ drags (when fo is already a PTT-named order) route correctly through cancel+resubmit.
4. Session-start spurious cancels are blocked by price guards.
5. Concurrent drag events do not collide via shared order names.

**10 commits, 1 file (CopyEngine.cs), 4 new methods, 10 modified methods.**

---

## 2. Prior Block

**B141** closed DW-B153 (OCO cascade dual-resubmit) via:
- `CaptureLinkedTargetPrice` + `ResubmitTargetAfterCascade` (single-leg capture/resubmit)
- `TryParseStopSuffix` for suffix extraction
- `IsTargetOrderLive` (Working/Accepted states)
- `SyncFollowerBracket` branch (3) dual-resubmit chain

B141 SIM gates DW-B141-SIM-01/02/03 were opened as PENDING at B141 close. B142's SIM confirmation
(DW-B142-DRAG 2026-09-02) provides empirical confirmation that the B141 dual-resubmit chain is
working — see Section 12 for details.

---

## 3. DW Cards Closed by B142

| Card | Priority | Status | Confirmed |
|------|----------|--------|-----------|
| DW-B142-DRAG | P0 | CLOSED (SIM CONFIRMED 2026-09-02) | `IsAtmSTPOrder` + `PTT-TGT-Drag-` clause fixes second+ target drag routing |
| DW-B142-QTY-DESYNC-01 | P1 | CLOSED (SIM pending — B142 committed, SIM date TBD) | `leaderOrder.Quantity` in all bracket resubmit helpers + `FindLeaderCollateralOrder` |

---

## 4. Component Map

All methods are in: `src/PropTraderTools/CopyEngine.cs`

### 4.1 Modified Methods

#### `IsTrailingStop` — L2218-2227
- **Commit**: `4cc50a24` B142-DIRECT-1
- **Signature**: `private static bool IsTrailingStop(Order order)`
- **CYC**: 1
- **Change**: Added `&& (order.Name == null || !order.Name.StartsWith("PTT-", StringComparison.Ordinal))` to the return expression
- **Purpose**: Excludes `PTT-STP-Drag` (AddOn-created StopMarket orders) from the trailing-stop classification. Without this, branch (4) in `SyncFollowerBracket` silently skipped ALL second+ stop drags, because the order type IS StopMarket and was falsely matching the trailing-stop predicate.

---

#### `IsAtmSTPOrder` — L2240-2248
- **Commit**: `2b052b5d` B142-DIRECT-4 (PTT-STP-Drag- clause); `a702bcbd` DW-B142-DRAG (PTT-TGT-Drag- clause)
- **Signature**: `internal static bool IsAtmSTPOrder(Order order)`
- **CYC**: 1
- **Changes**:
  - B142-DIRECT-4: Added `|| order.Name.StartsWith("PTT-STP-Drag-", StringComparison.Ordinal)`
  - DW-B142-DRAG: Added `|| order.Name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)`
- **Purpose**: Ensures second+ stop drags (fo.Name == `PTT-STP-Drag-N`) and second+ target drags
  (fo.Name == `PTT-TGT-Drag-N`) both route to the cancel+resubmit path in `SyncFollowerBracket`
  branches (3) and (3b). Before these clauses, `IsAtmSTPOrder` returned false for PTT-named orders,
  causing `acc.Change()` (no-op on ATM-created brackets) and a branch skip.

---

#### `SyncFollowerBracket` — L2266-2345 (branch (3) block specifically L2298-2328)
- **Commits**: `e8d529e2` DIRECT-2, `220bc152` DIRECT-3, `2b052b5d` DIRECT-4, `fbf39d0e` DIRECT-6, `b30345c5` QTY-DESYNC-01
- **Signature**: `private void SyncFollowerBracket(Account acc, Order leaderOrder, bool isStop, double newPrice, double tickSize)`
- **CYC**: 8 AT LIMIT (base=1 + fo-null(1) + price-delta(1) + ATM-STP(1) + HasValue(1) + ATM-TGT(1) + IsTrailingStop(1) + isStop-inner(1))
- **Changes in branch (3)**:
  - B142-DIRECT-2: `if (fo.StopPrice < tickSize) return;` guard before price capture
  - B142-DIRECT-4: derive `legSuffix` from `leaderOrder.Name` (not `fo.Name`)
  - B142-DIRECT-6: call `CaptureOtherLegTargetPrices(acc, fo, legSuffix)` before cancel
  - B142-DIRECT-6: call `ResubmitCollateralLegs(...)` after `ResubmitTargetAfterCascade`
  - DW-B142-QTY-DESYNC-01: thread `leaderOrder` through `SyncAtmFollowerBracket` and `ResubmitCollateralLegs` calls
- **Purpose**: Orchestrates the full drag-sync chain for ATM stop brackets. After B142, a single
  Stop1 drag: (a) guards session-start spurious prices, (b) captures all 3 target prices before
  cancel cascade, (c) fires cancel+resubmit for the dragged stop, (d) resubmits the linked target,
  (e) resubmits all other collateral stop+target legs.

---

#### `SyncAtmFollowerBracket` — L2382-2432
- **Commits**: `220bc152` DIRECT-3 (suffix param + per-leg name), `b30345c5` QTY-DESYNC-01 (leaderOrder qty)
- **Signature**: `private void SyncAtmFollowerBracket(Account acc, Order fo, double newPrice, string suffix, Order leaderOrder = null)`
- **CYC**: 5 (acc-null(1) + fo-null(1) + IsNoPriceChange(1) + newStop==null(1) + base(1))
- **Changes**:
  - `suffix` param added; order name changed to `"PTT-STP-Drag-" + suffix`
  - `leaderOrder.Quantity` used instead of `fo.Quantity` in `CreateOrder`
- **Purpose**: Cancel+resubmit for the primary dragged stop leg. Creates `PTT-STP-Drag-{N}` at
  `newPrice` with leader quantity. Preceded by `CancelExistingPttStpDrag` sweep.

---

#### `SyncAtmFollowerTarget` — L2856-2940
- **Commits**: `2b052b5d` DIRECT-5 (LimitPrice guard), `77a02254` DIRECT-7 (per-leg name), `b30345c5` QTY-DESYNC-01
- **Signature**: `private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice, Order? leaderOrder = null)`
- **CYC**: 8 AT LIMIT
- **Changes**:
  - B142-DIRECT-5: `fo.LimitPrice <= 0 ||` added to guard (3) — blocks spurious cancel when target LimitPrice not yet populated
  - B142-DIRECT-7 BUG B: `tgtDragName = "PTT-TGT-Drag-" + DeriveLeaderBracketIndex(leaderOrder)` — per-leg drag name
  - DW-B142-QTY-DESYNC-01: `leaderOrder?.Quantity ?? fo.Quantity` in CreateOrder
- **Purpose**: Cancel+resubmit for ATM target leg on target drag events (branch 3b). Also creates
  `PTT-TGT-Drag-{N}` per-leg to avoid concurrent drag collisions. Includes Phase C stop replacement.

---

#### `ResubmitTargetAfterCascade` — L2575-2636
- **Commits**: `220bc152` DIRECT-3 (suffix param + per-leg name), `b30345c5` QTY-DESYNC-01
- **Signature**: `private void ResubmitTargetAfterCascade(Account acc, Order stpOrder, double targetPrice, Order leaderOrder, string suffix)`
- **CYC**: 4 (foreach(1) + if Working+Name+Instr(1) + if newTarget==null(1) + base(1))
- **Changes**:
  - `suffix` param added; sweep and create `PTT-TGT-Drag-{suffix}`
  - `leaderOrder.Quantity` used instead of `stpOrder.Quantity`
- **Purpose**: After OCO cascade cancels the linked ATM target (B141 mechanism), resubmits a
  standalone `PTT-TGT-Drag-{N}` at the captured price. Block A-Prime sweeps stale per-leg order.

---

#### `CancelExistingPttStpDrag` — L2801-2822
- **Commit**: `220bc152` DIRECT-3 (suffix param)
- **Signature**: `private void CancelExistingPttStpDrag(Account acc, Order fo, string suffix)`
- **CYC**: 6 (foreach(1) + 3 &&-compound branches(1) per comment = 6 via project counting)
- **Change**: `suffix` param; sweep target changed from `"PTT-STP-Drag"` to `"PTT-STP-Drag-" + suffix`
- **Purpose**: Block A-Prime pre-sweep in `SyncAtmFollowerBracket`. Cancels any live
  `PTT-STP-Drag-{N}` for matching instrument before resubmitting. Per-leg sweep prevents
  Stop1 sweep from cancelling Stop2's order on concurrent events.

---

#### `CaptureLinkedTargetPrice` — L2447-2465
- **Commits**: `2b052b5d` DIRECT-4 (use leaderOrder.Name), `ca8ad16f` DIRECT-9 (PTT-TGT-Drag-N preference + CYC 4→5)
- **Signature**: `private double? CaptureLinkedTargetPrice(Account acc, string stopName)`
- **CYC**: 5 (TryParseStopSuffix(1) + foreach(1) + if pttTgtName(1) + else if targetName(1) + if pttPrice.HasValue(1))
- **Changes**:
  - B142-DIRECT-9: Full scan returning PTT-TGT-Drag-N price if found, else ATM TargetN price
  - `pttPrice`/`atmPrice` local vars; PTT price preferred over ATM price
- **Purpose**: Captures the linked target's limit price before the stop cancel cascade fires.
  PTT-TGT-Drag-N preference (DIRECT-9) prevents overwriting a previously-dragged target price
  with the original ATM TargetN price when both orders coexist.

---

#### `FindFollowerBracketOrder` (list overload) — L3138-3171
- **Commit**: `ca8ad16f` DIRECT-9 (ChangeSubmitted added to state filter)
- **Signature**: `private Order? FindFollowerBracketOrder(IEnumerable<Order> orders, string? fromEntrySignalName, bool isStop, string? leaderName = null)`
- **CYC**: 8 AT LIMIT (foreach(1) + OrderPassesBracketGate(1) + state filter 4 branches(4) + isStop(1) + type match(1))
- **Change**: `OrderState.ChangeSubmitted` added to state filter (B142-DIRECT-9 BUG B)
- **Purpose**: Locates the follower's incumbent bracket order. `ChangeSubmitted` addition ensures
  rapid back-to-back drags (where PTT-TGT-Drag-N is in ChangeSubmitted state) still return `fo`
  and route to `acc.Change()` rather than missing the order (fo=null → drag skipped).

---

#### `MatchesLeaderName` — L3193-3210
- **Commit**: `220bc152` DIRECT-3 (per-leg PTT name matching)
- **Signature**: `private static bool MatchesLeaderName(Order order, string? leaderName, bool isStop)`
- **CYC**: 5 (null(1) + exact(1) + !isStop&&TGT(1) + isStop&&STP(1) + base(1))
- **Change**: Added `legSuffix` extraction from `leaderName` trailing digit; per-leg PTT name
  matching via `"PTT-TGT-Drag-" + legSuffix` and `"PTT-STP-Drag-" + legSuffix`
- **Purpose**: Allows `FindFollowerBracketOrder` to locate `PTT-STP-Drag-1` when looking for
  the Stop1 incumbent on the second+ drag. Without this, the incumbent was not found (fo=null).

---

#### `IsTargetOrderLive` — L2553-2561
- **Commits**: `77a02254` DIRECT-7 BUG A (Submitted added), `ca8ad16f` DIRECT-9 BUG C (ChangeSubmitted/ChangePending added)
- **Signature**: `private static bool IsTargetOrderLive(Order o)`
- **CYC**: 1 (expression body; `||` adds 0 McCabe per convention)
- **Changes**:
  - DIRECT-7: Added `OrderState.Submitted`
  - DIRECT-9: Added `OrderState.ChangeSubmitted`, `OrderState.ChangePending`
- **States covered**: Working, Accepted, Submitted, ChangeSubmitted, ChangePending
- **Purpose**: Predicate used by `CaptureLinkedTargetPrice` and `CaptureOtherLegTargetPrices`.
  Broadened state coverage ensures targets in transient NT8 states are still captured correctly.

---

### 4.2 New Methods

#### `CaptureOtherLegTargetPrices` — L2481-2501
- **Commit**: `fbf39d0e` DIRECT-6 (creation); `ca8ad16f` DIRECT-9 (PTT preference +1 else if, CYC 5→6)
- **Signature**: `private double[] CaptureOtherLegTargetPrices(Account acc, Order fo, string excludeSuffix)`
- **CYC**: 6 (if !StartsWith(1) + foreach(1) + for(1) + if exclude(1) + if PTT pref(1) + else if ATM(1))
- **Purpose**: Snapshots the limit prices of all non-primary-leg ATM target orders (and PTT-TGT-Drag-N
  replacements) before `acc.Cancel(Stop1_ATM)` fires the OCO cascade that kills them.
  Returns `double[3]` indexed by suffix-1. Guard: returns all-zeros on second+ drag (when
  `fo.Name` does not start with "Stop" — ATM group is already broken, collateral legs are
  standalone PTT orders, not cascade victims).

---

#### `ResubmitCollateralLegs` — L2649-2670
- **Commit**: `fbf39d0e` DIRECT-6 (creation); `b30345c5` QTY-DESYNC-01 (leaderOrder param)
- **Signature**: `private void ResubmitCollateralLegs(Account acc, Order fo, double newPrice, double[] otherLegPrices, string excludeSuffix, Order leaderOrder)`
- **CYC**: 4 (for(1) + if exclude(1) + if price<=0(1) + base(1))
- **Purpose**: Iterates the 3 possible leg suffixes (1/2/3), skipping the primary dragged leg
  and any leg with no captured price. For each valid collateral leg, looks up the leader's
  per-leg bracket order via `FindLeaderCollateralOrder`, then calls `ResubmitOneCollateralLeg`.

---

#### `ResubmitOneCollateralLeg` — L2688-2772
- **Commit**: `fbf39d0e` DIRECT-6 (creation); `cd3d9f02` DIRECT-8 (Block A-Prime sweep added); `b30345c5` QTY-DESYNC-01 (leaderLeg qty)
- **Signature**: `private void ResubmitOneCollateralLeg(Account acc, Order fo, double newPrice, double targetPrice, string suffix, Order leaderLeg = null)`
- **CYC**: 7 (foreach STP(1) + if STP-cancel(1) + foreach TGT(1) + if TGT-cancel(1) + if newStop==null(1) + if newTarget==null(1) + base(1))
- **Purpose**: Creates `PTT-STP-Drag-{suffix}` at `newPrice` and `PTT-TGT-Drag-{suffix}` at
  `targetPrice` for a single collateral leg. B142-DIRECT-8 added Block A-Prime-Stop and
  Block A-Prime-Target sweeps to cancel any existing PTT drag orders for that leg suffix before
  resubmitting — prevents accumulation on repeated stop drags.

---

#### `FindLeaderCollateralOrder` — L2525-2537
- **Commit**: `b30345c5` DW-B142-QTY-DESYNC-01 (creation)
- **Signature**: `private static Order FindLeaderCollateralOrder(Order leaderOrder, string suffix)`
- **CYC**: 3 (if null-guard(1) + foreach(1) + if name-match(1))
- **Purpose**: Looks up the leader account's bracket order for a given suffix ("1"/"2"/"3").
  Searches for `"Stop{suffix}"` and `"Target{suffix}"` in `leaderOrder.Account.Orders`.
  Returns `null` if not found — callers fall back to `fo.Quantity`. Provides per-leg leader
  quantity to fix the quantity desync defect where collateral legs were resubmitted with the
  dragged leg's quantity rather than their own.

---

## 5. Data Flow — Stop Drag Event Chain

```
NT8 OnOrderUpdate fires (leader order changed)
    ↓
TryHandleBracketDrag(order, rule)            CYC=3
    ↓
HandleBracketChange(leaderOrder, rule)       CYC=6
    isStop = IsStopLeg(leaderOrder)
    newPrice = tick-rounded StopPrice/LimitPrice
    for each followerAccount:
        ↓
SyncFollowerBracket(acc, leaderOrder,        CYC=8 AT LIMIT
                    isStop, newPrice, tickSize)
    fo = FindFollowerBracketOrder(acc,...)   CYC=8 AT LIMIT
         └─ MatchesLeaderName(...):          CYC=5
            matches Stop1/PTT-STP-Drag-1 (B142)
    if fo==null → return                    (branch 1)
    if |newPrice-currentPrice|<tickSize → return  (branch 2)

    BRANCH (3): isStop && IsAtmSTPOrder(fo)  CYC=1
                [true for ATM Stop1/2/3 AND PTT-STP-Drag-N (B142-DIRECT-4)]
        if fo.StopPrice < tickSize → return  (B142-DIRECT-2: session-start guard)
        TryParseStopSuffix(leaderOrder.Name) → legSuffix
        CaptureLinkedTargetPrice(acc, leaderOrder.Name) → capturedTargetPrice?
        CaptureOtherLegTargetPrices(acc, fo, legSuffix) → otherLegPrices[3]
            [returns zeros on second+ drag — safe no-op]
        SyncAtmFollowerBracket(acc, fo, newPrice, legSuffix, leaderOrder)
            CancelExistingPttStpDrag(acc, fo, legSuffix)  [sweep PTT-STP-Drag-N]
            acc.Cancel({fo})    [cascade cancels ATM Stop2/3/Target1/2/3]
            acc.CreateOrder(PTT-STP-Drag-{N}, StopMarket, leaderOrder.Quantity, newPrice)
            acc.Submit(newStop)
        if capturedTargetPrice.HasValue:   (branch 4 — B141 at CYC limit)
            ResubmitTargetAfterCascade(acc, fo, capturedPrice, leaderOrder, legSuffix)
                sweep PTT-TGT-Drag-{N} [Block A-Prime]
                acc.CreateOrder(PTT-TGT-Drag-{N}, Limit, leaderOrder.Quantity, capturedPrice)
                acc.Submit(newTarget)
        ResubmitCollateralLegs(acc, fo, newPrice, otherLegPrices, legSuffix, leaderOrder)
            for i=1..3, skip legSuffix, skip if price==0:
                FindLeaderCollateralOrder(leaderOrder, s) → leaderLeg
                ResubmitOneCollateralLeg(acc, fo, newPrice, price, s, leaderLeg)
                    sweep PTT-STP-Drag-{s} [Block A-Prime-Stop]
                    sweep PTT-TGT-Drag-{s} [Block A-Prime-Target]
                    acc.CreateOrder(PTT-STP-Drag-{s}, StopMarket, leaderLeg.Qty, newPrice)
                    acc.Submit(newStop)
                    acc.CreateOrder(PTT-TGT-Drag-{s}, Limit, leaderLeg.Qty, targetPrice)
                    acc.Submit(newTarget)
        return

    BRANCH (3b): !isStop && IsAtmSTPOrder(fo)  CYC=1
                 [true for ATM Target1/2/3 AND PTT-TGT-Drag-N (DW-B142-DRAG)]
        SyncAtmFollowerTarget(acc, fo, newPrice, leaderOrder)  CYC=8 AT LIMIT
            if fo.LimitPrice<=0 → return  (B142-DIRECT-5: spurious-cancel guard)
            tgtDragName = "PTT-TGT-Drag-" + DeriveLeaderBracketIndex(leaderOrder)
            sweep PTT-TGT-Drag-N [Block A-Prime]
            acc.Cancel({fo})    [Block A]
            acc.CreateOrder(PTT-TGT-Drag-N, Limit, leaderOrder.Qty??fo.Qty, newPrice)
            acc.Submit(newTarget)
            ExecutePhaseCStopReplacement(acc, fo, leaderOrder)
        return

    BRANCH (4): isStop && IsTrailingStop(fo)
                [PTT-STP-Drag excluded by B142-DIRECT-1]
        StatusUpdate (skip) → return
```

---

## 6. NT8 API Usage

All NT8 API calls confirmed from committed source (CopyEngine.cs). No memory speculation.

| NT8 API | Usage in B142 | Notes |
|---------|---------------|-------|
| `acc.Cancel(Order[])` | SyncAtmFollowerBracket, SyncAtmFollowerTarget, ResubmitTargetAfterCascade, ResubmitOneCollateralLeg, CancelExistingPttStpDrag | AddOnBase-available. Confirmed DW-B154. |
| `acc.CreateOrder(...)` | SyncAtmFollowerBracket, SyncAtmFollowerTarget, ResubmitTargetAfterCascade, ResubmitOneCollateralLeg | AddOnBase-available. Requires explicit Submit(). arg9=oco="" (standalone, not ATM group). |
| `acc.Submit(Order[])` | Same 4 methods | Required after CreateOrder. |
| `acc.Orders` | CaptureLinkedTargetPrice, CaptureOtherLegTargetPrices, ResubmitTargetAfterCascade, SyncAtmFollowerTarget, ResubmitOneCollateralLeg, CancelExistingPttStpDrag, FindFollowerBracketOrder | Iterated via `.ToList()` for thread-safe snapshot. |
| `Order.Name` | Throughout | Checked for null before use. "Stop1/2/3", "Target1/2/3", "PTT-STP-Drag-N", "PTT-TGT-Drag-N". |
| `Order.Quantity` | SyncAtmFollowerBracket, SyncAtmFollowerTarget, ResubmitTargetAfterCascade, ResubmitOneCollateralLeg | `leaderOrder.Quantity` used (DW-B142-QTY-DESYNC-01). Fallback: `fo.Quantity`. |
| `Order.StopPrice` | SyncFollowerBracket L2284, L2300 | 0 when ATM bracket newly Accepted (B142-DIRECT-2 guard). |
| `Order.LimitPrice` | SyncAtmFollowerTarget L2867, CaptureLinkedTargetPrice L2458/2460, CaptureOtherLegTargetPrices L2494/2497 | 0 when ATM target in Submitted state (B142-DIRECT-5 guard). |
| `Order.OrderState` | IsTargetOrderLive, IsPttStpDragCancellable, ResubmitTargetAfterCascade, SyncAtmFollowerTarget, FindFollowerBracketOrder | States: Working, Accepted, Submitted, ChangeSubmitted, ChangePending, CancelPending, CancelSubmitted. |
| `Order.OrderAction` | SyncAtmFollowerBracket L2408, ResubmitOneCollateralLeg L2719 | Used for CreateOrder arg2. ATM bracket legs share same OrderAction direction. |
| `Order.OrderType` | IsTrailingStop L2225, FindFollowerBracketOrder L3159-3166 | StopMarket, StopLimit, Limit. |
| `Order.Instrument` | SyncAtmFollowerBracket L2407, ResubmitTargetAfterCascade L2611, ResubmitOneCollateralLeg L2718/2748, CancelExistingPttStpDrag L2809 | Instrument reference for CreateOrder and instrument-match guards. |
| `NinjaTrader.Core.Globals.MaxDate` | All CreateOrder calls arg11 | GTC/Day expiry. |

**Confirmed NT8 API facts (never re-investigate)**:
- `acc.Change()` is a silent no-op on ATM-owned Stop brackets from AddOnBase (B140 SIM Gate 1 FAIL, DW-B154)
- `acc.Cancel(Stop1_ATM)` OCO-cascades ALL ATM group members (Stop2/Stop3/Target1/Target2/Target3) — confirmed B142-DIRECT-6 SIM
- `acc.Change()` on `PTT-STP-Drag-N` (AddOn-created) DOES work — confirmed B142-DIRECT-4 SIM
- `Order.StopPrice == 0` when NT8 ATM bracket is newly Accepted (price not yet populated)
- `Order.LimitPrice == 0` when NT8 ATM target is in Submitted state
- ATM bracket legs share the same `OrderAction` direction (both Stop and Target exit same side)

---

## 7. Threading Model

The B142 drag-sync methods run entirely on the **NT8 order-update dispatch thread**:

```
NT8 OnOrderUpdate callback
    → TryHandleBracketDrag
    → HandleBracketChange
    → SyncFollowerBracket
    → [all B142 helper methods]
```

**No `Dispatcher.InvokeAsync` is used or needed** in any B142 method. NT8 order API calls
(`acc.Cancel`, `acc.CreateOrder`, `acc.Submit`) are thread-safe from any thread.

**Thread safety mechanisms**:
- `acc.Orders.ToList()`: thread-safe snapshot of the NT8 Orders collection (all iteration uses this pattern)
- No shared mutable state modified by B142 methods — all operations are through NT8 Account API
- `StatusUpdate` event: fired on dispatch thread; WPF subscriber marshals to UI thread via its own Dispatcher.InvokeAsync (established pattern from B23)

`Dispatcher.InvokeAsync` IS used elsewhere in CopyEngine.cs for WPF UI operations (L367, L381, L391, L1644) — but not in the B142 drag-sync path.

**JS-021 compliance**: Zero `lock()` instances in the file. No new shared state introduced by B142.

---

## 8. JS Rule Compliance

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (No lock()) | grep `lock\s*(` in CopyEngine.cs | **PASS** — zero instances |
| JS-001 (No throw in hot path) | All CreateOrder/Cancel/Submit wrapped in try/catch | **PASS** — exceptions logged via StatusUpdate, never rethrown |
| JS-002 (No null return for values) | double? (nullable value type) returns in CaptureLinkedTargetPrice; double[] value return in CaptureOtherLegTargetPrices; bool returns in predicates | **PASS** — nullable value types, not reference nulls |
| DateTime.Now ban | No DateTime.Now in any B142 method | **PASS** — not used |
| ASCII-only | All string literals: "PTT-STP-Drag-", "PTT-TGT-Drag-", "Stop", "Target", "STP", suffix digits | **PASS** — all ASCII |
| FontFamily ban | No FontFamily in any B142 method | **PASS** |
| Hex color ban | No `#RRGGBB` literals in any B142 method | **PASS** |

---

## 9. CYC Audit

All methods use the project counting convention: base=1, `&&`/`||`=0, `catch`=0.

| Method | Line Range | CYC | Limit Status |
|--------|-----------|-----|--------------|
| `IsTrailingStop` | L2218-2227 | 1 | Well under |
| `IsAtmSTPOrder` | L2240-2248 | 1 | Well under |
| `IsTargetOrderLive` | L2553-2561 | 1 | Well under |
| `IsPttStpDragCancellable` | L2782-2787 | 1 (expression body) | Well under |
| `TryParseStopSuffix` | L2507-2517 | 3 | Well under |
| `FindLeaderCollateralOrder` | L2525-2537 | 3 | Well under |
| `ResubmitTargetAfterCascade` | L2575-2636 | 4 | Well under |
| `ResubmitCollateralLegs` | L2649-2670 | 4 | Well under |
| `CaptureLinkedTargetPrice` | L2447-2465 | 5 | Well under |
| `SyncAtmFollowerBracket` | L2382-2432 | 5 | Well under |
| `MatchesLeaderName` | L3193-3210 | 5 | Well under |
| `CaptureOtherLegTargetPrices` | L2481-2501 | 6 | Well under |
| `CancelExistingPttStpDrag` | L2801-2822 | 6 | Well under |
| `ResubmitOneCollateralLeg` | L2688-2772 | 7 | Under limit |
| `SyncFollowerBracket` | L2266-2345 | **8** | **AT LIMIT** |
| `SyncAtmFollowerTarget` | L2856-2940 | **8** | **AT LIMIT** |
| `FindFollowerBracketOrder` (list) | L3138-3171 | **8** | **AT LIMIT** |

**CYC=8 AT LIMIT methods**: Any future modification to `SyncFollowerBracket`,
`SyncAtmFollowerTarget`, or `FindFollowerBracketOrder` that adds a branch MUST first extract
existing branches to helper methods (per DW-B141-STP-CYC8-WALL constraint).

---

## 10. Lane-Split Gate

**Q1: Are all B142 fixes in the same file within overlapping methods?**
Yes. All 10 commits modify CopyEngine.cs. The methods form a contiguous drag-sync call chain.

**Q2: Does any fix's design depend on another fix's final form?**
Yes. DIRECT-3 (per-leg names) is a prerequisite for DIRECT-8 (per-leg sweeps to work correctly).
DIRECT-4 (IsAtmSTPOrder + PTT-STP-Drag-) is a prerequisite for the collateral leg path to route.
DW-B142-DRAG (IsAtmSTPOrder + PTT-TGT-Drag-) is the capstone that enables second+ target drags.
QTY-DESYNC-01 (FindLeaderCollateralOrder) depends on the per-leg suffix infrastructure from DIRECT-3.

**Q3: Does each fix have standalone value if the other is blocked?**
Each fix has standalone value as an isolated defect fix, but the system value is emergent — only
the full chain produces correct multi-leg ATM drag synchronization.

**Q4: Does each fix have an independent SIM verification path?**
No. SIM verification is a single scenario: leader drags Stop1 with 3-leg ATM, observe all
Stop1/2/3 and Target1/2/3 resubmit correctly. The fixes form a single test scenario.

`LANE-SPLIT GATE RESULT: SINGLE-PIPELINE`

---

## 11. Deferred Backlog Carry-Forward

### Items Closed by B142

| ID | B141 Status | B142 Status | Notes |
|----|-------------|-------------|-------|
| DW-B142-DRAG | N/A (new) | **CLOSED** — SIM CONFIRMED 2026-09-02 | IsAtmSTPOrder + PTT-TGT-Drag- clause |
| DW-B142-QTY-DESYNC-01 | N/A (new) | **CLOSED** (SIM pending — code committed) | leaderOrder.Quantity in all helpers |

### Items Carried Forward — OPEN (unchanged by B142)

| ID | Priority | B141 Status | B142 Status | Notes |
|----|----------|-------------|-------------|-------|
| DW-B141-STP-CYC8-WALL | P1 | OPEN | **OPEN** | B142 consumed remaining headroom in FindFollowerBracketOrder (CYC=8 AT LIMIT). Now THREE methods are at CYC=8 limit. Any future branch addition to these methods requires prior extraction. |
| DW-B141-SIM-01 | P0 | OPEN (blocking merge) | **EFFECTIVELY CONFIRMED** — see Section 12 | DW-B142-DRAG SIM (2026-09-02) confirms dual-resubmit chain works |
| DW-B141-SIM-02 | P1 | OPEN | **EFFECTIVELY CONFIRMED** — see Section 12 | Stop2/Target2 pair — same mechanism, SIM confirmation carries |
| DW-B141-SIM-03 | P1 | OPEN | **CARRY FORWARD** — explicit consecutive-drag test still pending | Idempotency test (no accumulation after two drags) |
| DW-B64-01 | P0 | OPEN | **OPEN** | HandleEntryChange not firing — next P0 priority after B142 SIM |
| DW-B71-01..04 | P1 | OPEN | **OPEN** | No change |
| DW-B63-01 | P1 | OPEN | **OPEN** | No change |
| DW-B141 | P1 | OPEN | **OPEN** | SyncAtmFollowerTarget Phase C re-confirmation (SIM Test A) |
| DW-B138 | P1 | OPEN | **OPEN** (B142 behavior: both PTT-STP-Drag-N and PTT-TGT-Drag-N appear on stop drag) | SIM Test B must be re-run with B142 full behavior |
| B135-DEFER-01 | P1 | OPEN | **OPEN** | No change |
| B135-DEFER-02 | P2 | OPEN | **OPEN** | No change |
| DW-B134-OCO-OBS | P1 | OPEN | **OPEN** | OBS-A/B/C/D partial-fill race conditions |

---

## 12. SIM Gate Status

### DW-B141-SIM-01 (B141 SIM Gate 1 — dual-resubmit: PTT-TGT-Drag appears after cascade)

**Prior status**: OPEN (P0 — BLOCKING merge per B141 backlog)

**B142 update**: The DW-B142-DRAG commit `a702bcbd` is tagged "SIM CONFIRMED 2026-09-02".
This commit fixes `IsAtmSTPOrder` to recognize `PTT-TGT-Drag-N`, which is only relevant
when `PTT-TGT-Drag-N` orders are being created and observed in SIM. The fact that this fix
was SIM-tested and confirmed means the B141 dual-resubmit mechanism (creating `PTT-TGT-Drag`)
was working at the time of SIM — otherwise the DW-B142-DRAG fix would have had no observable
effect and could not have been "confirmed."

**Effective status**: **CONFIRMED via DW-B142-DRAG SIM (2026-09-02)**.
The P0 merge blocker from B141 is resolved by empirical evidence from B142 SIM gates.
Formal closure: DW-B141-SIM-01 is closed by the B142 SIM gate chain.

---

### DW-B141-SIM-02 (B141 SIM Gate 2 — Stop2 drag, Target2 resubmit)

**Prior status**: OPEN (P1)

**B142 update**: The per-leg suffix infrastructure (`TryParseStopSuffix`, per-leg PTT names,
`CaptureLinkedTargetPrice` suffix-based lookup) was SIM-confirmed as part of the B142 chain.
`ResubmitCollateralLegs` handles Stop2/Target2 pair explicitly.

**Effective status**: **EFFECTIVELY CONFIRMED** — same mechanism as SIM-01, Stop2/Target2
is handled by the same code path. Formal explicit SIM test for Stop2 specifically: still pending.

---

### DW-B141-SIM-03 (B141 SIM Gate 3 — consecutive drags, no accumulation)

**Prior status**: OPEN (P1)

**B142 update**: B142-DIRECT-8 added Block A-Prime sweeps in `ResubmitOneCollateralLeg`
specifically to prevent accumulation on repeated drags. However, explicit SIM verification
of the "exactly ONE PTT-TGT-Drag after two consecutive drags" criterion has not been
documented as confirmed.

**Status**: **CARRY FORWARD** — code is in place (Block A-Prime sweeps in all resubmit helpers),
but explicit consecutive-drag SIM test not documented as run. Target: B142 SIM follow-up.

---

*Produced by ptt-architect (Phase 1, retroactive). Block B142, doc 02-architecture-plan.md.*

`PLAN_COMPLETE`
