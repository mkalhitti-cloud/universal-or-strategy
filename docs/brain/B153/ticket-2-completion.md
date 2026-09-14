# Ticket Completion: DW-B153-02

**Block:** B153
**Ticket:** DW-B153-02 — Remove BE target ceiling (BE + BE ALL)
**Phase:** 4a — Implementation
**Engineer:** ptt-engineer
**Date:** 2026-09-13
**Status:** BUILD_PASS

---

## Implementation Summary

Applied surgical edit to `src/PropTraderTools/CopyEngine.cs`.

**Symbol changed:** `MoveStopToBreakEven` (~line 6702)

**Before (lines 6702-6706):**
```csharp
            // DW-B107: hard cap -- BE/QX contract is always exactly 3 targets max.
            // Prevents stale partial-fill residue submitting extra OCO pairs.
            // No LINQ -- while-loop trim per JS zero-alloc mandate.
            while (targets.Count > 3)
                targets.RemoveAt(targets.Count - 1);
```

**After (line 6702):**
```csharp
            // No ceiling -- SnapshotBeTargets DeduplicateByPrice handles residue inflation.
```

**Method signature:** Unchanged.  
**Lines touched:** 6702-6706 (3 comment lines + while statement + RemoveAt body replaced with 1 comment).  
**Nothing else in the file was touched.**

---

## CYC Analysis

- `while (targets.Count > 3)` removed — one McCabe decision point eliminated.
- CYC delta = **-1** (confirmed per plan and ticket CYC analysis).
- `MoveStopToBreakEven` post-fix CCN = **6** (lizard measured: down from ~7).
- Well within JS-013 limit (<=8).

---

## Exact Diff

```diff
-            // DW-B107: hard cap -- BE/QX contract is always exactly 3 targets max.
-            // Prevents stale partial-fill residue submitting extra OCO pairs.
-            // No LINQ -- while-loop trim per JS zero-alloc mandate.
-            while (targets.Count > 3)
-                targets.RemoveAt(targets.Count - 1);
+            // No ceiling -- SnapshotBeTargets DeduplicateByPrice handles residue inflation.
```

---

## Scan Results

All 10 scans from the per-ticket checklist run and reported.

| # | Scan | Command | Result | Status |
|---|------|---------|--------|--------|
| SCAN-1 | `n > 3` in CopyEngine.cs | `Select-String ... -Pattern "n > 3"` | 1 match at line 3457 (TryParseStopSuffix — DW-B153-03 scope, **N/A for T02**) | PASS (N/A) |
| SCAN-2 | `i <= 3` in CopyEngine.cs | `Select-String ... -Pattern "i <= 3"` | 2 matches at lines 3421, 3628 (CaptureOtherLegTargetPrices, ResubmitCollateralLegs — DW-B153-03 scope, **N/A for T02**) | PASS (N/A) |
| SCAN-3 | `new double[3]` in CopyEngine.cs | `Select-String ... -Pattern "new double\[3\]"` | 1 match at line 3416 (CaptureOtherLegTargetPrices — DW-B153-03 scope, **N/A for T02**) | PASS (N/A) |
| SCAN-4 | `Math.Min.*3` in PttQuickExit.cs | `Select-String ... -Pattern "Math\.Min.*3"` | 0 matches (already cleaned by T01) | PASS (N/A — different file) |
| **SCAN-5** | **`targets.Count > 3` in CopyEngine.cs** | `Select-String ... -Pattern "targets\.Count > 3"` | **0 matches** | **PASS — PRIMARY GATE** |
| SCAN-6 | `name.Length != 9` in PttBreakEven.cs | `Select-String ... -Pattern "name\.Length != 9"` | 1 match at line 590 (IsPttQxTarget — DW-B153-04 scope, **N/A for T02**) | PASS (N/A — different file) |
| SCAN-7 | lizard CopyEngine.cs --CCN 8 | `lizard src/PropTraderTools/CopyEngine.cs --CCN 8 -w` | Warning cnt: **7** (all pre-existing methods: MirrorCloseOneAccount@2411, TrimOneAccount@5638, CancelExistingTgtDragOrders@8331, HasInFlightFlattenOrder@8484, ResubmitFollowerEntry@8515, CancelStaleCascadeTgtDrag@8551, IsPttDragOrderCancellable@8821 — none caused by this change). MoveStopToBreakEven CCN=**6** (no warning). | PASS (pre-existing, no regression) |
| SCAN-8 | `lock\s*(` in src/PropTraderTools/ | `Select-String -Path "src/PropTraderTools/*.cs","src/PropTraderTools/Features/*.cs" -Pattern "lock\s*\("` | 0 actual lock() blocks (all matches are comment references "no lock()") | **PASS** |
| SCAN-9 | ASCII-only bytes in CopyEngine.cs | `[System.IO.File]::ReadAllBytes(...) | Where-Object { $_ -gt 127 }` | **0 non-ASCII bytes** | **PASS** |
| SCAN-10 | `dotnet build` | `dotnet build universal-or-strategy.sln` | **Build succeeded. 0 Warning(s). 0 Error(s).** | **PASS** |

---

## Supplementary Role-Definition Scans (7 mandatory scans from role definition)

| # | Scan | Command | Result | Status |
|---|------|---------|--------|--------|
| SD-1 | `lock(` | `Select-String ... -Pattern "lock\s*\("` | 0 actual lock() blocks | PASS |
| SD-2 | Non-ASCII bytes | `[System.IO.File]::ReadAllBytes(...)` > 127 | 0 bytes | PASS |
| SD-3 | FontFamily | `Select-String ... -Pattern "FontFamily"` | 0 code matches (3 comment references only) | PASS |
| SD-4 | Hex color literals | `Select-String ... -Pattern "#[0-9A-Fa-f]{6}"` | 0 matches | PASS |
| SD-5 | CreateOrder "PTT-" prefix | No new CreateOrder calls introduced | N/A — no change | PASS |
| SD-6 | DateTime.Now | `Select-String ... -Pattern "DateTime\.Now[^U]"` | 0 actual usage (7 comment references "No DateTime.Now" only) | PASS |
| SD-7 | lock\s*\( | Covered by SD-1 | 0 actual lock() blocks | PASS |

---

## Architecture-Locked Constraints Verified

- [x] `MoveStopToBreakEven` method signature unchanged
- [x] `while (targets.Count > 3)` block fully removed (comment + while + RemoveAt)
- [x] Replacement comment exactly: `// No ceiling -- SnapshotBeTargets DeduplicateByPrice handles residue inflation.`
- [x] `DeduplicateByPrice` not modified (residue guard remains upstream in SnapshotBeTargets)
- [x] This closes DW-B107 (residue-protection ceiling guard is now obsolete)
- [x] No new NT8 API calls introduced
- [x] No threading changes — method operates on caller-scoped snapshot list
- [x] ASCII-only in all literals and identifiers
- [x] No lock()
- [x] No DateTime.Now
- [x] No StrategyBase-only APIs

---

## xUnit Tests Required (AC-06 through AC-07)

The following tests are specified by the ticket and must pass against this implementation:

| Test | Asserts | Expected |
|------|---------|----------|
| `MoveStopToBreakEven_WithFiveTargets_ProcessesAllFive` | With snapshot of 5 targets, all 5 are processed; none are dropped (new capability, previously capped at 3) | PASS |
| `MoveStopToBreakEven_WithThreeTargets_UnchangedBehavior` | With snapshot of 3 targets, all 3 are processed; regression — existing 3-target path unaffected | PASS |

---

## VERDICT: BUILD_PASS
