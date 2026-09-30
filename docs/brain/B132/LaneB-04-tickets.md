# B132 LaneB -- Tickets

**Epic**: B132 LaneB
**Defect**: DW-B138 P1 -- Stop Drag Runtime Silent (Diagnostic Phase)
**Plan**: docs/brain/B132/LaneB-02-architecture-plan.md (REVIEW_PASS)
**Plan Review**: docs/brain/B132/LaneB-02-plan-review.md (REVIEW_PASS)
**Status**: TICKETS_COMPLETE

---

## Ticket 1 -- B132 LaneB Diagnostic Prints

**Epic**: B132 LaneB
**Requirement**: DW-B138 P1 -- Stop Drag Runtime Silent (diagnostic phase)
**Plan ref**: docs/brain/B132/LaneB-02-architecture-plan.md (REVIEW_PASS)
**File**: src/PropTraderTools/CopyEngine.cs

---

### Spec Requirement IDs

- **DW-B138**: ATM bracket drag must reach SyncFollowerBracket for Stop1/Stop2/Stop3.
  This ticket implements 4 diagnostic trace points (TP1-TP4) to identify where the dispatch
  chain silently exits before `SyncFollowerBracket` is called.

---

### Changes Required

This ticket adds ONLY observability. Zero behavioral changes. Zero new gate conditions.
All changes are guarded by `_diagnosticMode`. Set `_diagnosticMode = false` to quiesce.

---

#### Change 1 -- `_diagnosticMode` field declaration

**File**: `src/PropTraderTools/CopyEngine.cs`
**Location**: Field declarations block, after L400 (`internal event Action GlobalBeAllDisarmed;`),
before the constructor. Exact line TBD by engineer -- must be inside the field block, not inside
any method body.

Add exactly:
```csharp
// B132 LaneB diagnostic gate -- set to false to disable all TP1-TP4 Print calls.
// Remove this field and all TryLogDragTrace / TryLogSFBTrace calls when DW-B138 is confirmed fixed.
// JS-021: static bool read is lock-free (no torn reads on bool). Not volatile (diagnostic only).
private static bool _diagnosticMode = true;
```

**CYC impact**: 0 (field declaration, no branches).

---

#### Change 2 -- `TryLogDragTrace` helper method (TP1)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Reason for extraction**: `OnOrderUpdate` CYC is pre-existing ~11-18. Adding an inline
`if (_diagnosticMode)` guard would add +1 branch. Extraction to a helper adds +0 branches
to `OnOrderUpdate` (unconditional call).
**Declare near**: `TryHandleBracketDrag` (approximately after L1740).

Add exactly:
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

**Call site**: Add `TryLogDragTrace(e.Order);` in `OnOrderUpdate`:
- **After**: L1299 (`EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState);`)
- **Before**: L1301 (comment: `// HOTFIX-FLAT-DISARM-FOLLOWER:`) which precedes
  `TryFireFollowerBeDisarm(e);` (L1303)
- **New line**: `TryLogDragTrace(e.Order);`

**CYC after addition**:
- `OnOrderUpdate`: UNCHANGED (unconditional call, +0 branches)
- `TryLogDragTrace` (new): CYC = 4

---

#### Change 3 -- TP2 inline guard in `TryHandleBracketDrag`

**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `TryHandleBracketDrag` (L1720)
**Insert**: After L1721 (opening brace `{`), before L1722 (`if (!IsWorkingBracket(order)) return false;`)

Add exactly:
```csharp
if (_diagnosticMode)
    NinjaTrader.Code.Output.Process(
        "[TP2-DRAG] IsWorkingBracket=" + IsWorkingBracket(order)
        + " name=" + (order.Name ?? "null")
        + " state=" + order.OrderState,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
```

**CYC after addition**: `TryHandleBracketDrag` 3 -> 4 (one new `if` branch). Within budget.

---

#### Change 4 -- `TryLogSFBTrace` helper method (TP4)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Reason for extraction**: `SyncFollowerBracket` CYC = 8 (at budget). Adding an inline
`if (_diagnosticMode)` guard would push CYC to 9 -- OVER budget. Extraction adds +0 branches.
**Declare near**: `TryLogDragTrace` (declare after it in the same helper region).

Add exactly:
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

