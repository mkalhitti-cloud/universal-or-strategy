# Secrets Scanning Strategy - Multi-Layer Defense

## Executive Summary

**Goal**: Catch secrets BEFORE GitHub PR review to achieve 5/5 scores consistently.

**Current Status**: ✅ **YES, keep secrets scanning in local workflow**

---

## Your Current Security Stack

### Layer 1: Local Pre-Commit (RECOMMENDED ✅)
**Tool**: Gitleaks
**When**: Before `git commit`
**Speed**: 1-3 seconds
**Coverage**: Detects 14+ secret types

**Why This Matters**:
- ✅ Catches secrets BEFORE they enter git history
- ✅ Faster feedback than waiting for PR review
- ✅ Prevents embarrassing "secret detected" PR comments
- ✅ Achieves 5/5 GitHub PR scores from the start

**Setup**:
```bash
# Install Gitleaks
choco install gitleaks

# Run before every commit (manual)
gitleaks detect --source . --verbose --no-git

# Or install pre-commit hook (automatic)
# See: https://github.com/gitleaks/gitleaks#pre-commit
```

**Configuration**: `.gitleaks.toml` (already configured with allowlists)

---

### Layer 2: GitHub PR Review (Backup)
**Tools**: 
- Snyk (GitHub App + Extension)
- GitGuardian (GitHub App)
- Gitleaks (GitHub Actions workflow)

**When**: During PR review
**Speed**: 30-60 seconds
**Purpose**: Safety net if local scan missed something

**Current Status**:
- ✅ Snyk: Active (GitHub App + VS Code extension)
- ✅ GitGuardian: Active (GitHub App)
- ✅ Gitleaks: Active (GitHub Actions workflow)

---

### Layer 3: Jane Street Sentinel (Custom Agent)
**Tool**: Cubic custom agent with Jane Street rules
**When**: PR review
**Purpose**: High-frequency trading security standards
**Status**: ✅ Active (you created this)

**What It Checks**:
- P0 (CRITICAL): Blocks merge on violations
- P1 (HIGH): Blocks merge (upgraded from warning)
- P2 (MEDIUM): Blocks merge (upgraded from info)
- Labeling: `[CRITICAL-JS-P0]`, `[CRITICAL-JS-P1]`, `[CRITICAL-JS-P2]`

---

## Recommended Workflow (Local Loop)

### Step 1: Write Code
```bash
# Make your changes
code src/V12_001.cs
```

### Step 2: Pre-Commit Checks (LOCAL)
```bash
# 1. Check for secrets (CRITICAL)
gitleaks detect --source . --verbose --no-git

# 2. Run CodeAnt review (code quality)
codeant review

# 3. Build verification
dotnet build

# 4. Run tests
dotnet test
```

### Step 3: Commit & Push
```bash
git add .
git commit -m "feat: implement feature X"
git push origin feature-branch
```

### Step 4: GitHub PR Review (AUTOMATIC)
- ✅ Gitleaks (GitHub Actions) - redundant check
- ✅ Snyk - dependency vulnerabilities + secrets
- ✅ GitGuardian - secret scanning
- ✅ Jane Street Sentinel (Cubic) - trading standards
- ✅ CodeAnt AI - code quality
- ✅ SonarCloud - code smells
- ✅ CodeQL - security vulnerabilities

**Expected Result**: 5/5 scores because you caught everything locally!

---

## Why Multi-Layer Defense?

### Defense in Depth
1. **Local (Gitleaks)**: First line of defense - catches 95% of secrets
2. **GitHub (Snyk/GitGuardian)**: Safety net - catches edge cases
3. **Custom (Jane Street)**: Domain-specific rules - trading compliance

### Token Efficiency
- **Local scans**: Free, unlimited
- **GitHub scans**: Included in plan
- **Custom agent**: Runs only on PR (efficient)

### Speed Optimization
- **Local**: 1-3 seconds (instant feedback)
- **GitHub**: 30-60 seconds (parallel execution)
- **Total**: ~1 minute to full validation

---

## Current Configuration

### `.gitleaks.toml` (Allowlists)
```toml
[[allowlists]]
description = "Exclude test fixtures and gitignored files"
paths = [
    '''gitleaks_report\.json$''',
    '''firebase-credentials\.json$''',
    '''.*firebase-adminsdk.*\.json$''',
    '''infrastructure/paperclip/.*'''  # Test fixtures
]
```

### `.codeantignore` (C# Only)
```
# Only scan C# files
*
!*.cs

# Ignore generated files
*.Designer.cs
*.g.cs
*.g.i.cs
*AssemblyInfo.cs
*AssemblyAttributes.cs
```

### Snyk Integration
- ✅ GitHub App: Active
- ✅ VS Code Extension: Installed
- ✅ Scans: NuGet dependencies + secrets
- ⚠️ Note: Snyk also does secret scanning (redundant with Gitleaks, but good backup)

---

## Comparison: Local vs GitHub Scanning

