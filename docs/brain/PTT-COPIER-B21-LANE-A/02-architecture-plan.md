# PTT-COPIER-B21-LANE-A — Architecture Plan
# Block:  PTT-COPIER-B21
# Lane:   A
# Defect: DW-ATR-DEFAULTS-01
# Status: REVIEW_PENDING
# Date:   2026-07-14

---

## §1  Defect Summary and Root Cause

### Defect ID
`DW-ATR-DEFAULTS-01` (P1)

### Symptom
`AtrSizingEngine` field initialisers do not match the values that `TradeCopierAddOn.StartAtrEngine`
passes immediately after construction. This creates a race window: if the `DispatcherTimer` poll tick
fires between `new AtrSizingEngine()` (line 199) and `engine.SetParameters(...)` (line 201), the
engine runs `CalcContracts` using the wrong defaults.

### Root Cause (Three Bugs)
| Bug | File | Symptom |
|-----|------|---------|
| (a) | `AtrSizingEngine.cs` line 45 | `_maxRiskDollars` initialised to `150.0`; live panel default is `200.0` |
| (b) | `AtrSizingEngine.cs` line 50 | `_atrFraction` initialised to `1.0`; live panel default is `0.75` |
| (c) | `TradeCopierAddOn.cs` line 201 | `SetParameters(150.0, ...)` — wrong dollar value AND missing `SetAtrFraction(0.75)` call |

### Race Window Analysis
`StartAtrEngine` runs on the WPF UI thread (dispatched via `Dispatcher.InvokeAsync` from `DoInject`).
The `DispatcherTimer` (created and started in the same call) fires `engine.ManualOnBarUpdate()` every
1 second also on the UI thread. Because both lines run sequentially on the UI thread there is no
true data race; however, the bug persists because `_maxRiskDollars = 150.0` and `_atrFraction = 1.0`
ARE the field-initialiser defaults until `SetParameters` / `SetAtrFraction` overwrite them.
If any caller reads `GetSuggestedQty()` between construction and configuration — or if the
engine is used as a standalone object in a test without explicit configuration — it will return
the wrong contract count.

---

## §2  Three Surgical Changes

### Change Inventory
| # | File | Line | Old | New |
|---|------|------|-----|-----|
| Bug (a) | `AtrSizingEngine.cs` | 45 | `private double _maxRiskDollars  = 150.0;` | `private double _maxRiskDollars  = 200.0;` |
| Bug (b) | `AtrSizingEngine.cs` | 50 | `private double _atrFraction = 1.0;` | `private double _atrFraction = 0.75;` |
| Bug (c) | `TradeCopierAddOn.cs` | 201 | `engine.SetParameters(150.0, pointValue);` | `engine.SetParameters(200.0, pointValue);` + insert `engine.SetAtrFraction(0.75);` on the very next line |

### Bug (a) Detail — AtrSizingEngine.cs line 45

```
// BEFORE (line 45)
private double _maxRiskDollars  = 150.0;

// AFTER
private double _maxRiskDollars  = 200.0;
```

No type change. Plain `double` field — no `volatile` keyword; `NT8-003` compliant. Single-writer
UI thread per existing design comment on line 44.

### Bug (b) Detail — AtrSizingEngine.cs line 50

```
// BEFORE (line 50)
private double _atrFraction = 1.0;

// AFTER
private double _atrFraction = 0.75;
```

No type change. Plain `double` field — no `volatile`; `NT8-003` compliant. Same single-writer
comment block on lines 48-50 unchanged.

### Bug (c) Detail — TradeCopierAddOn.cs StartAtrEngine() lines 201-202

The `StartAtrEngine` method (L195-225) currently reads:

```csharp
var engine = new AtrSizingEngine();
double pointValue = instr.MasterInstrument?.PointValue ?? 5.0;
engine.SetParameters(150.0, pointValue);   // L201 — BUG: 150.0 wrong
_atrEngines[chart] = engine;              // L202 (current)
```

After the fix:

```csharp
var engine = new AtrSizingEngine();
double pointValue = instr.MasterInstrument?.PointValue ?? 5.0;
engine.SetParameters(200.0, pointValue);   // L201 — 150.0 → 200.0
engine.SetAtrFraction(0.75);               // L202 — NEW: explicit default alignment
_atrEngines[chart] = engine;              // L203 (shifted by 1)
```

