# B216 Ticket Review
## Ask/Bid Pattern-B Conversion + Cancel/CancelAll Buttons

**Block**: B216
**Reviewer Phase**: Ph3.5 (ptt-ticket-reviewer) -- CYCLE 2 (final)
**Inputs**: 04-tickets.md (revised), 02-architecture-plan.md, B216-orchestrator-prompt.md, RULES_CATALOG.md, TradeCopierPanel.cs, PttCancel.cs
**Previous Review**: Cycle 1 -- TICKET_REVIEW_FAIL (T-A1 "New Files: None" contradicted Scope Lock)
**Fix Applied**: T-A1 "New Files" section now reads: `src/PropTraderTools/Tests/B216Tests.cs (NEW -- created by T-A1. T-B1 and T-C1 append their tests to this file.)`

---

## T-A1: Lane A -- Ask Pattern B + Null-Leader Log

### Traceability
- SR-A1 (null-leader log on OnAskClick) -> T-A1 OnAskClick change: MAPPED
- SR-A2 (Ask-All Pattern B) -> T-A1 OnAskAllClick rewrite: MAPPED
- Architecture plan Lane A -> T-A1 target state: MATCH (code identical to plan target state)
- DW-B216-01 Part A (Ask) -> T-A1: MAPPED
- DW-B216-01 Part B (Ask-All) -> T-A1: MAPPED

**RESULT: PASS**

### JS Pre-Check
- JS-021 (no lock): No `lock(` in any code sample in T-A1. PASS
- JS-033 (no async void): All methods are synchronous `private void`. PASS
- JS-002 (no return null): `HasNonFlatPosition` returns bool. All new code returns void or bool. PASS
- JS-001 (no throw in hot path): No `throw` in any code sample. PASS

**RESULT: PASS**

### CYC Pre-Check
- `OnAskClick`: 3 decision points (`_instrument null`, `?? null-coalesce`, `_leaderAccount null`) = CYC 4. <= 8. PASS
- `OnAskAllClick` (Pattern B): decision points (`_instrument null`, `foreach`, `HasNonFlatPosition if`, `acted>0 if`) = CYC 4. <= 8. PASS
  - NOTE: SCAN-3 text says "count 3 decision points" but lists 4 items. Minor text inconsistency; CYC=4 is correct. Non-blocking WARN only.
- `HasNonFlatPosition`: decision points (`acc null`, `instr null`, `foreach`, `pos null continue`, compound `&&` condition counted per project convention as 1) = CYC 5. <= 8. PASS

**RESULT: PASS**

### NT8 Check
- `Account.All` in `OnAskAllClick`: UI thread event handler -- safe per NT8 docs. PASS
- `acc.Positions` in `HasNonFlatPosition`: UI thread -- safe. PASS
- `PttMoveTargetOne.Execute(acc, instr, useAsk: true, _askBuffer)`: matches existing signature confirmed at TradeCopierPanel.cs:1505. PASS
- `NinjaTrader.Code.Output.Process(string, PrintTo.OutputTab1)`: thread-safe NT8 API. PASS
- No `Account.All` call outside of event handler (synchronous UI-thread method). PASS
- No `async`/`await` in lifecycle methods. PASS

**RESULT: PASS**

### Test Coverage
- `OnAskClick` modified -> T_B216_A1 covers null-leader log (source inspection). PASS
- `OnAskAllClick` rewritten -> T_B216_A2 covers Pattern B presence (Account.All, log string). PASS
- `HasNonFlatPosition` (new private static): tested indirectly via source inspection in T_B216_A2. Architecture plan acknowledges source-inspection approach for NT8-bound helpers. PASS
  - WARN (non-blocking, carried from cycle 1): T_B216_A2 does not test HasNonFlatPosition behavioral correctness (flat vs non-flat). Both [Fact] tests are present and cover their ACs.

**RESULT: PASS** (with WARN on T_B216_A2 behavioral depth -- non-blocking)

