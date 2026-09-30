# B131 LaneA — Ticket Generation
## DW-B138: ATM Bracket Drag Not Reaching SyncFollowerBracket for Stop1/T1/T2

**Status**: TICKETS_COMPLETE
**Author**: ptt-architect
**Epic**: B131 LaneA
**Plan**: docs/brain/B131/LaneA-02-architecture-plan.md (REVIEW_PASS)
**Plan Review**: docs/brain/B131/LaneA-02-plan-review.md (REVIEW_PASS)
**Date**: 2026-08-31

---

## TICKET 1 — DW-B138: Extend FindFollowerBracketOrder to match ATM brackets by Name

**Spec Req IDs**: DW-B138
**Architecture Plan Sections**: B, C, D, E
**File to Modify**: `src/PropTraderTools/CopyEngine.cs`
**New File to Create**: `src/PropTraderTools/Tests/B131Tests.cs`

---

### A. Problem Statement

When a trader drags an ATM bracket order (Stop1, Target1, Target2) on the leader chart,
`OnOrderUpdate` fires with `OrderState.Working`. The call chain is:
`TryHandleBracketDrag` → `HandleBracketChange` → `SyncFollowerBracket` →
`FindFollowerBracketOrder`. Inside `FindFollowerBracketOrder` (L2345–L2365), every follower
order is skipped because of the guard at L2347:

```csharp
if (order.FromEntrySignal != fromEntrySignalName) // L2347
    continue;
```

ATM leader brackets carry a non-null `FromEntrySignal` (e.g., `"AtmEntrySignal"`). However,
PTT-placed follower brackets were created via `acc.CreateOrder` with `oco = ""` and therefore
have `FromEntrySignal = null`. The string comparison `null != "AtmEntrySignal"` is `true` on
every iteration, so `FindFollowerBracketOrder` returns `null`. `SyncFollowerBracket` hits
`if (fo == null) return;` at L2140 and exits silently — no cancel+resubmit is ever placed.

The T3 asymmetry (Target3 worked in the test session while Stop1/T1/T2 did not) is explained
by T3's follower bracket having a matching non-null `FromEntrySignal` in that specific session —
either from an earlier code path that captured the signal at placement time, or from a prior
successful sync that preserved the signal on the order record. Stop1, Target1, and Target2 follower
brackets consistently had `FromEntrySignal = null`, causing consistent failure. The fix adds a
Name-based fallback inside `FindFollowerBracketOrder` so that when `FromEntrySignal` matching
fails, the lookup retries by `Order.Name` (e.g., `"Stop1"` == `"Stop1"`). The existing
`FromEntrySignal` path has priority and is not altered; the Name fallback fires only when the
signal comparison would otherwise return `null`.

---

### B. Exact Code Context

#### B1. FindFollowerBracketOrder — full body (L2336–L2366 of CopyEngine.cs)

```
2336 |         // CYC=4. Returns first matching working bracket order for the follower.
2337 |         // V03: return type is Order? (nullable) -- null contract explicit (JS-002 compliant).
2338 |         // V01: matching by FromEntrySignal name -- not leg-type scan.
2339 |         private Order? FindFollowerBracketOrder(
2340 |             Account follower,
2341 |             string fromEntrySignalName,
2342 |             bool isStop
2343 |         )
2344 |         {
2345 |             foreach (var order in follower.Orders.ToList()) // (1) branch
2346 |             {
2347 |                 if (order.FromEntrySignal != fromEntrySignalName) // (1) branch
2348 |                     continue;
2349 |                 if (order.OrderState != OrderState.Working) // (1) branch
2350 |                     continue;
2351 |                 if (isStop)
2352 |                 {
2353 |                     if (
2354 |                         order.OrderType == OrderType.StopMarket
2355 |                         || order.OrderType == OrderType.StopLimit
2356 |                     ) // (1) branch
2357 |                         return order;
2358 |                 }
2359 |                 else
2360 |                 {
2361 |                     if (order.OrderType == OrderType.Limit && !IsStopLeg(order))
2362 |                         return order;
2363 |                 }
2364 |             }
2365 |             return null;
2366 |         }
```

#### B2. SyncFollowerBracket — call site to FindFollowerBracketOrder (L2131–L2141)

