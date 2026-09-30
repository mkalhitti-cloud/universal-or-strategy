# B75-LaneA Tickets — xUnit Test Stubs
**Block**: B75-LaneA
**Epic**: B75 Lane A (CopyEngine.cs — core dispatch engine)
**Phase**: Phase 4 — Ticket Generation
**Author**: ptt-architect
**Date**: 2026-08-17
**Plan Reference**: `docs/brain/B75-LaneA/02-architecture-plan.md` (REVIEW_PASS)
**Audit Reference**: `docs/brain/B75-LaneA/03-dna-audit.md` (DNA_PASS — Round 3)
**Repair Log Reference**: `docs/brain/NO-PIPELINE-REPAIRS.md` (B63–B67-ATM-OBJ sections)
**Status**: TICKETS_COMPLETE

---

## Spec Requirement IDs Satisfied

| Req ID | Source | Description |
|--------|--------|-------------|
| B63-FLATTEN-01 | Repair log B63 | Gate 2.5 PTT- prefix guard in TryDispatchLeaderFlat |
| B63-COPY-CANCEL-01 | Repair log B63 | ATM bracket guard in B56 cancel block |
| B64-ENTRY-FLATTEN-01 | Repair log B64 | Gate 2.6 "Entry" guard in TryDispatchLeaderFlat |
| B65-GATE-C-01 | Repair log B65 | Filled==0 gate in Gate C (IsDispatchTriggerState) |
| B66-COPY-REPLACE | Repair log B66 | IsPttEntryOrderCancelTrigger + HasWorkingPttCopy |
| B66-NATIVE-ATM | Repair log B66 | IsExitSignalName Named routing + SendCopyWithAtm |
| B67-ENTRY-UNBLOCK | Repair log B67 | "Entry" removed from IsExitSignalName |
| B67-CLONE-DRAG | Repair log B67 | FindFollowerEntryOrder name guard widened |
| B66-ATM-OBJ | Repair log B66 | _cloneAtmObject two-cache design |
| B67-CHECKBOX-RESTORE | Repair log B67 | GetSavedFollowerNames (CopyEngine side) |
| G.1 | Plan Section G.1 | IsBeDisarmCandidate extraction |
| G.2 | Plan Section G.2 | IsNonFlatDispatchName (covers IsNonFlatDispatchName) |

---

## Traceability Table

| Test ID | Method Under Test | Plan Section | Repair Log Entry |
|---------|------------------|--------------|-----------------|
| T_B63_01..06 | TryDispatchLeaderFlat | Section B / HOTFIX-B63-FLATTEN-01 | B63-FLATTEN-01 |
| T_B63C_01..05 | IsAtmBracketName | Section B / HOTFIX-B63-COPY-CANCEL-01 | B63-COPY-CANCEL-01 |
| T_B64E_01..05 | TryDispatchLeaderFlat / IsNonFlatDispatchName | Section B / HOTFIX-B64-ENTRY-FLATTEN-01 | B64-ENTRY-FLATTEN-01 |
| T_B65G_01..05 | IsDispatchTriggerState / IsNonFlatDispatchName | Section B / HOTFIX-B65-GATE-C-FILL-GUARD-01 | B65-GATE-C-01 |
| T_B66R_01..09 | IsPttEntryOrderCancelTrigger / HasWorkingPttCopy | Section D | B66-COPY-REPLACE |
| T_B66N_01..06 | IsExitSignalName | Section B / HOTFIX-B66-NATIVE-ATM | B66-NATIVE-ATM |
| T_B67E_01..05 | IsExitSignalName / IsNativeExitName | Section B / HOTFIX-B67-ENTRY-UNBLOCK | B67-ENTRY-UNBLOCK |
| T_CLONE_01..04 | GetCloneAtmMode | Section C | B67-CLONE-DRAG |
| T_B66OBJ_01..05 | SetCloneAtmObjectCache / GetCloneAtmMode / ParseAtmModeName / AtmModeToString | Section C | B66-ATM-OBJ |
| T_B67_04..05 | GetSavedFollowerNames | Section B / HOTFIX-B67-CHECKBOX-RESTORE | B67-CHECKBOX-RESTORE |
| T_CYC_01..08 | IsBeDisarmCandidate / IsNonFlatDispatchName | Section G.1 / G.2 | Plan arch only |

---

## File Path in Wave Workspace

`src/PropTraderTools/TradeCopierPanelB75Tests.cs`
(already present as untracked file per git status — engineer appends to or replaces stubs in this file)

---

## 7-Scan Engineer Checklist

Before marking any ticket DONE, engineer MUST verify all 7 scans pass against
`src/PropTraderTools/CopyEngine.cs` and `src/PropTraderTools/TradeCopierPanelB75Tests.cs`:

```
SCAN-01: lock() = 0
  Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" |
    Where-Object { $_.Line -notmatch "//" }
  Expected: 0 results

SCAN-02: async void = 0
  Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+\("
  Expected: 0 results

SCAN-03: throw new = 0
  Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw\s+new\s+\w+Exception"
  Expected: 0 results

SCAN-04: volatile double/float = 0 (live declarations, not comments)
  Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "volatile\s+(double|float)"
  Expected: hits are comment-only (lines 115, 203); no live declarations

SCAN-05: DIAG-Cancel = 0
  Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "DIAG-Cancel"
  Expected: 0 results

SCAN-06: non-ASCII new introductions = 0
  git diff HEAD -- src/PropTraderTools/CopyEngine.cs |
    Select-String "^\+" | Select-String "[^\x00-\x7F]"
  Expected: 0 results (pre-existing lines 202,203,493,697,1856,1857 are NOT newly introduced)

SCAN-07: CYC annotations correct — verify these post-extraction CYC values match audit Round 3:
  OnOrderUpdate             CYC=8  (at-limit, PASS)
  TryFireFollowerBeDisarm   CYC=5  (PASS)
  TryDispatchLeaderFlat     CYC=7  (PASS)
  IsBeDisarmCandidate       CYC=5  (PASS)
  TryHandleDrag             CYC=3  (PASS)
  IsPttEntryOrderCancelTrigger CYC=4  (PASS, at-limit)
  IsNonFlatDispatchName     CYC=3  (PASS)
```

---

## Test Stubs

### HOTFIX-B63-FLATTEN-01 — TryDispatchLeaderFlat gate 2.5 PTT- prefix guard

```csharp
[Fact]
public void T_B63_01_TryDispatchLeaderFlat_PttQxT2Name_LeaderFlat_ReturnsFalse()
{
    // Arrange: leader account has no open position on instrument.
    //          orderName = "PTT-QX-T2", state = OrderState.Filled.
    //          Gate (2.5): name.StartsWith("PTT-") returns true.
    // Act: call TryDispatchLeaderFlat with the above parameters.
    // Assert: returns false -- PTT-owned order must never trigger follower flatten.
}

[Fact]
public void T_B63_02_TryDispatchLeaderFlat_PttFlattenName_LeaderFlat_ReturnsFalse()
{
    // Arrange: leader account has no open position on instrument.
    //          orderName = "PTT-Flatten", state = OrderState.Filled.
    //          Gate (2.5): name.StartsWith("PTT-") returns true.
    // Act: call TryDispatchLeaderFlat.
    // Assert: returns false -- PTT- prefix gate fires regardless of suffix.
}

[Fact]
public void T_B63_03_TryDispatchLeaderFlat_PttCopyName_LeaderFlat_ReturnsFalse()
{
    // Arrange: leader account has no open position on instrument.
    //          orderName = "PTT-Copy", state = OrderState.Filled.
    //          Gate (2.5): name.StartsWith("PTT-") returns true.
    // Act: call TryDispatchLeaderFlat.
    // Assert: returns false -- PTT-Copy is a follower order; must never flatten.
}

[Fact]
public void T_B63_04_TryDispatchLeaderFlat_CloseName_LeaderFlat_ReturnsTrue()
{
    // Arrange: leader account has no open position on instrument.
    //          orderName = "Close", state = OrderState.Filled.
    //          Gate (2.5): "Close" does not start with "PTT-" -- gate passes.
    //          Gate (2.6): "Close" != "Entry" -- gate passes.
    //          Gate (3): IsNativeExitName("Close") = true -- no position check needed.
    //          Follower accounts exist in rule.
    // Act: call TryDispatchLeaderFlat.
    // Assert: returns true -- native exit signal fires follower flatten.
}

[Fact]
public void T_B63_05_TryDispatchLeaderFlat_CloseName_LeaderHasPosition_ReturnsFalse()
{
    // Arrange: leader account HAS an open position on instrument.
    //          orderName = "Close", state = OrderState.Filled.
    //          Gate (3): IsNativeExitName("Close") = true, but hasOpenPosition = true
    //                    so the condition !IsNativeExitName && hasOpenPosition does not block.
    //          Wait -- "Close" is a native exit; gate (3) is:
    //            if (!IsNativeExitName(name) && hasOpenPosition) return false;
    //          Since IsNativeExitName("Close")=true, the gate does NOT fire.
    //          Follower flatten proceeds (returns true) even when leader has position.
    //          Note: this test documents the INTENDED behavior -- Close always flattens,
    //          position state is irrelevant for native exit names.
    // Act: call TryDispatchLeaderFlat with orderName="Close", hasOpenPosition=true.
    // Assert: returns true -- Close bypasses position guard.
    //
    // Correction note for reviewer: original task brief says returns false (gate 3).
    // The plan Section E gate ordering shows gate (3) only blocks NON-native-exit names
    // when leader has position. "Close" is native -- it always passes gate (3).
    // If the live source returns false here, that would be a regression.
    // Engineer must confirm live behavior and adjust assertion accordingly.
}

[Fact]
public void T_B63_06_TryDispatchLeaderFlat_NullName_LeaderFlat_PassesPttGuard()
{
    // Arrange: leader account has no open position on instrument.
    //          orderName = null, state = OrderState.Filled.
    //          Gate (2.5): IsNonFlatDispatchName(null) = false -- null passes PTT guard.
    //          Gate (2.6): already covered by IsNonFlatDispatchName which returns false for null.
    //          Gate (3): IsNativeExitName(null) -- null is not a native exit name;
    //                    hasOpenPosition = false, so gate (3) passes.
    //          Follower flatten fires.
    // Act: call TryDispatchLeaderFlat with orderName=null, hasOpenPosition=false.
    // Assert: returns true -- null order name falls through all guards, triggers flatten.
}
```

