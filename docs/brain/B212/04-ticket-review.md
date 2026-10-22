# B212 Ticket Review
## Block: B211+B212 Repair Batch
## Reviewer: PTT Ticket Reviewer (ptt-ticket-reviewer mode)
## Pipeline Phase: Phase 3.5

**Source files reviewed:**
- `docs/brain/B212/04-tickets.md` (TICKETS_COMPLETE)
- `docs/brain/B212/02-architecture-plan.md` (REVIEW_PASS)
- `docs/brain/NO-PIPELINE-REPAIRS.md` (SESSION: 2026 B211/B212, lines 3209-3800)
- `docs/standards/jane-street/RULES_CATALOG.md` (JS-001..JS-110)

---

## T1 — B211 Button Repairs

### A — Traceability

**Spec req IDs present:** YES — B211-ASK-01, B211-MOVETARGET-01, B211-ASKALL-01, B211-BIDALL-01
(T1.1 Header table).

**Hotfix IDs mapped:** YES — HOTFIX-B211-ASK-01, HOTFIX-B211-MOVETARGET-01, HOTFIX-B211-ASKALL-01,
HOTFIX-B211-BIDALL-01. Each maps precisely to NO-PIPELINE-REPAIRS.md entries at lines 3225-3358.

**Files in scope match hotfix log:** NEAR-PASS with one note.
The hotfix log at line 3215 identifies HOTFIX-B211-ASK-01 as being in `TradeCopierPanel.cs /
OnAskClick` (single account path). T1.1 correctly maps the predicate widening (IsWorkingTarget)
to `PttMoveTargetOne.cs` (the refactored home of that logic). Acceptable: the architecture plan
Section 1 explicitly accounts for this refactoring.

T1.1 also lists `TradeCopierAddOn.cs` (MODIFIED, wiring) and `PttContracts.cs` (MODIFIED,
constants). Both appear in architecture plan Section 0 file table. PASS.

**No phantom work:** YES — all items in T1 trace to hotfix IDs. PASS.

**Traceability: PASS**

---

### B — JS Pre-Check (P0)

**JS-021 (lock):** T1.6 SCAN-01 covers PttMoveTargetOne.cs, PttMoveTargetAll.cs, TradeCopierPanel.cs.
JS-DNA table in T1.4 explicitly states "no lock" for every method. PASS.

**JS-001 (throw new):** T1.4 JS-DNA table states "no throw / try/catch log-and-swallow" for
Execute; "no throw" for all other methods. PASS.

**JS-033 (async void):** T1.4 JS-DNA table explicitly states OnAskAllClick / OnBidAllClick are
"RoutedEventHandler -- sync void allowed" (non-async event handler). PASS.

**JS-002 (no null return):** CollectWorkingTargets returns `new Order[0]` not null. Explicitly
stated in T1.3. PASS.

**CYC (JS-066):** All 7 methods listed with targets <= 8 (max is TryGetExitPrice at 7). PASS.

**ASCII-only (JS-081):** T1.4 JS-DNA table states "Interlocked.Increment only" with no Unicode.
SCAN-05 grep covers new files. PASS.

**JS Pre-Check: PASS**

---

### C — NT8 Constraints

**NT8-049 compliance documented:** YES — T1.4 includes a full NT8-049 CreateOrder arg-position
table showing arg6=limitPrice and arg7=stopPrice with concrete call examples. PASS.

**CollectWorkingTargets does NOT pick up PTT-MA-Stop-*:** YES — T1.3 states: "IsWorkingTarget
requires `OrderType.Limit`. `PTT-MA-Stop-*` orders are `StopMarket` -- they are excluded from
`CollectWorkingTargets` in subsequent calls." Explicitly documented. PASS.

**Account.Cancel + Account.CreateOrder + Submit() -- AtmStrategyChangeStopTarget NOT used:**
YES — T1.4 Fix Description uses `acc.CreateOrder(...)` + `acc.Submit(new[] {...})`. The
architecture plan Section 8 explicitly confirms "AtmStrategyChangeStopTarget() -- StrategyBase-only:
NOT used. PASS." PASS.

**NT8 Constraints: PASS**

---

### D — Completeness

**Required structural elements per ticket:**

| Element | Present? |
|---------|----------|
| Header (T1.1) | YES |
| Problem statement (T1.2) | YES |
| Fix description (T1.3) | YES |
| Method signatures (T1.4) | YES |
| [Fact] test names (T1.5) | YES |
| 7-scan checklist (T1.6) | YES |
| Pass criteria (T1.7) | YES |
| ptt-sync requirement (T1.8) | YES |

**Minimum 10 [Fact] tests:** T1.5 lists 12 named test methods. PASS (12 >= 10).

**xUnit only ([Fact] — no [Test], no NUnit):** YES — all test entries in T1.5 use [Fact] attribute
notation; no [Test], [TestCase], [TestFixture], or NUnit attributes present. PASS.

**Completeness: PASS**

---

### E — Engineer Safety Guards

**Two-pass count+fill explanation (no List<T> due to CS0246):** YES — T1.3 explicitly states:
"No `List<T>` used (NT8 NinjaScript CS0246 avoidance -- `System.Collections.Generic` not always in
implicit using scope)." Architecture plan Section G also explains this in full. PASS.

**SCAN-02 scope gap (FAIL):**
T1.6 SCAN-02 covers only `PttMoveTargetOne.cs` and `PttMoveTargetAll.cs`:
```powershell
grep -rn "throw new" src/PropTraderTools/Features/PttMoveTargetOne.cs `
              src/PropTraderTools/Features/PttMoveTargetAll.cs
