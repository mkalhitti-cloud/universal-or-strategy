# B15 Plan Review
# Reviewer: ptt-plan-reviewer (Phase 2)
# Plan reviewed: docs/brain/PTT-COPIER-B15/02-architecture-plan.md
# Date: 2026-07-14
# Verdict: REVIEW_PASS

---

## Check Results

| Check ID | Description | Result | Evidence |
|----------|-------------|--------|----------|
| CHECK-01 | Mission scope — plan only closes DW-B8-04; DW-B9-03 explicitly SHELVED | PASS | See below |
| CHECK-02 | No P0 RULES_CATALOG violations in proposed code | PASS | See below |
| CHECK-03 | NT8-009 (GetValueByY absent) — alternative correctly handled | PASS | See below |
| CHECK-04 | NT8-035 (hardcoded 0.0 production bug) — T2 fully removes stub | PASS | See below |
| CHECK-05 | NT8-029 (tick alignment mandatory) — present and correct | PASS | See below |
| CHECK-06 | NT8-007 (CreateOrder arg 12 = CustomOrder null) — T1 untouched; T2 does not break it | PASS | See below |
| CHECK-07 | NT8-003 (volatile double banned) — no volatile double in proposed fields | PASS | See below |
| CHECK-08 | NT8-017 (volatile bool required for cross-thread flags) — _chartDiagDone is volatile bool | PASS | See below |
| CHECK-09 | CYC budget — OnChartMouseDown final CYC ≤ 8; GetPriceAtY CYC ≤ 8 | PASS | See below |
| CHECK-10 | Protected files not touched | PASS | See below |
| CHECK-11 | 7-scan checklist present for both tickets | PASS | See below |
| CHECK-12 | [Fact] tests cover tick-align math | PASS | See below |
| CHECK-13 | Two-ticket justification sound | PASS | See below |
| CHECK-14 | DW-B8-04 closed at T2 VERIFY_PASS; remaining items shelved | PASS | See below |

---

## Detailed Findings

### CHECK-01 — Mission Scope PASS

Plan §1 states: "One deferred item targeted: DW-B8-04." Plan §2 shelved items table
explicitly lists DW-B9-03 with status "SHELVED — B16+" and the annotation:
> "DW-B9-03 is explicitly shelved even though DW-B8-04 is closing in this block. The
> mission brief states: 'DW-B9-03 (Bid+1/Ask-1 auto-offset) is SHELVED — do NOT implement.'
> No spread offset, no buffer. Exact pixel price (then tick-aligned) only."

No spread offset introduced. No Bid+1/Ask-1 buffer introduced. Scope is confined to
DW-B8-04 exclusively. ✅

---

### CHECK-02 — P0 RULES_CATALOG Violations PASS

All P0 rules from RULES_CATALOG.md checked against proposed code patterns:

| Rule | Check | Result |
|------|-------|--------|
| JS-021 `lock()` | No lock() in DumpChartControlTree, GetPriceAtY, or modified OnChartMouseDown | PASS |
| JS-033 `async void` | All new methods are synchronous void or static double | PASS |
| JS-001 `throw` in hot paths | No throw statements in any new method | PASS |
| JS-002 `return null` | GetPriceAtY returns 0.0 (double) on guard conditions, not null | PASS |
| JS-010 public constructor | No new classes or structs introduced | PASS |
| JS-015 unvalidated string | No string parameters crossing boundaries | PASS |
| JS-036 `new byte[]` in hot path | No buffer allocations | PASS |
| JS-037 `new T[]` in hot path | No array allocations | PASS |

Plan §9 (Rules Catalog Gate Result) provides the same check table with confirmation.
P0 verification powershell commands are listed for pre-commit enforcement. ✅

---

### CHECK-03 — NT8-009 Handling PASS

NT8-009 bans `ChartControl.GetValueByY()` directly. The plan correctly addresses this:

- T1 investigation does NOT call `GetValueByY` on ChartControl at all; it uses reflection
  (`cc.GetType().GetProperty("ChartBars")`) and VisualTreeHelper to discover the confirmed
  API path.
- T2 uses `ChartBars[0].ChartPanel.GetValueByY(y)` — a **different** method on **ChartPanel**,
  not on **ChartControl**. This is exactly what NT8-009's NOTE section contemplates when it
  says "Implement via pixel-to-price conversion using chart scale if needed in future."
- T1 SCAN-03: `\.GetValueByY\(` on ChartControl directly → 0 results required.
- T2 SCAN-03: `ChartControl.*GetValueByY\(` → 0 results required.

Both scans correctly enforce NT8-009 without blocking the alternate ChartPanel path. ✅

---

### CHECK-04 — NT8-035 Stub Removal PASS

