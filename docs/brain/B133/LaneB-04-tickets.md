# B133 LaneB -- Ticket File
# DW-B143 FindFollowerBracketOrder Accepted-State Fix

Epic: B133
Lane: B
Phase: 3 (Ticket Generation)
Status: TICKETS_COMPLETE

---

## Ticket 1

**Title**: DW-B143 FindFollowerBracketOrder Accepted-state fix + B133LaneBTests
**Spec Req IDs**: DW-B143 (P1), B133-LANEB-TEST (required)
**Status**: READY

---

### 1. Files Modified

| File | Action | Scope |
|------|--------|-------|
| src/PropTraderTools/CopyEngine.cs | MODIFY | L2535: change state filter to include OrderState.Accepted |
| src/PropTraderTools/Tests/B133Tests.cs | CREATE or MODIFY | Add class B133LaneBTests with 5 [Fact] methods |

No other files are touched. Exactly 2 files are in scope.

---

### 2. Method Signatures / Exact Change

#### 2a. CopyEngine.cs -- One-line logical change at L2535

**BEFORE (L2535-2536)**:
```csharp
if (order.OrderState != OrderState.Working)
    continue;
```

**AFTER (L2535)**:
```csharp
if (order.OrderState != OrderState.Working && order.OrderState != OrderState.Accepted)
    continue;
```

Net diff: the second condition `&& order.OrderState != OrderState.Accepted` is appended to the
existing single-state guard. No other lines in CopyEngine.cs are changed.

Surrounding method signature (unchanged -- shown for engineer context):
```csharp
private Order? FindFollowerBracketOrder(
    Account follower,
    string? fromEntrySignalName,
    bool isStop,
    string? leaderName = null
)
```

CYC analysis:
- CYC before fix: 5
- CYC after fix: 6 (ceiling 8) -- PASS (JS-066)
- foreach (1) + SignalOrNameMatches guard (1) + state filter (2) + isStop (1) + OrderType match (1) = 6

#### 2b. B133Tests.cs -- New class B133LaneBTests

Class and method signatures (engineer writes full bodies using the testable seam):

```csharp
public class B133LaneBTests
{
    [Fact]
    public void FindFollowerBracketOrder_AcceptedState_IsFound() { ... }

    [Fact]
    public void FindFollowerBracketOrder_SubmittedState_IsNotFound() { ... }

    [Fact]
    public void FindFollowerBracketOrder_FilledState_IsNotFound() { ... }

    [Fact]
    public void FindFollowerBracketOrder_WorkingState_IsFound() { ... }

    [Fact]
    public void FindFollowerBracketOrder_CancelledState_IsNotFound() { ... }
}
```

Test seam: `FindFollowerBracketOrderTestable(account, fromEntrySignal, isStop, leaderName)`
at L2559-2564 in CopyEngine.cs -- same seam used by all prior B-block test classes.

If B133Tests.cs is absent (LaneA not yet run): engineer creates the file containing only
B133LaneBTests. If B133Tests.cs already exists (LaneA ran first): engineer appends
B133LaneBTests as a second class in the same file.

---

### 3. JS Rule Constraints

Every constraint below must hold for the changed lines in CopyEngine.cs and in B133Tests.cs.

| Constraint | Rule ID | Requirement |
|------------|---------|-------------|
| No lock() anywhere in src/ | JS-021 (P0 CRITICAL) | Zero new lock() constructs. The fix adds no lock(). Existing file must also remain lock()-free. |
| No throw new in hot paths | JS-001 (P0 CRITICAL) | Zero new throw new XxxException() in FindFollowerBracketOrder or B133LaneBTests. |
| No return null introduced | JS-002 (P0 CRITICAL) | The fix introduces zero new return null. The pre-existing return null at the end of FindFollowerBracketOrder (end of foreach loop) is unchanged and pre-dates this ticket -- it is NOT introduced by this fix. |
| No async void (non-event-handler) | JS-033 (P0 CRITICAL) | Zero new async void methods in changed files. |
| CYC <= 8 per method | JS-066 | FindFollowerBracketOrder CYC=6 after fix. All test methods CYC=1. All <= 8 ceiling. |
| ASCII-only identifiers and literals | JS-066 | No Unicode, emoji, or curly quotes in any new or changed line. All identifiers are ASCII. |
| xUnit [Fact] only -- never NUnit or MSTest | JS-051 | B133LaneBTests uses [Fact] attributes from xunit. No [Test], [TestMethod], [TestCase], or NUnit/MSTest imports permitted. |

---

### 4. xUnit Test Specifications

All 5 tests are in class `B133LaneBTests` in `src/PropTraderTools/Tests/B133Tests.cs`.
Each test calls `FindFollowerBracketOrderTestable` with an injected stub order list.

#### Test 1: FindFollowerBracketOrder_AcceptedState_IsFound

- **Purpose**: Primary DW-B143 regression-prevention test. Verifies that a bracket order in
  OrderState.Accepted is returned by the finder after the fix is applied.
- **Setup**:
  - Create stub Order with: OrderState = OrderState.Accepted, OrderType = OrderType.StopMarket,
    Name or FromEntrySignal matching the fromEntrySignalName argument.
  - isStop = true
- **Call**: `result = engine.FindFollowerBracketOrderTestable(account, fromEntrySignal, isStop: true, leaderName: null)`
- **Assert**: `Assert.NotNull(result)`

#### Test 2: FindFollowerBracketOrder_SubmittedState_IsNotFound

- **Purpose**: Verifies that Submitted orders are NOT returned. Submitted is explicitly excluded
  from the fix because NT8 Account.Cancel() on Submitted is unreliable. The filter must
  continue to skip Submitted orders.
