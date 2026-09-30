# PR #6 SonarCloud Technical Debt Backlog

**Status**: Documented for EPIC-CCN-10 future work  
**Source**: SonarCloud analysis post-PR #6 Iteration 3  
**Date**: 2026-05-31

## Overview
These issues are **pre-existing technical debt** in `V12_002.SIMA.Fleet.cs`, not introduced by PR #6. Per PR Hygiene protocol, they should be addressed in a separate, focused PR to avoid scope creep.

## Issues Identified

### 1. Naming Convention (Low Priority)
**Line**: 34  
**Issue**: "Rename class 'V12_002' to match pascal case naming rules, consider using 'V12002'"  
**Severity**: Low  
**Category**: Convention  
**Decision**: **WONTFIX** - V12_002 naming is a Jane Street Deviation for NinjaTrader compatibility. This is intentional architecture, not a bug.

### 2. Parameter Count (Medium Priority)
**Line**: 44  
**Issue**: "Method has 8 parameters, which is greater than the 7 authorized"  
**Severity**: Medium  
**Category**: Brain-overload  
**Effort**: 20min  
**Recommendation**: Consider parameter object pattern or builder pattern for methods with >7 parameters.

### 3. Cognitive Complexity - Method 1 (High Priority)
**Line**: 120  
**Issue**: "Refactor this method to reduce its Cognitive Complexity from 26 to the 15 allowed"  
**Severity**: Critical  
**Category**: Brain-overload  
**Effort**: 6min  
**Recommendation**: Extract nested logic into helper methods. Target for EPIC-CCN-10.

### 4. Null Reference (Medium Priority)
**Line**: 210  
**Issue**: "'orders' is null on at least one execution path"  
**Severity**: Major  
**Category**: CWE, Symbolic-execution  
**Effort**: 10min  
**Recommendation**: Add null guard or use null-conditional operator.

### 5. Cognitive Complexity - Method 2 (High Priority)
**Line**: 286  
**Issue**: "Refactor this method to reduce its Cognitive Complexity from 18 to the 15 allowed"  
**Severity**: Critical  
**Category**: Brain-overload  
**Effort**: 6min  
**Recommendation**: Extract nested logic into helper methods. Target for EPIC-CCN-10.

### 6. Cognitive Complexity - Method 3 (High Priority)
**Line**: 327  
**Issue**: "Refactor this method to reduce its Cognitive Complexity from 30 to the 15 allowed"  
**Severity**: Critical  
**Category**: Brain-overload  
**Effort**: 6min  
**Recommendation**: Extract nested logic into helper methods. Target for EPIC-CCN-10.

### 7. Nested If Statement (Medium Priority)
**Line**: 423  
**Issue**: "Merge this if statement with the enclosing one"  
**Severity**: Major  
**Category**: Clumsy  
**Effort**: 5min  
**Recommendation**: Combine conditions with `&&` operator.

### 8. Nested If Statement (Medium Priority)
**Line**: 425  
**Issue**: "Merge this if statement with the enclosing one"  
**Severity**: Major  
**Category**: Clumsy  
**Effort**: 5min  
**Recommendation**: Combine conditions with `&&` operator.

### 9. Static Method Suggestion (Low Priority)
**Line**: 514  
**Issue**: "Make 'IsBrokerPositionFlat' a static method"  
**Severity**: Minor  
**Category**: Pitfall  
**Effort**: 5min  
**Recommendation**: Convert to static if no instance state is accessed.

### 10. Loop Simplification (Low Priority)
**Line**: 539  
**Issue**: "Loop should be simplified by calling Select(kvp => kvp.Value))"  
**Severity**: Minor  
**Category**: Style  
**Effort**: 5min  
**Recommendation**: Use LINQ Select for cleaner code.

### 11. Loop Simplification (Low Priority)
**Line**: 565  
**Issue**: "Loop should be simplified by calling Select(kvp => kvp.Value))"  
**Severity**: Minor  
**Category**: Style  
**Effort**: 5min  
**Recommendation**: Use LINQ Select for cleaner code.

### 12. Static Method Suggestion (Low Priority)
**Line**: 579  
**Issue**: "Make 'LogHealthCheckResult' a static method"  
**Severity**: Minor  
**Category**: Pitfall  
**Effort**: 5min  
**Recommendation**: Convert to static if no instance state is accessed.

### 13. Always-True Condition (Medium Priority)
**Line**: 598  
**Issue**: "Change this condition so that it does not always evaluate to 'True'"  
**Severity**: Major  
**Category**: CWE, Redundant  
**Effort**: 10min  
**Recommendation**: Review logic - this could be a bug or dead code.

### 14. Nested Ternary (Medium Priority)
**Line**: 604  
**Issue**: "Extract this nested ternary operation into an independent statement"  
**Severity**: Major  
**Category**: Confusing  
**Effort**: 5min  
**Recommendation**: Replace with if-else for clarity.

### 15. Loop Simplification (Low Priority)
**Line**: 659  
**Issue**: "Loops should be simplified using the 'Where' LINQ method"  
**Severity**: Minor  
**Category**: Style  
**Effort**: 5min  
**Recommendation**: Use LINQ Where for cleaner code.

## Priority Breakdown
- **Critical (3)**: Lines 120, 286, 327 - Cognitive complexity >15
- **Major (5)**: Lines 210, 423, 425, 598, 604 - Logic issues and nested conditions
- **Minor (7)**: Lines 34, 514, 539, 565, 579, 659 - Style and convention

## Recommended Action Plan
1. **EPIC-CCN-10**: Address Critical cognitive complexity issues (lines 120, 286, 327)
2. **Separate PR**: Fix Major logic issues (lines 210, 598) - potential bugs
3. **Style Pass**: Address Minor style issues in bulk cleanup PR
4. **WONTFIX**: Line 34 (V12_002 naming) - Jane Street Deviation

## Notes
- These issues existed **before** PR #6 and are not regressions
- Fixing them in PR #6 would violate PR Hygiene (focused changes only)
- Total estimated effort: ~2 hours for all issues
- Prioritize Critical and Major issues first per Jane Street alignment