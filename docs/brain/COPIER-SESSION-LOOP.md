# PTT Copier Session Loop — Perpetual $continue Protocol

**Version**: 2.2
**Created**: 2026-09-01
**Updated**: 2026-09-07 (V2.2 — PIPELINE EXECUTION RULES: 1-agent-per-ticket + lane-split gate)
**Purpose**: Every copier-spec session begins with a $continue prompt.
Each session validates pipeline output, runs SIM gates, updates the spec,
and ALWAYS ends by delivering the pipeline prompts AND the next $continue prompt
together as a single hand-off packet — so the Director has everything needed
in one place when testing completes.

---

## Loop Structure (every session does this in order)

```
SESSION START:   paste $continue prompt
  STEP 0:  Rules Catalog Gate (always mandatory)
  STEP 1:  Validate pipeline output from previous block (read source + verify docs)
  STEP 2:  Sync + F5 (ptt-sync-and-verify.ps1 + NinjaTrader recompile)
  STEP 3:  Update spec (close DW items, add pipeline section, update safety)
  STEP 4:  Provide SIM gate instructions. THEN STOP AND WAIT.
           !! SESSION PAUSES HERE — DO NOT PROCEED TO STEP 5 !!
           Director runs tests in NT8 SIM and reports actual results
           back in the same session chat.
  ── Director reports SIM results ──────────────────────────────────────────
  STEP 5:  Final spec updates — based on ACTUAL SIM results reported by Director.
           Log gate results in spec, deferred cards, DW-B134-OCO obs, new DW items.
  STEP 6:  Commit spec updates.
  STEP 7:  HAND-OFF PACKET (delivered together, never split):
           Part A — ptt-orchestrator prompts for the CONFIRMED next pipeline(s).
                    One prompt per confirmed action. No speculative branches.
                    Full 7-phase chain verbatim in every prompt.
           Part B — next $continue prompt, pre-filled from actual outcomes.
                    Director pastes Part A prompts into pipeline sessions.
                    Director pastes Part B into THIS session when pipelines complete.
SESSION END:     Director has Part A (pipelines to run) + Part B ($continue) in hand.
```

### Hard Rule: STEP 7 Is Post-SIM Only

The STEP 7 hand-off packet MUST NOT be generated before STEP 5 completes.
STEP 5 MUST NOT run before the Director reports SIM results.
Generating speculative prompts before real results are known is PROHIBITED.

The correct sequence is:
  STEPS 0-3 run immediately (automated, no Director input needed).
  STEP 4 outputs the SIM test instructions, then the session WAITS.
  Director runs tests, returns to the chat, reports PASS/FAIL/OBS/DEFER per gate.
  STEPS 5-7 run immediately after the Director's report, using real data.

### Why Pipeline Prompts and $continue Travel Together

The $continue prompt references the outcome of the pipelines that are about to run.
If the $continue is delivered before the pipelines, the Director holds a prompt that
refers to work not yet done — it cannot be used yet and creates confusion about
when to paste it. Delivering both together means:
  - Director pastes pipeline prompts → pipelines run → $continue is already in hand → paste.
  - No "where is my $continue?" between sessions.
  - No risk of using a stale $continue from a prior session.

---

## Loop Termination Criteria

The loop ends when ALL of the following are true:

1. **P0 + P1 defects**: zero open P0 or P1 DW items in the spec
2. **Deferred gates**: all deferred SIM gates resolved or explicitly archived
   (Director sign-off required to archive a deferred gate permanently)
3. **Complexity refactor wave**: autonomous-refactor wave complete —
   all CopyEngine.cs methods CYC <= 8 (Jane Street strict standard)
4. **Stable validation**: 5 consecutive live trading sessions with COPY ON,
   no new defects logged, no unexpected follower behavior observed
5. **Spec marked STABLE**: safety section updated to SAFE (no caveats),
   spec version bumped to STABLE in the header

Until all 5 conditions are true: the loop continues. A $continue prompt
is always provided at the end of every session.

---

## Loop Phase Map (updated 2026-09-04)

