# B187 Ticket Review
## ptt-ticket-reviewer Phase 3.5

**Date**: 2026-07-08
**Reviewer**: ptt-ticket-reviewer
**Epic**: B187 -- QX Stop Snapshot Fix + DIAG Cleanup + CS0067 Suppress
**Tickets file**: `docs/brain/B187/04-tickets.md`
**Plan file**: `docs/brain/B187/02-architecture-plan.md` (REVIEW_PASS)
**Source verified**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`, `src/PropTraderTools/CopyEngine.cs`

---

## Source Verification Results (pre-review)

These findings anchor every traceability and completeness check below.

| Item | Source Evidence | Line(s) | Result |
|------|----------------|---------|--------|
| Bug: `Execute(Instrument)` has `leaderStop` AFTER `CancelPttBeOrders` | Line 56 = `CancelPttBeOrders`, line 61 = `PttQuickExit.SnapshotStopPrice` | 56, 61 | BUG CONFIRMED |
| Bug: `Execute(List,Instrument)` has `leaderStop` AFTER `CancelPttBeOrders` | Line 140 = `CancelPttBeOrders`, line 142 = `PttQuickExit.SnapshotStopPrice` | 140, 142 | BUG CONFIRMED |
| `SnapshotTargetOrders` is AFTER `CancelPttBeOrders` in `Execute(Instrument)` | Line 59 = `SnapshotTargetOrders`, line 56 = `CancelPttBeOrders` | 56, 59 | DW-B126 currently maintained |
| `Execute(List,Instrument)` has NO `SnapshotTargetOrders` | Entire method lines 113-179 reviewed -- absent | -- | CONFIRMED |
| `LogLeaderDiag` call + 2-line comment in `Execute(Instrument)` | Lines 76-78 confirmed | 76-78 | DIAG CONFIRMED, T2 needed |
| DIAG block in `ExecuteFollowers` (`_fPosQty`, foreach, StringBuilder loop) | Lines 208-245 confirmed | 208-245 | DIAG CONFIRMED, T2 needed |
| `LogLeaderDiag` private static method | Lines 490-519 confirmed (`<summary>` at 490, closing `}` at 519) | 490-519 | METHOD CONFIRMED, T2 needed |
| `PendingBeArmed` event at line 423 lacks `#pragma warning disable CS0067` | Line 423 confirmed -- no pragma wrapper | 423 | PRAGMA ABSENT, T3 needed |
| `PendingBeFired` event at line 421 present for comparison | Adjacent event has no pragma (correct) | 421 | OK |

---

## T1 -- DW-B187-QX-STOP-SNAP-01: leaderStop snapshot order fix [P1]

### Traceability

| Check | Detail | Result |
|-------|--------|--------|
| DW card ID present | `DW-B187-QX-STOP-SNAP-01` cited in ticket header | PASS |
| Cross-references | `DW-B63-01`, `DW-B126` both cited | PASS |
| Traces to plan | Plan Section 3 describes this exact fix for both overloads | PASS |
| Closes DW-B187-QX-STOP-SNAP-01 | Stated explicitly in ticket header and plan Section 2 | PASS |
| No phantom work | Every change in T1 is in plan Section 3; nothing extra | PASS |
| No missing work from plan | Plan Section 3 calls for XML summary update to line 31 -- ticket documents this at line 60-70 of the tickets file | PASS |

**Traceability: PASS**

---

### JS Pre-Check (DNA Rules)

| Rule | Ticket Description Checked | Result |
|------|---------------------------|--------|
| JS-021 (no `lock()`) | No lock in any new code; pure statement reorder | PASS |
| JS-001 (no `throw new` in hot path) | No exceptions introduced | PASS |
| JS-002 (no `return null`) | No null returns introduced | PASS |
| JS-033 (no `async void`) | Both overloads are synchronous void; no async keyword in new code | PASS |
| JS-008/009 (mutable struct / unfreed brush) | Not applicable -- no struct or brush in scope | N/A |

New code strings are all ASCII (no Unicode, no curly quotes, no non-ASCII characters in comment text). Reviewed ticket lines 48-57, 96-100: all ASCII.

**JS Pre-Check: PASS**

---

### CYC Pre-Check