### Scan Checklist Presence
- SCAN-1 (P0 violations grep): PRESENT. Commands specified.
- SCAN-2 (dotnet test): PRESENT. Baseline `Failed=24`, `Passed>=202`. Consistent with orchestrator-prompt spec.
- SCAN-3 (CYC by inspection): PRESENT. Per-method breakdown present.
- SCAN-4 (ASCII-only): PRESENT. PowerShell command specified.
- SCAN-5 (symbol existence): PRESENT. grep/Select-String commands for all new symbols.
- SCAN-6 (functional correctness): PRESENT. AC-A1, AC-A2, AC-A3 described.
- SCAN-7 (diff hygiene): PRESENT. ptt-sync, verify_pr_hygiene, verify_critical_symbols, hunk count check.

**RESULT: PASS** (all 7 scans present)

### File Routing
- `src/PropTraderTools/TradeCopierPanel.cs`: Wave workspace path. PASS
- `src/PropTraderTools/Tests/B216Tests.cs`: Wave workspace path. PASS
- No Director workspace paths for .cs files. PASS

**RESULT: PASS**

### Internal Consistency Check (Cycle 2 -- Fix Verification)
- **Cycle 1 Violation**: T-A1 "New Files" section previously stated "None" -- contradicted "Scope Lock" and "xUnit Tests Required" sections.
- **Fix Applied**: T-A1 "New Files" section (line 154-156) now reads:
  `src/PropTraderTools/Tests/B216Tests.cs (NEW -- created by T-A1. T-B1 and T-C1 append their tests to this file.)`
- **Consistency Verified**:
  - "New Files": `B216Tests.cs (NEW -- created by T-A1)` ✓
  - "xUnit Tests Required": `src/PropTraderTools/Tests/B216Tests.cs (NEW file, namespace PropTraderTools)` ✓
  - "Scope Lock" > Files T-A1 MUST touch: `src/PropTraderTools/Tests/B216Tests.cs (NEW -- T_B216_A1 + T_B216_A2)` ✓
  - T-B1 "New Files": "None. T-B1 only modifies TradeCopierPanel.cs and adds tests to B216Tests.cs." ✓ (consistent -- T-A1 creates, T-B1 appends)
  - T-C1 "xUnit Tests Required": "already created by T-A1 -- append tests to existing class" ✓ (consistent)
- All three sections now agree: T-A1 creates the file, T-B1 and T-C1 append to it.

**RESULT: PASS** (Cycle 1 violation resolved)

---

### T-A1 VERDICT: TICKET_REVIEW_PASS

---

## T-B1: Lane B -- Bid Pattern B + Null-Leader Log

### Traceability
- SR-B1 (null-leader log on OnBidClick) -> T-B1 OnBidClick change: MAPPED
- SR-B2 (Bid-All Pattern B) -> T-B1 OnBidAllClick rewrite: MAPPED
- Architecture plan Lane B -> T-B1 target state: MATCH (code identical to plan target state)
- DW-B216-01 Part A (Bid) -> T-B1: MAPPED
- DW-B216-01 Part B (Bid-All) -> T-B1: MAPPED
- Prerequisite (HasNonFlatPosition from T-A1) stated explicitly in T-B1 header and confirmed in Target State note. PASS

**RESULT: PASS**

### JS Pre-Check
- JS-021 (no lock): No `lock(` in any T-B1 code sample. PASS
- JS-033 (no async void): All methods are synchronous `private void`. PASS
- JS-002 (no return null): All new code returns void. PASS
- JS-001 (no throw): No throw in any code sample. PASS

**RESULT: PASS**

### CYC Pre-Check
- `OnBidClick`: 3 decision points (mirrors OnAskClick) = CYC 4. <= 8. PASS
- `OnBidAllClick` (Pattern B): mirrors OnAskAllClick = CYC 4. <= 8. PASS
- `HasNonFlatPosition`: REUSED (from T-A1), CYC 5 unchanged. PASS

**RESULT: PASS**

### NT8 Check
- `Account.All` in `OnBidAllClick`: UI thread event handler -- safe. PASS
- `PttMoveTargetOne.Execute(acc, instr, useAsk: false, _bidBuffer)`: correct signature, `useAsk: false` for Bid. PASS
- Threading model identical to T-A1 (synchronous UI thread). PASS

