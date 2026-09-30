# B132 LaneB -- Ticket 1 Completion

**Epic**: B132 LaneB
**Defect**: DW-B138 P1 -- Stop Drag Runtime Silent (Diagnostic Phase)
**Ticket**: Ticket 1 -- B132 LaneB Diagnostic Prints
**Engineer**: ptt-engineer (Phase 4a)
**File modified**: src/PropTraderTools/CopyEngine.cs
**Test file modified**: src/PropTraderTools/Tests/B131Tests.cs (new B132LaneBTests class)
**Status**: BUILD_PASS

---

## Summary of Changes

### Change 1 -- `_diagnosticMode` field
**File**: src/PropTraderTools/CopyEngine.cs
**Location**: After L407 `CopyEnabledChanged` event, before `// --- Nested structs ---` comment.
**Lines added**: 4 lines (comment + field declaration)

```csharp
// B132 LaneB diagnostic gate -- set to false to disable all TP1-TP4 Print calls.
// Remove this field and all TryLogDragTrace / TryLogSFBTrace calls when DW-B138 is confirmed fixed.
// JS-021: static bool read is lock-free (no torn reads on bool). Not volatile (diagnostic only).
private static bool _diagnosticMode = true;
```

### Change 2 -- `TryLogDragTrace` helper (TP1) + call site in OnOrderUpdate
**File**: src/PropTraderTools/CopyEngine.cs
**Call site location**: In `OnOrderUpdate`, after `EvictDedup(...)` line, before `// HOTFIX-FLAT-DISARM-FOLLOWER:` comment.
**New method location**: After `TryHandleBracketDrag` closing brace, before `TryHandleDrag`.

Call site added:
```csharp
TryLogDragTrace(e.Order);
```

New method:
```csharp
// B132 LaneB diagnostic. Set _diagnosticMode=false to disable. Remove when DW-B138 confirmed fixed.
// CYC=4: (1) if-guard, (2) &&, (3) ||.
// JS-021: no lock. JS-001: no throw. NT8 Output.Process is safe from any thread.
private void TryLogDragTrace(Order order)
{
    if (_diagnosticMode && (IsWorkingBracket(order) || order.OrderState == OrderState.ChangeSubmitted))
        NinjaTrader.Code.Output.Process(
            "[TP1-OOU] name=" + (order.Name ?? "null")
            + " state=" + order.OrderState
            + " signal=" + (order.FromEntrySignal ?? "null")
            + " acct=" + (order.Account?.Name ?? "?"),
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
}
```

### Change 3 -- TP2 inline in `TryHandleBracketDrag`
**File**: src/PropTraderTools/CopyEngine.cs
**Location**: In `TryHandleBracketDrag`, after opening brace, before `if (!IsWorkingBracket(order))`.

Added:
```csharp
if (_diagnosticMode)
    NinjaTrader.Code.Output.Process(
        "[TP2-DRAG] IsWorkingBracket=" + IsWorkingBracket(order)
        + " name=" + (order.Name ?? "null")
        + " state=" + order.OrderState,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
```

**CYC impact**: TryHandleBracketDrag 3 -> 4.

### Change 4 -- TP3 inline in `HandleBracketChange`
**File**: src/PropTraderTools/CopyEngine.cs
**Location**: In `HandleBracketChange`, after `double newPrice = tickSize > 0 ? ...`, before `foreach`.

Added:
```csharp
if (_diagnosticMode)
    NinjaTrader.Code.Output.Process(
        "[TP3-HBC] isStop=" + isStop
        + " leaderName=" + (leaderOrder.Name ?? "null")
        + " rawPrice=" + rawPrice
        + " newPrice=" + newPrice
        + " followerCount=" + rule.FollowerAccounts.Length,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
```

**CYC impact**: HandleBracketChange 7 -> 8 (AT boundary, does not exceed).
**Reviewer note applied**: CYC=8 (not 7) per plan-review V1 correction. `?.TickSize` null-conditional counted.

### Change 5 -- `TryLogSFBTrace` helper (TP4) + call site in SyncFollowerBracket
**File**: src/PropTraderTools/CopyEngine.cs
**Call site location**: In `SyncFollowerBracket`, after `var fo = FindFollowerBracketOrder(...)`, before `if (fo == null)`.
**New method location**: After `TryLogDragTrace`, before `TryHandleDrag`.

