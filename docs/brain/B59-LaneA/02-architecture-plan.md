# B59-LaneA Architecture Plan

**Status**: REVIEW_PASS candidate (Ph1 revision — V-01 + V-02 applied)
**Epic**: B59-LaneA  
**Defect**: DW-B59-01  
**Architect**: ptt-architect (Ph1)  
**Date**: 2026-08-10  
**Output file**: `docs/brain/B59-LaneA/02-architecture-plan.md`

---

## Rules Catalog Gate

```
STEP 0 -- RULES CATALOG GATE (mandatory, non-skippable):
[x] Read docs/standards/jane-street/RULES_CATALOG.md  -- UTF-8 clean, readable
[x] JS-001 (P0): No throw in hot path -- IsExitSignalName returns bool; no exceptions. PASS.
[x] JS-002 (P0): No return null -- method returns bool, never null. PASS.
[x] JS-021 (P0): No lock() -- no lock anywhere in new or modified code. PASS.
[x] JS-CYC: IsExitSignalName CYC=7 (<= 8). DispatchCopy CYC=7 (<= 8) after gate simplification. PASS.
[x] ASCII-only: all string literals ("Close", "Flatten", "Rev", "Exit", "PTT-") are pure ASCII. PASS.
GATE RESULT: PASS
```

---

## 1. Problem Statement

### Root Cause

Gate 0.5 in [`DispatchCopy`](src/PropTraderTools/CopyEngine.cs:727) currently blocks only orders whose
`Name` begins with `"PTT-"`:

```csharp
// Gate 0.5: PTT-prefix guard -- prevents cascade copy of our own PTT- signals. CYC: 7->8.
if (order.Name != null && order.Name.StartsWith("PTT-")) return;
```

This guard is **insufficient** because it does not block NT8 built-in exit signal names.

### NT8 Built-In Exit Order Names (confirmed via `NT8_FULL_REFERENCE.md`)

| NT8 Action | `Order.Name` value | Source |
|---|---|---|
| Close button (market exit) | `"Close"` | NT8_FULL_REFERENCE.md line 845 + workspace AGENTS.md confirmed fact |
| Flatten button / `Account.Flatten()` | `"Flatten"` | NT8_FULL_REFERENCE.md line 358-359 |
| Reversal orders | `"Rev..."` (prefix) | NT8 platform generated reversal name convention |
| Exit strategy signals | `"Exit..."` (prefix) | NT8 ExitLong / ExitShort / ExitAll naming convention |

`Order.Name` is documented as: *"A string representing the name of an order which can be provided by
the entry or exit signal name"* (NT8_FULL_REFERENCE.md line 845).

### Failure Scenario

```
Leader presses Close button in NinjaTrader
  → NT8 creates market order: Name="Close", OrderType=Market, OrderState=Submitted
  → OnOrderUpdate fires → DispatchCopy(order, rule)
  → Gate 0.5: "Close" does not start with "PTT-" → PASSES
  → Gate 3: OrderState.Submitted → PASSES (IsDispatchTriggerState)
  → Gate 4: OrderType.Market → PASSES
  → Gate 5: dedup → PASSES (new order ID)
  → CopySignal dispatched to followers → PHANTOM REVERSAL / unexpected copy
```

The bug is a **false negative** in Gate 0.5: the guard is not broad enough.

---

## 2. Solution

### 2.1 New Helper: `IsExitSignalName`

Extract a dedicated `internal static` predicate that answers: *"Is this order name an NT8
built-in exit signal that must never be forwarded to followers?"*

**Placement**: after [`IsDispatchTriggerState`](src/PropTraderTools/CopyEngine.cs:716) (line 718),
before the `// --- B7-F0: Bracket mirroring methods ---` comment (line 720).

#### Method Signature

```csharp
internal static bool IsExitSignalName(string name)
```

#### Exact Method Body

