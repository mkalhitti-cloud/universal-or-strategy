# B132 LaneB -- Architecture Plan: DW-B138 Stop Drag Runtime Silent (Diagnostic Phase)

**Epic**: B132 LaneB
**Defect**: DW-B138 P1 -- Stop Drag Runtime Silent
**Phase**: Phase 1 -- Diagnostic Print Plan (ptt-architect)
**Prior fix**: B131 LaneA -- `SignalOrNameMatches` (L2361) + `FindFollowerBracketOrder` leaderName param (L2375) + `SyncFollowerBracket` call site (L2139) -- ALREADY IN SOURCE. NOT to be removed.

---

## Section H -- Rules Catalog Gate Result (STEP 0)

```
STEP 0 -- RULES CATALOG GATE:
  [x] Read docs/standards/jane-street/RULES_CATALOG.md lines 1-30 (UTF-8 clean, readable)
  [x] JS-021 (lock ban): Print statements use NinjaTrader.Code.Output.Process -- no shared
      mutable state, no lock(). Helper methods TryLogDragTrace and TryLogSFBTrace are read-only
      observability. PASS.
  [x] JS-001 (no throw in hot path): NinjaTrader.Code.Output.Process does not throw under
      normal conditions. No new try/catch or throw added. PASS.
  [x] JS-002 (no return null): No new return-null sites. TryLogDragTrace and TryLogSFBTrace
      are void methods. PASS.
  [x] JS-033 (no async void): No async methods added. PASS.
  [x] CYC <= 8: All modified methods within budget. See Section G SCAN-06.
  GATE RESULT: PASS
```

---

## Section A -- Hypothesis Analysis (H1 through H5)

### Background

B131 LaneA compiled and passed VERIFY_PASS. The fix introduced:
1. `SignalOrNameMatches` predicate at L2361 (signal-first + name-fallback)
2. `leaderName` parameter in `FindFollowerBracketOrder` at L2375
3. Updated call site in `SyncFollowerBracket` at L2139

Despite this, zero `PTT-STP-Drag` events appear on the follower in 2 SIM sessions. The signal is
being dropped somewhere in the dispatch chain between `OnOrderUpdate` and `SyncFollowerBracket`.

### Chain Summary (Gate-by-Gate)

```
OnOrderUpdate
  |-- EvictDedup                           [pre-gate, unconditional]
  |-- TryFireFollowerBeDisarm              [pre-gate, unconditional]
  |-- TryFireFollowerBeRetry               [pre-gate, unconditional]
  |-- TryEvictFollowerBeSlot               [pre-gate, unconditional]
  |-- PTT-BE-Target fill block             [pre-gate, if-guard]
  |-- PTT-BE-Cancel block                  [pre-gate, if-guard]
  |-- TryCleanupReArmedAtmBracket          [pre-gate, unconditional]
  |-- IsPttEntryOrderCancelTrigger guard   [pre-gate, if-guard]
  Gate 1 (L1363): !_isCopyEnabled          <-- exits if copy disabled
  Gate 2 (L1368): FindMatchingRule         <-- exits if not LEADER account
  Gate 2.5 (L1371): matchedRule null/disabled <-- exits if no rule
  |-- TryFirePositionState
  |-- MirrorOrderUpdate (if Mirror mode)
  |-- TryCancelFollowerEntries             <-- exits if leader cancel
  |-- TryDispatchLeaderFlat               <-- exits if leader flat
  Gate B+C (L1402): TryHandleDrag
    |-- TryHandleBracketDrag (L1720)
          IsWorkingBracket(order) gate     <-- exits if not Working/Accepted bracket
          HandleBracketChange (L2336)
            IsStopLeg -> isStop
            foreach followerAccounts
              SyncFollowerBracket (L2131)
                FindFollowerBracketOrder   <-- SUSPECT: Working-only filter (L2386)
                if (fo == null) return     <-- SILENT EXIT if not found
```

### H1 -- FindFollowerBracketOrder `OrderState.Working`-only filter (MOST LIKELY)

**Evidence**:

`FindFollowerBracketOrder` at L2386:
```csharp
if (order.OrderState != OrderState.Working) // (1) branch
    continue;
```

