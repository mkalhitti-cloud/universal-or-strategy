# V12 Workflow Integration - Pre-Push Validation

This document describes how the **Pre-Push Validation Suite** (`scripts/pre_push_validation.ps1`) is integrated into ALL V12 workflows to catch issues locally before GitHub push.

## Philosophy: Shift-Left Quality

**Problem**: Waiting 5-10 minutes for GitHub CI/bot checks to fail wastes time and breaks flow.

**Solution**: Run ALL checks locally in ~2 minutes BEFORE pushing. Catch issues immediately.

## Core Integration Points

### 1. Git Pre-Push Hook (Automatic)

**Install Once**:
```powershell
powershell -File .\scripts\install_hooks.ps1
```

**Behavior**:
- Runs automatically on every `git push`
- Blocks push if ANY check fails
- Exit code 1 = blocked, Exit code 0 = allowed

**Bypass** (emergency only):
```bash
git push --no-verify
```

### 2. Bob CLI Commands (Manual)

All Bob commands now include pre-push validation as Step 0:

#### `/pr-loop` - PR Perfection Loop
```markdown
Step 0: Pre-Push Validation (MANDATORY)
  1. Run `powershell -File .\scripts\pre_push_validation.ps1 -Fast`
  2. If ANY check fails: HALT and fix violations
  3. If ALL pass: Proceed to Step 1 (Local Integrity)
```

#### `/epic-run` - Epic Execution
```markdown
Before each ticket commit:
  1. Run `powershell -File .\scripts\pre_push_validation.ps1 -Fast`
  2. Fix any violations before moving to next ticket
```

#### `/extract` - God-Function Extraction
```markdown
Step 4: Post-Edit DNA Audit
  1. Run `powershell -File .\deploy-sync.ps1`
  2. Run `powershell -File .\scripts\pre_push_validation.ps1`
  3. Verify: ASCII, locks, build, tests all pass
```

#### `/ticket` - Single Ticket Workflow
```markdown
Final Step: Pre-Push Gate
  1. Run `powershell -File .\scripts\pre_push_validation.ps1 -Fast`
  2. Only push if all checks pass
```

### 3. VSCode Tasks (IDE Integration)

Add to `.vscode/tasks.json`:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Pre-Push Validation",
      "type": "shell",
      "command": "powershell",
      "args": [
        "-File",
        ".\\scripts\\pre_push_validation.ps1"
      ],
      "problemMatcher": [],
      "group": {
        "kind": "test",
        "isDefault": true
      },
      "presentation": {
        "reveal": "always",
        "panel": "new"
      }
    },
    {
      "label": "Pre-Push Validation (Fast)",
      "type": "shell",
      "command": "powershell",
      "args": [
        "-File",
        ".\\scripts\\pre_push_validation.ps1",
        "-Fast"
      ],
      "problemMatcher": [],
      "group": "test"
    }
  ]
}
```

**Usage**: `Ctrl+Shift+P` → "Run Task" → "Pre-Push Validation"

### 4. GitHub Actions (CI Parity)

The pre-push script mirrors GitHub CI checks:

| Local Check | GitHub CI Equivalent | Time Saved |
|-------------|---------------------|------------|
| ASCII Gate | sentinel-pyramid.yml | 1-2 min |
| Build | compile-ninjatrader.yml | 1-2 min |
| Tests | sentinel-pyramid.yml | 1-2 min |
| Lint | lint.yml | 30-60s |
| Security | gitleaks.yml, snyk | 1-2 min |
| Links | markdown-link-check.yml | 30s |
| **Total** | **~5-10 min** | **~5-10 min** |

## Validation Checks Breakdown

### 1. ASCII Gate (V12 DNA Mandate)
**What**: Scans all `src/**/*.cs` for non-ASCII bytes
**Why**: NinjaTrader compiler crashes on Unicode
**Blocks**: Emoji, curly quotes, accented characters
**Fix**: Replace with ASCII equivalents

### 2. Build Compilation
**What**: Compiles `Linting.csproj` with Roslyn analyzers
**Why**: Catches syntax errors, type errors, analyzer warnings
**Blocks**: Compilation failures, SA/IDE violations
**Fix**: Address compiler errors and warnings

### 3. Unit Tests
**What**: Runs all tests in `Testing.csproj`
**Why**: Prevents logic regressions
**Blocks**: Test failures, assertion errors
**Fix**: Fix broken tests or update test expectations

### 4. Roslyn Linting
**What**: Runs StyleCop, Roslynator, custom analyzers
**Why**: Enforces code style and best practices
**Blocks**: SA1xxx, IDE0xxx violations
**Fix**: Apply suggested fixes or suppress with justification

### 5. CSharpier Formatting
**What**: Checks code formatting (doesn't modify)
**Why**: Ensures consistent style
**Blocks**: Inconsistent indentation, line length
**Fix**: Run `dotnet csharpier .` to auto-format

### 6. Security Scans
**What**: Gitleaks (secrets) + Snyk (vulnerabilities)
**Why**: Prevents credential leaks and CVEs
**Blocks**: Hardcoded API keys, high-severity CVEs
**Fix**: Move secrets to env vars, update dependencies

### 7. Markdown Links
**What**: Validates all markdown links
**Why**: Prevents documentation rot
**Blocks**: Broken internal/external links
**Fix**: Update or remove broken links

### 8. PR Hygiene
**What**: Checks diff size (<10k chars) and branch state
**Why**: Keeps PRs reviewable
**Blocks**: Oversized diffs, dirty branches
**Fix**: Split into smaller PRs or clean branch

### 9. Complexity Audit (Optional)
**What**: Scans for high cyclomatic complexity (>15)
**Why**: Identifies god-functions
**Blocks**: None (informational)
**Fix**: Extract complex methods

### 10. Dead Code Scan (Optional)
**What**: Identifies unreferenced code
**Why**: Reduces maintenance burden
**Blocks**: None (informational)
**Fix**: Remove dead code

## Workflow-Specific Integration

### PR Loop (`/pr-loop`)

```markdown
## THE PERFECTION CYCLE

