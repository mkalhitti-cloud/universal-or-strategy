# B134 Tickets

**Epic**: B134 -- Two-Ticket: DW-B144 (Submitted-state gap) + DW-B145 (wrong bracket index)
**Plan Source**: docs/brain/B134/02-architecture-plan.md (REVIEW_PASS)
**Phase**: 3 (Ticket Generation)
**Author**: ptt-architect

---

## Ticket Overview

| Ticket | DW ID | Change | CYC delta | File |
|--------|-------|--------|-----------|------|
| T1 | DW-B144 | Add `OrderState.Submitted` to state filter in `FindFollowerBracketOrder` | 6 -> 7 | CopyEngine.cs |
| T2 | DW-B145 | Insert `leaderName` exact-match guard in `FindFollowerBracketOrder` | 7 -> 8 | CopyEngine.cs |

Both tickets touch **the same method** (`FindFollowerBracketOrder` list overload, L2538-2566).
**T1 must be applied before T2** because T2 depends on Submitted-state orders being reachable.

---

## TICKET 1 -- DW-B144: Submitted-state gap

### Spec Requirements

- **DW-B144**: `FindFollowerBracketOrder` rejects `OrderState.Submitted` orders; all follower brackets are
  `Submitted` at drag time during TP4 testing. Fix: extend state-acceptance predicate to include `Submitted`.

### Root Cause

[`FindFollowerBracketOrder`](src/PropTraderTools/CopyEngine.cs:2538) (list overload, L2538-2566)
contains the following state filter at L2549:

```csharp
if (order.OrderState != OrderState.Working && order.OrderState != OrderState.Accepted) // (2) branches
    continue;
```

All follower bracket orders are in `OrderState.Submitted` at the moment `SyncFollowerBracket` fires
during a TP4 drag event. The filter rejects every order, leaving `fo = null` at L2189. The
`if (fo == null) return;` guard at L2189 silently drops the sync. No crash, no log -- just no sync.

**NT8 Cancel-on-Submitted safety (verified in plan §B.5)**:
- `Account.Cancel()` has no documented state restriction (NT8_FULL_REFERENCE.md L2408-2452).
- `OrderState.Submitted` is a non-terminal (live) state (L3357-3374).
- `ErrorCode.UnableToCancelOrder` is returned on failure; it is NOT an exception.
- All `acc.Cancel()` calls in `SyncAtmFollowerTarget` (L2340-2347) and `SyncAtmFollowerBracket` are
  wrapped in `try/catch`. A failed cancel on Submitted is absorbed; Block B (CreateOrder + Submit)
  runs regardless.

### File

**`src/PropTraderTools/CopyEngine.cs`**

### Exact Edit

**Location**: L2549 -- the state-filter `if` inside `FindFollowerBracketOrder` list overload.

**Also update the block comment immediately above the method signature** (currently at L2535, which
will shift by line count after the B144 code is inserted). The comment documents the CYC count for the
method; it must reflect the post-B134 state after BOTH tickets are applied. Write it as the final
post-both-tickets comment to avoid a second comment-update in Ticket 2.

---

**BEFORE (L2538-2566 -- current source)**:

```csharp
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
                if (order.OrderState != OrderState.Working && order.OrderState != OrderState.Accepted) // (2) branches
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

**AFTER (T1 + T2 combined -- write this final form in one pass)**:

> The engineer MUST apply both T1 and T2 edits together (same method body). The "AFTER" block below
> is the complete final state of `FindFollowerBracketOrder` after both tickets.
> See Ticket 2 for the T2-specific guard annotation. For clarity, the BEFORE/AFTER in Ticket 2
> shows only the T2 delta against the T1-applied intermediate.

```csharp
        // CYC=8 (post-B134). AT LIMIT; PASS.
        // foreach(1) + SignalOrNameMatches guard(1) + leaderName exact guard(1) + state filter(3) + isStop(1) + type match(1) = 8.
        // DW-B143: Accepted added. DW-B144: Submitted added. DW-B145: leaderName exact guard added.
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
                if (leaderName != null && order.Name != leaderName) // (1) branch -- B134 DW-B145: require exact name when leaderName provided
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

