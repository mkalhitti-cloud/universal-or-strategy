# B137 Tickets

**Block**: B137
**Phase**: 3 — Ticket Generation
**Status**: TICKETS_COMPLETE
**Source plan**: docs/brain/B137/02-architecture-plan.md (REVIEW_PASS — 2026-09-08 third pass)
**Produced by**: ptt-architect
**Date**: 2026-09-08
**Execution order**: T1 → T2 → T3 → T4 (SINGLE-PIPELINE — sequential mandatory)

---

## Pipeline Gate

**SINGLE-PIPELINE** (Q1=YES, Q2=YES per plan review Check A):
- T2 depends on T1: SyncAtmFollowerTarget must be CYC=7 before T2 guard is added.
- T4 depends on T2: SyncAtmFollowerBracket must be CYC=5 before T4 adds the pre-sweep call.
- T3 is independent of T1/T2/T4 (modifies only OrderPassesBracketGate), but execute in order T1→T2→T3→T4 to maintain a clean verification trail.

**Engineer rule**: Do NOT start T2 until T1 SCAN-05 confirms SyncAtmFollowerTarget=7.
**Engineer rule**: Do NOT start T4 until T2 SCAN-05 confirms SyncAtmFollowerBracket=5.

---

## Test File

**New file**: `tests/PropTraderTools.Tests/CopyEngineB137Tests.cs`
**Framework**: xUnit ONLY. NEVER NUnit or MSTest.
**InternalsVisibleTo**: `PropTraderTools.Tests` is already granted at the top of CopyEngine.cs (L46). No new assembly attribute needed.
**Pattern**: Follow existing `CopyEngineB136Tests.cs` structure exactly.

Test-to-ticket assignment summary:

| Test ID | Ticket |
|---------|--------|
| T_B137_01 | T2 (IsNoPriceChange predicate — true path) |
| T_B137_02 | T2 (IsNoPriceChange predicate — false path) |
| T_B137_03 | T2 (SyncAtmFollowerTarget guard — no cancel on price match) |
| T_B137_04 | T2 (SyncAtmFollowerBracket guard — no cancel on price match) |
| T_B137_05 | T2 (regression — cancel fires on real price change) |
| T_B137_06 | T3 (OrderPassesBracketGate — empty signalName routes to ATM path) |
| T_B137_07 | T4 (CancelExistingPttStpDrag — cancels Working PTT-STP-Drag) |
| T_B137_08 | T4 (CancelExistingPttStpDrag — cancels Accepted PTT-STP-Drag) |
| T_B137_09 | T3 (OrderPassesBracketGate — null signalName regression) |

---

## T1 — Phase C Extraction from SyncAtmFollowerTarget

**Ticket ID**: T1
**Title**: Extract Phase C inline block from SyncAtmFollowerTarget to ExecutePhaseCStopReplacement
**Spec requirement IDs**: Structural prerequisite for DW-B147 / DW-B149 (T2). No DW item closed by T1 alone.
**File**: `src/PropTraderTools/CopyEngine.cs` ONLY
**Prerequisite**: None (first ticket in pipeline)
**CYC before**: SyncAtmFollowerTarget = 8 (source-verified L2363-2364)
**CYC after**: SyncAtmFollowerTarget = 7 | ExecutePhaseCStopReplacement (new) = 2

### Method Signatures

**SyncAtmFollowerTarget — signature UNCHANGED, body modified (Phase C lines replaced by call):**
```csharp
private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice, Order? leaderOrder = null)
```

**ExecutePhaseCStopReplacement — NEW method:**
```csharp
// CYC=2. Extracted Phase C block from SyncAtmFollowerTarget (T1 extraction).
// Replaces the inline Phase C code at the end of SyncAtmFollowerTarget (L2439-2442):
//   DeriveLeaderBracketIndex(leaderOrder) + FindLeaderStopPrice(leaderOrder?.Account, bracketIdx)
//   + CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp).
// The null-conditional leaderOrder?.Account contributes +1 McCabe branch (lizard convention),
// making this CYC=2 (base=1 + ?.=1). Extraction reduces SyncAtmFollowerTarget from CYC=8 to CYC=7.
// ZERO behavior change: identical logic, moved out of SyncAtmFollowerTarget inline body.
// JS-021: no lock. JS-001: no throw (delegates to CreateFollowerReplacementStop which has its own catch).
// JS-002: void return. NT8-014: PTT-STP-Drag via CreateFollowerReplacementStop.
private void ExecutePhaseCStopReplacement(Account acc, Order fo, Order? leaderOrder)
```

### Step-by-Step Implementation Instructions

**Step 1 — Locate Phase C in SyncAtmFollowerTarget.**
The Phase C block is the last 3 statements of SyncAtmFollowerTarget, currently at lines 2439-2442:
```csharp
// [Phase C -- B132 LaneA] Replace follower's OCO-cancelled stop after target drag (DW-B141)
int bracketIdx = DeriveLeaderBracketIndex(leaderOrder);
double stp = FindLeaderStopPrice(leaderOrder?.Account, bracketIdx);
CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp);
```
These 4 lines (1 comment + 3 statements) are the ENTIRE Phase C block. Nothing outside these lines is part of T1.

**Step 2 — Add new private method ExecutePhaseCStopReplacement.**
Insert it AFTER the existing `CreateFollowerReplacementStop` method body in `CopyEngine.cs`.
The exact body:
```csharp
// CYC=2. Extracted Phase C block from SyncAtmFollowerTarget (T1 extraction — B137).
// Replaces inline Phase C code (L2439-2442 pre-B137):
//   DeriveLeaderBracketIndex + FindLeaderStopPrice + CreateFollowerReplacementStop.
// McCabe branches: base(1) + leaderOrder?.Account null-conditional(1) = CYC=2.
// Extraction reduces SyncAtmFollowerTarget from CYC=8 to CYC=7 (removes ?.  branch from parent).
// ZERO behavior change. JS-021: no lock. JS-001: delegates to CreateFollowerReplacementStop catch.
// JS-002: void return. ASCII-only. No DateTime. No FontFamily.
private void ExecutePhaseCStopReplacement(Account acc, Order fo, Order? leaderOrder)
{
    int bracketIdx = DeriveLeaderBracketIndex(leaderOrder);
    double stp = FindLeaderStopPrice(leaderOrder?.Account, bracketIdx);
    CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp);
}
```

