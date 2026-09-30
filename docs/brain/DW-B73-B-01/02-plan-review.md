# DW-B73-B-01/02 -- Plan Review

**Date**: 2026-08-21
**Reviewer**: ptt-plan-reviewer (Phase 2)
**Plan version reviewed**: `docs/brain/DW-B73-B-01/02-architecture-plan.md` (ptt-architect, 2026-08-21)
**Spec reviewed**: `docs/brain/DW-B73-B-01/00-orchestrator-prompt.md`
**Rules catalog**: `docs/standards/jane-street/RULES_CATALOG.md` v1.0 (UTF-8 clean, readable — GATE: PASS)

---

## Rules Catalog Gate

**GATE RESULT: PASS**

Rules applicable to this plan:
| Rule ID | Category | Severity | Applicable? |
|---------|----------|----------|-------------|
| JS-021 | Concurrency — no `lock()` | P0 | YES — new code must not introduce locks |
| JS-001 | Type Safety — no `throw` in hot paths | P0 | YES — hot path touched (UpdateBeAllVisuals) |
| JS-002 | Type Safety — no `return null` | P0 | YES — new field/no new methods, check for null returns |
| JS-008 | Type Safety — frozen brushes / readonly struct | P1 | YES — new `SolidColorBrush` field added |
| JS-033 | Concurrency — no `async void` | P0 | YES — no new async code introduced |
| ASCII-only | DNA mandate | P0 | YES — all new identifiers must be ASCII |
| CYC <= 8 | DNA mandate | P1 | YES — 3 methods have CYC in plan Section D |

No P0 violations found in new code described by the plan.

---

## Section 2a — Spec Traceability

| Requirement | Source | Addressed in Plan? | Plan Section | Result |
|-------------|--------|--------------------|--------------|--------|
| DW-B73-B-01 fix present | spec § Scope | YES — remove `UpdateBeAllVisuals(BeState.Idle)` at L587 | A | PASS |
| DW-B73-B-01 preferred approach honored | spec § "preferred — simpler" + "See architecture plan for chosen approach" | PASS — spec explicitly defers to plan for chosen approach | A | PASS |
| DW-B73-B-02 fix present | spec § Scope | YES — `BrushTeal` field + 10-site replacement | A | PASS |
| `BrushTeal` field added | spec § Scope | YES — `private static readonly SolidColorBrush BrushTeal = MakeBrush(13, 148, 136)` | A | PASS |
| All 6 spec-identified MakeBrush sites replaced | spec § Scope | YES — plan includes all 6 spec-listed sites plus 4 additional (L1111-1112, L1140-1141) | A, G | PASS |
| No changes to CopyEngine.cs or other files | spec § Scope | YES — Section F confirms TradeCopierPanel.cs only | F | PASS |

**Informational — spec count vs. plan count**:
- Spec says "6 inline MakeBrush(13, 148, 136) call sites"; plan identifies **10**. Spec had TBD markers at L1111 and L1140 (source locations table), indicating the plan's 10-count is the correct/complete figure. No violation — plan is more thorough.
- Spec commit-message template says `295 → 298` (3 new [Fact]); plan proposes 6 new [Fact] → 301 total. Not a FAIL — plan exceeds spec's test estimate. The ptt-verifier will confirm actual count. Orchestrator should use actual post-implementation count in commit message.

---

## Section 2b — JS-DNA Rules

| Rule | Check | Result |
|------|-------|--------|
| JS-021: no `lock()` introduced | Plan Section E confirms no lock added. No locking construct appears in described changes. | PASS |
| JS-001: no `throw new XxxException` | Plan Section E confirms none added. No exception-throwing code in described changes. | PASS |
| JS-002: no `return null` | Plan Section E confirms none added. `BrushTeal` field is initialized by `MakeBrush`, never null. | PASS |
| JS-008: `BrushTeal` must be frozen | Plan Section E: "BrushTeal uses MakeBrush which calls .Freeze()". `MakeBrush` body at L267-272 shows `brush.Freeze()` before return. Invariant preserved. | PASS |
| JS-033: no `async void` | No new async code introduced. Existing `Dispatcher.InvokeAsync` path unchanged. | PASS |
| ASCII-only | All new identifiers: `BrushTeal`, `T_DW_B73_B01_01..03`, `T_DW_B73_B02_01..03`. All ASCII. | PASS |
| CYC <= 8 | Section D: all three affected methods at or below 8 after changes (see §2d). | PASS |
| NT8 no `async/await` in lifecycle | No lifecycle methods touched. | PASS |
| NT8 hardcoded `#RRGGBB` hex | Plan uses numeric RGB `(13, 148, 136)` — no hex string literals. | PASS |
| NT8 `SolidColorBrush` not `Freeze()`d | BrushTeal uses `MakeBrush` which already calls `.Freeze()`. | PASS |

---

## Section 2c — Scope Discipline

| Check | Plan Section | Result |
|-------|--------------|--------|
| Only `TradeCopierPanel.cs` modified in `src/` | Section F explicitly states one file only | PASS |
| No changes to `CopyEngine.cs` | Section F | PASS |
| No changes to `PttBreakEven.cs` | Section F | PASS |
| No changes to `CopyEngineTests.cs` | Section F | PASS |
| Test additions limited to B73Tests.cs or new test file | Section F, Section C | PASS |
| No refactors beyond the two described fixes | Plan scope is narrowly defined | PASS |
| No whitespace mutations beyond 11 changed lines | Section F explicitly calls this out | PASS |

