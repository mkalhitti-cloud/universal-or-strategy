# DW-B159-KS (K-Suite: K2 + K3 + K5) — Plan Review
<!-- status: REVIEW_PASS -->
<!-- epic: DW-B159-KS -->
<!-- phase: 2 -->
<!-- author: ptt-plan-reviewer -->
<!-- date: 2026-10-01 -->

---

## Review Summary

| Field | Value |
|-------|-------|
| Plan file | `docs/brain/DW-B159-KS/02-architecture-plan.md` |
| Backlog ref | `docs/brain/B159/06-deferred-backlog.md` |
| Source verified | `src/PropTraderTools/CopyEngine.cs` (grep spot-checks) |
| Violations found | **0** |
| Result | **REVIEW_PASS** |

---

## 1. LANE-SPLIT GATE Compliance

| Question | Plan Answer | Reviewer Verdict |
|----------|------------|-----------------|
| Q1 — Same method or within 50 lines? | NO | PASS — T1@3196, T2 const@~323 + usage@7192, T3 separate file. All geographically independent. Source-confirmed. |
| Q2 — Fix B design depends on Fix A? | NO | PASS — const extraction is independent of comment update. T3 does not depend on T1 comment text or T2 const name. |
| Q3 — Standalone value if other blocked? | YES (all three) | PASS — K5 is doc correctness; K3 is maintainability; K2 is test coverage. Each closes a distinct deferred item. |
| Q4 — Independent SIM verification path? | YES (all three) | PASS — T1/T2 use targeted grep commands; T3 uses `dotnet test`. All independent. |
| **Gate result stated in plan** | LANES-APPROVED | PASS — explicitly stated at Section 1 conclusion. |
| **Sequential execution mandated** | YES — T1→T2→T3 | PASS — stated in gate section and Section 9. |

---

## 2. Ticket Scope Matrix

| Ticket | Deferred Item | Files | Scope Correct | Verdict |
|--------|--------------|-------|---------------|---------|
| T1 (K5) | DW-B159-K5 stale comment | `CopyEngine.cs` only | Comment-only, single line (3196), no logic | PASS |
| T2 (K3) | DW-B159-K3 magic literal | `CopyEngine.cs` only | Const insertion + 1 literal replacement; K3-UI deferred explicitly | PASS |
| T3 (K2) | DW-B159-K2 unit tests | `CopyEngineTests.cs` only | 14 [Fact] methods, no production code change | PASS |

No ticket scope exceeds stated bounds. K3-UI settings panel wiring is explicitly deferred (plan Section 3 T2, verification check confirms 0 matches in `PropTraderToolsSettingsPanel.cs`).

---

## 3. JS Rule Compliance Matrix

| Rule | T1 (K5) | T2 (K3) | T3 (K2) |
|------|---------|---------|---------|
| JS-CYC (all methods ≤8) | PASS — comment only, CYC delta=0 | PASS — const adds zero branches, CYC delta=0, TryArmBePriceRetry CYC=5 unchanged | PASS — all test methods CYC=1-2 |
| JS-001 (no throw) | PASS — no code | PASS — no code change in execution path | PASS — Assert.* is xUnit contract, not production throw |
| JS-002 (no null return where value expected) | N/A — comment only | N/A — void method, no return changed | N/A — void test methods |
| JS-021 (no lock/Monitor/Mutex) | PASS — no new code | PASS — `private const` is compile-time literal, zero threading implications | PASS — ConcurrentDictionary local to each test, no lock |
| JS-042 (ASCII-only) | PASS — `(HasOpenPosition, line 5507)` is 7-bit ASCII | PASS — `BeRetryMaxAttempts` identifier and all comment text are ASCII | PASS — all string literals (`"PTT-BE-Stop-1"`, `"Sim102"`, `"PTT-QX-Stop-1"`) are ASCII |

Additional DNA rules checked (no violations):
- JS-009: No `Dictionary<K,V>` for thread-touched collections — T3 uses `ConcurrentDictionary` locally. PASS.
- JS-008: No mutable struct fields, no unFrozen SolidColorBrush. PASS (not applicable to this block).
- JS-010: No public constructor on singleton or signal struct. PASS (not applicable).
- NT8: No `async/await` in lifecycle methods. No `Account.All` in constructor. No `DateTime.Now`. No `FontFamily` override. No hardcoded `#RRGGBB`. No `CreateOrder` without PTT- prefix. All PASS.
- NT8 API: `AtmStrategyCreate()` and `AtmStrategyChangeStopTarget()` correctly noted as StrategyBase-only — not used. PASS.

---

## 4. K5 Stale Comment — Identification and Fix

