# Tickets — PTT-REPAIRS-11-BINDING-FLAGS-03

**Epic:** PTT-REPAIRS-11-BINDING-FLAGS-03  
**Phase:** 3 — Ticket Generation  
**Status:** TICKETS_COMPLETE  
**Source plan:** `docs/brain/PTT-REPAIRS-11-BINDING-FLAGS-03/02-architecture-plan.md` (REVIEW_PASS)  
**Deferred item closed:** DW-09-03

---

## T1 — Fix BindingFlags.Instance→Static for GetSenderAccountName reflection test

**Spec requirements satisfied:**
- DW-09-03: Remove incorrect Skip from `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate`
- Root cause: `BindingFlags.Instance` excludes static members; `GetSenderAccountName` is `internal static`
- Fix: Add `GetStaticMethod` helper (`BindingFlags.NonPublic | BindingFlags.Static`), switch test call site, remove Skip

**File:** `src/PropTraderTools/CopyEngineTests.cs`  
**Class:** `BwaveCycTaR2HelperTests`  
**Total edits:** 3 (all in the same file, no production `.cs` changes)

---

### Edit A — Insert `GetStaticMethod` helper

**Placement:** Immediately after line 6695 (the existing `GetMethod` expression-body helper), inside `BwaveCycTaR2HelperTests`.

**Exact text to insert (one blank line before, expression-body form):**

```csharp
        private static MethodInfo GetStaticMethod(string name) =>
            typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
```

**Full method signature:**

```
private static MethodInfo GetStaticMethod(string name)
```

- Return type: `System.Reflection.MethodInfo`
- Parameter: `string name` — the unobfuscated method name
- BindingFlags constraint: `BindingFlags.NonPublic | BindingFlags.Static` — required to resolve `internal static` members
- CYC: **1** (single expression, no branches)
- No `lock()`, no `throw`, no `DateTime.Now`, ASCII-only identifier

**InternalsVisibleTo note:**  
`CopyEngine.cs` line 46 contains `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]`. This attribute causes the .NET reflection engine to treat `internal` members as `NonPublic` when called from the test assembly. `BindingFlags.NonPublic` therefore resolves `internal static` members correctly. Without `InternalsVisibleTo`, `internal` members would be invisible to `Type.GetMethod` from an external assembly even with `NonPublic`.

**DO NOT modify** the adjacent existing helper on L6694–6695:

```csharp
private static MethodInfo GetMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
```

That helper is correct for all instance methods in `BwaveCycTaR2HelperTests` and must be left untouched.

---

### Edit B — Remove Skip from test attribute

**Location:** Line 6803 of `src/PropTraderTools/CopyEngineTests.cs`

**Find (exact):**
```csharp
        [Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]
```

**Replace with:**
```csharp
        [Fact]
```

**xUnit test name being un-skipped:**  
`GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate`

---

### Edit C — Switch call site from `GetMethod` to `GetStaticMethod`

**Location:** Line 6808 of `src/PropTraderTools/CopyEngineTests.cs`

**Find (exact):**
```csharp
            var m = GetMethod("GetSenderAccountName");
```

**Replace with:**
```csharp
            var m = GetStaticMethod("GetSenderAccountName");
```

---

### DO NOT TOUCH list (engineer must verify zero diff on these)

| Location | Reason |
|----------|--------|
| `BwaveCycT1R1BeHelperTests` L6571 — `GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNull` | NT8-runtime-skipped; instance invocation path; out of scope |
| `BwaveCycT1R1BeHelperTests` L6580 — `GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNotAccount` | NT8-runtime-skipped; instance invocation path; out of scope |
| `BwaveCycTaR2HelperTests` L6694–6695 — existing `GetMethod(string name)` | Correct for all other (instance) tests in this class; must not be modified |
| All other `[Fact(Skip=...)]` tests in `BwaveCycTaR2HelperTests` | Only `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate` loses Skip |
| All production `.cs` files | Zero production changes required or permitted |

---

### Contingency: If test fails after Skip removal

Per mission brief: do NOT leave the test un-skipped if it fails. Instead:

1. Revert Edit B and Edit C (restore `[Fact(Skip=...)]` and `GetMethod(...)`)
2. Keep Edit A (`GetStaticMethod` helper) — it is correct and harmless
3. Document as new deferred item:  
   `DW-REPAIRS-03-FALLBACK: GetSenderAccountName reflection fails with NonPublic|Static despite ObfuscationAttribute(Exclude=true). Investigate assembly load order.`

Failure probability is negligible given:
- `GetSenderAccountName` carries `[ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- `InternalsVisibleTo("PropTraderTools.Tests")` is confirmed at `CopyEngine.cs` L46
- `BindingFlags.NonPublic | BindingFlags.Static` is the correct flag combination for `internal static`
- Test asserts only `Assert.NotNull(m)` — no NT8 runtime required

---

## 7-Scan Checklist (SCAN-01 through SCAN-07)

All 7 scans must report zero violations before the ticket is closed. Engineer runs each scan after applying edits.

### SCAN-01 — No new `lock(` statements
```
grep -n "lock(" src/PropTraderTools/CopyEngineTests.cs
```
**Pass condition:** Zero new `lock(` matches introduced by this ticket's edits.

### SCAN-02 — No new `throw` statements
```
grep -n "throw " src/PropTraderTools/CopyEngineTests.cs
```
**Pass condition:** Zero new `throw` statements introduced by this ticket's edits.

### SCAN-03 — No `DateTime.Now` usage
```
grep -n "DateTime\.Now" src/PropTraderTools/CopyEngineTests.cs
```
**Pass condition:** Zero `DateTime.Now` references in file (none exist; none added).

### SCAN-04 — CYC check on `GetStaticMethod`
`GetStaticMethod` is a single expression-body method with no conditional branches:
```csharp
private static MethodInfo GetStaticMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
```
**Pass condition:** CYC = 1. Reviewer confirms by inspection — one path, zero branches.

### SCAN-05 — ASCII-only identifiers
All identifiers introduced: `GetStaticMethod`, `name`, `BindingFlags`, `NonPublic`, `Static`.  
**Pass condition:** All characters are 7-bit ASCII. No Unicode, no curly quotes, no emoji.

### SCAN-06 — Build gate
```
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
**Pass condition:** Build output contains `0 Error(s)`. Warnings are tolerated; errors are not.

### SCAN-07 — Test gate
```
dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "FullyQualifiedName~GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate"
```
**Pass condition:** `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate` — status `PASSED`.  
If status is `FAILED`, do NOT close this ticket — execute the Contingency path above.

---

## Expected metrics delta (this epic only)

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Passed | ≥23 | ≥24 | +1 |
| Skipped | ≥491 | ≥490 | -1 |
| Failed | 0 | 0 | 0 |
| Total | 514 | 514 | 0 |

---

**TICKETS_COMPLETE**