---

## Section 2d — CYC Correctness

| Method | Claimed Before | Claimed After | Delta | Soundness |
|--------|---------------|---------------|-------|-----------|
| `UpdateButtonColors` | 8 | 8 | 0 | Sound — removing one call from inside an `if` block does not remove a branch. The `if (!hasPosition && !...)` and nested `if (_leaderAccount != null)` both remain. CYC unchanged. |
| `UpdateBeAllVisuals` | 2 | 2 | 0 | Sound — 10 `BrushTeal` substitutions replace `MakeBrush(...)` calls; no conditional logic changed. |
| `BuildBufferedButtonsRow` | 1 | 1 | 0 | Sound — field-reference substitutions only, zero branch changes. |

All methods remain at or below CYC=8. No new methods introduced. **PASS.**

---

## Section 2e — Test Coverage

### Ticket 1 tests (DW-B73-B-01)

| Test | Description | Meaningful? | Duplicates existing B73Tests.cs? |
|------|-------------|-------------|----------------------------------|
| `T_DW_B73_B01_01` — `RaiseBeAllDisarmed_NoException_WhenCalled` | Structural: event path intact after T1 edit | YES | NO — confirms path integrity, different from existing disarm tests |
| `T_DW_B73_B01_02` — `GlobalBeAllDisarmed_EventExists_AndIsSubscribable` | Structural: event member not accidentally removed | YES | NO — complementary guard |
| `T_DW_B73_B01_03` — `RaiseBeAllDisarmed_FiresSubscriber_ExactlyOnce` | Behavioral: quantitative single-fire verification | YES (strongest T1 test) | NO — no existing test asserts count=1 |

### Ticket 2 tests (DW-B73-B-02)

| Test | Description | Meaningful? | JS-008 compliance? |
|------|-------------|-------------|-------------------|
| `T_DW_B73_B02_01` — `BrushTeal_IsNotNull` | Structural: field was added | YES | N/A |
| `T_DW_B73_B02_02` — `BrushTeal_IsFrozen` | JS-008: `IsFrozen == true` | YES (critical) | DIRECTLY validates JS-008 |
| `T_DW_B73_B02_03` — `BrushTeal_Color_MatchesTeal600` | Regression: R==13, G==148, B==136 | YES | N/A |

Note: `BrushTeal` is `private static readonly`; tests access via reflection as noted in plan. This is standard practice for structural guards on private fields.

**PASS** — all 6 tests are meaningful, non-duplicative, and collectively satisfy JS-008 compliance verification.

---

## Section 2f — Risk Assessment

| Claim | Soundness |
|-------|-----------|
| B-01: Panel is subscribed before `UpdateButtonColors` fires (via `OnSessionAttached` L624) | SOUND — `UpdateButtonColors` is called only via `TryFirePositionState` -> `Dispatcher.InvokeAsync`, which runs after session attach. Subscription at L624 precedes any raise. |
| B-01: `Dispatcher.InvokeAsync` at Background priority runs before next render pass | SOUND — WPF Dispatcher Background priority processes before render. No visible user-facing change. |
| B-02: Zero behavioral change (frozen brush = identical to inline) | SOUND — `MakeBrush` always returns a frozen brush; caching one instance is semantically equivalent to allocating-and-freezing on each call. |

**PASS.**

---

## Section 2g — Expected Diff Coherence

| Item | Spec/Plan Claim | Check | Result |
|------|----------------|-------|--------|
| Total `src/` lines changed | 12 (1 delete + 1 insert + 10 substitutions) | 1+1+10 = 12 ✓ | PASS |
| Test line estimate | ~60 lines for 6 [Fact] (~10 lines/Fact) | Plausible ✓ | PASS |
| Total lines including tests | ~72 | 12 + 60 = 72 ✓ | PASS |

No scope-creep lines are visible in the diff. Diff is well within the 10k-char limit (12 src lines ≈ trivially small).

---

## Section 2h — Ticket Split Viability

| T1 touch points | T2 touch points | Overlap? |
|----------------|----------------|---------|
| L587 (remove one call) | L279-area (add field), L957-958, L1049-1141 | NONE |

Tickets are fully independent. T1 edit and T2 edits touch zero shared lines. Sequential execution (T1 then T2) is safe. Parallel execution would also be safe. **PASS.**

---

## Violations Found

**None.** No P0, P1, or NT8 violations were identified in the plan.

---

## Informational Notes (non-blocking)

| ID | Note |
|----|------|
| INFO-1 | Spec says "6 inline call sites" for B-02; plan correctly identifies 10 (spec had TBD markers for L1111/L1140). Plan is more thorough. No action required. |
| INFO-2 | Spec commit-message template expects 295→298 (3 new [Fact]); plan proposes 295→301 (6 new [Fact]). Plan is more comprehensive. Orchestrator should update commit message NNN to actual post-implementation [Fact] count confirmed by ptt-verifier. |

---

## Final Verdict

**REVIEW_PASS**

The plan is complete, internally consistent, and compliant with all applicable JS-DNA rules. All spec requirements are addressed. CYC budget is respected. Scope is constrained to one file. Tests are meaningful and non-duplicative.

Pipeline proceeds to **Phase 3** (ptt-architect → `04-tickets.md`).
