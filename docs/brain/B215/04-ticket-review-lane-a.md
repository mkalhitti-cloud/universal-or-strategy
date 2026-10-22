# B215 Lane A: Ticket Review
## Block: B215-LaneA -- Test-Debt Remediation (DW-B214-02)
## Date: 2026
## Produced by: ptt-ticket-reviewer (Phase 3.5)
## Input: docs/brain/B215/04-tickets-lane-a.md (TICKETS_COMPLETE -- corrected pass 2)
## Plan: docs/brain/B215/02-architecture-plan-lane-a.md (REVIEW_PASS)
## Pass: 2 (second pass -- Check 5 re-run after architect correction)

---

## Ticket Review: B215-LaneA

---

### TA1 -- B153IsPttQxTargetTests (genuine regression -- add IsPttQxTarget to CopyEngine.cs)

**Traceability:** PASS
Plan Section 1/TA1 + Section 3/TA1 confirm B153IsPttQxTargetTests as a genuine regression (method absent from CopyEngine.cs).
DW-B215-LA-01 cited in both plan and ticket. Spec requirements DW-B214-02 + DW-B153-04 present.

**JS Pre-Check:** PASS
- JS-021: method is `private static`, no shared state, no `lock()`. PASS.
- JS-001: all paths return `false` or `true`. No `throw` anywhere in the method body. PASS.
- JS-002: bool return type. No `return null`. PASS.
- JS-008/009: no struct, no SolidColorBrush, no Dictionary<K,V>. N/A.

**CYC Pre-Check:** PASS
Method body has 3 `if` guards (decision points). The terminal `return n >= 1` is a boolean expression, not a branch.
CYC = 1 + 3 = 4. Ticket explicitly documents CYC=4. Limit is 8. PASS.

**NT8 Check:** PASS
`powershell -File .\scripts\ptt-sync.ps1` is now present as the first command in Post-Edit Verification Commands (line 96-97: `powershell -File .\scripts\ptt-sync.ps1  -- must show 21/0`). Hard-Link Integrity mandate satisfied.
No other NT8 anti-patterns (async/await lifecycle, Account.All, sealed TradeCopierWindow, FontFamily, hex color, DateTime.Now) are present. PASS.

**Test Coverage:** PASS
All 6 test methods are enumerated with inputs and expected return values. Reflection pattern (`GetMethod` + `Invoke`) is correctly described. Each method maps to a specific assertion.

**Scan Checklist:** PASS
SCAN-01 through SCAN-07 are all present with individual command/status entries. PASS.

**File Routing:** PASS
`src/PropTraderTools/CopyEngine.cs` -- Wave workspace. PASS.

**VERDICT: TICKET_REVIEW_PASS**

---

### TA2 -- B155ContractTests (stale assertion -- reflection method name updates)

**Traceability:** PASS
Plan Section 1/TA2 + Section 3/TA2 confirm B155ContractTests (2 failures) with stale reflection names.
`SubmitDrainedEntryTestable` does not exist (actual: `SubmitDrainedEntry` at CopyEngine.cs:7905).
`IsLeaderFlatDrainAbort` renamed to `TryAbortLeaderFlatDrains` at CopyEngine.cs:1928. Spec: DW-B214-02.

**JS Pre-Check:** PASS
Test-only changes. No production code patterns introduced. No `lock()`, no `throw`, no `return null`,
no mutable shared state. PASS.

**CYC Pre-Check:** PASS
No production code changed. No new methods with CYC risk. N/A.

**NT8 Check:** PASS
`powershell -File .\scripts\ptt-sync.ps1` is now present as the first command in Post-Edit Verification Commands (line 230-231: `powershell -File .\scripts\ptt-sync.ps1  -- must show 21/0`). Hard-Link Integrity mandate satisfied.
No other NT8 anti-patterns are present. PASS.

**Test Coverage:** PASS
Both failing tests enumerated: `SubmitDrainedEntry_ShouldAbort_WhenAccountIsFlat` and
`TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString`. Exact old-to-new string substitutions are specified with line numbers.

