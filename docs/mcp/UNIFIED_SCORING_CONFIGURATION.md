# Unified 5/5 Scoring Configuration

## Core Principle: NO SPECIALIZATION

**BANNED**: Agents reviewing only their specialty (Snyk = security only, CodeAnt = quality only)

**REQUIRED**: Every agent performs a COMPLETE, FULL-PASS review of EVERYTHING

---

## The Problem with Specialized Reviews

### ❌ Current (Fragmented)
```
PR #123:
- Snyk: ✅ 5/5 (only checked security, ignored quality)
- CodeAnt: ❌ 3/5 (only checked quality, ignored security)
- SonarCloud: ⚠️ 4/5 (only checked maintainability, ignored both)

Result: Unclear if PR is actually good or bad
```

### ✅ Target (Unified)
```
PR #123:
- Snyk: ✅ 5/5 (checked EVERYTHING: security, quality, testing, performance, standards)
- CodeAnt: ✅ 5/5 (checked EVERYTHING: security, quality, testing, performance, standards)
- SonarCloud: ✅ 5/5 (checked EVERYTHING: security, quality, testing, performance, standards)

Result: Clear consensus - PR is excellent across ALL dimensions
```

---

## Configuration: Full-Pass Review Mode

### 1. CodeAnt AI - Full-Pass Configuration

**File**: `.codeant.yml`

```yaml
# CodeAnt Full-Pass Review Configuration
version: 2.0

# CRITICAL: Full-pass mode (not specialized)
review_mode: full_pass

# Check EVERYTHING (not just code quality)
dimensions:
  security:
    enabled: true
    rules:
      - secrets-detection
      - vulnerability-scanning
      - sql-injection
      - xss-detection
      - command-injection
      - insecure-deserialization
    weight: 20%
    
  code_quality:
    enabled: true
    rules:
      - cyclomatic-complexity
      - code-duplication
      - code-smells
      - maintainability-index
      - cognitive-complexity
    weight: 20%
    
  testing:
    enabled: true
    rules:
      - test-coverage
      - test-quality
      - flaky-tests
      - test-naming
    weight: 20%
    
  performance:
    enabled: true
    rules:
      - algorithmic-efficiency
      - memory-leaks
      - resource-management
      - blocking-operations
    weight: 20%
    
  standards:
    enabled: true
    rules:
      - v12-dna-compliance
      - jane-street-rules
      - style-guide
      - naming-conventions
    weight: 20%

# Unified scoring (all dimensions must pass)
scoring:
  method: holistic
  pass_threshold: 4.5  # 90% overall
  
  # ALL dimensions required for 5/5
  dimension_requirements:
    security: 5.0      # Zero tolerance
    code_quality: 4.5  # Minor issues OK
    testing: 4.0       # Good coverage required
    performance: 4.5   # No critical issues
    standards: 5.0     # Full compliance

# C# specific (only scan .cs files)
languages:
  csharp:
    enabled: true
    file_patterns: ["*.cs"]
    exclude_patterns: ["*.Designer.cs", "*.g.cs", "*AssemblyInfo.cs"]

# Output format
output:
  format: unified_score
  show_dimension_breakdown: true
  show_all_issues: true
```

**Command**:
```bash
# Run full-pass review
codeant review --config .codeant.yml

# Expected output:
# ═══════════════════════════════════════
# CodeAnt Full-Pass Review: 5/5 ⭐⭐⭐⭐⭐
# ═══════════════════════════════════════
# 
# Security: 5/5 (0 vulnerabilities, 0 secrets)
# Code Quality: 5/5 (CCN ≤15, 0 code smells)
# Testing: 4/5 (82% coverage, target 80%)
# Performance: 5/5 (0 performance issues)
# Standards: 5/5 (V12 DNA compliant)
# 
# Overall: EXCELLENT - All checks passed
```

---

### 2. Cubic (Jane Street Sentinel) - Full-Pass Configuration

**Custom Agent Configuration**:

