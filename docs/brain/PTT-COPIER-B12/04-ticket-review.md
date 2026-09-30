# PTT-COPIER-B12 Ticket Review
# Block: PTT-COPIER-B12
# Date: 2026-07-11
# Author: ptt-ticket-reviewer (Phase 3.5)
# Cycle: 2 of 2 (final)
# Input: docs/brain/PTT-COPIER-B12/04-tickets.md (REVISED)
# Input: docs/brain/PTT-COPIER-B12/02-architecture-plan.md
# Input: docs/brain/PTT-COPIER-B12/02-plan-review.md
# Input: docs/standards/jane-street/RULES_CATALOG.md
# Input: docs/standards/NT8_COMPILER_RULES.md
# Status: TICKET_REVIEW_PASS

---

## Ticket Review: PTT-COPIER-B12

### Cycle 2 Summary

VIOLATION-01 from Cycle 1 has been resolved. The architect added:
1. An explicit **NT8 FQN NOTE** block at the top of T1 and T3 specifying that all
   `RepeatButton` references are `System.Windows.Controls.Primitives.RepeatButton`.
2. Fully qualified `System.Windows.Controls.Primitives.RepeatButton` at every occurrence
   in T1 §1.5 (7 occurrences) and T3 §3.2 (5 occurrences).

All 21 original checks have been re-confirmed. No new violations introduced.

---

### T1 -- DW-B12-BUFFERED-BUTTONS-01

**Traceability**: PASS
- DW-B11-DEFER-01 (CLOSED) cited in §Overview and Backlog Ledger.
- DW-B12-BUFFERED-BUTTONS-01 cited in §Overview.
- All methods trace to plan §4.1 (TradeCopierPanel), §4.4 (CopyEngine), §6 (CYC table).
- No phantom work (nothing in ticket absent from plan).
- No missing plan work (all T1 plan items present: BuildBufferedButtonsRow, FormatBuffer,
  OnTrimUp/Down/Click, OnFlattenUp/Down/Click, OnBeUp/Down/Click, UpdateBeLabel,
  UpdateBeVisuals, OnBeConnected, GetRefPrice, OnCopyToggle, OnCancel2,
  Trim(Instrument,int,double), Flatten(Instrument,int,double), DispatchCopy gate,
  DispatchShortcut modification).

**JS Pre-Check**: PASS
- JS-021 (P0): No lock() described in any T1 method. SCAN-01 enforces. PASS.
- JS-001 (P0): Trim/Flatten overloads wrap acc.CreateOrder in try/catch with StatusUpdate
  routing, no rethrow. Cited in §1.8 method comments. PASS.
- JS-002 (P0): All guards use bare `return;`, not `return null;`. SCAN-03 enforces. PASS.
- JS-033 (P0): OnBeConnected is `async void` invoked exclusively via
  `Dispatcher.InvokeAsync(() => OnBeConnected(instr))` (FlashBeFired replacement pattern).
  Cited in §1.5 OnBeConnected comment. SCAN-02 identifies it as the only permitted instance.
  PASS.
- JS-008 (P1): BrushConnected = `MakeBrush(59, 130, 246)` which calls Freeze(). Documented
  in §1.1 field comment. No hardcoded hex string. PASS.

**NT8 Check**: PASS
- NT8-003: All new fields plain int/double/bool with "no volatile" comment. SCAN-05. PASS.
- NT8-007: Both Trim and Flatten overloads use `(NinjaTrader.Cbi.CustomOrder)null` as
  arg 12. Cited inline in §1.8. PASS.
- NT8-013: DateTime.MaxValue used in CreateOrder calls (§1.8). No DateTime.Now. PASS.
- NT8-014: Signal names "PTT-TrimLimit" and "PTT-FlattenLimit" cited in §1.8. PASS.
- NT8-020: BrushConnected frozen via MakeBrush(). Cited in §1.1. PASS.
- NT8-028: No hex string literals. MakeBrush(59, 130, 246) in §1.1. PASS.
- Math.Clamp: Correctly avoided (Math.Max(Math.Min()) used). SCAN-06 enforces. PASS.
  WARN -- inline comment attributes this to "NT8-003" (the volatile double rule, not Math.Clamp
  unavailability). Misattribution in comments only; correct action is documented; not a
  blocking violation. (Carried from Cycle 1; see WARN-01 below.)

