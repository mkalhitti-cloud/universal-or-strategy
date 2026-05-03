---
name: Nexus Bridge (Self-Improving V3)
description: >
  Synchronizes state between Antigravity (P1), Claude (P3), Arena (P4), and Codex (P5)
  via the Nexus Blackboard (docs/brain/nexus_a2a.json) and Ultraplan (Cloud).
  Mandates the Triple-Agent UltraThink Reasoning Protocol for all structural
  hardening repairs. Use this whenever the Director is launching a mission,
  switching engineers, or performing P4 Adjudication Audits.
---

# Nexus Bridge (V3)

Use this skill to manage the multi-agent mesh for the V12 Universal OR Strategy.

## Commands

### /nexus:launch [ULTRAPLAN]

Initiate a high-complexity design mission using the web-based review surface.

- Action: Execute `/ultraplan` with the Mission Commander's brief.
- Action: Post the session URL to `docs/brain/nexus_a2a.json`.
- Action: Notify the Director to begin the Web Review.

### /nexus:sync

Sync with the V12 Repair Mission Blackboard.

- Action: Read `docs/brain/nexus_a2a.json` and `implementation_plan.md`.
- Action: Update system prompt with "Triple-UltraThink Mandate" and "Build 1109.003-v3" objective.
- Action: Report sync status to the Director.

### /nexus:matrix

Synchronize the Mission Progress Matrix from `task.md` to the Arena Dashboard.

- Action: Run `python scripts/sync_mission_matrix.py`.
- Action: Verify the `docs/arena_dashboard.html` reflects the latest `task.md` status.
- Action: This MUST be executed after every significant task status change.

### /nexus:relay

Hand off implementation to a subagent (Codex or Jules).

- Action: If Codex is rate-limited, use Jules-P5.
- Action: Write the handoff instruction to `docs/brain/nexus_a2a.json` and notify the Director.

### /nexus:audit

Run a Dual-Adversarial P4 Adjudication Audit (Local or Web).

- Action: If Ultraplan link exists, audit the web design via inline comments.
- Action: Trigger both Codex and Jules for an UltraThink review of the plan/diff.
- Action: Synthesize forensic findings into a "Logical Proof of Success."

## UltraThink Reasoning Protocol (MANDATORY)

Before any code execution, the agent MUST perform a high-density logical simulation:

1. **P2/P4 Diagnosis/Audit**: Verify the "Logical Proof of Failure" against current source bytes.
2. **Side-Effect Audit**: Predict the impact on SIMA Fleet synchrony and Rithmic Direct streams.
3. **Safety Gate**: No `lock()` patterns; ASCII-only strings.
4. **Consensus**: Both Codex and Jules must signal `UltraThink: PASS` before signing off to P5.

## Mandatory Self-Improvement (Pillar 4)

After EVERY mission phase or skill use, the agent MUST perform a post-use audit:

1. **Did the bridge desync?** Identify the stale key in `nexus_a2a.json` and fix the sync logic.
2. **Was UltraThink bypassed?** Hardify the trigger boundary in this SKILL.md.
3. **Was a handoff ambiguous?** Update the `relay` template in the blackboard.

If no gap is found, state: `skill(nexus-bridge): no gaps identified -- mission context stable.`
Self-improvement commits require NO Director approval. Commit: `skill(nexus-bridge): [what was hardened]`

## Security Validation Gates (MANDATORY)

1. **The P5 Engineer "Pre-Deploy" Gate**: Before any Engineer (Codex/Jules-P5) calls `deploy-sync.ps1`, they MUST trigger a `/security:analyze` scan via the Nexus Bridge. The Orchestrator (Antigravity/Gemini CLI) will execute the scan to mathematically validate unmanaged memory safety and SharedArrayBuffer race conditions.
2. **Supply Chain Validation (P7 Sentinel)**: Any pull requests or updates to the Next.js/Node.js Antigravity OS stack require running `/security:scan-deps` via OSV.dev before merging new npm packages.

## Instructions

1. **Mission Goal**: Execute Build 1109.003-v3.
2. **Engineer Roles**: Codex (Primary), Jules (Standby).
3. **UltraThink**: Always reason in depth before proposing edits.
4. **Pre-Deploy Security**: Ensure `/security:analyze` passes before compiling.
5. **Deployment**: Run `deploy-sync.ps1` after every code edit.
6. **Dashboard Sync**: Run `/nexus:matrix` after every `task.md` update to maintain Mission Visibility.
7. **Safety**: No internal locks in C#; use Enqueue(); ASCII-only.
8. **Context**: Load the Blackboard frequently.
