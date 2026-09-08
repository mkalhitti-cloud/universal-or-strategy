# WAVE2-LANE-A -- Tickets

**Status**: TICKETS_COMPLETE
**Wave**: WAVE2
**Lane**: A (SINGLE-PIPELINE -- sequential execution, Ticket 1 before Ticket 2)
**Phase**: 3 (Ticket Generation)
**Author**: ptt-architect
**Plan**: `docs/brain/WAVE2-LANE-A/02-architecture-plan.md` (REVIEW_PASS)
**Revision**: Phase 3 Revision (fixes V1-V8 from 04-ticket-review.md)

---

## Revision Notes (V1-V8 Fixes)

| Violation | Fix Applied |
|---|---|
| V1 (T1 file name) | Test class `Wave2LaneAIsExitSignalNameTests` placed in `Wave2LaneATests.cs` |
| V2 (phantom test) | `IsExitSignalName_AtmTarget_ReturnsTrue` kept -- valid test; noted as plan §9.6 addition |
| V3 (missing plan test) | `IsNativeCloseOrFlattenSignal_CloseLowercase_ReturnsFalse` restored (replaces `_OtherString_`) |
| V4 (private helper not testable) | `IsNativeCloseOrFlattenSignal` changed to `internal static` |
| V5 (SCAN-06) | Full 3-step NT8 sync + build + F5 gate replaces `dotnet build` only |
| V6 (T2 file name) | Test class `Wave2LaneAHasArmingAtmBracketsTests` placed in `Wave2LaneATests.cs` |
| V7 (private helper not testable) | `IsArmingOrderState` changed to `internal static` |
| V8 (SCAN-06) | Full 3-step NT8 sync + build + F5 gate replaces `dotnet build` only |

**InternalsVisibleTo**: `[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]`
is already declared at `CopyEngine.cs:46`. No new attribute needed. Both `internal` helpers are
directly callable from `PropTraderTools.Tests` assembly by virtue of that existing declaration.

---

## Execution Order

```
Ticket 1 MUST be fully complete (Phase 4a PASS + Phase 4b PASS) before Ticket 2 begins.
Both tickets modify src/PropTraderTools/CopyEngine.cs -- no parallel execution allowed.
```

---

## TICKET 1: IsExitSignalName CCN 9 -> 8

### Spec Requirement IDs Satisfied

| Rule ID | Description |
|---|---|
| JS-080 | CYC <= 8 -- primary goal of this ticket |
| JS-021 | No `lock()` anywhere |
| JS-001 | No `throw new XxxException` in hot paths |
| JS-002 | No `return null` for missing values |
| ASCII-only | All identifiers and string literals are ASCII |

### File Path

`src/PropTraderTools/CopyEngine.cs` -- class `TrimSignal` (nested static class)

### SCOPE LOCK

Ticket 1 ONLY. Touch ONLY the `IsExitSignalName` region (lines 2347-2370) and add one
`internal` helper `IsNativeCloseOrFlattenSignal` immediately after it. Do NOT touch any other
method. Do NOT touch `HasArmingAtmBrackets` (that is Ticket 2 scope).

### Current Source (exact -- lines 2347-2370)

```csharp
internal static bool IsExitSignalName(string name)
{
    if (name == null)
        return false;
    if (name.Length == 0)
        return true; // (0) DW-LB-FL-01: empty name = NT8 anonymous close order, never a valid entry
    if (name.StartsWith("PTT-", StringComparison.Ordinal))
        return true; // (1)
    if (name == "Close")
        return true; // (2)
    if (name == "Flatten")
        return true; // (3)
    if (name.StartsWith("Rev", StringComparison.Ordinal))
        return true; // (4)
    if (name.StartsWith("Exit", StringComparison.Ordinal))
        return true; // (5)
    // B78 DW-B78-01: ATM profit-target brackets (Target1..Target9) must not trigger follower copy.
    // Pattern: "Target" prefix + digit at index 6. TB-T6: delegated to IsAtmTargetSignalName.
    if (IsAtmTargetSignalName(name))
        return true; // (6)
    // NOTE: "Entry" is intentionally NOT blocked here.
    // Gate 2 already filters to master account only -- follower "Entry" orders never reach DispatchCopy.
    return false;
}
```

