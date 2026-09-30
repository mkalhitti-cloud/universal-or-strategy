# PTT-COPIER-B25 Lane A — Architecture Plan
**Status**: REVIEW_PASS  
**Block**: PTT-COPIER-B25  
**Lane**: A (parallel — no overlap with Lane B)  
**Defect**: DW-B25-01  
**Author**: ptt-architect  
**[Fact] Baseline Entering B25**: 128  
**[Fact] Target After Lane A**: 131 (+3)  
**F5 Status Entering B25**: GREEN  

---

## 1. Block / Lane / Defect Summary

| Field | Value |
|---|---|
| Block | PTT-COPIER-B25 |
| Lane | A |
| Defect ID | DW-B25-01 |
| Component | `CopyEngine.cs` — `MoveStopToBreakEven`, `IsStopLeg` |
| Test file | `CopyEngineTests.cs` |
| Symptom | ATM bracket stops silently skipped by Move-Stop-to-Break-Even |
| Root type | Missing `OrderType.StopLimit` arm in gate 4 + `IsStopLeg` name mismatch |
| Scope | 3 surgical edits, 3 new `[Fact]` tests |

---

## 2. Root Cause Analysis

### ATM bracket stop anatomy

ATM Strategy brackets create stop orders with the following properties:

| Property | ATM bracket stop value |
|---|---|
| `order.OrderType` | `OrderType.StopLimit` |
| `order.Name` | `"12s Buy STP"` (instrument + direction + "STP" suffix) |
| `order.FromEntrySignal` | `null` (no entry signal linkage for ATM orders) |

### Gate 4 miss — `MoveStopToBreakEven` L1152

Gate 4 in `MoveStopToBreakEven` currently reads:

```csharp
if (order.OrderType != OrderType.StopMarket)   // (5)
    continue;
```

This unconditionally skips every `StopLimit` order. ATM bracket stops are `StopLimit` →
**every ATM stop is silently discarded without reaching `acc.Change()`**.

The fix is already established as a precedent in the codebase. `TightenStop` at L1234-1235 uses
the two-type OR pattern:

```csharp
if (order.OrderType != OrderType.StopMarket &&
    order.OrderType != OrderType.StopLimit)
    continue;
```

### `IsStopLeg` miss — L1090-1093

`IsStopLeg` currently classifies an order as a stop leg by:

1. `order.FromEntrySignal != null` — false for ATM orders  
2. `order.Name.StartsWith("Stop")` — false for `"12s Buy STP"` (no "Stop" prefix)

Both arms return `false` for ATM bracket stops. Even if gate 4 is fixed, any upstream caller
that gates on `IsStopLeg` would still exclude ATM stops.

**Fix**: add a third arm — `order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase)`.

### `acc.Change()` safety on `StopLimit`

NT8 `acc.Change()` on a `StopLimit` order is safe. NT8 recalculates the `LimitPrice` from the
original price offset when `StopPrice` changes. Precedent: `TightenStop` calls `acc.Change()`
on `StopLimit` orders identically. No special handling required.

---

## 3. Exact Edits

### Edit 1 — `CopyEngine.cs` gate 4 fix (`MoveStopToBreakEven` ~L1152)

**File**: `src/PropTraderTools/CopyEngine.cs`

**BEFORE**:
```csharp
if (order.OrderType != OrderType.StopMarket)                               // (5)
    continue;
```

**AFTER**:
```csharp
// DW-B25-01: accept StopLimit (ATM bracket) as well as StopMarket (direct).
// Precedent: TightenStop L1234-1235 uses this exact two-type pattern.
// acc.Change() on StopLimit is safe -- NT8 recalculates LimitPrice from original offset.
if (order.OrderType != OrderType.StopMarket &&                             // (5)
    order.OrderType != OrderType.StopLimit)
    continue;
```

**Why**: The single-type guard silently discards all `StopLimit` ATM bracket stops.
The two-type OR guard mirrors the established `TightenStop` pattern.

---

### Edit 2 — `CopyEngine.cs` diagnostic log for `StopLimit` path (~L1164)

**File**: `src/PropTraderTools/CopyEngine.cs`

**Location**: Inside the `try` block, AFTER the `IsTrailingStop` log line (~L1164),
BEFORE the `order.StopPrice` assignment.

**ADD** (new lines):
```csharp
if (order.OrderType == OrderType.StopLimit)
    StatusUpdate?.Invoke(acc.Name + ": MoveStopToBreakEven: StopLimit bracket stop -> acc.Change");
```

**Why**: Provides an observable signal for test T_B25_01 to assert against.
Uses the existing `StatusUpdate` delegate pattern — no new dependencies introduced.
`?.Invoke` is null-safe; no throw risk.

