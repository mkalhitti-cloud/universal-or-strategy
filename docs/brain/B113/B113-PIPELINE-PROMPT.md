# B113 Pipeline Orchestrator Prompt

**Block**: B113
**Defect**: DW-B117 — QX-ALL PTT-QX-T2/T3 Missing on Followers (P0, blocks live trading)
**Director approval**: 2026-08-26
**Status**: READY TO START — paste this prompt into a ptt-orchestrator session

---

## PASTE THIS ENTIRE PROMPT INTO ptt-orchestrator

---

You are the ptt-orchestrator for Block B113.

Defect: DW-B117 — P0. QX-ALL leaves follower accounts with PTT-QX-T2 and/or PTT-QX-T3 missing
because the pre-cancel step in `ExecuteOne` (PttGlobalQuickExit.cs) triggers NT8 ATM re-arm.
Re-armed native ATM Target2/Target3 race with PTT-QX-T2/T3 and cause NT8 OCO to cancel the
PTT-QX orders. Follower position is partially unprotected. Reproduced Combo D (T3 missing Sim102)
and Combo C (T2+T3 missing Sim103, Director manually closed 3 naked contracts). Director approved
B113 pipeline 2026-08-26.

The pipeline for ALL C# src edits is the FULL 5-PHASE PTT PIPELINE. All phases are MANDATORY.
None may be skipped. None may be combined. The exact chain is:

  Ph1  ptt-architect       -> docs/brain/B113/02-architecture-plan.md
  Ph2  ptt-plan-reviewer   -> docs/brain/B113/02-plan-review.md       (REVIEW_PASS gate)
  Ph3  ptt-architect       -> docs/brain/B113/04-tickets.md
  Ph3.5 ptt-ticket-reviewer -> docs/brain/B113/04-ticket-review.md    (TICKET_REVIEW_PASS gate)
  Ph4a ptt-engineer        -> src .cs edits + docs/brain/B113/ticket-1-completion.md
  Ph4b ptt-verifier        -> docs/brain/B113/ticket-1-verification.md (VERIFY_PASS gate)
  Ph5  ptt-plan-reviewer   -> docs/brain/B113/05-final-review.md + docs/brain/B113/06-deferred-backlog.md

You will start_subtask for each phase in sequence, passing the phase-specific prompt below.
Do NOT start Ph2 until Ph1 produces REVIEW_PASS. Do NOT start Ph4a until Ph3.5 produces TICKET_REVIEW_PASS.
Do NOT start Ph5 until Ph4b produces VERIFY_PASS.

Read docs/brain/B113/00-defect-brief.md first before starting Ph1.

---

## Ph1 PROMPT — ptt-architect

You are ptt-architect for Block B113, Phase 1 (Architecture Plan).

MANDATORY FIRST STEPS:
1. Read docs/standards/jane-street/RULES_CATALOG.md lines 1–30. State GATE PASS or BLOCKED.
2. Read docs/brain/B113/00-defect-brief.md in full.
3. Read docs/intel/jane-street/complexity-reduction.md
4. Read docs/intel/jane-street/lock-free-patterns.md

CONTEXT — what you are fixing:
Root cause: `ExecuteOne` in `src/PropTraderTools/Features/PttGlobalQuickExit.cs` calls
`CancelQxBrackets` on the follower BEFORE submitting PTT-QX orders. This batch-cancel triggers
NT8's ATM engine to re-arm. Re-armed Target2/Target3 arrive Working during the PTT-QX submit
loop and cause NT8 OCO to cancel PTT-QX-T2/T3. Position partially unprotected.

Fix principle: Cancel-After. Do NOT batch-cancel before submit. Submit PTT-QX first. Then in
`OnOrderUpdate`, cancel each native ATM bracket one-for-one as the corresponding PTT-QX-T* order
confirms Working.

KEY SOURCE FACTS (read each file before writing the plan):

File 1: src/PropTraderTools/Features/PttGlobalQuickExit.cs
  - `ExecuteOne` method, L127–178
  - The pre-cancel block is L145–167 (inside `if (!skipIfFollower)`)
  - `CancelQxBrackets` call at L157
  - `_qxCancelInProgress.TryAdd` at L154, `TryRemove` at L165
  - `executor.Execute(...)` at L169–177 (the PTT-QX submit)