### Required Changes

**Change 1 -- Replace the two Close/Flatten if-checks in IsExitSignalName:**

REMOVE these two lines:
```csharp
    if (name == "Close")
        return true; // (2)
    if (name == "Flatten")
        return true; // (3)
```

REPLACE with:
```csharp
    if (IsNativeCloseOrFlattenSignal(name))
        return true; // (2-3)
```

**Change 2 -- Add new `internal` helper immediately AFTER IsExitSignalName (before any existing IsNativeExitName method):**

```csharp
// CCN=3: base(1)+Close(1)+Flatten(1). Extracted from IsExitSignalName WAVE2-LANE-A.
// Consolidates the two NT8 platform-generated anonymous exit signal names.
// internal: accessible to xUnit via InternalsVisibleTo("PropTraderTools.Tests") at CopyEngine.cs:46.
internal static bool IsNativeCloseOrFlattenSignal(string name) =>
    name == "Close" || name == "Flatten";
```

**Change 3 -- Update the comment block on IsExitSignalName to note CCN=8 (post-extraction).**

### Post-Extraction CCN Math

**IsExitSignalName (CCN = 8):**

| # | Decision point | Count |
|---|---|---|
| base | function entry | +1 |
| 1 | `name == null` | +1 |
| 2 | `name.Length == 0` | +1 |
| 3 | `name.StartsWith("PTT-", ...)` | +1 |
| 4 | `IsNativeCloseOrFlattenSignal(name)` | +1 |
| 5 | `name.StartsWith("Rev", ...)` | +1 |
| 6 | `name.StartsWith("Exit", ...)` | +1 |
| 7 | `IsAtmTargetSignalName(name)` | +1 |
| **Total** | | **8** |

**IsNativeCloseOrFlattenSignal (CCN = 3):**

| # | Decision point | Count |
|---|---|---|
| base | function entry | +1 |
| 1 | `name == "Close"` (left of `\|\|`) | +1 |
| 2 | `name == "Flatten"` (right of `\|\|`) | +1 |
| **Total** | | **3** |

### Method Signatures

```csharp
// Modified (CCN 9 -> 8)
internal static bool IsExitSignalName(string name)

// New internal helper (CCN = 3)
// Placement: immediately after IsExitSignalName closing brace (~line 2371)
// Visibility: internal (not private) -- required for direct xUnit access via InternalsVisibleTo
internal static bool IsNativeCloseOrFlattenSignal(string name)
```

### Implementation Constraints

