# PR #22 Fix Queue (Jane Street Audited)
Generated: 2026-06-02 19:33:00

## Instructions for v12-engineer

Process these VALID-FIX issues in priority order. Issues conflicting with Jane Street deviations have been moved to `pr_22_suppress_queue.md`.

---

## P0 CRITICAL FIXES

### Fix #1 - [P0] PR Separation Policy Violation
**Bot:** gitar-bot  
**Category:** VALID-FIX (Policy Enforcement)  
**File:** Multiple (src/ + tests/)  
**Issue:** PR mixes `src/` changes with `tests/` project modifications, violating `PR_SEPARATION_ENFORCEMENT` policy

**Root Cause:**
- Modified: `src/V12_002.SIMA.Shadow.cs` (source code)
- Modified: `tests/V12_Performance.Tests/V12_Performance.Tests.csproj` (test project)
- Policy requires separate PRs for src/ vs non-src/ changes

**Action Required:**
1. Split PR into two separate PRs:
   - PR #22a: `src/V12_002.SIMA.Shadow.cs` changes only
   - PR #22b: Test project infrastructure changes only
2. Reference: `docs/protocol/PR_SEPARATION_ENFORCEMENT.md`

**Jane Street Alignment:** ✅ Policy enforcement (not a code quality issue)

---

### Fix #2 - [P0] Missing Using Statement
**Bot:** coderabbitai  
**Category:** VALID-FIX (Compilation)  
**File:** `src/V12_002.SIMA.Shadow.cs`  
**Line:** 33-57  
**Issue:** Missing `System.Collections.Concurrent` using statement for `ConcurrentDictionary`

**Action Required:**
1. Add to top of file: `using System.Collections.Concurrent;`
2. Verify compilation: `dotnet build`

**Jane Street Alignment:** ✅ Correctness (compilation requirement)

---

## P1 HIGH PRIORITY FIXES

### Fix #3 - [P1] ValidateLeaderPosition Instance Field Access
**Bot:** gitar-bot, sourcery-ai  
**Category:** VALID-FIX (DI Pattern Violation)  
**File:** `src/V12_002.SIMA.Shadow.cs`  
**Lines:** 67, 82  
**Issue:** `ValidateLeaderPosition` accesses instance field `stopOrders` directly instead of receiving as parameter

**Root Cause:**
- Other helpers (`ValidateCachedEntry`, `DetectStopPriceChange`) correctly receive dependencies as parameters
- `ValidateLeaderPosition` breaks DI pattern by accessing `this.stopOrders`
- Makes method impossible to unit test in isolation

**Action Required:**
```csharp
// Change signature from:
internal bool ValidateLeaderPosition(
    PositionInfo pos,
    string entryKey,
    out Order leaderStop
)

// To:
internal bool ValidateLeaderPosition(
    PositionInfo pos,
    string entryKey,
    ConcurrentDictionary<string, Order> stopOrders,
    out Order leaderStop
)
```

**Jane Street Alignment:** ✅ Explicit dependencies (Jane Street principle: "Make dependencies visible")

---

### Fix #4 - [P1] Excessive Hot-Path Logging
**Bot:** gemini-code-assist, sourcery-ai  
**Category:** VALID-FIX (Performance)  
**File:** `src/V12_002.SIMA.Shadow.cs`  
**Issue:** All four extracted helpers call `Print()` in hot path, causing log flooding and GC pressure

**Root Cause:**
- `ValidateCachedEntry`, `ValidateLeaderPosition`, `DetectStopPriceChange`, `PropagateStopMove` all call `Print()`
- Called on every bar update in production
- Violates performance mandate for microsecond-latency systems

**Action Required:**
1. Remove `Print()` calls from hot-path helpers
2. Add diagnostic logging only at entry/exit of parent method `ShadowPropagateStopMoves`
3. Alternative: Add `#if DEBUG` guards around `Print()` calls

**Jane Street Alignment:** ✅ Performance (HFT systems require minimal allocations)

---

### Fix #5 - [P1] Placeholder Tests
**Bot:** coderabbitai, codacy-production  
**Category:** VALID-FIX (Testing)  
**File:** `tests/V12_Performance.Tests/ShadowPropagateTests.cs`  
**Issue:** Tests assert local constants instead of verifying extracted helper logic

**Root Cause:**
- Tests claim to verify 17 scenarios but only assert `true == true`
- No actual verification of `ValidateCachedEntry`, `ValidateLeaderPosition`, etc.
- Fails to catch regressions in extracted logic

**Action Required:**
1. Implement actual test logic for each of the 17 scenarios
2. Mock dependencies (`ConcurrentDictionary`, `PositionInfo`)
3. Verify helper return values and side effects
4. Reference: PR description lists 17 test scenarios to implement

**Jane Street Alignment:** ✅ Correctness by construction (tests must verify logic)

---

## REMOVED (Moved to Suppress Queue)

- ~~CodeScene Complex Method Warning~~ → `pr_22_suppress_queue.md` (Jane Street deviation)
- ~~Greptile Trial Limit~~ → INFRA-NOISE (not a code issue)

---

## Summary

| Priority | Count | Category |
|----------|-------|----------|
| P0 | 2 | Policy + Compilation |
| P1 | 3 | DI Pattern + Performance + Testing |
| **Total VALID-FIX** | **5** | **Must fix before merge** |

**Critical Path:** Fix P0 issues first (PR separation + missing using), then P1 issues.

**Jane Street Compliance:** All VALID-FIX issues align with Jane Street principles (correctness, explicit dependencies, performance, testability).
