# B135 Architecture Plan

**Epic**: B135 -- Two-Ticket: DW-B146 (second drag fo=null) + DW-B134-OCO (PTT drag orphan sweep)
**Status**: REVIEW_PASS
**Phase**: 1 (Architecture)
**Author**: ptt-architect
**Source block**: B134 SIM PARTIAL (2026-09-07)

---

## A. Epic Summary

B135 fixes two P1 defects observed during the B134 SIM PARTIAL gate run.

| DW ID | Description | Symptom | Root Cause |
|-------|-------------|---------|------------|
| DW-B146 | Second ATM bracket drag -- fo=null on repeated drag | Second drag silent; follower does not sync to new price | `FindFollowerBracketOrder` exact-name guard at L2551 rejects "PTT-TGT-Drag" when leaderName="Target3" |
| DW-B134-OCO | PTT drag orders orphaned after position flat | Working PTT-TGT-Drag / PTT-STP-Drag remain open after ATM natural fill or stop fire | Blue standalone orders not in NT8 OCO group; NT8 cancel-all does not sweep them |

Both defects are in `CopyEngine.cs`. Two surgical changes + two new helper methods. No new files in `src/`. One new test file.

**DW-B141 Phase C**: SIM Test A re-confirmation only. No code fix. See Section J.
**DW-B138**: SIM Test B only. No code fix. See Section J.

---

## B. Source Investigation Results

### B.1 FindFollowerBracketOrder Current State (CYC=8, AT LIMIT)

**Live source**: `src/PropTraderTools/CopyEngine.cs` L2536-2572

```
// CYC=8 (post-B134). AT LIMIT; PASS.
// foreach(1) + SignalOrNameMatches guard(1) + leaderName exact guard(1) + state filter(3)
//            + isStop(1) + type match(1) = 8.
private Order? FindFollowerBracketOrder(
    IEnumerable<Order> orders,
    string? fromEntrySignalName,
    bool isStop,
    string? leaderName = null)
{
    foreach (var order in orders)                                      // (1)
    {
        if (!SignalOrNameMatches(order, fromEntrySignalName, leaderName))  // (1)
            continue;
        if (leaderName != null && order.Name != leaderName)           // (1) -- B134 DW-B145
            continue;
        if (order.OrderState != OrderState.Working                    // (3 conditions)
            && order.OrderState != OrderState.Accepted
            && order.OrderState != OrderState.Submitted)
            continue;
        if (isStop)                                                    // (1)
        {
            if (order.OrderType == OrderType.StopMarket
                || order.OrderType == OrderType.StopLimit)            // (1)
                return order;
        }
        else
        {
            if (order.OrderType == OrderType.Limit && !IsStopLeg(order))
                return order;
        }
    }
    return null;
}
```

**DW-B146 root cause**: Guard at L2551 -- `if (leaderName != null && order.Name != leaderName)` -- rejects "PTT-TGT-Drag" when leaderName="Target3". After the first drag, the original "Target3" was Cancelled and replaced by "PTT-TGT-Drag". On the second drag leaderName="Target3" but only "PTT-TGT-Drag" exists Working on the follower. The guard filters it out → fo=null → sync silent.

**CONSTRAINT**: CYC=8 AT LIMIT. Any branch added inline to `FindFollowerBracketOrder` pushes to CYC=9 (OVER JS limit). Helper extraction `MatchesLeaderName` is MANDATORY before the fix.

### B.2 MatchesLeaderName Helper Design (new -- CYC=5)

**Chosen option**: Option A (explicit PTT-TGT-Drag / PTT-STP-Drag name check).

**Rationale over Option C**: Option C relies on "pre-sweep guarantees at most 1 PTT-order per type", which is architectural assumption rather than an invariant enforced by the type system. Option A is explicit and self-documenting; each match path is a named code branch with clear intent.

**Exact signature**:
```csharp
// B135 DW-B146: PTT-prefix fallback -- after first drag, original ATM bracket is Cancelled;
// replacement is "PTT-TGT-Drag" (target) or "PTT-STP-Drag" (stop).
// FindFollowerBracketOrder must recognize these as the incumbent bracket on repeated drags.
// CYC=5: base(1) + leaderName null(1) + name==(1) + !isStop&&TGT(1) + isStop&&STP(1) = 5.
// JS-021: no lock (static, no shared state). JS-001: no throw. JS-002: returns bool.
// ASCII-only. "PTT-TGT-Drag" and "PTT-STP-Drag" are ASCII.
private static bool MatchesLeaderName(Order order, string? leaderName, bool isStop)
{
    if (leaderName == null)                                           // (1) no constraint -- pass through
        return true;
    if (order.Name == leaderName)                                     // (2) exact ATM name match
        return true;
    if (!isStop && order.Name == "PTT-TGT-Drag")                     // (3) replacement target match
        return true;
    if (isStop && order.Name == "PTT-STP-Drag")                      // (4) replacement stop match
        return true;
    return false;
}
```

