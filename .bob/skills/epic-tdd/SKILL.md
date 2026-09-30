---
name: epic-tdd
description: >-
  Manual TDD-mode Epic Execution. Provides a structured path for manual ticket
  execution while maintaining autonomous /pr-loop hardening after every commit.
metadata:
  user-invocable: true
  disable-model-invocation: false
  argument-hint: <epic-slug> <ticket-number> <pr-number>
---

# EPIC TDD -- MANUAL EXECUTION
**Epic Slug:** $1
**Ticket:** $2
**Target PR:** $3
**Mode:** Engineer (Manual-parity)
**Protocol:** V12 High-Precision TDD Gate

You are the V12 TDD Engineer. You are executing ticket $2 of epic $1 manually.
Your goal is to implement the changes and drive them to 100/100 PHS on PR #$3.

---

## VERIFICATION GATES (MANDATORY)

All 4 gates from epic-run MUST be executed before implementation:

### GATE 1: INTAKE (Director Approval)
**Switch to: v12-epic-planner mode**

Hand off this exact task:
```
EPIC: $1
TASK: Review ticket scope in docs/brain/$1/ticket-$2.md
OUTPUT: Summarize ticket scope, CYC targets, and extraction plan
STOP at [INTAKE-GATE] and do not proceed.
```

When v12-epic-planner outputs [INTAKE-GATE], present the summary to Director.

**GATE 1 CHECKPOINT:**
> "Ticket scope: [summary]. Does this match your intent? Reply YES to proceed or give corrections."

- YES: advance to GATE 2
- Corrections: relay to v12-epic-planner, re-run intake

---

### GATE 2: PLAN REVIEW (Independent Agent)
**Switch to: v12-epic-planner mode**

Hand off this exact task:
```
EPIC: $1
TASK: Review extraction plan in docs/brain/$1/ticket-$2.md
VERIFY:
  - Sub-method names are descriptive
  - CYC targets are realistic
  - No logic duplication
  - Caller impact is minimal
OUTPUT: Write plan review to docs/brain/$1/ticket-$2-review.md
STOP at [PLAN-GATE] and do not proceed.
```

When v12-epic-planner outputs [PLAN-GATE], present key findings.

**GATE 2 CHECKPOINT:**
> "Plan review complete. Key findings: [top 3]. Type APPROVED to proceed or provide feedback."

- APPROVED: advance to GATE 2.3
- Feedback: relay to v12-epic-planner, re-run plan review

---

### GATE 2.3: SENTINEL AUDIT (Manual Review)
**MANUAL TASK (jcodemunch-mcp not working)**

Director must manually verify:
1. Read ticket file: `docs/brain/$1/ticket-$2.md`
2. Use Semgrep or manual audit to check:
   - No new `lock()` statements
   - ASCII-only compliance
   - FSM/Actor pattern followed
   - No hardcoded secrets
3. Document findings in: `docs/brain/$1/ticket-$2-sentinel.md`

**GATE 2.3 CHECKPOINT:**
> "Sentinel Audit complete. Verdict: [PASSED/REVISION REQUIRED]. Gaps found: [list]. Reply GO to proceed to GATE 3 or REVISE to update the plan."

- GO: advance to GATE 3
- REVISE: update ticket, re-run GATE 2

---

### GATE 3: DNA VALIDATION (Compliance Check)
**Switch to: v12-epic-planner mode**

Hand off this exact task:
```
EPIC: $1
TASK: Validate ticket against V12 DNA principles
INPUT: @docs/brain/$1/ticket-$2.md @docs/standards/JANE_STREET_DEVIATIONS.md
VERIFY:
  - Zero locks (FSM/Actor pattern)
  - ASCII-only (no Unicode)
  - Complexity ≤15 target
  - No illegal states possible
OUTPUT: Update ticket-$2.md with DNA compliance notes
STOP at [VALIDATE-GATE] and do not proceed.
```

When v12-epic-planner outputs [VALIDATE-GATE], present compliance summary.

**GATE 3 CHECKPOINT:**
> "DNA validation complete. [N issues resolved]. Type GO to begin implementation or HOLD to review."

- GO: advance to Step 1 (Implementation)
- HOLD: wait for Director review, then re-validate

---

## EXECUTION WORKFLOW

### Step 1: Implementation
Execute the changes described in `docs/brain/$1/ticket-$2.md`.
Ensure:
- ✅ Zero locks.
- ✅ ASCII only.
- ✅ FSM/Actor pattern followed.

### Step 2: Local Verification
Run the FULL validation suite:
1. `powershell -File .\deploy-sync.ps1`
2. `powershell -File .\scripts\pre_push_validation.ps1`
   (Includes CodeScene Delta Analysis as Check #14)

Expected output: ALL blocking checks PASS (9/14 required: ASCII, Build, Tests, Lint, Formatting, PR Hygiene, Complexity, CodeScene Delta, Jane Street DNA).

If ANY blocking check fails: **HALT and fix.**

### Step 3: F5 Verification
1. Press F5 in NinjaTrader.
2. Verify the BUILD_TAG banner.
3. Confirm the Logic Audit passes.

### Step 4: Commit & Push
1. `git add .`
2. `git commit -m "[$1] ticket-$2: manual TDD implementation"`
3. `git push`

### Step 5: Autonomous Perfection Gate
**Run: /pr-loop**

Immediately after pushing, hand off to the perfection loop:
```
EPIC: $1
TASK: Run /pr-loop $3
GOAL: Drive the ticket to 100/100 PHS.
STOP when /pr-loop outputs [PHS-PERFECT].
```

---

## COMPLETION
Once /pr-loop is 100/100, report status to Director:
"Ticket $2 complete. 100/100 PHS achieved. Proceed to next ticket?"

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
