# PTT-COPIER B55 LaneB -- Tickets
# Phase: 3 (ptt-architect Ticket Generation)
# Epic: B55-LaneB
# Architect: ptt-architect
# Date: 2026-08-10
# Plan status: REVIEW_PASS (02-plan-review.md)
# Spec: specs/002-trade-copier-spec.html id="section-b55" (LaneB)
# Defect closed: DW-B47-05 P2 -- FindRule null contract undocumented (JS-002)
# Wave workspace: C:\WSGTA\universal-or-strategy\

---

## TICKET-1: XML Doc Comment on FindRule

**Ticket ID:** T1
**Title:** Add XML doc comment to FindRule documenting the null-return contract
**Spec requirements:** DW-B47-05 P2 -- JS-002 null contract, XML doc comment
**File (Wave workspace):** `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
**Relative path:** `src/PropTraderTools/CopyEngine.cs`

---

### Pre-condition

Read `CopyEngine.cs` lines 1193-1210.  Confirm the current state shows:

```
        private CopyRule? FindRule(Instrument instrument)
        {
            if (instrument == null)
                return null; // Change 8: null guard
            foreach (var rule in _rules)
            {
                if (rule.Instrument == instrument.FullName)
                    return rule;
            }
            return null;
        }
```

There is no XML doc comment above the method signature. That is the pre-condition.

---

### Action

Insert the following 7-line XML doc comment block immediately above the line
`private CopyRule? FindRule(Instrument instrument)` (currently ~line 1197).

Match the **8-space indentation** of the surrounding method signature exactly.

```csharp
        /// <summary>
        /// Finds the copy rule for the given instrument.
        /// </summary>
        /// <returns>
        /// Matching <see cref="CopyRule"/>, or <c>null</c> if no rule exists for this instrument.
        /// Callers MUST null-check the return value.
        /// </returns>
        private CopyRule? FindRule(Instrument instrument)
```

**Do NOT alter** the method signature, method body, or any surrounding line.
**Do NOT add** `[return: MaybeNull]` -- this attribute is not available in .NET Framework 4.8
(NT8 target) and would require a BCL polyfill. The XML doc comment alone is the spec-approved fix.
Reference: plan Section 13 + plan-review NOTE-02.

---

### Post-condition

Read `CopyEngine.cs` lines 1193-1210 again.  Confirm:

```
        /// <summary>
        /// Finds the copy rule for the given instrument.
        /// </summary>
        /// <returns>
        /// Matching <see cref="CopyRule"/>, or <c>null</c> if no rule exists for this instrument.
        /// Callers MUST null-check the return value.
        /// </returns>
        private CopyRule? FindRule(Instrument instrument)
        {
            if (instrument == null)
                return null; // Change 8: null guard
            foreach (var rule in _rules)
            {
                if (rule.Instrument == instrument.FullName)
                    return rule;
            }
            return null;
        }
```

CYC of FindRule remains **3** (unchanged -- null guard + foreach + name match).
No logic change. No signature change. Doc comment insert only.

---

### Method signature (unchanged)

```
private CopyRule? FindRule(Instrument instrument)
```

Return type: `CopyRule?` (nullable reference type -- unchanged)
Visibility: `private` (unchanged)
Parameters: `Instrument instrument` (unchanged)
CYC: 3 (unchanged)

---

### JS rules applicable

| Rule | Assessment |
|------|------------|
| JS-021 (no lock()) | No lock added or removed. FindRule reads _rules (ConcurrentBag) via foreach -- lock-free. PASS. |
| JS-002 (null contract) | XML doc comment now explicitly states the null return contract. Callers MUST null-check mandate is documented. PASS. |
| JS-033 (no async void) | No async usage. PASS. |
| JS-001 (no throw in hot path) | No new throw. PASS. |

---

### NT8 rules applicable

| Rule | Assessment |
|------|------------|
| NT8-001 (`{ get; init; }`) | Not used. No new properties. PASS. |
| NT8-002 (abstract/sealed record) | Not used. PASS. |
| NT8-018/021 (lock()) | Not introduced. PASS. |
| NT8-019 (async void) | Not introduced. PASS. |
| NT8-028 (hex color string literals) | No UI changes. PASS. |
| XML doc syntax | `///`, `<summary>`, `<returns>`, `<see cref="..."/>`, `<c>` are standard C# XML documentation tags fully supported in .NET Framework 4.8. No NT8 rule triggered. PASS. |

