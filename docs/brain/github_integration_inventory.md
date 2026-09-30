# GitHub Integration Inventory - Complete Audit

**Repository**: universal-or-strategy  
**Audit Date**: 2026-05-25  
**Purpose**: Complete inventory for account migration (mdasdispatch-hash → malhitticrypto)

---

## 1. GitHub Apps (Install via GitHub Marketplace)

### Code Quality & Review Bots
1. **CodeFactor** ✅
   - URL: https://github.com/apps/codefactor-io
   - Purpose: Style violations, code smells
   - Config: None (uses default rules)
   - Status: Active on PR #11

2. **Codacy** ✅
   - URL: https://github.com/apps/codacy
   - Purpose: Code quality, complexity analysis
   - Config: `.codacy.yml` (complexity threshold: 15)
   - Secret Required: `CODACY_PROJECT_TOKEN`
   - Status: Active on PR #11

3. **CodeRabbit** ✅
   - URL: https://github.com/apps/coderabbitai
   - Purpose: AI-powered code review
   - Config: `.coderabbit.yaml` (V12 DNA rules)
   - Status: Likely active (config present)

4. **DeepSource** ✅
   - URL: https://github.com/apps/deepsource-io
   - Purpose: C# static analysis
   - Config: `.deepsource.toml`
   - Status: Likely active (config present)

5. **Sourcery-ai** ✅
   - URL: https://github.com/apps/sourcery-ai
   - Purpose: Python/general code suggestions
   - Status: Active on PR #11

6. **Gemini Code Assist** ✅
   - URL: https://github.com/marketplace/gemini-code-assist
   - Purpose: AI code review (Google)
   - Secret Required: `GEMINI_API_KEY`
   - Status: Active on PR #11 (High Priority feedback)

### Security Scanning
7. **Snyk** ✅
   - URL: https://github.com/apps/snyk-io
   - Purpose: Dependency vulnerability scanning
   - Status: Active on PR #11 (test limit reached)

