---
description: Wave 7 Top-Level Orchestrator (Tier 1). Starts the sequential chain of 10 Phase Orchestrators. Each Phase Orchestrator runs as a start_subtask, spawns 161 parallel epic workers, enforces 100% completion, then start_subtask hands off to the next phase. Final phase reports WAVE_COMPLETE back here.
argument-hint: "[--reset] [--resume-phase PHASE]"
---

# AUTONOMOUS REFACTOR — WAVE 7 TOP-LEVEL ORCHESTRATOR (TIER 1)

**Mode:** `autonomous-refactor`
**Architecture:** 3-Tier Subagent Chain (V2.4)
**Goal:** All 161 methods CYC > 8 → CYC ≤ 8, Jane Street strict standard

---

## YOUR ROLE (Tier 1)

You are the **entry point** only. You do NOT spawn 161 workers directly.
You spawn **one Phase Orchestrator at a time** using `start_subtask`, then wait.
Each Phase Orchestrator is responsible for its own 161 workers AND for chaining to the next phase.

---

## EXECUTION PROTOCOL

### Step 1: Verify Prerequisites
```
- Read docs/brain/wave7-epic-list.json → confirm 161 entries
- Verify .lamport/wave7/event_log.jsonl exists
- Confirm docs/brain/EPIC-W7-* directories exist (161 total)
```

### Step 2: Log Wave Start
Append to `.lamport/wave7/event_log.jsonl`:
```json
{"timestamp":"<ISO8601>","lamport_clock":1,"epic_id":"WAVE-7","phase":"init","tier":"top_orch","event_type":"wave_start","status":"running","note":"Wave 7 start — 161 epics, Phase 0 first"}
```

### Step 3: Launch Phase 0 Orchestrator via start_subtask
```
start_subtask(
  title: "Wave 7 — Phase 0 Orchestrator",
  mode: "wave-orch-phase0",
  message: |
    You are the Phase 0 (Hotspot Analysis) Orchestrator for Wave 7.
    Epic list: docs/brain/wave7-epic-list.json (161 epics)
    Lamport log: .lamport/wave7/event_log.jsonl
    
    Execute your full protocol:
    1. Spawn 161 v12-phase0-hotspot workers in parallel (spawn_subagent)
    2. Enforce 100% completion (retry loop up to 3 rounds)
    3. On VERIFIED_COMPLETE: start_subtask the Phase 1 Orchestrator
    4. The chain continues automatically through all 10 phases
    5. Phase 6 Orchestrator will report WAVE_COMPLETE back
    
    See your roleDefinition and docs/workflow/PHASE_ORCHESTRATOR_TEMPLATES.md for full protocol.
)
```

### Step 4: Wait for WAVE_COMPLETE
- The subtask chain runs autonomously: Ph0 → Ph1 → Ph1.5 → Ph2 → Ph3 → Ph4 → Ph4.5 → Ph5 → Ph5.V → Ph6
- Each phase orchestrator hands off via start_subtask to the next
- Phase 6 Orchestrator returns WAVE_COMPLETE to you

### Step 5: On WAVE_COMPLETE — Verify CYC
```
- Log wave_epic_complete to .lamport/wave7/event_log.jsonl
- Verify: python scripts/complexity_audit.py | grep "REFACTOR" | wc -l → should be 0
- Report: "Wave 7 epics complete — 161/161 methods now CYC ≤ 8"
```

### Step 5b: Launch Phase 7 (PR Review & Merge Loop)

Phase 7 handles all cluster PRs created after the CYC reduction wave.
It runs AFTER Phase 6 confirms all epics complete (CYC ≤ 8 verified).

```
start_subtask(
  mode: "wave-orch-phase7",
  title: "Wave 7 — Phase 7 PR Review & Merge Loop",
  message: |
    You are the Phase 7 (PR Review & Merge Loop) Coordinator for Wave 7.

    LAMPORT GATE: Verify phase_6_orchestrator_complete in
      .lamport/wave7/event_log.jsonl before proceeding.

    MANIFEST: docs/brain/wave7-pr-repairs/manifest.json
      Contains 6 cluster PRs (lanes L1–L6).

    YOUR JOB:
      1. Read manifest, verify all 6 PR branches exist on remote.
      2. Produce 6 lane prompts (start_subtask blocks) for the Director.
         Director will open 6 Bob IDE tabs and paste one prompt per tab.
         Each lane runs mode="wave-orch-phase7-lane" on one PR.
      3. Collect LANE_COMPLETE / LANE_HARD_FAILURE from Director as lanes finish.
      4. When all 6 lanes complete: log MERGE_COMPLETE, report back here.

    ARCHITECTURE (Option B — Director-pasted lanes):
      - Tier 2 (you): produce prompts, collect results, log
      - Tier 3 (wave-orch-phase7-lane): one per PR, sequential inside, parallel across tabs
      - Workers inside each lane: v12-phase2-architecture (logic planner) + v12-engineer (fixer)

    BRANCH HYGIENE (enforce in every lane message):
      - src/ edits → PR branch only
      - docs/ artifacts → main only
      - NEVER mix in one commit

    Report back: MERGE_COMPLETE wave=7 prs_merged=N prs_needs_director=M
)
```