```
2131 |         private void SyncFollowerBracket(
2132 |             Account acc,
2133 |             Order leaderOrder,
2134 |             bool isStop,
2135 |             double newPrice,
2136 |             double tickSize
2137 |         )
2138 |         {
2139 |             var fo = FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop);
2140 |             if (fo == null) // (1)
2141 |                 return;
```

**L2139 is the single call site** (confirmed by plan reviewer grep: exactly 1 call + 1 definition).

#### B3. IsAtmSTPOrder — full body (L2107–L2113, reference predicate, NOT changed)

```
2107 |         internal static bool IsAtmSTPOrder(Order order) =>
2108 |             order.Name != null
2109 |             && (
2110 |                 order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase)
2111 |                 || order.Name.StartsWith("Stop", StringComparison.OrdinalIgnoreCase)
2112 |                 || order.Name.StartsWith("Target", StringComparison.OrdinalIgnoreCase)
2113 |             );
```

---

### C. Required Changes (exact before/after)

#### Change 1 — Add new private static helper `SignalOrNameMatches`

**Location**: Insert immediately before `FindFollowerBracketOrder` (before L2336 in CopyEngine.cs).

**BEFORE**: Method does not exist.

**AFTER** (new method to add):

```csharp
// B131 DW-B138: predicate encapsulating signal-first / name-fallback match logic.
// CYC=3: (1) signal equality check, (2) leaderName null guard, (3) name equality check.
// JS-021: no lock (static, no shared state). JS-001: no throw. JS-002: returns bool (no null).
// ASCII-only. DateTime.UtcNow not used (no time logic).
private static bool SignalOrNameMatches(Order order, string? signalName, string? leaderName)
{
    if (order.FromEntrySignal == signalName) // (1) primary: signal equality (covers null==null)
        return true;
    if (leaderName == null) // (2) no fallback available
        return false;
    return order.Name == leaderName; // (3) ATM Name-based fallback
}
```

**CYC**: 3 (branches 1, 2, 3). Well within JS budget of <= 8.

**Placement note**: Place this method immediately before `FindFollowerBracketOrder` so the reader
can see the predicate and its consumer together. One blank line between them.

---

#### Change 2 — Modify `FindFollowerBracketOrder` signature and body

**Location**: L2339–L2366 of `src/PropTraderTools/CopyEngine.cs`

**BEFORE**:

```csharp
// CYC=4. Returns first matching working bracket order for the follower.
// V03: return type is Order? (nullable) -- null contract explicit (JS-002 compliant).
// V01: matching by FromEntrySignal name -- not leg-type scan.
private Order? FindFollowerBracketOrder(
    Account follower,
    string fromEntrySignalName,
    bool isStop
)
{
    foreach (var order in follower.Orders.ToList()) // (1) branch
    {
        if (order.FromEntrySignal != fromEntrySignalName) // (1) branch
            continue;
        if (order.OrderState != OrderState.Working) // (1) branch
            continue;
        if (isStop)
        {
            if (
                order.OrderType == OrderType.StopMarket
                || order.OrderType == OrderType.StopLimit
            ) // (1) branch
                return order;
        }
        else
        {
            if (order.OrderType == OrderType.Limit && !IsStopLeg(order))
                return order;
        }
    }
    return null;
}
```

**AFTER**:

```csharp
// CYC=4. Returns first matching working bracket order for the follower.
// V04 B131 DW-B138: leaderName param added -- ATM Name-based fallback when FromEntrySignal null/empty.
// V03: return type is Order? (nullable) -- null contract explicit (JS-002 compliant).
// V01: matching by FromEntrySignal name -- not leg-type scan.
// JS-021: no lock. JS-001: no throw. JS-002: Order? makes null contract explicit.
private Order? FindFollowerBracketOrder(
    Account follower,
    string? fromEntrySignalName,
    bool isStop,
    string? leaderName = null
)
{
    foreach (var order in follower.Orders.ToList()) // (1) branch
    {
        if (!SignalOrNameMatches(order, fromEntrySignalName, leaderName)) // (1) branch
            continue;
        if (order.OrderState != OrderState.Working) // (1) branch
            continue;
        if (isStop)
        {
            if (
                order.OrderType == OrderType.StopMarket
                || order.OrderType == OrderType.StopLimit
            ) // (1) branch
                return order;
        }
        else
        {
            if (order.OrderType == OrderType.Limit && !IsStopLeg(order))
                return order;
        }
    }
    return null;
}
```

