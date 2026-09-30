# B126 Architecture Plan

**Block**: B126  
**Defect**: DW-B58-01 -- SnapshotTargetsPublic hardcoded prefixes  
**Priority**: P2  
**Phase**: 1 (Architecture)  
**Status**: REVIEW_PASS  
**Date**: 2026-08-10  

---

## 1. PROBLEM STATEMENT

### 1.1 Exact Literals Being Constantified

[`CopyEngine.SnapshotTargetsPublic`](src/PropTraderTools/CopyEngine.cs:3492) (lines 3505-3506)
currently contains two hardcoded string literals:

```csharp
// Line 3505
n.StartsWith("PTT-QX-T", StringComparison.Ordinal)
// Line 3506
|| n.StartsWith("PTT-TGT-", StringComparison.Ordinal)
```

These are the ONLY two callers of these literals within `SnapshotTargetsPublic`.

### 1.2 Why This Violates Maintainability (DW-B58-01)

Hardcoded string literals scattered across production code create four concrete risks:

1. **Silent rename drift**: If the order-name prefix convention changes (e.g. QX renaming to QXT),
   a developer must grep across the entire codebase to find every usage. A constant reference is
   found by the compiler at the definition site automatically.
2. **Typo risk**: `"PTT-QX-T"` and `"PTT-TGT-"` differ by one character. A future copy-paste
   produces `"PTT-QX-T-"` (trailing hyphen) with no compiler error — wrong behavior, silent.
3. **Documentation gap**: A named constant `PttQxTargetPrefix` communicates intent in a way
   that `"PTT-QX-T"` does not.
4. **Test assertion coupling**: Tests currently asserting on raw literals break if the prefix
   changes; tests asserting via constant reference stay green until the constant is also changed.

---

## 2. SOLUTION DESIGN

### 2.1 Where Constants Are Added

**File**: [`src/PropTraderTools/Core/PttContracts.cs`](src/PropTraderTools/Core/PttContracts.cs)  
**Insertion point**: After line 320 (after the closing `}` of `FillSignalEventArgs`), still inside
the `PropTraderTools` namespace block, before the outer closing `}`.

A new section is appended at the bottom of the namespace, following the existing separator-comment
style used throughout [`PttContracts.cs`](src/PropTraderTools/Core/PttContracts.cs):

```csharp
// -------------------------------------------------------------------------
// ORDER NAME PREFIXES
// -------------------------------------------------------------------------

/// <summary>
/// Compile-time constants for PTT order name prefixes.
/// Used by SnapshotTargetsPublic and related order-filtering methods.
/// ASCII-only. JS-066: no branching -- CYC impact = 0.
/// </summary>
internal static class PttOrderNames
{
    /// <summary>Quick-exit target order name prefix. "PTT-QX-T"</summary>
    internal const string PttQxTargetPrefix = "PTT-QX-T";

    /// <summary>Target order name prefix. "PTT-TGT-"</summary>
    internal const string PttTgtPrefix = "PTT-TGT-";

    /// <summary>Break-even target order name prefix. "PTT-BE-Target-"
    /// Defined for completeness -- used in PttBreakEven.cs and
    /// PttGlobalQuickExit.cs (constantification of those callers is
    /// deferred to a future block per B126 scope constraint).</summary>
    internal const string PttBeTargetPrefix = "PTT-BE-Target-";
}
```

**Rationale for `internal static class`** (vs inline `const` fields on another class):
- [`PttContracts.cs`](src/PropTraderTools/Core/PttContracts.cs) already uses this pattern for
  `PttBus` (static hub class) and `IPttModule`/`ICopyEngine` (grouped by concern).
- `internal` visibility matches `SnapshotTargetsPublic`'s own `internal` visibility.
- Groups all three constants in one discoverable location for future maintainers.
- Zero runtime cost: `const string` values are baked into IL at compile time (no heap allocation,
  no static field initialization).

### 2.2 Exact Constant Names and Values

| Constant Name | Value | Usage |
|---|---|---|
| `PttOrderNames.PttQxTargetPrefix` | `"PTT-QX-T"` | CopyEngine.cs line 3505 |
| `PttOrderNames.PttTgtPrefix` | `"PTT-TGT-"` | CopyEngine.cs line 3506 |
| `PttOrderNames.PttBeTargetPrefix` | `"PTT-BE-Target-"` | Defined for completeness; tested in B126Tests.cs Test1; not yet used in SnapshotTargetsPublic |