> NOTE: The `leaderName` exact-name guard (line `if (leaderName != null && order.Name != leaderName)`)
> is introduced by Ticket 2. It is shown here in the combined AFTER block so the engineer sees the
> complete final state and can apply both changes in one edit. The T1-only intermediate (without the
> leaderName guard) is never committed; apply both guards together.

### CYC Analysis (Ticket 1)

| Stage | CYC | Formula |
|-------|-----|---------|
| Pre-B134 (current source) | 6 | foreach(1) + SignalOrNameMatches(1) + state-filter(2) + isStop(1) + type-match(1) = 6 |
| Post-T1 only (intermediate, never committed) | 7 | adds 1 branch for Submitted condition |
| Post-T1+T2 (final committed state) | 8 | adds 1 more branch for leaderName guard |

**Limit**: CYC <= 8 (Jane Street strict). Post-T1+T2 = 8. AT LIMIT; PASS.

### New Tests -- Ticket 1

**File**: `src/PropTraderTools/Tests/B134Tests.cs` (NEW FILE)
**Namespace**: `PropTraderTools`
**Test class**: `B134Ticket1Tests` (inside `B134FindFollowerBracketOrderTests` outer class)
**Access**: `FindFollowerBracketOrder` list-injection overload via `InternalsVisibleTo("PropTraderTools.Tests")`
**Framework**: xUnit only (NEVER NUnit, NEVER MSTest)

> **DO NOT MODIFY any existing test file (B129Tests.cs, B130Tests.cs, B131Tests.cs, B132Tests.cs, B133Tests.cs). Only create new file Tests/B134Tests.cs.**

```
[Fact] T1_SubmittedState_StopOrder_Found_And_Returned
  Arrange: single Stop order (OrderType.StopMarket, OrderState.Submitted, FromEntrySignal="ATM1")
           leaderName=null (signal-only path)
  Act:     call FindFollowerBracketOrder(orders, "ATM1", isStop:true, leaderName:null)
  Assert:  result != null
           result.OrderType == OrderType.StopMarket

[Fact] T1_SubmittedState_TargetOrder_Found_And_Returned
  Arrange: single Target order (OrderType.Limit, OrderState.Submitted, FromEntrySignal="ATM1")
           leaderName=null
  Act:     call FindFollowerBracketOrder(orders, "ATM1", isStop:false, leaderName:null)
  Assert:  result != null
           result.OrderType == OrderType.Limit

[Fact] T1_WorkingState_StillFound_Regression
  Arrange: single Stop order (OrderType.StopMarket, OrderState.Working, FromEntrySignal="ATM1")
  Act:     call FindFollowerBracketOrder(orders, "ATM1", isStop:true, leaderName:null)
  Assert:  result != null   (B143 regression -- Working must still be accepted)

[Fact] T1_AcceptedState_StillFound_Regression
  Arrange: single Target order (OrderType.Limit, OrderState.Accepted, FromEntrySignal="ATM1")
  Act:     call FindFollowerBracketOrder(orders, "ATM1", isStop:false, leaderName:null)
  Assert:  result != null   (B143 regression -- Accepted must still be accepted)

[Fact] T1_NullOrder_NotMatched_Guard
  Arrange: single order (OrderType.StopMarket, OrderState.Initialized, FromEntrySignal="ATM1")
           (Initialized is NOT in the accepted state set)
  Act:     call FindFollowerBracketOrder(orders, "ATM1", isStop:true, leaderName:null)
  Assert:  result == null   (non-accepted states still rejected)
```

### .csproj Registration

**File**: `src/PropTraderTools/PropTraderTools.csproj`

**Insertion point**: L161 contains `<Compile Include="Tests\B133Tests.cs" />`.
Insert the following on a **new line immediately after L161** (before `Tests\BgtmTests.cs` on L162):

```xml
    <Compile Include="Tests\B134Tests.cs" />
```

**BEFORE (L160-L163)**:

```xml
    <Compile Include="Tests\B132Tests.cs" />
    <Compile Include="Tests\B133Tests.cs" />
    <Compile Include="Tests\BgtmTests.cs" />
    <Compile Include="TradeCopierPanelB75Tests.cs" />
```

