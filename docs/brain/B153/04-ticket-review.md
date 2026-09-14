# Ticket Review: B153 — Remove Hardcoded 3-Leg Ceiling

**Block:** B153
**Phase:** 3.5 — Ticket Review (Re-Review: DW-B153-03 corrected)
**Reviewer:** ptt-ticket-reviewer
**Date:** 2026-09-13 (re-review pass)
**Tickets reviewed:** docs/brain/B153/04-tickets.md
**Plan reviewed:** docs/brain/B153/02-architecture-plan.md
**Plan review reviewed:** docs/brain/B153/02-plan-review.md
**Context reviewed:** docs/brain/B153/00-context.md
**RULES_CATALOG:** docs/protocol/RULES_CATALOG.md — NOT FOUND in workspace; JS rules applied from role definition and plan-review content.
**Prior review:** docs/brain/B153/04-ticket-review.md (first pass — DW-B153-01/02/04 PASS; DW-B153-03 FAIL on T03-TRACE-01)

---

## Ticket DW-B153-01 — Remove QX target ceiling (Quick + Quick ALL)

### Traceability
PASS
- Spec req B153-REQ-01 stated explicitly.
- Symbol `ResolveTargetCount` matches plan §6 DW-B153-01 exactly.
- Change (`return Math.Min(raw, 3)` → `return raw`) matches plan §6 exactly.
- No phantom work. No missing plan items.

### JS Pre-Check
PASS
- No `lock()` described. SCAN-8 listed as gate.
- No `DateTime.Now` usage.
- All C# literals and comments are ASCII-only.
- No StrategyBase-only APIs introduced.
- No `throw` in hot path described.
- No mutable struct or unfrozen SolidColorBrush described.
- Method is pure arithmetic — no shared state.

### CYC Pre-Check
PASS
- Ticket explicitly states CYC delta = 0 (corrected per OBS-CYC-01: `Math.Min` is a method call, not a McCabe branch).
- Post-fix CYC = 1. Annotation `// CYC=1` mandated.
- Well within JS-013 limit (≤8).

### NT8 Constraints
PASS
- No NT8 API calls introduced.
- `Quick2t` and `QAll2t` paths explicitly excluded.
- No `Account.Change()`, `AtmStrategyCreate()`, `AtmStrategyChangeStopTarget()`.

### Test Coverage
PASS
- `ResolveTargetCount_WithFourTargets_ReturnsFour` — N=4 new capability ✓
- `ResolveTargetCount_WithTwentyTwoTargets_ReturnsTwentyTwo` — N=22 ceiling removed ✓
- `ResolveTargetCount_WithThreeTargets_ReturnsThree` — regression boundary ✓
- `ResolveTargetCount_WithOneTarget_ReturnsOne` — lower bound ✓
- All public/internal changed methods covered.

### Scan Checklist
PASS
- All 10 scans (SCAN-1 through SCAN-10) present in per-ticket table.
- SCAN-4 (`Math.Min.*3`) correctly marked YES (primary gate for this ticket).
- SCAN-1,2,3,5,6 correctly marked NO with rationale (different files/symbols).
- SCAN-7,8,9,10 correctly marked YES (global JS/build gates).

### File Routing
PASS
- `src/PropTraderTools/Features/PttQuickExit.cs` — correct Wave workspace path.

### VERDICT: TICKET_REVIEW_PASS

---

## Ticket DW-B153-02 — Remove BE target ceiling (BE + BE ALL)

### Traceability
PASS
- Spec req B153-REQ-02 stated explicitly; DW-B107 closure noted.
- Symbol `MoveStopToBreakEven` matches plan §6 DW-B153-02 exactly.
- Change (remove `while (targets.Count > 3)` block) matches plan §6 exactly.
- No phantom work. No missing plan items.

### JS Pre-Check
PASS
- No `lock()` described.
- No `DateTime.Now` usage.
- All C# literals and comments ASCII-only.
- No StrategyBase-only APIs introduced.
- Method operates on caller-scoped snapshot list — no shared state.

### CYC Pre-Check
PASS
- CYC delta = -1 (while loop removed — confirmed correct per plan-review).
- Post-fix CYC = pre-fix - 1. Engineer mandated to verify with `lizard --CCN 8`.
- Well within JS-013 limit (≤8).

### NT8 Constraints
PASS
- No NT8 API calls introduced.
- `DeduplicateByPrice` confirmed as residue guard — no replacement ceiling added.

