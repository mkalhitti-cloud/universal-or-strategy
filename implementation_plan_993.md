# Build 993: Master Account Hydration & CANCEL_ALL Repair

**Status**: PENDING ENGINEER EXECUTION
**Author**: ARCHITECT (Claude / Sonnet 4.6)
**Date**: 2026-03-16
**Phase**: 5.6 -- Post-Restart Master Bracket Cleanup

---

## Problem Statement

After strategy disable/re-enable in NinjaTrader while holding an active position with working
Stop_/T_ bracket orders on the Master account (Sim101):

1. `HydrateExpectedPositionsFromBroker()` skips master -> `expectedPositions[master]` = 0
2. `HydrateWorkingOrdersFromBroker()` skips master -> no broker handles for master brackets
3. REAPER fires CRITICAL DESYNC (Expected=0, Actual=9) -- false alarm
4. FLATTEN_ONLY + CANCEL_ALL succeeds for fleet, cancels ZERO master brackets
5. Orphaned Stop_/T1_/T2_ remain at broker requiring manual cancellation

## Evidence From Live Test Logs

```text
[SIMA HYDRATE] SimApexSim_02: Seeded expected=9 from broker (Long 9)
-- Sim101/Master is MISSING (no fleet match)

[REAPER] Sim101 (Master): Expected=0, Actual=9
[REAPER] CRITICAL DESYNC on Sim101 (Master): Expected=0, Actual=9

[SIMA] CANCEL_ALL -> Cancelled 0 orders (Entries + Orphaned Brackets) (local + fleet) [992]
```

## Root Cause

Both hydration functions use `if (!IsFleetAccount(acct)) continue` -- master is excluded.
CANCEL_ALL master loop uses `CancelOrder(order)` which cannot cancel previous-session orders.

**Reference design**: `AuditApexPositions()` correctly handles this with a separate
`AuditMasterAccountIfNeeded()` block after the fleet loop. Hydration must follow the same pattern.

**Reference cancellation**: `ProcessReaperFlattenQueue()` uses `targetAcct.Cancel(ordersToCancel)`
even for master (when `targetAcct == this.Account`). CANCEL_ALL must match this.

---

## Changes

### Change 1: HydrateExpectedPositionsFromBroker -- Add Master

**File**: `src/V12_002.SIMA.Lifecycle.cs`
**Location**: After the closing `}` of the fleet loop (after line 193 -- after the
`if (hydratedCount > 0)` block), before the closing `}` of the method.

Add this block immediately before the method's closing brace:

```csharp
// Build 993: Hydrate master account (mirrors AuditMasterAccountIfNeeded pattern).
// IsFleetAccount excludes master -- must be handled separately, same as REAPER audit.
bool masterIsFleet993 = IsFleetAccount(Account);
if (!masterIsFleet993)
{
    try
    {
        foreach (Position pos in Account.Positions.ToArray())
        {
            if (pos != null && pos.Instrument?.FullName == Instrument.FullName
                && pos.MarketPosition != MarketPosition.Flat)
            {
                int qty = pos.MarketPosition == MarketPosition.Long ? pos.Quantity : -pos.Quantity;
                var capturedQty993 = qty;
                Enqueue(ctx => ctx.AddOrUpdateExpectedPosition(ExpKey(Account.Name), capturedQty993, v => capturedQty993));
                Print(string.Format("[SIMA HYDRATE] {0} (Master): Seeded expected={1} from broker ({2} {3})",
                    Account.Name, qty, pos.MarketPosition, pos.Quantity));
                hydratedCount++;
                break;
            }
        }
    }
    catch (Exception ex)
    {
        Print(string.Format("[SIMA HYDRATE] WARNING: Could not read positions for {0} (Master): {1}",
            Account.Name, ex.Message));
    }
}
```

### Change 2: HydrateWorkingOrdersFromBroker -- Add Master Bracket Adoption

**File**: `src/V12_002.SIMA.Lifecycle.cs`
**Location**: After the fleet loop's closing `}` (after line 324 -- after the fleet catch block),
BEFORE the call to `HydrateFSMsFromWorkingOrders()`.

Add this block immediately before `HydrateFSMsFromWorkingOrders()`:

```csharp
// Build 993: Adopt master account bracket orders (mirrors fleet loop; no FSM creation for master).
// IsFleetAccount excludes master -- must be handled separately.
bool masterIsFleetForOrders993 = IsFleetAccount(Account);
if (!masterIsFleetForOrders993)
{
    try
    {
        foreach (Order ord in Account.Orders.ToArray())
        {
            if (ord.Instrument?.FullName != Instrument?.FullName) continue;
            if (ord.OrderState != OrderState.Working    &&
                ord.OrderState != OrderState.Accepted   &&
                ord.OrderState != OrderState.Submitted  &&
                ord.OrderState != OrderState.ChangePending &&
                ord.OrderState != OrderState.ChangeSubmitted) continue;

            string name = ord.Name ?? string.Empty;
            ConcurrentDictionary<string, Order> targetDict = null;
            string key  = null;
            string dictName = null;

            if (name.StartsWith("Stop_", StringComparison.OrdinalIgnoreCase))
            { targetDict = stopOrders;   key = name.Substring(5); dictName = "stopOrders"; }
            else if (name.StartsWith("S_", StringComparison.OrdinalIgnoreCase))
            { targetDict = stopOrders;   key = name.Substring(2); dictName = "stopOrders"; }
            else if (name.StartsWith("T1_", StringComparison.OrdinalIgnoreCase))
            { targetDict = target1Orders; key = name.Substring(3); dictName = "target1Orders"; }
            else if (name.StartsWith("T2_", StringComparison.OrdinalIgnoreCase))
            { targetDict = target2Orders; key = name.Substring(3); dictName = "target2Orders"; }
            else if (name.StartsWith("T3_", StringComparison.OrdinalIgnoreCase))
            { targetDict = target3Orders; key = name.Substring(3); dictName = "target3Orders"; }
            else if (name.StartsWith("T4_", StringComparison.OrdinalIgnoreCase))
            { targetDict = target4Orders; key = name.Substring(3); dictName = "target4Orders"; }
            else if (name.StartsWith("T5_", StringComparison.OrdinalIgnoreCase))
            { targetDict = target5Orders; key = name.Substring(3); dictName = "target5Orders"; }

            if (targetDict == null || key == null) continue;

            targetDict[key] = ord;
            adoptedCount++;
            Print(string.Format("[SIMA HYDRATE] {0} (Master): Adopted {1} -> {2}[{3}]",
                Account.Name, name, dictName, key));
        }
    }
    catch (Exception ex)
    {
        Print(string.Format("[SIMA HYDRATE] WARNING: Could not adopt orders for {0} (Master): {1}",
            Account.Name, ex.Message));
    }
}
```

**NOTE**: No `activePositions` struct and no FSM creation for master. Master does not use the
follower FSM path. Order adoption only.

### Change 3: CANCEL_ALL Master Loop -- Use Account.Cancel (match REAPER pattern)

**File**: `src/V12_002.UI.IPC.Commands.Fleet.cs`
**Location**: Line 122 -- the `CancelOrder(order);` inside the master loop.

**Old**:

```csharp
CancelOrder(order);
cancelled++;
```

**New**:

```csharp
// Build 993: Use Account.Cancel() -- CancelOrder() cannot cancel orders from previous strategy instance.
// Mirrors ProcessReaperFlattenQueue() which uses targetAcct.Cancel() for all accounts including master.
Account.Cancel(new[] { order });
cancelled++;
```

### Change 4: BUILD_TAG = "993"

**File**: `src/V12_002.cs`
**Location**: Line 44

```csharp
public const string BUILD_TAG = "993";  // V12.993: Phase 5.6 Master Account Hydration & CANCEL_ALL Repair
```

---

## Logical Proof

### Scenario A: Restart with Live Position -> Flatten (the failure case)

| Step | State Before | Action | State After |
|------|-------------|--------|-------------|
| Restart | Sim101: 9L + Stop_/T1_/T2_ Working | Strategy enables | Hydrate runs |
| Hydrate Pos | expectedPositions[master]=0 | Change 1 seeds from broker | expectedPositions[master]=9 |
| Hydrate Orders | stopOrders/target1Orders/target2Orders empty | Change 2 adopts | dicts populated with handles |
| REAPER fires | Expected=9, Actual=9 | No desync | No alert. Heartbeat only. |
| FLATTEN_ONLY | expectedPositions=9, brackets Working | ClosePositionsOnlyApexAccounts() | expectedPositions=0, market close submitted |
| CANCEL_ALL | chkQty=0 (zeroed by FLATTEN_ONLY) | Change 3: Account.Cancel(new[]{order}) | Stop_/T1_/T2_ CANCELLED |
| Market fill | Position closes | Broker confirms flat | expectedPositions=0, actual=0 |
| REAPER | Expected=0, Actual=0 | No desync | Heartbeat: flat |

