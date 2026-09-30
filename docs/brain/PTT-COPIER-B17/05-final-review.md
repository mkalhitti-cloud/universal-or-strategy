# PTT-COPIER-B17 Final Review
# Reviewer: ptt-plan-reviewer (Phase 5)
# Date: 2026-07-15
# Block: PTT-COPIER-B17
# Prior block backlog: docs/brain/PTT-COPIER-B16/06-deferred-backlog.md
# Rules: docs/standards/jane-street/RULES_CATALOG.md
# NT8 Rules: docs/standards/NT8_COMPILER_RULES.md

---

## §A Spec Requirement Traceability

### DW-B17-01 — Click trader OnChartMouseDown never fires (MouseDown suppressed by NT8)

| Sub-requirement | Addressed In | Evidence |
|-----------------|-------------|---------|
| Diagnose visual tree (T1) | T1 completion + T1 VERIFY_PASS | `EnumerateAllChartPanels` + `ProbeChartsProperty` implemented; MessageBox confirmed with real F5 data |
| Interim fallback while T2 pending (T1) | T1 completion + T1 VERIFY_PASS | `if (rawPrice <= 0.0) rawPrice = GetRefPrice();` after `GetPriceAtY` — VERIFY_PASS §6 |
| Permanent fix: correct ChartPanel selection (T2) | T2 completion + T2 VERIFY_PASS | `FindPriceCanvasPanel` replaces `FindVisualChild<ChartPanel>` in `GetPriceAtY` |
| T1 diagnostic code removed in T2 | T2 completion + T2 VERIFY_PASS | §1 cleanup scan: 0 live-code occurrences of `_b17DiagDone`, `EnumerateAllChartPanels`, `ProbeChartsProperty` |
| Pure-math test coverage | T2 completion + T2 VERIFY_PASS | 7 `[Fact]` tests T_B17_01..07; total [Fact] count = 111 |

**STATUS: CLOSED (T1 + T1-Amendment + T2 all VERIFY_PASS)**

### DW-B17-02 — cc.MouseDown suppressed by NT8 chart canvas (e.Handled=true)

| Sub-requirement | Addressed In | Evidence |
|-----------------|-------------|---------|
| Director-authorized: MouseDown → PreviewMouseDown | T1 Amendment VERIFY_PASS | 3 occurrences changed: RegisterClickTrader (remove-old, add-new) + UnregisterClickTrader |
| Symmetry: += and -= both updated | T1 Amendment VERIFY_PASS | Lines 312, 314, 324 of TradeCopierAddOn.cs confirmed PreviewMouseDown |
| No plain cc.MouseDown handler lines remaining | T1 Amendment VERIFY_PASS | §D scan: zero plain `cc.MouseDown` references |

**STATUS: CLOSED (T1 Amendment VERIFY_PASS — Director authorized)**

### VERIFY_PASS Verdicts Present

| Verdict | Present |
|---------|---------|
| T1 VERIFY_PASS | YES — `ticket-1-verification.md` |
| T1 Amendment VERIFY_PASS | YES — appended to `ticket-1-verification.md` (Amendment Verification section) |
| T2 VERIFY_PASS | YES — `ticket-2-verification.md` |

**§A Result: PASS**

---

## §B Cross-File Coherence

### B1: TradeCopierAddOn.cs — PreviewMouseDown wiring

Source scan result (independent, 2026-07-15):

```
Line 312: cc.PreviewMouseDown -= old.OnChartMouseDown;
Line 314: if (cc != null) cc.PreviewMouseDown += panel.OnChartMouseDown;
Line 324: cc.PreviewMouseDown -= panel.OnChartMouseDown;
```

- RegisterClickTrader remove-old path: `cc.PreviewMouseDown -= old.OnChartMouseDown` ✅
- RegisterClickTrader add-new path: `cc.PreviewMouseDown += panel.OnChartMouseDown` ✅
- UnregisterClickTrader: `cc.PreviewMouseDown -= panel.OnChartMouseDown` ✅
- Zero plain `cc.MouseDown` handler lines ✅

**B1: PASS**

