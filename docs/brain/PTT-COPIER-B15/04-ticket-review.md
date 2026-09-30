# B15 Ticket Review — CYCLE 2
# Reviewer: ptt-ticket-reviewer (Phase 3.5)
# Cycle: 2 (re-review after V-01 + V-02 repair)
# Tickets reviewed: docs/brain/PTT-COPIER-B15/04-tickets.md (CYCLE 2 — V-01 + V-02 repaired)
# Plan reviewed: docs/brain/PTT-COPIER-B15/02-architecture-plan.md (REVIEW_PASS)
# Cycle 1 review: docs/brain/PTT-COPIER-B15/04-ticket-review.md (TICKET_REVIEW_FAIL T1: V-01 + V-02)
# Date: 2026-07-14
# Block: PTT-COPIER-B15

---

## Focus Checks (Cycle 2 — Repair Verification)

### V-01-FIX: CYC Verification — Three Split Methods

**V-01 in Cycle 1:** `DumpChartControlTree` had actual CYC = 14 (monolithic). Fix required
splitting into sub-helpers: `DumpReflectionPath` + `DumpVisualTree` + `DumpChartControlTree` (orchestrator).

**Cycle 2 result — per-method audit against actual code in ticket:**

#### `DumpReflectionPath(ChartControl cc, System.Text.StringBuilder sb)` — stated CYC = 7

| # | Decision construct | Line reference | Counted? |
|---|-------------------|---------------|---------|
| 1 | `if (barsInfo == null)` early return | Step 3a line 101 | ✅ |
| 2 | `if (barsVal != null)` | Step 3a line 110 | ✅ |
| 3 | `if (indexer != null)` | Step 3a line 113 | ✅ |
| 4 | `if (item0 != null)` | Step 3a line 116 | ✅ |
| 5 | `if (panelInfo != null)` | Step 3a line 120 | ✅ |
| 6 | `if (panelVal != null)` | Step 3a line 124 | ✅ |
| 7 | `catch (Exception ex)` | Step 3a line 135 | ✅ |
| **WARN** | `gvbMethod != null ? "YES" : "NO"` ternary | Step 3a line 127 | Not in table |

**WARN:** The ternary `gvbMethod != null ? "YES" : "NO"` on line 127 is a decision point
not listed in the CYC table. True CYC = 8 (not 7 as stated). CYC = 8 is at the budget
boundary but does NOT exceed 8. **Not a TICKET_REVIEW_FAIL** (threshold is > 8; 8 = 8
is within budget). Flagging as documentation inaccuracy (WARN). No repair required.

**VERDICT: CYC <= 8. PASS.**

#### `DumpVisualTree(ChartControl cc, System.Text.StringBuilder sb)` — stated CYC = 6

| # | Decision construct | Line reference | Counted? |
|---|-------------------|---------------|---------|
| 1 | `for (int i = 0; ...)` outer loop | Step 3b line 178 | ✅ |
| 2 | `if (child == null) continue` | Step 3b line 181 | ✅ |
| 3 | `for (int j = 0; ...)` inner loop | Step 3b line 183 | ✅ |
| 4 | `if (grand == null) continue` | Step 3b line 186 | ✅ |
| 5 | `for (int k = 0; ...)` inner-inner loop | Step 3b line 188 | ✅ |
| 6 | `if (great != null)` | Step 3b line 191 | ✅ |

No missing branches. CYC = 6. Stated CYC = 6. Accurate. Within budget.

**VERDICT: CYC = 6 confirmed. PASS.**

#### `DumpChartControlTree(ChartControl cc)` — stated CYC = 3 (conservative)

| # | Decision construct | Line reference | Counted? |
|---|-------------------|---------------|---------|
| 1 | `if (_chartDiagDone || cc == null) return` | Step 3c line 232 | ✅ |
| 2 | `if (_statusText != null)` inside lambda | Step 3c line 243 | ✅ |