**AFTER (L160-L164)**:

```xml
    <Compile Include="Tests\B132Tests.cs" />
    <Compile Include="Tests\B133Tests.cs" />
    <Compile Include="Tests\B134Tests.cs" />
    <Compile Include="Tests\BgtmTests.cs" />
    <Compile Include="TradeCopierPanelB75Tests.cs" />
```

### Files Changed -- Ticket 1

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | MODIFY -- state filter at L2549 (add `&& order.OrderState != OrderState.Submitted`); update block comment above method |
| `src/PropTraderTools/Tests/B134Tests.cs` | NEW -- 5 xUnit [Fact] tests in `B134Ticket1Tests` |
| `src/PropTraderTools/PropTraderTools.csproj` | MODIFY -- insert `<Compile Include="Tests\B134Tests.cs" />` after L161 |

### 7-Scan Checklist -- Ticket 1

| Scan | Rule | Check | Pass Criterion |
|------|------|-------|----------------|
| SCAN-01 | JS-021: no `lock()` | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches in new/modified lines. The change is a pure predicate extension (`&& order.OrderState != OrderState.Submitted`); no state mutation, no lock. |
| SCAN-02 | JS-001: no `throw` in hot path | `grep -n "throw" src/PropTraderTools/CopyEngine.cs` (filter to FindFollowerBracketOrder scope L2538-2575) | 0 throw statements introduced. `FindFollowerBracketOrder` is a predicate-only method; the new condition adds a boolean conjunction, not a throw site. All `acc.Cancel()` calls live in `SyncAtmFollowerTarget` (L2340-2347) and `SyncAtmFollowerBracket`, both already wrapped in `try/catch`. |
| SCAN-03 | ASCII-only | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 non-ASCII characters in new/modified lines. `"Submitted"` is ASCII. Comment tokens `B134`, `DW-B144`, `DW-B145` are ASCII. No Unicode, no curly quotes, no emoji. |
| SCAN-04 | CYC <= 8 | Run `python scripts/complexity_audit.py` on CopyEngine.cs; check `FindFollowerBracketOrder` | Post-T1-only intermediate = 7. Post-T1+T2 (committed form) = 8. AT LIMIT; <= 8 PASS. |
| SCAN-05 | JS-002: `Order?` null contract | Confirm `return null;` at the closing line of `FindFollowerBracketOrder` is unchanged. | `return null;` at L2565 (or equivalent after line shift) is still present. The `Order?` nullable return type is unchanged. The null contract required for `SyncFollowerBracket`'s `if (fo == null) return;` guard is preserved. |
| SCAN-06 | Build: 0 errors | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | Exit code 0. 0 build errors. 0 new warnings. |
| SCAN-07 | All prior tests pass | Run test suite; check B133Tests.cs (10), B132Tests.cs (6), B131Tests.cs (7), B130Tests.cs (8), B129Tests.cs (13) | 0 regressions in prior block tests. B134Ticket1Tests: 5 PASS. B134Ticket2Tests (once T2 applied): 3 PASS. Total new: 8 PASS. |

---

## TICKET 2 -- DW-B145: Wrong bracket index returned

### Spec Requirements

- **DW-B145**: After Ticket 1 fixes the `Submitted`-state gap, all three ATM targets (Target1, Target2,
  Target3) now pass the state filter. `SignalOrNameMatches` fires via path (1) (`FromEntrySignal` match)
  for ALL of them because they all share the same `FromEntrySignal`. The first order in iteration wins.
  If iteration returns Target1 before Target3, `fo = Target1` when `SyncFollowerBracket` expected
  `fo = Target3`. Fix: add an exact-name guard in `FindFollowerBracketOrder` so that when `leaderName`
  is non-null, only the order whose `order.Name == leaderName` is accepted.

### Root Cause

[`SignalOrNameMatches`](src/PropTraderTools/CopyEngine.cs:2511) (L2511-2518):

```csharp
internal static bool SignalOrNameMatches(
    Order order, string? signalName, string? leaderName)
{
    if (signalName != null && order.FromEntrySignal == signalName)  // (1) fires for ALL ATM brackets
        return true;
    if (leaderName == null)                                          // (2) no fallback
        return false;
    return order.Name == leaderName;                                 // (3) exact name -- NEVER REACHED when signalName fires
}
```

