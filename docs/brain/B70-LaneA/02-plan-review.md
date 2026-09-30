# B70-LaneA Plan Review

**Reviewer**: ptt-plan-reviewer
**Block**: B70-LaneA
**Date**: 2026-08-14
**Input**: docs/brain/B70-LaneA/02-architecture-plan.md
**Status**: REVIEW_PASS

---

## Source Baseline Confirmed

| Artifact | Line | Confirmed |
|----------|------|-----------|
| `CopyEngine.cs` `_qxOcoSeq = 0` field | 520 | YES — matches plan exactly |
| `IsQxCancelCandidate` before-state (4 branches, CYC=5) | 439-445 | YES — matches plan exactly |
| `PttQuickExit.Execute` Step 3 leader-only call | 52 | YES — matches plan exactly |
| Guid fallback path 1 | 55 | YES — plan notes line 55 (actual: line 55) |
| Guid fallback path 2 | 86-87 | YES — plan cites lines 55 and 86; actual line 86-87 |
| `NextQxOcoId` call sites | 7 grep hits | YES — only called in CopyEngine.cs (declaration) and PttQuickExit.cs (lines 55, 86) |

---

## Checklist Verdicts

### R-01: DW-B70-01 root cause addressed correctly

**PASS.**

The plan correctly identifies the root cause: `_qxOcoSeq = 0` at field-declaration time causes
reset to 0 on every `CopyEngine` re-instantiation (session reconnect / AddOn reload), producing
duplicate `"PTT-QX-00001"` OCO group IDs that NT8 rejects.

The chosen fix (Option A: seed with `Environment.TickCount & 0x7FFF`) directly addresses the
reset-on-instantiation root cause by starting the counter at a session-varying value rather than
a fixed 0. Plan Section 2 documents the cause and the fix clearly and accurately.

---

### R-02: Option A seed value (TickCount & 0x7FFF) collision avoidance

**PASS.**

- `& 0x7FFF` masks to bits 0-14, yielding `[0, 32767]` — always non-negative even when
  `Environment.TickCount` wraps negative after ~24.9 days uptime.
