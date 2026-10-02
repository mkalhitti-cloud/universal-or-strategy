# B184 Plan Review
## ptt-plan-reviewer Phase 2

**Date**: 2026-07-07
**Author**: ptt-plan-reviewer
**Epic**: B184 — Restore CopyEngine.cs BE Event Infrastructure + Full ATR Removal
**Plan under review**: `docs/brain/B184/02-architecture-plan.md`
**Spec**: `docs/brain/B184/00-orchestrator-prompt.md`
**Prior backlog**: `docs/brain/B183/06-deferred-backlog.md`

---

## OVERALL VERDICT

**REVIEW_FAIL**

**Violations**: 1
- **V-01**: Commit Size Gate not called out in the plan (Hard Constraint from spec, required by review checklist item 2)

---

## Section A — Lane-Split Gate Compliance

| Question | Answer in Plan | Required Answer | Pass? |
|----------|---------------|-----------------|-------|
| Q1. Same method or within 50 lines? | NO | NO for LANES-APPROVED | ✓ |
| Q2. T2 design depends on T1? | NO | NO for LANES-APPROVED | ✓ |
| Q3. Each fix has standalone value? | YES | YES for LANES-APPROVED | ✓ |
| Q4. Each fix has independent verification path? | YES | YES for LANES-APPROVED | ✓ |
| Gate result stated? | LANES-APPROVED | Must be stated | ✓ |

**Lane-Split Gate Result: PASS**

Reasoning is sound: T1 and T2 touch disjoint regions (BE event infrastructure vs ATR subsystem),
have zero design dependency, and each has a standalone build + verification chain.
The execution override (T1 first, sequential) is correctly called out.

---

## Section B — Spec Coverage Matrix

### T1 Symbols (6 items)

| Symbol | Spec Reference | Plan Address | Pass? |
|--------|---------------|--------------|-------|
| `BeState` enum | Spec line 62 | Plan line 46 | ✓ |
| `PendingBeArmed` event | Spec line 63 | Plan line 47 | ✓ |
| `GlobalBeAllDisarmed` event | Spec line 64 | Plan line 48 | ✓ |
| `DisarmPendingBe(Account)` method | Spec line 65 | Plan line 49 | ✓ |
| `IsPendingSlotsEmpty()` method | Spec line 66 | Plan line 50 | ✓ |
| `RaiseBeAllDisarmed()` method | Spec line 67 | Plan line 51 | ✓ |

All 6 T1 symbols addressed. ✓

### T2 Removal Scope

| File | Spec Requirement | Plan Address | Pass? |
|------|-----------------|--------------|-------|
| TradeCopierPanel.cs — 6 fields | Spec lines 114-119 | Plan lines 270-276 | ✓ |
| TradeCopierPanel.cs — 10 methods | Spec lines 121-130 | Plan lines 279-292 | ✓ |
| TradeCopierPanel.cs — 4 call sites | Spec lines 132-136 | Plan lines 294-301 | ✓ |
| CopyEngine.cs — 2 fields | Spec lines 139-140 | Plan lines 246-247 | ✓ |
| CopyEngine.cs — 5 methods / 1 call-site patch | Spec lines 141-146 | Plan lines 248-253 | ✓ |
| TradeCopierAddOn.cs — 2 fields | Spec lines 149-150 | Plan lines 310-313 | ✓ |
| TradeCopierAddOn.cs — 4 methods + call sites | Spec lines 151-155 | Plan lines 317-330 | ✓ |
| FeatureFlags.cs — record param + 4 factory sites | Spec lines 158-160 | Plan lines 194-227 | ✓ |
| Delete AtrSizingEngine.cs | Spec line 163 | Plan line 66 + lines 341-358 | ✓ |
| Delete AtrCalc.cs | Spec line 164 | Plan line 67 + lines 341-358 | ✓ |

All T2 removal targets addressed across all 6 files. ✓

### Deferred Backlog Coverage

| DW ID | Status Required | Plan Status | Pass? |
|-------|----------------|-------------|-------|
| DW-B183-06 | CLOSED by B184 | CLOSED by T1 | ✓ |
| DW-B183-07 | CLOSED by B184 | CLOSED by T1 | ✓ |
| DW-B183-08 | CLOSED by B184 | CLOSED by T1 | ✓ |
| DW-B183-09 | CLOSED by B184 | CLOSED by T1 | ✓ |
| DW-B183-10 | Referenced (was OPEN) | PARTIALLY LIFTED by T2 | ✓ |

