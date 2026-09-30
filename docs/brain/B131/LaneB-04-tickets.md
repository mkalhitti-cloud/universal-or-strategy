# B131 LaneB — Implementation Tickets

**Status**: TICKETS_COMPLETE
**Defect**: DW-B139
**Phase**: 3 (Ticket Generation)
**Architect**: ptt-architect
**Date**: 2026-08-27
**Plan basis**: docs/brain/B131/LaneB-02-architecture-plan.md (REVIEW_PASS confirmed)

---

## TICKET-B131-LANEB-T2: Cancel prior PTT-TGT-Drag before resubmit in SyncAtmFollowerTarget

**Defect**: DW-B139
**Severity**: P1
**File**: src/PropTraderTools/CopyEngine.cs
**Method**: `SyncAtmFollowerTarget` (~L2262)

---

### Spec Requirement IDs

**DW-B139** — `SyncAtmFollowerTarget` appends a new `PTT-TGT-Drag` Working order on every drag
event without cancelling the previously-created `PTT-TGT-Drag` first. After N drag events N
simultaneous Working `PTT-TGT-Drag` orders exist on the follower account.

---

### Problem

`SyncAtmFollowerTarget` (CopyEngine.cs L2262–2308) processes each target-drag event by (Block A)
cancelling the leader ATM target reference `fo` and (Block B) creating a new `PTT-TGT-Drag` limit
order on the follower account. However, there is no step that cancels previously-created
`PTT-TGT-Drag` orders before Block B fires. On the second and each subsequent drag event a new
`PTT-TGT-Drag` is appended to `acc.Orders` while all prior `PTT-TGT-Drag` orders for the same
instrument remain in Working state. The B130 SIM gate CSV log confirmed 3 simultaneous Working
`PTT-TGT-Drag` orders on Sim102 for the same instrument during one position's lifetime.

---

### Current Code (L2262-2308 — exact source)

```csharp
private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice)
{
    if (acc == null)                          // guard (1) — L2264
        return;
    if (fo == null)                           // guard (2) — L2266
        return;
                                              // L2268 blank
    // Block A -- Cancel only. Independent: if Cancel throws, Block B still runs.
    try
    {
        acc.Cancel(new Order[] { fo });       // cancels LEADER's ATM target ref
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke(acc.Name + ": TGT cancel error: " + ex.Message);
    }

    // Block B -- CreateOrder + Submit only. Runs regardless of Block A outcome.
    try
    {
        var newTarget = acc.CreateOrder(
            fo.Instrument,
            fo.OrderAction,
            OrderType.Limit,
            OrderEntry.Automated,
            TimeInForce.Day,
            fo.Quantity,
            newPrice,
            0,
            "",
            "PTT-TGT-Drag",                  // order name — L2292
            NinjaTrader.Core.Globals.MaxDate,
            (NinjaTrader.Cbi.CustomOrder)null
        );
        if (newTarget == null)               // guard (3) — L2296
        {
            StatusUpdate?.Invoke(acc.Name + ": ATM TGT CreateOrder returned null");
            return;
        }
        acc.Submit(new[] { newTarget });
        StatusUpdate?.Invoke(acc.Name + ": ATM TGT resubmit -> " + newPrice);
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke(acc.Name + ": TGT create error: " + ex.Message);
    }
}
```

**Structure**: null guard (1) → null guard (2) → Block A (cancel `fo`) → Block B (create+submit).
Missing: no sweep to cancel prior follower-side `PTT-TGT-Drag` orders before Block B.

---

### Required Change — Exact Pseudocode

**Location**: After `if (fo == null) return;` (L2266–2267), after the blank line at L2268, before
the `// Block A` comment at L2269. Insert Block A-Prime (~14 lines).

```csharp
// Block A-Prime -- cancel any existing PTT-TGT-Drag for this instrument on the follower.
// Pre-sweep prevents accumulation of stale drag orders on repeated drag events (DW-B139).
// JS-001: try/catch — no rethrow. JS-021: no lock — acc.Orders NT8 callback thread safe.
foreach (var o in acc.Orders.ToList())
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
            StatusUpdate?.Invoke(acc.Name + ": TGT pre-cancel error: " + ex.Message);
        }
    }
}
```

**Block A and Block B remain UNCHANGED — zero modifications to existing lines.**

**Complete method execution order after fix**:
```
[Guard]        acc == null  -> return
[Guard]        fo == null   -> return
[Block A-Prime] foreach sweep acc.Orders -> cancel each Working PTT-TGT-Drag for fo.Instrument
[Block A]      acc.Cancel(fo)  -- cancel leader's ATM target reference
[Block B]      acc.CreateOrder + acc.Submit  -- create new PTT-TGT-Drag at newPrice
```

---

### Method Signatures

