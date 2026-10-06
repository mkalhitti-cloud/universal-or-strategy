# B186 Plan Review
## PTT-COPIER B186 | Quick ALL + QAll2t Instrument Scope

**Reviewer**: ptt-plan-reviewer (Phase 2)
**Date**: 2026-07-08
**Plan reviewed**: `docs/brain/B186/02-architecture-plan.md`
**Spec reviewed**: `docs/brain/B186/00-orchestrator-prompt.md`, `docs/brain/B186/00-continue-prompt.md`
**Rules reviewed**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## VERDICT: REVIEW_PASS

No violations found. Zero rule citations. All 10 checklist items PASS. Plan is cleared for Phase 3.

---

## 1. Violation Register

| # | Rule ID | Description | Location in Plan | Result |
|---|---------|-------------|-----------------|--------|
| — | — | No violations found | — | — |

Zero violations. Zero FAIL triggers.

---

## 2. LANE-SPLIT GATE Compliance

| Check | Result |
|-------|--------|
| Gate result present | PASS — "LANE-SPLIT GATE RESULT: SINGLE-PIPELINE" stated explicitly in Section 0 |
| Plan uses lanes? | NO — single-pipeline, so lane-specific gate conditions do not apply |
| Q1 (same method / 50 lines?) | NO — T1 and T2 in different methods; T3 in different file. Correct answer for single-pipeline. |
| Q2 (Fix B depends on Fix A design?) | Answered with nuance: T3 depends on T1+T2 final signatures (serial order justified). |
| Q3 (standalone value?) | T1: yes; T2: no without T3. Mixed — correctly drives SINGLE-PIPELINE conclusion. |
| Q4 (independent SIM path?) | YES — Quick ALL and QAll2t independently verifiable in NT8 SIM. |
| Single-pipeline with no gate stated? | FAIL trigger not triggered — gate IS stated. |

**LANE-SPLIT GATE: PASS**

---

## 3. Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| T1: `Execute()` → `Execute(Instrument instrument)` | YES | Section 3 |
| T1: Combined gate `!QxGlobalExit \|\| instrument == null` | YES | Section 3, Change 1 |
| T1: Instrument filter `pos.Instrument?.FullName != instrument.FullName` | YES | Section 3, Change 3 |
| T1: Log line `[PTT-QX-ALL] GlobalQuickExit fired instr=` + FullName | YES | Section 3, Change 2 |
| T1: CYC 7→8 documented with branch-by-branch breakdown | YES | Section 3, CYC Analysis |
| T2: `Execute(List<...>)` → `Execute(List<...>, Instrument instrument)` | YES | Section 4 |
| T2: Combined gate `!QxGlobalExit \|\| instrument == null` | YES | Section 4, Change 1 |
| T2: CYC hold strategy — fold filter into existing branch (6) | YES | Section 4, CYC Hold Strategy |
| T2: CYC stays at 8 (extended compound, not new branch) | YES | Section 4 — explicitly stated |
| T2: Closes DW-B185-QX-ALL-SCOPE-01 | YES | Section 4 header + Section 13 |
| T3: `OnQuickAllClick` → `gqx.Execute(_instrument)` | YES | Section 5, Change 1 |
| T3: `OnInstrQAll2tClick` → `Execute(targets, _instrument)` | YES | Section 5, Change 2 |
| T3: No null guard at call site (combined gate handles it) | YES | Section 5 — explicitly justified |
| 6 tests T_B186_01..T_B186_06 | YES | Section 6 (all 6 stubs present) |
| NT8-runtime skip pattern for all tests | YES | Section 6 preamble + `SKIP_IF_NT8_RUNTIME_ABSENT` in each stub |
| DNA constraints all 6 addressed | YES | Section 7 |
| 7-scan checklist SCAN-01..07 | YES | Section 11 |
| B185 pattern reference present | YES | Section 2 |
| Deferred DW-B186-DIAG-CLEANUP-01 | YES | Section 13 |
| Deferred DW-B186-WARN-01 | YES | Section 13 |
| AC1..AC6 traceability table | YES | Section 12 |
| Expected build tag stated | YES | Section 12 |
| Implementation order documented | YES | Section 14 |

All 23 spec requirements covered.

---

## 4. DNA Constraint Checks (Per-Rule Assessment)

### Concurrency (P0)