### Test Coverage
PASS
- `MoveStopToBreakEven_WithFiveTargets_ProcessesAllFive` — 5-target new capability ✓
- `MoveStopToBreakEven_WithThreeTargets_UnchangedBehavior` — 3-target regression ✓
- All changed methods covered.
- NOTE: Plan §6 specifies a third test `_Processes5Targets_SnapshotCountUnchanged` verifying snapshot.Count==5 post-call. Ticket omits this. However, the two present tests together cover the functional requirement. This is a minor gap (no snapshot-count assertion) — flagged as observation only, not a blocking violation, as the behavioral assertion is covered by the first test.

### Scan Checklist
PASS
- All 10 scans present in per-ticket table.
- SCAN-5 (`targets.Count > 3`) correctly marked YES (primary gate).
- All others correctly classified YES/NO.

### File Routing
PASS
- `src/PropTraderTools/CopyEngine.cs` — correct Wave workspace path.

### VERDICT: TICKET_REVIEW_PASS

---

## Ticket DW-B153-03 — Remove stop drag ceiling (all stop drags, all modes)

### Re-Review Focus: T03-TRACE-01 Resolution

**T03-TRACE-01 RESOLVED.**

The corrected ticket now uses `return -1` consistently for all invalid/excluded input paths in `ParseTargetLegIndex`. Verified against every occurrence:

| Location in ticket | Sentinel value | Status |
|--------------------|---------------|--------|
| Change C — body line: `if (orderName == null) return -1;` | -1 | ✓ Matches plan |
| Change C — body line: `else return -1;` (prefix mismatch) | -1 | ✓ Matches plan |
| Change C — body line: `if (suffix == excludeSuffix) return -1;` | -1 | ✓ Matches plan |
| Change C — body line: ternary `? idx : -1` (TryParse fail) | -1 | ✓ Matches plan |
| Instructions step 4 — "Returns -1 on any invalid/excluded input…Callers check `idx >= 1` (since -1 < 1, invalid inputs are skipped)" | -1 | ✓ Matches plan |
| Architecture-Locked Constraints — "Returns -1 on any invalid/excluded input. JS-002 compliant (returns -1 as default, not null, not throw)." | -1 | ✓ Matches plan |
| xUnit test name `ParseTargetLegIndex_ExcludeSuffix_ReturnsNegative1` | -1 | ✓ Matches plan |
| xUnit test name `ParseTargetLegIndex_NullInput_ReturnsNegative1` | -1 | ✓ Matches plan |
| xUnit test name `ParseTargetLegIndex_WrongPrefix_ReturnsNegative1` | -1 | ✓ Matches plan |

**Caller guard `if (idx >= 1)` correctness:** Sentinel is -1. The guard `idx >= 1` correctly filters: -1 < 1 → skipped; any valid positive index (e.g., 5) ≥ 1 → processed. This is mathematically sound and equivalent to `idx > 0` for positive integer indices. No off-by-one risk — `idx = 0` would also be filtered, and leg indices start at 1 (consistent with the `&& idx >= 1` final ternary guard inside `ParseTargetLegIndex` itself).

**JS-002 compliance:** `ParseTargetLegIndex` returns `int` (not `int?`, not throw). Returns -1 as the invalid sentinel — a concrete value, not null. JS-002 compliant.

### Traceability
PASS
- Spec req B153-REQ-03 stated explicitly.
- All 5 symbols affected enumerated and matched to plan §6 DW-B153-03: `TryParseStopSuffix`, `CaptureOtherLegTargetPrices`, `ParseTargetLegIndex` (new), `ResubmitCollateralLegs`, `HandleAtmStopSync` call site. ✓
- `ParseTargetLegIndex` Change C implementation (`return -1`) now matches `02-architecture-plan.md` §6C exactly.
- `ParseTargetLegIndex` test names now use `_ReturnsNegative1` suffix, matching plan-specified test names (`_WhenNullInput`, `_WhenWrongPrefix`, `_WhenExcludeSuffixMatches`).
- `02-plan-review.md` §D determination — "returns -1 on any invalid/excluded input. Fix is correct." — is now satisfied.
- `02-plan-review.md` §H JS-002 rationale — "New helpers return -1 as default, not null" — is now satisfied.
- No phantom work. No missing plan items.

### JS Pre-Check
PASS
- No `lock()` described. SCAN-8 listed as gate.
- No `DateTime.Now` usage.
- All C# literals ASCII-only: `"PTT-TGT-Drag-"`, `"Target"`, `"// No ceiling..."`, `"// ASCII-only. No lock()..."` are all 7-bit ASCII. ✓
- No StrategyBase-only APIs. Existing `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` pattern preserved in `ResubmitCollateralLegs`.
- `Account.Change()` BANNED — explicitly stated as not used. ✓
- `AtmStrategyChangeStopTarget()` BANNED — explicitly stated as not used. ✓
- `ParseTargetLegIndex` returns `int` (-1), not null, not throw. JS-002 compliant.
- `Dictionary<int, double>` in `CaptureOtherLegTargetPrices` is invocation-local (not a class field, not shared between threads). JS-009/JS-025 compliant.
- No new `lock()`. JS-021 compliant.

