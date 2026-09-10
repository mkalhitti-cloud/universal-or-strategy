# 04-ticket-review.md
# Epic: PTT-REPAIRS-07-NT8-BULK-SKIP
# Phase: 3.5 -- Ticket Review
# Reviewer: ptt-ticket-reviewer
# Source tickets: 04-tickets.md (generated from 02-architecture-plan.md REVIEW_PASS Rev 1)

---

## Ticket Review: PTT-REPAIRS-07-NT8-BULK-SKIP

---

### T1 -- OPTION A Bulk Skip + OPTION B Verified Skip

---

#### 1. TRACEABILITY

Every ticket item maps to a plan section:

| Ticket Reference | Plan Section | Status |
|---|---|---|
| PLAN-SEC4: Class map, line ranges | Plan §4 File Inventory | MAPPED |
| PLAN-SEC5-OPTION-A: Bulk replace (5 classes) | Plan §5 OPTION A | MAPPED |
| PLAN-SEC5-OPTION-B: B79BeAllTargetSnapshotTests, B79BeReplaceAttemptGuardTests | Plan §5 OPTION B | MAPPED |
| PLAN-SEC6: OPTION B pre-analysis for both classes | Plan §6 OPTION B Pre-Analysis | MAPPED |
| PLAN-SEC7: Ticket 1 scope definition | Plan §7 Ticket Split | MAPPED |
| PLAN-SEC8: Baseline + SCAN-5 Ticket 1 target | Plan §8 Baseline and Target | MAPPED |
| PLAN-SEC9: All constraints | Plan §9 Critical Constraints | MAPPED |
| PLAN-SEC10: 7-scan checklist | Plan §10 7-Scan Checklist | MAPPED |
| PLAN-SEC13: Hard-link sync | Plan §13 Hard-Link Sync | MAPPED |

No phantom work (items in ticket not in plan/spec). No missing plan work (plan §11, §12, §14 are informational/summary -- no engineering action).

**Traceability: PASS**

---

#### 2. JS Pre-Check

| Rule | Check | Result |
|---|---|---|
| JS-021/023 lock() / mutex / SemaphoreSlim | Not described anywhere in T1 | PASS |
| JS-023 UI update without Dispatcher.InvokeAsync | No UI code described | PASS |
| JS-001 throw in hot path | No throw statements described | PASS |
| JS-002 return null | No logic code added | PASS |
| JS-003 magic string for discriminated state | Skip string is xUnit metadata, not FSM state | PASS |
| JS-008 mutable struct fields | None described | PASS |
| JS-008 SolidColorBrush without Freeze() | None described | PASS |
| JS-009 Dictionary for shared state | None described | PASS |
| NT8: async/await in lifecycle method | None described | PASS |
| NT8: Account.All outside Loaded | None described | PASS |
| NT8: sealed TradeCopierWindow | Not applicable (test file) | N/A |
| NT8: FontFamily on WPF element | None described | PASS |
| NT8: hardcoded hex color | None described | PASS |
| NT8: CreateOrder without PTT- prefix | None described | PASS |
| NT8: DateTime.Now | None described | PASS |
| SkipReason constants | Explicitly prohibited ("No new SkipReason constants") | PASS |
| Curly quotes / Unicode in Skip string | Explicitly prohibited ("ASCII-only") | PASS |

**JS Pre-Check: PASS**

---

#### 3. CYC Pre-Check

No new methods introduced. All changes are attribute annotations ([Fact] -> [Fact(Skip=...)]) on existing test methods. Cyclomatic complexity analysis is not applicable.

**CYC Pre-Check: PASS (N/A -- no new methods)**

---

#### 4. NT8 Check

No NT8 API introduced. [Fact(Skip=...)] is pure xUnit v2 syntax. No calls to Account.All, CreateOrder, AtmStrategyCreate, or any NinjaTrader API. Plan §11 explicitly confirms this.

**NT8 Check: PASS**

---

#### 5. Test Coverage

No new methods introduced. Skip annotations modify existing [Fact] test metadata only. No new test methods require [Fact] coverage.

**Test Coverage: PASS (N/A -- no new methods added)**

---

#### 6. Scan Checklist

All 7 scans present in T1 with correct commands and expected values:

| Scan | Command Present | Expected Value | SCAN-7 HardLink Check |
|---|---|---|---|
| SCAN-1 | `grep -c "lock("` | 0 | -- |
| SCAN-2 | `Select-String -Pattern "[^\x00-\x7F]"` | 0 non-ASCII | -- |
| SCAN-3 | `dotnet build --no-restore \| Select-String "Error"` | 0 CS errors | -- |
| SCAN-4 | `dotnet build \| Select-String " Error\(s\)"` | "0 Error(s)" | -- |
| SCAN-5 | `dotnet test --no-build \| Select-String "Total\|..."` | Total=501, Passed>=19, Failed<=157, Skipped>=325 | -- |
| SCAN-6 | `powershell -File .\deploy-sync.ps1` | "SYNC COMPLETE" | -- |
| SCAN-7 | `$f = Get-Item ...; $f.LinkType + " count=" + $f.HardLinkCount` | LinkType="HardLink" AND HardLinkCount>=2 | CORRECT: count>=2 required; count=1 explicitly flagged as FAIL state |

SCAN-7 correctly requires `LinkType = "HardLink" AND HardLinkCount >= 2` with an explicit note: "IMPORTANT: count=1 means NOT hard-linked (regular file -- SCAN-6 sync FAILED)". This satisfies the RULE-02 fix confirmed in 02-plan-review.md.

**Scan Checklist: PASS**

---

#### 7. File Routing

T1 file in scope: `src/PropTraderTools/CopyEngineTests.cs`

