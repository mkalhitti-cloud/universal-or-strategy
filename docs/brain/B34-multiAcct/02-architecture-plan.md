# B34 Architecture Plan — Multi-Account BE Fixes + Buffer Extension
<!-- PTT-COPIER B34 | be-multiAccount-fixes | 2026-07-27 -->

## Status: AWAITING REVIEW

---

## 1. Executive Summary

Block B34 closes five deferred-work items carried from B33:

| DW ID | Severity | Description |
|---|---|---|
| DW-B33-05 | P0 | `isLong` derived from `leaderPos` OUTSIDE foreach — short followers get wrong `OrderAction` |
| DW-B33-06 | P0 | `bePrice` = leader's `AveragePrice`, no sign flip, no buffer — wrong stop for every follower |
| DW-B33-07 | P0 | `CancelStaleBracketsLocal` called once before loop for leader only — followers retain stale brackets |
| DW-B33-02 | P1 | Buffer tick values (`BeBuffer`, `TrimBuffer`, `FlatBuffer`) not present on `IPttHostContext` |
| DW-B33-04 | P1 | `PttTrim`/`PttFlatten` use `OrderType.Market` regardless of buffer setting |

**Result:** After B34, every account in `ctx.AllAccounts` receives a BE stop at its own entry price, adjusted by the correct direction-aware buffer, after its own stale brackets are cancelled. Trim and Flatten use Limit orders when their respective buffers are non-zero, anchored to the live Ask/Bid price from `IPttHostContext`.

---

## 2. Source Baseline (Orchestrator-Confirmed)

| File | State |
|---|---|
| `CopyEngine.cs` | tag = `"PTT-COPIER B33 \| modular-independence \| 2026-07-25"` |
| `Features/PttBreakEven.cs` | B33 modular — bugs DW-B33-05/06/07 confirmed present |
| `Core/PttContracts.cs` | `IPttHostContext` has 3 props; buffer props absent |
| `TradeCopierPanel.cs` | Has `_beBuffer`, `_trimBuffer`, `_flattenBuffer` (all `int`); has `GetAsk()`/`GetBid()` (private) |
| `Features/PttTrim.cs` | B33 modular — uses `OrderType.Market` unconditionally |
| `Features/PttFlatten.cs` | B33 modular — uses `OrderType.Market` unconditionally |
| [Fact] count | 171 confirmed |

---

## 3. Critical Dependency Order

> **ENGINEER IMPLEMENTATION ORDER: B34-02 → B34-01 → B34-03 → B34-04**

The ticket numbers (B34-01, B34-02, B34-03) reflect logical grouping, not implementation sequence.

- **B34-01** references `ctx.BeBuffer` — this property is added by **B34-02**.
- **B34-03** references `ctx.TrimBuffer`, `ctx.FlatBuffer`, `ctx.Ask`, `ctx.Bid` — all added by **B34-02**.
- **B34-02** touches only `Core/PttContracts.cs` + `TradeCopierPanel.cs` and has zero upstream dependencies.

**The engineer assigned B34-01 MUST implement the `IPttHostContext` interface additions from B34-02 first**, before writing the B34-01 `Execute()` body, or the project will not compile.

---

## 4. Ticket Architectures

---

### TICKET B34-01 — Rewrite `PttBreakEven.Execute()`

**File:** `src/PropTraderTools/Features/PttBreakEven.cs`
**Deferred work closed:** DW-B33-05, DW-B33-06, DW-B33-07
**Prerequisite:** B34-02 implemented (ctx.BeBuffer available on IPttHostContext)

#### 4.1.1 Method: `Execute(IPttHostContext ctx)` — Full Rewrite

**Current (buggy) body — lines 52–67 approximate:**
```
// OUTSIDE loop — applies leader values to all accounts:
double entryPrice = leaderPos.AveragePrice;           // DW-B33-06: leader only
bool   isLong     = leaderPos.MarketPosition == Long; // DW-B33-05: leader only
double bePrice    = entryPrice;                       // DW-B33-06: no buffer, no sign flip

CancelStaleBracketsLocal(ctx.LeaderAccount, ctx.Instrument); // DW-B33-07: once, leader only

foreach (Account acc in ctx.AllAccounts)
    SubmitBeStopLocal(acc, ctx.Instrument, bePrice, isLong);  // wrong values
```

