# B118 Architecture Plan -- DW-B126 BE/QX Race Condition Fix

**Block**: B118
**Phase**: 1 (Architecture)
**Status**: REVIEW_PENDING
**Architect**: ptt-architect
**Date**: 2026-08-28
**Rules Catalog Gate**: PASS (JS-001, JS-021, JS-033 verified clean)

---

## Section A -- Defect Root Cause

### DW-B126 (P1): BE/QX Race Condition

**Scenario**: QX-ALL is pressed within ~3 seconds of BE-ALL. PTT-BE bracket orders
(`PTT-BE-Target-N` and `PTT-BE-Stop-N`) are still in Working or Accepted state at the
moment QX-ALL fires.

**Sequence that produces the defect**:

```
T=0    BE-ALL fires:  PTT-BE-Target-1 (Limit) + PTT-BE-Stop-1 (StopMarket) submitted
T=0.5  PTT-BE brackets reach Working state in NT8 order book
T=1    QX-ALL fires:  Execute() called on UI thread
T=1.1  SnapshotTargetOrders() scans acc.Orders -- sees PTT-BE-Target-1 as Working
         (includes it in pttTargets, used only when nativeTargets is empty)
T=1.2  ExecuteOne(acc) called -- PttQuickExit.Execute() begins
T=1.3  PttQuickExit cancels ATM brackets (not PTT-BE brackets -- they are named differently)
T=1.4  PttQuickExit snapshots position (e.g. long 4 contracts) and submits PTT-QX-Stop (4 qty)
T=1.5  PTT-BE-Target-1 fills (1 contract) -- position now 3 long
T=1.6  PTT-BE-Stop-1 OCO partner triggers -- fills remaining 3 contracts (or gets cancelled)
T=1.7  PTT-QX-Stop (qty=4) is now active against a position of 0-3 contracts
         OVERSELL: PTT-QX sells 4 when only 0-3 remain -> leaves 1-4 contract short
```

**Root cause (precise)**: `PttGlobalQuickExit.Execute()` calls `SnapshotTargetOrders()` and
`ExecuteOne()` while PTT-BE-Target-* and PTT-BE-Stop-* orders are still in active (non-terminal)
states. The QX stop order is sized at snapshot time to the current position. PTT-BE fills
that occur BETWEEN the snapshot and the QX stop arrival at the exchange reduce the position
below the stop order size, causing oversell.

**PttQuickExit.Execute() does NOT cancel PTT-BE orders** because its internal cancel sweep
targets ATM bracket patterns (Target1..9, Stop1..9) and PTT-QX-T* patterns, not PTT-BE-* patterns.

**Evidence**: Gate #3 (2026-08-27) -- Sim103 left 4-contract short after QX.
`PTT-BE-Target-1` filled 1 contract, `PTT-BE-Stop-1` filled 3 contracts before QX cancel
window closed. QX stop orders then oversold the residual position.

### DW-B127 (P2): Stale QX Window (Second Press)

**Root cause**: Rapid double-press of QX-ALL allowed PTT-BE orders to race against both
QX executions. With the cancel-first fix below, the second QX press finds zero PTT-BE orders
in active state (all terminal from first press). DW-B127 is structurally eliminated.

---

## Section B -- Fix Design

### Principle: Cancel-First, Then Snapshot

The fix inserts a cancel-and-wait step for all PTT-BE-* orders BEFORE `SnapshotTargetOrders()`
is called. Once PTT-BE orders are confirmed terminal, the QX snapshot sees only the true
residual position and order book, and QX stop orders are sized correctly.

### Step-by-Step: leader path in Execute()

**Before (current)**:
```
// line 47 (original)
var targets = SnapshotTargetOrders(acc, pos.Instrument);
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
```

**After (B118 fix)**:
```
// B118 DW-B126: cancel PTT-BE-* BEFORE snapshot to eliminate BE/QX race.
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
// PTT-BE-* are now terminal -- snapshot sees clean order book.
var targets = SnapshotTargetOrders(acc, pos.Instrument);
double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
```

### Step-by-Step: follower path in Execute()

**Before (current)**:
```
// line 89 (original)
var followerTargets = SnapshotTargetOrders(follower, pos.Instrument);
```

