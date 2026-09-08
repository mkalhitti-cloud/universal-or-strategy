# WAVE2-LANE-A -- Architecture Plan

**Status**: REVIEW_PASS  
**Wave**: WAVE2  
**Lane**: A (SINGLE-PIPELINE -- sequential, not parallel)  
**Phase**: 1 (Architecture)  
**Author**: ptt-architect  

---

## 1. LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

| Question | Answer |
|---|---|
| Q1. Same method or within 50 lines? | NO -- different regions of CopyEngine.cs (lines 2347 vs 5351) |
| Q2. Fix B design depends on Fix A final design? | NO -- independent branches of TrimSignal class |
| Q3. Each fix has standalone value if the other is blocked? | YES |
| Q4. Each fix has an independent SIM verification path? | YES |

**Gate conclusion**: Both tickets edit the same `.cs` file (`CopyEngine.cs`).  
Per Parallel Lane Protocol: "Two lanes writing to the same .cs file = merge conflict. Merge them into one sequential lane."  
**GATE RESULT: SINGLE-PIPELINE -- Ticket 1 executes fully before Ticket 2 begins.**

---

## 2. Scope

| Item | Value |
|---|---|
| Methods in scope | 2 |
| File in scope | `src/PropTraderTools/CopyEngine.cs` |
| Class | `TrimSignal` (nested static class within CopyEngine.cs) |
| Tickets | 2 (sequential) |
| New helper methods | 2 (private static, within TrimSignal) |
| Other files modified | `tests/PropTraderTools.Tests/Wave2LaneATests.cs` (new) |
| Files BANNED from modification | All `Ptt*.cs`, Panel, Window, AddOn files |

---

## 3. Spec Requirement IDs

All tickets satisfy the following Jane Street rules:

| Rule ID | Description | Applies To |
|---|---|---|
| JS-080 | CYC <= 8 for all methods | Both tickets -- primary goal |
| JS-021 | No `lock()` anywhere | Both tickets -- no synchronization needed |
| JS-001 | No `throw new XxxException` in hot paths | Both tickets -- helpers return bool only |
| JS-002 | No `return null` for missing values | Both tickets -- helpers return bool only |
| ASCII-only | All string literals and identifiers are ASCII | Both tickets |

---

## 4. NT8 Constraints Summary

These constraints apply to all work in this repository. The two methods under extraction
are query-only pure static functions. No NT8 order creation or ATM management is performed.

| Constraint | Status |
|---|---|
| `AtmStrategyCreate()` -- StrategyBase-only, NOT AddOnBase | NOT USED in these methods |
| `AtmStrategyChangeStopTarget()` -- StrategyBase-only, NOT AddOnBase | NOT USED in these methods |
| `Account.Change()` -- silent no-op on ATM-owned brackets | NOT USED in these methods |
| `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` -- correct AddOn bracket pattern | NOT USED in these methods |
| `lock()` -- BANNED | Not used; both helpers are pure functions with no shared state |
| `Dispatcher.InvokeAsync` -- required for UI access | NOT NEEDED; no UI access in these pure helpers |
| .NET 4.8 -- no C# 9+ features | Enforced: only `if`/`return` chains; no switch expressions |

NT8 types used (read-only, query only):
- `NinjaTrader.Cbi.Account` -- `acc.Orders.ToList()` (snapshot read, no mutation)
- `NinjaTrader.Cbi.Instrument` -- `instr.FullName` (read-only property)
- `NinjaTrader.Cbi.OrderState` -- enum value comparison only

---

## 5. Component List

### Modified components

| Class | Method | Lines | Action |
|---|---|---|---|
| `TrimSignal` | `IsExitSignalName(string)` | 2347-2370 | Reduce CCN 9 -> 8; delegate Close/Flatten to new helper |
| `TrimSignal` | `HasArmingAtmBrackets(Account, Instrument)` | 5351-5369 | Reduce CCN 9 -> 5; delegate OrderState check to new helper |

### New components (private static helpers within TrimSignal)

