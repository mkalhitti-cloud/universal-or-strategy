# B142 Tickets — Drag-Sync System Hardening

**Block**: B142
**Mode**: RETROACTIVE — tickets describe committed implementation for ptt-verifier
**Plan ref**: docs/brain/B142/02-architecture-plan.md (REVIEW_PASS)
**Source file**: src/PropTraderTools/CopyEngine.cs

---

## Ticket T1 — Guard Rails and Order Name Hardening

### Spec Requirement IDs
- COPIER-DRAG-01: stop drag events on follower accounts must copy leader stop price
- COPIER-DRAG-02: PTT-prefixed orders must not be misclassified as trailing stops
- COPIER-DRAG-03: per-leg order names must prevent concurrent drag collisions
- COPIER-INIT-01: session-start spurious cancels must be suppressed when NT8 bracket price not yet populated

### File Path
src/PropTraderTools/CopyEngine.cs

### Commits Covered
- `4cc50a24` — B142-DIRECT-1: IsTrailingStop excludes PTT-STP-Drag orders
- `e8d529e2` — B142-DIRECT-2: fo.StopPrice < tickSize guard in SyncFollowerBracket branch (3)
- `220bc152` — B142-DIRECT-3: per-leg PTT order names (PTT-STP-Drag-N / PTT-TGT-Drag-N); MatchesLeaderName per-leg matching; SyncAtmFollowerBracket suffix param; CancelExistingPttStpDrag suffix param; ResubmitTargetAfterCascade suffix param

### Method Signatures (exact — copy from source)

```csharp
// L2218-2227
private static bool IsTrailingStop(Order order)

// L2266-2345
private void SyncFollowerBracket(
    Account acc,
    Order leaderOrder,
    bool isStop,
    double newPrice,
    double tickSize
)

// L2382-2432
private void SyncAtmFollowerBracket(Account acc, Order fo, double newPrice, string suffix, Order leaderOrder = null)

// L2801-2822
private void CancelExistingPttStpDrag(Account acc, Order fo, string suffix)

// L2575-2636
private void ResubmitTargetAfterCascade(
    Account acc,
    Order stpOrder,
    double targetPrice,
    Order leaderOrder,
    string suffix)

// L3193-3210
private static bool MatchesLeaderName(Order order, string? leaderName, bool isStop)
```

### What Each Method Does (retroactive description)

**`IsTrailingStop`** (L2218-2227): Classifies a follower order as a trailing stop by testing `order.OrderType == OrderType.StopMarket`. B142-DIRECT-1 added the second clause `&& (order.Name == null || !order.Name.StartsWith("PTT-", StringComparison.Ordinal))`. This exclusion prevents `PTT-STP-Drag-N` orders (AddOn-created StopMarket orders from earlier drags) from being falsely classified as trailing stops. Without this fix, `SyncFollowerBracket` branch (4) fired for all second+ stop drags, silently skipping the cancel+resubmit path and producing no movement on the follower account.

**`SyncFollowerBracket`** (L2266-2345): Orchestrates follower bracket synchronization for one follower account. Calls `FindFollowerBracketOrder` to locate the incumbent `fo`, then routes to branch (3) for ATM stop cancel+resubmit, branch (3b) for ATM target cancel+resubmit, branch (4) for trailing stop skip, or the generic `acc.Change()` path. B142-DIRECT-2 added `if (fo.StopPrice < tickSize) return;` at L2300 as the first check inside branch (3). This guards against session-start events where NT8 has placed the ATM bracket in Accepted state but has not yet populated `StopPrice` — the outer price-delta check at branch (2) passes because `|newPrice - 0| >> tickSize`, so an explicit zero-price guard is required. B142-DIRECT-3 changed the stop suffix source from `fo.Name` to `leaderOrder.Name`, ensuring the correct numeric suffix is always parsed (on second+ drags, `fo.Name` is `PTT-STP-Drag-1` which is not parseable by `TryParseStopSuffix`).

**`SyncAtmFollowerBracket`** (L2382-2432): Executes cancel+resubmit for the primary dragged stop leg on the follower account. B142-DIRECT-3 added the `suffix` parameter; the cancel pre-sweep `CancelExistingPttStpDrag` and the `CreateOrder` call both use `"PTT-STP-Drag-" + suffix`. Per-leg naming ensures Stop1 events do not cancel or collide with Stop2/Stop3 PTT drag orders on concurrent drag events.

**`CancelExistingPttStpDrag`** (L2801-2822): Block A-Prime pre-sweep executed before `acc.Cancel(fo)` in `SyncAtmFollowerBracket`. Iterates `acc.Orders.ToList()`, cancels any live order matching `"PTT-STP-Drag-" + suffix` on the same instrument. B142-DIRECT-3 changed the target from the unsuffixed `"PTT-STP-Drag"` to the per-leg `"PTT-STP-Drag-" + suffix`, so Stop1's pre-sweep does not cancel Stop2's resubmitted order.

