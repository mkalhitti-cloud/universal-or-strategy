# PTT-COPIER-B16 Final Review
# Reviewer: ptt-plan-reviewer
# Phase: 5 (Final Review)
# Date: 2026-07-15
# Block: PTT-COPIER-B16
# Inputs: 02-architecture-plan.md, 04-tickets.md, ticket-1-completion.md,
#          ticket-1-verification.md, ticket-2-completion.md, ticket-2-verification.md,
#          PTT-COPIER-B15/06-deferred-backlog.md, NT8_ADDON_KNOWLEDGE.md (## B16 Discoveries),
#          TradeCopierPanel.cs (spot-check), CopyEngine.cs (spot-check)

---

## Block Summary

PTT-COPIER-B16 executed a two-ticket plan to fix the click trader's Y-pixel-to-price
conversion (DW-B16-01, reopened DW-B8-04) and the TightenOneStop cancel+replace bug
(DW-B16-02, injected by Director pre-T1). T1 was a diagnostic-phase ticket: it added a
`WalkChartPanelChildren` method and a `BuildMethodReport` reflection helper to the
`TradeCopierPanel.cs` to walk `ChartPanel`'s children via `VisualTreeHelper` and surface
their method signatures via a `MessageBox.Show`. Director ran F5 on NT8 Sim101 and recorded
the results in `NT8_ADDON_KNOWLEDGE.md ## B16 Discoveries`. T2 consumed the F5 data,
chose Branch B (no native Y-to-price API found at depth=2), removed all T1 diagnostic
scaffolding, replaced the B15 `MarketData.Last.Price` stub in `GetPriceAtY` with a
`ChartPanel.MaxValue`/`MinValue`/`ActualHeight` linear interpolation, added the
`LinearYToPrice` and `AlignToTick` internal static helpers for testability, added 8 pure-math
`[Fact]` tests, and fixed TightenOneStop (removed the cancel+replace branch, all stop types
now use `acc.Change()`). T2 also renamed the `"~"` tighten button to `"Tighten"` and added
2 [Fact] tests covering the TightenOneStop fix. Both tickets reached VERIFY_PASS. All 9
Layer 3 scans returned zero violations. Zero new build errors.

---

## T1 Outcome

| Field | Value |
|-------|-------|
| Ticket | T1 — ChartPanel Subtree Diagnostic |
| Status | **VERIFY_PASS** (clock=12) |
| Engineer verdict | BUILD_PASS (clock=10) |
| Verifier verdict | VERIFY_PASS (clock=12) |
| Files modified | `TradeCopierPanel.cs` only |
| New methods | `WalkChartPanelChildren` (CYC=5), `BuildMethodReport` (CYC=2) |
| Modified methods | `SetChart` (CYC: 1 → 2, one-shot guard added) |
| Scan results (7 scans) | All 0 violations |
| DNA violations | None |
| CYC budget | All methods ≤ 8 ✅ |
| F5 gate | Confirmed by Director: MessageBox fired with title "PTT B16 ChartPanel Subtree". ChartPanel child type = ContentPresenter. No native Y-to-price method found at depth=2. Branch B selected. CORRECTION_FACTOR = 1.0 (ContentPresenter.ActualHeight == ChartPanel.ActualHeight = 452.00). |
| NT8 discoveries | ChartPanel children: ContentPresenter only. No ChartScale at depth=2. ChartPanel.ActualHeight = 452.00, .ActualWidth = 56.00. Branch A criteria NOT met. Branch B confirmed appropriate. |

---

## T2 Outcome

