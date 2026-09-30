# B63-LaneA Architecture Plan

**Block**: B63-LaneA
**Status**: REVIEW_PASS candidate
**Written by**: ptt-architect (Phase 1)
**Date**: 2026-08-11
**Bug Priority**: P0 — fires on every ATM order in sim and live real-time

---

## Section A: Problem Statement

In `src/PropTraderTools/CopyEngine.cs`, the method [`IsWorkingBracket`](src/PropTraderTools/CopyEngine.cs:811)
(line 811–813) is the sole Gate B predicate that intercepts bracket orders and diverts them from
`DispatchCopy` to `HandleBracketChange`.

**Current implementation (line 811–813)**:
```csharp
// CYC=1. Gate predicate for bracket change detection in OnOrderUpdate.
private static bool IsWorkingBracket(Order order)
{
    return order.OrderState == OrderState.Working && IsBracketLegStatic(order);
}
```

Gate B fires at [`OnOrderUpdate`](src/PropTraderTools/CopyEngine.cs:651):
```csharp
// Gate B: bracket drag detection -- divert to HandleBracketChange path
if (IsWorkingBracket(e.Order))
{
    if (e.Order.FromEntrySignal != null)
        PopulateOrderMap(e.Order.FromEntrySignal, e.Order.Account);
    HandleBracketChange(e.Order, matchedRule.Value);
    return;                             // <-- diverts away from DispatchCopy
}

// No bracket -- normal copy dispatch
DispatchCopy(e.Order, matchedRule.Value);
```

**The bug**: `IsWorkingBracket` only returns `true` for `OrderState.Working`. NT8 fires bracket
orders at `OrderState.Accepted` **before** `Working` (100–200 ms earlier in real-time). In sim,
bracket orders may **only** fire `Accepted` and never transition to `Working`.

**Result**: An ATM `Target1` bracket order at `Accepted` state evaluates Gate B as `false`, falls
through to `DispatchCopy`, and is dispatched as a spurious `PTT-Copy` Sell Limit order to all
follower accounts. The bracket was never meant to be copied as a new entry; it should have been
intercepted and routed to `HandleBracketChange` (which would detect that no follower bracket
order exists yet and silently return).

**Callsite also affected**: [`MirrorOrderUpdate`](src/PropTraderTools/CopyEngine.cs:682) line 682
calls `IsWorkingBracket` for the mirror-mode path. Same fix applies.

---

## Section B: Root Cause

`NT8_FULL_REFERENCE.md` line 941–942 and line 1005 (confirmed from file read):

```
* OrderState.Accepted
* Order is accepted by the broker or exchange
```

> **Critical**: In a historical backtest, orders will always reach a "Working" state. In real-time,
> some stop orders may only reach "Accepted" state if they are simulated/held on a broker's server.

`IsWorkingBracket` was authored for backtest parity where `Working` is guaranteed. In real-time
and sim environments, the NT8 lifecycle is:

```
Submitted -> Accepted -> (optionally) Working -> ...
```

Bracket leg orders for ATM strategies in sim may transition directly to `Accepted` and remain
there throughout the bracket's life, never surfacing a `Working` event to `OnOrderUpdate`.

The gate condition `order.OrderState == OrderState.Working` is therefore **incomplete for
real-time**. It must be widened to include `OrderState.Accepted`.

---

## Section C: Proposed Change

**File**: `src/PropTraderTools/CopyEngine.cs`
**Location**: line 811–813
**Change type**: Widen OrderState condition + change access modifier for xUnit testability

### Before (line 810–814):
```csharp
// CYC=1. Gate predicate for bracket change detection in OnOrderUpdate.
private static bool IsWorkingBracket(Order order)
{
    return order.OrderState == OrderState.Working && IsBracketLegStatic(order);
}
```

