# B32-LaneA Architecture Plan

**Block**: B32-LaneA
**Status**: REVIEW_PENDING
**Architect**: ptt-architect
**Date**: 2026-07-19
**Gate**: PLAN_COMPLETE (pending reviewer)

---

## Scope

Three confirmed defects in the Trim/Flatten limit-order subsystem:

| Defect ID | Req ID | Description |
|---|---|---|
| DW-B32-TRIM-MARKET-01 | R-B32-04 | `buffer==0` falls through to market-order path |
| DW-B32-TRIM-ANCHOR-01 | R-B32-05 | `ComputeLimitPx` uses wrong price anchor |
| DW-B32-TRIM-CLOSE-01 | R-B32-03 | Raw market orders bypass ATM bracket, corrupting OCO |

**Files in scope** (Wave workspace `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`):
- `CopyEngine.cs`
- `TradeCopierPanel.cs`
- `CopyEngineTests.cs`

---

## Ticket Structure

- **Ticket 1** — DW-B32-TRIM-MARKET-01 + DW-B32-TRIM-ANCHOR-01
  Mechanical guard removals + formula swap + test corrections. Low risk.
- **Ticket 2** — DW-B32-TRIM-CLOSE-01
  ATM bracket detection + warn-and-block guard. Higher risk (touches market-order dispatch path).

Tickets are independent: Ticket 2 does not depend on Ticket 1 changes.

---

## STEP 0 — Rules Catalog Gate

**Gate result: PASS**

| P0 Rule | Check |
|---|---|
| JS-021 `lock()` | Zero new lock usages. All new/modified methods use ConcurrentBag/ToList snapshot pattern. |
| JS-033 `async void` | Zero async methods introduced. |
| JS-001 `throw` in hot path | Zero throw statements. All error paths use StatusUpdate + return. |
| JS-002 `return null` | Zero new null returns. IsAtmSlotName returns bool. IsAtmBracketActive returns bool. |
| NT8-019 `async void` | Zero. |
| NT8-013 `DateTime.Now` | Zero. All existing CreateOrder calls already use DateTime.MaxValue. |
| NT8-007 `CreateOrder` arg 12 | No new CreateOrder calls in this epic. Existing calls already compliant. |
| NT8-028 hex colors | Zero. |
| NT8-003 `volatile double` | Zero. |

---

## Defect Analysis

### DW-B32-TRIM-MARKET-01 — Wrong Market Fallback on buffer==0

**Root cause**: Six guard clauses incorrectly include `exitBuffer == 0` / `_trimBuffer == 0` /
`_flattenBuffer == 0` as triggers for the market-order fallback. The only legitimate
market fallback is `ask <= 0 || bid <= 0` (no live market data). `buffer == 0` means
"post limit at exact ask or bid" — it is still a limit order.

**Locations and exact changes**:

| File | Line | Current code (wrong) | Fixed code |
|---|---|---|---|
| `CopyEngine.cs` | 949 | `if (ask <= 0 \|\| bid <= 0 \|\| exitBuffer == 0)` | `if (ask <= 0 \|\| bid <= 0)` |
| `CopyEngine.cs` | 967 | `if (ask <= 0 \|\| bid <= 0 \|\| exitBuffer == 0)` | `if (ask <= 0 \|\| bid <= 0)` |
| `CopyEngine.cs` | 1059 | `if (ask <= 0 \|\| bid <= 0 \|\| exitBuffer == 0)` | `if (ask <= 0 \|\| bid <= 0)` |
| `CopyEngine.cs` | 1069 | `if (ask <= 0 \|\| bid <= 0 \|\| exitBuffer == 0)` | `if (ask <= 0 \|\| bid <= 0)` |
| `TradeCopierPanel.cs` | 808 | `if (ask <= 0 \|\| bid <= 0 \|\| _trimBuffer == 0)` | `if (ask <= 0 \|\| bid <= 0)` |
| `TradeCopierPanel.cs` | 836 | `if (ask <= 0 \|\| bid <= 0 \|\| _flattenBuffer == 0)` | `if (ask <= 0 \|\| bid <= 0)` |

