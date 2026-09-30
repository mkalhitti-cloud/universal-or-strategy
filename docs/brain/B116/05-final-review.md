# B116 Final Review — DW-B124 Fix (Option B)

**Reviewer**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-08-28
**Block**: B116
**Defect**: DW-B124 (P0) — CalcTNQty fallback wrong split, BE-ALL+QX-ALL Combo C

---

## FINAL REVIEW RESULT: FINAL_PASS

All checks PASS. No JS rule violations found cross-pipeline. All spec requirements satisfied.
Section K and 06-deferred-backlog.md written. FINAL_PASS is not blocked.

---

## 1. Pipeline Artifact Checklist

| Phase | Artifact | Gate Result | Confirmed |
|-------|---------|-------------|-----------|
| Ph1 | `02-architecture-plan.md` | PLAN_COMPLETE | ✓ |
| Ph2 | `02-plan-review.md` | REVIEW_PASS (15/15) | ✓ |
| Ph3 | `04-tickets.md` | TICKETS_COMPLETE | ✓ |
| Ph3.5 | `04-ticket-review.md` | TICKET_REVIEW_PASS (cycle 2) | ✓ |
| Ph4a | `ticket-1-completion.md` | BUILD_PASS | ✓ |
| Ph4a | `ticket-2-completion.md` | BUILD_PASS | ✓ |
| Ph4b | `ticket-1-verification.md` | VERIFY_PASS | ✓ |
| Ph4b | `ticket-2-verification.md` | VERIFY_PASS | ✓ |
| Ph5 | `05-final-review.md` | FINAL_PASS (this document) | ✓ |

No phase was skipped. No gate was bypassed. Ticket review required 2 cycles (cycle 1 FAIL
was test-name mismatch between plan Section 6 and ticket T2; resolved in cycle 2 by
aligning plan names to ticket names — a correct resolution).

---

## 2. Cross-File Coherence Findings

Source file read: `src/PropTraderTools/Features/PttGlobalQuickExit.cs` (376 lines, verified live).

| Requirement | Status | Evidence |
|-------------|--------|----------|
| `ScaleLeaderTargets` present | **PASS** | Line 336: `internal static ... ScaleLeaderTargets(...)` |
| `ResolveFollowerTargets` present | **PASS** | Line 364: `internal static ... ResolveFollowerTargets(...)` |
| Substitution call wired correctly | **PASS** | Line 125: `followerTargets = ResolveFollowerTargets(followerTargets, targets, _fPosQty, pos.Quantity);` |
| Substitution is AFTER DIAG block | **PASS** | DIAG block closes at line 121; substitution at line 125 |
| Substitution is BEFORE ExecuteOne log | **PASS** | Log at line 127; substitution at lines 125-126 |
| `_fPosQty` declared ABOVE DIAG block | **PASS** | Line 94: `int _fPosQty = 0;`; DIAG opens at line 105 |
| No double-declaration of `_fPosQty` | **PASS** | Only one declaration at line 94; references at lines 102, 111, 126 |
| `Execute` CYC = 8 | **PASS** | Verifier: 8 decision branches in Execute body; docstring confirms CYC=8 |
| `PttQuickExit.cs` untouched | **PASS** | Verifier confirmed; git status shows no PttQuickExit.cs modification |
| `CopyEngine.cs` untouched by T1 | **PASS** | Ticket 1 scope is PttGlobalQuickExit.cs only; verifier confirmed |
| DIAG blocks left in place | **PASS** | Lines 64-80 (leader DIAG) and 90-121 (follower DIAG) intact; Director removes when gate passes |
| `SnapshotTargetOrders` unchanged | **PASS** | DW-B123 dedup deployed and preserved; no modification in T1 or T2 |

**No scope leakage found. No changes to any file outside the ticket contract.**

---

## 3. JS Rule Compliance (Cross-Pipeline Scan)

Scans run by engineer (Layer 2) and independently confirmed by verifier (Layer 3).

### New Code in PttGlobalQuickExit.cs (ScaleLeaderTargets, ResolveFollowerTargets, substitution line)

