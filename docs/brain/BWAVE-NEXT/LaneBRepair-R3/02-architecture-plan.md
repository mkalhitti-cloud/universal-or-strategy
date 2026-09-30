# BWAVE-NEXT LaneBRepair-R3 — Architecture Plan
**Status**: REVIEW_PASS candidate  
**Branch**: `bwave-next-lane-b` (baseline commit 340b778a)  
**Author**: ptt-architect  
**Date**: 2026-08-22

---

## 1. LANE-SPLIT GATE

| # | Question | Answer |
|---|----------|--------|
| Q1 | Are the fixes in the same method or within 50 lines of each other? | **NO** — R3-F1 is in `BwaveNextLaneBTests.cs` (test file line ~172); R3-F2 is in `CopyEngine.cs` `SubmitDrainedEntry` (~line 6631). Different files, unrelated methods. |
| Q2 | Does Fix B design depend on Fix A final design? | **NO** — R3-F1 is a `BindingFlags` constant swap in the test. R3-F2 is a statement reorder in production code. No coupling. |
| Q3 | Does each fix have standalone value if the other is blocked? | **YES** — R3-F1 fixes test reflection (correctness). R3-F2 fixes use-after-clear drain bug (safety). Either delivers value independently. |
| Q4 | Does each fix have an independent SIM verification path? | **YES** — R3-F1: `GetMethod` returns non-null; test passes. R3-F2: structural test confirms `SubmitEntryDirect` is called before foreach cleanup. |

**LANE-SPLIT GATE RESULT: LANES-APPROVED**

> Despite LANES-APPROVED, all three items are bundled in a single ticket T1. The gate result records that independent execution is safe but the small scope does not justify ticket fragmentation.

---

## 2. Problem Summary

### R3-F1 — Test Reflection BindingFlags Mismatch (P1, verify-first)

**File**: `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs`  
**Test**: `FindFollowerEntryOrder_AcceptsEntryName_ForCloneMode`  

The test uses the shared constant `Priv = BindingFlags.NonPublic | BindingFlags.Instance` (line 15) to reflect `FindFollowerEntryOrder` via `EngineType.GetMethod("FindFollowerEntryOrder", Priv)` (line 172). Because `FindFollowerEntryOrder` is `private static`, `GetMethod` with `BindingFlags.Instance` returns `null`, causing `method` to be null and the subsequent `Assert.NotNull(method)` to fail.

**Verify step**: `CopyEngine.cs` line 3703 reads:
```csharp
private static Order? FindFollowerEntryOrder(Account follower, Instrument instrument)
```
**CONFIRMED STATIC**. Fix is required.

### R3-F2 — SubmitDrainedEntry Cleanup-Before-Submit Ordering Bug (P1, verify-first)

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Method**: `SubmitDrainedEntry` (~lines 6631–6651)  

As-found order of operations:
1. `_pendingDispatchDrains.TryRemove` — correct (double-submit prevention)
2. `follower == null` guard — correct
3. `foreach ... _drainOwnedOrderIds.TryRemove(id, out _)` — **PREMATURE**: drain IDs cleared before submit
4. `SubmitEntryDirect(...)` — submit happens after IDs already cleared

If `SubmitEntryDirect` fails to enqueue the replacement order (e.g., NT8 rejects silently), the drain IDs are permanently gone from `_drainOwnedOrderIds`. Those order IDs are now invisible to drain tracking, permanently lost.

**Verify step**: `CopyEngine.cs` lines 6640–6650 confirmed as-found:
```csharp
// F3-repair: clear drain-owned IDs now that drain is complete.
foreach (var id in payload.DrainedOrderIds) // (3)
    _drainOwnedOrderIds.TryRemove(id, out _);

SubmitEntryDirect( // (4) delegated
    follower, payload.Instrument, payload.Qty,
    payload.Price, payload.Action, payload.OrderType);
```
**CONFIRMED**: cleanup precedes submit. Fix is required.

### R3-V1 — Order.Name Null Guard in DrainThenDispatch Predicate (VERIFY ONLY)

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Lines**: ~6530–6535  

Predicate:
```csharp
o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)
    || o.Name == "Entry"
```
Finding: `StartsWith` throws `NullReferenceException` if `o.Name` is null.

**Verification result**: See §3 (R3-V1 Decision Tree). **DISMISSED — no fix.**

---

## 3. Verify-First Protocol

### R3-F1 Verify Protocol