**CYC Pre-Check**: PASS
- All T1 methods: CYC <= 8.
- Highest: OnBeClick=5, Trim=5, Flatten=5, DispatchCopy=8 (AT LIMIT, PASS).
- Note: OnBeConnected body contains two if-guards = structural CYC=3, not CYC=2 as annotated.
  Well within limit. Documentation annotation error only; not a FAIL. (Carried from Cycle 1;
  see WARN-02 below.)
- All methods verified in CYC Summary table at end of tickets file.

**Test Coverage**: PASS
- T1-Test-1: `Trim_LimitOverload_LongPosition_EmitsSellLimitAtRefPlusTick` [Fact] -- present.
- T1-Test-2: `Trim_LimitOverload_ShortPosition_EmitsBuyToCoverLimitAtRefMinusTick` [Fact] -- present.
- T1-Test-3: `Flatten_LimitOverload_LongPosition_EmitsSellLimitFullQty` [Fact] -- present.
- T1-Test-4: `Flatten_LimitOverload_ShortPosition_EmitsBuyToCoverLimitFullQty` [Fact] -- present.
- T1-Test-5: `DispatchCopy_PttPrefixGate_SkipsOrderNamedPttTrimLimit` [Fact] -- present.
- All 5 required tests present. All use [Fact] (xUnit). No NUnit/MSTest. PASS.

**Scan Checklist**: PASS
Section 1.11 contains all 7 scans:
- SCAN-01: grep lock() -- JS-021 P0. PRESENT.
- SCAN-02: grep async void -- JS-033 P0. PRESENT.
- SCAN-03: grep return null -- JS-002 P0. PRESENT.
- SCAN-04: CYC audit all new/modified methods. PRESENT.
- SCAN-05: grep volatile -- NT8-003. PRESENT.
- SCAN-06: grep Math.Clamp -- NT8 .NET 4.8 ban. PRESENT.
- SCAN-07: grep literal Unicode arrows/bullets -- ASCII-only. PRESENT.

**File Routing**: PASS
- `src/PropTraderTools/TradeCopierPanel.cs` -- Wave workspace relative path. PASS.
- `src/PropTraderTools/CopyEngine.cs` -- Wave workspace relative path. PASS.
- No Director-workspace .cs path references found.

**RepeatButton FQN Check** (VIOLATION-01 re-verify): PASS
- NT8 FQN NOTE block present at top of T1 ticket explicitly stating:
  "All `RepeatButton` references in this ticket are
   `System.Windows.Controls.Primitives.RepeatButton`."
- §1.5 BuildBufferedButtonsRow: all 7 inline occurrences use the FQN:
  - Row 1 Col 0 Trim cluster: `System.Windows.Controls.Primitives.RepeatButton "\u25B2"` PRESENT.
  - Row 1 Col 0 Trim cluster: `System.Windows.Controls.Primitives.RepeatButton "\u25BC"` PRESENT.
  - Row 1 Col 1 Flatten cluster: `System.Windows.Controls.Primitives.RepeatButton "\u25B2"` PRESENT.
  - Row 1 Col 1 Flatten cluster: `System.Windows.Controls.Primitives.RepeatButton "\u25BC"` PRESENT.
  - Row 2 Col 1 BE cluster: `System.Windows.Controls.Primitives.RepeatButton "\u25B2"` PRESENT.
  - Row 2 Col 1 BE cluster: `System.Windows.Controls.Primitives.RepeatButton "\u25BC"` PRESENT.
  - Final cluster sentence: `System.Windows.Controls.Primitives.RepeatButton`s PRESENT.
- VIOLATION-01 RESOLVED. PASS.

**VERDICT**: TICKET_REVIEW_PASS

---

### T2 -- DW-B12-COLLAPSE-01