| Method | Stated Before | Stated After | Delta | Verification |
|--------|--------------|-------------|-------|-------------|
| `Execute(Instrument)` | 8 | 8 | 0 | Source-confirmed 8 branches: combined gate(1), acc loop(2), follower guard(3), pos loop(4), null/flat continue(5), instr filter(6), NeedsLeaderFallbackFlatten(7), ExecuteFollowers(8). Pure reorder, no new branches. |
| `Execute(List,Instrument)` | 8 | 8 | 0 | Source-confirmed 8 branches: combined gate(1), null/empty guard(2), acc loop(3), follower skip(4), pos loop(5), null/flat/instr continue(6), flatten guard(7), ExecuteFollowers(8). Pure reorder, no new branches. |

**CYC Pre-Check: PASS**

---

### NT8 Constraints

| Constraint | Check | Result |
|-----------|-------|--------|
| No `async/await` in lifecycle method | No async in any modified code | PASS |
| No `Account.All` outside Loaded handler | Threading model unchanged (Section 7 of plan) | PASS |
| No `sealed` on TradeCopierWindow | Not touched | N/A |
| No `FontFamily` set on WPF element | Not touched | N/A |
| No hardcoded hex color | Not touched | N/A |
| No `CreateOrder` without `PTT-` prefix | No new orders submitted | N/A |
| No `DateTime.Now` | Not in new code | PASS |

**NT8 Check: PASS**

---

### Test Coverage

| Test | Present in Ticket | Bodies Provided | Assertion |
|------|-----------------|----------------|-----------|
| `T_B187_01` | YES -- lines 126-183 of tickets file | FULL body | `snapshotLine < cancelLine` in `Execute(Instrument)` |
| `T_B187_02` | YES -- lines 189-247 of tickets file | FULL body | `snapshotLine < cancelLine` in `Execute(List,Instrument)` |
| `T_B187_03` | YES -- lines 253-309 of tickets file | FULL body | `targetSnapshotLine > cancelLine` in `Execute(Instrument)` -- DW-B126 guard |
| `T_B187_04` | YES -- lines 315-363 of tickets file | FULL body | `Execute(List,Instrument)` body does NOT contain `SnapshotTargetOrders` -- DW-B126 N/A |

Additional checks on test bodies:
- T_B187_01/02: use file path via `Assembly.GetExecutingAssembly().Location` relative navigation -- no NT8 runtime required. xUnit `[Fact]` annotation present on all 4. ✓
- T_B187_02 method-locating logic: searches for second occurrence of `"internal void Execute("`. Source line 39 contains `internal void Execute(Instrument instrument)` (count=1); source line 113 contains `internal void Execute(` (count=2). Logic is correct. ✓
- T_B187_03: asserts `targetSnapshotLine > cancelLine`. Correctly guards DW-B126 post-fix. ✓
- T_B187_04: loops through method body and `Assert.False(lines[i].Contains("SnapshotTargetOrders"), ...)` for each line. Correctly confirms absence. ✓
- All 4 methods follow naming convention `T_B187_XX_<description>()` and are `public void`. ✓

**Test Coverage: PASS**

---

### Scan Checklist Presence

Ticket section "7-Scan Checklist -- T1" (lines 367-383 of tickets file):

| Scan | Present | Command Provided |
|------|---------|-----------------|
| SCAN-01 (no lock) | YES | `git diff ... Select-String "lock\("` |
| SCAN-02 (no return null) | YES | `git diff ... Select-String "return null"` |
| SCAN-03 (no async void) | YES | `git diff ... Select-String "async void "` |
| SCAN-04 (CYC manual count) | YES | Detailed breakdown for both methods provided |
| SCAN-05 (ASCII-only) | YES | `git diff ... Select-String "[^\x00-\x7F]"` |
| SCAN-06 (no throw new) | YES | `git diff ... Select-String "throw new"` |
| SCAN-07 (dotnet build) | YES | `dotnet build ... Select-Object -Last 10` |

All 7 scans present with exact commands.

**Scan Checklist: PASS**

---

### Completeness

