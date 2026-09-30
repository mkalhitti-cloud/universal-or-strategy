# B76-LaneA — ptt-orchestrator Prompt (UPDATED 2026-08-18)
# All Ph1..Ph3.5 documents are complete. Start at Ph4a.
# Paste the Ph4a section into a ptt-engineer session to begin.

---

## CONTEXT — What happened before this prompt

Direct-engineer pre-pipeline test was run (Director-authorized) on 2026-08-18.
3 bugs were found and fixed live during the test session:

| Fix ID | File | Status |
|--------|------|--------|
| HOTFIX-B76-FLATTEN-GUARD-01 v2 | CopyEngine.cs FlattenOneAccount | APPLIED + LIVE VERIFIED |
| HOTFIX-B76-FLATTEN-RACE-01 | CopyEngine.cs FlattenOneAccount | APPLIED + LIVE VERIFIED |
| HOTFIX-B76-POSSTATE-DEDUP-01 | CopyEngine.cs TryFirePositionState | APPLIED + LIVE VERIFIED |
| HOTFIX-B76-POSSTATE-LEAK-01 | TradeCopierAddOn.cs DoInject | APPLIED |
| HOTFIX-B76-POSSTATE-LEAK-02 | TradeCopierWindow.cs OnLoaded | APPLIED |
| HOTFIX-B76-ATM-TPL-CLASSNAME | TradeCopierPanel.cs GetLeaderAtmTemplateName | **NOT YET APPLIED** |

Pipeline documents (all pre-gated PASS):
- `docs/brain/B76-LaneA/02-architecture-plan.md` ✅
- `docs/brain/B76-LaneA/02-plan-review.md` REVIEW_PASS ✅
- `docs/brain/B76-LaneA/04-tickets.md` ✅
- `docs/brain/B76-LaneA/04-ticket-review.md` TICKET_REVIEW_PASS ✅
- `docs/brain/B76-LaneA/06-deferred-backlog.md` ✅

## THE PIPELINE IS (all phases mandatory -- none skippable -- none combinable):

  Ph1  ptt-architect       -> 02-architecture-plan.md               ✅ DONE
  Ph2  ptt-plan-reviewer   -> 02-plan-review.md (REVIEW_PASS)       ✅ DONE
  Ph3  ptt-architect       -> 04-tickets.md                         ✅ DONE
  Ph3.5 ptt-ticket-reviewer -> 04-ticket-review.md (TICKET_REVIEW_PASS) ✅ DONE
  Ph4a ptt-engineer        -> src .cs edits + ticket-N-completion.md   🔲 TODO
  Ph4b ptt-verifier        -> ticket-N-verification.md (VERIFY_PASS)   🔲 TODO
  Ph5  ptt-plan-reviewer   -> 05-final-review.md + 06-deferred-backlog.md  🔲 TODO

---

## Ph4a — PASTE INTO ptt-engineer session