**After (B118 fix)**:
```
// B118 DW-B126: cancel follower PTT-BE-* BEFORE snapshot (same race applies to followers).
int _fBeCancelCount = CancelPttBeOrders(follower, pos.Instrument);
WaitForPttBeCancelled(follower, pos.Instrument, _fBeCancelCount, 1000);
var followerTargets = SnapshotTargetOrders(follower, pos.Instrument);
```

### CancelPttBeOrders -- Method Design

**Signature**:
```csharp
internal static int CancelPttBeOrders(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Instrument instr)
```

**Logic**:
1. Guard: if acc == null || instr == null, return 0.
2. Build toCancel list: iterate acc.Orders.ToList() snapshot.
3. For each order o:
   - Skip if o == null.
   - Skip if o.Instrument == null || o.Instrument.FullName != instr.FullName.
   - Skip if !IsPttBeOrder(o.Name).
   - Skip if !IsNonTerminalPttBeState(o.OrderState). (only cancel active orders)
   - Add o to toCancel.
4. If toCancel is empty: log no-op and return 0.
5. Call acc.Cancel(toCancel.ToArray()).
6. Log count and return toCancel.Count.

**CYC**: 7 (null guard, instr guard, foreach, null check, instrOk, IsPttBeOrder, stateOk).

**NT8 API**: `Account.Cancel(IEnumerable<Order> orders)`
  Source: NT8_FULL_REFERENCE.md -- Account.Cancel() (scraped 2026-08-20).
  Syntax: `Cancel(IEnumerable<order> orders)`.
  Pattern already used at CopyEngine.cs lines 792, 891, 930, 2115, 2434, 3072, 3102.

### WaitForPttBeCancelled -- Method Design

**Signature**:
```csharp
internal static void WaitForPttBeCancelled(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Instrument instr,
    int expectedCount,
    int maxWaitMs)
```

**Logic**:
1. Guard: if acc == null || expectedCount <= 0, return immediately (no-op fast path).
2. Compute deadline: `var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs)`.
3. Poll loop (while DateTime.UtcNow < deadline):
   a. int nonTerminal = 0.
   b. Iterate acc.Orders.ToList() snapshot.
   c. For each o: skip if null, instrument mismatch, or !IsPttBeOrder(o.Name).
   d. If IsNonTerminalPttBeState(o.OrderState): nonTerminal++.
   e. If nonTerminal == 0: break (all confirmed terminal).
   f. Thread.Sleep(20). (20ms per iteration, max 50 iterations = 1000ms)
4. If loop exited via timeout: log warning with acc.Name (fail-safe, do not throw).

**CYC**: 7 (null/count guard, while loop, foreach, null check, instrOk, IsPttBeOrder, nonTerminal check).

**Threading**: Synchronous execution on the calling thread (NT8 UI/button thread).
  - Thread.Sleep(20) on UI thread is acceptable: NT8 SIM cancels confirm in < 50ms typical.
  - Existing Execute() already blocks the UI thread during sequential account processing.
  - maxWaitMs = 1000ms is bounded and safe.
  - acc.Orders reflects updated OrderStates while UI thread sleeps (NT8 internal thread updates).

**Fail-safe**: timeout logs a warning but does NOT throw. Execution proceeds to QX logic.
  Worst case on timeout: the original race condition (benign relative to hanging or crashing).

**Caller contract**: Always pass expectedCount = return value of CancelPttBeOrders.
  If CancelPttBeOrders returns 0, WaitForPttBeCancelled returns immediately (fast path).

### IsPttBeOrder -- Helper Predicate

**Signature**:
```csharp
private static bool IsPttBeOrder(string name)
```

**Logic**:
```csharp
return !string.IsNullOrEmpty(name)
    && (name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
        || name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal));
```

**CYC**: 1. Extracted to keep CancelPttBeOrders and WaitForPttBeCancelled within CYC budget.

### IsNonTerminalPttBeState -- Helper Predicate

**Signature**:
```csharp
private static bool IsNonTerminalPttBeState(NinjaTrader.Cbi.OrderState s)
```

