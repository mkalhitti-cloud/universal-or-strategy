# PR Perfection Loop V2 (Improved Workflow)

## Overview

The V2 PR Loop adds **mandatory Bot Forensics extraction** before any fix attempts. This prevents the critical flaw where agents missed P0 bugs because they never read bot comments.

## Key Improvements

1. **Bot Forensics Step (NEW)**: Extracts ALL bot findings into structured reports BEFORE fixes
2. **CodeRabbit Review Gates (NEW)**: Mandatory AI review with V12 DNA alignment
3. **Persistent Hallucination Log**: Tracks false positives for pattern learning
4. **ALL Issues Blocking**: P0/P1/P2 all block merge (except hallucinations/infra-noise)
5. **Manual Override Gate**: Director can approve <100 PHS when flagged

## The Improved Cycle

### Step -1: PR Existence Check (NEW - MANDATORY)
**Mode:** Advanced
**Action:** Verify if PR already exists before creating new branch

```powershell
# Check if PR exists and get its branch name
$prExists = gh pr view <PR_NUMBER> 2>&1
if ($LASTEXITCODE -eq 0) {
    # PR exists - checkout its branch
    $branchName = gh pr view <PR_NUMBER> --json headRefName --jq '.headRefName'
    git checkout $branchName
    Write-Host "[PR-LOOP] Checked out existing PR branch: $branchName"
} else {
    # PR doesn't exist - proceed to Step 0 (create new branch)
    Write-Host "[PR-LOOP] PR does not exist. Proceeding to create new branch..."
}
```

**Gate:**
- If PR exists: Skip Step 0, proceed to Step 1 (Pre-Flight Hygiene on existing branch)
- If PR doesn't exist: Proceed to Step 0 (create new branch)

**Rationale:** Prevents branch confusion when resuming work on existing PRs. Agents were creating new branches instead of checking out existing PR branches, causing pushes to wrong branches and bot checks never triggering.

---

### Step 0: Pre-Flight Hygiene (MANDATORY)
**Mode:** Advanced
**Action:** Verify branch is clean and rebased, run Semgrep scan

**CRITICAL SAFETY CHECK**: Before ANY rebase operation:
1. Check PR scope: `powershell -File .\scripts\check_pr_scope.ps1 -PrNumber <PR_NUMBER>`
2. If output is `SRC-ONLY`:
   - **SRC-ONLY PR**: Skip rebase entirely, use `git reset --hard origin/<branch>`
   - Rationale: Rebasing src-only PRs risks contamination from non-src commits in main
3. If output is `MIXED`:
   - **MIXED PR**: Proceed with normal rebase + conflict resolution
4. If output is `EMPTY`:
   - **EMPTY PR**: No files changed, skip hygiene checks

**Rebase Protocol** (MIXED PRs only):
1. Run: `git fetch origin main`
2. Run: `git rebase origin/main`
3. If conflicts in non-src files: resolve them
4. If conflicts in src/ files: HALT and report (likely merge issue)
5. Continue rebase until complete

**Hygiene Verification** (ALL PRs):

**If PR already exists (from Step -1):**
```powershell
# Check scope first
$scope = powershell -File .\scripts\check_pr_scope.ps1 -PrNumber <PR_NUMBER>

if ($scope -eq "SRC-ONLY") {
    # Src-only: hard reset to avoid contamination
    git fetch origin <branch>
    git reset --hard origin/<branch>
} elseif ($scope -eq "MIXED") {
    # Mixed: normal rebase
    git fetch origin main
    git rebase origin/main
}

# Run hygiene checks
powershell -File .\scripts\verify_pr_hygiene.ps1
powershell -File .\scripts\run_semgrep.ps1 -Severity ERROR
```

**If creating new PR:**
```powershell
git checkout -b <BRANCH_NAME>
git fetch origin main && git rebase origin/main
powershell -File .\scripts\verify_pr_hygiene.ps1
powershell -File .\scripts\run_semgrep.ps1 -Severity ERROR
```

**Semgrep Pre-Push Scan:**
- Catches P0 V12 DNA violations BEFORE they reach GitHub
- Blocks: `lock()` usage, Unicode strings, CAS on locals, blocking async
- Fast: ~10s scan of `src/` only
- **BLOCKING**: P0 findings must be fixed before push

**Gate:** PASS/FAIL. Fail = HALT and fix hygiene violations + Semgrep P0 findings.

---

### Step 1: CodeRabbit Pre-Review (NEW - MANDATORY)
**Mode:** Automated (GitHub)
**Action:** CodeRabbit performs initial AI review on PR creation

**Triggers:**
- Automatic on PR open
- Automatic on new commits
- Manual via `@coderabbitai review` comment

