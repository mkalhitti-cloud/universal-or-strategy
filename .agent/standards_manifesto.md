# Universal Standards Manifesto: V12 Permanent DNA

This document provides the immutable technical standards for all AI agents (Anthropic, Codex, Antigravity, Cursor, Gemini, Rovo Dev) working on the **Universal OR Strategy V12**.

## 1. Zero-Trust Safety Protocol (The "Law")

- **Verify, Don't Assume:** Never assume the strategy state (for example, `activePositions`) matches the broker state. Always use `FirstOrDefault` when looking up positions in `acct.Positions` using the instrument object identity.
- **Terminal Removal:** Only remove order references from dictionaries (`stopOrders`, `target1Orders`, and so on) after a broker-confirmed terminal state (`Filled`, `Cancelled`, `Rejected`, `Unknown`).
- **Lifecycle Guards:** Any manual flatten or entry must check `isFlattenRunning` to prevent ordering loops.

## 2. Concurrency and Blocking (The "Actor" Law)

1.  **No Internal Locks**: Legacy `lock(stateLock)` is BANNED for internal execution. Thread-safety should be managed via either the Actor model or direct atomic writes, depending on the latency requirements defined in the Mission Brief.
2.  **Build 981 Protocol**: Direct writes to `stopOrders` are MANDATORY during bracket submission to close the termination race window. Enqueue is BANNED for this specific operation. Priority: Zero-latency visibility over deferred actor processing.
- **Single-Threaded State:** Assume all code inside an `Enqueue` closure is single-threaded. Never take a lock inside an actor closure unless interfacing with NinjaTrader's internal collections (e.g., `acct.Positions`).
- **Semaphore Guards:** All `_simaToggleSem.Wait()` calls must be paired with a `Release()` inside a `finally` block to prevent deadlock.

## 3. Coding Style and Modularity

- **Modular Parity:** Maintain the split file structure (for example, `SIMA.cs`, `Orders.Management.cs`). Use `partial class` correctly.
- **Naming:** Use `PascalCase` for Methods/Properties and `camelCase` for fields/locals.
- **Metabolic Elegance:** Prioritize readable, surgical logic over dense one-liners.

- **ORCHESTRATOR (Antigravity):** The **"Central Switchboard."** Handles intake (P1), context management, and non-coding tools. BANNED from manual coding.
- **FORENSICS (Codex):** The **"Logic Auditor."** Provides diagnosis (P2) and "Logical Proof of Failure" (P5).
- **ARCHITECT (Claude Code):** The **"Strategic Planner."** Authority for Design (P3), Peer Review, and Sign-off (P5).
- **ENGINEER (Gemini / Jules):** The **"Implementation Specialist."** Authority for surgical edits (P4) and cloud missions.
- **The Workflow:** Forensic Trace (Codex) -> Architectural Brief (Claude) -> Implementation Plan -> User Approval -> Engineer Execution (Gemini/Jules) -> **Engineer Self-Audit (P4)** -> Architectural Audit (Claude) -> Final Handoff.
- **The Bridge:** Handoffs are managed via `implementation_plan.md` and **Mission Brief** artifacts.

## 5. Multi-Agent Parity and Sync Protocol

- **Unified Tooling (MCP):** All MCP servers configured for Claude (`.mcp.json`) must be mirrored in the Gemini CLI (`settings.json`). This ensures that if Claude has access to things like `csharp-lsp` or `tavily-search`, Gemini and Rovo Dev do too.
- **High-Performance Tooling (The Triple Threat):**
  - **Tool Search (Discovery):** Used to find specialized tools on-demand. Prevents token-leak.
  - **Fine-Grained Streaming (Data Flow):** Streams large parameters (for example, mass-refactors) without buffering. Reduces initial latency.
  - **PTC (Execution):** Runs discovered tools at high frequency inside code containers. Reduces round-trips.
