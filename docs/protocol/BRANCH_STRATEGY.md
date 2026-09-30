# Three-Tier Branch Model (V12.18)

## Overview
Separates changes by **architectural layer** to prevent PR noise and enable independent review cycles.

---

## Tier 1: Source Code Branches (`feature/src-*`, `fix/src-*`)

**Purpose**: Production code changes only

**Allowed Files:**
- ✅ `.cs` files in `src/` directory
- ✅ `.csproj` files (project structure)
- ✅ Test files in `tests/` (if test-only changes)

**Forbidden:**
- ❌ Any `docs/`, `scripts/`, `.github/` files
- ❌ Config files (`.yml`, `.json`, `.toml`)
- ❌ Markdown files
- ❌ PowerShell/Python scripts

**Review Process:**
- ✅ **PR REQUIRED** (always)
- Full Arena AI + Codacy + CodeRabbit review
- Complexity audit required
- Build + test gates enforced
- Longest review cycle (2-3 days typical)

**Example branches:**
- `feature/src-epic-8-extract-sima`
- `fix/src-null-reference-atm`

---

## Tier 2: Infrastructure Branches (`feature/infra-*`, `fix/infra-*`)

**Purpose**: Tooling, scripts, documentation, configs

**Allowed Files:**
- ✅ `docs/` (all markdown, images, PDFs)
- ✅ `scripts/` (PowerShell, Python, Shell)
- ✅ `.github/` (workflows, actions, templates)
- ✅ Config files (`.codacy.yml`, `.editorconfig`, etc.)
- ✅ `docs/brain/` (session notes, forensics)

**Forbidden:**
- ❌ `.cs` files in `src/`

**Review Process:**
- ✅ **PR REQUIRED** (for git history)
- Lightweight review (no complexity audit)
- Markdown linting only
- Fast-track merge (same day typical)
- No bot review unless security-sensitive

**Example branches:**
- `feature/infra-pr18-fixes`
- `fix/infra-broken-deploy-script`

---

## Tier 3: Protocol Branches (`feature/protocol-*`, `fix/protocol-*`)

**Purpose**: Agent rules, workflows, meta-configuration

**Allowed Files:**
- ✅ `.bob/`, `.agent/`, `.claude/`, `.gemini/` directories
- ✅ `AGENTS.md`, `CLAUDE.md`, `BOB.md`, `CODEX.md`
- ✅ `docs/protocol/` (protocol documentation)
- ✅ `.mcp.json`, `bob.config.yaml`

**Forbidden:**
- ❌ `.cs` files in `src/`
- ❌ General scripts (unless protocol-related)

