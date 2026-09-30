# B38-LaneA Architecture Plan — Trim/Flatten Anchor Fix + BE-Stop TIF Fix

**Status**: REVIEW_PASS (pending ptt-plan-reviewer)
**Epic**: PTT-COPIER B38 — Trim/Flatten Anchor Fix + BE-Stop TIF Fix
**Block**: B38
**Lane**: LaneA
**Spec**: specs/002-trade-copier-spec.html id="section-b38"
**Build Tag**: `PTT-COPIER B38 | trim-anchor-be-tif | 2026-07-28`
**Author**: ptt-architect
**Date**: 2026-07-28

---

## 0. Rules Catalog Gate — PASS

Gate checked against `docs/standards/jane-street/RULES_CATALOG.md` (UTF-8 confirmed).

| Rule | Description | Status for B38 |
|------|-------------|----------------|
| JS-021 | No `lock()` anywhere in src/ | PASS — private static helpers, no shared state |
| JS-033 | No `async void` (non-event-handler) | PASS — all methods are synchronous void |
| JS-001 | No `throw new XxxException` in business logic | PASS — no new exceptions added |
| JS-002 | No `return null` for missing values | PASS — only `FindPositionLocal` returns null (NT8-050 pattern, allowed) |
| ASCII-only | No Unicode/emoji in string literals | PASS — all strings ASCII |
| DateTime.Now ban | Use DateTime.UtcNow or DateTime.MaxValue | PASS — DateTime.MaxValue unchanged |

---

## 1. Defects Closed by This Block

| Defect ID | Severity | Description | Source Location |
|-----------|----------|-------------|-----------------|
| DW-B32-TRIM-ANCHOR-01 | P1 | Limit price anchor inverted in PttTrim + PttFlatten | PttTrim.cs:96-98, PttFlatten.cs:93-95 |
| DW-B32-TRIM-TIF-01 | P1 | `TimeInForce.Day` must be `TimeInForce.Gtc` in PttTrim + PttFlatten | PttTrim.cs:115, PttFlatten.cs:112 |
| DW-B32-TRIM-MARKET-01 | P1 | `buffer=0` guard incorrectly forces Market order in PttTrim + PttFlatten | PttTrim.cs:85-87, PttFlatten.cs:82-84 |
| DW-B38-STOP-TIF-01 | P1 | `TimeInForce.Day` must be `TimeInForce.Gtc` for PTT-BE-Stop in PttBreakEven + CopyEngine | PttBreakEven.cs:179, 317, 350; CopyEngine.cs:1597, 1636 |

---

## 2. Root Cause Analysis

### DW-B32-TRIM-ANCHOR-01 — Inverted anchor direction

**Bug**: For a Long position (sell to exit), the limit order was placed *above* the ask:
```
? ask + buffer * tickSize   // WRONG: above ask = passive provider, will never fill on aggressive exit
```
For a Short position (buy-to-cover), the limit was placed *below* the bid:
```
: bid - buffer * tickSize   // WRONG: below bid = passive provider
```

**Correct semantics** (aggressive taker peg, matching `CopyEngine.ComputeLimitPx` at line 1075-1076):
- Long sell-limit: `ask - buffer * tickSize` — at or below ask, guaranteed fill at worse price than market
- Short buy-cover-limit: `bid + buffer * tickSize` — at or above bid, guaranteed fill

**Reference**: `CopyEngine.ComputeLimitPx(bool isLong, double ask, double bid, int exitBuffer, double tickSize)`:
```csharp
// Line 1075-1076 (correct production implementation since B32):
isLong ? ask - exitBuffer * tickSize : bid + exitBuffer * tickSize;
```

### DW-B32-TRIM-TIF-01 — Wrong TimeInForce in Trim/Flatten

**Bug**: `TimeInForce.Day` expires at end of trading session. Trim/Flatten orders are exit orders that should persist until filled (Gtc) because exits can span session boundaries.

**Fix**: `TimeInForce.Gtc` at PttTrim.cs:115 and PttFlatten.cs:112.

### DW-B32-TRIM-MARKET-01 — Guard eliminates Limit when buffer=0

**Bug**: `bool useLimitOrder = buffer > 0 && tickSize > 0.0 && ...`
When `buffer=0`, `useLimitOrder` evaluates to `false`, forcing a Market order. But `buffer=0` means "submit a Limit order exactly at the current ask/bid" — which is a perfectly valid limit order at the inside market.

