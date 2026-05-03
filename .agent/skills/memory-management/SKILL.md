---
name: memory-management
description: >
  V12 session memory and context persistence protocol. Governs how agents (Antigravity,
  Claude, Codex, Gemini CLI, Jules, Droid) persist state across sessions using the
  Nexus Blackboard (nexus_a2a.json), task.md, compaction snapshots, and knowledge items.
  Use when: starting a new session, before /compact, after a context overflow, or when
  an agent needs to resume a mission mid-phase.
  Adapted from gstack/memory-management (MIT License) for the V12 multi-agent architecture.
triggers:
  - /compact
  - save context
  - session memory
  - resume mission
  - context overflow
  - pick up where we left off
  - nexus sync
---

# V12 Memory Management (gstack Edition)

The V12 project runs long multi-session missions with multiple agents. Without explicit
memory management, each new session starts blind -- wasting tokens re-reading context
that was already established.

This skill defines the **V12 Memory Hierarchy** and the rules for each layer.

---

## V12 Memory Hierarchy (Priority Order)

```
1. Nexus Blackboard (nexus_a2a.json)   -- Machine-readable. Wins over everything.
2. Compaction Snapshots                 -- Human-readable mission state for new sessions.
3. task.md                              -- Human-readable mission dashboard. Secondary.
4. implementation_plan.md              -- The active plan. Read after blackboard.
5. Antigravity Knowledge Items (KI)   -- Cross-session curated knowledge. Read if topic-matched.
6. Conversation Logs                   -- Raw logs. Read only when KIs/blackboard are insufficient.
```

**Resolution Rule**: If layers conflict, higher priority wins. If `nexus_a2a.json` says P5
and `task.md` says P3, trust the blackboard.

---

## Session Start Protocol (All Agents)

Every agent starting a session MUST execute these steps before any other action:

```
Step 1: Read nexus_a2a.json
  -> Extract: phase, build_tag, active_agent, p5_handoff, p6_verdict
  -> If unreadable: HALT, report to Director

Step 2: Route based on phase (see /nexus-sync decision tree)

Step 3: Read implementation_plan.md if phase is P3, P4, or P5
  -> This is the "what to do" document

Step 4: Read task.md for human-readable mission context
  -> Useful for understanding Director intent, not for routing decisions

Step 5: Check Antigravity KIs for any relevant curated knowledge
  -> Location: C:\Users\Mohammed Khalid\.gemini\antigravity\knowledge\
  -> Match topic to current mission focus
```

---

## Compaction Protocol (Before /compact)

When context exceeds ~70% OR before transitioning from P5 to P6:

### Step 1: Write Compaction Snapshot

```
File: docs/brain/memory/<mission_name>_compaction_state.md
```

Include:
- Mission name + BUILD_TAG
- Current phase and active_agent
- Completed steps (numbered list)
- **Next step** (single, specific action)
- Open blockers (with file:line references)
- Links to implementation_plan.md and nexus_a2a.json

**DO NOT** save raw code in snapshots -- only pointers to files.

Example:
```markdown
# Compaction State: ADR-019 Substrate Bypass
BUILD_TAG: 1111.003-v28.0-adr019
Phase: P5_ENGINEER_ACTIVE
Active Agent: Codex
Completed:
  1. Pre-edit AMAL baseline saved to tmp/p5_baseline_amal.txt
  2. SubmitOrderUnmanaged call injected at src/Execution/BracketManager.cs:L247
Next Step: Run dotnet test Testing.csproj and verify 0 failures
Blockers: None
Plan: docs/brain/implementation_plan.md
Blackboard: docs/brain/nexus_a2a.json
```

### Step 2: Run /compact

```
/compact Save mission state to docs/brain/memory/<mission_name>_compaction_state.md before summarizing.
```

---

## Session Resumption Protocol

When starting a new session after a compaction or break:

```
Step 1: Read docs/brain/memory/ -- find the latest compaction snapshot
Step 2: Read nexus_a2a.json -- confirm phase matches snapshot
Step 3: Read "Next Step" from snapshot -- this is your first action
Step 4: Report to Director: "Resumed [BUILD_TAG] at [phase]. Next: [next step]."
```

If snapshot and blackboard disagree: blackboard wins. Update snapshot to match.

---

## Knowledge Item (KI) Protocol

KIs are curated, persistent memory about the V12 codebase stored in the Antigravity knowledge base.

**When to create a KI**:
- A debugging investigation revealed a non-obvious architectural insight.
- A skill or workflow produced a reusable pattern worth preserving.
- A mission produced a key architectural decision (record it as an ADR + KI).

**When to read a KI**:
- Before any deep architectural exploration -- check if the knowledge already exists.
- When starting a new mission in a familiar area (SIMA, CIT, bracket submission).

**KI location**: `C:\Users\Mohammed Khalid\.gemini\antigravity\knowledge\`
**KI baseline**: `v12_baseline/artifacts/v12_dna.md` -- read this for fundamental V12 DNA rules.

---

## Token Efficiency Rules

1. **Read the blackboard first** -- never spend tokens re-discovering current phase.
2. **Read KIs before web searches** -- curated knowledge is more reliable than fresh research.
3. **Use /nexus-sync as the session entry point** -- it loads all context in one structured pass.
4. **Compact before P5->P6 transition** -- always. Long P5 sessions have the most context waste.
5. **Never save raw code in snapshots** -- save file pointers only.

---

## Mandatory Self-Improvement Audit

After EVERY skill use:
1. Was any memory layer missing from the hierarchy? Add it.
2. Did a new session fail to pick up state correctly? Fix the session start protocol.
3. Was a compaction snapshot incomplete? Add the missing field to the template.
4. Did an agent waste tokens re-discovering something a KI already contained? Flag it.

**If no gap:** `skill(memory-management): no gaps identified.`
**Commit format:** `skill(memory-management): [what was fixed and why]`

---

*Source: gstack/memory-management (MIT License, garrytan/gstack) adapted for V12 multi-agent architecture*
*V12 additions: Nexus Blackboard priority, compaction trigger at P5->P6 boundary, KI protocol, PowerShell paths*
*Build: 1111.003-v28.0-adr019 | Adapted: 2026-04-20*
