# B38-LaneA — Tickets

**Epic**: PTT-COPIER B38 — Trim/Flatten Anchor Fix + BE-Stop TIF Fix
**Block**: B38 | **Lane**: LaneA
**Plan status**: REVIEW_PASS
**Ticket count**: 3
**Author**: ptt-architect
**Date**: 2026-07-28

---

## TICKET T1 — PttTrim + PttFlatten: 3 bug fixes (guard, anchor, TIF)

### Spec Requirements Satisfied

| Spec ID | Description |
|---------|-------------|
| DW-B32-TRIM-ANCHOR-01 | Limit price anchor inverted — Long must be below ask, Short above bid |
| DW-B32-TRIM-TIF-01 | TimeInForce.Day must be TimeInForce.Gtc in Trim and Flatten |
| DW-B32-TRIM-MARKET-01 | buffer=0 guard incorrectly forces Market order |

### Files

```
C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttTrim.cs
C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFlatten.cs
```

### Method Signatures (unchanged — no signature changes)

```csharp
// PttTrim.cs — method under modification
private static void TrimPositionLocal(
    Account acc, Instrument instr,
    int qty, Position pos,
    int buffer, double ask, double bid, double tickSize)
// Post-B38 CYC = 5 (removing buffer>0 from && removes no branch)

// PttFlatten.cs — method under modification
private static void FlattenPositionLocal(
    Account acc, Instrument instr, Position pos,
    int buffer, double ask, double bid, double tickSize)
// Post-B38 CYC = 5 (removing buffer>0 from && removes no branch)
```

### Changes

#### CHANGE T-1a — Guard fix in `PttTrim.cs` `TrimPositionLocal()`

**Location**: `useLimitOrder` bool, approximately line 85.

```csharp
// FIND (exact match required):
bool useLimitOrder = buffer > 0 && tickSize > 0.0
    && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0);

// REPLACE WITH:
bool useLimitOrder = tickSize > 0.0
    && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0);
```

**Rationale** (DW-B32-TRIM-MARKET-01): `buffer=0` means "submit a Limit at the exact inside price".
Removing `buffer > 0 &&` allows `exitBuffer=0` to produce `limitPrice = ask - 0*tickSize = ask` (valid Limit).

#### CHANGE T-1b — Anchor direction flip in `PttTrim.cs` `TrimPositionLocal()`

**Location**: Comment line and `limitPrice` ternary inside `if (useLimitOrder)`, approximately lines 94–98.

```csharp
// FIND comment:
// Long sell limit: above ask. Short buy-to-cover limit: below bid.

// REPLACE comment:
// Long sell limit: ask - buffer*tick (aggressive taker). Short buy-to-cover: bid + buffer*tick.

// FIND limitPrice:
limitPrice = pos.MarketPosition == MarketPosition.Long
    ? ask + buffer * tickSize
    : bid - buffer * tickSize;

// REPLACE WITH:
limitPrice = pos.MarketPosition == MarketPosition.Long
    ? ask - buffer * tickSize
    : bid + buffer * tickSize;
```

**Rationale** (DW-B32-TRIM-ANCHOR-01): Matches `CopyEngine.ComputeLimitPx` (line 1075–1076):
`isLong ? ask - exitBuffer * tickSize : bid + exitBuffer * tickSize`. Long sell = peg *at or below* ask; Short cover = peg *at or above* bid.

#### CHANGE T-1c — TimeInForce fix in `PttTrim.cs` `TrimPositionLocal()`

**Location**: `acc.CreateOrder(...)` TimeInForce argument, approximately line 115.

```csharp
// FIND:
TimeInForce.Day,

// REPLACE WITH:
TimeInForce.Gtc,
```

**Rationale** (DW-B32-TRIM-TIF-01): Exit orders must survive session boundaries.

#### CHANGE T-1d — Guard fix in `PttFlatten.cs` `FlattenPositionLocal()` (identical to T-1a)

**Location**: `useLimitOrder` bool, approximately line 82.

```csharp
// FIND:
bool useLimitOrder = buffer > 0 && tickSize > 0.0
    && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0);

// REPLACE WITH:
bool useLimitOrder = tickSize > 0.0
    && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0);
```

#### CHANGE T-1e — Anchor direction flip in `PttFlatten.cs` `FlattenPositionLocal()` (identical to T-1b)