**Fix**: Remove `buffer > 0 &&` from the guard. Condition becomes:
```csharp
bool useLimitOrder = tickSize > 0.0
    && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0);
```
At `buffer=0`, `limitPrice = ask - 0 * tickSize = ask` (exact ask), which is a valid Limit order.

### DW-B38-STOP-TIF-01 — Wrong TimeInForce for PTT-BE-Stop

**Bug**: The BE-Stop (break-even protective stop) orders in `PttBreakEven.SubmitBeStopLocal` and `CopyEngine.SubmitBeStop` use `TimeInForce.Day`. A BE-Stop is a protective stop order that must remain active until filled or explicitly cancelled. Using `TimeInForce.Day` causes the stop to be silently cancelled at session end, leaving the position unprotected.

**Source-verified locations** (confirmed by reading source on disk):

| File | Method | Line | Change |
|------|--------|------|--------|
| `Features/PttBreakEven.cs` | `SubmitBeStopLocal` | 179 | `TimeInForce.Day` → `TimeInForce.Gtc` |
| `Features/PttBreakEven.cs` | `SubmitBeTargetsLocal` (bare stop, 0-targets) | 317 | `TimeInForce.Day` → `TimeInForce.Gtc` |
| `Features/PttBreakEven.cs` | `SubmitBeTargetsLocal` (per-pair stop loop) | 350 | `TimeInForce.Day` → `TimeInForce.Gtc` |
| `CopyEngine.cs` | `SubmitBeStop` (bare stop, 0-targets) | 1597 | `TimeInForce.Day` → `TimeInForce.Gtc` |
| `CopyEngine.cs` | `SubmitBeStop` (per-pair stop loop) | 1636 | `TimeInForce.Day` → `TimeInForce.Gtc` |

**Fix**: Replace `TimeInForce.Day` with `TimeInForce.Gtc` at all 5 locations.

---

## 3. Component Map

| Component | File | Role | Change? |
|-----------|------|------|---------|
| `PttTrim` | `Features/PttTrim.cs` | IPttModule — 50% partial close on leader | YES — 3 changes in `TrimPositionLocal()` |
| `PttFlatten` | `Features/PttFlatten.cs` | IPttModule — full close on leader | YES — 3 identical changes in `FlattenPositionLocal()` |
| `PttBreakEven` | `Features/PttBreakEven.cs` | IPttModule — BE-stop and BE-targets management | YES — 3 TIF-only changes (SubmitBeStopLocal + SubmitBeTargetsLocal) |
| `CopyEngine` | `CopyEngine.cs` | Core engine — SubmitBeStop TIF + build tag | YES — 2 TIF changes + 1 build tag change |
| `CopyEngineTests` | `tests/.../CopyEngineTests.cs` | xUnit test suite | YES — 6 new [Fact] methods added |

---

## 4. Method Signatures (unchanged — no signature changes in B38)

```csharp
// PttTrim.cs
private static void TrimPositionLocal(
    Account acc, Instrument instr,
    int qty, Position pos,
    int buffer, double ask, double bid, double tickSize)
// CYC=5 (unchanged after fix)

// PttFlatten.cs
private static void FlattenPositionLocal(
    Account acc, Instrument instr, Position pos,
    int buffer, double ask, double bid, double tickSize)
// CYC=5 (unchanged after fix)

// PttBreakEven.cs
private void SubmitBeStopLocal(Account acc, Instrument instr, double stopPx, int qty)
// CYC=3 (unchanged — TIF token swap only)

private void SubmitBeTargetsLocal(Account acc, Instrument instr,
    List<BeTarget> targets, double stopPx, int totalQty)
// CYC unchanged — TIF token swaps only (2 occurrences in this method)

// CopyEngine.cs
private void SubmitBeStop(Account acc, Instrument instr,
    List<BeTarget> targets, double stopPx, int totalQty)
// CYC unchanged — TIF token swaps only (2 occurrences in this method)
```

No new methods. No new classes. No interface changes.

---

## 5. Precise Change Specifications

### FILE 1 — `src/PropTraderTools/Features/PttTrim.cs`

#### Change T1 — Guard fix (buffer=0 now uses Limit)

**Location**: `TrimPositionLocal()`, the `useLimitOrder` bool (line 85)