| Rule | Check | Finding |
|------|-------|---------|
| JS-021 | No `lock()` in any proposed code | PASS — Section 7 explicitly states no lock; no lock appears in any code snippet; Section 9 (Threading Model) confirms lock not needed (UI thread origin). |
| JS-021 | No Monitor / Mutex / SemaphoreSlim for state | PASS — none proposed. |
| JS-023 | UI updates not from off-thread without Dispatcher | PASS — Section 9 confirms all calls originate on WPF click handler (UI thread); Dispatcher.InvokeAsync explicitly noted as not required. |

### Type Safety (P0)

| Rule | Check | Finding |
|------|-------|---------|
| JS-001 | No `throw new` in hot paths / gate chain | PASS — no throw statements in any proposed change. Combined gate uses `return;`, not exceptions. |
| JS-002 | No `return null` where value expected | PASS — all modified methods are `void`; every early exit is `return;` (no value returned). |
| JS-003 | No magic string for discriminated state | PASS — no such pattern introduced. |

### Immutability (P1)

| Rule | Check | Finding |
|------|-------|---------|
| JS-008 | No mutable fields on struct; no unfrozen SolidColorBrush | PASS — no new structs, no UI brushes proposed. |
| JS-009 | No `Dictionary<K,V>` for shared/thread-touched collections | PASS — no new dictionaries proposed. |

### Construction (P1)

| Rule | Check | Finding |
|------|-------|---------|
| JS-010 | No public constructor on singleton or signal struct | PASS — `new PttGlobalQuickExit()` is an existing ephemeral-instance pattern (per-click instantiation), not a singleton or signal struct. |

### NT8-Specific Hard Constraints

| Constraint | Finding |
|-----------|---------|
| No `async`/`await` in `OnInitialize`/`OnDestroyed`/`OnWindowCreated` | PASS — no async keywords in any proposed method. |
| No `Account.All` in constructor | PASS — `Account.All` used in `Execute()` body (existing pattern), not constructor. |
| `sealed TradeCopierWindow` not violated | PASS — `TradeCopierWindow` not touched. |
| No `FontFamily` override (SCAN-03) | PASS — no font changes. |
| No hardcoded `#RRGGBB` hex (SCAN-04) | PASS — no color literals. |
| No `CreateOrder` without `PTT-` prefix (SCAN-05) | PASS — no new order creation. |
| `DateTime.UtcNow` (no `DateTime.Now`) (SCAN-06) | PASS — Section 7 explicitly confirms no new DateTime usage. |

### Complexity (P1)

| Method | CYC | Assessment |
|--------|-----|-----------|
| `PttGlobalQuickExit.Execute(Instrument)` | 8 | AT CEILING — acceptable per spec. Full 8-branch breakdown in Section 3. |
| `PttGlobalQuickExit.Execute(List<...>, Instrument)` | 8 | AT CEILING — fold strategy correct; branch (6) extended as compound, CYC unchanged. Breakdown in Section 4. |
| `TradeCopierPanel.OnQuickAllClick` | 1 | Well under ceiling. |
| `TradeCopierPanel.OnInstrQAll2tClick` | 2 | Well under ceiling. |

No method exceeds CYC 8. ✓

### ASCII-only

All new string literals are ASCII:
- `"[PTT-QX-ALL] GlobalQuickExit fired instr="` — ASCII ✓
- `"[PTT-QX-2T-ALL]"` (existing, unchanged) — ASCII ✓

---

## 5. 7-Scan Checklist Presence (Section 11 of Plan)

| Scan | Present in Plan | Command Valid |
|------|----------------|--------------|
| SCAN-01: no new `lock(` | YES | `git diff ... | Select-String "lock\("` ✓ |
| SCAN-02: no new `return null` | YES | `git diff ... | Select-String "return null"` ✓ |
| SCAN-03: no new `async void ` | YES | `git diff ... | Select-String "async void "` ✓ |
| SCAN-04: CYC manual count | YES | Execute(Instrument)=8 and Execute(List,Instrument)=8 documented ✓ |
| SCAN-05: no non-ASCII | YES | `git diff ... | Select-String "[^\x00-\x7F]"` ✓ |
| SCAN-06: no `throw new` | YES | `git diff ... | Select-String "throw new"` ✓ |
| SCAN-07: build | YES | `dotnet build ... | Select-Object -Last 5` ✓ |