Note: DW-B183-10 was marked P1 in B183 backlog. Plan correctly notes ATR branch removal reduces DispatchCopy
effective branch count (lifting the freeze). Not all of DW-B183-10 is closed (DispatchCopy CYC=8 is the
remaining constraint), but the plan accurately describes the partial lift.

---

## Section C — Hard Constraints Audit

### C.1 — B180-2 Approved Removals Excluded (T1)

Plan explicitly calls out all 5 excluded symbols:
`ResetBeStateOnFlat`, `ExecuteBeIdle`, `ExecuteBeArmed`, `UpdateBeVisuals`, `OnBeConnected`.

Steps 3 and acceptance criterion 8 both verify exclusion via grep. ✓

### C.2 — FeatureFlags Arity Safety (T2)

Plan provides complete before/after for the record declaration (7 → 6 params), and covers all 4
factory sites:
| Method | 7-arg → 6-arg | Covered? |
|--------|--------------|---------|
| `Starter()` | `new(false×7)` → `new(false×6)` | ✓ |
| `Pro()` | `new(true,true,true,false,false,false,false)` → 6 args | ✓ |
| `Elite()` | `new(true×7)` → `new(true×6)` | ✓ |
| `FromFeatureList()` | 7 named args → 6 named args | ✓ |

IsExternalInit shim preservation explicitly called out. ✓

### C.3 — GetSuggestedQty Caller Verification

Plan (lines 236-240) requires explicit pre-check:
```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "GetSuggestedQty"
# Expected: only definition line and ATR-gated call sites
```
This satisfies the spec's "only if exclusively used by ATR sizing" requirement. ✓

### C.4 — Fix Source Correctness (T1)

Plan correctly specifies `git show f01b5155:src/PropTraderTools/CopyEngine.cs` as the verbatim
source for all 6 T1 symbols. ✓

No cherry-pick of full file is mandated — "Extract each symbol individually." ✓

### C.5 — Single-Hunk Ban

Plan addresses Single-Hunk Ban for both tickets:
- T1 acceptance criterion 11: `hunk count != 1`
- T2 acceptance criterion 8: `hunk count != 1 on CopyEngine.cs`
- Both scan checklists (SCAN-06) verify hunk count

✓

### C.6 — Commit Size Gate (**VIOLATION — V-01**)

**Spec (00-orchestrator-prompt.md, line 83)**:
> *"Commit Size Gate: >300 changed lines on CopyEngine.cs requires Director explicit approval."*

**Review checklist item 2** requires: "Commit Size Gate called out."

**Finding**: The plan does NOT contain a dedicated Commit Size Gate check or callout anywhere in
T1 acceptance criteria, step-by-step engineer instructions, or the risk register entries. The plan
estimates "~30-80 lines added" for T1 (well under 300), but that is a scope estimate — not a
guard check. A dedicated check such as:

```powershell
(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^[+-]").Count
# If > 300 STOP and obtain Director approval before committing
```

...is absent from the plan. The plan does not instruct the engineer to measure the diff size
or gate on Director approval if the count exceeds 300.

**Violation**: Review checklist item 2 — Hard constraint not honored in plan.
**Rule**: Not a JS-XXX rule violation; a PTT pipeline protocol violation (spec hard constraint omitted).

---

## Section D — JS Rule Compliance

### D.1 — JS-021 (lock() ban)

Plan does not introduce any `lock()` usage in new code. The threading model section (plan lines
424-426) correctly identifies the risk:
> *"If f01b5155 uses lock(), escalate to Director immediately."*

T1 scan checklist SCAN-01 requires: grep restored diff for `lock(` → must be 0 matches.
Risk register R10 explicitly calls out this scenario.

**Assessment**: Plan handles JS-021 correctly for a plan-stage review. The escalation language
("escalate to Director immediately") is appropriate — the plan cannot know the f01b5155 content
at plan-writing time. The scan and risk register together provide the required enforcement gate.
No violation. ✓

### D.2 — JS-001 (throw in hot path)