**New body — complete replacement of the three outside-loop lines + the Cancel call:**
```csharp
public void Execute(IPttHostContext ctx)
{
    if (!IsEnabled) return;                                                  // (1)

    Position leaderPos = FindPositionLocal(ctx.LeaderAccount, ctx.Instrument);
    if (leaderPos == null || leaderPos.Quantity == 0) return;                // (2)

    double tickSize = ctx.Instrument.MasterInstrument.TickSize;
    double buf      = (double)ctx.BeBuffer;

    foreach (Account acc in ctx.AllAccounts)                                 // (3)
    {
        Position pos = FindPositionLocal(acc, ctx.Instrument);
        if (pos == null || pos.Quantity == 0) continue;                      // (4)

        bool   isLong  = pos.MarketPosition == MarketPosition.Long;          // DW-B33-05 FIX
        double bePrice = pos.AveragePrice + (isLong ? +buf : -buf) * tickSize; // DW-B33-06 FIX

        CancelStaleBracketsLocal(acc, ctx.Instrument);                       // DW-B33-07 FIX
        SubmitBeStopLocal(acc, ctx.Instrument, bePrice, isLong);
    }

    // Notify bus with leader context for any downstream listeners.
    // NOTE DW-B34-RAISE-01: BeEventArgs carries leader values, not per-account —
    // acceptable for notification-only consumers. Mixed-direction portfolios deferred.
    bool   leaderIsLong  = leaderPos.MarketPosition == MarketPosition.Long;
    double leaderBePx    = leaderPos.AveragePrice + (leaderIsLong ? +buf : -buf) * tickSize;
    PttBus.RaiseBe(this, new BeEventArgs(                                     // (5)
        ctx.Instrument, leaderBePx, leaderPos.AveragePrice, leaderIsLong, string.Empty));
}
```

**CYC analysis:**
| Branch | +1 |
|---|---|
| Start | 1 |
| `if (!IsEnabled)` | +1 |
| `if (leaderPos == null \|\| ...Quantity == 0)` | +1 (the `\|\|` adds another) = +2 total |
| `foreach` | +1 |
| `if (pos == null \|\| ...Quantity == 0)` | +1 (+1 for `\|\|`) = +2 total |
| **Total CYC** | **7** ✓ (target <= 8) |

**No other methods in PttBreakEven.cs are changed.** `CancelStaleBracketsLocal`, `SubmitBeStopLocal`, `FindPositionLocal` are unchanged.

#### 4.1.2 Jane Street Compliance (B34-01)

| Rule | Check | Result |
|---|---|---|
| JS-021 no lock | `grep lock( Features/PttBreakEven.cs` → 0 | PASS |
| JS-033 no async void | No async anywhere | PASS |
| JS-001 no throw hot path | No new throw; existing try/catch in helpers preserved | PASS |
| JS-002 no return null | `continue` replaces null check on flat account | PASS |
| NT8-006 no LINQ | Explicit foreach, no `.Where`/`.First`/`.Select` | PASS |
| NT8-050 no Positions[instr] | Uses `FindPositionLocal(acc, instr)` | PASS |
| NT8-049 StopMarket arg order | `SubmitBeStopLocal` unchanged — arg6=0, arg7=stopPrice | PASS |
| NT8-014 signal "PTT-" prefix | Signal `"PTT-BE-Stop"` in `SubmitBeStopLocal` unchanged | PASS |

#### 4.1.3 Tests — B34-01 (4 new [Fact])

All tests use a `FakeHostContext : IPttHostContext` stub and `FakePosition` data objects. They verify logic-level correctness without triggering NT8 runtime `CreateOrder`. Since `SubmitBeStopLocal` is wrapped in try/catch, NT8 calls safely swallow in the test runner.