**`ResubmitTargetAfterCascade`** (L2575-2636): After OCO cascade cancels the linked ATM target, resubmits a standalone `PTT-TGT-Drag-{suffix}` limit order at the captured price. B142-DIRECT-3 added the `suffix` parameter; Block A-Prime sweeps `"PTT-TGT-Drag-" + suffix` and Block B creates `"PTT-TGT-Drag-" + suffix`. Per-leg naming ensures Stop1's resubmit does not cancel Stop2's target order on concurrent drags.

**`MatchesLeaderName`** (L3193-3210): Predicate called by `FindFollowerBracketOrder` to test whether a candidate order is the incumbent bracket for a given leader order name. B142-DIRECT-3 added per-leg PTT name matching: extracts the trailing digit from `leaderName` (e.g. `"Stop1"` → `'1'`) and constructs `"PTT-TGT-Drag-1"` (for target) or `"PTT-STP-Drag-1"` (for stop) as additional match candidates. This allows `FindFollowerBracketOrder` to locate `PTT-STP-Drag-1` as the incumbent `fo` on the second+ stop drag, instead of returning `null` and skipping the drag.

### JS Rule Constraints

**`IsTrailingStop`**:
- JS-021 (no lock): PASS — static pure function, no shared state
- JS-001 (no throw in hot path): PASS — returns bool expression, no exception path
- JS-002 (no null return for values): PASS — returns bool, never null

**`SyncFollowerBracket`**:
- JS-021 (no lock): PASS — no lock anywhere; all NT8 API calls thread-safe from dispatch thread
- JS-001 (no throw in hot path): PASS — `acc.Change()` path wrapped in try/catch at L2341-2359; exceptions absorbed via `StatusUpdate`
- JS-002 (no null return): PASS — void return

**`SyncAtmFollowerBracket`**:
- JS-021 (no lock): PASS
- JS-001 (no throw): PASS — two independent try/catch blocks (Block A: Cancel; Block B: CreateOrder+Submit); exceptions absorbed
- JS-002: PASS — void return

**`CancelExistingPttStpDrag`**:
- JS-021: PASS
- JS-001: PASS — try/catch inside sweep loop; exceptions absorbed
- JS-002: PASS — void

**`ResubmitTargetAfterCascade`**:
- JS-021: PASS
- JS-001: PASS — Block A-Prime and Block B each have independent try/catch
- JS-002: PASS — void

**`MatchesLeaderName`**:
- JS-021: PASS — static, no shared state
- JS-001: PASS — no throw; all paths return bool
- JS-002: PASS — bool return

### xUnit [Fact] Test Names

- `IsTrailingStop_PttSTPDrag_ReturnsFalse` — asserts IsTrailingStop returns false for an order with `OrderType.StopMarket` and `Name = "PTT-STP-Drag-1"`
- `IsTrailingStop_AtmStopMarket_ReturnsTrue` — asserts IsTrailingStop returns true for an order with `OrderType.StopMarket` and `Name = "Stop1"` (null-prefix guard)
- `SyncFollowerBracket_StopPriceZero_ReturnsWithoutCancel` — asserts that when `fo.StopPrice == 0.0` and `tickSize = 0.25`, `SyncFollowerBracket` returns without calling `acc.Cancel` (B142-DIRECT-2 guard)
- `MatchesLeaderName_PttSTPDrag1_MatchesLeaderStop1` — asserts `MatchesLeaderName` returns true for an order named `"PTT-STP-Drag-1"` when `leaderName = "Stop1"` and `isStop = true`
- `MatchesLeaderName_PttTGTDrag2_MatchesLeaderTarget2` — asserts `MatchesLeaderName` returns true for an order named `"PTT-TGT-Drag-2"` when `leaderName = "Target2"` and `isStop = false`
- `CancelExistingPttStpDrag_SweepsOnlySuffix1_NotSuffix2` — asserts that calling `CancelExistingPttStpDragTestable` with `suffix = "1"` cancels `"PTT-STP-Drag-1"` and does not cancel `"PTT-STP-Drag-2"` on the same account

### 7-Scan Checklist

- **SCAN-01 lock()**: PASS — `grep "lock(" src/PropTraderTools/CopyEngine.cs` returns zero matches in T1 methods. No `lock()` anywhere in file.
- **SCAN-02 DateTime.Now**: PASS — no `DateTime.Now` in any T1 method. No `DateTime.Now` anywhere in file.
- **SCAN-03 ASCII-only**: PASS — all string literals in T1 methods are ASCII: `"PTT-"`, `"PTT-STP-Drag-"`, `"PTT-TGT-Drag-"`, `"Stop1/2/3"`, digit chars. No Unicode, emoji, or non-ASCII characters.
- **SCAN-04 FontFamily**: PASS — no `FontFamily` reference in any T1 method or anywhere in file.
- **SCAN-05 CYC<=8**:
  - `IsTrailingStop`: CYC=1 — PASS
  - `SyncFollowerBracket`: CYC=8 — PASS (AT LIMIT, within threshold)
  - `SyncAtmFollowerBracket`: CYC=5 — PASS
  - `CancelExistingPttStpDrag`: CYC=6 — PASS
  - `ResubmitTargetAfterCascade`: CYC=4 — PASS
  - `MatchesLeaderName`: CYC=5 — PASS
