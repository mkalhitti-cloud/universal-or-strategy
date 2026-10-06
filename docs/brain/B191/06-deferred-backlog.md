# B191 Deferred Backlog

**Epic**: B191 -- QAll2t Per-Account Qty Fix (formalize) + DW-B187-01 Close
**Author**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-07-15
**Preceding backlog**: docs/brain/B190/06-deferred-backlog.md
**Status**: Authoritative deferred backlog for the NEXT epic block (B192+)

---

## Purpose

This document records all deferred work items that remain open after B191 completion.
It supersedes docs/brain/B190/06-deferred-backlog.md as the authoritative carry-forward list.
Items closed by B191 are listed with CLOSED status for audit trail completeness.

**P0 = Compile-blocking or P0 DNA violation. Must be resolved before shipping.**
**P1 = Architecture risk. Must be resolved before next major feature wave.**
**P2 = Quality / testing gap. Track and address in near-term pipeline.**
**P3 = Low-risk cleanup / informational. Address when convenient.**

---

## B191 Block -- 2026-07-15

### Items Closed by B191

| DW ID | Description | Priority | Closed By | Closing Artifact |
|-------|-------------|----------|-----------|-----------------|
| DW-B191-QTY-01 | QAll2t (OnInstrQAll2tClick) passed forcedTargets -- built from leader panel qty -- verbatim to all accounts. Accounts with smaller positions (e.g. PA-01 at 5c) received targets sized for the leader (e.g. PAAPEX10 at 20c). | P0 | B191-T1 (commit 365b8779) | ticket-1-completion.md + ticket-1-verification.md (VERIFY_PASS) |
| DW-B187-01 | followerPosQty=0 hardcode in ExecuteFollowers (B187-T2 regression). ResolveFollowerTargets short-circuited before ScaleLeaderTargets, leaving ScaleLeaderTargets permanently unreachable via normal code path. | P3 | B191-T3 (PttGlobalQuickExit.cs line 219) | ticket-3-completion.md + ticket-3-verification.md (VERIFY_PASS) |

### New Items Opened by B191

None. All B191 scans returned zero DNA violations. No new architectural debt discovered
during implementation or verification.

---

## Items Carried Forward from B190 (Remain Open)

The following item from docs/brain/B190/06-deferred-backlog.md was not addressed in B191.
It is the authoritative carry-forward item for B192+.

---

### DW-B183-03: IsLaneEEligible CYC=8 Frozen

```
Priority: P1
Description: IsLaneEEligible in CopyEngine.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to IsLaneEEligible without prior helper extraction is an automatic P1 JS-066
  violation. Not modified in B183-B191.
  Future engineers modifying Lane E eligibility MUST extract a helper method first.
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions
  to IsLaneEEligible)
Target Block: When Lane E is next modified
Status: OPEN (CARRIED FORWARD from B183 via B184, B185, B186, B187, B188, B189, B190, B191)
Owner: Director
```

---

## Summary by Priority

| Priority | Count | Items |
|----------|-------|-------|
| P0 | 0 | (DW-B191-QTY-01 CLOSED by B191) |
| P1 | 1 | DW-B183-03 |
| P2 | 0 | -- |
| P3 | 0 | (DW-B187-01 CLOSED by B191) |

**Total open items**: 1
**Items closed by B191**: 2 (DW-B191-QTY-01, DW-B187-01)
**New items opened by B191**: 0

---

## Recommended Next Block Priorities

1. **DW-B183-03** (P1): IsLaneEEligible CYC freeze -- address when Lane E eligibility
   logic is next modified. No action required unless a branch addition is planned.

---

*Written by: ptt-plan-reviewer*
*Epic: B191*
*Phase: 5 (Final Review)*
*Required for FINAL_PASS gate (role definition mandate).*
*Supersedes: docs/brain/B190/06-deferred-backlog.md (as authoritative carry-forward list)*