---

### HOTFIX-B63-COPY-CANCEL-01 — IsAtmBracketName ATM bracket guard

```csharp
[Fact]
public void T_B63C_01_IsAtmBracketName_Stop1_ReturnsTrue()
{
    // Arrange: name = "Stop1".
    // Act: call IsAtmBracketName("Stop1").
    // Assert: returns true -- "Stop1" is an NT8 ATM bracket order name (Stop + digit suffix).
}

[Fact]
public void T_B63C_02_IsAtmBracketName_Target3_ReturnsTrue()
{
    // Arrange: name = "Target3".
    // Act: call IsAtmBracketName("Target3").
    // Assert: returns true -- "Target3" is an NT8 ATM bracket order name (Target + digit suffix).
}

[Fact]
public void T_B63C_03_IsAtmBracketName_Entry_ReturnsFalse()
{
    // Arrange: name = "Entry".
    // Act: call IsAtmBracketName("Entry").
    // Assert: returns false -- "Entry" is the ATM entry order, not a bracket leg.
    //         The cancel-block guard must NOT return early for "Entry" -- it must fall
    //         through to the ReplaceFollowerCopyOnAtmCancel path.
}

[Fact]
public void T_B63C_04_IsAtmBracketName_PttCopy_ReturnsFalse()
{
    // Arrange: name = "PTT-Copy".
    // Act: call IsAtmBracketName("PTT-Copy").
    // Assert: returns false -- "PTT-Copy" is a follower copy order, not an ATM bracket leg.
}

[Fact]
public void T_B63C_05_IsAtmBracketName_Stop10_ReturnsTrue()
{
    // Arrange: name = "Stop10".
    // Act: call IsAtmBracketName("Stop10").
    // Assert: returns true -- "Stop10" has a digit at position 4 and beyond.
    //         The predicate should match multi-digit suffix (conservative/correct behavior).
    //         If the implementation only checks char[4] for a digit and char[4]='1',
    //         the result may be true due to the first-digit test. Engineer must verify
    //         the implementation handles 2-digit suffixes correctly.
}
```

---

### HOTFIX-B64-ENTRY-FLATTEN-01 — Gate 2.6 "Entry" guard in TryDispatchLeaderFlat

```csharp
[Fact]
public void T_B64E_01_TryDispatchLeaderFlat_EntryName_NoPosition_ReturnsFalse()
{
    // Arrange: leader account has NO open position on instrument.
    //          orderName = "Entry", state = OrderState.Filled.
    //          Gate (2.6): IsNonFlatDispatchName("Entry") = true -- fires immediately.
    // Act: call TryDispatchLeaderFlat.
    // Assert: returns false -- "Entry" fill must never trigger follower flatten
    //         (NT8 does not update position model until next OnBarUpdate after fill).
}

[Fact]
public void T_B64E_02_TryDispatchLeaderFlat_EntryName_OpenPosition_ReturnsFalse()
{
    // Arrange: leader account HAS an open position on instrument.
    //          orderName = "Entry", state = OrderState.Filled.
    //          Gate (2.6): IsNonFlatDispatchName("Entry") = true -- fires regardless of position.
    // Act: call TryDispatchLeaderFlat.
    // Assert: returns false -- same guard fires; open-position state is irrelevant for "Entry".
}

[Fact]
public void T_B64E_03_TryDispatchLeaderFlat_CloseName_NoPosition_ReturnsTrue_Regression()
{
    // Arrange: leader account has NO open position on instrument.
    //          orderName = "Close", state = OrderState.Filled.
    //          Gate (2.5): "Close" does not start with "PTT-" -- passes.
    //          Gate (2.6): IsNonFlatDispatchName("Close") = false -- passes.
    //          Gate (3): IsNativeExitName("Close")=true, hasOpenPosition=false -- passes.
    //          Follower flatten fires.
    // Act: call TryDispatchLeaderFlat.
    // Assert: returns true -- regression test confirming "Close" still works after B64 guard.
}

[Fact]
public void T_B64E_04_TryDispatchLeaderFlat_CloseName_OpenPosition_Behavior()
{
    // Arrange: leader account HAS an open position on instrument.
    //          orderName = "Close", state = OrderState.Filled.
    //          Gate (2.5): "Close" does not start with "PTT-" -- passes.
    //          Gate (2.6): IsNonFlatDispatchName("Close") = false -- passes.
    //          Gate (3): IsNativeExitName("Close")=true -- gate (3) condition is
    //                    !IsNativeExitName && hasOpenPosition; since IsNativeExitName=true,
    //                    the gate does NOT fire. Flatten still proceeds.
    // Act: call TryDispatchLeaderFlat with orderName="Close", hasOpenPosition=true.
    // Assert: returns true -- native exit bypasses position guard (documented behavior).
}

[Fact]
public void T_B64E_05_IsNonFlatDispatchName_Entry_ReturnsTrue()
{
    // Arrange: orderName = "Entry".
    // Act: call IsNonFlatDispatchName("Entry").
    // Assert: returns true -- "Entry" is in the blocked list (covers gate 2.6 via helper).
    //         Per plan Section G.2 and audit Round 3: IsNonFlatDispatchName CYC=3,
    //         handles both PTT-prefix and literal "Entry".
}
```

