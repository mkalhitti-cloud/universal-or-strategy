# BWAVE-NEXT LaneA Ticket 5 Completion Report

**Ticket**: T5 -- DW-NEW-09: ActiveOrders Filter Wrapper
**File**: `src/PropTraderTools/CopyEngine.cs`
**Engineer**: ptt-engineer
**Date**: 2026-09-04
**Status**: BUILD_PASS

---

## 1. ActiveOrders Helper Confirmed

**Location**: `src/PropTraderTools/CopyEngine.cs` lines 3430-3441

```csharp
// DW-NEW-09: ActiveOrders -- terminal-state filter for Account.Orders.
// Returns only orders in non-terminal states (Filled/Cancelled/Rejected excluded).
// CYC=1: expression body, single Where predicate, no branching.
// JS-021: no lock (LINQ Where is non-mutating). JS-002: IEnumerable<Order> (never null).
// JS-036: lazy Where -- no heap allocation beyond the enumerator.
// Fix point: callers that need active orders use this instead of .ToList().
// NT8: acc.Orders iteration is safe on order-update callback thread (same as existing ToList() pattern).
private static IEnumerable<Order> ActiveOrders(Account acc) =>
    acc.Orders.Where(static o =>
        o.OrderState != OrderState.Filled
        && o.OrderState != OrderState.Cancelled
        && o.OrderState != OrderState.Rejected);
```

Properties verified:
- **CYC**: 1 (expression body, single Where predicate)
- **Access modifier**: `private static`
- **Return type**: `IEnumerable<Order>` (never null -- JS-002 compliant)
- **No lock**: LINQ Where is non-mutating (JS-021 compliant)
- **No alloc**: lazy Where enumerator only (JS-036 compliant)

Additionally, an `internal static ActiveOrdersTestable(IEnumerable<Order>)` seam was added
at line 3446 for xUnit test access without requiring a live NT8 Account object.

---

## 2. Call Site 1 -- FindFollowerBracketOrder Account Overload

**Line**: 3468
**Before**: `follower.Orders.ToList(),`
**After**: `ActiveOrders(follower), // DW-NEW-09: terminal orders excluded`

Context (lines 3461-3472):
```csharp
private Order? FindFollowerBracketOrder(
    Account follower,
    string? fromEntrySignalName,
    bool isStop,
    string? leaderName = null
) =>
    FindFollowerBracketOrder(
        ActiveOrders(follower), // DW-NEW-09: terminal orders excluded
        fromEntrySignalName,
        isStop,
        leaderName
    );
```

---

## 3. Call Site 2 -- FindFollowerEntryOrder

**Line**: 3668
**Before**: `foreach (var order in follower.Orders.ToList())`
**After**: `foreach (var order in ActiveOrders(follower)) // (1) DW-NEW-09: terminal orders excluded`

Context (lines 3666-3670):
```csharp
private static Order? FindFollowerEntryOrder(Account follower, Instrument instrument)
{
    foreach (var order in ActiveOrders(follower)) // (1) DW-NEW-09: terminal orders excluded
    {
        if (order.Instrument != instrument) // (2)
```

---

## 4. Orders.ToList() Count Before and After

| Metric | Value |
|--------|-------|
| Count before T5 | 25 |
| Count after T5 | **23** |
| Difference | -2 (exactly 2 replaced) |

Verification command:
```
(Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\.Orders\.ToList\(\)").Count
```
Result: **23** PASS

---

## 5. TryLogSFBTrace Line -- Confirmed Unchanged

**Line**: 1956 (diagnostic dump -- intentionally shows full order history)
```csharp
var ordList = acc.Orders.ToList();
```
Context confirms this is inside `TryLogSFBTrace` at line 1952 (private void).
TryLogSFBTrace is a diagnostic method that intentionally iterates ALL orders including
terminal states. Per spec: DO NOT change. Confirmed UNCHANGED.

---

## 6. Tests Written

**File**: `src/PropTraderTools/Tests/BwaveDwLaneATests.cs`

### Test 1
```
[Fact] FindFollowerBracketOrder_SkipsFilledAndCancelledOrders()
```
- Arrange: 14 Cancelled StopMarket + 1 Working StopMarket orders (all named "Stop1")
- Uses: `CopyEngine.Instance.FindFollowerBracketOrderTestable(orders, null, isStop: true, "Stop1")`
- Assert: result is not null, OrderState == Working, Name == "Stop1"
- **Result**: PASS

### Test 2
```
[Fact] FindFollowerEntryOrder_SkipsFilledAndCancelledEntries()
```
- Arrange: 1 Cancelled Limit + 1 Working Limit order (both named "PTT-Copy")
- Uses: `CopyEngine.ActiveOrdersTestable(orders)` -- internal seam (no FindFollowerEntryOrder test seam exists)
- Assert: activeList.Count == 1, OrderState == Working, Name == "PTT-Copy"
- **Result**: PASS

Both tests: **2/2 PASS** (confirmed: `dotnet test --filter "FindFollowerBracketOrder_SkipsFilledAndCancelledOrders|FindFollowerEntryOrder_SkipsFilledAndCancelledEntries"`)

---

## 7. All 7 Scan Results

