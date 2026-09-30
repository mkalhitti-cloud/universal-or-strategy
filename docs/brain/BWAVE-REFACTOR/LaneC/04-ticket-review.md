# Ticket Review: BWAVE-REFACTOR LaneC

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-09-06
**Tickets file**: `docs/brain/BWAVE-REFACTOR/LaneC/04-tickets.md`
**Plan file**: `docs/brain/BWAVE-REFACTOR/LaneC/02-architecture-plan.md`
**Rules source**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## Ticket C-1: CCN Reduction -- PttQuickExit + PttGlobalQuickExit + PttBreakEven

### Traceability

All 10 CCN violations addressed by C-1 appear in the plan §1 scope table:
- `PttQuickExit::Execute` (CCN=32) -- plan §3.1 ✅
- `PttGlobalQuickExit::SnapshotTargetOrders` (CCN=20) -- plan §3.2 ✅
- `PttGlobalQuickExit::WaitForPttBeCancelled` (CCN=10) -- plan §3.10 ✅
- `PttGlobalQuickExit::Execute()` (CCN=9) -- plan §3.12 ✅
- `PttGlobalQuickExit::CancelPttBeOrders` (CCN=9) -- plan §3.13 ✅
- `PttBreakEven::CancelStaleBracketsLocal` (CCN=16) -- plan §3.3 ✅
- `PttBreakEven::SubmitBeTargetsLocal` (CCN=15) -- plan §3.4 ✅
- `PttBreakEven::SnapshotTargetsLocal` (CCN=13) -- plan §3.6 ✅
- `PttBreakEven::IsPttQxTarget` (CCN=12) -- plan §3.9 ✅
- `PttBreakEven::SubmitBeStopLocal` (CCN=9) -- plan §3.14 ✅

All 14 new helpers (SubmitStopOrder, SubmitTargetOrder, SubmitQxOcoPair, IsTargetOrder,
DeduplicateByPrice, LogLeaderDiag, IsNonTerminalForInstr, IsCancellableState, IsStaleOrder,
SubmitBareStop, SubmitBePair, IsSnapshotEligibleState, IsInvalidInput, SafeName) trace to
plan §§3.1–3.14. No phantom work.

**IsPttQxTarget in-place rewrite**: Ticket §5.8 takes the in-place StartsWith approach,
not the HasQxTargetBody helper approach. Plan §3.9 explicitly concludes: "No new extracted
helper needed -- pure simplification of the expression." Ticket is aligned with plan's
authoritative final conclusion. No traceability violation.

**SubmitQxOcoPair signature evolution**: Plan §3.1 initially shows string-return variant;
ticket §4.1 evolves to void+ref after self-documenting CCN analysis showing Execute would
exceed CCN=8 without the ref approach. The plan §3.1 explicitly states "if (i==0) firstOcoId
branch -- removed (moved into SubmitQxOcoPair)" which the ref variant correctly implements.
Consistent with plan intent. No traceability violation.

**Result**: PASS

---

### JS Pre-Check (P0 rules -- JS-001, JS-002, JS-021, JS-033)

| Rule | Check | Finding |
|------|-------|---------|
| JS-002 (no return null) | `SubmitQxOcoPair` is `void` (no return) | PASS |
| JS-002 | `DeduplicateByPrice` returns initialized `List` (never null per ticket §4.2) | PASS |
| JS-002 | `SafeName` returns `string` -- `"null"` literal when acc is null (never null) | PASS |
| JS-002 | `IsInvalidInput` returns `bool` | PASS |
| JS-002 | All other new helpers return `void`, `bool`, or value type | PASS |
| JS-021 (no lock) | No `lock()` described anywhere in C-1 | PASS |
| JS-001 (no throw in hot path) | SubmitStopOrder, SubmitTargetOrder, SubmitBareStop, SubmitBePair: all use try/catch, no `throw` per ticket §§4.1, 4.3 | PASS |
| JS-033 (no async void) | All new helpers described as synchronous (ticket §7 JS rules table) | PASS |

**Result**: PASS

---

### CYC Pre-Check

Complete before/after table present in ticket §6 with 24 rows. All predicted post-extraction
CCN values are <= 8. Full detail:

| Method | CCN Before | CCN After | <= 8? |
|--------|-----------|-----------|-------|
| Execute (PttQuickExit) | 32 | 8 | YES |
| SubmitStopOrder | NEW | 2 | YES |
| SubmitTargetOrder | NEW | 2 | YES |
| SubmitQxOcoPair (void+ref) | NEW | 6 | YES |
| SnapshotTargetOrders | 20 | 6 | YES |
| IsTargetOrder | NEW | 3 | YES |
| DeduplicateByPrice | NEW | 2 | YES |
| Execute() (PttGlobalQE) | 9 | 8 | YES |
| LogLeaderDiag | NEW | 2 | YES |
| WaitForPttBeCancelled | 10 | 6 | YES |
| IsNonTerminalForInstr | NEW | 4 | YES |
| CancelPttBeOrders | 9 | 5 | YES |
| CancelStaleBracketsLocal | 16 | 6 | YES |
| IsCancellableState | NEW | 5 | YES |
| IsStaleOrder | NEW | 3 | YES |
| SubmitBeTargetsLocal | 15 | 4 | YES |
| SubmitBareStop | NEW | 3 | YES |
| SubmitBePair | NEW | 3 | YES |
| SnapshotTargetsLocal | 13 | 5 | YES |
| IsSnapshotEligibleState | NEW | 5 | YES |
| IsPttQxTarget | 12 | 5 | YES (in-place rewrite) |
| SubmitBeStopLocal | 9 | 6 | YES |
| IsInvalidInput | NEW | 1 | YES |
| SafeName | NEW | 1 | YES |

`SubmitQxOcoPair` CCN breakdown documented at ticket §6 footnote:
base(1) + tNQty ternary (&&+?)=2 + tNQty<=0=1 + if(i==0)firstOcoId=1 = **6**. PASS.

SCAN-07 lizard command: present at ticket §11, with full PowerShell command and
expected "0 rows output" gate. PASS.

**Result**: PASS

---

### NT8 Constraints

| Constraint | Check | Finding |
|------------|-------|---------|
| NT8-049 StopMarket: arg6=0, arg7=stopPrice | SubmitStopOrder: arg6=0 arg7=snapshotStop (ticket §4.1 doc comment) | PASS |
| NT8-049 Limit: arg6=limitPrice, arg7=0 | SubmitTargetOrder: arg6=tNPrice arg7=0 (ticket §4.1 doc comment) | PASS |
| NT8-049 SubmitBareStop | arg6=0 arg7=bePrice (ticket §4.3 doc comment) | PASS |
| NT8-049 SubmitBePair | stop arg6=0 arg7=bePrice; target arg6=t.Price arg7=0 (ticket §4.3 doc comment + §7) | PASS |
| NT8-007 arg11=(CustomOrder)null | All submit helpers specify `(NinjaTrader.Cbi.CustomOrder)null` (ticket §7 NT8 block) | PASS |
| NT8-013 DateTime.MaxValue for GTC | All CreateOrder calls use `DateTime.MaxValue` (ticket §7 NT8 block) | PASS |
| NT8-014 Signal names start "PTT-" | SubmitStopOrder: "PTT-QX-Stop", SubmitTargetOrder: "PTT-QX-T{N}", SubmitBareStop: "PTT-BE-Stop", SubmitBePair: "PTT-BE-" prefix (ticket §7 NT8 block) | PASS |

**Result**: PASS

---

### Test Coverage

| Helper | [Fact] present? | Test name | xUnit? |
|--------|----------------|-----------|--------|
| SubmitStopOrder | YES | `PttQuickExit_SubmitStopOrder_Exists` | xUnit |
| SubmitTargetOrder | YES | `PttQuickExit_SubmitTargetOrder_Exists` | xUnit |
| SubmitQxOcoPair | YES | `PttQuickExit_SubmitQxOcoPair_Exists` | xUnit |
| IsTargetOrder | YES | `PttGlobalQuickExit_IsTargetOrder_Exists` | xUnit |
| DeduplicateByPrice | YES | `PttGlobalQuickExit_DeduplicateByPrice_Exists` | xUnit |
| LogLeaderDiag | YES | `PttGlobalQuickExit_LogLeaderDiag_Exists` | xUnit |
| IsNonTerminalForInstr | YES | `PttGlobalQuickExit_IsNonTerminalForInstr_Exists` | xUnit |
| IsCancellableState | YES | `PttBreakEven_IsCancellableState_Exists` | xUnit |
| IsStaleOrder | YES | `PttBreakEven_IsStaleOrder_Exists` | xUnit |
| SubmitBareStop | YES | `PttBreakEven_SubmitBareStop_Exists` | xUnit |
| SubmitBePair | YES | `PttBreakEven_SubmitBePair_Exists` | xUnit |
| IsSnapshotEligibleState | YES | `PttBreakEven_IsSnapshotEligibleState_Exists` | xUnit |
| IsInvalidInput | YES | `PttBreakEven_IsInvalidInput_Exists` | xUnit |
| SafeName | YES | `PttBreakEven_SafeName_Exists` | xUnit |
| IsPttQxTarget (in-place rewrite) | N/A -- no new helper | Per plan §3.9 + ticket §5.8: no test required | N/A |

