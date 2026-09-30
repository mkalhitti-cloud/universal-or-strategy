# BWAVE-CYC Lane A PR #36 Repair -- Plan Review

**Date**: 2026-09-03
**Reviewer**: ptt-plan-reviewer
**Plan file**: docs/brain/BWAVE-CYC/LaneA-PR36-repair/02-architecture-plan.md
**Phase**: Phase 2 (Plan Review gate)

---

## Known Baseline (Accepted by Director)

| Item | Status |
|------|--------|
| NT8-runtime pre-existing test failures | 80 -- accepted by Director |
| 10k diff waiver | Approved for PR #36 |
| Greptile check | SUCCESS on PR #36 |
| CodeRabbit state | CHANGES_REQUESTED on PR #36 |

---

## LANE-SPLIT GATE Check

**Required statement**: `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE`
**Found at**: Plan line 8 -- `**LANE-SPLIT GATE RESULT**: SINGLE-PIPELINE`
**Result**: PASS

---

## Per-Ticket Review

### TICKET A-1: ASCII violation -- buffered button arrows

| Check | Result | Notes |
|-------|--------|-------|
| Correct file identified | PASS | `TradeCopierPanel.cs` -- correct |
| Specific line numbers provided | PASS | 12 lines (1147, 1153, 1184, 1190, 1226, 1232, 1265, 1271, 1311, 1317, 1350, 1356) with exact text |
| Rule citation correct | **FAIL** | Plan cites **JS-006** ("ASCII-only identifiers and string literals"). JS-006 in RULES_CATALOG.md is "Use Phantom Types for Units" -- an entirely unrelated rule. The ASCII-only mandate is a V12 DNA / AGENTS.md Section 2 project rule ("ASCII-Only Compliance: NEVER use Unicode, emoji, or curly quotes in C# string literals"). It has no JS-XXX catalog number. Citation is factually wrong. |
| Verification method documented | PASS | `Select-String` scan lines 1130-1400, expected 0 results |
| Out-of-scope Unicode documented | PASS | Pre-existing lines (toggle/chevron/QX-2T) listed with Director waiver |

**Ticket A-1 verdict**: FAIL -- wrong rule citation (JS-006 cited; actual rule is V12 DNA ASCII-Only Compliance per AGENTS.md, no JS-XXX catalog entry)

---

### TICKET A-2: Misplaced TA-R9 test block in CopyEngineTests.cs