When `SyncFollowerBracket` calls `FindFollowerBracketOrder` with `leaderOrder.FromEntrySignal = "ATM1"`
and `leaderOrder.Name = "Target3"`:

- Path (1) fires for Target1 (`FromEntrySignal == "ATM1"` -- true), returns `true`.
- Path (1) fires for Target2 (`FromEntrySignal == "ATM1"` -- true), returns `true`.
- Path (1) fires for Target3 (`FromEntrySignal == "ATM1"` -- true), returns `true`.
- Exact-name path (3) is never reached because path (1) short-circuits.

After T1, all three pass the state filter. The first in iteration order wins. If the `acc.Orders`
collection (or the injected list) returns Target1 first, `fo = Target1`. The cancel+resubmit then
moves Target1's price, not Target3's. The leader's Target3 drag is mis-applied to the wrong bracket.

**Fix location**: inside `FindFollowerBracketOrder` list overload, not in `SignalOrNameMatches`.
Modifying `SignalOrNameMatches` would break all callers that rely on signal-only match semantics
(e.g., stop-only sync where `leaderName` is the ATM stop name but `signalName` still qualifies).

### File

**`src/PropTraderTools/CopyEngine.cs`**

### Exact Edit

**Location**: After L2547 (the `SignalOrNameMatches` continue line), insert a new `if` guard.

The complete combined AFTER block was already shown in Ticket 1. For completeness, the T2-specific
delta against the T1-only intermediate is:

**T1-only intermediate (never committed)**:

```csharp
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
                // <<< T2 guard inserted here >>>
                if (order.OrderState != OrderState.Working // (3) branches -- B134 DW-B144
                    && order.OrderState != OrderState.Accepted
                    && order.OrderState != OrderState.Submitted)
                    continue;
                ...
            }
            return null;
        }
```

**T2 insertion (at `<<< T2 guard inserted here >>>`)**: one new `if` line:

```csharp
                if (leaderName != null && order.Name != leaderName) // (1) branch -- B134 DW-B145: require exact name when leaderName provided
                    continue;
```

**Final committed form** (same as shown in Ticket 1 AFTER block -- reproduced for self-containment):

```csharp
        // CYC=8 (post-B134). AT LIMIT; PASS.
        // foreach(1) + SignalOrNameMatches guard(1) + leaderName exact guard(1) + state filter(3) + isStop(1) + type match(1) = 8.
        // DW-B143: Accepted added. DW-B144: Submitted added. DW-B145: leaderName exact guard added.
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
                if (leaderName != null && order.Name != leaderName) // (1) branch -- B134 DW-B145: require exact name when leaderName provided
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

**`SignalOrNameMatches` is NOT modified.** CYC stays at 3. No regression risk to existing callers.

### CYC Analysis (Ticket 2)

| Stage | CYC | Formula |
|-------|-----|---------|
| Post-T1 only | 7 | foreach(1) + SignalOrNameMatches(1) + state-filter(3) + isStop(1) + type-match(1) = 7 |
| Post-T1+T2 (committed) | 8 | +1 branch for leaderName exact guard = 8 |
| SignalOrNameMatches (unchanged) | 3 | unchanged |

**Limit**: CYC <= 8. Post-T1+T2 = 8. AT LIMIT; PASS.

### New Tests -- Ticket 2

**File**: `src/PropTraderTools/Tests/B134Tests.cs` (SAME file as Ticket 1 -- both test classes in one file)
**Namespace**: `PropTraderTools`
**Test class**: `B134Ticket2Tests` (inside `B134FindFollowerBracketOrderTests` outer class)

> **DO NOT MODIFY any existing test file (B129Tests.cs, B130Tests.cs, B131Tests.cs, B132Tests.cs, B133Tests.cs). Only create new file Tests/B134Tests.cs.**

```
[Fact] T2_Target3_ReturnsTarget3_NotTarget1
  Arrange: list of three Target orders, all OrderState.Submitted, all FromEntrySignal="ATM1":
           order1: Name="Target1", OrderType.Limit
           order2: Name="Target2", OrderType.Limit
           order3: Name="Target3", OrderType.Limit
           leaderName="Target3"
  Act:     call FindFollowerBracketOrder(orders, "ATM1", isStop:false, leaderName:"Target3")
  Assert:  result != null
           result.Name == "Target3"    (NOT "Target1", NOT "Target2")