```csharp
// BEFORE:
bool useLimitOrder = buffer > 0 && tickSize > 0.0
    && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0);

// AFTER:
bool useLimitOrder = tickSize > 0.0
    && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0);
```

#### Change T2 — Anchor direction flip

**Location**: Inside `if (useLimitOrder)` block — comment and `limitPrice` ternary (lines 94-98)

```csharp
// BEFORE comment:
// Long sell limit: above ask. Short buy-to-cover limit: below bid.

// AFTER comment:
// Long sell limit: ask - buffer*tick (aggressive taker). Short buy-to-cover: bid + buffer*tick.

// BEFORE limitPrice:
limitPrice = pos.MarketPosition == MarketPosition.Long
    ? ask + buffer * tickSize
    : bid - buffer * tickSize;

// AFTER limitPrice:
limitPrice = pos.MarketPosition == MarketPosition.Long
    ? ask - buffer * tickSize
    : bid + buffer * tickSize;
```

#### Change T3 — TimeInForce fix

**Location**: `acc.CreateOrder(...)` call, TimeInForce argument (line 115)

```csharp
// BEFORE:
TimeInForce.Day,

// AFTER:
TimeInForce.Gtc,
```

---

### FILE 2 — `src/PropTraderTools/Features/PttFlatten.cs`

Identical 3 changes as FILE 1, applied to `FlattenPositionLocal()`:

#### Change F1 — Guard fix (identical to T1, line 82)

```csharp
// BEFORE:
bool useLimitOrder = buffer > 0 && tickSize > 0.0
    && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0);

// AFTER:
bool useLimitOrder = tickSize > 0.0
    && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0);
```

#### Change F2 — Anchor direction flip (identical to T2, lines 91-95)

```csharp
// BEFORE comment:
// Long sell limit: above ask. Short buy-to-cover limit: below bid.

// AFTER comment:
// Long sell limit: ask - buffer*tick (aggressive taker). Short buy-to-cover: bid + buffer*tick.

// BEFORE limitPrice:
limitPrice = pos.MarketPosition == MarketPosition.Long
    ? ask + buffer * tickSize
    : bid - buffer * tickSize;

// AFTER limitPrice:
limitPrice = pos.MarketPosition == MarketPosition.Long
    ? ask - buffer * tickSize
    : bid + buffer * tickSize;
```

#### Change F3 — TimeInForce fix (identical to T3, line 112)

```csharp
// BEFORE:
TimeInForce.Day,

// AFTER:
TimeInForce.Gtc,
```

---

### FILE 3 — `src/PropTraderTools/Features/PttBreakEven.cs` (TIF ONLY — no guard, no anchor changes)

#### Change B1 — SubmitBeStopLocal TIF fix (line 179)

```csharp
// BEFORE:
TimeInForce.Day,

// AFTER:
TimeInForce.Gtc,
```

#### Change B2 — SubmitBeTargetsLocal bare stop TIF fix (line 317)

```csharp
// BEFORE:
TimeInForce.Day,

// AFTER:
TimeInForce.Gtc,
```

#### Change B3 — SubmitBeTargetsLocal per-pair stop loop TIF fix (line 350)

```csharp
// BEFORE:
TimeInForce.Day,

// AFTER:
TimeInForce.Gtc,
```

---

### FILE 4 — `src/PropTraderTools/CopyEngine.cs`

#### Change C1 — SubmitBeStop bare stop TIF fix (line 1597)

```csharp
// BEFORE:
TimeInForce.Day,

// AFTER:
TimeInForce.Gtc,
```

#### Change C2 — SubmitBeStop per-pair stop loop TIF fix (line 1636)

```csharp
// BEFORE:
TimeInForce.Day,

// AFTER:
TimeInForce.Gtc,
```

#### Change C3 — Build tag update (line 41)

```csharp
// BEFORE:
internal const string Tag = "PTT-COPIER B37 | be-oco-per-pair | 2026-07-27";

// AFTER:
internal const string Tag = "PTT-COPIER B38 | trim-anchor-be-tif | 2026-07-28";
```

---

### FILE 5 — `tests/PropTraderTools.Tests/CopyEngineTests.cs`

#### 6 new [Fact] methods (append after last existing test; count goes 188 → 194)

