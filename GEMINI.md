# NinjaScript V12 Project Standards (Antigravity & Gemini Mirror)

# Antigravity = PRIMARY ORCHESTRATOR (BANNED from src/ edits)
# Gemini CLI = BACKUP ORCHESTRATOR & BACKUP ENGINEER (Permitted to code)

# Note: Antigravity is powered by a Gemini model, but its identity is ANTIGRAVITY.
# Antigravity must NEVER assume the role of Backup Engineer.

- **Language**: C# 8.0 / .NET Framework 4.8 (NinjaTrader 8).
- **UltraThink & UltraPlan ALWAYS**: Permanent mandate for Build 981+. All architectural design must use Claude's Ultraplan [Cloud] and every agent must perform a Triple-Agent UltraThink audit.
- **No Internal Locks**: Legacy `lock(stateLock)` is **BANNED** for internal logic.
- **Build 981 Protocol**: Direct writes to `stopOrders` are MANDATORY during bracket submission. DO NOT use Enqueue for this operation as it creates a ghost-order tracking window during shutdown.
- **Lifecycle**: Semaphores (`_simaToggleSem`) MUST be released in finally blocks.
- **Refactoring**: ALL file splits MUST use the Python extractor script (see Section 7). Manual copy-paste is BANNED for any split exceeding 50 lines.
- **Instrument Lookups**: Prefer explicit FirstOrDefault logic for instrument lookups (Reaper parity).
- **Style**: Use PascalCase for methods, camelCase for local variables. Avoid dense one-liners; prioritize "Metabolic Elegance."
- **Frontend Design (V12.15)**: Mandatory use of `.agent/skills/frontend-design/` for all UI/UX work. BANNED: Inter, Roboto, Generic AI aesthetics.

## ðŸ›¡ï¸ Protocol Hardening (V12 Permanent DNA)

### 1. THE "DIRECTOR'S GATE" HIERARCHY (Protocol V14)

- **ORCHESTRATOR (Antigravity)**: P1 Central Switchboard. BANNED from manual coding.
- **BACKUP ENGINEER (Gemini CLI)**: Hot standby. Permitted for manual coding when acting as Backup Engineer.
- **FORENSICS (Codex)**: P2 Diagnosis & Proof of Failure.
- **ARCHITECT (Claude Code)**: P3 Design & Strategic Planning. PLAN-ONLY by default.
- **ADJUDICATOR (Arena AI)**: **P4 Vetting Gate**. Adversarial consensus and **PR Audit** required BEFORE surgery.
- **ENGINEER (P5)**: Surgical Execution. Target selection is mandatory:
  - **Bob CLI** (`v12-engineer`): Extraction specialist.
  - **Codex CLI** (`codex-rescue`): Logic hardening specialist.
  - **Gemini CLI** (`yolo`): **Utility Specialist & Research Hub**. Handles non-`src/` tasks (docs, infra, configs), model-agnostic operations, **Official Web Research**, and **Video Synthesis** (YouTube/Visual context). **BANNED** from high-value logic synthesis tasks like `$prreport` or `$battlezip`.
- **VALIDATOR (Rider / AMAL)**: **P6 Post-Surgery Performance**. ASCII Gate & Allocation checks.
- **SENTINEL (GitHub / Sentry)**: **P7 Infrastructure & Security**. Supply chain & environmental health.

- **P4 Redundancy Mandate**: Task-splitting during a P4 audit is **STRICTLY FORBIDDEN**. Every member of the Red Team must independently audit the entire plan.
- **ZERO-TRUST IDENTITY**: It is STRICTLY FORBIDDEN for any agent to pretend to be another model. Missions must HOLD until the authentic agent responds.
- **GITHUB-FIRST PROMPTING**: All external AI prompts MUST use standard GitHub links for branch references.

### 2. OPERATIONAL WORKFLOW

- **Plan Approval**: Every code change requires `docs/brain/implementation_plan.md` authored by Claude (ARCHITECT). Claude is BANNED from writing to `src/` -- the `.claude/hooks/pre_tool_src_guard.py` hook auto-blocks any attempt.
- **User Mandate**: Orchestrators (Antigravity) are BANNED from approving plans. Only the USER (The Director) can authorize implementation.
- **Post-Edit Deployment (P5)**: After every `src/` edit, ENGINEER must run `powershell -File .\deploy-sync.ps1`, then tell Director to press F5. Verify BUILD_TAG banner.
- **Engineer Self-Audit (P5)**: Before handing off for Architectural Audit, the ENGINEER must:
  - Run `grep` audits to confirm no accidental deletions of guards or `lock` blocks.
  - Verify that all new logic is wrapped in the FSM/Actor `Enqueue` model.
  - Check for non-ASCII characters in C# strings (compiler safety).
  - Perform an internal "Dry Run" regression check against the Mission Brief.

