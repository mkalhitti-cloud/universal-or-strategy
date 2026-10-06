# B190 Deferred Backlog

**Epic**: B190 -- Per-Instrument Tick Defaults + Spinner Layout Fix + BE Default=0 + Deferred Closures
**Author**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-07-11
**Preceding backlog**: `docs/brain/B189/06-deferred-backlog.md`
**Status**: Authoritative deferred backlog for the NEXT epic block (B191+)

---

## Purpose

This document records all deferred work items that remain open after B190 completion.
It supersedes `docs/brain/B189/06-deferred-backlog.md` as the authoritative carry-forward list.
Items closed by B190 are listed with CLOSED status for audit trail completeness.

**P0 = Compile-blocking or P0 DNA violation. Must be resolved before shipping.**
**P1 = Architecture risk. Must be resolved before next major feature wave.**
**P2 = Quality / testing gap. Track and address in near-term pipeline.**
**P3 = Low-risk cleanup / informational. Address when convenient.**

---

## B190 Block -- 2026-07-11

### Items Closed by B190

| DW ID | Description | Closed By | Closing Artifact |
|-------|-------------|-----------|-----------------|
| DW-B183-05 | RULES_CATALOG.md truncated at JS-041 (JS-042..JS-110 missing including JS-066 and JS-081) | B190-T4 | ticket-4-verification.md -- VERIFY_PASS; JS-066 at L2444, JS-081 at L2927, JS-110 at L3951 confirmed. UTF-8 no-BOM. |
| DW-B185-BE-ALL-SCOPE-01 | Spec HTML B39 scope row described old "All instruments with open positions" (code had been instrument-scoped since B185-T1) | B190-T5 | ticket-5-verification.md -- VERIFY_PASS; B39 scope row at L17876 updated, adjacent note at L17727 updated, closed DW card at L42035-42057. |
| DW-B183-02 | FlatGuardLoop test `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard` had stale [Fact(Skip="B180-2: method removed")] | B190-T6 | ticket-6-verification.md -- VERIFY_PASS; [Fact] at CopyEngineTests.cs:8812. Test passes: 1/0/0. |
| DW-B183-01 | Slate Gate test coverage described as absent; B190-T6 audit confirmed 5 [Fact] passing + 3 [Fact(Skip="NT8-runtime")] correctly skipped in Bug2FollowerSlateReadyTests (L8277-8444) | B190-T6 audit + Ph5 sign-off | ticket-6-verification.md VERIFY_PASS + Ph5 final review. 5+3 count vs spec 6+2 is a pre-existing documentation artifact. Gate semantics (ConcurrentDictionary TryGetValue three-state logic) fully covered. Ph5 formally closes. |

### New Items Opened by B190

None. All B190 scans returned zero violations. No new architectural debt discovered during implementation or verification.

---

## Items Carried Forward from B189 (Remain Open)

The following items from `docs/brain/B189/06-deferred-backlog.md` were not addressed in B190.
They are the authoritative carry-forward list for B191+.

---

### DW-B183-03: IsLaneEEligible CYC=8 Frozen

```
Priority: P1
Description: IsLaneEEligible in CopyEngine.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to IsLaneEEligible without prior helper extraction is an automatic P1 JS-066
  violation. Not modified in B183-B190.
  Future engineers modifying Lane E eligibility MUST extract a helper method first.
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions
  to IsLaneEEligible)
Target Block: When Lane E is next modified
Status: OPEN (CARRIED FORWARD from B183 via B184, B185, B186, B187, B188, B189, B190)
Owner: Director
```

---

### DW-B187-01: `followerPosQty` Parameter Path Review in `ResolveFollowerTargets`

```
Priority: P3
Description: T2 (B187) deleted the [DW-B115-DIAG] block in ExecuteFollowers. The DIAG
  block computed _fPosQty (follower position quantity) via a foreach over follower.Positions.
  The architect's plan stated _fPosQty was only used within the DIAG block. This was
  incorrect: _fPosQty was also passed as the 3rd argument (followerPosQty) to
  ResolveFollowerTargets immediately after the DIAG block.

  The engineer made the minimal fix: replaced _fPosQty with 0.

  With followerPosQty=0, ResolveFollowerTargets short-circuits via the guard
  `if (leaderTargets.Count == 0 || followerPosQty <= 0) return followerSnapshot;`
  and returns followerSnapshot directly, bypassing the ScaleLeaderTargets call.

  The verifier (T2) confirmed this is behaviorally safe: the dominant code path (non-empty
  followerSnapshot) is unaffected. The scaling fallback path (ScaleLeaderTargets) was also
  inactive pre-T2 because _fPosQty in the DIAG loop was only set if a matching position
  was found, and the DIAG loop did not guard ResolveFollowerTargets.

  A future epic should determine:
  (A) Whether ScaleLeaderTargets is dead code that should be removed.
  (B) Whether followerPosQty should be sourced from a proper, non-DIAG data path.
  (C) Whether the followerPosQty parameter itself should be removed if ScaleLeaderTargets
      is confirmed dead.

Blocking: No -- followerSnapshot is correctly returned; no functional regression.
Target Block: B191+ (low urgency -- dead path, not broken path)
Status: OPEN (CARRIED FORWARD from B187 via B188, B189, B190)
Owner: Director
```

---

## Summary by Priority

| Priority | Count | Items |
|----------|-------|-------|
| P1 | 1 | DW-B183-03 |
| P2 | 0 | (DW-B183-01 and DW-B183-02 CLOSED by B190) |
| P3 | 1 | DW-B187-01 |

**Total open items**: 2
**Items closed by B190**: 4 (DW-B183-05, DW-B185-BE-ALL-SCOPE-01, DW-B183-02, DW-B183-01)
**New items opened by B190**: 0

---

## Recommended Next Block Priorities

1. **DW-B183-03** (P1): IsLaneEEligible CYC freeze -- address when Lane E is next modified. No action required unless Lane E eligibility logic changes.
2. **DW-B187-01** (P3): ResolveFollowerTargets followerPosQty path review -- address when B115 scaling logic is revisited or when ScaleLeaderTargets dead-code cleanup is in scope.

---

*Written by: ptt-plan-reviewer*
*Epic: B190*
*Phase: 5 (Final Review)*
*Required for FINAL_PASS gate (role definition mandate).*
*Supersedes: docs/brain/B189/06-deferred-backlog.md (as authoritative carry-forward list)*
