# NinjaScript V12 Project Standards (Gemini Mirror)

- **Language**: C# 8.0 / .NET Framework 4.8 (NinjaTrader 8).
- **No Internal Locks**: Legacy `lock(stateLock)` is **BANNED** for internal logic. All state mutations must be thread-safe; use the `Enqueue(ctx => ...)` model by default EXCEPT when direct-write is required for termination safety (see Build 981).
- **Build 981 Protocol**: Direct writes to `stopOrders` are MANDATORY during bracket submission. DO NOT use Enqueue for this operation as it creates a ghost-order tracking window during shutdown.
- **Lifecycle**: Semaphores (`_simaToggleSem`) MUST be released in finally blocks.
- **Refactoring**: ALL file splits MUST use the Python extractor script (see Section 7). Manual copy-paste is BANNED for any split exceeding 50 lines.
- **Instrument Lookups**: Prefer explicit FirstOrDefault logic for instrument lookups (Reaper parity).
- **Style**: Use PascalCase for methods, camelCase for local variables. Avoid dense one-liners; prioritize "Metabolic Elegance."

## 🛡️ Protocol Hardening (V12 Permanent DNA)

### 1. THE "DIRECTOR'S GATE" HIERARCHY

- **ORCHESTRATOR (Antigravity)**: The Central Switchboard. Intake (P1) and multi-agent coordination.
- **FORENSICS (Codex)**: Diagnosis (P2) and Logic Audits (P5). Provides "Logical Proof of Failure."
- **ARCHITECT (Claude Code)**: Design & Strategic Planning (P3). Peer Review & Sign-off (P5).
- **ENGINEER (Gemini / Jules)**: Implementation (P4). Execution of approved surgical edits.

### 2. OPERATIONAL WORKFLOW

- **Plan Approval**: Every code change requires an `implementation_plan.md` designed by Claude/Codex.
- **User Mandate**: Antigravity is BANNED from approving plans. Only the USER (The Director) can authorize implementation.
- **Engineer Self-Audit (P4)**: Before handing off for Architectural Audit, the ENGINEER must:
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
- Verbatim Extraction: Script reads source lines by index and writes exact bytes. 
