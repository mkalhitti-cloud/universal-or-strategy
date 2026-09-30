---
name: mcp-loop
description: >-
  MCP Quality Loop (Pre-Push Semantic Review). Runs jcodemunch and Cubic semantic
  checks AFTER /local-loop and BEFORE pushing to GitHub, targeting 5/5 on both
  tools in ~3 minutes. Use when the user runs /mcp-loop, asks for semantic review,
  or is progressing through Phase 5 ticket execution after /local-loop passes.
metadata:
  user-invocable: true
  disable-model-invocation: true
---

# /mcp-loop - MCP Quality Loop (Pre-Push Semantic Review)

## Purpose
Execute MCP-based semantic quality checks (jcodemunch + Cubic) AFTER `/local-loop` and BEFORE pushing to GitHub. This loop uses MCP servers to achieve 5/5 scores on both tools, completing in ~3 minutes.

## Position in Triple-Nested Architecture
```
/autonomous-refactor (Outer - Master Orchestrator)
  └─ /epic-run (Middle - Single epic, 6 phases)
      └─ Phase 5: Ticket Execution
          ├─ /ticket (Engineer implementation)
          ├─ F5 Gate (Human verification - ONLY manual step)
          ├─ /local-loop (8 min, passed with 5/5)
          ├─ /mcp-loop ← YOU ARE HERE (3 min, MCP servers)
          ├─ Pre-Push Validation (2 min, fast mode)
          ├─ git commit (local only)
          └─ AFTER ALL EPICS: git push → /pr-loop (GitHub bots)
```

**CRITICAL**: `/mcp-loop` runs BEFORE any git push. It is a pre-push quality gate.

## What This Loop Does

### 1. jcodemunch Code Quality Review
**Tool**: jcodemunch-mcp Server
**Target**: All modified files (full context)
**Checks**:
- Semantic correctness
- Cross-module dependencies
- Architectural violations
- V12 DNA compliance (FSM/Actor, no locks, ASCII-only)

**MCP Tool**: `use_mcp_tool` with `jcodemunch` server
**Command**:
```
Ask Bob: "Run jcodemunch code analysis on current changes"
```

**Output**: Code quality score (0-5)

### 2. Cubic Merge Confidence
**Tool**: Cubic CLI (MCP disabled due to OAuth issues)
**Target**: Diff against base branch
**Checks**:
- Merge safety
- Breaking changes
- Team review patterns
- Cross-file impact

**Command**:
```bash
cubic review --base main
```

**Output**: Merge confidence score (0-5)

## Loop Protocol

### Entry Conditions
- ✅ `/local-loop` passed (CodeAnt + complexity + dead code clean)
- ✅ All changes committed locally
- ❌ NOT yet pushed to GitHub

### Exit Conditions
**SUCCESS** (proceed to Pre-Push Validation):
- ✅ jcodemunch: Score = 5/5 OR documented exceptions only
- ✅ Cubic: Score = 5/5 OR documented exceptions only
- ✅ Zero issues from either tool (unless explicitly suppressed)

**FAILURE** (fix and retry):
- ❌ jcodemunch: Score < 5/5
- ❌ Cubic: Score < 5/5
- ❌ ANY issues detected (P0, P1, P2, or style)

**Exception Protocol**:
1. **Jane Street Suppression**: Query KB via `python scripts/query_kb.py "<issue>"`, verify pattern is intentional
2. **Director Override**: Manual approval with documentation in `.jcodemunch.jsonc` or `.cubic/suppressions.json`
3. **Tool Hallucination**: Verified false positive, add to suppression config

**Target**: 5/5 clean score (zero issues) unless explicitly suppressed via Jane Street KB

### Loop Logic
```
1. Run jcodemunch code analysis
   ├─ Score < 5/5? → FIX → Restart loop
   ├─ ANY issues? → FIX OR SUPPRESS → Restart loop
   └─ Score = 5/5? → Continue

2. Run Cubic review
   ├─ Score < 5/5? → FIX → Restart loop
   ├─ ANY issues? → FIX OR SUPPRESS → Restart loop
   └─ Score = 5/5? → EXIT SUCCESS

3. Emit: [MCP-LOOP-PASS] → Proceed to Pre-Push Validation
```

**Target**: 5/5 on both tools (zero issues) unless Jane Street KB verifies suppression

## Autonomous Execution