`IsWorkingBracket` at L2083-2089 (leader-side gate):
```csharp
internal static bool IsWorkingBracket(Order order) =>
    (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)
    && IsBracketLegStatic(order);
```

**Asymmetry**: The leader bracket is accepted through `IsWorkingBracket` if it is `Working` OR
`Accepted`. But the follower bracket lookup in `FindFollowerBracketOrder` accepts ONLY `Working`.

**NT8 confirmed** (`NT8_FULL_REFERENCE.md` L3363-3365):
- `OrderState.Accepted` = "Order is accepted by the broker or exchange"
- `OrderState.Working` = "Order is working in the exchange queue"

**SIM behaviour**: PTT cancel+resubmit (SyncAtmFollowerBracket) places the follower ATM bracket via
`CreateOrder` + `Submit`. In NT8 Sim mode, newly submitted orders may enter `Accepted` state before
transitioning to `Working`. If the leader drag event fires while the follower order is in `Accepted`
(not yet `Working`), `FindFollowerBracketOrder` skips the follower order → returns null →
`SyncFollowerBracket` returns early → zero cancel+resubmit dispatched → zero `PTT-STP-Drag` output.

**TP that confirms**: TP4 (`[TP4-SFB]`) will show `fo=NULL` alongside follower orders whose
`OrderState` is `Accepted` (not `Working`), directly confirming the skip.

**Likelihood**: HIGHEST. This is a single-line root cause with direct evidence in the source.

---

### H2 -- Leader Stop1 fires in `ChangeSubmitted` state, IsWorkingBracket returns false

**Evidence**:
NT8 `ChangeSubmitted` = "Order change is submitted to the broker" (`NT8_FULL_REFERENCE.md` L3367).
When the user drags Stop1, NT8 fires the order event sequence: `ChangePending` -> `ChangeSubmitted`
-> `Working`. The `IsWorkingBracket` gate at L1722 returns false for `ChangeSubmitted`.

**Why NOT the primary cause**: A `Working` state event always follows the `ChangeSubmitted` event in
the drag sequence. The `Working` event will re-enter `TryHandleBracketDrag` and `IsWorkingBracket`
will return true. B131 plan Section A H3 confirms this analysis.

**TP that confirms**: TP1 (`[TP1-OOU]`) logs all `ChangeSubmitted` and `Working` bracket events.
If zero `[TP1-OOU]` entries appear for the leader Stop1 at all, there is a deeper gate failure
(Gate 1 or Gate 2) preventing the event from reaching the print. If both `ChangeSubmitted` AND
`Working` appear in TP1 but TP2 only fires for `Working`, that confirms `ChangeSubmitted` is benign.

**Likelihood**: LOW (expected Working event always follows).

---

### H3 -- IsStopLeg returns false for ATM Stop1 (isStop = false)

**Evidence**:

`IsStopLeg` at L3836-3844 (confirmed in source):
```csharp
private static bool IsStopLeg(Order order)
{
    return order.FromEntrySignal != null
        || (order.Name != null && order.Name.StartsWith("Stop"))
        || (order.Name != null && order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase));
}
```

For ATM brackets named `"Stop1"`, `"Stop2"`, `"Stop3"`: `name.StartsWith("Stop")` is true.
For ATM brackets named `"Buy STP"`, `"Sell STP"`: `name.EndsWith("STP")` is true.

**ELIMINATED**: `IsStopLeg` correctly returns true for all known ATM stop bracket naming patterns.
`isStop = false` only if the order has no `FromEntrySignal`, does NOT start with "Stop", and does NOT
end with "STP". This is not the case for standard ATM brackets.

**TP that confirms**: TP3 (`[TP3-HBC]`) logs `isStop=True/False`. `isStop=False` for a "Stop1" order
would indicate a new naming pattern not covered by `IsStopLeg` (would require separate investigation).

**Likelihood**: VERY LOW (eliminated by source analysis).

---

### H4 -- `rule.FollowerAccounts` is empty or all null at drag time

**Evidence**:

`HandleBracketChange` at L2349-2354:
```csharp
foreach (var acc in rule.FollowerAccounts) // (5)
{
    if (acc == null) // (6)
        continue;
    SyncFollowerBracket(acc, leaderOrder, isStop, newPrice, tickSize);
}
```

If `rule.FollowerAccounts` is empty (count=0), the foreach body executes 0 times → zero
`SyncFollowerBracket` calls → no drag sync dispatched.

**Why UNLIKELY**: If `FollowerAccounts` were consistently empty, NOTHING would be copied at all --
entries, targets, stops. The user reports entry copies are working correctly; the drag sync is the
only silent path. An empty `FollowerAccounts` list would be a blanket failure, not a drag-specific one.

**TP that confirms**: TP3 (`[TP3-HBC]`) logs `followerCount={rule.FollowerAccounts.Count}`.
`followerCount=0` would confirm this as the cause. Expected: `followerCount >= 1`.

**Likelihood**: LOW (configuration race; inconsistent with partial copy success).

---

### H5 -- leaderOrder.FromEntrySignal null AND leaderOrder.Name null simultaneously

**Evidence**:

`SignalOrNameMatches` at L2361-2368:
```csharp
internal static bool SignalOrNameMatches(Order order, string? signalName, string? leaderName)
{
    if (order.FromEntrySignal == signalName) return true;
    if (leaderName == null) return false;
    return order.Name == leaderName;
}
```

If both `leaderOrder.FromEntrySignal == null` AND the follower `order.FromEntrySignal == null`,
then `null == null` evaluates to true in C# reference equality -> `SignalOrNameMatches` returns
true immediately. This would still match orders (too broadly, not too narrowly). H5 as a cause of
zero matches is self-cancelling: if both signals are null, the predicate returns true for any
follower order also having null `FromEntrySignal`, which is most ATM bracket orders.

**ELIMINATED**: H5 could cause false positives but not false negatives.

**TP that confirms**: TP4 logs `leaderName` and follower order names. If `leaderName=null` and
follower orders show `FromEntrySignal=null` but still return `fo=NULL`, another cause is active.

**Likelihood**: VERY LOW (logic inversion; does not cause no-match).

---

### Hypothesis Summary

| Hypothesis | Description | Likelihood | Confirming TP |
|------------|-------------|------------|---------------|
| H1 | `FindFollowerBracketOrder` L2386 `Working`-only, follower may be `Accepted` | **HIGHEST** | TP4 |
| H2 | Leader `ChangeSubmitted` blocks `IsWorkingBracket` (Working event follows) | LOW | TP1, TP2 |
| H3 | `IsStopLeg` false for ATM Stop1 (eliminated by source) | VERY LOW | TP3 |
| H4 | `rule.FollowerAccounts` empty at drag time | LOW | TP3 |
| H5 | Both signals null (logic inversion, no-match impossible) | VERY LOW | TP4 |

**Expected diagnostic outcome**: TP4 will show `fo=NULL` with follower orders in `Accepted` state,
confirming H1. The engineer fix (Phase 4/5, NOT this phase) is to expand L2386 to also accept
`OrderState.Accepted`. This plan does NOT implement that fix.

---

## Section B -- 4 Trace Points

### TP1 -- TryLogDragTrace helper (called from OnOrderUpdate)

**Reason for extraction**: `OnOrderUpdate` current CYC is approximately 11-18 (simple McCabe 11,
lizard ~18 due to boolean operators in compound conditions). Adding an inline `if (_diagnosticMode)`
guard would push it further above the CYC=8 budget. Extracting to `TryLogDragTrace` adds 0 branches
to `OnOrderUpdate` (unconditional method call) while keeping the helper at CYC=4.

**Call site in OnOrderUpdate**:
- **Insert after**: L1299 (`EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState);`)
- **Insert before**: L1301 (comment: `// HOTFIX-FLAT-DISARM-FOLLOWER:`)
- **New line**: `TryLogDragTrace(e.Order);`
- **CYC impact on OnOrderUpdate**: +0 (unconditional call, no branch)

**New helper method** (declare near `TryHandleBracketDrag`, approximately after L1740):
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