**Review Process (HYBRID):**
- ⚠️ **PR OPTIONAL** (Director's choice)
- **Default: Direct push to main** (95% of cases)
  - Faster: 2 minutes vs 5 minutes
  - 100% reliable (zero bot interference)
  - Git history preserved via commit messages
- **Optional: Create PR** (5% of cases)
  - When seeking second opinion
  - When change is complex/controversial
  - When you want formal review record
  - Protocol Guard workflow auto-blocks bots

**Workflow Options:**

**Option A: Direct Push (Recommended)**
```powershell
# Work on protocol branch
git checkout -b feature/protocol-fix-issue

# Make changes, commit
git commit -m "[PROTOCOL] Fix issue - rationale here"

# Merge directly to main
git checkout main
git merge feature/protocol-fix-issue --no-ff
git push origin main

# Delete branch
git branch -d feature/protocol-fix-issue
```

**Option B: PR Review (When Needed)**
```powershell
# Work on protocol branch
git checkout -b feature/protocol-complex-change

# Make changes, commit
git commit -m "[PROTOCOL] Complex change - needs review"

# Push and create PR
git push origin feature/protocol-complex-change
gh pr create --title "[PROTOCOL] Complex change" --body "Rationale..."

# Protocol Guard workflow will:
# - Auto-add 'protocol-only' label
# - Block bot reviews via CODEOWNERS
# - Post human-readable notice
```

**Example branches:**
- `feature/protocol-branch-guard` (direct push)
- `fix/protocol-bob-mode-enforcement` (direct push)
- `feature/protocol-major-refactor` (PR if complex)

---

## Workflow Examples

### Scenario 1: Pure Source Code Work
```
1. Create feature/src-epic-X
2. Bob IDE works here (ONLY .cs commits)
3. PR review (full bot suite)
4. Merge to main
```

### Scenario 2: Infrastructure + Source Code (Same Epic)
```
1. Create feature/infra-epic-X (infrastructure first)
2. Merge infra branch (fast-track)
3. Create feature/src-epic-X (source code)
4. Merge src branch (full review)
```

### Scenario 3: Protocol Fix During Source Work
```
1. Working on feature/src-epic-X
2. Bob makes mistake → protocol needs update
3. STOP src work
4. Create feature/protocol-fix-issue
5. Fix protocol, merge directly to main (no PR)
6. Resume src work on original branch
```

### Scenario 4: Emergency Hotfix (Mixed)
```
1. Create hotfix/critical-issue
2. Mixed commits allowed BUT:
   - Separate commits: [SRC] and [INFRA] prefixes
   - Document reason in commit message
3. Create follow-up tickets to separate properly
```

---

## Benefits

**Token Efficiency:**
- Infra PRs: ~500 tokens (no bot review)
- Src PRs: ~5,000 tokens (full review)
- Protocol direct push: 0 tokens (no PR)
- Savings: 90% on infra, 100% on protocol

**Review Speed:**
- Protocol: Immediate (direct push)
- Infra: Same day merge
- Src: 2-3 day review cycle

**Git History Clarity:**
- Branch name = intent obvious
- No "mixed bag" commits
- Easy to revert by layer

**Migration Safety:**
- Protocol changes deploy immediately
- No bot configuration needed on new accounts
- CODEOWNERS + Protocol Guard travel with repo

**Jane Street Alignment:**
- Separation of concerns
- Predictable change scope
- Reduced cognitive load

---

## Enforcement

**Branch Guard Rule:** `.bob/rules-v12-engineer/branch-guard.md`
- Auto-blocks mixed commits
- Auto-stashes violating files
- Logs violations

**Protocol Guard (Optional PR Path):** `.github/workflows/protocol-guard.yml`
- Auto-detects protocol file changes
- Auto-adds `protocol-only` label
- Posts human-readable notice
- Blocks bot reviews via CODEOWNERS

**Git Hooks:** (optional, not yet implemented)
- Pre-commit validation
- Branch pattern matching

**Tool Flexibility:**
- Bob IDE and Bob Shell can work on ANY branch type
- Branch model organizes commits, not tools
- Use the right tool for the task, regardless of branch

---

## Decision Matrix: When to Use PR vs Direct Push

| Change Type | Complexity | Urgency | Recommendation |
|-------------|-----------|---------|----------------|
| Protocol typo fix | Low | High | Direct push |
| Protocol rule tweak | Low | High | Direct push |
| New protocol file | Medium | Medium | Direct push (or PR if unsure) |
| Protocol refactor | High | Low | PR (seek review) |
| Protocol breaking change | High | Medium | PR (document impact) |

**Rule of Thumb**: If you're confident and it's <50 lines, direct push. If you want a second opinion, use PR.

---

## Current Status

**Implemented:**
- ✅ Branch guard rule created (`.bob/rules-v12-engineer/branch-guard.md`)
- ✅ Three-tier model documented
- ✅ Protocol Guard workflow (`.github/workflows/protocol-guard.yml`)
- ✅ CODEOWNERS updated for protocol paths

**Pending:**
- ⏳ Git pre-commit hooks (optional)
- ⏳ Fine-tuning as we use it in practice

**Effective Date:** 2026-05-29 (V12.18)