**Scan Checklist:** PASS
All 7 scans present as a blanket "SCAN-01..07 PASS -- test-only change, no production code touched, no lock, no throw, ASCII-only" statement. Accepted form for test-only tickets. PASS.

**File Routing:** PASS
`src/PropTraderTools/CopyEngineTests.cs` -- Wave workspace. PASS.

**VERDICT: TICKET_REVIEW_PASS**

---

### TA3 -- DwB15701SlateReadyTests (stale assertion -- Skip + string updates)

**Traceability:** PASS
Plan Section 1/TA3 + Section 3/TA3 confirm DwB15701SlateReadyTests (2 failures). Test 1: CopyEngine.Instance
triggers Agile.NET cctor outside NT8 host. Test 2: `HandleDrainTerminalState` never existed; arg stale
`(e.Order)` vs actual `(e)`. Spec: DW-B214-02 (DwB157 subset).

**JS Pre-Check:** PASS
Test-only changes. All new string literals are ASCII. No `lock()`, no `throw`, no `return null`. PASS.

**CYC Pre-Check:** PASS
No production code changed. N/A.

**NT8 Check:** PASS
`powershell -File .\scripts\ptt-sync.ps1` is now present as the first command in Post-Edit Verification Commands (line 362-363: `powershell -File .\scripts\ptt-sync.ps1  -- must show 21/0`). Hard-Link Integrity mandate satisfied.
No other NT8 anti-patterns present (the `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]`
pattern is the CORRECT way to handle CopyEngine.Instance tests outside NT8 host). PASS.

**Test Coverage:** PASS
Both failing tests covered with precise fix instructions (line numbers, old/new code blocks, rationale for
assertion direction reversal: `slatePos < handlePos`).

**Scan Checklist:** PASS
All 7 scans individually listed. SCAN-04 explicitly verifies ASCII for all changed strings. PASS.

**File Routing:** PASS
`src/PropTraderTools/CopyEngineTests.cs` -- Wave workspace. PASS.

**VERDICT: TICKET_REVIEW_PASS**

---

### TA4 -- DwB168_02Tests + B196Tests (DW-card, gitignored)

**Traceability:** PASS
Covers DwB168_02Tests (4 failures) + B196Tests DrainThenDispatch (2 failures) + FindLeaderStopPrice (2 failures).
Plan Section 1/TA4 + TA8 (merged into TA4) confirm all 8 failures. DW-B215-LA-04 cited in plan and ticket. PASS.

**JS Pre-Check:** PASS (DW-card -- no agent code)
DW-card descriptions correctly identify `CopyEngine.Instance` Agile.NET cctor as root cause.
`[Fact(Skip)]` pattern and `[CallerFilePath]` path resolution helper are correct approaches.
No JS rule violations in the described manual changes. PASS.

**CYC Pre-Check:** PASS (N/A -- DW-card)
No agent-executed code changes. The `ResolveCopyEnginePath` helper described in the DW-card has CYC=1. N/A.

**NT8 Check:** PASS
Ticket is clearly marked "NO AGENT EXECUTION -- DW-CARD FOR DIRECTOR MANUAL ACTION" at the top.
File is marked "GITIGNORED. Agent MUST NOT write this file." Agent is not instructed to execute ptt-sync
(not applicable for DW-card tickets). PASS.

**Test Coverage:** PASS (DW-card)
All 8 failures documented with specific test method names and required actions.
Fix 1: 6 tests with `[Fact(Skip)]`. Fix 2: 2 FindLeaderStopPrice tests with `[CallerFilePath]` helper. PASS.

**Scan Checklist:** PASS
"7-SCAN: N/A -- DW-card ticket only, no agent execution. No source or test files are modified by this ticket."
Accepted form for DW-card/manual-only tickets. PASS.

**File Routing:** PASS (DW-card)
File is `src/PropTraderTools/Tests/B196Tests.cs` explicitly marked GITIGNORED. No agent routing to Director workspace. PASS.

