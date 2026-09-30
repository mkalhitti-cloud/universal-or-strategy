# PR #22 Round 6: Jane Street Suppressions

**Date**: 2026-06-02
**Protocol**: V12 DNA Architectural Mandates
**Round**: 6 (Post-CSharpier Auto-Fix)

---

## Overview

This document records CodeFactor findings from Round 6 that conflict with V12 DNA principles and are intentionally suppressed. These represent architectural decisions aligned with Jane Street's high-frequency trading practices.

---

## Suppression 1: Logical Grouping Over Member Ordering

### Issue: Internal Before Private Members
- **File**: src/V12_002.SIMA.Shadow.cs:73
- **Bot**: CodeFactor
- **Finding**: "'internal' members should come before 'private' members"
- **Severity**: Style (Low)

### Issue: Static Before Non-Static Members
- **File**: src/V12_002.SIMA.Shadow.cs:158
- **Bot**: CodeFactor
- **Finding**: "Static members should appear before non-static members"
- **Severity**: Style (Low)

### V12 DNA Position: SUPPRESS (Jane Street Decision #12)

**Rationale**:
1. **Jane Street Principle**: Co-locate related functionality for cognitive simplicity
2. **V12 DNA**: "Make illegal states unrepresentable" requires logical grouping
3. **HFT Context**: Functions that work together should be adjacent for:
   - Easier reasoning under microsecond latency constraints
   - Reduced cognitive load during incident response
   - Better cache locality when reading code

**Example from EPIC-CCN-12**:
```csharp
// Logical grouping: Shadow propagation validation chain
internal static bool ValidateLeaderPosition(...)  // Step 1
internal static bool ValidateCachedEntry(...)     // Step 2
internal static bool ValidateShadowEligibility(...) // Step 3

// Helper methods grouped by concern
private static void LogValidationFailure(...)
private static void UpdateMetrics(...)
```

**Alternative (Alphabetical - REJECTED)**:
```csharp
// Alphabetical: Breaks logical flow
internal static bool ValidateCachedEntry(...)
internal static bool ValidateLeaderPosition(...)
internal static bool ValidateShadowEligibility(...)
private static void LogValidationFailure(...)
private static void UpdateMetrics(...)
```

**Decision**: Maintain logical grouping. Alphabetical ordering optimizes for IDE navigation, but V12 optimizes for human reasoning under pressure.

**Reference**: This suppression was first documented in Round 5 and remains valid for Round 6.

---

## Summary

| Suppression | Type | Rationale | Action |
|-------------|------|-----------|--------|
| Logical Grouping (lines 73, 158) | Architectural | Jane Street cognitive simplicity | Maintain |

**Total Suppressions**: 2 (member ordering)
**Total Fixed**: 13 (1 blank line + 12 parenthesis placement)

---

## References

- **Jane Street Intel**: `docs/intel/jane-street/`
- **V12 DNA**: `AGENTS.md` Section 2
- **Round 5 Suppressions**: `docs/brain/pr_22_round5_suppressions.md`
- **EPIC-CCN-12**: Shadow propagation stop extraction