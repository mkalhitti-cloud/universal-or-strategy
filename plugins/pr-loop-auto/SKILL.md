name: pr-loop-auto
description: >
  Autonomous PR Loop V2 workflow with automated polling and iteration.
  Replaces manual /pr-loop command with fully autonomous fix-push-poll-repeat cycle.
  Continues until Project Health Score (PHS) reaches 100/100.
  Use for any PR that requires iterative bot-guided fixes.

---

# PR Loop Auto Skill (V12.19 Full Autonomy)

This skill implements the complete PR Loop V2 workflow autonomously, eliminating the need for manual Director intervention between iterations. It's the V12 equivalent of Greptile's `greploop` skill.

## Purpose

Transforms PR Loop V2 from a manual multi-step process into a single autonomous command:

**Before (Manual)**:
```
Director: "/pr-loop 7"
[Orchestrator spawns agent]
Agent: [Step 1] Extract forensics
Agent: [Step 2] Apply fixes
Agent: [Step 3] Push code
[Agent stops - hands off to Orchestrator]
[Director waits 5-10 min]
Director: "/pr-loop 7"  # Repeat
```

**After (Autonomous)**:
```
Director: "pr-loop-auto 7"
[Agent runs full loop autonomously]
Agent: [Iteration 1] Extract → Fix → Push → Poll → PHS = 50/100
Agent: [Iteration 2] Extract → Fix → Push → Poll → PHS = 75/100
Agent: [Iteration 3] Extract → Fix → Push → Poll → PHS = 100/100
Agent: "PR #7 ready for merge. PHS = 100/100"
```

## Dynamic Feedback Coverage

The skill monitors and responds to **ALL** GitHub PR feedback sources dynamically:
- Reviews, comments, actions, apps, integrations, checks, logs
- Bot-agnostic design - adapts as audit tools are added/removed
- No hardcoded bot names or source types

## Usage

```bash
# Basic usage (runs until PHS = 100)
pr-loop-auto 7

# With max iterations (safety limit)
pr-loop-auto 7 --max-iterations 10

# With custom polling intervals
pr-loop-auto 7 --initial-wait 300 --poll-interval 180

# Dry run (no pushes)
pr-loop-auto 7 --dry-run
```

## Workflow

### Iteration Loop

Each iteration consists of 5 steps:

1. **Extract Forensics** (Step 1)
   - Call `scripts/extract_pr_forensics.ps1`
   - Parse ALL feedback sources dynamically
   - Categorize findings (VALID/HALLUCINATION/INFRA-NOISE/ACCESS_BLOCKED)
   - Generate priority-ordered fix queue

2. **Apply Fixes** (Step 2)
   - Process VALID issues by priority (P0 → P1 → P2)
   - Apply surgical fixes to src/ files
   - Document each fix in `docs/brain/pr_N_iterationM_fixes.md`

3. **Push Changes** (Step 3)
   - Commit with message: `fix: PR #N Iteration M - [issue count] issues`
   - Push to PR branch
   - Run `deploy-sync.ps1` if src/ modified

4. **Poll for Feedback** (Step 4 - NEW)
   - Invoke `check-pr` skill
   - Sleep 4 minutes initially
   - Poll every 3 minutes until checks complete
   - Extract new feedback from ALL sources

5. **Calculate PHS** (Step 5 - NEW)
   - Run `scripts/calculate_fleet_score.ps1`
   - Aggregate scores across all feedback sources
   - If PHS = 100: Exit loop (success)
   - If PHS < 100: Continue to next iteration
   - If PHS unchanged after 3 iterations: Escalate to Director

### Exit Conditions

- ✅ **Success**: PHS = 100/100
- ⚠️ **Plateau**: PHS unchanged for 3 iterations (manual review needed)
- ❌ **Max Iterations**: Safety limit reached (default: 10)
- ❌ **Build Failure**: Compilation errors detected
- ❌ **Merge Conflict**: Branch diverged from main

## Output Format

```json
{
  "pr_number": 7,
  "iterations": [
    {
      "iteration": 1,
      "phs_before": 0,
      "issues_found": 11,
      "issues_fixed": 11,
      "phs_after": 50,
      "elapsed_time": "12m 30s"
    },
    {
      "iteration": 2,
      "phs_before": 50,
      "issues_found": 8,
      "issues_fixed": 8,
      "phs_after": 75,
      "elapsed_time": "10m 15s"
    },
    {
      "iteration": 3,
      "phs_before": 75,
      "issues_found": 5,
      "issues_fixed": 5,
      "phs_after": 100,
      "elapsed_time": "8m 45s"
    }
  ],
  "final_phs": 100,
  "total_elapsed": "31m 30s",
  "status": "success",
  "ready_for_merge": true
}
```