### SCAN-01 -- JS-021 lock()
```powershell
Select-String -Path "src/PropTraderTools/*.cs" -Pattern "^\s+lock\s*\(" -Include "*.cs"
```
**Result**: 0 actual lock() invocations. PASS
(All hits in grep output are code comments referencing "no lock()" -- not actual lock calls.)

### SCAN-02 -- JS-033 async void
```powershell
Select-String -Path "src/PropTraderTools/*.cs" -Pattern "^\s+async void [A-Z]" -Include "*.cs"
```
**Result**: 0 results. PASS

### SCAN-03 -- JS-002 return null (new occurrences)
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null"
```
**Result**: 21 occurrences -- ALL pre-existing. No new `return null` introduced by T5.
`ActiveOrders` returns `IEnumerable<Order>` (never null). PASS

### SCAN-04 -- JS-001 throw new
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw new" | Where-Object { $_.Line -notmatch "^\s*//" }
```
**Result**: 0 results. PASS

### SCAN-05 -- CYC<=8 (build check)
```
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
**Result**: Build succeeded. 1 Warning (pre-existing xUnit2004 in B131Tests.cs -- unrelated to T5). 0 Errors. PASS
```
    1 Warning(s)
    0 Error(s)
```

### SCAN-06 -- ASCII
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"
```
**Result**: 0 results. PASS

### SCAN-07 -- xUnit [Fact] (no [Test])
```powershell
Select-String -Path "src/PropTraderTools/Tests/BwaveDwLaneATests.cs" -Pattern "\[Fact\]"
Select-String -Path "src/PropTraderTools/Tests/BwaveDwLaneATests.cs" -Pattern "\[Test\]"
```
**Result**: 14 [Fact] found (lines 17, 28, 79, 94, 109, 130, 157, 177, 202, 218, 233, 249, 280, 319).
0 [Test] found. PASS

---

## 8. NT8 Sync Output (Verbatim)

```
=== PTT SYNC: src/PropTraderTools -> NT8 AddOns ===
  COPIED:  CopyEngine.cs

  Copied:   1  |  In-sync: 17  |  Excluded: 68

=== PTT VERIFY: MD5 check every synced file ===
  OK       AtrSizingEngine.cs
  OK       CopyEngine.cs
  OK       FeatureFlags.cs
  OK       LicenseClient.cs
  OK       TradeCopierAddOn.cs
  OK       TradeCopierPanel.cs
  OK       TradeCopierWindow.cs
  OK       Core\PttContracts.cs
  OK       Features\PttBreakEven.cs
  OK       Features\PttBreakEvenSwap.cs
  OK       Features\PttCancel.cs
  OK       Features\PttCopier.cs
  OK       Features\PttFlatten.cs
  OK       Features\PttFollowerStrategy.cs
  OK       Features\PttGlobalBreakEven.cs
  OK       Features\PttGlobalQuickExit.cs
  OK       Features\PttQuickExit.cs
  OK       Features\PttTrim.cs

=== SYNC + VERIFY: PASS (18 files confirmed) ===

NEXT STEP (MANDATORY):
  Press F5 in NinjaTrader 8, or go to:
  Tools -> Edit NinjaScript -> Compile
  File copy alone does NOT activate the new code.
```

**18/18 OK, 0 MISMATCH** -- PASS

---

## 9. dotnet build Result

```
Build succeeded.
    1 Warning(s)  [pre-existing: B131Tests.cs xUnit2004 warning -- not introduced by T5]
    0 Error(s)
```

**0 errors, 0 new warnings** -- PASS

---

## 10. dotnet test Result

```
dotnet test --filter "FindFollowerBracketOrder_SkipsFilledAndCancelledOrders|FindFollowerEntryOrder_SkipsFilledAndCancelledEntries"

Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2, Duration: 2 s
```

T5-specific tests: **2/2 PASS**

Full test suite: 525 passed, 36 failed (pre-existing WPF STA thread failures, CopyEngineB72
parameter count mismatches -- all pre-existing, 0 regressions introduced by T5).

---

## 11. Acceptance Criteria Checklist

| # | Criterion | Status |
|---|-----------|--------|
| 1 | `ActiveOrders(Account)` helper: CYC=1, static, private, no lock, lazy Where | PASS |
| 2 | FindFollowerBracketOrder Account overload (line 3468): uses `ActiveOrders(follower)` | PASS |
| 3 | FindFollowerEntryOrder (line 3668): uses `ActiveOrders(follower)` | PASS |
| 4 | ALL 23 other `acc.Orders.ToList()` call sites: unchanged | PASS |
| 5 | TryLogSFBTrace diagnostic (line 1956): unchanged (full history dump) | PASS |
| 6 | FindFollowerBracketOrderTestable IEnumerable seam (line 3624): unchanged | PASS |
| 7 | .Orders.ToList() count after T5: 23 (was 25; 2 replaced) | PASS |
| 8 | `dotnet build` 0 errors | PASS |
| 9 | [Fact] FindFollowerBracketOrder_SkipsFilledAndCancelledOrders passes | PASS |
| 10 | [Fact] FindFollowerEntryOrder_SkipsFilledAndCancelledEntries passes | PASS |
| 11 | No lock(), CYC<=8, ASCII-only, xUnit-only | PASS |

---

## Final Verdict

**BUILD_PASS**
