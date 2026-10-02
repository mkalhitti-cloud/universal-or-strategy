# B184 Deferred Backlog
## ptt-plan-reviewer Phase 5

**Date**: 2026-07-08
**Author**: ptt-plan-reviewer (Phase 5)
**Epic**: B184 -- Restore CopyEngine.cs BE Event Infrastructure + Full ATR Removal
**Status**: OPEN items awaiting Director triage

---

## Purpose

This document records all deferred work items identified during B184 or carried forward from B183
that remain unresolved. Items from B183 that were closed by B184 are listed with CLOSED status
for audit trail completeness.

**P0 = Compile-blocking or P0 DNA violation. Must be resolved before shipping.**
**P1 = Architecture risk. Must be resolved before next major feature wave.**
**P2 = Quality / testing gap. Track and address in near-term pipeline.**
**P3 = Low-risk cleanup / informational. Address when convenient.**

---

## B183 Items Closed by B184

The following P0 items from docs/brain/B183/06-deferred-backlog.md are now CLOSED.

| DW ID | Description | Closed By |
|-------|-------------|-----------|
| DW-B183-06 | PendingBeArmed event absent from CopyEngine.cs | B184-T1 (cdcf2f95) |
| DW-B183-07 | GlobalBeAllDisarmed event absent from CopyEngine.cs | B184-T1 (cdcf2f95) |
| DW-B183-08 | DisarmPendingBe method absent from CopyEngine.cs | B184-T1 (cdcf2f95) |
| DW-B183-09 | IsPendingSlotsEmpty + RaiseBeAllDisarmed absent from CopyEngine.cs | B184-T1 (cdcf2f95) |

---

## B184 New Deferred Items

### DW-B184-01: DispatchCopy CYC=8 AT BUDGET -- Frozen

```
Priority: P1
Description: DispatchCopy in CopyEngine.cs was measured at CYC=8 by the T2 verifier after
  ATR removal (independent count: 7 decision points + base = 8). Engineer estimated ~7 but
  verifier count is 8. CYC is AT the JS-066 ceiling. FROZEN: any branch addition (if/else, ||,
  &&) to DispatchCopy without a prior helper extraction is an automatic P1 JS-066 violation.
  DW-B183-10 is superseded by this entry (B183 measured CYC=8 before T2; T2 did not reduce it
  below the ceiling per independent verifier measurement).
  Future engineers modifying DispatchCopy MUST extract a helper method first.
  Suggested extraction: the inner ShouldSkipFollowerDispatch/ShouldSkipForReversalGuard
  compound condition (lines ~2482-2488) is a candidate helper pair.
Blocking: No (current CYC is at limit, not over it; blocks only future branch additions)
Target Block: B185 or whenever DispatchCopy is next modified
```

### DW-B184-02: Click-Trader Qty Hardcoded to 1 After ATR Removal

```
Priority: P2
Description: In TradeCopierPanel.cs, the click-trader order handler previously called
  CopyEngine.Instance.GetSuggestedQty(_instrument) to determine order quantity. After T2
  removed GetSuggestedQty (ATR-only method), the T2 engineer replaced this with the literal
  value 1. This is functionally correct as a minimal safe fallback, but it removes any
  dynamic quantity suggestion capability from click-trader. If the Director decides to add
  non-ATR quantity suggestion logic (e.g., user-configured default qty, account-based sizing),
  this must be done in a future ticket.
  Current behavior: click-trader always submits qty=1 regardless of instrument or account.
Blocking: No
Target Block: B185+
```

### DW-B184-03: No xUnit Test Project -- 14 Planned Tests Unimplemented

