# B104-LaneA Tickets
## Phase: Ph3 (ptt-architect)
## REVIEW_PASS confirmed: docs/brain/B104-LaneA/02-plan-review.md

---

## TICKET 1 — DW-B104: QX Bracket Fallback Loses Remainder Unit

**File:** `src/PropTraderTools/Features/PttQuickExit.cs`
**Lane:** B104-LaneA
**Spec requirement:** DW-B104 (QX fallback integer division leaves remainder unit unprotected)
**Plan reference:** docs/brain/B104-LaneA/02-architecture-plan.md §3

---

### Scope

Touch ONLY `src/PropTraderTools/Features/PttQuickExit.cs`. Zero other `.cs` files.

---

### Change 1A — Replace fallback expression in Execute

**Location:** L128-131

**BEFORE (exact current text):**
```csharp
                int tNQty =
                    (targets != null && i < targets.Count)
                        ? targets[i].Qty
                        : Math.Max(1, pos.Quantity / targetCount);
```

**AFTER:**
```csharp
                int tNQty =
                    (targets != null && i < targets.Count)
                        ? targets[i].Qty
                        : CalcTNQty(pos.Quantity, targetCount, i);
```

**Net:** 1 expression replaced. CYC of `Execute` unchanged at 8.

---

### Change 1B — Add CalcTNQty helper method

**Location:** Insert after the closing `)` of `ResolveTargetCount` expression-body at L258,  
before the start of `SnapshotStopPrice` (currently at L265).

**INSERT (blank line + method):**
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

### Math Verification (required in completion report)

| Call | floorQty | Last? | qty>count? | Return | Running Total |
|------|----------|-------|------------|--------|---------------|
| CalcTNQty(7,3,0) | 2 | No | — | 2 | 2 |
| CalcTNQty(7,3,1) | 2 | No | — | 2 | 4 |
| CalcTNQty(7,3,2) | 2 | Yes | Yes | 3 | **7 ✓** |
| CalcTNQty(6,3,2) | 2 | Yes | Yes | 2 | **6 ✓** |
| CalcTNQty(4,3,2) | 1 | Yes | Yes | 2 | **4 ✓** |
| CalcTNQty(1,3,2) | 1 | Yes | No | 1 | pre-existing ✓ |

---

### 7-Scan Checklist (engineer Layer 2 contract)

The engineer MUST run all 7 scans to zero and report results in `ticket-1-completion.md`:

| # | Scan | Command | Pass Condition |
|---|------|---------|----------------|
| 1 | Grep: old inline expression gone | `grep -n "Math.Max(1, pos.Quantity" src/PropTraderTools/Features/PttQuickExit.cs` | 0 results |
| 2 | Grep: CalcTNQty present 2x | `grep -c "CalcTNQty" src/PropTraderTools/Features/PttQuickExit.cs` | 2 (call + definition) |
| 3 | Grep: no lock() | `grep -n "lock(" src/PropTraderTools/Features/PttQuickExit.cs` | 0 results |
| 4 | Grep: no throw new | `grep -n "throw new" src/PropTraderTools/Features/PttQuickExit.cs` | 0 results |
| 5 | Grep: ASCII-only | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Features/PttQuickExit.cs` | 0 results |
| 6 | CYC: CalcTNQty | Manual count from source | CYC = 3 (≤ 8) |
| 7 | Sync verify | `powershell -File scripts\ptt-sync-and-verify.ps1` | 0 MISMATCH |

---

### Acceptance Criteria

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

### Method Signatures

```csharp
// New helper (private static, PttQuickExit class):
private static int CalcTNQty(int totalQty, int targetCount, int i)

// Modified call site (Execute, line ~128-131):
int tNQty = (targets != null && i < targets.Count)
    ? targets[i].Qty
    : CalcTNQty(pos.Quantity, targetCount, i);
```

---

### JS Rule Constraints

| Rule | Constraint |
|------|-----------|
| JS-021 | No `lock()` in CalcTNQty or any modified code |
| JS-001 | No `throw new Exception` — return int always |
| JS-002 | No return null — value type |
| JS-033 | Not async — static int method |
| ASCII | All string literals and comments ASCII-only |
| CYC | CalcTNQty ≤ 8 (target: 3) |

---

**TICKETS_COMPLETE**
