# B199 Deferred Backlog

**Epic**: B199 -- Direct Repair Promotion + B198 Deferred Backlog
**Author**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-10-14
**Preceding backlog**: docs/brain/B198/06-deferred-backlog.md (19 items; superseded by this document)
**Status**: Authoritative deferred backlog for the NEXT epic block (B200+)

---

## Purpose

This document records all deferred work items that remain open after B199 completion.
It supersedes docs/brain/B198/06-deferred-backlog.md as the authoritative carry-forward list.
Items closed by B199 are listed with CLOSED status for audit trail completeness.

**P0 = Compile-blocking or P0 DNA violation. Must be resolved before shipping.**
**P1 = Architecture risk or JS-066 violation. Must be resolved before next major feature wave.**
**P2 = Quality / testing gap. Track and address in near-term pipeline.**
**P3 = Low-risk cleanup / informational. Address when convenient.**

---

## Items Closed by B199

| DW ID | Description | Priority | Closed By | Closing Artifact |
|-------|-------------|----------|-----------|-----------------|
| DW-B198-CHECKREV-CYC-01 | CheckAtmTemplatesForReverse(string) CYC=11 -- extracted qty-parse block into `TryParseAtmTemplateGroupQty(string filePath, out int perGroupQty)`. Strict McCabe CYC reduced from 11 to 8 (at JS-066 ceiling). | P1 | T3 | ticket-3-verification.md VERIFY_PASS |
| DW-B198-QTY-SIM-01 | T2a SIM evidence: `[PTT-ATM-SEND]` qty=10 for Sim102/103/104 from B199 output.md lines 59-61. H1 CONFIRMED (qty passes unchanged, no divide needed). ticket-2a-sim-evidence.md created. | P1 | T4 | ticket-4-verification.md VERIFY_PASS |

---

## New Items Opened by B199

---

### DW-B199-H2-CLEANUP-01: Remove T2b H2 Logic (SendCopyWithAtm Area)

```
Priority: P3
Description: T2b (from B198) implemented an H2 hypothesis fix:
  _atmTemplateGroupQty ConcurrentDictionary + TryGetAtmTemplateGroupCount method +
  qty division in SendCopyWithAtm. The H2 code is a benign no-op when
  _atmTemplateGroupQty dict is empty (TryGetAtmTemplateGroupCount returns false,
  division path never executes).
  B199 T4 confirms H1 (docs/brain/B198/ticket-2a-sim-evidence.md):
  qty=10 passes through unchanged; follower qty equals leader qty exactly.
  Therefore the H2 code is unnecessary. It is currently harmless -- no correctness
  risk. Recommended action: remove in a future cleanup wave when SendCopyWithAtm
  is next modified, to reduce cognitive overhead.
  Components to remove:
    - _atmTemplateGroupQty ConcurrentDictionary field (if only used by H2 path)
    - TryGetAtmTemplateGroupCount method
    - qty division branch in SendCopyWithAtm
  Note: TryParseAtmTemplateGroupQty (added by B199 T3) populates _atmTemplateGroupQty.
  If _atmTemplateGroupQty is removed, TryParseAtmTemplateGroupQty's TryAdd call and
  the field itself would also be removed. Evaluate at cleanup time.
Blocking: No (code is a benign no-op; no correctness issue)
Target Block: B200+ (when SendCopyWithAtm is next touched; or as standalone cleanup)
Status: OPEN (opened B199 T4 evidence, per docs/brain/B199/02-architecture-plan.md)
Owner: Director
Source: docs/brain/B198/ticket-2a-sim-evidence.md, docs/brain/B199/02-architecture-plan.md
  Deferred Work Register
```

---

## Items Carried Forward Unchanged from B198

All 17 items from docs/brain/B198/06-deferred-backlog.md that were not closed by B199
carry forward unchanged. None were addressed by B199 scope.

---

### DW-B198-SIMSTP-SIM-01: B198 SIM Gate Not Yet Run

```
Priority: P1
Description: B198 SIM gate not yet run. Four SIM scenarios per docs/brain/B198-orchestrator-prompt.md:
  SIM-1 (T1 -- SimStop fix):
    Place ATM entry qty=10. Confirm ZERO "IsWorkingBracket=False name=SimStop" in log.
    Confirm followers all Long N. Let leader SimStop trigger. ZERO double Sell copy.
    PTT-Flatten fires <= 1 time per follower after stop.
  SIM-2 (T2a -- QTY instrumentation):
    PARTIALLY ADDRESSED: B199 T4 confirms H1 (qty=10 passes unchanged) via B199 output.
    Full T2a SIM scenario (tmpl= field showing template name) still pending formal record
    in docs/brain/B198/sim-gate-completion.md.
  SIM-3 (T1+T3 -- fo=NULL fix):
    Drag leader entry 2-3x. Let leader fill. ZERO "fo=NULL" in TP4-SFB log lines.
    Confirm [SLATE-READY-DRAIN] log shows drainFo=set (not drainFo=null).
  SIM-4 (T4 -- ATM dir fix):
    Restart PTT. ZERO "ATM template dir not found" on startup.
    If template has Reverse=true: "[PTT-STARTUP-WARN] ATM template ... Reverse=true" fires.
  Record all results in docs/brain/B198/sim-gate-completion.md.
Blocking: Yes -- live validation required for B198 to be considered production-ready.
Target Block: B200 SIM block
Status: OPEN (carried forward from B198; SIM-2 partially addressed by B199 T4)
Owner: Director
Source: docs/brain/B198-orchestrator-prompt.md ss B198 SIM GATE
```

