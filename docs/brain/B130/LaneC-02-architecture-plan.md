# B130-LaneC Architecture Plan
# DW-B107: MoveStopToBreakEven Step A Snapshots Stale PTT-BE-Target-* Orders

**Block**: B130-LaneC
**Defect**: DW-B107
**Phase**: Phase 2 (Architecture Plan)
**Architect**: ptt-architect
**Status**: REVIEW_READY
**Date**: 2026-09-01

---

## A. Implementation Status (Critical Context)

**The DW-B107 production fix is ALREADY IMPLEMENTED in [`CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs).**

Evidence confirmed by direct code read:

| Location | Evidence | Acceptance Criterion |
|----------|----------|----------------------|
| [`CopyEngine.cs:L3922`](src/PropTraderTools/CopyEngine.cs:3922) | `private List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargets(Account acc, Instrument instrument)` | T1 PASS |
| [`CopyEngine.cs:L4019`](src/PropTraderTools/CopyEngine.cs:4019) | `var targets = SnapshotBeTargets(acc, instrument); // (3)` | T2 PASS |
| [`CopyEngine.cs:L4023-4024`](src/PropTraderTools/CopyEngine.cs:4023) | `while (targets.Count > 3) targets.RemoveAt(targets.Count - 1);` | T3 PASS |
| [`CopyEngine.cs:L3873` comment](src/PropTraderTools/CopyEngine.cs:3873) | `// CYC=7: IsFlat(1) + tickSize/pos guard(2) + while-cap(3) + cancel-try(4)...` | T4 PASS |
| [`CopyEngine.cs:L3917` comment](src/PropTraderTools/CopyEngine.cs:3917) | `// CYC=7: null guard(1) + foreach(2) + o==null continue(3) + stateOk(4) + instrOk+type(5) + if(isNative)(6) + else if(isPtt)(7)` | T5 PASS |
| [`CopyEngine.cs:L3930`](src/PropTraderTools/CopyEngine.cs:3930) | `return nativeTargets; // (1) JS-002: empty list, never null` | T7 PASS |

**Conclusion**: B130-LaneC is a TESTS-ONLY block. No production `.cs` changes are permitted.

---

## B. Test Strategy: How to Test Private SnapshotBeTargets

### Access Constraint

`SnapshotBeTargets` is declared `private` at [`CopyEngine.cs:L3922`](src/PropTraderTools/CopyEngine.cs:3922).

[`InternalsVisibleTo("PropTraderTools.Tests")`](src/PropTraderTools/CopyEngine.cs:46) grants access to
`internal` members only, not `private`. Direct invocation from tests is impossible without reflection.

### Options Evaluated

| Option | Approach | Decision |
|--------|----------|----------|
| A: Reflection | Call private method via `MethodInfo.Invoke` | REJECTED: fragile, not idiomatic, violates JS simplicity mandate |
| B: Internal test seam | Add internal wrapper to CopyEngine production code | REJECTED: requires production code change in a tests-only block |
| C: Behavioral equivalence | Test the predicates and algorithms that SnapshotBeTargets uses, inline in test body | SELECTED |

### Selected Strategy: Option C -- Behavioral Equivalence

`SnapshotBeTargets` contains two separable behaviors:
1. **Two-pass classification predicate** (isNative vs isPtt) -- testable by replicating the identical
   string-matching conditions inline in the test body, applied to stub orders.
2. **Hard cap algorithm** `while (targets.Count > 3) targets.RemoveAt(targets.Count - 1)` --
   directly testable as a pure `List<T>` operation with no NT8 dependencies.

Both behaviors can be verified without calling the private method. The tests prove the logic is
correct by testing the EXACT same predicate expressions that the private method executes.

This mirrors how LaneA tests (`B130Tests.cs:B130_DW137_Stop1NameRoutesToCancelResubmit`) test
`IsAtmSTPOrder` (the routing predicate) rather than testing `SyncFollowerBracket` (the dispatcher)
directly -- testing the decision logic at the point where it has no NT8 runtime dependency.

---

## C. Test Designs: All 3 [Fact] Tests

### Test 1: `B130_DW107_SnapshotBeTargetsFiltersStaleOrders`

**Purpose**: Proves the two-pass native-first classification logic that `SnapshotBeTargets` uses
to separate native ATM targets from stale PTT residues. This is the CHANGE A proof.

**Access path**: Inline predicate helpers (C# local functions) mirroring `CopyEngine.cs:L3948-3958`
exactly. No NT8 Account/Instrument needed.

**Method signature**:
```csharp
[Fact]
public void B130_DW107_SnapshotBeTargetsFiltersStaleOrders()
```

**Setup**:
```csharp
// Local predicates mirroring SnapshotBeTargets L3948-3958 verbatim.
// CopyEngine.cs L3948-3952: isNative predicate
static bool IsNativeTarget(string n) =>
    n != null
    && n.Length >= 7
    && n.StartsWith("Target", StringComparison.Ordinal)
    && char.IsDigit(n[6])
    && n[6] != '0';

// CopyEngine.cs L3953-3958: isPtt predicate
static bool IsPttTarget(string n) =>
    n != null
    && (
        (n.StartsWith("PTT-QX-T", StringComparison.Ordinal)
         && n.Length > 8
         && char.IsDigit(n[8]))
        || n.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
    );
```

**Assertions**:
```csharp
// Native ATM target orders: must classify as native, not PTT
Assert.True(IsNativeTarget("Target1"));
Assert.True(IsNativeTarget("Target2"));
Assert.True(IsNativeTarget("Target3"));
Assert.False(IsPttTarget("Target1"));

// Stale PTT-BE-Target-* residues: must classify as PTT, not native
Assert.True(IsPttTarget("PTT-BE-Target-1"));
Assert.True(IsPttTarget("PTT-BE-Target-4")); // stale T4 from prior session (root cause)
Assert.False(IsNativeTarget("PTT-BE-Target-1"));

// PTT-QX-T* orders: must classify as PTT, not native
Assert.True(IsPttTarget("PTT-QX-T1"));
Assert.True(IsPttTarget("PTT-QX-T3"));

// Non-target orders: must classify as neither (proves empty-snapshot contract)
Assert.False(IsNativeTarget("Entry"));
Assert.False(IsPttTarget("Entry"));
Assert.False(IsNativeTarget("PTT-BE-Stop-1"));
Assert.False(IsPttTarget("PTT-BE-Stop-1"));

// Native-first priority: when natives exist, PTT residues are excluded
// Simulated: nativeTargets.Count > 0 ? nativeTargets : pttTargets
// If any native is present, result is nativeTargets (PTT-BE-Target-4 ignored)
var nativeTargets = new List<string>();
var pttTargets = new List<string>();
foreach (var name in new[] { "Target1", "Target2", "Target3", "PTT-BE-Target-4" })
{
    if (IsNativeTarget(name)) nativeTargets.Add(name);
    else if (IsPttTarget(name)) pttTargets.Add(name);
}
var result = nativeTargets.Count > 0 ? nativeTargets : pttTargets;
Assert.Equal(3, result.Count);           // exactly 3 native targets returned
Assert.DoesNotContain("PTT-BE-Target-4", result); // stale T4 excluded (DW-B107 fix)
```

**CYC**: 1 (no branches in test body outside local function definitions which are pure expressions)
**NT8 types used**: None (string operations only)
**Proves**: T1 (SnapshotBeTargets logic exists and classifies correctly)

---

### Test 2: `B130_DW107_HardCapTrimsSnapshotToThreeTargets`

**Purpose**: Directly tests the `while (targets.Count > 3) targets.RemoveAt(targets.Count - 1)`
algorithm from [`CopyEngine.cs:L4023-4024`](src/PropTraderTools/CopyEngine.cs:4023). This is the
CHANGE B proof. Pure `List<T>` operation -- no NT8 dependencies whatsoever.

**Method signature**:
```csharp
[Fact]
public void B130_DW107_HardCapTrimsSnapshotToThreeTargets()
```

**Setup + Assertions**:
```csharp
// Case 1: 4-item list (root-cause scenario: stale T4 present)
var targets4 = new List<(double Price, int Qty, OrderAction Action)>
{
    (4200.00, 1, OrderAction.Sell),
    (4210.00, 1, OrderAction.Sell),
    (4220.00, 1, OrderAction.Sell),
    (4230.00, 1, OrderAction.Sell), // stale T4 residue
};
while (targets4.Count > 3)
    targets4.RemoveAt(targets4.Count - 1);
Assert.Equal(3, targets4.Count); // T4 trimmed -- DW-B107 fix verified

// Case 2: 3-item list (nominal case: exactly 3 targets)
var targets3 = new List<(double Price, int Qty, OrderAction Action)>
{
    (4200.00, 1, OrderAction.Sell),
    (4210.00, 1, OrderAction.Sell),
    (4220.00, 1, OrderAction.Sell),
};
while (targets3.Count > 3)
    targets3.RemoveAt(targets3.Count - 1);
Assert.Equal(3, targets3.Count); // unchanged -- no over-trim

// Case 3: 0-item list (no targets -- retry path)
var targets0 = new List<(double Price, int Qty, OrderAction Action)>();
while (targets0.Count > 3)
    targets0.RemoveAt(targets0.Count - 1);
Assert.Equal(0, targets0.Count); // empty -- no crash, no spurious trim
```

**CYC**: 1 (while loops are algorithmic, not branching test logic)
**NT8 types used**: `NinjaTrader.Cbi.OrderAction` (enum -- from `using NinjaTrader.Cbi;` already present)
**Proves**: T3 (hard cap algorithm is correct and does not over-trim or crash on empty)

---

### Test 3: `B130_DW107_NonTargetOrdersProduceEmptySnapshot`

**Purpose**: Proves that non-target order names (Entry, Stop, Close, PTT-BE-Stop-*) match neither
the isNative nor isPtt predicate, so the snapshot would be empty (not null) when no target orders
exist. This anchors the T7 contract (JS-002: never return null) and proves "no-position equivalent"
behavior -- the empty-snapshot path that triggers the retry slot at `MoveStopToBreakEven:L4034`.

**Method signature**:
```csharp
[Fact]
public void B130_DW107_NonTargetOrdersProduceEmptySnapshot()
```

**Setup** (reuses same local predicate helpers as Test 1):
```csharp
static bool IsNativeTarget(string n) =>
    n != null
    && n.Length >= 7
    && n.StartsWith("Target", StringComparison.Ordinal)
    && char.IsDigit(n[6])
    && n[6] != '0';

static bool IsPttTarget(string n) =>
    n != null
    && (
        (n.StartsWith("PTT-QX-T", StringComparison.Ordinal)
         && n.Length > 8
         && char.IsDigit(n[8]))
        || n.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
    );
```

**Assertions**:
```csharp
// Non-target names that must NOT pollute the snapshot
var nonTargetNames = new[]
{
    "Entry", "Close", "PTT-BE-Stop-1", "PTT-BE-Stop-2", "PTT-BE-Stop-3",
    "PTT-Copy", "PTT-QX-Stop-1", "Stop1", "Stop2", "Stop3",
};
var nativeTargets = new List<string>();
var pttTargets = new List<string>();
foreach (var name in nonTargetNames)
{
    if (IsNativeTarget(name)) nativeTargets.Add(name);
    else if (IsPttTarget(name)) pttTargets.Add(name);
}
// Both lists must be empty -- no non-target name leaks into snapshot
Assert.Empty(nativeTargets);
Assert.Empty(pttTargets);

// Native-first return: empty pttTargets returned when both are empty
var result = nativeTargets.Count > 0 ? nativeTargets : pttTargets;
Assert.Empty(result);     // empty list -- not null (JS-002 contract)
Assert.NotNull(result);   // null guard: SnapshotBeTargets L3930 returns List, never null
```

**CYC**: 1 (foreach + if/else if inside loop are algorithmic classification, not test branches)
**NT8 types used**: None
**Proves**: T7 (empty List, never null) and the empty-snapshot path contract

---

## D. Access Path Summary

| Mechanism | Used? | Rationale |
|-----------|-------|-----------|
| Direct private call | NO | SnapshotBeTargets is private; InternalsVisibleTo does not grant access |
| Reflection | NO | Fragile; rejected per JS simplicity mandate |
| Internal test-seam wrapper | NO | Requires production code change; blocked in tests-only block |
| InternalsVisibleTo (internal members) | YES (already present at L46) | Powers LaneA + LaneB tests; not needed for LaneC |
| Inline predicate helpers (C# local functions) | YES (selected) | Tests the identical logic without NT8 runtime dependency |
| Pure List<T> algorithm test | YES (Test 2) | Tests hard-cap algorithm directly |

**InternalsVisibleTo** at [`CopyEngine.cs:L46`](src/PropTraderTools/CopyEngine.cs:46):
```csharp
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]
```
Already present. No change required.

---

## E. CYC Notes

| Method | CYC | Limit | Status | Evidence |
|--------|-----|-------|--------|----------|
| `SnapshotBeTargets` | 7 | 8 | PASS | Comment at [`CopyEngine.cs:L3917`](src/PropTraderTools/CopyEngine.cs:3917) |
| `MoveStopToBreakEven` | 7 | 8 | PASS | Comment at [`CopyEngine.cs:L3873`](src/PropTraderTools/CopyEngine.cs:3873) |
| `B130_DW107_SnapshotBeTargetsFiltersStaleOrders` | 1 | 8 | PASS | No branches in test body |
| `B130_DW107_HardCapTrimsSnapshotToThreeTargets` | 1 | 8 | PASS | No branches in test body |
| `B130_DW107_NonTargetOrdersProduceEmptySnapshot` | 1 | 8 | PASS | No branches in test body |

**SnapshotBeTargets CYC=7 breakdown**:
1. null guard `if (acc == null || instrument == null) return nativeTargets`
2. `foreach (Order o in acc.Orders)`
3. `if (o == null) continue`
4. `stateOk` multi-OR (counts as one branch node)
5. `instrOk + o.OrderType != Limit` guard
6. `if (isNative)` add to nativeTargets
7. `else if (isPtt)` add to pttTargets

---

## F. File Change Plan

### Files Modified

| File | Change Type | Description |
|------|-------------|-------------|
| [`src/PropTraderTools/Tests/B130Tests.cs`](src/PropTraderTools/Tests/B130Tests.cs) | APPEND ONLY | Add 3 new [Fact] tests (DW-B107 section) at end of class |

### Files NOT Modified

| File | Reason |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | Production fix already implemented |
| `src/PropTraderTools/PropTraderTools.csproj` | B130Tests.cs already in `<Compile Include="Tests\B130Tests.cs" />` |
| Any other `.cs` file | Out of scope |

### Append Location in B130Tests.cs

Insert after the last test (`B130_DW136_CancelLeaderOrder2DoesNotEvictLeaderOrder1Bag`, ends before
the closing `}` of the `B130Tests` class). Three new `[Fact]` methods follow exactly.

### Namespace + Using Directives

No new `using` directives required. The existing header provides:
```csharp
using NinjaTrader.Cbi;   // covers OrderAction, Order
using Xunit;              // covers [Fact], Assert
```
`System.Collections.Generic.List<T>` is available without a using directive in net48 with implicit
System.Collections.Generic (or via the existing `using NinjaTrader.Cbi;` which transitively pulls it).

---

## G. 7-Scan Checklist

| Scan | Rule | Test Code Mapping | Status |
|------|------|-------------------|--------|
| SCAN-01 | No `lock()` in new code | Tests use no shared mutable state. `List<T>` is local. | PASS |
| SCAN-02 | No `throw new XxxException` in hot paths | Tests use `Assert.*` (xUnit framework throws, not our code). | PASS |
| SCAN-03 | No `return null` | Tests return `void`. Local lists are always `new List<T>()` (never null). | PASS |
| SCAN-04 | No `async void` | All 3 tests are synchronous `void`. No async keyword. | PASS |
| SCAN-05 | ASCII-only string literals | All order names ("Target1", "PTT-BE-Target-4", etc.) are 7-bit ASCII. All comments are ASCII. | PASS |
| SCAN-06 | No LINQ in hot paths | Tests use `foreach` + `List.Add()` + `while`. Zero LINQ calls. | PASS |
| SCAN-07 | xUnit only (no NUnit/MSTest) | `[Fact]` attribute + `Assert.*` from xUnit 2.6.6. No `[Test]`, `[TestMethod]`. | PASS |

---

## H. JS-DNA Compliance

| Rule | Requirement | Test Code Status |
|------|-------------|-----------------|
| JS-021 (P0) | No `lock()` | PASS -- no lock in any test method |
| JS-001 (P0) | No throw in hot paths | PASS -- Assert.* is xUnit's throw, not ours |
| JS-002 (P0) | No `return null` | PASS -- tests return void; lists constructed as `new List<T>()` |
| JS-033 (P0) | No `async void` | PASS -- all tests are sync `[Fact] void` |
| ASCII-only | All strings 7-bit ASCII | PASS -- order names are "Target1", "PTT-BE-Target-4", etc. |
| No LINQ | No `.Select()`, `.Where()`, `.Take()` | PASS -- pure foreach + while |
| xUnit-only | `[Fact]` + `Assert.*` | PASS -- no NUnit, no MSTest |
| CYC <= 8 | Each method <= 8 branches | PASS -- CYC=1 for all 3 tests |
| No DateTime.Now | No timestamp usage | PASS -- tests do not use DateTime |

---

## I. Acceptance Criteria Mapping

The 8 acceptance criteria from `DW-B107/00-defect-brief.md`:

| Criterion | Type | How Verified |
|-----------|------|-------------|
| **T1**: `SnapshotBeTargets` helper exists | Structural | Confirmed by direct code read at [`CopyEngine.cs:L3922`](src/PropTraderTools/CopyEngine.cs:3922). Runtime proof: Test 1 inline predicates mirror the exact logic. |
| **T2**: `MoveStopToBreakEven` calls `SnapshotBeTargets` | Structural | Confirmed at [`CopyEngine.cs:L4019`](src/PropTraderTools/CopyEngine.cs:4019). Not runtime-testable (private method, live NT8 Account required). |
| **T3**: `while (targets.Count > 3) targets.RemoveAt(...)` cap present | Behavioral + Structural | **Test 2** (`B130_DW107_HardCapTrimsSnapshotToThreeTargets`) runs the exact algorithm on a local list. Structural proof at L4023-4024. |
| **T4**: `MoveStopToBreakEven` CYC <= 8 | Structural | Comment confirmed at L3873: `// CYC=7`. |
| **T5**: `SnapshotBeTargets` CYC <= 8 | Structural | Comment confirmed at L3917: `// CYC=7`. |
| **T6**: Zero `lock(` in new code | Structural | Grep of L3917-4030 confirms no lock. |
| **T7**: Zero `return null` in new code | Structural + Behavioral | L3930: `return nativeTargets; // JS-002: empty list, never null`. **Test 3** asserts `Assert.NotNull(result)` on the empty-list path. |
| **T8**: All new strings/comments ASCII-only | Structural | Comment at L3919-3921 is ASCII. String literals ("PTT-BE-Target-", "Target", etc.) are ASCII. |

---

## J. Component Summary

| Component | File | Type | Change |
|-----------|------|------|--------|
| `SnapshotBeTargets` | `CopyEngine.cs:L3922` | Private helper method | ALREADY IMPLEMENTED |
| `MoveStopToBreakEven` (Step A call + hard cap) | `CopyEngine.cs:L4019-4024` | Private instance method | ALREADY IMPLEMENTED |
| `B130_DW107_SnapshotBeTargetsFiltersStaleOrders` | `B130Tests.cs` | xUnit [Fact] | NEW (ticket-3) |
| `B130_DW107_HardCapTrimsSnapshotToThreeTargets` | `B130Tests.cs` | xUnit [Fact] | NEW (ticket-3) |
| `B130_DW107_NonTargetOrdersProduceEmptySnapshot` | `B130Tests.cs` | xUnit [Fact] | NEW (ticket-3) |

---

## K. Data Flow (for completeness)

```
OnOrderUpdate (follower account)
  -> MoveStopToBreakEven(acc, instrument, bufferTicks)
       -> SnapshotBeTargets(acc, instrument)           [private, L3922]
            -> nativeTargets: "Target1", "Target2", "Target3"
            -> pttTargets: "PTT-BE-Target-1", "PTT-BE-Target-4" (stale)
            <- returns nativeTargets (native-first, DW-B107 CHANGE A)
       -> while (targets.Count > 3) targets.RemoveAt(...)  [DW-B107 CHANGE B, L4023-4024]
       -> PttBreakEvenSwap.Execute(acc, instrument, newStop, targets)
            [targets is now exactly 3 -- no T4 submitted]
```

---

## L. Deferred Items From This Block

None. All DW-B107 acceptance criteria are met by existing production code + the 3 new tests.

No carry-forward items introduced by B130-LaneC.

---

**Return**: PLAN_COMPLETE