**CYC breakdown**: base(1) + guard leaderName==null(1) + guard name==leaderName(1) + guard !isStop&&TGT(1) + guard isStop&&STP(1) = **5**. Well within ≤8.

**Placement**: Immediately after `SignalOrNameMatches` (L2518). Before `FindFollowerBracketOrder` Account overload (L2529). This clusters all name-matching helpers together.

**Test seam**: Add `internal static bool MatchesLeaderNameTestable(Order order, string? leaderName, bool isStop) => MatchesLeaderName(order, leaderName, isStop);` after the definition -- same pattern as `SignalOrNameMatchesTestable` (L2576-2577).

### B.3 SyncAtmFollowerBracket / SyncAtmFollowerTarget Context

**PTT-STP-Drag creation point**: `SyncAtmFollowerBracket` (L2238-2297), Block B at L2271-2284:
```
var newStop = acc.CreateOrder(..., OrderType.StopMarket, ..., "PTT-STP-Drag", ...);
acc.Submit(new[] { newStop });
```
oco param = "" (L2280). PTT-STP-Drag is NOT linked to any NT8 ATM OCO group. Standalone blue order.

**PTT-TGT-Drag creation point**: `SyncAtmFollowerTarget` (L2299-2383), Block B at L2352-2371:
```
var newTarget = acc.CreateOrder(..., OrderType.Limit, ..., "PTT-TGT-Drag", ...);
acc.Submit(new[] { newTarget });
```
oco param = "" (L2360). PTT-TGT-Drag is NOT linked to any NT8 ATM OCO group. Standalone blue order.

**Block A-Prime** in `SyncAtmFollowerTarget` (L2319-2337): pre-sweep cancels any existing Working "PTT-TGT-Drag" before placing a new one. This handles the N+1 drag scenario for target only. After B135 T1 fix, on second drag fo=PTT-TGT-Drag -- A-Prime cancels it (found as Working), Block A cancels again (absorbed by try/catch), Block B places new PTT-TGT-Drag at new price. CORRECT.

**No Block A-Prime equivalent in `SyncAtmFollowerBracket`**: Stop path does not have a pre-sweep for PTT-STP-Drag. This is fine -- the same logic flow applies: fo=PTT-STP-Drag → Block A cancels it → Block B places new one.

### B.4 OnOrderUpdate / Sweep Hook Point

**Hook location**: `OnOrderUpdate` (L1301-1413). The pre-Gate-1 region (before L1369 `_isCopyEnabled` check) contains multiple cleanup helpers:
- L1304: `EvictDedup`
- L1305: `TryLogDragTrace`
- L1309: `TryFireFollowerBeDisarm`
- L1313: `TryFireFollowerBeRetry`
- L1316: `TryEvictFollowerBeSlot`   ← T2 call site: immediately after this, ~L1317

**Call insertion point**: After `TryEvictFollowerBeSlot(e)` at L1316, before the `if (e.Order.OrderState == OrderState.Filled && e.Order.Name != null...)` block at L1326. Inserting here:
```csharp
TrySweptPttDragOrphans(e);
```
This is a call statement -- zero McCabe branches added to `OnOrderUpdate`. `OnOrderUpdate` CYC stays = 8.

**Why pre-Gate-1**: The sweep must fire regardless of `_isCopyEnabled` state. Orphaned orders on follower accounts should be cleaned up even if copying is temporarily disabled. Same rationale as `TryEvictFollowerBeSlot`.

**Method definition location**: Right after `TryEvictFollowerBeSlot` (~L1557). Named group: "cleanup on flat" helpers together.

### B.5 NT8 API Confirmation

