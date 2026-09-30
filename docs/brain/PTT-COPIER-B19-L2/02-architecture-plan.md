# PTT-COPIER-B19-L2 — Architecture Plan
**Ticket**: DW-B19-LIMIT-PRICE-01
**Block**: PTT-COPIER-B19-L2 (Lane 2 — isolated from Lane 1 collision)
**Status**: REVIEW_PASS (Cycle 3 amendment)
**NT8-032**: Registered in docs/standards/NT8_COMPILER_RULES.md Version 1.2
**Date**: 2026-07-07

---

## §1 — Scope

This block fixes one surgical bug in the limit-order price anchor used by
`Trim` and `Flatten` in `CopyEngine.cs`.  The previous implementation used
`md.Last.Price` as the reference price for both long and short exits.  The
correct anchor is:

| Direction | Order type      | Correct anchor |
|-----------|-----------------|---------------|
| Long exit | Sell Limit      | `md.Ask.Price` |
| Short exit| BuyToCover Limit| `md.Bid.Price` |

**Files touched (3):**

| File | Change class |
|------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | Signature change: `refPrice` → `ask, bid`; add `ComputeLimitPx` helper |
| `src/PropTraderTools/TradeCopierPanel.cs` | Remove `GetRefPrice()`; add `GetAsk()` / `GetBid()` |
| `src/PropTraderTools/CopyEngineTests.cs` | Update 5 existing tests; add 5 new [Fact] tests |

**Files NOT touched:** `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`, `AtrSizingEngine.cs`

---

## §2 — Problem Statement

```
GetRefPrice(instrument)
    => instrument.MarketData.Last.Price    // wrong anchor
```

`TradeCopierPanel.cs` called `GetRefPrice()` and passed the single value to
both `Trim` and `Flatten`.  Inside `CopyEngine.cs` the engine then set:

```
limitPx = isLong
    ? refPrice + exitBuffer * tickSize   // last + buffer  (should be ask + buffer)
    : refPrice - exitBuffer * tickSize;  // last - buffer  (should be bid - buffer)
```

Using `Last` as the anchor:
- For a long exit the limit posts below the current offer → often fills immediately,
  defeating the purpose of a passive limit.
- For a short exit the limit posts above the current bid → same problem.

The semantics require the limit to sit **just beyond the current best quote** so
it is passive by a configurable number of ticks.

---

## §3 — Correct Semantics

```
Long exit  (Sell Limit):     ask + exitBuffer * tickSize
Short exit (BuyToCover):     bid - exitBuffer * tickSize
```

This guarantees the limit is always placed on the passive side of the spread,
outside the current best quote by exactly `exitBuffer` ticks.

---

## §4 — CopyEngine.cs Changes (FILE 1)

### 4.1 `Trim` — old vs new signature

```csharp
// OLD (B12)
internal void Trim(Instrument instrument, int exitBuffer, double refPrice)

// NEW (B19-L2)
internal void Trim(Instrument instrument, int exitBuffer, double ask, double bid)
```

### 4.2 `Flatten` — old vs new signature

```csharp
// OLD (B12)
internal void Flatten(Instrument instrument, int exitBuffer, double refPrice)

// NEW (B19-L2)
internal void Flatten(Instrument instrument, int exitBuffer, double ask, double bid)
```

### 4.3 ComputeLimitPx — Extracted Price Seam

```csharp
// B19 T1 -- ComputeLimitPx: pure-arithmetic price anchor helper.
// Extracted from Trim/Flatten limit bodies to enable unit testing of the direction logic.
// CYC=1: single ternary (one decision point). No NT8 deps, no state, no nulls.
// internal static so tests call CopyEngine.ComputeLimitPx(...) directly.
internal static double ComputeLimitPx(bool isLong, double ask, double bid, int exitBuffer, double tickSize)
    => isLong
        ? ask + exitBuffer * tickSize
        : bid - exitBuffer * tickSize;
```

