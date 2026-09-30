# DW-B159-TP4-RACE — Final Review
<!-- block: DW-B159-TP4-RACE -->
<!-- phase: 5 -->
<!-- produced-by: ptt-plan-reviewer (Phase 5) -->
<!-- date: 2026-10-01 -->

---

## Block Header

| Field | Value |
|-------|-------|
| Epic | DW-B159-TP4-RACE -- FindFollowerBracketOrder Accepted-state race fix |
| Phase | 5 -- Final Review |
| Tickets | 2 (T1 + T2) |
| Pipeline | SINGLE |
| Prior backlog source | docs/brain/DW-B159-KS/06-deferred-backlog.md |
| T1 verdict | BUILD_PASS (ticket-1-completion.md) + VERIFY_PASS (ticket-1-verification.md) |
| T2 verdict | BUILD_PASS (ticket-2-completion.md) + VERIFY_PASS (ticket-2-verification.md) |

---

## Files Read (Mandatory Inputs)

| File | Purpose | Confirmed Present |
|------|---------|-------------------|
| docs/brain/DW-B159-TP4-RACE/02-architecture-plan.md | Architecture plan | YES |
| docs/brain/DW-B159-TP4-RACE/04-ticket-review.md | Ticket review (Cycle 2 TICKET_REVIEW_PASS) | YES |
| docs/brain/DW-B159-TP4-RACE/ticket-1-completion.md | T1 BUILD_PASS | YES |
| docs/brain/DW-B159-TP4-RACE/ticket-1-verification.md | T1 VERIFY_PASS | YES |
| docs/brain/DW-B159-TP4-RACE/ticket-2-completion.md | T2 BUILD_PASS | YES |
| docs/brain/DW-B159-TP4-RACE/ticket-2-verification.md | T2 VERIFY_PASS | YES |
| docs/brain/DW-B159-TP4-RACE/06-deferred-backlog.md | Draft backlog (Ticket 2 engineer) | YES |
| docs/brain/DW-B159-KS/06-deferred-backlog.md | Prior block backlog (read only) | YES |
| src/PropTraderTools/CopyEngineTests.cs (lines 9820-9895) | Verify final test state | YES |
| src/PropTraderTools/CopyEngine.cs (lines 4595-4625) | Verify production guard unchanged | YES |

---

## Section A — Coherent System

### A1. New Tests Target the Production Guard Correctly

Both new test methods use `ResolveCopyEngineSource()` to resolve the
`CopyEngine.cs` source path and read it via `System.IO.File.ReadAllText`.

- **Test 1** (`FindFollowerBracketOrder_AcceptedState_IsReturned`, line 9836):
  `Assert.Contains("order.OrderState != OrderState.Accepted", src, StringComparison.Ordinal)`.
  The target string is present at `CopyEngine.cs` line 4608. If this line is ever
  removed from the guard, the assertion will throw `ContainsException` -- regression
  detected. **Non-tautological. Source-text contract. COHERENT.**

- **Test 2** (`FindFollowerBracketOrder_SubmittedState_IsSkipped`, line 9866):
  `Assert.DoesNotContain("order.OrderState != OrderState.Submitted", src, StringComparison.Ordinal)`.
  The target string IS present at `CopyEngine.cs` line 4609. While Submitted remains in
  the guard, this assertion fails (correctly). `[Fact(Skip=...)]` prevents CI breakage.
  Skip rationale is factually correct (verified in Cycle 2). **COHERENT.**

Both methods have a structural fallback (`FindFollowerBracketOrderTestable` reflection
check) when source file is unavailable. No structural gap.

**Section A verdict: PASS**

### A2. Cross-file JS Violations

No cross-file JS rule violations found in the new test methods. See Section C for
per-scan detail. See DNA Rule Check in ticket-1-verification.md (all 16 rules: PASS).

**Section A2 verdict: PASS -- no cross-file violations**

### A3. Missing Wiring

- `ResolveCopyEngineSource()` is defined at line 9768 within `DwB16702SlateReadyTests`.
  Both new methods are also in `DwB16702SlateReadyTests` (lines 9836-9886). Access: VALID.
