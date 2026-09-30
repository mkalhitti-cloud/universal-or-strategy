# PR #22 Round 5: Fix Queue (Prioritized)

**Date**: 2026-06-02
**Status**: ✅ ALL FIXES APPLIED

---

## Priority 1: Auto-Fixable (CSharpier)

### ✅ P1.1: Parenthesis Placement (6 issues)
- **Tool**: CSharpier
- **Command**: `dotnet csharpier format src/ tests/`
- **Result**: ✅ Fixed in 91 files (821ms)
- **Files**: src/V12_002.SIMA.Shadow.cs, tests/LogicTests.cs

### ✅ P1.2: Parenthesis Spacing (8 issues)
- **Tool**: CSharpier
- **Command**: `dotnet csharpier format src/ tests/`
- **Result**: ✅ Fixed in 91 files (821ms)
- **Files**: src/V12_002.SIMA.Shadow.cs, tests/LogicTests.cs

### ✅ P1.3: Brace Blank Lines (2 issues)
- **Tool**: CSharpier
- **Command**: `dotnet csharpier format src/ tests/`
- **Result**: ✅ Fixed in 91 files (821ms)
- **Files**: src/V12_002.SIMA.Shadow.cs

---

## Priority 2: Manual Fixes

### ✅ P2.1: Blank Line Before Comment
- **File**: src/V12_002.cs:25
- **Issue**: Single-line comment should be preceded by blank line
- **Fix**: Added blank line before "// EPIC-CCN-12: Enable unit testing of internal helper methods"
- **Result**: ✅ Applied via apply_diff

### ✅ P2.2: String Optimization (char overload)
- **File**: tests/LogicTests.cs:173
- **Issue**: Use 'string.StartsWith(char)' instead of 'string.StartsWith(string)'
- **Fix**: Changed `line.StartsWith("[", StringComparison.Ordinal)` to `line.StartsWith('[')`
- **Result**: ✅ Applied via apply_diff

### ✅ P2.3: String Optimization (char overload)
- **File**: tests/LogicTests.cs:174
- **Issue**: Use 'string.EndsWith(char)' instead of 'string.EndsWith(string)'
- **Fix**: Changed `line.EndsWith("]", StringComparison.Ordinal)` to `line.EndsWith(']')`
- **Result**: ✅ Applied via apply_diff

---

## Priority 3: Jane Street Suppressions (DO NOT FIX)

### ⚠️ P3.1: Member Ordering - Internal Before Private
- **File**: src/V12_002.SIMA.Shadow.cs:73
- **Issue**: 'internal' members should come before 'private' members
- **Decision**: SUPPRESS
- **Rationale**: V12 uses logical grouping over alphabetical ordering (Jane Street principle)
- **Action**: Document in suppressions file

### ⚠️ P3.2: Member Ordering - Static Before Non-Static
- **File**: src/V12_002.SIMA.Shadow.cs:158
- **Issue**: Static members should appear before non-static members
- **Decision**: SUPPRESS
- **Rationale**: V12 uses logical grouping over alphabetical ordering (Jane Street principle)
- **Action**: Document in suppressions file

### ⚠️ P3.3: Class Naming Convention
- **Files**: src/V12_002.cs:50, src/V12_002.SIMA.Shadow.cs:10
- **Issue**: Rename class 'V12_002' to match pascal case (suggest 'V12002')
- **Decision**: SUPPRESS
- **Rationale**: V12_002 is NinjaTrader-mandated class name, cannot change
- **Action**: Document in suppressions file

---

## Priority 4: Stale/Out of Scope (IGNORE)

### ℹ️ P4.1: Field Encapsulation (~45 issues)
- **Files**: src/V12_002.cs (lines 56-81)
- **Issue**: Make this field 'private' and encapsulate it in a 'public' property
- **Decision**: IGNORE
- **Rationale**: Pre-existing technical debt, not introduced in EPIC-CCN-12
- **Protocol**: No Scope Creep Protocol V12.23

### ℹ️ P4.2: LINQ Simplification (2 issues)
- **Files**: src/V12_002.SIMA.Shadow.cs:55, 330
- **Issue**: Loops should be simplified using LINQ methods
- **Decision**: IGNORE
- **Rationale**: Pre-existing code, not modified in EPIC-CCN-12
- **Protocol**: No Scope Creep Protocol V12.23

---

## Execution Summary

| Priority | Category | Count | Status |
|----------|----------|-------|--------|
| P1 | Auto-Fixed (CSharpier) | 16 | ✅ Complete |
| P2 | Manual Fixes | 3 | ✅ Complete |
| P3 | Jane Street Suppressions | 4 | ⚠️ Documented |
| P4 | Stale/Ignored | ~47 | ℹ️ Out of scope |
| **TOTAL** | | **~70** | **✅ Resolved** |

---

## Verification Checklist

- [x] CSharpier executed successfully (91 files, 821ms)
- [x] Manual fixes applied (3 issues)
- [x] Jane Street suppressions documented (4 issues)
- [x] Stale issues categorized (47 issues)
- [ ] Changes pushed to branch
- [ ] Bot re-analysis complete (wait 5 min)
- [ ] PHS score verified (target: 100/100)
- [ ] F5 verification in NinjaTrader