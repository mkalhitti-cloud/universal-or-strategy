# PTT-COPIER-B19-L2 Ticket T1 Verification Report
## DW-B19-LIMIT-PRICE-01 — Ask/Bid Anchor Fix for Trim/Flatten Limit Overloads

**Verdict**: VERIFY_PASS
**Date**: 2026-07-13 (L3 independent re-verification)
**Verifier**: ptt-verifier (independent Layer 3)
**Engineer report**: ticket-1-completion.md (BUILD_PASS)
**Plan**: 02-architecture-plan.md (REVIEW_PASS Cycle 5)
**Tickets**: 04-tickets.md (TICKET_REVIEW_PASS Cycle 4)

---

## Section A — CopyEngine.cs

| # | Check | Result |
|---|-------|--------|
| A1 | ComputeLimitPx: internal static double, CYC=1 ternary | PASS |
| A2 | Trim 4-arg: (Instrument, int exitBuffer, double ask, double bid) | PASS |
| A3 | Flatten 4-arg: (Instrument, int exitBuffer, double ask, double bid) | PASS |
| A4 | Old 3-arg Trim(Instrument, int, double refPrice) does NOT exist | PASS |
| A5 | Old 3-arg Flatten(Instrument, int, double refPrice) does NOT exist | PASS |
| A6 | Both call ComputeLimitPx(isLong, ask, bid, exitBuffer, tickSize) | PASS |
| A7 | Guard: if (ask <= 0 || bid <= 0 || exitBuffer == 0) present in both | PASS |
| A8 | Signal names: "PTT-TrimLimit" line 928, "PTT-FlattenLimit" line 968 | PASS |
| A9 | CreateOrder arg 12 = (NinjaTrader.Cbi.CustomOrder)null | PASS |

**Section A: 9/9 PASS**

---

## Section B — TradeCopierPanel.cs

| # | Check | Result |
|---|-------|--------|
| B1 | GetRefPrice() removed (comment-only at lines 6, 40, 45, 876) | PASS |
| B2 | GetAsk() no-arg, md.Ask.Price, CYC=4 (lines 876-885) | PASS |
| B3 | GetBid() no-arg, md.Bid.Price, CYC=4 (lines 890-898) | PASS |
| B4 | OnTrimClick calls _engine.Trim(_instrument, _trimBuffer, ask, bid) line 756 | PASS |
| B5 | OnFlattenClick calls _engine.Flatten(_instrument, _flattenBuffer, ask, bid) line 782 | PASS |
| B6 | DispatchShortcut Key.T: Trim(_instrument, _trimBuffer, GetAsk(), GetBid()) line 1475 | PASS |
| B7 | DispatchShortcut Key.F: Flatten(_instrument, _flattenBuffer, GetAsk(), GetBid()) line 1476 | PASS |

Note: Lines 1273-1281 legacy OnTrim/OnFlatten handlers call 1-arg market overload (pre-B12, not stale).

**Section B: 7/7 PASS**

---

## Section C — CopyEngineTests.cs

| # | Check | Result |
|---|-------|--------|
| C1 | Flatten_LimitOverload_LongPosition: 4-arg type array, param count 4, (null,2,100.0,100.0) line 1336 | PASS |
| C2 | Flatten_LimitOverload_ShortPosition: 4-arg, (null,3,4800.0,4800.0) line 1357 | PASS |
| C3 | Trim_LimitOverload_LongPosition: 4-arg, param count 4, (null,2,100.0,100.0) line 1382 | PASS |
| C4 | Trim_LimitOverload_ShortPosition: 4-arg, (null,2,100.0,100.0) line 1411 | PASS |
| C5 | Flatten_ZeroBuffer: (null,0,100.0,100.0) line 1450 and (null,2,0.0,0.0) line 1454 | PASS |
| C6 | TrimLimit_Long_PlacesAboveAsk: 5000.50=5000.25+1x0.25 (line 1469-1470) | PASS |
| C7 | TrimLimit_Short_PlacesBelowBid: 4999.75=5000.00-1x0.25 (line 1478-1479) | PASS |
| C8 | FlattenLimit_Long_PlacesAboveAsk: 5000.75=5000.25+2x0.25 (line 1487-1488) | PASS |
| C9 | FlattenLimit_Short_PlacesBelowBid: 4999.50=5000.00-2x0.25 (line 1496-1497) | PASS |
| C10 | TrimLimit_FallsBackToMarket_WhenAskIsZero: 3 Record.Exception calls lines 1505-1511 | PASS |

**Section C: 10/10 PASS**

---

## Section D — 7 Independent Scans (Layer 3)

### D1 — lock() actual statements
Command: Select-String -Pattern "lock\s*\("
Result: 4 comment-only hits in CopyEngine.cs (lines 319, 562, 793, 1206). Zero actual lock().
**PASS**

### D2 — async void
Command: Select-String -Pattern "async void \w+"
Result: 0 hits.
**PASS**

