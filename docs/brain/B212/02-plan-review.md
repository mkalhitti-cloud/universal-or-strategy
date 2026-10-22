# B212 Plan Review
# Block: B211+B212 Repair Batch
# Reviewer: ptt-plan-reviewer
# Phase: 2 (Plan Review)
# Date: 2026
# Plan file: docs/brain/B212/02-architecture-plan.md

---

## Final Verdict: REVIEW_PASS

No P0 or P1 violations found. All checklist sections pass.

---

## Section A — Lane-Split Gate Compliance

**Result: PASS**

| Check | Status | Evidence |
|-------|--------|----------|
| LANE-SPLIT GATE RESULT declared | PASS | Plan line 12: "LANE-SPLIT GATE RESULT: SINGLE-PIPELINE" |
| Q1..Q4 answered with evidence | PASS | Plan lines 17-21 (table with source citations) |
| Gate decision rationale present | PASS | Plan lines 23-25 |
| Q1=NO -> no lanes mandated on Q1 grounds | PASS | Q1 answers "same method or within 50 lines?" = NO. Correct. |
| Q2=YES -> SINGLE-PIPELINE mandated | PASS | Q2 answers "Fix B depends on Fix A design?" = YES for B212-MA-STOP-02. Gate decision uses this to mandate SINGLE-PIPELINE. Correct. |
| Q3=NO for B212-MA-STOP-02 -> SINGLE-PIPELINE confirmed | PASS | Q3 = NO (standalone value only if B211 loop exists). Consistent with SINGLE-PIPELINE. |
| Q4=NO for B212-MA-STOP-02 -> SINGLE-PIPELINE confirmed | PASS | Q4 = NO (SIM path requires B211 infrastructure). Consistent with SINGLE-PIPELINE. |
| Gate result matches Q2/Q3/Q4 answers | PASS | SINGLE-PIPELINE is the correct conclusion. |

---

## Section B — Repair Coverage

**Result: PASS**

| Hotfix ID | Covered? | Ticket | Plan Location |
|-----------|----------|--------|---------------|
| HOTFIX-B211-ASK-01 | YES | T1 | Plan Section 1, Ticket 1, line 65 |
| HOTFIX-B211-MOVETARGET-01 | YES | T1 | Plan Section 1, Ticket 1, line 66 |
| HOTFIX-B211-ASKALL-01 | YES | T1 | Plan Section 1, Ticket 1, line 67 |
| HOTFIX-B211-BIDALL-01 | YES | T1 | Plan Section 1, Ticket 1, line 68 |
| HOTFIX-B212-MA-STOP-02 | YES | T2 | Plan Section 1, Ticket 2, line 84 |
| HOTFIX-B212-FLATTEN-LEADERSKIP-01 | YES | T3 | Plan Section 1, Ticket 3, line 101 |
| HOTFIX-B212-REENTRY-V3 | YES | T3 | Plan Section 1, Ticket 3, line 102 |

All 7 hotfixes covered. Each has ticket assignment. Grouping rationale present for all three tickets.

**Superseded/reverted hotfixes correctly excluded:**
- HOTFIX-B212-BEALL-LEADERSKIP-01: reverted, listed in plan's "not formalized" table (plan lines 108-111). ✅
- HOTFIX-B212-BREAKEVEN-INSTROVERLOAD-01 v1/v2: reverted. ✅
- HOTFIX-B212-DW06-PARTIAL: superseded. ✅
- HOTFIX-B212-MA-STOP-01: superseded by STOP-02. ✅

---

## Section C — Architectural Decision Quality

**Result: PASS**