**Call site**: Add `TryLogSFBTrace(acc, leaderOrder, isStop, fo);` in `SyncFollowerBracket`:
- **After**: L2139 (`var fo = FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop, leaderOrder.Name);`)
- **Before**: L2140 (`if (fo == null) // (1)`)
- **New line**: `TryLogSFBTrace(acc, leaderOrder, isStop, fo);`

**CYC after addition**:
- `SyncFollowerBracket`: UNCHANGED at 8 (unconditional call, +0 branches)
- `TryLogSFBTrace` (new): CYC = 2

---

#### Change 5 -- TP3 inline guard in `HandleBracketChange`

**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `HandleBracketChange` (L2336)
**Insert**: After L2347 (`double newPrice = tickSize > 0 ? ... : rawPrice;`),
before L2349 (`foreach (var acc in rule.FollowerAccounts)`)

Add exactly:
```csharp
if (_diagnosticMode)
    NinjaTrader.Code.Output.Process(
        "[TP3-HBC] isStop=" + isStop
        + " leaderName=" + (leaderOrder.Name ?? "null")
        + " rawPrice=" + rawPrice
        + " newPrice=" + newPrice
        + " followerCount=" + rule.FollowerAccounts.Count,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
```

**CYC after addition**: `HandleBracketChange` 7 -> 8 (one new `if` branch).
At the exact CYC=8 limit. Within budget. (Reviewer notation V1 from plan-review: actual
before=7 due to `?.TickSize` null-conditional counted as a decision point; plan stated 6.
Engineer MUST record CYC=8 in SCAN-06 sign-off -- not 7.)

---

#### Change 6 -- New test in `src/PropTraderTools/Tests/CopyEngineTests.cs`

**File**: `src/PropTraderTools/Tests/CopyEngineTests.cs`
**Location**: Add at end of the test class body (before closing `}`)

Add exactly:
```csharp
[Fact]
public void B132_LaneB_DiagnosticMode_FieldExists()
{
    // Assert _diagnosticMode field exists as a private static bool.
    // Confirms the B132 LaneB diagnostic gate is correctly declared.
    var field = typeof(CopyEngine).GetField(
        "_diagnosticMode",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
    );
    Assert.NotNull(field);
    Assert.Equal(typeof(bool), field!.FieldType);
    // Default value must be true (diagnostic mode active).
    Assert.Equal(true, (bool)field.GetValue(null)!);
}
```

**What this asserts**:
1. `_diagnosticMode` is accessible as `private static bool`.
2. Default value is `true` (diagnostic Print calls are active by default).

**xUnit compliance**: Uses `[Fact]`, `Assert.NotNull`, `Assert.Equal`. No NUnit/MSTest.

---

### Method Signatures (all methods introduced or modified)

| Method | File | Signature | New/Modified |
|--------|------|-----------|--------------|
| `TryLogDragTrace` | CopyEngine.cs | `private void TryLogDragTrace(Order order)` | NEW |
| `TryLogSFBTrace` | CopyEngine.cs | `private void TryLogSFBTrace(Account acc, Order leaderOrder, bool isStop, Order? fo)` | NEW |
| `TryHandleBracketDrag` | CopyEngine.cs | (existing signature unchanged) | MODIFIED (TP2 inline) |
| `HandleBracketChange` | CopyEngine.cs | (existing signature unchanged) | MODIFIED (TP3 inline) |
| `OnOrderUpdate` | CopyEngine.cs | (existing signature unchanged) | MODIFIED (TP1 call site) |
| `SyncFollowerBracket` | CopyEngine.cs | (existing signature unchanged) | MODIFIED (TP4 call site) |
| `B132_LaneB_DiagnosticMode_FieldExists` | CopyEngineTests.cs | `[Fact] public void B132_LaneB_DiagnosticMode_FieldExists()` | NEW |

---

### JS Rule Constraints Per Method

| Method | JS Rules |
|--------|----------|
| `TryLogDragTrace` | JS-021: no lock(). JS-001: no throw. JS-002: void, no return null. JS-033: not async. |
| `TryLogSFBTrace` | JS-021: no lock(). JS-001: no throw. JS-002: void, no return null. JS-033: not async. |
| `TryHandleBracketDrag` (TP2 addition) | JS-021: inline `if (_diagnosticMode)` has no shared mutable state. |
| `HandleBracketChange` (TP3 addition) | JS-021: inline `if (_diagnosticMode)` has no shared mutable state. CYC=8 (at limit). |
| `OnOrderUpdate` (call site) | CYC UNCHANGED. JS-021: no lock added. |
| `SyncFollowerBracket` (call site) | CYC UNCHANGED at 8. JS-021: no lock added. |

