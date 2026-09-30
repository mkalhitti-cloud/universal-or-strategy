# Codacy Full Integration Plan for V12

**Created**: 2026-05-22
**Status**: DRAFT - Awaiting approval
**Owner**: Orchestrator

## Executive Summary

Codacy baseline: 3,100 issues (technical debt), Grade B, 0% coverage. This plan establishes coverage tracking, quality gates, and CI/CD integration to prevent new debt and systematically reduce existing issues.

## Current State

**Codacy Metrics** (as of 2026-05-22):
- **Total Issues**: 3,100
  - Error prone: 1,000
  - Code style: 1,000
  - Code complexity: 288
  - Best practice: 160
  - Compatibility: 80
  - Performance: 36
  - Security: 29
  - Unused code: 6
- **Complexity**: 32% (31/207 files)
- **Duplication**: 20%
- **Coverage**: 0% (no test data)
- **Grade**: B (above goal)

**PR #1 Status**: ✅ "Up to quality standards" (no new issues introduced)

## Integration Strategy

### Phase 1: Coverage Integration (HIGH PRIORITY)

**Goal**: Establish baseline coverage metrics and track improvements

**Current Gap**: 0% coverage reported to Codacy

**Implementation**:

1. **Generate Coverage Reports** (C# - Coverlet/OpenCover)
   ```yaml
   # Add to .github/workflows/test.yml
   - name: Run tests with coverage
     run: |
       dotnet test Linting.csproj \
         --collect:"XPlat Code Coverage" \
         --results-directory ./coverage \
         -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
   
   - name: Upload coverage to Codacy
     env:
       CODACY_PROJECT_TOKEN: ${{ secrets.CODACY_PROJECT_TOKEN }}
     run: |
       bash <(curl -Ls https://coverage.codacy.com/get.sh) report \
         -r ./coverage/**/coverage.cobertura.xml
   ```

2. **Set Coverage Goals**
   - **Initial target**: 40% (realistic for existing codebase)
   - **Incremental target**: +5% per quarter
   - **Critical paths**: 80% (REAPER, SIMA, Order Management)

3. **Benefits**:
   - Track test coverage on PRs (diff coverage)
   - Identify untested critical paths (REAPER, SIMA)
   - Prevent coverage regression

**Effort**: 1 day (add GitHub Action + validate)

---

### Phase 2: Quality Gates (MEDIUM PRIORITY)

**Goal**: Prevent new issues, chip away at debt

**Recommended Quality Gates** (Repository Settings > Quality Gates):
```yaml
- No new issues in PR: BLOCKING
- No new security issues: BLOCKING
- No new error-prone patterns: BLOCKING
- Coverage delta ≥ 0%: WARNING
```

**Debt Reduction Strategy**:
- **Boy Scout Rule**: Fix issues in files you touch
- **Debt Sprints**: Dedicate 20% of sprint to debt reduction
- **Priority Order**: Security (29) → Error-prone (1k) → Complexity (288) → Style (1k)

**Effort**: 2 hours (configure in Codacy UI)

---

### Phase 3: CI/CD Integration (HIGH PRIORITY)

**Goal**: Automated quality gates in CI pipeline

**GitHub Actions Workflow**:
```yaml
# .github/workflows/codacy-analysis.yml
name: Codacy Analysis

on:
  pull_request:
    branches: [main, develop, feat/*]
  push:
    branches: [main]

jobs:
  codacy-security-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Run Codacy Analysis CLI
        uses: codacy/codacy-analysis-cli-action@master
        with:
          project-token: ${{ secrets.CODACY_PROJECT_TOKEN }}
          upload: true
          max-allowed-issues: 0  # Block on new issues
          
      - name: Check quality gate
        run: |
          # Fail if new issues introduced
          if [ "$CODACY_NEW_ISSUES" -gt 0 ]; then
            echo "❌ Quality gate failed: $CODACY_NEW_ISSUES new issues"
            exit 1
          fi
```

**Benefits**:
- Automated quality checks on every PR
- Block PRs with new issues
- Consistent enforcement across team

**Effort**: 1 day (create workflow + test)

---

### Phase 4: Codacy Configuration File (MEDIUM PRIORITY)

**Goal**: Customize analysis for V12 patterns

**Create `.codacy.yml`**:
```yaml
---
engines:
  # Enable C# analyzers
  roslyn:
    enabled: true
    
  # Duplication detection
  duplication:
    enabled: true
    exclude_paths:
      - 'tests/**'
      - 'benchmarks/**'

exclude_paths:
  - 'docs/**'
  - 'scripts/**'
  - '.github/**'
  - 'conductor/**'
  - 'Traycerrefactor/**'

# Custom complexity thresholds (Jane Street alignment)
complexity:
  threshold: 15  # Keep functions simple
  
# Ignore patterns
ignore:
  # Legacy files (to be refactored separately)
  - 'src/V12_002.Atm.cs'
  - 'src/V12_002.DrawingHelpers.cs'
```

**Effort**: 2 hours (create config + validate)

---

### Phase 5: API Integration (ADVANCED)

**Goal**: Custom dashboards and automated reporting

**Use Cases**:
1. **Daily Quality Dashboard**: Track metrics over time
2. **Custom Metrics**: V12-specific patterns (lock-free, ASCII-only)
3. **Batch Operations**: Bulk ignore false positives

**Example - Quality Dashboard Script**:
```python
# scripts/codacy_dashboard.py
import requests
import os
from datetime import datetime

API_TOKEN = os.getenv('CODACY_API_TOKEN')
ORG = 'mdasdispatch-hash'
REPO = 'universal-or-strategy'

headers = {'api-token': API_TOKEN}
base_url = f'https://app.codacy.com/api/v3/organizations/gh/{ORG}/repositories/{REPO}'

# Get quality metrics
response = requests.get(f'{base_url}/quality-overview', headers=headers)
metrics = response.json()

print(f"=== V12 Quality Dashboard ({datetime.now().strftime('%Y-%m-%d')}) ===")
print(f"Grade: {metrics['grade']}")
print(f"Issues: {metrics['issuesCount']}")
print(f"Coverage: {metrics.get('coveragePercentage', 0)}%")
print(f"Complexity: {metrics.get('complexityPercentage', 0)}%")

# Get issue breakdown
issues_response = requests.get(f'{base_url}/issues', headers=headers)
issues = issues_response.json()

print("\n=== Issue Breakdown ===")
for category, count in issues['categories'].items():
    print(f"{category}: {count}")
```

**Effort**: 3 days (API integration + dashboard)

---

## Implementation Timeline

### Week 1: Coverage Foundation
- **Day 1**: Add coverage generation to test workflow
- **Day 2**: Configure Codacy coverage upload
- **Day 3**: Validate coverage metrics on Codacy dashboard
- **Day 4**: Set coverage goals (40% initial, 80% critical paths)
- **Day 5**: Document coverage workflow in AGENTS.md

### Week 2: Quality Gates
- **Day 1**: Configure quality gates in Codacy UI
- **Day 2**: Test quality gates on sample PR
- **Day 3**: Create `.codacy.yml` configuration
- **Day 4**: Document quality gate workflow
- **Day 5**: Team training on Codacy integration

### Week 3: CI/CD Automation
- **Day 1**: Create `codacy-analysis.yml` workflow
- **Day 2**: Test workflow on PR #1
- **Day 3**: Integrate with PR perfection loop
- **Day 4**: Update AGENTS.md with Codacy protocols
- **Day 5**: Validate end-to-end flow

### Month 2: Advanced Integration
- **Week 1**: API integration + dashboard script
- **Week 2**: Custom metrics for V12 patterns
- **Week 3**: Automated reporting (daily/weekly)
- **Week 4**: Team retrospective + optimization

---

## Key Benefits for V12

1. **Coverage Tracking**: Know which REAPER/SIMA paths are untested
2. **Regression Prevention**: Block PRs that introduce new issues
3. **Technical Debt Visibility**: 3,100 issues → prioritized backlog
4. **Security Hardening**: 29 security issues → actionable fixes
5. **Complexity Management**: 288 complex files → refactoring targets
6. **Jane Street Alignment**: Enforce complexity thresholds (≤15)

---

## Success Metrics

**Month 1**:
- ✅ Coverage: 0% → 40%
- ✅ New issues per PR: 0 (quality gates enforced)
- ✅ CI/CD: 100% PRs scanned by Codacy

**Quarter 1**:
- ✅ Coverage: 40% → 50%
- ✅ Security issues: 29 → 0
- ✅ Error-prone issues: 1,000 → 800

**Year 1**:
- ✅ Coverage: 50% → 70%
- ✅ Total issues: 3,100 → 1,500
- ✅ Grade: B → A

---

## Risk Mitigation

**Risk**: Coverage upload failures
- **Mitigation**: Add retry logic + fallback to manual upload

**Risk**: False positives blocking PRs
- **Mitigation**: `.codacy.yml` ignore patterns + manual override process

**Risk**: Team resistance to quality gates
- **Mitigation**: Gradual rollout + training sessions + clear documentation

---

## Next Steps

1. **Approval**: Review this plan with team
2. **Secrets**: Add `CODACY_PROJECT_TOKEN` to GitHub Secrets
3. **Kickoff**: Start Week 1 (Coverage Foundation)
4. **Monitor**: Track metrics weekly
5. **Iterate**: Adjust based on team feedback

---

## References

- **Codacy Coverage Docs**: https://docs.codacy.com/coverage-reporter/
- **Codacy API Docs**: https://docs.codacy.com/codacy-api/
- **Current Baseline**: https://app.codacy.com/gh/mdasdispatch-hash/universal-or-strategy/dashboard
- **PR #1 Status**: https://github.com/mdasdispatch-hash/universal-or-strategy/pull/1