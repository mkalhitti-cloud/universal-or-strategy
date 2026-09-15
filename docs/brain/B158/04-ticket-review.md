# B158 Ticket Review
# ptt-ticket-reviewer Phase 3.5 output
# Block: B158
# Reviewed: 2026-09-15
# Reviewer role: ptt-ticket-reviewer
# Input: docs/brain/B158/04-tickets.md
# Plan: docs/brain/B158/02-architecture-plan.md (REVIEW_PASS)
# Plan review: docs/brain/B158/02-plan-review.md (REVIEW_PASS)
# Status: TICKET_REVIEW_PASS

---

## Ticket Review: B158

---

### T1 -- DW-B156-01: Leader-flat drain abort SIM verification gate

**Nature**: VERIFICATION-ONLY (zero code changes)

#### Traceability
PASS

| Item | Check | Result |
|------|-------|--------|
| Defect Card ID | DW-B156-01 present in header | PASS |
| Spec req IDs | DW-B156-01 (ghost re-entry on follower after leader-flat BE fill) present in header | PASS |
| Plan section trace | Ticket description maps to 02-architecture-plan.md §3 (T1 Architecture: DW-B156-01 Leader-Flat Drain Abort) | PASS |
| No phantom work | All ticket items (IsLeaderFlatDrainAbort verification, call-site check, AbortDrainOnFill check, unit test, SIM gate) are present in plan §3.5 | PASS |
| No missing plan items | Plan §3.5 steps 1-4 are all reflected in ticket Fix Design Steps 1-5 | PASS |

#### JS Pre-Check (Jane Street DNA)
PASS

| Rule | Check | Result |
|------|-------|--------|
| JS-021: no lock() | Ticket explicitly confirms AbortDrainOnFill uses TryRemove "no lock()". SCAN-02 command confirms 0 lock() matches file-wide. No new code introduced. | PASS |
| JS-001: no throw in hot path | No new code. Ticket confirms guard structure uses early-return pattern (lines 90-97). | PASS |
| JS-023: no UI update from non-UI thread | No UI updates. Ticket confirms Output.Process is NT8-safe from any thread (plan §3.7). | PASS |
| JS-003: no magic string as state discriminant | Log markers ([DRAIN-ABORT-LEADER-FLAT]) are diagnostic output strings, not state discriminators. | PASS |
| JS-009/JS-025: ConcurrentDictionary for shared state | Ticket confirms _pendingDispatchDrains is ConcurrentDictionary. No mutation of collection type. | PASS |

#### CYC Pre-Check
PASS

| Method | Expected CYC | Ticket Statement | Result |
|--------|-------------|-----------------|--------|
| IsLeaderFlatDrainAbort | 8 (AT LIMIT) | Ticket §Fix Design header: "CYC: 8 (AT LIMIT -- confirm no changes have been made)" | PASS |
| TryEvictFollowerBeSlot | 7 | SCAN-04 table in ticket confirms CYC=7 at L1926 | PASS |
| AbortDrainOnFill | 3 | SCAN-04 table in ticket confirms CYC=3 at L8342 | PASS |

No new methods introduced. No method exceeds CYC=8. CYC=8 limit acknowledged and no changes permitted.

#### NT8 Constraints Check
PASS

| Constraint | Check | Result |
|-----------|-------|--------|
| ASCII-only | SCAN-01 command: byte-scan for bytes > 127 -> required result: 0 | PASS |
| No async void | SCAN-05 command: `Select-String '\basync void\b'` -> required result: 0 | PASS |
| No DateTime.Now | SCAN-03 command: `Select-String '\bDateTime\.Now\b'` -> required result: 0 | PASS |
| ptt-sync.ps1 | SCAN-07 explicitly states: "ptt-sync.ps1 is NOT required for a zero-diff ticket, but may be run as a sanity check" with command provided. Zero-diff is the correct statement for a verification-only ticket. | PASS |
| No Account.All outside Loaded | No new code. N/A. | PASS |
| No async/await in lifecycle | No new code. N/A. | PASS |
| No CreateOrder without PTT- prefix | No new code. N/A. | PASS |

#### Test Coverage
PASS

