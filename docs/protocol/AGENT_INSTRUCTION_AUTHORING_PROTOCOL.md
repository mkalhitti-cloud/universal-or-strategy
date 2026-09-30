# Agent Instruction Authoring Protocol (AIAP) v1.0

**Status**: MANDATORY -- applies to every agent at every tier
**Author**: Tier 1 Orchestrator (Wave 7 / Wave 8 architecture)
**Applies to**: roleDefinition blocks, start_subtask messages, spawn_subagent descriptions,
               mode definitions in custom_modes.yaml, skill SKILL.md files, handoff prompts

---

## CORE PRINCIPLE

> Every instruction you write is a *program*, not a memo.
> The receiving LLM has no memory, no context, no intent -- only what you give it.
> Write for zero shared context. Write for deterministic execution.
> If a human would find your instructions oddly mechanical, you are on the right track.

All agents in this system are multiples of Tier 1.
A Phase 5 worker must execute with exactly the same discipline as Tier 1 would.
The only way to guarantee this is to encode the discipline *in the instruction itself* --
not rely on the receiving agent to infer it.

---

## SECTION 1: THE SEVEN LAWS OF LLM INSTRUCTION AUTHORING

### LAW 1 -- Sequence Before Content

LLMs execute top-to-bottom in the order instructions appear.
**Put the sequence first. Put the what second.**

WRONG (human-style):
```
You are an expert at X. Your goal is Y.
The project uses Z technology.
Before you start, make sure to do A, B, C.
```

RIGHT (LLM-style):
```
STEP 1: Do A.
STEP 2: Do B.
STEP 3: Do C.
CONTEXT: You are doing this because of Y. The technology is Z.
```

The human reads the goal first and derives the steps.
The LLM needs the steps first -- it will use the context to qualify them.

### LAW 2 -- Name Every Tool Call Explicitly

Do not say "use MCP to check the code."
Say exactly which tool, with which parameters, in which order.

WRONG:
```
Use jCodemunch to understand the file before editing.
```

RIGHT:
```
TOOL CALL 1: mcp__jcodemunch-mcp__resolve_repo({ "path": "." })
TOOL CALL 2: mcp__jcodemunch-mcp__get_file_outline({ "path": "src/V12_002.SIMA.cs" })
TOOL CALL 3: read_file({ "path": "src/V12_002.SIMA.cs", "range": "<lines from outline>" })
-- Only after all 3 complete: proceed to edit.
```

The LLM will comply with a named tool call. It will improvise with a vague instruction.

### LAW 3 -- State Machine Over Prose

Instructions must have exactly one valid interpretation at every decision point.
Use explicit state transitions, not English paragraphs with "if" buried inside them.

WRONG:
```
If the build fails, try to fix it. If it still fails after a few attempts,
you might want to check the logs and potentially escalate.
```

RIGHT:
```
GATE: Run build.
  EXIT 0 -> proceed to STEP 5.
  EXIT 1 -> read build output, apply targeted fix, re-run build (max 2 retries).
  EXIT 1 after 2 retries -> write HARD_FAILURE to event_log.jsonl, STOP.
  NO OUTPUT -> treat as EXIT 1.
```

### LAW 4 -- Forbidden Actions Are More Important Than Allowed Actions

LLMs have a bias toward action and helpfulness.
They will add things, "improve" things, and expand scope unless explicitly forbidden.
The most critical lines in any instruction are the FORBIDDEN sections.

ALWAYS include a FORBIDDEN block. Structure it as a flat list. Put it EARLY -- not at the end.

```
FORBIDDEN (check before every action):
- NEVER use lock() -- BANNED, zero tolerance, escalate immediately if found
- NEVER use DateTime.Now -- use DateTime.UtcNow only
- NEVER write to src/ without a preceding read_file of the same file
- NEVER spawn a new subtask when the current one has not returned VERIFIED_COMPLETE
- NEVER use NUnit or MSTest -- xUnit [Fact] only
- NEVER skip the graphify update at task start and task end
```

### LAW 5 -- Verification Is a First-Class Step, Not an Afterthought

Every instruction that produces output must include an explicit verification step
that is *different* from the production step. Do not let the agent verify its own output
with the same tool that produced it.

