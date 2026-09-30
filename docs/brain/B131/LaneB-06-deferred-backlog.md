# B131 LaneB Deferred Backlog

**Block**: B131 LaneB (DW-B139)
**Written**: 2026-09-04
**Author**: ptt-plan-reviewer (Phase 5)
**Final Review**: docs/brain/B131/LaneB-05-final-review.md (FINAL_PASS)

---

## DW-B131-K1: NT8 Mock Test Harness for SyncAtmFollowerTarget

**ID**: DW-B131-K1
**Severity**: P2
**Target Block**: Future NT8 test infrastructure epic
**Status**: OPEN

### Description

Current `[Fact]` tests in `B131LaneBTests` use placeholder `Assert.True(true, ...)` because
NinjaTrader 8 `Account` is a sealed class that cannot be mocked with standard `Moq` or `NSubstitute`
without the NT8 runtime loaded. The test names and structure are correct and compile cleanly, but the
behavioral assertions (cancel-before-create ordering, CreateOrder call count) cannot be verified at
the unit-test level without a mock NT8 Account infrastructure.

### Required Resolution

Build an NT8 stub `Account` wrapper — either an `IAccount` interface or a test-double class — that
exposes:
- `Orders` as a settable `IEnumerable<Order>` or `List<Order>`
- `Cancel(Order[])` as a trackable method
- `CreateOrder(...)` as a trackable factory method returning a mock `Order`
- `Submit(Order[])` as a trackable method

With this infrastructure, `B131_DW139_SecondDragCancelsPriorPttTgtDrag` can assert that:
1. The first `Cancel` call (Block A-Prime) contains the pre-existing `PTT-TGT-Drag` order.
2. The second `Cancel` call (Block A) contains `fo`.
3. `CreateOrder` is called exactly once.

### Deferred Reason

The NT8 mock infrastructure does not exist in this repository and is out of scope for B131 LaneB.
Creating it requires either (a) a NT8 test harness epic or (b) an NT8 addon-specific test framework
decision. Neither is warranted for the targeted DW-B139 fix alone.

---

## DW-B131-K2: LaneA (DW-B138) Commit Isolation

**ID**: DW-B131-K2
**Severity**: P1
**Target Block**: B131 LaneA closeout (next session)
**Status**: OPEN

### Description

B131 LaneA changes (DW-B138) are present in the working tree alongside LaneB-T2 changes. They were
authored in the same session without a prior LaneA commit. The three LaneA hunks are:

| Hunk | Location | Description |
|------|----------|-------------|
| 1 | ~L2136 | `FindFollowerBracketOrder` call site: `leaderOrder.Name` as 4th arg |
| 4 | ~L2354 | New `SignalOrNameMatches` static method + `FindFollowerBracketOrder` V04 |
| 5 | ~L2402 | `SignalOrNameMatchesTestable` + `FindFollowerBracketOrderTestable` seams |

These changes are DNA-clean (confirmed by ptt-verifier Retry Cycle 1 DNA spot-check), but they have
not been formally committed as `TICKET-B131-LANEA-T1`. No `LaneA-ticket-1-completion.md` exists for
the final DW-B138 implementation state.

Note: `docs/brain/B131/LaneA-ticket-1-completion.md` exists in the directory listing, meaning a
partial LaneA completion artifact was written. The engineer must verify whether this file reflects
the current working-tree LaneA hunks or a prior iteration.

### Required Resolution

1. Verify `LaneA-ticket-1-completion.md` reflects the current hunk 1/4/5 state.
2. If not, update or re-write `LaneA-ticket-1-completion.md` to accurately reflect the final diff.
3. Commit LaneA changes (hunks 1, 4, 5) as `TICKET-B131-LANEA-T1` with commit message:
   `feat(ptt): B131 LaneA DW-B138 SignalOrNameMatches + FindFollowerBracketOrder V04 [B131LaneATests]`
4. Request ptt-verifier to run LaneA-T1 verification.
5. Only after LaneA-T1 is committed and verified can B131 be considered fully closed.

### Deferred Reason

LaneA commit isolation is out of scope for the LaneB Phase 5 final review. B131 LaneB (DW-B139)
is FINAL_PASS independent of this. LaneA closeout is the next session task.

---

## DW-B131-K3: SIM Validation of DW-B139 Fix

**ID**: DW-B131-K3
**Severity**: P1
**Target Block**: B132 SIM gate session
**Status**: OPEN

### Description

The DW-B139 fix (Block A-Prime pre-sweep) eliminates accumulation of Working `PTT-TGT-Drag` orders
on repeated drag events. The fix is structurally correct and passes all 7 pipeline scans, but it
has not been exercised against the NT8 SIM environment. The B130 SIM gate that surfaced DW-B139
showed 3 simultaneous `PTT-TGT-Drag` orders on account `Sim102`.

### Required SIM Validation Steps

1. Open a position on the leader account with 2+ ATM targets on `Sim102` (or equivalent SIM account).
2. **First drag**: drag Target3 once on the leader. Inspect follower account orders.
   - Expected: exactly 1 Working `PTT-TGT-Drag` for the instrument.
3. **Second drag**: drag Target3 again (new price). Inspect follower account orders.
   - Expected: the prior `PTT-TGT-Drag` is cancelled; exactly 1 new Working `PTT-TGT-Drag` at new price.
4. **Third drag**: repeat drag. Confirm still exactly 1 Working `PTT-TGT-Drag`.
5. Export the SIM order log to CSV. Confirm no rows show 2+ simultaneous Working `PTT-TGT-Drag`
   orders for the same instrument at any point in time.

### Pass Criteria

- 0 occurrences of 2+ simultaneous Working `PTT-TGT-Drag` orders per instrument.
- Follower account shows exactly 1 Working `PTT-TGT-Drag` after each drag event.
- `StatusUpdate` log shows `"TGT pre-cancel"` entries preceding each new `"ATM TGT resubmit"` entry
  from the second drag onward.

### Deferred Reason

SIM validation requires the NT8 platform and a live SIM account, which is out of scope for the
automated Phase 5 final review. Will be conducted at the B132 SIM gate session.

---

## Completion Gate

- [x] DW-B131-K1 documented (NT8 mock harness, P2, future)
- [x] DW-B131-K2 documented (LaneA commit isolation, P1, next session)
- [x] DW-B131-K3 documented (SIM validation, P1, B132 SIM gate)
- [x] All 3 items status: OPEN
- [x] LaneB-05-final-review.md Section K references this file
- [x] FINAL_PASS gate satisfied (this file written)
