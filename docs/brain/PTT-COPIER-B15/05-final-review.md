# PTT-COPIER-B15 -- Final Review
# Reviewer: ptt-plan-reviewer (Phase 5)
# Block: PTT-COPIER-B15
# Date: 2026-07-14
# Prior plan review: docs/brain/PTT-COPIER-B15/02-plan-review.md (REVIEW_PASS)
# Ticket review: docs/brain/PTT-COPIER-B15/04-ticket-review.md (TICKET_REVIEW_PASS Cycle 2)
# T1 verification: docs/brain/PTT-COPIER-B15/ticket-1-verification.md (VERIFY_PASS)
# T2 verification: docs/brain/PTT-COPIER-B15/ticket-2-verification.md (VERIFY_PASS)
# Wave workspace: c:\WSGTA\universal-or-strategy (READ ONLY scan)

---

## Cross-File Coherence Checks

### CF-01: FindVisualChild<T> access modifier

| Check | Expected | Actual | Status |
|-------|----------|--------|--------|
| `FindVisualChild<T>` in TradeCopierAddOn.cs is `internal static` | `internal static T FindVisualChild<T>(...)` | Line 501: `internal static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject` | **PASS** |

`TradeCopierPanel.GetPriceAtY` at line 298 calls
`TradeCopierAddOn.FindVisualChild<NinjaTrader.Gui.Chart.ChartPanel>(cc)`.
The method is `internal static` — accessible from `TradeCopierPanel` (same assembly, same namespace `PropTraderTools`). Cross-file call is valid.

### CF-02: CreateOrder wiring — all 12 args

`OnChartMouseDown` at `TradeCopierPanel.cs:1136-1144` (verified independently):

```csharp
_leaderAccount.CreateOrder(
    _instrument, action,                     // args 1, 2
    OrderType.Limit,                         // arg  3
    OrderEntry.Manual,                       // arg  4
    TimeInForce.Day,                         // arg  5
    qty, price, 0, null,                     // args 6, 7, 8, 9
    "PTT-Click",                             // arg 10 (NT8-014: "PTT-" prefix)
    DateTime.MaxValue,                       // arg 11 (NT8-013: GTC sentinel)
    (NinjaTrader.Cbi.CustomOrder)null);      // arg 12 (NT8-007: explicit cast)
```

All 12 arguments present and correct. NT8-007, NT8-013, NT8-014 all satisfied. **PASS.**

### CF-03: `_trailBeLastPnl` is `plain long` (not `volatile long`)

At `CopyEngine.cs:107`:
```csharp
private          long   _trailBeLastPnl       = 0L; // Interlocked.Read/CompareExchange provide memory barrier
```

The field is a plain `long`, NOT `volatile long`. This is the correct B14 T1 pattern:
NT8-003 bans `volatile` on 64-bit types; `Interlocked.Read` / `Interlocked.CompareExchange`
provide the required memory barrier. The B15 F5 fix is confirmed applied. **PASS.**

FS-04 scan result: 0 `volatile long` declarations in src/ — **PASS.**

### CF-04: CopyEngineTests T_B15_01 through T_B15_06 appended without disturbing existing tests

All 6 [Fact] tests verified at lines 1664-1719 in `CopyEngineTests.cs`:
- `T_B15_01_TickAlign_MesPriceBelowTick_RoundsDown` (line 1665)
- `T_B15_02_TickAlign_MesPriceAboveHalfTick_RoundsUp` (line 1674)
- `T_B15_03_TickAlign_PriceExactTick_Unchanged` (line 1683)
- `T_B15_04_TickAlign_PriceExactlyHalfTick_BankersRound` (line 1692)
- `T_B15_05_TickAlign_CrudePriceRoundTrip` (line 1703)
- `T_B15_06_TickAlign_ZeroPrice_ReturnsZero` (line 1712)

Appended after the last pre-B15 test (line 1658 B14 CAS test). Closing `}` of class at line 1723.
No existing test methods disturbed. **PASS.**

### CF-05: No cross-file compile dependency broken