`SetAtrFraction` is already declared at `AtrSizingEngine.cs` line 121:
`internal void SetAtrFraction(double fraction)` — no new method required.

`CYC` of `StartAtrEngine` after change: still `3` (the two `return` guards on lines 197-198 and the
`if (_atrPollTimer == null)` guard on line ~209 are the only branches; the new straight-line call
adds no branch).

---

## §3  New [Fact] Specification

### Test Name
`CalcContracts_DefaultValues_Use200Risk_075Fraction`

### Intent
Verify that a freshly constructed `AtrSizingEngine()` (no explicit `SetParameters` or
`SetAtrFraction` call) holds the expected defaults (`_maxRiskDollars = 200.0`,
`_atrFraction = 0.75`) by confirming its default field values produce the same contract count
as an explicit `CalcContracts` call parameterised with those values.

### Red-Before / Green-After Verification
| State | `_atrFraction` | `_maxRiskDollars` | `CalcContracts(10.0 * fraction, maxRisk, 5.0)` | Explicit baseline `CalcContracts(7.5, 200.0, 5.0)` | Assert result |
|-------|-----------|------------|-----|------|------|
| Before fix | 1.0 | 150.0 | `CalcContracts(10.0, 150.0, 5.0)` = `floor(150/50)` = **3** | **5** | **FAIL** (3 ≠ 5) |
| After fix  | 0.75 | 200.0 | `CalcContracts(7.5, 200.0, 5.0)` = `floor(200/37.5)` = **5** | **5** | **PASS** (5 == 5) |

### Math Derivation
`CalcContracts(atrPoints, maxRisk, tickDollarValue)`:
- `riskPerContract = atrPoints * tickDollarValue`
- `contracts = floor(maxRisk / riskPerContract)`, clamped ≥ 1

With `atrPoints = 7.5`, `maxRisk = 200.0`, `tickDollarValue = 5.0`:
- `riskPerContract = 7.5 * 5.0 = 37.5`
- `contracts = floor(200.0 / 37.5) = floor(5.333…) = 5`

### Test Body Plan

```csharp
[Fact]
public void CalcContracts_DefaultValues_Use200Risk_075Fraction()
{
    // Arrange: construct engine with NO SetParameters or SetAtrFraction calls.
    var engine = new AtrSizingEngine();

    // Read the actual default field values via reflection.
    // NOTE: existing GetField() helper is scoped to typeof(CopyEngine) -- cannot reuse.
    // Use typeof(AtrSizingEngine).GetField(...) directly.
    double fraction = (double)typeof(AtrSizingEngine)
        .GetField("_atrFraction",    BindingFlags.NonPublic | BindingFlags.Instance)
        .GetValue(engine);
    double maxRisk = (double)typeof(AtrSizingEngine)
        .GetField("_maxRiskDollars", BindingFlags.NonPublic | BindingFlags.Instance)
        .GetValue(engine);

    // Act: call the pure static method with the engine's actual defaults.
    const double atrPoints  = 10.0;
    const double tickDollar = 5.0;
    int lhs = AtrSizingEngine.CalcContracts(atrPoints * fraction, maxRisk, tickDollar);

    // Baseline: explicit values that the spec mandates as the correct defaults.
    int rhs = AtrSizingEngine.CalcContracts(atrPoints * 0.75, 200.0, tickDollar);

    // Assert: defaults match spec → both sides compute 5.
    Assert.Equal(rhs, lhs);
}
```

### Key Notes
- Uses `new AtrSizingEngine()` (parameterless NT8-required ctor, line 35) — not
  `AtrSizingEngine(int testContracts)` (test seam ctor, line 28).
- Uses `typeof(AtrSizingEngine).GetField(...)` directly because the class-level `GetField()`
  helper (line 18-19 of `CopyEngineTests.cs`) is hardcoded to `typeof(CopyEngine)`.
- `AtrSizingEngine.CalcContracts` is `internal static` (line 151) — accessible from within
  the `PropTraderTools` namespace (same assembly compilation unit).