- Class closing brace at line 9887 (shifted from 9829 after 58-line insert).
- `B159KSuiteTests` begins at line 9894 (separate class, unchanged).
- No missing wiring.

**Section A3 verdict: PASS -- no missing wiring**

---

## Section B — Spec Requirements Satisfied

### Ticket 1 Spec Requirements

| Req ID | Requirement | Status |
|--------|-------------|--------|
| DW-B159-TP4-RACE-T1-REQ-01 | `OrderState.Accepted` confirmed in guard at CopyEngine.cs line 4608 | SATISFIED -- confirmed by direct source read |
| DW-B159-TP4-RACE-T1-REQ-02 | Zero production code changes (CopyEngine.cs READ ONLY) | SATISFIED -- guard unchanged |
| DW-B159-TP4-RACE-T1-REQ-03 | Submitted discrepancy flagged via `[Fact(Skip=...)]` with correct rationale | SATISFIED -- line 9865 with corrected rationale (Cycle 2) |
| DW-B159-TP4-RACE-T1-REQ-04 | 2 new `[Fact]` methods in `DwB16702SlateReadyTests` class | SATISFIED -- lines 9836 and 9866 |

All 4 T1 spec requirements: **SATISFIED**.

### Ticket 2 Spec Requirements

| Req ID | Requirement | Status |
|--------|-------------|--------|
| DW-B159-TP4-RACE-T2-REQ-01 | `docs/brain/DW-B159-TP4-RACE/06-deferred-backlog.md` created | SATISFIED |
| DW-B159-TP4-RACE-T2-REQ-02 | B167-DEFER-02 MUST remain OPEN (P1 defect -- SPEC OVERRIDE) | SATISFIED -- OPEN OVERRIDE present |
| DW-B159-TP4-RACE-T2-REQ-03 | B167-DEFER-04 marked CLOSED (Director confirmation 2026-09-20) | SATISFIED |
| DW-B159-TP4-RACE-T2-REQ-04 | B167-DEFER-03 marked OPEN (UNBLOCKED) | SATISFIED |
| DW-B159-TP4-RACE-T2-REQ-05 | DW-B159-TP4-RACE item marked CLOSED (this epic) | SATISFIED |
| DW-B159-TP4-RACE-T2-REQ-06 | B167-DEFER-01, DW-B159-K4, DW-B159-K3-UI carried forward OPEN unchanged | SATISFIED |

All 6 T2 spec requirements: **SATISFIED**.

**Section B verdict: PASS**

---

## Section C — All 7 Scans Zero (Final State Verification)

Scan scope: `src/PropTraderTools/CopyEngineTests.cs` lines 9829-9886 (new content).
Source verified by direct read of lines 9820-9895.

| Scan | Check | Result | Evidence |
|------|-------|--------|---------|
| SCAN-01 | `lock(` = 0 in new methods | **PASS -- 0 matches** | Lines 9829-9886: no `lock(` keyword. Pure BCL calls only (File.ReadAllText, Assert, GetMethod). |
| SCAN-02 | `throw ` = 0 in new methods | **PASS -- 0 matches** | Lines 9829-9886: no `throw` statement. Assert raises ContainsException/DoesNotContainException -- not thrown by engineer code. |
| SCAN-03 | CYC <= 8 per method | **PASS -- CYC=2 each** | 1 `if (System.IO.File.Exists(srcFile))` branch per method. CYC = 1 + 1 = 2. Well within limit. |
| SCAN-04 | Non-ASCII = 0 in new lines | **PASS -- 0 matches** | All string literals ASCII-only. Confirmed: `"order.OrderState != OrderState.Accepted"`, `"order.OrderState != OrderState.Submitted"`, `"FindFollowerBracketOrderTestable"`, Skip attribute text -- all ASCII. |
| SCAN-05 | Whitespace mutation = 0 on surrounding lines | **PASS -- insert-only diff** | Lines 9822-9828 (pre-insert): unchanged. Lines 9887-9894 (post-insert): `}`, empty line, comment block, `B159KSuiteTests` class -- all intact. Insert-only diff confirmed. |
| SCAN-06 | `DateTime.Now` = 0 in new methods | **PASS -- 0 matches** | Lines 9829-9886: no DateTime reference. |
| SCAN-07 | `CreateOrder` = 0 in new methods | **PASS -- 0 matches** | Lines 9829-9886: no CreateOrder call. N/A for test methods. |