```csharp
// B59 T1: IsExitSignalName -- blocks NT8 built-in exit orders from follower dispatch.
// CYC=7: null/empty guard (1) + Close (2) + Flatten (3) + Rev-prefix (4) + Exit-prefix (5)
//        + PTT-prefix (6) + false-return base (base=1, total branches=6, CYC=7).
// JS-002: returns bool, never null. ASCII-only string literals. NT8 ref: Order.Name semantics.
// TESTABILITY: internal static -- directly testable as CopyEngine.IsExitSignalName(name).
internal static bool IsExitSignalName(string name)
{
    if (string.IsNullOrEmpty(name))                                   return false;  // guard
    if (name == "Close")                                              return true;   // NT8 Close button
    if (name == "Flatten")                                            return true;   // NT8 Flatten button
    if (name.StartsWith("Rev",  StringComparison.Ordinal))            return true;   // NT8 reversal orders
    if (name.StartsWith("Exit", StringComparison.Ordinal))            return true;   // NT8 exit signals
    if (name.StartsWith("PTT-", StringComparison.Ordinal))            return true;   // PTT own signals
    return false;
}
```

#### CYC Analysis

| Decision point | Count |
|---|---|
| Base CYC | 1 |
| `IsNullOrEmpty` guard | +1 |
| `== "Close"` | +1 |
| `== "Flatten"` | +1 |
| `StartsWith("Rev")` | +1 |
| `StartsWith("Exit")` | +1 |
| `StartsWith("PTT-")` | +1 |
| **Total** | **7** |

CYC = 7 ≤ 8 limit. **PASS.**

#### Design Notes

- The PTT- prefix check is **consolidated** into `IsExitSignalName`. The existing two-condition guard
  `order.Name != null && order.Name.StartsWith("PTT-")` (which was the entirety of Gate 0.5) is
  replaced. This preserves the cascade-copy protection while extending coverage to NT8 exit names.
- `StringComparison.Ordinal` is used for all `StartsWith` calls — fastest comparison, no culture
  overhead, correct for ASCII signal names.
- `string.IsNullOrEmpty` replaces the inline `!= null` check — handles both null and empty string
  in a single branch, reducing noise.

---

### 2.2 Updated Gate 0.5 in `DispatchCopy`

**Location**: [`CopyEngine.cs:727-728`](src/PropTraderTools/CopyEngine.cs:727)

**Before**:
```csharp
// Gate 0.5: PTT-prefix guard -- prevents cascade copy of our own PTT- signals. CYC: 7->8.
if (order.Name != null && order.Name.StartsWith("PTT-")) return;
```

**After**:
```csharp
// Gate 0.5: block NT8 built-in exit signals and PTT- own signals. Delegates to IsExitSignalName.
if (IsExitSignalName(order.Name)) return;
```

#### DispatchCopy CYC After Change

The old gate used a compound `&&` condition = 2 CYC points (1 for `if`, 1 for `&&` short-circuit).
The new gate uses a single method call = 1 CYC point.

DispatchCopy CYC: 8 (before) → 7 (after). Remains ≤ 8. **PASS.**

The method header comment must be updated accordingly:

```csharp
// B8 T1: DispatchCopy -- index-tracking loop replaces plain foreach.
// CYC=7 (Gate 0.5 simplified by IsExitSignalName helper). GetMultiplier + scaled signal per follower.
// JS-001: no throw in hot path. JS-021: no lock.
```

---

## 3. Component List

| Component | File | Action | CYC |
|---|---|---|---|
| `IsExitSignalName(string name)` | `src/PropTraderTools/CopyEngine.cs` | NEW — insert after line 718 | 7 |
| `DispatchCopy(Order, CopyRule)` Gate 0.5 | `src/PropTraderTools/CopyEngine.cs:727-728` | MODIFY — replace 2-line guard with 1-line call | 7 (was 8) |
| `IsExitSignalName` test suite | `src/PropTraderTools/CopyEngineTests.cs` | NEW — 7 `[Fact]` tests appended | N/A |

---

## 4. Test Plan

### Method Under Test

```csharp
CopyEngine.IsExitSignalName(string name)   // internal static -- directly callable without reflection
```

Same testability pattern as [`IsDispatchTriggerState`](src/PropTraderTools/CopyEngineTests.cs:2686):
`internal static` with a primitive (string) parameter → callable directly from xUnit [Fact].

### 7 Test Methods (T_B59_01 through T_B59_07)

#### T_B59_01 — null returns false (guard branch)

```csharp
[Fact]
public void IsExitSignalName_NullName_ReturnsFalse()
{
    Assert.False(CopyEngine.IsExitSignalName(null),
        "null must not be classified as an exit signal name");
}
```

#### T_B59_02 — empty string returns false (guard branch)