---

### HOTFIX-B65-GATE-C-FILL-GUARD-01 — Filled==0 gate in Gate C (IsDispatchTriggerState)

```csharp
[Fact]
public void T_B65G_01_IsDispatchTriggerState_LimitAccepted_ReturnsTrue()
{
    // Arrange: orderType = OrderType.Limit, orderState = OrderState.Accepted.
    //          Gate C outer condition: (Limit||StopLimit) && (Accepted||Working) && Filled==0.
    //          Limit+Accepted+Filled==0 = all three conditions satisfied.
    // Act: call IsDispatchTriggerState(OrderState.Accepted, OrderType.Limit, filled: 0).
    // Assert: returns true -- Limit order accepted with no fill is a dispatch trigger.
}

[Fact]
public void T_B65G_02_IsDispatchTriggerState_LimitWorking_ReturnsFalse()
{
    // Arrange: orderType = OrderType.Limit, orderState = OrderState.Working.
    //          Note: per plan Section B HOTFIX-B65, the Filled==0 guard was added to
    //          prevent mid-fill duplicate dispatch. However Working+Limit was a valid
    //          trigger BEFORE this hotfix. Post-hotfix behavior: Working is still accepted
    //          by Gate C (Accepted||Working covers both). Only Filled>0 blocks it.
    //          This test uses Filled=1 to verify the hotfix blocks correctly.
    // Act: call IsDispatchTriggerState(OrderState.Working, OrderType.Limit, filled: 1).
    // Assert: returns false -- Working Limit order with a partial fill (Filled>0) is NOT
    //         a dispatch trigger (HOTFIX-B65 guard fires).
}

[Fact]
public void T_B65G_03_IsDispatchTriggerState_MarketSubmitted_ReturnsTrue()
{
    // Arrange: orderType = OrderType.Market, orderState = OrderState.Submitted, filled=0.
    //          Note: Market orders follow a different path in Gate C (not Limit/StopLimit).
    //          This test validates the pre-Gate-C routing for Market orders.
    // Act: call IsDispatchTriggerState(OrderState.Submitted, OrderType.Market, filled: 0).
    // Assert: returns true -- Market+Submitted+Filled==0 triggers dispatch (if implemented).
    //         If IsDispatchTriggerState only handles Limit/StopLimit, this test
    //         should assert false and the engineer must note Market dispatch path is separate.
}

[Fact]
public void T_B65G_04_IsDispatchTriggerState_MarketAccepted_ReturnsFalse()
{
    // Arrange: orderType = OrderType.Market, orderState = OrderState.Accepted, filled=1.
    //          Per the plan note: "Market Accepted has numeric ID = not dispatch".
    //          With Filled>0, HOTFIX-B65 guard fires.
    // Act: call IsDispatchTriggerState(OrderState.Accepted, OrderType.Market, filled: 1).
    // Assert: returns false -- partial-fill Accepted Market order must not retrigger dispatch.
}

[Fact]
public void T_B65G_05_IsNonFlatDispatchName_PttQxT1_ReturnsTrue()
{
    // Arrange: orderName = "PTT-QX-T1".
    // Act: call IsNonFlatDispatchName("PTT-QX-T1").
    // Assert: returns true -- PTT-prefix check fires (covers gate 2.5 via the refactored helper).
    //         Per plan Section G.2: IsNonFlatDispatchName handles both PTT-prefix and "Entry".
}
```

---

### HOTFIX-B66-COPY-REPLACE — IsPttEntryOrderCancelTrigger + HasWorkingPttCopy