**CYC impact**: Each guard removal lowers CYC by 1 (one fewer condition path in the compound OR).
All modified methods remain well below CYC 8.

| Method | CYC before | CYC after |
|---|---|---|
| `CopyEngine.Trim(Account,Instrument,int,double,double)` | 5 | 4 |
| `CopyEngine.Flatten(Account,Instrument,int,double,double)` | 5 | 4 |
| `CopyEngine.Trim(Instrument,int,double,double)` | 6 | 5 |
| `CopyEngine.Flatten(Instrument,int,double,double)` | ~5 | 4 |
| `TradeCopierPanel.OnTrimClick` | 4 | 3 |
| `TradeCopierPanel.OnFlattenClick` | 4 | 3 |

**Test impact**: `TrimLimit_FallsBackToMarket_WhenAskIsZero` (CopyEngineTests.cs line 1532).
Remove the `exitBuffer == 0` case (lines 1541-1542). Keep ask==0 and bid==0 cases intact.
Test name remains accurate for the remaining two covered paths.

---

### DW-B32-TRIM-ANCHOR-01 — Wrong Price Anchor in ComputeLimitPx

**Root cause**: The current formula anchors long exits to `bid` and short exits to `ask`,
placing orders passively away from the market (e.g. a long Sell Limit below bid will
fill immediately but a `bid - buffer` at buffer=1 dials FURTHER from ask, not from ask).
The Director-confirmed intent is:
- Long exit (Sell Limit): start at ask, buffer dials toward bid = `ask - buffer*tick`
- Short exit (BuyToCover): start at bid, buffer dials toward ask = `bid + buffer*tick`

**Location**: `CopyEngine.cs` lines 1045-1048.

**Current code (wrong)**:
```csharp
internal static double ComputeLimitPx(bool isLong, double ask, double bid, int exitBuffer, double tickSize)
    => isLong
        ? bid - exitBuffer * tickSize
        : ask + exitBuffer * tickSize;
```

**Fixed code**:
```csharp
// B32 fix (DW-B32-TRIM-ANCHOR-01): passive anchor corrected.
// Long exit (Sell Limit): ask - buffer*tick  -- starts at ask, buffer dials toward bid.
// Short exit (BuyToCover): bid + buffer*tick -- starts at bid, buffer dials toward ask.
// buffer=0 means post at exact ask (long) or exact bid (short).
internal static double ComputeLimitPx(bool isLong, double ask, double bid, int exitBuffer, double tickSize)
    => isLong
        ? ask - exitBuffer * tickSize
        : bid + exitBuffer * tickSize;
```

**Header comment replacement** (lines 1039-1044):
Remove the old B29 comment block referencing the wrong formula. Replace with:
```
// B32 fix (DW-B32-TRIM-ANCHOR-01): anchor corrected per Director.
// Long exit (Sell Limit): ask - buffer*tick  (passive from ask, dials toward bid).
// Short exit (BuyToCover): bid + buffer*tick (passive from bid, dials toward ask).
// buffer=0 = post at exact ask/bid. CYC=1: single ternary.
// internal static -- CopyEngineTests.cs calls CopyEngine.ComputeLimitPx(...) directly.
```

**CYC impact**: Zero — formula swap on CYC=1 method. CYC remains 1.

**Test impact** — 4 tests renamed and expected values corrected (CopyEngineTests.cs):

| Old test name | New test name | Old expected | New expected |
|---|---|---|---|
| `TrimLimit_Long_PlacesBelowBid` | `TrimLimit_Long_PlacesBelowAsk` | 4999.75 | 5000.00 |
| `TrimLimit_Short_PlacesAboveAsk` | `TrimLimit_Short_PlacesAboveBid` | 5000.50 | 5000.25 |
| `FlattenLimit_Long_PlacesBelowBid` | `FlattenLimit_Long_PlacesBelowAsk` | 4999.50 | 4999.75 |
| `FlattenLimit_Short_PlacesAboveAsk` | `FlattenLimit_Short_PlacesAboveBid` | 5000.75 | 5000.50 |

