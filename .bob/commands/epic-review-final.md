---
description: Phase 6 - Final epic review. Produces READY_FOR_PR verdict. PR submission happens AFTER Phase 6 via /pr-loop.
argument-hint: <epic-slug>
---
# PHASE 6: EPIC REVIEW FINAL
**Epic Slug:** $1
**Protocol:** V12 Photon Kernel -- Manifest-Based Independent Subtask

> You are the Final Reviewer who validates the entire epic and produces a READY_FOR_PR verdict.
> You verify all tickets passed verification and the epic meets its success criteria.
> You do NOT modify src/ files in this phase.
> You do NOT create PRs in this phase.
> You produce ONE final review report then STOP. PR submission happens AFTER Phase 6 via separate `/pr-loop` command.

---

## ROLE & PHILOSOPHY
The final review is the last gate before PR submission. This phase ensures:
- All tickets completed and verified successfully
- Epic success criteria met (CYC targets, DNA compliance)
- No accumulated technical debt or scope creep
- PR is ready for external review and merge

Value system:
- Holistic validation -- individual ticket success doesn't guarantee epic success
- DNA compliance at epic level (not just ticket level)
- PR hygiene -- diff size, commit messages, documentation
- Clear READY_FOR_PR / NEEDS_REWORK verdict

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

# Verify all ticket verifications complete
if not validate_dependencies("$1", "6"):
    print("[ERROR] All ticket verifications must be completed first")
    print("Dependencies not satisfied for Phase 6")
    exit(1)

print("[✓] Manifest loaded. All ticket verifications complete.")

# Get ticket count
ticket_count = manifest['metadata']['total_tickets']
print(f"[✓] Epic has {ticket_count} tickets")

# Check verification results
verification_phases = [f"5.{i}.V" for i in range(1, ticket_count + 1)]
for verify_phase in verification_phases:
    status = manifest['phases'][verify_phase]['status']
    print(f"[✓] Phase {verify_phase}: {status}")
```

---

## STEP 1 -- COLLECT ALL ARTIFACTS

Read all epic artifacts to build complete picture:

```python
# Collect all artifacts from all phases
all_artifacts = []
for phase_id, phase_data in manifest['phases'].items():
    if phase_data['status'] == 'completed':
        all_artifacts.extend(phase_data['output_artifacts'])

print(f"[→] Reviewing {len(all_artifacts)} artifacts:")
for artifact in sorted(set(all_artifacts)):
    print(f"    - {artifact}")