| Check | Detail | Result |
|-------|--------|--------|
| BEFORE/AFTER for `Execute(Instrument)` | Provided with exact line numbers (55-62) and replacement block | PASS |
| BEFORE/AFTER for `Execute(List,Instrument)` | Provided with exact line numbers (140-143) and replacement block | PASS |
| XML summary update for `Execute(Instrument)` | Line 31 update specified with BEFORE and AFTER text | PASS |
| Line numbers verified against source | Ticket line 55-62 matches source (confirmed: line 56=CancelPttBeOrders, line 61=SnapshotStopPrice) | PASS |
| DW-B126 compliance explicitly stated | Ticket body states "SnapshotTargetOrders stays AFTER cancel+wait (DW-B126 maintained)" | PASS |
| T_B187_04 correctly addresses DW-B126 N/A case | Confirms `SnapshotTargetOrders` absent from `Execute(List,Instrument)` (forcedTargets path) | PASS |
| Acceptance criteria present | 7 acceptance criteria listed (lines 385-394 of tickets file) | PASS |
| File routing | `src/PropTraderTools/Features/PttGlobalQuickExit.cs` and `src/PropTraderTools/CopyEngineTests.cs` -- Wave workspace paths only | PASS |

**Completeness: PASS**

---

### File Routing

- Source edits: `src/PropTraderTools/Features/PttGlobalQuickExit.cs` -- Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`). ✓
- Test additions: `src/PropTraderTools/CopyEngineTests.cs` -- Wave workspace. ✓
- No Director workspace paths for `.cs` files. ✓

**File Routing: PASS**

---

### VERDICT T1: TICKET_REVIEW_PASS

---

## T2 -- DW-B186-DIAG-CLEANUP-01: Remove [DW-B115-DIAG] log blocks [P3]

### Traceability

| Check | Detail | Result |
|-------|--------|--------|
| DW card ID present | `DW-B186-DIAG-CLEANUP-01` cited in ticket header | PASS |
| Traces to plan | Plan Section 4 describes all 3 removal sites | PASS |
| Closes DW-B186-DIAG-CLEANUP-01 | Ticket header states dependency explicitly | PASS |
| No phantom work | T2 contains exactly the 3 removals and CYC comment update described in plan Section 4 | PASS |
| No missing work from plan | Plan Section 4 mandates `ExecuteFollowers` CYC comment update (7->4) -- ticket provides full before/after at lines 519-538 of tickets file | PASS |

**Traceability: PASS**

---

### JS Pre-Check (DNA Rules)

| Rule | Ticket Description Checked | Result |
|------|---------------------------|--------|
| JS-021 (no `lock()`) | Deletion only -- no new code introduced | PASS |
| JS-001 (no `throw new`) | Deletion only | PASS |
| JS-002 (no `return null`) | Deletion only | PASS |
| JS-033 (no `async void`) | Deletion only | PASS |

Replacement text for `ExecuteFollowers` XML summary is ASCII-only. ✓

**JS Pre-Check: PASS**

---

### CYC Pre-Check

| Method | Stated Before T2 | Stated After T2 | Delta | Verification |
|--------|-----------------|----------------|-------|-------------|
| `Execute(Instrument)` | 8 | 8 | 0 | `LogLeaderDiag(...)` is a statement call, not a branch. Source-confirmed: no decision point removed. |
| `ExecuteFollowers` | 7 | 4 | -3 | Source-confirmed 7 branches at lines 184-185 of `PttGlobalQuickExit.cs`. Removing branches (4) foreach, (5) null/instr guard, (6) for-loop yields 4. Arithmetic correct. |
| `LogLeaderDiag` | 2 | DELETED | -- | Method has no callers after T2. Full deletion correct. |

CYC comment update: Ticket provides exact BEFORE and AFTER text for `ExecuteFollowers` XML summary. The AFTER reads `CYC=4: rule null-check(1), follower foreach(2), follower null continue(3), delegate(4).` -- matches plan Section 4 specification exactly.

**CYC Pre-Check: PASS**

---

### NT8 Constraints

Deletion-only ticket. No new NT8 API usage. All NT8 constraint checks N/A except:
- No new `lock()`, `async void`, `return null`, `throw new` introduced. ✓

**NT8 Check: PASS**

---

### Test Coverage

Zero tests required and zero tests specified -- correct for a pure deletion ticket. Architecture plan Section 8 explicitly states "0 New Tests. Pure deletion."

**Test Coverage: PASS (0 tests, correctly stated)**

---

### Scan Checklist Presence

Ticket section "7-Scan Checklist -- T2" (lines 558-574 of tickets file):

| Scan | Present | Command Provided |
|------|---------|-----------------|
| SCAN-01 (no lock) | YES | `git diff ... Select-String "lock\("` |
| SCAN-02 (no return null) | YES | `git diff ... Select-String "return null"` |
| SCAN-03 (no async void) | YES | `git diff ... Select-String "async void "` |
| SCAN-04 (CYC manual count) | YES | Per-method breakdown: Execute=8, ExecuteFollowers 7->4, LogLeaderDiag deleted |
| SCAN-05 (ASCII-only) | YES | `git diff ... Select-String "[^\x00-\x7F]"` |
| SCAN-06 (no throw new) | YES | `git diff ... Select-String "throw new"` |
| SCAN-07 (dotnet build) | YES | `dotnet build ... Select-Object -Last 10` |

All 7 scans present with exact commands.

**Scan Checklist: PASS**

---

### Completeness

| Check | Detail | Result |
|-------|--------|--------|
| Removal 1 location specified | Lines 76-78 (comment + `LogLeaderDiag` call) -- source-confirmed | PASS |
| Removal 2 location specified | Lines 208-245 (full DIAG block in `ExecuteFollowers`) -- source-confirmed | PASS |
| Removal 3 location specified | Lines 490-519 (`LogLeaderDiag` method + XML summary) -- source-confirmed | PASS |
| Post-T1 line shift acknowledged | Ticket explicitly states "After T1 is committed the absolute line numbers will shift slightly... The engineer MUST re-read the file" | PASS |
| String patterns provided for re-location | Unique deterministic patterns provided for all 3 removals | PASS |
| `ExecuteFollowers` CYC comment update specified | Full before/after text provided | PASS |
| `_fPosQty` safety note | Ticket confirms variable is only used in the DIAG block being deleted | PASS |
| Acceptance criteria present | 5 acceptance criteria listed (lines 579-584 of tickets file) | PASS |
| File routing | `src/PropTraderTools/Features/PttGlobalQuickExit.cs` -- Wave workspace only | PASS |

**Completeness: PASS**

---

### File Routing

- Source edits: `src/PropTraderTools/Features/PttGlobalQuickExit.cs` -- Wave workspace. ✓

**File Routing: PASS**

---

### VERDICT T2: TICKET_REVIEW_PASS

---

## T3 -- DW-B186-WARN-01: Suppress CS0067 PendingBeArmed warning [P3]

### Traceability

| Check | Detail | Result |
|-------|--------|--------|
| DW card ID present | `DW-B186-WARN-01` cited in ticket header | PASS |
| Supersedes reference | `DW-B184-04` listed as superseded -- matches plan Section 12 carry-forward list | PASS |
| Traces to plan | Plan Section 5 describes this exact `#pragma` wrap | PASS |
| Closes DW-B186-WARN-01 and supersedes DW-B184-04 | Explicitly stated in ticket header | PASS |
| No phantom work | Ticket only adds `#pragma disable/restore CS0067` around `PendingBeArmed` as described in plan | PASS |
| No missing work from plan | Plan Section 5 fully reflected in ticket | PASS |

