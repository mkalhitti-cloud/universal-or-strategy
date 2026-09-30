# B126 Tickets

**Block**: B126
**Defect**: DW-B58-01 -- SnapshotTargetsPublic hardcoded prefixes
**Plan status**: REVIEW_PASS (02-plan-review.md)
**Phase**: 3 (Ticket Generation)
**Date**: 2026-08-10

---

## TICKET B126-T1 — Constantify SnapshotTargetsPublic Prefixes

### Spec Requirement IDs
- DW-B58-01: Replace hardcoded prefix literals with named constants

---

### Prerequisite Check

- [ ] `dotnet build src/PropTraderTools/PropTraderTools.csproj` passes with 0 errors before starting
- [ ] B121 FINAL_PASS confirmed (non-ASCII removal clean — no regression)

---

### Method Signatures

No new public methods. New `internal` constants only:

```csharp
internal static class PttOrderNames
{
    internal const string PttQxTargetPrefix = "PTT-QX-T";
    internal const string PttTgtPrefix      = "PTT-TGT-";
    internal const string PttBeTargetPrefix = "PTT-BE-Target-";
}
```

Zero runtime cost: `const string` values are baked into IL at compile time (no heap allocation,
no static field initialization). CYC impact = 0.

---

### File Changes

#### File 1: `src/PropTraderTools/Core/PttContracts.cs`

**Change type**: MODIFY (append new class)
**Exact insertion point**: Between line 319 and line 320.
- Line 319: closing `}` of `FillSignalEventArgs`
- Line 320: closing `}` of the `PropTraderTools` namespace (last line of file)

Insert the following block **before** the final `}` on line 320, keeping it inside the
`PropTraderTools` namespace:

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

After insertion the file ends:

```
    }           // closes FillSignalEventArgs  (was line 319)
                //
                // (new PttOrderNames class here)
                //
}               // closes namespace PropTraderTools (was line 320, now shifted)
```

**Files NOT modified in this change**: None. Only PttContracts.cs is touched in File 1.

---

#### File 2: `src/PropTraderTools/CopyEngine.cs`

**Change type**: MODIFY (2 lines only)
**Method**: `SnapshotTargetsPublic` (lines 3492-3511, CYC=3)
**Lines changed**: 3505 and 3506 ONLY.

**Before** (live source, verified):
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

No other lines in `CopyEngine.cs` change. CYC of `SnapshotTargetsPublic` stays at 3.
The comment on line 3488 (`// B58 -- SnapshotTargetsPublic: collects Working orders with PTT-QX-T or PTT-TGT- prefix.`)
may optionally be updated to reference the constant names, but this is NOT required.

**Files explicitly NOT modified in this block**:
- `src/PropTraderTools/Features/PttBreakEven.cs` (deferred)
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` (deferred)
- All existing test files (`B68Tests.cs`, `B71Tests.cs`, `B76Tests.cs`, `CopyEngineTests.cs`, etc.)

---

#### File 3: `src/PropTraderTools/Tests/B126Tests.cs` (NEW FILE)

**Change type**: CREATE
**Framework**: xUnit `[Fact]` — never NUnit or MSTest (V12.32 mandate)
**NT8 dependency**: None. All three tests are pure `const string` value assertions.

```csharp
using Xunit;
using PropTraderTools;

namespace PropTraderTools.Tests
{
    public class B126Tests
    {
        // B126-T1: Verify all three constants have the exact compile-time values
        //          required by DW-B58-01. Primary regression guard -- if any
        //          constant value drifts in a future refactor this test fails.
        [Fact]
        public void B126_T1_Constants_PttBeTargetPrefix_EqualsExpected()
        {
            Assert.Equal("PTT-BE-Target-", PttOrderNames.PttBeTargetPrefix);
            Assert.Equal("PTT-QX-T",       PttOrderNames.PttQxTargetPrefix);
            Assert.Equal("PTT-TGT-",       PttOrderNames.PttTgtPrefix);
        }

        // B126-T2: PttQxTargetPrefix correctly matches a QX target order name
        //          and does NOT match a TGT order name.
        //          Verifies the constant's semantic correctness in the predicate
        //          used by SnapshotTargetsPublic (string.StartsWith — same call).
        [Fact]
        public void B126_T2_PttQxTargetPrefix_MatchesPttQxOrder()
        {
            Assert.True(
                "PTT-QX-T1".StartsWith(PttOrderNames.PttQxTargetPrefix, StringComparison.Ordinal)
            );
            Assert.False(
                "PTT-TGT-1".StartsWith(PttOrderNames.PttQxTargetPrefix, StringComparison.Ordinal)
            );
        }