**V12 DNA Checks:**
1. ✅ V12_001/V12_002 naming convention enforcement
2. ✅ Lock-free pattern validation (no `lock()` usage)
3. ✅ Complexity threshold ≤15 (Jane Street alignment)
4. ✅ Atomic operation correctness (CAS on shared state)
5. ✅ ASCII-only compliance (no emoji/Unicode in literals)
6. ✅ Struct semantics validation (ref parameters)
7. ✅ Zero-allocation hot path checks (no LINQ)
8. ✅ Benchmark methodology validation
9. ✅ Circuit breaker pattern verification
10. ✅ Correctness by construction principles

**Configuration:** `.coderabbit.yaml` (see `docs/setup/GITHUB_APPS_INSTALLATION.md`)

**Gate:**
- **BLOCKING**: P0 issues (CRITICAL violations)
- **WARNING**: P1/P2 issues (must be addressed or justified)
- **INFO**: Style suggestions (non-blocking)

**Commands:**
```bash
# Trigger manual review
@coderabbitai review

# Resolve all CodeRabbit comments
@coderabbitai resolve

# Generate issue from comment
@coderabbitai issue

# Pause reviews
@coderabbitai pause

# Resume reviews
@coderabbitai resume
```

---

### Step 2: Bot Forensics (MANDATORY)
**Mode:** Advanced
**Action:** Extract and categorize ALL bot findings

```powershell
powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber <PR_NUMBER>
```

**Outputs:**
- `docs/brain/pr_<N>_forensics.md` - Full categorized findings
- `docs/brain/pr_<N>_fix_queue.md` - Priority-ordered fix list
- `docs/brain/bot_hallucinations.md` - Updated hallucination log

**Classification Logic:**
- **VALID**: Real issues requiring fixes (P0/P1/P2)
- **HALLUCINATION**: False positives (e.g., "missing files" that exist)
- **INFRA-NOISE**: Monthly limits, accessibility scans with 0 files
- **UNKNOWN**: Needs manual review

**Semgrep Integration (Future):**
When Semgrep GitHub App is installed, findings will appear in PR comments and be extracted by this script. Until then, Semgrep runs locally in Step 0 (pre-push).

**Gate:** Review forensics report. If P0 issues exist, they MUST be fixed before proceeding.

---

### Step 3: Local Repair
**Mode:** v12-engineer  
**Input:** `@docs/brain/pr_<N>_fix_queue.md`

**Protocol:**
1. Read fix queue completely
2. For each VALID issue (P0 → P1 → P2):
   - Apply fix
   - Verify locally (compile, test)
   - Mark as [x] FIXED in fix queue
3. Run formatters: `powershell -File .\scripts\format_all_csharp.ps1`
4. Run local verification: `powershell -File .\scripts\calculate_fleet_score.ps1`

**Gate:** Local Score = 15/15. If < 15, repeat Step 2.

---

### Step 4: Global Push & Monitor
**Mode:** Advanced

**Protocol:**
1. `powershell -File .\deploy-sync.ps1` (MANDATORY - syncs NT8 hard links)
2. `git add . && git commit -m "fix: PHS Perfection Loop - PR #<N>" && git push`
3. **MANDATORY SLEEP**: `Start-Sleep -Seconds 240` (4 min for first check - bots complete faster than expected)
4. Monitor: `gh pr checks <PR_NUMBER>`
5. **SUBSEQUENT SLEEP**: `Start-Sleep -Seconds 180` (3 min if checks still pending)
6. Calculate PHS: `powershell -File .\scripts\calculate_fleet_score.ps1 -PrNumber <N>`

**Outputs:**
- PHS Score (0-100)
- Passing/Failing checks breakdown
- New bot findings (if any)

**Gate:** 
- If Score < 100: **RESTART at Step 1** (re-extract forensics for new findings)
- If Score = 100: **Advance to Step 4**

---

### Step 5: Manual Override Gate
**Mode:** Orchestrator  
**Trigger:** PHS < 100 after 3+ iterations

**Protocol:**
1. Present current PHS and remaining issues to Director
2. Classify remaining issues:
   - VALID but low-priority (P2 style issues)
   - Hallucinations not yet logged
   - INFRA-NOISE
3. Ask Director: "PHS is X/100. Remaining issues: [list]. Approve merge? (YES/NO)"

**Director Options:**
- **YES**: Proceed to F5 Gate (Step 5)
- **NO**: Provide guidance, restart at Step 1
- **DEFER**: Create follow-up ticket, proceed to F5 Gate

---

### Step 6: Final F5 Verification
**Mode:** Orchestrator  
**Action:** Director presses F5 in NinjaTrader

**Output:**
```
[F5-GATE] PR #<N> - PHS <SCORE>/100
All automated gates: PASSED/APPROVED
Remaining issues: [list if <100]

ACTION REQUIRED: Press F5 in NinjaTrader IDE.
When you see the BUILD_TAG banner, type: F5 done [BUILD_TAG]
```