**Traceability: PASS**

---

### JS Pre-Check (DNA Rules)

| Rule | Ticket Description Checked | Result |
|------|---------------------------|--------|
| JS-021 (no `lock()`) | `#pragma` directive only -- no lock | PASS |
| JS-001 (no `throw new`) | No new code | PASS |
| JS-002 (no `return null`) | No new code | PASS |
| JS-033 (no `async void`) | No new code | PASS |

Comment text `// DW-B186-WARN-01: Roslyn false positive -- event IS subscribed by TradeCopierPanel` is ASCII-only. ✓

**JS Pre-Check: PASS**

---

### CYC Pre-Check

No methods modified. `#pragma` directives are compile-time metadata only. CYC delta = 0 throughout `CopyEngine.cs`. Ticket states this explicitly.

**CYC Pre-Check: PASS**

---

### NT8 Constraints

Pragma-only ticket. No NT8 API usage. All NT8 constraint checks N/A.

**NT8 Check: PASS**

---

### Test Coverage

Zero tests required and zero tests specified -- correct. Acceptance criterion is `dotnet build` producing 0 CS0067 warnings (SCAN-07 covers this). Architecture plan Section 8 explicitly states "0 New Tests."

**Test Coverage: PASS (0 tests, correctly stated)**

---

### Scan Checklist Presence

Ticket section "7-Scan Checklist -- T3" (lines 639-663 of tickets file):