**Key diffs**:
1. Parameter `string fromEntrySignalName` → `string? fromEntrySignalName` (nullable annotation —
   matches the real-world case where `leaderOrder.FromEntrySignal` may be null).
2. New optional parameter `string? leaderName = null` appended (C# default parameter — all
   existing callers that pass 3 args continue to compile with zero changes to their source).
3. Loop guard at the signal-check line replaced:
   - BEFORE: `if (order.FromEntrySignal != fromEntrySignalName)`
   - AFTER:  `if (!SignalOrNameMatches(order, fromEntrySignalName, leaderName))`
4. The instrument filter (`follower.Instrument == leaderOrder.Instrument`) is NOT present in this
   method's current body — it is applied upstream in `HandleBracketChange` which only ever calls
   `SyncFollowerBracket` with `leaderOrder.Instrument`-matched accounts. No instrument filter is
   needed here; this is consistent with the BEFORE code.
5. CYC remains 4 (the `SignalOrNameMatches` call substitutes for the previous signal comparison
   branch; no net new McCabe points in `FindFollowerBracketOrder` itself).

---

#### Change 3 — Update call site in `SyncFollowerBracket` (L2139)

**BEFORE** (L2139):

```csharp
var fo = FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop);
```

**AFTER** (L2139):

```csharp
var fo = FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop, leaderOrder.Name);
```

**Scope**: One line only. No other changes to `SyncFollowerBracket`.
**CYC**: Unchanged (existing comment at L2127 states CYC=7; no new branches added).

---

### D. Method Signatures (all methods touched)

| Method | File | Lines (BEFORE) | Before Signature | After Signature | CYC Before | CYC After |
|--------|------|----------------|-----------------|----------------|------------|-----------|
| `SignalOrNameMatches` | CopyEngine.cs | — (new) | Does not exist | `private static bool SignalOrNameMatches(Order order, string? signalName, string? leaderName)` | — | 3 |
| `FindFollowerBracketOrder` | CopyEngine.cs | L2339–L2366 | `private Order? FindFollowerBracketOrder(Account follower, string fromEntrySignalName, bool isStop)` | `private Order? FindFollowerBracketOrder(Account follower, string? fromEntrySignalName, bool isStop, string? leaderName = null)` | 4 | 4 |
| `SyncFollowerBracket` | CopyEngine.cs | L2131–L2183 | `private void SyncFollowerBracket(Account acc, Order leaderOrder, bool isStop, double newPrice, double tickSize)` | Signature UNCHANGED | 7 | 7 |

**All CYC values <= 8. All JS-021/JS-001/JS-002 constraints satisfied.**

---

### E. xUnit Test Specifications

**File**: `src/PropTraderTools/Tests/B131Tests.cs` (NEW FILE — must be created by engineer)
**Framework**: xUnit only. No NUnit. No MSTest. (JS mandatory — testing-strategies.md)
**Testability path**: `IsAtmSTPOrder` is already `internal static` (L2107). The engineer must
make `FindFollowerBracketOrder` and `SignalOrNameMatches` accessible to the test project by
either:
- (a) Marking them `internal` and adding `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]`
  to `CopyEngine.cs`, OR
- (b) Exposing a thin `internal static` test accessor `FindFollowerBracketOrderTestable` that
  delegates to the private method.

**Preferred option: (c) Test `SignalOrNameMatches` directly** — it is the new helper carrying
the entire new logic. The 4 `[Fact]` tests below call `SignalOrNameMatches` and the method must
be `internal static` (or `public static`) for the test project to invoke it. The engineer adds
`internal` visibility to `SignalOrNameMatches` and one `[assembly: InternalsVisibleTo]` attribute.

The integration-style tests for `Stop1` / `Target1` (Tests 1 and 2) also verify the full
`FindFollowerBracketOrder` path via the `FindFollowerBracketOrderTestable` accessor so that the
end-to-end lookup is confirmed, not just the predicate.

---