T1 rule constraint table (plan line 156): "DisarmPendingBe must null-guard, not throw." ✓
T1 SCAN-02: Grep restored diff for `throw new` → must be 0 in hot paths. ✓

No violation. ✓

### D.3 — JS-002 (return null)

Plan line 157: "No `return null` — Methods return void or bool — N/A." ✓
T2 is removal-only; no new code introducing return null. ✓

No violation. ✓

### D.4 — JS-033 (async void)

Plan line 158: "Methods are not async." ✓
T2 is removal-only; no async void introduced. ✓

No violation. ✓

### D.5 — JS-066 (CYC <= 8)

T1 rule constraint table (plan line 159): DisarmPendingBe CYC=2-4; IsPendingSlotsEmpty CYC=1;
RaiseBeAllDisarmed CYC=1. All well within budget. ✓

T2 rule constraint table (plan line 373): "DispatchCopy CYC must still be <= 8 after
ResolveBaseQty removal." Plan notes: "CYC can only go DOWN from removal — verified OK." ✓

Note: Plan acknowledges DW-B183-10 freeze on DispatchCopy (CYC=8 budget) and documents that
T2 partially lifts the freeze. The DW-B183-03 freeze on IsLaneEEligible is untouched and
remains OPEN (not in B184 scope — correct). ✓

No violation. ✓

### D.6 — ASCII-only

T1 SCAN-04: "No Unicode in identifiers or string literals." ✓
T2 SCAN-04: "No new string literals introduced." ✓

No violation. ✓

### D.7 — JS-023 (UI update from off-thread without Dispatcher.InvokeAsync)

Plan threading model (lines 424-426) confirms Panel subscribers "ALREADY use Dispatcher.InvokeAsync
(confirmed at lines 3025-3031, 3037-3040 of TradeCopierPanel.cs)." ✓

No new threading contracts introduced. No violation. ✓

---

## Section E — ptt-sync.ps1 and Guardian Script

| Check | T1 | T2 |
|-------|----|----|
| `ptt-sync.ps1` in acceptance criteria | Item 10 ✓ | Item 7 ✓ |
| `verify_critical_symbols.ps1` in acceptance criteria | Item 9 ✓ | Item 6 ✓ |
| Both in GUARDIAN / SYNC section | Lines 493-499 ✓ | Lines 493-499 ✓ |

Both scripts correctly required after every CopyEngine.cs edit. ✓

---

## Section F — Single-File Scope for T1

Plan "T1 Files" table (plan line 477-479): only `src/PropTraderTools/CopyEngine.cs`.
Plan component list T1 section (plan lines 44-51): all 6 symbols placed in CopyEngine.cs.

T1 is single-file scoped. ✓

---

## Section G — Risk Register

Plan contains a 10-entry risk register (plan lines 459-470) covering:
- R1: Single-Hunk Ban clustering risk (T1)
- R2: BeState enum duplicate definition risk
- R3: B180-2 contamination risk
- R4: FeatureFlags partial arity update risk
- R5: ResolveBaseQty call site missed
- R6: UpdateAtrOverlay undiscovered callers
- R7: AtrCalc callers outside AtrSizingEngine.cs
- R8: IsExternalInit shim removed accidentally
- R9: Cross-contamination of T1/T2 branches
- R10: f01b5155 source uses lock() in DisarmPendingBe

All material risks are identified with mitigations. ✓

---

## Section H — 7-Scan Checklists

### T1 Scan Checklist (plan lines 437-444)

| Scan | Description | Rule Cited | Pass? |
|------|-------------|-----------|-------|
| SCAN-01 | lock() ban | JS-021 | ✓ |
| SCAN-02 | throw new ban | JS-001 | ✓ |
| SCAN-03 | CYC <= 8 per method | JS-066 | ✓ |
| SCAN-04 | ASCII-only identifiers | (unlabeled) | ✓ |
| SCAN-05 | B180-2 exclusion grep | (protocol) | ✓ |
| SCAN-06 | Single-Hunk Ban | (protocol) | ✓ |
| SCAN-07 | Guardian script exit 0 | (protocol) | ✓ |

All 7 scans present. ✓

### T2 Scan Checklist (plan lines 447-453)

