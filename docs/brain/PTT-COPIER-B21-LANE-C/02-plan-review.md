# PTT-COPIER-B21-LANE-C Plan Review

**Epic**: PTT-COPIER-B21-LANE-C
**Spec**: DW-ATM-DROPDOWN-01
**Phase**: 2 (Plan Review)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-07-14
**Plan reviewed**: `docs/brain/PTT-COPIER-B21-LANE-C/02-architecture-plan.md`

---

## VERDICT: REVIEW_PASS

Zero violations found. All checks passed. Phase 3 (ticket generation) is unblocked.

---

## Spec Coverage Matrix

| # | Spec Requirement | Addressed? | Plan Section |
|---|-----------------|------------|--------------|
| 1 | Delete field `_atmTemplateCombo` | YES — line 160 | §Item 1 (plan line 59) |
| 2 | Delete field `_activeAtmTemplateName` | YES — line 161 | §Item 2 (plan line 67) |
| 3 | Delete method `BuildAtmTemplateRow()` | YES — lines 1405–1431 | §Item 4 (plan line 103) |
| 4 | Delete method `LoadAtmTemplates()` | YES — lines 1433–1451 | §Item 5 (plan line 120) |
| 5 | Delete method `OnAtmTemplateSelectionChanged()` | YES — lines 1453–1461 | §Item 6 (plan line 139) |
| 6 | Delete method `GetAtmTemplatesDirectory()` | YES — lines 1395–1403 | §Item 3 (plan line 86) |
| 7 | Delete call site in `BuildUI()` | YES — line 566 + adjacent comment | §Item 7 (plan line 156) |
| 8 | Delete call site in `OnLoaded()` | YES — line 459 | §Item 8 (plan line 173) |
| 9 | Delete header comment lines 51–57 | YES — lines 51–57 | §Item 9 (plan line 182) |

All 9 spec items covered with source-verified line numbers. **PASS**.

---

## Jane Street DNA Checks

| Rule ID | Rule Description | Result | Evidence in Plan |
|---------|-----------------|--------|-----------------|
| JS-021 | `lock()` banned | PASS | Plan line 16: 0 matches in file; removal adds 0. Plan line 275 confirms. |
| JS-033 | `async void` banned | PASS | Plan line 17: all removed methods are synchronous. Plan line 277 confirms. |
| JS-001 | No throw in hot paths | PASS | Plan line 277: removed methods had no throw; IO errors silently swallowed. |
| JS-002 | No `return null` | PASS | Plan line 278: removed methods return void or string, no null return. |
| JS-008 | Immutability / frozen brushes | N/A | No UI resource changes introduced. |
| JS-009 | No shared mutable Dictionary | N/A | No collection changes. |
| JS-010 | No public constructor on singleton | N/A | No constructor changes. |
| JS-023 | UI update via Dispatcher.InvokeAsync | PASS | Plan line 267: threading model unaffected; all removed methods were UI-thread-only. |

No P0 or P1 violations. **PASS**.

---

## Complexity Check (CYC ≤ 8)

All removed methods carry CYC annotations in the plan:

| Method | CYC (annotated) | Compliant? |
|--------|----------------|------------|
| `GetAtmTemplatesDirectory()` | CYC=1 | ✓ |
| `BuildAtmTemplateRow()` | CYC=1 | ✓ |
| `LoadAtmTemplates()` | CYC=3 | ✓ |
| `OnAtmTemplateSelectionChanged()` | CYC=2 | ✓ |

No new code is written. Net CYC change = 0. **PASS**.

---

## NT8 Compiler Rules Check

| Rule | Checked? | Result |
|------|---------|--------|
| NT8-003 (no `volatile double`) | Yes — plan line 279 | PASS — no volatile fields in ATM block |
| NT8 unused `using` tolerance | Yes — plan line 225 | Correctly deferred: `using System.IO` becomes unused but NT8/.NET 4.8 compiler accepts it without error; scope-capped per spec. |
| NT8 build gate (SCAN-07) | Yes — plan line 313 | `dotnet build` expected: 0 errors. |

**PASS**.

---

## Scope Creep Check

**File ownership**: `TradeCopierPanel.cs` only. No other file is modified.

- `using System.IO` removal explicitly deferred (plan lines 217–226). Correctly scoped out.
- No changes to `CopyEngine.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`, or test files.
- No changes to `ConcurrentQueue`, `Dispatcher`, or any order path.

**PASS** — single-file, removal-only. No scope creep.

---

## No-New-[Fact] Check

Plan lines 322–327 explicitly acknowledge that no new xUnit `[Fact]` tests are required, with four well-reasoned justifications:
1. No behavioral change to `CopyEngine` or any order path.
2. ATM template selection had zero test coverage (unwired dead code).
3. SCAN-01..05 + SCAN-07 form the verification contract.
4. Per-panel UI is not unit-testable without WPF infrastructure.

Matches spec directive: "No new `[Fact]` required." **PASS**.

---

## 7-Scan Contract Check

All 7 scans present with correct grep patterns and expected results (plan lines 288–316):

| Scan | Pattern | Target | Expected | Present in Plan? |
|------|---------|--------|----------|-----------------|
| SCAN-01 | `_atmTemplateCombo` | TradeCopierPanel.cs | 0 matches | YES |
| SCAN-02 | `_activeAtmTemplateName` | TradeCopierPanel.cs | 0 matches | YES |
| SCAN-03 | `BuildAtmTemplateRow` | TradeCopierPanel.cs | 0 matches | YES |
| SCAN-04 | `LoadAtmTemplates` | TradeCopierPanel.cs | 0 matches | YES |
| SCAN-05 | `OnAtmTemplateSelectionChanged` | TradeCopierPanel.cs | 0 matches | YES |
| SCAN-06 | `lock(` | TradeCopierPanel.cs | 0 matches | YES |
| SCAN-07 | `dotnet build` | solution | 0 errors | YES |

Complete and correct. **PASS**.

---

## Orphaned Caller Check

Plan lines 203–213 provide a full reference-site table for every removed symbol. Every reference is mapped to a line within the ATM block itself (being removed). The plan correctly concludes:

> "No orphaned callers. The ATM circuit is fully self-contained. After all 9 items are removed, the file will compile clean with zero references to any removed symbol."

Confirmed — no external callers in other methods or files. **PASS**.

---

## Minor Observation (Non-Blocking)

**Item 7 call-site line number uncertainty** (plan lines 163–171): The plan notes some ambiguity about whether the `// B11 T2: ATM template row` comment is at line 564 or 565 relative to the call at line 566. The plan correctly handles this by instructing the engineer to search for the comment text rather than relying solely on a line number. This is a sound approach and does not constitute a violation — the removal target is unambiguous. No violation.

---

## Summary

| Check | Result |
|-------|--------|
| All 9 spec items addressed | PASS |
| Exact line numbers provided | PASS |
| Scope: TradeCopierPanel.cs only | PASS |
| 7-scan contract complete and correct | PASS |
| No new [Fact] acknowledged + justified | PASS |
| JS-021 (lock) | PASS |
| JS-033 (async void) | PASS |
| JS-001 / JS-002 | PASS |
| CYC ≤ 8 for all removed methods | PASS |
| NT8 compiler compliance | PASS |
| Orphaned callers verified absent | PASS |
| No scope creep | PASS |

**VIOLATIONS FOUND**: 0

---

## REVIEW_PASS

Phase 3 (ticket generation) is unblocked. The engineer may proceed to `04-tickets.md`.
