# B135 Implementation Tickets

**Epic**: B135 -- Two-Ticket: DW-B146 (second drag fo=null) + DW-B134-OCO (PTT drag orphan sweep)
**Status**: TICKETS_COMPLETE
**Plan**: `docs/brain/B135/02-architecture-plan.md` (REVIEW_PASS, Cycle 2)
**Author**: ptt-architect
**Total tests**: 12 new `[Fact]` (7 T1 + 5 T2) + 52 prior must remain green

**EXECUTION ORDER**: Tickets are SEQUENTIAL.
Ticket 1 MUST achieve BUILD_PASS + VERIFY_PASS before Ticket 2 begins.

---

## Ticket 1 -- DW-B146 (P1): MatchesLeaderName helper + FindFollowerBracketOrder second-drag fix

### 1.1 Overview

**DW ID Resolved**: DW-B146
**Root Cause**: `FindFollowerBracketOrder` list overload at L2551 contains the guard
`if (leaderName != null && order.Name != leaderName)`. On a second drag the original ATM
bracket (e.g., "Target3") has been Cancelled and replaced by "PTT-TGT-Drag". When
`leaderName="Target3"` and only "PTT-TGT-Drag" exists Working on the follower, this guard
filters it out, returning fo=null and silently skipping the sync.

**Fix**: Extract a new `MatchesLeaderName` static helper (CYC=5) that recognises exact ATM
name matches AND PTT-prefix replacements. Replace the L2551 inline guard with a call to this
helper. `FindFollowerBracketOrder` CYC stays = 8 (in-kind 1-for-1 guard replacement).

**Why helper extraction is mandatory**: `FindFollowerBracketOrder` is at CYC=8 AT LIMIT
(post-B134). Adding any branch inline would push to CYC=9, violating JS ceiling. Extraction
collapses the replacement branch into one call site, keeping the method at CYC=8.

**Spec requirement IDs**: DW-B146, B135-T1

### 1.2 File and Method Locations

| Item | Value |
|------|-------|
| File | `src/PropTraderTools/CopyEngine.cs` |
| NEW method | `MatchesLeaderName` -- insert after `SignalOrNameMatchesTestable` (L2577), before `FindFollowerBracketOrder` Account overload (L2579) |
| NEW test seam | `MatchesLeaderNameTestable` -- insert immediately after `MatchesLeaderName` definition |
| MODIFY method | `FindFollowerBracketOrder` list overload (L2540-2572) |
| MODIFY comment | CYC comment block at L2536-2539 |
| NO CHANGE | `FindFollowerBracketOrderTestable` list-injection overload (L2589-2594) -- inherits fix automatically |
| NO CHANGE | `SignalOrNameMatches` (L2511-2518) -- untouched |
| Test file (NEW) | `src/PropTraderTools/Tests/B135Tests.cs` |
| csproj | `src/PropTraderTools/PropTraderTools.csproj` |

### 1.3 Method Signatures

```csharp
// NEW -- insert after SignalOrNameMatchesTestable (L2577)
private static bool MatchesLeaderName(Order order, string? leaderName, bool isStop)

// NEW test seam -- insert immediately after MatchesLeaderName
internal static bool MatchesLeaderNameTestable(Order order, string? leaderName, bool isStop)

// EXISTING -- modify guard only, signature unchanged
private Order? FindFollowerBracketOrder(
    IEnumerable<Order> orders,
    string? fromEntrySignalName,
    bool isStop,
    string? leaderName = null)
```

### 1.4 Exact Code Changes

#### Change 1a: Update CYC comment block (L2536-2539)

**BEFORE (L2536-2539)**:
```csharp
        // CYC=8 (post-B134). AT LIMIT; PASS.
        // foreach(1) + SignalOrNameMatches guard(1) + leaderName exact guard(1) + state filter(3) + isStop(1) + type match(1) = 8.
        // DW-B143: Accepted added. DW-B144: Submitted added. DW-B145: leaderName exact guard added.
        // JS-021: no lock. JS-001: no throw. JS-002: Order? null contract unchanged.
```

**AFTER**:
```csharp
        // CYC=8 (post-B135). AT LIMIT; PASS.
        // foreach(1) + SignalOrNameMatches guard(1) + MatchesLeaderName guard(1) + state filter(3) + isStop(1) + type match(1) = 8.
        // DW-B143: Accepted added. DW-B144: Submitted added. DW-B145: leaderName exact guard. DW-B146: MatchesLeaderName helper (PTT-Drag fallback).
        // JS-021: no lock. JS-001: no throw. JS-002: Order? null contract unchanged.
```

#### Change 1b: Replace guard at L2551-2552

**BEFORE (L2551-2552)**:
```csharp
                if (leaderName != null && order.Name != leaderName) // (1) branch -- B134 DW-B145: require exact name when leaderName provided
                    continue;
```