- `TradeCopierPanel.GetPriceAtY` calls `TradeCopierAddOn.FindVisualChild<ChartPanel>` — `internal static` — accessible.
- `TradeCopierPanel.OnChartMouseDown` calls `CopyEngine.Instance.GetSuggestedQty` — unchanged.
- `TradeCopierPanel.SetChart` is reverted to single-assignment form — no new dependencies.
- All T1 diagnostic methods (`DumpReflectionPath`, `DumpVisualTree`, `DumpChartControlTree`, `_chartDiagDone` field) have been removed in T2 — no dangling references.

**PASS.**

---

## DW-B8-04 Closure Verification

### DW-01: `double price = 0.0` stub absent

FS-07 scan: `Select-String -Pattern "price\s*=\s*0\.0"` in TradeCopierPanel.cs → **0 results.** PASS.

### DW-02: DW-B8-04 deferred comment absent

Ticket-2-verification SCAN-04: `DW-B8-04` pattern in TradeCopierPanel.cs → **0 results.** Confirmed
by V-T2-10 independent verification. The stub comment block (lines 1223-1225 in T1-state) has been
fully replaced by the B15 T2 comment at line 1123. **PASS.**

### DW-03: GetPriceAtY routes through FindVisualChild<ChartPanel>

`GetPriceAtY` at `TradeCopierPanel.cs:295-303`:
```csharp
private static double GetPriceAtY(ChartControl cc, double y)
{
    if (cc == null) return 0.0;                                                          // guard (1)
    var panel = TradeCopierAddOn.FindVisualChild<NinjaTrader.Gui.Chart.ChartPanel>(cc);
    if (panel == null) return 0.0;                                                       // guard (2)
    double raw = panel.GetValueByY(y);
    if (raw <= 0.0) return 0.0;                                                          // guard (3)
    return raw;
}
```

Routes through `FindVisualChild<ChartPanel>` — NOT through `ChartBars` (NT8-036 compliant).
`panel.GetValueByY(y)` is called on `ChartPanel`, NOT on `ChartControl` (NT8-009 compliant).
**PASS.**

### DW-04: Tick-align formula in OnChartMouseDown

`TradeCopierPanel.cs:1129`:
```csharp
double price = Math.Round(rawPrice / tickSize) * tickSize;
```

NT8-029 tick-align formula is present. Formula matches the SAFE pattern. **PASS.**

### DW-05: NT8_ADDON_KNOWLEDGE.md B15 Discoveries documents confirmed API path

`docs/standards/NT8_ADDON_KNOWLEDGE.md` section `## B15 Discoveries (2026-07-14)` confirmed present.
Key content:
- `ChartBars=NO` (reflection probe confirmed property absent)
- `ChartPanel` is direct visual child of `ChartControl` at depth=1 (VT walk confirmed)
- Confirmed access path: `FindVisualChild<NinjaTrader.Gui.Chart.ChartPanel>(chartControl)`
- NT8-036 appended to NT8_COMPILER_RULES.md

**PASS.**

---

## Protected Files Verification

### PF-01: CopyEngine.cs

Last B-block change comment: B14 T1. T2 verification confirms no B15-T2 changes.
`_trailBeLastPnl` is `plain long` at line 107 — this is the pre-existing B14 T1 design, not scope creep.
`Select-String -Pattern "B15-T2|B15 T2"` → 0 results. **PASS.**

### PF-02: TradeCopierAddOn.cs

`FindVisualChild<T>` confirmed `internal static` at line 501.
T2 verification: `Select-String -Pattern "B15"` in TradeCopierAddOn.cs → 0 results.
No B15 changes. The `internal static` access modifier on `FindVisualChild` was a pre-existing
B11/earlier state — it was already `internal static`. No modifications in B15. **PASS.**

**NOTE on plan vs. reality:** The plan and ticket protected-file table listed TradeCopierAddOn.cs
as PROTECTED — do not touch. The verifier's check (SCAN result: 0 B15 hits) confirms it was NOT
touched. CF-01 verified that `FindVisualChild` was already `internal static` from the B11 era
and required no modification for T2. ✅

### PF-03: TradeCopierWindow.cs

File header: `// PTT-COPIER-B11-T2 -- TradeCopierWindow.cs`. No B15 header.
T2 verification: `Select-String -Pattern "B15"` → 0 results. **PASS.**

### PF-04: AtrSizingEngine.cs

T2 verification: `Select-String -Pattern "B15"` → 0 results. The two `volatile double` comment
hits in AtrSizingEngine.cs are pre-existing explanatory comments (not code). **PASS.**