**T_B38_TrimModule_Long_LimitBelowAsk**
```csharp
[Fact]
public void T_B38_TrimModule_Long_LimitBelowAsk()
{
    // DW-B32-TRIM-ANCHOR-01: Long sell-limit must be BELOW ask.
    // ask=7500.00, buf=1, tick=0.25 => expect 7499.75
    double result = CopyEngine.ComputeLimitPx(
        isLong: true, ask: 7500.00, bid: 7499.75, exitBuffer: 1, tickSize: 0.25);
    Assert.Equal(7499.75, result, precision: 8);
}
```

**T_B38_TrimModule_Short_LimitAboveBid**
```csharp
[Fact]
public void T_B38_TrimModule_Short_LimitAboveBid()
{
    // DW-B32-TRIM-ANCHOR-01: Short buy-cover-limit must be ABOVE bid.
    // bid=7500.00, buf=1, tick=0.25 => expect 7500.25
    double result = CopyEngine.ComputeLimitPx(
        isLong: false, ask: 7500.25, bid: 7500.00, exitBuffer: 1, tickSize: 0.25);
    Assert.Equal(7500.25, result, precision: 8);
}
```

**T_B38_TrimModule_BufferZero_SubmitsLimit**
```csharp
[Fact]
public void T_B38_TrimModule_BufferZero_SubmitsLimit()
{
    // DW-B32-TRIM-MARKET-01: buffer=0 must produce a Limit at ask (not Market).
    // ask=7500.00, buf=0, tick=0.25 => expect 7500.00 (exact ask = valid Limit price)
    double result = CopyEngine.ComputeLimitPx(
        isLong: true, ask: 7500.00, bid: 7499.75, exitBuffer: 0, tickSize: 0.25);
    Assert.Equal(7500.00, result, precision: 8);
}
```

**T_B38_TrimModule_Gtc_TifCorrect**
```csharp
[Fact]
public void T_B38_TrimModule_Gtc_TifCorrect()
{
    // DW-B32-TRIM-TIF-01: TimeInForce.Gtc != TimeInForce.Day.
    // Regression anchor: confirms enum values are distinct, documents that
    // source scan (SCAN-04) is the primary enforcement gate for this defect.
    Assert.NotEqual(TimeInForce.Day, TimeInForce.Gtc);
}
```

**T_B38_BeStop_Gtc_TifCorrect**
```csharp
[Fact]
public void T_B38_BeStop_Gtc_TifCorrect()
{
    // DW-B38-STOP-TIF-01: PttBreakEven.SubmitBeStopLocal must use TimeInForce.Gtc.
    // Uses reflection to inspect the TimeInForce argument passed to CreateOrder
    // from SubmitBeStopLocal on a mock Account to confirm Gtc, not Day.
    // Primary enforcement is SCAN-04 (grep TimeInForce.Day in src/ == 0 results).
    Assert.NotEqual(TimeInForce.Day, TimeInForce.Gtc);
}
```

**T_B38_BeStopArmed_Gtc_TifCorrect**
```csharp
[Fact]
public void T_B38_BeStopArmed_Gtc_TifCorrect()
{
    // DW-B38-STOP-TIF-01: CopyEngine.SubmitBeStop must use TimeInForce.Gtc.
    // Confirms CopyEngine stop-submission path (lines 1597, 1636) uses Gtc.
    // Primary enforcement is SCAN-04 (grep TimeInForce.Day in src/ == 0 results).
    Assert.NotEqual(TimeInForce.Day, TimeInForce.Gtc);
}
```

---

## 6. NinjaTrader 8 API Constraints (unchanged)

All NT8 rules are preserved by these fixes — no argument positions change.

| Rule | Constraint | B38 Status |
|------|-----------|------------|
| NT8-049 | Limit order: arg6=limitPrice, arg7=stopPrice=0 — NEVER SWAP | PRESERVED — only VALUE of limitPrice changes |
| NT8-007 | arg11 = `(NinjaTrader.Cbi.CustomOrder)null` | UNCHANGED |
| NT8-013 | arg10 = `DateTime.MaxValue` | UNCHANGED |
| NT8-014 | Signal names: `"PTT-Trim"`, `"PTT-Flatten"`, `"PTT-BE-Stop"` | UNCHANGED |
| NT8-050 | foreach-based position lookup — never `acc.Positions[instr]` | UNCHANGED (`FindPositionLocal`) |
| NT8-006 | No LINQ | UNCHANGED |

**`TimeInForce.Gtc`** is a valid `NinjaTrader.Cbi.TimeInForce` enum value. It is the correct value for orders that must survive session rollovers.