```
Priority: P2
Description: No xUnit test project exists for CopyEngine or TradeCopierPanel as of B184.
  The following tests were specified in B184 tickets but could not be implemented:
  T1 tests (6):
    BeState_Enum_HasIdle_Armed_Connected_Values
    PendingBeArmed_Event_IsActionStringString_Delegate
    GlobalBeAllDisarmed_Event_IsAction_Delegate
    IsPendingSlotsEmpty_ReturnsTrue_WhenNoPendingSlots
    DisarmPendingBe_NullLeader_DoesNotThrow
    RaiseBeAllDisarmed_FiresGlobalBeAllDisarmed_Event
  T2 tests (8):
    FeatureFlags_Starter_HasSixParameters_NoAtrSizing
    FeatureFlags_Elite_HasSixParameters_NoAtrSizing
    FeatureFlags_Pro_HasSixParameters_NoAtrSizing
    FeatureFlags_Record_DoesNotHaveAtrSizingProperty
    CopyEngine_DoesNotHaveAtrEngine_Field
    CopyEngine_DoesNotHave_SetAtrEngine_Method
    CopyEngine_DoesNotHave_GetSuggestedQty_Method
    CopyEngine_DoesNotHave_ResolveBaseQty_Method
  All 14 tests are testable without NT8 stubs (enum values, event types, record properties,
  reflection-based absence checks). A dedicated test infrastructure epic is recommended.
Blocking: No (test project can be created independently)
Target Block: B185+ (test infrastructure epic)
```

### DW-B184-04: CS0067 Warning -- PendingBeArmed Event Reported as Never Used

```
Priority: P3
Description: dotnet build emits CS0067 "The event 'CopyEngine.PendingBeArmed' is never used."
  This is a Roslyn false positive. The event IS subscribed by TradeCopierPanel.cs at lines
  771-772 and is raised via RaiseBeAllDisarmed -> GlobalBeAllDisarmed?.Invoke() pattern.
  The CS0067 fires because Roslyn does not track event usage through external subscribers
  in a separate class. No functional defect. Warning adds noise to build output.
  Potential fix: suppress with #pragma warning disable CS0067 on the event declaration, or
  add an internal fire method that Roslyn can track. Low priority.
Blocking: No
Target Block: future (when build warning hygiene is addressed)
```

---

## B183 Items Carrying Forward (Remain Open)

The following items from docs/brain/B183/06-deferred-backlog.md were not addressed in B184.
They are listed here for continuity. The authoritative record remains in B183 backlog.

### DW-B183-10 (superseded by DW-B184-01)

DispatchCopy CYC=8 freeze is superseded by DW-B184-01 above, which reflects the post-T2
measurement. The B183 entry remains in docs/brain/B183/06-deferred-backlog.md for audit trail.
Current state per B184 T2 verifier: CYC=8 (at ceiling, not over).

### DW-B183-01: xUnit tests for Slate Gate methods

Reference: docs/brain/B183/06-deferred-backlog.md
Status: OPEN -- no test project exists; see DW-B184-03 above.
Target Block: B185+

### DW-B183-02: xUnit test for FlatGuardLoop

Reference: docs/brain/B183/06-deferred-backlog.md
Status: OPEN -- no test project exists; see DW-B184-03 above.
Target Block: B185+

### DW-B183-03: IsLaneEEligible CYC=8 frozen

Reference: docs/brain/B183/06-deferred-backlog.md
Status: OPEN -- IsLaneEEligible not modified in B184.
Target Block: future (when Lane E is modified)

### DW-B183-04: _laneDDispatched unbounded growth

Reference: docs/brain/B183/06-deferred-backlog.md
Status: OPEN -- not addressed in B184.
Target Block: B185

### DW-B183-05: RULES_CATALOG.md truncated at JS-041

Reference: docs/brain/B183/06-deferred-backlog.md
Status: OPEN -- catalog file still contains only JS-001 through JS-041 on disk.
Rules JS-042 through JS-110 (including JS-066 CYC and JS-081 ASCII) are only enforced
via the reviewer's hardcoded DNA block and workspace rule 06-rules-catalog-gate.md.
Target Block: ops (non-blocking -- fix in parallel with any block)

---

## Summary by Priority

| Priority | Count | Items |
|----------|-------|-------|
| P1 | 3 | DW-B184-01, DW-B183-03, DW-B183-05 |
| P2 | 4 | DW-B184-02, DW-B184-03, DW-B183-01, DW-B183-02 |
| P3 | 2 | DW-B184-04, DW-B183-04 |

**Total open items**: 9
**Items closed by B184**: 4 (DW-B183-06, -07, -08, -09)

---

*Written by: ptt-plan-reviewer (Phase 5) -- 2026-07-08*
*Epic: B184*
*Required for FINAL_PASS gate (role definition mandate).*