| Item | Check | Result |
|------|-------|--------|
| Existing test referenced | TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString at CopyEngineTests.cs:9481 | PASS |
| Test assertion stated | Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src) | PASS |
| dotnet test command provided | `dotnet test --filter "TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString"` with required result: 1 test PASSED | PASS |
| No new [Fact] required | Verification-only ticket. No new methods. No new tests required. Appropriate. | PASS |
| [Skip] attribute check | Ticket explicitly requires test is "present and without [Skip] attribute" | PASS |

#### Scan Checklist Presence
PASS

| Scan | Command Present | Required Result Stated | FAIL Action Documented |
|------|----------------|----------------------|----------------------|
| SCAN-01 | YES -- PowerShell byte scan | Count=0 | YES |
| SCAN-02 | YES -- `Select-String '^\s+lock\s*\('` | 0 matches | YES |
| SCAN-03 | YES -- `Select-String '\bDateTime\.Now\b'` | 0 executable-code matches | YES |
| SCAN-04 | YES -- table of methods with CYC values and source comment lines | CYC values confirmed from source comments | YES |
| SCAN-05 | YES -- `Select-String '\basync void\b'` | 0 matches | YES |
| SCAN-06 | YES -- `dotnet test --filter "..."` + `dotnet test` | 1 PASSED + zero new failures | YES |
| SCAN-07 | YES -- `git diff src\PropTraderTools\CopyEngine.cs` + `dotnet build` | Empty diff + 0 errors | YES |

All 7 scans present with executable PowerShell commands and required results. PASS.

#### File Routing
PASS

Source file: `src/PropTraderTools/CopyEngine.cs` (Wave workspace). Correct.
Test file: `src/PropTraderTools/CopyEngineTests.cs` (Wave workspace). Correct.
No Director workspace paths for .cs files.

#### Completeness (Verification-Only Specific)
PASS

| Item | Check | Result |
|------|-------|--------|
| Zero code changes explicitly stated | "T1 introduces zero code changes. All items below describe what the engineer must READ and CONFIRM." (Fix Design header) | PASS |
| 5-step SIM gate | Steps 1-5 present with setup, action, PASS criteria, FAIL criteria for each | PASS |
| PASS criteria defined | "OVERALL PASS: Step 3 shows [DRAIN-ABORT-LEADER-FLAT] with aborted >= 1, AND Step 4 shows no ghost entry. DW-B156-01 is CLOSED." | PASS |
| FAIL criteria defined | "OVERALL FAIL (timing race): Step 3 log absent AND Step 4 shows ghost entry." | PASS |
| Inconclusive scenario documented | Partial-coverage scenario (log absent, no ghost) documented -- escalate to Director | PASS |
| FAIL escalation path | DW-B158-DRAIN-RACE-01 defect card creation steps fully documented with 4 sub-steps | PASS |
| BUILD_FAIL path | Scope Constraints: "If implementation gap is found during source verification (Step 1-3 above), STOP. Document the gap. Mark BUILD_FAIL with gap description." | PASS |
| Completion report requirements | Acceptance Criteria explicitly lists: 7 scans documented, SIM evidence copied verbatim, DW-B156-01 closure status stated | PASS |
| Marker distinction documented | [DRAIN-ABORT-LEADER-FLAT] vs [DRAIN-ABORT-FLAT] distinction prominently called out in CRITICAL MARKER DISTINCTION section | PASS |

#### Scope Isolation
PASS

T1 Scope Constraints section explicitly states: "Do NOT make any changes to T2's region (CopyEngine.cs ~L6516-6527)." T1 does not reference T2's implementation detail. Lane independence confirmed.

**VERDICT: TICKET_REVIEW_PASS**

---

### T2 -- DW-B157-COMMENT-01: Fix stale comment at IsReversalToFlatFollower

**Nature**: COMMENT CHANGE ONLY (zero executable code change, zero logic change)

#### Traceability
PASS

