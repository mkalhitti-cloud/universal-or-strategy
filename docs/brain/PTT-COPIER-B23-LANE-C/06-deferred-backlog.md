# PTT-COPIER-B23-LANE-C — Deferred Backlog
# Author: ptt-plan-reviewer (Phase 5 Final Review)
# Block:  PTT-COPIER-B23
# Lane:   C
# Date:   2026-07-16

---

## Purpose

This file records all work items deferred out of PTT-COPIER-B23-LANE-C.
It is a mandatory output of Phase 5 (Final Review). FINAL_PASS is blocked
without this file.

---

## Entries from PTT-COPIER-B23-LANE-C

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B23-LANE-C-01 | Add short-direction price trigger [Fact] test (`PendingBe_Armed_FiresAtPriceTarget_Short`): symmetric coverage for `last <= target` branch in `OnPendingBeAccountUpdate`. Long direction covered; short direction is logically symmetric but untested. | P2 | B24 or future | OPEN |
| DW-B23-LANE-C-02 | Pre-existing `return null` occurrences at `CopyEngine.cs` lines 653, 1059, 1065, 1118 — confirmed outside the changed method by SCAN-03. Not introduced by this lane. JS-002 compliance sweep candidates for a future dedicated block. | P2 | future | OPEN |

---

## Scope Notes

- Both items above are P2 — neither is a P0 or P1 blocker.
- DW-B23-LANE-C-01 is a coverage gap only; the trigger logic itself is correct and
  arithmetically symmetric (verified by inspection).
- DW-B23-LANE-C-02 items are pre-existing and were present before Block B23 began.
  They are not regressions from this lane.

---

## Prior OPEN Items Closed by This Lane

None. PTT-COPIER-B23-LANE-C targeted only DW-B22-BE-TRIGGER-01 (P1), which is now
fully resolved: the dollar-PnL trigger (`if (e.Value < 0) return;`) has been replaced
with a price-based trigger (`last >= target` long / `last <= target` short) that is
immune to PA prop account commission deductions. This defect is **CLOSED**.

---

## Defects Resolved

| Defect ID | Description | Resolution | Status |
|-----------|-------------|------------|--------|
| DW-B22-BE-TRIGGER-01 | Armed BE trigger fires on dollar PnL; unreliable on PA prop accounts due to commission deduction at entry | Replaced with price-based trigger in `OnPendingBeAccountUpdate`; 2 [Fact] tests added; CYC=8; BUILD_PASS + VERIFY_PASS | **CLOSED** |
