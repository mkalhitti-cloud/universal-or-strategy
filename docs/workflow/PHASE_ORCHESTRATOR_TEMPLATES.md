# Phase Orchestrator Templates — Wave 7

**Version**: V3.0 (Bob IDE V2 — per-ticket execution model + file-lane architecture)
**Used By**: All 10 wave-orch-phaseN modes (Tier 2 Phase Orchestrators)
**Purpose**: Exact worker message payloads and verification protocols per phase

---

## CRITICAL: Execution Model (V2.9 — confirmed by live test 2026-06-28)

**USE `start_subtask`, NOT `spawn_subagent`.**

| Mechanism | Custom mode? | MCP tools? | Parallel? |
|---|---|---|---|
| `spawn_subagent("general")` | NO | NO — 16 base tools only | YES but useless without MCP |
| `spawn_subagent("v12-phase0-hotspot")` | ERROR — invalid name | N/A | N/A |
| `start_subtask(mode="v12-phase0-hotspot")` | YES | YES — full MCP | NO — sequential |

**Rules derived from live testing:**
- `start_subtask` with a custom mode slug → worker runs that mode with full MCP access
- Only **one** `start_subtask` can run at a time — calling 2+ in one turn causes "Severe error"
- Each subtask must return before the next is started
- `spawn_subagent` workers confirmed to receive 0 MCP tools — never use for MCP work

---

## How To Use These Templates

Each Phase Orchestrator (`wave-orch-phaseN`) calls `start_subtask` sequentially for each epic.
The `message` parameter must follow the template for that phase exactly.
Replace `<EPIC_ID>`, `<METHOD_NAME>`, `<CYC>`, `<SOURCE_FILE>` from `docs/brain/wave7-epic-list.json`.

**epic list access**: `docs/brain/wave7-epic-list.json` is a flat JSON array.
Access entries as `data[0]`, `data[1]`, ..., `data[160]` — NOT `data["epics"]`.

---

## Audit Cadence (V2.9 — ALL 10 orchestrators)

After every 20 epics run the compliance hook before continuing:

```bash
python3 scripts/wave7_batch_audit.py --phase <N> --epics <space-separated IDs>
```

- Exit 0 = all pass → continue to next 20
- Exit 1 = failures → immediately retry failed epics (up to 2x each) via start_subtask
- After 2 retries still failing → log HARD_FAILURE to event_log.jsonl, continue with remaining

**Skip list**: Before starting, run `--all` to find already-passing epics. Do NOT redo them.

---

## Universal Pilot Compliance Audit (V2.8 — ALL 10 orchestrators)

Before batching remaining 160 workers, EVERY Phase Orchestrator MUST:

1. Spawn EPIC-W7-001 (or first available epic) as a pilot worker
2. Wait for result
3. Run 8-check audit:

| Check | Verification | Hard Fail? |
|-------|-------------|------------|
| 0 | **MCP PROBE**: pilot's FIRST action must be `mcp__jcodemunch-mcp__resolve_repo` WITH RETRY (see V2.8 MCP Retry Rule below). If still fails after retry → HARD FAIL immediately. Do NOT proceed. | YES |
| 1 | Phase-specific jcodemunch-mcp tools were called AND evidenced in artifact text | YES |
| 2 | sequential-thinking MCP was used AND evidenced in artifact text | YES |
| 3 | Output artifact exists AND size > 200 bytes | YES |
| 4 | `manifest.json` updated: this phase's status = completed | YES |
| 5 | Agent Tracking block present (Agent Name, Bobcoins Used, Execution Time) | SOFT WARNING only |
| 6 | Phase-specific success criterion met (see per-phase section below) | YES |
| 7 | DNA violations (Phase 5 ONLY: no NUnit/MSTest, no lock(), UTF-8 encoded) | YES (Phase 5 only) |

**If any HARD check fails**:
- Log `pilot_failed` to `.lamport/wave7/event_log.jsonl`
- Write `docs/brain/EPIC-W7-001/failure-analysis.md` (or first epic used)
- Report `PILOT_FAILURE` to Tier 1
- **HALT — do NOT spawn remaining workers**
- **If Check 0 fails after retry (MCP_UNAVAILABLE): HALT immediately. Report `PILOT_FAILURE` to Tier 1. Do NOT execute epics directly. Do NOT pivot. Escalate for environment fix.**

## V2.8 MCP Cold-Start Retry Rule (ALL workers, ALL phases — PERMANENT)

Root cause of Category B failures: `jcodemunch-mcp` uses `stdio` transport — the server process
needs ~2–5s to start on a cold subagent session. `sequential-thinking` uses `npx -y` which needs
~3–15s on first use (package download). When 40 workers spawn simultaneously, the later workers
in the batch hit the `stdio` pipe backlog and get no handshake response, causing them to silently
mark MCP as unavailable.

**Every worker MUST implement this startup sequence:**

```
STEP 0a (jcodemunch probe):
  Call mcp__jcodemunch-mcp__resolve_repo.
  If it returns a valid repo object → proceed.
  If it returns error/null → wait 5 seconds → retry ONCE.
  If retry still fails → set internal flag MCP_FAILED=true.
  Return { "status": "MCP_FAILED", "epic_id": "<EPIC_ID>", "error": "jcodemunch unavailable after retry" }
  DO NOT produce a native-fallback artifact. HALT and return MCP_FAILED.

STEP 0b (sequential-thinking probe):
  Call mcp__sequential-thinking__sequentialthinking with thought="probe: starting <EPIC_ID> analysis", thoughtNumber=1, totalThoughts=1, nextThoughtNeeded=false.
  If it returns a valid response → proceed.
  If it fails → wait 5 seconds → retry ONCE.
  If retry still fails → set internal flag SEQ_FAILED=true.
  Return { "status": "MCP_FAILED", "epic_id": "<EPIC_ID>", "error": "sequential-thinking unavailable after retry" }
  DO NOT produce a native-fallback artifact. HALT and return MCP_FAILED.
```

**If worker returns MCP_FAILED**: orchestrator logs it, waits 30 seconds, then re-spawns that single
worker. This gives the stdio process time to finish initializing before the retry. Up to 2 re-spawns
per epic before escalating to HARD_FAILURE.

