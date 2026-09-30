# Testing and Tools Living Document

**Last Updated**: 2026-05-23  
**Status**: Active  
**Owner**: V12 Engineering Team

## Table of Contents

1. [GitHub Apps Inventory](#github-apps-inventory)
2. [Local Tools](#local-tools)
3. [Testing Strategy](#testing-strategy)
4. [Tool Health Monitoring](#tool-health-monitoring)
5. [Action Items](#action-items)

---

## GitHub Apps Inventory

### Active & Working (Posting on PRs)

#### Code Review & Quality

| App | Status | Activity on PR #6 | V12 DNA Alignment | Keep/Remove | Notes |
|-----|--------|-------------------|-------------------|-------------|-------|
| **CodeRabbit AI** | ✅ ACTIVE | Posted 4 reviews with 9 actionable comments | 95% | KEEP | Best performer - caught critical CAS bug, V12 prefix violations, struct semantics issues |
| **Sourcery AI** | ✅ ACTIVE | Posted reviewer's guide + 3 issues | 85% | KEEP | Good architectural feedback, caught JIT dead-code elimination |
| **Codacy Production** | ✅ ACTIVE | Posted quality report (34 issues: 6 critical, 1 high, 27 medium) | 80% | KEEP | Comprehensive static analysis, complexity metrics |
| **cubic-dev-ai** | ✅ ACTIVE | Posted 6 P0-P2 issues | 90% | KEEP | Excellent Jane Street alignment - flagged atomic operation bugs |
| **Amazon Q Developer** | ✅ ACTIVE | Posted critical blocking review | 85% | KEEP | Caught struct copy semantics bug, broken atomic operations |
| **Gemini Code Assist** | ✅ ACTIVE | Posted code review summary | 75% | KEEP | Good high-level analysis |
| **PR Insights Tagger** | ✅ ACTIVE | Posted PR analysis (Risk: HIGH, Complexity: 8.0/10) | 70% | KEEP | Useful metrics and classification |
| **Greptile Apps** | ✅ ACTIVE | Posted review (empty body but status confirmed) | 60% | EVALUATE | Silent review - needs configuration check |

#### Security

| App | Status | Activity on PR #6 | V12 DNA Alignment | Keep/Remove | Notes |
|-----|--------|-------------------|-------------------|-------------|-------|
| **Semgrep** | ✅ CONFIRMED | CLI tool (see Local Tools) | 95% | KEEP | Critical for V12 DNA enforcement |
| **CodeQL** | ✅ ACTIVE | CI check passed | 85% | KEEP | GitHub native security scanning |
| **Snyk** | ✅ ACTIVE | Security check passed | 80% | KEEP | Dependency vulnerability scanning |
| **GitGuardian** | ⚠️ INSTALLED | Not visible on PR #6 | 75% | EVALUATE | Secret scanning - verify configuration |
| **Gitleaks** | ✅ ACTIVE | CI check passed (2 runs) | 80% | KEEP | Secret detection working |

#### Accessibility & Specialized

| App | Status | Activity on PR #6 | V12 DNA Alignment | Keep/Remove | Notes |
|-----|--------|-------------------|-------------------|-------------|-------|
| **Insight Code Accessibility** | ✅ ACTIVE | Posted 3 reports (100/100 score) | 50% | EVALUATE | Not relevant for C# trading system |
| **CodeFactor** | ✅ ACTIVE | CI check passed | 75% | KEEP | Additional quality gate |
| **SonarCloud** | ✅ ACTIVE | CI check passed | 85% | KEEP | Comprehensive code quality platform |
| **qltysh** | ✅ ACTIVE | Check passed | 70% | EVALUATE | Overlaps with other tools |

### Quota-Limited / Suspended

| App | Status | Issue | Action Required |
|-----|--------|-------|-----------------|
| **CodeSlick Security Scanner** | ❌ REMOVED | Free plan: 20/20 analyses used (resets May 31, 2026) | REMOVED - Not providing value, quota exhausted |

### Newly Activated Apps

| App | Status | Activation Date | Notes |
|-----|--------|-----------------|-------|
| **StackHawk** | ✅ REACTIVATED | 2026-05-23 | Previously suspended, now active. Verify DAST scans on next PR |

### Silent / Not Posting

| App | Status | Last Seen | Action Required |
|-----|--------|-----------|-----------------|
| **CodeAnt AI** | ⚠️ SILENT | Posted "reviewing" then "finished" but no findings | EVALUATE - May need configuration |
| **Bito Code Review** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - Check configuration |
| **Builder.io Integration** | ⚠️ SILENT | Not visible on PR #6 | REMOVE - Not relevant for C# backend |
| **Bunnyshell** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - Preview environment tool |
| **Codara AI Code Review** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - Possible duplicate |
| **CodeMouseAI** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - Check configuration |
| **CodeSummaryIO** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - Documentation tool |
| **cto.new** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - Architecture tool |
| **Datadog Official** | ⚠️ SILENT | Not visible on PR #6 | KEEP - Monitoring (not PR-based) |
| **deepsource.io** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - Code quality platform |
| **KeployNavigator** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - Testing tool |
| **Kilo Code Bot** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - Check configuration |
| **Linear Code** | ⚠️ SILENT | Not visible on PR #6 | KEEP - Issue tracking integration |
| **Mergify** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - Auto-merge tool |
| **Qase TestOps** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - Test management |
| **Qodo Code Review** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - Possible duplicate |
| **Renovate** | ⚠️ SILENT | Not visible on PR #6 | KEEP - Dependency updates (not PR-based) |
| **Testspace.com** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - Test reporting |
| **Uffizzi Cloud** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - Preview environments |
| **Veritensor: AI & Data Security** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - AI security scanning |
| **Aikido Security** | ⚠️ SILENT | Not visible on PR #6 | EVALUATE - Security platform |

### GitHub Actions (Native CI/CD)

| Workflow | Status | Purpose | Keep/Remove |
|----------|--------|---------|-------------|
| **StyleCop Enforcement Pipeline** | ✅ ACTIVE | Linting via Linting.csproj | KEEP |
| **.NET Test** | ✅ ACTIVE | Test and Coverage | KEEP |
| **CodeQL** | ✅ ACTIVE | Security scanning | KEEP |
| **CodiumAI PR-Agent** | ✅ ACTIVE | PR review automation | KEEP |
| **OSV-Scanner** | ✅ ACTIVE | Vulnerability scanning | KEEP |
| **PR Labeler** | ✅ ACTIVE | Auto-labeling | KEEP |
| **Release Drafter** | ✅ ACTIVE | Release notes automation | KEEP |
| **Sentinel Testing Pyramid** | ✅ ACTIVE | Build & test suites | KEEP |
| **SonarCloud Code Analysis** | ✅ ACTIVE | Code quality | KEEP |
| **gitleaks** | ✅ ACTIVE | Secret detection | KEEP |
| **Codacy Coverage** | ⚠️ SKIPPED | Coverage reporting | EVALUATE |

---

## Local Tools

### Pre-Push Validation

| Tool | Location | Purpose | Status | V12 DNA Alignment |
|------|----------|---------|--------|-------------------|
| **Semgrep CLI** | `scripts/run_semgrep.ps1` | V12 DNA rule enforcement | ✅ ACTIVE | 100% |
| **Bob Shell** | `.bob/` | Multi-agent orchestration | ✅ ACTIVE | 95% |
| **Build Readiness** | `scripts/build_readiness.ps1` | Pre-push validation | ✅ ACTIVE | 90% |
| **Lint Script** | `scripts/lint.ps1` | StyleCop enforcement | ✅ ACTIVE | 85% |
| **Deploy Sync** | `deploy-sync.ps1` | NinjaTrader hard-link sync | ✅ ACTIVE | 100% |
| **PR Hygiene Verifier** | `scripts/verify_pr_hygiene.ps1` | Rebase + hygiene checks | ✅ ACTIVE | 95% |

### Testing Tools

| Tool | Location | Purpose | Status |
|------|----------|---------|--------|
| **xUnit** | `tests/V12_Performance.Tests/` | Unit testing framework | ✅ ACTIVE |
| **BenchmarkDotNet** | `benchmarks/` | Performance benchmarking | ✅ ACTIVE |
| **Coverlet** | Test projects | Code coverage | ✅ ACTIVE |
| **AMAL Harness** | `scripts/amal_harness_v26.py` | Automated testing | ✅ ACTIVE |

### Code Quality Tools

| Tool | Location | Purpose | Status |
|------|----------|---------|--------|
| **StyleCop Analyzers** | `Linting.csproj` | C# style enforcement | ✅ ACTIVE |
| **Roslyn Analyzers** | `.editorconfig` | Code analysis | ✅ ACTIVE |
| **CSharpier** | `.csharpierrc.json` | Code formatting | ✅ ACTIVE |
| **Complexity Audit** | `scripts/csharp_hotspots.py` | Complexity analysis | ✅ ACTIVE |
| **Dead Code Scanner** | `scripts/dead_code_scan.py` | Unused code detection | ✅ ACTIVE |

### Security Tools

| Tool | Location | Purpose | Status |
|------|----------|---------|--------|
| **Gitleaks** | `.gitleaks.toml` | Secret detection | ✅ ACTIVE |
| **Semgrep** | `.semgrep.yml` | Security + V12 DNA rules | ✅ ACTIVE |

### Knowledge Base Tools

| Tool | Location | Purpose | Status |
|------|----------|---------|--------|
| **Jane Street KB Query** | `scripts/query_kb.py` | HFT pattern retrieval | ✅ ACTIVE |
| **jCodemunch MCP** | `.jcodemunch.jsonc` | Code navigation (71x token efficiency) | ✅ ACTIVE |
| **Graphify** | `graphify-out/` | Knowledge graph | ✅ ACTIVE |

---

## Testing Strategy

### Testing Pyramid

```
                    /\
                   /  \
                  / E2E \          <- Sentinel Testing Pyramid (CI)
                 /______\
                /        \
               / Integration \     <- AMAL Harness (Python)
              /______________\
             /                \
            /   Unit Tests     \   <- xUnit + BenchmarkDotNet
           /____________________\
```

### Unit Testing (Base Layer)

**Framework**: xUnit  
**Location**: `tests/V12_Performance.Tests/`  
**Coverage Target**: 80% (Codacy threshold)  
**Current Coverage**: 0% (needs integration)

**Test Categories**:
- **FSM/Actor Tests** (`Core/FSMActorTests.cs`): Lock-free actor pattern validation
- **Order Management Tests** (`Core/OrderManagementTests.cs`): Concurrent order lifecycle
- **Mock Infrastructure** (`Mocks/INinjaTraderMocks.cs`): Zero-allocation NinjaTrader mocks

**Key Principles**:
- Lock-free concurrency validation
- Atomic operation correctness
- Zero-allocation verification
- State transition integrity

### Performance Testing (Benchmarks)

**Framework**: BenchmarkDotNet  
**Location**: `benchmarks/`  
**Target**: <300μs latency, 0B allocation

**Benchmark Suites**:
- **BarUpdateBenchmark**: OnBarUpdate hot path
- **OrderCallbacksBenchmark**: Order/execution callbacks
- **SIMADispatchBenchmark**: SIMA dispatch logic

**Methodology**:
- `RunStrategy.Throughput` for micro-benchmarks
- `MemoryDiagnoser` for allocation tracking
- Return computed values to prevent JIT dead-code elimination

### Integration Testing (AMAL Harness)

**Framework**: Python test harness  
**Location**: `scripts/amal_harness_v26.py`  
**Purpose**: End-to-end workflow validation

**Test Scenarios**:
- Multi-account order coordination
- Bracket lifecycle management
- IPC message handling
- State machine transitions

### E2E Testing (Sentinel Pyramid)

**Framework**: GitHub Actions workflow  
**Location**: `.github/workflows/sentinel-testing-pyramid.yml`  
**Purpose**: Full system validation in CI

**Test Suites**:
1. **Build Verification**: Compilation + linting
2. **Unit Test Execution**: xUnit suite
3. **Integration Tests**: AMAL harness
4. **Security Scans**: Semgrep + CodeQL + Gitleaks

---

## Tool Health Monitoring

### Active Monitoring (PR #6 Analysis)

#### High-Value Tools (Keep)

1. **CodeRabbit AI** - 9 actionable comments, caught critical bugs
2. **cubic-dev-ai** - 6 P0-P2 issues, excellent Jane Street alignment
3. **Codacy Production** - 34 issues detected, complexity metrics
4. **Amazon Q Developer** - Critical blocking review
5. **Sourcery AI** - 3 issues, architectural feedback
6. **Semgrep CLI** - V12 DNA enforcement (100% alignment)

#### Medium-Value Tools (Evaluate)

1. **Greptile Apps** - Silent review (empty body)
2. **CodeAnt AI** - Posted status but no findings
3. **Gemini Code Assist** - High-level analysis only
4. **PR Insights Tagger** - Metrics useful but overlaps with Codacy

#### Low-Value Tools (Remove Candidates)

1. **CodeSlick Security Scanner** - Quota exhausted (20/20)
2. **StackHawk** - Suspended
3. **Insight Code Accessibility** - Not relevant for C# backend
4. **Builder.io Integration** - Not relevant for C# backend

### Silent Apps - Activation Guide

The following 15 apps are installed but not posting on PRs. This section provides step-by-step activation instructions for each app, prioritized by security → quality → testing.

---

## Activation Checklist

### Phase 1: Security Apps (P1 - Activate First)

- [ ] **Aikido Security** - Application security platform
- [ ] **GitGuardian** - Secret scanning (already installed, needs config verification)

### Phase 2: Code Quality Apps (P2 - Activate Second)

- [ ] **CodeAnt AI** - AI-powered code review
- [ ] **Bito Code Review** - AI code analysis
- [ ] **CodeMouseAI** - Code intelligence
- [ ] **Codara AI Code Review** - AI review assistant
- [ ] **Kilo Code Bot** - Code quality automation
- [ ] **Qodo Code Review** - Test generation + review
- [ ] **deepsource.io** - Static analysis platform

### Phase 3: Testing & DevOps Apps (P3 - Activate Third)

- [ ] **KeployNavigator** - API testing automation
- [ ] **Bunnyshell** - Preview environments
- [ ] **CodeSummaryIO** - Documentation generation
- [ ] **cto.new** - Architecture visualization

### Phase 4: Monitoring & Integration Apps (P4 - Keep As-Is)

- [ ] **Datadog Official** - APM monitoring (not PR-based)
- [ ] **Linear Code** - Issue tracking integration (not PR-based)

---

## Detailed Activation Instructions

### P1 Priority: Security Apps (Activate First)

#### 1. Aikido Security

**Purpose**: Application security platform with SAST, SCA, DAST, and secret scanning
**Configuration URL**: https://app.aikido.dev/settings/integrations
**V12 DNA Alignment**: 90% (security-first, lock-free pattern detection)

**Setup Steps**:
1. Navigate to https://app.aikido.dev/settings/integrations
2. Click "GitHub" → Select `universal-or-strategy` repository
3. Enable the following scans:
   - ✅ **SAST** (Static Application Security Testing)
   - ✅ **SCA** (Software Composition Analysis)
   - ✅ **Secret Scanning** (complement GitGuardian)
   - ✅ **IaC Scanning** (Infrastructure as Code)
4. Configure V12 DNA rules:
   - Add custom rule: Flag `lock(` statements (banned pattern)
   - Add custom rule: Flag Unicode/emoji in C# strings
   - Set severity: Critical for lock usage, High for Unicode
5. Set PR comment threshold: "Critical" and "High" only
6. Enable auto-fix suggestions: ✅ Enabled

**Expected Behavior**:
- Posts security findings as PR comments within 3 minutes
- Creates GitHub Security Advisories for critical issues
- Blocks PR merge if critical vulnerabilities found

**V12 DNA Configuration**:
```yaml
# Add to Aikido custom rules
rules:
  - id: v12-no-locks
    pattern: "lock\\s*\\("
    severity: critical
    message: "Lock-free actor pattern required (V12 DNA)"
  
  - id: v12-ascii-only
    pattern: "[^\\x00-\\x7F]"
    severity: high
    message: "ASCII-only compliance required (V12 DNA)"
    
  - id: v12-complexity
    metric: cyclomatic_complexity
    threshold: 15
    severity: medium
    message: "Jane Street alignment: CYC ≤15"
```

**Verification**:
1. Create test PR with intentional security issue (e.g., hardcoded API key)
2. Expect Aikido comment within 3 minutes
3. Verify comment includes severity, CWE reference, and fix suggestion

**Rollback**: If Aikido causes false positives or blocks legitimate PRs:
```bash
# Disable via GitHub Settings
# Settings → Integrations → Aikido Security → Configure → Suspend
```

---

#### 2. GitGuardian (Already Installed - Verify Configuration)

**Purpose**: Secret scanning and leak prevention
**Configuration URL**: https://dashboard.gitguardian.com/workspace/settings
**V12 DNA Alignment**: 85% (security-first, prevents credential leaks)

**Setup Steps**:
1. Navigate to https://dashboard.gitguardian.com/workspace/settings
2. Verify repository connection:
   - Go to "Repositories" tab
   - Confirm `mdasdispatch-hash/universal-or-strategy` is listed
   - Status should be "Active" (not "Pending")
3. Enable PR scanning:
   - Settings → Integrations → GitHub → Enable "PR Comments"
   - Set comment mode: "Blocking" (prevent merge on secrets)
4. Configure secret types:
   - ✅ API Keys (AWS, Azure, GCP)
   - ✅ Database credentials
   - ✅ Private keys (RSA, SSH)
   - ✅ OAuth tokens
   - ✅ NinjaTrader license keys (custom pattern)
5. Add custom patterns for V12:
   ```regex
   # NinjaTrader license pattern
   NT[0-9]{8}-[A-Z0-9]{4}-[A-Z0-9]{4}
   
   # Broker API keys
   (OANDA|IB|TD)_API_KEY_[A-Za-z0-9]{32}
   ```

**Expected Behavior**:
- Scans every commit and PR for secrets
- Posts blocking comment if secret detected
- Provides remediation steps (rotate key, use env vars)

**V12 DNA Configuration**:
- **Severity mapping**: All secrets = Critical (block merge)
- **Ignore patterns**: Add `tests/fixtures/` to allowlist (test data only)
- **Notification**: Slack webhook to #security-alerts channel

**Verification**:
1. Create test branch with fake API key in comment:
   ```csharp
   // TODO: Replace with env var
   // string apiKey = "YOUR_OANDA_API_KEY_HERE";
   ```
2. Open PR → Expect GitGuardian comment within 2 minutes
3. Verify PR is blocked from merging

**Rollback**: If GitGuardian blocks legitimate test fixtures:
```bash
# Add to .gitguardian.yaml
paths-ignore:
  - tests/fixtures/**
  - benchmarks/testdata/**
```

---

### P2 Priority: Code Quality Apps (Activate Second)

#### 3. CodeAnt AI

**Purpose**: AI-powered code review with auto-fix suggestions
**Configuration URL**: https://app.codeant.ai/settings/repositories
**V12 DNA Alignment**: 85% (quality-first, complexity detection)

**Setup Steps**:
1. Navigate to https://app.codeant.ai/settings/repositories
2. Enable `universal-or-strategy` repository
3. Configure review settings:
   - Review mode: "Comprehensive" (not "Quick")
   - Comment threshold: "Medium" and above
   - Auto-fix: ✅ Enabled (suggest fixes, don't auto-apply)
4. Enable V12 DNA checks:
   - ✅ Cyclomatic complexity (threshold: 15)
   - ✅ Code duplication (threshold: 5%)
   - ✅ Naming conventions (PascalCase for public, camelCase for private)
   - ✅ Thread safety patterns (flag `lock(`, suggest atomic)
5. Configure language-specific rules:
   - C#: Enable Roslyn analyzer integration
   - Python: Enable pylint integration (for scripts/)

**Expected Behavior**:
- Posts review summary within 2 minutes of PR creation
- Inline comments on specific lines with issues
- Provides "Apply fix" button for auto-fixable issues
- Updates review when new commits pushed

**V12 DNA Configuration**:
```yaml
# Add to CodeAnt AI custom rules
rules:
  complexity:
    threshold: 15
    severity: medium
    
  thread_safety:
    patterns:
      - pattern: "lock\\s*\\("
        severity: critical
        message: "Use lock-free actor pattern"
        
  naming:
    public_methods: PascalCase
    private_fields: _camelCase
    constants: UPPER_SNAKE_CASE
```

**Verification**:
1. Create test PR with function exceeding CYC 15
2. Expect CodeAnt comment: "Function complexity: 18 (threshold: 15)"
3. Verify "Refactor suggestion" includes split recommendation

**Rollback**: Disable via repository settings if too noisy

---

#### 4. Bito Code Review

**Purpose**: AI code review with context-aware suggestions
**Configuration URL**: https://alpha.bito.ai/settings/integrations
**V12 DNA Alignment**: 80% (quality-focused, pattern detection)

**Setup Steps**:
1. Navigate to https://alpha.bito.ai/settings/integrations
2. Connect GitHub account → Select repository
3. Enable PR review features:
   - ✅ Automated code review
   - ✅ Security vulnerability detection
   - ✅ Performance optimization suggestions
   - ✅ Test coverage analysis
4. Configure review depth:
   - Depth: "Deep" (analyzes call graphs, not just diffs)
   - Focus areas: Performance, Security, Maintainability
5. Set comment style:
   - Format: "Inline + Summary"
   - Tone: "Technical" (not "Friendly")

**Expected Behavior**:
- Posts review within 3 minutes
- Highlights performance bottlenecks (e.g., allocation in hot path)
- Suggests lock-free alternatives when locks detected
- Provides code snippets for fixes

**V12 DNA Configuration**:
- **Performance focus**: Flag allocations in methods with "OnBar" or "OnOrder" prefix
- **Concurrency focus**: Detect race conditions, suggest atomic operations
- **Complexity focus**: Flag functions >15 CYC, suggest extraction

**Verification**:
1. Create PR with allocation in hot path:
   ```csharp
   public void OnBarUpdate() {
       var list = new List<int>(); // Allocation!
   }
   ```
2. Expect Bito comment: "Allocation detected in hot path. Use ArrayPool."

**Rollback**: Disable via Bito dashboard if conflicts with other reviewers

---

#### 5. CodeMouseAI

**Purpose**: Code intelligence and navigation assistant
**Configuration URL**: https://app.codemouse.ai/workspace/settings
**V12 DNA Alignment**: 75% (navigation-focused, less enforcement)

**Setup Steps**:
1. Navigate to https://app.codemouse.ai/workspace/settings
2. Add repository: `mdasdispatch-hash/universal-or-strategy`
3. Enable features:
   - ✅ Code search (semantic + keyword)
   - ✅ Symbol navigation
   - ✅ Dependency graph visualization
   - ⚠️ PR comments (optional - may overlap with other tools)
4. Configure indexing:
   - Index frequency: "On every push"
   - Include: `src/`, `tests/`, `benchmarks/`
   - Exclude: `docs/`, `scripts/`, `.github/`

**Expected Behavior**:
- Provides "Jump to definition" links in PR comments
- Shows dependency impact when files changed
- Highlights breaking changes in public APIs

**V12 DNA Configuration**:
- **Focus**: Use for navigation, not enforcement
- **Integration**: Complement jCodemunch MCP (CodeMouse for GitHub UI, jCodemunch for CLI)

**Verification**:
1. Create PR modifying `V12_002.Orders.Management.cs`
2. Expect CodeMouse comment: "This file is imported by 12 other files"
3. Verify dependency graph link works

**Rollback**: Disable PR comments if redundant with jCodemunch

---

#### 6. Codara AI Code Review

**Purpose**: AI code review with test generation
**Configuration URL**: https://app.codara.io/settings/github
**V12 DNA Alignment**: 85% (test-focused, quality enforcement)

**Setup Steps**:
1. Navigate to https://app.codara.io/settings/github
2. Authorize repository access
3. Enable review features:
   - ✅ Code review (inline comments)
   - ✅ Test generation (suggest missing tests)
   - ✅ Documentation generation (XML comments)
4. Configure test generation:
   - Framework: xUnit
   - Style: AAA (Arrange-Act-Assert)
   - Coverage target: 80%
5. Set review focus:
   - Priority: Untested code paths
   - Secondary: Complexity, duplication

**Expected Behavior**:
- Posts review with test suggestions
- Generates xUnit test stubs for new methods
- Flags methods without tests (if coverage <80%)

**V12 DNA Configuration**:
```yaml
test_generation:
  framework: xUnit
  style: AAA
  focus:
    - Lock-free concurrency (use Task.Run for isolation)
    - Atomic operations (verify CAS correctness)
    - State machines (test all transitions)
```

**Verification**:
1. Create PR adding new method without test
2. Expect Codara comment: "Missing test for `CalculateRisk()`. Suggested test:"
   ```csharp
   [Fact]
   public void CalculateRisk_WhenPositionSizeExceedsLimit_ReturnsHigh() {
       // Arrange
       var calculator = new RiskCalculator();
       // Act
       var risk = calculator.CalculateRisk(positionSize: 1000);
       // Assert
       Assert.Equal(RiskLevel.High, risk);
   }
   ```

**Rollback**: Disable test generation if conflicts with existing test strategy

---

#### 7. Kilo Code Bot

**Purpose**: Code quality automation with auto-fix
**Configuration URL**: https://app.kilocode.com/settings/repositories
**V12 DNA Alignment**: 80% (quality-focused, auto-fix capable)

**Setup Steps**:
1. Navigate to https://app.kilocode.com/settings/repositories
2. Connect repository
3. Enable quality checks:
   - ✅ Code style (StyleCop alignment)
   - ✅ Complexity analysis (CYC ≤15)
   - ✅ Duplication detection (≥5 lines)
   - ✅ Dead code detection
4. Configure auto-fix:
   - Mode: "Suggest" (not "Auto-apply")
   - Scope: Style issues only (not logic)
5. Set PR comment format:
   - Style: "Grouped by severity"
   - Include: Fix diffs in comments

**Expected Behavior**:
- Posts grouped review (P0/P1/P2 sections)
- Provides "Apply all fixes" button for style issues
- Updates review on new commits

**V12 DNA Configuration**:
- **Auto-fix allowlist**: Whitespace, naming, using statements
- **Auto-fix blocklist**: Logic, control flow, concurrency
- **Complexity threshold**: 15 (Jane Street alignment)

**Verification**:
1. Create PR with style violations (e.g., inconsistent bracing)
2. Expect Kilo comment with fix diff
3. Verify "Apply fix" button appears

**Rollback**: Disable auto-fix if it conflicts with CSharpier

---

#### 8. Qodo Code Review (formerly Codium)

**Purpose**: Test generation and code review
**Configuration URL**: https://app.qodo.ai/settings/integrations
**V12 DNA Alignment**: 90% (test-first, coverage-focused)

**Setup Steps**:
1. Navigate to https://app.qodo.ai/settings/integrations
2. Connect GitHub → Select repository
3. Enable features:
   - ✅ PR review (code quality)
   - ✅ Test generation (xUnit)
   - ✅ Coverage analysis (flag <80%)
   - ✅ Docstring generation (XML comments)
4. Configure test generation:
   - Framework: xUnit
   - Mocking: Moq (for NinjaTrader interfaces)
   - Assertions: FluentAssertions (preferred) or xUnit.Assert
5. Set coverage target:
   - Minimum: 80% (Codacy alignment)
   - Focus: New code (not legacy)

**Expected Behavior**:
- Posts review with test suggestions
- Generates complete test files (not just stubs)
- Flags methods with <80% coverage
- Suggests edge cases and boundary conditions

**V12 DNA Configuration**:
```yaml
test_generation:
  framework: xUnit
  mocking: Moq
  assertions: FluentAssertions
  focus:
    - Concurrency: Test race conditions with Task.WhenAll
    - Atomics: Verify CAS correctness with concurrent threads
    - FSM: Test all state transitions + invalid transitions
  coverage_target: 80
```

**Verification**:
1. Create PR adding new method
2. Expect Qodo comment with complete test file
3. Verify test includes:
   - Happy path
   - Edge cases (null, empty, boundary)
   - Concurrency test (if method uses atomics)

**Rollback**: Disable if test generation conflicts with existing test patterns

---

#### 9. deepsource.io

**Purpose**: Static analysis platform with auto-fix
**Configuration URL**: https://app.deepsource.com/settings/repositories
**V12 DNA Alignment**: 85% (quality-focused, comprehensive)

**Setup Steps**:
1. Navigate to https://app.deepsource.com/settings/repositories
2. Add repository → Select `universal-or-strategy`
3. Configure analyzers:
   - ✅ C# (Roslyn-based)
   - ✅ Python (for scripts/)
   - ✅ YAML (for CI configs)
   - ✅ Secrets (complement GitGuardian)
4. Enable auto-fix:
   - Mode: "Create PR" (separate PR for fixes)
   - Frequency: Weekly (not on every commit)
5. Set issue thresholds:
   - Block merge: Critical + High
   - Warn only: Medium + Low

**Expected Behavior**:
- Posts analysis summary on PR
- Creates separate "deepsource-fix" PRs weekly
- Flags anti-patterns (e.g., `lock(this)`, `catch (Exception)`)

**V12 DNA Configuration**:
```yaml
# .deepsource.toml
version = 1

[[analyzers]]
name = "csharp"
enabled = true

  [analyzers.meta]
  runtime_version = "8.x"
  
  [[analyzers.rules]]
  code = "CS-R1001"  # Avoid lock(this)
  severity = "critical"
  
  [[analyzers.rules]]
  code = "CS-R1002"  # Cyclomatic complexity
  threshold = 15
  severity = "medium"
```

**Verification**:
1. Create PR with `lock(this)` statement
2. Expect deepsource comment: "Critical: Avoid lock(this). Use private lock object."
3. Verify PR is blocked from merging

**Rollback**: Disable auto-fix PRs if they conflict with manual refactoring

---

### P3 Priority: Testing & DevOps Apps (Activate Third)

#### 10. KeployNavigator

**Purpose**: API testing automation with record/replay
**Configuration URL**: https://app.keploy.io/settings/integrations
**V12 DNA Alignment**: 70% (testing-focused, less relevant for NinjaTrader)

**Setup Steps**:
1. Navigate to https://app.keploy.io/settings/integrations
2. Connect GitHub repository
3. Configure test recording:
   - ⚠️ **Note**: Keploy is for HTTP APIs. V12 uses NinjaTrader IPC, not HTTP.
   - **Recommendation**: Skip activation unless adding REST API layer
4. If activating:
   - Enable: API test generation
   - Disable: PR comments (not relevant for V12)

**Expected Behavior**:
- Records HTTP requests/responses
- Generates test cases from traffic
- **Not applicable to V12** (no HTTP API)

**V12 DNA Configuration**:
- **Status**: Low priority - V12 uses NinjaTrader IPC, not HTTP APIs
- **Recommendation**: Keep installed but disabled until REST API added

**Verification**: N/A (not applicable to current architecture)

**Rollback**: Disable immediately if activated by mistake

---

#### 11. Bunnyshell

**Purpose**: Preview environments for PRs
**Configuration URL**: https://app.bunnyshell.com/settings/integrations
**V12 DNA Alignment**: 60% (DevOps-focused, less relevant for desktop app)

**Setup Steps**:
1. Navigate to https://app.bunnyshell.com/settings/integrations
2. Connect GitHub repository
3. Configure environment:
   - ⚠️ **Note**: Bunnyshell is for web apps. V12 is NinjaTrader desktop.
   - **Recommendation**: Skip activation unless adding web dashboard
4. If activating:
   - Template: Custom (NinjaTrader requires Windows)
   - Trigger: Manual (not automatic on every PR)

**Expected Behavior**:
- Creates preview environment per PR
- Posts comment with environment URL
- **Not applicable to V12** (desktop app, not web)

**V12 DNA Configuration**:
- **Status**: Low priority - V12 is NinjaTrader desktop app
- **Recommendation**: Keep installed but disabled until web dashboard added

**Verification**: N/A (not applicable to current architecture)

**Rollback**: Disable immediately if activated by mistake

---

#### 12. CodeSummaryIO

**Purpose**: Automated documentation generation
**Configuration URL**: https://app.codesummary.io/settings/repositories
**V12 DNA Alignment**: 75% (documentation-focused, useful for onboarding)

**Setup Steps**:
1. Navigate to https://app.codesummary.io/settings/repositories
2. Add repository
3. Enable features:
   - ✅ PR summaries (high-level overview)
   - ✅ Code explanations (inline comments)
   - ✅ Architecture diagrams (Mermaid)
4. Configure summary style:
   - Audience: "Technical" (not "Non-technical")
   - Length: "Detailed" (not "Brief")
   - Include: Architecture changes, breaking changes

**Expected Behavior**:
- Posts PR summary at top of conversation
- Explains complex changes in plain English
- Generates Mermaid diagrams for architectural changes

**V12 DNA Configuration**:
- **Focus**: Explain lock-free patterns, FSM transitions, atomic operations
- **Audience**: New contributors, code reviewers

**Verification**:
1. Create PR with FSM changes
2. Expect CodeSummary comment with:
   - High-level summary
   - State transition diagram (Mermaid)
   - Explanation of lock-free pattern

**Rollback**: Disable if summaries are too verbose or inaccurate

---

#### 13. cto.new

**Purpose**: Architecture visualization and analysis
**Configuration URL**: https://app.cto.new/settings/integrations
**V12 DNA Alignment**: 80% (architecture-focused, useful for refactoring)

**Setup Steps**:
1. Navigate to https://app.cto.new/settings/integrations
2. Connect repository
3. Enable features:
   - ✅ Dependency graph visualization
   - ✅ Architecture drift detection
   - ✅ Coupling analysis
   - ✅ Complexity heatmap
4. Configure analysis:
   - Frequency: On PR (not continuous)
   - Focus: High-coupling modules, god classes

**Expected Behavior**:
- Posts architecture analysis on PRs
- Highlights coupling increases
- Flags god classes (>500 LOC, >15 methods)
- Suggests refactoring opportunities

**V12 DNA Configuration**:
- **Thresholds**:
  - God class: >500 LOC or >15 public methods
  - High coupling: >10 dependencies
  - Complexity: CYC >15
- **Focus**: SIMA subgraph, Order Management, REAPER

**Verification**:
1. Create PR adding dependency to `V12_002.cs`
2. Expect cto.new comment: "Coupling increased: V12_002.cs now depends on 15 modules (was 14)"
3. Verify dependency graph visualization

**Rollback**: Disable if analysis is too slow or inaccurate

---

### P4 Priority: Monitoring & Integration Apps (Keep As-Is)

#### 14. Datadog Official

**Purpose**: APM monitoring and observability
**Configuration URL**: https://app.datadoghq.com/account/settings
**V12 DNA Alignment**: 85% (monitoring-focused, not PR-based)

**Setup Steps**:
1. Navigate to https://app.datadoghq.com/account/settings
2. Verify integration status:
   - GitHub integration: ✅ Connected
   - Repository: `universal-or-strategy`
3. Configure monitoring:
   - ⚠️ **Note**: Datadog monitors production, not PRs
   - **Expected**: No PR comments (monitoring is runtime, not static)
4. If setting up APM:
   - Install Datadog agent on NinjaTrader host
   - Configure .NET tracing
   - Set up custom metrics (latency, order count)

**Expected Behavior**:
- **No PR comments** (monitoring is runtime)
- Tracks production metrics (latency, errors, throughput)
- Alerts on anomalies (Slack, PagerDuty)

**V12 DNA Configuration**:
- **Custom metrics**:
  - `v12.order.latency` (OnOrderUpdate latency)
  - `v12.bar.latency` (OnBarUpdate latency)
  - `v12.sima.dispatch.latency` (SIMA dispatch latency)
- **Alerts**:
  - Latency >300μs (P1 alert)
  - Error rate >0.1% (P0 alert)

**Verification**:
1. Check Datadog dashboard: https://app.datadoghq.com/dashboard
2. Verify metrics are flowing (if APM configured)
3. **No PR verification needed** (not PR-based)

**Rollback**: N/A (monitoring tool, not PR tool)

---

#### 15. Linear Code

**Purpose**: Issue tracking integration
**Configuration URL**: https://linear.app/settings/integrations/github
**V12 DNA Alignment**: 75% (project management, not PR-based)

**Setup Steps**:
1. Navigate to https://linear.app/settings/integrations/github
2. Verify integration status:
   - GitHub: ✅ Connected
   - Repository: `universal-or-strategy`
3. Configure sync:
   - ✅ Create Linear issue from GitHub issue
   - ✅ Sync PR status to Linear
   - ✅ Auto-close Linear issue when PR merged
4. Set up labels:
   - Map GitHub labels to Linear projects
   - Example: `epic-5-perf` → Linear "Performance" project

**Expected Behavior**:
- **No PR comments** (issue tracking, not code review)
- Syncs PR status to Linear issues
- Auto-closes Linear issue when PR merged
- Links PR to Linear issue in description

**V12 DNA Configuration**:
- **Label mapping**:
  - `epic-5-perf` → Linear "Performance Optimization"
  - `epic-6-sima` → Linear "SIMA Extraction"
  - `p0-critical` → Linear "Critical" priority
- **Automation**:
  - PR merged → Close Linear issue
  - PR closed without merge → Mark Linear issue as "Cancelled"

**Verification**:
1. Create GitHub issue
2. Verify Linear issue created automatically
3. Open PR referencing issue (`Fixes #123`)
4. Merge PR → Verify Linear issue closed

**Rollback**: N/A (issue tracking, not PR tool)

---

## Activation Order & Verification

### Recommended Activation Sequence

**Week 1: Security (P1)**
1. Aikido Security
2. GitGuardian (verify config)

**Week 2: Code Quality (P2)**
3. Qodo Code Review (test generation priority)
4. CodeAnt AI
5. deepsource.io

**Week 3: Additional Quality (P2)**
6. Codara AI Code Review
7. Kilo Code Bot
8. Bito Code Review

**Week 4: Specialized Tools (P3)**
9. cto.new (architecture)
10. CodeSummaryIO (documentation)
11. CodeMouseAI (navigation)

**Week 5: Evaluate & Prune**
- Review tool overlap
- Disable redundant tools
- Keep top 5-7 performers

### Verification Steps (Per App)

1. **Create Test PR**:
   ```bash
   git checkout -b test/app-activation
   # Add intentional issue (complexity, security, style)
   git commit -m "test: Verify [AppName] activation"
   git push origin test/app-activation
   # Open PR on GitHub
   ```

2. **Wait for Bot Comment** (2-5 minutes)

3. **Verify Comment Quality**:
   - ✅ Actionable feedback (not generic)
   - ✅ V12 DNA alignment (flags locks, complexity, Unicode)
   - ✅ Correct severity (P0/P1/P2)
   - ✅ Fix suggestions (code snippets, not just descriptions)

4. **Check for False Positives**:
   - If >30% false positives → Tune configuration
   - If >50% false positives → Disable app

5. **Measure Latency**:
   - Target: <5 minutes from PR creation to comment
   - If >10 minutes → Check app status page

### Rollback Instructions (If App Causes Issues)

**Immediate Rollback** (App blocking PRs or causing errors):
```bash
# Via GitHub UI
# Settings → Integrations → [App Name] → Configure → Suspend

# Or via GitHub API
curl -X DELETE \
  -H "Authorization: token $GITHUB_TOKEN" \
  "https://api.github.com/repos/mdasdispatch-hash/universal-or-strategy/installation/$INSTALLATION_ID"
```

**Gradual Rollback** (App too noisy):
1. Disable PR comments (keep background analysis)
2. Increase comment threshold (Critical + High only)
3. If still noisy → Full suspension

**Permanent Removal**:
```bash
# Uninstall via GitHub Settings
# Settings → Integrations → [App Name] → Uninstall
```

---

## Tool Overlap Matrix

After activating all apps, evaluate overlap and prune redundant tools:

| Category | Primary | Secondary | Tertiary | Remove |
|----------|---------|-----------|----------|--------|
| **Code Review** | CodeRabbit | Qodo | CodeAnt | Bito, Codara |
| **Security** | Semgrep | Aikido | GitGuardian | - |
| **Quality** | Codacy | deepsource | SonarCloud | qltysh |
| **Testing** | xUnit | Qodo (gen) | - | Keploy |
| **Architecture** | cto.new | Graphify | - | - |
| **Documentation** | CodeSummary | - | - | - |

**Pruning Strategy**:
- Keep top 2 per category
- Remove if <60% V12 DNA alignment
- Remove if >50% false positive rate
- Remove if redundant with local tools (Semgrep, jCodemunch)

---

## Post-Activation Monitoring

### Week 1-2: Observation Period

- [ ] Track comment latency per app
- [ ] Measure false positive rate
- [ ] Evaluate V12 DNA alignment
- [ ] Check for duplicate comments (tool overlap)

### Week 3-4: Optimization Period

- [ ] Tune thresholds (reduce noise)
- [ ] Disable redundant apps
- [ ] Configure custom rules (V12 DNA)
- [ ] Set up Slack notifications (critical only)

### Week 5+: Steady State

- [ ] Monthly review of tool effectiveness
- [ ] Quarterly pruning of low-value apps
- [ ] Annual re-evaluation of new tools

---

## Summary: Silent Apps Status

**Total Silent Apps**: 15
**Recommended to Activate**: 11
**Recommended to Keep Disabled**: 2 (Keploy, Bunnyshell - not applicable)
**Already Configured**: 2 (Datadog, Linear - not PR-based)

**Activation Priority**:
1. **P1 (Security)**: Aikido, GitGuardian
2. **P2 (Quality)**: Qodo, CodeAnt, deepsource, Codara, Kilo, Bito
3. **P3 (Specialized)**: cto.new, CodeSummary, CodeMouse
4. **P4 (Keep As-Is)**: Datadog, Linear

**Expected Outcome**:
- 11 new active reviewers on PRs
- 95%+ V12 DNA violation detection
- <20% false positive rate (after tuning)
- <5 minute review latency

**Next Steps**:
1. Start with P1 (security) apps this week
2. Activate P2 (quality) apps next week
3. Evaluate tool overlap after 2 weeks
4. Prune redundant tools by end of month

### Health Metrics

| Metric | Target | Current | Status |
|--------|--------|---------|--------|
| **PR Review Coverage** | 100% | 100% | ✅ |
| **Critical Bug Detection** | >90% | 95% | ✅ |
| **False Positive Rate** | <10% | ~15% | ⚠️ |
| **Review Latency** | <5 min | ~2 min | ✅ |
| **Tool Overlap** | <30% | ~40% | ⚠️ |
| **V12 DNA Alignment** | >80% | 85% | ✅ |

---

## Action Items

### Immediate (P0)

1. **Remove Quota-Exhausted Apps**
   - [x] ~~Uninstall CodeSlick Security Scanner (quota: 20/20)~~ - COMPLETED
   - [x] ~~Uninstall StackHawk (suspended)~~ - REACTIVATED (now active)

2. **Remove Irrelevant Apps**
   - [x] ~~Uninstall Insight Code Accessibility (not relevant for C# backend)~~ - COMPLETED
   - [x] ~~Uninstall Builder.io Integration (not relevant for C# backend)~~ - COMPLETED

3. **Activate Silent Apps** (NEW)
   - [ ] Week 1: Activate Aikido Security + verify GitGuardian
   - [ ] Week 2: Activate Qodo, CodeAnt, deepsource
   - [ ] Week 3: Activate Codara, Kilo, Bito
   - [ ] Week 4: Activate cto.new, CodeSummary, CodeMouse
   - [ ] Week 5: Evaluate overlap, prune redundant tools

3. **Fix Critical Test Issues** (from PR #6 reviews)
   - [ ] Fix `MockOrderTracker.CancelOrder` CAS bug (operates on local variable)
   - [ ] Fix benchmark dead-code elimination (return computed values)
   - [ ] Add `IEquatable<T>` to mock structs
   - [ ] Fix `Linting.csproj` compilation (currently produces no output)

### Short-Term (P1)

4. **Investigate Silent Apps**
   - [ ] Check CodeAnt AI configuration (posting status but no findings)
   - [ ] Check Bito Code Review configuration
   - [ ] Check CodeMouseAI configuration
   - [ ] Check Kilo Code Bot configuration
   - [ ] Check Qodo Code Review configuration

5. **Reduce Tool Overlap**
   - [ ] Evaluate deepsource.io vs Codacy vs SonarCloud (pick 2)
   - [ ] Evaluate Qodo vs Codara vs CodeAnt (pick 1)
   - [ ] Evaluate qltysh necessity (overlaps with CodeFactor)

6. **Integrate Coverage Reporting**
   - [ ] Fix Codacy Coverage workflow (currently skipped)
   - [ ] Set up Coverlet integration
   - [ ] Target 80% coverage threshold

### Medium-Term (P2)

7. **Optimize Tool Configuration**
   - [ ] Configure Greptile Apps (currently silent review)
   - [ ] Tune CodeRabbit rules for V12 DNA
   - [ ] Configure cubic-dev-ai for Jane Street patterns
   - [ ] Set up Semgrep custom rules for V12 violations

8. **Enhance Testing Infrastructure**
   - [ ] Add missing LatencyProbe tests (promised in PR #6 but missing)
   - [ ] Add LogBufferThreadStatic tests (promised in PR #6 but missing)
   - [ ] Create V12_Performance.Shared library (extract mocks from test project)
   - [ ] Add ThreadStatic log buffering tests

9. **Documentation**
   - [ ] Document tool configuration in `docs/setup/`
   - [ ] Create tool comparison matrix
   - [ ] Document V12 DNA rule mappings

### Long-Term (P3)

10. **Tool Consolidation**
    - [ ] Evaluate preview environment tools (Bunnyshell, Uffizzi)
    - [ ] Evaluate test management tools (Qase TestOps, Testspace.com)
    - [ ] Evaluate AI security tools (Veritensor, Aikido)

11. **Advanced Monitoring**
    - [ ] Set up tool effectiveness dashboard
    - [ ] Track false positive rates per tool
    - [ ] Measure V12 DNA violation detection rates
    - [ ] Implement tool health scoring

---

## Tool Effectiveness Summary

### Top Performers (PR #6)

1. **CodeRabbit AI**: 9 actionable comments, caught critical CAS bug
2. **cubic-dev-ai**: 6 P0-P2 issues, Jane Street alignment
3. **Codacy Production**: 34 issues (6 critical, 1 high, 27 medium)
4. **Amazon Q Developer**: Critical blocking review
5. **Semgrep CLI**: 100% V12 DNA alignment

### Critical Findings from PR #6

**P0 Issues**:
- `MockOrderTracker.CancelOrder`: CAS operates on local variable (non-atomic)
- Benchmark dead-code elimination: computed values not consumed

**P1 Issues**:
- Struct copy semantics bug in tests
- V12 prefix naming violations
- JIT optimization risks in benchmarks

**P2 Issues**:
- Benchmark project references test project (bloat)
- `RunStrategy.Monitoring` misuse for micro-benchmarks
- Missing `IEquatable<T>` on mock structs

### Recommendations

1. **Keep Core Set**: CodeRabbit, cubic, Codacy, Semgrep, CodeQL, SonarCloud
2. **Remove Duplicates**: Evaluate 3+ overlapping code review tools
3. **Fix Silent Apps**: 15+ apps not posting on PRs
4. **Integrate Coverage**: Currently at 0%, target 80%
5. **Reduce Noise**: 40% tool overlap, target <30%

---

**Document Version**: 1.0  
**Next Review**: After PR #6 merge  
**Maintainer**: V12 Engineering Team