Test parameters (unchanged across all 4): `ask=5000.25, bid=5000.00, tickSize=0.25`
- Trim tests use `exitBuffer=1`; Flatten tests use `exitBuffer=2`.

---

### DW-B32-TRIM-CLOSE-01 — Raw Market Order Bypasses ATM Bracket

**Root cause**: `TrimOneAccount` and `FlattenOneAccount` issue raw `CreateOrder(Market)` calls
that are not ATM-aware. When the account holds an ATM position (Target1/Stop1 OCO bracket),
the raw market fill triggers the ATM's "Close operation timed out" race because the ATM engine
sees an unexpected fill on an order it did not submit.

**Architectural decision: WARN-AND-BLOCK (not Target nudge)**

**Why Target nudge was rejected**:

The codebase (DW-B32-07, CopyEngine.cs line 1351-1362) confirms that `acc.Change()` is
**silently rejected** by the NT8 ATM engine on ATM-managed Stop slot orders (Stop1, Stop2, etc.).
By exact structural symmetry in NT8's ATM architecture, Target slot orders (Target1, Target2)
are owned by the same ATM engine under the same OCO group and subject to the same silent
rejection. ATM slot orders are identifiable by:
- `order.FromEntrySignal == null` (ATM creates them, not PTT)
- `order.Name.StartsWith("Target")` with a digit at Name[6]

Attempting `acc.Change()` on Target1 to nudge its limit price would silently fail with no
exception and no price movement — the ATM re-applies its own managed price. This is the same
mechanism confirmed for Stop slots in B31's live test.

**Warn-and-block design**:

1. New `internal static bool IsAtmSlotName(string name)` helper:
   - Detects ATM-owned slot order names: "Stop1", "Stop2", ..., "Target1", "Target2", ...
   - Pattern: `StartsWith("Stop") && Name.Length > 4 && char.IsDigit(Name[4])` OR
     `StartsWith("Target") && Name.Length > 6 && char.IsDigit(Name[6])`
   - Returns false for all PTT-prefixed names and ordinary entry orders
   - `internal static` — directly testable without NT8 runtime
   - CYC=3: null/length guard (1), Stop pattern (2), Target pattern (3)

2. New `private bool IsAtmBracketActive(Account acc, Instrument instrument)` helper:
   - Scans `acc.Orders.ToList()` for an order where:
     - `order.Instrument == instrument`
     - `order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted`
     - `order.FromEntrySignal == null`
     - `IsAtmSlotName(order.Name)` is true
   - Returns true if any matching order found; false otherwise
   - CYC=4: foreach (1), instrument filter (2), state filter (3), name/signal filter (4)

3. `TrimOneAccount` modified:
   - Before the existing `FindPosition` + `CreateOrder` block, add:
     ```csharp
     if (IsAtmBracketActive(acc, instrument))
     {
         NinjaTrader.Code.Output.Process(
             "PTT-Trim: " + acc.Name + " -- ATM bracket active, use native Target/Close buttons",
             PrintTo.OutputTab1);
         StatusUpdate?.Invoke(acc.Name + ": PTT-Trim blocked -- ATM bracket active");
         return;
     }
     ```
   - CYC: 3 → 4 (one new branch). Remains ≤ 8. ✓

4. `FlattenOneAccount` modified — identical ATM guard inserted in the same position:
   - Output message: `"PTT-Flatten: " + acc.Name + " -- ATM bracket active, use native Target/Close buttons"`
   - CYC: 3 → 4. Remains ≤ 8. ✓

**Non-ATM fallback preserved**: When `IsAtmBracketActive` returns false (no ATM bracket),
the existing `CreateOrder(Market)` path executes unchanged. This is the safe case: raw market
order with no OCO to corrupt.

