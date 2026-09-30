# DW-B79-04 Plan Review

**Reviewer**: ptt-plan-reviewer (Phase 2)
**Plan**: `docs/brain/DW-B79-04/02-architecture-plan.md`
**Spec**: DW-B79-04 canonical ticket definitions (embedded in review request)
**Rules**: `docs/standards/jane-street/RULES_CATALOG.md` (JS-001..JS-110)
**Date**: 2026-08-20
**Result**: **REVIEW_PASS**

---

## Section A -- Violation Log

No violations found. The table below is provided for audit completeness.

| # | Rule ID | Severity | Description | Location in Plan | Finding |
|---|---------|----------|-------------|------------------|---------|
| - | JS-001 | P0 | throw in hot path | CancelAllAccountOrders, TryEvictFollowerBeSlot | CLEAR -- both methods are `void`; no `throw` in proposed code |
| - | JS-002 | P0 | return null | CancelAllAccountOrders, TryEvictFollowerBeSlot | CLEAR -- both methods are `void`; guard exits use bare `return;` |
| - | JS-003 | P0 | Magic string for discriminated state | stateOk filter | CLEAR -- `OrderState` enum values used directly, no string literals |
| - | JS-008 | P1 | Mutable fields on struct | N/A | CLEAR -- no new structs introduced |
| - | JS-009 | P1 | Dictionary for shared collection | N/A | CLEAR -- `ConcurrentDictionary` used throughout; no `Dictionary<K,V>` for thread-touched state |
| - | JS-010 | P1 | Public constructor on singleton/struct | N/A | CLEAR -- no new types introduced |
| - | JS-021 | P0 | lock() usage | Section 7 Threading Model | CLEAR -- explicitly confirmed zero `lock()` calls; ConcurrentDictionary is lock-free |
| - | JS-023 | P0 | UI update from off-thread without Dispatcher.InvokeAsync | TryEvictFollowerBeSlot Output.Process | CLEAR -- `Output.Process` is NT8 thread-safe; both methods execute on NT8 event thread |
| - | JS-033 | P0 | async void | CancelAllAccountOrders, TryEvictFollowerBeSlot | CLEAR -- both methods are synchronous `void`; no `async` keyword |
| - | CYC>8 | P1 | Method complexity exceeds 8 | Both methods | CLEAR -- T1: CYC=4, T2: CYC=4 (post-fix); both well within limit |

---

## Section B -- Spec Coverage Matrix

| Req ID | Spec Requirement | Addressed? | Plan Section |
|--------|-----------------|------------|--------------|
| DW-B79-CANCEL-01-R1 | Remove `OrderState.ChangeSubmitted` from `stateOk` in `CancelAllAccountOrders` | YES | Sec 2, Change B (L723) |
| DW-B79-CANCEL-01-R2 | Add `toCancel.RemoveAll(o => o.OrderState == Filled \|\| Cancelled)` before `acc.Cancel()` | YES | Sec 2, Change C |
| DW-B79-CANCEL-01-R3 | Update L710 comment (remove ChangeSubmitted from States list) | YES | Sec 2, Change A |
| DW-B79-CANCEL-01-R4 | New xUnit `[Fact]` `CancelAllAccountOrders_SkipsChangeSubmittedOrders` | YES | Sec 2, TDD Test Design |
| DW-B79-CANCEL-01-R5 | L2662 `MoveStopToBreakEven` ChangeSubmitted MUST NOT change | YES | Sec 1 (FROZEN), Sec 2 (rationale), Sec 11 (table) |
| DW-B79-LOG-01-R1 | Capture `bool` from `_pendingFollowerBeSlots.TryRemove` | YES | Sec 3, Change A |
| DW-B79-LOG-01-R2 | Gate `Output.Process` log on `slotEvicted` bool | YES | Sec 3, Change B |
| DW-B79-LOG-01-R3 | `_beReplaceAttempts.TryRemove` remains unconditional | YES | Sec 3, Change B (comment "ALWAYS reset on flat" preserved) |

