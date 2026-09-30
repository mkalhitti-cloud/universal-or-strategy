# B137 Ticket Review

**Block**: B137
**Phase**: 3.5 — Ticket Review
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-09-08
**Input**: docs/brain/B137/04-tickets.md (TICKETS_COMPLETE, sourced from REVIEW_PASS plan — third pass)
**Source cross-check**: src/PropTraderTools/CopyEngine.cs (L2290-2445, L2590-2700 verified)
**Plan review**: docs/brain/B137/02-plan-review.md (REVIEW_PASS — all 4 prior violations resolved)

---

## T1 — Phase C Extraction from SyncAtmFollowerTarget

**Ticket**: Extract Phase C inline block from SyncAtmFollowerTarget to ExecutePhaseCStopReplacement

### Traceability

PASS

- T1 is explicitly mapped as a structural prerequisite for DW-B147/DW-B149 (T2). No phantom work. No missing plan item.
- Architecture plan confirms: "T1: pure structural refactor, zero behavior change. Standalone value: CYC headroom."

### JS Pre-Check

PASS

- No `lock()` described in ticket body. JS-021 satisfied.
- No `async void` described. JS-033 satisfied.
- No `throw new Exception` in hot path. JS-001 satisfied.
- No `return null` added. JS-002 satisfied (void return method).
- No Dictionary for shared state. JS-009 N/A.
- All identifiers ASCII-only: "ExecutePhaseCStopReplacement", "PTT-STP-Drag", "PTT-TGT-Drag". JS ASCII-only satisfied.

### CYC Pre-Check

PASS

- Source-verified: SyncAtmFollowerTarget CYC=8 at L2363-2364 with 8 enumerated branches. The `leaderOrder?.Account` null-conditional at L2441 contributes one of those branches.
- Extraction removes `?.` null-conditional from parent body into ExecutePhaseCStopReplacement. Result: SyncAtmFollowerTarget CYC=8-1=7 after T1. Within limit.
- ExecutePhaseCStopReplacement: base(1) + `leaderOrder?.Account` null-conditional(1) = CYC=2. Within limit.
- Final CYC values after T1: SyncAtmFollowerTarget=7, ExecutePhaseCStopReplacement=2. Both <= 8. ✅

### NT8 Check

PASS

- No new NT8 API calls introduced in T1. Extraction moves existing calls verbatim.
- No StrategyBase-only API. No `AtmStrategyCreate` or `AtmStrategyChangeStopTarget`.
- No `Account.All` outside Loaded handler. No `sealed` on TradeCopierWindow. No `FontFamily`.
- No hardcoded hex color. No `DateTime.Now`.

### Test Coverage

PASS

- T1 is a pure structural refactor (zero behavior change). No new [Fact] tests are authored in T1. This is an explicit design decision stated in the ticket.
- Regression coverage is provided by T_B137_05 (authored in T2): confirms cancel+resubmit still fires on a real price change after T1 extraction. The regression path exercises Phase C execution.
- No new public or internal method requires a dedicated test that is absent: ExecutePhaseCStopReplacement is a private void method with zero new logic; the extraction delegates to three existing methods that are already covered by the B136 test suite. The T_B137_05 regression is sufficient.

### Scan Checklist

PASS

All 7 scans present:
- SCAN-01: `grep -r "lock(" src/ --include="*.cs"` → 0 matches ✅
- SCAN-02: `grep -rn "async void " src/ --include="*.cs"` → 0 matches ✅
- SCAN-03: `git diff HEAD src/PropTraderTools/CopyEngine.cs | grep "^+" | grep "return null;"` → 0 matches (git-diff scoped; pre-existing L2629 return null excluded) ✅
- SCAN-04: `dotnet build` → 0 errors 0 warnings ✅
- SCAN-05: `python scripts/complexity_audit.py` → SyncAtmFollowerTarget=7, ExecutePhaseCStopReplacement=2 ✅
- SCAN-06: `dotnet test` → 0 Failed 0 Errors ✅
- SCAN-07: `dotnet csharpier check src/` → clean ✅

