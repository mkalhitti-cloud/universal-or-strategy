---
description: Standard P1->P3 intake template -- Antigravity sends forensic findings to Claude (ARCHITECT) for structural repair design
---

Use this workflow when FORENSICS (P2) has produced a "Logical Proof of Failure" and the ARCHITECT (Claude) must design the structural repair.
The ORCHESTRATOR (Antigravity) runs this workflow. The ARCHITECT MUST NOT receive prescriptive implementation paths — only evidence.

---

## Phase 1: Package the Forensic Brief

1. **Read the Forensic Report** from CODEX/FORENSICS output or `docs/brain/forensic_*.md`.
2. **Context Enrichment (MANDATORY)**:
   - Use **Linear MCP** to fetch the corresponding issue context (`get_issue`).
   - Use **jCodeMunch MCP** (`get_context_bundle`, `get_blast_radius`) to query the **Graphify Knowledge Layer** and extract architectural dependencies related to the broken files.
3. **Extract evidence only** — DO NOT interpret or propose solutions. The Orchestrator is BANNED from prescribing implementation paths.
4. Build the Architect Brief with:
   - **Logical Proof of Failure**: exact code path, log trace, or state sequence that proves the bug
   - **Observed vs Expected**: what the system did vs what it should do
   - **Constraints**: any permanent DNA rules (no locks, Enqueue model, Build 981 direct-write, etc.)
   - **Scope boundary**: which files are in-scope for repair

---

## Phase 2: Handoff to Claude (ARCHITECT)

Send Claude the following structured block (copy-paste ready), following the **$claudecloud Standard** defined in `.agent/skills/architect/SKILL.md`:

```markdown
You are the V12 ARCHITECT (Claude). You are in PLAN-ONLY mode. Do NOT edit src/ files.
Produce the complete content of docs/brain/implementation_plan.md. The Director will commit it.

MISSION: [Mission Name]
BUILD_TAG: [tag]
REPO: https://github.com/mkalhitti-cloud/universal-or-strategy
BRANCH: [branch_name]

=== STEP 1: READ THESE CONTEXT FILES ===

[List context files as raw.githubusercontent.com URLs]

=== STEP 2: READ SOURCE FILES ===

[List source files with line numbers and raw.githubusercontent.com URLs]

=== KNOWN FACTS (do not re-derive these) ===

- [Fact 1]
- [Fact 2]

=== THE [PATTERN_NAME] PATTERN (apply this pattern) ===

BROKEN:
    [Broken Code Snippet]

FIXED:
    [Fixed Code Snippet]

=== OUTPUT REQUIREMENTS ===

Write the complete docs/brain/implementation_plan.md containing:

1. Header block with BUILD_TAG, date, author (ARCHITECT), phase name.
2. For EACH violation/site:
   a. File path and exact line number
   b. The exact BROKEN code block copied from the source file
   c. The exact FIXED code block
   d. One-line verification grep the Engineer can run to confirm the fix
3. A Director's Handoff Block at the end formatted as a code block for the Engineer.

=== HARD CONSTRAINTS ===

- Zero lock(stateLock) in any new code
- ASCII-only strings -- no Unicode, curly quotes, em-dashes, or arrows
- Minimal surgical change
```

---

## Phase 3: Architect Review

1. Claude produces `docs/brain/implementation_plan.md` with fully embedded code blocks.
2. **CHECKPOINT (Morpheus Gate):** Verify Claude's response includes the line:
   `"Pre-Handoff Validation: [tool used] -- [result]"`
   If this line is absent, the plan has NOT passed the testing gate. Do NOT route to P4. Reject the response and prompt Claude to provide the validation result.
3. Antigravity reviews the plan for:
   - Compliance with permanent DNA rules
   - Correct handoff block for ENGINEER
4. Update `nexus_a2a.json.p3_handoff.ready_for_p4 = true` and `plan_validated = true`.
5. Present plan to Director for approval. Director is the ONLY entity that can authorize implementation.

---

## Phase 4: Route to Adjudicator (P4 Audit)

Once Claude (P3) produces the plan, route to ADJUDICATOR (Arena) for P4 Audit.
Approval from the Director is required to _proceed to P4_, and P4 consensus is required to _proceed to P5_.

---

## Phase 5: Route to Engineer

Once P4 Audit clears: route to ENGINEER (Codex/Jules) via `/nexus-relay` (NEVER `/agent_as_tool` -- that is for single discrete tasks only, not full mission handoff).

---

## Post-Use Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

1. **Was the evidence packaging ambiguous?** Tighten the brief template.
2. **Did Claude receive any prescriptive hints?** Remove them -- Architect must reason independently.
3. **Was the handoff block incomplete?** Fix the template.
4. **Did Director need to ask clarifying questions?** Add those answers to the brief template.
5. **Was the Morpheus Gate checkpoint (Phase 3 step 2) triggered?** Confirm Claude's validation line was present.

**If no gap found, state:** `workflow(architect_intake): no gaps identified -- workflow correct as written.`

Skipping the audit is a protocol violation. No Director approval needed for self-improvement edits.

**Commit format:** `workflow(architect_intake): [what was fixed and why]`