**Why NOT native fallback**: Native-fallback artifacts (Cat B/C) are indistinguishable from real MCP
output to downstream phases. A false "completed" state in Phase 0 corrupts all downstream phases (1–6)
that depend on live complexity data. A clean MCP_FAILED return is far preferable — it is retryable
and traceable.

---

## NO PIVOT RULE (V2.8 — PERMANENT)

`spawn_subagent` workers HAVE full MCP access (`jcodemunch-mcp`, `sequential-thinking`). The orchestrator MUST always delegate work to workers via `spawn_subagent`:
- **REQUIRED**: All epics executed by spawned workers using the appropriate v12-phaseN worker mode
- **FORBIDDEN**: Orchestrator executes epics directly in its own session as a fallback to failed workers
- **FORBIDDEN**: Orchestrator writes templated artifacts using only epic-list data without live MCP calls
- **FORBIDDEN**: Orchestrator marks phase complete when worker MCP tool calls were not made
- Any orchestrator that executes epics directly instead of spawning workers = PROTOCOL VIOLATION
- If workers genuinely cannot access MCP: HALT, report `MCP_UNAVAILABLE` to Tier 1, escalate for environment fix — do NOT work around it

---

## POST-BATCH HOOK (V2.8 — MANDATORY after EVERY batch, not just at phase end)

After every batch of workers returns — BEFORE spawning the next batch — the orchestrator MUST
run the deterministic compliance hook. This catches Cat B/C artifacts immediately, within the
same phase execution, rather than discovering them 160 epics later.

```bash
# Set env vars from the batch that just completed, then run:
export WAVE7_BATCH_PHASE="<phase>"           # e.g. "0"
export WAVE7_BATCH_EPICS="EPIC-W7-001 EPIC-W7-002 ..."  # the IDs in this batch
python3 .bob/hooks/after_subagent_batch.py
```

**Interpreting exit code:**

| Exit | Meaning | Orchestrator action |
|------|---------|---------------------|
| `0`  | ALL_PASS — all epics in batch pass all 7 hard checks | Proceed to next batch |
| `1`  | HAS_FAILURES — one or more epics failed a hard check | Read `/tmp/wave7_redo.txt`; re-spawn only those epics; re-run hook on redo results; repeat until exit 0 or redo count = 2 (then HARD_FAILURE) |
| `2`  | HOOK_ERROR — hook invocation failed | Escalate to Tier 1 immediately; do NOT proceed |

**Hard checks run by the hook (all 7 must pass per epic):**

| # | Check | What it detects |
|---|-------|----------------|
| 1 | `artifact_exists` | Output file is present |
| 2 | `min_size` | File ≥ 200 bytes |
| 3 | `no_denial` | **No Cat B/C denial phrase** ("not available as callable tool", "simulated via static", etc.) |
| 4 | `jcm_evidence` | Phase-specific jcodemunch tool keywords in content |
| 5 | `seq_evidence` | "sequential" / "sequentialthinking" / "thought 1" in content |
| 6 | `manifest_complete` | `manifest.json` phases.<key>.status = "completed" |
| 7 | `agent_name` | Correct mode slug present in artifact Agent Tracking |

**Machine-readable JSON** is always written to `/tmp/wave7_audit_result.json`.
**Redo list** written to `/tmp/wave7_redo.txt` (one epic ID per line, empty if all pass).

**Why hooks instead of inline script?** Hooks fire deterministically outside the orchestrator's
LLM context, on every batch, regardless of what the orchestrator decides to check. The orchestrator
cannot "forget" to run the check — the hook is a separate process with its own exit code that
the orchestrator reads. This is structurally different from an inline `python3 -c "..."` snippet
that the orchestrator might skip if context pressure is high.

---

## POST-PHASE COMPLIANCE SCAN (V2.8 — MANDATORY, runs after ALL batches complete)

After ALL batches complete (every batch already passed post-batch hook), run a final full-phase
scan to verify the phase-level invariant (161/161 compliant) before logging `phase_N_complete`:

```bash
python3 scripts/wave7_batch_audit.py --phase <N> --all
# Exit 0 → log phase_N_orchestrator_complete and report VERIFIED_COMPLETE to Tier 1
# Exit 1 → re-spawn non-compliant epics (redo list at /tmp/wave7_redo.txt)
# Do NOT log phase_N_complete until exit code is 0
```

**This replaces** the old inline `python3 -c "..."` compliance snippet from V2.7. The new script
is deterministic, checks all 7 hard checks (including denial-phrase detection the old script missed),
and produces machine-readable JSON + a redo list file.

---

## Template 1 — Phase 0: Hotspot Analysis

**Phase Orchestrator Mode**: `wave-orch-phase0`
**Worker Mode**: `v12-phase0-hotspot`
**Pilot Check 6 criterion**: output `00-hotspots.md` exists and is non-empty, `cyc_confirmed` field present in return value

### Worker Description (spawn_subagent)