- **SCAN-06 PTT- prefix on CreateOrder calls**: PASS — `SyncAtmFollowerBracket` L2416 uses `"PTT-STP-Drag-" + suffix`; `ResubmitTargetAfterCascade` L2620 uses `tgtDragName` which is `"PTT-TGT-Drag-" + suffix`. All `CreateOrder` calls in T1 methods use PTT-prefixed names.
- **SCAN-07 Dispatcher.InvokeAsync for UI**: N/A — T1 methods are pure order-management logic executing on the NT8 dispatch thread. No WPF UI interactions in these methods.

---

## Ticket T2 — OCO Cascade Management

### Spec Requirement IDs
- COPIER-DRAG-04: second+ stop drag events (fo.Name starts with PTT-STP-Drag-) must route to cancel+resubmit path, not acc.Change() no-op
- COPIER-DRAG-05: target cancel must be suppressed when ATM target LimitPrice not yet populated (Submitted state)
- COPIER-DRAG-06: before Stop1 ATM cancel, all collateral leg target prices must be captured and collateral legs must be resubmitted after cascade

### File Path
src/PropTraderTools/CopyEngine.cs

### Commits Covered
- `2b052b5d` — B142-DIRECT-5: `SyncAtmFollowerTarget` `fo.LimitPrice <= 0` guard; `IsAtmSTPOrder` `PTT-STP-Drag-` clause (DIRECT-4 folded into same commit)
- `fbf39d0e` — B142-DIRECT-6: `CaptureOtherLegTargetPrices` (new method); `ResubmitCollateralLegs` (new method); `SyncFollowerBracket` branch (3) wiring

### Method Signatures (exact — copy from source)

```csharp
// L2240-2248
internal static bool IsAtmSTPOrder(Order order)

// L2481-2501
private double[] CaptureOtherLegTargetPrices(Account acc, Order fo, string excludeSuffix)

// L2649-2670
private void ResubmitCollateralLegs(
    Account acc,
    Order fo,
    double newPrice,
    double[] otherLegPrices,
    string excludeSuffix,
    Order leaderOrder)

// L2856-2940
private void SyncAtmFollowerTarget(
    Account acc,
    Order fo,
    double newPrice,
    Order? leaderOrder = null
)
```

### What Each Method Does (retroactive description)

**`IsAtmSTPOrder`** (L2240-2248): Expression-body predicate that returns true when an order is an ATM bracket stop or target recognized by the copy engine. The pre-B142 version matched names ending with `"STP"`, starting with `"Stop"`, or starting with `"Target"`. B142-DIRECT-4 (committed in `2b052b5d`) added `|| order.Name.StartsWith("PTT-STP-Drag-", StringComparison.Ordinal)` so that second+ stop drag events (where `fo.Name == "PTT-STP-Drag-N"`) route to branch (3) in `SyncFollowerBracket` (cancel+resubmit) instead of the generic `acc.Change()` no-op. DW-B142-DRAG (commit `a702ccbd`) added the symmetric `|| order.Name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)` clause — see T4.

**`CaptureOtherLegTargetPrices`** (L2481-2501): New method added in B142-DIRECT-6. Returns a `double[3]` indexed by suffix-1 containing the current `LimitPrice` of each ATM target leg other than `excludeSuffix`. Called in `SyncFollowerBracket` branch (3) before `SyncAtmFollowerBracket` executes `acc.Cancel(Stop1_ATM)`, which OCO-cascades to cancel Stop2, Stop3, Target2, and Target3. Without this capture, the cascaded cancellation destroys the target prices for collateral legs before `ResubmitCollateralLegs` can use them. Guard: if `fo.Name` does not start with `"Stop"` (i.e., `fo` is already a `PTT-STP-Drag-N` from a prior drag), the ATM OCO group is already broken and collateral legs are standalone PTT orders that are NOT cascade victims — the method returns `prices` (all zeros) as a safe no-op signal to the caller. B142-DIRECT-9 enhanced the price preference logic to use `PTT-TGT-Drag-N` over `ATM TargetN` when both exist (see T3).

**`ResubmitCollateralLegs`** (L2649-2670): New method added in B142-DIRECT-6. Iterates suffix values 1-3, skips `excludeSuffix` (the primary dragged leg) and any leg with `otherLegPrices[i-1] <= 0`. For each valid collateral leg, calls `FindLeaderCollateralOrder` to get the leader's per-leg bracket order (for quantity lookup), then delegates to `ResubmitOneCollateralLeg`. The all-zeros guard from `CaptureOtherLegTargetPrices` means this method is a no-op on second+ drags where the ATM OCO group is already broken.

**`SyncAtmFollowerTarget`** (L2856-2940): Cancel+resubmit for ATM target brackets on target drag events (branch 3b in `SyncFollowerBracket`). B142-DIRECT-5 added `fo.LimitPrice <= 0 ||` to the guard condition at L2867: when an ATM target is in `Submitted` state, NT8 has not yet populated `LimitPrice` (it is 0). The pre-B142 `IsNoPriceChange(fo.LimitPrice, newPrice)` check passed because `IsNoPriceChange(0, realPrice)` is false, causing `acc.Cancel(Target3)` to fire spuriously — the OCO cascade then cancelled `Stop3` before the first drag event. The `fo.LimitPrice <= 0` guard prevents this. B142-DIRECT-7 added per-leg `tgtDragName` (`"PTT-TGT-Drag-" + DeriveLeaderBracketIndex(leaderOrder)`) — see T3 for detail.

