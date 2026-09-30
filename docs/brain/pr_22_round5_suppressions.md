# PR #22 Round 5: Jane Street Suppressions

**Date**: 2026-06-02
**Protocol**: V12 DNA Architectural Mandates

---

## Overview

This document records bot findings that conflict with V12 DNA principles and are intentionally suppressed. These are NOT bugs or technical debt - they represent architectural decisions aligned with Jane Street's high-frequency trading practices.

---

## Suppression 1: Logical Grouping Over Alphabetical Ordering

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

### V12 DNA Position: SUPPRESS

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

---

## Suppression 2: NinjaTrader Class Naming Convention

### Issue: Class Name V12_002
- **Files**: 
  - src/V12_002.cs:50
  - src/V12_002.SIMA.Shadow.cs:10
- **Bot**: Codacy
- **Finding**: "Rename class 'V12_002' to match pascal case naming rules, consider using 'V12002'"
- **Severity**: Consistency (Low)

### V12 DNA Position: SUPPRESS

**Rationale**:
1. **NinjaTrader Mandate**: Class name MUST match filename for strategy loading
2. **Hard-Link Integration**: `deploy-sync.ps1` creates hard links to NinjaTrader directories
3. **Breaking Change**: Renaming would break:
   - NinjaTrader strategy loader
   - All existing chart configurations
   - Historical backtest references
   - Production deployment pipeline

**Technical Constraint**:
```
NinjaTrader/bin/Custom/Strategies/V12_002.cs  (hard link)
                                   ^^^^^^^^
                                   Must match class name exactly
```

**Decision**: Suppress. This is a platform constraint, not a code quality issue.

---

## Suppression 3: Complexity Threshold Alignment

### Issue: ValidateCachedEntry CYC 9
- **File**: src/V12_002.SIMA.Shadow.cs
- **Bot**: Codacy (screenshot)
- **Finding**: "Cyclomatic complexity 9 exceeds limit 8"
- **Severity**: Complexity (Medium)

### V12 DNA Position: SUPPRESS (Track as Technical Debt)

**Rationale**:
1. **V12 Threshold**: CYC ≤ 15 (Jane Street aligned)
2. **Codacy Hardcoded Limit**: 8 (too conservative for HFT hot-path co-location)
3. **Current Status**: CYC 9 is within V12 acceptable range
4. **Future Action**: Track in EPIC-CCN-10 backlog for refactoring to CCN 10

**Jane Street Alignment**:
- Jane Street prioritizes **cognitive simplicity** over strict metrics
- CYC 9-13 acceptable when logic is:
  - Single-purpose (no mixed concerns)
  - Testable (clear input/output contracts)
  - Auditable (no hidden state mutations)

**Decision**: Suppress Codacy warning. Monitor in EPIC-CCN-10 for future extraction if complexity grows.

---

## Suppression 4: Field Encapsulation (Pre-Existing Debt)

### Issue: Public Fields Without Properties
- **Files**: src/V12_002.cs (lines 56-81, ~45 fields)
- **Bot**: Codacy
- **Finding**: "Make this field 'private' and encapsulate it in a 'public' property"
- **Severity**: Adaptability (Low)

### V12 DNA Position: IGNORE (Out of Scope)

**Rationale**:
1. **No Scope Creep Protocol V12.23**: One epic = one concern
2. **Pre-Existing Debt**: Not introduced in EPIC-CCN-12
3. **Separate Epic Required**: Field encapsulation is a distinct refactoring effort
4. **Risk**: Mixing concerns in PR #22 would repeat EPIC-13 failure

**Decision**: Ignore for this PR. Create separate epic for field encapsulation if prioritized.

---

## Summary

| Suppression | Type | Rationale | Action |
|-------------|------|-----------|--------|
| Logical Grouping | Architectural | Jane Street cognitive simplicity | Maintain |
| V12_002 Naming | Platform Constraint | NinjaTrader hard requirement | Maintain |
| CYC 9 Threshold | Threshold Alignment | V12 uses 15, Codacy uses 8 | Track in EPIC-CCN-10 |
| Field Encapsulation | Scope Control | Pre-existing, separate epic | Defer |

---

## References

- **Jane Street Intel**: `docs/intel/jane-street/`
- **V12 DNA**: `AGENTS.md` Section 2
- **No Scope Creep Protocol**: `docs/brain/EPIC-13/09-pr12-failure-analysis.md`
- **Complexity Audit**: `scripts/complexity_audit.py`
- **EPIC-CCN-10 Backlog**: Future complexity reduction epic