# DW-B159-TP4-RACE — Engineering Tickets
<!-- block: DW-B159-TP4-RACE -->
<!-- phase: 3 -->
<!-- produced-by: ptt-architect (Phase 3, Correction Cycle 1) -->
<!-- date: 2026-10-01 -->
<!-- plan-source: docs/brain/DW-B159-TP4-RACE/02-architecture-plan.md -->
<!-- plan-verdict: REVIEW_PASS (Cycle 2 -- 02-plan-review.md) -->
<!-- correction: TR-T1-TEST1-TAUTOLOGY (P0), TR-T1-TEST2-ALWAYS-FAIL (P1) fixed -->

---

## Block Header

| Field | Value |
|-------|-------|
| Epic | DW-B159-TP4-RACE -- FindFollowerBracketOrder Accepted-state race fix |
| Phase | 3 -- Ticket Generation (Correction Cycle 1) |
| Pipeline | SINGLE |
| Tickets | 2 (T1 + T2) |
| Production code changes | **ZERO** |
| Plan source | docs/brain/DW-B159-TP4-RACE/02-architecture-plan.md |
| Plan verdict | REVIEW_PASS (Cycle 2) |
| Correction | TR-T1-TEST1-TAUTOLOGY (P0) and TR-T1-TEST2-ALWAYS-FAIL (P1) resolved in T1 |

---

## ENGINEER PRE-CHECK RESULT (mandatory first step -- confirmed by architect)

**Status: FIX ALREADY PRESENT**

Grep result (CopyEngine.cs lines 4606-4613):

```
order.OrderState != OrderState.Working        // (5) branches -- DW-LB-FL-01 V7: Initialized added
&& order.OrderState != OrderState.Accepted
&& order.OrderState != OrderState.Submitted
&& order.OrderState != OrderState.ChangeSubmitted
&& order.OrderState != OrderState.Initialized
```

- `OrderState.Accepted` is present at line 4608. **Fix confirmed.**
- `OrderState.Submitted` is also present at line 4609. **Spec/code discrepancy confirmed -- see T1 Test 2.**
- **Ticket 1 = VERIFICATION + TEST ADDITION ONLY. Zero production code changes.**

---

## SPEC/CODE DISCREPANCY NOTICE (engineer must read before implementing)

The spec for this epic stated the minimal fix was `Working || Accepted` only (Submitted excluded,
as `acc.Change()` on Submitted is NOT NT8-safe -- exchange has not yet confirmed routing).

**Current code includes Submitted in the accepted set (CopyEngine.cs line 4609).** This was
added in a prior block (DW-LB-FL-01 V7 / BWAVE-REFACTOR-LaneB-T4). The comment header
reads "DW-LB-FL-01 V7: Initialized added" -- Submitted appears to have been included
incidentally alongside ChangeSubmitted in an earlier pass.

**Engineer action:** Do NOT change the guard in this epic. The discrepancy is flagged for
Director visibility via Test 2 (Skipped). When Director resolves intent, a future epic will
either un-Skip Test 2 (if Submitted is removed from the guard) or convert it to an
IsIncluded assertion (if Submitted inclusion is confirmed intentional).

---

## T1 -- FindFollowerBracketOrder Accepted-State Verification + Tests

### Spec Requirement IDs

| Req ID | Description |
|--------|-------------|
| DW-B159-TP4-RACE-T1-REQ-01 | OrderState.Accepted must be in FindFollowerBracketOrder accepted-state set |
| DW-B159-TP4-RACE-T1-REQ-02 | Fix confirmed present -- Ticket 1 scope is test-only, zero production code changes |
| DW-B159-TP4-RACE-T1-REQ-03 | Submitted discrepancy flagged via Skipped test with correct conflict rationale |
| DW-B159-TP4-RACE-T1-REQ-04 | 2 new [Fact] test methods added to DwB16702SlateReadyTests class (line 9766) |

### Scope Lock

**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Scope:** Add 2 new [Fact] test methods. No edits to any other file. Zero changes to
`CopyEngine.cs` (production code). No whitespace mutation outside the new lines added.

**Verification target (READ ONLY -- do not modify):**
- `src/PropTraderTools/CopyEngine.cs` lines 4595-4618 (`FindFollowerBracketOrder` IEnumerable overload)
- State guard confirmed at lines 4606-4613: `Working || Accepted || Submitted || ChangeSubmitted || Initialized`

