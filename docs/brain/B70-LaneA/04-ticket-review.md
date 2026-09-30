# B70-LaneA Ticket Review

**Block**: B70-LaneA
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Input**: docs/brain/B70-LaneA/04-tickets.md
**Architecture Plan**: docs/brain/B70-LaneA/02-architecture-plan.md
**Rules Reference**: docs/standards/jane-street/RULES_CATALOG.md
**Date**: 2026-08-14
**Source files verified**:
- `src/PropTraderTools/CopyEngine.cs` lines 428-450, 515-525
- `src/PropTraderTools/Features/PttQuickExit.cs` lines 33-90

---

## Full Checklist Table

| Item | Check | Status | Notes |
|------|-------|--------|-------|
| TR-01 | T-B70-01 carries spec requirement ID DW-B70-01 | **PASS** | Ticket header and "Spec Requirement Satisfied" section both reference DW-B70-01 explicitly. |
| TR-02 | T-B70-02 carries spec requirement ID DW-B70-02 | **PASS** | Ticket header and "Spec Requirement Satisfied" section both reference DW-B70-02 explicitly. |
| TR-03 | T-B70-01 BEFORE at line 520 matches actual source `_qxOcoSeq = 0` | **PASS** | Actual source line 520: `private int _qxOcoSeq = 0;` — exact match to ticket BEFORE block. |
| TR-04 | T-B70-02 Part A BEFORE matches actual IsQxCancelCandidate (lines 439-446) | **PASS** | Method body lines 439-446 match exactly. MINOR: ticket header says "EXACT BEFORE (lines 439-446)" but the displayed block includes the 4-line comment header at lines 435-438. Line label is off by 4; method body content is correct. |
| TR-05 | T-B70-02 Part B BEFORE matches actual PttQuickExit.Execute line 52 | **PASS** | Actual line 51-52: `// Step 3: CancelStaleBrackets -- cancel ATM bracket + previous PTT-QX orders` / `CopyEngine.Instance?.CancelQxBrackets(leader, instr);` — exact match to ticket BEFORE block. |
| CO-01 | 7-scan checklist (SCAN-01 through SCAN-07) present in EVERY ticket | **PASS** | Both T-B70-01 and T-B70-02 each contain SCAN-01, SCAN-02, SCAN-03, SCAN-04, SCAN-05, SCAN-06, SCAN-07. MINOR: scan labels are inconsistent between tickets (T1: SCAN-01=ASCII; T2: SCAN-01=lock). All 7 numbered scans are present in each ticket. |
| CO-02 | SCAN-06 (dotnet build) included with file path in both tickets | **PASS** | T1 SCAN-06: `dotnet build src/PropTraderTools/PropTraderTools.csproj`. T2 SCAN-06: same command. File path explicit in both. |
| CO-03 | SCAN-07 includes all required test names for each ticket | **PASS** | T1 SCAN-07: T_B70_01, T_B70_02, T_B70_03 listed in filter. T2 SCAN-07: T_B70_04, T_B70_05, T_B70_06, T_B70_07, T_B70_08 listed. All 8 tests covered across both SCAN-07 entries. |
| CO-04 | All 8 tests T_B70_01..T_B70_08 present across the 2 tickets | **PASS** | T1 carries T_B70_01, T_B70_02, T_B70_03. T2 carries T_B70_04, T_B70_05, T_B70_06, T_B70_07, T_B70_08. Full set confirmed. |
| CO-05 | NT8-VERIFY-01 and NT8-VERIFY-02 present in T-B70-01 | **PASS** | Both NT8-VERIFY-01 and NT8-VERIFY-02 appear in the T-B70-01 7-scan checklist with explicit commands and verdicts. |
| JS-01 | No `lock()` in any proposed change | **PASS** | T1: field initializer only — no lock. NextQxOcoId uses Interlocked.Increment (unchanged). T2: IsQxCancelCandidate is a static pure predicate — no lock. PttQuickExit.Execute addition is a single `?.` method call — no lock. JS-021 PASS. |
| JS-02 | No `throw new Exception()` in any proposed change | **PASS** | No throw statement in the field initializer change, IsQxCancelCandidate addition, or Execute Step 3 addition. JS-001 PASS. |
| JS-03 | No `return null` from changed methods | **PASS** | NextQxOcoId returns `string` (non-null expression body). IsQxCancelCandidate returns `bool` (never null). Execute is `void`. JS-002 PASS. |
| JS-04 | No `async void` in any proposed change | **PASS** | All changed methods are synchronous. No async void. JS-033 PASS. |
| JS-05 | All new string literals ASCII-only | **PASS** | `"Environment.TickCount & 0x7FFF"` — no string literal, arithmetic expression on int. `"PTT-Copy"` — all chars ASCII (0x50 0x54 0x54 0x2D 0x43 0x6F 0x70 0x79). Comment tokens `"B70 DW-B70-02"` — ASCII-only. PASS. |
| CY-01 | T-B70-01 `_qxOcoSeq` field init: CYC unchanged (still 1 for NextQxOcoId) | **PASS** | NextQxOcoId is an expression body (CYC=1). Field initializer has no CYC. Method body explicitly marked UNCHANGED. PASS. |
| CY-02 | T-B70-02 IsQxCancelCandidate: CYC 5→6, <=8 | **PASS** | Plan Section 3 confirms CYC=5 before; new branch (5) adds +1 decision point. CYC=6 <=8 limit. PASS. |
| CY-03 | T-B70-02 PttQuickExit.Execute: CYC after = 6 (new `?.` = +1), still <=8 | **PASS** | Ticket explicitly states "?.  null-conditional operator on the new call counts as +1 McCabe decision point (Roslyn strict). Current Execute CYC=5. New Execute CYC=6." CYC=6 <=8. PASS. |
| NT-01 | T-B70-01 confirms "PTT-QX-" prefix preserved in NextQxOcoId output | **PASS** | NT8-VERIFY-01: "NextQxOcoId() output starts with 'PTT-QX-'. Method body unchanged; prefix literal unchanged." PASS. |
| NT-02 | T-B70-01 notes Environment.TickCount & 0x7FFF max = 32767, D5 format OK | **PASS** | NT8-VERIFY-02 states "0x7FFF = 32767 decimal. 32767 in D5 format = '32767' (5 characters, valid D5 column). _qxOcoSeq is int (not uint/long) -- no sign issue after masking." PASS. |
| NT-03 | T-B70-02 confirms CancelQxBracketsForFollowers signature is (Instrument instr) | **PASS** | Ticket states: "Expected: internal void CancelQxBracketsForFollowers(Instrument instr)" and cites B68Tests.cs T_B68_01 + PttGlobalQuickExit.cs line 38 as evidence. Engineer directed to confirm at CopyEngine.cs ~line 505. PASS. |
| TC-01 | T_B70_01 tests distinctness (not just non-null) | **PASS** | `Assert.NotEqual(id1, id2)` — tests that two sequential calls return different strings, not merely non-null. PASS. |
| TC-02 | T_B70_03 uses HashSet to prove 100 unique values | **PASS** | `new System.Collections.Generic.HashSet<string>()` — adds 100 IDs; asserts `Assert.Equal(100, ids.Count)`. HashSet.Count < 100 would expose any collision. PASS. |
| TC-03 | T_B70_04 AND T_B70_05 test PTT-Copy prefix (exact match AND prefix variant) | **PASS** | T_B70_04: `"PTT-Copy"` exact signal name (branch 5 fires for base name). T_B70_05: `"PTT-Copy-Variant"` suffix variant (StartsWith semantics confirmed). PASS. |
| TC-04 | T_B70_06 and T_B70_07 are regression guards for existing branches | **PASS** | T_B70_06: `"PTT-QX-Stop"` guards branch (3) (PTT-QX- prefix). T_B70_07: `"Stop1"` guards branch (2) (ATM bracket via IsAtmBracketName). Both explicitly labeled as regression guards. PASS. |
| TC-05 | T_B70_08 tests a true negative ("Entry" returns false) | **PASS** | `Assert.False(CopyEngine.IsQxCancelCandidate(order), ...)` for `"Entry"` — verifies that none of the 5 branches fires for a plain non-bracket name. PASS. |
| DD-01 | SCAN-01 (lock) in T-B70-01 | **PASS** | Lock scan is present in T-B70-01 at SCAN-02 ("No lock() in changed regions"). MINOR: T-B70-01's SCAN-01 is "ASCII-only" while T-B70-02's SCAN-01 is "No lock()" — scan label assignments are inconsistent between tickets. All 7 scans present; lock scan covered. |
| DD-02 | SCAN-01 (lock) in T-B70-02 | **PASS** | T-B70-02 SCAN-01: "No lock() in changed regions" — two grep commands covering both CopyEngine.cs and PttQuickExit.cs. PASS. |
| DD-03 | SCAN-04 (CYC) present in both tickets with specific expected values | **PASS** | T1 SCAN-04: "NextQxOcoId CYC=1. No regression from baseline." T2 SCAN-04: "IsQxCancelCandidate CYC=6 (was 5; +1 for new branch). Execute CYC=6 (was 5; +1 for ?.)." Specific values in both. PASS. |
| DD-04 | SCAN-05 (ASCII) present in both tickets | **PASS** | T1 SCAN-05: grep -P non-ASCII on CopyEngine.cs changed lines. T2 SCAN-05: git diff grep on both CopyEngine.cs and PttQuickExit.cs new lines. Both present. PASS. |
| DD-05 | SCAN-06 (build) present in both tickets | **PASS** | Both tickets: `dotnet build src/PropTraderTools/PropTraderTools.csproj`. PASS. |
| DD-06 | SCAN-07 (test) present in both tickets with test name list | **PASS** | T1 SCAN-07 lists T_B70_01/02/03. T2 SCAN-07 lists T_B70_04/05/06/07/08. Both include test name filter strings. PASS. |

