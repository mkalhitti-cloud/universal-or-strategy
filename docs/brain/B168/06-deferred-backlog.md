# B168 Deferred Backlog

## Block: B168 — DW-B168-01 COLLATERAL-STORM
Status: IMPLEMENTATION COMPLETE — awaiting Director SIM gate

---

### DW-B168-01 — COLLATERAL-STORM (CLOSED pending SIM)

Resolution: `IsLeaderLegBeingChanged` guard added to `ResubmitCollateralLegs`.
            `IsLeaderLegBeingChangedTestable` seam added for xUnit access.
            All 4 unit tests PASS. ptt-sync.ps1 exit 0.
SIM gate required: Director must drag all 3 stops simultaneously on live SIM
  and confirm 0 missing stops on followers before marking fully CLOSED.
See `docs/brain/B168/05-final-review.md` Section K, DW-B168-K1 for full SIM procedure.

---

### Known Acceptable Behavior (not a bug)

Stop3 as last Accepted handler executes one clean cancel+resubmit of legs 1+2.
This is correct and intentional — Stop3 confirms final price for all legs.
No further fix needed.
Documented in spec DW-B168-01 §9 and architecture plan §10.

---

### Newly Discovered Items

None. No new DW items surfaced during B168 implementation.

---

### Prior OPEN Items Carried Forward

The following items from preceding blocks remain OPEN and are not addressed in B168:

| ID | Block origin | Description | Priority | Status |
|---|---|---|---|---|
| DW-B168-K1 | B168 | Director SIM gate: drag all 3 stops simultaneously, confirm 0 missing follower stops | P1 | OPEN |
| B167-DEFER-02 / DW-B168-K2 | B167 | DW-B167-02: follower ATM brackets never placed after leader fill (fo=NULL). Second output.md required. Dedicated block required. | P1 | OPEN |
| B167-DEFER-01 / DW-B168-K3 | B167 | ECA-1 double-position race (pre-fill drag + manual fill). Low probability. Monitor at SIM. | P2 | OPEN |
| B167-DEFER-03 / DW-B168-K4 | B167 | Combined B161-B166 SIM gate pending | P1 | OPEN |
| B167-DEFER-04 / DW-B168-K5 | B167 | B167 SIM gate pending (prerequisite for B168 SIM gate) | P1 | OPEN |
| DW-B159-K2 / DW-B168-K6 | B159 | 14 xUnit tests for B159 features (infrastructure required) | P2 | OPEN |
| DW-B159-K3 / DW-B168-K7 | B159 | Attempt cap hardcoded at 3 in TryArmBePriceRetry | P2 | OPEN |
| DW-B159-K4 / DW-B168-K8 | B159 | intendedBePrice sourcing dependency | P2 | OPEN |
| DW-B159-K5 / DW-B168-K9 | B159 | Stale line number in IsFollowerPositionBlocked comment | P3 | OPEN |

---

*Deferred backlog produced by ptt-plan-reviewer. Phase 5. Block B168.*
*DW-B168-01 COLLATERAL-STORM implementation is complete. FINAL_PASS granted subject to Director SIM gate.*
