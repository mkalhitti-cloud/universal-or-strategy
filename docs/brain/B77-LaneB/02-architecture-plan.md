# B77-LaneB Architecture Plan — QX Race Guard

**Epic**: B77-LaneB
**Phase**: 1 — Architecture
**Author**: ptt-architect
**Status**: REVIEW_PASS

---

## 1. Bug Confirmation

### Race Sequence

The race exists between `PttQuickExit.Execute()` at
[`PttQuickExit.cs:67`](src/PropTraderTools/Features/PttQuickExit.cs:67) and the submit
loop at [`PttQuickExit.cs:83-152`](src/PropTraderTools/Features/PttQuickExit.cs:83).

**Execute() call sequence (confirmed from source read)**:

| Step | Line | Action |
|------|------|--------|
| 1 | 41-51 | Null/flat guard — exits early if no position |
| 2 | 55-61 | Follower guard |
| 3 | 63-64 | `SnapshotStopPrice` |
| 4 | **67** | `CopyEngine.Instance?.CancelQxBrackets(leader, instr)` — cancels ALL active PTT-QX-*, PTT-BE-*, ATM bracket orders |
| 5 | **69** | `CopyEngine.Instance?.CancelQxBracketsForFollowers(instr)` |
| 6 | **83-152** | `for (int i = 0; i < targetCount; i++)` — submits `N*2` new PTT-QX orders via `leader.Submit(new[] { stopOrd })` and `leader.Submit(new[] { tNOrd })` |

**The race window** (lines 67 → 83..152):

`CancelQxBrackets` at line 67 is called **before** the submit loop. After step 4
completes and the loop at step 6 begins emitting orders, any re-entrant call to
`CancelQxBrackets` — whether from a rapid second QX press queued on the dispatcher,
or from an NT8 `OnOrderUpdate` callback — will see the freshly-submitted PTT-QX
orders in `OrderState.Submitted` or `OrderState.Initialized` and **cancel them**,
because [`CancelQxBrackets`](src/PropTraderTools/CopyEngine.cs:586) includes those
states in its `stateOk` gate (lines 592-596):

```
bool stateOk = o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Initialized
            || o.OrderState == OrderState.Accepted
            || o.OrderState == OrderState.Submitted      // line 595
            || o.OrderState == OrderState.TriggerPending;// line 596
```

Result: loop iteration `i=0` orders (`PTT-QX-Stop`, `PTT-QX-T1`) are cancelled
mid-loop while iterations `i=1..N-1` orders are still being submitted. The account
ends up with an orphaned partial bracket set.

---

## 2. NT8 API Investigation

**Source**: `docs/standards/NT8_FULL_REFERENCE.md` lines 775–920 (Order class, Methods and Properties table).

### Does `Order.SubmittedTime` exist?

**NO.** The complete Order property list from NT8_FULL_REFERENCE.md contains no
`SubmittedTime` property. The full property table is:

> Account, AverageFillPrice, Filled, FromEntrySignal, Gtd, HasOverfill, Instrument,
> IsBacktestOrder, IsLiveUntilCancelled, IsTerminalState(), LimitPrice,
> LimitPriceChanged, Name, Oco, OrderAction, **OrderId**, **OrderState**, OrderType,
> Quantity, QuantityChanged, StopPrice, StopPriceChanged, **Time**, TimeInForce, ToString()

The only time-related property is `Order.Time`:

> **Time** — "A DateTime structure representing the **last time the order changed state**"
> (NT8_FULL_REFERENCE.md line 903-904)

This is NOT a submission timestamp — it reflects the most recent state transition and
changes as the order progresses through its lifecycle.

### Is `Order.OrderId` a stable key after `Submit()`?

**NO.** From NT8_FULL_REFERENCE.md line 771:

> "The property `<order>.OrderId` is NOT a unique value, since it can change
> throughout an order's lifetime."

`OrderId` is broker-issued and mutates during order lifecycle (historical → live
transition). It cannot be used as a stable dictionary key.

### What IS stable after `Submit()`?

From NT8_FULL_REFERENCE.md line 773:

> "To check for equality you can compare Order objects directly."

NT8 Order **object references** are stable within a session. An Order object
returned from `CreateOrder()` is the same reference accessible later via
`acc.Orders`. Direct reference equality (`ReferenceEquals` / default
`object.Equals`) is the NT8-endorsed comparison mechanism.

---

## 3. Approach Evaluation