- `CYC = 1` (straight-line; no branches in test body).
- Final test count after lane: **121** (baseline 120 + 1).

---

## §4  Scan Checklist for T1 (7 Scans)

All scans apply to the write-set: `AtrSizingEngine.cs`, `TradeCopierAddOn.cs`,
`CopyEngineTests.cs`.

| # | Scan ID | Pattern | Expected Result |
|---|---------|---------|-----------------|
| 1 | SCAN-01 | `grep -n "lock(" AtrSizingEngine.cs TradeCopierAddOn.cs CopyEngineTests.cs` | **0 matches** — JS-021 compliant |
| 2 | SCAN-02 | `grep -n "async void " AtrSizingEngine.cs TradeCopierAddOn.cs CopyEngineTests.cs` | **0 matches** — JS-033 compliant |
| 3 | SCAN-03 | `grep -n "return null" AtrSizingEngine.cs CopyEngineTests.cs` | **0 matches** in these two files — JS-002 compliant (pre-existing `return null` in `TradeCopierAddOn.cs` visual-tree helpers is unchanged; no new `return null` introduced by this lane) |
| 4 | SCAN-04 | Manual CYC inspection of all modified methods | `StartAtrEngine` = **3** (guards on lines 197, 198 and timer-init guard; new SetAtrFraction call adds no branch). All other touched methods = CYC 1. All ≤ 8 ✓ |
| 5 | SCAN-05 | `grep -n "volatile double" AtrSizingEngine.cs` | **0 matches** — NT8-003 compliant. `_maxRiskDollars` (line 45) and `_atrFraction` (line 50) are plain `double`, not `volatile double` |
| 6 | SCAN-06 | `grep -n "CreateOrder" AtrSizingEngine.cs TradeCopierAddOn.cs` | **0 new CreateOrder calls** introduced by this lane. Any existing CreateOrder calls in AddOn already carry "PTT-" prefix per prior blocks |
| 7 | SCAN-07 | `grep -n "DateTime.Now" AtrSizingEngine.cs TradeCopierAddOn.cs CopyEngineTests.cs` | **0 matches** — no datetime changes in this lane |

All 7 scans expected green before ptt-engineer marks VERIFY_PASS.

---

## §5  Risk Assessment and CYC Impact

### Change Risk: LOW
- All three changes are surgical value replacements (two field-initialiser literal swaps + one
  method-call argument swap + one new straight-line call insertion).
- No new methods, classes, interfaces, events, or NT8 API calls introduced.
- `SetAtrFraction` already exists and is covered by prior-block tests.
- The only observable behavioral change: a `new AtrSizingEngine()` with no configuration now
  computes the correct `200.0 / 0.75` sizing by default — matching what `StartAtrEngine` has
  always set a fraction of a millisecond later.

### CYC Impact Table
| Method | File | CYC Before | CYC After | Delta |
|--------|------|-----------|----------|-------|
| `AtrSizingEngine` (ctor, line 35) | `AtrSizingEngine.cs` | 1 | 1 | 0 |
| `StartAtrEngine` | `TradeCopierAddOn.cs` | 3 | 3 | 0 |
| `CalcContracts_DefaultValues_Use200Risk_075Fraction` | `CopyEngineTests.cs` | — | 1 | +1 (new test) |

No existing method increases in complexity. Lane A adds one new [Fact] at CYC=1.

### Threading Safety
`_maxRiskDollars` and `_atrFraction` are both single-writer UI-thread fields (comment lines 44,
48-50). No threading model change required. The race window noted in §1 is structural (ctor
defaults vs configured values) not concurrency-driven; closing the value gap is sufficient.

### Backward Compatibility
`SetParameters` and `SetAtrFraction` calls in `StartAtrEngine` continue to overwrite the field
values after construction. The change is forward-compatible: callers that explicitly call
`SetParameters(200.0, ...)` continue to work identically. Only callers that read the engine
without configuration (unit tests, standalone construction) see the corrected defaults.

