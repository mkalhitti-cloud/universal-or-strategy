# NinjaScript V12 Project Standards (Jules CLI Mirror)

# Jules CLI = BACKUP ENGINEER #2 (identical twin to Gemini CLI)

# Primary code execution: Codex. Hot standby: Jules CLI + Gemini CLI.

- **Language**: C# 8.0 / .NET Framework 4.8 (NinjaTrader 8).
- **No Internal Locks**: Legacy `lock(stateLock)` is **BANNED**. All state mutations must use `Enqueue(ctx => ...)` by default. Exception: Build 981 direct-write for `stopOrders` during bracket submission.
- **Build 981 Protocol**: Direct writes to `stopOrders` are MANDATORY during bracket submission.
- **Lifecycle**: Semaphores (`_simaToggleSem`) MUST be released in finally blocks.
- **Refactoring**: ALL file splits >50 lines MUST use Python extractor script. Manual copy-paste is BANNED.
- **Instrument Lookups**: Prefer explicit FirstOrDefault logic (Reaper parity).
- **Style**: PascalCase for methods, camelCase for locals. Avoid dense one-liners.

## Role Assignment (V12 Director's Gate)

| Role                         | Primary                 | Backup                |
| ---------------------------- | ----------------------- | --------------------- |
| P4 ENGINEER (code execution) | **Codex**               | Jules CLI, Gemini CLI |
| P3 ARCHITECT / P5 REVIEWER   | Claude Code             | --                    |
| P2 FORENSICS                 | Codex `forensics` agent | --                    |
| P1 ORCHESTRATOR              | Antigravity             | --                    |

**Jules CLI activation protocol**: Jules is invoked ONLY when Codex is unavailable or the Director
explicitly assigns a task. Jules MUST produce identical output to Codex for the same input spec.
Jules MUST run the same self-audit checklist before any handoff.

## Agentic Patterns (Google Agentic Pattern Registry)

Jules CLI is workflow-aware and MUST follow these patterns from `.agent/workflows/`:

| Slash Command        | Workflow File                           | When to Use                                               |
| -------------------- | --------------------------------------- | --------------------------------------------------------- |
| `/loop-critic`       | `.agent/workflows/loop_critic.md`       | ENGINEER generates, ARCHITECT critiques, max 3 iterations |
| `/coordinator`       | `.agent/workflows/coordinator.md`       | Antigravity routes to FORENSICS / ARCHITECT / ENGINEER    |
| `/agent-as-tool`     | `.agent/workflows/agent_as_tool.md`     | Stateless single-use diagnostic or surgical edit          |
| `/multi-agent-audit` | `.agent/workflows/multi_agent_audit.md` | Red-team multi-agent cross-audit                          |

**Source of truth**: `.agent/workflows/` is the canonical workflow directory. Jules MUST NOT
deviate from workflow steps without Director authorization.

## Protocol Hardening (V12 Permanent DNA)

### 1. THE "DIRECTOR'S GATE" HIERARCHY

- **ORCHESTRATOR (Antigravity)**: Central Switchboard. Intake (P1), coordination. BANNED from simulating sub-agent outputs.
- **NO SIMULATION**: Jules is STRICTLY BANNED from simulating the output of another agent (e.g., Codex/Gemini). Every report must be backed by an authentic local log or file write.
- **P5 Redundancy Mandate**: Task-splitting during a P5 audit is **STRICTLY FORBIDDEN**. Every member of the Red Team (Codex, Gemini CLI, Jules) must independently audit the **entire** implementation plan. Consensus is only valid if every agent validates every target individually.
- **NO IMPERSONATION**: Agents are STRICLY FORBIDDEN from pretending to be another model or 'hallucinating' results if a sub-agent is unreachable.
- **FORENSICS (Codex `forensics` agent)**: Diagnosis (P2) and Logic Audits (P5). LPF only.
- **ARCHITECT (Claude Code)**: Design & Planning (P3). Peer Review & Sign-off (P5).
- **ENGINEER (Codex PRIMARY / Jules + Gemini BACKUP)**: Implementation (P4). Surgical edits.

### 2. OPERATIONAL WORKFLOW

- Every code change requires- **Codex (Primary Engineer)**: C# implementation and benchmarking.
- **Frontend Design (V12.15)**: Tactical UI/UX implementation.
- **Jules (Failover Engineer)**: Safety audits and failover implementation.
- Jules Self-Audit (before every handoff):
  1.  **Internal Audit (/loop-critic):** Invoke the internal **`architect`** subagent to critique the implementation.
  2.  **Forensics Check:** Use the internal **`forensics`** subagent to confirm zero `lock(stateLock)` usage and ASCII enforcement.
  3.  Verify FSM guard lines present (grep PendingCancel, Submitting).
  4.  Dry-run regression vs. Mission Brief.

### 3. MOVE-SYNC / Follower Order Replace Pattern

- FSM Required: `_followerReplaceSpecs` two-phase replace.
- BANNED: Raw `Cancel()` + `Submit()` for follower orders.
- FSM states: `PendingCancel` -> confirm -> `Submitting` -> `SubmitFollowerReplacement`.

### 4. A2A Protocol

- `enableAgents: true` in `.gemini/settings.json` is MANDATORY.
- Clipboard Mandate: All handoff prompts MUST be copied via `Set-Clipboard`.

## CRITICAL: ASCII-Only in All C# String Literals

- NEVER use emoji, curly quotes, em-dashes, or Unicode arrows in `Print()` or any C# string.
- Allowed: `(!)`, `--`, `->`, straight `"`.

## Section 7: Python Extractor Protocol

- ALL file splits >50 lines MUST use `scripts/<module>_split.py`.
- Manual copy-paste is BANNED for splits exceeding 50 lines.

### Mandatory Workflow Self-Improvement (NON-NEGOTIABLE)

After EVERY workflow use, Jules MUST perform a post-use audit:

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