CYC = 2 (true). Ticket conservatively states 3. No missing branches beyond these two.
Sub-helper calls (`DumpReflectionPath`, `DumpVisualTree`) are straight-line calls — no
additional decision points in this method scope. CYC = 2–3. Well within budget.

**VERDICT: CYC = 2 (conservative 3). PASS.**

**V-01-FIX OVERALL: PASS.** All three methods are within CYC <= 8.

---

### V-02-FIX: NT8-008 — chart.ChartControl Banned Pattern

**V-02 in Cycle 1:** `SetChart` used `chart.ChartControl` as primary path (P0 banned,
CS1061 confirmed B8). `FindVisualChild<ChartControl>(chart)` was only a fallback comment.

**Cycle 2 result — exact SetChart implementation in ticket (Step 4):**

```csharp
public void SetChart(Chart chart)
{
    _currentChart = chart;
    var cc = TradeCopierAddOn.FindVisualChild<ChartControl>(chart);   // NT8-008: chart.ChartControl absent (CS1061 confirmed B8)
    if (cc != null)
        DumpChartControlTree(cc);
}
```

- `chart.ChartControl` is ABSENT from the implementation. ✅
- `TradeCopierAddOn.FindVisualChild<ChartControl>(chart)` is the sole path. ✅
- The NT8-008 critical note (lines 291–295) explicitly states:
  "Do NOT write `chart.ChartControl` anywhere in this file. Zero uses allowed." ✅
- SCAN-08 is present and checks `grep -n "chart\.ChartControl"` with expected 0 results. ✅

**V-02-FIX OVERALL: PASS.** NT8-008 P0 pattern is absent. FindVisualChild is the sole path.

---

## Full Check Table — All 26 Original Checks (Cycle 2)

| Check ID | Description | T1 | T2 | Overall |
|----------|-------------|----|----|---------|
| TR-01 | T1 traces to DW-B8-04 investigation requirement | PASS | N/A | PASS |
| TR-02 | T2 traces to DW-B8-04 fix (NT8-009 + NT8-035 + NT8-029) | N/A | PASS | PASS |
| TR-03 | DW-B9-03 not in any ticket (shelved) | PASS | PASS | PASS |
| TR-04 | Protected files in ZERO tickets | PASS | PASS | PASS |
| JS-01 | No lock() — JS-021 | PASS | PASS | PASS |
| JS-02 | No async void — JS-033 | PASS | PASS | PASS |
| JS-03 | No return null — JS-002 | PASS | PASS | PASS |
| JS-04 | No throw in hot paths — JS-001 | PASS | PASS | PASS |
| NT-01 | NT8-009 investigation gates T2 | PASS | PASS | PASS |
| NT-02 | NT8-035 stub removed in T2 | N/A | PASS | PASS |
| NT-03 | NT8-029 tick-align formula in T2 | N/A | PASS | PASS |
| NT-04 | NT8-007 CreateOrder arg 12 verified | N/A | PASS | PASS |
| NT-05 | NT8-003 no volatile double | PASS | PASS | PASS |
| NT-06 | NT8-017 _chartDiagDone is volatile bool | PASS | N/A | PASS |
| NT-07 | NT8-031 no Interlocked used — N/A | N/A | N/A | PASS |
| NT-08 | NT8-020 no new SolidColorBrush | PASS | PASS | PASS |
| CO-01 | 8-scan checklist present in T1 (SCAN-01 through SCAN-08) | PASS | N/A | PASS |
| CO-02 | 7-scan checklist present in T2 | N/A | PASS | PASS |
| CO-03 | CYC budgets explicitly stated for all methods | PASS | PASS | PASS |
| CO-04 | Method signatures fully specified | PASS | PASS | PASS |
| CO-05 | T2 gates on T1 VERIFY_PASS explicitly stated | N/A | PASS | PASS |
| CO-06 | [Fact] test names labelled T-B15-01 through T-B15-06 | N/A | PASS | PASS |
| CO-07 | [Fact] tests are pure Math.Round arithmetic — no NT8 runtime | N/A | PASS | PASS |
| CO-08 | T1 completion criteria: read _statusText + record in NT8_ADDON_KNOWLEDGE.md | PASS | N/A | PASS |
| PRE-01 | JS pre-check — no P0 patterns in proposed code | PASS | PASS | PASS |
| PRE-02 | CYC pre-check — all stated CYC values correct given described logic | PASS | PASS | PASS |
| PRE-03 | NT8 constraints pre-check — no banned patterns in proposed code | PASS | PASS | PASS |

