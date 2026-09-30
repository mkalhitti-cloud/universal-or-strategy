# ⚠️ DEPRECATED - DO NOT USE

**Status**: DEPRECATED as of V12.19.1 (2026-05-26)
**Superseded By**: `docs/protocol/SRC_ONLY_PUSH.md`
**Reason**: Conflicted with Src-Only Push Protocol, caused agent confusion

---

# PR Separation Enforcement Protocol (DEPRECATED)

**Version**: 2.0
**Effective**: 2026-05-25
**Status**: ~~MANDATORY~~ **DEPRECATED**

## The Rule

**src/ and non-src/ files MUST NEVER be in the same commit or PR.**

### Workflow by File Type

- **`src/` changes**: MUST go through PR review process
- **non-`src/` changes**: MUST be committed directly to `main` (no PR review)

### Rationale

1. **Token Conservation**: Review bots (Gemini Code Assist, Sourcery-ai, CodeFactor, Codacy) consume significant tokens on every PR. Non-`src/` changes (docs, scripts, configs, tests) don't require bot review and should bypass the PR process entirely to conserve review tokens.

2. **Focused Code Review**: `src/` changes require deep architectural review with V12 DNA compliance checks (lock-free patterns, ASCII-only, Jane Street alignment). Non-`src/` changes are typically straightforward and don't benefit from bot analysis.

3. **Reduced Cognitive Load**: Agents don't get overwhelmed by mixed contexts when reviewing pure `src/` PRs

4. **Better Bot Performance**: Code analysis bots work best on pure code PRs without documentation noise

## Enforcement Mechanism

### Pre-Push Validation (scripts/pre_push_validation.ps1)

Add this check after the diff size check:

```powershell
# Check for mixed src/ and non-src/ files
$srcFiles = git diff --name-only origin/main...HEAD | Where-Object { $_ -match '^src/' }
$nonSrcFiles = git diff --name-only origin/main...HEAD | Where-Object { $_ -notmatch '^src/' }

if ($srcFiles -and $nonSrcFiles) {
    Write-Host "[FAIL] PR Separation Violation" -ForegroundColor Red
    Write-Host "  src/ files and non-src/ files cannot be in the same PR" -ForegroundColor Red
    Write-Host ""
    Write-Host "  src/ files:" -ForegroundColor Yellow
    $srcFiles | ForEach-Object { Write-Host "    - $_" -ForegroundColor Yellow }
    Write-Host ""
    Write-Host "  non-src/ files:" -ForegroundColor Yellow
    $nonSrcFiles | ForEach-Object { Write-Host "    - $_" -ForegroundColor Yellow }
    Write-Host ""
    Write-Host "  Solution: Create two separate PRs:" -ForegroundColor Cyan
    Write-Host "    1. PR with src/ changes only" -ForegroundColor Cyan
    Write-Host "    2. PR with non-src/ changes only" -ForegroundColor Cyan
    exit 1
}

Write-Host "[PASS] PR Separation" -ForegroundColor Green
Write-Host "  Clean separation (src-only or non-src-only)" -ForegroundColor Green
```

### GitHub Actions Workflow (.github/workflows/pr-separation-check.yml)

```yaml
name: PR Separation Check

on:
  pull_request:
    types: [opened, synchronize, reopened]

jobs:
  check-separation:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      
      - name: Check PR Separation
        run: |
          # Get changed files
          git fetch origin ${{ github.base_ref }}
          SRC_FILES=$(git diff --name-only origin/${{ github.base_ref }}...HEAD | grep '^src/' || true)
          NON_SRC_FILES=$(git diff --name-only origin/${{ github.base_ref }}...HEAD | grep -v '^src/' || true)
          
          # Check for violation
          if [ -n "$SRC_FILES" ] && [ -n "$NON_SRC_FILES" ]; then
            echo "❌ PR Separation Violation"
            echo ""
            echo "src/ files and non-src/ files cannot be in the same PR."
            echo ""
            echo "src/ files:"
            echo "$SRC_FILES" | sed 's/^/  - /'
            echo ""
            echo "non-src/ files:"
            echo "$NON_SRC_FILES" | sed 's/^/  - /'
            echo ""
            echo "Solution: Split into two PRs:"
            echo "  1. PR with src/ changes only"
            echo "  2. PR with non-src/ changes only"
            exit 1
          fi
          
          echo "✅ PR Separation Check Passed"
          if [ -n "$SRC_FILES" ]; then
            echo "  Type: src-only PR"
          else
            echo "  Type: non-src-only PR"
          fi
```

## Agent Workflow Integration

### For Bob CLI / Orchestrator

Before committing any changes, agents MUST:

1. **Check file types**:
   ```powershell
   $changedFiles = git status --porcelain | ForEach-Object { $_.Substring(3) }
   $hasSrc = $changedFiles | Where-Object { $_ -match '^src/' }
   $hasNonSrc = $changedFiles | Where-Object { $_ -notmatch '^src/' }
   ```

2. **If mixed, split into separate commits**:
   ```powershell
   if ($hasSrc -and $hasNonSrc) {
       # Commit non-src changes directly to main
       git checkout main
       git pull origin main
       git add (non-src files)
       git commit -m "docs: (description)"
       git push origin main
       
       # Create branch for src changes and PR
       git checkout -b "feature/src-changes"
       git add src/
       git commit -m "src: (description)"
       git push origin feature/src-changes
       gh pr create --title "src: (description)" --body "..."
   }
   ```

3. **Routing logic**:
   - **src-only changes**: Create feature branch → Push → Create PR
   - **non-src-only changes**: Commit directly to `main` → Push (no PR)
   - **Mixed changes**: FORBIDDEN - must split as shown above

## Exception Cases

### None

There are **NO exceptions** to this rule. Even if:
- Changes are "related" (e.g., code + docs for same feature)
- Changes are "small" (e.g., 1 line in src/ + 1 line in docs/)
- Changes are "urgent" (e.g., hotfix)

**Always split**: src/ → PR, non-src/ → direct commit to main.

## Migration Path for Existing Mixed PRs

If a mixed PR is detected:

1. **Close the mixed PR** with comment: "Closing: Violated src/ vs non-src/ separation rule"
2. **Extract non-src changes** and commit directly to `main`
3. **Create new src-only PR** from separate branch
4. **Link in PR description**: "Related documentation changes committed in [SHA]"

## Verification

After implementing enforcement:

1. **Test with mixed commit**:
   ```powershell
   git checkout -b test-mixed
   echo "test" >> src/test.cs
   echo "test" >> AGENTS.md
   git add -A
   git commit -m "test: mixed commit"
   git push origin test-mixed
   ```

2. **Verify pre-push hook fails**
3. **Verify GitHub Actions fails**
4. **Verify PR creation is blocked**

## Version History

- **2.0** (2026-05-25): Token conservation update
  - **BREAKING**: non-src changes now commit directly to main (no PR review)
  - Updated rationale to emphasize token conservation
  - Modified agent workflow to route non-src changes to main
  - Removed GitHub Actions workflow (only src/ PRs exist now)

- **1.0** (2026-05-25): Initial protocol
  - Defined mandatory separation rule
  - Created enforcement mechanisms
  - Documented agent workflow integration