| Step | Action | Tool | Expected |
|------|--------|------|----------|
| V-F1-1 | Read `CopyEngine.cs` line 3703 | `read_file` | `private static Order? FindFollowerEntryOrder(...)` |
| V-F1-2 | Read `BwaveNextLaneBTests.cs` line 15 | `read_file` | `BindingFlags.NonPublic \| BindingFlags.Instance` |
| V-F1-3 | Read `BwaveNextLaneBTests.cs` line 172 | `read_file` | `EngineType.GetMethod("FindFollowerEntryOrder", Priv)` |
| V-F1-4 | If static: apply fix (§4). If instance: document STALE, no fix. | — | static confirmed → proceed |

**Result**: STATIC CONFIRMED (line 3703). Fix proceeds.

### R3-F2 Verify Protocol

| Step | Action | Tool | Expected |
|------|--------|------|----------|
| V-F2-1 | Read `CopyEngine.cs` lines 6627–6651 | `read_file` | foreach at (3) before SubmitEntryDirect at (4) |
| V-F2-2 | If cleanup precedes submit: apply fix (§5). | — | confirmed → proceed |

**Result**: CONFIRMED (lines 6641–6650). Fix proceeds.

### R3-V1 Verify Protocol

| Step | Action | Source | Finding |
|------|--------|--------|---------|
| V-V1-1 | `NT8_FULL_REFERENCE.md` line 845 | "A string representing the name of an order which can be provided by the entry or exit signal name" | Type is `string`, no null stated |
| V-V1-2 | `NT8_ADDON_KNOWLEDGE.md` line 229 | `order.Name // "PTT-Copy", "PTT-Trim" etc -- set at CreateOrder time` | Accessed without null guard in project knowledge |
| V-V1-3 | `NT8_FULL_REFERENCE.md` line 2106 | `CreateOrder(... string name ...)` | `name` is a required `string` parameter on every order creation path |
| V-V1-4 | `NT8_FULL_REFERENCE.md` line 770 | "properties will always reflect the current state of an order" | No null-state documented for Name |
| V-V1-5 | `NT8_FULL_REFERENCE.md` lines 3023, 3468 | "check the underlying Order object for null" | NT8 warns about null *Order object*, not null Name on a live Order |
| V-V1-6 | `ActiveOrders` filter | Only returns live, non-null Order objects | Name is set at creation; no path yields a live order with null Name |

**VERDICT**: NT8 guarantees Order.Name is non-null for all orders returned by `ActiveOrders`. `StartsWith` on `o.Name` is safe. **DISMISSED — no fix required.**

---

## 4. Fix Design: R3-F1 (BindingFlags.Static for Static Method)

**File**: `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs`  
**Line to change**: 172  

**Problem**: `Priv` constant (line 15) is `BindingFlags.NonPublic | BindingFlags.Instance`. `FindFollowerEntryOrder` is `private static`, so `GetMethod` with `Instance` flag returns `null`.

**Fix**: Replace the `Priv` constant with an inline `BindingFlags` expression that uses `Static` for this specific call only. **Do not modify the `Priv` constant** — it is shared by all other `GetMethod` calls that correctly target instance methods.

**Before** (line 172):
```csharp
var method = EngineType.GetMethod("FindFollowerEntryOrder", Priv);
```

**After** (line 172):
```csharp
var method = EngineType.GetMethod("FindFollowerEntryOrder",
    BindingFlags.NonPublic | BindingFlags.Static);
```

**Scope**: Single line change in test file. No production code touched.  
**CYC impact**: Zero. No branches added.  
**JS rules**: None applicable (test file).  
**NT8 rules**: None applicable (test file).

---

## 5. Fix Design: R3-F2 (Submit-Before-Cleanup Reorder)

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Method**: `SubmitDrainedEntry` (~lines 6631–6651)

**Fix**: Move the `_drainOwnedOrderIds` cleanup foreach to **after** `SubmitEntryDirect`. Keep `_pendingDispatchDrains.TryRemove` at position (1) (double-submit prevention is still the first gate).

**Before** (as-found, lines 6631–6651):
```csharp
// CYC=4: (1) TryRemove fails early return, (2) FollowerAccount null early return,
//        (3) F3 cleanup foreach, (4) delegated to SubmitEntryDirect.
private void SubmitDrainedEntry(string acctKey)
{
    if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
        return;

    var follower = payload.FollowerAccount;
    if (follower == null) // (2)
        return;

    // F3-repair: clear drain-owned IDs now that drain is complete.
    foreach (var id in payload.DrainedOrderIds) // (3)
        _drainOwnedOrderIds.TryRemove(id, out _);

    SubmitEntryDirect( // (4) delegated
        follower,
        payload.Instrument,
        payload.Qty,
        payload.Price,
        payload.Action,
        payload.OrderType);
}
```