**VERDICT: TICKET_REVIEW_PASS**

---

### TA5 -- LaneFTests T_B188 subset (stale assertion -- needle string updates)

**Traceability:** PASS
Plan Section 1/TA5 + Section 3/TA5 confirm LaneFTests T_B188 (3 failures). B191-DIRECT refactor renamed
`forcedTargets` → `perAccTargets` and introduced `effectiveTargets` in PttGlobalQuickExit.cs. Evidence
includes exact line references (lines 181-182, 95-96). Spec: DW-B214-02 (LaneF/B188 subset).

**JS Pre-Check:** PASS
Test-only changes. New needle strings `"perAccTargets"`, `"effectiveTargets,"` are ASCII. No production code patterns. PASS.

**CYC Pre-Check:** PASS
No production code changed. N/A.

**NT8 Check:** PASS
`powershell -File .\scripts\ptt-sync.ps1` is now present as the first command in Post-Edit Verification Commands (line 531-532: `powershell -File .\scripts\ptt-sync.ps1  -- must show 21/0`). Hard-Link Integrity mandate satisfied.
No other NT8 anti-patterns present. PASS.

**Test Coverage:** PASS
All 3 failing tests covered. T_B188_02 needle update rationale (multi-line call, match first line) is correctly documented. Error messages also updated to remain consistent with new needle content.

**Scan Checklist:** PASS
All 7 scans individually listed. Blanket PASS with ASCII verification on new strings. PASS.

**File Routing:** PASS
`src/PropTraderTools/CopyEngineTests.cs` -- Wave workspace. PASS.

**VERDICT: TICKET_REVIEW_PASS**

---

### TA6 -- B189T4SpinnerTests (stale assertion -- Skip + IL-to-source rewrite)

**Traceability:** PASS
Plan Section 1/TA6 + Section 3/TA6 confirm B189T4SpinnerTests (2 failures). T_03: CopyEngine.Instance cctor.
T_05: `_instrQAll2tBtn` accessed inside `Dispatcher.InvokeAsync` lambda, MetadataToken absent from outer method IL
(TradeCopierPanel.cs:1054-1063 confirmed). Spec: DW-B214-02 (B189 subset).

**JS Pre-Check:** PASS
New `GetPanelSourcePath([CallerFilePath])` helper: CYC=1, no `lock()`, no `throw`, no `return null`.
Replacement test body: `Assert.Contains` (xUnit assertion), no explicit `throw new XxxException`. PASS.
SCAN-02 note is correctly qualified: "Assert.True/Contains throw on failure (xUnit contract)" is acceptable -- those are
xUnit framework methods, not `throw new XxxException` anti-patterns per JS-001. PASS.

**CYC Pre-Check:** PASS
No production code changed. New test helper `GetPanelSourcePath` has CYC=1 (single path, `?? string.Empty` is an
operator not a branch in CYC counting). PASS.

**NT8 Check:** PASS
`powershell -File .\scripts\ptt-sync.ps1` is now present as the first command in Post-Edit Verification Commands (line 680-681: `powershell -File .\scripts\ptt-sync.ps1  -- must show 21/0`). Hard-Link Integrity mandate satisfied.
No other NT8 anti-patterns present. The `GetPanelSourcePath` helper correctly uses
`System.IO.Path.Combine(dir, "TradeCopierPanel.cs")` (same directory) -- the "Corrected helper" block clarifies this. PASS.

**Test Coverage:** PASS
Both failing tests covered with precise instructions. T_03: single attribute change with before/after. T_05: two-step
(add helper + replace test body) with full code blocks. PASS.

**Scan Checklist:** PASS
All 7 scans individually listed. SCAN-01 includes specific grep command targeting new code. PASS.

**File Routing:** PASS
`src/PropTraderTools/CopyEngineTests.cs` -- Wave workspace. PASS.

**VERDICT: TICKET_REVIEW_PASS**

---

### TA7 -- B195Tests (DW-card, gitignored)

