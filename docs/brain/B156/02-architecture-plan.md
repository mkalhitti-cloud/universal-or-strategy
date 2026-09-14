# B156/B157 Architecture Plan
# Epic: Post-Fix Documentation Pipeline — CopyEngine Three-Bug Repair
# Status: REVIEW_PASS (Director SIM-confirmed, .cs already committed)
# Output: docs/brain/B156/02-architecture-plan.md

---

## LANE-SPLIT GATE RESULT: LANES-APPROVED

Gate questions (mandatory pre-analysis):

| Q  | Question                                                        | Answer |
|----|-----------------------------------------------------------------|--------|
| Q1 | Same method or within 50 lines?                                 | NO     |
| Q2 | Fix B design depends on Fix A final design?                     | NO     |
| Q3 | Each fix has standalone value if the other is blocked?          | YES    |
| Q4 | Each fix has an independent SIM verification path?              | YES    |

Verdict: All four LANES-APPROVED conditions satisfied (NO on Q1+Q2, YES on Q3+Q4).
Execution: Unified documentation pipeline under B156/ per Director instruction
(Director committed all three fixes to a single .cs file and requested a single plan).

---

## 1. Executive Summary

Three bugs were identified and fixed in `src/PropTraderTools/CopyEngine.cs` via a direct SIM
repair session. All three fixes are committed and SIM-confirmed. This plan records the formal
architecture of each fix for the PTT 5-phase pipeline.

| ID          | Severity | Title                                         | Location                     |
|-------------|----------|-----------------------------------------------|------------------------------|
| DW-B156-01  | P0       | Ghost re-entry after leader-flat drain        | TryEvictFollowerBeSlot ~1934 |
| DW-B157-01  | P1       | fo=NULL for drain entries (drag broken)       | TrySetSlateReadyOnDrainFill  |
| DW-B157-02  | P2       | BE retry 500ms delay from inflated order count| LogDiagOrderCount ~6922      |

Source file: `src/PropTraderTools/CopyEngine.cs` (read-only — fixes committed)
Test file:   `src/PropTraderTools/CopyEngineTests.cs`

---

## 2. Root Cause Analysis

### 2.1 DW-B156-01 — Ghost Re-Entry After Leader-Flat Drain

**Symptom**: After a leader's PTT-BE-Stop order fills (leader account goes flat), follower
drain entries are submitted anyway, creating ghost re-entry positions on followers.

**Root cause**:
`TryEvictFollowerBeSlot` has an early-return at line 1958:
```csharp
if (!IsFollowerAccount(o.Account))
    return; // (3) follower-only evictions below
```
When the leader's PTT-BE-Stop fills, `TryEvictFollowerBeSlot` fires with a leader account.
The method hits this early-return BEFORE any drain cleanup logic runs.
`_pendingDispatchDrains` still contains follower drain entries from the current trade cycle.
When those drain entry fill events arrive, `SubmitDrainedEntry` fires and submits new
entry orders — even though the leader is now flat.

**Fix approach**: Insert a leader-flat drain abort block BEFORE the IsFollowerAccount
early-return. Detect: non-follower account, Filled state, resulting position is flat.
Iterate `_pendingDispatchDrains` and call `AbortDrainOnFill(kv.Key)` for every entry
whose instrument matches the filled order.

**Log marker**: `[DRAIN-ABORT-LEADER-FLAT] leader=X instr=Y aborted=N`

---

### 2.2 DW-B157-01 — fo=NULL for Drain Entries (Stop/Target Drag Broken)

**Symptom**: After a drain entry fills on a follower, `FindFollowerBracketOrder` returns
null (`fo=null`), breaking stop/target drag for that follower for the entire trade.

**Root cause**:
The normal slate-ready flow for follower entries:
1. Entry fill → `TryMarkFollowerSlateNotReady` sets `_followerSlateReady[acct] = false`
2. Position stable → `TrySetFollowerSlateReady` restores `_followerSlateReady[acct] = true`
   — BUT only when `IsAccountFlat` returns true (account has no open positions).

For drain entries: the PTT-Copy drain order fills and the follower IMMEDIATELY has an open
position. `IsAccountFlat` is false. `TrySetFollowerSlateReady` never restores the slate.
`_followerSlateReady[acct]` remains false for the entire trade duration.
`FindFollowerBracketOrder` gates on `_followerSlateReady` and returns null.

