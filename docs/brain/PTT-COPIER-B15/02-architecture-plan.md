# PTT-COPIER-B15 Architecture Plan
# Phase 1 — ptt-architect output
# Status: REVIEW_PENDING
# Date: 2026-07-14
# Block: PTT-COPIER-B15
# Primary deferred item closed: DW-B8-04 (Y-to-price axis conversion in click trader)

---

## §1  Scope Summary

**One deferred item targeted: DW-B8-04**

Replace the hardcoded `double price = 0.0` stub in
[`TradeCopierPanel.cs`](../../../src/PropTraderTools/TradeCopierPanel.cs:1100) `OnChartMouseDown`
with the real Y-to-price axis conversion so the click trader places a Limit order at the
EXACT price corresponding to the pixel the user clicked.

After converting, tick-align the result:
```csharp
double tickSize = _instrument.MasterInstrument.TickSize;
price = Math.Round(price / tickSize) * tickSize;   // NT8-029 mandatory tick-align
```

**Current stub (TradeCopierPanel.cs ~lines 1097-1101):**
```csharp
// NT8 constraint: ChartControl.GetValueByY does not exist in this NT8 version.
// DW-B8-04 (click trader) deferred -- price lookup via visual tree / scale panel pending.
// Temporary: use 0.0 so file compiles; click-trader will not fire valid orders until fixed.
double price  = 0.0;
_ = e.GetPosition(chartControl); // suppress unused-variable warning
```

**Root problem (NT8-009):** `ChartControl.GetValueByY(double y)` does not exist in this NT8
build. The Y pixel from `e.GetPosition(chartControl).Y` must be converted via an alternative
API path through the NT8 chart scale panel visual tree.

---

## §2  Shelved Items (carry-forward, no action in B15)

| ID | Description | Status |
|----|-------------|--------|
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset. BLOCKED on DW-B8-04. SHELVED per mission brief. | SHELVED — B16+ |
| DW-B9-01 | ATR box visualization on chart canvas. | SHELVED — B16+ |
| DW-B12-DEFER-01 (original) | Buy Ask / Sell Bid full-panel mode expansion. | SHELVED — future |

DW-B9-03 is **explicitly shelved** even though DW-B8-04 is closing in this block. The mission
brief states: "DW-B9-03 (Bid+1/Ask-1 auto-offset) is SHELVED — do NOT implement." No spread
offset, no buffer. Exact pixel price (then tick-aligned) only.

---

## §3  API Investigation Decision: TWO-TICKET PLAN

### Decision: TWO TICKETS REQUIRED

**Justification and evidence chain:**

| Evidence | Source | Verdict |
|----------|--------|---------|
| `ChartControl.GetValueByY()` does not exist in this NT8 build | NT8-009 (confirmed B8) | CONFIRMED ABSENT |
| `ChartPanel.GetValueByY(double y)` exists in NT8 public API documentation | Mission brief §4, NT8_ADDON_KNOWLEDGE.md B14 | PLAUSIBLE but UNCONFIRMED in this build |
| Path `chartControl.ChartBars[0].ChartPanel` to reach ChartPanel | Mission brief §4 | NOT confirmed in any B1-B14 block |
| `ChartBars` as a property on `ChartControl` | NT8_ADDON_KNOWLEDGE.md B14 | Candidate — "may expose" (explicit uncertainty) |
| LSP workspace_symbols queries for `ChartBars`, `ChartScale`, `ChartPanel` | Thought 7 LSP queries | Empty results — NT8 assemblies not in LSP scope |
| `GetValueByY` anywhere in src/ | grep across PropTraderTools/ | One hit — only the NT8-009 comment saying it does NOT exist |

**Mission brief rule applied verbatim:** "If there is ANY doubt about the exact method
signature or the correct property path, write TWO tickets."

There is doubt about:
1. Whether `ChartControl.ChartBars` is a valid property (vs. visual-tree-only child)
2. Whether `ChartBars[0].ChartPanel` is the correct path in this NT8 version
3. Whether `ChartPanel.GetValueByY()` compiles without error in this NT8 build

**→ TWO TICKETS. T1 = investigation. T2 = implementation with confirmed API.**