| API / Feature | Availability | Source | Notes |
|---------------|-------------|--------|-------|
| `acc.Cancel(Order[])` | AddOnBase: YES | NT8_FULL_REFERENCE.md L2408-2452; NT8_ADDON_KNOWLEDGE.md L222 | No state restriction documented. `UnableToCancelOrder` error absorbed by try/catch. |
| `acc.Orders` iteration | AddOnBase: YES | NT8_ADDON_KNOWLEDGE.md L219 | `.ToList()` safe in OnOrderUpdate callback thread (existing pattern L2322). |
| `Account.PositionUpdate` event | AddOnBase: YES | NT8_FULL_REFERENCE.md L388, L1993-1999 | Available as `acc.PositionUpdate += handler`. NOT chosen as T2 hook (see below). |
| `IsFollowerAccount(Account)` | CopyEngine internal: YES | CopyEngine.cs L1536 (existing usage) | Checks account against `_rules[i].FollowerAccounts`. |
| `HasOpenPosition(Account, Instrument)` | CopyEngine private: YES | CopyEngine.cs L3065-3071 | Returns `pos.Quantity > 0`. AddOn acc.Positions is real-time (no bar-lag caveat -- that is StrategyBase-specific). |
| `IsFlat(FindPosition(...))` | CopyEngine internal: YES | CopyEngine.cs L4002-4004, L4064-4070 | `pos==null || pos.Quantity==0`. Established flat-guard pattern (TryEvictFollowerBeSlot L1538). |

**PositionUpdate NOT chosen as T2 hook**: Existing pattern uses `OnOrderUpdate` (Filled + IsFlat) as established in `TryEvictFollowerBeSlot` (L1524-1556). Adding `PositionUpdate` subscription would require changes to `Subscribe()`/`Unsubscribe()` (L1288-1298) -- excess scope. `OnOrderUpdate` hook is sufficient, proven, and requires zero new subscriptions.

---

## C. Ticket 1 Architecture Decision (DW-B146)

### Decision: Extract MatchesLeaderName + update FindFollowerBracketOrder guard

**Step 1**: Add new `MatchesLeaderName` static method (CYC=5) after `SignalOrNameMatches`.

**Step 2**: Replace guard at L2551-2552:
```csharp
// BEFORE (B134):
if (leaderName != null && order.Name != leaderName)  // B134 DW-B145
    continue;

// AFTER (B135):
if (!MatchesLeaderName(order, leaderName, isStop))   // B135 DW-B146: PTT-prefix fallback
    continue;
```

**Complete post-B135 state of FindFollowerBracketOrder list overload**:
```csharp
// CYC=8 (post-B135). AT LIMIT; PASS.
// foreach(1) + SignalOrNameMatches guard(1) + MatchesLeaderName guard(1) + state filter(3)
//            + isStop(1) + type match(1) = 8.
// DW-B143: Accepted added. DW-B144: Submitted added. DW-B145: leaderName exact guard added.
// DW-B146: MatchesLeaderName extracted to include PTT-prefix fallback (B135).
// JS-021: no lock. JS-001: no throw. JS-002: Order? null contract unchanged.
private Order? FindFollowerBracketOrder(
    IEnumerable<Order> orders,
    string? fromEntrySignalName,
    bool isStop,
    string? leaderName = null)
{
    foreach (var order in orders)                                                    // (1)
    {
        if (!SignalOrNameMatches(order, fromEntrySignalName, leaderName))           // (1)
            continue;
        if (!MatchesLeaderName(order, leaderName, isStop))                          // (1) B135 DW-B146
            continue;
        if (order.OrderState != OrderState.Working                                  // (3)
            && order.OrderState != OrderState.Accepted
            && order.OrderState != OrderState.Submitted)
            continue;
        if (isStop)                                                                  // (1)
        {
            if (order.OrderType == OrderType.StopMarket
                || order.OrderType == OrderType.StopLimit)                          // (1)
                return order;
        }
        else
        {
            if (order.OrderType == OrderType.Limit && !IsStopLeg(order))
                return order;
        }
    }
    return null;
}
```

**Note on double-guard**: `SignalOrNameMatches` and `MatchesLeaderName` are sequential guards with independent semantics:
- `SignalOrNameMatches` (L2511-2518): determines whether the order belongs to the right entry signal or name context. `SignalOrNameMatches` returns TRUE for ALL brackets sharing the same `FromEntrySignal` (when signalName non-null) -- it does NOT disambiguate by PTT-prefix.
- `MatchesLeaderName`: narrows by leader name OR PTT-prefix. Runs only if `SignalOrNameMatches` passed. The interaction is correct: an order passing `SignalOrNameMatches` AND `MatchesLeaderName` is the correct follower bracket candidate.

**`SignalOrNameMatches` unchanged**: CYC=3. No regression risk to existing callers.

### CYC Table