### After:
```csharp
// CYC=1. Gate predicate for bracket detection in OnOrderUpdate.
// B63: Accepted added -- NT8 bracket orders fire Accepted before (or instead of) Working.
// NT8_FULL_REFERENCE.md line 1005: "some stop orders may only reach Accepted state".
// Extending to Accepted is safe: SyncFollowerBracket price-delta guard absorbs double-fire.
// JS-021: no lock. JS-001: no throw.
internal static bool IsWorkingBracket(Order order)
{
    return (order.OrderState == OrderState.Working
            || order.OrderState == OrderState.Accepted)
           && IsBracketLegStatic(order);
}
```

**Changes**:
1. `private static` → `internal static` (same pattern as `IsExitSignalName` line 729 — xUnit testability without NT8 runtime).
2. Condition widens `OrderState.Working` to `OrderState.Working || OrderState.Accepted`.
3. `IsBracketLegStatic` call unchanged (still required — filters entry orders, PTT-own orders, etc.).
4. CYC remains 1 (compound `||` in a single return expression — one decision point with short-circuit).

**No other changes** to `OnOrderUpdate`, `HandleBracketChange`, `SyncFollowerBracket`,
`MirrorOrderUpdate`, or any other method. All two callsites (`OnOrderUpdate` line 651 and
`MirrorOrderUpdate` line 682) automatically benefit from the widened predicate.

---

## Section D: Safety Analysis

### Point 1 — Entry orders are not caught at `Accepted`

`IsBracketLegStatic` ([`CopyEngine.cs:1519`](src/PropTraderTools/CopyEngine.cs:1519)) returns `true`
only when:
- `order.FromEntrySignal != null` (bracket legs carry a FromEntrySignal from the parent entry), **or**
- `order.Name.StartsWith("Stop")`, **or**
- `order.Name.StartsWith("Target")`, **or**
- `order.Name.StartsWith("PTT-")`

Leader entry orders carry names like `"Entry"`, user-defined signal names (e.g. `"MyEntry"`), or
ATM auto-generated names — none of which start with `"Stop"`, `"Target"`, or `"PTT-"`, and none
of which have a `FromEntrySignal` set (entry orders are the parent, not the child). Entry orders
in `Accepted` state will still evaluate Gate B as `false` and flow to `DispatchCopy` as intended.
**Safe.**

### Point 2 — Follower orders are not caught

Gate 2 in `OnOrderUpdate` (before Gate B) checks:
```
e.Order.Account.Name == rule.MasterAccount?.Name
```
Only leader account orders pass Gate 2. Follower account orders are rejected before Gate B is
ever reached. There is no recursion risk from the follower's own `Accepted`-state bracket legs.
**Safe.**

### Point 3 — Double-fire safety (both `Accepted` and `Working` fire for the same bracket leg)

In real-time, some bracket orders fire both `Accepted` and then `Working`. `IsWorkingBracket` will
return `true` for both events. Both calls reach `HandleBracketChange` → `SyncFollowerBracket`.

[`SyncFollowerBracket`](src/PropTraderTools/CopyEngine.cs:842) line 850 contains a price-delta guard:
```csharp
if (Math.Abs(newPrice - currentPrice) < tickSize)    // (2)
    return;
```
On the second call the bracket price is identical — delta is 0, which is less than `tickSize`.
The guard fires and returns immediately. `acc.Change()` is never invoked a second time.
The `StatusUpdate` string "bracket synced" is also not emitted. **Safe — idempotent.**

### Point 4 — Fresh bracket, follower order does not exist yet at `Accepted`

When `Accepted` fires for a brand-new ATM bracket, the follower account has not yet received the
corresponding bracket order (it arrives when the entry fills on the follower, which may be delayed).
`FindFollowerBracketOrder` will find no match and return `null`.

[`SyncFollowerBracket`](src/PropTraderTools/CopyEngine.cs:846) line 846:
```csharp
var fo = FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop);
if (fo == null)    // (1)
    return;
```
Returns immediately. No `acc.Change()`. No error. The `Working` event (when it fires) will find
the follower bracket by then and perform the sync correctly.
**Safe — early null return.**

