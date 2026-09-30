# B67-LaneB Architecture Plan

**Block**: B67-LaneB
**Written by**: ptt-architect (Phase 1)
**Date**: 2026-08-13
**Status**: PLAN_COMPLETE

---

## 1. Problem Statement

**DW-B67-02 (P0)**: `HandleEntryChange` calls `acc.Change()` to sync a follower pre-fill
entry order price when the leader drags their entry. On Apex/Rithmic, `acc.Change()` on a
Working pre-fill limit/StopLimit entry order is a **SILENT NO-OP**. NT8 returns "Change
submitted" but the broker ignores the price change.

Confirmed in live trading 2026-08-12: leader dragged 7821.75 -> 7818; follower stayed at
7821.75. The try/catch in `HandleEntryChange` (lines 1076-1086) swallowed the failure
silently — `acc.Change()` did not throw, it just had no effect at the broker.

**Root cause** (confirmed): Apex/Rithmic rejects `acc.Change()` on pre-fill entry orders
at the broker layer without surfacing an error to NT8.

**Fix**: Replace the `acc.Change()` call in `HandleEntryChange` with cancel + CreateOrder
+ Submit. This is the only cross-broker-safe path for moving a working pre-fill entry order.

---

## 2. NT8 Ground Truth

| Reference | Fact |
|-----------|------|
| NT8_FULL_REFERENCE.md line 893 | `StopPrice` = double value representing the stop price of an order |
| NT8_FULL_REFERENCE.md lines 898-899 | `StopPriceChanged` = new stop price for `Account.Change()` — confirms StopLimit price lives in `StopPrice`, not `LimitPrice` |
| NT8_FULL_REFERENCE.md (AGENTS.md mandate) | `CreateOrder()` requires explicit `Submit()` — the two calls are separate |
| @2Custom `PropagateMasterEntryMove` docstring | "`Account.Change()` removed — it completes silently on Apex/Tradovate but is a broker-side no-op. Cancel + CreateOrder + Submit is the sole path." (FIX-PM-02 and FIX-PM-02b) |
| NT8_FULL_REFERENCE.md Fact 1 | `StopLimit.LimitPrice == 0` always; the drag price lives in `StopPrice` |

**`acc.Change()` on bracket stops IS SAFE — these methods MUST NOT be touched:**

| Method | Location | Why safe |
|--------|----------|---------|
| `SyncFollowerBracket` | ~line 942 | Post-fill bracket stop/target; Working state; broker processes Change() on post-fill orders |
| `MoveStopToBreakEven` | ~line 1797 | Working stop order (post-fill); confirmed working |
| `TightenOneStop` | ~line 1866 | Working stop order (post-fill); confirmed working |

The Apex/Rithmic no-op only affects pre-fill entry orders in `HandleEntryChange`. All
three post-fill bracket methods are outside scope.

---

## 3. Scope

### IN SCOPE

- Replace `acc.Change()` inside the `HandleEntryChange` try-block with:
  cancel + `CreateOrder` + `Submit` pattern
- Remove `SetFollowerPrice(fo, newPrice)` call (no longer needed — price passed directly
  via `CreateOrder` parameters)
- Add `_dedupCache.TryRemove(leaderOrder.OrderId.ToString(), out _)` after cancel+resubmit
  per spec section 4d
- Update `HandleEntryChange` method comment block: remove `acc.Change()` reference, add
  citations to DW-B67-02, @2Custom FIX-PM-02, NT8_FULL_REFERENCE.md lines 898-899, CYC=7
- 5 new xUnit `[Fact]` tests `T_B67_B_01..T_B67_B_05` in `CopyEngineTests.cs` after
  T_B66_07 (insertion line 3342)
- SHA-256 manual deploy to NinjaTrader `bin/Custom` directory

### OUT OF SCOPE (DO NOT TOUCH)

