---
description: Comprehensive security audit and hardening for the V12 project using gstack CSO protocol.
---

# Security Hardening Workflow (P7 Sentinel)

This workflow is the final gate (P7 Sentinel) before mission completion. It mandates the use of the **gstack CSO (Chief Security Officer)** skill to ensure the Universal OR Strategy is secure.

## Phase 1: Autonomous Security Audit (Morpheus Level 5)

1. **Invoke the CSO Skill**: Read `.agent/skills/cso/SKILL.md` to adopt the Chief Security Officer persona.
2. **Execute Audit**: Run the daily or comprehensive security audit as defined in the CSO skill.
   - **Secrets Archaeology**: Verify no keys/credentials in plain text.
   - **Supply Chain**: Check dependencies (e.g., OSV.dev).
   - **Code Vulnerabilities**: Static analysis for OWASP Top 10 / STRIDE threat modeling.

## Phase 2: IDE-Based Forensic Verification

1. If the CSO skill identifies any critical vulnerabilities (score drops below the required 8/10 or 2/10 confidence gate):
   - **DO NOT GUESS**. Use the IDE's "Run and Debug" features to step through the identified vulnerability path.
   - Establish a "Logical Proof of Failure" before initiating a repair.
2. If repairs are needed, route back to **P5 ENGINEER** via `/nexus-relay` with the exact vulnerability report.

## Phase 3: Sentinel Sign-off

1. If the audit passes the confidence gate:
   - **Observability Sweep**: Confirm Sentry has zero unhandled exceptions for the new `BUILD_TAG`. Verify LangSmith traces are fully labeled.
   - **Linear Sync**: Mark the associated Linear issue as resolved/completed using Linear MCP.
   - Update `docs/brain/nexus_a2a.json` phase to `MISSION_COMPLETE`.
   - Update `docs/brain/task.md` matrix (P7 completed).
   - Report success to the Director.

---

## Post-Use Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

1. **Was the CSO skill utilized correctly?** Check if any step was skipped.
2. **Did IDE-based debugging successfully pinpoint vulnerabilities?** Update guidelines if not.
3. **Was the routing back to P5 clear?** Fix any ambiguities.

**If no gap found, state:** `workflow(security-hardening): no gaps identified.`

**Commit format:** `workflow(security-hardening): [what was fixed and why]`
