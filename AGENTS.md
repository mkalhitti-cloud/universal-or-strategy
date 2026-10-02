# AGENTS.md - Sovereign Agent Protocol

Welcome, Agent. You are operating within the **PTT Trade Copier (PropTraderTools)** repository. This environment is optimized for autonomous multi-agent development under the **Sovereign Droid Protocol (SDP)**.

## 1. Agent Hierarchy (The Director's Gate)

- **ORCHESTRATOR (P1)**: Central Switchboard (Antigravity / Gemini CLI). Controls context and cross-agent routing.
- **ARCHITECT + ENGINEER (P3/P4/P5)   src/ tasks**: **Bob CLI** (`v12-engineer`) is the unified Architect-Engineer for all `src/` work. Bob handles design (planning), extraction, refactoring, and surgical implementation in a single Orchestrator session. No separate P3 handoff to Claude is required for `src/` tickets.
  - **Bob CLI** (`v12-engineer`): Primary. Handles design-only gates, God-function splitting, and full implementation.
  - **Codex CLI** (`codex-rescue`): Secondary. Specialist for surgical logic hardening and lock-free kernel updates when Bob delegates.
- **ARCHITECT (P3)   escalation only**: **Claude Opus 4.7** is reserved for (a) non-src architectural review, (b) $battlezip compound intelligence sessions, and (c) cross-subgraph design decisions that span >3 files outside Bob's current context. Claude remains PLAN-ONLY when invoked.
- **ADJUDICATOR (Arena AI)**: **P4 Vetting Gate**. Adversarial consensus and **PR Audit** required BEFORE surgery.
- **ENGINEER (P4/P5)   non-src tasks**: Target selection follows strict routing logic:
    - **Jules AI**: Primary non-src engineer for GitHub-based workflows.
    - **Gemini CLI** (`yolo`): Secondary non-src local engineer for tasks requiring local file access or visual context.
- **FORENSICS (P2/P6)**: Diagnosis (P2) and Adversarial Audit (P6).

## 2. Architectural Mandates (THE PLATINUM STANDARD)

- **Correctness by Construction ("Make illegal states unrepresentable")**: Structure types, enums, and data models so that it is mathematically impossible for the compiler to allow an invalid state. Do not rely on runtime if/else guards for weird edge cases   design the architecture so the edge case literally cannot exist.
- **Lock-Free Actor Pattern**: Legacy `lock(stateLock)` blocks are **STRICTLY BANNED**. All state mutations must use the FSM/Actor `Enqueue` model or atomic primitives.
- **ASCII-Only Compliance**: NEVER use Unicode, emoji, or curly quotes in C# string literals.
- **Hard-Link Integrity**: Every `src/` modification MUST be followed by `powershell -File .\scripts\ptt-sync.ps1` to re-synchronize NinjaTrader hard links.

- **Ph4b Deletion Audit (MANDATORY)**: Before VERIFY_PASS, engineer MUST run:
  `git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"`
  Every deleted symbol must have an authorising DW-card in the ticket. Zero tolerance.

- **Single-Hunk Ban**: `(git show HEAD -- CopyEngine.cs | Select-String "^@@").Count -eq 1` means
  a full-file replace was committed. This is ALWAYS a VERIFY_FAIL. Requires Director re-review.

- **Guardian Symbol Script**: After every .cs edit involving CopyEngine.cs, run:
  `powershell -File .\scripts\verify_critical_symbols.ps1`
  PASS required before VERIFY_PASS may be issued.

- **Commit Size Gate**: >300 changed lines on CopyEngine.cs requires Director explicit approval
  before Ph4b may issue VERIFY_PASS.

## 3. Standard Commands

- **PTT Sync** (Hard-Link Pillar): `powershell -File .\scripts\ptt-sync.ps1`
- **Stress Test** (Testing Pillar): `powershell -File .\scripts\test_stress.ps1`
- **Sovereign Audit**: `droid /review` (Focus on P0-P3 severity findings).
- **Readiness Check**: `droid /readiness-report` (Maintain Level 2+).
- **Forensic Scan**: `grep -r "lock(" src/` (Zero-match requirement).
- **Jane Street KB Query**: `& "%USERPROFILE%\AppData\Local\Programs\Python\Python312\python.exe" scripts/query_kb.py "<term>"` (Retrieves HFT and high-performance system guidelines from the Firestore knowledge base).