| Rule | Check | Result |
|------|-------|--------|
| JS-021: No `lock()` | SCAN-01: 0 matches | **PASS** |
| JS-001: No `throw new XxxException` | SCAN-02: 0 matches in new code | **PASS** |
| JS-002: No `return null` | SCAN-03: line 4 is comment only; methods return initialized `List<>` | **PASS** |
| JS-033: No `async void` | SCAN-04: both helpers are synchronous static | **PASS** |
| JS-009: No mutable `Dictionary<K,V>` for shared state | Not applicable — local scope only | **PASS** |
| JS-008: No mutable struct fields | No struct fields | **PASS** |
| JS-051: xUnit only (test file) | `using Xunit;` only; no NUnit, MSTest, Moq | **PASS** |
| ASCII-only | SCAN-07 (B116Tests.cs): 0 non-ASCII | **PASS** |
| NT8: No `DateTime.Now` | No new `DateTime` usage; `UtcNow` used elsewhere | **PASS** |
| NT8: No `Account.All` in constructor | Not applicable | **PASS** |
| NT8: No sealed on `TradeCopierWindow` | File not touched | **PASS** |
| NT8: No `async`/`await` in lifecycle methods | Not applicable | **PASS** |
| NT8: `CreateOrder` without PTT- prefix | No `CreateOrder` calls in new code | **PASS** |

**Zero P0 JS violations found in any new code. Zero P1 violations.**

### 7-Scan Aggregate (across src/PropTraderTools/ for B116 new code)

| Scan | Scope | Result |
|------|-------|--------|
| SCAN-01: `lock(` | PttGlobalQuickExit.cs new code | 0 |
| SCAN-02: `throw new` | PttGlobalQuickExit.cs new code | 0 |
| SCAN-03: `return null` | PttGlobalQuickExit.cs new code | 0 (comment only on line 4) |
| SCAN-04: `async void` | PttGlobalQuickExit.cs new code | 0 (comment only on line 4) |
| SCAN-05: CYC audit | Execute=8, ScaleLeaderTargets=4, ResolveFollowerTargets=3 | All ≤ 8 |
| SCAN-06: build errors | 0 new errors from B116 files | PASS |
| SCAN-07: ASCII-only | B116Tests.cs | 0 non-ASCII |

All 7 scans at zero for new B116 code.

---

## 4. Test Coverage Summary

| Test | Method Under Test | What It Verifies | Status |
|------|------------------|------------------|--------|
| T2-1: `ScaleLeaderTargets_EqualQty_IdenticalSplit` | `ScaleLeaderTargets` | Equal qty → output identical to input; sum=7 | **PASS** |
| T2-2: `ScaleLeaderTargets_HalfQty_SumEqualsFollowerQty` | `ScaleLeaderTargets` | Half qty → sum=followerPosQty; each ≥ 1 | **PASS** |
| T2-3: `ScaleLeaderTargets_ZeroLeaderPosQty_ReturnsEmpty` | `ScaleLeaderTargets` | leaderPosQty=0 guard fires; no divide-by-zero; empty list | **PASS** |
| T2-4: `ResolveFollowerTargets_NonEmptySnapshot_ReturnsSelf` | `ResolveFollowerTargets` | Non-empty snapshot returned unchanged; leader scaling not applied | **PASS** |
| T2-5: `ResolveFollowerTargets_EmptySnapshotFullLeader_ReturnsScaled` | `ResolveFollowerTargets` | DW-B124 critical path; empty snapshot + valid leader → scaled result | **PASS** |
| T2-6: `ResolveFollowerTargets_EmptySnapshotEmptyLeader_ReturnsEmpty` | `ResolveFollowerTargets` | DW-B120 fallback path preserved; empty + empty → empty (CalcTNQty fires) | **PASS** |

- 6 `[Fact]` methods present (verified by verifier source read and scan count = 6).
- xUnit only: `using Xunit;` present; no NUnit, MSTest, Moq references (SCAN-T2-01/02: 0 results).
- DW-B120 degenerate path covered by T2-6 (required per mission brief).
- Both new helpers fully exercised: `ScaleLeaderTargets` via T2-1/2/3; `ResolveFollowerTargets` via T2-4/5/6.

---

## 5. Spec Requirements Satisfied