---

## Section E: Test Plan

### Test file

`tests/PropTraderTools.Tests/CopyEngineTests.cs` — **new file** (file does not yet exist; no
prior test file found in the repository). The test class mirrors the pattern used for
[`IsExitSignalName`](src/PropTraderTools/CopyEngine.cs:729) which is `internal static` and
directly callable from xUnit without NT8 runtime.

**Key pattern note**: NT8's `Order` type is sealed and cannot be mocked via Moq or NSubstitute.
The `IsWorkingBracket` signature accepts `Order order`. For unit testing we must use a real NT8
`Order` object or an `OrderStub` shim. Since the test file does not yet exist, the engineer must
determine if a `FakeOrder` / `OrderStub` approach is already defined elsewhere or must be
introduced. If no NT8 test harness exists, the engineer should follow the same approach as
any existing `IsExitSignalName` tests (which may use a reflection-based shim or a minimal
`OrderAdapter` wrapper). **This is flagged as a DW item below (DW-B63-01).**

### 4 required `[Fact]` tests

| Tag | Method Name | Arrange | Assert |
|-----|-------------|---------|--------|
| T_B63_01 | `IsWorkingBracket_Working_TargetName_ReturnsTrue` | Order stub: `OrderState.Working`, `Name="Target1"` | Returns `true` (regression — existing behaviour preserved) |
| T_B63_02 | `IsWorkingBracket_Accepted_TargetName_ReturnsTrue` | Order stub: `OrderState.Accepted`, `Name="Target1"` | Returns `true` (the fix — new behaviour) |
| T_B63_03 | `IsWorkingBracket_Accepted_EntryName_ReturnsFalse` | Order stub: `OrderState.Accepted`, `Name="Entry"` | Returns `false` (entry orders not caught — Safety Point 1) |
| T_B63_04 | `IsWorkingBracket_Submitted_TargetName_ReturnsFalse` | Order stub: `OrderState.Submitted`, `Name="Target1"` | Returns `false` (Submitted state not included) |

### Test class skeleton

```csharp
// tests/PropTraderTools.Tests/CopyEngineTests.cs
using NinjaTrader.Cbi;
using PropTraderTools;
using Xunit;

namespace PropTraderTools.Tests;

public class CopyEngineTests
{
    // NOTE: NT8 Order is sealed. Engineer must resolve stub strategy per DW-B63-01.
    // Placeholder shows intent; engineer replaces OrderStub with actual approach.

    [Fact]
    public void IsWorkingBracket_Working_TargetName_ReturnsTrue()
    {
        var order = OrderStub.Create(OrderState.Working, "Target1");
        Assert.True(CopyEngine.IsWorkingBracket(order));
    }

    [Fact]
    public void IsWorkingBracket_Accepted_TargetName_ReturnsTrue()
    {
        var order = OrderStub.Create(OrderState.Accepted, "Target1");
        Assert.True(CopyEngine.IsWorkingBracket(order));
    }

    [Fact]
    public void IsWorkingBracket_Accepted_EntryName_ReturnsFalse()
    {
        var order = OrderStub.Create(OrderState.Accepted, "Entry");
        Assert.False(CopyEngine.IsWorkingBracket(order));
    }

    [Fact]
    public void IsWorkingBracket_Submitted_TargetName_ReturnsFalse()
    {
        var order = OrderStub.Create(OrderState.Submitted, "Target1");
        Assert.False(CopyEngine.IsWorkingBracket(order));
    }
}
```

**Test assertions**: `Assert.True` / `Assert.False` only — no complex matchers, no NUnit, no
MSTest. Framework: xUnit only (per JS-testing mandate and `TEST_FRAMEWORK_PROTOCOL.md`).

---

## Section F: Jane Street Compliance