**Step 3 — Replace the 4-line Phase C block in SyncAtmFollowerTarget with a single call.**
Remove lines 2439-2442 (comment + 3 statements). Replace with:
```csharp
ExecutePhaseCStopReplacement(acc, fo, leaderOrder); // T1 B137: Phase C extracted
```

**Step 4 — Update the CYC comment on SyncAtmFollowerTarget.**
Change the existing CYC comment block (L2362-2364) from:
```csharp
// CYC=8: (1) acc null, (2) fo null, (3) foreach A-Prime, (4) OrderState==Working,
//        (5) Name=="PTT-TGT-Drag", (6) catch A-Prime, (7) Block A catch, (8) newTarget null.
```
To:
```csharp
// CYC=7: (1) acc null, (2) fo null, (3) foreach A-Prime, (4) OrderState==Working,
//        (5) Name=="PTT-TGT-Drag", (6) catch A-Prime, (7) Block A catch.
// T1 B137: Phase C ?.leaderOrder?.Account branch extracted to ExecutePhaseCStopReplacement (CYC=2).
```

**Step 5 — Verify zero behavior change.**
The extracted code calls the SAME three methods in the SAME order with the SAME arguments. No logic is added, removed, or reordered. `ExecutePhaseCStopReplacement` is called unconditionally at the same point in execution (end of SyncAtmFollowerTarget body) as the inline code was.

**Step 6 — Run SCAN-05.**
`python scripts/complexity_audit.py` must report:
- `SyncAtmFollowerTarget`: CYC = 7
- `ExecutePhaseCStopReplacement`: CYC = 2

### Tests Assigned to T1

T1 is a pure structural refactor (zero behavior change). No new [Fact] tests are authored in T1.

**Regression test T_B137_05** (authored in T2 ticket but validates T1 regression too):
Confirms that when `rawPrice != newPrice`, `SyncAtmFollowerTarget` still reaches cancel+resubmit.
Phase C execution is part of the same regression path (T_B137_05 confirms the full method still fires on a real drag after T1 extraction).

### 7-Scan Checklist — T1

```
SCAN-01: grep -r "lock(" src/ --include="*.cs"
         Expected: 0 matches
         Rationale: ExecutePhaseCStopReplacement is static-context-safe; no lock introduced.

SCAN-02: grep -rn "async void " src/ --include="*.cs"
         Expected: 0 matches
         Rationale: No async code introduced.

SCAN-03: git diff HEAD src/PropTraderTools/CopyEngine.cs | grep "^+" | grep "return null;"
         Expected: 0 matches
         Rationale: ExecutePhaseCStopReplacement returns void. No return null added.
         NOTE: pre-existing Order? return null at L2629 (FindFollowerBracketOrder) is not in T1 diff.

SCAN-04: dotnet build
         Expected: 0 errors, 0 warnings
         Rationale: ExecutePhaseCStopReplacement signature must compile; call site must compile.

SCAN-05: python scripts/complexity_audit.py
         Expected: SyncAtmFollowerTarget = 7, ExecutePhaseCStopReplacement = 2
         All other methods unchanged: SyncAtmFollowerBracket=4, OrderPassesBracketGate=2,
         MatchesLeaderName=5, FindFollowerBracketOrder(list)=7.

SCAN-06: dotnet test
         Expected: 0 Failed, 0 Errors
         Rationale: No test file added in T1. All existing tests must still pass (no regression).

SCAN-07: dotnet csharpier check src/
         Expected: clean (no formatting issues)
         Rationale: Use standard C# bracing. CSharpier will fix any deviations.
```

---

## T2 — IsNoPriceChange Guard (DW-B147 + DW-B149)

**Ticket ID**: T2
**Title**: Add IsNoPriceChange early-return guard to SyncAtmFollowerTarget and SyncAtmFollowerBracket
**Spec requirement IDs**: DW-B147 (ARM event spurious cancel+resubmit), DW-B149 (ChangeSubmitted race second TP3-HBC)
**Spec references**: specs/002-trade-copier-spec.html §DW-B147 (L40557), §B136 DW-B149 (L40683)
**File**: `src/PropTraderTools/CopyEngine.cs` ONLY
**Prerequisite**: T1 MUST be complete. SyncAtmFollowerTarget must be CYC=7 (verify SCAN-05 from T1 before starting).
**CYC before** (entering T2): SyncAtmFollowerTarget=7, SyncAtmFollowerBracket=4
**CYC after**: SyncAtmFollowerTarget=8 (AT LIMIT), SyncAtmFollowerBracket=5, IsNoPriceChange(new)=1

### Method Signatures

**IsNoPriceChange — NEW private static method:**
```csharp
// CYC=1. Pure predicate: returns true when currentPrice == newPrice (no price change occurred).
// Used as early-return guard in SyncAtmFollowerBracket and SyncAtmFollowerTarget to suppress
// spurious cancel+resubmit cycles caused by ARM events (DW-B147) or ChangeSubmitted races (DW-B149).
// CYC=1: pure expression method body, no branches.
// JS-021: no lock (static, no shared state). JS-001: no throw. JS-002: returns bool, not null.
// JS-036: stack-only, zero allocation. ASCII-only. No DateTime. No FontFamily.
private static bool IsNoPriceChange(double currentPrice, double newPrice)
    => currentPrice == newPrice;
```

**IsNoPriceChangeTestable — NEW internal static test seam:**
```csharp
// Test seam for xUnit access. InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
internal static bool IsNoPriceChangeTestable(double currentPrice, double newPrice)
    => IsNoPriceChange(currentPrice, newPrice);
```

