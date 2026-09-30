# B63-LaneA Tickets

**Block**: B63-LaneA
**Phase**: 3 — Ticket Generation
**Written by**: ptt-architect
**Date**: 2026-08-11
**Plan source**: docs/brain/B63-LaneA/02-architecture-plan.md (REVIEW_PASS)
**Review corrections applied**: OBS-01 — CYC=1 updated to CYC=3 throughout (ptt-plan-reviewer 2026-08-11)

---

# Ticket 1 — B63-LaneA: Widen IsWorkingBracket to catch OrderState.Accepted

**Block**: B63-LaneA
**Priority**: P0
**Spec Req IDs**: DW-B63-01 (live confirmed 2026-08-11)
**Architecture plan**: docs/brain/B63-LaneA/02-architecture-plan.md (REVIEW_PASS)
**Reviewer correction applied**: CYC=3 (not 1) per ptt-plan-reviewer OBS-01

## Summary

Fix Gate B in `CopyEngine.cs`: `IsWorkingBracket` misses ATM bracket orders at `OrderState.Accepted`.
Change: widen `OrderState.Working` to `OrderState.Working || OrderState.Accepted`.
Change: `private static` → `internal static` (testability, same pattern as `IsExitSignalName` line 729).
Add 4 xUnit `[Fact]` tests T_B63_01–T_B63_04.

## Method Signature

```
Before:  private static bool IsWorkingBracket(Order order)
After:   internal static bool IsWorkingBracket(Order order)
```

## Exact Diff — CopyEngine.cs (lines 810–814)

**File**: `src/PropTraderTools/CopyEngine.cs`

**BEFORE** (exact current lines 810–814 — verified 2026-08-11):

```csharp
        // CYC=1. Gate predicate for bracket change detection in OnOrderUpdate.
        private static bool IsWorkingBracket(Order order)
        {
            return order.OrderState == OrderState.Working && IsBracketLegStatic(order);
        }
```

**AFTER** (replacement — 9 lines):

```csharp
        // CYC=3. Gate predicate for bracket detection in OnOrderUpdate.
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

**Change inventory (3 lines only — no other lines touched):**

| Line | Change |
|------|--------|
| 810 | Comment updated: `CYC=1` → `CYC=3`; shortened description + B63 rationale + JS rules appended |
| 811 | `private static` → `internal static` |
| 813 | Condition widened: `OrderState.Working && ...` → `(OrderState.Working \|\| OrderState.Accepted) && ...` |

`OnOrderUpdate`, `HandleBracketChange`, `SyncFollowerBracket`, `MirrorOrderUpdate`,
`FindFollowerBracketOrder`, `IsBracketLegStatic` — **all unchanged**.
Both callsites (line 651 `OnOrderUpdate`, line 682 `MirrorOrderUpdate`) automatically benefit.

## Test File

**File**: `tests/PropTraderTools.Tests/CopyEngineTests.cs`

**Status**: File does NOT currently exist. Engineer creates it new.

## 4 Required [Fact] Tests

### T_B63_01 — `IsWorkingBracket_Working_TargetName_ReturnsTrue`

- **Arrange**: Order stub with `OrderState.Working`, `Name="Target1"`
- **Assert**: `CopyEngine.IsWorkingBracket(order) == true`
- **Purpose**: Regression — existing Working behaviour unchanged

### T_B63_02 — `IsWorkingBracket_Accepted_TargetName_ReturnsTrue`

- **Arrange**: Order stub with `OrderState.Accepted`, `Name="Target1"`
- **Assert**: `CopyEngine.IsWorkingBracket(order) == true`
- **Purpose**: The fix — ATM bracket at Accepted now caught by Gate B

### T_B63_03 — `IsWorkingBracket_Accepted_EntryName_ReturnsFalse`

- **Arrange**: Order stub with `OrderState.Accepted`, `Name="Entry"`
- **Assert**: `CopyEngine.IsWorkingBracket(order) == false`
- **Purpose**: Safety Point 1 — entry orders at Accepted not diverted to HandleBracketChange

### T_B63_04 — `IsWorkingBracket_Submitted_TargetName_ReturnsFalse`

- **Arrange**: Order stub with `OrderState.Submitted`, `Name="Target1"`
- **Assert**: `CopyEngine.IsWorkingBracket(order) == false`
- **Purpose**: Only Accepted+Working caught, not all states

### Test Class Skeleton

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

### NT8 Sealed Order Stub Strategy (engineer must resolve — DW-B63-01)

NT8 `Order` is sealed. Engineer must determine the stub approach before writing test bodies.

**Options in preference order** (from plan Section I / DW-B63-01):

1. **Option 1 (preferred)**: Reflection-based property setter.
   Use `typeof(Order).GetProperty("OrderState").SetValue(instance, OrderState.Working)` and
   `typeof(Order).GetProperty("Name").SetValue(instance, "Target1")` if NT8 `Order` properties
   have public setters. Lowest friction; no signature changes.

2. **Option 2**: NT8 test harness assembly, if NinjaTrader provides one.

3. **Option 3**: Extract `IOrderInfo` interface (`OrderState`, `Name`), change
   `IsWorkingBracket` to accept `IOrderInfo`, implement `FakeOrder : IOrderInfo` in tests.
   **Warning**: this changes the method signature — requires re-review by ptt-plan-reviewer
   before execution.

Engineer picks whichever approach compiles. Document chosen approach in `ticket-1-completion.md`.

## 7-Scan Checklist (Layer 1 — engineer contract)

```
[ ] SCAN-01: ASCII-only
    Command: grep -P "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
    Expected: ZERO hits in the changed hunk (lines 810–820)

[ ] SCAN-02: lock() ban
    Command: grep "lock(" src/PropTraderTools/CopyEngine.cs
    Expected: ZERO results (IsWorkingBracket is a static pure predicate -- no lock possible)

[ ] SCAN-03: async void ban
    Command: grep "async void" src/PropTraderTools/CopyEngine.cs
    Expected: ZERO results

[ ] SCAN-04: return null ban
    Command: grep "return null" in IsWorkingBracket body (lines 811--820)
    Expected: ZERO (bool return type -- structurally impossible)

[ ] SCAN-05: CYC check
    Command: python scripts/complexity_audit.py (or grep IsWorkingBracket output)
    Expected: IsWorkingBracket CYC = 3 (compound: || +1, && +1, base 1 = 3)
    Note: CYC=3 is well within the <=8 limit.

[ ] SCAN-06: xUnit only
    Command: grep -n "using NUnit\|using Microsoft.VisualStudio.TestTools" tests/PropTraderTools.Tests/CopyEngineTests.cs
    Expected: ZERO results

[ ] SCAN-07: build clean
    Command: dotnet build src/PropTraderTools/PropTraderTools.csproj
    Expected: 0 errors, 0 new warnings
```

## Acceptance Criteria

- [ ] `IsWorkingBracket` returns `true` for `OrderState.Accepted` + bracket name (T_B63_02 — the fix)
- [ ] `IsWorkingBracket` returns `true` for `OrderState.Working` + bracket name (T_B63_01 — regression)
- [ ] `IsWorkingBracket` returns `false` for `OrderState.Accepted` + non-bracket name (T_B63_03)
- [ ] `IsWorkingBracket` returns `false` for `OrderState.Submitted` + bracket name (T_B63_04)
- [ ] All 7 scans pass to ZERO
- [ ] `dotnet build` 0 errors
- [ ] git commit created with hash reported in `ticket-1-completion.md`

## Commit Message Template

```
fix(ptt): B63 -- Widen IsWorkingBracket to Accepted state; 4 tests [T_B63_01-04]
```