### D3 — return null in B19 new methods
Command: Select-String -Pattern "return null;"
Result: Hits at CopyEngine.cs:647,1047,1053,1106 and TradeCopierPanel.cs:360 — ALL pre-existing
(FindFollowerBracketOrder, FindRule, FindPriceCanvasPanel). B19 new methods return double/void only.
**PASS (0 in B19 code)**

### D4 — Stale 3-arg Trim/Flatten calls
Command: Select-String -Path TradeCopierPanel.cs -Pattern "_engine\.(Trim|Flatten)"
Result: 8 hits — all are either 1-arg market overload or 4-arg ask/bid. Zero 3-arg refPrice calls.
Lines: 754(1-arg), 756(4-arg), 780(1-arg), 782(4-arg), 1275(1-arg legacy), 1280(1-arg legacy), 1475(4-arg), 1476(4-arg).
**PASS**

### D5 — GetRefPrice residue
Command: Select-String -Path TradeCopierPanel.cs -Pattern "GetRefPrice"
Result: 4 comment-only hits (lines 6, 40, 45, 876). Zero live method definitions or call sites.
**PASS**

### D6 — .Ask/.Bid without .Price
Command: Select-String -Path TradeCopierPanel.cs -Pattern ".Ask[^.]|.Bid[^.]"
Result: 4 hits. Lines 875, 888: comments. Lines 882, 895: var ask=md.Ask / var bid=md.Bid
(local var assignments — .Price accessed on next line: ask.Price line 884, bid.Price line 897).
Source lines 878-898 read and confirmed. NT8-032 compliant.
**PASS**

### D7 — PTT-TrimLimit|PTT-FlattenLimit CreateOrder literals
Command: Select-String -Path CopyEngine.cs -Pattern "PTT-TrimLimit|PTT-FlattenLimit"
Result: 9 total hits. CreateOrder signal literals:
- Line 928: "PTT-TrimLimit" in Trim(Instrument,int,double,double) CreateOrder call
- Line 968: "PTT-FlattenLimit" in Flatten(Instrument,int,double,double) CreateOrder call
Other hits are comments and StatusUpdate error strings (not CreateOrder calls).
**PASS — exactly 2 CreateOrder signal literals**

---

## Section D Summary

| Scan | Pattern | Result |
|------|---------|--------|
| D1 | lock() actual | 0 actual (4 comment-only) | PASS |
| D2 | async void | 0 | PASS |
| D3 | return null in B19 | 0 (pre-existing elsewhere) | PASS |
| D4 | Stale 3-arg calls | 0 stale | PASS |
| D5 | GetRefPrice live | 0 live | PASS |
| D6 | .Ask/.Bid without .Price | 0 violations | PASS |
| D7 | PTT signal names | Exactly 2 CreateOrder literals | PASS |

---

## Section E — Layer 2 vs Layer 3 Cross-Check

| Scan | Engineer (L2) | Verifier (L3) | Discrepancy? |
|------|---------------|---------------|-------------|
| SCAN-01 | 0 actual lock, 4 comment hits | 0 actual lock, 4 comment hits | NONE |
| SCAN-02 | 0 results | 0 results | NONE |
| SCAN-03 | 0 in B19 methods | 0 in B19 methods; pre-existing elsewhere | NONE |
| SCAN-04 | 0 GetRefPrice calls | 0 stale 3-arg calls | NONE |
| SCAN-05 | 0 live definitions/calls | Comment-only (lines 6,40,45,876) | NONE |
| SCAN-06 | 0 violations | Local var assignments + .Price | NONE |
| SCAN-07 | 2 CreateOrder literals (928, 968) | 2 CreateOrder literals (928, 968) | NONE |

**E1-E2: Zero discrepancies between L2 and L3. All scans confirmed accurate.**

---

## Jane Street DNA

| Rule | Result |
|------|--------|
| JS-021 — no lock() | PASS |
| JS-001 — no throw in hot path | PASS — try/catch; no rethrow |
| JS-002 — no return null | PASS — GetAsk/GetBid return 0.0 |
| JS-010 — private ctor on CopyEngine | PASS — singleton unchanged |
| JS-023 — volatile on permitted types | PASS — no new volatile |
| JS-033 — no async void | PASS |
| CYC <= 8 | PASS — max CYC=6 |

---

## NT8 Compiler Rules

| Rule | Result |
|------|--------|
| NT8-007 — CreateOrder arg 12 = (NinjaTrader.Cbi.CustomOrder)null | PASS |
| NT8-013 — DateTime.MaxValue | PASS |
| NT8-014 — "PTT-" prefix on signal names | PASS |
| NT8-032 — .Ask/.Bid via .Price + null guard | PASS |

---

## Final Verdict

**VERIFY_PASS**

- Section A (9/9): PASS
- Section B (7/7): PASS
- Section C (10/10): PASS
- Section D (7/7 scans): PASS
- Section E cross-check: 0 discrepancies

Zero violations in B19 new/modified code. Zero discrepancies between engineer L2 and verifier L3.
All test arithmetic independently verified. DW-B19-LIMIT-PRICE-01 implementation is correct,
complete, and compliant. Cleared for Phase 5 plan-reviewer review.