| Class | Method | Placement | CCN |
|---|---|---|---|
| `TrimSignal` | `IsNativeCloseOrFlattenSignal(string name)` | After `IsExitSignalName` (~line 2371) | 3 |
| `TrimSignal` | `IsArmingOrderState(OrderState s)` | After `HasArmingAtmBrackets` (~line 5370) | 6 |

---

## 6. Data Flow (Pre/Post Extraction -- Semantic Equivalence Proof)

### IsExitSignalName (Ticket 1)

**Before:**

  name -> null guard -> empty guard -> PTT- check -> Close check -> Flatten check
       -> Rev check -> Exit check -> IsAtmTargetSignalName -> return bool

**After:**

  name -> null guard -> empty guard -> PTT- check -> IsNativeCloseOrFlattenSignal(name)
       -> Rev check -> Exit check -> IsAtmTargetSignalName -> return bool

`IsNativeCloseOrFlattenSignal(name)` = `name == "Close" || name == "Flatten"`  
Semantic equivalence: IDENTICAL -- same two string comparisons, same result.

NOTE: `IsNativeCloseOrFlattenSignal` is only reached when `name` is non-null and non-empty
(both guards fire before this call). No null guard needed inside the helper.

### HasArmingAtmBrackets (Ticket 2)

**Before:**

  acc.Orders.ToList() -> foreach o -> instrument filter -> stateActive compound (5 || terms)
                      -> IsAtmBracketName check -> return bool

**After:**

  acc.Orders.ToList() -> foreach o -> instrument filter -> IsArmingOrderState(o.OrderState)
                      -> IsAtmBracketName check -> return bool

`IsArmingOrderState(s)` = `s == Initialized || s == Working || s == Submitted || s == Accepted || s == TriggerPending`  
Semantic equivalence: IDENTICAL -- same 5 OrderState values, same result.

---

## 7. Threading Model

Both extractions produce pure static functions with no shared state:

- `IsNativeCloseOrFlattenSignal(string)` -- input is an immutable string, output is bool. Zero threading concern.
- `IsArmingOrderState(OrderState)` -- input is a value-type enum, output is bool. Zero threading concern.
- `HasArmingAtmBrackets` already uses `acc.Orders.ToList()` to create a local snapshot before iterating. No new threading concern introduced.
- No `Dispatcher.InvokeAsync` required (no UI access).
- No `ConcurrentQueue` required (no state mutation).
- No `lock()` (banned by JS-021 and project DNA).

---

## 8. Exact Method Signatures

### Ticket 1 -- new + modified signatures

```csharp
// Modified (CCN reduced 9 -> 8)
internal static bool IsExitSignalName(string name)

// New private helper (CCN = 3)
private static bool IsNativeCloseOrFlattenSignal(string name)
```

### Ticket 2 -- new + modified signatures

```csharp
// Modified (CCN reduced 9 -> 5)
internal static bool HasArmingAtmBrackets(Account acc, Instrument instr)

// New private helper (CCN = 6)
private static bool IsArmingOrderState(OrderState s)
```

---

## 9. Ticket 1 -- IsExitSignalName

### 9.1 Spec Requirements Satisfied
- JS-080: CYC reduces from 9 to 8
- JS-021: No lock()
- JS-001: No throw
- JS-002: No null return
- ASCII-only: String literals "Close", "Flatten" are ASCII

### 9.2 File Path
`src/PropTraderTools/CopyEngine.cs` -- class `TrimSignal`

### 9.3 Line Range
Original method: lines 2347-2370  
New helper placed: immediately after `IsExitSignalName` (~line 2371)

### 9.4 CCN Math

**IsExitSignalName post-extraction (CCN = 8):**

| # | Decision point | Count |
|---|---|---|
| 1 | `name == null` | +1 |
| 2 | `name.Length == 0` | +1 |
| 3 | `name.StartsWith("PTT-", ...)` | +1 |
| 4 | `IsNativeCloseOrFlattenSignal(name)` | +1 |
| 5 | `name.StartsWith("Rev", ...)` | +1 |
| 6 | `name.StartsWith("Exit", ...)` | +1 |
| 7 | `IsAtmTargetSignalName(name)` | +1 |
| base | | +1 |
| **Total** | | **8** |