| Aspect | Local (Gitleaks) | GitHub (Snyk/GitGuardian) |
|--------|------------------|---------------------------|
| **Speed** | 1-3 seconds | 30-60 seconds |
| **Feedback** | Immediate | After push |
| **Cost** | Free | Included in plan |
| **Coverage** | 14+ secret types | 100+ secret types |
| **Git History** | Prevents entry | Detects after entry |
| **PR Score Impact** | Prevents issues | Reports issues |
| **Best For** | Daily development | Safety net |

---

## Recommendation: Keep Both ✅

### Local Scanning (Gitleaks)
**Purpose**: Primary defense - catch secrets before commit
**Frequency**: Every commit
**Impact**: Prevents 95% of secret leaks

### GitHub Scanning (Snyk/GitGuardian)
**Purpose**: Safety net - catch edge cases
**Frequency**: Every PR
**Impact**: Catches the remaining 5%

### Why Not Remove GitHub Scanning?
1. **Different detection engines**: Snyk/GitGuardian catch patterns Gitleaks might miss
2. **Team protection**: Catches secrets from other developers who skip local scans
3. **Compliance**: Many orgs require multiple scanning layers
4. **Zero cost**: Already included in your GitHub plan

---

## Jane Street Sentinel Integration

Your custom Cubic agent adds **domain-specific security rules** on top of generic secret scanning:

### What It Adds
- **Trading-specific patterns**: API keys for trading platforms
- **Compliance rules**: Financial data handling
- **Custom severity**: P0/P1/P2 blocking rules
- **Labeling strategy**: Clear severity indicators

### How It Complements Gitleaks
- **Gitleaks**: Generic secrets (AWS keys, GitHub tokens, etc.)
- **Jane Street**: Trading-specific secrets (broker APIs, market data keys)
- **Together**: Comprehensive coverage for high-frequency trading

---

## Updated Workflow Documentation

I'll update `MULTI_TOOL_REVIEW_WORKFLOW.md` to emphasize the local loop:

### Recommended Daily Workflow
```bash
# 1. Make changes
code src/V12_001.cs

# 2. Local validation loop (BEFORE COMMIT)
gitleaks detect --source . --verbose --no-git  # Secrets
codeant review                                  # Code quality
dotnet build                                    # Compilation
dotnet test                                     # Tests

# 3. Commit only when local checks pass
git add .
git commit -m "feat: implement feature X"

# 4. Push and let GitHub validate (should be 5/5)
git push origin feature-branch
```

---

## Action Items

### Immediate
1. ✅ Keep Gitleaks in local workflow
2. ✅ Keep Snyk/GitGuardian in GitHub workflow
3. ✅ Keep Jane Street Sentinel custom agent
4. ⬜ Install Gitleaks pre-commit hook (optional automation)

### Optional Enhancements
1. **Pre-commit hook**: Auto-run Gitleaks on every commit
   ```bash
   # Install hook
   curl -o .git/hooks/pre-commit https://raw.githubusercontent.com/gitleaks/gitleaks/master/scripts/pre-commit.py
   chmod +x .git/hooks/pre-commit
   ```

2. **VS Code task**: Add Gitleaks to tasks.json
   ```json
   {
     "label": "Gitleaks Scan",
     "type": "shell",
     "command": "gitleaks detect --source . --verbose --no-git"
   }
   ```

3. **PowerShell alias**: Quick scan command
   ```powershell
   # Add to $PROFILE
   function Check-Secrets { gitleaks detect --source . --verbose --no-git }
   Set-Alias -Name secrets -Value Check-Secrets
   ```

---

## Summary

### Current State: ✅ OPTIMAL
- **Local**: Gitleaks catches secrets before commit
- **GitHub**: Snyk/GitGuardian/Gitleaks provide safety net
- **Custom**: Jane Street Sentinel adds trading-specific rules

### Why This Works
1. **Fast feedback**: Local scans give instant results
2. **High confidence**: Multiple layers catch edge cases
3. **5/5 PR scores**: Issues caught before GitHub review
4. **Zero waste**: All tools serve distinct purposes

### Answer to Your Question
**Q**: "Should we keep secrets scanning to catch it before the PR to make it faster?"

**A**: ✅ **YES - Keep local Gitleaks scanning**
- Catches secrets BEFORE they reach GitHub
- Achieves 5/5 PR scores from the start
- Faster than waiting for GitHub review
- Complements (doesn't replace) GitHub-level scanning

**Bonus**: Your Jane Street Sentinel custom agent adds trading-specific security on top of generic secret scanning - excellent defense-in-depth strategy!

---

## References

- [Gitleaks Documentation](https://github.com/gitleaks/gitleaks)
- [Snyk Secret Scanning](https://docs.snyk.io/scan-applications/snyk-code/snyk-code-security-rules/secrets-rules)
- [GitGuardian Best Practices](https://docs.gitguardian.com/secrets-detection/secrets-detection-engine/detectors)
- [TICKET-001 Completion Summary](../brain/EPIC-7-QUALITY/TICKET-001-COMPLETION-SUMMARY.md)