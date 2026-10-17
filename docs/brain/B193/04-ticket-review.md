# Ticket Review: B193

**Epic**: B193 -- Formalize B193-DIRECT-1/2/3: per-panel Quick ALL + button rename
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-10-06
**Tickets reviewed**: docs/brain/B193/04-tickets.md
**Plan source**: docs/brain/B193/02-architecture-plan.md (REVIEW_PASS 2026-10-06)
**Rules source**: docs/standards/jane-street/RULES_CATALOG.md
**Spec source**: specs/002-trade-copier-spec.html
**Plan review**: docs/brain/B193/02-plan-review.md (REVIEW_PASS 2026-10-06)

---

## T1 -- New Test File: B193Tests.cs

### Traceability: PASS

| Ticket item | Maps to |
|------------|---------|
| 10 new [Fact] tests (T_B193_01..10) | B193-DIRECT-2, B193-DIRECT-3, QUICK-LABEL-UNIT-01 (spec) |
| InvokeFormatBuffer helper | Architecture plan T1, plan review Section B9 |
| InvokeFormatQuickAllBuffer helper | Architecture plan T1 |
| File path `src/PropTraderTools/Tests/B193Tests.cs` | Architecture plan Component Inventory |

All 10 test cases T_B193_01..10 present with exact method names, inputs, and assertions matching plan Section T1 assertion table. No phantom items. No missing plan items.

### JS Pre-Check: PASS

| Rule | Ticket claim | Reviewer verdict |
|------|-------------|-----------------|
| JS-021 (P0): lock() | COMPLIANT -- reflection + Assert only, no shared mutable state | PASS -- no lock() in described or inline code |
| JS-001 (P0): throw in hot paths | COMPLIANT -- no throw statements in any test method | PASS |
| JS-002 (P0): return null | COMPLIANT -- helpers return string via cast; [Fact] methods return void | PASS -- no `return null;` statement in any described method |
| JS-033 (P0): async void | COMPLIANT -- all [Fact] methods synchronous void | PASS |
| JS-066 (P0): CYC <= 8 | All 12 methods CYC=1 | PASS |
| JS-081: ASCII-only | All string literals verified ASCII | PASS |

NOTE on `m.Invoke(null, ...)`: The reflection helpers call `m.Invoke(null, ...)` where `m` is the result of `GetMethod()`. If the target method is absent, `m` is null and a NullReferenceException is thrown at runtime. This is NOT a JS-001 violation (no `throw new` statement) and is the standard xUnit test failure mechanism -- an unhandled exception in a [Fact] body causes the test to fail with an error, which is the correct behavior when testing a method's presence. This pattern is identical to the existing B189T4SpinnerTests pattern (plan review Section B9). NOT a violation.

### CYC Pre-Check: PASS

All 12 methods (2 helpers + 10 [Fact] tests) stated CYC=1. No if/else/switch/ternary/loop/catch in any described or inline method body. SCAN-01 contract is explicit.

### NT8 Check: PASS

| Check | Verdict |
|-------|---------|
| async/await in lifecycle method | PASS -- no NT8 lifecycle methods present |
| Account.All outside Loaded handler | PASS -- no Account.All calls |
| sealed on TradeCopierWindow | PASS -- no TradeCopierWindow subclass |
| FontFamily on WPF element | PASS -- no WPF element construction |
| Hardcoded hex color | PASS -- no color strings |
| CreateOrder without PTT- prefix | PASS -- no CreateOrder calls |
| DateTime.Now | PASS -- no DateTime usage |
| NT8 host required | PASS -- ticket states "No NT8 host required -- pure reflection + formula tests" |

### Test Coverage: PASS

| Method described | [Fact] or test specified | Assertion |
|-----------------|--------------------------|-----------|
| InvokeFormatBuffer (private static string) | Used by T_B193_05, T_B193_06, T_B193_07 | Implicit: output verified via Assert.Equal |
| InvokeFormatQuickAllBuffer (private static string) | Used by T_B193_08 | Implicit: output verified via Assert.Equal |
| T_B193_01 | [Fact] -- Assert.Equal((10,20), result) | PRESENT |
| T_B193_02 | [Fact] -- Assert.Equal((10,20), result) | PRESENT |
| T_B193_03 | [Fact] -- Assert.Equal((4,8), result) | PRESENT |
| T_B193_04 | [Fact] -- Assert.Equal((4,8), result) | PRESENT |
| T_B193_05 | [Fact] -- Assert.Equal("3x +10", result) | PRESENT |
| T_B193_06 | [Fact] -- Assert.Equal("2x +10", result) | PRESENT |
| T_B193_07 | [Fact] -- Assert.Equal("2x All +10", result) | PRESENT |
| T_B193_08 | [Fact] -- Assert.Equal("3x All +10t", result) | PRESENT |
| T_B193_09 | [Fact] -- Assert.NotNull(GetField("_quickAllT1", NonPublic|Instance)) | PRESENT -- BindingFlags.NonPublic|Instance verified |
| T_B193_10 | [Fact] -- Assert.Equal(10, InstrumentDefaults.GetQuickTicks("MGC").t1) | PRESENT |