All 8 spec requirements are addressed. Coverage: 8/8 (100%).

---

## Section C -- Checklist Findings (Item-by-Item)

**[PASS] Plan addresses both tickets completely**
- TICKET-1: Sections 2, 5, 6, 9, 10, 11 provide complete design, method shape, acceptance criteria, and component table.
- TICKET-2: Section 3 provides complete design, method shape, and no-test justification.

**[PASS] Ticket-1 removes ChangeSubmitted from stateOk AND adds RemoveAll filter**
- Change B: removes `|| o.OrderState == OrderState.ChangeSubmitted` from the 5-term stateOk predicate, leaving 4 terms.
- Change C: inserts `toCancel.RemoveAll(o => o.OrderState == OrderState.Filled || o.OrderState == OrderState.Cancelled)` after the foreach, before the `Count == 0` guard. Placement is correct -- operates on local `List<Order>`, cannot throw.

**[PASS] Ticket-1 explicitly marks L2662 in MoveStopToBreakEven as FROZEN/untouched**
- Section 1 uses the exact word "FROZEN" and states "must not be touched by this epic under any circumstances."
- Section 2 contains a dedicated sub-section ("Why L2662 in MoveStopToBreakEven MUST NOT Change") with line-by-line rationale distinguishing ACTION filter vs READ filter semantics.
- Section 11 Change Summary Table repeats the FROZEN designation.

**[PASS] Ticket-2 gates the log on bool returned from TryRemove**
- Change A: `bool slotEvicted = _pendingFollowerBeSlots.TryRemove(accName, out _);`
- Change B: wraps `NinjaTrader.Code.Output.Process(...)` in `if (slotEvicted) { ... }`.
- Key invariant correctly preserved: `_beReplaceAttempts.TryRemove(accName, out _)` is NOT inside the if-gate. Comment `// ALWAYS reset on flat` is explicitly required to be retained.

**[PASS] CYC impact correctly analyzed for both methods**
- T1: stateOk drops from 5 to 4 OR terms. RemoveAll lambda correctly excluded from structural branch count (it is an external delegate, not an inline decision point). CYC stays at 4. Analysis is sound.
- T2: `if (slotEvicted)` adds exactly 1 decision point. CYC increases from 3 to 4. Analysis is sound.
- Both methods remain well within the CYC <= 8 limit.

**[PASS] xUnit [Fact] test design for Ticket-1 is correct and sufficient**
- Annotation: `[Fact]` (xUnit). Correct -- NUnit/MSTest not used. Satisfies JS-051 (xUnit mandate).
- Strategy: IL token scanning via `MethodBody.GetILAsByteArray()` and `ldsfld` token resolution. This is the appropriate pattern given NT8 `Account` is a sealed runtime class that cannot be instantiated in a unit-test context.
- Primary assert: `OrderState.ChangeSubmitted` must NOT appear as an `ldsfld` target in the method IL.
- Secondary (regression guard) assert: `Working`, `Accepted`, `Submitted`, `Initialized` MUST appear in the IL -- prevents the false-positive case where the stateOk block was accidentally deleted entirely.
- Red-green contract explicit: "must FAIL against BEFORE state; must PASS after TICKET-1 is applied."
- File placement: `src/PropTraderTools/Tests/B79Tests.cs` (append to existing `B79Tests` class).

**[PASS] 7-scan checklist present and complete**
- SCAN-01: ASCII-only -- `grep -P "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` ✓
- SCAN-02: lock() (JS-021 P0) -- `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` ✓
- SCAN-03: async void (JS-033 P0) -- `grep -n "async void" src/PropTraderTools/CopyEngine.cs` ✓
- SCAN-04: return null (JS-002 P0) -- `grep -n "return null" src/PropTraderTools/CopyEngine.cs` ✓
- SCAN-05: throw new (JS-001 P0) -- `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` ✓
- SCAN-06: CYC <= 8 -- `python scripts/complexity_audit.py` + comment verification ✓
- SCAN-07: Build -- `build_readiness.ps1` + `dotnet csharpier check src/` ✓
- All 7 scans present with exact commands and expected results.

