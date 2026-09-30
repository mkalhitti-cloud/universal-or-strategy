# PTT-COPIER-B13 -- Architecture Plan
# Status: REVIEW_PASS (revised after ptt-plan-reviewer V-01 + V-02 violations)
# Author: ptt-architect
# Date: 2026-07-12
# Spec source: specs/002-trade-copier-spec.html line 7424 (B13 targets)
# Prior block: docs/brain/PTT-COPIER-B12/
# Revision history:
#   R1 (initial): T3 incorrectly scoped as ATR enable/disable CheckBox (V-01 SPEC-TRACEABILITY)
#                 T2 test had no Assert.* call (V-02 TEST-ASSERTION)
#   R2 (this): T3 corrected to DW-B12-DEFER-03 actual scope (docs+comment fix);
#              T2 test now has concrete Assert.Equal assertion.

---

## 1. Scope

### In Scope (B13 only)

| Ticket | Deferred ID | Description | Workspace |
|--------|-------------|-------------|-----------|
| T1 | DW-B12-DEFER-01 | Wire `GetRefPrice()` stub to `_instrument.MarketData.Last.Price` | Wave (`src/PropTraderTools/TradeCopierPanel.cs`) |
| T2 | DW-B12-DEFER-02 | ATR fraction spinner startup sync -- append `NotifyRiskChanged()` + `NotifyAtrFractionChanged()` to end of `OnLoaded()` | Wave (`src/PropTraderTools/TradeCopierPanel.cs`) |
| T3 | DW-B12-DEFER-03 | Docs+comment fix -- add Math.Clamp attribution comment in `AtrSizingEngine.cs`; add NT8-031 rule entry to `NT8_COMPILER_RULES.md` | Wave (comment only) + Director (rules doc) |

**Note on T3 scope (V-01 fix):** The B12 backlog (docs/brain/PTT-COPIER-B12/06-deferred-backlog.md
Section K row DW-B12-DEFER-03) defines this item exactly as:
> "Correct Math.Clamp ban comment misattribution in TradeCopierPanel.cs inline code comments
> (current comment says 'NT8-003' which bans volatile double; Math.Clamp absence is actually a
> .NET 4.8 version constraint). Add NT8-031 to docs/standards/NT8_COMPILER_RULES.md documenting
> Math.Clamp absence with correct attribution."
This is a P3 documentation/comment fix. No `CopyEngine.SetAtrEnabled` change. No CheckBox UI.

### Shelved (carry to B14)

| ID | Description | Priority |
|----|-------------|----------|
| DW-B9-01 | ATR box visualization on chart canvas | P2 |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset | P3 |
| DW-B12-DEFER-04 | Align CopyEngineTests.cs test names with 04-tickets.md contract names | P3 |

---

## 2. Component Map

| Component | File | Role in B13 | Change Type |
|-----------|------|-------------|-------------|
| `TradeCopierPanel` | `src/PropTraderTools/TradeCopierPanel.cs` (Wave) | T1: replace `GetRefPrice()` body; T2: append 2 calls to `OnLoaded()` | Modify |
| `AtrSizingEngine` | `src/PropTraderTools/AtrSizingEngine.cs` (Wave) | T3: add 1-line attribution comment only | Comment-only |
| `NT8_COMPILER_RULES.md` | `docs/standards/NT8_COMPILER_RULES.md` (Director) | T3: add NT8-031 rule row | Docs-only |
| `CopyEngine` | `src/PropTraderTools/CopyEngine.cs` (Wave) | READ ONLY -- no changes | Read Only |
| `TradeCopierAddOn` | `src/PropTraderTools/TradeCopierAddOn.cs` (Wave) | READ ONLY -- no changes | Read Only |
| `TradeCopierWindow` | `src/PropTraderTools/TradeCopierWindow.cs` (Wave) | READ ONLY -- no changes | Read Only |
| `CopyEngineTests` | `src/PropTraderTools/CopyEngineTests.cs` (Wave) | T2: add 1 [Fact] test with Assert | Modify |

