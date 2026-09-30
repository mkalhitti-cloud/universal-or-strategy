# B171 Deferred Backlog

**Block:** B171
**Written by:** ptt-plan-reviewer (Phase 5)
**Last updated:** B171 final review

---

## B171 Items

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B171-01 | B171 | DUPLICATE-STP-DRAG-ON-REPEATED-TGT-DRAG — `ExecutePhaseCStopReplacement` had no `CancelLiveCollateralStop` before creating replacement stop. Each target drag stacked a new `PTT-STP-Drag` on top of prior ones. Fixed: suffix derivation + `CancelLiveCollateralStop(acc, fo, "PTT-STP-Drag-"+suffix)` added before `CreateFollowerReplacementStop`. | P0 | **CLOSED** — B171-1 VERIFY_PASS |
| DW-B171-02 | B171 | fo=NULL-ON-STOP-DRAG-WHEN-STP-IS-PTT-NAMED — `SubmitReplacementStopOrder` created stop with unsuffixed name `"PTT-STP-Drag"`; `IsDragNameMatch` checks `"PTT-STP-Drag-"+legSuffix`. Exact match failed → `fo = NULL` → stop drag not synced. Fixed: all three `"PTT-STP-Drag"` literals in `SubmitReplacementStopOrder` changed to `"PTT-STP-Drag-"+suffix`; SCAN-02 confirmed 0 unsuffixed literals remain in `SubmitReplacementStopOrder`. | P0 | **CLOSED** — B171-2 VERIFY_PASS |

---

## Prior Open Items (carried forward)

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B170-SIM | B170 | SIM GATE: Director drags target on leader — confirm follower stop preserved (not missing) after drag. DW-B170-01 live regression verify. | P0 | **PENDING SIM GATE** — B171 fix deployed (unsuffixed stop name was the root cause of the stop-drag sync failure). SIM verification required; close upon Director SIM confirmation per B171-orchestrator-prompt.md §SIM GATE. |
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

**B171 changes summary:**
- DW-B171-01 CLOSED: `ExecutePhaseCStopReplacement` now derives `suffix = bracketIdx.ToString()`, validates price with `if (stp <= 0.0)` guard (EC-1 protection, Variant E), then calls `CancelLiveCollateralStop(acc, fo, "PTT-STP-Drag-"+suffix)` before `CreateFollowerReplacementStop`. Duplicate stop accumulation on repeated target drags is eliminated.
- DW-B171-02 CLOSED: `SubmitReplacementStopOrder` now creates stop as `"PTT-STP-Drag-"+suffix`. `IsDragNameMatch` match succeeds → `fo != NULL` on stop drag events. All three mandated literals in `SubmitReplacementStopOrder` suffixed; SCAN-02 confirmed 0 unsuffixed standalone literals remain in that method.
- `string suffix` threaded end-to-end: `ExecutePhaseCStopReplacement` → `CreateFollowerReplacementStop` → `SubmitReplacementStopOrder` → `CreateOrder` name argument.
- 5 xUnit tests added covering all spec scenarios including IsDragNameMatch dual-assertion (Test 4).
- ptt-sync.ps1: 18 linked, 0 errors.

**DW-B170-SIM updated to PENDING SIM GATE:** B171 directly addresses the root cause of the stop-drag sync failure that DW-B170-SIM was tracking (the unsuffixed `"PTT-STP-Drag"` name caused `IsDragNameMatch` to return false → `fo = NULL`). Code fix deployed and verified. Director SIM run per B171-orchestrator-prompt.md §SIM GATE required to close.

**Remaining highest-risk open item:** B167-DEFER-02 (P1): follower ATM brackets never placed after leader fill when `fo = NULL`. Blocks DW-B169-K1 Sim102 path. Requires dedicated block.