```
`TradeCopierPanel.cs` is listed in T1.1 as an in-scope MODIFIED file. It is NOT included in
SCAN-02. If `throw new` were inadvertently introduced in the modified `OnAskAllClick` /
`OnBidAllClick` methods, this scan would NOT detect it.

**Violation:** T1.6 SCAN-02 — JS-001 grep scope omits `TradeCopierPanel.cs`.
**Required:** Add `src/PropTraderTools/TradeCopierPanel.cs` to SCAN-02 command.

**SCAN-03 scope gap (FAIL):**
T1.6 SCAN-03 covers only `PttMoveTargetOne.cs` and `PttMoveTargetAll.cs`. `TradeCopierPanel.cs`
is in scope for T1 but is excluded from the async void scan. Although the ticket correctly
notes that OnAskAllClick/OnBidAllClick are sync void event handlers, the scan command does not
enforce this — it cannot verify that `TradeCopierPanel.cs` contains no `async void` in modified
regions.

**Violation:** T1.6 SCAN-03 — JS-033 grep scope omits `TradeCopierPanel.cs`.
**Required:** Add `src/PropTraderTools/TradeCopierPanel.cs` to SCAN-03 command.

**SCAN-05 scope gap (FAIL):**
T1.6 SCAN-05 (ASCII-only grep) covers only `PttMoveTargetOne.cs` and `PttMoveTargetAll.cs`.
In-scope modified files `TradeCopierPanel.cs` and `PttContracts.cs` (listed in T1.1) are not
covered by SCAN-05. Non-ASCII characters in those files would not be detected.

**Violation:** T1.6 SCAN-05 — ASCII-only grep scope omits `TradeCopierPanel.cs` and
`PttContracts.cs`.
**Required:** Add both files to SCAN-05 command.

**Engineer Safety Guards: FAIL**
Violations: SCAN-02 omits TradeCopierPanel.cs; SCAN-03 omits TradeCopierPanel.cs;
SCAN-05 omits TradeCopierPanel.cs and PttContracts.cs.

---

### F — Test Coverage

**CollectWorkingTargets: tests for both PTT-* and native Target[1-9] names:**
T1.5 includes `CollectWorkingTargets_BothPttAndNativeTargetsFound` (asserts IsWorkingTarget
returns true for "PTT-MA-Ask-1" AND "Target1"). Checklist requirement satisfied. PASS.

**Architecture plan gap — missing test `IsWorkingTarget_PttPrefixed_ReturnsTrue` (WARN):**
Architecture plan Section 4 lists `IsWorkingTarget_PttPrefixed_ReturnsTrue` as a required
additional test. T1.5 covers this implicitly via `CollectWorkingTargets_BothPttAndNativeTargetsFound`
(which asserts PTT-MA-Ask-1 is found) but does not list a standalone `IsWorkingTarget_PttPrefixed_ReturnsTrue`
or `CollectWorkingTargets_MethodExists_TwoPassArray` (reflection) test.
This is a WARN (not FAIL) because the predicate logic is covered by the combined test.

**Test Coverage: PASS (with WARN noted above)**

---

### T1 VERDICT: TICKET_REVIEW_FAIL

**Violations (3):**
1. **SCAN-02 scope omits TradeCopierPanel.cs** (T1.6, SCAN-02): JS-001 throw new prohibition
   scan does not cover an in-scope modified file.
2. **SCAN-03 scope omits TradeCopierPanel.cs** (T1.6, SCAN-03): JS-033 async void prohibition
   scan does not cover an in-scope modified file.
3. **SCAN-05 scope omits TradeCopierPanel.cs and PttContracts.cs** (T1.6, SCAN-05):
   ASCII-only scan does not cover two in-scope modified files. Violation of JS-081/JS-082.

---

---

## T2 — B212 MA-STOP-02 OCO Pair

### A — Traceability

**Spec req IDs present:** YES — B212-MA-STOP-02 (T2.1). Correctly notes HOTFIX-B212-MA-STOP-02
supersedes HOTFIX-B212-MA-STOP-01. NO-PIPELINE-REPAIRS.md lines 3653-3719 confirm STOP-02 as
the authoritative fix. PASS.

**Hotfix IDs mapped:** YES — HOTFIX-B212-MA-STOP-02. Traceable. PASS.

**Files in scope match hotfix log:** YES — `PttMoveTargetOne.cs` (MODIFIED). Hotfix log at
line 3674 confirms same file. `B212Tests.cs` (NEW/MODIFIED) for tests. PASS.

**No phantom work:** YES — T2 scope is limited to Execute OCO loop modification.
STOP-01 correctly noted as superseded and not formalized. PASS.

**Traceability: PASS**

---

### B — JS Pre-Check (P0)

**JS-021 (lock):** T2.4 JS-DNA table: "No lock(). Interlocked.Increment(ref _maSeq) for
sequence counter. NextBeOcoSeq() uses Interlocked internally." SCAN-01 covers PttMoveTargetOne.cs.
PASS.

**JS-001 (throw new):** T2.4 states "No throw new. Each CreateOrder+Submit call is wrapped
in the existing try/catch log-and-swallow block." SCAN-02 covers PttMoveTargetOne.cs. PASS.

**JS-033 (async void):** T2.4 states "Execute is static void (sync). No async keyword."
SCAN-03 covers PttMoveTargetOne.cs. PASS.

**JS-002 (no null return):** T2.4 states "ocoId is never null. snapshotStop==0 path routes
to string.Empty ocoId, not null." PASS.

**CYC (JS-066):** Execute CYC <= 7 (was 6 in T1, +1 for OCO branch). Within JS-066 ceiling
of 8. PASS.

**ASCII-only (JS-081):** T2.4 JS-DNA table states all prefixes are ASCII. Noted in DNA row. PASS.

**JS Pre-Check: PASS**

---

### C — NT8 Constraints

**NT8-049 compliance for OCO pair (arg6/arg7 positions):** YES — T2.3 explicitly documents:
- StopMarket: arg6=`0.0` (limitPrice), arg7=`snapshotStop` (stopPrice)
- Limit: arg6=`exitPrice` (limitPrice), arg7=`0.0` (stopPrice)
With code snippets showing exact CreateOrder call. PASS.

**SCAN-05 repurposed to NT8-049 manual check:** T2.6 SCAN-05 is the NT8-049 argument position
compliance manual scan. The requirement "ASCII-only or NT8-049 compliance check stated" is met.
PASS.

**CollectWorkingTargets does NOT pick up PTT-MA-Stop-*:** YES — T2.3 explicitly states:
"IsWorkingTarget filter safety: IsWorkingTarget requires OrderType.Limit. PTT-MA-Stop-* orders
are StopMarket -- they are excluded from CollectWorkingTargets in subsequent calls." PASS.

**snapshotStop==0 path submits target-only with ocoId=string.Empty:** YES — T2.3 states:
"When snapshotStop == 0: submit target only with ocoId = string.Empty (no-OCO path)." And
T2.5 test `OcoId_Empty_WhenSnapshotStopZero` asserts this. PASS.

**NT8 Constraints: PASS**

---

### D — Completeness

**Required structural elements per ticket:**

| Element | Present? |
|---------|----------|
| Header (T2.1) | YES |
| Problem statement (T2.2) | YES |
| Fix description (T2.3) | YES |
| Method signatures (T2.4) | YES |
| [Fact] test names (T2.5) | YES |
| 7-scan checklist (T2.6) | YES |
| Pass criteria (T2.7) | YES |
| ptt-sync requirement (T2.8) | YES |

**Minimum 5 [Fact] tests:** T2.5 lists 9 named test methods. PASS (9 >= 5).

**xUnit only:** All test entries in T2.5 use [Fact] notation. No [Test] or NUnit attributes. PASS.

**Completeness: PASS**

---

### E — Engineer Safety Guards

**snapshotStop==0 path note (target-only, ocoId=string.Empty):** YES — T2.3 states this
explicitly. T2.5 test `OcoId_Empty_WhenSnapshotStopZero` asserts it. PASS.

**No additional safety guard gaps identified for T2.**

**Engineer Safety Guards: PASS**

---

### F — Test Coverage

**Stop reaches Working (not just Accepted) via ocoId linkage — test required:**
The review checklist requires: "test confirms stop goes Working (not just Accepted) via ocoId
linkage." T2.5 lists `OcoId_NotEmpty_WhenSnapshotStopPresent` (asserts ocoId not empty) and
`StopPriceArg_IsSnapshotStop_NotLimitPrice` (asserts NT8-049 arg positions). Neither test
asserts the Working state transition. The checklist explicitly requires this assertion.

The ticket correctly marks NT8-dependent tests as "Skip (NT8 Account.CreateOrder)". The
Working-state transition requires NT8 SIM infrastructure and cannot be unit-tested. However,
the test T2.5 does not assert the structural precondition that enables the Working transition:
that BOTH the stop and the target carry the same non-empty ocoId string. The test
`OcoId_NotEmpty_WhenSnapshotStopPresent` covers only the stop ocoId.

A test asserting that the TARGET order also receives the same non-empty ocoId (verifying
the OCO PAIR is fully linked, not just the stop) is absent. This is the structural precondition
that guarantees the Working-state transition — and it IS testable as pure string logic.

**Violation:** T2.5 — no test asserting that `targetOrd.ocoId == stopOrd.ocoId` (same
non-empty string for both orders in the pair). The ocoId equality of the pair is the critical
invariant per T2.3, and it is testable without NT8. Without this test the engineer has no
automated check that the pair is correctly linked.

**Remediation:** Add a test (e.g. `B212_STOP02_OcoId_MatchesBetweenStopAndTarget_InSamePair`)
that asserts both generated orders carry the same non-empty ocoId when snapshotStop > 0.
This is pure string/logic — no NT8 dependency.

**Test Coverage: FAIL**
Violation: Missing test asserting stop.ocoId == target.ocoId (same non-empty string) for the
paired orders. Checklist item F requires confirmation of the structural precondition for
Working-state transition.

---

### T2 VERDICT: TICKET_REVIEW_FAIL

**Violations (1):**
1. **Missing OCO pair equality test** (T2.5): No test asserts `stop.ocoId == target.ocoId`
   (same non-empty string). The paired ocoId equality is the critical invariant that enables
   the Working-state transition (root cause of STOP-01 failure). This invariant is testable
   as pure string logic and must have a [Fact] covering it.

---

---

## T3 — B212 Re-entry Suppression

### A — Traceability

**Spec req IDs present:** YES — B212-FLATTEN-LEADERSKIP-01, B212-REENTRY-V3 (T3.1). PASS.

**Hotfix IDs mapped:** YES — HOTFIX-B212-FLATTEN-LEADERSKIP-01 (NO-PIPELINE-REPAIRS.md line
3444), HOTFIX-B212-REENTRY-V3 (line 3721). Both match exactly. PASS.

**Reverted hotfixes listed:** YES — T3.1 explicitly lists HOTFIX-B212-BEALL-LEADERSKIP-01 and
HOTFIX-B212-BREAKEVEN-INSTROVERLOAD-01 v1+v2 as reverted and "do NOT re-implement". PASS.

**Files in scope match hotfix log:** YES — `CopyEngine.cs` (MODIFIED) for both hotfixes;
`PttBeSwapAll.cs` (MODIFIED — revert + comment) for BEALL-LEADERSKIP-01 revert. Both match
hotfix log entries. PASS.

**No phantom work:** PASS — all items trace to hotfix IDs.

**Traceability: PASS**

---

### B — JS Pre-Check (P0)

**JS-021 (lock) — FAIL:**
T3.6 SCAN-01 issues a file-wide grep on `CopyEngine.cs`:
```powershell
grep -rn "lock(" src/PropTraderTools/CopyEngine.cs `
             src/PropTraderTools/Features/PttBeSwapAll.cs
```
The expected result is stated as "0 results in any new or modified code sections." However,
`CopyEngine.cs` is a large file (~6000+ lines). If the file contains any pre-existing `lock()`
calls in unmodified methods, this file-wide grep will return non-zero results — contradicting
the "expected: 0 results" instruction.