**AFTER**:
```csharp
                if (!MatchesLeaderName(order, leaderName, isStop)) // (1) branch -- B135 DW-B146: extracted helper handles PTT-Drag fallback
                    continue;
```

#### Change 1c: Insert MatchesLeaderName + test seam after L2577

Insert the following block immediately after line 2577
(`=> SignalOrNameMatches(order, signalName, leaderName);`), before line 2579
(`internal Order? FindFollowerBracketOrderTestable(`):

```csharp

        // B135 DW-B146: PTT-prefix fallback -- after first drag, original ATM bracket is Cancelled;
        // replacement is "PTT-TGT-Drag" (target) or "PTT-STP-Drag" (stop).
        // FindFollowerBracketOrder must recognise these as the incumbent bracket on repeated drags.
        // CYC=5: base(1) + leaderName null(1) + name==(1) + !isStop&&TGT(1) + isStop&&STP(1) = 5.
        // JS-021: no lock (static, no shared state). JS-001: no throw. JS-002: returns bool.
        // ASCII-only. "PTT-TGT-Drag" and "PTT-STP-Drag" are ASCII.
        private static bool MatchesLeaderName(Order order, string? leaderName, bool isStop)
        {
            if (leaderName == null)                                           // (1) no constraint -- pass through
                return true;
            if (order.Name == leaderName)                                     // (2) exact ATM name match
                return true;
            if (!isStop && order.Name == "PTT-TGT-Drag")                     // (3) replacement target match
                return true;
            if (isStop && order.Name == "PTT-STP-Drag")                      // (4) replacement stop match
                return true;
            return false;
        }

        // B135 DW-B146: test seam -- delegates to MatchesLeaderName for xUnit test access.
        // InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
        internal static bool MatchesLeaderNameTestable(Order order, string? leaderName, bool isStop)
            => MatchesLeaderName(order, leaderName, isStop);
```

### 1.5 Complete Post-B135 State of FindFollowerBracketOrder List Overload

After applying changes 1a and 1b, the method at L2536-2572 reads:

```csharp
        // CYC=8 (post-B135). AT LIMIT; PASS.
        // foreach(1) + SignalOrNameMatches guard(1) + MatchesLeaderName guard(1) + state filter(3) + isStop(1) + type match(1) = 8.
        // DW-B143: Accepted added. DW-B144: Submitted added. DW-B145: leaderName exact guard. DW-B146: MatchesLeaderName helper (PTT-Drag fallback).
        // JS-021: no lock. JS-001: no throw. JS-002: Order? null contract unchanged.
        private Order? FindFollowerBracketOrder(
            IEnumerable<Order> orders,
            string? fromEntrySignalName,
            bool isStop,
            string? leaderName = null
        )
        {
            foreach (var order in orders) // (1) branch
            {
                if (!SignalOrNameMatches(order, fromEntrySignalName, leaderName)) // (1) branch
                    continue;
                if (!MatchesLeaderName(order, leaderName, isStop)) // (1) branch -- B135 DW-B146: extracted helper handles PTT-Drag fallback
                    continue;
                if (order.OrderState != OrderState.Working // (3) branches -- B134 DW-B144: Submitted added
                    && order.OrderState != OrderState.Accepted
                    && order.OrderState != OrderState.Submitted)
                    continue;
                if (isStop) // (1) branch
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

### 1.6 csproj Registration

In `src/PropTraderTools/PropTraderTools.csproj`, insert after line 162
(`<Compile Include="Tests\B134Tests.cs" />`):

```xml
    <Compile Include="Tests\B135Tests.cs" />
