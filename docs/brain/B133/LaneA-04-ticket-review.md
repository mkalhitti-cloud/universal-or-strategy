# B133 LaneA — Ticket Review
**Phase**: 3.5 (Ticket Review)
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-08-21
**Tickets file**: `docs/brain/B133/LaneA-04-tickets.md`
**Plan file**: `docs/brain/B133/LaneA-02-architecture-plan.md` (REVIEW_PASS Cycle 2)
**Spec**: DW-B142 P0 (SignalOrNameMatches null==null false-positive)

---

## Ticket 1 — DW-B142 SignalOrNameMatches null-guard fix + B133 tests

### T-01: Spec Requirement IDs
**PASS**
Ticket L13-17 contains a dedicated table listing `DW-B142` (P0, null==null false-positive) and
`B133-TEST` (Required, 5 xUnit [Fact] regression tests). Both spec requirement IDs are present
with descriptions and priority levels.

### T-02: Exact Method Signatures
**PASS**
Ticket L34-40 states the unchanged signature:
`internal static bool SignalOrNameMatches(Order order, string? signalName, string? leaderName)`
Ticket L47-55 contains the exact BEFORE (L2512 bug) and AFTER (null-guarded) one-line diff as
required by the spec.

### T-03: Scope — One-Line Fix Only (No Scope Creep)
**PASS**
Ticket L23-28 Files Modified table lists only `CopyEngine.cs` (one character insertion at L2512)
and `B133Tests.cs` (new file, additive). Ticket L287-289 (Implementation Note 1) states: "Do not
modify any other line in CopyEngine.cs. Do not reformat, reindent, or adjust any surrounding code.
Touch only L2512." No other methods are described as modified.

### T-04: New File B133Tests.cs with Class B133LaneATests
**PASS**
Ticket L25-26 lists `src/PropTraderTools/Tests/B133Tests.cs` as CREATE action with class
`B133LaneATests`. Ticket L66-70 confirms: class `B133LaneATests`, namespace `PropTraderTools.Tests`,
framework xUnit `[Fact]` only, no NUnit, no MSTest.

### T-05: All 5 [Fact] Method Names Match Architecture Plan Exactly
**PASS**
Cross-checked against architecture plan Section 4 table. All 5 names are reproduced verbatim:
1. `SignalOrNameMatches_NullSignal_DoesNotMatchBySignal` — plan row 1 ✓
2. `SignalOrNameMatches_NullSignal_MatchesByName` — plan row 2 ✓
3. `SignalOrNameMatches_NullSignal_NoMatch_WrongName` — plan row 3 ✓
4. `SignalOrNameMatches_NonNullSignal_MatchesBySignal` — plan row 4 ✓
5. `SignalOrNameMatches_NullLeaderName_NullSignal_NoMatch` — plan row 5 ✓
No paraphrasing, no renaming.

### T-06: Each Test Has Setup, Expected, and [Fact] Tag
**PASS**
All 5 tests (ticket L93-193) include:
- `[Fact]` tag in the csharp signature block
- **Setup** subsection with explicit input values
- **Expected** value (`true` or `false`)
- **Rationale** explaining branch traversal
Pattern is consistent across all 5 tests.

### T-07: Regression Requirement (B132x5, B131x7, B130x8, B129x13)
**PASS**
Ticket L197-210 contains a Regression Requirement table with all four prior suites:
B131(7), B132(5), B130(8), B129(13) — exact counts match spec.
Ticket L325-328 (Completion Criteria) restates: "All 28 prior tests (B129x13, B130x8, B131x7,
B132x5) continue to pass (0 regressions)."

### T-08: JS Rule Constraints per Touched Method
**PASS**
Ticket L214-228 contains a JS Rule Constraints table covering:
JS-021 (no lock), JS-001 (no throw in hot paths), JS-002 (no return null), JS-033 (no async void),
JS-036 (no new byte[]), JS-037 (no new T[] without ArrayPool), CYC, ASCII, DateTime, Order naming.
Each rule is addressed with N/A rationale specific to this one-line fix. Coverage exceeds the
minimum four rules required by the spec check.

### T-09: 7-Scan Checklist Present with All 7 Scans and Exact Commands
**PASS**
Ticket L231-282 contains all 7 scans with checkbox notation, exact commands, and required results:
- SCAN-01: `grep -r "lock(" src/ --include="*.cs"` — required: 0 results ✓
- SCAN-02: `grep -rn "async void " src/ --include="*.cs"` — required: 0 results ✓
- SCAN-03: `grep -rn "return null;" src/ --include="*.cs"` — required: 0 new in touched files ✓
- SCAN-04: `grep -rn "throw new" src/ --include="*.cs"` — required: 0 new in touched files ✓
- SCAN-05: `python scripts/complexity_audit.py` — required: 0 methods > CYC 8 ✓
- SCAN-06: `Select-String -Path "..." -Pattern "[^\x00-\x7F]"` + bash alternative ✓
- SCAN-07: `dotnet build src/PropTraderTools/PropTraderTools.csproj` — required: 0 errors/warnings ✓
All 7 scans present. Checklist is the engineer's contract. Defense-in-depth Layer 1 satisfied.

### T-10: CYC<=8 Pre-Check — SignalOrNameMatches Stays CYC=3
**PASS**
Ticket L60-61 states: "CYC impact: None. The null-guard is a short-circuit within the same boolean
expression, not a new branch node in the control-flow graph. SignalOrNameMatches CYC remains 3."
SCAN-05 (L265) required result: "0 methods > CYC 8 in CopyEngine.cs and B133Tests.cs."
Test methods are CYC=1 (no branching). No method described in the ticket is at risk of exceeding 8.