```
Epic: <EPIC_ID>
Method: <METHOD_NAME> (CYC: <CYC>)
Source: <SOURCE_FILE>
Wave: 7
Phase: 0 — Hotspot Analysis

GRAPHIFY (MANDATORY — before any analysis):
  Run: graphify update . --no-cluster --no-description
  Read: .graphify/GRAPH_REPORT.md — note god nodes and communities relevant to <METHOD_NAME>

YOUR TASK (V2.8 — follow EXACTLY, including cold-start probes):

STEP 0a — jcodemunch cold-start probe (MANDATORY FIRST ACTION):
  Call mcp__jcodemunch-mcp__resolve_repo with path "/home/malhitticrypto/universal-or-strategy".
  If it returns a valid repo object → proceed to STEP 0b.
  If it returns error or is unavailable → wait 5 seconds → retry ONCE.
  If retry still fails → return { "status": "MCP_FAILED", "epic_id": "<EPIC_ID>", "error": "jcodemunch unavailable after retry" } and STOP.
  DO NOT write any artifact. DO NOT fall back to native tools.

STEP 0b — sequential-thinking cold-start probe (MANDATORY SECOND ACTION):
  Call mcp__sequential-thinking__sequentialthinking with:
    thought="probe: starting <EPIC_ID> Phase 0 analysis", thoughtNumber=1, totalThoughts=1, nextThoughtNeeded=false
  If it returns a valid response → proceed to STEP 1.
  If it fails → wait 5 seconds → retry ONCE.
  If retry still fails → return { "status": "MCP_FAILED", "epic_id": "<EPIC_ID>", "error": "sequential-thinking unavailable after retry" } and STOP.
  DO NOT write any artifact. DO NOT fall back to native tools.

STEP 1. Use mcp__jcodemunch-mcp__search_symbols to locate method <METHOD_NAME> in <SOURCE_FILE>
STEP 2. Use mcp__jcodemunch-mcp__get_symbol_complexity to get current CYC score
STEP 3. Use mcp__jcodemunch-mcp__get_blast_radius to identify impact scope
STEP 4. Use mcp__jcodemunch-mcp__get_hotspots to identify related complexity hotspots
STEP 5. Use mcp__sequential-thinking__sequentialthinking (3 thoughts minimum) to structure your analysis:
   - Thought 1: Complexity drivers — what are the top 3 sources of CYC?
   - Thought 2: Extraction strategy — how many helpers, what are their responsibilities?
   - Thought 3: Risk assessment — threading constraints, blast radius, correctness risks
STEP 6. Write docs/brain/<EPIC_ID>/00-hotspots.md with:
   - Method name, CYC, file path
   - Blast radius summary (from get_blast_radius output)
   - Top 3 complexity drivers (from sequential thinking Thought 1)
   - Recommended extraction count and helper names
   - MCP Evidence section with actual tool call results
   - Sequential Thinking Evidence section with actual thought content
   - Agent Tracking block (Agent Name: v12-phase0-hotspot, Bobcoins Used: [amount], Execution Time: [duration])
STEP 7. Update docs/brain/<EPIC_ID>/manifest.json:
   - Set phases.phase_0.status = "completed"
   - Set phases.phase_0.output = "00-hotspots.md"
STEP 8. Return: { "status": "success", "output_path": "docs/brain/<EPIC_ID>/00-hotspots.md", "cyc_confirmed": <CYC> }
STEP 9. GRAPHIFY SHUTDOWN: Run graphify update . --no-cluster --no-description
```

---

## Template 2 — Phase 1: Scope Definition

**Phase Orchestrator Mode**: `wave-orch-phase1`
**Worker Mode**: `v12-phase1-scope`
**Pilot Check 6 criterion**: return value has `scope_confirmed_single_method: true`

### Worker Description (spawn_subagent)

```
Epic: <EPIC_ID>
Method: <METHOD_NAME> (CYC: <CYC>)
Source: <SOURCE_FILE>
Wave: 7
Phase: 1 — Scope Definition
Input: docs/brain/<EPIC_ID>/00-hotspots.md

GRAPHIFY (MANDATORY — before any analysis):
  Run: graphify update . --no-cluster --no-description
  Read: .graphify/GRAPH_REPORT.md — note god nodes and communities relevant to <METHOD_NAME>

YOUR TASK:
1. Read docs/brain/<EPIC_ID>/00-hotspots.md (Phase 0 output)
2. Use mcp__jcodemunch-mcp__get_file_outline on <SOURCE_FILE>
3. Use mcp__jcodemunch-mcp__find_references for <METHOD_NAME>
4. Use mcp__jcodemunch-mcp__get_dependency_graph on <SOURCE_FILE>
5. Use mcp__sequential-thinking__sequentialthinking to define scope boundaries
6. SCOPE RULE: ONE method only. No other methods in scope.
7. Write docs/brain/<EPIC_ID>/00-scope.md with:
   - Single method in scope: <METHOD_NAME>
   - Current CYC: <CYC>, Target CYC: <= 8
   - File: <SOURCE_FILE>
   - Callers count (from find_references)
   - Scope boundary statement: "Only <METHOD_NAME> and its new extracted helper methods"
   - Agent Tracking block (Agent Name: v12-phase1-scope, Bobcoins Used: [amount], Execution Time: [duration])
8. Update docs/brain/<EPIC_ID>/manifest.json phases.phase_1.status = "completed"
9. Return: { "status": "success", "output_path": "docs/brain/<EPIC_ID>/00-scope.md", "scope_confirmed_single_method": true }
10. GRAPHIFY SHUTDOWN: Run graphify update . --no-cluster --no-description
```

---

## Template 3 — Phase 1.5: Scope Boundary Validation

**Phase Orchestrator Mode**: `wave-orch-phase1-5`
**Worker Mode**: `v12-phase1-5-boundary`
**Pilot Check 6 criterion**: return value has `boundary_verdict: "PASS"`

### Worker Description (spawn_subagent)

```
Epic: <EPIC_ID>
Method: <METHOD_NAME> (CYC: <CYC>)
Source: <SOURCE_FILE>
Wave: 7
Phase: 1.5 — Scope Boundary Validation (V12.23 No Scope Creep Protocol)
Input: docs/brain/<EPIC_ID>/00-scope.md

GRAPHIFY (MANDATORY — before any analysis):
  Run: graphify update . --no-cluster --no-description
  Read: .graphify/GRAPH_REPORT.md — note god nodes and communities relevant to <METHOD_NAME>

YOUR TASK:
1. Read docs/brain/<EPIC_ID>/00-scope.md (Phase 1 output)
2. Use mcp__jcodemunch-mcp__get_symbol_source for <METHOD_NAME>
3. Use mcp__jcodemunch-mcp__get_blast_radius — confirm blast limited to target + new helpers
4. Use mcp__jcodemunch-mcp__find_references — confirm no unintended callsite changes
5. Use mcp__sequential-thinking__sequentialthinking to validate no scope creep
6. BOUNDARY RULE: If scope exceeds single method + its new helpers → verdict FAIL
7. Write docs/brain/<EPIC_ID>/01-scope-boundary.md with:
   - boundary_verdict: PASS or FAIL
   - Reason (if FAIL: describe the scope violation)
   - Confirmed scope: "<METHOD_NAME> + N new helper methods (to be named in Phase 2)"
   - Agent Tracking block (Agent Name: v12-phase1-5-boundary, Bobcoins Used: [amount], Execution Time: [duration])
8. Update docs/brain/<EPIC_ID>/manifest.json phases.phase_1_5.status = "completed"
9. Return: { "status": "success", "output_path": "docs/brain/<EPIC_ID>/01-scope-boundary.md", "boundary_verdict": "PASS" }
10. GRAPHIFY SHUTDOWN: Run graphify update . --no-cluster --no-description
```