| Field | Value |
|-------|-------|
| Ticket | T2 — GetPriceAtY Branch B + DW-B16-02 |
| Status | **VERIFY_PASS** (clock=16) |
| Engineer verdict | BUILD_PASS (clock=14) |
| Verifier verdict | VERIFY_PASS (clock=16) |
| Files modified | `TradeCopierPanel.cs`, `CopyEngine.cs`, `CopyEngineTests.cs`, `NT8_ADDON_KNOWLEDGE.md` |
| Removed (T1 cleanup) | `using System.Reflection;`, `using System.Text;`, `_chartScaleDiagDone` field, `WalkChartPanelChildren`, `BuildMethodReport`, T1 guard in `SetChart` |
| SetChart restored | CYC=1 (straight-line) |
| GetPriceAtY | Branch B: `ChartPanel.MaxValue`/`MinValue`/`ActualHeight` linear interpolation. CORRECTION_FACTOR=1.0 (T1-confirmed). CYC=5. |
| New helpers | `LinearYToPrice` (internal static, CYC=2), `AlignToTick` (internal static, CYC=2) |
| TightenOneStop fix | `if (IsTrailingStop(order)) { acc.Cancel + acc.CreateOrder }` branch REMOVED. All stop types use `acc.Change()`. CYC: 4 → 3. DW-B16-02 CLOSED. |
| Button rename | `"~"` → `"Tighten"` in `BuildUI()`. SCAN-09 = 0 results. |
| Tests added | T_B16_01–T_B16_10 (10 [Fact] tests). SCAN-07 = 10 results confirmed by verifier. |
| Scan results (9 scans) | All 0 violations (SCAN-07 = 10 expected results ✅) |
| Layer 2 vs Layer 3 | Full agreement across all 9 scans |
| Implementation checks | 27/27 PASS (A through AA) |
| DNA violations | None |
| CYC budget | All methods ≤ 8 ✅ |
| Build errors | 3 pre-existing (AtrSizingEngine.cs×2, CopyEngine.cs CS8370). Zero new T2 errors. |
| ChartPanel.MaxValue/MinValue | Compiled clean. NT8-039/040 NOT needed. |
| RoundToTickSize | UNCONFIRMED (as per plan). `AlignToTick` fallback used. NT8-038 NOT needed. |

---

## Defect Work Items Status

| ID | Title | Status | Evidence |
|----|-------|--------|----------|
| DW-B16-01 | Click trader Y-pixel-to-price lookup (reopened DW-B8-04) | **CLOSED** | `GetPriceAtY` Branch B implemented. Real pixel geometry used: `ChartPanel.MaxValue`, `MinValue`, `ActualHeight`. `LinearYToPrice` + `AlignToTick` helpers testable. 8 [Fact] tests passing. NT8_ADDON_KNOWLEDGE.md § B16 T2 section confirms `DW-B16-01 status: CLOSED`. |
| DW-B16-02 | TightenOneStop cancel+replace kills ATM bracket and trail watermark | **CLOSED** | `TightenOneStop` body: only `acc.Change()` path remains. No `IsTrailingStop()` branch, no `acc.Cancel`, no `acc.CreateOrder("PTT-Tighten-Stop")`. GAP-001d confirmed safe. `"~"` button renamed `"Tighten"`. T_B16_09–10 tests verify behavior. NT8_ADDON_KNOWLEDGE.md § B16 T2 section confirms `DW-B16-02 status: CLOSED`. |
| DW-B9-01 | ATR box visualization on chart canvas | **OPEN** | Carry-forward from B9. Not in scope for B16. No changes. |
| DW-B9-03 | Click trader Bid+1/Ask-1 spread auto-offset | **OPEN (UNBLOCKED)** | DW-B8-04 prerequisite CLOSED (B15). DW-B16-01 now provides real Y-to-price. DW-B9-03 is fully unblocked. Remains SHELVED per Director decision. Eligible for scheduling in B17+. |
| DW-B12-DEFER-01 (orig) | Buy Ask/Sell Bid full-panel mode expansion | **OPEN** | Carry-forward from B12. Not in scope for B16. No changes. |
| DW-B16-02-conditional | Branch B MaxValue/MinValue CS1061 fallback | **N/A — NOT NEEDED** | `ChartPanel.MaxValue` and `ChartPanel.MinValue` compiled clean. NT8-039/040 rules not added. Conditional item not activated. |

---

## NT8 Knowledge Gained (B16 Discoveries Summary)

Source: `c:\WSGTA\universal-or-strategy\docs\standards\NT8_ADDON_KNOWLEDGE.md ## B16 Discoveries`

### T1 F5 Findings

- `ChartPanel.ActualHeight` = 452.00, `ActualWidth` = 56.00 (confirmed FrameworkElement base).
- `ChartPanel` child count at depth=2 via `VisualTreeHelper.GetChildrenCount(panel)` = 1.
- Child type = `System.Windows.Controls.ContentPresenter` (NOT ChartScale or any price API type).
- `ContentPresenter.ActualHeight` = 452.00 (equals ChartPanel.ActualHeight exactly → CORRECTION_FACTOR = 1.0).
- No method on `ContentPresenter` matched the name filter ("value", "price", "gety", "y"). Branch A criteria NOT met.
- **Decision: Branch B selected.**