- **Setup**:
  - Create stub Order with: OrderState = OrderState.Submitted, OrderType = OrderType.StopMarket,
    matching Name or FromEntrySignal.
  - isStop = true
- **Call**: `result = engine.FindFollowerBracketOrderTestable(account, fromEntrySignal, isStop: true, leaderName: null)`
- **Assert**: `Assert.Null(result)`

#### Test 3: FindFollowerBracketOrder_FilledState_IsNotFound

- **Purpose**: Verifies that Filled orders are NOT returned. Filled is a terminal state and must
  not be selected for cancel-and-resubmit.
- **Setup**:
  - Create stub Order with: OrderState = OrderState.Filled, OrderType = OrderType.Limit,
    matching Name or FromEntrySignal.
  - isStop = false
- **Call**: `result = engine.FindFollowerBracketOrderTestable(account, fromEntrySignal, isStop: false, leaderName: null)`
- **Assert**: `Assert.Null(result)`

#### Test 4: FindFollowerBracketOrder_WorkingState_IsFound (regression)

- **Purpose**: Regression guard. Verifies that the original Working-state path is preserved after
  the fix. Guards against the fix accidentally narrowing the filter too far.
- **Setup**:
  - Create stub Order with: OrderState = OrderState.Working, OrderType = OrderType.StopLimit,
    matching Name or FromEntrySignal.
  - isStop = true
- **Call**: `result = engine.FindFollowerBracketOrderTestable(account, fromEntrySignal, isStop: true, leaderName: null)`
- **Assert**: `Assert.NotNull(result)`

#### Test 5: FindFollowerBracketOrder_CancelledState_IsNotFound (regression)

- **Purpose**: Regression guard. Verifies that Cancelled orders are NOT returned. Cancelled is a
  terminal state and must remain excluded.
- **Setup**:
  - Create stub Order with: OrderState = OrderState.Cancelled, OrderType = OrderType.Limit,
    matching Name or FromEntrySignal.
  - isStop = false
- **Call**: `result = engine.FindFollowerBracketOrderTestable(account, fromEntrySignal, isStop: false, leaderName: null)`
- **Assert**: `Assert.Null(result)`

---

### 5. Regression Suite

All prior block tests must pass after Ticket 1 is applied. Engineer must run the full suite.

| Test Class | Count | File |
|------------|-------|------|
| B133LaneATests | 5 | src/PropTraderTools/Tests/B133Tests.cs |
| B132Tests | 5 | src/PropTraderTools/Tests/B132Tests.cs |
| B131Tests | 7 | src/PropTraderTools/Tests/B131Tests.cs |
| B130Tests | 8 | src/PropTraderTools/Tests/B130Tests.cs |
| B129Tests | 13 | src/PropTraderTools/Tests/B129Tests.cs |
| **Total prior regression** | **38** | all must pass |
| B133LaneBTests (new) | 5 | src/PropTraderTools/Tests/B133Tests.cs |
| **Total after Ticket 1** | **43** | target |

---

### 6. 7-Scan Checklist (SCAN-01 through SCAN-07)

Engineer contract: all 7 scans must be run and must return the expected result before the
ticket is reported complete. This is a Layer 1 hard gate.

| Scan | Command | Expected Result |
|------|---------|-----------------|
| SCAN-01 | `grep -rn "lock(" src/ --include="*.cs"` | 0 matches |
| SCAN-02 | `grep -rn "async void " src/ --include="*.cs"` | 0 new non-event-handler matches in changed files |
| SCAN-03 | `grep -n "return null;" src/PropTraderTools/CopyEngine.cs` | 0 NEW occurrences in FindFollowerBracketOrder (pre-existing return null at loop end is unchanged) |
| SCAN-04 | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 in FindFollowerBracketOrder |
| SCAN-05 | `python scripts/complexity_audit.py` | FindFollowerBracketOrder CYC=6, <= 8 ceiling |
| SCAN-06 | `grep -Prn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/Tests/B133Tests.cs` | 0 matches |
| SCAN-07 | `dotnet build src/PropTraderTools.csproj` | 0 errors, 0 warnings |

---

### 7. Acceptance Criteria

Ticket 1 is complete when ALL of the following are true:

- [ ] CopyEngine.cs compiles with 0 errors, 0 warnings (SCAN-07)
- [ ] FindFollowerBracketOrder_AcceptedState_IsFound passes -- fo != null for Accepted state
- [ ] FindFollowerBracketOrder_SubmittedState_IsNotFound passes -- fo == null for Submitted
- [ ] FindFollowerBracketOrder_FilledState_IsNotFound passes -- fo == null for Filled
- [ ] FindFollowerBracketOrder_WorkingState_IsFound passes -- fo != null for Working (regression)
- [ ] FindFollowerBracketOrder_CancelledState_IsNotFound passes -- fo == null for Cancelled (regression)
- [ ] All 38 regression tests pass (B133LaneA + B132 + B131 + B130 + B129)
- [ ] Total test count: 43 (38 regression + 5 new B133LaneBTests)
- [ ] SCAN-01 through SCAN-07 all return 0 violations

---

### 8. Compliance Confirmation (for ptt-verifier)

- No new lock() constructs introduced (JS-021)
- No new throw new constructs introduced (JS-001)
- No new return null constructs introduced (JS-002); pre-existing return null is unchanged
- No new async void constructs introduced (JS-033)
- CYC of FindFollowerBracketOrder after fix: 6, within ceiling 8 (JS-066)
- All identifiers ASCII-only, no Unicode or curly quotes (JS-066)
- Tests use xUnit [Fact] only, no NUnit/MSTest (JS-051)
- CreateOrder: N/A -- no CreateOrder calls introduced or modified
- PTT- order name prefix: N/A -- no new order submission in this fix
- DateTime.UtcNow: N/A -- no DateTime usage in this fix
