# B132 LaneB Deferred Backlog

## Block: B132 LaneB (DW-B138 Diagnostic Phase) -- 2026

### DW-B138-FIX: Sub-Phase 2 Fix Pipeline
**Priority**: P1
**Status**: OPEN
**Trigger**: Director provides Output Tab 1 trace from Stop1 drag on SIM leader account
**Action**: ptt-architect reads the trace output to identify the exact drop point, then produces
`LaneB-02-architecture-plan-fix.md` (addendum). Pipeline proceeds through:
  Ph3 (ticket generation) -> Ph3.5 (ticket review) -> Ph4a (engineer) -> Ph4b (verifier) -> Ph5 (final review)
**Estimated scope**: 1-3 line change in CopyEngine.cs
**Most likely change**: `FindFollowerBracketOrder` L2524 -- expand `OrderState.Working`-only
  filter to also accept `OrderState.Accepted` (H1: asymmetry with `IsWorkingBracket` which
  already accepts both Working and Accepted on the leader side)
**Alternative**: `HandleBracketChange` or `IsStopLeg` if trace eliminates H1 (low probability)
**Gate**: FINAL_PASS of Sub-Phase 2 pipeline
**Blocked by**: Director trace (pending)

---

### DW-B132-CLEANUP: Remove Diagnostic Prints
**Priority**: P2
**Status**: OPEN
**Trigger**: Sub-Phase 2 fix is confirmed working in SIM (zero PTT-STP-Drag silence on follower)
**Action**:
  1. Set `_diagnosticMode = false` in CopyEngine.cs
  2. Verify zero [TP1-OOU], [TP2-DRAG], [TP3-HBC], [TP4-SFB] output in one SIM session
  3. Delete the following artifacts from CopyEngine.cs:
     - `_diagnosticMode` field declaration (L409-412)
     - `TryLogDragTrace` method body (L1743-1756)
     - `TryLogDragTrace(e.Order)` call site in OnOrderUpdate (L1305)
     - `TryLogSFBTrace` method body (L1758-1776)
     - `TryLogSFBTrace(...)` call site in SyncFollowerBracket (L2188)
     - Inline `if (_diagnosticMode)` guard in TryHandleBracketDrag (L1728-1734)
     - Inline `if (_diagnosticMode)` guard in HandleBracketChange (L2488-2496)
  4. Run SCAN-06 to confirm all methods return to pre-diagnostic CYC values
     (TryHandleBracketDrag: 4->3; HandleBracketChange: 8->7)
  5. Separate ticket required; must pass BUILD_PASS + VERIFY_PASS + all 7 scans
**Gate**: BUILD_PASS + VERIFY_PASS on cleanup ticket
**Blocked by**: DW-B138-FIX confirmed working

---

### H3-DEBT: IsStopLeg ATM Stop1/Stop2/Stop3 Technical Debt
**Priority**: P2
**Status**: OPEN
**Trigger**: Next wave or ad-hoc code review
**Description**: `IsStopLeg` (CopyEngine.cs L3836-3844) currently recognizes:
  - Orders with non-null `FromEntrySignal`
  - Orders whose Name starts with "Stop" (covers ATM Stop1/Stop2/Stop3)
  - Orders whose Name ends with "STP" (case-insensitive; covers "Buy STP", "Sell STP")
  This method was ELIMINATED as the root cause for DW-B138 (source analysis confirmed
  ATM Stop1/2/3 correctly returns true). However, if future ATM strategies use naming
  patterns outside these three patterns, IsStopLeg would silently return false.
**Action**: Verify IsStopLeg handles any new ATM stop naming pattern encountered.
  If a new pattern is found, add a new branch and a corresponding [Fact] test.
**Note**: NOT a confirmed blocking bug -- investigation only. Low urgency.
**Gate**: Code review sign-off or failing [Fact] test confirming a new pattern gap