| Scan | Description | Rule Cited | Observation |
|------|-------------|-----------|-------------|
| SCAN-01 | lock() ban | JS-021 | ✓ |
| SCAN-02 | arity consistency | **JS-002** cited | ⚠ Minor: JS-002 is "Use Option<T> Instead of Null" — not the arity rule. No JS rule covers record arity exactly; build compilation is the correct verification mechanism. Rule mis-citation but scan content is correct. Non-blocking. |
| SCAN-03 | CYC <= 8 DispatchCopy | JS-066 | ✓ |
| SCAN-04 | ASCII-only | (unlabeled) | ✓ |
| SCAN-05 | No residual ATR refs | (protocol) | ✓ |
| SCAN-06 | Single-Hunk Ban | (protocol) | ✓ |
| SCAN-07 | Guardian + sync scripts | (protocol) | ✓ |

All 7 scans present. Rule mis-citation in SCAN-02 is an observation only (non-blocking).

---

## Section I — NT8 / AddOn API Violations

No NT8-specific violations found:
- No `async/await` in lifecycle methods introduced.
- No `Account.All` in constructors introduced.
- No sealed `TradeCopierWindow` introduced.
- No `FontFamily` override introduced.
- No hardcoded `#RRGGBB` hex introduced.
- No `CreateOrder` without `PTT-` prefix introduced.
- No `DateTime.Now` (non-UTC) introduced.

T1 restores pre-existing infrastructure symbols; T2 removes existing code. Neither ticket
introduces new NT8 pattern violations. ✓

---

## Section J — Observations (Non-Blocking)

| # | Observation | Location | Blocking? |
|---|-------------|---------|-----------|
| OBS-1 | RULES_CATALOG.md on disk is truncated at JS-041 (DW-B183-05 OPEN). The hardcoded DNA block in the reviewer role definition covers JS-042 through JS-110. | B183 backlog DW-B183-05 | No |
| OBS-2 | T2 SCAN-02 cites JS-002 for FeatureFlags arity check. JS-002 is "Use Option<T> Instead of Null" — incorrect rule attribution. The scan itself is correct; only the rule citation is wrong. Engineer should treat this as "build verification, no JS rule number." | Plan line 448 | No |
| OBS-3 | DW-B183-01 and DW-B183-02 (xUnit test coverage gaps) remain OPEN. B184 does not address them per scope definition. The plan correctly identifies DW-B183-01/-02/-03/-04/-05 as remaining open. | Plan line 36 | No |
| OBS-4 | The T1 threading model note at plan line 427 uses "escalate to Director" language for the lock() scenario rather than "auto-FAIL." For a plan document, this is acceptable — the code can't be reviewed until written. The enforcement path (R10 + SCAN-01) is correct. | Plan lines 424-426 | No |

---

## Section K — Violation Summary

| ID | Violation | Type | Location in Plan | Blocking? |
|----|-----------|------|-----------------|-----------|
| V-01 | Commit Size Gate (>300 changed lines on CopyEngine.cs requires Director approval) not called out in plan acceptance criteria or engineer steps | Hard Constraint from spec (00-orchestrator-prompt.md line 83); review checklist item 2 | T1 acceptance criteria / engineer instructions | **YES — REVIEW_FAIL** |

---

## FINAL VERDICT

**REVIEW_FAIL**

**Blocking violations**: 1

**V-01**: The spec Hard Constraint *"Commit Size Gate: >300 changed lines on CopyEngine.cs requires Director explicit approval"* is absent from the plan. Review checklist item 2 explicitly requires this to be called out. The plan must add a Commit Size Gate check to the T1 engineer instructions and acceptance criteria. Recommended addition:

```powershell
# T1 Acceptance Criterion (add to existing list):
# Commit Size Gate: (git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^[+-]").Count
# If result > 300 → STOP. Obtain Director explicit approval before committing.
```

**Path to REVIEW_PASS**:
1. ptt-architect adds the Commit Size Gate check to `02-architecture-plan.md` (T1 acceptance criteria and/or engineer step instructions).
2. ptt-plan-reviewer re-reviews the updated plan.

**All other checklist items PASS.** The plan is otherwise complete, well-structured, and correct.
The Lane-Split Gate compliance is properly documented. All 6 T1 symbols and all T2 removal targets
across all 6 files are addressed. JS rule constraints are correctly mapped. Risk register is thorough.
7-scan checklists are present for both tickets.