No signature change. The fix is internal-only.

```csharp
// UNCHANGED — exact current signature
private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice)
```

---

### CYC Impact

| # | Branch point | Location |
|---|-------------|----------|
| 1 | `if (acc == null)` | L2264 — existing |
| 2 | `if (fo == null)` | L2266 — existing |
| 3 | `catch` in Block A | L2274 — existing |
| 4 | `if (newTarget == null)` | L2296 — existing |
| 5 | `foreach` loop | Block A-Prime — **new** |
| 6 | `o.OrderState == OrderState.Working` | Block A-Prime — **new** |
| 7 | `o.Name == "PTT-TGT-Drag"` | Block A-Prime — **new** |
| 8 | `catch` in Block A-Prime | Block A-Prime — **new** |

**Before**: CYC = 4
**After**: CYC = 4 + 4 = **8**
**Result**: CYC = 8 <= 8 **PASS** — Jane Street strict standard met. No helper extraction required.

---

### 7-Scan Checklist (MANDATORY engineer contract)

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 | JS-021: No `lock()` in new or modified code | **PASS** — `acc.Orders` is NT8 thread-safe; no `lock` statement introduced |
| SCAN-02 | JS-001: No `throw new XxxException(...)` in hot path — all cancels in try/catch | **PASS** — `acc.Cancel` wrapped in try/catch; no rethrow; catch logs via `StatusUpdate` |
| SCAN-03 | CYC <= 8 for all modified methods | **PASS** — before=4, after=8, confirmed at limit |
| SCAN-04 | ASCII-only in all new string literals | **PASS** — `"PTT-TGT-Drag"`, `"TGT pre-cancel error: "` are ASCII-only |
| SCAN-05 | `acc.Cancel(Order[])` correct overload — array form used | **PASS** — `acc.Cancel(new Order[] { o })` identical pattern to existing Block A at L2272 |
| SCAN-06 | Instrument null safety — `?.FullName` used on both sides | **PASS** — `o.Instrument?.FullName == fo.Instrument?.FullName` with `?.` on both operands |
| SCAN-07 | Minimal change scope — only `SyncAtmFollowerTarget` modified in production code | **PASS** — Block A and Block B byte-for-byte unchanged; no other method touched |

---

### Tests Required (xUnit only — no NUnit, no MSTest)

**File**: src/PropTraderTools/Tests/B131Tests.cs
**Class**: `B131LaneBTests` (append as new class; do not modify existing classes in that file)
**Framework**: xUnit `[Fact]` — required by `docs/protocol/TEST_FRAMEWORK_PROTOCOL.md`

---

#### [Fact] B131_DW139_SecondDragCancelsPriorPttTgtDrag

**Scenario**: Second drag event — prior `PTT-TGT-Drag` exists and must be cancelled before new one is created.

**Setup**:
- Create a mock `Account` (`acc`) whose `Orders` collection contains exactly 1 order:
  - `OrderState = OrderState.Working`
  - `Name = "PTT-TGT-Drag"`
  - `Instrument.FullName = "ES 09-26 CME"`
- Create a mock leader order `fo` with `Instrument.FullName = "ES 09-26 CME"`.
- Register call-order tracking on `acc.Cancel(Order[])` and `acc.CreateOrder(...)`.

**Action**:
- Call `SyncAtmFollowerTarget(acc, fo, 4215.75)`.

**Assert**:
- `acc.Cancel` was called with an array containing the pre-existing `PTT-TGT-Drag` order
  (Block A-Prime sweep) BEFORE `acc.CreateOrder` was called.
- Specifically: the first `acc.Cancel` invocation contains the prior `PTT-TGT-Drag` order;
  the second `acc.Cancel` invocation contains `fo` (Block A).
- `acc.CreateOrder` was called exactly once (only one new `PTT-TGT-Drag` created).

---

#### [Fact] B131_DW139_FirstDragCreatesExactlyOnePttTgtDrag

**Scenario**: First drag event — `acc.Orders` is empty; Block A-Prime sweep finds nothing; exactly one `PTT-TGT-Drag` created.

**Setup**:
- Create a mock `Account` (`acc`) whose `Orders` collection is **empty** (no prior `PTT-TGT-Drag`).
- Create a valid mock leader order `fo` with `Instrument.FullName = "ES 09-26 CME"`.
- Track calls to `acc.Cancel(Order[])`, `acc.CreateOrder(...)`, `acc.Submit(...)`.

**Action**:
- Call `SyncAtmFollowerTarget(acc, fo, 4215.75)`.

**Assert**:
- `acc.Cancel` was called exactly **once** (Block A only — sweep found no `PTT-TGT-Drag` to cancel;
  Block A cancels `fo`).