| Method | Pre-B135 CYC | Post-B135 CYC | Limit | Pass? |
|--------|-------------|---------------|-------|-------|
| `FindFollowerBracketOrder` (list overload) | 8 (AT LIMIT) | 8 (AT LIMIT) | 8 | YES -- guard replaced in-kind (1 for 1) |
| `MatchesLeaderName` (new) | -- | 5 | 8 | YES |
| `SignalOrNameMatches` (L2511) | 3 | 3 (unchanged) | 8 | YES |

---

## D. Ticket 2 Architecture Decision (DW-B134-OCO)

### Decision: OnOrderUpdate sweep via TrySweptPttDragOrphans + CancelPttDragOrphansForAccount

**Root cause confirmed**: PTT-TGT-Drag and PTT-STP-Drag are created via `acc.CreateOrder(... oco="" ...)`. The empty OCO string means NT8 does NOT include them in the ATM bracket OCO cancel-all group. When the leader position closes naturally (ATM fill or stop fire), NT8 propagates the close to follower ATM brackets (green orders) via OCO, but the blue PTT-drag orders remain Working.

**Fix**: On every Filled order event in `OnOrderUpdate`, check if the follower account just went flat. If so, cancel any Working PTT-TGT-Drag or PTT-STP-Drag on that account for that instrument.

**Hook method**: `TrySweptPttDragOrphans(OrderEventArgs e)` -- new, CYC=5.
**Cancel worker**: `CancelPttDragOrphansForAccount(Account acc, Instrument instr)` -- new, CYC=5.

**Exact code -- TrySweptPttDragOrphans**:
```csharp
// B135 DW-B134-OCO: sweep orphaned PTT-drag orders when follower position goes flat.
// PTT-TGT-Drag and PTT-STP-Drag are standalone (oco="") -- not in any NT8 ATM OCO group.
// When ATM fills naturally, NT8 only cancels OCO-linked (green) orders; PTT-drag orders survive.
// Fire on Filled + follower + flat -- same pattern as TryEvictFollowerBeSlot (L1538).
// CYC=5: base(1) + o null guard(1) + Filled guard(1) + follower guard(1) + flat guard(1) = 5.
// JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only.
private void TrySweptPttDragOrphans(OrderEventArgs e)
{
    var o = e?.Order;
    if (o == null)                                                    // (1)
        return;
    if (o.OrderState != OrderState.Filled)                           // (2)
        return;
    if (!IsFollowerAccount(o.Account))                               // (3)
        return;
    if (!IsFlat(FindPosition(o.Account, o.Instrument)))              // (4)
        return;
    CancelPttDragOrphansForAccount(o.Account, o.Instrument);
}
```

**Exact code -- CancelPttDragOrphansForAccount**:
```csharp
// B135 DW-B134-OCO: cancel all Working PTT-TGT-Drag and PTT-STP-Drag orders for this account+instrument.
// Called ONLY when position is confirmed flat (TrySweptPttDragOrphans gate).
// acc.Orders.ToList() is safe in OnOrderUpdate callback thread (existing pattern: L2322).
// try/catch: absorbs ErrorCode.UnableToCancelOrder (existing pattern: SyncAtmFollowerBracket Block A).
// CYC=5: base(1) + foreach(1) + state guard(1) + instr guard(1) + name guard(1) = 5.
// JS-021: no lock. JS-001: try/catch -- no throw in hot path. JS-002: void. ASCII-only.
// NT8-014: "PTT-TGT-Drag" and "PTT-STP-Drag" already confirmed as order names (L2362, L2453).
private void CancelPttDragOrphansForAccount(Account acc, Instrument instr)
{
    foreach (var o in acc.Orders.ToList())                           // (1)
    {
        if (o.OrderState != OrderState.Working)                      // (2)
            continue;
        if (o.Instrument?.FullName != instr?.FullName)               // (3)
            continue;
        if (o.Name != "PTT-TGT-Drag" && o.Name != "PTT-STP-Drag")  // (4)
            continue;
        try
        {
            acc.Cancel(new Order[] { o });
            StatusUpdate?.Invoke(acc.Name + ": PTT drag sweep: cancelled " + o.Name);
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke(acc.Name + ": PTT drag sweep cancel error: " + ex.Message);
        }
    }
}
```

