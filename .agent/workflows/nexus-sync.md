---
description: Sync with the V12 Repair Mission Blackboard -- Morpheus autonomous session entry protocol
---

# Nexus Sync: Autonomous Session Entry

This is the **mandatory first action** for every agent starting any session in this repo.
It enables Morpheus Level 5 operation: zero human prompts between phases.

---

## Step 1: Load the Blackboard (ALWAYS FIRST)

```
READ docs/brain/nexus_a2a.json
```

`nexus_a2a.json` is the **single source of truth**. It wins over `task.md` if they disagree.
If `nexus_a2a.json` is missing or unreadable: **HALT. Report to Director. Do NOT infer state.**

---

## Step 1.5: Observability & Graph Sync (MANDATORY)

1. **Linear Task Sync**: Verify the active Linear issue matches the mission phase. Pull any blocker/status updates.
2. **LangSmith Trace Init**: Ensure current agent reasoning trace is tagged with the Mission Name + `BUILD_TAG`.
3. **Graphify Knowledge Layer**: Use **jCodeMunch MCP** tools (`resolve_repo`, `get_repo_outline`) to verify structural indexing. If `graphify-out/graph.json` is stale, queue a graphify update.
4. **Sentry Context**: Verify that Sentry crash reports from the previous phase (if any) are acknowledged before proceeding.

---

## Step 2: Autonomous Phase Routing (Morpheus Decision Tree)

Read `nexus_a2a.json.phase` and route accordingly. No human confirmation required.

```
phase == "P3_ARCHITECT_ACTIVE"
  p3_handoff.ready_for_p4 == false  ->  Resume P3: re-read architect SKILL.md, continue design
  p3_handoff.ready_for_p4 == true   ->  Trigger P4: open /multi_agent_audit in Arena session

phase == "P4_ADJUDICATION_WAITING"
  -> Trigger Arena Red Team via /multi_agent_audit
  -> Do NOT proceed to P5 without unanimous PASS verdict

phase == "P5_ENGINEER_ACTIVE"
  p5_handoff.ready_for_p6 == false  ->  Resume P5: re-read nexus-relay.md, continue implementation
  p5_handoff.ready_for_p6 == true   ->  Trigger P6: spawn fresh Gemini CLI session with /mission-validate

phase == "P6_VALIDATION_PENDING"
  p6_verdict.status == "PENDING"    ->  You ARE the P6 Validator. Run /mission-validate now.
  p6_verdict.status == "PASS"       ->  Advance to P7: run security-hardening workflow
  p6_verdict.status == "FAIL"       ->  Route back to P5 with p6_verdict.failure_evidence as context

phase == "P7_SENTINEL_ACTIVE"
  -> Run .agent/workflows/security-hardening.md
  -> On completion: set phase to MISSION_COMPLETE, update build_tag, report to Director

phase == "MISSION_COMPLETE"
  -> Report success. No further action.

phase == UNKNOWN
  -> HALT. Read task.md for human-readable context. Report ambiguity to Director.
```

---

## Step 3: Adopt Role Context

After routing, load the skill or workflow for your assigned phase:

| Phase | Load |
|-------|------|
| P2 | `.agent/skills/systematic-debugging/SKILL.md` |
| P3 | `.agent/skills/architect/SKILL.md` |
| P4 | `.agent/workflows/multi_agent_audit.md` |
| P5 | `.agent/workflows/nexus-relay.md` |
| P6 | `.agent/workflows/mission-validate.md` |
| P7 | `.agent/workflows/security-hardening.md` |

---

## Step 4: Adopt UltraThink + Build Target

- Adopt the **Triple-Agent UltraThink** reasoning profile for your phase.
- Set your build target from `nexus_a2a.json.build_tag` (do NOT hardcode it).
- Report sync status: "Synced to [phase] / [build_tag]. Next action: [action]."

---

## Step 5: Mandatory Post-Use Self-Improvement Audit

1. Was the decision tree ambiguous for any phase you encountered?
2. Did the blackboard schema lack a field you needed to make a routing decision?
3. Was any phase entry missing from the decision tree?

**If no gap found:** `workflow(nexus-sync): no gaps identified.`
**Commit format:** `workflow(nexus-sync): [what was fixed and why]`
