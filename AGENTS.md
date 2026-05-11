# AGENTS.md - Sovereign Agent Protocol

Welcome, Agent. You are operating within the **V12 Universal OR Strategy** repository. This environment is optimized for autonomous multi-agent development under the **Sovereign Droid Protocol (SDP)**.

## 1. Agent Hierarchy (The Director's Gate)

- **ORCHESTRATOR (P1)**: Central Switchboard (Antigravity / Gemini CLI). Controls context and cross-agent routing.
- **ARCHITECT (P3)**: Strategic Design (**Claude Opus 4.7**). **PLAN-ONLY**. Authored plans reside in `docs/brain/implementation_plan.md`.
- **ADJUDICATOR (Arena AI)**: **P4 Vetting Gate**. Adversarial consensus and **PR Audit** required BEFORE surgery.
- **ENGINEER (P4/P5)**: Surgical Implementation. Executes approved plans. Target selection is mandatory:
    - **Bob CLI** (`v12-engineer`): Specialist for SIMA extraction, god-function splitting, and high-performance repairs.
    - **Codex CLI** (`codex-rescue`): Specialist for logic hardening, lock-free kernel updates, and forensic repairs.
    - **Gemini CLI** (`yolo`): **Utility Specialist & Research Hub**. Handles non-`src/` tasks (docs, infra, configs), model-agnostic operations, **Official Web Research**, and **Video Synthesis** (YouTube/Visual context).
- **FORENSICS (P2/P6)**: Diagnosis (P2) and Adversarial Audit (P6).

## 2. Architectural Mandates (THE PLATINUM STANDARD)

- **Correctness by Construction ("Make illegal states unrepresentable")**: Structure types, enums, and data models so that it is mathematically impossible for the compiler to allow an invalid state. Do not rely on runtime if/else guards for weird edge casesâ€”design the architecture so the edge case literally cannot exist.
- **Lock-Free Actor Pattern**: Legacy `lock(stateLock)` blocks are **STRICTLY BANNED**. All state mutations must use the FSM/Actor `Enqueue` model or atomic primitives.
- **ASCII-Only Compliance**: NEVER use Unicode, emoji, or curly quotes in C# string literals.
- **Hard-Link Integrity**: Every `src/` modification MUST be followed by `powershell -File .\deploy-sync.ps1` to re-synchronize NinjaTrader hard links.

## 3. Standard Commands

- **Build & Sync** (Build Pillar): `powershell -File .\scripts\build_readiness.ps1`
- **Lint Audit** (Style Pillar): `powershell -File .\scripts\lint.ps1`
- **Stress Test** (Testing Pillar): `powershell -File .\scripts\test_stress.ps1`
- **Sovereign Audit**: `droid /review` (Focus on P0-P3 severity findings).
- **Readiness Check**: `droid /readiness-report` (Maintain Level 2+).
- **Forensic Scan**: `grep -r "lock(" src/` (Zero-match requirement).

## 4. Communication & Context

- **Active Task**: Always check `docs/brain/task.md` before initiating work.
- **Handoffs**: Use the `docs/brain/nexus_a2a.json` via the **Nexus Bridge** for inter-agent state synchronization.

## 5. Karpathy Behavioral Protocols (LLM Coding Hygiene)

Derived from Andrej Karpathy's observations on LLM coding pitfalls.
These principles apply to all agents including Gemini CLI as Orchestrator.
Bias toward caution over speed. For trivial tasks, use judgment.

### Think Before Coding

- State assumptions explicitly. If uncertain, ASK -- do not silently pick an interpretation.
- If multiple interpretations exist, surface them to the Director before proceeding.
- If a simpler approach exists, say so. Push back when warranted.

### Simplicity First

- Minimum code that solves the problem. Nothing speculative.
- No features beyond what was asked. No abstractions for single-use code.
- If 200 lines could be 50, rewrite it before submission.

### Surgical Changes

- Touch only what you must. Clean up only your own mess.
- Do NOT "improve" adjacent code, comments, or formatting.
- **WHITESPACE MUTATION BANNED**: Never mutate whitespace, line endings, or indentation across files. This creates bloated diffs that obscure logic and break CI limits.
- **STRICT DIFF LIMIT**: Pull Request diffs MUST remain under 150,000 characters.
- **DIFF PRE-CHECK**: Before pushing, run `powershell -File .\deploy-sync.ps1`. If the **DIFF GUARD** fails, you must isolate the logic changes and revert whitespace/artifact bloat.
- If unrelated dead code is noticed, REPORT it -- do not act on it.
- Every changed line must trace directly to the Mission Brief.

### Goal-Driven Execution

- State verify criteria before each implementation stage:
  1. [Step] -> verify: [check]
  2. [Step] -> verify: [check]
- Strong success criteria let you loop independently. "Make it work" is not a criterion.

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

## 7. Phase 6 Recursive Protocol (V15.4)

This protocol governs the **SIMA Subgraph Extraction** and all complex refactoring missions.

### Stage 0: Forensic Intake (Orchestrator)
- **Tool**: `jcodemunch-mcp` + `graphify`
- **Goal**: Generate "Platinum Standard" prompts for the ARCHITECT.
- **Output**: Forensic report in `docs/brain/forensics_report.md`.

### Stage 1: Vision/Spec (Architect)
- **Agent**: Traycer (Frontier Mode)
- **Goal**: Dialogue with Director to generate `mini-spec.md`.
- **Constraint**: Must verify logic against V12 DNA.

### Stage 2: Arch Planning (Architect)
- **Agent**: Traycer (Frontier Mode)
- **Goal**: Generate `implementation_plan.md` + Mermaid diagrams.
- **Audit**: Triple-Agent UltraThink audit required.

### Stage 3: DNA & PR Audit (Adjudicator)
- **Agent**: Arena AI (Red Team)
- **Goal**: Verify plan and PR health against V12 constraints (No locks, Atomic, ASCII-only).
- **Gate**: PASS/FAIL. Fail triggers Stage 2 rework.

### Stage 4: Recursive Execution (Engineer Selection)
- **Action**: Hand off to the selected Engineer via Traycer Handoff Menu.
- **Targets**: 
  - **Bob CLI** for extraction/splitting (P5 Surgical).
  - **Codex CLI** for logic hardening (P5 Logic).
  - **Gemini CLI** for **Utility/Non-src** tasks (P5 Utility). Always use Gemini for model-agnostic tasks to conserve specialized tokens.
- **Safety**: Mandatory checkpointing enabled.

### Stage 5: Verification/Review (Forensics)
- **Agent**: Traycer (Re-verify cycle) + Orchestrator
- **Goal**: Compare implementation against `implementation_plan.md`.
- **Loop**: Automated "Fix-all" loop if logic drifts.

### Stage 6: Sign-off (Director)
- **Action**: `powershell -File .\deploy-sync.ps1`
- **Final Test**: F5 in NinjaTrader + BUILD_TAG verification.

## 8. IBM Bob Shell Integration

- **Binary**: `bob` (via alias or path)
- **Mode**: `v12-engineer` (custom mode defined in `.bob/custom_modes.yaml`)
- **Rules**: Enforced via `.bob/rules-v12-engineer/`
- **Checkpointing**: Always enabled via `.bob/settings.json`. Restore via `/restore`.

## graphify

This project has a graphify knowledge graph at graphify-out/.

Rules:
- Before answering architecture or codebase questions, read graphify-out/GRAPH_REPORT.md for god nodes and community structure
- If graphify-out/wiki/index.md exists, navigate it instead of reading raw files
- After modifying code files in this session, run `graphify update .` to keep the graph current (AST-only, no API cost)