**Gate:** Wait for Director confirmation.

---

## Comparison: V1 vs V2

| Aspect | V1 (Old) | V2 (New) |
|--------|----------|----------|
| Bot Comment Reading | ❌ Never read | ✅ Mandatory extraction |
| CodeRabbit Integration | ❌ None | ✅ Automated V12 DNA checks |
| Semgrep Integration | ❌ None | ✅ Pre-push + PR comments (future) |
| Issue Categorization | ❌ None | ✅ VALID/HALLUCINATION/INFRA-NOISE |
| Hallucination Tracking | ❌ None | ✅ Persistent log |
| Fix Priority | ❌ Undefined | ✅ P0 → P1 → P2 |
| Manual Override | ❌ None | ✅ Director gate at <100 |
| Loop Efficiency | ❌ Blind retries | ✅ Forensics-guided |
| V12 DNA Enforcement | ❌ Manual | ✅ Automated (CodeRabbit + Semgrep) |

## Example Session

```
[PR-LOOP] Starting for PR #6

[Step 0] Pre-Flight Hygiene
✅ Branch rebased on origin/main
✅ Diff size: 8,432 chars (< 10k limit)

[Step 1] CodeRabbit Pre-Review
⏳ Waiting for CodeRabbit analysis...
✅ CodeRabbit review complete
   - P0 Issues: 2 (struct copy bug, CAS on local variable)
   - P1 Issues: 3 (Task.WaitAll, benchmark methodology)
   - P2 Issues: 3 (encapsulation, unused locals)
   - Suggestions: 5 (style improvements)

[Step 2] Bot Forensics
Extracting findings from 19 comments, 15 reviews...
✅ Forensics report: docs/brain/pr_6_forensics.md
✅ Fix queue: docs/brain/pr_6_fix_queue.md

Summary:
- VALID Issues: 8 (P0: 2, P1: 3, P2: 3)
- Hallucinations: 1 (Codacy "missing files")
- INFRA-NOISE: 3 (CodeSlick monthly limit)

[Step 3] Local Repair
Fixing P0 issues:
  [x] CancelOrder race condition (OrderManagementTests.cs:199-211)
  [x] Struct copy semantics bug (OrderManagementTests.cs:30, 52)
Fixing P1 issues:
  [x] Task.WaitAll blocking call (FSMActorTests.cs:77)
  [x] Benchmark dead-code elimination (BarUpdateBenchmark.cs:45)
  [x] Division-by-zero guard (SIMADispatchBenchmark.cs:108)
Fixing P2 issues:
  [x] Encapsulate OrderData fields (OrderManagementTests.cs:241)
  [x] IEquatable for MockBar (INinjaTraderMocks.cs:30)
  [x] Benchmark unused locals (OrderCallbacksBenchmark.cs:67)

Local Score: 15/15 ✅

[Step 4] Global Push & Monitor
✅ deploy-sync.ps1 passed
✅ Pushed commit bd6e7e1
⏳ Waiting 5 minutes for CI...
✅ All checks passed

PHS: 100/100 ✅

[Step 6] F5 Gate
All issues resolved. Ready for merge.
Press F5 in NinjaTrader...

[COMPLETE] PR #6 ready to merge
```

## Integration with `/pr-loop` Command

The `/pr-loop` command in `.bob/custom_modes.yaml` should be updated to reference this V2 workflow. The Orchestrator mode will delegate each step to the appropriate specialized mode (Advanced for forensics/monitoring, v12-engineer for fixes).

## Forensics Script Usage

```powershell
# Extract forensics for PR #6
.\scripts\extract_pr_forensics.ps1 -PrNumber 6

# Outputs:
# - docs/brain/pr_6_forensics.md (full report)
# - docs/brain/pr_6_fix_queue.md (priority-ordered fixes)
# - docs/brain/bot_hallucinations.md (updated log)
```

## Hallucination Learning

The persistent `docs/brain/bot_hallucinations.md` log enables pattern recognition:

```markdown
## PR #6 - 2026-05-23
- **codacy-production**: "Missing files: LatencyProbeTests.cs, LogBufferThreadStaticTests.cs"
  → VERIFIED FALSE: Files exist at tests/V12_Performance.Tests/Infrastructure/

## PR #4 - 2026-05-22
- **sourcery-ai**: "File not found: V12_002.Orders.Management.cs"
  → VERIFIED FALSE: File exists at src/V12_002.Orders.Management.cs
```

Future iterations can auto-classify similar claims as hallucinations.

## Success Criteria

- ✅ Zero P0 bugs missed due to unread comments
- ✅ All VALID issues fixed or explicitly deferred by Director
- ✅ Hallucinations logged for pattern learning
- ✅ PHS 100/100 OR Director manual approval
- ✅ F5 verification passed

