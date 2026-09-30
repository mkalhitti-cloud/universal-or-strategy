# Ticket T1 Completion Report

**Epic**: BWAVE-NEXT LaneBRepair
**Ticket**: T1 -- PR43-F1 through PR43-F5 + F7/F8/F9 Test Renames
**Engineer**: ptt-engineer
**Date**: 2026-09-05
**Branch**: bwave-next-lane-b

---

## SCOPE: TICKET 1 ONLY

All work confined to:
- F1, F2, F3, F4, F5: `src/PropTraderTools/CopyEngine.cs`
- F7/F8: `src/PropTraderTools/Tests/BwaveDwLaneATests.cs`
- F9: `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs`

No other files touched. No out-of-scope items (TickCount64, ToList, DW-NEXT-B-01, DW-NEXT-B-02) were changed.

---

## Rules Catalog Gate Result: PASS

Catalog read: `docs/standards/jane-street/RULES_CATALOG.md` (UTF-8 clean).
Applicable P0 rules confirmed:
- JS-021 (lock ban): zero `lock(` in new code -- ConcurrentDictionary + Interlocked only
- JS-033 (async void ban): all modified methods synchronous void
- JS-001 (throw ban in hot paths): no throw in new code
- JS-002 (return null ban): all new early returns are bare `return;` (void methods)

**GATE RESULT: PASS**

---

## Implementation Summary

### F1 -- OnOrderUpdate drain routing (CopyEngine.cs lines ~1422-1432)

Replaced single compound `if (Cancelled||Rejected||Filled) && ContainsKey -> OnDrainCancelAck` with:
- `if (Cancelled || Rejected)` -> ContainsKey guard -> `OnDrainCancelAck`
- `else if (Filled)` -> `TryRemove` (abort drain, position is open)

**Effect**: Filled entry orders no longer trigger `OnDrainCancelAck` and inadvertent second-entry dispatch.

### F2 -- entryCandidates predicate (CopyEngine.cs lines ~6526-6532)

Added to LINQ Where clause:
- `&& (o.OrderType == OrderType.Limit || o.OrderType == OrderType.StopLimit)`
- `&& o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)`

Name prefix confirmed from `FindFollowerEntryOrder` (line 3698): `"PTT-Copy"` (exact). `SubmitEntryDirect` at line 6575 confirms `"PTT-Copy"` is the created order name.

**Effect**: Stop brackets (StopMarket) and non-PTT-Copy orders excluded from drain cancel scope.

### F3 -- _drainOwnedOrderIds field + guard + cleanup (CopyEngine.cs multiple locations)

**Step A -- New field** (after `_pendingDispatchDrains` at line ~383):
```
private readonly ConcurrentDictionary<string, byte> _drainOwnedOrderIds =
    new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
```
Note: `OrderId` type confirmed as `string` per NT8_FULL_REFERENCE.md line 864 ("A string representing the broker issued order id value"). Plan specified `long` but NT8 API is `string` -- corrected to match actual source.

**Step B -- PendingDispatchDrain class**: Added `IReadOnlyList<string> DrainedOrderIds` property; extended constructor with `drainedOrderIds` parameter at position 7 (between `orderType` and `followerAccount`); body sets `DrainedOrderIds = drainedOrderIds`.

**Step C -- TryReplaceOnAtmCancel guard** (line ~872): New first statement -- `if (_drainOwnedOrderIds.ContainsKey(order.OrderId)) return;` CYC 2->3.

**Step D -- SubmitDrainedEntry cleanup** (after TryRemove, before SubmitEntryDirect): `foreach (var id in payload.DrainedOrderIds) _drainOwnedOrderIds.TryRemove(id, out _);`

**Step E -- TryDrainWatchdog cleanup** (inside timeout block): `foreach (var id in kv.Value.DrainedOrderIds) _drainOwnedOrderIds.TryRemove(id, out _);`

### F4 -- TOCTOU fix (CopyEngine.cs DrainThenDispatch)

- Added `var drainedIds = entryCandidates.Select(static e => e.OrderId).ToList();` before constructor call
- Passed `drainedIds` as parameter 7 to `PendingDispatchDrain` constructor
- Changed `pendingCancelCount: 0` to `pendingCancelCount: entryCandidates.Count`
- Removed `int cancelCount = 0;` variable
- Removed `cancelCount++` inside foreach
- Removed `Interlocked.Exchange(ref payload.PendingCancelCount, cancelCount);`

### F5 -- Remove dead cancelCount==0 block (CopyEngine.cs DrainThenDispatch)

Deleted entire `if (cancelCount == 0) { ... return; }` block.
Updated method comment from `CYC=4: ... (4) cancelCount==0 edge guard.` to `CYC=3: ... F5-repair: dead (4) cancelCount==0 branch removed.`

### F7/F8 -- BwaveDwLaneATests.cs renames (method declaration lines only)

- `ActiveOrders_ThreadSafetyVerification` -> `ActiveOrders_FilterBehavior_AfterToListAddition`
- `NakedDetector_DebounceField_UsesLongArithmetic` -> `NakedDetector_DebounceState_FieldTypeIsLong`

### F9 -- BwaveNextLaneBTests.cs renames (method declaration lines only)

- `DrainThenDispatch_CancelsExistingEntryBeforeSubmit` -> `DrainThenDispatch_MethodExists_WithExpectedSignature`
- `OnDrainCancelAck_SubmitsDrainedEntry_WhenPendingCountReachesZero` -> `OnDrainCancelAck_MethodExists_WithExpectedSignature`
- `DrainWatchdog_ClearsStuckDrain_AfterTimeout` -> `DrainWatchdog_MethodExists_WithExpectedSignature`

Bodies, [Fact] attributes, assertions: UNCHANGED.

---