---

### DW-B183-03: IsLaneEEligible CYC=8 Frozen

```
Priority: P1
Description: IsLaneEEligible in CopyEngine.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to IsLaneEEligible without prior helper extraction is an automatic P1 JS-066
  violation. Not modified in B183-B199.
  Future engineers modifying Lane E eligibility MUST extract a helper method first.
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions)
Target Block: When Lane E is next modified
Status: OPEN (carried forward from B183 via B184-B199)
Owner: Director
Source: docs/brain/B191/06-deferred-backlog.md -> docs/brain/B196/06-deferred-backlog.md
  -> B197 -> B198 -> B199
```

---

### DW-B167-DEFER-03: Combined B161-B166 SIM Gate

```
Priority: P1
Description: The combined B161-B166 SIM gate (confirming all 6 signals of
  IsFollowerPositionBlocked fire correctly in a live BE-All burst scenario) has not been
  run. B196-T4 restores the implementation; the SIM gate is the live validation step.
Blocking: Yes -- SIM gate must pass before B161-B166 + B163 guard can be considered
  fully validated in production.
Target Block: B200 SIM block
Status: OPEN (carried forward from B167 via B168-B199)
Owner: Director
Source: docs/brain/B167/05-final-review.md Section K
```

---

### DW-B167-DEFER-04: B167 SIM Gate

```
Priority: P1
Description: B167 SIM gate -- prerequisite for B168/B169 SIM gate. Must be run and
  recorded before B168/B169 SIM gates are considered validated.
Blocking: Prerequisite for DW-B167-DEFER-03 and B169 SIM gate
Target Block: B200 SIM gate sequence
Status: OPEN (carried forward from B167 via B168-B199)
Owner: Director
Source: docs/brain/B167/05-final-review.md Section K
```

---

### DW-B170-SIM: B170 SIM Gate (Live Regression Verify for DW-B170-01)

```
Priority: P1
Description: Director drags a target on the leader account while the leader bracket stop
  is in OrderState.Accepted. Confirms FindLeaderStopPrice (now including Accepted) returns
  non-zero and the follower stop is preserved after the drag. This is the live validation
  of the DW-B170-01 fix (B196-T2).
Blocking: Yes -- live regression verify required before the Accepted window fix is
  considered fully validated in SIM.
Target Block: B200 SIM block
Status: OPEN (carried forward from B196 via B197-B199)
Owner: Director
Source: docs/brain/B196/02-architecture-plan.md; docs/brain/B170/05-final-review.md
```

---

### DW-B197-SIM-01: B197 SIM Gate

```
Priority: P1
Description: B197 SIM gate not yet run. Four SIM scenarios planned in arch plan ss 8:
  SIM-1 (T1): TrySetSlateReadyOnDrainFill -- verify "[SLATE-READY-DRAIN]" fires and
    subsequent drag sync to follower succeeds after drain fill.
  SIM-2 (T2): TryAbortLeaderFlatDrains -- verify "[DRAIN-ABORT-LEADER-FLAT]" fires and
    no ghost PTT-Copy submitted after leader goes flat.
  SIM-3 (T3): TryCancelStaleGtcExitOnEntry -- verify "[GTC-SWEEP] cancelled PTT-BE-*"
    fires and stale GTC exit is cancelled when new entry fills.
  SIM-4 (T4): IsPttDragOrphanCancellable suffix -- verify suffixed drag orders
    (PTT-TGT-Drag-1, PTT-STP-Drag-2) are swept after flat.
  Must be run before B197 gap restore is considered fully validated in production.
Blocking: Yes -- live validation required for FINAL_PASS to be considered production-ready
Target Block: B200 SIM block (now expanded to cover B197, B198, and B199 scenarios)
Status: OPEN (carried forward from B197 via B198-B199)
Owner: Director
Source: docs/brain/B197/02-architecture-plan.md ss 8
```

---

### DW-B198-GUARDIAN-01: Guardian Script TradeCopierPanel Check Failure (Pre-existing)