---

## Ticket 1 Detail — DW-B32-TRIM-MARKET-01 + DW-B32-TRIM-ANCHOR-01

**Spec requirements satisfied**: R-B32-04, R-B32-05

### File: `CopyEngine.cs`

**Change 1a — Guard removal, Trim 5-arg (line 949)**:
```
BEFORE: if (ask <= 0 || bid <= 0 || exitBuffer == 0) { Trim(leader, instrument); return; }
AFTER:  if (ask <= 0 || bid <= 0)                    { Trim(leader, instrument); return; }
```
Also update method CYC comment from 5 to 4.

**Change 1b — Guard removal, Flatten 5-arg (line 967)**:
```
BEFORE: if (ask <= 0 || bid <= 0 || exitBuffer == 0) { Flatten(leader, instrument); return; }
AFTER:  if (ask <= 0 || bid <= 0)                    { Flatten(leader, instrument); return; }
```

**Change 1c — Guard removal, Trim 4-arg (line 1059)**:
```
BEFORE: if (ask <= 0 || bid <= 0 || exitBuffer == 0) { Trim(instrument); return; }
AFTER:  if (ask <= 0 || bid <= 0)                    { Trim(instrument); return; }
```

**Change 1d — Guard removal, Flatten 4-arg (line 1069)**:
```
BEFORE: if (ask <= 0 || bid <= 0 || exitBuffer == 0) { Flatten(instrument); return; }
AFTER:  if (ask <= 0 || bid <= 0)                    { Flatten(instrument); return; }
```

**Change 2 — ComputeLimitPx formula + comment (lines 1039-1048)**:
Replace header comment block and formula ternary as specified in Defect 2 analysis above.
Exact diff:
```
BEFORE header comment:
    // B29 fix -- ComputeLimitPx: aggressive exit anchor.
    // Long exits (Sell Limit) post at bid - buffer (at/below market → fills immediately).
    // Short exits (BuyToCover) post at ask + buffer (at/above market → fills immediately).
    // DW-B29-01: original used ask+buffer for long, placing passive limit ABOVE market (never filled).
    // CYC=1: single ternary. No NT8 deps, no state, no nulls.
    // internal static -- CopyEngineTests.cs calls CopyEngine.ComputeLimitPx(...) directly.

AFTER header comment:
    // B32 fix (DW-B32-TRIM-ANCHOR-01): passive anchor corrected per Director.
    // Long exit (Sell Limit): ask - buffer*tick  (starts at ask; buffer dials toward bid).
    // Short exit (BuyToCover): bid + buffer*tick (starts at bid; buffer dials toward ask).
    // buffer=0 = post limit at exact ask (long) or exact bid (short).
    // CYC=1: single ternary. No NT8 deps, no state, no nulls.
    // internal static -- CopyEngineTests.cs calls CopyEngine.ComputeLimitPx(...) directly.

BEFORE formula:
    internal static double ComputeLimitPx(bool isLong, double ask, double bid, int exitBuffer, double tickSize)
        => isLong
            ? bid - exitBuffer * tickSize
            : ask + exitBuffer * tickSize;

AFTER formula:
    internal static double ComputeLimitPx(bool isLong, double ask, double bid, int exitBuffer, double tickSize)
        => isLong
            ? ask - exitBuffer * tickSize
            : bid + exitBuffer * tickSize;
```

### File: `TradeCopierPanel.cs`

**Change 3a — Guard removal, OnTrimClick (line 808)**:
```
BEFORE: if (ask <= 0 || bid <= 0 || _trimBuffer == 0)
AFTER:  if (ask <= 0 || bid <= 0)
```
Update CYC comment from 4 to 3 (if present on that method).

**Change 3b — Guard removal, OnFlattenClick (line 836)**:
```
BEFORE: if (ask <= 0 || bid <= 0 || _flattenBuffer == 0)
AFTER:  if (ask <= 0 || bid <= 0)
```

### File: `CopyEngineTests.cs`