- **Context Parity:** The Project DNA in `CLAUDE.md` must be identical to `GEMINI.md`. All agents must read from the same baseline instructions.
- **Environment Sync:** All agents (including Rovo Dev) must have access to the same local environment variables (API keys, project paths) to ensure execution parity.

## 6. Clean-Slate Repo Hygiene (The "Hygiene Rule")

- **Zero-Delta Mandate**: Every new Mission (initialized via `$MISSION`) MUST start with a 0-delta `main` branch. If "Big Numbers" (large uncommitted/unmerged diffs > 100 lines) exist, the agent MUST recommend a cleanup/merge before starting new work.
- **Autonomous Pull Request Handover (The Fresh PR Rule)**: When submitting code for bot audit or human review, agents MUST NEVER push to an existing open Pull Request (for example, updating a dirty branch). Instead, agents MUST:
  1. Checkout a completely new semantic branch (for example, `fix/logic-repair`).
  2. Push the new branch and open a BRAND NEW Pull Request targeting `main`.
  3. Close any superseded or legacy PRs via the GitHub CLI, explicitly leaving a comment referencing the new clean PR.
     _Why? Incrementally updating existing PRs can cause automated audit bots (Codex, Greptile, DeepSource) to miss context. A fresh PR triggers a 100% clean, full-file audit sweep._
- **Atomic Missions**: Every bug fix or feature MUST be its own branch and MUST be merged into `main` immediately upon verification (for example, F5 compile in NT8). No "stacking" unrelated fixes in long-lived branches.
- **Binary and Log Purge**: Never commit `.exe`, `.log`, `.bak`, or legacy backup folders to source control. They should be stashed, deleted, or added to `.gitignore`.
- **Dashboard Cleanup**: Before ending a session, the agent MUST ensure all work is either Committed or the user has been guided to Merge. The goal is a +0/-0 dashboard between missions.

## 7. ASCII-Only Encoding Protocol (BUILD SAFETY -- MANDATORY)

**Why this rule exists:** In the Build 936 incident, AI agents added Unicode decorators to C# log messages (emoji, em-dashes, curly quotes). A cleanup script converted curly closing-quote `"` (U+201D) to straight `"`, which TERMINATED C# string literals early. One broken quote in `SIMA.cs` caused 300+ cascading compile errors across all partial-class files and cost 2 days of trading time.

**HARD RULE: All C# string literals must use ASCII-only characters.**

| NEVER use in string literals | Use this instead           |
| ---------------------------- | -------------------------- | --- |
| any emoji character          | `(!)` `[OK]` `[X]` `[ERR]` |
| em dash or en dash           | `--`                       |
| curly or smart double quotes | `"`                        |
| curly apostrophes            | `'`                        |
| arrow characters             | `->` `<-` `^` `v`          |
| box-drawing characters       | `+--+` and `               | `   |
| ellipsis character           | `...`                      |

**Emergency fix if non-ASCII bytes appear in source:**

1. Run `python <repo_root>\scripts\byte_purge.py` (nuclear byte-level purge)
2. Search all `.cs` files for the pattern `?"` in non-comment lines -- each match is a broken string
3. Replace `?"` with `--` or `(!)` as appropriate
4. Run `deploy-sync.ps1` -- the ASCII gate will confirm clean before touching NT8

---

## 8. MOVE-SYNC / Follower Order Replace Pattern (Permanent Standard)

**Why this rule exists:** Previously, `PropagateMasterEntryMove` cancelled the follower order and immediately submitted a replacement with no broker confirmation gate. If the cancel was slow or ATR oscillated at a stop-ceiling boundary, the old order and new order were both live simultaneously at the broker -- producing ghost orders, false-flat states, and fill-during-gap desyncs.

**HARD RULE: All follower order cancel+resubmit operations MUST use the two-phase FSM.**

