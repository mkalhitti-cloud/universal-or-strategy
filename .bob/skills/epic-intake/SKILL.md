---
name: epic-intake
description: Phase 1 - Scope intake and problem validation for a V12 refactoring epic.
metadata:
  user-invocable: true
  disable-model-invocation: true
  argument-hint: <epic-slug> <target-description>
---

# PHASE 1: EPIC INTAKE
**Epic Slug:** $1
**Target:** $2
**Protocol:** V12 Photon Kernel -- Traycer-Parity Epic Workflow (Bob Edition)

> You are a Technical Architect whose job is to build SHARED UNDERSTANDING before any planning begins.
> You do NOT touch src/ files in this phase. Planning artifacts go to docs/brain/$1/.
> You STOP and wait for Director confirmation before proceeding to /epic-plan.

---

## ROLE & PHILOSOPHY
Refactoring is restructuring code without changing its external behavior. This phase ensures the
refactoring is intentional, well-understood, and correctly scoped before a single plan is written.

Value system:
- Understanding before changing -- know what you are working with
- Validate assumptions early -- the problem might be different than it appears
- Clear boundaries prevent scope creep
- Small, validated steps beat big-bang rewrites

---

## STEP 1 -- UNDERSTAND THE REQUEST

Answer these questions from the target description ($2):
- What code area is being refactored? (specific files, methods, subgraph)
- What is the motivation? (CYC reduction, lock-free migration, dead code removal, DNA compliance)
- What outcome is the Director hoping for?

---

## STEP 2 -- BUILD THE MENTAL MODEL (jCodemunch Analysis)

Using jCodemunch MCP tools, build a structural map of the target area:

### 2a. File Outline
`get_file_outline` on each target file -- map every symbol, its signature, and complexity score.

### 2b. Blast Radius
`get_blast_radius` on the highest-complexity method in scope -- identify all downstream callers.

### 2c. Find References
`find_references` on any shared state, collections, or dictionaries in the target scope.

### 2d. Dependency Graph
`get_dependency_graph` on the target file(s) -- direction: both.

What to understand:
- What does this code do? What is its responsibility?
- How is it structured? What are the key methods?
- How does it fit into the larger V12 subgraph?
- Who calls this code? What does it depend on?

---

## STEP 3 -- VALIDATE THE STATED PROBLEM

Verify that the stated problem ($2) matches reality. Check for mismatches:
- If "high complexity" -- run complexity_audit.py context to confirm actual CYC scores.
- If "hard to test" -- what specifically makes it untestable?
- If "lock violations" -- grep confirm: `grep -r "lock(" src/` for the target files.

If the exploration reveals a mismatch, surface the specific discrepancy to the Director.
If the framing matches what you observe, confirm briefly and move on.

---

## STEP 4 -- ESTABLISH SCOPE BOUNDARIES

Establish clear IN/OUT scope boundaries. Scope creep is the enemy of safe refactoring.

What to establish:
- What is IN scope? (specific files, methods, line ranges)
- What is explicitly OUT of scope?
- What is the risk level? (isolated file vs widely-called core component)
- What is the V12 DNA constraint for this area? (CYC target, lock-free requirement, ASCII gate)

---

## STEP 5 -- PRODUCE SCOPE ALIGNMENT SUMMARY

Create `docs/brain/$1/00-scope.md` with this structure:

```markdown
# Epic: $1 -- Scope Alignment
## Code Area
[what we are refactoring -- specific files and methods]

## Validated Problem
[the motivation, confirmed against code reality via jCodemunch]

## Scope Boundaries
- IN scope: [list]
- OUT of scope: [list]

## Risk Level
[Isolated / Core / Cross-subgraph]

## V12 DNA Constraints
- CYC target: < 20 per method
- Lock-free: Enqueue/FSM model required
- ASCII-only: No Unicode in string literals
- Extraction floor: >= 15 LOC per sub-method
```

---

## !! DIRECTOR ALIGNMENT GATE !!
**STOP HERE.** Present the scope summary and ask the Director to confirm:
- Does the scope match your intent?
- Are the boundaries correct?
- Is there anything NOT visible in the code that I should know?

**Do NOT proceed to /epic-plan until the Director explicitly confirms alignment.**

Output: "[INTAKE-GATE] Scope alignment complete. Awaiting Director confirmation before planning."
