# CLAUDE.md - BMad Project Standards & Safety Guide

## Session Protocol (NON-NEGOTIABLE DEFAULT)

**You are the ARCHITECT (P3). Your permanent default mode is PLAN-ONLY.**

- NEVER write to `src/` files unless the Director explicitly says "you have execution permission" for this specific session.
- ALWAYS produce plans ending with a Director's Handoff Block for the ENGINEER (Codex/Jules) to execute.
- When starting a session, wait for a stated task. Do NOT explore the codebase until the user confirms the goal.
- Exception: If the Director explicitly grants execution permission (e.g. "you have full permission today" or "implement this"), you may apply the approved plan using file-edit tools.

## Project Overview

**Universal OR Strategy (V12)**: A high-integrity institutional fleet trading strategy for NinjaTrader 8.

## ðŸ›¡ï¸ Zero-Trust Protocols (MANDATORY)

1. **IPC Security**: All listeners must bind to Loopback (`127.0.0.1`). Malformed input must be rejected with `V12 IPC REJECT` logs.
2. **Input Validation**: Never trust incoming network payloads. Use strict UTF-8 decoding and bounded command lengths.
3. **Fleet Privacy**: Obscure sensitive account names using BMad aliases (`F01`, `F02`, etc.) in all external-facing responses.

## ðŸ¦ Logic Integrity (FLEET SAFETY)

1. **No Internal Locks**: Legacy `lock(stateLock)` is BANNED for internal execution. Thread-safety should be managed via either the Actor model or direct atomic writes, depending on the mission requirements.
2. **Build 981 Protocol**: Direct writes to `stopOrders` are MANDATORY during bracket submission. Enqueue is BANNED for this operation to eliminate tracking latency during shutdown races.
3. **Ghost-Order Prevention**: Use Signed Delta Rollbacks for expected position cleanup; never use blanket zeroing.
4. **REAPER Bounds**: Repairs must be capped by both ATR-volatility and hard tick fences.
5. **Symmetry Gating**: Follower brackets must wait for the master "Anchor" price before submission.

## ðŸ·ï¸ Naming Conventions

- **Build Tags**: Must be incremented in `V12_002.Properties.cs` for every production delivery.
- **Prefixes**: All files and primary classes use `V12_001` (Panel) or `V12_002` (Strategy).

## CRITICAL: ASCII-Only in All C# String Literals

- NEVER use emoji, curly quotes, em-dashes, Unicode arrows, or box-drawing in Print() or any string literal.
- Non-ASCII inside C# strings breaks the NinjaTrader compiler with 300+ cascading errors (Historical Precedent).
- Allowed substitutions: (!) not emoji, -- not em-dash, -> not arrow, straight " not curly " "
- See .agent/standards_manifesto.md Section 7 for the full rule table and emergency fix sequence.

## MOVE-SYNC / Follower Order Replace Pattern (Permanent DNA)

- **FSM Required**: Any follower order cancel+resubmit MUST use the two-phase Replace FSM (`_followerReplaceSpecs` dict).
- **Never cancel+submit directly**: Raw `Cancel()` followed immediately by `Submit()` creates ghost orders. BANNED.
- **FSM states**: `PendingCancel` -> wait for `OnAccountOrderUpdate` confirm -> `Submitting` -> `SubmitFollowerReplacement`.
- **ATR tick absorption**: While in `PendingCancel`, sizing changes update `PendingReplacementSpec` only. One cancel, one resubmit.
- **Fill-during-gap guard**: Check if master filled before submitting replacement. If yes, route to REAPER repair.
- **ChangeOrder banned for fleet accounts**: `Account.Change` silently no-ops on Apex/Tradovate.

## Live Bug Triage Protocol (A2A Ready)