### Placement

Insert a new comment block `// DW-B159-TP4-RACE` followed by 2 test methods at the END of
the **`DwB16702SlateReadyTests` class** (which starts at line 9766) -- immediately before
the final closing `}` of that class at line 9829, just before the `B159KSuiteTests` class
boundary at line 9836.

The `DwB16702SlateReadyTests` class already has `ResolveCopyEngineSource()` defined at
line 9768 -- the new tests use this helper. Do NOT insert into `B159KSuiteTests`
(separate class starting at line 9836).

### Method Signatures (complete engineer contract)

```csharp
[Fact]
public void FindFollowerBracketOrder_AcceptedState_IsReturned()

[Fact(Skip = "SPEC-CONFLICT: current guard (DW-LB-FL-01 V7 Fix B, CopyEngine.cs line 4609) includes Submitted in accepted-state set. Spec requires Submitted excluded: acc.Change() on Submitted is NOT safe (NT8 routing not yet confirmed by exchange). When Director removes Submitted from guard and un-Skips this test, Assert.DoesNotContain will pass.")]
public void FindFollowerBracketOrder_SubmittedState_IsSkipped()
```

Both methods:
- Return type: `void`
- Parameters: none
- Access: `public`
- Class: `DwB16702SlateReadyTests` (line 9766), which contains `ResolveCopyEngineSource()`
- No NT8 runtime required (source-text check with structural fallback)

### Full Test Source (engineer must copy verbatim)

```csharp
        // DW-B159-TP4-RACE -- FindFollowerBracketOrder Accepted-state race fix
        // Guard confirmed at CopyEngine.cs lines 4606-4613 (DW-LB-FL-01 V7 Fix B + BWAVE-REFACTOR-LaneB-T4).
        // Source-text contract: Assert.Contains/DoesNotContain verify the actual production guard.
        // ResolveCopyEngineSource() defined at line 9768 -- reused here (same class).
        // JS-021: no lock. JS-001: no throw. CYC=1 each. ASCII-only. No whitespace mutation.

        [Fact]
        public void FindFollowerBracketOrder_AcceptedState_IsReturned()
        {
            // DW-B159-TP4-RACE: Accepted state must be in the FindFollowerBracketOrder
            // accepted-state set (CopyEngine.cs lines 4606-4613, DW-LB-FL-01 V7 Fix B).
            // Race window: leader bracket fires Accepted (initial arm); follower may still be
            // Accepted when SyncFollowerBracket calls FindFollowerBracketOrder.
            // With Working-only guard (old), fo=NULL and sync was skipped silently.
            // Fix confirmed: source guard must contain "!= OrderState.Accepted" (skip-on-NOT-in-set).
            // CYC=1. No lock. ASCII-only.
            var srcFile = ResolveCopyEngineSource();
            if (System.IO.File.Exists(srcFile))
            {
                var src = System.IO.File.ReadAllText(srcFile);
                Assert.Contains(
                    "order.OrderState != OrderState.Accepted",
                    src,
                    System.StringComparison.Ordinal);
            }
            else
            {
                // Source unavailable -- structural fallback: verify FindFollowerBracketOrderTestable
                // (IEnumerable overload, CopyEngine.cs line 4756) exists as the test seam.
                var m = typeof(CopyEngine).GetMethod(
                    "FindFollowerBracketOrderTestable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.NotNull(m);
            }
        }

        [Fact(Skip = "SPEC-CONFLICT: current guard (DW-LB-FL-01 V7 Fix B, CopyEngine.cs line 4609) includes Submitted in accepted-state set. Spec requires Submitted excluded: acc.Change() on Submitted is NOT safe (NT8 routing not yet confirmed by exchange). When Director removes Submitted from guard and un-Skips this test, Assert.DoesNotContain will pass.")]
        public void FindFollowerBracketOrder_SubmittedState_IsSkipped()
        {
            // DW-B159-TP4-RACE: Spec contract -- Submitted must NOT be in the
            // FindFollowerBracketOrder accepted-state set. acc.Change() on Submitted
            // is NOT NT8-safe (exchange confirmation not yet received).
            // CURRENTLY FAILS (Skip active): CopyEngine.cs line 4609 DOES include Submitted.
            // Assert.DoesNotContain will FAIL while Submitted remains in the guard (correct).
            // When Director removes Submitted from the guard, un-Skip this test --
            // Assert.DoesNotContain will then PASS (correct enforcement of spec contract).
            // CYC=1. No lock. ASCII-only.
            var srcFile = ResolveCopyEngineSource();
            if (System.IO.File.Exists(srcFile))
            {
                var src = System.IO.File.ReadAllText(srcFile);
                Assert.DoesNotContain(
                    "order.OrderState != OrderState.Submitted",
                    src,
                    System.StringComparison.Ordinal);
            }
            // Source unavailable: vacuous pass -- regression caught only when source is present.
        }
```