Call site added:
```csharp
TryLogSFBTrace(acc, leaderOrder, isStop, fo);
```

New method:
```csharp
// B132 LaneB diagnostic. Set _diagnosticMode=false to disable. Remove when DW-B138 confirmed fixed.
// CYC=2: (1) if-guard.
// JS-021: no lock. acc.Orders.ToList() is NT8-safe on order-update thread.
private void TryLogSFBTrace(Account acc, Order leaderOrder, bool isStop, Order? fo)
{
    if (!_diagnosticMode)
        return;
    var ordList = acc.Orders.ToList();
    NinjaTrader.Code.Output.Process(
        "[TP4-SFB] acc=" + acc.Name
        + " leaderName=" + (leaderOrder.Name ?? "null")
        + " isStop=" + isStop
        + " fo=" + (fo?.Name ?? "NULL")
        + " followerOrders=["
        + string.Join(",", ordList.Select(o => (o.Name ?? "?") + ":" + o.OrderState))
        + "]",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
}
```

### Change 6 -- New test `B132_LaneB_DiagnosticMode_FieldExists`
**File**: src/PropTraderTools/Tests/B131Tests.cs
**Location**: New class `B132LaneBTests` appended at end of file (before closing namespace brace).
**Note**: Ticket specified `CopyEngineTests.cs` -- the test was also added there (in `B79CancelRaceGuardTests`
class), but that class is pre-existing non-discovered. The authoritative test is in `B131Tests.cs`
in the new `B132LaneBTests` class in namespace `PropTraderTools.Tests` -- confirmed PASSING by
`dotnet test --filter DiagnosticMode_FieldExists` (1 passed).

```csharp
[Fact]
public void B132_LaneB_DiagnosticMode_FieldExists()
{
    var field = typeof(CopyEngine).GetField(
        "_diagnosticMode",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
    );
    Assert.NotNull(field);
    Assert.Equal(typeof(bool), field!.FieldType);
    Assert.Equal(true, (bool)field.GetValue(null)!);
}
```

---

## B131 LaneA Non-Regression Verification

The following B131 LaneA changes remain UNCHANGED in source:

| Symbol | Status |
|--------|--------|
| `SignalOrNameMatches` (L2361 area) | UNCHANGED -- not touched |
| `FindFollowerBracketOrder` signature (L2375 area) | UNCHANGED -- not touched |
| `SyncFollowerBracket` call site passing `leaderOrder.Name` | UNCHANGED -- only added `TryLogSFBTrace` call after it |

B131 regression tests all PASS (verified by `--filter B131` run: 7 passed, 0 failed).

---

## 7-Scan Results

### SCAN-01 -- LOCK SCAN
```
Get-ChildItem -Path src -Filter "*.cs" -Recurse | Select-String -Pattern "lock\s*\("
```
**Result**: All matches are in comments only (e.g. `// JS-021: no lock()`). Zero actual `lock(` usage.
**SCAN-01: PASS (0 violations)**

### SCAN-02 -- THROW SCAN
```
Get-ChildItem -Path src/PropTraderTools/CopyEngine.cs | Select-String -Pattern "throw new"
```
**Result**: No output. Zero `throw new` in CopyEngine.cs.
**SCAN-02: PASS (0 -- no new throws, no existing throws)**

### SCAN-03 -- NULL RETURN SCAN
```
Get-ChildItem -Path src/PropTraderTools/CopyEngine.cs | Select-String -Pattern "return null"
```
**Result**: Pre-existing `return null` at L1641, L2460, L2518, L3855, L3861, L3940, L4776.
Zero new `return null` added. Both new methods are `void`.
**SCAN-03: PASS (existing only, no new additions)**

### SCAN-04 -- ASYNC VOID SCAN
```
Get-ChildItem -Path src -Filter "*.cs" -Recurse | Select-String -Pattern "async void "
```
**Result**: All matches are in comments only (JS-033 rule references). Zero actual `async void` declarations.
**SCAN-04: PASS (0 violations)**

### SCAN-05 -- DATETIME NOW SCAN
```
Get-ChildItem -Path src -Filter "*.cs" -Recurse | Select-String -Pattern "DateTime\.Now"
```
**Result**: One match in PttBreakEven.cs comment only (`NOT DateTime.Now`). Zero actual usage.
**SCAN-05: PASS (0 violations)**