### Step 1: jcodemunch code analysis
```bash
# Via MCP (Bob IDE)
use_mcp_tool jcodemunch-mcp query_repository \
  --query "Review changes for V12 DNA compliance and semantic correctness"

# Parse response
JCODEMUNCH_SCORE=$(extract_score_from_response)

if [[ $JCODEMUNCH_SCORE -lt 5 ]]; then
  echo "[MCP-LOOP] jcodemunch score: $JCODEMUNCH_SCORE/5 (threshold: 5/5)"
  
  # Extract issues
  ISSUES=$(extract_issues_from_response)
  
  # Categorize issues
  for issue in $ISSUES; do
    if is_p0_issue "$issue"; then
      # Apply fix autonomously
      apply_fix "$issue"
    elif is_valid_suppress "$issue"; then
      # Add suppression comment
      add_suppression "$issue"
    else
      # Escalate to Director
      escalate_issue "$issue"
    fi
  done
  
  # Commit fixes
  git add .
  git commit -m "fix(mcp-loop): address jcodemunch analysis results"
  
  # Restart loop
  exec /mcp-loop
fi

echo "[MCP-LOOP] jcodemunch: $JCODEMUNCH_SCORE/5 ✅"
```

### Step 2: Cubic Review
```bash
# Via CLI (OAuth broken in Bob IDE)
cubic review --base main --format json > cubic_report.json

# Parse response
CUBIC_SCORE=$(jq -r '.merge_confidence' cubic_report.json)

if [[ $CUBIC_SCORE -lt 5 ]]; then
  echo "[MCP-LOOP] Cubic score: $CUBIC_SCORE/5 (threshold: 5/5)"
  
  # Extract issues
  ISSUES=$(jq -r '.issues[] | select(.priority == "P0")' cubic_report.json)
  
  # Apply fixes
  for issue in $ISSUES; do
    apply_cubic_fix "$issue"
  done
  
  # Commit fixes
  git add .
  git commit -m "fix(mcp-loop): address Cubic findings"
  
  # Restart loop
  exec /mcp-loop
fi

echo "[MCP-LOOP] Cubic: $CUBIC_SCORE/5 ✅"
```

### Step 3: Success Exit
```bash
echo "[MCP-LOOP-PASS] All MCP checks passed"
echo "jcodemunch: $JCODEMUNCH_SCORE/5"
echo "Cubic: $CUBIC_SCORE/5"
echo "Duration: $(($SECONDS / 60)) minutes"
echo "Next: Pre-Push Validation"
```

## Integration with Batch Commit Strategy

For the CCN run (157 epics), `/mcp-loop` runs ONCE per epic (not on batch):

```
Epic N:
  1. /ticket (Engineer execution)
  2. F5 Gate (Human verification)
  3. /local-loop (Autonomous fix loop, 8 min, 5/5 target)
  4. /mcp-loop ← Run per epic (3 min, 5/5 target)
  5. Pre-push validation (2 min, fast mode)
  6. git commit (local only, no push)
  7. Continue to Epic N+1

After all 157 epics:
  1. git push (batch push to GitButler virtual branch)
  2. Create batch PRs (~10-15 PRs)
  3. /pr-loop (on each batch PR → 100/100 PHS)
```

**Key Change**: MCP loop runs per-epic to catch semantic issues early, not on batch.

## Time Investment

**Per-Epic Strategy** (current):
- `/mcp-loop`: 3 minutes × 157 epics = 7.85 hours
- **Benefit**: Catches semantic issues early, prevents cascading failures
- **Cost**: Higher upfront time investment
- **ROI**: Prevents expensive rework in `/pr-loop` phase

**Why Per-Epic vs Batch**:
- Early detection of V12 DNA violations (locks, Unicode, non-FSM)
- Prevents 157 epics worth of work being rejected at batch PR time
- Jane Street KB validation happens incrementally
- jcodemunch semantic search catches cross-file issues immediately

## jcodemunch-mcp Integration

### Configuration (`.bob/mcp.json`)
```json
{
  "mcpServers": {
    "jcodemunch-mcp": {
      "url": "https://api.jcodemunch.com/mcp",
      "type": "streamable-http",
      "headers": {
        "Authorization": "Bearer YOUR_GREPTILE_TOKEN"
      },
      "disabled": false
    }
  }
}
```

### Available Tools
1. `query_repository` - Natural language Q&A
2. `search_repository` - Semantic + regex search
3. `index_repository` - Update repo index
4. `get_repository_info` - Check index status

### V12 DNA Patterns (jcodemunch Config)