**RESULT: PASS**

### Test Coverage
- `OnBidClick` modified -> T_B216_B1 covers null-leader log (source inspection). PASS
- `OnBidAllClick` rewritten -> T_B216_B2 covers Pattern B presence and HasNonFlatPosition call. PASS
- T-B1 correctly appends to existing B216Tests.cs (created by T-A1) rather than creating new file. PASS

**RESULT: PASS**

### Scan Checklist Presence
- SCAN-1 (P0 violations grep): PRESENT. Commands specified.
- SCAN-2 (dotnet test): PRESENT. Baseline `Failed=24`, `Passed>=204`. Increments correctly from T-A1 baseline. PASS
- SCAN-3 (CYC by inspection): PRESENT. Per-method breakdown present.
- SCAN-4 (ASCII-only): PRESENT. PowerShell command specified. Log strings noted as ASCII-hyphen (not em-dash). PASS
- SCAN-5 (symbol existence): PRESENT. Select-String commands for all modified symbols. PASS
- SCAN-6 (functional correctness): PRESENT. AC-B1, AC-B2, AC-B3 described. PASS
- SCAN-7 (diff hygiene): PRESENT. ptt-sync, verify_pr_hygiene, verify_critical_symbols, hunk count check. PASS

**RESULT: PASS** (all 7 scans present)

### File Routing
- `src/PropTraderTools/TradeCopierPanel.cs`: Wave workspace. PASS
- `src/PropTraderTools/Tests/B216Tests.cs`: Wave workspace. PASS

**RESULT: PASS**

### Scope Lock Constraints
- T-B1 must NOT touch OnAskClick or OnAskAllClick: explicitly stated in scope lock. PASS
- T-B1 must NOT redefine HasNonFlatPosition: explicitly stated ("Do NOT redefine it"). PASS
- PttCancel.cs not touched: explicitly prohibited. PASS

**RESULT: PASS**

---

### T-B1 VERDICT: TICKET_REVIEW_PASS

---

## T-C1: Lane C -- Cancel + Cancel All Button Row

### Traceability
- SR-C1 (Cancel button Pattern A via LogAndDispatchModule) -> T-C1 OnCancelClick: MAPPED
- SR-C2 (Cancel All button + PttCancelAll.cs) -> T-C1 OnCancelAllClick + PttCancelAll.cs: MAPPED
- SR-C3 (Cancel row layout, UniformGrid, teal style) -> T-C1 BuildCancelRow: MAPPED
- Architecture plan Lane C -> T-C1 target state: MATCH (code identical to plan)
- OnCancel2 preservation explicitly noted in ticket. PASS

**RESULT: PASS**

### JS Pre-Check
- JS-021 (no lock): No `lock(` in any T-C1 code sample (TradeCopierPanel.cs changes or PttCancelAll.cs). PASS
- JS-033 (no async void): All handlers are synchronous `private void`. PASS
- JS-002 (no return null): All methods return void or bool. `IsWorkingOrderForInstr` returns `false` not `null`. PASS
- JS-001 (no throw in hot path): PttCancelAll.Execute has a bare `catch { }` block -- identical in structure to PttCancel.cs:89-91 which uses the same project-established pattern. Non-hot-path cancel path. PASS (consistent with established project pattern)

**RESULT: PASS**

### CYC Pre-Check
- `BuildCancelRow`: CYC=1 (straight-line construction, no branches). <= 8. PASS
- `OnCancelClick`: CYC=1 (straight-line delegation, no branches). <= 8. PASS
  - NOTE (carried from cycle 1): Orchestrator-prompt CYC budget table lists `OnCancelClick: CYC=2` but ticket correctly implements CYC=1 mirroring OnCancel2 (no null guard). Ticket correctly follows SR-C1 behavioral description. Non-blocking WARN.