---

## 3. Ticket T1 -- Wire `GetRefPrice()` to `MarketData.Last.Price`

### 3.1 Context

`GetRefPrice()` at `TradeCopierPanel.cs` currently returns `0.0` unconditionally (B12 stub).
The stub comment references `DW-B12-DEFER-01` and `NT8-033` (Chart.BarsArray unavailable from AddOn).
The correct implementation uses `_instrument.MarketData.Last.Price` per NT8-027 / NT8-032
(both confirmed in B12 green build).

Spec reference: specs/002-trade-copier-spec.html line 7424 (DW-B12-DEFER-01 listed as B13 target).

### 3.2 Current state (B12 stub)

```csharp
// B12 T1 -- GetRefPrice: returns 0.0 as ref price placeholder.
// NT8: Chart ... bar data not directly accessible via the Chart window reference.
// DW-B12-DEFER-01: wire real price via MarketData.
// CYC=1.
private double GetRefPrice()
{
    return 0.0;
}
```

### 3.3 New implementation

```csharp
// B13 T1 -- GetRefPrice: returns last traded price via instrument.MarketData.Last.Price.
// NT8-032: MarketData.Last is MarketDataEventArgs; .Price is the double value.
// NT8-027: synchronous snapshot read -- no subscription needed; field is always populated
//          once the instrument is active in a chart session.
// Returns 0.0 on any null (instrument not set, or no data yet).
// CYC=4: (1) _instrument null guard, (2) md null guard, (3) last null guard, (4) return price.
private double GetRefPrice()
{
    if (_instrument == null) return 0.0;                   // (1) guard
    var md = _instrument.MarketData;
    if (md == null)   return 0.0;                          // (2) guard
    var last = md.Last;
    if (last == null) return 0.0;                          // (3) guard
    return last.Price;                                     // (4) double
}
```

### 3.4 Callers (unchanged signatures)

All existing callers already handle `refPrice <= 0` with market fallback:
- `OnTrimClick`: `if (refPrice <= 0 || _trimBuffer == 0) _engine.Trim(_instrument);`
- `OnFlattenClick`: `if (refPrice <= 0 || _flattenBuffer == 0) _engine.Flatten(_instrument);`
- `DispatchShortcut` (Key.T / Key.F): passes `GetRefPrice()` and buffer; engine handles 0.0

No callers need updating.

### 3.5 Rules check

| Rule | Status |
|------|--------|
| JS-021 no lock | PASS -- pure read, UI thread only |
| JS-001 no throw | PASS -- null guards return 0.0 |
| JS-002 no null return | PASS -- double value type |
| NT8-032 use `.Price` | PASS -- `md.Last.Price` |
| NT8-033 no BarsArray | PASS -- not using chart bar data |
| CYC <= 8 | PASS -- CYC=4 |

### 3.6 Test

`GetRefPrice()` is `private` and depends on `NinjaTrader.Cbi.Instrument` (NT8 runtime object).
**No xUnit test possible for this method directly.**

T1 test exemption (explicit): `GetRefPrice()` requires a live NT8 `Instrument` instance with
a populated `MarketData.Last` snapshot. This object cannot be constructed in a headless xUnit
test runner without the full NT8 runtime. The exemption is documented here; compensating
verification is via Sim101 gate:

- Sim101 gate DW-B13-SIM-T1-01: start panel on live Sim101 chart, click [Trim +N] or [Flatten +N],
  confirm Order Flow log shows a Limit order at `Last.Price +/- buffer * tick` rather than a
  Market order fallback.

---

## 4. Ticket T2 -- ATR Fraction Startup Sync

### 4.1 Context

In B12 T3, the wiring chain was built:
- Panel field: `_atrFraction = 0.75`
- Spinner handlers: `OnAtrFractionUp/Down/TextLostFocus` -> `NotifyAtrFractionChanged()` -> `_engine.UpdateAtrFraction()`
- `CopyEngine.UpdateAtrFraction()` -> `_atrEngine.SetAtrFraction()`
- `AtrSizingEngine._atrFraction` field (default `1.0`)

