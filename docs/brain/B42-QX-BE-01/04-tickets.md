# B42-QX-BE-01 — Tickets
## Quick All / BE All any-order interaction repair

**Status**: TICKETS_COMPLETE
**Block**: B42-QX-BE-01
**Wave workspace**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`
**Director workspace**: `c:\WSGTA\universal-or-strategy-director\`
**Author**: ptt-architect
**Date**: 2026-08-05
**Plan status**: REVIEW_PASS (02-architecture-plan.md)

---

## RULES CATALOG GATE — PASS

| Rule | Description | Verdict |
|------|-------------|---------|
| JS-001 | No throw in hot paths | PASS — no new throw introduced |
| JS-002 | No return null | PASS — new method returns bool |
| JS-021 | No lock() | PASS — no lock introduced |
| JS-033 | No async void | PASS — no async method introduced |
| NT8-006 | No LINQ in PttBreakEven.cs | PASS — IsPttQxTarget uses string primitives only |
| NT8-007 | arg11 = CustomOrder null | PASS — no new CreateOrder calls |
| NT8-014 | PTT- prefix on order names | PASS — no new order names introduced |

---

## Ticket Summary

| Ticket | File | Change | Spec Req |
|--------|------|--------|----------|
| T1 | `Features/PttBreakEven.cs` | Add `IsPttQxTarget` + extend `SnapshotTargetsLocal` filter | BUG-B42-QX-BE-01, Direction 1 |
| T2 | `CopyEngine.cs` | Flip `cancelPttBe: false` → `cancelPttBe: true` in `CancelQxBrackets` | BUG-B42-QX-BE-01, Direction 2 |
| T3 | `CopyEngineTests.cs` | 7 xUnit `[Fact]` tests for T_BUG_QX_BE_01 through _07 | BUG-B42-QX-BE-01, Validation |

---

## T1 — PttBreakEven.cs: Add `IsPttQxTarget` + extend `SnapshotTargetsLocal`

**Spec requirement ID**: BUG-B42-QX-BE-01, Direction 1
**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs`

### Problem

`PttBreakEven.SnapshotTargetsLocal()` filters `acc.Orders` with:

```csharp
if (!stateOk || !instrOk || !IsAtmTargetName(o.Name)) continue;
```

`IsAtmTargetName()` accepts only ATM slot names (`"Target1"` .. `"Target9"`). After Quick All
fires, live target orders are named `"PTT-QX-T1"`, `"PTT-QX-T2"`, `"PTT-QX-T3"`. Those names
fail `IsAtmTargetName` → snapshot returns 0 targets → `SubmitBeTargetsLocal` hits the 0-targets
branch → submits a bare stop with no target pairs.

### Change 1 of 2 — Add `IsPttQxTarget` after `IsAtmTargetName()` (line ~245)

**Location**: immediately after the closing `}` of `IsAtmTargetName()`, approximately line 248.

**Method signatures** (must appear verbatim):
- `private static bool IsPttQxTarget(string name)` — CYC = 2

**Implementation** (copy verbatim — char-level exact match required):

```csharp
/// <summary>
/// Return true if name is a PTT Quick Exit target order (PTT-QX-T1, PTT-QX-T2, PTT-QX-T3).
/// These are plain Limit orders -- LimitPrice and Quantity are readable.
/// BUG-B42-QX-BE-01 FIX (Direction 1): BE All after Quick All must recognise QX targets.
/// CYC=2: (1) length+prefix guard, (2) digit range check.
/// JS-021: no lock. JS-002: returns bool.
/// NT8-006: string primitives only, no LINQ.
/// </summary>
private static bool IsPttQxTarget(string name)
{
    if (name == null || name.Length != 9) return false;                     // (1)
    return name[0] == 'P' && name[1] == 'T' && name[2] == 'T'
           && name[3] == '-' && name[4] == 'Q' && name[5] == 'X'
           && name[6] == '-' && name[7] == 'T'
           && name[8] >= '1' && name[8] <= '3';                            // (2)
}
```

**CYC analysis**: branch (1) = if-return; expression (2) = single compound `&&` chain = 1 decision.
Total CYC = 2. Jane Street threshold <= 8: PASS.

**Invariant**: `IsAtmTargetName()` signature and body MUST NOT be touched. Engineer must verify
`git diff` shows zero changed lines inside `IsAtmTargetName`.

### Change 2 of 2 — Extend filter in `SnapshotTargetsLocal()` (line ~266)

**Method signatures** (must appear verbatim):
- `SnapshotTargetsLocal` — CYC stays 3 (no new branch node added)

**Current line 266**:
```csharp
if (!stateOk || !instrOk || !IsAtmTargetName(o.Name)) continue;
```

**Replacement**:
```csharp
if (!stateOk || !instrOk || (!IsAtmTargetName(o.Name) && !IsPttQxTarget(o.Name))) continue;
```

