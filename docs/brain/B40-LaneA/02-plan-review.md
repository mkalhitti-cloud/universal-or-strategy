# B40 Plan Review

**Date**: 2026-07-30
**Reviewer**: ptt-plan-reviewer
**Block**: B40-LaneA — BE ALL Armed/Wait + OCO Collision Fix
**Plan File**: `docs/brain/B40-LaneA/02-architecture-plan.md`
**Spec**: `specs/002-trade-copier-spec.html#section-b40`
**Verdict**: REVIEW_FAIL

---

## Checklist Results

| # | Check | Result | Citation |
|---|-------|--------|----------|
| **FIX 1 — OCO** | | | |
| 1 | Root cause (accName[0..4] collision) identified and addressed | PASS | Plan §2 DW-B39-OCO-01; spec line 18030–18033 |
| 2 | `BuildGlobalBeOcoId` format = `"PTT-BEG-NNNNN-accIdx-pairIdx"` | PASS | Plan §4 PttGlobalBreakEven; spec line 18271 |
| 3 | `PttBreakEven.cs` explicitly marked UNCHANGED | PASS | Plan §8 item 4; spec line 18308 |
| **FIX 2 — Armed** | | | |
| 4 | `ArmAllPendingBe` returns `int armedCount` | PASS | Plan §4 `internal int ArmAllPendingBe(int bufferTicks)` |
| 5 | `IsPriceAlreadyAtBeForAccount` uses per-account bid/ask (not global price feed) | **FAIL** | See Violation V-01 below |
| 6 | `ComputeBePrice` is pure/static with null-coalesce tick size | PASS | Plan §4 `private static double ComputeBePrice`; `?? 0.25` |
| 7 | `IsPendingSlotsEmpty` returns `_pendingBeSlots.IsEmpty` | PASS | Plan §4 `=> _pendingBeSlots.IsEmpty` |
| 8 | `TradeCopierPanel _globalBeState` field + `UpdateBeAllVisuals` + `OnBeAllClick` FSM | PASS | Plan §9 T2 |
| 9 | `TradeCopierWindow` mirrors Panel changes exactly | PASS | Plan §9 T2 Window section |
| 10 | `Detach()` cleanup loops both Panel and Window | PASS | Plan §9 T2 Detach() |
| **JS Rules** | | | |
| 11 | No `lock()` anywhere (JS-021) | PASS | Plan §6; `volatile int` + Interlocked + ConcurrentDictionary |
| 12 | No `async void` (JS-033) | PASS | All new methods are synchronous; event handlers are plain `void` |
| 13 | No `return null` (JS-002) | PASS | New methods return `int`, `bool`, `double`, `string` — all non-null expression bodies |
| 14 | No `throw new` in hot path (JS-001) | PASS | No throw statements in any new method |
| 15 | CYC ≤ 8 for all new methods (plan max ≤ 5) | PASS | Plan §3 and §9 SCAN-05: max = 5 (ArmAllPendingBe) |
| **NT8 Rules** | | | |
| 16 | `volatile int` allowed (NT8-003 bans `volatile double` only) | PASS | Plan §4 field comment confirms; NT8-003 scope is `double` only |
| 17 | No `init` properties (NT8-001) | PASS | No `{ get; init; }` in any proposed signature |
| 18 | No record types (NT8-002) | PASS | No `record` types introduced |
| **Test Coverage** | | | |
| 19 | 12 `[Fact]` tests T_B40_01–T_B40_12 | PASS | Plan §7; spec line 18488 |
| 20 | Both positive and negative cases covered | PASS | T_B40_11 (true) and T_B40_12 (false) for `IsPriceAlreadyAtBeForAccount`; T_B40_06/07 for long/short |
| 21 | Baseline stated as 202 → 214 | PASS | Plan §7 and §9 T3 |
| **Ticket Structure** | | | |
| 22 | 3 tickets: T1 (engine+OCO), T2 (UI), T3 (tests) | PASS | Plan §9 T1/T2/T3 |
| 23 | Each ticket has 7-scan checklist (SCAN-01 through SCAN-07) | PASS | Plan §9 each ticket ends with SCAN-01..07 |
| 24 | Sequential order T1→T2→T3 (T2 depends on T1 interface) | PASS | Plan §9 ordering; T2 calls `CopyEngine.Instance.ArmAllPendingBe` defined in T1 |

---

## Violations

### V-01 — SPEC DEVIATION: Wrong market data API in `IsPriceAlreadyAtBeForAccount`

