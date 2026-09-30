# Semgrep Setup & Integration Guide

## Overview

Semgrep is integrated into the V12 workflow to enforce DNA principles **before** code reaches GitHub. This prevents P0 violations from ever entering PRs, reducing bot noise and CI wait times.

## Status

**Current State**: Semgrep GitHub App is **NOT installed** on this repository (verified via PR #6 forensics).

**Integration Level**: Local CLI scans only (pre-push validation)

**Future**: GitHub App installation for PR comments and autofix

## Quick Start

### 1. Install Semgrep CLI

**Option A: Python pip (Recommended)**
```bash
pip install semgrep
```

**Option B: Homebrew (macOS)**
```bash
brew install semgrep
```

**Option C: npm**
```bash
npm install -g @semgrep/cli
```

**Verify Installation:**
```bash
semgrep --version
# Expected: semgrep 1.x.x
```

### 2. Run Local Scan

```powershell
# Full scan (all severity levels)
powershell -File .\scripts\run_semgrep.ps1

# Only show ERROR findings (P0 blocking issues)
powershell -File .\scripts\run_semgrep.ps1 -Severity ERROR

# Dry run (show what would be scanned)
powershell -File .\scripts\run_semgrep.ps1 -DryRun

# JSON output for CI integration
powershell -File .\scripts\run_semgrep.ps1 -OutputJson
```

### 3. Integrate into Pre-Push Workflow

Semgrep is **automatically included** in the pre-push validation script:

```powershell
powershell -File .\scripts\pre_push_validation.ps1
```

This runs Semgrep alongside:
- ASCII Gate
- Build Compilation
- Unit Tests
- Roslyn Linting
- Security Scans (Gitleaks, Snyk)

## V12 DNA Rules

The `.semgrep.yml` configuration enforces these V12 DNA principles:

### P0 - CRITICAL (Blocking)

| Rule ID | Description | Fix |
|---------|-------------|-----|
| `v12-ban-lock-statement` | No `lock()` usage | Use `Interlocked` or Actor model |
| `v12-atomic-on-local-variable` | CAS on local variable | Move to class field |
| `v12-unicode-in-string-literals` | Non-ASCII characters | Replace with ASCII equivalents |
| `v12-task-waitall-blocking` | Blocking async calls | Use `await Task.WhenAll()` |
| `v12-hardcoded-credentials` | Hardcoded secrets | Use environment variables |
| `v12-sql-injection-risk` | SQL injection | Use parameterized queries |

### P1 - HIGH (Must Fix)

| Rule ID | Description | Fix |
|---------|-------------|-----|
| `v12-struct-copy-mutation` | Mutating struct copy | Use `ref` parameters |
| `v12-linq-in-hot-path` | LINQ in `OnBarUpdate` | Use for loops |
| `v12-missing-v12-prefix` | Missing V12_001/V12_002 prefix | Rename class |
| `v12-threadstatic-without-initialization` | ThreadStatic without init | Add null-coalescing |

### P2 - MEDIUM (Should Fix)

| Rule ID | Description | Fix |
|---------|-------------|-----|
| `v12-public-mutable-fields` | Public mutable fields | Encapsulate as properties |
| `v12-string-concat-in-loop` | String concat in loop | Use `StringBuilder` |
| `v12-exception-for-control-flow` | Exceptions for control flow | Use return codes |
| `v12-print-in-hot-path` | `Print()` in hot path | Remove or use conditional logging |
| `v12-currentbar-without-barsrequired-check` | Missing `BarsRequiredToTrade` check | Add guard clause |

## Integration Points

### 1. Pre-Push Validation (Local)

**File**: `scripts/pre_push_validation.ps1`

Semgrep runs as **Step 6** in the pre-push workflow:

```
1. ASCII Gate
2. Build Compilation
3. Unit Tests
4. Roslyn Linting
5. CSharpier Formatting
6. Semgrep Scan ← NEW
7. Gitleaks (Secrets)
8. Snyk (Dependencies)
9. Markdown Links
10. PR Hygiene
```

**Exit Behavior**:
- P0 findings (ERROR) → **BLOCK push** (exit code 1)
- P1/P2 findings (WARNING/INFO) → **WARN but allow** (exit code 0)

### 2. PR Loop V2 (GitHub)

**File**: `docs/protocol/PR_LOOP_V2.md`

Semgrep integrates into the PR perfection loop:

```
Step 0: Pre-Flight Hygiene
  ├─ Rebase on origin/main
  └─ Run pre-push validation (includes Semgrep)

Step 1: CodeRabbit Pre-Review
  └─ AI review with V12 DNA checks

Step 2: Bot Forensics
  ├─ Extract Semgrep findings (when GitHub App installed)
  └─ Categorize: VALID / HALLUCINATION / INFRA-NOISE

Step 3: Local Repair
  └─ Fix P0 → P1 → P2 issues

Step 4: Global Push & Monitor
  └─ Re-run Semgrep in CI (when GitHub App installed)
```

### 3. GitHub Actions (Future)

**File**: `.github/workflows/semgrep.yml` (to be created)

When Semgrep GitHub App is installed:

```yaml
name: Semgrep
on:
  pull_request:
    branches: [main, develop]
  push:
    branches: [main]

jobs:
  semgrep:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: semgrep/semgrep-action@v1
        with:
          config: .semgrep.yml
```

## GitHub App Installation (Future)

### Prerequisites

1. Repository admin access
2. Semgrep account (free tier available)
3. GitHub App permissions approved

### Installation Steps

1. **Navigate to Semgrep Dashboard**
   - Go to: https://semgrep.dev/orgs/-/settings/integrations
   - Click "Add GitHub Integration"

2. **Authorize GitHub App**
   - Select `mdasdispatch-hash/universal-or-strategy`
   - Grant permissions:
     - Read: Repository contents, Pull requests
     - Write: Pull request comments, Checks

3. **Configure Scan Settings**
   - Enable: Code, Secrets, Supply Chain
   - Set blocking rules: P0 (ERROR) only
   - Enable autofix: Yes (for safe fixes)

4. **Test on PR**
   - Create test PR with intentional violation
   - Verify Semgrep posts comment
   - Test triage commands: `/fp`, `/ar`, `/open`

### Expected Behavior

Once installed, Semgrep will:

1. **Post PR Comments** with findings
2. **Provide Autofix** for safe violations
3. **Support Triage Commands**:
   - `/fp` - Mark as false positive
   - `/ar` - Mark as acceptable risk
   - `/other` - Other reason to ignore
   - `/open` - Reopen finding

4. **Block Merge** if P0 findings exist (configurable)

## Triage Workflow

### Local Findings (Pre-Push)

```powershell
# Run scan
.\scripts\run_semgrep.ps1

# Review findings
# Fix P0 issues immediately
# Document P1/P2 issues for later

# Re-run to verify
.\scripts\run_semgrep.ps1 -Severity ERROR
```

### GitHub PR Findings (Future)

```
1. Semgrep posts comment on PR
2. Review finding details
3. Choose action:
   a. Fix the code → Push commit
   b. False positive → Reply `/fp <reason>`
   c. Acceptable risk → Reply `/ar <reason>`
   d. Defer → Create follow-up ticket
4. Semgrep updates finding status
```

## Customizing Rules

### Adding New Rules

Edit `.semgrep.yml`:

```yaml
rules:
  - id: my-custom-rule
    patterns:
      - pattern: MyBadPattern(...)
    message: |
      Explanation of why this is bad.
      Fix: How to fix it.
    languages: [csharp]
    severity: ERROR
    metadata:
      category: security
      v12_dna: my-principle
      priority: P0
```

### Testing Rules

```bash
# Test on specific file
semgrep --config .semgrep.yml src/V12_002.cs

# Test on specific rule
semgrep --config .semgrep.yml --include v12-ban-lock-statement src/
```

### Rule Development Tips

1. **Start Simple**: Test pattern on single file first
2. **Use Playground**: https://semgrep.dev/playground
3. **Check False Positives**: Run on entire codebase
4. **Document Fixes**: Include clear fix guidance in message
5. **Set Correct Severity**:
   - ERROR = P0 (blocks merge)
   - WARNING = P1 (must fix or justify)
   - INFO = P2 (should fix)

## Performance

### Scan Speed

| Scope | Files | Time | Notes |
|-------|-------|------|-------|
| `src/` only | ~50 | ~10s | Recommended for pre-push |
| Full repo | ~200 | ~30s | Includes tests/benchmarks |
| Single file | 1 | <1s | For rapid iteration |

### Optimization Tips

1. **Scope Scans**: Only scan `src/` in pre-push
2. **Cache Results**: Semgrep caches unchanged files
3. **Parallel Execution**: Use `--jobs` flag for large repos
4. **Skip Tests**: Tests have different standards

## Troubleshooting

### "Semgrep not found"

```bash
# Check installation
which semgrep

# Reinstall
pip install --upgrade semgrep
```

### "No findings but violations exist"

```bash
# Verify config syntax
semgrep --validate --config .semgrep.yml

# Test specific rule
semgrep --config .semgrep.yml --include v12-ban-lock-statement src/
```

### "Too many false positives"

1. Review rule patterns in `.semgrep.yml`
2. Add `pattern-not` to exclude false positives
3. Adjust confidence level in metadata
4. Document in `docs/brain/bot_hallucinations.md`

### "Scan too slow"

```bash
# Use fast mode (skip slow rules)
semgrep --config .semgrep.yml --fast src/

# Scan specific paths only
semgrep --config .semgrep.yml src/V12_002.*.cs
```

## Comparison to Other Tools

| Tool | Purpose | Overlap with Semgrep | Keep? |
|------|---------|---------------------|-------|
| **CodeRabbit** | AI review | V12 DNA checks | ✅ Yes (complementary) |
| **Roslyn** | C# linting | Style, syntax | ✅ Yes (different focus) |
| **Codacy** | Static analysis | Code quality | ⚠️ Partial (Semgrep preferred for V12 DNA) |
| **Gitleaks** | Secret detection | Hardcoded credentials | ✅ Yes (specialized) |
| **Snyk** | Dependency scan | Vulnerable packages | ✅ Yes (specialized) |

**Recommendation**: Keep Semgrep + CodeRabbit + Roslyn as primary. Others provide supplementary signals.

## Success Metrics

Track these KPIs:

- **P0 Bugs Caught Pre-Push**: Target >90%
- **False Positive Rate**: Target <10%
- **Scan Time**: Target <15s for `src/`
- **Developer Satisfaction**: Survey quarterly

## Related Documentation

- **PR Loop V2**: `docs/protocol/PR_LOOP_V2.md`
- **Pre-Push Validation**: `.bob/commands/pre-push.md`
- **V12 DNA Principles**: `AGENTS.md`
- **CodeRabbit Integration**: `docs/protocol/PR_LOOP_V2.md#coderabbit-integration-details`

## Support

- **Semgrep Docs**: https://semgrep.dev/docs/
- **Rule Writing**: https://semgrep.dev/docs/writing-rules/overview/
- **Playground**: https://semgrep.dev/playground
- **Community**: https://go.semgrep.dev/slack