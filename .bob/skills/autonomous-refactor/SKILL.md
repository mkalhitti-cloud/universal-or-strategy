---
name: autonomous-refactor
description: >-
  Wave orchestrator for autonomous complexity refactoring (Bob IDE V2). 3-tier
  subagent architecture: 1 top-level orchestrator spawns 10 phase orchestrators
  sequentially; each phase orchestrator spawns 161 epic workers in parallel.
  100% completion enforced per phase before hand-off. All 161 methods reach CYC
  <= 8 (Jane Street strict standard).
metadata:
  user-invocable: true
  disable-model-invocation: false
  argument-hint: '[--phase PHASE] [--start-epic EPIC-W7-NNN] [--target-cyc N]'
---

# AUTONOMOUS REFACTOR — WAVE ORCHESTRATOR

**Protocol:** V12.28 Bob IDE V2 — 3-Tier Subagent Architecture
**Goal:** All 161 methods CYC > 8 → CYC ≤ 8, Jane Street strict standard

---

## ARCHITECTURE: 3-TIER SUBAGENT TREE

```
Tier 1: Top-Level Orchestrator (THIS SESSION — autonomous-refactor mode)
   |
   |-- spawns SEQUENTIALLY (wait for VERIFIED_COMPLETE before next phase)
   |
   +--> Phase 0 Orchestrator  (autonomous-refactor)  → spawns 161 workers → v12-phase0-hotspot
   +--> Phase 1 Orchestrator  (autonomous-refactor)  → spawns 161 workers → v12-phase1-scope
   +--> Phase 1.5 Orchestrator (autonomous-refactor) → spawns 161 workers → v12-phase1-5-boundary
   +--> Phase 2 Orchestrator  (autonomous-refactor)  → spawns 161 workers → v12-phase2-architecture
   +--> Phase 3 Orchestrator  (autonomous-refactor)  → spawns 161 workers → v12-phase3-audit
   +--> Phase 4 Orchestrator  (autonomous-refactor)  → spawns 161 workers → v12-phase4-tickets
   +--> Phase 4.5 Orchestrator (autonomous-refactor) → spawns 161 workers → v12-phase4-5-review
   +--> Phase 5 Orchestrator  (autonomous-refactor)  → spawns 161 workers → v12-engineer
   +--> Phase 5.V Orchestrator (autonomous-refactor) → spawns 161 workers → v12-phase5-v-verify
   +--> Phase 6 Orchestrator  (autonomous-refactor)  → spawns 161 workers → v12-phase6-review
```

**Total agents in flight at peak:** 1 top + 1 phase orch + 161 workers = 163 concurrent agents per phase.

---

## TIER 1: TOP-LEVEL ORCHESTRATOR PROTOCOL

1. Read `docs/brain/wave7-epic-list.json` — all 161 epic IDs + metadata
2. Create `.lamport/wave7/event_log.jsonl` (if not exists)
3. Spawn Phase Orchestrators **sequentially** using templates in `docs/workflow/PHASE_ORCHESTRATOR_TEMPLATES.md`
4. Each Phase Orchestrator receives:
   - Phase number + mode map
   - Full epic list (161 entries)
   - Jane Street KB query results (for phases that require them)
5. **WAIT** for `"status": "VERIFIED_COMPLETE"` before spawning next phase orchestrator
6. On `HARD_FAILURE`: write incident report → escalate to Director → do NOT proceed to next phase

---

## TIER 2: PHASE ORCHESTRATOR PROTOCOL

Each Phase Orchestrator (`autonomous-refactor` mode) runs this loop:

```
STEP 1: Verify prerequisite artifacts from previous phase (all 161 exist)
         If missing → HALT → report "prerequisite not met" to Tier 1

STEP 2: (Phases 2, 4.5, 5, 5.V, 6 only) Run Jane Street KB queries:
         python scripts/query_kb.py "<relevant term>"
         Capture results to include in worker descriptions

STEP 3: Spawn all 161 epic workers SIMULTANEOUSLY (no delay needed)
         mode: <phase-specific custom mode>
         description: <see PHASE_ORCHESTRATOR_TEMPLATES.md for exact payload>

STEP 4: COMPLETION VERIFICATION LOOP (MANDATORY — 100% enforcement):
         Round 1: collect all worker results
         If < 161/161 success:
           Log failures to .lamport/wave7/event_log.jsonl
           Write failure-analysis.md for each failed epic
           Re-spawn ONLY the failed workers (Round 2)
         Repeat up to 3 rounds
         After 3 rounds: if still incomplete → HARD_FAILURE → report to Tier 1

STEP 5: Final validation checks (phase-specific — see templates)
         e.g. Phase 1.5: boundary_verdict=PASS for all 161
              Phase 5: cyc_achieved <= 8 + build_passed for all 161
              Phase 5.V: verification_verdict=PASS (independent check)

STEP 6: Report VERIFIED_COMPLETE to Tier 1
```

---

## TIER 3: EPIC WORKER SPAWN PATTERN

Each epic worker receives a clean-context subagent spawn:

```
spawn_subagent(
  mode: <phase-specific custom mode slug>
  description: |
    Epic: EPIC-W7-NNN
    Method: [MethodName] (CYC: N)
    Source: src/V12_002.*.cs
    Input: docs/brain/EPIC-W7-NNN/[previous-phase-output].md
    Task: Read input. Execute phase work.
          Write output to docs/brain/EPIC-W7-NNN/[output].md
          Return: {status, output_path, <phase-specific metrics>}
)
```

