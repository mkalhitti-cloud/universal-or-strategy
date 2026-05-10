# Phase Execution Workflow

This workflow dictates how individual Phases (defined in an Epic) are executed, audited, and deployed using the multi-agent pipeline.

## 1. Phase Initialization
- **Input:** Specific Phase instructions from `docs/brain/epic_roadmap.md`.
- **Action:** 
  1. Verify the current branch is clean (0-delta).
  2. Orchestrator (Antigravity/Gemini) delegates to ARCHITECT (Claude) to draft the `implementation_plan.md` using the `/architect_intake` workflow.

## 2. Red Team Adjudication (P4)
- **Agent:** Adjudicator / Arena Red Team.
- **Action:** 
  1. The drafted plan is scrutinized for compliance with the Lock-Free Actor Pattern and ASCII constraints.
  2. Run `scripts/amal_harness.py` for high-performance paths.
  3. Re-draft via `/hardened_adjudication` if failures occur.

## 3. Surgical Implementation (P5)
- **Agent:** ENGINEER (Codex / Jules / Rovo Dev).
- **Action:** 
  1. Handoff plan to Engineer using the standard pipe-prompt command:
     `Get-Content docs\brain\implementation_plan.md -Raw | acli rovodev run` (for Rovo)
  2. Engineer executes surgical edits to `src/`.

## 4. Post-Surgery Validation (P6)
- **Action:** 
  1. Engineer runs `powershell -File .\deploy-sync.ps1`.
  2. ASCII Gate and lock audits MUST PASS.
  3. Await Director confirmation (NinjaTrader F5 Compile).
  4. Once confirmed, Orchestrator writes completion status to `docs/brain/nexus_a2a.json` and signals readiness for the next Phase.
