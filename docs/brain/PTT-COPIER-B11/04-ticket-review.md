# PTT-COPIER-B11 -- Ticket Review
# Reviewer: ptt-ticket-reviewer
# Phase: 3.5 (Ticket Review)
# Date: 2026-07-11 (CYCLE 2 of 2 -- FINAL)
# Tickets under review: docs/brain/PTT-COPIER-B11/04-tickets.md
# Plan under review:    docs/brain/PTT-COPIER-B11/02-architecture-plan.md (REVIEW_PASS Cycle 2)
# Verdict: TICKET_REVIEW_PASS

---

## CYCLE 2 Fix Verifications

### Fix 1 -- Key.F comment: no "market" implication, DW-B12-BUFFERED-BUTTONS-01 deferred
T1 `DispatchShortcut` comment reads:
> "Key.F: calls current CopyEngine.Flatten(_instrument) -- fires at market.
>  DW-B12-BUFFERED-BUTTONS-01 (B12) will add int exitBuffer parameter for
>  OrderType.Limit@bid+buffer. Known spec debt, explicitly deferred."

"Fires at market" is a factual description of the current engine state. Deferral to B12 is
named explicitly. DW-B12-BUFFERED-BUTTONS-01 is cited by ID.
RESULT: PASS

### Fix 2 -- Key.T comment: same deferral note
T1 `DispatchShortcut` comment reads:
> "Key.T: calls current CopyEngine.Trim(_instrument) -- fires at market.
>  DW-B12-BUFFERED-BUTTONS-01 (B12) will convert Trim to OrderType.Limit@ask-buffer.
>  Known spec debt, explicitly deferred."

Identical deferral pattern present.
RESULT: PASS

### Fix 3 -- DW-B11-DEFER-01 backlog item present
Tickets backlog section contains:

  | DW-B11-DEFER-01 | Convert Flatten/Trim shortcuts to Limit orders per
  | DW-B12-BUFFERED-BUTTONS-01. Key.F should emit OrderType.Limit@bid+buffer;
  | Key.T should emit OrderType.Limit@ask-buffer. Requires new
  | Flatten(Instrument, int exitBuffer) and Trim(Instrument, int exitBuffer)
  | signatures on CopyEngine. | B12 |

Item present, correctly scoped to B12, covers both Key.F and Key.T conversions and names
the required new engine signatures.
RESULT: PASS

### Fix 4 -- 02-architecture-plan.md §14 has DW-B12-BUFFERED-BUTTONS-01 in Deferred Items table
Plan §14 contains:
  DW-B12-BUFFERED-BUTTONS-01 | Convert Flatten(Instrument) to
  Flatten(Instrument, int exitBuffer)... | P1 | B11 Key.F/Key.T shortcuts fire at
  market (current engine API). Buffered-limit exits are spec-required but depend on
  new CopyEngine method signatures -- deferred to B12.

Row present with correct ID, description, priority P1, and rationale.
RESULT: PASS

### Fix 5 -- No new CopyEngine code (market-order path uses existing signatures)
T1 DispatchShortcut implementation contract:
  case Key.T: _engine.Trim(_instrument);                    break;
  case Key.F: _engine.Flatten(_instrument);                 break;
  case Key.C: _engine.CancelPendingEntries(_instrument);    break;
  case Key.B: ... _engine.BreakEven(_instrument, buf);      break;

All four calls target existing CopyEngine public methods. No new method added to
CopyEngine. Preamble states "Do NOT add a new engine method." Verified against actual
source: CopyEngine.cs line 806 internal void Flatten(Instrument instrument) and
line 767 internal void Trim(Instrument instrument) -- both exist, no new signatures.
RESULT: PASS

---

## Ticket T1 -- DW-B11-HK-01: Keyboard Shortcut Layer + Diag Cleanup + KB Update

### Traceability
Spec requirements in T1 header: DW-B11-HK-01, DW-B10-01, DW-B10-04, SIM101-gate.
All items map to plan §1 (T1 scope) and §13 (spec requirements table).
No phantom work. No missing plan items for T1 scope.
RESULT: PASS