---

## Detailed Check Notes

### TR-01 / TR-02 / TR-03 / TR-04 — Traceability

- T1 header: `Spec Req | DW-B8-04 (prerequisite investigation step)` — maps to plan §4 T1. ✅
- T2 header: `Spec Req | DW-B8-04 (closure)` — maps to plan §4 T2. ✅
- DW-B9-03 (shelved): NOT present in either ticket. ✅
- Protected files: `CopyEngine.cs`, `TradeCopierAddOn.cs`, `TradeCopierWindow.cs`,
  `AtrSizingEngine.cs` appear only in "PROTECTED — do not touch" tables. ✅

### JS-01 — No lock() (JS-021)

No `lock(` in any proposed code block in T1 or T2. ✅

### JS-02 — No async void (JS-033)

No `async void` in any proposed method. All methods are synchronous void or private static
double. Dispatcher.InvokeAsync is a fire-and-forget call, not async void. ✅

### JS-03 — No return null (JS-002)

- `DumpReflectionPath`: returns void. ✅
- `DumpVisualTree`: returns void. ✅
- `DumpChartControlTree`: returns void. ✅
- `GetPriceAtY`: returns `0.0` (double) on all guard paths, NOT null. ✅

### JS-04 — No throw in hot paths (JS-001)

No `throw new ...Exception(...)` in any proposed code. The `catch (Exception ex)` in
`DumpReflectionPath` appends to StringBuilder and returns — it does NOT re-throw. ✅

### NT-01 — NT8-009 investigation gates T2

T2 gate statement (lines 452–465): "DO NOT begin T2 implementation until
ticket-1-verification.md exists and contains VERIFY_PASS." ✅

### NT-02 — NT8-035 stub removed in T2

T2 Step 2 instructs removing `_chartDiagDone`, `DumpReflectionPath`, `DumpVisualTree`,
`DumpChartControlTree`. T2 Step 4 removes `double price = 0.0` and `_ = e.GetPosition(chartControl)`.
SCAN-04 and SCAN-05 both verify these are absent. ✅

### NT-03 — NT8-029 tick-align in T2

T2 Step 4 line: `double price = Math.Round(rawPrice / tickSize) * tickSize;` ✅
SCAN-06 verifies `Math.Round.*tickSize` is present. ✅

### NT-04 — NT8-007 CreateOrder arg 12

T2 Step 4 shows the full CreateOrder call with:
`(NinjaTrader.Cbi.CustomOrder)null);` as argument 12. ✅
Completion artifact also requires NT8-007 arg 12 verification. ✅

### NT-05 — NT8-003 no volatile double

`_chartDiagDone` is `volatile bool` (allowed). No `volatile double` anywhere. ✅

### NT-06 — NT8-017 _chartDiagDone is volatile bool

Step 2 explicitly states: "Field type is `volatile bool` (allowed). Field type is NOT
`volatile double` (would violate NT8-003)." SCAN-04 verifies `volatile bool` is on
the field declaration. ✅

### NT-08 — NT8-020 no new SolidColorBrush

No `new SolidColorBrush(` in any code block in T1 or T2. ✅

### CO-01 — Scan checklist present in T1

T1 contains SCAN-01 through SCAN-08 (8 scans, including the new SCAN-08 for NT8-008). ✅
Note: T1 has EIGHT scans (SCAN-08 added for NT8-008 enforcement). This exceeds the 7-scan
minimum and is correct — it is defence in depth for the V-02 fix. ✅

