# EPIC Execution Prompts for Bob Shell (V12.22)

## Overview
This document provides the exact prompts to use in Bob Shell (`v12-engineer` mode) for executing EPICs. Use these prompts **in order** for each EPIC.

---

## Pre-Execution Setup (One-Time)

### 1. Open Bob Shell Terminal
```bash
# In PowerShell terminal
bob --mode v12-engineer
```

### 2. Verify Protocol Awareness
**Prompt to Bob Shell:**
```
Before we start EPIC work, please confirm your protocol awareness by reading:
1. .bob/rules-v12-engineer/00-epic-readiness-checklist.md
2. docs/protocol/BRANCH_STRATEGY.md
3. docs/protocol/SRC_ONLY_PUSH.md

Then provide the mandatory confirmation statement from the checklist.
```

**Expected Response:**
Bob should state the 7-point confirmation from the checklist.

---

## EPIC Execution Sequence (Per EPIC)

### Phase 1: Branch Setup

**Prompt 1: Create Feature Branch**
```
Create a new feature branch for EPIC-8 following the three-tier branch strategy:
- Branch name: feature/src-epic-8-s1-extract-onkeydown
- Verify we're on the correct branch
- Confirm no non-.cs files are staged
```

**Expected Actions:**
- `git checkout -b feature/src-epic-8-s1-extract-onkeydown`
- `git status` (verify clean)

---

### Phase 2: Ticket Analysis

**Prompt 2: Load and Verify Ticket**
```
Read the EPIC-8 ticket brief from docs/brain/epic-8/ticket-01-extract-onkeydown.md

Then verify against live source:
1. Use jCodemunch to search for OnKeyDown method
2. Verify the stated line numbers match current src/
3. Verify the stated CYC value (45) matches complexity_audit.py output
4. Report any discrepancies before proceeding
```

**Expected Actions:**
- Read ticket file
- `search_symbols` for OnKeyDown
- `get_symbol_source` for full method
- `python scripts/complexity_audit.py` (verify CYC)

---

### Phase 3: Planning

**Prompt 3: Create Extraction Plan**
```
Based on the verified ticket and live source, produce a written PLAN:

1. Target structure (what methods to extract)
2. Helper method names (follow V12 naming conventions)
3. Caller impact analysis (use get_blast_radius)
4. Validation against V12 DNA:
   - No locks introduced
   - ASCII-only strings
   - CYC < 20 per extracted method
   - Zero logic drift

STOP after producing the plan. Wait for my approval before executing.
```

**Expected Output:**
- Written plan in markdown format
- Clear extraction strategy
- Blast radius analysis
- DNA compliance verification

**Your Response:**
```
APPROVED - proceed with execution
```

---

### Phase 4: Surgical Execution

**Prompt 4: Execute Extraction**
```
Execute the approved plan:

1. Extract methods as planned (surgical edits only)
2. Touch ONLY the files in scope (no scope creep)
3. Maintain zero logic drift (pure structural movement)
4. After each file edit:
   - Run: python scripts/complexity_audit.py
   - Verify CYC reduction
5. After all edits:
   - Run: powershell -File .\deploy-sync.ps1
   - Verify ASCII gate PASSES
6. Bump BUILD_TAG in src/V12_002.cs
7. Report: files modified, CYC before/after, deploy-sync result
```

**Expected Actions:**
- Surgical edits to src/ files
- Complexity verification after each edit
- Deploy-sync execution
- BUILD_TAG bump

---

### Phase 5: Pre-Push Validation

**Prompt 5: Run Pre-Push Checks**
```
Run pre-push validation before committing:

1. Verify ONLY src/ files are modified: git status
2. Run: powershell -File .\scripts\pre_push_validation.ps1 -Fast
3. If any check fails, fix before proceeding
4. Report validation results
```

**Expected Actions:**
- `git status` (verify src/ only)
- Pre-push validation script
- Fix any failures

---

### Phase 6: Commit and Push

**Prompt 6: Stage, Commit, Push**
```
Stage and commit changes following src-only protocol:

1. Stage ONLY src/ files: git add src/
2. Verify staging: git status (should show ONLY .cs files)
3. Commit with clear message: git commit -m "feat(epic-8): extract OnKeyDown handlers (CYC 45→12)"
4. Push to branch: git push origin feature/src-epic-8-s1-extract-onkeydown
5. Report push result and provide PR creation command
```

**Expected Actions:**
- `git add src/`
- `git status` (verify)
- `git commit -m "..."`
- `git push origin ...`

---

### Phase 7: PR Creation

