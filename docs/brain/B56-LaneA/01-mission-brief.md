# PTT-COPIER B56 — LaneA Mission Brief
## DW-B56-01 P0: Limit Order Gate 3 + Follower Cancel Propagation

**Date**: 2026-08-09
**Epic**: B56-LaneA
**Status**: ACTIVE — Stage 1 (ptt-orchestrator)
**Prerequisite**: B55-LaneA FINAL_PASS ✅ (baseline: 279 total, 255 pass, 24 fail)

---

## 1. Objective

Fix two gaps in `CopyEngine.OnOrderUpdate` / `DispatchCopy` so that:

1. **Limit orders** placed via AddOn `Account.CreateOrder()` are correctly copied through
   to Filled/Cancelled (they never reach `OrderState.Submitted`, so Gate 3 currently
   silently drops all post-placement events).
2. **Follower entry orders** are cancelled when the leader order is cancelled.

**Single modified file**: `src/PropTraderTools/CopyEngine.cs`

---

## 2. Root Cause

### NT8 Order State Lifecycle

| Order Type | States Observed |
|------------|----------------|
| Market order | Initialized → **Submitted** → Accepted → Working → Filled |
| Limit order (AddOn `CreateOrder()`) | Initialized → **Accepted** → Working → Filled / Cancelled |

`Submitted` is **never fired** for AddOn-placed limit orders. This is a confirmed
NinjaTrader 8 NT8 platform behavior (not a code bug in the library).

### Gap 1 — DispatchCopy Gate 3

```csharp
// CURRENT (line ~512):
if (order.OrderState != OrderState.Submitted) return;
```

This guard silently drops every `OnOrderUpdate` event for limit orders after initial
placement. The follower order is created (the `Initialized` path fires), but the engine
then ignores `Accepted`, `Working`, `Filled`, and `Cancelled` events — so the follower
order is never driven to fill or cancel.

### Gap 2 — No Cancelled propagation in OnOrderUpdate

`OnOrderUpdate` has no handler for `OrderState.Cancelled` on the leader order.
When the leader is cancelled, no code calls `CancelOneAccount()` on follower accounts.
The follower's Initialized/Working entry order lingers indefinitely.

---

## 3. Change Spec (authoritative)

### Change A — `IsDispatchTriggerState` predicate

**Add** immediately before `DispatchCopy()`:

```csharp
// CYC=2. True for states that should trigger a new follower order placement.
// Market orders fire Submitted; AddOn limit orders fire Accepted (skip Submitted).
// JS-002: returns bool (not null). JS-021: no lock. NT8 confirmed state set.
private static bool IsDispatchTriggerState(Order order)
    => order.OrderState == OrderState.Submitted   // market orders
    || order.OrderState == OrderState.Accepted;   // limit orders (AddOn path)
```

**Modify** `DispatchCopy()` Gate 3:
```csharp
// BEFORE:
if (order.OrderState != OrderState.Submitted) return;

// AFTER:
if (!IsDispatchTriggerState(order)) return;
```

### Change B — Leader Cancelled → cancel follower entry orders

**Insert** in `OnOrderUpdate()`, AFTER Gate 2.5 (per-rule enable check) and BEFORE
Gate B (`if (IsWorkingBracket(e.Order))`):

```csharp
// B56 T1: propagate leader cancel to follower entry orders.
// Fires when leader order is cancelled — cancels all Initialized/Working
// follower entry orders for this instrument via CancelOneAccount.
// Placed BEFORE Gate B so bracket orders are not affected (they have their own path).
if (e.Order.OrderState == OrderState.Cancelled)
{
    foreach (var acc in matchedRule.Value.FollowerAccounts)
    {
        if (acc == null) continue;
        CancelOneAccount(acc, e.Order.Instrument);
    }
    return;
}
```

---

## 4. Invariants (ptt-verifier confirms all)