---

### xUnit Tests

| Test Name | File | Asserts |
|-----------|------|---------|
| `B132_LaneB_DiagnosticMode_FieldExists` | CopyEngineTests.cs | Field `_diagnosticMode` exists as `private static bool` with default value `true` |

**Regression tests (must remain green, no modifications):**

| Test | File | What It Asserts |
|------|------|-----------------|
| `T_B25_03_IsStopLeg_AtmSTPSuffix_ReturnsTrue` | CopyEngineTests.cs L2619 | IsStopLeg returns true for "STP" suffix |
| `SignalOrNameMatchesTestable` tests | CopyEngineTests.cs | signal-first / name-fallback predicate |
| `FindFollowerBracketOrderTestable` tests | CopyEngineTests.cs | leaderName param match |
| `OnOrderUpdate_WithWorkingBracket_DoesNotDispatchCopy` | CopyEngineTests.cs L458 | OOU exists as non-public instance method |

---

### 7-Scan Checklist

```
SCAN-01 LOCK SCAN     grep -r "lock(" src/ --include="*.cs"                    ZERO MATCHES REQUIRED
SCAN-02 THROW SCAN    grep -n "throw new" src/PropTraderTools/CopyEngine.cs     ZERO NEW THROWS
SCAN-03 NULL RETURN   grep -n "return null" src/PropTraderTools/CopyEngine.cs   EXISTING ONLY, NO NEW ADDITIONS
SCAN-04 ASYNC VOID    grep -rn "async void " src/ --include="*.cs"             ZERO MATCHES
SCAN-05 DATETIME NOW  grep -rn "DateTime\.Now" src/ --include="*.cs"           ZERO MATCHES
SCAN-06 CYC BUDGET    TryLogDragTrace<=4, TryHandleBracketDrag<=4, HandleBracketChange<=8, TryLogSFBTrace<=2, SyncFollowerBracket UNCHANGED at <=8
SCAN-07 ASCII SCAN    grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs  ZERO NON-ASCII
```

---

### Acceptance Criteria

1. `_diagnosticMode` static bool field declared in CopyEngine field block, default `true`.
2. `TryLogDragTrace` helper added; called from `OnOrderUpdate` after `EvictDedup` (L1299), before `TryFireFollowerBeDisarm` comment block.
3. TP2 Print added inline in `TryHandleBracketDrag` after opening brace, before `IsWorkingBracket` guard (L1722).
4. `TryLogSFBTrace` helper added; called from `SyncFollowerBracket` after `FindFollowerBracketOrder` (L2139), before `if (fo == null)` (L2140).
5. TP3 Print added inline in `HandleBracketChange` after `newPrice` computed (L2347), before `foreach` loop (L2349).
6. All 7 scans pass (zero violations on SCAN-01 through SCAN-07).
7. `dotnet test` -- all existing B131 tests green; new `B132_LaneB_DiagnosticMode_FieldExists` passes.
8. `powershell -File scripts\ptt-sync-and-verify.ps1` -- 0 MISMATCH lines.
9. F5 pressed in NinjaTrader 8 after sync -- green compile.
10. Completion doc includes section: `PENDING: Director to run drag and paste Output Tab trace in chat.`

---

### B131 LaneA Non-Regression Requirement

The following B131 LaneA source changes are ALREADY IN SOURCE and MUST NOT be removed or modified:

| Symbol | Line | Change |
|--------|------|--------|
| `SignalOrNameMatches` | L2361 | Predicate: signal-first, leaderName fallback |
| `FindFollowerBracketOrder` signature | L2375 | `string? leaderName = null` param |
| `SyncFollowerBracket` call site | L2139 | Passes `leaderOrder.Name` as leaderName arg |

---

### Reviewer Note (from LaneB-02-plan-review.md, Check 5 / V1)

`HandleBracketChange` actual CYC before TP3 addition is **7** (not 6 as stated in plan Section B/G).
The `?.TickSize` null-conditional at L2344 is a McCabe decision point omitted from the plan count.
CYC after TP3 addition = **8** (not 7). This is AT the CYC=8 limit but does NOT exceed it.
**Engineer MUST record CYC=8 (not 7) in SCAN-06 sign-off.**

---

**Status**: TICKETS_COMPLETE