**Violation:** T3.6 SCAN-01 — grep command is file-wide but expected result states "0 results."
This creates an unresolvable ambiguity: a non-zero result may indicate either a pre-existing
`lock()` (acceptable per ticket qualification) or a newly introduced one (FAIL). An engineer
following the scan verbatim cannot distinguish these cases. The automated contract is broken.
JS-021 (P0 CRITICAL).

**Required:** Replace file-wide grep with a diff-scoped command, e.g.:
```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | `
    grep "^+" | grep -n "lock("
```
Expected: 0 results in added lines. This correctly scopes to new/modified code only.

**JS-021 (lock) — FAIL** (T3.6 SCAN-01 command vs expected result contradiction).

**JS-001 (throw new) — FAIL:**
T3.6 SCAN-02 issues a file-wide grep on `CopyEngine.cs`:
```powershell
grep -rn "throw new" src/PropTraderTools/CopyEngine.cs
```
Expected: "0 results in methods modified by this ticket." Same ambiguity as SCAN-01: any
pre-existing `throw new` in unmodified CopyEngine.cs methods would produce false-positive
results against the stated expectation of "0 results."

**Violation:** T3.6 SCAN-02 — grep command scope contradicts the expected-result scope.
JS-001 (P0 CRITICAL).

**JS-033 (async void) — FAIL:**
T3.6 SCAN-03 issues a file-wide grep on `CopyEngine.cs`:
```powershell
grep -rn "async void" src/PropTraderTools/CopyEngine.cs
```
Expected: "0 results in methods modified by this ticket." Same ambiguity.

**Violation:** T3.6 SCAN-03 — grep command scope contradicts the expected-result scope.
JS-033 (P0 CRITICAL).

**CYC (JS-066):** All five modified methods listed with targets <= 8. DispatchCopy explicitly
noted as <= 8 pre-patch + 2 gates = total <= 8. PASS.

**ASCII-only (JS-081):** T3.4 JS-DNA table states all methods are ASCII-only. No non-ASCII
claims in fix description. PASS (scan commands are absent for T3 but the issue is scoped under
the JS-021/022/023 scan defects above — this is a separate note, not an additional FAIL).

**JS Pre-Check: FAIL**
Violations: SCAN-01/02/03 all issue file-wide greps on CopyEngine.cs with expected-result
scopes of "0 in modified code only" — a contradiction an engineer cannot resolve automatically.
Cites JS-021 (P0), JS-001 (P0), JS-033 (P0).

---

### C — NT8 Constraints

**DispatchCopy gate placement (after Gate 0.5, before Gate 3):** YES — T3.3 explicitly states:
"after Gate 0.5 (exit signal name filter) and before Gate 3." Architecture plan Section A
confirms the same rationale. PASS.

**Flatten(Account leader, Instrument) overload NOT modified:** YES — T3.3 states: "The
`Flatten(Account leader, Instrument)` overload (used by panel direct calls) is NOT modified --
it was correct before this block." PASS.

**NT8 Constraints: PASS**

---

### D — Completeness

**Required structural elements per ticket:**

| Element | Present? |
|---------|----------|
| Header (T3.1) | YES |
| Problem statement (T3.2) | YES |
| Fix description (T3.3) | YES |
| Method signatures (T3.4) | YES |
| [Fact] test names (T3.5) | YES |
| 7-scan checklist (T3.6) | YES |
| Pass criteria (T3.7) | YES |
| ptt-sync requirement (T3.8) | YES |

**Minimum 9 [Fact] tests:** T3.5 lists 12 named test methods. PASS (12 >= 9).

**xUnit only:** All test entries in T3.5 use [Fact] notation. No [Test] or NUnit attributes. PASS.

**Completeness: PASS**

---

### E — Engineer Safety Guards

**Explicit "DO NOT re-implement" warning for reverted hotfixes:** YES — T3.1 Header:
"Reverted hotfixes: HOTFIX-B212-BEALL-LEADERSKIP-01, HOTFIX-B212-BREAKEVEN-INSTROVERLOAD-01
v1+v2 (do NOT re-implement)." T3.2 Problem Statement: "These approaches are REVERTED and
must NOT be re-implemented." T3.3 Fix Description: "MUST be reverted. Final state: no
masterAccount/leader conditional inside PttBeSwapAll.Execute." Explicit, prominent. PASS.

**Ph4b deletion audit scan command present:** YES — T3.6 SCAN-05 contains:
```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | `
    Select-String "^-    (private|internal|public)"
```
PASS.

