# Branch Guard Protocol (V12.18)

## CRITICAL: File Type Enforcement by Branch Pattern

### Rule 1: Source Code Branches (`feature/src-*`, `fix/src-*`)

**ALLOWED:**
- ✅ `.cs` files in `src/` directory ONLY
- ✅ `.csproj` files (if modifying project structure)

**STRICTLY FORBIDDEN:**
- ❌ Any files in `docs/`
- ❌ Any files in `scripts/`
- ❌ Any `.md` files (including `docs/brain/`)
- ❌ Any `.yml`, `.yaml`, `.json` config files
- ❌ Any `.ps1`, `.py`, `.sh` scripts
- ❌ Any image files (`.jpg`, `.png`, etc.)

**Enforcement:**
- Before EVERY commit on `feature/src-*` or `fix/src-*` branches:
  1. Check staged files with `git diff --cached --name-only`
  2. If ANY non-.cs file is staged → ABORT commit
  3. Stash non-.cs files: `git stash push -m "infra-changes" -- <non-cs-files>`
  4. Commit ONLY .cs files
  5. Report stashed files to user for separate infra branch

### Rule 2: Infrastructure Branches (`feature/infra-*`, `fix/infra-*`)

**ALLOWED:**
- ✅ Any files in `docs/`
- ✅ Any files in `scripts/`
- ✅ Config files (`.yml`, `.yaml`, `.json`, `.toml`)
- ✅ Markdown files
- ✅ PowerShell/Python/Shell scripts

**STRICTLY FORBIDDEN:**
- ❌ Any `.cs` files in `src/`

**Enforcement:**
- Before EVERY commit on `feature/infra-*` or `fix/infra-*` branches:
  1. Check staged files with `git diff --cached --name-only`
  2. If ANY .cs file in `src/` is staged → ABORT commit
  3. Report violation to user

### Rule 3: Protocol Fix Branches (`feature/protocol-*`)

**ALLOWED:**
- ✅ Files in `.bob/`, `.agent/`, `.github/`
- ✅ `AGENTS.md`, `CLAUDE.md`, `BOB.md`, etc.
- ✅ Files in `docs/protocol/`

**FORBIDDEN:**
- ❌ Any `.cs` files in `src/`
- ❌ Any files in `scripts/` (unless protocol-related)

### Rule 4: Emergency Hotfix Exception

**ONLY for `hotfix/*` branches:**
- Mixed file types allowed BUT:
  1. Must document reason in commit message
  2. Must split into separate commits: `[SRC]` and `[INFRA]` prefixes
  3. Must create follow-up tickets to separate properly

### Violation Response

If Bob IDE attempts to commit mixed file types:

1. **STOP immediately**
2. **Display violation message:**
   ```
   ⛔ BRANCH GUARD VIOLATION
   Branch: <branch-name>
   Attempted to commit: <file-list>
   Violation: [src/infra mixing detected]
   
   Action required:
   - Stash non-conforming files
   - Commit only allowed file types
   - Switch to appropriate branch for other files
   ```
3. **Auto-stash violating files** (if possible)
4. **Log violation** to `docs/brain/branch_guard_violations.log`

### Pre-Commit Hook Integration

This rule is enforced by:
- Bob Shell internal checks (this file)
- Git pre-commit hook (if installed)
- Manual verification before push

### Rationale

**Why this matters:**
- ✅ Clean PR diffs (no noise from docs/configs)
- ✅ Focused code reviews (bots only see .cs changes)
- ✅ Token efficiency (no wasted context on unrelated files)
- ✅ Clear git history (intent is obvious from branch name)
- ✅ Independent merge cycles (infra can merge fast, code gets thorough review)

**Jane Street Alignment:**
- Separation of concerns
- Predictable change scope
- Reduced cognitive load during review
- Atomic, verifiable changes

### Enforcement Date

**Effective:** 2026-05-29 (V12.18)
**Mandatory Compliance:** All Bob IDE sessions, all branches