Both `Trim(Instrument,int,double,double)` and `Flatten(Instrument,int,double,double)` call this
instead of the inline ternary:

```csharp
// OLD (inline in both methods):
double refPrice = isLong ? ask : bid;
double limitPx  = isLong
    ? refPrice + exitBuffer * tickSize
    : refPrice - exitBuffer * tickSize;

// NEW (one call):
double limitPx = ComputeLimitPx(isLong, ask, bid, exitBuffer, tickSize);
```

Behavioral result is identical. CYC for Trim/Flatten drops from 7 to 6 (the `refPrice` ternary
and `limitPx` ternary are now inside ComputeLimitPx). Net CYC added to codebase: 1 (ComputeLimitPx itself).

### 4.4 Guard block (identical in both methods)

```csharp
if (ask <= 0 || bid <= 0 || exitBuffer == 0)
{
    // Fallback: market order without limit price
    Trim(instrument);   // or Flatten(instrument)
    return;
}
```

The guard preserves the existing zero-arg market-order overload as the safe
fallback.  No exceptions thrown (JS-001 compliance).  No null returned
(JS-002 compliance).

### 4.5 CreateOrder call — NT8-007 and NT8-014 unchanged

```csharp
// arg 12 stays (NinjaTrader.Cbi.CustomOrder)null  — NT8-007
// order name stays "PTT-TrimLimit" / "PTT-FlattenLimit"  — NT8-014
// time-in-force stays DateTime.MaxValue  — NT8-013
```

No changes to the 12-argument `CreateOrder` call beyond the `limitPx` value.

---

## §5 — TradeCopierPanel.cs Changes (FILE 2)

### 5.1 Remove `GetRefPrice()`

```csharp
// DELETED — reads md.Last.Price which is the wrong anchor
private double GetRefPrice() { ... }
```

### 5.2 Add `GetAsk()`

Uses `_instrument` field (same pattern as the `GetRefPrice()` it replaces — no parameter).

```csharp
// B19 T1 -- GetAsk: returns current ask price from _instrument.MarketData.Ask.Price.
// NT8-032: md.Ask is MarketDataEventArgs; .Price is the double value. CYC=4.
private double GetAsk()
{
    if (_instrument == null) return 0.0;     // (1) guard
    var md = _instrument.MarketData;
    if (md == null)   return 0.0;            // (2) guard
    var ask = md.Ask;                        // NT8-032: Ask is MarketDataEventArgs
    if (ask == null)  return 0.0;            // (3) guard
    return ask.Price;                        // (4) double
}
```

**CYC = 4** (3 guard branches + 1 happy path).

### 5.3 Add `GetBid()`

```csharp
// B19 T1 -- GetBid: returns current bid price from _instrument.MarketData.Bid.Price.
// NT8-032: md.Bid is MarketDataEventArgs; .Price is the double value. CYC=4.
private double GetBid()
{
    if (_instrument == null) return 0.0;     // (1) guard
    var md = _instrument.MarketData;
    if (md == null)   return 0.0;            // (2) guard
    var bid = md.Bid;                        // NT8-032: Bid is MarketDataEventArgs
    if (bid == null)  return 0.0;            // (3) guard
    return bid.Price;                        // (4) double
}
```

**CYC = 4** (3 guard branches + 1 happy path).

### 5.4 `OnTrimClick` — updated call site

```csharp
// OLD
_engine.Trim(_instrument, _trimBuffer, GetRefPrice());

// NEW
double ask = GetAsk();
double bid = GetBid();
if (ask <= 0 || bid <= 0 || _trimBuffer == 0)
    _engine.Trim(_instrument);
else
    _engine.Trim(_instrument, _trimBuffer, ask, bid);
```

### 5.5 `OnFlattenClick` — updated call site

```csharp
// OLD
_engine.Flatten(_instrument, _flattenBuffer, GetRefPrice());

// NEW
double ask = GetAsk();
double bid = GetBid();
if (ask <= 0 || bid <= 0 || _flattenBuffer == 0)
    _engine.Flatten(_instrument);
else
    _engine.Flatten(_instrument, _flattenBuffer, ask, bid);
```