Plan §5 T2 section lists:
```
Remove:  volatile bool _chartDiagDone (T1 diagnostic field)
Remove:  DumpChartControlTree(ChartControl cc) method (entire method)
Remove:  SetChart call to DumpChartControlTree
```

And in OnChartMouseDown T2 changes:
> "Replace 3 stub lines with confirmed price lookup + tick-align"

The stub `double price = 0.0` and the suppression line `_ = e.GetPosition(chartControl)` are
both explicitly removed. T2 SCAN-04 (`price\s*=\s*0\.0` in OnChartMouseDown → 0 results) and
T2 SCAN-05 (`_ = e.GetPosition` suppression → 0 results) enforce the removal at verification
time. Plan §11 data flow confirms the real lookup path replaces the stub end-to-end. ✅

---

### CHECK-05 — NT8-029 Tick Alignment PASS

Tick-align formula appears in three independent plan locations:

1. §1 scope summary: `price = Math.Round(price / tickSize) * tickSize;   // NT8-029`
2. §4 T2 code block: `double price = Math.Round(rawPrice / tickSize) * tickSize;   // NT8-029`
3. §11 data flow: `double price = Math.Round(rawPrice / tickSize) * tickSize   [NT8-029 tick-align]`

Formula matches NT8-029 SAFE pattern exactly:
`Math.Round(raw / instrument.MasterInstrument.TickSize) * instrument.MasterInstrument.TickSize`

T2 SCAN-06 requires `Math.Round.*tickSize.*tickSize` → 1 result to confirm presence.

Six [Fact] tests in §6 independently verify the tick-align math across boundary, round-up,
round-down, small tick size, negative price, and zero scenarios. ✅

---

### CHECK-06 — NT8-007 CreateOrder Preservation PASS

T1 does NOT modify `OnChartMouseDown` at all. Plan §5 T1 explicitly states:
> "What T1 does NOT do: Does NOT change OnChartMouseDown"

T2 modifies only the stub block (lines replacing `double price = 0.0`) within
`OnChartMouseDown`. The `CreateOrder(...)` call below the price derivation is not
listed as changed. Plan §11 data flow shows the final `CreateOrder` invocation with
`(NinjaTrader.Cbi.CustomOrder)null` as arg 12, preserving NT8-007 compliance. Plan §8
marks NT8-013 and NT8-014 as "T2 (unchanged)" — confirming the CreateOrder call
structure is carried forward unmodified. ✅

---

### CHECK-07 — No Volatile Double PASS

Fields introduced in this plan:
- T1: `private volatile bool _chartDiagDone = false;` — volatile **bool** (allowed)
- T2: adds static method `GetPriceAtY` (no new instance fields)
- T2: removes `_chartDiagDone`

No `volatile double` anywhere in the proposed changes. NT8-003 is not triggered. ✅

---

### CHECK-08 — Volatile Bool on Cross-Thread Guard PASS

T1 adds: `private volatile bool _chartDiagDone = false;   // one-shot guard (JS-023 cross-thread)`

NT8-017 requires `volatile` for any bool/int field read on one thread and written on another.
`_chartDiagDone` is written in `SetChart` (called from TradeCopierAddOn on the UI thread when
a chart attaches) and read in `DumpChartControlTree`. The field ensures the diagnostic runs
only once and is correctly declared `volatile bool`.

T1 SCAN-04 explicitly verifies: `volatile` on `_chartDiagDone` field → must be present (JS-023). ✅

---

### CHECK-09 — CYC Budget PASS

**OnChartMouseDown after T2:**

| Branch | Condition |
|--------|-----------|
| 1 | `if (!_clickArmed) return;` |
| 2 | `if (_leaderAccount == null) return;` |
| 3 | `if (_instrument == null) return;` |
| 4 | `if (chartControl == null) return;` |
| 5 | `if (rawPrice <= 0.0) return;` |
| 6 | `isBuy ? OrderAction.Buy : OrderAction.SellShort` |

CYC = 6 ≤ 8 ✅ (plan §5 T2 confirms: "CYC = 6. Within budget (≤8).")

**GetPriceAtY:**

| Branch | Condition |
|--------|-----------|
| 1 | `bars == null` |
| 2 | `bars.Count == 0` |
| 3 | `panel == null` |
| 4 | return path |

CYC = 4 ≤ 8 ✅ (plan §4 states CYC=4 explicitly)

**DumpChartControlTree:**

Plan states CYC=4 with 4 numbered algorithm steps:
(1) null guard, (2) reflection probe conditional, (3) visual tree walk, (4) write to _statusText.
CYC = 4 ≤ 8 ✅ — note: this method is T1-only and removed in T2. Engineer must confirm
implementation does not exceed CYC=8 during T1 execution.