- `SyncFollowerBracket` `acc.Change()` calls — post-fill bracket stops, confirmed working
- `MoveStopToBreakEven` `acc.Change()` — Working stop, confirmed working
- `TightenOneStop` `acc.Change()` — Working stop, confirmed working
- `DispatchCopy` Gate 5 dedup (DW-B66-C-02 — separate block B67+)
- `GetOrderPrice` helper — unchanged
- `SetFollowerPrice` helper — method body unchanged (not called from `HandleEntryChange`
  after this block; may be referenced by future callers or removed in a cleanup block)
- `FindFollowerEntryOrder` helper — unchanged
- Any other file in `src/PropTraderTools/` besides `CopyEngine.cs` and `CopyEngineTests.cs`

---

## 4. Design Decision: Cancel + CreateOrder + Submit Pattern

### 4a. Why Cancel First

Apex/Rithmic broker rejects `acc.Change()` on pre-fill entry orders silently. NT8 returns
"Change submitted" with no error, but the broker ignores the price update. The only
reliable cross-broker path is: cancel the stale order, create a new order at the new price,
submit it. This pattern is confirmed by @2Custom `PropagateMasterEntryMove` FIX-PM-02.

### 4b. CreateOrder Parameter Mapping (NT8 API)

Source: NT8_FULL_REFERENCE.md + @2Custom FIX-PM-02.

```
acc.CreateOrder(
    fo.OrderAction,       // preserved — Buy or Sell
    fo.OrderType,         // preserved — Limit or StopLimit
    OrderEntry.Manual,    // always Manual for AddOn-submitted orders
    fo.TimeInForce,       // preserved — Day or Gtc
    fo.Quantity,          // preserved
    limitPrice:  fo.OrderType == OrderType.StopLimit ? 0 : newPrice,
                          // StopLimit: LimitPrice=0 (NT8 Fact 1)
                          // Limit:     LimitPrice=newPrice
    stopPrice:   fo.OrderType == OrderType.StopLimit ? newPrice : 0,
                          // StopLimit: StopPrice=newPrice (NT8 lines 898-899)
                          // Limit:     StopPrice=0
    oco:         null,
    name:        fo.Name, // preserves "PTT-Copy" prefix (PTT- rule)
    gtd:         DateTime.MaxValue,
    templateName:null
)
```

**limitPrice/stopPrice logic** (confirmed by NT8 ground truth):
- `StopLimit` orders: `LimitPrice == 0` always; price lives in `StopPrice`. Pass
  `limitPrice: 0, stopPrice: newPrice`.
- `Limit` orders: `StopPrice == 0`; price lives in `LimitPrice`. Pass
  `limitPrice: newPrice, stopPrice: 0`.

### 4c. Submit

```csharp
if (order != null)            // CYC branch 7 — null guard (CreateOrder can return null)
    acc.Submit(new Order[] { order });
```

`CreateOrder` returns null if the broker rejects the order locally (e.g., account not
connected). The null guard is required and becomes CYC branch 7.

### 4d. _dedupCache Stale Key Removal

After cancel+resubmit, the old follower orderId is being cancelled. The `_dedupCache`
update at line 1061 (`_dedupCache[leaderOrder.OrderId.ToString()] = newPrice`) remains as-is
to track the latest leader price for subsequent drag events. After the cancel+resubmit
inside the follower loop, remove the stale key:

```csharp
_dedupCache.TryRemove(leaderOrder.OrderId.ToString(), out _);
// New follower order will be re-keyed by DispatchCopy on its Accepted event.
// Do NOT insert newPrice under the old key after cancel+resubmit.
```

`ConcurrentDictionary.TryRemove` is atomic and idempotent — compliant with JS-021 (no lock).

### 4e. CYC Count = 7