WRONG:
```
Write the fix, then confirm it compiles.
```

RIGHT:
```
STEP N: Apply fix using apply_diff.
STEP N+1 (VERIFY): Run `dotnet build` -- capture exit code.
STEP N+2 (VERIFY): Run `grep -r "lock(" src/` -- must return 0 results.
STEP N+3 (VERIFY): Run `python scripts/complexity_audit.py` -- target method must show CYC <= 8.
ONLY AFTER all 3 verify steps pass: write completion artifact.
```

### LAW 6 -- Artifacts Are the Contract

A subtask that produces no artifact cannot be verified by the orchestrator.
Every task -- no matter how small -- must write a file, update a JSON, or return
a structured string that the caller can inspect.

Artifact rules:
- Location: always under `docs/brain/<EPIC_ID>/` or `.lamport/wave7/`
- Format: JSON for machine-readable, markdown for human-readable
- Content: MUST include status (pass/fail), evidence (tool output), and timestamp
- Immutable: never overwrite -- append or use versioned filenames

### LAW 7 -- The Closing Gate Is Mandatory

Every instruction must end with a COMPLETION GATE.
The gate is a checklist the agent must pass before reporting done.
If any item fails, the agent is NOT done -- it must loop back.

```
COMPLETION GATE (all must pass before reporting VERIFIED_COMPLETE):
[ ] Build exits 0
[ ] Zero lock() in src/
[ ] Target method CYC <= 8 (confirmed by complexity_audit.py)
[ ] Completion artifact written to docs/brain/<EPIC_ID>/
[ ] graphify update run (shutdown step)
[ ] event_log.jsonl updated with lamport clock increment
```

---

## SECTION 2: STRUCTURAL TEMPLATE FOR LLM INSTRUCTIONS

Use this structure for every instruction you write -- mode roleDefinition,
start_subtask message, spawn_subagent description, or skill SKILL.md.

```
# [TASK NAME] -- [VERSION]

## IDENTITY
You are [EXACT ROLE]. You are running as a [TIER N] agent.
You are a multiple of Tier 1. Execute with Tier 1 discipline.

## FORBIDDEN (read before any action)
- [list all banned patterns -- lock(), DateTime.Now, NUnit, etc.]
- [list all banned scope expansions]
- [list all banned tool substitutions]

## COLD-START SEQUENCE (execute in exact order, no skipping)
STEP 1: [mandatory first tool call -- always graphify or resolve_repo]
STEP 2: [mandatory second tool call]
STEP 3: Read [specific artifact] to load context.
HALT if any cold-start step fails after 1 retry -- report COLD_START_FAILED.

## INPUTS
- [named input 1]: [exact path or parameter]
- [named input 2]: [exact path or parameter]

## TASK SEQUENCE (execute in exact order)
STEP 4: [exact action with exact tool]
STEP 5: [exact action with exact tool]
GATE after STEP 5: [exact condition -- if fail, go to STEP 5 retry]
STEP 6: [next action -- only execute after gate passes]
...

## VERIFICATION (mandatory -- different tools from production steps)
VERIFY 1: [tool call + expected output]
VERIFY 2: [tool call + expected output]
VERIFY 3: [tool call + expected output]

## OUTPUT ARTIFACT
Write to: [exact path]
Format:
{
  "epic_id": "...",
  "status": "pass" | "fail",
  "evidence": { ... },
  "lamport_clock": N,
  "timestamp_utc": "..."
}

## COMPLETION GATE
[ ] All verify steps passed
[ ] Artifact written
[ ] graphify shutdown update run
[ ] event_log.jsonl updated
Report: VERIFIED_COMPLETE <EPIC_ID> | HARD_FAILURE <EPIC_ID> reason=...

## ESCALATION
If HARD_FAILURE: write to .lamport/wave7/event_log.jsonl, STOP.
Do NOT attempt workarounds. Do NOT reduce scope silently. STOP and report.
```

---

## SECTION 3: TOOL-USE ORDERING PROTOCOL

LLM agents have a tendency to reach for the most convenient tool, not the right one.
These rules establish the canonical tool-use order for this codebase.

