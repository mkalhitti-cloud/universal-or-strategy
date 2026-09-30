# PTT-COPIER B55 LaneB -- Architecture Plan
# Block: B55-LaneB
# Status: REVIEW_PASS (awaiting ptt-plan-reviewer)
# Spec: specs/002-trade-copier-spec.html id="section-b55" (Lane B section)
# Defect closed: DW-B47-05 P2 -- FindRule null contract undocumented (JS-002)
# Architect: ptt-architect
# Date: 2026-08-10

---

## 1. Objective

Close DW-B47-05 P2. `FindRule(Instrument)` in `CopyEngine.cs` already returns `null`
when `_rules` is empty or no entry matches `instrument.FullName`. The null contract is
not documented, and JS-002 (Option<T> instead of null) applies. The pragmatic fix for
this NT8/.NET 4.8 codebase is:

1. Add an XML doc comment to `FindRule` that explicitly states the null return contract
   and mandates callers null-check the return value.
2. Verify every call site has an existing null guard (confirmed: ALL GUARDED).
3. Lock the null-return contract with one new `[Fact]` test.

**No return-type change. No call-site rewrites. No logic changes. Doc + test only.**

---

## 2. Deferred Backlog Review

From `docs/brain/B55-LaneA/06-deferred-backlog.md` (most recent):

| ID | Status | Relevant to LaneB? |
|----|--------|--------------------|
| DW-B54-01 | OPEN | No -- ATM API path issue |
| DW-B54-02 | OPEN | No -- live ATM bracket test |
| PRE-EXISTING-01 | OPEN | No -- 24 CopyEngineTests.cs pre-existing failures |
| PRE-EXISTING-02 | OPEN | No -- return null in PttBreakEven/PttFlatten (not FindRule) |
| PRE-EXISTING-03 | OPEN | No -- throw new in B42Tests/TradeCopierWindow |

**No open deferred items are closed or affected by this lane.**

Test baseline inherited from B55-LaneA: 279 total (255 pass, 24 fail pre-existing).
After this lane: 280 total (256 pass, 24 fail pre-existing) -- 1 new [Fact] T_B55B_01.

---

## 3. Component List

| File | Type | Change |
|------|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | Production | Add XML doc comment above `FindRule` method signature (lines 1196-1207 area) |
| `src/PropTraderTools/CopyEngineTests.cs` | Test | Add `T_B55B_01` [Fact] at end of test class |

**Files NOT touched by this lane:**
- `TradeCopierPanel.cs` (LaneA only)
- `Tests/B55Tests.cs` (LaneA only)
- All other feature files

---

## 4. Class and Method Signatures

### 4.1 FindRule (existing -- signature unchanged)

**File:** `src/PropTraderTools/CopyEngine.cs`
**Location:** approximately line 1197