---

## 7-Scan Final Pass (Wave workspace src/PropTraderTools/)

All scans run independently via PowerShell Select-String on `c:\WSGTA\universal-or-strategy\src\PropTraderTools\*.cs`.

| Scan | Pattern | Scope | Layer 5 Result | Assessment | Status |
|------|---------|-------|---------------|------------|--------|
| FS-01 | `lock(` code hits | all .cs | CopyEngine.cs:562 `block(` in comment; CopyEngine.cs:1197 `block(` in comment. Zero actual `lock(` code. | Comment substring false positives. 0 executable lock() calls. | **PASS** |
| FS-02 | `async void ` declarations | all .cs | 0 results | Zero `async void` method declarations across entire src/ | **PASS** |
| FS-03 | `volatile double` code declarations | all .cs | AtrSizingEngine.cs:13 comment; AtrSizingEngine.cs:49 comment. Zero field declarations. | Pre-existing explanatory comments; 0 actual `volatile double` fields. | **PASS** |
| FS-04 | `volatile long` code declarations | all .cs | 0 results | Zero `volatile long` fields. `_trailBeLastPnl` is plain `long` (correct pattern). | **PASS** |
| FS-05 | `\.GetValueByY(` | all .cs | TradeCopierPanel.cs:300 `double raw = panel.GetValueByY(y);` — single hit on `panel` (ChartPanel type), NOT on ChartControl. | 1 hit, correct target. NT8-009 and NT8-036 satisfied. | **PASS** |
| FS-06 | `ChartBars` | TradeCopierPanel.cs | 0 results | NT8-036 SAFE pattern used exclusively. | **PASS** |
| FS-07 | `price\s*=\s*0\.0` | TradeCopierPanel.cs | 0 results | Stub removed in T2. DW-B8-04 stub comment absent. NT8-035 satisfied. | **PASS** |

**All 7 final scans: PASS.**

---

## CYC Final Audit

### OnChartMouseDown (TradeCopierPanel.cs:1115-1154)

Independent CYC count from final source:

| # | Guard/Branch | Construct |
|---|--------------|-----------|
| 1 | `if (!_clickArmed) return` | guard |
| 2 | `if (_leaderAccount == null) return` | guard |
| 3 | `if (_instrument == null) return` | guard |
| 4 | `if (chartControl == null) return` | guard |
| 5 | `if (rawPrice <= 0.0) return` | guard |
| 6 | `isBuy ? OrderAction.Buy : OrderAction.SellShort` | ternary |
| 7 | `catch (Exception ex)` | exception branch |

**Final CYC = 7.** Plan stated CYC = 6 (the `catch` branch at line 1146 was present but not
counted in the plan CYC table — it is a pre-existing catch from the B9/B10 era, not new code
introduced in B15). CYC = 7 ≤ 8 (Jane Street budget). **Not a FAIL.**

**This is a documentation discrepancy only** — the plan CYC table missed the catch branch.
True CYC = 7, which is within budget. No rule is violated.

### GetPriceAtY (TradeCopierPanel.cs:295-303)

CYC = 4 (3 guards + base). Confirmed by T2 verification. **PASS.**

### SetChart (TradeCopierPanel.cs:285-288)

Reverted to CYC = 1 (single assignment). **PASS.**

---

## Jane Street DNA Final Check (cross-file)

| Rule | Pattern Searched | Result | Status |
|------|-----------------|--------|--------|
| JS-021 `lock()` | FS-01 scan | 0 code hits | **PASS** |
| JS-033 `async void` | FS-02 scan | 0 declarations | **PASS** |
| JS-002 `return null` | `GetPriceAtY` returns 0.0 (double) on all guards | Not null | **PASS** |
| JS-001 `throw` in hot path | No `throw` in GetPriceAtY, OnChartMouseDown; catch re-wraps to _statusText | **PASS** |
| JS-023 cross-thread volatile | `_clickArmed`, `_clickBuy` remain `volatile bool`; `_chartDiagDone` removed as intended | **PASS** |
| NT8-003 `volatile double` banned | FS-03: 0 code declarations | **PASS** |
| NT8-036 `ChartBars` banned | FS-06: 0 hits | **PASS** |
| NT8-029 tick-align | Math.Round formula at line 1129 | **PASS** |
| NT8-007 CreateOrder arg 12 | `(NinjaTrader.Cbi.CustomOrder)null` at line 1144 | **PASS** |
| NT8-009 ChartControl.GetValueByY | FS-05: GetValueByY on `panel` (ChartPanel), not on chartControl | **PASS** |
| NT8-035 0.0 stub | FS-07: 0 hits | **PASS** |