**Traceability:** PASS
Plan Section 1/TA7 + Section 3/TA7 confirm B195Tests (1 failure). CopyEngine.Instance cctor failure outside NT8 host.
File gitignored. DW-B215-LA-07 cited in plan and ticket. Spec: DW-B214-02 (B195 subset).

**JS Pre-Check:** PASS (DW-card -- no agent code)
DW-card description is a single `[Fact(Skip)]` attribute addition. No JS rule violations. PASS.

**CYC Pre-Check:** PASS (N/A -- DW-card)

**NT8 Check:** PASS
Ticket clearly marked "NO AGENT EXECUTION -- DW-CARD FOR DIRECTOR MANUAL ACTION".
File marked "GITIGNORED. Agent MUST NOT write this file." Agent is not instructed to execute ptt-sync. PASS.

**Test Coverage:** PASS (DW-card)
Single failing test documented with exact old attribute (`[Fact]`) and new attribute
(`[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]`). PASS.

**Scan Checklist:** PASS
"7-SCAN: N/A -- DW-card ticket only, no agent execution. No source or test files are modified by this ticket."
Accepted form for DW-card/manual-only tickets. PASS.

**File Routing:** PASS (DW-card)
`src/PropTraderTools/Tests/B195Tests.cs` explicitly marked GITIGNORED. No agent routing to Director workspace. PASS.

**VERDICT: TICKET_REVIEW_PASS**

---

## Aggregate Checks

### Spec Coverage (Checklist Item 7)

All 24 diagnosed failures from the architecture plan are accounted for:

| Ticket | Test Class | Failures | Disposition | Covered? |
|--------|-----------|----------|------------|----------|
| TA1 | B153IsPttQxTargetTests | 6 | Agent: add IsPttQxTarget to CopyEngine.cs | YES |
| TA2 | B155ContractTests | 2 | Agent: fix stale reflection names | YES |
| TA3 | DwB15701SlateReadyTests | 2 | Agent: Skip + string update | YES |
| TA4 | DwB168_02Tests (4) + B196Tests (4) | 8 | DW-B215-LA-04 (manual) | YES |
| TA5 | LaneFTests T_B188 | 3 | Agent: needle update | YES |
| TA6 | B189T4SpinnerTests | 2 | Agent: Skip + IL-to-source | YES |
| TA7 | B195Tests | 1 | DW-B215-LA-07 (manual) | YES |
| **Total** | | **24** | 15 agent + 9 DW-card | **PASS** |

Spec Coverage Check: **PASS**

---

### Violation Summary (Pass 2)

No violations. All previously flagged violations (V1-V5: ptt-sync missing from TA1/TA2/TA3/TA5/TA6) have been resolved.

---

## Overall: TICKET_REVIEW_PASS

Checklist results:
- Check 1 (Traceability): **PASS** -- all 7 tickets trace to diagnosed classes in the architecture plan
- Check 2 (Scope for agent-executable tickets): **PASS** -- TA1 touches only CopyEngine.cs; TA2/TA3/TA5/TA6 touch only CopyEngineTests.cs
- Check 3 (CYC for TA1): **PASS** -- CYC=4, 3 decision points, well under limit of 8
- Check 4 (DW-card tickets TA4, TA7): **PASS** -- both marked NO AGENT EXECUTION; neither instructs engineer to write gitignored files
- Check 5 (ptt-sync + dotnet test verification): **PASS** -- `powershell -File .\scripts\ptt-sync.ps1` now present as first command in Post-Edit Verification Commands for TA1, TA2, TA3, TA5, and TA6
- Check 6 (7-scan checklists): **PASS** -- all 7 tickets have 7-scan coverage in the appropriate form
- Check 7 (Test coverage -- all 24 failures): **PASS** -- all 24 failures covered; 15 agent + 9 DW-card

**Return: TICKET_REVIEW_PASS**

---

*Ticket review produced by ptt-ticket-reviewer (Phase 3.5).*
*B215 Lane A: TICKET_REVIEW_PASS -- Pass 2 (corrected). Zero violations. Phase 4a (ptt-engineer) is unlocked.*
