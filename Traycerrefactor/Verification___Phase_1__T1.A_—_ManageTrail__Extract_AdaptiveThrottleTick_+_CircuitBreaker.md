I have the following verification comments after thorough review and exploration of the codebase. Implement the comments by following the instructions in the comments verbatim.

---
The context section for each comment explains the problem and its significance. The fix section defines the scope of changes to make — implement only what the fix describes.

## Comment 1: The adaptive-throttle/circuit-breaker block was not extracted into `ManageTrail_AdaptiveThrottleTick` as required.

### Context
The requested T1.A implementation is effectively missing: `ManageTrailingStops` still begins with `DateTime now = DateTime.Now;` and retains the entire adaptive-throttle, stale-cleanup, and circuit-breaker block inline at `src/V12_002.Trailing.cs` lines 41-78. The required `private void ManageTrail_AdaptiveThrottleTick(out bool shouldExit)` helper is not declared anywhere under `src/`, so the parent does not perform `bool _shouldExit; ManageTrail_AdaptiveThrottleTick(out _shouldExit); if (_shouldExit) return;` before the `activePositions.ToArray()` snapshot. As a result, the user-requested extraction, cyclomatic-complexity reduction, helper LOC/CYC gate, and subsequent-phase integration contract are not satisfied, even though the original runtime behavior remains inline.

### Fix

In `src/V12_002.Trailing.cs`, extract the current lines 41-78 of `ManageTrailingStops` into a new `private void ManageTrail_AdaptiveThrottleTick(out bool shouldExit)` member of the existing `public partial class V12_002 : Strategy` in the same file. Initialize `shouldExit = false;`, preserve the existing statement order byte-for-byte, and replace only the two inline `return;` exits with `shouldExit = true; return;`. Replace the parent preamble with `bool _shouldExit; ManageTrail_AdaptiveThrottleTick(out _shouldExit); if (_shouldExit) return;` immediately before `var positionSnapshot = activePositions.ToArray();`. Do not touch the foreach body, SIMA block, or `ShadowEngineCheck()` placement.

### Referred Files
- c:\WSGTA\universal-or-strategy\src\V12_002.Trailing.cs
---