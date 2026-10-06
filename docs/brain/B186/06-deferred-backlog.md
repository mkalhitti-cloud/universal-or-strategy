# B186 Deferred Backlog
## ptt-plan-reviewer Phase 5

**Date**: 2026-07-08
**Author**: ptt-plan-reviewer (Phase 5)
**Epic**: B186 -- Quick ALL + QAll2t Instrument Scope
**Preceding backlog**: `docs/brain/B185/06-deferred-backlog.md`
**Status**: Authoritative deferred backlog for the NEXT epic block (B187+)

---

## Purpose

This document records all deferred work items that remain open after B186 completion.
It supersedes `docs/brain/B185/06-deferred-backlog.md` as the authoritative carry-forward list.
Items closed by B186 are listed with CLOSED status for audit trail completeness.

**P0 = Compile-blocking or P0 DNA violation. Must be resolved before shipping.**
**P1 = Architecture risk. Must be resolved before next major feature wave.**
**P2 = Quality / testing gap. Track and address in near-term pipeline.**
**P3 = Low-risk cleanup / informational. Address when convenient.**

---

## Items Closed by B186

The following item from `docs/brain/B185/06-deferred-backlog.md` is now CLOSED.

| DW ID | Description | Closed By | Closing Ticket |
|-------|-------------|-----------|----------------|
| DW-B185-QX-ALL-SCOPE-01 | QAll2t wrong-instrument qty split: positions on instruments other than `_instrument` were incorrectly included in the forced-target exit calculation | B186-T2 VERIFY_PASS | `ticket-2-verification.md` — instrument filter folded into branch (6) of `Execute(List, Instrument)` at line 138 |

---

## New Items Opened by B186

### DW-B186-DIAG-CLEANUP-01: Remove [DW-B115-DIAG] Log Blocks from PttGlobalQuickExit

```
Priority: P3
Description: PttGlobalQuickExit.Execute(Instrument) contains a diagnostic log call
  LogLeaderDiag(acc, targets, pos.Quantity) at line 78, guarded by a
  // DW-B115-DIAG comment. This scaffolding was added in B115 to trace the
  root cause of a qty-split discrepancy between leader and followers.
  B186 confirmed instrument scope now limits the loop to _instrument only,
  which resolves the primary vector for the discrepancy.
  Once DW-B115 root cause is formally confirmed and closed, these diagnostic
  log blocks should be removed from Execute and ExecuteFollowers to reduce
  log noise in production.
  Also applies to any [DW-B115-DIAG] blocks in ExecuteFollowers helper.
Blocking: No (diagnostic code is benign; log noise only)
Target Block: B187+ (when DW-B115 is formally closed)
Status: OPEN
```

---

### DW-B186-WARN-01: CS0067 PendingBeArmed Never Raised Warning

```
Priority: P3
Description: dotnet build emits CS0067 "The event 'CopyEngine.PendingBeArmed' is never used."
  This is a Roslyn false positive. The event IS subscribed by TradeCopierPanel.cs
  (lines 771-772) and raised via the DisarmPendingBe/RaiseBeAllDisarmed path.
  CS0067 fires because Roslyn does not track event usage through external subscribers
  in a separate class.
  This item supersedes and consolidates DW-B184-04 (same root cause).
  Potential fix: #pragma warning disable CS0067 on the event declaration, or add
  an internal fire method that Roslyn can track. Low priority.
  No functional defect.
Note: DW-B184-04 is retained in this backlog for audit trail traceability.
  DW-B186-WARN-01 is the canonical item going forward.
Blocking: No
Target Block: B187+ or any block in CopyEngine/TradeCopierPanel vicinity
Status: OPEN
```

---

## Items Carried Forward from B185 (Remain Open)

The following items from `docs/brain/B185/06-deferred-backlog.md` were not addressed in B186.
They are the authoritative carry-forward list for B187+.

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
Target Block: B187+ or Director task (parallel with next block)
Status: OPEN (CARRIED FORWARD from B185)
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
Status: OPEN (CARRIED FORWARD from B183 via B184, B185)
```

---

### DW-B183-03: IsLaneEEligible CYC=8 Frozen

```
Priority: P1
Description: IsLaneEEligible in CopyEngine.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to IsLaneEEligible without prior helper extraction is an automatic P1 JS-066
  violation. Not modified in B183, B184, B185, or B186.
  Future engineers modifying Lane E eligibility MUST extract a helper method first.
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions
  to IsLaneEEligible)
Target Block: When Lane E is next modified
Status: OPEN (CARRIED FORWARD from B183 via B184, B185)
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
Status: OPEN (CARRIED FORWARD from B183 via B184, B185)
```

---

### DW-B183-02: xUnit Test for FlatGuardLoop

```
Priority: P2
Description: FlatGuardLoop in CopyEngine.cs has no xUnit test coverage. Same situation
  as DW-B183-01 -- test project exists; tests simply not written.
Blocking: No
Target Block: Future test coverage block
Status: OPEN (CARRIED FORWARD from B183 via B184, B185)
```

---

### DW-B184-04: CS0067 Warning -- PendingBeArmed Event

```
Priority: P3
Description: See DW-B186-WARN-01 above -- same root cause, now the canonical item.
  DW-B184-04 is retained for audit trail traceability only.
  dotnet build emits CS0067 "The event 'CopyEngine.PendingBeArmed' is never used."
  Roslyn false positive. No functional defect.
Blocking: No
Target Block: See DW-B186-WARN-01
Status: OPEN (superseded by DW-B186-WARN-01 as canonical item; retained for traceability)
```

---

## Summary by Priority

| Priority | Count | Items |
|----------|-------|-------|
| P1 | 2 | DW-B183-03, DW-B183-05 |
| P2 | 3 | DW-B185-BE-ALL-SCOPE-01, DW-B183-01, DW-B183-02 |
| P3 | 3 | DW-B186-DIAG-CLEANUP-01, DW-B186-WARN-01, DW-B184-04 |

**Total open items**: 8
**Items closed by B186**: 1 (DW-B185-QX-ALL-SCOPE-01)
**New items opened by B186**: 2 (DW-B186-DIAG-CLEANUP-01, DW-B186-WARN-01)

---

## Recommended Next Block Priorities

1. **DW-B183-05** (P1): Restore full RULES_CATALOG.md to disk — ops task, zero functional risk, parallel with any block.
2. **DW-B183-03** (P1): IsLaneEEligible CYC freeze — address when Lane E is next modified.
3. **DW-B185-BE-ALL-SCOPE-01** (P2): Spec HTML update — Director or documentation task. Low effort.
4. **DW-B183-01/02** (P2): Slate Gate + FlatGuardLoop test coverage — bundle into a test-coverage block.
5. **DW-B186-WARN-01** (P3): CS0067 suppression — low effort, do when in TradeCopierPanel/CopyEngine vicinity.
6. **DW-B186-DIAG-CLEANUP-01** (P3): Remove [DW-B115-DIAG] log blocks — address when DW-B115 is formally closed.

---

*Written by: ptt-plan-reviewer*
*Epic: B186*
*Phase: 5 (Final Review)*
*Required for FINAL_PASS gate (role definition mandate).*
*Supersedes: docs/brain/B185/06-deferred-backlog.md (as authoritative carry-forward list)*