**[PASS] No P0 JS rule violations**
- JS-001: No `throw new` in either method. ✓
- JS-002: No `return null` in either method. ✓
- JS-021: No `lock()` in either method. `ConcurrentDictionary` is lock-free. ✓
- JS-033: No `async void` in either method. Both are synchronous. ✓

**[PASS] ASCII-only compliance noted**
- SCAN-01 is present and mandatory. All method shapes in the plan use ASCII-only identifiers and string literals. The `[BE-RETRY]` log string uses standard ASCII hyphens and brackets. No Unicode, no curly quotes, no emoji.

**[PASS] Acceptance criteria match spec**
- Section 10 Acceptance Criteria table covers all spec requirements plus build, complexity, and lock-scan verification.
- L2662 non-diff criterion is verifiable via `git diff`.
- Test count of 292 (291 + 1) is explicitly stated as the pass threshold.

---

## Section D -- NT8 API Review

Per `docs/standards/NT8_FULL_REFERENCE.md` mandate: all NT8 API claimed in the plan must be valid AddOn API.

| API | Plan Claim | Verdict |
|-----|-----------|---------|
| `OrderState.ChangeSubmitted` | Existing at L723 in-file | VALID -- NT8 `OrderState` enum member |
| `OrderState.Filled` | Existing in-file (referenced at existing L1078) | VALID |
| `OrderState.Cancelled` | Standard NT8 enum member | VALID |
| `List<Order>.RemoveAll` | BCL method, not NT8 | VALID (BCL) |
| `ConcurrentDictionary.TryRemove` | BCL method, not NT8 | VALID (BCL) |
| `NinjaTrader.Code.Output.Process` | Existing at L1084 in-file | VALID -- NT8 AddOn API |
| `acc.Cancel(toCancel)` | Existing in-file (unchanged) | VALID |

No `AtmStrategyCreate`, no `StrategyBase`-only API, no invalid AddOn API surface. All APIs confirmed by existing in-file usage.

---

## Section E -- Threading Model Review

| Method | Thread | Shared State | Lock-Free Mechanism | Verdict |
|--------|--------|-------------|---------------------|---------|
| `CancelAllAccountOrders` | NT8 dispatch thread | `acc.Orders` (NT8-managed, safe on dispatch thread), local `List<Order>` (stack) | N/A -- single-threaded dispatch | CLEAR |
| `TryEvictFollowerBeSlot` | NT8 event thread (OnOrderUpdate) | `_pendingFollowerBeSlots` (ConcurrentDictionary), `_beReplaceAttempts` (ConcurrentDictionary) | `ConcurrentDictionary.TryRemove` is lock-free | CLEAR |

`bool slotEvicted` is a stack-allocated value type (not shared). `NinjaTrader.Code.Output.Process` is documented as thread-safe. No `Dispatcher.InvokeAsync` required because neither method touches NT8 UI objects. JS-023 satisfied.

---

## Section F -- Minor Observations (Non-Blocking)

1. **SCAN-06 phrasing is slightly soft**: "Verify *if* the CYC annotation is updated" should ideally read "Verify the CYC annotation IS updated." This is stylistic -- the plan body (Section 3) unambiguously mandates the annotation update. Not a FAIL trigger.

2. **No DW- deferred items created**: This is correct. Both tickets are complete, surgical, and require no deferred work. Section 1 confirms the DW-B79-03 deferred backlog is empty.

---

## Section G -- Final Verdict

**REVIEW_PASS**

All 10 checklist items pass. Zero P0 violations. Zero P1 violations. All 8 spec requirements addressed. 7-scan checklist complete with correct commands and expected results. NT8 API surface valid. Threading model sound. Test strategy appropriate for the NT8 sealed-class constraint. L2662 protection is explicit, motivated, and carried through all summary sections.

**Gate status**: REVIEW_PASS -- Phase 3 (ticket generation) is UNLOCKED.