**Single-hunk ban stated for CopyEngine.cs:** YES — T3.7 Pass Criteria contains the
single-hunk ban check:
```powershell
(git show HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count -gt 1
```
"This must be True (more than one hunk). A single-hunk result means a full-file replace was
committed = VERIFY_FAIL per AGENTS.md Platinum Standard." PASS.

**Engineer Safety Guards: PASS**

---

### F — Test Coverage

**REENTRY: test for both IsBeSwapInProgress and HasActivePttFlatten paths:** YES — T3.5
contains `DispatchCopy_BeSwapInProgress_ReturnsEarly` (Gate B212-REENTRY-V3) and
`DispatchCopy_ActivePttFlatten_ReturnsEarly` (Gate B212-REENTRY-FLATTEN). PASS.

**REENTRY: test for copier-OFF scenario (PttBeSwapAll revert — all accounts including leader
get BE swap):** YES — T3.5 contains `BeSwapAll_LeaderNotSkipped_AllAccountsSwapped` which
asserts "PttBeSwapAll.Execute does NOT skip leader (copier-OFF scenario -- revert confirmed)."
PASS.

**Test Coverage: PASS**

---

### T3 VERDICT: TICKET_REVIEW_FAIL

**Violations (3):**
1. **T3.6 SCAN-01 command vs expected-result scope contradiction** (JS-021 P0 CRITICAL):
   File-wide `grep -rn "lock(" CopyEngine.cs` will return pre-existing lock() calls if any
   exist in unmodified methods, contradicting "Expected: 0 results." Engineer cannot
   distinguish pre-existing violations from newly introduced ones. Requires diff-scoped command.