### 5.6 Shortcut dispatcher — Key.T and Key.F

```csharp
// Key.T  (Trim shortcut)
case Key.T: _engine.Trim(_instrument, _trimBuffer, GetAsk(), GetBid());     break;

// Key.F  (Flatten shortcut)
case Key.F: _engine.Flatten(_instrument, _flattenBuffer, GetAsk(), GetBid()); break;
```

Both dispatch paths now pass `ask` and `bid` consistently.

---

## §6 — CopyEngineTests.cs Changes (FILE 3)

### 6.1 Update 5 existing B12 tests

All existing tests that invoke `Trim` or `Flatten` via reflection use a 3-element
`Type[]` and `object[]`.  These become 4-element arrays:

```csharp
// OLD (3-arg)
var types = new[] { typeof(Instrument), typeof(int), typeof(double) };
var args  = new object[] { instrument, exitBuffer, refPrice };

// NEW (4-arg)
var types = new[] { typeof(Instrument), typeof(int), typeof(double), typeof(double) };
var args  = new object[] { instrument, exitBuffer, ask, bid };
```

Direct calls in the same tests change from `(instrument, buf, refPrice)` to
`(instrument, buf, ask, bid)`.

Affected tests (5 total) — **exact method names from CopyEngineTests.cs**:
1. `Flatten_LimitOverload_LongPosition_EmitsSellLimitFullQty` (B12 T-B12-01)
2. `Flatten_LimitOverload_ShortPosition_EmitsBuyToCoverLimitFullQty` (B12 T-B12-02)
3. `Trim_LimitOverload_LongPosition_EmitsSellLimitAtRefPlusTick` (B12 T-B12-03)
4. `Trim_LimitOverload_ShortPosition_EmitsBuyToCoverLimitAtRefMinusTick` (B12 T1-Test-2)
5. `Flatten_ZeroBuffer_FallsBackToMarketOrder` (B12 T-B12-05)

### 6.2 Add 5 new [Fact] tests

Tests 1–4 call `CopyEngine.ComputeLimitPx` directly (pure arithmetic, no NT8 deps):

| Test name | Assert |
|-----------|--------|
| `TrimLimit_Long_PlacesAboveAsk` | `CopyEngine.ComputeLimitPx(isLong:true, ask:5000.25, bid:5000.00, exitBuffer:1, tickSize:0.25) == 5000.50` (ask + 1 tick) |
| `TrimLimit_Short_PlacesBelowBid` | `CopyEngine.ComputeLimitPx(isLong:false, ask:5000.25, bid:5000.00, exitBuffer:1, tickSize:0.25) == 4999.75` (bid - 1 tick) |
| `FlattenLimit_Long_PlacesAboveAsk` | `CopyEngine.ComputeLimitPx(isLong:true, ask:5000.25, bid:0, exitBuffer:2, tickSize:0.25) == 5000.75` (ask + 2 ticks) |
| `FlattenLimit_Short_PlacesBelowBid` | `CopyEngine.ComputeLimitPx(isLong:false, ask:0, bid:5000.00, exitBuffer:2, tickSize:0.25) == 4999.50` (bid - 2 ticks) |
| `TrimLimit_FallsBackToMarket_WhenAskIsZero` | market overload called when `ask == 0` — 3 direct no-throw calls |

All 5 new tests use xUnit `[Fact]` (never NUnit / MSTest — xUnit mandate).

---

## §7 — Cyclomatic Complexity Table