**Traceability**: PASS
- DW-B12-COLLAPSE-01 cited in §Overview.
- Methods trace to plan §4.2: BuildCollapsibleHeader, OnCollapseClick.
- Dependency on T1's `_contentPanel` (StackPanel) documented in §2.1 and §2.3.
- No phantom work. No missing plan work.

**JS Pre-Check**: PASS
- JS-021 (P0): No lock() described in T2 methods. SCAN-01 enforces. PASS.
- JS-001 (P0): No throw in T2 methods. PASS.
- JS-002 (P0): No return null in T2 methods. SCAN-03 enforces. PASS.
- JS-033 (P0): No new async void in T2. SCAN-02 correctly references T1's OnBeConnected
  as the only existing async void. PASS.

**NT8 Check**: PASS
- NT8-003: _isCollapsed is plain bool, no volatile. §2.1 documents this. SCAN-05. PASS.
- Unicode: "\u25B2" and "\u25BC" used in OnCollapseClick and BuildCollapsibleHeader body.
  No literal arrows. SCAN-07 enforces. PASS.
- No Math.Clamp. SCAN-06. PASS.

**CYC Pre-Check**: PASS
- BuildCollapsibleHeader: CYC=1. PASS.
- OnCollapseClick: CYC=2. PASS.

**Test Coverage**: PASS
- No unit tests required for T2 (pure WPF Visibility mutation, CYC=2, verifiable by F5
  visual inspection). Documented in §2.4. PASS.

**Scan Checklist**: PASS
Section 2.5 contains all 7 scans:
- SCAN-01: grep lock() -- JS-021 P0. PRESENT.
- SCAN-02: grep async void -- JS-033 P0. PRESENT.
- SCAN-03: grep return null -- JS-002 P0. PRESENT.
- SCAN-04: CYC of OnCollapseClick, BuildCollapsibleHeader. PRESENT.
- SCAN-05: grep volatile T2 fields -- NT8-003. PRESENT.
- SCAN-06: grep Math.Clamp -- NT8 .NET 4.8 ban. PRESENT.
- SCAN-07: literal arrows in T2 string content. PRESENT.

**File Routing**: PASS
- `src/PropTraderTools/TradeCopierPanel.cs` -- Wave workspace relative path. PASS.

**RepeatButton FQN Check**: PASS
- T2 introduces no RepeatButton references. No FQN issue applies. PASS.

**VERDICT**: TICKET_REVIEW_PASS

---

### T3 -- DW-B12-RISK-ATR-INPUTS-01

**Traceability**: PASS
- DW-B12-RISK-ATR-INPUTS-01 cited in §Overview.
- DW-B10-02 precedent noted (test coverage required for sizing engine logic).
- All methods trace to plan §4.3 (TradeCopierPanel), §4.4 (CopyEngine), §4.5 (AtrSizingEngine).
- No phantom work. No missing plan work.

**JS Pre-Check**: PASS
- JS-021 (P0): No lock() described in T3 methods. SCAN-01 enforces. PASS.
- JS-001 (P0): No throw in T3 methods. PASS.
- JS-002 (P0): All guards use bare `return;`. SCAN-03 enforces. PASS.
- JS-033 (P0): No new async void in T3. SCAN-02 enforces. PASS.

**NT8 Check**: PASS
- NT8-003: _maxRiskDollars, _atrFraction (panel) are plain double. _atrFraction (AtrSizingEngine)
  is plain double. All fields have "no volatile per NT8-003" comment. SCAN-05. PASS.
- Math.Clamp: Correctly avoided with Math.Max(Math.Min()). SCAN-06. PASS.
  WARN -- same comment misattribution as T1 ("no Math.Clamp (NT8-003)") in OnRiskUp body.
  Not a blocking violation. (See WARN-01 below.)
- No DateTime.Now, no hex strings, no PTT-prefix issues in T3. PASS.

**CYC Pre-Check**: PASS
- All T3 methods: CYC <= 8.
- Highest: OnRiskTextLostFocus=3, OnAtrFractionTextLostFocus=3, NotifyRiskChanged=2,
  NotifyAtrFractionChanged=2, OnRiskUp=1, OnRiskDown=1, OnAtrFractionUp=1, OnAtrFractionDown=1,
  BuildRiskAtrRow=1, UpdateMaxRisk(Engine)=2, UpdateAtrFraction(Engine)=2,
  SetAtrFraction(AtrEngine)=1, UpdateMaxRisk(AtrEngine)=1. All PASS.