**SyncAtmFollowerTarget — signature UNCHANGED, guard inserted:**
```csharp
private void SyncAtmFollowerTarget(Account acc, Order fo, double newPrice, Order? leaderOrder = null)
```

**SyncAtmFollowerBracket — signature UNCHANGED, guard inserted:**
```csharp
private void SyncAtmFollowerBracket(Account acc, Order fo, double newPrice)
```

### Step-by-Step Implementation Instructions

**Step 1 — Confirm T1 prerequisite.**
Run `python scripts/complexity_audit.py`. Confirm SyncAtmFollowerTarget = 7. If CYC=8, T1 is not complete. STOP and complete T1 first.

**Step 2 — Add IsNoPriceChange and its test seam.**
Insert both methods (IsNoPriceChange + IsNoPriceChangeTestable) near the other static predicate helpers in CopyEngine.cs (near OrderPassesBracketGate or MatchesLeaderName area). The exact location does not affect correctness but keep related static predicates together.

**Step 3 — Insert guard in SyncAtmFollowerTarget.**
Location: AFTER the `if (fo == null) return;` guard (currently line 2376-2377) and BEFORE the Block A-Prime `foreach` loop (currently line 2382).
The existing code after `if (fo == null) return;` is:
```csharp
if (fo == null) // (2)
    return;

// Block A-Prime -- cancel any existing PTT-TGT-Drag for this instrument on the follower.
foreach (var o in acc.Orders.ToList())
```
Insert after the `if (fo == null) return;` block:
```csharp
if (IsNoPriceChange(fo.LimitPrice, newPrice)) // (3) T2 B137 DW-B147/DW-B149 guard
    return;
```
So the sequence becomes:
```csharp
if (acc == null) // (1)
    return;
if (fo == null) // (2)
    return;
if (IsNoPriceChange(fo.LimitPrice, newPrice)) // (3) T2 B137 DW-B147/DW-B149 guard
    return;

// Block A-Prime -- cancel any existing PTT-TGT-Drag ...
foreach (var o in acc.Orders.ToList())
```

**Step 4 — Insert guard in SyncAtmFollowerBracket.**
Location: AFTER the `if (fo == null) return;` guard (currently line 2315-2316) and BEFORE Block A (Cancel).
The existing code after `if (fo == null) return;` is:
```csharp
if (fo == null) // (2)
    return;

// Block A -- Cancel only. Independent: if Cancel throws, Block B still runs.
try
{
    acc.Cancel(new Order[] { fo });
```
Insert after the `if (fo == null) return;` block:
```csharp
if (IsNoPriceChange(fo.StopPrice, newPrice)) // (3) T2 B137 DW-B147/DW-B149 guard
    return;
```
So the sequence becomes:
```csharp
if (acc == null) // (1)
    return;
if (fo == null) // (2)
    return;
if (IsNoPriceChange(fo.StopPrice, newPrice)) // (3) T2 B137 DW-B147/DW-B149 guard
    return;

// Block A -- Cancel only. Independent: if Cancel throws, Block B still runs.
try
```

**Step 5 — Update CYC comment on SyncAtmFollowerTarget.**
Change the CYC comment (set by T1 to CYC=7) to CYC=8:
```csharp
// CYC=8: (1) acc null, (2) fo null, (3) IsNoPriceChange guard [T2 B137],
//        (4) foreach A-Prime, (5) OrderState==Working, (6) Name=="PTT-TGT-Drag",
//        (7) catch A-Prime, (8) Block A catch.
// AT LIMIT. T2 B137: DW-B147/DW-B149 IsNoPriceChange guard added.
// NOTE: newTarget null check in Block B is inside try/catch and not counted separately.
```
Wait — review the original CYC=8 branches from the plan (L2363-2364 source comment):
```
(1) acc null, (2) fo null, (3) foreach A-Prime, (4) OrderState==Working,
(5) Name=="PTT-TGT-Drag", (6) catch A-Prime, (7) Block A catch, (8) newTarget null.
```
After T1 extracted Phase C (CYC=7, branch list is (1)-(7) above minus the ?.leaderOrder contribution).
After T2 adds guard: the new guard is the new branch (3). The prior branch numbering shifts. Final CYC=8:
```csharp
// CYC=8: (1) acc null, (2) fo null, (3) IsNoPriceChange guard [T2],
//        (4) foreach A-Prime, (5) OrderState==Working, (6) Name=="PTT-TGT-Drag",
//        (7) catch A-Prime, (8) Block A catch.
// AT LIMIT. T2 B137: DW-B147/DW-B149 guard. T1 B137: Phase C -> ExecutePhaseCStopReplacement.
```

**Step 6 — Update CYC comment on SyncAtmFollowerBracket.**
Change existing CYC=4 comment (L2301) to CYC=5:
```csharp
// CYC=5: (1) acc null guard, (2) fo null guard, (3) IsNoPriceChange guard [T2 B137],
//        (4) Block A catch, (5) newStop null guard.
// T2 B137: DW-B147/DW-B149 IsNoPriceChange guard added after fo null check.
// Two independent try/catch blocks -- exception handlers add 0 McCabe branches each.
// JS-021: no lock. JS-001: two independent try/catch -- no throw in hot path.
```
Note: The original CYC=4 comment listed "(3) newStop null guard in Block B" and "Two independent try/catch blocks -- exception handlers add 0 McCabe branches each." After T2, branch (3) is the IsNoPriceChange guard; the Block B newStop null becomes branch (5).

**Step 7 — Author the test file CopyEngineB137Tests.cs (new file).**
See xUnit Tests section below for all 9 [Fact] definitions.

**Step 8 — Run SCAN-05.**
`python scripts/complexity_audit.py` must report:
- `SyncAtmFollowerTarget`: CYC = 8 (AT LIMIT — must not exceed 8)
- `SyncAtmFollowerBracket`: CYC = 5
- `IsNoPriceChange`: CYC = 1

### xUnit Tests Assigned to T2