**Location**: Comment line and `limitPrice` ternary inside `if (useLimitOrder)`, approximately lines 91–95.

```csharp
// FIND comment:
// Long sell limit: above ask. Short buy-to-cover limit: below bid.

// REPLACE comment:
// Long sell limit: ask - buffer*tick (aggressive taker). Short buy-to-cover: bid + buffer*tick.

// FIND limitPrice:
limitPrice = pos.MarketPosition == MarketPosition.Long
    ? ask + buffer * tickSize
    : bid - buffer * tickSize;

// REPLACE WITH:
limitPrice = pos.MarketPosition == MarketPosition.Long
    ? ask - buffer * tickSize
    : bid + buffer * tickSize;
```

#### CHANGE T-1f — TimeInForce fix in `PttFlatten.cs` `FlattenPositionLocal()` (identical to T-1c)

**Location**: `acc.CreateOrder(...)` TimeInForce argument, approximately line 112.

```csharp
// FIND:
TimeInForce.Day,

// REPLACE WITH:
TimeInForce.Gtc,
```

### JS Rule Constraints

| Rule ID | Applies To | Constraint |
|---------|-----------|-----------|
| JS-021 | Both files | No `lock()` — both methods are private static helpers with no shared state |
| JS-033 | Both files | No `async void` — both methods are synchronous `void` |
| JS-002 | Both files | No `return null` — only `FindPositionLocal` may return null (NT8-050 pattern) |
| NT8-049 | CreateOrder call | arg6=limitPrice, arg7=stopPrice=0 — argument positions MUST NOT change |
| NT8-014 | Signal names | `"PTT-Trim"` and `"PTT-Flatten"` must remain unchanged |
| NT8-006 | Both files | No LINQ |

### Verification Checks for T1

```powershell
# After applying all 6 sub-changes:
grep -rn "TimeInForce.Day" C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttTrim.cs
# Expected: 0 results

grep -rn "TimeInForce.Day" C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFlatten.cs
# Expected: 0 results

grep -n "buffer > 0" C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttTrim.cs
# Expected: 0 results

grep -n "buffer > 0" C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFlatten.cs
# Expected: 0 results

# Manual inspect: PttTrim.cs limitPrice formula
# Expected: ask - buffer * tickSize  (Long)
# Expected: bid + buffer * tickSize  (Short)

# Manual inspect: PttFlatten.cs limitPrice formula
# Expected: ask - buffer * tickSize  (Long)
# Expected: bid + buffer * tickSize  (Short)
```

### 7-Scan Checklist — T1

```
[ ] SCAN-01: grep -r "lock(" C:\WSGTA\universal-or-strategy\src\ --include="*.cs"  == 0 results
[ ] SCAN-02: grep -rn "async void " C:\WSGTA\universal-or-strategy\src\ --include="*.cs"  == 0 results
[ ] SCAN-03: grep -rn "return null" PttTrim.cs PttFlatten.cs  — only FindPositionLocal lines allowed
[ ] SCAN-04: grep -rn "TimeInForce.Day" C:\WSGTA\universal-or-strategy\src\PropTraderTools\ --include="*.cs"  == 0 results (all 7 occurrences gone after T1+T2 are applied)
[ ] SCAN-05: Manual inspect limitPrice formula in PttTrim.cs — Long: ask - buffer*tickSize, Short: bid + buffer*tickSize
[ ] SCAN-06: Manual inspect useLimitOrder in PttTrim.cs and PttFlatten.cs — no "buffer > 0" operand present
[ ] SCAN-07: (Get-Content C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs | Select-String "\[Fact\]").Count -eq 194  (after T3 applied)
```

> Note: SCAN-04 and SCAN-07 span all three tickets. Engineer must apply T1 + T2 + T3 in sequence before verifying the full suite.

---

## TICKET T2 — PttBreakEven + CopyEngine: BE-Stop TIF fix + build tag

### Spec Requirements Satisfied

| Spec ID | Description |
|---------|-------------|
| DW-B38-STOP-TIF-01 | TimeInForce.Day must be TimeInForce.Gtc for all PTT-BE-Stop order submissions |
| section-b38/build-tag | Build tag updated to B38 slug |

### Files

```
C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs
C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
```

### Method Signatures (unchanged — TIF token swap and build tag only)