| Branch | Code location |
|--------|---------------|
| (1) | `if (instrument == null) return;` |
| (2) | `tickSize > 0 ? Math.Round(...) * tickSize : rawPrice` (ternary) |
| (3) | `foreach (var acc in rule.FollowerAccounts)` (loop) |
| (4) | `if (acc == null) continue;` |
| (5) | `if (fo == null) continue;` |
| (6) | `if (tickSize > 0 && Math.Abs(newPrice - currentPrice) < tickSize) continue;` |
| (7) | `if (order != null) acc.Submit(...)` (new — CreateOrder null guard) |

Total branches: **7**. Within CYC <= 8 threshold (Jane Street strict standard). PASS.

---

## 5. Method Comment Block Update

The updated `HandleEntryChange` comment block MUST:

- **Remove** all references to `acc.Change()` and `SetFollowerPrice`
- **Add** citation: `DW-B67-02: acc.Change() is Apex/Rithmic no-op on pre-fill entry
  orders — cancel+CreateOrder+Submit is the sole cross-broker path.`
- **Add** citation: `@2Custom PropagateMasterEntryMove FIX-PM-02: cancel+resubmit pattern
  confirmed.`
- **Add** citation: `NT8_FULL_REFERENCE.md lines 898-899: StopLimit price in StopPrice;
  CreateOrder params: limitPx=0/newPrice, stopPx=newPrice/0 per OrderType.`
- **Update** CYC note: `CYC=7: instr null (1), tickSize ternary (2), foreach acc (3),
  acc null (4), fo null (5), price delta guard (6), order null guard (7).`
- **Keep**: `JS-021: no lock -- _dedupCache is ConcurrentDictionary (lock-free).`
- **Keep**: `JS-001: try/catch around cancel+CreateOrder+Submit -- no throw in hot path.`
- **Keep**: `JS-002: void return.`

---

## 6. Test Plan (5 [Fact] Tests)

**File**: `tests/PropTraderTools.Tests/CopyEngineTests.cs`
**Insertion point**: after `T_B66_07` (line 3342), before closing braces at lines 3349-3350.
**All test names ASCII-only.**

| Test ID | Method Name | Verifies |
|---------|-------------|---------|
| T_B67_B_01 | `HandleEntryChange_calls_Cancel_not_Change` | `acc.Cancel` is called with the follower order; `acc.Change` is NOT called |
| T_B67_B_02 | `HandleEntryChange_calls_CreateOrder_with_newPrice` | `CreateOrder` is called with `limitPx = newPrice` for `OrderType.Limit` |
| T_B67_B_03 | `HandleEntryChange_StopLimit_uses_StopPrice` | `CreateOrder` is called with `stopPx = newPrice`, `limitPx = 0` for `OrderType.StopLimit` |
| T_B67_B_04 | `HandleEntryChange_price_within_tick_noOp` | No `Cancel`, no `CreateOrder` when `Math.Abs(newPrice - currentPrice) < tickSize` |
| T_B67_B_05 | `HandleEntryChange_null_follower_order_skip` | No `Cancel`, no `CreateOrder` when `FindFollowerEntryOrder` returns null |

### Test Design Notes

- Tests use the existing mock infrastructure (`FakeAccount`, `FakeOrder`) already established
  in `CopyEngineTests.cs` for prior blocks.
- T_B67_B_01: Assert `FakeAccount.CancelCalled == true` AND `FakeAccount.ChangeCalled == false`.
- T_B67_B_02: Assert `FakeAccount.CreateOrderArgs.LimitPrice == newPrice` and
  `FakeAccount.CreateOrderArgs.StopPrice == 0`.
- T_B67_B_03: Assert `FakeAccount.CreateOrderArgs.StopPrice == newPrice` and
  `FakeAccount.CreateOrderArgs.LimitPrice == 0`.
- T_B67_B_04: Set `tickSize = 0.25`, `leaderPrice = 7820.00`, `followerPrice = 7820.25` —
  delta = 0.25 = 1 tick, which is NOT less than tickSize, so adjust: set `followerPrice =
  7820.10` — delta = 0.10 < 0.25 tick. Assert nothing is called.
