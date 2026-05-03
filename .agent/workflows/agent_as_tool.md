---
description: Agent-as-Tool pattern -- invoke FORENSICS or ENGINEER as a stateless, single-use tool for a discrete task
---

Use this workflow when you need a single clean output from a specialized agent without a full multi-phase handoff.
Best for: quick forensic diagnosis, isolated code edits, one-shot searches, standalone audits.

---

## Phase 1: Define the Tool Invocation

1. **Identify the agent to invoke**:
   - **FORENSICS (Codex)**: Diagnosis, grep audits, logic tracing, "Logical Proof of Failure"
   - **ENGINEER (Codex/Jules)**: Surgical file edits, script execution, compile verification
   - **ARCHITECT (Claude)**: One-shot design review only (no code writes unless Director permits)

2. **Define the task boundary**:
   - Input: exact file(s), function(s), or error message
   - Expected output: one specific artifact (diagnosis, diff, or code block)
   - Scope: single task — if it requires multiple decisions, use `/coordinator` instead

3. **Write the Tool Prompt** — must include:
   - Agent name + version target
   - Exact input (file path, function name, or error)
   - Expected output format
   - Hard stop condition ("return when you have X, do not proceed further")

---

## Phase 2: Execute & Collect

1. Invoke the agent with the prompt.
2. Wait for the single output.
3. Do NOT chain into further tasks within the same invocation.

---

## Phase 3: Validate & Route

1. Verify the output matches the expected format.
2. If output is a diagnosis → route to ARCHITECT via `/architect_intake`.
3. If output is a code diff → route to ENGINEER for application + deploy-sync.
4. If output is ambiguous → re-invoke with a tighter prompt (log what was ambiguous).

---

## Phase 5: Mandatory Self-Improvement Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

1. **Was the task scope too broad?** Narrow the prompt template.
2. **Did the agent exceed its single-task boundary?** Add a hard-stop instruction.
3. **Was the output format wrong?** Fix the output format spec.
4. **Was routing to the next phase unclear?** Clarify Phase 3 routing rules.

**If no gap found, state:** `workflow(agent_as_tool): no gaps identified -- workflow correct as written.`

Skipping the audit is a protocol violation. No Director approval needed for self-improvement edits.

**Commit format:** `workflow(agent_as_tool): [what was fixed and why]`