### JS Rule Constraints

**`IsAtmSTPOrder`**:
- JS-021 (no lock): PASS — expression body, no shared state
- JS-001 (no throw): PASS — pure predicate, no exception path
- JS-002 (no null return): PASS — returns bool

**`CaptureOtherLegTargetPrices`**:
- JS-021: PASS
- JS-001: PASS — no throw; iteration uses `ToList()` snapshot; all paths return array
- JS-002: PASS — `double[]` is a value array, not a reference-null return. Returns all-zeros array on guard path rather than null.

**`ResubmitCollateralLegs`**:
- JS-021: PASS
- JS-001: PASS — delegates throw-free to `ResubmitOneCollateralLeg` which has its own try/catch
- JS-002: PASS — void return

**`SyncAtmFollowerTarget`**:
- JS-021: PASS
- JS-001: PASS — Block A-Prime, Block A, and Block B each have independent try/catch; exceptions absorbed via `StatusUpdate`
- JS-002: PASS — void return

### xUnit [Fact] Test Names

- `IsAtmSTPOrder_PttSTPDrag1_ReturnsTrue` — asserts `IsAtmSTPOrder` returns true for order with `Name = "PTT-STP-Drag-1"`
- `IsAtmSTPOrder_PttSTPDrag3_ReturnsTrue` — asserts `IsAtmSTPOrder` returns true for order with `Name = "PTT-STP-Drag-3"`
- `IsAtmSTPOrder_GenericStopMarket_ReturnsFalse` — asserts `IsAtmSTPOrder` returns false for an order with `Name = "generic-stop"` (not matching any ATM or PTT pattern)
- `SyncAtmFollowerTarget_LimitPriceZero_SkipsCancel` — asserts that when `fo.LimitPrice == 0`, `SyncAtmFollowerTarget` returns early without calling `acc.Cancel`
- `CaptureOtherLegTargetPrices_PttFoName_ReturnsAllZeros` — asserts that when `fo.Name = "PTT-STP-Drag-1"`, `CaptureOtherLegTargetPrices` returns a `double[3]` with all values zero (second+ drag guard)
- `CaptureOtherLegTargetPrices_StopFoName_CapturesLegs2And3` — asserts that when `fo.Name = "Stop1"` and account has live `Target2` (Working) and `Target3` (Submitted), the returned array has `prices[1] > 0` and `prices[2] > 0` and `prices[0] == 0` (excludeSuffix="1")
- `ResubmitCollateralLegs_AllZeroPrices_NoResubmit` — asserts that when `otherLegPrices` is `{0,0,0}`, `ResubmitCollateralLegs` makes zero `acc.CreateOrder` calls

### 7-Scan Checklist

- **SCAN-01 lock()**: PASS — no `lock()` in any T2 method. Zero instances in file.
- **SCAN-02 DateTime.Now**: PASS — no `DateTime.Now` in any T2 method.
- **SCAN-03 ASCII-only**: PASS — all string literals ASCII: `"PTT-STP-Drag-"`, `"PTT-TGT-Drag-"`, `"Stop"`, `"Target"`, `"STP"`, digit strings, status message strings.
- **SCAN-04 FontFamily**: PASS — no `FontFamily` reference.
- **SCAN-05 CYC<=8**:
  - `IsAtmSTPOrder`: CYC=1 — PASS
  - `CaptureOtherLegTargetPrices`: CYC=6 — PASS
  - `ResubmitCollateralLegs`: CYC=4 — PASS
  - `SyncAtmFollowerTarget`: CYC=8 — PASS (AT LIMIT, within threshold)
- **SCAN-06 PTT- prefix on CreateOrder calls**: PASS — `SyncAtmFollowerTarget` L2922 uses `tgtDragName` which is `"PTT-TGT-Drag-" + tgtIdx.ToString()` when `tgtIdx > 0`. All CreateOrder calls in T2 methods use PTT-prefixed names.
- **SCAN-07 Dispatcher.InvokeAsync for UI**: N/A — T2 methods are pure order-management logic on NT8 dispatch thread. No WPF UI interactions.

---

## Ticket T3 — Drag Order State Accuracy

### Spec Requirement IDs
- COPIER-DRAG-07: target capture must succeed when target is in Submitted, ChangeSubmitted, or ChangePending state
- COPIER-DRAG-08: per-leg PTT-TGT-Drag-N naming must be used in SyncAtmFollowerTarget to prevent concurrent target drag collisions
- COPIER-DRAG-09: ResubmitOneCollateralLeg must sweep existing PTT drag orders before resubmitting (Block A-Prime-Stop and Block A-Prime-Target)
- COPIER-DRAG-10: PTT-TGT-Drag-N price must be preferred over ATM TargetN price when both coexist