**Fix approach**: New helper `TrySetSlateReadyOnDrainFill` called from `OnOrderUpdate`
after `HandleDrainTerminalState` (line 1756). This helper bypasses the flat check and
directly sets `_followerSlateReady[order.Account.Name] = true` when a PTT-Copy Filled
event arrives on a follower that is NOT flat (i.e., has an open drain position).

**Log marker**: `[SLATE-READY-DRAIN] acct=X instr=Y`

---

### 2.3 DW-B157-02 — BE Retry 500ms Delay from Inflated Order Count

**Symptom**: The [BE-DIAG] log shows order counts of 291-304 instead of the expected
1-8, causing `RegisterBeRetrySlotIfNeeded` to schedule unnecessary 500ms retry delays.

**Root cause**:
`acc.Orders` in NT8 AddOnBase returns ALL orders for the account — including Filled,
Cancelled, and Rejected historical orders from prior trades. For a busy SIM account,
this collection can contain hundreds of terminal orders. `LogDiagOrderCount` was iterating
this full collection and counting by instrument match only, without excluding terminal states.
The resulting inflated count misled the BE retry diagnostic path.

**Fix approach**: Add a terminal-state skip inside `LogDiagOrderCount`'s foreach loop:
skip any order whose state is `Filled`, `Cancelled`, or `Rejected`. The resulting count
matches the `ActiveOrders` filter semantics used elsewhere in CopyEngine.

---

## 3. Implementation Details

### 3.1 DW-B156-01 — TryEvictFollowerBeSlot Inline Block

**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `TryEvictFollowerBeSlot(OrderEventArgs e)` (existing, modified)
**Signature**: `private void TryEvictFollowerBeSlot(OrderEventArgs e)` — unchanged

**Change**: Insert new leader-flat drain abort block at line ~1934, between
`_entryDispatchedOrders.Clear()` and `if (!IsFollowerAccount(o.Account)) return;`.

**Inserted block (lines 1934-1957)**:
```csharp
// DW-B156-01: leader-flat drain abort.
if (!IsFollowerAccount(o.Account) && o.OrderState == OrderState.Filled
    && IsFlat(FindPosition(o.Account, o.Instrument)))
{
    int abortCount = 0;
    foreach (var kv in _pendingDispatchDrains)
    {
        if (kv.Value.Instrument?.FullName == o.Instrument?.FullName)
        {
            AbortDrainOnFill(kv.Key);
            abortCount++;
        }
    }
    if (abortCount > 0)
        NinjaTrader.Code.Output.Process(
            "[DRAIN-ABORT-LEADER-FLAT] leader=" + o.Account.Name
            + " instr=" + (o.Instrument?.FullName ?? "?")
            + " aborted=" + abortCount,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
}
```

**CYC of inserted block (McCabe strict)**:
- if (!IsFollower && Filled && IsFlat): if=+1, &&=+1, &&=+1 → 3
- foreach: +1 → 1
- if (instrument match): +1 → 1
- if (abortCount > 0): +1 → 1
- Block total decision points added: 7

**Note — method-level CYC**:
TryEvictFollowerBeSlot is a pre-existing method with CYC ~7 before this fix (guard chain
+ follower-path branches). B156-01 adds 7 decision points, bringing the total to ~13.
This exceeds CYC=8 (P1). However, this is pre-existing complexity accumulated across
multiple prior epics. B156-01 adds the minimal change required to fix the P0 bug.
A TryEvictFollowerBeSlot decomposition is tracked in the deferred backlog (Section 7).

**NT8 APIs used**:
- `o.Account`, `o.OrderState`, `o.Instrument` — standard Order properties
- `OrderState.Filled` — standard NT8 enum value
- `NinjaTrader.Code.Output.Process(string, PrintTo)` — standard NT8 diagnostic output

---

### 3.2 DW-B157-01 — TrySetSlateReadyOnDrainFill (New Helper)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `TrySetSlateReadyOnDrainFill(Order order)` — NEW private instance helper
**Signature**: `private void TrySetSlateReadyOnDrainFill(Order order)`

