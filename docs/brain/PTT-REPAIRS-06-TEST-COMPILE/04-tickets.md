# PTT-REPAIRS-06-TEST-COMPILE — Tickets
**Epic:** PTT-REPAIRS-06-TEST-COMPILE  
**Phase:** 3 — Ticket Generation  
**Source plan:** `docs/brain/PTT-REPAIRS-06-TEST-COMPILE/02-architecture-plan.md` (REVIEW_PASS)  
**Status:** TICKETS_COMPLETE

---

## TICKET-1

**ID:** TICKET-1  
**Epic:** PTT-REPAIRS-06-TEST-COMPILE  
**File:** `src/PropTraderTools/CopyEngineTests.cs`  
**Spec req IDs:** F1, F2, F3, F4, F5, F6, F7, F8, F9, F10  
**Estimated lines changed:** ~60 insertions, ~8 line-modifications

---

## SCOPE LOCK

SCOPE LOCK: This ticket covers ONLY src/PropTraderTools/CopyEngineTests.cs.
No changes to CopyEngine.cs or TradeCopierPanel.cs. Any edit outside
CopyEngineTests.cs is a protocol violation.

---

## IMPLEMENTATION INSTRUCTIONS

Apply all fixes in the order listed. Because fixes F1, F2, F3 insert lines near the
top of the file, line numbers for all subsequent fixes reference the **original** line
numbers before any insertion. The engineer MUST resolve the line-number shift manually
after each insertion OR apply all insertions in reverse-line-number order so that
earlier insertions do not invalidate later line references. The safest approach is to
apply fixes in **descending line number order** (F10 first, F9, F8 ... F1 last).

---

### F1 — Add namespace-scoped CopyRule alias

**Compile error resolved:** `CS0246: The type or namespace name 'CopyRule' could not be found`  
(affects lines 71, 92, 113, 122, 127, 401, 403, 504, 533, 535, 571, 573, 603, 605, 658, 660, 697, 699, 894, 896, 910, 912, 943, 945, 959, 961, 1348, 1349, 1434, 2537, 2546, 3044, 3068, 7311, 7312 and others)

**Operation:** INSERT after line 12 (the opening brace `{` of `namespace PropTraderTools`).

**Context at insertion point:**
```
Line 11: namespace PropTraderTools
Line 12: {
Line 13:     public class CopyEngineTests : IDisposable
```

**Insert after line 12:**
```csharp
    using CopyRule = PropTraderTools.CopyEngine.CopyRule;
```

**Effect:** Resolves every bare `CopyRule` reference in the file without modifying any of them.

---

### F2 — Add `using System.Collections.Generic;`

**Compile error resolved:** `CS0246: The type or namespace name 'Dictionary' could not be found`  
(affects lines 2522, 2529: `new Dictionary<string, FollowerAtmMode>()`)

**Operation:** INSERT after line 9 (after `using Xunit;`).

**Context at insertion point:**
```
Line 5:  using System;
Line 6:  using System.Collections.Concurrent;
Line 7:  using System.Reflection;
Line 8:  using NinjaTrader.Cbi;
Line 9:  using Xunit;
```

**Insert after line 9:**
```csharp
using System.Collections.Generic;
```

---

### F3 — Add `using System.Linq;`

**Compile error resolved:** `CS1061: 'X' does not contain a definition for 'Any'` and `'FirstOrDefault'`  
(affects `.Any()` at lines 2977, 3823, 3888, 3926; `.FirstOrDefault()` at lines 2781, 2797, 2813)

**Operation:** INSERT after the `using System.Collections.Generic;` line added by F2 (i.e., after the newly inserted line, which sits after original line 9).

**Insert after F2's new line:**
```csharp
using System.Linq;
```

---

### F4 — Skip tests using `System.Collections.Immutable` (net48 not available)

**Compile error resolved:** `CS0234: The type or namespace name 'Immutable' does not exist in the namespace 'System.Collections'`  
(affects lines 499, 529, 567, 689, 738, 768, 889, 938 inside these test methods)

**Operation:** REPLACE `[Fact]` with `[Fact(Skip = "net48: System.Collections.Immutable not available")]` at each of the following 8 lines.

| Original line | Method name |
|---|---|
| 486 | `AddRule_WithMultipliers_StoresCorrectMultipliers` |
| 519 | `GetMultiplier_OutOfRangeIndex_ReturnsOne` |
| 557 | `GetMultiplier_ValidIndex_ReturnsStoredValue` |
| 684 | `GetAtmMode_WithNamedEntry_ReturnsNamedMode` |
| 728 | `SaveLoad_RoundTrip_PreservesMultipliers` |
| 763 | `SaveLoad_RoundTrip_PreservesAtmModeNames` |
| 879 | `SetFollowerMultiplier_UpdatesMultiplier_RebuildsRules` |
| 928 | `SetAtmMode_UpdatesAtmTemplate_RebuildsRules` |