### JS Pre-Check
| Rule   | Check                                                      | Result |
|--------|------------------------------------------------------------|--------|
| JS-021 | _keyHandlers = ConcurrentDictionary; all handlers on WPF  |        |
|        | UI thread; no lock() in any code snippet                   | PASS   |
| JS-001 | OnChartKeyDown / DispatchShortcut use guard-return; no     |        |
|        | throw in handler path                                      | PASS   |
| JS-002 | All void methods use guard-return (return;); no return null| PASS   |
| JS-033 | No async void in any new handler; Dispatcher.InvokeAsync   |        |
|        | lambda is not async void                                   | PASS   |
| JS-023 | _sim101KeyDiag is reference type; volatile not applicable  |        |
|        | nor present; _keyHandlers is readonly ConcurrentDictionary | PASS   |

Constraint table present in ticket with per-rule citations.
RESULT: PASS

### CYC Pre-Check
| Method              | CYC | Within 8? |
|---------------------|-----|-----------|
| OnChartKeyDiag      |  1  | YES       |
| HookKeyShortcut     |  2  | YES       |
| UnhookKeyShortcut   |  2  | YES       |
| RemoveSim101        |  2  | YES       |
| OnChartKeyDown      |  3  | YES       |
| DispatchShortcut    |  5  | YES       |

Highest: DispatchShortcut=5. All within limit.
RESULT: PASS

### NT8 Check
| Rule                                | Result |
|-------------------------------------|--------|
| NT8-003 no volatile double          | PASS -- no new double fields |
| NT8-001 no { get; init; }           | PASS -- no new properties |
| NT8-002 no abstract/sealed record   | PASS -- no new type declarations |
| NT8-004 no ImmutableDictionary      | PASS -- _keyHandlers is ConcurrentDictionary |
| NT8-007 CreateOrder PTT- prefix     | PASS -- no new CreateOrder calls; only _engine.* |
| ASCII-only string literals          | PASS -- "KB: ", " M=", all ASCII; listed in table |
| No FontFamily override              | PASS -- no new UI widgets with font override |
| No hardcoded hex color              | PASS -- no new Color.FromArgb |
| No DateTime.Now                     | PASS -- not used in T1 |
| Math.Clamp ban                      | PASS -- int.TryParse with default 2 used instead |
| No async/await in lifecycle method  | PASS -- OnWindowDestroyed unhook calls are synchronous |

NT8 constraint table present in ticket.
RESULT: PASS

### Test Coverage
Phase A (SIM101): No xUnit test. SIM101 IS the validation gate for PreviewKeyDown
feasibility. PreviewKeyDown cannot be simulated from xUnit test runner. Documented
exception -- rationale present and sound.

Phase B keyboard wiring: No xUnit test. Same justification -- WPF handler wired to
NT8 Chart Window; cannot be exercised from test runner.

DW-B10-01 (deletion work): No testable new public/internal methods; deletions leave
no new API surface requiring [Fact] tests.

DW-B10-04 (doc update): Documentation only. No testable code.

All four T1 items have no testable new logic accessible from xUnit. The absence of
[Fact] tests in T1 is a documented and justified exception, not a missing test.
RESULT: PASS

### Scan Checklist
SCAN-01 through SCAN-07 all present in T1 with exact grep commands and REQUIRED clauses:
  SCAN-01: lock() zero occurrences -- grep command present
  SCAN-02: async void zero (FlashBeFired exempt) -- grep command present
  SCAN-03: return null zero in new/modified methods -- grep command present
  SCAN-04: CYC > 8 zero -- manual count table present
  SCAN-05: volatile double/bool zero (new fields only) -- field-by-field verification present
  SCAN-06: Math.Clamp zero -- grep command present
  SCAN-07: ASCII-only string literals -- grep -Pn "[^\x00-\x7F]" command present

All 7 scans present. Engineer contract complete.
RESULT: PASS

### File Routing
  src/PropTraderTools/TradeCopierAddOn.cs -- Wave workspace (c:\WSGTA\universal-or-strategy\)
  src/PropTraderTools/TradeCopierPanel.cs -- Wave workspace
  docs/standards/NT8_ADDON_KNOWLEDGE.md  -- doc update, not a .cs file; correct workspace
