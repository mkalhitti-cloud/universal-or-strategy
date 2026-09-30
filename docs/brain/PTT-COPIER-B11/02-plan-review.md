# PTT-COPIER-B11 -- Plan Review
# Reviewer: ptt-plan-reviewer
# Phase: 2 (Architecture Review)
# Date: 2026-07-11 (REVISED -- Cycle 2 of 2)
# Plan under review: docs/brain/PTT-COPIER-B11/02-architecture-plan.md
# Verdict: REVIEW_PASS

---

## Verdict: REVIEW_PASS

**Violations from cycle 1: 2. Both resolved. Violations remaining: 0.**

The revised plan (status: PLAN_COMPLETE, V1+V2 fixes applied 2026-07-11) addresses both
cycle-1 violations fully. All specific fixes verified. All original criteria pass.
Phase 3 (ticket generation) is UNBLOCKED.

---

## Cycle-1 Violation Resolution Verification

### V1 -- Flatten/FlattenAll discrepancy (RESOLVED)

All `Flatten` references updated to `FlattenAll` across all required locations:

| Location | Before | After | Verified |
|----------|--------|-------|----------|
| §1 scope line 26 | `Ctrl+Shift+F=Flatten` | `Ctrl+Shift+F=FlattenAll` | PASS |
| §4.2 DispatchShortcut comment | `F=Flatten` | `F=FlattenAll` | PASS |
| §6 threading table | `Engine calls (Trim, Flatten, Cancel, BreakEven)` | `Engine calls (Trim, FlattenAll, Cancel, BreakEven)` | PASS |
| §8 NT8 API table | `CopyEngine.Trim/Flatten/...` | `CopyEngine.Trim/FlattenAll/...` + spec line 4750 citation | PASS |

Spec line 4750 (`CopyEngine.FlattenAll(rule)` for `Ctrl+Shift+F`) is now explicitly cited
in §8. Reconciliation complete.

### V2 -- `_sim101KeyDiag` leak guard (RESOLVED)

All six specific items from the Director's revision brief verified:

| Item | Requirement | Verified |
|------|-------------|----------|
| 1 | `_sim101KeyDiag` documented as class-level `KeyEventHandler` field | PASS -- §2 V2 note declares `private KeyEventHandler _sim101KeyDiag;`; §3 component list entry `TradeCopierAddOn._sim101KeyDiag | Field | NEW`; §12 ADD line lists field |
| 2 | `RemoveSim101(Chart chart)` documented with implementation | PASS -- §2 V2 note contains full method body with call-contract comments; §4.1 contains signature with CYC=2 annotation |
| 3 | SIM101 PASS path calls `RemoveSim101()` before any other action | PASS -- §2 Step 3 table PASS row: "Remove `_sim101KeyDiag` from `chart.PreviewKeyDown` **first**", then `HookKeyShortcut`; §2 V2 order-of-operations (PASS path) explicitly lists `RemoveSim101(chart)` as step 1 |
| 4 | SIM101 FAIL path calls `RemoveSim101()` before any other action | PASS -- §2 Step 3 table FAIL row: "Remove `_sim101KeyDiag` from `chart.PreviewKeyDown` **first**", then VERIFIED_NOT_FEASIBLE; §2 V2 order-of-operations (FAIL path) lists `RemoveSim101(chart)` as step 1 |
| 5 | `_sim101KeyDiag` is always null after SIM101 regardless of outcome | PASS -- §2 V2 note states explicitly: "`_sim101KeyDiag` is ALWAYS null after leaving the SIM101 phase, regardless of outcome." `RemoveSim101` body nulls the field unconditionally |
| 6 | `UnhookKeyShortcut` handles PRODUCTION handler only (not `_sim101KeyDiag`) | PASS -- §4.1 UnhookKeyShortcut doc: "Unwire chart.PreviewKeyDown (PRODUCTION handler only)... Does NOT remove `_sim101KeyDiag` -- that is RemoveSim101's responsibility." |

---

## Review Criteria Checklist (Full Re-Check)

