name: check-pr
description: >
  Poll GitHub PR for bot responses and calculate Project Health Score (PHS).
  Waits for all CI checks to complete, extracts bot comments, and returns PHS.
  Use after pushing code to a PR to monitor bot feedback autonomously.
  Mandatory for PR Loop V2 workflows.

---

# Check PR Skill (V12.19 Autonomous Polling)

This skill polls a GitHub Pull Request for bot responses and calculates the Project Health Score (PHS). It implements the sleep/polling protocol specified in `.bob/commands/pr-loop.md` lines 146-147.

## Purpose

Replaces manual "wait 5-10 minutes and check GitHub" workflow with autonomous polling. Enables agents to:
- Wait for CI checks to complete
- Extract bot comments (Codacy, CodeRabbit, PR-Agent, etc.)
- Calculate PHS score
- Determine if another iteration is needed

## Usage

```bash
# Basic usage (polls PR #7)
check-pr 7

# With custom initial wait (default: 5 min)
check-pr 7 --initial-wait 300

# With custom poll interval (default: 3 min)
check-pr 7 --poll-interval 180
```

## Protocol

1. **Initial Wait**: Sleep 4 minutes (240 seconds) after push
2. **Poll Loop**: Check CI status every 3 minutes (180 seconds)
3. **Exit Conditions**:
   - All checks complete (success or failure)
   - Timeout reached (30 minutes default)
   - Bot comments detected

## Output Format

```json
{
  "pr_number": 7,
  "phs": 0,
  "checks_complete": true,
  "bot_comments": [
    {
      "bot": "codacy",
      "timestamp": "2026-05-26T20:30:00Z",
      "issues_count": 11,
      "severity_breakdown": {
        "critical": 5,
        "high": 5,
        "medium": 1
      }
    }
  ],
  "elapsed_time": "8m 30s",
  "next_action": "extract_forensics"
}
```

## Integration with PR Loop V2

This skill is called automatically by `pr-loop-auto` after Step 3 (push). It replaces the manual handoff to Orchestrator.

**Before (Manual)**:

```
Agent: "Pushed commit 8fd9b065"
[Agent stops]
[Director waits 5-10 min]
Director: "/pr-loop 7"
[Orchestrator spawns new agent]
```

**After (Autonomous)**:

```
Agent: "Pushed commit 8fd9b065"
Agent: "Invoking check-pr skill..."
[Skill sleeps 5 min, polls every 3 min]
Agent: "Bots responded. PHS = 0/100. Extracting forensics..."
[Agent continues to Iteration 3]
```

## Implementation

Uses PowerShell script `scripts/monitor_pr_checks.ps1` (see that file for implementation details).

## Error Handling

- **Timeout**: If checks don't complete in 30 minutes, return partial results
- **API Rate Limit**: Exponential backoff on 403 responses
- **Network Errors**: Retry up to 3 times with 30-second delay

## Post-Use Audit (MANDATORY - Anthropic Skill-Creator Protocol)

**All agents MUST perform this audit after EVERY use of this skill:**

### Audit Checklist

1. **Ambiguity Check**: Were any instructions unclear or produce unexpected results?
   - Did the initial wait execute as specified? (Verify 4-minute sleep, not 5)
   - Did polling occur at 3-minute intervals? (Check timestamps)
   - Were all bot comments extracted? (Compare to GitHub UI)
   - Was PHS calculated correctly? (Verify against manual calculation)
   - Did the skill exit cleanly? (No hanging processes)

2. **Gap Detection**: If ANY instruction was ambiguous or produced unexpected results:
   - Document the gap in this SKILL.md immediately
   - Add the quirk to the relevant section (Protocol, Error Handling, etc.)
   - Update version history with the fix

3. **Audit Statement**: If no gaps found, state:

   ```
   skill(check-pr): no gaps identified
   ```

4. **Protocol Violation**: Skipping this audit is a V12 protocol violation.

### Known Quirks (Updated During Audits)

- **Timing Adjustment (2026-05-26)**: Initial wait changed from 5 min to 4 min per Director request
- **GitHub CLI Fields (2026-05-26)**: Must use `bucket` and `state` fields, not `conclusion`/`status`
- **ASCII-Only (2026-05-26)**: Replaced Unicode ✓/⚠/✗ with ASCII [OK]/[WARN]/[ERROR]

## V12 DNA Alignment

- **Correctness by Construction**: Timeout prevents infinite loops
- **ASCII-Only**: All output is ASCII-safe JSON
- **Jane Street Alignment**: Deterministic polling, no heuristics
- **Karpathy Protocol**: Explicit success criteria, no silent failures

## Related Files

- `scripts/monitor_pr_checks.ps1` - Implementation
- `.bob/commands/pr-loop.md` - Parent workflow
- `plugins/pr-loop-auto/SKILL.md` - Autonomous loop skill