### Step 6: On MERGE_COMPLETE
```
- Log wave_7_complete to .lamport/wave7/event_log.jsonl:
  {"lamport_clock": N, "epic_id":"WAVE-7", "phase":"final",
   "event_type":"wave_7_complete", "status":"complete",
   "epics": 161, "prs_merged": 6}
- Report: "Wave 7 complete — 161/161 CYC ≤ 8, 6/6 PRs merged"
```

---

## HARD_FAILURE HANDLING

If any phase reports `HARD_FAILURE`:
1. Log to `.lamport/wave7/event_log.jsonl`
2. Write `docs/brain/wave7-incident-report.md` with stuck epic list
3. HALT — do NOT proceed
4. Report to Director for manual resolution
5. After resolution: re-start_subtask that specific Phase Orchestrator only

---

## PHASE CHAIN MAP

| Phase | Orchestrator Mode | Worker Mode | Hands Off To |
|-------|-------------------|-------------|--------------|
| 0 | `wave-orch-phase0` | `v12-phase0-hotspot` | `wave-orch-phase1` |
| 1 | `wave-orch-phase1` | `v12-phase1-scope` | `wave-orch-phase1-5` |
| 1.5 | `wave-orch-phase1-5` | `v12-phase1-5-boundary` | `wave-orch-phase2` |
| 2 | `wave-orch-phase2` | `v12-phase2-architecture` | `wave-orch-phase3` |
| 3 | `wave-orch-phase3` | `v12-phase3-audit` | `wave-orch-phase4` |
| 4 | `wave-orch-phase4` | `v12-phase4-tickets` | `wave-orch-phase4-5` |
| 4.5 | `wave-orch-phase4-5` | `v12-phase4-5-review` | `wave-orch-phase5` |
| 5 | `wave-orch-phase5` | `v12-engineer` | `wave-orch-phase5v` |
| 5.V | `wave-orch-phase5v` | `v12-phase5-v-verify` | `wave-orch-phase6` |
| 6 | `wave-orch-phase6` | `v12-phase6-review` | → WAVE_COMPLETE to Tier 1 |
| 7 | `wave-orch-phase7` | `wave-orch-phase7-lane` (×6) | → MERGE_COMPLETE to Tier 1 |

**Phase 7 Note:** 6 lanes run in parallel across Director-pasted tabs.
Each lane uses `start_subtask(mode="wave-orch-phase7-lane")` internally.
Workers inside each lane: `v12-phase2-architecture` (logic planner) + `v12-engineer` (fixer).

---

## CRITICAL RULES

- **NEVER** spawn 161 workers directly from Tier 1 — delegate to Phase Orchestrators
- **NEVER** use spawn_subagent for Phase Orchestrators — use `start_subtask`
- **NEVER** skip a phase — each phase's output is the next phase's input
- **xUnit ONLY**: `[Fact]`, `Assert.Equal()` — NEVER NUnit/MSTest
- **UTF-8**: All files UTF-8 (no BOM)
- **Workers**: Bob IDE V2 spawns subagents internally — no external API keys needed

## REFERENCE
- Epic list: `docs/brain/wave7-epic-list.json`
- Templates: `docs/workflow/PHASE_ORCHESTRATOR_TEMPLATES.md`
- Matrix: `docs/workflow/AUTONOMOUS_REFACTOR_INTEGRATION_MATRIX_V2.md`
- Lamport: `.lamport/wave7/event_log.jsonl`

---

## GRAPHIFY PROTOCOL (MANDATORY — Every Task)

**STARTUP** (run this as your FIRST action before any exploration):
```bash
graphify update . --no-cluster --no-description
```
Then read `.graphify/GRAPH_REPORT.md` for god nodes and community structure.

**SHUTDOWN** (run this as your LAST action after any file edits):
```bash
graphify update . --no-cluster --no-description
```