| Decision | Required Check | Status | Plan Location |
|----------|---------------|--------|---------------|
| A: DispatchCopy as suppression layer | Rationale documented (orthogonal concerns) | PASS | Section 2, Decision A, lines 126-136 |
| B: IsBeSwapInProgress | Timing window documented (50-200ms); lock-free mechanism confirmed (ConcurrentDictionary TryGetValue) | PASS | Section 2, Decision B, lines 143-148 |
| C: HasActivePttFlatten | Order-state scan semantics correct vs flag-based; PartFilled inclusion justified | PASS | Section 2, Decision C, lines 156-161 |
| D: OCO pair pattern for MoveTarget | Mirrors PttBreakEvenSwap.SubmitSwapPair; per-slot ocoId generation documented | PASS | Section 2, Decision D, lines 168-178 |
| E: Flatten(Instrument) leader-skip unconditional | Justification present: leader's close already in-flight before RelayFlatten fires | PASS | Section 2, Decision E, lines 184-191 |
| F: Target name widening | PTT-* AND Target[1-9] coverage rationale; char.IsDigit guard explained | PASS | Section 2, Decision F, lines 196-202 |
| G: Two-pass count+fill array | CS0246 / no List<T> rationale; NT8 NinjaScript restriction documented | PASS | Section 2, Decision G, lines 207-212 |

---

## Section D — JS-DNA Compliance Matrix

**Result: PASS**

### JS-021 (No lock() — P0 CRITICAL)

| Ticket | Fix | Status | Mechanism |
|--------|-----|--------|-----------|
| T1 | All B211 button fixes | PASS | No lock(); ConcurrentDictionary reads only; Interlocked for _maSeq |
| T2 | HOTFIX-B212-MA-STOP-02 | PASS | Interlocked.Increment for _maSeq; NextBeOcoSeq uses Interlocked |
| T3 | HOTFIX-B212-FLATTEN-LEADERSKIP-01 | PASS | FindRule is ConcurrentDictionary lookup; LINQ Where is pure |
| T3 | HOTFIX-B212-REENTRY-V3 | PASS | IsBeSwapInProgress = ConcurrentDictionary TryGetValue; HasActivePttFlatten = acc.Orders.ToList() (lock-free NT8 snapshot) |

P0 summary table confirms: "JS-021: No lock() anywhere in new/modified code — PASS" (plan line 256).

### JS-001 (No throw new in hot paths — P0 CRITICAL)

| Ticket | Status | Notes |
|--------|--------|-------|
| T1 | PASS | try/catch log-and-swallow; no throw in Execute, CollectWorkingTargets, IsWorkingTarget, TryGetExitPrice |
| T2 | PASS | try/catch per pair submission; no throw in OCO pair loop |
| T3 | PASS | No throw in Flatten(Instrument) lambda or DispatchCopy gates |

### JS-033 (No async void non-event-handler — P0 CRITICAL)

| Ticket | Status | Notes |
|--------|--------|-------|
| T1 | PASS | OnAskAllClick and OnBidAllClick are sync void RoutedEventHandlers (not async void). Execute/CollectWorkingTargets are sync void. |
| T2 | PASS | Execute is sync void |
| T3 | PASS | Flatten(Instrument) and DispatchCopy are sync void |

### JS-066 (CYC <= 8 — P1 CRITICAL)

| Ticket | Method | CYC | Status |
|--------|--------|-----|--------|
| T1 | Execute (PttMoveTargetOne) | 6 | PASS |
| T1 | CollectWorkingTargets | 5 | PASS |
| T1 | IsWorkingTarget | 5 | PASS |
| T1 | TryGetExitPrice | 7 | PASS |
| T1 | OnAskAllClick | 4 | PASS |
| T1 | OnBidAllClick | 4 | PASS |
| T2 | Execute (with STOP-02 OCO branch) | 6 | PASS |
| T3 | Flatten(Instrument) | <=8 (plan lines 249) | PASS |
| T3 | DispatchCopy | <=8 (plan L2783 comment) | PASS |

### JS-081 (ASCII-only string literals — P1)

All tickets: PASS. Documented in compliance matrix (plan lines 234-251). All prefixes ("PTT-MA-", "PTT-MA-Stop-", "PTT-MA-Ask-", "PTT-MA-Bid-", ocoId components) are ASCII.

### NT8-specific violations

