---
description: NinjaTrader Add-On builder workflow. Runs pre-flight checks in the Director workspace, then outputs a single formatted prompt for the user to paste into a new ptt-orchestrator session (Tier 2). Tier 2 owns all start_subtask chaining.
argument-hint: <spec-file> <brain-dir> [--ticket N]
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
        epic           = "$2"
        spec           = "specs/$1"
        phase          = "pending"
        tickets        = @{}
        lamport_events = @()
    } | ConvertTo-Json -Depth 4 | [System.IO.File]::WriteAllText("docs/brain/$2/manifest.json", $_, (New-Object System.Text.UTF8Encoding $false))
}

# Check 6: Locate previous block's deferred backlog (if any) and surface OPEN items
$prevBacklog = $null
$prevBacklogPath = $null
Get-ChildItem "docs/brain" -Directory | Sort-Object Name -Descending | ForEach-Object {
    if ($_.Name -ne "$2" -and (Test-Path "$($_.FullName)/06-deferred-backlog.md")) {
        if (-not $prevBacklogPath) {
            $prevBacklogPath = "$($_.FullName)/06-deferred-backlog.md"
            $prevBacklog = Get-Content $prevBacklogPath -Raw
        }
    }
}
if ($prevBacklogPath) {
    Write-Host "  backlog : $prevBacklogPath -- OPEN items will be passed to Architect"
} else {
    Write-Host "  backlog : none found (first block or no prior 06-deferred-backlog.md)"
}

