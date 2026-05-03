---
name: repo-intake-and-plan
description: >
  Onboard a new or unfamiliar repository by systematically reading its structure, key files,
  and architecture — then produce a concise brief and implementation plan. Use this when 
  starting work in a fresh codebase, after a major refactor, or when a new agent needs to 
  be oriented to the V12 project. 
  Keywords: repo intake, onboarding, codebase analysis, architecture brief, implementation plan.
---

# Repo Intake and Plan Skill

## Overview

Before any agent can contribute to a codebase effectively, it must develop a working mental model of the repository. This skill defines a systematic intake process that produces a concise architecture brief and readiness assessment.

## Phase 1: Structure Scan

Run these in order, parallelizing where possible:

```
1. List root directory (1 level)
2. Read README.md (or equivalent)
3. Read package.json / *.csproj / go.mod / requirements.txt (dependency manifest)
4. Read .gitignore, .env.example (environment shape)
5. Identify entry points (main.cs, index.ts, app.py, Program.cs, etc.)
```

**Output**: Directory tree + dependency list + environment variables required.

## Phase 2: Architecture Read

Prioritize reading in this order:

1. **docs/** or **docs/brain/** — existing architecture docs
2. **src/** top-level structure — identify layers (domain, infra, presentation)
3. **Key abstractions** — base classes, interfaces, shared types
4. **Configuration files** — appsettings.json, config.ts, etc.
5. **Test structure** — what's tested, test conventions

**Output**: Architecture summary (max 500 words) covering:

- What the system does
- Core components and their responsibilities
- Data flows (producer → consumer pattern)
- Key constraints (performance, compliance, licensing)

## Phase 3: Gap & Risk Assessment

After reading, answer these questions:
| Question | Why It Matters |
|----------|---------------|
| Are there missing tests for core paths? | Regression risk |
| Is documentation current with the code? | Onboarding risk |
| Are there TODOs/FIXMEs signaling known debt? | Hidden complexity |
| Is the build reproducible (lock files, pinned versions)? | CI/CD risk |
| Are secrets properly managed? | Security risk |

## Phase 4: Implementation Plan Stub

Produce a brief implementation_plan.md stub containing:

```markdown
# [Task Name] — Intake Brief

## Repository: [name]

## Entry Point: [file]

## Primary Language: [C#/TS/Python]

## Architecture Summary

[2-3 sentences on what the system does and how it's structured]

## Key Files

- [path]: [role]

## Environment Requirements

- [VAR_NAME]: [description]

## Gaps Identified

- [gap 1]
- [gap 2]

## Recommended First Steps

1. [action]
2. [action]
```

## V12 Project-Specific Intake Checklist

When onboarding to the V12 Universal OR Strategy project:

- [ ] Read `GEMINI.md` (project-wide standards)
- [ ] Read `docs/brain/nexus_a2a.json` (A2A bridge state)
- [ ] Read `docs/brain/implementation_plan.md` (current approved plan)
- [ ] Read `.agent/skills/` directory listing (available skills)
- [ ] Read `.agent/workflows/` directory listing (available workflows)
- [ ] Check `src/` for any `.bak` or `.log` files (Repo Hygiene gate)
- [ ] Verify `deploy-sync.ps1` exists and is current
- [ ] Verify `scripts/amal_harness.py` exists for AMAL gate

## Anti-Patterns

- **Don't start coding without intake** — always run Phase 1-3 first
- **Don't read every file** — prioritize entry points, key abstractions, docs
- **Don't skip the gap assessment** — unknown debt causes regressions

## Post-Use Audit

After every use:

1. Was the architecture brief produced before any implementation started?
2. Were V12-specific files checked (GEMINI.md, nexus_a2a.json)?
3. Were gaps documented in the implementation plan stub?

State: `skill(repo-intake-and-plan): no gaps identified.` or document fix.
