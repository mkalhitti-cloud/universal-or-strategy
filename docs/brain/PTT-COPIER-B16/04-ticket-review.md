# PTT-COPIER-B16 Ticket Review
# Reviewer: ptt-ticket-reviewer
# Phase: 3.5 (Ticket Review)
# Date: 2026-07-14
# Tickets reviewed: docs/brain/PTT-COPIER-B16/04-tickets.md
# Plan reviewed:    docs/brain/PTT-COPIER-B16/02-architecture-plan.md (REVIEW_PASS)
# Rules applied:    docs/standards/jane-street/RULES_CATALOG.md
#                   docs/standards/NT8_COMPILER_RULES.md
# Source verified:  c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs
#                   c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs

---

## Ticket Review: PTT-COPIER-B16

---

### T1 — ChartPanel Subtree Diagnostic

#### A. Traceability

| Check | Result | Evidence |
|-------|--------|----------|
| References DW-B16-01 | PASS | T1.1 metadata table: `DW-B16-01 (partial — T1 is the investigation phase)` |
| References plan section C | PASS | T1.2 Goal and T1.6 method signatures align exactly with plan §C.2/C.3/C.4 |
| No phantom work (items not in plan) | PASS | Every T1 item (field, using directives, SetChart mod, WalkChartPanelChildren, BuildMethodReport) appears in plan §C |
| No missing plan work | PASS | Plan §C.1-C.6 fully covered: field (C.1), SetChart (C.2), WalkChartPanelChildren (C.3), BuildMethodReport (C.4), MessageBox output record (C.5), F5 gate (C.6) |

**Section verdict: PASS**

---

#### B. 7-Scan Checklist Presence

All 7 scans present in T1.10 with correct scan commands and expected results.

| Scan | Present | Command | Expected Result | Correct? |
|------|---------|---------|-----------------|---------|
| SCAN-01: lock() | YES | `grep -n "lock(" ...TradeCopierPanel.cs` | 0 results | YES |
| SCAN-02: async void | YES | `grep -n "async void" ...TradeCopierPanel.cs` | 0 results | YES |
| SCAN-03: DateTime.Now | YES | `grep -n "DateTime\.Now[^U]" ...TradeCopierPanel.cs` | 0 results | YES |
| SCAN-04: hex color | YES | `grep -n '"#[0-9A-Fa-f]' ...TradeCopierPanel.cs` | 0 results | YES |
| SCAN-05: GetValueByY | YES | `grep -n "\.GetValueByY(" ...TradeCopierPanel.cs` | 0 results (comments OK) | YES |
| SCAN-06: price=0.0 stub | YES | `grep -n "price\s*=\s*0\.0" ...TradeCopierPanel.cs` | 1 result (stub present in T1) | YES |
| SCAN-07: T_B16_ test names | YES | `grep -n "T_B16_" ...CopyEngineTests.cs` | 0 results (no T1 tests) | YES |

