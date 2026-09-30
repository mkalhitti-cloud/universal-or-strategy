# B66-LaneB Ticket-1 Verification Report

**Verifier**: ptt-verifier (independent)
**Date**: 2026-08-12
**Ticket**: DW-B66-BE-01 -- SubmitBeStop isLong direction race fix
**Commit**: 78b55d8d

---

## NT8 Verification Citations

### NT8-VERIFY-01 -- Line 1721 position update lag

**Verbatim citation** (NT8_FULL_REFERENCE.md line 1721):
> "Changes to positions will not be reflected till at least the next **OnBarUpdate()** event after an order fill."

**Context**: This note appears in the `GetAtmStrategyMarketPosition` entry but the position-update-lag
behavior applies broadly to NT8 position state. This is the same race cited in B65-LaneA for
`TryDispatchLeaderFlat` (CopyEngine.cs lines 651-654), where direction was read from `e.Order.Name`
at call-site time instead of re-reading inside the method.

**Confirmation**: SubmitBeStop body (lines 482-503) does NOT contain `pos.MarketPosition`. The old
race line `bool isLong = pos.MarketPosition == MarketPosition.Long;` has been removed. Line 489
reads only `OrderAction dir = isLong ? OrderAction.Sell : OrderAction.BuyToCover;` using the
`isLong` parameter. Race eliminated. CONFIRMED.

### NT8-VERIFY-02 -- BuyToCover on Long positions

**Grep result**: NT8_FULL_REFERENCE.md line 857:
```
  * OrderAction.BuyToCover
```
(from the OrderAction enum listing at lines 854-859 of NT8_FULL_REFERENCE.md)

**Interpretation**: NT8_FULL_REFERENCE.md confirms `BuyToCover` is an enum value under `OrderAction`.
The reference does not contain an explicit "stop price must be above market" rejection message --
that is observed broker behavior. The bug summary in the architecture plan
(docs/brain/B66-LaneB/02-architecture-plan.md) documents the symptom:
"Orders rejected with 'buy order stop price must be above trade price' on Long positions."

**Fix validity**: By passing `isLong` from the caller's snapshot, `BuyToCover` is only submitted
when `isLong=false` (actual Short position). For Long positions, `OrderAction.Sell` is used,
which is valid for a stop-loss on a Long. The fix eliminates the incorrect BuyToCover-on-Long case.
CONFIRMED.

### NT8-VERIFY-03 -- SubmitBeStop call sites (all 4-arg)

**Grep scan results** (Select-String -Path CopyEngine.cs -Pattern "SubmitBeStop"):
- Line 5: comment reference (pattern header) -- not a call
- Line 345: comment only
- Line 346: comment only
- Line 351: `SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong);` -- RelayBe -- 4-arg CONFIRMED
- Line 473: comment (method header)
- Line 482: method signature -- definition
- Line 521: `SubmitBeStop(acc, pos.Instrument, bePrice, isLong);` -- ArmAllPendingBe -- 4-arg CONFIRMED

**Grep scan results** (Select-String -Path PttGlobalBreakEven.cs -Pattern "SubmitBeStop"):
- Line 2: comment
- Line 25: comment
- Line 27: comment
- Line 28: comment
- Line 33: comment
- Line 35: `CopyEngine.Instance.SubmitBeStop(acc, instr, price, lng)` -- production ctor lambda -- 4-arg CONFIRMED
- Line 38: comment (test ctor header)
- Line 65: comment
- Line 82: comment

**All call sites confirmed 4-arg. No 3-arg call to SubmitBeStop remains.**

### NT8-VERIFY-04 -- SubmitBeStop CYC <= 8 (independent branch count from source)

Source read from CopyEngine.cs lines 482-503:

```
internal void SubmitBeStop(Account acc, NinjaTrader.Cbi.Instrument instr, double bePrice, bool isLong)
{
    if (acc == null || instr == null) return;              // (1)
    NinjaTrader.Cbi.Position pos = null;
    foreach (NinjaTrader.Cbi.Position p in acc.Positions) // (2)
        if (p.Instrument == instr) { pos = p; break; }    // (3)
    if (pos == null || pos.Quantity == 0) return;          // (4)
    OrderAction dir = isLong ? OrderAction.Sell : OrderAction.BuyToCover; // (5)
    try                                                    // (6) CreateOrder call
    {
        var order = acc.CreateOrder(...)
        if (order != null)                                 // (6) inner if
            acc.Submit(new[] { order });
    }
    catch { }
}
```

| Branch | Source | Count |
|--------|--------|-------|
| base | always 1 | 1 |
| `if (acc == null \|\| instr == null)` | compound = 1 decision | +1 |
| `foreach (...) acc.Positions` | loop = 1 decision | +1 |
| `if (p.Instrument == instr)` | inner condition | +1 |
| `if (pos == null \|\| pos.Quantity == 0)` | compound = 1 decision | +1 |
| `isLong ? Sell : BuyToCover` | ternary = 1 decision | +1 |
| `if (order != null)` | null check | +1 |
| **Total** | | **7** |

CYC = **7 <= 8**. PASS.

---

