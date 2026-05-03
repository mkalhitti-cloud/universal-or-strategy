---
description: $battlezip -- Post-Battle Forensic Synthesis, Go/No-Go Decision, Next Prompt, Dashboard Update
---

Use this workflow whenever the Director downloads zip files from an Arena AI battle and says `$battlezip`.
This is a fully end-to-end, non-interactive workflow. Complete ALL phases before stopping.

**Protocol Mandate**: Every $battlezip MUST produce five outputs before finishing:

1. **Hallucination Screen** (Phase 1b) -- disqualify hallucinating models BEFORE any synthesis
2. UltraThink Forensic Audit (matrix + winner + bug findings) -- clean models only
3. Go/No-Go Decision (compound again vs. send to Claude ARCHITECT)
4. Next Agent Prompt (either new Arena prompt or Claude ARCHITECT brief)
5. Dashboard Update (new battle matrix row + new Mermaid diagram in Freeze-Burst Audit tab)

> [!CAUTION]
> **AUDIT MODE CHECK (MANDATORY FIRST STEP)**
> Before doing anything else, confirm: was this Arena battle run in **MODE A (Plan Integrity / pre-implementation)** or **MODE B (Code Verification / post-implementation)**?
>
> - MODE A audits find issues in the PLAN before any code is written. The canary for MODE A is: models must cite correct C# file names and exact plan section content.
> - MODE B audits verify COMMITTED CODE. The canary for MODE B is: the BuildTag constant value.
>   If you cannot determine the mode from context, ASK THE DIRECTOR before proceeding. Wrong-mode synthesis produced the ADR-019 false-consensus failure on 2026-04-18.

---

## Phase 1: Ingest & Inventory

1. Find all new zips downloaded today (since last $battlezip run or since 11pm if first run):
   - `Get-ChildItem "$env:USERPROFILE\Downloads" -Filter *.zip | Where-Object { $_.LastWriteTime.Date -eq (Get-Date).Date } | Sort-Object LastWriteTime`
