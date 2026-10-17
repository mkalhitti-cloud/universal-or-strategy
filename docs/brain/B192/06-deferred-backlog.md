# B192 Deferred Backlog

**Epic**: B192 -- GlobalQuickAll MGC/GC Tick Default Bug Fix
**Author**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-07-15
**Preceding backlog**: docs/brain/B191/06-deferred-backlog.md
**Status**: Authoritative deferred backlog for the NEXT epic block (B193+)

---

## Purpose

This document records all deferred work items that remain open after B192 completion.
It supersedes docs/brain/B191/06-deferred-backlog.md as the authoritative carry-forward list.
Items closed by B192 are listed with CLOSED status for audit trail completeness.

**P0 = Compile-blocking or P0 DNA violation. Must be resolved before shipping.**
**P1 = Architecture risk. Must be resolved before next major feature wave.**
**P2 = Quality / testing gap. Track and address in near-term pipeline.**
**P3 = Low-risk cleanup / informational. Address when convenient.**

---

## B192 Block -- 2026-07-15

### Items Closed by B192

None. B192 was a targeted bug fix (3 files, 3 methods). No items from the B191 open backlog
fell within B192 scope. DW-B183-03 (`IsLaneEEligible` CYC freeze) was not modified in B192
and remains open.

### New Items Opened by B192

None. All three B192 changes are surgical and confined to their respective methods:
- `InstrumentDefaults.GetQuickTicks` (PttQuickExit.cs) -- add 4 if-guards, no structural debt
- `CopyEngine.ResetGlobalQuickAll` (CopyEngine.cs) -- new CYC=1 method, no structural debt
- `TradeCopierPanel.SetInstrument` (TradeCopierPanel.cs) -- single-line addition, no structural debt

All 7 scans returned zero violations. No new architectural debt introduced.

---

## Items Carried Forward from B191 (Remain Open)

The following item from docs/brain/B191/06-deferred-backlog.md was not addressed in B192.
It is the authoritative carry-forward item for B193+.

---

### DW-B183-03: IsLaneEEligible CYC=8 Frozen

```
Priority: P1
Description: IsLaneEEligible in CopyEngine.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to IsLaneEEligible without prior helper extraction is an automatic P1 JS-066
  violation. Not modified in B183-B192.
  Future engineers modifying Lane E eligibility MUST extract a helper method first.
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions
  to IsLaneEEligible)
Target Block: When Lane E is next modified
Status: OPEN (CARRIED FORWARD from B183 via B184, B185, B186, B187, B188, B189, B190, B191, B192)
Owner: Director
```

---

## Summary by Priority

| Priority | Count | Items |
|----------|-------|-------|
| P0 | 0 | -- |
| P1 | 1 | DW-B183-03 |
| P2 | 0 | -- |
| P3 | 0 | -- |

**Total open items**: 1
**Items closed by B192**: 0
**New items opened by B192**: 0

---

## Recommended Next Block Priorities

1. **DW-B183-03** (P1): IsLaneEEligible CYC freeze -- address when Lane E eligibility
   logic is next modified. No action required unless a branch addition is planned.

---

*Written by: ptt-plan-reviewer*
*Epic: B192*
*Phase: 5 (Final Review)*
*Required for FINAL_PASS gate (role definition mandate).*
*Supersedes: docs/brain/B191/06-deferred-backlog.md (as authoritative carry-forward list)*
