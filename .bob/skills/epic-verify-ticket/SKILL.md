---
name: epic-verify-ticket
description: Phase 5.X.V - Per-ticket verification subtask to validate implementation.
metadata:
  user-invocable: true
  disable-model-invocation: true
  argument-hint: <epic-slug> <ticket-number>
---

# PHASE 5.X.V: EPIC VERIFY TICKET
**Epic Slug:** $1
**Ticket Number:** $2
**Protocol:** V12 Photon Kernel -- Manifest-Based Independent Subtask

> You are a Verification Engineer who validates ticket implementation against requirements.
> You check that the implementation matches the ticket spec and passes all V12 DNA gates.
> You do NOT modify src/ files in this phase.
> You produce ONE verification report then STOP for Director approval.

---

## ROLE & PHILOSOPHY
Verification is the safety net between implementation and PR. Each ticket must be independently
verified before the epic can proceed to final review. This phase answers:
- Does the implementation match the ticket specification?
- Do all V12 DNA gates pass?
- Are there any regressions or side effects?
- Is the code ready for the next ticket or final review?

Value system:
- Trust but verify -- implementation may drift from spec
- DNA compliance is non-negotiable
- Regressions must be caught before they compound
- Clear PASS/FAIL/NEEDS_FIXES verdict required

---

## STEP 0 -- LOAD MANIFEST

```python
import sys
sys.path.append('scripts')
from epic_manifest import load_manifest, validate_dependencies

# Parse ticket number
ticket_num = "$2"
phase_id = f"5.{ticket_num}"
verify_phase_id = f"5.{ticket_num}.V"

# Load manifest
try:
    manifest = load_manifest("$1")
except FileNotFoundError:
    print("[ERROR] Manifest not found. Run /epic-intake first.")
    exit(1)

# Verify ticket execution phase complete
if not validate_dependencies("$1", verify_phase_id):
    print(f"[ERROR] Phase {phase_id} (Ticket {ticket_num}) must be completed first")
    print(f"Dependencies not satisfied for Phase {verify_phase_id}")
    exit(1)

print(f"[✓] Manifest loaded. Phase {phase_id} complete.")
print(f"[✓] Inputs:")
for artifact in manifest['phases'][phase_id]['output_artifacts']:
    print(f"    - {artifact}")
```

---

## STEP 1 -- READ TICKET SPECIFICATION

Read the ticket file and completion report:

```python
# Get ticket file from Phase 4 outputs
phase4_outputs = manifest['phases']['4']['output_artifacts']
ticket_files = [f for f in phase4_outputs if f'ticket-{ticket_num.zfill(2)}' in f]

if not ticket_files:
    print(f"[ERROR] Ticket file not found for ticket {ticket_num}")
    exit(1)

ticket_file = ticket_files[0]
print(f"[→] Reading ticket spec: {ticket_file}")

# Get completion report from ticket execution phase
completion_outputs = manifest['phases'][phase_id]['output_artifacts']
completion_file = [f for f in completion_outputs if 'completion' in f][0] if completion_outputs else None

if completion_file:
    print(f"[→] Reading completion report: {completion_file}")
else:
    print("[WARNING] No completion report found. Verification will be based on git diff only.")
```

Use `read_file` to load:
- Ticket specification (objectives, scope, acceptance criteria)
- Completion report (if exists)

---

## STEP 2 -- ANALYZE GIT DIFF

Get the changes made during ticket execution:

```python
# Use obtain_git_diff to see what changed
print(f"[→] Analyzing git diff for ticket {ticket_num}...")
```

Use `obtain_git_diff` tool to get the diff. Analyze:
- Which files were modified?
- Do the changes align with ticket scope?
- Are there any unexpected modifications?
- Were any files modified that were OUT OF SCOPE?

---

## STEP 3 -- VERIFY ACCEPTANCE CRITERIA

For each acceptance criterion in the ticket:
- [ ] All listed sub-methods created in correct file
- [ ] Original method reduced to dispatcher role (< 20 CYC)
- [ ] deploy-sync.ps1 ASCII gate: PASS
- [ ] complexity_audit.py shows reduced CYC
- [ ] lock() audit: ZERO matches
- [ ] BUILD_TAG banner visible in NinjaTrader

Check each criterion against:
- Git diff (structural changes)
- Completion report (test results)
- jCodemunch analysis (complexity verification)

### 3a -- Complexity Verification
Using jCodemunch:
```
get_symbol_complexity { "repo": ".", "symbol_id": "<target-method>" }
```

Verify:
- Target method CYC reduced as specified
- Extracted methods meet 15 LOC minimum
- No new methods exceed CYC 20

### 3b -- DNA Compliance Check
Run V12 DNA audits:

```powershell
# ASCII gate
grep -Prn "[^\x00-\x7F]" src/

# Lock audit
grep -r "lock(" src/

# Build verification
dotnet build

# Complexity audit
python scripts/complexity_audit.py
```

Document results for each gate.