| Check | Status | Notes |
|-------|--------|-------|
| async/await in lifecycle methods | PASS | No async used anywhere |
| Account.All in constructor | PASS | Not present |
| sealed TradeCopierWindow | PASS | Not a new class |
| FontFamily override | PASS | No font changes |
| Hardcoded #RRGGBB hex | PASS | No hex colors |
| CreateOrder without PTT- prefix | PASS | All orders: PTT-MA-Ask-, PTT-MA-Bid-, PTT-MA-Stop- (Section 8) |
| DateTime.Now (not UtcNow) | PASS | No DateTime.Now in plan or hotfix source |

### Immutability / Construction violations

| Check | Status | Notes |
|-------|--------|-------|
| Dictionary<K,V> for shared collections | PASS | ConcurrentDictionary used throughout |
| Public constructor on singleton | PASS | PttMoveTargetOne is static (no instance, no constructor) |
| Mutable fields on struct | PASS | No new structs |

### NT8 API Surface (Section 8)

All APIs confirmed AddOnBase-available. Banned StrategyBase-only APIs confirmed absent:
- AtmStrategyCreate(): NOT used. PASS.
- AtmStrategyChangeStopTarget(): NOT used. PASS.

---

## Section E — Test Coverage

**Result: PASS**

| Ticket | Minimum Required | Planned | Test Framework | Status |
|--------|-----------------|---------|----------------|--------|
| T1 | 5 | 8 existing + 5 additional = 13 | xUnit [Fact] | PASS |
| T2 | 3 | 5 additional | xUnit [Fact] | PASS |
| T3 | 3 | 5 existing + 4 additional = 9 | xUnit [Fact] | PASS |

All [Fact] names listed for each ticket (plan Section 4).

xUnit-only confirmed: No [TestFixture], [TestMethod], NUnit, or MSTest references. PASS (JS-051).

**Advisory note (JS-063, P2 — not a REVIEW_FAIL trigger):**

The plan proposes one test that invokes a private static method via reflection:
- `B212_REENTRY_V3_IsBeSwapInProgress_NullAccName_ReturnsFalse` — "invoke via reflection with null arg" (plan line 321)

JS-063 (Test Public API Only, P2 MEDIUM) discourages testing private methods via `.GetMethod(...BindingFlags.NonPublic...).Invoke(...)`. This is P2 — not an auto-FAIL trigger in the DNA block. Cited for engineer awareness only.

**Recommendation to engineer (not a blocker):** Expose `IsBeSwapInProgress` as `internal` and use `[InternalsVisibleTo]` (JS-064) rather than reflection to satisfy both JS-063 and JS-064.

---

## Section F — Verification Requirements

**Result: PASS**

| Ticket | ptt-sync 21/0 | Guardian symbol check | SIM criteria |
|--------|--------------|----------------------|--------------|
| T1 | STATED (line 329) | STATED (line 331, sanity check if TradeCopierAddOn.cs modified) | STATED (lines 333-341, 6 scenarios) |
| T2 | STATED (line 348) | N/A (no CopyEngine.cs modification in T2) | STATED (lines 350-358, 5 scenarios + expected output) |
| T3 | STATED (line 374) | STATED (line 376, required) | STATED (lines 378-388, 4 scenarios + timing window confirmation) |

---

## Section G — Deferred Backlog

**Result: PASS**

Plan Section 6 (lines 392-418) provides a deferred backlog table equivalent to Section K format. Present and complete.

**Items CLOSED by this block (cross-checked against B211/06-deferred-backlog.md):**

| ID | Plan status | B211 backlog status | Match |
|----|-------------|---------------------|-------|
| DW-B211-SIM-01 | CLOSED (plan line 397) | OPEN in B211 backlog | Correctly closed by SIM execution |
| DW-B211-SIM-02 | CLOSED (plan line 398) | OPEN in B211 backlog | Correctly closed: race not observed in SIM |
| DW-B211-04 | CLOSED (plan line 407) | OPEN in B211 backlog (SIM-conditional) | Correctly closed: SIM condition not triggered |
| DW-B209-03 | CLOSED (plan line 399) | OPEN in B211 backlog | Correctly closed: combined SIM executed |

