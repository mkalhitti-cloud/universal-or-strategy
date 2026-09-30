# Ticket Review: PTT-COPIER-B21-LANE-C

**Epic**: PTT-COPIER-B21-LANE-C
**Spec**: DW-ATM-DROPDOWN-01
**Phase**: 3.5 (Ticket Review)
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-07-14
**Tickets reviewed**: `docs/brain/PTT-COPIER-B21-LANE-C/04-tickets.md`
**Plan reviewed**: `docs/brain/PTT-COPIER-B21-LANE-C/02-architecture-plan.md`
**Plan review**: `docs/brain/PTT-COPIER-B21-LANE-C/02-plan-review.md` (REVIEW_PASS)

---

## T1 — Remove ATM Template Dead Code from TradeCopierPanel.cs

### Traceability: PASS

T1 explicitly maps to **DW-ATM-DROPDOWN-01** in the "Spec Requirements Satisfied" section.
All 9 spec items are assigned to this single ticket with source-verified line numbers.
No phantom work (items not in spec/plan) was found.
No missing work (items in plan but absent from ticket) was found.

| Spec Item | In Ticket? | Ticket Section |
|-----------|-----------|----------------|
| 1. Field `_atmTemplateCombo` | YES | "Items 1–2 — Field declarations (lines 158–161)" |
| 2. Field `_activeAtmTemplateName` | YES | "Items 1–2 — Field declarations (lines 158–161)" |
| 3. Method `BuildAtmTemplateRow()` | YES | "Item 4 — Method `BuildAtmTemplateRow()` (lines 1405–1431)" |
| 4. Method `LoadAtmTemplates()` | YES | "Item 5 — Method `LoadAtmTemplates()` (lines 1433–1451)" |
| 5. Method `OnAtmTemplateSelectionChanged()` | YES | "Item 6 — Method `OnAtmTemplateSelectionChanged()` (lines 1453–1461)" |
| 6. Method `GetAtmTemplatesDirectory()` | YES | "Item 3 — Method `GetAtmTemplatesDirectory()` (lines 1395–1403)" |
| 7. Call site in `BuildUI()` | YES | "Item 7 — Call site in `BuildUI()` (line 566 + adjacent comment)" |
| 8. Call site in `OnLoaded()` | YES | "Item 8 — Call site in `OnLoaded()` (line 459)" |
| 9. Header comment lines 51–57 | YES | "Item 9 — Header comment block (lines 51–57)" |

### JS Pre-Check: PASS

All applicable Jane Street rules reviewed against ticket descriptions.
No P0 or P1 violations described anywhere in T1.

| Rule ID | Check | Result | Evidence in Ticket |
|---------|-------|--------|-------------------|
| JS-021 (lock() banned) | Does ticket describe any `lock()` usage? | PASS | "Jane Street Rule Constraints" table: "This deletion adds zero lock() calls. Post-edit file must contain 0 lock() — verified by SCAN-06." |
| JS-033 (async void banned) | Does ticket describe any async void (non-event-handler)? | PASS | Table states: "All removed methods are synchronous void. No async void introduced." |
| JS-001 (no throw in hot path) | Does ticket describe any throw in new code? | PASS | Table states: "No new code written. No throw introduced." |
| JS-002 (no return null) | Does ticket describe any null return for missing value? | PASS | Table states: "No new code written. No null return introduced." |
| JS-008 (immutability) | Does ticket describe mutable struct fields? | N/A | Removal-only task; no struct fields added. |
| JS-009 (no mutable Dictionary on shared fields) | Does ticket describe Dictionary on CopyRule/CopyEngine fields? | N/A | No new fields introduced. |
| JS-023 (UI update from non-UI thread) | Does ticket describe off-thread UI updates? | PASS | Threading section confirms all removed methods were UI-thread-only; no Dispatcher violation described. |

### CYC Pre-Check: PASS

All removed methods have CYC annotations in the ticket. No new code is written.
Net CYC impact is zero (methods deleted, none added). No at-risk methods.

| Method | CYC (annotated) | Within ≤ 8 limit? |
|--------|----------------|-------------------|
| `GetAtmTemplatesDirectory()` | CYC=1 | YES |
| `BuildAtmTemplateRow()` | CYC=1 | YES |
| `LoadAtmTemplates()` | CYC=3 | YES |
| `OnAtmTemplateSelectionChanged()` | CYC=2 | YES |

### NT8 Check: PASS

| Constraint | Result | Evidence in Ticket |
|------------|--------|-------------------|
| No async/await in lifecycle methods | PASS | No async introduced (removal-only) |
| No Account.All outside Loaded handler | PASS | No Account.All present in ATM block |
| No `sealed` on TradeCopierWindow | PASS | TradeCopierWindow not modified |
| No FontFamily on WPF element | PASS | No new WPF elements added |
| No hardcoded hex color | PASS | No new UI elements added |
| No CreateOrder without "PTT-" prefix | PASS | No CreateOrder calls in ticket scope |
| No DateTime.Now | PASS | No DateTime.Now in ticket scope |
| NT8-003 (no `volatile double`) | PASS | "NT8 Compiler Rule Constraints" table: "No volatile fields exist in the ATM block." |
| `using System.IO` deferred | PASS | "Scope Constraint" section: "Do not remove `using System.IO;` — explicitly out of scope." |
| NT8 build gate (SCAN-07) | PASS | SCAN-07: `dotnet build` expected 0 errors — present in 7-scan checklist. |