```yaml
# Jane Street Sentinel - Full-Pass Mode
name: "Jane Street Sentinel"
version: 2.0

# CRITICAL: Full-pass mode (not just trading rules)
review_mode: full_pass

# Check EVERYTHING with Jane Street standards
dimensions:
  security:
    enabled: true
    jane_street_rules:
      - no-hardcoded-credentials
      - no-sql-injection
      - no-command-injection
      - secure-random-generation
      - input-sanitization
      - output-encoding
    severity: P0  # Block merge
    weight: 25%
    
  code_quality:
    enabled: true
    jane_street_rules:
      - max-cyclomatic-complexity: 15
      - max-function-length: 50
      - no-god-classes
      - single-responsibility
      - dry-principle
      - solid-principles
    severity: P1  # Block merge
    weight: 20%
    
  testing:
    enabled: true
    jane_street_rules:
      - min-coverage: 80%
      - no-flaky-tests
      - test-naming-convention
      - test-isolation
      - mock-quality
    severity: P1  # Block merge
    weight: 20%
    
  performance:
    enabled: true
    jane_street_rules:
      - no-n-squared-algorithms
      - no-memory-leaks
      - no-blocking-async
      - efficient-data-structures
      - cache-optimization
    severity: P1  # Block merge
    weight: 20%
    
  standards:
    enabled: true
    jane_street_rules:
      - v12-dna-compliance
      - no-emoji-in-code
      - no-lock-state-lock
      - fsm-guard-validation
      - actor-model-enforcement
    severity: P0  # Block merge
    weight: 15%

# Unified scoring (Jane Street style)
scoring:
  method: holistic
  formula: weighted_average
  pass_threshold: 4.5
  
  # ALL dimensions required
  dimension_requirements:
    security: 5.0      # P0 violations = fail
    code_quality: 4.5  # P1 violations = warning
    testing: 4.0       # P2 violations = info
    performance: 4.5   # P1 violations = warning
    standards: 5.0     # P0 violations = fail

# Output format
output:
  format: jane_street_report
  labeling:
    5.0: "[EXCELLENT-JS] All Jane Street standards met"
    4.5-4.9: "[GOOD-JS] Minor improvements needed"
    4.0-4.4: "[ACCEPTABLE-JS] Some issues to address"
    < 4.0: "[NEEDS-WORK-JS] Significant issues"
```

**Expected Output**:
```
═══════════════════════════════════════════════
Jane Street Sentinel Full-Pass Review: 5/5 ⭐⭐⭐⭐⭐
═══════════════════════════════════════════════

Security: 5/5 (0 P0 issues)
  ✅ No hardcoded credentials
  ✅ No SQL injection risks
  ✅ Input sanitization present
  ✅ Secure random generation

Code Quality: 5/5 (0 P1 issues)
  ✅ CCN ≤15 (max: 12)
  ✅ Function length ≤50 (max: 45)
  ✅ SOLID principles followed
  ✅ DRY principle followed

Testing: 4/5 (coverage 82%, target 80%)
  ✅ Coverage above threshold
  ✅ No flaky tests
  ✅ Test naming conventions followed
  ⚠️ 3 tests could use better isolation

Performance: 5/5 (0 P1 issues)
  ✅ No N² algorithms
  ✅ No memory leaks
  ✅ No blocking async operations
  ✅ Efficient data structures

Standards: 5/5 (0 P0 issues)
  ✅ V12 DNA compliant
  ✅ No emoji in code
  ✅ No lock(stateLock) violations
  ✅ FSM guards validated

Overall: [EXCELLENT-JS] All Jane Street standards met
```

---

### 3. Snyk - Full-Pass Configuration

**File**: `.snyk`

```yaml
# Snyk Full-Pass Review Configuration
version: v1.22.0

# CRITICAL: Full-pass mode (not just security)
review_mode: full_pass

# Check EVERYTHING (not just vulnerabilities)
dimensions:
  security:
    enabled: true
    checks:
      - secrets-detection
      - vulnerability-scanning
      - code-injection
      - xss-detection
      - sql-injection
      - insecure-dependencies
    weight: 25%
    
  code_quality:
    enabled: true
    checks:
      - error-handling
      - input-validation
      - resource-management
      - concurrency-issues
      - code-smells
    weight: 20%
    
  testing:
    enabled: true
    checks:
      - test-coverage
      - test-quality
      - integration-tests
    weight: 20%
    
  performance:
    enabled: true
    checks:
      - memory-leaks
      - resource-leaks
      - inefficient-algorithms
    weight: 15%
    
  standards:
    enabled: true
    checks:
      - licensing-compliance
      - dependency-freshness
      - best-practices
    weight: 20%

# Unified scoring
scoring:
  method: holistic
  pass_threshold: 4.5
  
  dimension_requirements:
    security: 5.0
    code_quality: 4.5
    testing: 4.0
    performance: 4.5
    standards: 4.5

# Output format
output:
  format: unified_score
  show_all_dimensions: true
```

