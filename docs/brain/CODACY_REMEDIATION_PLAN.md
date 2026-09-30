# Codacy Technical Debt Remediation Plan

**Generated**: 2026-05-27  
**Current Issues**: 1,957 (C#: 1,916 | Scripts: 41)  
**Target**: Zero noise in future PRs  
**Strategy**: Hybrid Severity-Pattern Approach

---

## Executive Summary

### Issue Distribution

| Category | Count | % of Total | Priority |
|----------|-------|------------|----------|
| Code Style | 1,100 | 56% | P2 (Automated) |
| Code Complexity | 375 | 19% | P1 (Manual) |
| Best Practice | 191 | 10% | P3 (Incremental) |
| Error Prone | 115 | 6% | P0 (Critical) |
| Compatibility | 98 | 5% | P3 (Deferred) |
| Performance | 45 | 2% | P2 (Review) |
| Security | 16 | 1% | P0 (Critical) |
| Unused Code | 16 | 1% | P2 (Cleanup) |

### Severity Distribution (C# Only)

| Severity | Count | % of C# | Action |
|----------|-------|---------|--------|
| HIGH | 140 | 7% | Immediate fix |
| MEDIUM | 662 | 35% | Phased approach |
| MINOR | 1,114 | 58% | Boy Scout Rule |

### Top Code Patterns (Quick Wins)

| Pattern | Count | Auto-Fix? | Effort |
|---------|-------|-----------|--------|
| Enforce Curly Braces | 909 | ✅ YES | 30 min |
| Enforce Medium Cyclomatic Complexity | 201 | ❌ NO | Ongoing (Phase 7) |
| Enforce Medium Number of Lines | 138 | ❌ NO | Architectural |
| Mark Assemblies as CLS Compliant | 98 | ✅ YES | 15 min |
| Avoid Redundant Modifiers | 80 | ✅ YES | 20 min |

---

## Remediation Strategy

### Phase 1: Critical Security & Error-Prone (P0)
**Target**: 131 issues (16 Security + 115 Error-Prone)  
**Timeline**: 1 sprint (2-3 hours)  
**Approach**: Fix ALL in one focused session  
**Epic**: EPIC-QUALITY-SEC

#### Breakdown
- **Security (16)**:
  - Potential SQL injection vectors
  - Unsafe deserialization
  - Path traversal vulnerabilities
  - Weak cryptography usage
  
- **Error-Prone (115)**:
  - Null reference risks
  - Resource leak patterns
  - Exception handling gaps
  - Race condition patterns

#### Execution Plan
1. Export all Security + Error-Prone issues via Codacy API
2. Categorize by file (cluster fixes)
3. Create branch: `epic-quality-sec-errorprone`
4. Fix all 131 issues
5. Run full validation suite
6. PR with detailed fix summary

#### Success Criteria
- ✅ Zero Security issues
- ✅ Zero Error-Prone issues
- ✅ All tests pass
- ✅ F5 verification successful

---

### Phase 2: High-Severity Complexity (P1)
**Target**: 140 HIGH severity issues  
**Timeline**: 2 sprints (4-6 hours)  
**Approach**: File cluster strategy  
**Epic**: EPIC-QUALITY-COMPLEXITY

#### File Clustering Strategy
1. **Identify hot files**: Files with 3+ HIGH issues
2. **Prioritize by impact**: Files in hot paths (OnBarUpdate, OnMarketData, ProcessOnExecutionUpdate)
3. **Extract methods**: Use Phase 7 complexity audit as guide
4. **Verify**: Each file cluster gets its own PR

#### Known Hot Files (from Phase 7 audit)
- `V12_002.UI.Callbacks.cs`: OnKeyDown (49 CYC)
- `V12_002.UI.IPC.cs`: ProcessIpc_MatchSymbol (49 CYC)
- `V12_002.UI.Panel.Handlers.cs`: AttachPanelHandlers (39 CYC)
- `V12_002.Trailing.cs`: ManageTrail_RunPerTradeBranches (36 CYC)
- `V12_002.Orders.Management.StopSync.cs`: ValidateStopPrice (33 CYC)

#### Execution Plan
1. Export HIGH severity issues, group by file
2. Cross-reference with Phase 7 complexity audit
3. Create extraction tickets (1 ticket per file cluster)
4. Use Bob CLI (`v12-engineer`) for surgical extractions
5. Each PR: 1-3 files max (stay under 10k diff limit)

#### Success Criteria
- ✅ Zero HIGH severity issues
- ✅ All extracted methods have CYC ≤ 15
- ✅ F5 verification after each PR
- ✅ No logic drift

---

### Phase 3: Pattern-Based Cleanup (P2)
**Target**: 1,248 issues (64% of total)  
**Timeline**: 1 sprint (automated) + ongoing (complexity)  
**Approach**: Automated fixes + architectural decisions  
**Epic**: EPIC-QUALITY-STYLE

#### Sub-Phase 3A: Automated Fixes (Quick Win)
**Target**: 909 + 98 + 80 = 1,087 issues  
**Timeline**: 1 hour  
**Tools**: Roslyn Code Fixes, ReSharper CLI

##### Patterns to Auto-Fix
1. **Curly Braces (909)**: 
   ```csharp
   // Before
   if (condition) DoSomething();
   
   // After
   if (condition) 
   { 
       DoSomething(); 
   }
   ```
   **Command**: Use Roslyn analyzer with auto-fix

2. **CLS Compliance (98)**:
   ```csharp
   [assembly: CLSCompliant(true)]
   ```
   **Action**: Add to AssemblyInfo.cs or global usings

3. **Redundant Modifiers (80)**:
   ```csharp
   // Before
   private sealed class Foo { }
   
   // After
   sealed class Foo { }
   ```
   **Command**: ReSharper cleanup profile

##### Execution Plan
1. Create branch: `epic-quality-style-autofix`
2. Run automated fixes:
   ```powershell
   # Curly braces
   dotnet format --severity info --diagnostics IDE0011
   
   # Redundant modifiers
   dotnet format --severity info --diagnostics IDE0040,IDE0044
   ```
3. Review diff (expect ~3k lines changed)
4. Commit: "style: Auto-fix 1,087 Codacy style violations"
5. Push and verify CI

##### Success Criteria
- ✅ 1,087 issues resolved
- ✅ Zero compilation errors
- ✅ Zero test failures
- ✅ Diff < 10k lines (split if needed)

#### Sub-Phase 3B: Complexity (Ongoing)
**Target**: 201 issues  
**Timeline**: Distributed across Phase 7 queue  
**Approach**: Leverage existing Phase 7 work

**Status**: Already tracked in `docs/brain/master_roadmap.md` Phase 7 queue
- 45 C# symbols with CYC > 20
- Extraction tickets already defined
- Ongoing work (not blocking this epic)

**Action**: Mark as "IN PROGRESS" in Codacy, link to Phase 7 tickets

#### Sub-Phase 3C: File Length (Deferred)
**Target**: 138 issues  
**Timeline**: M9 (post-production)  
**Approach**: Architectural review required

**Rationale**: 
- Splitting god-files requires architectural decisions
- May conflict with NinjaTrader's single-file strategy requirement
- Not blocking PR noise (file length is MINOR severity)

**Action**: Create EPIC-QUALITY-ARCHITECTURE for M9

---

### Phase 4: Style & Best Practice Polish (P3)
**Target**: ~578 remaining issues  
**Timeline**: Distributed across EPIC-2 through EPIC-4  
**Approach**: Boy Scout Rule  
**Epic**: EPIC-QUALITY-POLISH (ongoing)

#### Strategy
- Fix issues in files you touch during feature work
- No dedicated sprint - incremental cleanup
- Track progress in weekly health reports

#### Categories
- Naming conventions
- Documentation comments
- Unused using statements
- String concatenation → interpolation
- LINQ optimization opportunities

#### Success Criteria
- ✅ Issue count decreases by 10% per sprint
- ✅ No new issues introduced in PRs
- ✅ Grade improves from B → A over 3 months

---

## Configuration Updates

### 1. Exclude Scripts from Codacy

**File**: `.codacy.yml`

```yaml
exclude_paths:
  - "scripts/**"
  - "benchmarks/**"
  - "tests/**"
  - "conductor/**"
  - "routa-tools/**"
```

**Rationale**: 
- 41 PowerShell/Python issues are tooling, not production code
- Reduces noise by 2%
- Focuses Codacy on C# src/ files only

### 2. Update Complexity Threshold

**File**: `.codacy.yml`

```yaml
engines:
  duplication:
    enabled: true
    exclude_paths:
      - "tests/**"
      - "benchmarks/**"
  metrics:
    enabled: true
    config:
      threshold: 15  # Jane Street alignment (was 8)
```

**Rationale**:
- Aligns with V12 DNA CYC ≤ 15 standard
- Reduces false positives (Lizard's threshold 8 is too conservative for HFT)
- Matches `complexity_audit.py` threshold

### 3. Enable Auto-Fix Suggestions

**File**: `.codacy.yml`

```yaml
engines:
  roslyn:
    enabled: true
    config:
      file_extensions:
        - ".cs"
      rules:
        - IDE0011  # Curly braces
        - IDE0040  # Accessibility modifiers
        - IDE0044  # Readonly modifier
```

---

## PR Strategy

### Diff Size Management

**Problem**: 1,957 issues = massive diff, violates 10k limit

**Solution**: Split into focused PRs

| PR | Scope | Est. Diff | Timeline |
|----|-------|-----------|----------|
| PR-SEC | Security + Error-Prone (131) | ~500 lines | Week 1 |
| PR-STYLE-1 | Curly braces (909) | ~3k lines | Week 1 |
| PR-STYLE-2 | CLS + Redundant (178) | ~200 lines | Week 1 |
| PR-COMPLEX-1 | HIGH severity cluster 1 (30-40 issues) | ~1k lines | Week 2 |
| PR-COMPLEX-2 | HIGH severity cluster 2 (30-40 issues) | ~1k lines | Week 2 |
| PR-COMPLEX-3 | HIGH severity cluster 3 (30-40 issues) | ~1k lines | Week 3 |
| PR-COMPLEX-4 | HIGH severity cluster 4 (30-40 issues) | ~1k lines | Week 3 |

**Total**: 7 PRs over 3 weeks

### PR Hygiene Protocol

**Every PR must**:
1. Pass `pre_push_validation.ps1` (13/13 checks)
2. Include before/after Codacy issue count
3. Link to remediation plan section
4. F5 verification screenshot
5. Stay under 10k diff limit

---

## API Integration

### Export All Issues

```powershell
# Requires CODACY_API_TOKEN in .env
powershell -File .\scripts\query_codacy_issues.ps1 -ExportAll -OutputPath docs/brain/codacy_full_export.json
```

### Filter by Category

```powershell
# Security only
powershell -File .\scripts\query_codacy_issues.ps1 -Category Security -OutputPath docs/brain/codacy_security.json

# Error-Prone only
powershell -File .\scripts\query_codacy_issues.ps1 -Category ErrorProne -OutputPath docs/brain/codacy_errorprone.json

# HIGH severity only
powershell -File .\scripts\query_codacy_issues.ps1 -Severity High -OutputPath docs/brain/codacy_high_severity.json
```

### File Clustering Analysis

```powershell
# Group issues by file, sort by count
powershell -File .\scripts\analyze_codacy_clusters.ps1 -InputPath docs/brain/codacy_full_export.json
```

**Output**: `docs/brain/codacy_file_clusters.md`

---

## Success Metrics

### Sprint 1 (Week 1)
- ✅ Security: 16 → 0
- ✅ Error-Prone: 115 → 0
- ✅ Curly Braces: 909 → 0
- ✅ CLS + Redundant: 178 → 0
- **Total Reduction**: 1,218 issues (62%)
- **Remaining**: 739 issues

### Sprint 2-3 (Week 2-3)
- ✅ HIGH Severity: 140 → 0
- ✅ MEDIUM Complexity: 201 → ~150 (ongoing Phase 7)
- **Total Reduction**: ~190 issues
- **Remaining**: ~549 issues

### Post-Sprint (Ongoing)
- ✅ MEDIUM/MINOR: 549 → 0 (Boy Scout Rule)
- **Timeline**: 3-4 months
- **Target Grade**: A (from current B)

### Final State
- **Total Issues**: 1,957 → 0
- **Grade**: B → A
- **Complexity**: 58% → <10% (files with CYC > 15)
- **PR Noise**: Eliminated

---

## Risk Mitigation

### Risk 1: Automated Fixes Break Logic
**Mitigation**: 
- Run full test suite after each auto-fix
- F5 verification mandatory
- Rollback plan: `git revert` + redeploy

### Risk 2: Diff Size Exceeds 10k
**Mitigation**:
- Pre-check diff size before commit
- Split into smaller PRs if needed
- Use `git diff --stat` to monitor

### Risk 3: Complexity Fixes Introduce Bugs
**Mitigation**:
- Bob CLI surgical extractions only
- Arena AI red team audit before merge
- Mandatory F5 verification
- Checkpointing enabled

### Risk 4: Codacy API Rate Limits
**Mitigation**:
- 100 requests/hour limit (sufficient)
- Cache exports locally
- Batch operations where possible

---

## Next Steps

### Immediate (Today)
1. ✅ Update `.codacy.yml` (exclude scripts, adjust threshold)
2. ✅ Export all issues via API
3. ✅ Create file clustering analysis
4. ✅ Create EPIC-QUALITY-SEC ticket

### Week 1
1. Execute Phase 1 (Security + Error-Prone)
2. Execute Phase 3A (Automated style fixes)
3. Verify 1,218 issues resolved

### Week 2-3
1. Execute Phase 2 (HIGH severity complexity)
2. 4 PRs, ~140 issues resolved

### Ongoing
1. Phase 4 (Boy Scout Rule)
2. Monitor weekly progress
3. Update health reports

---

## References

- **Codacy Dashboard**: https://app.codacy.com/gh/malhitticrypto-debug/universal-or-strategy/dashboard
- **Phase 7 Complexity Audit**: `docs/brain/master_roadmap.md` (lines 645-729)
- **V12 DNA Standards**: `AGENTS.md` (Platinum Standard)
- **Jane Street Alignment**: `docs/intel/jane-street/` (CYC ≤ 15 rationale)

---

**Plan Status**: READY FOR EXECUTION  
**Next Action**: Update `.codacy.yml` and export issues via API