Additional AC verification commands also present (T_B186_ pattern, Execute.*Instrument, Guardian + ptt-sync). ✓

---

## 6. Test Plan Assessment

| Test | Spec Requirement | Coverage | NT8 Skip Pattern |
|------|-----------------|----------|-----------------|
| T_B186_01 | Execute has 1 Instrument param (reflection) | DW-B186-QX-ALL-SCOPE-01 AC1 | `SKIP_IF_NT8_RUNTIME_ABSENT` ✓ |
| T_B186_02 | Execute(null) returns immediately (combined gate) | DW-B186-QX-ALL-SCOPE-01 AC2 | `SKIP_IF_NT8_RUNTIME_ABSENT` ✓ |
| T_B186_03 | OnQuickAllClick passes _instrument (reflection) | DW-B186-PANEL-CALLSITES-01 AC3 | `SKIP_IF_NT8_RUNTIME_ABSENT` ✓ |
| T_B186_04 | Execute(List, Instrument) has 2 params (reflection) | DW-B186-QX-ALL-SCOPE-02 AC2 | `SKIP_IF_NT8_RUNTIME_ABSENT` ✓ |
| T_B186_05 | Execute(targets, null) returns immediately (combined gate) | DW-B186-QX-ALL-SCOPE-02 | `SKIP_IF_NT8_RUNTIME_ABSENT` ✓ |
| T_B186_06 | OnInstrQAll2tClick passes _instrument (reflection) | DW-B186-PANEL-CALLSITES-01 AC4 | `SKIP_IF_NT8_RUNTIME_ABSENT` ✓ |

All 6 tests map to spec ACs. xUnit `[Fact]` attribute used (JS testing standard). ✓

---

## 7. Scope Creep Check

Items explicitly deferred to B187+ and NOT implemented in the plan:
- DW-B186-DIAG-CLEANUP-01 — deferred, no implementation steps for it in T1/T2/T3. ✓
- DW-B186-WARN-01 — deferred, no fix proposed. ✓
- Carried-forward B185/B184/B183 items — listed for awareness only, not touched. ✓

No scope creep detected. ✓

---

## 8. NT8 API Grounding Check

Section 8 of the plan cross-references all APIs against existing source lines:

| API | Source Evidence |
|-----|----------------|
| `Instrument` (NinjaTrader.Cbi) | Confirmed at line 306 of `PttGlobalQuickExit.cs` |
| `instrument.FullName` | Confirmed at line 468 |
| `pos.Instrument?.FullName` | Confirmed at lines 226-227 |
| `Account.All` | Confirmed at lines 51, 140 |
| `NinjaTrader.Code.Output.Process` | Confirmed throughout |
| `NinjaTrader.NinjaScript.PrintTo.OutputTab1` | Confirmed throughout |

NT8 API surface is grounded in existing source. No StrategyBase-only APIs claimed for AddOnBase context. ✓

---

## 9. Final Checklist Summary

| Item | Check | Result |
|------|-------|--------|
| 1 | LANE-SPLIT GATE: result present and valid | PASS |
| 2 | T1 coverage: gate + filter + log + CYC 7→8 | PASS |
| 3 | T2 coverage: CYC hold (fold into branch 6) + gate + signature | PASS |
| 4 | T3 coverage: both call sites documented | PASS |
| 5 | 6 tests T_B186_01..06 with NT8-runtime skip | PASS |
| 6 | DNA: JS-021, JS-001, JS-002, JS-033, ASCII, CYC≤8 | PASS |
| 7 | 7-scan checklist SCAN-01..07 present | PASS |
| 8 | B185 PttBeSwapAll.Execute pattern reference | PASS |
| 9 | Deferred DW-B186-DIAG-CLEANUP-01 + DW-B186-WARN-01 | PASS |
| 10 | No scope creep | PASS |

---

## REVIEW_PASS

Plan is complete, internally consistent, and fully compliant with the B186 spec and RULES_CATALOG.
**Phase 3 is unblocked.** ptt-architect may proceed to `04-tickets.md`.

---

*Written by: ptt-plan-reviewer (Phase 2)*
*Epic: B186*
*Baseline: 0c27cd88 (B185-POST)*
*Next phase: ptt-architect -> 04-tickets.md*
