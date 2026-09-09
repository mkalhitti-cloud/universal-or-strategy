# PTT-REPAIRS-06-TEST-COMPILE — Ticket Review

**Epic:** PTT-REPAIRS-06-TEST-COMPILE  
**Phase:** 3.5 — Ticket Review (adversarial)  
**Reviewer:** PTT Ticket Reviewer  
**Input:** `docs/brain/PTT-REPAIRS-06-TEST-COMPILE/04-tickets.md`  
**Reference reads:** `docs/brain/PTT-REPAIRS-06-TEST-COMPILE/02-architecture-plan.md`, `docs/brain/PTT-REPAIRS-06-TEST-COMPILE/02-plan-review.md`, `src/PropTraderTools/CopyEngine.cs:2320-2335`

---

## TCHECK-1: F1 alias placement — PASS

Ticket F1 instructs: "INSERT after line 12 (the opening brace `{` of `namespace PropTraderTools`)."
Line 12 is confirmed as the opening brace of the namespace block. The alias
`using CopyRule = PropTraderTools.CopyEngine.CopyRule;` is therefore placed **inside**
the namespace block, not at file level (lines 1–11).

Namespace-scoped type-alias `using` directives are valid C# from C# 2.0 onward.
The project targets net48 / LangVersion 9.0; this placement is correct and will compile.
No file-level alias (which would require C# 10+ file-scoped namespaces) is used.

---

## TCHECK-2: F7 updated assertions match CopyEngine.cs logic — PASS

`CopyEngine.cs:2324-2332` independently verified (source read performed this session):

```csharp
internal static bool IsDispatchTriggerState(OrderState state, OrderType type) =>
    (type == OrderType.Market && state == OrderState.Submitted)
    || (
        type == OrderType.Limit
        && (
            state == OrderState.Accepted
            || state == OrderState.Working
        )
    );
```

Ticket assertion mapping checked against production logic:

| Assert | State | Type | Expected by source | Ticket says | Verdict |
|--------|-------|------|--------------------|-------------|---------|
| True | Submitted | Market | TRUE | True | PASS |
| True | Accepted | Limit | TRUE | True | PASS |
| False | Initialized | Limit | FALSE | False | PASS |
| True | Working | Limit | TRUE (DW-B96 ChartTrader) | True | PASS |
| False | Filled | Limit | FALSE | False | PASS |
| False | Cancelled | Limit | FALSE | False | PASS |

Critical correction present: Working flipped from `Assert.False` (wrong) to `Assert.True`
(correct per DW-B96 ChartTrader path). All 6 assertion values are correct.

---

## TCHECK-3: Skip attribute placement (F4, F5, F6) — PASS

- **F4:** "REPLACE `[Fact]` with `[Fact(Skip = "...")]` at each of the 8 lines listed." Operation
  is a line-level replacement of the attribute token. No insertion inside a method body.
- **F5:** "REPLACE `[Fact]` at line 440 with Skip attribute." Same pattern — attribute line
  replaced in place. No second `[Fact]` line added; no injection into method body.
- **F6:** "REPLACE `[Fact]` with `[Fact(Skip = "...")]` at each of the 8 lines listed."
  Same pattern.

All three fix groups replace the `[Fact]` attribute line in-place. No body injection.

---

## TCHECK-4: No production files in scope — PASS

SCOPE LOCK section present verbatim:
> "SCOPE LOCK: This ticket covers ONLY src/PropTraderTools/CopyEngineTests.cs.
> No changes to CopyEngine.cs or TradeCopierPanel.cs. Any edit outside
> CopyEngineTests.cs is a protocol violation."

`CopyEngine.cs` appears only in the METHOD SIGNATURES reference table (read-only context)
and in F9/F10 code snippets as the `typeof(CopyEngine)` target (not an edit target).
`TradeCopierPanel.cs` does not appear anywhere in the ticket. No production file is listed
as a change target.

---

## TCHECK-5: All 10 fixes present — PASS

| Fix | Line numbers exact? | Old/New text exact? | Verdict |
|-----|---------------------|---------------------|---------|
| F1 | Insert after line 12 | Exact alias line provided | PASS |
| F2 | Insert after line 9 | Exact `using System.Collections.Generic;` line | PASS |
| F3 | Insert after F2 line | Exact `using System.Linq;` line | PASS |
| F4 | 486, 519, 557, 684, 728, 763, 879, 928 | Old `[Fact]` / new Skip attribute text stated | PASS |
| F5 | Line 440 | Old `[Fact]` / new Skip attribute text stated | PASS |
| F6 | 2640, 2775, 2791, 2807, 2850, 2876, 2904, 3798 | Old `[Fact]` / new Skip attribute text stated | PASS |
| F7 | Line 2987 (rename) + 6 Assert blocks at 2990–3017 | All 7 old→new code blocks provided verbatim | PASS |
| F8 | Lines 4036, 4137 | `new CopyEngine()` → `CopyEngine.Instance` explicit | PASS |
| F9 | Insert after line 5782 | Exact GetMethod + GetField code block provided | PASS |
| F10 | Insert after line 7210 | Exact `_engine` field + GetMethod + GetField block | PASS |

No fix is missing. No vague/placeholder language found in any fix description.

---

## TCHECK-6: JS rule constraints section present — PASS

JS RULE CONSTRAINTS table in ticket:

| Rule | Present? |
|------|----------|
| JS-021 (no lock()) | Yes |
| JS-042 (ASCII-only strings) | Yes |
| JS-013 (CYC <= 3) | Yes |
| JS-010 (use Instance, not new) | Yes |

All four required JS rules are present with constraint description and applicability scope.

---

## TCHECK-7: 7-scan checklist present and complete — PASS

All 7 scans present with exact PowerShell commands and required results:

| Scan | Command present? | Required result stated? |
|------|-----------------|------------------------|
| SCAN-1: lock() check | Yes | "0 matches" |
| SCAN-2: Non-ASCII check | Yes | "0 matches" |
| SCAN-3: Build error check | Yes | "0 matches" |
| SCAN-4: Build summary | Yes | "0 Error(s)" |
| SCAN-5: Test run | Yes | T_CLONE_02/03/04/T_B66OBJ_02/T_CLONE_XISO_01 PASS; min 17 skips; full counts recorded |
| SCAN-6: Hard-link sync | Yes | "SYNC COMPLETE" |
| SCAN-7: Hard-link count | Yes | "1" (test file not hard-linked to NT8) |

---

## TCHECK-8: Acceptance criteria measurable — PASS

Acceptance criteria section contains:

1. `dotnet build ... = 0 Error(s)` — binary, measurable ✓
2. `dotnet test exits with 0 genuine regressions` with NT8-runtime failures explicitly
   classified as expected — measurable, non-vague ✓
3. `T_CLONE_02`, `T_CLONE_03`, `T_CLONE_04`, `T_B66OBJ_02`, `T_CLONE_XISO_01`
   listed as explicit PASS requirement ✓
4. `IsDispatchTriggerState_CorrectStates` explicit PASS requirement ✓
5. SCAN-1 = 0 matches (measurable) ✓
6. SCAN-2 = 0 matches (measurable) ✓
7. SCAN-6 = "SYNC COMPLETE" (measurable) ✓
8. All 7 scans reported in completion artifact ✓

XUNIT names table also appears in XUNIT [Fact] NAMES section with explicit PASS/SKIP
expectations per test. No vague criterion found.

---

## TCHECK-9: Completion artifact specified — PASS

COMPLETION ARTIFACT section present, specifying:

- File path: `docs/brain/PTT-REPAIRS-06-TEST-COMPILE/ticket-1-completion.md` ✓
- `BUILD_PASS` or `BUILD_FAIL` declaration at the top ✓
- Actual output of all 7 scans (copy-pasted terminal output, not paraphrased) ✓
- SCAN-5: full pass/fail/skip counts and list of any failing test names ✓
- Classification of every non-PASS result as `expected` or `genuine regression`
  with clear definitions for both categories ✓

NT8-runtime failure classification is explicitly required. Completion artifact
specification is complete.

---

## TCHECK-10: Application order safety — PASS

IMPLEMENTATION INSTRUCTIONS section explicitly states:

> "Because fixes F1, F2, F3 insert lines near the top of the file, line numbers for
> all subsequent fixes reference the **original** line numbers before any insertion.
> The engineer MUST resolve the line-number shift manually after each insertion OR
> apply all insertions in reverse-line-number order so that earlier insertions do not
> invalidate later line references. The safest approach is to apply fixes in
> **descending line number order** (F10 first, F9, F8 ... F1 last)."

The ticket correctly identifies the top-insertion shift risk and mandates descending-order
application as the resolution. The engineer is not told to apply F1 first. No line-shift
ambiguity is left to the engineer to discover independently.

---

## VERDICT: TICKET_REVIEW_PASS

All 10 checks pass with no violations.

Ticket is approved. Engineer may proceed to Phase 4a.

*Reviewed against: `04-tickets.md`, `02-architecture-plan.md`, `02-plan-review.md`,*  
*`src/PropTraderTools/CopyEngine.cs:2320-2335` (independent source read).*