## 4. Communication & Context

- **Active Task**: Always check `docs/brain/task.md` before initiating work.
- **Handoffs**: Use the `docs/brain/nexus_a2a.json` via the **Nexus Bridge** for inter-agent state synchronization.
- **Expert Knowledge Base (RAG)**: Before starting complex design, refactoring, or performance engineering tasks, query the Jane Street Knowledge Base using `scripts/query_kb.py` to retrieve verified microsecond-latency patterns and testing standards.

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
- **STRICT DIFF LIMIT**: Pull Request diffs MUST target less than 10,000 characters of source code changes (in `src/`). Split larger epics into smaller, focused PRs.
- **DIFF PRE-CHECK**: Before pushing, run `powershell -File .\deploy-sync.ps1`. If the **DIFF GUARD** fails, you must isolate the logic changes and revert whitespace/artifact bloat.
- If unrelated dead code is noticed, REPORT it -- do not act on it.
- Every changed line must trace directly to the Mission Brief.

### Goal-Driven Execution

- State verify criteria before each implementation stage:
  1. [Step] -> verify: [check]
  2. [Step] -> verify: [check]
- Strong success criteria let you loop independently. "Make it work" is not a criterion.

## 6. Autonomous Skill Creation & Self-Improvement (MANDATORY PILLAR)

**All agents MUST perform a post-use audit after every skill or tool use:**
1. Check if any instruction was ambiguous or produced an unexpected result.
2. Update the corresponding `SKILL.md` or persistent rule file if a gap or quirk is found.
3. State `skill(name): no gaps identified` if no gap is found.
4. Skipping the post-use audit is a protocol violation.

## Graphify Protocols (Universal Knowledge Layer)

- **Check First**: Before deep architectural exploration, always check for `graphify-out/graph.json` or `graphify-out/GRAPH_REPORT.md`.
- **Update**: Use `graphify update .` to refresh the repo knowledge graph after major structural changes.
- **Efficiency**: Use the graph to navigate codebase relationships with 71x fewer tokens than raw file reading.

## Code Exploration Policy

Always use jCodemunch-MCP tools for code navigation. Never fall back to Read, Grep, Glob, or Bash for code exploration.
**Exception:** Use `Read` when you need to edit a file     the agent harness requires a `Read` before `Edit`/`Write` will succeed. Use jCodemunch tools to *find and understand* code, then `Read` only the specific file you're about to modify.

**Start any session:**
1. `resolve_repo { "path": "." }`     confirm the project is indexed. If not: `index_folder { "path": "." }`
2. `suggest_queries`     when the repo is unfamiliar

**Finding code:**
- symbol by name     `search_symbols` (add `kind=`, `language=`, `file_pattern=`, `decorator=` to narrow)
- decorator-aware queries     `search_symbols(decorator="X")` to find symbols with a specific decorator (e.g. `@property`, `@route`); combine with set-difference to find symbols *lacking* a decorator (e.g. "which endpoints lack CSRF protection?")
- string, comment, config value     `search_text` (supports regex, `context_lines`)
- database columns (dbt/SQLMesh)     `search_columns`

**Reading code:**
- before opening any file     `get_file_outline` first
- one or more symbols     `get_symbol_source` (single ID     flat object; array     batch)
- symbol + its imports     `get_context_bundle`
- specific line range only     `get_file_content` (last resort)

**Repo structure:**
- `get_repo_outline`     dirs, languages, symbol counts
- `get_file_tree`     file layout, filter with `path_prefix`

**Relationships & impact:**
- what imports this file     `find_importers`
- where is this name used     `find_references`
- is this identifier used anywhere     `check_references`
- file dependency graph     `get_dependency_graph`
- what breaks if I change X     `get_blast_radius`
- what symbols actually changed since last commit     `get_changed_symbols`
- find unreachable/dead code     `find_dead_code`
- class hierarchy     `get_class_hierarchy`

## Session-Aware Routing