**Test Coverage**: PASS
- T3-Test-1: `AtrSizingEngine_SetAtrFraction_ScalesCalcContractsDown_WhenFractionBelow1` [Fact] -- present.
- T3-Test-2: `UpdateMaxRisk_SetsAtrEngineMaxRiskDollars_ReflectsInSubsequentSizing` [Fact] -- present.
- T3-Test-3: `BuildRiskAtrRow_ClampMin_RejectsSubMinValue` [Fact] -- present.
- All 3 required tests present. All use [Fact] (xUnit). No NUnit/MSTest. PASS.

**Scan Checklist**: PASS
Section 3.7 contains all 7 scans:
- SCAN-01: grep lock() -- JS-021 P0. PRESENT.
- SCAN-02: grep async void -- JS-033 P0. PRESENT.
- SCAN-03: grep return null new T3 methods -- JS-002 P0. PRESENT.
- SCAN-04: CYC all new/modified T3 methods. PRESENT.
- SCAN-05: grep volatile T3 fields -- NT8-003. PRESENT.
- SCAN-06: grep Math.Clamp -- NT8 .NET 4.8 ban. PRESENT.
- SCAN-07: literal Unicode in T3 string literals. PRESENT.

**File Routing**: PASS
- `src/PropTraderTools/TradeCopierPanel.cs` -- Wave workspace relative path. PASS.
- `src/PropTraderTools/CopyEngine.cs` -- Wave workspace relative path. PASS.
- `src/PropTraderTools/AtrSizingEngine.cs` -- Wave workspace relative path. PASS.

**RepeatButton FQN Check** (VIOLATION-01 re-verify): PASS
- NT8 FQN NOTE block present at top of T3 ticket explicitly stating:
  "All `RepeatButton` references in this ticket are
   `System.Windows.Controls.Primitives.RepeatButton`."
- §3.2 BuildRiskAtrRow: all 5 inline occurrences use the FQN:
  - Col 0 Risk $ up: `System.Windows.Controls.Primitives.RepeatButton "\u25B2"` PRESENT.
  - Col 0 Risk $ down: `System.Windows.Controls.Primitives.RepeatButton "\u25BC"` PRESENT.
  - Col 1 ATR % up: `System.Windows.Controls.Primitives.RepeatButton "\u25B2"` PRESENT.
  - Col 1 ATR % down: `System.Windows.Controls.Primitives.RepeatButton "\u25BC"` PRESENT.
  - Final sentence: `Both System.Windows.Controls.Primitives.RepeatButton use NTButtonStyle` PRESENT.
- VIOLATION-01 RESOLVED. PASS.

**VERDICT**: TICKET_REVIEW_PASS

---

## Check #21 Cross-Ticket Verification