---

## Template 4 — Phase 2: Architecture Planning

**Phase Orchestrator Mode**: `wave-orch-phase2`
**Worker Mode**: `v12-phase2-architecture`
**Pilot Check 6 criterion**: return value has `max_cyc_projected <= 8`
**Jane Street KB**: Phase Orchestrator runs BEFORE spawning workers:
- `python scripts/query_kb.py "extraction patterns"`
- `python scripts/query_kb.py "complexity reduction FSM"`
- `python scripts/query_kb.py "lock-free actor pattern"`

### Worker Description (spawn_subagent)

```
Epic: <EPIC_ID>
Method: <METHOD_NAME> (CYC: <CYC>)
Source: <SOURCE_FILE>
Wave: 7
Phase: 2 — Architecture Planning
Input: docs/brain/<EPIC_ID>/01-scope-boundary.md

Jane Street KB Results (apply these patterns):
<INSERT KB QUERY RESULTS HERE>

GRAPHIFY (MANDATORY — before any analysis):
  Run: graphify update . --no-cluster --no-description
  Read: .graphify/GRAPH_REPORT.md — note god nodes and communities relevant to <METHOD_NAME>

YOUR TASK:
1. Read docs/brain/<EPIC_ID>/01-scope-boundary.md
2. Use mcp__jcodemunch-mcp__get_context_bundle for <METHOD_NAME>
3. Use mcp__jcodemunch-mcp__get_call_hierarchy for <METHOD_NAME>
4. Use mcp__jcodemunch-mcp__get_dependency_graph on the method
5. Use mcp__jcodemunch-mcp__get_extraction_candidates for <METHOD_NAME>
6. Use mcp__sequential-thinking__sequentialthinking to design extraction plan
7. DESIGN RULE: Each extracted helper method MUST have projected CYC <= 8.
   Remaining parent method MUST also have projected CYC <= 8.
8. Write docs/brain/<EPIC_ID>/02-architecture-plan.md with:
   - Original method CYC: <CYC>
   - Extraction plan: list each new helper method name + what it does + projected CYC
   - Parent method after extraction: projected CYC
   - max_cyc_projected: <max of all projected CYCs> (MUST be <= 8)
   - Jane Street alignment notes
   - Agent Tracking block (Agent Name: v12-phase2-architecture, Bobcoins Used: [amount], Execution Time: [duration])
9. Update docs/brain/<EPIC_ID>/manifest.json phases.phase_2.status = "completed"
10. Return: { "status": "success", "output_path": "docs/brain/<EPIC_ID>/02-architecture-plan.md", "extraction_count": N, "max_cyc_projected": M }
11. GRAPHIFY SHUTDOWN: Run graphify update . --no-cluster --no-description
```

---

## Template 5 — Phase 3: DNA Audit

**Phase Orchestrator Mode**: `wave-orch-phase3`
**Worker Mode**: `v12-phase3-audit`
**Pilot Check 6 criterion**: return value has `dna_verdict: "PASS"`

### Worker Description (spawn_subagent)

```
Epic: <EPIC_ID>
Method: <METHOD_NAME> (CYC: <CYC>)
Source: <SOURCE_FILE>
Wave: 7
Phase: 3 — DNA Audit
Input: docs/brain/<EPIC_ID>/02-architecture-plan.md

GRAPHIFY (MANDATORY — before any analysis):
  Run: graphify update . --no-cluster --no-description
  Read: .graphify/GRAPH_REPORT.md — note god nodes and communities relevant to <METHOD_NAME>

YOUR TASK:
1. Read docs/brain/<EPIC_ID>/02-architecture-plan.md
2. Use mcp__jcodemunch-mcp__search_ast to check for "lock(" patterns in <SOURCE_FILE>
3. Use mcp__jcodemunch-mcp__get_layer_violations for architectural compliance
4. Use mcp__jcodemunch-mcp__get_dependency_cycles to verify no circular deps introduced
5. Use mcp__sequential-thinking__sequentialthinking to validate DNA compliance
6. DNA CHECKS (all must pass):
   - Zero lock() blocks planned
   - ASCII-only string literals (no Unicode in literals)
   - UTF-8 source files (no BOM)
   - No scope creep beyond target method
   - xUnit tests ([Fact], Assert.Equal()) planned — NEVER NUnit/MSTest
   - No max_cyc_projected > 8
7. Write docs/brain/<EPIC_ID>/03-audit-report.md with:
   - dna_verdict: PASS or FAIL
   - Each DNA check result (pass/fail)
   - violations: [] (empty list if PASS)
   - Agent Tracking block (Agent Name: v12-phase3-audit, Bobcoins Used: [amount], Execution Time: [duration])
8. Update docs/brain/<EPIC_ID>/manifest.json phases.phase_3.status = "completed"
9. Return: { "status": "success", "output_path": "docs/brain/<EPIC_ID>/03-audit-report.md", "dna_verdict": "PASS", "violations": [] }
10. GRAPHIFY SHUTDOWN: Run graphify update . --no-cluster --no-description
```

---

## Template 6 — Phase 4: Ticket Generation

**Phase Orchestrator Mode**: `wave-orch-phase4`
**Worker Mode**: `v12-phase4-tickets`
**Execution model**: `spawn_subagent("general")` — parallel, 20/turn (read-only planning, no .cs writes)
**Pilot Check 6 criterion**: return value has `ticket_count >= 1`

### Worker Description

