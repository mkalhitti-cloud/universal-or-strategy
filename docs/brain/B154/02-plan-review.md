# Plan Review — Block B154
# GTC Exit Order Survival — BE/QX Stale Order Sweep + Drag Orphan Suffix Fix

**Reviewer:** PTT Plan Reviewer (ptt-plan-reviewer mode)
**Phase:** 2 — Plan Review
**Plan file:** `docs/brain/B154/02-architecture-plan.md`
**Result:** REVIEW_PASS

---

## Violation Table

No violations found. All items PASS.

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| DW-B154-01: PTT-BE-* GTC exit orders survive into new trade | YES | §2 TICKET 1 — `TryCancelStaleGtcExitOnEntry`, StartsWith "PTT-BE-" filter |
| DW-B154-02: PTT-QX-* GTC exit orders survive into new trade | YES | §2 TICKET 1 — same helper, StartsWith "PTT-QX-" filter |
| DW-B154-03: PTT-STP-Drag-N / PTT-TGT-Drag-N suffixed variants missed by orphan sweep | YES | §2 TICKET 2 — `IsPttDragOrphanCancellable` + `IsPttDragOrphanCancellableTestable` StartsWith fix |
| DW-B154-04: PTT-Copy / Entry late fills (drain issue) — out of scope | YES | §1 §2 §6 — explicitly excluded; deferred to future block |
| DW-B153-05 prior backlog carry-forward | YES | §6 — STILL OPEN, engineer guidance included |
| DW-B153-06 prior backlog carry-forward | YES | §6 — STILL OPEN, target noted |

---

## Checklist Results

### [1] LANE-SPLIT GATE compliance
**PASS.**
Section "LANE-SPLIT GATE RESULT" present (line 11). Gate result "LANES-APPROVED" stated.
- Q1=NO ✓ (tickets >50 lines apart in CopyEngine.cs)
- Q2=NO ✓ (TICKET 2 independent of TICKET 1 design)
- Q3=YES ✓ (each defect independently harmful)
- Q4=YES ✓ (each fix has independent SIM/unit verification path)
Sequential-only constraint stated (TICKET 1 committed before TICKET 2 begins).

### [2] CYC counts
**PASS.**
- `TryCancelStaleGtcExitOnEntry` CYC=8: 8 branches enumerated in step table (§2 TICKET 1). Within CYC<=8 budget.
- `IsPttCopyEntryFilled` CYC=4: 4 short-circuit AND conditions listed (a–d). Note: condition (c) contains an internal `||`; McCabe count could be 5. Either way, well within CYC<=8 budget. Not a violation.
- `OnOrderUpdate` delta=0: explicitly stated in call-site annotation ("CYC delta=0 to OnOrderUpdate (extracted helper)").

### [3] No lock() introduced
**PASS.**
No `lock()` call appears in any code snippet in the plan.
SCAN-8 (`grep -rn "lock\s*(" src/PropTraderTools/`) required at zero matches as scan gate.

### [4] ASCII-only string literals
**PASS.**
All string literals in plan code snippets are 7-bit ASCII:
`"PTT-BE-"`, `"PTT-QX-"`, `"Entry"`, `"PTT-Copy"`, `"PTT-TGT-Drag"`, `"PTT-STP-Drag"`,
`"[BE-ENTRY-SWEEP] acc="`, `" cancelled="`, `"StartsWith: matches suffixed..."`.
SCAN-9 (byte-scan > 127) required as scan gate.

### [5] Call site position
**PASS.**
§2 TICKET 1 states: "Insert after `TryNakedDetect(e)` at line 1712, before `HandleDrainTerminalState` at line 1717."
Position is exact and unambiguous.

### [6] TriggerPending in state filter
**PASS.**
Step 4d (§2 TICKET 1 helper table): State set explicitly listed as
`{Working, Accepted, Submitted, Initialized, TriggerPending}`.
Rationale provided: references `IsQxCancelEligible2` pattern (not `IsCancelAllStateOk`)
per HOTFIX-QX-DOUBLE-01 at CopyEngine.cs line 1039.

### [7] acc.Cancel(stale) with List<Order>
**PASS.**
Step 3 declares `var stale = new List<Order>()`.
Step 6 calls `acc.Cancel(stale)` — `stale` is `List<Order>`, not `Order[]`.

### [8] Both predicates updated
**PASS.**
- Change 1: `IsPttDragOrphanCancellable` at line 1998 — equality → `StartsWith(..., Ordinal)`.
- Change 2: `IsPttDragOrphanCancellableTestable` at line 2016 — same substitution.
Both changes specified with current and replacement code blocks.

