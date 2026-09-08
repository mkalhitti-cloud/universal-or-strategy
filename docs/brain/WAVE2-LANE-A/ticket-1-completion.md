# Ticket 1 Completion Report: WAVE2-LANE-A

**Status**: BUILD_PASS
**Scope lock**: TICKET 1 ONLY
**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-09-07
**File**: `src/PropTraderTools/CopyEngine.cs`
**Test file**: `tests/PropTraderTools.Tests/Wave2LaneATests.cs`

---

## Summary of Changes

### Change 1: IsExitSignalName -- replaced two if-checks with helper call

In `CopyEngine.IsExitSignalName` (lines ~2347-2371):

**REMOVED:**
```csharp
    if (name == "Close")
        return true; // (2)
    if (name == "Flatten")
        return true; // (3)
```

**REPLACED WITH:**
```csharp
    if (IsNativeCloseOrFlattenSignal(name))
        return true; // (2-3)
```

### Change 2: New helper IsNativeCloseOrFlattenSignal added

Inserted immediately after `IsExitSignalName` closing brace (before `IsNativeExitName`):

```csharp
// CCN=3: base(1)+Close(1)+Flatten(1). WAVE2-LANE-A extraction.
// Consolidates the two NT8 platform-generated anonymous exit signal names.
// internal: accessible to xUnit via InternalsVisibleTo("PropTraderTools.Tests") at CopyEngine.cs:46.
// JS-021: no lock (static). JS-001: no throw. JS-002: returns bool. ASCII-only.
internal static bool IsNativeCloseOrFlattenSignal(string name) =>
    name == "Close" || name == "Flatten";
```

### Change 3: CCN comment added to IsExitSignalName header

Added comment block above `IsExitSignalName`:
```csharp
// CCN=8: base(1)+null(1)+empty(1)+PTT-(1)+IsNativeClose(1)+Rev(1)+Exit(1)+IsAtmTarget(1). WAVE2-LANE-A.
// JS-021: no lock. JS-001: no throw. JS-002: returns bool. ASCII-only.
```

---

## CCN Math

### IsExitSignalName (CCN = 8, reduced from 9)

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

### IsNativeCloseOrFlattenSignal (CCN = 3, new helper)

| # | Decision point | Count |
|---|---|---|
| base | function entry | +1 |
| 1 | `name == "Close"` (left of `||`) | +1 |
| 2 | `name == "Flatten"` (right of `||`) | +1 |
| **Total** | | **3** |

---

## 7-Scan Results

### SCAN-01: Lizard CCN (lizard ... --csv | Where-Object {CCN -gt 8})

```
CCN  Function                         File
---  --------                         ----
9    TrimSignal::HasArmingAtmBrackets  CopyEngine.cs
```

**Result**: One pre-existing hit -- `HasArmingAtmBrackets` (CCN=9).
This is the Ticket 2 scope target. It was NOT modified in Ticket 1.
`IsExitSignalName` is NOT in the list (CCN reduced from 9 to 8 -- extraction successful).
**Ticket 1 target (IsExitSignalName): PASS. Pre-existing Ticket 2 target will be fixed in Ticket 2.**

### SCAN-02: lock() ban

```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\("
```

**Result**: All matches are in comments (e.g., "no lock()" documentation). Zero actual `lock(` usage.
**New/modified lines (2347-2381): 0 lock() hits. PASS**

### SCAN-03: throw ban

```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "throw new"
```

**Result**: No output -- zero `throw new` anywhere in CopyEngine.cs.
**PASS (0 hits)**

### SCAN-04: return null ban

```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "return null"
```

**Result**: Pre-existing `return null` in unrelated methods (not in lines 2347-2381).
Zero new `return null` in new/modified lines.
**PASS (0 new hits in scope)**

### SCAN-05: ASCII-only

```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]" -Encoding UTF8
```

**Result**: No output -- zero non-ASCII characters in CopyEngine.cs.
**PASS (0 hits)**

### SCAN-06: Build + Sync

