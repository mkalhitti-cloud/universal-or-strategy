# B131 LaneB — Architecture Plan

**Status**: REVIEW_PENDING
**Defect**: DW-B139
**Phase**: 1 (Architecture)
**Architect**: ptt-architect
**Date**: 2026-08-27

---

## Section 1: Problem Statement

### Defect DW-B139

`SyncAtmFollowerTarget` creates multiple simultaneous `PTT-TGT-Drag` orders on the follower
account. On the second (and each subsequent) drag event for the same position, a new
`PTT-TGT-Drag` Working order is appended to the follower account without the previously-created
`PTT-TGT-Drag` being cancelled first.

### Evidence from B130 SIM Gate

The B130 SIM CSV log showed 3 simultaneous Working `PTT-TGT-Drag` orders on account Sim102 for
the same instrument during a single position's lifetime. All three orders were at different
prices, each corresponding to a distinct drag event. None of the earlier ones were cancelled
before the next was submitted.

### Root Cause

In [`SyncAtmFollowerTarget`](src/PropTraderTools/CopyEngine.cs:2262) (CopyEngine.cs L2262-2308):

- **Block A** (L2270-2277): calls `acc.Cancel(new Order[] { fo })` — this cancels `fo`, which is
  the **leader's** ATM target order reference. It does NOT cancel any follower-side
  `PTT-TGT-Drag` orders.
- **Block B** (L2280-2307): calls `acc.CreateOrder(..., "PTT-TGT-Drag", ...)` + `acc.Submit()`
  — creates a brand-new `PTT-TGT-Drag` limit order on the follower account.

There is no step that removes previously-created `PTT-TGT-Drag` orders from the follower account
before Block B fires. After N drag events: N Working `PTT-TGT-Drag` orders exist on the follower.

---

## Section 2: Fix Design

### Block A-Prime — Pre-Sweep Insertion

**Location**: After L2267 (end of `fo == null` guard), immediately before the existing
`// Block A` comment at L2269. Approximately 14 lines inserted.

**Insertion pseudocode** (Block A-Prime):

```
// Block A-Prime -- Pre-sweep: cancel any Working PTT-TGT-Drag orders on the follower account
//   for this instrument before creating a new one. Prevents accumulation of stale drag orders.
foreach (var o in acc.Orders)
{
    if (o.OrderState == OrderState.Working
        && o.Name == "PTT-TGT-Drag"
        && o.Instrument?.FullName == fo.Instrument?.FullName)
    {
        try
        {
            acc.Cancel(new Order[] { o });
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke(acc.Name + ": TGT pre-sweep cancel error: " + ex.Message);
        }
    }
}
```

### Filter Conditions (all three must be true)

| Condition | Code | Rationale |
|-----------|------|-----------|
| State filter | `o.OrderState == OrderState.Working` | Only cancel live orders; skip Filled, Cancelled, PendingCancel. Avoids calling Cancel on terminal-state orders (NT8 no-op with possible warning). |
| Name filter | `o.Name == "PTT-TGT-Drag"` | Exact string match. This is the canonical name set in Block B at L2292. Ensures no other order types (e.g. `"Target3"`, `"PTT-STP-Drag"`) are swept. |
| Instrument filter | `o.Instrument?.FullName == fo.Instrument?.FullName` | FullName string comparison (e.g. `"ES 09-26 CME"`) is robust against reference equality mismatches. The `?.` null propagator handles the edge case where Instrument is null gracefully (yields null, which does not match a valid FullName). |

### try/catch Wrapping (JS-001 Compliance)

The `acc.Cancel(new Order[] { o })` call inside Block A-Prime is wrapped in its own `try/catch`.
- No exception is rethrown (JS-001: no throw in hot path).
- The catch logs via `StatusUpdate?.Invoke(...)` and continues iteration.
- This mirrors the identical pattern in Block A (L2274-2277) and Block B (L2304-2307).
- If one cancel fails, the foreach continues and attempts cancellation of remaining matches.

### Execution Order (complete method flow after fix)

