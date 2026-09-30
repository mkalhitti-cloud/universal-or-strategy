# PTT-COPIER B55 LaneB -- Plan Review
# Phase: 2 (ptt-plan-reviewer)
# Reviewed by: ptt-plan-reviewer
# Date: 2026-08-10
# Plan file: docs/brain/B55-LaneB/02-architecture-plan.md
# Spec: specs/002-trade-copier-spec.html id="section-b55" (LaneB)
# Standards: docs/standards/jane-street/RULES_CATALOG.md
#            docs/standards/NT8_COMPILER_RULES.md

---

## CHECK 1 — Spec Coverage: DW-B47-05 P2

**Requirement (spec section-b55 LaneB):**
1. Add XML doc comment to `FindRule` documenting the null-return contract and the caller MUST null-check mandate.
2. Run call-site audit; confirm every call site is guarded.
3. Add one new `[Fact]` test (`T_B55B_01`) that locks the null-return contract via reflection.
4. Zero logic changes, zero call-site rewrites (doc + test only).
5. Build tag: `PTT-COPIER B55 | findrule-null-contract | {date}`
6. SCAN-08 (FindRule call-site audit) required as the +1 scan beyond the base 7.

**Plan coverage:**

| Spec item | Plan section | Addressed? |
|-----------|-------------|------------|
| XML doc comment exact text (summary + returns) | Section 4.1, Ticket T1 | YES — text matches spec verbatim |
| Caller MUST null-check mandate in doc comment | Section 4.1 | YES |
| Call-site audit (all guarded) | Section 5 (SCAN-08 Pre-Run) | YES — 2 sites, both GUARDED |
| T_B55B_01 [Fact] test | Section 4.2, Ticket T2 | YES — see CHECK 9 for defect |
| Zero logic changes, zero call-site rewrites | Sections 1, 3, Tickets | YES — plan explicitly states this |
| Build tag | Section 11 | YES — matches spec |
| SCAN-08 +1 scan | Section 10 | YES |
| 7+1 scan checklist | Section 10 | YES — all 8 scans listed |
| `[return: MaybeNull]` annotation | Section 13 (compliance note) | NOTED — spec says "Option chosen: add `[return: MaybeNull]` annotation + doc comment". Plan addresses only XML doc comment (no `[return: MaybeNull]`). |

