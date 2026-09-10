# PTT-REPAIRS-11-BINDING-FLAGS-02 — Implementation Tickets

**Epic:** PTT-REPAIRS-11-BINDING-FLAGS-02
**Status:** TICKETS_COMPLETE
**Wave workspace:** `C:\WSGTA\universal-or-strategy\`
**Source plan:** `docs/brain/PTT-REPAIRS-11-BINDING-FLAGS-02/02-architecture-plan.md` (REVIEW_PASS)
**Ticket count:** 1 (SINGLE-PIPELINE gate confirmed)

---

## T1 — Fix LogBeSlotEviction Binding Flags in BwaveCycTaR3HelperTests

### Spec Requirement IDs
- **DW-09-02** — Fix `LogBeSlotEviction` binding flags in `BwaveCycTaR3HelperTests`; remove 2 obfuscation-skip annotations.
- Prerequisite **DW-09-01** CLOSED (BWAVE-CYC-IMPL-01): `LogBeSlotEviction` implemented as `private static`.
- Prerequisite **ObfuscationAttribute** confirmed: `CopyEngine.cs` L1778 `Exclude = true` — AgileDotNetRT will not rename `LogBeSlotEviction`.

### File and Class
| Field | Value |
|---|---|
| **File** | `src/PropTraderTools/CopyEngineTests.cs` |
| **Class** | `BwaveCycTaR3HelperTests` |
| **Other files touched** | NONE |

### Root Cause
`BwaveCycTaR3HelperTests.GetMethod` uses `BindingFlags.NonPublic | BindingFlags.Instance`.
`LogBeSlotEviction` is `private static`. Instance flags exclude static members — `GetMethod` returns `null`, causing `Assert.NotNull` to fail.
The `[Fact(Skip = "...")]` annotations were a workaround; the real fix is a `GetStaticMethod` helper using `BindingFlags.NonPublic | BindingFlags.Static`.

### Method Signatures

#### New method to add (Change A)
```csharp
private static MethodInfo GetStaticMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
```
- **Visibility:** `private static`
- **Return type:** `System.Reflection.MethodInfo`
- **Parameter:** `string name`
- **CCN:** 1 (single expression, no branches)
- **Placement:** Immediately after the closing semicolon of the existing `GetMethod` helper at line ~6821 (one blank line separator)

#### Existing method — DO NOT MODIFY
```csharp
private static MethodInfo GetMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
```
This helper remains unchanged. All other tests in `BwaveCycTaR3HelperTests` that call `GetMethod(...)` continue to use `BindingFlags.Instance` which is correct for private instance members.

### xUnit Tests Affected

| Test Method | Before Fix | After Fix |
|---|---|---|
| `LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod` | SKIPPED (`[Fact(Skip="...")]`) | PASSED |
| `LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters` | SKIPPED (`[Fact(Skip="...")]`) | PASSED |

### Step-by-Step Implementation Instructions

**PREREQUISITE CHECK before touching any file:**
```powershell
dotnet test src/PropTraderTools/PropTraderTools.csproj --no-build
```
Confirm baseline: `23 passed / 0 failed / 491 skipped / 514 total`. If baseline differs, STOP and report before proceeding.

---

#### Step 1 — Locate insertion point for Change A
Open `src/PropTraderTools/CopyEngineTests.cs`.
Find the line containing:
```
typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
```
inside class `BwaveCycTaR3HelperTests` (around line 6821). This is the end of the existing `GetMethod` helper.

**Insert the following two lines immediately after** (one blank line, then the new helper):
```csharp

        private static MethodInfo GetStaticMethod(string name) =>
            typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