### CO-02 — Scan checklist present in T2

T2 contains SCAN-01 through SCAN-07. All 7 present. ✅

### CO-03 — CYC budgets explicitly stated

T1 header table: `DumpChartControlTree CYC <= 3; DumpReflectionPath CYC <= 7; DumpVisualTree CYC <= 6; SetChart CYC = 2`
Each method has a dedicated CYC count verification table. ✅
T2 header table: `GetPriceAtY = 4; OnChartMouseDown final = 6`
CYC summary table (T1 lines 401–408) itemises all four methods. ✅

### CO-04 — Method signatures fully specified

T1:
- `private void DumpReflectionPath(ChartControl cc, System.Text.StringBuilder sb)` ✅
- `private void DumpVisualTree(ChartControl cc, System.Text.StringBuilder sb)` ✅
- `private void DumpChartControlTree(ChartControl cc)` ✅
- `private volatile bool _chartDiagDone = false;` ✅

T2:
- `private static double GetPriceAtY(ChartControl cc, double y)` ✅
- 6 [Fact] test method signatures all present with exact names ✅

### CO-05 — T2 gates on T1 VERIFY_PASS

Explicit gate statement: "T1 VERIFY_PASS required. NT8_ADDON_KNOWLEDGE.md must contain
'## B15 Discoveries' with confirmed API path." ✅

### CO-06 — [Fact] tests labelled T-B15-01 through T-B15-06

Comments in test block: `// T-B15-01:` through `// T-B15-06:` present in T2 Step 5. ✅

### CO-07 — [Fact] tests pure math, no NT8 runtime

All 6 tests use `TickAlign(double, double)` which is a pure `Math.Round` helper with no
NT8 types. Ticket explicitly states: "No NT8 runtime required — pure double arithmetic." ✅

### CO-08 — T1 completion criteria include _statusText + NT8_ADDON_KNOWLEDGE.md

T1 Engineer Deliverable section requires: paste raw _statusText output into
NT8_ADDON_KNOWLEDGE.md B15 Discoveries, complete resolved questions table, fill in
T2 confirmed API call. Completion artifact requires: "Whether NT8_ADDON_KNOWLEDGE.md
B15 Discoveries section was written." ✅

### PRE-01 — JS pre-check (no P0 patterns in proposed code)

Scanned all code blocks in T1 and T2 for P0 patterns:
- `lock(` → 0 occurrences ✅
- `async void ` → 0 occurrences ✅
- `return null;` → 0 occurrences (guards return `0.0` or `void`) ✅
- `throw new ...Exception(` → 0 occurrences ✅

### PRE-02 — CYC pre-check

All methods stated CYC values verified against implementation:
- DumpReflectionPath: stated 7, actual 8 (ternary missed in table) — still ≤ 8. WARN only. ✅
- DumpVisualTree: stated 6, actual 6. Accurate. ✅
- DumpChartControlTree: stated 3 (conservative), actual 2. Accurate/conservative. ✅
- GetPriceAtY: stated 4, actual 4. Accurate. ✅
- OnChartMouseDown after T2: stated 6, actual 6. Accurate. ✅

No method exceeds CYC 8. PASS.

### PRE-03 — NT8 constraints pre-check (no banned patterns)

- NT8-008: `chart.ChartControl` — ABSENT from T1 SetChart. `FindVisualChild<ChartControl>(chart)` is sole path. ✅
- NT8-009: `chartControl.GetValueByY(` direct call — ABSENT (GetPriceAtY uses ChartPanel path via confirmation from T1). ✅
- NT8-003: `volatile double` — ABSENT. Only `volatile bool` used. ✅
- NT8-019: `async void` — ABSENT. ✅
- NT8-013: `DateTime.Now` — ABSENT. `DateTime.MaxValue` in CreateOrder. ✅
- NT8-007: CreateOrder arg 12 as bare `null` — ABSENT. `(NinjaTrader.Cbi.CustomOrder)null` used. ✅
- NT8-035: `price = 0.0` in OnChartMouseDown — removed in T2. T1 explicitly states stub stays unchanged. ✅
- NT8-014: Signal name — `"PTT-Click"` confirmed in CreateOrder. ✅