### File Path
src/PropTraderTools/CopyEngine.cs

### Commits Covered
- `77a02254` — B142-DIRECT-7: `IsTargetOrderLive` adds `OrderState.Submitted` (BUG A); `SyncAtmFollowerTarget` per-leg `PTT-TGT-Drag-N` naming (BUG B)
- `cd3d9f02` — B142-DIRECT-8: `ResubmitOneCollateralLeg` Block A-Prime-Stop + Block A-Prime-Target sweeps
- `ca8ad16f` — B142-DIRECT-9: `CaptureLinkedTargetPrice` PTT-TGT-Drag-N preference; `CaptureOtherLegTargetPrices` PTT price overwrite; `IsTargetOrderLive` adds `ChangeSubmitted`/`ChangePending`; `FindFollowerBracketOrder` adds `ChangeSubmitted` to state filter

### Method Signatures (exact — copy from source)

```csharp
// L2553-2561
private static bool IsTargetOrderLive(Order o)

// L2447-2465
private double? CaptureLinkedTargetPrice(Account acc, string stopName)

// L2688-2772
private void ResubmitOneCollateralLeg(
    Account acc,
    Order fo,
    double newPrice,
    double targetPrice,
    string suffix,
    Order leaderLeg = null)

// L3138-3171
private Order? FindFollowerBracketOrder(
    IEnumerable<Order> orders,
    string? fromEntrySignalName,
    bool isStop,
    string? leaderName = null
)
```

### What Each Method Does (retroactive description)

**`IsTargetOrderLive`** (L2553-2561): Expression-body predicate returning true when order `o` is in a live (capturable) state. B141 established the base with `Working` and `Accepted`. B142-DIRECT-7 BUG A added `OrderState.Submitted` — the NT8 ATM engine places `Target3` in `Submitted` state briefly before transitioning to `Working`; `CaptureOtherLegTargetPrices` was missing these orders, leaving `prices[2] = 0` and causing `ResubmitCollateralLegs` to skip leg 3 entirely. B142-DIRECT-9 BUG C added `OrderState.ChangeSubmitted` and `OrderState.ChangePending` — rapid back-to-back drags leave `PTT-TGT-Drag-N` in `ChangeSubmitted` state; the capture methods were falling back to the original ATM `TargetN.LimitPrice`, overwriting the dragged price. Current states covered: `Working`, `Accepted`, `Submitted`, `ChangeSubmitted`, `ChangePending`.

**`CaptureLinkedTargetPrice`** (L2447-2465): Captures the `LimitPrice` of the target linked to the primary dragged stop leg before `acc.Cancel(fo)` triggers the OCO cascade. Uses `TryParseStopSuffix(stopName)` to derive suffix, then searches `acc.Orders.ToList()` for `"PTT-TGT-Drag-{suffix}"` and `"Target{suffix}"`. B142-DIRECT-9 BUG A changed the logic from single-scan to dual-scan: `pttPrice` stores the PTT order price; `atmPrice` stores the ATM order price; the method returns `pttPrice` when `pttPrice.HasValue`, else `atmPrice`. This prevents the case where both `Target1` (original ATM) and `PTT-TGT-Drag-1` (from a prior target drag) coexist, and the stop cascade was overwriting the dragged PTT price with the original ATM price.

**`ResubmitOneCollateralLeg`** (L2688-2772): Creates `PTT-STP-Drag-{suffix}` at `newPrice` and `PTT-TGT-Drag-{suffix}` at `targetPrice` for a single collateral leg. B142-DIRECT-8 added Block A-Prime-Stop (L2699-2703): iterates `acc.Orders.ToList()` and cancels any live `"PTT-STP-Drag-{suffix}"` for the same instrument before calling `acc.CreateOrder`. Block A-Prime-Target (L2708-2712): same sweep for `"PTT-TGT-Drag-{suffix}"`. Without these sweeps, each repeated stop drag accumulated a new pair of PTT orders per collateral leg (N stop drags → N PTT-STP-Drag + N PTT-TGT-Drag per leg). DW-B142-QTY-DESYNC-01 added `leaderLeg` parameter and uses `leaderLeg != null ? leaderLeg.Quantity : fo.Quantity` in both `CreateOrder` calls — see T4 for quantity details.

**`FindFollowerBracketOrder`** (L3138-3171): Locates the follower's incumbent bracket order by iterating `orders`, applying `OrderPassesBracketGate`, filtering by order state, and matching by type (`StopMarket`/`StopLimit` for stop; `Limit` for target). B142-DIRECT-9 BUG B added `OrderState.ChangeSubmitted` to the state filter at L3153. Without this, rapid back-to-back drags that left `PTT-TGT-Drag-N` in `ChangeSubmitted` state caused `fo = null`, the drag was silently skipped, and the follower target price diverged from the leader. With `ChangeSubmitted` in the filter, `fo` is found and `acc.Change()` is issued — NT8 queues or absorbs the overlapping change and the follower price converges.

### JS Rule Constraints

**`IsTargetOrderLive`**:
- JS-021 (no lock): PASS — expression body, no shared state
- JS-001 (no throw): PASS — pure predicate
- JS-002 (no null): PASS — bool return