| Check | Verdict |
|-------|---------|
| Original stale comment text (`// Delegates to HasOpenPosition (line 5273)`) still present in source? | PASS — plan correctly identifies it was overwritten by B160-B167 expansion. Grep confirms no match for "line 5273". |
| Current HasOpenPosition declaration line identified correctly? | PASS — plan grep confirms line 5507. Source grep confirms line 5507 (`private bool HasOpenPosition(Account acc, Instrument instrument)`). |
| Remaining K5 anchor comment identified? | PASS — plan identifies line 3196: `// Signal 1 (HasOpenPosition): position Quantity > 0 (normal case, B159).` Source confirms this exact text at line 3196. |
| Exact fix specified? | PASS — append `, line 5507` → `// Signal 1 (HasOpenPosition, line 5507): position Quantity > 0 (normal case, B159).` Minimal, targeted. |
| Backlog line-number discrepancy (5273 vs 5294 vs 5507) resolved? | PASS — plan supersedes prior stale numbers with grep-verified 5507. Self-consistent. |

---

## 5. K3 Const Extraction — Change Sites

| Check | Verdict |
|-------|---------|
| Const name correct? | PASS — `BeRetryMaxAttempts` matches plan spec exactly |
| Const value correct? | PASS — `= 3` matches deferred backlog and production literal |
| Const visibility correct? | PASS — `private` (consumed only by TryArmBePriceRetry in same class; no cross-class exposure needed) |
| Change site 1 present? | PASS — const declaration insertion at ~line 323 region (near `_pendingBePriceRetry` field). Source confirms anchor at lines 324-335. |
| Change site 2 present? | PASS — literal replacement at line 7192: `if (attempts >= 3)` → `if (attempts >= BeRetryMaxAttempts)`. Source grep confirms literal `3` at line 7192. |
| Exactly 2 change sites? | PASS — declaration + usage. No other occurrences of attempt-cap literal `3` in this method. |
| BeRetryMaxAttempts absent from source? | PASS — grep returns 0 matches. Safe to introduce. |
| K3-UI wiring absent from plan scope? | PASS — explicitly deferred in const comment and scope boundary text. |

---

## 6. K2 Test Class — 14 Method Names

All 14 method names verified against canonical list in `docs/brain/B159/06-deferred-backlog.md` (DW-B159-K2).

**T1 group (6 tests):**

| # | Method Name | Present in Plan | Exact Match |
|---|-------------|----------------|-------------|
| 1 | `ShouldSkipFollower_FollowerHasOpenPosition_ReturnsTrue` | YES | ✅ |
| 2 | `ShouldSkipFollower_FollowerNoOpenPosition_ReturnsFalse` | YES | ✅ |
| 3 | `IsFollowerPositionBlocked_NullAccount_ReturnsFalse` | YES | ✅ |
| 4 | `IsFollowerPositionBlocked_NullInstrument_ReturnsFalse` | YES | ✅ |
| 5 | `IsFollowerPositionBlocked_AccountWithNoPosition_ReturnsFalse` | YES | ✅ |
| 6 | `IsFollowerPositionBlocked_AccountWithOpenPosition_ReturnsTrue` | YES | ✅ |

**T2 group (8 tests):**

| # | Method Name | Present in Plan | Exact Match |
|---|-------------|----------------|-------------|
| 7 | `TryFireBePriceRetry_CancelledOrderWithRetryKey_HasOpenPosition_StillAtNonBePrice_CallsSubmitBeStop` | YES | ✅ |
| 8 | `TryFireBePriceRetry_CancelledOrderWithRetryKey_AlreadyAtBePrice_NoSubmitBeStop` | YES | ✅ |
| 9 | `TryArmBePriceRetry_AttemptsAtCap_NoNewEntry` | YES | ✅ |
| 10 | `TryFireBePriceRetry_WorkingOrder_RemovesFromDict` | YES | ✅ |
| 11 | `TryFireBePriceRetry_NonPttBeStopPrefix_ExitsEarly` | YES | ✅ |
| 12 | `TryArmBePriceRetry_ValidInputsFirstAttempt_AddsEntryToDict` | YES | ✅ |
| 13 | `TryFireBePriceRetry_CancelledPttBeStop_KeyNotInDict_NoSubmitBeStop` | YES | ✅ |
| 14 | `StillAtNonBePrice_WorkingStopAtOriginalPrice_ReturnsTrue` | YES | ✅ |

All 14 present. Zero [Skip]. All declared as `[Fact] public void`. ✅

---

## 7. Verification Checklists

| Ticket | Checklist Present | Commands Specified | Verdict |
|--------|------------------|-------------------|---------|
| T1 (K5) | YES (Section 8) | grep for new annotation; grep for absence of stale numbers (5273, 5294); CYC manual check; ASCII scan | PASS |
| T2 (K3) | YES (Section 8) | grep for const (2 matches expected); grep for literal gone (0 matches); CYC check; no-lock check; ASCII; K3-UI absent | PASS |
| T3 (K2) | YES (Section 8) | grep for class; grep for 14 [Fact]; grep for no [Skip]; dotnet test; ASCII; no lock | PASS |