| Scan | Present | Command Provided |
|------|---------|-----------------|
| SCAN-01 (no lock) | YES | `git diff ... Select-String "lock\("` |
| SCAN-02 (no return null) | YES | `git diff ... Select-String "return null"` |
| SCAN-03 (no async void) | YES | `git diff ... Select-String "async void "` |
| SCAN-04 (CYC manual count) | YES | "No methods modified. #pragma does not introduce branches. CYC = 0 delta." |
| SCAN-05 (ASCII-only) | YES | `git diff ... Select-String "[^\x00-\x7F]"` |
| SCAN-06 (no throw new) | YES | `git diff ... Select-String "throw new"` |
| SCAN-07 (dotnet build) | YES | Full command + CS0067-specific verification command |

All 7 scans present with exact commands. SCAN-07 additionally provides the targeted CS0067 verification command.

**Scan Checklist: PASS**

---

### Completeness

| Check | Detail | Result |
|-------|--------|--------|
| Pragma exact text provided | `#pragma warning disable CS0067  // DW-B186-WARN-01: ...` and `#pragma warning restore CS0067` | PASS |
| BEFORE/AFTER provided | Line 423 before and 3-line replacement after -- source-confirmed correct | PASS |
| Context lines provided | Lines 420-425 shown in context block | PASS |
| Rationale documented | "Roslyn false positive -- subscribed by TradeCopierPanel.cs lines 771-772" | PASS |
| Acceptance criteria present | 3 acceptance criteria listed (lines 667-670 of tickets file) | PASS |
| File routing | `src/PropTraderTools/CopyEngine.cs` -- Wave workspace only | PASS |

**Completeness: PASS**

---

### File Routing

- Source edits: `src/PropTraderTools/CopyEngine.cs` -- Wave workspace. ✓

**File Routing: PASS**

---

### VERDICT T3: TICKET_REVIEW_PASS

---

## Aggregate Spec Coverage

| Spec/Plan Requirement | Covered By | Coverage |
|----------------------|-----------|---------|
| DW-B187-QX-STOP-SNAP-01 (new -- QX stop price = 0 when BE ALL fires before QX) | T1 | YES, exactly once |
| DW-B186-DIAG-CLEANUP-01 (spec line 42996 -- remove DIAG scaffolding) | T2 | YES, exactly once |
| DW-B186-WARN-01 (spec line 43008 -- CS0067 suppress) | T3 | YES, exactly once |
| DW-B184-04 supersession | T3 header | YES |
| DW-B126 compliance maintained (SnapshotTargetOrders after cancel) | T1 description + T_B187_03 | YES |
| CYC <= 8 for all modified methods | T1 SCAN-04, T2 SCAN-04 | YES |
| 7-scan checklist per ticket | T1, T2, T3 each have full checklist | YES |
| SINGLE-PIPELINE execution order T1->T2->T3 | Stated in tickets file header and Section "Execution Order Summary" | YES |

No uncovered requirements. No duplicate coverage. No phantom work.

---

## Observations (Non-Blocking, Informational)

1. **T2 line number caveat is correct and well-documented**: T2 explicitly warns the engineer that post-T1 absolute line numbers will shift and instructs them to re-read the file and use string patterns for location. This is good defensive engineering.

2. **T_B187_04 assertion style**: Uses `Assert.False(lines[i].Contains(...))` in a loop rather than `Assert.DoesNotContain`. Both are valid xUnit patterns. The chosen pattern is more explicit about which line triggered the failure, which aids debugging.

3. **T2 BEFORE block line count discrepancy (informational)**: The ticket section header says "current lines 208-245" (38 lines) and the body says "BEFORE (remove this entire block, current lines 208-245)". The code block in the ticket contains exactly 38 lines (208-245 inclusive). Source-confirmed correct. The earlier plan reference (Section 4) says "~lines 208-244" but the ticket's count of 208-245 inclusive (38 lines) matches the actual source. Non-blocking.

4. **DW-B183-05 carry-forward**: RULES_CATALOG.md is known truncated at JS-041 (DW-B183-05, P1). JS-081 (ASCII-only) cited in the DNA table is enforced via hardcoded DNA block. This known open item does not affect B187 ticket review.

---

## Overall: TICKET_REVIEW_PASS

All three tickets passed all checks:

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | VERDICT |
|--------|-------------|-------------|--------------|-----------|--------------|---------------|-------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |

**No violations found. Tickets are approved for Phase 4 engineering.**

The engineer may proceed with T1 -> T2 -> T3 in sequential order per SINGLE-PIPELINE mandate.

---

*Written by: ptt-ticket-reviewer*
*Epic: B187*
*Phase: 3.5 (Ticket Review)*
*Return: TICKET_REVIEW_PASS*
