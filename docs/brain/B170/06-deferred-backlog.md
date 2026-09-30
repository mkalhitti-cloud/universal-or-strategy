# B170 Deferred Backlog

**Block:** B170
**Written by:** ptt-plan-reviewer (Phase 5)
**Last updated:** B170 final review

This file is REQUIRED for FINAL_PASS. It carries forward all OPEN deferred items from prior blocks and adds new items discovered in B170.

---

## B170 Items

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B170-01 | B170 | Extend FindLeaderStopPrice predicate to include OrderState.Accepted (follower stop lost on target drag) | P0 | **CLOSED** — implemented in B170, VERIFY_PASS both tickets |
| DW-B170-SIM | B170 | SIM GATE: Director drags target on leader — confirm follower stop preserved (not missing) after drag. DW-B170-01 live regression verify. | P0 | **OPEN** — code complete; SIM verification required |

---

## Prior Open Items (carried forward)

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B169-K1 | B169 | SIM GATE: 3-stop simultaneous drag — 0 missing follower stops | P0 | OPEN (partially verified — Sim103/104 PASS, Sim102 blocked by B167-DEFER-02) |
| DW-B169-K2 | B169 | SIM GATE: single-stop drag regression | P0 | OPEN |
| DW-B168-K1 | B168 | Superseded by DW-B169-K1/K2 | P0 | OPEN (superseded) |
| B167-DEFER-02 | B167 | fo=NULL: follower ATM brackets never placed after leader fill. Dedicated block required. | P1 | OPEN |
| B167-DEFER-01 | B167 | ECA-1 double-position race. Monitor at SIM. | P2 | OPEN |
| B167-DEFER-03 | B167 | Combined B161-B166 SIM gate pending | P1 | OPEN |
| B167-DEFER-04 | B167 | B167 SIM gate pending | P1 | OPEN |
| DW-B159-K2 | B159 | 14 xUnit tests | P2 | OPEN |
| DW-B159-K3 | B159 | Attempt cap hardcoded at 3 | P2 | OPEN |
| DW-B159-K4 | B159 | intendedBePrice sourcing | P2 | OPEN |
| DW-B159-K5 | B159 | Stale line comment | P3 | OPEN |

---

## Notes

- DW-B170-SIM is P0: the code fix is deployed but the SIM gate must be cleared before DW-B170-01 can be considered production-safe.
- DW-B169-K1 remains partially blocked by B167-DEFER-02 (fo=NULL bracket placement). These two items must be addressed in the same block or in sequence.
- DW-B168-K1 is superseded by DW-B169-K1/K2; it will be CLOSED when both K1/K2 are CLOSED.
- B167-DEFER-02 is the highest-risk unaddressed functional gap (P1): follower ATM brackets never placed after leader fill when fo=NULL.