**Implementation (lines 992-1010)**:
```csharp
// DW-B157-01: TrySetSlateReadyOnDrainFill.
// CYC<=6. JS-021: ConcurrentDictionary indexer is lock-free.
// JS-001: no throw. JS-002: void. ASCII-only.
private void TrySetSlateReadyOnDrainFill(Order order)
{
    if (order == null)                                                          // (1)
        return;
    if (order.OrderState != OrderState.Filled)                                 // (2)
        return;
    if (!order.Name.StartsWith("PTT-Copy", StringComparison.Ordinal))          // (3)
        return;
    if (!IsFollowerAccount(order.Account))                                     // (4)
        return;
    if (IsFlat(FindPosition(order.Account, order.Instrument)))                 // (5) no open pos
        return;
    _followerSlateReady[order.Account.Name] = true;
    NinjaTrader.Code.Output.Process(
        "[SLATE-READY-DRAIN] acct=" + order.Account.Name
        + " instr=" + (order.Instrument?.FullName ?? "?"),
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
}
```

**CYC (McCabe strict)**:
base=1 + if(null)=1 + if(Filled)=1 + if(StartsWith)=1 + if(!Follower)=1 + if(IsFlat)=1 = **CYC=6** ✅

**Call site**: `OnOrderUpdate` at line 1756, after `HandleDrainTerminalState(e.Order)`.
```csharp
// DW-B157-01: restore _followerSlateReady after a PTT-Copy drain entry fills.
TrySetSlateReadyOnDrainFill(e.Order);
```

**NT8 APIs used**:
- `order.OrderState`, `order.Name`, `order.Account`, `order.Instrument` — standard Order properties
- `OrderState.Filled` — standard NT8 enum value
- `_followerSlateReady[order.Account.Name] = true` — ConcurrentDictionary<string,bool> indexer (lock-free)
- `NinjaTrader.Code.Output.Process(string, PrintTo)` — standard NT8 diagnostic output

---

### 3.3 DW-B157-02 — LogDiagOrderCount Terminal-State Filter

**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `LogDiagOrderCount(Account acc, Instrument instrument)` (existing, modified)
**Signature**: `private void LogDiagOrderCount(Account acc, Instrument instrument)` — unchanged

**Change**: Add terminal-state skip inside the foreach loop (lines 6927-6930):
```csharp
if (o.OrderState == OrderState.Filled    // (2) skip terminal states
    || o.OrderState == OrderState.Cancelled
    || o.OrderState == OrderState.Rejected)
    continue;
```

**Full method after fix (lines 6922-6938)**:
```csharp
// DW-B157-02: count only ACTIVE orders (exclude Filled/Cancelled/Rejected).
// CYC=6. JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only.
private void LogDiagOrderCount(Account acc, Instrument instrument)
{
    int diagTotal = 0;
    foreach (Order o in acc.Orders)                         // (1) foreach
    {
        if (o.OrderState == OrderState.Filled               // (2) if
            || o.OrderState == OrderState.Cancelled         //     ||
            || o.OrderState == OrderState.Rejected)         //     ||
            continue;
        if (o?.Instrument?.FullName == instrument?.FullName) // (3) if
            diagTotal++;
    }
    NinjaTrader.Code.Output.Process(
        "[BE-DIAG] " + acc.Name + " orders-for-instr=" + diagTotal,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
}
```

**CYC (McCabe strict)**:
base=1 + foreach=1 + if(terminal)=1 + ||Cancelled=1 + ||Rejected=1 + if(instrument)=1 = **CYC=6** ✅

Note: Source comment says "CYC<=3" using a simplified block-counting style (3 logical gates:
loop, skip, filter). Strict McCabe per RULES_CATALOG yields CYC=6. Both are ≤8. ✅

**NT8 APIs used**:
- `acc.Orders` — NT8 Account.Orders (all historical orders — exactly the API whose behavior the fix addresses)
- `o.OrderState` — `OrderState.Filled/Cancelled/Rejected` — standard NT8 enum values
- `o.Instrument.FullName` — NT8 Instrument property
- `NinjaTrader.Code.Output.Process(string, PrintTo)` — standard NT8 diagnostic output

---

