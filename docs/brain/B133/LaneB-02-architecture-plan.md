# B133 LaneB - Architecture Plan
# FindFollowerBracketOrder Accepted-State Fix

Status: REVIEW_PENDING
Lane: B
Epic: B133
Phase: 1 (Architecture)

---

## Section 1: CHANGE SUMMARY

**Defect ID**: DW-B143  
**P-Level**: P1  
**Title**: FindFollowerBracketOrder Working-only state filter misses Accepted follower orders  
**File**: src/PropTraderTools/CopyEngine.cs  
**Location**: FindFollowerBracketOrder body, state filter at L2535

### Root Cause

Confirmed by B132 SIM Test B TP4 trace (2026-09-04):

    TP4 output: fo=NULL followerOrders=[...,Stop1:Submitted,Target1:Submitted,...]

`FindFollowerBracketOrder` at L2535 contains a state filter:

    if (order.OrderState != OrderState.Working)
        continue;   // Skips Accepted AND Submitted orders

The leader-side gate `IsWorkingBracket` (L2131) already accepts `Working || Accepted`:

    return (
        order.OrderState == OrderState.Working
        || order.OrderState == OrderState.Accepted
    ) && IsBracketLegStatic(order);

There is an asymmetry: the leader gate is inclusive of Accepted; the follower lookup is not.
When a drag event fires while follower bracket orders are in the Accepted state -- which is
normal for newly submitted PTT-placed or ATM orders before the exchange confirms Working --
`FindFollowerBracketOrder` returns null. `SyncFollowerBracket` then returns early, no
PTT-STP-Drag is dispatched, and the bracket adjustment is silently dropped.

### Silent No-Op Failure Path

A user drags a stop or target on the leader account. NT8 fires the order-change event.
`SyncFollowerBracket` is called and attempts to locate the matching bracket order on the
follower account via `FindFollowerBracketOrder`. If the follower orders were placed
recently (either by PTT CopyEngine or by an ATM strategy) and are still in the broker-
accepted-but-not-yet-exchange-working state (`OrderState.Accepted`), the single-state
filter `!= OrderState.Working` causes the loop to skip every candidate and return null.
`SyncFollowerBracket` interprets null as "no follower bracket order exists" and exits
without issuing the cancel-and-resubmit sequence. The user's drag is acknowledged on the
leader but never replicated to the follower. No error is logged at the default diagnostic
level. The mismatch persists silently until the user drags again after the follower orders
have transitioned to Working state.

### Exact Before/After Diff

```
// BEFORE (L2535-2536 in CopyEngine.cs):
if (order.OrderState != OrderState.Working)
    continue;

// AFTER:
if (order.OrderState != OrderState.Working && order.OrderState != OrderState.Accepted)
    continue;
```

This is a one-line logical change (the second condition is appended with `&&`).
Net diff: +1 branch, +0 new methods, +0 new files in CopyEngine.cs.

---

## Section 2: SCOPE

### Files Touched (exactly 2)

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | MODIFY: L2535 -- extend state filter to also skip Accepted orders only when NOT Working AND NOT Accepted |
| `src/PropTraderTools/Tests/B133Tests.cs` | MODIFY (or CREATE): add class B133LaneBTests with 5 [Fact] methods; file may have been created by LaneA (which adds B133LaneATests); if absent, LaneB engineer creates it fresh |

### Files NOT Touched

| File | Reason |
|------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` | UI layer -- no state-filter logic involved |
| `src/PropTraderTools/GateEngine.cs` | Gate evaluation -- IsWorkingBracket already correct; not changed |
| `src/PropTraderTools/OrderRouter.cs` | Order routing -- does not contain FindFollowerBracketOrder |
| `src/PropTraderTools/DiagnosticLogger.cs` | Diagnostic layer -- TryLogSFBTrace not modified per spec |
| `src/PropTraderTools/CopyEngine.SIMGate.cs` | SIM gate partial -- no state-filter changes required |
| `src/PropTraderTools/Tests/B132Tests.cs` | Prior-block tests -- run as regression only, not modified |
| `src/PropTraderTools/Tests/B131Tests.cs` | Prior-block tests -- run as regression only, not modified |

---

## Section 3: FIX DESIGN

### Exact Before/After Code Diff

```csharp
// BEFORE (CopyEngine.cs L2535-2536):
if (order.OrderState != OrderState.Working)
    continue;

// AFTER (CopyEngine.cs L2535):
if (order.OrderState != OrderState.Working && order.OrderState != OrderState.Accepted)
    continue;
