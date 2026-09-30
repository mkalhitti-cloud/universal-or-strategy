# B139 Tickets

**Block**: B139
**Phase**: 3 (Ticket Generation)
**Produced by**: ptt-architect
**Plan source**: `docs/brain/B139/02-architecture-plan.md` (REVIEW_PASS)
**Date**: 2026-09-01
**Spec requirement closed**: DW-B152-B

---

## T1 — Implement CancelExistingPttStpDrag B139 Fix

### Spec Requirements Satisfied

| ID | Title |
|----|-------|
| DW-B152-B | Cancel-in-flight race in SyncAtmFollowerBracket Block B -- CancelPending/CancelSubmitted gap |

### File

```
src/PropTraderTools/CopyEngine.cs
```

---

### Work Description

Add `IsPttStpDragCancellable` private static predicate helper and `IsPttStpDragCancellableTestable`
test seam. Refactor `CancelExistingPttStpDrag` body to call the new helper instead of the inline
3-state `OrderState` condition. Update the header comment on `CancelExistingPttStpDrag` to reflect
CYC=6 and the DW-B152-B closure.

**This ticket does NOT touch**:
- `SyncAtmFollowerBracket` (Block B is NOT modified)
- `CancelExistingPttStpDragTestable` (signature unchanged; pure delegation continues)
- Any other method in `CopyEngine.cs`

---

### Method Signatures to Implement

#### 1. New private static helper — `IsPttStpDragCancellable`

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

#### 2. New internal test seam — `IsPttStpDragCancellableTestable`

```csharp
// CYC=1: pure delegation to IsPttStpDragCancellable.
// Test seam for xUnit access. InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
internal static bool IsPttStpDragCancellableTestable(Order o) =>
    IsPttStpDragCancellable(o);
```

#### 3. Modified method — `CancelExistingPttStpDrag`

**Signature** (unchanged):
```csharp
private void CancelExistingPttStpDrag(Account acc, Order fo)
```

**Header comment update** — change CYC annotation and add DW-B152-B note:
```csharp
// CYC=6: base(1) + foreach(1) + if(1) + &&Name(1) + &&Instrument(1) + ?.(1) = 6.
// B139: DW-B152-B fix -- IsPttStpDragCancellable extracted to include CancelPending||CancelSubmitted.
// OrderState filter now covers: Submitted||Working||Accepted||CancelPending||CancelSubmitted.
// acc.Cancel() on CancelPending/CancelSubmitted is idempotent; rejection absorbed by try/catch.
// JS-021: no lock. JS-001: try/catch -- no rethrow. JS-002: void return.
// acc.Orders.ToList(): thread-safe snapshot. ASCII-only. No DateTime.
```

**Body change** — replace the compound inline 3-state condition:

BEFORE (current source):
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

AFTER (B139 fix):
```csharp
if (
    IsPttStpDragCancellable(o)
    && o.Name == "PTT-STP-Drag"
    && o.Instrument?.FullName == fo.Instrument?.FullName
)
```

#### 4. Unchanged — `CancelExistingPttStpDragTestable`

This seam MUST remain exactly as-is. Do NOT modify:
```csharp
// CYC=1: pure delegation. Signature unchanged.
internal void CancelExistingPttStpDragTestable(Account acc, Order fo) =>
    CancelExistingPttStpDrag(acc, fo);
```

---

### JS Rule Constraints

| Rule | Constraint | Enforcement |
|------|-----------|-------------|
| JS-021 | No `lock()` anywhere in modified methods | SCAN-1: grep `lock(` in `CopyEngine.cs` — zero results |
| JS-001 | No `throw` or `rethrow` in hot path | SCAN-2: only try/catch with `StatusUpdate?.Invoke`; no rethrow |
| JS-002 | No `return null` in non-factory methods | `IsPttStpDragCancellable` returns `bool`; `CancelExistingPttStpDrag` is `void` |
| JS-033 | No `async void` | All methods synchronous — no `async` keyword added |
| JS-036 | No `new byte[]` in hot path | No byte array allocation in proposed methods |
| ASCII-only | No Unicode in string literals | `"PTT-STP-Drag"` and all identifiers are ASCII |
| No DateTime.Now | Use `DateTime.UtcNow` if needed | No `DateTime` usage in affected methods |

---

### NT8 API Verification

