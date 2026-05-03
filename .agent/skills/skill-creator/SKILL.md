---
name: Skill Creator
description: >
  Creates, improves, and iterates on agent skills (.agent/skills/**/ SKILL.md files) for the
  V12 Universal OR Strategy project. Use this skill whenever an agent wants to capture a
  workflow as a reusable skill, improve an existing SKILL.md, update a skill's reference files,
  or add skill self-improvement capability to an agent. 
  Make sure to use this skill whenever the user says "create a skill", "improve this skill", 
  "capture this as a skill", "update the skill", or any agent identifies a repeated workflow 
  that should be formalized. All agents (Antigravity, Claude, Codex, Gemini/Jules) can use 
  this skill to self-improve.
---

# Skill Creator (V12.16 Standard)

Use this skill to create, test, and iterate on agent skills stored in `.agent/skills/`.

## Skill Creation Workflow

### 1. Capture Intent & Research

- **Workflow**: What repeated steps or logic are being captured?
- **Triggers**: When should this skill activate? Be "pushy" in the description to prevent undertriggering.
- **Output**: What is the expected format?
- **Research**: Check project DNA (`GEMINI.md`, `CLAUDE.md`) and existing skills to ensure alignment.

### 2. Write the SKILL.md

- **Frontmatter**: Mandatory `name` and `description`.
- **Description**: Include specific trigger contexts. (e.g., "Use this whenever the user mentions dashboards, even if they don't say 'dashboard'").
- **Instructions**: Use imperative, clear steps.
- **Reference Files**: Move heavy lookup tables or templates to `references/` directory.

### 3. Verification & Test Cases

- For verifiable outputs (code, data, fixed workflows), define 2-3 test cases.
- Evaluate the skill by running these cases and refining the instructions until success.

### 4. Propagation

- Update `GEMINI.md`, `CLAUDE.md`, `CODEX.md`, and `JULES.md` to reference the new skill.
- Ensure all agents understand their role in triggering it.

## Mandatory Self-Improvement Audit

After EVERY skill use, perform this audit:

1. **Instruction Clarity**: Did any step produce an unexpected or ambiguous result?
2. **Trigger Coverage**: Is the description "pushy" enough to capture all relevant contexts?
3. **Template Accuracy**: Are the output templates up to date?
4. **Gap Analysis**: If a gap is found, fix it immediately. Otherwise, state: `skill(name): no gaps identified`.

**Commit format**: `skill(name): [details of improvement]`
