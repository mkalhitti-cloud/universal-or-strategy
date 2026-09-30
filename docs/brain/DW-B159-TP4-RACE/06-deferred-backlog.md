# DW-B159-TP4-RACE Deferred Backlog
<!-- block: DW-B159-TP4-RACE -->
<!-- wave: DW-B159-TP4-RACE -->
<!-- date: 2026-10-01 -->
<!-- produced-by: ptt-engineer (Phase 4a, Ticket 2) / finalized: ptt-plan-reviewer (Phase 5) -->

---

## Block Header

| Field | Value |
|-------|-------|
| Epic | DW-B159-TP4-RACE -- FindFollowerBracketOrder Accepted-state race fix |
| Block | DW-B159-TP4-RACE |
| Prior backlog source | docs/brain/DW-B159-KS/06-deferred-backlog.md |
| Phase 5 verdict | FINAL_PASS |
| Date | 2026-10-01 |

---

## Items CLOSED This Block

---

### B167-DEFER-04 -- B167 12-Step SIM Gate

| Field | Value |
|-------|-------|
| ID | B167-DEFER-04 |
| Priority | P1 |
| Status | **CLOSED** |
| Closed in block | DW-B159-TP4-RACE (Director confirmation 2026-09-20) |

**Closure evidence:** Second SIM session 2026-09-20. Entry copy confirmed, stop drags
propagated to all 3 followers, flatten cleaned all brackets, no re-entry. All sec-F
criteria met. Evidence: output.md second session + NinjaTrader Grid 2026-09-20 CSVs.

---

### DW-B159-TP4-RACE -- fo=NULL Working-Only Guard (Race Fix)

| Field | Value |
|-------|-------|
| ID | DW-B159-TP4-RACE |
| Priority | P1 |
| Status | **CLOSED** (opened and closed this block) |
| Closed in block | DW-B159-TP4-RACE |

**Closure evidence:** Pre-check confirmed fix already present at CopyEngine.cs lines
4606-4613 (OrderState.Accepted included since prior block DW-LB-FL-01 V7 Fix B).
Two tests added: `FindFollowerBracketOrder_AcceptedState_IsReturned` and
`FindFollowerBracketOrder_SubmittedState_IsSkipped`. VERIFY_PASS confirmed
(ticket-1-verification.md).

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

**Note:** Second SIM session 2026-09-20 confirmed bracket delivery to all 3 followers
when user does NOT interfere via chart trader. Root cause of original observation was
user operating chart trader on follower account (cancels follower orders directly).
Code-level investigation of IsDispatchTriggerState + Gate0.5 still deferred to B168.

**Action:** Requires separate block (B168 or DW-B168). Director must run the second
output.md test procedure per DW-B167-02/00-dw-card.md before architect can design fix.

---

### B167-DEFER-01 -- ECA-1 Double-Position Race (Pre-fill Drag + Manual Fill)

| Field | Value |
|-------|-------|
| ID | B167-DEFER-01 |
| Priority | P2 |
| Target Block | future / monitor at SIM gate session |
| Status | OPEN |
| DW card | None yet |

**Description (carried forward verbatim from DW-B159-KS):**
~100-500 ms race window where a pre-fill drag drain fires (HadPositionAtDrainTime=false)
AND the follower manually fills before SubmitDrainedEntry can submit the resubmit entry.
If the fill arrives during the drain window, SubmitDrainedEntry may submit a second entry
order, potentially creating a double follower position.

**Probability:** Low. Not observed in any test session to date.

**Mitigation in B167:** `[DRAIN-SUBMIT-PREFILL]` diagnostic log (T4) fires for every
pre-fill drain path, providing forensic evidence if a race occurs.

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

## 7-Scan Table

| Scan | Check | Result | Reason |
|------|-------|--------|--------|
| SCAN-01 | No `lock(` in modified files | N/A | No .cs file modified in this ticket |
| SCAN-02 | No non-ASCII chars in modified files | N/A | No .cs file modified in this ticket |
| SCAN-03 | No `FontFamily` in modified files | N/A | No .cs file modified in this ticket |
| SCAN-04 | No hardcoded hex colors in modified files | N/A | No .cs file modified in this ticket |
| SCAN-05 | All `CreateOrder` calls use "PTT-" prefix | N/A | No .cs file modified in this ticket |
| SCAN-06 | No `DateTime.Now` in modified files | N/A | No .cs file modified in this ticket |
| SCAN-07 | No `lock\s*(` pattern in modified files | N/A | No .cs file modified in this ticket |

**All 7 scans N/A.** This ticket creates one doc file only (`06-deferred-backlog.md`).
Zero `.cs` files touched.

---

*Deferred backlog produced by ptt-engineer (Phase 4a, Ticket 2). Block DW-B159-TP4-RACE. Date 2026-10-01.*
*Closed this block: DW-B159-TP4-RACE, B167-DEFER-04.*
*Updated this block: B167-DEFER-03 (UNBLOCKED).*
*Carried forward OPEN: B167-DEFER-01, B167-DEFER-02 (MUST remain OPEN -- P1/B168), DW-B159-K4, DW-B159-K3-UI.*