| Method | File | CYC | Decision points |
|--------|------|-----|-----------------|
| `ComputeLimitPx` | CopyEngine.cs | 1 | 1 ternary |
| `GetAsk()` | TradeCopierPanel.cs | 4 | 3 null guards + return |
| `GetBid()` | TradeCopierPanel.cs | 4 | 3 null guards + return |
| `OnTrimClick` (updated) | TradeCopierPanel.cs | 4 | null instr + 2 (compound) + else |
| `OnFlattenClick` (updated) | TradeCopierPanel.cs | 4 | null instr + 2 (compound) + else |
| `Trim(Instrument, int, double, double)` | CopyEngine.cs | 6 | 2 (ask/bid\|\|) + 1 (exitBuffer) + 1 (foreach) + 2 (pos null\|\|qty) |
| `Flatten(Instrument, int, double, double)` | CopyEngine.cs | 6 | same as Trim |

All values ≤ 8.  Jane Street strict standard satisfied.  PASS.

---

## §8 — Jane Street Compliance Table

| Rule | Description | Status |
|------|-------------|--------|
| JS-021 | No `lock()` anywhere in src/ | PASS — no lock() added |
| JS-001 | No `throw` in hot path | PASS — guard returns `0.0`, no throw |
| JS-002 | No `return null` for missing values | PASS — helpers return `0.0` |
| JS-010 | Smart constructor / factory pattern | N/A — no new classes |
| JS-033 | No `async void` (non-event-handler) | PASS — no async void added |

---

## §9 — NT8 Compiler Rules Compliance Table

| Rule | Description | Status |
|------|-------------|--------|
| NT8-007 | `CreateOrder` arg 12 = `(NinjaTrader.Cbi.CustomOrder)null` | PASS — unchanged |
| NT8-013 | Time-in-force = `DateTime.MaxValue` | PASS — unchanged |
| NT8-014 | Order name prefix `"PTT-"` | PASS — `"PTT-TrimLimit"` / `"PTT-FlattenLimit"` unchanged |
| NT8-032 | `MarketData.Ask/.Bid` are `MarketDataEventArgs`; use `.Price`; full null-guard chain required | PASS — `GetAsk()` / `GetBid()` implement 3-level null guard |

---

## §10 — Build Steps

The engineer MUST execute these steps in order after implementing the changes:

1. `dotnet csharpier format src/PropTraderTools/`
2. `dotnet build` (zero errors, zero warnings required)
3. `dotnet test` (all tests pass, including 5 new [Fact] tests)
4. `powershell -File .\deploy-sync.ps1` (hard-link re-sync to NinjaTrader)
5. F5 in NinjaTrader (NT8 compile gate — must be GREEN before merge)
6. `powershell -File .\scripts\pre_push_validation.ps1` (13-check gate)

---

## §11 — Shelved Items (Not in This Block)

The following were considered and explicitly deferred to preserve scope
discipline (No Scope Creep Protocol V12.23):

- Replacing the `int exitBuffer` parameter with a typed `ExitBufferTicks`
  value-object (JS-015 improvement — deferred to B20)
- Adding bid/ask spread validation (`ask - bid > maxSpread`) before placing
  the limit — deferred to B20
- ATR-relative buffer sizing — belongs to `AtrSizingEngine.cs`, different epic

---

## §12 — Deferred Items for B20

| Item | Priority | Rationale |
|------|----------|-----------|
| `ExitBufferTicks` value-object (JS-015) | P2 | Prevents raw `int` crossing API boundary |
| Spread validation guard in `GetAsk`/`GetBid` | P2 | Safety: reject stale/crossed quotes |
| `OnMarketData` event hook to refresh ask/bid in panel | P2 | Eliminate stale quote risk at button press |
| Telemetry: log anchor price at order placement | P3 | Observability — does not affect correctness |

---

## §13 — Data Flow

