# Ticket TA5 Verification Report

## Block: B215-LaneA
## Ticket: TA5 — Fix LaneFTests (T_B188 subset) needle strings in CopyEngineTests.cs
## Verifier: PTT Verifier (Phase 4b)
## Status: VERIFY_PASS

---

## Layer 2 vs Layer 3 Comparison Table

| Scan | Engineer (Layer 2) | Verifier (Layer 3) | Match? |
|------|-------------------|-------------------|--------|
| SCAN-1: diff scope | CopyEngineTests.cs only (TA5 changes) | Confirmed: 6 lines changed in T_B188 region (lines 9171-9218); additional hunks are pre-existing dirty-tree changes from prior tickets | PASS |
| SCAN-2: dotnet test T_B188_01/02/03 | Failed=0, Passed=3 | Failed=0, Passed=3, Duration=176ms | MATCH |
| SCAN-3: lock() in lines 9165-9225 | 0 matches | 0 matches | MATCH |
| SCAN-4: async void in lines 9165-9225 | 0 matches | 0 matches | MATCH |
| SCAN-5: return null; in lines 9165-9225 | 0 matches | 0 matches | MATCH |
| SCAN-6: old needles gone, new needles present | forcedTargets needle=0, targets needle=0; perAccTargets/effectiveTargets confirmed | Same — 0 const needle forcedTargets hits; 6 new string matches at correct lines | MATCH |
| SCAN-7: PR hygiene | Pre-existing branch divergence (75 ahead / 839 behind origin/main) | Same: FAIL: Branch is NOT based on the latest main — pre-existing, not caused by TA5 | MATCH |
| Full suite after TA5 | Failed=11, Passed=206, Skipped=554 | Not re-run (build flag --no-build); 3 target tests PASS confirmed independently | CONSISTENT |

---

## Content Verification — Each Fix

### Fix 1: T_B188_01 (line 9174)
- **Old needle (gone):** `"ExecuteOne(acc, pos.Instrument, ticks.t1, forcedTargets, leaderStop: leaderStop)"`
  - Verified: `Select-String -Pattern 'const string needle.*forcedTargets'` in lines 9165-9225 → **0 matches**
- **New needle (present):** `"ExecuteOne(acc, pos.Instrument, ticks.t1, perAccTargets, leaderStop: leaderStop)"` at line 9174
  - Verified: confirmed by direct file read and Select-String → **present**
- **Error message updated:** line 9182 now reads `"B188 fix: ExecuteOne call with perAccTargets must include 'leaderStop: leaderStop' in PttGlobalQuickExit.cs"` — **PASS**

### Fix 2: T_B188_02 (line 9192)
- **Old needle (gone):** `"ExecuteOne(acc, pos.Instrument, ticks.t1, targets, leaderStop: leaderStop)"`
  - Verified: `Select-String -Pattern '"targets, leaderStop'` in lines 9165-9225 → **0 matches**
- **New needle (present):** `"ExecuteOne(acc, pos.Instrument, ticks.t1, effectiveTargets,"` at line 9192
  - Verified: confirmed by direct file read and Select-String → **present**
- **Error message updated:** line 9200 now reads `"B188 fix: ExecuteOne call with effectiveTargets must be present in PttGlobalQuickExit.cs"` — **PASS**

### Fix 3: T_B188_03 (line 9210)
- **Old needle (gone):** `"ExecuteFollowers(acc, pos, forcedTargets, ticks, leaderStop)"`
  - Verified: `Select-String -Pattern 'const string needle.*forcedTargets'` in lines 9165-9225 → **0 matches**
- **New needle (present):** `"ExecuteFollowers(acc, pos, perAccTargets, ticks, leaderStop)"` at line 9210
  - Verified: confirmed by direct file read and Select-String → **present**
- **Error message updated:** line 9218 now reads `"B187 wiring: ExecuteFollowers(acc, pos, perAccTargets, ticks, leaderStop) must be present in PttGlobalQuickExit.cs"` — **PASS**

---