| # | Criterion | Result | Notes |
|---|-----------|--------|-------|
| 1 | Plan covers both required tickets (DW-B11-HK-01, DW-B11-HK-02) | PASS | §1 T1 covers DW-B11-HK-01; §1 T2 covers DW-B11-HK-02. |
| 2 | SIM101 protocol documented: logging-only handler test BEFORE production implementation | PASS | §2 fully documents Step 1 (logging-only handler), Step 2 (execute test), Step 3 (evaluate result). |
| 3 | VERIFIED_NOT_FEASIBLE path documented: if SIM101 fails, both tickets defer, block still PIPELINE_COMPLETE | PASS | §11 documents the FAIL path; DW-B10-01 through DW-B10-04 still close independently. |
| 4 | All 4 backlog items addressed (DW-B10-01 through DW-B10-04) | PASS | §10: DW-B10-01 CLOSED T1, DW-B10-02 CLOSED T2, DW-B10-03 CLOSED T2, DW-B10-04 CLOSED T1. |
| 5 | Shelved items confirmed not in scope (DW-B9-01, DW-B9-03) | PASS | §1, §10, §14 all mark both SHELVED carry to B12. |
| 6a | No lock() | PASS | §6: all new code on WPF UI thread; §15 pre-flight PASS. No lock() in any code snippet. JS-021: CLEAN. |
| 6b | No async void (except FlashBeFired) | PASS | §8 JS-033: CLEAN. No async void in any new handler. |
| 6c | No return null | PASS | §8 JS-002: CLEAN -- LoadAtmTemplates returns empty array, GetAtmTemplatesDirectory returns string, all guard paths are `return;` (void). |
| 6d | CYC <= 8 per method | PASS | §9 CYC table: all methods 1--5. Highest is DispatchShortcut=5. All within limit. |
| 7a | No volatile double | PASS | N/A -- no doubles in B11. |
| 7b | No volatile bool (new, UI-thread) | PASS | No new volatile fields added; deleted `_gap002TickCount` volatile is removed. |
| 7c | No Math.Clamp | PASS | §8: explicitly acknowledged, Math.Max pattern used instead. |
| 7d | No { get; init; } | PASS | No record/init properties in plan. |
| 8 | chart.PreviewKeyDown unhook in OnDestroyed() documented (leak guard) | PASS | PRODUCTION handler: §4.1 UnhookKeyShortcut + §4.1 OnWindowDestroyed modification. SIM101 diag handler: §2 V2 note + §4.1 RemoveSim101. Both hooks documented with explicit unhook paths. |
| 9 | All keyboard handler calls target EXISTING CopyEngine methods only | PASS | §4.2 DispatchShortcut: Trim, FlattenAll, CancelPendingEntries, BreakEven -- all documented as existing public methods. §8 confirms "no new engine code." §7 NT8 API table calls out spec line 4750 alignment. |
| 10 | Plan cites spec requirement IDs | PASS | §13 cites DW-B11-HK-01, DW-B11-HK-02, DW-B10-01 through DW-B10-04, SIM101-gate. §8 now cites spec line 4750 for FlattenAll. |

---

## Spec Coverage Matrix

| Spec Requirement | Addressed? | Plan Section |
|-----------------|-----------|--------------|
| DW-B11-HK-01: chart.PreviewKeyDown shortcut layer, 4 keys | YES | §1 T1, §4.2, §5.1 |
| DW-B11-HK-02: focus-independence verification + ATM template ComboBox | YES | §1 T2, §4.2 (ATM), §5.3 |
| SIM101 gate: logging-only handler first | YES | §2 |
| VERIFIED_NOT_FEASIBLE fallback if SIM101 fails | YES | §11 |
| Unhook PreviewKeyDown in cleanup (spec line 4792) | YES | Production handler: §4.1 UnhookKeyShortcut. Diag handler: §2 V2 + §4.1 RemoveSim101. Both paths complete. |
| Only existing CopyEngine methods called | YES | Trim/FlattenAll/Cancel/BreakEven all cited as existing. Spec line 4750 reconciled. |
| DW-B10-01: remove diag scaffolding | YES | §1 T1, §3 DELETE rows, §12 T1 |
| DW-B10-02: 3 AtrSizingEngine xUnit tests | YES | §1 T2, §4.4, §12 T2 |
| DW-B10-03: Window Arm BE column | YES | §4.3, §5.4, §12 T2 |
| DW-B10-04: NT8_ADDON_KNOWLEDGE.md update | YES | §1 T1, §12 T1 |
| DW-B9-01 shelved | YES | §1, §10, §14 |
| DW-B9-03 shelved | YES | §1, §10, §14 |