**Discrepancy note**: The spec names the third constant `PttBeTargetPrefix = "PTT-BE-Target-"`.
This literal does NOT appear in `SnapshotTargetsPublic`. It is included because:
(a) the spec explicitly requires it, and (b) it is used as a raw literal in
[`PttBreakEven.cs`](src/PropTraderTools/Features/PttBreakEven.cs) and
[`PttGlobalQuickExit.cs`](src/PropTraderTools/Features/PttGlobalQuickExit.cs) —
those callers are OUT OF SCOPE for B126 and will be updated in a future block.

### 2.3 Lines Changed in CopyEngine.cs

**File**: [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs)  
**Method**: `SnapshotTargetsPublic` (lines 3492-3511)  
**Lines changed**: 3505 and 3506 only.

**Before**:
```csharp
if (
    n.StartsWith("PTT-QX-T", StringComparison.Ordinal) // (3) prefix check
    || n.StartsWith("PTT-TGT-", StringComparison.Ordinal)
)
```

**After**:
```csharp
if (
    n.StartsWith(PttOrderNames.PttQxTargetPrefix, StringComparison.Ordinal) // (3) prefix check
    || n.StartsWith(PttOrderNames.PttTgtPrefix, StringComparison.Ordinal)
)
```

No other lines in `SnapshotTargetsPublic` change. CYC remains 3 (comment-documented on line 3489
is preserved). No structural or behavioral change.

### 2.4 Caller Scope

| Location | Literal | Action in B126 |
|---|---|---|
| `CopyEngine.cs` line 3505 | `"PTT-QX-T"` | REPLACE with `PttOrderNames.PttQxTargetPrefix` |
| `CopyEngine.cs` line 3506 | `"PTT-TGT-"` | REPLACE with `PttOrderNames.PttTgtPrefix` |
| `PttBreakEven.cs` (multiple) | `"PTT-BE-Target-"` | OUT OF SCOPE -- future block |
| `PttGlobalQuickExit.cs` (multiple) | `"PTT-BE-Target-"` | OUT OF SCOPE -- future block |
| All existing test files | any of the above | DO NOT TOUCH -- passing regression tests |

---

## 3. TEST PLAN

**Test file**: [`src/PropTraderTools/Tests/B126Tests.cs`](src/PropTraderTools/Tests/B126Tests.cs)  
**Framework**: xUnit [Fact] -- never NUnit/MSTest (V12.32 mandate).  
**NT8 dependency**: None. All three tests are pure value-assertion tests on `const string` fields.
No NT8 runtime required.

### Test 1: `ConstantsMatch`

```csharp
[Fact]
public void ConstantsMatch()
{
    Assert.Equal("PTT-BE-Target-", PttOrderNames.PttBeTargetPrefix);
    Assert.Equal("PTT-QX-T",       PttOrderNames.PttQxTargetPrefix);
    Assert.Equal("PTT-TGT-",       PttOrderNames.PttTgtPrefix);
}
```

**Asserts**: All three constants have the exact compile-time values required by the spec.
If any constant value drifts in a future refactor, this test immediately fails and surfaces
the change. This is the primary regression guard for DW-B58-01.

### Test 2: `SnapshotTargetsPublic_QxPrefix_HasCorrectValue`

```csharp
[Fact]
public void SnapshotTargetsPublic_QxPrefix_HasCorrectValue()
{
    // Verifies the QX target prefix constant used by SnapshotTargetsPublic
    // is the exact string that matches QX target order names.
    Assert.True("PTT-QX-T1".StartsWith(PttOrderNames.PttQxTargetPrefix, StringComparison.Ordinal));
    Assert.False("PTT-TGT-1".StartsWith(PttOrderNames.PttQxTargetPrefix, StringComparison.Ordinal));
}
```

**Asserts**: `PttQxTargetPrefix` correctly matches a QX target order name and does NOT match a
TGT order name. Verifies the constant value semantics that `SnapshotTargetsPublic` relies on.

### Test 3: `SnapshotTargetsPublic_TgtPrefix_HasCorrectValue`

```csharp
[Fact]
public void SnapshotTargetsPublic_TgtPrefix_HasCorrectValue()
{
    // Verifies the TGT prefix constant used by SnapshotTargetsPublic
    // is the exact string that matches TGT order names.
    Assert.True("PTT-TGT-1".StartsWith(PttOrderNames.PttTgtPrefix, StringComparison.Ordinal));
    Assert.False("PTT-QX-T1".StartsWith(PttOrderNames.PttTgtPrefix, StringComparison.Ordinal));
}
```

