# W9-L4-001 Scan Report

## Identity

| Field | Value |
|-------|-------|
| **W9_ID** | W9-L4-001 |
| **File** | `src/V12_002.MetadataGuard.cs` |
| **Line** | 168 |
| **Pattern** | `.Values.Any(f =>` |
| **OKF Rule** | Rule 7 — LINQ hot-path |
| **Status** | **CONFIRMED** |

---

## Exact Code at Line 168

```csharp
// src/V12_002.MetadataGuard.cs:164-186
private bool MetadataGuardRepairAuthorized(string accountName, string context)
{
    try
    {
        bool hasActiveFsm = _followerBrackets.Values.Any(f =>        // <-- LINE 168
            f != null && f.AccountName == accountName && f.State == FollowerBracketState.Active
        );

        if (hasActiveFsm)
        {
            Print(
                string.Format("[METADATA-G4] Repair suppressed for {0}: FSM Active (self-healed)", accountName)
            );
            return false;
        }

        return true;
    }
    catch
    {
        return true;
    }
}
```

---

## Enclosing Method

**`MetadataGuardRepairAuthorized(string accountName, string context)`**
Declared at [`src/V12_002.MetadataGuard.cs:164`](../../../src/V12_002.MetadataGuard.cs:164)

---

## Call Graph (upward trace)

```
MetadataGuardRepairAuthorized(accountName, context)       [MetadataGuard.cs:164]
  └─ called from ExecuteReaperRepair(accountName)         [REAPER.Repair.cs:234]
       └─ called from ProcessReaperRepairQueue()           [REAPER.Repair.cs:25]
            ├─ Path A: RouteMasterFilledToRepair()         [Orders.Callbacks.AccountOrders.cs:693]
            │    └─ HandleMatchedFollower_TargetReplaceCancel(order)         [:697]
            │         └─ ProcessFollowerCancellationSafe() / ProcessFollowerCancellationUnconditional()
            │               └─ ProcessQueuedAccountOrder(item)               [:1123]
            │                    └─ ProcessAccountOrderQueue()               [:220]
            │                         └─ OnAccountOrderUpdate (broker-thread → TriggerCustomEvent)  [V12_002.cs:890]
            └─ Path B: TriggerCustomEvent(o => ProcessReaperRepairQueue())   [REAPER.Audit.cs:253]
                 └─ AuditApexPositions / EnqueueReaperRepairCandidate
                      └─ periodic REAPER audit timer (not OnBarUpdate/OnOrderUpdate/OnExecutionUpdate)
```

---

## Hot-Path Classification

**HOT PATH: NO**

### Reasoning

1. **Not reachable from `OnBarUpdate`** — `ProcessReaperRepairQueue` is invoked via `TriggerCustomEvent`, not inside the bar-update tick loop.

2. **Not reachable from `OnOrderUpdate` directly** — `OnOrderUpdate` feeds the actor Enqueue queue → `ProcessOnOrderUpdate`. The account-order path (`OnAccountOrderUpdate`) marshals through a separate `_accountOrderQueue` → `ProcessAccountOrderQueue` → `ProcessQueuedAccountOrder` → (eventually) `RouteMasterFilledToRepair` → `ProcessReaperRepairQueue`. This is a **deferred repair path**, invoked at most once per cancel event on a follower order — not on every order-state tick.

3. **Not reachable from `OnExecutionUpdate`** — execution callbacks feed `ProcessOnExecutionUpdate` via a separate actor path.

4. **Not reachable from `Dispatch*`** — the `Dispatch*` methods (`DispatchMatchedFollowerResult`, etc.) eventually reach `HandleMatchedFollowerOrder` but the repair path via `MetadataGuardRepairAuthorized` is guarded behind a master-filled detection branch that fires only in rare desync/cancel-race scenarios.

5. **Semantic classification**: `MetadataGuardRepairAuthorized` is a **defensive guard** called at most once per REAPER repair cycle. Repair cycles are infrequent (triggered by position desync events or the periodic REAPER audit, not per-bar or per-order-tick). This is solidly **P3 — non-hot-path** as registered.

---

## Blast Radius

Only one call site:
- [`src/V12_002.REAPER.Repair.cs:234`](../../../src/V12_002.REAPER.Repair.cs:234) — `ExecuteReaperRepair`

No other callers. Blast radius is **minimal (1 call site)**.

---

## NT8 API Context

`_followerBrackets` is a `ConcurrentDictionary` (inferred from `.Values` usage on a shared dict). The `.Values` property on `ConcurrentDictionary<K,V>` returns a **snapshot copy** in .NET, so the `Any()` call is safe for concurrency — but still allocates a new `ICollection<V>` on each call.

---

## Recommended Fix

Since this is **non-hot-path (P3)**, the LINQ violation is **low severity**. The OKF Rule 7 guidance for non-hot-path LINQ is: **leave + comment**. However, if the team decides to fix for completeness:

```csharp
// Replace:
bool hasActiveFsm = _followerBrackets.Values.Any(f =>
    f != null && f.AccountName == accountName && f.State == FollowerBracketState.Active
);

// With (explicit loop, no allocation):
bool hasActiveFsm = false;
foreach (var f in _followerBrackets.Values)
{
    if (f != null && f.AccountName == accountName && f.State == FollowerBracketState.Active)
    {
        hasActiveFsm = true;
        break;
    }
}
```

This eliminates the `ICollection<V>` snapshot allocation and removes the LINQ dependency. The change is purely internal to `MetadataGuardRepairAuthorized` — no caller signatures change.

---

## Test Requirement

**NO** — `MetadataGuardRepairAuthorized` is private, invoked only from `ExecuteReaperRepair`. The repair path is covered by existing integration tests that exercise the REAPER cycle. No new unit test needed for the loop → `Any` swap; behaviour is identical.