### NT8 Compiler Constraints Verified
| Rule | Check | Result |
|------|-------|--------|
| NT8-001 | `{ get; init; }` not used | PASS — plain field initialisers |
| NT8-002 | `abstract record` / `sealed record` not used | PASS — no records touched |
| NT8-003 | `volatile double` not present in changed fields | PASS — `double`, not `volatile double` |
| NT8-004 | `ImmutableDictionary` not used | PASS — not touched |
| NT8-007 | No new `CreateOrder` call added | PASS |

---

## §6  Architecture Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **Three surgical changes only** — no new methods, classes, or files. | The spec is a pure default-alignment fix. Any additional abstraction (e.g., a factory method or a "DefaultEngine" helper) would violate the engineering-discipline mandate (minimal change that solves the problem). |
| 2 | **Field initialisers, not constructor body.** | The values are set in field initialisers (`= 150.0`, `= 1.0`) rather than in the parameterless constructor body. Fixing at the initialiser site is the smallest, most obvious change. |
| 3 | **`SetAtrFraction(0.75)` is the second line after `SetParameters(200.0, ...)`.** | `SetAtrFraction` was introduced in B12 T3 specifically for this purpose. Using it maintains API separation: `SetParameters` controls `_maxRiskDollars` and `_tickDollarValue`; `SetAtrFraction` controls `_atrFraction`. Merging them into one call would require a new overload — unnecessary scope. |
| 4 | **Direct `typeof(AtrSizingEngine).GetField(...)` in the new test.** | The existing `GetField(string)` helper (line 18-19 of `CopyEngineTests.cs`) is hard-bound to `typeof(CopyEngine)`. Using it would silently test CopyEngine fields, not AtrSizingEngine fields. The direct approach is unambiguous and self-documenting. |
| 5 | **Test reads actual field values via reflection, then compares to explicit baseline.** | This design makes the test red-before-fix and green-after-fix: if either default is wrong, the computed `lhs` diverges from the explicit `rhs`. A test that hardcodes both sides to the same expression would always pass regardless of the fix. |
| 6 | **11 open deferred items carry forward unchanged.** | This lane closes only `DW-ATR-DEFAULTS-01`. None of the 11 items in the B20-LANE-A backlog are in scope for B21-LANE-A per director mandate. They are noted here for traceability. |

---

## Backlog Reference

11 open deferred items carry forward from `docs/brain/PTT-COPIER-B20-LANE-A/06-deferred-backlog.md`
(Section 4). This lane closes only `DW-ATR-DEFAULTS-01` and adds no new deferred items.

| ID | Description | Priority |
|----|-------------|----------|
| DW-B9-01 | ATR box visualization on chart canvas | P2 |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset | P3 |
| DW-B12-DEFER-01 | Full-panel mode expansion: Buy Ask / Sell Bid buttons | P2 |
| DW-B12-DEFER-02 | Auto-trail stop from BE CONNECTED level | P3 |
| DW-B12-DEFER-03 | Correct Math.Clamp ban comment attribution; add NT8-031 | P3 |
| DW-B12-DEFER-04 | Align CopyEngineTests.cs test names with ticket contract names | P3 |
| DW-B19L2-DEFER-01 | ExitBufferTicks value-object (JS-015) | P2 |
| DW-B19L2-DEFER-02 | Spread validation guard in GetAsk/GetBid | P2 |
| DW-B19L2-DEFER-03 | OnMarketData event hook to cache ask/bid in TradeCopierPanel | P2 |
| DW-B19L2-DEFER-04 | Telemetry: log anchor price at order placement | P3 |
| DW-B20-LANE-A-DEFER-01 | Lane C wiring: CopyEnabledChanged subscribers in Panel and Window | P2 |

---

## File Ownership Summary

| File | Path | Changes |
|------|------|---------|
| `AtrSizingEngine.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` | Bug (a) line 45: `150.0` → `200.0`; Bug (b) line 50: `1.0` → `0.75` |
| `TradeCopierAddOn.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` | Bug (c) line 201: arg `150.0` → `200.0`; insert `engine.SetAtrFraction(0.75);` on line 202 |
| `CopyEngineTests.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` | Add [Fact] `CalcContracts_DefaultValues_Use200Risk_075Fraction` (Lane A adds first, unique name) |

DO NOT TOUCH: `TradeCopierPanel.cs`, any `.md` docs files.

---

## PLAN_COMPLETE