**Logic**:
```csharp
return s != NinjaTrader.Cbi.OrderState.Cancelled
    && s != NinjaTrader.Cbi.OrderState.Filled
    && s != NinjaTrader.Cbi.OrderState.Rejected
    && s != NinjaTrader.Cbi.OrderState.PartFilled
    && s != NinjaTrader.Cbi.OrderState.Unknown;
```

**CYC**: 1. Terminal states sourced from NT8_FULL_REFERENCE.md lines 976-997.
  CancelPending and CancelSubmitted are NON-terminal (cancel not yet confirmed by exchange);
  WaitForPttBeCancelled correctly continues polling through these intermediate states.

**Why not Order.IsTerminalState()**: NT8_FULL_REFERENCE.md line 829 documents this method
but its exact set of terminal states is not enumerated. Explicit predicate is safer and
avoids version-specific NT8 behavior on PartFilled classification.

### How DW-B127 is Structurally Eliminated

When QX-ALL is pressed a second time rapidly:
- First press: CancelPttBeOrders finds PTT-BE-* orders and sends cancel. WaitForPttBeCancelled
  waits for terminal confirmation. By the time ExecuteOne fires and PTT-QX-* orders are
  submitted, PTT-BE-* are in Cancelled/Filled state.
- Second press: CancelPttBeOrders scans acc.Orders and finds zero PTT-BE-* orders in
  non-terminal states. Returns 0. WaitForPttBeCancelled fast-path returns immediately.
- The _qxCancelInProgress guard in ExecuteOne (existing B113 logic) prevents double-submission
  of PTT-QX orders on the follower path.
- DW-B127 is eliminated: the cancel-first gate guarantees no active PTT-BE orders remain
  when any QX execution proceeds.

---

## Section C -- CYC Budget

| Method | CYC | Branches | Status |
|--------|-----|----------|--------|
| Execute() | 8 | acc loop, follower guard, pos loop, null/flat, rule null, follower foreach, follower null, delegate | UNCHANGED -- 2 method calls added (no branches) |
| ExecuteOne() | 2 | follower guard, delegate | UNCHANGED |
| SnapshotTargetOrders() | 5 | null guard, foreach, stateOk, isTarget, dedup loop | UNCHANGED |
| ScaleLeaderTargets() | 3 | leaderPosQty guard, last-tranche, loop | UNCHANGED |
| ResolveFollowerTargets() | 4 | partial-reject, count-match, empty-leader, delegate | UNCHANGED |
| CancelPttBeOrders() | 7 | acc null, instr null, foreach, o null, instrOk, IsPttBeOrder, stateOk | NEW -- within budget |
| WaitForPttBeCancelled() | 7 | acc/count guard, while, foreach, o null, instrOk, IsPttBeOrder, nonTerminal | NEW -- within budget |
| IsPttBeOrder() | 1 | expression | NEW helper -- extracted to keep callers within CYC budget |
| IsNonTerminalPttBeState() | 1 | expression | NEW helper -- extracted to keep callers within CYC budget |

All methods: CYC <= 8. Jane Street strict standard satisfied.

---

## Section D -- Preserved Patterns

The following patterns are explicitly NOT changed by B118:

### _qxCancelInProgress (ConcurrentDictionary<string, bool>)
- Location: CopyEngine.cs line 267; used in ExecuteOne() lines 209, 238.
- Role: Intent-guard preventing TryReplacePttBeBrackets from racing with QX-ALL submit.
- B118 status: UNCHANGED. ExecuteOne() is not modified.

### _qxPendingFollowerCleanup (ConcurrentDictionary<string, (Instrument, DateTime)>)
- Location: CopyEngine.cs line 276; used in ExecuteOne() line 217.
- Role: Cleanup map for follower ATM bracket re-arm detection after QX.
- B118 status: UNCHANGED. ExecuteOne() is not modified.

### DW-B115-DIAG logging blocks
- Location: Execute() lines 66-80 (leader DIAG) and lines 93-121 (follower DIAG).
- Role: Director-retained diagnostics for DW-B115 root cause analysis.
- B118 status: UNCHANGED. The 2 new lines (CancelPttBeOrders + WaitForPttBeCancelled) are
  inserted BEFORE the first DIAG block in the leader path and BEFORE the follower snapshot
  in the follower path. The DIAG blocks themselves are not touched.

