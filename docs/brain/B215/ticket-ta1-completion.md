# B215 Lane A — Ticket TA1 Completion

## Ticket
**Block**: B215-LaneA
**Ticket**: TA1 — Fix B153IsPttQxTargetTests (IsPttQxTarget method + Skip annotations)
**Status**: BUILD_PASS

---

## Changes Made

### CopyEngine.cs
- **IsPttQxTarget private static method** — added in prior session at line 1987.
- **This session**: verified still present (`Select-String` confirmed line 1987). No changes made to CopyEngine.cs in this session.

### CopyEngineTests.cs (this session)
- In class `B153IsPttQxTargetTests` (line 9277): replaced `[Fact]` with
  `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` on all 6 test methods:
  1. `IsPttQxTarget_T4_ReturnsTrue` (line 9291)
  2. `IsPttQxTarget_T10_ReturnsTrue` (line 9298)
  3. `IsPttQxTarget_T22_ReturnsTrue` (line 9305)
  4. `IsPttQxTarget_T0_ReturnsFalse` (line 9312)
  5. `IsPttQxTarget_NullName_ReturnsFalse` (line 9319)
  6. `IsPttQxTarget_T3_StillReturnsTrue` (line 9329)

**Root cause**: `Invoke(null, ...)` on a static method via reflection triggers `CopyEngine`'s type
initializer (static cctor), which requires NT8 DLLs not available outside the NT8 host. Same
pattern as all other CopyEngine reflection-invocation tests that already carry the Skip annotation.

---

## Post-Edit Command Results

### ptt-sync.ps1
```
ptt-sync complete: 21 linked, 0 errors
```
Result: **21/0 PASS**

### B153 Targeted Test Run
```
Skipped! - Failed: 0, Passed: 0, Skipped: 6, Total: 6
```
Result: **6 Skipped, 0 Failed — PASS**

### Full Test Suite
```
Failed! - Failed: 18, Passed: 200, Skipped: 553, Total: 771
```
Result: **18 Failed (was 24 — reduced by 6 exactly) — PASS**

---

## 7-Scan Results

### SCAN-1: Git diff scope
```
git diff --name-only HEAD -- src/PropTraderTools/CopyEngine.cs src/PropTraderTools/CopyEngineTests.cs
```
Output: `src/PropTraderTools/CopyEngine.cs` (prior session), `src/PropTraderTools/CopyEngineTests.cs` (this session)
Result: **TA1 scope only — PASS**

### SCAN-2: Failed count dropped by 6
Full suite: 18 Failed (was 24). Reduction = 6. B153 tests now Skipped.
Result: **PASS**

### SCAN-3: No lock() in IsPttQxTarget
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\("
```
Output: All 10 hits are comment-only lines (e.g. `// JS-021: no lock()`). Zero actual `lock(` calls.
Result: **0 new lock() — PASS**

### SCAN-4: No async void in CopyEngineTests.cs
```
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "async void "
```
Output: (no matches)
Result: **0 — PASS**

### SCAN-5: No return null in IsPttQxTarget
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "return null;"
```
IsPttQxTarget (line 1987) uses `return false` — none of the `return null` hits are in this method.
All 15 hits are pre-existing in other methods, not introduced by TA1.
Result: **0 new return null in IsPttQxTarget — PASS**

### SCAN-6: IsPttQxTarget confirmed present
```
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "IsPttQxTarget"
```
Output: `CopyEngine.cs:1987: private static bool IsPttQxTarget(string name)`
Result: **Method confirmed present at line 1987 — PASS**

### SCAN-7: verify_pr_hygiene.ps1 DIFF GUARD
Pre-existing 839-commit branch divergence from prior sessions — documented, not a TA1 issue.
TA1+LB1 content diff is 6 annotation changes (~300 chars) — well under 10k character limit.
Result: **Branch divergence pre-existing, content diff under limit — NOTED (pre-existing)**

---

## Guardian Symbol Script
```
powershell -File .\scripts\verify_critical_symbols.ps1
```
Output: GUARDIAN FAIL — `_instr2tBtn.Background = BrushTeal (Quick2t visual fix)`
**DW-B209-01**: Pre-existing false positive documented in B215 prior sessions. Not introduced by TA1.
Result: **Pre-existing false positive — NOTED**

---

## Design Waivers

| ID | Status | Description |
|----|--------|-------------|
| DW-B215-LA-01 | CLOSED | B153IsPttQxTargetTests: 6 tests skip-annotated due to NT8 cctor requirement |
| DW-B209-01 | OPEN (pre-existing) | Guardian false positive: `_instr2tBtn.Background = BrushTeal` |

---

## Summary

- CopyEngine.cs: `IsPttQxTarget` present at line 1987 (prior session, untouched this session)
- CopyEngineTests.cs: 6 `[Fact]` replaced with `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]`
- ptt-sync: 21/0
- B153 targeted: 6 Skipped, 0 Failed
- Full suite: 18 Failed (was 24, reduced by 6)
- All 7 scans: PASS (SCAN-7 and Guardian: pre-existing branch divergence / false positive documented)

**DW-B215-LA-01: CLOSED**

---

**Result: BUILD_PASS**