**NT8 API confirmation for T2**:
- `acc.Orders.ToList()`: AddOnBase (NT8_ADDON_KNOWLEDGE.md L219). Safe in OrderUpdate callback (L2322 existing pattern). CONFIRMED.
- `acc.Cancel(Order[])`: AddOnBase (NT8_FULL_REFERENCE.md L2408). CONFIRMED.
- `try/catch` absorbs `ErrorCode.UnableToCancelOrder`: existing pattern (SyncAtmFollowerBracket L2259-2266, SyncAtmFollowerTarget L2339-2347). CONFIRMED.
- `IsFlat(FindPosition(...))`: CopyEngine private (L4002-4070). Established flat-guard pattern (TryEvictFollowerBeSlot L1538). CONFIRMED.
- `IsFollowerAccount(acc)`: CopyEngine internal (L1536 existing usage). CONFIRMED.

**Why NOT PositionUpdate**: `PositionUpdate` is available in AddOnBase (NT8_FULL_REFERENCE.md L388, L1993-1999) but would require new subscriptions in `Subscribe()`/`Unsubscribe()` (L1288-1298). The `OnOrderUpdate` (Filled + flat) pattern is established in `TryEvictFollowerBeSlot` and requires zero new subscriptions. PositionUpdate availability is CONFIRMED for future use if needed.

**CYC Table for T2**:

| Method | CYC | Limit | Pass? |
|--------|-----|-------|-------|
| `TrySweptPttDragOrphans` (new) | 5 | 8 | YES |
| `CancelPttDragOrphansForAccount` (new) | 5 | 8 | YES |
| `OnOrderUpdate` (call added) | 8 (unchanged) | 8 | YES -- call adds 0 McCabe |

---

## E. DW-B147 Evaluate / Defer Decision (P2)

**Decision: DEFER DW-B147 to a future block.**

**Analysis**:

| Method | Current CYC | After +1 guard | Limit | Verdict |
|--------|-------------|----------------|-------|---------|
| `SyncAtmFollowerBracket` (L2251) | 4 | 5 | 8 | Safe to add in isolation |
| `SyncAtmFollowerTarget` (L2312) | 8 | 9 | 8 | **OVER LIMIT -- blocks B135** |

`SyncAtmFollowerTarget` is already at CYC=8 (AT LIMIT per L2303-2304 comment). A rawPrice==newPrice early-return guard would push it to CYC=9, violating the JS ceiling. To include DW-B147 for `SyncAtmFollowerTarget` would require a helper extraction (e.g., `IsNoPriceChange(double rawPrice, double newPrice, double tickSize)`) in addition to the guard change -- out of proportion for a P2 improvement with no confirmed symptom.

Since DW-B147 cannot be applied symmetrically to both methods in this block without helper extraction, and neither the B134 SIM PARTIAL nor B135 SIM plan identify a concrete duplicate-sync symptom tied to price equality, this P2 item is deferred.

**Carry-forward**: DW-B147 DEFER to B136 or later. Future block can: (a) extract a `SyncAtmNoPriceChange()` guard helper from `SyncAtmFollowerTarget`, (b) then add the rawPrice==newPrice guard to both methods cleanly.

---

## F. Constraint Compliance Table

| Constraint | Check | Status |
|------------|-------|--------|
| JS-021: no `lock()` anywhere | `MatchesLeaderName` -- static pure predicate, no state. `TrySweptPttDragOrphans` / `CancelPttDragOrphansForAccount` -- `acc.Orders.ToList()` is NT8 lock-free. No `lock()` in any new or modified code. | **PASS** |
| JS-001: no `throw` in hot path | `MatchesLeaderName`: no throw (returns bool). `FindFollowerBracketOrder`: no throw (returns Order?). `TrySweptPttDragOrphans`: no throw (returns void). `CancelPttDragOrphansForAccount`: try/catch -- exception absorbed, no rethrow. | **PASS** |
| JS-002: no bare `return null` | `FindFollowerBracketOrder` `return null` at L2571 preserved unchanged. `MatchesLeaderName` returns bool (no null). No new nullable return paths. | **PASS** |
| CYC ≤ 8 per method | All new/modified methods: see CYC tables in §C and §D. Maximum = 8 (FindFollowerBracketOrder, unchanged). All new methods ≤ 5. | **PASS** |
| ASCII-only identifiers and strings | "PTT-TGT-Drag", "PTT-STP-Drag", "PTT drag sweep", "B135 DW-B146", "B135 DW-B134-OCO" -- all ASCII. No Unicode literals. | **PASS** |
| `acc.Cancel()` wrapped in try/catch | `CancelPttDragOrphansForAccount` wraps each `acc.Cancel()` in try/catch (NT8_FULL_REFERENCE.md L2408 no-state-restriction; `UnableToCancelOrder` error absorbed). | **PASS** |
| CreateOrder PTT- prefix | No new CreateOrder calls in B135. PTT-TGT-Drag and PTT-STP-Drag were created by B134/B132 at L2362 and L2453 respectively. | **N/A** |
| No `DateTime.Now` | No DateTime in new code. | **PASS** |
| No FontFamily / hex colors | No WPF code in scope. | **N/A** |
| `_diagnosticMode` field | Not touched. L412: `private static bool _diagnosticMode = true;` unchanged. | **PASS** |
| `PropTraderTools.csproj` registration | Add `<Compile Include="Tests\B135Tests.cs" />` after L162 (B134Tests.cs entry). | **REQUIRED** |