### File Routing

PASS

- Source: `src/PropTraderTools/CopyEngine.cs` — Wave workspace. ✅
- No Director workspace paths for .cs files.

### VERDICT: TICKET_REVIEW_PASS

---

## T2 — IsNoPriceChange Guard (DW-B147 + DW-B149)

**Ticket**: Add IsNoPriceChange early-return guard to SyncAtmFollowerTarget and SyncAtmFollowerBracket

### Traceability

PASS

- T2 → DW-B147 (ARM event spurious cancel+resubmit). Spec reference: specs/002-trade-copier-spec.html §DW-B147 (L40557). Present in ticket.
- T2 → DW-B149 (ChangeSubmitted race second TP3-HBC). Spec reference: §B136 DW-B149 (L40683). Present in ticket.
- Both DW items appear in the architecture plan "Deferred Items Closed" section. Traceability complete.
- No phantom work detected.

### JS Pre-Check

PASS

- No `lock()` described. JS-021: `IsNoPriceChange` is static, no shared state. ✅
- No `async void` described. JS-033 satisfied. ✅
- No `throw new Exception`. JS-001 satisfied. ✅
- `IsNoPriceChange` returns bool (not null). `IsNoPriceChangeTestable` returns bool. JS-002 satisfied. ✅
- No Dictionary for shared state. JS-009 N/A. ✅
- `IsNoPriceChange => currentPrice == newPrice` — stack-only, zero allocation. JS-036 satisfied. ✅
- `string.IsNullOrEmpty` is BCL intrinsic — no allocation. ✅
- Guards return void (early exit from method, not return null). ✅

### CYC Pre-Check

PASS

- Prerequisite check: T2 requires SyncAtmFollowerTarget=7 (from T1). Correctly gated at Step 1.
- Adding `if (IsNoPriceChange(fo.LimitPrice, newPrice)) return;` to SyncAtmFollowerTarget: +1 branch. 7+1=8. AT LIMIT. ✅
- Adding `if (IsNoPriceChange(fo.StopPrice, newPrice)) return;` to SyncAtmFollowerBracket: +1 branch. 4+1=5. ✅
- `IsNoPriceChange`: expression method body `=> currentPrice == newPrice;` — base=1, no branches. CYC=1. ✅
- `IsNoPriceChangeTestable`: pure delegation (`=> IsNoPriceChange(currentPrice, newPrice)`). CYC=1. ✅
- All final CYC values after T2: SyncAtmFollowerTarget=8 (AT LIMIT — must not exceed), SyncAtmFollowerBracket=5, IsNoPriceChange=1. All <= 8. ✅
- SyncAtmFollowerTarget AT LIMIT is correctly documented with "AT LIMIT" warning.

### NT8 Check

PASS

- `fo.LimitPrice` and `fo.StopPrice` — existing Order property access. No new NT8 API. ✅
- No StrategyBase-only API. ✅
- No `Account.All` outside Loaded handler. No `sealed` on TradeCopierWindow. No `FontFamily`. ✅
- No hardcoded hex color. No `DateTime.Now`. ✅

### Test Coverage

PASS