### ExecuteOne() follower path structure
- Location: ExecuteOne() lines 199-253.
- Role: Follower submit via _qxCancelInProgress guard + _qxPendingFollowerCleanup arm + try/finally.
- B118 status: UNCHANGED. The cancel-first step happens in Execute() before ExecuteOne() is called,
  not inside ExecuteOne(). ExecuteOne() remains CYC=2.

### PTT-QX-GUARD log line
- Location: ExecuteOne() line 201.
- Role: Diagnostic marker for follower submit window.
- B118 status: UNCHANGED.

### SnapshotTargetOrders() DW-B106 two-pass logic
- Location: lines 306-326.
- Role: Native-first discriminator prevents PTT-BE-Target-* inflation when native ATM targets exist.
- B118 status: UNCHANGED. The cancel-first step ensures PTT-BE-* orders are terminal before
  SnapshotTargetOrders runs, so they will not be in Working/Accepted state and will be excluded
  from pttTargets automatically (existing stateOk filter at line 281-283).

---

## Section E -- NT8 API

All NT8 API claims below are sourced from `docs/standards/NT8_FULL_REFERENCE.md`.

### Account.Cancel(IEnumerable<Order> orders)

**Source**: NT8_FULL_REFERENCE.md lines 2408-2451 (scraped 2026-08-20).
**URL**: https://developer.ninjatrader.com/docs/desktop/cancel
**Syntax**: `Cancel(IEnumerable<order> orders)`
**Usage in B118**:
```csharp
var toCancel = new System.Collections.Generic.List<NinjaTrader.Cbi.Order>();
// ... populate toCancel ...
acc.Cancel(toCancel.ToArray());
```
**Existing usage pattern**: CopyEngine.cs lines 792, 891, 930, 2115, 2434, 3072, 3102.
**Thread safety**: Called from UI thread in AddOn context. Same pattern as existing usages.

**NOTE**: `CancelOrder(Order order)` (StrategyBase method, NT8_FULL_REFERENCE.md line 1057)
is NOT used here. That method is only available on StrategyBase/NinjaScript derivatives.
PttGlobalQuickExit is a plain C# class (AddOn pattern). The correct method is `Account.Cancel()`.

### OrderState enum (non-terminal values for polling)

**Source**: NT8_FULL_REFERENCE.md lines 922-998 (OrderState Values table).

| State | Terminal? | Reason |
|-------|-----------|--------|
| OrderState.Initialized | No | Order not yet submitted |
| OrderState.Submitted | No | Cancel not confirmed |
| OrderState.Accepted | No | Cancel not confirmed |
| OrderState.TriggerPending | No | Cancel not confirmed |
| OrderState.Working | No | Cancel not confirmed |
| OrderState.ChangePending | No | Cancel not confirmed |
| OrderState.ChangeSubmitted | No | Cancel not confirmed |
| OrderState.CancelPending | No | Cancel in NT8 queue |
| OrderState.CancelSubmitted | No | Cancel sent to broker |
| OrderState.Cancelled | **YES** | Exchange confirmed cancel |
| OrderState.Rejected | **YES** | Order rejected |
| OrderState.PartFilled | **YES** | Position already reduced |
| OrderState.Filled | **YES** | Order completely filled |
| OrderState.Unknown | **YES** | No action possible |

**IsNonTerminalPttBeState** returns true for all Non-terminal states (continue polling).
**WaitForPttBeCancelled** exits when all PTT-BE-* orders report terminal states.

### acc.Orders enumeration pattern

**Source**: NT8_FULL_REFERENCE.md -- AddOn context.
**Existing pattern**: `acc.Orders.ToList()` snapshot prevents `InvalidOperationException`
from concurrent modification. Used throughout CopyEngine.cs (lines 2418, 2539, 2940, etc.).
**B118 usage**: Both CancelPttBeOrders and WaitForPttBeCancelled use `acc.Orders.ToList()`.

---

## Section F -- Test Plan

**File**: `src/PropTraderTools/Tests/B118Tests.cs` (new file)
**Framework**: xUnit only ([Fact] attributes). No NUnit, no MSTest (JS testing mandate).
**Access**: Methods are `internal static` -- accessible via `InternalsVisibleTo` (existing project config).

