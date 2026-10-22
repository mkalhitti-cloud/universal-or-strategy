# Ticket TA2 Verification — B215 Lane A (Re-verification)

**Block**: B215-LaneA
**Ticket**: TA2 — Fix B155ContractTests (stale reflection strings)
**Epic**: B215 (Test-Debt Remediation)
**Verifier**: PTT Verifier (ptt-verifier mode)
**Date**: 2026-07
**Status**: VERIFY_PASS

---

## Summary

This is a re-verification run following the engineer's retry fix for the previous VERIFY_FAIL finding.
The prior violation was: `IsLeaderFlatDrainAbort` still present in a comment at CopyEngineTests.cs ~line 9514.
The engineer retry fixed that comment: changed `IsLeaderFlatDrainAbort` → `TryAbortLeaderFlatDrains`.

All TA2 requirements are now satisfied.

---

## What TA2 Fixed (both fixes)

**Fix 1** — `SubmitDrainedEntry_ShouldAbort_WhenAccountIsFlat`:
- GetMethod string: `"SubmitDrainedEntryTestable"` → `"SubmitDrainedEntry"` ✅ verified at line 9419
- Removed `| BindingFlags.Public` (NonPublic|Instance only) ✅ verified at line 9420
- Comment updated ✅ verified at line 9417

**Fix 2** — `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString`:
- ALL occurrences of `IsLeaderFlatDrainAbort` → `TryAbortLeaderFlatDrains` ✅ verified
  - Line 9514 comment: `// Tests that TryAbortLeaderFlatDrains (extracted from TryEvictFollowerBeSlot, DW-B156-DEFER-1)` ✅
  - Line 9521 comment: `// TryAbortLeaderFlatDrains exists as a private instance method (structural proxy).` ✅
  - Line 9523 GetMethod string: `"TryAbortLeaderFlatDrains"` ✅

---

## Layer 2 vs Layer 3 Comparison Table

