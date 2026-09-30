# B50-LaneC — Ticket Review (Retroactive Phase 3.5)
## Reviewer: ptt-ticket-reviewer
## Date: 2026-08-08 (retroactive — code already implemented)
## Input: docs/brain/B50-LaneC/04-tickets.md
## Against: 02-architecture-plan.md, spec prompt DW-B48-01, RULES_CATALOG.md, NT8_COMPILER_RULES.md

---

## Ticket Under Review: T1 — Fix CopyEngineTests.cs Compilation Errors

---

## Check T1-1 — Spec Requirement Traceability

Each DW-B48-01 closure criterion must map to a scan or change in the ticket.

| Spec criterion | Ticket coverage | SCAN # |
|----------------|----------------|--------|
| `dotnet build` 0 errors | Change Group A + B + C | SCAN-05 |
| Zero CS0246 on CopyRule | Change Group A (`internal`) | SCAN-01 |
| Zero CS0234 ImmutableDictionary | Change Group B | SCAN-02 |
| Zero CS0433 Globals | Stated out-of-scope | SCAN-03 |
| Zero CS0246 DisarmTrailBe | Change Group C (delete tests) | SCAN-04 |
| `dotnet test` green | Implicit after SCAN-05 | SCAN-06 |
| CopyEngineTests.cs at flat root | Stated — no move | Not scanned (assumption) |
| DESYNC=0 MISSING=0 | Stated in SCAN-07 | SCAN-07 |

**Verdict: PASS** — all 8 spec criteria map to a ticket change or scan.

---

## Check T1-2 — 7-Scan Checklist Completeness

Per protocol, every ticket must include a 7-scan checklist with exact commands and expected results.

| Scan | Present in ticket? | Command exact? | Expected result stated? |
|------|-------------------|---------------|------------------------|
| SCAN-01 | YES | YES | YES — "zero matches" |
| SCAN-02 | YES | YES | YES — "zero matches" |
| SCAN-03 | YES | YES | YES — "zero matches" |
| SCAN-04 | YES | YES | YES — "zero matches" |
| SCAN-05 | YES | YES | YES — "0 Error(s)" |
| SCAN-06 | YES | YES | YES — "Failed: 0, Errors: 0" |
| SCAN-07 | YES | YES | YES — "DESYNC=0 MISSING=0" |

**Verdict: PASS** — full 7-scan checklist present with all fields.

---

## Check T1-3 — Method Signatures and Before/After Code

The ticket must provide exact before/after code for every change.

| Change | Before/after present? | Exact? | Assessment |
|--------|----------------------|--------|------------|
| Group A: `private → internal` | YES | YES — single-line before/after | PASS |
| Group B sub-pattern A: empty map | YES — generic pattern shown | YES | PASS |
| Group B sub-pattern B site 1: .SetItem FollowerA | YES — two-line → one-line shown | YES | PASS |
| Group B sub-pattern B site 2: .SetItem FollowerB | YES | YES | PASS |
| Group C: method deletions | YES — full method bodies shown | YES | PASS |

**Verdict: PASS**

---

## Check T1-4 — NT8 Pre-Check (NT8-004, NT8-054)

| NT8 rule | Ticket addresses it? |
|----------|---------------------|
| NT8-004: No ImmutableDictionary | YES — Change Group B is the direct fix |
| NT8-054: CopyEngineTests.cs must stay at flat root | YES — stated in scope, no move permitted |
| NT8-001 through NT8-003: init/record/volatile | N/A — no new code introduces these |

**Verdict: PASS**

---

## Check T1-5 — JS Pre-Check

| JS rule | Ticket pre-checks it? |
|---------|----------------------|
| JS-010: `internal` not `public` | YES — explicitly stated in Group A rules |
| JS-021: No `lock()` | YES — stated in ticket footer |
| JS-002: No `return null` | YES — stated |
| JS-033: No `async void` | Implicit (no new methods) |

**Verdict: PASS**

---

## Check T1-6 — Scope Containment

**CONCERN: Ticket does not cover engineer's actual additional changes.**

The ticket lists Files:
- `CopyEngine.cs`
- `CopyEngineTests.cs`

It does NOT list `PropTraderTools.csproj`. The engineer modified the csproj (removed `NinjaTrader.Client.dll`). This change was:
- Outside the ticket's stated file scope
- Not authorized by the ticket
- Not covered by any scan in the ticket (no scan checks csproj integrity)

The ticket also does not address:
- The `NullabilityInfoContext` replacement (test semantics change)
- The Instrument namespace correction (8 occurrences)
- The `if (ruleValue == null)` → `if (!ruleValue.HasValue)` fix
- The `using CopyRule = PropTraderTools.CopyEngine.CopyRule;` alias

These were all unplanned changes the engineer made. A strict ticket review would have caught that these needed to be either in the ticket scope or deferred. Since this is retroactive, they have been implemented — the question is whether they were correct.

Assessment of each unplanned change:
| Unplanned change | Correct? | Risk |
|-----------------|---------|------|
| Remove `NinjaTrader.Client.dll` from csproj | Appears correct (build passes) | MEDIUM — unreviewed structural change |
| `NullabilityInfoContext` → return type check | Weakens test (see plan review Check 5) | MEDIUM — JS-002 test is now weaker |
| Instrument namespace fix | Correct | LOW |
| `ruleValue == null` → `.HasValue` | Correct for struct | LOW |
| `using CopyRule = ...` alias | Correct alternative to just relying on `internal` | LOW |

**Verdict: CONCERN** — ticket scope was exceeded. Unplanned changes are mostly correct but one (NullabilityInfoContext) weakens a compliance test.

---

## Check T1-7 — xUnit Test Coverage

The ticket correctly states: "No new [Fact] methods are written. All existing [Fact] methods after removal of the two dead methods constitute the test suite."

The ticket does not specify how many tests are expected to exist after the removal. This is a minor gap — it would have been cleaner to state "N tests expected after changes" so the engineer has a verification count.

**Verdict: MINOR GAP** — not blocking.

---

## Check T1-8 — CYC Pre-Check

No new methods are introduced. All changes are:
- One access modifier change
- Find-and-replace of type references
- Method deletions

CYC pre-check: N/A — CYC cannot increase when no new code is written.

**Verdict: PASS**

---

## Summary of Ticket Review Findings

| Check | Verdict | Severity |
|-------|---------|---------|
| T1-1: Spec traceability | PASS | — |
| T1-2: 7-scan checklist | PASS | — |
| T1-3: Before/after code | PASS | — |
| T1-4: NT8 pre-check | PASS | — |
| T1-5: JS pre-check | PASS | — |
| T1-6: Scope containment | CONCERN | MEDIUM |
| T1-7: xUnit test count | MINOR GAP | LOW |
| T1-8: CYC pre-check | PASS | — |

---

## Overall Verdict

**TICKET_REVIEW_PASS_WITH_FINDINGS**

The ticket is structurally complete: full 7-scan checklist, exact before/after code, correct JS/NT8 rule citations, full spec traceability. It would have been approved pre-flight with the note that unplanned changes must not be made during implementation.

The material finding (same as plan review Check 5 and Check 7): the engineer exceeded ticket scope with unplanned changes to `PropTraderTools.csproj` and `NullabilityInfoContext`. These are tracked as DW-B50C-01 and DW-B50C-02 from the plan review.

**The ticket as written was a valid contract. The engineer deviated from it in 5 ways, 1 of which (NullabilityInfoContext) has a quality impact.**