All 7 scans: **PASS**.

**Section C verdict: PASS**

---

## Section D — No Production Code Change

Direct read of `src/PropTraderTools/CopyEngine.cs` lines 4595-4618:

```
4595:  private Order? FindFollowerBracketOrder(
4596:      IEnumerable<Order> orders,
4597:      string? fromEntrySignalName,
4598:      bool isStop,
4599:      string? leaderName = null
4600:  )
4601:  {
4602:      foreach (var order in orders)
4603:      {
4604:          if (!OrderPassesBracketGate(order, fromEntrySignalName, leaderName, isStop))
4605:              continue;
4606:          if (
4607:              order.OrderState != OrderState.Working        // DW-LB-FL-01 V7: Initialized added
4608:              && order.OrderState != OrderState.Accepted
4609:              && order.OrderState != OrderState.Submitted
4610:              && order.OrderState != OrderState.ChangeSubmitted
4611:              && order.OrderState != OrderState.Initialized
4612:          )
4613:              continue;
4614:          if (MatchesBracketType(order, isStop))
4615:              return order;
4616:      }
4617:      return null;
4618:  }
```

Guard is **UNCHANGED** from the state documented in the architecture plan and ticket review.
`OrderState.Accepted` is present at line 4608 (Accepted-state race fix confirmed).
`OrderState.Submitted` is present at line 4609 (spec/code discrepancy -- flagged via
Skipped test, to be resolved by Director in a future block).

**Zero production code changes. Section D verdict: PASS**

---

## Section E — Cross-file Coherence

### E1. Class Boundary

- `DwB16702SlateReadyTests` class: opens line 9766, closes line 9887.
- New methods: lines 9836-9886 (inside `DwB16702SlateReadyTests`).
- `B159KSuiteTests` class: opens line 9894 (NOT inside `DwB16702SlateReadyTests`).
- No class boundary violation.

### E2. Naming Conflicts

- `FindFollowerBracketOrder_AcceptedState_IsReturned`: unique in the file (no prior method with this name).
- `FindFollowerBracketOrder_SubmittedState_IsSkipped`: unique in the file.
- No naming conflicts.

### E3. Helper Accessibility

`ResolveCopyEngineSource()` is `private static` in `DwB16702SlateReadyTests` (line 9768).
Both new test methods are also `public` within `DwB16702SlateReadyTests`. Private static
helper is accessible from instance methods in the same class. **ACCESSIBLE.**

### E4. xUnit Test Discovery

Both methods have `public` access and correct `[Fact]` / `[Fact(Skip=...)]` decorators.
Test 2 is Skipped -- it will not execute in CI. No xUnit discovery issues.

### E5. Interaction With Existing Tests

No edits to existing test methods. Insert-only. Surrounding lines (9822-9828,
9887-9894) verified unchanged by verifier. No existing test logic affected.

**Section E verdict: PASS**

---

## Section F — SIM Gate Status (Informational)

No SIM gate is required for this epic. Both tickets are test-only + doc-only. No
production behavior change was made.

**B167-DEFER-03** (combined B161-B166 SIM gate): **UNBLOCKED** this block. The blocking
prerequisite (B167-DEFER-04, B167 12-step SIM gate) was closed by Director confirmation
of the second SIM session on 2026-09-20. The combined B161-B166 SIM gate may now be
scheduled.

**Section F verdict: PASS (informational)**

---