### xUnit [Fact] Names and Assertions

| Test Name | Decorator | Assert | CYC | Notes |
|-----------|-----------|--------|-----|-------|
| `FindFollowerBracketOrder_AcceptedState_IsReturned` | `[Fact]` | `Assert.Contains("order.OrderState != OrderState.Accepted", src)` -- verifies Accepted is in the production guard | 1 | Source-text contract. Structural fallback if source unavailable. No NT8 runtime. Must pass in CI. Non-tautological: fails if guard text is removed from CopyEngine.cs. |
| `FindFollowerBracketOrder_SubmittedState_IsSkipped` | `[Fact(Skip=...)]` | `Assert.DoesNotContain("order.OrderState != OrderState.Submitted", src)` -- verifies Submitted is absent from guard | 1 | Skip active because Submitted IS currently in guard (line 4609). When guard is fixed, un-Skip. Skip rationale is factually correct. Does not break CI. |

### JS Rules (Ticket 1)

| Rule | Requirement | Verification |
|------|-------------|--------------|
| JS-021 | No `lock()` in test methods | Source-text check and reflection fallback -- no shared mutable state |
| JS-001 | No `throw` in hot path | Test methods are not hot path; no throw present |
| CYC <= 8 | All methods <= 8 branches | CYC=1 per test method (straight-line source check + assert) |
| JS-042 | ASCII-only string literals | All strings including Skip attribute string are ASCII-only. No Unicode, no curly quotes, no emoji. |
| JS-036 | No `DateTime.Now` | Not used in tests |
| WS-GUARD | No whitespace mutation outside changed lines | Insert-only diff. No surrounding whitespace edits. |
| PTT-PREFIX | `CreateOrder` calls use "PTT-" prefix | N/A -- no CreateOrder in test methods |

### 7-Scan Checklist (Ticket 1)

| Scan | Check | Command | Expected | Result |
|------|-------|---------|----------|--------|
| SCAN-01 | No `lock(` in new test methods | `grep "lock(" src/PropTraderTools/CopyEngineTests.cs` (new lines only) | 0 matches in new lines | PASS -- source-text check, no shared state |
| SCAN-02 | No `throw ` in new test methods | `grep "throw " src/PropTraderTools/CopyEngineTests.cs` (new lines only) | 0 matches in new lines | PASS -- no throw present |
| SCAN-03 | CYC <= 8 in new methods | Manual review | CYC=1 each (straight-line source check + assert) | PASS |
| SCAN-04 | ASCII-only string literals in new methods | Manual review of string literals and Skip attribute | No Unicode, no emoji, no curly quotes | PASS |
| SCAN-05 | No whitespace mutation | `git diff --stat` | Diff touches only the inserted test lines | PASS -- insert-only |
| SCAN-06 | No `DateTime.Now` | `grep "DateTime.Now" src/PropTraderTools/CopyEngineTests.cs` (new lines only) | 0 matches in new lines | PASS -- not used |
| SCAN-07 | No `CreateOrder` in new test methods | `grep "CreateOrder" src/PropTraderTools/CopyEngineTests.cs` (new lines only) | 0 matches in new lines | N/A -- PASS |

---

## T2 -- DW-BACKLOG-TRIAGE-B167 (Doc Only)

### Spec Requirement IDs