---

## §4  Proposed Solution

### T1: Visual Tree Diagnostic

Inject a one-shot diagnostic helper `DumpChartControlTree(ChartControl cc)` called from
`SetChart(Chart chart)` (already called on the UI thread by TradeCopierAddOn when a chart
attaches). The diagnostic:

1. Uses `VisualTreeHelper` (confirmed WPF API) to recursively enumerate all children of
   `ChartControl`, printing their `GetType().FullName` to `_statusText`.
2. Additionally tries reflection: `cc.GetType().GetProperty("ChartBars")` — if non-null,
   reads the value, iterates to find ChartPanel, calls `GetMethod("GetValueByY")` to
   confirm the signature.
3. Output written to `_statusText` (the TextBlock already in the panel UI).

The engineer runs F5 on Sim101 with a chart open, reads the statusText, records the full
type path under "B15 Discoveries" in `NT8_ADDON_KNOWLEDGE.md`.

**T1 also gates on the failure path:** If `ChartBars` does not exist as a property AND
no visual child is a `ChartPanel` type, the fallback is the **reflection-enumerated
child approach** (documented in T1 output). If even that fails, T2 will adopt a
`MarketData.Last.Price` + click-Y proximity approach instead.

### T2: Implementation with Confirmed API

Replace stub with:
```csharp
// B15 T2 -- DW-B8-04: real Y-to-price conversion via confirmed NT8 ChartScale API.
// Confirmed property path: <populated by T1 engineer from B15 Discoveries>
// NT8-009: ChartControl.GetValueByY absent -- use ChartBars[0].ChartPanel.GetValueByY() instead.
// NT8-029: tick-align result before submitting Limit order.
Point  mousePos = e.GetPosition(chartControl);
double rawPrice = GetPriceAtY(chartControl, mousePos.Y);   // new private helper
if (rawPrice <= 0.0) return;   // guard: price not available yet (new guard 5)
double tickSize = _instrument.MasterInstrument.TickSize;
double price    = Math.Round(rawPrice / tickSize) * tickSize;   // NT8-029 tick-align
```

The `_ = e.GetPosition(chartControl)` suppression line is **removed** (it only existed to
silence the unused-variable warning from the disabled lookup).

### Private helper `GetPriceAtY`:
```csharp
// CYC=4: (1) ChartBars null guard, (2) count==0 guard,
//        (3) ChartPanel null guard, (4) return value
// NT8 API: ChartBars[0].ChartPanel.GetValueByY(y) -- confirmed by T1.
private static double GetPriceAtY(ChartControl cc, double y)
{
    // Property path confirmed by T1 diagnostic (see NT8_ADDON_KNOWLEDGE.md §B15)
    var bars = cc.ChartBars;          // ChartBarsCollection (confirmed by T1)
    if (bars == null || bars.Count == 0) return 0.0;    // guard (1+2)
    var panel = bars[0].ChartPanel;   // ChartPanel (confirmed by T1)
    if (panel == null) return 0.0;    // guard (3)
    return panel.GetValueByY(y);      // double (4)
}
```

**Note:** The exact type name for `ChartBars` (`ChartBarsCollection` or similar) is NOT
hardcoded above — the `var` keyword will accept whatever NT8 resolves at compile time.
The engineer MUST substitute the confirmed type path from T1 output before writing T2 code.

---

## §5  Per-Ticket Design

---

### T1 — Investigation: Confirm ChartControl → ChartPanel → GetValueByY API Path

**File:** `src/PropTraderTools/TradeCopierPanel.cs`

**Purpose:** Add one-shot diagnostic to find confirmed Y-to-price API. Do NOT fix the 0.0
stub in this ticket. The stub stays. The `_ = e.GetPosition(chartControl)` suppression stays.

**New code in T1:**

```
Field:  private volatile bool _chartDiagDone = false;   // one-shot guard (JS-023 cross-thread)
Method: private void DumpChartControlTree(ChartControl cc)  // CYC=4
        Called from: SetChart(Chart chart) -- already UI thread, _currentChart assignment
```