**Expected Output**:
```
═══════════════════════════════════════
Snyk Full-Pass Review: 5/5 ⭐⭐⭐⭐⭐
═══════════════════════════════════════

Security: 5/5 (0 critical, 0 high, 0 medium)
Code Quality: 5/5 (0 security-impacting issues)
Testing: 4/5 (coverage 82%, target 80%)
Performance: 5/5 (0 memory leaks, 0 resource leaks)
Standards: 5/5 (all dependencies compliant)

Overall: EXCELLENT - All checks passed
```

---

### 4. SonarCloud - Full-Pass Configuration

**File**: `sonar-project.properties`

```properties
# SonarCloud Full-Pass Review Configuration
sonar.projectKey=universal-or-strategy
sonar.organization=backtothefutures83-oss

# CRITICAL: Full-pass mode (not just code smells)
sonar.qualitygate.mode=full_pass

# Check EVERYTHING
sonar.qualitygate.dimensions=security,reliability,maintainability,coverage,performance

# Quality gate (ALL dimensions required for 5/5)
sonar.qualitygate.conditions=\
  security_rating=A,\
  reliability_rating=A,\
  maintainability_rating=A,\
  coverage>=80%,\
  duplicated_lines_density<=3%,\
  code_smells<=10,\
  bugs=0,\
  vulnerabilities=0,\
  security_hotspots_reviewed=100%,\
  cognitive_complexity<=15,\
  performance_issues=0

# Unified score calculation
sonar.score.method=holistic
sonar.score.dimensions=security,reliability,maintainability,coverage,performance
sonar.score.weights=25,20,20,15,20

# C# specific
sonar.cs.roslyn.reportFilePaths=**/roslyn-report.json
sonar.cs.vscoveragexml.reportsPaths=**/coverage.xml
```

**Expected Output**:
```
═══════════════════════════════════════════
SonarCloud Full-Pass Review: 5/5 ⭐⭐⭐⭐⭐
═══════════════════════════════════════════

Security: A (0 vulnerabilities, 0 hotspots)
Reliability: A (0 bugs)
Maintainability: A (5 code smells, target ≤10)
Coverage: 82% (target ≥80%)
Performance: A (0 performance issues)

Overall Quality Gate: PASSED
Quality Score: 96/100 (Excellent)
```

---

### 5. GitGuardian - Full-Pass Configuration

**File**: `.gitguardian.yaml`

```yaml
# GitGuardian Full-Pass Review Configuration
version: 2

# CRITICAL: Full-pass mode (not just secrets)
review_mode: full_pass

# Check EVERYTHING (not just secrets)
dimensions:
  security:
    enabled: true
    checks:
      - secrets-detection
      - api-keys
      - database-credentials
      - private-keys
      - oauth-tokens
      - custom-patterns
    weight: 30%
    
  code_quality:
    enabled: true
    checks:
      - security-best-practices
      - error-handling
      - input-validation
    weight: 20%
    
  testing:
    enabled: true
    checks:
      - security-test-coverage
      - penetration-tests
    weight: 15%
    
  performance:
    enabled: true
    checks:
      - encryption-efficiency
      - hashing-algorithms
    weight: 15%
    
  standards:
    enabled: true
    checks:
      - compliance-gdpr
      - compliance-pci-dss
      - compliance-sox
      - secrets-rotation
      - least-privilege
    weight: 20%

# Unified scoring
scoring:
  method: holistic
  pass_threshold: 4.5
  
  dimension_requirements:
    security: 5.0      # Zero secrets
    code_quality: 4.5
    testing: 4.0
    performance: 4.5
    standards: 5.0     # Full compliance

# Output format
output:
  format: unified_score
  show_all_dimensions: true
```

**Expected Output**:
```
═══════════════════════════════════════════════
GitGuardian Full-Pass Review: 5/5 ⭐⭐⭐⭐⭐
═══════════════════════════════════════════════

Security: 5/5 (0 secrets detected)
Code Quality: 5/5 (all best practices followed)
Testing: 4/5 (security tests present)
Performance: 5/5 (efficient encryption)
Standards: 5/5 (GDPR, PCI-DSS, SOX compliant)

Overall Security Posture: 100% (Excellent)
```

---

## GitHub PR Comment Format

All bots post the SAME format (unified):

