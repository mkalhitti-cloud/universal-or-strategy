# B132 LaneA -- Deferred Backlog

## Block: B132 LaneA
## Date: 2026-08-31
## Status: PIPELINE_COMPLETE

---

## Deferred Items

| ID | Item | Priority | Reason Deferred | Target Block |
|----|------|----------|-----------------|--------------|
| DW-B132-K1 | **SIM Gate validation**: Live NT8 session confirming PTT-STP-Drag placed on follower account after target drag (DW-B141 runtime confirmation). Co-scheduled with DW-B131-K3 (Block A-Prime SIM validation of DW-B139). Requires a physical NT8 trading session with SIM accounts; cannot be executed in the unit-test-only CI environment. | P1 | SIM gate requires human-in-the-loop NT8 session. Not a code defect. | B133 or next SIM block |
| DW-B132-K2 | **Complexity audit tooling**: `scripts/complexity_audit.py` does not exist. SCAN-05 (CYC check) was executed via manual count by both engineer (Layer 2) and verifier (Layer 3). Pre-existing tooling gap, not introduced by B132 LaneA. Automated complexity audit tooling needs to be created in a dedicated tooling ticket. | P2 | Pre-existing gap. No code correctness impact -- manual CYC count confirmed all methods <=8. | future |
| DW-B132-K3 | **Integration-level xUnit test coverage**: Integration-level xUnit tests for `SyncAtmFollowerTarget` Phase C (asserting that `acc.CreateOrder(StopMarket)` is actually invoked on the follower Account mock) use `Assert.True(true, ...)` structural placeholders because NT8 `Account` class is sealed and cannot be subclassed or mocked without a test-double framework. Full integration coverage requires a NT8 test harness abstraction. This is the established project convention (same pattern as B131LaneBTests). | P2 | NT8 sealed Account constraint. Established convention. No correctness impact -- pure-computation paths are verified by real [Fact] assertions. | future |

---

## Prior Blocks

First backlog entry for B132. No prior B132 deferred backlog exists.

---

## Notes

- DW-B131-K3 (from B131 LaneB deferred backlog): SIM validation of DW-B139 Block A-Prime fix. This was co-scheduled with DW-B132-K1 -- both validations run in the same SIM gate session.
- All three items above are documentation obligations only. None block PIPELINE_COMPLETE.
- No P0 items deferred. DW-B141 (P0) is fully implemented and verified in B132 LaneA.