**Test file**: `tests/PropTraderTools.Tests/CopyEngineB137Tests.cs` (NEW — create in T2)
All 9 B137 tests live in this file. Author all 9 here; they will be exercised in T3/T4 as well.

```csharp
[Fact]
public void T_B137_01_IsNoPriceChange_SamePriceReturnsTrue()
{
    // Arrange: currentPrice == newPrice
    double price = 100.25;
    // Act
    bool result = CopyEngine.IsNoPriceChangeTestable(price, price);
    // Assert
    Assert.True(result);
}

[Fact]
public void T_B137_02_IsNoPriceChange_DifferentPriceReturnsFalse()
{
    // Arrange: currentPrice != newPrice
    double currentPrice = 100.25;
    double newPrice = 100.50;
    // Act
    bool result = CopyEngine.IsNoPriceChangeTestable(currentPrice, newPrice);
    // Assert
    Assert.False(result);
}

[Fact]
public void T_B137_03_SyncAtmFollowerTarget_NoCancelWhenPriceUnchanged()
{
    // Arrange: fo.LimitPrice == newPrice -- guard must suppress cancel+resubmit
    // Use Account/Order stub pattern from B136Tests.cs.
    // Set fo stub with LimitPrice = 100.25; newPrice = 100.25.
    // Act: call CopyEngine (via testable seam or stub injection) with fo.LimitPrice == newPrice.
    // Assert: acc.Cancel was NOT called (DW-B149 guard fires, method returns early).
    // (Implementation: inject CancelCallCount tracker into acc stub; verify count = 0.)
}

[Fact]
public void T_B137_04_SyncAtmFollowerBracket_NoCancelWhenPriceUnchanged()
{
    // Arrange: fo.StopPrice == newPrice -- guard must suppress cancel+resubmit
    // Set fo stub with StopPrice = 99.75; newPrice = 99.75.
    // Act: call SyncAtmFollowerBracket (via testable seam) with fo.StopPrice == newPrice.
    // Assert: acc.Cancel was NOT called (DW-B147 guard fires, method returns early).
}

[Fact]
public void T_B137_05_SyncMethods_CancelFiresOnRealPriceChange()
{
    // Arrange: fo.LimitPrice != newPrice (real drag scenario)
    // Set fo stub with LimitPrice = 100.25; newPrice = 100.50 (price changed).
    // Act: call SyncAtmFollowerTarget with different prices.
    // Assert: acc.Cancel WAS called (real drag proceeds; guard does NOT fire).
    // This is the regression test: confirms T1 extraction and T2 guard do not break real drags.
}

[Fact]
public void T_B137_06_OrderPassesBracketGate_EmptySignalRoutesToAtmPath_FindsStop3()
{
    // Arrange: signalName="", leaderName="Stop3", isStop=true
    // order stub: Name="Stop3", FromEntrySignal=null, OrderType=StopMarket, OrderState=Working.
    // Act
    bool result = CopyEngine.OrderPassesBracketGateTestable(order, signalName: "", leaderName: "Stop3", isStop: true);
    // Assert: true (ATM path taken; MatchesLeaderName finds "Stop3")
    // NOTE: FAILS on pre-B137 code (DW-B150 bug). PASSES after T3 fix.
    Assert.True(result);
}

[Fact]
public void T_B137_07_CancelExistingPttStpDrag_CancelsWorkingDrag()
{
    // Arrange: acc has one PTT-STP-Drag order in Working state for same instrument.
    // Act: call SyncAtmFollowerBracket (T4 path) or inject CancelExistingPttStpDrag directly.
    // Assert: acc.Cancel was called with the Working PTT-STP-Drag order (DW-B151 fix).
}

[Fact]
public void T_B137_08_CancelExistingPttStpDrag_CancelsAcceptedDrag()
{
    // Arrange: acc has one PTT-STP-Drag order in Accepted state for same instrument.
    // Act: same as T_B137_07 but order state = Accepted.
    // Assert: acc.Cancel was called (Accepted orders are pre-swept by T4 fix).
}

[Fact]
public void T_B137_09_OrderPassesBracketGate_NullSignalRoutesToAtmPath_Regression()
{
    // Arrange: signalName=null, leaderName="Stop3", isStop=true
    // order stub: Name="Stop3", FromEntrySignal=null (ATM bracket — unchanged from pre-B137).
    // Act
    bool result = CopyEngine.OrderPassesBracketGateTestable(order, signalName: null, leaderName: "Stop3", isStop: true);
    // Assert: true (null signalName still takes ATM path — unchanged behavior; regression guard).
    Assert.True(result);
}
```

**Test implementation notes**:
- T_B137_01/02: Pure static predicate — no stubs needed. Direct call.
- T_B137_03/04: Use Account/Order stub from B136Tests pattern. Inject fo with matching price to trigger early return; verify Cancel not called.
- T_B137_05: Inject fo with different price; verify Cancel IS called.
- T_B137_06: Direct static call. Fails pre-B137 (bug), passes post-T3.
- T_B137_07/08: Inject acc stub with pre-existing PTT-STP-Drag in Working/Accepted state; call SyncAtmFollowerBracket (or CancelExistingPttStpDrag directly via internal seam). Verify Cancel called.
- T_B137_09: Same as T_B137_06 but signalName=null — regression, must always pass.

### 7-Scan Checklist — T2