```

Use `read_file` to load key documents:
- Scope document (Phase 1)
- Boundary analysis (Phase 1.5)
- Analysis and approach (Phase 2)
- All ticket specifications (Phase 4)
- All ticket verifications (Phase 5.X.V)

---

## STEP 2 -- VERIFY EPIC SUCCESS CRITERIA

### 2a -- Complexity Targets Met
From scope document, extract target CYC scores. Verify each target method:

Using jCodemunch:
```
get_symbol_complexity { "repo": ".", "symbol_id": "<target-method>" }
```

Compare:
- Before CYC (from scope)
- Target CYC (from scope)
- Actual CYC (from jCodemunch)

**Success**: All target methods meet or exceed CYC targets

### 2b -- Extraction Completeness
From ticket specifications, verify:
- All planned sub-methods created
- All sub-methods >= 15 LOC
- All residual methods < 20 CYC
- No partial extractions left incomplete

### 2c -- Scope Boundary Integrity
From boundary analysis, verify:
- No scope creep occurred
- Pre-existing issues remain OUT OF SCOPE
- ONE CONCERN rule maintained throughout

---

## STEP 3 -- EPIC-LEVEL DNA COMPLIANCE

Run comprehensive V12 DNA audits across entire epic:

### 3a -- ASCII Gate (Epic-Wide)
```powershell
grep -Prn "[^\x00-\x7F]" src/
```
**Required**: ZERO matches

### 3b -- Lock Audit (Epic-Wide)
```powershell
grep -r "lock(" src/
```
**Required**: ZERO matches

### 3c -- Build Verification
```powershell
dotnet build
```
**Required**: ZERO errors, ZERO warnings

### 3d -- Complexity Audit (Epic-Wide)
```powershell
python scripts/complexity_audit.py
```
**Required**: All modified methods < 20 CYC

### 3e -- Hard-Link Integrity
```powershell
powershell -File .\deploy-sync.ps1
```
**Required**: ASCII gate PASS, BUILD_TAG updated

### 3f -- Pre-Push Validation
```powershell
powershell -File .\scripts\pre_push_validation.ps1
```
**Required**: All 13 checks PASS (or acceptable warnings)

---

## STEP 4 -- PR HYGIENE VERIFICATION

### 4a -- Diff Size Check
Using `obtain_git_diff`:
```python
# Get full epic diff
print("[→] Analyzing epic diff size...")
```

Verify:
- Total diff < 10,000 characters (V12 limit)
- If exceeded, recommend splitting into multiple PRs

### 4b -- Commit History
Check git log for:
- Clear commit messages
- Logical commit boundaries
- No "WIP" or "fix" commits (should be squashed)

### 4c -- Documentation Updates
Verify:
- BUILD_TAG bumped in src/V12_002.cs
- CHANGELOG updated (if exists)
- Any new methods documented

---

## STEP 5 -- VERIFICATION REPORT ANALYSIS

Review all ticket verification reports:

```python
# Analyze verification verdicts
verification_verdicts = {}
for i in range(1, ticket_count + 1):
    verify_phase = f"5.{i}.V"
    outputs = manifest['phases'][verify_phase]['output_artifacts']
    if outputs:
        verify_file = outputs[0]
        # Read and extract verdict
        print(f"[→] Analyzing {verify_file}")
        # Track PASS/NEEDS_FIXES/FAIL counts
```

Check:
- All verifications: PASS?
- Any NEEDS_FIXES deferred?
- Any FAIL tickets re-executed?

**Red Flag**: If any verification is not PASS, epic is not ready for PR.

---

## STEP 6 -- REGRESSION SWEEP

Using jCodemunch, perform final regression checks:

### 6a -- Blast Radius Validation
For each modified method:
```
get_blast_radius { "repo": ".", "symbol": "<method>", "depth": 2 }
```

Verify:
- No unexpected callers affected
- All affected files still compile
- No new cross-file dependencies

### 6b -- Dead Code Detection
```
find_dead_code { "repo": ".", "granularity": "symbol" }
```

Check:
- No new dead code introduced
- Extracted methods are all reachable

### 6c -- Dependency Cycles
```
get_dependency_cycles { "repo": "." }
```

Verify:
- No new circular dependencies
- Existing cycles not worsened

---

## STEP 7 -- DETERMINE FINAL VERDICT

Based on all verification results, assign verdict:

**READY_FOR_PR**: Epic complete, all gates passed
- All success criteria: ✓
- All DNA gates: PASS
- All verifications: PASS
- PR hygiene: PASS
- No regressions detected
- Ready for external review and merge

**NEEDS_REWORK**: Issues found that must be addressed
- 1-3 success criteria: ✗
- DNA gates: 1-2 warnings
- 1-2 verifications: NEEDS_FIXES
- PR hygiene: Minor issues
- Requires targeted fixes before PR

**BLOCKED**: Major issues requiring epic restart
- >3 success criteria: ✗
- DNA gates: FAIL
- >2 verifications: FAIL
- PR hygiene: Major violations
- Scope creep detected
- Requires epic re-planning or cancellation

---

## STEP 8 -- WRITE FINAL REVIEW REPORT

Produce `docs/brain/$1/06-final-review.md`:

```markdown
# Epic: $1 -- Final Review

## Verdict
**Status**: [READY_FOR_PR / NEEDS_REWORK / BLOCKED]