```
Priority: P2
Description: verify_critical_symbols.ps1 has been failing since before B198 on:
  "TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)"
  The script expects the literal string "_instr2tBtn.Background = BrushTeal" in
  TradeCopierPanel.cs. The actual code uses a conditional:
  "btn.Background = isTeal ? BrushTeal : BrushInactive" (line ~1162).
  The literal assignment expected by the script was never introduced.
  All B198 and B199 tickets noted this as pre-existing.
  Resolution options:
    (a) Update the guardian script pattern to match "BrushTeal" (broader match), or
    (b) Add a dedicated "_instr2tBtn.Background = BrushTeal;" assignment to
        TradeCopierPanel.cs where appropriate.
  Not a DNA violation; the guardian script is a supplementary correctness check.
Blocking: No
Target Block: When TradeCopierPanel.cs is next touched, or as standalone cleanup
Status: OPEN (first documented B198, was pre-existing; confirmed carry-forward by B199)
Owner: Director
Source: ticket-1-verification.md through ticket-4-verification.md (B198 and B199) -- all
  note same pre-existing failure
```

---

### DW-B192-SINST-01: SetInstrument() CYC at Ceiling 8

```
Priority: P2
Description: SetInstrument() in TradeCopierPanel.cs is at CYC=8 (JS-066 ceiling). Any branch
  addition to SetInstrument() without prior helper extraction is an automatic P1 JS-066
  violation. Engineers modifying SetInstrument() MUST extract a helper method first.
  Not blocking; CYC is at limit, not over it.
Blocking: No (blocks only future branch additions)
Target Block: When SetInstrument() is next modified
Status: OPEN (opened B193, carried forward through B194-B199)
Owner: Director
Source: docs/brain/B193/02-architecture-plan.md (Deferred Work Register)
```

---

### DW-B159-K2: 14 xUnit Tests (BE Path)

```
Priority: P2
Description: 14 xUnit tests for the BE (break-even) path require infrastructure that was
  not yet available at B159 time. Tests are planned but not implemented.
Blocking: No
Target Block: B200+ (when test infrastructure is available)
Status: OPEN (carried forward from B159 via B160-B199)
Owner: Director
```

---

### DW-B159-K3: Attempt Cap Hardcoded at 3 in TryArmBePriceRetry

```
Priority: P2
Description: TryArmBePriceRetry has a hardcoded cap of 3 retry attempts. This should be
  configurable or derived from the rule configuration.
Blocking: No
Target Block: B200+
Status: OPEN (carried forward from B159 via B160-B199)
Owner: Director
```

---

### DW-B159-K4: intendedBePrice Sourcing Dependency

```
Priority: P2
Description: intendedBePrice sourcing has an unresolved dependency on the BE calculation
  pipeline. Not blocking current function.
Blocking: No
Target Block: B200+
Status: OPEN (carried forward from B159 via B160-B199)
Owner: Director
```

---

### DW-B167-DEFER-01: ECA-1 Double-Position Race

```
Priority: P2
Description: ECA-1 double-position race condition -- follower can enter a position at the
  same time as a legitimate ATM entry in rare timing conditions. Monitor at SIM.
Blocking: No (monitor-only)
Target Block: Monitor at SIM; address if observed
Status: OPEN (carried forward from B167 via B168-B199)
Owner: Director
Source: docs/brain/B167/05-final-review.md Section K
```

---

### DW-B197-03: Pre-existing DwB15701SlateReadyTests Failures

```
Priority: P2
Description: Two tests in CopyEngineTests.cs:DwB15701SlateReadyTests are failing and
  pre-date B197:
  (a) TrySetSlateReadyOnDrainFill_CalledAfterHandleDrainTerminal: expects non-existent
      HandleDrainTerminalState caller + wrong argument type. Both expectations incorrect.
  (b) TrySetSlateReadyOnDrainFill_NullOrder_NoSideEffect: TypeInitializationException --
      CopyEngine.Instance requires NT8 runtime.
  Neither introduced by B197, B198, or B199.
Blocking: No (not B199 or predecessor regressions)
Target Block: When CopyEngineTests.cs DwB15701SlateReadyTests is next reviewed
Status: OPEN (carried forward from B197 via B198-B199)
Owner: Director
Source: docs/brain/B197/ticket-1-verification.md (Pre-Existing Test Failure Assessment)
```

---

### DW-B193-01: OnQuickAllBufferChanged Old Label Strings

```
Priority: P3
Description: OnQuickAllBufferChanged in TradeCopierPanel.cs still uses old label strings
  "Quick ALL" and "QAll2t" in the singleton-sink callback path. Low severity: visual label
  shows old name only on the singleton path. Not a DNA violation.
Blocking: No
Target Block: When OnQuickAllBufferChanged is next touched
Status: OPEN (opened B193, carried forward through B194-B199)
Owner: Director
Source: docs/brain/B193/02-architecture-plan.md (Deferred Work Register)
```

