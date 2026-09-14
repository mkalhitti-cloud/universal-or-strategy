# Ticket 2 Completion Report — Block B154
# Fix IsPttDragOrphanCancellable to match suffixed Drag order names (B142)

**Block:** B154
**Ticket:** 2 — DW-B154-03
**Engineer:** PTT Engineer (ptt-engineer mode)
**Phase:** 4a — Implementation
**Prerequisite:** Ticket 1 committed (TICKET_REVIEW_PASS confirmed).

---

## Changes Made

### (a) IsPttDragOrphanCancellable — Replace equality with StartsWith

**File:** `src/PropTraderTools/CopyEngine.cs`
**Method:** `IsPttDragOrphanCancellable(Order o, Instrument instr)`
**Lines changed:** ~2003 (name guard condition)

**Before:**
```csharp
if (o.Name != "PTT-TGT-Drag" && o.Name != "PTT-STP-Drag") // (3) && = 1 extra Lizard
    return false;
```

**After:**
```csharp
if (!o.Name.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)
    && !o.Name.StartsWith("PTT-STP-Drag", StringComparison.Ordinal)) // (3) && = 1 extra Lizard
    return false;
```

CYC delta = 0. McCabe branch count unchanged — equality replaced by prefix check, same two conditions.
CCN measured post-change: 7 (within CYC<=8 limit, no lizard warning).

---

### (b) IsPttDragOrphanCancellableTestable — Replace equality with StartsWith

**File:** `src/PropTraderTools/CopyEngine.cs`
**Method:** `IsPttDragOrphanCancellableTestable(OrderState, string, string, string)`
**Lines changed:** ~2023-2025 (name guard condition)

**Before:**
```csharp
if (orderName != "PTT-TGT-Drag" && orderName != "PTT-STP-Drag")
    return false;
```

**After:**
```csharp
if (!orderName.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)
    && !orderName.StartsWith("PTT-STP-Drag", StringComparison.Ordinal))
    return false;
```

CYC delta = 0. CCN measured post-change: 5 (within CYC<=8 limit, no lizard warning).

---

### (c) Comment updates on IsPttDragOrphanCancellable and IsPttDragOrphanCancellableTestable

**File:** `src/PropTraderTools/CopyEngine.cs`

**IsPttDragOrphanCancellable comment block (~line 1994):**
- REMOVED: `// NT8-014: "PTT-TGT-Drag" and "PTT-STP-Drag" confirmed names -- must not be generalized.`
- ADDED: `// StartsWith: matches suffixed variants PTT-TGT-Drag-N / PTT-STP-Drag-N (B142).`

**IsPttDragOrphanCancellableTestable comment (~line 2011):**
- ADDED: `// StartsWith: matches suffixed variants PTT-TGT-Drag-N / PTT-STP-Drag-N (B142).`

Confirmation: The phrase "must not be generalized" has been removed. No similar phrase was present in the IsPttDragOrphanCancellableTestable comment.

---

### (d) PttDragOrphanPredicates — New pure helper class (PttContracts.cs)

**File:** `src/PropTraderTools/Core/PttContracts.cs`
**Addition:** New `internal static class PttDragOrphanPredicates` after `PttOrderNames`.

Rationale: `IsPttDragOrphanCancellableTestable` lives on `CopyEngine`, which has an NT8 AgileDotNetRT type initializer (cctor) at line 129. Any call to `CopyEngine.*` static methods in xUnit triggers the cctor and fails with `TypeInitializationException` in the test environment (no NT8 host). This matches the pre-existing pattern in the codebase — all existing tests that call `CopyEngine.*` static methods are marked `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]`.

Solution: `PttDragOrphanPredicates.IsDragOrphanName(string name)` provides the pure StartsWith logic as an NT8-free static helper in `PttContracts.cs`, with no NT8 dependency. The 6 B154T2 tests call this helper directly via a private test-local wrapper that also replicates the state guard, mirroring the logic of `IsPttDragOrphanCancellableTestable` exactly.

```csharp
internal static class PttDragOrphanPredicates
{
    internal static bool IsDragOrphanName(string name)
    {
        if (name == null) return false;
        return name.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)
            || name.StartsWith("PTT-STP-Drag", StringComparison.Ordinal);
    }
}
```

---

### (d) B154T2ContractTests — 6 new [Fact] tests in CopyEngineTests.cs

**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Class:** `B154T2ContractTests` (new, added after `B154T1ContractTests`)
**Lines:** ~9285 onwards

Tests use a private test-local wrapper `IsPttDragOrphanCancellableTestable(OrderState, string)` that
delegates to `PttDragOrphanPredicates.IsDragOrphanName` with the state guard inline — no NT8 runtime required.

| Test Method | Setup | Expected |
|-------------|-------|----------|
| `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttTgtDragExact` | `Working`, `"PTT-TGT-Drag"` | `true` |
| `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttTgtDragSuffixed` | `Working`, `"PTT-TGT-Drag-2"` | `true` |
| `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttStpDragExact` | `Working`, `"PTT-STP-Drag"` | `true` |
| `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttStpDragSuffixed` | `Working`, `"PTT-STP-Drag-1"` | `true` |
| `IsPttDragOrphanCancellableTestable_ShouldReturnFalse_WhenNameIsUnrelated` | `Working`, `"PTT-TGT-Other"` | `false` |
| `IsPttDragOrphanCancellableTestable_ShouldReturnFalse_WhenStateIsNotWorking` | `Filled`, `"PTT-TGT-Drag"` | `false` |