2. **T3.6 SCAN-02 command vs expected-result scope contradiction** (JS-001 P0 CRITICAL):
   File-wide `grep -rn "throw new" CopyEngine.cs` same ambiguity.
3. **T3.6 SCAN-03 command vs expected-result scope contradiction** (JS-033 P0 CRITICAL):
   File-wide `grep -rn "async void" CopyEngine.cs` same ambiguity.

---

---

## Overall Block Summary

### Violation Index

| Ticket | Section | Violation | Rule | Severity |
|--------|---------|-----------|------|----------|
| T1 | T1.6 SCAN-02 | Grep omits TradeCopierPanel.cs from throw new scan | JS-001 | P0 |
| T1 | T1.6 SCAN-03 | Grep omits TradeCopierPanel.cs from async void scan | JS-033 | P0 |
| T1 | T1.6 SCAN-05 | Grep omits TradeCopierPanel.cs and PttContracts.cs from ASCII scan | JS-081 | P1 |
| T2 | T2.5 | Missing test asserting stop.ocoId == target.ocoId (same non-empty string for OCO pair) | JS-052 / Test Coverage | P1 |
| T3 | T3.6 SCAN-01 | File-wide grep on CopyEngine.cs contradicts "0 results in modified code only" | JS-021 | P0 |
| T3 | T3.6 SCAN-02 | File-wide grep on CopyEngine.cs contradicts "0 results in modified methods only" | JS-001 | P0 |
| T3 | T3.6 SCAN-03 | File-wide grep on CopyEngine.cs contradicts "0 results in modified methods only" | JS-033 | P0 |

### Per-Ticket Result Summary

| Ticket | Traceability | JS Pre-Check | NT8 Check | Completeness | Engineer Safety | Test Coverage | Verdict |
|--------|-------------|-------------|-----------|-------------|----------------|---------------|---------|
| T1 | PASS | PASS | PASS | PASS | FAIL | PASS | TICKET_REVIEW_FAIL |
| T2 | PASS | PASS | PASS | PASS | PASS | FAIL | TICKET_REVIEW_FAIL |
| T3 | PASS | FAIL | PASS | PASS | PASS | PASS | TICKET_REVIEW_FAIL |

---

## Overall: TICKET_REVIEW_FAIL

All three tickets carry violations that must be corrected by the architect before the engineer
is spawned. The violations fall into two categories:

**Category 1 — Scan scope gaps (T1 SCAN-02/03/05, T3 SCAN-01/02/03):**
The 7-scan checklist commands are the engineer's contractual verification tool. Scan commands
that omit in-scope modified files (T1) or that issue file-wide greps with "expected 0 results"
instructions on a large file known to contain pre-existing pattern matches (T3) cannot serve
as verification contracts. An engineer following these scans exactly would either (a) miss
violations in omitted files or (b) receive non-zero results on T3 scans with no way to
distinguish pre-existing from newly introduced violations. Both outcomes break the 3-layer
defense (ticket contract → engineer attestation → verifier cross-check).

**Category 2 — Missing critical test (T2 T2.5):**
The OCO pair equality test (stop.ocoId == target.ocoId, same non-empty string) is the
structural precondition for the Working-state transition that is the entire purpose of
HOTFIX-B212-MA-STOP-02. This invariant is testable as pure string logic (no NT8 dependency)
and must have a named [Fact] in B212Tests.cs.