```

### 1.7 JS Rule Constraints

| Rule | Applies To | Constraint | Status |
|------|-----------|------------|--------|
| JS-021 (P0) | `MatchesLeaderName` | No `lock()` -- static pure predicate, no shared state | PASS |
| JS-021 (P0) | `FindFollowerBracketOrder` (modified) | No `lock()` -- no state mutation | PASS |
| JS-001 (P0) | `MatchesLeaderName` | No `throw` -- returns bool, all paths return value | PASS |
| JS-001 (P0) | `FindFollowerBracketOrder` (modified) | No `throw` -- guard replaced in-kind | PASS |
| JS-002 (P0) | `FindFollowerBracketOrder` (modified) | `return null` at L2571 preserved (nullable `Order?` contract unchanged) | PASS |
| JS-003 (P0) | N/A | No new discriminated unions | N/A |
| ASCII-only | "PTT-TGT-Drag", "PTT-STP-Drag" string literals | All ASCII, no Unicode | PASS |

### 1.8 NT8 API Constraints

No NT8 API calls in Ticket 1. `MatchesLeaderName` is a pure predicate operating on `Order.Name`
(a string property). `FindFollowerBracketOrder` is modified at the guard level only -- no new
NT8 API calls introduced.

NT8 fact confirmed (NT8_FULL_REFERENCE.md): `Order.Name` is a read-only string property
accessible from AddOnBase context. No threading constraints.

### 1.9 CYC Analysis

| Method | Pre-B135 CYC | Post-B135 CYC | Limit | Pass? | Notes |
|--------|-------------|---------------|-------|-------|-------|
| `FindFollowerBracketOrder` list overload | 8 | 8 | 8 | YES | Guard replaced 1-for-1; CYC unchanged |
| `MatchesLeaderName` (new) | -- | 5 | 8 | YES | base(1)+null(1)+exact(1)+!isStop&&TGT(1)+isStop&&STP(1)=5 |
| `SignalOrNameMatches` | 3 | 3 | 8 | YES | Unchanged |

**CYC-8 budget for FindFollowerBracketOrder post-B135**:
`foreach(1) + SignalOrNameMatches(1) + MatchesLeaderName(1) + state×3(3) + isStop(1) + type(1) = 8`

### 1.10 xUnit [Fact] Tests -- Ticket 1

**Test file**: `src/PropTraderTools/Tests/B135Tests.cs`
**Test class**: `B135Ticket1Tests` (nested in `B135Tests` or standalone)
**Framework**: xUnit only. No NUnit. No MSTest.
**Test access**: `MatchesLeaderNameTestable` (internal static seam); `FindFollowerBracketOrderTestable`
list-injection overload (L2589-2594, existing seam from B133).

| # | `[Fact]` Name | What It Asserts |
|---|---------------|-----------------|
| 1 | `T1_MatchesLeaderName_NullLeaderName_ReturnsTrue` | When `leaderName=null`: returns `true` regardless of `order.Name` or `isStop`. No constraint = pass through. |
| 2 | `T1_MatchesLeaderName_ExactName_ReturnsTrue` | When `order.Name=="Target3"` and `leaderName=="Target3"`: returns `true`. Exact ATM name match path. |
| 3 | `T1_MatchesLeaderName_WrongName_ReturnsFalse` | When `order.Name=="Target1"` and `leaderName=="Target3"`: returns `false`. Guard rejects non-matching, non-PTT order. |
| 4 | `T1_MatchesLeaderName_PttTgtDrag_Target_ReturnsTrue` | When `order.Name=="PTT-TGT-Drag"`, `leaderName="Target3"`, `isStop=false`: returns `true`. B135 fix path (target context). |
| 5 | `T1_MatchesLeaderName_PttStpDrag_Stop_ReturnsTrue` | When `order.Name=="PTT-STP-Drag"`, `leaderName="Stop1"`, `isStop=true`: returns `true`. B135 fix path (stop context). |
| 6 | `T1_MatchesLeaderName_PttTgtDrag_StopContext_ReturnsFalse` | When `order.Name=="PTT-TGT-Drag"`, `leaderName="Stop1"`, `isStop=true`: returns `false`. Wrong type guard: PTT-TGT-Drag rejected in stop context. |
| 7 | `T1_FindFollower_SecondDrag_ReturnsReplacementTarget` | Inject list with one Working Limit order named "PTT-TGT-Drag" (original "Target3" absent), call `FindFollowerBracketOrderTestable` with `leaderName="Target3"`, `isStop=false`. Assert result is the "PTT-TGT-Drag" order (not null). Integration test of B135 fix end-to-end. |

**Minimum**: 7 `[Fact]` -- all 7 required. ≥ 5 spec minimum satisfied.

### 1.11 Seven-Scan Checklist (Engineer Contract)

Run all scans after implementing Ticket 1 changes and BEFORE committing:

```
SCAN-01  lock() ban
         Command: grep -n "lock(" src/PropTraderTools/CopyEngine.cs
         Required: 0 matches
         Rationale: JS-021 P0. No new or existing lock() in modified file.

SCAN-02  throw new ban (in modified scope)
         Command: grep -n "throw new" src/PropTraderTools/CopyEngine.cs
         Required: 0 matches in MatchesLeaderName and FindFollowerBracketOrder
         Rationale: JS-001 P0. Hot-path methods must not throw.

SCAN-03  Non-ASCII bytes
         Command (PowerShell):
           [System.IO.File]::ReadAllBytes('src/PropTraderTools/CopyEngine.cs') |
             Where-Object { $_ -gt 127 } | Measure-Object
           [System.IO.File]::ReadAllBytes('src/PropTraderTools/Tests/B135Tests.cs') |
             Where-Object { $_ -gt 127 } | Measure-Object
         Required: Count = 0 for both files
         Rationale: ASCII-only mandate. "PTT-TGT-Drag" and "PTT-STP-Drag" are ASCII confirmed.