## Section K — Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B159-TP4-RACE | FindFollowerBracketOrder Accepted-state race fix (this epic) | P1 | DW-B159-TP4-RACE | **CLOSED** (fix verified, 2 tests added, VERIFY_PASS) |
| B167-DEFER-04 | B167 12-step SIM gate | P1 | DW-B159-TP4-RACE | **CLOSED** (Director confirmed 2026-09-20) |
| B167-DEFER-03 | B161-B166 combined SIM gate | P1 | next SIM session | **OPEN (UNBLOCKED)** (B167-DEFER-04 prerequisite now met) |
| B167-DEFER-02 | ATM bracket placement (fo=NULL) -- IsDispatchTriggerState + Gate0.5 | P1 | B168 (dedicated block required) | **OPEN -- OVERRIDE** (P1 code defect; cannot be closed by test-only epic; B168 required) |
| B167-DEFER-01 | ECA-1 double-position race (pre-fill drag + manual fill) | P2 | future / monitor at SIM gate | **OPEN** (not observed; monitor at B167-DEFER-03 SIM session) |
| DW-B159-K4 | intendedBePrice sourcing dependency (MoveStopToBreakEven) | P2 | future | **OPEN** (carried forward unchanged) |
| DW-B159-K3-UI | Settings-panel exposure of BeRetryMaxAttempts | P3 | future | **OPEN** (carried forward unchanged) |

**Notes:**
- **No new deferred items** introduced by this block.
- B167-DEFER-02 OPEN status is a **hard constraint** (SPEC OVERRIDE). The P1 code defect
  (IsDispatchTriggerState + Gate0.5 root cause unfixed) requires a dedicated B168 block.
  The second SIM session 2026-09-20 clarified the ATM bracket delivery issue was
  triggered by user chart-trader interference, but the code-level root cause remains
  unaddressed. B167-DEFER-02 must not be closed until B168 investigation and fix are complete.
- DW-B159-TP4-RACE was opened and closed within this block (zero net addition to backlog).
- Prior OPEN items from DW-B159-KS/06-deferred-backlog.md that are not listed above:
  DW-B159-K1 was subsumed into B167-DEFER-03 in the prior block. Not re-listed.

---

## Aggregate 7-Scan Summary (Across src/PropTraderTools/)

Scope: all content introduced or modified in this block within `src/PropTraderTools/`.

| Scan | T1 (CopyEngineTests.cs lines 9829-9886) | T2 (doc-only, no .cs) | Aggregate |
|------|-----------------------------------------|----------------------|-----------|
| SCAN-01 lock( | 0 matches | N/A | **0** |
| SCAN-02 throw  | 0 matches | N/A | **0** |
| SCAN-03 CYC | CYC=2 each (<=8) | N/A | **PASS** |
| SCAN-04 non-ASCII | 0 matches | N/A | **0** |
| SCAN-05 whitespace mutation | 0 (insert-only) | N/A | **0** |
| SCAN-06 DateTime.Now | 0 matches | N/A | **0** |
| SCAN-07 CreateOrder | 0 matches | N/A | **0** |

**All 7 scans zero across src/PropTraderTools/ for this block.**

---

## Violation Summary

| ID | Rule | Description | Finding |
|----|------|-------------|---------|
| — | — | — | **None** |

No violations found.

---

## Final Verdict

**FINAL_PASS**

Both tickets delivered and verified without violations:
- T1 (CopyEngineTests.cs): 2 correct non-tautological test methods added. Source-text
  contract anchored to production guard. CYC=2 each. All 7 scans zero. Zero production
  code changes. All 4 spec requirements satisfied.
- T2 (06-deferred-backlog.md): All 7 backlog item dispositions correct. B167-DEFER-02
  OPEN OVERRIDE preserved. B167-DEFER-04 closed. B167-DEFER-03 unblocked. All 6 spec
  requirements satisfied.

Production guard (`FindFollowerBracketOrder`, CopyEngine.cs lines 4606-4613) is
confirmed unchanged and correctly includes `OrderState.Accepted` (race fix present
since DW-LB-FL-01 V7 Fix B). The `OrderState.Submitted` spec/code discrepancy is
correctly flagged via Skipped test and deferred for Director resolution.

Section K (deferred backlog): see above. 06-deferred-backlog.md finalized.

---

*Final review produced by ptt-plan-reviewer (Phase 5). Block DW-B159-TP4-RACE. Date 2026-10-01.*
*Verdict: FINAL_PASS. Violations: 0. Section K present.*
