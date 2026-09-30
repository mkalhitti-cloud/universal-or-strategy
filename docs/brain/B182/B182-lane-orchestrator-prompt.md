# B182 Lane Orchestrator Prompt
## Wave: B182 -- BE-All QX-Pattern Fix + Safety Hardening

---

## PASTE THIS ENTIRE DOCUMENT into the lane orchestrator session.

---

## Context

You are the **Lane Orchestrator** for the B182 pipeline.

The full 5-phase PTT pipeline has been run in spec-only mode and all planning documents
are complete in `docs/brain/B182/`. The pipeline documents are:

```
docs/brain/B182/02-architecture-plan.md   -- Ph1 ptt-architect output
docs/brain/B182/02-plan-review.md         -- Ph2 ptt-plan-reviewer output (REVIEW_PASS)
docs/brain/B182/04-tickets.md             -- Ph3 ptt-architect output
docs/brain/B182/04-ticket-review.md       -- Ph3.5 ptt-ticket-reviewer output (TICKET_REVIEW_PASS)
docs/brain/B182/ticket-1-completion.md    -- Ph4a template (Ticket 1)
docs/brain/B182/ticket-2-completion.md    -- Ph4a template (Ticket 2)
docs/brain/B182/ticket-3-completion.md    -- Ph4a template (Ticket 3)
docs/brain/B182/ticket-1-verification.md  -- Ph4b template (Ticket 1)
docs/brain/B182/ticket-2-verification.md  -- Ph4b template (Ticket 2)
docs/brain/B182/ticket-3-verification.md  -- Ph4b template (Ticket 3)
docs/brain/B182/05-final-review.md        -- Ph5 ptt-plan-reviewer output (FINAL_PASS)
docs/brain/B182/06-deferred-backlog.md    -- Ph5 deferred items
```

Your job: **Execute Tickets 1 through 3 in order** using the ptt-engineer mode.

---

## Baseline State

Direct repair already applied (2026-09-28 direct session):
- `src/PropTraderTools/Features/PttBreakEvenSwap.cs` -- DW-B182-01 + DW-B182-02 applied
- `src/PropTraderTools/Features/PttBeSwapOne.cs` -- DW-B182-02 caller corrected
- `src/PropTraderTools/Features/PttBeSwapAll.cs` -- DW-B182-02 caller corrected
- `src/PropTraderTools/Tests/BwaveRefactorLaneCTests.cs` -- pre-existing compile fix

dotnet build: 0 errors, 1 pre-existing warning (xUnit2004 in B131Tests.cs).
SIM gate: PASS (2026-09-28).

---

## Mandatory Pipeline Chain (ALL PHASES MANDATORY -- NONE SKIPPABLE -- NONE COMBINABLE)

```
Ph1  ptt-architect       -> docs/brain/B182/02-architecture-plan.md     [DONE]
Ph2  ptt-plan-reviewer   -> docs/brain/B182/02-plan-review.md            [DONE -- REVIEW_PASS]
Ph3  ptt-architect       -> docs/brain/B182/04-tickets.md                [DONE]
Ph3.5 ptt-ticket-reviewer -> docs/brain/B182/04-ticket-review.md         [DONE -- TICKET_REVIEW_PASS]
Ph4a ptt-engineer        -> src .cs edits + ticket-N-completion.md       [EXECUTE NOW]
Ph4b ptt-verifier        -> ticket-N-verification.md                     [EXECUTE AFTER EACH Ph4a]
Ph5  ptt-plan-reviewer   -> docs/brain/B182/05-final-review.md           [DONE -- FINAL_PASS]
```

**Ph4a is the only remaining work.** Tickets 1-3 must be executed in order.
Each ticket must complete and be verified (Ph4b) before the next ticket starts.

---

## SRC CODE BAN REMINDER

You are the **ptt-engineer** executing Ph4a. You are the ONLY role that touches .cs files.
The pipeline already has REVIEW_PASS, TICKET_REVIEW_PASS, and FINAL_PASS from the planning docs.
This is Ph4a execution: implement exactly what is in `docs/brain/B182/04-tickets.md`.

**DO NOT re-apply the direct repair.** Ticket 1 is read-verify only. The repair is already in source.

---

## Execution Steps

### TICKET 1 -- B182-T1 (Tests only, no .cs Feature edits)

**Read first:**
```
docs/brain/B182/04-tickets.md  (Ticket 1 section)
```

**Steps:**
1. `dotnet build` -- confirm 0 errors.
2. `powershell -File .\scripts\ptt-sync.ps1` -- confirm 21/21 LINKED.
3. Read and verify direct repair in the 3 Feature files (read-only, NO EDITS):
   - `src/PropTraderTools/Features/PttBreakEvenSwap.cs` -- verify lines 115, 165, 193, no IsStopPriceSubmittable.
   - `src/PropTraderTools/Features/PttBeSwapOne.cs` -- verify SnapshotStopPrice before Execute.
   - `src/PropTraderTools/Features/PttBeSwapAll.cs` -- verify SnapshotStopPrice before Execute.
   If any check fails: STOP. Report discrepancy. Do NOT proceed.