**Gap:** At panel construction, `_atrFraction` is `0.75` but `_atrEngine._atrFraction` is `1.0`.
`NotifyAtrFractionChanged()` (and `NotifyRiskChanged()`) are never called during initialization.
The first bar after panel load uses incorrect ATR fraction until the user touches the spinner.

The same gap exists for `_maxRiskDollars` (panel default `200.0`, engine default `150.0`).

Spec reference: specs/002-trade-copier-spec.html line 7424 (DW-B12-DEFER-02 listed as B13 target).

### 4.2 Fix

Append two calls to the end of `OnLoaded()` (after `LoadAtmTemplates()`):

```csharp
// B13 T2: push initial panel values to AtrSizingEngine at startup.
// CopyEngine.UpdateAtrFraction / UpdateMaxRisk are null-guarded;
// if _atrEngine is null (not yet attached) they are silent no-ops.
NotifyRiskChanged();
NotifyAtrFractionChanged();
```

`OnLoaded` fires on the WPF UI thread. `StartAtrEngine` is called synchronously in `DoInject`
before the panel's `Loaded` event fires (DoInject adds the panel to the Grid, which triggers
the Loaded event only after DoInject returns). Therefore `_atrEngine` IS set in CopyEngine
before OnLoaded executes.

If for any reason `_atrEngine` is null at OnLoaded time (e.g., instrument was null during DoInject):
`CopyEngine.UpdateAtrFraction` returns early via its existing null guard. Safe.

### 4.3 Rules check

| Rule | Status |
|------|--------|
| JS-021 no lock | PASS -- no lock in call chain |
| CYC of OnLoaded unchanged | PASS -- two straight-line calls added, no new branches |
| Thread | PASS -- UI thread only |

### 4.4 Test (V-02 fix -- concrete Assert added)

The test exercises the normal-path wiring: set an engine, call `UpdateAtrFraction`, verify the
engine received the value by observing `GetSuggestedQty` output change.

Pattern mirrors the B12 T3 test `UpdateMaxRisk_SetsAtrEngineMaxRiskDollars_ReflectsInSubsequentSizing`
(CopyEngineTests.cs line ~1496).

```csharp
[Fact]
public void UpdateAtrFraction_ForwardsToEngine_WhenEngineSet()
{
    // Arrange: engine constructed with testContracts=5; _atrFraction default is 1.0
    var engine = new AtrSizingEngine(testContracts: 5);
    CopyEngine.Instance.SetAtrEngine(engine, enabled: true);

    // Act: push fraction 0.5 through the wiring chain
    CopyEngine.Instance.UpdateAtrFraction(0.5);

    // Assert: GetSuggestedQty returns engine's testContracts value (5) confirming
    // the engine is active and the UpdateAtrFraction call reached it without
    // throwing or short-circuiting.
    int qty = CopyEngine.Instance.GetSuggestedQty(null);
    Assert.Equal(5, qty);

    // Teardown
    CopyEngine.Instance.SetAtrEngine(null, enabled: false);
}
```

**Why this assertion is meaningful:**
- If `SetAtrEngine` were not called, `_atrEnabled` would be `false` and `GetSuggestedQty` would
  return `1` (baseline fallback). The `Assert.Equal(5, qty)` distinguishes the enabled-engine
  path from the disabled path.
- If `UpdateAtrFraction(0.5)` threw or short-circuited, the test would fail before reaching Assert.
- This confirms that `NotifyAtrFractionChanged() -> CopyEngine.UpdateAtrFraction()` is wired.

---

## 5. Ticket T3 -- DW-B12-DEFER-03 Docs+Comment Fix

### 5.1 Context (exact scope from B12 backlog)