### [9] Obsolete "must not be generalized" comment flagged for removal
**PASS.**
§2 TICKET 2 "Comment update" section explicitly states:
- Add new comment describing StartsWith semantics and B142 suffixed variants.
- **Remove** obsolete `"must not be generalized"` comment (superseded by B154).

### [10] Test seam IsPttCopyEntryFilledTestable
**PASS.**
§2 TICKET 1 "Test seam" block specifies:
```csharp
internal bool IsPttCopyEntryFilledTestable(Order o) => IsPttCopyEntryFilled(o);
```
Placed near existing `IsPttCopyEntry` testable seam at line 7819 pattern.

### [11] TICKET 2 StartsWith test coverage
**PASS.** All four required categories present in §3 test plan:
| Category | Test Name | Input |
|----------|-----------|-------|
| Exact match (PTT-TGT-Drag) | `...ShouldReturnTrue_WhenNameIsPttTgtDragExact` | `"PTT-TGT-Drag"` |
| Suffixed (PTT-TGT-Drag-2) | `...ShouldReturnTrue_WhenNameIsPttTgtDragSuffixed` | `"PTT-TGT-Drag-2"` |
| Non-matching (PTT-TGT-Other) | `...ShouldReturnFalse_WhenNameIsUnrelated` | `"PTT-TGT-Other"` |
| State-fail | `...ShouldReturnFalse_WhenStateIsNotWorking` | `OrderState.Filled` (valid name) |
PTT-STP-Drag exact and suffixed also covered (4 additional tests).

### [12] DW-B154-04 explicitly excluded from scope
**PASS.**
- §1 Root Cause: "DW-B154-04 — Out of Scope / PTT-Copy / Entry late fills (drain issue) — deferred. Not addressed in B154."
- §1 Defect Register row: "OUT OF SCOPE — deferred."
- §6 Prior Backlog row: "Deferred to future block."

### [13] Prior backlog DW-B153-05 and DW-B153-06 acknowledged
**PASS.**
§6 Prior Backlog Carry-Forward table contains both items:
- DW-B153-05 (stale CYC annotation): STILL OPEN — engineer guidance to check/correct annotation if adjacent to changes.
- DW-B153-06 (dead code `UpdateLegTargetPrice`): STILL OPEN — target nearest src-touching block for stop-drag region.

---

## DNA Rules Audit

| Rule | Category | Result |
|------|----------|--------|
| JS-021 lock() banned | CONCURRENCY P0 | PASS — no lock() in plan |
| JS-021 Monitor/Mutex/SemaphoreSlim | CONCURRENCY P0 | PASS — not present |
| JS-023 UI update from off-thread | CONCURRENCY P0 | PASS — no UI mutation in design |
| JS-001 throw in gate chain | TYPE SAFETY P0 | PASS — no throw; guard returns early |
| JS-002 null return where value expected | TYPE SAFETY P0 | PASS — predicate returns bool; helper is void |
| JS-003 magic string discriminated state | TYPE SAFETY P0 | PASS — order names are identifiers, not discriminated union keys |
| JS-009 Dictionary for shared collection | IMMUTABILITY P1 | PASS — List<Order> used, not Dictionary |
| JS-008 Mutable struct fields | IMMUTABILITY P1 | PASS — no structs introduced |
| JS-008 SolidColorBrush not Frozen | IMMUTABILITY P1 | PASS — no brush usage |
| JS-010 Public constructor on singleton | CONSTRUCTION P1 | PASS — no new types or constructors |
| NT8: async/await in lifecycle | NT8 HARD | PASS — no async in any lifecycle method |
| NT8: Account.All in constructor | NT8 HARD | PASS — account access via order.Account at call time |
| NT8: sealed TradeCopierWindow | NT8 HARD | PASS — no window type changes |
| NT8: FontFamily override | NT8 SCAN-03 | PASS — no UI changes |
| NT8: Hardcoded #RRGGBB hex | NT8 SCAN-04 | PASS — no hex color literals |
| NT8: CreateOrder without PTT- prefix | NT8 SCAN-05 | PASS — no CreateOrder calls |
| NT8: DateTime.Now | NT8 SCAN-06 | PASS — no DateTime usage |
| CYC > 8 in any method | COMPLEXITY P1 | PASS — max CYC=8 for TryCancelStaleGtcExitOnEntry |

---

## Summary

**13 of 13 checklist items: PASS**
**18 of 18 DNA rules: PASS**
**0 violations found**

---

## REVIEW_PASS

Plan is approved. Phase 3 (ticket generation) is unlocked.
