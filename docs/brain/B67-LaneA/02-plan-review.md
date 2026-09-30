# B67-LaneA Plan Review
## DW-B67-01 — FlattenOneAccount: cancel follower ATM+QX brackets before market close order

**Block**: B67-LaneA
**Phase**: 2 (Plan Review)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-13
**Input**: `docs/brain/B67-LaneA/02-architecture-plan.md`
**References**: `docs/standards/jane-street/RULES_CATALOG.md`, DW-B67-01 spec requirements

---

## Review Result

**REVIEW_PASS**

All 10 checklist items pass. No RULES_CATALOG.md violations found. No spec requirements
unaddressed. Plan is cleared for Phase 3 (ticket generation).

---

## Section A — Checklist Results (RC-01 through RC-10)

| Check | ID | Description | Result | Notes |
|---|---|---|---|---|
| RC-01 | Code location coverage | Plan identifies all 3 locations: FlattenOneAccount body, FlattenOneAccount comment, CancelQxBrackets comment | **PASS** | Sections 3.1, 3.2, 3.3 each address one location explicitly |
| RC-02 | CancelQxBrackets placement | Inserted after `if (pos == null \|\| pos.Quantity == 0)` guard block, before `var action` ternary | **PASS** | Section 3.2 and 3.4 target shape confirm correct sequence |
| RC-03 | CYC analysis completeness | BEFORE=3, AFTER=4; all 4 segments enumerated | **PASS** | Section 5 tables are complete; project convention vs. McCabe difference noted appropriately |
| RC-04 | JS-001/002/021 citations | All three rules cited explicitly with PASS verdict | **PASS** | Section 6 and inline comment in Section 3.1 cite all three |
| RC-05 | Test contracts (T_B67_01–04) | All 4 [Fact] test names and Assert contracts documented | **PASS** | Section 7: method names, setup, and Assert blocks present for each |
| RC-06 | 7-scan checklist (S1–S7) | All 7 scans present with commands and pass conditions | **PASS** | Section 8: S1-lock, S2-throw new, S3-CYC, S4-ASCII, S5-build, S6-test, S7-SHA-256 |
| RC-07 | NT8 evidence | NT8 cancel-before-flatten precedent cited from @2Custom-0909edcc FlattenPositionByName V8.31 | **PASS** | Section 2 and Section 3.1 comment block both carry the NT8 reference |
| RC-08 | File scope | Only CopyEngine.cs and CopyEngineTests.cs; no other files | **PASS** | Section 4 explicitly states "No other files are touched" |
| RC-09 | DW-B67-01 closure | DW-B67-01 marked CLOSED in Section 9 | **PASS** | "CLOSED — implemented in B67-LaneA T1" |
| RC-10 | RULES_CATALOG.md violations | Zero violations found in new/modified code proposed by plan | **PASS** | See Section B below |

---

## Section B — RULES_CATALOG.md Violation Scan

### P0 Rules (auto-FAIL triggers)

| Rule ID | Rule | Assessment | Result |
|---|---|---|---|
| JS-001 | No throw new Exception in hot path | No `throw` added. Existing `catch (Exception ex)` surfaces via `StatusUpdate?.Invoke`. | PASS |
| JS-002 | No return null | Both modified methods are `void`. Not applicable. | PASS |
| JS-010 | No public constructor on singleton/signal struct | No new types introduced. | N/A |
| JS-015 | No raw string params without parse | No new string-typed method parameters introduced. | N/A |
| JS-021 | No lock() | Zero `lock()` in all new/modified code. NT8 Cbi calls run on dispatcher thread. | PASS |
| JS-022 | Actor pattern for stateful concurrency | No new concurrent state. Single-threaded NT8 dispatcher chain. | N/A |
| JS-033 | No async void (non-event-handler) | No async/await in any modified code. | PASS |

### P1 Rules (auto-FAIL triggers)