[Fact] T2_Target1_ReturnsTarget1_WhenRequested
  Arrange: same three-Target list as above, leaderName="Target1"
  Act:     call FindFollowerBracketOrder(orders, "ATM1", isStop:false, leaderName:"Target1")
  Assert:  result != null
           result.Name == "Target1"    (index 1 correctness)

[Fact] T2_NullLeaderName_ReturnsFirstMatch_BackwardCompat
  Arrange: same three-Target list, leaderName=null
  Act:     call FindFollowerBracketOrder(orders, "ATM1", isStop:false, leaderName:null)
  Assert:  result != null              (backward compat: signal-only match still works)
           result.OrderType == OrderType.Limit   (some target returned)
```

### Files Changed -- Ticket 2

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | MODIFY -- insert `if (leaderName != null && order.Name != leaderName) continue;` in `FindFollowerBracketOrder` after `SignalOrNameMatches` guard; update block comment (already covered by T1 AFTER block) |
| `src/PropTraderTools/Tests/B134Tests.cs` | MODIFY -- add `B134Ticket2Tests` class with 3 [Fact] tests (same file created by T1) |

**PropTraderTools.csproj is NOT modified again** -- it was registered in Ticket 1.

### 7-Scan Checklist -- Ticket 2

| Scan | Rule | Check | Pass Criterion |
|------|------|-------|----------------|
| SCAN-01 | JS-021: no `lock()` | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches in new/modified lines. The leaderName guard is a pure boolean predicate `leaderName != null && order.Name != leaderName`; no state mutation, no shared mutable state, no lock. |
| SCAN-02 | JS-001: no `throw` in hot path | `grep -n "throw" src/PropTraderTools/CopyEngine.cs` (FindFollowerBracketOrder scope) | 0 throw statements introduced. The new line is a `continue` guard. `FindFollowerBracketOrder` and `SignalOrNameMatches` (unchanged) contain zero Cancel calls and zero throw sites. |
| SCAN-03 | ASCII-only | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 non-ASCII characters in new/modified lines. `"leaderName"`, `"Target3"`, `"ATM1"` are all ASCII string literals. No Unicode, no curly quotes. |
| SCAN-04 | CYC <= 8 | `python scripts/complexity_audit.py` on CopyEngine.cs | Combined post-T1+T2 CYC of `FindFollowerBracketOrder` = 8. AT LIMIT; <= 8 PASS. `SignalOrNameMatches` = 3 (unchanged). All other methods in scope: unchanged. |
| SCAN-05 | JS-002: `Order?` null contract | Confirm `return null;` at closing line of `FindFollowerBracketOrder` unchanged. | `return null;` is the final statement; not removed or altered by T2 guard insertion. The new guard only adds an early `continue` inside the `foreach` -- it cannot reach past the closing `return null`. The null contract for `SyncFollowerBracket`'s `if (fo == null)` guard is preserved. |
| SCAN-06 | Build: 0 errors | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | Exit code 0. 0 build errors. 0 new warnings. |
| SCAN-07 | All prior tests pass | Run test suite: B133Tests.cs (10), B132Tests.cs (6), B131Tests.cs (7), B130Tests.cs (8), B129Tests.cs (13) | 0 regressions. B134 changes are (a) additive to state filter -- no existing test passes Submitted orders so no change in prior behavior; (b) restrictive to name selection -- the leaderName guard fires only when `leaderName != null` and existing callers that use `leaderName=null` are unaffected (guard condition is false when leaderName is null). B134Ticket2Tests: 3 PASS. |

---

## Combined Constraint Summary

| Constraint | Rule | Both Tickets | Evidence |
|------------|------|--------------|----------|
| No `lock()` | JS-021 (P0-CRITICAL) | PASS | Both changes are pure predicate guard extensions inside a `foreach`. No shared mutable state touched. No `lock()` introduced in any new/modified line. |
| No `throw` in hot path | JS-001 (P0-CRITICAL) | PASS | `FindFollowerBracketOrder` is a predicate-only selection method. Neither the state-filter extension (T1) nor the leaderName guard (T2) introduce a `throw`. All `acc.Cancel()` calls that receive the returned `Order?` live in `SyncAtmFollowerTarget` (L2340-2347, try/catch confirmed in plan §B.5) and `SyncAtmFollowerBracket` (try/catch confirmed). |
| `Order?` null contract preserved | JS-002 (P0-CRITICAL) | PASS | `return null;` at the closing line of `FindFollowerBracketOrder` is not touched by either ticket. The `Order?` nullable return type is unchanged. The null-safe call path at `SyncFollowerBracket` L2189 (`if (fo == null) return;`) is preserved. |
| ASCII-only | NT8/DNA mandate | PASS | All new string literals and comment tokens are ASCII. `"Submitted"`, `"Target3"`, `"ATM1"`, `"leaderName"`, `"B134"`, `"DW-B144"`, `"DW-B145"` are all 7-bit ASCII. No Unicode, emoji, or curly quotes in any modified line. |
| CYC <= 8 per method | Jane Street strict | PASS | Post-T1+T2: `FindFollowerBracketOrder` = 8 (AT LIMIT); `SignalOrNameMatches` = 3 (unchanged); all other methods unchanged. |
| `_diagnosticMode` untouched | Spec constraint | PASS | Neither ticket modifies any field or method related to `_diagnosticMode`. B134 is scoped entirely to `FindFollowerBracketOrder` and the new test file. |
| `PropTraderTools.csproj` registered | Build mandate | PASS | `<Compile Include="Tests\B134Tests.cs" />` inserted after `Tests\B133Tests.cs` on L161. Insertion point verified from live `.csproj` read (L155-L167). |

---

## Prior Block Regression Guard

The ptt-engineer MUST run the full test suite and confirm the following counts before reporting
Ticket completion. Any transition from PASS to FAIL is a blocker; do NOT commit.

| Test File | Expected Count | Acceptable Outcome |
|-----------|---------------|-------------------|
| `Tests/B133Tests.cs` | 10 | 10 PASS, 0 FAIL |
| `Tests/B132Tests.cs` | 6 | 6 PASS, 0 FAIL |
| `Tests/B131Tests.cs` | 7 | 7 PASS, 0 FAIL |
| `Tests/B130Tests.cs` | 8 | 8 PASS, 0 FAIL |
| `Tests/B129Tests.cs` | 13 | 13 PASS, 0 FAIL |
| `Tests/B134Tests.cs` (new) | 8 | 8 PASS, 0 FAIL (5 T1 + 3 T2) |

**Regression rationale**:
- T1 (Submitted state): No prior test exercises `OrderState.Submitted` input; existing tests use
  `Working` or `Accepted`. Existing behaviors are unaffected; new `Submitted` branch is additive only.
- T2 (leaderName guard): The guard is `if (leaderName != null && ...)`. When `leaderName == null`,
  the condition is false at the first operand (short-circuit) and the loop continues identically to
  pre-B134. All prior tests that call `FindFollowerBracketOrder` with `leaderName=null` or that do not
  rely on exact-name selection are unaffected.

---

## Engineer Execution Order

1. Apply the single combined edit to `FindFollowerBracketOrder` in `CopyEngine.cs` (T1+T2 together).
2. Create `src/PropTraderTools/Tests/B134Tests.cs` with both `B134Ticket1Tests` and `B134Ticket2Tests`.
3. Insert `<Compile Include="Tests\B134Tests.cs" />` in `PropTraderTools.csproj` after L161.
4. Run SCAN-01 through SCAN-07 for each ticket.
5. Run `powershell -File scripts\ptt-sync-and-verify.ps1` -- confirm 0 MISMATCH lines.
6. Press F5 in NinjaTrader 8 to recompile -- confirm green (0 errors).
7. Report completion with: SCAN results, test counts, ptt-sync-and-verify output.

---

*Tickets produced by ptt-architect, B134 Phase 3. Source: 02-architecture-plan.md (REVIEW_PASS).*