### Step 0: Pre-Push Validation (MANDATORY)
**Switch to: Advanced mode**
Hand off:
```
TASK: Pre-Push Validation Suite
PROTOCOL:
  1. Run `powershell -File .\scripts\pre_push_validation.ps1 -Fast`.
  2. This checks: ASCII, Build, Tests, Lint, Formatting, Security, Links, PR Hygiene.
  3. If ANY check fails: HALT and report the specific violation.
  4. If ALL checks pass: Advance to Step 1.
  
NOTE: Use -Fast to skip slow checks (complexity audit, dead code scan).
Full validation: `powershell -File .\scripts\pre_push_validation.ps1`
```

### Step 1: Local Integrity (Goal: 15/15)
... (continues with existing steps)
```

### Epic Run (`/epic-run`)

```markdown
## TICKET EXECUTION LOOP

For each ticket in the epic:

### Pre-Commit Gate
1. Run `powershell -File .\scripts\pre_push_validation.ps1 -Fast`
2. Fix any violations before committing
3. Only proceed if all checks pass

### Commit & Push
1. `git add .`
2. `git commit -m "feat(epic-X): ticket description"`
3. `git push` (pre-push hook runs validation again)
```

### Extract (`/extract`)

```markdown
## STEP 4 -- POST-EDIT DNA AUDIT (mandatory)

```powershell
# 4a: Re-establish hard links
powershell -File .\deploy-sync.ps1

# 4b: Run full pre-push validation
powershell -File .\scripts\pre_push_validation.ps1

# 4c: Verify specific DNA mandates
grep -r "lock(" src/
grep -Prn "[^\x00-\x7F]" src/
```

Report to Director:
```
[EXTRACT-AUDIT]
Pre-Push Validation: PASS / FAIL
  - ASCII Gate: PASS / FAIL
  - Build: PASS / FAIL
  - Tests: PASS / FAIL
  - Lint: PASS / FAIL
  - Security: PASS / FAIL
deploy-sync.ps1: PASS / FAIL
lock() audit: CLEAN / [N matches]
Unicode audit: CLEAN / [N matches]
```
```

### Ticket (`/ticket`)

```markdown
## FINAL STEP: VALIDATION & PUSH

