# Ticket 2 Verification -- FlattenOneAccountLimit + TrimOneAccountLimit

## Scope: TICKET 2 ONLY (WAVE1-LANE-A-07 + WAVE1-LANE-A-08)
**Verifier**: PTT Verifier (Phase 4b)
**Date**: 2026-09-07
**Source file**: `src/PropTraderTools/CopyEngine.cs` (READ-ONLY, branch: main)
**Input**: `docs/brain/WAVE1-LANE-A/ticket-2-completion.md`

---

## Independent CCN Scan (lizard)

| Method | Engineer Reported | Verifier Measured | Match? |
|--------|------------------|-------------------|--------|
| `TrimOneAccountLimit` | 6 | **6** | YES |
| `SubmitLimitExitOrder` | 3 | **3** | YES |
| `FlattenOneAccountLimit` | 6 | **6** | YES |

All three methods: CCN <= 8 (JS-080 compliant). Lizard source lines confirmed:
- `TrimOneAccountLimit`: lines 5544-5566
- `SubmitLimitExitOrder`: lines 5573-5602
- `FlattenOneAccountLimit`: lines 5608-5629

---

## Independent 7-Scan Results

| Scan | Description | Engineer Reported | Verifier Result | Match? | Verdict |
|------|-------------|------------------|-----------------|--------|---------|
| SCAN-01 | `lock(` usage (non-comment) | 0 hits | **0 hits** | YES | PASS |
| SCAN-02 | `async void` usage | 0 hits | **0 hits** | YES | PASS |
| SCAN-03 | `return null` in target methods | 0 hits | **0 hits** | YES | PASS |
| SCAN-04 | CCN for target methods | CCN<=8 | **CCN<=8** (6/3/6) | YES | PASS |
| SCAN-05 | PTT- prefix + NT8-007 arg12 | PASS | **PASS** | YES | PASS |
| SCAN-06 | ASCII-only in target methods | 0 hits | **0 hits** | YES | PASS |
| SCAN-07 | `public.*SubmitLimitExitOrder` | 0 hits | **0 hits** | YES | PASS |

SCAN-05 detail (verifier confirmed):
- Line 5564: `SubmitLimitExitOrder(acc, instrument, action, trimQty, limitPx, "PTT-TrimLimit");`
- Line 5595: `(NinjaTrader.Cbi.CustomOrder)null` inside `SubmitLimitExitOrder` body
- Line 5627: `SubmitLimitExitOrder(acc, instrument, action, pos.Quantity, limitPx, "PTT-FlattenLimit");`

---

## Implementation Correctness Check

| Check | Expected | Actual (source) | Result |
|-------|----------|-----------------|--------|
| `SubmitLimitExitOrder` is `private void` | YES | `private void SubmitLimitExitOrder(` (line 5573) | PASS |
| 6 parameters on `SubmitLimitExitOrder` | 6 params | `acc, instrument, action, qty, limitPx, orderName` | PASS |
| NT8-007: arg12 = `(NinjaTrader.Cbi.CustomOrder)null` | Present in body | Line 5595 confirmed | PASS |
| `FlattenOneAccountLimit` passes `pos.Quantity` (full) | qty=pos.Quantity | Line 5627: `pos.Quantity` | PASS |
| `TrimOneAccountLimit` passes `(int)Math.Ceiling(pos.Quantity / 2.0)` | half-ceil | Line 5559: `int trimQty = (int)Math.Ceiling(pos.Quantity / 2.0);` passed at 5564 | PASS |
| `FlattenOneAccountLimit` retains `CancelStaleExitOrders` | YES | Line 5616: `CancelStaleExitOrders(acc, instrument, "PTT-FlattenLimit");` | PASS |
| `TrimOneAccountLimit` retains `CancelStaleExitOrders` | YES | Line 5552: `CancelStaleExitOrders(acc, instrument, "PTT-TrimLimit");` | PASS |
| `FlattenOneAccountLimit` retains `StatusUpdate` success log | YES | Line 5628: `StatusUpdate?.Invoke(acc.Name + ": flatten-limit " ...)` | PASS |
| `TrimOneAccountLimit` retains `StatusUpdate` success log | YES | Line 5565: `StatusUpdate?.Invoke(acc.Name + ": trim-limit " ...)` | PASS |
| External signatures of both parent methods unchanged | 5 params each | Both `(Account, Instrument, int, double, double)` unchanged | PASS |
| `acc.Submit` NOT called in helper | Correct -- omit Submit | Not present in `SubmitLimitExitOrder` body | PASS |

---

## NT8-007 arg12 Verified

**YES** -- exact quote from source (line 5595):
```csharp
(NinjaTrader.Cbi.CustomOrder)null
```
Position: 12th argument to `acc.CreateOrder(...)` inside `SubmitLimitExitOrder`. Matches all other NT8-007 call sites in the file.

---

## qty Computation Verified

**FlattenOneAccountLimit** (full flatten):
- Line 5627: `SubmitLimitExitOrder(acc, instrument, action, pos.Quantity, limitPx, "PTT-FlattenLimit")`
- Passes `pos.Quantity` directly -- no division. Correct.

**TrimOneAccountLimit** (half trim, ceiling):
- Line 5559: `int trimQty = (int)Math.Ceiling(pos.Quantity / 2.0);`
- Line 5564: `SubmitLimitExitOrder(acc, instrument, action, trimQty, limitPx, "PTT-TrimLimit")`
- Ceiling division confirmed. Example: qty=7 -> ceil(3.5)=4 (matches T42 test).

---

## Test Verification

Verifier ran: `dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj`

**Result: 157 passing / 0 failing / 3 skipped (Total: 160)**

Note: Engineer reported 143 passing at time of implementation. Verifier measures 157 because
subsequent tickets (WAVE1-LANE-B, WAVE1-LANE-C) added additional tests before this verification run.
The delta from engineer baseline (143) is consistent with all tests passing and no regressions.

T39-T42 test methods confirmed present in `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`:

| Test | Line | What It Verifies |
|------|------|-----------------|
| `SubmitLimitExitOrder_UsesSellAction_WhenPositionIsLong` | 789 | isLong=true -> Sell branch |
| `SubmitLimitExitOrder_UsesBuyToCoverAction_WhenPositionIsShort` | 798 | isLong=false -> BuyToCover branch |
| `FlattenOneAccountLimit_UsesFullPositionQty_WhenCalled` | 807 | posQty=7 -> flattenQty=7 |
| `TrimOneAccountLimit_UsesHalfPositionQty_WhenCalled` | 816 | posQty=7 -> trimQty=4 (ceiling) |

---

## DNA Rule Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | SCAN-01: 0 lock() in target methods | PASS |
| JS-002 (no return null) | SCAN-03: 0 return null in target methods | PASS |
| JS-080 (CYC <= 8) | All three methods CCN <= 8 | PASS |
| NT8-007 (arg12 CustomOrder null) | Line 5595 confirmed | PASS |
| NT8-014 (PTT- prefix) | "PTT-TrimLimit" and "PTT-FlattenLimit" confirmed | PASS |
| ASCII-only | SCAN-06: 0 non-ASCII in target lines | PASS |
| private visibility on helper | SCAN-07: 0 public hits | PASS |

---

## VERIFY_PASS