8. **Qlty** ⚠️
   - URL: https://github.com/apps/qlty
   - Purpose: Code quality metrics
   - Status: Requires paid seat (blocked on PR #11)

### Dependency Management
9. **Dependabot** ✅ (Built-in)
   - Config: `.github/dependabot.yml`
   - Purpose: Automated dependency updates
   - Reviewer: mkalhitti-cloud (UPDATE TO NEW ACCOUNT)

---

## 2. GitHub Actions Workflows

### Active Workflows (`.github/workflows/`)
1. **pr-agent.yml** ✅
   - Purpose: PR-Agent AI review
   - Config: `.pr_agent.toml`
   - Triggers: PR open/sync
   - Status: Active

2. **codacy-coverage.yml** ✅
   - Purpose: Code coverage reporting to Codacy
   - Secret: `CODACY_PROJECT_TOKEN`
   - Triggers: Push to main, PRs

3. **codecov.yml** ✅
   - Purpose: Code coverage reporting to Codecov
   - Secret: `CODECOV_TOKEN`
   - Triggers: Push to main, PRs

4. **sonarcloud.yml** ✅
   - Purpose: SonarCloud static analysis
   - Secret: `SONAR_TOKEN`
   - Triggers: Push to main, PRs
   - Note: Partial analysis (no NinjaTrader refs)

5. **codeql.yml** ✅
   - Purpose: GitHub CodeQL security scanning
   - Secret: None (uses GITHUB_TOKEN)
   - Triggers: Push to main, PRs, schedule

6. **dotnet-build.yml** ✅
   - Purpose: .NET build verification
   - Triggers: Push, PRs

7. **dotnet-test.yml** ✅
   - Purpose: .NET test execution
   - Triggers: Push, PRs

8. **gitleaks.yml** ✅
   - Purpose: Secret scanning
   - Config: `.gitleaks.toml`
   - Triggers: Push, PRs

9. **stylecop-enforcement.yml** ✅
   - Purpose: StyleCop analyzer enforcement
   - Config: `stylecop.json`
   - Triggers: Push, PRs

10. **osv-scanner.yml** ✅
    - Purpose: OSV vulnerability scanning
    - Triggers: Push, PRs

11. **sentinel-pyramid.yml** ✅
    - Purpose: Custom security checks
    - Triggers: Push, PRs

12. **markdown-link-check.yml** ✅
    - Purpose: Validate markdown links
    - Config: `.github/mlc_config.json`
    - Triggers: Push, PRs

13. **labeler.yml** ✅
    - Purpose: Auto-label PRs
    - Config: `.github/labeler.yml`
    - Triggers: PR open/sync

14. **release-drafter.yml** ✅
    - Purpose: Auto-generate release notes
    - Config: `.github/release-drafter.yml`
    - Triggers: Push to main

15. **stale.yml** ✅
    - Purpose: Mark stale issues/PRs
    - Triggers: Schedule (daily)

16. **upstream-sync.yml** ✅
    - Purpose: Sync with upstream (if forked)
    - Triggers: Manual/schedule

### Disabled Workflows
17. **claude-code-review.yml.disabled** ⚠️
    - Status: Disabled (not active)

---

## 3. GitHub Secrets Required

### Repository Secrets (Settings → Secrets → Actions)
1. **CODACY_PROJECT_TOKEN** ✅
   - Used by: `codacy-coverage.yml`
   - Get from: https://app.codacy.com/account/apiTokens

2. **CODECOV_TOKEN** ✅
   - Used by: `codecov.yml`
   - Get from: https://codecov.io/account/gh/malhitticrypto/access

3. **GEMINI_API_KEY** ✅
   - Used by: Gemini Code Assist bot
   - Get from: https://aistudio.google.com/app/apikey

4. **SONAR_TOKEN** ✅
   - Used by: `sonarcloud.yml`
   - Get from: https://sonarcloud.io/account/security

### Built-in Secrets (Auto-provided)
5. **GITHUB_TOKEN** ✅
   - Auto-provided by GitHub Actions
   - No setup required

---

## 4. Configuration Files to Migrate

### Code Quality Configs
- `.codacy.yml` ✅ (already in repo)
- `.coderabbit.yaml` ✅ (already in repo)
- `.deepsource.toml` ✅ (already in repo)
- `.pr_agent.toml` ✅ (already in repo)
- `stylecop.json` ✅ (already in repo)
- `.gitleaks.toml` ✅ (already in repo)

### GitHub-Specific Configs
- `.github/dependabot.yml` ⚠️ (UPDATE REVIEWER)
- `.github/CODEOWNERS` ⚠️ (UPDATE OWNERS if present)
- `.github/labeler.yml` ✅ (already in repo)
- `.github/release-drafter.yml` ✅ (already in repo)

---

## 5. External Service Integrations

### Services Requiring Account Setup
1. **Codacy** (https://app.codacy.com)
   - Action: Link new GitHub account
   - Generate new project token

2. **Codecov** (https://codecov.io)
   - Action: Link new GitHub account
   - Generate new token

3. **SonarCloud** (https://sonarcloud.io)
   - Action: Link new GitHub account
   - Create new organization: `malhitticrypto`
   - Generate new token

4. **DeepSource** (https://deepsource.io)
   - Action: Link new GitHub account
   - Activate repository

5. **CodeRabbit** (https://coderabbit.ai)
   - Action: Link new GitHub account
   - Activate repository

---

## 6. Migration Checklist (UPDATED)

### Phase 1: Fork & Basic Setup
- [ ] Fork repository to `malhitticrypto` account
- [ ] Verify fork exists: https://github.com/malhitticrypto/universal-or-strategy

### Phase 2: Install GitHub Apps (9 apps)
- [ ] CodeFactor
- [ ] Codacy
- [ ] CodeRabbit
- [ ] DeepSource
- [ ] Sourcery-ai
- [ ] Gemini Code Assist
- [ ] Snyk
- [ ] Qlty (optional - requires paid seat)
- [ ] Dependabot (built-in, auto-enabled)

### Phase 3: Configure External Services (4 services)
- [ ] **Codacy**: Link account, generate token
- [ ] **Codecov**: Link account, generate token
- [ ] **SonarCloud**: Link account, create org, generate token
- [ ] **DeepSource**: Link account, activate repo
- [ ] **CodeRabbit**: Link account, activate repo

### Phase 4: Create GitHub Secrets (4 secrets)
- [ ] `CODACY_PROJECT_TOKEN`
- [ ] `CODECOV_TOKEN`
- [ ] `GEMINI_API_KEY`
- [ ] `SONAR_TOKEN`

### Phase 5: Update Configuration Files
- [ ] Update `.github/dependabot.yml` reviewer (mkalhitti-cloud → malhitticrypto)
- [ ] Update `.github/CODEOWNERS` if present
- [ ] Verify all config files are present in fork

### Phase 6: Agent Automation (After User Completes Above)
- [ ] Add `malhitticrypto` remote
- [ ] Push branch `1111.010-epic5-perf`
- [ ] Generate PR description
- [ ] Create PR via GitHub CLI
- [ ] Monitor bot checks
- [ ] Report final status

---

## 7. Bot Check Expectations (New PR)

### Should PASS ✅
- CodeFactor (style violations fixed)
- Gemini Code Assist (ToArray() reverted)
- Sourcery-ai (foreach restored)
- StyleCop (no SA1636 in PR scope)
- Gitleaks (no secrets)
- CodeQL (no security issues)
- OSV Scanner (no vulnerabilities)

### May Show Warnings ⚠️
- Codacy (existing technical debt, but no NEW issues)
- SonarCloud (partial analysis, existing debt)
- DeepSource (existing debt)

### May Be Blocked ❌
- Snyk (test limit - account dependent)
- Qlty (requires paid seat)

---

## 8. Time Estimates

### User Setup Time
- Fork repository: 1 minute
- Install 9 GitHub Apps: 10-15 minutes (1-2 min each)
- Configure 5 external services: 15-20 minutes (3-4 min each)
- Create 4 GitHub secrets: 5 minutes
- Update config files: 2 minutes
- **Total User Time**: ~35-45 minutes

### Agent Automation Time
- Add remote + push branch: 1 minute
- Generate PR description: 1 minute
- Create PR: 30 seconds
- **Total Agent Time**: ~2-3 minutes

### Bot Check Time
- Initial checks: 5-10 minutes
- **Total End-to-End**: ~45-60 minutes

---

## 9. Rollback Plan

If migration fails at any point:
1. Delete fork: `https://github.com/malhitticrypto/universal-or-strategy/settings`
2. Remove local remote: `git remote remove malhitticrypto`
3. Original repo remains untouched
4. Can retry migration or push to original account

---

## 10. Post-Migration Verification

After PR is created on new account:
- [ ] All 16 GitHub Actions workflows trigger
- [ ] All bot checks appear in PR
- [ ] No authentication errors in workflow logs
- [ ] Secrets are accessible (check workflow logs)
- [ ] PR description renders correctly
- [ ] Bot feedback is actionable

---

## Notes

- **No Data Loss Risk**: Original repo and PR #11 remain intact
- **Clean Slate**: New account starts with fixed code (no bot debt)
- **Full Coverage**: All 9 bots + 16 workflows will run on new PR
- **Secrets Security**: Never commit secrets; always use GitHub Secrets UI

---

**Status**: Waiting for user to complete Phases 1-5  
**Next Step**: User replies "Setup complete - ready for migration"