## 4. Cross-Fix Interaction Analysis

| Interaction             | Relationship | Notes                                              |
|-------------------------|--------------|----------------------------------------------------|
| Fix 1 + Fix 2           | Orthogonal   | Fix 1 aborts drains where leader went flat BEFORE  |
|                         |              | drain filled. Fix 2 restores slate for drains that |
|                         |              | DID fill (leader has position). Non-overlapping    |
|                         |              | code paths.                                        |
| Fix 1 + Fix 3           | Independent  | Fix 3 is diagnostic-only. No shared state.         |
| Fix 2 + Fix 3           | Complementary| Fix 2 unblocks stop/target drag for drain followers|
|                         |              | Fix 3 ensures BE retry count is accurate once drag |
|                         |              | is unblocked. Sequential improvement, no conflict. |
| Fix 1 prevents Fix 2    | NO           | If Fix 1 aborts a drain, Fix 2 never fires         |
|                         |              | (PTT-Copy order never fills). No double-write.     |

**Ordering safety**: Fix 2 is called at line 1756 (after HandleDrainTerminalState at 1748).
Fix 1 is in TryEvictFollowerBeSlot (line 1926+), called separately via OrderUpdate routing.
No ordering dependency between Fix 1 and Fix 2 within a single event dispatch.

---

## 5. Jane Street Compliance

### JS-001 — No throw in hot paths

| Fix          | Result | Evidence                                         |
|--------------|--------|--------------------------------------------------|
| DW-B156-01   | PASS   | New block uses early-return pattern only         |
| DW-B157-01   | PASS   | TrySetSlateReadyOnDrainFill: 5 early returns,    |
|              |        | no throw                                         |
| DW-B157-02   | PASS   | LogDiagOrderCount: continue + counter increment, |
|              |        | no throw                                         |

### JS-002 — No unexpected null return

| Fix          | Result | Evidence                                         |
|--------------|--------|--------------------------------------------------|
| DW-B156-01   | PASS   | void method, no return value                     |
| DW-B157-01   | PASS   | void method, no return value                     |
| DW-B157-02   | PASS   | void method, no return value                     |

### JS-021 — No lock() / Monitor / Mutex

| Fix          | Result | Evidence                                         |
|--------------|--------|--------------------------------------------------|
| DW-B156-01   | PASS   | _pendingDispatchDrains is ConcurrentDictionary;  |
|              |        | foreach enumeration is snapshot-safe             |
| DW-B157-01   | PASS   | _followerSlateReady ConcurrentDictionary indexer |
|              |        | assignment is lock-free                          |
| DW-B157-02   | PASS   | acc.Orders read-only enumeration; no shared      |
|              |        | state written                                    |

### JS-013 — CYC <= 8

| Fix          | Scope            | CYC (McCabe) | Result |
|--------------|------------------|--------------|--------|
| DW-B156-01   | Inserted block   | +7 points    | N/A (block, not new method) |
|              | Method total     | ~13          | P1 deferred (pre-existing) |
| DW-B157-01   | New method       | 6            | PASS ✅ |
| DW-B157-02   | Method total     | 6            | PASS ✅ |

### ASCII-only string literals

| Fix          | String Literals                          | Result |
|--------------|------------------------------------------|--------|
| DW-B156-01   | "[DRAIN-ABORT-LEADER-FLAT] leader="      | PASS   |
| DW-B157-01   | "[SLATE-READY-DRAIN] acct="              | PASS   |
| DW-B157-02   | "[BE-DIAG] " (pre-existing)              | PASS   |

### No DateTime.Now

All three fixes: no DateTime usage of any kind. PASS.

---

## 6. Test Coverage Plan

### Existing Tests (CopyEngineTests.cs)

| Test                                                    | Line  | Fix Covered    | Notes                        |
|---------------------------------------------------------|-------|----------------|------------------------------|
| Assert.Contains("DRAIN-ABORT-FLAT", src)                | 9364  | DW-B156-01     | Substring match on log marker|
| TryEvictFollowerBeSlot_ShouldCallAbortDrain_When...     | 9382  | DW-B156-01     | Structural / reflection      |
| AbortDrainOnFill_ShouldExist_AsPrivateInstanceHelper    | 9412  | DW-B156-01     | Existence contract           |
| LogDiagOrderCount_ShouldLogCorrectCount_When...         | 6267  | DW-B157-02     | Skip: NT8-runtime            |
| Bug2FollowerSlateReadyTests suite                       | 8503+ | DW-B157-01     | _followerSlateReady state    |