- T_B137_01: `IsNoPriceChangeTestable` returns true when same price. [Fact], xUnit. Concrete body with direct static call. ✅
- T_B137_02: `IsNoPriceChangeTestable` returns false when different prices. [Fact], xUnit. Concrete body. ✅
- T_B137_03: SyncAtmFollowerTarget guard — no cancel when `fo.LimitPrice == newPrice`. [Fact], xUnit. Stub pattern described (B136Tests). Assertion: `acc.Cancel NOT called`. ✅
- T_B137_04: SyncAtmFollowerBracket guard — no cancel when `fo.StopPrice == newPrice`. [Fact], xUnit. Assertion: `acc.Cancel NOT called`. ✅
- T_B137_05: Both sync methods — cancel fires on real price change. [Fact], xUnit. Regression guard. Assertion: `acc.Cancel WAS called`. ✅
- T_B137_06: `OrderPassesBracketGateTestable` with `signalName=""` → ATM path → true. [Fact], xUnit. Authored in T2 test file; runs after T3. `[Skip("DW-B150: passes after T3")]` guidance explicitly provided. ✅
- T_B137_07 through T_B137_09: Authored in T2 file; exercised in T4/T3 respectively. ✅
- Framework: xUnit [Fact] throughout. No NUnit or MSTest. ✅
- Tests T_B137_01/02 will FAIL pre-T2 (method doesn't exist) and PASS after T2. All tests logically sound. ✅

**Note (non-blocking)**: Test bodies for T_B137_03/04/05/07/08 are stub-sketch format ("Use Account/Order stub pattern from B136Tests.cs"). The method names, assertion goals, and stub strategy are all explicitly defined. Per the role definition requirement, "[Fact] test method names and what they assert" are present for all 9 tests. Implementation follows established B136Tests pattern.

### Scan Checklist

PASS

All 7 scans present:
- SCAN-01: `grep -r "lock(" src/ --include="*.cs"` → 0 matches ✅
- SCAN-02: `grep -rn "async void " src/ --include="*.cs"` → 0 matches ✅
- SCAN-03: `git diff HEAD src/PropTraderTools/CopyEngine.cs | grep "^+" | grep "return null;"` → 0 matches (git-diff scoped) ✅
- SCAN-04: `dotnet build` → 0 errors 0 warnings ✅
- SCAN-05: `python scripts/complexity_audit.py` → SyncAtmFollowerTarget=8 (AT LIMIT), SyncAtmFollowerBracket=5, IsNoPriceChange=1, ExecutePhaseCStopReplacement=2 ✅
- SCAN-06: `dotnet test` → 0 Failed 0 Errors (T_B137_06 with [Skip] guidance explicitly documented) ✅
- SCAN-07: `dotnet csharpier check src/` → clean ✅

### File Routing

PASS

- Source: `src/PropTraderTools/CopyEngine.cs` — Wave workspace. ✅
- Test: `tests/PropTraderTools.Tests/CopyEngineB137Tests.cs` — Wave workspace. ✅

### VERDICT: TICKET_REVIEW_PASS

---

## T3 — OrderPassesBracketGate Empty-String Condition Fix (DW-B150)

**Ticket**: Fix OrderPassesBracketGate branch (1) to treat empty signalName as ATM path

### Traceability

PASS

- T3 → DW-B150 (NEW P1 — OrderPassesBracketGate empty-string signalName takes signal path, fo=NULL on stop drag when no PTT-STP-Drag yet). Spec references: specs/002-trade-copier-spec.html §section-b135, §section-b136. Present in ticket.
- DW-B150 appears in architecture plan "New Defects Addressed by This Block" section. Root cause fully confirmed in plan (11-step deterministic trace verified in plan review).
- Source-verified at L2677: `if (signalName != null)` — matches ticket description exactly.
- No phantom work. No missing plan item.

### JS Pre-Check

PASS

- No `lock()`. JS-021 satisfied (static method, no shared state). ✅
- No `async void`. JS-033 satisfied. ✅
- No `throw new Exception`. JS-001 satisfied. ✅
- No `return null` added. JS-002 satisfied (`OrderPassesBracketGate` returns bool). ✅
- `string.IsNullOrEmpty` — BCL static method, no allocation, no throw. JS-036 satisfied. ✅
- ASCII-only: `string.IsNullOrEmpty`, comment text all ASCII. ✅

### CYC Pre-Check

PASS

- T3 is independent; no prerequisite CYC dependency.
- Source-verified: OrderPassesBracketGate CYC=2 at L2668: "CYC=2: base(1) + if(signalName != null)(1) = 2."
- Change is a condition expression replacement (`signalName != null` → `!string.IsNullOrEmpty(signalName)`). Branch COUNT is unchanged — same single `if` branch, different predicate expression. McCabe counts branches, not sub-expressions of a single condition.
- CYC after T3: OrderPassesBracketGate=2 (UNCHANGED). ✅
- MatchesLeaderName: NOT modified. CYC=5 (source-verified at L2640). ✅
- All final CYC values after T3: unchanged from T2 state. All <= 8. ✅

### NT8 Check

PASS

- No new NT8 API calls. Change is a BCL `string.IsNullOrEmpty` call. ✅
- No StrategyBase-only API. ✅
- No `Account.All` outside Loaded handler. No `sealed` on TradeCopierWindow. No `FontFamily`. ✅
- No hardcoded hex color. No `DateTime.Now`. ✅
- `OrderPassesBracketGate` is a static predicate — no NT8 API involved. ✅

### Test Coverage

PASS

- T_B137_06: `OrderPassesBracketGateTestable(order, signalName: "", leaderName: "Stop3", isStop: true)` → Assert.True. [Fact], xUnit. Validates DW-B150 fix directly. Will FAIL pre-T3 (`"" != null` → signal path → `null == ""` = false). Will PASS after T3. ✅
- T_B137_09: `OrderPassesBracketGateTestable(order, signalName: null, ...)` → Assert.True. [Fact], xUnit. Regression guard — null signalName ATM path unchanged. ✅
- Both tests authored in T2 file; T3 Step 7 removes [Skip] from T_B137_06. ✅
- Framework: xUnit [Fact]. No NUnit or MSTest. ✅
- Tests are logically sound, non-tautological. ✅

### Scan Checklist

PASS

All 7 scans present:
- SCAN-01: `grep -r "lock(" src/ --include="*.cs"` → 0 matches ✅
- SCAN-02: `grep -rn "async void " src/ --include="*.cs"` → 0 matches ✅
- SCAN-03: `git diff HEAD src/PropTraderTools/CopyEngine.cs | grep "^+" | grep "return null;"` → 0 matches (git-diff scoped) ✅
- SCAN-04: `dotnet build` → 0 errors 0 warnings ✅
- SCAN-05: `python scripts/complexity_audit.py` → OrderPassesBracketGate=2 (UNCHANGED), MatchesLeaderName=5 (UNCHANGED); all prior method CYC values stable ✅
- SCAN-06: `dotnet test` → 0 Failed 0 Errors (T_B137_06 must PASS — [Skip] removed) ✅
- SCAN-07: `dotnet csharpier check src/` → clean ✅

### File Routing

PASS

- Source: `src/PropTraderTools/CopyEngine.cs` — Wave workspace. ✅
- Test file already created in T2: `tests/PropTraderTools.Tests/CopyEngineB137Tests.cs`. ✅

### VERDICT: TICKET_REVIEW_PASS

---

## T4 — CancelExistingPttStpDrag Block A-Prime Extraction for SyncAtmFollowerBracket (DW-B151)

**Ticket**: Add CancelExistingPttStpDrag extracted helper and call it from SyncAtmFollowerBracket

### Traceability

PASS

- T4 → DW-B151 (NEW P1 — SyncAtmFollowerBracket missing Block A-Prime pre-sweep, PTT-STP-Drag accumulates on repeated stop drags). Spec references: specs/002-trade-copier-spec.html §section-dw-b137, §section-b136. Present in ticket.
- DW-B151 appears in architecture plan "New Defects Addressed by This Block" section.
- Extraction-first design is the primary approach (V4 violation resolved in plan review — extraction promoted as mandatory).
- No phantom work. No missing plan item.

### JS Pre-Check

PASS

- No `lock()` described. JS-021: `CancelExistingPttStpDrag` uses `acc.Orders.ToList()` snapshot (established lock-free pattern from L2382). ✅
- No `async void` described. JS-033 satisfied. ✅
- No `throw new Exception` in hot path. JS-001 satisfied: `catch (Exception ex) { StatusUpdate?.Invoke(...); }` — no rethrow. ✅
- No `return null` added. JS-002 satisfied (void return method). ✅
- No Dictionary for shared state. JS-009 N/A. ✅
- `acc.Orders.ToList()` is established lock-free pattern (L2382). ✅
- `CancelExistingPttStpDrag` identifiers and string literals are ASCII: "PTT-STP-Drag", "STP pre-cancel error". ✅
- Order name "PTT-STP-Drag" correctly starts with "PTT-". ✅

### CYC Pre-Check

PASS

- Prerequisite check: T4 requires SyncAtmFollowerBracket=5 (from T2). Correctly gated at Step 1.
- `CancelExistingPttStpDrag(acc, fo)` call in SyncAtmFollowerBracket: single method call, 0 McCabe branches. SyncAtmFollowerBracket CYC=5+0=6. ✅
- `CancelExistingPttStpDrag` branches: base(1) + foreach(1) + if-cond(1) + `||`(1) + `&&Name`(1) + `&&Instrument`(1) + `?.`(1) = CYC=7 (strict worst-case). Loose count: CYC=6. Both bounds <= 8. ✅
- SCAN-05 target: CancelExistingPttStpDrag <= 8. Ticket states "expect 6 or 7." Both are within limit. ✅
- SyncAtmFollowerTarget=8 (AT LIMIT, unchanged after T4 — verify no regression). ✅
- All final CYC values after T4: SyncAtmFollowerBracket=6, CancelExistingPttStpDrag=6-7. All <= 8. ✅

**Note (non-blocking)**: T4 Step 4 CYC comment update describes "(4) Block A catch, (5) Block B catch, (6) newStop null guard" as McCabe branches, while the pre-existing source comment at L2301 states "Two independent try/catch blocks — exception handlers add 0 McCabe branches each." This introduces an inconsistency in the comment narrative (catches are listed as branches in the T4 update but not in the source convention). However, the final CYC value of 6 is derived from the numeric addition (5+0=6), not from the comment listing. The discrepancy affects the comment documentation only, not the CYC enforcement. The engineer should reconcile this to use the established source convention (catches = 0 branches per L2301/L2302) — but this is a documentation note, not a code correctness issue. CYC=6 final value is correct regardless.

### NT8 Check

PASS

- `acc.Cancel(new Order[] { o })` — AddOnBase-available. Mirrors established pattern at L2390. ✅
- `acc.Orders.ToList()` — Thread-safe snapshot. Established pattern at L2382. ✅
- `o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted` — valid NT8 OrderState values. ✅
- `o.Instrument?.FullName` — null-conditional access. Mirrors L2386. ✅
- No `AtmStrategyCreate` or `AtmStrategyChangeStopTarget`. ✅
- No `Account.All` outside Loaded handler. No `sealed` on TradeCopierWindow. No `FontFamily`. ✅
- No hardcoded hex color. No `DateTime.Now`. ✅

### Test Coverage

PASS

- T_B137_07: `CancelExistingPttStpDrag` cancels a Working PTT-STP-Drag. [Fact], xUnit. Assertion: `acc.Cancel called with Working order`. DW-B151 Working-state coverage. ✅
- T_B137_08: `CancelExistingPttStpDrag` cancels an Accepted PTT-STP-Drag. [Fact], xUnit. Assertion: `acc.Cancel called with Accepted order`. DW-B151 Accepted-state extension coverage. ✅
- Both tests authored in T2 file; T4 SCAN-06 verifies they pass. ✅
- `CancelExistingPttStpDragTestable` internal seam described in "Tests Assigned" section with exact signature: `internal void CancelExistingPttStpDragTestable(Account acc, Order fo) => CancelExistingPttStpDrag(acc, fo);` ✅
- Framework: xUnit [Fact]. No NUnit or MSTest. ✅
- Tests are logically sound, non-tautological. ✅

### Scan Checklist

PASS

All 7 scans present:
- SCAN-01: `grep -r "lock(" src/ --include="*.cs"` → 0 matches ✅
- SCAN-02: `grep -rn "async void " src/ --include="*.cs"` → 0 matches ✅
- SCAN-03: `git diff HEAD src/PropTraderTools/CopyEngine.cs | grep "^+" | grep "return null;"` → 0 matches (git-diff scoped; pre-existing L2629 acknowledged) ✅
- SCAN-04: `dotnet build` → 0 errors 0 warnings ✅
- SCAN-05: `python scripts/complexity_audit.py` → SyncAtmFollowerBracket=6, CancelExistingPttStpDrag<=8 (expect 6 or 7), SyncAtmFollowerTarget=8 (AT LIMIT, regression check) ✅
- SCAN-06: `dotnet test` → 0 Failed 0 Errors (T_B137_07 and T_B137_08 must PASS) ✅
- SCAN-07: `dotnet csharpier check src/` → clean ✅

### File Routing

PASS

- Source: `src/PropTraderTools/CopyEngine.cs` — Wave workspace. ✅
- Test file: `tests/PropTraderTools.Tests/CopyEngineB137Tests.cs` (created in T2). ✅

### VERDICT: TICKET_REVIEW_PASS

---

## Non-Blocking Notes (Engineer Awareness)

These items do not trigger TICKET_REVIEW_FAIL. They are flagged for engineer awareness only.

**NOTE-1 (T4 Step 4 — CYC comment inconsistency)**: The T4 Step 4 CYC comment update lists "(4) Block A catch, (5) Block B catch" as McCabe branches, inconsistent with the pre-existing source convention at L2301-L2302 where catches contribute 0 McCabe branches. The final CYC=6 value is numerically correct (5+0=6). The engineer should update the comment to follow the established source convention to avoid confusion for future reviewers. Suggested update: list branches as "(1) acc null, (2) fo null, (3) IsNoPriceChange guard [T2], (4) newStop null" and note "Two independent try/catch blocks — exception handlers add 0 McCabe branches each (per codebase convention L2301)."

**NOTE-2 (T4 testable seam)**: `CancelExistingPttStpDragTestable` is described as "recommended" in the Tests Assigned section rather than as an explicit numbered step in the Step-by-Step Instructions. The seam signature is fully defined. Recommend the engineer treat this as a mandatory step since T_B137_07/T_B137_08 require it.

---

## Spec Coverage Matrix — Aggregate

| Requirement | Ticket | Status |
|-------------|--------|--------|
| DW-B147 (ARM event spurious cancel+resubmit) | T2 | ✅ Covered |
| DW-B149 (ChangeSubmitted race second TP3-HBC) | T2 | ✅ Covered |
| DW-B150 (OrderPassesBracketGate empty-string fo=NULL) | T3 | ✅ Covered |
| DW-B151 (SyncAtmFollowerBracket missing Block A-Prime) | T4 | ✅ Covered |
| T1 structural prerequisite (CYC headroom for T2) | T1 | ✅ Covered |
| CYC <= 8 all methods | All | ✅ Covered — worst case SyncAtmFollowerTarget=8 |
| xUnit tests >= 9 | T2 file | ✅ 9 [Fact] tests T_B137_01..T_B137_09 |
| lock-free (JS-021) | All | ✅ No lock() in any ticket |
| NT8 AddOnBase API only | T4 | ✅ acc.Cancel, acc.Orders, acc.CreateOrder, acc.Submit |
| PTT- prefix on order names | T4 | ✅ "PTT-STP-Drag" |
| 7-scan checklist per ticket | All | ✅ SCAN-01..SCAN-07 in T1, T2, T3, T4 |
| SCAN-03 git-diff scoped | All | ✅ git diff HEAD scoping in all 4 tickets |
| Sequential dependency T1→T2 | T2 gate | ✅ "Do NOT start T2 until T1 SCAN-05 confirms CYC=7" |
| Sequential dependency T2→T4 | T4 gate | ✅ "Do NOT start T4 until T2 SCAN-05 confirms CYC=5" |
| T3 independent | T3 | ✅ Stated as independent; no CYC dependency |
| Zero behavior change T1 | T1 | ✅ 5-step verification + Step 5 explicit constraint |
| File scope: CopyEngine.cs only | All | ✅ Stated in every ticket |

No uncovered requirements. No duplicate coverage.

---

## Violation Summary

| ID | Severity | Check | Location | Description | Status |
|----|----------|-------|----------|-------------|--------|
| — | — | — | — | No violations found. | — |

---

## Overall

TICKET_REVIEW_PASS