### SCAN-06 -- CYC BUDGET CHECK
Manual computation per ticket spec and reviewer-verified calculations:

| Method | CYC Before | CYC After | Within 8? | Notes |
|--------|-----------|-----------|-----------|-------|
| `TryLogDragTrace` (NEW) | N/A | 4 | YES | base+1, if+1, &&+1, \|\|+1 |
| `TryHandleBracketDrag` | 3 | 4 | YES | +1 for `if (_diagnosticMode)` |
| `HandleBracketChange` | 7 | 8 | YES -- AT BOUNDARY | Reviewer V1: CYC=8 not 7 (?.TickSize counted) |
| `TryLogSFBTrace` (NEW) | N/A | 2 | YES | base+1, if+1 |
| `SyncFollowerBracket` | 8 | 8 | YES -- UNCHANGED | unconditional call, +0 branches |
| `OnOrderUpdate` | ~11-18 | ~11-18 | pre-existing | unconditional call, +0 branches |

**SCAN-06: PASS (all new/modified methods within CYC <= 8)**

### SCAN-07 -- ASCII SCAN
```
Get-Content src/PropTraderTools/CopyEngine.cs | ForEach-Object { if ($_ -match '[^\x00-\x7F]') { $_ } } | Measure-Object
```
**Result**: Count = 0. Zero non-ASCII characters in CopyEngine.cs.
**SCAN-07: PASS (0 non-ASCII characters)**

---

## Build Result

```
=== PTT SYNC: src/PropTraderTools -> NT8 AddOns ===
  COPIED:  CopyEngine.cs
  Copied:   1  |  In-sync: 17  |  Excluded: 57

=== PTT VERIFY: MD5 check every synced file ===
  OK       AtrSizingEngine.cs
  OK       CopyEngine.cs
  ... (all 18 files OK)

=== SYNC + VERIFY: PASS (18 files confirmed) ===
```

**Build: PASS -- 0 MISMATCH lines**

---

## Test Result

```
dotnet test src/PropTraderTools/PropTraderTools.csproj
Failed!  - Failed: 15 (pre-existing), Passed: 324, Skipped: 15, Total: 354
```

New tests added: **+6** (1 B132 test + 5 B131 tests now discovered via B132LaneBTests class addition).
Pre-existing failures: **15** (unchanged -- B71/B72/B74/B76/B79/B44/B77 pre-existing reflection mismatches).
B132 new test: **PASS** (`dotnet test --filter DiagnosticMode_FieldExists` -> 1 passed).
B131 regression tests: **PASS** (`dotnet test --filter B131` -> 7 passed, 0 failed).

---

## PENDING: Director to run drag and paste Output Tab trace in chat.

Director: please SIM-open a position with ATM on the leader account, then drag Stop1 to a new price. After the drag, paste the NinjaTrader Output Tab 1 contents here. The trace will contain [TP1-OOU], [TP2-DRAG], [TP3-HBC], [TP4-SFB] lines that identify the drop point.

Expected trace lines (all 4 should appear if the drag reaches SyncFollowerBracket):
- `[TP1-OOU] name=Stop1 state=ChangeSubmitted signal=<signal> acct=<leader_acct>`
- `[TP2-DRAG] IsWorkingBracket=True name=Stop1 state=ChangeSubmitted`
- `[TP3-HBC] isStop=True leaderName=Stop1 rawPrice=<X> newPrice=<X> followerCount=<N>`
- `[TP4-SFB] acc=<follower_acct> leaderName=Stop1 isStop=True fo=<follower_stop_order_or_NULL> followerOrders=[...]`

If TP4-SFB shows `fo=NULL`, the `FindFollowerBracketOrder` match is failing (Sub-Phase 2 target).
If TP3-HBC appears but TP4-SFB does NOT, the issue is upstream of SyncFollowerBracket.
If TP2-DRAG appears but TP3-HBC does NOT, `HandleBracketChange` is being bypassed.
If TP1-OOU appears but TP2-DRAG does NOT, the drag is not reaching `TryHandleBracketDrag`.
If none appear: diagnostic mode may be off, or the drag is not triggering `OnOrderUpdate`.

---

**Final Gate**: BUILD_PASS