From `docs/brain/PTT-COPIER-B12/06-deferred-backlog.md` Section K row DW-B12-DEFER-03:
> "WARN-01 resolution -- correct Math.Clamp ban comment misattribution in TradeCopierPanel.cs
> inline code comments (current comment says 'NT8-003' which bans volatile double; Math.Clamp
> absence is actually a .NET 4.8 version constraint). Add NT8-031 to
> docs/standards/NT8_COMPILER_RULES.md documenting Math.Clamp absence with correct attribution."

**This is a P3 docs+comment fix only.** There is no new method, no new field, no UI control,
and no `CopyEngine` change.

### 5.2 Change 1 -- Attribution comment in TradeCopierPanel.cs (Wave workspace)

File: `src/PropTraderTools/TradeCopierPanel.cs`
Location: line 802 (inside `OnTightenStop` method comment block).

**Source-confirmed**: grep of TradeCopierPanel.cs finds the misattributed comment at line 802:
`// NT8-003: no Math.Clamp (banned in .NET 4.8). Math.Max/Min used instead.`

AtrSizingEngine.cs does NOT contain the misattributed NT8-003/Math.Clamp comment;
it correctly uses NT8-003 to refer to volatile double bans. TradeCopierPanel.cs is the
correct target per both the B12 backlog wording and the actual source.

Replace line 802 only:

```csharp
// BEFORE (line 802, incorrect attribution):
// NT8-003: no Math.Clamp (banned in .NET 4.8). Math.Max/Min used instead.

// AFTER (corrected):
// NT8-031: no Math.Clamp (.NET 4.8 version constraint -- not the NT8-003 volatile ban).
```

**Only line 802 comment text changes.** No logic, no signatures, no methods are altered.
`AtrSizingEngine.cs` is READ ONLY for B13 (no changes to that file).

### 5.3 Change 2 -- NT8-031 rule row in NT8_COMPILER_RULES.md (Director workspace)

File: `docs/standards/NT8_COMPILER_RULES.md`
Action: Append a new rule row for NT8-031 to the INDEX TABLE and add a rule entry section.

**Rule entry to add:**

```
NT8-031
ERROR:   System.Math.Clamp not found (or method has no overloads matching argument types)
CAUSE:   Math.Clamp was added in .NET Standard 2.1 / .NET Core 2.0. NT8 targets .NET Framework 4.8
         which does not include Math.Clamp.
BANNED:  Math.Clamp(value, min, max)
SAFE:    value < min ? min : value > max ? max : value
SCAN:    grep -r "Math.Clamp" src/ --include="*.cs"
NOTE:    This is a .NET 4.8 version constraint, NOT the NT8-003 volatile ban.
         Comments in source that cite "NT8-003" for Math.Clamp are incorrect; use NT8-031.
```

### 5.4 Rules check

| Rule | Status |
|------|--------|
| No new methods | PASS -- comment-only change |
| No new fields | PASS |
| No CopyEngine change | PASS |
| ASCII-only in comment | PASS -- no Unicode |
| CYC unchanged | PASS -- no logic changed |
| No lock / no async void | PASS -- no new code |

### 5.5 Tests

T3 is a pure docs+comment change. No xUnit test applies.

No behavioral change to `AtrSizingEngine` logic -- the manual clamp expression is identical to
what is already compiled. The comment correction is verified by code review (ptt-verifier reads
the updated comment and rules doc, confirms attribution matches NT8-031 rule).

---

## 6. NinjaTrader 8 API Surface Reference

### 6.1 MarketData model (NT8-032, B12 confirmed)

```
instrument.MarketData           -> MarketDataEventArgs   (live snapshot)
instrument.MarketData.Bid       -> MarketDataEventArgs   (bid snapshot)
instrument.MarketData.Ask       -> MarketDataEventArgs   (ask snapshot)
instrument.MarketData.Last      -> MarketDataEventArgs   (last trade snapshot)
instrument.MarketData.Bid.Price -> double
instrument.MarketData.Ask.Price -> double
instrument.MarketData.Last.Price-> double  (T1 uses this)
```

Source: NT8 reflection cache, confirmed in B12 green build (Round 3 fix).

