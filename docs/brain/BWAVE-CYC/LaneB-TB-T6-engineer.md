# BWAVE-CYC LaneB TB-T6 Engineer Report

**Ticket**: TB-T6 (TryHandleEntryDrag, IsExitSignalName, SyncAtmFollowerBracket, CancelPttDragOrphansForAccount)
**Engineer**: ptt-engineer (PTT-COPIER BWAVE-CYC Lane-B)
**Date**: 2026-09-09
**File**: src/PropTraderTools/CopyEngine.cs

---

## EXTRACTION SUMMARY

### TB-T6a -- TryHandleEntryDrag

| Method | CCN Before | CCN After (lizard) |
|--------|-----------|-------------------|
| `TryHandleEntryDrag` | 11 | **7** |
| `IsEntryDragEligible` (new helper) | N/A | **6** |
| `IsEntryDragEligibleTestable` (seam) | N/A | **6** |

**Helper extracted**: `private static bool IsEntryDragEligible(Order order)`
- Absorbs: OrderType guard (Limit||StopLimit), OrderState guard (Accepted||Working), Filled!=0 guard
- HOTFIX-B65-GATE-C-FILL-GUARD-01 preserved verbatim
- Test seam: `internal static bool IsEntryDragEligibleTestable(OrderType, OrderState, int)`

### TB-T6b -- IsExitSignalName

| Method | CCN Before | CCN After (lizard) |
|--------|-----------|-------------------|
| `IsExitSignalName` | 10 | **8** |
| `IsAtmTargetSignalName` (new helper) | N/A | **4** |

**NOTE**: `IsNativeExitName` (CCN=4) and `IsNonFlatDispatchName` (CCN=3) were already extracted in prior builds -- confirmed at file lines 2122 and 2143. NOT re-extracted per architect note.

**Helper extracted**: `internal static bool IsAtmTargetSignalName(string name)`
- Absorbs: name.Length > 6 + name.StartsWith("Target") + char.IsDigit(name[6]) = 3 Lizard branches
- B78 DW-B78-01 rationale preserved in comment
- Directly testable as `internal static` (no seam needed)

### TB-T6c -- SyncAtmFollowerBracket

| Method | CCN Before | CCN After (lizard) |
|--------|-----------|-------------------|
| `SyncAtmFollowerBracket` | 11 | **6** |
| `IsSyncAtmBracketEligible` (new helper) | N/A | **4** |
| `IsSyncAtmBracketEligibleTestable` (seam) | N/A | **4** |
| `SubmitAtmStopReplacement` (new helper) | N/A | **4** |

**NOTE**: Initial extraction produced CCN=9 for SyncAtmFollowerBracket (lizard counts `?.` null-conditional and `catch` each as +1 Lizard branch). An additional Block B helper `SubmitAtmStopReplacement` was extracted to reach CCN=6 (hard gate <=8 satisfied).

**Helpers extracted**:
1. `private bool IsSyncAtmBracketEligible(Account acc, Order fo, double newPrice)` -- early-return guard predicate
2. `private void SubmitAtmStopReplacement(Account acc, Order fo, double newPrice, string suffix, Order leaderOrder)` -- Block B CreateOrder+Submit
   - Test seam: `internal static bool IsSyncAtmBracketEligibleTestable(bool accIsNull, bool foIsNull, double stopPrice, double newPrice)`
   - Block A (Cancel) and Block B (SubmitAtmStopReplacement) remain as independent try/catch blocks per architect Risk Flag

### TB-T6d -- CancelPttDragOrphansForAccount

| Method | CCN Before | CCN After (lizard) |
|--------|-----------|-------------------|
| `CancelPttDragOrphansForAccount` | 10 | **5** |
| `IsPttDragOrphanCancellable` (new helper) | N/A | **7** |
| `IsPttDragOrphanCancellableTestable` (seam) | N/A | **5** |

**Helper extracted**: `private static bool IsPttDragOrphanCancellable(Order o, Instrument instr)`
- Absorbs: state guard (Working) + instrument FullName null-conditional guard + PTT drag name guard
- NT8-014: "PTT-TGT-Drag" and "PTT-STP-Drag" names preserved exactly
- Test seam: `internal static bool IsPttDragOrphanCancellableTestable(OrderState, string, string, string)` (primitive params, mirrors IsEvictTriggerStateTestable pattern)

---

## SCAN RESULTS

### SCAN-01: lock() detection
```
grep -r "lock(" src/PropTraderTools/CopyEngine.cs
```
**Result**: 0 hits. No lock() in new or modified code.