**Rule violated**: Spec compliance (FIX 2 requirement)
**Severity**: P1

**Spec requirement** (spec `#section-b40`, line 18366):
> Uses `acc.Get(AccountItem.BidPrice)` and `acc.Get(AccountItem.AskPrice)` per-account
> so each account uses its own live market data. CYC=4 (null+flat+long+short).

**Plan implementation** (plan §4, `IsPriceAlreadyAtBeForAccount` comment):
> `NT8-032: uses pos.Instrument.MarketData.Bid/Ask.Price (null-guarded)`

**Problem**: The plan uses `pos.Instrument.MarketData.Bid/Ask.Price` — the global instrument-level
market data feed shared across all accounts. The spec explicitly requires `acc.Get(AccountItem.BidPrice)`
and `acc.Get(AccountItem.AskPrice)` — per-account data from NT8's `AccountItem` enumeration.

The spec's rationale is stated explicitly: each account must use its **own** live market data so that
multi-account scenarios (e.g., accounts on different instruments or different brokers with different
spread widths) resolve the threshold independently. Using a shared instrument feed conflates per-account
data sources.

**Required fix**: Change `IsPriceAlreadyAtBeForAccount` to call `acc.Get(AccountItem.BidPrice)` /
`acc.Get(AccountItem.AskPrice)` to retrieve the per-account bid/ask. The CYC annotation in the spec
already accounts for this API path (CYC=4: null guard, flat guard, long branch, short branch).

Note: `acc.Get(AccountItem.BidPrice)` returns `double` and returns `0.0` if the account has no
market data — the null-guard logic changes slightly (check `refPx > 0` on the result), which the
plan's CYC=4 annotation already budgets for.

---

## Spec Coverage Matrix

| Spec Requirement | Addressed? | Plan Section |
|-----------------|-----------|--------------|
| DW-B39-OCO-01 (P0) — OCO collision root cause identified | Yes | §2 |
| DW-B39-OCO-01 — globally unique ID format `PTT-BEG-NNNNN-accIdx-pairIdx` | Yes | §4 `BuildGlobalBeOcoId` |
| DW-B39-OCO-01 — `volatile int _ocoSeq` + `Interlocked.Increment` in PttGlobalBreakEven | Yes | §4 PttGlobalBreakEven fields |
| DW-B39-OCO-01 — PttBreakEven.cs unchanged | Yes | §8 item 4 |
| DW-B39-BEHAVIOR-01 (P1) — Option B (armed/wait) chosen | Yes | §2 DW-B39-BEHAVIOR-01 |
| `ArmAllPendingBe` engine method, `internal int`, CYC=5 | Yes | §4 CopyEngine.cs |
| `IsPriceAlreadyAtBeForAccount` with per-account data | **Partial** — method exists, wrong API (V-01) |
| `ComputeBePrice` pure static, null-coalesce tick | Yes | §4 |
| `IsPendingSlotsEmpty` = `_pendingBeSlots.IsEmpty` | Yes | §4 |
| `_globalBeState` in Panel and Window, `UpdateBeAllVisuals` | Yes | §9 T2 |
| `OnGlobalBeClick`/`OnWindowGlobalBeClick` FSM (Idle→Armed→Idle) | Yes | §9 T2 |
| `OnPendingBeFiredDispatch` auto-reset when IsEmpty | Yes | §9 T2 |
| `Detach()` cleanup for both Panel and Window | Yes | §9 T2 |
| 12 `[Fact]` tests T_B40_01–T_B40_12, baseline 202→214 | Yes | §7, §9 T3 |
| 7-scan checklist on every ticket | Yes | §9 per-ticket SCAN blocks |
| JS-021 no lock() | Yes | §6 compliance table |
| JS-033 no async void | Yes | §6 |
| JS-002 no return null | Yes | §6 |
| JS-001 no throw in hot path | Yes | §6 |
| NT8-003 volatile int allowed (not double) | Yes | §4 field comments |
| NT8-001 no init properties | Yes (implicitly) | No init properties in any signature |
| NT8-002 no record types | Yes (implicitly) | No record types introduced |

---

## Summary

One spec violation found:

**V-01** (P1): `IsPriceAlreadyAtBeForAccount` uses the global instrument market data feed
(`pos.Instrument.MarketData.Bid/Ask.Price`) instead of per-account data
(`acc.Get(AccountItem.BidPrice/AskPrice)`) as explicitly required by spec line 18366.