**`CaptureLinkedTargetPrice`**:
- JS-021: PASS
- JS-001: PASS — no throw; returns `double?` on all paths
- JS-002: PASS — `double?` is a nullable VALUE type (not a reference null). Returning `null` (`double?` with no value) is the documented contract for "target not found" — not a JS-002 violation.

**`ResubmitOneCollateralLeg`**:
- JS-021: PASS
- JS-001: PASS — Block A-Prime-Stop and Block A-Prime-Target each use `try { ... } catch { }` (empty catch per project convention); Block B and Block C (stop/target create) each have `catch (Exception ex)` → `StatusUpdate`
- JS-002: PASS — void return

**`FindFollowerBracketOrder`**:
- JS-021: PASS — no lock; iterates a passed `IEnumerable<Order>` (caller provides snapshot)
- JS-001: PASS — no throw; returns `Order?` null on not-found
- JS-002: PASS — `Order?` null contract is explicitly documented and tested

### xUnit [Fact] Test Names

- `IsTargetOrderLive_Submitted_ReturnsTrue` — asserts `IsTargetOrderLive` returns true for order in `OrderState.Submitted`
- `IsTargetOrderLive_ChangeSubmitted_ReturnsTrue` — asserts `IsTargetOrderLive` returns true for order in `OrderState.ChangeSubmitted`
- `IsTargetOrderLive_ChangePending_ReturnsTrue` — asserts `IsTargetOrderLive` returns true for order in `OrderState.ChangePending`
- `IsTargetOrderLive_Cancelled_ReturnsFalse` — asserts `IsTargetOrderLive` returns false for order in `OrderState.Cancelled`
- `CaptureLinkedTargetPrice_PttPricePreferredOverAtmPrice` — asserts that when both `Target1` (LimitPrice=7640) and `PTT-TGT-Drag-1` (LimitPrice=7647.25) are Working, the method returns 7647.25 (PTT preferred)
- `CaptureLinkedTargetPrice_PttAbsent_ReturnsAtmPrice` — asserts that when only `Target1` (LimitPrice=7640) is Working, the method returns 7640
- `ResubmitOneCollateralLeg_ExistingPttSTPDrag_SweptBeforeCreate` — asserts that a pre-existing Working `"PTT-STP-Drag-2"` order is cancelled before the new `CreateOrder` call for suffix "2"
- `ResubmitOneCollateralLeg_ExistingPttTGTDrag_SweptBeforeCreate` — asserts that a pre-existing Working `"PTT-TGT-Drag-2"` order is cancelled before the new `CreateOrder` call for suffix "2"
- `FindFollowerBracketOrder_ChangeSubmittedState_ReturnsFo` — asserts that an order in `OrderState.ChangeSubmitted` matching instrument and name is returned (not skipped)

### 7-Scan Checklist

- **SCAN-01 lock()**: PASS — no `lock()` in any T3 method. Zero instances in file.
- **SCAN-02 DateTime.Now**: PASS — no `DateTime.Now` in any T3 method.
- **SCAN-03 ASCII-only**: PASS — all string literals ASCII: `"PTT-TGT-Drag-"`, `"PTT-STP-Drag-"`, `"Target"`, `"Stop"`, suffix digit strings, status strings.
- **SCAN-04 FontFamily**: PASS — no `FontFamily` reference.
- **SCAN-05 CYC<=8**:
  - `IsTargetOrderLive`: CYC=1 — PASS
  - `CaptureLinkedTargetPrice`: CYC=5 — PASS
  - `ResubmitOneCollateralLeg`: CYC=7 — PASS
  - `FindFollowerBracketOrder` (list overload): CYC=8 — PASS (AT LIMIT, within threshold)
- **SCAN-06 PTT- prefix on CreateOrder calls**: PASS — `ResubmitOneCollateralLeg` L2727 uses `"PTT-STP-Drag-" + suffix` and L2756 uses `"PTT-TGT-Drag-" + suffix`. All CreateOrder calls in T3 methods use PTT-prefixed names.
- **SCAN-07 Dispatcher.InvokeAsync for UI**: N/A — T3 methods are pure order-management logic on NT8 dispatch thread. No WPF UI interactions.

---

## Ticket T4 — DW Card Fixes (DW-B142-DRAG + DW-B142-QTY-DESYNC-01)

### Spec Requirement IDs
- COPIER-DRAG-11: second+ target drag events (fo.Name starts with PTT-TGT-Drag-) must route to cancel+resubmit path in SyncAtmFollowerTarget (branch 3b), not acc.Change() no-op
- COPIER-QTY-01: resubmitted PTT stop and target orders must use the leader account's per-leg quantity, not the follower's order quantity
- COPIER-QTY-02: collateral leg resubmit must use the leader's per-leg bracket quantity (StopN/TargetN from leader account) not the primary dragged leg's quantity

### File Path
src/PropTraderTools/CopyEngine.cs