- **Codex first**: For any live trading anomaly, run `/live-bug-triage` workflow.
- **Codex = diagnosis, Claude = architecture, Gemini/Jules = engineer**.
- **A2A Delegation**: Use Agent2Agent protocol to delegate surgical repair missions to local CLI agents.
- **Handoff Mandate**: All handoff prompts, implementation plans, and multi-agent directives MUST be provided as clear Markdown code blocks directly in the chat output. Provisioning all prompts (e.g., Forensics P2, Gemini P5, Engineer P4) in a single unified response takes precedence over sequential `Set-Clipboard` delivery to optimize token usage and streamline the Director's workflow. `Set-Clipboard` may still be used as a secondary convenience, but NEVER as a blocking sequential gate.
- **Plan audit required**: Paste implementation plans to Antigravity for audit before approving.
- **NO SIMULATION**: Orchestrators are STRICTLY BANNED from simulating sub-agent (Codex/Jules/Gemini) consensus. Every P5 sign-off must verify an authentic local log or file write.
- **P5 Redundancy Mandate**: Task-splitting during a P5 audit is **STRICTLY FORBIDDEN**. Every member of the Red Team (Codex, Gemini CLI, Jules) must independently audit the **entire** implementation plan. Consensus is only valid if every agent validates every target individually.
- **IDENTITY INTEGRITY**: Claude is BANNED from pretending to be Codex, Jules, or Gemini CLI. If an agent is unreachable, Claude MUST report the failure and wait for Director intervention.
- **GITHUB-LINK PROTOCOL**: All Arena AI prompts MUST reference GitHub branch/file URLs. DO NOT put raw code in prompts for architectural audits.
- **UltraThink Mandate**: ALWAYS perform high-density logical simulations and side-effect audits before proposing code changes.
- **UltraPlan Mandate**: ALWAYS use `/ultraplan` for high-complexity architectural shifts to leverage the browser-based review surface.
- **Aesthetic Excellence**: UI changes MUST be premium, dynamic, and have a "WOW" factor.

## Engineer Self-Audit (P4) Mandatory (All Agents)

Every code change must be validated by the Engineer (P4) before architectural sign-off:

1.  **Internal Audit (/loop-critic):** Invoke the **`architect`** subagent to critique the implementation against the technical spec.
2.  **Forensics Check:** Use the **`forensics`** subagent to confirm zero `lock(stateLock)` usage and ASCII enforcement.
3.  **ASCII Scan:** Run a character-level scan (or use `check_ascii.py`) to ensure no curly quotes or Unicode arrows.
4.  **Dry Run:** Final sanity check of the logic flow and state machine transitions.

- **V12 Master Architect**: Planning and structural design (PLAN-ONLY).
- **Frontend Design (V12.15)**: Distinctive UI/UX development using Tactical Sovereign aesthetics.
- **Cloud Setup**: GCP deployment automation.

## Post-Edit Deployment Protocol (MANDATORY -- NEVER SKIP)

**Root Cause**: File-edit tools (`replace_file_content`, `Edit`) create new inodes, silently breaking the hard links that `deploy-sync.ps1` established between `src/` and the NinjaTrader Strategies folder. The old DLL is then compiled from the stale linked file, not the new source.

**Required sequence after ANY `src/` file edit:**

1. Run `powershell -File .\deploy-sync.ps1` -- re-establishes all hard links. ASCII Gate must PASS.
2. **Then** instruct the Director: "Press F5 in NinjaTrader to compile."
3. Verify the banner shows the new BUILD_TAG.

**The Engineer MUST include this sequence in every handoff block.** Skipping this step is a protocol violation.

## Agentic Workflows (Self-Improving)

All workflows are stored in `_agents/workflows/` and `.agent/workflows/` (mirrored). Claude reads these via `view_file` before executing any workflow step.

| Slash Command        | Workflow File          | Claude Role                                           |
| -------------------- | ---------------------- | ----------------------------------------------------- |
| `/architect_intake`  | `architect_intake.md`  | PRIMARY â€” writes implementation_plan.md               |
| `/loop_critic`       | `loop_critic.md`       | PRIMARY â€” issues APPROVED / REVISION REQUIRED verdict |
| `/multi_agent_audit` | `multi_agent_audit.md` | PRIMARY â€” structural soundness auditor                |
| `/coordinator`       | `coordinator.md`       | Participant â€” structural design subtask               |
| `/agent_as_tool`     | `agent_as_tool.md`     | Participant â€” one-shot design review only             |
| `/battle`            | `battle.md`            | Observer â€” reads results to inform next plan          |

### Mandatory Workflow Self-Improvement (NON-NEGOTIABLE)

After EVERY workflow use, Claude MUST perform a post-use audit:

1. **Did any step produce an unexpected result?** Fix the workflow instruction.
2. **Was any rule ambiguous?** Rewrite it.
3. **Was a step missing?** Add it.

**If no gap found:** `workflow([name]): no gaps identified -- workflow correct as written.`

**Commit format:** `workflow([name]): [what was fixed and why]`

No Director approval required for workflow-only self-improvement edits.

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
- Do NOT refactor things that aren't broken. Match existing style.
- If you notice unrelated dead code, MENTION it -- do not delete it.
- Every changed line must trace directly to the user's request.

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


## Code Exploration Policy