2. Determine next submission folder number (check existing `C:\tmp\arena_round_N\` folders).
3. Extract each zip into `C:\tmp\arena_round_N\sub_01..sub_NN` using `Expand-Archive -Force`.
4. Run file inventory: confirm each sub has exactly 1 `index.html` or a `src/App.tsx` (Vite builds).

**Zip Structure Rule (permanent)**: Arena downloads 1 zip PER AGENT -- not 1 per battle pair.
Each zip = 1 `index.html` from one agent. 21 zips = 21 individual agent responses (~10-11 battle pairs).

---

## Phase 1b: Hallucination Screen (MANDATORY -- runs before Phase 2)

> [!WARNING]
> A hallucinating model produces a FRAUDULENT consensus verdict. One bad model poisons the fleet.
> This phase MUST run before any synthesis. Disqualified models are excluded from all subsequent phases.

**Canary Check (MODE A -- Plan Integrity audits):**

For each submission, scan the primary output file (App.tsx, index.html, or auditData.ts) for:

```powershell
# Run for every submission folder
$subs = Get-ChildItem "C:\tmp\arena_round_N" -Directory
foreach ($sub in $subs) {
    $files = Get-ChildItem $sub.FullName -Recurse -Include "*.tsx","*.ts","*.html"
    Write-Host "=== $($sub.Name) ==="
    # Check 1: Do orphan site file names end in .cs? (non-.cs = hallucination)
    $files | Select-String -Pattern "file.*\.py|file.*\.js|file.*\.ts(?!x)" | Select-Object LineNumber, Line
    # Check 2: Does the model cite the correct canary value from the plan?
    $files | Select-String -Pattern "[CANARY_VALUE_FROM_PLAN]" | Select-Object LineNumber, Line
    # Check 3: Does the model name appear in h2/title?
    $files | Select-String -Pattern "<h2|<title" | Select-Object LineNumber, Line
}
```

**Check 4 — Exhaustive Scope Verification (MODE A ONLY — MANDATORY):**

For each CLEAN submission, verify it covered ALL sites in the plan's Section C.1 Site Inventory Table:

```powershell
# Count distinct site numbers referenced in the submission.
# Compare against the total site count from the plan's Section C.1 table.
# The plan's total site count must be obtained by reading Section C.1 before running $battlezip.
$totalSitesInPlan = [TOTAL_SITE_COUNT]  # Set this from the plan before running

foreach ($sub in $subs) {
    $files = Get-ChildItem $sub.FullName -Recurse -Include "*.tsx","*.ts","*.html"
    # Count distinct "Site #N" or "id: N" or "site.id" references
    $siteRefs = $files | Select-String -Pattern "[Ss]ite\s*#?\d+|id:\s*\d+" | `
        ForEach-Object { $_.Matches[0].Value } | Sort-Object -Unique
    $coveredCount = ($siteRefs | Measure-Object).Count
    Write-Host "=== $($sub.Name) === Covered: $coveredCount / $totalSitesInPlan sites"
    if ($coveredCount -lt $totalSitesInPlan) {
        Write-Host "  *** PARTIAL AUDIT WARNING: only $coveredCount of $totalSitesInPlan sites covered ***"
    }
}
```

**Disqualification Rules:**

| Finding                                                   | Status                                                   |
| --------------------------------------------------------- | -------------------------------------------------------- |
| Orphan sites reference non-.cs files (e.g., `.py`, `.js`) | DISQUALIFIED (hallucination)                             |
| Canary value is wrong or absent                           | DISQUALIFIED (hallucination)                             |
| Submission says "SOURCE UNAVAILABLE"                      | ABSTENTION (not hallucination -- valid failure)          |
| No model name in h2/title                                 | ATTRIBUTION GAP (warn, do not disqualify)                |
| Covered < 80% of plan sites (MODE A only)                 | PARTIAL AUDIT -- flag, downgrade to advisory weight only |
| Covered >= 80% of plan sites AND all checks pass          | CLEAN -- eligible for Phase 2 consensus                  |

> [!WARNING]
> **PARTIAL AUDIT RULE (PERMANENT -- 2026-04-18)**: A model that only audited a subset of the
> plan's sites cannot contribute to a BLOCKING or APPROVED consensus verdict. Its findings are
> advisory only. Root cause: ADR-019 rework loop was caused by partial-scope prompts surfacing
> only N of M defects per round. If 2+ submissions are flagged PARTIAL AUDIT, the Arena prompt
> was not exhaustive -- re-run using the canonical exhaustive prompt from `redteambattle.md`.

**Gate Rule**: If fewer than 3 CLEAN (non-partial, non-hallucinating) submissions are found:

- STOP. Do NOT proceed to Phase 2.
- Report to Director: "[N] of [TOTAL] models passed hallucination + scope screen. Minimum 3 required."
- If partial audit rate is high (2+/total): the prompt was too narrow -- re-run with the exhaustive prompt.
- Log disqualified/partial models and reason in `docs/arena_audit_matrix.md`.

---

## Phase 2: UltraThink Forensic Audit (Triple-Agent Protocol)

Run a PowerShell matrix scan across ALL submissions checking:

```powershell
# Key flags to scan for in each index.html:
Ring       = SpscRing|TryEnqueue|TryDequeue
Mask       = capacity - 1|& (cap|(capacity-1)
ProdField  = FieldOffset.*producerIndex|FieldOffset.*consumerIndex
Pad64      = _pad|CacheLine|padding.*64
Volatile   = VolatileRead|VolatileWrite
BadLock    = SpinWait.SpinOnce|Monitor.|lock (
Error      = went wrong|This model is not
AgentName  = <title>.*[Cc]laude|<title>.*[Gg]emini|<title>.*[Gg][Pp][Tt]|<h2>.*[Mm]odel
```

For each submission, record results in a markdown table.

Then perform the "Triple-Agent UltraThink" audit on the TOP 3 largest clean submissions:

1. **Logical Proof of Improvement**: What specific mechanism makes it better than the locked baseline?
2. **Regression Check**: Does it violate any V14 DNA rule? (lock, generic collection, single-ptr AllocAligned, use-after-free)
3. **Breakthrough Extraction**: What exact delta can be compounded into the next prompt?

**Agent Verification**: Check if `<title>` or `<h2>` contains model name. Log attribution table.
If 0/N submissions have agent names -- note this as "attribution gap" and ensure next prompt enforces the rule.

---

## Phase 2b: AMAL Vetting Gate (MANDATORY -- LOCKED before Phase 3)

**Protocol Directive (2026-04-07)**: Phase 3 is LOCKED until the AMAL automated harness completes vetting.
Manual porting is BANNED to prevent "Ghost Implementation" errors.

1. **Execute Harness**: `python scripts/amal_harness.py`
2. **Review Logs**: Open `docs/battle_results.md` and `docs/battle_results.json`.
3. **Pass Criteria**:
   - `Allocated = 0 B` on ALL benchmarks (Strict ADR-012 enforcement).
   - `Gen0 = 0` (No GC pressure).
   - RoundTrip Mean < Baseline (7.792 ns).
4. **Adjudication**: The harness identifies the "Absolute Winner" based on the lowest latency that satisfies the 0B allocation gate.
5. **Update History**: Synchronize `docs/benchmark_history.md` with the winning result.

## Phase 3: Go/No-Go Decision

Apply this decision tree:

```
IF any of these conditions are met → COMPOUND AGAIN (more Arena battle):
  - Winner still has a known gap (IDisposable, Init protocol, edge case)
  - Agent attribution is missing (no model names in any submission)
  - Regression found in winner (lock, generic collection detected)
  - Less than 3 rounds on this component

IF ALL of these are true → SEND TO CLAUDE ARCHITECT (P3):
  - Winner has zero regressions
  - All mandatory patterns present (unsafe, AggressiveInlining, tuple AllocAligned, FieldOffset isolation, bitwise mask, IDisposable)
  - 3+ rounds of compounding completed on this component
  - No further structural gap identifiable
```

State the decision clearly:

- `DECISION: COMPOUND AGAIN` — include reason + new Arena prompt
- `DECISION: SEND TO ARCHITECT` — include forensic brief for Claude

---

## Phase 4: Next Prompt Generation

### If COMPOUND AGAIN:

Output a ready-to-paste Arena prompt following ALL rules from the battle.md workflow:

- First line: `Do not use any web search. Answer from memory only.`
- Frame as index.html deliverable (not raw .cs)
- Include agent name rule: `In the page <title> tag and in a visible <h2> heading, write your model name and version.`
- Lock all prior-round breakthroughs in the CONTEXT section
- Focus the new challenge on exactly the gap identified in Phase 3
- Section 5: **Implementation Mandate (NO STUBS)** — BANNED: "Ready to build" skeletons, empty method bodies, or theoretical-only responses. AGENTS MUST implement functional `Send`/`Receive` (or round-appropriate) logic.
- Under 200 tokens in the challenge body
- No blocked phrases (see battle.md filter table)

### If SEND TO ARCHITECT:

Output a full Claude ARCHITECT brief (P3 handoff) including:

- All confirmed breakthrough deltas as a locked baseline table
- Forensic proof of correctness for the winner implementation
- Specific structural repair / integration task (e.g., "integrate CoreLane+SpscRing into the V12 dispatcher")
- Reference to implementation_plan.md for full code

---

## Phase 5: Dashboard Update & Tooling Sync (MANDATORY -- always runs)

After every $battlezip, execute the following tooling syncs and dashboard updates regardless of Go/No-Go decision:

### 5a. Linear & LangSmith Sync
- **Linear**: Use the **Linear MCP** (`create_comment` or `update_issue`) to log the Go/No-Go decision, winning agent, and breakthrough summary to the active Linear issue.
- **LangSmith**: Ensure this $battlezip synthesis trace is tagged with the Arena Battle ID and `BUILD_TAG`.

### 5b. Add battle matrix row to `docs/arena_dashboard.html`

Insert a new `<tr>` at the TOP of the Full Battle Matrix `<tbody>`:

```html
<tr class="winner">
  <td><span class="round-label">V14.X</span></td>
  <td><span class="agent-name">[Winner sub / agent name if known]</span></td>
  <td><span class="metric">[NS estimate]</span></td>
  <td>[N]/[TOTAL] ([PCT]%)</td>
  <td><span class="outcome-win">WINNER — [N]-AGENT CONSENSUS</span></td>
  <td class="breakthrough">[One-line breakthrough summary]</td>
  <td>
    <span class="badge-pill badge-green">[P] Pass</span>
    <span class="badge-pill badge-red">[F] Fail</span>
    <span class="badge-pill badge-dim">[S] Skip</span>
  </td>
  <td><span class="site-badge">zip-[winner]/[total]</span></td>
</tr>
```

### 5b. Add new Mermaid diagram in the Freeze-Burst Audit tab

Insert ABOVE the previous round card (newest always on top).

**SYNTAX SAFETY RULES (CRITICAL)**:

- **No Unescaped Brackets**: Never use `[` or `]` inside a label string `["..."]`. Use `(` and `)` instead.
- **No Unescaped Operators**: Replace `^` with `XOR`, `==` with `is`, and `&` with `and`.
- **Decision Delimiters**: Use `nodeID{Label text}` without quotes inside the braces.
- **Label Quotes**: Always wrap subgraph and node labels in double quotes `["Label"]` if they contain spaces.
- **No Reserved HTML**: Escape `<` as `&lt;` and `>` as `&gt;` if used, or replace with text.

**NON-CODER DIAGRAM RULE (MANDATORY)**: All node labels and subgraph titles MUST be plain English:

- BAD: `[FieldOffset(64)] _producerIndex (volatile int)`
- GOOD: `PRODUCER COUNTER -- The slot the writer will write to next`
- BAD: `diff = seq - producerIndex`
- GOOD: `Check: is this slot free? Compare ready-flag to write position`
- Subgraph titles must say WHAT THE SECTION DOES, not its code name.

**PULSE ENGINE RULE (MANDATORY)**: Use the right keywords in node labels to get auto-animated glow colours:

- CYAN glow (write/send): include `producer`, `write`, `publish`, `enqueue`, or `dispatch`
- GREEN glow (read/receive): include `consumer`, `read`, `output`, or `deliver`
- GOLD glow (memory): include `memory`, `alloc`, `handle`, `cache`, or `slot`
- PURPLE glow (protocol): include `ring`, `protocol`, `sequence`, or `generation`
- ORANGE glow (safety): include `guard`, `dispose`, `free`, or `pad`

No extra code needed -- the JS engine classifies nodes automatically from label text.

### 5c. Update header badges

```html
<span class="badge-pill badge-green">✓ V14.X [COMPONENT] DEPLOYED</span>
<span class="badge-pill badge-cyan">GATE: Xns [DESCRIPTOR]</span>
<span class="badge-pill badge-gold">V14.X-[NAME] | N-AGENT CONSENSUS</span>
```

### 5d. Update `docs/arena_audit_matrix.md`

- Add new round row to the results matrix table
- Add new ADR entry if a new permanent architectural decision was made (e.g., ADR-016)
- Update "Current Platinum Standard" table with the latest winner's specs
- Update the timestamp line at the bottom

### 5e. Automation Protocol Hardening

The agent MUST use the `forensics` findings to automatically populate the `breakthrough` summary in both the Dashboard and the Audit Matrix. Any identified "Mechanical Breakthrough" (e.g., XOR safety invariant) MUST be visualized in the Mermaid diagram.

---

## Post-Use Audit

After every $battlezip run:

1. Did the extraction succeed cleanly?
2. Did the matrix scan produce accurate results?
3. Did the Go/No-Go decision feel correct?
4. Did the dashboard get updated (diagram + matrix row + header badges)?
5. **Did any submission receive a PARTIAL AUDIT flag?** If yes: was the Arena prompt exhaustive? If not, update the prompt template before the next round.
6. **Was the exhaustive scope rule enforced?** Confirm total sites covered matched the plan's Section C.1 count.

State: `workflow(battlezip): no gaps identified.` or fix and commit `workflow(battlezip): [what was fixed]`
