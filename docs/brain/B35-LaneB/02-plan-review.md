# B35-LaneB Plan Review
# Reviewer: ptt-plan-reviewer
# Block: B35 | Lane: B | DW-B32-queue | 5x P0 BE Defects
# Date: 2026-07-23
# Input: docs/brain/B35-LaneB/02-architecture-plan.md
# Spec: specs/002-trade-copier-spec.html id="section-b35" (LaneB card)
# Rules: docs/standards/jane-street/RULES_CATALOG.md
#        docs/standards/NT8_COMPILER_RULES.md

---

## VERDICT: REVIEW_PASS

No JS-XXX or NT8-XXX rule violations found. Two advisory findings documented in
Section V below. Neither is an auto-FAIL trigger.

---

## Section I — Spec Coverage Matrix

| Requirement | Addressed in Plan | Plan Section |
|-------------|------------------|--------------|
| DW-B32-01b: IsStopAlreadyAtBe short branch | YES | Section B.1 |
| DW-B32-02: MoveStopToBreakEven Accepted state filter | YES | Section B.2 |
| DW-B32-04b: BeState.Connected CS0117 compile fix | YES | Section B.3 |
| DW-B32-07: IsAtmSlotName guard in MoveStopToBreakEven | YES | Section B.4 |
| DW-B32-08: SubmitBeStop unconditional leader BE path | YES | Section B.5 |
| [Fact] test for DW-B32-01b | YES — T1 | Section C |
| [Fact] test for DW-B32-02 | YES — T2 | Section C |
| [Fact] test for DW-B32-04b | YES — T3 | Section C |
| [Fact] test for DW-B32-07 | YES — T4 | Section C |
| [Fact] test for DW-B32-08 | YES — T5 | Section C |
| Build tag supersedes LaneA with "bracket-cancel + BE-fixes" | YES | Section D |
| Scope locked to 3 files, no new files | YES | Section F |
| Hard-link gate specified | YES | Section G |
| Working-tree-already-fixed status documented | YES | Section A |

All 14 spec requirements are addressed. PASS.

---

## Section II — Jane Street DNA Gate Checks

| Rule ID | Description | Plan Section | Result |
|---------|-------------|--------------|--------|
| JS-021 | lock() ban — no lock() in any changed method | Section E | PASS |
| JS-002 | return null ban — IsStopAlreadyAtBe returns bool, MoveStopToBreakEven/BreakEven return void | Section E | PASS |
| JS-001 | no throw in hot path — MoveStopToBreakEven try/catch wraps acc.Change(); exception caught + logged, not propagated | Section E | PASS |
| JS-033 (NT8-019) | no async void — no async added in any fix | Section E | PASS |

---

## Section III — NT8 Compiler Rule Gate Checks

| Rule ID | Description | Plan Section | Result |
|---------|-------------|--------------|--------|
| NT8-046 | acc.Change() on ATM-owned stops (Stop1/Stop2) silently overridden — DW-B32-07 uses IsAtmSlotName guard to skip them; leader uses SubmitBeStop (not subject to NT8-046) | Section B.4, E | PASS |
| NT8-031 | OrderState.PendingSubmit does not exist in NT8 — plan uses Accepted (correct NT8 state) | Section B.2 | PASS |
| SCAN-06 / NT8-013 | DateTime.Now banned — plan notes DateTime.UtcNow in SubmitBeStop OCO ID only (existing code, not changed line); no Now in changed lines | Section E | PASS |
| NT8-003 | volatile double banned — no new volatile fields in plan | Section E | PASS |

---

## Section IV — Structural Checks

### 4.1 — CYC <= 8 Compliance

| Method | Declared CYC | Limit | Status |
|--------|-------------|-------|--------|
| IsStopAlreadyAtBe | 2 | 8 | PASS |
| MoveStopToBreakEven | 6 | 8 | PASS |
| BreakEven | 6 | 8 | PASS |
| OnBeUp | 1 | 8 | PASS |

All declared values are within the 8-point limit. PASS.

### 4.2 — Scope Constraint

Plan Section F specifies:
- CopyEngine.cs — permitted
- TradeCopierPanel.cs — permitted
- CopyEngineTests.cs — permitted
- All other files — BANNED
- New .cs files — BANNED

No new files introduced. PASS.

### 4.3 — Build Tag

