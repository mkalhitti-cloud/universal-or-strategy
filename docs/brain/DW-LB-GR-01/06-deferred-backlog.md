# DW-LB-GR-01 -- Deferred Work Backlog
# Maintained by: ptt-plan-reviewer

---

## Block: DW-LB-GR-01

Date: 2026-09-07
Epic: RegisterBeRetrySlotIfNeeded guard fix (leaderCount -> targetsCount)
Final verdict: FINAL_PASS

### Deferred Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-LB-GR-01-D01 | F5 NinjaTrader 8 compilation gate -- ptt-sync-and-verify.ps1 confirmed 18/18 OK (0 MISMATCH). F5 press in NT8 is the mandatory final compile step. Director must confirm F5 was green before marking PIPELINE_COMPLETE. | P0 | Immediate | OPEN |
| DW-LB-GR-01-D02 | SIM gate -- BE session with Sim101 leader + Sim102/103 followers. Enter position, copy on, press BE-ALL. Verify OCO protection intact after BE fires (no spurious cancel). PASS before status OPEN -> PIPELINE_COMPLETE. | P0 | Immediate | OPEN |
| DW-LB-GR-01-D03 | Spec HTML update -- DW-LB-GR-01 status OPEN -> PIPELINE_COMPLETE, pending SIM gate result. | P1 | After SIM | OPEN |

### Rationale for Deferred Items

DW-LB-GR-01-D01: F5 compilation gate is a Director-owned human-action step requiring physical
access to a running NinjaTrader 8 instance. The software gate (ptt-sync-and-verify.ps1) confirmed
all 18 files are MD5-matched. F5 is the final binary compile step and cannot be automated or
remotely verified by any pipeline agent.

DW-LB-GR-01-D02: SIM gate requires a live simulation session with a configured leader (Sim101)
and follower accounts (Sim102/Sim103). The test verifies the fix under runtime conditions: that
BE-ALL on a follower with working PTT targets does NOT spuriously cancel OCO protection. This
is the production-fidelity acceptance test for the defect described in DW-LB-GR-01.

DW-LB-GR-01-D03: Spec HTML update is a documentation step contingent on D02 SIM gate result.
Cannot be marked PIPELINE_COMPLETE until the SIM confirms no spurious cancel in live conditions.

### Items Closed This Block

| Item | Closed By | Status |
|------|-----------|--------|
| DW-LB-GR-01 (from BWAVE-REFACTOR/06-deferred-backlog.md L74) | Logic fix at L6118 + VERIFY_PASS (2026-09-07) | CLOSED |

### Prior BWAVE-REFACTOR Open Items (NOT closed by this block)

The following items from `docs/brain/BWAVE-REFACTOR/06-deferred-backlog.md` remain OPEN.
This block does not affect them.

| ID | Status | Rationale |
|----|--------|-----------|
| DW-LB-01 | OPEN | ActiveOrders .ToList() -- separate epic, deferred DW-NEXT-A-07. |
| DW-LB-02 | OPEN | Features/*.cs CCN violations -- Lane C scope. |
| DW-LB-03 | OPEN | BWAVE-NEXT LaneBRepair backlog items. |
| DW-LB-04 | OPEN | ResolveNullFollowerSlot null return -- future nullability refactor. |
| DW-LB-05 | OPEN | Misleading test name ExtractLegSuffix_NoDigit_ReturnsNull. |
| DW-LB-06 | OPEN | BWAVE-REFACTOR Lane B F5 gate (separate from DW-LB-GR-01-D01). |
| DW-LB-07 | OPEN | xUnit2004 pre-existing warning in B131Tests.cs. |
| DW-LB-AQ-01 | OPEN | Missing File.Exists guard in BwaveRefactorLaneBTests.cs. |
| DW-LB-AQ-02 | OPEN | Misleading test name ExtractLegSuffix_NoDigit_ReturnsNull (same as DW-LB-05). |
| DW-LB-AQ-03 | OPEN | GetSeamMethod() null guard missing in BwaveRefactorLaneBTests.cs. |
| DW-LB-AQ-04 | OPEN | Duplicate test IsImmediateBeEligible_ZeroTickSize in BwaveRefactorLaneBTests.cs. |
| DW-LB-CA-01 | OPEN | Hard-coded bin\Debug path in BwaveRefactorLaneBTests.cs. |