```
SCAN-01: grep -r "lock(" src/ --include="*.cs"
         Expected: 0 matches
         Rationale: IsNoPriceChange is a static pure predicate — no lock, no shared state.

SCAN-02: grep -rn "async void " src/ --include="*.cs"
         Expected: 0 matches
         Rationale: No async code introduced.

SCAN-03: git diff HEAD src/PropTraderTools/CopyEngine.cs | grep "^+" | grep "return null;"
         Expected: 0 matches
         Rationale: IsNoPriceChange returns bool. Guards return void (early return from method, not return null).
         Pre-existing Order? return null at L2629 is not in T2 diff.

SCAN-04: dotnet build
         Expected: 0 errors, 0 warnings
         Rationale: IsNoPriceChange, IsNoPriceChangeTestable must compile.
                    Guard insertions must compile (fo.LimitPrice and fo.StopPrice are valid Order properties).

SCAN-05: python scripts/complexity_audit.py
         Expected: SyncAtmFollowerTarget=8 (AT LIMIT — must not exceed 8),
                   SyncAtmFollowerBracket=5, IsNoPriceChange=1, ExecutePhaseCStopReplacement=2.
         All other methods unchanged: OrderPassesBracketGate=2, MatchesLeaderName=5,
         FindFollowerBracketOrder(list)=7.

SCAN-06: dotnet test
         Expected: 0 Failed, 0 Errors
         Includes: T_B137_01 through T_B137_09 (all 9 tests in CopyEngineB137Tests.cs).
         Note: T_B137_06 tests OrderPassesBracketGate with signalName="". It is authored in T2
               but it MUST FAIL on pre-T3 code (DW-B150 bug still present at end of T2).
               That is expected — the test will pass once T3 is complete.
               For T2 SCAN-06: either mark T_B137_06 [Skip] until T3, or confirm expected failure
               is the documented pre-T3 behavior. Engineer chooses: [Skip("DW-B150: passes after T3")]
               is the recommended approach to avoid false-red in T2.

SCAN-07: dotnet csharpier check src/
         Expected: clean
```

---

## T3 — OrderPassesBracketGate Empty-String Condition Fix (DW-B150)

**Ticket ID**: T3
**Title**: Fix OrderPassesBracketGate branch (1) to treat empty signalName as ATM path
**Spec requirement IDs**: DW-B150 (NEW P1 — OrderPassesBracketGate empty-string signalName takes signal path, fo=NULL on stop drag when no PTT-STP-Drag yet)
**Spec references**: specs/002-trade-copier-spec.html §section-b135, §section-b136 (DW-B150 root cause confirmed B137 plan)
**File**: `src/PropTraderTools/CopyEngine.cs` ONLY
**Prerequisite**: None (T3 is independent; modifies only OrderPassesBracketGate, no overlap with T1/T2/T4). Execute after T2 in pipeline order.
**CYC before**: OrderPassesBracketGate = 2 (source-verified L2668)
**CYC after**: OrderPassesBracketGate = 2 (UNCHANGED — condition expression change, not a new branch)
**MatchesLeaderName**: CYC = 5, NOT modified

### Method Signatures

**OrderPassesBracketGate — signature UNCHANGED, condition expression changed:**
```csharp
private static bool OrderPassesBracketGate(
    Order order,
    string? signalName,
    string? leaderName,
    bool isStop)
```
Signature is identical before and after T3. Only the branch (1) condition expression inside the body changes.

### Step-by-Step Implementation Instructions

**Step 1 — Locate OrderPassesBracketGate in CopyEngine.cs.**
Lines 2671-2680 (current source). Body is:
```csharp
private static bool OrderPassesBracketGate(
    Order order,
    string? signalName,
    string? leaderName,
    bool isStop)
{
    if (signalName != null)                                    // (1) signal path: exact match only
        return order.FromEntrySignal == signalName;
    return MatchesLeaderName(order, leaderName, isStop);       // ATM path: exact name OR PTT-prefix
}
```

**Step 2 — Change branch (1) condition expression.**
Change ONLY the `if` condition on branch (1). The `return` body inside the `if` is NOT changed.

BEFORE:
```csharp
if (signalName != null)                                    // (1) signal path: exact match only
    return order.FromEntrySignal == signalName;
```

AFTER:
```csharp
if (!string.IsNullOrEmpty(signalName))                     // (1) signal path: non-empty only -- null OR "" = ATM path [T3 B137 DW-B150]
    return order.FromEntrySignal == signalName;
```

That is the ENTIRE change to the method body. One condition expression replaced. Nothing else.

**Step 3 — Update the CYC comment and signal-path inline comment.**
The CYC comment at L2668 states:
```csharp
// CYC=2: base(1) + if(signalName != null)(1) = 2. Well within <= 8.
```
Update to:
```csharp
// CYC=2: base(1) + if(!string.IsNullOrEmpty(signalName))(1) = 2. Well within <= 8.
// T3 B137 DW-B150: condition changed from (signalName != null) to (!string.IsNullOrEmpty(signalName)).
// Empty string now routes to ATM path (MatchesLeaderName), not signal path.
// Root cause fixed: leaderOrder.FromEntrySignal="" (NT8 ATM bracket state-transition event)
//   was routing to signal path, comparing null == "" = FALSE, returning fo=NULL.
//   After fix: !IsNullOrEmpty("") = false -> ATM path -> MatchesLeaderName -> Stop3 found.
```

The signal-path comment on the branch line is updated in Step 2 above (inline comment change).

**Step 4 — Verify MatchesLeaderName is NOT modified.**
Inspect `MatchesLeaderName` body (L2643-2654). Confirm it is exactly as sourced — no changes.

**Step 5 — Confirm reachability of the fix.**
The fixed condition is reachable because:
- `signalName = ""` occurs when `leaderOrder.FromEntrySignal = ""` (NT8 ATM bracket state-transition events — confirmed in plan DW-B150 root cause section).
- After fix: `!string.IsNullOrEmpty("")` = false → ATM path → `MatchesLeaderName(order, "Stop3", true)` → `order.Name == "Stop3"` → true → fo=Stop3 returned.
- Test T_B137_06 directly validates this path.

**Step 6 — Confirm regression-safe for non-empty signalName.**
- `signalName = "SomeSignal"`: `!string.IsNullOrEmpty("SomeSignal")` = true → signal path (unchanged).
- `signalName = null`: `!string.IsNullOrEmpty(null)` = false → ATM path (unchanged from before).
- Test T_B137_09 validates the null regression case.

**Step 7 — Remove [Skip] from T_B137_06 in test file.**
T_B137_06 was authored in T2 with `[Skip("DW-B150: passes after T3")]` (if the engineer followed T2 SCAN-06 guidance). Remove the Skip attribute so it runs in SCAN-06 for T3.