- Helper MUST be `internal static bool` -- NOT `private` (private prevents xUnit test access even with InternalsVisibleTo)
- `InternalsVisibleTo("PropTraderTools.Tests")` is already declared at `CopyEngine.cs:46` -- no new attribute needed
- No null guard inside `IsNativeCloseOrFlattenSignal` (caller guarantees non-null/non-empty via earlier guards)
- No `StringComparison` parameter needed -- ordinal equality is default for `==` on `string` in C#
- All string literals (`"Close"`, `"Flatten"`) are ASCII-only
- .NET 4.8 compatible -- expression-body (`=>`) is C# 6 syntax, fully compliant
- No switch expression (C# 9+, banned for .NET 4.8 targets)
- No `lock()` (JS-021)
- No `throw` (JS-001)
- No `return null` (JS-002)

### xUnit Test Class

**Class name**: `Wave2LaneAIsExitSignalNameTests`
**File**: `tests/PropTraderTools.Tests/Wave2LaneATests.cs`
(Both Ticket 1 and Ticket 2 test classes are in this single file.)

All tests must be decorated with `[Fact]`. All assertions must use `Assert.True` or `Assert.False`.
No `[Theory]` -- each test is a standalone `[Fact]`.

| # | Test method name | What it asserts |
|---|---|---|
| 1 | `IsExitSignalName_NullInput_ReturnsFalse` | `TrimSignal.IsExitSignalName(null)` returns `false` |
| 2 | `IsExitSignalName_EmptyString_ReturnsTrue` | `TrimSignal.IsExitSignalName("")` returns `true` (DW-LB-FL-01 anonymous close) |
| 3 | `IsExitSignalName_PttPrefixed_ReturnsTrue` | `TrimSignal.IsExitSignalName("PTT-Stop1")` returns `true` |
| 4 | `IsExitSignalName_CloseSignal_ReturnsTrue` | `TrimSignal.IsExitSignalName("Close")` returns `true` |
| 5 | `IsExitSignalName_FlattenSignal_ReturnsTrue` | `TrimSignal.IsExitSignalName("Flatten")` returns `true` |
| 6 | `IsExitSignalName_RevPrefix_ReturnsTrue` | `TrimSignal.IsExitSignalName("RevEntry")` returns `true` |
| 7 | `IsExitSignalName_ExitPrefix_ReturnsTrue` | `TrimSignal.IsExitSignalName("ExitLong")` returns `true` |
| 8 | `IsExitSignalName_AtmTarget_ReturnsTrue` | `TrimSignal.IsExitSignalName("Target1")` returns `true` (B78 ATM profit-target) |
| 9 | `IsExitSignalName_EntrySignal_ReturnsFalse` | `TrimSignal.IsExitSignalName("Entry")` returns `false` -- must remain false |
| 10 | `IsNativeCloseOrFlattenSignal_Close_ReturnsTrue` | `TrimSignal.IsNativeCloseOrFlattenSignal("Close")` returns `true` |
| 11 | `IsNativeCloseOrFlattenSignal_Flatten_ReturnsTrue` | `TrimSignal.IsNativeCloseOrFlattenSignal("Flatten")` returns `true` |
| 12 | `IsNativeCloseOrFlattenSignal_CloseLowercase_ReturnsFalse` | `TrimSignal.IsNativeCloseOrFlattenSignal("close")` returns `false` (StringComparison.Ordinal: case-sensitive) |

**Note on test #8**: `IsExitSignalName_AtmTarget_ReturnsTrue` is an explicit addition to the plan's
§9.6 test list (B78 DW-B78-01 ATM profit-target coverage). It exercises the `IsAtmTargetSignalName`
delegation path and confirms "Target1" is recognized as an exit signal.

**Note on test #12**: `IsNativeCloseOrFlattenSignal` uses `==` with default ordinal string comparison.
`"close"` (lowercase) is NOT equal to `"Close"` (exact NT8 signal name). This test verifies the
case-sensitivity contract that prevents false positives on user-named orders.

### 7-Scan Checklist (Engineer Contract)

Engineer MUST run all 7 scans and report results in `ticket-1-completion.md`. All must PASS before commit.

**SCAN-01 -- Lizard CCN (all methods <= 8)**
```powershell
lizard src/PropTraderTools/ -x "*/bin/*" -x "*/obj/*" -x "*Tests*" --csv | ConvertFrom-Csv -Header NLOC,CCN,Token,Params,Length,Location,File,Function,Sig,Start,End | Where-Object {[int]$_.CCN -gt 8} | Select-Object CCN, Function, @{L="File";E={[IO.Path]::GetFileName($_.File)}}
```
Expected: EMPTY (no output -- no methods exceed CCN 8)

**SCAN-02 -- lock() ban (zero results in new/modified lines)**
```powershell
grep -rn "lock(" src/PropTraderTools/CopyEngine.cs
```
Expected: 0 matches in new or modified lines

**SCAN-03 -- throw ban (zero new throws)**
```powershell
grep -n "throw new" src/PropTraderTools/CopyEngine.cs
```
Expected: 0 new `throw new` in lines 2347-2380

**SCAN-04 -- return null ban (zero new null returns)**
```powershell
grep -n "return null" src/PropTraderTools/CopyEngine.cs
```
Expected: 0 new `return null` in lines 2347-2380

**SCAN-05 -- ASCII-only (zero non-ASCII in new code)**
```powershell
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
```
Expected: 0 matches in new or modified lines

**SCAN-06 -- NT8 Sync + Build**

Step 1: dotnet build
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
Expected: 0 errors, 0 warnings related to modified code

Step 2: NT8 sync and MD5 verification
```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```
Expected: 0 MISMATCH lines (all synced files match their source)

Step 3: NinjaTrader 8 recompile (manual)
Press **F5** in NinjaTrader 8 after sync completes.
Expected: NinjaTrader reports 0 errors in the output log

**SCAN-07 -- Tests pass**
```powershell
dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj
```
Expected: >= 248 passing, 0 failing (all 12 new Ticket 1 tests included)

### Commit Message

```
refactor(CopyEngine): extract IsNativeCloseOrFlattenSignal CYC 9->8 [WAVE2-LANE-A T1]
```

---

## TICKET 2: HasArmingAtmBrackets CCN 9 -> 5

### Spec Requirement IDs Satisfied

| Rule ID | Description |
|---|---|
| JS-080 | CYC <= 8 -- primary goal of this ticket |
| JS-021 | No `lock()` anywhere |
| JS-001 | No `throw new XxxException` in hot paths |
| JS-002 | No `return null` for missing values |
| ASCII-only | All identifiers and string literals are ASCII |

### File Path

`src/PropTraderTools/CopyEngine.cs` -- class `TrimSignal` (nested static class)

### SCOPE LOCK

Ticket 2 ONLY. Touch ONLY the `HasArmingAtmBrackets` region (lines 5351-5369) and add one
`internal` helper `IsArmingOrderState` immediately after it. Do NOT touch any other method.
Do NOT re-touch the `IsExitSignalName` region already modified in Ticket 1.

**Prerequisite**: Ticket 1 must be fully complete (Phase 4a PASS + Phase 4b PASS) before starting this ticket.

### Current Source (exact -- lines 5351-5369)

```csharp
internal static bool HasArmingAtmBrackets(Account acc, Instrument instr) // DW-LB-FL-01 visibility
{
    foreach (var o in acc.Orders.ToList())
    {
        if (o.Instrument?.FullName != instr.FullName)
            continue;
        bool stateActive =
            o.OrderState == OrderState.Initialized       // DW-LB-FL-01-V2 belt+suspenders
            || o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Submitted
            || o.OrderState == OrderState.Accepted
            || o.OrderState == OrderState.TriggerPending;
        if (!stateActive)
            continue;
        if (IsAtmBracketName(o.Name))
            return true;
    }
    return false;
}
```

### Required Changes

**Change 1 -- Replace the stateActive compound assignment in HasArmingAtmBrackets:**

REMOVE these lines:
```csharp
        bool stateActive =
            o.OrderState == OrderState.Initialized       // DW-LB-FL-01-V2 belt+suspenders
            || o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Submitted
            || o.OrderState == OrderState.Accepted
            || o.OrderState == OrderState.TriggerPending;
        if (!stateActive)
            continue;
```

REPLACE with:
```csharp
        if (!IsArmingOrderState(o.OrderState)) // WAVE2-LANE-A extraction
            continue;
```

**Change 2 -- Add new `internal` helper immediately AFTER HasArmingAtmBrackets closing brace (~line 5370):**

```csharp
// CCN=6: base(1)+Initialized(1)+Working(1)+Submitted(1)+Accepted(1)+TriggerPending(1). WAVE2-LANE-A.
// JS-021: no lock (static). JS-001: no throw. JS-002: returns bool.
// Extracted from HasArmingAtmBrackets: consolidates 5-state active-order gate.
// DW-LB-FL-01-V2: Initialized included (belt+suspenders for cancel-storm race).
// internal: accessible to xUnit via InternalsVisibleTo("PropTraderTools.Tests") at CopyEngine.cs:46.
internal static bool IsArmingOrderState(OrderState s)
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

**Change 3 -- Update the comment block on HasArmingAtmBrackets to note CCN=5 (post-extraction).**

### Post-Extraction CCN Math

**HasArmingAtmBrackets (CCN = 5):**

| # | Decision point | Count |
|---|---|---|
| base | function entry | +1 |
| 1 | `foreach` loop header | +1 |
| 2 | instrument `FullName` skip (`continue`) | +1 |
| 3 | `!IsArmingOrderState(o.OrderState)` skip (`continue`) | +1 |
| 4 | `IsAtmBracketName(o.Name)` | +1 |
| **Total** | | **5** |

**IsArmingOrderState (CCN = 6):**

| # | Decision point | Count |
|---|---|---|
| base | function entry | +1 |
| 1 | `s == OrderState.Initialized` | +1 |
| 2 | `s == OrderState.Working` | +1 |
| 3 | `s == OrderState.Submitted` | +1 |
| 4 | `s == OrderState.Accepted` | +1 |
| 5 | `s == OrderState.TriggerPending` | +1 |
| **Total** | | **6** |

**Why original CCN was 9:**
Each `||` operator in the compound `stateActive` expression adds one branch path.
4 `||` operators + base(1) + foreach(1) + instr-skip(1) + stateActive-check(1) + IsAtmBracketName(1) = 9.

### Method Signatures

```csharp
// Modified (CCN 9 -> 5)
internal static bool HasArmingAtmBrackets(Account acc, Instrument instr)

// New internal helper (CCN = 6)
// Placement: immediately after HasArmingAtmBrackets closing brace (~line 5370)
// Visibility: internal (not private) -- required for direct xUnit access via InternalsVisibleTo
internal static bool IsArmingOrderState(OrderState s)
```

### Implementation Constraints

- Helper MUST be `internal static bool` -- NOT `private` (private prevents xUnit test access even with InternalsVisibleTo)
- `InternalsVisibleTo("PropTraderTools.Tests")` is already declared at `CopyEngine.cs:46` -- no new attribute needed
- Parameter type is `OrderState` (value-type enum) -- thread-safe by nature, no synchronization needed
- No `lock()` (JS-021)
- No `throw` (JS-001)
- No `return null` (JS-002)
- .NET 4.8 compatible -- no switch expression, no C# 9+ features
- No string literals in this helper -- ASCII-only trivially satisfied
- `HasArmingAtmBrackets` already uses `acc.Orders.ToList()` for a local snapshot -- do NOT remove this
- `Dispatcher.InvokeAsync` NOT required (no UI access, pure logic)

### xUnit Test Class

**Class name**: `Wave2LaneAHasArmingAtmBracketsTests`
**File**: `tests/PropTraderTools.Tests/Wave2LaneATests.cs`
(Same file as Ticket 1 -- both test classes are in this single file.)

All tests must be decorated with `[Fact]`. All assertions must use `Assert.True` or `Assert.False`.
No `[Theory]` -- each test is a standalone `[Fact]`.

| # | Test method name | What it asserts |
|---|---|---|
| 1 | `IsArmingOrderState_Initialized_ReturnsTrue` | `TrimSignal.IsArmingOrderState(OrderState.Initialized)` returns `true` |
| 2 | `IsArmingOrderState_Working_ReturnsTrue` | `TrimSignal.IsArmingOrderState(OrderState.Working)` returns `true` |
| 3 | `IsArmingOrderState_Submitted_ReturnsTrue` | `TrimSignal.IsArmingOrderState(OrderState.Submitted)` returns `true` |
| 4 | `IsArmingOrderState_Accepted_ReturnsTrue` | `TrimSignal.IsArmingOrderState(OrderState.Accepted)` returns `true` |
| 5 | `IsArmingOrderState_TriggerPending_ReturnsTrue` | `TrimSignal.IsArmingOrderState(OrderState.TriggerPending)` returns `true` |
| 6 | `IsArmingOrderState_Filled_ReturnsFalse` | `TrimSignal.IsArmingOrderState(OrderState.Filled)` returns `false` |
| 7 | `IsArmingOrderState_Cancelled_ReturnsFalse` | `TrimSignal.IsArmingOrderState(OrderState.Cancelled)` returns `false` |
| 8 | `IsArmingOrderState_Rejected_ReturnsFalse` | `TrimSignal.IsArmingOrderState(OrderState.Rejected)` returns `false` |

### 7-Scan Checklist (Engineer Contract)

Engineer MUST run all 7 scans and report results in `ticket-2-completion.md`. All must PASS before commit.

**SCAN-01 -- Lizard CCN (all methods <= 8)**
```powershell
lizard src/PropTraderTools/ -x "*/bin/*" -x "*/obj/*" -x "*Tests*" --csv | ConvertFrom-Csv -Header NLOC,CCN,Token,Params,Length,Location,File,Function,Sig,Start,End | Where-Object {[int]$_.CCN -gt 8} | Select-Object CCN, Function, @{L="File";E={[IO.Path]::GetFileName($_.File)}}
```
Expected: EMPTY (no output -- no methods exceed CCN 8)

**SCAN-02 -- lock() ban (zero results in new/modified lines)**
```powershell
grep -rn "lock(" src/PropTraderTools/CopyEngine.cs
```
Expected: 0 matches in new or modified lines

**SCAN-03 -- throw ban (zero new throws)**
```powershell
grep -n "throw new" src/PropTraderTools/CopyEngine.cs
```
Expected: 0 new `throw new` in lines 5351-5385

**SCAN-04 -- return null ban (zero new null returns)**
```powershell
grep -n "return null" src/PropTraderTools/CopyEngine.cs
```
Expected: 0 new `return null` in lines 5351-5385

**SCAN-05 -- ASCII-only (zero non-ASCII in new code)**
```powershell
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
```
Expected: 0 matches in new or modified lines

**SCAN-06 -- NT8 Sync + Build**

Step 1: dotnet build
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
Expected: 0 errors, 0 warnings related to modified code

Step 2: NT8 sync and MD5 verification
```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```
Expected: 0 MISMATCH lines (all synced files match their source)

Step 3: NinjaTrader 8 recompile (manual)
Press **F5** in NinjaTrader 8 after sync completes.
Expected: NinjaTrader reports 0 errors in the output log

**SCAN-07 -- Tests pass**
```powershell
dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj
```
Expected: >= 256 passing, 0 failing (all 8 new Ticket 2 tests included, plus 12 from Ticket 1)

### Commit Message

```
refactor(CopyEngine): extract IsArmingOrderState CYC 9->5 [WAVE2-LANE-A T2]
```

---

## Summary

| Item | Ticket 1 | Ticket 2 |
|---|---|---|
| Method modified | `TrimSignal::IsExitSignalName` | `TrimSignal::HasArmingAtmBrackets` |
| Lines | 2347-2370 | 5351-5369 |
| CCN before | 9 | 9 |
| CCN after | 8 | 5 |
| New helper | `IsNativeCloseOrFlattenSignal` (CCN=3) | `IsArmingOrderState` (CCN=6) |
| Helper visibility | `internal static` | `internal static` |
| xUnit tests | 13 | 8 |
| Test class | `Wave2LaneAIsExitSignalNameTests` | `Wave2LaneAHasArmingAtmBracketsTests` |
| Test file | `Wave2LaneATests.cs` | `Wave2LaneATests.cs` (same file) |
| JS rules satisfied | JS-080, JS-021, JS-001, JS-002, ASCII | JS-080, JS-021, JS-001, JS-002, ASCII |
| Completion artifact | `ticket-1-completion.md` | `ticket-2-completion.md` |

**TICKETS_COMPLETE**