SCAN-04  CYC verification
         Method: MatchesLeaderName
           Expected CYC = 5
           Count: base(1) + null guard(1) + exact name(1) + !isStop&&TGT(1) + isStop&&STP(1) = 5
         Method: FindFollowerBracketOrder list overload
           Expected CYC = 8 (AT LIMIT; PASS)
           Count: foreach(1) + SignalOrNameMatches(1) + MatchesLeaderName(1) + state×3(3) + isStop(1) + type(1) = 8
         Required: MatchesLeaderName=5, FindFollowerBracketOrder=8

SCAN-05  return null documentation
         Existing: `return null;` at L2571 (FindFollowerBracketOrder list overload)
         Status: UNCHANGED. Order? nullable contract preserved.
         New code: MatchesLeaderName returns bool -- no null return.
         Required: No new return null; introduced by Ticket 1.

SCAN-06  Build
         Command: dotnet build src/PropTraderTools/PropTraderTools.csproj
         Required: 0 errors, 0 warnings introduced by Ticket 1 changes

SCAN-07  Prior test regression guard
         Command: dotnet test (run all test suites in the solution)
         Required pass counts (all prior suites must be green):
           B134Tests.cs  --  8 PASS, 0 FAIL
           B133Tests.cs  -- 10 PASS, 0 FAIL
           B132Tests.cs  --  6 PASS, 0 FAIL
           B131Tests.cs  --  7 PASS, 0 FAIL
           B130Tests.cs  --  8 PASS, 0 FAIL
           B129Tests.cs  -- 13 PASS, 0 FAIL
           B135 T1 new   --  7 PASS, 0 FAIL
         Total: 59 PASS (52 prior + 7 new T1)
```

### 1.12 Acceptance Criteria

Ticket 1 is BUILD_PASS + VERIFY_PASS when ALL of the following are true:

- [ ] `MatchesLeaderName` inserted after `SignalOrNameMatchesTestable` (L2577), before `FindFollowerBracketOrderTestable` Account overload (L2579)
- [ ] `MatchesLeaderNameTestable` internal seam inserted immediately after `MatchesLeaderName`
- [ ] CYC comment at L2536-2539 updated to reflect DW-B146 and `MatchesLeaderName` guard
- [ ] Guard at L2551-2552 replaced with `!MatchesLeaderName(order, leaderName, isStop)` call
- [ ] `MatchesLeaderName` CYC = 5 verified by manual count
- [ ] `FindFollowerBracketOrder` CYC = 8 verified by manual count (AT LIMIT; PASS)
- [ ] `<Compile Include="Tests\B135Tests.cs" />` added to csproj after L162
- [ ] All 7 T1 `[Fact]` tests pass
- [ ] All 52 prior tests pass (B134:8, B133:10, B132:6, B131:7, B130:8, B129:13)
- [ ] SCAN-01 through SCAN-07: all zero (CYC AT LIMIT counts as PASS)
- [ ] `dotnet build`: 0 errors

### 1.13 Out of Scope (DO NOT TOUCH)

The following are explicitly out of scope for Ticket 1:

- `SignalOrNameMatches` (L2511-2518) -- DO NOT modify
- `SyncAtmFollowerTarget` -- DO NOT touch
- `SyncAtmFollowerBracket` -- DO NOT touch
- `SyncFollowerBracket` -- DO NOT touch
- `OnOrderUpdate` -- DO NOT touch (T2 only)
- `TrySweptPttDragOrphans` -- DO NOT create (T2 only)
- `CancelPttDragOrphansForAccount` -- DO NOT create (T2 only)
- `_diagnosticMode` field (L412) -- DO NOT touch
- Any test file other than `B135Tests.cs`
- Any B129-B134 test file

---

## Ticket 2 -- DW-B134-OCO (P1): Orphaned PTT-Drag sweep on position flat

**PRECONDITION**: Ticket 1 must be BUILD_PASS + VERIFY_PASS before starting Ticket 2.

### 2.1 Overview

**DW ID Resolved**: DW-B134-OCO
**Root Cause**: `PTT-TGT-Drag` and `PTT-STP-Drag` orders are created in
`SyncAtmFollowerTarget` (L2362) and `SyncAtmFollowerBracket` (L2281) respectively, both with
`oco=""`. The empty OCO string means NT8 does not include them in the ATM bracket OCO cancel-all
group (confirmed NT8_ADDON_KNOWLEDGE.md). When the leader's ATM fills naturally or the stop
fires, NT8 propagates the close to follower green (OCO-linked) brackets but the blue PTT-drag
orders remain Working as orphans.

**Fix**: On every `Filled` order event in `OnOrderUpdate`, check if the follower account just
went flat. If so, cancel all Working `PTT-TGT-Drag` and `PTT-STP-Drag` orders on that account
for that instrument. Implemented as two new methods: `TrySweptPttDragOrphans` (gate, CYC=5)
and `CancelPttDragOrphansForAccount` (cancel worker, CYC=5). Call inserted in `OnOrderUpdate`
after `TryEvictFollowerBeSlot` at L1316, pre-Gate-1 (before `_isCopyEnabled` check at L1369).

**Spec requirement IDs**: DW-B134-OCO, B135-T2

### 2.2 File and Method Locations

| Item | Value |
|------|-------|
| File | `src/PropTraderTools/CopyEngine.cs` |
| MODIFY method | `OnOrderUpdate` (L1301) -- add one call after L1316 |
| NEW method | `TrySweptPttDragOrphans` -- add after `TryEvictFollowerBeSlot` region (~L1557) |
| NEW method | `CancelPttDragOrphansForAccount` -- add immediately after `TrySweptPttDragOrphans` |
| NEW test seam | `TrySweptPttDragOrphansTestable` -- add immediately after `TrySweptPttDragOrphans` |
| NEW test seam | `CancelPttDragOrphansForAccountTestable` -- add immediately after `CancelPttDragOrphansForAccount` |
| Test file | `src/PropTraderTools/Tests/B135Tests.cs` (same file as T1, add `B135Ticket2Tests` class) |

### 2.3 Method Signatures

```csharp
// MODIFY -- OnOrderUpdate signature unchanged; add one call statement only
private void OnOrderUpdate(object sender, OrderEventArgs e)

