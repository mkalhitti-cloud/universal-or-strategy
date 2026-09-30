# B143 Ticket 1 Completion

**Status**: BUILD_PASS
**Engineer**: ptt-engineer
**Date**: 2026-09-07

## Ticket
Ticket 1 -- Add test seam shims + B143 xUnit test suite

## Files Modified

| File | Change Type | Summary |
|------|-------------|---------|
| `src/PropTraderTools/CopyEngine.cs` | Test seam only -- no logic changes | Added `#region B143 test seam` block after L3511 (after DW-B135 accessors). 5 thin expression-body forwarding shims: IsLiveEntryBlocked_ForTest, EvictDedup_ForTest, ClearLiveEntryForInstrument_ForTest, LiveEntryInstrumentsContains_ForTest, EntryInstrKeyByOrderIdContains_ForTest. Zero logic, CYC=1 each. |
| `src/PropTraderTools/Tests/B143Tests.cs` | New file | 7 xUnit [Fact] tests in class B143Tests (namespace PropTraderTools.Tests). All tests access CopyEngine.Instance via test seam shims. ASCII-only. No async, no throw, no return null. |
| `src/PropTraderTools/PropTraderTools.csproj` | Compile entry added | Added `<Compile Include="Tests\B143Tests.cs" />` after the `Tests\B139Tests.cs` entry. |

## SCAN-01 (ASCII): PASS

CopyEngine.cs: 0 hits
B143Tests.cs: 0 hits

Both files confirmed ASCII-only (Select-String returned no results for non-ASCII bytes).

## SCAN-02 (lock ban JS-021): PASS

0 hits in actual code. 4 comment-only matches (lines 324, 358, 1750, 3725) -- all in comments
beginning with "//". No actual lock() statements anywhere in CopyEngine.cs.

Confirmed: all state mutations use ConcurrentDictionary exclusively (TryAdd, TryRemove, ContainsKey,
Keys enumeration). Zero lock() calls.

## SCAN-03 (CYC audit): PASS

Manual CYC verification (no complexity_audit.py in scripts/):

| Method | Expected CYC | Actual CYC | Status |
|--------|-------------|-----------|--------|
| IsLiveEntryBlocked | 4 | 4 (base=1 + 3 branches) | PASS |
| ClearLiveEntryForInstrument | 2 | 2 (base=1 + foreach=1) | PASS |
| EvictDedup | 5 | 5 (base=1 + terminal=1 + Cancelled=1 + TryRemove-guard=1 + Filled=1) | PASS |
| TryFirePositionState | 8 (AT LIMIT) | 8 (base=1 + 7 branches: state-check, null-check, prior-check, !hasPos, foreach, account-match, isLeaderAcct) | PASS -- unchanged |
| DispatchCopy | 8 (AT LIMIT, no touch) | 8 | PASS -- unchanged |
| New shims (5 x CYC=1) | 1 each | 1 each (expression-body, no branches) | PASS |

All methods CYC <= 8. TryFirePositionState AT LIMIT -- shims add 0 branches.

## SCAN-04 (JS P0 gate): PASS

throw new in CopyEngine.cs code: 0 hits (all existing throws are in comments)
async void in CopyEngine.cs code: 0 hits
B143Tests.cs throw new / async void / return null: 0 hits

No P0 JS-001, JS-033, or JS-002 violations in new/changed code.

## SCAN-05 (dotnet build): PASS

Build output:
  Build succeeded.
  1 Warning(s)  [pre-existing: xUnit2004 in B131Tests.cs L165 -- not from B143 code]
  0 Error(s)
  Time Elapsed 00:00:08.89

0 errors, 0 new warnings from B143 code.

## SCAN-06 (dotnet test): PASS

Test output:
  Passed!  - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 9 ms

Tests:
  T_B143_01_IsLiveEntryBlocked_FirstCall_ReturnsFalse_AllowsDispatch    PASS
  T_B143_02_IsLiveEntryBlocked_SecondCall_SameInstrKey_ReturnsTrue_BlocksDuplicate    PASS
  T_B143_03_EvictDedup_Cancelled_ClearsInstrKey_FutureEntryUnblocked    PASS
  T_B143_04_EvictDedup_Filled_DoesNotClear_TradeStillLive    PASS
  T_B143_05_ClearLiveEntryForInstrument_RemovesAllKeysWithPrefix    PASS
  T_B143_06_ClearLiveEntryForInstrument_IsNoOp_WhenNoMatchingKey    PASS
  T_B143_07_EvictDedup_BracketCancelOrderId_DoesNotClearLiveEntryGuard    PASS

7/7 PASS. 0 failures. 0 skipped.

## SCAN-07 (ptt-sync-and-verify): PASS

Sync output:
  COPIED:  CopyEngine.cs
  Copied: 1  |  In-sync: 17  |  Excluded: 63

Verify output (all 18 files):
  OK  AtrSizingEngine.cs
  OK  CopyEngine.cs
  OK  FeatureFlags.cs
  OK  LicenseClient.cs
  OK  TradeCopierAddOn.cs
  OK  TradeCopierPanel.cs
  OK  TradeCopierWindow.cs
  OK  Core\PttContracts.cs
  OK  Features\PttBreakEven.cs
  OK  Features\PttBreakEvenSwap.cs
  OK  Features\PttCancel.cs
  OK  Features\PttCopier.cs
  OK  Features\PttFlatten.cs
  OK  Features\PttFollowerStrategy.cs
  OK  Features\PttGlobalBreakEven.cs
  OK  Features\PttGlobalQuickExit.cs
  OK  Features\PttQuickExit.cs
  OK  Features\PttTrim.cs

=== SYNC + VERIFY: PASS (18 files confirmed) ===
0 MISMATCH lines.

## DW Items Closed

- **DW-B142-MGC-02**: CLOSED -- T_B143_01 verifies first-pass dispatch allowed (IsLiveEntryBlocked returns false for fresh instrKey); T_B143_02 verifies duplicate blocked (second call same instrKey returns true). Instrument-level entry guard confirmed working via test seam.
- **DW-B142-MGC-01**: CLOSED -- Root cause confirmed resolved by MGC-02 instrument-level guard. The MGC cancel+resubmit pattern produces a new orderId on same instrument+direction; IsLiveEntryBlocked Branch 1 (ContainsKey) blocks it before any orderId check.

---

*Produced by ptt-engineer, B143 Phase 4a.*