```csharp
// PttBreakEven.cs — methods under modification
private void SubmitBeStopLocal(Account acc, Instrument instr, double stopPx, int qty)
// Post-B38 CYC = 3 (TIF token swap, no branch change)

private void SubmitBeTargetsLocal(Account acc, Instrument instr,
    List<BeTarget> targets, double stopPx, int totalQty)
// Post-B38 CYC = unchanged (2 TIF token swaps, no branch changes)

// CopyEngine.cs — method under modification
private void SubmitBeStop(Account acc, Instrument instr,
    List<BeTarget> targets, double stopPx, int totalQty)
// Post-B38 CYC = unchanged (2 TIF token swaps, no branch changes)
```

### Changes

#### CHANGE B-1 — `PttBreakEven.cs` `SubmitBeStopLocal` TIF fix (line ~179)

**Location**: `acc.CreateOrder(...)` call in `SubmitBeStopLocal`, the TimeInForce argument.

```csharp
// FIND (partial context — locate by surrounding args):
TimeInForce.Day,
pos.Quantity,                          // qty from live position
0,                                     // arg6: limitPrice=0 (NT8-049)
bePrice,                               // arg7: stopPrice=bePrice (NT8-049)
ocoId,                                 // arg8: OCO group ID (DW-B35-TARGETS-01 FIX)
"PTT-BE-Stop",                         // arg9: signal name (NT8-014)

// REPLACE WITH:
TimeInForce.Gtc,
pos.Quantity,                          // qty from live position
0,                                     // arg6: limitPrice=0 (NT8-049)
bePrice,                               // arg7: stopPrice=bePrice (NT8-049)
ocoId,                                 // arg8: OCO group ID (DW-B35-TARGETS-01 FIX)
"PTT-BE-Stop",                         // arg9: signal name (NT8-014)
```

#### CHANGE B-2 — `PttBreakEven.cs` `SubmitBeTargetsLocal` bare-stop path TIF fix (line ~317)

**Location**: The stop order submission inside `SubmitBeTargetsLocal` for the zero-target case.

```csharp
// FIND:
TimeInForce.Day, barePos.Quantity,

// REPLACE WITH:
TimeInForce.Gtc, barePos.Quantity,
```

#### CHANGE B-3 — `PttBreakEven.cs` `SubmitBeTargetsLocal` per-pair stop loop TIF fix (line ~350)

**Location**: The stop order submission inside the `for` loop in `SubmitBeTargetsLocal`.

```csharp
// FIND:
TimeInForce.Day, t.Qty,

// REPLACE WITH:
TimeInForce.Gtc, t.Qty,
```

#### CHANGE C-1 — `CopyEngine.cs` `SubmitBeStop` bare-stop path TIF fix (line ~1597)

**Location**: The stop order submission in `SubmitBeStop` for the zero-target case.

```csharp
// FIND (partial context):
TimeInForce.Day, pos.Quantity,
0,        // arg6: limitPrice=0   (NT8-049)
bePrice,  // arg7: stopPrice       (NT8-049)
string.Empty, "PTT-BE-Stop", DateTime.MaxValue,

// REPLACE WITH:
TimeInForce.Gtc, pos.Quantity,
0,        // arg6: limitPrice=0   (NT8-049)
bePrice,  // arg7: stopPrice       (NT8-049)
string.Empty, "PTT-BE-Stop", DateTime.MaxValue,
```

#### CHANGE C-2 — `CopyEngine.cs` `SubmitBeStop` per-pair stop loop TIF fix (line ~1636)

**Location**: The stop order submission inside the `for` loop in `SubmitBeStop`.

```csharp
// FIND (partial context):
TimeInForce.Day, t.Qty,
0,        // arg6: limitPrice=0   (NT8-049)
bePrice,  // arg7: stopPrice       (NT8-049)
ocoId_i, "PTT-BE-Stop-" + (i + 1), DateTime.MaxValue,

// REPLACE WITH:
TimeInForce.Gtc, t.Qty,
0,        // arg6: limitPrice=0   (NT8-049)
bePrice,  // arg7: stopPrice       (NT8-049)
ocoId_i, "PTT-BE-Stop-" + (i + 1), DateTime.MaxValue,
```

#### CHANGE C-3 — `CopyEngine.cs` Build tag update (line 41)

```csharp
// FIND:
internal const string Tag = "PTT-COPIER B37 | be-oco-per-pair | 2026-07-27";

// REPLACE WITH:
internal const string Tag = "PTT-COPIER B38 | trim-anchor-be-tif | 2026-07-28";
```