**File**: `.jcodemunch.jsonc` (repo root)
```json
{
  "instructions": "Enforce NinjaScript V12 Project Standards. BANNED: lock(stateLock), Unicode in strings, non-FSM order submission. MANDATORY: ASCII Gate, Enqueue model for all state mutations.",
  "rules": [
    {
      "id": "v12-fsm-only",
      "rule": "Follower order replacements must use the two-phase Replace FSM.",
      "severity": "critical"
    },
    {
      "id": "v12-no-locks",
      "rule": "All state mutations must use FSM/Actor Enqueue model. lock(stateLock) is BANNED.",
      "severity": "critical"
    },
    {
      "id": "v12-ascii-only",
      "rule": "All string literals must be ASCII-only. No Unicode, emoji, or curly quotes.",
      "severity": "critical"
    },
    {
      "id": "v12-complexity",
      "rule": "All methods must have cyclomatic complexity ≤ 15 (Jane Street aligned).",
      "severity": "major"
    }
  ]
}
```

## Cubic CLI Integration

### Installation
```bash
# Install Cubic CLI
npm install -g @cubic/cli

# Authenticate (first run)
cubic auth login
```

### Configuration
**File**: `.cubic/config.json`
```json
{
  "team_patterns": true,
  "merge_confidence_threshold": 4,
  "focus_areas": [
    "security",
    "architecture",
    "breaking_changes"
  ]
}
```

### Why CLI Instead of MCP?
- **OAuth Issue**: Bob IDE doesn't support OAuth for `streamable-http` MCP servers
- **Workaround**: Use Cubic CLI directly in terminal
- **Future**: Switch to MCP when Bob IDE OAuth support is added

## Issue Categorization

### VALID-FIX (Apply Autonomously)
- P0 security issues
- V12 DNA violations (locks, Unicode, non-FSM)
- Complexity regressions (CYC > 15)
- Breaking changes

### VALID-SUPPRESS (Add Comment)
- False positives (verified by Jane Street KB)
- Intentional deviations (documented in ARCHITECTURE.md)
- Tool limitations (e.g., jcodemunch can't see NinjaTrader API)

### HALLUCINATION (Ignore)
- Issues in files not modified
- Issues already fixed in previous loop
- Contradictory findings between tools

## Error Recovery

### jcodemunch Failures
**Symptom**: Score stuck at 3/5 after 3 fix attempts
**Action**:
1. STOP loop
2. Export jcodemunch analysis results: `jcodemunch_analysis.json`
3. Escalate to Director with context
4. Manual review required
5. Document in `docs/brain/EPIC-X/mcp-loop-failure.md`

### Cubic Failures
**Symptom**: Score stuck at 3/5 after 3 fix attempts
**Action**:
1. STOP loop
2. Export Cubic findings: `cubic_findings.json`
3. Run Jane Street KB audit: `python scripts/query_kb.py "merge safety patterns"`
4. Apply KB-verified fixes
5. Restart loop

### Both Tools Disagree
**Symptom**: jcodemunch 5/5, Cubic 2/5 (or vice versa)
**Action**:
1. STOP loop
2. Compare findings side-by-side
3. Prioritize tool with higher confidence
4. Escalate conflicting findings to Director
5. Document in `docs/brain/tool-disagreement-[date].md`

## Success Metrics

**Target**: 95% first-pass success rate (both tools = 5/5)
**Current**: TBD (first run pending)

**Tracking**:
- Success rate per epic
- Average loop iterations
- Most common failure modes
- Time per iteration
- Tool agreement rate (jcodemunch vs Cubic)

## Comparison with /pr-loop

| Feature | /mcp-loop | /pr-loop |
|---------|-----------|----------|
| **Timing** | Pre-push | Post-push |
| **Tools** | jcodemunch + Cubic | GitHub bots (29 bots) |
| **Duration** | 3 minutes | 15 minutes |
| **API Cost** | Low (2 tools) | High (29 bots) |
| **Scope** | Semantic + merge safety | Style + security + complexity |
| **Target** | 5/5 on both tools | 100/100 PHS |
| **Blocking** | Yes (pre-push gate) | No (post-push fixes) |

## Next Steps

1. ✅ Document `/mcp-loop` protocol
2. ⏳ Test jcodemunch-mcp integration
3. ⏳ Test Cubic CLI integration
4. ⏳ Validate on EPIC-51 (first run)
5. ⏳ Integrate into `/epic-run` Phase 5
6. ⏳ Update `BATCH_COMMIT_STRATEGY.md` with loop details

---

**Status**: Protocol defined, awaiting first execution
**Owner**: V12 Orchestrator
**Dependencies**: jcodemunch-mcp (✅), Cubic CLI (⏳)
**Last Updated**: 2026-06-08
