# Wave 7 Phase 0 Worker Message Template V3.0
# For use with spawn_subagent("general") — parallel execution

## How This Template Works

The orchestrator (`wave-orch-phase0` V3.0) calls `spawn_subagent("general", description=<filled template>)`
with up to 20 workers per turn. Each worker:
1. Reads `docs/brain/EPIC-W7-NNN/precomputed.json` (all graph data pre-loaded)
2. Reads the relevant OKF Jane Street KB entries from `docs/brain/wave7-okf-cache.json`
3. Reads the source file from `src/` to understand complexity drivers
4. Writes `docs/brain/EPIC-W7-NNN/00-hotspots.md`
5. Updates `docs/brain/EPIC-W7-NNN/manifest.json` → phases.phase_0.status = "completed"

No MCP calls needed. No custom mode needed. Runs in parallel.

---

## Worker Description Payload (fill EPIC_ID, METHOD_NAME, CYC, SOURCE_FILE, RISK_LEVEL, ESTIMATED_EXTRACTIONS)

```
WAVE 7 PHASE 0 HOTSPOT ANALYSIS — {EPIC_ID}
Agent label: wave7-phase0-worker
YOLO MODE: All tools pre-approved. Execute autonomously. No confirmations.

YOUR TASK: Write the Phase 0 hotspot analysis artifact for {EPIC_ID}.

== STEP 1: Load pre-computed data ==
Read file: docs/brain/{EPIC_ID}/precomputed.json
This contains: method_name, source_file, cyc, risk_level, estimated_extractions.
Pre-computed values are authoritative — do NOT second-guess them.

== STEP 2: Read the source method ==
Read the source file listed in precomputed.json.
Find the method {METHOD_NAME}. Read its full body.
Identify the TOP 3 complexity drivers (nested ifs, switches, loops, boolean chains).
For each driver estimate its CYC contribution (~N points).

== STEP 3: Load Jane Street KB ==
Read file: docs/brain/wave7-okf-cache.json
Extract the entry with key "complexity-reduction". Read its content field.
Note the applicable patterns for CYC > {CYC} methods (Helper Extraction, Early Return, etc).

== STEP 4: Write 00-hotspots.md ==
Write docs/brain/{EPIC_ID}/00-hotspots.md with ALL of these sections:

```markdown
# Phase 0 Hotspot Analysis — {EPIC_ID}

## Agent Tracking
- **Agent Name**: wave7-phase0-worker
- **Data Source**: precomputed.json v3.0 (wave7-epic-list.json + complexity_audit.py)
- **Completed At**: [ISO timestamp]

---

## Method Summary

| Field | Value |
|---|---|
| **EPIC** | {EPIC_ID} |
| **Method** | `{METHOD_NAME}` |
| **Source File** | `{SOURCE_FILE}` |
| **CYC (Confirmed)** | **{CYC}** (Jane Street threshold: <=8) |
| **Risk Level** | {RISK_LEVEL} |
| **Estimated Extractions** | {ESTIMATED_EXTRACTIONS} |
| **CYC Over Threshold** | {CYC - 8} |

---

## Top 3 Complexity Drivers

### Driver 1 — [name] (CYC contribution ~N)
[description from reading the source]

### Driver 2 — [name] (CYC contribution ~N)
[description]

### Driver 3 — [name] (CYC contribution ~N)
[description]

---

## Jane Street KB: Applicable Patterns

[Extract 2-3 applicable patterns from the complexity-reduction.md OKF entry]

---

## Recommended Extraction Plan

| # | New Method Name | CYC Reduction | Rationale |
|---|---|---|---|
| 1 | `[name]` | ~N | [why] |
| 2 | `[name]` | ~N | [why] |

**Target CYC after extraction**: ≤8 (Jane Street threshold)

---

## Data Provenance
- Source: docs/brain/{EPIC_ID}/precomputed.json (schema v3.0)
- OKF KB: docs/brain/wave7-okf-cache.json (complexity-reduction entry)
- Epic list: docs/brain/wave7-epic-list.json
```

== STEP 5: Update manifest ==
Read docs/brain/{EPIC_ID}/manifest.json.
Set phases.phase_0.status = "completed" and phases.phase_0.completed_at = [ISO timestamp].
Write the updated manifest back.

== DONE ==
Return a one-line summary: "EPIC-W7-NNN phase_0 complete. CYC={CYC}, extractions={N}, artifact written."
```

---

## Notes for Orchestrator

- Spawn up to 20 workers per turn — they run in parallel
- After each batch of 20, run: `python3 scripts/wave7_batch_audit.py --phase 0 --epics <batch_ids>`
- Any FAIL: re-spawn just that worker (up to 2 retries)
- All 161 pass: log phase_0_complete to .lamport/wave7/event_log.jsonl, return VERIFIED_COMPLETE
