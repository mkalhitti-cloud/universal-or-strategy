# B104-LaneA Ticket 1 Verification Report
## Phase: Ph4b (ptt-verifier)
## Engineer report: docs/brain/B104-LaneA/ticket-1-completion.md
## Source: src/PropTraderTools/Features/PttQuickExit.cs (read independently)

---

## Independent Source Checks

### Check 1 — Fallback call site (L128-131)
**Read result (L128-131):**
```csharp
                int tNQty =
                    (targets != null && i < targets.Count)
                        ? targets[i].Qty
                        : CalcTNQty(pos.Quantity, targetCount, i);
```
✅ Fallback calls `CalcTNQty(pos.Quantity, targetCount, i)` — confirmed.

### Check 2 — CalcTNQty definition (L260-276)
**Read result:**
```csharp
        private static int CalcTNQty(int totalQty, int targetCount, int i)
        {
            int floorQty = Math.Max(1, totalQty / targetCount);
            if (i == targetCount - 1 && totalQty > targetCount)
                return Math.Max(1, totalQty - floorQty * (targetCount - 1)); // DW-B104: last pair absorbs remainder
            return floorQty;
        }
```
✅ `private static int` — correct scope.  
✅ Contains `int floorQty = Math.Max(1, totalQty / targetCount)`.  
✅ Last-pair guard: `if (i == targetCount - 1 && totalQty > targetCount)`.  
✅ Remainder return: `Math.Max(1, totalQty - floorQty * (targetCount - 1))`.  
✅ Fallback return: `return floorQty` (when not last pair OR totalQty <= targetCount).

### Check 3 — Scan: old inline expression gone
```
grep "Math.Max(1, pos.Quantity" → 0 results ✅
```

### Check 4 — Scan: CalcTNQty call + definition present
```
grep "CalcTNQty" → 5 occurrences:
  L131: call site ✅
  L261,267,268: doc comment refs ✅
  L270: method definition ✅
```
Call site (L131) and definition (L270) both confirmed.

### Check 5 — Scan: no lock()
```
grep "lock(" → 0 results ✅
```

### Check 6 — Scan: no throw new
```
grep "throw new" → 0 results ✅
```

### Check 7 — ASCII-only (new code)
Lines 128-131 and 260-276 (all new/modified lines) contain only ASCII characters.
Pre-existing non-ASCII at L222 (compat overload doc comment "→") is NOT in scope for this ticket — that line was not touched.  
✅ New code: ASCII-only.

### Check 8 — ResolveTargetCount unchanged (L255-258)
**Read result:**
```csharp
        private static int ResolveTargetCount(
            System.Collections.Generic.List<(double Price, int Qty)> own,
            int leaderCount
        ) => own?.Count > 0 ? own.Count : (leaderCount > 0 ? leaderCount : 2);
```
✅ Identical to pre-edit state — no change.

### Check 9 — CYC of CalcTNQty
- L270: `private static int CalcTNQty(...)` → baseline 1
- L273: `if (i == targetCount - 1 && totalQty > targetCount)` → +1 for `if`, +1 for `&&` condition
- Total CYC = **3** ✅ ≤ 8

### Check 10 — CYC of Execute
Counting branches in Execute: null/flat guard(1) + follower guard(2) + cancelFollowers guard(3) + snapshotStop guard(4) + isLong(5) + for-loop(6) + stop-submit null check(7) + target-submit null check(8).  
The fallback expression was replaced with a call — no branch added or removed.  
CYC of Execute = **8** ✅ (unchanged from pre-edit comment at L28-30).

---

## Cross-Check vs Engineer Layer 2 Report

| Item | Engineer Claimed | Verifier Confirmed |
|------|------------------|--------------------|
| L131 fallback expression | CalcTNQty call | ✅ Confirmed |
| CalcTNQty exists at L270 | private static int | ✅ Confirmed |
| floorQty line | L272 | ✅ Confirmed |
| Last-pair guard | L273 | ✅ Confirmed |
| CYC CalcTNQty | 3 | ✅ Confirmed (independent count) |
| CYC Execute | 8 unchanged | ✅ Confirmed |
| Old inline gone | 0 grep hits | ✅ Confirmed |
| lock() | 0 results | ✅ Confirmed |
| throw new | 0 results | ✅ Confirmed |
| ASCII (new code) | Clean | ✅ Confirmed |
| ResolveTargetCount | Unchanged | ✅ Confirmed |
| ptt-sync-and-verify | 0 MISMATCH | ✅ Confirmed (PASS in Phase 4a) |

**No discrepancies between engineer Layer 2 report and independent verification.**

---

## Acceptance Criteria Final Check

- [x] L131 fallback branch calls `CalcTNQty(pos.Quantity, targetCount, i)` ✅
- [x] `CalcTNQty` exists as `private static int` in `PttQuickExit` class (L270) ✅
- [x] `CalcTNQty` contains `int floorQty = Math.Max(1, totalQty / targetCount)` (L272) ✅
- [x] `CalcTNQty` returns last-pair remainder when `totalQty > targetCount` (L273-274) ✅
- [x] `CalcTNQty` returns `floorQty` when `totalQty <= targetCount` (L275) ✅
- [x] CYC of `CalcTNQty` = 3 (≤ 8) ✅
- [x] CYC of `Execute` = 8 (unchanged) ✅
- [x] No `lock()`. No `throw new Exception`. New code ASCII-only. ✅
- [x] `ResolveTargetCount` at L255-258 UNCHANGED ✅
- [x] `ptt-sync-and-verify.ps1`: 0 MISMATCH ✅

---

## Gate Decision

**VERIFY_PASS**

---

## MANDATORY NEXT STEP

**Please press F5 in NinjaTrader 8 (or go to Tools → Edit NinjaScript → Compile) and confirm green compile.**

Report back with:
- ✅ F5 GREEN — pipeline proceeds to Ph5 (final review + deferred backlog)
- ❌ F5 RED — report the compile error; engineer retry loop begins (max 3 cycles)