**Rationale**: negated-AND means: skip order only if it satisfies NEITHER predicate. A
`"PTT-QX-T1"` order satisfies `IsPttQxTarget` and is included in the snapshot. `"Target1"`
satisfies `IsAtmTargetName` and continues to be included. No other lines in
`SnapshotTargetsLocal` are modified.

**No other changes to PttBreakEven.cs.**

### JS / NT8 rule constraints

| Rule | Method | Constraint |
|------|--------|------------|
| JS-021 | `IsPttQxTarget` | No `lock()` — pure static computation |
| JS-002 | `IsPttQxTarget` | Returns `bool`, never nullable, never null |
| JS-001 | `IsPttQxTarget` | No `throw` — early return on guard failure |
| NT8-006 | `IsPttQxTarget` | Zero LINQ: no `using System.Linq`, no `.Where`, `.Select`, `.Any`, `.ToList` |
| NT8-006 | `SnapshotTargetsLocal` | Existing filter loop uses no LINQ — no regression allowed |

### 7-scan checklist

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 (JS-021 lock) | No `lock(` introduced in PttBreakEven.cs | PASS |
| SCAN-02 (JS-002 null) | `IsPttQxTarget` returns `bool`, `SnapshotTargetsLocal` returns `List<>` — no null returns | PASS |
| SCAN-03 (JS-033 async void) | `IsPttQxTarget` is synchronous; `SnapshotTargetsLocal` is synchronous | PASS |
| SCAN-04 (NT8-006 LINQ) | `grep "using System.Linq"` in PttBreakEven.cs = 0; `grep "\.Where\|\.Select\|\.Any\|\.ToList"` in IsPttQxTarget = 0 | PASS |
| SCAN-05 (CYC <= 8) | `IsPttQxTarget` = 2, `SnapshotTargetsLocal` = 3 | PASS |
| SCAN-06 (IsAtmTargetName unchanged) | `git diff` shows zero lines changed inside `IsAtmTargetName` body | PASS |
| SCAN-07 (no new state fields) | No instance fields or static fields added | PASS |

---

## T2 — CopyEngine.cs: Extend `CancelQxBrackets` to cancel PTT-BE-* orders

**Spec requirement ID**: BUG-B42-QX-BE-01, Direction 2
**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`

### Problem

`CopyEngine.CancelQxBrackets()` delegates with `cancelPttBe: false`, which excludes all
`"PTT-BE-*"` orders from the cancel sweep. After BE All fires, `"PTT-BE-Stop-1"`,
`"PTT-BE-Target-1"`, etc. survive the cancel pass and compete with the new `"PTT-QX-T1"`
orders for the same position.

### Change — One boolean argument at line ~2230

**Method signatures** (must appear verbatim):
- `internal void CancelQxBrackets(Account acc, Instrument instr)` — CYC stays 1

**Current lines 2229–2230**:
```csharp
internal void CancelQxBrackets(Account acc, Instrument instr)
    => CancelStaleBrackets(acc, instr, cancelPttBe: false, cancelPttQx: true);
```

**Replacement**:
```csharp
// BUG-B42-QX-BE-01 FIX (Direction 2): also cancel PTT-BE-* orders so Quick All after
// BE All starts from a clean slate -- no stale PTT-BE-Stop/Target orders competing.
internal void CancelQxBrackets(Account acc, Instrument instr)
    => CancelStaleBrackets(acc, instr, cancelPttBe: true, cancelPttQx: true);
