# Rules Catalog Compliance Gate (V12.36) — HARD BLOCKER

## Status: P0 — ALL WORK STOPS IMMEDIATELY ON VIOLATION

This is not a warning. This is not a suggestion.
**If `docs/standards/jane-street/RULES_CATALOG.md` compliance is not 100%, ALL agent work stops.**

---

## The Rule

Before ANY task begins — planning, coding, reviewing, spec writing, anything — the agent MUST:

1. Confirm `docs/standards/jane-street/RULES_CATALOG.md` is UTF-8 clean and readable.
2. Confirm the task does not introduce a P0 or P1 violation from the Rules Catalog.
3. If a violation exists in the current codebase scope: **STOP and report it. Do not proceed.**

**There is no exception. There is no "I'll fix it after". Work stops. The violation is fixed first.**

---

## Why This Exists

Agents were reading a garbled UTF-16 version of RULES_CATALOG.md and silently proceeding with
incomplete rule knowledge. This caused architectural drift that cost real money to diagnose and fix.
The rule is now: zero tolerance. The catalog is the contract. The contract must be 100% intact
before any money is spent on tokens executing work.

---

## P0 Rules — Instant Work Stopper

Any of these found in new or modified code = **HARD STOP, no exceptions**:

| Rule ID | Description | Pattern |
|---------|-------------|---------|
| JS-021 | `lock()` anywhere in src/ | `lock\s*\(` |
| JS-001 | `throw new XxxException` in hot paths | `throw\s+new\s+\w+Exception\(` |
| JS-002 | `return null` for missing values | `return\s+null\s*;` |
| JS-010 | Public constructors without smart constructor | public constructor + no factory |
| JS-015 | Unvalidated string types crossing boundaries | raw string params without parse |
| JS-033 | `async void` (non-event-handler) | `async\s+void\s+\w+\(` |
| JS-036 | `new byte[]` heap allocation in hot path | `byte\[\]\s*=\s*new\s+byte\[` |
| JS-037 | `new T[]` without ArrayPool in hot path | `new\s+\w+\[\d+\]` without ArrayPool |

---

## P0 Verification — Agent MUST Run Before Any Commit

```powershell
# P0 lock() check — must return 0 results
grep -r "lock(" src/ --include="*.cs"

# P0 async void check
grep -rn "async void " src/ --include="*.cs"

# P0 return null check (hot paths)
grep -rn "return null;" src/ --include="*.cs"
```

If ANY of the above return results in new or modified files: **STOP. Fix first. Then proceed.**

---

## The Gate Protocol

Every agent, every mode, every task MUST begin with:

```
STEP 0 — RULES CATALOG GATE (mandatory, non-skippable):
  [ ] Read docs/standards/jane-street/RULES_CATALOG.md (it is now UTF-8 clean)
  [ ] Identify which JS-XXX rules apply to this task's scope
  [ ] Confirm zero P0 violations in files this task will touch
  [ ] If P0 violation found → STOP. Report violation ID + file + line. Do not proceed.
  [ ] If catalog is unreadable → STOP. Fix encoding. Do not proceed.
  GATE RESULT: PASS or BLOCKED(JS-XXX at file:line)
```

Only a PASS result allows the task to continue.

---

## Enforcement Layers

| Layer | Mechanism | Blocks? |
|-------|-----------|---------|
| Hook | `.bob/hooks/pre_task_rules_gate.py` runs at task start | YES — exits 1 on P0 violation |
| Rule | This file — loaded into every agent session | YES — agent MUST stop |
| Custom modes | `roleDefinition` includes full OKF rules block | YES — agent is instructed to stop |
| AGENTS.md | Section 2 Platinum Standard references this gate | YES — project mandate |

---

## What Agents Report When Blocked

```
=== RULES CATALOG GATE: BLOCKED ===
Violation: JS-021 (lock() usage — P0 CRITICAL)
File: src/V12_002.SIMA.Lifecycle.cs
Line: 847
Pattern: lock (_stateLock)
Action Required: Replace with Actor/Enqueue pattern per lock-free-patterns.md
Work Status: STOPPED. No further execution until violation is resolved.
====================================
```

The agent outputs this block, stops, and waits for director resolution.
It does NOT auto-fix. It does NOT continue past the violation. It stops.

---

## Cost Justification

A session that proceeds on broken rules wastes 100% of its tokens producing non-compliant output
that must be thrown away. A gate that costs 2 read_file calls saves the entire session budget.
The math is simple: gate runs first, or the session is worthless.

---

**Effective**: 2026-07-07 (V12.36)
**Supersedes**: Advisory-only OKF Knowledge Protocol (04-okf-knowledge-protocol.md)
**Authority**: Director mandate — no agent may override this rule