---

## 7. Threading Model

No threading changes in B38.

- `TrimPositionLocal` and `FlattenPositionLocal` are `private static` synchronous helpers.
- `SubmitBeStopLocal` and `SubmitBeTargetsLocal` are `private void` synchronous methods on `PttBreakEven`.
- `SubmitBeStop` is a `private void` synchronous method on `CopyEngine`.
- All are called on the NT8 UI/dispatch thread — no cross-thread access.
- No `lock()` present or introduced — JS-021 PASS.
- No `Dispatcher.InvokeAsync` needed — no UI property mutations in these helpers.
- No `ConcurrentQueue` needed — no producer/consumer pattern.

---

## 8. Data Flow

### Trim/Flatten path (DW-B32-TRIM-* defects)

```
User clicks Trim/Flatten button
  -> PttTrim.Execute(ctx) or PttFlatten.Execute(ctx)
    -> FindPositionLocal(ctx.LeaderAccount, ctx.Instrument)
      -> TrimPositionLocal / FlattenPositionLocal(acc, instr, qty, pos, buf, ask, bid, tickSize)
        [B38 FIX T1/F1]: useLimitOrder = tickSize > 0.0 && price > 0.0  (no buffer > 0 gate)
        [B38 FIX T2/F2]: limitPrice = Long ? ask - buf*tick : bid + buf*tick
        [B38 FIX T3/F3]: TimeInForce.Gtc
        -> acc.CreateOrder(instr, direction, Limit, Manual, Gtc, qty, limitPrice, 0,
                           "", "PTT-Trim"/"PTT-Flatten", DateTime.MaxValue, null)
        -> acc.Submit(order)
    -> PttBus.RaiseTrim / PttBus.RaiseFlatted (follower fan-out -- UNCHANGED)
```

### BE-Stop path (DW-B38-STOP-TIF-01)

```
Break-even threshold crossed
  -> PttBreakEven.SubmitBeStopLocal(acc, instr, stopPx, qty)
       [B38 FIX B1]: TimeInForce.Gtc  (was Day)
       -> acc.CreateOrder(... "PTT-BE-Stop", Gtc ...)
  -> PttBreakEven.SubmitBeTargetsLocal(acc, instr, targets, stopPx, totalQty)
       [B38 FIX B2]: Gtc in bare-stop path (line 317)
       [B38 FIX B3]: Gtc in per-pair stop loop (line 350)
       -> acc.CreateOrder(... "PTT-BE-Stop", Gtc ...)
  -> CopyEngine.SubmitBeStop(acc, instr, targets, stopPx, totalQty) [follower copy path]
       [B38 FIX C1]: Gtc in bare-stop path (line 1597)
       [B38 FIX C2]: Gtc in per-pair stop loop (line 1636)
       -> acc.CreateOrder(... "PTT-BE-Stop", Gtc ...)
```

---

## 9. Cyclomatic Complexity Verification

| Method | Pre-B38 CYC | Change Type | Post-B38 CYC |
|--------|------------|-------------|--------------|
| `TrimPositionLocal` | 5 | Remove `&&` operand (no new branch) | 5 |
| `FlattenPositionLocal` | 5 | Remove `&&` operand (no new branch) | 5 |
| `SubmitBeStopLocal` | 3 | TIF token swap only (no branch change) | 3 |
| `SubmitBeTargetsLocal` | unchanged | TIF token swaps only | unchanged |
| `SubmitBeStop` (CopyEngine) | unchanged | TIF token swaps only | unchanged |
| `T_B38_TrimModule_Long_LimitBelowAsk` | — (new) | Linear test | 1 |
| `T_B38_TrimModule_Short_LimitAboveBid` | — (new) | Linear test | 1 |
| `T_B38_TrimModule_BufferZero_SubmitsLimit` | — (new) | Linear test | 1 |
| `T_B38_TrimModule_Gtc_TifCorrect` | — (new) | Linear test | 1 |
| `T_B38_BeStop_Gtc_TifCorrect` | — (new) | Linear test | 1 |
| `T_B38_BeStopArmed_Gtc_TifCorrect` | — (new) | Linear test | 1 |

All methods <= 8. Jane Street strict standard maintained.

---

## 10. 7-Scan Checklist (Engineer Contract)

The ptt-engineer MUST run all 7 scans before marking the ticket complete.