### T-11: NT8 Constraints — Order Mock Pattern (No Moq)
**PASS**
Ticket L74-87 (StubOrder Helper section) explicitly states: "Do NOT use Moq or any mocking
framework." Ticket L294-297 (Implementation Note 3) reiterates: "Do NOT use Moq or any mocking
framework." Direct NT8 `NinjaTrader.Cbi.Order` instantiation via `StubOrder()` is the documented
pattern, consistent with B131Tests.cs and B132Tests.cs. NT8 Order constructor pattern is
empirically validated by existing CI suite.

### T-12: ASCII-Only Confirmation for New Identifiers in B133Tests.cs
**PASS**
Ticket L225 (JS Rule Constraints table, ASCII row): "All new identifiers and string literals in
`B133Tests.cs` are ASCII-only." Ticket L305-306 (Implementation Note 6): "ASCII-only. No
underscores in class name (`B133LaneATests` not `B133_LaneA_Tests`)." SCAN-06 provides post-
implementation verification of this constraint.

### T-13 (BONUS): No Extra Scope — FindFollowerBracketOrder and SyncFollowerBracket Untouched
**PASS**
Ticket L23-28 Files Modified table: only `CopyEngine.cs` L2512 (one character insertion) and
`B133Tests.cs` (new file). `FindFollowerBracketOrder` and `SyncFollowerBracket` do not appear in
any Files Modified entry. Implementation Note 1 (L287) mandates "Touch only L2512." No scope
creep to any other method is described anywhere in the ticket.

---

### Mandatory PTT Checks (role-definition compliance)

**Concurrency Violations (JS-021/023/025)**: PASS — No `lock()`, no unguarded `Dictionary<K,V>`
for shared state, no UI-thread Dispatcher pattern described in any ticket section.

**Type Safety Violations (JS-001/002/003)**: PASS — No `throw new XxxException` in hot path, no
`return null` (method returns `bool`), no empty-string sentinel for mode/state.

**Immutability Violations (JS-008/009)**: PASS — No mutable struct fields, no SolidColorBrush
without Freeze(), no `Dictionary<K,V>` on field definitions.

**NT8 Hard Constraints**: PASS — No `async/await` in lifecycle method, no `Account.All` outside
Loaded handler, no `sealed` on TradeCopierWindow, no FontFamily WPF element, no hardcoded hex
color, no `CreateOrder` without `"PTT-"` prefix (N/A — no CreateOrder call), no `DateTime.Now`.

**CYC > 8 risk**: PASS — All described methods are CYC=3 (production fix) or CYC=1 (test methods).
No method is at risk of exceeding CYC=8.

**Traceability**: PASS — Every ticket item (one-line fix, 5 tests, regression suite, scan
checklist) maps to DW-B142 or a plan section. No phantom work (items in ticket not in plan). No
missing work (all plan sections are represented in the ticket).

**Spec Coverage**: PASS — DW-B142 and B133-TEST are the only in-scope requirements; both are
covered in Ticket 1. No uncovered requirements. No duplicate coverage.

**Test Coverage**: PASS — The single production change (one-line guard) does not introduce a new
method. All 5 new public/internal test methods have explicit `[Fact]` signatures specified. The
`SignalOrNameMatchesTestable` accessor is existing; referenced correctly (no new accessor created).

**File Routing**: PASS — All `.cs` paths reference `src/PropTraderTools/` (Wave workspace
`c:\WSGTA\universal-or-strategy`). No Director workspace path appears for any `.cs` file.

**Scan Checklist Defense-in-Depth**: PASS — All 7 scans (SCAN-01 through SCAN-07) present in the
ticket. Per-ticket scan checklist is Layer 1 of the 3-layer defense-in-depth (ticket contract,
engineer attestation, verifier cross-check). Absence of any scan would break the engineer contract
and the verifier anchor. All 7 are present — contract is intact.

---

## Per-Check Summary

| Check | Result |
|-------|--------|
| T-01: Spec Req IDs (DW-B142, B133-TEST) | PASS |
| T-02: Exact method signatures before/after | PASS |
| T-03: Fix is ONLY L2512 one-line null-guard | PASS |
| T-04: B133Tests.cs + class B133LaneATests | PASS |
| T-05: All 5 [Fact] names match plan exactly | PASS |
| T-06: Each test has Setup + Expected + [Fact] | PASS |
| T-07: Regression B132x5+B131x7+B130x8+B129x13 | PASS |
| T-08: JS rule constraints per touched method | PASS |
| T-09: 7-scan checklist — all 7 present + commands | PASS |
| T-10: CYC<=8 — SignalOrNameMatches stays CYC=3 | PASS |
| T-11: NT8 Order mock pattern — no Moq | PASS |
| T-12: ASCII-only for B133Tests.cs identifiers | PASS |
| T-13: No scope creep to FindFollowerBracketOrder / SyncFollowerBracket | PASS |

---

## Overall: TICKET_REVIEW_PASS

All 13 checks pass. No JS rule violations. No NT8 constraint violations. No scope creep.
No missing [Fact] tests. 7-scan checklist present and complete (all 7 scans, exact commands).
Engineer contract is intact. Verifier anchor is established.

**The orchestrator may spawn the Phase 4a engineer.**

---

*Review written by ptt-ticket-reviewer. No violations found. Zero cycles to architect.*