### Before ANY file edit:
1. `graphify update . --no-cluster --no-description` (if not run in last 5 minutes)
2. `mcp__jcodemunch-mcp__resolve_repo({ "path": "." })`
3. `mcp__jcodemunch-mcp__get_file_outline({ "path": "<target>" })`
4. `read_file({ "path": "<target>" })` -- ONLY the lines you need
5. Apply edit
6. `dotnet build` -- verify

### Before ANY complexity claim:
1. `python scripts/complexity_audit.py` -- ground truth oracle
2. Never claim CYC from visual inspection alone
3. Never claim "this is below 8" without audit output as evidence

### Before ANY push:
1. `powershell -File .\scripts\pre_push_validation.ps1`
2. `powershell -File .\deploy-sync.ps1`
3. `git status --short` -- must show clean or only expected files
4. `git push origin <branch>`

### For MCP tool failures:
1. Retry once -- same call, same parameters
2. If fails again: report MCP_FAILED, STOP -- do NOT fall back to native file tools
3. Native tools (read_file, grep) are NOT substitutes for jCodemunch MCP
4. Exception: `read_file` is permitted ONLY for files you are about to edit

### Tool precedence order (highest to lowest authority):
1. MCP tools (jCodemunch, sequential-thinking) -- primary exploration
2. execute_command (build, test, audit scripts) -- verification
3. apply_diff / search_and_replace -- surgical edits only
4. read_file -- only for files being edited, after jCodemunch outline
5. write_file -- only for new files or complete rewrites
6. grep / glob -- last resort search, only if MCP unavailable

---

## SECTION 4: INTENT PRESERVATION ACROSS DELEGATION TIERS

### The Telephone Problem

Tier 1 -> Phase Orchestrator -> Worker: each hop degrades intent fidelity.
The Phase Orchestrator rewrites instructions to pass to workers.
The worker receives a further-degraded version.
By Worker, the original Tier 1 discipline may be entirely absent.

### The Solution: Embed, Don't Reference

WRONG (reference style -- loses fidelity):
```
Follow the OKF rules documented in docs/intel/jane-street/.
See AGENTS.md for protocol details.
Apply the standard V12 patterns.
```

RIGHT (embed style -- zero fidelity loss):
```
MANDATORY CONSTRAINTS (embedded -- do not look these up, follow them now):
- lock() is BANNED. Zero tolerance. grep -r "lock(" src/ must return 0.
- DateTime.Now is BANNED. Use DateTime.UtcNow.
- NUnit/MSTest are BANNED. Use xUnit [Fact] only.
- CYC > 8 is BANNED. Run complexity_audit.py to verify.
- New allocations on hot path are BANNED. No LINQ, no new T() per call.
```

The receiving agent must not need to fetch a document to understand a constraint.
The constraint must be in the instruction. Full stop.

### The Amplification Rule

Each tier must RE-EMBED the constraints it received, plus any new ones it discovered.
A Phase Orchestrator that receives 10 constraints from Tier 1 must pass ALL 10
to its workers -- plus any phase-specific constraints it adds.

Constraints must never be summarized, paraphrased, or abbreviated in transit.
Copy them verbatim. If the receiving tier's context window is a concern,
that is a sign the task needs to be split, not that the constraints should be trimmed.

### The Evidence Chain Rule

Every tier must pass evidence of its own verification to the next tier.
A worker that outputs "status: pass" without tool output as evidence
is producing an unverifiable claim.

WRONG worker output:
```
{ "status": "pass", "message": "Build succeeded and CYC is good" }
```

RIGHT worker output:
```
{
  "status": "pass",
  "evidence": {
    "build_exit_code": 0,
    "build_output_tail": "Build succeeded. 0 Error(s). 0 Warning(s).",
    "complexity_audit_line": "V12_002.SIMA.Dispatch.cs::Dispatch CYC=7 PASS",
    "lock_scan": "grep -r lock( src/ returned 0 results",
    "graphify_updated": true
  }
}
```

The orchestrator can verify the evidence. The orchestrator cannot verify a claim.

---

## SECTION 5: MODE DEFINITION AUTHORING (custom_modes.yaml)