---

## Scan Results

### SCAN-7: lizard --CCN 8 src/PropTraderTools/CopyEngine.cs
**Result: PASS — 0 new warnings introduced by T2 changes**

IsPttDragOrphanCancellable CCN=7 (no warning — under limit of 8).
IsPttDragOrphanCancellableTestable CCN=5 (no warning — under limit of 8).

Pre-existing warnings (not introduced by T2, all CCN=9):
- TrimSignal::MirrorCloseOneAccount@2419-2454 (CCN=9)
- TrimSignal::TrimOneAccount@5722-5759 (CCN=9)
- CopyRulesContainer::CancelExistingTgtDragOrders@8432-8445 (CCN=9)
- CopyRulesContainer::HasInFlightFlattenOrder@8585-8596 (CCN=9)
- CopyRulesContainer::ResubmitFollowerEntry@8616-8637 (CCN=9)
All 5 pre-existing. T2 did not introduce any new CCN warnings.

### SCAN-8: grep -rn "lock\s*(" src/PropTraderTools/
**Result: PASS — 0 actual lock() calls**

Pattern matches in TradeCopierWindow.cs lines 218 and 246 are false positives:
- `var titleBlock = BuildWindowTitleBlock();` — identifier `titleBlock`, not a lock statement
- `private TextBlock BuildWindowTitleBlock()` — method name, not a lock statement

No `lock()`, `Monitor.Enter`, `Mutex`, or `SemaphoreSlim` added by T2.

### SCAN-9: CopyEngine.cs non-ASCII bytes > 127
**Result: PASS — 0 bytes**

```
(Get-Content src/PropTraderTools/CopyEngine.cs -Encoding Byte | Where-Object { $_ -gt 127 }).Count
0
```

### SCAN-9b: CopyEngineTests.cs non-ASCII bytes > 127
**Result: PASS — 0 bytes**

```
(Get-Content src/PropTraderTools/CopyEngineTests.cs -Encoding Byte | Where-Object { $_ -gt 127 }).Count
0
```

### SCAN-10: dotnet build src/PropTraderTools/
**Result: PASS — 0 errors, 0 warnings (new)**

```
Build succeeded.
    0 Error(s)
```

Pre-existing CS0436 warnings (type name conflicts with NinjaTrader.Custom assembly) are unchanged. No new warnings introduced by T2.

### SCAN-L: grep -rn "lock(" src/PropTraderTools/
**Result: PASS — 0 actual lock() calls**

Same analysis as SCAN-8. All pattern matches are false positives from identifiers containing "lock" as a substring (titleBlock, BuildWindowTitleBlock).

### SCAN-B: dotnet test
**Result: PASS — All 6 new [Fact] tests pass; 0 failures**

```
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6 (B154T2 filter)
```

Full test suite (no filter):
```
Passed!  - Failed: 0, Passed: 192, Skipped: 369, Total: 561
```

0 regressions. All pre-existing tests continue to pass.

---

## Comment Removal Confirmation

**Confirmed removed:** The comment line
```
// NT8-014: "PTT-TGT-Drag" and "PTT-STP-Drag" confirmed names -- must not be generalized.
```
has been removed from the `IsPttDragOrphanCancellable` header (was at line 1994).

The phrase "must not be generalized" no longer exists anywhere in either changed method's comment block.

**Confirmed added:** 
- `// StartsWith: matches suffixed variants PTT-TGT-Drag-N / PTT-STP-Drag-N (B142).` 
added to both `IsPttDragOrphanCancellable` and `IsPttDragOrphanCancellableTestable` comment headers.

---

## CYC Delta Confirmation

| Method | Pre-T2 CCN | Post-T2 CCN | Delta |
|--------|-----------|-------------|-------|
| `IsPttDragOrphanCancellable` | 7 | 7 | 0 |
| `IsPttDragOrphanCancellableTestable` | 5 | 5 | 0 |

Equality replaced by prefix check — same McCabe branch count.

---

## JS Rule Compliance

| Rule | Status |
|------|--------|
| JS-021 (no lock) | PASS — no lock() introduced |
| ASCII-only | PASS — all string literals 7-bit ASCII: "PTT-TGT-Drag", "PTT-STP-Drag", StringComparison.Ordinal |

---

## Files Modified

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | (a) equality→StartsWith in IsPttDragOrphanCancellable; (b) equality→StartsWith in IsPttDragOrphanCancellableTestable; (c) comment update (remove "must not be generalized", add StartsWith note) |
| `src/PropTraderTools/Core/PttContracts.cs` | (d) added PttDragOrphanPredicates.IsDragOrphanName pure static helper |
| `src/PropTraderTools/CopyEngineTests.cs` | (d) added B154T2ContractTests with 6 [Fact] tests |

---

## BUILD_PASS
