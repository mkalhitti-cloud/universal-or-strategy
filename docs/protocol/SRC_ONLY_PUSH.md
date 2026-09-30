# Src-Only GitHub Push Protocol (V12.19)

**Effective Date**: 2026-05-25  
**Status**: MANDATORY for all agents

## Executive Summary

Agents MUST push ONLY `src/` files to GitHub for PR review. All non-src changes (documentation, scripts, brain files, configuration) are merged manually by the Director or upon explicit instruction.

## Rationale

### Problem Statement
- PR reviews were consuming 60-80% more time due to documentation/planning noise
- Git history cluttered with non-production changes
- Agents wasting cycles on doc formatting and brain file conflicts
- CodeFactor and CI tools scanning irrelevant files

### Solution Benefits
1. **Faster PR Reviews**: Focus only on production code changes
2. **Cleaner Git History**: Production changes clearly separated from planning artifacts
3. **Reduced Conflicts**: Brain files and docs don't block code merges
4. **Better CI Performance**: Smaller diffs, faster builds, lower token costs

## Mandatory Workflow

### Standard Agent Push (Src-Only)

```powershell
# 1. Verify hygiene (already checks src/ only)
powershell -File .\scripts\verify_pr_hygiene.ps1

# 2. Stage ONLY src/ files
git add src/

# 3. Commit with descriptive message
git commit -m "feat: implement X in src/Y.cs"

# 4. Push for PR review
git push origin <branch-name>
```

### Helper Script (Recommended)

```powershell
# Use the new helper script for automated src-only workflow
powershell -File .\scripts\git_src_only.ps1 -message "feat: your commit message"
```

## File Categories

### ✅ ALWAYS Push (Src-Only)
- `src/**/*.cs` - All C# source files
- `src/**/*.csproj` - Project files (if modified)

### ❌ NEVER Push (Unless Explicitly Instructed)
- `docs/**` - All documentation
- `*.md` - Markdown files (AGENTS.md, README.md, etc.)
- `scripts/**` - PowerShell/Python scripts
- `.bob/**` - Bob CLI configuration
- `.vscode/**` - VS Code settings
- `.github/**` - GitHub workflows (unless explicitly updating CI)
- `docs/brain/**` - Planning and brain files
- `conductor/**` - Conductor tracks
- `benchmarks/**` - Benchmark code (unless part of src/ work)
- `tests/**` - Test files (unless part of src/ work)

## Exception Handling

### When Director Overrides
If the Director explicitly instructs: "Push all changes including docs", then:

```powershell
# Full push workflow
git add .
git commit -m "feat: X + docs update"
git push origin <branch-name>
```

### When Non-Src Changes Are Critical
If a non-src change is blocking src/ work (e.g., CI config fix):
1. **Ask the Director first**: "Should I push the CI config change with src/ changes?"
2. **Wait for explicit approval**
3. **Document in commit message**: "feat: X + CI fix (approved by Director)"

## Integration with Existing Protocols

### PR Hygiene Mandate (00-pr-hygiene.md)
- `verify_pr_hygiene.ps1` already checks `src/` diffs only
- No changes needed - this protocol aligns perfectly

### Deploy-Sync Protocol
- `deploy-sync.ps1` syncs `src/` to NinjaTrader
- Non-src files don't affect deployment
- Continue running after src/ changes

### Graphify Updates
- Run `graphify update .` after structural src/ changes
- Graph updates are local-only (not pushed to GitHub)
- Keeps agent context current without PR noise

## Verification Checklist

Before every push, verify:
- [ ] Only `src/` files staged (`git status` shows only src/ paths)
- [ ] `verify_pr_hygiene.ps1` passed
- [ ] Commit message describes src/ changes only
- [ ] No documentation/brain files in staging area

## Migration Guide for Agents

### Old Workflow (Deprecated)
```powershell
git add .                          # ❌ Adds everything
git commit -m "update"             # ❌ Vague message
git push                           # ❌ Pushes non-src noise
```

### New Workflow (V12.19+)
```powershell
git add src/                       # ✅ Src-only
git commit -m "feat: specific"     # ✅ Clear message
git push origin <branch>           # ✅ Clean PR
```

## Enforcement

### Automated Checks
- `verify_pr_hygiene.ps1` enforces src/ diff limits
- New `git_src_only.ps1` helper automates src-only staging

### Manual Review
- Director reviews PR file list before merge
- PRs with non-src files trigger protocol violation review

### Violation Protocol
If an agent pushes non-src files without approval:
1. PR is flagged for review
2. Agent must explain rationale
3. If unjustified, PR is closed and agent re-submits src-only
4. Repeat violations trigger protocol audit

## FAQ

**Q: What if I need to update AGENTS.md with a new protocol?**  
A: Make the change locally, inform the Director. They will merge it manually when ready.

**Q: What if a script fix is blocking src/ work?**  
A: Ask the Director for explicit approval to include the script in the PR.

**Q: What about test files in `tests/`?**  
A: If tests are part of the src/ feature work, ask the Director. Default: src-only.

**Q: Can I push `.bob/rules/` updates?**  
A: No. Bob rules are protocol-level changes that require Director review.

## Version History

- **V12.19** (2026-05-25): Initial protocol - src-only push mandate