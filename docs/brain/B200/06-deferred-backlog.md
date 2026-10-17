# B200+B201 Deferred Backlog

**Epic**: PTT-COPIER-B200 + B201
**Created**: T4 completion (ptt-engineer, Phase 4a)
**Author**: ptt-engineer (T4)
**Preceding backlog**: docs/brain/B199/06-deferred-backlog.md (18 open items; superseded by this document)
**Status**: Authoritative deferred backlog for the NEXT epic block (B202+)

---

## Purpose

This document records all deferred work items that remain open after B200+B201 completion.
It supersedes docs/brain/B199/06-deferred-backlog.md as the authoritative carry-forward list.
Items closed by B200+B201 are listed with CLOSED status for audit trail completeness.

**P0 = Compile-blocking or P0 DNA violation. Must be resolved before shipping.**
**P1 = Architecture risk or JS-066 violation. Must be resolved before next major feature wave.**
**P2 = Quality / testing gap. Track and address in near-term pipeline.**
**P3 = Low-risk cleanup / informational. Address when convenient.**

---

## Items Closed by B200+B201

**0 items closed by B200+B201.**

None of the 18 open items from B199 were directly addressed in B200+B201 scope.
B200 was a test formalisation + direct repair audit. B201 was a targeted cancel-candidate fix.
No P1 SIM gates were scheduled for B200+B201 pipeline.

---

## New Items Opened by B200+B201

---

### DW-B200-LANE-E-COPY-ENABLED (P2) — OPEN

```
Priority: P2
Description: TryLaneEHook is called from OnFollowerPositionUpdate and is not gated on
  _isCopyEnabled. When copy is disabled, Lane E can still dispatch follower orders.
  Low correctness risk: PassesDailyCapCheck is the outer guard; _isCopyEnabled may be
  redundant here. However, for defensive parity with Lane D, Lane E should check
  _isCopyEnabled before dispatching.
  Deferred pending further analysis of Lane E gate logic vs Lane D.
Blocking: No
Target Block: B202+
Status: OPEN (opened B200 T4)
Owner: Director
```

---

### DW-B201-OPTION-B (P3) — OPEN

```
Priority: P3
Description: Alternative fix for B201-ENTRY-CANCEL-01: rename the follower "Entry" dispatch
  name to "PTT-Copy-Entry" at dispatch time (in TryLaneDDispatchFollower and equivalent Lane
  paths). This avoids the need for the isFollower guard entirely, as "PTT-Copy-Entry" != "Entry".
  Simpler long-term; eliminates the DW-B93 comment dependency.
  Deferred pending Director decision on naming convention for follower ATM copies.
Blocking: No (B201 Option A fix is in place and working)
Target Block: B202+ (Director decision required)
Status: OPEN (opened B200 T4, Option A already applied)
Owner: Director
```

---

### DW-B199-H2-CLEANUP-01 (P3) — OPEN

```
Priority: P3
Description: B199 H2 divide remnant logic in SendCopyWithAtm area — conditional cleanup.
  Components still present in CopyEngine.cs (confirmed by T4 grep):
    - _atmTemplateGroupQty ConcurrentDictionary (3 references found)
    - TryGetAtmTemplateGroupCount method (2 references found)
  These are benign no-ops when _atmTemplateGroupQty dict is empty.
  B199 T4 confirmed H1 (qty passes unchanged; H2 path never executes).
  Recommended action: remove when SendCopyWithAtm is next modified.
Blocking: No (code is a benign no-op; no correctness issue)
Target Block: B202+ (when SendCopyWithAtm is next touched)
Status: OPEN (carry-forward from B199; H2 code still present as of B200 T4 verification)
Owner: Director
Source: docs/brain/B198/ticket-2a-sim-evidence.md, docs/brain/B199/06-deferred-backlog.md
```

---

## Items Carried Forward Unchanged from B199

All 18 open items from docs/brain/B199/06-deferred-backlog.md carry forward unchanged.
None were addressed in B200+B201 scope. Full text of each item is in the B199 backlog.