| API Surface | Status | Source |
|-------------|--------|--------|
| `OrderState.CancelPending` | CONFIRMED valid enum member | `NT8_FULL_REFERENCE.md` L966, L3368 |
| `OrderState.CancelSubmitted` | CONFIRMED valid enum member | `NT8_FULL_REFERENCE.md` L971, L3369 |
| `acc.Cancel(Order[])` on `CancelPending` order | SAFE — idempotent; rejection absorbed by existing try/catch | `DW-B134-OCO-OBS` OBS-A pattern; existing try/catch at L2413–2421 |
| `AtmStrategyChangeStopTarget()` | NOT USED — StrategyBase-only | Confirmed NOT AddOnBase |
| `Account.Change()` | NOT USED — no-op on ATM brackets, undefined for CancelPending | Approach B rejected |

---

### 7-SCAN CHECKLIST

Engineer MUST run all 7 scans to zero before marking BUILD_PASS:

```
[ ] SCAN-1: lock() grep — zero results in modified files
    Command: grep -n "lock(" src/PropTraderTools/CopyEngine.cs

[ ] SCAN-2: throw/rethrow in hot path — zero results
    Command: grep -n "throw " src/PropTraderTools/CopyEngine.cs | grep -v "//"
    (only existing try/catch with StatusUpdate?.Invoke permitted -- no rethrow)

[ ] SCAN-3: return null — zero results in non-factory methods
    Command: grep -n "return null" src/PropTraderTools/CopyEngine.cs
    (IsPttStpDragCancellable returns bool; CancelExistingPttStpDrag is void)

[ ] SCAN-4: CYC audit — all modified methods <= 8
    CancelExistingPttStpDrag = 6 (base+foreach+if+&&Name+&&Instrument+?.) PASS
    IsPttStpDragCancellable  = 5 (base+4x||)                              PASS
    IsPttStpDragCancellableTestable = 1 (delegation)                      PASS

[ ] SCAN-5: ASCII-only audit — zero non-ASCII in string literals
    Command: grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs

[ ] SCAN-6: NT8 API correctness — no banned API used
    Confirm: no AtmStrategyCreate(), no AtmStrategyChangeStopTarget(), no Account.Change()
    in CancelExistingPttStpDrag or IsPttStpDragCancellable.
    Confirm: OrderState.CancelPending and OrderState.CancelSubmitted present.

[ ] SCAN-7: Build passes (dotnet build) — zero errors
    Command: dotnet build src/PropTraderTools/PropTraderTools.csproj
    Then: powershell -File scripts\ptt-sync-and-verify.ps1 (0 MISMATCH lines required)
```

---

## T2 — Write B139Tests.cs

### Spec Requirements Satisfied

| ID | Title |
|----|-------|
| DW-B152-B | Test coverage — T_B139_01 through T_B139_07 |

### File

```
src/PropTraderTools/Tests/B139Tests.cs
```

---

### Work Description

Create `B139Tests.cs` with 7 `[Fact]` test methods. Use the `FakeOrder` / `FakeAccount` seam
pattern established in `B137Tests.cs`. Call the test seams `CancelExistingPttStpDragTestable`
and `IsPttStpDragCancellableTestable` exposed by `CopyEngine.cs`. xUnit only — no NUnit,
no MSTest, no `[Test]` attributes.

---

### Test Seams Used

| Seam | Declared in | Access |
|------|------------|--------|
| `CancelExistingPttStpDragTestable(Account acc, Order fo)` | `CopyEngine.cs` | `internal` — via `InternalsVisibleTo("PropTraderTools.Tests")` at L46 |
| `IsPttStpDragCancellableTestable(Order o)` | `CopyEngine.cs` | `internal static` — via same `InternalsVisibleTo` |

---

### 7 [Fact] Test Methods to Implement

Each method must be a `[Fact]`-decorated `public void` with the EXACT name shown.

---

#### T_B139_01 — Accumulation prevention: 3 prior PTT-STP-Drags in mixed states all cancelled

**Exact method name**:
```csharp
[Fact]
public void CancelExistingPttStpDrag_ThreePriorDragsInMixedStates_CancelsAllThree()
```

**Arrange**:
- `FakeAccount` with 4 orders:
  - `PTT-STP-Drag`, `OrderState.CancelPending`, instrument = `"MES SEP26"`
  - `PTT-STP-Drag`, `OrderState.Working`, instrument = `"MES SEP26"`
  - `PTT-STP-Drag`, `OrderState.Accepted`, instrument = `"MES SEP26"`
  - One unrelated order with a different `Name` (e.g. `"PTT-TGT-Drag"`), `OrderState.Working`, instrument = `"MES SEP26"`
