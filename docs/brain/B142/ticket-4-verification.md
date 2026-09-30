# Ticket T4 Verification — B142

**Produced by**: ptt-verifier (independent)
**Date**: 2026-09-06
**Inputs**: docs/brain/B142/04-tickets.md, docs/brain/B142/ticket-4-completion.md, src/PropTraderTools/CopyEngine.cs

---

## 7-Scan Results

| Scan | Description | Result | Evidence |
|------|-------------|--------|----------|
| SCAN-01 | lock() ban | **PASS** | `Select-String -Pattern "lock\("` returns 4 comment-only hits. Lines 309, 343, 1735, 3686. Zero actual `lock(` statements anywhere in file. |
| SCAN-02 | DateTime.Now ban | **PASS** | `Select-String -Pattern "DateTime\.Now[^U]"` returns 0 matches. |
| SCAN-03 | ASCII-only | **PASS** | Byte scan: 0 bytes > 127. Pure ASCII/UTF-8 throughout. |
| SCAN-04 | FontFamily ban | **PASS** | `Select-String -Pattern "FontFamily"` returns 3 comment-only hits. Lines 3041, 3225, 3247. Zero actual FontFamily usage. |
| SCAN-05 | CYC<=8 | **PASS** | See per-method table below. |
| SCAN-06 | PTT- prefix on CreateOrder | **PASS** | All 4 T4 CreateOrder methods confirmed using PTT-prefixed names: `SyncAtmFollowerBracket` L2416: `"PTT-STP-Drag-" + suffix`; `SyncAtmFollowerTarget` L2875/2922: `tgtDragName` starting with "PTT-"; `ResubmitTargetAfterCascade` L2586/2620: `tgtDragName = "PTT-TGT-Drag-" + suffix`; `ResubmitOneCollateralLeg` L2727/2756: per-suffix PTT names. |
| SCAN-07 | Dispatcher.InvokeAsync | **N/A** | T4 methods are pure order-management logic on NT8 dispatch thread. No WPF UI interactions. `Dispatcher.InvokeAsync` used only at L367/381/391/1644 (outside B142 scope). |

---

## SCAN-05 Per-Method CYC Table (T4 Methods)

| Method | Lines | CYC (Project Convention) | Decision Points | Result |
|--------|-------|--------------------------|-----------------|--------|
| `IsAtmSTPOrder` | L2240-2248 | 1 | Expression-body; compound `\|\|` in single predicate = 0 extra per project convention | **PASS** |
| `FindLeaderCollateralOrder` | L2525-2537 | 3 | 1+if(null guard)+foreach+if(name match) = 4 by verifier count; project says 3 — both well below 8 | **PASS** |
| `SyncAtmFollowerBracket` | L2382-2432 | 5 | 1+if(acc)+if(fo)+if(NoPriceChange)+if(newStop==null) = 5; ternary `leaderOrder?.Quantity` not applicable here (direct access at L2412) | **PASS** |
| `SyncAtmFollowerTarget` | L2856-2940 | 8 (AT LIMIT) | Same as T2 analysis: 1+if(acc)+if(fo)+if(LimitPrice\|\|NoPriceChange)+foreach+if(State&&Name&&Instr)+if(newTarget==null) = 8 under project convention; ternary qty L2918 = 0 per project convention | **PASS** |
| `ResubmitTargetAfterCascade` | L2575-2636 | 4 | 1+foreach(APrime)+if(OrderState)+if(newTarget==null) = 4 (confirmed in source L2575-2636) | **PASS** |
| `ResubmitOneCollateralLeg` | L2688-2772 | 7 | Same as T3 analysis (T4 adds ternary qty expressions but project counts these as 0); CYC=7 confirmed | **PASS** |

---

## Method Presence

| Method | Expected Lines (Ticket) | Found at | Signature Match |
|--------|------------------------|----------|-----------------|
| `IsAtmSTPOrder` (DW-B142-DRAG clause) | L2240-2248 | L2240 confirmed | `internal static bool IsAtmSTPOrder(Order order)` ✓ — both PTT-STP-Drag (L2246) and PTT-TGT-Drag (L2247) clauses confirmed |
| `FindLeaderCollateralOrder` | L2525-2537 | L2525 confirmed | `private static Order FindLeaderCollateralOrder(Order leaderOrder, string suffix)` ✓ |
| `SyncAtmFollowerBracket` (qty fix) | L2382-2432 | L2382 confirmed | `private void SyncAtmFollowerBracket(Account acc, Order fo, double newPrice, string suffix, Order leaderOrder = null)` ✓ |
| `SyncAtmFollowerTarget` (qty fix) | L2856-2940 | L2856 confirmed | `private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice, Order? leaderOrder = null)` ✓ |
| `ResubmitTargetAfterCascade` (qty fix) | L2575-2636 | L2575 confirmed | `private void ResubmitTargetAfterCascade(Account acc, Order stpOrder, double targetPrice, Order leaderOrder, string suffix)` ✓ |
| `ResubmitOneCollateralLeg` (qty fix) | L2688-2772 | L2688 confirmed | `private void ResubmitOneCollateralLeg(Account acc, Order fo, double newPrice, double targetPrice, string suffix, Order leaderLeg = null)` ✓ |

---

## Key Implementation Details Verified in Source

- **`IsAtmSTPOrder` DW-B142-DRAG** (L2247): `|| order.Name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)` — confirmed present; symmetric to B142-DIRECT-4 clause at L2246.
- **`FindLeaderCollateralOrder`** (L2525-2537): null guard at L2527; `foreach` over `leaderOrder.Account.Orders.ToList()` at L2531; searches `"Stop" + suffix` and `"Target" + suffix`; returns first match or null — confirmed.
- **`SyncAtmFollowerBracket` qty fix** (L2412): `leaderOrder.Quantity` with comment `// DW-B142-QTY-DESYNC-01: use leader qty, not fo.Quantity` — confirmed.
- **`SyncAtmFollowerTarget` qty fix** (L2918): `leaderOrder != null ? leaderOrder.Quantity : fo.Quantity` — confirmed.
- **`ResubmitTargetAfterCascade` qty fix** (L2616): `leaderOrder.Quantity` with comment `// DW-B142-QTY-DESYNC-01: use leader qty, not stpOrder.Quantity` — confirmed.
- **`ResubmitOneCollateralLeg` qty fix** (L2723): `leaderLeg != null ? leaderLeg.Quantity : fo.Quantity`; (L2752): same expression for target — both confirmed.

---

## DNA Rule Spot-Check (T4 Methods)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 no lock | 0 actual `lock(` statements | PASS |
| JS-001 no throw in hot path | All CreateOrder calls inside try/catch blocks with StatusUpdate absorption; no rethrow | PASS |
| JS-002 no null return where non-null expected | `FindLeaderCollateralOrder` returns `Order` null — documented contract for "not found"; caller has null-fallback to `fo.Quantity` | PASS |
| NT8 PTT- prefix | All CreateOrder calls in T4 methods use PTT-prefixed names (verified at exact source lines) | PASS |

---

## Layer 2 vs Layer 3 Cross-Check

Engineer self-scan (Layer 2) reported:
- SCAN-01: 12 comment hits — verifier found 4. Discrepancy: engineer used pattern `\block\s*\(` (broader match); verifier used `lock\(` (exact). Neither found actual lock() statements. No violation.
- All other scans: results match. No discrepancies on substantive scan results.

---

## Verdict: VERIFY_PASS