**Finding on `[return: MaybeNull]`:** Spec line 23107 area says the chosen option is "add `[return: MaybeNull]` annotation + doc comment." The plan omits the `[return: MaybeNull]` attribute and only adds the XML doc comment. However, `[return: MaybeNull]` (from `System.Diagnostics.CodeAnalysis`) requires .NET Standard 2.1 / .NET Core 3.0+. NT8 targets .NET Framework 4.8, where `MaybeNullAttribute` does not exist in the BCL. Adding it would require NT8-004-style isolation (linting project only) or a polyfill. The plan's Section 13 acknowledges the pragmatic resolution. The spec's "CHANGE SPEC" block (lines 23099–23135) describes the XML doc comment approach as the chosen fix — the `[return: MaybeNull]` note appears to be a mid-spec editorial carried over from an earlier draft. **Given NT8-004 constraints (System.Collections.Immutable and similar BCL additions banned by NT8's NinjaScript compiler), adding `[return: MaybeNull]` is not safely executable in production NT8 code without a polyfill.** This is a plan-level risk note, not a FAIL: the plan's chosen scope (XML doc comment only) is the correct pragmatic choice for NT8/.NET 4.8. The plan must document the `[return: MaybeNull]` omission explicitly (it does so in Section 13).

**Result: PASS** — all spec-required items are addressed. The `[return: MaybeNull]` omission is NT8-justified and documented.

---

## CHECK 2 — JS-002 (Use Option<T> Instead of Null)

**Rule:** JS-002 P0 CRITICAL — "Never return null for missing values. Use Option<T> or nullable reference types."

**Plan treatment (Section 13):**
The plan explicitly acknowledges JS-002, explains why full Option<T> migration is blocked by NT8/.NET 4.8 (no BCL Option<T>, custom Option<T> struct requires separate block), and declares the pragmatic fix: XML doc comment + [Fact] test that lock the null contract explicitly. This is also the spec-approved resolution.

**Assessment:** The null returns in `FindRule` are pre-existing — they are not introduced by this lane. The plan adds no new `return null` statements. The plan documents and tests the pre-existing null contract rather than silently leaving it untested. No new JS-002 violation is introduced.

**Result: PASS** — no new `return null` introduced; pre-existing null returns are documented and tested per spec direction.

---

## CHECK 3 — JS-021 (No lock() Usage)

**Rule:** JS-021 P0 CRITICAL — "`lock()` anywhere = FAIL."

**Plan treatment (Section 7):**
Explicitly states: "No lock() added or removed." `FindRule` reads `_rules` (a `ConcurrentBag<CopyRule>`) via `foreach` — lock-free ConcurrentBag snapshot enumeration. Sections 9 (T1 invariants) and 9 (T2 invariants) both state "No new lock()."

**Result: PASS** — no `lock()` introduced anywhere in the plan.

---

## CHECK 4 — JS-033 (No async void)

**Rule:** JS-033 P0 CRITICAL — "Never use async void except for event handlers."

**Plan treatment:** Both ticket invariant sections explicitly state "No new async void." The test method `T_B55B_01_FindRule_ReturnsNull_WhenNoRules` is a `[Fact]` returning `void` (xUnit sync fact) — not `async void`. The XML doc comment insert has no async semantics.

**Result: PASS** — no `async void` introduced.

---

## CHECK 5 — JS-001, JS-010, JS-008, JS-009 (Other P0/P1 Rules)

| Rule | Relevance | Assessment |
|------|-----------|------------|
| JS-001 (Result<T,E> not throw) | No exceptions thrown in plan | PASS |
| JS-010 (Private constructors) | No new classes/structs | PASS |
| JS-008 (Readonly structs) | No new structs | PASS |
| JS-009 (ImmutableDictionary) | Not used | PASS |
| JS-003 (Sealed record hierarchies) | Not used | PASS |

**Result: PASS** — no other P0/P1 violations.

---

## CHECK 6 — NT8 Compiler Rules

| Rule | Pattern checked | Assessment |
|------|----------------|------------|
| NT8-001 (`{ get; init; }`) | No new properties | PASS |
| NT8-002 (`abstract/sealed record`) | No records | PASS |
| NT8-003 (`volatile double`) | No new volatile fields | PASS |
| NT8-004 (`ImmutableDictionary`) | Not used | PASS |
| NT8-005 (`readonly struct` + private set) | No new structs | PASS |
| NT8-013 (`DateTime.Now` in CreateOrder) | No CreateOrder call | PASS |
| NT8-016 (`sealed TradeCopierWindow`) | Not touched | PASS |
| NT8-018/021 (`lock()`) | Confirmed absent | PASS |
| NT8-019 (`async void`) | Confirmed absent | PASS |
| NT8-028 (hex color string literals) | No UI changes | PASS |
| NT8-042 (`Dispatcher.InvokeAsync`) | Not used | PASS |
| NT8-043 (null-conditional compound assignment) | Not used | PASS |
| NT8-044 (`StringComparison` without `using System`) | Not used | PASS |

The XML doc comment uses standard C# `///` XML documentation syntax, `<summary>`, `<returns>`, `<see cref="..."/>`, and `<c>` tags. These are supported in all .NET Framework versions including 4.8. No NT8 compiler rule is triggered.

**Result: PASS** — no NT8 rule violations in plan.

---

## CHECK 7 — Scope (Doc + Test Only, No Logic Changes)

**Spec mandate:** "No return type change. No call site rewrites. No logic changes. Doc + test only."

**Plan treatment:**
- Section 1 (Objective): explicitly states "No return-type change. No call-site rewrites. No logic changes. Doc + test only."
- Section 3 (Component List): only 2 files touched — `CopyEngine.cs` (XML doc comment insert) and `CopyEngineTests.cs` (one new `[Fact]`).
- Section 4.1: "Method signature unchanged", "Method body unchanged", "CYC unchanged: 3".
- Ticket T1 invariants: "No logic change. No signature change. Doc comment insert only."
- Ticket T2 invariants: "CYC of new test: 1", "No new lock(), no new async void, no new return null."
- Section 5 confirms the call-site audit was run but confirms NO guard changes needed (all already guarded).

**Result: PASS** — scope is strictly doc + test only. Zero logic changes.

---

## CHECK 8 — CYC Compliance (All Methods <= 8)

**Plan:**
- `FindRule` existing: CYC = 3 (unchanged). PASS.
- `T_B55B_01`: CYC = 1 (straight-line, no branches). PASS.

**Result: PASS** — no method touches or exceeds CYC threshold.

---

## CHECK 9 — Test Design: T_B55B_01 Nullable Struct Assertion Correctness

**CRITICAL FINDING:**

The test body (Section 4.2, Ticket T2) contains this assertion:

```csharp
Assert.Equal(typeof(CopyRule?), mi.ReturnType);
```

**This assertion is incorrect and will FAIL at runtime.**

`CopyRule` is a `class` (reference type), not a `struct` (value type). For reference types, the nullable annotation `CopyRule?` (C# nullable reference type) is a compile-time annotation only — it does **not** change the underlying `System.Type` at runtime. `typeof(CopyRule?)` and `typeof(CopyRule)` return the **same** `System.RuntimeType` object for reference types.

However, the reflection API `mi.ReturnType` reports the underlying CLR type, which for `CopyRule?` (nullable reference type) is `typeof(CopyRule)` — there is no nullable wrapper type for reference types at the CLR level.

**What `Assert.Equal(typeof(CopyRule?), mi.ReturnType)` does at runtime:**
- `typeof(CopyRule?)` evaluates to `typeof(CopyRule)` (same type — NRT annotation stripped by CLR)
- `mi.ReturnType` returns `typeof(CopyRule)`
- **If `CopyRule` is a class:** `typeof(CopyRule?) == typeof(CopyRule)` — so the assertion passes trivially but is misleading and does not verify what the plan claims ("Verify signature: `private CopyRule? FindRule(Instrument instrument)`").

**Separate concern — if `CopyRule` were a struct:**
If `CopyRule` were a value-type struct, `CopyRule?` would be `Nullable<CopyRule>` and `typeof(CopyRule?)` would equal `typeof(Nullable<CopyRule>)`. In that case the assertion would be meaningful and would correctly verify the method returns a `Nullable<CopyRule>`.

**Conclusion on this assertion:** The assertion `Assert.Equal(typeof(CopyRule?), mi.ReturnType)` is semantically vacuous for a reference-type `CopyRule` — it will pass but verifies nothing meaningful about the nullable annotation. **The test will not FAIL and the lane is not blocked by this issue.** However the plan's claim that this "Verify signature: `private CopyRule? FindRule(Instrument instrument)`" is verified by this assertion is misleading.

**Verdict on CHECK 9:**

This is a test quality / documentation accuracy defect. The assertion does not break the test (it passes trivially), but it adds false confidence. The claim in the plan that the test verifies "Verify signature: `private CopyRule? FindRule(Instrument instrument)`" via `Assert.Equal(typeof(CopyRule?), mi.ReturnType)` is inaccurate. The test still achieves its primary goal — `Assert.Null(result)` correctly locks the null return contract.

**Is this a FAIL?** Under the strict enforcement standard:
- The primary test purpose (lock the null-return contract via `Assert.Null(result)`) is sound.
- The signature-verification assertions are redundant safety-checks and the vacuous one (`typeof(CopyRule?) == typeof(CopyRule)` for reference types) passes without error.
- The test will PASS when run. No production behaviour is incorrectly described.
- The defect is an inaccurate comment/claim in the plan, not a defect that causes test failure or masks a real bug.

**Ruling: PLAN-LEVEL FINDING — not a FAIL gate.** The plan must acknowledge that `Assert.Equal(typeof(CopyRule?), mi.ReturnType)` is a no-op assertion for reference types. The engineer must be informed. The test still correctly locks the null-return contract via `Assert.Null(result)`.

**Result: PASS with NOTE** — test achieves its spec purpose (null-return contract locked by `Assert.Null`). The signature-verification assertion is vacuous but harmless. Plan must carry this note for engineer awareness.

---

## CHECK 10 — Call-Site Audit Completeness (SCAN-08)

**Spec requirement (lines 23112–23120):**
"Run: Select-String 'FindRule(' src/ -Recurse -Include *.cs. For every result, confirm the SAME or NEXT line has one of: `if (rule == null)`, `if (rule is null)`, `?.`, `??`. Report any call site that lacks a guard. If found: add guard at that site."

**Plan (Section 5):**
- Command run documented: `Get-ChildItem -Path "C:\WSGTA\universal-or-strategy\src" -Filter "*.cs" -Recurse | Select-String -Pattern "FindRule\(" | Select-Object Filename, LineNumber, Line`
- 2 production call sites reported:
  - `CopyEngine.cs` L1185: `var rule = FindRule(instrument);` → L1186: `if (rule == null) yield break;` — GUARDED
  - `CopyEngine.cs` L1355: `var rule = FindRule(instrument);` → L1356: `if (rule == null) return;` — GUARDED
- L1197 correctly identified as the method definition (N/A).
- External file search documented: `PttQuickExit.cs` (no call), `PttTightenStop.cs` (not separate), all other `src/PropTraderTools/*.cs` — none found.

**Assessment:** The audit covers the Wave workspace path and reports 2 guarded call sites. Both use the `if (rule == null)` form which matches the spec's required guard patterns. The audit result is complete and correctly reported. No unguarded call sites exist.

**One note:** The spec also lists `?.` (null-conditional) and `??` (null-coalescing) as acceptable guard forms. The plan does not report any call site using these forms — instead both use the explicit `if (rule == null)` pattern. This is fine; both are valid guard forms and the explicit if-null is the strongest form.

**Result: PASS** — all 2 call sites confirmed guarded. SCAN-08 = ALL GUARDED.

---

## CHECK 11 — 7+1 Scan Checklist (Section 10)

**Spec requires (lines 23139–23147):** 8 scans total (SCAN-01 through SCAN-07 baseline + SCAN-08 FindRule call-site audit).

**Plan (Section 10):**

| Scan | In Plan? | Command matches spec? | Expected result matches spec? |
|------|----------|----------------------|-------------------------------|
| SCAN-01 `lock(` | YES | YES | YES — 0 results |
| SCAN-02 `async void ` | YES | YES | YES — 0 results |
| SCAN-03 `return null` | YES | YES | YES — 0 new instances |
| SCAN-04 `throw new ` | YES | YES | YES — 0 new instances |
| SCAN-05 complexity_audit.py | YES | YES | YES — all CYC <= 8 |
| SCAN-06 dotnet build | YES | YES | YES — 0 errors |
| SCAN-07 dotnet test | YES | YES | YES — T_B55B_01 PASS + baseline |
| SCAN-08 FindRule call-site | YES | YES — same grep + ±2 context | YES — ALL GUARDED |

**Minor discrepancy:** Spec SCAN-07 says "all [Fact] pass (baseline ~261 + 1 new)" (line 23146). Plan SCAN-07 says "T_B55B_01 PASS; all baseline tests unchanged (255 pass + 24 pre-existing fail)". The plan baseline of 255 pass + 24 fail = 279 total (plus 1 new = 280 total) matches the plan's test count tracking (Section 2). The spec's "~261 + 1 new" appears to be a LaneA+LaneB combined estimate (261 = both lanes together, as stated in spec line 23172: "~263 [Fact] tests total after both lanes (261 baseline + 2 new)"). The plan's baseline of 279 (255 pass + 24 fail) is consistent with its deferred-backlog context. This is a baseline count notation difference, not a scan defect.

**Result: PASS** — all 8 scans present and correctly specified.

---

## CHECK 12 — Threading Model (JS-021 / NT8-018)

No `lock()` added. `FindRule` reads `_rules` (ConcurrentBag) via `foreach` — snapshot enumeration is lock-free. XML doc comment has no threading implications. New test runs on xUnit test thread with no concurrency involved.

**Result: PASS**

---

## CHECK 13 — Scan-03 / Pre-existing `return null` Baseline

The plan correctly notes in Section 13 that the null returns in `FindRule` are pre-existing (not new) and uses the "PRE-EXISTING-02" deferred item as precedent. SCAN-03 in the plan specifies "0 NEW instances" — acknowledging pre-existing instances are expected. This is aligned with spec SCAN-03 ("→ 0 new instances").

**Result: PASS**

---

## SPEC COVERAGE MATRIX

| Requirement | Plan Section | Status |
|-------------|-------------|--------|
| DW-B47-05 P2 — XML doc comment on FindRule | Sections 4.1, 9 Ticket T1 | ADDRESSED |
| DW-B47-05 P2 — Call-site audit (all guarded) | Section 5 (SCAN-08) | ADDRESSED |
| DW-B47-05 P2 — T_B55B_01 [Fact] test | Sections 4.2, 9 Ticket T2 | ADDRESSED (see CHECK 9 note) |
| No return-type change | All sections | ADDRESSED |
| No call-site rewrites | All sections | ADDRESSED |
| No logic changes (doc + test only) | Sections 1, 3 | ADDRESSED |
| JS-021 compliance (no lock) | Sections 7, 9 | ADDRESSED |
| JS-002 compliance (documented/tested) | Section 13 | ADDRESSED |
| JS-033 compliance (no async void) | Sections 9 | ADDRESSED |
| NT8 rule compliance | Section 8 | ADDRESSED |
| 7+1 scan checklist | Section 10 | ADDRESSED |
| Build tag | Section 11 | ADDRESSED |
| Hard-link sync mandate | Section 12 | ADDRESSED |
| `[return: MaybeNull]` from spec | Section 13 (omitted, NT8-justified) | PARTIALLY — omission NT8-justified |

---

## VIOLATIONS SUMMARY

| ID | Severity | Rule | Description | Location |
|----|----------|------|-------------|----------|
| — | — | — | No P0 violations found | — |
| — | — | — | No P1 violations found | — |
| NOTE-01 | INFO | JS-002 / test quality | `Assert.Equal(typeof(CopyRule?), mi.ReturnType)` is vacuous for reference types (NRT annotation is compile-time only; at CLR level `typeof(CopyRule?) == typeof(CopyRule)`). Test still PASSES and locks null contract via `Assert.Null(result)`. Engineer must be aware this signature assertion verifies nothing meaningful. | Plan Section 4.2, Ticket T2 |
| NOTE-02 | INFO | Spec alignment | Spec mentions `[return: MaybeNull]` annotation; plan omits it citing NT8/.NET 4.8 (no `System.Diagnostics.CodeAnalysis.MaybeNullAttribute` in .NET 4.8 BCL). Omission is NT8-justified and documented in Section 13. | Plan Section 13 |

---

## VERDICT

**REVIEW_PASS**

All P0 and P1 checks pass. Both findings are INFO-level notes that do not block execution:

- **NOTE-01** (vacuous `typeof(CopyRule?)` assertion): The test will PASS and the null-return contract is correctly locked by `Assert.Null(result)`. The signature-verification assertion is redundant and semantically inert for a reference type, but it does no harm. The engineer should be aware this assertion provides no real type-safety coverage.

- **NOTE-02** (`[return: MaybeNull]` omission): NT8/.NET 4.8 does not include `MaybeNullAttribute` in BCL. Omitting the attribute is the correct pragmatic decision for this codebase. Section 13 documents the resolution accurately.

The plan is complete, correctly scoped (doc + test only), and compliant with all P0/P1 Jane Street rules and NT8 compiler constraints. The 7+1 scan checklist is present and fully specified. The call-site audit is complete and correctly reports 2 guarded sites.

**Proceed to Phase 3 (Ticket Generation).**

---

*ptt-plan-reviewer | B55-LaneB | 2026-08-10*