| Test Name | What It Asserts |
|---|---|
| `T_B34_BE_ShortAccountBuyToCover` | Given `pos.MarketPosition == Short`, the `isLong` local inside the loop is `false`, so `OrderAction.BuyToCover` is selected — not `Sell`. Verified by subclassing `PttBreakEven` and capturing the direction passed to a test-override of `SubmitBeStopLocal`, OR by reflection on the method body for the `BuyToCover` branch. |
| `T_B34_BE_PerAccountBePrice` | Given two accounts with different `AveragePrice` values (e.g., 100.0 and 200.0), `bePrice` for each account uses its own `pos.AveragePrice` — not the leader's. Asserts two distinct `bePrice` values were computed. |
| `T_B34_BE_CancelBeforeSubmitPerAccount` | Verifies `CancelStaleBracketsLocal` is invoked once per account inside the loop — not a single pre-loop call. Uses a call-count observer pattern on a `PttBreakEven` subclass or via reflection on the method's IL structure. |
| `T_B34_BE_BufferShortFlipped` | Given `buf=2`, `tickSize=0.25`, `pos.AveragePrice=100.0`, `isLong=false`: asserts `bePrice == 100.0 + (-2) * 0.25 == 99.5`. Verifies sign flip for short positions. |

**Deferred test (DW-B34-RAISE-01):** Test for mixed-direction portfolio where leader is long but a follower is short — the final `PttBus.RaiseBe` event carries leader's values. This edge case is logged as deferred, not blocking B34.

---

### TICKET B34-02 — Add Buffer and Market Props to `IPttHostContext` + `TradeCopierPanel`

**Files:**
- `src/PropTraderTools/Core/PttContracts.cs`
- `src/PropTraderTools/TradeCopierPanel.cs`

**Deferred work closed:** DW-B33-02
**No other files touched.**

#### 4.2.1 Interface: `IPttHostContext` — 5 new properties

Add to `Core/PttContracts.cs` inside the `IPttHostContext` interface declaration, after the existing 3 props:

```csharp
/// <summary>Break-even buffer in ticks. Read from TradeCopierPanel._beBuffer.</summary>
int BeBuffer { get; }

/// <summary>Trim buffer in ticks. Read from TradeCopierPanel._trimBuffer.</summary>
int TrimBuffer { get; }

/// <summary>Flatten buffer in ticks. Read from TradeCopierPanel._flattenBuffer.</summary>
int FlatBuffer { get; }

/// <summary>
/// Current ask price from instrument market data. Returns 0.0 if no quote.
/// NT8-032: reads _instrument.MarketData.Ask.Price. CYC=4 in impl.
/// </summary>
double Ask { get; }

/// <summary>
/// Current bid price from instrument market data. Returns 0.0 if no quote.
/// NT8-032: reads _instrument.MarketData.Bid.Price. CYC=4 in impl.
/// </summary>
double Bid { get; }
```

**Type decision — `int` not `double` for buffer props:** The backing fields in `TradeCopierPanel` (`_beBuffer`, `_trimBuffer`, `_flattenBuffer`) are declared `private int`. Using `int` on the interface avoids a cast and matches the existing field types exactly.

#### 4.2.2 Implementation: `TradeCopierPanel` — 5 new explicit interface implementations

Add immediately after line 130 (`IReadOnlyList<Account> IPttHostContext.AllAccounts { get { return _allAccounts; } }`):

```csharp
// B34 T2 -- Buffer props and market quote props wired to existing private fields/methods.
int    IPttHostContext.BeBuffer   { get { return _beBuffer; } }
int    IPttHostContext.TrimBuffer { get { return _trimBuffer; } }
int    IPttHostContext.FlatBuffer { get { return _flattenBuffer; } }
double IPttHostContext.Ask        { get { return GetAsk(); } }
double IPttHostContext.Bid        { get { return GetBid(); } }
```

**NT8-001 compliance:** All use `{ get { return ...; } }` syntax. No `{ get; init; }`. No expression-body `=> field` (banned in some NT8 contexts — use explicit braces). ✓

#### 4.2.3 Jane Street Compliance (B34-02)

