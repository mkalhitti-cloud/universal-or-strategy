# GitHub Account Migration Plan: PR #11

**Source Account**: mdasdispatch-hash  
**Target Account**: malhitticrypto (malhitticrypto@gmail.com)  
**Repository**: universal-or-strategy  
**Branch**: 1111.010-epic5-perf  
**Date**: 2026-05-25

---

## Migration Strategy

We'll use a **fork-based migration** approach since you haven't cloned the repo in the new account yet.

---

## Step-by-Step Plan

### Phase 1: Repository Setup (USER ACTION REQUIRED)

#### 1.1 Fork Repository to New Account
**Action**: You need to do this manually
1. Go to: https://github.com/mdasdispatch-hash/universal-or-strategy
2. Click "Fork" button (top right)
3. Select your new account: **malhitticrypto**
4. Keep repository name: `universal-or-strategy`
5. ✅ Check "Copy the main branch only" (we'll push our branch separately)
6. Click "Create fork"

**Result**: You'll have `malhitticrypto/universal-or-strategy` with only the `main` branch

---

#### 1.2 Configure GitHub Secrets (USER ACTION REQUIRED)
Based on the screenshot showing your old account's secrets, you need to create these in the **new account**:

Navigate to: `https://github.com/malhitticrypto/universal-or-strategy/settings/secrets/actions`

**Required Secrets** (copy values from old account):
1. `CODACY_PROJECT_TOKEN` - For Codacy integration
2. `CODECOV_TOKEN` - For code coverage reports
3. `GEMINI_API_KEY` - For Gemini Code Assist bot
4. `SONAR_TOKEN` - For SonarCloud analysis

**How to get values**:
- Open old account secrets page (you showed in screenshot)
- You cannot view secret values directly, so you'll need to:
  - Check your password manager / secure notes
  - OR regenerate tokens from each service:
    - **Codacy**: https://app.codacy.com/account/apiTokens
    - **Codecov**: https://codecov.io/account/gh/malhitticrypto/access
    - **Gemini**: https://aistudio.google.com/app/apikey
    - **SonarCloud**: https://sonarcloud.io/account/security

---

#### 1.3 Install GitHub Apps (USER ACTION REQUIRED)
You mentioned you'll install these. Here's the list from the old account:

**Required Apps** (install on `malhitticrypto/universal-or-strategy`):
1. **CodeFactor** - https://github.com/apps/codefactor-io
2. **Codacy** - https://github.com/apps/codacy
3. **Gemini Code Assist** - https://github.com/marketplace/gemini-code-assist
4. **Sourcery-ai** - https://github.com/apps/sourcery-ai
5. **Snyk** - https://github.com/apps/snyk-io
6. **Qlty** - https://github.com/apps/qlty (if you have paid seat)
7. **StyleCop** - Usually runs via GitHub Actions, no app install needed

**Installation Steps** (for each app):
1. Click the app link above
2. Click "Install" or "Configure"
3. Select account: **malhitticrypto**
4. Select repository: **universal-or-strategy**
5. Grant requested permissions
6. Complete setup

---

### Phase 2: Local Repository Configuration (AGENT ACTION)

#### 2.1 Add New Remote
```bash
git remote add malhitticrypto https://github.com/malhitticrypto/universal-or-strategy.git
```

#### 2.2 Verify Remotes
```bash
git remote -v
```
Expected output:
```
origin          https://github.com/mdasdispatch-hash/universal-or-strategy.git (fetch)
origin          https://github.com/mdasdispatch-hash/universal-or-strategy.git (push)
malhitticrypto  https://github.com/malhitticrypto/universal-or-strategy.git (fetch)
malhitticrypto  https://github.com/malhitticrypto/universal-or-strategy.git (push)
```

---

### Phase 3: Push Branch to New Account (AGENT ACTION)

#### 3.1 Push Branch
```bash
git push malhitticrypto 1111.010-epic5-perf
```

#### 3.2 Verify Branch Exists
```bash
git ls-remote malhitticrypto 1111.010-epic5-perf
```

---

### Phase 4: Create Pull Request (AGENT ACTION)

#### 4.1 Generate PR Description
Use the forensics report to create a comprehensive PR description that includes:
- Summary of changes
- Bot feedback addressed
- Performance rationale
- Testing verification

#### 4.2 Create PR via GitHub CLI
```bash
gh pr create \
  --repo malhitticrypto/universal-or-strategy \
  --base main \
  --head 1111.010-epic5-perf \
  --title "[EPIC-5-PERF] T-W1: Revert ToArray() Anti-Pattern" \
  --body-file docs/brain/pr_11_description.md
```

---

### Phase 5: Verification (AGENT ACTION)

#### 5.1 Check PR Status
```bash
gh pr view --repo malhitticrypto/universal-or-strategy
```

#### 5.2 Monitor Bot Checks
Wait for all bot checks to complete:
- ✅ CodeFactor (should pass - style violations fixed)
- ✅ Gemini Code Assist (should pass - ToArray() reverted)
- ✅ Sourcery-ai (should pass - foreach restored)
- ✅ StyleCop (should pass - no SA1636 in PR scope)
- ⚠️ Codacy (may show existing debt, but no new issues)
- ⚠️ Snyk/Qlty (may be blocked by account limits)

---

## What You Need to Do (USER CHECKLIST)

### Before Agent Can Proceed:
- [ ] **Fork repository** to malhitticrypto account
- [ ] **Create GitHub secrets** (4 tokens: CODACY, CODECOV, GEMINI, SONAR)
- [ ] **Install GitHub Apps** (CodeFactor, Codacy, Gemini, Sourcery, Snyk, Qlty)
- [ ] **Verify fork exists**: Visit https://github.com/malhitticrypto/universal-or-strategy
- [ ] **Notify agent** when setup is complete

### After Agent Creates PR:
- [ ] Review PR on new account
- [ ] Verify all bot checks pass
- [ ] Merge PR when ready

---

## Agent Actions (AUTOMATED)

Once you complete the user checklist above, I will:
1. ✅ Add new remote (`malhitticrypto`)
2. ✅ Push branch `1111.010-epic5-perf` to new account
3. ✅ Generate PR description from forensics report
4. ✅ Create PR via GitHub CLI
5. ✅ Monitor bot check status
6. ✅ Report final status

---

## Expected Timeline

- **User Setup**: 15-20 minutes (fork + secrets + apps)
- **Agent Migration**: 2-3 minutes (automated)
- **Bot Checks**: 5-10 minutes (automated)
- **Total**: ~30 minutes end-to-end

---

## Rollback Plan

If migration fails:
1. Delete fork from malhitticrypto account
2. Remove `malhitticrypto` remote: `git remote remove malhitticrypto`
3. Original PR #11 on mdasdispatch-hash remains untouched
4. Can retry migration or push fixes to original account

---

## Notes

- **No data loss risk**: Original repo and PR #11 remain intact
- **Clean slate**: New account starts with fixed code (no bot debt from old PR)
- **Bot coverage**: All bots will re-run on new PR with fresh context
- **Secrets security**: Never commit secrets to git; always use GitHub Secrets

---

## Next Steps

**WAITING FOR USER**: Please complete the "User Checklist" above, then reply with:
> "Setup complete - ready for migration"

I will then execute Phase 2-5 automatically.