// NEW
private void TrySweptPttDragOrphans(OrderEventArgs e)

// NEW test seam
internal void TrySweptPttDragOrphansTestable(OrderEventArgs e)

// NEW
private void CancelPttDragOrphansForAccount(Account acc, Instrument instr)

// NEW test seam
internal void CancelPttDragOrphansForAccountTestable(Account acc, Instrument instr)
```

### 2.4 Exact Code Changes

#### Change 2a: Insert call in OnOrderUpdate after L1316

**Context (L1315-1318 current)**:
```csharp
            // DW-B79-06: evict stale BE retry slot when follower position closes via any path.
            TryEvictFollowerBeSlot(e);

            // DW-B79-08: PTT-BE bracket wipe recovery.
```

**AFTER change (L1315-1319)**:
```csharp
            // DW-B79-06: evict stale BE retry slot when follower position closes via any path.
            TryEvictFollowerBeSlot(e);

            // B135 DW-B134-OCO: sweep orphaned PTT-drag orders when follower position goes flat.
            TrySweptPttDragOrphans(e);

            // DW-B79-08: PTT-BE bracket wipe recovery.
```

**CYC impact on OnOrderUpdate**: `TrySweptPttDragOrphans(e)` is a call statement with no
boolean branches. McCabe branches added = 0. `OnOrderUpdate` CYC remains = 8. PASS.

#### Change 2b: Add TrySweptPttDragOrphans + seam (after TryEvictFollowerBeSlot region, ~L1557)

Insert the following block after the `TryEvictFollowerBeSlot` method definition:

```csharp
        // B135 DW-B134-OCO: sweep orphaned PTT-drag orders when follower position goes flat.
        // PTT-TGT-Drag and PTT-STP-Drag are standalone (oco="") -- not in any NT8 ATM OCO group.
        // When ATM fills naturally, NT8 only cancels OCO-linked (green) orders; PTT-drag orders survive.
        // Fire on Filled + follower + flat -- same pattern as TryEvictFollowerBeSlot (L1538).
        // CYC=5: base(1) + o null guard(1) + Filled guard(1) + follower guard(1) + flat guard(1) = 5.
        // JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only.
        private void TrySweptPttDragOrphans(OrderEventArgs e)
        {
            var o = e?.Order;
            if (o == null)                                                    // (1)
                return;
            if (o.OrderState != OrderState.Filled)                           // (2)
                return;
            if (!IsFollowerAccount(o.Account))                               // (3)
                return;
            if (!IsFlat(FindPosition(o.Account, o.Instrument)))              // (4)
                return;
            CancelPttDragOrphansForAccount(o.Account, o.Instrument);
        }

        // B135 DW-B134-OCO: test seam -- delegates to TrySweptPttDragOrphans for xUnit test access.
        internal void TrySweptPttDragOrphansTestable(OrderEventArgs e)
            => TrySweptPttDragOrphans(e);

        // B135 DW-B134-OCO: cancel all Working PTT-TGT-Drag and PTT-STP-Drag orders for this account+instrument.
        // Called ONLY when position is confirmed flat (TrySweptPttDragOrphans gate).
        // acc.Orders.ToList() is safe in OnOrderUpdate callback thread (existing pattern: L2322).
        // try/catch: absorbs ErrorCode.UnableToCancelOrder (existing pattern: SyncAtmFollowerBracket L2259-2266).
        // CYC=5: base(1) + foreach(1) + state guard(1) + instr guard(1) + name guard(1) = 5.
        // JS-021: no lock. JS-001: try/catch -- no throw in hot path. JS-002: void. ASCII-only.
        // NT8-014: "PTT-TGT-Drag" confirmed L2362, "PTT-STP-Drag" confirmed L2281. acc.Cancel confirmed AddOnBase.
        private void CancelPttDragOrphansForAccount(Account acc, Instrument instr)
        {
            foreach (var o in acc.Orders.ToList())                           // (1)
            {
                if (o.OrderState != OrderState.Working)                      // (2)
                    continue;
                if (o.Instrument?.FullName != instr?.FullName)               // (3)
                    continue;
                if (o.Name != "PTT-TGT-Drag" && o.Name != "PTT-STP-Drag")  // (4)
                    continue;
                try
                {
                    acc.Cancel(new Order[] { o });
                    StatusUpdate?.Invoke(acc.Name + ": PTT drag sweep: cancelled " + o.Name);
                }
                catch (Exception ex)
                {
                    StatusUpdate?.Invoke(acc.Name + ": PTT drag sweep cancel error: " + ex.Message);
                }
            }
        }

        // B135 DW-B134-OCO: test seam -- delegates to CancelPttDragOrphansForAccount for xUnit test access.
        internal void CancelPttDragOrphansForAccountTestable(Account acc, Instrument instr)
            => CancelPttDragOrphansForAccount(acc, instr);
