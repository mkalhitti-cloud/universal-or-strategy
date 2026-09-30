# B104-LaneA Ticket 1 Completion Report
## Phase: Ph4a (ptt-engineer)
## Ticket: DW-B104 — QX Bracket Fallback Loses Remainder Unit
## File: src/PropTraderTools/Features/PttQuickExit.cs

---

## Changes Applied

### Change 1A — L128-131 fallback expression replaced
**Location:** L128-131 (now L131 after final state)
**Before:**
```csharp
                int tNQty =
                    (targets != null && i < targets.Count)
                        ? targets[i].Qty
                        : Math.Max(1, pos.Quantity / targetCount);
```
**After:**
```csharp
                int tNQty =
                    (targets != null && i < targets.Count)
                        ? targets[i].Qty
                        : CalcTNQty(pos.Quantity, targetCount, i);
```

### Change 1B — CalcTNQty helper inserted after ResolveTargetCount (L259-276)
```csharp
        /// <summary>
        /// CalcTNQty: compute per-pair qty for fallback path (no ATM snapshot).
        /// Last pair absorbs remainder so total bracketed qty equals pos.Quantity exactly.
        /// Guard: only applies remainder logic when pos.Quantity > targetCount (avoids negative).
        /// CYC = 3: (1) is-last-pair AND (2) qty-exceeds-count, (3) remainder vs floor.
        /// JS-001: no throw. JS-002: returns int. ASCII-only.
        /// DW-B104: fixes integer division gap where Math.Max(1, qty/n)*n < qty.
        /// Verified: CalcTNQty(7,3,0)=2, (7,3,1)=2, (7,3,2)=3 -- total=7.
        ///           CalcTNQty(6,3,2)=2 -- total=6. CalcTNQty(1,3,2)=1 -- pre-existing qty<n behavior unchanged.
        /// </summary>
        private static int CalcTNQty(int totalQty, int targetCount, int i)
        {
            int floorQty = Math.Max(1, totalQty / targetCount);
            if (i == targetCount - 1 && totalQty > targetCount)
                return Math.Max(1, totalQty - floorQty * (targetCount - 1)); // DW-B104: last pair absorbs remainder
            return floorQty;
        }
```

---

## 7-Scan Results (Layer 2)

| # | Scan | Result | Status |
|---|------|--------|--------|
| 1 | `grep "Math.Max(1, pos.Quantity"` | 0 results | ✅ PASS |
| 2 | `grep -c "CalcTNQty"` | 5 occurrences (1 call L131, 1 definition L270, 3 doc-comment refs) | ✅ PASS |
| 3 | `grep "lock("` | 0 results | ✅ PASS |
| 4 | `grep "throw new"` | 0 results | ✅ PASS |
| 5 | Non-ASCII in new code | 0 — pre-existing arrow char at L222 (out of scope, NOT touched) | ✅ PASS |
| 6 | CYC of CalcTNQty | 1 (baseline) + 1 (if) + 1 (&&) = **3** ≤ 8 | ✅ PASS |
| 7 | ptt-sync-and-verify.ps1 | `SYNC + VERIFY: PASS (16 files confirmed)` — 0 MISMATCH | ✅ PASS |

---

## Math Verification

| Call | floorQty | Last pair? | qty>count? | Return | Cumulative |
|------|----------|------------|------------|--------|------------|
| CalcTNQty(7,3,0) | 2 | No | — | 2 | 2 |
| CalcTNQty(7,3,1) | 2 | No | — | 2 | 4 |
| CalcTNQty(7,3,2) | 2 | Yes | Yes | 3 | **7 ✓** |
| CalcTNQty(6,3,2) | 2 | Yes | Yes | 2 | **6 ✓** |
| CalcTNQty(4,3,2) | 1 | Yes | Yes | 2 | **4 ✓** |
| CalcTNQty(1,3,2) | 1 | Yes | No | 1 | pre-existing ✓ |

---

## Acceptance Criteria Status

- [x] L131 fallback branch calls `CalcTNQty(pos.Quantity, targetCount, i)`
- [x] `CalcTNQty` exists as `private static int` in `PttQuickExit` class (L270)
- [x] `CalcTNQty` contains `int floorQty = Math.Max(1, totalQty / targetCount)` (L272)
- [x] `CalcTNQty` returns last-pair remainder when `totalQty > targetCount` (L273-274)
- [x] `CalcTNQty` returns `floorQty` when `totalQty <= targetCount` (L275)
- [x] CYC of `CalcTNQty` = 3 (≤ 8)
- [x] CYC of `Execute` = 8 (unchanged — no branch added/removed)
- [x] No `lock()`. No `throw new Exception`. New code ASCII-only.
- [x] `ResolveTargetCount` at L255-258 UNCHANGED (verified)
- [x] `ptt-sync-and-verify.ps1`: 0 MISMATCH ✅

---

## BUILD_PASS