| Requirement | Status | Evidence |
|-------------|--------|----------|
| DW-B124 root cause addressed: `SnapshotTargetOrders` returns empty in BE-ALL+QX-ALL Combo C | **PASS** | `ResolveFollowerTargets` substitution at line 125 intercepts empty follower snapshot before `ExecuteOne` consumes it; leader qty array passed scaled |
| Fix is Option B (leader qty array scaling, not inline conditional) | **PASS** | `ScaleLeaderTargets` + `ResolveFollowerTargets` extracted as two private static helpers; no inline branch added to `Execute` |
| DW-B120 independence preserved | **PASS** | `ResolveFollowerTargets` branch (2): `if (leaderTargets.Count == 0 \|\| followerPosQty <= 0) return followerSnapshot;` — CalcTNQty path unaffected |
| Execute CYC ≤ 8 | **PASS** | CYC=8 (unchanged) confirmed by verifier branch count |
| No changes to `PttQuickExit.Execute` or `CalcTNQty` | **PASS** | Scope boundary maintained; verifier confirmed |
| Follower Combo C split: T1=4, T2=2, T3=1 (equal-qty accounts) | **PASS** | T2-5 manually traced: equal qty → scale=1.0 → ScaleLeaderTargets returns [(price1,4),(price2,2),(price3,1)] |
| 6 xUnit tests covering all branches | **PASS** | All 6 tests present, correctly named, assertions verified |

---

## 6. Notable Finding — CYC Deviation (Non-Blocking)

| Method | Plan CYC | Measured CYC | Convention Used | Compliant? |
|--------|----------|--------------|-----------------|-----------|
| `ScaleLeaderTargets` | 3 | 4 | Engineer/verifier used McCabe (base=1 + 3 branches = 4); plan used branch-count (3 branches = 3) | **YES** — CYC=4 ≤ 8 limit |
| `ResolveFollowerTargets` | 3 | 3 | Both conventions agree | **YES** |
| `Execute` | 8 | 8 | Unchanged | **YES** |

The CYC=4 for `ScaleLeaderTargets` (vs plan's CYC=3) is a convention difference only. The method
has exactly 3 decision branches: (1) `leaderPosQty <= 0` guard, (2) `for` loop, (3) `if (i == leaderTargets.Count - 1)` last-tranche. McCabe adds the implicit base path (1), giving CYC=4. Both measurements are well within the CYC ≤ 8 ceiling. Not a blocker. Tracked as Section K note below.

---

## Section K — Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B116-K01 | DW-B120 monitor: `CalcTNQty` arithmetic split still used in non-BE QX path (empty snapshot, empty leaderTargets). Acceptable for equal-qty accounts (scale=1.0 → identical result). Monitor only — no code change required unless live evidence shows wrong split in non-BE scenario. | P2 | future | OPEN |
| DW-B116-K02 | Combo C live gate: B116 fix applied (DW-B124). Awaiting NT8 F5 recompile confirmation and live Combo C session (BE-ALL then QX-ALL with equal-qty accounts) to confirm T1=4, T2=2, T3=1 split on all followers. | P1 | immediate (Director) | OPEN |
| DW-B116-K03 | ScaleLeaderTargets CYC convention note: plan stated CYC=3 (branch-count convention); engineer/verifier measured CYC=4 (McCabe). Minor convention discrepancy only. CYC=4 ≤ 8 — no compliance issue. No action required. | P3 | N/A | CLOSED (noted, no action) |
| DW-B116-K04 | Partial-snapshot variant (count=1, Sim104): follower snapshot with 1 PTT-BE-Target-* Working at snapshot time returns that partial snapshot unchanged via `ResolveFollowerTargets` branch (1). T2/T3 may still be wrong for this sub-case. This is the pre-existing DW-B120 P1 monitor scope. No new code change in B116. | P1 | B117 or post Combo C gate | OPEN |

---

## Summary

**B116 is a complete, coherent, spec-compliant delivery.**

- DW-B124 (P0) root cause correctly addressed at the right call site (L89 region in `PttGlobalQuickExit.Execute`).
- Two-helper extraction (`ScaleLeaderTargets` + `ResolveFollowerTargets`) keeps `Execute` at CYC=8.
- DW-B120 CalcTNQty fallback path is preserved and verified by T2-6.
- 6 xUnit [Fact] tests cover all branches of both helpers.
- Zero P0 or P1 JS rule violations in any new code.
- No scope leakage to `PttQuickExit.cs`, `CopyEngine.cs`, or any other file.
- 16/16 NT8 sync files MD5-verified, 0 MISMATCH.
- Remaining gate: NT8 F5 recompile + Combo C live session (Director-owned, tracked in K02).

---

**FINAL_PASS**

*Ph5 final review complete. FINAL_PASS.*