Private/internal reflection helpers (InvokeFormatBuffer, InvokeFormatQuickAllBuffer) serve as infrastructure, not standalone targets; they are exercised by T_B193_05..08. No standalone [Fact] for helpers is required by convention. PASS.

### Scan Checklist: PASS

All 7 scans present in T1:

| Scan | Present | Pass Criterion Stated |
|------|---------|-----------------------|
| SCAN-01 CYC | YES | Zero methods > 8 branches |
| SCAN-02 lock() | YES | 0 matches on B193Tests.cs |
| SCAN-03 async void | YES | 0 matches on B193Tests.cs |
| SCAN-04 return null | YES | 0 matches on B193Tests.cs |
| SCAN-05 throw new | YES | 0 matches on B193Tests.cs |
| SCAN-06 ASCII | YES | No codepoint > 0x7F in any string literal |
| SCAN-07 JS-033 | YES | 0 async void signatures |

Post-edit gate present: `dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build` with 10 new PASS, zero regressions.

### File Routing: PASS

`src/PropTraderTools/Tests/B193Tests.cs` is within the Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`). No Director workspace paths for .cs files.

### VERDICT: TICKET_REVIEW_PASS

---

## T2 -- Fix Stale Assertions in Existing Test Files

### Traceability: PASS

| Ticket item | Maps to |
|------------|---------|
| Edit block A (T_B189_T4_01, CopyEngineTests.cs) | Architecture plan T2 Edit block A; plan review Section B3 |
| Edit block B (T_B189_T4_02, CopyEngineTests.cs) | Architecture plan T2 Edit block B; plan review Section B3 |
| Edit block C (T_B189_T4_06, CopyEngineTests.cs) | Architecture plan T2 Edit block C; plan review Section B3 |
| Edit block D (T_LABEL_01, B73Tests.cs) | Architecture plan T2 Edit block D; plan review Section B3 |
| Edit block E (T_LABEL_03, B73Tests.cs) | Architecture plan T2 Edit block E; plan review Section B3 |
| Edit block F (T_LABEL_04, B73Tests.cs) | Architecture plan T2 Edit block F; plan review Section B3 |
| dotnet test gate (0 failures) | Architecture plan T2 Post-edit gate; plan review Section B4 |
| Pre-condition: T1 green | Ticket Ordering Summary; plan T2 ordering |

All 6 edit blocks present, scoped to the correct files, with old/new strings matching the B193-DIRECT-3 rename table (Quick->3x, Quick2t->2x, Quick ALL->3x All, QAll2t->2x All). No phantom items. No plan items absent.

NOTE on Edit Block E (T_LABEL_03) assertion strengthening: The ticket strengthens `Assert.Contains("t", result)` to `Assert.Contains("3x All", result)` + `Assert.Contains("+4t", result)`. The ticket's explanatory note correctly identifies that the old assertion was semantically weak. This strengthening is in-scope (fixing a stale test) and introduces no new branches (still two sequential `Assert.Contains` calls). This deviation from the plan (plan shows old assertion was `Assert.Contains("Quick ALL", result)` + `Assert.Contains("+4t", result)`) is additive and conservative. PASS.

### JS Pre-Check: PASS

| Rule | Ticket claim | Reviewer verdict |
|------|-------------|-----------------|
| JS-021 (P0): lock() | COMPLIANT -- string literal replacements only | PASS |
| JS-001 (P0): throw | COMPLIANT -- no throw added | PASS |
| JS-002 (P0): return null | COMPLIANT -- no return null added | PASS |
| JS-033 (P0): async void | COMPLIANT -- no method signature changes | PASS |
| JS-066 (P0): CYC | COMPLIANT -- no new branches; existing CYC unchanged | PASS |
| JS-081: ASCII-only | "3x", "2x", "3x All", "2x All", "2x All +4", "3x All +4t", "3x All +0t" -- all 7-bit ASCII | PASS |

### CYC Pre-Check: PASS

No new if/switch/ternary/loop/catch in any of the 6 edited methods. String literal replacements and one assertion call addition (Edit block E) only. All 6 edited methods remain CYC=1.

### NT8 Check: PASS

No NT8 API calls added. No lifecycle methods modified. No WPF/NT8 constructs introduced. String literal replacements only.

### Test Coverage: PASS

T2 is a fix-ticket (no new production methods). All 6 modified test methods have existing [Fact] decorators (they are the stale tests being fixed). No new production methods introduced that would require new [Fact] tests.

### Scan Checklist: PASS

All 7 scans present in T2:

| Scan | Present | Pass Criterion Stated |
|------|---------|-----------------------|
| SCAN-01 CYC | YES | Zero new branches in any edited method |
| SCAN-02 lock() | YES | 0 new lock() matches on edited lines |
| SCAN-03 async void | YES | 0 new async void on edited lines |
| SCAN-04 return null | YES | 0 new return null on edited lines |
| SCAN-05 throw new | YES | 0 new throw new on edited lines |
| SCAN-06 ASCII | YES | Replacement strings verified 7-bit ASCII |
| SCAN-07 JS-033 | YES | No method signature changes |

POST-GATE present: `dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build` with 0 failures. Named tests T_B189_T4_01, T_B189_T4_02, T_B189_T4_06, T_LABEL_01, T_LABEL_03, T_LABEL_04 must individually PASS. Exact dotnet test output required in completion artifact.

### File Routing: PASS

`src/PropTraderTools/CopyEngineTests.cs` and `src/PropTraderTools/Tests/B73Tests.cs` are within the Wave workspace. No Director workspace paths for .cs files.

### VERDICT: TICKET_REVIEW_PASS

---

## T3 -- Spec HTML Update

### Traceability: PASS

| Ticket item | Maps to |
|------------|---------|
| Section 1: Button label rename table | Architecture plan T3 Section 1; plan review Section B5 |
| Section 2: B193 block history row | Architecture plan T3 Section 2; plan review Section B5 |
| Section 3: InstrumentDefaults table verification | Architecture plan T3 Section 3; plan review Section B5 |
| Section 4: SetInstrument() AC for _quickAllT1 | Architecture plan T3 Section 4; plan review Section B5 |
| Section 5: DW-B192-SINST-01 open card | Architecture plan T3 Section 5; Deferred Work Register |
| Section 6: DW-B192-QALL-RESET-01 close (conditional) | Architecture plan T3 Section 6; plan review Section B5 |
| Pre-condition: T2 green | Ticket Ordering Summary |

All 6 spec update sections covered. DW-B192-SINST-01 addition present. No phantom items. No plan items absent.

NOTE on Table-cell scope restriction (Section 1): The ticket correctly restricts label replacements to button label table cells and prohibits global find/replace that would alter historical references. This matches the architecture plan's intent and is the correct scoping.

### JS Pre-Check: PASS

| Rule | Ticket claim | Reviewer verdict |
|------|-------------|-----------------|
| JS-081: ASCII-only | All new HTML text content 7-bit ASCII ("B193", "2026-10-06", "3x", "2x", "3x All", "2x All", "Formalize B193-DIRECT-1/2/3", "_quickAllT1", "DW-B192-SINST-01", "SetInstrument()", "CYC=8") | PASS |
| All other JS rules | N/A for HTML file | N/A -- correct |

### CYC Pre-Check: PASS

N/A -- HTML file has no cyclomatic complexity.

### NT8 Check: PASS

N/A -- spec HTML file. No NT8 API constraints apply.

### Test Coverage: PASS

T3 is a spec update ticket. No C# methods introduced. No [Fact] tests required. Manual 6-section review gate specified in POST-GATE.

### Scan Checklist: PASS

All 7 scans present in T3:

| Scan | Present | Verdict |
|------|---------|---------|
| SCAN-01 CYC | YES (N/A stated with rationale) | PASS -- correct to mark N/A for HTML |
| SCAN-02 lock() | YES (N/A stated) | PASS |
| SCAN-03 async void | YES (N/A stated) | PASS |
| SCAN-04 return null | YES (N/A stated) | PASS |
| SCAN-05 throw new | YES (N/A stated) | PASS |
| SCAN-06 ASCII | YES -- explicit list of new text content | PASS |
| SCAN-07 JS-033 | YES (N/A stated) | PASS |

POST-GATE present: Manual 6-section review against Section requirements. Section-by-section result required in completion artifact. Spec HTML validity check (well-formed HTML, no unclosed tags) included in acceptance criteria.

### File Routing: PASS

`specs/002-trade-copier-spec.html` is a spec file (not a .cs source file). No Wave workspace .cs path constraint applies. Correct.

### VERDICT: TICKET_REVIEW_PASS

---

## Aggregate Spec Coverage Matrix

| Spec / Plan Requirement | Covered by | Status |
|------------------------|-----------|--------|
| B193-DIRECT-2: per-panel _quickAllT1 field formalization | T1 (T_B193_09) | COVERED |
| B193-DIRECT-3: button rename formalization | T1 (T_B193_05..08), T2 (all 6 edit blocks) | COVERED |
| QUICK-LABEL-UNIT-01: FormatQuickAllBuffer "t" suffix | T1 (T_B193_08) | COVERED |
| All 10 test cases T_B193_01..10 | T1 | COVERED |
| Stale assertion fix: T_B189_T4_01 | T2 Edit block A | COVERED |
| Stale assertion fix: T_B189_T4_02 | T2 Edit block B | COVERED |
| Stale assertion fix: T_B189_T4_06 | T2 Edit block C | COVERED |
| Stale assertion fix: T_LABEL_01 | T2 Edit block D | COVERED |
| Stale assertion fix: T_LABEL_03 | T2 Edit block E | COVERED |
| Stale assertion fix: T_LABEL_04 | T2 Edit block F | COVERED |
| dotnet test gate (0 failures after T2) | T2 POST-GATE | COVERED |
| Spec Section 1: button label rename table | T3 Section 1 | COVERED |
| Spec Section 2: B193 block history row | T3 Section 2 | COVERED |
| Spec Section 3: InstrumentDefaults table | T3 Section 3 | COVERED |
| Spec Section 4: SetInstrument() AC | T3 Section 4 | COVERED |
| Spec Section 5: DW-B192-SINST-01 open card | T3 Section 5 | COVERED |
| Spec Section 6: DW-B192-QALL-RESET-01 close | T3 Section 6 | COVERED |
| DW-B183-03 (P1) carry-forward | Deferred Work Register | COVERED |
| DW-B192-SINST-01 in Deferred Work Register | Deferred Work Register | COVERED |
| DW-B193-01 (P3) in Deferred Work Register | Deferred Work Register | COVERED |
| T1->T2->T3 sequential gate ordering | Ticket Ordering Summary | COVERED |

Zero uncovered requirements. Zero duplicate coverage conflicts.

---

## P0 Violation Log

| # | Rule ID | Description | Location | Severity |
|---|---------|-------------|----------|----------|
| (none) | -- | -- | -- | -- |

**Zero P0 violations found across all 3 tickets.**

---

## Non-Blocking Notes

1. **InvokeFormatBuffer return type (T1)**: Ticket helper returns `string` via internal cast; existing CopyEngineTests.cs B189T4SpinnerTests pattern returns `object` with call-site cast. Both are valid. Plan review Section B9 explicitly passed this as acceptable. No action required.

2. **Edit Block E assertion strengthening (T2)**: Ticket strengthens T_LABEL_03 from a single weak `Assert.Contains("t", result)` to dual `Assert.Contains("3x All", result)` + `Assert.Contains("+4t", result)`. This is architecturally sound (eliminates false-pass risk), introduces zero new CYC, and is within the stale-assertion-fix scope. No action required.

3. **DW-B193-01 (P3, OPEN)**: OnQuickAllBufferChanged still uses old label strings "Quick ALL"/"QAll2t". Correctly deferred -- low severity, correctly documented in Deferred Work Register. No action required for B193.

---

## Overall: TICKET_REVIEW_PASS

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | VERDICT |
|--------|-------------|-------------|---------------|-----------|---------------|----------------|-------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |
| T3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |

**TICKET_REVIEW_PASS**

Engineer (Phase 4a) is unblocked. Execute T1 -> T2 -> T3 sequentially. Each ticket must satisfy its green gate before the next begins.

---

*Written by: ptt-ticket-reviewer*
*Epic: B193*
*Phase: 3.5 (Ticket Review)*
*Status: TICKET_REVIEW_PASS*
*Plan reviewed: docs/brain/B193/02-architecture-plan.md (REVIEW_PASS 2026-10-06)*
*Tickets reviewed: docs/brain/B193/04-tickets.md*