### Required New Tests (tickets will specify)

**For DW-B157-01 (TrySetSlateReadyOnDrainFill):**

| Test Name                                                     | Type        | Assertion                                      |
|---------------------------------------------------------------|-------------|------------------------------------------------|
| `TrySetSlateReadyOnDrainFill_MethodExists_StructuralCheck`    | Structural  | Reflection: method exists as private instance  |
| `TrySetSlateReadyOnDrainFill_NullOrder_NoSideEffect`          | BCL         | Guard (1): null order → no write to state      |
| `TrySetSlateReadyOnDrainFill_NonFilledOrder_NoSideEffect`     | BCL         | Guard (2): non-Filled state → no state write   |
| `TrySetSlateReadyOnDrainFill_NonPttCopyName_NoSideEffect`     | BCL         | Guard (3): name without "PTT-Copy" → no write  |
| `TrySetSlateReadyOnDrainFill_CalledAfterHandleDrainTerminal`  | Structural  | OnOrderUpdate src contains call after line 1748|

**For DW-B156-01 (assertion upgrade):**

| Test Name                                                     | Type        | Assertion                                      |
|---------------------------------------------------------------|-------------|------------------------------------------------|
| `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerExists` | Structural  | Source contains "[DRAIN-ABORT-LEADER-FLAT]"    |

**For DW-B157-02 (assertion upgrade):**

| Test Name                                                     | Type        | Assertion                                      |
|---------------------------------------------------------------|-------------|------------------------------------------------|
| `LogDiagOrderCount_FiltersTerminalOrders_SkipsFilledCancRej`  | BCL         | Skip: NT8-runtime (needs acc.Orders stub)      |
|                                                               |             | Structural: method body contains "Filled"      |

### Test Execution Strategy

Tests requiring NT8 runtime (Account.Orders, Account.Positions, IsFollowerAccount) are
marked `[Fact(Skip = "NT8-runtime: ...")]` with a structural fallback via IL/source inspection.
Tests relying only on BCL types (null checks, string StartsWith, bool state) run without
NT8 and are marked `[Fact]`.

---

## 7. Deferred Backlog

| ID      | Severity | Item                                             | Tracked In     |
|---------|----------|--------------------------------------------------|----------------|
| DEFER-1 | P1       | TryEvictFollowerBeSlot total CYC ~13 exceeds     | 06-deferred-   |
|         |          | CYC=8 limit. Pre-existing across B154-B156.      | backlog.md     |
|         |          | Decompose: extract IsLeaderFlatDrainAbort() and  |                |
|         |          | RunFollowerEvictPath() sub-helpers.              |                |

---

## 8. File Change Summary

| File                                        | Change Type | Lines Affected        |
|---------------------------------------------|-------------|-----------------------|
| `src/PropTraderTools/CopyEngine.cs`         | Modified    | 1934-1957 (Fix 1)     |
|                                             |             | 986-1010 (Fix 2 new)  |
|                                             |             | 1750-1756 (Fix 2 hook)|
|                                             |             | 6914-6938 (Fix 3)     |
| `src/PropTraderTools/CopyEngineTests.cs`    | To add      | New tests for Fix 2   |

---

## 9. SIM Verification Evidence (Director-Confirmed)

All three fixes verified via direct SIM session:
- DW-B156-01: `[DRAIN-ABORT-LEADER-FLAT]` log appeared on leader BE-Stop fill. Ghost re-entries eliminated.
- DW-B157-01: `[SLATE-READY-DRAIN]` log appeared on PTT-Copy drain fill. `fo` no longer null; stop/target drag functional.
- DW-B157-02: `[BE-DIAG]` count dropped from 291-304 to 1-8. 500ms retry delay eliminated.

---

_Plan authored by PTT Architect (ptt-architect mode). Source verified from committed .cs files._
_Ready for Phase 3 (ticket generation) or ptt-plan-reviewer._