| Check | T1 | T2 | T3 | Result |
|-------|----|----|----|----|
| 1. Traceability | PASS | PASS | PASS | PASS |
| 2. Method signatures match plan | PASS | PASS | PASS | PASS |
| 3. JS rule constraints cited | PASS | PASS | PASS | PASS |
| 4. NT8 constraints cited | PASS | PASS | PASS | PASS |
| 5. 7-scan checklists present (T1,T2,T3) | PASS | PASS | PASS | PASS |
| 6. xUnit [Fact] tests (T1:5, T2:none, T3:3) | PASS | PASS | PASS | PASS |
| 7. CYC <= 8 all methods; DispatchCopy=8 PASS | PASS | PASS | PASS | PASS |
| 8. No volatile int/double/bool (NT8-003) | PASS | PASS | PASS | PASS |
| 9. Math.Clamp absent; Math.Max/Min used | PASS | PASS | PASS | PASS |
| 10. Arrows as \u25B2/\u25BC escape seqs | PASS | PASS | PASS | PASS |
| 11. RepeatButton as FQN (System.Windows.Controls.Primitives.RepeatButton) | PASS | n/a | PASS | PASS |
| 12. PTT-prefix gate in T1 DispatchCopy | PASS | n/a | n/a | PASS |
| 13. DW-B11-DEFER-01 closed (Flatten+Trim overloads) | PASS | n/a | n/a | PASS |
| 14. CopyEngine.UpdateMaxRisk/UpdateAtrFraction in T3 | n/a | n/a | PASS | PASS |
| 15. AtrSizingEngine SetAtrFraction/UpdateMaxRisk in T3 | n/a | n/a | PASS | PASS |
| 16. _contentPanel StackPanel wrapping documented | PASS | PASS | n/a | PASS |
| 17. BeState enum (Idle/Armed/Connected) + blue border | PASS | n/a | n/a | PASS |
| 18. FormatBuffer CYC=1 static helper in T1 | PASS | n/a | n/a | PASS |
| 19. File routing to Wave workspace | PASS | PASS | PASS | PASS |
| 20. No lock() in any ticket scope | PASS | PASS | PASS | PASS |
| 21. DW-B9-01/DW-B9-03 shelved to B13 | PASS | n/a | n/a | PASS |

**All 21 checks: PASS**

---

## Violation Summary

### VIOLATION-01 (RESOLVED in Cycle 2) -- Check #11 -- RepeatButton FQN

**Cycle 1 finding**: `System.Windows.Controls.Primitives.RepeatButton` FQN was absent from
T1 §1.5 (6 occurrences) and T3 §3.2 (4 occurrences).

**Cycle 2 resolution**: The architect added an explicit **NT8 FQN NOTE** block to both T1
and T3, and updated all inline RepeatButton references in T1 §1.5 and T3 §3.2 to use
the fully qualified type name. All 12 original occurrences (7 in T1, 5 in T3) now carry
`System.Windows.Controls.Primitives.RepeatButton`. RESOLVED.

---

## Warnings (Non-Blocking, Carried from Cycle 1)

### WARN-01 -- Math.Clamp Ban Misattributed to NT8-003

**Scope**: T1 §1.5 OnTrimUp/Down, OnBeUp/Down; T3 §3.2 OnRiskUp/Down/AtrFractionUp/Down
  inline code comments say `// no Math.Clamp (NT8-003)`.

**Issue**: NT8-003 bans `volatile double`, not Math.Clamp. Math.Clamp is unavailable because
  it was introduced in .NET Core 2.0 / .NET 5 and is absent from .NET Framework 4.8.
  There is no NT8-NNN rule for Math.Clamp -- it is a pure .NET version constraint.

**Impact**: None on correctness. Math.Max(Math.Min()) is correctly specified in all cases.
  SCAN-06 correctly labels it "NT8 .NET 4.8 ban". The misattribution is in inline code
  comments only.

**Recommendation**: Consider adding NT8-031 to NT8_COMPILER_RULES.md to document this
  constraint formally with the correct attribution.

### WARN-02 -- OnBeConnected CYC Annotation Off by One

**Scope**: T1 §1.5 OnBeConnected comment and CYC Summary table.
**Issue**: Body contains 2 if-guards (`if (_beBtn2 == null)` and `if (_instrument != null)`)
  giving structural CYC=3, but ticket annotates CYC=2.
**Impact**: None. CYC=3 is well within the limit of 8.
**Recommendation**: Update annotation to CYC=3 for accuracy.

---

## Overall: TICKET_REVIEW_PASS

**Cycle 2 Result**: All 3 tickets PASS. All 21 checks PASS. VIOLATION-01 resolved.
No blocking violations remain. No new violations introduced by the Cycle 2 revision.

**Engineer green light**: ptt-engineer may proceed with Phase 4a implementation using
`docs/brain/PTT-COPIER-B12/04-tickets.md` as the contract. Engineer must read
`docs/brain/PTT-COPIER-B12/04-ticket-review.md` FIRST (this file), then execute tickets
in order T1 → T2 → T3 (T2 depends on T1's `_contentPanel`).

---

*ptt-ticket-reviewer gate output. Phase 3.5 Cycle 2 complete.*
