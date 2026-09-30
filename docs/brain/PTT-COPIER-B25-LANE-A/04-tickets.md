# PTT-COPIER-B25 Lane A — Tickets
**Status**: TICKETS_COMPLETE  
**Block**: PTT-COPIER-B25  
**Lane**: A  
**Author**: ptt-architect  
**Plan source**: `docs/brain/PTT-COPIER-B25-LANE-A/02-architecture-plan.md` (REVIEW_PASS)  
**[Fact] Baseline Entering B25**: 128  
**[Fact] Target After T1**: 131 (+3)

---

## T1 — DW-B25-01: gate 4 StopLimit fix + IsStopLeg STP hardening

### Spec Requirement IDs
- **DW-B25-01** — ATM bracket stops silently skipped by `MoveStopToBreakEven` due to gate 4
  accepting only `OrderType.StopMarket` and `IsStopLeg` missing `STP` suffix arm.

---

### Files

| Role | Path (Wave workspace: `c:\WSGTA\universal-or-strategy\`) |
|------|----------------------------------------------------------|
| Source | `src/PropTraderTools/CopyEngine.cs` |
| Tests | `src/PropTraderTools/CopyEngineTests.cs` |

---

### Method Signatures

```csharp
// CopyEngine.cs — 3 edits
private void MoveStopToBreakEven(Account acc, Instrument instrument, int bufferTicks)
private bool IsStopLeg(Order order)
```

---

### Exact Edits

> **Engineer contract**: implement these edits verbatim. Do not paraphrase or restructure.

---

#### Edit 1 — Gate 4 two-type OR (`MoveStopToBreakEven` ~L1152)

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

#### Edit 2 — StopLimit diagnostic log inside try block (~L1164)

**File**: `src/PropTraderTools/CopyEngine.cs`

**Location**: Inside the `try` block, AFTER the `IsTrailingStop` log line (~L1164),
BEFORE the `order.StopPrice` assignment.

**ADD** (new lines — insert, not replace):
```csharp
if (order.OrderType == OrderType.StopLimit)
    StatusUpdate?.Invoke(acc.Name + ": MoveStopToBreakEven: StopLimit bracket stop -> acc.Change");
```

**Why**: Provides an observable signal for test T_B25_01 to assert against.
Uses the existing `StatusUpdate` delegate pattern — no new dependencies introduced.
`?.Invoke` is null-safe; no throw risk.

---

#### Edit 3 — `IsStopLeg` EndsWith STP arm (~L1090-1093)

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

### xUnit Tests

**File**: `src/PropTraderTools/CopyEngineTests.cs`  
**Insertion point**: After last B24 test at L2303, before closing braces.  
**Framework**: xUnit `[Fact]` — NO NUnit, NO MSTest.

---

#### T_B25_01 — `T_B25_01_MoveStopToBreakEven_StopLimitBracket_MovesStop`

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

#### T_B25_02 — `T_B25_02_MoveStopToBreakEven_StopMarket_StillPasses`

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

#### T_B25_03 — `T_B25_03_IsStopLeg_AtmSTPSuffix_ReturnsTrue`

**Asserts**: `IsStopLeg` returns `true` for an ATM bracket stop with `STP` suffix.

**Scenario**:
- Construct a mock `Order` with `Name = "12s Buy STP"`, `FromEntrySignal = null`.

**Assert**:
```csharp
Assert.True(engine.IsStopLeg(order));
```

**Validates**: Edit 3 (`STP` suffix arm fires correctly).

---

### JS Rule Constraints

| Rule | Description | Constraint |
|------|-------------|------------|
| JS-021 | No `lock()` anywhere | Engineer MUST confirm `grep -r "lock(" src/PropTraderTools/` returns zero results before commit. No `lock()` introduced by these edits. |
| JS-001 | No `throw` in hot path | All edits are inside existing `try/catch` blocks. No new `throw` statements. `?.Invoke` is null-safe. |
| JS-002 | No `return null` | `IsStopLeg` returns `bool`. `MoveStopToBreakEven` returns `void`. Neither can return `null`. |
| JS-033 | No `async void` | No async methods introduced. `MoveStopToBreakEven` and `IsStopLeg` are synchronous. |
| NT8-044 | `StringComparison.OrdinalIgnoreCase` | SAFE — available since .NET 2.0; NT8 targets .NET 4.8. `using System;` confirmed present at `CopyEngine.cs` file top (verified GREEN F5 baseline entering B25). |

---

### CYC Budget

| Method | Before | After | Delta | Ceiling | Status |
|--------|--------|-------|-------|---------|--------|
| `IsStopLeg` | 2 | 3 | +1 | 8 | PASS |
| `MoveStopToBreakEven` | 6 | 7 | +1 | 8 | PASS |

- `IsStopLeg`: original 2 branches + 1 new `||` clause (Edit 3) = **3**
- `MoveStopToBreakEven`: Edit 1 replaces 1 branch with 1 branch (net 0); Edit 2 adds 1 `if` = **+1** → 6 + 1 = **7**

---

### 7-Scan Checklist

Engineer MUST run all 7 scans before commit. All must return **zero matches**.

| Scan | Pattern | Command | Expected |
|------|---------|---------|----------|
| SCAN-01 | `lock(` | `grep -rn "lock\s*(" src/PropTraderTools/` | Zero matches |
| SCAN-02 | `async void` | `grep -rn "async void " src/PropTraderTools/` | Zero matches |
| SCAN-03 | FontFamily | `grep -rn "FontFamily" src/PropTraderTools/` | Zero matches |
| SCAN-04 | Hardcoded hex colors | `grep -rn '"#[0-9A-Fa-f]\{6\}"' src/PropTraderTools/` | Zero matches |
| SCAN-05 | CreateOrder without PTT- prefix | `grep -rn "CreateOrder" src/PropTraderTools/` (verify all signal names begin with `PTT-`) | Zero bare names |
| SCAN-06 | DateTime.Now | `grep -rn "DateTime\.Now[^U]" src/PropTraderTools/` | Zero matches |
| SCAN-07 | sealed class Window | `grep -rn "sealed class.*Window" src/PropTraderTools/` | Zero matches |

---

### Verification Criteria

All 6 checks must pass before Lane A is declared complete:

| # | Check | How to Verify |
|---|-------|---------------|
| V1 | `[Fact]` count = 131 | `dotnet test` — confirm 131 tests pass (baseline 128 + 3 new) |
| V2 | `MoveStopToBreakEven` CYC = 7 | Complexity audit: `python scripts/complexity_audit.py` — method shows 7 (was 6, +1 for Edit 2 `if`; still ≤ 8) |
| V3 | `IsStopLeg` CYC = 3 | Complexity audit: method shows 3 (was 2, +1 for Edit 3 `||`; still ≤ 8) |
| V4 | Gate 4 shows two-condition form | `grep -A2 "OrderType.StopMarket" src/PropTraderTools/CopyEngine.cs` — confirms `&& order.OrderType != OrderType.StopLimit` on next line |
| V5 | F5 in NinjaTrader = GREEN | Open solution in NinjaTrader 8; press F5 — zero errors, zero warnings |
| V6 | Commit message | `B25 T1: DW-B25-01 gate4 StopLimit+IsStopLeg STP fallback +3 [Fact] 128->131` |

---

### NT8 Compatibility Summary

| Item | Verdict | Evidence |
|------|---------|----------|
| `StringComparison.OrdinalIgnoreCase` | SAFE | .NET 2.0+; NT8 targets .NET 4.8 |
| NT8-044 — `using System;` present | SAFE | Confirmed at file top; GREEN F5 entering B25 |
| `acc.Change()` on `StopLimit` | SAFE | `TightenStop` L1234-1235 is identical precedent in same file |
| No `{ get; init; }` | PASS | No new properties introduced |
| No `abstract record` / `sealed record` | PASS | No new types introduced |
| No `volatile` fields | PASS | No new fields introduced |
| No `ImmutableDictionary` | PASS | No new collections introduced |
| No `async void` | PASS | No new async methods |
| `CreateOrder` arg 12 | N/A | No `CreateOrder` calls in scope |
| NT8 UI thread | N/A | `MoveStopToBreakEven` runs on NT8 callback thread; no `Dispatcher.InvokeAsync` needed |

---

*Ticket written by ptt-architect from REVIEW_PASS plan. Engineer contract: implement exactly as specified above.*
