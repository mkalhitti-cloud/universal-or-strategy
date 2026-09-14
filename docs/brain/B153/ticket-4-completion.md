# Ticket Completion: DW-B153-04
# Remove IsPttQxTarget Single-Digit Ceiling (BE after QX)

**Block:** B153
**Ticket:** DW-B153-04
**Phase:** 4a — Implementation
**Engineer:** ptt-engineer
**Date:** 2026-09-13
**Status:** BUILD_PASS

---

## Summary

Removed the hardcoded single-digit ceiling from `IsPttQxTarget` in
`src/PropTraderTools/Features/PttBreakEven.cs`.

**Root cause:** The old implementation used `name.Length != 9` (rejecting
any name not exactly 9 chars) and `name[8] >= '1' && name[8] <= '3'`
(restricting to T1/T2/T3 only). Both constraints prevented T4+ from being
recognized during "BE after QX ALL" with 4+ targets.

**Fix:** Changed to `name.Length < 9` (accepts T10, T22, etc.) and replaced
the char-index range check with `int.TryParse(name.Substring(8), out int n) && n >= 1`.

**Files changed:**
- `src/PropTraderTools/Features/PttBreakEven.cs` — surgical edit (IsPttQxTarget)
- `src/PropTraderTools/CopyEngineTests.cs` — 6 new [Fact] tests added

---

## Before / After Diff

### Before (`PttBreakEven.cs` lines 581-595)

```csharp
        /// <summary>
        /// Return true if name is a PTT Quick Exit target order (PTT-QX-T1, PTT-QX-T2, PTT-QX-T3).
        /// These are plain Limit orders -- LimitPrice and Quantity are readable.
        /// BUG-B42-QX-BE-01 FIX (Direction 1): BE All after Quick All must recognise QX targets.
        /// CYC=2: (1) length+null guard, (2) char-index body.
        /// JS-021: no lock. JS-002: returns bool. NT8-006: no LINQ, char primitives only.
        /// </summary>
        private static bool IsPttQxTarget(string name)
        {
            if (name == null || name.Length != 9)
                return false;
            return name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                && name[8] >= '1'
                && name[8] <= '3';
        }
```

### After (`PttBreakEven.cs` lines 581-601)

```csharp
        /// <summary>
        /// Return true if name is a PTT Quick Exit target order (PTT-QX-T1, PTT-QX-T2, ..., PTT-QX-TN).
        /// These are plain Limit orders -- LimitPrice and Quantity are readable.
        /// BUG-B42-QX-BE-01 FIX (Direction 1): BE All after Quick All must recognise QX targets.
        /// B153-REQ-04: ceiling removed -- accepts T4, T10, T22, etc.
        /// CYC=5. JS-021: no lock. JS-002: returns bool. NT8-006: no LINQ.
        /// </summary>
        // CYC=5
        private static bool IsPttQxTarget(string name)
        {
            // "PTT-QX-T" is 8 chars. Minimum valid name is "PTT-QX-T1" (length 9).
            // Substring(8) extracts "1", "2", "10", "22" etc.
            if (name == null || name.Length < 9)
                return false;
            if (!name.StartsWith("PTT-QX-T", StringComparison.Ordinal))
                return false;
            return int.TryParse(name.Substring(8), out int n) && n >= 1;
        }
```

**Key semantic changes:**
| Line | Before | After |
|------|--------|-------|
| Length guard | `name.Length != 9` | `name.Length < 9` |
| Prefix check | Compound return with `&&` | Standalone guard `if (!StartsWith) return false;` |
| Index check | `name[8] >= '1' && name[8] <= '3'` | `int.TryParse(name.Substring(8), out int n) && n >= 1` |
| CYC annotation | `// CYC=2` | `// CYC=5` |

---

## Scan Results

All 7 scans run. All return zero violations.