**Items carried forward (cross-checked against B211/06-deferred-backlog.md):**

| ID | B211 backlog status | Plan status |
|----|---------------------|-------------|
| DW-B211-01 | OPEN (P2) | OPEN (plan line 401) — correctly carried |
| DW-B211-02 | OPEN (P2) | OPEN (plan line 402) — correctly carried |
| DW-B209-01 | OPEN (P2) | OPEN (plan line 405) — correctly carried |
| DW-B209-02 | OPEN (P2) | OPEN (plan line 406) — correctly carried |
| DW-B209-04 | OPEN (P1) | OPEN (plan line 407) — correctly carried |

Note: DW-B211-04 is listed in the carried-forward table as "CLOSED: SIM Round confirmed race not observed" (plan line 407). The P1 OPEN item from B211 backlog is properly resolved with evidence. ✅

**New deferred items from B212 — appropriately scoped:**

| ID | Item | Priority | Scope justification |
|----|------|----------|---------------------|
| DW-B212-01 | Trim(Instrument) RelayTrim path leader-skip check | P1 | Newly discovered concern, not an existing spec requirement. Architecture comment at CopyEngine L3491 flags this for next block. Correct deferral. |
| DW-B212-02 | PttMoveTargetAll.cs future role clarification | P2 | Documentation item. Low risk. Appropriate deferral. |
| DW-B212-03 | DIFF LIMIT compliance audit (<10k chars) | P1 | Pre-push verification step. Appropriate deferral to PR phase. |

No deferred items found that should be in-scope for this pipeline.

---

## Violations Summary

| Rule ID | Severity | Description | Location in Plan | Disposition |
|---------|----------|-------------|------------------|-------------|
| JS-063 | P2 (MEDIUM) | `IsBeSwapInProgress_NullAccName_ReturnsFalse` test uses reflection to invoke private static (advisory only — P2 is not auto-FAIL) | Plan Section 4, Ticket 3 tests, line 321 | ADVISORY — not a REVIEW_FAIL trigger |

**P0 violations:** ZERO  
**P1 violations:** ZERO  
**P2 violations:** 1 (advisory, not a REVIEW_FAIL trigger)

---

## Spec Coverage Matrix

| Spec Requirement | Addressed? | Plan Section |
|-----------------|------------|--------------|
| HOTFIX-B211-ASK-01: widen target name filter to accept Target1..9 | YES | Section 1 T1, Decision F |
| HOTFIX-B211-MOVETARGET-01: multi-target batch cancel+resubmit | YES | Section 1 T1, Decision G |
| HOTFIX-B211-ASKALL-01: bypass AllAccounts/FindRule in OnAskAllClick | YES | Section 1 T1, Decision H |
| HOTFIX-B211-BIDALL-01: direct loop pattern for OnBidAllClick | YES | Section 1 T1, Decision H |
| HOTFIX-B212-MA-STOP-02: OCO pair per target slot (snapshotStop > 0 path) | YES | Section 1 T2, Decision D |
| HOTFIX-B212-FLATTEN-LEADERSKIP-01: leader-skip in Flatten(Instrument) | YES | Section 1 T3, Decision E |
| HOTFIX-B212-REENTRY-V3: IsBeSwapInProgress + HasActivePttFlatten gates in DispatchCopy | YES | Section 1 T3, Decisions A-C |
| HOTFIX-B212-MA-STOP-01 superseded by STOP-02 (not formalized) | YES | Section 1 T2 footnote, line 89 |
| HOTFIX-B212-BEALL-LEADERSKIP-01 reverted (not formalized) | YES | Section 1 T3 reverted table, lines 108-111 |
| ptt-sync 21/0 per ticket | YES | Section 5 |
| Guardian symbol check for CopyEngine.cs edits | YES | Section 5 T3 |
| SIM validation criteria per ticket | YES | Section 5 (3 subsections) |
| Deferred backlog documented | YES | Section 6 |

---

## FINAL VERDICT: REVIEW_PASS