All JS rules (JS-001, JS-002, JS-021, JS-033) pass. All NT8 rules pass. All CYC ≤ 5.
Ticket structure and scan checklist complete. Test baseline correct.

---

## Approved For Phase 3

**BLOCKED** — return to ptt-architect.

Fix required: Update `IsPriceAlreadyAtBeForAccount` in plan §4 to specify
`acc.Get(AccountItem.BidPrice)` / `acc.Get(AccountItem.AskPrice)` as the price source,
matching spec line 18366. Update the compliance note in plan §6 accordingly.
No other changes required.

---

*ptt-plan-reviewer | Phase 2 | B40-LaneA | 2026-07-30*

---

## Rev 2 Review

**Date**: 2026-07-30
**Reviewer**: ptt-plan-reviewer
**Cycle**: 2 of 2 (post-fix re-review)
**Verdict**: REVIEW_PASS

---

### V-01 Fix Verification

**Prior violation**: `IsPriceAlreadyAtBeForAccount` used `pos.Instrument.MarketData.Bid/Ask.Price`
(global instrument feed) instead of the per-account API required by spec line 18366.

**Fix confirmed**: Plan §4 now reads:

```
// Long:  acc.Get(AccountItem.BidPrice) >= averagePrice + bufferTicks * tickSize
// Short: acc.Get(AccountItem.AskPrice) <= averagePrice - bufferTicks * tickSize
// Per-account API: each account uses its own live market data feed.
// NT8-AccItem: uses acc.Get(AccountItem.BidPrice) / acc.Get(AccountItem.AskPrice) (null-guarded).
```

The old `pos.Instrument.MarketData.Bid/Ask.Price` wording is completely absent from the plan.
The `refPx<=0` guard in the CYC=4 annotation correctly handles the case where
`acc.Get(AccountItem.BidPrice)` returns `0.0` (account has no market data — equivalent to the
null-guard the spec requires). The CYC annotation (2: bid/ask selection, 3: refPx≤0 guard,
4: long/short comparison) is internally consistent with the spec's stated CYC=4.

**V-01 is RESOLVED.**

---

### Full Checklist (Rev 2)

All 24 checks from Rev 1 re-verified against the updated plan text:

| # | Check | Result |
|---|-------|--------|
| 1 | OCO root cause (accName[0..4] collision) identified | PASS |
| 2 | `BuildGlobalBeOcoId` format `"PTT-BEG-NNNNN-accIdx-pairIdx"` | PASS |
| 3 | `PttBreakEven.cs` UNCHANGED | PASS |
| 4 | `ArmAllPendingBe` returns `int armedCount` | PASS |
| 5 | `IsPriceAlreadyAtBeForAccount` uses `acc.Get(AccountItem.BidPrice/AskPrice)` | **PASS (FIXED)** |
| 6 | `ComputeBePrice` pure static, null-coalesce tick | PASS |
| 7 | `IsPendingSlotsEmpty` returns `_pendingBeSlots.IsEmpty` | PASS |
| 8 | Panel `_globalBeState` + `UpdateBeAllVisuals` + FSM | PASS |
| 9 | Window mirrors Panel exactly | PASS |
| 10 | `Detach()` cleanup loops both Panel and Window | PASS |
| 11 | No `lock()` (JS-021) | PASS |
| 12 | No `async void` (JS-033) | PASS |
| 13 | No `return null` (JS-002) | PASS |
| 14 | No `throw new` in hot path (JS-001) | PASS |
| 15 | CYC ≤ 8 all new methods (max = 5) | PASS |
| 16 | `volatile int` allowed — NT8-003 bans `volatile double` only | PASS |
| 17 | No `init` properties (NT8-001) | PASS |
| 18 | No `record` types (NT8-002) | PASS |
| 19 | 12 `[Fact]` tests T_B40_01–T_B40_12 | PASS |
| 20 | Both positive and negative cases for `IsPriceAlreadyAtBeForAccount` | PASS |
| 21 | Test isolation strategy specified (`[InternalsVisibleTo]` + test-seam overload) | PASS |
| 22 | 3 tickets: T1 (engine+OCO), T2 (UI), T3 (tests) | PASS |
| 23 | Each ticket has 7-scan checklist SCAN-01..07 | PASS |
| 24 | Sequential order T1→T2→T3 enforced | PASS |

**Violations found in Rev 2**: 0

---

### Approved For Phase 3

**REVIEW_PASS** — plan is cleared for ticket generation.

No further architect changes required.

---

*ptt-plan-reviewer | Phase 2 Rev 2 | B40-LaneA | 2026-07-30*