**CYC**: before=N/A (new method), after=4. Within budget.
**What it reveals**: Whether the leader Stop1 drag event is reaching pre-gate phase at all.
If zero `[TP1-OOU]` lines appear, Gate 1 (`!_isCopyEnabled`) or a pre-gate path is eating the event.
If `[TP1-OOU]` appears with `state=ChangeSubmitted` but not `state=Working`, the Working event is
being blocked before OnOrderUpdate (unlikely for NT8 account event). If `[TP1-OOU]` appears for
`state=Working`, the event is reaching the pre-gate phase and the drop is downstream.

---

### TP2 -- TryHandleBracketDrag (inline guard)

**Method**: `TryHandleBracketDrag` (L1720-L1728)
**Insert location**: After L1721 (`{`), before L1722 (`if (!IsWorkingBracket(order))`)

```csharp
if (_diagnosticMode)
    NinjaTrader.Code.Output.Process(
        "[TP2-DRAG] IsWorkingBracket=" + IsWorkingBracket(order)
        + " name=" + (order.Name ?? "null")
        + " state=" + order.OrderState,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
```

**CYC**: `TryHandleBracketDrag` before=3 (if L1722 + if L1724 + 1 base), after=4. Within budget.
**What it reveals**: Whether `TryHandleBracketDrag` is reached at all, and whether
`IsWorkingBracket` returns true or false for the leader order at the time of the drag event.
If TP2 does not appear but TP1 does, the event exits between Gate 2 / Gate 2.5 / mirror / cancel /
flat dispatch -- ruling out H1 and pointing to a gate failure upstream of `TryHandleDrag`.
If TP2 shows `IsWorkingBracket=False`, H2 is active (leader order in ChangeSubmitted at dispatch).

---

### TP3 -- HandleBracketChange (inline guard)

**Method**: `HandleBracketChange` (L2336-L2355)
**Insert location**: After L2347 (`double newPrice = tickSize > 0 ? ... : rawPrice;`), before L2349 (`foreach`)

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

**CYC**: `HandleBracketChange` before=6 (instrument null + rawPrice ternary + newPrice ternary +
foreach + acc null + 1 base), after=7. Within budget.
**What it reveals**:
- `isStop=False` for "Stop1" would confirm H3 (new IsStopLeg naming gap).
- `followerCount=0` confirms H4 (empty FollowerAccounts).
- `followerCount >= 1` and `isStop=True` with expected `newPrice` rules out H3 and H4,
  focusing investigation entirely on `FindFollowerBracketOrder` (H1).

---

### TP4 -- SyncFollowerBracket (extracted to TryLogSFBTrace helper)

**Reason for extraction**: `SyncFollowerBracket` (L2131-L2187) current CYC = 8 (simple McCabe:
fo null + delta guard + isStop+AtmSTP + !isStop+AtmSTP + isStop+Trailing + isStop in try +
catch + 1 base = 8). Adding an inline `if (_diagnosticMode)` guard would push CYC to 9 --
OVER the CYC=8 budget. Extracting to `TryLogSFBTrace` adds 0 branches to `SyncFollowerBracket`.

**Call site in SyncFollowerBracket**:
- **Insert after**: L2139 (`var fo = FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop, leaderOrder.Name);`)
- **Insert before**: L2140 (`if (fo == null) // (1)`)
- **New line**: `TryLogSFBTrace(acc, leaderOrder, isStop, fo);`
- **CYC impact on SyncFollowerBracket**: +0 (unconditional call, no branch)

**New helper method** (declare near other trace helpers, after `TryLogDragTrace`):
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

**CYC**: before=N/A (new method), after=2. Well within budget.
**What it reveals**: This is the CRITICAL trace point for H1.
- `fo=NULL` with follower orders showing `Accepted` state confirms H1 definitively.
- `fo=NULL` with follower orders showing `Working` state would indicate a name-match failure
  (H5 variant) requiring further investigation into `SignalOrNameMatches` predicate.
- `fo=<order_name>` confirms the follower order IS found; a deeper bug in `SyncAtmFollowerBracket`
  or `SyncAtmFollowerTarget` would then be investigated in a subsequent block.

---

