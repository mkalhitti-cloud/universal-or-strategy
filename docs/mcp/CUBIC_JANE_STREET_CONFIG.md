# Cubic (Jane Street Sentinel) Full-Pass Configuration

**Purpose**: Configure your custom Cubic agent "Jane Street Sentinel" for full-pass reviews instead of trading-specific rules only.

## Access Cubic Dashboard

1. Go to: https://www.cubic.dev/dashboard
2. Navigate to: **Agents** → **Jane Street Sentinel**
3. Click: **Edit Configuration**

## Full-Pass Configuration

Replace the existing configuration with this full-pass setup:

```yaml
# Jane Street Sentinel - Full-Pass Review Configuration
# V12 Universal OR Strategy - Unified 5/5 Scoring
# Protocol: Review EVERYTHING with Jane Street principles

agent_name: "Jane Street Sentinel"
version: "2.0"
review_mode: full_pass

# Core Principle: NO SPECIALIZATION
# Jane Street Sentinel evaluates ALL dimensions through Jane Street lens
dimensions:
  security:
    enabled: true
    weight: 20
    jane_street_principles:
      - "Correctness by Construction"
      - "Make illegal states unrepresentable"
      - "Zero-trust validation"
      - "Explicit error handling"
    checks:
      - lock_free_concurrency  # Jane Street: No lock() blocks
      - atomic_operations      # CAS on shared state only
      - immutable_data         # Prefer readonly structs
      - no_exceptions_hot_path # Return Result<T> instead
      - input_validation       # Validate at boundaries
  
  code_quality:
    enabled: true
    weight: 20
    jane_street_principles:
      - "Cognitive Simplicity"
      - "Single Responsibility"
      - "Composition over Inheritance"
    checks:
      - cyclomatic_complexity  # CYC ≤ 8 (ultra-strict)
      - function_length        # ≤50 lines
      - class_size             # ≤300 lines
      - nesting_depth          # ≤3 levels
      - parameter_count        # ≤4 parameters
      - ascii_only             # No Unicode (V12 DNA)
  
  testing:
    enabled: true
    weight: 20
    jane_street_principles:
      - "Property-Based Testing"
      - "Exhaustive Edge Cases"
      - "Fast Feedback Loops"
    checks:
      - test_coverage          # ≥80%
      - property_tests         # QuickCheck-style
      - edge_case_coverage     # Boundary conditions
      - test_isolation         # No shared state
      - fast_tests             # <100ms per test
  
  performance:
    enabled: true
    weight: 20
    jane_street_principles:
      - "Zero-Allocation Hot Paths"
      - "Lock-Free Algorithms"
      - "Mechanical Sympathy"
    checks:
      - allocation_free        # No new in hot paths
      - lock_free              # FSM/Actor model only
      - cache_friendly         # Sequential access patterns
      - branch_prediction      # Minimize branches in loops
      - linq_in_hot_paths      # BANNED in hot paths
  
  standards:
    enabled: true
    weight: 20
    jane_street_principles:
      - "Explicit is Better than Implicit"
      - "Documentation as Code"
      - "API Design First"
    checks:
      - naming_conventions     # Clear, unambiguous names
      - documentation          # XML docs on public APIs
      - api_design             # Minimal surface area
      - error_messages         # Actionable, specific
      - logging_practices      # Structured logging only

# Jane Street Specific Rules
jane_street_rules:
  # Concurrency (CRITICAL)
  - id: "JS-001"
    name: "Lock-Free Concurrency"
    severity: P0
    pattern: "lock\\s*\\("
    message: "BANNED: lock() blocks. Use FSM/Actor Enqueue model."
    remediation: "Replace with Enqueue(ctx => ...) actor pattern"
  
  # String Safety (CRITICAL)
  - id: "JS-002"
    name: "ASCII-Only Strings"
    severity: P0
    pattern: "[^\\x00-\\x7F]"
    message: "BANNED: Non-ASCII characters. NinjaTrader compiler crashes."
    remediation: "Use ASCII equivalents: (!) not emoji, -- not em-dash"
  
  # Complexity (CRITICAL)
  - id: "JS-003"
    name: "Cognitive Simplicity"
    severity: P0
    threshold: 8
    message: "CYC > 8. Extract sub-methods."
    remediation: "Break into smaller functions with clear names"
  
  # Allocation (HIGH)
  - id: "JS-004"
    name: "Zero-Allocation Hot Paths"
    severity: P1
    pattern: "new\\s+\\w+\\("
    context: "hot_path"
    message: "Allocation in hot path. Use object pooling or stack allocation."
    remediation: "Use ArrayPool<T> or stackalloc"
  
  # LINQ (HIGH)
  - id: "JS-005"
    name: "No LINQ in Hot Paths"
    severity: P1
    pattern: "\\.Where\\(|\\.Select\\(|\\.OrderBy\\("
    context: "hot_path"
    message: "LINQ in hot path. Use for loops."
    remediation: "Replace with explicit for/foreach loops"
  
  # Immutability (MEDIUM)
  - id: "JS-006"
    name: "Prefer Immutable Data"
    severity: P2
    pattern: "public\\s+\\w+\\s+\\w+\\s*\\{\\s*get;\\s*set;"
    message: "Mutable property. Consider readonly struct."
    remediation: "Use readonly struct or init-only property"

# Exclusions (V12 DNA Deviations)
exclusions:
  # Documented Jane Street deviations
  - path: "src/V12_001.cs"
    reason: "Legacy code - scheduled for Phase 8 refactor"
    rules: ["JS-001", "JS-003"]
  
  - path: "src/V12_002.cs"
    reason: "FSM promotion in progress"
    rules: ["JS-001"]

# Scoring Configuration
scoring:
  format: "X/5"
  threshold_excellent: 90  # 5/5 stars
  threshold_good: 75       # 4/5 stars
  threshold_fair: 60       # 3/5 stars
  threshold_poor: 40       # 2/5 stars
  
  # Dimension weights (equal)
  weights:
    security: 20
    code_quality: 20
    testing: 20
    performance: 20
    standards: 20

# Output Template
output:
  format: markdown
  template: |
    ## Jane Street Sentinel Full-Pass Review
    
    **Overall Score**: {score}/5 ⭐⭐⭐⭐⭐
    
    ### Dimension Breakdown
    - **Security**: {security_score}/5 ({security_issues} issues)
    - **Code Quality**: {quality_score}/5 ({quality_issues} issues)
    - **Testing**: {testing_score}/5 ({testing_issues} issues)
    - **Performance**: {performance_score}/5 ({perf_issues} issues)
    - **Standards**: {standards_score}/5 ({standards_issues} issues)
    
    ### Jane Street Alignment
    - Lock-Free: {lock_free_status}
    - ASCII-Only: {ascii_status}
    - CYC ≤ 8: {complexity_status}
    - Zero-Allocation: {allocation_status}
    
    ### Verdict
    {verdict}

# Integration
integrations:
  github:
    enabled: true
    comment_on_pr: true
    block_merge_below: 3  # Block if score < 3/5
  
  slack:
    enabled: false
    webhook_url: ""

# Hot Path Detection
hot_path_detection:
  enabled: true
  markers:
    - "OnBarUpdate"
    - "OnMarketData"
    - "OnOrderUpdate"
    - "ProcessBracketEvent"
    - "MonitorRmaProximity"
  
  # Files in hot paths
  hot_files:
    - "src/V12_001.cs"
    - "src/V12_002.cs"
```