---

### DW-B197-01: Stale CCN Comment at IsPttDragOrphanCancellable

```
Priority: P3
Description: CopyEngine.cs comment states "TB-T6: IsPttDragOrphanCancellable --
  CCN<=4." After T4 null guard additions, the actual Lizard CCN = 8. The comment is stale.
  Update to: "TB-T6: IsPttDragOrphanCancellable -- CCN=8 (JS-066 ceiling)."
Blocking: No
Target Block: When IsPttDragOrphanCancellable is next touched
Status: OPEN (carried forward from B197 via B198-B199)
Owner: Director
Source: docs/brain/B197/ticket-4-verification.md W-T4-02
```

---

### DW-B197-02: T4 Fact Names Misleading

```
Priority: P3
Description: T4 test facts in B197Tests.cs are named "SuffixedTgtName_ReturnsTrue" etc.
  but use source-text assertions (Assert.Contains) rather than direct seam calls.
  Names imply a method call return value was verified. Cosmetic naming issue only.
  Tests pass and assertions are meaningful.
Blocking: No
Target Block: When B197Tests.cs or test harness is next revisited
Status: OPEN (carried forward from B197 via B198-B199)
Owner: Director
Source: docs/brain/B197/ticket-4-verification.md W-T4-01
```

---

### DW-B197-04: TryCancelSingleStaleGtcExit Not in Plan CYC Table

```
Priority: P3
Description: TryCancelSingleStaleGtcExit (CopyEngine.cs) was not in the original
  B197 plan ss 7 CYC Impact Summary. Extracted from TryCancelStaleGtcExitOnEntry to keep
  parent CCN<=8. Extraction is architecturally correct; plan ss 7 table is implicitly addended.
  Informational only. No action required.
Blocking: No
Target Block: Informational only
Status: OPEN (carried forward from B197 via B198-B199)
Owner: Director
Source: docs/brain/B197/ticket-3-completion.md, ticket-3-verification.md
```

---

## Summary by Priority

| Priority | Count | DW IDs |
|----------|-------|--------|
| P0 | 0 | (None) |
| P1 | 6 | DW-B198-SIMSTP-SIM-01, DW-B183-03, DW-B167-DEFER-03, DW-B167-DEFER-04, DW-B170-SIM, DW-B197-SIM-01 |
| P2 | 8 | DW-B198-GUARDIAN-01, DW-B192-SINST-01, DW-B159-K2, DW-B159-K3, DW-B159-K4, DW-B167-DEFER-01, DW-B197-03 |
| P3 | 4 | DW-B193-01, DW-B197-01, DW-B197-02, DW-B197-04 |
| P3 (new) | 1 | DW-B199-H2-CLEANUP-01 |

**Total open items: 19** (but note: the P2 count above is 7, not 8; see below)

Corrected count:
- P1: 6 items
- P2: 7 items (DW-B198-GUARDIAN-01, DW-B192-SINST-01, DW-B159-K2, DW-B159-K3, DW-B159-K4, DW-B167-DEFER-01, DW-B197-03)
- P3: 5 items (DW-B193-01, DW-B197-01, DW-B197-02, DW-B197-04, DW-B199-H2-CLEANUP-01)

**Total open items: 18**
**Items closed by B199: 2** (DW-B198-CHECKREV-CYC-01, DW-B198-QTY-SIM-01)
**New items opened by B199: 1** (DW-B199-H2-CLEANUP-01)
**Items carried forward unchanged from B198: 17**

---

## Recommended Next Block Priorities (B200)

1. **Combined SIM Gate** (DW-B198-SIMSTP-SIM-01, DW-B197-SIM-01, DW-B167-DEFER-03, DW-B167-DEFER-04, DW-B170-SIM -- P1):
   Schedule a comprehensive SIM session covering B197, B198 SIM-1/3/4, B167 SIM gate,
   and B170 SIM gate. Record all results. This single SIM session can close up to 5 P1 items.

2. **Guardian Script Fix** (DW-B198-GUARDIAN-01 -- P2):
   Update verify_critical_symbols.ps1 pattern for TradeCopierPanel BrushTeal check.
   Low effort, high signal improvement for future verifications.

3. **H2 Cleanup** (DW-B199-H2-CLEANUP-01 -- P3):
   Remove T2b H2 logic from SendCopyWithAtm area when that method is next touched.
   Benign no-op now; remove to reduce technical debt.

---

*Written by: ptt-plan-reviewer*
*Epic: B199*
*Phase: 5 (Final Review)*
*Supersedes: docs/brain/B198/06-deferred-backlog.md (as authoritative carry-forward list)*
*Required for FINAL_PASS gate (role definition mandate).*
