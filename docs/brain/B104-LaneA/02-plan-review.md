# B104-LaneA Plan Review
## Phase: Ph2 (ptt-plan-reviewer)
## Reviewing: docs/brain/B104-LaneA/02-architecture-plan.md

---

## Review Checklist

### 1. Root Cause Accuracy
- [x] Bug location correctly identified: L128-131, fallback branch `Math.Max(1, pos.Quantity / targetCount)`
- [x] Integer floor division mechanics correctly explained
- [x] Concrete failure case verified: qty=7, targetCount=3 → 6 covered, 1 unprotected
- [x] Live incident cited (Sim102) — confirms this is a production defect, not theoretical
- [x] Primary path (ATM snapshot) correctly identified as unaffected

### 2. Fix Design Correctness
- [x] Extract Method pattern is minimal and targeted — does not refactor any other logic
- [x] `CalcTNQty(int totalQty, int targetCount, int i)` signature is correct and complete
- [x] `floorQty = Math.Max(1, totalQty / targetCount)` preserves the existing per-pair baseline
- [x] Last-pair absorption formula `Math.Max(1, totalQty - floorQty * (targetCount - 1))` is algebraically correct
- [x] Guard `totalQty > targetCount` correctly prevents negative/zero arithmetic when qty <= count
- [x] `private static` scope is correct: no instance state accessed

### 3. Math Verification Table
- [x] CalcTNQty(7,3,2) → Max(1, 7 - 2*2) = Max(1,3) = 3. Total 2+2+3=7 ✓
- [x] CalcTNQty(6,3,2) → Max(1, 6 - 2*2) = Max(1,2) = 2. Total 2+2+2=6 ✓
- [x] CalcTNQty(4,3,2) → Max(1, 4 - 1*2) = Max(1,2) = 2. Total 1+1+2=4 ✓
- [x] CalcTNQty(1,3,2) → last pair, BUT 1 NOT > 3 → returns floorQty=1. Pre-existing behavior unchanged ✓
- [x] Table is exhaustive for specified test cases

### 4. CYC Impact
- [x] `Execute` CYC: fallback expression replaced by call, no branch added → CYC unchanged at 8
- [x] `CalcTNQty` CYC=3: (i==targetCount-1) check=1, (totalQty>targetCount) check=1, return paths baseline=1, total=3 ✓ ≤ 8
- [x] All other methods (`ResolveTargetCount`, `ResolveStop`, `SnapshotStopPrice`) untouched

### 5. Rule Compliance
- [x] JS-021: No `lock()` in `CalcTNQty` or at call site
- [x] JS-001: No `throw new Exception` — method returns `int`
- [x] JS-002: Returns `int` (value type, never null)
- [x] JS-033: Method is `private static int`, not `async void`
- [x] ASCII-only: all comment text uses ASCII characters only, no Unicode/emoji
- [x] File scope: only `PttQuickExit.cs` touched — zero other `.cs` files

### 6. Preservation of Existing Behavior
- [x] `ResolveTargetCount` (L255-258) explicitly listed as unchanged
- [x] `ResolveStop` unchanged
- [x] `Execute` primary path (ATM snapshot branch) unchanged
- [x] Compat overload `Execute(leader, instr, t1Ticks, t2Ticks, skipIfFollower)` unchanged

### 7. Spec Alignment
- [x] Plan addresses the exact DW-B104 defect described in the mission spec
- [x] Change 1A and Change 1B are both planned and well-specified with before/after
- [x] Acceptance criteria mirror the mission spec checklist precisely

---

## Violations Found

**None.**

All sections pass review. The plan is minimal, mathematically verified, rule-compliant, and precisely targeted to the single defective expression.

---

## Gate Decision

**REVIEW_PASS**

Proceeding to Ph3 — ticket generation.