```

### 2.5 JS Rule Constraints

| Rule | Applies To | Constraint | Status |
|------|-----------|------------|--------|
| JS-021 (P0) | `TrySweptPttDragOrphans` | No `lock()` -- guard returns void with early exits | PASS |
| JS-021 (P0) | `CancelPttDragOrphansForAccount` | No `lock()` -- `acc.Orders.ToList()` is NT8-thread-safe established pattern (L2322) | PASS |
| JS-021 (P0) | `OnOrderUpdate` (modified) | No `lock()` added -- call statement only | PASS |
| JS-001 (P0) | `TrySweptPttDragOrphans` | No `throw` -- void with guard returns | PASS |
| JS-001 (P0) | `CancelPttDragOrphansForAccount` | `try/catch` absorbs `UnableToCancelOrder`; no rethrow | PASS |
| JS-002 (P0) | `TrySweptPttDragOrphans` | `void` return -- no null return | PASS |
| JS-002 (P0) | `CancelPttDragOrphansForAccount` | `void` return -- no null return | PASS |
| ASCII-only | "PTT-TGT-Drag", "PTT-STP-Drag", "PTT drag sweep" | All ASCII, no Unicode literals | PASS |
| JS-033 (P0) | Both new methods | No `async void` -- both are synchronous void | PASS |

### 2.6 NT8 API Constraints

| API | Availability | Source | Notes |
|-----|-------------|--------|-------|
| `acc.Cancel(Order[])` | AddOnBase: YES | NT8_FULL_REFERENCE.md L2408-2452; NT8_ADDON_KNOWLEDGE.md L222 | Wrapped in try/catch to absorb `UnableToCancelOrder`. Existing pattern: SyncAtmFollowerBracket Block A (L2259-2266). |
| `acc.Orders.ToList()` | AddOnBase: YES | NT8_ADDON_KNOWLEDGE.md L219 | Safe in `OnOrderUpdate` callback thread. Existing pattern: SyncAtmFollowerTarget Block A-Prime (L2322). |
| `IsFollowerAccount(acc)` | CopyEngine internal: YES | L1536 existing usage | Checks account against `_rules[i].FollowerAccounts`. |
| `IsFlat(FindPosition(...))` | CopyEngine private: YES | L4002-4070 | `pos==null \|\| pos.Quantity==0`. Established flat-guard pattern: TryEvictFollowerBeSlot (L1538). |
| `o.Instrument?.FullName` | AddOnBase: YES | NT8_FULL_REFERENCE.md | Null-safe property access; `?.` pattern used consistently in CopyEngine. |

**Why NOT PositionUpdate**: `PositionUpdate` event (NT8_FULL_REFERENCE.md L388, L1993-1999) would
require new subscriptions in `Subscribe()`/`Unsubscribe()` (L1288-1298) -- excess scope.
The `OnOrderUpdate` (Filled + flat) pattern is established in `TryEvictFollowerBeSlot` and
requires zero new subscriptions.

### 2.7 CYC Analysis

| Method | CYC | Limit | Pass? | Branch Count |
|--------|-----|-------|-------|-------------|
| `TrySweptPttDragOrphans` (new) | 5 | 8 | YES | base(1)+null(1)+Filled(1)+follower(1)+flat(1)=5 |
| `CancelPttDragOrphansForAccount` (new) | 5 | 8 | YES | base(1)+foreach(1)+state(1)+instr(1)+name(1)=5 |
| `OnOrderUpdate` (modified) | 8 | 8 | YES | Call adds 0 McCabe branches; CYC unchanged |

**Note on `CancelPttDragOrphansForAccount` catch block**: The `catch (Exception ex)` handler
adds 0 McCabe branches per standard McCabe counting (exception handlers are not conditional
branches in the normal flow). Total CYC=5 as stated.

### 2.8 xUnit [Fact] Tests -- Ticket 2

**Test file**: `src/PropTraderTools/Tests/B135Tests.cs` (same file as T1)
**Test class**: `B135Ticket2Tests`
**Framework**: xUnit only. No NUnit. No MSTest.
**Test access**: `CancelPttDragOrphansForAccountTestable` (internal seam); `TrySweptPttDragOrphansTestable` (internal seam).

| # | `[Fact]` Name | What It Asserts |
|---|---------------|-----------------|
| 1 | `T2_CancelPttDragOrphans_CancelsWorkingTgtDrag` | When `acc` has a Working `PTT-TGT-Drag` order for `instr`, `CancelPttDragOrphansForAccountTestable` calls `acc.Cancel` on it (verify via test double/spy pattern or confirm order reaches Cancelled state). |
| 2 | `T2_CancelPttDragOrphans_CancelsWorkingStpDrag` | When `acc` has a Working `PTT-STP-Drag` order for `instr`, `CancelPttDragOrphansForAccountTestable` calls `acc.Cancel` on it. |
| 3 | `T2_CancelPttDragOrphans_IgnoresNonPttOrders` | When `acc` has a Working native ATM order named "Target3" (non-PTT name), `CancelPttDragOrphansForAccountTestable` does NOT call `acc.Cancel` on it. Non-PTT Working orders survive the sweep. |
| 4 | `T2_TrySwept_PartialFill_NotFlat_DoesNotSweep` | When order is `Filled` but `FindPosition` returns a non-flat position (qty > 0), `TrySweptPttDragOrphansTestable` returns without calling `CancelPttDragOrphansForAccount`. Validates the flat guard (branch 4). |
| 5 | `T2_CancelPttDragOrphans_ExceptionAbsorbed_NoRethrow` | When `acc.Cancel` throws (e.g., mock throws `InvalidOperationException`), `CancelPttDragOrphansForAccountTestable` absorbs the exception via try/catch and does not propagate it. Verifies `UnableToCancelOrder` absorption path -- no exception reaches the caller. |

**Minimum**: 5 `[Fact]` -- all 5 required. All 5 DW-B134-OCO spec scenarios (a-e) covered.

### 2.9 Seven-Scan Checklist (Engineer Contract)

Run all scans after implementing Ticket 2 changes and BEFORE committing:

```
SCAN-01  lock() ban
         Command: grep -n "lock(" src/PropTraderTools/CopyEngine.cs
         Required: 0 matches
         Rationale: JS-021 P0. No lock() in TrySweptPttDragOrphans or CancelPttDragOrphansForAccount.

