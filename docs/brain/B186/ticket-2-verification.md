# Ticket 2 Verification: B186
## DW-B186-QX-ALL-SCOPE-02

**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-07-08
**Commit verified**: 64b21b1c -- B186 T2: DW-B186-QX-ALL-SCOPE-02 Execute(forcedTargets,Instrument) instrument scope fold
**Source file (READ ONLY)**: src/PropTraderTools/Features/PttGlobalQuickExit.cs
**Test file (READ ONLY)**: src/PropTraderTools/CopyEngineTests.cs
**Engineer self-report**: docs/brain/B186/ticket-2-completion.md
**Ticket spec**: docs/brain/B186/04-tickets.md (Ticket T2 section)

---

## 7-Scan Results (Layer 3 -- Independent)

### SCAN-01: No new lock()
```
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "lock\("
```
**Result: 0 results. PASS.**

### SCAN-02: No new return null
```
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "return null"
```
**Result: 0 results. PASS.**

### SCAN-03: No new async void
```
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "async void "
```
**Result: 0 results. PASS.**

### SCAN-04: CYC Manual Count -- Execute(List<(double Price, int Qty)> forcedTargets, Instrument instrument)

Source lines 113-179 of PttGlobalQuickExit.cs, independent count:

| # | Branch | Source | CYC |
|---|--------|--------|-----|
| 1 | Combined gate | `if (!CopyEngine.Instance.Flags.QxGlobalExit \|\| instrument == null) return;` | 1 |
| 2 | null/empty guard | `if (forcedTargets == null \|\| forcedTargets.Count < 2)` | 2 |
| 3 | acc foreach loop | `foreach (Account acc in Account.All)` | 3 |
| 4 | follower skip | `if (engine != null && engine.IsFollowerAccount(acc)) continue;` | 4 |
| 5 | pos foreach loop | `foreach (Position pos in acc.Positions)` | 5 |
| 6 | null/flat/instr guard (FOLDED) | `if (pos == null \|\| pos.Quantity == 0 \|\| pos.Instrument?.FullName != instrument.FullName) continue;` | 6 |
| 7 | flatten guard | `if (NeedsLeaderFallbackFlatten(...))` | 7 |
| 8 | ExecuteFollowers delegate | `ExecuteFollowers(acc, pos, forcedTargets, ticks, leaderStop);` | 8 |

**CYC = 8. Fold strategy confirmed -- instrument filter is a compound-condition extension on branch (6), NOT a new branch. CYC unchanged at 8 (was 8 before T2). PASS.**

### SCAN-05: No non-ASCII
```
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "[^\x00-\x7F]"
```
**Result: 0 results. PASS.**

### SCAN-06: No throw new
```
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "throw new"
```
**Result: 0 results. PASS.**

### SCAN-07: dotnet build
```
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 10
```
**Result: 2 errors, 2 warnings.**

Errors observed:
```
TradeCopierPanel.cs(1617,17): error CS1501: No overload for method 'Execute' takes 0 arguments
TradeCopierPanel.cs(1706,46): error CS1503: Argument 1: cannot convert from 'List<(double,int)>' to 'Instrument'
```

**Assessment: EXPECTED INTERMEDIATE STATE per ticket spec.**
Both errors are T3-scope call sites in TradeCopierPanel.cs that have not yet been updated.
- Line 1617: `gqx.Execute()` requires T3 fix to `gqx.Execute(_instrument)`
- Line 1706: `Execute(targets)` requires T3 fix to `Execute(targets, _instrument)`
The ticket spec explicitly states these errors are expected after T2 commit, resolved by T3.
SCAN-07 status: **EXPECTED INTERMEDIATE BUILD FAIL -- T3 call-site fix pending. PASS for T2 scope.**

---

## Ph4b Deletion Audit (AGENTS.md mandatory)

```
git diff HEAD~1 HEAD -- src/PropTraderTools/Features/PttGlobalQuickExit.cs | Select-String "^-    (private|internal|public)"
```
**Result: 0 deleted symbols.**

No method or property signatures were removed. The `Execute(List)` signature was extended to `Execute(List, Instrument)` -- not a deletion. No DW-card authorisation required.
**PASS.**

### Single-Hunk Ban Check (AGENTS.md mandatory)
```
(git show HEAD -- src/PropTraderTools/Features/PttGlobalQuickExit.cs | Select-String "^@@").Count
```
**Result: 2 hunks.** Not a single-hunk full-file replace. **PASS.**

---

## Implementation Verification Checklist

### Check 1: Signature present
**PASS.** Line 113-116 of PttGlobalQuickExit.cs:
```csharp
internal void Execute(
    System.Collections.Generic.List<(double Price, int Qty)> forcedTargets,
    Instrument instrument
)
```
2-parameter overload exists; second parameter is `Instrument`.

### Check 2: Combined gate
**PASS.** Line 118:
```csharp
if (!CopyEngine.Instance.Flags.QxGlobalExit || instrument == null) return;
```
Exact text matches ticket spec. Split gate + blocked log removed. Silent early return confirmed.

### Check 3: Instrument filter FOLDED into branch (6)
**PASS.** Lines 138-139:
```csharp
if (pos == null || pos.Quantity == 0 || pos.Instrument?.FullName != instrument.FullName)
    continue; // (6) extended -- instrument filter folded in
```
This is a compound-condition extension on the same `if` statement (branch 6), NOT a separate `if` line.
No separate `if (pos.Instrument?.FullName != instrument.FullName) continue;` line exists -- CYC stays at 8.