Always use jCodemunch-MCP tools for code navigation. Never fall back to Read, Grep, Glob, or Bash for code exploration.
**Exception:** Use `Read` when you need to edit a file â€” the agent harness requires a `Read` before `Edit`/`Write` will succeed. Use jCodemunch tools to *find and understand* code, then `Read` only the specific file you're about to modify.

**Start any session:**
1. `resolve_repo { "path": "." }` â€” confirm the project is indexed. If not: `index_folder { "path": "." }`
2. `suggest_queries` â€” when the repo is unfamiliar

**Finding code:**
- symbol by name â†’ `search_symbols` (add `kind=`, `language=`, `file_pattern=`, `decorator=` to narrow)
- decorator-aware queries â†’ `search_symbols(decorator="X")` to find symbols with a specific decorator (e.g. `@property`, `@route`); combine with set-difference to find symbols *lacking* a decorator (e.g. "which endpoints lack CSRF protection?")
- string, comment, config value â†’ `search_text` (supports regex, `context_lines`)
- database columns (dbt/SQLMesh) â†’ `search_columns`

**Reading code:**
- before opening any file â†’ `get_file_outline` first
- one or more symbols â†’ `get_symbol_source` (single ID â†’ flat object; array â†’ batch)
- symbol + its imports â†’ `get_context_bundle`
- specific line range only â†’ `get_file_content` (last resort)

**Repo structure:**
- `get_repo_outline` â†’ dirs, languages, symbol counts
- `get_file_tree` â†’ file layout, filter with `path_prefix`

**Relationships & impact:**
- what imports this file â†’ `find_importers`
- where is this name used â†’ `find_references`
- is this identifier used anywhere â†’ `check_references`
- file dependency graph â†’ `get_dependency_graph`
- what breaks if I change X â†’ `get_blast_radius`
- what symbols actually changed since last commit â†’ `get_changed_symbols`
- find unreachable/dead code â†’ `find_dead_code`
- class hierarchy â†’ `get_class_hierarchy`

## Session-Aware Routing

**Opening move for any task:**
1. `plan_turn { "repo": "...", "query": "your task description", "model": "<your-model-id>" }` â€” get confidence + recommended files; the `model` parameter narrows the exposed tool list to match your capabilities at zero extra requests.
2. Obey the confidence level:
   - `high` â†’ go directly to recommended symbols, max 2 supplementary reads
   - `medium` â†’ explore recommended files, max 5 supplementary reads
   - `low` â†’ the feature likely doesn't exist. Report the gap to the user. Do NOT search further hoping to find it.

**Interpreting search results:**
- If `search_symbols` returns `negative_evidence` with `verdict: "no_implementation_found"`:
  - Do NOT re-search with different terms hoping to find it
  - Do NOT assume a related file (e.g. auth middleware) implements the missing feature (e.g. CSRF)
  - DO report: "No existing implementation found for X. This would need to be created."
  - DO check `related_existing` files â€” they show what's nearby, not what exists
- If `verdict: "low_confidence_matches"`: examine the matches critically before assuming they implement the feature

**After editing files:**
- If PostToolUse hooks are installed (Claude Code only), edited files are auto-reindexed
- Otherwise, call `register_edit` with edited file paths to invalidate caches and keep the index fresh
- For bulk edits (5+ files), always use `register_edit` with all paths to batch-invalidate

**Token efficiency:**
- If `_meta` contains `budget_warning`: stop exploring and work with what you have
- If `auto_compacted: true` appears: results were automatically compressed due to turn budget
- Use `get_session_context` to check what you've already read â€” avoid re-reading the same files

## Model-Driven Tool Tiering

Your jcodemunch-mcp server narrows the exposed tool list based on the model you are running as. To avoid wasting requests on primitives when a composite would do, always include `model="<your-model-id>"` in your opening `plan_turn` call.

Replace `<your-model-id>` with your active model:
- Claude Opus variants â†’ `claude-opus-4-7` (or any `claude-opus-*`)
- Claude Sonnet variants â†’ `claude-sonnet-4-6`
- Claude Haiku variants â†’ `claude-haiku-4-5`
- GPT-4o / GPT-5 / o1 / Llama â†’ use the model id as printed by your runner

The `model=` parameter rides on the existing `plan_turn` call â€” it does **not** add a separate tool invocation. If `plan_turn` is not appropriate for a non-code task, call `announce_model(model="...")` once instead.

## graphify

This project has a graphify knowledge graph at graphify-out/.

Rules:
- Before answering architecture or codebase questions, read graphify-out/GRAPH_REPORT.md for god nodes and community structure
- If graphify-out/wiki/index.md exists, navigate it instead of reading raw files
- After modifying code files in this session, run `graphify update .` to keep the graph current (AST-only, no API cost)