```csharp
[Fact]
public void IsExitSignalName_EmptyName_ReturnsFalse()
{
    Assert.False(CopyEngine.IsExitSignalName(string.Empty),
        "empty string must not be classified as an exit signal name");
}
```

#### T_B59_03 — "Close" returns true (NT8 Close button)

```csharp
[Fact]
public void IsExitSignalName_Close_ReturnsTrue()
{
    Assert.True(CopyEngine.IsExitSignalName("Close"),
        "NT8 Close button order name must be blocked");
}
```

#### T_B59_04 — "Flatten" returns true (NT8 Flatten button)

```csharp
[Fact]
public void IsExitSignalName_Flatten_ReturnsTrue()
{
    Assert.True(CopyEngine.IsExitSignalName("Flatten"),
        "NT8 Flatten order name must be blocked");
}
```

#### T_B59_05 — "Rev..." prefix returns true (NT8 reversal orders)

```csharp
[Fact]
public void IsExitSignalName_RevPrefix_ReturnsTrue()
{
    Assert.True(CopyEngine.IsExitSignalName("Rev"),        "bare Rev must be blocked");
    Assert.True(CopyEngine.IsExitSignalName("Reversal"),   "Reversal must be blocked");
    Assert.True(CopyEngine.IsExitSignalName("RevLong"),    "RevLong must be blocked");
}
```

#### T_B59_06 — "Exit..." prefix returns true (NT8 exit signals)

```csharp
[Fact]
public void IsExitSignalName_ExitPrefix_ReturnsTrue()
{
    Assert.True(CopyEngine.IsExitSignalName("Exit"),       "bare Exit must be blocked");
    Assert.True(CopyEngine.IsExitSignalName("ExitLong"),   "ExitLong must be blocked");
    Assert.True(CopyEngine.IsExitSignalName("ExitShort"),  "ExitShort must be blocked");
}
```

#### T_B59_07 — "PTT-..." prefix returns true (cascade-copy protection) AND non-matching name returns false

```csharp
[Fact]
public void IsExitSignalName_PttPrefixBlockedAndNonMatchingPasses()
{
    // PTT- signals must be blocked (cascade-copy protection preserved)
    Assert.True(CopyEngine.IsExitSignalName("PTT-Copy"),        "PTT-Copy must be blocked");
    Assert.True(CopyEngine.IsExitSignalName("PTT-TrimLimit"),   "PTT-TrimLimit must be blocked");
    Assert.True(CopyEngine.IsExitSignalName("PTT-Mirror-Close"),"PTT-Mirror-Close must be blocked");

    // Non-exit user signal names must NOT be blocked (false-return path)
    Assert.False(CopyEngine.IsExitSignalName("MyLongEntry"),    "user entry signal must pass through");
    Assert.False(CopyEngine.IsExitSignalName("BuySignal"),      "user buy signal must pass through");
}
```

### Test Placement

Append after [`IsDispatchTriggerState_ReturnsTrueForSubmittedAndAccepted`](src/PropTraderTools/CopyEngineTests.cs:2686)
(after line 2749 — before the class closing brace; lines 2701–2749 contain the B55 LaneB test block
and must not be split). Same xUnit test class — no new class needed.

---

## 5. ASCII Compliance Confirmation

All new string literals in `IsExitSignalName`:

| Literal | ASCII? |
|---|---|
| `"Close"` | Yes |
| `"Flatten"` | Yes |
| `"Rev"` | Yes |
| `"Exit"` | Yes |
| `"PTT-"` | Yes |

All test assertion message strings are ASCII-only. **PASS.**

---

## 6. CYC Budget Summary

| Method | CYC Before | CYC After | Limit | Status |
|---|---|---|---|---|
| `IsExitSignalName` | N/A (new) | 7 | 8 | PASS |
| `DispatchCopy` | 8 | 7 | 8 | PASS |
| `IsDispatchTriggerState` | 2 | 2 (unchanged) | 8 | PASS |

---

## 7. Diff Size Estimate

| File | Change | Lines |
|---|---|---|
| `src/PropTraderTools/CopyEngine.cs` | New helper (10 lines) + modified Gate 0.5 comment+guard (2 lines replaced by 2) + updated method header comment (1 line) | ~13 lines net new |
| `src/PropTraderTools/CopyEngineTests.cs` | 7 `[Fact]` test methods + region divider comment | ~75 lines net new |
| **Total** | | **~88 lines** |