---

## T-B70-01 Verdict

| Check | Result |
|-------|--------|
| Traceability | PASS |
| JS Pre-Check (JS-001/002/021/033) | PASS |
| CYC Pre-Check | PASS |
| NT8 Constraints | PASS |
| Test Coverage | PASS |
| Scan Checklist (SCAN-01..07 present) | PASS |
| File Routing | PASS |
| **TICKET VERDICT** | **TICKET_REVIEW_PASS** |

---

## T-B70-02 Verdict

| Check | Result |
|-------|--------|
| Traceability | PASS |
| JS Pre-Check (JS-001/002/021/033) | PASS |
| CYC Pre-Check | PASS |
| NT8 Constraints | PASS |
| Test Coverage | PASS |
| Scan Checklist (SCAN-01..07 present) | PASS |
| File Routing | PASS |
| **TICKET VERDICT** | **TICKET_REVIEW_PASS** |

---

## MINOR Items (Non-Blocking)

These are cosmetic/labeling issues. None block the engineer.

### MINOR-01 — Test file path inconsistency between plan and tickets

| Item | Value |
|------|-------|
| Architecture plan path | `tests/PropTraderTools.Tests/CopyEngineB70Tests.cs` |
| Ticket path | `src/PropTraderTools/Tests/B70Tests.cs` |
| File name in plan | `CopyEngineB70Tests.cs` |
| File name in ticket | `B70Tests.cs` |