- `fo.Instrument?.FullName = "MES SEP26"`

**Act**:
```csharp
_engine.CancelExistingPttStpDragTestable(fakeAcc, fo);
```

**Assert**:
```csharp
Assert.Equal(3, fakeAcc.CancelledOrders.Count);
Assert.DoesNotContain(unrelatedOrder, fakeAcc.CancelledOrders);
```

---

#### T_B139_02 — CancelPending and CancelSubmitted states return true from predicate

**Exact method name**:
```csharp
[Fact]
public void IsPttStpDragCancellable_CancelPendingAndCancelSubmitted_ReturnTrue()
```

**Arrange / Act / Assert** (two sub-cases in one [Fact]):
```csharp
var orderCP = MakeFakeOrder(OrderState.CancelPending);
var orderCS = MakeFakeOrder(OrderState.CancelSubmitted);

Assert.True(CopyEngine.IsPttStpDragCancellableTestable(orderCP));
Assert.True(CopyEngine.IsPttStpDragCancellableTestable(orderCS));
```

---

#### T_B139_03 — Working and Accepted orders are still cancelled (DW-B151 regression)

**Exact method name**:
```csharp
[Fact]
public void CancelExistingPttStpDrag_WorkingAndAcceptedDrag_CancelsCalled()
```

**Arrange**:
- `FakeAccount` with 2 orders:
  - `PTT-STP-Drag`, `OrderState.Working`, instrument = `"MES SEP26"`
  - `PTT-STP-Drag`, `OrderState.Accepted`, instrument = `"MES SEP26"`
- `fo.Instrument?.FullName = "MES SEP26"`

**Act**:
```csharp
_engine.CancelExistingPttStpDragTestable(fakeAcc, fo);
```

**Assert**:
```csharp
Assert.Equal(2, fakeAcc.CancelledOrders.Count);
```

---

#### T_B139_04 — Terminal states (Cancelled, Filled, Rejected) return false from predicate

**Exact method name**:
```csharp
[Fact]
public void IsPttStpDragCancellable_TerminalStates_ReturnFalse()
```

**Assert** (three sub-cases in one [Fact]):
```csharp
Assert.False(CopyEngine.IsPttStpDragCancellableTestable(MakeFakeOrder(OrderState.Cancelled)));
Assert.False(CopyEngine.IsPttStpDragCancellableTestable(MakeFakeOrder(OrderState.Filled)));
Assert.False(CopyEngine.IsPttStpDragCancellableTestable(MakeFakeOrder(OrderState.Rejected)));
```

---

#### T_B139_05 — Submitted state returns true (DW-B152 partial fix regression)

**Exact method name**:
```csharp
[Fact]
public void IsPttStpDragCancellable_Submitted_ReturnsTrue()
```

**Assert**:
```csharp
Assert.True(CopyEngine.IsPttStpDragCancellableTestable(MakeFakeOrder(OrderState.Submitted)));
```

---

#### T_B139_06 — Working state returns true (DW-B151 regression)

**Exact method name**:
```csharp
[Fact]
public void IsPttStpDragCancellable_Working_ReturnsTrue()
```

**Assert**:
```csharp
Assert.True(CopyEngine.IsPttStpDragCancellableTestable(MakeFakeOrder(OrderState.Working)));
```

---

#### T_B139_07 — Different instrument does not match filter (instrument selectivity)

**Exact method name**:
```csharp
[Fact]
public void CancelExistingPttStpDrag_DifferentInstrument_DoesNotCancel()
```

**Arrange**:
- `FakeAccount` with 1 order:
  - `PTT-STP-Drag`, `OrderState.Working`, instrument = `"MES SEP26"`
- `fo.Instrument?.FullName = "NQ SEP26"` (different instrument)

**Act**:
```csharp
_engine.CancelExistingPttStpDragTestable(fakeAcc, fo);
```

**Assert**:
```csharp
Assert.Equal(0, fakeAcc.CancelledOrders.Count);
```

---

### Test Infrastructure Pattern

Follow the `FakeOrder` / `FakeAccount` pattern from `B137Tests.cs`:

```csharp
// FakeOrder: implements/subclasses Order or wraps it.
// Must expose settable: OrderState, Name, Instrument (with FullName).
private static Order MakeFakeOrder(OrderState state, string name = "PTT-STP-Drag", string instrument = "MES SEP26")
{
    // construct FakeOrder with state, name, instrument as used in B137Tests.cs
}

// FakeAccount: captures Cancel() calls.
// Must expose: CancelledOrders list populated when Cancel(Order) is invoked.
```

**File header** (mandatory):
```csharp
// B139Tests.cs
// xUnit tests for DW-B152-B: CancelPending/CancelSubmitted gap in CancelExistingPttStpDrag.
// Framework: xUnit only. No NUnit. No MSTest.
// Seams: CancelExistingPttStpDragTestable, IsPttStpDragCancellableTestable.
using Xunit;
using NinjaTrader.Cbi;
using PropTraderTools;
```

---

### JS Rule Constraints

| Rule | Constraint | Enforcement |
|------|-----------|-------------|
| JS-021 | No `lock()` in test code | SCAN-1: grep `lock(` in `B139Tests.cs` — zero results |
| JS-001 | No throw in arrange paths | No exception throws during test arrangement |
| JS-002 | No `return null` in test helpers | `MakeFakeOrder` returns a concrete object, never null |
| ASCII-only | No Unicode in string literals | `"PTT-STP-Drag"`, `"MES SEP26"`, `"NQ SEP26"` are ASCII |
| No DateTime.Now | N/A | No DateTime usage in test methods |
| xUnit-only | `[Fact]` attributes; no `[Test]` | SCAN-7: grep `[Test]` in `B139Tests.cs` — zero results |

---

### 7-SCAN CHECKLIST

Engineer MUST run all 7 scans to zero before marking BUILD_PASS:

```
[ ] SCAN-1: lock() grep — zero results in test file
    Command: grep -n "lock(" src/PropTraderTools/Tests/B139Tests.cs

[ ] SCAN-2: throw in test arrange paths — zero results
    Command: grep -n "throw " src/PropTraderTools/Tests/B139Tests.cs | grep -v "//"

[ ] SCAN-3: return null — zero results
    Command: grep -n "return null" src/PropTraderTools/Tests/B139Tests.cs

[ ] SCAN-4: CYC audit — all test methods <= 8
    Each [Fact] method: Arrange/Act/Assert pattern. Max 2-3 Assert calls per method.
    No complex branching in test bodies. CYC = 1 per test method.

[ ] SCAN-5: ASCII-only audit — zero non-ASCII in string literals
    Command: grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Tests/B139Tests.cs

[ ] SCAN-6: NT8 API correctness — only confirmed OrderState enum members used
    OrderState.CancelPending    -- confirmed NT8_FULL_REFERENCE.md L966
    OrderState.CancelSubmitted  -- confirmed NT8_FULL_REFERENCE.md L971
    OrderState.Submitted        -- confirmed
    OrderState.Working          -- confirmed
    OrderState.Accepted         -- confirmed
    OrderState.Cancelled        -- confirmed
    OrderState.Filled           -- confirmed
    OrderState.Rejected         -- confirmed

[ ] SCAN-7: Build passes (dotnet build) — zero errors
    Command: dotnet build src/PropTraderTools/PropTraderTools.csproj
    Confirm: 7 [Fact] tests discovered and pass via: dotnet test src/PropTraderTools/
    Confirm: NO [Test] attributes present (NUnit/MSTest banned)
```

---

## DW-B152-B Closure Criteria (Engineer Verification Gate)

Both T1 and T2 must be complete before closing DW-B152-B:

| Criterion | Requirement | Verified by |
|-----------|-------------|-------------|
| Code | `CancelExistingPttStpDrag` filter includes `CancelPending \|\| CancelSubmitted` via `IsPttStpDragCancellable` | T1 SCAN-6 |
| CYC | `CancelExistingPttStpDrag` CYC=6; `IsPttStpDragCancellable` CYC=5 | T1 SCAN-4 |
| Tests | T_B139_01 through T_B139_07 all PASS (7/7) | T2 SCAN-7 |
| Build | F5 in NinjaTrader 8 clean compile | T1 + T2 SCAN-7 |
| Sync | `ptt-sync-and-verify.ps1` reports 0 MISMATCH lines | Post-build step |
| SIM | 3-stop ATM drag: qty=1 PTT-STP-Drag per follower (no accumulation) | Manual SIM gate |

---

*Produced by ptt-architect, B139 Phase 3. TICKETS_COMPLETE.*