---

### Edit 3 — `CopyEngine.cs` `IsStopLeg` hardening (~L1090-1093)

**File**: `src/PropTraderTools/CopyEngine.cs`

**BEFORE**:
```csharp
private bool IsStopLeg(Order order)
{
    return order.FromEntrySignal != null || (order.Name != null && order.Name.StartsWith("Stop"));
}
```

**AFTER**:
```csharp
// B25 T1 -- DW-B25-01: ATM bracket stops use name format "12s Buy STP".
// FromEntrySignal is null for ATM orders. No "Stop" prefix. STP suffix is the only discriminator.
// CYC: 2 + 1 (STP clause) = 3. OrdinalIgnoreCase: consistent with WireLeaderAccount (B24 Lane A).
private bool IsStopLeg(Order order)
{
    return order.FromEntrySignal != null
        || (order.Name != null && order.Name.StartsWith("Stop"))
        || (order.Name != null && order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase));
}
```

**Why**: Adds the `STP` suffix arm as the sole discriminator available for ATM bracket stops.
`OrdinalIgnoreCase` is consistent with the `WireLeaderAccount` comparison pattern introduced in B24 Lane A.

---

## 4. Test Specifications

**File**: `src/PropTraderTools/CopyEngineTests.cs`  
**Insertion point**: After last B24 test at L2303, before closing braces.  
**Framework**: xUnit `[Fact]` — no NUnit, no MSTest.

---

### T_B25_01 — `T_B25_01_MoveStopToBreakEven_StopLimitBracket_MovesStop`

**Asserts**: `StatusUpdate` fires the diagnostic message for the `StopLimit` path.

**Scenario**:
- Construct a mock `Order` with `OrderType = OrderType.StopLimit`, `Name = "12s Buy STP"`,
  `FromEntrySignal = null`, `OrderState = OrderState.Working`.
- Wire `CopyEngine.StatusUpdate` to capture invocation strings.
- Call the `MoveStopToBreakEven` path (or the method directly if accessible via test harness).

**Assert**:
```csharp
Assert.Contains(captured, s => s.Contains("StopLimit bracket stop -> acc.Change"));
```

**Validates**: Edit 1 (gate 4 passes `StopLimit`) + Edit 2 (diagnostic log fires).

---

### T_B25_02 — `T_B25_02_MoveStopToBreakEven_StopMarket_StillPasses`

**Asserts**: The original `StopMarket` path is not broken (regression guard).

**Scenario**:
- Construct a mock `Order` with `OrderType = OrderType.StopMarket`, `Name = "Stop loss"`,
  `OrderState = OrderState.Working`.
- Wire `CopyEngine.StatusUpdate` to capture invocation strings.
- Exercise the same `MoveStopToBreakEven` path.

**Assert**:
```csharp
Assert.Contains(captured, s => s.Contains("BE moved to"));
```

**Validates**: Edit 1 does not regress the `StopMarket` path.

---

### T_B25_03 — `T_B25_03_IsStopLeg_AtmSTPSuffix_ReturnsTrue`

**Asserts**: `IsStopLeg` returns `true` for an ATM bracket stop with `STP` suffix.

**Scenario**:
- Construct a mock `Order` with `Name = "12s Buy STP"`, `FromEntrySignal = null`.

**Assert**:
```csharp
Assert.True(engine.IsStopLeg(order));
```

**Validates**: Edit 3 (`STP` suffix arm fires correctly).

---

## 5. CYC Analysis

| Method | Before | After | Delta | Ceiling | Status |
|---|---|---|---|---|---|
| `IsStopLeg` | 2 | 3 | +1 | 8 | PASS |
| `MoveStopToBreakEven` | 6 | 7 | +1 | 8 | PASS |

### `IsStopLeg` detail

- Original: 2 branch points (`FromEntrySignal != null` OR; `StartsWith("Stop")` guard)
- Edit 3 adds: 1 additional `||` clause (`EndsWith("STP", ...)` guard)
- New CYC: **3** — well within `<= 8`

### `MoveStopToBreakEven` detail

- Edit 1: replaces `OrderType != StopMarket` (1 branch) with `!= StopMarket && != StopLimit`
  (still 1 branch point — the `continue` branch). No CYC delta from Edit 1.
- Edit 2: adds 1 `if` inside `try` block. CYC delta: **+1**
- Net after both edits: 6 + 1 = **7** — within `<= 8`

---

## 6. NT8 Compatibility Confirmation

