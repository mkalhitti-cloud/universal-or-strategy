# Wave 7 — Phase 0 & Phase 1 MCP Compliance Audit

**Generated**: 2026-06-26  
**Auditor**: top_orch (Tier 1)  
**Scope**: All 161 epics, Phase 0 (`00-hotspots.md`) and Phase 1 (`00-scope.md`)

---

## Executive Summary

| Metric | Count |
|--------|-------|
| Total epics | 161 |
| Phase 0 fully compliant (JCM + sequential-thinking both evidenced) | **11 / 161** |
| Phase 1 fully compliant | **1 / 161** (EPIC-W7-001 only) |
| Both phases compliant | **0 / 161** |
| Phase 0 needs redo | **150 / 161** |
| Phase 1 needs redo | **160 / 161** |

**Root cause (V2.6 session — now corrected)**: Workers ran inside `spawn_subagent` and fell back to native file tools (`read_file`, `grep`, `GetSymbolsOverview`) instead of calling MCP tools. This was caused by a misconfiguration in that session — **not** a platform limitation. `spawn_subagent` workers DO have full MCP access (`jcodemunch-mcp`, `sequential-thinking`). Artifacts produced without MCP evidence fail Pilot Checks 1 and 2. The protocol has been fixed in V2.7: orchestrators must HALT and escalate if workers fail the MCP probe rather than pivoting to direct execution.

---

## Phase 0 Compliance Detail

### Compliant (11 / 161) — JCM + sequential-thinking both evidenced
These 11 epics explicitly reference jcodemunch-mcp tool calls and sequential-thinking in their `00-hotspots.md`:

| Epic ID | Method |
|---------|--------|
| EPIC-W7-031 | AuditMaster_HandleNakedPosition |
| EPIC-W7-093 | Dispatch_ProcessFleetLoop |
| EPIC-W7-111 | HydrateExpectedPositionsFromBroker |
| EPIC-W7-113 | HydrateFSMsFromWorkingOrders |
| EPIC-W7-124 | SymmetryFindDispatchForMasterFill |
| EPIC-W7-130 | SymmetryGuardCascadeFollowerCleanup |
| EPIC-W7-132 | SymmetryNormalizeTradeType |
| EPIC-W7-139 | UpdateStopOrder |
| EPIC-W7-152 | TryApplyConfigTarget_Value |
| EPIC-W7-154 | (sparse — resolved: TryHandleFleet_LongShort) |
| EPIC-W7-155 | TryHandleFleetCommand |

### Non-Compliant (150 / 161)

**Root cause breakdown**:
- Missing jcodemunch-mcp evidence in artifact: **131 epics** — workers used native grep/read_file instead of calling MCP
- Missing sequential-thinking evidence: **120 epics** — sequential-thinking not called (misconfiguration in that session)
- 1 epic (W7-118) explicitly logged MCP as "unavailable" in tracking block (V2.6 session issue — now resolved)

**Full list of P0 non-compliant epics** (need redo):
W7-001, W7-002, W7-003, W7-004, W7-005, W7-006, W7-007, W7-008, W7-009, W7-010,
W7-011 (seq only — missing JCM), W7-012, W7-013 (JCM only — missing seq), W7-014,
W7-015, W7-016, W7-017, W7-018, W7-019, W7-020, W7-021, W7-022, W7-023, W7-024,
W7-025, W7-026, W7-027, W7-028, W7-029, W7-030, W7-032, W7-033, W7-034, W7-035,
W7-036, W7-037, W7-038, W7-039, W7-040, W7-041, W7-042 through W7-092,
W7-094 through W7-110, W7-112, W7-114 through W7-129, W7-131, W7-133 through W7-147,
W7-148, W7-149 through W7-161 (excluding the 11 compliant above)

---

## Phase 1 Compliance Detail

### Compliant (1 / 161)
- **EPIC-W7-001** only — the pilot epic, run by the orchestrator with MCP access

### Non-Compliant (160 / 161) — Two root causes

