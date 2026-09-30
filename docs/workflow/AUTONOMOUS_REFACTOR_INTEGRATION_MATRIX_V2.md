# Autonomous Refactor Integration Matrix V2

**Date**: 2026-06-27
**Version**: 3.0 (Wave 7 PR Repair Loop — Phase 7 PR Repair added, Greptile exception documented, poll_all_bots.py integrated)
**Purpose**: Map skills, MCPs, custom modes, slash commands, and Jane Street KB hooks across all phases
**Context**: Wave 7 execution — 3-tier subagent model (1 top orch → 10 phase orchs → 161 epic workers) + Phase 7 PR repair (autonomous-refactor → lane orchs → planner/engineer/verifier triplets)

## Executive Summary

This document validates that the `/autonomous-refactor` master orchestrator properly integrates all 10 phases of the V12 epic workflow, showing which MCPs, skills, and **custom modes** each phase uses.

### Key Findings (V3.0 — Per-Ticket Execution + File-Lane Architecture)

✅ **All phases mapped to custom modes** (NOT generic modes!)
✅ **Custom modes defined in `.bob/custom_modes.yaml`**
✅ **MCP usage validated for all custom modes**
✅ **Bob IDE V2 native subagent model CONFIRMED WORKING** (Wave 7 Phases 0–3: 161/161 each)
✅ **3-TIER SUBAGENT ARCHITECTURE** — 1 top orch → phase orchs → epic/ticket workers
✅ **100% COMPLETION ENFORCEMENT** — per-phase verification loop before hand-off
✅ **UNIVERSAL PILOT COMPLIANCE AUDIT (V2.8)** — ALL phase orchs run 8-check pilot before batching
✅ **ADAPTIVE BATCH SIZING (V2.8)** — first batch cold-start capped at 20; subsequent batches up to 40
✅ **BOBCOIN PAUSE PROTOCOL (V2.5)** — auto-pauses if balance drops to 15% buffer
✅ **NO-PIVOT RULE (V2.8)** — workers HAVE MCP access; orchestrators MUST always spawn workers
✅ **MCP COLD-START RETRY (V2.8)** — workers probe both MCPs at startup, retry once after 5s
✅ **POST-BATCH HOOK (V2.8)** — `.bob/hooks/after_subagent_batch.py` fires after EVERY batch
✅ **DETERMINISTIC AUDIT SCRIPT** — `scripts/wave7_batch_audit.py` 7-check compliance audit
✅ **Phase Orchestrator Templates** — `docs/workflow/PHASE_ORCHESTRATOR_TEMPLATES.md`
✅ **FILE-LANE ARCHITECTURE (V3.0 NEW)** — Phase 5 uses 40 file-lanes across 7 clusters. One lane per .cs file. Eliminates ALL same-file write conflicts by construction.
✅ **PER-TICKET EXECUTION (V3.0 NEW)** — Each ticket in an epic = its own start_subtask(v12-p5-ticket) + start_subtask(v12-p5-verify). Phase 6 per-epic after all tickets pass.
✅ **CLUSTER DOMAIN CONTEXT (V3.0 NEW)** — Cluster description injected into every Phase 5 worker. Better helper naming, better tests, better invariant preservation.
✅ **BUILD RETRY PROTOCOL (V3.0 NEW)** — dotnet build lock collisions handled: wait 15s, retry max 3. Expected ~1 collision per wave.
✅ **LANE ASSIGNMENT TABLE** — `docs/workflow/WAVE7_PHASE5_LANE_ASSIGNMENT.md` — canonical 40-lane mapping
✅ **PHASE 7 PR REPAIR LOOP (V3.0 NEW)** — Post-wave PR repair: autonomous-refactor produces 6 lane prompts → wave-orch-phase7-lane runs planner→engineer→verifier triplets per finding → LANE_COMPLETE/LANE_HARD_FAILURE
✅ **GREPTILE MCP EXCEPTION (V3.0)** — Greptile IS used in Phase 7 PR repair (wave-orch-phase7-lane reads bot comments via mcp__greptile__*). All other phases: Greptile not used.
✅ **poll_all_bots.py (V3.0)** — `scripts/poll_all_bots.py` replaces raw `gh pr view` for bot signal extraction; 8-bot triage, OKF-override filter, 5-bot satisfaction score
✅ **CS-ONLY GATE (V3.0)** — `scripts/wave7_prepush_gate.py` Check 0 + `.github/workflows/cs-only-pr-gate.yml` enforce no non-.cs files on wave7/* branches
❌ **OBSOLETE**: `gcp-vm-wave-execution` skill — marked retired
❌ **OBSOLETE**: Greptile MCP removed from all phases EXCEPT Phase 7 PR repair (see exception above)
❌ **OBSOLETE**: 2-tier model (top orch spawning workers directly)
❌ **OBSOLETE**: Inline `python3 -c "..."` compliance scan
❌ **OBSOLETE**: Single Phase 5 worker per epic (V2.x model) — replaced by per-ticket file-lane model
❌ **OBSOLETE**: wave-orch-phase5v as separate phase — verification is now inline with each ticket

---

## V2.8 Upgrades — MCP Cold-Start Hardening + Post-Batch Deterministic Hook

### Root Cause of Category B Failures (Diagnosed Wave 7)

Three categories of non-compliant artifacts were produced across Wave 7 Phase 0:
- **Cat A (real MCP)**: 56 epics — resolve_repo + tool calls evidenced, Bobcoins > 0
- **Cat B (admitted failure)**: 9 epics — workers wrote "not available as callable tool" / "no MCP server process responded"
- **Cat C (silent native)**: 96 epics — orchestrator direct-write with no MCP calls at all

**Root cause of Cat B**: `jcodemunch-mcp` uses `stdio` transport. When 40 workers spawn simultaneously,
the stdio pipe backlog is overloaded — workers that lose the startup race get no initial handshake
and silently fall back to native tools. `sequential-thinking` uses `npx -y` which adds 3–15s
cold-start download time. V2.8 fixes both with explicit probes + retry + cold-start batch cap.

### Universal Pilot Compliance Audit (V2.8)

Every Phase Orchestrator (all 10) runs this before batching:

| Check | What is Verified | Hard Fail? |
|-------|-----------------|------------|
| 0 | **MCP PROBE WITH RETRY**: pilot's FIRST action must be `mcp__jcodemunch-mcp__resolve_repo`. If error → wait 5s → retry once. If still fails → HARD FAIL. HALT, report PILOT_FAILURE, do NOT execute epics directly. | YES |
| 1 | jcodemunch-mcp tools called (phase-specific) AND evidenced in artifact | YES |
| 2 | sequential-thinking MCP used AND evidenced in artifact | YES |
| 3 | Output artifact exists and ≥ 200 bytes | YES |
| 4 | manifest.json updated with this phase's status=completed | YES |
| 5 | Agent Tracking block present (Agent Name, Bobcoins Used, Execution Time) | SOFT WARNING |
| 6 | Phase-specific success criterion met | YES |
| 7 | No DNA violations (Phase 5 only: no NUnit, no lock(), UTF-8 encoding) | YES (Phase 5 only) |

**Pilot Fail = HALT**: If any hard check fails, the Phase Orchestrator logs `pilot_failed`,
writes a `failure-analysis.md`, and reports `PILOT_FAILURE` to Tier 1. No workers are spawned.

### Post-Batch Deterministic Hook (V2.8 — NEW)

**The single most important change in V2.8.** Previous checks ran only at phase end — meaning
all 161 workers could complete with Cat B/C artifacts before anything was caught.

**New flow:**
```
Spawn pilot → audit pilot (8-check) → PASS
↓
Spawn batch-1 (20 workers, cold-start cap)
  → workers return → ALL return before hook fires
  → run: python3 .bob/hooks/after_subagent_batch.py  (WAVE7_BATCH_PHASE=0, WAVE7_BATCH_EPICS=...)
  → exit 0: proceed  |  exit 1: redo /tmp/wave7_redo.txt epics → re-run hook → exit 0: proceed
↓
Spawn batch-2 (up to 40 workers)
  → same hook pattern
↓
... repeat for all batches ...
↓
Final scan: python3 scripts/wave7_batch_audit.py --phase 0 --all
  → exit 0: log phase_N_orchestrator_complete → report VERIFIED_COMPLETE to Tier 1
```

**The hook (`after_subagent_batch.py`) runs 7 hard checks per epic:**

| # | Check | Catches |
|---|-------|---------|
| 1 | `artifact_exists` | Missing file |
| 2 | `min_size ≥ 200b` | Empty/stub file |
| 3 | **`no_denial`** | **Cat B/C artifacts** — any denial phrase = immediate FAIL |
| 4 | `jcm_evidence` | No jcodemunch tool keywords in artifact |
| 5 | `seq_evidence` | No sequential-thinking evidence in artifact |
| 6 | `manifest_complete` | manifest.json not updated |
| 7 | `agent_name` | Wrong agent mode (direct orchestrator execution) |

**Why a hook instead of inline orchestrator logic?**
Hooks execute as a separate OS process. They fire deterministically regardless of:
- LLM context pressure causing the orchestrator to skip checks
- The orchestrator convincing itself the artifacts "look good enough"
- Batch-level summary errors where 1 failure is swallowed by 39 successes
The hook's exit code is an objective signal — `0` or `1` — that the orchestrator must branch on.

### Adaptive Batch Sizing (V2.8)

```
First batch (cold-start): BATCH_SIZE = min(20, computed_size)   ← NEW: 20 hard cap
Subsequent batches:        BATCH_SIZE = max(1, min(40, floor(BALANCE * 0.85 / PILOT_COST)))
```

- **Cold-start cap = 20**: First batch after any session start or pause. MCP stdio servers need
  warm-up time; 40 simultaneous handshakes overload the pipe. 20 is empirically safe.
- **Warm cap = 40**: After first batch succeeds (servers warm), full cap applies.
- **Floor = 1**: Always spawn at least 1
- **15% safety buffer**: Never spend the last 15% of balance
- **Balance source**: `.lamport/wave7/bobcoin_tracker.json`
- **Pause protocol**: If balance drops below 15%, Phase Orchestrator pauses and reports `BOBCOIN_PAUSE` to Tier 1

### Bobcoin Tracker

**File**: [`.lamport/wave7/bobcoin_tracker.json`](.lamport/wave7/bobcoin_tracker.json)

**Director reload protocol**: When bobcoins run out mid-wave:
1. Phase Orchestrator pauses, reports `BOBCOIN_PAUSE: N epics remaining`
2. Director reloads bobcoins
3. Director updates `balance_estimate` in `bobcoin_tracker.json`
4. Phase Orchestrator resumes next batch automatically

---

## 3-Tier Subagent Architecture (V2.4)

### Why 3 Tiers?

The previous 2-tier model (Tier 1 Orchestrator → N workers) had one weakness:
**The top-level orchestrator's context window would grow unbounded** as it tracked 161 results per phase × 10 phases = 1,610 result summaries accumulating in one session.

The 3-tier model solves this:
- **Tier 1** (top orchestrator) only tracks 10 phase results — stays lightweight the entire wave
- **Tier 2** (phase orchestrators) each handle 161 results — clean context per phase, discarded after
- **Tier 3** (epic workers) — leaf nodes, clean context, only write artifacts and return summary

### Architecture Diagram

```
Tier 1: Top-Level Orchestrator (autonomous-refactor mode — YOUR SESSION)
   Spawns Phase Orchestrators SEQUENTIALLY
   Only advances to Ph(N+1) after Ph(N) reports VERIFIED_COMPLETE
   |
   +-> Ph0 Orch  -> [161 v12-phase0-hotspot workers, spawn_subagent parallel]  -> VERIFIED_COMPLETE
   +-> Ph1 Orch  -> [161 v12-phase1-scope workers, spawn_subagent parallel]    -> VERIFIED_COMPLETE
   +-> Ph1.5 Orch-> [161 v12-phase1-5-boundary workers, spawn_subagent parallel] -> VERIFIED_COMPLETE
   +-> Ph2 Orch  -> [161 v12-phase2-architecture workers, start_subtask seq]   -> VERIFIED_COMPLETE
   +-> Ph3 Orch  -> [161 v12-phase3-audit workers, start_subtask seq 6 lanes]  -> VERIFIED_COMPLETE
   +-> Ph4 Orch  -> [161 v12-phase4-tickets workers, spawn_subagent parallel]  -> VERIFIED_COMPLETE
   +-> Ph4.5 Orch-> [161 v12-phase4-5-review workers, start_subtask seq]       -> VERIFIED_COMPLETE
   |
   +-> Ph5 Orch  -> [40 FILE-LANE ORCHESTRATORS in parallel]                   -> VERIFIED_COMPLETE
   |     |
   |     +-> FL-01 (S1_SIMA / SIMA.Dispatch.cs, 3 epics)
   |     |     Epic W7-119: ticket-1 → verify-1 → ticket-2 → verify-2 → review
   |     |     Epic W7-027: ticket-1 → verify-1 → review
   |     |     Epic W7-093: ticket-1 → verify-1 → review
   |     |     [all sequential, start_subtask one at a time]
   |     |
   |     +-> FL-02 (S1_SIMA / SIMA.Execution.cs, 4 epics)  [parallel to FL-01]
   |     +-> FL-03 (S1_SIMA / SIMA.Flatten.cs, 3 epics)    [parallel to FL-01]
   |     +-> ...
   |     +-> FL-40 (S7_MISC / PureLogic.cs, 1 epic)        [parallel to all others]
   |
   +-> Ph6 Orch  -> [161 v12-p6-review workers already run inline in Phase 5] -> WAVE_COMPLETE
```

**Phase 5 is the only phase with TRUE PARALLELISM** — 40 lanes run concurrently as separate Bob IDE sessions.
**All writes within each lane are sequential** — zero file conflict risk.
**Phase 6 reviews happen inline** — each epic gets its Phase 6 review immediately after all its tickets pass, within its lane.

### Peak Concurrency

- **Agents in flight at peak**: 1 (Tier 1) + 1 (Phase Orch) + 161 (workers) = **163**
- **Context isolation**: Each worker has its own clean context — no cross-epic contamination
- **Phase Orchestrator lifecycle**: spawned → runs verification loop → reports back → context discarded

### 100% Completion Enforcement (Per Phase)

Every Phase Orchestrator (Tier 2) MUST run this loop before reporting VERIFIED_COMPLETE:

```
Round 1: Spawn all 161 workers simultaneously. Collect results.
If < 161/161 success:
  Log failures to .lamport/wave7/event_log.jsonl
  Write failure-analysis.md for each failed epic
  Re-spawn ONLY failed workers (never re-run successes)
  Round 2... Round 3...
After 3 rounds still incomplete:
  Report HARD_FAILURE to Tier 1 with stuck epic list
  Tier 1 escalates to Director
```

**Phase-specific success criteria (must ALL be true for 100% to pass):**

| Phase | Success Criteria |
|-------|-----------------|
| 0 | `00-hotspots.md` exists and non-empty |
| 1 | `scope_confirmed_single_method=true` |
| 1.5 | `boundary_verdict=PASS` |
| 2 | `max_cyc_projected <= 8` |
| 3 | `dna_verdict=PASS` |
| 4 | `ticket_count >= 1` AND `projected_parent_cyc_after_all <= 8` |
| 4.5 | `review_verdict=PASS` AND `failed_tickets=[]` |
| 5 | ALL 40 `phase_5_lane_complete` events in Lamport log AND all epics have `05-completion-report.md` |
| 6 | `wave_ready=true` AND `final_cyc <= 8` (already embedded in Phase 5 inline) |

**Phase 5 success is lane-gated**: The Phase 5 Orchestrator does not log `phase_5_orchestrator_complete` until all 40 lanes log `phase_5_lane_complete`. Each lane must have 0 stuck epics (or all stuck epics documented in event_log with STUCK_TICKET events).

### Reference: Phase Orchestrator Templates

All phase worker payloads are fully specified in:
**`docs/workflow/PHASE_ORCHESTRATOR_TEMPLATES.md`** (V3.0)

File-lane assignment for Phase 5:
**`docs/workflow/WAVE7_PHASE5_LANE_ASSIGNMENT.md`** (V1.0)

---

---

## Bob IDE V2 Subagent Execution Model (V2.3 — AUTHORITATIVE)

### What Changed (V1 Shell → V2 IDE)

| Concept | V1 (Bob Shell — OBSOLETE) | V2 (Bob IDE — CURRENT) |
|---------|--------------------------|------------------------|
| Parallel execution | GCP VM + screen sessions + `_pX_NNN.sh` scripts | Bob IDE spawns N subagents natively in parallel |
| Epic invocation | `bob --yolo --chat-mode MODE "$(cat /tmp/msg.txt)"` | Orchestrator spawns subagent with custom mode |
| 12-second delay | Required between parallel shell launches | Not needed — subagents launch natively |
| File persistence | SSH verification loop required | Subagent writes directly to local filesystem |
| Context isolation | Each screen session = isolated context | Each subagent = own clean context window |
| Result aggregation | Poll output files every 4 minutes | Subagent result returns to main agent |
| Script generation | Building-Blocks Method (copy `_pX_NNN.sh`) | **NOT NEEDED** — no scripts to generate |

### Subagent Spawn Pattern (MANDATORY for all wave phases)

When the orchestrator (`autonomous-refactor` mode) needs to execute a phase for N epics in parallel:

```
SPAWN SUBAGENT:
  mode: <custom-mode-slug>          # e.g. v12-phase1-scope
  context: <epic-specific data>     # hotspot file path, method name, CYC, file
  task: <phase-specific instruction> # read 00-hotspots.md → write 00-scope.md

SUBAGENT BEHAVIOR:
  1. Receives clean context (no pollution from orchestrator history)
  2. Reads its assigned input artifacts
  3. Executes phase work (MCP calls, analysis, writing)
  4. Writes output artifact to docs/brain/EPIC-W7-NNN/
  5. Returns summary to orchestrator (not intermediate steps)

ORCHESTRATOR RECEIVES:
  - Success/failure status
  - Output artifact path
  - Key metrics (e.g. CYC achieved, extraction count)
```

### Parallel Execution for Wave Phases

For a batch of N epics in the same phase:
1. Orchestrator spawns **all N subagents simultaneously** (no delay needed)
2. Each subagent operates independently in its own context
3. Orchestrator monitors completion and logs Lamport events
4. Failed subagents are re-spawned individually without affecting others

### What Is Still Needed (Unchanged)

- ✅ All 10 custom modes (unchanged — same slugs, same roles)
- ✅ Lamport event tracking (`.lamport/wave7/event_log.jsonl`)
- ✅ Manifest-based state (`docs/brain/EPIC-W7-NNN/manifest.json`)
- ✅ Phase artifact chain (00-hotspots → 00-scope → 01-scope-boundary → ...)
- ✅ Jane Street KB queries at phases 2, 4.5, 5, 5.V, 6
- ✅ Custom mode restrictions (edit scope, MCP requirements)

### What Is Obsolete (Do Not Use)

- ❌ `_p0_NNN.sh`, `_p1_NNN.sh`, `_p2_NNN.sh` shell scripts
- ❌ Bob CLI invocation: `bob --yolo --chat-mode MODE "$(cat /tmp/msg.txt)"`
- ❌ GCP VM screen sessions for phase execution
- ❌ `gcp-vm-wave-execution` skill for spawning agents
- ❌ 12-second delay between parallel launches
- ❌ SSH file persistence verification
- ❌ `WAVE2_SHELL_WORKAROUND` pattern
- ❌ `launch_wave7_phase1_batched.sh` and similar master launch scripts
- ❌ `scripts/wave7/_pX_NNN.sh` building blocks

---

## Phase-by-Phase Integration Matrix (V3.0 — FILE-LANE ARCHITECTURE)

| Phase | Orch Mode | Worker Mode(s) | Spawn Mechanism | Jane Street KB | Parallelism |
|-------|-----------|----------------|-----------------|----------------|-------------|
| **0** | `wave-orch-phase0` | `v12-phase0-hotspot` | `spawn_subagent("general")` 20/turn | ❌ No | ✅ Full parallel |
| **1** | `wave-orch-phase1` | `v12-phase1-scope` | `spawn_subagent("general")` 20/turn | ❌ No | ✅ Full parallel |
| **1.5** | `wave-orch-phase1-5` | `v12-phase1-5-boundary` | `spawn_subagent("general")` 20/turn | ❌ No | ✅ Full parallel |
| **2** | `wave-orch-phase2` | `v12-phase2-architecture` | `start_subtask` sequential, 5 lanes | ✅ MANDATORY | ⚡ 5-lane parallel |
| **3** | `wave-orch-phase3` | `v12-phase3-audit` | `start_subtask` sequential, 6 lanes | ❌ No | ⚡ 6-lane parallel |
| **4** | `wave-orch-phase4` | `v12-phase4-tickets` | `spawn_subagent("general")` 20/turn | ❌ No | ✅ Full parallel |
| **4.5** | `wave-orch-phase4-5` | `v12-phase4-5-review` | `start_subtask` sequential | ✅ Hardcoded | 🔄 Sequential |
| **5** | `wave-orch-phase5` | `v12-p5-ticket` + `v12-p5-verify` + `v12-p6-review` | `start_subtask` sequential **within each of 40 file-lanes** | ✅ MANDATORY | ⚡ **40-lane parallel** |
| **6** | _(inline in Phase 5)_ | `v12-p6-review` | `start_subtask` per-epic (within lane) | ✅ MANDATORY | _(part of Phase 5 lane)_ |

**Phase 5 is the parallelism apex**: 40 independent file-lane sessions run concurrently. Within each lane, the ticket → verify → review cycle is strictly sequential. This prevents any two agents from writing the same `.cs` file simultaneously.

**Phase 6 is now inline**: `v12-p6-review` runs per-epic at the end of each lane, not as a separate phase sweep. After all 40 lanes complete, Tier 1 runs a final `python3 scripts/complexity_audit.py` gate and logs `wave_7_complete`.

**CRITICAL**: All phases use **custom modes**, NOT generic modes! See `.bob/custom_modes.yaml`.

---

## Custom Modes Analysis

### Phase 0: Hotspot Analysis
**Custom Mode**: `v12-phase0-hotspot`  
**Slug**: `v12-phase0-hotspot`  
**MCPs**:
- ✅ `jcodemunch-mcp` (MANDATORY) - search_symbols, get_hotspots, get_symbol_complexity, get_blast_radius
- ✅ `sequential-thinking` (MANDATORY) - Break down analysis into explicit steps

**Groups**: read, edit (md/json/yaml/yml/txt only), command, mcp
**Skills**:
- `.bob/skills/autonomous-refactor/SKILL.md` — orchestrator spawns this mode as subagent

**Output**: `docs/brain/EPIC-{ID}/00-hotspots.md`, `manifest.json`

---

### Phase 1: Scope Definition
**Custom Mode**: `v12-phase1-scope`  
**Slug**: `v12-phase1-scope`  
**MCPs**:
- ✅ `jcodemunch-mcp` (MANDATORY) - get_file_outline, find_references, get_dependency_graph
- ✅ `sequential-thinking` (MANDATORY) - Validate scope boundaries

**Groups**: read, edit (md/json/yaml/yml/txt only), command, mcp  
**Skills**: None

**Output**: `docs/brain/EPIC-{ID}/00-scope.md`

---

### Phase 1.5: Scope Boundary Validation
**Custom Mode**: `v12-phase1-5-boundary`  
**Slug**: `v12-phase1-5-boundary`  
**MCPs**:
- ✅ `jcodemunch-mcp` (MANDATORY) - get_symbol_source, get_blast_radius, find_references
- ✅ `sequential-thinking` (MANDATORY) - Validate no scope creep

**Groups**: read, edit (md/json/yaml/yml/txt only), command, mcp
**Skills**:
- `.bob/skills/epic-scope-boundary/SKILL.md` — subagent instructions for this phase

**Output**: `docs/brain/EPIC-{ID}/01-scope-boundary.md`

**Special Rule**: BLOCKER if scope exceeds single method

---

### Phase 2: Architecture Planning
**Custom Mode**: `v12-phase2-architecture`  
**Slug**: `v12-phase2-architecture`  
**MCPs**:
- ✅ `jcodemunch-mcp` (MANDATORY) - get_context_bundle, get_call_hierarchy, get_dependency_graph
- ✅ `sequential-thinking` (MANDATORY) - Validate architecture decisions
- ✅ `graphify` (MANDATORY) - Codebase structure visualization

**Groups**: read, edit (md/json/yaml/yml/txt only), command, mcp, browser
**Skills**:
- `.bob/skills/epic-plan/SKILL.md` — subagent instructions for this phase

**Output**: `docs/brain/EPIC-{ID}/02-architecture-plan.md`

**Special Rule**: MANDATORY Jane Street KB query before planning

---

### Phase 3: DNA Audit
**Custom Mode**: `v12-phase3-audit`  
**Slug**: `v12-phase3-audit`  
**MCPs**:
- ✅ `jcodemunch-mcp` (MANDATORY) - search_ast, get_layer_violations, get_dependency_cycles
- ✅ `sequential-thinking` (MANDATORY) - Validate compliance

**Groups**: read, edit (md/json/yaml/yml/txt only), command, mcp, browser  
**Skills**: None explicitly referenced

**Output**: `docs/brain/EPIC-{ID}/03-audit-report.md`

**CRITICAL**: NO Greptile MCP used (despite system prompt references)

---

### Phase 4: Ticket Generation
**Custom Mode**: `v12-phase4-tickets`  
**Slug**: `v12-phase4-tickets`  
**MCPs**:
- ✅ `jcodemunch-mcp` (MANDATORY) - get_symbol_complexity, get_extraction_candidates
- ✅ `sequential-thinking` (MANDATORY) - Validate ticket breakdown

**Groups**: read, edit (md/json/yaml/yml/txt only), command, mcp  
**Skills**: None

**Output**: `docs/brain/EPIC-{ID}/04-tickets.md`

---

### Phase 4.5: Ticket Review
**Custom Mode**: `v12-phase4-5-review`  
**Slug**: `v12-phase4-5-review`  
**MCPs**:
- ✅ `sequential-thinking` (MANDATORY) - Validate against Jane Street KB

**Groups**: read, edit (md/json/yaml/yml/txt only), command, mcp, browser  
**Skills**: None

**Output**: `docs/brain/EPIC-{ID}/04-5-ticket-review.md`

**Special Rule**: MANDATORY Jane Street KB query for ticket validation

**Gap**: ⚠️ No dedicated slash command (manual review gate)

---

### Phase 5: Ticket Execution
**Custom Mode**: `v12-engineer` (Photon Engineer)  
**Slug**: `v12-engineer`  
**MCPs**:
- ✅ `jcodemunch-mcp` (MANDATORY) - get_symbol_source, get_context_bundle, plan_refactoring
- ✅ `sequential-thinking` (MANDATORY) - Validate implementation

**Groups**: read, edit (ALL files), command, mcp, browser
**Skills**:
- None — `v12-engineer` mode handles this phase directly as subagent

**Output**: `docs/brain/EPIC-{ID}/ticket-X-completion.md`

**Special Rules**:
- TEST FRAMEWORK MANDATE (V12.32): ALWAYS xUnit, NEVER NUnit/MSTest
- DNA rules: `rules-v12-engineer/dna.md`

---

### Phase 5.V: Verification
**Custom Mode**: `v12-phase5-v-verify`  
**Slug**: `v12-phase5-v-verify`  
**MCPs**:
- ✅ `jcodemunch-mcp` (MANDATORY) - get_symbol_complexity, get_changed_symbols
- ✅ `sequential-thinking` (MANDATORY) - Validate verification

**Groups**: read, edit (md/json/yaml/yml/txt only), command, mcp, browser  
**Skills**: None explicitly referenced

**Output**: `docs/brain/EPIC-{ID}/ticket-X-verification.md`

**Verification Checklist**:
- CYC ≤ 8 achieved
- Only target method modified
- xUnit tests generated and passing
- UTF-8 encoding compliance

---

### Phase 6: Final Review
**Custom Mode**: `v12-phase6-review`  
**Slug**: `v12-phase6-review`  
**MCPs**:
- ✅ `jcodemunch-mcp` (MANDATORY) - get_repo_health, get_hotspots
- ✅ `sequential-thinking` (MANDATORY) - Validate completion

**Groups**: read, edit (md/json/yaml/yml/txt only), command, mcp, browser  
**Skills**: None explicitly referenced

**Output**: `docs/brain/EPIC-{ID}/05-completion-report.md`

---

## Jane Street KB Integration

### Overview

The Jane Street Knowledge Base (OKF — Open Knowledge Format) contains 100+ ingested rules covering HFT patterns, FSM/Actor patterns, complexity reduction strategies, and testing standards. The KB is stored as local markdown files in `docs/intel/jane-street/` (14 OKF files). Phases query it via `scripts/query_kb.py` which tries Firebase first then falls back to the local OKF wiki automatically. Queries always resolve via OKF.

### Hook Status by Phase

| Phase | KB Hook Status | Query Purpose | Command Example |
|-------|----------------|---------------|-----------------|
| **0** | ❌ No hook | Hotspot analysis only | N/A |
| **1** | ❌ No hook | Scope definition only | N/A |
| **1.5** | ❌ No hook | Boundary validation only | N/A |
| **2** | ⚠️ Optional | Extraction pattern guidance | `python scripts/query_kb.py "extraction patterns"` |
| **3** | ❌ No hook | DNA audit (no KB needed) | N/A |
| **4** | ❌ No hook | Ticket generation only | N/A |
| **4.5** | ✅ **MANDATORY** | Ticket validation against KB rules | `python scripts/query_kb.py "complexity reduction"` |
| **5** | ✅ **MANDATORY** | Implementation pattern lookup | `python scripts/query_kb.py "FSM extraction"` |
| **5.V** | ✅ **MANDATORY** | DNA compliance verification | `python scripts/query_kb.py "lock-free patterns"` |
| **6** | ✅ **MANDATORY** | Final audit against KB standards | `python scripts/query_kb.py "testing strategies"` |

### Mandatory KB Queries

**Phase 4.5 (Ticket Review)**:
- Query: "complexity reduction", "single responsibility", "extraction patterns"
- Purpose: Validate tickets meet Jane Street cognitive simplicity standards
- Enforcement: BLOCKER if KB unavailable

**Phase 5 (Ticket Execution)**:
- Query: "FSM extraction", "lock-free patterns", "Actor model"
- Purpose: Ensure implementation follows Jane Street HFT patterns
- Enforcement: BLOCKER if KB unavailable

**Phase 5.V (Verification)**:
- Query: "DNA compliance", "testing patterns", "complexity thresholds"
- Purpose: Verify changes don't violate Jane Street principles
- Enforcement: BLOCKER if KB unavailable

**Phase 6 (Final Review)**:
- Query: "final audit", "regression detection", "performance patterns"
- Purpose: Comprehensive Jane Street alignment check
- Enforcement: BLOCKER if KB unavailable

### KB Access Method

**Script**: `scripts/query_kb.py`
**Backend**: OKF local wiki at `docs/intel/jane-street/` (14 markdown files, OKF v0.1 format) — Firebase tried first, OKF fallback always resolves
**Coverage**: 100+ rules with P0/P1/P2 severity
**Authentication**: None required — OKF wiki is local, no Firebase dependency

**Example Usage**:
```bash
python scripts/query_kb.py "complexity reduction"
# Returns: Jane Street rules for CYC ≤ 8, cognitive load reduction, etc.
```

### Automatic Loading

**Hook**: `.bob/hooks/pre_session.py`
**Trigger**: Every Bob CLI session start
**Effect**: Jane Street KB patterns loaded into session context automatically

### KB Rule Categories

1. **Complexity Reduction** (P0): CYC ≤ 8, single responsibility, extraction patterns
2. **Lock-Free Concurrency** (P0): FSM/Actor Enqueue model, no `lock()` blocks
3. **Cognitive Simplicity** (P1): Readable code, explicit state machines
4. **Testing Standards** (P1): xUnit patterns, F5 verification gates
5. **Performance Patterns** (P2): Zero-allocation, dictionary dispatch, microsecond-latency

### Enforcement Protocol

**If KB Query Fails**:
1. Phase 4.5, 5, 5.V, 6: **HALT** - KB required for validation (OKF wiki must exist at `docs/intel/jane-street/`)
2. Phase 2: **WARN** - Optional guidance, can proceed without KB
3. All other phases: **N/A** - No KB dependency
**Note**: Firebase unavailability is NOT a failure condition. OKF local wiki is the authoritative fallback and is always available.

**Violation Handling**:
- New Jane Street violations detected → REVERT changes
- Apply KB-verified patterns → Re-run phase
- Document deviation in `docs/standards/JANE_STREET_DEVIATIONS.md`

---

## MCP Usage Summary

### Available MCPs (from `.mcp.json`)

1. **jcodemunch-mcp** (stdio)
   - **Used in**: ALL 10 phases (MANDATORY for 9 phases, Phase 4.5 uses sequential-thinking only)
   - **Purpose**: Code analysis, symbol search, complexity checks, file navigation
   - **Tools**: 70+ tools for code exploration and analysis

2. **sequential-thinking** (stdio)
   - **Used in**: ALL 10 phases (MANDATORY for all)
   - **Purpose**: Complex reasoning, step-by-step analysis
   - **Tools**: `sequentialthinking` tool for dynamic problem-solving

3. **graphify** (NOT in `.mcp.json` but referenced in custom mode)
   - **Used in**: Phase 2 only (Architecture Planning)
   - **Purpose**: Codebase structure visualization
   - **Status**: ⚠️ May need to be added to `.mcp.json` if not available globally

### Greptile MCP - NOT USED ❌

**Status**: Referenced in system prompt but NOT used in any of the 10 phases  
**Action Required**: Remove all Greptile references from system prompts  
**Reason**: Custom modes use jcodemunch-mcp for all code analysis needs

---

## Skill Usage Summary

### Explicitly Referenced Skills

1. **`plugins/architecture-validation/SKILL.md`**
   - **Used in**: Phase 2 (Architecture Planning)
   - **Status**: ✅ Properly referenced in custom mode

### Implicitly Used Skills (Now Explicit in Matrix)

2. **`.bob/skills/gcp-vm-wave-execution/`**
   - **Used in**: Phase 0, 5 (VM parallel execution)
   - **Status**: ✅ Now explicit in matrix
   - **Purpose**: SSH-based VM execution for parallel epic processing

3. **`.bob/skills/lamport-clock-recovery/`**
   - **Used in**: All phases (Lamport conflict resolution)
   - **Status**: ⚠️ Should add explicit references
   - **Purpose**: Deterministic event ordering for wave execution

4. **`plugins/scope-boundary-check/SKILL.md`**
   - **Used in**: Phase 1.5 (Scope validation)
   - **Status**: ✅ Now explicit in matrix
   - **Purpose**: Validate single-method scope, prevent scope creep

5. **`plugins/parallel-epic-execution/SKILL.md`**
   - **Used in**: Phase 5 (Local parallel execution)
   - **Status**: ✅ Now explicit in matrix
   - **Purpose**: Local multi-epic execution (alternative to VM)

6. **`plugins/multi-agent-orchestrator/WAVE2_SHELL_WORKAROUND.md`**
   - **Used in**: Phase 0 (SSH file I/O)
   - **Status**: ✅ Now explicit in matrix
   - **Purpose**: SSH file persistence workaround for VM execution

---

## Custom Mode Distribution

| Custom Mode | Phases | Edit Scope | MCP Support | Browser |
|-------------|--------|------------|-------------|---------|
| `v12-phase0-hotspot` | Phase 0 | md/json/yaml/yml/txt | ✅ Yes | ❌ No |
| `v12-phase1-scope` | Phase 1 | md/json/yaml/yml/txt | ✅ Yes | ❌ No |
| `v12-phase1-5-boundary` | Phase 1.5 | md/json/yaml/yml/txt | ✅ Yes | ❌ No |
| `v12-phase2-architecture` | Phase 2 + Phase 7 planner | md/json/yaml/yml/txt | ✅ Yes | ✅ Yes |
| `v12-phase3-audit` | Phase 3 | md/json/yaml/yml/txt | ✅ Yes | ✅ Yes |
| `v12-phase4-tickets` | Phase 4 | md/json/yaml/yml/txt | ✅ Yes | ❌ No |
| `v12-phase4-5-review` | Phase 4.5 | md/json/yaml/yml/txt | ✅ Yes | ✅ Yes |
| `v12-engineer` | Phase 5 + Phase 7 fixer | ALL files | ✅ Yes | ✅ Yes |
| `v12-phase5-v-verify` | Phase 5.V | md/json/yaml/yml/txt | ✅ Yes | ✅ Yes |
| `v12-phase6-review` | Phase 6 | md/json/yaml/yml/txt | ✅ Yes | ✅ Yes |
| `wave-orch-phase7-lane` | Phase 7 PR repair (Tier 2 lane orch) | md/json/jsonl/txt | ✅ Yes (incl. Greptile) | ❌ No |
| `v12-pr-repair-verify` | Phase 7 verifier (Tier 3) | md/json/yaml/yml/txt | ✅ Yes | ❌ No |

**Key Insight**: Only `v12-engineer` (Phase 5 + Phase 7 fixes) can edit source code files. All other phases are restricted to documentation files.

**Phase 7 Exception**: `wave-orch-phase7-lane` uses Greptile MCP (`mcp__greptile__*`) to fetch bot review comments. This is the ONLY phase where Greptile is actively used.

---

## Autonomous Refactor Command Validation

### Does `/autonomous-refactor` properly orchestrate all phases (including Phase 7)?

**Analysis of `/autonomous-refactor` command structure**:

```
Phase 1: Initialize Session (autonomous-refactor mode)
  ↓
Phase 2: Epic Execution Loop (autonomous-refactor mode)
  ├─ Step A: Epic Status Report (autonomous-refactor - no mode switch)
  ├─ Step B: Run Epic via /epic-run (orchestrator mode)
  │   └─ /epic-run internally calls:
  │       ├─ Phase 0: /epic-intake → v12-phase0-hotspot
  │       ├─ Phase 1: /epic-scope-boundary → v12-phase1-scope
  │       ├─ Phase 1.5: /epic-scope-boundary --phase 1.5 → v12-phase1-5-boundary
  │       ├─ Phase 2: /epic-plan → v12-phase2-architecture
  │       ├─ Phase 3: /epic-scan → v12-phase3-audit
  │       ├─ Phase 4: /epic-tickets → v12-phase4-tickets
  │       ├─ Phase 5: /epic-validate → v12-engineer
  │       ├─ Phase 5.V: /epic-verify-ticket → v12-phase5-v-verify
  │       └─ Phase 6: /epic-review-final → v12-phase6-review [partial]
  ├─ Step C: Verify Epic Completion (autonomous-refactor - no mode switch)
  ├─ Step D: Update Progress Log (autonomous-refactor mode)
  └─ Step E: Check Completion Criteria (autonomous-refactor mode)
  ↓
Phase 3: Final Verification (autonomous-refactor mode)
  ↓
Phase 4: Completion Handshake (autonomous-refactor - no mode switch)
  ↓
Phase 7 (POST-WAVE): PR Repair Loop (autonomous-refactor mode)  [see below]
```

### Phase 7 PR Repair Loop — Architecture

**⚠️ NAME COLLISION NOTE**: There are TWO things called "Phase 7":
- **OLD Phase 7** = Wave 7 lock-free concurrency hardening (`v12-phase7-lead` mode, `/phase7` command) — refactors individual .cs files
- **NEW Phase 7** = Wave 7 PR repair loop (`wave-orch-phase7-lane` mode, `/pr-loop` command) — iterative bot triage + repair until PRs are merge-ready

This section documents the **NEW Phase 7 (PR Repair Loop)** only.

```
autonomous-refactor (Tier 1):
  - Reads manifest.json + 6 fix_queue.md files
  - Produces 6 lane prompts (one per PR/branch)
  - Director pastes each into a new wave-orch-phase7-lane tab
  ↓
wave-orch-phase7-lane (Tier 2, one instance per PR):
  STEP 0: CS-only gate (python3 scripts/wave7_prepush_gate.py)
  STEP 1: Bot poll via scripts/poll_all_bots.py (8-bot triage)
  STEP 2: Triage findings → VALID-LOGIC-BUG / VALID-MECHANICAL / VALID-DNA / HALLUCINATION / INFRA-NOISE
  STEP 3: For each VALID-LOGIC-BUG:
    - Read OKF doc(s) for finding type (see table below)
    - start_subtask(v12-phase2-architecture) → plan  [PLANNER]
    - start_subtask(v12-engineer)            → fix   [ENGINEER]
    - start_subtask(v12-pr-repair-verify)    → verify [VERIFIER]
  STEP 4: Mechanical/DNA fixes applied directly (no subtask)
  STEP 5: Gate + push → re-poll until 5/5 bots green or max 3 rounds
  STEP 6: Write repair-log.md + completion.md on main
  ↓
  Emits: LANE_COMPLETE L{N} PR#{N} status=(MERGED_READY|NEEDS_DIRECTOR) findings={N}_fixed
       | LANE_HARD_FAILURE L{N} PR#{N} reason=<one-line>
```

### Phase 7 OKF Doc → Finding Type Mapping

| Finding Type | OKF Docs to Load |
|---|---|
| VALID-LOGIC-BUG (timezone, state regression, wrong key) | `how-to-build-an-exchange.md` (determinism, one_in_flight), `production-engineering-billions.md` (independent_tracking, rate_limiting) |
| VALID-LOGIC-BUG (security / ordering) | `production-engineering-billions.md` (rate_limiting), `how-to-build-an-exchange.md` (sidecar_lifecycle) |
| VALID-DNA (lock()) | `lock-free-patterns.md` — **HALT AND ESCALATE** (never auto-fix) |
| VALID-DNA (DateTime.Now) | `how-to-build-an-exchange.md` → determinism pattern |
| VALID-DNA (Unicode / underscore locals) | No OKF doc needed — mechanical ASCII-only rule |
| Any verification of logic fix | `production-engineering-billions.md` (independent_tracking), `testing-strategies.md` |

### Phase 7 Modes & Tools

| Mode | Tier | MCP Groups | Bot Signal Source |
|------|------|------------|-------------------|
| `wave-orch-phase7-lane` | 2 (Lane Orch) | read, edit, execute, **mcp** (Greptile+ST), skill, todo, subtask, subagent | `mcp__greptile__*` + `scripts/poll_all_bots.py` |
| `v12-phase2-architecture` | 3 (Planner) | read, edit, execute, **mcp** (all), browser, skill, todo, subtask, subagent | n/a |
| `v12-engineer` | 3 (Engineer) | read, edit (ALL), execute, **mcp** (all), browser, skill, todo, subtask, subagent | n/a |
| `v12-pr-repair-verify` | 3 (Verifier) | read, edit, execute, **mcp** (all), skill, todo, subtask, subagent | `scripts/poll_all_bots.py` |

### Phase 7 Scripts & Infrastructure

| Script/File | Purpose |
|---|---|
| `scripts/poll_all_bots.py` | 8-bot triage via gh CLI + GitHub API; OKF-override filter; 5-bot satisfaction score |
| `scripts/wave7_prepush_gate.py` | Pre-push gate: Check 0=CS-only, Check 1=ASCII, Check 2=DateTime.Now, Check 3=lock(), Check 4=diff size, Check 5=Sourcery 150k |
| `.github/workflows/cs-only-pr-gate.yml` | CI enforcement: no non-.cs files on wave7/* branches |
| `docs/brain/wave7-pr-repairs/manifest.json` | 6 lane entries: PR, branch, cluster, gate_status |
| `docs/brain/wave7-pr-repairs/PR-{20..25}/fix_queue.md` | Per-PR P0/P1 findings with OKF references |
| `.bob/commands/pr-loop.md` | `/pr-loop` slash command — PR review & repair loop V4 |

### Validation Results

✅ **Phase 0**: Mapped via `/epic-run` → `/epic-intake` → `v12-phase0-hotspot`
✅ **Phase 1**: Mapped via `/epic-run` → `/epic-scope-boundary` → `v12-phase1-scope`
✅ **Phase 1.5**: Mapped via `/epic-run` → `/epic-scope-boundary --phase 1.5` → `v12-phase1-5-boundary`
✅ **Phase 2**: Mapped via `/epic-run` → `/epic-plan` → `v12-phase2-architecture`
✅ **Phase 3**: Mapped via `/epic-run` → `/epic-scan` → `v12-phase3-audit`
✅ **Phase 4**: Mapped via `/epic-run` → `/epic-tickets` → `v12-phase4-tickets`
✅ **Phase 4.5**: Mapped via `/epic-review-tickets` → `v12-phase4-5-review`
✅ **Phase 5**: Mapped via `/epic-run` → `/epic-validate` → `v12-engineer`
✅ **Phase 5.V**: Mapped via `/epic-run` → `/epic-verify-ticket` → `v12-phase5-v-verify`
✅ **Phase 6**: Mapped via `/epic-run` → `/epic-review-final` → `v12-phase6-review` (partial) + Phase 3 (Final Verification)
✅ **Phase 7 (PR Repair)**: Mapped via `/pr-loop` → `wave-orch-phase7-lane` (Tier 2) → `v12-phase2-architecture` + `v12-engineer` + `v12-pr-repair-verify` (Tier 3)

**Conclusion**: All phases mapped to custom modes. Phase 7 (PR Repair) fully automated with 3-tier architecture.

---

## Integration Gaps & Recommendations

### Critical Gaps

1. **Greptile MCP — Phase 7 Exception** ✅ RESOLVED
   - **Status**: Greptile IS actively used in Phase 7 PR repair (`wave-orch-phase7-lane`)
   - **Old matrix said**: "❌ OBSOLETE: Greptile MCP referenced in old system prompts" — **WRONG for Phase 7**
   - **Correct policy**: Greptile removed from Phases 0–6; KEPT and required for Phase 7 lane orchestrators
   - **Priority**: Documentation corrected in this version (V3.0)

2. **Phase 4.5 Automation Complete** ✅
   - **Status**: `/epic-review-tickets` command created
   - **Impact**: Manual review gate eliminated
   - **Validation**: 6 automated checks via Sequential Thinking MCP

3. **Graphify MCP Not in `.mcp.json`**
   - **Impact**: Phase 2 may fail if graphify not available globally
   - **Fix**: Add graphify to `.mcp.json` or verify global availability
   - **Priority**: P1 (blocks Phase 2)

### Skill Reference Gaps

4. **Missing Explicit Skill References**
   - **Phases affected**: 0, 1.5, 5
   - **Impact**: Skills used but not documented in custom modes
   - **Fix**: Add explicit `@skill` references to custom mode definitions
   - **Priority**: P3 (documentation improvement)

---

## Greptile Cleanup Plan

### Files Requiring Greptile Removal

Based on search results, the following files contain Greptile references that should be reviewed and cleaned:

#### System Prompts (P0 - CRITICAL)

1. **AGENTS.md** (line 30) - Remove Greptile report reference
2. **System prompt in Bob IDE** - Check for Greptile MCP references

#### Documentation (P1 - HIGH)

3. **docs/AGENTS.md** (line 30) - Remove `02-greptile-report.md` reference
4. **docs/workflow/AUTONOMOUS_REFACTOR_INTEGRATION_MATRIX.md** (lines 92, 180, 315, 422) - Remove Greptile MCP references
5. **docs/workflow/LOOP_ORCHESTRATION.md** (multiple lines) - Remove Greptile CLI and MCP references
6. **docs/workflow/HANDOFF_PROMPT_EPIC_LOOP.md** (lines 117, 234) - Remove Greptile references
7. **docs/wave6/PHASE_MCP_VS_CUSTOM_MODES_ANALYSIS.md** (lines 109, 137, 145) - Remove Greptile MCP references

#### Historical/Archive (P2 - LOW)

8. **docs/protocol/GREPTILE_REMOVAL_PROTOCOL.md** - Archive or update to reflect complete removal
9. **docs/mcp/GREPTILE_MCP_TROUBLESHOOTING.md** - Archive (no longer relevant)
10. **docs/protocol/PR_LOOP_V2_HARDENING.md** - Remove Greptile bot references
11. **Wave 4/5/6 completion reports** - Historical, no action needed

### Cleanup Script

```powershell
# Greptile Cleanup Script
# Run from repository root

$filesToClean = @(
    "AGENTS.md",
    "docs/AGENTS.md",
    "docs/workflow/AUTONOMOUS_REFACTOR_INTEGRATION_MATRIX.md",
    "docs/workflow/LOOP_ORCHESTRATION.md",
    "docs/workflow/HANDOFF_PROMPT_EPIC_LOOP.md",
    "docs/wave6/PHASE_MCP_VS_CUSTOM_MODES_ANALYSIS.md"
)

foreach ($file in $filesToClean) {
    if (Test-Path $file) {
        Write-Host "Cleaning: $file"
        # Manual review required - automated replacement may break context
        code $file
    }
}

# Archive obsolete Greptile docs
$archiveDir = "docs/archive/greptile"
New-Item -ItemType Directory -Force -Path $archiveDir
Move-Item "docs/mcp/GREPTILE_MCP_TROUBLESHOOTING.md" $archiveDir -Force
Move-Item "docs/protocol/GREPTILE_REMOVAL_PROTOCOL.md" $archiveDir -Force

Write-Host "Greptile cleanup complete. Review files manually for context-specific changes."
```

---

## Recommendations

### Immediate (Pre-Wave 7)

1. ✅ **Matrix V2 Complete**: This document corrects custom mode mapping
2. ✅ **Greptile Cleanup Complete**: Greptile removed from Phases 0–6; preserved and documented for Phase 7
3. ✅ **Phase 4.5 Automated**: `/epic-review-tickets` command created with 6 validation checks
4. ✅ **Phase 7 PR Repair Loop Automated**: 3-tier architecture, 6 lane prompts, OKF hardening committed
5. ⏳ **Add Graphify to `.mcp.json`**: Verify Phase 2 requirements (P1)
6. ⏳ **Add Skill References**: Update custom mode definitions (P3)
7. ⏳ **Test Integration**: Run pilot epic with full custom mode stack

### Short-Term (Wave 7 Execution + PR Repair)

8. ⏳ **Enable Extended Thinking**: Phases 2, 3, 5 (via custom modes)
9. ⏳ **Enable Prompt Caching**: System prompts + Jane Street KB
10. ⏳ **Monitor MCP Usage**: Track jcodemunch + Greptile usage patterns (Phase 7)
11. ⏳ **Measure Cost Savings**: Compare with/without caching

### Long-Term (Post-Wave 7)

12. ⏳ **Consolidate Skills**: Merge VM + local parallel execution
13. ⏳ **Optimize MCP Calls**: Reduce redundant tool invocations
14. ⏳ **Document Patterns**: Best practices for custom mode + MCP integration

---

## Related Documentation

- **Skill Audit**: `docs/workflow/SKILL_AUDIT_10_PHASES.md`
- **Epic Workflow**: `docs/workflow/V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md`
- **Autonomous Refactor**: `.bob/commands/autonomous-refactor.md`
- **Custom Modes**: `.bob/custom_modes.yaml`
- **MCP Configuration**: `.mcp.json`
- **GCP VM Skill**: `.bob/skills/gcp-vm-wave-execution/skill.md`
- **Lamport Recovery**: `.bob/skills/lamport-clock-recovery/skill.md`
- **PR Repair Manifest**: `docs/brain/wave7-pr-repairs/manifest.json`
- **PR Repair Fix Queues**: `docs/brain/wave7-pr-repairs/PR-{20..25}/fix_queue.md`
- **PR Loop Command**: `.bob/commands/pr-loop.md`
- **Bot Triage Script**: `scripts/poll_all_bots.py`

---

**Document Status**: ✅ Complete (V3.0 - Phase 7 PR Repair Loop + Greptile Exception + poll_all_bots.py)
**Validation**: 12/12 custom modes mapped (10 epic phases + 2 Phase 7 PR repair), all phases automated
**Greptile Status**: ✅ Phases 0–6: removed. Phase 7 (wave-orch-phase7-lane): ACTIVE and required.
**Jane Street KB**: ✅ Documented (4 MANDATORY hooks, 1 optional hook, 5 no hooks) + Phase 7 OKF table
**Skills**: ✅ Made explicit in matrix (gcp-vm-wave-execution, scope-boundary-check, architecture-validation, parallel-epic-execution, WAVE2_SHELL_WORKAROUND)
**Phase 4.5**: ✅ Automated via `/epic-review-tickets` command
**Phase 7 PR Repair**: ✅ Automated — 3-tier architecture, OKF hardening, CS-only gate, 6 lane prompts ready
**Next Review**: After Wave 7 pilot epic