| ID | Assertion |
|----|-----------|
| INV-1 | `IsDispatchTriggerState(Submitted)` == `true` |
| INV-2 | `IsDispatchTriggerState(Accepted)` == `true` |
| INV-3 | `IsDispatchTriggerState(Initialized)` == `false` |
| INV-4 | `IsDispatchTriggerState(Working)` == `false` |
| INV-5 | `IsDispatchTriggerState(Filled)` == `false` |
| INV-6 | `IsDispatchTriggerState(Cancelled)` == `false` |
| INV-7 | `DispatchCopy` Gate 3 reads `IsDispatchTriggerState` (not raw `== Submitted`) |
| INV-8 | Cancelled branch is present in `OnOrderUpdate` BEFORE `IsWorkingBracket` check |
| INV-9 | `CancelOneAccount` called for each non-null follower account on leader Cancelled |

---

## 5. New Tests

| Test ID | Method | Purpose |
|---------|--------|---------|
| `T_B56_01` | `IsDispatchTriggerState_ReturnsTrueForSubmittedAndAccepted` | Verify predicate for all 6 relevant `OrderState` values via reflection |

**T_B56_01 assertions**:
- `Assert.True`  for `Submitted`, `Accepted`
- `Assert.False` for `Initialized`, `Working`, `Filled`, `Cancelled`

**T_B56_02** (optional per architect's decision): dedup guard test for Gate 5 double-fire.

---

## 6. JS Rule Constraints

| Rule | Requirement |
|------|------------|
| JS-021 | No `lock()`. `IsDispatchTriggerState` is read-only; Cancelled path uses existing `ConcurrentBag` foreach via `CancelOneAccount`. |
| JS-002 | No `return null`. Both new constructs return `bool` or `void`. |
| JS-033 | No `async void`. |
| JS-001 | No `throw new` in hot path. |
| CYC | `IsDispatchTriggerState` CYC=2. Cancelled `foreach` CYC=2. Both ≤ 8. |

---

## 7. 7-Scan Checklist (ptt-verifier runs all independently)

| # | Scan | Target | Pass Criterion |
|---|------|--------|----------------|
| SCAN-01 | `Select-String "lock("` | `src/ -Recurse -Include *.cs` | 0 actual `lock()` calls |
| SCAN-02 | `Select-String "async void "` | `src/ -Recurse -Include *.cs` | 0 `async void` decls |
| SCAN-03 | `Select-String "return null"` | `src/ -Recurse -Include *.cs` | 0 new instances |
| SCAN-04 | `Select-String "throw new "` | `src/ -Recurse -Include *.cs` | 0 new instances |
| SCAN-05 | `complexity_audit.py` | `IsDispatchTriggerState`, Cancelled block | CYC ≤ 8 for all new methods |
| SCAN-06 | `dotnet build` | PropTraderTools.csproj | 0 errors |
| SCAN-07 | `dotnet test` | PropTraderTools.csproj | T_B56_01 PASS; total=279+1=280, 255+1=256 pass, 24 fail |

Post-scan:
- `powershell -File scripts\verify_links.ps1 -Fix` → 0 DESYNC

---

## 8. Build Tag

```
PTT-COPIER B56 | limit-order-gate3-fix | 2026-08-09
```

---

## 9. FINAL_PASS Criteria

- [ ] VERIFY_PASS on all 7 scans
- [ ] `IsDispatchTriggerState` method exists in `CopyEngine.cs` (private or internal static)
- [ ] `DispatchCopy` Gate 3 calls `IsDispatchTriggerState` (not raw `== Submitted`)
- [ ] Cancelled propagation block present in `OnOrderUpdate` BEFORE `IsWorkingBracket` call
- [ ] T_B56_01 PASS — all 6 `OrderState` assertions correct
- [ ] 0 new `lock()`, 0 new `async void`, 0 new `return null`
- [ ] Hard-link sync PASS
- [ ] Build tag written and confirmed in source

---

## 10. Conflict Check

B55-LaneB (doc-comment-only change) is still potentially in-progress.
**B56-LaneA only touches `CopyEngine.cs` method bodies** (no doc comments).
No structural overlap. ptt-engineer must read the current state of `CopyEngine.cs`
from disk before editing to incorporate any B55-LaneB changes already committed.

---

*Stage 1 complete. Authored by ptt-orchestrator.*