**Change 4a — TrimLimit_FallsBackToMarket_WhenAskIsZero (line 1532)**:
Remove the `exitBuffer == 0` assertion case (lines 1541-1542):
```csharp
// REMOVE THESE TWO LINES:
var ex3 = Record.Exception(() => _engine.Trim(null, 0, 100.25, 99.75));
Assert.Null(ex3);
```
Also remove the `// exitBuffer=0 -> same guard` comment on the line before.
Keep ask==0 and bid==0 cases intact.

**Change 4b — 4 test renames + expected value corrections**:

Test 1 — rename `TrimLimit_Long_PlacesBelowBid` to `TrimLimit_Long_PlacesBelowAsk`:
```csharp
// Comment: "Long: ask - 1 tick = 5000.25 - 0.25 = 5000.00"
double px = CopyEngine.ComputeLimitPx(isLong: true, ask: 5000.25, bid: 5000.00, exitBuffer: 1, tickSize: 0.25);
Assert.Equal(5000.00, px, precision: 10);  // was 4999.75
```

Test 2 — rename `TrimLimit_Short_PlacesAboveAsk` to `TrimLimit_Short_PlacesAboveBid`:
```csharp
// Comment: "Short: bid + 1 tick = 5000.00 + 0.25 = 5000.25"
double px = CopyEngine.ComputeLimitPx(isLong: false, ask: 5000.25, bid: 5000.00, exitBuffer: 1, tickSize: 0.25);
Assert.Equal(5000.25, px, precision: 10);  // was 5000.50
```

Test 3 — rename `FlattenLimit_Long_PlacesBelowBid` to `FlattenLimit_Long_PlacesBelowAsk`:
```csharp
// Comment: "Long: ask - 2 ticks = 5000.25 - 0.50 = 4999.75"
double px = CopyEngine.ComputeLimitPx(isLong: true, ask: 5000.25, bid: 5000.00, exitBuffer: 2, tickSize: 0.25);
Assert.Equal(4999.75, px, precision: 10);  // was 4999.50
```

Test 4 — rename `FlattenLimit_Short_PlacesAboveAsk` to `FlattenLimit_Short_PlacesAboveBid`:
```csharp
// Comment: "Short: bid + 2 ticks = 5000.00 + 0.50 = 5000.50"
double px = CopyEngine.ComputeLimitPx(isLong: false, ask: 5000.25, bid: 5000.00, exitBuffer: 2, tickSize: 0.25);
Assert.Equal(5000.50, px, precision: 10);  // was 5000.75
```

---

## Ticket 2 Detail — DW-B32-TRIM-CLOSE-01

**Spec requirements satisfied**: R-B32-03

### File: `CopyEngine.cs`

**Method signatures**:

```csharp
// New helper 1 -- name-pattern predicate for ATM slot orders.
// CYC=3: null/length guard(1), Stop pattern(2), Target pattern(3).
// internal static -- directly testable without NT8 runtime.
// JS-002: bool return, no null. JS-001: no throw.
internal static bool IsAtmSlotName(string name)

// New helper 2 -- live order scan for ATM bracket presence.
// CYC=4: foreach(1), instrument filter(2), state filter(3), name+signal filter(4).
// JS-021: acc.Orders.ToList() snapshot -- same pattern as CancelOneAccount, FindFollowerBracketOrder.
// JS-002: bool return, no null. JS-001: no throw.
private bool IsAtmBracketActive(Account acc, Instrument instrument)
```

**IsAtmSlotName implementation**:
```csharp
internal static bool IsAtmSlotName(string name)
{
    if (name == null || name.Length < 5)
        return false;
    if (name.StartsWith("Stop") && name.Length > 4 && char.IsDigit(name[4]))
        return true;
    if (name.StartsWith("Target") && name.Length > 6 && char.IsDigit(name[6]))
        return true;
    return false;
}
```