**Result**: Zero orphaned brackets. PASS.

### Scenario B: Flat Master + CANCEL_ALL (safety check)

| Step | State | Action | Result |
|------|-------|--------|--------|
| Restart (flat) | No position | Hydrate: no positions found | expectedPositions[master]=0 (unchanged) |
| FLATTEN_ONLY | No positions | ClosePositionsOnlyApexAccounts: nothing to close | No-op |
| CANCEL_ALL | chkQty=0 | No Working bracket orders exist | cancelled=0. Safe. |

**Result**: No unintended cancellations. PASS.

### Scenario C: Live Master (no restart) + FLATTEN_ONLY + CANCEL_ALL (Build 992 regression check)

| Step | State | Action | Result |
|------|-------|--------|--------|
| Normal run | expectedPositions[master]=9 (set at entry) | No restart | Brackets own session |
| FLATTEN_ONLY | expectedPositions=9 | Direct SetExpectedPositionLocked(master, 0) | expectedPositions=0 |
| CANCEL_ALL | chkQty=0 | Account.Orders has brackets (current session) | Account.Cancel() works |

**Result**: Brackets cancelled. PASS. (Change 3 is safe for this path too.)

---

## Self-Audit Checklist (Engineer Must Run)

| Check | Command | Expected |
|-------|---------|----------|
| 1. No new lock() | `grep -n "lock(" SIMA.Lifecycle.cs` | 0 new occurrences |
| 2. Master hydration added | `grep -n "Master.*Seeded\|Master.*Adopted" SIMA.Lifecycle.cs` | 2 matches |
| 3. Account.Cancel in CANCEL_ALL master | `grep -n "Account.Cancel" UI.IPC.Commands.Fleet.cs` | 1+ match in master loop |
| 4. No CancelOrder in bracket sweep | `grep -n "CancelOrder(order)" UI.IPC.Commands.Fleet.cs` | 0 matches in state=SIMA |
| 5. ASCII gate | Python byte scan both files | 0 non-ASCII |
| 6. BUILD_TAG | `grep -n BUILD_TAG V12_002.cs` | "993" |

---

## Safety Assessment

**Preserved invariants**:

- Semantic Separation (Build 990): SweepTrackedOrders(force=false) still skips brackets -- unchanged
- FSM lifecycle: HydrateFSMsFromWorkingOrders() unchanged; master has no FSM (correct)
- REAPER suppression: isFlattenRunning guard unchanged
- Actor model: Enqueue used in hydration for expectedPositions (same as fleet path)
- `lock(stateLock)`: Zero new usages

**Risk surface**:

- Change 1: Read-only position scan. Enqueue is safe for off-strategy-thread hydration call.
- Change 2: Read-only order scan. Writes to ConcurrentDictionary (thread-safe).
- Change 3: `Account.Cancel()` is the REAPER-proven API. Idempotent.
- Change 4: String constant. No logic change.

**Stability Score Projection**: 92/100 (up from 88/100 at Build 992)

---

## Live Validation Criteria

After NT8 disable/re-enable with open position + brackets:

1. Log MUST show: `[SIMA HYDRATE] Sim101 (Master): Seeded expected=N from broker`
2. Log MUST show: `[SIMA HYDRATE] Sim101 (Master): Adopted Stop_X -> stopOrders[X]` (per bracket)
3. REAPER log MUST show: `Expected=N, Actual=N` (no desync) or heartbeat flat
4. After FLATTEN_ONLY + CANCEL_ALL: log MUST show `Cancelled N orders` where N = count of brackets
5. Broker order screen: ZERO orphaned Stop_/T1_/T2_ remaining

---

*Reviewed by ARCHITECT (Claude Sonnet 4.6) on 2026-03-16*
*Pending Antigravity audit before Engineer execution*