```markdown
## 🎯 [Bot Name] Full-Pass Review: 5/5 ⭐⭐⭐⭐⭐

### Complete Codebase Assessment

| Dimension | Score | Status |
|-----------|-------|--------|
| 🔒 Security | 5/5 | ✅ 0 vulnerabilities, 0 secrets |
| 📊 Code Quality | 5/5 | ✅ CCN ≤15, 0 code smells |
| 🧪 Testing | 4/5 | ✅ 82% coverage (target 80%) |
| ⚡ Performance | 5/5 | ✅ 0 performance issues |
| 📋 Standards | 5/5 | ✅ V12 DNA compliant |

### Overall Assessment

✅ **EXCELLENT** - All dimensions checked. All standards met. Ready to merge.

---

*Full-pass review completed - all aspects of codebase evaluated*
```

---

## Implementation Steps

### Step 1: Update All Bot Configurations

```bash
# 1. CodeAnt
cat > .codeant.yml << 'EOF'
review_mode: full_pass
dimensions:
  security: {enabled: true, weight: 20%}
  code_quality: {enabled: true, weight: 20%}
  testing: {enabled: true, weight: 20%}
  performance: {enabled: true, weight: 20%}
  standards: {enabled: true, weight: 20%}
EOF

# 2. Snyk
cat > .snyk << 'EOF'
review_mode: full_pass
dimensions:
  security: {enabled: true, weight: 25%}
  code_quality: {enabled: true, weight: 20%}
  testing: {enabled: true, weight: 20%}
  performance: {enabled: true, weight: 15%}
  standards: {enabled: true, weight: 20%}
EOF

# 3. SonarCloud
cat > sonar-project.properties << 'EOF'
sonar.qualitygate.mode=full_pass
sonar.qualitygate.dimensions=security,reliability,maintainability,coverage,performance
EOF

# 4. GitGuardian
cat > .gitguardian.yaml << 'EOF'
review_mode: full_pass
dimensions:
  security: {enabled: true, weight: 30%}
  code_quality: {enabled: true, weight: 20%}
  testing: {enabled: true, weight: 15%}
  performance: {enabled: true, weight: 15%}
  standards: {enabled: true, weight: 20%}
EOF

# 5. Cubic (Jane Street Sentinel)
# Update custom agent via Cubic dashboard
# Set review_mode: full_pass
```

### Step 2: Test Full-Pass Reviews

```bash
# Test each bot locally
codeant review --config .codeant.yml
snyk test --all-projects
sonar-scanner
# (GitGuardian and Cubic test on PR)
```

### Step 3: Verify Unified Scoring

Create test PR and verify all bots report:
- ✅ Same 5/5 format
- ✅ All dimensions checked
- ✅ Consistent assessment

---

## Benefits of Full-Pass Reviews

### 1. No Blind Spots
- ✅ Every bot checks EVERYTHING
- ✅ No "that's not my job" gaps
- ✅ Comprehensive coverage

### 2. Redundancy = Reliability
- ✅ Multiple bots catch same issues (good!)
- ✅ Consensus validation
- ✅ Reduced false negatives

### 3. Clear Merge Criteria
- ✅ All bots must agree (5/5)
- ✅ No conflicting assessments
- ✅ Simple decision: merge or fix

### 4. Faster Reviews
- ✅ One pass per bot (not multiple specialized passes)
- ✅ Parallel execution
- ✅ Clear feedback

---

## Summary

### Core Rule: NO SPECIALIZATION

**Every agent performs a COMPLETE review of:**
1. ✅ Security (secrets, vulnerabilities, injection)
2. ✅ Code Quality (complexity, duplication, maintainability)
3. ✅ Testing (coverage, quality, isolation)
4. ✅ Performance (efficiency, memory, algorithms)
5. ✅ Standards (V12 DNA, Jane Street rules, style)

### Expected Outcome

Every PR gets **5 independent full-pass reviews**, all reporting the same unified 5/5 score:

```
PR #123 Status:
- CodeAnt: 5/5 ⭐⭐⭐⭐⭐ (full-pass review)
- Cubic: 5/5 ⭐⭐⭐⭐⭐ (full-pass review)
- Snyk: 5/5 ⭐⭐⭐⭐⭐ (full-pass review)
- SonarCloud: 5/5 ⭐⭐⭐⭐⭐ (full-pass review)
- GitGuardian: 5/5 ⭐⭐⭐⭐⭐ (full-pass review)

Consensus: ✅ EXCELLENT - Ready to merge
```

No fragmentation. No specialization. Just complete, holistic reviews from every bot.