| Check | Result | Notes |
|-------|--------|-------|
| Correct file identified | PASS | `CopyEngineTests.cs` -- correct |
| Specific line numbers provided | PASS | Lines 7181-7395 with exact start/end markers |
| Rule rationale correct | PASS | CS0103 compile errors (`_engine`, `GetField` don't exist in class); canonical tests in `BwaveCycLaneAR9Tests.cs` |
| Verification method documented | PASS | `Select-String` for `_engine.SetEnabled` / `GetField(` within class context, expected 0 |
| A-6 interaction documented | PASS | Lines 7364-7395 (FindPositionForInstrument tests) are inside removed block; A-6 does NOT re-add to this file |

**Ticket A-2 verdict**: PASS

---

### TICKET A-3: Vacuous test assertions (swallowed exceptions)

| Check | Result | Notes |
|-------|--------|-------|
| Correct files identified | PASS | `CopyEngineTests.cs` and `BwaveCycLaneAR9Tests.cs` |
| Location identified | PASS | Method name `TryCancelOrders_ShouldNotThrow_WhenStaleListIsEmpty` in CopyEngineTests.cs (line shift noted after A-2); `T_R9_09_TryCancelOrders_EmptyList_DoesNotThrow` at lines 141-154 in BwaveCycLaneAR9Tests.cs |
| Rule/rationale correct | PASS | Test integrity -- `try/catch(TargetInvocationException){}` inside `Record.Exception` swallows exceptions, making `Assert.Null(ex)` vacuous; fix removes inner try/catch |
| Old and new body both documented | PASS | Both instances provide exact before/after code |
| Verification method documented | PASS | `Select-String` for `TargetInvocationException` in both files, expected 0 results |

**Ticket A-3 verdict**: PASS

---

### TICKET A-4: SA1507/SA1508 StyleCop violations (NO-OP)

| Check | Result | Notes |
|-------|--------|-------|
| Correct file identified | PASS | `CopyEngineTests.cs` |
| NO-OP rationale documented | PASS | CSharpier commit `2270c544` already resolved all SA1507/SA1508 violations; current HEAD confirmed clean |
| Verification scan provided | PASS | PowerShell SA1507/SA1508 detection script with expected count 0 |
| Engineer action documented | PASS | "Document result as CONFIRMED-ALREADY-FIXED. No source edit required." |

**Ticket A-4 verdict**: PASS

---

### TICKET A-5: Teal button background regression (NO-OP)

| Check | Result | Notes |
|-------|--------|-------|
| Correct file identified | PASS | `TradeCopierPanel.cs` |
| NO-OP rationale documented | PASS | `BuildArrowCluster` does not exist in current HEAD; inline `BuildBufferedButtonsRow` replaced it; bug does not exist in current code |
| Trim/Flatten buttons still receive BrushInactive | PASS | Plan confirms `_trimBtn2` and `_flattenBtn2` have `Background = BrushInactive`; teal buttons (`_beBtn2`, `_globalBeBtn2`, `_quickBtn`, `_quickAllBtn`) have no Background (correct for NTButtonStyle default) |
| Null-conditional assignment pattern | PASS | The original bug (unconditional `mainBackground` assignment to teal buttons) does not exist; confirmed by source-read of lines 1245-1375 |
| Verification scan provided | PASS | `Select-String` for `BuildArrowCluster`, expected 0 results |

**Ticket A-5 verdict**: PASS

---

### TICKET A-6: JS-002 -- TryFindPositionForInstrument (bool+out pattern)

| Check | Result | Notes |
|-------|--------|-------|
| Correct files identified | PASS | `CopyEngine.cs` (add method), `BwaveCycLaneAR9Tests.cs` (update T_R9_10, T_R9_11); `CopyEngineTests.cs` covered by A-2 removal |
| JS-002 compliance | PASS | Signature `bool TryFindPositionForInstrument(..., out Position pos)` -- returns `bool`, no `return null`. `pos = null` is required `out` parameter initialization per C# spec, NOT a `return null` violation |
| Signature matches JS-002 (bool+out) | PASS | `private static bool TryFindPositionForInstrument(Account acc, NinjaTrader.Cbi.Instrument instr, out NinjaTrader.Cbi.Position pos)` -- correct TryXxx pattern |
| CYC documented | PASS | CYC=3 (base 1 + foreach 1 + inner null-guard 1) -- within CYC<=8 limit |
| Caller update plan | PASS | Original caller in `SubmitBeStop` was already removed with the method; plan verifies no old name remains via scan; no callers to update in production |
| Test T_R9_10 update | PASS | Old: 2 params, name `FindPositionForInstrument`. New: `ReturnType=typeof(bool)`, 3 params, name `TryFindPositionForInstrument` |
| Test T_R9_11 update | PASS | Old: 2 params, `acc`+`instr`. New: 3 params adds `pos` with `IsOut` assertion |
| Ordering (A-2 before A-6) | PASS | Explicitly mandated: "A-2 must be executed BEFORE A-6 to avoid conflicts" |
| Verification scans (4 steps) | PASS | Confirms new name present, old name absent in production; tests updated; old test name absent |
| Pre-existing `FindBePosition` null return | PASS | Acknowledged as pre-existing JS-002 site; Director accepted in TA-R9 verify; SCAN-03 confirms count unchanged |

**Ticket A-6 verdict**: PASS

---

## Cross-File Concerns

### A-6 Rename Coverage

| Scope | Covered? | Notes |
|-------|----------|-------|
| `CopyEngine.cs` production callers | PASS | No callers -- method was removed with original implementation; verification scan confirms old name absent |
| `CopyEngineTests.cs` test references | PASS | Lines 7364-7395 removed by A-2; no further update needed |
| `BwaveCycLaneAR9Tests.cs` | PASS | T_R9_10 and T_R9_11 updated to `TryFindPositionForInstrument` |
| Other test files | PASS (no concern) | Plan does not reference other test files; `FindPositionForInstrument` was only in the two test locations identified |

### A-5 Trim/Flatten Button Regression

| Concern | Result | Notes |
|---------|--------|-------|
| Trim button still gets BrushInactive | PASS | `_trimBtn2` confirmed `Background = BrushInactive` in current inline code |
| Flatten button still gets BrushInactive | PASS | `_flattenBtn2` confirmed `Background = BrushInactive` in current inline code |
| Teal buttons unaffected | PASS | `_beBtn2`, `_globalBeBtn2`, `_quickBtn`, `_quickAllBtn` -- no Background set; NTButtonStyle default applies |

---

## 7-Scan Checklist

| Scan | Present | Pattern | Expected Result |
|------|---------|---------|----------------|
| SCAN-01: `lock()` | PASS | `lock\s*\(` in 4 files | 0 results |
| SCAN-02: `async void` | PASS | `async void ` in CopyEngine.cs + TradeCopierPanel.cs | 0 results |
| SCAN-03: `return null` count | PASS | Count unchanged from baseline; `pos = null` correctly excluded | Count unchanged |
| SCAN-04: `throw new` | PASS | 0 new in repair-modified files; pre-existing 2 noted | 0 new |
| SCAN-05: build | PASS | `dotnet build` 0 errors | 0 errors |
| SCAN-06: ASCII repair scope | PASS | `\u25B[23]` in lines 1130-1400 | 0 results |
| SCAN-07: `dotnet test` | PASS | Failed count = 80 (pre-existing accepted) | No new failures |

**7-scan checklist**: ALL 7 scans present and complete. PASS

---

## Violations Summary

| # | Rule | Location in Plan | Severity | Description |
|---|------|-----------------|----------|-------------|
| V-1 | JS-006 misattribution | A-1 rationale line | P1 | Plan cites JS-006 ("Use Phantom Types for Units") as the authority for the ASCII mandate. JS-006 in RULES_CATALOG.md (the coding bible per AGENTS.md V12.39) is an entirely unrelated type-safety rule. The actual rule governing ASCII compliance is the V12 DNA project mandate from AGENTS.md Section 2 ("ASCII-Only Compliance"). There is no JS-XXX catalog entry for ASCII-only compliance. The plan must cite the correct authority. |

---

## Final Verdict

**REVIEW_FAIL**

### Violation Requiring Fix Before REVIEW_PASS

> **V-1 — A-1 Rule Citation Error**
> Plan Ticket A-1 cites **JS-006** as the governing rule for the ASCII mandate.
> JS-006 in `docs/standards/jane-street/RULES_CATALOG.md` is **"Use Phantom Types for Units"** (type-safety rule for preventing unit mixing).
> The ASCII-only mandate is a V12 DNA / AGENTS.md Section 2 project rule:
> *"ASCII-Only Compliance: NEVER use Unicode, emoji, or curly quotes in C# string literals."*
> It has **no JS-XXX catalog entry** — it is cited by the project as a standalone DNA mandate.
> The plan must correct the rule citation in Ticket A-1 to reference the V12 DNA ASCII-Only Compliance mandate from AGENTS.md Section 2 (not JS-006).

### What Does NOT Need to Change

All other plan content is correct and well-structured:
- LANE-SPLIT GATE statement: present
- Tickets A-2 through A-6: logically sound, complete, correctly rule-cited
- 7-scan checklist: complete (all 7 scans present)
- Cross-file concerns: addressed
- NO-OP documentation for A-4 and A-5: correct
- A-2-before-A-6 ordering: explicitly mandated
- JS-002 compliance of TryFindPositionForInstrument (`pos = null` is out-param init, not `return null`): correct

### Cycle Count

This is review cycle 1 of maximum 2 before escalation to Director.

---

*Review status: REVIEW_FAIL (1 violation) -- return to ptt-architect for correction of A-1 rule citation.*

---

## Cycle 2 Review

**Date**: 2026-09-03
**Reviewer**: ptt-plan-reviewer
**Cycle**: 2 of 2 (maximum)

---

### V-1 Resolution Check

| Item | Cycle 1 Finding | Cycle 2 Re-check | Result |
|------|----------------|-----------------|--------|
| A-1 Rule Citation | `JS-006` (wrong — Phantom Types) | `V12 DNA ASCII-Only Compliance (AGENTS.md §2)` | RESOLVED ✓ |

Plan line 56: `**Rule**: V12 DNA ASCII-Only Compliance (AGENTS.md §2) — "NEVER use Unicode, emoji, or curly quotes in C# string literals"`
Plan line 57: `**Source**: CodeRabbit CR36-3 + Greptile P2. Authority: V12 DNA mandate (AGENTS.md §2, Architectural Mandates).`

`JS-006` is absent from Ticket A-1. Correct authority cited. **V-1 RESOLVED.**

---

### New Violation Scan (DNA Block — Full Pass)

| Category | Rule | Check | Result |
|----------|------|-------|--------|
| Concurrency P0 | JS-021 `lock()` | No `lock()` proposed; A-6 comment explicitly notes JS-021 compliant via NT8 read-only Positions collection | PASS |
| Concurrency P0 | JS-023 off-thread UI | No UI updates proposed | PASS |
| Type Safety P0 | JS-001 throw in gate chain | No `throw` proposed anywhere | PASS |
| Type Safety P0 | JS-002 null return | A-6 returns `bool`; `pos = null` is mandatory C# `out`-param initializer, not `return null` | PASS |
| Type Safety P0 | JS-003 magic string | No discriminated-union patterns introduced | PASS |
| Immutability P1 | JS-008 mutable struct | No new struct types introduced | PASS |
| Immutability P1 | JS-009 Dictionary shared | No new collections introduced | PASS |
| Construction P1 | JS-010 public constructor on singleton | No new types introduced | PASS |
| NT8 hard | async/await in lifecycle | Not proposed | PASS |
| NT8 hard | Account.All in constructor | Not referenced | PASS |
| NT8 hard | sealed TradeCopierWindow | Not touched | PASS |
| NT8 hard | FontFamily override | Not proposed | PASS |
| NT8 hard | Hardcoded #RRGGBB hex | Not proposed | PASS |
| NT8 hard | CreateOrder without PTT- prefix | Not proposed | PASS |
| NT8 hard | DateTime.Now | Not proposed | PASS |
| Complexity P1 | CYC > 8 | A-6 new method: CYC=3 (documented in plan); no other new methods | PASS |
| Spec P0 | All PR #36 findings addressed | A-1 through A-6 cover all CodeRabbit/Greptile/CodeFactor findings | PASS |

**Zero new violations introduced.** ✓

---

### Previously-Passing Checks Re-Verification

| Check | Location in Plan | Result |
|-------|-----------------|--------|
| LANE-SPLIT GATE: "SINGLE-PIPELINE" present | Line 8 | PASS |
| A-2 block delete (lines 7181–7395 with start/end markers) | Lines 122–148 | PASS |
| A-3 swallowed-exception fix (2 instances, before/after) | Lines 177–225 | PASS |
| A-4 NO-OP confirmation with verification script | Lines 250–269 | PASS |
| A-5 NO-OP confirmation with verification script | Lines 279–298 | PASS |
| A-6 TryFindPositionForInstrument (bool+out, JS-002) | Lines 330–346 | PASS |
| A-2 before A-6 ordering explicitly mandated | Lines 46–48, 438–439 | PASS |
| 7-scan checklist (SCAN-01 through SCAN-07) | Lines 457–506 | PASS |
| A-6 rename caller coverage (all 3 files) | Lines 349–357, 437–443 | PASS |
| Trim/Flatten BrushInactive preservation (A-5) | Lines 284–290 | PASS |

**All 10 previously-passing checks: still PASS.** ✓

---

### Cycle 2 Final Verdict

**REVIEW_PASS**

All violations from Cycle 1 have been corrected. No new violations introduced. All spec requirements addressed. The plan is approved to proceed to Phase 3 (ticket generation).

*Review status: REVIEW_PASS — ptt-architect may proceed to 04-tickets.md.*