### Trace Point Summary

| TP | Method | Insert After Line | Guard Type | CYC Before | CYC After | Confirms |
|----|--------|-------------------|------------|------------|-----------|---------|
| TP1 | `TryLogDragTrace` (new helper) | L1299 (call site) | Extracted | +0 to OnOrderUpdate | 4 (new) | Gate 1/2 pass-through |
| TP2 | `TryHandleBracketDrag` | L1721 | Inline | 3 | 4 | H2 (`IsWorkingBracket`) |
| TP3 | `HandleBracketChange` | L2347 | Inline | 6 | 7 | H3 (isStop), H4 (followerCount) |
| TP4 | `TryLogSFBTrace` (new helper) | L2139 (call site) | Extracted | +0 to SyncFollowerBracket | 2 (new) | H1 (fo=NULL + Accepted state) |

---

## Section C -- `_diagnosticMode` Pattern

### Field Declaration

Declare in the field declarations block of `CopyEngine`, after L400 (after `GlobalBeAllDisarmed`
event declaration). This is near the end of the field/event cluster, before the constructor:

```csharp
// B132 LaneB diagnostic gate -- set to false to disable all TP1-TP4 Print calls.
// Remove this field and all TryLogDragTrace / TryLogSFBTrace calls when DW-B138 is confirmed fixed.
// JS-021: static bool read is lock-free (no torn reads on bool). Not volatile (diagnostic only).
private static bool _diagnosticMode = true;
```

### Thread Safety

`_diagnosticMode` is a read-only boolean at runtime (never mutated after class initialization).
No torn reads possible on `bool` in .NET. No `volatile` required for a diagnostic flag.
`NinjaTrader.Code.Output.Process` is NT8's output sink, safe to call from any thread including
the order-update background thread. No Dispatcher.InvokeAsync needed for Output.Process calls.

### Clean Removal Protocol (post-fix)

1. Set `_diagnosticMode = false` — confirms all Print calls are dead-code gated and quiesces output.
2. Verify zero `[TP1-OOU]`, `[TP2-DRAG]`, `[TP3-HBC]`, `[TP4-SFB]` output in one SIM session.
3. Delete: the `_diagnosticMode` field declaration, the `TryLogDragTrace` method, the
   `TryLogSFBTrace` method, the inline `if (_diagnosticMode)` guards in `TryHandleBracketDrag` and
   `HandleBracketChange`, and the `TryLogDragTrace(e.Order)` call in `OnOrderUpdate`.
4. Run SCAN-06 (CYC check) to confirm all methods return to their pre-diagnostic CYC values.

---

## Section D -- Non-Regression Scope

### B131 LaneA Fixes -- UNCHANGED

The following B131 LaneA source changes are ALREADY IN SOURCE and must NOT be removed or modified:

| Symbol | Line | Change |
|--------|------|--------|
| `SignalOrNameMatches` | L2361 | Predicate: signal-first, leaderName fallback |
| `FindFollowerBracketOrder` signature | L2375 | Added `string? leaderName = null` param |
| `SyncFollowerBracket` call site | L2139 | Passes `leaderOrder.Name` as leaderName arg |

### Behavioral Change

This diagnostic plan introduces ZERO behavioral changes. Print statements are observability-only:
- No order dispatch paths are modified.
- No gate conditions are changed.
- `NinjaTrader.Code.Output.Process` writes to OutputTab1 only.
- No state is mutated by the helper methods.

### B131 Regression Tests

All 4 B131 tests must compile and pass without modification:

| Test | File | What It Asserts |
|------|------|-----------------|
| `T_B25_03_IsStopLeg_AtmSTPSuffix_ReturnsTrue` | `CopyEngineTests.cs` L2619 | IsStopLeg returns true for "STP" suffix |
| `SignalOrNameMatchesTestable` tests | `CopyEngineTests.cs` | signal-first / name-fallback predicate |
| `FindFollowerBracketOrderTestable` tests | `CopyEngineTests.cs` | leaderName param match |
| `OnOrderUpdate_WithWorkingBracket_DoesNotDispatchCopy` | `CopyEngineTests.cs` L458 | OOU exists as non-public instance method |