Old text (at each of the 8 lines listed above):
```
[Fact]
```
New text:
```
[Fact(Skip = "net48: System.Collections.Immutable not available")]
```

---

### F5 — Skip `FindFollowerBracketOrder_NullableReturnType` test (.NET 6+ only)

**Compile error resolved:** `CS0234: The type or namespace name 'NullabilityInfoContext' does not exist`  
(affects lines 452–454: `new System.Reflection.NullabilityInfoContext()`)

**Operation:** REPLACE `[Fact]` at line **440** with Skip attribute.

Old (line 440):
```
[Fact]
```
New:
```
[Fact(Skip = "net48: NullabilityInfoContext requires .NET 6+")]
```

---

### F6 — Skip tests referencing `NinjaTrader.NinjaScript.Instruments`

**Compile/runtime error resolved:** `CS0234` or type-load exception for `NinjaTrader.NinjaScript.Instruments.Instrument`  
(not available outside NT8 runtime)

**Operation:** REPLACE `[Fact]` with `[Fact(Skip = "NT8-runtime: NinjaTrader.NinjaScript.Instruments not available")]` at each of the following 8 lines.

| Original line | Method name |
|---|---|
| 2640 | `T_B26_01_TrailBe_WithNoRule_StillMovesStop` |
| 2775 | `T_B28_01_Trim_LeaderOverload_Exists` |
| 2791 | `T_B28_02_Flatten_LeaderOverload_Exists` |
| 2807 | `T_B28_03_CancelPendingEntries_LeaderOverload_Exists` |
| 2850 | `MoveStopToBreakEven_RetriesOnCreateOrderFailure` |
| 2876 | `CancelOneAccount_UsesSnapshotNotLiveOrders` |
| 2904 | `ArmPendingBe_SkipsWhenFlat` |
| 3798 | `T_B67_01_CancelQxBrackets_called_before_CreateOrder` |

Old text (at each of the 8 lines listed above):
```
[Fact]
```
New text:
```
[Fact(Skip = "NT8-runtime: NinjaTrader.NinjaScript.Instruments not available")]
```

---

### F7 — Update `IsDispatchTriggerState` test to 2-param signature + correct semantics

**Compile error resolved:** `CS7036: There is no argument given that corresponds to the required formal parameter 'type'`  
(affects lines 2991, 2997, 3003, 3007, 3011, 3015 — all single-arg calls to `IsDispatchTriggerState`)

**Logic error corrected:** `Working` was asserted `False` but the method returns `True` for `(Limit, Working)` per DW-B96 ChartTrader path.

#### Change 1 — Rename the method (line 2987)

Old:
```csharp
public void IsDispatchTriggerState_ReturnsTrueForSubmittedAndAccepted()
```
New:
```csharp
public void IsDispatchTriggerState_CorrectStates()
```

#### Change 2 — Update Assert block at lines 2990–2993

Old:
```csharp
Assert.True(
    CopyEngine.IsDispatchTriggerState(OrderState.Submitted),
    "Submitted must be true"
);
```
New:
```csharp
Assert.True(
    CopyEngine.IsDispatchTriggerState(OrderState.Submitted, OrderType.Market),
    "Submitted+Market must be true"
);
```

#### Change 3 — Update Assert block at lines 2996–2999

Old:
```csharp
Assert.True(
    CopyEngine.IsDispatchTriggerState(OrderState.Accepted),
    "Accepted must be true"
);
```
New:
```csharp
Assert.True(
    CopyEngine.IsDispatchTriggerState(OrderState.Accepted, OrderType.Limit),
    "Accepted+Limit must be true"
);
```

#### Change 4 — Update Assert block at lines 3002–3005

Old:
```csharp
Assert.False(
    CopyEngine.IsDispatchTriggerState(OrderState.Initialized),
    "Initialized must be false"
);
```
New:
```csharp
Assert.False(
    CopyEngine.IsDispatchTriggerState(OrderState.Initialized, OrderType.Limit),
    "Initialized+Limit must be false"
);
```

#### Change 5 — Update Assert block at lines 3006–3009 (Assert.False → Assert.True, DW-B96)

Old:
```csharp
Assert.False(
    CopyEngine.IsDispatchTriggerState(OrderState.Working),
    "Working must be false"
);
```
New:
```csharp
Assert.True(
    CopyEngine.IsDispatchTriggerState(OrderState.Working, OrderType.Limit),
    "Working+Limit must be true (DW-B96 ChartTrader path)"
);
```

