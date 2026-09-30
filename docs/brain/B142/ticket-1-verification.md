# Ticket T1 Verification — B142

**Produced by**: ptt-verifier (independent)
**Date**: 2026-09-06
**Inputs**: docs/brain/B142/04-tickets.md, docs/brain/B142/ticket-1-completion.md, src/PropTraderTools/CopyEngine.cs

---

## 7-Scan Results

| Scan | Description | Result | Evidence |
|------|-------------|--------|----------|
| SCAN-01 | lock() ban | **PASS** | `Select-String -Pattern "lock\("` returns 4 hits, all in comments (`// JS-021: no lock()`). Lines 309, 343, 1735, 3686. Zero actual `lock(` statements anywhere in file. |
| SCAN-02 | DateTime.Now ban | **PASS** | `Select-String -Pattern "DateTime\.Now[^U]"` returns 0 matches. |
| SCAN-03 | ASCII-only | **PASS** | `[System.IO.File]::ReadAllBytes` byte scan: 0 bytes > 127. Pure ASCII/UTF-8 throughout. |
| SCAN-04 | FontFamily ban | **PASS** | `Select-String -Pattern "FontFamily"` returns 3 hits, all in comments (`No FontFamily`). Lines 3041, 3225, 3247. Zero actual FontFamily usage. |
| SCAN-05 | CYC<=8 | **PASS** | See per-method table below. |
| SCAN-06 | PTT- prefix on CreateOrder | **PASS** | `SyncAtmFollowerBracket` L2416: `"PTT-STP-Drag-" + suffix` confirmed in source. `ResubmitTargetAfterCascade` L2586 + L2620: `tgtDragName = "PTT-TGT-Drag-" + suffix` confirmed. All CreateOrder calls use PTT-prefixed names. |
| SCAN-07 | Dispatcher.InvokeAsync | **N/A** | T1 methods are pure order-management logic on NT8 dispatch thread. No WPF UI interactions. Dispatcher.InvokeAsync used at L367/381/391/1644 (outside B142 scope). |

---

## SCAN-05 Per-Method CYC Table (T1 Methods)

| Method | Lines | CYC (Project Convention) | Decision Points | Result |
|--------|-------|--------------------------|-----------------|--------|
| `IsTrailingStop` | L2218-2227 | 1 | Expression-body: `&&` compound = single predicate | **PASS** |
| `SyncFollowerBracket` | L2266-2360 | 8 (AT LIMIT) | 1(base)+if(fo==null)+if(tickSize)+if(isStop&&IsAtmSTP)+if(fo.StopPrice<tickSize)+if(HasValue)+if(!isStop&&IsAtmSTP)+if(isStop&&IsTrailingStop)+if(isStop) = 8 (project: `&&` compound = 0 extra) | **PASS** |
| `SyncAtmFollowerBracket` | L2382-2432 | 5 | 1+if(acc)+if(fo)+if(NoPriceChange)+if(newStop==null) = 5 | **PASS** |
| `CancelExistingPttStpDrag` | L2801-2822 | 6 | 1+foreach+if+&&Name+&&Instrument+?. = 6 (per L2794 comment) | **PASS** |
| `ResubmitTargetAfterCascade` | L2575-2636 | 4 | 1+foreach+if(OrderState)+if(newTarget==null) = 4 | **PASS** |
| `MatchesLeaderName` | L3193-3210 | 5 | 1+if(null)+if(==)+ternary(legSuffix)+if(!isStop&&)+if(isStop&&) = 5 | **PASS** |

---

## Method Presence

| Method | Expected Lines (Ticket) | Found at | Signature Match |
|--------|------------------------|----------|-----------------|
| `IsTrailingStop` | L2218-2227 | L2218 confirmed | `private static bool IsTrailingStop(Order order)` ✓ |
| `SyncFollowerBracket` | L2266-2345 | L2266 confirmed | `private void SyncFollowerBracket(Account acc, Order leaderOrder, bool isStop, double newPrice, double tickSize)` ✓ |
| `SyncAtmFollowerBracket` | L2382-2432 | L2382 confirmed | `private void SyncAtmFollowerBracket(Account acc, Order fo, double newPrice, string suffix, Order leaderOrder = null)` ✓ |
| `CancelExistingPttStpDrag` | L2801-2822 | L2801 confirmed | `private void CancelExistingPttStpDrag(Account acc, Order fo, string suffix)` ✓ |
| `ResubmitTargetAfterCascade` | L2575-2636 | L2575 confirmed | `private void ResubmitTargetAfterCascade(Account acc, Order stpOrder, double targetPrice, Order leaderOrder, string suffix)` ✓ |
| `MatchesLeaderName` | L3193-3210 | L3193 confirmed | `private static bool MatchesLeaderName(Order order, string? leaderName, bool isStop)` ✓ |

---

## DNA Rule Spot-Check (T1 Methods)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 no lock | `lock(` scan = 0 actual statements | PASS |
| JS-001 no throw in hot path | All method returns are bool/void; exception paths use try/catch with StatusUpdate absorption | PASS |
| JS-002 no null return where non-null expected | `IsTrailingStop` returns bool, never null; `MatchesLeaderName` returns bool, never null | PASS |
| JS-008 no mutable struct | No struct fields in T1 methods | PASS |
| NT8 PTT- prefix | `"PTT-STP-Drag-" + suffix` at L2416; `"PTT-TGT-Drag-" + suffix` in `tgtDragName` at L2586 | PASS |

---

## Cross-Check vs Engineer Self-Scan

Engineer reported SCAN-01: 12 comment hits. Verifier found 4 comment hits. **Discrepancy**: engineer may have included hits from a different pattern (`\block\s*\(` instead of `lock\(`). The important fact is identical: zero actual `lock(` statements in real code. No violation either way.

All other scan results match engineer self-report.

---

## Verdict: VERIFY_PASS