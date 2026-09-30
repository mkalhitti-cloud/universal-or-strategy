---
name: nt-builder
description: >-
  NinjaTrader Add-On builder workflow. Runs pre-flight checks in the Director
  workspace, then outputs a single formatted prompt for the user to paste into a
  new ptt-orchestrator session (Tier 2). Tier 2 owns all start_subtask chaining.
metadata:
  user-invocable: true
  disable-model-invocation: true
  argument-hint: '<spec-file> <brain-dir> [--ticket N]'
---

# /nt-builder — NinjaTrader Add-On Build Orchestrator (Tier 1)

**Spec:** $1
**Brain Dir:** $2
**Ticket Filter:** $3 (optional — omit to run all tickets)

You are the **Tier 1 Director** for building a NinjaTrader 8 Add-On.

**Your job is exactly two things:**
1. Run pre-flight checks in the Director workspace.
2. Output a single, copy-paste-ready prompt for the user to start a new `ptt-orchestrator` session.

You do NOT call `start_subtask` yourself.
You do NOT write any C# code.
You do NOT produce an architecture plan.
All of that belongs to Tier 2 (`ptt-orchestrator`).

---

## STEP 1 — PRE-FLIGHT CHECKS (run these, stop on any failure)

```powershell
# Check 1: Spec file exists
Test-Path "specs/$1"

# Check 2: Rules catalog exists
Test-Path "docs/standards/jane-street/RULES_CATALOG.md"

# Check 3: Workspace protocol exists
Test-Path "docs/protocol/PTT_WORKSPACE_PROTOCOL.md"

# Check 4: Create brain dir if missing
New-Item -ItemType Directory -Force "docs/brain/$2"

# Check 5: Initialize manifest if missing
if (-not (Test-Path "docs/brain/$2/manifest.json")) {
    @{
        epic    = "$2"
        spec    = "specs/$1"
        phase   = "pending"
        tickets = @{}
    } | ConvertTo-Json | Set-Content "docs/brain/$2/manifest.json"
}
```

**On any failure:** Print the failed check, then STOP. Do not produce the Tier 2 prompt.

**On all checks green:** Print:

```
PRE-FLIGHT: PASS
  spec  : specs/$1 ✓
  rules : docs/standards/jane-street/RULES_CATALOG.md ✓
  brain : docs/brain/$2/ ✓ (manifest initialized)
```

Then proceed to Step 2.

---

## STEP 2 — OUTPUT THE TIER 2 PROMPT

Print ONLY the following block (verbatim, with $1 / $2 / $3 substituted).
The user will copy this entire block and paste it as the first message in a new
`ptt-orchestrator` Bob session.

---