Plan Section D:
```
internal const string Tag = "PTT-COPIER B35 | bracket-cancel + BE-fixes | {date}";
```
Spec states exactly:
```
internal const string Tag = "PTT-COPIER B35 | bracket-cancel + BE-fixes | {date}";
```
Exact match. PASS.

### 4.4 — Merge Sequence Constraint

Plan Section A documents the rebase constraint: LaneB engineer rebases on LaneA's
commit before pushing. LaneB tag supersedes LaneA. This matches spec exactly. PASS.

### 4.5 — Working-Tree Status Documentation

Plan Section A states "All 5 fixes are ALREADY PRESENT in the source files" and
documents the pipeline obligation (document, test, verify — not re-implement).
Each defect section (B.1-B.5) cites specific line numbers with "verified at line X"
annotations. PASS.

---

## Section V — Advisory Findings (Non-Blocking)

These findings do NOT trigger REVIEW_FAIL. No JS-XXX or NT8-XXX rule is violated.
They are documented for the engineer's awareness.

### ADF-01 — DW-B32-07 Test Name Divergence from Spec

**Severity**: Advisory  
**Location**: Plan Section C T4 vs spec orchestrator prompt  
**Finding**: Plan names the test `MoveStopToBreakEven_SkipsAtmOrders_ViaIsAtmSlotNameGuard`.
The spec orchestrator prompt names it `MoveStopToBreakEven_SkipsNonAtmOrders_ViaIsAtmSlotNameGuard`.
**Impact**: Minor naming inconsistency. The engineer receives the orchestrator prompt as
the execution directive and will use the spec name. No rule violation.

### ADF-02 — DW-B32-08 Architecture Re-Interpretation

**Severity**: Advisory  
**Location**: Plan Section B.5 vs spec DW-B32-08 description  
**Finding**: The spec states "MoveStopToBreakEven(leader,...) call in SubmitBeStop was
conditional — make unconditional". The plan re-interprets this: in the current
working-tree architecture (B33 rewrite), the leader path goes through `BreakEven ->
SubmitBeStop` (creates a new PTT-BE-Stop), and `MoveStopToBreakEven` is explicitly
excluded for the leader (`BreakEven` line 1757 skips leader in the follower fan-out).
The plan's T5 test accordingly tests `BreakEven` for `SubmitBeStop` presence, not
`SubmitBeStop` for `MoveStopToBreakEven` presence.  
**Assessment**: The plan provides working-tree line verification (lines 1737-1759) and
the architectural rationale (B33 changed the leader path). The defect intent (BE fires
on leader unconditionally given open position) is satisfied. The spec mechanism
description appears to reference pre-B33 architecture. No rule violation.
**Action**: ptt-engineer should confirm lines 1737-1759 match the plan's code block
before writing T5.

### ADF-03 — TradeCopierPanel.cs Not Listed in Spec Parallelism Table

**Severity**: Advisory  
**Location**: Spec parallelism table (section-b35) vs plan Section F  
**Finding**: The spec's parallelism safety table lists LaneB files as
"CopyEngine.cs, CopyEngineTests.cs" only. The plan adds TradeCopierPanel.cs to
address DW-B32-04b (BeState enum + OnBeUp). The spec's orchestrator prompt for
DW-B32-04b says "File: CopyEngine.cs — OnBeUp method (or BeState enum usage)" —
indicating the spec author may have believed OnBeUp was in CopyEngine.cs.
**Conflict risk**: ZERO — LaneA does not touch TradeCopierPanel.cs. The parallelism
guarantee (no merge conflict) is preserved.  
**Authorisation**: DW-B32-04b is explicitly listed in the spec's 5-defect queue. The
pipeline register lists all 5 tickets. TradeCopierPanel.cs is the correct file per
the working-tree source.  
**Assessment**: Plan is correct. Spec table was incomplete. No rule violation.
**Action**: None required. The plan's Section F scope table is authoritative.

---

## Section VI — Scan Pre-Check Status

The plan confirms scans are pre-verified against the working tree. Per the plan's
Section E gate table, all 8 applicable rules show PASS status before any engineer
action. This is appropriate given the working-tree-already-fixed nature of this lane.

---

## REVIEW_PASS

All P0 and P1 rule checks pass. All 5 defects are addressed. All 5 [Fact] tests are
specified. Build tag is correct. Scope is bounded to 3 files, no new files. CYC <= 8
for all methods. Working-tree status is accurately documented.

Proceed to Phase 3 (ticket generation).