#### Root Cause A: V2.6 session MCP misconfiguration (W7-002 through W7-041 — 40 epics)
Workers spawned via `spawn_subagent` for the first batch. Workers fell back to native tools and documented "MCP tools called: [unavailable]" in their tracking blocks. This was a V2.6 session misconfiguration — `spawn_subagent` workers DO have MCP access (confirmed V2.7). These 40 epics need redo with proper worker spawning under the V2.7 protocol.

| Epics | Count |
|-------|-------|
| W7-002 through W7-041 | 40 epics |

#### Root Cause B: Orchestrator-direct templated write (W7-042 through W7-161 — 120 epics)
After batch 1 failed MCP checks, the Phase 1 orchestrator pivoted to writing all remaining `00-scope.md` files directly using a Python loop — same template for all 120. Files have correct method/CYC/file data from epic list but were generated at identical timestamp `2026-06-26T02:35:31Z` with label "orchestrator direct -- MCP available in parent context". No live `find_references`, `get_file_outline`, or `get_dependency_graph` calls were made.

| Epics | Count |
|-------|-------|
| W7-042 through W7-161 | 120 epics |

---

## What Needs To Happen

### Required Action: Full Redo of Phase 0 and Phase 1

All 161 epics in Phase 0, and 160 epics in Phase 1, must be re-executed with proper MCP tool calls evidenced.

**Why this matters**: Phases 2–6 depend on accurate complexity analysis (Phase 0) and verified caller counts (Phase 1). If jcodemunch-mcp `get_symbol_complexity` was never called, the CYC values in the artifacts are from the static epic list — which has 5 blank-method sparse entries and several stale/0 CYC values. Phase 2 (architecture planning) will produce incorrect extraction plans if the CYC data is wrong.

### Permanent Protocol Fix (Applied — V2.7)

The following has been applied to `docs/workflow/PHASE_ORCHESTRATOR_TEMPLATES.md`:

1. **MCP Availability Probe** — Pilot worker MUST call `mcp__jcodemunch-mcp__resolve_repo` as its FIRST action. If it returns an error: pilot MUST fail with `MCP_UNAVAILABLE`, orchestrator MUST halt, do NOT pivot to direct execution.

2. **No Pivot Rule (V2.7)** — Orchestrators MUST NEVER execute epics directly as a fallback. `spawn_subagent` workers HAVE full MCP access. The 3-tier architecture (Tier 1 → Tier 2 Orchestrator → Tier 3 Workers) is always the correct model. If workers fail MCP probe: HALT and escalate — do NOT work around it.

3. **Post-Phase Artifact Scan** — After every phase completes, the orchestrator MUST run the compliance audit script before logging `phase_N_complete` to the Lamport log.

### Redo Strategy

**Correct approach — Fix the worker environment and re-spawn:**
`spawn_subagent` workers HAVE full MCP access (`jcodemunch-mcp`, `sequential-thinking`) — confirmed in V2.7. Re-run all 161 workers via the Phase Orchestrators using the standard 3-tier model. The orchestrator spawns workers; workers use MCP directly.

**Incorrect approach (do NOT use) — Orchestrator-direct:**
~~The phase orchestrator runs all 161 epics directly in its own session.~~ This bypasses the 3-tier architecture, produces non-auditable artifacts, and was a V2.6 workaround based on the false assumption that workers lacked MCP. It is permanently forbidden.

---

## Audit Script (re-runnable)

```bash
python3 scripts/audit_mcp_compliance.py --phase 0 --phase 1
```

Script location: `scripts/audit_mcp_compliance.py` (to be created — see protocol fix below).

---

## Lamport Log Entry

This audit result should be logged as:
```json
{"timestamp":"<ISO>","lamport_clock":14,"epic_id":"WAVE-7","phase":"0-1","tier":"top_orch","event_type":"mcp_compliance_audit","status":"FAIL","p0_compliant":11,"p1_compliant":1,"p0_needs_redo":150,"p1_needs_redo":160,"root_cause":"V2.6 session misconfiguration — workers did not call MCP. V2.7 protocol fixes this. spawn_subagent workers HAVE MCP access."}
```

---

*Audit version: V1.0 — Wave 7 Post-Phase-1*