        // B126-T3: PttTgtPrefix correctly matches a TGT order name and does NOT
        //          match a QX order name.
        [Fact]
        public void B126_T3_PttQxTargetPrefix_DoesNotMatchNativeTarget()
        {
            Assert.True(
                "PTT-TGT-1".StartsWith(PttOrderNames.PttTgtPrefix, StringComparison.Ordinal)
            );
            Assert.False(
                "PTT-QX-T1".StartsWith(PttOrderNames.PttTgtPrefix, StringComparison.Ordinal)
            );
        }
    }
}
```

---

### xUnit [Fact] Test Names and Assertions

| Test Method | Asserts |
|---|---|
| `B126_T1_Constants_PttBeTargetPrefix_EqualsExpected` | All 3 constants equal their exact spec-required string values |
| `B126_T2_PttQxTargetPrefix_MatchesPttQxOrder` | `"PTT-QX-T1".StartsWith(PttQxTargetPrefix)` = true; `"PTT-TGT-1".StartsWith(PttQxTargetPrefix)` = false |
| `B126_T3_PttQxTargetPrefix_DoesNotMatchNativeTarget` | `"PTT-TGT-1".StartsWith(PttTgtPrefix)` = true; `"PTT-QX-T1".StartsWith(PttTgtPrefix)` = false |

---

### JS Rule Constraints

| Rule ID | Constraint | How Satisfied |
|---|---|---|
| JS-066 | CYC unchanged in `SnapshotTargetsPublic` | Literal-to-constant substitution: zero new branches, CYC remains 3 |
| JS-021 | No `lock()` | `PttOrderNames` is `static class` with `const`-only members; no state, no thread contention possible |
| JS-002 | No `null` returns | No change to `SnapshotTargetsPublic` return logic; already compliant |
| ASCII-only | All constant string values and identifiers are ASCII | `"PTT-QX-T"`, `"PTT-TGT-"`, `"PTT-BE-Target-"` are strict ASCII (0x20-0x7E); identifiers use A-Z, a-z, 0-9, hyphen only |
| V12.32 | xUnit only | `B126Tests.cs` uses xUnit `[Fact]` exclusively — never NUnit or MSTest |

---

### 7-SCAN CHECKLIST

**Engineer must run all 7 scans and cite results verbatim in `ticket-1-completion.md`.**

#### SCAN-01 — CYC unchanged

```powershell
python scripts/complexity_audit.py | Select-String "SnapshotTargetsPublic"
```

**Expected**: CYC=3, OR no output (method is below threshold 8 — both are acceptable).
**Fail condition**: CYC > 3 (any new branch added).

---

#### SCAN-02 — lock() zero results in modified files

```powershell
grep -n "lock(" src/PropTraderTools/Core/PttContracts.cs src/PropTraderTools/CopyEngine.cs
```

**Expected**: 0 results in newly modified regions (PttOrderNames class and SnapshotTargetsPublic body).

---

#### SCAN-03 — ASCII-only in new class

```powershell
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Core/PttContracts.cs
```

**Expected**: 0 results. All constant string values and identifiers in PttOrderNames are ASCII.

---

#### SCAN-04 — dotnet build

```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj --no-incremental
```

**Expected**: `Build succeeded.` with `0 Error(s)`.

---

#### SCAN-05 — xUnit tests

```powershell
dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "B126" --no-build
```

**Expected**: 3 tests pass, 0 fail.

---

#### SCAN-06 — Raw `"PTT-QX-T"` literal gone from SnapshotTargetsPublic

```powershell
grep -n '"PTT-QX-T"' src/PropTraderTools/CopyEngine.cs
```

**Expected**: 0 results at lines 3504-3508 (the `SnapshotTargetsPublic` predicate body).
A match anywhere in that range = FAIL.

---

#### SCAN-07 — Raw `"PTT-TGT-"` literal gone from SnapshotTargetsPublic

```powershell
grep -n '"PTT-TGT-"' src/PropTraderTools/CopyEngine.cs
```

**Expected**: 0 results anywhere in `CopyEngine.cs` that are not inside a comment.
A match in the `SnapshotTargetsPublic` body = FAIL.

---

### Completion Artifact

Engineer writes: `docs/brain/B126/ticket-1-completion.md`

**Must include**:
- BUILD_PASS or BUILD_FAIL
- All 7 scan results cited verbatim (command + output)
- git diff summary (3 files changed, insertion counts)

---

## Summary

| Item | Value |
|---|---|
| Tickets | 1 |
| Files changed | 3 (PttContracts.cs modify, CopyEngine.cs modify 2 lines, B126Tests.cs new) |
| Files NOT modified | PttBreakEven.cs, PttGlobalQuickExit.cs, all existing test files |
| New public API | None |
| New internal constants | 3 (`PttQxTargetPrefix`, `PttTgtPrefix`, `PttBeTargetPrefix`) |
| CYC delta | 0 |
| Behavior delta | None (const string = identical IL bytes) |
| xUnit tests | 3 [Fact] methods, 0 NT8 runtime dependency |
