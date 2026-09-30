# B142 Ticket Review — Drag-Sync System Hardening

**Block**: B142
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Mode**: RETROACTIVE — tickets describe committed implementation
**Source verified against**: `src/PropTraderTools/CopyEngine.cs`
**Plan verified against**: `docs/brain/B142/02-architecture-plan.md`
**Date**: 2026-09-02

---

## T1 — Guard Rails and Order Name Hardening

| Check | Result | Notes |
|-------|--------|-------|
| Spec Requirement IDs present | PASS | COPIER-DRAG-01..03, COPIER-INIT-01 listed |
| File Path present | PASS | `src/PropTraderTools/CopyEngine.cs` |
| Commits Covered listed | PASS | `4cc50a24`, `e8d529e2`, `220bc152` |
| Method Signatures present | PASS | 6 signatures listed |
| What Each Method Does present | PASS | Per-method descriptions present for all 6 |
| JS Rule Constraints present | PASS | JS-021, JS-001, JS-002 per method |
| xUnit [Fact] Test Names present | PASS | 6 test names listed |
| 7-Scan Checklist (SCAN-01..07) present | PASS | All 7 scans present |

**Signature Accuracy (verified against source):**

| Signature in Ticket | Source Lines | Match |
|---------------------|-------------|-------|
| `private static bool IsTrailingStop(Order order)` | L2218-2227 | PASS |
| `private void SyncFollowerBracket(Account acc, Order leaderOrder, bool isStop, double newPrice, double tickSize)` | L2266-2272 | PASS |
| `private void SyncAtmFollowerBracket(Account acc, Order fo, double newPrice, string suffix, Order leaderOrder = null)` | L2382 | PASS |
| `private void CancelExistingPttStpDrag(Account acc, Order fo, string suffix)` | L2801 | PASS |
| `private void ResubmitTargetAfterCascade(Account acc, Order stpOrder, double targetPrice, Order leaderOrder, string suffix)` | L2575-2580 | PASS |
| `private static bool MatchesLeaderName(Order order, string? leaderName, bool isStop)` | L3193 | PASS |

**SCAN Accuracy:**

| Scan | Ticket Claim | Verified | Result |
|------|-------------|----------|--------|
| SCAN-01 lock() | PASS — zero matches | `lock(` absent in all T1 methods | PASS |
| SCAN-02 DateTime.Now | PASS — not present | Not found in file | PASS |
| SCAN-03 ASCII-only | PASS — all ASCII | All string literals ASCII | PASS |
| SCAN-04 FontFamily | PASS — not present | Not found in T1 methods | PASS |
| SCAN-05 CYC<=8 | IsTrailingStop=1, SyncFollowerBracket=8, SyncAtmFollowerBracket=5, CancelExistingPttStpDrag=6, ResubmitTargetAfterCascade=4, MatchesLeaderName=5 | All consistent with architecture plan Section 9; SyncFollowerBracket=8 AT LIMIT confirmed | PASS |
| SCAN-06 PTT- prefix | PASS — PTT-STP-Drag-+suffix, PTT-TGT-Drag-+suffix | Verified L2416, L2620 (tgtDragName) | PASS |
| SCAN-07 Dispatcher.InvokeAsync | N/A — order-management only | Architecture plan Section 7 confirms no Dispatcher in B142 path | PASS |

**Traceability**: All 6 methods map to plan Section 4.1 (modified methods). All spec IDs map to documented defects. No phantom work. No missing work from plan.

**JS Pre-Check**: PASS — no lock(), no throw in hot path (all wrapped in try/catch), no null returns for value types (bool/void returns).

**CYC Pre-Check**: PASS — highest is SyncFollowerBracket CYC=8, AT LIMIT per architecture plan.

**NT8 Check**: PASS — all CreateOrder calls use PTT- prefix; no async/await in lifecycle; no sealed on window; no FontFamily; no hex colors; no DateTime.Now.

**Test Coverage**: PASS — all 6 methods have at least one [Fact] test; IsTrailingStop has 2 tests (true and false paths); MatchesLeaderName has 2 tests; SyncFollowerBracket zero-price guard covered.

**File Routing**: PASS — `src/PropTraderTools/CopyEngine.cs`

### T1 VERDICT: TICKET_REVIEW_PASS

---

## T2 — OCO Cascade Management

| Check | Result | Notes |
|-------|--------|-------|
| Spec Requirement IDs present | PASS | COPIER-DRAG-04..06 listed |
| File Path present | PASS | `src/PropTraderTools/CopyEngine.cs` |
| Commits Covered listed | PASS | `2b052b5d`, `fbf39d0e` |
| Method Signatures present | PASS | 4 signatures listed |
| What Each Method Does present | PASS | Per-method descriptions for all 4 |
| JS Rule Constraints present | PASS | JS-021, JS-001, JS-002 per method |
| xUnit [Fact] Test Names present | PASS | 7 test names listed |
| 7-Scan Checklist (SCAN-01..07) present | PASS | All 7 scans present |

