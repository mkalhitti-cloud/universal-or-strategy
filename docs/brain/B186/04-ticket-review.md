# Ticket Review: B186
## PTT-COPIER B186 | Quick ALL + QAll2t Instrument Scope

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-07-08
**Input tickets**: `docs/brain/B186/04-tickets.md`
**Plan source**: `docs/brain/B186/02-architecture-plan.md` (REVIEW_PASS)
**Spec source**: `docs/brain/B186/00-orchestrator-prompt.md`
**Rules source**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## T1 — DW-B186-QX-ALL-SCOPE-01

### Traceability
- DW card **DW-B186-QX-ALL-SCOPE-01** present in ticket header and spec requirements section.
- Ticket changes (combined gate, new log line, instrument filter, CYC doc annotation) map exactly to plan Section 3.
- No phantom work: every change in ticket is in plan Section 3.
- No missing work: all plan Section 3 items (combined gate, log replacement, instrument filter, doc comment) appear in ticket.

**Traceability: PASS**

### JS Pre-Check
| Rule | Ticket Description | Status |
|------|--------------------|--------|
| JS-021 (P0) | `lock()` | Ticket explicitly states no lock added. Combined gate uses `\|\|` operator only. PASS |
| JS-001 (P0) | `throw new` in hot path | Ticket adds no throw statements. PASS |
| JS-002 (P0) | `return null` | Method is `void`; early returns are `return;`. PASS |
| JS-033 (P0) | `async void` | Method is synchronous `void`. PASS |
| ASCII | New string literals | `"[PTT-QX-ALL] GlobalQuickExit fired instr="` — ASCII-only. PASS |

**JS Pre-Check: PASS**

### CYC Pre-Check
Branches enumerated in ticket CYC table:

| # | Branch | Before | After |
|---|--------|--------|-------|
| 1 | Combined gate `!QxGlobalExit \|\| instrument==null` | flag-only gate (1) | combined gate (1) |
| 2 | acc foreach loop | (1) | (2) |
| 3 | follower guard continue | (2) | (3) |
| 4 | pos foreach loop | (3) | (4) |
| 5 | null/flat guard continue | (4) | (5) |
| 6 | instrument filter continue (NEW) | — | (6) |
| 7 | NeedsLeaderFallbackFlatten guard | (6) | (7) |
| 8 | ExecuteFollowers delegate | (7) | (8) |

Before CYC = 7 → After CYC = 8. AT CEILING. Acceptable per spec. All 8 branches enumerated and documented. No at-risk method (8 <= 8).

**CYC Pre-Check: PASS**

### NT8 Check
- No `async/await` in `Execute(Instrument)`. PASS
- `Account.All` usage is existing pattern, called from UI thread. PASS
- No `sealed` on `TradeCopierWindow`. PASS
- No `FontFamily` usage. PASS
- No hardcoded hex color. PASS
- No `DateTime.Now` (no DateTime usage added). PASS
- `CreateOrder` not touched. PASS

**NT8 Check: PASS**

### Test Coverage
- `T_B186_01_ExecuteHasInstrumentParameter`: full `[Fact]` body provided, NT8-runtime skip pattern (`if (type == null) return;`) present. Asserts `Execute(Instrument)` overload exists.
- `T_B186_02_ExecuteNullInstrumentReturnsImmediately`: full `[Fact]` body provided, NT8-runtime skip pattern present. Asserts `Execute(null)` does not throw (combined gate returns early).
- Test file: `src/PropTraderTools/CopyEngineTests.cs` — correctly named.
- Every new public/internal method described in T1 (`Execute(Instrument)`) has a corresponding `[Fact]` test.

**Test Coverage: PASS**

### Scan Checklist
SCAN-01 through SCAN-07 present with exact PowerShell commands and expected results. Per-ticket presence confirmed.