All 7 scans present, correctly scoped to Wave workspace (`c:\WSGTA\universal-or-strategy\`). Per-ticket scan checklist is the engineer's contract and verifier's anchor (Layer 1 of the 3-layer quality chain).

**Section verdict: PASS**

---

#### C. JS Pre-Check

| Rule | Constraint | T1 Code | Result |
|------|-----------|---------|--------|
| JS-021 | No `lock()` | No lock pattern in any T1 code block | PASS |
| JS-023 | `volatile bool` for cross-thread state | `private volatile bool _chartScaleDiagDone = false;` — confirmed volatile per T1.5 Step 2 | PASS |
| JS-033 | No `async void` | All new methods are `private void` or `private static string` — none async | PASS |
| JS-002 | No `return null` | `BuildMethodReport` returns `sb.ToString()` (never null); `WalkChartPanelChildren` uses bare `return;` for void | PASS |

**Section verdict: PASS**

---

#### D. CYC Pre-Check

| Method | Claimed CYC | Branches Enumerated | Within Budget? |
|--------|-------------|---------------------|----------------|
| `SetChart` (T1 modified) | 2 | base(1) + `if (!_chartScaleDiagDone)` guard(2) | YES (≤8) |
| `WalkChartPanelChildren` | 5 | guard cc null(1) + guard panel null(2) + for-loop body(3) + FrameworkElement is-check(4) + base(5) | YES (≤8) |
| `BuildMethodReport` | 2 | foreach(1) + `if (!match) continue` early-exit(2) | YES (≤8) |

All methods ≤ 8. No split required.

**Section verdict: PASS**

---

#### E. NT8 Constraints

| Check | Result | Evidence |
|-------|--------|----------|
| `using System.Reflection` added in T1 | PASS | T1.5 Step 1: explicitly adds `using System.Reflection;` citing NT8-031 |
| `using System.Text` added in T1 | PASS | T1.5 Step 1: explicitly adds `using System.Text;` with note it was absent before B16 |
| Volatile bool `_chartScaleDiagDone` | PASS | T1.5 Step 2, T1.7 JS table row NT8-017: `private volatile bool _chartScaleDiagDone = false;` |
| MessageBox.Show (not `_statusText`) | PASS | T1.2 Goal: "Output all findings to `System.Windows.MessageBox.Show`". T1.5 Step 4 uses `System.Windows.MessageBox.Show(sb.ToString(), "PTT B16 ChartPanel Subtree")` |
| One-shot guard pattern | PASS | `_chartScaleDiagDone = true` is the FIRST statement in `WalkChartPanelChildren` (set immediately — re-entry guard); guard checked in `SetChart` before call |
| RoundToTickSize flagged UNCONFIRMED | PASS | T1.7 row NT8-029: `RoundToTickSize (UNCONFIRMED)` (T2 scope); plan §F confirms; T1 does not call it |
| ChartPanel.MaxValue/MinValue flagged | PASS | Both flagged UNCONFIRMED in plan §F; T1 does not use them (T2 scope) |
| Files-not-to-touch respected | PASS | T1.4 lists: CopyEngine.cs, TradeCopierAddOn.cs, TradeCopierWindow.cs, AtrSizingEngine.cs, CopyEngineTests.cs |
| `VisualTreeHelper` dependencies met | PASS | `System.Windows.Media` already imported in TradeCopierPanel.cs (line 110, verified from source read). No new using needed for VisualTreeHelper |
| `FindVisualChild<>` availability | PASS | T1 calls `TradeCopierAddOn.FindVisualChild<ChartControl>` and `<ChartPanel>` — both confirmed available per plan §F (NT8-008, B15) |
| No async/await in lifecycle methods | PASS | SetChart and WalkChartPanelChildren are synchronous void methods |

**Section verdict: PASS**

---

#### F. Completeness

| Check | Result | Evidence |
|-------|--------|----------|
| F5 gate instruction present | PASS | T1.11 BUILD_PASS criteria items 1-3 cover F5 gate; T1.5 Step 6 has a 7-point F5 procedure |
| NT8_ADDON_KNOWLEDGE.md update instruction present | PASS | T1.9 explicitly states engineer MUST update before filing completion; T1.5 Step 7 has exact markdown template |
| No test requirement (correct for T1) | PASS | T1.8: "None. T1 adds diagnostic code that depends on NT8 runtime visual tree state." Correct — no pure-math test surface exists |
| T2 gated on T1 VERIFY_PASS | PASS | T1.1 metadata: `Gates: T2 is BLOCKED until T1 VERIFY_PASS`; T1.12 Gate Statement repeats this explicitly |
| NT8_COMPILER_RULES.md update instruction | PASS | T1.5 Step 8 and T1.11 item 5: conditional update on CS error; instruction to state `nt8-rules(B16-T1): no new rules` if none found |

**Section verdict: PASS**

---

#### G. Test Coverage

Not applicable for T1 — T1 has no pure-math logic, no testable methods accessible from xUnit. T1.8 explicitly justifies this. Test coverage begins in T2.

**Section verdict: N/A (by design — PASS)**

---

#### H. Gate Statement

| Check | Result | Evidence |
|-------|--------|----------|
| T1 states T2 is BLOCKED until T1 VERIFY_PASS | PASS | T1.1 metadata table row: `Gates: T2 is BLOCKED until T1 VERIFY_PASS`. T1.12: "T2 is BLOCKED until T1 VERIFY_PASS." with full verifier attestation requirements listed |

**Section verdict: PASS**

---

#### File Routing

All T1 `.cs` source paths: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` — Wave workspace. Correct.
All documentation paths: `c:\WSGTA\universal-or-strategy\docs\standards\` — Wave workspace. Correct.
No Director workspace (`..\universal-or-strategy-director`) `.cs` paths present.

**Section verdict: PASS**

---

### T1 VERDICT: TICKET_REVIEW_PASS

---

---

### T2 — GetPriceAtY Implementation (Gated on T1 VERIFY_PASS)

#### A. Traceability

| Check | Result | Evidence |
|-------|--------|----------|
| References DW-B16-01 | PASS | T2.1 metadata: `DW-B16-01 (close or document conditional failure)` |
| References plan sections D (Branch A and B) | PASS | T2.6 Step 2 maps to plan §D.1 (Branch A) and §D.2 (Branch B); T2.7 signatures match plan §D.5 exactly |
| No phantom work | PASS | All T2 items (GetPriceAtY Branch A/B, LinearYToPrice, AlignToTick, T1 cleanup, 8 tests) appear in plan §D |
| No missing plan work | PASS | Plan §D.6 changes table fully covered: all REMOVE, REPLACE, ADD, and UNCHANGED items implemented in T2.6 |

**Section verdict: PASS**

---

#### B. 7-Scan Checklist Presence

All 7 scans present in T2.12 with correct scan commands and expected results.

| Scan | Present | Command | Expected Result | Correct? |
|------|---------|---------|-----------------|---------|
| SCAN-01: lock() | YES | `grep -n "lock(" ...TradeCopierPanel.cs` | 0 results | YES |
| SCAN-02: async void | YES | `grep -n "async void" ...TradeCopierPanel.cs` | 0 results | YES |
| SCAN-03: DateTime.Now | YES | `grep -n "DateTime\.Now[^U]" ...TradeCopierPanel.cs` | 0 results | YES |
| SCAN-04: hex color | YES | `grep -n '"#[0-9A-Fa-f]' ...TradeCopierPanel.cs` | 0 results | YES |
| SCAN-05: GetValueByY | YES | `grep -n "\.GetValueByY(" ...TradeCopierPanel.cs` | 0 results (code hits = violation) | YES |
| SCAN-06: price=0.0 stub eliminated | YES | `grep -n "price\s*=\s*0\.0" ...TradeCopierPanel.cs` | 0 results (Branch A or B replaces stub) | YES |
| SCAN-07: T_B16_ test names | YES | `grep -n "T_B16_" ...CopyEngineTests.cs` | 8 results (T_B16_01–T_B16_08) | YES |

Note: SCAN-06 note correctly states that if Branch B CS1061 forces a `Last.Price` fallback, the `rawPrice` variable does not use a `0.0` literal — SCAN-06 still passes. Fallback documentation requirement stated.

All 7 scans present, correctly scoped to Wave workspace. Defense-in-depth Layer 1 confirmed.

**Section verdict: PASS**

---

#### C. JS Pre-Check

| Rule | Constraint | T2 Code | Result |
|------|-----------|---------|--------|
| JS-021 | No `lock()` | No lock pattern in any T2 code block (both Branch A and B); T2.8 row JS-021: "No lock in any new or modified method" | PASS |
| JS-023 | `volatile bool` for cross-thread state | `_clickArmed`, `_clickBuy` unchanged and remain `volatile`; `_chartScaleDiagDone` removed in T2 | PASS |
| JS-033 | No `async void` | All new methods are `static double` or `void`; no async keyword anywhere in T2 code | PASS |
| JS-002 | No `return null` | All guard returns use `return 0.0` (double) or `return raw` (double); T2.8 explicitly states "All returns are `0.0` (double) or `raw` (double); no null returns" | PASS |

**Section verdict: PASS**

---

#### D. CYC Pre-Check

| Method | Claimed CYC | Branches Enumerated | Within Budget? |
|--------|-------------|---------------------|----------------|
| `SetChart` (T2 restored) | 1 | straight-line — T1 guard removed | YES (≤8) |
| `GetPriceAtY` Branch A | 4 | cc null(1) + panel null/goto(2) + scale null/goto(3) + raw≤0/goto(4) | YES (≤8, ≤6) |
| `GetPriceAtY` Branch B | 5 | cc null(1) + panel null(2) + height≤0(3) + raw≤0(4) + instrument null(5) | YES (≤8, ≤6) |
| `LinearYToPrice` | 2 | panelH≤0 guard(1) + raw≤0 guard(2) | YES (≤8) |
| `AlignToTick` | Ticket says 2; table in §D says 1 | Body contains `if (tickSize <= 0.0) return raw;` guard → CYC=2; plan review §4 confirms "CYC=1 (table) / 2 (body) — Both values ≤ 8. No violation." | YES (≤8) |
| Each `[Fact]` test | 1 | straight-line assertion; no branches | YES (≤8) |
| `OnChartMouseDown` | 7 (unchanged) | unchanged from B15; not touched in T2 | YES (≤8) |

All methods ≤ 8. No split required.

**Section verdict: PASS**

---

#### E. NT8 Constraints

| Check | Result | Evidence |
|-------|--------|----------|
| `using System.Reflection` REMOVED in T2 | PASS | T2.6 Step 1 removal list: `using System.Reflection;` explicitly listed with grep verification command |
| `using System.Text` REMOVED in T2 | PASS | T2.6 Step 1 removal list: `using System.Text;` explicitly listed |
| `_chartScaleDiagDone` field REMOVED | PASS | T2.6 Step 1 removal list: field and 3-line comment block explicitly listed; grep check: 0 results |
| T1 diagnostic methods REMOVED | PASS | `WalkChartPanelChildren` and `BuildMethodReport` both in removal list with grep verification |
| SetChart comment restored | PASS | T2.6 Step 1 states comment must be updated to `// B9 T2: Store chart reference. CYC=1 (straight-line).` |
| RoundToTickSize flagged UNCONFIRMED; CS1061 path documented | PASS | T2.6 Step 6 rule NT8-038 template; T2.8 row NT8-029 "RoundToTickSize or AlignToTick fallback"; AlignToTick is the confirmed fallback in T2 |
| ChartPanel.MaxValue/MinValue UNCONFIRMED; CS1061 path documented | PASS | T2.6 Step 6 rules NT8-039/NT8-040 templates with fallback to `Last.Price` (DW-B16-01 OPEN) |
| NT8-035: 0.0 stub eliminated | PASS | T2.8 row NT8-035: "GetPriceAtY T2 replaces stub — DW-B16-01 closes"; SCAN-06 expects 0 results |
| NT8-036: ChartControl.ChartBars absent — not called | PASS | T2.8 row NT8-036: "Not used; FindVisualChild<ChartPanel> only" |
| NT8-037: ChartPanel.GetValueByY absent — not called | PASS | T2.8 row NT8-037: "Not called; replacement strategy used"; SCAN-05 expects 0 code hits |
| NT8-013: DateTime.MaxValue in CreateOrder | PASS | T2.8 row NT8-013: "Line 1145 unchanged" |
| NT8-014: Signal name starts with "PTT-" | PASS | T2.8 row NT8-014: `"PTT-Click"` on line 1144 unchanged |
| NT8-028: No hex color literals | PASS | T2.8: "No color changes in T2" |
| NT8-032: MarketData.Last.Price fallback | PASS | Both Branch A fallback and Branch B CS1061 fallback correctly use `.Last.Price` not `.Last` |
| Files-not-to-touch respected | PASS | T2.5 lists: CopyEngine.cs, TradeCopierAddOn.cs, TradeCopierWindow.cs, AtrSizingEngine.cs |
| `goto fallback` pattern in Branch A | PASS | CYC-safe pattern; plan review §4 accepted; plan §D.1 note on goto explicitly explains CYC=4 holds |

**Section verdict: PASS**

---

#### F. Completeness

| Check | Result | Evidence |
|-------|--------|----------|
| Both Branch A and Branch B present | PASS | T2.6 Step 2 has full implementations for both; T2.3 Branch Decision Rules specifies how engineer picks |
| T2 explicitly gated on T1 VERIFY_PASS | PASS | T2.1 metadata: `Depends on: T1 VERIFY_PASS (HARD GATE — do not start T2 without T1 VERIFY_PASS)`. T2.3: "Read `ticket-1-verification.md` before writing any code." |
| All 8 [Fact] test names with full assertions | PASS | T2.9 table has all 8 tests; T2.6 Step 5 has complete code stubs for all 8 including helper methods `CallLinearYToPrice` and `CallAlignToTick` |
| NT8_ADDON_KNOWLEDGE.md update instruction | PASS | T2.10 and T2.6 Step 7 with exact template |
| NT8_COMPILER_RULES.md update instruction | PASS | T2.11 and T2.6 Step 6 with three NT8-038/039/040 templates |
| BUILD_PASS criteria include F5 green + tests pass | PASS | T2.13 item 1 (dotnet build / F5 zero errors), item 2 (dotnet test 8/8 pass), item 10 (F5 gate with three outcome options: Branch A confirmed, Branch B approximation, full CS1061 failure) |
| BRANCH_CHOSEN documentation instruction | PASS | T2.3: "Record the branch chosen at the top of `ticket-2-completion.md`" with exact format block |
| DW-B16-01 closure status documentation | PASS | T2.14: three explicit outcomes listed (CLOSED, PARTIAL-CLOSED, OPEN); T2.10 NT8_ADDON_KNOWLEDGE.md template requires DW-B16-01 status |

**Section verdict: PASS**

---

#### G. Test Coverage

| Test Name | Inputs | Expected Output | Assertion | Present? |
|-----------|--------|-----------------|-----------|---------|
| `T_B16_01_LinearPriceInterp_TopOfChart_ReturnsMaxValue` | y=0, panelH=400, max=5000, min=4900, cf=1.0 | 5000.0 | `Assert.Equal(5000.0, result, 5)` | YES |
| `T_B16_02_LinearPriceInterp_BottomOfChart_ReturnsMinValue` | y=400, panelH=400, max=5000, min=4900, cf=1.0 | 4900.0 | `Assert.Equal(4900.0, result, 5)` | YES |
| `T_B16_03_LinearPriceInterp_MiddleOfChart_ReturnsMidpoint` | y=200, panelH=400, max=5000, min=4900, cf=1.0 | 4950.0 | `Assert.Equal(4950.0, result, 5)` | YES |
| `T_B16_04_LinearPriceInterp_QuarterFromTop_ReturnsThreeQuarterRange` | y=100, panelH=400, max=5000, min=4900, cf=1.0 | 4975.0 | `Assert.Equal(4975.0, result, 5)` | YES |
| `T_B16_05_LinearPriceInterp_ZeroHeight_ReturnsZero` | y=100, panelH=0, max=5000, min=4900, cf=1.0 | 0.0 | `Assert.Equal(0.0, result, 5)` | YES |
| `T_B16_06_AlignToTick_ValueBelowMidTick_RoundsDown` | raw=4975.10, tick=0.25 | 4975.00 | `Assert.Equal(4975.00, result, 5)` | YES |
| `T_B16_07_AlignToTick_ValueAboveMidTick_RoundsUp` | raw=4975.15, tick=0.25 | 4975.25 | `Assert.Equal(4975.25, result, 5)` | YES |
| `T_B16_08_AlignToTick_ExactTickBoundary_Unchanged` | raw=4975.25, tick=0.25 | 4975.25 | `Assert.Equal(4975.25, result, 5)` | YES |

All 8 `[Fact]` tests present. All are CYC=1 (straight-line). Access pattern via `CallLinearYToPrice`/`CallAlignToTick` private helpers using `BindingFlags.NonPublic | BindingFlags.Static` matches the established CopyEngineTests.cs reflection pattern (verified: `GetField`/`GetMethod` helpers at lines 18-22 of CopyEngineTests.cs use same BindingFlags approach). Tests append before closing `}` at line 1723.

Math verification on T_B16_04: y=100, panelH=400, max=5000, min=4900, cf=1.0 → yRatio=100/400=0.25 → rawPrice=5000-(0.25×100)=5000-25=4975.0. Correct.

Math verification on T_B16_06: raw=4975.10, tick=0.25 → 4975.10/0.25=19900.4 → AwayFromZero rounds to 19900 → 4975.00. Correct.

Math verification on T_B16_07: raw=4975.15, tick=0.25 → 4975.15/0.25=19900.6 → AwayFromZero rounds to 19901 → 4975.25. Correct.

**Section verdict: PASS**

---

#### H. Gate Statement

| Check | Result | Evidence |
|-------|--------|----------|
| T2 reads T1 VERIFY_PASS before starting | PASS | T2.3: "Read `docs/brain/PTT-COPIER-B16/ticket-1-verification.md` before writing any code." T2.1 metadata: "Depends on: T1 VERIFY_PASS (HARD GATE — do not start T2 without T1 VERIFY_PASS)" |

**Section verdict: PASS**

---

#### File Routing

All T2 `.cs` source paths: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` — Wave workspace. Correct.
All documentation paths: `c:\WSGTA\universal-or-strategy\docs\standards\` — Wave workspace. Correct.
No Director workspace `.cs` paths present.

**Section verdict: PASS**

---

### T2 VERDICT: TICKET_REVIEW_PASS

---

---

## Violations Found

None.

---

## Cross-Ticket Consistency Check

| Item | T1 State | T2 State | Consistent? |
|------|---------|---------|-------------|
| `using System.Reflection` | ADDED | REMOVED | YES — T2.6 Step 1 grep check enforced |
| `using System.Text` | ADDED | REMOVED | YES — T2.6 Step 1 grep check enforced |
| `_chartScaleDiagDone` field | ADDED | REMOVED | YES — T2.6 Step 1, verification: 0 grep hits |
| `WalkChartPanelChildren` method | ADDED | REMOVED | YES — T2.6 Step 1 |
| `BuildMethodReport` method | ADDED | REMOVED | YES — T2.6 Step 1 |
| `SetChart` CYC | 2 | 1 (restored) | YES |
| `GetPriceAtY` | B15 stub (Last.Price, Y ignored) | Branch A or B (real Y price) | YES |
| `LinearYToPrice` | absent | ADDED internal static | YES |
| `AlignToTick` | absent | ADDED internal static | YES |
| Tests (T_B16_xx) | 0 | 8 [Fact] | YES |
| NT8_ADDON_KNOWLEDGE.md | B16 T1 section written | B16 T2 appended | YES |
| Gate chain | T1 gates T2 | T2 reads T1 verification | YES — bidirectional gate confirmed |

---

## Summary

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | VERDICT |
|--------|-------------|-------------|--------------|-----------|--------------|---------------|-------------|---------|
| T1 | PASS | PASS | PASS | PASS | N/A (by design) | PASS | PASS | TICKET_REVIEW_PASS |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |

---

## Overall: TICKET_REVIEW_PASS

Both tickets pass all required checks. No violations. No remediation items for the architect.

Engineering is unblocked. The orchestrator may spawn ptt-engineer for T1.

**Constraints on engineer:**
1. T1 must reach VERIFY_PASS before T2 begins — the T1.12 gate statement is binding.
2. T2 engineer must read `ticket-1-verification.md` and record `BRANCH_CHOSEN: A` or `BRANCH_CHOSEN: B` at the top of `ticket-2-completion.md` before writing any production code.
3. `CORRECTION_FACTOR` placeholder in Branch B MUST be replaced with the measured value from T1 ActualHeight data — leaving the literal `1.0` is only acceptable if T1 confirmed it is the exact measured ratio.
4. For both tickets: all 7 scan results must be attested with actual values (not left as `[ ]`) before filing completion.

---

## Amendment: DW-B16-02 injected (2026-07-15)

DW-B16-02 added to T2 scope by Director before engineer started.
Ticket review verdict for T1 **unchanged** (TICKET_REVIEW_PASS — T1 scope not affected).

T2 scope expanded:
- `CopyEngine.cs` added to T2 files-to-modify (remove `IsTrailingStop` cancel+replace branch in `TightenOneStop`)
- `"~"` button label rename to `"Tighten"` added to T2 Step 0b
- 2 new [Fact] tests added: T_B16_09, T_B16_10 (TightenOneStop guard logic)
- SCAN-08 + SCAN-09 added to T2.12 checklist
- BUILD_PASS criteria expanded to 12 items

No re-review required — fix is removal-only (no new logic added to CopyEngine).
The change aligns TightenOneStop with the GAP-001d confirmed safe `acc.Change()` pattern
already used by all other PTT stop-price actions. Zero new JS/NT8 rule violations.