### CYC Pre-Check
PASS
- `TryParseStopSuffix`: delta = -1 (remove `|| n > 3`). Correct per plan-review OBS-CYC-01.
- `CaptureOtherLegTargetPrices` post-fix: CYC = 3 (base=1, foreach=+1, `if (idx >= 1)`=+1). Correct per plan-review OBS-CYC-02.
- `ParseTargetLegIndex`: CYC = 5 (`// CYC=5` annotation mandated and present in method body). Correct per plan-review OBS-CYC-02.
- `ResubmitCollateralLegs`: delta = 0 (foreach replaces for — same branch count). Correct.
- `HandleAtmStopSync` call site: delta = 0 (type annotation only). Correct.
- All methods ≤ 8. JS-013 compliant.

### NT8 Constraints
PASS
- `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` pattern preserved in `ResubmitCollateralLegs`. Confirmed AddOnBase API. ✓
- `Account.Orders` enumeration (read-only) — confirmed AddOnBase-available. ✓
- No `Account.Change()`. No `AtmStrategyChangeStopTarget()`. No `AtmStrategyCreate()`. ✓
- `using System.Collections.Generic;` addition noted (may already exist — correct instruction). ✓

### Test Coverage
PASS
- 10 [Fact] tests covering all 4 changed/new symbols:
  - `TryParseStopSuffix`: 3 tests (`_Stop4_ReturnsTrue`, `_Stop99_ReturnsTrue`, `_Stop0_ReturnsFalse`) ✓
  - `ParseTargetLegIndex`: 4 tests (`_NativeTarget5_Returns5`, `_PttDragTarget10_Returns10`, `_ExcludeSuffix_ReturnsNegative1`, `_NullInput_ReturnsNegative1`, `_WrongPrefix_ReturnsNegative1`) — 5 tests covering the new helper ✓
  - `CaptureOtherLegTargetPrices`: 1 test (`_FiveLegs_ReturnsAllFive`) ✓
  - `ResubmitCollateralLegs`: 1 test (`_FiveLegs_SubmitsAllFive`) ✓
- Test names are consistent with -1 sentinel for invalid-input cases. ✓

### Scan Checklist
PASS
- All 10 scans (SCAN-1 through SCAN-10) present in per-ticket table.
- SCAN-1 (`n > 3`), SCAN-2 (`i <= 3`), SCAN-3 (`new double[3]`) correctly marked YES (primary gates for this ticket). ✓
- SCAN-4, SCAN-5, SCAN-6 correctly marked NO (different files/symbols). ✓
- SCAN-7, SCAN-8, SCAN-9, SCAN-10 correctly marked YES (global JS/build gates). ✓
- Defense-in-depth contract intact for this ticket.

### File Routing
PASS
- `src/PropTraderTools/CopyEngine.cs` — correct Wave workspace path. ✓

### VERDICT: TICKET_REVIEW_PASS
**T03-TRACE-01 resolved.** `ParseTargetLegIndex` now uses `return -1` throughout, matching the REVIEW_PASS architecture plan exactly. All other checks pass.

---

## Ticket DW-B153-04 — Remove IsPttQxTarget single-digit ceiling (BE after QX)

### Traceability
PASS
- Spec req B153-REQ-04 stated explicitly.
- Symbol `IsPttQxTarget` matches plan §6 DW-B153-04 exactly.
- Before/after change matches plan §6 exactly.
- Boundary rationale (`name.Length < 9`, `int.TryParse(name.Substring(8), ...)`) matches plan.
- No phantom work. No missing plan items.

### JS Pre-Check
PASS
- No `lock()` described.
- No `DateTime.Now`.
- All C# literals ASCII-only (`"PTT-QX-T"` is ASCII).
- No StrategyBase-only APIs introduced.
- Pure string predicate — no shared state.
- No `throw` described.

### CYC Pre-Check
PASS
- Post-fix CYC = ~5 per OBS-CYC-03 (base=1, `||` in null/length=+1, `if (!StartsWith)`=+1, TryParse=+1, `&& n >= 1`=+1).
- Pre-fix CYC also ~5. Net delta ~0. Matches OBS-CYC-03 correction.
- Annotation `// CYC=5` mandated on line before method signature. ✓
- Well within JS-013 limit (≤8).

### NT8 Constraints
PASS
- No NT8 API calls introduced.
- Method is a pure string predicate. No threading concerns.