| Scan | Present | Command |
|------|---------|---------|
| SCAN-01 | YES | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "lock\("` |
| SCAN-02 | YES | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "return null"` |
| SCAN-03 | YES | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "async void "` |
| SCAN-04 | YES | CYC manual count documented (8 branches) |
| SCAN-05 | YES | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "[^\x00-\x7F]"` |
| SCAN-06 | YES | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "throw new"` |
| SCAN-07 | YES | `dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 \| Select-Object -Last 5` |

**Scan Checklist: PASS**

### File Routing
- Source: `src/PropTraderTools/Features/PttGlobalQuickExit.cs` — Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`). PASS
- Tests: `src/PropTraderTools/CopyEngineTests.cs` — Wave workspace. PASS

**File Routing: PASS**

### T1 Verdict: TICKET_REVIEW_PASS

---

## T2 — DW-B186-QX-ALL-SCOPE-02

### Traceability
- DW card **DW-B186-QX-ALL-SCOPE-02** present in ticket header and spec requirements section.
- Deferred item **DW-B185-QX-ALL-SCOPE-01** explicitly cited as closed by this ticket.
- Ticket changes (combined gate, fold into branch (6), doc annotation) map exactly to plan Section 4.
- No phantom work: every change in ticket is in plan Section 4.
- No missing work: all plan Section 4 items (combined gate, branch-6 fold, doc comment) appear in ticket.

**Traceability: PASS**

### JS Pre-Check
| Rule | Ticket Description | Status |
|------|--------------------|--------|
| JS-021 (P0) | `lock()` | No lock added. PASS |
| JS-001 (P0) | `throw new` in hot path | No throw statements added. PASS |
| JS-002 (P0) | `return null` | Method is `void`; early returns are `return;`. PASS |
| JS-033 (P0) | `async void` | Method is synchronous `void`. PASS |
| ASCII | New string literals | No new string literals added in T2 body. PASS |

**JS Pre-Check: PASS**

### CYC Pre-Check — CRITICAL FOLD VERIFICATION
The critical review requirement: instrument filter must be FOLDED into existing branch (6), NOT added as a new branch.

Ticket Change 2 specifies:
```csharp
// BEFORE (existing branch 6):
if (pos == null || pos.Quantity == 0)
    continue; // (6)

// AFTER (fold — CYC unchanged):
if (pos == null || pos.Quantity == 0 || pos.Instrument?.FullName != instrument.FullName)
    continue; // (6) extended -- instrument filter folded in
```

This is a compound-condition extension of one `if` statement — NOT a new `if` statement. CYC count for a compound boolean condition using `||` within a single `if` does NOT add a new branch in cyclomatic complexity. CYC stays at 8.

Ticket CYC table explicitly labels branch (6) as "FOLDED" and documents "Before: CYC = 8. After: CYC = 8." The CRITICAL section states: "This is a fold into existing branch (6), NOT a new branch."

Branches enumerated: combined-gate(1), null/empty-guard(2), acc-loop(3), follower-skip(4), pos-loop(5), null/flat/instr-continue(6 FOLDED), flatten-guard(7), ExecuteFollowers(8). Total = 8. AT CEILING. Hold strategy confirmed.

**CYC Pre-Check: PASS** (fold strategy correctly applied; no new branch added)

### NT8 Check
- No `async/await` in `Execute(List, Instrument)`. PASS
- `Account.All` usage is existing pattern, called from UI thread. PASS
- No `sealed` on `TradeCopierWindow`. PASS
- No `FontFamily` usage. PASS
- No hardcoded hex color. PASS
- No `DateTime.Now`. PASS

**NT8 Check: PASS**

### Test Coverage
- `T_B186_04_ExecuteForcedTargetsHasInstrumentParameter`: full `[Fact]` body provided, NT8-runtime skip pattern present. Asserts 2-parameter `Execute(List, Instrument)` overload exists.
- `T_B186_05_ExecuteForcedTargetsNullInstrumentReturnsImmediately`: full `[Fact]` body provided, NT8-runtime skip pattern present. Asserts `Execute(targets, null)` does not throw (combined gate returns early).
- Test file: `src/PropTraderTools/CopyEngineTests.cs` — correctly named.
- Every new public/internal method described in T2 (`Execute(List, Instrument)`) has a corresponding `[Fact]` test.