### Phase → Custom Mode Map

| Phase | Custom Mode Slug | Output Artifact | 100% Check |
|-------|-----------------|-----------------|------------|
| 0 | `v12-phase0-hotspot` | `00-hotspots.md` | file exists + non-empty |
| 1 | `v12-phase1-scope` | `00-scope.md` | scope_confirmed_single_method=true |
| 1.5 | `v12-phase1-5-boundary` | `01-scope-boundary.md` | boundary_verdict=PASS |
| 2 | `v12-phase2-architecture` | `02-architecture-plan.md` | max_cyc_projected <= 8 |
| 3 | `v12-phase3-audit` | `03-audit-report.md` | dna_verdict=PASS |
| 4 | `v12-phase4-tickets` | `04-tickets.md` | ticket_count >= 1 |
| 4.5 | `v12-phase4-5-review` | `04-5-ticket-review.md` | review_verdict=PASS |
| 5 | `v12-engineer` | `ticket-X-completion.md` | cyc_achieved <= 8 + build_passed |
| 5.V | `v12-phase5-v-verify` | `ticket-X-verification.md` | verification_verdict=PASS (independent) |
| 6 | `v12-phase6-review` | `05-completion-report.md` | wave_ready=true + final_cyc <= 8 |

---

## 100% COMPLETION ENFORCEMENT

**The wave is NOT complete until every single phase reports VERIFIED_COMPLETE.**

- Phase orchestrator NEVER hands off with any epic still pending
- Re-spawn failures individually (never re-run successes)
- 3 retry rounds max → then HARD_FAILURE escalation
- Phase 5.V is an **independent** verification pass — re-triggers Phase 5 for any failures
- Phase 6 runs a final `complexity_audit.py` scan — zero methods > 8 required

---

## NEVER USE (Obsolete — V1 Bob Shell patterns)

- ❌ `_p0_NNN.sh`, `_p1_NNN.sh`, `_p2_NNN.sh` shell scripts
- ❌ `bob --yolo --chat-mode MODE "$(cat /tmp/msg.txt)"`
- ❌ GCP VM screen sessions
- ❌ `gcp-vm-wave-execution` skill
- ❌ 12-second delays between launches
- ❌ "Switch to: Advanced mode" — does not exist
- ❌ `/epic-run` — deprecated monolithic command
- ❌ 2-tier model (orchestrator → workers directly without phase orchestrator)

---

## ORCHESTRATION RULES

- **SEQUENTIAL PHASES**: Phase N+1 never starts until Phase N is VERIFIED_COMPLETE
- **PARALLEL WORKERS**: All 161 workers within a phase launch simultaneously
- **ISOLATION**: Each subagent has clean context; only summary returns to orchestrator
- **LAMPORT**: Log all transitions to `.lamport/wave7/event_log.jsonl`
- **JANE STREET KB**: `python scripts/query_kb.py "<term>"` before phases 2, 4.5, 5, 5.V, 6
- **100% MANDATE**: Each phase NOT complete until N/N = 161/161
- **RECOVERY**: Re-spawn failed subagents individually; log failures
- **xUnit ONLY**: `[Fact]` / `Assert.Equal()` — NEVER NUnit/MSTest
- **UTF-8**: All files UTF-8 (no BOM)

---

## PHASE SEQUENCE

```
Ph0 Orch (161 workers) → Ph1 Orch (161) → Ph1.5 Orch (161) → Ph2 Orch (161)
     → Ph3 Orch (161) → Ph4 Orch (161) → Ph4.5 Orch (161)
          → Ph5 Orch (161) → Ph5.V Orch (161) → Ph6 Orch (161)
                                                      → WAVE 7 COMPLETE
```

Each arrow represents: VERIFIED_COMPLETE handoff

---

## LAMPORT EVENT SCHEMA

```json
{
  "timestamp": "ISO8601",
  "lamport_clock": 42,
  "epic_id": "EPIC-W7-NNN",
  "phase": "1",
  "tier": "worker|phase_orch|top_orch",
  "event_type": "phase_complete|phase_failed|phase_orch_start|phase_orch_verified_complete|wave_complete",
  "status": "success|failure|hard_failure",
  "output_artifact": "docs/brain/EPIC-W7-NNN/00-scope.md",
  "retry_round": 0
}
```

---

## FAILURE RECOVERY

1. Log failure to `.lamport/wave7/event_log.jsonl`
2. Write `docs/brain/EPIC-W7-NNN/failure-analysis.md`
3. Phase Orchestrator re-spawns that epic individually (same mode, same inputs)
4. Do NOT re-run successful epics
5. After 3 rounds: Phase Orchestrator reports HARD_FAILURE to Tier 1
6. Tier 1 writes incident report → escalates to Director
7. After Director resolution: re-spawn Phase Orchestrator for stuck epics ONLY
8. Wave complete only when 161/161 reach Phase 6 with VERIFIED_COMPLETE

---

## REFERENCE

- **Phase Orchestrator Templates**: `docs/workflow/PHASE_ORCHESTRATOR_TEMPLATES.md`
- **Integration Matrix**: `docs/workflow/AUTONOMOUS_REFACTOR_INTEGRATION_MATRIX_V2.md`
- **Event Log**: `.lamport/wave7/event_log.jsonl`
- **Epic List**: `docs/brain/wave7-epic-list.json`
- **Jane Street KB**: `python scripts/query_kb.py "<term>"`

*Protocol: V12.28 — Bob IDE V2 3-Tier Subagent Model*

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