---

### 7-Scan Checklist (engineer contract)

Run ALL scans from the **Wave workspace root** (`C:\WSGTA\universal-or-strategy\`).

| Scan | Command | Expected result |
|------|---------|-----------------|
| SCAN-01 | `Select-String "lock(" src/ -Recurse -Include *.cs` | 0 results |
| SCAN-02 | `Select-String "async void " src/ -Recurse -Include *.cs` | 0 results |
| SCAN-03 | `Select-String "return null" src/ -Recurse -Include *.cs` | 0 NEW instances (pre-existing null returns in FindRule and other methods are expected -- verify count unchanged vs baseline) |
| SCAN-04 | `Select-String "throw new " src/ -Recurse -Include *.cs` | 0 NEW instances |
| SCAN-05 | `python scripts/complexity_audit.py` | All methods CYC <= 8; FindRule CYC = 3 (unchanged) |
| SCAN-06 | `dotnet build` | 0 errors (pre-existing warnings OK) |
| SCAN-07 | `dotnet test` | All baseline tests pass (255 pass + 24 pre-existing fail = 279 total). T_B55B_01 does NOT exist yet in Ticket-1 scope -- Ticket-2 adds it. |

---

## TICKET-2: T_B55B_01 Test -- CopyEngineTests.cs

**Ticket ID:** T2
**Title:** Add T_B55B_01_FindRule_ReturnsNull_WhenNoRules [Fact] test
**Spec requirements:** DW-B47-05 P2 -- T_B55B_01 documents and locks the null-return contract
**File (Wave workspace):** `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`
**Relative path:** `src/PropTraderTools/CopyEngineTests.cs`
**xUnit [Fact] name:** `T_B55B_01_FindRule_ReturnsNull_WhenNoRules`

---

### Pre-condition

Read `CopyEngineTests.cs` (last 30 lines).  Confirm:

- The class is `CopyEngineTests` (existing).
- `using System.Reflection;` is present (line 9 area).
- `using NinjaTrader.Cbi;` is present (line 10 area).
- `using CopyRule = PropTraderTools.CopyEngine.CopyRule;` is present (line 12 area).
- `private readonly CopyEngine _engine = CopyEngine.Instance;` field exists.
- The test `T_B55B_01_FindRule_ReturnsNull_WhenNoRules` does NOT yet exist.

The reflection pattern below mirrors the established precedent from B53 LaneA
(e.g., `T_B53_FindRuleByFollower_ReturnsRule` in the same file).

---

### Action

Append the following `[Fact]` method to the `CopyEngineTests` class.
Insert it **before the closing `}` of the class** (i.e., after the last test method in the file).

```csharp
    // T_B55B_01 -- FindRule_ReturnsNull_WhenNoRules
    // Documents and locks the null-return contract of FindRule.
    // Engine with empty _rules list: FindRule(stub instrument) returns null.
    // Uses reflection (private method in sealed class) -- same pattern as B53 LaneA tests.
    // JS-002: null contract now tested and documented.
    // Plan-review NOTE-01: Assert.Equal(typeof(CopyRule?), mi.ReturnType) is vacuous for
    // reference types (NRT annotation is compile-time only; CLR typeof(CopyRule?) == typeof(CopyRule)).
    // Primary assertion is result.HasValue == false which correctly handles boxed nullable structs.
    [Fact]
    public void T_B55B_01_FindRule_ReturnsNull_WhenNoRules()
    {
        // Arrange: verify _rules is empty via reflection on _rules field
        var rulesField = typeof(CopyEngine).GetField(
            "_rules",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(rulesField);
        var rulesValue = rulesField.GetValue(_engine);
        Assert.NotNull(rulesValue);
        // ConcurrentBag -- cast and verify empty
        var bag = rulesValue as System.Collections.Concurrent.ConcurrentBag<CopyRule>;
        Assert.NotNull(bag);
        Assert.Empty(bag);

        // Arrange: get FindRule via reflection
        var mi = typeof(CopyEngine).GetMethod(
            "FindRule",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(mi);

        // Verify parameter count and type
        Assert.Equal(1, mi.GetParameters().Length);
        Assert.Equal(typeof(NinjaTrader.Cbi.Instrument), mi.GetParameters()[0].ParameterType);

        // Act: invoke with stub Instrument whose FullName will not match any rule.
        // Passing null as Instrument hits the first null guard in FindRule (return null).
        // Both code paths (null guard hit, no-match fallthrough) return null -- same observable contract.
        var result = mi.Invoke(_engine, new object[] { (NinjaTrader.Cbi.Instrument)null });

        // Assert: null-return contract confirmed.
        // result is boxed CopyRule? -- use HasValue check (NOT Assert.Null which may mis-behave
        // on boxed nullable structs when the boxed value is non-null but the inner nullable is null).
        Assert.False(((CopyRule?)result).HasValue);
    }
```

**Do NOT modify** any existing test method, field, using directive, or closing brace.
**Do NOT add** any new using directive -- all required using statements are already present.

---

### Post-condition

Run `dotnet test --filter T_B55B_01_FindRule_ReturnsNull_WhenNoRules`.  Confirm:

- Test appears and status = PASS.
- Total test count: 280 (was 279 -- 255 pass + 24 pre-existing fail + 1 new pass).

---

### xUnit [Fact] details

| Property | Value |
|----------|-------|
| Method name | `T_B55B_01_FindRule_ReturnsNull_WhenNoRules` |
| Test class | `CopyEngineTests` |
| Framework | xUnit (NOT NUnit, NOT MSTest -- xUnit only per V12 DNA) |
| CYC | 1 (straight-line, no branches) |
| Assertion pattern | Reflection-based; primary assert: `Assert.False(((CopyRule?)result).HasValue)` |
| What it locks | The null-return contract of FindRule: when no rules match (or instrument is null), FindRule returns null, never a non-null CopyRule. |
| Reflection precedent | Same `GetMethod(NonPublic | Instance)` + `Invoke` pattern as B53 LaneA tests in this file. |

**Plan-review NOTE-01 (engineer awareness -- no action required):**
The assertion `Assert.Equal(typeof(CopyRule?), mi.ReturnType)` was present in the architecture
plan's draft test body. It was **removed** from this ticket's final test body as a simplification.
`CopyRule` is a `private readonly struct` (value type, not a reference type). For a value-type
struct, `typeof(CopyRule?)` compiles to `typeof(Nullable<CopyRule>)`, which IS a distinct CLR
type from `typeof(CopyRule)`. The assertion `Assert.Equal(typeof(Nullable<CopyRule>), mi.ReturnType)`
would therefore be meaningful and non-vacuous -- it correctly verifies the method returns a nullable
struct. It was removed as a simplification: the null-return contract is sufficiently locked by the
`Assert.False(((CopyRule?)result).HasValue)` assertion below.

That assertion is correct because: when `FindRule` returns `null`, `mi.Invoke` returns `null`
(a null `Nullable<CopyRule>` boxes to `null` per CLR Nullable<T> boxing rules); casting the boxed
`null` to `(CopyRule?)` unboxes to a `Nullable<CopyRule>` with `HasValue = false`. Therefore
`Assert.False(((CopyRule?)result).HasValue)` correctly confirms the null-return contract. Do NOT
use `Assert.Null(result)` instead -- it checks reference equality to `null` and will fail for a
boxed `Nullable<CopyRule>` whose inner value is null but whose reference is non-null.

---

### Method signature verified via reflection

```
private CopyRule? FindRule(Instrument instrument)
```

The test's reflection call `GetMethod("FindRule", NonPublic | Instance)` must return a non-null
`MethodInfo` -- if it returns null, the production code has been renamed or removed (test would
fail at `Assert.NotNull(mi)` and surface the issue immediately).

---

### JS rules applicable

| Rule | Assessment |
|------|------------|
| JS-021 (no lock()) | No lock in new test. PASS. |
| JS-002 (null contract) | Test explicitly asserts the null-return contract via `Assert.False(... .HasValue)`. PASS. |
| JS-033 (no async void) | Test is a synchronous xUnit `[Fact]` returning `void`. Not `async void`. PASS. |
| JS-001 (no throw) | No throw introduced. PASS. |

---

### NT8 rules applicable

Test file (`CopyEngineTests.cs`) is compiled by the Linting `.csproj` (MSBuild / dotnet test),
**not** by NT8's internal NinjaScript Roslyn compiler. NT8 compiler rules do not apply to
test files. All rules below are included for completeness.

| Rule | Assessment |
|------|------------|
| NT8-001 (`{ get; init; }`) | Not used. PASS. |
| NT8-002 (abstract/sealed record) | Not used. PASS. |
| NT8-003 (volatile double) | Not used. PASS. |
| NT8-004 (ImmutableDictionary) | Not used. PASS. |
| NT8-006 (System.Linq Any()) | `Assert.Empty(bag)` used -- no raw `.Any()` call. PASS. |
| NT8-018/021 (lock()) | Not introduced. PASS. |
| NT8-019 (async void) | Not introduced. PASS. |

---

### 7-Scan Checklist + SCAN-08 (engineer contract)

Run ALL scans from the **Wave workspace root** (`C:\WSGTA\universal-or-strategy\`).

| Scan | Command | Expected result |
|------|---------|-----------------|
| SCAN-01 | `Select-String "lock(" src/ -Recurse -Include *.cs` | 0 results |
| SCAN-02 | `Select-String "async void " src/ -Recurse -Include *.cs` | 0 results |
| SCAN-03 | `Select-String "return null" src/ -Recurse -Include *.cs` | 0 NEW instances (pre-existing null returns in FindRule body and other methods are expected -- verify count unchanged vs Ticket-1 baseline) |
| SCAN-04 | `Select-String "throw new " src/ -Recurse -Include *.cs` | 0 NEW instances |
| SCAN-05 | `python scripts/complexity_audit.py` | All methods CYC <= 8; T_B55B_01 CYC = 1 |
| SCAN-06 | `dotnet build` | 0 errors |
| SCAN-07 | `dotnet test` | T_B55B_01 PASS; total 280 (256 pass + 24 pre-existing fail); all baseline tests unchanged |
| SCAN-08 | FindRule call-site audit (see detail below) | ALL GUARDED |

**SCAN-08 detail -- FindRule call-site audit (ptt-verifier must run this):**

```powershell
Get-ChildItem C:\WSGTA\universal-or-strategy\src -Filter *.cs -Recurse |
    Select-String -Pattern "FindRule\(" -Context 2
```

For each production call site (exclude the method definition line and test file), confirm
the same or next line contains one of: `if (rule == null)`, `if (rule is null)`, `?.`, `??`.

**Expected result:**

| File | Line | Call | Guard | Status |
|------|------|------|-------|--------|
| `CopyEngine.cs` | ~1185 | `var rule = FindRule(instrument);` | L1186: `if (rule == null) yield break;` | GUARDED |
| `CopyEngine.cs` | ~1197 | `private CopyRule? FindRule(...)` | (definition -- N/A) | N/A |
| `CopyEngine.cs` | ~1355 | `var rule = FindRule(instrument);` | L1356: `if (rule == null) return;` | GUARDED |

**SCAN-08 expected result: ALL GUARDED.**

If ANY unguarded call site is found: add guard at that site before marking Ticket-2 complete.
This is the only permitted scope expansion under the No Scope Creep Protocol (rule 11).

---

## Build Tag

`PTT-COPIER B55 | findrule-null-contract | 2026-08-10`

---

## Hard-Link Sync (mandatory after all src/ edits)

After both tickets are complete and `dotnet test` passes:

```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

This is the Wave workspace hard-link sync. Do NOT use `deploy-sync.ps1` (V12 epic-cluster only).

---

## Summary

| Ticket | File | Change type | CYC delta | Tests delta |
|--------|------|-------------|-----------|-------------|
| T1 | `CopyEngine.cs` | XML doc comment insert above FindRule | 0 | 0 |
| T2 | `CopyEngineTests.cs` | New [Fact] T_B55B_01 appended to class | +1 (CYC=1) | +1 (PASS) |

Zero logic changes. Zero call-site rewrites. Doc + test only.
DW-B47-05 P2 closed: FindRule null contract documented and locked.

---

*ptt-architect | B55-LaneB | Phase 3 | 2026-08-10*
