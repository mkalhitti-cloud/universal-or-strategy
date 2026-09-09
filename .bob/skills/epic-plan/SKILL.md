---
name: epic-plan
description: Phase 2 - Dependency analysis and refactoring approach design for a V12 epic.
metadata:
  user-invocable: true
  disable-model-invocation: true
  argument-hint: <epic-slug>
---

# PHASE 2: EPIC PLAN
**Epic Slug:** $1
**Input:** docs/brain/$1/00-scope.md (from /epic-intake)
**Output:** docs/brain/$1/01-analysis.md + docs/brain/$1/02-approach.md
**Protocol:** V12 Photon Kernel -- Traycer-Parity Epic Workflow (Bob Edition)

> You are a Technical Architect who thoroughly analyzes and plans before executing.
> You do NOT touch src/ files in this phase.
> You produce TWO documents then STOP for Director approval before /epic-validate.

---

## ROLE & PHILOSOPHY
Good refactoring plans are grounded in reality. Analysis reveals what is actually there --
dependencies, risks, test coverage gaps. Only then can you make sound technical decisions.
Planning is where the thinking happens. Investing time in thorough planning produces better,
more controlled results.

Value system:
- Blast radius first -- know what you are affecting before deciding how to change it
- Surface risks early -- surprises during implementation are expensive
- Decisions need buy-in -- technical approach requires genuine alignment
- Constrain the implementation -- detailed architecture prevents unintended paths

---

## PART 1: ANALYSIS

### Step 1a -- Internalize Scope
Read docs/brain/$1/00-scope.md. Confirm you understand the agreed scope and boundaries.
If anything is unclear, ask the Director before proceeding.

### Step 1b -- Map Dependencies and Coupling
Using jCodemunch:
- `get_blast_radius` (depth: 2) on each target method -- who calls this code?
- `get_dependency_graph` (direction: both) -- what does this code call?
- `find_references` on any shared state, FSM fields, or collections touched by the target

Capture:
- Direct callers (files and methods that call the target)
- Indirect dependents (files that call the callers)
- Shared state or side effects (globals, events, FSM fields)
- API boundaries (public interfaces external code depends on)

### Step 1c -- Identify Risk Hotspots
Identify areas that need extra care in this epic:
- Core flows -- critical paths that must not break
- Concurrency -- threading, FSM state mutations, Enqueue paths
- ASCII compliance -- any string literals in the target scope
- Lock violations -- any existing lock() blocks in scope
- Complexity -- the actual CYC scores vs the < 20 target

### Step 1d -- Assess Test Coverage
- What test coverage exists for this code area?
- Which critical paths are tested vs untested?
- What is the gap between current coverage and what we need for safe refactoring?
  (Note: V12 NinjaTrader code is tested via F5 compile + live session. No unit test harness exists.)

### Step 1e -- Write Analysis Document
Produce `docs/brain/$1/01-analysis.md`:

```markdown
# Epic: $1 -- Refactoring Analysis

## Dependency Map
| Caller | File | How It Uses Target |
|--------|------|-------------------|
| ...    | ...  | ...               |

## Risk Hotspots
| Area | Risk | Why |
|------|------|-----|
| ...  | ...  | ... |

## Test Coverage
[Current state -- F5 compile gate + complexity_audit.py as primary verification]

## Change Surface Area
[Summary of what is affected by this refactoring]
```

**DO NOT propose implementation details in this document -- it is purely about current state.**

---

## PART 2: APPROACH

### Step 2a -- Identify Key Technical Decisions
Analyze the scope and identify the 3-5 key decisions that shape the refactoring.
For each decision, think through:
- What are the options?
- What are the trade-offs (simpler vs safer vs more elegant)?
- What does V12 DNA require?

V12-specific decision categories:
- **Structure:** How to decompose the God-method? (by concern, by flow, by guard clause?)
- **Extraction placement:** Same file (partial class) or new partial file?
- **LOC threshold:** Each extracted sub-method must be >= 15 LOC
- **Naming:** PascalCase verb-noun (Handle..., Process..., Validate..., Route...)
- **Transition:** Incremental extraction or full rewrite?

Present the key decisions to the Director with OPTIONS -- not open-ended asks.
Example: "Should we extract by flow (HandleOrderShortcuts, HandleUIShortcuts) or by guard
type (HandleInvalidStateGuard, HandleActiveTradeActions)? Here are the trade-offs: ..."

### Step 2b -- Draft Refactoring Approach Document
ONLY after Director alignment on decisions, produce `docs/brain/$1/02-approach.md`:

```markdown
# Epic: $1 -- Refactoring Approach

## 1. Key Decisions
### Decision: [name]
- Chosen approach: [what]
- Rationale: [why this over alternatives]
- Trade-offs: [what we gain / give up]
- V12 DNA impact: [how this aligns with DNA constraints]

## 2. Target State
[Concrete description of what "done" looks like]
- CYC scores after extraction: [list per method]
- Sub-methods to create: [list with names and responsibilities]
- File placement: [same file / new partial file]
- Residual God-method role: [dispatcher/router only, < 20 CYC]

## 3. Component Architecture (if new files needed)
[New partial class files, method signatures, call site changes]

## 4. Invariants (what MUST NOT change)
- External behavior: [list]
- FSM state transitions: [any that must be preserved]
- Signal names and order IDs: [must remain unchanged]
- deploy-sync.ps1 hard-link integrity: [mandatory after every edit]

## 5. V12 DNA Verification Plan
- complexity_audit.py: Run after each extraction to verify CYC < 20
- deploy-sync.ps1: Mandatory after every src/ edit
- grep lock( src/: Must return zero matches
- ASCII gate: Must PASS in deploy-sync output
- BUILD_TAG bump: Required in src/V12_002.cs after epic completion
```

---

## !! DIRECTOR APPROVAL GATE !!
**STOP HERE.** Present both documents (01-analysis.md and 02-approach.md).
Ask the Director:
- Does the approach match your intent?
- Are the key decisions aligned with how you want to refactor this?
- Are the invariants complete?

**Do NOT proceed to /epic-validate until the Director explicitly types: APPROVED**

Output: "[PLAN-GATE] Analysis and Approach documents complete. Awaiting Director approval."
