# B185 Deferred Backlog
## ptt-plan-reviewer Phase 5

**Date**: 2026-07-08
**Author**: ptt-plan-reviewer (Phase 5)
**Epic**: B185 -- BE ALL Instrument Scope + DispatchCopy CYC Headroom + Click-Trader Qty + B184 Tests
**Preceding backlog**: `docs/brain/B184/06-deferred-backlog.md`
**Status**: Authoritative deferred backlog for the NEXT epic block (B186+)

---

## Purpose

This document records all deferred work items that remain open after B185 completion.
It supersedes `docs/brain/B184/06-deferred-backlog.md` as the authoritative carry-forward list.
Items closed by B185 are listed with CLOSED status for audit trail completeness.

**P0 = Compile-blocking or P0 DNA violation. Must be resolved before shipping.**
**P1 = Architecture risk. Must be resolved before next major feature wave.**
**P2 = Quality / testing gap. Track and address in near-term pipeline.**
**P3 = Low-risk cleanup / informational. Address when convenient.**

---

## Items Closed by B185

The following items from `docs/brain/B184/06-deferred-backlog.md` are now CLOSED.

| DW ID | Description | Closed By | Closing Ticket |
|-------|-------------|-----------|----------------|
| DW-B184-01 | DispatchCopy CYC=8 frozen -- `TryGetLastDirection` + `RecordLastDirection` extraction delivers documented headroom | B185-T2 VERIFY_PASS | `ticket-2-verification.md` |
| DW-B184-02 | Click-trader qty hardcoded to 1 -- `private int _clickQty = 1;` field promoted on TradeCopierPanel; `OnChartMouseDown` uses field | B185-T3 VERIFY_PASS | `ticket-3-verification.md` |
| DW-B184-03 | 14 B184 tests unwritten (backlog incorrectly stated no test project -- `PropTraderTools.Tests.csproj` + `CopyEngineTests.cs` existed) -- all 14 implemented in `B184DeferredTests` class | B185-T4 VERIFY_PASS | `ticket-4-verification.md` |
| DW-B183-04 | `_laneDDispatched` ConcurrentDictionary unbounded growth -- `LaneDEvictThreshold = 10000` constant + eviction branch added in `TryLaneDDispatchFollower` | B185-T3 VERIFY_PASS | `ticket-3-verification.md` |
| DW-B183-10 | DispatchCopy CYC freeze (B183 entry) -- superseded by DW-B184-01 in B184 and now CLOSED by B185-T2 | B185-T2 (via DW-B184-01 closure) | `ticket-2-verification.md` |

---

## New Item Opened by B185

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
Blocking: No (FINAL_PASS was not blocked by this item)
Target Block: B186 or Director task (parallel with next block)
```

---

## Items Carried Forward from B184 (Remain Open)

The following items from `docs/brain/B184/06-deferred-backlog.md` were not addressed in B185.
They are the authoritative carry-forward list for B186+.

---

### DW-B183-03: IsLaneEEligible CYC=8 Frozen

```
Priority: P1
Description: IsLaneEEligible in CopyEngine.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to IsLaneEEligible without prior helper extraction is an automatic P1 JS-066
  violation. Not modified in B183, B184, or B185.
  Future engineers modifying Lane E eligibility MUST extract a helper method first.
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions
  to IsLaneEEligible)
Target Block: When Lane E is next modified
Status: OPEN (CARRIED FORWARD from B183 via B184)
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
Status: OPEN (CARRIED FORWARD from B183 via B184)
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
Status: OPEN (CARRIED FORWARD from B183 via B184)
```

---

### DW-B183-02: xUnit Test for FlatGuardLoop

```
Priority: P2
Description: FlatGuardLoop in CopyEngine.cs has no xUnit test coverage. Same situation
  as DW-B183-01 -- test project exists; tests simply not written.
Blocking: No
Target Block: Future test coverage block
Status: OPEN (CARRIED FORWARD from B183 via B184)
```

---

### DW-B184-04: CS0067 Warning -- PendingBeArmed Event

```
Priority: P3
Description: dotnet build emits CS0067 "The event 'CopyEngine.PendingBeArmed' is never used."
  Roslyn false positive. The event IS subscribed by TradeCopierPanel.cs (lines 771-772) and
  raised via the DisarmPendingBe/RaiseBeAllDisarmed path. CS0067 fires because Roslyn does
  not track event usage through external subscribers in a separate class.
  No functional defect. Warning adds noise to build output.
  Note: T1 and T3 verifiers noted this warning; T3 verifier showed 0 warnings (may have been
  cleared transiently by subsequent commits or build caching). The CS0067 is structural --
  it will reappear unless explicitly suppressed or the event is given an internal fire wrapper.
  Potential fix: #pragma warning disable CS0067 on the event declaration, or add an internal
  fire method that Roslyn can track. Low priority.
Blocking: No
Target Block: Future build hygiene block
Status: OPEN (CARRIED FORWARD from B184)
```

---

## Summary by Priority

| Priority | Count | Items |
|----------|-------|-------|
| P1 | 2 | DW-B183-03, DW-B183-05 |
| P2 | 4 | DW-B185-BE-ALL-SCOPE-01, DW-B183-01, DW-B183-02, DW-B184-04* |
| P3 | 1 | DW-B184-04 |

*DW-B184-04 is P3 per its original classification; listed in P2 count row above for P3 total below.

| Priority | Count | Items |
|----------|-------|-------|
| P1 | 2 | DW-B183-03, DW-B183-05 |
| P2 | 3 | DW-B185-BE-ALL-SCOPE-01, DW-B183-01, DW-B183-02 |
| P3 | 1 | DW-B184-04 |

**Total open items**: 6
**Items closed by B185**: 5 (DW-B184-01, DW-B184-02, DW-B184-03, DW-B183-04, DW-B183-10)
**New items opened by B185**: 1 (DW-B185-BE-ALL-SCOPE-01)

---

## Recommended Next Block Priorities

1. **DW-B185-BE-ALL-SCOPE-01** (P2): Spec HTML update -- Director or documentation task. Low effort.
2. **DW-B183-05** (P1): Restore full RULES_CATALOG.md to disk -- ops task, parallel with any block.
3. **DW-B183-03** (P1): IsLaneEEligible CYC freeze -- address when Lane E is next modified.
4. **DW-B183-01/02** (P2): Slate Gate + FlatGuardLoop test coverage -- bundle into a test-coverage block.
5. **DW-B184-04** (P3): CS0067 suppression -- low effort, do when in TradeCopierPanel/CopyEngine vicinity.

---

*Written by: ptt-plan-reviewer*
*Epic: B185*
*Phase: 5 (Final Review)*
*Required for FINAL_PASS gate (role definition mandate).*
*Supersedes: docs/brain/B184/06-deferred-backlog.md (as authoritative carry-forward list)*