- `OnCancelAllClick`: CYC=2 (`_instrument null` guard + `PttCancelAll.Execute`). <= 8. PASS
- `PttCancelAll.Execute`: CYC=5 (instr null, foreach Account.All, foreach acc.Orders, IsWorkingOrderForInstr, toCancel.Count>0). <= 8. PASS
- `PttCancelAll.IsWorkingOrderForInstr`: CYC=4 (o null, stateOk `||`, o.Instrument null, FullName match). <= 8. PASS

**RESULT: PASS**

### NT8 Check
- `Account.All` in `PttCancelAll.Execute`: UI-thread call chain (called from OnCancelAllClick event handler). PASS
- `acc.Orders` iteration: confirmed as safe by PttCancel.cs:72 precedent. PASS
- `acc.Cancel(toCancel.ToArray())`: confirmed signature via PttCancel.cs:83 -- `acc.Cancel(Order[])`. PASS
- `OrderState.Working`, `OrderState.Initialized`: enum comparisons -- thread-safe. PASS
- `NinjaTrader.Code.Output.Process`: thread-safe NT8 API. PASS
- `BrushTeal`: existing field in TradeCopierPanel -- UI thread access safe. PASS
- `System.Windows.Media.Brushes.White`: frozen system brush -- safe from UI thread. PASS
- No `async/await` in lifecycle methods. PASS
- No `Account.All` call outside of synchronous UI-thread context. PASS
- `PttCancelAll.cs` namespace `PropTraderTools` matches PttCancel.cs namespace. PASS
- No `FontFamily` usage. PASS
- No hardcoded hex colors (`#RRGGBB`). Uses named `BrushTeal` field. PASS

**RESULT: PASS**

### Test Coverage
- `OnCancelAllClick` -> T_B216_C1 verifies null-instrument guard (source inspection). PASS
- `PttCancelAll.IsWorkingOrderForInstr` -> T_B216_C2 tests null Order -> false via reflection. PASS
- `BuildCancelRow` (new): no behavioral test. CYC=1 straight-line construction -- acceptable per project convention.
- `OnCancelClick` (new): no independent test. CYC=1 delegation -- covered by AC-C2 SCAN-6 inspection. Acceptable.
- All new public/internal methods (`PttCancelAll.Execute`, `PttCancelAll.IsWorkingOrderForInstr`) have at least one [Fact] test. PASS

**RESULT: PASS**

### Scan Checklist Presence
- SCAN-1 (P0 violations grep): PRESENT. Covers BOTH TradeCopierPanel.cs AND PttCancelAll.cs (new file). PASS
- SCAN-2 (dotnet test): PRESENT. Baseline `Failed=24`, `Passed>=206`. Increments correctly from T-B1. PASS
- SCAN-3 (CYC by inspection): PRESENT. All 5 new/modified methods covered with branch counts. PASS
- SCAN-4 (ASCII-only): PRESENT. Covers BOTH TradeCopierPanel.cs AND PttCancelAll.cs. PASS
- SCAN-5 (symbol existence): PRESENT. Detailed grep commands for all new symbols across both files. PASS
- SCAN-6 (functional correctness): PRESENT. AC-C1 through AC-C6 described with verification methods. PASS
- SCAN-7 (diff hygiene): PRESENT. ptt-sync, verify_pr_hygiene, verify_critical_symbols, hunk count (>=4 expected). New file note for PttCancelAll.cs in ptt-sync. PASS

**RESULT: PASS** (all 7 scans present)

### File Routing
- `src/PropTraderTools/TradeCopierPanel.cs`: Wave workspace. PASS
- `src/PropTraderTools/Features/PttCancelAll.cs`: Wave workspace. PASS
- `src/PropTraderTools/Tests/B216Tests.cs`: Wave workspace. PASS
- PttCancel.cs NOT in scope (read-only reference). PASS

**RESULT: PASS**

### Scope Lock Constraints
- T-C1 must NOT change OnCancel2: explicitly stated ("MUST remain unchanged"). PASS
- T-C1 must NOT change PttCancel.cs: explicitly prohibited. PASS
- PttMoveTargetAll.cs not touched: explicitly prohibited (DW-B212-02 noted). PASS

