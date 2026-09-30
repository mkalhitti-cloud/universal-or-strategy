# B52-LaneA Ticket Review
**Block**: B52-LaneA | `test-restore-extraction`
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-08-08
**Input**: docs/brain/B52-LaneA/04-tickets.md (from PLAN_REVIEW_PASS plan)

---

## Overall Status: TICKET_REVIEW_PASS

---

## Ticket T-B52-01 — DW-B50C-01: Restore FindFollowerBracketOrder Test

### Review Table

| # | Check | Result | Notes |
|---|-------|--------|-------|
| 1 | Restored assertion specificity | PASS | `Assert.Null(result)` present after `method.Invoke(...)`. Assertion fails if SUT returns non-null. `TargetInvocationException` guard correctly swallows only `NullReferenceException` inner; all other inner exceptions are re-thrown (`throw;`). Exact old code (lines 428–440) and exact new code both specified. |
| 2 | Scope creep (V12.23) | PASS | T-B52-01 touches only `CopyEngineTests.cs`. No production `.cs` file is modified. |
| 6 | 7-scan checklist complete (SCAN-01 through SCAN-07) | PASS | All 7 scans present. SCAN-01 (lock), SCAN-02 (async void), SCAN-03 (return null), SCAN-04 (CYC of test method ≤ 8, Lizard=2 documented), SCAN-05 (build), SCAN-06 (N/A with explanation), SCAN-07 (verify_links). No scan absent. |
| 7 | NT8 constraints: no banned patterns in new test code | PASS | No `{ get; init; }`, no `abstract/sealed record`, no `volatile`, no `ImmutableDictionary`. |
| 8 | JS-002 compliance | PASS | `Assert.Null(result)` is test-assertion code, NOT a `return null` in production. Ticket explicitly documents the exemption. `object result = null;` is a local variable initializer, not a `return null` statement. Exemption correctly stated. |

### Traceability
- T-B52-01 maps to **DW-B50C-01** (B50-LaneC deferred) and architecture plan Section 2. No phantom work. No plan item missed.

### JS Pre-Check
- JS-021: No `lock(` in test code ✅
- JS-002: No `return null;` statement — `object result = null` is initialization, not a return ✅
- JS-033: `public void [Fact]` — not `async void` ✅

### CYC Pre-Check
- `FindFollowerBracketOrder_ReturnsNullWhenNoMatch`: 2 decisions (try/catch + NRE if-check) → McCabe=3, Lizard=2. Well within ≤ 8 threshold ✅

