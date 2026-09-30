# B34 Plan Review — 02-plan-review.md
<!-- PTT-COPIER B34 | ptt-plan-reviewer | 2026-07-27 -->

## Result: REVIEW_PASS

**Reviewer:** ptt-plan-reviewer  
**Plan reviewed:** `docs/brain/B34-multiAcct/02-architecture-plan.md`  
**Violations found:** 0 blocking (P0/P1)  
**Advisories:** 4 (non-blocking, noted for engineer awareness)

---

## 1. Violations Table

| # | Rule ID | Severity | Description | Location in Plan | Verdict |
|---|---------|----------|-------------|-----------------|---------|
| — | — | — | No violations found | — | ALL PASS |

No P0 or P1 rule violations were identified. Plan proceeds to Phase 3 (ticket generation).

---

## 2. Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| Fix DW-B33-05: `isLong` must be per-account inside foreach | ✅ YES | §4.1.1 — `isLong = pos.MarketPosition == Long` inside foreach |
| Fix DW-B33-06: `bePrice` must use per-account `AveragePrice` + direction-aware buffer | ✅ YES | §4.1.1 — `bePrice = pos.AveragePrice + (isLong ? +buf : -buf) * tickSize` |
| Fix DW-B33-07: `CancelStaleBracketsLocal` must be called per-account inside foreach | ✅ YES | §4.1.1 — `CancelStaleBracketsLocal(acc, ctx.Instrument)` inside loop |
| Add `BeBuffer`, `TrimBuffer`, `FlatBuffer` to `IPttHostContext` | ✅ YES | §4.2.1 — 3 `int` properties added |
| Add `Ask`, `Bid` to `IPttHostContext` | ✅ YES | §4.2.1 — 2 `double` properties added |
| Wire buffer props via `TradeCopierPanel` existing fields | ✅ YES | §4.2.2 — explicit interface implementations |
| Use Limit order for Trim/Flatten when buffer > 0 | ✅ YES | §4.3.2 / §4.3.3 |
| Fall back to Market order when buffer == 0 | ✅ YES | §4.3.2 — `else { orderType = OrderType.Market; }` |
| B34-02 must be implemented before B34-01 (compile dependency) | ✅ YES | §3 — bold warning + implementation order stated |
| 6 new `[Fact]` tests (171 baseline → 177 minimum) | ✅ YES | §7 — all 6 named; §4 — SCAN-07 checks `>= 177` |
| 7-scan checklist present | ✅ YES | §8 — SCAN-01 through SCAN-07 |
| No scope creep beyond 4 tickets | ✅ YES | §9 — extras deferred explicitly as DW-B34-RAISE-01, DW-B34-TRIM-02 |

All 12 spec requirements are addressed in the plan.

---

## 3. Per-Check Findings

### 3.1 P0 Bug Identification and Fix Design

**DW-B33-05 — `isLong` outside loop:**  
- Identified correctly in §1 table.  
- Fix: `isLong = pos.MarketPosition == MarketPosition.Long` inside foreach per `pos` (not `leaderPos`). ✅  

**DW-B33-06 — `bePrice` from leader only, no buffer, no sign flip:**  
- Identified correctly in §1 table.  
- Fix: `bePrice = pos.AveragePrice + (isLong ? +buf : -buf) * tickSize` per-account. ✅  
- Sign convention verified: long → stop above entry (buf ticks profit); short → stop below entry (buf ticks profit on BuyToCover). Formula is correct.  

**DW-B33-07 — `CancelStaleBracketsLocal` called once before loop:**  
- Identified correctly in §1 table.  
- Fix: moved inside foreach, invoked as `CancelStaleBracketsLocal(acc, ctx.Instrument)` before each `SubmitBeStopLocal`. ✅  

### 3.2 Compile Dependency Handling

Plan §3 states implementation order explicitly: **B34-02 → B34-01 → B34-03 → B34-04**, with a bold warning to the engineer that B34-01 references `ctx.BeBuffer` (added by B34-02) and B34-03 references `ctx.TrimBuffer`, `ctx.FlatBuffer`, `ctx.Ask`, `ctx.Bid` (all added by B34-02). Dependency handling is correct and prominently communicated. ✅

### 3.3 IPttHostContext Additions — NT8-001 Compliance

Plan §4.2.1 adds 5 properties to the interface:
```csharp
int    BeBuffer   { get; }
int    TrimBuffer { get; }
int    FlatBuffer { get; }
double Ask        { get; }
double Bid        { get; }
```
All are plain getter-only interface properties. No `{ get; init; }` accessor used. NT8-001 compliance: ✅