`DumpChartControlTree` algorithm (CYC=4):
```
(1) null guard: if (cc == null) return;
(2) Reflection probe: PropertyInfo barsInfo = cc.GetType().GetProperty("ChartBars");
    if (barsInfo != null): read value, drill to [0].ChartPanel, try GetValueByY reflection
(3) Visual tree walk (depth ≤ 5): VisualTreeHelper depth-first, collect all child type names
(4) Assemble diagnostic string; write to _statusText via Dispatcher.InvokeAsync
```

**T1 output gates T2**: Engineer records:
- `ChartBars` property: EXISTS / DOES NOT EXIST
- If exists: exact C# type name, `[0]` indexer result type, `ChartPanel` property type
- `GetValueByY(double y)` method: EXISTS / DOES NOT EXIST on ChartPanel
- Visual tree child type at scale-panel position

**T1 also updates:**
- `docs/standards/NT8_ADDON_KNOWLEDGE.md` — new section "## B15 Discoveries" with confirmed API path
- `docs/standards/NT8_COMPILER_RULES.md` — append new rule NT8-036 if a new compiler error is discovered

**What T1 does NOT do:**
- Does NOT change OnChartMouseDown
- Does NOT remove the 0.0 stub
- Does NOT add [Fact] tests

**SCAN checklist (T1):**
- SCAN-01: `lock\s*\(` in modified code → 0 results required
- SCAN-02: `async\s+void\s+\w+\(` in modified code → 0 results required
- SCAN-03: `\.GetValueByY\(` on ChartControl directly → 0 results required (NT8-009)
- SCAN-04: `volatile` on `_chartDiagDone` field → must be present (JS-023)
- SCAN-05: `DumpChartControlTree` called from `SetChart` only → verify single call site
- SCAN-06: `_statusText.Text` update via `Dispatcher.InvokeAsync` → verify thread-safe UI update
- SCAN-07: File header comment added for B15 T1 changes

---

### T2 — Implementation: Replace 0.0 Stub with Confirmed API + Tick-Align + Tests

**Files:** `TradeCopierPanel.cs` + `CopyEngineTests.cs`

**Precondition:** T1 must be `VERIFY_PASS` and NT8_ADDON_KNOWLEDGE.md must contain confirmed
API path under "B15 Discoveries" before T2 begins.

**TradeCopierPanel.cs changes:**

```
Remove:  volatile bool _chartDiagDone (T1 diagnostic field)
Remove:  DumpChartControlTree(ChartControl cc) method (entire method)
Remove:  SetChart call to DumpChartControlTree
Add:     GetPriceAtY(ChartControl cc, double y) private static method (CYC=4)
Modify:  OnChartMouseDown -- replace 3 stub lines with confirmed price lookup + tick-align
```

`OnChartMouseDown` after T2 (lines replacing the stub block):
```csharp
// B15 T2 -- DW-B8-04: real Y-to-price conversion (NT8-009 resolved via ChartPanel.GetValueByY).
// NT8-029: tick-align before submitting Limit order.
// Remove suppression line -- e.GetPosition is now actively used.
Point  mousePos = e.GetPosition(chartControl);
double rawPrice = GetPriceAtY(chartControl, mousePos.Y);
if (rawPrice <= 0.0) return;                                        // guard (5)
double tickSize = _instrument.MasterInstrument.TickSize;
double price    = Math.Round(rawPrice / tickSize) * tickSize;
```

Full method CYC after T2:
```
guard (1): if (!_clickArmed) return;
guard (2): if (_leaderAccount == null) return;
guard (3): if (_instrument == null) return;
guard (4): if (chartControl == null) return;
guard (5): if (rawPrice <= 0.0) return;
ternary:   isBuy ? OrderAction.Buy : OrderAction.SellShort
```
CYC = 6. Within budget (≤8). ✅

`GetPriceAtY` CYC budget:
```
guard (1): bars == null
guard (2): bars.Count == 0
guard (3): panel == null
return (4): panel.GetValueByY(y)
```
CYC = 4. ✅

**CopyEngineTests.cs additions:**

New [Fact] tests — all pure math; no NT8 runtime required:

| Test Name | Input | Expected | What It Verifies |
|-----------|-------|----------|-----------------|
| `TickAlign_ExactBoundary_ReturnsUnchanged` | raw=4250.00, tick=0.25 | 4250.00 | No change when already aligned |
| `TickAlign_AboveHalfTick_RoundsUp` | raw=4250.13, tick=0.25 | 4250.25 | Mid+1 rounds up |
| `TickAlign_BelowHalfTick_RoundsDown` | raw=4250.12, tick=0.25 | 4250.00 | Mid-1 rounds down |
| `TickAlign_AtHalfTick_Rounds` | raw=4250.125, tick=0.25 | result is 4250.00 or 4250.25 (banker's round) | Documents rounding mode |
| `TickAlign_SmallTickSize_6E` | raw=1.08753, tick=0.00005 | 1.08755 | Forex precision tick |
| `TickAlign_NegativePrice_Aligns` | raw=-50.13, tick=0.25 | -50.00 or -50.25 | Below-zero price (short) |
| `GetPriceAtY_NullBars_ReturnsZero` | ChartControl with null ChartBars | 0.0 | Null guard path |
| `GetPriceAtY_EmptyBars_ReturnsZero` | ChartControl with 0 ChartBars | 0.0 | Empty collection guard |

Note: Tests 7-8 require a `ChartControl` stub/mock or inline fake. Since xUnit in the
Linting.csproj has access to NinjaTrader types only via reference (not runtime), these tests
will test the helper method's guard logic via a direct helper call with a test double.

If the `ChartBars` property is not accessible without an NT8 runtime (cannot instantiate
`ChartControl` in unit test context), tests 7-8 are written as **integration notes** in
NT8_ADDON_KNOWLEDGE.md instead of [Fact] tests. The tick-align tests (1-6) are always pure
math and require no NT8 runtime — they are always [Fact] tests.

**SCAN checklist (T2):**
- SCAN-01: `lock\s*\(` in modified code → 0 results required
- SCAN-02: `async\s+void\s+\w+\(` in modified code → 0 results required
- SCAN-03: `ChartControl.*GetValueByY\(` → 0 results (NT8-009 — must use ChartPanel path only)
- SCAN-04: `price\s*=\s*0\.0` in OnChartMouseDown → 0 results (NT8-035 — stub must be gone)
- SCAN-05: `_ = e.GetPosition` suppression line → 0 results (must be replaced by active usage)
- SCAN-06: `Math.Round.*tickSize.*tickSize` tick-align present → 1 result required
- SCAN-07: [Fact] tests for tick-align present in CopyEngineTests.cs → ≥ 4 tests

---

## §6  xUnit Test Design

All tests live in [`CopyEngineTests.cs`](../../../src/PropTraderTools/CopyEngineTests.cs).
No new test file. Append at end of existing test class.

### Tick-align helper (shared by all math tests):
```csharp
// Pure math helper — no NT8 runtime dependency. Static, testable in isolation.
private static double TickAlign(double raw, double tickSize)
    => Math.Round(raw / tickSize) * tickSize;
```

### Test signatures (T2):

```csharp
[Fact] public void TickAlign_ExactBoundary_ReturnsUnchanged()
{
    Assert.Equal(4250.00, TickAlign(4250.00, 0.25), precision: 8);
}

[Fact] public void TickAlign_AboveHalfTick_RoundsUp()
{
    Assert.Equal(4250.25, TickAlign(4250.13, 0.25), precision: 8);
}

[Fact] public void TickAlign_BelowHalfTick_RoundsDown()
{
    Assert.Equal(4250.00, TickAlign(4250.12, 0.25), precision: 8);
}

[Fact] public void TickAlign_SmallTickSize_6E()
{
    // 6E tick = 0.00005; raw 1.08753 rounds to 1.08755
    Assert.Equal(1.08755, TickAlign(1.08753, 0.00005), precision: 8);
}

[Fact] public void TickAlign_NegativePrice_Aligns()
{
    // Short sale scenario: -50.13 rounds to -50.00 (nearest 0.25)
    double result = TickAlign(-50.13, 0.25);
    Assert.True(result == -50.00 || result == -50.25,
        $"Expected -50.00 or -50.25 but got {result}");
}

[Fact] public void TickAlign_ZeroRaw_ReturnsZero()
{
    Assert.Equal(0.0, TickAlign(0.0, 0.25), precision: 8);
}
```

Note on `Assert.Equal` with doubles: use the overload with `precision:` parameter (number of
decimal places to compare) rather than comparing raw doubles. xUnit's `Assert.Equal(double,
double, int)` overload performs rounded comparison to the specified precision.

---

## §7  Files Touched vs Files Protected

### Files touched in B15:

| File | Ticket | Change |
|------|--------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` | T1 + T2 | T1: add DumpChartControlTree + _chartDiagDone field. T2: remove diagnostic, add GetPriceAtY, modify OnChartMouseDown (remove stub, add real lookup + tick-align) |
| `src/PropTraderTools/CopyEngineTests.cs` | T2 | Add 6+ [Fact] tests for tick-align logic |
| `docs/standards/NT8_ADDON_KNOWLEDGE.md` | T1 output | Record confirmed ChartScale API path under "## B15 Discoveries" |
| `docs/standards/NT8_COMPILER_RULES.md` | T2 (conditional) | Add NT8-036 if a new compiler error is discovered during T1 F5 run |

