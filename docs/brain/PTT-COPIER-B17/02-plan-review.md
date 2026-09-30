# PTT-COPIER-B17 Plan Review
# Status: REVIEW_PASS
# Block: PTT-COPIER-B17
# Date: 2026-07-15
# Reviewer: ptt-plan-reviewer
# Cycle: 2 of 2
# Plan reviewed: docs/brain/PTT-COPIER-B17/02-architecture-plan.md (revised)
# Prior review: docs/brain/PTT-COPIER-B17/02-plan-review.md (Cycle 1 — REVIEW_FAIL)

---

## Verdict: REVIEW_PASS

**Zero violations found. All 8 sections pass. Phase 3 (ticket generation) unlocked.**

---

## Cycle 2 Focus: CYC Violation Resolution

Cycle 1 identified one violation: `EnumerateAllChartPanels` had CYC ≥ 10 because the
reflection probe branches (countProp != null, charts null, count > 0, itemProp != null,
el != null) were embedded inline, raising the method well above the ≤ 8 bound.

The fix applied: all reflection probe logic was extracted into a new dedicated method
`ProbeChartsProperty(ChartControl cc, StringBuilder sb)`.

### Verification of Fix

#### `EnumerateAllChartPanels` (revised §B.2.1)

Pseudocode decision points:
1. `if (cc == null) return;`                          — branch 1
2. `if (_b17DiagDone) return;`                        — branch 2
3. `while (stack.Count > 0)`                          — branch 3
4. `if (node is ChartPanel cp)`                       — branch 4
(child-push inner loop `for i = 0..n`: +1 if counted) — branch 5 at most

**CYC = 4 (plan) or 5 (if for-loop counted). Either way: ≤ 8. PASS.**

No reflection probe branches remain in this method. The Cycle 1 violation is resolved.

#### `ProbeChartsProperty` (new §B.2.2)

Decision points:
1. `chartsProp == null`         — branch 1
2. `charts == null`             — branch 2
3. `countProp != null` (ternary) — branch 3
4. `count > 0`                  — branch 4
5. `itemProp != null`           — branch 5
6. `el != null`                 — branch 6

**CYC = 6 ≤ 8. PASS.**

Both method signatures present in §F with correct visibility, parameters, and CYC
annotations. Both appear in §G CYC table with values within bound.

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| Diagnose visual tree (all ChartPanels + Charts probe) | YES | §B.2 |
| Interim fallback (GetRefPrice) while T2 in progress | YES | §B.4 |
| T1 fire-once diagnostic via volatile bool guard | YES | §B.1, §B.2 |
| T1 only touches TradeCopierPanel.cs + NT8_ADDON_KNOWLEDGE.md | YES | §J T1 |
| T2 blocked on T1 F5 output | YES | §C header |
| T2 option B (ChartControl.Charts) as preferred path | YES | §C.2, §C.3 |
| T2 option A (FindPriceCanvasPanel heuristic) as fallback | YES | §C.4 |
| T2 removes ALL T1 diagnostic code | YES | §C.1 |
| T2 adds ≥4 [Fact] tests in CopyEngineTests.cs | YES | §C.6, §H |
| T2 tests are pure-math (no WPF tree required) | YES | §C.6 |
| CopyEngine.cs / TradeCopierAddOn.cs / TradeCopierWindow.cs / AtrSizingEngine.cs NOT touched | YES | §J |
| ChartPanel.MaxValue / MinValue used (confirmed safe in B16) | YES | §A, §C.4 |
| NT8_ADDON_KNOWLEDGE.md updated both T1 and T2 | YES | §B.5, §C.7 |
| Deferred items DW-B9-01, DW-B9-03, DW-B12-DEFER-01-orig carried forward OPEN | YES | §K.2 |

---

## Per-Check Results

### §1 Root Cause Accuracy

| Check | Result | Location |
|-------|--------|----------|
| DFS first-match returns ChartTrader sidebar (MaxValue=0, Width~139) | PASS | §A table |
| No incorrect root cause claims | PASS | §A |
| Correct line references: GetPriceAtY line 297, OnChartMouseDown line 1155 | PASS | §A, §B.3 |

**§1 Result: PASS**

---

### §2 T1 Scope Compliance

| Check | Result | Location |
|-------|--------|----------|
| T1 only touches TradeCopierPanel.cs and NT8_ADDON_KNOWLEDGE.md | PASS | §J T1 |
| T1 does NOT touch CopyEngine.cs, TradeCopierAddOn.cs, TradeCopierWindow.cs, AtrSizingEngine.cs | PASS | §J "Files NOT Touched" |
| `_b17DiagDone` declared as `private volatile bool` | PASS | §B.1, §F |
| EnumerateAllChartPanels fires MessageBox once only (via _b17DiagDone guard) | PASS | §B.2.1 steps 1–2 |
| Reflection probe for ChartControl.Charts via System.Reflection (zero-compile-risk) | PASS | ProbeChartsProperty extracted — GetProperty/GetValue only |
| Interim fallback: rawPrice = GetRefPrice() BEFORE existing guard | PASS | §B.4 |
| NT8_ADDON_KNOWLEDGE.md update with B17 T1 Discoveries placeholder | PASS | §B.5 |

**§2 Result: PASS**

---

### §3 T2 Scope Compliance

| Check | Result | Location |
|-------|--------|----------|
| T2 explicitly BLOCKED on T1 F5 output | PASS | §C header |
| Option B (ChartControl.Charts) specified as PREFERRED | PASS | §C.2 decision tree |
| Option A (FindPriceCanvasPanel: MaxValue>0 AND largest ActualWidth) as fallback | PASS | §C.4 |
| T2 removes ALL T1 diagnostic code (_b17DiagDone field, EnumerateAllChartPanels, ProbeChartsProperty, call site, fallback line, using directives) | PASS | §C.1 items 1–5 |
| T2 adds minimum 4 [Fact] tests in CopyEngineTests.cs | PASS | §C.6, §H: T_B17_01–T_B17_04 minimum |
| T2 does NOT touch CopyEngine.cs, TradeCopierAddOn.cs, TradeCopierWindow.cs, AtrSizingEngine.cs | PASS | §J T2 |

