# Validated Implementation Plan: Build 951.5 Triage & Repair

This plan has been forensic-verified by Codex 5.4. It addresses three critical P1 defects and one P2 enhancement, with optional safety hardening for REAPER.

## User Review Required

> [!IMPORTANT]
> **FSM Survival Protocol**: If bracket resubmission fails (broker rejection or exception), the strategy will now immediately secure the live follower position using account-level emergency flatten primitives.

## Proposed Changes

### 1. FSM & Callbacks Hardening

**File**: [V12_002.Orders.Callbacks.cs](file:///c:/WSGTA/universal-or-strategy/src/V12_002.Orders.Callbacks.cs)

- **[P1] FSM Failure Fallback**: In `SubmitBracketReplacement`, add emergency protection for:
  - `CreateOrder` returning `null` (Broker rejection).
  - `ExecutingAccount.Submit()` throwing an exception.
  - **Logic**: Use `spec.ExecutingAccount` to close the live position before removing the FSM spec.
- **[P1] Stop-Gap Fill**:
  - In `HandleMatchedFollowerOrder`, if a stop leg fills during the cancel-gap, set a `PositionClosed` flag on the spec.
  - In `SubmitBracketReplacement`, abort all target recreation if `PositionClosed` is true.
- **[P1] Broker-Live Sync**: Before resubmitting targets in `SubmitBracketReplacement`, verify the broker-live position size on `spec.ExecutingAccount` is > 0.
- **[P2] Target Absorption**:
  - Reorder `PropagateMasterTargetMove`. Check `_bracketReplaceSpecs` first.
  - If a spec exists, update its in-flight target prices under `SyncRoot`. Presence of the spec is the ownership proof.

### 2. REAPER Safety (Optional Hardening)

**File**: [V12_002.REAPER.cs](file:///c:/WSGTA/universal-or-strategy/src/V12_002.REAPER.cs)

- **[Consistency] Unified Helper**: Implement `IsBracketMoveInFlight(accountName)` to check both `_bracketReplaceSpecs` and `pendingStopReplacements`. This is non-blocking hardening for master-account consistency.

## Verification Plan

### Automated Verification

- Run `./scripts/audit_scan.ps1` and GitHub Actions audits.

### Forensic Triage Drills

- **FSM Failure Drill**: Force `Submit()` failure and verify immediate account-level flatten of the follower position.
- **Stop-Gap Fill Race**: Simulate a stop fill during the cancel-gap and verify no targets are "orphaned" onto a flat account.
- **Target Absorption Race**: Move a master target while a follower bracket is being replaced; verify the replacement uses the final updated price.