| Rule | Scope | Verification |
|------|-------|-------------|
| **JS-021** — `lock()` ban | `IsWorkingBracket` is a static pure predicate. No shared mutable state. No `lock()`. | `grep "lock(" src/PropTraderTools/CopyEngine.cs` → must return 0 results in changed lines |
| **JS-001** — No `throw` in hot path | Method returns `bool`. No exception path. | `grep "throw" src/PropTraderTools/CopyEngine.cs:811-817` → 0 matches |
| **JS-002** — No `return null` | Method returns `bool`. `null` cannot be returned. | N/A — type safe |
| **CYC ≤ 8** | `IsWorkingBracket` CYC = 1. Compound `||` in one return expression = one decision point. | `python scripts/complexity_audit.py` — `IsWorkingBracket` must show CYC=1 |
| **ASCII-only** | No new string literals introduced. No new comments with non-ASCII. | `grep` non-ASCII in changed hunk → 0 results |
| **xUnit only** | All 4 `[Fact]` tests use xUnit. No `[Test]` (NUnit), no `[TestMethod]` (MSTest). | Review test file imports |
| **`internal` precedent** | `IsExitSignalName` (line 729) is `internal static` — same pattern. Consistent. | `grep "internal static" src/PropTraderTools/CopyEngine.cs` |
| **No `DateTime.Now`** | No temporal references introduced. | N/A |
| **No FontFamily / hex** | No UI layer touched. | N/A |
| **No Dispatcher** | No UI thread context. Static predicate. | N/A |

---

## Section G: 7-Scan Checklist

Engineer must complete all 7 scans before marking the ticket done.

```
[ ] SCAN-01: ASCII-only
    grep -P "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
    Expected: zero hits in lines 810–817 (the changed hunk)

[ ] SCAN-02: lock() ban
    grep "lock(" src/PropTraderTools/CopyEngine.cs
    Expected: zero new results (existing zero-hit baseline preserved)

[ ] SCAN-03: async void ban
    grep "async void" src/PropTraderTools/CopyEngine.cs
    Expected: zero results

[ ] SCAN-04: return null ban
    grep "return null" in IsWorkingBracket body (lines 811–817)
    Expected: zero results (bool return type makes this trivially satisfied)

[ ] SCAN-05: CYC check
    python scripts/complexity_audit.py -- check IsWorkingBracket
    Expected: CYC = 1

[ ] SCAN-06: xUnit only
    grep -n "using NUnit\|using Microsoft.VisualStudio.TestTools" tests/PropTraderTools.Tests/CopyEngineTests.cs
    Expected: zero results

[ ] SCAN-07: build clean
    dotnet build src/PropTraderTools/PropTraderTools.csproj
    Expected: 0 errors, 0 warnings (new warnings are a fail)
```

---

## Section H: Files Changed

### File 1: `src/PropTraderTools/CopyEngine.cs`

| Location | Change |
|----------|--------|
| Line 810 | Comment updated: "Gate predicate for bracket detection in OnOrderUpdate." (shortened + B63 rationale added) |
| Line 811 | `private static` → `internal static` |
| Line 813 | Condition: `order.OrderState == OrderState.Working && ...` → `(order.OrderState == OrderState.Working \|\| order.OrderState == OrderState.Accepted) && ...` |

**No other lines touched.** `OnOrderUpdate`, `HandleBracketChange`, `SyncFollowerBracket`,
`MirrorOrderUpdate`, `FindFollowerBracketOrder`, `IsBracketLegStatic` — all unchanged.

### File 2: `tests/PropTraderTools.Tests/CopyEngineTests.cs`

**New file** (does not currently exist).

| Content | Description |
|---------|-------------|
| 4 `[Fact]` methods | T_B63_01, T_B63_02, T_B63_03, T_B63_04 as specified in Section E |
| `OrderStub` | Engineer-resolved NT8 Order stub (see DW-B63-01) |
| Namespace | `PropTraderTools.Tests` |
| Framework | xUnit only |

**Total diff surface**: ~10 lines in `CopyEngine.cs` + ~50 lines in new test file.
Well within 10,000-character diff limit per JS PR hygiene mandate.

---