### Approach A — `_qxEpoch` volatile int + ConcurrentDictionary: INFEASIBLE

**Fatal flaw — epoch cross-contamination**:

`CancelQxBrackets` is called from three independent paths in addition to
PttQuickExit:

| Caller | File | Line |
|--------|------|------|
| `RelayBe()` | CopyEngine.cs | 419 |
| `CancelQxBracketsForFollowers()` | CopyEngine.cs | 649 |
| `TradeCopierPanel` (panel UI) | TradeCopierPanel.cs | 597 |

Placing `_qxEpoch` on `CopyEngine` and incrementing it inside `CancelQxBrackets`
means a BE relay (`RelayBe` → `CancelQxBrackets`) would bump `_qxEpoch`,
invalidating all previously tagged PTT-QX orders from the current QX cycle. The
dictionary would treat still-live QX orders as "old epoch" and re-cancel them.

**Fatal flaw — OrderId instability**:

`ConcurrentDictionary<string, int>` keyed on `Order.OrderId` cannot work because
`OrderId` changes throughout the order's lifetime (NT8_FULL_REFERENCE.md line 771).
A `ConcurrentDictionary<Order, int>` by reference is possible but the epoch
cross-contamination problem is disqualifying regardless.

**Verdict**: INFEASIBLE.

---

### Approach B — `_qxSubmitCutoff` volatile long + `Order.SubmittedTime`: INFEASIBLE

**Fatal flaw — property does not exist**:

`Order.SubmittedTime` does NOT exist in the NT8 API (NT8_FULL_REFERENCE.md lines
775–920, confirmed by full property table read and grep — 0 matches for
"SubmittedTime" across the reference document).

**Partial alternative — `Order.Time`**:

Using `Order.Time` (last state change) as a proxy for submission time is not
reliable:
1. `Order.Time` is a `DateTime`; `Environment.TickCount64` is `long` (ms since
   boot) — mixing the two requires fragile conversion.
2. Sub-millisecond race window: a Submit() and a cutoff capture within the same
   millisecond are indistinguishable.
3. `Order.Time` updates on EVERY state transition; an order that progresses
   Initialized → Submitted → Accepted within the window has a newer `Time` that
   may or may not exceed the cutoff, making the filter non-deterministic.

**Verdict**: INFEASIBLE. `Order.SubmittedTime` does not exist; `Order.Time` is an
unreliable proxy with an unresolvable sub-millisecond race window.

---

### Approach C — Snapshot `HashSet<Order>` before submit loop: FEASIBLE

**Mechanism**:

1. Before `CancelQxBrackets` is called in `Execute()`, capture a point-in-time
   snapshot of all currently active QX-candidate orders: `BuildQxSnapshot(leader, instr)`.
2. Pass the snapshot to a new overload `CancelQxBrackets(acc, instr, snapshot)`.
3. The new overload only cancels orders that ARE in the snapshot. Orders not in the
   snapshot (submitted after the snapshot was taken = freshly placed this cycle)
   are skipped.

**NT8 feasibility**: Order object equality by reference is NT8-endorsed
(NT8_FULL_REFERENCE.md line 773). `HashSet<Order>` uses `object.Equals` (reference
equality by default for reference types with no custom `Equals`), which matches NT8's
stated comparison model.

**Blast radius of signature change**:

The existing 2-parameter `CancelQxBrackets(Account, Instrument)` signature is
**NOT changed**. A new 3-parameter overload is added. Existing callers:

| Caller | Change needed? |
|--------|---------------|
| `RelayBe()` — CopyEngine.cs:419 | None — calls 2-param overload |
| `CancelQxBracketsForFollowers()` — CopyEngine.cs:649 | None — calls 2-param overload |
| `TradeCopierPanel` — TradeCopierPanel.cs:597 | None — calls 2-param overload |
| `PttQuickExit.Execute()` — PttQuickExit.cs:67 | Updated — calls new 3-param overload |

Blast radius: **0 existing callers broken**. 1 call site updated. 1 new overload.
1 new static helper.

**JS-021 compliance**: No `lock()`. `HashSet<Order>` is created on the calling
thread, passed by reference to a synchronous method, and never shared across threads.
NT8 AddOn execution dispatches through `Dispatcher.InvokeAsync` — Execute() calls
run sequentially on the NT8 dispatcher, so the snapshot is captured and consumed
within a single dispatcher invocation with no concurrent mutation.

**Verdict**: FEASIBLE. Correct, minimal, NT8-grounded, zero blast radius on existing callers.