### Tests Assigned to T3

Tests T_B137_06 and T_B137_09 were authored in T2 (file `CopyEngineB137Tests.cs` already exists). T3's SCAN-06 verifies they pass:

- **T_B137_06**: `OrderPassesBracketGateTestable(order, signalName: "", leaderName: "Stop3", isStop: true)` where `order.Name="Stop3"`, `order.FromEntrySignal=null` → **Assert.True**. This test validates the DW-B150 fix directly.
- **T_B137_09**: `OrderPassesBracketGateTestable(order, signalName: null, leaderName: "Stop3", isStop: true)` where `order.Name="Stop3"` → **Assert.True**. Regression guard — null signalName still takes ATM path.

### 7-Scan Checklist — T3

```
SCAN-01: grep -r "lock(" src/ --include="*.cs"
         Expected: 0 matches
         Rationale: OrderPassesBracketGate is static; no lock introduced.

SCAN-02: grep -rn "async void " src/ --include="*.cs"
         Expected: 0 matches
         Rationale: No async code introduced.

SCAN-03: git diff HEAD src/PropTraderTools/CopyEngine.cs | grep "^+" | grep "return null;"
         Expected: 0 matches
         Rationale: T3 adds no return null. OrderPassesBracketGate returns bool.
                    Pre-existing Order? return null at L2629 is not in T3 diff.

SCAN-04: dotnet build
         Expected: 0 errors, 0 warnings
         Rationale: string.IsNullOrEmpty is BCL static, no new references needed.
                    Condition change is syntactically straightforward.

SCAN-05: python scripts/complexity_audit.py
         Expected: OrderPassesBracketGate=2 (UNCHANGED — condition change, not new branch).
                   MatchesLeaderName=5 (UNCHANGED — not modified).
                   SyncAtmFollowerTarget=8, SyncAtmFollowerBracket=5, IsNoPriceChange=1,
                   ExecutePhaseCStopReplacement=2, FindFollowerBracketOrder(list)=7.

SCAN-06: dotnet test
         Expected: 0 Failed, 0 Errors
         Includes all 9 B137 tests. T_B137_06 must PASS (no Skip attribute).
         T_B137_09 must PASS (regression: null signalName unchanged).

SCAN-07: dotnet csharpier check src/
         Expected: clean
```

---

## T4 — CancelExistingPttStpDrag Block A-Prime Extraction for SyncAtmFollowerBracket (DW-B151)

**Ticket ID**: T4
**Title**: Add CancelExistingPttStpDrag extracted helper and call it from SyncAtmFollowerBracket
**Spec requirement IDs**: DW-B151 (NEW P1 — SyncAtmFollowerBracket missing Block A-Prime pre-sweep, PTT-STP-Drag accumulates on repeated stop drags)
**Spec references**: specs/002-trade-copier-spec.html §section-dw-b137, §section-b136 (DW-B151 NEW)
**File**: `src/PropTraderTools/CopyEngine.cs` ONLY
**Prerequisite**: T2 MUST be complete. SyncAtmFollowerBracket must be CYC=5 (verify SCAN-05 from T2 before starting).
**CYC before** (entering T4): SyncAtmFollowerBracket=5
**CYC after**: SyncAtmFollowerBracket=6 | CancelExistingPttStpDrag(new)=6-7 (both ≤ 8)

### Method Signatures

**CancelExistingPttStpDrag — NEW private instance method:**
```csharp
// CYC=6-7. Block A-Prime pre-sweep for SyncAtmFollowerBracket (T4 extraction — B137 DW-B151).
// Cancels any Working or Accepted PTT-STP-Drag for the same instrument on the follower account.
// Prevents accumulation of Working PTT-STP-Drag orders on repeated stop drag events.
// Mirrors SyncAtmFollowerTarget A-Prime pattern (lines 2382-2397 pre-B137); adds Accepted filter.
// OrderState filter: Working || Accepted ONLY (not Submitted -- ChangeSubmitted is in-flight, unsafe to cancel).
// McCabe branches: base(1) + foreach(1) + if-opening(1) + ||(1) + &&Name(1) + &&Instrument(1) + ?.(1) = CYC=7
//   Loose count (&&Instrument and ?. as one): CYC=6. Both bounds <= 8. Compliant either way.
// JS-001: try/catch -- no rethrow. JS-021: no lock. JS-002: void return.
// acc.Orders.ToList(): thread-safe snapshot. Established pattern (SyncAtmFollowerTarget L2382).
// acc.Cancel(new Order[] { o }): AddOnBase-available. Established pattern (L2390).
// ASCII-only. No DateTime. No FontFamily.
private void CancelExistingPttStpDrag(Account acc, Order fo)
```

**SyncAtmFollowerBracket — signature UNCHANGED, single call added:**
```csharp
private void SyncAtmFollowerBracket(Account acc, Order fo, double newPrice)
```

### Step-by-Step Implementation Instructions

**Step 1 — Confirm T2 prerequisite.**
Run `python scripts/complexity_audit.py`. Confirm SyncAtmFollowerBracket = 5. If CYC=4, T2 is not complete. STOP and complete T2 first.

**Step 2 — Add CancelExistingPttStpDrag method.**
Insert it near SyncAtmFollowerBracket in CopyEngine.cs (place immediately after or before the SyncAtmFollowerBracket body for co-location). Exact body:

```csharp
// CYC=6-7. Block A-Prime pre-sweep for SyncAtmFollowerBracket (T4 extraction -- B137 DW-B151).
// Cancels any Working or Accepted PTT-STP-Drag for the same instrument on the follower account.
// Prevents accumulation of Working PTT-STP-Drag orders on repeated stop drag events.
// Mirrors SyncAtmFollowerTarget A-Prime pattern (L2382-2397 pre-B137); adds Accepted filter.
// OrderState filter: Working || Accepted ONLY (not Submitted -- ChangeSubmitted is in-flight, unsafe to cancel).
// McCabe: base(1) + foreach(1) + if(1) + ||(1) + &&Name(1) + &&Instrument(1) + ?.(1) = CYC 6-7 (<= 8).
// JS-001: try/catch -- no rethrow. JS-021: no lock. JS-002: void return.
// acc.Orders.ToList(): thread-safe snapshot. acc.Cancel(new Order[] { o }): AddOnBase pattern (L2390).
// ASCII-only. No DateTime. No FontFamily.
private void CancelExistingPttStpDrag(Account acc, Order fo)
{
    foreach (var o in acc.Orders.ToList())
    {
        if ((o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted)
            && o.Name == "PTT-STP-Drag"
            && o.Instrument?.FullName == fo.Instrument?.FullName)
        {
            try
            {
                acc.Cancel(new Order[] { o });
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke(acc.Name + ": STP pre-cancel error: " + ex.Message);
            }
        }
    }
}
```

**Step 3 — Insert call to CancelExistingPttStpDrag in SyncAtmFollowerBracket.**
Location: AFTER the IsNoPriceChange guard (added in T2) and BEFORE Block A (Cancel fo).
The T2 state of SyncAtmFollowerBracket is:
```csharp
if (acc == null) // (1)
    return;
if (fo == null) // (2)
    return;
if (IsNoPriceChange(fo.StopPrice, newPrice)) // (3) T2 B137
    return;

// Block A -- Cancel only. Independent: if Cancel throws, Block B still runs.
try
{
    acc.Cancel(new Order[] { fo });
```
Insert exactly ONE new statement after the IsNoPriceChange guard and before Block A:
```csharp
CancelExistingPttStpDrag(acc, fo); // T4 B137 Block A-Prime pre-sweep (DW-B151)
```
So the sequence becomes:
```csharp
if (acc == null) // (1)
    return;
if (fo == null) // (2)
    return;
if (IsNoPriceChange(fo.StopPrice, newPrice)) // (3) T2 B137
    return;

CancelExistingPttStpDrag(acc, fo); // T4 B137 Block A-Prime pre-sweep (DW-B151)

// Block A -- Cancel only. Independent: if Cancel throws, Block B still runs.
try
{
    acc.Cancel(new Order[] { fo });
```

**CRITICAL**: `CancelExistingPttStpDrag(acc, fo)` is a method CALL — it is NOT a branch. McCabe complexity counts control-flow branches (if/foreach/while/&&/||/??/null-conditional). A bare method call adds 0 to the caller's CYC. SyncAtmFollowerBracket CYC goes from 5 (after T2) to 5+0=**6** (after T4). The foreach/if/||/&&/null-conditional branches are all inside CancelExistingPttStpDrag, counted in THAT method's own CYC.

**Step 4 — Update the CYC comment on SyncAtmFollowerBracket.**
Change from CYC=5 (T2 state) to CYC=6:
```csharp
// CYC=6: (1) acc null guard, (2) fo null guard, (3) IsNoPriceChange guard [T2 B137],
//        (4) Block A catch, (5) Block B catch, (6) newStop null guard.
// T4 B137: CancelExistingPttStpDrag(acc, fo) call added before Block A (DW-B151 pre-sweep).
//   Method call adds 0 McCabe branches. CancelExistingPttStpDrag CYC counted in that method.
// T2 B137: DW-B147/DW-B149 IsNoPriceChange guard.
// Two independent try/catch blocks -- exception handlers add 0 McCabe branches each.
// JS-021: no lock. JS-001: two independent try/catch -- no throw in hot path.
```
Note: Reviewing the source branches:
- (1) acc null
- (2) fo null
- (3) IsNoPriceChange guard [NEW T2]
- Block A catch: the try/catch adds 1 McCabe branch per codebase convention
- Block B catch: the second try/catch adds 1 McCabe branch
- (6) newStop null check inside Block B

This matches CYC=6 exactly. The CYC=4 original listed "(1) acc null guard, (2) fo null guard, (3) newStop null guard" and "Two independent try/catch blocks -- exception handlers add 0 McCabe branches each." That comment was incorrect about catch blocks (the actual CYC=4 source-verified value L2301 lists only 3 guards — catches add 0 per that source comment convention). To stay consistent with the codebase convention: use the same counting as the existing source comments. Update comment to match what the tool reports.

**Step 5 — Verify CancelExistingPttStpDrag body matches SyncAtmFollowerTarget A-Prime pattern.**
Compare to existing SyncAtmFollowerTarget lines 2382-2397:
- Source A-Prime uses `OrderState.Working` only. T4 adds `|| OrderState.Accepted`.
- Source A-Prime uses `o.Name == "PTT-TGT-Drag"`. T4 uses `o.Name == "PTT-STP-Drag"`.
- Source A-Prime uses `o.Instrument?.FullName == fo.Instrument?.FullName`. T4 uses the same pattern.
- Source A-Prime uses `try { acc.Cancel(new Order[] { o }); }`. T4 uses the same.
All differences are intentional (stop vs target, Accepted added). Structure is identical.

**Step 6 — Run SCAN-05.**
`python scripts/complexity_audit.py` must report:
- `SyncAtmFollowerBracket`: CYC = 6
- `CancelExistingPttStpDrag`: CYC ≤ 8 (expect 6 or 7 depending on counting convention)

### Tests Assigned to T4

Tests T_B137_07 and T_B137_08 were authored in T2 (file `CopyEngineB137Tests.cs` already exists). T4's SCAN-06 verifies they pass:

- **T_B137_07**: Stub acc with one Working PTT-STP-Drag order for the same instrument. Call `SyncAtmFollowerBracket` or invoke `CancelExistingPttStpDrag` directly via internal test seam. **Assert.True** that `acc.Cancel` was called with that order. Validates DW-B151 Working-state pre-sweep.

- **T_B137_08**: Stub acc with one Accepted PTT-STP-Drag order for the same instrument. Same flow. **Assert.True** that `acc.Cancel` was called. Validates DW-B151 Accepted-state pre-sweep (the `|| OrderState.Accepted` extension beyond the A-Prime template).

