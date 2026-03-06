# $MISSION: Build 951.5 Triage Drill (Final Handoff)

**Branch**: `build/951-5-triage`
**Baseline**: `ae65ca9773`
**Objective**: Execute surgical repairs for Build 951.5 based on Codex 5.4 validated rules.

---

## 🛠️ Sonnet (Claude) Implementation Prompt

**Role**: Lead Engineer (BMad Team)
**Context**: Execute verified repairs for Build 951.5.

"Execute the Build 951.5 DRILL on branch `build/951-5-triage`. Implement the following surgical fixes based on the validated rules in `implementation_plan_951_5_triage.md` and the Codex 5.4 forensic audit:

1.  **FSM Failure Guard (V12_002.Orders.Callbacks.cs)**:
    - Update `SubmitBracketReplacement`. The fallback must run for BOTH `CreateOrder == null` branches AND the `Submit()` exception branch.
    - **Crucial**: Before calling `_bracketReplaceSpecs.TryRemove(...)` in these error paths, you MUST secure the live account. Do not rely solely on `FlattenPositionByName`. Instead, use `spec.ExecutingAccount` to perform a live-account protection exit (ensure any broker-live position is closed if resubmission fails).

2.  **Stop-Gap Fill Termination (V12_002.Orders.Callbacks.cs)**:
    - In `HandleMatchedFollowerOrder`, if a stop leg fills during the cancel-gap, mark the spec as `PositionClosed`.
    - In `SubmitBracketReplacement`, abort all target recreation if this flag is set. This prevents orphaning targets on a flat account.

3.  **Broker-Live Position Sync (V12_002.Orders.Callbacks.cs)**:
    - Add a final safety check in `SubmitBracketReplacement`: confirming broker-live position exists on `spec.ExecutingAccount` before resubmitting any targets.

4.  **Target Absorption Logic (V12_002.Orders.Callbacks.cs)**:
    - Reorder `PropagateMasterTargetMove` (Lines 1448+). Check `_bracketReplaceSpecs.TryGetValue(...)` first.
    - If a spec exists, update the target price inside the spec (under `SyncRoot`) and RETURN. The FSM owns the replacement; do not touch the state-dicts or working order states.

5.  **REAPER Hardening (Optional, V12_002.REAPER.cs)**:
    - Implement `IsBracketMoveInFlight(accountName)` as a shared helper that checks both `_bracketReplaceSpecs` (Fleet) and `pendingStopReplacements` (Master). Re-use this helper in both audit paths for consistency.

**Standards**:

- NinjaTrader V12 Phase 7.
- ASCII-only in all strings.
- Lock `stateLock` for FSM mutations.
- Deploy via `./deploy-sync.ps1` after build.

Report 'READY FOR FINAL AUDIT' once implemented and deployed."