```

Indentation: 8 spaces (matching the existing `GetMethod` helper in the same class).

---

#### Step 2 — Apply Change B (Test 1)
Find the block:
```csharp
        [Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]
        public void LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod()
        {
            var m = GetMethod("LogBeSlotEviction");
```

Replace with:
```csharp
        [Fact]
        public void LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod()
        {
            var m = GetStaticMethod("LogBeSlotEviction");
```

**Exactly two edits on this block:** (1) `[Fact(Skip = "...")]` → `[Fact]`; (2) `GetMethod(` → `GetStaticMethod(`. No other lines changed.

---

#### Step 3 — Apply Change C (Test 2)
Find the block:
```csharp
        [Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]
        public void LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters()
        {
            var m = GetMethod("LogBeSlotEviction");
```

Replace with:
```csharp
        [Fact]
        public void LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters()
        {
            var m = GetStaticMethod("LogBeSlotEviction");
```

**Exactly two edits on this block:** (1) `[Fact(Skip = "...")]` → `[Fact]`; (2) `GetMethod(` → `GetStaticMethod(`. No other lines changed.

---

#### Step 4 — Build verification
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
Required: `Build succeeded. 0 Error(s)`. If any error appears, revert and investigate before continuing.

---

#### Step 5 — Test run verification
```powershell
dotnet test src/PropTraderTools/PropTraderTools.csproj --no-build
```
Required target: `25 passed / 0 failed / 489 skipped / 514 total`

Verify:
- `LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod` → PASSED
- `LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters` → PASSED
- Zero tests that were PASSED at baseline have become FAILED

---

### Expected Test Results

| State | Passed | Failed | Skipped | Total |
|---|---|---|---|---|
| **BASELINE (before fix)** | 23 | 0 | 491 | 514 |
| **TARGET (after fix)** | 25 | 0 | 489 | 514 |

Delta: +2 passed, −2 skipped. All other test counts unchanged.

---

### Fallback Action (if tests fail post-fix)

If either `LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod` or `LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters` reports **FAIL** (i.e., `Assert.NotNull(m)` fails, meaning reflection still returns `null`):

1. **REVERT Changes B and C only.** Restore both `[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]` annotations and restore both `GetMethod(` calls.
2. **KEEP Change A** — the `GetStaticMethod` helper is inert if never called. It does not affect build or test results and may be useful for future use.
3. **DO NOT remove any `[Fact(Skip = "...")]` annotations** from other tests.
4. **Document as DW-09-02-BLOCKED** in `docs/brain/PTT-REPAIRS-11-BINDING-FLAGS-02/06-deferred-backlog.md` with exact `dotnet test` output showing the failure.
5. Baseline remains `23 passed / 0 failed / 491 skipped / 514 total`.

---

### 7-Scan Checklist (Mandatory Engineer Contract)

Run all 7 scans after completing Steps 1–3, before running the build in Step 4. Every scan must show zero violations before the PR is submitted. Any scan failure blocks merge.

---

**SCAN-01 — No lock() introduced**
```powershell
grep -r "lock(" src/PropTraderTools/
```
Required result: **zero matches** in the diff and in the full directory.
This change introduces no lock() statements. If grep returns any matches, they are pre-existing and do not block this ticket (but DO NOT introduce new ones).

---

**SCAN-02 — No new throw statements**
Inspect the diff for this ticket. Count new lines starting with or containing `throw`.
Required result: **zero new `throw` statements** in any `.cs` file in the diff.
This change adds no throw statements. Only `GetStaticMethod` body, `[Fact]`, and `GetStaticMethod(` call replacements are present.

---

**SCAN-03 — Cyclomatic complexity**
All touched and new methods must have CCN = 1:

| Method | CCN | Rationale |
|---|---|---|
| `GetStaticMethod(string name)` (NEW) | 1 | Single expression-body, no branches |
| `LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod` (MODIFIED) | 1 | Sequential assignments + one Assert, no branches |
| `LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters` (MODIFIED) | 1 | Sequential assignments + two Asserts, no branches |

Required result: **all three methods CCN = 1**.

---

**SCAN-04 — ASCII-only string literals**
Inspect all inserted and modified string literals:
- `"LogBeSlotEviction"` — ASCII only.
- `BindingFlags.NonPublic | BindingFlags.Static` — no string literal, enum flags.

Required result: **zero non-ASCII characters** (no Unicode, emoji, curly quotes) in any string literal introduced by this ticket.

---

**SCAN-05 — ObfuscationAttribute on LogBeSlotEviction**
```powershell
grep -n "ObfuscationAttribute" src/PropTraderTools/CopyEngine.cs
```
Confirm line ~1778 reads:
```
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
```
Required result: `Exclude = true` present on `LogBeSlotEviction`. **Production file `CopyEngine.cs` is unmodified by this ticket** — if the attribute is missing, this is a pre-existing issue and must be reported before removing the Skips.

---

**SCAN-06 — BindingFlags correctness**
Inspect the inserted `GetStaticMethod` helper:
```csharp
typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
```
Required result: Flags are `NonPublic | Static`. **Zero use of `BindingFlags.Instance`** in the new helper.
Verify the existing `GetMethod` helper still uses `NonPublic | Instance` (unchanged).

---

**SCAN-07 — Build zero errors**
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
Required result: `Build succeeded. 0 Error(s)`.

---

**Scan failure action:** Any scan that returns a violation blocks merge. Fix the violation and re-run all 7 scans from SCAN-01 before re-submitting.

---

### Scope Lock — What Is NOT Changing

| Item | Disposition |
|---|---|
| `CopyEngine.cs` | NOT touched — no production changes |
| `GetMethod(string)` helper in `BwaveCycTaR3HelperTests` | NOT changed |
| Any other `[Fact(Skip = "...")]` in the file | NOT removed |
| `deploy-sync.ps1` | NOT needed — test file only, no hard-linked production file |
| Any other class in `CopyEngineTests.cs` | NOT touched |
| DW-09-03 (`GetSenderAccountName` binding fix) | OUT OF SCOPE |
| DW-09-04 (full obfuscation skip removal) | OUT OF SCOPE |

---

### Deferred Work Carried Forward

| ID | Description | Status |
|---|---|---|
| DW-09-02 | Fix `LogBeSlotEviction` binding flags — 2 Skips removed | **CLOSED by T1** |
| DW-09-03 | Fix `GetSenderAccountName` binding flags in `BwaveCycTaR2HelperTests`: `NonPublic\|Instance` → `NonPublic\|Static`. Remove obfuscation Skip from 1 affected test. | OPEN |
| DW-09-04 | Remove all 137 remaining obfuscation-skip annotations after each method has verified production logic and ObfuscationAttribute confirmed. Prerequisites: DW-09-02 + DW-09-03 complete. | OPEN |