### 3c -- Blast Radius Check
Using jCodemunch:
```
get_blast_radius { "repo": ".", "symbol": "<modified-method>", "depth": 1 }
```

Verify:
- No unexpected callers affected
- All affected callers still compile
- No new dependencies introduced

---

## STEP 4 -- REGRESSION DETECTION

Check for unintended side effects:

### 4a -- Scope Boundary Violations
- Were any files modified outside ticket scope?
- Were any pre-existing issues "fixed" (scope creep)?
- Were any unrelated methods modified?

### 4b -- Logic Drift
Using jCodemunch:
```
get_symbol_source { "repo": ".", "symbol_id": "<extracted-method>" }
```

Compare extracted method logic against original:
- Is the logic identical (pure structural movement)?
- Were any conditions changed?
- Were any variables renamed or types changed?

### 4c -- Integration Points
- Do all call sites still work?
- Are method signatures unchanged (unless specified)?
- Are FSM state transitions preserved?

---

## STEP 5 -- DETERMINE VERDICT

Based on verification results, assign verdict:

**PASS**: All criteria met, no regressions, DNA compliant
- All acceptance criteria: ✓
- All DNA gates: PASS
- No scope violations
- No logic drift
- Ready for next ticket or final review

**NEEDS_FIXES**: Minor issues that must be addressed
- 1-3 acceptance criteria: ✗
- DNA gates: 1-2 warnings
- Minor scope boundary issues
- Requires targeted fixes before proceeding

**FAIL**: Major issues requiring ticket rework
- >3 acceptance criteria: ✗
- DNA gates: FAIL (compilation errors, lock violations)
- Significant scope violations
- Logic drift detected
- Requires full ticket re-execution

---

## STEP 6 -- WRITE VERIFICATION REPORT

Produce `docs/brain/$1/ticket-{ticket_num}-verification.md`:

```markdown
# Epic: $1 -- Ticket {ticket_num} Verification

## Verdict
**Status**: [PASS / NEEDS_FIXES / FAIL]

## Acceptance Criteria Verification
| Criterion | Status | Evidence |
|-----------|--------|----------|
| Sub-methods created | ✓/✗ | [file:line references] |
| CYC reduced | ✓/✗ | [before: X, after: Y] |
| ASCII gate | ✓/✗ | [grep result] |
| Complexity audit | ✓/✗ | [audit output] |
| Lock audit | ✓/✗ | [grep result] |
| BUILD_TAG visible | ✓/✗ | [F5 test result] |

## DNA Compliance
| Gate | Result | Details |
|------|--------|---------|
| ASCII-only | PASS/FAIL | [details] |
| Lock-free | PASS/FAIL | [details] |
| Build | PASS/FAIL | [details] |
| Complexity | PASS/FAIL | [details] |

## Regression Analysis
**Scope Violations**: [NONE / list violations]
**Logic Drift**: [NONE / list drift]
**Unexpected Changes**: [NONE / list changes]

## Blast Radius Verification
- Direct callers affected: [count]
- All callers compile: [YES/NO]
- New dependencies: [NONE / list]

## Issues Found
[If NEEDS_FIXES or FAIL, list specific issues with file:line references]

## Recommendations
[If PASS: "Ready for next ticket" or "Ready for final review"]
[If NEEDS_FIXES: Specific fixes needed]
[If FAIL: "Requires ticket re-execution with focus on: ..."]
```

---

## STEP 7 -- UPDATE MANIFEST

```python
from epic_manifest import update_manifest

# Write output artifact
output_path = f"docs/brain/$1/ticket-{ticket_num.zfill(2)}-verification.md"

# Determine status based on verdict
import os
if os.path.exists(output_path):
    with open(output_path, 'r') as f:
        content = f.read()
        if "**Status**: PASS" in content:
            status = "completed"
            notes = f"Ticket {ticket_num} verification PASSED. Ready to proceed."
        elif "**Status**: NEEDS_FIXES" in content:
            status = "completed"
            notes = f"Ticket {ticket_num} verification found minor issues. Fixes required."
        else:  # FAIL
            status = "completed"
            notes = f"Ticket {ticket_num} verification FAILED. Ticket re-execution required."
else:
    print("[ERROR] Verification report not created")
    exit(1)

# Update manifest
update_manifest(
    "$1",
    verify_phase_id,
    status,
    outputs=[output_path],
    notes=notes
)

print(f"[✓] Phase {verify_phase_id} complete. Output: {output_path}")
```

---

## !! VERIFICATION GATE !!
**STOP HERE.** Present the verification report.

Ask the Director:
- If PASS: Proceed to next ticket or final review?
- If NEEDS_FIXES: Apply fixes now or defer?
- If FAIL: Re-execute ticket or revise approach?

**Do NOT proceed until the Director provides explicit direction.**

Output: "[VERIFY-GATE] Ticket {ticket_num} verification complete. Verdict: [PASS/NEEDS_FIXES/FAIL]. Awaiting Director decision."