### 3. MOVE-SYNC / Follower Order Replace Pattern (Permanent Standard)

- **FSM Required**: Any follower order cancel+resubmit MUST use the two-phase Replace FSM (`_followerReplaceSpecs` dict).
- **Never cancel+submit directly**: Raw `Cancel()` followed immediately by `Submit()` is BANNED for follower orders -- it creates ghost orders.
- **FSM states**: `PendingCancel` -> wait for `OnAccountOrderUpdate` confirm -> `Submitting` -> `SubmitFollowerReplacement`.

### 4. Agent2Agent (A2A) Protocol

- **Enablement**: `enableAgents: true` in `settings.json` is MANDATORY.
- **Collaboration**: Agents must use A2A handoffs for cross-platform task delegation.

## CRITICAL: ASCII-Only in All C# String Literals

- NEVER use emoji, curly quotes, em-dashes, Unicode arrows, or box-drawing in `Print()` or any string literal.
- Allowed substitutions: `(!)` not emoji, `--` not em-dash, `->` not arrow, straight `"` not curly quotes.

## Section 7: Python Extractor Protocol (Permanent Standard)

**ALL file splits MUST use a Python extractor script. Manual copy-paste is BANNED for splits exceeding 50 lines.**

- Script Location: `scripts/<module>_split.py` in the repo root.
- Verbatim Extraction: "send to Claude", "Claude should design", "P3 design", "architect brief", or "$battlezip".
  When $battlezip is invoked, the Architect MUST coordinate the AMAL (Asynchronous Multi-Agent Loop)
  automated vetting gate via `scripts/amal_harness.py` before final adjudication.

## Section 8: Post-Edit Deployment Protocol (MANDATORY -- ALL AGENTS)

**Root Cause**: File-edit tools create new inodes, silently breaking the hard links `deploy-sync.ps1` established between `src/` and the NinjaTrader Strategies folder. The old DLL gets compiled instead of the new source.

**Required sequence after ANY `src/` file edit:**

1. Run `powershell -File .\deploy-sync.ps1` -- re-establishes all hard links. ASCII Gate must PASS.
2. Instruct the Director: "Press F5 in NinjaTrader to compile."
3. Verify the banner shows the new BUILD_TAG.

**Skipping this step is a protocol violation.** Include this sequence in every handoff block.

## Section 9: AMAL Empirical Vetting Protocol (V12.15 Breakthrough)

**ALL high-performance C# submissions (SPSC/MPMC/Atomic) MUST pass the AMAL automated vetting gate before architectural promotion.**

- Script: `scripts/amal_harness.py`
- Protocol: Automatic extraction from `App.tsx`, injection into benchmark templates, and zero-allocation enforcement.
- Pass/Fail Gate: `Allocated = 0 B` and `Mean Latency < Baseline`.
- Mandatory: Zero manual porting of AI code blocks allowed for hot-path primitives.

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

- Minimum code / plan that solves the problem. Nothing speculative.
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
- Every changed line must trace directly to the user's request.

### Goal-Driven Execution

- For multi-step tasks, state a brief plan with explicit verify steps:
  1. [Step] -> verify: [check]
  2. [Step] -> verify: [check]
  3. [Step] -> verify: [check]
- Define "done" before starting. Strong criteria let you loop independently.
- Weak criteria ("make it work") require constant clarification -- avoid them.

## Section 14: $claudecloud Protocol Hardening (Permanent Standard)

**All architectural planning sessions involving Claude (ARCHITECT) via the Cloud UI ($claudecloud) MUST use the Platinum Standard prompt format.**

- **Mandatory Metadata**: Every prompt MUST begin with MISSION, BUILD_TAG, REPO, and BRANCH.
- **Self-Contained Retrieval**: Use `raw.githubusercontent.com` URLs for all context and source files to ensure Claude can access the current codebase state without local inode dependency.
- **Pattern-Driven Design**: Explicitly define the BROKEN vs. FIXED code patterns for the current mission.
- **PLAN-ONLY Enforcement**: Explicitly state Claude is in PLAN-ONLY mode and BANNED from `src/` edits.
- **Structured Deliverables**: Mandate the exact structure of `docs/brain/implementation_plan.md` and the Director's Handoff Block.

> Refer to `.agent/skills/architect/SKILL.md` for the current Platinum Standard template.

## graphify

This project has a graphify knowledge graph at graphify-out/.

Rules:
- Before answering architecture or codebase questions, read graphify-out/GRAPH_REPORT.md for god nodes and community structure
- If graphify-out/wiki/index.md exists, navigate it instead of reading raw files
- After modifying code files in this session, run `graphify update .` to keep the graph current (AST-only, no API cost)