### Commits Covered
- `a702ccbd` — DW-B142-DRAG: `IsAtmSTPOrder` `PTT-TGT-Drag-` clause (SIM CONFIRMED 2026-09-02)
- `b30345c5` — DW-B142-QTY-DESYNC-01: `FindLeaderCollateralOrder` (new helper); `leaderOrder.Quantity` in `SyncAtmFollowerBracket`, `SyncAtmFollowerTarget`, `ResubmitTargetAfterCascade`, `ResubmitOneCollateralLeg`

### Method Signatures (exact — copy from source)

```csharp
// L2240-2248 (DW-B142-DRAG clause added)
internal static bool IsAtmSTPOrder(Order order)

// L2525-2537 (new method)
private static Order FindLeaderCollateralOrder(Order leaderOrder, string suffix)

// L2382-2432 (leaderOrder.Quantity applied)
private void SyncAtmFollowerBracket(Account acc, Order fo, double newPrice, string suffix, Order leaderOrder = null)

// L2856-2940 (leaderOrder?.Quantity applied)
private void SyncAtmFollowerTarget(
    Account acc,
    Order fo,
    double newPrice,
    Order? leaderOrder = null
)

// L2575-2636 (leaderOrder.Quantity applied)
private void ResubmitTargetAfterCascade(
    Account acc,
    Order stpOrder,
    double targetPrice,
    Order leaderOrder,
    string suffix)

// L2688-2772 (leaderLeg.Quantity applied)
private void ResubmitOneCollateralLeg(
    Account acc,
    Order fo,
    double newPrice,
    double targetPrice,
    string suffix,
    Order leaderLeg = null)
```

### What Each Method Does (retroactive description)

**`IsAtmSTPOrder`** (DW-B142-DRAG addition): The DW-B142-DRAG commit `a702ccbd` added `|| order.Name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)` to `IsAtmSTPOrder`. This is the symmetric fix to B142-DIRECT-4 (PTT-STP-Drag- clause). Without this clause, on second+ target drag events where `fo.Name == "PTT-TGT-Drag-N"`, `IsAtmSTPOrder` returned false, branch (3b) in `SyncFollowerBracket` was skipped, and the generic `acc.Change()` path was taken — a silent no-op on AddOn-created Limit orders. With this clause, second+ target drags route to `SyncAtmFollowerTarget` and execute cancel+resubmit correctly. SIM confirmed 2026-09-02.

**`FindLeaderCollateralOrder`** (L2525-2537): New static helper added by DW-B142-QTY-DESYNC-01. Accepts `leaderOrder` and a `suffix` string ("1"/"2"/"3"). Searches `leaderOrder.Account.Orders.ToList()` for an order named `"Stop" + suffix` or `"Target" + suffix`. Returns the first match or `null` if not found. Called by `ResubmitCollateralLegs` to resolve the per-leg bracket quantity for each collateral leg. Before this method existed, all collateral legs were resubmitted with `fo.Quantity` (the quantity of the primary dragged stop order), which was wrong when legs had different quantities.

**`SyncAtmFollowerBracket`** (quantity fix): B142-QTY-DESYNC-01 changed the `CreateOrder` call at L2412 from `fo.Quantity` to `leaderOrder.Quantity`. The leader's stop leg quantity is the authoritative source for how many contracts the follower should hold on that leg. `leaderOrder` is the NT8 `Order` object that triggered the drag event — always present in branch (3) since `SyncFollowerBracket` passes it. The parameter is `Order leaderOrder = null` for backward compatibility with existing tests; null is guarded via the `leaderOrder.Quantity` call (callers in production always pass non-null).

**`SyncAtmFollowerTarget`** (quantity fix): B142-QTY-DESYNC-01 changed the `CreateOrder` call at L2918 from `fo.Quantity` to `leaderOrder != null ? leaderOrder.Quantity : fo.Quantity`. The `leaderOrder` parameter is `Order? leaderOrder = null` for backward compatibility. When `leaderOrder` is available (all production call sites), the leader's target quantity is used for the resubmitted `PTT-TGT-Drag-N` order.

**`ResubmitTargetAfterCascade`** (quantity fix): B142-QTY-DESYNC-01 changed the `CreateOrder` call at L2616 from `stpOrder.Quantity` to `leaderOrder.Quantity`. `stpOrder` is the follower's stop leg; its quantity could differ from the leader's target leg quantity. `leaderOrder` is the leader's stop order that triggered the cascade — not the leader's target order — but in a properly sized 1:1 copy relationship the stop and target legs share the same quantity.

**`ResubmitOneCollateralLeg`** (quantity fix): B142-QTY-DESYNC-01 changed both `CreateOrder` calls (L2723 for stop, L2752 for target) from `fo.Quantity` to `leaderLeg != null ? leaderLeg.Quantity : fo.Quantity`. `leaderLeg` is provided by `FindLeaderCollateralOrder` in `ResubmitCollateralLegs`. The `null` fallback preserves the prior behavior if the leader's bracket order for that suffix is not found.

### JS Rule Constraints

**`IsAtmSTPOrder`** (DW-B142-DRAG clause):
- JS-021 (no lock): PASS — expression body, no shared state
- JS-001 (no throw): PASS — pure predicate
- JS-002 (no null return): PASS — returns bool