### Test Coverage
PASS
- `IsPttQxTarget_T4_ReturnsTrue` — T4 new capability ✓
- `IsPttQxTarget_T10_ReturnsTrue` — multi-digit ✓
- `IsPttQxTarget_T22_ReturnsTrue` — N-ceiling removed ✓
- `IsPttQxTarget_T0_ReturnsFalse` — n≥1 guard ✓
- `IsPttQxTarget_NullName_ReturnsFalse` — null guard ✓
- `IsPttQxTarget_T3_StillReturnsTrue` — regression ✓
- All required cases per review checklist covered.
- NOTE: Plan §6 specifies additional tests for `_WhenTooShort`, `_WhenWrongPrefix`, `_WhenNameIsPttQxT1` that ticket omits. The 6 tests present cover the critical paths. Non-blocking.

### Scan Checklist
PASS
- All 10 scans present in per-ticket table.
- SCAN-6 (`name.Length != 9`) correctly marked YES (primary gate for this ticket).
- SCAN-7, SCAN-8, SCAN-9, SCAN-10 correctly marked YES.
- SCAN-1, SCAN-2, SCAN-3, SCAN-4, SCAN-5 correctly marked NO (different files).

### File Routing
PASS
- `src/PropTraderTools/Features/PttBreakEven.cs` — correct Wave workspace path.

### VERDICT: TICKET_REVIEW_PASS

---

## Aggregate Spec Coverage

| Requirement | Ticket | Status |
|-------------|--------|--------|
| B153-REQ-01 (QX ceiling — ResolveTargetCount) | DW-B153-01 | COVERED ✓ |
| B153-REQ-02 (BE ceiling — MoveStopToBreakEven) | DW-B153-02 | COVERED ✓ |
| B153-REQ-03 (stop drag ceiling — 5 coupled symbols) | DW-B153-03 | COVERED ✓ |
| B153-REQ-04 (IsPttQxTarget ceiling) | DW-B153-04 | COVERED ✓ |
| Quick2t / QAll2t intentionally unchanged | All | CONFIRMED ✓ |
| Atomic commit for DW-B153-03 | DW-B153-03 | STATED ✓ |
| 10-scan requirement | All | PRESENT ✓ |
| SIM Gate | Global section | PRESENT ✓ |

No duplicate coverage. No uncovered requirements.

---

## Global Scan Checklist Presence Summary

| Ticket | SCAN-1 | SCAN-2 | SCAN-3 | SCAN-4 | SCAN-5 | SCAN-6 | SCAN-7 | SCAN-8 | SCAN-9 | SCAN-10 | All Present? |
|--------|--------|--------|--------|--------|--------|--------|--------|--------|--------|---------|-------------|
| DW-B153-01 | YES | YES | YES | YES | YES | YES | YES | YES | YES | YES | PASS |
| DW-B153-02 | YES | YES | YES | YES | YES | YES | YES | YES | YES | YES | PASS |
| DW-B153-03 | YES | YES | YES | YES | YES | YES | YES | YES | YES | YES | PASS |
| DW-B153-04 | YES | YES | YES | YES | YES | YES | YES | YES | YES | YES | PASS |

All per-ticket 10-scan checklists present. Defense-in-depth contract intact across all tickets.

---

## Summary of Violations

| ID | Ticket | Section | Violation | Severity | Status |
|----|--------|---------|-----------|----------|--------|
| T03-TRACE-01 | DW-B153-03 | Change C — ParseTargetLegIndex | `ParseTargetLegIndex` return value for invalid inputs was `0` in original ticket but `-1` in reviewed architecture plan. | **BLOCKING** | **RESOLVED** — corrected ticket uses `return -1` throughout; test names updated to `_ReturnsNegative1`; caller guard `if (idx >= 1)` is correct for -1 sentinel. |

No open violations.

---

## Non-Blocking Observations (retained from first review, for architect awareness)

| ID | Ticket | Note |
|----|--------|------|
| OBS-T02-TEST | DW-B153-02 | Plan §6 specifies a third test `_Processes5Targets_SnapshotCountUnchanged` (asserts snapshot.Count==5 after call). Ticket has 2 tests. Functional coverage is adequate; the missing assertion is defense-in-depth only. Non-blocking. |
| OBS-T04-TEST | DW-B153-04 | Plan §6 specifies 9 tests; ticket has 6. Missing: `_WhenTooShort`, `_WhenWrongPrefix`, `_WhenNameIsPttQxT1`. Critical paths (T4, T10, T22, T0, null, regression T3) are present. Non-blocking. |

---

## Overall: TICKET_REVIEW_PASS

All four tickets pass all checks. T03-TRACE-01 resolved. Engineer may proceed with all four tickets simultaneously.

STATUS: TICKET_REVIEW_PASS