| Scan | Layer 2 (Engineer Self-Report) | Layer 3 (Independent Run) | Match? |
|------|-------------------------------|---------------------------|--------|
| SCAN-1: Files touched | CopyEngineTests.cs in diff, line 9514 change only | CopyEngineTests.cs confirmed in git diff --name-only HEAD; multiple files in diff are pre-existing | PASS ✅ |
| SCAN-2: B155ContractTests | Failed: 0, Passed: 4 | Failed: 0, Passed: 4, Skipped: 0, Total: 4 | MATCH ✅ |
| SCAN-3: lock( in scope | 1 hit at line 9533 — in comment only, not executable code | 1 hit at line 9533: `// JS-001: no throw. JS-021: no lock(). ASCII-only.` — comment only | MATCH ✅ |
| SCAN-4: async void | 0 matches | 0 matches | MATCH ✅ |
| SCAN-5: return null; | Line 2988 pre-existing, not in TA2 scope | 1 hit at line 2988 — pre-existing, outside TA2 scope (lines 9413-9530) | MATCH ✅ |
| SCAN-6: IsLeaderFlatDrainAbort | 0 matches | 0 matches | MATCH ✅ |
| SCAN-7: PR hygiene | FAIL — pre-existing branch divergence 75 ahead/839 behind | FAIL — branch not based on latest main; pre-existing, exempt per ticket directive | MATCH ✅ |

---

## Content Verification

### Stale strings — GONE

| Stale String | Result |
|---|---|
| `IsLeaderFlatDrainAbort` anywhere in CopyEngineTests.cs | **0 matches** ✅ |
| `SubmitDrainedEntryTestable` anywhere in CopyEngineTests.cs | Not scanned separately but GetMethod at line 9419 reads `"SubmitDrainedEntry"` ✅ |
| `BindingFlags.Public` removed from Fix 1 GetMethod call | Line 9420: `System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance` — no Public flag ✅ |

### New strings — PRESENT

| New String | Line | Verified |
|---|---|---|
| `"SubmitDrainedEntry"` in GetMethod | 9419 | ✅ |
| `"TryAbortLeaderFlatDrains"` in GetMethod | 9523 | ✅ |
| Comment at 9514: `TryAbortLeaderFlatDrains` | 9514 | ✅ |
| Comment at 9521: `TryAbortLeaderFlatDrains exists as a private instance method` | 9521 | ✅ |

### Target methods in CopyEngine.cs — EXIST

| Method | CopyEngine.cs Line | Verified |
|---|---|---|
| `private void SubmitDrainedEntry(string acctKey)` | 7915 | ✅ |
| `private void TryAbortLeaderFlatDrains(OrderEventArgs e)` | 1928 | ✅ |

---

## Previous VERIFY_FAIL Finding

**Finding**: `IsLeaderFlatDrainAbort` in comment at CopyEngineTests.cs ~line 9514.

**Status**: **RESOLVED** ✅

Layer 3 independent scan result:
```
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "IsLeaderFlatDrainAbort"
→ 0 matches (no output)
```

Line 9514 now reads:
```csharp
// Tests that TryAbortLeaderFlatDrains (extracted from TryEvictFollowerBeSlot, DW-B156-DEFER-1)
```

---

## DNA Rule Check

All checks performed against lines 9413-9530 (TA2 scope):

| Rule | Check | Result |
|---|---|---|
| JS-021: No lock() | line 9533 hit is in a comment, not executable code; no lock() in TA2 scope | PASS ✅ |
| JS-001: No throw in dispatch/gate | No `throw new` in TA2 code changes | PASS ✅ |
| JS-002: No return null | `return null;` at line 2988 is pre-existing, outside TA2 scope | PASS ✅ |
| JS-033: No async void | 0 matches | PASS ✅ |
| SCAN-03: No FontFamily | N/A — test-only change, no WPF elements | PASS ✅ |
| SCAN-04: No hex color | N/A — no hex literals introduced | PASS ✅ |
| SCAN-06: DateTime.UtcNow | N/A — no DateTime usage introduced | PASS ✅ |
| ASCII-only | All strings: "SubmitDrainedEntry", "TryAbortLeaderFlatDrains" — ASCII only | PASS ✅ |
| CYC <= 8 | Test-only change, no production code modified | N/A ✅ |

---

## Architecture Compliance

- **File modified**: `src/PropTraderTools/CopyEngineTests.cs` only (test file) ✅
- **No production code modified**: CopyEngine.cs unchanged by TA2 ✅
- **Minimal change principle**: Only stale comment and stale GetMethod strings updated ✅
- **Spec requirement satisfied**: DW-B214-02 (B155 subset) — stale reflection assertions corrected ✅
- **Class under test scope**: `B155ContractTests` (4 tests, all passing) ✅
- **Per-ticket success gate**: `dotnet test --filter "FullyQualifiedName~B155ContractTests"` → Failed: 0, Passed: 4 ✅

---

## Pre-Existing Issues (Not Caused by TA2)

1. **Branch divergence**: Local main is 75 commits ahead / 839 behind origin/main. Per ticket directive and `docs/Branch Divergence — Root Cause & Status.md`, this is pre-existing and DO NOT rebase.
2. **return null; at line 2988**: Pre-existing in CopyEngineTests.cs, not introduced by TA2.
3. **Multiple modified files in git status**: Pre-existing work-in-progress from prior tickets (CopyEngine.cs, PttContracts.cs, PttBeSwapAll.cs, PttGlobalBreakEven.cs, TradeCopierAddOn.cs, TradeCopierPanel.cs, etc.) — not caused by TA2.

---

## Scan Results Summary (All 7 Scans)

| Scan | Command | Result | Status |
|------|---------|--------|--------|
| SCAN-1 | `git diff --name-only HEAD` | CopyEngineTests.cs present; multiple pre-existing files also in diff | PASS ✅ |
| SCAN-2 | `dotnet test --filter "FullyQualifiedName~B155ContractTests" --no-build` | Failed: 0, Passed: 4 | PASS ✅ |
| SCAN-3 | `Select-String lock\(` | 1 hit at line 9533 — comment only, not executable code | PASS ✅ |
| SCAN-4 | `Select-String "async void "` | 0 matches | PASS ✅ |
| SCAN-5 | `Select-String "return null;"` | 1 hit at line 2988 — pre-existing, outside TA2 scope | PASS ✅ |
| SCAN-6 | `Select-String "IsLeaderFlatDrainAbort"` | **0 matches** (previous VERIFY_FAIL finding: resolved) | PASS ✅ |
| SCAN-7 | `verify_pr_hygiene.ps1` | FAIL — pre-existing branch divergence, not caused by TA2, exempt | PASS (exempt) ✅ |

---

## Final Verdict

**VERIFY_PASS**

All TA2 requirements are satisfied:
- Both stale GetMethod strings corrected (`SubmitDrainedEntryTestable` → `SubmitDrainedEntry`, `IsLeaderFlatDrainAbort` → `TryAbortLeaderFlatDrains`)
- All occurrences of `IsLeaderFlatDrainAbort` removed from CopyEngineTests.cs (0 matches)
- Both target methods exist in CopyEngine.cs at their expected locations
- B155ContractTests: 4/4 passing, 0 failures
- DNA rules: all pass (test-only change, no production code touched)
- Previous VERIFY_FAIL finding: **RESOLVED**