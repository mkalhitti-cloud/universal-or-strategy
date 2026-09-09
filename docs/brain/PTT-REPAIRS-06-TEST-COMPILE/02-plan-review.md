# PTT-REPAIRS-06-TEST-COMPILE — Plan Review

**Epic:** PTT-REPAIRS-06-TEST-COMPILE  
**Phase:** 2 — Plan Review  
**Reviewer:** PTT Plan Reviewer  
**Input:** `docs/brain/PTT-REPAIRS-06-TEST-COMPILE/02-architecture-plan.md`  
**Source reads performed:** CopyEngine.cs:2320-2335, CopyEngine.cs:459-480, CopyEngine.cs:599-605, CopyEngineTests.cs:440-445, CopyEngineTests.cs:2985-3020

---

## CHECK-1: LANE-SPLIT GATE — PASS

Plan line 8: `## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` — present verbatim.  
All fixes confirmed as targeting a single file (`CopyEngineTests.cs`). Gate condition satisfied.

---

## CHECK-2: F1 alias placement — PASS

Plan places `using CopyRule = PropTraderTools.CopyEngine.CopyRule;` **after line 12** where line 12 is the opening brace `{` of `namespace PropTraderTools`. This puts the alias inside the namespace block, not at file level (lines 1–11). Namespace-scoped type aliases inside a namespace block are valid C# from C# 2.0 onward and are compatible with .NET 4.8 targets. Placement is correct.

---

## CHECK-3: F7 semantics — PASS

Actual `IsDispatchTriggerState` signature read from `CopyEngine.cs:2324`:
```csharp
internal static bool IsDispatchTriggerState(OrderState state, OrderType type) =>
    (type == OrderType.Market && state == OrderState.Submitted)
    || (type == OrderType.Limit && (state == OrderState.Accepted || state == OrderState.Working));
```

Plan assertion values verified against production logic:

| Assert | State | Type passed | Expected | Plan says | Correct? |
|--------|-------|-------------|----------|-----------|----------|
| (a) True | Submitted | Market | TRUE | True | ✓ PASS |
| (b) True | Accepted | Limit | TRUE | True | ✓ PASS |
| (c) False | Initialized | Limit | FALSE | False | ✓ PASS |
| (d) True | Working | Limit | TRUE (DW-B96) | True | ✓ PASS |
| (e) False | Filled | Limit | FALSE | False | ✓ PASS |
| (f) False | Cancelled | Limit | FALSE | False | ✓ PASS |

All 6 old→new call rewrites in the plan match the source at lines 2990–3017 exactly.  
The critical correction — `Working` flipped from `Assert.False` (wrong) to `Assert.True` (correct per DW-B96 ChartTrader path) — is present and semantically verified.

---

## CHECK-4: Skip attribute placement (F4, F5, F6) — PASS

- F4: "replace `[Fact]` with `[Fact(Skip = "...")]` at each of the 8 lines" (486, 519, 557, 684, 728, 763, 879, 928). The `[Fact]` attribute line is replaced in-place; no extra line is inserted inside a method body.
- F5: "replace line 440" — the `[Fact]` attribute line for `FindFollowerBracketOrder_NullableReturnType`. Attribute replaced, not injected into body.
- F6: Same pattern at lines 2640, 2775, 2791, 2807, 2850, 2876, 2904, 3798. Attribute replaced, not injected into body.

All three fix groups correctly replace the `[Fact]` token on its own attribute line.

---

## CHECK-5: No production files in scope — PASS

Plan header: "Output: `src/PropTraderTools/CopyEngineTests.cs` (single file, surgical edits)".  
Component Summary footer: "Total files changed: 1 (`src/PropTraderTools/CopyEngineTests.cs`) — Source code ban respected: No `.cs` production files touched."  
`CopyEngine.cs` and `TradeCopierPanel.cs` appear only in **read verification** context, not as change targets.

---

## CHECK-6: All 10 fixes present — PASS

| Fix | Line numbers stated | Old→New text stated | Verdict |
|-----|--------------------|--------------------|---------|
| F1  | Insert after line 12 | Exact alias line provided | ✓ |
| F2  | Insert after line 9  | Exact using line provided | ✓ |
| F3  | Insert after F2      | Exact using line provided | ✓ |
| F4  | 486, 519, 557, 684, 728, 763, 879, 928 | Old `[Fact]` → new Skip attribute text stated | ✓ |
| F5  | Line 440 | Old `[Fact]` → new Skip attribute text stated | ✓ |
| F6  | 2640, 2775, 2791, 2807, 2850, 2876, 2904, 3798 | Old `[Fact]` → new Skip attribute text stated | ✓ |
| F7  | 2987–3017 (rename + 6 Assert rewrites) | All 6 old→new call blocks provided | ✓ |
| F8  | 4036, 4137 | `new CopyEngine()` → `CopyEngine.Instance` stated | ✓ |
| F9  | Insert after line 5782 | Exact `GetMethod` + `GetField` code block provided | ✓ |
| F10 | Insert after line 7210 | Exact `_engine` field + `GetMethod` + `GetField` code block provided | ✓ |

All 10 fixes are present, each with exact line numbers and exact change text. No fix is vague.

---

## CHECK-7: Acceptance criteria measurable — PASS

Section 2 specifies exact expected build output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```
This is a binary, measurable criterion — not "should work".

Section 3 specifies: record `X passed / Y failed / Z skipped` counts, with a minimum expected skip count of 17 (F4: 8 + F5: 1 + F6: 8). Failure categorization table is concrete. Both criteria are measurable.

---

## CHECK-8: Jane Street compliance — PASS

Section 4 explicitly documents compliance status per rule:

| Rule | Requirement | Plan Status |
|------|-------------|-------------|
| JS-021 | No `lock()` introduced | PASS — no lock() added anywhere |
| JS-042 | All strings/comments ASCII-only | PASS — all skip strings are ASCII |
| JS-013 | CYC of new test methods ≤ 3 | PASS — F9/F10 helpers CYC=1; F7 updated method CYC=1 |
| JS-010 | Private ctor respected | PASS — F8 replaces `new CopyEngine()` with `CopyEngine.Instance` |
| JS-002 | Null contract explicit | PASS — GetMethod/GetField return nullable; callers use Assert.NotNull |

Note: JS-013 is not in the mandatory DNA block (CYC > 8 = FAIL by JS rule set), but the plan correctly uses the applicable rule (CYC ≤ 8 for any method). New helper methods CYC=1 and updated F7 method CYC=1 are well within the JS-XXX CYC limit. No violations.

---

## VERDICT: REVIEW_PASS

All 8 checks pass with no violations.

Plan is approved to proceed to Phase 3 (ticket generation).

---

*Reviewed against: `docs/standards/jane-street/RULES_CATALOG.md` DNA block; `CopyEngine.cs` source reads at lines 2320-2335, 459-480, 599-605; `CopyEngineTests.cs` source reads at lines 440-445, 2985-3020.*