# Check 7: Initialize Lamport event log for this epic (idempotent)
$lamportDir = ".lamport/ptt/$2"
New-Item -ItemType Directory -Force $lamportDir | Out-Null
if (-not (Test-Path "$lamportDir/global_clock.json")) {
    @{ clock = 0; updated_at = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ") } `
        | ConvertTo-Json `
        | [System.IO.File]::WriteAllText("$lamportDir/global_clock.json", $_, (New-Object System.Text.UTF8Encoding $false))
    Write-Host "  lamport : $lamportDir/ -- clock initialized at 0"
} else {
    $clock = (Get-Content "$lamportDir/global_clock.json" | ConvertFrom-Json).clock
    Write-Host "  lamport : $lamportDir/ -- clock resume at $clock"
}

# Check 8: Show current pipeline status (resume detection)
if (Test-Path "$lamportDir/event_log.jsonl") {
    $eventCount = (Get-Content "$lamportDir/event_log.jsonl" | Measure-Object -Line).Lines
    Write-Host "  events  : $eventCount events in log (resume mode)"
    python scripts/ptt_lamport.py status "$2"
} else {
    Write-Host "  events  : 0 events (fresh start)"
}
```

**On any failure (Checks 1–5):** Print the failed check, then STOP. Do not produce the Tier 2 prompt.
Checks 6–8 never fail — missing backlog/lamport are normal for Block 1.

**On all checks green:** Print:

```
PRE-FLIGHT: PASS
  spec    : specs/$1 (ok)
  rules   : docs/standards/jane-street/RULES_CATALOG.md (ok)
  brain   : docs/brain/$2/ (ok, manifest initialized)
  backlog : {$prevBacklogPath or "none"}
  lamport : .lamport/ptt/$2/ (ok, clock initialized or resumed)
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
Run the full 7-phase pipeline below using start_subtask.
All artifacts live in the Director workspace (c:\WSGTA\universal-or-strategy-director).
C# source files are written to the Wave workspace (c:\WSGTA\universal-or-strategy).

INPUTS
  Spec      : specs/$1
  Brain dir : docs/brain/$2/
  Rules     : docs/standards/jane-street/RULES_CATALOG.md
  Protocol  : docs/protocol/PTT_WORKSPACE_PROTOCOL.md
  Ticket    : $3   (blank = all tickets)
  Lamport   : .lamport/ptt/$2/event_log.jsonl

LAMPORT CLOCK PROTOCOL (MANDATORY -- every phase, no exceptions):
  Before spawning ANY subtask:
    python scripts/ptt_lamport.py gate $2 <phase_slug>
    Exit 1 = GATE CLOSED -- prerequisite incomplete, do NOT spawn, escalate to Director.
    Exit 0 = GATE OPEN -- proceed, phase_start already logged.
  After each subtask returns:
    python scripts/ptt_lamport.py complete $2 <phase_slug> <RESULT_TOKEN>   (on pass)
    python scripts/ptt_lamport.py fail     $2 <phase_slug> "<reason>"        (on fail)
  Resume check (if event_log.jsonl has events):
    python scripts/ptt_lamport.py status $2
    Skip any phase already showing [DONE] -- do not re-run completed work.

PHASE SLUGS:  architect | plan_review | tickets | ticket_review |
              engineer_T1 | verifier_T1 | engineer_T2 | verifier_T2 |
              engineer_T3 | verifier_T3 | final_review

QUALITY CHAIN (what each phase validates):
  Phase 1  : Architect produces plan
  Phase 2  : Plan reviewed vs spec + requirements + RULES_CATALOG.md
  Phase 3  : Tickets generated from REVIEW_PASS plan + spec + RULES_CATALOG.md
  Phase 3.5: Tickets reviewed vs plan + spec + RULES_CATALOG.md
  Phase 4  : Each ticket implemented + independently verified vs ticket + plan + spec + RULES_CATALOG.md
  Phase 5  : Full epic reviewed vs plan + RULES_CATALOG.md (cross-file coherence)

--- PHASE 1: ARCHITECT ---
LAMPORT: python scripts/ptt_lamport.py gate $2 architect
start_subtask(mode="ptt-architect", title="PTT Architect: $2")
  Read: specs/$1
        docs/standards/jane-street/RULES_CATALOG.md
        {prevBacklogPath if found in pre-flight -- READ ONLY, pass OPEN items as context}
  Architect Thought 1 MUST cover: which OPEN deferred items from prior block are
    addressed in this block vs deferred further. If no prior backlog: state "no prior debt".
  Write: docs/brain/$2/02-architecture-plan.md
  Return: PLAN_COMPLETE | PLAN_FAILED
LAMPORT (on return): complete/fail based on return token.
Gate: stop on PLAN_FAILED, report reason.

--- PHASE 2: PLAN REVIEWER ---
LAMPORT: python scripts/ptt_lamport.py gate $2 plan_review
start_subtask(mode="ptt-plan-reviewer", title="PTT Plan Review: $2")
  Read: docs/brain/$2/02-architecture-plan.md
        specs/$1
        docs/standards/jane-street/RULES_CATALOG.md
  Write: docs/brain/$2/02-plan-review.md
  Return: REVIEW_PASS | REVIEW_FAIL (list violations with exact rule citation)
LAMPORT (on return): complete/fail based on return token.
Gate: on REVIEW_FAIL re-spawn ptt-architect with violations appended (max 2 cycles).
After 2 REVIEW_FAIL: stop, escalate to Director.

--- PHASE 3: TICKET GENERATION ---
LAMPORT: python scripts/ptt_lamport.py gate $2 tickets
start_subtask(mode="ptt-architect", title="PTT Tickets: $2")
  Read: docs/brain/$2/02-architecture-plan.md (REVIEW_PASS confirmed)
        specs/$1
        docs/standards/jane-street/RULES_CATALOG.md
  Write: docs/brain/$2/04-tickets.md
  Format: one section per ticket (T1/T2/T3):
    - Spec requirement ID(s) this ticket satisfies
    - File path in Wave workspace
    - All method signatures to implement
    - JS rule constraints that apply to each method
    - xUnit tests to write ([Fact] method names + what they assert)
    - 7-scan checklist
  Return: TICKETS_COMPLETE
LAMPORT (on return): python scripts/ptt_lamport.py complete $2 tickets TICKETS_COMPLETE

--- PHASE 3.5: TICKET REVIEWER ---
LAMPORT: python scripts/ptt_lamport.py gate $2 ticket_review
start_subtask(mode="ptt-ticket-reviewer", title="PTT Ticket Review: $2")
  Read: docs/brain/$2/04-tickets.md
        docs/brain/$2/02-architecture-plan.md
        specs/$1
        docs/standards/jane-street/RULES_CATALOG.md
  Check per ticket:
    1. TRACEABILITY: Does every ticket item map to a spec requirement or plan item?
       Any phantom work (in ticket, not in plan/spec)? Any missing work (in plan, not in ticket)?
    2. JS PRE-CHECK: Do the described implementations violate any JS rule?
       - lock() planned? FAIL
       - throw in dispatch path? FAIL
       - null return? FAIL
       - Dictionary<K,V> (mutable, non-concurrent)? FAIL
       - mutable struct? FAIL
       - DateTime.Now? FAIL
       - hardcoded hex color? FAIL
       - FontFamily override? FAIL
       - CreateOrder without PTT- prefix? FAIL
    3. CYC PRE-CHECK: Does each described method stay <= 8 branches?
       Flag any method with more than 2 nested conditions as CYC risk.
    4. NT8 CONSTRAINT CHECK:
       - lifecycle methods async/await? FAIL
       - off-thread UI callbacks without Dispatcher.InvokeAsync? FAIL
       - Account.All outside Loaded handler? FAIL
       - TradeCopierWindow sealed? FAIL
    5. COMPLETENESS: Are all in-scope files addressed (CopyEngine / Panel / Window)?
    6. TEST COVERAGE: Does every new method have a [Fact] test specified?
    7. SCAN CHECKLIST: Does each ticket include all 7 scans?
  Write: docs/brain/$2/04-ticket-review.md
    Include: per-ticket verdict, any violations with exact plan/spec/rule citation
  Return: TICKET_REVIEW_PASS | TICKET_REVIEW_FAIL (list violations per ticket)
LAMPORT (on return): complete/fail based on return token.
Gate: on TICKET_REVIEW_FAIL re-spawn ptt-architect (Phase 3) with violations (max 2 cycles).
After 2 TICKET_REVIEW_FAIL: stop, escalate to Director.

--- PHASE 4: ENGINEER + VERIFIER LOOP (per ticket) ---
For each ticket (T1, T2, T3 -- or only $3 if specified):

  4a. ENGINEER
  LAMPORT: python scripts/ptt_lamport.py gate $2 engineer_TN
  start_subtask(mode="ptt-engineer", title="PTT Engineer: $2 TN")
    Read: docs/brain/$2/04-tickets.md (ticket N only)
          docs/brain/$2/04-ticket-review.md (TICKET_REVIEW_PASS confirmed)
          docs/brain/$2/02-architecture-plan.md
          docs/standards/jane-street/RULES_CATALOG.md
          [on retry]: docs/brain/$2/ticket-N-verification.md -- fix ONLY cited violations
    Write: src/PropTraderTools/[File].cs  (Wave workspace)
           docs/brain/$2/ticket-N-completion.md
    Run all 7 scans to zero before returning.
    Return: BUILD_PASS | BUILD_FAIL (include compiler error)
  LAMPORT (on return): complete/fail based on return token.
  Gate: on BUILD_FAIL re-spawn engineer with error (max 2 build retries).

  4b. VERIFIER
  LAMPORT: python scripts/ptt_lamport.py gate $2 verifier_TN
  start_subtask(mode="ptt-verifier", title="PTT Verify: $2 TN")
    Read: src/PropTraderTools/[File].cs  (Wave workspace -- READ ONLY)
          docs/brain/$2/02-architecture-plan.md
          docs/brain/$2/04-tickets.md (ticket N only)
          docs/brain/$2/ticket-N-completion.md
          specs/$1
          docs/standards/jane-street/RULES_CATALOG.md
    Write: docs/brain/$2/ticket-N-verification.md
    Run all 7 scans independently. Do NOT trust engineer scan results.
    Check: implementation satisfies ticket AND plan AND spec requirement.
    Return: VERIFY_PASS | VERIFY_FAIL (exact file+line violations)
  LAMPORT (on return): complete/fail based on return token.
  Gate: on VERIFY_FAIL re-spawn engineer with verification report (max 3 cycles).
  After 3 VERIFY_FAIL: stop, escalate to Director.

--- PHASE 5: FINAL REVIEW ---
After ALL tickets reach VERIFY_PASS:
LAMPORT: python scripts/ptt_lamport.py gate $2 final_review
  (gate enforces ALL verifier_T1/T2/T3 complete before final_review can run)
start_subtask(mode="ptt-plan-reviewer", title="PTT Final Review: $2")
  Read: docs/brain/$2/02-architecture-plan.md
        docs/brain/$2/04-ticket-review.md
        docs/brain/$2/ticket-1-completion.md through ticket-3-completion.md
        docs/brain/$2/ticket-1-verification.md through ticket-3-verification.md
        specs/$1
        docs/standards/jane-street/RULES_CATALOG.md
        {prevBacklogPath} (prior OPEN items to close or carry forward -- READ ONLY)
  Write: docs/brain/$2/05-final-review.md  (MUST include Section K -- Deferred Work)
         docs/brain/$2/06-deferred-backlog.md  (REQUIRED -- append this block's entries)
  Check (all required):
    - CopyEngine + TradeCopierPanel + TradeCopierWindow form a complete coherent system?
    - Any cross-file JS violations? (lock, throw in dispatch, mutable struct, null return, DateTime.Now)
    - Any missing wiring between files?
    - All spec requirements satisfied end-to-end?
    - All 7 scans confirmed zero across entire src/PropTraderTools/ directory?
  Section K REQUIRED FORMAT (in 05-final-review.md):
    ## Section K -- Deferred Work / Block Backlog
    | ID          | Item                        | Priority | Target Block | Status |
    |-------------|-----------------------------|----------|--------------|--------|
    | DW-$2-01    | <description>               | P0/P1/P2 | B5/B6/future | OPEN   |
    Prior OPEN items from prevBacklog must be listed as CLOSED (if done this block) or OPEN.
    Minimum one row required. If nothing deferred: one row with "None -- all scope complete".
  06-deferred-backlog.md REQUIRED FORMAT (append block section):
    ## $2 -- Deferred Items
    | ID | Item | Priority | Target Block | Status |
    (same rows as Section K, plus status updates for prior OPEN items)
  FINAL_PASS is BLOCKED if:
    - Section K absent from 05-final-review.md
    - 06-deferred-backlog.md not written / not updated for this block
  Return: FINAL_PASS | FINAL_FAIL
LAMPORT (on return): python scripts/ptt_lamport.py complete $2 final_review FINAL_PASS

GATE -- DEFERRED BACKLOG EXISTENCE CHECK:
After FINAL_PASS: orchestrator must verify
  Test-Path "docs/brain/$2/06-deferred-backlog.md"
  If missing: re-spawn ptt-plan-reviewer once to produce it.
  If still missing after retry: PIPELINE_FAILED.

--- COMPLETION ---
On FINAL_PASS + backlog confirmed: reply with exactly:
  PIPELINE_COMPLETE: $2 -- all tickets verified, final review passed.
  Artifacts: docs/brain/$2/
  Source:    src/PropTraderTools/ (Wave workspace)
  Deferred:  docs/brain/$2/06-deferred-backlog.md
  Lamport:   .lamport/ptt/$2/event_log.jsonl (full causal audit trail)

On any unrecoverable failure: reply with:
  PIPELINE_FAILED: $2 -- [phase] [reason]

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

### Defense-in-Depth: 3-Layer Scan Chain

The 7 scans appear **three times** in every ticket's lifecycle. This is intentional defense in depth, not redundancy:

| Layer | Document | Who | Purpose |
|-------|----------|-----|---------|
| **Layer 1** | `04-tickets.md` (per ticket) | Architect writes it | Engineer's contract — explicit list of what must pass before BUILD_PASS |
| **Layer 2** | `ticket-N-completion.md` | Engineer self-reports | Attestation — proves scans were run in the correct Wave workspace |
| **Layer 3** | `ticket-N-verification.md` | Verifier re-runs independently | Cross-check — compares Layer 3 results against Layer 2 report |

**`ptt-ticket-reviewer` (Phase 3.5) enforces Layer 1:** Every ticket in `04-tickets.md` MUST include SCAN-01 through SCAN-07 as a checklist. Missing any scan from any ticket = `TICKET_REVIEW_FAIL`. This is non-negotiable and must never be flagged as redundant.

**`ptt-engineer` (Phase 4a) fulfills Layer 2:** Run all 7 scans to zero. Report every result in `ticket-N-completion.md`. Never return `BUILD_PASS` with any scan un-run.

**`ptt-verifier` (Phase 4b) runs Layer 3:** Re-run all 7 scans independently via `ctx_shell`. Compare against Layer 2. Any discrepancy = `VERIFY_FAIL`. Never trust engineer results.

### The 7 Scans

| # | Scan | Command | Expected |
|---|---|---|---|
| SCAN-01 | No `lock(` | `execute_lsp(workspace_symbols, query="lock")` (compiler-accurate) + `grep -r "lock(" src/PropTraderTools/` | 0 results both |
| SCAN-02 | ASCII-only | `Get-Content src/PropTraderTools/*.cs \| Where-Object {$_ -match '[^\x00-\x7F]'}` | 0 results |
| SCAN-03 | No FontFamily | `Select-String -Path src/PropTraderTools/*.cs -Pattern "FontFamily"` | 0 results |
| SCAN-04 | No hardcoded hex | `Select-String -Path src/PropTraderTools/*.cs -Pattern "#[0-9A-Fa-f]{6}"` | 0 results |
| SCAN-05 | PTT- prefix | Verify all `CreateOrder` calls use `"PTT-"` prefixed name | 0 violations |
| SCAN-06 | No DateTime.Now | `Select-String -Path src/PropTraderTools/*.cs -Pattern "DateTime\.Now[^U]"` | 0 results |
| SCAN-07 | No lock pattern | `Select-String -Path src/PropTraderTools/*.cs -Pattern "\block\s*\("` | 0 results |

Any scan with hits > 0 = immediate BUILD_FAIL / VERIFY_FAIL.

### Pipeline Team Map (quick reference for all phases)

```
Tier 1 Director   (copier-spec)       Pre-flight. Emits this prompt. Never writes .cs.
Tier 2 Orch       (ptt-orchestrator)  Drives start_subtask chain. Never writes .cs.
Phase 1           ptt-architect       Writes 02-architecture-plan.md
Phase 2           ptt-plan-reviewer   Plan vs spec+rules -> REVIEW_PASS gate
Phase 3           ptt-architect       Writes 04-tickets.md (each ticket: spec IDs +
                                        signatures + [Fact] tests + 7-scan checklist)
Phase 3.5         ptt-ticket-reviewer Tickets vs plan+spec+rules -> TICKET_REVIEW_PASS gate
                                        FAILS any ticket missing SCAN-01..07 checklist
Phase 4a          ptt-engineer        Implements C# in Wave. Runs Layer 2 scans.
Phase 4b          ptt-verifier        Re-runs Layer 3 scans independently. Cross-checks.
Phase 5           ptt-plan-reviewer   Final coherence. Writes deferred backlog.
```

## LSP MCP — ENGINEER AND VERIFIER USAGE

The `lsp-server` MCP is always available. All workers use `execute_lsp` via the single tool.

**Engineer (Phase 4a) — BEFORE writing each method:**
```
execute_lsp(operation="hover", uri="c:/WSGTA/universal-or-strategy/src/PropTraderTools/{File}.cs", line=N, character=N)
  → get exact NT8 base class method signature before overriding
execute_lsp(operation="definition", uri="...", line=N, character=N)
  → navigate to NT8 source to confirm API contract
execute_lsp(operation="document_symbols", uri="...", line=1, character=1)
  → POST-WRITE: confirm all ticket method names are present in the compiled file
```

**Verifier (Phase 4b) — INDEPENDENTLY, never trusting engineer results:**
```
execute_lsp(operation="document_symbols", uri="...")
  → confirm all ticket-required methods are present by exact name
execute_lsp(operation="workspace_symbols", query="lock")
  → compiler-accurate SCAN-01 supplement (0 results = clean)
execute_lsp(operation="incoming_calls", uri="...", line=N, character=N)
  → real call graph — confirms no orphaned methods, no unexpected callers
execute_lsp(operation="references", uri="...", line=N, character=N)
  → confirm every new public method is wired up somewhere
```

**Architect (Phase 1, Thought 7) — NT8 API surface, never from memory:**
```
execute_lsp(operation="workspace_symbols", query="NinjaTrader")
  → enumerate available NT8 types before designing against them
execute_lsp(operation="hover", uri="...", line=N, character=N)
  → live signature for Account.All, CreateOrder, Positions
```

---

## BRAIN DIRECTORY LAYOUT

```
docs/brain/$2/
  manifest.json              -- phase tracking (initialized by Tier 1 pre-flight)
  02-architecture-plan.md    -- Phase 1 output (architect)
  02-plan-review.md          -- Phase 2 output (plan reviewer)
  04-tickets.md              -- Phase 3 output (ticket generation)
  04-ticket-review.md        -- Phase 3.5 output (ticket reviewer) [NEW]
  ticket-1-completion.md     -- Phase 4a T1 engineer output
  ticket-1-verification.md   -- Phase 4b T1 verifier output
  ticket-2-completion.md     -- Phase 4a T2 engineer output
  ticket-2-verification.md   -- Phase 4b T2 verifier output
  ticket-3-completion.md     -- Phase 4a T3 engineer output
  ticket-3-verification.md   -- Phase 4b T3 verifier output
  05-final-review.md         -- Phase 5 output (final review) -- MUST include Section K
  06-deferred-backlog.md     -- Phase 5 output (deferred ledger) -- REQUIRED, blocks FINAL_PASS
```

## DEFERRED BACKLOG PROTOCOL

The `06-deferred-backlog.md` file is the **single source of truth** for all work not
implemented in the current block.

- **Written by:** `ptt-plan-reviewer` in Phase 5 (Final Review) -- every block, no exceptions
- **Read by:** `ptt-architect` in Phase 1 of the NEXT block (passed via Tier 1 pre-flight)
- **Gate:** `FINAL_PASS` and `PIPELINE_COMPLETE` are both BLOCKED if this file is missing
- **Format:** Append-only ledger -- each block adds a new `## {epic} -- Deferred Items` section
- **Lifecycle:** OPEN items from prior block are reviewed at Phase 1 of next block.
  Architect marks each as: addressed this block / deferred again / cancelled.

### Priority Definitions

| Level | Meaning |
|-------|---------|
| P0 | Blocks correctness -- must close within 1 block |
| P1 | Feature incomplete -- target next block |
| P2 | Nice to have / enhancement -- future block |