This is the Wave workspace path (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`). No Director workspace path referenced for any .cs file.

**File Routing: PASS**

---

#### 8. Option A Coverage (Checklist Item 2)

All active [Fact] in OPTION A classes are marked for Skip. Existing Skip counts preserved.

| Class | Active [Fact] | Skip Target | Already-Skipped Preserved | Double-Skip Risk |
|---|---|---|---|---|
| CopyEngineTests | 188 | 188 | 17 -- explicit preservation constraint stated | NONE: "Do NOT alter any [Fact(Skip...)] line" |
| CopyEngineB75Tests | 47 | 47 | 14 (7 single-line + 7 multi-line) -- all line numbers listed | NONE: multi-line format explicitly protected |
| B77QxRaceGuardTests | 8 | 8 | 0 | N/A |
| B78TargetDispatchTests | 8 | 8 | 0 | N/A |
| BwaveCycTaR7HelperTests | 34 | 34 | 1 at line 8059 -- "Do NOT double-skip it" | NONE: explicit protection |

Counts match plan §4 and §5. Preservation constraints explicit per class.

**Option A Coverage: PASS**

---

#### 9. Option B Exactness (Checklist Item 3)

| Class | Method Names Listed | Filter Command Present | Lane A Boundary Stated |
|---|---|---|---|
| B79BeAllTargetSnapshotTests | YES -- all 8 listed with lines and inferences | YES -- `FullyQualifiedName~B79BeAllTargetSnapshotTests` | YES -- "Do NOT apply Skip to the 1 passing test, regardless of which test it is" |
| B79BeReplaceAttemptGuardTests | YES -- all 3 listed with lines and inferences | YES -- `FullyQualifiedName~B79BeReplaceAttemptGuardTests` | YES -- "Do NOT apply Skip to the 2 passing tests" |

Verification re-run steps present for both classes. Filter is authoritative (architect inferences are guides only). PASS.

---

#### 10. Baseline Capture (Checklist Item 7)

T1 Pre-Conditions section contains:
- Mandatory fresh baseline command: `dotnet test src/PropTraderTools/ --no-build 2>&1 | Select-String "Total|Passed|Failed|Skipped"`
- Record instruction: "Record the output."
- STOP gate: "If the numbers differ from this baseline, STOP and report before making any changes."
- Engineer Notes: "Record actual pre-change baseline from `dotnet test` run at the start of this ticket."

Engineer does NOT rely on prompt numbers -- actual runtime measurement required.

**Baseline Capture: PASS**

---

#### 11. Constraint Checklist (Checklist Item 9)

All constraints present in T1 Constraints Checklist section:
- [x] Only CopyEngineTests.cs modified
- [x] Zero production code changes
- [x] Exact Skip string
- [x] ASCII-only
- [x] No lock()
- [x] No throw
- [x] No SkipReason constants
- [x] Existing [Fact(Skip...)] not modified (17+14+1 preservation)
- [x] OPTION B filter-first requirement for both B79 classes
- [x] Skip applied ONLY to TypeInit-failing tests

**Constraint Checklist: PASS**

---

### T1 VERDICT: TICKET_REVIEW_PASS

---

---

### T2 -- OPTION B Individual Skip (3 mixed classes)

---

#### 1. TRACEABILITY

| Ticket Reference | Plan Section | Status |
|---|---|---|
| PLAN-SEC5-OPTION-B: BwaveCycTaR6HelperTests (6), B79CancelRaceGuardTests (5), BwaveCycT1R1BeHelperTests (2) | Plan §5 OPTION B | MAPPED |
| PLAN-SEC6: OPTION B pre-analysis for all three classes | Plan §6 | MAPPED |
| PLAN-SEC7: Ticket 2 scope definition | Plan §7 Ticket Split | MAPPED |
| PLAN-SEC8: Post-T2 SCAN-5 targets | Plan §8 | MAPPED |
| PLAN-SEC9: All constraints | Plan §9 | MAPPED |
| PLAN-SEC10: 7-scan checklist | Plan §10 | MAPPED |
| PLAN-SEC13: Hard-link sync | Plan §13 | MAPPED |

No phantom work. No missing plan work.

**Traceability: PASS**

---

#### 2. JS Pre-Check

Identical analysis to T1 -- T2 is also pure xUnit attribute annotation work. No JS rule violations described.

**JS Pre-Check: PASS**

---

#### 3. CYC Pre-Check

No new methods introduced.

**CYC Pre-Check: PASS (N/A)**

---

#### 4. NT8 Check

No NT8 API introduced. Same as T1.

**NT8 Check: PASS**

---

#### 5. Test Coverage

No new methods introduced.

**Test Coverage: PASS (N/A)**

---

#### 6. Scan Checklist

All 7 scans present in T2 with correct commands and expected values:

| Scan | Command Present | Expected Value | SCAN-7 HardLink Check |
|---|---|---|---|
| SCAN-1 | `grep -c "lock("` | 0 | -- |
| SCAN-2 | `Select-String -Pattern "[^\x00-\x7F]"` | 0 non-ASCII | -- |
| SCAN-3 | `dotnet build --no-restore \| Select-String "Error"` | 0 CS errors | -- |
| SCAN-4 | `dotnet build \| Select-String " Error\(s\)"` | "0 Error(s)" | -- |
| SCAN-5 | `dotnet test --no-build \| Select-String "Total\|..."` | Total=501, Passed>=19, Failed<=144, Skipped>=338 | -- |
| SCAN-6 | `powershell -File .\deploy-sync.ps1` | "SYNC COMPLETE" | -- |
| SCAN-7 | `$f = Get-Item ...; $f.LinkType + " count=" + $f.HardLinkCount` | LinkType="HardLink" AND HardLinkCount>=2 | CORRECT: count>=2 required; count=1 explicitly flagged as FAIL state |

**Scan Checklist: PASS**

---

#### 7. File Routing

T2 file in scope: `src/PropTraderTools/CopyEngineTests.cs` -- Wave workspace path only.

**File Routing: PASS**

---

#### 8. Option B Exactness (Checklist Item 3)

| Class | Method Names Listed | Filter Command Present | Lane A Boundary Stated |
|---|---|---|---|
| BwaveCycTaR6HelperTests | YES -- 5 named candidates, 6th requires filter | YES -- `FullyQualifiedName~BwaveCycTaR6HelperTests` | YES -- "Do NOT apply Skip to the 11 passing Lane A tests" |
| B79CancelRaceGuardTests | YES -- 4 named candidates, 5th requires filter | YES -- `FullyQualifiedName~B79CancelRaceGuardTests` | YES -- "DO NOT TOUCH the 59 Lane A tests in this class" |
| BwaveCycT1R1BeHelperTests | YES -- 6 named candidates (2 of which are TypeInit, filter is authoritative) | YES -- `FullyQualifiedName~BwaveCycT1R1BeHelperTests` | YES -- "DO NOT TOUCH the other 23 tests in this class" |

Verification re-run steps present for all three classes. Filter is authoritative. PASS.

---

#### 9. Baseline Capture (Checklist Item 7)

T2 Pre-Conditions section contains:
- Mandatory post-T1 baseline command: `dotnet test src/PropTraderTools/ --no-build 2>&1 | Select-String "Total|Passed|Failed|Skipped"`
- Record instruction: "Record the output."
- STOP gate: "If T1 SCAN-5 was not met (skipped < 325 or failed > 157), STOP and complete T1 first."
- Engineer Notes: "Record actual pre-change baseline from `dotnet test` run at the start of this ticket (must reflect T1 completed state)."

Engineer measures actual post-T1 state, not assumed prompt numbers.

**Baseline Capture: PASS**

---

#### 10. SCAN-5 Numeric Targets (Checklist Item 8)

T2 applies 13 new Skips (6+5+2). Starting from T1 post-state (Skipped>=325, Failed<=157):
- Failed: 157 - 13 = 144. Ticket requires Failed<=144. ✓
- Skipped: 325 + 13 = 338. Ticket requires Skipped>=338. ✓
- Total: 501 unchanged. ✓
- Passed: >= 19 unchanged. ✓

Math is consistent with plan §8 and §7 Ticket 2 SCAN-5 checkpoint.

**SCAN-5 Numeric Targets: PASS**

---

#### 11. Constraint Checklist (Checklist Item 9)

All constraints present in T2 Constraints Checklist section including per-class Lane A preservation counts (59 in B79Cancel, 23 in BwaveCycT1R1, 11 in BwaveR6).

**Constraint Checklist: PASS**

---

#### 12. Ticket Isolation (Checklist Item 10)

T2 classes (BwaveCycTaR6HelperTests lines 7110-7268, B79CancelRaceGuardTests lines 5823-6455, BwaveCycT1R1BeHelperTests lines 6469-6678) are disjoint from all T1 class line ranges. No shared method names. No shared state. T2 engineering action (named-test filter-based Skip) is fully independent. If T1 scope changes, T2's specific test method targets are unaffected. T2's SCAN-5 numeric targets would adjust with the actual post-T1 baseline measured at T2 start -- the Pre-Conditions command handles this.

**Ticket Isolation: PASS**

---

### T2 VERDICT: TICKET_REVIEW_PASS

---

---

## Observations (non-blocking)

The following are informational discrepancies between ticket line ranges and plan §4. They do NOT affect engineering outcomes because all affected classes use OPTION B (filter-driven named-test skip, not range-based replace):

| Class | Plan §4 End Line | Ticket End Line | Δ | Impact |
|---|---|---|---|---|
| B79BeAllTargetSnapshotTests | 5624 | 5615 | -9 | NONE -- OPTION B, filter-driven |
| B79BeReplaceAttemptGuardTests | 5691 | 5683 | -8 | NONE -- OPTION B, filter-driven |
| BwaveCycT1R1BeHelperTests | 6685 | 6678 | -7 | NONE -- OPTION B, filter-driven |

These are cosmetic navigational discrepancies only. The engineer uses test filter commands (FullyQualifiedName~ClassName), not line range replaces, for all three classes. No correction required.

---

## Overall: TICKET_REVIEW_PASS

**Violations found: 0**

All 10 review checklist items PASS for both tickets:

| Checklist Item | T1 | T2 |
|---|---|---|
| 1. Traceability | PASS | PASS |
| 2. Option A Coverage | PASS | N/A |
| 3. Option B Exactness | PASS | PASS |
| 4. Skip String Compliance | PASS | PASS |
| 5. No Production Code | PASS | PASS |
| 6. 7-Scan Checklist (all 7 present, SCAN-7 HardLinkCount>=2) | PASS | PASS |
| 7. Baseline Capture | PASS | PASS |
| 8. SCAN-5 Numeric Targets | PASS | PASS |
| 9. Constraint Checklist | PASS | PASS |
| 10. Ticket Isolation | PASS | PASS |

Phase 3.5 gate OPEN. Engineer may proceed to Phase 4a implementation.

---

TICKET_REVIEW_PASS