### NT8 Check
- No NT8-P0 banned pattern introduced ✅
- .NET 4.8 compatible (no NullabilityInfoContext, no C# 9+ features) ✅

### Test Coverage
- Ticket is itself a test restoration. The `[Fact]` name, assertions (2), and behavior are fully specified. ✅

### Scan Checklist
- SCAN-01 through SCAN-07 all present ✅

### File Routing
- `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` — Wave workspace ✅

### VERDICT: TICKET_REVIEW_PASS

---

## Ticket T-B52-02 — DW-B51-03: Extract OnFollowerAtmTemplateComboLoaded Helpers

### Review Table

| # | Check | Result | Notes |
|---|-------|--------|-------|
| 3 | All 11 branches accounted for | PASS | Parent retains branches 1–4 (null guard, idempotency, !Contains, Clone mode). `PopulateAtmComboItems` absorbs branches 5–8 (dir-exists, foreach, leader-match, catch). `ApplyAtmAutoSelect` absorbs branches 9–11 (defaultIdx>0, !IsNullOrEmpty, item!=null). Total: 4+4+3=11. No branch dropped, no duplicate. |
| 4 | CYC targets achievable | PASS | `PopulateAtmComboItems`: 4 decisions → Lizard=4 ≤ 5. `ApplyAtmAutoSelect`: 3 decisions → Lizard=3 ≤ 4. Parent: 4 decisions → Lizard=4 ≤ 5. All three methods comfortably within ≤ 8 project threshold. |
| 5 | 7-scan checklist complete (SCAN-01 through SCAN-07) | PASS | All 7 scans present. SCAN-06 explicitly specifies CYC for all three methods: `parent=4, PopulateAtmComboItems=4, ApplyAtmAutoSelect=3`. SCAN-07 specifies `verify_links.ps1` with `DESYNC=0 MISSING=0`. SCAN-04 marked N/A (no test method, justified). |
| 7 | NT8 constraints: no banned patterns in new methods | PASS | No `{ get; init; }`, no `abstract/sealed record`, no `volatile`, no `ImmutableDictionary`. `out int` parameter: standard C# since v1.0, available in .NET 4.8. Inline `out int defaultIdx` declaration at call site: C# 7.0 feature, supported by VS2017+ Roslyn on .NET 4.8 — does NOT require IsExternalInit. ✅ |
| 8 | JS-002 compliance — new methods return void | PASS | Both `PopulateAtmComboItems` and `ApplyAtmAutoSelect` return `void`. No `return null` in either method. Parent `return;` early exits are void returns — not a JS-002 violation. ✅ |
| 9 | Build tag update present | PASS | STEP C in T-B52-02 specifies exact line 41 change: from `"PTT-COPIER B51 \| ui-fixes \| 2026-08-08"` to `"PTT-COPIER B52 \| test-restore-extraction \| 2026-08-08"`. ✅ |
| 10 | No new xUnit tests required | PASS | WPF `RoutedEventHandler` context unavailable in xUnit harness. Rationale documented inline consistent with B51 precedent. SCAN-06 (branch count + CYC verification) explicitly substitutes for test coverage on this refactoring. ✅ |

### Traceability
- T-B52-02 maps to **DW-B51-03** (B51-LaneA deferred) and architecture plan Section 3. No phantom work. No plan item missed.
- Build tag update maps to architecture plan Section 7.

### JS Pre-Check
- JS-021: No `lock(` in any new or modified method ✅
- JS-002: All new methods return `void`; no `return null` introduced ✅
- JS-033: All three methods are `private void` (not `async void`) ✅

### CYC Pre-Check
- Parent `OnFollowerAtmTemplateComboLoaded`: before=12/11, after=5/4 — passes ≤ 8 ✅
- `PopulateAtmComboItems`: 5/4 — passes ≤ 8 ✅
- `ApplyAtmAutoSelect`: 4/3 — passes ≤ 8 ✅
- No method estimated to exceed CYC 8 ✅

### NT8 Check
- No NT8-P0 banned pattern introduced ✅
- `out int` and inline `out int defaultIdx` at call site: C# 7 / .NET 4.8 compatible ✅

### Test Coverage
- `OnFollowerAtmTemplateComboLoaded` is a WPF event handler — no `[Fact]` tests required or expected. SCAN-06 is the contracted substitute. ✅

### Scan Checklist
- SCAN-01 through SCAN-07 all present ✅
- SCAN-06 covers all three methods by name with Lizard scores ✅

### File Routing
- `C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` — Wave workspace ✅
- `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` (tag only) — Wave workspace ✅
- No Director workspace (`..\universal-or-strategy-director`) `.cs` file paths present ✅

### VERDICT: TICKET_REVIEW_PASS

---

## Source Verification

| File | Lines Read | Match? |
|------|-----------|--------|
| `CopyEngineTests.cs` | 428–440 | ✅ Exact match — weakened test (`FindFollowerBracketOrder_NullableReturnType`) confirmed present at those lines. Ticket's replacement block is a valid substitution. |
| `TradeCopierPanel.cs` | 1969–2021 | ✅ Exact match — full 11-branch `OnFollowerAtmTemplateComboLoaded` confirmed at those lines. Plan branch inventory matches source. |

---

## Aggregate Spec Coverage

| Req ID | Ticket | Status |
|--------|--------|--------|
| DW-B50C-01 | T-B52-01 | Covered exactly once ✅ |
| DW-B51-03 | T-B52-02 | Covered exactly once ✅ |

No spec requirement is uncovered. No duplicate coverage.

---

## Overall: TICKET_REVIEW_PASS

All 10 checks pass across both tickets. Zero JS rule violations. Zero NT8 banned patterns. All 7-scan checklists present and complete per ticket. Source code alignment confirmed. File paths route to Wave workspace only.

**Engineer is CLEARED to execute T-B52-01 then T-B52-02.**

Execute in order:
1. T-B52-01 first (P1, `CopyEngineTests.cs` only)
2. T-B52-02 second (P2, `TradeCopierPanel.cs` + `CopyEngine.cs` tag)
3. Run `dotnet build` after each ticket
4. Run `powershell -File scripts\verify_links.ps1 -Fix` once after T-B52-02

---

*Review written by ptt-ticket-reviewer (Phase 3.5). Input: 04-tickets.md (PLAN_REVIEW_PASS). Output: TICKET_REVIEW_PASS.*