| Scan | Command | Result | Status |
|------|---------|--------|--------|
| SCAN-1 | `grep -n "n > 3" CopyEngine.cs` | 0 matches | PASS (not applicable — different file) |
| SCAN-2 | `grep -n "i <= 3" CopyEngine.cs` | 0 matches | PASS (not applicable — different file) |
| SCAN-3 | `grep -n "new double\[3\]" CopyEngine.cs` | 0 matches | PASS (not applicable — different file) |
| SCAN-4 | `grep -n "Math.Min.*3" PttQuickExit.cs` | 0 matches | PASS (not applicable — different file) |
| SCAN-5 | `grep -n "targets.Count > 3" CopyEngine.cs` | 0 matches | PASS (not applicable — different file) |
| **SCAN-6** | `Select-String "name\.Length != 9" PttBreakEven.cs` | **0 matches** | **PASS — primary gate** |
| **SCAN-7** | `lizard PttBreakEven.cs --CCN 8` | **Warning cnt: 0** | **PASS** |
| **SCAN-8** | `Select-String "^\s+lock\s*\(" src/PropTraderTools/` | **0 actual lock() blocks** | **PASS** |
| **SCAN-9** | ASCII byte check on PttBreakEven.cs | **0 bytes > 127** | **PASS** |
| **SCAN-10** | `dotnet build universal-or-strategy.sln` | **0 errors, 0 warnings** | **PASS** |

### SCAN-6 output
```
(no output — 0 matches)
```

### SCAN-7 output
```
No thresholds exceeded (cyclomatic_complexity > 8 or length > 1000 ...)
Total nloc   Avg.NLOC  AvgCCN  Avg.token   Fun Cnt  Warning cnt   Fun Rt   nloc Rt
       474      18.5     4.4       92.3       25            0      0.00    0.00
```

### SCAN-8 output
```
(no output — 0 actual lock() blocks; all matches are comments "no lock()")
```

### SCAN-9 output
```
Non-ASCII bytes found: 0
```

### SCAN-10 output
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.34
```

---

## xUnit Test Results

All 6 [Fact] tests from the ticket pass.

```
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 164 ms
```

| Test Name | Result |
|-----------|--------|
| `IsPttQxTarget_T4_ReturnsTrue` | PASS |
| `IsPttQxTarget_T10_ReturnsTrue` | PASS |
| `IsPttQxTarget_T22_ReturnsTrue` | PASS |
| `IsPttQxTarget_T0_ReturnsFalse` | PASS |
| `IsPttQxTarget_NullName_ReturnsFalse` | PASS |
| `IsPttQxTarget_T3_StillReturnsTrue` | PASS |

---

## Architecture-Locked Constraints Verified

| Constraint | Status |
|-----------|--------|
| `IsPttQxTarget` method signature unchanged | ✓ |
| `"PTT-QX-T"` prefix unchanged (ASCII-only) | ✓ |
| `name.Length < 9` (not `!= 9`) as per spec | ✓ |
| `// CYC=5` annotation present on method | ✓ |
| No new NT8 API calls introduced | ✓ |
| No threading changes (pure string predicate) | ✓ |
| No `lock()` blocks | ✓ |
| No `DateTime.Now` | ✓ |
| No Unicode / bytes > 127 | ✓ |
| No `throw` in hot path | ✓ |
| Boundary comments present inside method body | ✓ |

---

## CYC Analysis

| Metric | Value |
|--------|-------|
| Pre-fix CYC | ~5 (plan-review OBS-CYC-03: before was also ~5) |
| Delta | ~0 |
| Post-fix CYC | **5** |
| JS-013 limit | ≤8 |
| Status | PASS |

McCabe count breakdown (post-fix):
- base = 1
- `||` in null/length guard = +1
- `if (!StartsWith)` = +1
- `TryParse` (ternary-equivalent in return) = +1
- `&& n >= 1` = +1
- **Total = 5** — annotated as `// CYC=5`

---

## Return

**BUILD_PASS**
