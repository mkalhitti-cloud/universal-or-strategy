---
name: gstack-registry
description: >
  Universal gstack toolkit registry for V12. Maps every gstack-adapted skill to
  its purpose, trigger phrases, and location so ALL agents (Antigravity, Claude,
  Codex, Gemini CLI, Jules, Droid) use the same toolbox.
  Source: garrytan/gstack (MIT License). V12-adapted versions live in .agent/skills/.
  Use this as the authoritative index when selecting a gstack skill.
triggers:
  - gstack
  - which skill should I use
  - what tools do we have
  - skill registry
  - agent toolkit
---

# V12 gstack Registry (Universal Agent Toolkit)

All agents operating in this repo (Antigravity, Claude, Codex, Gemini CLI, Jules, Droid)
MUST use this registry to select the right skill. Do NOT invent ad-hoc procedures when
a gstack-adapted skill already exists.

**Source**: garrytan/gstack (MIT License) -- V12-adapted for Windows/PowerShell/.NET 4.8

---

## Fully Adapted Skills (Production-Ready)

| Skill | Trigger | Agent | Location |
|-------|---------|-------|----------|
| `systematic-debugging` | Bug report, regression, "weird behavior", stack trace | P2 Forensics (any agent) | `.agent/skills/systematic-debugging/SKILL.md` |
| `cso` | Security audit, threat model, `/security:analyze`, OWASP | P1 Orchestrator / any agent | `.agent/skills/cso/SKILL.md` |
| `code-review` | PR review, diff review, `/review` | P4 Adjudicator / any agent | `.agent/skills/code-review/SKILL.md` |
| `memory-management` | Session memory, context across sessions, compaction | P1 Orchestrator | `.agent/skills/memory-management/SKILL.md` |

---

## How to Select a Skill

```
1. Is this a bug / regression?              -> systematic-debugging
2. Is this a security concern?              -> cso (or /security:analyze)
3. Is this a PR or diff review?             -> code-review
4. Do I need to persist context?            -> memory-management
5. Is this a structural design task?        -> architect SKILL.md (V12-native, not gstack)
6. Is this a workflow routing question?     -> nexus-bridge SKILL.md (V12-native)
```

---

## Agent-Specific Routing

| Agent | Primary gstack Skills |
|-------|-----------------------|
| Antigravity (P1) | `memory-management`, `cso`, `systematic-debugging` |
| Claude (P3 Architect) | `systematic-debugging` (evidence gathering only -- no fixes) |
| Codex (P5 Engineer) | `systematic-debugging` (Phase 4 implementation only) |
| Gemini CLI (P1 co-orch / P6 Validator) | `memory-management`, `systematic-debugging`, `cso` |
| Jules (P5 Engineer) | `systematic-debugging` (Phase 4 implementation only) |
| Droid (P2/P5) | `systematic-debugging`, `code-review` |

---

## gstack Adaptation Notes (V12-Specific Overrides)

All gstack skills have been V12-hardened with the following mandatory overrides:

1. **Lock audit**: Every skill that touches C# code runs `grep -r "lock(" src/` -- zero hits required.
2. **ASCII gate**: Every Phase 4 / post-edit step checks for non-ASCII in C# strings.
3. **deploy-sync.ps1**: Every skill that edits `src/` files ends with `powershell -File .\deploy-sync.ps1`.
4. **PowerShell only**: All shell commands use PowerShell syntax (not bash). Never use `grep -rP` without confirming PowerShell compatibility.
5. **No lock(stateLock)**: Any skill suggesting `lock()` blocks is in protocol violation.

---

## Expanding the Registry

When a new gstack plugin is adapted for V12:
1. Create `.agent/skills/<name>/SKILL.md` following the V12 adaptation pattern.
2. Add a row to the table above with trigger phrases and agent routing.
3. Add a post-use self-improvement audit section at the bottom of the SKILL.md.
4. Update `AGENTS.md` and `GEMINI.md` skill lists if the skill is broadly applicable.

**Source attribution**: Always include at the bottom of adapted SKILL.md files:
`*Source: gstack/<plugin-name> (MIT License, garrytan/gstack) adapted for Windows/PowerShell/NinjaScript/.NET 4.8*`

---

## Mandatory Self-Improvement Audit

After EVERY use:
1. Was a skill missing from the registry that an agent needed?
2. Was agent routing ambiguous for any task?
3. Did a V12 override conflict with the gstack base skill?

**If no gap:** `skill(gstack-registry): no gaps identified.`
**Commit format:** `skill(gstack-registry): [what was added/fixed and why]`