```csharp
[Fact]
public void T_B66R_01_IsPttEntryOrderCancelTrigger_NullOrder_ReturnsFalse()
{
    // Arrange: order = null.
    // Act: call IsPttEntryOrderCancelTrigger(null).
    // Assert: returns false -- null order is not a cancel trigger.
    //         Per audit Round 3: IsPttEntryOrderCancelTrigger CYC=4,
    //         first guard is null check.
}

[Fact]
public void T_B66R_02_IsPttEntryOrderCancelTrigger_NotCancelled_ReturnsFalse()
{
    // Arrange: order is non-null, OrderState = OrderState.Filled (not Cancelled).
    //          Name = "PTT-Copy", LimitPrice = 5.0.
    // Act: call IsPttEntryOrderCancelTrigger(order).
    // Assert: returns false -- order must be in Cancelled state to trigger re-placement.
}

[Fact]
public void T_B66R_03_IsPttEntryOrderCancelTrigger_CancelledEntryNoPrice_ReturnsFalse()
{
    // Arrange: order.OrderState = OrderState.Cancelled.
    //          order.Name = "Entry", order.LimitPrice = 0.
    //          Instrument.FullName is non-null.
    // Act: call IsPttEntryOrderCancelTrigger(order).
    // Assert: returns false -- LimitPrice = 0 means no valid price to re-place;
    //         market orders should not be re-placed via this path.
}

[Fact]
public void T_B66R_04_IsPttEntryOrderCancelTrigger_CancelledPttCopyWithPrice_ReturnsTrue()
{
    // Arrange: order.OrderState = OrderState.Cancelled.
    //          order.Name = "PTT-Copy", order.LimitPrice = 5050.25.
    //          order.Instrument.FullName = "MES SEP26".
    // Act: call IsPttEntryOrderCancelTrigger(order).
    // Assert: returns true -- Cancelled PTT-Copy with valid LimitPrice is eligible for re-placement.
}

[Fact]
public void T_B66R_05_IsPttEntryOrderCancelTrigger_CancelledEntryWithPrice_ReturnsTrue()
{
    // Arrange: order.OrderState = OrderState.Cancelled.
    //          order.Name = "Entry", order.LimitPrice = 5050.25.
    //          order.Instrument.FullName = "MES SEP26".
    // Act: call IsPttEntryOrderCancelTrigger(order).
    // Assert: returns true -- Cancelled "Entry" (Clone mode) with valid LimitPrice triggers re-place.
}

[Fact]
public void T_B66R_06_IsPttEntryOrderCancelTrigger_CancelledStop1WithPrice_ReturnsFalse()
{
    // Arrange: order.OrderState = OrderState.Cancelled.
    //          order.Name = "Stop1", order.LimitPrice = 5045.0.
    //          order.Instrument.FullName = "MES SEP26".
    // Act: call IsPttEntryOrderCancelTrigger(order).
    // Assert: returns false -- "Stop1" is an ATM bracket name, not a managed entry.
    //         Per audit: name guard checks name != "PTT-Copy" && name != "Entry" -- Stop1 fails.
}

[Fact]
public void T_B66R_07_HasWorkingPttCopy_NoOrders_ReturnsFalse()
{
    // Arrange: follower account has an empty Orders collection (or all orders in terminal state).
    //          No Working, Accepted, or Submitted orders exist for the instrument.
    // Act: call HasWorkingPttCopy(account, instrument).
    // Assert: returns false -- no replacement in flight; ATM-sweep scenario applies.
}

[Fact]
public void T_B66R_08_HasWorkingPttCopy_WorkingPttCopyExists_ReturnsTrue()
{
    // Arrange: follower account.Orders contains one order:
    //          Name = "PTT-Copy", OrderState = OrderState.Working,
    //          Instrument = the target instrument.
    // Act: call HasWorkingPttCopy(account, instrument).
    // Assert: returns true -- a replacement PTT-Copy is already in flight; skip re-place.
}

[Fact]
public void T_B66R_09_HasWorkingPttCopy_AcceptedEntryExists_ReturnsTrue()
{
    // Arrange: follower account.Orders contains one order:
    //          Name = "Entry", OrderState = OrderState.Accepted,
    //          Instrument = the target instrument.
    // Act: call HasWorkingPttCopy(account, instrument).
    // Assert: returns true -- a Clone-mode Entry replacement is in flight; skip re-place.
    //         Per plan Section D: HasWorkingPttCopy checks name == "PTT-Copy" || "Entry".
}
```

---

### HOTFIX-B66-NATIVE-ATM — IsExitSignalName Named routing

```csharp
[Fact]
public void T_B66N_01_IsExitSignalName_Entry_ReturnsFalse_B67Regression()
{
    // Arrange: name = "Entry".
    // Act: call IsExitSignalName("Entry").
    // Assert: returns false -- "Entry" was REMOVED from IsExitSignalName by HOTFIX-B67-ENTRY-UNBLOCK.
    //         This is the primary regression guard for the B67 unblock.
    //         If this returns true, the HOTFIX-B67 fix has been undone.
}

[Fact]
public void T_B66N_02_IsExitSignalName_PttCopy_ReturnsTrue()
{
    // Arrange: name = "PTT-Copy".
    // Act: call IsExitSignalName("PTT-Copy").
    // Assert: returns true -- "PTT-Copy" is a PTT-managed copy signal name.
}

[Fact]
public void T_B66N_03_IsExitSignalName_Close_ReturnsTrue()
{
    // Arrange: name = "Close".
    // Act: call IsExitSignalName("Close").
    // Assert: returns true -- "Close" is the standard NT8 native close/exit signal name.
}

[Fact]
public void T_B66N_04_IsExitSignalName_Null_ReturnsFalse()
{
    // Arrange: name = null.
    // Act: call IsExitSignalName(null).
    // Assert: returns false -- null is not a recognized exit signal.
}

[Fact]
public void T_B66N_05_IsExitSignalName_PttQxT1_ReturnsTrue()
{
    // Arrange: name = "PTT-QX-T1".
    // Act: call IsExitSignalName("PTT-QX-T1").
    // Assert: returns true -- PTT- prefix covers all PTT-owned partial-exit orders.
}

[Fact]
public void T_B66N_06_IsExitSignalName_ExitLong_ReturnsTrue()
{
    // Arrange: name = "ExitLong".
    // Act: call IsExitSignalName("ExitLong").
    // Assert: returns true -- "Exit*" prefix matches native NT8 strategy exit signal names.
}
```