**All Jane Street P0 and NT8 rules: PASS across all 4 source files.**

---

## Spec Coverage Verification (end-to-end)

| Spec Requirement | Status | Evidence |
|-----------------|--------|----------|
| Replace hardcoded 0.0 stub in OnChartMouseDown | CLOSED | FS-07 = 0; V-T2-09 PASS |
| Real Y-to-price axis conversion via ChartPanel.GetValueByY | IMPLEMENTED | GetPriceAtY at lines 295-303 |
| Tick-align result before Limit order (NT8-029) | CLOSED | Line 1129; FS scan PASS |
| API investigation before implementation (T1 first) | CLOSED | ticket-1-verification.md VERIFY_PASS |
| NT8_ADDON_KNOWLEDGE.md B15 Discoveries written | CLOSED | Section confirmed at lines 740-795 |
| NT8-036 added to NT8_COMPILER_RULES.md | CLOSED | NT8-036 confirmed at line 814 |
| DW-B9-03 NOT implemented (shelved) | CONFIRMED | No Bid+1/Ask-1 code in any file |
| 6 [Fact] xUnit tests for tick-align math | CLOSED | T_B15_01..T_B15_06 at lines 1664-1719 |
| Protected files not touched | CONFIRMED | PF-01..PF-04 all PASS |
| DW-B8-04 closure criteria (items 1-7 from ticket) | IMPLEMENTED | F5 pending (see below) |

**Note on F5 gate:** All 7 DW-B8-04 closure criteria are IMPLEMENTED in code. Items 1 and 4 have an
outstanding F5 gate:
- Item 1: "TradeCopierPanel.cs compiles in NT8 (F5 green on Sim101)" — F5 PENDING
- Item 4: "`GetPriceAtY` uses confirmed API path" — API path confirmed from T1; compile pending F5

The `GetValueByY` call on `ChartPanel` is documented NT8 API. If CS1061 is raised at F5, NT8-037
will be added and a fallback will be required (new deferred item). Until F5 confirms green,
**DW-B8-04 is IMPLEMENTED and pending F5 confirmation.**

---

## Section K — Deferred Work Ledger

### K.1 B15 Closed Items (from B14 backlog)

| ID | Item | Closed By | Notes |
|----|------|-----------|-------|
| DW-B8-04 | Fix click trader price lookup — replace hardcoded 0.0 stub with real Y-to-price axis conversion via NT8 ChartPanel.GetValueByY. | T2 VERIFY_PASS | GetPriceAtY uses FindVisualChild<ChartPanel> (NT8-036 SAFE path). Tick-align applied. 6 [Fact] tests. F5 gate pending — code IMPLEMENTED. |

### K.2 B15 Shelved Items (carry-forward, no change in B15)

| ID | Item | Reason | Next Target |
|----|------|--------|-------------|
| DW-B9-01 | ATR box visualization on chart canvas | Shelved since B9. No chart canvas API investigation in B15. | B16+ |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset. **BLOCKER REMOVED** — DW-B8-04 is now IMPLEMENTED. | Previously blocked on DW-B8-04. DW-B8-04 is now IMPLEMENTED (F5 pending). DW-B9-03 is now UNBLOCKED per Director; remains SHELVED (Director decision — not scheduled for B16). Priority: P3. | B16+ eligible |
| DW-B12-DEFER-01 (original) | Buy Ask / Sell Bid full-panel mode expansion | Shelved since B12. | future |

### K.3 New Deferred Items from B15

| ID | Item | Priority | Notes |
|----|------|----------|-------|
| DW-B15-01 | F5 gate: confirm ChartPanel.GetValueByY compiles green in NinjaTrader 8 on Sim101. If CS1061: add NT8-037 and implement fallback (MarketData.Last.Price or manual axis math). | P1 | Code implemented (T2 VERIFY_PASS). F5 is a mandatory manual runtime gate that cannot be run by the verifier. If F5 passes: close DW-B15-01 and mark DW-B8-04 FULLY CLOSED. If F5 fails: add NT8-037, implement fallback in B16 T1. |

