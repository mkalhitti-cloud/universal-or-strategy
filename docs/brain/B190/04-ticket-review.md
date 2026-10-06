# Ticket Review: B190

**Epic**: PTT-COPIER B190 | Per-Instrument Tick Defaults + Spinner Layout Fix + BE Default=0 + Deferred Closures
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-07-11
**Review cycle**: RE-REVIEW after Repair Cycle 1
**Source tickets**: `docs/brain/B190/04-tickets.md`
**Source plan**: `docs/brain/B190/02-architecture-plan.md` (REVIEW_PASS 2026-07-11)
**Spec block**: `specs/002-trade-copier-spec.html` B190 (lines 41545-42035)
**Rules catalog**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## Repair Cycle 1 Fixes — Verification

| Violation | Fix claimed | Verified |
|-----------|-------------|---------|
| V1: T_B190_T1_01 name mismatch | `InstrumentDefaults_MES_Returns4And8` | CONFIRMED -- matches spec line 41852 |
| V2: T_B190_T1_02 name mismatch | `InstrumentDefaults_MGC_Returns10And20` | CONFIRMED -- matches spec line 41853 |
| V3: T_B190_T1_03 name mismatch | `InstrumentDefaults_Unknown_ReturnsMesDefault` | CONFIRMED -- matches spec line 41854 |
| V3: T_B190_T1_04 name mismatch | `SetInstrument_MES_SetsQuickT1_4` | CONFIRMED -- matches spec line 41855 |
| V3: T_B190_T1_05 name mismatch | `SetInstrument_MGC_SetsQuickT1_10` | CONFIRMED -- matches spec line 41856 |
| V4: T6 file path in Tests/ subfolder | Corrected to `src/PropTraderTools/CopyEngineTests.cs` | CONFIRMED -- T6 Files table uses root path |

All Repair Cycle 1 fixes verified present and correct.

---

## T1 -- InstrumentDefaults MGC update + SetInstrument() tick reset

**Traceability**: PASS
- B190-T1 spec block confirmed at spec lines 41547-41863.
- AC1 (GetQuickTicks("MES")==(4,8)) → T_B190_T1_01. AC2 (MGC=(10,20)) → T_B190_T1_02.
- AC3 (unknown fallback) → T_B190_T1_03. AC4+AC5 (SetInstrument reset) → T_B190_T1_04, T_B190_T1_05.
- All ticket items trace to plan T1 section and spec AC1-AC5.

**JS Pre-Check**: PASS
- JS-021: No `lock()` in `GetQuickTicks()` or `SetInstrument()`. Explicitly contracted.
- JS-002: `GetQuickTicks()` returns `(int t1, int t2)` value tuple -- never null. `SetInstrument()` is void. Both PASS.
- JS-066: `GetQuickTicks` CYC=4 (unchanged). `SetInstrument` CYC=5 after change. Both <= 8. PASS.
- JS-081: All new/modified string literals ASCII-only: `"MGC"`, `"Ready: "`, `"Quick"`, `"Quick2t"`, `""`. No Unicode.
- JS-023: `SetInstrument()` is UI-thread-only (called from NT8 AddOn on UI thread). No `Dispatcher.InvokeAsync` added. PASS.

**CYC Pre-Check**: PASS
- `GetQuickTicks`: CYC=4 (null/empty guard + MES branch + MGC branch + default = 3 branches + 1). No structural change. <= 8.
- `SetInstrument`: CYC=5 (existing `_statusText` null+instrument null guard = 2; new `_quickBtn` null(3) + `_instr2tBtn` null(4) + base = 5). <= 8.

**NT8 Check**: PASS
- No `async/await` in lifecycle methods.
- No `Account.All` call.
- `Instrument.MasterInstrument.Name` accessed via null-conditional chain -- confirmed NT8 API usage, pattern already present in PttGlobalQuickExit.cs and CopyEngine.cs.
- No `CreateOrder`. No `DateTime.Now`. No `FontFamily`. No hardcoded hex color.
- xUnit proxy tests (T_B190_T1_04, T_B190_T1_05) test `InstrumentDefaults.GetQuickTicks()` directly -- NO NT8 runtime calls in test harness. NOT marked `[Fact(Skip=...)]`. Correct.