**IsAtmBracketActive implementation**:
```csharp
private bool IsAtmBracketActive(Account acc, Instrument instrument)
{
    foreach (var order in acc.Orders.ToList())
    {
        if (order.Instrument != instrument)
            continue;
        if (order.OrderState != OrderState.Working &&
            order.OrderState != OrderState.Accepted)
            continue;
        if (order.FromEntrySignal == null && IsAtmSlotName(order.Name))
            return true;
    }
    return false;
}
```

**TrimOneAccount modification** — insert ATM guard after method signature, before existing pos check:
```csharp
private void TrimOneAccount(Account acc, Instrument instrument)
{
    // DW-B32-TRIM-CLOSE-01: block raw market order when ATM bracket is active.
    // acc.Change() is silently rejected on ATM-owned slot orders (DW-B32-07 confirmed).
    // Target nudge path rejected for same reason. User must use native Target/Close buttons.
    if (IsAtmBracketActive(acc, instrument))
    {
        NinjaTrader.Code.Output.Process(
            "PTT-Trim: " + acc.Name + " -- ATM bracket active, use native Target/Close buttons",
            PrintTo.OutputTab1);
        StatusUpdate?.Invoke(acc.Name + ": PTT-Trim blocked -- ATM bracket active");
        return;
    }
    // ... existing pos scan and CreateOrder(Market) unchanged ...
```

**FlattenOneAccount modification** — identical ATM guard:
```csharp
private void FlattenOneAccount(Account acc, Instrument instrument)
{
    // DW-B32-TRIM-CLOSE-01: block raw market order when ATM bracket is active.
    if (IsAtmBracketActive(acc, instrument))
    {
        NinjaTrader.Code.Output.Process(
            "PTT-Flatten: " + acc.Name + " -- ATM bracket active, use native Target/Close buttons",
            PrintTo.OutputTab1);
        StatusUpdate?.Invoke(acc.Name + ": PTT-Flatten blocked -- ATM bracket active");
        return;
    }
    // ... existing pos scan and CreateOrder(Market) unchanged ...
```

**CYC summary for Ticket 2 new/modified methods**:

| Method | CYC |
|---|---|
| `IsAtmSlotName` | 3 |
| `IsAtmBracketActive` | 4 |
| `TrimOneAccount` | 4 (was 3) |
| `FlattenOneAccount` | 4 (was 3) |

All ≤ 8. ✓

### File: `CopyEngineTests.cs`

Four new `[Fact]` tests for `IsAtmSlotName`:

```csharp
// T-B32-T2-01: "Target1" is recognized as an ATM slot name.
[Fact]
public void IsAtmSlotName_DetectsTarget1()
{
    Assert.True(CopyEngine.IsAtmSlotName("Target1"));
    Assert.True(CopyEngine.IsAtmSlotName("Target2"));
    Assert.True(CopyEngine.IsAtmSlotName("Target9"));
}

// T-B32-T2-02: "Stop1" and "Stop2" are recognized as ATM slot names.
[Fact]
public void IsAtmSlotName_DetectsStop1()
{
    Assert.True(CopyEngine.IsAtmSlotName("Stop1"));
    Assert.True(CopyEngine.IsAtmSlotName("Stop2"));
}

// T-B32-T2-03: "PTT-Trim" and other PTT signal names are NOT ATM slot names.
[Fact]
public void IsAtmSlotName_RejectsPttSignalNames()
{
    Assert.False(CopyEngine.IsAtmSlotName("PTT-Trim"));
    Assert.False(CopyEngine.IsAtmSlotName("PTT-Flatten"));
    Assert.False(CopyEngine.IsAtmSlotName("PTT-Copy"));
    Assert.False(CopyEngine.IsAtmSlotName(null));
}

// T-B32-T2-04: "Target" without a digit suffix is NOT an ATM slot name (avoids false positives).
[Fact]
public void IsAtmSlotName_RejectsTargetWithoutDigit()
{
    Assert.False(CopyEngine.IsAtmSlotName("Target"));
    Assert.False(CopyEngine.IsAtmSlotName("Stop"));
    Assert.False(CopyEngine.IsAtmSlotName("TargetEntry"));
}
```

