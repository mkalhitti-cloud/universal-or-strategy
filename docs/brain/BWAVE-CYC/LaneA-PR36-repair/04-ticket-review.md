# BWAVE-CYC Lane A PR #36 Repair -- Ticket Review

**Epic**: BWAVE-CYC Lane A Repair (PR #36 blockers)
**Date**: 2026-09-03
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Tickets reviewed**: A-1, A-2, A-3, A-4, A-5, A-6
**Plan reviewed**: 02-architecture-plan.md (status: REVIEW_PENDING)
**Tickets reviewed**: 04-tickets.md (status: TICKETS_COMPLETE)

---

## Known Baseline (Director-accepted)

| Item | Status |
|------|--------|
| NT8-runtime pre-existing test failures | 80 -- accepted by Director |
| 10k diff waiver | Approved for PR #36 |
| Greptile check | SUCCESS on PR #36 |
| CodeRabbit state | CHANGES_REQUESTED on PR #36 |

---

## TICKET A-1

**Title**: ASCII violation -- buffered button arrows (`\u25B2`/`\u25BC` → `"^"`/`"v"`)

| Check | Result | Notes |
|-------|--------|-------|
| Traceability | PASS | Maps to CodeRabbit CR36-3 + Greptile P2; plan section "TICKET A-1" fully matches. |
| old_text present and non-empty | PASS | 12 exact `Content = "\u25B2",` / `Content = "\u25BC",` pairs provided; representative verbatim csharp block included. |
| new_text present and non-empty | PASS | 12 exact `Content = "^",` / `Content = "v",` replacements specified. |
| Rationale cites correct rule | PASS | Cites "V12 DNA ASCII-Only Compliance (AGENTS.md §2)" -- correct authority. Does NOT incorrectly cite JS-006 (phantom types). |
| NT8 constraints field present | PASS | Present and specific: explains `Content` assignments are in a builder method called from Dispatcher.InvokeAsync-wrapped initialization. |
| xUnit [Fact] names listed | PASS | "None -- no test references these content strings directly." Acceptable nil answer. |
| All 7 scans present (SCAN-01..07) | PASS | SCAN-01 through SCAN-07 all present; each has documented expected result. |
| A-1 specific: Unicode escapes exact | PASS | `\u25B2` (BLACK UP-POINTING TRIANGLE) and `\u25BC` (BLACK DOWN-POINTING TRIANGLE) -- exact C# escape sequence form. |
| A-1 specific: Replacement chars correct | PASS | `"^"` and `"v"` -- pure ASCII, semantically correct directional indicators. |
| A-1 specific: Line numbers source-confirmed | PASS | Plan "Pre-Flight" section documents scan confirming lines 1147, 1153, 1184, 1190, 1226, 1232, 1265, 1271, 1311, 1317, 1350, 1356. |
| A-1 specific: Waiver lines protected | PASS | Range constraint 1130-1400 documented; Director-waiver lines 1781-3207 explicitly excluded. |
| File routing | PASS | `src/PropTraderTools/TradeCopierPanel.cs` -- correct Wave workspace path. |

**VERDICT**: TICKET_REVIEW_PASS

---

## TICKET A-2

**Title**: Remove misplaced TA-R9 test block from `BwaveCycTaR7HelperTests` (lines 7181-7395)

| Check | Result | Notes |
|-------|--------|-------|
| Traceability | PASS | Maps to CodeRabbit CR36-1 (CS0103 compile errors); plan section "TICKET A-2" fully matches. |
| old_text present and non-empty | PASS | Verbatim start marker (line 7181 TA-R9 header) and end marker (line 7395 closing brace) both documented with csharp blocks. Follow-on anchor line 7396 also specified. |
| new_text documented | PASS | "Empty -- entire block lines 7181-7395 is deleted." Correct representation of a pure deletion with empty replacement. |
| Rationale | PASS | CS0103 on `_engine` / `GetField` references; canonical tests reside in BwaveCycLaneAR9Tests.cs. |
| NT8 constraints field present | PASS | "None -- test/non-NT8 file." |
| xUnit [Fact] names listed | PASS | 8 removed test names enumerated; each noted as having canonical copy in BwaveCycLaneAR9Tests.cs. |
| All 7 scans present (SCAN-01..07) | PASS | All 7 present with expected results. |
| A-2 specific: outer class boundary safe | PASS | Ticket documents that `BwaveCycTaR7HelperTests` closing brace is at line 7396+ (TA-R10 comment block follows immediately), not inside the removal range. |
| A-2 specific: no overlap with A-3 | FAIL | SEE CRITICAL FINDING below. |
| File routing | PASS | `src/PropTraderTools/CopyEngineTests.cs` -- correct. |

### CRITICAL FINDING -- A-2 / A-3 OVERLAP VIOLATION

**Location**: A-2 ticket "xUnit [Fact] test names affected" vs. A-3 ticket "Instance 1"

**Violation**: A-3 Instance 1 targets `TryCancelOrders_ShouldNotThrow_WhenStaleListIsEmpty`
in `BwaveCycTaR7HelperTests` at `CopyEngineTests.cs` lines 7352-7361.
Line 7352 is inside the A-2 removal block (range 7181-7395 inclusive).

A-2 explicitly names this method in its own xUnit removed-list:
> `TryCancelOrders_ShouldNotThrow_WhenStaleListIsEmpty` (removed from CopyEngineTests.cs;
> canonical copy exists in BwaveCycLaneAR9Tests.cs as T_R9_09)

A-3 ticket Instance 1 states the method "shifts to approximately line 7156" after A-2 runs.
This is **factually wrong**: the method is DELETED by A-2, not shifted. It does not shift
because it is inside the removal range.

Consequence: After A-2 executes, A-3 Instance 1 finds no target. The engineer would either:
- Locate by method name and find nothing → silently skip Instance 1
- Cause a search_and_replace error (no match found)

The SCAN-07 verification for A-3 (checking for `TargetInvocationException` = 0 results) would
appear to pass only because A-2 deleted the containing method -- not because A-3 fixed the
vacuous assertion. The fix is NOT applied; the finding appears resolved only by deletion.

**Rule citation**: Traceability check failure (TICKET_REVIEW_FAIL): A-3 Instance 1 describes
work on a target that does not survive A-2. The plan (02-architecture-plan.md, "TICKET A-3"
section) acknowledges that after A-2 "line numbers shift" but also says to "locate by method
name" -- which would find zero results because the method is removed. The plan section
contradicts A-2 on the fate of this method.

**Required architect action**: Remove A-3 Instance 1 entirely (the method is deleted by A-2;
the vacuous assertion is already gone). Confirm that A-3 scope is Instance 2 only
(BwaveCycLaneAR9Tests.cs lines 146-154, which is unaffected by A-2).

**VERDICT**: TICKET_REVIEW_FAIL

---

## TICKET A-3

**Title**: Fix vacuous `Record.Exception` assertions (remove inner `try/catch(TargetInvocationException)`)

| Check | Result | Notes |
|-------|--------|-------|
| Traceability | PASS | Maps to CodeRabbit CR36-2 + Cubic confidence=10; plan section "TICKET A-3" matches. |
| old_text Instance 1 | FAIL | Target (`TryCancelOrders_ShouldNotThrow_WhenStaleListIsEmpty` at lines 7352-7361) is inside A-2 removal block 7181-7395. Target is deleted by A-2, not fixable by A-3. |
| new_text Instance 1 | FAIL | Corresponding replacement is moot; target does not survive A-2. |
| old_text Instance 2 | PASS | Verbatim `BwaveCycLaneAR9Tests.cs` lines 146-154 provided. |
| new_text Instance 2 | PASS | Correct replacement with inner try/catch removed; `Record.Exception` directly observes `mi.Invoke`. |
| Rationale | PASS | Correctly explains why inner try/catch makes Assert.Null(ex) vacuous; explains TryCancelOrders null-account+empty-list behavior. |
| NT8 constraints field present | PASS | "None -- test/non-NT8 file." |
| xUnit [Fact] names listed | PASS | Both affected test names listed. |
| All 7 scans present (SCAN-01..07) | PASS | All 7 present. |
| A-3 specific: two locations documented | FAIL | Instance 1 location is inside A-2 removal block -- phantom target. Only Instance 2 (BwaveCycLaneAR9Tests.cs) is a valid target. |
| A-3 specific: xUnit test structure integrity | PASS | Instance 2 new_text: `Record.Exception` lambda is still complete (expression-bodied lambda with single mi.Invoke call). |
| File routing | PASS | Both file paths correct. |

**VERDICT**: TICKET_REVIEW_FAIL
(A-3 Instance 1 is a phantom operation -- target removed by A-2. Architect must remove Instance 1 from A-3 scope. Only Instance 2 is valid.)

---

## TICKET A-4

**Title**: SA1507/SA1508 StyleCop violations -- CONFIRMED-NOOP

| Check | Result | Notes |
|-------|--------|-------|
| Traceability | PASS | Maps to CodeFactor FAILURE (original PR); plan section "TICKET A-4" matches. |
| old_text / new_text | PASS | Correctly documented as CONFIRMED-NOOP; N/A with explanation. |
| CONFIRMED-NOOP evidence | PASS | PowerShell SA1507/SA1508 scan script provided; expected output: both 0. Resolved by CSharpier commit 2270c544 documented. |
| Rationale | PASS | CodeFactor failure was against pre-format commit; current HEAD is clean. |
| NT8 constraints field present | PASS | "None -- test/non-NT8 file." |
| xUnit [Fact] names listed | PASS | "None." Acceptable nil answer for a no-op. |
| All 7 scans present (SCAN-01..07) | PASS | All 7 present; all marked as no-change baseline. |
| File routing | PASS | `src/PropTraderTools/CopyEngineTests.cs` -- correct. |

**VERDICT**: TICKET_REVIEW_PASS

---

## TICKET A-5

**Title**: Teal button background regression -- CONFIRMED-NOOP (`BuildArrowCluster` absent)

| Check | Result | Notes |
|-------|--------|-------|
| Traceability | PASS | Maps to Greptile P2 + Cubic confidence=10 + CodeRabbit CHANGES_REQUESTED; plan section "TICKET A-5" matches. |
| old_text / new_text | PASS | Correctly documented as CONFIRMED-NOOP; N/A with explanation. |
| CONFIRMED-NOOP evidence | PASS | PowerShell grep for `BuildArrowCluster` with expected 0 results. Per-button analysis documents correct state: teal buttons have no `Background` property set. |
| Rationale | PASS | Bug existed in `BuildArrowCluster`, which was eliminated when LaneC remediation replaced data-driven loop with inline `BuildBufferedButtonsRow`. |
| NT8 constraints field present | PASS | "None -- this is WPF UI code with no NT8 API call at issue." |
| xUnit [Fact] names listed | PASS | "None." Acceptable nil answer for a no-op. |
| All 7 scans present (SCAN-01..07) | PASS | All 7 present; all marked as no-change. |
| File routing | PASS | `src/PropTraderTools/TradeCopierPanel.cs` -- correct. |

**VERDICT**: TICKET_REVIEW_PASS

---

## TICKET A-6

**Title**: JS-002 -- Add `TryFindPositionForInstrument` (bool+out); update T_R9_10, T_R9_11

| Check | Result | Notes |
|-------|--------|-------|
| Traceability | PASS | Maps to Greptile P0 (JS-002 violation); plan section "TICKET A-6" matches. |
| old_text CopyEngine.cs | PASS | Anchor context (lines 1131-1133: `return null; }` then TB-T3 comment) provided as verbatim insertion anchor. Valid apply_diff pattern for method insertion. |
| new_text CopyEngine.cs | PASS | Complete method body provided verbatim with inline compliance annotations. |
| old_text T_R9_10 | PASS | Exact lines 159-166 of BwaveCycLaneAR9Tests.cs provided verbatim. |
| new_text T_R9_10 | PASS | Updated method name, lookup string, added ReturnType assertion, parameter count 2→3. |
| old_text T_R9_11 | PASS | Exact lines 168-176 of BwaveCycLaneAR9Tests.cs provided verbatim. |
| new_text T_R9_11 | PASS | Updated method name, lookup string, added parms[2].Name and parms[2].IsOut assertions. |
| A-6 specific: return type is bool | PASS | `private static bool TryFindPositionForInstrument(...)` -- return type is bool. |
| A-6 specific: no return null | PASS | Method body has no `return null`. `pos = null` is an `out` parameter initialization before `return false`. JS-002 compliant. |
| A-6 specific: null path = pos=null; return false | PASS | Early return path: `pos = null; if (...) return false;` -- correct. |
| A-6 specific: 3-param bool+out signature | PASS | `(Account acc, NinjaTrader.Cbi.Instrument instr, out NinjaTrader.Cbi.Position pos)` -- 3 params, bool return, out param. |
| A-6 specific: CopyEngineTests.cs callers handled | PASS | Ticket correctly notes lines 7364-7395 (within A-2 removal block) handle all CopyEngineTests.cs references. No further update to CopyEngineTests.cs required. |
| A-6 specific: no production caller update needed | PASS | Plan documents that original caller in SubmitBeStop was removed along with the method; no caller exists in current HEAD. Verified in plan "Cross-Cutting Concerns" section. |
| Rationale cites correct rule | PASS | Cites JS-002 explicitly with correct description. |
| NT8 constraints field present | PASS | `acc.Positions` confirmed as AddOnBase-available read-only collection. No CreateOrder/AtmStrategy involvement. |
| xUnit [Fact] names listed | PASS | T_R9_10 renamed, T_R9_11 renamed -- both documented. |
| All 7 scans present (SCAN-01..07) | PASS | All 7 present. SCAN-03 correctly addresses `pos = null` distinction. |
| File routing | PASS | `src/PropTraderTools/CopyEngine.cs` and `src/PropTraderTools/Tests/BwaveCycLaneAR9Tests.cs` -- both correct Wave workspace paths. |

**VERDICT**: TICKET_REVIEW_PASS

---

## JS Pre-Check (All Tickets)

| Scan | Result | Notes |
|------|--------|-------|
| No lock() introduced | PASS | No ticket describes adding `lock()`. A-6 new method explicitly annotated JS-021 compliant; acc.Positions is NT8 read-only. |
| No async void introduced | PASS | No ticket describes adding `async void`. A-6 method is `private static bool` (synchronous). |
| A-6 no return null | PASS | `TryFindPositionForInstrument` uses bool+out pattern. No `return null` in method body. `pos = null` is out-param initialization, not a null return. |
| No Dictionary<K,V> on shared state | PASS | No ticket introduces new Dictionary fields. |
| No SolidColorBrush without Freeze | PASS | A-1 only modifies string Content properties; no brush created. |

---

## CYC Pre-Check (All Tickets)

| Ticket | Method | Estimated CYC | Within Limit? |
|--------|--------|---------------|---------------|
| A-6 | `TryFindPositionForInstrument` | 3 (base=1, null-guard branch=1, foreach=1) | PASS (≤8) |
| A-1 | `BuildBufferedButtonsRow` (string replacements only) | No new branches added | PASS |
| A-2, A-3, A-4, A-5 | Removals / NOOPs / test body simplifications | CYC reduced or unchanged | PASS |

---

## NT8 Constraints Check (All Tickets)

| Ticket | NT8 API concern | Result |
|--------|----------------|--------|
| A-1 | Content property on RepeatButton -- WPF property, builder method called from Dispatcher.InvokeAsync context | PASS |
| A-2, A-3, A-4 | Test files only -- no NT8 API involvement | PASS |
| A-5 | WPF UI code -- BuildArrowCluster absent; no NT8 API at issue | PASS |
| A-6 (CopyEngine.cs) | `acc.Positions` -- NT8 `Account.Positions` (AccountPositionCollection), AddOnBase-available, read-only enumeration. No Submit/Cancel/CreateOrder. No AtmStrategy API. | PASS |
| A-6 (test file) | Reflection-based test; no NT8 API called from test code | PASS |
| Global | No ticket introduces sealed on TradeCopierWindow, FontFamily, hardcoded hex color, DateTime.Now, CreateOrder without "PTT-" prefix, async/await in lifecycle method, or Account.All outside Loaded | PASS |

---

## Spec Coverage Summary

| Finding | Ticket | Coverage |
|---------|--------|---------|
| CodeRabbit CR36-1 (CS0103 compile errors) | A-2 | Covered |
| CodeRabbit CR36-2 (vacuous Record.Exception) | A-3 | Covered (Instance 2 only after FAIL resolution) |
| CodeRabbit CR36-3 (ASCII Unicode arrows) | A-1 | Covered |
| Greptile P0 (JS-002 null return) | A-6 | Covered |
| Greptile P2 (Unicode in string literals) | A-1 | Covered |
| Greptile P2 (teal button background) | A-5 | Covered (NOOP) |
| CodeFactor SA1507/SA1508 | A-4 | Covered (NOOP) |

All spec findings covered. No phantom work (all tickets trace to a finding). No uncovered finding.

---

## Overall: TICKET_REVIEW_FAIL

**Failing tickets**: A-2 (FAIL), A-3 (FAIL)

### Violations

**VIOLATION 1 -- A-2 / A-3 Overlap: A-3 Instance 1 target is inside A-2 removal block**

- **A-2 ticket**, section "xUnit [Fact] test names affected": explicitly names
  `TryCancelOrders_ShouldNotThrow_WhenStaleListIsEmpty` as removed from `CopyEngineTests.cs`.
- **A-3 ticket**, section "A-3 Instance 1": targets the same method at lines 7352-7361 and
  states it "shifts to approximately line 7156" after A-2 runs.
- Line 7352 is inside the A-2 removal range 7181-7395. The method is deleted by A-2, not shifted.
- A-3 Instance 1 describes work on a non-existent post-A-2 target.
- Result: A-3 Instance 1 is a phantom operation. Engineer would silently skip it or error.
- The SCAN-07 verification (TargetInvocationException = 0) would pass only because A-2 deleted
  the method -- not because A-3 corrected the vacuous assertion.
- **Rule**: Traceability failure + Content Completeness failure on A-3 Instance 1.
  A-2 Traceability check also fails on the overlap-awareness item.

### Required Architect Fixes

1. **A-3**: Remove Instance 1 entirely. The vacuous assertion in `CopyEngineTests.cs`
   `TryCancelOrders_ShouldNotThrow_WhenStaleListIsEmpty` is resolved by A-2's deletion,
   not by A-3. A-3 scope becomes Instance 2 only (`BwaveCycLaneAR9Tests.cs` lines 146-154).
   Update A-3 header to reflect single-instance scope. Remove the "two separate locations"
   claim from the checklist item.

2. **A-2**: Add an explicit note: "A-2 removal also eliminates the vacuous assertion in
   `TryCancelOrders_ShouldNotThrow_WhenStaleListIsEmpty` (formerly A-3 Instance 1). No
   separate A-3 action is required for CopyEngineTests.cs."

3. **Verify**: After the above changes, confirm the mandatory execution order A-1→A-2→A-3
   section still makes sense (A-3 now only touches BwaveCycLaneAR9Tests.cs, which is
   independent of A-2 scope).

### Tickets that PASS (unblocked)

A-1, A-4, A-5, A-6 are clean and may proceed as written once architect delivers
corrected A-2 and A-3 tickets.

---

*Reviewer: ptt-ticket-reviewer | Phase 3.5 gate | 2026-09-03*

---

## Cycle 2 Review

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-09-03
**Trigger**: Architect fix applied -- A-3 Instance 1 (phantom target) removed; A-2 CR36-2 partial-resolution note added.

---

### Cycle 1 Violation -- Resolution Confirmation

**VIOLATION 1 (Cycle 1)**: A-3 Instance 1 described work on `TryCancelOrders_ShouldNotThrow_WhenStaleListIsEmpty` at `CopyEngineTests.cs` lines 7352-7361 -- inside A-2 removal block 7181-7395. Phantom target.

**Required architect actions from Cycle 1**:
1. Remove A-3 Instance 1 entirely. ✅ DONE -- A-3 now has a single target only.
2. Add explicit CR36-2 partial-resolution note to A-2. ✅ DONE -- A-2 spec requirement IDs now include "CodeRabbit CR36-2 (partial)" and a full explanatory paragraph.
3. Verify A-1→A-2→A-3 execution order still makes sense. ✅ DONE -- A-3 now targets only `BwaveCycLaneAR9Tests.cs`, independent of A-2's scope.

---

### Cycle 2 -- Per-Ticket Re-Check

#### TICKET A-1 (re-check)

| Check | Result | Notes |
|-------|--------|-------|
| Previously passed -- unchanged by fix | PASS | A-1 was TICKET_REVIEW_PASS in Cycle 1 and is unmodified in this revision. |
| old_text/new_text (12 pairs) | PASS | `\u25B2` → `"^"`, `\u25BC` → `"v"` at lines 1147-1356; verbatim blocks present. |
| V12 DNA ASCII mandate cited | PASS | AGENTS.md §2 cited correctly. No JS-006 phantom citation. |
| 7 scans (SCAN-01..07) | PASS | All present with documented expected results. |

**VERDICT**: TICKET_REVIEW_PASS (unchanged from Cycle 1)

---

#### TICKET A-2 (re-check)

| Check | Result | Notes |
|-------|--------|-------|
| Block delete boundaries 7181-7395 | PASS | Unchanged. |
| Outer-class brace safety | PASS | TA-R10 comment block at line 7396 (new 7181 after deletion) -- outer class closing brace not inside removal range. |
| CR36-2 partial resolution noted | PASS | Spec requirement IDs now include CR36-2 (partial). Body paragraph explicitly names `TryCancelOrders_ShouldNotThrow_WhenStaleListIsEmpty` as the eliminated instance and directs to A-3 for the remaining instance. |
| No overlap with A-3 | PASS | A-2 targets `CopyEngineTests.cs` 7181-7395. A-3 targets `BwaveCycLaneAR9Tests.cs` 146-154. Different files; zero overlap. |
| 7 scans (SCAN-01..07) | PASS | All present. |
| File routing | PASS | `src/PropTraderTools/CopyEngineTests.cs` -- correct Wave workspace path. |

**VERDICT**: TICKET_REVIEW_PASS (previously FAIL due to overlap issue -- now resolved)

---

#### TICKET A-3 (re-check)

| Check | Result | Notes |
|-------|--------|-------|
| Instance count = 1 only | PASS | A-3 now has exactly one target: `BwaveCycLaneAR9Tests.cs` lines 146-154. No "Instance 1" section exists. |
| No reference to CopyEngineTests.cs line 7352 | PASS | The only CopyEngineTests.cs reference is in the "Note on scope" paragraph, which correctly states that instance is eliminated by A-2. Not presented as a target for A-3. |
| old_text (BwaveCycLaneAR9Tests.cs 146-154) | PASS | Verbatim inner `try { mi.Invoke(...) } catch (TargetInvocationException) {}` block inside `Record.Exception` lambda -- exact match. |
| new_text | PASS | Inner try/catch removed; `Record.Exception` directly observes `mi.Invoke` call. Expression-bodied lambda is structurally valid C#. |
| Note on scope describes A-2 partial resolution correctly | PASS | "The instance in `CopyEngineTests.cs` … is eliminated by Ticket A-2 (it falls within the A-2 removal block lines 7181-7395). A-3 addresses only the remaining instance in `BwaveCycLaneAR9Tests.cs`." -- accurate and non-contradictory. |
| xUnit [Fact] name listed | PASS | `T_R9_09_TryCancelOrders_EmptyList_DoesNotThrow` documented as body-rewritten; still present in BwaveCycLaneAR9Tests.cs. |
| Rationale completeness | PASS | Explains vacuous assertion mechanism; explains TryCancelOrders null-account+empty-list behavior (no `acc.Cancel` call fired). |
| JS constraints (A-3) | PASS | JS-021: no lock; JS-001: no throw; JS-033: no async void. All annotated COMPLIANT. |
| NT8 constraints | PASS | "None -- test/non-NT8 file." |
| 7 scans (SCAN-01..07) | PASS | All present. SCAN-07 verification command for `TargetInvocationException` = 0 results is correct post-A-2+A-3. |
| File routing | PASS | `src/PropTraderTools/Tests/BwaveCycLaneAR9Tests.cs` -- correct Wave workspace path. |

**VERDICT**: TICKET_REVIEW_PASS (previously FAIL due to phantom Instance 1 -- now resolved)

---

#### TICKET A-4 (re-check)

| Check | Result | Notes |
|-------|--------|-------|
| Previously passed -- unchanged by fix | PASS | Unmodified in this revision. CONFIRMED-NOOP with SA1507/SA1508 scan script; commit 2270c544 evidence. |
| 7 scans (SCAN-01..07) | PASS | All present. |

**VERDICT**: TICKET_REVIEW_PASS (unchanged from Cycle 1)

---

#### TICKET A-5 (re-check)

| Check | Result | Notes |
|-------|--------|-------|
| Previously passed -- unchanged by fix | PASS | Unmodified in this revision. CONFIRMED-NOOP with BuildArrowCluster grep evidence and per-button teal Background analysis. |
| 7 scans (SCAN-01..07) | PASS | All present. |

**VERDICT**: TICKET_REVIEW_PASS (unchanged from Cycle 1)

---

#### TICKET A-6 (re-check)

| Check | Result | Notes |
|-------|--------|-------|
| Previously passed -- unchanged by fix | PASS | Unmodified in this revision. |
| bool+out signature | PASS | `private static bool TryFindPositionForInstrument(Account acc, NinjaTrader.Cbi.Instrument instr, out NinjaTrader.Cbi.Position pos)` |
| JS-002 compliant | PASS | No `return null` in method body. `pos = null` is out-param initialization before `return false`. |
| JS-021 compliant | PASS | `acc.Positions` is NT8 read-only collection; no lock. |
| JS-001 compliant | PASS | No throw in new method. |
| JS-033 compliant | PASS | `private static bool` -- synchronous. |
| CYC = 3 | PASS | base(1) + null-guard branch(1) + foreach(1) = 3. Within CYC <= 8 mandate. |
| Callers updated | PASS | CopyEngineTests.cs callers at 7364-7395 inside A-2 removal block (handled by A-2). No production caller in HEAD. Both cases documented. |
| T_R9_10 and T_R9_11 update texts correct | PASS | Method rename, ReturnType assertion added, parameter count 2→3, parms[2].Name, parms[2].IsOut -- all present and correct. |
| 7 scans (SCAN-01..07) | PASS | All present; SCAN-03 correctly distinguishes `pos = null` from `return null`. |

**VERDICT**: TICKET_REVIEW_PASS (unchanged from Cycle 1)

---

### Cycle 2 -- Global Checks

| Global Check | Result | Notes |
|---|---|---|
| Phantom A-3 violation resolved | PASS | A-3 Instance 1 removed; A-2 CR36-2 partial-resolution note added; no overlap. |
| JS Pre-Check: no lock() | PASS | No ticket introduces lock(). A-6 explicitly annotates JS-021 compliant. |
| JS Pre-Check: no async void | PASS | No ticket introduces async void. A-6 method is synchronous. |
| JS Pre-Check: no return null in A-6 | PASS | TryFindPositionForInstrument has no `return null`. |
| JS Pre-Check: no Dictionary<K,V> on shared state | PASS | No new Dictionary fields in any ticket. |
| JS Pre-Check: no SolidColorBrush without Freeze | PASS | A-1 modifies Content strings only; no brush created. |
| CYC Pre-Check: A-6 CYC <= 8 | PASS | CYC = 3. |
| All 6 tickets have SCAN-01..07 | PASS | A-1: ✅ A-2: ✅ A-3: ✅ A-4: ✅ A-5: ✅ A-6: ✅ |
| Spec coverage complete | PASS | CR36-1 (A-2), CR36-2 (A-2 partial + A-3), CR36-3 (A-1), Greptile P0 (A-6), Greptile P2 x2 (A-1, A-5), CodeFactor SA1507/SA1508 (A-4). All findings covered; no phantom work. |
| Mandatory execution order preserved | PASS | A-1→A-2→A-3→A-4→A-5→A-6; A-3 now only touches BwaveCycLaneAR9Tests.cs (independent of A-2 scope -- order still meaningful due to A-6 needing A-2 to remove conflicting CopyEngineTests references). |
| File routing: all .cs paths in Wave workspace | PASS | All paths under `src/PropTraderTools/` in `c:\WSGTA\universal-or-strategy`. |
| NT8 constraints: no sealed on TradeCopierWindow | PASS | Not described in any ticket. |
| NT8 constraints: no FontFamily set | PASS | Not described in any ticket. |
| NT8 constraints: no hardcoded hex color | PASS | Not described in any ticket. |
| NT8 constraints: no DateTime.Now | PASS | Not described in any ticket. |
| NT8 constraints: no async/await in lifecycle | PASS | Not described in any ticket. |
| NT8 constraints: no Account.All outside Loaded | PASS | Not described in any ticket. |
| NT8 constraints: no CreateOrder without PTT- prefix | PASS | Not described in any ticket. |
| 10k diff waiver | Accepted by Director (baseline). |
| 80 pre-existing NT8-runtime failures | Accepted by Director (baseline). |

---

### Cycle 2 -- Final Verdict

**All Cycle 1 violations resolved. No new violations found.**

| Ticket | Cycle 1 | Cycle 2 |
|--------|---------|---------|
| A-1 | PASS | PASS |
| A-2 | FAIL | PASS |
| A-3 | FAIL | PASS |
| A-4 | PASS | PASS |
| A-5 | PASS | PASS |
| A-6 | PASS | PASS |

## Overall (Cycle 2): TICKET_REVIEW_PASS

*Reviewer: ptt-ticket-reviewer | Phase 3.5 gate | Cycle 2 | 2026-09-03*