### 3.4 TradeCopierPanel Explicit Interface Implementations

Plan §4.2.2 uses explicit interface form with full brace syntax:
```csharp
int    IPttHostContext.BeBuffer   { get { return _beBuffer; } }
int    IPttHostContext.TrimBuffer { get { return _trimBuffer; } }
int    IPttHostContext.FlatBuffer { get { return _flattenBuffer; } }
double IPttHostContext.Ask        { get { return GetAsk(); } }
double IPttHostContext.Bid        { get { return GetBid(); } }
```
Backing fields (`_beBuffer`, `_trimBuffer`, `_flattenBuffer`) and methods (`GetAsk()`, `GetBid()`) are confirmed present in TradeCopierPanel at baseline. No NT8 rule violation. ✅

### 3.5 B34-03 Ask/Bid Feasibility

Plan §4.3.1 correctly resolves the access problem: `TradeCopierPanel.GetAsk()` and `GetBid()` are private, so they cannot be called from `PttTrim.Execute()` directly. B34-02 bridges them via `IPttHostContext.Ask` and `IPttHostContext.Bid`. B34-03 then reads `ctx.Ask` / `ctx.Bid`. The delegation chain is sound.

NT8-032 (MarketData.Ask/Bid are `MarketDataEventArgs` — use `.Price`, full null-guard required): B34 does **not** change `GetAsk()` / `GetBid()`. These are pre-existing methods confirmed present in TradeCopierPanel. The existing implementation already handles NT8-032 requirements. ✅

### 3.6 CYC Targets

| Method | Before | After | Target |
|--------|--------|-------|--------|
| `PttBreakEven.Execute()` | 4 | 7 | ≤ 8 ✓ |
| `PttTrim.TrimPositionLocal()` | 2 | 7 | ≤ 8 ✓ |
| `PttFlatten.FlattenPositionLocal()` | 2 | 7 | ≤ 8 ✓ |
| `PttTrim.Execute()` | 3 | 3 | ≤ 8 ✓ |
| `PttFlatten.Execute()` | 3 | 3 | ≤ 8 ✓ |
| Interface property getters | — | 1 each | ≤ 8 ✓ |

All CYC values ≤ 8. Detailed count for `Execute()` (7) verified: start(1) + `if(!IsEnabled)`(+1) + `if(null||qty==0)` with `||`(+2) + `foreach`(+1) + inner `if(null||qty==0)` with `||`(+2) = 7. ✅

### 3.7 Test Names and Strategy

All 6 test names present in §7 inventory table, cross-referenced to ticket and file. Each test has a clear assertion contract. ✅

| # | Test Name | Ticket | Strategy | Sound? |
|---|-----------|--------|----------|--------|
| 1 | `T_B34_BE_ShortAccountBuyToCover` | B34-01 | Subclass override capture direction | ✅ |
| 2 | `T_B34_BE_PerAccountBePrice` | B34-01 | Two accounts, two distinct bePrices | ✅ |
| 3 | `T_B34_BE_CancelBeforeSubmitPerAccount` | B34-01 | Call-count observer per-account | ✅ |
| 4 | `T_B34_BE_BufferShortFlipped` | B34-01 | Numeric: buf=2, tick=0.25, avg=100 → 99.5 | ✅ |
| 5 | `T_B34_ContextBeBuffer_Forwarded` | B34-02 | Reflection: `_beBuffer=3` → `ctx.BeBuffer==3` | ✅ |
| 6 | `T_B34_Trim_BufferContextWired` | B34-03 | Interface property type check + optional limit path | ✅ |

### 3.8 7-Scan Checklist

Plan §8 contains all 7 scans:

| Scan | Pattern | Covers | Present? |
|------|---------|--------|---------|
| SCAN-01 | `grep "lock("` | JS-021 | ✅ |
| SCAN-02 | `grep "async void "` | JS-033 | ✅ |
| SCAN-03 | `grep -E "\.Where|\.First|\.Select|\.Any"` | NT8-006 | ✅ |
| SCAN-04 | `grep "{ get; init; }"` | NT8-001 | ✅ |
| SCAN-05 | `grep "acc\.Positions\["` | NT8-050 | ✅ |
| SCAN-06 | NT8 F5 gate | NT8 compilation | ✅ |
| SCAN-07 | `[Fact]` count ≥ 177 | Test baseline | ✅ |

### 3.9 JS Rules Gate

