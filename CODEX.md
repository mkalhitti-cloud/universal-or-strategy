# NinjaScript V12 Project Standards (Codex Mirror)

- **Language**: C# 8.0 / .NET Framework 4.8 (NinjaTrader 8).
- **No Internal Locks**: `lock(stateLock)` is **BANNED**. All state mutations MUST use `Enqueue(ctx => ...)` by default. Exception: Build 981 direct-write for `stopOrders` during bracket submission.
- **Lifecycle**: Semaphores (\_simaToggleSem) MUST be released in finally blocks.
- **Refactoring**: Prefer explicit FirstOrDefault logic for instrument lookups (Reaper parity).
- **Style**: Use PascalCase for methods, camelCase for local variables. Avoid dense one-liners; prioritize "Metabolic Elegance."

## ðŸ›¡ï¸ Protocol Hardening (V12.Phase7)

### 1. Scope Control

- **Surgical Edits**: AI agents MUST restrict code modifications to the specific files requested in the Mission Brief. NEVER refactor unrelated files without explicit Director authorization.
- **Zero-Trust Planning**: Always generate an `implementation_plan.md` before applying code changes to local files.

### 2. WPF/UI Guardrails

- **Escalation**: If a UI layout or positioning task enters a loop (more than 2 failed attempts), the agent MUST halt and escalate to the Director for manual layout review.
- **Headless Mode**: Prefer headless execution for batch logic updates; do not attempt complex UI re-styling without a visual brief.

### 3. Path & Deployment Management

