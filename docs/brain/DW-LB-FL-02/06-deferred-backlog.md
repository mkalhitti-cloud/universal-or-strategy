# DW-LB-FL-02 -- Deferred Backlog

**Maintained by**: ptt-plan-reviewer (Phase 5 Final Review)
**Format**: One section per block. Items deferred from current block are appended at end.

---

## Block: DW-LB-FL-02 -- 2026-08-22

### Items Deferred

| ID | Item | Priority | Status |
|----|------|----------|--------|
| DW-LB-FL-02-DW-01 | **Option B: cancel PTT-BE-Stop-* in FlattenFollower before flatten.** When the flatten path fires, proactively cancel any Working PTT-BE-Stop-* orders on the follower account before issuing PTT-Flatten. This was the alternative to the chosen Option A fix and was evaluated during architecture planning. | P2 | OPEN |

### Rationale

Option B was rejected as the primary fix for DW-LB-FL-02 for the following reasons (from 02-architecture-plan.md):

1. `CancelAllAccountOrders` (DW-B69-01) already cancels ALL active orders before flatten. Adding a targeted PTT-BE-Stop-* cancel is redundant.
2. The race condition (PTT-BE-Stop fills between cancel submission and ACK) cannot be eliminated at the follower-cancel layer. B76 HOTFIX-B76-FLATTEN-RACE-01 already addresses the post-cancel position re-read.
3. Option B addresses the consequence (follower inversion) rather than the root cause (spurious dispatch when leader is flat). Option A (guard 3.5 in TryDispatchLeaderFlat) blocks at the source.
4. Option B would increase CYC in FlattenFollower (currently CYC=3) and require new name-matching logic, creating regression risk in the critical flatten path.

Option A is both necessary and sufficient. Option B is retained here in case new follower-side inversion patterns emerge in future blocks that are not preventable at the leader-dispatch gate (e.g., scenarios where the leader dispatch gate cannot be reached or is bypassed by a different code path).

**No blocking work deferred.** DW-LB-FL-02 is complete. Option B is a future P2 consideration only.

---

*This file will be appended when new blocks produce deferred items.*