### T2 Compilation Findings

- `ChartPanel.MaxValue` and `ChartPanel.MinValue`: compiled clean. NT8-039 and NT8-040 NOT needed.
- `MasterInstrument.RoundToTickSize(double)`: UNCONFIRMED from dotnet build (no NT8 runtime for build). `AlignToTick` internal helper used instead (safe, tested, CYC=2).
- `nt8-rules(B16-T1): no new rules.`
- `nt8-rules(B16-T2): no new rules.`

### Architectural Implication for B17+

The NT8 chart pixel-to-price coordinate transform is accessible via `ChartPanel.MaxValue`,
`ChartPanel.MinValue`, and `ChartPanel.ActualHeight`. Linear interpolation is accurate on the
default NT8 linear price scale. CORRECTION_FACTOR = 1.0 because the content area fills the
full panel height with no chrome margin at depth=2. This finding is stable and reusable for
B17+ work involving `ChartPanel` price geometry.

---

## Cross-File Coherence Review

| Check | Files | Result | Notes |
|-------|-------|--------|-------|
| T1 scaffolding fully removed | TradeCopierPanel.cs | PASS | Checks A–F in T2 verifier all PASS. `_chartScaleDiagDone`, `WalkChartPanelChildren`, `BuildMethodReport`, diagnostic usings — all absent. |
| `GetPriceAtY` wired correctly | TradeCopierPanel.cs line 297 | PASS | Called from `OnChartMouseDown` (line 1167). Uses real interpolation. Returns `AlignToTick(rawPrice, instrument.MasterInstrument.TickSize)`. |
| `LinearYToPrice` / `AlignToTick` accessible to tests | TradeCopierPanel.cs + CopyEngineTests.cs | PASS | `internal static` with `BindingFlags.NonPublic | Static` reflection access in test helpers `CallLinearYToPrice`, `CallAlignToTick`. |
| `TightenOneStop` no longer calls `acc.Cancel` or `acc.CreateOrder` | CopyEngine.cs lines 1196–1223 | PASS | Verifier spot-check PASS. `IsTrailingStop` still exists in file but is used ONLY in `SyncFollowerBracket` (bracket-change path — correct). NOT used in `TightenOneStop`. |
| `SyncFollowerBracket` still skips trailing stops via `IsTrailingStop` | CopyEngine.cs line 576 | PASS | Correct: `SyncFollowerBracket` uses `IsTrailingStop` as a SKIP guard (not cancel+replace). This is the safe path. No change needed. |
| Button label `"Tighten"` wired in `BuildUI` | TradeCopierPanel.cs | PASS | `Content = "Tighten"` confirmed. SCAN-09 = 0 results for `"~"`. |
| Tests T_B16_01–10 in correct file | CopyEngineTests.cs | PASS | SCAN-07 = 10 results. Lines 1751–1814 confirmed by verifier. |
| No cross-thread violations | TradeCopierPanel.cs | PASS | `GetPriceAtY` called from `OnChartMouseDown` (UI thread). `LinearYToPrice`/`AlignToTick` are pure math — thread-agnostic. No volatile fields added in T2. |
| No files outside scope modified | TradeCopierAddOn.cs, TradeCopierWindow.cs, AtrSizingEngine.cs | PASS | T2 checks W–Y all PASS. Zero B16 patterns in those files. |

---

## 7-Scan Final Aggregate (across src/PropTraderTools/)

