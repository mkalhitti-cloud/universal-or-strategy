# CodeFactor Protocol - Lessons Learned

## Date: 2026-05-22
## Context: REAPER-EXPANSION Phase 2.3

## CRITICAL LESSON: Never Use CodeFactor "Apply Fixes" Button

### What Happened
1. CodeFactor offered an "Apply fixes" button for 376 style issues
2. We accepted it, thinking it would be safe automated fixes
3. **Result**: Complete build failure with 320 compilation errors
4. **Root Cause**: CodeFactor made destructive refactorings:
   - Changed `BUILD_TAG` to `BUILDTAG` (removed underscore)
   - Changed `CIRCUIT_BREAKER_THRESHOLD` to `CIRCUITBREAKERTHRESHOLD`
   - Changed `LastKnownPrice` (property) to `lastKnownPrice` (non-existent field)
   - Changed `ema9Val` to `_ema9Val` (added underscore prefix)
   - 300+ similar corruptions across the codebase

### Recovery Actions
1. Verified build worked at commit 03ad47a (before CodeFactor)
2. Confirmed build failed at commit d75e5f1 (CodeFactor autofix)
3. Reverted branch: `git reset --hard 03ad47a`
4. Force pushed: `git push origin feat/reaper-expansion-phase2 --force`
5. Ran `deploy-sync.ps1` to sync 3 desynced files

## MANDATORY PROTOCOL

### Rule 1: NEVER Use Automated Fix Tools
- ❌ **BANNED**: CodeFactor "Apply fixes" button
- ❌ **BANNED**: Any automated refactoring tool without manual review
- ✅ **ALLOWED**: Manual fixes with explicit `apply_diff` commands
- ✅ **ALLOWED**: Fixes verified by build + test cycle

### Rule 2: Always Verify Build After External Changes
**Before accepting ANY external tool's changes:**
1. Checkout the commit: `git checkout <commit-hash>`
2. Run build: `dotnet build Linting.csproj --no-restore`
3. Verify: `Build succeeded` (not just warnings)
4. If build fails: **REJECT** the changes immediately

### Rule 3: CodeFactor Issue Triage
When reviewing CodeFactor issues, categorize as:

#### SAFE TO FIX (Low Risk):
- Documentation periods (add `.` to XML comments)
- Blank line formatting (add/remove blank lines)
- Closing parenthesis formatting (move `)` to previous line)

#### REJECT (V12 Standards):
- Field underscore prefix (`_fieldName`) - V12 standard for private fields
- `BUILD_TAG` underscore - Constant naming convention
- "Do not use regions" - V12 uses regions for file organization
- "Braces should not be omitted" - V12 style for single-line returns
- Readonly field ordering - Risky, needs dependency analysis

#### DEFER (Architectural):
- Complex method refactoring (requires Phase 7)
- Field ordering changes (risky - initialization dependencies)
- "Field should be private" in DTO classes (incorrect - DTO pattern)

### Rule 4: Incremental Fixes with Build Verification
**Process for manual fixes:**
1. Fix 1-5 related issues in a single file
2. Run build: `dotnet build Linting.csproj --no-restore`
3. Verify: `Build succeeded`
4. Commit with descriptive message
5. Repeat for next batch

**Never fix more than 10 issues without a build verification cycle.**

### Rule 5: Document Rejections
When rejecting CodeFactor issues, document WHY:
- Create `docs/brain/codefactor-rejections.md`
- List each rejected issue with rationale
- Reference V12 standards or architectural constraints
- This prevents future agents from re-attempting the same fixes

## V12-Specific Standards

### Naming Conventions
- **Constants**: `UPPER_SNAKE_CASE` (e.g., `BUILD_TAG`, `CIRCUIT_BREAKER_THRESHOLD`)
- **Private Fields**: `_camelCase` with underscore prefix (e.g., `_accountMailbox`)
- **Properties**: `PascalCase` (e.g., `LastKnownPrice`)

### Code Organization
- **Regions**: REQUIRED for partial class organization (`#region V12 REAPER`)
- **Braces**: Single-line returns allowed for guard clauses
- **DTO Pattern**: Public fields in private DTO classes are CORRECT

### Documentation
- **XML Comments**: Should end with periods (`.`)
- **Partial Classes**: Documented in main file, not in each partial

## Emergency Rollback Procedure

If a commit breaks the build:
```powershell
# 1. Find last working commit
git log --oneline | Select-Object -First 10

# 2. Verify it works
git checkout <good-commit-hash>
dotnet build Linting.csproj --no-restore

# 3. Reset branch
git checkout <branch-name>
git reset --hard <good-commit-hash>

# 4. Force push (if already pushed)
git push origin <branch-name> --force

# 5. Sync NinjaTrader
powershell -File .\deploy-sync.ps1
```

## Success Metrics

### Before This Protocol
- Accepted CodeFactor autofix: 376 "fixes"
- Result: 320 compilation errors
- Recovery time: 30+ minutes
- Commits lost: 2 (d75e5f1, 2eab3f8)

### After This Protocol
- Manual fixes only: <10 issues per batch
- Build verification: After every batch
- Zero compilation errors
- Incremental progress with safety

## Agent Responsibilities

Every agent working on this codebase MUST:
1. Read this protocol before accepting ANY automated fixes
2. Verify builds after EVERY code change
3. Document rejected issues with rationale
4. Use incremental fixes with build verification
5. Never trust external tools without manual review

## Signature
This protocol is MANDATORY for all agents (Bob, Codex, Gemini, Jules, etc.) working on the V12 codebase.

**Effective Date**: 2026-05-22  
**Last Updated**: 2026-05-22  
**Status**: ACTIVE - MANDATORY COMPLIANCE