When writing a new mode's `roleDefinition`:

### Required sections in order:
1. **IDENTITY** -- single sentence, role + tier + wave context
2. **FORBIDDEN** -- full list, embedded, no references
3. **COLD-START SEQUENCE** -- exact tool calls, exact order
4. **TASK PROTOCOL** -- numbered steps, gates between steps
5. **COMPLETION GATE** -- checklist with exact verification commands
6. **ESCALATION** -- what to do on failure (STOP, not improvise)

### Groups permissions (always flat list):
```yaml
groups:
  - read
  - edit
  - execute
  - mcp
```
Never use nested lists. The broken `- - edit` pattern causes silent permission failures.

### roleDefinition length:
- Minimum: 200 lines for any phase worker
- Maximum: none -- completeness beats brevity for LLM instructions
- Compression is the enemy: every trimmed constraint is a future bug

---

## SECTION 6: SKILL AUTHORING (SKILL.md files)

Skills are reusable instruction sets loaded into any agent's context on demand.
They follow the same laws but have additional requirements:

### Skill file structure:
```markdown
# [SKILL NAME] v[VERSION]

## TRIGGER CONDITIONS
Activate this skill when: [exact conditions, not vague descriptions]
Do NOT activate when: [anti-patterns]

## COLD-START
[exact sequence]

## PROTOCOL
[numbered steps with gates]

## VERIFICATION
[exact tool calls]

## POST-USE AUDIT (mandatory)
After every use: check if any instruction was ambiguous or produced unexpected results.
Update this SKILL.md if a gap is found.
Report: skill([name]): no gaps identified -- if clean.
```

### Skill versioning:
- Increment version on every meaningful change
- Keep CHANGELOG section at bottom
- Never delete old behavior -- mark as DEPRECATED with date

---

## SECTION 7: THE ANTI-PATTERNS REGISTER

These patterns have caused real failures in this codebase. Never repeat them.

| Anti-Pattern | Failure Mode | Correct Pattern |
|---|---|---|
| "Follow standard protocol" | Agent invents its own | Embed the protocol verbatim |
| "Fix the issue" | Agent expands scope | "Fix ONLY line N of file X -- nothing else" |
| "Verify it works" | Agent self-certifies | List 3 specific tool calls with expected outputs |
| "See docs/X for details" | Agent skips or misreads | Embed the critical content inline |
| "Use best judgment" | Agent uses wrong judgment | Provide a decision tree with exit conditions |
| Soft failure modes ("might", "could", "try") | Agent treats failures as optional | Hard gates: EXIT 0 or STOP |
| Putting FORBIDDEN at the end | Agent acts before reading it | FORBIDDEN section is always SECOND block |
| Omitting the completion gate | Agent reports done prematurely | Completion gate is always the LAST section |
| CYC claim without audit output | Unverifiable, often wrong | Always run complexity_audit.py, embed output |
| Parallel subtasks without dependency check | Race conditions, artifact corruption | Sequential gates -- one subtask at a time |
| Referencing event_log.jsonl without lamport clock | Clock skew, ordering bugs | Always read last entry, increment, write |
| "Build it and push" in one instruction | No verification window | Build -> verify -> push are always 3 separate steps |

---

## SECTION 8: QUICK CHECKLIST FOR INSTRUCTION AUTHORS

Before finalizing any instruction (roleDefinition, start_subtask message, skill):

```
[ ] Does it start with the sequence (STEP 1, STEP 2...) before any context?
[ ] Is the FORBIDDEN block present and positioned early (second block)?
[ ] Are all tool calls named explicitly with parameters?
[ ] Are all constraints embedded inline (not referenced by document link)?
[ ] Is there a verification section using different tools from production?
[ ] Is there a structured output artifact specification?
[ ] Is there a completion gate (checklist) at the end?
[ ] Is there an escalation path for hard failures (STOP, not improvise)?
[ ] Are all failure modes handled as hard gates (not soft suggestions)?
[ ] Does the instruction survive being read by an agent with ZERO prior context?
```

If any box is unchecked: the instruction is not ready.

---

## CHANGELOG

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-07-06 | Initial protocol -- 7 laws, 6 sections, anti-patterns register |