| DW ID | Priority | Description (brief) | Status |
|-------|----------|---------------------|--------|
| DW-B198-SIMSTP-SIM-01 | P1 | B198 SIM gate not yet run (4 SIM scenarios) | OPEN |
| DW-B183-03 | P1 | IsLaneEEligible CYC=8 frozen — future branch addition requires helper extraction first | OPEN |
| DW-B167-DEFER-03 | P1 | Combined B161-B166 SIM gate | OPEN |
| DW-B167-DEFER-04 | P1 | B167 SIM gate (prerequisite for B168/B169) | OPEN |
| DW-B170-SIM | P1 | B170 SIM gate (Accepted window fix live regression verify) | OPEN |
| DW-B197-SIM-01 | P1 | B197 SIM gate (4 SIM scenarios) | OPEN |
| DW-B198-GUARDIAN-01 | P2 | Guardian script TradeCopierPanel BrushTeal check failure (pre-existing) | OPEN |
| DW-B192-SINST-01 | P2 | SetInstrument() CYC=8 frozen — future branch addition requires helper extraction first | OPEN |
| DW-B159-K2 | P2 | 14 xUnit tests for BE path (test infrastructure pending) | OPEN |
| DW-B159-K3 | P2 | Attempt cap hardcoded at 3 in TryArmBePriceRetry | OPEN |
| DW-B159-K4 | P2 | intendedBePrice sourcing dependency unresolved | OPEN |
| DW-B167-DEFER-01 | P2 | ECA-1 double-position race (monitor-only) | OPEN |
| DW-B197-03 | P2 | Pre-existing DwB15701SlateReadyTests failures (2 tests) | OPEN |
| DW-B193-01 | P3 | OnQuickAllBufferChanged old label strings | OPEN |
| DW-B197-01 | P3 | Stale CCN comment at IsPttDragOrphanCancellable | OPEN |
| DW-B197-02 | P3 | T4 Fact names misleading (B197Tests.cs) | OPEN |
| DW-B197-04 | P3 | TryCancelSingleStaleGtcExit not in plan CYC table (informational only) | OPEN |
| DW-B199-H2-CLEANUP-01 | P3 | (see full entry above — updated status from B199) | OPEN |

**Carry-forward note**: Items above are reproduced from docs/brain/B199/06-deferred-backlog.md
without modification. See that document for full text of each item.

---

## Summary by Priority

| Priority | Count | DW IDs |
|----------|-------|--------|
| P0 | 0 | (None) |
| P1 | 6 | DW-B198-SIMSTP-SIM-01, DW-B183-03, DW-B167-DEFER-03, DW-B167-DEFER-04, DW-B170-SIM, DW-B197-SIM-01 |
| P2 | 9 | DW-B198-GUARDIAN-01, DW-B192-SINST-01, DW-B159-K2, DW-B159-K3, DW-B159-K4, DW-B167-DEFER-01, DW-B197-03, DW-B200-LANE-E-COPY-ENABLED (new), DW-B199-H2-CLEANUP-01 (promoted from B199 P3) |
| P3 | 5 | DW-B193-01, DW-B197-01, DW-B197-02, DW-B197-04, DW-B201-OPTION-B (new) |

**Total open items: 20** (18 carried forward + 2 new items DW-B200-LANE-E-COPY-ENABLED and DW-B201-OPTION-B)
**Note**: DW-B199-H2-CLEANUP-01 was P3 in B199; remains at P3 with updated status confirming code still present.
**Items closed by B200+B201: 0**
**New items opened by B200+B201: 2** (DW-B200-LANE-E-COPY-ENABLED, DW-B201-OPTION-B)
**Items carried forward from B199: 18**

---

## Recommended Next Block Priorities (B202+)

1. **Combined SIM Gate** (DW-B198-SIMSTP-SIM-01, DW-B197-SIM-01, DW-B167-DEFER-03, DW-B167-DEFER-04, DW-B170-SIM -- P1):
   Schedule a comprehensive SIM session covering B197, B198, B167, and B170 scenarios.
   This single SIM session can close up to 5 P1 items.

2. **Lane E Copy-Enabled Gate** (DW-B200-LANE-E-COPY-ENABLED -- P2):
   Add `_isCopyEnabled` guard to TryLaneEHook for defensive parity with Lane D.

3. **Guardian Script Fix** (DW-B198-GUARDIAN-01 -- P2):
   Update verify_critical_symbols.ps1 pattern for TradeCopierPanel BrushTeal check.

4. **H2 Cleanup** (DW-B199-H2-CLEANUP-01 -- P3):
   Remove _atmTemplateGroupQty / TryGetAtmTemplateGroupCount when SendCopyWithAtm is next touched.

5. **B201 Option B Decision** (DW-B201-OPTION-B -- P3):
   Director decision on follower "Entry" dispatch naming convention.

---

*Written by: ptt-engineer (T4)*
*Epic: B200+B201*
*Phase: 4a (Ticket Execution)*
*Supersedes: docs/brain/B199/06-deferred-backlog.md (as authoritative carry-forward list)*
*Required for FINAL_PASS gate.*