### Test Coverage: PASS

The "[Fact] Section" is explicitly present. It correctly states:

> "No new `[Fact]` tests required — dead code removal."

Four justified reasons are given:
1. No behavioral change to `CopyEngine` or any order path — existing `[Fact]` tests remain green.
2. ATM template selection had zero test coverage (unwired dead code).
3. Symbol-absence scans (SCAN-01..SCAN-05) + build gate (SCAN-07) constitute the full verification contract.
4. The WPF panel is not unit-testable without a live WPF infrastructure.

The ticket additionally requires the engineer to confirm `CopyEngineTests.cs` passes unchanged,
which is the correct regression guard for a removal-only task.

No new public or internal methods are introduced; the "[Fact] required for every new method" rule
does not apply. PASS.

### Scan Checklist: PASS

All 7 scans are present with exact grep patterns, target file, and expected results.
Per-ticket scan presence is confirmed non-negotiable (defense-in-depth — 3 layers: ticket contract,
engineer attestation in ticket-N-completion.md, verifier cross-check in ticket-N-verification.md).

| Scan | Pattern | Expected | Present? | Purpose |
|------|---------|----------|----------|---------|
| SCAN-01 | `grep -n "_atmTemplateCombo" TradeCopierPanel.cs` | 0 matches | YES | Verify field deleted |
| SCAN-02 | `grep -n "_activeAtmTemplateName" TradeCopierPanel.cs` | 0 matches | YES | Verify field deleted |
| SCAN-03 | `grep -n "BuildAtmTemplateRow" TradeCopierPanel.cs` | 0 matches | YES | Verify method + call site deleted |
| SCAN-04 | `grep -n "LoadAtmTemplates" TradeCopierPanel.cs` | 0 matches | YES | Verify method + call site deleted |
| SCAN-05 | `grep -n "OnAtmTemplateSelectionChanged" TradeCopierPanel.cs` | 0 matches | YES | Verify method deleted |
| SCAN-06 | `grep -n "lock(" TradeCopierPanel.cs` | 0 matches | YES | JS-021 gate (independent of deletion) |
| SCAN-07 | `dotnet build` | 0 errors | YES | NT8 compiler gate |

Each scan serves a distinct purpose (defense-in-depth rationale intact). PASS.

### File Routing: PASS

Target file is correctly specified as:
```
c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs
```

This points to the **Wave workspace** (`c:\WSGTA\universal-or-strategy`), not the Director
workspace (`c:\WSGTA\universal-or-strategy-director`). PASS.

### Scope Constraint Check: PASS

The "Scope Constraint" section explicitly:
- Restricts all modifications to `TradeCopierPanel.cs` ONLY.
- Names all files NOT to touch: `CopyEngine.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`, `CopyEngineTests.cs`, any `.csproj`, any other file.
- Prohibits adjacent refactoring: "Do not refactor any code adjacent to the removed blocks."
- Defers `using System.IO` removal as explicitly out of scope.
- Anchors each change: "Every changed line must trace directly to DW-ATM-DROPDOWN-01."

No scope creep detected. PASS.

### Engineer Completion Checklist: PASS

A 16-item completion checklist is present covering:
- All 9 spec deletions (lines 51–57, 459, ~565-566, 159–161, 1395–1404, 1405–1432, 1433–1452, 1453–1462)
- All 7 scan results (SCAN-01 through SCAN-07)
- Regression guard: "Existing `CopyEngineTests.cs` [Fact] tests pass unchanged"

Checklist is well-formed and complete. PASS.

---

## Minor Observation (Non-Blocking)

**Item 7 comment-line ambiguity** (ticket section "Item 7 — Call site in `BuildUI()`"):
The ticket notes that the `// B11 T2: ATM template row` comment may be at line 564 or 565
depending on prior edits, and correctly instructs the engineer to use text-pattern search
rather than a fixed line number alone. This matches the plan and the plan-reviewer's prior
non-blocking observation. The removal target is unambiguous. Not a violation.

---

## Summary

| Check | Result |
|-------|--------|
| Traceability: T1 → DW-ATM-DROPDOWN-01 | PASS |
| All 9 spec items present with line numbers | PASS |
| JS-021 (no lock()) constraint | PASS |
| JS-033 (no async void) | PASS |
| JS-001 / JS-002 (no throw / no null return) | PASS |
| CYC ≤ 8 for all annotated methods | PASS |
| NT8 compiler rules addressed | PASS |
| [Fact] section: "no new [Fact] required" justified | PASS |
| 7-scan checklist: all 7 scans present and correct | PASS |
| File routing: Wave workspace, not Director workspace | PASS |
| Scope constraint: TradeCopierPanel.cs only | PASS |
| No scope creep | PASS |
| Engineer completion checklist: 16 items | PASS |
| Defense-in-depth: each scan serves distinct purpose | PASS |

**VIOLATIONS FOUND**: 0

---

## Overall: TICKET_REVIEW_PASS

Phase 4 (engineer implementation) is unblocked. The engineer may proceed to execute
`docs/brain/PTT-COPIER-B21-LANE-C/04-tickets.md` → T1 and report results in
`docs/brain/PTT-COPIER-B21-LANE-C/ticket-1-completion.md`.