### Files protected (MUST NOT touch):

| File | Reason |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | No CopyEngine changes required for price lookup |
| `src/PropTraderTools/TradeCopierAddOn.cs` | No AddOn changes required for price lookup |
| `src/PropTraderTools/TradeCopierWindow.cs` | No Window changes required |
| `src/PropTraderTools/AtrSizingEngine.cs` | No ATR engine changes required |

---

## §8  NT8 Constraints Referenced

| Rule | Applies to | Impact |
|------|------------|--------|
| NT8-009 | T1, T2 | `ChartControl.GetValueByY()` absent — use ChartPanel path instead |
| NT8-029 | T2 | Tick alignment mandatory on all limit prices — formula: `Math.Round(raw/tick)*tick` |
| NT8-035 | T2 | Hardcoded 0.0 in CreateOrder is a production bug — must be replaced |
| NT8-014 | T2 (unchanged) | Signal name "PTT-Click" starts with "PTT-" — already correct |
| NT8-013 | T2 (unchanged) | DateTime.MaxValue for CreateOrder GTC — already correct |
| NT8-008 | T1 | `Chart.ChartControl` property does not exist — use FindVisualChild<ChartControl> (already done in AddOn; panel receives ChartControl directly via OnChartMouseDown sender) |
| NT8-019 | T1, T2 | No `async void` in any new method |
| NT8-031 | T1 (reflection) | `using System.Reflection` required if GetProperty() used — add to file if not present |
| NT8-034 | T1, T2 | No `Math.Clamp` — use `Math.Max(Math.Min(...))` where needed |