---

## NT8 Constraints Applicable

| Rule | Applicability |
|---|---|
| NT8-007 | No new CreateOrder calls. Existing calls already compliant (arg 12 = (CustomOrder)null). |
| NT8-013 | No DateTime.Now. Existing calls use DateTime.MaxValue. |
| NT8-014 | No new signal names. Existing PTT-Trim/PTT-Flatten/PTT-TrimLimit/PTT-FlattenLimit used. |
| NT8-018 | No lock(). acc.Orders.ToList() snapshot is the established lock-free pattern. |
| NT8-019 | No async void. All methods synchronous void or static bool. |
| NT8-029 | ComputeLimitPx output is consumed by TrimOneAccountLimit/FlattenOneAccountLimit which already apply tick rounding at line 1150/1183. No tick alignment regression. |
| NT8-031 | OrderState.Working and OrderState.Accepted used in IsAtmBracketActive. Both confirmed valid in NT8. |
| NT8-043 | No null-conditional compound assignments introduced. |

---

## JS Rules Applicable

| Rule | Verdict |
|---|---|
| JS-001 (no throw in hot path) | PASS — all error paths use StatusUpdate + return |
| JS-002 (no return null) | PASS — new helpers return bool |
| JS-021 (no lock) | PASS — ToList() snapshot pattern only |
| JS-033 (no async void) | PASS — zero async methods introduced |

---

## 7-Scan Checklist Template

*(To be carried into every ticket unchanged)*

1. **lock() scan**: `grep -r "lock(" src/PropTraderTools/ --include="*.cs"` → 0 matches in new/modified code
2. **async void scan**: `grep -rn "async void " src/PropTraderTools/ --include="*.cs"` → 0 matches in new/modified code
3. **return null scan**: `grep -rn "return null;" src/PropTraderTools/ --include="*.cs"` → 0 new occurrences (all pre-existing)
4. **NT8 compiler rules scan**: manual check per `docs/standards/NT8_COMPILER_RULES.md` — no new banned patterns (init setters, record types, volatile double, Immutable collections, async void, DateTime.Now, missing PTT- prefix, hex colors)
5. **CYC scan**: all new/modified methods ≤ 8 (verified: IsAtmSlotName=3, IsAtmBracketActive=4, TrimOneAccount=4, FlattenOneAccount=4, all guards =3-5)
6. **Test scan**: `dotnet test` in `c:\WSGTA\universal-or-strategy\` → all pass
7. **ASCII scan**: no non-ASCII characters in new string literals or identifiers

---

## Component Summary

| Component | File | Change type | Risk |
|---|---|---|---|
| Guard removal × 4 | `CopyEngine.cs` lines 949, 967, 1059, 1069 | Delete compound OR clause | Low |
| Guard removal × 2 | `TradeCopierPanel.cs` lines 808, 836 | Delete compound OR clause | Low |
| `ComputeLimitPx` formula | `CopyEngine.cs` lines 1045-1048 | 2-line formula swap | Low |
| Header comment | `CopyEngine.cs` lines 1039-1044 | Comment update | None |
| `IsAtmSlotName` | `CopyEngine.cs` (new) | New internal static bool | Low |
| `IsAtmBracketActive` | `CopyEngine.cs` (new) | New private bool | Medium |
| `TrimOneAccount` | `CopyEngine.cs` | Insert 6-line ATM guard block | Medium |
| `FlattenOneAccount` | `CopyEngine.cs` | Insert 6-line ATM guard block | Medium |
| Test: remove exitBuffer==0 case | `CopyEngineTests.cs` line 1541-1542 | Delete 2 lines | Low |
| Tests: 4 renames + value corrections | `CopyEngineTests.cs` | Rename + Assert.Equal value | Low |
| Tests: 4 new [Fact] | `CopyEngineTests.cs` | New tests for IsAtmSlotName | None |
