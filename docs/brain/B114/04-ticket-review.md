# B114 Ticket Review — Phase 3.5

**Block**: B114
**Date**: 2026-08-27
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Tickets under review**: `docs/brain/B114/04-tickets.md` (1 ticket — B114-T1)
**Plan source reviewed**: `docs/brain/B114/02-architecture-plan.md` (REVIEW_PASS confirmed)
**Sources read**:
- `docs/brain/B114/04-tickets.md` (full)
- `docs/brain/B114/02-architecture-plan.md` (full)
- `docs/brain/B114/02-plan-review.md` (full)
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` L127–194
- `src/PropTraderTools/Tests/B113Tests.cs` (access denied — gitignored; plan Section H used as reference source)

---

## Ticket Review: B114

### B114-T1 — TryAdd Placement Fix + Test Update

---

#### TR-01 — Traceability

**Check**: Ticket references spec sections `#section-dw-b119` and `#section-dw-b120`; spec closure deferred to Ph5.

**Evidence**:
- Ticket §"Spec Requirement IDs": explicitly cites `#section-dw-b119` (root cause) and `#section-dw-b120` (mitigated/monitored).
- Ticket explicitly states: "spec closure for #section-dw-b119 and #section-dw-b120 is **DEFERRED TO Ph5**. The engineer does NOT touch the spec file."
- Plan Section J confirms Ph5 owns spec update.

**VERDICT**: PASS

---

#### TR-02 — Change Scope

**Check**: Ticket covers exactly `PttGlobalQuickExit.cs`, `B113Tests.cs`, `NO-PIPELINE-REPAIRS.md`; spec closure deferred to Ph5; NO changes to `CopyEngine.cs` or other source files.

**Evidence**:
- Ticket FILE 1: `src/PropTraderTools/Features/PttGlobalQuickExit.cs` ✓
- Ticket FILE 2: `src/PropTraderTools/Tests/B113Tests.cs` ✓
- Ticket FILE 3: `docs/brain/NO-PIPELINE-REPAIRS.md` ✓
- Ticket FILE 4: `specs/002-trade-copier-spec.html` — explicitly DEFERRED TO Ph5 ✓
- FORBIDDEN list: "DO NOT modify `src/PropTraderTools/CopyEngine.cs`" ✓
- Plan Section B scope (2 source files + 2 doc files, with spec as Ph5) matches ticket scope exactly.

**VERDICT**: PASS

---

#### TR-03 — Exact Diff

**Check**: Ticket specifies exact BEFORE/AFTER code for the TryAdd move; TryAdd moves from inside `try{}` after `Execute` to before `try{}` before `Execute`; comment updated to B114 DW-B119 text.

**Evidence**:
- Ticket §FILE 1 BEFORE block (L145–181): verbatim — `_qxPendingFollowerCleanup.TryAdd(...)` is inside `try{}` after `executor.Execute(...)`. Cross-checked against live file read (L145–182): exact match.
- Ticket AFTER block: `_qxPendingFollowerCleanup.TryAdd(...)` is BEFORE `try {`, preceded by 4-line B114 DW-B119 comment. `try{}` body contains only `var executor = new PttQuickExit()` + `executor.Execute(...)`.
- Net change summary enumerates 6 specific operations (remove old B113 comment, remove TryAdd from try{}, insert 4-line DW-B119 comment, insert TryAdd before try{}, confirm try{} body, preserve finally{}).

**VERDICT**: PASS

---

#### TR-04 — Exception Safety

**Check**: Ticket addresses what happens if `Execute` throws; `TryAdd` already in map; `finally{}` still removes `_qxCancelInProgress` — safe.

**Evidence**:
- Ticket AFTER block structural invariant: `finally{}` block preserved exactly, `_qxCancelInProgress.TryRemove` fires unconditionally regardless of Execute outcome.
- FORBIDDEN list: "DO NOT modify the `finally {}` block in `ExecuteOne`".
- Completion criteria: "`finally {}` `TryRemove` block is word-for-word identical to B113 shipped state".
- Plan Section E Case 2 (exception safety: orphaned map entry expires via 2s TTL) is the architectural basis; the ticket encodes the structural guarantee through the preserved AFTER block and the FORBIDDEN constraint.
- The exception-safety invariant is implicit in the structural content but is fully covered by the contract artifacts.