---

### HOTFIX-B67-ENTRY-UNBLOCK — "Entry" removed from IsExitSignalName

```csharp
[Fact]
public void T_B67E_01_IsExitSignalName_Entry_ReturnsFalse_PrimaryGuard()
{
    // Arrange: name = "Entry".
    // Act: call IsExitSignalName("Entry").
    // Assert: returns false -- HOTFIX-B67 removed "Entry" from IsExitSignalName.
    //         Adding "Entry" here caused every Entry fill on the leader to bypass
    //         hasOpenPosition in TryDispatchLeaderFlat gate (3), silently blocking
    //         all copy dispatch for the rest of that bar.
    //         See plan Section B HOTFIX-B67-ENTRY-UNBLOCK for full root cause.
}

[Fact]
public void T_B67E_02_IsExitSignalName_PttPrefix_ReturnsTrue()
{
    // Arrange: name = "PTT-".
    // Act: call IsExitSignalName("PTT-").
    // Assert: returns true -- the bare PTT- prefix still matches as a PTT-exit signal.
}

[Fact]
public void T_B67E_03_IsNativeExitName_Entry_ReturnsFalse()
{
    // Arrange: name = "Entry".
    // Act: call IsNativeExitName("Entry").
    // Assert: returns false -- "Entry" is not a native NT8 exit order name.
    //         NT8 native exits are "Close", "Rev", "ExitLong", "ExitShort", etc.
    //         This verifies that "Entry" was never a correct IsNativeExitName candidate.
}

[Fact]
public void T_B67E_04_IsNativeExitName_Close_ReturnsTrue()
{
    // Arrange: name = "Close".
    // Act: call IsNativeExitName("Close").
    // Assert: returns true -- "Close" is the standard NT8 native close order name.
}

[Fact]
public void T_B67E_05_IsNativeExitName_Rev_ReturnsTrue()
{
    // Arrange: name = "Rev".
    // Act: call IsNativeExitName("Rev").
    // Assert: returns true -- "Rev" is an NT8 native reversal order name.
}
```

---

### HOTFIX-CLONE-DRAG — FindFollowerEntryOrder name guard widened + GetCloneAtmMode

```csharp
[Fact]
public void T_CLONE_01_GetCloneAtmMode_NonNullAtmObject_ReturnsNamedWithAtmObject()
{
    // Arrange: _cloneAtmObject is set to a non-null AtmStrategy reference via
    //          SetCloneAtmObjectCache(atmObj).
    //          _cloneAtmCache may be any non-empty string.
    // Act: call GetCloneAtmMode().
    // Assert: returns a FollowerAtmMode.Named value where AtmObject != null.
    //         Per plan Section C: priority 1 is _cloneAtmObject != null -- returns Named with object.
}

[Fact]
public void T_CLONE_02_GetCloneAtmMode_NullObjectNonEmptyCache_ReturnsNamedString()
{
    // Arrange: _cloneAtmObject is null (SetCloneAtmObjectCache never called or called with null).
    //          _cloneAtmCache is non-empty string set via SetCloneAtmCache("MES $200 SL6").
    // Act: call GetCloneAtmMode().
    // Assert: returns a FollowerAtmMode.Named value with TemplateName = "MES $200 SL6"
    //         and AtmObject == null (string fallback path).
    //         Per plan Section C: priority 2 is _cloneAtmCache.Length > 0.
}

[Fact]
public void T_CLONE_03_GetCloneAtmMode_NullObjectEmptyCache_ReturnsInherit()
{
    // Arrange: _cloneAtmObject is null.
    //          _cloneAtmCache is empty string (default initial state).
    // Act: call GetCloneAtmMode().
    // Assert: returns FollowerAtmMode.Inherit().
    //         Per plan Section C: priority 3 (default) -- both caches empty/null.
}

[Fact]
public void T_CLONE_04_SetCloneAtmCache_NonEmpty_GetCloneAtmModeReturnsNamed()
{
    // Arrange: fresh CopyEngine instance (or reset state).
    //          _cloneAtmObject = null (not set).
    // Act: call SetCloneAtmCache("MES $200 SL6").
    //      Then call GetCloneAtmMode().
    // Assert: GetCloneAtmMode() returns Named (not Inherit) with TemplateName = "MES $200 SL6".
    //         Confirms the cache setter updates the fallback path correctly.
}
```

---

### HOTFIX-B66-ATM-OBJ — _cloneAtmObject two-cache design