**Step 1: dotnet build**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```
**PASS**

**Step 2: ptt-sync-and-verify.ps1**
```
OK       LicenseClient.cs
OK       TradeCopierAddOn.cs
OK       TradeCopierPanel.cs
OK       TradeCopierWindow.cs
OK       Core\PttContracts.cs
OK       Features\PttBreakEven.cs
OK       Features\PttBreakEvenSwap.cs
OK       Features\PttCancel.cs
OK       Features\PttCopier.cs
OK       Features\PttFlatten.cs
OK       Features\PttFollowerStrategy.cs
OK       Features\PttGlobalBreakEven.cs
OK       Features\PttGlobalQuickExit.cs
OK       Features\PttQuickExit.cs
OK       Features\PttTrim.cs
0 MISMATCH
```
**PASS**

**Step 3: F5 in NinjaTrader 8**
F5 required -- manual step. Cannot be automated from this session.
Report: F5 required -- manual NT8 recompile after sync.

### SCAN-07: Tests pass

```
Passed!  - Failed: 0, Passed: 260, Skipped: 3, Total: 263, Duration: 36 ms
```

- 248 pre-existing tests passing
- 12 new Wave2LaneA Ticket 1 tests added
- Total: 260 passing, 0 failing
**PASS (260 >= 248 threshold)**

---

## Test File

**File**: `tests/PropTraderTools.Tests/Wave2LaneATests.cs`
**Class**: `Wave2LaneAIsExitSignalNameTests`
**Pattern**: Reflection seam (CopyEngine internal static methods, no NT8 runtime needed)

Note on implementation: `IsExitSignalName` and `IsNativeCloseOrFlattenSignal` are internal static
methods on `CopyEngine` directly -- not on the `TrimSignal` struct (which is a private readonly struct
with only a `Create` factory method). The reflection seam targets `CopyEngine` type.

| # | Test | Result |
|---|---|---|
| 1 | IsExitSignalName_NullInput_ReturnsFalse | PASS |
| 2 | IsExitSignalName_EmptyString_ReturnsTrue | PASS |
| 3 | IsExitSignalName_PttPrefixed_ReturnsTrue | PASS |
| 4 | IsExitSignalName_CloseSignal_ReturnsTrue | PASS |
| 5 | IsExitSignalName_FlattenSignal_ReturnsTrue | PASS |
| 6 | IsExitSignalName_RevPrefix_ReturnsTrue | PASS |
| 7 | IsExitSignalName_ExitPrefix_ReturnsTrue | PASS |
| 8 | IsExitSignalName_AtmTarget_ReturnsTrue | PASS |
| 9 | IsExitSignalName_EntrySignal_ReturnsFalse | PASS |
| 10 | IsNativeCloseOrFlattenSignal_Close_ReturnsTrue | PASS |
| 11 | IsNativeCloseOrFlattenSignal_Flatten_ReturnsTrue | PASS |
| 12 | IsNativeCloseOrFlattenSignal_CloseLowercase_ReturnsFalse | PASS |

---

## Issues and Deviations

### Issue 1: SCAN-01 pre-existing HasArmingAtmBrackets CCN=9

The lizard scan shows `HasArmingAtmBrackets` with CCN=9. This is the Ticket 2 target. It was 
CCN=9 before Ticket 1 started and was not modified (scope lock honoured). It will be fixed in Ticket 2.
The Ticket 1 target `IsExitSignalName` is NOT in the CCN>8 list, confirming the extraction succeeded.

### Issue 2: TrimSignal struct vs CopyEngine direct methods

The ticket spec references "class TrimSignal (nested static class)". In the actual codebase, 
TrimSignal is a `private readonly struct` with only a `Create` factory method. `IsExitSignalName`
and `IsNativeCloseOrFlattenSignal` are static methods on `CopyEngine` directly. The test file 
uses `CopyEngine` as the reflection target (not `TrimSignal`). Behavior is identical -- the 
methods are accessible, all 12 tests pass.

---

## Commit Message

```
refactor(CopyEngine): extract IsNativeCloseOrFlattenSignal CYC 9->8 [WAVE2-LANE-A T1]
```

---

## BUILD_PASS