SCAN-02  throw new ban (in modified scope)
         Command: grep -n "throw new" src/PropTraderTools/CopyEngine.cs
         Required: 0 matches in TrySweptPttDragOrphans, CancelPttDragOrphansForAccount, OnOrderUpdate (modified region)
         Rationale: JS-001 P0. try/catch absorbs; no rethrow.

SCAN-03  Non-ASCII bytes
         Command (PowerShell):
           [System.IO.File]::ReadAllBytes('src/PropTraderTools/CopyEngine.cs') |
             Where-Object { $_ -gt 127 } | Measure-Object
           [System.IO.File]::ReadAllBytes('src/PropTraderTools/Tests/B135Tests.cs') |
             Where-Object { $_ -gt 127 } | Measure-Object
         Required: Count = 0 for both files
         Rationale: ASCII-only mandate. All string literals confirmed ASCII.

SCAN-04  CYC verification
         Method: TrySweptPttDragOrphans
           Expected CYC = 5
           Count: base(1) + o null(1) + Filled(1) + follower(1) + flat(1) = 5
         Method: CancelPttDragOrphansForAccount
           Expected CYC = 5
           Count: base(1) + foreach(1) + state(1) + instr(1) + name(1) = 5
         Method: OnOrderUpdate (verify unchanged)
           Expected CYC = 8 (call adds 0 McCabe)
         Required: TrySweptPttDragOrphans=5, CancelPttDragOrphansForAccount=5, OnOrderUpdate=8