| Rule | Check | Result |
|---|---|---|
| JS-021 no lock | Property getters are read-only field returns | PASS |
| NT8-001 no init accessor | `{ get { return _field; } }` pattern used throughout | PASS |
| NT8-006 no LINQ | No LINQ in property getters | PASS |

#### 4.2.4 Tests — B34-02 (1 new [Fact])

| Test Name | What It Asserts |
|---|---|
| `T_B34_ContextBeBuffer_Forwarded` | Creates a `TradeCopierPanel` instance (or a partial stub that exposes the IPttHostContext implementation). Sets `_beBuffer = 3` via reflection. Reads `((IPttHostContext)panel).BeBuffer`. Asserts result == 3. Verifies the explicit interface property correctly forwards the private field without transformation. |

---

### TICKET B34-03 — Wire Buffer in `PttTrim` and `PttFlatten`

**Files:**
- `src/PropTraderTools/Features/PttTrim.cs`
- `src/PropTraderTools/Features/PttFlatten.cs`

**Deferred work closed:** DW-B33-04
**Prerequisite:** B34-02 implemented (`ctx.TrimBuffer`, `ctx.FlatBuffer`, `ctx.Ask`, `ctx.Bid` available)

#### 4.3.1 Design Decision: Ask/Bid in Context

**The spec requires:** Limit orders for Trim/Flatten when buffer > 0, anchored to Ask (trim long) or Bid (trim short).

**Available:** `TradeCopierPanel.GetAsk()` and `GetBid()` (confirmed at lines 1007 and 1020) are private methods. They cannot be called from `PttTrim.Execute()` directly.

**Resolution:** B34-02 adds `double Ask { get; }` and `double Bid { get; }` to `IPttHostContext`, implemented by `TradeCopierPanel` returning `GetAsk()` and `GetBid()` respectively. B34-03 then uses `ctx.Ask` / `ctx.Bid`.

**Fallback when buffer == 0:** Keep `OrderType.Market`. This ensures zero-buffer configuration behaves identically to B33.

#### 4.3.2 Method: `PttTrim.TrimPositionLocal()` — Signature Change

**Current signature:**
```csharp
private static void TrimPositionLocal(Account acc, Instrument instr, int qty, Position pos)
```

**New signature:**
```csharp
private static void TrimPositionLocal(Account acc, Instrument instr,
                                      int qty, Position pos,
                                      int buffer, double ask, double bid)
```

**Key logic addition inside `TrimPositionLocal`:**

```csharp
bool   isLong     = pos.MarketPosition == MarketPosition.Long;
double tickSize   = instr.MasterInstrument.TickSize;
double limitPrice = 0.0;
OrderType orderType;

if (buffer > 0)
{
    // Long: sell above market (Ask + buffer*tick). Short: buy below market (Bid - buffer*tick).
    limitPrice = isLong
        ? ask + buffer * tickSize
        : bid - buffer * tickSize;
    orderType = OrderType.Limit;
}
else
{
    orderType = OrderType.Market;   // buffer == 0: unchanged behavior
}
```

Then in `CreateOrder`:
- `orderType` replaces the hardcoded `OrderType.Market`
- `arg6 = limitPrice` (was `0`)
- `arg7 = 0` (stopPrice stays 0 — NT8-049 not affected since this is Limit, not StopMarket)
- Signal name stays `"PTT-Trim"` (NT8-014 ✓)

**Updated Execute() call site:**
```csharp
TrimPositionLocal(ctx.LeaderAccount, ctx.Instrument, trimQty, pos,
                  ctx.TrimBuffer, ctx.Ask, ctx.Bid);
```

**CYC of `TrimPositionLocal` new:**
| Branch | +1 |
|---|---|
| Start | 1 |
| `if (acc == null \|\| instr == null \|\| qty <= 0)` | +1 (+2 for `\|\|` × 2) = +3 total |
| `if (buffer > 0)` | +1 |
| ternary `isLong ? ask+... : bid-...` | +1 |
| try/catch block | +1 |
| **Total CYC** | **7** ✓ |

#### 4.3.3 Method: `PttFlatten.FlattenPositionLocal()` — Same Pattern

