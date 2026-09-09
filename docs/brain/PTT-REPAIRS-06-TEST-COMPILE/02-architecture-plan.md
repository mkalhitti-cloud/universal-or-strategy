# PTT-REPAIRS-06-TEST-COMPILE — Architecture Plan
**Epic:** PTT-REPAIRS-06-TEST-COMPILE  
**Output:** `src/PropTraderTools/CopyEngineTests.cs` (single file, surgical edits)  
**Status:** PLAN_COMPLETE  

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

Q1: Are all fixes in the same file (CopyEngineTests.cs)?  
**YES** — every fix below touches only `src/PropTraderTools/CopyEngineTests.cs`.  
Gate stops here. Single-pipeline execution.

---

## SECTION 1 — EXACT FIX CATALOGUE

All line numbers are verified against source reads performed before this plan was written.

---

### Fix F1 — Add namespace-scoped CopyRule alias

**Problem:** `CopyRule` is a nested struct inside `CopyEngine` (`CopyEngine.CopyRule`).  
Bare references to `CopyRule` throughout the file do not resolve without an alias.

**Verification:**  
- `CopyEngine.CopyRule` confirmed at `CopyEngine.cs:459` (nested `internal readonly struct CopyRule`).  
- Bare `CopyRule` found at lines 71, 92, 113, 122, 127, 401, 403, 504, 533, 535, 571, 573, 603, 605, 658, 660, 697, 699, 894, 896, 910, 912, 943, 945, 959, 961, 1348, 1349, 1434, 2537, 2546, 3044, 3068, 7311, 7312 (and more).

**File structure at insertion point:**
```
Line 11: namespace PropTraderTools
Line 12: {
Line 13:     public class CopyEngineTests : IDisposable
```

**Change — insert after line 12:**
```csharp
    using CopyRule = PropTraderTools.CopyEngine.CopyRule;
```

**Effect:** Resolves all bare `CopyRule` references in the file without touching any of them.

---

### Fix F2 — Add `using System.Collections.Generic;`

**Problem:** `Dictionary<string, FollowerAtmMode>` used bare (without fully-qualified name) at lines 2522 and 2529. `System.Collections.Generic` is not in the current file-level using block.

**Verification:** File-level usings (lines 5–9):
```
using System;
using System.Collections.Concurrent;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;
```
No `System.Collections.Generic` present.  
Lines 2522, 2529 confirmed: `new Dictionary<string, FollowerAtmMode>()` (bare Dictionary).

**Change — insert after line 9 (after `using Xunit;`):**
```csharp
using System.Collections.Generic;
```

**Effect:** Resolves `Dictionary<,>` references at lines 2522 and 2529.

---

### Fix F3 — Add `using System.Linq;`

**Problem:** `.Any()` calls on `LocalVariables` at lines 2977, 3823, 3888, 3926 require `System.Linq`. The lines 2781, 2797, 2813 use `.FirstOrDefault()` but those tests are skipped by F6.

**Verification:** `.Any(` confirmed at lines 2977, 3823, 3888, 3926. `.FirstOrDefault(` confirmed at 2781, 2797, 2813 (F6 targets).

**Change — insert after the `using System.Collections.Generic;` added by F2:**
```csharp
using System.Linq;
```

**Effect:** Resolves `.Any()` and `.FirstOrDefault()` LINQ extension method calls.

---

### Fix F4 — Skip tests that use `System.Collections.Immutable`

**Problem:** `System.Collections.Immutable.ImmutableDictionary<,>` is NOT available in .NET Framework 4.8 without a NuGet reference. The test project (`PropTraderTools.Tests.csproj`) targets net48.

**Verification:** `System.Collections.Immutable` found at lines 499, 529, 567, 689, 738, 768, 889, 938.  
The enclosing `[Fact]` attribute lines and method names:

| [Fact] line | Method name |
|---|---|
| 486 | `AddRule_WithMultipliers_StoresCorrectMultipliers` |
| 519 | `GetMultiplier_OutOfRangeIndex_ReturnsOne` |
| 557 | `GetMultiplier_ValidIndex_ReturnsStoredValue` |
| 684 | `GetAtmMode_WithNamedEntry_ReturnsNamedMode` |
| 728 | `SaveLoad_RoundTrip_PreservesMultipliers` |
| 763 | `SaveLoad_RoundTrip_PreservesAtmModeNames` |
| 879 | `SetFollowerMultiplier_UpdatesMultiplier_RebuildsRules` |
| 928 | `SetAtmMode_UpdatesAtmTemplate_RebuildsRules` |

None of these currently have a `Skip` parameter (confirmed: all show bare `[Fact]`).

**Change — replace `[Fact]` with `[Fact(Skip = "net48: System.Collections.Immutable not available")]` at each of the 8 lines listed above.**

Old → New for each:
```
[Fact]
→
[Fact(Skip = "net48: System.Collections.Immutable not available")]
```

Apply at lines: **486, 519, 557, 684, 728, 763, 879, 928**.

---

### Fix F5 — Skip NullabilityInfoContext test

**Problem:** `System.Reflection.NullabilityInfoContext` requires .NET 6+. The test at lines 452–454 uses it.

**Verification:** Lines 452–454:
```csharp
var ctx = new System.Reflection.NullabilityInfoContext();
var nullInfo = ctx.Create(method.ReturnParameter);
Assert.Equal(System.Reflection.NullabilityState.Nullable, nullInfo.WriteState);
```
Enclosing method: `FindFollowerBracketOrder_NullableReturnType` at line 441.  
`[Fact]` attribute at line **440**. Currently bare `[Fact]`.

**Change — replace line 440:**
```
[Fact]
→
[Fact(Skip = "net48: NullabilityInfoContext requires .NET 6+")]
```

Apply at line: **440**.

---

### Fix F6 — Skip tests referencing `NinjaTrader.NinjaScript.Instruments`