## Section I: Deferred Items

### Carry-Forward from B59-LaneA/06-deferred-backlog.md

The following items remain **OPEN** after B59-LaneA and are carried into B63 context.
B63 **does not target any of these items** — they are listed for reviewer awareness.

| Item | Priority | Status | Target Block |
|------|----------|--------|--------------|
| **DW-B60-01** — Leader manual close does not close follower position | P1 | **OPEN** | B60 (confirmed from live test 2026-08-10) |
| **DW-B59-02** — `IsExitSignalName` uses exact `"Rev"` match instead of prefix | P1 | **OPEN** | B60 |
| **DW-B58-01** — `SnapshotTargetsPublic` hardcoded order-name prefixes | P2 | **OPEN** | future |
| **DW-B58-02** — `GlobalBe` non-atomic lazy init | P2 | **OPEN** | future |
| **DW-B58-03** — `RelayBe` does not forward `OcoGroup` from `BeEventArgs` | P2 | **OPEN** | future |
| **DW-B54-01** — ATM auto-inject (blocked on StrategyBase-level API) | P1 | **OPEN — blocked** | future |
| **PRE-EXISTING-01** — Non-ASCII at CopyEngine.cs lines 395, 496 | P2 | **OPEN** | future |
| **PRE-EXISTING-02** — Non-ASCII at CopyEngine.cs lines 1256, 1257 | P2 | **OPEN** | future |
| **PRE-EXISTING-03** — `deploy-sync.ps1` archived; manual sync workflow | P2 | **OPEN** | future |

### New Deferred Item from B63

#### DW-B63-01 — NT8 `Order` sealed type; xUnit stub strategy undetermined

**Priority**: P1 (must resolve before ticket execution)
**Target block**: B63-LaneA (engineer to resolve during Ticket 1)
**Status**: OPEN

**Description**: NT8's `Order` class is `sealed` (confirmed — cannot be subclassed or mocked via
Moq). `IsWorkingBracket(Order order)` accepts a concrete NT8 `Order`. The test file
`tests/PropTraderTools.Tests/CopyEngineTests.cs` does not yet exist, so no prior stub strategy
is established for this project.

**Options for engineer** (in preference order):
1. **Reflection-based property setter**: Use `typeof(Order).GetProperty("OrderState").SetValue(...)` +
   `typeof(Order).GetProperty("Name").SetValue(...)` to set sealed-type properties directly.
   This is the lowest-friction approach if NT8 `Order` properties have public setters.
2. **NT8 test harness**: If NinjaTrader provides a test-harness assembly, use it.
3. **Wrapper approach**: Extract an `IOrderInfo` interface covering `OrderState` and `Name`,
   change `IsWorkingBracket` to accept `IOrderInfo`, implement a `FakeOrder : IOrderInfo` in
   tests. Note: this changes the method signature and must be reviewed by ptt-plan-reviewer.

**Action required**: Engineer investigates which approach compiles and runs in CI before writing
the test methods.

---

## Appendix: CopyEngine.cs Call Graph (Gate B region)

```
OnOrderUpdate (line ~600)
  └── Gate 2: Account == MasterAccount?
        └── Gate B: IsWorkingBracket(e.Order)   ← THIS CHANGE
              ├── TRUE  → PopulateOrderMap
              │         → HandleBracketChange
              │               └── SyncFollowerBracket  (price-delta guard at line 850)
              │                     └── FindFollowerBracketOrder → null → return early
              └── FALSE → DispatchCopy   ← spurious dispatch of bracket leg (THE BUG)

MirrorOrderUpdate (line ~673)
  └── IsWorkingBracket(masterOrder)             ← THIS CHANGE (same predicate)
        └── TRUE → HandleBracketChange
```

After the fix, `Accepted`-state bracket legs are diverted to `HandleBracketChange` at both
callsites. `SyncFollowerBracket` handles the null-follower case (early return) safely.

---

*Plan status: ready for ptt-plan-reviewer Phase 2.*