Estimated diff character count: ~2,200 characters. Well within 10,000-character PR diff limit. **PASS.**

---

## 8. NinjaTrader 8 API Notes

- `Order.Name` (string): *"A string representing the name of an order which can be provided by the
  entry or exit signal name"* — NT8_FULL_REFERENCE.md line 845.
- No NT8 API calls are made in `IsExitSignalName` — it is a pure string predicate.
- The fix does not alter `CreateOrder`, `Submit`, `OnOrderUpdate`, or any NT8-bound method signatures.
- `AtmStrategyCreate` is irrelevant (StrategyBase-only; CopyEngine is AddOnBase).

---

## 9. Insertion Point Diagram

```
CopyEngine.cs (existing):
  ...
  line 716  internal static bool IsDispatchTriggerState(OrderState state) ...  [UNCHANGED]
  line 718  ;                                                                   [UNCHANGED]
  line 719  (blank)                                                             [UNCHANGED]
>>> INSERT IsExitSignalName block here (10 lines) <<<
  line 720  // --- B7-F0: Bracket mirroring methods ---                        [UNCHANGED]
  line 721  (blank)                                                             [UNCHANGED]
  line 722  // B8 T1: DispatchCopy ...                                         [MODIFY header comment]
  line 725  private void DispatchCopy(Order order, CopyRule rule)              [UNCHANGED]
  line 726  {                                                                   [UNCHANGED]
  line 727  // Gate 0.5: ...                                                   [MODIFY comment]
  line 728  if (order.Name != null && order.Name.StartsWith("PTT-")) return;   [REPLACE with 1-line call]
  ...
```

---

## 10. Ticket Preview (for Phase 3 Ticket Generation)

### T1 — Implement `IsExitSignalName` helper + update Gate 0.5

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Spec requirement**: DW-B59-01  
**Method signatures**:
- `internal static bool IsExitSignalName(string name)` — new, insert after line 718
- `DispatchCopy` Gate 0.5 — single-line replace (line 728)

**JS rule constraints**:
- JS-001: no throw
- JS-002: return bool only
- JS-021: no lock
- JS-CYC: IsExitSignalName CYC=7, DispatchCopy CYC=7

**SCAN-01**: `grep -r "lock(" src/PropTraderTools/CopyEngine.cs` → 0 results
**SCAN-02**: `grep -n "return null" src/PropTraderTools/CopyEngine.cs` → 0 results
**SCAN-03**: CYC audit `IsExitSignalName`: 7 decision points counted manually
**SCAN-04**: ASCII scan — no non-ASCII bytes in new string literals
**SCAN-05**: All PTT signal names start with "PTT-" — invariant preserved
**SCAN-06**: `dotnet build` exits 0
**SCAN-07**: `dotnet test` exits 0 (all 7 new tests pass + no regression)

**Commit sequence**:
```
powershell -File .\deploy-sync.ps1
git add src/PropTraderTools/CopyEngine.cs
git commit -m "feat(ptt): B59 T1 IsExitSignalName helper + Gate 0.5 update [DW-B59-01]"
```

### T2 — Write 7 `[Fact]` tests for `IsExitSignalName`

**File**: `src/PropTraderTools/CopyEngineTests.cs`  
**Spec requirement**: DW-B59-01 (verification)  
**xUnit `[Fact]` tests**:
- `IsExitSignalName_NullName_ReturnsFalse` (T_B59_01)
- `IsExitSignalName_EmptyName_ReturnsFalse` (T_B59_02)
- `IsExitSignalName_Close_ReturnsTrue` (T_B59_03)
- `IsExitSignalName_Flatten_ReturnsTrue` (T_B59_04)
- `IsExitSignalName_RevPrefix_ReturnsTrue` (T_B59_05)
- `IsExitSignalName_ExitPrefix_ReturnsTrue` (T_B59_06)
- `IsExitSignalName_PttPrefixBlockedAndNonMatchingPasses` (T_B59_07)

**SCAN-01 through SCAN-07**: same as T1 (test file only — build + test green).

**Commit sequence**:
```
powershell -File .\deploy-sync.ps1
git add src/PropTraderTools/CopyEngineTests.cs
git commit -m "test(ptt): B59 T2 IsExitSignalName 7 xUnit facts [DW-B59-01]"
```
