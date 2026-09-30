# Ticket T2 Verification — B142

**Produced by**: ptt-verifier (independent)
**Date**: 2026-09-06
**Inputs**: docs/brain/B142/04-tickets.md, docs/brain/B142/ticket-2-completion.md, src/PropTraderTools/CopyEngine.cs

---

## 7-Scan Results

| Scan | Description | Result | Evidence |
|------|-------------|--------|----------|
| SCAN-01 | lock() ban | **PASS** | `Select-String -Pattern "lock\("` returns 4 hits, all in comments. Lines 309, 343, 1735, 3686. Zero actual `lock(` statements anywhere in file. |
| SCAN-02 | DateTime.Now ban | **PASS** | `Select-String -Pattern "DateTime\.Now[^U]"` returns 0 matches. |
| SCAN-03 | ASCII-only | **PASS** | Byte scan: 0 bytes > 127. Pure ASCII/UTF-8 throughout. |
| SCAN-04 | FontFamily ban | **PASS** | `Select-String -Pattern "FontFamily"` returns 3 comment-only hits. Lines 3041, 3225, 3247. Zero actual FontFamily usage. |
| SCAN-05 | CYC<=8 | **PASS** | See per-method table below. |
| SCAN-06 | PTT- prefix on CreateOrder | **PASS** | `SyncAtmFollowerTarget` L2875: `tgtDragName = tgtIdx > 0 ? "PTT-TGT-Drag-" + tgtIdx : "PTT-TGT-Drag"` — both branches start with "PTT-". L2922: `tgtDragName` passed to CreateOrder. Confirmed in source. |
| SCAN-07 | Dispatcher.InvokeAsync | **N/A** | T2 methods are pure order-management logic on NT8 dispatch thread. No WPF UI interactions. Dispatcher.InvokeAsync used only at L367/381/391/1644 (outside B142 scope). |

---

## SCAN-05 Per-Method CYC Table (T2 Methods)

| Method | Lines | CYC (Project Convention) | Decision Points | Result |
|--------|-------|--------------------------|-----------------|--------|
| `IsAtmSTPOrder` | L2240-2248 | 1 | Expression-body; no branching decision points | **PASS** |
| `CaptureOtherLegTargetPrices` | L2481-2501 | 6 | 1+if(!StartsWith)+foreach+for+if(excludeSuffix)+if(IsTargetLive&&PTT)+else-if(IsTargetLive&&ATM) = 6 (comment L2467 confirmed) | **PASS** |
| `ResubmitCollateralLegs` | L2649-2670 | 4 | 1+for+if(==excludeSuffix)+if(prices<=0)+{no branch for method calls} = 4 | **PASS** |
| `SyncAtmFollowerTarget` | L2856-2940 | 8 (AT LIMIT) | 1+if(acc==null)+if(fo==null)+if(LimitPrice<=0\|\|NoPriceChange)+foreach(APrime)+if(OrderState&&Name&&Instrument)+if(newTarget==null) = 8 (project: ternaries for local-var = 0; `\|\|` in guard = +1 per project) | **PASS** |

**Note on `CaptureOtherLegTargetPrices` CYC**: Independent count yields 6-7 depending on whether `else if` adds +1. Under project convention where `else if` as alternate-path-of-same-check = 0 extra: CYC=6. Either way well below 8.

---

## Method Presence

| Method | Expected Lines (Ticket) | Found at | Signature Match |
|--------|------------------------|----------|-----------------|
| `IsAtmSTPOrder` | L2240-2248 | L2240 confirmed | `internal static bool IsAtmSTPOrder(Order order)` ✓ — `internal static` for test access confirmed |
| `CaptureOtherLegTargetPrices` | L2481-2501 | L2481 confirmed | `private double[] CaptureOtherLegTargetPrices(Account acc, Order fo, string excludeSuffix)` ✓ |
| `ResubmitCollateralLegs` | L2649-2670 | L2649 confirmed | `private void ResubmitCollateralLegs(Account acc, Order fo, double newPrice, double[] otherLegPrices, string excludeSuffix, Order leaderOrder)` ✓ |
| `SyncAtmFollowerTarget` | L2856-2940 | L2856 confirmed | `private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice, Order? leaderOrder = null)` ✓ |

---

## Key Implementation Details Verified in Source

- **`IsAtmSTPOrder` B142-DIRECT-4 clause** (L2246): `|| order.Name.StartsWith("PTT-STP-Drag-", StringComparison.Ordinal)` — confirmed present.
- **`IsAtmSTPOrder` DW-B142-DRAG clause** (L2247): `|| order.Name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)` — confirmed present.
- **`CaptureOtherLegTargetPrices` second-drag guard** (L2484): `if (!fo.Name.StartsWith("Stop"))` — confirmed; returns all-zeros array.
- **`SyncAtmFollowerTarget` LimitPrice guard** (L2867): `if (fo.LimitPrice <= 0 || IsNoPriceChange(...))` — confirmed; B142-DIRECT-5.
- **`SyncAtmFollowerTarget` per-leg name** (L2874-2875): `tgtIdx = DeriveLeaderBracketIndex(leaderOrder)`, `tgtDragName = tgtIdx > 0 ? "PTT-TGT-Drag-" + tgtIdx : "PTT-TGT-Drag"` — confirmed; B142-DIRECT-7.
- **`SyncAtmFollowerTarget` quantity fix** (L2918): `leaderOrder != null ? leaderOrder.Quantity : fo.Quantity` — confirmed; DW-B142-QTY-DESYNC-01.

---

## DNA Rule Spot-Check (T2 Methods)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 no lock | 0 actual `lock(` statements | PASS |
| JS-001 no throw in hot path | All exception paths use try/catch with StatusUpdate absorption; Block A and Block B independent try/catch | PASS |
| JS-002 no null return where non-null expected | `CaptureOtherLegTargetPrices` returns `double[]` (value array, not null); all-zeros guard path returns empty array, not null | PASS |
| NT8 PTT- prefix | All CreateOrder calls use PTT-prefixed names confirmed at source | PASS |

---

## Verdict: VERIFY_PASS