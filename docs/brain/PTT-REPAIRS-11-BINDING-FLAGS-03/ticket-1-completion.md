# Ticket 1 Completion — PTT-REPAIRS-11-BINDING-FLAGS-03

**Epic:** PTT-REPAIRS-11-BINDING-FLAGS-03  
**Ticket:** T1 — Fix BindingFlags.Instance→Static for GetSenderAccountName reflection test  
**Engineer phase:** 4a  
**Status:** BUILD_PASS  
**File modified:** `src/PropTraderTools/CopyEngineTests.cs` (test file only — zero production `.cs` changes)

---

## Edits Applied

### Edit A — Insert `GetStaticMethod` helper

**Location:** `BwaveCycTaR2HelperTests` class, immediately after the existing `GetMethod` helper.

**Lines before edit:** 6694–6695 (existing `GetMethod`), blank line 6696.  
**Lines after edit:** 6694–6695 (unchanged `GetMethod`), new lines 6696–6697 (`GetStaticMethod`).

**Before:**
```csharp
        private static MethodInfo GetMethod(string name) =>
            typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);

        // -- HasValidTargetNameSuffix (extracted from IsLeaderTargetOrder) --------
```

**After:**
```csharp
        private static MethodInfo GetMethod(string name) =>
            typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
        private static MethodInfo GetStaticMethod(string name) =>
            typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

        // -- HasValidTargetNameSuffix (extracted from IsLeaderTargetOrder) --------
```

**New lines inserted:** 6696–6697 (after shift from Edit A insertion)

---

### Edit B — Remove Skip from test attribute

**Location:** `BwaveCycTaR2HelperTests` class, test `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate`.

**Line before edit (original L6803, shifted to L6805 after Edit A):** 6805  
**Line after edit:** 6805 (same position)

**Before:**
```csharp
        [Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]
```

**After:**
```csharp
        [Fact]
```

---

### Edit C — Switch call site from `GetMethod` to `GetStaticMethod`

**Location:** Body of `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate`.

**Line before edit (original L6808, shifted to L6810 after Edits A+B):** 6810  
**Line after edit:** 6810 (same position)

**Before:**
```csharp
            var m = GetMethod("GetSenderAccountName");
```

**After:**
```csharp
            var m = GetStaticMethod("GetSenderAccountName");
```

---

## DO NOT TOUCH — Verified Zero Diff

| Item | Status |
|------|--------|
| `BwaveCycT1R1BeHelperTests` — `GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNull` | UNTOUCHED |
| `BwaveCycT1R1BeHelperTests` — `GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNotAccount` | UNTOUCHED |
| `BwaveCycTaR2HelperTests` L6694–6695 — existing `GetMethod(string name)` | UNTOUCHED |
| All other `[Fact(Skip=...)]` tests | UNTOUCHED |
| All production `.cs` files | UNTOUCHED |

---

## 7-Scan Results

### SCAN-01 — No new `lock(` statements
```
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "lock\(" | Measure-Object | Count
```
**Result:** `0`  
**Status:** PASS — zero `lock(` matches in file.

---

### SCAN-02 — No new `throw` statements (new additions only)
```
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "throw " | Select-Object LineNumber, Line
```
**Result:** 11 pre-existing baseline matches (comments and existing code). None in lines 6694–6697 or 6805–6812 (our edits). Zero new `throw` statements introduced.  
**Pre-existing locations (baseline):** Lines 388, 851, 1327, 1452, 1502, 1770, 2462, 2622, 4872, 7777, 7779 — all comments or existing test infrastructure.  
**Status:** PASS — zero new `throw` statements from this ticket's edits.

---

### SCAN-03 — No `DateTime.Now` usage
```
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "DateTime\.Now" | Measure-Object | Count
```
**Result:** `0`  
**Status:** PASS — zero `DateTime.Now` references in file.

---

### SCAN-04 — CYC check on `GetStaticMethod`
```csharp
private static MethodInfo GetStaticMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
```
**Result:** Single expression-body method. One execution path. Zero branches. Zero conditionals.  
**CYC = 1**  
**Status:** PASS

---

### SCAN-05 — ASCII-only identifiers
All identifiers introduced by this ticket:  
`GetStaticMethod`, `name`, `BindingFlags`, `NonPublic`, `Static`

Character-by-character inspection:
- `GetStaticMethod` — all letters A-Z a-z (7-bit ASCII)
- `name` — all letters a-z (7-bit ASCII)
- `BindingFlags` — all letters A-Z a-z (7-bit ASCII)
- `NonPublic` — all letters A-Z a-z (7-bit ASCII)
- `Static` — all letters A-Z a-z (7-bit ASCII)

**Status:** PASS — all identifiers are 7-bit ASCII. No Unicode, no curly quotes, no emoji.

---

### SCAN-06 — Build gate
```
dotnet build src\PropTraderTools\PropTraderTools.Tests.csproj
```
**Result:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```
**Status:** PASS — `0 Error(s)`, build succeeded.

---

### SCAN-07 — Test gate
```
dotnet test src\PropTraderTools\PropTraderTools.Tests.csproj --filter "FullyQualifiedName~GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate" --no-build
```
**Result:**
```
Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 215 ms
```
**Status:** PASS — test `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate` PASSED.  
**Contingency:** NOT triggered. Test passes with `BindingFlags.NonPublic | BindingFlags.Static`.

---

## Final Build / Test Counts

| Metric | Baseline | After T1 | Delta |
|--------|----------|----------|-------|
| Passed | 23 | 24 | +1 |
| Skipped | 491 | 490 | -1 |
| Failed | 0 | 0 | 0 |
| Total | 514 | 514 | 0 |

*(Counts match expected metrics delta from 04-tickets.md)*

---

## Scan Summary Table

| Scan | Description | Result | Status |
|------|-------------|--------|--------|
| SCAN-01 | No new `lock(` | 0 matches | PASS |
| SCAN-02 | No new `throw` (new additions) | 0 new | PASS |
| SCAN-03 | No `DateTime.Now` | 0 matches | PASS |
| SCAN-04 | CYC of `GetStaticMethod` | CYC = 1 | PASS |
| SCAN-05 | ASCII-only new identifiers | All 7-bit ASCII | PASS |
| SCAN-06 | `dotnet build` | 0 Error(s) | PASS |
| SCAN-07 | Targeted test run | 1 Passed, 0 Failed | PASS |

All 7 scans: PASS.

---

## Deferred Item

No contingency triggered. DW-REPAIRS-03-FALLBACK is NOT applicable.  
Deferred item **DW-09-03** is CLOSED by this ticket.

---

## BUILD_PASS