---

## 8. Execution Order

| Check | Verdict |
|-------|---------|
| T1→T2→T3 sequential stated? | PASS — Section 9 flowchart explicit |
| Rationale for T1/T2 sequential (same file)? | PASS — "T1 and T2 both modify CopyEngine.cs. Execute them sequentially." |
| Parallel execution of T1/T2 permitted? | PASS — NO parallel permitted; plan is unambiguous |
| ptt-sync.ps1 after each .cs edit? | PASS — present in Section 9 |
| deploy-sync.ps1 before push? | PASS — present in Section 9 |
| Diff size estimate within 10,000 char DIFF GUARD? | PASS — ~80 + ~250 + ~4000 = ~4330 chars, well under limit |

---

## 9. CYC Delta Verification

| Ticket | Method | CYC Before | CYC After | Delta | Verdict |
|--------|--------|-----------|-----------|-------|---------|
| T1 (K5) | IsFollowerPositionBlocked | 8 | 8 | 0 | PASS |
| T1 (K5) | (comment line 3196 only) | — | — | 0 | PASS |
| T2 (K3) | TryArmBePriceRetry | 5 | 5 | 0 | PASS — literal→const replacement preserves branch semantics |

---

## 10. NT8 Runtime Types in T3

| Check | Verdict |
|-------|---------|
| `new CopyEngine()` used? | PASS — explicitly prohibited; plan states Agile.NET cctor risk |
| `CopyEngine.Instance` used? | PASS — not used |
| NT8 `Account` instantiated? | PASS — no `new Account()`. Tests use string key `"Sim102"` as BCL stand-in. |
| NT8 `Instrument` instantiated? | PASS — no `new Instrument()`. Not instantiated. |
| NT8 `Order` instantiated? | PASS — no `new Order()`. BCL simulation uses dict keys only. |
| WPF/NT8 UI types? | PASS — none |
| Structural reflection uses type metadata only? | PASS — `typeof(CopyEngine).GetMethod(...)` — no instance |
| Test 3 reflection invoke concern resolved? | PASS — plan provides corrective NOTE: use BCL simulation for null-guard test, not reflection invoke |

---

## 11. Spec Coverage Matrix

| Requirement (from backlog) | Plan Section | Addressed? |
|---------------------------|-------------|-----------|
| K5: Correct stale line number in comment | Section 3 T1 | YES — exact change specified |
| K3: Extract magic literal `3` to named const | Section 3 T2 | YES — `BeRetryMaxAttempts = 3`, 2 change sites |
| K3: Settings panel wiring (K3-UI) | Section 3 T2 scope boundary | YES — explicitly deferred, in-scope excluded |
| K2: 14 xUnit [Fact] tests in B159KSuiteTests | Section 3 T3, Section 5 | YES — all 14 names, pure-BCL pattern |
| K2: No [Skip] | Section 3 T3 | YES — "no [Skip]" stated |
| K2: Pure BCL stubs (no NT8 instance) | Section 3 T3 | YES — Agile.NET cctor warning, BCL-only allocations |
| K1 (SIM gate re-run) | Section 10 | Correctly noted as remaining OPEN (out of scope for this block) |
| K4 (intendedBePrice dependency) | Section 10 | Correctly noted as remaining OPEN (out of scope) |

All in-scope requirements addressed. Out-of-scope items correctly flagged as OPEN in Section 10.

---

## 12. Violations Found

**None.**

| Rule ID | Description | Location | Verdict |
|---------|-------------|----------|---------|
| (none) | — | — | — |

---

## Final Verdict

**REVIEW_PASS**

Zero violations. All ten review criteria pass:

1. LANE-SPLIT GATE result present and all Q1-Q4 answers correct and source-verified ✅
2. All 3 ticket scopes within stated bounds ✅
3. JS-CYC, JS-001, JS-002, JS-021, JS-042 all PASS for all tickets ✅
4. K5: stale comment correctly identified and exact fix specified ✅
5. K3: `private const int BeRetryMaxAttempts = 3;` with exactly 2 change sites ✅
6. K2: all 14 [Fact] method names present, exact match, no [Skip], pure-BCL stub pattern ✅
7. Verification checklists present for all 3 tickets ✅
8. Execution order T1→T2→T3 sequential, parallel explicitly prohibited ✅
9. CYC delta = 0 for T1 and T2 ✅
10. No NT8 runtime types in T3 test stubs ✅

Plan is cleared for Phase 3 (ticket generation).

---

*Plan review produced by ptt-plan-reviewer. Phase 2. Epic DW-B159-KS. Date 2026-10-01.*