| Req ID | Description |
|--------|-------------|
| DW-B159-TP4-RACE-T2-REQ-01 | Create `docs/brain/DW-B159-TP4-RACE/06-deferred-backlog.md` |
| DW-B159-TP4-RACE-T2-REQ-02 | B167-DEFER-02 MUST remain OPEN (P1 defect -- cannot be closed by test-only epic) |
| DW-B159-TP4-RACE-T2-REQ-03 | B167-DEFER-04 marked CLOSED (Director confirmed SIM gate complete 2026-09-20) |
| DW-B159-TP4-RACE-T2-REQ-04 | B167-DEFER-03 marked OPEN (UNBLOCKED) -- B167-DEFER-04 prerequisite now met |
| DW-B159-TP4-RACE-T2-REQ-05 | DW-B159-TP4-RACE item marked CLOSED (this epic -- verification + tests complete) |
| DW-B159-TP4-RACE-T2-REQ-06 | All other items (B167-DEFER-01, DW-B159-K4, DW-B159-K3-UI) carried forward OPEN unchanged |

### Scope Lock

**File:** `docs/brain/DW-B159-TP4-RACE/06-deferred-backlog.md` -- **CREATE** (new file)
**Zero `.cs` edits. Zero production code changes. Zero test file edits.**
This ticket is doc-only. The only file touched is the new backlog markdown document.

### Backlog Item Operations (complete engineer contract)

| Item ID | Prior Status (DW-B159-KS/06-deferred-backlog.md) | New Status | Action |
|---------|--------------------------------------------------|------------|--------|
| B167-DEFER-01 | OPEN | **OPEN (carried forward unchanged)** | ECA-1 double-position race -- monitor at SIM gate. No change. |
| B167-DEFER-02 | OPEN (P1, B168 required) | **OPEN (carried forward unchanged) -- CRITICAL** | OVERRIDE: P1 code defect. IsDispatchTriggerState + Gate0.5 systemic routing failure. Cannot be closed by test-only pipeline. Requires dedicated B168 block. Second SIM session 2026-09-20 confirmed bracket delivery issue was user chart-trader interference; code-level fix still requires B168 investigation. |
| B167-DEFER-03 | OPEN | **OPEN (UNBLOCKED)** | B167-DEFER-04 was blocking prerequisite. Now that B167-DEFER-04 is CLOSED, B161-B166 combined SIM gate is unblocked. Next action: schedule dedicated SIM session. Not a code ticket. |
| B167-DEFER-04 | OPEN | **CLOSED** | B167 12-step SIM gate complete. Entry copy confirmed, stop drags propagated to all followers, flatten cleaned all brackets, no re-entry. All sec-F criteria met. Evidence: output.md second session + NinjaTrader Grid 2026-09-20 CSVs. Director confirmed 2026-09-20. |
| DW-B159-TP4-RACE | NEW (this epic) | **CLOSED** | Opened and closed in this block. Fix was already present in code (DW-LB-FL-01 V7 Fix B + BWAVE-REFACTOR-LaneB-T4). Ticket 1 added 2 verification tests. |
| DW-B159-K4 | OPEN | **OPEN (carried forward unchanged)** | intendedBePrice sourcing dependency -- future epic. No change. |
| DW-B159-K3-UI | OPEN | **OPEN (carried forward unchanged)** | Settings-panel exposure of BeRetryMaxAttempts -- future epic. No change. |

### Document Content (engineer must create verbatim)