- **Source Truth**: All primary NinjaScript logic resides in `src/`.
- **Deployment**: Local builds MUST be synced to `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\Strategies\` using the `/deploy` skill.

## ðŸ•¹ï¸ Director Commands ($)

- **$PLAN_AUDIT**: Use `read_terminal` on the active Claude/Antigravity PID to ingest- **Engineer**: Implementation of surgical C# edits and performance optimizations.
- **Frontend Design (V12.15)**: High-fidelity dashboard and overlay development.
- **Forensics**: Strategic diagnosis and logic audits.
  before recommending approval to the Director.
- **$MISSION**: Initialize a new project phase via a Mission Brief artifact.
- **$AUDIT**: Trigger the `/audit` skill to scan the `src/` directory.

## Agent Synchronization

- **NO SIMULATION**: AI agents are STRICTLY BANNED from simulating the output of another agent (e.g., imagining an audit report). Every P5 sign-off must cite an authentic, system-generated log or file write.
- **P5 Redundancy Mandate**: Task-splitting during a P5 audit is **STRICTLY FORBIDDEN**. Every member of the Red Team (Codex, Gemini CLI, Jules) must independently audit the **entire** implementation plan. Consensus is only valid if every agent validates every target individually.
- **IDENTITY LOCK**: Codex MUST NOT pretend to be Claude or Antigravity. Hallucinating architectural sign-offs or 'imagining' orchestrator approval is a protocol violation.
- AI Agents (Anthropic, Codex, Antigravity, Cursor, Gemini) MUST follow the **[.agent/standards_manifesto.md](file:///.agent/standards_manifesto.md)** as the primary source of truth for architectural standards and safety protocols.

**Clipboard Mandate**: All cross-agent handoff prompts, implementation plans, and commands MUST be automatically copied to the Director's clipboard (e.g., via PowerShell `Set-Clipboard`) so manual copying is never required.

## MOVE-SYNC / Follower Order Replace Pattern (Build 947+)

- **FSM Required**: Any follower order cancel+resubmit MUST use the two-phase Replace FSM (`_followerReplaceSpecs` dict).
- **Never cancel+submit directly**: Raw `Cancel()` followed immediately by `Submit()` creates ghost orders. BANNED.
- **FSM states**: `PendingCancel` -> wait for `OnAccountOrderUpdate` confirm -> `Submitting` -> `SubmitFollowerReplacement`.
- **ATR tick absorption**: While in `PendingCancel`, sizing changes update `PendingReplacementSpec` only. One cancel, one resubmit.
- **Fill-during-gap guard**: Check if master filled before submitting replacement. If yes, route to REAPER repair.
- **ChangeOrder banned for fleet accounts**: `Account.Change` silently no-ops on Apex/Tradovate.

## Live Bug Triage Protocol

- **Codex first**: For any live trading anomaly, run `/live-bug-triage` workflow before writing any mission brief.
- **Codex = diagnosis, Sonnet = implementation**: Do not ask Codex for patches.
- **Plan audit required**: Paste Sonnet's plan to Antigravity for audit before approving.
- **Workflow file**: `.agent/workflows/live-bug-triage.md`

## CRITICAL: ASCII-Only in All C# String Literals

- NEVER use emoji, curly quotes, em-dashes, Unicode arrows, or box-drawing in Print() or any string literal.
- Non-ASCII inside C# strings breaks the NinjaTrader compiler with 300+ cascading errors (Build 936 incident).
- Allowed substitutions: (!) not emoji, -- not em-dash, -> not arrow, straight " not curly " "
- **NO SIMULATION**: Orchestrators (Antigravity / Gemini CLI) are STRICTLY BANNED from simulating sub-agent (Codex/Jules/Gemini) consensus. Every report must be backed by an authentic local log or file write from the specific agent.
- See .agent/standards_manifesto.md Section 7 for the full rule table and emergency fix sequence.

## Role Assignment (V12 Director's Gate)

| Role                         | Primary                 | Backup                |
| ---------------------------- | ----------------------- | --------------------- |
| P4 ENGINEER (code execution) | **Codex**               | Jules CLI, Gemini CLI |
| P3 ARCHITECT / P5 REVIEWER   | Claude Code             | --                    |
| P2 FORENSICS                 | Codex `forensics` agent | --                    |
| P1 ORCHESTRATOR              | Antigravity             | --                    |

Codex is the **PRIMARY code executor**. Jules CLI and Gemini CLI are identical twin backups
dispatched only when Codex is unavailable or the Director explicitly assigns a task to them.

## Agentic Patterns (Google Agentic Pattern Registry)

Codex is workflow-aware and MUST follow these patterns from `.agent/workflows/`:

| Slash Command        | Workflow File                           | Pattern                                                   |
| -------------------- | --------------------------------------- | --------------------------------------------------------- |
| `/loop-critic`       | `.agent/workflows/loop_critic.md`       | ENGINEER generates, ARCHITECT critiques, max 3 iterations |
| `/coordinator`       | `.agent/workflows/coordinator.md`       | Antigravity routes to FORENSICS / ARCHITECT / ENGINEER    |
| `/agent-as-tool`     | `.agent/workflows/agent_as_tool.md`     | Stateless single-use diagnostic or surgical edit          |
| `/multi-agent-audit` | `.agent/workflows/multi_agent_audit.md` | Red-team multi-agent cross-audit                          |

Source of truth: `.agent/workflows/` is canonical. Codex MUST NOT deviate from workflow steps
without Director authorization. All coding tasks MUST conclude with a **/loop-critic** or **/multi-agent-audit** cycle using internal subagents before final P5 escalation.

### Mandatory Workflow Self-Improvement (NON-NEGOTIABLE)

After EVERY workflow use, Codex MUST perform a post-use audit:

1. **Did any step produce an unexpected result?** Fix the instruction in the workflow file.
2. **Was any rule ambiguous?** Rewrite it to be unambiguous.
3. **Was a step missing?** Add it now.

**If no gap found:** `workflow([name]): no gaps identified -- workflow correct as written.`

**Commit format:** `workflow([name]): [what was fixed and why]`

No Director approval required for workflow-only self-improvement edits.
Workflow edit must be mirrored to BOTH `_agents/workflows/` and `.agent/workflows/`.

## ðŸ§  Karpathy Behavioral Protocols (LLM Coding Hygiene)

Derived from Andrej Karpathy's observations on LLM coding pitfalls.
These principles apply to all agents including Gemini CLI as Orchestrator.
Bias toward caution over speed. For trivial tasks, use judgment.

### Think Before Coding

- State assumptions explicitly. If uncertain, ASK -- do not silently pick an interpretation.
- If multiple interpretations exist, present them and let the Director choose.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, STOP. Name what is confusing. Ask.

### Simplicity First

- Minimum code that solves the problem. Nothing speculative.
- No features beyond what was asked. No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- If you write 200 lines and it could be 50, rewrite it.
- Test: Would a senior engineer say this is overcomplicated? If yes, simplify.

### Surgical Changes

- Touch only what you must. Clean up only your own mess.
- Do NOT "improve" adjacent code, comments, or formatting.
- **WHITESPACE MUTATION BANNED**: Never mutate whitespace, line endings, or indentation across files. This creates bloated diffs that obscure logic and break CI limits.
- **STRICT DIFF LIMIT**: Pull Request diffs MUST remain under 150,000 characters. If your formatting or logic pushes the diff over this limit, you must revert and isolate the logic changes.
- Do NOT refactor things that aren't broken. Match existing style.
- If you notice unrelated dead code, MENTION it -- do not delete it.
- Every changed line must trace directly to the Mission Brief.

### Goal-Driven Execution

- For multi-step tasks, state a brief plan with explicit verify steps:
  1. [Step] -> verify: [check]
  2. [Step] -> verify: [check]
  3. [Step] -> verify: [check]
- Define "done" before starting. Strong criteria let you loop independently.
- Weak criteria ("make it work") require constant clarification -- avoid them.

## Graphify Protocols (Universal Knowledge Layer)

- **Check First**: Before deep architectural exploration, always check for `graphify-out/graph.json` or `graphify-out/GRAPH_REPORT.md`.
- **Update**: Use `graphify update .` to refresh the repo knowledge graph after major structural changes.
- **Efficiency**: Use the graph to navigate codebase relationships with 71x fewer tokens than raw file reading.