---
description: Ultraplan + AMAL (Asynchronous Multi-Agent Loop) for V12 Sovereign Integration
---

# Ultraplan + AMAL Integration Workflow (V1.0)

Use this workflow to orchestrate high-complexity architectural repairs (e.g., V14.2 Sovereign Photon) using Claude Code's web-based Ultraplan interface.

## 1. MISSION LAUNCH [P1/P3]

- **Command**: `/ultraplan [mission_objective]`
- **Action**: Antigravity (Mission Commander) prepares the brief and triggers the cloud draft.
- **Goal**: Free up the local terminal and save session tokens during intensive design.
- **Crucial Execution Rules**:
  - **Browser**: Always execute the web session on the standard Google Chrome browser using the `malhitti` authenticated account.
  - **NO PLAN MODE**: Do **NOT** enable "Plan Mode" in the web UI. Plan Mode revokes local file-write and script execution permissions, which will block Claude from running critical physical validation gates (e.g., `dotnet build` or `amal_harness.py`).
  - **Hands-Off Validation**: Allow Claude to autonomously design, create local draft files, and execute pre-flight physical tests before intervening.

## 2. WEB-BASED ADVERSARIAL REVIEW [P4 ADJUDICATION]

- **Surface**: Open the Ultraplan session link in the browser.
- **Audit**: **Codex, Jules, and Gemini CLI** perform a "Remote Audit" of the web plan.
- **Critique**: Leave inline comments for Claude (P3) to address "Ghost-Order" or "Zero-Alloc" logic flaws.

## 3. ITERATIVE REFINEMENT [P3]

- **Action**: Claude refines the plan based on P5 Auditor feedback in the cloud session.
- **Consensus**: Consensus is reached when the P5 Auditors signal `UltraThink: PASS` in the `nexus_a2a.json` blackboard.

## 4. TELEPORT & IMPLEMENT [P5 ENGINEERING]

- **Action**: Teleport the audited plan back to the terminal using "Approve plan and teleport back to terminal".
- **Handoff**: Antigravity (Mission Commander) triggers the **Nexus Relay** (`/nexus-relay`).
- **Implementation**: **Codex/Jules (P5 Engineer)** executes the surgical repair using the approved `implementation_plan.md`.
- **Mandatory**: P5 must run Phase 0 baseline + Phase 3 post-edit verification from `/nexus-relay` before handing off to P6.

## 5. RECOVERY & SYNC

- **Update**: Update `nexus_a2a.json` phase to `P6_VALIDATION_PENDING` with `p5_handoff` block populated.
- **Validation**: Trigger P6 Validator (Gemini CLI) in a fresh session via `/mission-validate`.

---

**Protocol Mandate**: TRIPLE-AGENT ULTRATHINK
**Review Surface**: Claude Code Web (Ultraplan)
**Orchestrator**: Antigravity (Mission Commander)

---

## Post-Use Audit (NON-NEGOTIABLE)

1. Were phase labels correct throughout this session? (P4=Adjudication, P5=Engineering)
2. Did the AMAL gate run before final adjudication?
3. Was the Nexus Relay triggered correctly for P5?

**If no gap found:** `workflow(ultraplan_amal): no gaps identified.`
**Commit format:** `workflow(ultraplan_amal): [what was fixed and why]`