---

## Section E -- Test Specification

### Regression Tests (must pass, no modifications needed)

All 4 B131 tests listed in Section D. Diagnostic helpers (`TryLogDragTrace`, `TryLogSFBTrace`)
are private void methods with no testable return value. They are observability-only; unit tests
would only verify Print output which is not the xUnit pattern in this project.

### New Smoke Test

**Test class**: `CopyEngineTests.cs` (existing file, add to end of class)
**Test name**: `B132_LaneB_DiagnosticMode_FieldExists`

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

**What it asserts**:
1. `_diagnosticMode` is accessible as `private static bool`.
2. Default value is `true` (diagnostic Print calls are active).

**xUnit compliance**: Uses `[Fact]`, `Assert.NotNull`, `Assert.Equal`. No NUnit/MSTest.

---

## Section F -- DW Items

**No DW items.**

All NT8 API facts required for this plan were resolved from:
- `docs/standards/NT8_FULL_REFERENCE.md` L3363-3367: `OrderState.Accepted`, `OrderState.Working`,
  `OrderState.ChangeSubmitted` confirmed with exact descriptions.
- `docs/standards/NT8_FULL_REFERENCE.md` L3363+: `NinjaTrader.Code.Output.Process(string, PrintTo)`
  is standard NT8 output API, safe to call from any thread.

`IsStopLeg` at L3836-3844 is a private static method in `CopyEngine` (not an NT8 API). Confirmed
in source; no NT8 API lookup needed.

No ambiguities remain after reading both NT8 reference documents.

---

## Section G -- Lamport/Scan Checklist

```
SCAN-01 LOCK SCAN
  Command:  grep -r "lock(" src/ --include="*.cs"
  Requirement: ZERO MATCHES
  Basis: JS-021. Print statements have no shared mutable state. No lock() added.

SCAN-02 THROW SCAN
  Command:  grep -n "throw new" src/PropTraderTools/CopyEngine.cs
  Requirement: ZERO NEW THROWS (existing only, count must not increase)
  Basis: JS-001. TryLogDragTrace and TryLogSFBTrace contain no throw statements.

SCAN-03 NULL RETURN SCAN
  Command:  grep -n "return null" src/PropTraderTools/CopyEngine.cs
  Requirement: EXISTING ONLY -- count must not increase
  Basis: JS-002. Both new helper methods are void; no return-null sites added.

SCAN-04 ASYNC VOID SCAN
  Command:  grep -rn "async void " src/ --include="*.cs"
  Requirement: ZERO MATCHES
  Basis: JS-033. No async methods added.

SCAN-05 DATETIME NOW SCAN
  Command:  grep -rn "DateTime\.Now" src/ --include="*.cs"
  Requirement: ZERO MATCHES
  Basis: Project mandate. No DateTime.Now usage in diagnostic helpers.

SCAN-06 CYC BUDGET
  All modified/added methods must be <= CYC 8:

  | Method                    | CYC Before | CYC After | Status      |
  |---------------------------|------------|-----------|-------------|
  | OnOrderUpdate             | ~11-18     | ~11-18    | UNCHANGED   |
  | TryLogDragTrace (NEW)     | N/A        | 4         | OK (<= 8)   |
  | TryHandleBracketDrag      | 3          | 4         | OK (<= 8)   |
  | HandleBracketChange       | 6          | 7         | OK (<= 8)   |
  | SyncFollowerBracket       | 8          | 8         | UNCHANGED   |
  | TryLogSFBTrace (NEW)      | N/A        | 2         | OK (<= 8)   |

  Note: OnOrderUpdate is pre-existing at CYC > 8 (not introduced by this block).
  No net CYC increase on OnOrderUpdate or SyncFollowerBracket in this block.
  PASS.

SCAN-07 ASCII SCAN
  Command:  grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
  Requirement: ZERO NON-ASCII
  Basis: Project mandate. Print string literals use ASCII-only characters.
  All TP strings use: letters, digits, spaces, =, [, ], ,, :, ., ?, !, -.
  No Unicode, emoji, or curly quotes.
```

---

**Status**: REVIEW_PENDING
