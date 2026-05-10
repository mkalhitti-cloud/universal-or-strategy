# Epic Planning Workflow

This workflow is designed to structure large-scale architectural refactoring missions (Epics) into manageable, autonomous phases executed by the Sovereign Agent pipeline.

## 1. Epic Intake & Forensic Mapping
- **Input:** Director provides a high-level Epic goal (e.g., "SIMA Subgraph Extraction").
- **Agent:** ARCHITECT (Claude) or FORENSICS (Codex).
- **Action:** 
  1. Parse the epic objectives against the V12 Global Standards.
  2. Run diagnostic scans across the codebase to identify 'God Classes' and tight coupling.
  3. Emit a `docs/brain/epic_roadmap.md` detailing the required phases.

## 2. Phase Segmentation
- **Action:** Break the Epic into 3-5 distinct, sequential Phases.
- **Criteria for a Phase:** 
  - Must be independently verifiable.
  - Must pass the ASCII & No-Lock checks.
  - Must culminate in a zero-delta branch commit before proceeding.

## 3. Delegation & Handoff
- For each Phase, trigger the `/phase_execution` workflow.
- Ensure the `nexus_a2a.json` state is updated with Epic progress to maintain context across multi-agent handoffs.
