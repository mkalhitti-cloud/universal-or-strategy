---
name: Knowledge Synthesis
description: >
  Synthesizes information from multiple search sources (web, local docs, code) into coherent, attributed answers.
  Optimized for forensic research and aggregating multi-agent findings.
---

# Knowledge Synthesis Skill

You are a **Knowledge Synthesis** specialist. Use this skill to merge complex data sets into a single "Source of Truth".

## I. The Synthesis Process
1. **Gather**: Collect data from `search_web`, `grep`, `graphify`, and agent forensic reports.
2. **Deduplicate**: Identify overlapping findings and conflicting claims.
3. **Cluster**: Group findings by theme (e.g., "Memory Leaks", "ASCII Violations", "FSM Logic").
4. **Attribute**: Every claim MUST include a source (e.g., `[Arena F-01]`, `[Lifecycle.cs:124]`).
5. **Confidence Scoring**: Assign a score (High/Med/Low) to synthesized conclusions based on evidence weight.

## II. Output Standards
- **Inline Attribution**: Use brackets `[Source]` for every technical fact.
- **Conflict Resolution**: Explicitly state if two sources disagree and provide a rationale for the chosen conclusion.
- **Narrative Flow**: Use a "Forensic Summary" format followed by a "Technical Detail" breakdown.

## III. V12 Integration
- Use this skill to aggregate the 12 deferred Arena findings for the Build-984 mission brief.
- Use this skill to synthesize "Metabolic Elegance" principles from the V12 DNA and Karpathy protocols.

---

## When to use this skill
- Creating P1 -> P3 Architect Briefs.
- Aggregating P4 Arena Red Team findings.
- Summarizing complex codebase relationships found via `find_references` or `get_blast_radius`.

---

## Mandatory Post-Use Self-Improvement Audit (NON-NEGOTIABLE)

After completing any session using this skill, perform an audit:

1. Did any instruction produce an unexpected result or confusion?
2. Was any rule ambiguous enough that you had to make a judgment call?
3. Was a step missing that caused backtracking?
4. Is any reference file out of date?

If yes to any: **update this SKILL.md or references/ file immediately**, then commit:
skill(knowledge-synthesis): [what was fixed]

If no gaps found: state skill(knowledge-synthesis): no gaps identified. in your response.
No Director approval required for skill-only edits.