**Current signature:**
```csharp
private static void FlattenPositionLocal(Account acc, Instrument instr, Position pos)
```

**New signature:**
```csharp
private static void FlattenPositionLocal(Account acc, Instrument instr, Position pos,
                                         int buffer, double ask, double bid)
```

**Logic mirrors TrimPositionLocal exactly.** Signal name stays `"PTT-Flatten"` (NT8-014 ✓).

**Updated Execute() call site:**
```csharp
FlattenPositionLocal(ctx.LeaderAccount, ctx.Instrument, pos,
                     ctx.FlatBuffer, ctx.Ask, ctx.Bid);
```

**CYC of `FlattenPositionLocal` new:** 7 ✓ (identical structure to Trim)

#### 4.3.4 Jane Street Compliance (B34-03)

| Rule | Check | Result |
|---|---|---|
| JS-021 no lock | No lock anywhere in both files | PASS |
| JS-033 no async void | Synchronous void only | PASS |
| NT8-006 no LINQ | No `.Where`/`.First`/`.Select`/`.Any` | PASS |
| NT8-007 arg11 | `(NinjaTrader.Cbi.CustomOrder)null` — unchanged | PASS |
| NT8-013 GTC | `DateTime.MaxValue` — unchanged | PASS |
| NT8-014 signal prefix | `"PTT-Trim"`, `"PTT-Flatten"` — unchanged | PASS |
| NT8-049 arg6/arg7 order | Limit: `arg6=limitPrice, arg7=0` (correct for Limit) | PASS |
| NT8-050 no Positions[instr] | `FindPositionLocal` unchanged | PASS |

**NT8-049 note for Limit orders:** `arg6=limitPrice, arg7=0` is correct for `OrderType.Limit`. The NT8-049 rule is specifically about StopMarket confusion. A Limit order uses `arg6` as the limit price and `arg7=0` for stop — this is the correct NT8 pattern.

#### 4.3.5 Tests — B34-03 (1 new [Fact])

| Test Name | What It Asserts |
|---|---|
| `T_B34_Trim_BufferContextWired` | Creates a `FakeHostContext` with `TrimBuffer = 5`. Constructs a `PttTrim` instance. Verifies that `ctx.TrimBuffer` is accessible (property exists on the interface type via reflection). Asserts that `typeof(IPttHostContext).GetProperty("TrimBuffer")` is non-null and returns type `int`. Optionally verifies that when `TrimBuffer > 0`, the `TrimPositionLocal` helper is called with a non-zero `limitPrice` argument (captured via test override). |

---

### TICKET B34-04 — Verifier Pass

**No source code changes.** Validation only.

**Checklist:**
1. F5 in NinjaTrader: all 3 changed files compile without error
2. `[Fact]` count: `grep -c "\[Fact\]" tests/**/*.cs` >= **177** (171 baseline + 6 new)
3. Zero test regressions: all 171 existing tests still PASS
4. Update CopyEngine.cs tag line:
   ```
   "PTT-COPIER B34 | be-multiAccount-fixes | {date}"
   ```
   where `{date}` = `DateTime.UtcNow.ToString("yyyy-MM-dd")` at time of merge
5. Run `scripts\verify_links.ps1 -Fix`

---

## 5. Full Data Flow (Post-B34)