### Pre-Push Gate
1. Run `powershell -File .\scripts\pre_push_validation.ps1 -Fast`
2. Fix any violations
3. Only push if all checks pass

### Push
1. `git push` (pre-push hook validates again)
2. Monitor PR checks
3. Address any bot findings
```

## Tool Integration Matrix

| Tool | Local Check | GitHub Bot | Pre-Push Script |
|------|-------------|------------|-----------------|
| **Bob CLI** | Roslyn analyzers | N/A | ✅ Build step |
| **CodeRabbit** | Markdown lint | PR comments | ✅ Links step |
| **Greptile** | Complexity audit | N/A | ✅ Optional step |
| **Snyk** | Vuln scan | PR check | ✅ Security step |
| **Gitleaks** | Secret scan | PR check | ✅ Security step |
| **Kilo AI** | N/A | PR review | ❌ No local equivalent |
| **Cubic AI** | N/A | PR review | ❌ No local equivalent |
| **DeepSource** | Roslyn analyzers | PR check | ✅ Build step |
| **Codacy** | Roslyn analyzers | PR check | ✅ Build step |
| **SonarCloud** | Roslyn analyzers | PR check | ✅ Build step |

**Note**: Kilo AI and Cubic AI are LLM-based reviewers with no local equivalent. All other tools have local parity.

## Performance Optimization

### Fast Mode (Recommended for Iterations)
```powershell
powershell -File .\scripts\pre_push_validation.ps1 -Fast
```
- **Time**: ~30 seconds
- **Skips**: Complexity audit, dead code scan
- **Use**: During PR loop iterations

### Full Mode (Recommended for Final Push)
```powershell
powershell -File .\scripts\pre_push_validation.ps1
```
- **Time**: ~2 minutes
- **Includes**: All checks
- **Use**: Before merging to main

### Selective Skipping
```powershell
# Skip build (if you just compiled)
powershell -File .\scripts\pre_push_validation.ps1 -SkipBuild

# Skip tests (for docs-only changes)
powershell -File .\scripts\pre_push_validation.ps1 -SkipTests

# Skip lint (for non-code changes)
powershell -File .\scripts\pre_push_validation.ps1 -SkipLint
```

## Troubleshooting

### "CSharpier not installed"
```bash
dotnet tool install -g csharpier
```

### "Gitleaks not installed"
```bash
choco install gitleaks
# Or download from: https://github.com/gitleaks/gitleaks/releases
```

### "Snyk not installed"
```bash
npm install -g snyk
snyk auth
```

### "Python not found"
Install Python 3.12+ from python.org

### Pre-Push Hook Not Running
```bash
# Reinstall hooks
powershell -File .\scripts\install_hooks.ps1

# Verify hook exists
cat .git/hooks/pre-push
```

## Migration Guide

### For Existing Workflows

1. **Update Bob Commands**: Add Step 0 (Pre-Push Validation) to all workflow commands
2. **Install Git Hook**: Run `powershell -File .\scripts\install_hooks.ps1`
3. **Update VSCode Tasks**: Add pre-push validation tasks to `.vscode/tasks.json`
4. **Train Team**: Share this document with all contributors

### For New Contributors

1. Clone repo
2. Run `powershell -File .\scripts\install_hooks.ps1`
3. Install optional tools (CSharpier, Gitleaks, Snyk)
4. Read this document
5. Run first validation: `powershell -File .\scripts\pre_push_validation.ps1`

## Success Metrics

**Before Pre-Push Integration**:
- Average PR iteration time: 10-15 minutes (wait for CI)
- Failed pushes per PR: 3-5
- Total wasted time per PR: 30-75 minutes

**After Pre-Push Integration**:
- Average PR iteration time: 2-3 minutes (local validation)
- Failed pushes per PR: 0-1
- Total wasted time per PR: 0-3 minutes

**Time Savings**: ~30-70 minutes per PR

## Related Documentation

- [`/pre-push` Command Reference](.bob/commands/pre-push.md)
- [`/pr-loop` Command Reference](.bob/commands/pr-loop.md)
- [V12 DNA Compliance](../AGENTS.md)
- [Infrastructure Protocol](../INFRASTRUCTURE_PROTOCOL.md)