**Test Coverage: PASS**

### Scan Checklist
SCAN-01 through SCAN-07 present with exact PowerShell commands and expected results. Per-ticket presence confirmed.

| Scan | Present | Command |
|------|---------|---------|
| SCAN-01 | YES | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "lock\("` |
| SCAN-02 | YES | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "return null"` |
| SCAN-03 | YES | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "async void "` |
| SCAN-04 | YES | CYC manual count documented (8 branches, fold confirmed) |
| SCAN-05 | YES | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "[^\x00-\x7F]"` |
| SCAN-06 | YES | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "throw new"` |
| SCAN-07 | YES | `dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 \| Select-Object -Last 5` |

**Scan Checklist: PASS**

### File Routing
- Source: `src/PropTraderTools/Features/PttGlobalQuickExit.cs` — Wave workspace. PASS
- Tests: `src/PropTraderTools/CopyEngineTests.cs` — Wave workspace. PASS

**File Routing: PASS**

### T2 Verdict: TICKET_REVIEW_PASS

---

## T3 — DW-B186-PANEL-CALLSITES-01

### Traceability
- DW card **DW-B186-PANEL-CALLSITES-01** present in ticket header and spec requirements section.
- Both call site changes (`OnQuickAllClick` and `OnInstrQAll2tClick`) documented.
- Ticket changes map exactly to plan Section 5.
- No phantom work: every change in ticket is in plan Section 5.
- No missing work: both plan Section 5 call-site changes covered; no-null-guard-at-call-site constraint documented.
- T3 dependency on T1+T2 explicitly stated (correct execution order enforced).

**Traceability: PASS**

### JS Pre-Check
| Rule | Ticket Description | Status |
|------|--------------------|--------|
| JS-021 (P0) | `lock()` | No lock added to either method. PASS |
| JS-001 (P0) | `throw new` in hot path | No throw statements added. PASS |
| JS-002 (P0) | `return null` | Both methods are `void`; existing `return;` unchanged. PASS |
| JS-033 (P0) | `async void` | Both methods are synchronous `void` event handlers. PASS |
| ASCII | New string literals | No new string literals added in T3. PASS |

**JS Pre-Check: PASS**

### CYC Pre-Check
- `OnQuickAllClick`: only change is argument added to existing call — no new branch. CYC = 1, UNCHANGED.
- `OnInstrQAll2tClick`: only change is argument added to existing call on line `// (2)` — no new branch. CYC = 2, UNCHANGED.
- Both methods well below CYC ceiling of 8.

**CYC Pre-Check: PASS**

### NT8 Check
- `OnQuickAllClick` and `OnInstrQAll2tClick` are `private void` synchronous event handlers — no `async void` concern. PASS
- No `sealed` on `TradeCopierWindow` or any class. PASS
- No `FontFamily`. PASS
- No hardcoded hex color. PASS
- No `DateTime.Now`. PASS
- No null guard added at call site — consistent with combined gate in `Execute` overloads per B185 pattern. PASS

**NT8 Check: PASS**

### Test Coverage
- `T_B186_03_OnQuickAllClickPassesInstrument`: full `[Fact]` body provided, NT8-runtime skip pattern present. Asserts `_instrument` field exists on `TradeCopierPanel` AND `Execute(Instrument)` overload exists on `PttGlobalQuickExit` — structural evidence for valid `gqx.Execute(_instrument)` call site.
- `T_B186_06_OnInstrQAll2tClickPassesInstrument`: full `[Fact]` body provided, NT8-runtime skip pattern present. Asserts `_instrument` field exists on `TradeCopierPanel` AND `Execute(List, Instrument)` overload exists — structural evidence for valid `Execute(targets, _instrument)` call site.
- Test file: `src/PropTraderTools/CopyEngineTests.cs` — correctly named.
- Both modified methods (`OnQuickAllClick`, `OnInstrQAll2tClick`) have corresponding `[Fact]` tests.