---

*Written by: ptt-plan-reviewer (Phase 2) — 2026-07-07*
*Epic: B184*
*Plan revision reviewed: 2026-07-07 PLAN_COMPLETE*

---

## RE-REVIEW ADDENDUM — Cycle 2
**Date**: 2026-07-07
**Author**: ptt-plan-reviewer
**Trigger**: REVIEW_FAIL (V-01) returned in cycle 1. ptt-architect revised plan to cycle 2.
**Plan revision reviewed**: `02-architecture-plan.md` — Status: `PLAN_COMPLETE (cycle 2 — V-01 resolved)`

---

### V-01 Resolution Check

The prior review required the Commit Size Gate to be present in three locations:

| Location Required | Found in Cycle-2 Plan | Evidence |
|------------------|-----------------------|---------|
| T1 Acceptance Criteria | ✅ YES | AC item 12, plan line 182: `**Commit Size Gate**: ... if result > 300, STOP. Do NOT commit. Obtain Director explicit approval first.` |
| T1 Engineer Step Instructions | ✅ YES | Step 6 (pre-commit, MANDATORY), plan lines 121–128: PowerShell command + stop-on->300 escalation instruction |
| Risk Register | ✅ YES | R11, plan line 481: `T1 diff exceeds 300 changed lines on CopyEngine.cs | Low | Protocol violation (Commit Size Gate) | Run ... before commit. If > 300, STOP and escalate to Director...` |

**V-01 STATUS: RESOLVED.** All three required locations are populated with the correct gate language.

---

### Previously-PASSed Items Integrity Check

All 12 items that PASSed in cycle 1 are confirmed intact in the cycle-2 plan:

| # | Item | Intact? |
|---|------|---------|
| 1 | Lane-Split Gate (4 Q&A + LANES-APPROVED result) | ✅ |
| 2 | Deferred Backlog table (DW-B183-06/07/08/09/10) | ✅ |
| 3 | T1 Component List (6 symbols, correct kinds) | ✅ |
| 4 | T2 Component List (all 6 files, all symbol counts) | ✅ |
| 5 | Fix Source (f01b5155 verbatim, no cherry-pick) + B180-2 exclusion grep (Steps 1–3) | ✅ |
| 6 | JS Rule Constraint tables (T1 + T2) | ✅ |
| 7 | Threading model — Dispatcher.InvokeAsync confirmed for Panel subscribers | ✅ |
| 8 | 7-Scan Checklists — T1 (7 scans) + T2 (7 scans) | ✅ |
| 9 | ptt-sync.ps1 + verify_critical_symbols.ps1 in both AC lists and GUARDIAN/SYNC section | ✅ |
| 10 | Single-Hunk Ban (T1 AC item 11, T2 AC item 8, Step 5, both SCAN-06s) | ✅ |
| 11 | Risk Register R1–R10 (all 10 original entries present) | ✅ |
| 12 | NT8 API usage — no lifecycle async/await, no Account.All, no sealed Window, no hex literals | ✅ |

---

### New Violation Scan (cycle-2 additions only)

The cycle-2 diff is strictly additive:
- Step 6 inserted into T1 engineer instructions (PowerShell command + gate narrative)
- AC item 12 appended to T1 Acceptance Criteria list
- R11 appended to Risk Register
- Revision note appended to plan footer

None of these additions introduce code, symbols, NT8 API calls, threading constructs, or
new spec requirements. No new JS-XXX or PTT protocol violations are introduced.

**New violations found: 0.**

---

### Cycle-2 Re-Review Verdict

| Check | Result |
|-------|--------|
| V-01 resolved (Commit Size Gate in AC + Steps + Risk Register) | ✅ PASS |
| All 12 prior PASS items intact | ✅ PASS |
| No new violations introduced | ✅ PASS |

## OVERALL VERDICT (cycle 2)

**REVIEW_PASS**

The plan is approved for Phase 3 (ticket generation). ptt-architect may proceed to write
`docs/brain/B184/04-tickets.md`.

---

*Written by: ptt-plan-reviewer (Phase 2 re-review, cycle 2) — 2026-07-07*
*Epic: B184*