14 new helpers = 14 [Fact] tests. No NUnit, no MSTest.

**WARN (non-blocking)**: `PttQuickExit_SubmitQxOcoPair_Exists` asserts `Assert.NotNull(m)` twice
but does NOT assert parameter count. The final signature has 12 parameters (including `ref string
firstOcoId`). A future build may silently pass even with an incorrect overload. This is a quality
gap but does not constitute a blocking violation (the existence check still functions).
Recommend architect adds `Assert.Equal(12, m.GetParameters().Length)` in next pass.

**Result**: PASS

---

### Scan Checklist Presence

SCAN-01 through SCAN-07 all present in ticket C-1 §11:

| Scan | Present? |
|------|---------|
| SCAN-01 (lock grep) | YES |
| SCAN-02 (non-ASCII chars) | YES |
| SCAN-03 (FontFamily) | YES |
| SCAN-04 (hardcoded hex color) | YES |
| SCAN-05 (PTT- prefix on CreateOrder) | YES |
| SCAN-06 (DateTime.Now) | YES |
| SCAN-07 (lizard CCN) | YES -- full PowerShell command with 0-rows gate |

**Result**: PASS

---

### File Routing

All .cs source paths:
- `src/PropTraderTools/Features/PttQuickExit.cs` -- Wave workspace ✅
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` -- Wave workspace ✅
- `src/PropTraderTools/Features/PttBreakEven.cs` -- Wave workspace ✅
- `src/PropTraderTools/Tests/BwaveRefactorLaneCTests.cs` -- Wave workspace ✅

Explicitly lists do-not-touch files: `CopyEngine.cs`, `TradeCopierPanel.cs`, `TradeCopierWindow.cs`. ✅

**Result**: PASS

---

### C-1 VERDICT: TICKET_REVIEW_PASS

---
---

## Ticket C-2: CCN Reduction -- PttBreakEvenSwap + PttTrim + PttFlatten + PttCancel

### Traceability

All 4 CCN violations addressed by C-2 appear in the plan §1 scope table:
- `PttBreakEvenSwap::Execute` (CCN=15) -- plan §3.5 ✅
- `PttTrim::TrimPositionLocal` (CCN=13) -- plan §3.7 ✅
- `PttFlatten::FlattenPositionLocal` (CCN=13) -- plan §3.8 ✅
- `PttCancel::CancelWorkingEntriesLocal` (CCN=10) -- plan §3.11 ✅

All 5 new helpers (SubmitBareStopSwap, SubmitSwapPair, ResolveOrderParams[PttTrim],
ResolveOrderParams[PttFlatten], IsWorkingEntryOrder) trace to plan §§3.5, 3.7, 3.8, 3.11.
No phantom work.

**SubmitBareStopSwap parameter count**: Plan §3.5 shows 5 parameters (no `posQty`).
Ticket §4.1 adds `int posQty` as 6th parameter. The ticket documents the justification:
`CreateOrder` needs `posQty` and it is supplied from `pos.Quantity` at the call site in
Execute. This is a necessary engineering detail (the plan's pseudocode at §3.5 shows
`pos.Quantity` being passed in the Execute extraction, implying the helper receives it).
No behavioral deviation. No traceability violation.

**Result**: PASS

---

### JS Pre-Check (P0 rules -- JS-001, JS-002, JS-021, JS-033)

| Rule | Check | Finding |
|------|-------|---------|
| JS-002 (no return null) | SubmitBareStopSwap: `void` | PASS |
| JS-002 | SubmitSwapPair: `void` | PASS |
| JS-002 | ResolveOrderParams (both): returns `(OrderType, double, double)` value tuple (never null) | PASS |
| JS-002 | IsWorkingEntryOrder: returns `bool` | PASS |
| JS-021 (no lock) | No `lock()` described anywhere in C-2 | PASS |
| JS-001 (no throw in hot path) | SubmitBareStopSwap, SubmitSwapPair: try/catch, no `throw` (ticket §7 JS rules table) | PASS |
| JS-033 (no async void) | All new helpers synchronous (ticket §7 JS rules table) | PASS |

**Result**: PASS

---

### CYC Pre-Check

Complete before/after table present in ticket §6 with 9 rows. All predicted post-extraction
CCN values are <= 8:

| Method | CCN Before | CCN After | <= 8? |
|--------|-----------|-----------|-------|
| Execute (PttBreakEvenSwap) | 15 | 8 | YES |
| SubmitBareStopSwap | NEW | 3 (footnote: may be 4 with else-log) | YES |
| SubmitSwapPair | NEW | 3 (footnote: may be 4) | YES |
| TrimPositionLocal | 13 | 6 | YES |
| ResolveOrderParams (PttTrim) | NEW | 5 | YES |
| FlattenPositionLocal | 13 | 6 | YES |
| ResolveOrderParams (PttFlatten) | NEW | 5 | YES |
| CancelWorkingEntriesLocal | 10 | 6 | YES |
| IsWorkingEntryOrder | NEW | 4 | YES |

CCN breakdowns documented for SubmitBareStopSwap (4 per ticket §6 footnote),
SubmitSwapPair (4), IsWorkingEntryOrder (4). All within limit.

`Execute (PttBreakEvenSwap)` CCN=8 detailed at ticket §5.1: base(1) + acc||instr(1) +
pos||qty(1) + isLong ternary(1) + targets||Count(1) + targets-Count branch(1) + for-loop(1) = 8.
Note: the ticket's own arithmetic lists `null-guard(||)=1 + flat-guard(||)=1 + isLong-ternary=1 +
targets-null-||=1 + targets-Count-branch=1 + for-loop=1` = base(1)+6 = 7, then confirms
"exactly at limit" as 8 elsewhere. The count of 8 in the CYC table is consistent with plan §3.5
which shows CCN≈6 after extraction (post-extraction count in plan is 6; ticket computes 8 because
plan missed two `||` operators). Engineer must run SCAN-07 to confirm. No pre-check failure.

SCAN-07 lizard command: present at ticket C-2 §11, with full PowerShell command and
expected "0 rows output" gate. PASS.

**Result**: PASS

---

### NT8 Constraints

| Constraint | Check | Finding |
|------------|-------|---------|
| NT8-049 SubmitBareStopSwap | arg6=0, arg7=newStop (ticket §4.1 doc comment + §7) | PASS |
| NT8-049 SubmitSwapPair | stop arg6=0 arg7=newStop; target arg6=t.Price arg7=0 (ticket §4.1 doc comment + §7) | PASS |
| NT8-007 arg11=(CustomOrder)null | Both submit helpers specify `(NinjaTrader.Cbi.CustomOrder)null` (ticket §7 NT8 block) | PASS |
| NT8-013 DateTime.MaxValue | All CreateOrder calls use `DateTime.MaxValue` (ticket §7 NT8 block) | PASS |
| NT8-014 Signal names start "PTT-" | "PTT-BE-Stop", "PTT-BE-Stop-N", "PTT-BE-Target-N" all start with "PTT-" (ticket §7 NT8 block) | PASS |
| ResolveOrderParams (PttTrim, PttFlatten) | Does not call CreateOrder -- returns tuple consumed by caller. NT8-049 note at ticket §4.2: "preserved in caller" | PASS |

**Result**: PASS

---

### Test Coverage

| Helper | [Fact] present? | Test name | xUnit? |
|--------|----------------|-----------|--------|
| SubmitBareStopSwap | YES | `PttBreakEvenSwap_SubmitBareStopSwap_Exists` | xUnit |
| SubmitSwapPair | YES | `PttBreakEvenSwap_SubmitSwapPair_Exists` | xUnit |
| ResolveOrderParams (PttTrim) | YES | `PttTrim_ResolveOrderParams_Exists` | xUnit |
| ResolveOrderParams (PttFlatten) | YES | `PttFlatten_ResolveOrderParams_Exists` | xUnit |
| IsWorkingEntryOrder | YES | `PttCancel_IsWorkingEntryOrder_Exists` | xUnit |

5 new helpers = 5 [Fact] tests. No NUnit, no MSTest.

Parameter count assertions verified against signatures:
- `SubmitBareStopSwap`: test asserts 6 params, signature has 6 (acc, instr, isLong, stopDir, newStop, posQty) ✅
- `SubmitSwapPair`: test asserts 8 params, signature has 8 (acc, instr, isLong, stopDir, newStop, ocoId_i, i, t) ✅
- `ResolveOrderParams` (both): test asserts 5 params, signature has 5 (pos, buffer, ask, bid, tickSize) ✅
- `IsWorkingEntryOrder`: test asserts 2 params, signature has 2 (o, instr) ✅

**Result**: PASS

---

### Scan Checklist Presence

SCAN-01 through SCAN-07 all present in ticket C-2 §11:

| Scan | Present? |
|------|---------|
| SCAN-01 (lock grep) | YES |
| SCAN-02 (non-ASCII chars) | YES |
| SCAN-03 (FontFamily) | YES |
| SCAN-04 (hardcoded hex color) | YES |
| SCAN-05 (PTT- prefix on CreateOrder) | YES |
| SCAN-06 (DateTime.Now) | YES |
| SCAN-07 (lizard CCN) | YES -- full PowerShell command with 0-rows gate |

**Result**: PASS

---

### File Routing

All .cs source paths:
- `src/PropTraderTools/Features/PttBreakEvenSwap.cs` -- Wave workspace ✅
- `src/PropTraderTools/Features/PttTrim.cs` -- Wave workspace ✅
- `src/PropTraderTools/Features/PttFlatten.cs` -- Wave workspace ✅
- `src/PropTraderTools/Features/PttCancel.cs` -- Wave workspace ✅
- `src/PropTraderTools/Tests/BwaveRefactorLaneCTests.cs` (append) -- Wave workspace ✅

Explicitly lists do-not-touch files: `CopyEngine.cs`, `TradeCopierPanel.cs`, `TradeCopierWindow.cs`,
and all C-1 files. ✅

**Result**: PASS

---

### C-2 VERDICT: TICKET_REVIEW_PASS

---
---

## Aggregate Spec Coverage

| Plan Section | CCN Violation | Ticket | Covered? |
|-------------|--------------|--------|---------|
| §3.1 | PttQuickExit::Execute (CCN=32) | C-1 | YES |
| §3.2 | PttGlobalQuickExit::SnapshotTargetOrders (CCN=20) | C-1 | YES |
| §3.3 | PttBreakEven::CancelStaleBracketsLocal (CCN=16) | C-1 | YES |
| §3.4 | PttBreakEven::SubmitBeTargetsLocal (CCN=15) | C-1 | YES |
| §3.5 | PttBreakEvenSwap::Execute (CCN=15) | C-2 | YES |
| §3.6 | PttBreakEven::SnapshotTargetsLocal (CCN=13) | C-1 | YES |
| §3.7 | PttTrim::TrimPositionLocal (CCN=13) | C-2 | YES |
| §3.8 | PttFlatten::FlattenPositionLocal (CCN=13) | C-2 | YES |
| §3.9 | PttBreakEven::IsPttQxTarget (CCN=12) | C-1 | YES |
| §3.10 | PttGlobalQuickExit::WaitForPttBeCancelled (CCN=10) | C-1 | YES |
| §3.11 | PttCancel::CancelWorkingEntriesLocal (CCN=10) | C-2 | YES |
| §3.12 | PttGlobalQuickExit::Execute() (CCN=9) | C-1 | YES |
| §3.13 | PttGlobalQuickExit::CancelPttBeOrders (CCN=9) | C-1 | YES |
| §3.14 | PttBreakEven::SubmitBeStopLocal (CCN=9) | C-1 | YES |

All 14 plan violations: **14/14 covered** across C-1 (10) and C-2 (4). No duplicates. No gaps.

---

## Notes for Architect (non-blocking)

1. **`PttQuickExit_SubmitQxOcoPair_Exists` test** (C-1, §12): The reflection test checks
   `Assert.NotNull(m)` but does not assert parameter count. The final void+ref signature has 12
   parameters. Recommend adding `Assert.Equal(12, m.GetParameters().Length)` to prevent a
   wrong-overload silent pass in a future build cycle.

2. **`SubmitBareStopSwap` plan signature delta** (C-2, §4.1): Plan §3.5 listed 5 parameters;
   ticket adds `int posQty` as 6th. The [Fact] test correctly asserts 6 params. No action needed
   unless the plan doc is used as an authoritative reference by a downstream agent.

---

## Overall: TICKET_REVIEW_PASS

Both tickets pass all checks:
- Traceability: PASS (14/14 plan violations covered, no phantom work)
- JS Pre-Check: PASS (JS-001, JS-002, JS-021, JS-033 clean on all 19 helpers)
- CYC Pre-Check: PASS (all post-extraction CCN <= 8, SCAN-07 command present in both tickets)
- NT8 Constraints: PASS (NT8-049, NT8-007, NT8-013, NT8-014 all correct in both tickets)
- Test Coverage: PASS (19 [Fact] xUnit tests for 19 new helpers)
- Scan Checklist: PASS (SCAN-01 through SCAN-07 present in both C-1 §11 and C-2 §11)
- File Routing: PASS (all paths in `src/PropTraderTools/` Wave workspace, not Director)