```
PHASE 1 — Active defect fix (P1 items)         [COMPLETE — B131]
  B131: DW-B138 (LaneA CLOSED) + DW-B139 (LaneB CLOSED)
  B131 SIM: PENDING (Tests A-E not yet run)

PHASE 2 — Residual defect fix (conditional)    [CURRENT]
  B132 LaneA: DW-B134-OCO fix (if orphan confirmed in B131 Test C)
  B132 LaneB: any new defects from B131 SIM Tests A-E
  If no new defects: advance to Phase 3

PHASE 3 — Backlog P1 items (when copier stable) [QUEUED]
  DW-B115: ATM target-qty distribution mismatch
  DW-B120: partial ATM arm on late-filling followers
  DW-B126: BE/QX race condition
  DW-B127: QX fires on position entry transition
  DW-B131: bracket-event storm (P2 — lower priority)

PHASE 4 — Complexity refactor wave              [QUEUED — after Phase 2/3]
  autonomous-refactor: CopyEngine.cs all methods CYC <= 8
  Many methods above CYC 8: OnOrderUpdate, TryCopyEntry,
  SyncAtmFollowerBracket, HandleBracketChange, etc.

PHASE 5 — Stable validation                    [FINAL]
  5 consecutive live sessions, COPY ON, zero defects
  Spec marked STABLE. Loop ends.
```

---

## Hand-Off Packet Rules (V2.1)

The STEP 7 hand-off packet is generated at the end of the session, after STEP 5+6.
It is ALWAYS grounded in actual SIM results from the current session.
It is NEVER generated speculatively before SIM results are known.
It is ALWAYS delivered as one unit — Part A (pipeline prompts) + Part B ($continue).

### Part A — ptt-orchestrator prompts
One prompt per confirmed next action. No "if Test A passes do X, if fails do Y".
Each prompt must include:
  - Full 7-phase PTT pipeline chain verbatim
  - SRC CODE BAN reminder
  - Block number (B133, B134, etc.)
  - DW item being fixed
  - Key source locations for that fix

### Part B — $continue prompt
Parameterized fields filled in from actual session outcomes:
- {BLOCK_JUST_VALIDATED}: block whose pipeline was verified this session
- {LANES_COMPLETED}: lanes verified (pipeline audit, STEP 1)
- {DW_FIXED}: DW items closed this session (pipeline close + SIM confirmation)
- {SIM_RESULTS}: actual Test A/B/C/D/E outcomes from STEP 5
- {DW_OPEN_P0P1}: remaining open P0/P1 items after STEP 5
- {DEFERRED_CARRY}: gates that were DEFERed in STEP 5
- {PHASE}: current loop phase number
- {NEXT_PIPELINE}: the specific pipeline that was just handed off in Part A

The Director pastes Part A prompts into pipeline sessions.
When all Part A pipelines finish, the Director pastes Part B into this session.

---

## SRC CODE BAN (permanent — all sessions)

No .cs edits in any copier-spec session.
All .cs changes go through the FULL 5-PHASE PTT PIPELINE:
  Ph1  ptt-architect       -> 02-architecture-plan.md
  Ph2  ptt-plan-reviewer   -> 02-plan-review.md       (REVIEW_PASS gate)
  Ph3  ptt-architect       -> 04-tickets.md
  Ph3.5 ptt-ticket-reviewer -> 04-ticket-review.md   (TICKET_REVIEW_PASS gate)
  Ph4a ptt-engineer        -> src .cs edits + ticket-N-completion.md
  Ph4b ptt-verifier        -> ticket-N-verification.md (VERIFY_PASS gate)
  Ph5  ptt-plan-reviewer   -> 05-final-review.md + 06-deferred-backlog.md

"add it" = add to spec HTML only. Never edit src code.

---

## PIPELINE EXECUTION RULES (V2.2 — MANDATORY)

### Rule 1: One Agent Per Ticket (Ph4a + Ph4b) — NON-NEGOTIABLE

Each Ph4a (engineer) ticket execution MUST be a separate independent agent session.
Each Ph4b (verifier) ticket verification MUST be a separate independent agent session.

BANNED: One ptt-engineer session reading ticket-1 AND ticket-2 and implementing both.
BANNED: One ptt-verifier session verifying ticket-1 AND ticket-2 in sequence.

CORRECT:
  Ticket 1: start_subtask(ptt-engineer) [reads ONLY ticket 1] -> PASS
            start_subtask(ptt-verifier) [verifies ONLY ticket 1] -> PASS
  Ticket 2: start_subtask(ptt-engineer) [reads ONLY ticket 2] -> PASS
            start_subtask(ptt-verifier) [verifies ONLY ticket 2] -> PASS

Why: Mixed-ticket sessions share context, causing cross-contamination of scope,
incomplete scans, and verification gaps that compound silently across blocks.
Each agent must have a clean isolated context for exactly one ticket.

Full protocol: docs/protocol/PHASE5_EXECUTION_PROTOCOL.md

### Rule 2: Lane-Split Decision Gate — Answer ALL 4 before splitting

