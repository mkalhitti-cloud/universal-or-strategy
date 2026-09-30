# Ticket Review: BWAVE-NEXT LaneBRepair

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-09-05
**Tickets source**: docs/brain/BWAVE-NEXT/LaneBRepair/04-tickets.md
**Plan source**: docs/brain/BWAVE-NEXT/LaneBRepair/02-architecture-plan.md (REVIEW_PASS)
**Spec source**: docs/brain/BWAVE-NEXT/LaneB-repair-mission-brief.md
**Rules source**: docs/standards/jane-street/RULES_CATALOG.md

---

## RULES CATALOG GATE RESULT: PASS

Catalog read: docs/standards/jane-street/RULES_CATALOG.md (UTF-8 clean).
P0 rules confirmed applicable: JS-021 (lock ban), JS-033 (async void ban),
JS-001 (throw ban in hot paths), JS-002 (return null ban).
Zero P0 violations found in proposed code blocks in ticket T1.
Gate: **PASS**

---

## T1 -- PR43-F1 through PR43-F5 + F7/F8/F9 Test Renames

---

### Traceability

Every spec requirement maps to an implementation step:

| Spec Req | Ticket Section | Status |
|----------|---------------|--------|
| F1 (Filled event triggers double-entry) | "Fix F1 -- OnOrderUpdate drain routing" | PASS |
| F2 (entryCandidates cancels brackets) | "Fix F2 -- entryCandidates predicate" | PASS |
| F3 (TryReplaceOnAtmCancel double-replacement) | "Fix F3 -- _drainOwnedOrderIds field + guard + cleanup" | PASS |
| F4 (TOCTOU payload initialization race) | "Fix F4 -- TOCTOU fix" | PASS |
| F5 (dead cancelCount==0 branch) | "Fix F5 -- Remove dead cancelCount==0 block" | PASS |
| F7 (ActiveOrders test rename) | "Test Renames F7/F8/F9 -- Rename 1" | PASS |
| F8 (NakedDetector test rename) | "Test Renames F7/F8/F9 -- Rename 2" | PASS |
| F9 (Drain tests rename x3) | "Test Renames F7/F8/F9 -- Renames 3, 4, 5" | PASS |

Phantom work check: No implementation steps exist in the ticket that are not in the plan or spec.
Missing work check: All plan Section H items appear in the ticket. No plan item omitted.

**Traceability: PASS**

---

### 7-Scan Checklist Presence

Defense-in-depth check -- all 7 canonical scans (SCAN-01 through SCAN-07) must be
present with explicit shell command and required result.