## PttGlobalQuickExit.cs Variable Name Confirmation

Source file: `src/PropTraderTools/Features/PttGlobalQuickExit.cs` (READ-ONLY verification)

| Variable | Line | Code | Present? |
|----------|------|------|---------|
| `effectiveTargets` | 94 | `var effectiveTargets = ResolveEffectiveTargets(targets, 3);` | YES |
| `effectiveTargets` | 95 | `ExecuteOne(acc, pos.Instrument, ticks.t1, effectiveTargets,` | YES |
| `perAccTargets` | 180 | `var perAccTargets = TradeCopierPanel.Build2TargetList(pos.Quantity);` | YES |
| `perAccTargets` | 181 | `ExecuteOne(acc, pos.Instrument, ticks.t1, perAccTargets, leaderStop: leaderStop);` | YES |
| `perAccTargets` | 182 | `ExecuteFollowers(acc, pos, perAccTargets, ticks, leaderStop); // (8)` | YES |
| `forcedTargets` | 115 | PARAMETER NAME only — expected, not removed | N/A (correct) |

**All needle targets exist in production source. Tests will find their target strings. PASS.**

Note: `forcedTargets` at line 98 of PttGlobalQuickExit.cs is the `Execute(Instrument)` overload calling
`ExecuteFollowers(acc, pos, targets, ticks, leaderStop)` — this is a different call site not targeted by T_B188_01/03.
The T_B188_04 test covers this separately and is not in TA5 scope.

---

## DNA Rule Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 lock() | No lock() in lines 9165-9225 | PASS — 0 matches |
| JS-001 throw new Exception | No throw in test-only changes | PASS — N/A (assertions only) |
| JS-002 return null | No return null in lines 9165-9225 | PASS — 0 matches |
| JS-033 async void | No async void in lines 9165-9225 | PASS — 0 matches |
| ASCII-only | All new strings: perAccTargets, effectiveTargets — pure ASCII | PASS |
| CYC <= 8 | No production code changed; test methods unchanged structurally | N/A — test-only change |
| DateTime.UtcNow | No DateTime usage in changed lines | N/A |
| FontFamily | No WPF in test file | N/A |
| #RRGGBB hex | No hex color literals in changed lines | N/A |
| PTT- prefix | No CreateOrder in test file | N/A |

---

## Architecture Compliance

- **File scope**: Only `src/PropTraderTools/CopyEngineTests.cs` modified by TA5 — correct per ticket spec.
- **No production code touched**: All changes are `const string needle` and `Assert.True` message string literals.
- **Spec requirement DW-B214-02 (LaneF/B188 subset)**: All 3 tests now pass; needles match actual variable names used in `PttGlobalQuickExit.cs` post-B191-DIRECT refactor.
- **Test method names unchanged**: Method names retain `forcedTargets` in their identifiers (correct — comment line 9168 and method name 9170 still reference `forcedTargets` as historical context; the spec allows this).
- **ptt-sync.ps1**: Engineer reported 21 linked, 0 errors — consistent with test-only change (no new production files).

---

## Pre-Existing Issues (NOT caused by TA5)

1. **Branch divergence**: Local `main` is 75 commits ahead / 839 commits behind `origin/main`. Pre-existing across all tickets in this session. DO NOT rebase per ticket directive.
2. **Multiple dirty files**: `CopyEngine.cs`, `TradeCopierAddOn.cs`, `TradeCopierPanel.cs`, etc. modified by prior tickets — outside TA5 scope.
3. **Remaining 11 test failures**: Pre-existing failures from other tickets (baseline was 14; TA5 eliminated 3; 11 remain). These are addressed by other LaneA/LaneB tickets.

---

## Final Verdict

**VERIFY_PASS**

All 3 T_B188 tests pass (0 failures). All needle strings correctly updated to match current
`PttGlobalQuickExit.cs` variable names (`perAccTargets`, `effectiveTargets`). All 7 scans pass
or are pre-existing/N/A. Zero DNA violations introduced. Spec requirements satisfied.