---

## 4. Chosen Approach

**Approach C — Snapshot `HashSet<Order>` before submit loop.**

### Rationale

1. **Only approach with zero race window**: The snapshot is a deterministic
   point-in-time view. There is no timestamp comparison, no epoch counter
   cross-contamination, and no NT8 API that is absent or unstable.
2. **NT8 API grounded**: Uses NT8-endorsed Order reference equality
   (NT8_FULL_REFERENCE.md line 773) and the already-used `acc.Orders` collection.
3. **No new shared state on CopyEngine**: `BuildQxSnapshot` is a pure static
   method; the new overload is instance-stateless. No volatile fields, no
   concurrent collections added to CopyEngine.
4. **Zero blast radius on existing callers**: All three existing callers of
   `CancelQxBrackets` continue calling the 2-parameter overload unchanged.
5. **All CYC budgets met**: All new and modified methods remain CYC ≤ 8.

### Step-by-Step Changes

**CopyEngine.cs — add `BuildQxSnapshot`**:
```
internal static HashSet<Order> BuildQxSnapshot(Account acc, Instrument instr)
```
- Returns `new HashSet<Order>()` on null inputs (never null)
- Iterates `acc.Orders`, collects orders where stateOk AND same instrument AND
  `IsQxCancelCandidate(o)` returns true
- Uses the same 5-state `stateOk` gate as `CancelQxBrackets`

**CopyEngine.cs — add overload `CancelQxBrackets(Account, Instrument, HashSet<Order>)`**:
```
internal void CancelQxBrackets(Account acc, Instrument instr, HashSet<Order> snapshot)
```
- Identical to the 2-param overload except: inside the foreach body, skips any
  order where `snapshot == null || !snapshot.Contains(o)` — only cancels orders
  present in the snapshot
- `snapshot == null` fallback: cancels all (same as 2-param) — safe default

**PttQuickExit.cs — update `Execute()`**:
- Add before line 67: `var snapshot = CopyEngine.BuildQxSnapshot(leader, instr);`
- Change line 67 from:
  `CopyEngine.Instance?.CancelQxBrackets(leader, instr);`
  to:
  `CopyEngine.Instance?.CancelQxBrackets(leader, instr, snapshot);`
- No other changes. Execute() CYC remains 8.

---

## 5. CYC Analysis

| Method | Current CYC | Projected CYC | Budget | Status |
|--------|-------------|---------------|--------|--------|
| `CancelQxBrackets(acc, instr)` | 6 | 6 (unchanged) | ≤ 8 | PASS |
| `CancelQxBrackets(acc, instr, snapshot)` — new overload | — | 7 | ≤ 8 | PASS |
| `BuildQxSnapshot(acc, instr)` — new static | — | 4 | ≤ 8 | PASS |
| `IsQxCancelCandidate(Order)` | 6 | 6 (unchanged) | ≤ 8 | PASS |
| `PttQuickExit.Execute()` | 8 | 8 (unchanged) | ≤ 8 | PASS |

### CYC Breakdown — `CancelQxBrackets(acc, instr, HashSet<Order> snapshot)` CYC=7

1. `if (acc == null || instr == null) return;` — null guard
2. `foreach (Order o in acc.Orders)` — loop
3. `stateOk` compound `||` expression (Roslyn counts as 1 decision) — state gate
4. `if (!stateOk) continue;` — state filter
5. `if (o.Instrument == null || o.Instrument.FullName != instr.FullName) continue;` — instrument filter
6. `if (snapshot != null && !snapshot.Contains(o)) continue;` — snapshot filter
7. `if (stale.Count == 0) return;` — empty guard

### CYC Breakdown — `BuildQxSnapshot(acc, instr)` CYC=4

1. `if (acc == null || instr == null) return new HashSet<Order>();` — null guard
2. `foreach (Order o in acc.Orders)` — loop
3. `stateOk` (same 5-state gate, Roslyn=1) — state filter
4. `if (stateOk && o.Instrument?.FullName == instr.FullName && IsQxCancelCandidate(o))` — add to set

---

## 6. Ticket Breakdown

### T1 — CopyEngine.cs changes

**File**: `src/PropTraderTools/CopyEngine.cs`

Methods to add (no existing methods modified):

