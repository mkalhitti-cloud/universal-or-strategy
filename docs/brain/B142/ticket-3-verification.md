# Ticket T3 Verification — B142

**Produced by**: ptt-verifier (independent)
**Date**: 2026-09-06
**Inputs**: docs/brain/B142/04-tickets.md, docs/brain/B142/ticket-3-completion.md, src/PropTraderTools/CopyEngine.cs

---

## 7-Scan Results

| Scan | Description | Result | Evidence |
|------|-------------|--------|----------|
| SCAN-01 | lock() ban | **PASS** | `Select-String -Pattern "lock\("` returns 4 comment-only hits. Lines 309, 343, 1735, 3686. Zero actual `lock(` statements anywhere in file. |
| SCAN-02 | DateTime.Now ban | **PASS** | `Select-String -Pattern "DateTime\.Now[^U]"` returns 0 matches. |
| SCAN-03 | ASCII-only | **PASS** | Byte scan: 0 bytes > 127. Pure ASCII/UTF-8 throughout. |
| SCAN-04 | FontFamily ban | **PASS** | `Select-String -Pattern "FontFamily"` returns 3 comment-only hits. Lines 3041, 3225, 3247. Zero actual FontFamily usage. |
| SCAN-05 | CYC<=8 | **PASS** | See per-method table below. |
| SCAN-06 | PTT- prefix on CreateOrder | **PASS** | `ResubmitOneCollateralLeg` L2727: `"PTT-STP-Drag-" + suffix`; L2756: `"PTT-TGT-Drag-" + suffix`. Both confirmed in source at those exact lines. |
| SCAN-07 | Dispatcher.InvokeAsync | **N/A** | T3 methods are pure order-management logic on NT8 dispatch thread. No WPF UI interactions. |

---

## SCAN-05 Per-Method CYC Table (T3 Methods)

| Method | Lines | CYC (Project Convention) | Decision Points | Result |
|--------|-------|--------------------------|-----------------|--------|
| `IsTargetOrderLive` | L2553-2561 | 1 | Expression-body; compound `\|\|` not counted per project comment at L2546/2550 | **PASS** |
| `CaptureLinkedTargetPrice` | L2447-2465 | 5 | 1+if(!TryParse)+foreach+if(IsLive&&pttTgt)+else-if(IsLive&&atmTgt)+if(pttPrice.HasValue) = 5 (project: else-if as complement = 0 extra); verifier count: 5-6 both below 8 | **PASS** |
| `ResubmitOneCollateralLeg` | L2688-2772 | 7 | 1+foreach(APrime-Stop)+if(Cancellable)+foreach(APrime-Tgt)+if(IsTargetLive)+if(newStop==null)+if(newTarget==null) = 7 (project comment L2672 confirmed); ternary qty expressions = 0 per project | **PASS** |
| `FindFollowerBracketOrder` | L3138-3171 | 8 (AT LIMIT) | 1+foreach+if(!PassesBracketGate)+if(OrderState!=)+if(isStop)+if(StopMarket\|\|StopLimit)+if(Limit&&!IsStopLeg) = 8 (project comment at L3150 labels "(4) branches" for state filter counting) | **PASS** |

---

## Method Presence

| Method | Expected Lines (Ticket) | Found at | Signature Match |
|--------|------------------------|----------|-----------------|
| `IsTargetOrderLive` | L2553-2561 | L2553 confirmed | `private static bool IsTargetOrderLive(Order o)` ✓ — expression body with 5 states: Working, Accepted, Submitted (B142-DIRECT-7), ChangeSubmitted, ChangePending (B142-DIRECT-9) |
| `CaptureLinkedTargetPrice` | L2447-2465 | L2447 confirmed | `private double? CaptureLinkedTargetPrice(Account acc, string stopName)` ✓ |
| `ResubmitOneCollateralLeg` | L2688-2772 | L2688 confirmed | `private void ResubmitOneCollateralLeg(Account acc, Order fo, double newPrice, double targetPrice, string suffix, Order leaderLeg = null)` ✓ |
| `FindFollowerBracketOrder` | L3138-3171 | L3138 confirmed | `private Order? FindFollowerBracketOrder(IEnumerable<Order> orders, string? fromEntrySignalName, bool isStop, string? leaderName = null)` ✓ |

---

## Key Implementation Details Verified in Source

- **`IsTargetOrderLive` B142-DIRECT-7 BUG A** (L2558): `|| o.OrderState == OrderState.Submitted` — confirmed present.
- **`IsTargetOrderLive` B142-DIRECT-9 BUG C** (L2559-2560): `|| o.OrderState == OrderState.ChangeSubmitted` and `|| o.OrderState == OrderState.ChangePending` — confirmed present.
- **`CaptureLinkedTargetPrice` dual-scan** (L2451-2463): `pttTgtName`, `targetName` dual search with `pttPrice` preferred over `atmPrice` — confirmed in source (B142-DIRECT-9 BUG A).
- **`ResubmitOneCollateralLeg` Block A-Prime-Stop** (L2699-2704): sweep `"PTT-STP-Drag-{suffix}"` before stop CreateOrder — confirmed.
- **`ResubmitOneCollateralLeg` Block A-Prime-Target** (L2708-2713): sweep `"PTT-TGT-Drag-{suffix}"` before target CreateOrder — confirmed.
- **`ResubmitOneCollateralLeg` quantity fix** (L2723, L2752): `leaderLeg != null ? leaderLeg.Quantity : fo.Quantity` — confirmed at both CreateOrder calls.
- **`FindFollowerBracketOrder` ChangeSubmitted** (L3153): `order.OrderState != OrderState.ChangeSubmitted` — confirmed added (B142-DIRECT-9 BUG B).

---

## DNA Rule Spot-Check (T3 Methods)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 no lock | 0 actual `lock(` statements | PASS |
| JS-001 no throw in hot path | `ResubmitOneCollateralLeg` Block A-Prime sweeps use empty `catch {}` per project convention; Block B and C use `catch (Exception ex) => StatusUpdate` | PASS |
| JS-002 no null return where non-null expected | `CaptureLinkedTargetPrice` returns `double?` — null is the documented contract for "target not found"; `FindFollowerBracketOrder` returns `Order?` null on not-found per documented contract | PASS |
| NT8 PTT- prefix | `"PTT-STP-Drag-" + suffix` at L2727; `"PTT-TGT-Drag-" + suffix` at L2756 — confirmed | PASS |

---

## Verdict: VERIFY_PASS