# PR #9 Round 10 Fix Queue - Cognitive Complexity Resolution

**Date**: 2026-06-01  
**Branch**: `feature/src-phase7-new2-stopsync`  
**Source**: SonarCloud Report (`docs/sonarcloudpr9.md`)

## Root Cause Identified

**CodeScene and SonarCloud use COGNITIVE COMPLEXITY, not Cyclomatic Complexity.**

**Cognitive Complexity** penalizes:
- Nested control structures (if inside if, loop inside loop)
- Break/continue statements
- Recursive calls
- Binary logical operators in conditions

**Cyclomatic Complexity** only counts:
- Decision points (if, for, while, case, &&, ||)

This is why `complexity_audit.py` (CYC) showed 15 but CodeScene (Cognitive) shows 16-29.

## SonarCloud Findings - Cognitive Complexity Violations

| Line | Method | Cognitive | Threshold | Delta | Priority |
|------|--------|-----------|-----------|-------|----------|
| L941 | RestoreCascadedTargets | 29 | 15 | +14 | P0 |
| L37 | RefreshActivePositionOrders | 22 | 15 | +7 | P1 |
| L176 | (8-param method) | 22 | 15 | +7 | P1 |
| L443 | UpdateStopQuantity_HandleEmergencyFlatten | 19 | 15 | +4 | P1 |
| L537 | UpdateStopQuantity | 16 | 15 | +1 | P2 |

## Fix Strategy

### P0: RestoreCascadedTargets (Line 941, Cognitive=29)

**Status**: OUT OF SCOPE for PR #9  
**Rationale**: This method was NOT modified in PR #9 (Phase 7 NEW-2 ticket)  
**Action**: Defer to future EPIC (EPIC-CCN-10: Cognitive Complexity Reduction)

### P1: RefreshActivePositionOrders (Line 37, Cognitive=22)

**Status**: OUT OF SCOPE for PR #9  
**Rationale**: This method was NOT modified in PR #9  
**Action**: Defer to EPIC-CCN-10

### P1: Line 176 Method (Cognitive=22, 8 params)

**Status**: INVESTIGATE  
**Action**: Identify method at line 176, determine if modified in PR #9

### P1: UpdateStopQuantity_HandleEmergencyFlatten (Line 443, Cognitive=19)

**Status**: IN SCOPE - Created in Round 7  
**Action**: Extract nested logic to reduce cognitive load

### P2: UpdateStopQuantity (Line 537, Cognitive=16)

**Status**: IN SCOPE - Primary target of PR #9  
**Action**: Extract one more helper to reduce Cognitive from 16→15

## Scope Decision

**PR #9 Scope**: Phase 7 NEW-2 - UpdateStopQuantity complexity reduction

**Methods Modified in PR #9**:
1. UpdateStopQuantity (line 537) - PRIMARY TARGET
2. UpdateStopQuantity_CancelAndReplace (line 414) - Helper extracted in earlier rounds
3. IsOrderActiveOrPending (line 436) - Helper extracted in Round 7
4. UpdateStopQuantity_HandleEmergencyFlatten (line 443) - Helper extracted in earlier rounds
5. UpdateStopQuantity_CreateReplacement (line 384) - Helper extracted in earlier rounds

**Methods NOT Modified in PR #9**:
- RefreshActivePositionOrders (line 37) - Pre-existing
- RestoreCascadedTargets (line 941) - Pre-existing
- Line 176 method - Unknown (need to identify)

## Recommendation

**Option A: Strict Scope Enforcement (RECOMMENDED)**

Only fix methods that were MODIFIED in PR #9:
- UpdateStopQuantity (Cognitive 16→15) - 1 point reduction needed
- UpdateStopQuantity_HandleEmergencyFlatten (Cognitive 19→15) - 4 points reduction needed

**Rationale**:
- PR #9 scope is UpdateStopQuantity complexity reduction
- Fixing pre-existing methods violates Three-Tier Branch Model
- Pre-existing violations should be addressed in EPIC-CCN-10

**Option B: Suppress CodeScene Check**

Request Director approval to suppress CodeScene for this PR because:
- Primary target (UpdateStopQuantity) achieved Cyclomatic target (CYC 25→15)
- Cognitive Complexity violations are in pre-existing code
- Full Cognitive Complexity reduction requires separate epic

## Next Steps

1. Identify method at line 176
2. Confirm it was NOT modified in PR #9
3. If confirmed, proceed with Option A:
   - Fix UpdateStopQuantity (Cognitive 16→15)
   - Fix UpdateStopQuantity_HandleEmergencyFlatten (Cognitive 19→15)
4. If line 176 was modified, add to fix list
5. Push Round 10 fixes
6. Re-run CodeScene

## Expected Outcome

After Round 10 fixes:
- UpdateStopQuantity: Cognitive 16→15 ✅
- UpdateStopQuantity_HandleEmergencyFlatten: Cognitive 19→15 ✅
- CodeScene: PASS (only checks modified methods)
- Pre-existing violations: Deferred to EPIC-CCN-10