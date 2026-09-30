# B104-LaneA Architecture Plan
## Epic: DW-B104 — QX Bracket Fallback Loses Remainder Unit
**Phase:** Ph1 (ptt-architect)
**Block:** B104  **Lane:** A  **File scope:** `src/PropTraderTools/Features/PttQuickExit.cs` ONLY

---

## 1. Root Cause Analysis

### 1.1 Defect Location
`PttQuickExit.Execute()` at **L128-131** of `PttQuickExit.cs`:

```csharp
int tNQty =
    (targets != null && i < targets.Count)
        ? targets[i].Qty
        : Math.Max(1, pos.Quantity / targetCount);  // <-- BUG: integer floor division
```

### 1.2 Mechanism of Failure
The fallback path (`targets == null` or empty, i.e., bare market order entry with no ATM snapshot) uses:

```
Math.Max(1, pos.Quantity / targetCount)
```

C# integer division truncates toward zero (floor division). With `pos.Quantity=7, targetCount=3`:

| Iteration | Expression | Result |
|-----------|-----------|--------|
| i=0 | Math.Max(1, 7/3) | 2 |
| i=1 | Math.Max(1, 7/3) | 2 |
| i=2 | Math.Max(1, 7/3) | 2 |
| **Total covered** | **2+2+2** | **6** |

**1 unit has no stop, no target — completely unprotected.**

### 1.3 Live Incident (Confirmed)
Sim102 held 1 unit long with no bracket after QX-ALL on 7-unit position.  
With TradeCopier active: the gap was multiplied across all follower accounts (all held 7-unit positions).

### 1.4 Why the Primary Path is Unaffected
When `targets != null && i < targets.Count` is true, `targets[i].Qty` is used directly — the broker-snapshotted ATM quantity. Integer division is not involved. The defect is **fallback-path only**.

---

## 2. Fix Design — Extract Method (Jane Street approved)

### 2.1 Strategy
Extract a `private static int CalcTNQty(int totalQty, int targetCount, int i)` helper that:
1. Computes `floorQty = Math.Max(1, totalQty / targetCount)` (identical to current floor for pairs 0..N-2)
2. On the **last pair** (`i == targetCount - 1`) AND when `totalQty > targetCount`, absorbs the remainder:  
   `Math.Max(1, totalQty - floorQty * (targetCount - 1))`
3. Otherwise returns `floorQty` unchanged (preserves pre-existing behavior for `totalQty <= targetCount`).

This is a pure extract-method refactor: **no behavioral change for non-last pairs, no behavioral change for the primary path, no change to any method except the one expression replaced + one helper added.**

### 2.2 Why Last-Pair Absorption is Correct
The remainder is `totalQty % targetCount`. Distributing it on the last pair ensures:
- `sum(CalcTNQty for i=0..N-1) == totalQty` always
- No unit is unprotected
- Simple arithmetic, no loop needed

### 2.3 Why `totalQty <= targetCount` Guard is Required
When `totalQty <= targetCount` (e.g., qty=1, targetCount=3), every pair already gets `floorQty=1` (due to `Math.Max(1,...)`). Applying remainder logic would produce `Math.Max(1, 1-2) = 1` — same result, but the guard makes intent explicit and avoids spurious arithmetic on edge cases where qty < targetCount.

---

## 3. Exact Changes

### Change 1A — Replace fallback expression in Execute (L128-131)
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

Net change: 1 expression swapped. CYC of `Execute` is unchanged (still 8).

### Change 1B — Add CalcTNQty helper after ResolveTargetCount (after L258)
Insert after the closing brace of `ResolveTargetCount` (currently L258):

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

## 4. Math Verification Table

| Call | floorQty | Last pair? | qty>count? | Result | Cumulative |
|------|----------|------------|------------|--------|------------|
| CalcTNQty(7,3,0) | 2 | No | — | 2 | 2 |
| CalcTNQty(7,3,1) | 2 | No | — | 2 | 4 |
| CalcTNQty(7,3,2) | 2 | Yes | Yes (7>3) | Max(1,7-4)=**3** | **7 ✓** |
| CalcTNQty(6,3,0) | 2 | No | — | 2 | 2 |
| CalcTNQty(6,3,1) | 2 | No | — | 2 | 4 |
| CalcTNQty(6,3,2) | 2 | Yes | Yes (6>3) | Max(1,6-4)=**2** | **6 ✓** |
| CalcTNQty(4,3,0) | 1 | No | — | 1 | 1 |
| CalcTNQty(4,3,1) | 1 | No | — | 1 | 2 |
| CalcTNQty(4,3,2) | 1 | Yes | Yes (4>3) | Max(1,4-2)=**2** | **4 ✓** |
| CalcTNQty(1,3,0) | 1 | No | — | 1 | 1 |
| CalcTNQty(1,3,1) | 1 | No | — | 1 | 2 |
| CalcTNQty(1,3,2) | 1 | Yes | No (1≤3) | **1** (floorQty) | 3 |

Row (1,3,2): total=3 exceeds qty=1 — this is the pre-existing over-assignment behavior (qty < targetCount). The fix does **not** alter it; `Math.Max(1,...)` already ensures each pair gets at least 1.

---

## 5. CYC Impact Analysis

| Method | Before | After | Change |
|--------|--------|-------|--------|
| `Execute` | 8 | 8 | Unchanged (fallback expression replaced with call, no branch added) |
| `CalcTNQty` | — | 3 | New (branch: last-pair check=1, qty>count check=1, return paths=1, baseline=0; total=3) |
| `ResolveTargetCount` | 2 | 2 | Unchanged |
| `ResolveStop` | 1 | 1 | Unchanged |

All methods remain ≤ 8 (JS CYC mandate).

---

## 6. Rule Compliance Checklist

| Rule | Requirement | Status |
|------|-------------|--------|
| JS-021 | No `lock()` | No lock in new code |
| JS-001 | No `throw new Exception` | No throw in CalcTNQty |
| JS-002 | No `return null` | Returns `int` (value type) |
| JS-033 | No `async void` | Static int method |
| ASCII-only | No Unicode/emoji in literals | All comments ASCII |
| CYC ≤ 8 | All methods | CalcTNQty=3, Execute=8 |
| File scope | PttQuickExit.cs ONLY | One file touched |
| ResolveTargetCount | Unchanged | L255-258 untouched |

---

## 7. File Scope Boundary

- **Modified:** `src/PropTraderTools/Features/PttQuickExit.cs`
- **Not modified:** Any other `.cs` file, any `.md` file in `src/`
- **No new files** in `src/`

---

## 8. Acceptance Criteria

- [ ] L128-131 fallback branch calls `CalcTNQty(pos.Quantity, targetCount, i)`
- [ ] `CalcTNQty` exists as `private static int` in `PttQuickExit` class
- [ ] `CalcTNQty` contains `int floorQty = Math.Max(1, totalQty / targetCount)`
- [ ] `CalcTNQty` returns last-pair remainder when `totalQty > targetCount`
- [ ] `CalcTNQty` returns `floorQty` when `totalQty <= targetCount` (pre-existing preserved)
- [ ] CYC of `CalcTNQty` = 3 (≤ 8)
- [ ] CYC of `Execute` = 8 (unchanged)
- [ ] No `lock()`. No `throw new Exception`. ASCII-only.
- [ ] `ResolveTargetCount` at L255-258 UNCHANGED
- [ ] `ptt-sync-and-verify.ps1`: 0 MISMATCH

---

**PLAN_COMPLETE**