| Item | Verdict | Evidence |
|---|---|---|
| `StringComparison.OrdinalIgnoreCase` | SAFE | Available since .NET 2.0; NT8 targets .NET 4.8 |
| StringComparison.OrdinalIgnoreCase — NT8-044 | SAFE | `using System;` confirmed present at CopyEngine.cs file top (added B24 Lane A, verified GREEN F5 baseline entering B25) |
| `acc.Change()` on `StopLimit` | SAFE | `TightenStop` L1234-1235 calls `acc.Change()` on `StopLimit` (established precedent in same file) |
| No `{ get; init; }` | PASS | No new properties introduced |
| No `abstract record` / `sealed record` | PASS | No new types introduced |
| No `volatile` fields | PASS | No new fields introduced |
| No `ImmutableDictionary` | PASS | No new collections introduced |
| No `async void` | PASS | No new async methods |
| `CreateOrder` arg 12 | N/A | No `CreateOrder` calls in scope |
| NT8 UI thread | N/A | `MoveStopToBreakEven` runs on NT8 callback thread; no `Dispatcher.InvokeAsync` needed |

All edits are surgical one-to-three line changes within existing methods. No new classes,
no new using directives, no new NT8 API surface.

---

## 7. JS Rules Confirmation

| Rule | Description | Status |
|---|---|---|
| JS-021 | No `lock()` anywhere | PASS — no lock() in any edited method |
| JS-001 | No `throw` in hot path | PASS — only `StatusUpdate?.Invoke` inside existing `try/catch` |
| JS-002 | No `return null` | PASS — `IsStopLeg` returns `bool`; `MoveStopToBreakEven` returns `void` |
| JS-033 | No `async void` | PASS — no async methods introduced |
| JS-010 | Public constructors | N/A — no new types |
| JS-015 | Unvalidated string types | PASS — `order.Name` null-guarded before use in both old and new clauses |
| JS-036 | No `new byte[]` in hot path | N/A — no byte arrays |
| JS-037 | No `new T[]` without ArrayPool | N/A — no arrays |

Full JS compliance: **PASS** (8/8 applicable rules).

---

## 8. Verification Criteria

The following 6 checks must all pass before Lane A is declared complete:

| # | Check | How to verify |
|---|---|---|
| V1 | F5 compilation green | Open solution in NinjaTrader 8; press F5 — zero errors, zero warnings |
| V2 | `[Fact]` count = 131 | Run `dotnet test`; confirm test count is 131 (baseline 128 + 3 new) |
| V3 | T_B25_01 passes | `T_B25_01_MoveStopToBreakEven_StopLimitBracket_MovesStop` — green |
| V4 | T_B25_02 passes | `T_B25_02_MoveStopToBreakEven_StopMarket_StillPasses` — green (regression guard) |
| V5 | T_B25_03 passes | `T_B25_03_IsStopLeg_AtmSTPSuffix_ReturnsTrue` — green |
| V6 | No P0 JS/NT8 violations | `grep -r "lock(" src/PropTraderTools/` returns zero results; `grep -rn "async void " src/PropTraderTools/` returns zero results |

### 7-Scan Checklist (SCAN-01 through SCAN-07)

All 7 scans must return zero matches before Lane A is declared complete:

| Scan | Pattern | Scope | Expected |
|------|---------|-------|----------|
| SCAN-01 | `lock\s*(` | src/PropTraderTools/*.cs | Zero matches |
| SCAN-02 | `async void ` | src/PropTraderTools/*.cs | Zero matches |
| SCAN-03 | FontFamily override | src/PropTraderTools/*.cs | Zero matches |
| SCAN-04 | `"#[0-9A-Fa-f]{6}"` hardcoded hex colors | src/PropTraderTools/*.cs | Zero matches |
| SCAN-05 | `acc\.CreateOrder` without `PTT-` prefix on signal name | src/PropTraderTools/*.cs | Zero matches |
| SCAN-06 | `DateTime\.Now` (not UtcNow) | src/PropTraderTools/*.cs | Zero matches |
| SCAN-07 | `sealed class.*Window` | src/PropTraderTools/*.cs | Zero matches |

---

## 9. Deferred Backlog Note

The following defects are tracked but are **NOT in scope for Lane A**:

| Defect | Description | Owner |
|---|---|---|
| DW-B24-01 | (deferred from B24) | Lane B or future block |
| DW-B24-02 | (deferred from B24) | Lane B or future block |
| DW-B24-03 | (deferred from B24) | Lane B or future block |

Lane A is strictly scoped to DW-B25-01. Any additional ATM order handling improvements
identified during implementation must be filed as new defect IDs and deferred to B26.

---

*Architecture plan written by ptt-architect. Engineer contract: implement exactly as specified above.
Ticket file (04-tickets.md) will be generated from this plan in Phase 3.*
