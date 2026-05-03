---
description: $mission -- V12 Standard Mission Execution Protocol. Contract + TDD RED/GREEN + sequential Orchestrator->Worker->Validator for every Engineer session. Use whenever launching a new Codex Engineer session or a multi-phase implementation run.
---

# $mission: V12 Standard Mission Execution Protocol

Invoke with `$mission` whenever you are launching any Engineer (Codex) implementation session.
This encodes the full pattern: Contract -> Orchestrator -> Worker -> Validator, sequentially.
One role at a time. No parallel sub-agents for src/ edits.

---

## What This Workflow Governs

Every Codex Engineer session in V12 MUST follow this structure:

1. **CONTRACT** -- Declared upfront. Defines pre-conditions, deliverables, and acceptance criteria
   before any code is written. This is the "Director's Gate" equivalent for execution.
2. **ORCHESTRATOR ROLE** -- Reads the plan. Verifies pre-conditions from the CONTRACT are met.
   Loads context. Does NOT write tests or code.
3. **WORKER ROLE** -- Owns the full TDD loop:
   a) Write the test first and confirm it FAILS (RED)
   b) Implement the plan
   c) Re-run the test and confirm it PASSES (GREEN)
4. **VALIDATOR ROLE** -- Independent post-check in clean context. Re-runs all tests plus
   DNA audits (lock scan, ASCII, build). Confirms no regressions.
5. **HANDOFF** -- Updates nexus_a2a.json. Tells Director to press F5.

---

## Step 1: Draft the Contract (Orchestrator does this before spawning Worker)

Before sending any prompt to Codex, write the CONTRACT for this session.
The contract has exactly three sections:

```
=== CONTRACT ===
PRE-CONDITIONS:
  - [List what must already be true before the Worker starts]
  - [Include: which plan file, which prior milestone, which scan state]
DELIVERABLES:
  - [List exactly which files will be created or modified]
  - [Be specific: file path, what changes]
ACCEPTANCE CRITERIA (all must be true for milestone PASS):
  1. [Grep/scan check with exact expected output]
  2. [Build check]
  3. [Any domain-specific check, e.g., BUILD_TAG visible]
If any criterion fails, the milestone is BLOCKED. Report to Director.
===
```

**Rule**: If you cannot write the contract because the plan is unclear, STOP.
Return to the Architect (Claude) before spawning Codex.

---

## Step 2: Orchestrator Role -- RED Gate

The first thing Codex does in the session is prove the problem EXISTS before touching code.

```
=== ORCHESTRATOR ROLE: Write the test first (RED gate) ===
Run these checks BEFORE any edits. Record exact output.

[Insert domain-specific checks here, e.g.:]
  powershell -File .\scripts\scan_deadlock_windows.ps1
  grep -c "TargetSymbol" src/TargetFile.cs

Expected: [specific violation count or absence that proves the problem is real]
Do NOT move to Worker role until RED confirmed.
If result is already GREEN: STOP. The problem may already be fixed. Report to Director.
```

---

## Step 3: Worker Role -- TDD Loop (Test First, Then Implement)

The Worker owns the full RED->GREEN cycle. This is classic TDD.

```
=== WORKER ROLE: TDD RED->GREEN ===

-- STEP 3a: Write the test (RED) --
Before touching any implementation file, write or identify the test that will
prove the problem is real. Run it. Confirm it FAILS.
[domain-specific: e.g., write a PowerShell assertion, grep check, or unit test]
Expected: FAIL. If it already passes, STOP -- problem may not exist. Report to Director.

-- STEP 3b: Implement --
PLAN: Read docs/brain/implementation_plan.md [Phase X]. Follow it exactly.
HARD RULES (non-negotiable):
- Touch ONLY the files listed in the plan
- Zero lock(stateLock) in any new code
- ASCII-only strings (no Unicode, curly quotes, em-dashes)
- No new abstractions beyond what the plan requires
- Every catch block MUST log -- never silent swallow
- After ANY src/ edit: deploy-sync.ps1 MUST run

If you discover scope NOT in the plan: STOP. Report to Director. Do not self-expand.

-- STEP 3c: Re-run the test (GREEN) --
Re-run the same test from Step 3a. Must now PASS.
If still FAILING: fix only the specific item. Re-run. Loop until GREEN.
Do NOT hand off to Validator until your own test passes.
```