#### Change 6 — Update Assert block at lines 3010–3013

Old:
```csharp
Assert.False(
    CopyEngine.IsDispatchTriggerState(OrderState.Filled),
    "Filled must be false"
);
```
New:
```csharp
Assert.False(
    CopyEngine.IsDispatchTriggerState(OrderState.Filled, OrderType.Limit),
    "Filled+Limit must be false"
);
```

#### Change 7 — Update Assert block at lines 3014–3017

Old:
```csharp
Assert.False(
    CopyEngine.IsDispatchTriggerState(OrderState.Cancelled),
    "Cancelled must be false"
);
```
New:
```csharp
Assert.False(
    CopyEngine.IsDispatchTriggerState(OrderState.Cancelled, OrderType.Limit),
    "Cancelled+Limit must be false"
);
```

---

### F8 — Replace `new CopyEngine()` with `CopyEngine.Instance`

**Compile error resolved:** `CS0122: 'CopyEngine.CopyEngine()' is inaccessible due to its protection level`  
(private constructor at `CopyEngine.cs:601`)

**Operation:** REPLACE at two lines.

**Line 4036** — inside `T_B69_01_CancelAllAccountOrders_cancels_PTT_Copy_orders`:

Old:
```csharp
var engine = new CopyEngine();
```
New:
```csharp
var engine = CopyEngine.Instance;
```

**Line 4137** — inside `T_B69_07_CancelAllAccountOrders_null_acc_noOp`:

Old:
```csharp
var engine = new CopyEngine();
```
New:
```csharp
var engine = CopyEngine.Instance;
```

---

### F9 — Add `GetMethod` + `GetField` helpers to `B79CancelRaceGuardTests`

**Compile error resolved:** `CS0103: The name 'GetMethod' does not exist in the current context`  
(affects lines 5903, 5911, 5919, 5927, 5937, 5945, 5953, 5961, 5970, 5975, 5982, 5993, 5998 and others)  
`CS0103: The name 'GetField' does not exist in the current context` (related calls)

**Context at insertion point:**
```
Line 5768: public class B79CancelRaceGuardTests
Line 5771: private static bool ContainsMethodToken(...) =>
...
Line 5782: (closing brace of ContainsMethodToken)
```

**Operation:** INSERT after line 5782 (after closing brace of `ContainsMethodToken`).

**Insert after line 5782:**
```csharp

        private static System.Reflection.MethodInfo GetMethod(string name) =>
            typeof(CopyEngine).GetMethod(name, System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);

        private static System.Reflection.FieldInfo GetField(string name) =>
            typeof(CopyEngine).GetField(name, System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);
```

---

### F10 — Add `_engine`, `GetMethod`, and `GetField` to `BwaveCycTaR7HelperTests`

**Compile error resolved:**  
`CS0103: The name '_engine' does not exist in the current context` (lines 7308, 7309, 7341, etc.)  
`CS0103: The name 'GetMethod' does not exist in the current context` (line 7709)  
`CS0103: The name 'GetField' does not exist in the current context` (line 7713)

**Context at insertion point:**
```
Line 7209: public class BwaveCycTaR7HelperTests
Line 7210: {
Line 7211: private static MethodInfo GetStaticMethod(string name) =>
```

**Operation:** INSERT after line 7210 (after opening brace `{` of the class).

**Insert after line 7210:**
```csharp
        private readonly CopyEngine _engine = CopyEngine.Instance;

        private static System.Reflection.MethodInfo GetMethod(string name) =>
            typeof(CopyEngine).GetMethod(name, System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);

        private static System.Reflection.FieldInfo GetField(string name) =>
            typeof(CopyEngine).GetField(name, System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);

```

---

## METHOD SIGNATURES (referenced types)

| Symbol | Declaration |
|---|---|
| `CopyEngine.IsDispatchTriggerState` | `internal static bool IsDispatchTriggerState(OrderState state, OrderType type)` |
| `CopyEngine.Instance` | `internal static CopyEngine Instance { get; }` — singleton accessor |
| `CopyEngine.CopyRule` | `internal readonly struct CopyRule` — nested inside `CopyEngine` at `CopyEngine.cs:459` |
| `System.Reflection.MethodInfo` | Standard BCL type; use `typeof(CopyEngine).GetMethod(name, BindingFlags)` |
| `System.Reflection.FieldInfo` | Standard BCL type; use `typeof(CopyEngine).GetField(name, BindingFlags)` |
| `BindingFlags` | `System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance` |

---

## JS RULE CONSTRAINTS

