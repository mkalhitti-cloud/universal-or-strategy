# PTT-COPIER-B19 — Final Review

**Block**: PTT-COPIER-B19
**Status**: COMPLETE — FINAL_PASS
**Date**: 2026-07-14
**Reviewer**: Director (post-lane verification)

---

## Summary

B19 delivered two parallel lane fixes targeting account reconnect safety and limit-exit price anchoring.

| Lane | Ticket | Description | Tests Added | Status |
|------|--------|-------------|-------------|--------|
| Lane 1 | DW-B19-COPIER-BUG-01 (P0) | Gate 2 Account ref-equality → name equality | +2 [Fact] | CLOSED |
| Lane 2 | DW-B19-LIMIT-PRICE-01 (P1) | GetRefPrice() Last.Price → GetAsk()/GetBid() ask/bid anchor | +5 [Fact] | CLOSED |

**[Fact] count**: 111 (B18 baseline) → 113 (Lane 1) → 118 (Lane 2 final)

---

## Lane 1 — DW-B19-COPIER-BUG-01

**Root cause**: `CopyEngine.OnOrderUpdate` Gate 2 used `e.Order.Account == rule.MasterAccount`
(C# object reference equality). After Rithmic reconnect at 16:43 (log.20260713.00002.txt),
NinjaTrader recreates Account objects. Stale reference → Gate 2 always false → zero follower orders.

**Fix**: `CopyEngine.cs` line 381 — changed to `e.Order.Account.Name == rule.MasterAccount?.Name`
(string name equality, null-safe via `?.`).

**Tests added**:
- `Gate2_UsesAccountName_SourceContractVerified` (line 1958) — reflection type-contract: confirms `CopyRule.MasterAccount` is `Account` and `Account.Name` is `string`.
- `Gate2_NullMasterAccount_NoCopyOrder` (line 1988) — null-safety guard: null `MasterAccount` evaluates to null name without throwing.

**5-scan result**:
| Scan | Expected | Result |
|------|----------|--------|
| `Account.Name == rule.MasterAccount` at line 381 | 1 match | ✅ PASS |
| Old `e.Order.Account == rule.MasterAccount[^.]` ref-eq | 0 | ✅ PASS |
| `Gate2_UsesAccountName_SourceContractVerified` | present | ✅ PASS (line 1958) |
| `Gate2_NullMasterAccount_NoCopyOrder` | present | ✅ PASS (line 1988) |
| `[Fact]` count after Lane 1 | 113 | ✅ PASS |

**JS compliance**: JS-021 (no lock) PASS. JS-001 (no throw in hot path) PASS. CYC unchanged at 7.

---

## Lane 2 — DW-B19-LIMIT-PRICE-01

**Root cause**: `TradeCopierPanel.GetRefPrice()` returned `instrument.MarketData.Last.Price`
for both long and short Trim/Flatten limit exits. Last.Price lags the live spread — long exits
(Sell Limit) should anchor to Ask; short exits (BuyToCover Limit) should anchor to Bid.

**Fix**:
- `CopyEngine.cs`: New `internal static double ComputeLimitPx(bool isLong, double ask, double bid, int exitBuffer, double tickSize)` pure-arithmetic helper. `Trim`/`Flatten` 4-arg overloads updated to call `ComputeLimitPx`.
- `TradeCopierPanel.cs`: `GetRefPrice()` removed. Added `GetAsk()` + `GetBid()` (null-guarded, CYC=4 each). `OnTrimClick`, `OnFlattenClick`, `DispatchShortcut` Key.T/Key.F updated to pass `GetAsk(), GetBid()`.

**Tests added** (5 new [Fact]):
- `TrimLimit_Long_PlacesAboveAsk` — long: ask + 1 tick = 5000.50
- `TrimLimit_Short_PlacesBelowBid` — short: bid − 1 tick = 4999.75
- `FlattenLimit_Long_PlacesAboveAsk` — long: ask + 2 ticks = 5000.75
- `FlattenLimit_Short_PlacesBelowBid` — short: bid − 2 ticks = 4999.50
- `TrimLimit_FallsBackToMarket_WhenAskIsZero` — ask=0/bid=0/buffer=0 → market fallback guard

**7-scan result**:
| Scan | Expected | Result |
|------|----------|--------|
| `GetRefPrice` in CopyEngine.cs | 0 | ✅ PASS |
| `GetAsk`/`GetBid` in TradeCopierPanel.cs | both present | ✅ PASS (lines 751, 752, 777, 778, 877, 890) |
| `Trim(instr, buf, ask, bid)` / `Flatten(instr, buf, ask, bid)` 4-arg | present | ✅ PASS (lines 906, 947) |
| `GetAsk`/`GetBid` call sites in TradeCopierPanel.cs | present | ✅ PASS (Key.T/Key.F lines 1475-1476) |
| `GetRefPrice` in TradeCopierPanel.cs (live calls) | 0 live calls | ✅ PASS (4 comment-only refs) |
| B19 test names present | 5 matches | ✅ PASS |
| `[Fact]` count after Lane 2 | 118 | ✅ PASS |

**P0 safety**: 0 `lock()` calls in TradeCopierPanel.cs. 0 `lock()` calls in CopyEngine.cs (4 comment-only refs). 0 `async void`. JS-021 PASS.

---

## Deferred to B20

| ID | File | Line | Description | Priority |
|----|------|------|-------------|----------|
| DW-B19-02 | CopyEngine.cs | ~659 | `PopulateOrderMap` Account ref-equality dedup guard — after reconnect may produce duplicate FollowerBinding entries | P2 |
| DW-B17-SYNC-01 | CopyEngine.cs/TradeCopierPanel.cs | — | Copy ON/OFF state not synced between Panel and Window | P2 |
| DW-B17-ACCOUNT-NAME-01 | Display layer | — | Strip `!Apex!Apex` broker suffix at display | P2 |

---

## Block Metrics

| Metric | Value |
|--------|-------|
| Files changed | `CopyEngine.cs`, `TradeCopierPanel.cs`, `CopyEngineTests.cs` |
| [Fact] baseline | 111 (B18) |
| [Fact] final | 118 |
| Tests added | 7 (+2 Lane 1, +5 Lane 2) |
| JS P0 violations | 0 |
| NT8 violations | 0 |
| New NT8 rules | NT8-032 (confirmed existing — `MarketData.Bid/.Ask` return `MarketDataEventArgs`, use `.Price`) |
| Lamport clock | 8 |

**FINAL_PASS**