File 2: src/PropTraderTools/CopyEngine.cs
  - `_qxCancelInProgress` field at L263 (ConcurrentDictionary<string, bool>)
  - `IsAtmBracketName` at L675
  - `IsFollowerAccount` at L645
  - `OnOrderUpdate` working-state handler: L1208–1255 (where new cleanup logic goes)
  - DW-B117-DIAG probe: L1230–1250 (MUST BE REMOVED in this block)
  - `TryReplacePttBeBrackets` guard chain at L2308–2340 (DW-B112 structural check — do NOT modify)

REQUIRED CHANGES (exactly 4):

CHANGE 1 — PttGlobalQuickExit.cs `ExecuteOne` (follower path):
  REMOVE the `try { CancelQxBrackets(...) } finally { TryRemove }` block (L155–166).
  KEEP `_qxCancelInProgress.TryAdd(acc.Name, true)` and `TryRemove(acc.Name, ...)` but
  restructure: TryAdd before executor.Execute, TryRemove in a finally AFTER executor.Execute.
  KEEP `[PTT-QX-GUARD]` log line.
  ADD: After `executor.Execute(...)` returns, set `_qxPendingFollowerCleanup` entry:
    `CopyEngine.Instance?._qxPendingFollowerCleanup.TryAdd(acc.Name, (instr, DateTime.UtcNow.AddSeconds(2)))`
  NOTE: The new finally wrapping executor.Execute ensures TryRemove fires even on exception.
  CYC for ExecuteOne must remain ≤ 8.

CHANGE 2 — CopyEngine.cs: new field `_qxPendingFollowerCleanup`
  After the `_qxCancelInProgress` field declaration (L263), add:
  `internal readonly ConcurrentDictionary<string, (Instrument Instr, DateTime Expiry)>
       _qxPendingFollowerCleanup = new ConcurrentDictionary<string, (Instrument, DateTime)>();`
  Comment: B113 DW-B117 cancel-after: keyed by acc.Name, expiry = UtcNow+2s.
  JS-021: ConcurrentDictionary, no lock.

CHANGE 3 — CopyEngine.cs `OnOrderUpdate`: cancel-after logic
  LOCATION: In the Working-state handler. Remove the DW-B117-DIAG block (L1230–1250) entirely.
  In its place, add the cancel-after guard:

  Condition (all must be true):
    a. e.Order.OrderState == OrderState.Working
    b. e.Order.Name starts with "PTT-QX-T" AND e.Order.Name.Length >= 9
       AND char.IsDigit(e.Order.Name[8]) (e.g. PTT-QX-T1, T2, T3)
    c. e.Order.Account != null AND IsFollowerAccount(e.Order.Account)
    d. _qxPendingFollowerCleanup.TryGetValue(e.Order.Account.Name, out var entry)
    e. entry.Expiry > DateTime.UtcNow  (TTL not elapsed)
    f. entry.Instr?.FullName == e.Order.Instrument?.FullName

  Action:
    char tChar = e.Order.Name[8]; // '1', '2', or '3'
    string nativeName = "Target" + tChar; // "Target1", "Target2", "Target3"
    Iterate e.Order.Account.Orders:
      find order o where o.Name == nativeName
        AND o.Instrument?.FullName == entry.Instr.FullName
        AND (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted)
      If found: call e.Order.Account.CancelOrder(o)
      Log: "[PTT-QX-CLEANUP] " + acc.Name + " cancelled " + nativeName + " (cancel-after DW-B117)"

    After cancelling or not-found: check if all 3 native brackets (Target1/2/3) are now in a
    terminal/cancel state on this account+instr. If yes (or TTL elapsed): TryRemove from dict.
    Alternative (simpler): always remove after processing T3. Or remove on TTL only.
    Architect must specify the exact removal policy.

  CYC constraint: the entire new block is a single if-branch. CYC delta = +1.
  The surrounding OnOrderUpdate method CYC must remain ≤ 8 after extraction rules apply.
  If adding +1 would exceed 8, the architect must extract the cleanup logic into a helper:
    `private void TryCleanupReArmedAtmBracket(OrderEventArgs e)` (CYC ≤ 4).

CHANGE 4 — Remove DW-B117-DIAG probe
  The DW-B117-DIAG block at CopyEngine.cs L1230–1250 is a temporary diagnostic.
  Remove it entirely. Root cause confirmed. No replacement needed (the cancel-after log
  from CHANGE 3 provides observability during live testing).

