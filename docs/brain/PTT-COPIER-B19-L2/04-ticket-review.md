# Ticket Review: PTT-COPIER-B19-L2
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Cycle**: 4 (final)
**Plan status read**: REVIEW_PASS Cycle 5 (02-architecture-plan.md)
**Tickets read**: 04-tickets.md Cycle 4
**Date**: 2026-07-07

---

## T1 — DW-B19-LIMIT-PRICE-01: Ask/Bid anchor fix for Trim/Flatten limit overloads

### Traceability: PASS
Every ticket item maps to a plan section. No phantom work. No missing plan items.

| Ticket item | Plan anchor |
|-------------|------------|
| `ComputeLimitPx` helper | §4.3 |
| `Trim(Instrument,int,double,double)` | §4.1 |
| `Flatten(Instrument,int,double,double)` | §4.2 |
| Guard block (ask<=0 \|\| bid<=0 \|\| exitBuffer==0) | §4.4 |
| `CreateOrder` arg constraints unchanged | §4.5 |
| Remove `GetRefPrice()` | §5.1 |
| `GetAsk()` no-arg | §5.2 |
| `GetBid()` no-arg | §5.3 |
| `OnTrimClick` update | §5.4 |
| `OnFlattenClick` update | §5.5 |
| DispatchShortcut Key.T/Key.F | §5.6 |
| 5 updated existing tests | §6.1 |
| 5 new [Fact] tests | §6.2 |

### Spec Coverage: PASS
- DW-B19-LIMIT-PRICE-01: covered exactly once in T1. No uncovered requirement. No duplicate.

### GetAsk()/GetBid() Signature Check: PASS
All call sites in **both** plan and ticket are no-arg. Verified:

| Location | Signature |
|----------|-----------|
| Plan §5.2 | `private double GetAsk()` — "no parameter" |
| Plan §5.3 | `private double GetBid()` — "no parameter" |
| Plan §13 data flow | `GetAsk()   [no-arg, reads _instrument field]` |
| Plan §15 method table | `private double GetAsk()  // CYC=4, no-arg` |
| Plan §5.4 call site | `double ask = GetAsk();` |
| Plan §5.5 call site | `double bid = GetBid();` |
| Plan §5.6 dispatch | `GetAsk(), GetBid()` |
| Ticket GetAsk() section | `private double GetAsk()` — no parameter |
| Ticket GetBid() section | `private double GetBid()` — no parameter |
| Ticket OnTrimClick | `double ask = GetAsk(); double bid = GetBid();` |
| Ticket OnFlattenClick | same pattern |
| Ticket DispatchShortcut | `GetAsk(), GetBid()` |

No-arg violation (V-SIG-01/02/03/04) is fully resolved.

### Tests 6–9 Assert.Equal on ComputeLimitPx: PASS
All 4 arithmetic new [Fact] tests use `Assert.Equal(expected, px, precision: 10)`
where `px = CopyEngine.ComputeLimitPx(...)`. Verified:

| Test | Assert |
|------|--------|
| `TrimLimit_Long_PlacesAboveAsk` | `Assert.Equal(5000.50, px, precision: 10)` |
| `TrimLimit_Short_PlacesBelowBid` | `Assert.Equal(4999.75, px, precision: 10)` |
| `FlattenLimit_Long_PlacesAboveAsk` | `Assert.Equal(5000.75, px, precision: 10)` |
| `FlattenLimit_Short_PlacesBelowBid` | `Assert.Equal(4999.50, px, precision: 10)` |

T1-TEST-01 (Cycle 1 violation) is fully resolved.

### §6.1 Test Names Match Exact Method Names: PASS
All 5 updated existing test method names in plan §6.1 match ticket §6.1 exactly:

1. `Flatten_LimitOverload_LongPosition_EmitsSellLimitFullQty` ✅
2. `Flatten_LimitOverload_ShortPosition_EmitsBuyToCoverLimitFullQty` ✅
3. `Trim_LimitOverload_LongPosition_EmitsSellLimitAtRefPlusTick` ✅
4. `Trim_LimitOverload_ShortPosition_EmitsBuyToCoverLimitAtRefMinusTick` ✅
5. `Flatten_ZeroBuffer_FallsBackToMarketOrder` ✅