```csharp
[Fact]
public void T_B66OBJ_01_SetCloneAtmObjectCache_NonNull_GetCloneAtmModeReturnsNamedWithObject()
{
    // Arrange: create a mock or stub AtmStrategy object (non-null).
    //          _cloneAtmCache may be any string.
    // Act: call SetCloneAtmObjectCache(atmObj).
    //      Then call GetCloneAtmMode().
    // Assert: GetCloneAtmMode() returns Named where the AtmObject field equals atmObj.
    //         Priority 1 in GetCloneAtmMode: _cloneAtmObject != null wins over string cache.
}

[Fact]
public void T_B66OBJ_02_SetCloneAtmObjectCache_Null_ClearsAtmObject()
{
    // Arrange: first set _cloneAtmObject to non-null via SetCloneAtmObjectCache(atmObj).
    //          Then set _cloneAtmCache to a non-empty string.
    // Act: call SetCloneAtmObjectCache(null).
    //      Then call GetCloneAtmMode().
    // Assert: GetCloneAtmMode() returns Named with AtmObject == null (falls back to string cache).
    //         Use reflection or CopyEngine test accessor to verify _cloneAtmObject is null.
    //         Per plan Section C: both caches set together in OnCloneModeClick; but if object
    //         is explicitly cleared, string fallback must still work.
}

[Fact]
public void T_B66OBJ_03_ParseAtmModeName_NamedPrefix_ReturnsNamedWithTemplateName()
{
    // Arrange: serialized string = "Named:MES 200".
    // Act: call ParseAtmModeName("Named:MES 200")
    //      (or the equivalent deserialization entry point for FollowerAtmMode).
    // Assert: returns FollowerAtmMode.Named with TemplateName = "MES 200".
    //         This covers the string-based Named path deserialization.
}

[Fact]
public void T_B66OBJ_04_ParseAtmModeName_Inherit_ReturnsInherit()
{
    // Arrange: serialized string = "Inherit".
    // Act: call ParseAtmModeName("Inherit").
    // Assert: returns FollowerAtmMode.Inherit().
    //         Default fallback value must round-trip through serialization.
}

[Fact]
public void T_B66OBJ_05_AtmModeToString_Named_ReturnsNamedPrefix()
{
    // Arrange: mode = FollowerAtmMode.Named("MES 200").
    // Act: call AtmModeToString(mode) (or equivalent ToString/serialize method).
    // Assert: returns "Named:MES 200".
    //         Verifies the round-trip T_B66OBJ_03 depends on is consistent.
}
```

---

### HOTFIX-B67-CHECKBOX-RESTORE — GetSavedFollowerNames (CopyEngine side only)

```csharp
[Fact]
public void T_B67_04_GetSavedFollowerNames_EmptyRules_ReturnsEmptyHashSet()
{
    // Arrange: CopyEngine instance with no rules loaded (_rules is empty ConcurrentBag).
    //          instrument = "MES SEP26", masterName = "Sim101".
    // Act: call GetSavedFollowerNames("MES SEP26", "Sim101").
    // Assert: returns an empty HashSet<string> (not null, not throws).
    //         Per plan Section B HOTFIX-B67-CHECKBOX-RESTORE: method is CYC=2,
    //         returns empty set on no-match. JS-002: never returns null.
}

[Fact]
public void T_B67_05_GetSavedFollowerNames_MatchingRule_ReturnsFollowerNames()
{
    // Arrange: CopyEngine instance with one CopyRule in _rules:
    //          rule.Instrument = "MES SEP26",
    //          rule.MasterAccount = Account with Name = "Sim101",
    //          rule.FollowerAccounts = [Account("Sim102"), Account("Sim103")].
    // Act: call GetSavedFollowerNames("MES SEP26", "Sim101").
    // Assert: returned HashSet<string> contains "Sim102" and "Sim103" (both follower names).
    //         Set count = 2. No duplicates.
}
```

---

### CYC REFACTOR HELPERS — IsBeDisarmCandidate + IsNonFlatDispatchName