**Assessment**: The architecture plan uses a "conceptual" tests directory. The ticket uses the
actual project convention (`src/PropTraderTools/Tests/`), consistent with the existing
`B68Tests.cs` location. The ticket path is factually correct. Class name (`CopyEngineB70Tests`)
matches between plan and ticket. **Non-blocking** — engineer should follow ticket path.

### MINOR-02 — Inconsistent SCAN-01 label assignment between tickets

| Ticket | SCAN-01 Content | SCAN-02 Content |
|--------|-----------------|-----------------|
| T-B70-01 | ASCII-only | No lock() |
| T-B70-02 | No lock() | No throw new |

The scan numbering scheme shifts between tickets. All 7 numbered scans are present in each
ticket; no scan is missing. The content of each scan is substantively correct. **Non-blocking.**

### MINOR-03 — TR-04 line range label

T-B70-02 Change A header reads "EXACT BEFORE (lines 439-446)" but the displayed block starts
with 4 comment lines that correspond to source lines 435-438 (method signature is at 439).
The BEFORE code block is correct and complete. The "439-446" label is off by 4 lines for the
block start. **Non-blocking** — the actual method body content is exact.

---

## Violations Summary

**Zero blocking violations found.**

All items pass. Three MINOR cosmetic/labeling notes recorded above (test file path
discrepancy, scan label numbering shift, and line range label). None trigger TICKET_REVIEW_FAIL
under the defined verdict rules.

---

## Overall Verdict

**TICKET_REVIEW_PASS**