**Prompt 7: Create Pull Request**
```
Create a pull request for EPIC-8:

1. Use GitHub CLI: gh pr create --title "EPIC-8: Extract OnKeyDown handlers" --body "..."
2. Or provide the GitHub URL for manual PR creation
3. Report PR number
```

**Expected Output:**
- PR created
- PR number reported

---

### Phase 8: PR Loop (100/100 PHS)

**Prompt 8: Run PR Loop**
```
Run the PR loop to achieve 100/100 Project Health Score:

1. Wait for initial bot reviews (Arena AI, Codacy, CodeRabbit)
2. Address any findings iteratively
3. Run /pr-loop workflow until PHS = 100/100
4. Report final PHS and any remaining manual review items
```

**Expected Actions:**
- Monitor PR checks
- Fix any bot findings
- Iterate until 100/100 PHS

---

## Quick Reference: All Prompts in Order

For copy-paste convenience, here are all prompts in sequence:

```
# 1. Protocol Awareness
Before we start EPIC work, please confirm your protocol awareness by reading .bob/rules-v12-engineer/00-epic-readiness-checklist.md, docs/protocol/BRANCH_STRATEGY.md, and docs/protocol/SRC_ONLY_PUSH.md. Then provide the mandatory confirmation statement.

# 2. Branch Setup
Create a new feature branch for EPIC-8: feature/src-epic-8-s1-extract-onkeydown. Verify we're on the correct branch and no non-.cs files are staged.

# 3. Ticket Analysis
Read docs/brain/epic-8/ticket-01-extract-onkeydown.md and verify against live source using jCodemunch. Confirm line numbers and CYC values match.

# 4. Planning
Produce a written PLAN for the extraction: target structure, helper names, caller impact, and V12 DNA validation. STOP and wait for approval.

# 5. Execution (after approval)
Execute the approved plan with surgical edits only. Run complexity_audit.py after each edit, then deploy-sync.ps1, then bump BUILD_TAG. Report results.

# 6. Pre-Push Validation
Run pre-push validation: verify git status shows only src/ files, then run pre_push_validation.ps1 -Fast. Report results.

# 7. Commit and Push
Stage src/ only, commit with clear message, push to branch. Report push result.

# 8. PR Creation
Create pull request using gh CLI or provide GitHub URL. Report PR number.

# 9. PR Loop
Run /pr-loop workflow until PHS = 100/100. Report final score.
```

---

## Parallel Execution (2 EPICs Simultaneously)

If running 2 EPICs in parallel (different subgraphs):

**Window 1 (Bob Shell #1):**
```
Execute EPIC-8 (S1: SIMA Core) using the prompts above
```

**Window 2 (Bob Shell #2):**
```
Execute EPIC-9 (S3: UI & Photon IO) using the prompts above
```

**Orchestrator (Bob IDE):**
```
Monitor both windows:
- Track phase progress
- Handle F5 gates sequentially
- Coordinate PR merges (first-complete, first-merge)
```

---

## Emergency Protocols

### If Bob Violates Branch Guard
```
STOP immediately. You attempted to stage non-.cs files on a feature/src-* branch.

Actions required:
1. Stash violating files: git stash push -m "infra-changes" -- <files>
2. Commit only .cs files
3. Report stashed files to Director for separate infra branch
```

### If Pre-Push Validation Fails
```
STOP. Pre-push validation failed on check: <check-name>

Actions required:
1. Fix the specific failure
2. Re-run validation
3. Do NOT push until all checks pass
```

### If Deploy-Sync ASCII Gate Fails
```
STOP. ASCII gate failed in deploy-sync.ps1

Actions required:
1. Identify non-ASCII characters in modified files
2. Replace with ASCII equivalents
3. Re-run deploy-sync.ps1
4. Verify PASS before proceeding
```

---

## Success Criteria (Per EPIC)

- ✅ Branch created following naming convention
- ✅ Protocol awareness confirmed
- ✅ Ticket verified against live source
- ✅ Written plan approved by Director
- ✅ Surgical edits executed (zero logic drift)
- ✅ Complexity reduced (CYC before/after documented)
- ✅ Deploy-sync PASSED (ASCII gate)
- ✅ BUILD_TAG bumped
- ✅ Pre-push validation PASSED (all checks)
- ✅ ONLY src/ files committed
- ✅ PR created with clear title/description
- ✅ PR loop completed (PHS = 100/100)
- ✅ Telemetry collected automatically

---

**Version:** V12.22  
**Effective Date:** 2026-05-31  
**Usage:** Copy prompts into Bob Shell terminal in order