```csharp
[Fact]
public void T_CYC_01_IsBeDisarmCandidate_NullOrder_ReturnsFalse()
{
    // Arrange: order = null.
    // Act: call IsBeDisarmCandidate(null) (or IsBeDisarmCandidate(OrderEventArgs e) where e.Order=null).
    // Assert: returns false -- null guard is the first check in IsBeDisarmCandidate.
    //         Per audit Round 3: IsBeDisarmCandidate CYC=5, first guard is null check (line 863).
}

[Fact]
public void T_CYC_02_IsBeDisarmCandidate_FilledPttBeStopWithInstrument_ReturnsTrue()
{
    // Arrange: order.OrderState = OrderState.Filled,
    //          order.Name = "PTT-BE-Stop",
    //          order.Instrument.FullName = "MES SEP26".
    // Act: call IsBeDisarmCandidate(order or e).
    // Assert: returns true -- all 5 preamble guards pass; order is a valid BE-disarm candidate.
    //         Per plan Section G.1: this method guards TryFireFollowerBeDisarm.
}

[Fact]
public void T_CYC_03_IsBeDisarmCandidate_FilledPttBeStop2WithInstrument_ReturnsTrue()
{
    // Arrange: order.OrderState = OrderState.Filled,
    //          order.Name = "PTT-BE-Stop2",
    //          order.Instrument.FullName = "NQ SEP26".
    // Act: call IsBeDisarmCandidate(order or e).
    // Assert: returns true -- name.StartsWith("PTT-BE-Stop") matches "PTT-BE-Stop2" as well.
    //         Verifies the StartsWith pattern handles suffix variants correctly.
}

[Fact]
public void T_CYC_04_IsBeDisarmCandidate_CancelledOrder_ReturnsFalse()
{
    // Arrange: order.OrderState = OrderState.Cancelled (not Filled),
    //          order.Name = "PTT-BE-Stop",
    //          order.Instrument.FullName = "MES SEP26".
    // Act: call IsBeDisarmCandidate(order or e).
    // Assert: returns false -- OrderState guard fires; only Filled state triggers BE disarm.
}

[Fact]
public void T_CYC_05_IsNonFlatDispatchName_Null_ReturnsFalse()
{
    // Arrange: orderName = null.
    // Act: call IsNonFlatDispatchName(null).
    // Assert: returns false -- per audit Round 3: IsNonFlatDispatchName CYC=3,
    //         null check must return false without throwing.
    //         Covers the JS-001 no-throw and JS-002 no-null-return requirements.
}

[Fact]
public void T_CYC_06_IsNonFlatDispatchName_PttQxT1_ReturnsTrue()
{
    // Arrange: orderName = "PTT-QX-T1".
    // Act: call IsNonFlatDispatchName("PTT-QX-T1").
    // Assert: returns true -- PTT-prefix check fires (covers former gate 2.5).
}

[Fact]
public void T_CYC_07_IsNonFlatDispatchName_Entry_ReturnsTrue()
{
    // Arrange: orderName = "Entry".
    // Act: call IsNonFlatDispatchName("Entry").
    // Assert: returns true -- literal "Entry" check fires (covers former gate 2.6).
}

[Fact]
public void T_CYC_08_IsNonFlatDispatchName_Close_ReturnsFalse()
{
    // Arrange: orderName = "Close".
    // Act: call IsNonFlatDispatchName("Close").
    // Assert: returns false -- "Close" is a native exit signal, not a blocked dispatch name.
    //         Must not accidentally block flatten on Close order.
}
```

---

## Test Count Summary

| Group | Tests | IDs |
|-------|-------|-----|
| HOTFIX-B63-FLATTEN-01 | 6 | T_B63_01..06 |
| HOTFIX-B63-COPY-CANCEL-01 | 5 | T_B63C_01..05 |
| HOTFIX-B64-ENTRY-FLATTEN-01 | 5 | T_B64E_01..05 |
| HOTFIX-B65-GATE-C-FILL-GUARD-01 | 5 | T_B65G_01..05 |
| HOTFIX-B66-COPY-REPLACE | 9 | T_B66R_01..09 |
| HOTFIX-B66-NATIVE-ATM | 6 | T_B66N_01..06 |
| HOTFIX-B67-ENTRY-UNBLOCK | 5 | T_B67E_01..05 |
| HOTFIX-CLONE-DRAG | 4 | T_CLONE_01..04 |
| HOTFIX-B66-ATM-OBJ | 5 | T_B66OBJ_01..05 |
| HOTFIX-B67-CHECKBOX-RESTORE | 2 | T_B67_04..05 |
| CYC REFACTOR HELPERS | 8 | T_CYC_01..08 |
| **TOTAL** | **60** | |

> Total exceeds the 48 target because the HOTFIX-B66-COPY-REPLACE group (9 tests) and HOTFIX-B66-NATIVE-ATM (6 tests) together cover two separate sub-themes.
> All tests listed in the task brief are present. The engineer should implement all 60 stubs.

---

## Method Signatures (Engineer Contract)

All methods below must be present in `src/PropTraderTools/CopyEngine.cs` at the
signatures recorded in the DNA audit Round 3:

```csharp
// --- EXISTING (post-B75 extraction, confirmed present) ---
internal static bool IsPttEntryOrderCancelTrigger(Order order)          // CYC=4, line 546
internal static bool IsNonFlatDispatchName(string orderName)            // CYC=3, line 1070
private void TryFireFollowerBeDisarm(OrderEventArgs e)                  // CYC=5, line 866
private static bool IsBeDisarmCandidate(OrderEventArgs e)               // CYC=5, line 533
private void TryHandleDrag(...)                                          // CYC=3, line 932

// --- REQUIRED (must be present for tests to compile) ---
private static bool IsAtmBracketName(string name)                       // CYC=3 (from plan B)
private bool HasWorkingPttCopy(Account acc, Instrument instrument)      // CYC=3 (from plan D)
internal HashSet<string> GetSavedFollowerNames(string instrument, string masterName) // CYC=2
private FollowerAtmMode GetCloneAtmMode()                               // CYC=2 (plan C)
internal void SetCloneAtmObjectCache(NinjaTrader.NinjaScript.AtmStrategy atmObj) // CYC=1
internal void SetCloneAtmCache(string templateName)                     // CYC=1
```

NT8-runtime-bound tests (any method requiring `Account` or `AtmStrategy` live objects):
Mark with `[Fact(Skip="NT8-runtime")]` if the NT8 runtime cannot be mocked in xUnit.

---

*End of B75-LaneA Tickets.*