| Rule | Constraint | Applies to |
|---|---|---|
| **JS-021** | No `lock()` statement in any added code | All fixes — zero lock() introduced |
| **JS-042** | All added strings and comments must be ASCII-only (no Unicode, emoji, curly quotes) | Skip strings in F4/F5/F6; comment in F7 DW-B96 string |
| **JS-013** | CYC of every method (new or modified) must remain <= 3; all test helpers are straight-line (CYC=1) | F7 updated method CYC=1; F9/F10 helpers CYC=1 |
| **JS-010** | Use `CopyEngine.Instance`, never `new CopyEngine()` | F8 and F10 (_engine field) |

---

## XUNIT [Fact] NAMES

| Test name | Expected result after ticket applied |
|---|---|
| `T_CLONE_02` | PASS |
| `T_CLONE_03` | PASS |
| `T_CLONE_04` | PASS |
| `T_B66OBJ_02` | PASS |
| `T_CLONE_XISO_01` | PASS |
| `IsDispatchTriggerState_CorrectStates` (renamed from `_ReturnsTrueForSubmittedAndAccepted`) | PASS |
| F4 tests (8 tests) | SKIP — `net48: System.Collections.Immutable not available` |
| F5 test (`FindFollowerBracketOrder_NullableReturnType`) | SKIP — `net48: NullabilityInfoContext requires .NET 6+` |
| F6 tests (8 tests) | SKIP — `NT8-runtime: NinjaTrader.NinjaScript.Instruments not available` |

---

## 7-SCAN CHECKLIST

The engineer MUST run all 7 scans before declaring BUILD_PASS. Record actual command output for every scan in `ticket-1-completion.md`.

### SCAN-1: lock() check

```powershell
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "\block\s*\(" | Where-Object { $_.Line -notmatch "^\s*//" }
```

**Required result:** 0 matches

### SCAN-2: Non-ASCII check

```powershell
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "[^\x00-\x7F]"
```

**Required result:** 0 matches

### SCAN-3: Build error check

```powershell
dotnet build src\PropTraderTools\PropTraderTools.Tests.csproj /nologo 2>&1 | Select-String "error CS"
```

**Required result:** 0 matches

### SCAN-4: Build summary

```powershell
dotnet build src\PropTraderTools\PropTraderTools.Tests.csproj /nologo 2>&1 | Select-Object -Last 3
```

**Required result:** `0 Error(s)` in output

### SCAN-5: Test run

```powershell
dotnet test src\PropTraderTools\PropTraderTools.Tests.csproj /nologo 2>&1 | Select-Object -Last 30
```

**Required result:**  
- `T_CLONE_02`, `T_CLONE_03`, `T_CLONE_04`, `T_B66OBJ_02`, `T_CLONE_XISO_01` = PASS  
- Minimum 17 tests skipped (F4: 8, F5: 1, F6: 8)  
- Record full counts: `X passed / Y failed / Z skipped`

### SCAN-6: Hard-link sync

```powershell
powershell -File .\deploy-sync.ps1 2>&1 | Select-Object -Last 5
```

**Required result:** `SYNC COMPLETE`

### SCAN-7: Hard-link count

```powershell
fsutil hardlink list "src\PropTraderTools\CopyEngineTests.cs" | Measure-Object -Line
```

**Required result:** 1 (test file is not hard-linked to NT8 install — expected)

---

## ACCEPTANCE CRITERIA

1. `dotnet build src\PropTraderTools\PropTraderTools.Tests.csproj /nologo` = `0 Error(s)`
2. `dotnet test` exits with 0 genuine regressions (NT8-runtime failures are expected/classified)
3. `T_CLONE_02`, `T_CLONE_03`, `T_CLONE_04`, `T_B66OBJ_02`, `T_CLONE_XISO_01` all PASS
4. `IsDispatchTriggerState_CorrectStates` PASS
5. No new `lock()` lines anywhere in the file (SCAN-1 = 0 matches)
6. No new non-ASCII characters (SCAN-2 = 0 matches)
7. Hard-link sync completes (SCAN-6 = `SYNC COMPLETE`)
8. All 7 scans reported in `ticket-1-completion.md` with actual command output

---

## COMPLETION ARTIFACT

The engineer MUST write: `docs/brain/PTT-REPAIRS-06-TEST-COMPILE/ticket-1-completion.md`

Required content:
- `BUILD_PASS` or `BUILD_FAIL` declaration at the top
- Actual output of all 7 scans (copy-pasted terminal output, not paraphrased)
- For SCAN-5: full pass/fail/skip counts and list of any failing test names
- Classification of every non-PASS test result as one of:
  - `expected` — NT8-runtime or net48 skip (matches F4/F5/F6 skip lists)
  - `genuine regression` — test that should pass but does not (requires immediate investigation)