V-TEST-NAME-01 (Cycle 3 violation) is fully resolved.

### JS Pre-Check: PASS
| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No `lock()` described | PASS |
| JS-001 | No `throw` in hot path — exceptions caught to `StatusUpdate` | PASS |
| JS-002 | `GetAsk()`/`GetBid()` return `0.0`, not null | PASS |
| JS-033 | No `async void` described | PASS |
| JS-008/009 | No mutable structs or SolidColorBrush | N/A — PASS |
| JS-023/025 | No Dictionary for shared state; no non-UI thread UI updates | PASS |

### CYC Pre-Check: PASS
| Method | CYC |
|--------|-----|
| `ComputeLimitPx` | 1 ✅ |
| `GetAsk()` | 4 ✅ |
| `GetBid()` | 4 ✅ |
| `OnTrimClick` | 4 ✅ |
| `OnFlattenClick` | 4 ✅ |
| `Trim(Instrument,int,double,double)` | 6 ✅ |
| `Flatten(Instrument,int,double,double)` | 6 ✅ |

All ≤ 8. Jane Street strict standard satisfied.

### NT8 Check: PASS
| Rule | Check | Result |
|------|-------|--------|
| NT8-007 | `CreateOrder` arg 12 = `(NinjaTrader.Cbi.CustomOrder)null` — unchanged | PASS |
| NT8-013 | `DateTime.MaxValue` used — not `DateTime.Now` | PASS |
| NT8-014 | Order names `"PTT-TrimLimit"` / `"PTT-FlattenLimit"` | PASS |
| NT8-032 | `GetAsk()`/`GetBid()` implement full 3-level null guard chain | PASS |
| sealed on TradeCopierWindow | Not described | PASS |
| async/await in lifecycle | Not described | PASS |
| FontFamily / hex color | Not described | PASS |

### Test Coverage: PASS
| Method (public or internal) | [Fact] test |
|-----------------------------|-------------|
| `ComputeLimitPx` (internal static) | 4 direct [Fact] tests ✅ |
| `Trim(Instrument,int,double,double)` (internal) | Updated tests 3 & 4; fallback in test 5 ✅ |
| `Flatten(Instrument,int,double,double)` (internal) | Updated tests 1 & 2; fallback in test 5 ✅ |

Private methods (`GetAsk`, `GetBid`, `OnTrimClick`, `OnFlattenClick`) are not required
to have isolated [Fact] tests — exercised indirectly.

### Scan Checklist: PASS
All 7 scans present with commands and required results:

| Scan | Command | Required result | Present |
|------|---------|----------------|---------|
| SCAN-01 | `grep -n "lock("` on CE + TCP | 0 results | ✅ |
| SCAN-02 | `grep -n "async void "` on CE + TCP | 0 results | ✅ |
| SCAN-03 | `grep -n "return null"` on CE + TCP | 0 results | ✅ |
| SCAN-04 | `grep -n "_engine\.Trim\|_engine\.Flatten"` on TCP | All have 4 args | ✅ |
| SCAN-05 | `grep -n "GetRefPrice"` on TCP | 0 results | ✅ |
| SCAN-06 | bare `.Ask/.Bid/.Last` without `.Price` on TCP | 0 results | ✅ |
| SCAN-07 | `grep -n "PTT-TrimLimit\|PTT-FlattenLimit"` on CE | Exactly 2 results | ✅ |

### File Routing: PASS
All `.cs` paths point to Wave workspace `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`.
No Director workspace paths for source files.

---

## VERDICT: T1 — TICKET_REVIEW_PASS

---

## Overall: TICKET_REVIEW_PASS

**All violations from Cycles 1–3 are resolved:**
- T1-TEST-01 (Cycle 1): Assert.Equal on ComputeLimitPx ✅ RESOLVED
- T1-TRACE-01/02/03 (Cycle 2): ComputeLimitPx in plan §4.3 ✅ RESOLVED
- V-SIG-01/02/03/04 (Cycles 3–4): GetAsk()/GetBid() no-arg everywhere ✅ RESOLVED
- V-TEST-NAME-01 (Cycle 3): §6.1 exact method names ✅ RESOLVED

**No new violations found in Cycle 4.**

The engineer is cleared to proceed with implementation from `04-tickets.md` Cycle 4.