---

## Step 4: Validator Role -- GREEN Gate

```
=== VALIDATOR ROLE: Confirm GREEN ===
Re-run the exact same checks from the ORCHESTRATOR ROLE.
All ACCEPTANCE CRITERIA from the CONTRACT must now PASS.

MANDATORY post-edit sequence:
1. powershell -File .\deploy-sync.ps1
2. powershell -File .\scripts\scan_deadlock_windows.ps1  (must return ZERO violations)
3. grep -r "lock(" src/                                   (must return ZERO matches)
4. powershell -File .\scripts\build_readiness.ps1         (must return BUILD READINESS PASS)

If any criterion FAILS:
- Identify the specific failing item
- Return to Worker role for that item only
- Re-run Validator
- Do NOT report PASS until ALL criteria are GREEN

If all GREEN:
- Tell Director: "TDD GREEN: [milestone name] PASS. All [N] acceptance criteria met."
- Tell Director: "Press F5 in NinjaTrader. Verify BUILD_TAG: [tag]"
```

---

## Step 5: Handoff Note

After Validator confirms GREEN, update `docs/brain/nexus_a2a.json`:

```json
{
  "phase": "P6_VALIDATION_PENDING",
  "last_relay": {
    "agent": "Codex",
    "milestone": "[milestone name]",
    "time": "<now UTC>",
    "status": "TDD_GREEN"
  },
  "p5_handoff": {
    "files_modified": ["src/file1.cs"],
    "contract_criteria_met": "N/N",
    "scan_violations": "0",
    "lock_audit": "0",
    "build": "PASS",
    "deploy_sync": "PASS"
  }
}
```

---

## Step 6: Multi-Milestone Sequencing

When a session covers multiple milestones (e.g., Phase 2 Part A then Part B):

- **Do NOT auto-proceed to the next milestone** after a GREEN.
- Report GREEN to Director.
- Wait for explicit Director approval before starting the next CONTRACT.
- This is the "milestone gate" -- the Director is the project manager.

```
After each milestone GREEN:
"Milestone [X] PASS. Awaiting Director approval to begin Milestone [X+1]."
```

---

## Step 7: Mandatory Post-Use Self-Improvement Audit

After the session ends:

1. Was any CONTRACT criterion ambiguous?
2. Did the RED gate surface a pre-existing GREEN (false positive)? If so, the scan needs tuning.
3. Did any Worker step discover undocumented scope? If so, the plan needs updating.

**If no gap found:** `workflow(mission): no gaps identified.`
**Commit format:** `workflow(mission): [what was fixed and why]`

---

## Quick Reference: V12 Mission Prompt Template

Use this as the base for every Codex Engineer prompt:

```
You are the V12 ENGINEER (Codex). Execute [MILESTONE NAME].
Use TDD: Orchestrator->Worker->Validator sequentially. One role at a time.

=== CONTRACT ===
PRE-CONDITIONS:
  - [list]
DELIVERABLES:
  - [list]
ACCEPTANCE CRITERIA:
  1. [check]
  2. [check]
If any criterion fails, milestone is BLOCKED. Report to Director.
===

REPO: c:\WSGTA\universal-or-strategy
PLAN: Read docs/brain/implementation_plan.md [Phase X]. Follow exactly.

=== ORCHESTRATOR ROLE: RED gate ===
[domain-specific pre-checks]

=== WORKER ROLE: Implement ===
[domain-specific tasks]

=== VALIDATOR ROLE: GREEN gate ===
[re-run same checks -- all must PASS]
[post-edit sequence: deploy-sync -> scan -> grep lock -> build]
Tell Director: "TDD GREEN: [milestone]. Press F5. Verify BUILD_TAG: [tag]"
```

---

## When to Use This Workflow

| Trigger | Action |
|:--------|:-------|
| `$mission` | Load this workflow. Draft the CONTRACT before spawning Codex. |
| Any new Codex Engineer session | Always use this structure. No exceptions. |
| Director says "run it" | Contract must exist before Worker starts. |
| Validator returns FAIL | Loop Worker->Validator only. Do NOT re-run Orchestrator. |
| Scope creep discovered | STOP Worker. Report to Director. Update plan. Restart. |
