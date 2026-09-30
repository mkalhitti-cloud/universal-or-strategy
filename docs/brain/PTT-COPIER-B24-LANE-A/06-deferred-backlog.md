# PTT-COPIER-B24-LANE-A — Deferred Backlog
# Author: ptt-plan-reviewer (Phase 5 Final Review)
# Block:  PTT-COPIER-B24
# Lane:   A
# Date:   2026-07-17

---

## Purpose

This file records all work items deferred out of PTT-COPIER-B24-LANE-A and carries forward
all prior OPEN items from the B23-LANE-C backlog.

It is a mandatory output of Phase 5 (Final Review). FINAL_PASS is blocked without this file.

---

## Current-Block Entries (PTT-COPIER-B24-LANE-A)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-LEADER-CASTNULL-01 | Cold-start leader account wiring: WPF ComboBox.SelectedItem cast returns null at NT8 inject time; text-fallback via `Account.All.FirstOrDefault(a => string.Equals(a.Name, accountCombo.Text, StringComparison.OrdinalIgnoreCase))` added to `WireLeaderAccount` in `TradeCopierAddOn.cs`. Resolved by T1 (5 insertions, 1 deletion). All 7 scans PASS. VERIFY_PASS granted. | P0 | B24 (this block) | **CLOSED** |
| DW-B24-LANE-A-01 | Pre-existing `System.Windows.Application.Current.Dispatcher.InvokeAsync` calls at `TradeCopierAddOn.cs` lines 251 and 293. Flagged by plan-review ADV-01. Pattern is listed as BANNED by NT8-042 (`System.Windows.Application.Current.Dispatcher.InvokeAsync` — all 3 Dispatcher paths fail CS0117/CS1061 in NT8 AddOn context). NOT introduced by B24-LANE-A T1; outside the T1 write-set. Pending investigation: whether `Dispatcher.BeginInvoke` via `System.Windows.Threading.Dispatcher` is the correct replacement (noted as UNCONFIRMED in NT8-042 SAFE section). | P1 | B25 or future | OPEN |

---

## Carried Forward — Prior OPEN Items from PTT-COPIER-B23-LANE-C

These items were OPEN at the end of B23-LANE-C. Neither is in the B24-LANE-A write-set.
Status is unchanged.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B23-LANE-C-01 | Add short-direction price trigger `[Fact]` test (`PendingBe_Armed_FiresAtPriceTarget_Short`): symmetric coverage for the `last <= target` branch in `OnPendingBeAccountUpdate`. Long-direction test (`last >= target`) is covered; short-direction is logically symmetric but untested. | P2 | B25 or future | OPEN |
| DW-B23-LANE-C-02 | Pre-existing `return null` occurrences at `CopyEngine.cs` lines 653, 1059, 1065, 1118 — confirmed outside the B23-LANE-C changed method by SCAN-03. Not introduced by any lane in B23 or B24. JS-002 compliance sweep candidates for a future dedicated block. | P2 | future | OPEN |

---

## Scope Notes

- `DW-B24-LEADER-CASTNULL-01` is **CLOSED** — defect fixed and verified in this block.
- `DW-B24-LANE-A-01` is a new P1 item. The banned calls at lines 251/293 pre-date B24-LANE-A.
  They are not causing a build error in current NT8 context (possibly the specific variant used
  does not trigger CS0117/CS1061 in this NT8 build). However they match the BANNED pattern in
  NT8-042 and must be addressed in a future block.
- `DW-B23-LANE-C-01` and `DW-B23-LANE-C-02` remain P2 — neither is a P0 or P1 blocker.
  Both items predate B24 and are tracked for future sprint allocation.

---

## Prior OPEN Items Closed by PTT-COPIER-B24-LANE-A

| Defect ID | Description | Resolution | Status |
|-----------|-------------|------------|--------|
| DW-B24-LEADER-CASTNULL-01 | Cold-start leader account: `WireLeaderAccount` cast-null at inject time | Added text-fallback `Account.All.FirstOrDefault` in `TradeCopierAddOn.cs` (T1, 3 lines); CYC 4→6; 7/7 scans PASS; BUILD_PASS + VERIFY_PASS | **CLOSED** |