No .cs paths point to Director workspace.
RESULT: PASS

### T1 VERDICT: TICKET_REVIEW_PASS

---

## Ticket T2 -- DW-B11-HK-02: Focus-Independence Verification + ATM Template Writer + Window Arm BE + 3 Tests

### Traceability
Spec requirements in T2 header: DW-B11-HK-02, DW-B10-02, DW-B10-03.
All items map to plan §1 (T2 scope) and §13 (spec requirements table).
SIM101 dependency note present: keyboard focus-independence step conditional on T1
PASS; ATM template writer, DW-B10-02, DW-B10-03 unblocked regardless.
No phantom work. No missing plan items for T2 scope.
RESULT: PASS

### JS Pre-Check
| Rule   | Check                                                        | Result |
|--------|--------------------------------------------------------------|--------|
| JS-021 | _armBeBtns accessed on UI thread only; no lock(); LoadAtm    |        |
|        | Templates and OnRuleArmBe use guard-return, no lock()        | PASS   |
| JS-001 | LoadAtmTemplates returns string[0] on IO fail -- no throw;   |        |
|        | OnRuleArmBe uses guard-return on null paths -- no throw      | PASS   |
| JS-002 | LoadAtmTemplates returns string[0] not null; all void methods|        |
|        | use guard-return (return;); no return null                   | PASS   |
| JS-033 | No async void in any new handler                             | PASS   |
| JS-023 | _atmTemplateCombo, _activeAtmTemplateName, _armBeBtns are    |        |
|        | all UI-thread-only fields; no volatile applied               | PASS   |

Constraint table present in ticket with per-rule citations.
RESULT: PASS

### CYC Pre-Check
| Method                         | CYC | Within 8? |
|--------------------------------|-----|-----------|
| BuildAtmTemplateRow            |  1  | YES       |
| GetAtmTemplatesDirectory       |  1  | YES       |
| LoadAtmTemplates               |  3  | YES       |
| OnAtmTemplateSelectionChanged  |  2  | YES       |
| OnRuleArmBe                    |  4  | YES       |

Highest: OnRuleArmBe=4. All within limit.
RESULT: PASS

### NT8 Check
| Rule                                | Result |
|-------------------------------------|--------|
| NT8-003 no volatile double          | PASS -- no new double fields |
| NT8-001 no { get; init; }           | PASS -- no new properties |
| NT8-002 no abstract/sealed record   | PASS -- no new type declarations |
| NT8-004 no ImmutableDictionary      | PASS -- LoadAtmTemplates uses string[], not ImmutableDictionary |
| NT8-007 CreateOrder PTT- prefix     | PASS -- no CreateOrder in T2; only _engine.ArmPendingBe() |
| ASCII-only string literals          | PASS -- "ATM:", "Arm BE", "tks", "2", "NinjaTrader 8",         |
|                                     |        "templates", "ATM", "*.xml" -- all ASCII; listed in table |
| No FontFamily override              | PASS -- no new font overrides |
| No hardcoded hex color              | PASS -- reuses existing WBrushInactive |
| No DateTime.Now                     | PASS -- not used in T2 |
| Math.Clamp ban                      | PASS -- int.TryParse with default 2 in OnRuleArmBe |
| sealed on TradeCopierWindow         | PASS -- no class declarations changed |

NT8 constraint table present in ticket.
RESULT: PASS

### Test Coverage
Three [Fact] test names explicitly present with full implementation bodies:

  StartAtrEngine_NullChart_DoesNotThrow
    Assert: Record.Exception(() => engine.ManualOnBarUpdate()) is null.
    Validates: AtrSizingEngine constructor + ManualOnBarUpdate cold-path robustness.

  StartAtrEngine_NullInstrument_DoesNotThrow
    Assert: Record.Exception(() => engine.SetParameters(150.0, 5.0)) is null.
    Validates: SetParameters cold-path robustness.

  UpdateAtrOverlay_FormatsDisplayString_CorrectText
    Assert: Contains "ATR=", "pts", "stopTicks=" in format string;
            AtrSizingEngine.CalcContracts(6.0, 150.0, 5.0) == 5.
    Validates: Format token contract and CalcContracts consistency.