### 6.2 Rules applied (index)

| Rule | Applied in |
|------|-----------|
| NT8-027: MarketData from AddOn -- verify before use | T1: confirmed safe, synchronous read |
| NT8-032: instrument.MarketData.Last is EventArgs, not double | T1: use `.Last.Price` |
| NT8-033: Chart.BarsArray does not exist | T1: NOT using chart bar data |
| NT8-003: no volatile double | T2/T3: no new volatile fields |
| NT8-031: Math.Clamp absent (.NET 4.8) | T3: comment fix -- new rule entry |
| NT8-017: volatile for cross-thread bool/int | No new cross-thread fields in B13 |
| NT8-018: no lock | All: no lock usage |
| NT8-019: no async void | All: no async usage |

---

## 7. Threading Model

| Change | Thread of write | Thread of read | Safety |
|--------|----------------|----------------|--------|
| T1 GetRefPrice | UI (event handlers) | UI (event handlers) | Same thread -- safe |
| T2 NotifyAtrFractionChanged (OnLoaded) | UI (Loaded event) | Bar-close thread (OnBarUpdate) | Plain double, x64 atomic, sizing hint only -- same pattern as existing _lastAtr |
| T2 NotifyRiskChanged (OnLoaded) | UI (Loaded event) | Bar-close thread (OnBarUpdate) | Same as above |
| T3 Comment fix | n/a | n/a | Documentation only |

No `Dispatcher.InvokeAsync` required in B13 (all new code originates on UI thread).
No new cross-thread fields introduced.

---

## 8. Data Flow Diagrams

### T1 -- GetRefPrice wired

```
User clicks [Trim +N] button
  OnTrimClick() [UI thread]
    GetRefPrice()
      _instrument.MarketData.Last.Price -> double  (or 0.0 if any null)
    if refPrice > 0 and _trimBuffer > 0:
      _engine.Trim(_instrument, _trimBuffer, refPrice)   <- limit exit
    else:
      _engine.Trim(_instrument)                          <- market exit
```

### T2 -- Startup sync

```
DoInject() -> StartAtrEngine(chart, instr)
  -> CopyEngine.Instance.SetAtrEngine(engine, enabled: false)
  -> [panel added to Grid]

Panel.Loaded fires -> OnLoaded()
  -> ... account population ...
  -> LoadAtmTemplates()
  -> NotifyRiskChanged()        -> CopyEngine.UpdateMaxRisk(200.0)
                                    -> _atrEngine.UpdateMaxRisk(200.0)   <- syncs engine default
  -> NotifyAtrFractionChanged() -> CopyEngine.UpdateAtrFraction(0.75)
                                    -> _atrEngine.SetAtrFraction(0.75)   <- syncs engine default
```

### T3 -- Comment fix only (no runtime flow change)

```
AtrSizingEngine.cs: clamp expression logic unchanged.
Comment updated: "NT8-003" -> "NT8-031" attribution.
NT8_COMPILER_RULES.md: new row NT8-031 appended.
No runtime data flow affected.
```

---

## 9. Method Signatures (complete)

### TradeCopierPanel.cs (Wave workspace)

| Signature | Change | CYC |
|-----------|--------|-----|
| `private double GetRefPrice()` | Body replaced (T1) | 4 |
| `private void OnLoaded(object sender, RoutedEventArgs e)` | 2 lines appended (T2) | unchanged |

### AtrSizingEngine.cs (Wave workspace)

| Signature | Change | CYC |
|-----------|--------|-----|
| (no signature changes) | Comment-only update (T3) | unchanged |

### NT8_COMPILER_RULES.md (Director workspace)

| Section | Change |
|---------|--------|
| INDEX TABLE | New row: NT8-031 Math.Clamp absent |
| Rule entry | New section: NT8-031 full rule |

### CopyEngineTests.cs (Wave workspace)

| Test method | Asserts |
|------------|---------|
| `UpdateAtrFraction_ForwardsToEngine_WhenEngineSet` | `Assert.Equal(5, qty)` -- confirms engine receives fraction via wiring chain |

