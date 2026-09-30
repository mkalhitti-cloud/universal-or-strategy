# PR-LOOP V2 Protocol Hardening (V2.1)

## Issue Identified (PR #5, Iteration 4-5)

**Problem**: Agent began repairs before bot analysis was fully complete, leading to:
1. Incomplete forensics extraction (truncated excerpts)
2. Premature fix application (started Iteration 4 before all bots finished)
3. Regression introduction (Iteration 2 exception filter bug not caught until Iteration 5)

## Root Causes

1. **Insufficient Wait Time**: 3-5 minutes may not be enough for all bots to complete analysis
2. **Forensics Script Limitations**: `extract_pr_forensics.ps1` truncates long excerpts
3. **No Completion Verification**: No check to confirm all bots have finished before proceeding
4. **Manual GitHub Audit Skipped**: Relied on automated extraction instead of manual verification

## Hardened Protocol (V2.1)

### Mandatory Bot Completion Checks

**REQUIRED BOTS** (must complete before repairs):
1. **greptile-apps** - Code intelligence analysis
2. **coderabbitai** - AI-powered code review
3. **codacy-production** - Static analysis
4. **SonarCloud** - Code quality & security
5. **cubic-dev-ai** - Multi-file analysis
6. **codeant-ai** - Automated code review
7. **sourcery-ai** - Code quality & refactoring suggestions

**BLOCKING**: If ANY of these bots show `IN_PROGRESS`, agent MUST wait.

### Step 1: Bot Analysis Wait (MANDATORY)

**BEFORE** extracting forensics:

```powershell
# Wait MINIMUM 5 minutes after push
Start-Sleep -Seconds 300

# Check mandatory bot completion status
$mandatoryBots = @(
    "greptile-apps",
    "coderabbitai",
    "codacy-production",
    "SonarCloud",
    "cubic-dev-ai",
    "codeant-ai",
    "sourcery-ai"
)

$inProgress = gh pr view <PR_NUMBER> --json statusCheckRollup --jq '.statusCheckRollup[] | select(.status == "IN_PROGRESS") | .name' | ConvertFrom-Json

$blockedBots = $inProgress | Where-Object { $mandatoryBots -contains $_ }

if ($blockedBots.Count -gt 0) {
    Write-Host "⏳ BLOCKED: Waiting for mandatory bots: $($blockedBots -join ', ')"
    Write-Host "Agent MUST NOT proceed with repairs until all mandatory bots complete."
    exit 1
}

Write-Host "✅ All mandatory bots completed. Safe to proceed."
```

**If ANY mandatory bots are IN_PROGRESS**:
- ⏳ Wait another 2 minutes
- 🔄 Re-check status
- 🚫 DO NOT proceed with repairs until ALL mandatory bots show "COMPLETED" or "NEUTRAL"

### Step 2: Forensics Extraction (ENHANCED)

**Run extraction script**:
```powershell
powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber <PR_NUMBER>
```

**THEN manually verify**:
```powershell
# Get latest gitar-bot comment (most reliable)
gh pr view <PR_NUMBER> --json comments --jq '.comments | sort_by(.createdAt) | reverse | .[0].body' | Out-File -FilePath "docs/brain/pr_<N>_gitar_latest.txt" -Encoding utf8

# Read the full comment
cat docs/brain/pr_<N>_gitar_latest.txt
```

### Step 3: Manual GitHub Audit (MANDATORY)

**NEVER skip this step**. Open the PR page in browser and:

1. **Check gitar-bot status badge**: `🚫 BLOCKED` or `✅ APPROVED`
2. **Count resolved vs total findings**: e.g., "4 resolved / 6 findings"
3. **Read FULL issue descriptions**: Click each collapsed section
4. **Verify inline comments**: Check "Files changed" tab for line-specific comments
5. **Cross-reference with forensics**: Ensure script captured all issues

### Step 4: Categorization (STRICT)

Create `docs/brain/pr_<N>_iteration<M>_categorization.md` with:

```markdown
## Source: Manual GitHub Audit + Forensics Script

### Summary
| Category | Count |
|----------|-------|
| RESOLVED | X |
| P0 BLOCKED | Y |
| P1/P2 | Z |

### RESOLVED Issues
- List each with verification that it's truly fixed

### P0 BLOCKED Issues
- Full description from gitar-bot
- Root cause analysis
- Proposed fix
- Jane Street alignment check

### P1/P2 Issues
- Defer or fix decision with rationale
```

### Step 5: Fix Application (GATED)

**ONLY proceed if**:
1. ✅ All bots have completed analysis (no IN_PROGRESS checks)
2. ✅ Manual GitHub audit confirms issue count matches forensics
3. ✅ Categorization document created and reviewed
4. ✅ All P0 BLOCKED issues have clear fix plans

**NEVER**:
- ❌ Start fixes while bots are still analyzing
- ❌ Rely solely on automated forensics extraction
- ❌ Skip manual GitHub verification
- ❌ Apply fixes without categorization document

### Step 6: Regression Prevention

**After each iteration**:
1. Run full pre-push validation (10/10 checks)
2. Manually verify the fix addresses the root cause
3. Check for side effects in related code
4. Wait for bot re-analysis before declaring success

## Forensics Script Enhancement (TODO)

**Current Limitation**: `extract_pr_forensics.ps1` truncates excerpts at 500 characters.

**Required Fix**:
```powershell
# In extract_pr_forensics.ps1, replace:
$excerpt = $comment.body.Substring(0, [Math]::Min(500, $comment.body.Length))

# With:
$excerpt = $comment.body  # Full content, no truncation

# OR add parameter:
param(
    [int]$ExcerptLength = 2000  # Increase default from 500 to 2000
)
```

## Enforcement Checklist

Before starting ANY iteration:

- [ ] All bot checks show "COMPLETED" or "NEUTRAL" (not IN_PROGRESS)
- [ ] Forensics script has run successfully
- [ ] Manual GitHub audit completed (browser verification)
- [ ] gitar-bot status badge checked (BLOCKED vs APPROVED)
- [ ] Categorization document created
- [ ] All P0 issues have clear fix plans
- [ ] No fixes applied until all above steps complete

## Violation Consequences

**If protocol is violated**:
1. STOP immediately
2. Wait for full bot analysis
3. Re-extract forensics
4. Perform manual GitHub audit
5. Create/update categorization document
6. Restart iteration from Step 1

## Success Metrics

**Protocol is working when**:
- Zero regressions introduced (no fixes that break other code)
- Zero premature fix applications (all bots complete before repairs)
- 100% issue capture rate (forensics matches manual audit)
- Clear audit trail (categorization docs for every iteration)