```
[UI Button: BE]
  → TradeCopierPanel._beModule.Execute(this)     // this = IPttHostContext

  → PttBreakEven.Execute(ctx)
      → FindPositionLocal(ctx.LeaderAccount, ctx.Instrument)
          → if null/flat → return (guard)
      → tickSize = ctx.Instrument.MasterInstrument.TickSize
      → buf = (double)ctx.BeBuffer               // NEW: from IPttHostContext
      → foreach acc in ctx.AllAccounts:
          → pos = FindPositionLocal(acc, ctx.Instrument)
          → if null/flat → continue
          → isLong = pos.MarketPosition == Long   // NEW: per-account (DW-B33-05)
          → bePrice = pos.AveragePrice + (isLong ? +buf : -buf) * tickSize  // NEW (DW-B33-06)
          → CancelStaleBracketsLocal(acc, ctx.Instrument)  // NEW: per-account (DW-B33-07)
          → SubmitBeStopLocal(acc, ctx.Instrument, bePrice, isLong)
      → PttBus.RaiseBe(...)                       // notification (leader values)

[UI Button: Trim]
  → TradeCopierPanel._trimModule.Execute(this)

  → PttTrim.Execute(ctx)
      → pos = FindPositionLocal(ctx.LeaderAccount, ctx.Instrument)
      → if null/flat → return
      → trimQty = Max(1, pos.Quantity / 2)
      → TrimPositionLocal(acc, instr, trimQty, pos, ctx.TrimBuffer, ctx.Ask, ctx.Bid)
          → if buffer > 0: Limit at ask+(buf*tick) [long] or bid-(buf*tick) [short]
          → if buffer == 0: Market (unchanged behavior)
      → PttBus.RaiseTrim(...)

[UI Button: Flatten]
  → Same pattern with ctx.FlatBuffer, ctx.Ask, ctx.Bid
```

---

## 6. IPttHostContext Interface — Before / After

**Before (B33):**
```csharp
public interface IPttHostContext
{
    Account                LeaderAccount { get; }
    Instrument             Instrument    { get; }
    IReadOnlyList<Account> AllAccounts   { get; }
}
```

**After (B34):**
```csharp
public interface IPttHostContext
{
    Account                LeaderAccount { get; }
    Instrument             Instrument    { get; }
    IReadOnlyList<Account> AllAccounts   { get; }
    // B34 additions:
    int    BeBuffer   { get; }
    int    TrimBuffer { get; }
    int    FlatBuffer { get; }
    double Ask        { get; }
    double Bid        { get; }
}
```

**TradeCopierPanel additions (after line 130):**
```csharp
int    IPttHostContext.BeBuffer   { get { return _beBuffer; } }
int    IPttHostContext.TrimBuffer { get { return _trimBuffer; } }
int    IPttHostContext.FlatBuffer { get { return _flattenBuffer; } }
double IPttHostContext.Ask        { get { return GetAsk(); } }
double IPttHostContext.Bid        { get { return GetBid(); } }
```

---

## 7. Test Inventory — All 6 New [Fact] Tests

| # | Test Name | File | Ticket | Verification Pattern |
|---|---|---|---|---|
| 1 | `T_B34_BE_ShortAccountBuyToCover` | `PttBreakEvenTests.cs` | B34-01 | Stub with Short pos → verify direction == BuyToCover |
| 2 | `T_B34_BE_PerAccountBePrice` | `PttBreakEvenTests.cs` | B34-01 | Two accounts with distinct AvgPrice → two distinct bePrices |
| 3 | `T_B34_BE_CancelBeforeSubmitPerAccount` | `PttBreakEvenTests.cs` | B34-01 | Cancel call count == AllAccounts.Count |
| 4 | `T_B34_BE_BufferShortFlipped` | `PttBreakEvenTests.cs` | B34-01 | buf=2, tick=0.25, avg=100, short → bePrice==99.5 |
| 5 | `T_B34_ContextBeBuffer_Forwarded` | `PttContractsTests.cs` | B34-02 | Reflection: `_beBuffer=3` → `ctx.BeBuffer==3` |
| 6 | `T_B34_Trim_BufferContextWired` | `PttTrimTests.cs` | B34-03 | Reflection: `IPttHostContext.TrimBuffer` is `int` property; > 0 → Limit order path |

**Baseline protection:** All 171 existing tests must remain green. No existing test file is modified by B34; only new `[Fact]` methods are added to existing test files.

---

## 8. 7-Scan Checklist (Per-Ticket Pre-Flight)

Run these before every ticket's F5 submission. All must return zero hits in changed files.