- T_B67_B_05: Configure `FakeAccount.Orders` to return empty list so
  `FindFollowerEntryOrder` returns null. Assert neither Cancel nor CreateOrder is called.

---

## 7. Scan Checklist (7 Scans)

| Scan | Description | Target | Tool |
|------|-------------|--------|------|
| S1 | `lock(` in new/changed code | 0 matches | `grep -n "lock(" CopyEngine.cs` restricted to `HandleEntryChange` region |
| S2 | `throw new` in new/changed code | 0 matches | `grep -n "throw new" CopyEngine.cs` restricted to `HandleEntryChange` region |
| S3 | `acc.Change` in `HandleEntryChange` | 0 matches | `grep -n "acc.Change" CopyEngine.cs` restricted to `HandleEntryChange` region |
| S4 | CYC of `HandleEntryChange` = 7 | CYC == 7 | `python scripts/complexity_audit.py --method HandleEntryChange` |
| S5 | Non-ASCII chars in new/changed code | 0 matches | `grep -Pn "[^\x00-\x7F]" CopyEngine.cs` restricted to new lines |
| S6 | Build passes | 0 errors | `dotnet build src/PropTraderTools/PropTraderTools.csproj` |
| S7 | Tests pass | 5/5 T_B67_B_* pass | `dotnet test --filter "T_B67_B"` |

---

## 8. Deploy Step

Manual SHA-256 copy after commit. **deploy-sync.ps1 is archived; manual copy is the
current PropTraderTools deploy workflow** (PRE-EXISTING-03 from B66-LaneC backlog).

```
Source:      C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
Destination: C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs
```

Verification:
```powershell
$src = Get-FileHash "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Algorithm SHA256
$dst = Get-FileHash "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs" -Algorithm SHA256
Write-Host "SRC: $($src.Hash)"
Write-Host "DST: $($dst.Hash)"
if ($src.Hash -eq $dst.Hash) { Write-Host "MATCH -- deploy verified" } else { Write-Host "MISMATCH -- copy failed" }
```

Both hashes MUST match. Report SHA-256 hash in `ticket-1-completion.md`.

---

## 9. Deferred Backlog Impact

| ID | Item | Status after B67-LaneB |
|----|------|----------------------|
| DW-B67-02 | HandleEntryChange acc.Change() Apex/Rithmic no-op | **CLOSED by this block** |
| DW-B66-C-02 | DispatchCopy Gate 5 dedup key = 0.0 for StopLimit | OPEN — not touched |
| DW-B66-BE-01 | CancelQxBrackets PTT-BE-Stop on Quick Exit | OPEN — not touched |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 | OPEN — not touched |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | OPEN — not touched |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines ~1449-1450 | OPEN — not touched |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; manual PropTraderTools deploy | OPEN — acknowledged in deploy step |

---

## 10. Pre-Flight Violations (Sequential Thinking Summary)

All 8 sequential thinking thoughts completed. No violations found:

| Check | Result |
|-------|--------|
| JS-021 no lock() | PASS — ConcurrentDictionary.TryRemove is lock-free |
| JS-001 no throw in hot path | PASS — try/catch wraps all NT8 calls |
| JS-002 no return null | PASS — HandleEntryChange is void |
| JS-033 no async void | PASS — method is synchronous void |
| CYC <= 8 | PASS — CYC = 7 (7 branches) |
| ASCII-only | PASS — all new strings ASCII |
| PTT- prefix | PASS — fo.Name preserved ("PTT-Copy") |
| DateTime.Now ban | PASS — gtd = DateTime.MaxValue |
| .cs file ban on ptt-architect | PASS — plan document only, no .cs written |
| NT8 API verified | PASS — acc.Cancel, acc.CreateOrder, acc.Submit confirmed in NT8_FULL_REFERENCE.md |
| File split | PASS — 2 files only: CopyEngine.cs + CopyEngineTests.cs |
