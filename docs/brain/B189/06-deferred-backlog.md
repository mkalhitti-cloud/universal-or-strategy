# B189 Deferred Backlog

**Epic**: B189 -- BE Direction Fix + BE Negative Range + Quick ALL 3T Fix + Quick2t/QAll2t Spinners
**Author**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-07-10
**Preceding backlog**: `docs/brain/B188/06-deferred-backlog.md`
**Status**: Authoritative deferred backlog for the NEXT epic block (B190+)

---

## Purpose

This document records all deferred work items that remain open after B189 completion.
It supersedes `docs/brain/B188/06-deferred-backlog.md` as the authoritative carry-forward list.
Items closed by B189 are listed with CLOSED status for audit trail completeness.

**P0 = Compile-blocking or P0 DNA violation. Must be resolved before shipping.**
**P1 = Architecture risk. Must be resolved before next major feature wave.**
**P2 = Quality / testing gap. Track and address in near-term pipeline.**
**P3 = Low-risk cleanup / informational. Address when convenient.**

---

## B189 Block -- 2026-07-10

### Items Closed by B189

| DW ID | Description | Closed By | Closing Artifact |
|-------|-------------|-----------|-----------------|
| DW-B187-02 | xUnit2004 warning at B131Tests.cs:165 (`Assert.Equal(true, ...)`) | B189-T5 (1-line fix) | ticket-5-verification.md -- VERIFY_PASS; build 0 warnings confirmed. |

### New Items Opened by B189

No new items. B189 verification surfaced no new violations, no architectural gaps, and no
new DW candidates.

---

## Items Carried Forward from B188 (Remain Open)

The following items from `docs/brain/B188/06-deferred-backlog.md` were not addressed in B189.
They are the authoritative carry-forward list for B190+.

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
Target Block: B190+ (low urgency -- dead path, not broken path)
Status: OPEN (CARRIED FORWARD from B187 via B188, B189)
Owner: Director
```

---

### DW-B185-BE-ALL-SCOPE-01: Spec HTML Update Deferred (T1-AC8)

```
Priority: P2
Description: B185-T1 implemented instrument-scoped BE ALL (code complete, VERIFY_PASS).
  T1-AC8 requires a spec HTML update:
  - Add DW-B185-BE-ALL-SCOPE-01 card to specs/002-trade-copier-spec.html
  - Update B39 scope table row:
    "Instrument scope: All instruments with open positions"
    --> "Panel's _instrument only (Account.All)"
  - Update B39 comparison table note accordingly.
  This is a Director/documentation task (spec HTML is not a .cs file).
  Code change is complete and shipped. Only the spec document lags.
Blocking: No
Target Block: B190+ or Director task (parallel with next block)
Status: OPEN (CARRIED FORWARD from B185 via B186, B187, B188, B189)
```

---

### DW-B183-05: RULES_CATALOG.md Truncated at JS-041

```
Priority: P1
Description: The file docs/standards/jane-street/RULES_CATALOG.md on disk contains only
  JS-001 through JS-041. Rules JS-042 through JS-110 (including JS-066 CYC ceiling and
  JS-081 ASCII-only) are absent from the file. These rules are enforced via the
  ptt-plan-reviewer's hardcoded DNA block and workspace rule 06-rules-catalog-gate.md,
  so enforcement is not impaired. However, the on-disk catalog is incomplete and any
  agent that reads the file directly will have an incomplete picture.
  Fix: Restore the full catalog (JS-001..JS-110) to the file.
Blocking: No (enforcement operates via DNA block + workspace rule)
Target Block: Ops task -- fix in parallel with any block
Status: OPEN (CARRIED FORWARD from B183 via B184, B185, B186, B187, B188, B189)
```

---

### DW-B183-03: IsLaneEEligible CYC=8 Frozen

```
Priority: P1
Description: IsLaneEEligible in CopyEngine.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to IsLaneEEligible without prior helper extraction is an automatic P1 JS-066
  violation. Not modified in B183-B189.
  Future engineers modifying Lane E eligibility MUST extract a helper method first.
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions
  to IsLaneEEligible)
Target Block: When Lane E is next modified
Status: OPEN (CARRIED FORWARD from B183 via B184, B185, B186, B187, B188, B189)
```

---

### DW-B183-01: xUnit Tests for Slate Gate Methods

```
Priority: P2
Description: Slate Gate methods (in CopyEngine.cs) have no xUnit test coverage.
  Tests were specified in B183 but not implemented. B184 found a test project DID exist
  (PropTraderTools.Tests.csproj), so the blocker was never "no project" -- the tests
  were simply not written. B185-T4 closed the B184-specific deferred tests (14 tests).
  Slate Gate tests remain unwritten.
Blocking: No
Target Block: Future test coverage block
Status: OPEN (CARRIED FORWARD from B183 via B184, B185, B186, B187, B188, B189)
```

---

### DW-B183-02: xUnit Test for FlatGuardLoop

```
Priority: P2
Description: FlatGuardLoop in CopyEngine.cs has no xUnit test coverage. Same situation
  as DW-B183-01 -- test project exists; tests simply not written.
Blocking: No
Target Block: Future test coverage block
Status: OPEN (CARRIED FORWARD from B183 via B184, B185, B186, B187, B188, B189)
```

---

## Summary by Priority

| Priority | Count | Items |
|----------|-------|-------|
| P1 | 2 | DW-B183-03, DW-B183-05 |
| P2 | 3 | DW-B185-BE-ALL-SCOPE-01, DW-B183-01, DW-B183-02 |
| P3 | 1 | DW-B187-01 |

**Total open items**: 6
**Items closed by B189**: 1 (DW-B187-02)
**New items opened by B189**: 0

---

## Recommended Next Block Priorities

1. **DW-B183-05** (P1): Restore full RULES_CATALOG.md to disk -- ops task, zero functional risk, parallel with any block.
2. **DW-B183-03** (P1): IsLaneEEligible CYC freeze -- address when Lane E is next modified.
3. **DW-B185-BE-ALL-SCOPE-01** (P2): Spec HTML update -- Director or documentation task. Low effort.
4. **DW-B183-01/02** (P2): Slate Gate + FlatGuardLoop test coverage -- bundle into a test-coverage block.
5. **DW-B187-01** (P3): ResolveFollowerTargets followerPosQty path review -- address when B115 scaling logic is revisited.

---

*Written by: ptt-plan-reviewer*
*Epic: B189*
*Phase: 5 (Final Review)*
*Required for FINAL_PASS gate (role definition mandate).*
*Supersedes: docs/brain/B188/06-deferred-backlog.md (as authoritative carry-forward list)*