```
Epic: <EPIC_ID>
Method: <METHOD_NAME> (CYC: <CYC>)
Source: <SOURCE_FILE>
Wave: 7
Phase: 4 — Ticket Generation
Inputs: docs/brain/<EPIC_ID>/02-architecture-plan.md + docs/brain/<EPIC_ID>/03-audit-report.md

GRAPHIFY (MANDATORY — before any analysis):
  Run: graphify update . --no-cluster --no-description
  Read: .graphify/GRAPH_REPORT.md — note god nodes and communities relevant to <METHOD_NAME>

YOUR TASK:
STEP 0a — MCP probe: mcp__jcodemunch-mcp__resolve_repo ("/home/malhitticrypto/universal-or-strategy")
  If fails → retry once after 5s → if still fails → return MCP_FAILED, STOP.
STEP 0b — SEQ probe: mcp__sequential-thinking__sequentialthinking (thought="probe", thoughtNumber=1, totalThoughts=1, nextThoughtNeeded=false)
  If fails → retry once → return MCP_FAILED, STOP.

STEP 1. Read docs/brain/<EPIC_ID>/02-architecture-plan.md and 03-audit-report.md
STEP 2. mcp__jcodemunch-mcp__get_symbol_complexity for <METHOD_NAME>
STEP 3. mcp__jcodemunch-mcp__get_extraction_candidates for <SOURCE_FILE>
STEP 4. mcp__sequential-thinking__sequentialthinking (min 3 thoughts):
   Thought 1: How many extraction tickets? One ticket = one extracted helper = one concern.
   Thought 2: For each ticket: what lines move, what the helper is named, projected CYC after.
   Thought 3: Verify every helper AND parent method will have CYC <= 8 post-extraction.
STEP 5. Write docs/brain/<EPIC_ID>/04-tickets.md with:
   - ticket_count: N  (minimum 1; one per extracted helper)
   - For each ticket 1..N:
       ticket_id: T
       helper_name: <HelperMethodName>
       concern: <single responsibility this helper will own>
       lines_to_move: <description of code block>
       cyc_reduction: <estimated CYC removed from parent>
       projected_helper_cyc: <CYC of new helper, MUST be <= 8>
   - projected_parent_cyc_after_all: <parent CYC after all extractions, MUST be <= 8>
   - Agent Tracking block (Agent Name: v12-phase4-tickets, Bobcoins Used: [amount], Execution Time: [duration])
STEP 6. Update docs/brain/<EPIC_ID>/manifest.json phases.phase_4.status = "completed"
STEP 7. Return: { "status": "success", "output_path": "docs/brain/<EPIC_ID>/04-tickets.md", "ticket_count": N }
STEP 8. GRAPHIFY SHUTDOWN: Run graphify update . --no-cluster --no-description
```

---

## Template 7 — Phase 4.5: Ticket Review (Jane Street Gate)

**Phase Orchestrator Mode**: `wave-orch-phase4-5`
**Worker Mode**: `v12-phase4-5-review`
**Execution model**: `start_subtask(mode="v12-phase4-5-review")` — sequential MCP
**Pilot Check 6 criterion**: return value has `review_verdict: "PASS"`
**Jane Street KB** (orchestrator loads before spawning — hardcoded fallback if Firebase unavailable):
```
CYC<=8 mandatory. Single-responsibility extraction. Actor/Enqueue model — no lock() blocks.
Make illegal states unrepresentable. Zero-allocation hot paths. Pure predicates for REAPER/Safety.
```

### Worker Description

```
Epic: <EPIC_ID>
Method: <METHOD_NAME> (CYC: <CYC>)
Source: <SOURCE_FILE>
Cluster: <CLUSTER_NAME> — <CLUSTER_DESC>
Wave: 7
Phase: 4.5 — Ticket Review (Jane Street Validation Gate)
Input: docs/brain/<EPIC_ID>/04-tickets.md

Jane Street KB Rules:
CYC<=8 mandatory — functions >8 are cognitively unsafe at microsecond latency.
Single-responsibility extraction. Actor/Enqueue model — no lock() blocks.
Make illegal states unrepresentable. Zero-allocation hot paths.

GRAPHIFY (MANDATORY — before any analysis):
  Run: graphify update . --no-cluster --no-description
  Read: .graphify/GRAPH_REPORT.md — note god nodes and communities relevant to <METHOD_NAME>

YOUR TASK:
STEP 0 — MCP probes (same as all workers, see Cold-Start Retry Rule above).
STEP 1. Read docs/brain/<EPIC_ID>/04-tickets.md
STEP 2. mcp__sequential-thinking__sequentialthinking (one thought per ticket + summary thought):
   For each ticket: does it extract exactly ONE concern? Is projected_helper_cyc <= 8?
   Is projected_parent_cyc_after_all <= 8? No lock()? Valid xUnit test plan possible?
   Summary: overall review_verdict.
STEP 3. Write docs/brain/<EPIC_ID>/04-5-ticket-review.md with:
   - review_verdict: PASS or FAIL
   - per_ticket_results: [{ticket_id, verdict, reason}] for each ticket
   - failed_tickets: [] (empty if PASS, list of ticket_ids if FAIL)
   - jane_street_alignment: brief statement per cluster domain
   - Agent Tracking block (Agent Name: v12-phase4-5-review, Bobcoins Used: [amount], Execution Time: [duration])
STEP 4. Update docs/brain/<EPIC_ID>/manifest.json phases.phase_4_5.status = "completed"
STEP 5. Return: { "status": "success", "review_verdict": "PASS", "failed_tickets": [] }
  If review_verdict = FAIL: phase orchestrator must re-run phase 4 for this epic, then re-run 4.5.
STEP 6. GRAPHIFY SHUTDOWN: Run graphify update . --no-cluster --no-description
```

---

## Template 8 — Phase 5: Per-Ticket Execution (Code Writing)