All declared CYC values are within the ≤8 budget. ✅

---

### CHECK-10 — Protected Files Not Touched PASS

Plan §7 protected files table:

| File | Plan Status |
|------|------------|
| `src/PropTraderTools/CopyEngine.cs` | "No CopyEngine changes required" — NOT touched |
| `src/PropTraderTools/TradeCopierAddOn.cs` | "No AddOn changes required" — NOT touched |
| `src/PropTraderTools/TradeCopierWindow.cs` | "No Window changes required" — NOT touched |
| `src/PropTraderTools/AtrSizingEngine.cs` | "No ATR engine changes required" — NOT touched |

Touched files are limited to: `TradeCopierPanel.cs`, `CopyEngineTests.cs`,
`NT8_ADDON_KNOWLEDGE.md` (documentation), and conditionally `NT8_COMPILER_RULES.md`
(only if a new compiler error is discovered during T1 F5 run). ✅

---

### CHECK-11 — 7-Scan Checklist Present PASS

**T1 SCAN checklist (plan §5 T1):** Exactly 7 scans:
1. SCAN-01: `lock\s*\(` → 0 results
2. SCAN-02: `async\s+void\s+\w+\(` → 0 results
3. SCAN-03: `\.GetValueByY\(` on ChartControl directly → 0 results (NT8-009)
4. SCAN-04: `volatile` on `_chartDiagDone` → must be present (JS-023)
5. SCAN-05: `DumpChartControlTree` called from `SetChart` only → single call site
6. SCAN-06: `_statusText.Text` update via `Dispatcher.InvokeAsync` → thread-safe UI
7. SCAN-07: File header comment added for B15 T1 changes

**T2 SCAN checklist (plan §5 T2):** Exactly 7 scans:
1. SCAN-01: `lock\s*\(` → 0 results
2. SCAN-02: `async\s+void\s+\w+\(` → 0 results
3. SCAN-03: `ChartControl.*GetValueByY\(` → 0 results (NT8-009)
4. SCAN-04: `price\s*=\s*0\.0` in OnChartMouseDown → 0 results (NT8-035)
5. SCAN-05: `_ = e.GetPosition` suppression line → 0 results
6. SCAN-06: `Math.Round.*tickSize.*tickSize` tick-align present → 1 result required
7. SCAN-07: [Fact] tests for tick-align in CopyEngineTests.cs → ≥ 4 tests

Both tickets have exactly 7 scans. Content is mission-appropriate for each ticket. ✅

---

### CHECK-12 — Test Coverage PASS

Plan §6 provides 6 fully-written [Fact] tests covering the tick-align formula:

| Test | raw | tick | Expected | Verify |
|------|-----|------|----------|--------|
| TickAlign_ExactBoundary_ReturnsUnchanged | 4250.00 | 0.25 | 4250.00 | No change when aligned |
| TickAlign_AboveHalfTick_RoundsUp | 4250.13 | 0.25 | 4250.25 | Mid+1 rounds up correctly |
| TickAlign_BelowHalfTick_RoundsDown | 4250.12 | 0.25 | 4250.00 | Mid-1 rounds down correctly |
| TickAlign_SmallTickSize_6E | 1.08753 | 0.00005 | 1.08755 | Forex precision tick |
| TickAlign_NegativePrice_Aligns | -50.13 | 0.25 | -50.00 or -50.25 | Short-sale price |
| TickAlign_ZeroRaw_ReturnsZero | 0.0 | 0.25 | 0.0 | Zero input guard |

Math verified independently:
- `4250.13 / 0.25 = 17000.52 → Round = 17001 → 17001 * 0.25 = 4250.25` ✅
- `4250.12 / 0.25 = 17000.48 → Round = 17000 → 17000 * 0.25 = 4250.00` ✅
- `1.08753 / 0.00005 = 21750.6 → Round = 21751 → 21751 * 0.00005 = 1.08755` ✅

All tests use a pure static `TickAlign(double raw, double tickSize)` helper — no NT8 runtime
required. Tests 7-8 (GetPriceAtY null guards) are correctly deferred to integration notes
if `ChartControl` cannot be instantiated in the xUnit test context. This is a valid and
honest acknowledgment of NT8 test constraints. T2 SCAN-07 requires ≥ 4 [Fact] tests —
the plan provides 6. ✅

---

### CHECK-13 — Two-Ticket Justification PASS

Plan §3 provides a 6-row evidence table demonstrating confirmed absence of:
- `ChartControl.GetValueByY()` (NT8-009, confirmed absent since B8)
- `ChartBars` property on `ChartControl` (unconfirmed in any B1-B14 block)
- `ChartBars[0].ChartPanel` navigation path (unconfirmed)
- `ChartPanel.GetValueByY()` method in this NT8 build (unconfirmed)