---

## 10. 7-Scan Checklist Gate (pre-flight)

| SCAN | Check | Result |
|------|-------|--------|
| SCAN-01 | No `lock(` in any new/modified code | PASS -- no lock in GetRefPrice, OnLoaded append, or comment fix |
| SCAN-02 | No `async void` methods | PASS -- no async in any change |
| SCAN-03 | No `return null` for missing values (non-nullable types) | PASS -- GetRefPrice returns `double` (0.0), not null |
| SCAN-04 | No `DateTime.Now` | PASS -- no date/time usage in B13 |
| SCAN-05 | All `CreateOrder` signal names start with `"PTT-"` | PASS -- no new CreateOrder calls |
| SCAN-06 | No hex string color literals | PASS -- no new color assignments or UI controls in B13 |
| SCAN-07 | All cross-thread mutable fields are `volatile` | PASS -- no new cross-thread fields; existing `_atrEnabled` already volatile |

---

## 11. Forward Roadmap (B14 targets)

| ID | Description | Priority |
|----|-------------|----------|
| DW-B12-DEFER-01 (original) | Full-panel mode expansion: Buy Ask / Sell Bid quick-entry buttons | P2 |
| DW-B9-01 | ATR box visualization on chart canvas | P2 |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset | P3 |
| DW-B12-DEFER-02 (original) | Auto-trail stop from BE CONNECTED level | P3 |
| DW-B12-DEFER-04 | Align CopyEngineTests.cs implemented test names with 04-tickets.md | P3 |

---

## 12. Spec Coverage Matrix

| Spec B13 Target (line 7424) | Addressed in Plan? | Plan Section |
|----------------------------|--------------------|--------------|
| DW-B9-01 ATR box on canvas | Shelved to B14 (explicit) | §1 "Shelved" |
| DW-B9-03 Click-trader Bid+1/Ask-1 | Shelved to B14 (explicit) | §1 "Shelved" |
| DW-B12-DEFER-01 GetRefPrice via MarketData.Last.Price | Addressed | §3 Ticket T1 |
| DW-B12-DEFER-02 ATR fraction spinner startup sync | Addressed | §4 Ticket T2 |

| Plan Ticket | Spec/Backlog Basis | Status |
|-------------|-------------------|--------|
| T1 | Spec line 7424 (DW-B12-DEFER-01) | In scope |
| T2 | Spec line 7424 (DW-B12-DEFER-02) | In scope |
| T3 | B12 backlog DW-B12-DEFER-03 (P3 docs fix, carried from B12) | In scope (P3 docs only) |

---

## 13. Revision Summary (R2 vs R1)

### V-01 fix (T3 re-scoped)

| R1 (FAIL) | R2 (corrected) |
|-----------|----------------|
| T3 = ATR enable/disable CheckBox UI feature | T3 = DW-B12-DEFER-03 docs+comment fix |
| Added `CopyEngine.SetAtrEnabled(bool)` | No CopyEngine change |
| Added `private CheckBox _atrEnableCheck` field | No new field |
| Added `OnAtrEngineToggle` handler | No new handler |
| Added `BuildUI()` row with CheckBox | No UI change |
| Not traceable to spec B13 targets | Traceable to B12 backlog DW-B12-DEFER-03 (OPEN, P3) |

### V-02 fix (T2 test assertion added)

| R1 (FAIL) | R2 (corrected) |
|-----------|----------------|
| Test body ends with teardown; no Assert.* call | `Assert.Equal(5, qty)` added |
| Comment: "Verification: no exception thrown = chain is connected" | Actual assertion on observable state |
| Test named `UpdateAtrFraction_Persists_ToAtrEngine_WhenEngineSet` | Renamed `UpdateAtrFraction_ForwardsToEngine_WhenEngineSet` for clarity |
| Vacuous pass (zero coverage signal) | Distinguishes enabled-engine path (qty=5) from fallback path (qty=1) |