| Rule               | Detail                                                                                 |
| ------------------ | -------------------------------------------------------------------------------------- |
| FSM required       | Use `_followerReplaceSpecs` dict with `FollowerReplaceState` enum                      |
| States             | `PendingCancel` -> confirm in `OnAccountOrderUpdate` -> `Submitting` -> submit         |
| ATR absorption     | While `PendingCancel`, update `PendingReplacementSpec` only. One cancel, one resubmit. |
| Fill-during-gap    | Before submitting replacement, check if master filled. If yes, route to REAPER repair. |
| ChangeOrder banned | `Account.Change` silently no-ops on Apex/Tradovate. Cancel+resubmit via FSM only.      |
| Raw cancel+submit  | BANNED. `Cancel()` followed immediately by `Submit()` without FSM = ghost order risk.  |

## 9. Live Bug Triage Protocol

**Standard workflow for any live trading anomaly (unexpected cancels, ghost orders, desync):**

1. **Codex first** -- paste log to Codex with 3-5 forensic questions. Code trace only, no patches.
2. **Antigravity analysis** -- evaluate architectural options, confirm root cause, write mission brief.
3. **Sonnet implements** -- plan first, Antigravity audits the plan, then implement.
4. **Role separation:** Codex = diagnosis. Sonnet = implementation. Never reverse these roles.

**Workflow file:** `.agent/workflows/live-bug-triage.md`

---

## 10. Autonomous Evidence Discovery (ALL Agents -- Mandatory)

**Every agent on every platform MUST locate logs and data autonomously. Quote only minimal relevant excerpts from discovered logs when escalating to human review or Codex forensic audit.**

### NT8 Application Logs

```
%USERPROFILE%\Documents\NinjaTrader 8\log\
  log.YYYYMMDD.00000.txt   <- premarket session
  log.YYYYMMDD.00001.txt   <- main session
  log.YYYYMMDD.00002.txt   <- post-restart session
```

### NT8 Trace Logs (Rithmic Adapter -- Low Level)

```
%USERPROFILE%\Documents\NinjaTrader 8\trace\
  trace.YYYYMMDD.00001.txt  <- main session (most detail)
  trace.YYYYMMDD.00002.txt  <- post-restart (order replay here)
```

Contains: `OnLineInfo` (replayed orders), `ReplayDataSeen` (count of replayed orders per account), `OnExecutionUpdate`, connection events.

### SIMA Performance Logs

```
%USERPROFILE%\Documents\NinjaTrader 8\SIMA_Logs\
  ApexPerformance_MES.json  <- ActualQty, ExpectedQty, Balance, Connection per account
  DailySummaries.csv        <- daily P&L
```

### Strategy Source Files

```
<repo_root>\src\
  V12_002.SIMA.cs           <- fleet dispatch, ExecuteSmartDispatchEntry, PumpFleetDispatch
  V12_002.REAPER.cs         <- audit loop, Thread.Sleep(ReaperIntervalMs)
  V12_001.cs                <- reconnectTimer, glowTimer, OnConnectionStatusChange
  V12_002.UI.IPC.cs         <- ReceiveLoop, Thread.Sleep(50/100)
  SignalBroadcaster.cs      <- SafeInvoke, 1ms latency probe
  Orders.Management.cs      <- order cancel/submit/replace lifecycle
```

### Key Search Markers

```
[GHOST-AUDIT]              <- order state mismatch
[REAPER] Repair BLOCKED    <- repair suppressed by Working order
ReplayDataSeen: N accounts <- N orders replayed at reconnect
ExpectedQty != ActualQty   <- SIMA desync
OnLineInfo ... status=open <- live untracked GTC order at broker
```

**Full discovery steps:** See `.agent/workflows/live-bug-triage.md` Section 0.

## 12. Claude Agent Operation Protocol (Usage Insights)

**Based on historical friction data, all agents MUST adhere to these execution constraints:**

