name: scope-boundary-check
description: Phase 1.5 mandatory gate between intake and planning to prevent scope creep. Creates pattern analysis, enhances scope boundaries with WHY explanations, documents anti-patterns. Enforces V12.23 No Scope Creep Protocol.

---

# Scope Boundary Check Skill

Phase 1.5 mandatory checkpoint between intake and planning phases of V12 refactoring epics.

## Purpose

Prevents scope creep by forcing explicit documentation of:
- Pattern analysis with quantitative metrics
- Anti-patterns with WHY explanations  
- Boundary explanations for OUT OF SCOPE items
- Success and anti-success criteria

## When to Use

**Mandatory for:**
- All refactoring epics (EPIC-*)
- Any epic touching src/ files
- Epics with scope ambiguity

**Optional for:**
- Pure documentation epics
- Infrastructure changes
- Trivial fixes (<10 lines)

## Protocol

### 1. Create Pattern Analysis

Create `docs/brain/EPIC-[slug]/01-pattern-analysis.md`:

```markdown
# Pattern Analysis: [Descriptive Pattern Name]

## Pattern Name
"[Name]" - [Brief description]

## Duplication Metrics
- Methods affected: [N]
- Lines of duplication: [N] total
- Cyclomatic complexity: CYC=[N]
- CodeScene score: [Current]

## Pattern Structure
[Actual code examples from source]

## Anti-Patterns to Avoid
1. **[Name]** - [Why banned]
2. **[Name]** - [Why banned]

## Recommended Approach
[Defer to Phase 2]
```

### 2. Enhance Scope Document

Add to `docs/brain/EPIC-[slug]/00-scope.md`:

```markdown
## Scope Boundaries Explained

### Why [Item] is OUT OF SCOPE
**Reason**: [Specific reason]
**Impact if changed**: [Analysis]
**Requires separate epic**: EPIC-[NAME]

## Anti-Patterns (What NOT to Do)
[Code examples with WHY explanations]

## Success Criteria
### Functional Requirements
### Quality Metrics  
### Anti-Success Criteria (Failure Modes)
```

### 3. Verify Anti-Patterns Guide

Ensure `docs/standards/REFACTORING_ANTIPATTERNS.md` exists with:
- Allocation anti-patterns (arrays, dictionaries, reflection, LINQ)
- Scope creep anti-patterns ("while we're here" fixes)
- Complexity anti-patterns (clever abstractions)
- Lock-based anti-patterns (V12 DNA violations)
- Style anti-patterns (Unicode, missing braces)

### 4. Add Gate Checkpoint

```markdown
## [SCOPE-BOUNDARY-GATE] Director Confirmation Required

**Questions**:
1. Are pattern boundaries clear?
2. Are anti-patterns comprehensive?
3. Anything not visible in code I should know?
4. Proceed to Phase 2 Planning?

**Next Step**: Awaiting Director approval.
```

## Outputs

**Required:**
1. `docs/brain/EPIC-[slug]/01-pattern-analysis.md` (NEW)
2. `docs/brain/EPIC-[slug]/00-scope.md` (ENHANCED)
3. `docs/standards/REFACTORING_ANTIPATTERNS.md` (VERIFY)

**Optional:**
4. `docs/brain/EPIC-[slug]/phase1.5-complete.md` (SUMMARY)

## Validation Checklist

- [ ] Pattern name is specific (not "Duplication Pattern")
- [ ] Metrics are quantitative (not "lots" or "many")
- [ ] Code examples are ACTUAL code (not pseudocode)
- [ ] Every OUT OF SCOPE item has WHY explanation
- [ ] Anti-patterns have code examples + reasons
- [ ] Success AND anti-success criteria defined
- [ ] [SCOPE-BOUNDARY-GATE] checkpoint added
- [ ] No Phase 2 work started

## Example