---

## G. Test Plan

**File**: `src/PropTraderTools/Tests/B135Tests.cs`
**Namespace**: `PropTraderTools`
**Class**: `B135Tests` (top-level, containing nested test classes per ticket)
**Framework**: xUnit only (`[Fact]`). No NUnit, no MSTest.
**Test access**: `MatchesLeaderNameTestable` (internal static testable seam, same pattern as `SignalOrNameMatchesTestable` L2576). `FindFollowerBracketOrderTestable` (list-injection overload, L2589-2594, existing seam from B133).

### Ticket 1 Tests (DW-B146 -- MatchesLeaderName + second drag fix)

| `[Fact]` Name | What It Asserts |
|---------------|-----------------|
| `T1_MatchesLeaderName_NullLeaderName_ReturnsTrue` | When leaderName=null: returns true regardless of order name or isStop (no constraint = pass through) |
| `T1_MatchesLeaderName_ExactName_ReturnsTrue` | When order.Name=="Target3" and leaderName=="Target3": returns true (exact ATM match) |
| `T1_MatchesLeaderName_WrongName_ReturnsFalse` | When order.Name=="Target1" and leaderName=="Target3": returns false (guard works) |
| `T1_MatchesLeaderName_PttTgtDrag_Target_ReturnsTrue` | When order.Name=="PTT-TGT-Drag", leaderName="Target3", isStop=false: returns true (B135 fix) |
| `T1_MatchesLeaderName_PttStpDrag_Stop_ReturnsTrue` | When order.Name=="PTT-STP-Drag", leaderName="Stop1", isStop=true: returns true (B135 fix) |
| `T1_MatchesLeaderName_PttTgtDrag_StopContext_ReturnsFalse` | When order.Name=="PTT-TGT-Drag", leaderName="Stop1", isStop=true: returns false (wrong type) |
| `T1_FindFollower_SecondDrag_ReturnsReplacementTarget` | When follower has only PTT-TGT-Drag Working (original Target3 Cancelled), leaderName="Target3", isStop=false: FindFollowerBracketOrder returns PTT-TGT-Drag (not null) |

**Minimum**: 7 `[Fact]` tests for Ticket 1.

### Ticket 2 Tests (DW-B134-OCO -- orphan sweep)

| `[Fact]` Name | What It Asserts |
|---------------|-----------------|
| `T2_CancelPttDragOrphans_CancelsWorkingTgtDrag` | When acc has Working PTT-TGT-Drag for the instrument, `CancelPttDragOrphansForAccount` calls Cancel on it (uses test double / spy pattern) |
| `T2_CancelPttDragOrphans_CancelsWorkingStpDrag` | When acc has Working PTT-STP-Drag for the instrument, `CancelPttDragOrphansForAccount` calls Cancel on it |
| `T2_CancelPttDragOrphans_IgnoresNonPttOrders` | When acc has Working native ATM "Target3" (non-PTT), `CancelPttDragOrphansForAccount` does NOT attempt to cancel it |
| `T2_TrySwept_PartialFill_NotFlat_DoesNotSweep` | When order is Filled but position qty > 0 (not flat), `TrySweptPttDragOrphans` returns without calling `CancelPttDragOrphansForAccount` -- validates `IsFlat` guard at gate branch (4) |
| `T2_CancelPttDragOrphans_ExceptionAbsorbed_NoRethrow` | When `acc.Cancel` throws, `CancelPttDragOrphansForAccount` absorbs the exception via try/catch and does not propagate it -- validates the `UnableToCancelOrder` absorption path |

**Note**: `TrySweptPttDragOrphans` integration path is exercised via the OrderEventArgs gate checks. The above tests target `CancelPttDragOrphansForAccount` directly (if test seam is available) or via behavioral assertion on Cancel call side-effect.

