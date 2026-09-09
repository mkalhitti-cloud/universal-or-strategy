---
name: epic-validate
description: Phase 3 - Stress-test the refactoring approach before ticket breakdown.
metadata:
  user-invocable: true
  disable-model-invocation: true
  argument-hint: <epic-slug>
---

# PHASE 3: EPIC VALIDATE
**Epic Slug:** $1
**Input:** docs/brain/$1/01-analysis.md + docs/brain/$1/02-approach.md
**Protocol:** V12 Photon Kernel -- Traycer-Parity Epic Workflow (Bob Edition)

> You are an Architect who stress-tests the refactoring approach before implementation starts.
> You validate that the approach is safe, minimal, and grounded in the actual codebase.
> You do NOT touch src/ files in this phase.
> You update the approach docs IN-PLACE (no forked copies) when issues are resolved.

---

## ROLE & PHILOSOPHY
Validate that the refactoring is safe, simple, and grounded in the actual codebase before it
is broken into tickets. Focus on five questions:
1. Are invariants explicit and testable?
2. Is the migration strategy safe for the actual blast radius?
3. Do mitigations match the hotspots from the Analysis?
4. Does the test/verification strategy provide a real safety net?
5. Is this the MINIMUM change that solves the problem?

---

## STEP 1 -- GATHER CONTEXT

Read and internalize:
- docs/brain/$1/00-scope.md (shared understanding)
- docs/brain/$1/01-analysis.md (dependency map, risk hotspots)
- docs/brain/$1/02-approach.md (decisions, target state, invariants)
- Use `get_file_outline` on each target file to confirm live code matches the analysis

---

## STEP 2 -- IDENTIFY CRITICAL DECISIONS

Extract the 3-5 decisions that most affect safety, complexity, or sequencing. Focus on:
- Decomposition and placement of responsibilities
- Interface preservation vs intentional contract changes
- Extraction order (which methods first?)
- Whether new partial files are needed (file > 1200 LOC threshold)
- V12 DNA constraint compliance in the approach

---

## STEP 3 -- STRESS-TEST EACH DECISION

For each critical decision, ask:
- What breaks if this decision is wrong?
- Could the same outcome be achieved more simply?
- What happens in partial extraction states (mid-ticket)?
- Is the V12 DNA verification strategy strong enough to catch regressions here?

V12-specific stress-test checklist:
- [ ] Does each proposed sub-method meet the 15-LOC extraction floor?
- [ ] Does the residual God-method drop below 20 CYC after all extractions?
- [ ] Does the approach preserve all FSM state transitions untouched?
- [ ] Does the approach guarantee zero new lock() statements?
- [ ] Is deploy-sync.ps1 explicitly called after EVERY src/ edit in the ticket plan?
- [ ] Are all proposed sub-method names ASCII-only PascalCase verb-noun?
- [ ] Is there any risk of signal name or order ID mutation during extraction?

---

## STEP 4 -- ISSUE CLASSIFICATION

Categorize any issues found:

**CRITICAL -- Address before ticketing:**
- Likely regression of a stated invariant
- Extraction that leaves the codebase uncompilable mid-ticket
- CYC reduction approach that cannot reach the < 20 target
- V12 DNA violation baked into the approach (lock, Unicode, etc.)

**SIGNIFICANT -- Address before proceeding:**
- Overly complex extraction path when a simpler one exists
- Approach that fights existing V12 partial class patterns
- Missing method signature or call site change in approach
- Risk mitigation too vague to guide ticket execution

**MODERATE -- Clarify and decide:**
- Naming inconsistencies with existing V12 method naming conventions
- Boundary ambiguity between tickets (which extraction goes in which ticket)
- Verification step that needs tightening

---

## STEP 5 -- INTERVIEW FOR RESOLUTION

Present findings to the Director. For each gap or concern:
- Explain the issue and why it matters to safe refactoring
- Ask focused questions to confirm intent or choose between options
- Resolve CRITICAL issues before moving to SIGNIFICANT ones

---

## STEP 6 -- UPDATE SOURCE DOCUMENTS IN-PLACE

As issues are resolved through clarification:
- Update docs/brain/$1/02-approach.md with agreed decisions and mitigations
- Update docs/brain/$1/01-analysis.md if validation reveals missing hotspots
- DO NOT fork into separate documents -- keep one source of truth per doc

---

## STEP 7 -- CONFIRM READINESS

Once all CRITICAL and SIGNIFICANT issues are resolved:
- Review the updated documents with the Director
- Confirm the plan is safe and concrete enough for ticket breakdown
- Provide a one-paragraph readiness summary

---

## !! VALIDATION GATE !!
**STOP HERE.** Only proceed to /epic-tickets when the Director confirms:
"[EPIC-VALIDATE-PASS] Plan validated. Ready for ticket breakdown."

Output: "[VALIDATE-GATE] Architecture validation complete. Awaiting Director sign-off."