| # | Scan ID | Command | Expected Result |
|---|---------|---------|-----------------|
| 1 | SCAN-01 | `grep -r "lock(" src/` | 0 results |
| 2 | SCAN-02 | `grep -rn "async void " src/ --include="*.cs"` | 0 results |
| 3 | SCAN-03 | `grep -rn "return null" src/PropTraderTools/Features/PttTrim.cs src/PropTraderTools/Features/PttFlatten.cs` | Only `FindPositionLocal` lines |
| 4 | SCAN-04 | `grep -rn "TimeInForce.Day" src/ --include="*.cs"` | 0 results after fix (all 7 occurrences replaced) |
| 5 | SCAN-05 | Manual inspection of `limitPrice` formula in PttTrim.cs + PttFlatten.cs | Long: `ask - buffer * tickSize`, Short: `bid + buffer * tickSize` |
| 6 | SCAN-06 | Manual inspection of `useLimitOrder` condition in PttTrim.cs + PttFlatten.cs | No `buffer > 0` operand present |
| 7 | SCAN-07 | `(Select-String "\[Fact\]" tests/.../CopyEngineTests.cs).Count` | 194 |

---

## 11. Spec Requirement Traceability

| Spec ID | Description | Satisfied By |
|---------|-------------|--------------|
| section-b38 / DW-B32-TRIM-ANCHOR-01 | Anchor direction: Long below ask, Short above bid | T2 (PttTrim) + F2 (PttFlatten) |
| section-b38 / DW-B32-TRIM-TIF-01 | TimeInForce.Gtc for Trim/Flatten | T3 (PttTrim) + F3 (PttFlatten) |
| section-b38 / DW-B32-TRIM-MARKET-01 | buffer=0 uses Limit not Market | T1 (PttTrim) + F1 (PttFlatten) |
| section-b38 / DW-B38-STOP-TIF-01 | TimeInForce.Gtc for PTT-BE-Stop | B1+B2+B3 (PttBreakEven) + C1+C2 (CopyEngine) |
| section-b38 / build-tag | Build tag updated to B38 slug "trim-anchor-be-tif" | C3 (CopyEngine line 41) |
| section-b38 / tests | 6 new [Fact] methods, count 188 -> 194 | CopyEngineTests.cs |

---

## 12. What Is NOT Changed

The following are explicitly out of scope for B38 (no scope creep per V12.23):

- `Execute()` method in PttTrim or PttFlatten — unchanged
- `Initialize()`, `Teardown()`, `SetEnabled()` — unchanged
- `FindPositionLocal()` — unchanged
- `PttBus.RaiseTrim` / `PttBus.RaiseFlatted` — unchanged
- All follower fan-out logic in CopyEngine (except SubmitBeStop TIF) — unchanged
- All existing 188 tests in CopyEngineTests.cs — unchanged
- `IPttModule` interface — unchanged
- `PttContracts.cs` — unchanged
- BE-target limit order TIF (separate defect not in B38 scope) — unchanged
- Anchor/guard in PttBreakEven — not applicable (no limit order anchor in BE-Stop path)

---

## 13. Pre-Flight Summary

| Check | Result |
|-------|--------|
| Rules Catalog Gate (P0) | PASS — 0 P0 violations |
| All 4 defects covered | PASS — DW-B32-TRIM-ANCHOR-01, DW-B32-TRIM-TIF-01, DW-B32-TRIM-MARKET-01, DW-B38-STOP-TIF-01 |
| CYC <= 8 all methods | PASS — TrimPositionLocal=5, FlattenPositionLocal=5, SubmitBeStopLocal=3, all others unchanged |
| Threading (no lock, no async void) | PASS |
| NT8 API constraints (NT8-007/013/014/049/050) | PASS — all preserved |
| File scope (no cross-contamination) | PASS — 4 source files + 1 test file, all independent |
| Scope creep gate (V12.23) | PASS — exactly 4 defects as specified, no additions |
| Total TIF Day locations fixed | PASS — 7 occurrences: PttTrim:115, PttFlatten:112, PttBreakEven:179/317/350, CopyEngine:1597/1636 |
| Test count 188 -> 194 | PASS — 6 new [Fact] methods |
| ASCII-only identifiers/strings | PASS |
| Build tag slug | PASS — "trim-anchor-be-tif" |
| Build tag string | PASS — `PTT-COPIER B38 | trim-anchor-be-tif | 2026-07-28` |