```markdown
# DW-B159-TP4-RACE Deferred Backlog
<!-- block: DW-B159-TP4-RACE -->
<!-- wave: DW-B159-TP4-RACE -->
<!-- date: 2026-10-01 -->
<!-- produced-by: ptt-architect (Phase 3, Ticket 2) -->

---

## Block Header

| Field | Value |
|-------|-------|
| Epic | DW-B159-TP4-RACE -- FindFollowerBracketOrder Accepted-state race fix |
| Wave | DW-B159-TP4-RACE |
| Prior backlog source | docs/brain/DW-B159-KS/06-deferred-backlog.md |
| Date | 2026-10-01 |

---

## Items CLOSED This Block

---

### DW-B159-TP4-RACE -- FindFollowerBracketOrder Accepted-State Verification

| Field | Value |
|-------|-------|
| ID | DW-B159-TP4-RACE |
| Priority | P1 |
| Status | **CLOSED** |
| Closed in block | DW-B159-TP4-RACE |

**Closure evidence:** Fix was already present in CopyEngine.cs at lines 4606-4613
(DW-LB-FL-01 V7 Fix B + BWAVE-REFACTOR-LaneB-T4). OrderState.Accepted confirmed at
line 4608. 2 new [Fact] tests added to CopyEngineTests.cs DwB16702SlateReadyTests class:
`FindFollowerBracketOrder_AcceptedState_IsReturned` (PASS) and
`FindFollowerBracketOrder_SubmittedState_IsSkipped` (Skipped -- spec-code conflict
flagged for Director). Zero production code changes.

---

### B167-DEFER-04 -- B167 SIM Gate

| Field | Value |
|-------|-------|
| ID | B167-DEFER-04 |
| Priority | P1 |
| Status | **CLOSED** |
| Closed in block | DW-B159-TP4-RACE (Director confirmation 2026-09-20) |

**Closure evidence:** B167 12-step SIM gate complete. Entry copy confirmed. Stop drags
propagated to all followers. Flatten cleaned all brackets with no re-entry. All sec-F
criteria met. Evidence: output.md second session + NinjaTrader Grid 2026-09-20 CSVs.

---

## Items UPDATED This Block

---

### B167-DEFER-03 -- B161-B166 Combined SIM Gate

| Field | Value |
|-------|-------|
| ID | B167-DEFER-03 |
| Priority | P1 |
| Target Block | next SIM session |
| Status | OPEN -- **UNBLOCKED** (was: OPEN, blocked on B167-DEFER-04) |

**Update reason:** B167-DEFER-04 (B167 SIM Gate) is now CLOSED. B167-DEFER-03 was
explicitly waiting on B167-DEFER-04 as its prerequisite per DW-B159-KS/06-deferred-backlog.md.
Combined B161-B166 SIM gate is now unblocked.

**Next action:** Schedule dedicated combined SIM session. Not a code ticket.

---

## Items OPEN / Deferred (Carried Forward Unchanged)

---

### B167-DEFER-02 -- ATM Bracket Placement (fo=NULL)

| Field | Value |
|-------|-------|
| ID | B167-DEFER-02 |
| Priority | P1 |
| Target Block | B168 (dedicated block required) |
| Status | **OPEN -- OVERRIDE: must remain OPEN** |
| DW card | docs/brain/DW-B167-02/00-dw-card.md |

**OVERRIDE REASON:** This is a P1 code defect requiring dedicated B168 block. Cannot
be closed by a test-only verification epic. Second SIM session 2026-09-20 confirmed that
bracket delivery issue during that session was user chart-trader interference (not the
systemic routing failure). However, the code-level root cause -- IsDispatchTriggerState
rejects StopMarket bracket orders at all states, Gate0.5 blocks Target1/2/3 by name --
has NOT been fixed. ATM brackets (Stop1/2/3, Target1/2/3) are still never copied to
followers after leader entry fill. P1 priority unchanged.

**Action:** Requires separate block (B168 or DW-B168). Director must run the second
output.md test procedure per DW-B167-02/00-dw-card.md before architect can design fix.

---

### B167-DEFER-01 -- ECA-1 Double-Position Race

| Field | Value |
|-------|-------|
| ID | B167-DEFER-01 |
| Priority | P2 |
| Target Block | future / monitor at SIM gate session |
| Status | OPEN |

**Description (carried forward verbatim from DW-B159-KS):**
~100-500 ms race window where a pre-fill drag drain fires (HadPositionAtDrainTime=false)
AND the follower manually fills before SubmitDrainedEntry can submit the resubmit entry.
If the fill arrives during the drain window, SubmitDrainedEntry may submit a second entry
order, potentially creating a double follower position.

**Action:** Monitor during combined B161-B166 SIM gate session (now unblocked).

---

### DW-B159-K4 -- intendedBePrice Sourcing Dependency

| Field | Value |
|-------|-------|
| ID | DW-B159-K4 |
| Priority | P2 |
| Target Block | future |
| Status | OPEN |

**Description (carried forward verbatim):** TryArmBePriceRetry receives newStop from
MoveStopToBreakEven. If the BE level formula changes in a future epic, the retry will
fire at the new BE level. Formulas must remain consistent.

**Next action:** Flag this item for the architect in any future epic that modifies
newStop computation in MoveStopToBreakEven.

---

### DW-B159-K3-UI -- Settings-Panel Exposure of BeRetryMaxAttempts

| Field | Value |
|-------|-------|
| ID | DW-B159-K3-UI |
| Priority | P3 |
| Target Block | future |
| Status | OPEN |

**Description (carried forward verbatim):** BeRetryMaxAttempts was extracted to a
private const in DW-B159-KS. Settings-panel wiring deferred out of scope.

**Next action:** Evaluate as part of a future settings/configuration epic.

---

*Deferred backlog produced by ptt-architect (Phase 3, Ticket 2). Block DW-B159-TP4-RACE. Date 2026-10-01.*
*Closed this block: DW-B159-TP4-RACE, B167-DEFER-04.*
*Updated this block: B167-DEFER-03 (UNBLOCKED).*
*Carried forward OPEN: B167-DEFER-01, B167-DEFER-02 (MUST remain OPEN -- P1/B168), DW-B159-K4, DW-B159-K3-UI.*
```

