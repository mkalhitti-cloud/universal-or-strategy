# PTT-COPIER-B19-L2 — Plan Review
**Cycle**: 5 (final)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-07-07
**Result**: ✅ REVIEW_PASS

---

## Scan Results

### SCAN-SIG: GetAsk(instrument) / GetBid(instrument) — must be 0

```
grep "GetAsk\(instrument\)\|GetBid\(instrument\)" 02-architecture-plan.md
```

**Hits: 0** — all occurrences of the parameterised forms have been removed.
Only `GetAsk()` / `GetBid()` (no-arg) appear in §5, §13, §15. ✅

### SCAN-TEST-NAME: §6.1 exact method names

Cross-checked against `CopyEngineTests.cs` (lines 1318/1344/1364/1392/1446):

| Plan name | File match | Result |
|-----------|-----------|--------|
| `Flatten_LimitOverload_LongPosition_EmitsSellLimitFullQty` | line 1318 | ✅ |
| `Flatten_LimitOverload_ShortPosition_EmitsBuyToCoverLimitFullQty` | line 1344 | ✅ |
| `Trim_LimitOverload_LongPosition_EmitsSellLimitAtRefPlusTick` | line 1364 | ✅ |
| `Trim_LimitOverload_ShortPosition_EmitsBuyToCoverLimitAtRefMinusTick` | line 1392 | ✅ |
| `Flatten_ZeroBuffer_FallsBackToMarketOrder` | line 1446 | ✅ |

All 5 names match exactly. ✅

---

## 14-Point Checklist

| # | Rule | Check | Evidence | Result |
|---|------|-------|----------|--------|
| 1 | JS-021 | No `lock()` anywhere | §8: "PASS — no lock() added"; no lock() in any code block | ✅ PASS |
| 2 | JS-001 | No `throw` in hot path | §4.4 guard returns market-overload call + `return`; no `throw` anywhere | ✅ PASS |
| 3 | JS-002 | No `return null` | `GetAsk()`/`GetBid()` return `0.0`; §8 confirms | ✅ PASS |
| 4 | JS-010 | No public constructor on singleton/signal struct | No new classes or structs introduced | ✅ PASS |
| 5 | JS-033 | No `async void` (non-event-handler) | §8: "PASS — no async void added"; confirmed by code review | ✅ PASS |
| 6 | JS-009 | No `Dictionary<K,V>` for shared/thread-touched collection | No Dictionary introduced | ✅ PASS |
| 7 | JS-008 | No mutable struct fields; SolidColorBrush Freeze()d | No structs or brushes introduced | ✅ PASS |
| 8 | JS-003 | No magic string for discriminated state | Order name strings are constants, not state discriminators | ✅ PASS |
| 9 | JS-023 | No UI update from off-thread without Dispatcher.InvokeAsync | §13 explicitly states Dispatcher.InvokeAsync; no new threading | ✅ PASS |
| 10 | CYC ≤ 8 | All methods CYC ≤ 8 | §7: max CYC = 6 (Trim/Flatten); all values ≤ 8 | ✅ PASS |
| 11 | NT8 violations | async in lifecycle; Account.All in ctor; sealed TradeCopierWindow; FontFamily override; #RRGGBB hex; CreateOrder without PTT- prefix; DateTime.Now | §9: NT8-007/013/014/032 all PASS; none of the banned patterns appear | ✅ PASS |
| 12 | V-SIG-01..04 | GetAsk(instrument)/GetBid(instrument) = 0 hits | grep: **0 matches** | ✅ PASS |
| 13 | V-TEST-NAME-01 | §6.1 exact names from CopyEngineTests.cs | All 5 names confirmed at exact line numbers | ✅ PASS |
| 14 | Spec completeness | All 3 spec requirements addressed | (a) anchor fix in §4; (b) Panel callers in §5; (c) test updates in §6 | ✅ PASS |

**Violations found: 0**

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| Fix limit price anchor from `Last` → `Ask`/`Bid` | ✅ Yes | §3, §4 |
| Update `TradeCopierPanel` callers to pass `ask`/`bid` | ✅ Yes | §5 |
| Update existing 5 B12 tests (3-arg → 4-arg) | ✅ Yes | §6.1 |
| Add 5 new xUnit `[Fact]` tests for `ComputeLimitPx` | ✅ Yes | §6.2 |
| `ComputeLimitPx` helper extracted for testability | ✅ Yes | §4.3, §15 |
| All new methods CYC ≤ 8 | ✅ Yes | §7 |
| NT8-032 null-guard chain for `md.Ask`/`md.Bid` | ✅ Yes | §5.2, §5.3 |
| No scope creep (untouched files confirmed) | ✅ Yes | §1 |

---

## Prior Violation History

| Cycle | Violations | Status |
|-------|-----------|--------|
| 1 | V-SIG-01: `GetAsk(instrument)` used in §5.4 | Fixed in Cycle 2 |
| 2 | V-SIG-02/03/04: remaining parameterised forms in §5.5, §5.6, §13 | Fixed in Cycle 3 |
| 3 | V-TEST-NAME-01: §6.1 used placeholder names, not actual CopyEngineTests.cs method names | Fixed in Cycle 4 |
| 4 | No new violations introduced | — |
| **5** | **0 violations** | **REVIEW_PASS** |

---

## Decision

**REVIEW_PASS** — Plan is approved for Phase 3 (ticket generation).

All 14 checklist items pass. Zero Jane Street DNA violations. Zero NT8 violations.
All prior violation classes (V-SIG-01..04, V-TEST-NAME-01) confirmed remediated.