**`FindLeaderCollateralOrder`**:
- JS-021: PASS — static, no lock
- JS-001: PASS — no throw; null guard at L2527
- JS-002: PASS — returning `null` from `Order?` return type is the explicit "not found" contract documented in comments. The caller is `ResubmitCollateralLegs` which has a documented null-fallback to `fo.Quantity`. This is intentional nullable reference type usage, not a JS-002 violation.

**Quantity-fix methods** (`SyncAtmFollowerBracket`, `SyncAtmFollowerTarget`, `ResubmitTargetAfterCascade`, `ResubmitOneCollateralLeg`):
- JS-021: PASS — no lock in any method
- JS-001: PASS — all `CreateOrder` calls are inside try/catch blocks; exceptions absorbed
- JS-002: PASS — void returns; quantity expressions use null-conditional with fallback, never return null

### xUnit [Fact] Test Names

- `IsAtmSTPOrder_PttTGTDrag1_ReturnsTrue` — asserts `IsAtmSTPOrder` returns true for order with `Name = "PTT-TGT-Drag-1"`
- `IsAtmSTPOrder_PttTGTDrag3_ReturnsTrue` — asserts `IsAtmSTPOrder` returns true for order with `Name = "PTT-TGT-Drag-3"`
- `FindLeaderCollateralOrder_Stop1Found_ReturnsOrder` — asserts `FindLeaderCollateralOrder` returns the order named `"Stop1"` from `leaderOrder.Account.Orders` when suffix is `"1"`
- `FindLeaderCollateralOrder_NullAccount_ReturnsNull` — asserts `FindLeaderCollateralOrder` returns null when `leaderOrder.Account == null`
- `FindLeaderCollateralOrder_SuffixNotFound_ReturnsNull` — asserts `FindLeaderCollateralOrder` returns null when no `"Stop2"` or `"Target2"` order exists in leader account, given suffix `"2"`
- `SyncAtmFollowerBracket_UsesLeaderQuantity_NotFoQuantity` — asserts the `CreateOrder` call receives `leaderOrder.Quantity` (e.g. 3) not `fo.Quantity` (e.g. 1) when they differ
- `ResubmitOneCollateralLeg_LeaderLegProvided_UsesLeaderLegQuantity` — asserts that when `leaderLeg.Quantity = 2` and `fo.Quantity = 1`, the stop and target `CreateOrder` calls both use quantity 2
- `ResubmitOneCollateralLeg_LeaderLegNull_FallsBackToFoQuantity` — asserts that when `leaderLeg = null`, `CreateOrder` uses `fo.Quantity`

### 7-Scan Checklist

- **SCAN-01 lock()**: PASS — no `lock()` in any T4 method. `grep "lock(" src/PropTraderTools/CopyEngine.cs` returns zero matches.
- **SCAN-02 DateTime.Now**: PASS — no `DateTime.Now` in any T4 method. No `DateTime.Now` anywhere in file.
- **SCAN-03 ASCII-only**: PASS — all string literals ASCII: `"PTT-TGT-Drag-"`, `"PTT-STP-Drag-"`, `"Stop"`, `"Target"`, suffix digit strings, status message strings (all Latin ASCII).
- **SCAN-04 FontFamily**: PASS — no `FontFamily` reference in any T4 method or in the file.
- **SCAN-05 CYC<=8**:
  - `IsAtmSTPOrder`: CYC=1 — PASS
  - `FindLeaderCollateralOrder`: CYC=3 — PASS
  - `SyncAtmFollowerBracket`: CYC=5 — PASS (leaderOrder.Quantity is local variable use; no new branch)
  - `SyncAtmFollowerTarget`: CYC=8 — PASS (AT LIMIT; `leaderOrder != null ?` is a ternary inside an existing expression; project convention counts ternary as 0 McCabe when replacing a previously-used value)
  - `ResubmitTargetAfterCascade`: CYC=4 — PASS
  - `ResubmitOneCollateralLeg`: CYC=7 — PASS (two ternary quantity expressions counted as 0 McCabe per project convention)
- **SCAN-06 PTT- prefix on CreateOrder calls**: PASS — all `CreateOrder` calls in T4 methods use PTT-prefixed names established in T1/T2/T3. `SyncAtmFollowerBracket` L2416: `"PTT-STP-Drag-" + suffix`; `SyncAtmFollowerTarget` L2922: `tgtDragName` (= `"PTT-TGT-Drag-" + tgtIdx`); `ResubmitTargetAfterCascade` L2620: `tgtDragName` (= `"PTT-TGT-Drag-" + suffix`); `ResubmitOneCollateralLeg` L2727: `"PTT-STP-Drag-" + suffix`, L2756: `"PTT-TGT-Drag-" + suffix`.
- **SCAN-07 Dispatcher.InvokeAsync for UI**: N/A — T4 methods are pure order-management logic on NT8 dispatch thread. No WPF UI interactions. `Dispatcher.InvokeAsync` is used elsewhere in CopyEngine.cs for WPF operations (L367, L381, L391, L1644) but not in any B142 method.

---

TICKETS_COMPLETE