```
SCAN-01: grep "lock("         src/PropTraderTools/ --include="*.cs" -r
         → Expected: 0 hits in any B34-modified file

SCAN-02: grep "async void "   src/PropTraderTools/ --include="*.cs" -r
         → Expected: 0 hits in any B34-modified file

SCAN-03: grep -E "\.Where|\.First|\.Select|\.Any"  src/PropTraderTools/ --include="*.cs" -r
         → Expected: 0 hits in any B34-modified file

SCAN-04: grep "{ get; init; }"  src/PropTraderTools/ --include="*.cs" -r
         → Expected: 0 hits in any B34-modified file

SCAN-05: grep "acc\.Positions\["  src/PropTraderTools/ --include="*.cs" -r
         → Expected: 0 hits in any B34-modified file

SCAN-06: NT8 F5 gate
         → Build PttBreakEven.cs, PttContracts.cs, TradeCopierPanel.cs, PttTrim.cs, PttFlatten.cs
         → Expected: 0 errors, 0 warnings in modified files

SCAN-07: [Fact] count
         → grep -c "\[Fact\]" tests/**/*.cs
         → Expected: >= 177
```

---

## 9. Deferred Items (Not in B34 Scope)

| DW ID | Description | Blocked By |
|---|---|---|
| DW-B34-RAISE-01 | `PttBus.RaiseBe` carries leader values only — incorrect for mixed-direction portfolios where followers are in opposite direction | Requires per-account event model change (larger scope) |
| DW-B34-TRIM-02 | Trim currently operates on leader account only; follower trim copy is handled by `PttCopier` relay — confirm relay also passes ask/bid | Requires relay signature audit in B35 |

---

## 10. Component Summary

| Component | File | Change Type | CYC Before | CYC After |
|---|---|---|---|---|
| `PttBreakEven.Execute()` | `Features/PttBreakEven.cs` | Rewrite loop body | 4 | 7 |
| `IPttHostContext` | `Core/PttContracts.cs` | Add 5 properties | — | 1 each |
| `TradeCopierPanel` | `TradeCopierPanel.cs` | Add 5 explicit impls | — | 1 each |
| `PttTrim.TrimPositionLocal()` | `Features/PttTrim.cs` | Add buffer/limit path | 2 | 7 |
| `PttTrim.Execute()` | `Features/PttTrim.cs` | Update call site only | 3 | 3 |
| `PttFlatten.FlattenPositionLocal()` | `Features/PttFlatten.cs` | Add buffer/limit path | 2 | 7 |
| `PttFlatten.Execute()` | `Features/PttFlatten.cs` | Update call site only | 3 | 3 |

**All CYC values <= 8.** No violations.

---

## 11. Rules Catalog Gate Result

| Rule ID | Description | B34-01 | B34-02 | B34-03 |
|---|---|---|---|---|
| JS-021 | No `lock()` | PASS | PASS | PASS |
| JS-033 | No `async void` | PASS | PASS | PASS |
| JS-001 | No throw in hot path | PASS | PASS | PASS |
| JS-002 | No `return null` for missing values | PASS (pre-existing `FindPositionLocal` retained) | PASS | PASS |
| NT8-001 | No `{ get; init; }` | N/A | PASS | N/A |
| NT8-006 | No LINQ | PASS | N/A | PASS |
| NT8-007 | `arg11 = (CustomOrder)null` | PASS (unchanged) | N/A | PASS (unchanged) |
| NT8-013 | `DateTime.MaxValue` not `DateTime.Now` | PASS (unchanged) | N/A | PASS (unchanged) |
| NT8-014 | Signal name starts `"PTT-"` | PASS (unchanged) | N/A | PASS (unchanged) |
| NT8-049 | `arg6=limitPrice, arg7=stopPrice` | PASS (unchanged) | N/A | PASS (Limit: arg6=limit, arg7=0) |
| NT8-050 | `FindPositionLocal` not `acc.Positions[instr]` | PASS | N/A | PASS (unchanged) |

**GATE RESULT: PASS — all P0 and P1 rules satisfied across all 3 code-change tickets.**

---

*Plan author: ptt-architect | Block: B34 | Phase 1 | 2026-07-27*
*Next: ptt-plan-reviewer → 02-plan-review.md → REVIEW_PASS or REVIEW_FAIL*