**Internal test seam** (recommended — avoids full NT8 account stub for these tests):
```csharp
// In CopyEngine.cs, add after CancelExistingPttStpDrag body:
internal void CancelExistingPttStpDragTestable(Account acc, Order fo)
    => CancelExistingPttStpDrag(acc, fo);
```
This follows the `MatchesLeaderNameTestable` / `OrderPassesBracketGateTestable` pattern already in the file.

### 7-Scan Checklist — T4

```
SCAN-01: grep -r "lock(" src/ --include="*.cs"
         Expected: 0 matches
         Rationale: CancelExistingPttStpDrag uses acc.Orders.ToList() snapshot (no lock).
                    acc.Cancel is NT8 API call, not a lock.

SCAN-02: grep -rn "async void " src/ --include="*.cs"
         Expected: 0 matches
         Rationale: No async code introduced.

SCAN-03: git diff HEAD src/PropTraderTools/CopyEngine.cs | grep "^+" | grep "return null;"
         Expected: 0 matches
         Rationale: CancelExistingPttStpDrag returns void. No return null added.
                    Pre-existing Order? return null at L2629 is not in T4 diff.

SCAN-04: dotnet build
         Expected: 0 errors, 0 warnings
         Rationale: CancelExistingPttStpDrag must compile. OrderState.Working, OrderState.Accepted,
                    Order.Name, Instrument.FullName are all valid NT8 properties.
                    acc.Cancel(new Order[] { o }) matches AddOnBase API signature.
                    CancelExistingPttStpDrag call site in SyncAtmFollowerBracket must compile.

SCAN-05: python scripts/complexity_audit.py
         Expected: SyncAtmFollowerBracket=6, CancelExistingPttStpDrag<=8 (expect 6 or 7).
                   SyncAtmFollowerTarget=8 (AT LIMIT -- verify no regression).
                   IsNoPriceChange=1, ExecutePhaseCStopReplacement=2.
                   OrderPassesBracketGate=2 (UNCHANGED), MatchesLeaderName=5 (UNCHANGED).
                   FindFollowerBracketOrder(list)=7 (UNCHANGED).

SCAN-06: dotnet test
         Expected: 0 Failed, 0 Errors
         Includes all 9 B137 tests (T_B137_01 through T_B137_09).
         T_B137_07 must PASS (Working PTT-STP-Drag cancelled).
         T_B137_08 must PASS (Accepted PTT-STP-Drag cancelled).
         No regressions in T_B137_01..T_B137_06, T_B137_09.

SCAN-07: dotnet csharpier check src/
         Expected: clean
```

---

## Final CYC State After All Tickets

| Method | Pre-B137 | After T1 | After T2 | After T3 | After T4 | Final | Limit |
|--------|----------|----------|----------|----------|----------|-------|-------|
| `SyncAtmFollowerTarget` | 8 | 7 | 8 | — | — | **8** | ≤8 ✅ |
| `SyncAtmFollowerBracket` | 4 | — | 5 | — | 6 | **6** | ≤8 ✅ |
| `OrderPassesBracketGate` | 2 | — | — | 2 | — | **2** | ≤8 ✅ |
| `MatchesLeaderName` | 5 | — | — | 5 | — | **5** | ≤8 ✅ |
| `FindFollowerBracketOrder` (list) | 7 | 7 | 7 | 7 | 7 | **7** | ≤8 ✅ |
| `IsNoPriceChange` (NEW) | — | — | 1 | — | — | **1** | ≤8 ✅ |
| `ExecutePhaseCStopReplacement` (NEW) | — | 2 | — | — | — | **2** | ≤8 ✅ |
| `CancelExistingPttStpDrag` (NEW) | — | — | — | — | 6-7 | **6-7** | ≤8 ✅ |

All final CYC values ≤ 8. No violations.

---

## JS Rules Reference (Per Ticket)

| Rule | All Tickets |
|------|-------------|
| JS-001 | No throw in hot paths. All new methods use try/catch with StatusUpdate?.Invoke (no rethrow) or are pure predicates (no throw path at all). |
| JS-002 | No return null. All new methods return bool or void. Pre-existing Order? return null at L2629 not modified. |
| JS-021 | No lock(). Static predicates have zero shared state. CancelExistingPttStpDrag uses acc.Orders.ToList() snapshot — established lock-free pattern (L2382). |
| JS-033 | No async void. All NT8 callbacks synchronous on NT8 background thread. |
| JS-036 | No heap allocation in hot path. IsNoPriceChange is stack-only. string.IsNullOrEmpty is BCL intrinsic, no allocation. |
| JS-066 | CYC ≤ 8 for all methods. Worst case: SyncAtmFollowerTarget = 8 (AT LIMIT). All new methods ≤ 8. |
| ASCII-only | All new identifiers, string literals, comments: "PTT-STP-Drag", "STP pre-cancel error", "CancelExistingPttStpDrag", "IsNoPriceChange", "ExecutePhaseCStopReplacement" — all ASCII. |
| DateTime.UtcNow | No time logic added in any ticket. |
| PTT- prefix | New order names: "PTT-STP-Drag" (T4), "PTT-TGT-Drag" (unchanged). All PTT-prefixed. |

---

## DW Items Closed by B137

| ID | Title | Closed by Ticket |
|----|-------|-----------------|
| DW-B147 | ARM event spurious cancel+resubmit (rawPrice==newPrice) | T2 (IsNoPriceChange guard in SyncAtmFollowerTarget) |
| DW-B149 | ChangeSubmitted race second TP3-HBC at same rawPrice | T2 (same IsNoPriceChange guard — same root cause class) |
| DW-B150 | OrderPassesBracketGate empty-string signalName → fo=NULL on stop drag | T3 (condition fix) |
| DW-B151 | SyncAtmFollowerBracket missing Block A-Prime pre-sweep | T4 (CancelExistingPttStpDrag extraction + call) |