**Architecture**: V3.0 FILE-LANE MODEL — see `docs/workflow/WAVE7_PHASE5_LANE_ASSIGNMENT.md`
**Lane Orchestrator Mode**: `wave-orch-phase5` (one per cluster, 7 total)
**Ticket Worker Mode**: `v12-p5-ticket` (code writing, uses `v12-engineer` permissions)
**Verify Worker Mode**: `v12-p5-verify` (independent verification)
**Review Worker Mode**: `v12-p6-review` (final epic review)
**Execution model**: `start_subtask` sequential within lane — one ticket at a time, verify immediately after

### KEY DESIGN RULES (V3.0)

1. **One lane per source file** — 40 file-lanes total (see lane table in WAVE7_PHASE5_LANE_ASSIGNMENT.md)
2. **Lanes run in parallel** — each is a separate Bob IDE session with exclusive file ownership
3. **Tickets run sequentially within a lane** — never two ticket workers writing the same `.cs` file
4. **Each ticket = its own start_subtask** — followed immediately by its own verify start_subtask
5. **Phase 6 review runs per-epic** — after ALL tickets for that epic pass verification
6. **Build retry protocol** — if `dotnet build` fails with a lock error, wait 15s + retry (max 3 retries)
7. **Epic order in lane**: CYC descending — hardest methods first (see WAVE7_PHASE5_LANE_ASSIGNMENT.md)

### Lane Orchestrator Loop (pseudo-code)

```
# Lane orchestrator receives: cluster_name, cluster_desc, file_path, epic_list (CYC desc)
# Runs as: start_subtask(mode="wave-orch-phase5", message=LANE_ORCH_MSG)

for EPIC_ID in epic_list:
    read docs/brain/EPIC_ID/04-tickets.md → ticket_count = N

    for T in 1..N:
        # --- TICKET EXECUTION ---
        start_subtask(mode="v12-p5-ticket", message=TICKET_MSG(EPIC_ID, T, cluster_desc))
        # worker writes: docs/brain/EPIC_ID/ticket-T-completion.md
        # worker runs: dotnet csharpier format src/ AND dotnet build

        if return.status == "BUILD_FAIL":
            wait 15s → retry start_subtask(mode="v12-p5-ticket", ...) once
            if still BUILD_FAIL: log STUCK_TICKET, skip to next epic

        # --- TICKET VERIFICATION ---
        start_subtask(mode="v12-p5-verify", message=VERIFY_MSG(EPIC_ID, T, claimed_cyc))
        # worker writes: docs/brain/EPIC_ID/ticket-T-verification.md
        # worker independently re-measures CYC, checks lock(), build, xUnit

        if verification_verdict == "FAIL":
            retry: start_subtask(mode="v12-p5-ticket", ...) + start_subtask(mode="v12-p5-verify", ...)
            if still FAIL: log STUCK_TICKET, skip to next epic

    # --- EPIC FINAL REVIEW ---
    start_subtask(mode="v12-p6-review", message=REVIEW_MSG(EPIC_ID, verified_cycs))
    # worker writes: docs/brain/EPIC_ID/05-completion-report.md

python3 scripts/wave7_batch_audit.py --phase 5 --epics <all_epic_ids_in_lane>
# exit 0 → log lane_FL-XX_complete
# exit 1 → redo failed epics
```

---

### Ticket Worker Message Template (v12-p5-ticket)

```
Epic: <EPIC_ID>
Method: <METHOD_NAME> (CYC: <CYC>, Target: <= 8)
Source: src/<SOURCE_FILE>
Ticket: T of N  (<TICKET_ID>: extract <HELPER_NAME> — <CONCERN>)
Cluster: <CLUSTER_NAME> — <CLUSTER_DESC>
Wave: 7

CLUSTER CONTEXT: <CLUSTER_DESC>
(This shapes how you name helpers, what invariants they must preserve, and what to test.)

Jane Street KB:
CYC<=8 mandatory — functions >8 are cognitively unsafe at microsecond latency.
Single-responsibility extraction. Actor/Enqueue model — no lock() blocks.
Make illegal states unrepresentable. Zero-allocation hot paths.

DNA RULES (non-negotiable — violation = immediate FAIL):
- xUnit ONLY: [Fact], Assert.Equal() — NEVER NUnit, NEVER MSTest
- UTF-8 source files (no BOM)
- Zero lock() blocks — use FSM/Actor Enqueue model
- ASCII-only string literals (no Unicode, no curly quotes)
- SINGLE CONCERN: only modify <METHOD_NAME> in src/<SOURCE_FILE>
- Run: dotnet csharpier format src/  AFTER every write
- Run: dotnet build  MUST pass with zero errors
  If build fails with lock/access error: wait 15s, retry build up to 3 times.
  If build fails with compilation error: fix it before returning.

GRAPHIFY (MANDATORY — before writing any code):
  Run: graphify update . --no-cluster --no-description
  Read: .graphify/GRAPH_REPORT.md — note god nodes and communities relevant to <METHOD_NAME>

YOUR TASK:
STEP 0a — MCP probe: mcp__jcodemunch-mcp__resolve_repo("/home/malhitticrypto/universal-or-strategy")
  If fails → retry once after 5s → return { "status": "MCP_FAILED" }, STOP.
STEP 0b — SEQ probe: mcp__sequential-thinking__sequentialthinking (probe thought)
  If fails → retry once → return { "status": "MCP_FAILED" }, STOP.

STEP 1. mcp__jcodemunch-mcp__get_symbol_source for <METHOD_NAME> in src/<SOURCE_FILE>
        (Get the current state of the method — may already be partially extracted by prior tickets)
STEP 2. mcp__jcodemunch-mcp__get_context_bundle for <METHOD_NAME>
        (Understand callers, callees, field dependencies)
STEP 3. mcp__sequential-thinking__sequentialthinking (min 3 thoughts):
   Thought 1: What exact lines constitute <CONCERN>? Where do they start/end?
   Thought 2: What parameters does <HELPER_NAME> need? What does it return?
              Are there field reads that must become parameters (no hidden state access)?
   Thought 3: Write the xUnit test FIRST. What inputs → what outputs?
              Name test: <HELPER_NAME>_<Scenario>_<ExpectedResult>
STEP 4. Apply the extraction:
   a. Read src/<SOURCE_FILE>
   b. Extract the <CONCERN> lines into private method <HELPER_NAME>(params) : returnType
   c. Replace extracted lines in <METHOD_NAME> with a call to <HELPER_NAME>(args)
   d. write_file / apply_diff with the modified src/<SOURCE_FILE>
   e. Write xUnit test to tests/ for <HELPER_NAME> with [Fact] attribute
   f. run: dotnet csharpier format src/
   g. run: dotnet build
      If lock error: wait 15s, retry (max 3). If compilation error: fix and rebuild.
   h. run: python3 scripts/complexity_audit.py | grep "<METHOD_NAME>"
      Verify CYC is reduced. (Final CYC <= 8 required after ALL tickets complete, not after each ticket.)
STEP 5. Write docs/brain/<EPIC_ID>/ticket-T-completion.md:
   - epic_id: <EPIC_ID>
   - ticket_id: T
   - helper_name: <HELPER_NAME>
   - concern_extracted: <CONCERN>
   - cyc_parent_now: <current CYC of METHOD_NAME after this extraction>
   - build_passed: true
   - tests_written: N (count of [Fact] tests added)
   - source_file: src/<SOURCE_FILE>
   - Agent Tracking block (Agent Name: v12-p5-ticket, Bobcoins Used: [amount], Execution Time: [duration])
STEP 6. Update docs/brain/<EPIC_ID>/manifest.json:
   - phases.phase_5_ticket_<T>.status = "completed"
STEP 7. Return: {
   "status": "success",
   "epic_id": "<EPIC_ID>",
   "ticket_id": T,
   "helper_name": "<HELPER_NAME>",
   "cyc_parent_now": <N>,
   "build_passed": true,
   "tests_written": <N>
}
STEP 8. GRAPHIFY SHUTDOWN: Run graphify update . --no-cluster --no-description
```