**Signature Accuracy (verified against source):**

| Signature in Ticket | Source Lines | Match |
|---------------------|-------------|-------|
| `internal static bool IsAtmSTPOrder(Order order)` | L2240-2248 | PASS |
| `private double[] CaptureOtherLegTargetPrices(Account acc, Order fo, string excludeSuffix)` | L2481-2501 | PASS |
| `private void ResubmitCollateralLegs(Account acc, Order fo, double newPrice, double[] otherLegPrices, string excludeSuffix, Order leaderOrder)` | L2649-2655 | PASS |
| `private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice, Order? leaderOrder = null)` | L2856-2860 | PASS |

**SCAN Accuracy:**

| Scan | Ticket Claim | Verified | Result |
|------|-------------|----------|--------|
| SCAN-01 lock() | PASS — zero instances | Confirmed absent | PASS |
| SCAN-02 DateTime.Now | PASS — not present | Not found | PASS |
| SCAN-03 ASCII-only | PASS — all ASCII | Verified | PASS |
| SCAN-04 FontFamily | PASS — not present | Not found | PASS |
| SCAN-05 CYC<=8 | IsAtmSTPOrder=1, CaptureOtherLegTargetPrices=6, ResubmitCollateralLegs=4, SyncAtmFollowerTarget=8 | All consistent with architecture plan Section 9; SyncAtmFollowerTarget=8 AT LIMIT confirmed | PASS |
| SCAN-06 PTT- prefix | PASS — PTT-TGT-Drag-+tgtIdx at L2922 | Verified L2875 (`tgtDragName = tgtIdx > 0 ? "PTT-TGT-Drag-" + tgtIdx.ToString() : "PTT-TGT-Drag"`); all CreateOrder calls in T2 methods use PTT-prefixed names | PASS |
| SCAN-07 Dispatcher.InvokeAsync | N/A — order-management only | Confirmed | PASS |

**Traceability**: All 4 methods map to plan Section 4.1/4.2. Spec IDs COPIER-DRAG-04..06 map to `IsAtmSTPOrder` clause (DRAG-04), `SyncAtmFollowerTarget` guard (DRAG-05), and `CaptureOtherLegTargetPrices`+`ResubmitCollateralLegs` (DRAG-06). No phantom work. No missing plan work.

**JS Pre-Check**: PASS — no lock(); `CaptureOtherLegTargetPrices` returns `double[]` (value array, not reference null) — JS-002 compliant per ticket documentation; `SyncAtmFollowerTarget` all CreateOrder/Cancel calls wrapped in independent try/catch blocks.

**CYC Pre-Check**: PASS — highest is SyncAtmFollowerTarget CYC=8, AT LIMIT per architecture plan.

**NT8 Check**: PASS — no violations found in T2 methods.

**Test Coverage**: PASS — all 4 methods have [Fact] tests; `IsAtmSTPOrder` has 3 tests (PTT-STP-Drag-1, PTT-STP-Drag-3, generic false); `SyncAtmFollowerTarget` guard covered; `CaptureOtherLegTargetPrices` guard and capture paths covered; `ResubmitCollateralLegs` zero-price no-op covered.

**File Routing**: PASS — `src/PropTraderTools/CopyEngine.cs`

### T2 VERDICT: TICKET_REVIEW_PASS

---

## T3 — Drag Order State Accuracy

| Check | Result | Notes |
|-------|--------|-------|
| Spec Requirement IDs present | PASS | COPIER-DRAG-07..10 listed |
| File Path present | PASS | `src/PropTraderTools/CopyEngine.cs` |
| Commits Covered listed | PASS | `77a02254`, `cd3d9f02`, `ca8ad16f` |
| Method Signatures present | PASS | 4 signatures listed |
| What Each Method Does present | PASS | Per-method descriptions for all 4 |
| JS Rule Constraints present | PASS | JS-021, JS-001, JS-002 per method |
| xUnit [Fact] Test Names present | PASS | 9 test names listed |
| 7-Scan Checklist (SCAN-01..07) present | PASS | All 7 scans present |

**Signature Accuracy (verified against source):**

| Signature in Ticket | Source Lines | Match |
|---------------------|-------------|-------|
| `private static bool IsTargetOrderLive(Order o)` | L2553-2561 | PASS |
| `private double? CaptureLinkedTargetPrice(Account acc, string stopName)` | L2447-2465 | PASS |
| `private void ResubmitOneCollateralLeg(Account acc, Order fo, double newPrice, double targetPrice, string suffix, Order leaderLeg = null)` | L2688-2694 | PASS |
| `private Order? FindFollowerBracketOrder(IEnumerable<Order> orders, string? fromEntrySignalName, bool isStop, string? leaderName = null)` | L3138-3143 | PASS |

