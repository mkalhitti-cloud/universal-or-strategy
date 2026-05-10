# AGENTS.md - Sovereign Agent Protocol

Welcome, Agent. You are operating within the **V12 Universal OR Strategy** repository. This environment is optimized for autonomous multi-agent development under the **Sovereign Droid Protocol (SDP)**.

## 1. Agent Hierarchy (The Director's Gate)

- **ORCHESTRATOR (P1)**: Central Switchboard (Antigravity). Controls context and cross-agent routing.
- **ARCHITECT (P3)**: Strategic Design (**Claude Opus 4.7**). **PLAN-ONLY**. Authored plans reside in `docs/brain/implementation_plan.md`.
- **ENGINEER (P4)**: Implementation (Codex/Jules). Executes surgical edits to `src/`.
- **FORENSICS (P2/P5)**: Diagnosis (P2) and Adversarial Audit (P5).

## 2. Architectural Mandates (THE PLATINUM STANDARD)

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

> Derived from Andrej Karpathy's observations on LLM coding pitfalls.
> Every agent operating in this repo MUST apply these principles.

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
- **STRICT DIFF LIMIT**: Pull Request diffs MUST remain under 150,000 characters. If your formatting or logic pushes the diff over this limit, you must revert and isolate the logic changes.
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
**Exception:** Use `Read` when you need to edit a file — the agent harness requires a `Read` before `Edit`/`Write` will succeed. Use jCodemunch tools to *find and understand* code, then `Read` only the specific file you're about to modify.

**Start any session:**
1. `resolve_repo { "path": "." }` — confirm the project is indexed. If not: `index_folder { "path": "." }`
2. `suggest_queries` — when the repo is unfamiliar

**Finding code:**
- symbol by name → `search_symbols` (add `kind=`, `language=`, `file_pattern=`, `decorator=` to narrow)
- decorator-aware queries → `search_symbols(decorator="X")` to find symbols with a specific decorator (e.g. `@property`, `@route`); combine with set-difference to find symbols *lacking* a decorator (e.g. "which endpoints lack CSRF protection?")
- string, comment, config value → `search_text` (supports regex, `context_lines`)
- database columns (dbt/SQLMesh) → `search_columns`

**Reading code:**
- before opening any file → `get_file_outline` first
- one or more symbols → `get_symbol_source` (single ID → flat object; array → batch)
- symbol + its imports → `get_context_bundle`
- specific line range only → `get_file_content` (last resort)

**Repo structure:**
- `get_repo_outline` → dirs, languages, symbol counts
- `get_file_tree` → file layout, filter with `path_prefix`

**Relationships & impact:**
- what imports this file → `find_importers`
- where is this name used → `find_references`
- is this identifier used anywhere → `check_references`
- file dependency graph → `get_dependency_graph`
- what breaks if I change X → `get_blast_radius`
- what symbols actually changed since last commit → `get_changed_symbols`
- find unreachable/dead code → `find_dead_code`
- class hierarchy → `get_class_hierarchy`

## Session-Aware Routing

**Opening move for any task:**
1. `plan_turn { "repo": "...", "query": "your task description", "model": "<your-model-id>" }` — get confidence + recommended files; the `model` parameter narrows the exposed tool list to match your capabilities at zero extra requests.
2. Obey the confidence level:
   - `high` → go directly to recommended symbols, max 2 supplementary reads
   - `medium` → explore recommended files, max 5 supplementary reads
   - `low` → the feature likely doesn't exist. Report the gap to the user. Do NOT search further hoping to find it.

**Interpreting search results:**
- If `search_symbols` returns `negative_evidence` with `verdict: "no_implementation_found"`:
  - Do NOT re-search with different terms hoping to find it
  - Do NOT assume a related file (e.g. auth middleware) implements the missing feature (e.g. CSRF)
  - DO report: "No existing implementation found for X. This would need to be created."
  - DO check `related_existing` files — they show what's nearby, not what exists
- If `verdict: "low_confidence_matches"`: examine the matches critically before assuming they implement the feature

**After editing files:**
- If PostToolUse hooks are installed (Claude Code only), edited files are auto-reindexed
- Otherwise, call `register_edit` with edited file paths to invalidate caches and keep the index fresh
- For bulk edits (5+ files), always use `register_edit` with all paths to batch-invalidate

**Token efficiency:**
- If `_meta` contains `budget_warning`: stop exploring and work with what you have
- If `auto_compacted: true` appears: results were automatically compressed due to turn budget
- Use `get_session_context` to check what you've already read — avoid re-reading the same files

## Model-Driven Tool Tiering

Your jcodemunch-mcp server narrows the exposed tool list based on the model you are running as. To avoid wasting requests on primitives when a composite would do, always include `model="<your-model-id>"` in your opening `plan_turn` call.

Replace `<your-model-id>` with your active model:
- Claude Opus variants → `claude-opus-4-7` (or any `claude-opus-*`)
- Claude Sonnet variants → `claude-sonnet-4-6`
- Claude Haiku variants → `claude-haiku-4-5`
- GPT-4o / GPT-5 / o1 / Llama → use the model id as printed by your runner

The `model=` parameter rides on the existing `plan_turn` call — it does **not** add a separate tool invocation. If `plan_turn` is not appropriate for a non-code task, call `announce_model(model="...")` once instead.

## graphify

This project has a graphify knowledge graph at graphify-out/.

Rules:
- Before answering architecture or codebase questions, read graphify-out/GRAPH_REPORT.md for god nodes and community structure
- If graphify-out/wiki/index.md exists, navigate it instead of reading raw files
- After modifying code files in this session, run `graphify update .` to keep the graph current (AST-only, no API cost)