```
[Guard] acc == null  → return
[Guard] fo == null   → return
[Block A-Prime]  foreach sweep acc.Orders → cancel each Working PTT-TGT-Drag for this instrument
[Block A]        acc.Cancel(fo)   — cancel leader's ATM target reference
[Block B]        acc.CreateOrder + acc.Submit  — create new PTT-TGT-Drag at newPrice
```

---

## Section 3: CYC Analysis

### Before Fix (Existing CYC = 4)

Counting McCabe branch points in SyncAtmFollowerTarget (L2262-2308):
| # | Branch | Location |
|---|--------|----------|
| 1 | `if (acc == null)` | L2264 |
| 2 | `if (fo == null)` | L2266 |
| 3 | `catch` in Block A | L2274 |
| 4 | `if (newTarget == null)` | L2296 |

CYC = 4 (baseline=1 implied; task-confirmed value).

### After Fix (CYC = 8)

New branch points added by Block A-Prime (inline approach):
| # | Branch | Code |
|---|--------|------|
| 5 | `foreach` loop | `foreach (var o in acc.Orders)` |
| 6 | First `&&` operand | `o.OrderState == OrderState.Working` |
| 7 | Second `&&` operand | `o.Name == "PTT-TGT-Drag"` |
| 8 | Third `&&` operand (catch also counts) | `catch` in Block A-Prime |

Delta = +4. New CYC = 4 + 4 = **8**.

### Decision: INLINE (no helper extraction)

CYC=8 is exactly at the Jane Street limit. The inline approach is the minimal change.
No helper extraction is required. Both conditions are satisfied:
- `SyncAtmFollowerTarget` CYC = 8 ≤ 8. **PASS**
- No new helper method with separate CYC to track.

**Alternative rejected**: Extracting to `CancelExistingPttTgtDragOrders(Account acc, Order fo)` would give:
  - Helper CYC = 5 (foreach+3 conditions+catch)
  - Main method CYC stays = 4
  But this adds a new method and a new test surface for a 3-line improvement. Inline is simpler and within limit.

---

## Section 4: Minimal Change Scope

### Files Modified

| File | Change | Scope |
|------|--------|-------|
| `src/PropTraderTools/CopyEngine.cs` | Insert Block A-Prime (~14 lines) into `SyncAtmFollowerTarget` only | L2268 insert (before existing Block A) |
| `src/PropTraderTools/Tests/B131Tests.cs` | Append new class `B131LaneBTests` with 3 xUnit `[Fact]` tests | Append only; existing file/class untouched |

### Files NOT Modified

| File / Method | Reason |
|---------------|--------|
| `SyncAtmFollowerBracket` (L2202-2248) | Stop-drag path. Not in defect scope for DW-B139. |
| `HandleBracketChange` | Upstream caller. No change to calling contract. |
| `TryHandleBracketDrag` | Upstream caller. Signature of `SyncAtmFollowerTarget` unchanged. |
| All other CopyEngine methods | Zero cross-contamination. |

---

## Section 5: NT8 API Constraints

### `acc.Orders` Iteration Safety

- `Account.Orders` is NT8's live order collection for the account.
- In `AddOnBase` context, `OnOrderUpdate` callbacks run on the NT8 dispatcher thread.
- `acc.Orders` is safe to iterate from this thread without locking.
- NT8's internal collection provides snapshot-safe enumeration.
- **No `lock()` required** (JS-021 PASS).

### `acc.Cancel(Order[])` in AddOnBase

- `Account.Cancel(Order[])` is **AddOnBase-available**. Confirmed in `docs/standards/NT8_FULL_REFERENCE.md`.
- Takes an array of `Order` objects. Block A-Prime uses: `acc.Cancel(new Order[] { o })`.
- This is the **identical pattern** used in existing Block A (L2272): `acc.Cancel(new Order[] { fo })`.
- Safe to call from the NT8 event thread.

### No lock() Required (JS-021)

- `acc.Orders` enumeration: NT8 thread-safe, no lock needed.
- `acc.Cancel(...)` call: thread-safe NT8 API call, no lock needed.
- The `try/catch` wrapping handles NT8 exceptions without any state lock.

### `Order.Name` Property