## Epic Success Criteria
| Criterion | Target | Actual | Status |
|-----------|--------|--------|--------|
| Method A CYC | < 20 | [X] | ✓/✗ |
| Method B CYC | < 20 | [X] | ✓/✗ |
| Extractions | [N] | [N] | ✓/✗ |
| Scope boundary | Maintained | [status] | ✓/✗ |

## Epic-Level DNA Compliance
| Gate | Result | Details |
|------|--------|---------|
| ASCII-only | PASS/FAIL | [grep output] |
| Lock-free | PASS/FAIL | [grep output] |
| Build | PASS/FAIL | [build output] |
| Complexity | PASS/FAIL | [audit output] |
| Hard-links | PASS/FAIL | [deploy-sync output] |
| Pre-push | PASS/FAIL | [validation output] |

## Ticket Verification Summary
| Ticket | Verdict | Issues |
|--------|---------|--------|
| 1 | PASS/NEEDS_FIXES/FAIL | [summary] |
| 2 | PASS/NEEDS_FIXES/FAIL | [summary] |
| ... | ... | ... |

## PR Hygiene
- Diff size: [X] characters ([PASS/FAIL] < 10k limit)
- Commit history: [CLEAN / NEEDS_SQUASH]
- Documentation: [UPDATED / MISSING]
- BUILD_TAG: [BUMPED / NOT_BUMPED]

## Regression Analysis
- Blast radius: [X] files affected, all compile: [YES/NO]
- Dead code: [NONE / list]
- Dependency cycles: [NONE / list]

## Issues Found
[If NEEDS_REWORK or BLOCKED, list specific issues with severity]

## Recommendations
[If READY_FOR_PR: "Epic complete. Ready for PR submission and external review."]
[If NEEDS_REWORK: Specific fixes needed before PR]
[If BLOCKED: "Epic requires re-planning. Recommend: ..."]

## Next Steps
[If READY_FOR_PR:]
1. Run `/pr-loop` to create PR
2. Monitor CI/CD pipeline
3. Address review feedback

[If NEEDS_REWORK:]
1. Address listed issues
2. Re-run affected ticket verifications
3. Re-run final review

[If BLOCKED:]
1. Analyze root cause of failures
2. Revise epic approach
3. Consider splitting into smaller epics
```

---

## STEP 9 -- UPDATE MANIFEST

```python
from epic_manifest import update_manifest

# Write output artifact
output_path = f"docs/brain/$1/06-final-review.md"

# Determine status based on verdict
import os
if os.path.exists(output_path):
    with open(output_path, 'r') as f:
        content = f.read()
        if "**Status**: READY_FOR_PR" in content:
            status = "completed"
            notes = "Epic final review PASSED. Ready for PR submission."
            # Update epic status to completed
            manifest['status'] = 'completed'
        elif "**Status**: NEEDS_REWORK" in content:
            status = "completed"
            notes = "Epic final review found issues. Rework required before PR."
        else:  # BLOCKED
            status = "completed"
            notes = "Epic final review BLOCKED. Major issues require re-planning."
            manifest['status'] = 'blocked'
else:
    print("[ERROR] Final review report not created")
    exit(1)

# Update manifest
update_manifest(
    "$1",
    "6",
    status,
    outputs=[output_path],
    notes=notes
)

print(f"[✓] Phase 6 complete. Output: {output_path}")
print(f"[✓] Epic status: {manifest['status']}")
```

---

## !! FINAL REVIEW GATE !!
**STOP HERE.** Present the final review report.

Ask the Director:
- If READY_FOR_PR: Proceed with `/pr-loop` to create PR?
- If NEEDS_REWORK: Which issues to address first?
- If BLOCKED: Cancel epic or revise approach?

**Do NOT create PR until the Director explicitly approves.**

Output: "[FINAL-REVIEW-GATE] Epic $1 final review complete. Verdict: [READY_FOR_PR/NEEDS_REWORK/BLOCKED]. Awaiting Director decision."
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
