# Epic 2 TDD: High-Complexity Methods (CYC 20-31)

**Strategy**: Manual TDD with F5 verification per ticket
**Target**: 3 methods → CYC ≤ 15
**PR Strategy**: 1 PR for all 3 tickets (single high-risk batch)

## Overview

These are the **highest-risk** methods in the codebase, requiring careful manual TDD treatment:
- CYC 21-31 (critical complexity)
- Core order management and SIMA dispatch logic
- High blast radius if broken

## Ticket Breakdown

### Ticket 01: SIMA Fleet Health Check (HIGH)
**File**: `src/V12_002.SIMA.Fleet.cs`
**Method**: `ShouldSkipFleet_RunHealthCheck`
- **Current CYC**: 29 (ACTUAL, verified via jcodemunch)
- **Target CYC**: ≤8
- **LOC**: 53 (lines 407-459)
- **Risk**: MODERATE - Diagnostic logging only (returns void, no decision path)
- **Estimated sub-methods**: 4-5
  - `ValidateFleetAccountHealth()` - account-level checks
  - `ValidateFleetPositionLimits()` - position size validation
  - `ValidateFleetOrderCapacity()` - order count checks
  - `ValidateFleetRiskMetrics()` - risk threshold validation

**Complexity Drivers**:
- Multiple nested conditionals for health checks
- Account-level validation loops
- Position and order limit checks
- Risk metric calculations

**Jane Street Alignment**:
- ✅ Zero locks (verify with grep -r "lock(" src/)
- ✅ Deterministic behavior (no DateTime.Now, use UTC)
- ✅ Explicit error handling (no silent catches)
- ✅ Cognitive simplicity (CYC ≤15 per function)

---

### Ticket 02: Order Lifecycle Callbacks (HIGH)
**File**: `src/V12_002.Orders.Callbacks.cs`
**Method**: `HandleSecondaryOrderFilled`
- **Current CYC**: 17 (ACTUAL, verified via jcodemunch)
- **Target CYC**: ≤15
- **LOC**: 55 (lines 253-307)
- **Risk**: HIGH - Secondary order fill handling, affects bracket management
- **Estimated sub-methods**: 3-4
  - `ValidateSecondaryFillState()` - state validation
  - `UpdateBracketAfterSecondaryFill()` - bracket state update
  - `PropagateSecondaryFillToFollowers()` - follower notification
  - `RecordSecondaryFillMetrics()` - telemetry

**Complexity Drivers**:
- Order state validation
- Bracket state machine transitions
- Follower propagation logic
- Error handling for partial fills

**Jane Street Alignment**:
- ✅ FSM/Actor pattern (maintain Enqueue model)
- ✅ No locks (atomic state transitions only)
- ✅ ASCII-only (no Unicode in strings)
- ✅ Explicit error paths

---

### Ticket 03: SIMA Dispatch to Photon (CRITICAL - HIGHEST PRIORITY)
**File**: `src/V12_002.SIMA.Dispatch.cs`
**Method**: `Dispatch_PublishMarketBracketToPhoton`
- **Current CYC**: 27 (ACTUAL, verified via jcodemunch - 29% HIGHER than claimed!)
- **Target CYC**: ≤15
- **LOC**: 229 (lines 445-673 - 21% LARGER than claimed!)
- **Risk**: CRITICAL - Core SIMA dispatch, affects all market bracket orders
- **Priority**: HIGHEST (CYC 27 exceeds threshold by 80%)
- **Estimated sub-methods**: 5-6
  - `ValidateMarketBracketRequest()` - pre-dispatch validation
  - `BuildPhotonMarketBracketPayload()` - payload construction
  - `CalculateBracketPricing()` - price calculations
  - `AssignBracketIdentifiers()` - ID generation
  - `PublishToPhotonQueue()` - queue submission
  - `RecordDispatchMetrics()` - telemetry

**Complexity Drivers**:
- Large LOC (189 lines)
- Multiple validation stages
- Payload construction logic
- Price calculation branches
- Queue submission handling

**Jane Street Alignment**:
- ✅ Lock-free queue operations
- ✅ Deterministic pricing (no floating-point drift)
- ✅ Explicit validation (fail-fast on invalid state)
- ✅ Cognitive simplicity (split into focused sub-methods)

---

## Execution Order

**CRITICAL**: Execute in this exact order to minimize risk:

1. **Ticket 01** (ShouldSkipFleet_RunHealthCheck) - FIRST
   - Rationale: Fleet health is a gatekeeper - if broken, all fleet operations fail
   - Must be stable before touching order callbacks or dispatch

2. **Ticket 02** (HandleSecondaryOrderFilled) - SECOND
   - Rationale: Order callbacks depend on stable fleet health
   - Must be stable before touching dispatch logic

3. **Ticket 03** (Dispatch_PublishMarketBracketToPhoton) - LAST
   - Rationale: Dispatch depends on both fleet health and order callbacks
   - Largest LOC, highest refactoring risk

## Success Criteria (Per Ticket)

- [ ] Method CYC ≤15 (verified by `python scripts/complexity_audit.py`)
- [ ] All sub-methods CYC ≤15
- [ ] Zero locks introduced (verified by `grep -r "lock(" src/`)
- [ ] ASCII-only maintained (verified by `python check_ascii.py`)
- [ ] FSM/Actor pattern preserved (no new stateful classes without Enqueue)
- [ ] All tests pass (`powershell -File .\scripts\pre_push_validation.ps1`)
- [ ] F5 verification passed (BUILD_TAG visible in NinjaTrader)
- [ ] `/pr-loop` achieves 100/100 PHS

## PR Strategy

**Single PR for all 3 tickets**:
- Title: `[EPIC2-TDD] Reduce 3 critical methods from CYC 21-31 to ≤15`
- Body: Reference all 3 tickets, include before/after CYC scores
- Labels: `epic2`, `tdd`, `critical-complexity`

**Rationale**: These 3 methods are tightly coupled (fleet → callbacks → dispatch). Testing them together ensures no integration breakage.

## Implementation Notes

[Leave blank for engineer to fill during execution]