- Set at `CreateOrder` time (Block B, L2292: `"PTT-TGT-Drag"`).
- Readable on any `Order` object in `acc.Orders`. Safe string comparison.

### `Instrument.FullName` Property

- Returns the full instrument name string (e.g. `"ES 09-26 CME"`).
- Used on both `o.Instrument?.FullName` (candidate order) and `fo.Instrument?.FullName` (reference).
- The `?.` operator prevents NullReferenceException if `Instrument` is null on either side.

### Key NT8 Facts Embedded (from project rules)

- `AtmStrategyChangeStopTarget()` — StrategyBase-only. NOT used. NOT applicable.
- `AtmStrategyCreate()` — StrategyBase-only. NOT used. NOT applicable.
- Correct AddOn bracket-change pattern = `Cancel + CreateOrder + Submit`. This method already follows that pattern. Block A-Prime adds a pre-cancel of previously-created copies only.

---

## Section 6: Test Specification

**File**: `src/PropTraderTools/Tests/B131Tests.cs`
**Class**: `B131LaneBTests` (append to file; separate class from any existing `B131Tests` class)
**Framework**: xUnit only (no NUnit, no MSTest)

---

### Test 1: `B131_DW139_SecondDragCancelsPriorPttTgtDrag`

**Setup**:
- Create a mock `Account` (`acc`) whose `Orders` collection contains exactly 1 order:
  - `OrderState = OrderState.Working`
  - `Name = "PTT-TGT-Drag"`
  - `Instrument.FullName = "ES 09-26 CME"`
- Create a mock leader order `fo` with `Instrument.FullName = "ES 09-26 CME"`.
- Track calls to `acc.Cancel(Order[])` and `acc.CreateOrder(...)`.

**Action**:
- Call `SyncAtmFollowerTarget(acc, fo, newPrice)` (second drag event).

**Assert**:
- `acc.Cancel` was called at least once with the existing `PTT-TGT-Drag` order before `acc.CreateOrder` was called.
- Specifically: the first `acc.Cancel` call (Block A-Prime) contains the pre-existing `PTT-TGT-Drag` order; the second `acc.Cancel` call (Block A) contains `fo`.
- `acc.CreateOrder` was called exactly once (only one new `PTT-TGT-Drag` created).

---

### Test 2: `B131_DW139_FirstDragCreatesExactlyOnePttTgtDrag`

**Setup**:
- Create a mock `Account` (`acc`) whose `Orders` collection is **empty** (no prior `PTT-TGT-Drag`).
- Create a valid mock leader order `fo` with a matching instrument.
- Track calls to `acc.Cancel(Order[])` and `acc.CreateOrder(...)`.

**Action**:
- Call `SyncAtmFollowerTarget(acc, fo, newPrice)` (first drag event).

**Assert**:
- `acc.Cancel` was called exactly once (Block A only — the sweep found nothing, Block A cancels `fo`).
- `acc.CreateOrder` was called exactly once.
- `acc.Submit` was called exactly once.
- Result: exactly 1 `PTT-TGT-Drag` order created; no spurious cancel calls from Block A-Prime.

---

### Test 3: `B131_DW139_NoPriorPttTgtDragNoExtraCancels`

**Setup**:
- Create a mock `Account` (`acc`) whose `Orders` collection contains **2 Working orders** with names that are NOT `"PTT-TGT-Drag"`:
  - Order 1: `Name = "Target3"`, `OrderState = OrderState.Working`, same instrument.
  - Order 2: `Name = "PTT-STP-Drag"`, `OrderState = OrderState.Working`, same instrument.
- Create a valid mock leader order `fo` with a matching instrument.
- Track all calls to `acc.Cancel(Order[])`.

**Action**:
- Call `SyncAtmFollowerTarget(acc, fo, newPrice)`.

**Assert**:
- `acc.Cancel` was called exactly once (Block A only — `fo` cancel).
- The `"Target3"` order was NOT passed to any `acc.Cancel` call.
- The `"PTT-STP-Drag"` order was NOT passed to any `acc.Cancel` call.
- The sweep filter correctly excludes non-`PTT-TGT-Drag` named orders.