## Apply Configuration

1. **Copy** the YAML above
2. **Paste** into Cubic dashboard configuration editor
3. **Save** configuration
4. **Test** on a sample PR:
   ```bash
   cubic review --pr <PR_NUMBER>
   ```

## Verify Full-Pass Mode

After configuration, verify Cubic is reviewing ALL dimensions:

```bash
# Trigger a review
cubic review --pr 6

# Expected output format:
# Jane Street Sentinel Full-Pass Review
# Overall Score: X/5 ⭐⭐⭐⭐⭐
# 
# Dimension Breakdown:
# - Security: X/5
# - Code Quality: X/5
# - Testing: X/5
# - Performance: X/5
# - Standards: X/5
```

## Jane Street Baseline

Your current Jane Street baseline (from `docs/brain/JANE_STREET_BASELINE_AUDIT.md`):
- **347 P0 violations** (documented and accepted)
- **Lock-Free**: 98% compliant (2% legacy code)
- **ASCII-Only**: 100% compliant
- **CYC ≤ 8**: 31 methods pending (EPIC-CCN-15 to EPIC-CCN-45)

## Integration with /pr-loop

Jane Street Sentinel is automatically invoked during `/pr-loop` Step 1 (Bot Forensics):

```bash
/pr-loop 6

# Step 1: Bot Forensics + Jane Street Audit
# - Extracts Cubic findings
# - Categorizes as VALID-FIX or VALID-SUPPRESS
# - Cross-references against Jane Street baseline
# - Documents new deviations in JANE_STREET_DEVIATIONS.md
```

## Suppression Protocol

If Cubic flags an issue that conflicts with Jane Street principles:

1. **Verify** it's a genuine Jane Street deviation (not a bug)
2. **Document** in `docs/standards/JANE_STREET_DEVIATIONS.md`:
   ```markdown
   ## Decision #N: [Title]
   
   **Date**: 2026-06-05
   **Cubic Issue**: JS-XXX
   **Jane Street Principle**: [Principle name]
   **Rationale**: [Why we deviate]
   **Scope**: [Affected files/patterns]
   **Mitigation**: [How we compensate]
   ```
3. **Suppress** in `.codacy.yml`:
   ```yaml
   exclude_paths:
     - "src/V12_001.cs"  # Jane Street deviation: Decision #N
   ```

## References

- **Jane Street Baseline**: `docs/brain/JANE_STREET_BASELINE_AUDIT.md`
- **Deviations Log**: `docs/standards/JANE_STREET_DEVIATIONS.md`
- **Cubic Dashboard**: https://www.cubic.dev/dashboard
- **V12 DNA Protocol**: `AGENTS.md` (Section: V12 Permanent DNA)