**Test seam required**: Add `internal void CancelPttDragOrphansForAccountTestable(Account acc, Instrument instr) => CancelPttDragOrphansForAccount(acc, instr);` after the definition.

**Minimum**: 5 `[Fact]` tests for Ticket 2.

**Total B135 tests**: 12 minimum.

---

## H. Files Changed

| File | Change Type | Description |
|------|-------------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | MODIFY | (1) NEW `MatchesLeaderName` private static method (~10 lines) at ~L2520, after `SignalOrNameMatches`. (2) NEW `MatchesLeaderNameTestable` internal static test seam (~2 lines) after `MatchesLeaderName`. (3) MODIFY `FindFollowerBracketOrder` list overload: replace L2551-2552 guard with `MatchesLeaderName` call; update CYC comment at L2536-2539. (4) NEW call `TrySweptPttDragOrphans(e)` in `OnOrderUpdate` at ~L1317 (after `TryEvictFollowerBeSlot`). (5) NEW `TrySweptPttDragOrphans` private method (~10 lines) at ~L1558. (6) NEW `CancelPttDragOrphansForAccount` private method (~15 lines) immediately after `TrySweptPttDragOrphans`. (7) NEW `CancelPttDragOrphansForAccountTestable` internal test seam (~2 lines). |
| `src/PropTraderTools/Tests/B135Tests.cs` | NEW | 10 xUnit `[Fact]` tests across two test classes (B135Ticket1Tests and B135Ticket2Tests) |
| `src/PropTraderTools/PropTraderTools.csproj` | MODIFY | Add `<Compile Include="Tests\B135Tests.cs" />` after L162 (B134Tests.cs entry) |

**Files NOT touched**: `SignalOrNameMatches`, `SyncFollowerBracket`, `SyncAtmFollowerTarget`, `SyncAtmFollowerBracket`, `DeriveLeaderBracketIndex`, `FindLeaderStopPrice`, `CreateFollowerReplacementStop`, any B129-B134 test file.

---

## I. Prior Regression Guard

The ptt-engineer MUST run all prior test suites before reporting Ticket completion:

```
B134Tests.cs  -- expect 8 PASS (0 FAIL)
B133Tests.cs  -- expect 10 PASS (0 FAIL)
B132Tests.cs  -- expect 6 PASS (0 FAIL)
B131Tests.cs  -- expect 7 PASS (0 FAIL)
B130Tests.cs  -- expect 8 PASS (0 FAIL)
B129Tests.cs  -- expect 13 PASS (0 FAIL)
```

Total prior tests: 52. All 52 must remain PASS.

**Regression risk assessment**: LOW.
- T1 change: `MatchesLeaderName` replaces a guard that was checking `(leaderName != null && order.Name != leaderName)`. The new helper is strictly MORE permissive (allows PTT-prefix matches in addition to exact name match). No existing test passes an order named "PTT-TGT-Drag" with a non-matching leaderName, so no existing test should flip to FAIL.
- T2 change: New method `TrySweptPttDragOrphans` is called from `OnOrderUpdate` but its gate conditions (Filled + follower + flat) are narrow. No existing test exercises this path.
- `SignalOrNameMatches` is unchanged: B133Tests (which tests this helper) is unaffected.

---

## J. Deferred Items Carried Forward (from B134/06-deferred-backlog.md + new items)

| ID | Title | Priority | Target Block | Status | Notes |
|----|-------|----------|--------------|--------|-------|
| B135-DEFER-01 | Gap B runtime -- two simultaneous entries, cancel first, verify 2nd copied | P1 | B136+ | OPEN | Carry from B133-DEFER-01 / B134-DEFER-01 chain |
| B135-DEFER-02 | Stale orders multi-session -- FindFollowerBracketOrder may match prior-session orders | P2 | future | OPEN | Carry from B133-DEFER-02 / B134-DEFER-02 chain |
| DW-B141 | Phase C re-confirmation: SIM Test A only (no code fix) | P1 | B135 SIM | OPEN | After B135 T1 fix, director runs SIM Test A: drag leader target, verify follower sync Phase C. Not a ticket. |
| DW-B138 | Follower stop drag confirmed: SIM Test B only (no code fix) | P1 | B135 SIM | OPEN | Director runs SIM Test B: drag leader stop, verify follower stop syncs. Not a ticket. |
| DW-B134-OCO OBS-A | Cancel races partial fill -- cancel may be rejected with UnableToCancelOrder after partial fill | P1 | future | OPEN | T2 sweep (B135) fixes orphan-after-flat but does NOT fix the partial-fill race window. OBS-A remains open. |
| DW-B134-OCO OBS-B | Replacement order duplicates partially-filled quantity -- follower over-positioned | P1 | future | OPEN | Not addressed by T2 sweep. Requires quantity-aware cancel guard in SyncAtmFollowerTarget. |
| DW-B134-OCO OBS-C | Stop side not cancelled before target replacement | P1 | future | OPEN | Not addressed by B135. Separate from sweep. |
| DW-B134-OCO OBS-D | Net position drift on two-leg partial fill | P1 | future | OPEN | Not addressed by B135. Requires SIM data. |
| DW-B147 | SyncAtmFollowerBracket/SyncAtmFollowerTarget rawPrice==newPrice early-return guard | P2 | B136+ | DEFERRED | `SyncAtmFollowerTarget` at CYC=8; adding guard pushes to CYC=9 (OVER LIMIT). Requires helper extraction in a dedicated block. See Section E. |