**Remediation instructions for architect (not for engineer):**

T1.6 SCAN-02: Add `src/PropTraderTools/TradeCopierPanel.cs` to the grep command.
T1.6 SCAN-03: Add `src/PropTraderTools/TradeCopierPanel.cs` to the grep command.
T1.6 SCAN-05: Add `src/PropTraderTools/TradeCopierPanel.cs` and
  `src/PropTraderTools/Core/PttContracts.cs` to the grep command.
T2.5: Add test `B212_STOP02_OcoId_MatchesBetweenStopAndTarget_InSamePair` asserting
  stop.ocoId == target.ocoId and both are non-empty when snapshotStop > 0.
T3.6 SCAN-01: Replace file-wide grep with:
  `git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | grep "^+" | grep -n "lock("`
  Expected: 0 results in added lines.
T3.6 SCAN-02: Replace file-wide grep with:
  `git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | grep "^+" | grep -n "throw new"`
  Expected: 0 results in added lines.
T3.6 SCAN-03: Replace file-wide grep with:
  `git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | grep "^+" | grep -n "async void"`
  Expected: 0 results in added lines.

---

*Review produced by ptt-ticket-reviewer (Phase 3.5). Send to ptt-architect for rewrite.
Do NOT spawn engineer until TICKET_REVIEW_PASS is issued.*

---

## Cycle 2 Review — B211+B212 Repair Batch
### Reviewer: PTT Ticket Reviewer (ptt-ticket-reviewer mode)
### Pipeline Phase: Phase 3.5 — Cycle 2
### Source: docs/brain/B212/04-tickets.md (revised post Cycle 1 TICKET_REVIEW_FAIL)

---

## Cycle 2 — V1 through V7 Resolution Confirmation

| Violation | Original Finding | Cycle 2 Status | Evidence (line ref in revised 04-tickets.md) |
|-----------|-----------------|----------------|----------------------------------------------|
| **V1 — T1 SCAN-02** | SCAN-02 omitted TradeCopierPanel.cs from throw new grep | **RESOLVED** | T1.6 SCAN-02 (line 176-182): `TradeCopierPanel.cs` added as third argument to grep command |
| **V2 — T1 SCAN-03** | SCAN-03 omitted TradeCopierPanel.cs from async void grep | **RESOLVED** | T1.6 SCAN-03 (line 184-191): `TradeCopierPanel.cs` added as third argument to grep command |
| **V3 — T1 SCAN-05** | SCAN-05 omitted TradeCopierPanel.cs and Core/PttContracts.cs from ASCII scan | **RESOLVED** | T1.6 SCAN-05 (line 208-213): Both `TradeCopierPanel.cs` and `Core/PttContracts.cs` added to grep command |
| **V4 — T2 missing [Fact]** | No test asserting stop.ocoId == target.ocoId (same non-empty string) for OCO pair | **RESOLVED** | T2.5 (line 404-405): `OcoId_StopAndTarget_ShareSameNonEmptyString` added — asserts stop.ocoId == target.ocoId and both non-empty for a given target slot |
| **V5 — T3 SCAN-01** | File-wide lock() grep on CopyEngine.cs contradicted "0 results" expected value | **RESOLVED** | T3.6 SCAN-01 (line 656-666): Replaced with `git diff HEAD~1 HEAD -- CopyEngine.cs | grep "^+" | grep "lock("` — expected "0 results in added lines" |
| **V6 — T3 SCAN-02** | File-wide throw new grep on CopyEngine.cs contradicted "0 results" expected value | **RESOLVED** | T3.6 SCAN-02 (line 668-675): Replaced with `git diff HEAD~1 HEAD -- CopyEngine.cs | grep "^+" | grep "throw new"` — expected "0 results in added lines" |
| **V7 — T3 SCAN-03** | File-wide async void grep on CopyEngine.cs contradicted "0 results" expected value | **RESOLVED** | T3.6 SCAN-03 (line 677-688): CopyEngine.cs now diff-scoped; PttBeSwapAll.cs uses file-wide grep (correct for small feature file) — expected "0 results in added lines" / "0 results" respectively |

**All 7 violations from Cycle 1 are confirmed resolved.**

---

## Cycle 2 — Full Checklist Re-run

### A — Traceability

| Ticket | Spec req IDs | Hotfix IDs | Files match hotfix log | Phantom work |
|--------|-------------|------------|----------------------|--------------|
| T1 | PASS — B211-ASK-01, MOVETARGET-01, ASKALL-01, BIDALL-01 (T1.1) | PASS — all 4 hotfixes mapped | PASS — TradeCopierPanel.cs, PttMoveTargetOne.cs, PttMoveTargetAll.cs, PttContracts.cs, TradeCopierAddOn.cs all in arch plan Sec 0 | PASS — no phantom work |
| T2 | PASS — B212-MA-STOP-02 (T2.1); STOP-01 correctly noted as superseded | PASS — HOTFIX-B212-MA-STOP-02 | PASS — PttMoveTargetOne.cs, B212Tests.cs | PASS |
| T3 | PASS — B212-FLATTEN-LEADERSKIP-01, B212-REENTRY-V3 (T3.1) | PASS — both hotfix IDs present; reverted hotfixes listed with "do NOT re-implement" | PASS — CopyEngine.cs, PttBeSwapAll.cs match arch plan Sec 0 | PASS |