### Test Inventory

```
T_B118_CancelPttBeOrders_ReturnsCancelledCount
  Arrange: StubAccount with 2 PTT-BE-Target-1 + 1 PTT-BE-Stop-1 in Working state, 1 native Target1.
  Act: CancelPttBeOrders(acc, instr).
  Assert: returns 3 (only PTT-BE-* orders counted, not native Target1).

T_B118_CancelPttBeOrders_SkipsTerminalOrders
  Arrange: StubAccount with 1 PTT-BE-Target-1 in Cancelled state + 1 PTT-BE-Stop-1 in Working state.
  Act: CancelPttBeOrders(acc, instr).
  Assert: returns 1 (only the Working order counted; Cancelled already terminal).

T_B118_CancelPttBeOrders_SkipsNonPttBeOrders
  Arrange: StubAccount with 1 Target1 Working, 1 PTT-QX-T1 Working, 0 PTT-BE-* orders.
  Act: CancelPttBeOrders(acc, instr).
  Assert: returns 0 (no PTT-BE-* orders found).

T_B118_WaitForPttBeCancelled_ReturnsImmediately_WhenExpectedCountZero
  Arrange: StubAccount (any state). expectedCount = 0.
  Act: WaitForPttBeCancelled(acc, instr, 0, 1000).
  Assert: returns without Thread.Sleep (fast path verified via elapsed time < 5ms).

T_B118_WaitForPttBeCancelled_ReturnsImmediately_WhenAllTerminalOnFirstPoll
  Arrange: StubAccount with 1 PTT-BE-Target-1 in Cancelled state. expectedCount = 1.
  Act: WaitForPttBeCancelled(acc, instr, 1, 1000).
  Assert: returns without sleeping (elapsed time < 5ms -- all orders already terminal on first scan).

T_B118_WaitForPttBeCancelled_TimesOutGracefully_WhenOrdersStayNonTerminal
  Arrange: StubAccount with 1 PTT-BE-Stop-1 permanently in Working state. expectedCount = 1.
  Act: WaitForPttBeCancelled(acc, instr, 1, 100). (100ms timeout for test speed)
  Assert: returns (does not hang). Does not throw. Returns after ~100ms.

T_B118_CancelPttBeOrders_ReturnsZero_WhenNoPttBeOrdersExist
  Arrange: StubAccount with 3 native Target1-3 Working. No PTT-BE-* orders.
  Act: CancelPttBeOrders(acc, instr).
  Assert: returns 0. (DW-B127 structural elimination: second QX press fast path)

T_B118_IsPttBeOrder_MatchesTargetAndStop
  Arrange: string literals "PTT-BE-Target-1", "PTT-BE-Target-10", "PTT-BE-Stop-1", "Target1",
           "PTT-QX-T1", "", null.
  Act: IsPttBeOrder(name) for each.
  Assert: true for PTT-BE-Target-1/10, PTT-BE-Stop-1. False for all others.
```

**Total tests**: 8 xUnit [Fact] tests.

---

## Section G -- File Scope

### Modified Files

| File | Change Type | Description |
|------|-------------|-------------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | MODIFIED | Add 4 new methods; insert 4 lines in Execute() |

### New Files

| File | Change Type | Description |
|------|-------------|-------------|
| `src/PropTraderTools/Tests/B118Tests.cs` | NEW | 8 xUnit tests for cancel-first path |

### Unchanged Files (explicit list)

- `src/PropTraderTools/CopyEngine.cs` -- NO CHANGE
- `src/PropTraderTools/Features/PttBreakEven.cs` -- NO CHANGE
- `src/PropTraderTools/Features/PttBreakEvenSwap.cs` -- NO CHANGE
- `src/PropTraderTools/Features/PttQuickExit.cs` -- NO CHANGE
- `src/PropTraderTools/TradeCopierPanel.cs` -- NO CHANGE
- `src/PropTraderTools/Features/PttCancel.cs` -- NO CHANGE
- All existing test files -- NO CHANGE (existing tests must stay green)

### New Method Location in PttGlobalQuickExit.cs