### K.4 Open Items for B16

| ID | Description | Priority | Source |
|----|-------------|----------|--------|
| DW-B15-01 | F5 gate: ChartPanel.GetValueByY compile confirmation in NT8. Close DW-B8-04 fully on green. Add NT8-037 + fallback if CS1061. | P1 | B15 T2 implementation (F5 pending) |
| DW-B9-01 | ATR box visualization on chart canvas | P2 | Carried from B9 |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset (UNBLOCKED; SHELVED per Director) | P3 | Carried from B9; blocker DW-B8-04 now IMPLEMENTED |
| DW-B12-DEFER-01 (original) | Buy Ask / Sell Bid full-panel mode expansion | P2 | Carried from B12 |

### K.5 Running Deferred Work Ledger (B10 onwards — append B15 rows)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B10-01 | Remove BuildDiagRow / OnDiagGap001d / OnDiagGap002 scaffolding | P2 | B11 | CLOSED (B11 T1) |
| DW-B10-02 | Add 3 missing AtrSizingEngine xUnit tests | P1 | B11 | CLOSED (B11 T2) |
| DW-B10-03 | TradeCopierWindow.cs Arm BE column | P2 | B11 | CLOSED (B11 T2) |
| DW-B10-04 | Update NT8_ADDON_KNOWLEDGE.md with T4 chart attachment result | P1 | B11 | CLOSED (B11 T1) |
| DW-B8-04 | Fix click trader price lookup (Y-to-price via ChartPanel.GetValueByY) | P2 | B15 | IMPLEMENTED — F5 pending (DW-B15-01) |
| DW-B9-01 | ATR box visualization on chart canvas | P2 | B16+ | OPEN |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset — BLOCKER REMOVED (DW-B8-04 IMPLEMENTED). SHELVED per Director. | P3 | B16+ eligible | OPEN (UNBLOCKED, SHELVED) |
| DW-B11-DEFER-01 | Convert Flatten/Trim keyboard shortcuts to Limit orders | P1 | B12 | CLOSED (B12 T1) |
| DW-B12-DEFER-01 | Wire GetRefPrice() to _instrument.MarketData.Last.Price | P1 | B13 | CLOSED (B13 T1) |
| DW-B12-DEFER-02 | ATR fraction spinner startup sync | P2 | B13 | CLOSED (B13 T2) |
| DW-B12-DEFER-03 | Correct Math.Clamp comment attribution (NT8-003 -> NT8-034) | P3 | B13 | CLOSED (B13 T3) |
| DW-B12-DEFER-01 (original) | Full-panel mode expansion: Buy Ask / Sell Bid quick-entry buttons | P2 | future | OPEN |
| DW-B12-DEFER-02 (original) | Auto-trail stop from BE CONNECTED level | P3 | B14 | CLOSED (B14 T1) |
| DW-B12-DEFER-04 | Align CopyEngineTests.cs test names with 04-tickets.md contract names | P3 | B14 | CLOSED (B14 T2) |
| DW-B15-01 | F5 gate: ChartPanel.GetValueByY compile in NT8. Add NT8-037 + fallback if CS1061. | P1 | B16 | OPEN |

---

## Verdict

**FINAL_PASS**

All cross-file coherence checks pass. All DW-B8-04 closure checks pass. All protected files
are unchanged. All 7 final scans return 0 violations. Jane Street DNA is clean across all 4
source files. CYC = 7 for OnChartMouseDown (not 6 as documented in plan — catch branch was
pre-existing, not new B15 code) — within the ≤8 Jane Street budget, not a rule violation.

Section K is present and complete. 06-deferred-backlog.md is written.

**The single open item (DW-B15-01) is a manual F5 runtime gate** — not a code review finding.
The code is implemented, reviewed, and verified. F5 must be run by the engineer on Sim101.
If F5 passes: DW-B8-04 is FULLY CLOSED, DW-B15-01 is CLOSED.
If F5 fails (CS1061 on GetValueByY): NT8-037 is added and B16 T1 implements the fallback.

**FINAL_PASS**