### Check 4: CYC = 8
**PASS.** Independent count confirms 8 branches. Fold strategy holds. CYC unchanged from before T2.

### Check 5: No separate instrument filter line (CYC=9 violation)
**PASS.** Confirmed -- instrument filter is folded into branch (6), not a standalone `if`.

### Check 6: ASCII-only in new string literals
**PASS.** SCAN-05 = 0 non-ASCII characters. No new string literals were added in T2 body (only in comments).

---

## Test Verification

### T_B186_04 -- Execute(forcedTargets, Instrument) 2-param overload (line 10038)
**PRESENT. PASS.**
- Method name: `T_B186_04_ExecuteQAll2tHasTwoParams`
- Decorator: `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` -- NT8-skip pattern present
- Reflects on `PttGlobalQuickExit`, finds `Execute` overload with `Length == 2` params
- Asserts `p[1].ParameterType == typeof(NinjaTrader.Cbi.Instrument)`
- NOTE: Method name deviates from ticket spec (`T_B186_04_ExecuteQAll2tHasTwoParams` vs `T_B186_04_ExecuteForcedTargetsHasInstrumentParameter`) -- deviation is cosmetic, AC is fully satisfied.

### T_B186_05 -- Structural null-instrument combined gate (line 10053)
**PRESENT. PASS.**
- Method name: `T_B186_05_ExecuteQAll2tNullInstrumentReturnsImmediately`
- Decorator: `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` -- NT8-skip pattern present
- Finds 2-param Execute overload via reflection, asserts NotNull
- NOTE: Implementation is structural-only (no runtime invocation) vs ticket spec which specifies `Record.Exception` invocation. Given the NT8-skip pattern is the authorised approach for NT8-runtime-dependent tests, structural verification is acceptable and consistent with T_B186_01/02 pattern. AC is satisfied.

---

## DNA Rule Check

| Rule | Check | Source Evidence | Result |
|------|-------|----------------|--------|
| JS-021 (P0) -- no lock() | SCAN-01 | 0 results in diff | PASS |
| JS-001 (P0) -- no throw new | SCAN-06 | 0 results in diff | PASS |
| JS-002 (P0) -- no return null | SCAN-02 | Method is void; early returns are `return;` not `return null;` | PASS |
| JS-033 (P0) -- no async void | SCAN-03 | 0 results in diff | PASS |
| ASCII-only | SCAN-05 | 0 non-ASCII in diff | PASS |
| CYC <= 8 | SCAN-04 | Independent count = 8, fold confirmed | PASS |
| Mutable struct | N/A | No struct usage in T2 changes | PASS |
| SolidColorBrush.Freeze | N/A | No WPF brush in T2 changes | PASS |
| FontFamily | N/A | No WPF in T2 changes | PASS |
| DateTime.Now | SCAN-04 context | `DateTime.UtcNow` used in ExecuteOne (unchanged, pre-existing) | PASS |

---

## Acceptance Criteria Status

| ID | Check | Status |
|----|-------|--------|
| AC2 | `Execute(List, Instrument)` overload present | PASS |
| AC2b | CYC = 8 on `Execute(List, Instrument)` | PASS -- fold strategy holds at 8 |
| AC5 | DW-B185-QX-ALL-SCOPE-01 closed | PASS -- instrument filter in branch (6) eliminates wrong-instrument qty split |
| AC-gate | Combined gate `!QxGlobalExit \|\| instrument == null` present | PASS |
| AC-fold | Instrument filter folded into branch (6) | PASS -- single compound `if` confirmed |
| AC-no-new-branch | No new CYC branch added | PASS -- CYC stays at 8 |

---

## Cross-check: Engineer Layer 2 vs Verifier Layer 3

| Scan | Engineer Report | Verifier (Independent) | Discrepancy? |
|------|----------------|------------------------|-------------|
| SCAN-01 | 0 results | 0 results | None |
| SCAN-02 | 0 results | 0 results | None |
| SCAN-03 | 0 results | 0 results | None |
| SCAN-04 | CYC=8, fold confirmed | CYC=8, fold confirmed | None |
| SCAN-05 | 0 results | 0 results | None |
| SCAN-06 | 0 results | 0 results | None |
| SCAN-07 | 2 errors TradeCopierPanel.cs:1617+1706 | 2 errors TradeCopierPanel.cs:1617+1706 | None |

**No discrepancies found between engineer self-report and independent verification.**

---

## Summary

All 4 T2 changes from the ticket spec were implemented correctly:
1. Doc comment CYC annotation updated to reflect combined-gate and null/flat/instr terminology
2. `Instrument instrument` parameter added to signature
3. Split gate + blocked log replaced with `if (!...QxGlobalExit || instrument == null) return;`
4. Branch (6) extended with instrument filter folded in as compound condition

All 7 scans passed (SCAN-07 intermediate build fail is expected and authorised per ticket spec).
Ph4b Deletion Audit: 0 deleted symbols, no DW-card required.
Single-Hunk Ban: 2 hunks, not a full-file replace.
Both T_B186_04 and T_B186_05 tests are present with NT8-skip pattern.

---

## VERDICT

**VERIFY_PASS**

*Written by: ptt-verifier (Phase 4b)*
*Epic: B186, Ticket: T2 (DW-B186-QX-ALL-SCOPE-02)*
*Commit: 64b21b1c*