4. Create `src/PropTraderTools/Tests/B182Tests.cs` with `B182BeFixTests` class (4 [Fact] methods).
   Use exact code from `04-tickets.md` Ticket 1 Step 2.
5. `dotnet build` -- must pass 0 errors.
6. `dotnet test --filter B182` -- must pass 4/4.
7. `powershell -File .\scripts\ptt-sync.ps1` -- must show 21/21 LINKED.
8. Update `docs/brain/B182/ticket-1-completion.md` with actual results.

**Ph4b verify:** Confirm ticket-1-verification.md checklist against actual results.

---

### TICKET 2 -- B182-T2 (CopyEngine.cs edit + test)

**Read first:**
```
docs/brain/B182/04-tickets.md  (Ticket 2 section)
src/PropTraderTools/CopyEngine.cs  lines 1359-1415
```

**Steps:**
1. Read `CopyEngine.cs` lines 1359-1415 (OnOrderUpdate pre-Gate-1 block). Note line numbers.
2. Add `TryEmitBeFlattenWarn` method to `CopyEngine.cs`. Use exact code from `04-tickets.md` Ticket 2 Step 3.
3. Wire `TryEmitBeFlattenWarn(e);` into `OnOrderUpdate` after `TrySweptPttDragOrphans(e)`.
   Add comment: `// DW-B179: emit [BE-WARN] when PTT-BE-* or PTT-QX-* order cancels and position is flat.`
4. Add `B182BeFlattenWarnTests` class (3 [Fact] + 1 skip stub) to `B182Tests.cs`.
   Use exact code from `04-tickets.md` Ticket 2 Step 5.
5. `powershell -File .\scripts\ptt-sync.ps1` -- must show 21/21 LINKED.
6. `dotnet build` -- must pass 0 errors.
7. `dotnet test --filter B182` -- must pass 7/7 (1 skipped).
8. Update `docs/brain/B182/ticket-2-completion.md` with actual results.

**Ph4b verify:** Confirm ticket-2-verification.md checklist against actual results.

---

### TICKET 3 -- B182-T3 (CopyEngine.cs + TradeCopierAddOn.cs edits + test)

**Read first:**
```
docs/brain/B182/04-tickets.md  (Ticket 3 section)
src/PropTraderTools/CopyEngine.cs  (find existing HashSet fields section)
src/PropTraderTools/TradeCopierAddOn.cs  lines 60-82 (OnStateChange / State.Configure)
```

**Steps:**
1. Read `TradeCopierAddOn.cs` lines 60-82. Note the State.Configure block structure.
2. Read `CopyEngine.cs` to find appropriate location for new field + methods.
3. Add `_warnedAtmTemplates` field to `CopyEngine.cs`. Use exact code from `04-tickets.md` Ticket 3 Step 1.
4. Add both `CheckAtmTemplatesForReverse` overloads to `CopyEngine.cs`. Use exact code from `04-tickets.md` Ticket 3 Step 1.
5. In `TradeCopierAddOn.cs` State.Configure block: add `CopyEngine.Instance.CheckAtmTemplatesForReverse(); // DW-B181-G5`
   after `CopyEngine.Instance.SetFlags(flags);`.
6. Add `B182AtmTemplateScanTests` class (3 [Fact]) to `B182Tests.cs`.
   Use exact code from `04-tickets.md` Ticket 3 Step 3.
7. `powershell -File .\scripts\ptt-sync.ps1` -- must show 21/21 LINKED.
8. `dotnet build` -- must pass 0 errors.
9. `dotnet test --filter B182` -- must pass 10/10 (1 skipped).
10. `dotnet test` (full suite) -- no regressions.
11. Update `docs/brain/B182/ticket-3-completion.md` with actual results.

**Ph4b verify:** Confirm ticket-3-verification.md checklist against actual results.

---

## Final Gate After All 3 Tickets

```powershell
dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj
# Expected: 0 errors, 1 pre-existing warning

dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj
# Expected: all tests pass, no regressions, B182 10 pass + 1 skip

powershell -File .\scripts\ptt-sync.ps1
# Expected: 21/21 LINKED
```

If all pass: **B182 COMPLETE. Director may proceed to live trading gate (NT8 F5 recompile).**

---

## Constraints (Non-Negotiable)

- JS-001: no throw in new methods (try/catch only)
- JS-002: no return null
- JS-021: no lock
- JS-033: no async void
- CYC <= 8 per method
- ASCII-only string literals
- NT8-014: [BE-WARN], [PTT-STARTUP-WARN], [PTT-STARTUP-INFO] prefixes
- ptt-sync.ps1 AFTER EVERY .cs edit
- dotnet build AFTER EVERY ticket
- DO NOT re-apply direct repair (verify only in T1)
- DO NOT skip any ticket
- DO NOT combine tickets

---

## Deferred (Out of Scope for B182)

DW-B181-G3 (concurrent flatten test) -- B183.
DW-B181-G1/G2 (event burst / price divergence) -- B183/B184.
DW-B182-WARN-DEDUP (log dedup for TryEmitBeFlattenWarn) -- B183.