1. **`internal static HashSet<Order> BuildQxSnapshot(Account acc, Instrument instr)`**
   - Signature: `internal static HashSet<Order> BuildQxSnapshot(Account acc, NinjaTrader.Cbi.Instrument instr)`
   - Returns `new HashSet<Order>()` on null inputs — JS-002: never null
   - Same 5-state `stateOk` gate as `CancelQxBrackets`
   - Filters by `o.Instrument.FullName == instr.FullName`
   - Filters by `IsQxCancelCandidate(o)`
   - CYC=4, JS-021 compliant (no lock), JS-001 compliant (no throw)
   - Place immediately above `CancelQxBrackets` (line ~585)

2. **`internal void CancelQxBrackets(Account acc, NinjaTrader.Cbi.Instrument instr, HashSet<Order> snapshot)`**
   - New 3-parameter overload; existing 2-parameter overload left unchanged
   - Body: identical structure to 2-param overload, with one additional `continue` guard:
     `if (snapshot != null && !snapshot.Contains(o)) continue;`
   - `snapshot == null` fallback cancels all active QX orders (same as 2-param)
   - CYC=7, JS-021 compliant (no lock)
   - JS constraints: JS-001 (no throw), JS-002 (void), JS-033 (sync void), ASCII-only
   - Place immediately below existing `CancelQxBrackets` (after line 605)

---

### T2 — PttQuickExit.cs changes

**File**: `src/PropTraderTools/Features/PttQuickExit.cs`

1. **Before line 67** — add snapshot capture:
   ```
   var snapshot = CopyEngine.BuildQxSnapshot(leader, instr);
   ```

2. **Line 67** — update call to 3-param overload:
   ```
   // BEFORE:
   CopyEngine.Instance?.CancelQxBrackets(leader, instr);
   // AFTER:
   CopyEngine.Instance?.CancelQxBrackets(leader, instr, snapshot);
   ```

3. **No other changes**. `Execute()` CYC remains 8. `CancelQxBracketsForFollowers`
   call at line 69 is unchanged (followers use the 2-param overload via
   `CancelQxBracketsForFollowers` — no snapshot needed for the follower cancel path
   because follower brackets are always wholesale-replaced, not partially rebuilt).

---

### T3 — 8 xUnit Test IDs

**File**: `src/PropTraderTools/CopyEngineTests.cs` (appended as class `B77QxRaceGuardTests`)

| ID | Test name | Asserts |
|----|-----------|---------|
| 1 | `T_B77_QX_01_RaceGuard_NewOrderNotInSnapshot_IsNotCancelled` | Race-guard positive path: an order submitted after snapshot was taken (not in snapshot) is NOT added to the cancel list |
| 2 | `T_B77_QX_02_RaceGuard_StaleOrderInSnapshot_IsCancelled` | Race-guard negative path: an order that WAS in the snapshot (prior-cycle stale order) IS added to the cancel list |
| 3 | `T_B77_QX_03_RaceGuard_NonQxOrder_UnaffectedBySnapshot` | Non-PTT-QX orders (e.g. Name="Entry") are not cancelled regardless of snapshot membership -- `IsQxCancelCandidate` blocks them before snapshot check |
| 4 | `T_B77_QX_04_BuildQxSnapshot_NoWorkingQxOrders_ReturnsEmptySet` | `BuildQxSnapshot` returns non-null empty `HashSet<Order>` when account has no active PTT-QX orders |
| 5 | `T_B77_QX_05_IsQxCancelCandidate_WorkingQxStop_InSnapshot_IsCancelled_NotInSnapshot_IsSkipped` | `IsQxCancelCandidate` returns true for Working PTT-QX-Stop; when in snapshot order is cancelled, when snapshot is empty the same order is skipped |
| 6 | `T_B77_QX_06_IsQxCancelCandidate_FilledOrder_InSnapshot_IsNotCancelled` | Filled order is not cancelled even when present in snapshot -- terminal-state `stateOk` gate fires before snapshot check |
| 7 | `T_B77_QX_07_CancelQxBrackets_EmptySnapshot_NoExceptionZeroCancels` | `CancelQxBrackets` with empty (non-null) snapshot throws no exception and produces 0 cancels |
| 8 | `T_B77_QX_08_BuildQxSnapshot_TwoCalls_SameState_ReturnEqualSets` | `BuildQxSnapshot` is deterministic/idempotent: two calls against the same account state return sets where `snapshot1.SetEquals(snapshot2) == true` |

All tests use xUnit `[Fact]`. No NUnit, no MSTest (JS testing standard, OKF
`testing-strategies.md`).

---

## 7. Status

**REVIEW_PASS**