**Test Coverage**: PASS
- 5 [Fact] tests specified: T_B190_T1_01 through T_B190_T1_05.
- All 5 test names match spec xUnit registry exactly (lines 41852-41856).
- Every new/modified public/internal method (`GetQuickTicks`, `SetInstrument` post-change) has test coverage (direct or proxy). PASS.

**Scan Checklist**: PASS
- SCAN-01: `dotnet build` with expected 0 errors 0 warnings. Present.
- SCAN-02: `grep lock(` scoped to `PttQuickExit.cs` and `TradeCopierPanel.cs`. Present.
- SCAN-03: `grep async void` same scope. Present.
- SCAN-04: `grep return null` same scope. Present.
- SCAN-05: CYC manual check -- `GetQuickTicks=4`, `SetInstrument=5`. Present.
- SCAN-06: Non-ASCII grep on new/modified lines. Present.
- SCAN-07: `ptt-sync.ps1` with expected `19 linked, 0 errors`. Present.
All 7 scans present and complete.

**File Routing**: PASS
- `src/PropTraderTools/Features/PttQuickExit.cs` -- Wave workspace. PASS.
- `src/PropTraderTools/TradeCopierPanel.cs` -- Wave workspace. PASS.
- `src/PropTraderTools/Tests/B190Tests.cs` -- Wave workspace, Tests/ subfolder for new test file. PASS.

**VERDICT: TICKET_REVIEW_PASS**

---

## T2 -- BuildInstrRow() spinner layout fix

**Traceability**: PASS
- B190-T2 spec block confirmed. AC6 (arrows docked right), AC7 (RepeatButton hold auto-fire), AC8 (no +/- buttons).
- `BuildInstrRow()` replacement and new `BuildSpinnerCluster()` method match plan T2 exactly.
- T_B190_T2_01 and T_B190_T2_02 match spec xUnit registry lines 41857-41858.
- All ticket items trace to plan T2 section and spec AC6-AC8.

**JS Pre-Check**: PASS
- JS-021: No `lock()` in `BuildInstrRow()` or `BuildSpinnerCluster()`. Explicitly contracted.
- JS-002: `BuildSpinnerCluster()` always returns `cluster` (DockPanel) -- never null. PASS.
- JS-066: `BuildInstrRow` CYC=1 (2 sequential calls, no branches). `BuildSpinnerCluster` CYC=1 (sequential construction, no branches). Both <= 8. PASS.
- JS-081: `"^"` (0x5E), `"v"` (0x76), `"Quick2t"`, `"QAll2t"`, `"NTButtonStyle"` all ASCII. `BrushTeal` + `System.Windows.Media.Brushes.White` are named constants -- no hardcoded hex literals. No `FontFamily`. PASS.
- JS-023: Both methods are UI-thread-only (called from `BuildUI()` from ctor). PASS.

**CYC Pre-Check**: PASS
- `BuildInstrRow`: CYC=1. <= 8.
- `BuildSpinnerCluster`: CYC=1. <= 8.

**NT8 Check**: PASS
- No `async/await`. No `Account.All`. No `CreateOrder`. No `DateTime.Now`.
- `SetResourceReference(Control.StyleProperty, "NTButtonStyle")` is an existing pattern -- not a lifecycle violation.
- `CopyEngine.Instance.GlobalQuickAllT1` in `BuildInstrRow` -- existing pattern per plan. Not a new introduction.
- No `FontFamily`. No hardcoded hex color.
- T_B190_T2_01 and T_B190_T2_02: engineering note permits `[Fact(Skip="NT8-runtime")]` ONLY if ctor is genuinely impossible without NT8. Both tests specified as `[Fact]` first (preferred). PASS.

**Test Coverage**: PASS
- 2 [Fact] tests specified: `T_B190_T2_01_BuildSpinnerCluster_ReturnsDockPanel` and `T_B190_T2_02_BuildSpinnerCluster_GridDockedRight`.
- Both test names match spec registry lines 41857-41858.
- New method `BuildSpinnerCluster` (private) has both structural tests. `BuildInstrRow` (delegating wrapper, CYC=1) has no independent test -- acceptable: its behavior is fully exercised through `BuildSpinnerCluster` tests and NT8 SIM verification (AC6-AC8). PASS.

