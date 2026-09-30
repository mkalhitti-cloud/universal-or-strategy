# B128 Ph3.5 Ticket Review

**Reviewer**: ptt-ticket-reviewer
**Phase**: 3.5 (Ticket Review)
**Date**: 2026
**Ticket file**: `docs/brain/B128/04-tickets.md`
**Plan file**: `docs/brain/B128/02-architecture-plan.md`
**Plan review**: `docs/brain/B128/02-plan-review.md` (REVIEW_PASS, R-01..R-11 all passed)

---

## Gate: TICKET_REVIEW_PASS

---

## STEP 0 — Rules Catalog Gate

`docs/standards/jane-street/RULES_CATALOG.md` — UTF-8 clean, fully readable (JS-001..JS-110 confirmed).
No P0 violations found in the ticket-described code.
**GATE RESULT: PASS**

---

## Checks

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| TR-01 | Traceability: REQ-01..REQ-07 each maps to at least one CHANGE block | PASS | REQ-01→C2; REQ-02→C1+C3; REQ-03→C4; REQ-04→C5; REQ-05→C7; REQ-06→C5+C7; REQ-07→C8. No phantom work. No uncovered requirement. |
| TR-02 | Scope boundary: exactly 2 files changed; PttQuickExit.cs, PttGlobalBreakEven.cs, CopyEngine.cs explicitly NOT changed | PASS | Ticket §"Files To Modify" lists exactly `TradeCopierPanel.cs` + `Tests/B128Tests.cs`. §"Files Explicitly NOT Changed" lists all three excluded files with explicit rationale. |
| TR-03 | All 6 method signatures match architecture plan exactly; CYC budgets listed per method | PASS | All signatures verbatim identical to plan: `BuildInstrRow` (private void), `ComputeInstrSplit` (internal static (int t1, int t2)), `OnInstrQxClick/Up/Down/BeClick` (private void, RoutedEventArgs). CYC budgets all present. |
| TR-04 | 7-scan checklist: all 7 scans (SCAN-01..SCAN-07) present with command + expected result | PASS | All 7 scans present. Each has an exact command and a pass criterion (0 matches / Build succeeded / 4 passed). Engineer is required to run all 7 to zero before BUILD_PASS. |
| TR-05 | Layout insertion: `_instrRowPanel` inserted BETWEEN `_beRowPanel` (L914) and `_quickRowPanel` (L915); both surrounding lines unchanged | PASS | CHANGE 2 shows `_beRowPanel` line unchanged (comment preserved), then `BuildInstrRow()` call, then `root.Children.Add(_instrRowPanel)`, then `_quickRowPanel` line unchanged (comment preserved). Correct insertion order. |
| TR-06 | ArmPendingBe call site: `_engine.ArmPendingBe(_instrument, _leaderAccount, _beBuffer)`; NOT `GlobalBe.ArmPendingBe`; parameter order matches `CopyEngine.cs` L3935 | PASS | CHANGE 7 line: `_engine.ArmPendingBe(_instrument, _leaderAccount, _beBuffer)`. Source L3935 confirms `internal void ArmPendingBe(Instrument instr, Account masterAcc, int bufferTicks)`. Parameter order (Instrument, Account, int) matches. AC-04 also enforces this. |
| TR-07 | ComputeInstrSplit arithmetic: `t1=(n+1)/2`, `t2=n/2`; all 4 test cases verified: 4→(2,2), 5→(3,2), 1→(1,0), 7→(4,3) | PASS | CHANGE 4 implementation: `((instrQxT1 + 1) / 2, instrQxT1 / 2)`. Integer arithmetic verified: (4+1)/2=2, 4/2=2; (5+1)/2=3, 5/2=2; (1+1)/2=1, 1/2=0; (7+1)/2=4, 7/2=3. All 4 correct. |
| TR-08 | 4 [Fact] test methods with exact names; xUnit only; call `TradeCopierPanel.ComputeInstrSplit` directly | PASS | CHANGE 8: `QxInstrSplit_Even_T1EqualT2`, `QxInstrSplit_Odd_T1Heavier`, `QxInstrSplit_One_BothOne`, `QxInstrSplit_Large_Odd`. `using Xunit; [Fact]` only. No NUnit. No MSTest. Each test calls `TradeCopierPanel.ComputeInstrSplit(n)` directly. |
| TR-09 | AC-01 through AC-07 all present; AC-01 confirms visual tree order; AC-04 confirms `_engine.ArmPendingBe`; AC-05 confirms `internal static` | PASS | All 7 acceptance criteria present. AC-01: visual tree order correct. AC-04: `_engine.ArmPendingBe` (not GlobalBe). AC-05: `ComputeInstrSplit` is `internal static`. |
| TR-10 | NT8 constraints: RepeatButtons use `SetResourceReference(Control.StyleProperty, "NTButtonStyle")`; no direct WPF style assignment; no hardcoded hex | PASS | CHANGE 3: all 4 controls (`instrQxUp`, `instrQxDn`, `_instrQxBtn`, `_instrBeBtn`) use `SetResourceReference(Control.StyleProperty, "NTButtonStyle")`. Colors use existing `BrushTeal` brush. No hex literals. Pattern matches L1262/L1278 source. |
| TR-11 | ASCII compliance: `"[PTT-QX-INSTR]"` and `"[PTT-BE-INSTR]"` are ASCII-only; no `lock()`; no `async void`; no `return null` in new methods | PASS | Both log prefix strings are 7-bit ASCII. `\u25B2`/`\u25BC` are pre-existing spinner pattern (accepted per plan R-11). 0 `lock()` in all 6 new methods. All handlers are synchronous `void` (not `async void`). Null-checks use `return;` (void return). |

---

## Violations

*None.*

---

## Source Cross-Reference (spot-check by this reviewer)

| Claim in Ticket | Source Verification | Status |
|-----------------|-------------------|--------|
| `ArmPendingBe(Instrument, Account, int)` at `CopyEngine.cs` L3935 | Read L3930-3955: `internal void ArmPendingBe(Instrument instr, Account masterAcc, int bufferTicks)` confirmed at L3935 | PASS |
| `_beRowPanel` at L914, `_quickRowPanel` at L915 | Plan review R-01 and plan §"Source Cross-Reference" both confirm. Plan reviewer read source directly. | PASS |
| `ComputeInstrSplit` arithmetic | 4 integer division cases manually verified above | PASS |
| `SetResourceReference(Control.StyleProperty, "NTButtonStyle")` is pre-existing NT8 pattern | Plan review R-11 confirmed at L1262/L1278 in source | PASS |

---

## Recommendation

**TICKET_REVIEW_PASS → proceed to Ph4a (ptt-engineer)**

All 11 checks passed. The ticket is architecturally aligned with the REVIEW_PASS plan, fully traceable to REQ-01..REQ-07, JS-compliant, arithmetically verified, and carries the complete 7-scan engineer contract (SCAN-01..SCAN-07). No violations found.

The engineer reads this review and `04-tickets.md`, implements CHANGE 1 through CHANGE 8, runs all 7 scans to zero, and returns BUILD_PASS.

The verifier (Ph4b) independently re-runs SCAN-01..SCAN-07 against the actual source and cross-checks against the engineer's `ticket-1-completion.md` attestation.
