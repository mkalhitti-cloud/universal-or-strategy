# Src-Only Protocol Enforcement Fix

**Date**: 2026-05-26
**Issue**: Agents still trying to commit/push non-src files despite V12.19 protocol
**Root Cause**: Conflicting instructions across multiple protocol documents

## Problem Analysis

### The Contradiction

Two protocols are giving **opposite instructions**:

1. **`docs/protocol/SRC_ONLY_PUSH.md` (V12.19)**:
   - ✅ Says: "Push ONLY `src/` files to GitHub"
   - ✅ Says: "Non-src changes merged manually by Director"
   - ✅ Clear: `git add src/` only

2. **`docs/protocol/PR_SEPARATION_ENFORCEMENT.md` (V2.0)**:
   - ❌ Says: "non-src changes MUST be committed directly to main"
   - ❌ Instructs agents to commit non-src files to main branch
   - ❌ Creates confusion about what to do with non-src files

### The Smoking Gun

**`.bob/commands/pr-loop.md:144`**:
```powershell
git add . && git commit -m "fix: PHS Perfection Loop - PR #$1" && git push
```

This is the **PRIMARY VIOLATION**. The PR Loop command uses `git add .` which stages ALL files, not just `src/`.

### Why Agents Are Confused

Agents see these conflicting instructions:
1. "Only push src/ files" (SRC_ONLY_PUSH.md)
2. "Commit non-src files to main" (PR_SEPARATION_ENFORCEMENT.md)
3. "Use `git add .`" (pr-loop.md)

Result: Agents try to handle non-src files instead of ignoring them.

## The Solution

### Core Principle

**Agents should IGNORE non-src files completely when working on src/ tasks.**

- ✅ Stage only `src/` files: `git add src/`
- ✅ Let non-src changes sit unstaged
- ✅ Director handles non-src files manually when ready
- ❌ Never commit non-src files to any branch
- ❌ Never worry about non-src files during PR work

### Files to Fix

#### 1. `.bob/commands/pr-loop.md:144`
**Current (WRONG)**:
```powershell
git add . && git commit -m "fix: PHS Perfection Loop - PR #$1" && git push
```

**Fixed (CORRECT)**:
```powershell
git add src/ && git commit -m "fix: PHS Perfection Loop - PR #$1" && git push
```

#### 2. `docs/protocol/PR_SEPARATION_ENFORCEMENT.md`
**Action**: DEPRECATE this entire file. It contradicts SRC_ONLY_PUSH.md.

**Rationale**: 
- The "commit non-src to main" workflow is confusing
- Agents should never touch non-src files during src/ work
- Director handles non-src files manually

**Replacement**: Add deprecation notice at top:
```markdown
# ⚠️ DEPRECATED - DO NOT USE

**Status**: DEPRECATED as of V12.19
**Superseded By**: `docs/protocol/SRC_ONLY_PUSH.md`

This protocol is no longer valid. Agents MUST NOT commit non-src files to any branch.
See `docs/protocol/SRC_ONLY_PUSH.md` for current workflow.
```

#### 3. `docs/protocol/PR_LOOP_V2.md:168`
**Current (WRONG)**:
```powershell
2. `git add . && git commit -m "fix: PHS Perfection Loop - PR #<N>" && git push`
```

**Fixed (CORRECT)**:
```powershell
2. `git add src/ && git commit -m "fix: PHS Perfection Loop - PR #<N>" && git push`
```

#### 4. `docs/WORKFLOW_INTEGRATION.md:223`
**Current (WRONG)**:
```bash
1. `git add .`
```

**Fixed (CORRECT)**:
```bash
1. `git add src/`
```

## Implementation Plan

### Phase 1: Fix Core Commands (CRITICAL)
1. Fix `.bob/commands/pr-loop.md:144` - Change `git add .` to `git add src/`
2. Fix `docs/protocol/PR_LOOP_V2.md:168` - Change `git add .` to `git add src/`
3. Fix `docs/WORKFLOW_INTEGRATION.md:223` - Change `git add .` to `git add src/`

### Phase 2: Deprecate Conflicting Protocol
1. Add deprecation notice to `docs/protocol/PR_SEPARATION_ENFORCEMENT.md`
2. Update all references to point to `SRC_ONLY_PUSH.md` instead

### Phase 3: Verify Agent Understanding
1. Update `AGENTS.md` to emphasize: "IGNORE non-src files during src/ work"
2. Add explicit rule: "Never commit non-src files to any branch"
3. Clarify: "Director handles non-src files manually"

## Updated Agent Rules

Add to `AGENTS.md` Section 2 (Architectural Mandates):

```markdown
### Src-Only Push Protocol (V12.19)
- **ALWAYS**: Stage only `src/` files with `git add src/`
- **NEVER**: Use `git add .` or `git add -A` during src/ work
- **IGNORE**: Let non-src changes sit unstaged - Director handles them
- **RATIONALE**: Token conservation, focused PR reviews, cleaner git history
```

## Verification Checklist

After fixes are applied:
- [ ] `.bob/commands/pr-loop.md` uses `git add src/`
- [ ] `docs/protocol/PR_LOOP_V2.md` uses `git add src/`
- [ ] `docs/WORKFLOW_INTEGRATION.md` uses `git add src/`
- [ ] `PR_SEPARATION_ENFORCEMENT.md` marked as DEPRECATED
- [ ] `AGENTS.md` has clear "IGNORE non-src files" rule
- [ ] Test: Run `/pr-loop` command and verify only src/ files are staged

## Expected Behavior After Fix

**Scenario**: Agent working on src/ task with unstaged docs changes

**Before Fix (WRONG)**:
```
Agent: "I see unstaged docs files. Should I commit them to main?"
Result: Confusion, wasted tokens, mixed commits
```

**After Fix (CORRECT)**:
```
Agent: "Staging only src/ files. Ignoring unstaged docs."
Result: Clean src-only PR, no confusion
```

## Migration Notes

### For Existing PRs
- If a PR has mixed src/ and non-src files:
  1. Close the PR
  2. Create new branch with only src/ changes
  3. Director manually commits non-src changes to main when ready

### For Agents
- **Old habit**: "I need to handle all changed files"
- **New habit**: "I only care about src/ files. Everything else is Director's job."

## Success Criteria

✅ Agents never ask about non-src files during src/ work
✅ All PR Loop iterations use `git add src/` only
✅ No mixed src/non-src commits in git history
✅ Token costs reduced (no bot reviews on docs)
✅ PR reviews faster (src-only focus)

## Related Issues

- PR #5: 3354 files pushed (caused by `git add .` violation)
- PR #6: Clean src-only push (correct behavior)

## Version History

- **V12.19** (2026-05-25): Src-Only Push Protocol introduced
- **V12.19.1** (2026-05-26): Fixed conflicting instructions (this document)