---

## CodeRabbit Integration Details

### Configuration File: `.coderabbit.yaml`

Located at repository root. Key settings:

```yaml
reviews:
  profile: assertive          # Strict review mode
  request_changes_workflow: true
  auto_review:
    enabled: true
    base_branches:
      - main
      - develop
  
  path_filters:               # Exclude non-critical paths
    - "!docs/**"
    - "!scripts/**"
    - "!.github/**"
```

### V12 DNA Review Checklist

CodeRabbit applies these checks on every PR (configured via dashboard):

1. **V12 Naming Convention**: All files/classes must have `V12_001` (Panel) or `V12_002` (Strategy) prefix
2. **Lock-Free Mandate**: Flag any `lock()` usage as CRITICAL - require `Interlocked` or Actor model
3. **Complexity Threshold**: Flag functions with cyclomatic complexity >15 (Jane Street threshold)
4. **Atomic Operations**: Verify `Interlocked.CompareExchange` operates on shared state, not local copies
5. **ASCII-Only Compliance**: No emoji, curly quotes, em-dashes, Unicode arrows in string literals
6. **Struct Semantics**: Check for struct copy mutations (should use `ref` parameters)
7. **Zero-Allocation**: Flag LINQ in hot paths (`OnBarUpdate`, `OnOrderUpdate`, `OnExecutionUpdate`)
8. **Benchmark Methodology**: Void benchmarks risk JIT elimination - require return values
9. **Circuit Breakers**: Verify all `Interlocked.Decrement` paths check reset condition
10. **Correctness by Construction**: Prefer type system enforcement over runtime checks

### Review Severity Levels

| Level | Description | Action Required |
|-------|-------------|-----------------|
| **CRITICAL** | P0 blocking issues (lock usage, race conditions) | MUST FIX before merge |
| **HIGH** | P1 issues (complexity >15, struct bugs) | MUST FIX or justify |
| **MEDIUM** | P2 issues (style, encapsulation) | SHOULD FIX or defer |
| **LOW** | Suggestions (refactoring opportunities) | OPTIONAL |

### CodeRabbit Commands Reference

| Command | Purpose | Example |
|---------|---------|---------|
| `@coderabbitai review` | Trigger new review | Use after major changes |
| `@coderabbitai resolve` | Resolve all comments | After fixing all issues |
| `@coderabbitai issue` | Create GitHub issue | Reply to specific comment |
| `@coderabbitai pause` | Pause auto-reviews | During WIP commits |
| `@coderabbitai resume` | Resume auto-reviews | When ready for review |
| `@coderabbitai summary` | Regenerate PR summary | After description updates |

### Integration with Other Bots

CodeRabbit works alongside:
- **Semgrep**: V12 DNA pattern matching (pre-push + PR comments when installed)
- **Cubic**: Jane Street principle enforcement (complementary)
- **Greptile**: Architectural analysis (complementary)
- **Amazon Q**: Deep logic analysis (complementary)
- **Codacy**: Static analysis (overlapping, CodeRabbit preferred)
- **SonarCloud**: Quality metrics (overlapping, both useful)

**Recommendation**: Keep Semgrep + CodeRabbit + Cubic + Greptile as primary reviewers. Others provide supplementary signals.

### Troubleshooting

**Issue**: CodeRabbit not commenting on PR
- **Solution**: Check `.coderabbit.yaml` syntax, verify bot has repo access

**Issue**: Too many false positives
- **Solution**: Update hallucination log, adjust review profile to `chill`

**Issue**: Missing V12 DNA checks
- **Solution**: Update custom instructions in CodeRabbit dashboard

**Issue**: Review timeout
- **Solution**: Split large PRs, use path filters to exclude non-critical files

### Performance Impact

- **Review Time**: 2-5 minutes for typical PR (10-20 files)
- **Token Cost**: ~$0.10-0.50 per review (Pro plan)
- **False Positive Rate**: ~5-10% (logged in hallucination tracker)
- **P0 Detection Rate**: ~95% (critical bugs caught)

### Success Metrics

Track these KPIs:
- **P0 Bugs Caught**: Target >90%
- **False Positive Rate**: Target <10%
- **Review Turnaround**: Target <5 minutes
- **Developer Satisfaction**: Survey quarterly

---

## Related Documentation

- **Semgrep Setup**: `docs/setup/SEMGREP_SETUP.md`
- **Bot Installation Guide**: `docs/setup/GITHUB_APPS_INSTALLATION.md`
- **CodeFactor Protocol**: `docs/protocol/CODEFACTOR_PROTOCOL.md`
- **Agent Hierarchy**: `AGENTS.md`
- **PR Loop Command**: `.bob/commands/pr-loop.md`
- **Pre-Push Validation**: `.bob/commands/pre-push.md`