**Problem:** `NinjaTrader.NinjaScript.Instruments.Instrument` is not available outside NT8 runtime. Tests that reference this type fail with a type-load exception (or compile error if the stub assembly doesn't include it).

**Verification — [Fact] lines and method names:**

| [Fact] line | Method name | Lines with Instruments ref |
|---|---|---|
| 2640 | `T_B26_01_TrailBe_WithNoRule_StillMovesStop` | 2651, 2663 |
| 2775 | `T_B28_01_Trim_LeaderOverload_Exists` | 2786 |
| 2791 | `T_B28_02_Flatten_LeaderOverload_Exists` | 2801 |
| 2807 | `T_B28_03_CancelPendingEntries_LeaderOverload_Exists` | 2817 |
| 2850 | `MoveStopToBreakEven_RetriesOnCreateOrderFailure` | 2863 |
| 2876 | `CancelOneAccount_UsesSnapshotNotLiveOrders` | 2888 |
| 2904 | `ArmPendingBe_SkipsWhenFlat` | 2925 |
| 3798 | `T_B67_01_CancelQxBrackets_called_before_CreateOrder` | 3813 |

None of these currently have a `Skip` parameter.

**Change — replace `[Fact]` with `[Fact(Skip = "NT8-runtime: NinjaTrader.NinjaScript.Instruments not available")]` at each of the 8 lines listed above.**

Apply at lines: **2640, 2775, 2791, 2807, 2850, 2876, 2904, 3798**.

**NOTE on F3 interaction:** After F6 skips lines 2775/2791/2807 (`.FirstOrDefault()` callers), F3 is still required for `.Any()` callers at lines 2977, 3823, 3888, 3926. F3 must be applied regardless.

---

### Fix F7 — Update IsDispatchTriggerState calls to 2-param signature

**Problem:** The test method `IsDispatchTriggerState_ReturnsTrueForSubmittedAndAccepted` (line 2987) calls `CopyEngine.IsDispatchTriggerState(OrderState.X)` with ONE argument. The actual method signature at `CopyEngine.cs:2324` is:

```csharp
internal static bool IsDispatchTriggerState(OrderState state, OrderType type)
```

The test also asserts `Working → false` which is WRONG per `CopyEngine.cs:2330` (`state == OrderState.Working` is in the true-path for Limit orders).

**Verification of actual signature** (CopyEngine.cs:2324–2332):
```csharp
internal static bool IsDispatchTriggerState(OrderState state, OrderType type) =>
    (type == OrderType.Market && state == OrderState.Submitted)
    || (
        type == OrderType.Limit
        && (
            state == OrderState.Accepted  // AddOn path
            || state == OrderState.Working // ChartTrader path (DW-B96)
        )
    );
```

**Corrected OrderType mapping per spec:**
| OrderState | OrderType to pass | Expected result |
|---|---|---|
| Submitted | OrderType.Market | **True** |
| Accepted | OrderType.Limit | **True** |
| Initialized | OrderType.Limit | False |
| Working | OrderType.Limit | **True** (DW-B96 ChartTrader path) |
| Filled | OrderType.Limit | False |
| Cancelled | OrderType.Limit | False |

**Change 1 — rename method at line 2987:**
```
public void IsDispatchTriggerState_ReturnsTrueForSubmittedAndAccepted()
→
public void IsDispatchTriggerState_CorrectStates()
```

**Change 2 — update each Assert call (lines 2990–3016):**

Old line 2990–2993:
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

Old lines 2996–2999:
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

Old lines 3002–3005:
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

Old lines 3006–3009 — **Assert changes from False to True:**
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

Old lines 3010–3013:
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

Old lines 3014–3017:
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

### Fix F8 — Replace `new CopyEngine()` with `CopyEngine.Instance`

**Problem:** `CopyEngine` has a private constructor (`CopyEngine.cs:601`):
```csharp
private CopyEngine() { }
```
Two occurrences of `new CopyEngine()` in the test file will not compile.

**Verification:**
- Line **4036**: `var engine = new CopyEngine();` — inside `T_B69_01_CancelAllAccountOrders_cancels_PTT_Copy_orders`
- Line **4137**: `var engine = new CopyEngine();` — inside `T_B69_07_CancelAllAccountOrders_null_acc_noOp`

**Change:**
```
new CopyEngine()  →  CopyEngine.Instance
```
Apply at lines **4036** and **4137**.

---

### Fix F9 — Add GetMethod/GetField helpers to `B79CancelRaceGuardTests`

**Problem:** `B79CancelRaceGuardTests` (line 5768) uses bare `GetMethod(...)` calls (e.g., line 5903: `var m = GetMethod("TryFireImmediateBeIfAlreadyAtLevel");`) but has no `GetMethod` helper defined. Only `ContainsMethodToken` exists (lines 5771–5782).

**Verification:**
- Class declared at line 5768: `public class B79CancelRaceGuardTests`
- `ContainsMethodToken` helper: lines 5771–5782. Closing brace at line 5782.
- No `GetMethod` or `GetField` exists in this class.
- Bare `GetMethod(...)` used at lines 5903, 5911, 5919, 5927, 5937, 5945, 5953, 5961, 5970, 5975, 5982, 5993, 5998, and others.

**Change — insert after line 5782 (after closing brace of ContainsMethodToken):**
```csharp

        private static System.Reflection.MethodInfo GetMethod(string name) =>
            typeof(CopyEngine).GetMethod(name, System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);

        private static System.Reflection.FieldInfo GetField(string name) =>
            typeof(CopyEngine).GetField(name, System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);
```

---

### Fix F10 — Add `_engine` + GetMethod + GetField to `BwaveCycTaR7HelperTests`

**Problem:** `BwaveCycTaR7HelperTests` (line 7209) uses `_engine` (e.g., line 7308: `_engine.SetEnabled(false)`) and bare `GetMethod(...)` / `GetField(...)` calls (e.g., lines 7709, 7713) but declares neither `_engine` field, `GetMethod` helper, nor `GetField` helper. This class only has `GetStaticMethod` (line 7211) and `GetInstanceMethod` (line 7214).

**Verification:**
- Class declared at line 7209: `public class BwaveCycTaR7HelperTests`
- Opening brace at line 7210: `{`
- Next line 7211: `private static MethodInfo GetStaticMethod(string name) =>`
- No `_engine`, no `GetMethod`, no `GetField` defined in this class.
- `_engine` used at lines 7308, 7309, 7341 (etc.).
- `GetMethod(...)` used at line 7709.
- `GetField(...)` used at line 7713.

**Change — insert after line 7210 (after opening brace `{`):**
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

## SECTION 2 — BUILD VERIFICATION PLAN

**Command:**
```powershell
dotnet build src\PropTraderTools\PropTraderTools.Tests.csproj /nologo 2>&1 | Select-Object -Last 5
```

**Expected output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**If build fails:** Record the exact error messages and line numbers. Do not proceed to test run until 0 errors.

---

## SECTION 3 — TEST RUN PLAN

**Command:**
```powershell
dotnet test src\PropTraderTools\PropTraderTools.Tests.csproj /nologo 2>&1 | Select-Object -Last 30
```

**Expected outcome:** Record `X passed / Y failed / Z skipped`.

**Expected skip count:** At minimum:
- F4: 8 tests skipped
- F5: 1 test skipped
- F6: 8 tests skipped
- Total expected skips from this plan: **17 tests**

**Failure categorization:**
| Category | Description | Action |
|---|---|---|
| NT8-runtime | Tests that reach NT8 API at runtime (Account.All, etc.) | Expected — not a regression |
| net48 compile stub | Tests hitting missing .NET 6+ or Immutable API | Should be caught by F4/F5/F6 skips |
| Genuine regression | Test that was previously passing, now fails | Investigate immediately |
| State pollution | CopyEngine.Instance state leaked from one test to another | Note for future IDisposable/Reset work |

---

## SECTION 4 — JANE STREET COMPLIANCE

| Rule | Requirement | Status |
|---|---|---|
| JS-021 | No `lock()` introduced | PASS — no lock() added anywhere |
| JS-042 | All strings/comments ASCII-only | PASS — all skip strings are ASCII |
| JS-013 | CYC of new test methods ≤ 3 | PASS — F9/F10 helpers CYC=1; F7 updated method CYC=1 |
| JS-010 | Private ctor respected | PASS — F8 replaces `new CopyEngine()` with `CopyEngine.Instance` |
| JS-002 | Null contract explicit | PASS — GetMethod/GetField return nullable reference; callers use Assert.NotNull |

---

## SECTION 5 — 7-SCAN CHECKLIST

| Scan | Check | Expected |
|---|---|---|
| SCAN-1 | `grep -c "lock(" src/PropTraderTools/CopyEngineTests.cs` | 0 |
| SCAN-2 | Non-ASCII character count in CopyEngineTests.cs | 0 |
| SCAN-3 | `dotnet build` error count | 0 |
| SCAN-4 | `dotnet build` warning count | 0 (or pre-existing only) |
| SCAN-5 | `dotnet test` exits with recorded counts | Pass/Skip/Fail counts recorded |
| SCAN-6 | `powershell -File .\deploy-sync.ps1` output | SYNC COMPLETE |
| SCAN-7 | Hard-link count for `CopyEngineTests.cs` | 1 (test files are not hard-linked to NinjaTrader) |

---

## Component Summary

| Fix | Lines Modified | Operation |
|---|---|---|
| F1 | Insert after line 12 | Add namespace-scoped `using CopyRule = ...` alias |
| F2 | Insert after line 9 | Add `using System.Collections.Generic;` |
| F3 | Insert after F2 | Add `using System.Linq;` |
| F4 | Lines 486, 519, 557, 684, 728, 763, 879, 928 | Replace `[Fact]` with Skip attribute |
| F5 | Line 440 | Replace `[Fact]` with Skip attribute |
| F6 | Lines 2640, 2775, 2791, 2807, 2850, 2876, 2904, 3798 | Replace `[Fact]` with Skip attribute |
| F7 | Lines 2987–3017 | Rename method + update 6 Assert calls + add OrderType arg + fix Working=True |
| F8 | Lines 4036, 4137 | Replace `new CopyEngine()` with `CopyEngine.Instance` |
| F9 | Insert after line 5782 | Add GetMethod + GetField helpers to B79CancelRaceGuardTests |
| F10 | Insert after line 7210 | Add _engine + GetMethod + GetField to BwaveCycTaR7HelperTests |

**Total files changed:** 1 (`src/PropTraderTools/CopyEngineTests.cs`)  
**Source code ban respected:** No `.cs` production files touched.