**Section A: PASS (all tickets)**

---

### B — JS Pre-Check (7-scan checklist presence and content)

**T1:**
- SCAN-01 (lock()): Covers PttMoveTargetOne.cs, PttMoveTargetAll.cs, TradeCopierPanel.cs. 0 results expected. PASS.
- SCAN-02 (throw new): Covers all three in-scope files including TradeCopierPanel.cs (V1 fix). PASS.
- SCAN-03 (async void): Covers all three in-scope files including TradeCopierPanel.cs (V2 fix). PASS.
- SCAN-04 (CYC): All 7 methods listed; max CYC=7 (TryGetExitPrice); all <= 8. PASS.
- SCAN-05 (ASCII): Now covers all 4 in-scope files including TradeCopierPanel.cs and Core/PttContracts.cs (V3 fix). PASS.
- SCAN-06 (ptt-sync 21/0): Present. PASS.
- SCAN-07 (test baseline <= 24 failures): Present. PASS.
- All 7 scans present in T1. PASS.

**T2:**
- SCAN-01 (lock()): PttMoveTargetOne.cs. PASS.
- SCAN-02 (throw new): PttMoveTargetOne.cs. PASS.
- SCAN-03 (async void): PttMoveTargetOne.cs. PASS.
- SCAN-04 (CYC): Execute CYC <= 7 after OCO branch (+1 from T1 baseline). <= 8. PASS.
- SCAN-05: Repurposed as NT8-049 manual arg-position compliance check (ticket-specific substitution; T2 adds no new files beyond PttMoveTargetOne.cs already covered in SCAN-01/02/03). PASS.
- SCAN-06 (ptt-sync 21/0): Present. PASS.
- SCAN-07 (test baseline): Present. PASS.
- All 7 scans present in T2. PASS.

**T3:**
- SCAN-01 (lock()): Diff-scoped for CopyEngine.cs and PttBeSwapAll.cs (V5 fix). 0 results in added lines. PASS.
- SCAN-02 (throw new): Diff-scoped for CopyEngine.cs (V6 fix). 0 results in added lines. PASS.
- SCAN-03 (async void): Diff-scoped for CopyEngine.cs; file-wide for PttBeSwapAll.cs (V7 fix). PASS.
- SCAN-04 (CYC): DispatchCopy <= 8, Flatten(Instrument) <= 8, IsBeSwapInProgress <= 2, HasActivePttFlatten <= 5, PttBeSwapAll.Execute <= 5. All within ceiling. PASS.
- SCAN-05: Ph4b deletion audit — `git diff HEAD~1 HEAD -- CopyEngine.cs | Select-String "^-    (private|internal|public)"`. Authorizations documented. PASS.
- SCAN-06 (ptt-sync 21/0 + verify_critical_symbols.ps1): Present and includes guardian symbol check. PASS.
- SCAN-07 (test baseline): Present. PASS.
- All 7 scans present in T3. PASS.

**Section B: PASS (all tickets)**

---

### C — NT8 Constraints

**T1:**
- NT8-049 arg compliance documented (T1.4): arg6=limitPrice, arg7=stopPrice with concrete call examples. PASS.
- CollectWorkingTargets excludes StopMarket (OrderType.Limit filter stated in T1.3): "IsWorkingTarget requires OrderType.Limit. PTT-MA-Stop-* orders are StopMarket -- they are excluded." PASS.

**T2:**
- NT8-049 arg compliance: StopMarket arg6=0.0/arg7=snapshotStop; Limit arg6=exitPrice/arg7=0.0 — explicitly documented in T2.3 with code snippets and in T2.6 SCAN-05 manual check. PASS.
- CollectWorkingTargets excludes StopMarket: stated in T2.3 "IsWorkingTarget filter safety" paragraph. PASS.

**T3:**
- DispatchCopy gate placement (after Gate 0.5, before Gate 3): Explicitly stated in T3.3: "after Gate 0.5 (exit signal name filter) and before Gate 3." PASS.
- Flatten(Account leader, Instrument) overload NOT modified: T3.3 states "The Flatten(Account leader, Instrument) overload (used by panel direct calls) is NOT modified." PASS.

**Section C: PASS (all tickets)**

---

### D — Completeness

**T1:**
- All 8 required structural elements present (T1.1 through T1.8). PASS.
- Test count: 12 [Fact] tests in T1.5. >= 10 required. PASS.
- xUnit only: All entries use [Fact] notation. No [Test]/NUnit. PASS.

**T2:**
- All 8 required structural elements present (T2.1 through T2.8). PASS.
- Test count: 10 [Fact] tests in T2.5 (was 9 + `OcoId_StopAndTarget_ShareSameNonEmptyString` added per V4 fix). >= 5 required. PASS.
- xUnit only: All entries use [Fact] notation. PASS.

**T3:**
- All 8 required structural elements present (T3.1 through T3.8). PASS.
- Test count: 12 [Fact] tests in T3.5. >= 9 required. PASS.
- xUnit only: All entries use [Fact] notation. PASS.

**Section D: PASS (all tickets)**

---

### E — Engineer Safety Guards

**T1:**
- Two-pass count+fill / no List<T> rationale (CS0246 note): Explicitly in T1.3. PASS.
- No new safety guard gaps identified.

**T2:**
- snapshotStop==0 path (no-OCO path, target-only with string.Empty): Explicitly in T2.3 and asserted by `OcoId_Empty_WhenSnapshotStopZero` test. PASS.

