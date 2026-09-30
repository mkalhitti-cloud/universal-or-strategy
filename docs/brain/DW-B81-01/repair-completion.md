# DW-B81-01 -- Direct Repair Completion

**Date**: 2026-08-21
**Commit**: e609cfe3
**Test count**: 304 [Fact] (added 3: T_DW_B81_01_01, T_DW_B81_01_02, T_DW_B81_01_03)
**CYC delta**: TryEvictFollowerBeSlot CYC=4 -> CYC=6 (+2 guards)

## Change Applied

### File: src/PropTraderTools/CopyEngine.cs  L1082-1122

Guard expanded in `TryEvictFollowerBeSlot`:
- **Before**: only evicted on `OrderState.Filled` + flat position
- **After**: also evicts on `OrderState.Rejected` when order name is `PTT-BE-Stop`
  - Flat-guard bypassed for Rejected (slot dead even while position open)
  - `_beReplaceAttempts` reset on both Rejected and Filled paths

## Root Cause Fixed

`_pendingFollowerBeSlots` slot persisted after NT8 rejected PTT-BE-Stop
("Buy stop below market"). On next BE press, `TryReplacePttBeBrackets` TryAdd
found existing slot -> returned early -> no bracket placed -> follower got stop-only, no targets.

## SIM Gate Required Before CLOSED Stamp

Director must run 5-cycle QX->BE-ALL SIM test on clean NT8 restart.
Protocol: see specs/002-trade-copier-spec.html section-b81 pipeline priority card.

GREEN: stamp DW-B81-01 CLOSED in spec section-b81.
RED:   open DW-B81-04 with fresh log evidence.

## Status
REPAIR_COMPLETE -- SIM gate pending