**Note on NT8-031**: The diagnostic `cc.GetType().GetProperty("ChartBars")` uses `System.Reflection`
which is part of `mscorlib` (always available in NT8's .NET 4.8). No explicit using directive is
required. `GetType()` is on `System.Object`. `PropertyInfo` is `System.Reflection.PropertyInfo`
but auto-resolved via `var`. The engineer must verify at F5 time whether any explicit using is needed.

---

## §9  Rules Catalog Gate Result

**Gate check performed against `docs/standards/jane-street/RULES_CATALOG.md`:**

Checked all P0 rules for code patterns introduced in this plan:

| Rule | Check | Result |
|------|-------|--------|
| JS-021 `lock()` | No lock() in any new method | PASS |
| JS-033 `async void` | No async void in DumpChartControlTree or GetPriceAtY | PASS |
| JS-001 throw in hot path | No new throw; existing try/catch wrapper retained | PASS |
| JS-002 return null | No new return null; helper returns 0.0 (double) on failure | PASS |
| JS-010 public constructor | No new classes | PASS |
| JS-015 unvalidated string | No string crossing boundary | PASS |
| JS-036 new byte[] in hot path | No new buffers | PASS |
| JS-037 new T[] in hot path | No new arrays | PASS |

P0 verification commands to run before T2 commit:
```powershell
# Lock check -- must return 0 results
grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs

# Async void check
grep -n "async void " src/PropTraderTools/TradeCopierPanel.cs

# Return null check
grep -n "return null;" src/PropTraderTools/TradeCopierPanel.cs

# Hardcoded 0.0 price stub check -- must return 0 results after T2
grep -n "price\s*=\s*0\.0" src/PropTraderTools/TradeCopierPanel.cs

# GetValueByY on ChartControl directly -- must return 0 results
grep -n "chartControl\.GetValueByY\|ChartControl.*GetValueByY" src/PropTraderTools/TradeCopierPanel.cs
```

**RULES CATALOG GATE RESULT: PASS**

No P0 violations in the proposed plan. No P1 violations introduced by new methods.

---

## §10  Component List

| Component | Type | File | New/Modified |
|-----------|------|------|-------------|
| `DumpChartControlTree(ChartControl cc)` | private void method | TradeCopierPanel.cs | New (T1) |
| `_chartDiagDone` | private volatile bool field | TradeCopierPanel.cs | New (T1) |
| `GetPriceAtY(ChartControl cc, double y)` | private static double method | TradeCopierPanel.cs | New (T2) |
| `OnChartMouseDown` stub block (3 lines) | lines ~1097-1101 | TradeCopierPanel.cs | Modified (T2) |
| `TickAlign_*` [Fact] tests (6 tests) | test methods | CopyEngineTests.cs | New (T2) |
| "B15 Discoveries" section | markdown section | NT8_ADDON_KNOWLEDGE.md | New (T1 output) |

---

## §11  Data Flow

```
[User clicks chart pixel (x, y)]
    |
    v
OnChartMouseDown(sender=ChartControl, e=MouseButtonEventArgs)  [UI thread]
    |
    +-- guard (1): _clickArmed == false → return
    +-- guard (2): _leaderAccount == null → return
    +-- guard (3): _instrument == null → return
    +-- guard (4): sender as ChartControl == null → return
    |
    v
Point mousePos = e.GetPosition(chartControl)   [mousePos.Y = pixel Y]
    |
    v
GetPriceAtY(chartControl, mousePos.Y)
    |
    +-- ChartBars == null || Count == 0 → return 0.0
    +-- ChartBars[0].ChartPanel == null → return 0.0
    +-- return ChartBars[0].ChartPanel.GetValueByY(mousePos.Y)  → rawPrice
    |
    v
guard (5): rawPrice <= 0.0 → return   [price not available: chart not ready]
    |
    v
double tickSize = _instrument.MasterInstrument.TickSize
double price = Math.Round(rawPrice / tickSize) * tickSize   [NT8-029 tick-align]
    |
    v
bool isBuy = _clickBuy (volatile read)
OrderAction action = isBuy ? Buy : SellShort
int qty = CopyEngine.Instance.GetSuggestedQty(_instrument)
    |
    v
_leaderAccount.CreateOrder(
    _instrument, action, OrderType.Limit, OrderEntry.Manual,
    TimeInForce.Day, qty, price, 0, null,
    "PTT-Click",          // NT8-014: "PTT-" prefix
    DateTime.MaxValue,    // NT8-013: GTC
    null)                 // NT8-007: CustomOrder null cast
```

---

## §12  Pre-Flight Summary (Thought 10 Verification)

All 10 thoughts reached `nextThoughtNeeded=false` with zero violations:

| Check | Status |
|-------|--------|
| Spec feature inventory — all requirements mapped | PASS |
| JS P0 rule pre-check — no lock, no async void, no throw, no null return | PASS |
| CYC pre-check — all methods ≤ 8 | PASS (max CYC=6 in OnChartMouseDown) |
| Threading model — UI thread only, no new volatile fields needed in T2 | PASS |
| Data flow — complete from click to CreateOrder verified | PASS |
| File split — no cross-contamination of protected files | PASS |
| NT8 API surface — LSP queries returned no results; two-ticket mandated | PASS (two-ticket) |
| NT8 constraints referenced — NT8-009, NT8-029, NT8-035 all addressed | PASS |
| Rules Catalog Gate — all P0 checks PASS | PASS |

---

## Return: PLAN_COMPLETE