**VERDICT**: PASS

---

#### TR-05 — CYC Pre-Check

**Check**: Ticket states `ExecuteOne` CYC=2 before and after.

**Evidence**:
- Ticket §"JS Rule Constraints" table row `CYC <= 8`: "ExecuteOne CYC = 2 (unchanged). if(!skipIfFollower)+1, base+1, try/finally=0".
- Completion criteria item: "CYC of `ExecuteOne` = 2 (verify: `if (!skipIfFollower)` is the only conditional)".
- Plan Section F: CYC before=2, after=2, delta=0, status=PASS.

**VERDICT**: PASS

---

#### TR-06 — 7-Scan Checklist Present

**Check**: All 7 scans present with exact PowerShell commands; SCAN-1 lock(), SCAN-2 async void, SCAN-3 TryAdd placement, SCAN-4 DW-B117-DIAG, SCAN-5 ptt-sync-and-verify, SCAN-6 return null, SCAN-7 ASCII.

**Evidence** (each scan confirmed with exact command):
- SCAN-1: `Select-String -Path "src\PropTraderTools\Features\PttGlobalQuickExit.cs" -Pattern "lock\s*\("` — expected 0 results ✓
- SCAN-2: `Select-String -Path "src\PropTraderTools\Features\PttGlobalQuickExit.cs" -Pattern "async void"` — expected 0 results ✓
- SCAN-3: `Select-String -Path "src\PropTraderTools\Features\PttGlobalQuickExit.cs" -Pattern "_qxPendingFollowerCleanup"` — manual TryAdd placement verification ✓
- SCAN-4: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "DW-B117-DIAG"` — expected 0 results ✓
- SCAN-5: `powershell -File scripts\ptt-sync-and-verify.ps1` — expected N/N OK, 0 MISMATCH ✓
- SCAN-6: `Select-String -Path "src\PropTraderTools\Features\PttGlobalQuickExit.cs" -Pattern "return null"` — expected 0 results ✓
- SCAN-7: `Select-String -Path "src\PropTraderTools\Features\PttGlobalQuickExit.cs" -Pattern "[^\x00-\x7F]"` — expected 0 results ✓

All 7 scans carry expected result and explicit pass criterion.

**Note**: Ticket expands plan Section G (5 scans) to 7 scans — additive improvement. No phantom scope; all scans directly verify B114 contract requirements.

**VERDICT**: PASS

---

#### TR-07 — NT8 Constraints

**Check**: Ticket respects NT8 AddOn constraints; no NinjaScript-only APIs, no `Account.CancelOrder` usage in new code.

**Evidence**:
- No new NT8 API calls introduced. TryAdd is a ConcurrentDictionary operation.
- No `async/await` in lifecycle methods.
- No `Account.All` outside Loaded handler.
- No `sealed` on TradeCopierWindow.
- No `FontFamily` on WPF elements.
- No hardcoded hex colors.
- No `CreateOrder` call (no new order submission).
- `DateTime.UtcNow` used (not `DateTime.Now`); confirmed in JS rule table.
- FORBIDDEN list prohibits engineer from touching CopyEngine.cs where NT8 API patterns could emerge.

**VERDICT**: PASS

---

#### TR-08 — Completeness

**Check**: Ticket includes all required sections: title, spec refs, files, before/after, JS rules, test names, scans, forbidden actions, completion criteria.

**Evidence**:
- Title: "B114-T1 — TryAdd Placement Fix + Test Update" ✓
- Spec Requirement IDs section ✓
- FILE 1/2/3/4 sections with full content ✓
- BEFORE/AFTER code blocks (verbatim, L145–181 range confirmed against live source) ✓
- JS Rule Constraints table (7 rules) ✓
- xUnit [Fact] Tests table (4 tests) ✓
- 7-SCAN CHECKLIST (7 scans, each with command + expected result + pass criterion) ✓
- FORBIDDEN Actions list (11 explicit prohibitions) ✓
- COMPLETION CRITERIA gate checklist (19 items) ✓
- Completion Artifact specification ✓

**VERDICT**: PASS

---

#### TR-09 — Test Coverage

**Check**: T_B113_01 rename and assertion flip; OLD name `QxPendingFollowerCleanup_SetAfterExecuteOne_ForFollower`, NEW `QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower`; assertion: map populated BEFORE execute; T_B113_02/03/04 unchanged; all 4 [Fact] present; xUnit only; no async void.

**Evidence**:
- Ticket §FILE 2: OLD name cited verbatim ✓; NEW name cited verbatim ✓
- Complete NEW T_B113_01 body provided: comment says "fires BEFORE executor.Execute" and "DW-B119 fix -- B114" ✓
- Act comment: "simulate the TryAdd call that fires BEFORE executor.Execute in ExecuteOne follower path (B114 DW-B119 fix)" ✓
- Assertions: `ContainsKey(accName)` true; `entry.Expiry > DateTime.UtcNow`; `entry.Expiry <= DateTime.UtcNow.AddSeconds(3)` ✓
- T_B113_02/03/04: "COPY VERBATIM from existing B113Tests.cs. Zero changes." ✓
- Tests table: all 4 [Fact] tests listed ✓
- "xUnit only. No NUnit. No MSTest. No `async void`." stated explicitly ✓

**VERDICT**: PASS

---

#### TR-10 — Forbidden Actions Listed

**Check**: Ticket explicitly prohibits: no CopyEngine.cs changes, no InternalsVisibleTo duplication, no lock(), no async void, no DateTime.Now, no T_B113_02/03/04 changes.

**Evidence** (FORBIDDEN Actions section, 11 items):
- "DO NOT modify `src/PropTraderTools/CopyEngine.cs`" ✓
- "DO NOT add `[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]`" ✓
- "DO NOT add `lock()` anywhere — JS-021 P0 violation." ✓
- "DO NOT add `async void` — JS-033 P0 violation." ✓
- "DO NOT use `DateTime.Now` — must be `DateTime.UtcNow`." ✓
- "DO NOT change T_B113_02, T_B113_03, T_B113_04 — only T_B113_01 changes." ✓
- Additional prohibitions: no new test classes/[Fact] methods ✓; no finally{} modification ✓; no spec changes ✓; no leader path changes ✓; no PttQuickExit.cs changes ✓

**VERDICT**: PASS

---

#### TR-11 — JS Rules Explicitly Listed

**Check**: JS-021 (no lock), JS-033 (no async void), CYC<=8 (CYC=2 for ExecuteOne), ASCII-only explicitly listed.

**Evidence** (JS Rule Constraints table):
- JS-021: "No `lock()` anywhere — all state uses `ConcurrentDictionary.TryAdd`/`TryRemove` (lock-free)" ✓
- JS-033: "No `async void` (non-event-handler) — `ExecuteOne` is synchronous `void`; test methods are synchronous `void`" ✓
- JS-001: "No `throw` in hot paths. `TryAdd` is non-throwing on ConcurrentDictionary" ✓
- JS-002: "No `return null`. Bare `return;` at L181 preserved" ✓
- CYC<=8: "ExecuteOne CYC = 2 (unchanged)." ✓
- ASCII-only: "New 4-line DW-B119 comment is ASCII-only. `--` not em-dash." ✓
- DateTime.UtcNow: "use `DateTime.UtcNow`, never `DateTime.Now`" ✓

**VERDICT**: PASS

---

#### TR-12 — Completion Criteria Gate

**Check**: Ticket has completion criteria checklist that gates `ticket-1-completion.md`; all scans pass + code checks.

**Evidence**:
- Ticket §"COMPLETION CRITERIA": 19-item checklist with explicit check boxes.
- Items cover: all 7 SCAN pass conditions ✓; TryAdd placement structural check ✓; executor.Execute position ✓; finally{} identity ✓; B113 comment absence ✓; B114 comment presence ✓; CYC=2 ✓; T_B113_01 rename ✓; T_B113_02/03/04 byte-identity ✓; build pass ✓; NO-PIPELINE-REPAIRS.md entry ✓; ticket-1-completion.md written ✓
- "Before writing `docs/brain/B114/ticket-1-completion.md` and reporting PIPELINE_COMPLETE, ALL of the following must be true" — explicit gate language ✓
- Completion Artifact section specifies 5 required sections for `ticket-1-completion.md` ✓

**VERDICT**: PASS

---

#### TR-13 — NO-PIPELINE-REPAIRS.md Entry

**Check**: Ticket specifies exact DW-B119 entry to append (ID, Date, File, Location, Status).

**Evidence** (Ticket §FILE 3 exact markdown block):
- ID: `DW-B119` ✓
- Date: `2026-08-27` ✓
- File: `src/PropTraderTools/Features/PttGlobalQuickExit.cs` ✓
- Location (Method): `ExecuteOne follower path -- _qxPendingFollowerCleanup.TryAdd` ✓
- Bug description: full verbatim text ✓
- Fix description: full verbatim text ✓
- Status: `FIXED-B114-T1 -- TryAdd moved before executor.Execute...` ✓

**VERDICT**: PASS

---

#### TR-14 — DW-B112 Preservation

**Check**: Ticket explicitly states finally{} TryRemove block is UNCHANGED.

**Evidence**:
- Ticket §"Net change summary" item 6: "`finally {}` block: **PRESERVE EXACTLY** (DW-B112 `_qxCancelInProgress.TryRemove` with its comment)."
- FORBIDDEN list: "DO NOT modify the `finally {}` block in `ExecuteOne` — `_qxCancelInProgress.TryRemove(acc.Name, out _)` with its DW-B112 comment must be preserved exactly."
- Completion criteria: "`finally {}` `TryRemove` block is word-for-word identical to B113 shipped state"
- AFTER code block: `finally{}` text is character-for-character identical to BEFORE block.

**VERDICT**: PASS

---

## Summary Matrix

| ID | Check | Verdict |
|----|-------|---------|
| TR-01 | Traceability — spec refs #section-dw-b119, #section-dw-b120 | **PASS** |
| TR-02 | Change scope — correct files, no CopyEngine.cs | **PASS** |
| TR-03 | Exact diff — BEFORE/AFTER verbatim, TryAdd placement | **PASS** |
| TR-04 | Exception safety — finally{} guard preserved | **PASS** |
| TR-05 | CYC pre-check — ExecuteOne CYC=2 before and after | **PASS** |
| TR-06 | 7-scan checklist — all 7 scans with exact commands | **PASS** |
| TR-07 | NT8 constraints — no prohibited APIs | **PASS** |
| TR-08 | Completeness — all required sections present | **PASS** |
| TR-09 | Test coverage — T_B113_01 rename + all 4 [Fact] | **PASS** |
| TR-10 | Forbidden actions — all 6 required prohibitions listed | **PASS** |
| TR-11 | JS rules — JS-021, JS-033, CYC=2, ASCII-only listed | **PASS** |
| TR-12 | Completion criteria gate — 19-item checklist gates ticket-1-completion.md | **PASS** |
| TR-13 | NO-PIPELINE-REPAIRS.md entry — exact DW-B119 block specified | **PASS** |
| TR-14 | DW-B112 preservation — finally{} TryRemove explicitly UNCHANGED | **PASS** |

---

## Violations

**None.** Zero violations found across all 14 checklist items.

---

## Notes for Engineer (Phase 4a)

1. **Source file confirmed**: Live read of `PttGlobalQuickExit.cs` L127–194 confirms the file is in the exact B113 shipped state described in the BEFORE block. The TryAdd is currently inside `try{}` after `executor.Execute` at L170–173.

2. **Test file access**: `B113Tests.cs` is gitignored and could not be read directly. The ticket provides the complete replacement method body verbatim — use the ticket's §FILE 2 as the authoritative source.

3. **Scan count expansion**: Ticket specifies 7 scans vs. plan Section G's 5 scans. This is additive improvement (SCAN-4 DW-B117-DIAG and SCAN-6 return-null added). All 7 must be run and recorded in `ticket-1-completion.md`.

4. **Spec file**: Do NOT touch `specs/002-trade-copier-spec.html`. It is explicitly deferred to Ph5.

5. **Single surgical change**: The entire change is a 3-line reorder in one method. The risk of scope creep is low. Follow the FORBIDDEN list precisely.

---

## Overall

**TICKET_REVIEW_PASS**

All 14 checklist items pass. Zero JS rule violations. Zero NT8 constraint violations. Zero traceability gaps. Zero missing test coverage. All 7 scans present with exact commands. Ticket is approved for Phase 4a engineer execution.

---

*Review completed by ptt-ticket-reviewer (Phase 3.5). Gate: TICKET_REVIEW_PASS.*
