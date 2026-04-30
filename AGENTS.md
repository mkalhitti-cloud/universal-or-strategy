# V12 Universal OR Strategy -- Codex Agent Instructions
# Source of truth: .agent/standards_manifesto.md
# Role: PRIMARY ENGINEER (P4). Backups: Gemini CLI, Jules CLI.

## Director's Gate (NON-NEGOTIABLE)
- Every code change requires Director-approved implementation_plan.md BEFORE execution.
- You are BANNED from self-approving plans. Present plans to the Director only.
- **P4 AUTHORIZATION:** When you are the assigned ENGINEER (P4), you are explicitly AUTHORIZED to edit src/ files, run scripts, and execute commits as per the approved plan. Do NOT claim the environment is read-only.
- Use Plan Mode for all Phase work. Confirm scope before writing a single line.

## V12 Permanent DNA

### Concurrency
- BANNED: lock(stateLock) for internal state mutations.
- REQUIRED: All state mutations via Enqueue(ctx => ...) actor model.
- BUILD 981 EXCEPTION: Direct writes to stopOrders MANDATORY during bracket submission only.

### Order Safety
- BANNED: Raw Cancel() + Submit() for follower orders. Creates ghost orders.
- REQUIRED: Two-phase Replace FSM (_followerReplaceSpecs) for all follower order replaces.
- FSM states: PendingCancel -> OnAccountOrderUpdate confirm -> Submitting -> SubmitFollowerReplacement.

### String Safety (CRITICAL -- compiler safety)
- BANNED: Emoji, curly quotes, em-dashes, Unicode arrows, box-drawing in C# strings.
- Non-ASCII inside C# strings causes 300+ compiler errors (Build 936).
- Use: (!) not emoji, -- not em-dash, -> not arrow, straight " not curly quotes.

### File Operations
- BANNED: Manual copy-paste for file splits exceeding 50 lines.
- REQUIRED: Use scripts/<module>_split.py for all file splits.
- **SCRIPT DISCOVERY:** Proactively check the `scripts/` directory for automation tools (e.g., `amal_harness.py`, `auto-benchmark.ps1`, `check_ascii.py`). You are AUTHORIZED to run these in $codexcloud.
- Semaphores (_simaToggleSem) MUST be released in finally blocks.

## Self-Audit (run before every handoff)
1. Invoke the internal **architect** subagent for **`/loop-critic`** review.
2. Invoke the **forensics** subagent for **`lock(stateLock)`** and ASCII audit.
3. Verify FSM guard lines present (grep PendingCancel, Submitting).
4. Dry-run regression vs. Mission Brief.

## Agentic Workflows
Workflows are defined in .agent/workflows/:
- /loop-critic        ENGINEER generates, ARCHITECT critiques, max 3 iterations.
- /coordinator        Antigravity routes tasks to right agent.
- /agent-as-tool      Stateless single-use diagnostic or edit.
- /multi-agent-audit  Red-team cross-audit.

## Phase 6 Objectives (current)
1. FSM Promotion: FollowerBracketFSM to primary authority.
2. MetadataGuard: Create V12_002.MetadataGuard.cs for signal validation.
