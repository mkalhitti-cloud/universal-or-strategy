# WAVE2-LANE-E Deferred Backlog

## Block: WAVE2-LANE-E (completed 2026-09-06)

---

## Prior Block Deferred Items Carried Forward

The following items from `docs/brain/BWAVE-REFACTOR/LaneC/06-deferred-backlog.md` remain unresolved
after WAVE2-LANE-E. They are carried forward to future blocks unchanged.

### DW-LC-01 STATUS: CLOSED

**Original**: AT-LIMIT CCN=8 methods from LaneC -- PttQuickExit::Execute (grew to CCN=17),
PttGlobalQuickExit::Execute (CCN=9), PttBreakEvenSwap::Execute (CCN=9).

**Resolution by WAVE2-LANE-E**:
- PttQuickExit::Execute: CCN 17 -> 5. RESOLVED with 3 branches of headroom below the limit.
- PttGlobalQuickExit::Execute(forcedTargets): CCN 9 -> 8. RESOLVED (AT-LIMIT). Promoted to DW-LE-02.
- PttBreakEvenSwap::Execute: CCN 9 -> 8. RESOLVED (AT-LIMIT). Promoted to DW-LE-02.

**Status**: CLOSED. Superseded by DW-LE-02 which tracks the current AT-LIMIT set.

---

### DW-LC-02 (P2): OPEN -- Carry Forward

**Summary**: `ResolveOrderParams` duplication in PttTrim and PttFlatten.
Both classes contain an identical private `ResolveOrderParams` method.
Proposed extraction to shared `PttOrderUtils.cs`.

**Status in WAVE2-LANE-E**: Not addressed. The FormatOrderPrice helper (DW-LE-01) was extracted
in this block, but `ResolveOrderParams` remains duplicated.

**Risk**: LOW -- purely cosmetic duplication, no correctness impact.

**Recommended Action**: Extract both `ResolveOrderParams` implementations to `PttOrderUtils.cs`
or a similar shared static utility class in a future block.

---

### DW-LC-03 (P2): OPEN -- Carry Forward

**Summary**: Pre-existing `FindPositionLocal` returns `null` in PttBreakEven, PttTrim,
and PttFlatten. JS-002 spirit violation (null return from private helper). Functionally
safe because all callers have immediate `if (pos == null || pos.Quantity == 0) return;` guards.
This is an NT8-mandated pattern (NT8-050: no indexer on Positions collection).

**Status in WAVE2-LANE-E**: Not addressed. FindPositionLocal not touched by this block.

**Risk**: LOW -- all callers guarded; no correctness impact. Acknowledged JS-002 deviation.

**Recommended Action**: In a future block, convert to `Option<Position>` return type or
extract to a shared NT8-safe `TryFindPosition` helper. Requires caller updates.

---

### DW-LC-04 (P2): OPEN -- Carry Forward

**Summary**: `SubmitQxOcoPair` test in BwaveLaneETests.cs uses `GetMethod` single-name lookup
(which may throw if overloads exist). Should use `GetMethods().FirstOrDefault(m => m.GetParameters().Length == 12)`
for robustness against future overload additions.

**Status in WAVE2-LANE-E**: Not addressed. New E-1/E-2/E-3 tests added in BwaveLaneETests.cs
do not introduce the same pattern (they test simple private static helpers via single-name lookup).

**Risk**: LOW -- no current overload ambiguity; risk only if a future overload is added.

**Recommended Action**: Update the SubmitQxOcoPair GetMethod call before adding any overload.

---

### DW-LC-05 (P2): OPEN -- Carry Forward

**Summary**: Stale CCN values in XML doc comments on 6 methods post-LaneC extraction.
Comments still cite pre-extraction CCN values.

**Status in WAVE2-LANE-E**: Not addressed. New helpers added in this block carry correct
CCN values in their doc comments; the stale LaneC comments remain.

**Risk**: LOW -- no correctness impact; documentation drift only.

**Recommended Action**: Sweep all `/// CYC=` doc comments and update to current lizard values
in a dedicated documentation hygiene pass (no CCN change needed, editorial only).

---

### DW-LC-06 (P2): OPEN -- Carry Forward

**Summary**: `IsCancellableState` (PttBreakEven) and `IsNonTerminalPttBeState` (PttGlobalQuickExit)
are overlapping order-state predicates that could be consolidated into a shared
`PttOrderStatePredicates` static utility class.

**Status in WAVE2-LANE-E**: Not addressed.

**Risk**: LOW -- no duplication bug; purely an architectural consolidation opportunity.

**Recommended Action**: Evaluate overlap in a future block. Extract to shared utility if
the predicate logic is confirmed identical across both files.

---

## This Block's Deferred Items

### DW-LE-01 (P2): FormatOrderPrice Duplication

**Summary**: Both `PttFlatten` and `PttTrim` contain an identical `private static FormatOrderPrice(OrderType orderType, double price)` helper extracted in ticket E-3. The duplication is intentional and documented with a `// DW-LE-01` comment in both files.

**Root Cause**: The same price-formatting pattern is needed in two separate classes. Extracting
to a shared utility was deferred because the extraction is purely cosmetic (no correctness
concern) and adding a new shared class was outside the minimal-footprint scope of WAVE2-LANE-E.

**Risk**: LOW -- purely cosmetic duplication. Both copies have identical logic.
No correctness impact. No synchronisation burden (price formatting is stateless).

**Recommended Action**: In a future block, extract both `FormatOrderPrice` implementations
(along with `ResolveOrderParams` from DW-LC-02) into a shared `PttOrderUtils.cs` or
`PttOrderPriceHelper.cs` static class. Both items can be addressed in a single focused PR.

**Target Block**: B6 or future.

---

### DW-LE-02 (P1): 5 AT-LIMIT Methods (CCN=8 Exactly)

**Summary**: After WAVE2-LANE-E, five methods are exactly at the CCN=8 ceiling:

| Method | Class | CCN | Risk Level |
|--------|-------|-----|-----------|
| `SnapshotTargetOrders` | PttGlobalQuickExit | 8 | MEDIUM |
| `Execute(List<Order> forcedTargets)` | PttGlobalQuickExit | 8 | MEDIUM |
| `Execute` | PttBreakEvenSwap | 8 | MEDIUM |
| `FlattenPositionLocal` | PttFlatten | 8 | MEDIUM |
| `TrimPositionLocal` | PttTrim | 8 | MEDIUM |

**Risk**: MEDIUM -- any single new branch (if/else, switch case, ternary, null-conditional,
catch clause, logical operator) in any of these methods would immediately create a CCN=9
violation requiring a new extraction cycle.

**Recommended Action**: Before any feature work that touches these 5 methods:
1. Run `lizard src/PropTraderTools/Features/<File>.cs -l csharp -C 8` to confirm current CCN.
2. If the planned change adds even one branch, extract a helper FIRST (before the feature change).
3. Document the extraction in the ticket as a prerequisite step.

**Target Block**: Review required before any feature addition to these methods. Formal
extraction work deferred to future block if/when a CCN=9 trigger occurs.