OUTPUT ARTIFACT: docs/brain/B113/02-architecture-plan.md

The plan must include:
  - Section A: Problem statement (3 lines)
  - Section B: Change Plan — 4 changes with BEFORE/AFTER code blocks (exact C# syntax)
  - Section C: CYC impact table (method, before, after, delta)
  - Section D: Jane Street scan checklist (JS-021, JS-033, JS-001, JS-002, ASCII-only)
  - Section E: Files Modified / Files NOT Modified table
  - Section F: Sync gate command
  - Section G: Test requirements — 4 [Fact] tests with exact names and assertions
  - Section H: Live re-test criteria (Combo D + Combo C pass/fail)

---

## Ph2 PROMPT — ptt-plan-reviewer

You are ptt-plan-reviewer for Block B113, Phase 2 (Plan Review).

MANDATORY FIRST STEPS:
1. Read docs/standards/jane-street/RULES_CATALOG.md lines 1–30. State GATE PASS or BLOCKED.
2. Read docs/brain/B113/02-architecture-plan.md in full.
3. Read src/PropTraderTools/Features/PttGlobalQuickExit.cs ExecuteOne (L127–178).
4. Read src/PropTraderTools/CopyEngine.cs L1208–1255 (OnOrderUpdate working-state region).
5. Read src/PropTraderTools/CopyEngine.cs L2308–2360 (TryReplacePttBeBrackets — must NOT be touched).

REVIEW CHECKLIST (must verify all):
  [ ] CHANGE 1 BEFORE/AFTER is syntactically valid C#; CancelQxBrackets call removed; TryAdd/TryRemove restructured correctly; _qxPendingFollowerCleanup set after execute
  [ ] CHANGE 2 field type correct: ConcurrentDictionary<string,(Instrument,DateTime)>; initialized at declaration; JS-021 compliant
  [ ] CHANGE 3 condition indexes are correct: PTT-QX-T* means Name[8] is the digit (not Name[7]); verify "PTT-QX-T1".Length = 9, Name[8]='1' ✓
  [ ] CHANGE 3 nativeName mapping: T1→Target1, T2→Target2, T3→Target3 is exact NT8 ATM bracket naming
  [ ] CHANGE 3 CYC impact: if delta >1, extraction helper required; architect must specify
  [ ] CHANGE 4 removes only L1230–1250 and nothing else
  [ ] TryReplacePttBeBrackets at L2308–2360 unchanged
  [ ] No lock() anywhere in new code
  [ ] No async void anywhere in new code
  [ ] No return null (all new returns are void or ConcurrentDictionary bool)
  [ ] ASCII-only in all new string literals
  [ ] 4 test cases present with exact names; assertions are meaningful (not trivially true)
  [ ] Sync gate command present
  [ ] Live re-test criteria present for Combo D and Combo C

OUTPUT: docs/brain/B113/02-plan-review.md
Gate result: REVIEW_PASS or REVIEW_BLOCKED(reason).
If REVIEW_BLOCKED: list exact items that must be fixed. ptt-architect must produce a revised plan.
Do NOT proceed until REVIEW_PASS is recorded.

---

## Ph3 PROMPT — ptt-architect (Ticket Generation)

You are ptt-architect for Block B113, Phase 3 (Ticket Generation).

MANDATORY FIRST STEPS:
1. Read docs/standards/jane-street/RULES_CATALOG.md lines 1–30. State GATE PASS or BLOCKED.
2. Read docs/brain/B113/02-architecture-plan.md in full (REVIEW_PASS confirmed).
3. Read src/PropTraderTools/Features/PttGlobalQuickExit.cs in full (current source truth).
4. Read src/PropTraderTools/CopyEngine.cs L255–275 (field declarations region).
5. Read src/PropTraderTools/CopyEngine.cs L1208–1260 (OnOrderUpdate working-state region).

OUTPUT ARTIFACT: docs/brain/B113/04-tickets.md

The ticket file must contain exactly ONE ticket: T1.
T1 must include:

  TICKET-B113-T1:
  Title: DW-B117 Cancel-After Fix — Remove Pre-Cancel, Add QX Cleanup State, Cancel Native ATM Brackets After PTT-QX Working
  Files modified: PttGlobalQuickExit.cs, CopyEngine.cs
  Files NOT modified: PttQuickExit.cs, PttGlobalBreakEven.cs, PttBreakEvenSwap.cs, TradeCopierPanel.cs
  New test file: src/PropTraderTools/Tests/B113Tests.cs

  For EACH of the 4 changes:
    CHANGE-ID (CHANGE-1/2/3/4)
    File path
    Method/location (exact line range from current source)
    BEFORE block (exact current C# code, no truncation)
    AFTER block (exact replacement C# code)
    CYC: before / after / delta

  REMOVE-PROBE:
    File: src/PropTraderTools/CopyEngine.cs
    Location: L1230–1250
    BEFORE: exact current DW-B117-DIAG block
    AFTER: (empty — entirely removed)

  TEST SPEC (for B113Tests.cs):
    4 [Fact] test stubs with exact method names, using Xunit only, no NUnit, no MSTest, no async void.
    Each test must have a comment explaining what it asserts.

  SYNC-GATE: powershell -File scripts\ptt-sync-and-verify.ps1
  COMPILE-GATE: F5 in NinjaTrader 8 — must produce 0 errors

  DW-B117-DIAG REMOVAL NOTE: "Remove DW-B117-DIAG block from OnOrderUpdate (L1230–1250).
    Also remove from docs/brain/NO-PIPELINE-REPAIRS.md: update entry status to REMOVED-B113-T1."

---

## Ph3.5 PROMPT — ptt-ticket-reviewer

You are ptt-ticket-reviewer for Block B113, Phase 3.5 (Ticket Review).

MANDATORY FIRST STEPS:
1. Read docs/standards/jane-street/RULES_CATALOG.md lines 1–30. State GATE PASS or BLOCKED.
2. Read docs/brain/B113/04-tickets.md in full.
3. Read src/PropTraderTools/Features/PttGlobalQuickExit.cs in full.
4. Read src/PropTraderTools/CopyEngine.cs L255–275 (fields).
5. Read src/PropTraderTools/CopyEngine.cs L1208–1260 (OnOrderUpdate).
6. Read src/PropTraderTools/CopyEngine.cs L2308–2360 (TryReplacePttBeBrackets — must be unchanged).

TICKET REVIEW CHECKLIST:
  [ ] CHANGE-1 BEFORE block matches exact current source in PttGlobalQuickExit.cs ExecuteOne
  [ ] CHANGE-1 AFTER removes CancelQxBrackets; restructures TryAdd/TryRemove around executor.Execute; sets _qxPendingFollowerCleanup after execute
  [ ] CHANGE-2 BEFORE: field does not exist (new addition) — confirm _qxPendingFollowerCleanup absent from current source
  [ ] CHANGE-2 AFTER: correct ConcurrentDictionary type; initialized inline; comment present
  [ ] CHANGE-3 BEFORE: DW-B117-DIAG block exact text (L1230–1250)
  [ ] CHANGE-3 AFTER: cancel-after logic; correct Name[8] index for PTT-QX-T*; correct nativeName mapping
  [ ] CHANGE-4 / REMOVE-PROBE: removes only the DIAG block; no surrounding code touched
  [ ] TryReplacePttBeBrackets BEFORE/AFTER identical (not in ticket scope — verify not modified)
  [ ] No new lock() in any AFTER block
  [ ] No new async void
  [ ] All new string literals ASCII-only
  [ ] 4 [Fact] tests: xUnit only, no NUnit, no MSTest, all synchronous
  [ ] CYC table: delta confirmed ≤ +1 for OnOrderUpdate region; helper extracted if needed
  [ ] Sync gate command present and correct

OUTPUT: docs/brain/B113/04-ticket-review.md
Gate result: TICKET_REVIEW_PASS or TICKET_REVIEW_BLOCKED(reason).
Do NOT proceed to Ph4a until TICKET_REVIEW_PASS is recorded.

---

## Ph4a PROMPT — ptt-engineer

You are ptt-engineer for Block B113, Phase 4a (Implementation).

SRC CODE BAN EXCEPTION: This session has Director approval for exactly the changes specified in
docs/brain/B113/04-tickets.md (TICKET_REVIEW_PASS). You may edit ONLY the files and lines
listed in T1. No other .cs edits permitted.

MANDATORY FIRST STEPS:
1. Read docs/standards/jane-street/RULES_CATALOG.md lines 1–30. State GATE PASS or BLOCKED.
2. Read docs/brain/B113/04-tickets.md in full.
3. Read src/PropTraderTools/Features/PttGlobalQuickExit.cs in full (confirm BEFORE blocks match).
4. Read src/PropTraderTools/CopyEngine.cs L255–275, L1208–1260 (confirm BEFORE blocks match).
5. If any BEFORE block does not match current source: STOP. Report discrepancy. Do not edit.

IMPLEMENTATION ORDER (strictly sequential — do not batch):
  1. Apply CHANGE-2 to CopyEngine.cs (add field — simplest, no logic change)
  2. Apply CHANGE-1 to PttGlobalQuickExit.cs (remove pre-cancel, restructure TryAdd/TryRemove, set cleanup dict)
  3. Apply REMOVE-PROBE + CHANGE-3 to CopyEngine.cs (remove DIAG block, add cancel-after logic)
  4. Create src/PropTraderTools/Tests/B113Tests.cs (4 [Fact] tests)
  5. Update docs/brain/NO-PIPELINE-REPAIRS.md: mark DW-B117-DIAG entry status as "REMOVED-B113-T1"

AFTER ALL EDITS:
  Run: powershell -File scripts\ptt-sync-and-verify.ps1
  Expected: N/N OK, 0 MISMATCH. Paste the output.
  If any MISMATCH: do NOT proceed. Fix the mismatch first.

Jane Street compliance scan (run these, paste results):
  grep -n "lock(" src/PropTraderTools/PttGlobalQuickExit.cs
  grep -n "lock(" src/PropTraderTools/CopyEngine.cs
  grep -n "async void" src/PropTraderTools/PttGlobalQuickExit.cs
  grep -rn "DW-B117-DIAG" src/PropTraderTools/CopyEngine.cs
  (last command must return 0 results — probe removed)

OUTPUT ARTIFACT: docs/brain/B113/ticket-1-completion.md
Must include:
  - List of all changes applied with exact line numbers
  - Diff summary (lines added, lines removed per file)
  - Sync gate result (paste output)
  - Jane Street scan results (paste output)
  - CYC of modified methods (manual count)
  - Self-assessment: IMPLEMENTATION_COMPLETE or IMPLEMENTATION_BLOCKED(reason)

---

## Ph4b PROMPT — ptt-verifier

You are ptt-verifier for Block B113, Phase 4b (Independent Verification).

MANDATORY FIRST STEPS:
1. Read docs/standards/jane-street/RULES_CATALOG.md lines 1–30. State GATE PASS or BLOCKED.
2. Read docs/brain/B113/04-tickets.md in full (ground truth — read FIRST, before looking at source).
3. Read src/PropTraderTools/Features/PttGlobalQuickExit.cs ExecuteOne in full (from source, not completion report).
4. Read src/PropTraderTools/CopyEngine.cs L255–280 (field region).
5. Read src/PropTraderTools/CopyEngine.cs L1208–1270 (OnOrderUpdate working-state region).
6. Read docs/brain/B113/ticket-1-completion.md LAST (for sync result cross-check only).

INDEPENDENT VERIFICATION CHECKLIST (verify each from source, not completion report):
  [ ] CHANGE-1: CancelQxBrackets call ABSENT from ExecuteOne
  [ ] CHANGE-1: _qxCancelInProgress.TryAdd present; executor.Execute called; _qxPendingFollowerCleanup.TryAdd present after execute
  [ ] CHANGE-1: TryRemove in finally wrapping executor.Execute (not wrapping CancelQxBrackets)
  [ ] CHANGE-2: _qxPendingFollowerCleanup field present in CopyEngine.cs field region; correct type
  [ ] CHANGE-3: cancel-after logic present in OnOrderUpdate; correct PTT-QX-T* condition; correct nativeName mapping
  [ ] REMOVE-PROBE: DW-B117-DIAG block ABSENT from OnOrderUpdate (grep must return 0 results)
  [ ] TryReplacePttBeBrackets UNCHANGED at L2308–2360 (read from source)
  [ ] No lock() in modified methods (grep scan)
  [ ] No async void in modified files (grep scan)
  [ ] ASCII-only in new string literals (scan modified lines)
  [ ] B113Tests.cs present with 4 [Fact] tests; xUnit only; no async void
  [ ] Sync result: N/N OK, 0 MISMATCH (cross-check completion report)
  [ ] NO-PIPELINE-REPAIRS.md DW-B117-DIAG entry updated to REMOVED-B113-T1
  [ ] CYC of ExecuteOne ≤ 8 (manual count)
  [ ] CYC of cancel-after block / helper ≤ 8

Run these scans independently (paste results):
  grep -n "CancelQxBrackets" src/PropTraderTools/Features/PttGlobalQuickExit.cs
  grep -n "DW-B117-DIAG" src/PropTraderTools/CopyEngine.cs
  grep -n "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs
  grep -n "_qxPendingFollowerCleanup" src/PropTraderTools/CopyEngine.cs
  grep -n "_qxPendingFollowerCleanup" src/PropTraderTools/Features/PttGlobalQuickExit.cs

OUTPUT: docs/brain/B113/ticket-1-verification.md
Gate result: VERIFY_PASS or VERIFY_BLOCKED(reason).
If VERIFY_BLOCKED: describe exact discrepancy. ptt-engineer must fix and re-run sync.

---

## Ph5 PROMPT — ptt-plan-reviewer (Final Review)

You are ptt-plan-reviewer for Block B113, Phase 5 (Final Review).

MANDATORY FIRST STEPS:
1. Read docs/standards/jane-street/RULES_CATALOG.md lines 1–30. State GATE PASS or BLOCKED.
2. Read docs/brain/B113/02-architecture-plan.md
3. Read docs/brain/B113/04-tickets.md
4. Read docs/brain/B113/ticket-1-completion.md
5. Read docs/brain/B113/ticket-1-verification.md (VERIFY_PASS confirmed)

CROSS-FILE COHERENCE:
  [ ] Architecture plan CHANGE-1/2/3/4 → Ticket T1 CHANGE-1/2/3/4: identical BEFORE/AFTER blocks
  [ ] Ticket → Implementation (from VERIFY_PASS): all 4 changes confirmed applied
  [ ] Sync result consistent across completion and verification artifacts
  [ ] CYC values consistent across plan, ticket, completion, verification
  [ ] Test file confirmed present with correct 4 tests

SPEC UPDATE (write this, do not delegate):
  Update specs/002-trade-copier-spec.html #section-dw-b117:
    - Section label: "DW-B117 — P0 — CLOSED B113-T1"
    - Add green closure card:
        "Fix: Cancel-After pattern. Pre-cancel of follower ATM brackets removed from ExecuteOne.
         PTT-QX orders submitted while native ATM brackets remain. OnOrderUpdate: when PTT-QX-T*
         goes Working on follower, cancel corresponding native ATM bracket (Target1/2/3) one-for-one.
         ConcurrentDictionary _qxPendingFollowerCleanup with 2s TTL. DW-B117-DIAG probe removed.
         Files: PttGlobalQuickExit.cs + CopyEngine.cs. Tests T_B113_01..04 pass.
         Live re-test required (Combo D + Combo C) before marking fully closed. Pipeline: B113-T1."

DEFERRED BACKLOG:
  Write docs/brain/B113/06-deferred-backlog.md.
  Must include:
    - B113-DEFER-01: Director F5 NT8 Compilation Gate
    - B113-DEFER-02: Live re-test Combo D (QX-ALL then BE-ALL — verify T1/T2/T3 all placed, no re-arm)
    - B113-DEFER-03: Live re-test Combo C (BE-ALL then QX-ALL — verify DW-B112 guard + no missing PTT-QX-T*)
    - Carry-forward: DW-B115 (ATM T1 qty mismatch — P1, Director triage required)

OUTPUT:
  docs/brain/B113/05-final-review.md — gate result: PIPELINE_COMPLETE
  docs/brain/B113/06-deferred-backlog.md — deferred items listed
  specs/002-trade-copier-spec.html — #section-dw-b117 updated to CLOSED B113-T1

---

## END OF B113 PIPELINE PROMPT

Orchestrator: start Ph1 now. When Ph1 produces docs/brain/B113/02-architecture-plan.md,
start Ph2. Continue the chain above. Do not skip any phase. Do not combine phases.