New UI handler methods (BuildAtmTemplateRow, LoadAtmTemplates,
OnAtmTemplateSelectionChanged, OnRuleArmBe) have no [Fact] tests.
WPF event handlers and NT8-filesystem methods cannot be exercised from xUnit runner.
This is accepted -- the only testable subsystem added in T2 is AtrSizingEngine via its
test-seam constructor, and all three required [Fact] tests cover it.
RESULT: PASS

### Scan Checklist
SCAN-01 through SCAN-07 all present in T2 with exact grep commands and REQUIRED clauses:
  SCAN-01: lock() zero occurrences -- grep on 3 files
  SCAN-02: async void zero (FlashBeFired exempt) -- grep on 2 files
  SCAN-03: return null zero in new/modified methods -- per-method verification present
  SCAN-04: CYC > 8 zero -- method-by-method count table present
  SCAN-05: volatile double/bool zero (new fields only) -- field-by-field verification present
  SCAN-06: Math.Clamp zero -- grep on 2 files
  SCAN-07: ASCII-only string literals -- grep -Pn "[^\x00-\x7F]" on 2 files with literal list

All 7 scans present. Engineer contract complete.
RESULT: PASS

### File Routing
  src/PropTraderTools/TradeCopierPanel.cs  -- Wave workspace
  src/PropTraderTools/TradeCopierWindow.cs -- Wave workspace
  src/PropTraderTools/CopyEngineTests.cs   -- Wave workspace
No .cs paths point to Director workspace.
RESULT: PASS

### T2 VERDICT: TICKET_REVIEW_PASS

---

## Aggregate Checks

### Check 1 -- Traceability (per ticket)
T1: DW-B11-HK-01, DW-B10-01, DW-B10-04, SIM101-gate -- all map to plan §1/§13.
T2: DW-B11-HK-02, DW-B10-02, DW-B10-03 -- all map to plan §1/§13.
No phantom work. No missing plan items.
RESULT: PASS

### Check 2 -- Method signatures match plan §4
All 11 new method signatures in tickets match plan §4 exactly (access modifier,
static qualifier, return type, parameter types). Verified T1 vs plan §4.1 and T2 vs plan §4.2/4.3.
RESULT: PASS

### Check 3 -- JS rule constraints present and clean
Both tickets carry per-rule JS constraint tables. No violation described in any ticket.
RESULT: PASS

### Check 4 -- NT8 constraint tables present and clean
Both tickets carry per-rule NT8 constraint tables. No violation described in any ticket.
RESULT: PASS

### Check 5 -- 7-scan checklists SCAN-01 through SCAN-07 complete
T1: all 7 scans present with grep commands and REQUIRED clauses.
T2: all 7 scans present with grep commands and REQUIRED clauses.
RESULT: PASS

### Check 6 -- xUnit [Fact] test names present
T1: no new testable methods; documented exception (SIM101 + WPF handlers). PASS.
T2: 3 [Fact] tests present with full assertion bodies for AtrSizingEngine. PASS.
RESULT: PASS

### Check 7 -- CYC pre-check: all methods <= 8
All 11 new methods across both tickets confirmed <= 8.
Highest: DispatchShortcut=5. OnRuleArmBe=4.
RESULT: PASS

### Check 8 -- SIM101 Phase A before Phase B
T1 structure enforces Phase A first with explicit heading and preamble constraint.
Phase B heading states "execute ONLY if SIM101 PASS."
RESULT: PASS

### Check 9 -- RemoveSim101 on both PASS and FAIL paths
Phase A Step 3 table: PASS row calls RemoveSim101 first; FAIL row calls RemoveSim101 first.
RemoveSim101 implementation contract nulls field unconditionally.
RESULT: PASS

### Check 10 -- DW-B10-01 through DW-B10-04 all assigned
DW-B10-01: T1 (diag scaffolding removal). PASS.
DW-B10-02: T2 (3 AtrSizingEngine xUnit tests). PASS.
DW-B10-03: T2 (Arm BE cluster in TradeCopierWindow). PASS.
DW-B10-04: T1 (NT8_ADDON_KNOWLEDGE.md update). PASS.
All four present in ticket headers and cross-ticket dependency summary table.
RESULT: PASS