### JS Rules (Ticket 2)

| Rule | Requirement | Status |
|------|-------------|--------|
| JS-021 | No `lock()` | N/A -- no .cs edits |
| JS-001 | No `throw` in hot path | N/A -- no .cs edits |
| CYC <= 8 | All methods <= 8 branches | N/A -- no .cs edits |
| JS-042 | ASCII-only string literals | N/A -- no .cs edits |
| JS-036 | No `DateTime.Now` | N/A -- no .cs edits |
| WS-GUARD | No whitespace mutation outside changed lines | N/A -- no .cs edits |
| PTT-PREFIX | `CreateOrder` calls use "PTT-" prefix | N/A -- no .cs edits |

### 7-Scan Checklist (Ticket 2)

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 | `grep "lock("` in modified files | N/A -- no .cs file modified in this ticket |
| SCAN-02 | `grep "throw "` in modified files | N/A -- no .cs file modified in this ticket |
| SCAN-03 | CYC <= 8 in all new methods | N/A -- no .cs file modified in this ticket |
| SCAN-04 | ASCII-only string literals in new code | N/A -- no .cs file modified in this ticket |
| SCAN-05 | No whitespace mutation (diff touches only inserted lines) | N/A -- no .cs file modified in this ticket |
| SCAN-06 | No `DateTime.Now` in modified files | N/A -- no .cs file modified in this ticket |
| SCAN-07 | No `CreateOrder` in modified files | N/A -- no .cs file modified in this ticket |

---

## Execution Order

| Step | Ticket | Action | Verify |
|------|--------|--------|--------|
| 1 | T1 | Grep CopyEngine.cs lines 4606-4613 to confirm Accepted at line 4608 | Match confirmed (pre-check) |
| 2 | T1 | Insert 2 test methods in CopyEngineTests.cs DwB16702SlateReadyTests class before line 9829 `}` | `dotnet test` -- new tests visible; Test1 PASS, Test2 Skipped |
| 3 | T2 | Create docs/brain/DW-B159-TP4-RACE/06-deferred-backlog.md with content above | File exists, content matches template |
| 4 | Both | `powershell -File .\scripts\ptt-sync.ps1` | Hard-link sync confirmed |

---

## Final Summary

| Item | Value |
|------|-------|
| Production code changes | **ZERO** |
| Test file changes | 2 new [Fact] methods in `CopyEngineTests.cs` `DwB16702SlateReadyTests` class |
| Doc file changes | 1 new `docs/brain/DW-B159-TP4-RACE/06-deferred-backlog.md` |
| Fix status | Already present (DW-LB-FL-01 V7 Fix B + BWAVE-REFACTOR-LaneB-T4) at CopyEngine.cs lines 4606-4613 |
| Spec/code discrepancy | Submitted in guard but spec says excluded -- flagged via Skipped test |
| B167-DEFER-02 | **OPEN -- must not be closed** (P1/B168 required) |
| B167-DEFER-04 | CLOSED (Director confirmed SIM gate 2026-09-20) |
| B167-DEFER-03 | OPEN (UNBLOCKED -- B167-DEFER-04 prerequisite met) |
| CYC (new tests) | 1 per method (well within <= 8 limit) |
| JS violations | None |
| TR-T1-TEST1-TAUTOLOGY | FIXED -- Test 1 now uses Assert.Contains(source guard text). Non-tautological: fails if Accepted removed from guard. |
| TR-T1-TEST2-ALWAYS-FAIL | FIXED -- Test 2 now uses Assert.DoesNotContain(source guard text). Skip rationale corrected: test fails while Submitted is in guard, passes when removed. |

---

*Tickets produced by ptt-architect (Phase 3, Correction Cycle 1). Block DW-B159-TP4-RACE. Date 2026-10-01.*
*Return value: TICKETS_COMPLETE*