**Scan Checklist**: PASS
- SCAN-01 through SCAN-07 all present.
- SCAN-01: `dotnet build` with ref parameter and RoutedEventHandler compile check. Present.
- SCAN-02–04: `lock(`, `async void`, `return null` greps scoped to `TradeCopierPanel.cs`. Present.
- SCAN-05: CYC check for both methods. Present.
- SCAN-06: ASCII-only check on new string literals including `"^"`, `"v"`. Present.
- SCAN-07: `ptt-sync.ps1` with `19 linked`. Present.
All 7 scans present and complete.

**File Routing**: PASS
- `src/PropTraderTools/TradeCopierPanel.cs` -- Wave workspace. PASS.
- `src/PropTraderTools/Tests/B190Tests.cs` -- Wave workspace. PASS.

**VERDICT: TICKET_REVIEW_PASS**

---

## T3 -- BE default = 0

**Traceability**: PASS
- B190-T3 spec block confirmed. AC9 (BE button shows "BE" on open), AC10 (0-tick buffer click), AC11 (BE ALL unchanged).
- Field initializer change `_beBuffer = 1` → `_beBuffer = 0` matches plan T3 exactly.
- T_B190_T3_01 matches spec xUnit registry line 41859.
- All ticket items trace to plan T3 section and spec AC9-AC11.

**JS Pre-Check**: PASS
- JS-021: No `lock()` -- field initializer change only, no method added. PASS.
- JS-066: No method CYC change. Field initializer has no branches. PASS.
- JS-081: New comment `"// B190-T3: default exact BE (no buffer)"` is ASCII-only. PASS.
- JS-023: `_beBuffer` is UI-thread-only (read/written by UI event handlers `OnBeUp`/`OnBeDown`/`OnBeClick`). NT8-003 confirmed. PASS.

**CYC Pre-Check**: PASS
- No method modified. CYC delta = 0. Confirmed explicitly in SCAN-05.

**NT8 Check**: PASS
- No `async/await`, no `Account.All`, no `CreateOrder`, no `DateTime.Now`.
- Field initializer only -- no new NT8 API usage.
- `T_B190_T3_01` proxy-tests `FormatGlobalBeBuffer("BE", 0)` -- pure BCL logic, no NT8 runtime call. PASS.

**Test Coverage**: PASS
- 1 [Fact] test: `T_B190_T3_01_BeBuffer_Default_IsZero`.
- Test name matches spec registry line 41859.
- Single field initializer change (no new method): proxy test via `FormatGlobalBeBuffer` is the correct observable gate for `_beBuffer=0`. PASS.

**Scan Checklist**: PASS
- SCAN-01 through SCAN-07 all present.
- SCAN-01: `dotnet build`. Present.
- SCAN-02–04: `lock(`, `async void`, `return null` greps. Present.
- SCAN-05: CYC delta = 0 confirmation. Present.
- SCAN-06: ASCII-only check on new comment. Present.
- SCAN-07: `ptt-sync.ps1` with `19 linked`. Present.
All 7 scans present and complete.

**File Routing**: PASS
- `src/PropTraderTools/TradeCopierPanel.cs` -- Wave workspace. PASS.
- `src/PropTraderTools/Tests/B190Tests.cs` -- Wave workspace. PASS.

**VERDICT: TICKET_REVIEW_PASS**

---

## T4 -- Restore RULES_CATALOG.md (DW-B183-05)

**Traceability**: PASS
- DW-B183-05 confirmed in spec deferred table (line 41895-41898): "RULES_CATALOG.md truncated at JS-041 (ops task)". P1.
- Current state confirmed: `RULES_CATALOG.md` ends at JS-041 (no JS-066, no JS-081 matches found in catalog). Ticket correctly describes the gap.
- Change (append JS-042..JS-110) matches plan T4 exactly.

**JS Pre-Check**: N/A -- docs-only ticket. No C# code changes. Appropriately documented in ticket.

**CYC Pre-Check**: N/A -- no methods. Documented.

**NT8 Check**: N/A -- no .cs files touched.

**Test Coverage**: PASS -- No xUnit tests required for docs-only ticket. Verification by grep commands specified in ticket. Correct per spec (spec line 41860: "-- T4: no new tests").

**Scan Checklist**: PASS
- SCAN-01 through SCAN-05: All marked N/A with explicit `DOCUMENT:` annotation. Correct adapted format for docs-only ticket.
- SCAN-06 (ADAPTED): Content verification greps for `JS-066` and `JS-081` with expected: at least 1 match each. Present.
- SCAN-07 (ADAPTED): UTF-8 no-BOM encoding check + `ptt-sync.ps1` confirmation. Present.
All 7 scans present (5 N/A with documentation + 2 adapted). PASS.