---

### Verify Worker Message Template (v12-p5-verify)

```
Epic: <EPIC_ID>
Method: <METHOD_NAME> (Original CYC: <CYC>)
Ticket: T of N — verifying extraction of <HELPER_NAME>
Source: src/<SOURCE_FILE>
Claimed cyc_parent_now: <CLAIMED_CYC>
Wave: 7
Phase: 5.V Ticket Verification — DO NOT TRUST Phase 5 self-reports. Verify independently.

GRAPHIFY (MANDATORY — before verification):
  Run: graphify update . --no-cluster --no-description
  Read: .graphify/GRAPH_REPORT.md — confirm <METHOD_NAME> community context

YOUR TASK:
STEP 0 — MCP probes (same cold-start retry as all workers).

STEP 1. mcp__jcodemunch-mcp__register_edit for src/<SOURCE_FILE>
        (Force re-index so complexity data reflects latest writes)
STEP 2. INDEPENDENT CHECK A: mcp__jcodemunch-mcp__get_symbol_complexity(<METHOD_NAME>)
   → Record actual measured CYC. If T < N (not last ticket): just record, don't require <= 8 yet.
     If T == N (last ticket): MUST be <= 8 or this is a FAIL.
STEP 3. INDEPENDENT CHECK B: mcp__jcodemunch-mcp__search_ast pattern="lock(" on src/<SOURCE_FILE>
   → MUST return zero matches (no lock() in entire file)
STEP 4. INDEPENDENT CHECK C: mcp__jcodemunch-mcp__get_changed_symbols()
   → MUST only show <METHOD_NAME> + <HELPER_NAME> as changed (no unintended symbol changes)
STEP 5. INDEPENDENT CHECK D: grep "[Fact]" in tests/ subdirectory
   → MUST find at least 1 new [Fact] test for <HELPER_NAME>
STEP 6. INDEPENDENT CHECK E: grep "TestFixture\|\[Test\]\|\[TestCase\]" in tests/
   → MUST return zero (no NUnit/MSTest contamination)
STEP 7. INDEPENDENT CHECK F: execute_command: dotnet build
   → MUST pass with zero errors
STEP 8. mcp__sequential-thinking__sequentialthinking:
   Summarize all check results. Is verification_verdict PASS or FAIL?
STEP 9. Write docs/brain/<EPIC_ID>/ticket-T-verification.md:
   - epic_id: <EPIC_ID>
   - ticket_id: T
   - helper_name: <HELPER_NAME>
   - verification_verdict: PASS or FAIL
   - check_A_cyc_measured: <N>  (claimed: <CLAIMED_CYC>)
   - check_B_no_lock: true/false
   - check_C_scope_clean: true/false
   - check_D_xunit_test_found: true/false
   - check_E_no_nunit: true/false
   - check_F_build: true/false
   - failures: [] or [list of failed checks]
   - Agent Tracking block (Agent Name: v12-p5-verify, Bobcoins Used: [amount], Execution Time: [duration])
STEP 10. Update docs/brain/<EPIC_ID>/manifest.json:
   - phases.phase_5_verify_<T>.status = "completed"
STEP 11. Return: {
   "status": "success",
   "verification_verdict": "PASS",
   "ticket_id": T,
   "cyc_measured": <N>,
   "failures": []
}
STEP 12. GRAPHIFY SHUTDOWN: Run graphify update . --no-cluster --no-description
```

---

### Final Review Worker Message Template (v12-p6-review)