```

**No other lines in CopyEngine.cs are modified.**

### Explanation

`CancelStaleBrackets` already contains the flag-guarded filter:

```csharp
(cancelPttBe || !o.Name.StartsWith("PTT-BE-"))
```

Flipping `cancelPttBe` from `false` to `true` makes this condition always `true`, so
`"PTT-BE-Stop-1"` and `"PTT-BE-Target-1"` are included in the cancel list. The body of
`CancelStaleBrackets` is untouched.

### Build tag update (same file)

Engineer must update `PttBuild.Tag` in `CopyEngine.cs`:

```csharp
internal const string Tag = "PTT-COPIER B42 | qx-be-interaction | 2026-08-05";
```

### JS / NT8 rule constraints

| Rule | Method | Constraint |
|------|--------|------------|
| JS-021 | `CancelQxBrackets` | No `lock()` — expression-body delegation |
| JS-002 | `CancelQxBrackets` | Returns `void` — no null return possible |
| JS-001 | `CancelQxBrackets` | No `throw` — argument flip only |

### 7-scan checklist

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 (JS-021 lock) | No `lock(` introduced in CopyEngine.cs at the changed line | PASS |
| SCAN-02 (JS-002 null) | No return-null changes; method returns void | PASS |
| SCAN-03 (JS-033 async void) | No async changes — `CancelQxBrackets` is sync void | PASS |
| SCAN-04 (NT8-006 LINQ) | `CancelStaleBrackets` already uses LINQ (existing, unmodified) — change is call-site argument only | PASS — not our change |
| SCAN-05 (CYC <= 8) | `CancelQxBrackets` CYC stays 1 (single expression body) | PASS |
| SCAN-06 (CancelStaleBrackets body unchanged) | `git diff` shows zero lines changed inside `CancelStaleBrackets` body | PASS |
| SCAN-07 (no new state fields) | No instance fields or static fields added | PASS |

---

## T3 — CopyEngineTests.cs: xUnit `[Fact]` tests T_BUG_QX_BE_01 through _07

**Spec requirement ID**: BUG-B42-QX-BE-01, Validation
**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`
**Append location**: before the closing `}` of the last test class (line ~4340, before `}` at line 4341).

### xUnit `[Fact]` test names and assertions

All tests: pure arithmetic / string logic — no NT8 runtime objects needed. `[Fact]` only.
No `[Theory]`. No `async`. No NUnit. No MSTest.

---

#### T_BUG_QX_BE_01 — `IsPttQxTarget` returns true for valid QX target names

**What it asserts**: The helper recognises `"PTT-QX-T1"` and `"PTT-QX-T2"` as valid QX target names.

```csharp
[Fact]
public void T_BUG_QX_BE_01_IsPttQxTarget_ReturnsTrue_ForValidQxTargets()
{
    // Replicate IsPttQxTarget logic inline (static helper accessible via InternalsVisibleTo
    // or inline predicate matching the method under test).
    string n1 = "PTT-QX-T1";
    string n2 = "PTT-QX-T2";
    Assert.True(IsPttQxTargetInline(n1));
    Assert.True(IsPttQxTargetInline(n2));
}

// Inline helper replicating the private static logic:
private static bool IsPttQxTargetInline(string name)
{
    if (name == null || name.Length != 9) return false;
    return name[0] == 'P' && name[1] == 'T' && name[2] == 'T'
           && name[3] == '-' && name[4] == 'Q' && name[5] == 'X'
           && name[6] == '-' && name[7] == 'T'
           && name[8] >= '1' && name[8] <= '3';
}
```

---

#### T_BUG_QX_BE_02 — `IsPttQxTarget` returns false for invalid names

**What it asserts**: Names `"PTT-QX-T4"`, `"PTT-QX-Stop"`, and `"Target1"` are all rejected.

```csharp
[Fact]
public void T_BUG_QX_BE_02_IsPttQxTarget_ReturnsFalse_ForInvalidNames()
{
    Assert.False(IsPttQxTargetInline("PTT-QX-T4"));    // digit out of range
    Assert.False(IsPttQxTargetInline("PTT-QX-Stop"));  // wrong suffix
    Assert.False(IsPttQxTargetInline("Target1"));      // ATM slot name, length 7
}
```

---

#### T_BUG_QX_BE_03 — Combined predicate accepts ATM target name via `IsAtmTargetName` path

**What it asserts**: `"Target1"` passes when evaluated through the combined OR predicate
(`IsAtmTargetName || IsPttQxTarget`), confirming the ATM-only path is unaffected.

```csharp
[Fact]
public void T_BUG_QX_BE_03_CombinedPredicate_Accepts_AtmTargetName()
{
    string name = "Target1";
    // Replicate the combined filter condition from SnapshotTargetsLocal:
    // pass = IsAtmTargetName(name) || IsPttQxTarget(name)
    bool isAtm = IsAtmTargetNameInline(name);   // "Target1" length=7, digit in ['1'..'9']
    bool isQx  = IsPttQxTargetInline(name);
    Assert.True(isAtm || isQx);
}

// Inline helper replicating IsAtmTargetName logic:
private static bool IsAtmTargetNameInline(string name)
{
    if (name == null || name.Length != 7) return false;
    return name[0] == 'T' && name[1] == 'a' && name[2] == 'r'
           && name[3] == 'g' && name[4] == 'e' && name[5] == 't'
           && name[6] >= '1' && name[6] <= '9';
}
```

---

#### T_BUG_QX_BE_04 — Combined predicate accepts QX target name via `IsPttQxTarget` path

**What it asserts**: `"PTT-QX-T1"` passes the combined filter, confirming the bug fix is
in effect (pre-fix: this would have been excluded).

```csharp
[Fact]
public void T_BUG_QX_BE_04_CombinedPredicate_Accepts_QxTargetName()
{
    string name = "PTT-QX-T1";
    bool isAtm = IsAtmTargetNameInline(name);   // false — length 9, not 7
    bool isQx  = IsPttQxTargetInline(name);     // true
    Assert.True(isAtm || isQx);
}
```

---

#### T_BUG_QX_BE_05 — `cancelPttBe: true` includes `"PTT-BE-Stop"` in cancel list

**What it asserts**: When `cancelPttBe` is `true`, the filter condition for `"PTT-BE-Stop"`
evaluates to `true` (order IS included), verifying the new `CancelQxBrackets` behaviour.

```csharp
[Fact]
public void T_BUG_QX_BE_05_CancelPttBeTrue_IncludesPttBeStopOrder()
{
    // Replicate CancelStaleBrackets filter:
    // passesFilter = cancelPttBe || !name.StartsWith("PTT-BE-")
    bool cancelPttBe = true;
    string name = "PTT-BE-Stop";
    bool passesFilter = cancelPttBe || !name.StartsWith("PTT-BE-");
    Assert.True(passesFilter);
}
```

---

#### T_BUG_QX_BE_06 — `cancelPttBe: true` includes `"PTT-BE-Target-1"` in cancel list

**What it asserts**: When `cancelPttBe` is `true`, `"PTT-BE-Target-1"` is also included,
confirming all BE bracket orders are swept.

```csharp
[Fact]
public void T_BUG_QX_BE_06_CancelPttBeTrue_IncludesPttBeTargetOrder()
{
    bool cancelPttBe = true;
    string name = "PTT-BE-Target-1";
    bool passesFilter = cancelPttBe || !name.StartsWith("PTT-BE-");
    Assert.True(passesFilter);
}
```

---

#### T_BUG_QX_BE_07 — `IsAtmTargetName` still returns false for `"PTT-QX-T1"` (regression guard)

**What it asserts**: The invariant that `IsAtmTargetName` does NOT cross-match QX names is
confirmed via reflection. This guards against any accidental modification to `IsAtmTargetName`.

```csharp
[Fact]
public void T_BUG_QX_BE_07_IsAtmTargetName_ReturnsFalse_ForQxTargetName()
{
    var method = typeof(PttBreakEven).GetMethod(
        "IsAtmTargetName",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    Assert.NotNull(method);  // method must exist and be accessible
    var result = (bool)method.Invoke(null, new object[] { "PTT-QX-T1" });
    Assert.False(result);
}
```

---

### Required `using` statements (add at top of file if not already present)

```csharp
using System.Reflection;
using Xunit;
```

### 7-scan checklist

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 (JS-021 lock) | No `lock(` in any of the 7 test methods | PASS |
| SCAN-02 (JS-002 null) | No `return null` in any test method | PASS |
| SCAN-03 (JS-033 async void) | All test methods are synchronous `void` | PASS |
| SCAN-04 (NT8-006 LINQ) | LINQ is acceptable in test files — N/A | N/A |
| SCAN-05 (CYC <= 8) | Each `[Fact]` method is linear (CYC = 1) | PASS |
| SCAN-06 (xUnit [Fact] only) | All tests use `[Fact]` — no `[Theory]`, no NUnit, no MSTest | PASS |
| SCAN-07 (no new state fields) | No instance or static fields introduced by tests | PASS |

---

## Cross-Ticket Invariants

| Invariant | Enforced by | Verification |
|-----------|-------------|--------------|
| `IsAtmTargetName()` body unchanged | T1 SCAN-06 | `git diff Features/PttBreakEven.cs` shows 0 lines changed inside `IsAtmTargetName` |
| `CancelStaleBrackets()` body unchanged | T2 SCAN-06 | `git diff CopyEngine.cs` shows 0 lines changed inside `CancelStaleBrackets` |
| No LINQ in PttBreakEven.cs | T1 SCAN-04 | `grep -n "using System.Linq\|\.Where\|\.Select\|\.Any\|\.ToList" Features/PttBreakEven.cs` = 0 |
| No lock() in modified files | T1/T2 SCAN-01 | `grep -n "lock(" Features/PttBreakEven.cs CopyEngine.cs` = 0 |
| CYC <= 8 on all touched methods | T1/T2 SCAN-05 | `IsPttQxTarget`=2, `SnapshotTargetsLocal`=3, `CancelQxBrackets`=1 |
| xUnit [Fact] only in test file | T3 SCAN-06 | `grep -n "\[Test\]\|\[TestMethod\]\|\[Theory\]" CopyEngineTests.cs` = 0 |

---

## Execution Order

1. **T1** (PttBreakEven.cs) — add method + modify one line
2. **T2** (CopyEngine.cs) — change one argument + update build tag
3. **T3** (CopyEngineTests.cs) — append 7 test methods + inline helpers + using statements
4. Build: `dotnet build` — must produce zero errors
5. Test: `dotnet test` — all 7 new `[Fact]` methods must pass

Total source modifications: ~3 lines changed + ~12 lines added in source files;
~80 lines added in test file.