See EPIC-POSINFO implementation:
- [`docs/brain/EPIC-POSINFO/01-pattern-analysis.md`](../../docs/brain/EPIC-POSINFO/01-pattern-analysis.md)
- [`docs/brain/EPIC-POSINFO/00-scope.md`](../../docs/brain/EPIC-POSINFO/00-scope.md)
- [`docs/standards/REFACTORING_ANTIPATTERNS.md`](../../docs/standards/REFACTORING_ANTIPATTERNS.md)

## Common Mistakes

### ❌ Vague Pattern Names
```markdown
# Pattern Analysis: Duplication Pattern
```
**Fix:** Be specific - "Switch-Based Field Accessor Pattern"

### ❌ Missing WHY Explanations
```markdown
### OUT OF SCOPE
- PositionInfo struct
```
**Fix:** Add reason, impact, code example

### ❌ Qualitative Metrics
```markdown
- Methods affected: Several
- Lines: Lots
```
**Fix:** Use numbers - "6 methods", "72 lines"

### ❌ Pseudocode Examples
**Fix:** Use actual code from source files

### ❌ Proceeding Without Approval
**Fix:** STOP at [SCOPE-BOUNDARY-GATE]

## Integration

Updated epic workflow (5 phases):
1. Phase 1: Intake → [INTAKE-GATE]
2. **Phase 1.5: Scope Boundary → [SCOPE-BOUNDARY-GATE]**
3. Phase 2: Planning → [PLAN-GATE]
4. Phase 3: Validation → [VALIDATE-GATE]
5. Phase 4: Tickets → [TICKETS-GATE]

## Post-Use Audit (MANDATORY - Anthropic Skill-Creator Protocol)

**All agents MUST perform this audit after EVERY use of this skill:**

### Audit Checklist

1. **Ambiguity Check**: Were any instructions unclear or produce unexpected results?
   - Was pattern analysis created with quantitative metrics? (Verify numbers, not "lots"/"many")
   - Did pattern name avoid vague terms? (Not "Duplication Pattern", but specific like "Switch-Based Field Accessor")
   - Were all OUT OF SCOPE items explained with WHY? (Reason + impact + code example)
   - Did anti-patterns include actual code examples? (Not pseudocode)
   - Were success AND anti-success criteria defined? (Both required)
   - Was [SCOPE-BOUNDARY-GATE] checkpoint added? (Mandatory gate)
   - Did agent STOP at gate without proceeding to Phase 2? (Critical)

2. **Gap Detection**: If ANY instruction was ambiguous or produced unexpected results:
   - Document the gap in this SKILL.md immediately
   - Add the quirk to the Common Mistakes section
   - Update version history with the fix

3. **Audit Statement**: If no gaps found, state:
   ```
   skill(scope-boundary-check): no gaps identified
   ```

4. **Protocol Violation**: Skipping this audit is a V12 protocol violation.

### Known Quirks (Updated During Audits)

- **Quantitative Metrics (2026-06-08)**: "Several methods" is NOT quantitative - must be "6 methods", "72 lines", etc.
- **Pattern Names (2026-06-08)**: Generic names like "Duplication Pattern" fail validation - must be specific
- **Gate Enforcement (2026-06-08)**: Agents often skip [SCOPE-BOUNDARY-GATE] - MUST stop and wait for Director approval
- **Pseudocode Ban (2026-06-08)**: Code examples MUST be actual source code, not simplified pseudocode

## V12 DNA Alignment

- **Correctness by Construction**: Mandatory gate prevents scope creep before it starts
- **ASCII-Only**: All documentation must be ASCII-safe
- **Jane Street Alignment**: Explicit boundaries reduce cognitive load during implementation
- **Karpathy Protocol**: Explicit success criteria (quantitative metrics, specific names, WHY explanations)

## References

- V12.23 No Scope Creep Protocol
- EPIC-13 PR #12 failure analysis
- Jane Street Intel
- `.bob/custom_modes.yaml` (v12-epic-planner)

---

**Last Updated**: 2026-06-08
**Maintainer**: Gemini CLI (Advanced Mode)
**Status**: ✅ Active - Converted to self-improving format