**SCAN Accuracy:**

| Scan | Ticket Claim | Verified | Result |
|------|-------------|----------|--------|
| SCAN-01 lock() | PASS — zero instances | Confirmed | PASS |
| SCAN-02 DateTime.Now | PASS — not present | Confirmed | PASS |
| SCAN-03 ASCII-only | PASS — all ASCII | Verified | PASS |
| SCAN-04 FontFamily | PASS — not present | Confirmed | PASS |
| SCAN-05 CYC<=8 | IsTargetOrderLive=1, CaptureLinkedTargetPrice=5, ResubmitOneCollateralLeg=7, FindFollowerBracketOrder=8 | All consistent with architecture plan Section 9; FindFollowerBracketOrder=8 AT LIMIT confirmed (L3130 comment); ResubmitOneCollateralLeg=7 consistent with plan (7 branches at L2672 comment) | PASS |
| SCAN-06 PTT- prefix | PASS — PTT-STP-Drag-+suffix at L2727, PTT-TGT-Drag-+suffix at L2756 | Verified at L2727 and L2756 | PASS |
| SCAN-07 Dispatcher.InvokeAsync | N/A — order-management only | Confirmed | PASS |

**Traceability**: All 4 methods map to plan Section 4.1/4.2. Spec IDs COPIER-DRAG-07..10 map correctly: DRAG-07=`IsTargetOrderLive` state expansion + `FindFollowerBracketOrder` ChangeSubmitted, DRAG-08=`SyncAtmFollowerTarget` per-leg name (via T2 description cross-ref), DRAG-09=`ResubmitOneCollateralLeg` Block A-Prime sweeps, DRAG-10=`CaptureLinkedTargetPrice`+`CaptureOtherLegTargetPrices` PTT preference. No phantom work. No missing plan work.

**JS Pre-Check**: PASS — `CaptureLinkedTargetPrice` returns `double?` (nullable VALUE type, not reference null) — JS-002 compliant per ticket documentation; `FindFollowerBracketOrder` returns `Order?` null as documented "not-found" contract — JS-002 compliant per ticket documentation; all iteration uses `acc.Orders.ToList()` snapshot.

**CYC Pre-Check**: PASS — highest is FindFollowerBracketOrder CYC=8, AT LIMIT.

**NT8 Check**: PASS — no violations found in T3 methods.

**Test Coverage**: PASS — `IsTargetOrderLive` has 4 tests covering all 3 new states (Submitted, ChangeSubmitted, ChangePending) plus negative (Cancelled); `CaptureLinkedTargetPrice` has 2 tests; `ResubmitOneCollateralLeg` has 2 sweep tests; `FindFollowerBracketOrder` ChangeSubmitted state covered.

**File Routing**: PASS — `src/PropTraderTools/CopyEngine.cs`

### T3 VERDICT: TICKET_REVIEW_PASS

---

## T4 — DW Card Fixes (DW-B142-DRAG + DW-B142-QTY-DESYNC-01)

| Check | Result | Notes |
|-------|--------|-------|
| Spec Requirement IDs present | PASS | COPIER-DRAG-11, COPIER-QTY-01, COPIER-QTY-02 listed |
| File Path present | PASS | `src/PropTraderTools/CopyEngine.cs` |
| Commits Covered listed | PASS | `a702ccbd`, `b30345c5` |
| Method Signatures present | PASS | 6 signatures listed |
| What Each Method Does present | PASS | Per-method descriptions for all 6 |
| JS Rule Constraints present | PASS | JS-021, JS-001, JS-002 per method |
| xUnit [Fact] Test Names present | PASS | 8 test names listed |
| 7-Scan Checklist (SCAN-01..07) present | PASS | All 7 scans present |

**Signature Accuracy (verified against source):**

| Signature in Ticket | Source Lines | Match |
|---------------------|-------------|-------|
| `internal static bool IsAtmSTPOrder(Order order)` | L2240-2248 | PASS |
| `private static Order FindLeaderCollateralOrder(Order leaderOrder, string suffix)` | L2525-2537 | PASS |
| `private void SyncAtmFollowerBracket(Account acc, Order fo, double newPrice, string suffix, Order leaderOrder = null)` | L2382 | PASS |
| `private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice, Order? leaderOrder = null)` | L2856-2860 | PASS |
| `private void ResubmitTargetAfterCascade(Account acc, Order stpOrder, double targetPrice, Order leaderOrder, string suffix)` | L2575-2580 | PASS |
| `private void ResubmitOneCollateralLeg(Account acc, Order fo, double newPrice, double targetPrice, string suffix, Order leaderLeg = null)` | L2688-2694 | PASS |