## Integration with Existing Workflows

### Replaces Manual /pr-loop

The `/pr-loop` command in `.bob/commands/pr-loop.md` becomes a thin wrapper:

```bash
# Old: Manual multi-step
/pr-loop 7

# New: Single autonomous command
pr-loop-auto 7
```

### Checkpoint Safety

- Automatic checkpointing enabled (`.bob/settings.json`)
- Restore via `/restore` if interrupted
- Each iteration commits separately for rollback safety

### Src-Only Push Protocol (V12.19)

- Always uses `git add src/` (never `git add .`)
- Runs `deploy-sync.ps1` after src/ changes
- Verifies BUILD_TAG before push

## Error Handling

### Build Failures
```
Agent: "Build failed after applying fixes"
Agent: "Reverting commit..."
Agent: "Escalating to Director with build log"
```

### Plateau Detection
```
Agent: "PHS unchanged for 3 iterations (50/100)"
Agent: "Remaining issues may require manual review:"
Agent: "- P1: Ambiguous heuristic in RetryHelper.cs"
Agent: "- P2: CodeStyle requires project file"
```

### API Rate Limits
```
Agent: "GitHub API rate limit hit"
Agent: "Sleeping 60 minutes until reset..."
Agent: "Resuming at 21:30 UTC"
```

### Conflicting Feedback
```
Agent: "Conflicting feedback detected:"
Agent: "- Source A: 'Use StringBuilder' (P1)"
Agent: "- Source B: 'String concat is fine here' (P2)"
Agent: "Prioritizing higher severity (Source A P1)"
```

## Post-Use Audit (MANDATORY - Anthropic Skill-Creator Protocol)

**All agents MUST perform this audit after EVERY use of this skill:**

### Audit Checklist

1. **Ambiguity Check**: Were any instructions unclear or produce unexpected results?
   - Did all iterations complete autonomously? (No manual intervention)
   - Was polling executed correctly? (4 min initial, 3 min subsequent)
   - Were ALL feedback sources parsed? (Dynamic detection working)
   - Did PHS reach 100/100? (Or escalate appropriately)
   - Were all commits src-only? (Verify with `git log --stat`)
   - Was `deploy-sync.ps1` run after src/ changes? (Check NinjaTrader sync)

2. **Gap Detection**: If ANY instruction was ambiguous or produced unexpected results:
   - Document the gap in this SKILL.md immediately
   - Add the quirk to the relevant section (Workflow, Error Handling, etc.)
   - Update version history with the fix

3. **Audit Statement**: If no gaps found, state:
   ```
   skill(pr-loop-auto): no gaps identified
   ```

4. **Protocol Violation**: Skipping this audit is a V12 protocol violation.

### Known Quirks (Updated During Audits)

- **Timing Adjustment (2026-05-26)**: Initial wait changed from 5 min to 4 min per Director request
- **Hardened Protocol (2026-05-26)**: Agent loops autonomously until PHS=100, only stops for F5 verification or critical decisions
- **Duplicate Detection (2026-05-26)**: Most P0/P1 issues in Iteration 3 were duplicates from Iteration 2 - forensics extraction needs deduplication logic

## V12 DNA Alignment

- **Correctness by Construction**: Max iterations prevent infinite loops
- **ASCII-Only**: All commits and logs are ASCII-safe
- **Jane Street Alignment**: Deterministic iteration, no heuristics
- **Karpathy Protocol**: Explicit success criteria at each step
- **Src-Only Push Protocol**: Enforces V12.19 git discipline

## Related Files

- `scripts/monitor_pr_checks.ps1` - Polling implementation
- `plugins/check-pr/SKILL.md` - Feedback polling skill
- `.bob/commands/pr-loop.md` - Original manual workflow
- `scripts/extract_pr_forensics.ps1` - Forensics extraction (dynamic)
- `scripts/calculate_fleet_score.ps1` - PHS calculation (dynamic)

## Implementation Notes

This skill orchestrates multiple sub-skills and scripts:
- Forensics: `extract_pr_forensics.ps1` (parses ALL feedback sources dynamically)
- Fixes: Advanced mode agent (surgical edits)
- Polling: `check-pr` skill (monitors ALL check types)
- PHS: `calculate_fleet_score.ps1` (aggregates ALL feedback scores)

The skill maintains state in `docs/brain/pr_N_loop_state.json` for checkpoint recovery.

## Bot-Agnostic Design

The skill does NOT hardcode specific audit tools. It:
- Parses GitHub API responses generically
- Categorizes feedback by source type dynamically
- Extracts severity and content regardless of source identity
- Adapts automatically as audit tools are added/removed

This ensures the skill remains functional as the audit fleet evolves.