**IsNativeCloseOrFlattenSignal (CCN = 3):**

| # | Decision point | Count |
|---|---|---|
| 1 | `name == "Close"` | +1 |
| 2 | `name == "Flatten"` | +1 |
| base | | +1 |
| **Total** | | **3** |

### 9.5 Implementation Contract

```csharp
// BEFORE -- lines 2347-2370 (the two checks being extracted):
if (name == "Close")
    return true; // (2)
if (name == "Flatten")
    return true; // (3)

// AFTER -- replace above two blocks with:
if (IsNativeCloseOrFlattenSignal(name))
    return true; // (2)+(3)

// NEW HELPER -- place after IsExitSignalName:
private static bool IsNativeCloseOrFlattenSignal(string name)
{
    if (name == "Close")
        return true;
    if (name == "Flatten")
        return true;
    return false;
}
```

Constraints:
- Helper is `private static bool` -- not `internal`, not public
- No null guard in helper (caller guarantees non-null, non-empty via earlier guards)
- No StringComparison parameter needed -- ordinal equality is default for `==` on string in C#
- ASCII-only string literals
- .NET 4.8 compatible (no switch expression)

### 9.6 xUnit Test Names (Fact-level)

All in class `Wave2LaneAIsExitSignalNameTests` in `tests/PropTraderTools.Tests/Wave2LaneATests.cs`:

| Test name | Asserts |
|---|---|
| `IsExitSignalName_NullName_ReturnsFalse` | `null` input returns `false` |
| `IsExitSignalName_EmptyName_ReturnsTrue` | `""` input returns `true` (DW-LB-FL-01 anonymous close) |
| `IsExitSignalName_PttPrefixedName_ReturnsTrue` | `"PTT-Stop1"` returns `true` |
| `IsExitSignalName_CloseName_ReturnsTrue` | `"Close"` returns `true` |
| `IsExitSignalName_FlattenName_ReturnsTrue` | `"Flatten"` returns `true` |
| `IsExitSignalName_RevPrefixedName_ReturnsTrue` | `"RevEntry"` returns `true` |
| `IsExitSignalName_ExitPrefixedName_ReturnsTrue` | `"ExitLong"` returns `true` |
| `IsExitSignalName_UnknownName_ReturnsFalse` | `"MyEntry"` returns `false` |
| `IsNativeCloseOrFlattenSignal_CloseName_ReturnsTrue` | `"Close"` returns `true` |
| `IsNativeCloseOrFlattenSignal_FlattenName_ReturnsTrue` | `"Flatten"` returns `true` |
| `IsNativeCloseOrFlattenSignal_OtherName_ReturnsFalse` | `"Stop"` returns `false` |
| `IsNativeCloseOrFlattenSignal_CloseLowercase_ReturnsFalse` | `"close"` returns `false` (case-sensitive) |

### 9.7 Seven-Scan Checklist (Engineer Contract)

- SCAN-01 (CYC): Verify `IsExitSignalName` CCN = 8 after edit. Verify `IsNativeCloseOrFlattenSignal` CCN = 3. Run `python scripts/complexity_audit.py` -- both must show <= 8.
- SCAN-02 (lock): `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` -- must return 0 new results in lines 2347-2380.
- SCAN-03 (ASCII): `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` -- zero matches in modified lines.
- SCAN-04 (throw): No `throw` statement in `IsExitSignalName` or `IsNativeCloseOrFlattenSignal`. Grep confirms.
- SCAN-05 (null return): Both methods return `bool`. Cannot return `null`. Compiler enforces.
- SCAN-06 (NT8 compile): Run `powershell -File scripts\ptt-sync-and-verify.ps1` -> 0 MISMATCH lines. Press F5 in NinjaTrader 8 -> green compile.
- SCAN-07 (tests pass): `dotnet test tests/PropTraderTools.Tests/ --filter "Wave2LaneAIsExitSignalName"` -- all 12 tests pass.

---

## 10. Ticket 2 -- HasArmingAtmBrackets