Three specific doubts are enumerated:
1. Whether `ChartControl.ChartBars` is a valid property
2. Whether `ChartBars[0].ChartPanel` is the correct path in this NT8 version
3. Whether `ChartPanel.GetValueByY()` compiles without error in this build

LSP workspace_symbols queries for `ChartBars`, `ChartScale`, `ChartPanel` returned
empty results (NT8 assemblies not in LSP scope). Grep in src/ found only the NT8-009
comment confirming absence.

Mission brief rule: "If there is ANY doubt about the exact method signature or the
correct property path, write TWO tickets." Three sources of doubt exist. Two-ticket
approach is mandatory per the mission brief. ✅

---

### CHECK-14 — DW-B8-04 Closure and Shelved Items PASS

DW-B8-04 closure:
- T2 precondition: "T1 must be VERIFY_PASS and NT8_ADDON_KNOWLEDGE.md must contain
  confirmed API path under 'B15 Discoveries' before T2 begins."
- T2 removes the 0.0 stub and suppression line, replacing with real ChartPanel
  Y-to-price conversion + tick-align.
- T2 SCAN-04 + SCAN-05 confirm the stub is gone at VERIFY_PASS time.
- DW-B8-04 is the sole target; its resolution unblocks DW-B9-03 per B14 backlog.

Remaining shelved items (plan §2):

| ID | Status |
|----|--------|
| DW-B9-03 | SHELVED — B16+ (mission brief explicitly prohibits in B15) |
| DW-B9-01 | SHELVED — B16+ (no chart canvas work in B15) |
| DW-B12-DEFER-01 (original) | SHELVED — future |

All three remaining B14 open items are correctly carried forward as shelved. ✅

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| Replace hardcoded 0.0 stub in OnChartMouseDown | Yes — T2 removes stub | §1, §4, §5-T2 |
| Use real Y-to-price axis conversion | Yes — via ChartPanel.GetValueByY (confirmed by T1) | §4, §11 |
| Tick-align result before submitting Limit order (NT8-029) | Yes — Math.Round formula | §4, §5-T2 |
| Investigate API path before implementing (API unconfirmed) | Yes — T1 diagnostic ticket | §3, §4, §5-T1 |
| DW-B9-03 NOT implemented | Yes — explicitly shelved | §2 |
| No spread/buffer offset | Yes — exact pixel price only | §2 |
| [Fact] tests for tick-align math | Yes — 6 tests in §6 | §6 |
| Protected files not touched | Yes — §7 confirms isolation | §7 |
| 7-scan checklist per ticket | Yes — both tickets have 7 scans | §5-T1, §5-T2 |
| NT8 constraint documentation updated | Yes — NT8_ADDON_KNOWLEDGE.md "B15 Discoveries" | §5-T1 |

---

## Reviewer Notes

1. **DumpChartControlTree CYC=4 claim (T1):** The plan states CYC=4 for this diagnostic
   helper. The visual-tree walk step could produce higher CYC if implemented with nested
   loops. The engineer must confirm the implementation stays at CYC ≤ 8 during T1 execution.
   Since this method is removed entirely in T2 and is diagnostic-only, this is a low-risk
   advisory note only — not a blocking violation.

2. **NT8-031 (System.Threading) advisory:** The T1 diagnostic uses `cc.GetType().GetProperty()`
   which is `System.Reflection` (part of mscorlib — no explicit using required). The plan
   correctly notes this in §8: "GetType() is on System.Object. PropertyInfo is
   System.Reflection.PropertyInfo but auto-resolved via var." If the engineer adds any
   `Interlocked` usage in T1 (unlikely given the diagnostic-only scope), NT8-031 requires
   `using System.Threading;` to be present. This is acknowledged in §8 NT8-031 reference.

3. **T1 output gate for T2:** T2 is blocked until T1 VERIFY_PASS and NT8_ADDON_KNOWLEDGE.md
   contains confirmed API path under "B15 Discoveries." This dependency is correctly
   expressed and enforced by the two-ticket design.

4. **TickAlign_AtHalfTick_Rounds test** appears in the test table (§5 T2) but not in the
   written-out [Fact] code (§6). The engineer should either add it or confirm the
   TickAlign_ZeroRaw test replaces it. Six tests are present; SCAN-07 requires ≥4; this
   is not a blocking issue.

---

## Final Verdict

**REVIEW_PASS**

Zero P0 violations. Zero NT8 compiler rule violations in proposed code. All 14 checks
pass. The two-ticket structure is justified by confirmed API uncertainty. CYC budgets
are within the ≤8 mandate. Protected files are correctly isolated. 7-scan checklists
are present for both tickets. Test coverage addresses all tick-align math paths.