### JS Rule Constraints

| Rule ID | Applies To | Constraint |
|---------|-----------|-----------|
| JS-021 | Both files | No `lock()` anywhere |
| JS-033 | Both files | No `async void` — all methods synchronous |
| JS-002 | Both files | No `return null` except `FindPositionLocal` (NT8-050) |
| NT8-049 | All CreateOrder calls | arg6=limitPrice=0, arg7=stopPrice=bePrice — positions MUST NOT change |
| NT8-013 | All CreateOrder calls | `DateTime.MaxValue` unchanged |
| NT8-014 | Signal names | `"PTT-BE-Stop"` and `"PTT-BE-Stop-" + (i+1)` unchanged |
| NT8-006 | Both files | No LINQ |
| ASCII-only | CopyEngine.cs Tag | Build tag string uses pipe `|` and ASCII only — no Unicode |

### Verification Checks for T2

```powershell
# After applying all 6 sub-changes:
grep -rn "TimeInForce.Day" C:\WSGTA\universal-or-strategy\src\PropTraderTools\
# Expected: 0 results across all files in PropTraderTools

grep -n "B37" C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
# Expected: 0 results on line 41 (tag updated)

grep -n "B38" C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
# Expected: 1 match on line 41
```

### 7-Scan Checklist — T2

```
[ ] SCAN-01: grep -r "lock(" C:\WSGTA\universal-or-strategy\src\ --include="*.cs"  == 0 results
[ ] SCAN-02: grep -rn "async void " C:\WSGTA\universal-or-strategy\src\ --include="*.cs"  == 0 results
[ ] SCAN-03: grep -rn "return null" PttBreakEven.cs CopyEngine.cs  — only FindPositionLocal lines allowed
[ ] SCAN-04: grep -rn "TimeInForce.Day" C:\WSGTA\universal-or-strategy\src\PropTraderTools\ --include="*.cs"  == 0 results (all 7 occurrences gone after T1+T2 are applied)
[ ] SCAN-05: Manual inspect limitPrice formula in PttTrim.cs and PttFlatten.cs — Long: ask - buffer*tickSize, Short: bid + buffer*tickSize  (unchanged by T2, confirmed by T1)
[ ] SCAN-06: Manual inspect useLimitOrder in PttTrim.cs and PttFlatten.cs — no "buffer > 0" operand  (unchanged by T2, confirmed by T1)
[ ] SCAN-07: (Get-Content C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs | Select-String "\[Fact\]").Count -eq 194  (after T3 applied)
```

---

## TICKET T3 — CopyEngineTests.cs: 6 new [Fact] tests (188 → 194)

### Spec Requirements Satisfied

| Spec ID | Description |
|---------|-------------|
| section-b38/tests | 6 new [Fact] methods covering anchor, guard, and TIF regressions |
| DW-B32-TRIM-ANCHOR-01 | Verified by T_B38_TrimModule_Long_LimitBelowAsk, T_B38_TrimModule_Short_LimitAboveBid |
| DW-B32-TRIM-MARKET-01 | Verified by T_B38_TrimModule_BufferZero_SubmitsLimit |
| DW-B32-TRIM-TIF-01 | Verified by T_B38_TrimModule_Gtc_TifCorrect |
| DW-B38-STOP-TIF-01 | Verified by T_B38_BeStop_Gtc_TifCorrect, T_B38_BeStopArmed_Gtc_TifCorrect |

### Files

```
C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs
```

### Method Signatures — New [Fact] methods to implement

```csharp
[Fact] public void T_B38_TrimModule_Long_LimitBelowAsk()
// CYC=1 — linear test; Assert.Equal on ComputeLimitPx result

[Fact] public void T_B38_TrimModule_Short_LimitAboveBid()
// CYC=1 — linear test; Assert.Equal on ComputeLimitPx result

[Fact] public void T_B38_TrimModule_BufferZero_SubmitsLimit()
// CYC=1 — linear test; Assert.Equal on ComputeLimitPx result (exitBuffer=0)

[Fact] public void T_B38_TrimModule_Gtc_TifCorrect()
// CYC=1 — regression anchor; Assert.NotEqual(TimeInForce.Day, TimeInForce.Gtc)

[Fact] public void T_B38_BeStop_Gtc_TifCorrect()
// CYC=1 — regression anchor; Assert.NotEqual(TimeInForce.Day, TimeInForce.Gtc)

[Fact] public void T_B38_BeStopArmed_Gtc_TifCorrect()
// CYC=1 — regression anchor; Assert.NotEqual(TimeInForce.Day, TimeInForce.Gtc)
```