### 10.1 Spec Requirements Satisfied
- JS-080: CYC reduces from 9 to 5
- JS-021: No lock()
- JS-001: No throw
- JS-002: No null return
- ASCII-only: No string literals in modified code

### 10.2 File Path
`src/PropTraderTools/CopyEngine.cs` -- class `TrimSignal`

### 10.3 Line Range
Original method: lines 5351-5369  
New helper placed: immediately after `HasArmingAtmBrackets` (~line 5370)

### 10.4 CCN Math

**HasArmingAtmBrackets post-extraction (CCN = 5):**

| # | Decision point | Count |
|---|---|---|
| 1 | `foreach` loop | +1 |
| 2 | instrument `FullName` skip | +1 |
| 3 | `IsArmingOrderState(o.OrderState)` skip | +1 |
| 4 | `IsAtmBracketName(o.Name)` | +1 |
| base | | +1 |
| **Total** | | **5** |

**IsArmingOrderState (CCN = 6):**

| # | Decision point | Count |
|---|---|---|
| 1 | `s == OrderState.Initialized` | +1 |
| 2 | `s == OrderState.Working` | +1 |
| 3 | `s == OrderState.Submitted` | +1 |
| 4 | `s == OrderState.Accepted` | +1 |
| 5 | `s == OrderState.TriggerPending` | +1 |
| base | | +1 |
| **Total** | | **6** |

NOTE on original CCN=9 for HasArmingAtmBrackets:  
The compound boolean expression `stateActive = a || b || c || d || e` creates 4 extra branch paths
(each `||` operator adds one path). Combined with base(1) + foreach(1) + instr-skip(1) +
stateActive-check(1) + IsAtmBracketName(1) = 9.

### 10.5 Implementation Contract

```csharp
// BEFORE -- lines 5351-5369 (stateActive compound being extracted):
bool stateActive =
    o.OrderState == OrderState.Initialized
    || o.OrderState == OrderState.Working
    || o.OrderState == OrderState.Submitted
    || o.OrderState == OrderState.Accepted
    || o.OrderState == OrderState.TriggerPending;
if (!stateActive)
    continue;

// AFTER -- replace stateActive block with:
if (!IsArmingOrderState(o.OrderState))
    continue;

// NEW HELPER -- place after HasArmingAtmBrackets:
private static bool IsArmingOrderState(OrderState s)
{
    if (s == OrderState.Initialized)
        return true;
    if (s == OrderState.Working)
        return true;
    if (s == OrderState.Submitted)
        return true;
    if (s == OrderState.Accepted)
        return true;
    if (s == OrderState.TriggerPending)
        return true;
    return false;
}
```

Constraints:
- Helper is `private static bool` -- not `internal`, not public
- Parameter type is `OrderState` (value type enum) -- thread-safe by nature
- No `lock()`, no `Dispatcher.InvokeAsync`
- .NET 4.8 compatible (no switch expression)
- ASCII-only: No string literals in this helper

### 10.6 xUnit Test Names (Fact-level)

All in class `Wave2LaneAHasArmingAtmBracketsTests` in `tests/PropTraderTools.Tests/Wave2LaneATests.cs`:

| Test name | Asserts |
|---|---|
| `IsArmingOrderState_Initialized_ReturnsTrue` | `OrderState.Initialized` returns `true` |
| `IsArmingOrderState_Working_ReturnsTrue` | `OrderState.Working` returns `true` |
| `IsArmingOrderState_Submitted_ReturnsTrue` | `OrderState.Submitted` returns `true` |
| `IsArmingOrderState_Accepted_ReturnsTrue` | `OrderState.Accepted` returns `true` |
| `IsArmingOrderState_TriggerPending_ReturnsTrue` | `OrderState.TriggerPending` returns `true` |
| `IsArmingOrderState_Filled_ReturnsFalse` | `OrderState.Filled` returns `false` |
| `IsArmingOrderState_Cancelled_ReturnsFalse` | `OrderState.Cancelled` returns `false` |
| `IsArmingOrderState_Rejected_ReturnsFalse` | `OrderState.Rejected` returns `false` |

