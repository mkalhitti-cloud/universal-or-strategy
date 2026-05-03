---
name: self-improving-agent
description: >
  Pattern for building and running self-improving AI agents that can refine their own instructions,
  skills, and workflows based on outcomes. Use this when designing agents that learn from their
  own outputs, iterate on prompt quality, or improve skill SKILL.md files autonomously.
  Keywords: self-improvement, meta-learning, agent loop, skill refinement, prompt optimization.
---

# Self-Improving Agent Skill

## Overview

A self-improving agent is one that can critique its own outputs, identify systematic gaps, and update its own instructions or skills to perform better in future runs. This is the backbone of the V12 MANDATORY SELF-IMPROVEMENT PILLAR.

## Core Loop

```
Generate Output → Evaluate Against Criteria → Identify Gaps → Update Instructions → Re-run
```

### Phase 1: Generate

Run the skill/task as normal. Produce a concrete output artifact.

### Phase 2: Evaluate

After completion, perform a **structured self-audit**:

- Did output meet stated success criteria?
- Were any instructions ambiguous or counterproductive?
- Did any edge case produce unexpected behavior?
- What would an adversarial reviewer say?

### Phase 3: Identify Gaps

Classify defects:
| Type | Example |
|------|---------|
| Missing Instruction | "Skill didn't say what to do when API returns 429" |
| Ambiguous Guidance | "Bold was not defined — led to 32px font everywhere" |
| Incorrect Assumption | "Assumed dark mode — client wanted light" |
| Process Gap | "No validation step before committing" |

### Phase 4: Update Instructions

For **skill gaps**: edit the relevant `SKILL.md` file directly.
For **workflow gaps**: update the relevant workflow `.md` file.
For **protocol gaps**: escalate to Director for `GEMINI.md` / `MEMORY` update.

**Commit message format**: `skill(name): [what was fixed]`

### Phase 5: Re-run (if applicable)

Re-apply improved instructions to the same task or mark for next occurrence.

## V12 Protocol Alignment

Per the BMad V12 Permanent DNA:

> All agents MUST perform a post-use audit after every skill use:
>
> 1. Check if any instruction was ambiguous or produced an unexpected result.
> 2. Update the skill's SKILL.md or references/ files if a gap is found.
> 3. State `skill(name): no gaps identified.` if no gap found.

This skill formalizes that requirement into a repeatable pattern.

## Agent Architecture Patterns

### Single-Agent Loop

```
Agent -> Task -> Audit -> Self-Edit SKILL.md -> Next Task
```

Best for: skill refinement, prompt optimization, single-domain improvement.

### Multi-Agent Critic Loop (`/loop_critic` workflow)

```
Engineer -> Output -> Architect Critique -> Engineers Revises -> Loop until SIGN-OFF
```

Best for: code quality, architectural compliance, adversarial hardening.

### Hierarchical Meta-Loop

```
Orchestrator -> Aggregate Audit Logs -> Identify Cross-Skill Patterns -> Director Escalation
```

Best for: protocol-level improvements, GEMINI.md / MEMORY updates.

## Implementation Checklist

- [ ] Define clear, measurable success criteria **before** running the task.
- [ ] Capture the output artifact for audit reference.
- [ ] Run the 4-phase audit loop after every completion.
- [ ] If a gap is found: edit SKILL.md; commit with `skill(name): [fix]`.
- [ ] If no gaps: log `skill(self-improving-agent): no gaps identified.`

## Post-Use Audit

After every use of this skill:

1. Was the audit loop actually run, or skipped? (Skipping = protocol violation)
2. Were gaps correctly classified and fixed in the right file?
3. Was the commit message formatted correctly?

State: `skill(self-improving-agent): no gaps identified.` or document fix.