| Item | Check | Result |
|------|-------|--------|
| Defect Card ID | DW-B157-COMMENT-01 present in header | PASS |
| Spec req IDs | DW-B157-COMMENT-01 (stale comment at IsReversalToFlatFollower) present in header | PASS |
| Plan section trace | Ticket description maps to 02-architecture-plan.md §4 (T2 Architecture: DW-B157-COMMENT-01 Stale Comment) | PASS |
| No phantom work | Ticket scope (single comment line at L6517) exactly matches plan §4.3 | PASS |
| No missing plan items | Plan §4.3 old/new text fully reproduced in ticket Fix Design section | PASS |

#### JS Pre-Check (Jane Street DNA)
PASS

| Rule | Check | Result |
|------|-------|--------|
| JS-021: no lock() | Comment change cannot introduce lock(). SCAN-02 confirms 0 lock() file-wide. | PASS |
| JS-001: no throw | No executable code modified. N/A. | PASS |
| JS-023: no UI update from non-UI thread | No code change. N/A. | PASS |
| JS-003: no magic string as state discriminant | No executable code. N/A. | PASS |

#### CYC Pre-Check
PASS

| Method | Expected CYC | Ticket Statement | Result |
|--------|-------------|-----------------|--------|
| IsReversalToFlatFollower | 2 (unchanged) | "No executable code modified. CYC for IsReversalToFlatFollower remains 2." SCAN-04 verifies CYC=2 comment still present at adjacent line. | PASS |

Ticket explicitly states: "T2 is annotation-only. No CYC impact." Appropriate.

#### NT8 Constraints Check
PASS

| Constraint | Check | Result |
|-----------|-------|--------|
| ASCII-only | Ticket explicitly states: "All characters in the new comment are printable ASCII (0x20-0x7E). ASCII-001 compliant." SCAN-01 verifies Count=0 after edit. | PASS |
| ptt-sync.ps1 | Acceptance Criteria: "ptt-sync.ps1 run after the edit." SCAN-07: "Run ptt-sync.ps1 after confirming the diff is clean" with command `powershell -File .\scripts\ptt-sync.ps1` and required result. | PASS |
| No async void | SCAN-05 present. Comment change cannot introduce async void. | PASS |
| No DateTime.Now | SCAN-03 present. Comment change cannot introduce DateTime.Now. | PASS |
| No async/await in lifecycle | No executable code. N/A. | PASS |

#### Test Coverage
PASS

| Item | Check | Result |
|------|-------|--------|
| No new [Fact] required | "No new [Fact] tests required. This is an annotation-only change with zero behavioral impact." -- explicit statement in xUnit Tests section | PASS |
| Existing tests to remain passing | IsReversalToFlatFollower test group at CopyEngineTests.cs ~L8103-8175 | PASS |
| dotnet test command provided | `dotnet test --filter "IsReversalToFlatFollower"` with required result: All PASSED, 0 failures | PASS |

#### Scan Checklist Presence
PASS

| Scan | Command Present | Required Result Stated | FAIL Action Documented |
|------|----------------|----------------------|----------------------|
| SCAN-01 | YES -- PowerShell byte scan (run AFTER edit) | Count=0 | YES |
| SCAN-02 | YES -- `Select-String '^\s+lock\s*\('` | 0 matches | YES |
| SCAN-03 | YES -- `Select-String '\bDateTime\.Now\b'` | 0 executable-code matches | YES |
| SCAN-04 | YES -- verify CYC=2 comment still present at adjacent line | CYC=2 comment unchanged | YES |
| SCAN-05 | YES -- `Select-String '\basync void\b'` | 0 matches | YES (noted N/A for comment change, but must be recorded PASS) |
| SCAN-06 | YES -- `dotnet test --filter "IsReversalToFlatFollower"` + `dotnet test` | All PASSED + zero new failures | YES |
| SCAN-07 | YES -- `git diff src\PropTraderTools\CopyEngine.cs` showing exact old/new line diff + `powershell -File .\scripts\ptt-sync.ps1` + `dotnet build` | Exactly ONE changed line L6517 + hard links re-sync + 0 errors | YES |

All 7 scans present with executable PowerShell commands, required results, and FAIL actions. PASS.

#### File Routing
PASS

Source file: `src/PropTraderTools/CopyEngine.cs` (Wave workspace). Correct.
Test file: `src/PropTraderTools/CopyEngineTests.cs` (read-only reference, Wave workspace). Correct.
No Director workspace paths for .cs files.