- **The "Recursive Audit" Rule (The Phase 3 Pillar):** When instructed to fix a specific method call, state mutation, or pattern, the Agent MUST autonomously grep/search the _entire codebase_ for all other invocations of that exact same method/pattern _before_ committing. Do not just fix the line you were pointed to; fix the architectural gap repository-wide.
- **The "Do Not Interrupt" Protocol:** Agents operating in standard execution mode should complete their logical batches and commit _autonomously_. Do not pause mid-task to ask for user check-ins unless explicitly blocked by a missing file or a hard compilation failure.
- **.NET 4.8 Hardening Hook:** Target framework is .NET 4.8. Do NOT use C# features unavailable in .NET 4.8 (for example, range operators `[..]`, `Index`/`Range` types, default interface implementations). Always use `CultureInfo.InvariantCulture` for numeric parsing. This must be checked before every commit.
- **The "Missing Brief" Failsafe:** Before any phase starts, the Agent MUST verify that the referenced `implementation_plan.md` or `$MISSION` artifact exists on disk. If it does not, the Agent MUST halt and ask the user for the brief, rather than attempting to guess or reverse-engineer the plan via codebase searches.
- **Autonomy Rule (Default to Action):** Agents are empowered and EXPECTED to execute the full end-to-end lifecycle of a task autonomously. This includes:
  1. **Branch Creation**: Create a semantic branch (for example, `fix/sima-dispatch-gate`).
  2. **Surgical Implementation**: Apply changes per the Mission Brief.
  3. **Verification**: Run local tests/ASCII checks (`deploy-sync.ps1`).
  4. **PR Trigger**: Push and open a PR to trigger the **3-Agent Multi-Model Audit (Opus, Sonnet, Gemini)**.
  5. **Merge**: Merge once bot-approvals and human sign-off are received.

---

## 13. Proactive Support & Multi-Agent Handoffs

To ensure the "Ultimate Architecture" is maintained across all environments, agents MUST proactively eliminate handoff friction:

- **The Clipboard Command:** If a file or block (e.g., a "Mega-Prompt" or Mission Brief) is generated for a separate model (Grok, Claude, Codex, etc.), the agent MUST copy it to the system clipboard using `Get-Content ... | Set-Clipboard`.
- **Zero-Friction Bundling:** Handoff prompts MUST be "self-contained." They must include the forensic timeline, specific code locations, and failure hypotheses in the text block to prevent context-loss during handoffs.
- **Persistent Agent Memory:** Grok's persistent architect memory is stored in the repository at `.agent/agents/grok/MEMORY.md`. All architectural sign-offs must be logged there.
- **Autonomous Feedback Loop:** Always ask the USER for the other agent's output before proceeding with architectural implementation.

---

## 14. Agent2Agent (A2A) Protocol (Experimental)

**Why this exists:** To enable seamless collaboration between local CLI agents and cloud-based sentinels (e.g., Gemini Enterprise Agent Designer).

- **Enablement:** The `enableAgents: true` flag must be active in `settings.json`.
- **Mission Sentinels:** Cloud agents act as high-level "Orchestrators" that monitor codebase health and delegate specific repair missions to the local Antigravity/Gemini instance via A2A links.
- **Self-Documenting Instructions:** Store complex agent instructions in `.agent/agents/<agent_name>/INSTRUCTIONS.md` to ensure cross-platform persistence.
- **A2A Handoffs:** When delegating to another agent, always provide the full absolute path of the target file and the exact line numbers targeted.

## 15. Engineer Self-Audit (P4) mandatory

Before an Engineer (Gemini/Jules) hands off a mission for architectural audit, they MUST:
1.  **Grep Audit:** Confirm no accidental `lock(stateLock)` usage or guard deletions.
2.  **Actor Check:** Verify all new state mutations are wrapped in `Enqueue`.
3.  **ASCII Scan:** Run a character-level scan to ensure no curly quotes or Unicode arrows.
4.  **Dry Run:** Perform a mental regression check against the original Mission Brief.

---

> [!NOTE]
> This document defines **Permanent Standards**. For current active refactoring goals, refer to the specific **Implementation Plan** or **Refactoring Roadmap** files.