```
Epic: <EPIC_ID>
Method: <METHOD_NAME> (Original CYC: <ORIGINAL_CYC>)
Source: src/<SOURCE_FILE>
Cluster: <CLUSTER_NAME> — <CLUSTER_DESC>
All tickets completed: <N> tickets, all verified PASS
Final claimed CYC: <FINAL_CLAIMED_CYC>
Wave: 7
Phase: 6 — Final Epic Review & Completion

GRAPHIFY (MANDATORY — before final review):
  Run: graphify update . --no-cluster --no-description
  Read: .graphify/GRAPH_REPORT.md — confirm <METHOD_NAME> no longer appears as a god node

YOUR TASK:
STEP 0 — MCP probes (same cold-start retry as all workers).

STEP 1. mcp__jcodemunch-mcp__register_edit for src/<SOURCE_FILE>
STEP 2. mcp__jcodemunch-mcp__get_symbol_complexity(<METHOD_NAME>)
   → final_cyc MUST be <= 8 — if not, this review FAILS and lane orch must re-run tickets
STEP 3. mcp__jcodemunch-mcp__get_hotspots
   → confirm <METHOD_NAME> is no longer in top hotspots
STEP 4. mcp__jcodemunch-mcp__get_repo_health
   → confirm no new dependency cycles or dead code introduced
STEP 5. mcp__sequential-thinking__sequentialthinking:
   Thought 1: CYC journey: <ORIGINAL_CYC> → <FINAL_CYC>. Is Jane Street standard met?
   Thought 2: Are all N helpers well-named for the <CLUSTER_NAME> domain context?
   Thought 3: Are xUnit tests sufficient? Do they cover edge cases relevant to this cluster?
   Thought 4: Write completion narrative (2-3 sentences summarizing the refactor).
STEP 6. Write docs/brain/<EPIC_ID>/05-completion-report.md:
   - epic_id: <EPIC_ID>
   - method_name: <METHOD_NAME>
   - source_file: src/<SOURCE_FILE>
   - cluster: <CLUSTER_NAME>
   - original_cyc: <ORIGINAL_CYC>
   - final_cyc: <FINAL_CYC>  (MUST be <= 8)
   - wave_ready: true
   - ticket_count: N
   - helpers_extracted: [list of helper method names]
   - tests_written_total: <total [Fact] tests across all tickets>
   - jane_street_compliant: true
   - completion_narrative: "<narrative from Thought 4>"
   - phases_completed: [0, 1, 1.5, 2, 3, 4, 4.5, "5.1".."5.N", "5.1V".."5.NV", 6]
   - Agent Tracking block (Agent Name: v12-p6-review, Bobcoins Used: [amount], Execution Time: [duration])
STEP 7. Update docs/brain/<EPIC_ID>/manifest.json:
   - phases.phase_6.status = "completed"
   - status = "complete"
   - wave = 7
   - final_cyc = <FINAL_CYC>
STEP 8. Return: {
   "status": "success",
   "epic_id": "<EPIC_ID>",
   "final_cyc": <N>,
   "wave_ready": true,
   "helpers_extracted": N,
   "tests_written_total": N
}
STEP 9. GRAPHIFY SHUTDOWN: Run graphify update . --no-cluster --no-description
```

---

## Lamport Event Sequence (All Phases)

Each Phase Orchestrator appends to `.lamport/wave7/event_log.jsonl`:

```jsonl
// Phase start (all phases):
{"timestamp":"<ISO>","lamport_clock":<N>,"epic_id":"WAVE-7","phase":"<P>","tier":"phase_orch","event_type":"phase_<P>_orchestrator_start","status":"running"}

// Phase 5 file-lane start (one per lane, 40 total):
{"timestamp":"<ISO>","lamport_clock":<N>,"epic_id":"WAVE-7","phase":"5","tier":"phase_orch","event_type":"phase_5_lane_start","lane":"FL-XX","cluster":"<S>","file":"<FILE>","epic_count":<N>,"status":"running"}

// Phase 5 file-lane complete (one per lane, 40 total):
{"timestamp":"<ISO>","lamport_clock":<N>,"epic_id":"WAVE-7","phase":"5","tier":"phase_orch","event_type":"phase_5_lane_complete","lane":"FL-XX","completed":<N>,"failed":0,"status":"complete"}

// Phase complete (after all lanes/workers done):
{"timestamp":"<ISO>","lamport_clock":<N>,"epic_id":"WAVE-7","phase":"<P>","tier":"phase_orch","event_type":"phase_<P>_orchestrator_complete","status":"complete","completed":161,"failed":0}
```

Phase 6 (terminal wave gate) additionally appends:
```jsonl
{"timestamp":"<ISO>","lamport_clock":<FINAL>,"epic_id":"WAVE-7","phase":"6","tier":"phase_orch","event_type":"wave_7_complete","status":"complete","methods_above_8_remaining":0}
```

---

## Lamport Dependency Gates (Chain Verification)

Each Phase Orchestrator's VERY FIRST action is to verify the predecessor event:

| Phase Orch | Must Find In Lamport Log Before Starting |
|-----------|------------------------------------------|
| wave-orch-phase0 | `wave_start` OR `wave_reset` event |
| wave-orch-phase1 | `phase_0_orchestrator_complete` with `status=complete` |
| wave-orch-phase1-5 | `phase_1_orchestrator_complete` with `status=complete` |
| wave-orch-phase2 | `phase_1_5_orchestrator_complete` with `status=complete` |
| wave-orch-phase3 | `phase_2_orchestrator_complete` with `status=complete` |
| wave-orch-phase4 | `phase_3_orchestrator_complete` with `status=complete` |
| wave-orch-phase4-5 | `phase_4_orchestrator_complete` with `status=complete` |
| wave-orch-phase5 | `phase_4_5_orchestrator_complete` with `status=complete` |
| wave-orch-phase6 | ALL 40 `phase_5_lane_complete` events present AND `phase_5_orchestrator_complete` |

**Phase 5 gate is special**: Tier 1 advances to Phase 6 only after all 40 file-lanes log `phase_5_lane_complete`. The Phase 5 top-level orchestrator aggregates all lane completions then logs `phase_5_orchestrator_complete`.

If the predecessor event is absent: **HALT**, report `DEPENDENCY_NOT_MET` to Tier 1. Do NOT spawn any workers.

---

*Templates Version: V3.0 — Bob IDE V2 — Per-Ticket Execution + File-Lane Architecture*
*Created: 2026-06-25 | Updated: 2026-06-29*
*V3.0: Phase 5 is now the file-lane model. 40 file-lanes (grouped by 7 architectural clusters). Each ticket = its own start_subtask(v12-p5-ticket) + start_subtask(v12-p5-verify). Phase 6 runs per-epic after all tickets pass. Cluster domain context injected into every worker. Build retry protocol (15s wait, max 3 retries). Epic order within lane: CYC descending.*
*V2.9: spawn_subagent confirmed 0 MCP tools. start_subtask confirmed full MCP. Sequential-only model permanent.*