**SCAN Accuracy:**

| Scan | Ticket Claim | Verified | Result |
|------|-------------|----------|--------|
| SCAN-01 lock() | PASS — zero matches | Confirmed absent in file | PASS |
| SCAN-02 DateTime.Now | PASS — not present | Confirmed absent | PASS |
| SCAN-03 ASCII-only | PASS — all ASCII | Verified | PASS |
| SCAN-04 FontFamily | PASS — not present | Confirmed | PASS |
| SCAN-05 CYC<=8 | IsAtmSTPOrder=1, FindLeaderCollateralOrder=3, SyncAtmFollowerBracket=5, SyncAtmFollowerTarget=8, ResubmitTargetAfterCascade=4, ResubmitOneCollateralLeg=7 | All consistent with architecture plan Section 9; no new branches introduced by quantity fix (local variable assignment = 0 McCabe; ternaries per project convention = 0 when inside existing expression) | PASS |
| SCAN-06 PTT- prefix | PASS — all CreateOrder calls use PTT-prefixed names; explicit line citations L2416, L2922 (tgtDragName), L2620 (tgtDragName), L2727, L2756 | Verified at all cited lines | PASS |
| SCAN-07 Dispatcher.InvokeAsync | N/A — order-management only; correctly notes Dispatcher.InvokeAsync exists elsewhere (L367, L381, L391, L1644) but not in B142 path | Architecture plan Section 7 confirms | PASS |

**Traceability**: All 6 methods map to plan Section 4.1/4.2/DW card section. COPIER-DRAG-11 maps to `IsAtmSTPOrder` PTT-TGT-Drag- clause. COPIER-QTY-01 maps to `SyncAtmFollowerBracket`, `SyncAtmFollowerTarget`, `ResubmitTargetAfterCascade`. COPIER-QTY-02 maps to `FindLeaderCollateralOrder` + `ResubmitOneCollateralLeg`. No phantom work. No missing plan work.

**Commit SHA discrepancy (informational, not a blocking violation):**
- T4 Commits Covered lists `a702ccbd` for DW-B142-DRAG
- Architecture plan Section 4.1 and Section 12 both use `a702bcbd` (note: 'c' vs 'b' in position 5)
- This is a SHA typo in one of the two documents. Since this is a retroactive documentation block and both documents agree on the commit description and the code is verified in source, this is flagged as a documentation note but does NOT affect the engineering contract — the method signatures and scan results are verified against committed source. **WARN only — not a TICKET_REVIEW_FAIL**.

**JS Pre-Check**: PASS — no lock(); `FindLeaderCollateralOrder` returns `Order?` null as documented "not-found" contract with explicit null-fallback in callers — JS-002 compliant; all CreateOrder calls in quantity-fix methods wrapped in try/catch.

**CYC Pre-Check**: PASS — highest is SyncAtmFollowerTarget CYC=8, AT LIMIT. Quantity-fix ternary expressions at L2918 and L2723/L2752 apply project convention (ternary inside existing expression = 0 McCabe). No new branches introduced.

**NT8 Check**: PASS — no violations found in T4 methods.

**Test Coverage**: PASS — `IsAtmSTPOrder` PTT-TGT-Drag- clause covered by 2 tests; `FindLeaderCollateralOrder` covered by 3 tests (found, null account, suffix not found); quantity-fix behavior covered for `SyncAtmFollowerBracket` and `ResubmitOneCollateralLeg` (both leader-qty and null-fallback paths).

**File Routing**: PASS — `src/PropTraderTools/CopyEngine.cs`

### T4 VERDICT: TICKET_REVIEW_PASS

---

## Overall Review Summary

| Ticket | Mandatory Fields | Signature Accuracy | Scan Accuracy | Traceability | JS Pre-Check | CYC | NT8 | Tests | File Routing | Verdict |
|--------|-----------------|-------------------|--------------|-------------|-------------|-----|-----|-------|-------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| T3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| T4 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |

**Warnings (non-blocking):**
- SHA discrepancy between `04-tickets.md` (`a702ccbd`) and `02-architecture-plan.md` (`a702bcbd`) for the DW-B142-DRAG commit. One document has a 1-character typo. Source code is verified correct; this is a documentation artifact only.

**AT-LIMIT methods confirmed (all CYC=8, all <= 8):**
- `SyncFollowerBracket` (T1): CYC=8 per plan Section 9 and source comment L2262
- `SyncAtmFollowerTarget` (T2/T4): CYC=8 per plan Section 9 and source comment L2834
- `FindFollowerBracketOrder` (T3): CYC=8 per plan Section 9 and source comment L3130

---

## TICKET_REVIEW_PASS