```
PTT PIPELINE Ph4a — B76-LaneA Engineer

Workspace: C:\WSGTA\universal-or-strategy (main branch)
SRC CODE BAN is LIFTED for Ph4a only.

Read these documents first (mandatory):
  docs/brain/B76-LaneA/04-tickets.md
  docs/brain/B76-LaneA/04-ticket-review.md

Key context:
  - TICKETS 1 and 2: Code is ALREADY LIVE-APPLIED. Your job is TESTS ONLY for these two.
  - TICKET 3: Code is NOT YET APPLIED. Apply the change, then write tests.

════════════════════════════════════════════════════════════
TICKET-B76-1 (tests only, no code change)
════════════════════════════════════════════════════════════

Read src/PropTraderTools/CopyEngine.cs lines 1861-1932 (FlattenOneAccount).
Confirm these strings exist in the body:
  - "flat-guard: in-flight skip"
  - "flat-race skip"
  - Two FindPosition call sites

Create src/PropTraderTools/Tests/B76Tests.cs (NEW FILE) with namespace matching existing
B7x test files (check B70Tests.cs or B71Tests.cs for the correct namespace).

Write [Fact] tests T_B76_01..T_B76_06:
  T_B76_01: FlattenOneAccount exists as BindingFlags.NonPublic | Instance on CopyEngine
  T_B76_02: FlattenOneAccount IL body contains string "flat-guard: in-flight skip"
  T_B76_03: FlattenOneAccount IL body contains string "flat-race skip"
  T_B76_04: FlattenOneAccount IL contains >= 2 call sites for FindPosition
             (scan GetMethodBody().GetILAsByteArray() for the FindPosition token, same
              pattern as T_B67_01 in CopyEngineTests.cs which already does IL inspection)
  T_B76_05: In FlattenOneAccount IL, the IL offset of CancelAllAccountOrders call is LESS THAN
             the IL offset of the second FindPosition call
  T_B76_06: FlattenOneAccount GetMethodBody().LocalVariables.Count >= 5

Run dotnet test -- T_B76_01..T_B76_06 must pass, T_B67_01..T_B67_04 must still pass.

════════════════════════════════════════════════════════════
TICKET-B76-2 (tests only, no code change)
════════════════════════════════════════════════════════════

Read src/PropTraderTools/CopyEngine.cs lines 181-188 (_lastHasPos field).
Read src/PropTraderTools/CopyEngine.cs lines 1418-1444 (TryFirePositionState).
Confirm _lastHasPos field and Interlocked.Exchange are present.

Add to B76Tests.cs:
  T_B76_07: CopyEngine has a field named _lastHasPos (BindingFlags.NonPublic | Instance)
  T_B76_08: TryFirePositionState method IL contains a call to System.Threading.Interlocked.Exchange
             (scan GetILAsByteArray() for the Interlocked.Exchange method token)
  T_B76_09: TryFirePositionState is BindingFlags.NonPublic | Instance on CopyEngine

Run dotnet test -- T_B76_07..T_B76_09 must pass.

════════════════════════════════════════════════════════════
TICKET-B76-3 (code change + tests)
════════════════════════════════════════════════════════════

Read src/PropTraderTools/TradeCopierPanel.cs lines 2218-2238 (GetLeaderAtmTemplateName).

Apply this SURGICAL change using apply_diff (NOT write_file):

SEARCH (exact text at lines 2227-2228):
                if (ct.AtmStrategy != null)                                  // branch 3 -- primary path
                    return ct.AtmStrategy.Name ?? string.Empty;

REPLACE WITH:
                if (ct.AtmStrategy != null)                                  // branch 3 -- primary path
                {
                    var n = ct.AtmStrategy.Name ?? string.Empty;
                    // B76 HOTFIX-B76-ATM-TPL-CLASSNAME: "AtmStrategy" is the NT8 class name
                    // returned when no template is staged on ChartTrader -- not a user template.
                    // Observed live 2026-08-18: [PTT-CLONE] SetCloneAtmCache: 'AtmStrategy'.
                    // Fall through to AtmStrategySelector fallback for the real template name.
                    if (n.Length > 0 && n != "AtmStrategy")
                        return n;
                }

JS-DNA check after edit:
  grep "lock(" src/PropTraderTools/TradeCopierPanel.cs  -- zero new matches
  grep "throw new" src/PropTraderTools/TradeCopierPanel.cs  -- zero new matches

Add to B76Tests.cs:
  T_B76_10: typeof(TradeCopierPanel).GetMethod("GetLeaderAtmTemplateName",
            BindingFlags.NonPublic | BindingFlags.Static)
            invoked with null argument returns string.Empty (not throws, not "AtmStrategy")
  T_B76_11: GetLeaderAtmTemplateName IL body contains the string literal "AtmStrategy"
            (confirm guard string is compiled in)
  T_B76_12: GetLeaderAtmTemplateName is BindingFlags.NonPublic | Static on TradeCopierPanel

Run dotnet build -- must pass zero errors.
Run dotnet test -- ALL 12 tests (T_B76_01..T_B76_12) must pass.
  Also: T_B43_04, T_B67_01..T_B67_04 must still pass (regressions).

════════════════════════════════════════════════════════════
AFTER ALL 3 TICKETS
════════════════════════════════════════════════════════════

Run: powershell -File scripts\sync-ptt-to-nt8.ps1
Output must show TradeCopierPanel.cs COPIED or in-sync.

JS-DNA FINAL SCAN (run these, report results):
  grep -n "lock(" src/PropTraderTools/CopyEngine.cs
  grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs
  grep -n "DIAG-" src/PropTraderTools/CopyEngine.cs
  grep -n "DIAG-" src/PropTraderTools/TradeCopierPanel.cs
All must return zero new matches.

Write docs/brain/B76-LaneA/ticket-1-completion.md
Write docs/brain/B76-LaneA/ticket-2-completion.md
Write docs/brain/B76-LaneA/ticket-3-completion.md

Each completion file must contain:
  - Ticket ID + title
  - Files read/modified
  - Tests written (T_B76_NN list)
  - Build result: PASS/FAIL
  - Test result: PASS/FAIL + count
  - Any gaps or surprises found
```