#### Test 1: `B131_DW138_Stop1DragReachesHandleBracketChange`

**Purpose**: Verify that a follower Stop1 order with `FromEntrySignal = null` is matched when
`leaderName = "Stop1"` is supplied (the Name-fallback path).

```csharp
[Fact]
public void B131_DW138_Stop1DragReachesHandleBracketChange()
{
    // Arrange
    // Follower order: PTT-placed Stop1 with null FromEntrySignal (the failing case before fix)
    var followerStop1 = MockOrder(
        name: "Stop1",
        orderType: OrderType.StopMarket,
        orderState: OrderState.Working,
        fromEntrySignal: null,
        stopPrice: 4498.75
    );
    var followerAccount = MockAccount(new[] { followerStop1 });

    // Act
    // Direct predicate test -- confirms the new Name-fallback branch fires
    bool matched = CopyEngine.SignalOrNameMatchesTestable(
        followerStop1,
        signalName: "AtmEntrySignal",   // leader signal (non-null, no follower match)
        leaderName: "Stop1"             // fallback
    );

    // Integration test -- confirms full FindFollowerBracketOrder path
    var found = CopyEngine.FindFollowerBracketOrderTestable(
        followerAccount,
        fromEntrySignalName: "AtmEntrySignal",
        isStop: true,
        leaderName: "Stop1"
    );

    // Assert
    Assert.True(matched);              // SignalOrNameMatches returns true via Name fallback
    Assert.NotNull(found);             // FindFollowerBracketOrder does NOT return null
    Assert.Equal("Stop1", found!.Name);
}
```

---

#### Test 2: `B131_DW138_Target1DragReachesHandleBracketChange`

**Purpose**: Same scenario for Target1 (Limit order, isStop=false).

```csharp
[Fact]
public void B131_DW138_Target1DragReachesHandleBracketChange()
{
    // Arrange
    var followerTarget1 = MockOrder(
        name: "Target1",
        orderType: OrderType.Limit,
        orderState: OrderState.Working,
        fromEntrySignal: null,
        limitPrice: 4510.00
    );
    var followerAccount = MockAccount(new[] { followerTarget1 });

    // Act
    bool matched = CopyEngine.SignalOrNameMatchesTestable(
        followerTarget1,
        signalName: "AtmEntrySignal",
        leaderName: "Target1"
    );
    var found = CopyEngine.FindFollowerBracketOrderTestable(
        followerAccount,
        fromEntrySignalName: "AtmEntrySignal",
        isStop: false,
        leaderName: "Target1"
    );

    // Assert
    Assert.True(matched);
    Assert.NotNull(found);
    Assert.Equal("Target1", found!.Name);
}
```

---

#### Test 3: `B131_DW138_Target3DragStillReachesHandleBracketChange` (Regression)

**Purpose**: Verify T3 follower with matching `FromEntrySignal` still works via the
PRIMARY signal-match path (no regression from adding the fallback).

```csharp
[Fact]
public void B131_DW138_Target3DragStillReachesHandleBracketChange()
{
    // Arrange
    // Follower Target3 has matching FromEntrySignal -- the ORIGINAL working case
    var followerTarget3 = MockOrder(
        name: "Target3",
        orderType: OrderType.Limit,
        orderState: OrderState.Working,
        fromEntrySignal: "AtmEntrySignal",   // non-null, matches leader
        limitPrice: 4520.00
    );
    var followerAccount = MockAccount(new[] { followerTarget3 });

    // Act
    bool matched = CopyEngine.SignalOrNameMatchesTestable(
        followerTarget3,
        signalName: "AtmEntrySignal",
        leaderName: "Target3"
    );
    var found = CopyEngine.FindFollowerBracketOrderTestable(
        followerAccount,
        fromEntrySignalName: "AtmEntrySignal",
        isStop: false,
        leaderName: "Target3"
    );

    // Assert
    Assert.True(matched);              // returns true on branch (1) -- signal equality
    Assert.NotNull(found);
    Assert.Equal("Target3", found!.Name);
}
```

---

#### Test 4: `B131_DW138_BuySTPDragStillRoutesCorrectly` (Regression)