| Scan | Command Present | Required Result Stated | Status |
|------|----------------|----------------------|--------|
| SCAN 1 -- JS-021 lock() | `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches in new/modified code | PASS |
| SCAN 2 -- JS-033 async void | `grep -rn "async void " src/PropTraderTools/CopyEngine.cs` | 0 matches in new/modified code | PASS |
| SCAN 3 -- JS-002 return null | `grep -n "return null;" src/PropTraderTools/CopyEngine.cs` | 0 matches in new code | PASS |
| SCAN 4 -- CYC check | lizard command + manual count specified; all 6 methods listed with expected post-fix CYC | All <=8 required | PASS |
| SCAN 5 -- ASCII-only | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 matches in new/modified lines | PASS |
| SCAN 6 -- NT8 banned API | `grep -n "Account\.Change\|AtmStrategyCreate\|AtmStrategyChangeStopTarget" src/PropTraderTools/CopyEngine.cs` | 0 matches in new code | PASS |
| SCAN 7 -- Build | `dotnet build src/PropTraderTools 2>&1 \| tail -5` | 0 errors, 0 new warnings | PASS |

All 7 canonical scans present with explicit commands and required results.

**Scan Checklist: PASS**

---

### JS Pre-Check

Scan of all proposed code blocks in the ticket for P0 violations:

| Rule | Check | Proposed Code | Status |
|------|-------|---------------|--------|
| JS-021 (lock ban) | No `lock(` in proposed code | New field: `ConcurrentDictionary<long, byte>`. New code: `TryAdd`, `TryRemove`, `ContainsKey`. No `lock()` anywhere. | PASS |
| JS-033 (async void ban) | No `async void` in proposed code | All modified methods are `private void` (synchronous). No `async` keyword introduced. | PASS |
| JS-002 (return null ban) | No `return null;` in proposed code | All early returns are bare `return;` (void). No null returned. | PASS |
| JS-001 (throw ban in hot paths) | No `throw new XxxException` in proposed code | No throw statement in any proposed code block. | PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check

All post-fix CYC values stated in the ticket and verified against the plan's Section D table:

| Method | Pre-fix CYC | Post-fix CYC (ticket) | Post-fix CYC (plan) | <=8 | Status |
|--------|------------|----------------------|--------------------|----|--------|
| OnOrderUpdate | 7 | 8 | 8 | YES | PASS |
| DrainThenDispatch | 4 | 3 | 3 | YES | PASS |
| TryReplaceOnAtmCancel | 2 | 3 | 3 | YES | PASS |
| SubmitDrainedEntry | 2-3 | 3-4 | 3-4 | YES (max 4) | PASS |
| TryDrainWatchdog | 3 | 4 | 4 | YES | PASS |
| OnDrainCancelAck | stated "<=8 (unchanged)" | unchanged | 3 | YES | PASS |

OnDrainCancelAck adds a ForEach cleanup loop in `SubmitDrainedEntry` (Step D), not in
`OnDrainCancelAck` itself. `OnDrainCancelAck` CYC is stated as unchanged -- consistent with
the plan (Section D: delta 0, CYC 3 -> 3). No budget risk.

TryDrainWatchdog cleanup adds one ForEach loop (Step E). Post-fix CYC = 4, within budget.

**CYC Pre-Check: PASS**

---

### NT8 Constraints

| Constraint | Check | Status |
|-----------|-------|--------|
| No Account.Change() in proposed code | Absent from all proposed code blocks | PASS |
| No AtmStrategyCreate() in proposed code | Absent from all proposed code blocks | PASS |
| No AtmStrategyChangeStopTarget() in proposed code | Absent from all proposed code blocks | PASS |
| (long)(int) pattern preserved -- no Environment.TickCount64 introduced | JS Rule Constraints table explicitly states "DO NOT CHANGE Environment.TickCount64 -> (long)(int) pattern stays as-is" | PASS |
| No async/await in lifecycle methods | All modified methods are synchronous | PASS |
| No Account.All call outside Loaded handler | Not referenced in proposed code | PASS |
| No sealed on TradeCopierWindow | Not referenced in proposed code | PASS |
| No FontFamily set on WPF element | Not referenced in proposed code | PASS |
| No hardcoded hex color | Not referenced in proposed code | PASS |
| No CreateOrder with name not starting "PTT-" | No CreateOrder in proposed code; new entry orders use "PTT-Copy" via SubmitEntryDirect (existing, unchanged) | PASS |
| No DateTime.Now usage | Not referenced in proposed code | PASS |

**NT8 Check: PASS**

---

### Completeness

| Check | Result | Status |
|-------|--------|--------|
| "SCOPE LOCK -- TICKET 1 ONLY" header present | Present at top of ticket: "SCOPE LOCK -- TICKET 1 ONLY." | PASS |
| All 3 files to edit listed | CopyEngine.cs, BwaveDwLaneATests.cs, BwaveNextLaneBTests.cs -- all 3 listed in "Files to Edit" section | PASS |
| Pre-read steps cover all required line ranges | 6 pre-read ranges: lines 855-875, 1395-1430, 6480-6720, 3660-3710, BwaveDwLaneATests.cs, BwaveNextLaneBTests.cs | PASS |
| Completion artifact template specified | Full template in "Completion Artifact" section with all 7 scans and test rename table | PASS |
| Completion artifact location correct | `docs/brain/BWAVE-NEXT/LaneBRepair/ticket-1-completion.md` | PASS |
| Method signatures for all affected methods present | "Method Signatures Affected" section lists all 5 methods + new field + ctor | PASS |
| Acceptance criteria mirror spec | T1 acceptance criteria matches spec acceptance criteria item-for-item | PASS |

**Completeness: PASS**

---

### Test Coverage

| Check | Result | Status |
|-------|--------|--------|
| All 5 test renames specified with old -> new names | All 5 renames present in "Test Renames F7/F8/F9" section | PASS |
| BwaveDwLaneATests.cs identified for F7/F8 | "In BwaveDwLaneATests.cs" subsection present with Renames 1-2 | PASS |
| BwaveNextLaneBTests.cs identified for F9 | "In BwaveNextLaneBTests.cs" subsection present with Renames 3-5 | PASS |
| "Rename ONLY -- do NOT change bodies" constraint explicitly stated | "Rule: Change only the method declaration line. Do NOT change: [Fact] attribute, method body, assertions, comments inside body." -- present verbatim | PASS |
| xUnit [Fact] names listed for post-rename verification | "xUnit Test Names (post-rename)" section lists all 5 with [Fact] prefix and dotnet test filter command | PASS |
| New methods requiring [Fact] tests | No NEW methods introduced. Only: (a) new private field (no test needed), (b) new property on PendingDispatchDrain (data class, covered structurally), (c) constructor parameter expansion (covered by existing test structure). No new public/internal behavior-introducing methods. | PASS |

**Test Coverage: PASS**

---

### Out-of-Scope Exclusions Verified

| Out-of-Scope Item | Ticket Implements? | Status |
|------------------|--------------------|--------|
| TickCount64 | Not mentioned in any implementation step. JS Rule Constraints table bans it. | PASS |
| Remove .ToList() | JS Rule Constraints table explicitly: "DO NOT CHANGE .ToList() on ActiveOrders stays as-is (thread-safety fix)." | PASS |
| Drain key extension (DW-NEXT-B-01) | Not referenced in any implementation step. | PASS |
| GTC/Day TIF preservation (DW-NEXT-B-02) | Not referenced in any implementation step. | PASS |

**Out-of-Scope Exclusions: PASS**

---

### File Routing

| Check | Result | Status |
|-------|--------|--------|
| CopyEngine.cs path | `src/PropTraderTools/CopyEngine.cs` -- Wave workspace | PASS |
| BwaveDwLaneATests.cs path | `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` -- Wave workspace | PASS |
| BwaveNextLaneBTests.cs path | `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` -- Wave workspace | PASS |
| No Director workspace paths for .cs files | No reference to `universal-or-strategy-director` in any file path | PASS |

**File Routing: PASS**

---

## Summary Table

| Check Category | Result |
|----------------|--------|
| Rules Catalog Gate | PASS |
| Traceability | PASS |
| 7-Scan Checklist Presence | PASS |
| JS Pre-Check | PASS |
| CYC Pre-Check | PASS |
| NT8 Constraints | PASS |
| Completeness | PASS |
| Test Coverage | PASS |
| Out-of-Scope Exclusions | PASS |
| File Routing | PASS |

---

## Violations

None.

---

## Overall: TICKET_REVIEW_PASS

**T1 VERDICT: TICKET_REVIEW_PASS**

The ticket is cleared for engineer execution. All 7 canonical scans are present per
defense-in-depth contract (Layers 1-3). All spec requirements F1-F5 and F7/F8/F9 are
covered. No JS P0 violations in proposed code. All CYC values within budget. File routing
correct. Completion artifact template specified. Engineer may proceed.

---

*Review authored: 2026-09-05 | ptt-ticket-reviewer | Phase 3.5 | BWAVE-NEXT LaneBRepair*