---

## Ph4b — PASTE INTO ptt-verifier session

```
PTT PIPELINE Ph4b — B76-LaneA Verifier

Read:
  docs/brain/B76-LaneA/ticket-1-completion.md
  docs/brain/B76-LaneA/ticket-2-completion.md
  docs/brain/B76-LaneA/ticket-3-completion.md
  docs/brain/B76-LaneA/04-tickets.md

Verify TICKET-B76-1 (FlattenOneAccount):
  [ ] Body contains "flat-guard: in-flight skip" string
  [ ] Body contains "flat-race skip" string
  [ ] Two FindPosition call sites present in IL
  [ ] CancelAllAccountOrders IL offset < second FindPosition IL offset
  [ ] T_B76_01..T_B76_06: all PASS in completion file

Verify TICKET-B76-2 (TryFirePositionState):
  [ ] _lastHasPos field exists and is ConcurrentDictionary<string,int[]>
  [ ] Interlocked.Exchange call site in TryFirePositionState IL
  [ ] T_B76_07..T_B76_09: all PASS in completion file

Verify TICKET-B76-3 (GetLeaderAtmTemplateName):
  [ ] Lines 2227-2228 replaced with class-name guard block in TradeCopierPanel.cs
  [ ] Guard string "AtmStrategy" present in method body
  [ ] T_B76_10..T_B76_12: all PASS in completion file
  [ ] Existing T_B43_04 + T_B67_01..T_B67_04 still PASS

Verify JS-DNA:
  [ ] No new lock() in any modified file
  [ ] No new throw new in any modified file
  [ ] No DIAG- lines in CopyEngine.cs or TradeCopierPanel.cs

Verify sync:
  [ ] sync-ptt-to-nt8.ps1 shows TradeCopierPanel.cs in-sync, no errors

Write docs/brain/B76-LaneA/ticket-1-verification.md (VERIFY_PASS or VERIFY_FAIL)
Write docs/brain/B76-LaneA/ticket-2-verification.md
Write docs/brain/B76-LaneA/ticket-3-verification.md
```

---

## Ph5 — PASTE INTO ptt-plan-reviewer session

```
PTT PIPELINE Ph5 — B76-LaneA Final Review

Read all B76-LaneA documents:
  02-architecture-plan.md, 02-plan-review.md
  04-tickets.md, 04-ticket-review.md
  ticket-1-completion.md, ticket-1-verification.md
  ticket-2-completion.md, ticket-2-verification.md
  ticket-3-completion.md, ticket-3-verification.md
  06-deferred-backlog.md

Final checklist:
  [ ] All 3 tickets verified (VERIFY_PASS)
  [ ] 12 [Fact] tests all pass, zero failures
  [ ] CYC ≤8 on all modified methods
  [ ] Zero JS-DNA violations
  [ ] Zero DIAG lines
  [ ] sync-ptt-to-nt8.ps1 clean
  [ ] 06-deferred-backlog.md has DW-B76-01 recorded

Write: docs/brain/B76-LaneA/05-final-review.md (FINAL_PASS or FINAL_FAIL + rationale)

Update: docs/brain/NO-PIPELINE-REPAIRS.md PIPELINE STATUS table — add row:
  | B76-LaneA | Flatten race+guard + PositionState dedup + ATM class-name |
  | CopyEngine.cs + TradeCopierPanel.cs + TradeCopierAddOn.cs + TradeCopierWindow.cs |
  | 5 hotfixes | 12 [Fact] | FINAL_PASS |

Final commit (only after FINAL_PASS):
  git add src/PropTraderTools/CopyEngine.cs
  git add src/PropTraderTools/TradeCopierPanel.cs
  git add src/PropTraderTools/Tests/B76Tests.cs
  git add docs/brain/B76-LaneA/
  git add docs/brain/NO-PIPELINE-REPAIRS.md
  git commit -m "feat(ptt): B76 FlattenOneAccount race+guard, PositionState dedup, ATM class-name fix [12 tests]"
  Report: commit hash + test count
```