## 7-Scan Results

**SCAN 1 -- lock() check**:
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\("`
Output: 0 actual `lock(` statements (all hits are comments referencing "no lock()")
Result: **PASS -- 0 violations in new code**

**SCAN 2 -- async void check**:
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async void "`
Output: 1 comment match only ("NOT async void (JS-033)"), no actual declarations
Result: **PASS -- 0 async void in new code**

**SCAN 3 -- return null check**:
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null;"`
Output: 12 pre-existing matches at lines 1151, 1854, 2778, 2859, 2867, 3549, 3718, 5172, 5178, 5257, 6323, 6338 -- all in unrelated methods, none in new code added by this ticket
Result: **PASS -- 0 in new code**

**SCAN 4 -- CYC check (manual branch count)**:

| Method | Pre-fix CYC | Post-fix CYC | Delta | <= 8 |
|--------|-------------|--------------|-------|------|
| OnOrderUpdate | 7 | 8 | +1 (F1: if/else-if block) | PASS |
| DrainThenDispatch | 4 | 3 | -1 (F5 dead branch removed) | PASS |
| TryReplaceOnAtmCancel | 2 | 3 | +1 (F3 drain guard) | PASS |
| SubmitDrainedEntry | 3 | 4 | +1 (F3 foreach cleanup) | PASS |
| TryDrainWatchdog | 3 | 4 | +1 (F3 inner foreach cleanup) | PASS |
| OnDrainCancelAck | 3 | 3 | 0 (unchanged) | PASS |

All <= 8. Result: **PASS**

Note on _drainOwnedOrderIds type correction: Plan specified `ConcurrentDictionary<long, byte>` but NT8 `Order.OrderId` is `string` per NT8_FULL_REFERENCE.md line 864. Corrected to `ConcurrentDictionary<string, byte>` and `IReadOnlyList<string>` throughout. This is a type correctness fix, not scope creep. Build error CS1503 confirmed the correction was required.

**SCAN 5 -- ASCII-only check**:
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]" -Encoding UTF8`
Output: 0 matches
Result: **PASS -- 0 non-ASCII characters**

**SCAN 6 -- NT8 banned API check**:
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "Account\.Change|AtmStrategyCreate|AtmStrategyChangeStopTarget"`
Output: 4 matches -- all comments referencing banned APIs, zero actual calls
Result: **PASS -- 0 banned API calls in new code**

**SCAN 7 -- Build check**:
Command: `dotnet build src/PropTraderTools 2>&1 | Select-Object -Last 20`
Output:
```
Build succeeded.
    1 Warning(s)   [pre-existing: B131Tests.cs xUnit2004 -- unrelated to this ticket]
    0 Error(s)
Time Elapsed 00:00:02.40
```
Result: **PASS -- 0 errors**

---

## Test Renames Confirmed

| # | Old Name | New Name | File | Result |
|---|----------|----------|------|--------|
| 1 | `ActiveOrders_ThreadSafetyVerification` | `ActiveOrders_FilterBehavior_AfterToListAddition` | BwaveDwLaneATests.cs | PASS |
| 2 | `NakedDetector_DebounceField_UsesLongArithmetic` | `NakedDetector_DebounceState_FieldTypeIsLong` | BwaveDwLaneATests.cs | PASS |
| 3 | `DrainThenDispatch_CancelsExistingEntryBeforeSubmit` | `DrainThenDispatch_MethodExists_WithExpectedSignature` | BwaveNextLaneBTests.cs | PASS |
| 4 | `OnDrainCancelAck_SubmitsDrainedEntry_WhenPendingCountReachesZero` | `OnDrainCancelAck_MethodExists_WithExpectedSignature` | BwaveNextLaneBTests.cs | PASS |
| 5 | `DrainWatchdog_ClearsStuckDrain_AfterTimeout` | `DrainWatchdog_MethodExists_WithExpectedSignature` | BwaveNextLaneBTests.cs | PASS |

Old names confirmed absent from both files (Select-String returns 0 matches for all 5 old patterns).

---

## Test Run Result

Command:
```
dotnet test src/PropTraderTools --filter "DrainThenDispatch_MethodExists_WithExpectedSignature|OnDrainCancelAck_MethodExists_WithExpectedSignature|DrainWatchdog_MethodExists_WithExpectedSignature|ActiveOrders_FilterBehavior_AfterToListAddition|NakedDetector_DebounceState_FieldTypeIsLong"
```
Output: `Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 2s`

**5 passed, 0 failed**

---

## Build Result

0 errors. 1 pre-existing warning (B131Tests.cs xUnit2004 Assert.Equal for bool -- unrelated to this ticket, pre-existing).

---

## NT8 Sync Result

Command: `powershell -File scripts\ptt-sync-and-verify.ps1`
Output: `=== SYNC + VERIFY: PASS (18 files confirmed) ===`
Result: **18/18 OK, 0 MISMATCH**

---

## Notes

1. **OrderId type correction**: The plan and ticket spec specified `long` for the drain-owned ID dict key and `DrainedOrderIds` property. NT8_FULL_REFERENCE.md line 864 confirms `Order.OrderId` is a `string`. All occurrences of `long` corrected to `string` with `StringComparer.Ordinal`. This was a necessary type correctness fix discovered during build (error CS1503).

2. **No scope creep**: The OrderId type correction is within the F3 fix scope -- the field and property were introduced by this ticket. No other existing code was changed.

---

## Result Summary

**BUILD_PASS**

All 7 scans: ZERO violations in new code.
All 5 test renames: confirmed, all pass under new names.
Build: 0 errors.
NT8 sync: 18/18 OK.

*Completion report authored: 2026-09-05 | ptt-engineer | Phase 4a | BWAVE-NEXT LaneBRepair T1*