```

The surrounding method body is unchanged:

```csharp
private Order? FindFollowerBracketOrder(
    Account follower,
    string? fromEntrySignalName,
    bool isStop,
    string? leaderName = null
)
{
    foreach (var order in follower.Orders.ToList())                         // branch (1)
    {
        if (!SignalOrNameMatches(order, fromEntrySignalName, leaderName))   // branch (2)
            continue;
        if (order.OrderState != OrderState.Working                          // branch (3+4)
            && order.OrderState != OrderState.Accepted)
            continue;
        if (isStop)                                                         // branch (5)
        {
            if (
                order.OrderType == OrderType.StopMarket
                || order.OrderType == OrderType.StopLimit
            )                                                               // branch (6)
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

### CYC Analysis

| State | Branches | CYC | Ceiling | Result |
|-------|----------|-----|---------|--------|
| Before fix | foreach(1) + SignalOrNameMatches(1) + OrderState!=Working(1) + isStop(1) + OrderType match(1) | 5 | 8 | PASS |
| After fix | foreach(1) + SignalOrNameMatches(1) + OrderState filter(2) + isStop(1) + OrderType match(1) | 6 | 8 | PASS |

CYC increases from 5 to 6. The ceiling is 8. This fix passes the Jane Street strict standard.

### Why Accepted Is Safe and Submitted Is Excluded

**Accepted**: NT8 documentation confirms `OrderState.Accepted` means the order has been
accepted by the broker or exchange. An `Account.Cancel()` call on an Accepted order
succeeds reliably in NT8 (both Live and Sim). The existing `IsWorkingBracket` method at
L2131 already treats Accepted as a valid state for leader-side operations. This fix
mirrors that established pattern.

**Submitted**: `OrderState.Submitted` means the order is in transit to the broker but has
not yet been confirmed. NT8 Sim gate behavior for `Account.Cancel()` on Submitted orders
is not reliably supported; the B132 SIM test TP4 trace showed Submitted orders in the
follower list but the spec explicitly excludes Submitted from the fix to avoid unreliable
cancel behavior. Submitted orders will continue to be skipped by the filter.

### Mirror to IsWorkingBracket Pattern

`IsWorkingBracket` (L2131) already expresses the correct business rule:

    order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted

This fix aligns `FindFollowerBracketOrder`'s filter to accept exactly the same set of
states as `IsWorkingBracket` evaluates as valid for leader orders, eliminating the
asymmetry that caused the silent no-op.

### Compliance Statements

- "This fix introduces no new lock(), throw new, return null, or async void constructs."
- "All new identifiers are ASCII-only. No Unicode, emoji, or curly quotes."
- "CreateOrder: N/A -- no new CreateOrder calls introduced."

---

## Section 4: TEST STRATEGY

### Test Class Location

File: `src/PropTraderTools/Tests/B133Tests.cs`  
Class: `B133LaneBTests`  
Note: LaneA writes class `B133LaneATests` in this same file. LaneB adds a second class
`B133LaneBTests`. Both classes coexist in the same file. If LaneA has not run when LaneB
engineer executes, the engineer creates `B133Tests.cs` with only `B133LaneBTests`; if
LaneA has already run, the engineer appends `B133LaneBTests` to the existing file.

### Test Seam

`FindFollowerBracketOrderTestable` exists at L2559-2564 in CopyEngine.cs. This is the
same seam used by all prior B-block tests. It accepts an injected order list, bypassing
`Account.Orders`, enabling pure unit tests without NT8 runtime.

Order is NOT sealed in the test assembly. Tests directly instantiate or mock `Order`
stubs and set `OrderState`, `OrderType`, `Name`, and `FromEntrySignal` fields directly.
This pattern was established by LaneA and all prior B-block test classes.

### 5 Named [Fact] Methods

#### Test 1: FindFollowerBracketOrder_AcceptedState_IsFound

- **Description**: Verifies that a bracket order in OrderState.Accepted is returned by the
  finder after the fix. This is the primary regression-prevention test for DW-B143.
- **Inputs**:
  - OrderState: Accepted
  - OrderType: StopMarket
  - Name / FromEntrySignal: matching fromEntrySignalName
  - isStop: true
- **Expected**: returned Order is not null; Order.OrderState == Accepted

#### Test 2: FindFollowerBracketOrder_SubmittedState_IsNotFound

- **Description**: Verifies that a bracket order in OrderState.Submitted is NOT returned.
  Submitted is explicitly excluded from the fix because NT8 cancel on Submitted is
  unreliable. The filter must continue to skip Submitted orders.
- **Inputs**:
  - OrderState: Submitted
  - OrderType: StopMarket
  - isStop: true
- **Expected**: returned Order is null

#### Test 3: FindFollowerBracketOrder_FilledState_IsNotFound

- **Description**: Verifies that a bracket order in OrderState.Filled is NOT returned.
  Filled orders are terminal and must not be selected for cancel-and-resubmit.
- **Inputs**:
  - OrderState: Filled
  - OrderType: Limit
  - isStop: false
- **Expected**: returned Order is null

#### Test 4: FindFollowerBracketOrder_WorkingState_IsFound (regression)

- **Description**: Verifies that the original Working-state path is preserved after the fix.
  This guards against the fix accidentally narrowing the filter too far.
- **Inputs**:
  - OrderState: Working
  - OrderType: StopLimit
  - isStop: true
- **Expected**: returned Order is not null; Order.OrderState == Working

#### Test 5: FindFollowerBracketOrder_CancelledState_IsNotFound (regression)

- **Description**: Verifies that a bracket order in OrderState.Cancelled is NOT returned.
  Cancelled is a terminal state and must remain excluded.
- **Inputs**:
  - OrderState: Cancelled
  - OrderType: Limit
  - isStop: false
- **Expected**: returned Order is null

### Regression Table

| Class | Tests | Source |
|-------|-------|--------|
| B133LaneATests | 5 | B133 LaneA (DW-B142 drag-null fix) |
| B132Tests | 5 | B132 block |
| B131Tests | 7 | B131 block |
| B130Tests | 8 | B130 block |
| B129Tests | 13 | B129 block |
| **Total prior** | **38** | all must pass |

All 38 prior tests plus the 5 new B133LaneBTests must pass. Total after B133 LaneB: 43 tests.

---

## Section 5: SCAN CHECKLIST

All 7 scans are mandatory. Engineer must run each command and confirm the expected result
before reporting ticket completion.

| Scan ID | Description | Command | Expected Result |
|---------|-------------|---------|-----------------|
| SCAN-01 | No lock() usage anywhere in src/ | `grep -rn "lock(" src/ --include="*.cs"` | 0 matches |
| SCAN-02 | No async void (non-event-handler) | `grep -rn "async void " src/ --include="*.cs"` | 0 matches in changed method |
| SCAN-03 | No return null in changed method | `grep -n "return null;" src/PropTraderTools/CopyEngine.cs` | 0 new occurrences in FindFollowerBracketOrder |
| SCAN-04 | No throw new in changed method | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 in FindFollowerBracketOrder |
| SCAN-05 | CYC audit confirms <= 8 | `python scripts/complexity_audit.py` | FindFollowerBracketOrder CYC=6, <= 8 ceiling |
| SCAN-06 | ASCII-only in changed files | `grep -Prn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/Tests/B133Tests.cs` | 0 matches |
| SCAN-07 | Build succeeds | `dotnet build src/PropTraderTools.csproj` | 0 errors, 0 warnings |

---

## Section 6: RISKS / DEFERRED WORK

### DW- Items

None. All NT8 API facts were confirmed before this plan was written:
- OrderState.Accepted cancel behavior: confirmed safe (NT8 documentation + IsWorkingBracket precedent)
- OrderState.Submitted exclusion: confirmed correct (unreliable cancel in NT8 Sim, spec-mandated)
- FindFollowerBracketOrderTestable seam: confirmed exists at L2559
- IsWorkingBracket Working||Accepted pattern: confirmed at L2131

No deferred work items are opened for this lane.

### Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| B133Tests.cs absent when LaneB runs (LaneA not yet complete) | Low-Medium | Engineer creates the file from scratch with only B133LaneBTests; LaneA will add B133LaneATests in its own ticket execution |
| B133Tests.cs already present (LaneA ran first) | Low-Medium | Engineer appends B133LaneBTests class; file already compiles cleanly; no conflict since class names are distinct |
| Order not mockable in test assembly | Very Low | Established by all prior B-block lanes; if sealed restriction discovered, use FindFollowerBracketOrderTestable with list injection pattern already confirmed |

### Compliance Confirmation

- No new lock() constructs introduced.
- No new throw new constructs introduced.
- No new return null constructs introduced (existing return null at end of FindFollowerBracketOrder is unchanged).
- No new async void constructs introduced.
- All identifiers are ASCII-only.
- CreateOrder: N/A -- no CreateOrder calls introduced or modified.
- _diagnosticMode remains true through B133 SIM as specified.
- TryLogSFBTrace at L1761 is not touched.