**Purpose**: Verify that a `"Buy STP"` follower order (non-ATM name, has matching
`FromEntrySignal`) is found via SIGNAL match. The Name fallback MUST NOT produce a false
positive match when leader name is `"Stop1"` and follower name is `"Buy STP"`.

```csharp
[Fact]
public void B131_DW138_BuySTPDragStillRoutesCorrectly()
{
    // Arrange
    // Follower: "Buy STP" order WITH matching FromEntrySignal
    var followerBuySTP = MockOrder(
        name: "Buy STP",
        orderType: OrderType.StopMarket,
        orderState: OrderState.Working,
        fromEntrySignal: "AtmEntrySignal",   // matches leader signal
        stopPrice: 4498.75
    );
    var followerAccount = MockAccount(new[] { followerBuySTP });

    // Act
    bool matched = CopyEngine.SignalOrNameMatchesTestable(
        followerBuySTP,
        signalName: "AtmEntrySignal",
        leaderName: "Stop1"             // leader is Stop1, follower is Buy STP -- names differ
    );
    var found = CopyEngine.FindFollowerBracketOrderTestable(
        followerAccount,
        fromEntrySignalName: "AtmEntrySignal",
        isStop: true,
        leaderName: "Stop1"
    );

    // Assert
    Assert.True(matched);              // true via branch (1) signal equality -- NOT branch (3)
    Assert.NotNull(found);
    Assert.Equal("Buy STP", found!.Name); // "Buy STP" returned, not null, not "Stop1"
}
```

---

#### Test scaffold helpers required in B131Tests.cs

The engineer must add the following private helpers in `B131Tests.cs` to construct mock NT8
objects (or adapt existing helpers from B129Tests.cs / B130Tests.cs if already present):

```csharp
// Mock Order factory -- fills only the properties used by FindFollowerBracketOrder
private static Order MockOrder(
    string name,
    OrderType orderType,
    OrderState orderState,
    string? fromEntrySignal,
    double stopPrice = 0.0,
    double limitPrice = 0.0
) { /* ... implement using NinjaTrader mock infrastructure or test doubles ... */ }

// Mock Account factory -- returns an Account whose .Orders list contains the given orders
private static Account MockAccount(IEnumerable<Order> orders)
{ /* ... */ }
```

The engineer must also add the internal test accessor methods to `CopyEngine.cs` (or a
`CopyEngineTestAccessors.cs` partial if preferred):

```csharp
// Test accessor for SignalOrNameMatches (internal visibility for InternalsVisibleTo)
internal static bool SignalOrNameMatchesTestable(Order order, string? signalName, string? leaderName)
    => SignalOrNameMatches(order, signalName, leaderName);

// Test accessor for FindFollowerBracketOrder (internal visibility for InternalsVisibleTo)
internal Order? FindFollowerBracketOrderTestable(
    Account follower,
    string? fromEntrySignalName,
    bool isStop,
    string? leaderName = null
) => FindFollowerBracketOrder(follower, fromEntrySignalName, isStop, leaderName);
```

Add to `CopyEngine.cs` assembly attribute (top of file or in a separate attributes file):

```csharp
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]
```

---

### F. 7-Scan Checklist (MANDATORY — engineer contract)

The engineer MUST run ALL 7 scans to zero results (or expected baseline) before reporting
`BUILD_PASS`. Each scan command and its expected output are the binding contract.

---

#### SCAN-01 — lock() ban (JS-021 P0 CRITICAL)

```
Command : grep -rn "lock(" src/PropTraderTools/CopyEngine.cs
Expected: 0 matches in new or changed code (SignalOrNameMatches, FindFollowerBracketOrder,
          SyncFollowerBracket call site).
          Any pre-existing lock() occurrences in unrelated methods are a separate debt item
          and do NOT block this ticket. New code added by this ticket must be 0.
```

---

#### SCAN-02 — async void ban (JS-033 P0 CRITICAL)

```
Command : grep -rn "async void " src/PropTraderTools/CopyEngine.cs
Expected: 0 matches in new or changed code.
          (No async methods are added by this ticket.)
```

---

#### SCAN-03 — return null audit (JS-002 P0 CRITICAL)

