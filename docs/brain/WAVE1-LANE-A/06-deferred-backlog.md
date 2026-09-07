# WAVE1-LANE-A Deferred Backlog

**Block**: WAVE1-LANE-A (CopyEngine.cs god-method extraction)
**Date**: 2026-09-07
**Status**: Current block entry — required for PIPELINE_COMPLETE

---

## Deferred Items

| ID | Method | File | Current CCN | Proposed Fix | Priority |
|----|--------|------|------------|--------------|----------|
| DW-W1A-01 | IsExitSignalName | CopyEngine.cs | 9 | Replace sequential if-return chain with HashSet\<string\> _exactExitNames for exact-match checks; reduces CCN 9→7 | HIGH |
| DW-W1A-02 | HasArmingAtmBrackets | CopyEngine.cs | 9 | Extract IsArmingOrderState(OrderState s) to absorb 5-term OR; reduces CCN 9→5 | HIGH |
| DW-W1A-03 | MoveStopToBreakEven | CopyEngine.cs | 7 | Remove ~55 lines of commented-out DW-B88 legacy code (NLOC reduction, no CCN impact) | LOW |
| DW-W1A-04 | SendCopy | CopyEngine.cs | 6 | Remove dead atmTemplate local variable (3 lines; latent JS-002 smell — can be null) | LOW |
| DW-W1A-05 | (process gate) | CopyEngine.cs | N/A | Run ptt-sync-and-verify.ps1 and confirm 0 MISMATCH before merge; press F5 in NT8 | P0 (immediate) |

---

## Why Deferred

- DW-W1A-01 and DW-W1A-02 were discovered during this epic but are NOT in the WAVE1-LANE-A target
  method list. Both require their own ticket, test coverage, and verifier cycle.
- DW-W1A-03 and DW-W1A-04 are advisory cleanup with no active compliance violation.
- DW-W1A-05 is a process gate (sync + F5) to be closed by Director before merging this epic's PR.

---

## Recommended Next Epic

WAVE2-LANE-A: IsExitSignalName + HasArmingAtmBrackets CCN reduction (DW-W1A-01 + DW-W1A-02)
Target: both methods CCN <= 8

IsExitSignalName (line 2347, `internal static bool`):
- Current pattern: sequential `if (name == "X") return true;` chain
- Proposed: `private static readonly HashSet<string> _exactExitNames = new(...); return _exactExitNames.Contains(name);`
- Result: CCN 9 → 3 (single HashSet.Contains call)

HasArmingAtmBrackets (line 5351, `internal static bool`):
- Current pattern: long OR expression across 5 OrderState terms
- Proposed: extract `private static bool IsArmingOrderState(OrderState s)` for the OR clause
- Result: CCN 9 → 5 parent + CCN 5 helper (both compliant)

---

## Work Completed This Block

| Method | CCN Before | CCN After | Delta | Helpers Extracted |
|--------|-----------|-----------|-------|-------------------|
| RegisterBeRetrySlotIfNeeded | 8 | 6 | -2 | IsBeRetrySlotNeeded, RegisterPendingBeSlot (4-param) |
| FlattenOneAccountLimit | 8 | 6 | -2 | SubmitLimitExitOrder (shared) |
| TrimOneAccountLimit | 8 | 6 | -2 | SubmitLimitExitOrder (shared, same helper) |
| OnOrderUpdate | 8 | 5 | -3 | TryResolveEnabledRule |
| **Total CCN reduced** | 32 (sum before) | 23 (sum after) | **-9** | **4 new private helpers** |

Note: FlattenOneAccountLimit and TrimOneAccountLimit share SubmitLimitExitOrder — net 4 unique new helpers
for -9 total CCN points across 4 parent methods. 16 new [Fact] tests added (T31-T46).

---

## CCN Compliance Status After This Block

All 8 modified/extracted methods are JS-080 compliant (CCN <= 8):

| Method | CCN | Status |
|--------|-----|--------|
| IsBeRetrySlotNeeded | 4 | COMPLIANT |
| RegisterPendingBeSlot (4-param) | 1 | COMPLIANT |
| RegisterBeRetrySlotIfNeeded | 6 | COMPLIANT |
| SubmitLimitExitOrder | 3 | COMPLIANT |
| FlattenOneAccountLimit | 6 | COMPLIANT |
| TrimOneAccountLimit | 6 | COMPLIANT |
| TryResolveEnabledRule | 5 | COMPLIANT |
| OnOrderUpdate | 5 | COMPLIANT |

Out-of-scope non-compliant methods (CCN > 8) remaining in CopyEngine.cs:
- IsExitSignalName: CCN=9 (line 2347) → tracked as DW-W1A-01
- HasArmingAtmBrackets: CCN=9 (line 5351) → tracked as DW-W1A-02