Insertion order (after existing methods, before closing brace of class):
1. `CancelPttBeOrders()` -- internal static
2. `WaitForPttBeCancelled()` -- internal static
3. `IsPttBeOrder()` -- private static
4. `IsNonTerminalPttBeState()` -- private static

---

## Section H -- DW-B126 Closure Criteria

The following are the observable conditions that confirm DW-B126 is fixed:

### 1. Output Tab Evidence (NT8 SIM gate)

During QX-ALL after BE-ALL, the NT8 Output tab MUST show:

**Required lines** (before existing QX lines):
```
[PTT-QX-ALL] CancelPttBeOrders: acc=Sim101 instr=... count=N
[PTT-QX-ALL] WaitForPttBeCancelled: acc=Sim101 completed ms=X
[PTT-QX-ALL] CancelPttBeOrders: acc=Sim102 instr=... count=N
[PTT-QX-ALL] WaitForPttBeCancelled: acc=Sim102 completed ms=X
... (one pair per account with open position)
```

**Absence of oversell**: After QX-ALL completes, all accounts must show flat or long/short
position matching expected exit. No position quantity overshoot (no negative where 0 expected).

### 2. No Naked Position

All accounts with pre-QX positions must have PTT-QX-Stop orders in Working state.
No account should have an open position without a corresponding PTT-QX protective stop.

### 3. PTT-QX Stop Qty Matches Residual Position

Each PTT-QX-Stop order's quantity must match the account's actual position quantity
at the time QX fires (after PTT-BE fills are terminal). No PTT-QX stop should be sized
larger than the position it is protecting.

### 4. Regression: Normal QX (No BE-ALL)

When QX-ALL is pressed WITHOUT a prior BE-ALL:
- CancelPttBeOrders returns 0 for all accounts.
- WaitForPttBeCancelled returns immediately (fast path, 0 expected count).
- No behavioral change from pre-B118 code path.
- All existing tests pass.

### 5. DW-B127 Structural Check

Rapid double-press of QX-ALL:
- Second press CancelPttBeOrders returns 0 for all accounts (PTT-BE-* already terminal).
- No additional PTT-QX orders submitted (existing _qxCancelInProgress guard fires).
- Output tab shows single set of PTT-QX-* Working orders, not doubled.

---

## Appendix: Method Signatures Summary

```csharp
// NEW -- cancel all PTT-BE-* orders in active states
// Returns: count of orders sent for cancel (0 if none)
// CYC=7, JS-021 PASS, JS-001 PASS, JS-033 PASS
internal static int CancelPttBeOrders(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Instrument instr)

// NEW -- synchronous poll until PTT-BE-* orders are terminal or timeout
// maxWaitMs recommended: 1000 (50 x 20ms iterations)
// No throw on timeout (fail-safe)
// CYC=7, JS-021 PASS, JS-001 PASS, JS-033 PASS
internal static void WaitForPttBeCancelled(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Instrument instr,
    int expectedCount,
    int maxWaitMs)

// PRIVATE HELPER -- CYC=1
private static bool IsPttBeOrder(string name)

// PRIVATE HELPER -- CYC=1
// Terminal states: Cancelled, Filled, Rejected, PartFilled, Unknown
// Per NT8_FULL_REFERENCE.md lines 976-997
private static bool IsNonTerminalPttBeState(NinjaTrader.Cbi.OrderState s)
```

---

## Appendix: Execute() Diff Sketch (insertion points)

```
// BEFORE line 47 in Execute():
+ int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument); // B118 DW-B126
+ WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000); // B118 DW-B126
  var targets = SnapshotTargetOrders(acc, pos.Instrument);  // UNCHANGED

// BEFORE follower SnapshotTargetOrders (after follower DIAG block ends, ~line 89):
+ int _fBeCancelCount = CancelPttBeOrders(follower, pos.Instrument); // B118 DW-B126
+ WaitForPttBeCancelled(follower, pos.Instrument, _fBeCancelCount, 1000); // B118 DW-B126
  var followerTargets = SnapshotTargetOrders(follower, pos.Instrument);  // UNCHANGED
```

Total additions to Execute(): 4 lines. No deletions. Execute() CYC unchanged at 8.
