# Deferred Backlog — BWAVE-NEXT Lane B Repair

**Epic**: BWAVE-NEXT LaneBRepair
**Block**: BWAVE-NEXT-LaneBRepair
**Date recorded**: 2026-09-05
**Written by**: ptt-plan-reviewer (Phase 5 Final Review)

---

## Block: BWAVE-NEXT-LaneBRepair (2026-09-05)

| ID | Description | Priority | Source |
|----|-------------|----------|--------|
| DW-NEXT-B-01 | Drain key is acct-only — second instrument on same account overwrites first drain intent. Extend key to `acct.Name + "|" + instrument.FullName` when multi-instrument trading is added. | P2 (future) | PR #43 cubic finding |
| DW-NEXT-B-02 | GTC/Day TIF and native-ATM Entry name not preserved in `SubmitEntryDirect` replacement. Carry original TIF + name in `PendingDispatchDrain` payload and use when creating replacement. | P2 (future) | PR #43 CodeRabbit finding |

---

## Notes

- No additional deferred items were identified during Phase 5 final review.
- Both DW-NEXT-B-01 and DW-NEXT-B-02 were explicitly out-of-scope for the repair commit as stated in the mission brief Out of Scope table.
- Both are P2 (future) items with no immediate safety or correctness risk at current single-instrument operational scope.
- Status of both items: **OPEN** — not yet scheduled for a specific block.
- No prior deferred backlog existed for this epic; this is the initial record.

---

*Deferred backlog written: 2026-09-05 | ptt-plan-reviewer | Phase 5 | BWAVE-NEXT LaneBRepair*
