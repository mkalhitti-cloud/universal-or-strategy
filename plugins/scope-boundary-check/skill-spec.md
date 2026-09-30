# Skill Specification: scope-boundary-check

## Skill Name
scope-boundary-check

## Description
Phase 1.5 mandatory gate between intake and planning to prevent scope creep. Creates pattern analysis document, enhances scope boundaries with WHY explanations, and documents anti-patterns. Enforces V12.23 No Scope Creep Protocol. Use after /epic-intake and before /epic-plan.

## When to Use
- After completing Phase 1 (Intake) of any V12 refactoring epic
- Before starting Phase 2 (Planning)
- When scope boundaries need explicit documentation
- To prevent "while we're here" scope creep

## Inputs
- Epic slug (e.g., "EPIC-POSINFO")
- Existing `docs/brain/EPIC-[slug]/00-scope.md` file
- Source code files identified in scope document

## Outputs
1. `docs/brain/EPIC-[slug]/01-pattern-analysis.md` - Pattern analysis with code examples
2. Enhanced `docs/brain/EPIC-[slug]/00-scope.md` - With boundaries explained
3. `docs/standards/REFACTORING_ANTIPATTERNS.md` - Anti-patterns guide (if not exists)
4. [SCOPE-BOUNDARY-GATE] checkpoint in scope document

## Steps

### Step 1: Read Source Code
- Read the source files identified in 00-scope.md
- Extract actual code patterns (not pseudocode)
- Count lines, methods, complexity metrics

### Step 2: Create Pattern Analysis
- Document pattern name (descriptive, specific)
- Quantitative metrics (lines, CYC, methods affected)
- Actual code examples from source
- List anti-patterns with WHY explanations
- Defer approach to Phase 2

### Step 3: Enhance Scope Document
- Add "Scope Boundaries Explained" section
- For each OUT OF SCOPE item, add:
  - WHY explanation
  - Code example of what NOT to do
  - Impact analysis
  - Link to separate epic if needed
- Add "Anti-Patterns" section
- Add "Success Criteria" section with anti-success criteria

### Step 4: Verify Anti-Patterns Guide
- Check if `docs/standards/REFACTORING_ANTIPATTERNS.md` exists
- Create if missing
- Ensure it covers: allocation, scope creep, complexity, locks, style

### Step 5: Add Gate Checkpoint
- Add [SCOPE-BOUNDARY-GATE] section to scope document
- List Director confirmation questions
- STOP - do not proceed to Phase 2

## Success Criteria
- Pattern analysis has quantitative metrics
- All OUT OF SCOPE items have WHY explanations
- Code examples are actual code (not pseudocode)
- Anti-patterns documented with reasons
- Gate checkpoint added
- No Phase 2 work started

## Example
See EPIC-POSINFO:
- `docs/brain/EPIC-POSINFO/01-pattern-analysis.md`
- `docs/brain/EPIC-POSINFO/00-scope.md` (enhanced)
- `docs/standards/REFACTORING_ANTIPATTERNS.md`

## References
- V12.23 No Scope Creep Protocol
- EPIC-13 PR #12 failure analysis
- Jane Street Intel