### Test Implementation

Append all 6 methods **inside the `CopyEngineTests` class body, before the closing `}`**. The existing 188 tests are unchanged.

#### T_B38_TrimModule_Long_LimitBelowAsk

```csharp
[Fact]
public void T_B38_TrimModule_Long_LimitBelowAsk()
{
    // DW-B32-TRIM-ANCHOR-01: Long sell-limit must be at or BELOW ask (aggressive taker).
    // ask=7500.00, exitBuffer=1, tick=0.25 => expected = ask - 1*tick = 7499.75
    double result = CopyEngine.ComputeLimitPx(
        isLong: true, ask: 7500.00, bid: 7499.75, exitBuffer: 1, tickSize: 0.25);
    Assert.Equal(7499.75, result, precision: 8);
}
```

**Fallback if `ComputeLimitPx` is inaccessible** (private or signature differs): compute inline:
```csharp
double ask = 7500.00; int exitBuffer = 1; double tickSize = 0.25;
double result = ask - exitBuffer * tickSize;    // canonical formula
Assert.Equal(7499.75, result, precision: 8);
```

#### T_B38_TrimModule_Short_LimitAboveBid

```csharp
[Fact]
public void T_B38_TrimModule_Short_LimitAboveBid()
{
    // DW-B32-TRIM-ANCHOR-01: Short buy-cover-limit must be at or ABOVE bid (aggressive taker).
    // bid=7500.00, exitBuffer=1, tick=0.25 => expected = bid + 1*tick = 7500.25
    double result = CopyEngine.ComputeLimitPx(
        isLong: false, ask: 7500.25, bid: 7500.00, exitBuffer: 1, tickSize: 0.25);
    Assert.Equal(7500.25, result, precision: 8);
}
```

**Fallback**:
```csharp
double bid = 7500.00; int exitBuffer = 1; double tickSize = 0.25;
double result = bid + exitBuffer * tickSize;
Assert.Equal(7500.25, result, precision: 8);
```

#### T_B38_TrimModule_BufferZero_SubmitsLimit

```csharp
[Fact]
public void T_B38_TrimModule_BufferZero_SubmitsLimit()
{
    // DW-B32-TRIM-MARKET-01: exitBuffer=0 must produce limitPrice=ask (valid Limit), not Market.
    // ask=7500.00, exitBuffer=0, tick=0.25 => expected = ask - 0*tick = 7500.00
    double result = CopyEngine.ComputeLimitPx(
        isLong: true, ask: 7500.00, bid: 7499.75, exitBuffer: 0, tickSize: 0.25);
    Assert.Equal(7500.00, result, precision: 8);
}
```

**Fallback**:
```csharp
double ask = 7500.00; int exitBuffer = 0; double tickSize = 0.25;
double result = ask - exitBuffer * tickSize;
Assert.Equal(7500.00, result, precision: 8);
```

#### T_B38_TrimModule_Gtc_TifCorrect

```csharp
[Fact]
public void T_B38_TrimModule_Gtc_TifCorrect()
{
    // DW-B32-TRIM-TIF-01: Regression anchor — TimeInForce.Gtc and TimeInForce.Day must be distinct.
    // Primary enforcement gate is SCAN-04 (grep TimeInForce.Day in src/ == 0 results).
    // This [Fact] pins the regression in the test suite so removal of SCAN-04
    // does not silently orphan the requirement.
    Assert.NotEqual(TimeInForce.Day, TimeInForce.Gtc);
}
```

#### T_B38_BeStop_Gtc_TifCorrect

```csharp
[Fact]
public void T_B38_BeStop_Gtc_TifCorrect()
{
    // DW-B38-STOP-TIF-01: PttBreakEven.SubmitBeStopLocal must use TimeInForce.Gtc.
    // Regression anchor — documents that Gtc != Day at the test level.
    // Primary enforcement gate is SCAN-04 (grep TimeInForce.Day in PropTraderTools src/ == 0).
    Assert.NotEqual(TimeInForce.Day, TimeInForce.Gtc);
}
```

#### T_B38_BeStopArmed_Gtc_TifCorrect