- Plan Section 7 (NT8-VERIFY-02) and Section 2 both confirm this range and the edge case.
- Maximum seed 32767 in D5 format is `"32767"` — 5 digits, within the `D5` column width.
  Worst-case seed 32767 + sequential increments still produces valid D5 strings until 99999
  (67,232 unique IDs in the worst case — far above any realistic session's QX press count).
- Plan explicitly states (Section 2) that NT8 sim resets its OCO name table on each reconnect,
  so prior-session OCO names cannot collide with new-session ones anyway.

---

### R-03: DW-B70-02 Part A — PTT-Copy branch uses StringComparison.Ordinal

**PASS.**

Plan Sections 3 and 8 (Ticket 2) show the new branch as:

```csharp
if (o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)) return true;   // (5) B70
```

`StringComparison.Ordinal` is explicitly present, consistent with branches (3) and (4) in the
existing method. No `StringComparison.OrdinalIgnoreCase` or culture-sensitive comparison.

---

### R-04: DW-B70-02 Part B — PttQuickExit.Execute follower sweep call

**PASS.**

Plan Section 3 (Part B) specifies adding:

```csharp
CopyEngine.Instance?.CancelQxBracketsForFollowers(instr);
```

immediately after the existing Step 3 `CancelQxBrackets(leader, instr)` call at line 52.

The plan correctly explains why this is necessary: `CancelQxBrackets(leader, instr)` only
iterates `leader.Orders`; PTT-Copy orders live on follower accounts and are invisible to the
leader sweep. The plan cites `PttGlobalQuickExit.cs` line 38 as the precedent call to confirm
the method exists and is already proven in an analogous context. The null-guard note on
`FindRule(instr)` returning early when no copy rule is configured is correct and adequately
scoped.

---

### R-05: CYC specified for every changed method

**PASS.**

| Method | Plan Before | Plan After | Limit | Status |
|--------|------------|------------|-------|--------|
| `NextQxOcoId()` | 1 | 1 | 8 | PASS |
| `IsQxCancelCandidate` | 5 | 6 | 8 | PASS |
| `PttQuickExit.Execute` | 5 | 6 | 8 | PASS |
| `CancelQxBracketsForFollowers` (unchanged) | 5 | 5 | 8 | PASS |

Plan correctly notes that the `?.` null-conditional operator in the new
`CancelQxBracketsForFollowers(instr)` call counts as +1 McCabe decision point (Roslyn strict).

---

### R-06: All 8 required tests T_B70_01..T_B70_08 present

**PASS.**

Plan Section 5 contains exactly T_B70_01 through T_B70_08 with concrete `[Fact]` method
signatures and `Assert.*` calls:

| ID | Method Name | Coverage |
|----|-------------|----------|
| T_B70_01 | `NextQxOcoId_TwoCalls_ReturnDistinctIds` | Monotonic guarantee |
| T_B70_02 | `NextQxOcoId_AllIds_StartWithPttQxPrefix` | Prefix correctness |
| T_B70_03 | `NextQxOcoId_100Calls_AllDistinct` | No collision over 100 calls |
| T_B70_04 | `IsQxCancelCandidate_PttCopyName_ReturnsTrue` | New branch (5) fires |
| T_B70_05 | `IsQxCancelCandidate_PttCopyVariant_ReturnsTrue` | StartsWith prefix coverage |
| T_B70_06 | `IsQxCancelCandidate_PttQxStop_ReturnsTrue_Regression` | Branch (3) regression |
| T_B70_07 | `IsQxCancelCandidate_Stop1_ReturnsTrue_Regression` | Branch (2) regression |
| T_B70_08 | `IsQxCancelCandidate_EntryName_ReturnsFalse` | Non-bracket name rejection |

All are xUnit `[Fact]` tests. No NUnit or MSTest (JS mandate complied with).
The optional T_B70_09 (integration-level follower cancel mock) is correctly flagged as
conditional on test harness capability and is not part of the required 8.

---

### R-07: All new string literals ASCII-only

**PASS.**

New/modified string literals in scope:

- `"PTT-QX-"` — ASCII-only, already existing in unchanged method body.
- `"PTT-Copy"` — ASCII-only (no Unicode, no em-dash, no smart quotes).
- Comment prefix `"B70 DW-B70-02:"` — ASCII-only.
- `Environment.TickCount & 0x7FFF` — no string literals involved.

Plan SCAN-01 explicitly notes the pre-existing non-ASCII violations at lines 398, 499 and
~1449-1450 of CopyEngine.cs are out of scope for B70-LaneA, and the engineer must NOT touch
those lines. This is correct.

---

### R-08: No lock() usage in changed methods confirmed

**PASS.**

Plan Section 6 (JS Compliance Matrix) and Section 7 (SCAN-02) explicitly confirm:

- `NextQxOcoId()` — uses `Interlocked.Increment`, no lock. Unchanged method body.
- `IsQxCancelCandidate` — static pure predicate, no state, no lock.
- `PttQuickExit.Execute` addition — a single `?.` method call, no lock.

Plan provides grep commands in Section 6 that the engineer must run:

```
grep "lock(" src/PropTraderTools/CopyEngine.cs
grep "lock(" src/PropTraderTools/Features/PttQuickExit.cs
```

both expected to return 0 results in changed regions.

---

### R-09: Guid fallback paths in PttQuickExit.cs addressed

**PASS.**

Plan Section 2 (Option A discussion) explicitly states:

> PttQuickExit.cs Guid fallback paths at lines 55 and 86 remain unchanged. They are valid
> defensive fallbacks for the case where CopyEngine.Instance is null (e.g., AddOn not yet
> loaded). Their output format ("PTT-QX-" + Guid 8-hex-chars) is intentionally different from
> the main D5 path and remains correct.

Source confirms both paths exist at lines 55 and 86-87 of PttQuickExit.cs. The plan's
disposition (retain, no change needed) is correct — Option A does not alter the method body
of `NextQxOcoId()` nor the fallback expressions in `PttQuickExit.Execute`.

---

### R-10: All files changed listed

**PASS.**

Plan Section 4 lists exactly the three changed source files and one new test file:

| File | Change | Status |
|------|--------|--------|
| `src/PropTraderTools/CopyEngine.cs` | `_qxOcoSeq` field initializer (T1) | Listed |
| `src/PropTraderTools/CopyEngine.cs` | `IsQxCancelCandidate` new branch (T2) | Listed |
| `src/PropTraderTools/Features/PttQuickExit.cs` | `CancelQxBracketsForFollowers` call (T2) | Listed |
| `tests/PropTraderTools.Tests/CopyEngineB70Tests.cs` | NEW test file | Listed |

Section 4 also correctly identifies all files NOT changed (`PttGlobalQuickExit.cs`,
`CancelQxBracketsForFollowers`, `CancelQxBrackets`, `IsAtmBracketName`, `PttBreakEven.cs`).

---

### R-11: Section K / deferred backlog coverage

**PASS.**

The plan's Appendix ("Deferred Backlog Carry-Forward") carries forward all prior OPEN items
from B66-LaneC/06-deferred-backlog.md with their existing status. No new items are opened
by B70-LaneA fixes (both defects are fully closed in-block). The plan correctly makes no
claim to close the pre-existing backlog items.

The Section K table (deferred-backlog format) is not embedded in the plan itself but the
Appendix fulfils the equivalent function for Phase 2 — the full Section K with DW-B70-XX
closed items belongs in `05-final-review.md` (Phase 5), not Phase 2. This is correct per
the PTT Pipeline Team Map.

---

### R-12: NT8-VERIFY-01 and NT8-VERIFY-02 present in 7-scan section

**PASS.**

Plan Section 7 contains exactly 7 scans (SCAN-01 through SCAN-07) followed by two dedicated
NT8 verification entries:

- **NT8-VERIFY-01** — `Order.Name` property use in `IsQxCancelCandidate` confirmed against
  `CopyEngine.cs` line 1264 ground truth (`signalName = "PTT-Copy"`).
- **NT8-VERIFY-02** — `Environment.TickCount` range analysis (signed int, wrap risk, & 0x7FFF
  mitigation) confirmed correct for `int _qxOcoSeq` with `Interlocked.Increment`.

Both entries render PASS verdicts with explicit reasoning.

---

## JS Rule Compliance Summary

| Rule | Applicable? | Plan Verdict | Reviewer Verdict |
|------|-------------|--------------|-----------------|
| JS-021 (no lock) | YES | PASS | PASS — Interlocked only; no lock in any changed region |
| JS-001 (no throw) | YES | PASS | PASS — no throw in changed methods |
| JS-002 (no null return) | YES | PASS | PASS — string/bool returns only |
| JS-033 (no async void) | YES | PASS | PASS — all synchronous |
| JS-003 (magic string) | YES | PASS | PASS — "PTT-Copy" is a typed constant string, not discriminated state |
| JS-008 (mutable struct) | NO | N/A | N/A — no structs introduced |
| JS-009 (ImmutableDict) | NO | N/A | N/A — no collections introduced |
| JS-010 (public constructor) | NO | N/A | N/A — no new types introduced |

---

## NT8 API Compliance

| Constraint | Plan Claim | Reviewer Verdict |
|------------|-----------|-----------------|
| `Order.Name` is the NT8 signal name string | YES, confirmed via CopyEngine.cs line 1264 | PASS |
| `CancelQxBracketsForFollowers` is AddOn-accessible | YES, already called from PttGlobalQuickExit.cs | PASS |
| `Environment.TickCount` non-negative after `& 0x7FFF` | YES, plan NT8-VERIFY-02 | PASS |
| `AtmStrategyCreate` not used | N/A — not in scope | N/A |
| No `CreateOrder` without PTT- prefix | PASS — no new CreateOrder calls | PASS |
| No `DateTime.Now` | PASS — `Environment.TickCount` is not DateTime | PASS |

---

## Spec Coverage Matrix

| Requirement | Plan Section | Addressed? |
|-------------|-------------|-----------|
| DW-B70-01: Seed `_qxOcoSeq` to non-zero on instantiation | Section 2 | YES |
| DW-B70-01: Option A (TickCount & 0x7FFF) chosen | Section 2 | YES |
| DW-B70-01: `NextQxOcoId()` method body unchanged | Section 2 | YES |
| DW-B70-02: `IsQxCancelCandidate` gains PTT-Copy branch | Section 3 | YES |
| DW-B70-02: Branch uses StringComparison.Ordinal | Section 3 | YES |
| DW-B70-02: `PttQuickExit.Execute` sweeps followers | Section 3 | YES |
| Guid fallback paths remain valid | Section 2 | YES |
| 8 tests T_B70_01..T_B70_08 | Section 5 | YES |
| CYC <= 8 all changed methods | Sections 2, 3, 7 | YES |
| No lock() in any changed method | Section 6, 7 (SCAN-02) | YES |
| All new string literals ASCII | Section 7 (SCAN-01) | YES |
| Files changed listed | Section 4 | YES |
| NT8-VERIFY-01, NT8-VERIFY-02 | Section 7 | YES |

All spec requirements are addressed. No requirement is unresolved or deferred without justification.

---

## Violations

**None.** No JS-XXX rule violations found. No NT8 API constraint violations found.
No spec requirements unaddressed.

---

## Final Decision

**REVIEW_PASS**

The plan is complete, internally consistent, and grounded in the actual source baseline.
All 12 checklist items pass. The plan is ready for ticket generation (Phase 3).

REVIEW_PASS