## 7-Scan Results (Layer 3 -- Independent)

| Scan | Command | Engineer reported | Verifier result | Cross-check |
|------|---------|-------------------|----------------|-------------|
| 1 lock( | `Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "lock\("` | 1 comment hit (block(0) in CYC comment) -- no actual lock() calls in modified methods | 1 hit at line 916: `// CYC=5: fo null(1), price delta(2), TrailPrice>0(3), isStop branch(4), try block(0).` -- substring match of `lock(` inside `block(0)` in a comment; 0 actual lock() calls in modified methods | MATCH |
| 2 throw new | `Select-String -Path src\PropTraderTools\CopyEngine.cs,src\PropTraderTools\Features\PttGlobalBreakEven.cs -Pattern "throw new"` | 0 matches | 0 matches (command produced no output) | MATCH |
| 3 return null | `Select-String -Path src\PropTraderTools\CopyEngine.cs,src\PropTraderTools\Features\PttGlobalBreakEven.cs -Pattern "return null;"` | 5 pre-existing hits (lines 1001,1039,1660,1666,1728) -- 0 in B66 methods | 5 hits at lines 1001, 1039, 1660, 1666, 1728 -- confirmed all in unmodified methods (FindBestEntry, FindFollowerEntryOrder, FindRule, FindRule, FindPosition). None inside SubmitBeStop (482-503), ArmAllPendingBe (508-524), RelayBe (348-352), or ExecuteOne (67-76). | MATCH |
| 4 CYC | manual count SubmitBeStop from source | CYC=7 | CYC=7 (independent count: 1 base + null-guard + foreach + inner-if + pos-null-guard + ternary + order-null-check = 7) | MATCH |
| 5 xUnit | `Select-String -Path src\PropTraderTools\Tests\B66Tests.cs -Pattern "\[Fact\]"` and NUnit/MSTest scan | 5 hits (lines 17,27,37,55,69); NUnit/MSTest: 0 code hits (1 comment only) | [Fact]: 5 hits at lines 17, 27, 37, 55, 69 CONFIRMED. NUnit/MSTest scan: 1 hit at line 5 -- comment `// xUnit only -- no NUnit, no MSTest. ASCII identifiers only.` (prohibition comment, not usage). 0 NUnit/MSTest code hits. | MATCH |
| 6 ASCII | `Select-String -Path src\PropTraderTools\Tests\B66Tests.cs -Pattern "[^\x00-\x7F]"` | 0 matches | 0 matches (command produced no output) | MATCH |
| 7 NT8 CreateOrder | manual: count args to acc.CreateOrder() in SubmitBeStop, verify arg2=dir from isLong | 12 args confirmed; arg2=dir derived from isLong (not pos.MarketPosition re-read) | From source lines 492-498: instr, dir, OrderType.StopMarket, OrderEntry.Manual, TimeInForce.Gtc, pos.Quantity, 0, bePrice, string.Empty, "PTT-BE-Stop", DateTime.MaxValue, (NinjaTrader.Cbi.CustomOrder)null = **12 args**. arg2=`dir` assigned at line 489 from `isLong ? OrderAction.Sell : OrderAction.BuyToCover` using parameter. No `pos.MarketPosition` re-read in body. | MATCH |

---

## Implementation Verification Checklist

### V-A: SubmitBeStop signature change

**Evidence from CopyEngine.cs line 482**:
```csharp
internal void SubmitBeStop(Account acc, NinjaTrader.Cbi.Instrument instr, double bePrice, bool isLong)
```
- [x] Signature is 4-arg with `bool isLong` as 4th parameter -- CONFIRMED
- [x] Body does NOT contain `pos.MarketPosition` read -- CONFIRMED (searched lines 483-503; not present)
- [x] Body DOES contain `OrderAction dir = isLong ? OrderAction.Sell : OrderAction.BuyToCover;` at line 489 -- CONFIRMED

**V-A: PASS**

### V-B: ArmAllPendingBe call site

**Evidence from CopyEngine.cs line 521**:
```csharp
SubmitBeStop(acc, pos.Instrument, bePrice, isLong);
```
- [x] 4-arg call confirmed at line 521
- [x] `isLong` computed at line 516: `bool isLong = pos.MarketPosition == MarketPosition.Long;` -- in scope before the call

**V-B: PASS**

### V-C: RelayBe call site

**Evidence from CopyEngine.cs line 351**:
```csharp
SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong);
```
- [x] 4-arg call confirmed at line 351
- [x] `e.IsLong` comes from BeEventArgs parameter -- set at event-fire time, not re-read

**V-C: PASS**

### V-D: PttGlobalBreakEven delegate chain

**Evidence from PttGlobalBreakEven.cs**:

Line 29 (field):
```csharp
private readonly Action<Account, Instrument, double, bool> _submitBeStop;
```
- [x] Field type is `Action<Account, Instrument, double, bool>` -- CONFIRMED

Line 35 (production ctor lambda):
```csharp
: this((acc, instr, price, lng) => CopyEngine.Instance.SubmitBeStop(acc, instr, price, lng)) { }
```
- [x] Lambda has 4 params: `(acc, instr, price, lng)` -- CONFIRMED
- [x] Forwards all 4 to SubmitBeStop -- CONFIRMED

Line 38 (test injection ctor):
```csharp
internal PttGlobalBreakEven(Action<Account, Instrument, double, bool> submitBeStop)
```
- [x] Test ctor accepts `Action<Account, Instrument, double, bool>` -- CONFIRMED

Line 75 (ExecuteOne call):
```csharp
_submitBeStop(acc, pos.Instrument, bePrice, isLong);
```
- [x] ExecuteOne calls `_submitBeStop` with 4 args including `isLong` -- CONFIRMED

**V-D: PASS**

### V-E: B66Tests.cs

**Evidence from grep scan of src/PropTraderTools/Tests/B66Tests.cs**:

- [x] File exists at `src/PropTraderTools/Tests/B66Tests.cs` -- CONFIRMED (grep returned 70 matches)
- [x] Contains exactly 5 [Fact] tests at lines 17, 27, 37, 55, 69 -- CONFIRMED
- [x] Test names: T_B66_BE_01_LongPosition_SubmitsSellDirection, T_B66_BE_02_ShortPosition_SubmitsBuyToCoverDirection, T_B66_BE_03_NullAccount_ReturnsImmediately, T_B66_BE_04_PttGlobalBreakEven_DelegateSignatureAcceptsIsLong, T_B66_BE_05_BeEventArgs_IsLong_StoredCorrectly -- all 5 named T_B66_BE_01 through T_B66_BE_05 CONFIRMED
- [x] Namespace is `PropTraderTools` (line 11) -- CONFIRMED
- [x] `using Xunit;` at line 7 -- CONFIRMED
- [x] No NUnit/MSTest in code -- line 5 match is a prohibition comment, not actual usage -- CONFIRMED

**V-E: PASS**

---

## Cross-Check Summary

All 7 Layer 3 scans agree with the engineer's Layer 2 self-report. No discrepancies found.

| Layer 2 item | Layer 3 result | Status |
|---|---|---|
| SCAN-01: 1 comment hit, 0 actual lock() | 1 comment hit at line 916 (block(0) substring), 0 actual lock() | MATCH |
| SCAN-02: 0 throw new | 0 matches | MATCH |
| SCAN-03: 5 pre-existing return null (lines 1001,1039,1660,1666,1728), 0 in B66 methods | 5 hits confirmed, none in B66 methods | MATCH |
| SCAN-04: CYC=7 for SubmitBeStop | CYC=7 (independently counted) | MATCH |
| SCAN-05: 5 [Fact] hits; NUnit/MSTest 0 code hits | 5 [Fact] hits; NUnit/MSTest 1 comment-only hit | MATCH |
| SCAN-06: 0 ASCII violations | 0 ASCII violations | MATCH |
| SCAN-07: 12 CreateOrder args, arg2=dir from isLong | 12 args confirmed, arg2=dir from isLong parameter | MATCH |

**Layer 3 confidence: all scans match. No discrepancies. Layer 2 self-report verified correct.**

---

## Additional DNA Rule Checks (per role definition)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 lock( ban | No lock() in any modified method -- confirmed by SCAN-01 | PASS |
| JS-001 throw new | No throw in modified methods -- confirmed by SCAN-02 | PASS |
| JS-002 return null | All modified methods void; early returns are `return;` -- confirmed by SCAN-03 | PASS |
| JS-033 async void | All modified methods synchronous void -- confirmed by source read | PASS |
| SCAN-03 FontFamily | Not applicable (no WPF elements in modified files) | N/A |
| SCAN-04 hex color | Not applicable (no UI files modified) | N/A |
| SCAN-05 PTT- prefix | `CreateOrder` name arg = "PTT-BE-Stop" -- CONFIRMED at CopyEngine.cs line 496 | PASS |
| SCAN-06 DateTime.Now | `DateTime.MaxValue` used at line 497 (not DateTime.Now) -- PASS | PASS |
| SCAN-07 block( | SCAN-01 confirms 0 actual lock/block calls | PASS |
| Immutability | No SolidColorBrush, no Dictionary on CopyRule/CopyEngine fields added | PASS |
| Construction | CopyEngine non-private ctor not added; signal structs not changed | PASS |
| NT8 constraints | async/await: none; Account.All in ArmAllPendingBe (called from production Execute path, not inside OnInitialize/OnDestroyed/OnWindowCreated); sealed TradeCopierWindow: not modified | PASS |

---

## Architecture Compliance

All changes conform to the architecture plan (02-architecture-plan.md, REVIEW_PASS):
- 3 change sites in CopyEngine.cs: SubmitBeStop signature + ArmAllPendingBe call site + RelayBe call site
- 4 change sites in PttGlobalBreakEven.cs: field type + production ctor + test ctor + ExecuteOne call site
- New test file B66Tests.cs with 5 xUnit [Fact] tests
- CopyEngineTests.cs untouched per spec

Deferred items correctly not addressed: DW-B64-01, DW-B63-01, DW-B58-03 carry forward.

---

## Gate Result: VERIFY_PASS