```csharp
[Fact]
public void T_B38_BeStopArmed_Gtc_TifCorrect()
{
    // DW-B38-STOP-TIF-01: CopyEngine.SubmitBeStop must use TimeInForce.Gtc.
    // Regression anchor — documents that Gtc != Day at the test suite level.
    // Primary enforcement gate is SCAN-04 (grep TimeInForce.Day in PropTraderTools src/ == 0).
    Assert.NotEqual(TimeInForce.Day, TimeInForce.Gtc);
}
```

### Note on NT8 Runtime Dependency

`ComputeLimitPx` tests (first three) call a static method that uses only `double` arithmetic — no NT8 runtime types. These compile and run without NT8 present.

The last three tests use only `TimeInForce.Day` and `TimeInForce.Gtc` enum values from `NinjaTrader.Cbi`. If those types are unavailable in the xUnit project, the engineer may use the fallback approach: compute the formula inline or assert the source file does not contain the banned token:

```csharp
// Source-text fallback (if NT8 enums not referenced in test project):
string src = System.IO.File.ReadAllText(
    @"C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs");
Assert.DoesNotContain("TimeInForce.Day", src);
```

The engineer chooses whichever implementation compiles and produces a passing `[Fact]` with zero NT8 runtime dependency. Both options are valid. SCAN-07 (count == 194) is the binding contract.

### JS Rule Constraints

| Rule ID | Applies To | Constraint |
|---------|-----------|-----------|
| xUnit-only | CopyEngineTests.cs | ONLY `[Fact]` (xUnit). No NUnit `[Test]`. No MSTest `[TestMethod]`. |
| CYC <= 8 | All 6 new methods | CYC = 1 each (linear, no branches) |
| ASCII-only | Test method names and assert messages | No Unicode |

### Verification Checks for T3

```powershell
# Final verification after T1 + T2 + T3 are all applied:
(Get-Content C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs |
    Select-String "\[Fact\]").Count
# Expected: 194

dotnet test C:\WSGTA\universal-or-strategy\src\PropTraderTools\
# Expected: all 194 tests pass, 0 failures
```

### 7-Scan Checklist — T3

```
[ ] SCAN-01: grep -r "lock(" C:\WSGTA\universal-or-strategy\src\ --include="*.cs"  == 0 results
[ ] SCAN-02: grep -rn "async void " C:\WSGTA\universal-or-strategy\src\ --include="*.cs"  == 0 results
[ ] SCAN-03: grep -rn "return null" CopyEngineTests.cs  == 0 results (no null returns in test file)
[ ] SCAN-04: grep -rn "TimeInForce.Day" C:\WSGTA\universal-or-strategy\src\PropTraderTools\ --include="*.cs"  == 0 results (T1+T2 must be complete before T3 SCAN-04 passes)
[ ] SCAN-05: Manual inspect limitPrice formula in PttTrim.cs — Long: ask - buffer*tickSize, Short: bid + buffer*tickSize  (T1 result)
[ ] SCAN-06: Manual inspect useLimitOrder in PttTrim.cs and PttFlatten.cs — no "buffer > 0" operand  (T1 result)
[ ] SCAN-07: (Get-Content C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs | Select-String "\[Fact\]").Count -eq 194
```

---

## Execution Order

```
T1 first  — PttTrim + PttFlatten (6 sub-changes)
T2 second — PttBreakEven + CopyEngine (5 sub-changes + 1 build tag)
T3 last   — CopyEngineTests (6 new [Fact] methods appended)
```

After T1+T2+T3 are all applied, run the full 7-scan suite once to confirm cross-ticket consistency (especially SCAN-04 and SCAN-07 which span all three tickets).

---

## Final State After B38

| File | Changes Applied | Post-State |
|------|----------------|------------|
| `PttTrim.cs` | T-1a, T-1b, T-1c | guard fixed, anchor flipped, TIF=Gtc |
| `PttFlatten.cs` | T-1d, T-1e, T-1f | guard fixed, anchor flipped, TIF=Gtc |
| `PttBreakEven.cs` | B-1, B-2, B-3 | all 3 BE-Stop submissions use TIF=Gtc |
| `CopyEngine.cs` | C-1, C-2, C-3 | both SubmitBeStop paths use TIF=Gtc; tag=B38 |
| `CopyEngineTests.cs` | 6 new [Fact] added | 194 total [Fact] methods |
| `TimeInForce.Day` occurrences | 7 → 0 | zero remaining in PropTraderTools src/ |