SCAN-05  return null documentation
         TrySweptPttDragOrphans: void -- no return null
         CancelPttDragOrphansForAccount: void -- no return null
         OnOrderUpdate (modified): existing return null paths unchanged
         Required: No new return null; introduced by Ticket 2.

SCAN-06  Build
         Command: dotnet build src/PropTraderTools/PropTraderTools.csproj
         Required: 0 errors, 0 warnings introduced by Ticket 2 changes

SCAN-07  Prior test regression guard
         Command: dotnet test (run all test suites in the solution)
         Required pass counts:
           B134Tests.cs  --  8 PASS, 0 FAIL
           B133Tests.cs  -- 10 PASS, 0 FAIL
           B132Tests.cs  --  6 PASS, 0 FAIL
           B131Tests.cs  --  7 PASS, 0 FAIL
           B130Tests.cs  --  8 PASS, 0 FAIL
           B129Tests.cs  -- 13 PASS, 0 FAIL
           B135 T1       --  7 PASS, 0 FAIL
           B135 T2 new   --  5 PASS, 0 FAIL
         Total: 64 PASS (52 prior + 7 T1 + 5 T2)
```

### 2.10 Acceptance Criteria

Ticket 2 is BUILD_PASS + VERIFY_PASS when ALL of the following are true:

- [ ] `TrySweptPttDragOrphans(e)` call inserted in `OnOrderUpdate` after `TryEvictFollowerBeSlot(e)` at L1316, with `// B135 DW-B134-OCO` comment
- [ ] `TrySweptPttDragOrphans` private method added (CYC=5 verified)
- [ ] `TrySweptPttDragOrphansTestable` internal seam added immediately after
- [ ] `CancelPttDragOrphansForAccount` private method added (CYC=5 verified)
- [ ] `CancelPttDragOrphansForAccountTestable` internal seam added immediately after
- [ ] `OnOrderUpdate` CYC = 8 (delta = 0; call adds 0 McCabe branches) verified
- [ ] All 5 T2 `[Fact]` tests pass
- [ ] All 7 T1 tests still pass
- [ ] All 52 prior tests pass (B134:8, B133:10, B132:6, B131:7, B130:8, B129:13)
- [ ] SCAN-01 through SCAN-07: all zero
- [ ] `dotnet build`: 0 errors

### 2.11 Out of Scope (DO NOT TOUCH)

The following are explicitly out of scope for Ticket 2:

- `FindFollowerBracketOrder` -- DO NOT touch (T1 only)
- `MatchesLeaderName` -- DO NOT touch (T1 only)
- `SyncAtmFollowerTarget` -- DO NOT touch
- `SyncAtmFollowerBracket` -- DO NOT touch
- `SyncFollowerBracket` -- DO NOT touch
- `_diagnosticMode` field (L412) -- DO NOT touch
- DW-B134-OCO OBS-A, OBS-B, OBS-C, OBS-D -- all OPEN deferred items, NOT addressed by T2
- DW-B147 rawPrice==newPrice guard -- DEFERRED, not in scope
- Any B129-B134 test file
- `Subscribe()`/`Unsubscribe()` (L1288-1298) -- DO NOT add PositionUpdate subscriptions

---

## Appendix A: Prior Test Suite Counts (Regression Baseline)

| Test File | Expected Pass | Expected Fail |
|-----------|--------------|---------------|
| `B129Tests.cs` | 13 | 0 |
| `B130Tests.cs` | 8 | 0 |
| `B131Tests.cs` | 7 | 0 |
| `B132Tests.cs` | 6 | 0 |
| `B133Tests.cs` | 10 | 0 |
| `B134Tests.cs` | 8 | 0 |
| **Total prior** | **52** | **0** |

---

## Appendix B: Deferred Items NOT Addressed by B135

The following DW items are OPEN and carry forward to future blocks. ptt-engineer MUST NOT
attempt to fix these in B135:

| ID | Title | Target |
|----|-------|--------|
| B135-DEFER-01 | Gap B runtime -- two simultaneous entries race | B136+ |
| B135-DEFER-02 | Stale orders multi-session -- FindFollowerBracketOrder matches prior-session orders | future |
| DW-B134-OCO OBS-A | Cancel race on partial fill (UnableToCancelOrder window) | future |
| DW-B134-OCO OBS-B | Replacement order duplicates partially-filled quantity | future |
| DW-B134-OCO OBS-C | Stop side not cancelled before target replacement | future |
| DW-B134-OCO OBS-D | Net position drift on two-leg partial fill | future |
| DW-B147 | SyncAtmFollowerBracket/Target rawPrice==newPrice early-return guard | B136+ |

---

*Tickets produced by ptt-architect, B135 Phase 3. Plan: 02-architecture-plan.md REVIEW_PASS (Cycle 2).*