### B2: TradeCopierPanel.cs — FindPriceCanvasPanel used in GetPriceAtY

Source scan result:

```
Line 309: var panel = FindPriceCanvasPanel(cc);    // B17 T2: heuristic selects widest ChartPanel
Line 358: private static ChartPanel FindPriceCanvasPanel(DependencyObject root)
```

- `GetPriceAtY` line 309 uses `FindPriceCanvasPanel(cc)` ✅
- Old call `TradeCopierAddOn.FindVisualChild<ChartPanel>(cc)` present only in comment at line 299 ✅
- `FindPriceCanvasPanel` declared at line 358 (private static, DFS heuristic, CYC=5) ✅

**B2: PASS**

### B3: TradeCopierPanel.cs — No T1 diagnostic code remaining

Source scan for `_b17DiagDone|EnumerateAllChartPanels|ProbeChartsProperty|B17 interim`:

```
Line 3  (header comment): //   1. Added _b17DiagDone volatile bool field...
Line 4  (header comment): //   2. Added EnumerateAllChartPanels...
Line 5  (header comment): //   3. Modified OnChartMouseDown...
```

- `_b17DiagDone` field: REMOVED (historical comment only) ✅
- `EnumerateAllChartPanels` method: REMOVED (historical comment only) ✅
- `ProbeChartsProperty` method: REMOVED (live code absent) ✅
- `EnumerateAllChartPanels(chartControl)` call in OnChartMouseDown: REMOVED ✅
- Interim fallback `rawPrice = GetRefPrice()`: REMOVED ✅
- `using System.Reflection;` and `using System.Text;`: REMOVED ✅

**B3: PASS**

### B4: CopyEngineTests.cs — 7 T_B17 tests added, [Fact] count = 111

Source scan:

- [Fact] count = 111 (confirmed via Select-String Measure-Object) ✅
- T_B17_01 at line 1830 ✅
- T_B17_02 at line 1839 ✅
- T_B17_03 at line 1848 ✅
- T_B17_04 at line 1857 ✅
- T_B17_05 at line 1866 ✅
- T_B17_06 at line 1875 ✅
- T_B17_07 at line 1884 ✅

**B4: PASS**

### B5: No circular dependency introduced

- TradeCopierPanel.cs uses `TradeCopierAddOn.FindVisualChild<T>` only in historical comment (removed from live code) ✅
- TradeCopierAddOn.cs references `TradeCopierPanel.OnChartMouseDown` (same pattern as B16 — not a new coupling) ✅
- CopyEngineTests.cs calls `CallLinearYToPrice` / `CallAlignToTick` via reflection (existing B16 helpers) ✅
- No new file-level coupling introduced ✅

**B5: PASS**

**§B Result: PASS — all cross-file wiring correct**

---

## §C JS P0 Cross-File Final Scan

All scans run independently via lean-ctx ctx_shell Select-String on `src/PropTraderTools/*.cs`.

### SCAN-01 — JS-021: `lock(`

```
Select-String -Path *.cs -Pattern "lock\("
```

Results (all files):
- `CopyEngine.cs` lines 319, 562, 793, 1197 — ALL are `// CYC=... try block(0)` comments where "lock" appears as part of the comment text. Zero `lock(` C# concurrency primitive calls.
- All other files: 0 matches.

**RESULT: 0 code-level lock() calls. PASS (JS-021)**

### SCAN-02 — JS-033: `async void`

```
Select-String -Path TradeCopierPanel.cs -Pattern "async void " → Count 0
Select-String -Path TradeCopierAddOn.cs -Pattern "async void " → Count 0
```

**RESULT: 0 matches in B17 target files. PASS (JS-033)**

### SCAN-03 — NT8-003: `volatile double`

```
Select-String -Path *.cs -Pattern "volatile double"
```

Results:
- `AtrSizingEngine.cs` lines 13, 49 — both are comments (`// volatile double forbidden`) documenting the rule. Zero volatile double field declarations.

**RESULT: 0 volatile double declarations. PASS (NT8-003 / JS-008)**

### SCAN-04 — NT8-034: `Math.Clamp`

