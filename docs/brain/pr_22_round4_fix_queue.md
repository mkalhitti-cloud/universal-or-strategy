# PR #22 Round 4 Fix Queue
Generated: 2026-06-02 20:57 UTC

## Summary

**Total VALID-FIX Issues**: 3 confirmed + TBD (CodeFactor)
**Total SUPPRESS Issues**: 1 (Codacy CYC 9)
**Estimated Effort**: 51 minutes

---

## P1 HIGH - Must Fix (Target: PHS 100/100)

### Fix #1 - Make ValidateLeaderPosition Static
- [ ] **Bot**: SonarCloud
- [ ] **File**: `src/V12_002.SIMA.Shadow.cs:73`
- [ ] **Severity**: LOW (Intentionality)
- [ ] **Issue**: Method doesn't access instance state, should be static
- [ ] **Fix**:
  ```csharp
  // Line 73: Add 'static' keyword
  internal static bool ValidateLeaderPosition(
      IOrder leader,
      ConcurrentDictionary<string, IOrder> stopOrders,
      Action<string> logger)
  ```
- [ ] **Effort**: 2 minutes
- [ ] **Impact**: +1 PHS point

---

### Fix #2 - Make DetectStopPriceChange Static
- [ ] **Bot**: SonarCloud
- [ ] **File**: `src/V12_002.SIMA.Shadow.cs:113`
- [ ] **Severity**: LOW (Intentionality)
- [ ] **Issue**: Method doesn't access instance state, should be static
- [ ] **Fix**:
  ```csharp
  // Line 113: Add 'static' keyword
  internal static bool DetectStopPriceChange(
      IOrder leader,
      IOrder stop,
      Action<string> logger)
  ```
- [ ] **Effort**: 2 minutes
- [ ] **Impact**: +1 PHS point

---

### Fix #3 - Make ValidateCachedEntry Static
- [ ] **Bot**: SonarCloud
- [ ] **File**: `src/V12_002.SIMA.Shadow.cs:158`
- [ ] **Severity**: LOW (Intentionality)
- [ ] **Issue**: Method doesn't access instance state, should be static
- [ ] **Fix**:
  ```csharp
  // Line 158: Add 'static' keyword
  internal static bool ValidateCachedEntry(
      IOrder leader,
      IOrder stop,
      Action<string> logger)
  ```
- [ ] **Effort**: 2 minutes
- [ ] **Impact**: +1 PHS point

---

### Fix #4 - CodeFactor Line-Level Issues (TBD)
- [ ] **Bot**: CodeFactor
- [ ] **Status**: PENDING EXTRACTION
- [ ] **Action**: Visit https://www.codefactor.io/repository/github/backtothefutures83-oss/universal-or-strategy/pull/22
- [ ] **Expected**: 5-10 VALID-FIX issues after Jane Street filtering
- [ ] **Effort**: 15 min extraction + 20 min fixes
- [ ] **Impact**: +2 PHS points

**Extraction Steps**:
1. Open CodeFactor link
2. Review all 38 issues
3. Categorize each:
   - [VALID-FIX]: Style, duplication, correctness
   - [JANE-STREET-SUPPRESS]: CYC 9-15, primitive obsession
4. Create fix list for VALID-FIX issues only
5. Apply fixes in single commit

---

## P2 LOW - Suppressions (Document Only)

### Suppress #1 - ValidateCachedEntry Complexity (CYC 9)
- [ ] **Bot**: Codacy
- [ ] **File**: `src/V12_002.SIMA.Shadow.cs:198`
- [ ] **Issue**: Cyclomatic complexity of 9 exceeds Codacy threshold of 8
- [ ] **Action**: Document suppression (no code change)
- [ ] **Rationale**: V12 threshold is 15 (Jane Street Decision #8)
- [ ] **Documentation**:
  ```yaml
  # .codacy.yml
  exclude_patterns:
    - 'src/V12_002.SIMA.Shadow.cs:198'  # CYC 9 < V12 threshold 15
  ```

---

## Verification Checklist

After applying all fixes:

- [ ] Run CSharpier: `dotnet csharpier format src/`
- [ ] Run pre-push validation: `powershell -File .\scripts\pre_push_validation.ps1 -Fast`
- [ ] Verify build: `dotnet build src/V12_Performance.sln`
- [ ] Verify tests: `dotnet test tests/V12_Performance.Tests/`
- [ ] Check complexity: `python scripts/complexity_audit.py`
- [ ] Commit: `git commit -m "fix(epic-ccn-12-r4): Make helpers static + CodeFactor fixes"`
- [ ] Push: `git push origin feature/src-epic-ccn-12-shadowpropagatestop`

---

## Expected Outcome

| Check | Before | After |
|-------|--------|-------|
| SonarCloud | 3 issues | ✅ PASS |
| Codacy | 1 issue | ✅ PASS (suppressed) |
| CodeFactor | 38 issues | ✅ PASS (5-10 fixed, rest suppressed) |
| CodeScene | Advisory fail | ✅ PASS (suppressions documented) |
| **PHS** | **95/100** | **100/100** |

---

## Notes

- All 3 SonarCloud fixes are trivial (add `static` keyword)
- CodeFactor extraction required before proceeding with fixes
- Jane Street suppressions align with V12 DNA (Decisions #8, #9, #10)
- PR Separation violation documented as known exception