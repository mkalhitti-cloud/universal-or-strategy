# Ticket T5: Logic Drift & Thread-Safety Repairs

## Scope & Objective
**Single sentence**: Resolve the TREND E2 fall-through logic drift and the stale snapshot thread-safety risk in the trailing stop module.

**In scope**: file:`src/V12_002.Trailing.cs` only.
**Out of scope**: Modifying actual trailing calculation math or the Fleet Sync logic itself.

## Task Breakdown
- **Task A ([LD-002])**: Fix Logic Drift in `ManageTrailingStops`.
  - **Context**: The `ManageTrail_RunPerTradeBranches` method handles specialized trade branches. Ensure that the TREND E2 branch (around line 150) properly terminates by ensuring it returns `true` (if missing or bypassed). Verify no other specialized branch (E1, RETEST) "falls through" to the point-based generic cascade.
  - **Audit**: Arena AI flagged a "fall-through" where TREND E2 was bypassing its termination, hitting the generic point-based trailing logic.
- **Task B ([LD-003])**: Fix Thread-Safety Stale Snapshot.
  - **Location**: `src/V12_002.Trailing.cs` around line 51.
  - **Before**: `if (EnableSIMA) ManageTrail_RunFleetSymmetrySync(positionSnapshot);`
  - **After**: 
    ```csharp
    // [LD-003] Thread-Safety: Use a fresh snapshot for fleet sync to prevent stale stop synchronization.
    var updatedSnapshot = activePositions.ToArray();
    if (EnableSIMA) ManageTrail_RunFleetSymmetrySync(updatedSnapshot);
    ```

## References
- **Audit Findings**: `docs/brain/prreport_audit_results.md` ([LD-002], [LD-003]).
- **Target Methods**: `ManageTrailingStops()`, `ManageTrail_RunPerTradeBranches()`.

## Guardrails
- **Fresh Snapshot**: The `updatedSnapshot` MUST be captured AFTER the main trailing loop finishes. The second snapshot must be a new `ToArray()` call, not a reference to the first.
- **No Fall-Through**: Verify that no specialized trade branch elides its `return true` statement.
- **Zero Locks**: Do not introduce `lock(stateLock)`.
- **ASCII-ONLY**: Ensure no Unicode markers or emojis are introduced in comments or logs.
- **Role Limits**: The Architect (P3) operates in PLAN-ONLY mode and MUST NOT edit `src/` files. Write the implementation plan for the Engineer (P4).

## Acceptance Criteria
- `ManageTrail_RunPerTradeBranches` correctly returns `true` for all specialized branches (E1, E2, Retest) when condition is met.
- `ManageTrailingStops` uses two distinct `ToArray()` snapshots: one for the `foreach` loop and one for the `ManageTrail_RunFleetSymmetrySync` call.
- `grep -rn "return true" src/V12_002.Trailing.cs` confirms termination for E2.
- `powershell -File .\deploy-sync.ps1` passes with zero ASCII violations.

## Verification Steps
1. `python check_ascii.py` -- verify no BOM or Unicode introduced.
2. Manual Code Review: Confirm `ManageTrail_RunFleetSymmetrySync` is now passed a fresh array from `activePositions`.
3. `powershell -File .\deploy-sync.ps1` -- ensure hard links are updated.