**Test Coverage: PASS**

### Scan Checklist
SCAN-01 through SCAN-07 present with exact PowerShell commands and expected results. Per-ticket presence confirmed.

| Scan | Present | Command |
|------|---------|---------|
| SCAN-01 | YES | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "lock\("` |
| SCAN-02 | YES | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "return null"` |
| SCAN-03 | YES | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "async void "` |
| SCAN-04 | YES | CYC manual count documented (1 and 2 respectively, unchanged) |
| SCAN-05 | YES | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "[^\x00-\x7F]"` |
| SCAN-06 | YES | `git diff HEAD~1 HEAD -- src/ \| Select-String "^\+" \| Select-String "throw new"` |
| SCAN-07 | YES | `dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 \| Select-Object -Last 5` |

**Scan Checklist: PASS**

### File Routing
- Source: `src/PropTraderTools/TradeCopierPanel.cs` — Wave workspace. PASS
- Tests: `src/PropTraderTools/CopyEngineTests.cs` — Wave workspace. PASS

**File Routing: PASS**

### T3 Verdict: TICKET_REVIEW_PASS

---

## Aggregate Spec Coverage Check

| Spec Requirement | Covered By | Status |
|-----------------|-----------|--------|
| DW-B186-QX-ALL-SCOPE-01: `Execute()` → `Execute(Instrument)` | T1 | COVERED |
| DW-B186-QX-ALL-SCOPE-02: `Execute(forcedTargets)` → `Execute(List, Instrument)` | T2 | COVERED |
| DW-B186-PANEL-CALLSITES-01: both call sites pass `_instrument` | T3 | COVERED |
| DW-B185-QX-ALL-SCOPE-01 (deferred closure): wrong-instrument qty split eliminated | T2 (branch 6 fold) | COVERED |

No duplicate coverage. No uncovered spec requirement. ✅

## Test Count Check

| Test ID | Ticket | What it asserts |
|---------|--------|----------------|
| T_B186_01 | T1 | `Execute(Instrument)` overload exists (reflection) |
| T_B186_02 | T1 | `Execute(null)` does not throw (combined gate structural) |
| T_B186_03 | T3 | `_instrument` field + `Execute(Instrument)` overload exist (call site structural) |
| T_B186_04 | T2 | `Execute(List, Instrument)` overload exists (reflection) |
| T_B186_05 | T2 | `Execute(targets, null)` does not throw (combined gate structural) |
| T_B186_06 | T3 | `_instrument` field + `Execute(List, Instrument)` overload exist (call site structural) |

**6 tests total. All T_B186_01..T_B186_06 present. All in `CopyEngineTests.cs`. All use NT8-runtime skip pattern.** ✅

## Pattern Check — B185 PttBeSwapAll Reference

- T1: Combined gate `if (!flags.QxGlobalExit || instrument == null) return;` matches B185 pattern exactly. ✅
- T1: Instrument filter as separate line after null/flat guard matches B185 pattern. ✅
- T2: Combined gate same as T1 / B185 pattern. ✅
- T2: Fold into branch (6) rather than separate line — correct CYC hold strategy. ✅
- T3: No null guard at call site — explicitly stated and matches B185 `PttBeSwapAll.Execute(buffer, _instrument)` precedent. ✅

---

## Overall: TICKET_REVIEW_PASS

All 3 tickets pass all checks. No violations found. Pipeline may proceed to Phase 4a (ptt-engineer).

**Execution order for engineer**: T1 + T2 (single pass, `PttGlobalQuickExit.cs`) → T3 (`TradeCopierPanel.cs`) → Tests (`CopyEngineTests.cs`).

---
*Written by: ptt-ticket-reviewer (Phase 3.5)*
*Epic: B186*
*Result: TICKET_REVIEW_PASS*
*Next phase: ptt-engineer -> Ph4a implementation*