- `acc.CreateOrder` was called exactly once.
- `acc.Submit` was called exactly once.
- No `PTT-TGT-Drag`-named order was passed to `acc.Cancel` from Block A-Prime.

---

#### [Fact] B131_DW139_NoPriorPttTgtDragNoExtraCancels

**Scenario**: `acc.Orders` contains Working orders with different names — sweep must NOT cancel them.

**Setup**:
- Create a mock `Account` (`acc`) whose `Orders` collection contains exactly 2 Working orders:
  - Order 1: `Name = "Target3"`, `OrderState = OrderState.Working`, `Instrument.FullName = "ES 09-26 CME"`
  - Order 2: `Name = "PTT-STP-Drag"`, `OrderState = OrderState.Working`, `Instrument.FullName = "ES 09-26 CME"`
- Create a valid mock leader order `fo` with `Instrument.FullName = "ES 09-26 CME"`.
- Capture all `acc.Cancel(Order[])` calls and log which orders are in each call's array.

**Action**:
- Call `SyncAtmFollowerTarget(acc, fo, 4215.75)`.

**Assert**:
- `acc.Cancel` was called exactly **once** (Block A only — cancels `fo`).
- `"Target3"` order was NOT present in any `acc.Cancel` call array.
- `"PTT-STP-Drag"` order was NOT present in any `acc.Cancel` call array.
- Name filter `o.Name == "PTT-TGT-Drag"` correctly excludes both non-matching orders.

---

### NT8 API Notes

- **`acc.Orders`**: Live order collection for the account in `AddOnBase` context. NT8's internal
  collection is safe to iterate from the NT8 callback thread. Use `.ToList()` to snapshot before
  iterating to prevent `InvalidOperationException` if collection is modified during sweep.
- **`acc.Cancel(Order[])`**: `AddOnBase`-available. Array overload required — matches existing
  Block A pattern at L2272. Confirmed in `docs/standards/NT8_FULL_REFERENCE.md`.
- **`acc.CreateOrder()` + `acc.Submit()`** in Block B: **unchanged**.
- **`acc.Change()`**: NOT used — confirmed no-op on ATM-owned brackets (B129 SIM gate).
- **`AtmStrategyChangeStopTarget()`**: NOT applicable — `StrategyBase`-only, not `AddOnBase`.
- **`AtmStrategyCreate()`**: NOT applicable — `StrategyBase`-only, not `AddOnBase`.

---

### JS Rules

| Rule | Severity | Application | Status |
|------|----------|-------------|--------|
| JS-001 | P0 | `acc.Cancel` in Block A-Prime wrapped in try/catch; no rethrow; catch logs via `StatusUpdate?.Invoke(...)` | **PASS** |
| JS-021 | P0 | No `lock()`. `acc.Orders` iteration is NT8 thread-safe on callback thread. No lock statement. | **PASS** |
| JS-002 | P0 | Void method — no null returned | **PASS** |
| JS-033 | P0 | Method remains `private void` — no `async void` introduced | **PASS** |

No other JS rules are violated by this change.

---

### Definition of Done

- [ ] Block A-Prime inserted at correct location in `SyncAtmFollowerTarget`:
      after `if (fo == null) return;` (L2266–2267), before `// Block A` comment (L2269)
- [ ] Block A (L2269–2277) byte-for-byte unchanged
- [ ] Block B (L2279–2307) byte-for-byte unchanged
- [ ] CYC of `SyncAtmFollowerTarget` <= 8 (verify with `python scripts/complexity_audit.py`)
- [ ] 3 xUnit `[Fact]` tests in `B131LaneBTests` class, all pass (`dotnet test`)
- [ ] No compilation errors (`dotnet build`)
- [ ] No new `lock()` anywhere in modified file (`grep -n "lock(" src/PropTraderTools/CopyEngine.cs`)
- [ ] No Unicode in new code (`grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs`)
- [ ] `powershell -File scripts\ptt-sync-and-verify.ps1` shows 0 MISMATCH lines
- [ ] F5 in NinjaTrader 8 compiles green
- [ ] `docs/brain/B131/LaneB-ticket-2-completion.md` written after implementation

---

## Completion Gate

- [x] LaneB-02-architecture-plan.md read (REVIEW_PASS confirmed)
- [x] LaneB-02-plan-review.md read (all R01–R12 PASS, no violations)
- [x] CopyEngine.cs L2262–2308 read (exact source confirmed; Block A at L2269, Block B at L2279)
- [x] RULES_CATALOG.md read (JS-001 P0, JS-021 P0 verified)
- [x] Ticket-2 written with all required sections
- [x] 7-scan checklist (SCAN-01 through SCAN-07) included
- [x] 3 xUnit [Fact] tests with full Setup/Action/Assert
- [x] File written: docs/brain/B131/LaneB-04-tickets.md

**Return: TICKETS_COMPLETE**