| Rule | B34-01 | B34-02 | B34-03 | Notes |
|------|--------|--------|--------|-------|
| JS-021 No `lock()` | PASS | PASS | PASS | No locking introduced anywhere |
| JS-033 No `async void` | PASS | PASS | PASS | All methods sync `void` |
| JS-001 No throw in hot path | PASS | PASS | PASS | No new throws; existing try/catch in helpers preserved |
| JS-002 No `return null` | PASS | PASS | PASS | `return` from `void Execute()` is not `return null`; null guard uses `continue` inside loop |

### 3.10 NT8 Rules Gate

| Rule | B34-01 | B34-02 | B34-03 | Notes |
|------|--------|--------|--------|-------|
| NT8-001 No `{ get; init; }` | N/A | PASS | N/A | `{ get { return _field; } }` pattern used throughout |
| NT8-006 No LINQ | PASS | N/A | PASS | Explicit foreach; no `.Where`/`.First`/`.Select`/`.Any` |
| NT8-007 `arg11 = (CustomOrder)null` | PASS (unchanged) | N/A | PASS (unchanged) | CreateOrder calls preserved from B33 |
| NT8-013 `DateTime.MaxValue` | PASS (unchanged) | N/A | PASS (unchanged) | GTC expiry unchanged |
| NT8-014 Signal `"PTT-"` prefix | PASS (unchanged) | N/A | PASS (unchanged) | `"PTT-BE-Stop"`, `"PTT-Trim"`, `"PTT-Flatten"` |
| NT8-049 `arg6=limitPrice, arg7=stopPrice` | PASS (unchanged) | N/A | PASS — `arg6=limitPrice, arg7=0` for Limit | NT8-049 note in §4.3.4 is accurate |
| NT8-050 `FindPositionLocal` | PASS | N/A | PASS (unchanged) | All position lookups via existing helper |

---

## 4. Advisories (Non-Blocking)

The following items are not rule violations. They are flagged for engineer awareness only. No action required before ticket generation.

### ADV-01 — SCAN-07 Does Not Explicitly Cover NT8-049 on New B34-03 `CreateOrder` Calls

**§8 scan checklist** does not include a grep for arg6/arg7 order correctness on the new `OrderType.Limit` call paths introduced in B34-03.  
**Mitigated by:** §4.3.4 compliance table documents NT8-049 as PASS with explicit note `"Limit: arg6=limitPrice, arg7=0"`. The manual compliance assertion is sufficient.  
**Recommendation:** Engineer should manually verify arg order on the new `CreateOrder` call during implementation. No scan addition required.

### ADV-02 — `T_B34_BE_ShortAccountBuyToCover` Primary Mechanism Depends on Subclass Override

Test §4.1.3 describes a subclass override as the primary mechanism to capture the direction argument. If `SubmitBeStopLocal` is `private static`, subclass override is not possible and the IL-reflection fallback must be used. IL-reflection tests are fragile. Engineer should confirm `SubmitBeStopLocal` visibility before choosing the implementation pattern; if private, a test-seam (e.g., protected virtual wrapper) may be needed.  
**Does not block plan approval.** Implementation detail for the engineer.

### ADV-03 — NT8-029 Tick Alignment Not Explicitly Called Out

`bePrice = pos.AveragePrice + (isLong ? +buf : -buf) * tickSize` implicitly preserves tick alignment (integer tick count × tick size, added to a tick-aligned entry price). NT8-029 is satisfied by the math. The plan does not reference NT8-029 explicitly.  
**No action needed** — the formula is correct. Advisory for completeness.

### ADV-04 — `T_B34_Trim_BufferContextWired` Is a Thin Type-System Test

Test §4.3.5 primarily asserts that `IPttHostContext.TrimBuffer` is a non-null `int` property (type-system check via reflection). The behavioral assertion (limit order path when TrimBuffer > 0) is marked "optionally verifies."  
**Recommendation:** Engineer should attempt to promote the limit-order-path assertion from optional to required in the [Fact] body, given it is the direct behavioral claim of B34-03.  
**Does not block plan approval.**

---

## 5. Final Disposition

**REVIEW_PASS**

- Zero P0 violations
- Zero P1 violations
- All 12 spec requirements addressed
- Compile dependency order explicit and correct
- All 6 test names present with clear contracts
- All 7 scans present
- JS-021, JS-033, JS-001, JS-002 satisfied across all 3 code-change tickets
- NT8-001, NT8-006, NT8-007, NT8-013, NT8-014, NT8-049, NT8-050 satisfied
- CYC ≤ 8 on all modified methods
- 4 non-blocking advisories logged for engineer awareness (ADV-01 through ADV-04)

**Next phase:** ptt-architect → generate `docs/brain/B34-multiAcct/04-tickets.md`

---

*Reviewer: ptt-plan-reviewer | Block: B34 | Phase 2 | 2026-07-27*