---

## Jane Street Rule Scan

| Rule | Scan Pattern | Result |
|------|-------------|--------|
| JS-021 (no lock) | `lock\(` | CLEAN |
| JS-001 (no throw in hot path) | `throw new` | CLEAN |
| JS-002 (no return null) | `return null` | CLEAN -- LoadAtmTemplates returns empty array on IO fail |
| JS-023 (volatile for state) | new `volatile` fields | CLEAN -- no new volatile fields |
| JS-033 (no async void) | `async void` | CLEAN -- Dispatcher.InvokeAsync lambda is not async void |
| NT8-003 (no volatile double) | `volatile double` | CLEAN |
| Math.Clamp ban | `Math.Clamp` | CLEAN |
| Hardcoded hex colors | `#[0-9A-Fa-f]{6}` | CLEAN |
| FontFamily override | `FontFamily` | CLEAN |
| No `{ get; init; }` | `init;` | CLEAN |
| DateTime.Now | `DateTime.Now` | CLEAN |
| async/await in lifecycle | async in OnWindowDestroyed/OnInitialize | CLEAN |

---

## NT8 Constraint Scan

| Check | Result |
|-------|--------|
| No `volatile double` | PASS |
| No `volatile bool` (new, UI-thread) | PASS |
| No `Math.Clamp` | PASS |
| No `{ get; init; }` | PASS |
| No `abstract record` / `sealed record` | PASS |
| No `ImmutableDictionary` | PASS |
| `async/await` in `OnWindowDestroyed` | PASS -- unhook calls are synchronous |
| `Account.All` in constructor | PASS -- not used |
| sealed `TradeCopierWindow` | PASS -- no class declarations changed |
| `FontFamily` override | PASS |
| `CreateOrder` without PTT- prefix | PASS -- no new CreateOrder calls |
| `DateTime.Now` (vs UtcNow) | PASS |

---

## Non-Blocking Observations (Informational Only -- NOT violations)

These are implementation notes for the engineer; they do NOT affect the REVIEW_PASS verdict.

1. **`static` modifier on `_sim101KeyDiag` field**: The plan declares
   `private KeyEventHandler _sim101KeyDiag` without `static`, but `RemoveSim101` and the
   accessor methods are declared `private static`. Since `TradeCopierAddOn` appears to use
   exclusively static members (matching the `_panels`, `_keyHandlers`, `_clickHandlers`
   pattern), the engineer should declare the field as
   `private static KeyEventHandler _sim101KeyDiag`. The plan's intent is unambiguous;
   this is an editorial omission in the plan snippet only.

2. **CYC count for `OnRuleArmBe`**: §9 lists decision points as `3` but §4.3 identifies
   4 guards (tag null, name empty, instr null, leader null). The CYC=4 value in §9 is
   internally consistent with the plan's counting convention. Even if CYC is 5, it remains
   within the <= 8 limit.

---

## Decision

| Gate | Result |
|------|--------|
| V1 (Flatten/FlattenAll) | RESOLVED |
| V2 (_sim101KeyDiag leak guard) | RESOLVED |
| All 12 original criteria | PASS |
| Jane Street DNA scan | CLEAN |
| NT8 constraint scan | CLEAN |
| Spec coverage | COMPLETE |

**REVIEW_PASS**

Phase 3 (ticket generation) is UNBLOCKED.
The ptt-architect may proceed to write `docs/brain/PTT-COPIER-B11/04-tickets.md`.

---

*Cycle 2 of 2. This review is final. No further review cycles are permitted.*
*Reviewed by ptt-plan-reviewer against docs/standards/jane-street/RULES_CATALOG.md.*