| Rule ID | Rule | Assessment | Result |
|---|---|---|---|
| JS-008 | Readonly struct / no mutable fields | No new structs introduced. | N/A |
| JS-009 | No Dictionary for shared/thread-touched collection | No new collections. Pre-existing `List<Order>` in `CancelQxBrackets` (untouched). | N/A |
| JS-023 | UI update from off-thread needs Dispatcher.InvokeAsync | `StatusUpdate?.Invoke` is existing pattern; marshaling is a TradeCopierPanel concern (plan correctly identifies this). | N/A |

### NT8 Hard Constraints

| Constraint | Assessment | Result |
|---|---|---|
| async/await in OnInitialize/OnDestroyed/OnWindowCreated | None present. | PASS |
| Account.All in constructor | Not used. | PASS |
| sealed TradeCopierWindow | Not modified. | N/A |
| FontFamily override | Not present. | N/A |
| Hardcoded #RRGGBB hex | Not present in new/modified code. | PASS |
| CreateOrder without PTT- prefix | Order name is `"PTT-Flatten"` (existing, unchanged). | PASS |
| DateTime.Now (not UtcNow) | `DateTime.MaxValue` used (not DateTime.Now). | PASS |
| ASCII-only | All new string literals verified ASCII-only in plan Section 6. | PASS |

### CYC Threshold

| Method | CYC | Threshold | Result |
|---|---|---|---|
| FlattenOneAccount (updated) | 4 | ≤ 8 | PASS |
| CancelQxBrackets (unchanged) | 6 | ≤ 8 | PASS |

---

## Section C — Spec Requirement Coverage Matrix

| # | Requirement | Addressed? | Plan Section |
|---|---|---|---|
| 1 | `CancelQxBrackets(acc, instrument)` called AFTER pos null/qty guard, BEFORE `acc.CreateOrder` | YES | Section 3.2, 3.4 |
| 2 | Comment block cites B67, DW-B67-01, NT8 precedent, CYC=4, JS-021, JS-001, JS-002 | YES | Section 3.1 |
| 3 | `CancelQxBrackets` comment at line 443 adds `FlattenOneAccount` as caller | YES | Section 3.3 |
| 4 | No new helpers; no logic changes to CancelQxBrackets or any other method | YES | Sections 2, 4 |
| 5 | 4 [Fact] tests: T_B67_01..T_B67_04 with specific verification contracts | YES | Section 7 |
| 6 | CYC of updated FlattenOneAccount = exactly 4 | YES | Section 5 |
| 7 | JS-DNA: no lock(), no throw new, ASCII-only in new/modified code | YES | Section 6 |
| 8 | Files changed: CopyEngine.cs + CopyEngineTests.cs ONLY | YES | Section 4 |
| 9 | Deploy step: SHA-256 match required after engineer copy | YES | Section 8, S7 |

All 9 requirements addressed. No gaps.

---

## Section D — Advisory Notes (Non-Blocking)

**ADV-01 (informational)**: The plan notes that under strict McCabe CYC, the `CancelQxBrackets`
call does not constitute a branch and the strict McCabe value would remain 3. The project comment
convention counts each major code segment (CYC=4). Both are fully within the ≤8 threshold.
This discrepancy is correctly disclosed. No action required.

**ADV-02 (informational)**: Section 7 notes that `[assembly: InternalsVisibleTo("CopyEngineTests")]`
must be confirmed present from B28 T1. The engineer must verify this attribute exists before
writing test stubs that call `internal` methods. If absent, it is a 1-line addition to the
existing test project setup — not a scope change.

**ADV-03 (informational)**: T_B67_01 requires recording call order between `CancelQxBrackets`
and `acc.CreateOrder`. The plan describes wrapping/overriding these in a test subclass. The
engineer should confirm `FlattenOneAccount` is accessible for subclassing or that the test can
use the existing stub pattern. Not a blocker — the plan correctly identifies this requirement.

---

*Review status: REVIEW_PASS — plan cleared for Phase 3 (ticket generation).*
*Reviewer: ptt-plan-reviewer | Block: B67-LaneA | Date: 2026-08-13*