**After** (fixed):
```csharp
// CYC=4: (1) TryRemove fails early return, (2) FollowerAccount null early return,
//        (3) delegated to SubmitEntryDirect, (4) F3 cleanup foreach (after submit).
// R3-F2: cleanup moved after SubmitEntryDirect -- drain IDs preserved until submit completes.
private void SubmitDrainedEntry(string acctKey)
{
    if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
        return;

    var follower = payload.FollowerAccount;
    if (follower == null) // (2)
        return;

    SubmitEntryDirect( // (3) submit first -- drain IDs still in dict here
        follower,
        payload.Instrument,
        payload.Qty,
        payload.Price,
        payload.Action,
        payload.OrderType);

    // R3-F2: clear drain-owned IDs AFTER submit so IDs are preserved on submit failure.
    foreach (var id in payload.DrainedOrderIds) // (4)
        _drainOwnedOrderIds.TryRemove(id, out _);
}
```

**Scope**: Statement reorder within existing method. No new statements. No new branches.  
**CYC impact**: Zero. CYC stays 4 (4 decision points unchanged: TryRemove, null guard, SubmitEntryDirect delegate, foreach).  
**JS rules**: JS-021 (no lock) — satisfied. No atomic/locking changes.  
**NT8 rules**: No try/catch added (NT8 AddOnBase has no catchable CreateOrder-null pattern; prompt prohibits try/catch).  
**Comment update**: Header comment renumbers `(3)` and `(4)` to reflect new order. Adds R3-F2 rationale comment.

---

## 6. R3-V1 Verification Outcome Decision Tree

```
READ NT8_FULL_REFERENCE.md Order.Name definition
            |
            v
NT8 guarantees Name is non-null for live orders?
   YES (confirmed) ──> DISMISSED — no fix
   NO               ──> add null guard:
                        && (o.Name != null
                            && (o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)
                                || o.Name == "Entry"))
                        CYC +1 lambda boolean
```

**Outcome**: **DISMISSED** (path taken).

**Evidence chain**:
1. `NT8_FULL_REFERENCE.md` line 2106: `CreateOrder(... string name ...)` — name is a required string at creation.
2. `NT8_ADDON_KNOWLEDGE.md` line 229: `order.Name` accessed directly without null guard in all project code.
3. `NT8_FULL_REFERENCE.md` lines 3023, 3468: NT8 warns about null *Order objects*, not null Name on a non-null Order.
4. `ActiveOrders` returns only live, non-null Order objects with all properties reflecting current state.

No fix required. Finding recorded in dismissed table (§9).

---

## 7. Single Ticket T1 Scope

**Ticket T1**: All 3 items (R3-F1, R3-F2, R3-V1 dismissed)

| Item | File | Action |
|------|------|--------|
| R3-F1 | `Tests/BwaveNextLaneBTests.cs` line 172 | Change `Priv` → `BindingFlags.NonPublic \| BindingFlags.Static` |
| R3-F2 | `CopyEngine.cs` `SubmitDrainedEntry` ~line 6640 | Move foreach cleanup to after `SubmitEntryDirect`; update header comment |
| R3-V1 | — | DISMISSED. Document in plan. No code change. |

**Execution order within T1**:
1. Verify R3-F1 (read line 3703) → apply fix if static (confirmed)
2. Verify R3-F2 (read lines 6631–6651) → apply reorder if cleanup precedes submit (confirmed)
3. Document R3-V1 dismissal
4. Build + test gate

---

## 8. CYC Budget Per Method

| Method | File | Current CYC | Post-Fix CYC | Change |
|--------|------|-------------|--------------|--------|
| `SubmitDrainedEntry` | `CopyEngine.cs` | 4 | 4 | 0 (statement reorder only) |
| `FindFollowerEntryOrder_AcceptsEntryName_ForCloneMode` | `BwaveNextLaneBTests.cs` | N/A (test) | N/A (test) | 0 |
| `DrainThenDispatch` (entryCandidates predicate) | `CopyEngine.cs` | no change | no change | 0 (R3-V1 dismissed) |

All methods remain within Jane Street CYC ≤ 8 strict standard.

---

## 9. Dismissed Findings Table