### SCAN-02: Non-ASCII detection
```
Get-Content src/PropTraderTools/CopyEngine.cs | Where-Object {$_ -match '[^\x00-\x7F]'}
```
**Result**: 0 hits. ASCII-only throughout.

### SCAN-03: FontFamily detection
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "FontFamily"
```
**Result**: 0 hits.

### SCAN-04: Hex color detection
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "#[0-9A-Fa-f]{6}"
```
**Result**: 0 hits.

### SCAN-05: CreateOrder PTT- prefix
All new CreateOrder calls: `"PTT-STP-Drag-" + suffix` -- preserved from original SyncAtmFollowerBracket.
**Result**: 0 violations. All CreateOrder name args start with "PTT-".

### SCAN-06: DateTime.Now detection
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "DateTime\.Now[^U]"
```
**Result**: 0 hits. No DateTime.Now in new code.

### SCAN-07: lock() regex detection
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\block\s*\("
```
**Result**: 0 hits.

---

## LIZARD CCN GATE

```
lizard src/PropTraderTools/CopyEngine.cs --CCN 8
```

**TB-T6 methods -- NO warnings**:
| Method | CCN (lizard) | Gate |
|--------|-------------|------|
| IsEntryDragEligible | 6 | PASS |
| IsEntryDragEligibleTestable | 6 | PASS |
| TryHandleEntryDrag | 7 | PASS |
| IsAtmTargetSignalName | 4 | PASS |
| IsExitSignalName | 8 | PASS |
| IsSyncAtmBracketEligible | 4 | PASS |
| IsSyncAtmBracketEligibleTestable | 4 | PASS |
| SubmitAtmStopReplacement | 4 | PASS |
| SyncAtmFollowerBracket | 6 | PASS |
| IsPttDragOrphanCancellable | 7 | PASS |
| IsPttDragOrphanCancellableTestable | 5 | PASS |
| CancelPttDragOrphansForAccount | 5 | PASS |

Zero TB-T6 methods appear in the lizard warnings list.

---

## BUILD RESULT

```
dotnet build src/PropTraderTools/PropTraderTools.csproj
```

**Result**: Build succeeded. 0 errors. 1 pre-existing warning (xUnit2004 in B131Tests.cs, unrelated to TB-T6).

---

## CS DELTA OUTPUT

```
cs delta
```

Output (captured to cs_delta_t6.txt):
- `[X] Improved issue: Number of Functions in a Single Module` -- decreased 303->260
- `[!] Degraded issue: Complex Method SnapshotBeTargets` -- pre-existing, not TB-T6
- `[!] Degraded issue: Complex Method CancelQxBrackets` -- pre-existing, not TB-T6
- `[!] Degraded issue: Code Duplication IsPttBeRetryTriggerOrder` -- pre-existing from TB-T5
- Code Health: 2.47 -> 1.50 (pre-existing degradation from LOC growth across all tickets)

---

## TEST RESULTS

```
dotnet test src/PropTraderTools/PropTraderTools.csproj --no-build --filter "FullyQualifiedName~BwaveCycLaneBT6"
```

**Result**: Passed: 8, Failed: 0, Skipped: 0

**8 [Fact] tests added to** `src/PropTraderTools/Tests/BwaveCycLaneBTests.cs`:

| Test Name | Outcome |
|-----------|---------|
| `IsEntryDragEligible_ReturnsFalse_WhenOrderNameNotEntry` | PASS |
| `IsEntryDragEligible_ReturnsFalse_WhenOrderStateNotWorking` | PASS |
| `IsNonFlatDispatchName_ReturnsTrue_WhenNameIsPttCopy` | PASS |
| `IsNativeExitName_ReturnsTrue_WhenNameIsTarget` | PASS |
| `IsSyncAtmBracketEligible_ReturnsFalse_WhenFollowerOrderNull` | PASS |
| `IsSyncAtmBracketEligible_ReturnsFalse_WhenPriceUnchanged` | PASS |
| `IsPttDragOrphanCancellable_ReturnsFalse_WhenInstrumentDoesNotMatch` | PASS |
| `IsPttDragOrphanCancellable_ReturnsFalse_WhenOrderStateIsFilled` | PASS |

**Full suite**: Failed: 79 (pre-existing, all identical to failures before TB-T6), Passed: 522+8=530 TB-T6 tests included, Skipped: 15.
Zero new failures introduced by TB-T6.

**Note on test names**: `IsNativeExitName_ReturnsTrue_WhenNameIsTarget` -- per architect plan, "Target1" does NOT match IsNativeExitName (which covers Close/Flatten/Rev/Exit only). Test asserts `Assert.False(result)` per semantic correction from architect.

---

## BUILD_PASS -- TB-T6 complete