```
Select-String -Path TradeCopierPanel.cs -Pattern "Math\.Clamp\s*\("
```

Results: 4 lines — ALL are comments (`// no Math.Clamp (NT8 .NET 4.8)`) or code using `Math.Max/Math.Min`. Confirmed line-by-line:
- Line 736: `Math.Max(Math.Min(...))   // no Math.Clamp`
- Line 929: `// NT8-034: no Math.Clamp...`
- Line 936: `Math.Max(1, Math.Min(500, t))   // clamp 1-500: no Math.Clamp`
- Line 1561: `Math.Max(Math.Min(...))   // no Math.Clamp`

Zero actual `Math.Clamp(` function calls.

**RESULT: 0 Math.Clamp( calls. PASS (NT8-034)**

**§C Result: PASS — zero JS P0 violations across all target files**

---

## §D NT8 Constraint Cross-File Final Scan

### SCAN-D1 — NT8-001: `{ get; init; }`

```
Select-String -Path *.cs -Pattern "\.init;"
```

Results: 0 matches in TradeCopierPanel.cs, CopyEngine.cs, CopyEngineTests.cs, TradeCopierAddOn.cs.

**RESULT: 0 matches. PASS (NT8-001)**

### SCAN-D2 — NT8-034: `Math.Clamp` (confirmed above in §C SCAN-04)

**RESULT: 0 actual calls. PASS**

### SCAN-D3 — NT8-037: `ChartPanel.GetValueByY` / `ChartControl.GetValueByY`

```
Select-String -Path TradeCopierPanel.cs -Pattern "ChartPanel\.GetValueByY|ChartControl\.GetValueByY" → Count 0
Select-String -Path TradeCopierAddOn.cs -Pattern "ChartPanel\.GetValueByY|ChartControl\.GetValueByY" → Count 0
```

**RESULT: 0 matches. PASS (NT8-037 / NT8-009)**

**§D Result: PASS — zero NT8 constraint violations**

---

## §E CYC Summary

All CYC values from T1 verification (§3) and T2 verification (§5) — independently counted:

| Method | Block | CYC | Bound | Status |
|--------|-------|-----|-------|--------|
| `EnumerateAllChartPanels` | T1 new (REMOVED in T2) | 4–6 | ≤ 8 | PASS (removed cleanly) |
| `ProbeChartsProperty` | T1 new (REMOVED in T2) | 6 | ≤ 8 | PASS (removed cleanly) |
| `OnChartMouseDown` | T1 modified (CYC=7) | 7 | ≤ 8 | PASS |
| `FindPriceCanvasPanel` | T2 new | 5 | ≤ 8 | PASS |
| `GetPriceAtY` | T2 modified (single-line) | 5 | ≤ 8 | PASS |
| `OnChartMouseDown` | T2 restored | 6 | ≤ 8 | PASS |
| `RegisterClickTrader` | T1 Amendment | 2 | ≤ 8 | PASS |
| `UnregisterClickTrader` | T1 Amendment | 2 | ≤ 8 | PASS |

**No method exceeds CYC 8. §E Result: PASS**

---

## §F Test Coverage

### 7 T_B17 Tests Present (T2 VERIFY_PASS §6)

| Test Name | What It Tests | Line |
|-----------|--------------|------|
| `T_B17_01_LinearYToPrice_TopOfPanel_ReturnsMaxVal` | y=0 → maxVal (top of panel guard path) | 1830 |
| `T_B17_02_LinearYToPrice_MiddleOfPanel_ReturnsMidpointPrice` | y=226 → midpoint price (typical case) | 1839 |
| `T_B17_03_LinearYToPrice_ZeroPanelHeight_ReturnsZero` | panelH=0 → 0.0 (guard 1 fires) | 1848 |
| `T_B17_04_LinearYToPrice_OverBoundary_ReturnsZero` | y too large → raw ≤ 0 → 0.0 (guard 2 fires) | 1857 |
| `T_B17_05_AlignToTick_AlreadyAligned_Unchanged` | Already-aligned price unchanged | 1866 |
| `T_B17_06_AlignToTick_HalfTickRoundsAwayFromZero` | Half-tick rounds away from zero | 1875 |
| `T_B17_07_AlignToTick_ZeroTickSize_ReturnsRaw` | Zero tickSize guard returns raw | 1884 |

All 7 tests exceed the minimum of 4 required by plan §C.6. ✅

### Pure-Math Confirmation

- Tests call `CallLinearYToPrice` / `CallAlignToTick` via Reflection (B16 T2 helpers at lines 1726/1735)
- No WPF tree, no NT8 runtime, no ChartControl instantiation
- T2 VERIFY_PASS §6 confirmed via independent scan ✅

### Prior Tests Untouched

- Total [Fact] count: 111 (prior 104 + 7 new) ✅
- No T_B16, T_B15, or earlier tests were modified ✅

**§F Result: PASS**

---

## §G NT8 Knowledge Updated

### NT8_ADDON_KNOWLEDGE.md

| Section | Location | Content | Status |
|---------|----------|---------|--------|
| `## B17 T1 Discoveries` | Line 632 | Real F5 Sim101 MessageBox output: `B17 ChartPanel[0]: W=931.33 H=639.33 Max=7633.34 Min=7547.66` and `Charts property: NOT FOUND` | PRESENT ✅ |
| `## B17 T2 Discoveries` | Line 675 | Confirmed path (Option A), root cause summary, NT8-041 rule, test count delta (104→111) | PRESENT ✅ |
| `NT8-041` | Line 691 | `ChartControl.Charts property does NOT exist (Reflection returns null)` | PRESENT ✅ |
| `nt8-rules B17-T2: 1 new rule` | Line 701 | NT8-041 declared | PRESENT ✅ |

### NT8_COMPILER_RULES.md — NT8-041 Not Yet Added

**FINDING**: `NT8-041` is present in `NT8_ADDON_KNOWLEDGE.md` (line 691) but has NOT been added to `NT8_COMPILER_RULES.md`. The rules file ends at NT8-030 in the INDEX TABLE (line 749). No NT8-031 through NT8-041 block definitions exist.

**Assessment**: NT8_ADDON_KNOWLEDGE.md is the primary discovery log for NT8 runtime behaviour. NT8_COMPILER_RULES.md is the compiler/build gate. `ChartControl.Charts NOT FOUND` is a runtime reflection finding (not a compiler error), so it is correctly classified in the ADDON_KNOWLEDGE doc. The engineer stated `nt8-rules B17-T2: 1 new rule (NT8-041)` which is satisfied by the ADDON_KNOWLEDGE entry. However, the formal NT8_COMPILER_RULES.md entry for NT8-041 was NOT written.

**Action Required (B18)**: ptt-architect B18 planning should ensure NT8-041 is added to NT8_COMPILER_RULES.md (a DW-B17-NT8-041 deferred work item is recorded in the backlog below).

**§G Result: PARTIAL — NT8_ADDON_KNOWLEDGE.md fully populated (PASS); NT8_COMPILER_RULES.md missing NT8-041 entry (DEFERRED to B18, no FINAL_FAIL trigger per task instructions: "note the need to add NT8-041")**

---

## §H Block Summary

| Metric | Value |
|--------|-------|
| Tickets executed | 2 (T1 + T1-Amendment, T2) |
| VERIFY_PASS verdicts | 3 (T1 VERIFY_PASS, T1-Amendment VERIFY_PASS, T2 VERIFY_PASS) |
| New methods added (surviving into final state) | 1 (`FindPriceCanvasPanel`) |
| Methods removed (T1 diagnostic, cleaned in T2) | 2 (`EnumerateAllChartPanels`, `ProbeChartsProperty`) |
| Methods modified | 2 (`GetPriceAtY`, `OnChartMouseDown`) |
| New [Fact] tests | 7 (T_B17_01..07) |
| Total [Fact] count | 111 |
| New NT8 rules discovered | 1 (NT8-041: ChartControl.Charts NOT FOUND) |
| NT8_ADDON_KNOWLEDGE.md updated | YES (B17 T1 + T2 Discoveries) |
| NT8_COMPILER_RULES.md NT8-041 added | NO — deferred to B18 |
| Cross-file JS P0 scan violations | 0 |
| Cross-file NT8 scan violations | 0 |
| CYC > 8 violations | 0 |
| DW-B17-01 closed | YES |
| DW-B17-02 closed | YES (Director authorized) |
| Open items carried to B18 | 4 (DW-B9-01, DW-B9-03, DW-B12-DEFER-01-orig, NT8-041 compiler rule) |

---

## §K Deferred Work Ledger (Running from B10 — B17 rows appended)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B10-01 | Remove BuildDiagRow / OnDiagGap001d / OnDiagGap002 scaffolding | P2 | B11 | CLOSED (B11 T1) |
| DW-B10-02 | Add 3 missing AtrSizingEngine xUnit tests | P1 | B11 | CLOSED (B11 T2) |
| DW-B10-03 | TradeCopierWindow.cs Arm BE column — OnRuleArmBe + Col 11 cluster | P2 | B11 | CLOSED (B11 T2) |
| DW-B10-04 | Update NT8_ADDON_KNOWLEDGE.md with T4 chart attachment result | P1 | B11 | CLOSED (B11 T1) |
| DW-B8-04 | Fix click trader price lookup — replace hardcoded 0.0 stub | P2 | B15 | CLOSED (B15 T2 + F5 GREEN) |
| DW-B9-01 | ATR box visualization on chart canvas | P2 | B18+ | OPEN |
| DW-B9-03 | Click trader Bid+1/Ask-1 spread auto-offset (UNBLOCKED) | P3 | B18+ eligible | OPEN (SHELVED per Director) |
| DW-B11-DEFER-01 | Convert Flatten/Trim keyboard shortcuts to Limit orders | P1 | B12 | CLOSED (B12 T1) |
| DW-B12-DEFER-01 | Wire GetRefPrice() to _instrument.MarketData.Last.Price | P1 | B13 | CLOSED (B13 T1) |
| DW-B12-DEFER-02 | ATR fraction spinner startup sync | P2 | B13 | CLOSED (B13 T2) |
| DW-B12-DEFER-03 | Correct Math.Clamp comment attribution (NT8-003 → NT8-034) | P3 | B13 | CLOSED (B13 T3) |
| DW-B12-DEFER-01 (orig) | Full-panel mode expansion: Buy Ask / Sell Bid quick-entry buttons | P2 | future | OPEN |
| DW-B12-DEFER-02 (orig) | Auto-trail stop from BE CONNECTED level | P3 | B14 | CLOSED (B14 T1) |
| DW-B12-DEFER-04 | Align CopyEngineTests.cs test names with 04-tickets.md contract names | P3 | B14 | CLOSED (B14 T2) |
| DW-B15-01 | F5 gate: ChartPanel.GetValueByY compile in NT8 Sim101 | P1 | B15 | CLOSED (B15 F5 GREEN) |
| DW-B16-01 | Click trader Y-pixel-to-price lookup via Branch B linear interpolation | P2 | B16 | CLOSED (B16 T2 VERIFY_PASS) |
| DW-B16-02 | TightenOneStop cancel+replace kills native ATM bracket and trail watermark | P1 | B16 | CLOSED (B16 T2 VERIFY_PASS) |
| DW-B16-02-conditional | Branch B MaxValue/MinValue CS1061 fallback | P2 | B16/B17 | NOT ACTIVATED |
| DW-B17-01 | Click trader OnChartMouseDown never fires — wrong ChartPanel (DFS first-match bug) | P1 | B17 | **CLOSED (T1+T1-Amendment+T2 VERIFY_PASS)** |
| DW-B17-02 | cc.MouseDown suppressed by NT8 chart canvas (e.Handled=true) | P1 | B17 | **CLOSED (T1 Amendment VERIFY_PASS — Director auth)** |
| DW-B17-NT8-041 | Add NT8-041 (ChartControl.Charts NOT FOUND) to NT8_COMPILER_RULES.md INDEX TABLE | P2 | B18 | OPEN |

---

*End of B17 Final Review — ptt-plan-reviewer*