```
=== PTT ORCHESTRATOR — PASTE THIS INTO A NEW ptt-orchestrator SESSION ===

You are the Tier 2 Orchestrator for building a NinjaTrader 8 Add-On.
Run the full 5-phase pipeline below using start_subtask.
All artifacts live in the Director workspace (c:\WSGTA\universal-or-strategy-director).
C# source files are written to the Wave workspace (c:\WSGTA\universal-or-strategy).

INPUTS
  Spec      : specs/$1
  Brain dir : docs/brain/$2/
  Rules     : docs/standards/jane-street/RULES_CATALOG.md
  Protocol  : docs/protocol/PTT_WORKSPACE_PROTOCOL.md
  Ticket    : $3   (blank = all tickets)

--- PHASE 1: ARCHITECT ---
start_subtask(mode="ptt-architect", title="PTT Architect: $2")
  Read: specs/$1, RULES_CATALOG.md
  Write: docs/brain/$2/02-architecture-plan.md
  Return: PLAN_COMPLETE | PLAN_FAILED
Gate: stop on PLAN_FAILED, report reason.

--- PHASE 2: PLAN REVIEWER ---
start_subtask(mode="ptt-plan-reviewer", title="PTT Plan Review: $2")
  Read: docs/brain/$2/02-architecture-plan.md, specs/$1, RULES_CATALOG.md
  Write: docs/brain/$2/02-plan-review.md
  Return: REVIEW_PASS | REVIEW_FAIL (list violations)
Gate: on REVIEW_FAIL re-spawn ptt-architect with violations appended (max 2 cycles).
After 2 REVIEW_FAIL: stop, escalate to Director.

--- PHASE 3: TICKET GENERATION ---
start_subtask(mode="ptt-architect", title="PTT Tickets: $2")
  Read: docs/brain/$2/02-architecture-plan.md (REVIEW_PASS confirmed)
  Write: docs/brain/$2/04-tickets.md
  Format: one section per ticket (T1/T2/T3):
    - File path in Wave workspace
    - All method signatures to implement
    - xUnit tests to write
    - 7-scan checklist
  Return: TICKETS_COMPLETE

--- PHASE 4: ENGINEER + VERIFIER LOOP (per ticket) ---
For each ticket (T1, T2, T3 — or only $3 if specified):

  4a. ENGINEER
  start_subtask(mode="ptt-engineer", title="PTT Engineer: $2 TN")
    Read: docs/brain/$2/04-tickets.md (ticket N only)
          docs/brain/$2/02-architecture-plan.md
          RULES_CATALOG.md
          [on retry]: docs/brain/$2/ticket-N-verification.md — fix ONLY cited violations
    Write: src/PropTraderTools/[File].cs  (Wave workspace)
           docs/brain/$2/ticket-N-completion.md
    Run all 7 scans to zero before returning.
    Return: BUILD_PASS | BUILD_FAIL (include compiler error)
  Gate: on BUILD_FAIL re-spawn engineer with error (max 2 build retries).

  4b. VERIFIER
  start_subtask(mode="ptt-verifier", title="PTT Verify: $2 TN")
    Read: src/PropTraderTools/[File].cs  (Wave workspace — READ ONLY)
          docs/brain/$2/02-architecture-plan.md
          docs/brain/$2/ticket-N-completion.md
          RULES_CATALOG.md
    Write: docs/brain/$2/ticket-N-verification.md
    Run all 7 scans independently. Do NOT trust engineer scan results.
    Return: VERIFY_PASS | VERIFY_FAIL (exact file+line violations)
  Gate: on VERIFY_FAIL re-spawn engineer with verification report (max 3 cycles).
  After 3 VERIFY_FAIL: stop, escalate to Director.

--- PHASE 5: FINAL REVIEW ---
After ALL tickets reach VERIFY_PASS:
start_subtask(mode="ptt-plan-reviewer", title="PTT Final Review: $2")
  Read: docs/brain/$2/02-architecture-plan.md
        docs/brain/$2/ticket-1-completion.md through ticket-3-completion.md
        docs/brain/$2/ticket-1-verification.md through ticket-3-verification.md
  Write: docs/brain/$2/05-final-review.md
  Check: CopyEngine + TradeCopierPanel + TradeCopierWindow form a complete coherent system?
         Any cross-file JS violations? Any missing wiring?
  Return: FINAL_PASS | FINAL_FAIL

--- COMPLETION ---
On FINAL_PASS: reply with exactly:
  PIPELINE_COMPLETE: $2 — all tickets verified, final review passed.
  Artifacts: docs/brain/$2/
  Source:    src/PropTraderTools/ (Wave workspace)

On any unrecoverable failure: reply with:
  PIPELINE_FAILED: $2 — [phase] [reason]

=== END ORCHESTRATOR PROMPT ===
```

---

## WORKSPACE ROUTING REFERENCE

| Work | Workspace | Path |
|---|---|---|
| Specs, docs, brain artifacts, plans | **Director** | `c:\WSGTA\universal-or-strategy-director` |
| C# source files (`src/`) | **Wave** | `c:\WSGTA\universal-or-strategy` |

These workspaces never cross-pollinate.
The engineer writes `src/` in Wave.
Everything else lives in Director.
The verifier reads `src/` from Wave (read-only) and writes its report to Director.

---

## SCAN REFERENCE (7 mandatory — run by engineer AND independently by verifier)

| # | Scan | Command | Expected |
|---|---|---|---|
| SCAN-01 | No `lock(` | `grep -r "lock(" src/PropTraderTools/` | 0 results |
| SCAN-02 | ASCII-only | `Get-Content src/PropTraderTools/*.cs \| Where-Object {$_ -match '[^\x00-\x7F]'}` | 0 results |
| SCAN-03 | No FontFamily | `Select-String -Path src/PropTraderTools/*.cs -Pattern "FontFamily"` | 0 results |
| SCAN-04 | No hardcoded hex | `Select-String -Path src/PropTraderTools/*.cs -Pattern "#[0-9A-Fa-f]{6}"` | 0 results |
| SCAN-05 | PTT- prefix | Verify all `CreateOrder` calls use `"PTT-"` prefixed name | 0 violations |
| SCAN-06 | No DateTime.Now | `Select-String -Path src/PropTraderTools/*.cs -Pattern "DateTime\.Now[^U]"` | 0 results |
| SCAN-07 | No lock pattern | `Select-String -Path src/PropTraderTools/*.cs -Pattern "\block\s*\("` | 0 results |

Any scan with hits > 0 = immediate BUILD_FAIL / VERIFY_FAIL.

---

## BRAIN DIRECTORY LAYOUT

```
docs/brain/$2/
  manifest.json              — phase tracking (initialized by Tier 1 pre-flight)
  02-architecture-plan.md    — Phase 1 output (architect)
  02-plan-review.md          — Phase 2 output (reviewer)
  04-tickets.md              — Phase 3 output (ticket gen)
  ticket-1-completion.md     — Phase 4 T1 engineer output
  ticket-1-verification.md   — Phase 4 T1 verifier output
  ticket-2-completion.md     — Phase 4 T2 engineer output
  ticket-2-verification.md   — Phase 4 T2 verifier output
  ticket-3-completion.md     — Phase 4 T3 engineer output
  ticket-3-verification.md   — Phase 4 T3 verifier output
  05-final-review.md         — Phase 5 output (final review)
```
