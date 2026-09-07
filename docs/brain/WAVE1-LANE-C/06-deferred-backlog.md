# WAVE1-LANE-C Deferred Backlog

**Epic**: WAVE1-LANE-C
**Written by**: ptt-plan-reviewer (Phase 5 Final Sign-Off)
**Date**: 2026-09-07
**Status**: REVIEW_PASS — items below are non-blocking observations for future cycles

---

## Summary

WAVE1-LANE-C achieved REVIEW_PASS with zero P0 violations and zero blocking issues.
All 10 tickets received VERIFY_PASS. The items below are deferred for future improvement
and were identified during Phase 5 review.

---

## Deferred Items

### DW-C-01 — Spec/impl width drift in C-02
**Priority**: P2
**Target**: future block
**Status**: OPEN

C-02 ticket spec (04-tickets.md line 101) states `T_C02_03` should verify `Width == 120`.
Implementation uses `Width = 110` (adjusted via HOTFIX-FOLLOWER-LABEL-CLIP-01 to prevent
account name clipping). Test correctly validates Width=110. The spec document itself was not
updated to reflect this hotfix. Future: update 04-tickets.md C-02 to document Width=110
as the accepted value.

---

### DW-C-02 — WireModuleLicenses vs ApplyModuleLicenses name mismatch
**Priority**: P2
**Target**: future block
**Status**: OPEN

C-10 ticket spec named the helper `WireModuleLicenses`. Implementation reused the
pre-existing `ApplyModuleLicenses` (CCN=2), which is semantically equivalent. Test
T_C10_05 is named `T_C10_05_WireModuleLicenses_BeModule_...` but exercises
`ApplyModuleLicenses` via inline mirror. No code defect — behaviour correct.
Future: align spec name with actual implementation name, or document the deliberate reuse
in 04-tickets.md C-10.

---

### DW-C-03 — lizard CCN inflation for WPF object-initializer chains
**Priority**: P2
**Target**: future block
**Status**: OPEN

lizard 1.24 inflates CCN for WPF-heavy methods by counting property assignments in
`new Widget { Prop = val, ... }` initializer blocks as branch tokens. This produced
lizard CCN=29 for `BuildUI` (C-09) and 9-12 for its helpers despite true McCabe=1.
The same artifact was observed in C-04 helpers (lizard=13-14, true McCabe=1).

Established pattern: verifiers independently confirm zero real branches via regex scan.
This is documented in ticket-4-verification.md and ticket-9-verification.md.

Future: investigate lizard `--exclude-keywords` configuration or adopt a supplementary
complexity tool that correctly handles WPF object initializer syntax. Track in wave-level
tooling backlog.

---

### DW-C-04 — Pre-flight test template to prevent cycle 2 re-runs
**Priority**: P1
**Target**: B_next (next pipeline block)
**Status**: OPEN

Two tickets required cycle 2 re-runs:
- **C-01 cycle 1 VERIFY_FAIL**: Missing [Fact] tests. Engineer added compliance comment
  but forgot to add the actual test methods.
- **C-07 cycle 1 VERIFY_FAIL**: Test used `NinjaTrader.Cbi.Instrument instr = null`
  which fails to compile in the net8.0 test project (no NT8 dependency available in
  xUnit test TFM).

These are both preventable. Recommend adding to the ticket spec template:
1. A checklist item: "[x] xUnit [Fact] methods present AND committed to Wave1LaneCTests.cs"
2. A NT8-free mandate note: "Test parameters must use plain C# types only (object, bool,
   int, string). Never reference NinjaTrader.* types in test code."

This should be encoded in 04-ticket-review.md's Phase 3.5 checklist for future waves.

---

### DW-C-05 — Missing orchestration-level integration test for BuildInlineFollowerRow
**Priority**: P2
**Target**: future block
**Status**: OPEN

All five C-02 helpers are individually tested (T_C02_01..T_C02_05). The parent method
`BuildInlineFollowerRow` itself (which orchestrates the DockPanel layout and child
ordering) is not directly exercised by a test. The inline-mirror pattern used for all
LANE-C tests does not support multi-method orchestration tests due to TFM boundary
constraints (net48 WPF not available in net8.0 test project).

Future: explore a lightweight mock-WPF pattern or consider adding a `net48`-targeted
test assembly that can exercise the DockPanel layout directly. Low priority given that
behaviour equivalence was confirmed via source code review in ticket-2-verification.md.

---

## Items NOT Deferred (Resolved in LANE-C)

The following items from prior planning were fully resolved in this pipeline and require
no further action:

| Item | Resolution |
|------|-----------|
| CCN > 8 in all 10 target methods | Fully resolved: max post-extraction CCN = 6 (OnBeClick) |
| Test suite below 117 passing threshold | Fully resolved: 248 passing at pipeline completion |
| CopyEngine.cs hard boundary | Maintained throughout all 10 tickets |
| lock() (JS-021) violations | Zero live lock() in any LANE-C modified code |
| async void (JS-033) violations | Zero live async void in any LANE-C modified code |
| NinjaTrader refs in test project | Resolved in C-07 cycle 2 |
| Missing [Fact] tests | Resolved in C-01 cycle 2 |