**RESULT: PASS**

---

### T-C1 VERDICT: TICKET_REVIEW_PASS

---

## Cross-Ticket Coverage Check (Aggregate)

### Spec Requirements Coverage

| Requirement | Ticket | Status |
|-------------|--------|--------|
| SR-A1 (Ask null-leader log) | T-A1 | COVERED |
| SR-A2 (Ask-All Pattern B) | T-A1 | COVERED |
| SR-B1 (Bid null-leader log) | T-B1 | COVERED |
| SR-B2 (Bid-All Pattern B) | T-B1 | COVERED |
| SR-C1 (Cancel button) | T-C1 | COVERED |
| SR-C2 (Cancel All + PttCancelAll.cs) | T-C1 | COVERED |
| SR-C3 (Cancel row layout) | T-C1 | COVERED |
| DW-B216-01 Part A (Ask + Bid) | T-A1 + T-B1 | COVERED |
| DW-B216-01 Part B (Ask-All + Bid-All) | T-A1 + T-B1 | COVERED |

No uncovered spec requirements. No duplicate coverage.

### Test Coverage (Aggregate)

| Test | Ticket | Assertion |
|------|--------|-----------|
| T_B216_A1 | T-A1 | Ask null-leader log string present in source |
| T_B216_A2 | T-A1 | Account.All pattern and Ask-All log string in source |
| T_B216_B1 | T-B1 | Bid null-leader log string present in source |
| T_B216_B2 | T-B1 | Bid-All log string and HasNonFlatPosition in source |
| T_B216_C1 | T-C1 | OnCancelAllClick, PttCancelAll.Execute, _cancelRowPanel in source |
| T_B216_C2 | T-C1 | IsWorkingOrderForInstr(null, null) returns false (reflection) |

All 6 tests specified by orchestrator-prompt SCAN-2 are present across the 3 tickets. PASS

### Execution Order Contract
T-A1 -> T-B1 (HasNonFlatPosition dependency declared) -> T-C1 (independent). Correctly specified. PASS

### Pre-existing Baseline Discrepancy (WARN -- non-blocking)
- Architecture plan (02-architecture-plan.md) SCAN-2 states `Failed=9` baseline.
- All 3 tickets and the orchestrator-prompt spec state `Failed=24` baseline.
- The orchestrator-prompt is the authoritative spec. `Failed=24` is the correct baseline.
- The architecture plan SCAN-2 baseline is stale. This is a plan defect, not a ticket defect.

---

## Violations Summary

| Ticket | Check | Severity | Violation |
|--------|-------|----------|-----------|
| T-A1 (cycle 1) | Internal Consistency -- New Files | FIXED | "New Files: None" contradicted Scope Lock. Resolved in cycle 2: now correctly reads `src/PropTraderTools/Tests/B216Tests.cs (NEW -- created by T-A1. T-B1 and T-C1 append their tests to this file.)` |
| T-A1 (cycle 2) | All checks | PASS | No violations remaining |
| T-B1 | All checks | PASS | No violations |
| T-C1 | All checks | PASS | No violations |

---

## Overall: TICKET_REVIEW_PASS

**Cycle**: 2 of 2 (final).

**Basis**:
- T-A1: Cycle 1 violation (New Files contradiction) was the only failing item. The architect corrected the "New Files" section. The fix is verified: all three internal sections (New Files, xUnit Tests Required, Scope Lock) now agree that `src/PropTraderTools/Tests/B216Tests.cs` is created by T-A1 and appended to by T-B1 and T-C1. No new issues introduced.
- T-B1: Unchanged from cycle 1. All checks PASS.
- T-C1: Unchanged from cycle 1. All checks PASS.
- All 7-scan checklists present in all 3 tickets (SCAN-01 through SCAN-07 per ticket).
- All spec requirements covered. All 6 [Fact] tests specified. All file paths route to Wave workspace.

The engineer contract is unambiguous. Safe to spawn Phase 4a engineer.

---

*Reviewed by ptt-ticket-reviewer, Phase 3.5. Cycle 2 of 2. Zero violations remaining.*