```
Command : grep -n "return null" src/PropTraderTools/CopyEngine.cs
Expected: Exactly 1 pre-existing match (L2365: return null in FindFollowerBracketOrder,
          pre-existing and JS-002 compliant because return type is Order?).
          NO new "return null" lines added by this ticket.
          Verify: the existing L2365 return null remains -- it is the correct contract terminus.
```

---

#### SCAN-04 — throw in hot path ban (JS-001 P0 CRITICAL)

```
Command : grep -n "throw new" src/PropTraderTools/CopyEngine.cs
Expected: 0 matches in new or changed methods (SignalOrNameMatches, FindFollowerBracketOrder
          body changes, SyncFollowerBracket L2139 call site).
          Pre-existing throw statements in unrelated methods are not in scope.
```

---

#### SCAN-05 — CYC compliance (Jane Street <= 8 mandate)

```
Command : python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs
Expected:
  SignalOrNameMatches       CYC = 3  (<= 8 PASS)
  FindFollowerBracketOrder  CYC = 4  (<= 8 PASS, reviewer-confirmed actual value)
  SyncFollowerBracket       CYC = 7  (<= 8 PASS, unchanged)
  No method added or changed by this ticket may exceed CYC = 8.
```

---

#### SCAN-06 — ASCII-only compliance (V12 DNA mandate)

```
Command : grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
Expected: 0 matches in new or changed code (all identifiers, strings, and comments
          added by this ticket must use ASCII-only characters).
Also run: grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Tests/B131Tests.cs
Expected: 0 matches.
```

---

#### SCAN-07 — Build + xUnit tests pass

```
Command : dotnet build src/PropTraderTools/
Expected: 0 errors, 0 warnings related to new code.
          (Existing unrelated warnings are pre-existing baseline and do not block.)

Command : dotnet test src/PropTraderTools/Tests/
Expected: All 4 B131_DW138_* tests GREEN.
          All B129_DW134_* and B130_DW137_* regression tests GREEN (no regressions).
          Test run summary: N passed, 0 failed, 0 skipped.
```

---

### G. Acceptance Criteria

The ticket is DONE when ALL of the following are true:

- [ ] Stop1 drag on leader chart → `[DW-B138]` log line appears in NT8 output → follower
  Stop1 bracket updated (PTT-STP-Drag order placed on follower account).
- [ ] Target1 drag → follower Target1 bracket updated (PTT-TGT-Drag order placed).
- [ ] Target2 drag → follower Target2 bracket updated.
- [ ] Target3 drag still works — no regression (signal-match path intact).
- [ ] "Buy STP" drag still works — no regression (signal match fires, Name fallback not needed).
- [ ] All 7 scans zero (SCAN-01 through SCAN-06) or at confirmed pre-existing baseline (SCAN-03).
- [ ] `dotnet build src/PropTraderTools/` exits 0.
- [ ] All 4 `B131_DW138_*` xUnit tests GREEN.
- [ ] All `B129_DW134_*` and `B130_DW137_*` regression tests GREEN.
- [ ] `powershell -File scripts\ptt-sync-and-verify.ps1` exits with 0 MISMATCH lines.
- [ ] F5 compile in NinjaTrader 8 passes (green compilation bar, no NT8 errors).

---

### H. DW Items

**None.**

All NT8 API facts used in this ticket are confirmed from authoritative docs (no unknowns):

| Fact | Source |
|------|--------|
| `order.FromEntrySignal` non-null on ATM leader brackets | NT8_ADDON_KNOWLEDGE.md L228 |
| `Order.Name` = ATM template slot name (e.g., `"Stop1"`, `"Target3"`) | NT8_FULL_REFERENCE.md + B129/B130 confirmed in SIM |
| `acc.Cancel + acc.CreateOrder + acc.Submit` is correct AddOnBase cancel+resubmit pattern | NT8_ADDON_KNOWLEDGE.md (pre-confirmed B129 SIM gate) |
| `Account.Change()` is silent no-op on ATM-owned brackets | NT8_ADDON_KNOWLEDGE.md (B129 SIM gate empirically confirmed) |
| `OrderState.ChangeSubmitted` falls through `IsWorkingBracket` (noise, not blocker) | NT8_FULL_REFERENCE.md L3367 |

No genuine NT8 API unknowns remain for this ticket.

---

*End of Ticket 1 — B131 LaneA DW-B138*