No NT8 banned patterns in any code block. PASS.

### File Routing — T1 and T2

All `.cs` file paths reference `src/PropTraderTools/TradeCopierPanel.cs` and
`src/PropTraderTools/CopyEngineTests.cs`. Wave workspace stated as
`c:\WSGTA\universal-or-strategy` in both ticket headers. No Director workspace paths. ✅

---

## Warnings (Non-Blocking)

| WARN-ID | Ticket | Location | Description |
|---------|--------|----------|-------------|
| WARN-C2-01 | T1 | Step 3a CYC table for DumpReflectionPath | Ternary `gvbMethod != null ? "YES" : "NO"` (line 127) is a decision point not listed in the CYC table. True CYC = 8, not 7 as stated. CYC = 8 is at the budget limit but NOT > 8. Not a blocker. Architect may correct the stated CYC to 8 in a future pass. |

---

## Per-Ticket Verdict

### T1 — ChartControl Visual Tree Diagnostic

| Check | Result |
|-------|--------|
| Traceability (TR-01, TR-04) | PASS |
| JS Pre-Check (JS-021, JS-033, JS-002, JS-001) | PASS |
| CYC Pre-Check (V-01-FIX) | PASS — all three methods ≤ 8 |
| NT8 Constraints (V-02-FIX, NT8-008, NT8-003, NT8-017, NT8-020) | PASS |
| Test Coverage | PASS (T1 is runtime investigation; no unit-testable logic) |
| Scan Checklist | PASS (SCAN-01 through SCAN-08 all present — 8 scans) |
| File Routing | PASS (Wave workspace `c:\WSGTA\universal-or-strategy`) |
| **VERDICT** | **TICKET_REVIEW_PASS** |

### T2 — Replace 0.0 Stub: Y-to-Price Conversion + Tick-Align

| Check | Result |
|-------|--------|
| Traceability (TR-02, TR-04) | PASS |
| JS Pre-Check (JS-021, JS-033, JS-002, JS-001) | PASS |
| CYC Pre-Check | PASS — GetPriceAtY CYC=4, OnChartMouseDown CYC=6 |
| NT8 Constraints (NT8-007, NT8-009, NT8-029, NT8-035) | PASS |
| Test Coverage | PASS — 6 [Fact] tests T-B15-01 through T-B15-06, pure math |
| Scan Checklist | PASS (SCAN-01 through SCAN-07 all present) |
| File Routing | PASS |
| **VERDICT** | **TICKET_REVIEW_PASS** |

---

## Cycle 2 Repair Confirmation

| Violation (Cycle 1) | Fix Required | Cycle 2 Status |
|--------------------|-------------|---------------|
| V-01: DumpChartControlTree CYC=14 — monolithic method exceeded CYC 8 | Split into DumpReflectionPath (CYC≤8) + DumpVisualTree (CYC=6) + DumpChartControlTree orchestrator (CYC≤3) | **FIXED — PASS** |
| V-02: `chart.ChartControl` P0 NT8-008 banned pattern in SetChart primary implementation | Replace with `TradeCopierAddOn.FindVisualChild<ChartControl>(chart)` as sole path; remove fallback comment | **FIXED — PASS** |

---

## Overall Verdict

**TICKET_REVIEW_PASS**

Both tickets pass all checks. The two Cycle 1 violations (V-01 CYC > 8 and V-02 NT8-008
banned pattern) are fully resolved in the Cycle 2 repairs. One non-blocking warning issued
(WARN-C2-01: DumpReflectionPath CYC stated as 7, actual 8 due to ternary; still within budget).

**Engineer may proceed. T1 first; T2 blocked on T1 VERIFY_PASS.**
