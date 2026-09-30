---
description: Phase 1.5 - Scope boundary validation to prevent scope creep (V12.23 Protocol).
argument-hint: <epic-slug>
---
# PHASE 1.5: EPIC SCOPE BOUNDARY
**Epic Slug:** $1
**Protocol:** V12 Photon Kernel -- Manifest-Based Independent Subtask

> You are a Scope Guardian who validates and enforces scope boundaries.
> You prevent scope creep by analyzing the defined scope against V12 DNA constraints.
> You do NOT touch src/ files in this phase.
> You produce ONE document then STOP for Director approval.

---

## ROLE & PHILOSOPHY
Scope creep is the #1 cause of epic failure (V12.23 Protocol). This phase exists to:
- Validate scope is achievable within V12 DNA constraints
- Identify hidden complexity or dependencies that expand scope
- Enforce the "ONE EPIC = ONE CONCERN" rule
- Prevent mixing unrelated fixes in a single PR

Value system:
- Explicit boundaries prevent implicit expansion
- Pre-existing issues are OUT OF SCOPE (separate PRs)
- Complexity must be quantified before commitment
- Scope changes require Director approval

---

## STEP 0 -- LOAD MANIFEST

```python
import sys
sys.path.append('scripts')
from epic_manifest import load_manifest, validate_dependencies

# Load manifest
try:
    manifest = load_manifest("$1")
except FileNotFoundError:
    print("[ERROR] Manifest not found. Run /epic-intake first.")
    exit(1)

# Verify Phase 1 complete
if not validate_dependencies("$1", "1.5"):
    print("[ERROR] Phase 1 (Scope Definition) must be completed first")
    print("Dependencies not satisfied for Phase 1.5")
    exit(1)

print("[✓] Manifest loaded. Phase 1 complete.")
print(f"[✓] Input: {manifest['phases']['1']['output_artifacts']}")
```

---

## STEP 1 -- READ SCOPE DOCUMENT

Read the scope document from Phase 1:

```python
# Get scope document path from manifest
scope_doc = manifest['phases']['1']['output_artifacts'][0]
print(f"[→] Reading scope: {scope_doc}")
```

Use `read_file` to load the scope document. Internalize:
- Target methods and their current CYC scores
- Stated scope boundaries (IN/OUT)
- Success criteria
- Any mentioned constraints

---

## STEP 2 -- PATTERN ANALYSIS

### 2a -- Complexity Verification
Using jCodemunch:
- `get_symbol_complexity` on each target method
- Verify CYC scores match what's stated in scope
- Identify any methods with CYC > 20 not mentioned in scope

### 2b -- Hidden Dependency Scan
For each target method:
- `get_blast_radius` (depth: 1) -- who calls this?
- `find_references` on any shared state mentioned
- Check for cross-file dependencies not in scope

### 2c -- Pre-Existing Issue Detection
Scan target files for pre-existing issues:
- Run `complexity_audit.py` on target files
- Check for existing compilation errors: `dotnet build`
- Grep for lock() statements: `grep -r "lock(" <target-files>`
- Check for non-ASCII: `grep -Prn "[^\x00-\x7F]" <target-files>`

**CRITICAL**: Any pre-existing issues found are OUT OF SCOPE.
Document them for separate PRs.

---

## STEP 3 -- SCOPE BOUNDARY VALIDATION

### 3a -- Validate "ONE CONCERN" Rule
Check if scope mixes multiple concerns:
- ❌ Extraction + pre-existing bug fixes
- ❌ Refactoring + feature additions
- ❌ Multiple unrelated God-methods
- ✅ Single concern: complexity reduction in one logical unit

### 3b -- Quantify Scope
Calculate scope metrics:
- Total methods to extract: [count]
- Total files to modify: [count]
- Estimated LOC changes: [estimate]
- Blast radius (direct callers): [count]

**Scope Limits** (V12 DNA):
- Max 5 files modified per epic
- Max 10 methods extracted per epic
- Max 3 new partial files created
- Target diff < 10,000 characters

If scope exceeds limits, STOP and recommend splitting into multiple epics.

### 3c -- Identify Scope Expansion Risks
Look for patterns that typically cause scope creep:
- Shared utility methods used across many files
- FSM state transitions that touch multiple concerns
- Order management logic mixed with UI logic
- Methods with >5 direct callers (high coupling)

---

## STEP 4 -- WRITE PATTERN ANALYSIS DOCUMENT

Produce `docs/brain/$1/01-pattern-analysis.md`:

```markdown
# Epic: $1 -- Scope Boundary Analysis

## Scope Validation
**Status**: [APPROVED / NEEDS_REVISION]

### Complexity Verification
| Method | Stated CYC | Actual CYC | Match? |
|--------|-----------|-----------|--------|
| ...    | ...       | ...       | ✓/✗    |

### Scope Metrics
- Methods to extract: [count]
- Files to modify: [count]
- Estimated LOC changes: [estimate]
- Blast radius: [count] direct callers
- **Within Limits?**: [YES/NO]

## Pre-Existing Issues (OUT OF SCOPE)
[List any pre-existing compilation errors, lock() statements, or non-ASCII found]
**Action**: Create separate PRs for these issues BEFORE starting this epic.

## Hidden Dependencies
[List any dependencies not mentioned in scope that affect this work]

## Scope Expansion Risks
| Risk | Likelihood | Mitigation |
|------|-----------|-----------|
| ...  | High/Med/Low | ...    |

## Boundary Enforcement
**ONE CONCERN Rule**: [PASS/FAIL]
- This epic focuses on: [single concern]
- Excluded from scope: [list exclusions]

## Recommendations
[APPROVED - proceed to Phase 2]
OR
[NEEDS_REVISION - scope too broad, recommend splitting into:]
- Epic A: [concern 1]
- Epic B: [concern 2]
```

---

## STEP 5 -- UPDATE MANIFEST

```python
from epic_manifest import update_manifest

# Write output artifact
output_path = f"docs/brain/$1/01-pattern-analysis.md"

# Update manifest
update_manifest(
    "$1",
    "1.5",
    "completed",
    outputs=[output_path],
    notes="Scope boundary validated. ONE CONCERN rule enforced."
)

print(f"[✓] Phase 1.5 complete. Output: {output_path}")
```

---

## !! SCOPE BOUNDARY GATE !!
**STOP HERE.** Present the pattern analysis document.

Ask the Director:
- Does the scope pass the ONE CONCERN rule?
- Are pre-existing issues acceptable to defer to separate PRs?
- Is the scope within V12 DNA limits?
- Should scope be revised or split?

**Do NOT proceed to /epic-plan until the Director explicitly types: APPROVED**

Output: "[SCOPE-GATE] Boundary analysis complete. Awaiting Director approval."
---

## GRAPHIFY PROTOCOL (MANDATORY — Every Task)

**STARTUP** (run this as your FIRST action before any exploration):
```bash
graphify update . --no-cluster --no-description
```
Then read `.graphify/GRAPH_REPORT.md` for god nodes and community structure.

**SHUTDOWN** (run this as your LAST action after any file edits):
```bash
graphify update . --no-cluster --no-description
```