**T3:**
- "DO NOT re-implement" warning for reverted hotfixes: Present in T3.1 header and T3.2 problem statement. PASS.
- Ph4b deletion audit scan command: Present in T3.6 SCAN-05. PASS.
- Single-hunk ban stated for CopyEngine.cs: Present in T3.7 pass criteria with PowerShell verification command. PASS.
- Commit size gate (> 300 changed lines requires Director approval): Present in T3.7. PASS.

**Section E: PASS (all tickets)**

---

### F — Test Coverage

**T1:**
- PTT-* and native Target[1-9] names both covered: `CollectWorkingTargets_BothPttAndNativeTargetsFound` asserts IsWorkingTarget returns true for "PTT-MA-Ask-1" AND "Target1"; `IsWorkingTarget_NativeTarget9_ReturnsTrue` covers Target9; `IsWorkingTarget_Target_NoDigitSuffix_ReturnsFalse` covers false-positive guard. PASS.

**T2:**
- OCO structural invariant (stop.ocoId == target.ocoId, non-empty): `OcoId_StopAndTarget_ShareSameNonEmptyString` added in T2.5 (V4 fix). Asserts both stop and target in the same pair carry the same non-empty ocoId — directly tests the structural precondition for Working-state transition. PASS.

**T3:**
- IsBeSwapInProgress gate path: `DispatchCopy_BeSwapInProgress_ReturnsEarly` covers Gate B212-REENTRY-V3. PASS.
- HasActivePttFlatten gate path: `DispatchCopy_ActivePttFlatten_ReturnsEarly` covers Gate B212-REENTRY-FLATTEN. PASS.
- copier-OFF scenario (PttBeSwapAll revert, all accounts including leader): `BeSwapAll_LeaderNotSkipped_AllAccountsSwapped` asserts leader not skipped. PASS.

**Section F: PASS (all tickets)**

---

## Cycle 2 — Per-Ticket Verdicts

### T1 — B211 Button Repairs

| Check | Result |
|-------|--------|
| Traceability | PASS |
| JS Pre-Check (SCAN-01..07 all present, all scoped correctly) | PASS |
| CYC Pre-Check (max CYC=7, all <= 8) | PASS |
| NT8 Check | PASS |
| Test Coverage (12 [Fact], both PTT-* and native Target[1-9] covered) | PASS |
| Scan Checklist (all 7 scans present and correctly scoped, V1/V2/V3 fixed) | PASS |
| File Routing (all .cs paths under src/PropTraderTools/) | PASS |
| **VERDICT** | **TICKET_REVIEW_PASS** |

### T2 — B212 MA-STOP-02 OCO Pair

| Check | Result |
|-------|--------|
| Traceability | PASS |
| JS Pre-Check (SCAN-01..07 all present) | PASS |
| CYC Pre-Check (Execute CYC <= 7) | PASS |
| NT8 Check (NT8-049 arg positions documented; StopMarket exclusion stated) | PASS |
| Test Coverage (10 [Fact]; OCO pair equality invariant covered by V4 fix) | PASS |
| Scan Checklist (all 7 scans present; SCAN-05 repurposed as NT8-049 manual check — acceptable) | PASS |
| File Routing (all .cs paths under src/PropTraderTools/) | PASS |
| **VERDICT** | **TICKET_REVIEW_PASS** |

### T3 — B212 Re-entry Suppression

| Check | Result |
|-------|--------|
| Traceability | PASS |
| JS Pre-Check (SCAN-01/02/03 now diff-scoped per V5/V6/V7 fixes; no ambiguity) | PASS |
| CYC Pre-Check (DispatchCopy <= 8, all others within targets) | PASS |
| NT8 Check (gate placement after Gate 0.5, before Gate 3; Flatten overload not modified) | PASS |
| Test Coverage (12 [Fact]; both REENTRY gates covered; copier-OFF revert scenario covered) | PASS |
| Scan Checklist (all 7 scans present; deletion audit in SCAN-05; single-hunk ban in T3.7) | PASS |
| File Routing (all .cs paths under src/PropTraderTools/) | PASS |
| **VERDICT** | **TICKET_REVIEW_PASS** |

---

## Cycle 2 — Block-Level Summary

### Violation Index (Cycle 2)

**No new violations identified.** All 7 Cycle 1 violations are confirmed resolved.

### Per-Ticket Result Summary (Cycle 2)

| Ticket | Traceability | JS Pre-Check | NT8 Check | Completeness | Engineer Safety | Test Coverage | Scan Checklist | Verdict |
|--------|-------------|-------------|-----------|-------------|----------------|---------------|----------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |

---

## Overall Cycle 2: TICKET_REVIEW_PASS

All three tickets pass all checks. The 7-scan defense-in-depth contract is intact across all three
tickets. The engineer may now be spawned against docs/brain/B212/04-tickets.md.

**Engineer entry point**: Read `docs/brain/B212/04-ticket-review.md` (this file) first, then
`docs/brain/B212/04-tickets.md`. Execute tickets T1 → T2 → T3 in order (T2 depends on T1 loop
structure; T3 is independent but should follow). Run all 7 scans per ticket before issuing
BUILD_PASS. Run `verify_critical_symbols.ps1` after any `CopyEngine.cs` edit.

*Cycle 2 review produced by ptt-ticket-reviewer (Phase 3.5). TICKET_REVIEW_PASS issued.
Safe to spawn engineer.*
