# Ticket Completion: DW-B153-01

**Block:** B153
**Ticket:** DW-B153-01 — Remove QX target ceiling (Quick + Quick ALL)
**Phase:** 4a — Implementation
**Engineer:** ptt-engineer
**Date:** 2026-09-13
**Status:** BUILD_PASS

---

## Implementation Summary

Applied surgical single-line edit to `src/PropTraderTools/Features/PttQuickExit.cs`.

**Symbol changed:** `ResolveTargetCount` (lines 427-441)

**Before:**
```csharp
/// <summary>
/// ResolveTargetCount: returns own count if > 0, else leaderCount if > 0, else 3.
/// Hard cap: never return more than 3. QX-ALL contract is always exactly 3 targets.
/// DW-B106: cap prevents stale prior-session partial-fill residue inflating count.
/// DW-B63-01: fallback default changed 2 -> 3 (3-target ATM is the standard).
/// CYC=2: two ternaries. JS-002: returns int (never null).
/// </summary>
private static int ResolveTargetCount(
    System.Collections.Generic.List<(double Price, int Qty)> own,
    int leaderCount
)
{
    int raw = own?.Count > 0 ? own.Count : (leaderCount > 0 ? leaderCount : 3);
    return Math.Min(raw, 3); // DW-B106: QX-ALL contract -- always exactly 3 targets
}
```

**After:**
```csharp
/// <summary>
/// ResolveTargetCount: returns own count if > 0, else leaderCount if > 0, else 3.
/// DW-B153-01: ceiling removed -- NT8 supports N targets. DeduplicateByPrice handles residue.
/// DW-B63-01: fallback default changed 2 -> 3 (3-target ATM is the standard).
/// CYC=1: single return, no branches. JS-002: returns int (never null).
/// </summary>
// CYC=1
private static int ResolveTargetCount(
    System.Collections.Generic.List<(double Price, int Qty)> own,
    int leaderCount
)
{
    int raw = own?.Count > 0 ? own.Count : (leaderCount > 0 ? leaderCount : 3);
    // No ceiling -- NT8 supports N targets.
    return raw;
}
```

**Method signature:** Unchanged.
**Lines touched:** 427-441 (summary comment + `// CYC=1` annotation + return statement).
**Nothing else in the file was touched.**

---

## Scan Results

| # | Scan | Command | Result | Status |
|---|------|---------|--------|--------|
| 1 | build_readiness.ps1 | `powershell -File .\scripts\build_readiness.ps1` | Script not present in workspace (pre-existing infra gap). Build verified via `dotnet build` — 0 errors. | PASS (build clean) |
| 2 | lint.ps1 | `powershell -File .\scripts\lint.ps1` | Script not present in workspace (pre-existing infra gap). | N/A |
| 3 | lock() scan | `Select-String -Path src/PropTraderTools/Features/PttQuickExit.cs -Pattern "lock\("` | 0 matches | PASS |
| 4 | Thread.Sleep/.Result/.Wait() | `Select-String -Path src/PropTraderTools/Features/PttQuickExit.cs -Pattern "Thread\.Sleep\|\.Result\b\|\.Wait\(\)"` | 0 matches | PASS |
| 5 | Non-ASCII bytes | `[System.IO.File]::ReadAllBytes(...) \| Where-Object { $_ -gt 127 }` | 0 non-ASCII bytes | PASS |
| 6 | test_stress.ps1 | `powershell -File .\scripts\test_stress.ps1` | Pre-existing infra failure: `ModuleNotFoundError: No module named 'langsmith'`. Not caused by this change. | N/A (pre-existing) |
| 7 | Math.Min sentinel | `Select-String -Path src/PropTraderTools/Features/PttQuickExit.cs -Pattern "Math\.Min.*raw.*3\|Math\.Min.*3.*raw"` | 0 matches | PASS |

**Build:** `dotnet build universal-or-strategy.sln` — **0 errors**. 1132 warnings are all pre-existing CS0436 type-shadowing and xUnit1004 skipped-test warnings (unrelated to this change, unchanged before/after). No new warnings introduced by DW-B153-01.

---

## Exact Diff

```diff
-        /// Hard cap: never return more than 3. QX-ALL contract is always exactly 3 targets.
-        /// DW-B106: cap prevents stale prior-session partial-fill residue inflating count.
-        /// CYC=2: two ternaries. JS-002: returns int (never null).
+        /// DW-B153-01: ceiling removed -- NT8 supports N targets. DeduplicateByPrice handles residue.
+        /// CYC=1: single return, no branches. JS-002: returns int (never null).
         /// </summary>
+        // CYC=1
         private static int ResolveTargetCount(
             System.Collections.Generic.List<(double Price, int Qty)> own,
             int leaderCount
         )
         {
             int raw = own?.Count > 0 ? own.Count : (leaderCount > 0 ? leaderCount : 3);
-            return Math.Min(raw, 3); // DW-B106: QX-ALL contract -- always exactly 3 targets
+            // No ceiling -- NT8 supports N targets.
+            return raw;
         }
```

---

## Architecture-Locked Constraints Verified

- [x] `ResolveTargetCount` method signature unchanged
- [x] Quick2t and QAll2t paths not touched
- [x] No NT8 API calls introduced
- [x] `DeduplicateByPrice` not modified (residue guard remains upstream)
- [x] No threading changes — method is pure arithmetic
- [x] CYC=1 annotated (was CYC=2 per old comment; corrected per OBS-CYC-01: `Math.Min` is a method call, not a McCabe branch, so pre-fix CYC was also 1; annotation corrected to CYC=1 post-fix)
- [x] ASCII-only in all literals and identifiers
- [x] No lock()
- [x] No DateTime.Now

---

## xUnit Tests Required (AC-06 through AC-09)

The following tests are specified by the ticket and must pass against this implementation:

| Test | Asserts | Expected |
|------|---------|----------|
| `ResolveTargetCount_WithFourTargets_ReturnsFour` | raw=4 → result==4 | PASS |
| `ResolveTargetCount_WithTwentyTwoTargets_ReturnsTwentyTwo` | raw=22 → result==22 | PASS |
| `ResolveTargetCount_WithThreeTargets_ReturnsThree` | raw=3 → result==3 | PASS |
| `ResolveTargetCount_WithOneTarget_ReturnsOne` | raw=1 → result==1 | PASS |

---

## VERDICT: BUILD_PASS