**Asserts**: `PttTgtPrefix` correctly matches a TGT order name and does NOT match a QX order name.
Verifies the constant value semantics that `SnapshotTargetsPublic` relies on.

**Pattern note**: Tests 2 and 3 use `string.StartsWith` directly — the same call as in
`SnapshotTargetsPublic`. This mirrors the behavioral intent without requiring NT8 Account/Order
objects that cannot be constructed in a unit test context (NT8 runtime required for those).

---

## 4. 7-SCAN CHECKLIST

Pre-planned. Engineer MUST complete all 7 scans before marking ticket complete.

| # | Scan | Command | Expected |
|---|---|---|---|
| SCAN-01 | CYC unchanged | Review `SnapshotTargetsPublic` branch count in modified file | CYC=3 (unchanged from comment on line 3489) |
| SCAN-02 | lock() ban | `grep -n "lock(" src/PropTraderTools/Core/PttContracts.cs src/PropTraderTools/CopyEngine.cs` | 0 results in new/modified lines |
| SCAN-03 | ASCII-only | Assert all three constant values are ASCII (printable range 0x20-0x7E) | PASS (values: "PTT-QX-T", "PTT-TGT-", "PTT-BE-Target-") |
| SCAN-04 | Build | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 0 warnings |
| SCAN-05 | Tests pass | `dotnet test` (B126Tests.cs — 3 [Fact] methods) | All 3 green |
| SCAN-06 | No raw QX literal in SnapshotTargetsPublic | `grep -n "PTT-QX-T" src/PropTraderTools/CopyEngine.cs` scoped to lines 3492-3511 | 0 raw string literals (only constant reference allowed) |
| SCAN-07 | No raw TGT literal in SnapshotTargetsPublic | `grep -n "PTT-TGT-" src/PropTraderTools/CopyEngine.cs` scoped to lines 3492-3511 | 0 raw string literals (only constant reference allowed) |

---

## 5. FILES CHANGED LIST

| File | Change Type | Description |
|---|---|---|
| [`src/PropTraderTools/Core/PttContracts.cs`](src/PropTraderTools/Core/PttContracts.cs) | MODIFY (add) | Append new `internal static class PttOrderNames` with 3 `internal const string` fields after line 320 |
| [`src/PropTraderTools/CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs) | MODIFY (2 lines) | Replace `"PTT-QX-T"` (line 3505) and `"PTT-TGT-"` (line 3506) with `PttOrderNames.PttQxTargetPrefix` and `PttOrderNames.PttTgtPrefix` |
| [`src/PropTraderTools/Tests/B126Tests.cs`](src/PropTraderTools/Tests/B126Tests.cs) | NEW | 3 xUnit [Fact] tests: `ConstantsMatch`, `SnapshotTargetsPublic_QxPrefix_HasCorrectValue`, `SnapshotTargetsPublic_TgtPrefix_HasCorrectValue` |

**Files explicitly NOT modified**:
- `src/PropTraderTools/Features/PttBreakEven.cs`
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
- All files in `src/PropTraderTools/Tests/` other than the new `B126Tests.cs`
- All files in `src/PropTraderTools/` not listed above

---

## 6. CONSTRAINTS

| Constraint | Rule ID | How Satisfied |
|---|---|---|
| CYC unchanged in `SnapshotTargetsPublic` | JS-066 | String literal replacement preserves identical branch structure; no new `if`/`||`/`&&` added |
| No `lock()` usage | JS-021 | No lock introduced; `PttOrderNames` is a static class with const-only members (zero thread contention possible) |
| ASCII-only identifiers and string values | V12 DNA | All three constant values are strict ASCII; identifier names use only A-Z, a-z, 0-9, `-` |
| No behavior change | DW-B58-01 scope | `const string` replacement is semantically identical — CLR inlines the same bytes at JIT time |
| No existing test modifications | B126 scope | Only new `B126Tests.cs` created; zero changes to B68Tests.cs, B71Tests.cs, B76Tests.cs, B112Tests.cs, B113Tests.cs, CopyEngineTests.cs |
| No `.cs` file written by ptt-architect | SRC CODE BAN | This plan document is the ONLY output of this phase; `.cs` writing is exclusively ptt-engineer's task |
| xUnit only (never NUnit/MSTest) | V12.32 | `B126Tests.cs` uses xUnit `[Fact]` exclusively |