**Opening move for any task:**
1. `plan_turn { "repo": "...", "query": "your task description", "model": "<your-model-id>" }`     get confidence + recommended files; the `model` parameter narrows the exposed tool list to match your capabilities at zero extra requests.
2. Obey the confidence level:
   - `high`     go directly to recommended symbols, max 2 supplementary reads
   - `medium`     explore recommended files, max 5 supplementary reads
   - `low`     the feature likely doesn't exist. Report the gap to the user. Do NOT search further hoping to find it.

**Interpreting search results:**
- If `search_symbols` returns `negative_evidence` with `verdict: "no_implementation_found"`:
  - Do NOT re-search with different terms hoping to find it
  - Do NOT assume a related file (e.g. auth middleware) implements the missing feature (e.g. CSRF)
  - DO report: "No existing implementation found for X. This would need to be created."
  - DO check `related_existing` files     they show what's nearby, not what exists
- If `verdict: "low_confidence_matches"`: examine the matches critically before assuming they implement the feature

**After editing files:**
- If PostToolUse hooks are installed (Claude Code only), edited files are auto-reindexed
- Otherwise, call `register_edit` with edited file paths to invalidate caches and keep the index fresh
- For bulk edits (5+ files), always use `register_edit` with all paths to batch-invalidate

**Token efficiency:**
- If `_meta` contains `budget_warning`: stop exploring and work with what you have
- If `auto_compacted: true` appears: results were automatically compressed due to turn budget
- Use `get_session_context` to check what you've already read     avoid re-reading the same files

## Model-Driven Tool Tiering

Your jcodemunch-mcp server narrows the exposed tool list based on the model you are running as. To avoid wasting requests on primitives when a composite would do, always include `model="<your-model-id>"` in your opening `plan_turn` call.

Replace `<your-model-id>` with your active model:
- Claude Opus variants     `claude-opus-4-7` (or any `claude-opus-*`)
- Claude Sonnet variants     `claude-sonnet-4-6`
- Claude Haiku variants     `claude-haiku-4-5`
- GPT-4o / GPT-5 / o1 / Llama     use the model id as printed by your runner

The `model=` parameter rides on the existing `plan_turn` call     it does **not** add a separate tool invocation. If `plan_turn` is not appropriate for a non-code task, call `announce_model(model="...")` once instead.

## 7. Phase 6 Recursive Protocol (V15.4)

This protocol governs the **SIMA Subgraph Extraction** and all complex refactoring missions.

### Stage 0: Forensic Intake (Orchestrator)
- **Tool**: `jcodemunch-mcp` + `graphify`
- **Goal**: Generate "Platinum Standard" prompts for the ARCHITECT.
- **Output**: Forensic report in `docs/brain/forensics_report.md`.

### Stage 1: Vision/Spec (Architect)
- **Agent**: Bob CLI (`v12-engineer`)
- **Goal**: Dialogue with Director to generate `mini-spec.md`.
- **Constraint**: Must verify logic against V12 DNA.

### Stage 2: Arch Planning (Architect)
- **Agent**: Bob CLI (`v12-engineer`)
- **Goal**: Generate `implementation_plan.md` + Mermaid diagrams.
- **Audit**: Triple-Agent UltraThink audit required.

### Stage 3: DNA & PR Audit (Adjudicator)
- **Agent**: Arena AI (Red Team)
- **Goal**: Verify plan and PR health against V12 constraints (No locks, Atomic, ASCII-only).
- **Gate**: PASS/FAIL. Fail triggers Stage 2 rework.

### Stage 4: Recursive Execution (Engineer Selection)
- **Action**: Hand off to the selected Engineer via the Bob CLI Orchestrator session.
- **Targets**: 
  - **Bob CLI** for extraction/splitting (P5 Surgical).
  - **Codex CLI** for logic hardening (P5 Logic).
  - **Gemini CLI** for **Utility/Non-src** tasks (P5 Utility). Always use Gemini for model-agnostic tasks to conserve specialized tokens.
- **Safety**: Mandatory checkpointing enabled.

### Stage 5: Verification/Review (Forensics)
- **Agent**: Bob CLI (verify cycle) + Orchestrator
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