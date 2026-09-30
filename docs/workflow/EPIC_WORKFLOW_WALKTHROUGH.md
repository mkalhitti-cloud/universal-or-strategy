# V12 Epic Workflow Walkthrough

**Version**: 1.0  
**Date**: 2026-06-09  
**Status**: Active  
**Author**: V12 Architecture Team

## Executive Summary

This guide provides a step-by-step walkthrough for executing V12 epics using the new manifest-based independent subtask workflow. Follow this guide to understand the complete epic lifecycle from initialization to completion.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Quick Start](#quick-start)
3. [Phase-by-Phase Walkthrough](#phase-by-phase-walkthrough)
4. [Parallel Execution](#parallel-execution)
5. [Troubleshooting](#troubleshooting)
6. [Example: EPIC-CCN-16](#example-epic-ccn-16)
7. [Best Practices](#best-practices)

## Prerequisites

### Required Tools

- ✅ Git (with GitButler virtual branches)
- ✅ Python 3.11+ (for manifest helper)
- ✅ PowerShell 7+ (for build scripts)
- ✅ Bob CLI (for v12-engineer mode)
- ✅ All epic phase commands installed

### Environment Setup

```bash
# Verify commands available
epic-intake --help
epic-scope-boundary --help
epic-plan --help
epic-scan --help
epic-tickets --help
epic-validate --help
epic-verify-ticket --help
epic-review-final --help

# Verify manifest helper
python scripts/epic_manifest.py --help

# Verify on gitbutler/workspace branch
git branch --show-current  # Should show: gitbutler/workspace
```

### Knowledge Requirements

- Understanding of V12 DNA principles (AGENTS.md)
- Familiarity with complexity reduction protocol
- Knowledge of GitButler virtual branches
- Understanding of manifest-based state management

## Quick Start

### 5-Minute Epic Execution

```bash
# 1. Initialize epic (Phase 0)
epic-intake EPIC-CCN-X "Extract high-complexity method"

# 2. Define scope (Phase 1)
epic-scope-boundary EPIC-CCN-X --phase 1

# 3. Validate scope (Phase 1.5)
epic-scope-boundary EPIC-CCN-X --phase 1.5

# 4. Plan architecture (Phase 2)
epic-plan EPIC-CCN-X

# 5. Audit plan (Phase 3)
epic-scan EPIC-CCN-X

# 6. Generate tickets (Phase 4)
epic-tickets EPIC-CCN-X

# 7. Execute tickets (Phase 5.X)
epic-validate EPIC-CCN-X --ticket 1
epic-validate EPIC-CCN-X --ticket 2

# 8. Verify tickets (Phase 5.X.V)
epic-verify-ticket EPIC-CCN-X --ticket 1
epic-verify-ticket EPIC-CCN-X --ticket 2

# 9. Final review (Phase 6)
epic-review-final EPIC-CCN-X

# 10. Deploy
powershell -File .\deploy-sync.ps1
```

### Check Status Anytime

```bash
# View manifest status
python scripts/epic_manifest.py status EPIC-CCN-X

# View next executable phases
python scripts/epic_manifest.py next EPIC-CCN-X

# View detailed phase info
python scripts/epic_manifest.py phase EPIC-CCN-X 5.1
```

## Phase-by-Phase Walkthrough

### Phase 0: Hotspot Analysis

**Purpose**: Identify high-complexity methods requiring refactoring

**Command**:
```bash
epic-intake EPIC-CCN-16 "Extract ProcessOrderUpdate complexity (CYC 28)"
```

**What Happens**:
1. Creates `docs/brain/EPIC-CCN-16/` directory
2. Generates `manifest.json` with initial state
3. Runs hotspot analysis using jcodemunch-mcp
4. Creates `00-hotspots.md` with findings
5. Updates manifest: Phase 0 = `completed`

**Output Artifacts**:
- `docs/brain/EPIC-CCN-16/manifest.json`
- `docs/brain/EPIC-CCN-16/00-hotspots.md`

**Verification**:
```bash
# Check manifest created
cat docs/brain/EPIC-CCN-16/manifest.json | jq '.phases."0".status'
# Expected: "completed"

# Check hotspots identified
cat docs/brain/EPIC-CCN-16/00-hotspots.md
# Expected: List of high-complexity methods with CYC scores
```

**Success Criteria**:
- ✅ Manifest exists and is valid JSON
- ✅ Phase 0 status = `completed`
- ✅ Hotspots report contains at least 1 method
- ✅ CYC scores documented for each hotspot

**Common Issues**:
- **Issue**: "jcodemunch-mcp not available"
  - **Fix**: Ensure MCP server running: `jcodemunch-mcp --version`
- **Issue**: "No hotspots found"
  - **Fix**: Check complexity threshold in `.jcodemunch.jsonc`

---

### Phase 1: Scope Definition

**Purpose**: Define precise scope boundaries for the epic

**Command**:
```bash
epic-scope-boundary EPIC-CCN-16 --phase 1
```

**What Happens**:
1. Reads `00-hotspots.md` from manifest
2. Analyzes method complexity and dependencies
3. Defines scope: methods, files, extraction targets
4. Creates `00-scope.md` with scope definition
5. Updates manifest: Phase 1 = `completed`

**Input Artifacts** (from manifest):
- `docs/brain/EPIC-CCN-16/00-hotspots.md`

**Output Artifacts**:
- `docs/brain/EPIC-CCN-16/00-scope.md`

**Verification**:
```bash
# Check scope defined
cat docs/brain/EPIC-CCN-16/00-scope.md

# Expected sections:
# - Target Methods (1-3 methods)
# - Affected Files (1-5 files)
# - Extraction Plan (helper methods to extract)
# - Complexity Reduction Goal (target CYC)
```

**Success Criteria**:
- ✅ Scope limited to 1-3 methods
- ✅ Affected files ≤ 5
- ✅ Extraction plan defined
- ✅ Target CYC ≤ 15 (Jane Street alignment)

**Common Issues**:
- **Issue**: "Scope too broad (>5 files)"
  - **Fix**: Split into multiple epics
- **Issue**: "Circular dependencies detected"
  - **Fix**: Refactor dependencies first in separate epic

---

### Phase 1.5: Scope Boundary Validation

**Purpose**: Validate scope boundaries to prevent scope creep (V12.23 Protocol)

**Command**:
```bash
epic-scope-boundary EPIC-CCN-16 --phase 1.5
```

**What Happens**:
1. Reads `00-scope.md` from manifest
2. Validates scope against V12.23 No Scope Creep Protocol
3. Checks for hidden dependencies
4. Verifies no pre-existing compilation errors
5. Creates `01-scope-boundary.md` with validation results
6. Updates manifest: Phase 1.5 = `completed`

**Input Artifacts** (from manifest):
- `docs/brain/EPIC-CCN-16/00-scope.md`

**Output Artifacts**:
- `docs/brain/EPIC-CCN-16/01-scope-boundary.md`

**Verification**:
```bash
# Check boundary validation
cat docs/brain/EPIC-CCN-16/01-scope-boundary.md

# Expected sections:
# - Scope Validation: PASS/FAIL
# - Hidden Dependencies: None/List
# - Pre-existing Errors: None/List
# - Boundary Violations: None/List
```

**Success Criteria**:
- ✅ Scope validation = PASS
- ✅ No hidden dependencies outside scope
- ✅ No pre-existing compilation errors
- ✅ No boundary violations detected

**Common Issues**:
- **Issue**: "Pre-existing compilation errors found"
  - **Fix**: Create separate PR to fix errors first
- **Issue**: "Hidden dependencies detected"
  - **Fix**: Expand scope or create dependency epic first

**CRITICAL**: Phase 1.5 is MANDATORY (V12.23). Never skip this phase.

---

### Phase 2: Architecture Planning

**Purpose**: Design extraction architecture and helper methods

**Command**:
```bash
epic-plan EPIC-CCN-16
```

**What Happens**:
1. Reads `01-scope-boundary.md` from manifest
2. Designs helper method signatures
3. Plans extraction sequence
4. Creates Mermaid diagrams for architecture
5. Creates `02-architecture-plan.md` and `02-diagrams.mmd`
6. Updates manifest: Phase 2 = `completed`

**Input Artifacts** (from manifest):
- `docs/brain/EPIC-CCN-16/01-scope-boundary.md`

**Output Artifacts**:
- `docs/brain/EPIC-CCN-16/02-architecture-plan.md`
- `docs/brain/EPIC-CCN-16/02-diagrams.mmd`

**Verification**:
```bash
# Check architecture plan
cat docs/brain/EPIC-CCN-16/02-architecture-plan.md

# Expected sections:
# - Helper Methods (signatures with parameters)
# - Extraction Sequence (order of operations)
# - Complexity Impact (CYC reduction per helper)
# - Risk Assessment (potential issues)

# View diagrams
cat docs/brain/EPIC-CCN-16/02-diagrams.mmd
```

**Success Criteria**:
- ✅ Helper methods have clear signatures
- ✅ Extraction sequence is logical
- ✅ Each helper reduces CYC by 3-7 points
- ✅ Diagrams show before/after architecture

**Common Issues**:
- **Issue**: "Helper method signatures unclear"
  - **Fix**: Refine signatures with explicit parameter types
- **Issue**: "Extraction sequence has circular dependencies"
  - **Fix**: Reorder extraction to break cycles

---

### Phase 3: DNA & PR Audit

**Purpose**: Verify plan against V12 DNA principles and PR health

**Command**:
```bash
epic-scan EPIC-CCN-16
```

**What Happens**:
1. Reads `02-architecture-plan.md` from manifest
2. Audits plan against V12 DNA (no locks, atomic, ASCII-only)
3. Checks PR health (diff size, complexity, test coverage)
4. Runs adversarial review (Arena AI)
5. Creates `03-audit-report.md` with findings
6. Updates manifest: Phase 3 = `completed`

**Input Artifacts** (from manifest):
- `docs/brain/EPIC-CCN-16/02-architecture-plan.md`

**Output Artifacts**:
- `docs/brain/EPIC-CCN-16/03-audit-report.md`

**Verification**:
```bash
# Check audit results
cat docs/brain/EPIC-CCN-16/03-audit-report.md

# Expected sections:
# - DNA Compliance: PASS/FAIL
# - PR Health Score: 0-100
# - Violations: None/List
# - Recommendations: List
```

**Success Criteria**:
- ✅ DNA compliance = PASS
- ✅ PR health score ≥ 80
- ✅ No P0/P1 violations
- ✅ Diff size < 10,000 characters

**Common Issues**:
- **Issue**: "DNA violation: lock() detected in plan"
  - **Fix**: Redesign to use FSM/Actor pattern
- **Issue**: "PR health score < 80"
  - **Fix**: Split epic into smaller tickets

**GATE**: If Phase 3 fails, return to Phase 2 for redesign.

---

### Phase 4: Ticket Generation

**Purpose**: Generate executable tickets from architecture plan

**Command**:
```bash
epic-tickets EPIC-CCN-16
```

**What Happens**:
1. Reads `02-architecture-plan.md` and `03-audit-report.md` from manifest
2. Generates tickets for each helper extraction
3. Assigns ticket numbers (1, 2, 3, ...)
4. Creates `04-tickets.md` with ticket details
5. Updates manifest: Phase 4 = `completed`
6. Updates manifest with ticket count

**Input Artifacts** (from manifest):
- `docs/brain/EPIC-CCN-16/02-architecture-plan.md`
- `docs/brain/EPIC-CCN-16/03-audit-report.md`

**Output Artifacts**:
- `docs/brain/EPIC-CCN-16/04-tickets.md`

**Verification**:
```bash
# Check tickets generated
cat docs/brain/EPIC-CCN-16/04-tickets.md

# Expected format per ticket:
# ## Ticket 1: Extract ValidateOrderState
# - **Method**: ValidateOrderState
# - **Signature**: private bool ValidateOrderState(Order order)
# - **CYC Reduction**: 5 points
# - **Dependencies**: None
# - **Estimated Duration**: 30 minutes
```

**Success Criteria**:
- ✅ Each ticket has clear method name
- ✅ Each ticket has complete signature
- ✅ Each ticket has CYC reduction estimate
- ✅ Dependencies documented
- ✅ Tickets are independent (can run in parallel)

**Common Issues**:
- **Issue**: "Tickets have circular dependencies"
  - **Fix**: Reorder tickets or merge dependent tickets

---

### Phase 5.X: Ticket Execution

**Purpose**: Execute surgical extraction for a single ticket

**Command**:
```bash
epic-validate EPIC-CCN-16 --ticket 1
```

**What Happens**:
1. Reads `04-tickets.md` and `02-architecture-plan.md` from manifest
2. Launches Bob CLI in v12-engineer mode
3. Extracts helper method per ticket specification
4. Updates calling code to use helper
5. Runs build verification
6. Creates `ticket-1-completion.md` with results
7. Updates manifest: Phase 5.1 = `completed`

**Input Artifacts** (from manifest):
- `docs/brain/EPIC-CCN-16/04-tickets.md`
- `docs/brain/EPIC-CCN-16/02-architecture-plan.md`

**Output Artifacts**:
- `docs/brain/EPIC-CCN-16/ticket-1-completion.md`

**Verification**:
```bash
# Check ticket completion
cat docs/brain/EPIC-CCN-16/ticket-1-completion.md

# Expected sections:
# - Extraction Summary (what was extracted)
# - Files Modified (list of changed files)
# - CYC Reduction (before/after scores)
# - Build Status (PASS/FAIL)
# - Test Status (PASS/FAIL)

# Verify build passes
powershell -File .\scripts\build_readiness.ps1
```

**Success Criteria**:
- ✅ Helper method extracted successfully
- ✅ Calling code updated correctly
- ✅ Build passes (zero errors)
- ✅ CYC reduced as planned
- ✅ No new lint violations

**Common Issues**:
- **Issue**: "Build fails after extraction"
  - **Fix**: Check for missing using statements or parameter mismatches
- **Issue**: "CYC not reduced as expected"
  - **Fix**: Verify helper method contains all extracted logic

**Parallel Execution**: If tickets are independent, run multiple `epic-validate` commands concurrently:
```bash
# Terminal 1
epic-validate EPIC-CCN-16 --ticket 1

# Terminal 2 (simultaneously)
epic-validate EPIC-CCN-16 --ticket 2

# Terminal 3 (simultaneously)
epic-validate EPIC-CCN-16 --ticket 3
```

---

### Phase 5.X.V: Per-Ticket Verification

**Purpose**: Verify ticket execution against plan

**Command**:
```bash
epic-verify-ticket EPIC-CCN-16 --ticket 1
```

**What Happens**:
1. Reads `ticket-1-completion.md` from manifest
2. Verifies extraction matches architecture plan
3. Checks build status
4. Checks test coverage
5. Runs complexity audit
6. Creates `ticket-1-verification.md` with results
7. Updates manifest: Phase 5.1.V = `completed`

**Input Artifacts** (from manifest):
- `docs/brain/EPIC-CCN-16/ticket-1-completion.md`

**Output Artifacts**:
- `docs/brain/EPIC-CCN-16/ticket-1-verification.md`

**Verification**:
```bash
# Check verification results
cat docs/brain/EPIC-CCN-16/ticket-1-verification.md

# Expected sections:
# - Plan Alignment: PASS/FAIL
# - Build Status: PASS/FAIL
# - Test Coverage: X%
# - Complexity Audit: PASS/FAIL
# - Issues Found: None/List
```

**Success Criteria**:
- ✅ Plan alignment = PASS
- ✅ Build status = PASS
- ✅ Test coverage ≥ previous level
- ✅ Complexity audit = PASS (CYC ≤ 15)
- ✅ No new issues introduced

**Common Issues**:
- **Issue**: "Plan alignment FAIL: signature mismatch"
  - **Fix**: Update helper signature to match plan
- **Issue**: "Complexity audit FAIL: CYC still > 15"
  - **Fix**: Extract additional helper methods

**Parallel Execution**: Verify multiple tickets concurrently:
```bash
# Terminal 1
epic-verify-ticket EPIC-CCN-16 --ticket 1

# Terminal 2 (simultaneously)
epic-verify-ticket EPIC-CCN-16 --ticket 2
```

---

### Phase 6: Final Review

**Purpose**: Comprehensive review of entire epic

**Command**:
```bash
epic-review-final EPIC-CCN-16
```

**What Happens**:
1. Reads all `ticket-X-verification.md` files from manifest
2. Aggregates verification results
3. Runs final build and test suite
4. Checks PR health score
5. Verifies deploy-sync readiness
6. Creates `05-completion-report.md` with summary
7. Updates manifest: Phase 6 = `completed`
8. Updates manifest: epic status = `completed`

**Input Artifacts** (from manifest):
- All `docs/brain/EPIC-CCN-16/ticket-X-verification.md` files

**Output Artifacts**:
- `docs/brain/EPIC-CCN-16/05-completion-report.md`

**Verification**:
```bash
# Check completion report
cat docs/brain/EPIC-CCN-16/05-completion-report.md

# Expected sections:
# - Epic Summary (tickets completed, CYC reduction)
# - Build Status (PASS/FAIL)
# - Test Status (PASS/FAIL)
# - PR Health Score (0-100)
# - Deploy Readiness (READY/NOT READY)
# - Recommendations (next steps)
```

**Success Criteria**:
- ✅ All tickets verified successfully
- ✅ Build passes (zero errors)
- ✅ Test suite passes (100%)
- ✅ PR health score ≥ 80
- ✅ Deploy readiness = READY
- ✅ Total CYC reduction meets goal

**Common Issues**:
- **Issue**: "PR health score < 80"
  - **Fix**: Review diff size, may need to split into multiple PRs
- **Issue**: "Deploy readiness NOT READY"
  - **Fix**: Check for uncommitted changes or merge conflicts

---

### Phase 7: Deployment

**Purpose**: Synchronize changes to NinjaTrader hard links

**Command**:
```bash
powershell -File .\deploy-sync.ps1
```

**What Happens**:
1. Verifies all changes committed
2. Runs pre-push validation
3. Synchronizes src/ to NinjaTrader hard links
4. Verifies hard link integrity
5. Runs final build in NinjaTrader context

**Verification**:
```bash
# Check deploy-sync output
# Expected: "✅ Hard links synchronized successfully"

# Test in NinjaTrader
# 1. Open NinjaTrader
# 2. Press F5 to compile
# 3. Verify zero errors
```

**Success Criteria**:
- ✅ deploy-sync completes without errors
- ✅ Hard links synchronized
- ✅ NinjaTrader F5 compiles successfully
- ✅ No runtime errors in NinjaTrader

**Common Issues**:
- **Issue**: "Hard link sync failed"
  - **Fix**: Check file permissions, run as administrator
- **Issue**: "NinjaTrader F5 fails"
  - **Fix**: Check for missing dependencies or namespace issues

## Parallel Execution

### Identifying Parallel Opportunities

**Check Manifest**:
```bash
python scripts/epic_manifest.py parallel EPIC-CCN-16
```

**Output**:
```json
{
  "parallel_groups": [
    {
      "group_id": "ticket_execution",
      "phases": ["5.1", "5.2", "5.3"],
      "description": "Independent tickets can run concurrently"
    },
    {
      "group_id": "verification",
      "phases": ["5.1.V", "5.2.V", "5.3.V"],
      "description": "Verifications can run concurrently"
    }
  ]
}
```

### Executing Phases in Parallel

**Using Multiple Terminals**:
```bash
# Terminal 1
epic-validate EPIC-CCN-16 --ticket 1

# Terminal 2
epic-validate EPIC-CCN-16 --ticket 2

# Terminal 3
epic-validate EPIC-CCN-16 --ticket 3
```

**Using GNU Parallel** (Linux/macOS):
```bash
parallel epic-validate EPIC-CCN-16 --ticket ::: 1 2 3
```

**Using PowerShell Jobs** (Windows):
```powershell
$jobs = @()
$jobs += Start-Job { epic-validate EPIC-CCN-16 --ticket 1 }
$jobs += Start-Job { epic-validate EPIC-CCN-16 --ticket 2 }
$jobs += Start-Job { epic-validate EPIC-CCN-16 --ticket 3 }
$jobs | Wait-Job | Receive-Job
```

### Monitoring Parallel Execution

**Watch Manifest Updates**:
```bash
watch -n 5 'python scripts/epic_manifest.py status EPIC-CCN-16'
```

**Check Individual Phase Status**:
```bash
python scripts/epic_manifest.py phase EPIC-CCN-16 5.1
python scripts/epic_manifest.py phase EPIC-CCN-16 5.2
python scripts/epic_manifest.py phase EPIC-CCN-16 5.3
```

## Troubleshooting

### Common Issues

#### Issue: "Dependencies not satisfied"

**Symptom**:
```
Error: Phase 1.5 cannot start - dependencies not satisfied
Missing: Phase 1 (status: in_progress)
```

**Diagnosis**:
```bash
python scripts/epic_manifest.py dependencies EPIC-CCN-16 1.5
```

**Fix**:
```bash
# Complete Phase 1 first
epic-scope-boundary EPIC-CCN-16 --phase 1

# Then retry Phase 1.5
epic-scope-boundary EPIC-CCN-16 --phase 1.5
```

---

#### Issue: "Manifest not found"

**Symptom**:
```
Error: Manifest not found for EPIC-CCN-16
```

**Diagnosis**:
```bash
ls docs/brain/EPIC-CCN-16/manifest.json
```

**Fix**:
```bash
# If manifest missing, reinitialize epic
epic-intake EPIC-CCN-16 "Description"
```

---

#### Issue: "Phase failed"

**Symptom**:
```
Error: Phase 5.1 failed - build errors detected
```

**Diagnosis**:
```bash
# Check phase details
python scripts/epic_manifest.py phase EPIC-CCN-16 5.1

# Check completion report
cat docs/brain/EPIC-CCN-16/ticket-1-completion.md
```

**Fix**:
```bash
# 1. Review error details
cat docs/brain/EPIC-CCN-16/ticket-1-completion.md

# 2. Fix issues manually
# (edit files as needed)

# 3. Reset phase to pending
python scripts/epic_manifest.py reset EPIC-CCN-16 5.1

# 4. Retry phase
epic-validate EPIC-CCN-16 --ticket 1
```

---

#### Issue: "Manifest corrupted"

**Symptom**:
```
Error: Invalid JSON in manifest.json
```

**Diagnosis**:
```bash
python -m json.tool docs/brain/EPIC-CCN-16/manifest.json
```

**Fix**:
```bash
# 1. Backup corrupted manifest
cp docs/brain/EPIC-CCN-16/manifest.json docs/brain/EPIC-CCN-16/manifest.json.bak

# 2. Restore from git
git restore docs/brain/EPIC-CCN-16/manifest.json

# 3. If no git version, regenerate
python scripts/epic_manifest.py regenerate EPIC-CCN-16
```

---

#### Issue: "Parallel execution conflicts"

**Symptom**:
```
Error: Manifest update conflict - file locked
```

**Diagnosis**:
```bash
# Check for lock file
ls docs/brain/EPIC-CCN-16/.manifest.lock
```

**Fix**:
```bash
# 1. Wait for lock to release (max 30 seconds)
sleep 30

# 2. If lock persists, remove manually
rm docs/brain/EPIC-CCN-16/.manifest.lock

# 3. Retry operation
```

### Recovery Procedures

#### Rollback Failed Phase

```bash
# 1. Identify failed phase
python scripts/epic_manifest.py status EPIC-CCN-16

# 2. Rollback phase
python scripts/epic_manifest.py rollback EPIC-CCN-16 5.1

# 3. Verify rollback
python scripts/epic_manifest.py phase EPIC-CCN-16 5.1
# Expected: status = "pending"

# 4. Retry phase
epic-validate EPIC-CCN-16 --ticket 1
```

#### Resume from Checkpoint

```bash
# 1. Check current status
python scripts/epic_manifest.py status EPIC-CCN-16

# 2. Get next phases
python scripts/epic_manifest.py next EPIC-CCN-16

# 3. Resume from next phase
# (execute commands for next phases)
```

#### Complete Epic Reset

```bash
# 1. Backup current state
cp -r docs/brain/EPIC-CCN-16 docs/brain/EPIC-CCN-16.backup

# 2. Reset manifest
python scripts/epic_manifest.py reset-all EPIC-CCN-16

# 3. Restart from Phase 0
epic-intake EPIC-CCN-16 "Description"
```

## Example: EPIC-CCN-16

### Epic Overview

**Goal**: Extract ProcessOrderUpdate complexity (CYC 28 → 15)

**Scope**:
- 1 method: `ProcessOrderUpdate`
- 1 file: `src/V12_002.cs`
- 3 helper extractions

### Complete Execution

```bash
# Phase 0: Hotspot Analysis
epic-intake EPIC-CCN-16 "Extract ProcessOrderUpdate complexity (CYC 28)"
# Output: 00-hotspots.md identifies ProcessOrderUpdate (CYC 28)

# Phase 1: Scope Definition
epic-scope-boundary EPIC-CCN-16 --phase 1
# Output: 00-scope.md defines scope (1 method, 1 file, 3 helpers)

# Phase 1.5: Scope Boundary Validation
epic-scope-boundary EPIC-CCN-16 --phase 1.5
# Output: 01-scope-boundary.md validates scope (PASS)

# Phase 2: Architecture Planning
epic-plan EPIC-CCN-16
# Output: 02-architecture-plan.md designs 3 helpers
#   - ValidateOrderState (CYC -5)
#   - CalculateRiskMetrics (CYC -7)
#   - UpdateOrderStatus (CYC -6)

# Phase 3: DNA & PR Audit
epic-scan EPIC-CCN-16
# Output: 03-audit-report.md (DNA: PASS, PR Health: 85)

# Phase 4: Ticket Generation
epic-tickets EPIC-CCN-16
# Output: 04-tickets.md generates 3 tickets

# Phase 5.1: Execute Ticket 1
epic-validate EPIC-CCN-16 --ticket 1
# Output: ticket-1-completion.md (ValidateOrderState extracted, CYC -5)

# Phase 5.1.V: Verify Ticket 1
epic-verify-ticket EPIC-CCN-16 --ticket 1
# Output: ticket-1-verification.md (PASS)

# Phase 5.2: Execute Ticket 2
epic-validate EPIC-CCN-16 --ticket 2
# Output: ticket-2-completion.md (CalculateRiskMetrics extracted, CYC -7)

# Phase 5.2.V: Verify Ticket 2
epic-verify-ticket EPIC-CCN-16 --ticket 2
# Output: ticket-2-verification.md (PASS)

# Phase 5.3: Execute Ticket 3
epic-validate EPIC-CCN-16 --ticket 3
# Output: ticket-3-completion.md (UpdateOrderStatus extracted, CYC -6)

# Phase 5.3.V: Verify Ticket 3
epic-verify-ticket EPIC-CCN-16 --ticket 3
# Output: ticket-3-verification.md (PASS)

# Phase 6: Final Review
epic-review-final EPIC-CCN-16
# Output: 05-completion-report.md
#   - Total CYC reduction: 18 points (28 → 10)
#   - Build: PASS
#   - Tests: PASS
#   - PR Health: 90
#   - Deploy: READY

# Phase 7: Deployment
powershell -File .\deploy-sync.ps1
# Output: Hard links synchronized, NinjaTrader F5 successful
```

### Timeline

| Phase | Duration | Status |
|-------|----------|--------|
| 0 | 5 min | ✅ Completed |
| 1 | 10 min | ✅ Completed |
| 1.5 | 5 min | ✅ Completed |
| 2 | 20 min | ✅ Completed |
| 3 | 10 min | ✅ Completed |
| 4 | 5 min | ✅ Completed |
| 5.1 | 15 min | ✅ Completed |
| 5.1.V | 5 min | ✅ Completed |
| 5.2 | 15 min | ✅ Completed |
| 5.2.V | 5 min | ✅ Completed |
| 5.3 | 15 min | ✅ Completed |
| 5.3.V | 5 min | ✅ Completed |
| 6 | 20 min | ✅ Completed |
| 7 | 5 min | ✅ Completed |
| **Total** | **140 min** | **✅ Completed** |

### Results

**Before**:
- ProcessOrderUpdate: CYC 28
- File complexity: High
- Test coverage: 0%

**After**:
- ProcessOrderUpdate: CYC 10
- ValidateOrderState: CYC 3
- CalculateRiskMetrics: CYC 4
- UpdateOrderStatus: CYC 3
- File complexity: Medium
- Test coverage: 85%

## Best Practices

### 1. Always Check Status Before Proceeding

```bash
# Before starting any phase
python scripts/epic_manifest.py status EPIC-CCN-X
python scripts/epic_manifest.py next EPIC-CCN-X
```

### 2. Verify Dependencies Satisfied

```bash
# Before executing a phase
python scripts/epic_manifest.py dependencies EPIC-CCN-X <phase>
```

### 3. Use Parallel Execution When Possible

```bash
# Check for parallel opportunities
python scripts/epic_manifest.py parallel EPIC-CCN-X

# Execute independent phases concurrently
```

### 4. Commit After Each Phase

```bash
# After each phase completes
git add docs/brain/EPIC-CCN-X/
git commit -m "EPIC-CCN-X: Phase X completed"
```

### 5. Monitor Build Health

```bash
# After code-changing phases (5.X)
powershell -File .\scripts\build_readiness.ps1
```

### 6. Document Issues Immediately

```bash
# If issues arise, document in phase notes
python scripts/epic_manifest.py note EPIC-CCN-X <phase> "Issue description"
```

### 7. Use Checkpoints for Long Phases

```bash
# For phases >30 minutes, create checkpoints
python scripts/epic_manifest.py checkpoint EPIC-CCN-X <phase>
```

### 8. Validate Artifacts After Each Phase

```bash
# Verify output artifacts exist
ls docs/brain/EPIC-CCN-X/<artifact>

# Verify artifact content is valid
cat docs/brain/EPIC-CCN-X/<artifact>
```

### 9. Keep Manifest Clean

```bash
# Periodically validate manifest
python scripts/epic_manifest.py validate EPIC-CCN-X
```

### 10. Archive Completed Epics

```bash
# After epic completion
python scripts/epic_manifest.py archive EPIC-CCN-X
```

## Next Steps

- **For New Epics**: Start with Phase 0 (epic-intake)
- **For In-Progress Epics**: Check status and resume from next phase
- **For Failed Epics**: Review troubleshooting section and recovery procedures
- **For Migration**: See `docs/workflow/EPIC_WORKFLOW_MIGRATION_GUIDE.md`

## References

- **V12 Epic Workflow Design**: `docs/workflow/V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md`
- **Manifest Schema**: `docs/workflow/EPIC_MANIFEST_SCHEMA.md`
- **Watsonx Integration**: `docs/workflow/WATSONX_ORCHESTRATE_INTEGRATION.md`
- **Migration Guide**: `docs/workflow/EPIC_WORKFLOW_MIGRATION_GUIDE.md`
- **V12 DNA Principles**: `AGENTS.md`

---

**Document Status**: Active v1.0  
**Last Updated**: 2026-06-09  
**Maintainer**: V12 Architecture Team