```
Button press / keyboard shortcut (UI thread)
  └─> TradeCopierPanel.OnTrimClick / OnFlattenClick / DispatchShortcut
        |
        ├─> GetAsk()   [no-arg, reads _instrument field]
        |     _instrument.MarketData.Ask.Price  (NT8-032 null-guarded)
        |
        └─> GetBid()   [no-arg, reads _instrument field]
              _instrument.MarketData.Bid.Price  (NT8-032 null-guarded)
              |
              v
        CopyEngine.Trim(instrument, exitBuffer, ask, bid)
          |
          ├─> Guard: ask <= 0 || bid <= 0 || exitBuffer == 0
          |     => Trim(instrument)  [market fallback]
          |
          └─> ComputeLimitPx(isLong, ask, bid, exitBuffer, tickSize)
                  => isLong ? ask + exitBuffer * tickSize
                            : bid - exitBuffer * tickSize
                  |
                  v
              CreateOrder("PTT-TrimLimit", ..., limitPx, ..., (CustomOrder)null)
```

All operations on the UI thread dispatched via `Dispatcher.InvokeAsync` where
required by existing NT8 add-on threading rules — no new threading introduced.

---

## §14 — Forward Roadmap

| Block | Theme |
|-------|-------|
| B19-L1 | Lane 1 bug fix (DW-B19-COPIER-BUG-01) — isolated |
| **B19-L2** | **This block — limit price anchor fix** |
| B20 | `ExitBufferTicks` value-object + spread validation + market-data event hook |
| B21 | ATR-relative buffer sizing integration with `AtrSizingEngine.cs` |

---

## §15 — Ticket Summary

### Ticket: DW-B19-LIMIT-PRICE-01

**Spec requirement**: Fix limit order price anchor from `Last` → `Ask`/`Bid`

**Files**:
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/TradeCopierPanel.cs`
- `src/PropTraderTools/CopyEngineTests.cs`

**Method signatures to implement**:

```csharp
// CopyEngine.cs
internal static double ComputeLimitPx(bool isLong, double ask, double bid, int exitBuffer, double tickSize)
internal void Trim(Instrument instrument, int exitBuffer, double ask, double bid)
internal void Flatten(Instrument instrument, int exitBuffer, double ask, double bid)

// TradeCopierPanel.cs
private double GetAsk()   // CYC=4, no-arg, reads _instrument field, returns 0.0
private double GetBid()   // CYC=4, no-arg, reads _instrument field, returns 0.0
```

**ComputeLimitPx is in T1 scope**: The helper is extracted from `Trim`/`Flatten` bodies as
part of Ticket 1 (DW-B19-LIMIT-PRICE-01).  It is `internal static` so `CopyEngineTests.cs`
can call it directly without reflection.

**xUnit tests to write** (CopyEngineTests.cs):

| Test | Assert |
|------|--------|
| `TrimLimit_Long_PlacesAboveAsk` | `ComputeLimitPx(true, 5000.25, 5000.00, 1, 0.25) == 5000.50` |
| `TrimLimit_Short_PlacesBelowBid` | `ComputeLimitPx(false, 5000.25, 5000.00, 1, 0.25) == 4999.75` |
| `FlattenLimit_Long_PlacesAboveAsk` | `ComputeLimitPx(true, 5000.25, 0, 2, 0.25) == 5000.75` |
| `FlattenLimit_Short_PlacesBelowBid` | `ComputeLimitPx(false, 0, 5000.00, 2, 0.25) == 4999.50` |
| `TrimLimit_FallsBackToMarket_WhenAskIsZero` | market overload invoked (3 no-throw calls) |

**SCAN-01**: No `lock()` — grep `lock\s*\(` in 3 files → zero hits required
**SCAN-02**: No `async void` — grep `async void ` → zero hits required
**SCAN-03**: No `return null` — grep `return null;` in helpers → zero hits required
**SCAN-04**: NT8-032 null-guard chain — `GetAsk`/`GetBid` each have 3-level null guard
**SCAN-05**: NT8-007 — `CreateOrder` arg 12 = `(NinjaTrader.Cbi.CustomOrder)null`
**SCAN-06**: NT8-014 — order names start with `"PTT-"`
**SCAN-07**: CYC ≤ 8 — all methods in table §7 confirmed ≤ 8

---

*Plan status: REVIEW_PASS (Cycle 3 amendment) — ComputeLimitPx added to architecture. Approved for ticket re-review.*