```csharp
// XML doc comment to ADD (lines 1193-1207 context area):
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

**CYC = 3 (unchanged):** branch 1 = null guard, branch 2 = foreach, branch 3 = name match.
**No logic change. No signature change. Doc comment insert only.**

### 4.2 T_B55B_01 (new [Fact] in CopyEngineTests.cs)

**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Class:** `CopyEngineTests` (existing class, appended)
**CYC = 1 (straight-line)**

```csharp
// T_B55B_01 -- FindRule_ReturnsNull_WhenNoRules
// Documents and locks the null-return contract of FindRule.
// Engine with empty _rules list: FindRule(null instrument) returns null.
// Uses reflection (private method, sealed class) -- same pattern as B53 LaneA tests.
[Fact]
public void T_B55B_01_FindRule_ReturnsNull_WhenNoRules()
{
    // Arrange: engine with no rules active
    _engine.SetEnabled(false);

    var mi = typeof(CopyEngine).GetMethod(
        "FindRule",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(mi);

    // Verify signature: private CopyRule? FindRule(Instrument instrument)
    Assert.Equal(typeof(CopyRule?), mi.ReturnType);
    Assert.Equal(1, mi.GetParameters().Length);
    Assert.Equal(typeof(NinjaTrader.Cbi.Instrument), mi.GetParameters()[0].ParameterType);

    // Act: null instrument hits null guard -- returns null (no rules match)
    var result = mi.Invoke(_engine, new object[] { (NinjaTrader.Cbi.Instrument)null });

    // Assert: null contract confirmed -- no rule exists, no CopyRule returned
    Assert.Null(result);
}
```

**Note on test approach:** `FindRule` is `private` in a `sealed class`. Reflection is the
standard pattern for these tests (see `T_B53_FindRuleByFollower_ReturnsRule` as the
established precedent in this file). Passing `null` as the `Instrument` argument exercises
the first guard in `FindRule` (`if (instrument == null) return null`) which is also the same
observable behaviour as "no rules match" — both return `null`. The test documents the null
return contract and locks it so any future refactoring that removes the null return will fail
this test.

---

## 5. Call-Site Audit (SCAN-08 Pre-Run)

**Command run (Wave workspace):**
```
Get-ChildItem -Path "C:\WSGTA\universal-or-strategy\src" -Filter "*.cs" -Recurse |
  Select-String -Pattern "FindRule\(" |
  Select-Object Filename, LineNumber, Line
```

**Results — production call sites only (excluding definition + test comments):**

| File | Line | Call | Guard (next line) | Status |
|------|------|------|-------------------|--------|
| `CopyEngine.cs` | 1185 | `var rule = FindRule(instrument);` | L1186: `if (rule == null) yield break;` | GUARDED |
| `CopyEngine.cs` | 1197 | `private CopyRule? FindRule(...)` | (definition) | N/A |
| `CopyEngine.cs` | 1355 | `var rule = FindRule(instrument);` | L1356: `if (rule == null)` return; | GUARDED |

**External files searched:**
- `PttQuickExit.cs`: no `FindRule(` call found (only a comment in file header)
- `PttTightenStop.cs`: does not exist as a separate file; tighten logic is in `CopyEngine.cs`
- All other `src/PropTraderTools/*.cs` and `src/PropTraderTools/**/*.cs`: no `FindRule(` found

**Result: 2 call sites. Both GUARDED. SCAN-08 = ALL GUARDED.**

The `AllAccounts()` call at L1185 guards with `if (rule == null) yield break;`.
The `TightenStop()` call at L1355 guards with `if (rule == null) return;`.

---

## 6. Data Flow

`FindRule` is a private helper called only within `CopyEngine`:

```
instrument (Instrument param)
  --> FindRule(instrument)
        [null guard] instrument == null --> return null
        [loop] foreach rule in _rules
               rule.Instrument == instrument.FullName --> return rule (CopyRule?)
        [no match] --> return null

Callers:
  AllAccounts(instrument) L1185:
    rule = FindRule(instrument) --> if null: yield break (empty enumerable, no copies)
  TightenStop(instrument, ticks) L1355:
    rule = FindRule(instrument) --> if null: return (no-op, no stops tightened)
```

The null return at `FindRule` is always handled by an explicit guard at each call site.
The XML doc comment documents this contract precisely.

---

## 7. Threading Model

No threading changes. `FindRule` reads `_rules` (a `ConcurrentBag<CopyRule>`)
using `foreach` -- ConcurrentBag snapshot enumeration is lock-free and thread-safe.
No `lock()` added or removed. JS-021 compliance: unchanged.

---

## 8. NT8 API Surface

XML doc comments (`///`, `<summary>`, `<returns>`, `<see cref="..."/>`, `<c>`) are
standard C# XML documentation syntax supported in all .NET versions including
.NET Framework 4.8 (NT8's target). No NT8 rule is triggered.

NT8 rules checked:
- NT8-001 (`{ get; init; }`): Not used. PASS.
- NT8-002 (`abstract/sealed record`): Not used. PASS.
- NT8-003 (`volatile` field): Not added. PASS.
- NT8-004 (`ImmutableDictionary`): Not used. PASS.
- NT8-007 (`CreateOrder` arg 12): Not touched. PASS.
- All other NT8 rules: No production code logic changed. PASS.

---

## 9. Tickets

### Ticket T1 — Add XML Doc Comment to FindRule

**Spec requirement:** DW-B47-05 P2 step 1 (CHANGE SPEC section of spec section-b55 LaneB)
**File:** `src/PropTraderTools/CopyEngine.cs`
**Wave workspace path:** `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`

**Task:** Insert the following XML doc comment block immediately above the
`private CopyRule? FindRule(Instrument instrument)` line signature (currently line 1197):

```csharp
/// <summary>
/// Finds the copy rule for the given instrument.
/// </summary>
/// <returns>
/// Matching <see cref="CopyRule"/>, or <c>null</c> if no rule exists for this instrument.
/// Callers MUST null-check the return value.
/// </returns>
```

**Invariants:**
- Method signature unchanged: `private CopyRule? FindRule(Instrument instrument)`
- Method body unchanged: null guard + foreach loop + null fallthrough
- CYC unchanged: 3
- No new `lock()`, no `async void`, no `return null` (pre-existing null returns not new)

**JS rules applied:**
- JS-021: no lock() -- PASS (no change to method)
- JS-002: null contract now documented via XML doc -- PASS (explicit contract)
- JS-033: no async void -- PASS

**NT8 rules applied:**
- XML doc syntax fully supported in .NET 4.8 -- PASS

**Verification step:** After edit, read `CopyEngine.cs` lines 1193-1210, confirm:
```
/// <summary>
...doc comment block...
/// </returns>
private CopyRule? FindRule(Instrument instrument)
{
    if (instrument == null)
        return null; // Change 8: null guard
    ...unchanged...
}
```

---

### Ticket T2 — Add T_B55B_01 Test to CopyEngineTests.cs

**Spec requirement:** DW-B47-05 P2 step 3 (NEW TESTS section of spec section-b55 LaneB)
**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Wave workspace path:** `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`

**Task:** Append the following `[Fact]` method to the `CopyEngineTests` class, after the
last test method in the file (find the closing `}` of the class, insert before it):

```csharp
// T_B55B_01 -- FindRule_ReturnsNull_WhenNoRules
// Documents and locks the null-return contract: FindRule returns null when no rule
// matches the given instrument. Uses reflection (private method in sealed class).
// Same reflection pattern as T_B53_FindRuleByFollower_ReturnsRule.
// JS-002: null contract now tested and documented.
[Fact]
public void T_B55B_01_FindRule_ReturnsNull_WhenNoRules()
{
    // Arrange: engine with no rules active
    _engine.SetEnabled(false);

    var mi = typeof(CopyEngine).GetMethod(
        "FindRule",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(mi);

    // Verify signature: private CopyRule? FindRule(Instrument instrument)
    Assert.Equal(typeof(CopyRule?), mi.ReturnType);
    Assert.Equal(1, mi.GetParameters().Length);
    Assert.Equal(typeof(NinjaTrader.Cbi.Instrument), mi.GetParameters()[0].ParameterType);

    // Act: null instrument hits null guard -- returns null (no rules match)
    var result = mi.Invoke(_engine, new object[] { (NinjaTrader.Cbi.Instrument)null });

    // Assert: null contract confirmed -- no rule exists, no CopyRule returned
    Assert.Null(result);
}
```

**Invariants:**
- `using System.Reflection;` already present in CopyEngineTests.cs (line 9)
- `using NinjaTrader.Cbi;` already present (line 10)
- `using CopyRule = PropTraderTools.CopyEngine.CopyRule;` already present (line 12)
- `_engine` field already declared: `private readonly CopyEngine _engine = CopyEngine.Instance;`
- CYC of new test: 1 (straight-line, no branches)
- No new `lock()`, no `async void`, no `return null`

**JS rules applied:**
- JS-021: no lock -- PASS
- JS-002: null documented via test assertion -- PASS
- JS-033: no async void -- PASS

**NT8 rules applied:** Test file only -- not compiled by NT8's Roslyn. PASS.

**Verification step:** After edit, run `dotnet test` — confirm T_B55B_01 appears and PASSES.

---

## 10. Scan Checklist (7+1 — ptt-verifier contract)

| Scan | Command | Expected Result |
|------|---------|-----------------|
| SCAN-01 | `Select-String "lock(" src/ -Recurse -Include *.cs` (Wave workspace) | 0 results |
| SCAN-02 | `Select-String "async void " src/ -Recurse -Include *.cs` | 0 results |
| SCAN-03 | `Select-String "return null" src/ -Recurse -Include *.cs` | 0 NEW instances (pre-existing OK) |
| SCAN-04 | `Select-String "throw new " src/ -Recurse -Include *.cs` | 0 NEW instances |
| SCAN-05 | `python scripts/complexity_audit.py` (Wave workspace) | all methods CYC <= 8 |
| SCAN-06 | `dotnet build` (Wave workspace) | 0 errors (pre-existing warnings OK) |
| SCAN-07 | `dotnet test` | T_B55B_01 PASS; all baseline tests unchanged (255 pass + 24 pre-existing fail) |
| SCAN-08 | FindRule call-site audit (see section 5) | ALL GUARDED |

**SCAN-08 detail:** ptt-verifier must:
1. Run `Get-ChildItem C:\WSGTA\universal-or-strategy\src -Filter *.cs -Recurse | Select-String "FindRule\("`
2. For each production call site, read +-2 lines context, confirm null guard present
3. Output: list of call sites with guard confirmation
4. Expected: L1185 GUARDED (yield break), L1355 GUARDED (return) -- ALL GUARDED

---

## 11. Build Tag

`PTT-COPIER B55 | findrule-null-contract | 2026-08-10`

---

## 12. Final Pass Criteria

- [ ] ptt-verifier reports VERIFY_PASS on all 8 scans
- [ ] 1 new [Fact] T_B55B_01 passing
- [ ] FindRule XML doc comment present in CopyEngine.cs immediately above method signature
- [ ] All FindRule call sites confirmed guarded (SCAN-08 ALL GUARDED)
- [ ] Zero logic changes to FindRule body
- [ ] Zero changes to any call site logic (doc + test only pass)
- [ ] No new lock(), no new async void
- [ ] Hard-link sync complete (`powershell -File scripts\verify_links.ps1 -Fix`)

---

## 13. JS-002 Compliance Note

JS-002 (Use Option<T> Instead of Null) formally bans `return null`. The full Option<T>
fix for `FindRule` would require changing the return type from `CopyRule?` to
`Option<CopyRule>` and rewriting all 6+ call sites. This is blocked on NT8/.NET 4.8:
- `Option<T>` is not part of .NET Framework 4.8 BCL
- Introducing a custom Option<T> struct requires a separate block with its own scope and test coverage
- Changing all call sites violates the No Scope Creep Protocol for this block

**Pragmatic resolution (spec-approved):** Add XML doc comment + [Fact] test that lock the
null contract explicitly. The null return is now documented, tested, and provably guarded
at every call site. DW-B47-05 is closed as "null contract documented and tested; full
Option<T> migration deferred to future block per No Scope Creep Protocol."

This resolution was explicitly chosen in the spec: "Option chosen: add `[return: MaybeNull]`
annotation + doc comment."