### Check 11 -- ATM template writer uses Directory.GetFiles
LoadAtmTemplates implementation contract: var files = Directory.GetFiles(dir, "*.xml");
Correct API. Not GetFileSystemEntries, not EnumerateFiles.
RESULT: PASS

### Check 12 -- DW-B10-03 Window Arm BE (OnRuleArmBe CYC <= 4)
CYC table: OnRuleArmBe=4. Implementation contract shows exactly 4 guard-return branches.
RESULT: PASS

### Check 13 -- Flatten/FlattenAll label vs method name resolved
Preamble states: "The actual callable method on CopyEngine is Flatten(Instrument).
No FlattenAll method exists." DispatchShortcut uses _engine.Flatten(_instrument).
Comment in DispatchShortcut: "Key.F calls _engine.Flatten(_instrument). The method
on CopyEngine is Flatten(), not FlattenAll(). 'FlattenAll' is the user-facing label
only; no new engine method is added." Verified against CopyEngine.cs line 806.
RESULT: PASS

### Check 14 -- Keyboard scope SIM101-conditional; non-keyboard SIM101-independent
Cross-ticket dependency table documents independence of DW-B10-01 through DW-B10-04
from SIM101 outcome. T2 SIM101 dependency note explicitly unblocks ATM writer, tests,
and Arm BE from SIM101 result. Plan §11 / tickets state all four DW-B10-xx items
MUST complete regardless of keyboard feasibility.
RESULT: PASS

### File Routing (aggregate)
All .cs files in both tickets route to Wave workspace
(c:\WSGTA\universal-or-strategy\src\PropTraderTools\). No Director workspace .cs paths.
RESULT: PASS

---

## B12 Spec Note (informational -- does NOT affect B11 verdict)

The B12 spec (spec lines 6452-6465, 6596-6626) for DW-B12-BUFFERED-BUTTONS-01 is
confirmed correct:

  Long exit:  Sell Limit @ bid + exitBuffer×tick
              (at buffer=1, sells at ask -- collects the spread)
  Short exit: Buy  Limit @ ask - exitBuffer×tick
              (at buffer=1, buys at bid -- collects the spread)

This is the correct direction (exit longs at ask, exit shorts at bid) with default
buffer=1. The ▲▼ RepeatButton spinner pairs on each button adjust the buffer live.
The DW-B11-DEFER-01 description in the B11 tickets is consistent with this.
No spec correction required. B12 ticket generation may proceed on this point.

---

## Summary Table

| Ticket | Traceability | JS Pre-Check | CYC | NT8 | Test Coverage | Scan 1-7 | File Routing | VERDICT |
|--------|-------------|-------------|-----|-----|---------------|----------|-------------|---------|
| T1     | PASS        | PASS        |PASS |PASS | PASS          | PASS     | PASS        | PASS    |
| T2     | PASS        | PASS        |PASS |PASS | PASS          | PASS     | PASS        | PASS    |

| CYCLE 2 Fix | Result |
|-------------|--------|
| Fix 1: Key.F deferral note | PASS |
| Fix 2: Key.T deferral note | PASS |
| Fix 3: DW-B11-DEFER-01 present | PASS |
| Fix 4: Plan §14 DW-B12-BUFFERED-BUTTONS-01 row | PASS |
| Fix 5: No new CopyEngine code | PASS |

---

## Overall: TICKET_REVIEW_PASS

All tickets PASS on all checks. All 5 Cycle-2 fixes verified present and correct.
Zero violations found. Zero warnings.

Phase 4 (engineer) is UNBLOCKED.
The ptt-orchestrator may spawn ptt-engineer to implement from 04-tickets.md.

---

*Cycle 2 of 2. This review is final. No further review cycles are permitted.*
*Reviewed by ptt-ticket-reviewer against docs/standards/jane-street/RULES_CATALOG.md,*
*docs/brain/PTT-COPIER-B11/02-architecture-plan.md (REVIEW_PASS),*
*and specs/002-trade-copier-spec.html.*