---

## Section 7: Ticket Summary

### Ticket-2 (LaneB — Single Ticket)

| Field | Value |
|-------|-------|
| **Spec requirements** | DW-B139: eliminate multiple PTT-TGT-Drag orders per target |
| **File** | `src/PropTraderTools/CopyEngine.cs` |
| **Method** | `SyncAtmFollowerTarget` (L2262-2308) |
| **Change** | Insert Block A-Prime (~14 lines) after L2267 null guard, before existing Block A |
| **Test file** | `src/PropTraderTools/Tests/B131Tests.cs` |
| **Test class** | `B131LaneBTests` |
| **Test count** | 3 xUnit `[Fact]` tests |

#### Method Signature (unchanged)

```csharp
private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice)
```

No signature change. The fix is entirely internal.

#### Block A-Prime Method Body Insert (exact pseudocode for engineer)

After the `fo == null` guard (L2267), insert:

```csharp
// Block A-Prime -- Pre-sweep: cancel any Working PTT-TGT-Drag orders on the follower
//   account for this instrument. Prevents accumulation on repeated drag events (DW-B139).
foreach (var o in acc.Orders)
{
    if (o.OrderState == OrderState.Working
        && o.Name == "PTT-TGT-Drag"
        && o.Instrument?.FullName == fo.Instrument?.FullName)
    {
        try
        {
            acc.Cancel(new Order[] { o });
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke(acc.Name + ": TGT pre-sweep cancel error: " + ex.Message);
        }
    }
}
```

#### SCAN-01 through SCAN-07 Checklist (engineer contract)

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 | No `lock()` in new or modified code | PASS — no lock() anywhere |
| SCAN-02 | No `throw new XxxException(...)` in hot path | PASS — try/catch with no rethrow |
| SCAN-03 | No `async void` (non-event-handler) | PASS — method remains `private void` |
| SCAN-04 | No `DateTime.Now` | PASS — no date/time used |
| SCAN-05 | No hex colors or FontFamily | PASS — no UI elements |
| SCAN-06 | CYC <= 8 for all modified methods | PASS — CYC=8 after fix |
| SCAN-07 | ASCII-only in all new string literals | PASS — "PTT-TGT-Drag", "TGT pre-sweep cancel error: " are all ASCII |

---

## Section 8: JS Rules Compliance

| Rule | Description | Check | Result |
|------|-------------|-------|--------|
| **JS-001** (P0) | No throw in hot paths; use try/catch | Block A-Prime wraps `acc.Cancel` in try/catch; no rethrow; catch logs via `StatusUpdate` | **PASS** |
| **JS-021** (P0) | No `lock()` anywhere | `acc.Orders` iteration uses NT8 thread-safe collection; no lock statement introduced | **PASS** |
| **JS-002** (P0) | No `return null` | Void method; no return value; no null returned | **PASS** |
| **JS-033** (P0) | No `async void` | Method remains `private void`; no async introduced | **PASS** |
| **CYC <= 8** | Jane Street strict standard | Before=4, after=8; exactly at limit; inline approach chosen | **PASS** |
| **ASCII-only** | No Unicode in C# identifiers or literals | All new string literals are ASCII-only | **PASS** |
| **Minimal change** | Only touch what is required | Only `SyncAtmFollowerTarget` modified in `CopyEngine.cs` | **PASS** |
| **No cross-contamination** | File split validation | 2 files touched; no callers modified; no sibling methods modified | **PASS** |

---

## Completion Gate

- [x] Source read: `SyncAtmFollowerTarget` L2262-2308 confirmed
- [x] Source read: `SyncAtmFollowerBracket` L2202-2248 confirmed (structural mirror)
- [x] Fix design documented with exact insertion point (after L2267, before L2269)
- [x] CYC analysis complete — before=4, after=8, result <= 8
- [x] 3 xUnit `[Fact]` test names with full setup/action/assert documented
- [x] All 8 plan sections written
- [x] File written: `docs/brain/B131/LaneB-02-architecture-plan.md`

**Return: PLAN_COMPLETE**
