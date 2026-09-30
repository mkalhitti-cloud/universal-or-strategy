# B114 Plan Review — Phase 2

**Block**: B114
**Date**: 2026-08-27
**Reviewer**: ptt-plan-reviewer (Phase 2)
**Plan under review**: `docs/brain/B114/02-architecture-plan.md`
**Defect addressed**: DW-B119 (P0) — `_qxPendingFollowerCleanup` TryAdd placement race
**Sources read**:
- `docs/brain/B114/02-architecture-plan.md` (full)
- `docs/standards/jane-street/RULES_CATALOG.md` (P0/P1 rules: JS-001, JS-002, JS-010, JS-015, JS-021, JS-033, CYC)
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` L127-194
- `src/PropTraderTools/CopyEngine.cs` L2382-2444

---

## Verdict

> **REVIEW_PASS**

All 14 checklist items pass. Zero violations found. Zero rule citations required.

---

## Checklist Assessment

| ID | Item | Result | Evidence |
|----|------|--------|----------|
| R-01 | Root cause identified correctly | **PASS** | Plan Section A: TryAdd placed after executor.Execute inside try{}; NT8 Sim OnOrderUpdate dispatched synchronously during SubmitOrder — map empty when Working fires. Source file read (L155-173) confirms this is the exact current shipped state. |
| R-02 | Fix specified precisely | **PASS** | Plan Section C: verbatim BEFORE/AFTER diff moves TryAdd block (5 lines + comment) from inside try{} after Execute to before try{}. Section B: scope is exactly 2 source files + 2 doc files. No other changes to ExecuteOne. |
| R-03 | Exception-safety invariant covered | **PASS** | Plan Section E Case 2: if Execute throws, map entry exists but no PTT-QX-T* orders submitted → no Working events → entry expires via 2s TTL harmlessly. finally{} removes _qxCancelInProgress unconditionally (DW-B112 preserved). Section C confirms. |
| R-04 | CYC analysis correct (CYC=2 before and after) | **PASS** | Plan Section F: ExecuteOne CYC=2, delta=0. Manual branch count: if(!skipIfFollower)+1, base+1, try/finally=0, TryAdd/TryRemove=0. Total=2. Source file confirms exactly 1 conditional branch in follower path. |
| R-05 | JS-021 compliance (no lock()) | **PASS** | Plan Section K JS-021: PASS. Plan Section G SCAN-A command present with 0-result pass criterion. No lock() in BEFORE or AFTER code blocks. ConcurrentDictionary.TryAdd is lock-free. |
| R-06 | JS-033 compliance (no async void) | **PASS** | Plan Section K JS-033: PASS. SCAN-B covers both modified files. ExecuteOne is synchronous void. No new methods. |
| R-07 | Test strategy complete (B113Tests.cs T_B113_01) | **PASS** | Plan Section H: method renamed SetAfterExecuteOne→SetBeforeExecuteOne; assertion (ContainsKey + Expiry>UtcNow) correct; all 4 [Fact] tests listed; xUnit-only. Ordering validated by B114-DEFER-02 SIM re-test (correct architectural separation — ExecuteOne requires sealed NT8 Account). |
| R-08 | Scan plan present (SCAN-A through SCAN-E) | **PASS** | Plan Section G: all 5 scans with exact grep/python commands and explicit pass criteria. SCAN-A lock(), SCAN-B async void, SCAN-C ASCII, SCAN-D CYC, SCAN-E DateTime.Now. |
| R-09 | Change scope correct (no CopyEngine.cs expansion) | **PASS** | Plan Sections B and L: exactly PttGlobalQuickExit.cs + B113Tests.cs (source); NO-PIPELINE-REPAIRS.md + specs/002-trade-copier-spec.html (docs). CopyEngine.cs explicitly in "Files NOT Modified" with rationale. |
| R-10 | Deferred backlog complete | **PASS** | Plan Section I: DW-B120 (monitored), B114-DEFER-01/02/03, B114-OBS-01, B113 carry-forwards table (DW-B107, DW-PTT-BE-FIX-01/02/03, DW-B42-01/02/03, DW-B89-DEFERRED-01..06). |
| R-11 | Spec update plan present | **PASS** | Plan Section J: #section-dw-b119 OPEN→CLOSED-B114 with root cause + fix + verified-by; #section-dw-b120 OPEN→MONITORED-B114 with conditional escalation path; #section-dw-b117 add B114 confirmation note. |
| R-12 | No prohibited patterns in new code | **PASS** | AFTER block: no lock(), no async void, no return null (only bare return;), DateTime.UtcNow (not Now), all comment/string literals are ASCII-only. |
| R-13 | Assembly seam not duplicated | **PASS** | Plan Section B ASSEMBLY-SEAM note: InternalsVisibleTo confirmed present at CopyEngine.cs L46 (B113). B114 explicitly prohibited from adding it again. CopyEngine.cs in "Files NOT Modified". |
| R-14 | DW-B112 guard preservation | **PASS** | Plan Section C: "DW-B112 guard (finally block): Preserved exactly as-is." AFTER code block shows finally{} TryRemove(_qxCancelInProgress) word-for-word identical to BEFORE. Section E confirms TryRemove is independent of TryAdd ordering. |

---

## Jane Street Rules Checked

| Rule | Scope | Result |
|------|-------|--------|
| JS-021 | No lock() in new/modified code | PASS |
| JS-033 | No async void in new/modified code | PASS |
| JS-001 | No throw in hot paths (ExecuteOne, TryCleanupReArmedAtmBracket) | PASS |
| JS-002 | No return null in new/modified code | PASS |
| CYC<=8 | ExecuteOne CYC=2 before and after | PASS |
| DateTime.UtcNow | New TryAdd call uses UtcNow.AddSeconds(2) | PASS |
| ASCII-only | New comment lines verified ASCII | PASS |

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| DW-B119 root cause: TryAdd after Execute is too late in Sim | YES | Section A |
| DW-B119 fix: move TryAdd before try{} | YES | Section A, C |
| Exception safety if Execute throws | YES | Section E (Case 2) |
| Partial execute safety (T1 placed, T2/T3 fail) | YES | Section E (Case 3) |
| Idempotency if TryAdd returns false (key exists) | YES | Section E (Case 4) |
| DW-B112 finally{} TryRemove unchanged | YES | Section C, E |
| Live-mode correctness preserved | YES | Section A (Why Moving TryAdd Earlier is Correct, point 2) |
| CYC <= 8 for all modified methods | YES | Section F |
| JS-021 lock-free compliance | YES | Section G (SCAN-A), Section K |
| JS-033 no async void | YES | Section G (SCAN-B), Section K |
| Test: T_B113_01 rename to reflect before-Execute ordering | YES | Section H |
| All 4 [Fact] tests present, xUnit-only | YES | Section H |
| InternalsVisibleTo assembly seam NOT duplicated | YES | Section B (ASSEMBLY-SEAM note) |
| CopyEngine.cs NOT modified | YES | Section B, Section L |
| NO-PIPELINE-REPAIRS.md updated for DW-B119 | YES | Section B, Section L |
| Spec #section-dw-b119 closed | YES | Section J |
| Spec #section-dw-b120 monitored | YES | Section J |
| Spec #section-dw-b117 note added | YES | Section J |
| DW-B120 monitored (not closed) | YES | Section A (Executive Summary), Section I |
| B114-DEFER-01 (F5 gate) | YES | Section I |
| B114-DEFER-02 (SIM re-test Combo D) | YES | Section I |
| B114-DEFER-03 (DW-B120 re-assessment) | YES | Section I |
| Sync gate command | YES | Section M |

---

## Violations

None. Zero violations found across all 14 checklist items and all applicable Jane Street P0/P1 rules.

---

## Notes for Engineer (Phase 4a)

The plan is precise and self-consistent. The actual source file (`PttGlobalQuickExit.cs` L127-194)
was read and confirms it is in the exact B113 shipped state described in the BEFORE block. The CopyEngine.cs
`TryCleanupReArmedAtmBracket` (L2382-2444) was verified to be unchanged and correctly deployed.

The B114-OBS-01 note (acc.Cancel vs acc.CancelOrder API discrepancy) is correctly classified as
observational/low-priority — it does not affect the B114 change scope and was already present in B113.

Plan is approved for Phase 3 (ticket generation) without modification.

---

*Review completed by ptt-plan-reviewer. Phase 2 gate: REVIEW_PASS.*