---

## K. LANE-SPLIT GATE RESULT

**LANE-SPLIT GATE RESULT: LANES-APPROVED**

Rationale (answers to all four mandatory questions):

**Q1. T1 (FindFollowerBracketOrder ~L2551) and T2 (orphan sweep hook point ~L1317 call / ~L1558 definition) -- same method or within 50 lines?**
Answer: **NO**. T1 fix is at L2551 (FindFollowerBracketOrder list overload). T2 call site is at ~L1317 (OnOrderUpdate) and T2 definition is at ~L1558 (TrySweptPttDragOrphans). Distance between T1 and T2 definition: ~993 lines. Distance between T1 and T2 call site: ~1234 lines. Not within 50 lines. Not in the same method.

**Q2. Does T2 fix design depend on T1 final design?**
Answer: **NO**. T2 operates on `OrderState.Filled` + flat-position events in `OnOrderUpdate`. T2 does not call `MatchesLeaderName`, does not call `FindFollowerBracketOrder`, and does not depend on any state introduced or changed by T1. T1 and T2 touch completely independent code regions.

**Q3. Does each fix have standalone value if the other is blocked?**
Answer: **YES**. T1 alone fixes the second-drag silent-fail symptom (fo=null → no sync on repeated drags). T2 alone fixes the orphaned-order symptom (Working PTT-drag orders surviving after position flat). Each is independently shippable.

**Q4. Does each fix have an independent SIM verification path?**
Answer: **YES**. T1 SIM: drag leader target twice; observe follower syncs to second price (T1 [Fact] `T1_FindFollower_SecondDrag_ReturnsReplacementTarget` provides unit coverage). T2 SIM: let ATM fill naturally; observe NT8 order list shows no Working PTT-TGT-Drag or PTT-STP-Drag on follower after flat (independent NT8 grid check).

All four questions answered in the LANES-APPROVED direction. **LANES-APPROVED**.

---

## Summary of Architectural Decisions

1. **DW-B146**: Helper extraction `MatchesLeaderName` (CYC=5) replaces the inline leaderName guard at L2551. Extraction is MANDATORY because `FindFollowerBracketOrder` is at CYC=8 AT LIMIT. `MatchesLeaderName` returns true on exact name match OR PTT-TGT-Drag (target context) OR PTT-STP-Drag (stop context). `FindFollowerBracketOrder` CYC stays = 8 (in-kind guard replacement). **Option A chosen**.

2. **DW-B134-OCO**: Two new methods: `TrySweptPttDragOrphans` (CYC=5, gate method) + `CancelPttDragOrphansForAccount` (CYC=5, cancel worker). Called from `OnOrderUpdate` pre-Gate-1 (after `TryEvictFollowerBeSlot`). Fires on Filled + follower + flat. Cancels Working PTT-TGT-Drag and PTT-STP-Drag for the instrument. Each cancel wrapped in try/catch. `OnOrderUpdate` CYC stays = 8.

3. **DW-B147**: DEFERRED. `SyncAtmFollowerTarget` at CYC=8; guard would push to CYC=9. Helper extraction needed in a dedicated future block.

4. **Tests**: 12 new `[Fact]` tests (7 T1 + 5 T2). All prior 52 tests must remain green.

5. **NT8 threading**: All new code runs on the NT8 OrderUpdate callback thread. No `lock()`. No `Dispatcher.InvokeAsync`. `acc.Orders.ToList()` and `acc.Cancel()` are established safe patterns.

---

*Plan produced by ptt-architect, B135 Phase 1. Awaiting ptt-plan-reviewer.*