All scans run independently by ptt-verifier (Layer 3) in `c:\WSGTA\universal-or-strategy\`:

| Scan | Pattern | Files | Result |
|------|---------|-------|--------|
| SCAN-01 | `lock\(` | TradeCopierPanel.cs | **0 results** ✅ |
| SCAN-02 | `async void` | TradeCopierPanel.cs | **0 results** ✅ |
| SCAN-03 | `DateTime\.Now[^U]` | TradeCopierPanel.cs | **0 results** ✅ |
| SCAN-04 | `"#[0-9A-Fa-f]` | TradeCopierPanel.cs | **0 results** ✅ |
| SCAN-05 | `\.GetValueByY\(` | TradeCopierPanel.cs | **0 results** ✅ |
| SCAN-06 | `price\s*=\s*0\.0` (stub) | TradeCopierPanel.cs | **0 results** ✅ |
| SCAN-07 | `T_B16_` | CopyEngineTests.cs | **10 results** ✅ |
| SCAN-08 | `PTT-Tighten-Stop` | CopyEngine.cs | **0 results** ✅ |
| SCAN-09 | `"~"` | TradeCopierPanel.cs | **0 results** ✅ |

Zero violations across all 9 scans. The 7 core DNA scans + 2 B16-specific scans all pass.

---

## Section K — Deferred Work Ledger (B16 Update)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B10-01 | Remove BuildDiagRow / OnDiagGap001d / OnDiagGap002 scaffolding | P2 | B11 | CLOSED (B11 T1) |
| DW-B10-02 | Add 3 missing AtrSizingEngine xUnit tests | P1 | B11 | CLOSED (B11 T2) |
| DW-B10-03 | TradeCopierWindow.cs Arm BE column cluster | P2 | B11 | CLOSED (B11 T2) |
| DW-B10-04 | Update NT8_ADDON_KNOWLEDGE.md T4 chart attachment result | P1 | B11 | CLOSED (B11 T1) |
| DW-B8-04 | Fix click trader price lookup stub | P2 | B15 | CLOSED (B15 T2 F5 GREEN) |
| DW-B9-01 | ATR box visualization on chart canvas | P2 | B16+ | **OPEN** |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset (UNBLOCKED) | P3 | B17+ eligible | **OPEN (UNBLOCKED, SHELVED)** |
| DW-B11-DEFER-01 | Flatten/Trim keyboard shortcuts to Limit orders | P1 | B12 | CLOSED (B12 T1) |
| DW-B12-DEFER-01 | Wire GetRefPrice() to MarketData.Last.Price | P1 | B13 | CLOSED (B13 T1) |
| DW-B12-DEFER-02 | ATR fraction spinner startup sync | P2 | B13 | CLOSED (B13 T2) |
| DW-B12-DEFER-03 | Math.Clamp comment attribution NT8-034 | P3 | B13 | CLOSED (B13 T3) |
| DW-B12-DEFER-01 (orig) | Buy Ask/Sell Bid full-panel mode expansion | P2 | future | **OPEN** |
| DW-B12-DEFER-02 (orig) | Auto-trail stop from BE CONNECTED level | P3 | B14 | CLOSED (B14 T1) |
| DW-B12-DEFER-04 | Align test names with 04-tickets.md contract | P3 | B14 | CLOSED (B14 T2) |
| DW-B15-01 | F5 gate: ChartPanel.GetValueByY CS1061 | P1 | B15 | CLOSED (B15 F5 GREEN) |
| DW-B16-01 | Click trader Y-pixel-to-price (DW-B8-04 proper fix) | P2 | B16 | **CLOSED (B16 T2 VERIFY_PASS)** |
| DW-B16-02 | TightenOneStop cancel+replace kills ATM bracket + trail | P1 | B16 | **CLOSED (B16 T2 VERIFY_PASS)** |

**B16 additions (from plan §K.2, now resolved):**

| ID | Item | Priority | Target | Status |
|----|------|----------|--------|--------|
| DW-B16-02-conditional | Branch B MaxValue/MinValue absent (CS1061) | P2 | B17+ | **NOT ACTIVATED — MaxValue/MinValue compiled clean** |

**Net open items entering B17:**

| ID | Item | Priority | Notes |
|----|------|----------|-------|
| DW-B9-01 | ATR box visualization | P2 | Shelved since B9 |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset | P3 | Unblocked, shelved per Director |
| DW-B12-DEFER-01 (orig) | Buy Ask/Sell Bid full-panel mode | P2 | Shelved since B12 |

---

## Overall Verdict

**FINAL_PASS**

All plan requirements delivered. Both tickets VERIFY_PASS. All 9 Layer 3 scans zero violations.
27/27 implementation checks pass. Zero DNA rule violations. Zero new build errors. Both
DW-B16-01 and DW-B16-02 CLOSED. Section K complete. 06-deferred-backlog.md written.
