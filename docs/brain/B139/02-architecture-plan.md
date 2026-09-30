# B139 Architecture Plan

**Block**: B139
**Phase**: 1 (Architecture)
**Status**: REVIEW_PENDING
**Produced by**: ptt-architect
**Date**: 2026-09-01
**Prior deferred backlog**: `docs/brain/B137/06-deferred-backlog.md`

---

## Defect Summary — DW-B152-B

| Field | Value |
|-------|-------|
| **ID** | DW-B152-B |
| **Title** | Cancel-in-flight race in SyncAtmFollowerBracket Block B -- CancelPending/CancelSubmitted gap |
| **Priority** | P1 |
| **Root Cause** | CancelExistingPttStpDrag filter does not include CancelPending or CancelSubmitted |
| **Evidence** | B138 grid log: Sim102/103/104 end with qty=4 PTT-STP-Drag (Cancelled) + qty=2 PTT-STP-Drag (Accepted) |
| **Prior partial fix** | DW-B152 Submitted filter (commit 5250d8ee) retained as valid improvement; this block closes remaining race |

### Root Cause Narrative

A 3-stop ATM template fires 3 sequential TP3-HBC events in rapid succession. Each event invokes
`SyncAtmFollowerBracket`. Each invocation:
1. Calls `CancelExistingPttStpDrag` (Block A-Prime pre-sweep, T4 B137).
2. Calls `acc.Cancel(fo)` on the ATM bracket (Block A).
3. Creates + submits a new `PTT-STP-Drag` (Block B).