**Scope Isolation**: PASS -- Only `docs/standards/jane-street/RULES_CATALOG.md` touched. No `.cs` files. Confirmed by ticket Files table.

**File Routing**: PASS -- Not a `.cs` file; Wave workspace `.cs` constraint does not apply to docs/. PASS.

**VERDICT: TICKET_REVIEW_PASS**

---

## T5 -- Spec HTML update (DW-B185-BE-ALL-SCOPE-01)

**Traceability**: PASS
- DW-B185-BE-ALL-SCOPE-01 confirmed in spec deferred table (line 41905-41908): "Spec HTML update for instrument-scoped BE ALL". P2.
- Changes (B39 scope row text + closed DW card) match plan T5 exactly.

**JS Pre-Check**: N/A -- HTML-only ticket. No C# code changes. Appropriately documented.

**CYC Pre-Check**: N/A -- no methods. Documented.

**NT8 Check**: N/A -- no .cs files touched.

**Test Coverage**: PASS -- No xUnit tests required for HTML-only ticket. Verification by grep commands specified. Correct per spec (spec line 41861: "-- T5: no new tests").

**Scan Checklist**: PASS
- SCAN-01 through SCAN-05: All marked N/A with explicit `DOCUMENT:` annotation. Correct adapted format.
- SCAN-06 (ADAPTED): Content verification greps for `_instrument only` (updated scope text) and `DW-B185-BE-ALL-SCOPE-01` (closed DW card). Expected: at least 1 match each. Present.
- SCAN-07 (ADAPTED): UTF-8 no-BOM encoding check for `specs/002-trade-copier-spec.html` + `ptt-sync.ps1` confirmation. Present.
All 7 scans present (5 N/A with documentation + 2 adapted). PASS.

**Scope Isolation**: PASS -- Only `specs/002-trade-copier-spec.html` touched. No `.cs` files. Confirmed by ticket Files table.

**File Routing**: PASS -- Not a `.cs` file; Wave workspace `.cs` constraint does not apply to specs/. PASS.

**VERDICT: TICKET_REVIEW_PASS**

---

## T6 -- FlatGuardLoop skip removal (DW-B183-02 + DW-B183-01)

**Traceability**: PASS
- DW-B183-02 (P2) and DW-B183-01 (P2) both confirmed in spec deferred table (lines 41910-41918).
- Change at `src/PropTraderTools/CopyEngineTests.cs` line ~8812: remove `[Fact(Skip="B180-2: method removed")]` matches plan T6 exactly.
- T_B190_T6_01 name (`FlatGuardLoop_StructuralContract_SkipRemoved` → existing `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard` un-skipped) matches spec xUnit registry line 41862.

**JS Pre-Check**: PASS
- JS-021: No `lock()` -- attribute removal only, no production code change. PASS.
- JS-066: No method CYC change -- no method body modified. PASS.
- JS-081: No new string literals added to production code. PASS.

**CYC Pre-Check**: PASS
- No method body modified. Attribute removal only. CYC delta = 0.

**NT8 Check**: PASS
- Test body uses only reflection metadata (`GetMethods`, parameter inspection). No `.Invoke()`. NT8 static constructor NOT triggered.
- No NT8 runtime required. Test can pass in plain `dotnet test`. PASS.
- No `async/await`, no `Account.All`, no `CreateOrder`, no `DateTime.Now`.

**Test Coverage**: PASS
- T_B190_T6_01: existing test un-skipped, 1 [Fact] test made active.
- Pre-condition check `grep -n "FlatGuardLoop" src/PropTraderTools/CopyEngine.cs` specified as MANDATORY before change. Present.
- Expected result documented: at least one match at line ~6768. Stale Skip reason confirmed per plan.
- `dotnet test --filter "Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard"` specified with expected: `1 passed, 0 failed, 0 skipped`. Present.
- DW-B183-01 Slate Gate audit (lines 8277-8441, 6 passing + 2 correctly-skipped [Fact] tests) specified as Ph5 reviewer action. Correctly scoped to Ph5, not engineer. PASS.