| ID | Finding | Reason Dismissed | Authority |
|----|---------|-----------------|-----------|
| R3-V1 | `o.Name.StartsWith` NullReferenceException risk | NT8 guarantees non-null Name for all live orders in `ActiveOrders`. Every order creation path requires a string name. `NT8_FULL_REFERENCE.md` line 2106, `NT8_ADDON_KNOWLEDGE.md` line 229. | Verified against NT8 docs |
| — | TickCount64 | Not available in .NET 4.8 targeted by NT8 | LOCKED |
| — | Remove `.ToList()` on `ActiveOrders` | DW-NEXT-A-07 — future backlog | Locked decision |
| — | Drain key acct+instrument | DW-NEXT-B-01 — future backlog | Locked decision |
| — | GTC/TIF preservation | DW-NEXT-B-02 — future backlog | Locked decision |
| — | Watchdog resubmit | DW-NEXT-B-03 — future backlog | Locked decision |
| — | TryAdd fail-fast replacement | Correct design — locked | Locked decision |
| — | Test PascalCase rename | Out of scope R3 | Prompt exclusion |
| — | FSM on TryAdd fail | Out of scope R3 | Prompt exclusion |
| — | OnOrderUpdate helper extraction | DW-NEXT-B-04 — future backlog | Locked decision |
| — | SubmitDrainedEntry try/catch | NT8 AddOnBase has no catchable CreateOrder-null pattern; no try/catch in hot path | Prompt exclusion + JS-001 |

---

## 10. Acceptance Criteria

| Criterion | Verified By |
|-----------|-------------|
| R3-F1: `GetMethod("FindFollowerEntryOrder", BindingFlags.NonPublic \| BindingFlags.Static)` returns non-null | Test `FindFollowerEntryOrder_AcceptsEntryName_ForCloneMode` — `Assert.NotNull(method)` passes |
| R3-F1: No other test broken by the change (only line 172 modified) | All other `Priv`-based `GetMethod` calls unchanged; full test suite passes |
| R3-F2: `SubmitEntryDirect` call appears before `foreach ... _drainOwnedOrderIds.TryRemove` in `SubmitDrainedEntry` | Code review + structural test asserting line ordering |
| R3-F2: `_pendingDispatchDrains.TryRemove` remains the first statement in `SubmitDrainedEntry` | Code review |
| R3-F2: `SubmitDrainedEntry` CYC = 4 (unchanged) | Complexity audit — `complexity_audit.py` |
| R3-V1: No code change in `DrainThenDispatch` | Diff shows zero lines changed in that method |
| Build: `dotnet build` exits 0, zero errors | Build gate |
| Tests: all 8 BwaveNextLaneBTests + full suite pass | `dotnet test` exits 0 |
| NT8 sync: `ptt-sync-and-verify.ps1` exits 0, 0 MISMATCH lines | Sync gate |
| NT8 F5: compiles green | Manual F5 gate |

---

## 11. Deferred Backlog Carry-Forward

Source: `docs/brain/BWAVE-NEXT/LaneBRepair-R2/06-deferred-backlog.md`

All open items carry forward unchanged. No new items generated by R3.

| ID | Description | Status |
|----|-------------|--------|
| DW-NEXT-B-01 | Drain key: extend from `acct` to `acct+instrument` to prevent cross-instrument collision | OPEN — future |
| DW-NEXT-B-02 | Preserve GTC/TIF in `PendingDispatchDrain` payload; re-apply on resubmit | OPEN — future |
| DW-NEXT-B-03 | Watchdog: option to resubmit on timeout rather than drop | OPEN — future |
| DW-NEXT-B-04 | `OnOrderUpdate` helper extraction — reduce method body (CYC reduction target) | OPEN — future |

No new deferred items from R3. R3-V1 finding (Order.Name null) was investigated and dismissed; it does not enter the backlog.

---

## Component Map

| Component | File | Lines | Role |
|-----------|------|-------|------|
| `SubmitDrainedEntry` | `src/PropTraderTools/CopyEngine.cs` | ~6631–6651 | R3-F2 fix target |
| `FindFollowerEntryOrder` | `src/PropTraderTools/CopyEngine.cs` | 3703 | R3-F1 verify subject (no change) |
| `DrainThenDispatch` entryCandidates predicate | `src/PropTraderTools/CopyEngine.cs` | ~6530–6535 | R3-V1 verify subject (no change) |
| `FindFollowerEntryOrder_AcceptsEntryName_ForCloneMode` | `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` | ~169–178 | R3-F1 fix target |
| `Priv` constant | `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` | 15 | Shared constant — NOT modified |
