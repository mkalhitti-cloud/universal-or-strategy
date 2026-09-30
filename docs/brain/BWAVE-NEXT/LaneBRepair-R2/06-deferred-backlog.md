# Deferred Backlog -- BWAVE-NEXT Lane B Repair (Cumulative)

**Epic scope**: BWAVE-NEXT LaneBRepair + LaneBRepair-R2 (Round 2)
**Written by**: ptt-plan-reviewer (Phase 5 Final Review)
**Last updated**: 2026-09-05 (Round 2 append)

---

## Block: BWAVE-NEXT-LaneBRepair (2026-09-05) — CARRIED FORWARD

Items recorded during Phase 5 final review of the original LaneBRepair block.

| ID | Description | Priority | Source | Status |
|----|-------------|----------|--------|--------|
| DW-NEXT-B-01 | Drain key is acct-only — second instrument on same account overwrites first drain intent. Extend key to `acct.Name + "\|" + instrument.FullName` when multi-instrument trading is added. | P2 (future) | PR #43 cubic finding | OPEN |
| DW-NEXT-B-02 | GTC/Day TIF and native-ATM Entry name not preserved in `SubmitEntryDirect` replacement. Carry original TIF + name in `PendingDispatchDrain` payload and use when creating replacement. | P2 (future) | PR #43 CodeRabbit finding | OPEN |

---

## Block: BWAVE-NEXT-LaneBRepair-R2 (2026-09-05) — CURRENT BLOCK

Items identified during Phase 5 final review of the LaneBRepair-R2 block.

| ID | Description | Priority | Source | Status |
|----|-------------|----------|--------|--------|
| DW-NEXT-B-03 | Test behavioral coverage gap: the 3 T1 tests are structural (reflection-based). They do not verify guard behavior — specifically: (a) TryAdd rejection preventing actual concurrent drain overwrite, (b) ContainsKey guard suppressing `ReplaceFollowerCopyOnAtmCancel` when a drain is active. A future ticket should add true behavioral tests using NT8 test-seam helpers or mock Account/Order objects to cover these guard paths end-to-end. | P2 | PR #43 LaneBRepair-R2 review — VERIFY §7 test-name-deviation note | OPEN |

---

## Notes

- DW-NEXT-B-01 and DW-NEXT-B-02 carried forward unchanged from `LaneBRepair/06-deferred-backlog.md`. Neither was addressed in the R2 block (both explicitly out of scope per architecture plan Dismissed Findings table).
- DW-NEXT-B-03 is new to this block. Root cause: engineer implemented structural reflection tests rather than the behavioral tests prescribed in `04-tickets.md`. The implementation itself is correct (verified by source inspection in Phase 4b); the gap is test coverage depth, not correctness.
- R2-F1 (STALE) and R2-V2 (STALE) do not generate deferred items.
- No items closed this block.

---

## Block: BWAVE-NEXT-LaneBRepair-R2-Round2 (2026-09-05) -- CURRENT BLOCK

Items identified during Phase 5 final review of the LaneBRepair-R2 Round 2 block
(R2-F1: AbortDrainOnFill + R2-F2: Clone mode Entry drain inclusion).

| ID | Description | Priority | Source | Status |
|----|-------------|----------|--------|--------|
| DW-NEXT-B-01 | Drain key is acct-only -- second instrument on same account overwrites first drain intent. Extend key to `acct.Name + "\|" + instrument.FullName` when multi-instrument trading is added. | P2 (future) | Carried forward -- PR #43 cubic finding | OPEN |
| DW-NEXT-B-02 | GTC/Day TIF and native-ATM Entry name not preserved in `SubmitEntryDirect` replacement. Carry original TIF + name in `PendingDispatchDrain` payload and use when creating replacement. | P2 (future) | Carried forward -- PR #43 CodeRabbit finding | OPEN |
| DW-NEXT-B-03 | Test behavioral coverage gap: the R2 tests are structural (reflection-based). They do not verify guard behavior -- (a) `TryAdd` rejection preventing concurrent drain overwrite, (b) `ContainsKey` guard suppressing `TryReplaceOnAtmCancel` when drain is active. A future ticket should add true behavioral tests using NT8 test-seam helpers or mock Account/Order objects. | P2 (future) | Carried forward -- VERIFY §7 test-name-deviation note | OPEN |
| DW-NEXT-B-04 | Pre-existing CCN debt: `OnOrderUpdate` lizard CCN=12 (budget <=8) and `DrainThenDispatch` lizard CCN=11 (budget <=8). Both methods pre-date the R2 pipeline (confirmed via independent git stash, L3 verifier). The R2 block added zero new branches to `OnOrderUpdate` (statement swap only); +1 to `DrainThenDispatch` from R2-F2 lambda `||` counted by lizard as a boolean branch. Both methods require extraction to reduce complexity to Jane Street strict standard. Target: dedicated complexity reduction epic. | P2 (future) | New -- L3 verifier git stash confirmation, ticket-1-verification.md Task 4b | OPEN |

---

## Notes (Round 2 append)

- DW-NEXT-B-01, DW-NEXT-B-02, DW-NEXT-B-03 carried forward unchanged. None addressed in Round 2 (all explicitly out of scope per architecture plan Section 8).
- DW-NEXT-B-04 is new. Root cause: pre-existing complexity debt confirmed by independent L3 git stash comparison. The R2 block did not introduce this debt. `AbortDrainOnFill` (new method) has CCN=2, fully within budget.
- No items closed this block.

---

*Deferred backlog updated: 2026-09-05 | ptt-plan-reviewer | Phase 5 | BWAVE-NEXT LaneBRepair-R2 Round 2*