**T6 Pre-Condition Check**: PASS
- `grep -n "FlatGuardLoop" src/PropTraderTools/CopyEngine.cs` specified before edit.
- Skip text `[Fact(Skip="B180-2: method removed")]` exactly specified in BEFORE block.
- Stop-and-report-to-Director instruction if grep returns 0 matches. Present.

**Scan Checklist**: PASS
- SCAN-01 through SCAN-07 all present.
- SCAN-01: `dotnet build` after attribute removal. Present.
- SCAN-02–04: `lock(`, `async void`, `return null` greps scoped to `CopyEngineTests.cs`. Present.
- SCAN-05: CYC delta = 0. Present.
- SCAN-06: No new string literals -- ASCII check notes `[Fact]` attribute has no string content. Present.
- SCAN-07: `ptt-sync.ps1` with `19 linked`; ALSO RUN `dotnet test --filter`; ALSO RUN DW-B183-01 audit. Present.
All 7 scans present and complete.

**File Routing**: PASS
- `src/PropTraderTools/CopyEngineTests.cs` -- Wave workspace, root of PropTraderTools (NOT Tests/ subfolder). Correct per V4 fix. PASS.

**Scope Isolation**: PASS -- Production code unchanged. Test attribute removal only.

**VERDICT: TICKET_REVIEW_PASS**

---

## Aggregate Checks

### Spec Coverage (complete)

| Spec requirement | Covered by | Status |
|-----------------|-----------|--------|
| B190-T1 / AC1-AC5 | T1 | PASS |
| B190-T2 / AC6-AC8 | T2 | PASS |
| B190-T3 / AC9-AC11 | T3 | PASS |
| AC-DNA (0 lock, 0 async void, CYC<=8, ASCII, build clean) | T1+T2+T3 JS checks + scan checklists | PASS |
| DW-B183-05 (RULES_CATALOG restore) | T4 | PASS |
| DW-B185-BE-ALL-SCOPE-01 (spec HTML) | T5 | PASS |
| DW-B183-02 (FlatGuardLoop Skip) | T6 | PASS |
| DW-B183-01 (Slate Gate audit) | T6 (Ph5 action) | PASS |

No spec requirement in B190 scope is uncovered. No duplicate coverage.

### Deferred Items (carry forward confirmed NOT in scope)

| DW ID | Status in tickets |
|-------|------------------|
| DW-B183-03 | In deferred table only. Not in any ticket scope. PASS. |
| DW-B187-01 | In deferred table only. Not in any ticket scope. PASS. |

### Test Count Summary

| Ticket | [Fact] tests | Notes |
|--------|-------------|-------|
| T1 | 5 | T_B190_T1_01..05. Names match spec. |
| T2 | 2 | T_B190_T2_01..02. Names match spec. |
| T3 | 1 | T_B190_T3_01. Name matches spec. |
| T4 | 0 | Docs-only. Correct per spec. |
| T5 | 0 | HTML-only. Correct per spec. |
| T6 | 1 (un-skipped) | T_B190_T6_01 (existing test activated). |
| **Total** | **9** | 8 new + 1 un-skipped |

### ptt-sync.ps1 Presence (all .cs tickets)

| Ticket | ptt-sync in SCAN-07 | Expected count |
|--------|---------------------|----------------|
| T1 | Present | 19 linked |
| T2 | Present | 19 linked |
| T3 | Present | 19 linked |
| T6 | Present | 19 linked |
| T4/T5 | Adapted (encoding-only, sync noted) | 19 linked |

All .cs tickets include `ptt-sync.ps1` in SCAN-07. PASS.

---

## Overall: TICKET_REVIEW_PASS

All 6 tickets pass all checks. Repair Cycle 1 violations V1-V4 are confirmed resolved.

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | VERDICT |
|--------|-------------|-------------|--------------|----------|--------------|----------------|-------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T4 | PASS | N/A | N/A | N/A | PASS | PASS | PASS | **PASS** |
| T5 | PASS | N/A | N/A | N/A | PASS | PASS | PASS | **PASS** |
| T6 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |

**Return: TICKET_REVIEW_PASS**

*Written by: ptt-ticket-reviewer*
*Epic: B190*
*Phase: 3.5 (Ticket Review — RE-REVIEW Repair Cycle 1)*
*Gate: TICKET_REVIEW_PASS — engineer may proceed*