When Event #2 fires, Event #1's `PTT-STP-Drag` may already be in `CancelPending` or `CancelSubmitted`
(a prior cancel is in-flight from Event #2's own Block A-Prime). The current filter
(`Submitted || Working || Accepted`) **does not match** `CancelPending || CancelSubmitted`, so
Event #3's Block B places a second `PTT-STP-Drag` alongside the already-being-cancelled first one.

Result: 2 `PTT-STP-Drag` orders per follower after the 3-event burst.

---

## LANE-SPLIT GATE RESULT

```
LANE-SPLIT GATE: SINGLE-PIPELINE
Q1: Same method or within 50 lines?      YES -- both approaches target CancelExistingPttStpDrag
Q2: Fix B design depends on Fix A?       YES -- mutually exclusive root-cause approaches
Q3: Each fix has standalone value?       NO  -- one defect, one fix required
Q4: Independent SIM verification paths? NO  -- single SIM scenario (3-stop ATM burst)
RESULT: SINGLE-PIPELINE (default applies; all gates confirm)
```

---

## Chosen Approach: A — Expand CancelExistingPttStpDrag State Filter

### Rationale

**Approach A** (expand the state filter + extract helper for CYC) is chosen. **Approach B**
(Account.Change() for Working/Accepted PTT-STP-Drag) is rejected.

**Why Approach A:**
- Directly addresses `CancelPending || CancelSubmitted` — the exact missing states.
- `acc.Cancel()` on an order in `CancelPending`/`CancelSubmitted` is idempotent: if NT8 rejects it with
  `ErrorCode.UnableToCancelOrder`, the existing `try/catch` absorbs it silently (consistent with OBS-A
  pattern in `DW-B134-OCO-OBS`).
- Zero new NT8 API surface: only adds two `OrderState` enum comparisons that are confirmed valid
  (`NT8_FULL_REFERENCE.md` L966, L3368; L971, L3369).
- Consistent with the established cancel+resubmit codebase architecture for all PTT-Drag orders.
- CYC compliance achieved via extraction to `IsPttStpDragCancellable` helper.

**Why Approach B is rejected:**
- `Account.Change()` cannot operate on an order in `CancelPending` or `CancelSubmitted` — the order is
  already being cancelled. Approach B leaves the core race unaddressed.
- `Account.Change()` on AddOn-created `PTT-STP-Drag` is untested for this scenario (NT8-K-004 confirms
  Change() on Working *entry* orders, not stop-drag orders). Introduces new risk.
- Codebase convention: `NT8_ADDON_KNOWLEDGE.md` L462-463 establishes cancel+resubmit as the
  architectural choice. `CopyEngine.cs` L2253, L4361 confirm `acc.Change()` is a no-op for ATM brackets;
  all PTT-Drag code uses cancel+resubmit exclusively.

---

## NT8 API Constraint Verification

| API Surface | Status | Source |
|-------------|--------|--------|
| `OrderState.CancelPending` | CONFIRMED | `NT8_FULL_REFERENCE.md` L966, L3368 |
| `OrderState.CancelSubmitted` | CONFIRMED | `NT8_FULL_REFERENCE.md` L971, L3369 |
| `acc.Cancel(Order[])` on CancelPending order | SAFE (idempotent, try/catch absorbs rejection) | `DW-B134-OCO-OBS` OBS-A pattern; existing try/catch L2413-2421 |
| `acc.Orders.ToList()` thread-safe snapshot | CONFIRMED | Existing usage L2401 |
| `Account.Change()` on AddOn-created StopMarket | NOT USED | Approach B rejected |

**KEY NT8 FACTS (embedded per protocol):**
- `AtmStrategyChangeStopTarget()` — StrategyBase-only. NOT used in this AddOn.
- `AtmStrategyCreate()` — StrategyBase-only. NOT used.
- `Account.Change()` — AddOnBase available but: (a) silent no-op on ATM-owned brackets; (b) undefined for CancelPending orders. NOT used in fix.
- `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` — AddOnBase available. **Correct AddOn pattern. Used.**

---

## Component List

### Modified Components

| Component | File | Type | Change |
|-----------|------|------|--------|
| `CancelExistingPttStpDrag` | `CopyEngine.cs` | private method | Refactor: replace inline 3-state condition with `IsPttStpDragCancellable(o)` call |
| `IsPttStpDragCancellable` | `CopyEngine.cs` | private static method | NEW: 5-state predicate helper |
| `IsPttStpDragCancellableTestable` | `CopyEngine.cs` | internal static seam | NEW: xUnit test seam, pure delegation |

### Unchanged Components

| Component | Reason |
|-----------|--------|
| `SyncAtmFollowerBracket` | Block B is NOT modified; fix operates entirely within CancelExistingPttStpDrag |
| `CancelExistingPttStpDragTestable` | Signature unchanged; pure delegation continues |
| `SyncAtmFollowerTarget` | Not involved in stop-drag race |
| `OrderPassesBracketGate` | Not involved |

---

## Method Signatures

### 1. Modified: `CancelExistingPttStpDrag`

```csharp
// CYC=6: base(1) + foreach(1) + if(1) + &&Name(1) + &&Instrument(1) + ?.(1) = 6.
// B139: DW-B152-B fix -- IsPttStpDragCancellable extracted to include CancelPending||CancelSubmitted.
// OrderState filter now covers: Submitted||Working||Accepted||CancelPending||CancelSubmitted.
// acc.Cancel() on CancelPending/CancelSubmitted is idempotent; rejection absorbed by try/catch.
// JS-021: no lock. JS-001: try/catch -- no rethrow. JS-002: void return.
// acc.Orders.ToList(): thread-safe snapshot. ASCII-only. No DateTime.
private void CancelExistingPttStpDrag(Account acc, Order fo)
```

**Body change**: Replace the compound 3-branch `OrderState` test with `IsPttStpDragCancellable(o)`.

**Before** (current):
```csharp
if (
    (
        o.OrderState == OrderState.Submitted
        || o.OrderState == OrderState.Working
        || o.OrderState == OrderState.Accepted
    )
    && o.Name == "PTT-STP-Drag"
    && o.Instrument?.FullName == fo.Instrument?.FullName
)
```

**After** (B139 fix):
```csharp
if (
    IsPttStpDragCancellable(o)
    && o.Name == "PTT-STP-Drag"
    && o.Instrument?.FullName == fo.Instrument?.FullName
)
```

### 2. New: `IsPttStpDragCancellable`

```csharp
// CYC=5: base(1) + ||(1) + ||(1) + ||(1) + ||(1) = 5.
// Pure state predicate -- no side effects. Static.
// Returns true for all non-terminal states where a PTT-STP-Drag may still be cancelled.
// Submitted: order en-route to broker. Working: live in exchange. Accepted: acked by broker.
// CancelPending: cancel dispatched by NT8, not yet acked by broker.
// CancelSubmitted: cancel acked by broker, not yet confirmed by exchange.
// JS-002: bool return, no null. ASCII-only. No DateTime. No lock.
private static bool IsPttStpDragCancellable(Order o) =>
    o.OrderState == OrderState.Submitted
    || o.OrderState == OrderState.Working
    || o.OrderState == OrderState.Accepted
    || o.OrderState == OrderState.CancelPending
    || o.OrderState == OrderState.CancelSubmitted;
```

### 3. New Test Seam: `IsPttStpDragCancellableTestable`

```csharp
// CYC=1: pure delegation to IsPttStpDragCancellable.
// Test seam for xUnit access. InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
internal static bool IsPttStpDragCancellableTestable(Order o) =>
    IsPttStpDragCancellable(o);
```

### 4. Unchanged: `CancelExistingPttStpDragTestable`

```csharp
// CYC=1: pure delegation. Signature unchanged.
internal void CancelExistingPttStpDragTestable(Account acc, Order fo) =>
    CancelExistingPttStpDrag(acc, fo);
```

---

## CYC Analysis

| Method | CYC Before | CYC After | Status |
|--------|-----------|-----------|--------|
| `CancelExistingPttStpDrag` | 7-8 (comment L2395) | 6 | PASS (≤ 8) |
| `IsPttStpDragCancellable` | N/A (new) | 5 | PASS (≤ 8) |
| `IsPttStpDragCancellableTestable` | N/A (new) | 1 | PASS (≤ 8) |
| `CancelExistingPttStpDragTestable` | 1 (unchanged) | 1 | PASS (≤ 8) |
| `SyncAtmFollowerBracket` | 6 (unchanged) | 6 | PASS (≤ 8) |

**CYC detail — `CancelExistingPttStpDrag` after fix:**
```
base(1) + foreach(1) + if(1) + &&Name(1) + &&Instrument(1) + ?.-conditional(1) = CYC 6
```
Two try/catch blocks add 0 McCabe branches each (per codebase convention, confirmed L2326).

**CYC detail — `IsPttStpDragCancellable`:**
```
base(1) + ||(1) + ||(1) + ||(1) + ||(1) = CYC 5
```

---

## Threading Model

- `CancelExistingPttStpDrag` is called from `SyncAtmFollowerBracket` on NT8's order-event thread.
- No `Dispatcher.InvokeAsync` needed — pure order management path, no UI writes.
- `acc.Orders.ToList()` creates a thread-safe snapshot (existing pattern, L2401).
- `IsPttStpDragCancellable` is a pure static predicate — zero shared state, zero threading concerns.
- `acc.Cancel(Order[])` is an NT8 AddOnBase thread-safe API method.
- **No `lock()` anywhere** — JS-021 P0 compliance maintained.

---

## Data Flow

```
NT8 fires TP3-HBC event #N (3-stop ATM, Nth stop-leg)
  -> HandleBracketChange
  -> SyncAtmFollowerBracket(acc, fo, newPrice)
       (1) acc null guard          -> return
       (2) fo null guard           -> return
       (3) IsNoPriceChange guard   -> return (DW-B147/B149, B137 T2)
       (4) CancelExistingPttStpDrag(acc, fo)  [Block A-Prime pre-sweep]
             foreach (acc.Orders.ToList())
               if IsPttStpDragCancellable(o)    <-- B139 FIX: includes CancelPending|CancelSubmitted
               && o.Name == "PTT-STP-Drag"
               && o.Instrument matches
                 try acc.Cancel(o)              <-- idempotent; try/catch absorbs rejection
       (5) Block A: acc.Cancel(fo)             [cancel ATM bracket]
       (6) Block B: acc.CreateOrder + Submit   [place single new PTT-STP-Drag]

With B139 fix applied -- 3-event burst scenario:
  Event #1: no prior PTT-STP-Drag -> Block B places PTT-STP-Drag#1
  Event #2: PTT-STP-Drag#1 is Submitted/Accepted/Working -> cancel called -> put in CancelPending
            Block B places PTT-STP-Drag#2
  Event #3: PTT-STP-Drag#1 in CancelPending   -> IsPttStpDragCancellable=true -> cancel (idempotent)
            PTT-STP-Drag#2 in Submitted/Accepted -> IsPttStpDragCancellable=true -> cancel
            Block B places PTT-STP-Drag#3 (the single correct stop)
  Result: 1 PTT-STP-Drag per follower after burst. DW-B152-B CLOSED.
```

---

## JS Rule Constraints

| Rule | Constraint | This Plan |
|------|-----------|-----------|
| JS-021 | No `lock()` | No lock added. PASS. |
| JS-001 | No throw in hot path | try/catch absorbs all exceptions; no rethrow. PASS. |
| JS-002 | No return null | `IsPttStpDragCancellable` returns bool; `CancelExistingPttStpDrag` void. PASS. |
| JS-033 | No async void | All methods synchronous. PASS. |
| ASCII-only | No Unicode in string literals | "PTT-STP-Drag", all identifiers ASCII. PASS. |
| No DateTime.Now | Use DateTime.UtcNow | No DateTime usage in affected methods. PASS. |
| No FontFamily | No UI elements | Order management path only. PASS. |
| CYC ≤ 8 | Jane Street strict | CancelExistingPttStpDrag=6, IsPttStpDragCancellable=5. PASS. |

---

## Test Plan

**Test file**: `src/PropTraderTools/Tests/B139Tests.cs`
**Framework**: xUnit only. No NUnit. No MSTest.
**Seam**: `CancelExistingPttStpDragTestable` (existing) + `IsPttStpDragCancellableTestable` (new).

---

### T_B139_01 — Single PTT-STP-Drag after 3 stop-leg events (accumulation prevention)

**Spec requirement**: DW-B152-B — no duplicate PTT-STP-Drag after rapid 3-event burst.

**[Fact] method name**: `CancelExistingPttStpDrag_ThreePriorDragsInMixedStates_CancelsAllThree`

**What it asserts**:
- Arrange: FakeAccount with 3 PTT-STP-Drag orders in states {CancelPending, Working, Accepted}
  for the same instrument as `fo`. Plus 1 unrelated order (different name) to confirm filter selectivity.
- Act: `CancelExistingPttStpDragTestable(fakeAcc, fo)`.
- Assert: `fakeAcc.CancelledOrders.Count == 3` (all 3 PTT-STP-Drags cancelled).
- Assert: the unrelated order was NOT cancelled (filter selectivity confirmed).

---

### T_B139_02 — Cancel-in-flight guard fires for CancelPending and CancelSubmitted states

**Spec requirement**: DW-B152-B — CancelPending/CancelSubmitted must be caught by filter.

**[Fact] method name**: `IsPttStpDragCancellable_CancelPendingAndCancelSubmitted_ReturnTrue`

**What it asserts**:
- Arrange: FakeOrder with `OrderState = OrderState.CancelPending`.
- Act: `IsPttStpDragCancellableTestable(order)`.
- Assert: returns `true`.
- Arrange: FakeOrder with `OrderState = OrderState.CancelSubmitted`.
- Assert: returns `true`.

---

### T_B139_03 — Second stop drag moves PTT-STP-Drag without accumulation (regression)

**Spec requirement**: DW-B151 (B137 T4) regression — Working and Accepted filters must still fire.

**[Fact] method name**: `CancelExistingPttStpDrag_WorkingAndAcceptedDrag_CancelsCalled`

**What it asserts**:
- Arrange: FakeAccount with 1 PTT-STP-Drag in `Working` state + 1 in `Accepted` state, same instrument.
- Act: `CancelExistingPttStpDragTestable(fakeAcc, fo)`.
- Assert: `fakeAcc.CancelledOrders.Count == 2`.

---

### T_B139_04 — Terminal states are not cancelled (Cancelled, Filled, Rejected)

**[Fact] method name**: `IsPttStpDragCancellable_TerminalStates_ReturnFalse`

**What it asserts**:
- `IsPttStpDragCancellableTestable` returns `false` for `OrderState.Cancelled`.
- `IsPttStpDragCancellableTestable` returns `false` for `OrderState.Filled`.
- `IsPttStpDragCancellableTestable` returns `false` for `OrderState.Rejected`.

---

### T_B139_05 — Submitted state still caught (DW-B152 partial fix regression)

**[Fact] method name**: `IsPttStpDragCancellable_Submitted_ReturnsTrue`

**What it asserts**:
- `IsPttStpDragCancellableTestable` returns `true` for `OrderState.Submitted`.

---

### T_B139_06 — Working state still caught (DW-B151 regression)

**[Fact] method name**: `IsPttStpDragCancellable_Working_ReturnsTrue`

**What it asserts**:
- `IsPttStpDragCancellableTestable` returns `true` for `OrderState.Working`.

---

### T_B139_07 — Different instrument name does not cancel

**[Fact] method name**: `CancelExistingPttStpDrag_DifferentInstrument_DoesNotCancel`

**What it asserts**:
- Arrange: FakeAccount with 1 PTT-STP-Drag in `Working` state on instrument "MES SEP26".
  `fo.Instrument?.FullName = "NQ SEP26"` (different instrument).
- Act: `CancelExistingPttStpDragTestable(fakeAcc, fo)`.
- Assert: `fakeAcc.CancelledOrders.Count == 0`.

---

## Tickets

### T1 — Implement CancelExistingPttStpDrag B139 Fix

**File**: `src/PropTraderTools/CopyEngine.cs`
**Spec IDs**: DW-B152-B
**Work**:
1. Add `private static bool IsPttStpDragCancellable(Order o)` method (expression-body, CYC=5).
2. Add `internal static bool IsPttStpDragCancellableTestable(Order o)` seam (CYC=1).
3. Modify `CancelExistingPttStpDrag` body: replace inline 3-state condition with `IsPttStpDragCancellable(o)`.
4. Update header comment on `CancelExistingPttStpDrag`: change CYC annotation from 7-8 to 6; add DW-B152-B closure note.

**7-Scan Checklist (SCAN-01 through SCAN-07)**:
- SCAN-01: No `lock()` in modified methods.
- SCAN-02: No `throw` in hot path — only try/catch with StatusUpdate?.Invoke.
- SCAN-03: CYC — `CancelExistingPttStpDrag` = 6 ≤ 8; `IsPttStpDragCancellable` = 5 ≤ 8.
- SCAN-04: ASCII-only — all string literals and identifiers are ASCII.
- SCAN-05: No `DateTime.Now` — no DateTime usage in affected methods.
- SCAN-06: No `return null` — `IsPttStpDragCancellable` returns bool; `CancelExistingPttStpDrag` void.
- SCAN-07: `OrderState.CancelPending` and `OrderState.CancelSubmitted` confirmed in `NT8_FULL_REFERENCE.md` L966/L971.

---

### T2 — Write B139Tests.cs

**File**: `src/PropTraderTools/Tests/B139Tests.cs`
**Spec IDs**: DW-B152-B (T_B139_01, T_B139_02, T_B139_03, T_B139_04, T_B139_05, T_B139_06, T_B139_07)
**Work**:
1. Create `B139Tests.cs` with `[Fact]` methods for all 7 tests above.
2. Use `FakeOrder` (set `OrderState`, `Name`, `Instrument?.FullName`) and `FakeAccount` (capture `Cancel` calls) — follow B137 test seam pattern.
3. Call `CancelExistingPttStpDragTestable` and `IsPttStpDragCancellableTestable` test seams.
4. xUnit only — no NUnit, no MSTest.

**7-Scan Checklist**:
- SCAN-01: No `lock()` in test code.
- SCAN-02: No exception throws in test arrange paths.
- SCAN-03: Each test method CYC ≤ 8.
- SCAN-04: ASCII-only string literals ("PTT-STP-Drag", instrument names).
- SCAN-05: No `DateTime.Now` in test code.
- SCAN-06: No `return null` — void test methods.
- SCAN-07: xUnit `[Fact]` attributes used; no `[Test]` attributes.

---

## DW-B152-B Closure Criteria

| Criterion | Requirement |
|-----------|-------------|
| Code | `CancelExistingPttStpDrag` filter includes `CancelPending \|\| CancelSubmitted` via `IsPttStpDragCancellable` |
| CYC | `CancelExistingPttStpDrag` CYC = 6 ≤ 8; `IsPttStpDragCancellable` CYC = 5 ≤ 8 |
| Tests | T_B139_01 through T_B139_07 all PASS |
| SIM | 3-stop ATM drag: qty=1 PTT-STP-Drag per follower (no accumulation). Sim102/103/104 grid shows no Accepted PTT-STP-Drag after burst. |
| Build | F5 in NinjaTrader 8 clean compile. `ptt-sync-and-verify.ps1` 0 MISMATCH. |

---

## Deferred Backlog (OPEN, Not Closed by B139)

| ID | Title | Status |
|----|-------|--------|
| DW-B141 | Phase C re-confirmation — pending SIM Test A | OPEN (carried forward) |
| DW-B138 | Stop drag confirmed — pending SIM Test B | OPEN (carried forward) |
| B135-DEFER-01 | Gap B — two simultaneous entries | OPEN (carried forward) |
| B135-DEFER-02 | Stale orders multi-session | OPEN (carried forward) |
| DW-B134-OCO-OBS | OBS-A/B/C/D partial-fill race conditions | OPEN (carried forward) |

---

*Produced by ptt-architect, B139 Phase 1.*