Before deciding to split a fix into two pipeline lanes (LaneA + LaneB),
answer ALL four questions. Lanes require Q3 AND Q4 to be YES. Any NO = single pipeline.

  Q1. Do all fixes touch the same method OR within 50 lines of each other?
      YES -> single pipeline. STOP. (co-located changes must be reviewed together)

  Q2. Does the architect need to know Fix A's final design to correctly design Fix B?
      YES -> single pipeline. STOP. (design dependency means sequential, not parallel)

  Q3. If one fix is blocked at review, does the other still have standalone value?
      NO  -> single pipeline. STOP. (neither fix delivers value alone)

  Q4. Do all fixes need to be present together for the SIM gate to be meaningful?
      YES -> single pipeline. STOP. (SIM requires both — run them together)

  DEFAULT: single pipeline.
  LANES: require affirmative answers on Q3 AND Q4 with NO to Q1 AND Q2.

Retrospective calibration (empirical from B129-B133):
  B129: Lanes CORRECT  — different methods, different call trees, independent SIM paths
  B130: Lanes CORRECT  — LaneB was diagnostic only (zero .cs logic change)
  B131: Should have been single pipeline — same method, neither fix useful alone
  B132: Lanes CORRECT  — LaneB diagnostic/read-only, zero .cs change
  B133: Should have been single pipeline — both edits in FindFollowerBracketOrder,
        36 lines apart, CYC must be assessed together, both required before SIM

Full protocol: docs/protocol/PTT_PIPELINE_LANE_SPLIT_PROTOCOL.md

---

## Key Source Locations (stable reference — update if methods move)

```
CopyEngine.cs
  OnOrderUpdate            ~L1296  : entry point for all order events
  TryHandleDrag            ~L1734  : Gate B+C drag routing
  TryHandleBracketDrag     ~L1720  : Gate B — bracket drag
  IsWorkingBracket         ~L2083  : state guard for bracket detection
  IsBracketLegStatic       ~L3800  : name predicate for bracket orders
  IsAtmSTPOrder            ~L2107  : name predicate for ATM STP/Stop/Target
  HandleBracketChange      ~L2315  : fan-out to followers for bracket drag
  SyncFollowerBracket      ~L2131  : per-follower bracket sync
  FindFollowerBracketOrder ~L2339  : locate follower bracket by FromEntrySignal
  SyncAtmFollowerBracket   ~L2202  : cancel+resubmit for ATM stop brackets
  SyncAtmFollowerTarget    ~L2262  : cancel+resubmit for ATM target brackets
  TryCancelFollowerEntries ~L1638  : leader cancel propagation (scoped by order ID)
  MoveStopToBreakEven      ~L3922  : BE-ALL step A — calls SnapshotBeTargets
  SnapshotBeTargets        (new)   : filters stale PTT-BE-Target-* orders

specs/002-trade-copier-spec.html  : primary spec
docs/brain/B{N}/                  : per-block pipeline artifacts
docs/standards/NT8_FULL_REFERENCE.md     : NT8 API reference (102.7 KB)
docs/standards/NT8_ADDON_KNOWLEDGE.md   : AddOn-specific constraints (37.3 KB)
docs/standards/jane-street/RULES_CATALOG.md : JS-001..JS-110
```

---

## NT8 Key Facts (do not re-investigate)

- AtmStrategyChangeStopTarget() — StrategyBase-only. NOT AddOnBase.
- AtmStrategyCreate() — StrategyBase-only. NOT AddOnBase.
- Account.Change() — AddOnBase-available but silent no-op on ATM-owned brackets.
- Account.Cancel() + Account.CreateOrder() + Submit() — AddOnBase correct pattern.
- ATM bracket Order.Name = template name ("Stop1", "Target3" etc.).
- ATM bracket drag fires OnOrderUpdate with OrderState.ChangeSubmitted -> Working.
- NT8 Close button fires Order.Name = "Close".
- Copier resets to COPY OFF on every NT8 restart (by design, not a bug).

---

## Dev Setup (stable reference)

```
%NT8UserDataDir%\PropTraderTools\dev_mode.txt
%NT8UserDataDir%\PropTraderTools\license.txt       (key = "DEV")
%NT8UserDataDir%\PropTraderTools\license_cache.json (Elite features cached)
Session startup: select chart -> check follower boxes -> click COPY ON
SIM accounts: Sim101 (leader), Sim102/103/104 (followers)
ATM template: MES $200 SL 6 (bracket names: Stop1/Stop2/Stop3, Target1/Target2/Target3)
```

---

## Sync + F5 Gate (mandatory after every pipeline)

```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
# Gate: 0 MISMATCH
# Then: F5 in NinjaTrader 8 -> confirm green compile
# Then: dotnet test --filter B{N} -> all tests green
```