#### Completeness
PASS

| Item | Check | Result |
|------|-------|--------|
| Single-line annotation only | "Change type: Replace comment text. ONE line only. No other lines change." | PASS |
| Exact OLD string stated | `// Returns true iff the current dispatch reverses the last direction AND the follower is flat.` | PASS |
| Exact NEW string stated | `// Returns true iff the current dispatch reverses the last direction AND the follower is NOT flat (open position + reversal = protection path).` | PASS |
| Acceptance criteria clear | Checkbox list with exact string match, one-line diff, ASCII-only, dotnet build/test | PASS |
| Fallback line-search command | Scope Constraints provides `Select-String -Pattern "the follower is flat\." ...` if L6517 has drifted | PASS |

#### Scope Isolation
PASS

T2 Scope Constraints section explicitly states: "Do NOT make any changes to T1's region (CopyEngine.cs L1929-1974)." T2 does not reference T1's implementation detail. Lane independence confirmed.

**VERDICT: TICKET_REVIEW_PASS**

---

## Lane-Split Confirmation

| Q | Check | Result |
|---|-------|--------|
| T1 completable without T2 | YES -- T1 SIM gate requires zero input from T2 | PASS |
| T2 completable without T1 | YES -- comment edit at L6517 requires zero input from T1 | PASS |
| T1 references T2 implementation? | NO -- Scope Constraints explicitly forbid touching T2's region | PASS |
| T2 references T1 implementation? | NO -- Scope Constraints explicitly forbid touching T1's region | PASS |
| Both tickets have independent 7-scan checklists | YES -- each ticket carries its own complete SCAN-01 through SCAN-07 | PASS |

---

## Spec Coverage Matrix

| Requirement | Ticket | Coverage |
|-------------|--------|---------|
| DW-B156-01: Leader-flat drain abort SIM gate | T1 | COVERED -- SIM gate closes this if PASS |
| DW-B157-COMMENT-01: Stale comment correction | T2 | COVERED -- L6517 comment correction |
| SIM FAIL escalation path (DW-B158-DRAIN-RACE-01) | T1 | COVERED -- full defect card creation steps |
| Deferred items carried forward (4 items) | Both (Deferred Items table at end) | COVERED |

No uncovered spec requirements. No duplicate coverage.

---

## Violation Summary

| Rule ID | Severity | Description | Location | Result |
|---------|----------|-------------|----------|--------|
| (none) | -- | No violations found | -- | -- |

---

## Overall: TICKET_REVIEW_PASS

Both T1 and T2 pass all checklist items:

- **Traceability**: PASS (both tickets)
- **7-Scan Checklist Presence**: PASS (both tickets -- all SCAN-01 through SCAN-07 present with commands, required results, and FAIL actions)
- **JS Pre-Check**: PASS (both tickets -- no lock(), no throw in hot path, no UI off-thread)
- **CYC Pre-Check**: PASS (both tickets -- no CYC > 8; T1 confirms CYC=8 AT LIMIT with no changes)
- **NT8 Constraints**: PASS (both tickets -- ASCII-only, no async void, no DateTime.Now, ptt-sync.ps1 addressed correctly for each ticket's nature)
- **Completeness**: PASS (both tickets -- T1 has 5-step SIM, PASS/FAIL criteria, FAIL escalation path, BUILD_FAIL path; T2 has single-line scope, exact strings, clear acceptance criteria)
- **Test Coverage**: PASS (both tickets -- T1 references existing test; T2 explicitly states no new tests required)
- **Scope Isolation**: PASS (both tickets -- mutual cross-region exclusion explicitly stated)
- **Lane-Split Consistency**: PASS (both tickets independently completable with independent 7-scan checklists)

The tickets are approved for engineering execution (Phase 4a).
T1 (P0) and T2 (P3) may be executed in parallel per LANES-APPROVED status.

---

_Review authored by ptt-ticket-reviewer (Phase 3.5). Block B158._
_Input: docs/brain/B158/04-tickets.md, docs/brain/B158/02-architecture-plan.md, docs/brain/B158/02-plan-review.md._