NOTE: `HasArmingAtmBrackets` integration tests (using mock Account/Order objects) are in a
separate test class `Wave2LaneAHasArmingAtmBracketsIntegrationTests` if the NT8 test harness
supports Account mock injection. If not available, unit-test `IsArmingOrderState` directly
(all 8 Fact tests above) and add a manual SIM verification note.

### 10.7 Seven-Scan Checklist (Engineer Contract)

- SCAN-01 (CYC): Verify `HasArmingAtmBrackets` CCN = 5 after edit. Verify `IsArmingOrderState` CCN = 6. Run `python scripts/complexity_audit.py` -- both must show <= 8.
- SCAN-02 (lock): `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` -- must return 0 new results in lines 5351-5380.
- SCAN-03 (ASCII): `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` -- zero matches in modified lines.
- SCAN-04 (throw): No `throw` statement in `HasArmingAtmBrackets` or `IsArmingOrderState`. Grep confirms.
- SCAN-05 (null return): Both methods return `bool`. Cannot return `null`. Compiler enforces.
- SCAN-06 (NT8 compile): Run `powershell -File scripts\ptt-sync-and-verify.ps1` -> 0 MISMATCH lines. Press F5 in NinjaTrader 8 -> green compile.
- SCAN-07 (tests pass): `dotnet test tests/PropTraderTools.Tests/ --filter "Wave2LaneA"` -- all tests pass.

---

## 11. Risk Register

| Risk | Severity | Mitigation |
|---|---|---|
| Single .cs file edited by both tickets | LOW | Sequential execution enforced (T1 completes before T2 starts). Line ranges non-overlapping (2347 vs 5351). No merge conflict possible. |
| Behavioral regression in IsExitSignalName | LOW | 12 xUnit tests cover all 7 original branches including the extracted Close/Flatten path. |
| Behavioral regression in HasArmingAtmBrackets | LOW | 8 xUnit tests cover all 5 OrderState values (active) plus 3 inactive states. |
| NT8 compile failure | LOW | Both helpers are pure C# 4.8 static methods -- no NT8 API calls. F5 gate enforced by SCAN-06. |
| CYC regression after edit | LOW | Complexity audit script run as part of SCAN-01 before any commit. |

---

## 12. Execution Order

```
Ticket 1 (IsExitSignalName)
  Step A: Modify IsExitSignalName at lines 2347-2370 (replace 2 if-blocks with 1 helper call)
  Step B: Add IsNativeCloseOrFlattenSignal immediately after (~line 2371)
  Step C: Run SCAN-01 through SCAN-07 (all must pass)
  Step D: Commit -- "refactor(CopyEngine): extract IsNativeCloseOrFlattenSignal CYC 9->8 [WAVE2-LANE-A T1]"

Ticket 2 (HasArmingAtmBrackets) -- only starts after Ticket 1 is committed
  Step A: Modify HasArmingAtmBrackets at lines 5351-5369 (replace stateActive block with helper call)
  Step B: Add IsArmingOrderState immediately after (~line 5370)
  Step C: Run SCAN-01 through SCAN-07 (all must pass)
  Step D: Commit -- "refactor(CopyEngine): extract IsArmingOrderState CYC 9->5 [WAVE2-LANE-A T2]"
```

---

## 13. Summary

| Item | Value |
|---|---|
| Wave | WAVE2-LANE-A |
| Pipeline | SINGLE (sequential, 2 tickets) |
| File modified | `src/PropTraderTools/CopyEngine.cs` |
| Methods modified | `TrimSignal::IsExitSignalName`, `TrimSignal::HasArmingAtmBrackets` |
| New helpers | `TrimSignal::IsNativeCloseOrFlattenSignal`, `TrimSignal::IsArmingOrderState` |
| CCN before | 9, 9 |
| CCN after | 8, 5 |
| New helper CCNs | 3, 6 |
| xUnit tests | 12 (Ticket 1) + 8 (Ticket 2) = 20 total |
| NT8 API risk | None -- both methods are query-only static functions |
| lock() used | None -- JS-021 compliant |
| .NET version | 4.8 -- no C# 9+ syntax |

**PLAN_COMPLETE**