**§3 Result: PASS**

---

### §4 JS P0 Rules (RULES_CATALOG.md)

| Check | Rule | Result | Location |
|-------|------|--------|----------|
| No `lock()` in plan | JS-021 | PASS | §D SCAN-01 zero-match; no lock() in any planned code block |
| No `async void` in new methods | JS-033 | PASS | §D SCAN-07; all new methods are synchronous |
| No `throw` in hot paths | JS-001 | PASS | No throws planned in any new or modified method |
| `volatile bool _b17DiagDone` declared | JS-023 | PASS | §B.1; field written once on UI thread |
| Null returns in panel helpers are structural guards, not order-routing hot paths | JS-002 | PASS | §C.3, §C.4 — FindPriceCanvasPanel/ViaCharts are visual-tree helpers, not business logic |

**§4 Result: PASS**

---

### §5 NT8 Constraints

| Check | Rule | Result | Location |
|-------|------|--------|----------|
| No `volatile double` | NT8-003 | PASS | §I row NT8-003; new field is `volatile bool` only |
| No `Math.Clamp` | NT8-034 | PASS | §I row NT8-034; not used |
| No `ChartPanel.GetValueByY()` | NT8-037 | PASS | §I row NT8-037; not used |
| No `ChartControl.ChartBars` | NT8-036 | PASS | §I row NT8-036; not used |
| `DateTime.MaxValue` for GTC orders (not `DateTime.Now`) | NT8-013 | PASS | §I row NT8-013; unchanged in OnChartMouseDown |
| Signal name starts with "PTT-" | NT8-014 | PASS | §I row NT8-014; "PTT-Click" preserved |
| CreateOrder arg 11 is `(NinjaTrader.Cbi.CustomOrder)null` | NT8-007 | PASS | §I row NT8-007; unchanged |
| No `async void` in callback methods | NT8-019 | PASS | No async methods planned |
| `TradeCopierWindow` not sealed, not touched | NT8-016 | PASS | §J "Files NOT Touched" |
| No `Account.All` in constructor | NT8-021 | PASS | Not used |
| `using System.Reflection` and `using System.Text` explicitly listed | NT8-031 (pattern) | PASS | §B.2 end — both directives specified for T1 |

**§5 Result: PASS**

---

### §6 CYC Bounds

| Method | Ticket | Plan CYC | Reviewer Verified CYC | ≤ 8? | Result |
|--------|--------|----------|----------------------|------|--------|
| `EnumerateAllChartPanels` | T1 new | 4 | 4–5 (child-push for-loop +1 if counted) | YES | **PASS** |
| `ProbeChartsProperty` | T1 new (extracted) | 6 | 6 | YES | **PASS** |
| `OnChartMouseDown` | T1 modified | 7 | 7 | YES | PASS |
| `GetPriceAtY` | T2 modified | 5 | 5 | YES | PASS |
| `FindPriceCanvasPanel` | T2 Option A | 5 | 5 | YES | PASS |
| `FindPriceCanvasPanelViaCharts` | T2 Option B | 5 | 5 | YES | PASS |
| `OnChartMouseDown` | T2 restored | 6 | 6 | YES | PASS |

Both new T1 methods appear in §F (signatures) and §G (CYC table). No method exceeds the ≤ 8 bound.

**§6 Result: PASS**

---

### §7 Test Coverage

| Check | Result | Location |
|-------|--------|----------|
| ≥4 [Fact] test names specified in §H | PASS | T_B17_01–T_B17_04 minimum; T_B17_01–T_B17_07 recommended |
| Tests cover pure-math methods LinearYToPrice and AlignToTick (internal static) | PASS | §C.6 — "no WPF tree required" |
| T_B17_04 boundary test correctly parameterized (avoids false expectation) | PASS | §H: uses max=10, min=5, panelH=100, y=300 → raw=-5 ≤ 0 → 0.0 |

**§7 Result: PASS**

---

### §8 Deferred Items

| Check | Result | Location |
|-------|--------|----------|
| DW-B9-01 carried forward OPEN | PASS | §K.2 |
| DW-B9-03 carried forward OPEN (shelved per Director decision) | PASS | §K.2 |
| DW-B12-DEFER-01-orig carried forward OPEN | PASS | §K.2 |
| Director decision on DW-B9-03 acknowledged | PASS | §K.2 note "SHELVED per Director decision (B17 brief)" |
| Only DW-B17-01 opened in this block (no scope creep) | PASS | §K.1 |

**§8 Result: PASS**

---

## Violation Summary

| # | Rule | Severity | Description | Location | Status |
|---|------|----------|-------------|----------|--------|
| — | — | — | No violations found in Cycle 2 review | — | CLEAR |

*Cycle 1 violation (CYC ≥ 10 in EnumerateAllChartPanels) fully resolved by extraction of `ProbeChartsProperty`.*

---

## Final Verdict

**REVIEW_PASS**

All §1–§8 checks pass. Zero violations. The extraction of `ProbeChartsProperty` from
`EnumerateAllChartPanels` correctly resolves the Cycle 1 CYC violation. Both methods now
have CYC ≤ 8 individually. The 7-scan checklist (§D) is present and unchanged.

Phase 3 (ticket generation) is unlocked. ptt-architect may proceed to write `04-tickets.md`.

Cycle: 2 of 2.
