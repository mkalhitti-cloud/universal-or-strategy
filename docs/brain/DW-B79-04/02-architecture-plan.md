# DW-B79-04 Architecture Plan

**Block**: DW-B79-04
**Author**: ptt-architect (Phase 1)
**Status**: REVIEW_PENDING
**Date**: 2026-08-20
**Tickets**: 2 (DW-B79-CANCEL-01, DW-B79-LOG-01)
**File**: src/PropTraderTools/CopyEngine.cs (single file, surgical changes only)

---

## 1. EPIC SUMMARY

DW-B79-04 is a 2-ticket bundle. Both tickets touch a single file (`src/PropTraderTools/CopyEngine.cs`)
and make surgical, non-structural changes to two separate methods. No new classes, no new files,
no interface changes, no namespace changes.

| Ticket | ID | Priority | Method | Line Approx | Change Type |
|--------|----|----------|--------|-------------|-------------|
| T1 | DW-B79-CANCEL-01 | P1 | `CancelAllAccountOrders` | L706-731 | Remove enum value from stateOk; add RemoveAll belt-and-suspenders; update comment |
| T2 | DW-B79-LOG-01 | P3 | `TryEvictFollowerBeSlot` | L1075-1087 | Capture TryRemove bool; gate log on it |

**Deferred items from DW-B79-03**: NONE. The DW-B79-03 deferred backlog confirms all acceptance
criteria met; no open items carried forward.

**Protected line**: `MoveStopToBreakEven` stateOk at L2662 contains `OrderState.ChangeSubmitted`
intentionally (target-price snapshot for OCO pair creation). This line is **FROZEN** -- it must
not be touched by this epic under any circumstances.

---

## 2. TICKET-1 DESIGN: DW-B79-CANCEL-01

### Problem Statement

`CancelAllAccountOrders` at L713 builds a cancel list using a stateOk filter that includes
`OrderState.ChangeSubmitted`. When NT8's native ATM auto-breakeven sends a price-modification
request, the stop order transiently enters `ChangeSubmitted` state. PTT adds it to the cancel
list. By the time `acc.Cancel()` reaches the broker, the modification has completed and the
stop has returned to `Working` (or filled). The broker rejects the stale cancel, NT8 fires
an asynchronous OMS "Cancellation rejected" event, and a popup appears. The `try { } catch { }`
around `acc.Cancel()` does NOT suppress this because the rejection is an async OMS event, not
a C# exception raised at the call site.

### Root Cause

`OrderState.ChangeSubmitted` should never be in a cancel filter. A ChangeSubmitted order has
an in-flight modification at the exchange. It cannot be directly cancelled. After the modification
resolves it returns to `Working`; after the enclosing PTT-Flatten fills, ATM OCO cascade handles
it. There is no position-protection gap in excluding it from the cancel list.

### Exact Changes to `CancelAllAccountOrders`

**Change A -- Comment at L710** (remove ChangeSubmitted from States list):

```
BEFORE L710:
    //   States: Working|Submitted|Accepted|ChangePending|ChangeSubmitted.

AFTER L710:
    //   States: Working|Submitted|Accepted|ChangePending.
```

Rationale: ChangeSubmitted is no longer in the filter; keeping it in the comment would be
misleading documentation.

**Change B -- stateOk at L723** (remove ChangeSubmitted term):

```csharp
// BEFORE (L719-723):
bool stateOk = o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Initialized
            || o.OrderState == OrderState.Submitted
            || o.OrderState == OrderState.Accepted
            || o.OrderState == OrderState.ChangeSubmitted;   // <-- REMOVE

// AFTER (L719-722, 4 terms):
bool stateOk = o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Initialized
            || o.OrderState == OrderState.Submitted
            || o.OrderState == OrderState.Accepted;
```

**Change C -- RemoveAll belt-and-suspenders** (insert between existing L728 and L729):

```csharp
// AFTER existing L728: toCancel.Add(o);
// AFTER closing brace of foreach (L728)
// BEFORE existing L729: if (toCancel.Count == 0) return;

toCancel.RemoveAll(o => o.OrderState == OrderState.Filled
                        || o.OrderState == OrderState.Cancelled);
```

Placement rationale: After the foreach builds `toCancel` but before the `Count == 0` guard.
The existing `Count == 0` guard (L729) will also fire if RemoveAll empties the list (defensive
overlap — correct). RemoveAll is NOT inside the `try` block; it operates on a local
`List<Order>` and cannot throw an NT8 exception.

**Change D -- CYC comment update at L711**:

```
BEFORE L711:
    // CYC=4: null-guard(1) + foreach(2) + stateOk(3) + instrument-name(4). JS-021: no lock.

AFTER L711:
    // CYC=4: null-guard(1) + foreach(2) + stateOk-4terms(3) + instrument-name(4). JS-021: no lock.
```

### CYC Impact Analysis (TICKET-1)

| Metric | Before | After | Rule |
|--------|--------|-------|------|
| stateOk OR terms | 5 | 4 | N/A |
| Structural branch count (project convention) | 4 | 4 | CYC <= 8 |
| Strict McCabe OR-operator count | +1 per \|\| | -1 | CYC reduces or stays |
| RemoveAll lambda branch | 0 | 0 (lambda is external) | N/A |

CYC stays at 4 under project structural convention. Under strict McCabe it reduces by 1.
Neither increases CYC. JS rule (CYC <= 8) satisfied.

### Final Method Shape After TICKET-1

```csharp
// B69 DW-B69-01: CancelAllAccountOrders -- cancel every active order on acc for instr
// before submitting a market flatten. No name filter -- all order names cancelled.
// NT8 precedent: @2Custom-0909edcc EmergencyFlattenSingleFleetAccount [938-EF-GUARD]:
//   "Step 1: Cancel ALL working orders on this instrument for this account."
//   States: Working|Submitted|Accepted|ChangePending.
// CYC=4: null-guard(1) + foreach(2) + stateOk-4terms(3) + instrument-name(4). JS-021: no lock.
// JS-001: no throw. JS-002: void. ASCII-only.
internal void CancelAllAccountOrders(Account acc, NinjaTrader.Cbi.Instrument instr)
{
    if (acc == null || instr == null) return;                              // (1)
    var toCancel = new System.Collections.Generic.List<Order>();
    foreach (Order o in acc.Orders)                                        // (2)
    {
        bool stateOk = o.OrderState == OrderState.Working
                    || o.OrderState == OrderState.Initialized
                    || o.OrderState == OrderState.Submitted
                    || o.OrderState == OrderState.Accepted;
        if (!stateOk) continue;                                            // (3)
        if (o.Instrument == null
            || o.Instrument.FullName != instr.FullName) continue;          // (4)
        toCancel.Add(o);
    }
    toCancel.RemoveAll(o => o.OrderState == OrderState.Filled
                            || o.OrderState == OrderState.Cancelled);
    if (toCancel.Count == 0) return;
    try { acc.Cancel(toCancel); } catch { }
}
```

### Why L2662 in MoveStopToBreakEven MUST NOT Change

`MoveStopToBreakEven` at L2662 uses `OrderState.ChangeSubmitted` in its own stateOk filter
for a completely different purpose: it is **reading** the current prices of the ATM stop/target
orders to build the OCO bracket pair for a breakeven move. When the ATM sends a price-mod, the
stop transiently enters ChangeSubmitted but its `LimitPrice`/`StopPrice` fields are still valid
and readable. If ChangeSubmitted is excluded from MoveStopToBreakEven's snapshot, PTT would
fail to find the target order during the transient window and take the bare-stop fallback path
incorrectly. The comment at L2662 reads:
`// DW-B79-04: NT8 sim ATM target transient state on creation`
This annotation documents the intentional inclusion. It must remain.

The two uses are:
- `CancelAllAccountOrders` stateOk: ACTION filter (which orders to cancel) -- ChangeSubmitted EXCLUDED.
- `MoveStopToBreakEven` stateOk: READ filter (which orders to snapshot prices from) -- ChangeSubmitted KEPT.

### TDD Test Design: `CancelAllAccountOrders_SkipsChangeSubmittedOrders`

**File**: `src/PropTraderTools/Tests/B79Tests.cs` (append to existing `B79Tests` class)
**Class**: `B79Tests` (existing sealed class -- use partial if needed, otherwise append [Fact])
**Method name**: `CancelAllAccountOrders_SkipsChangeSubmittedOrders`

**What it asserts**:

NT8 `Account` is a sealed runtime class. Direct instantiation is impossible in unit tests.
The test uses IL token scanning (same pattern as existing B79Tests methods) to verify that
`OrderState.ChangeSubmitted` does NOT appear as an ldsfld/ldc argument in the stateOk
computation of `CancelAllAccountOrders`.

Strategy: scan `CancelAllAccountOrders` method body IL for any reference to the
`OrderState.ChangeSubmitted` enum value. The enum field token must be absent from the method's
IL byte stream.

```
[Fact]
CancelAllAccountOrders_SkipsChangeSubmittedOrders:

  Step 1: Reflect CancelAllAccountOrders on CopyEngine via BindingFlags.NonPublic|Instance.
  Step 2: Get IL byte array from MethodBody.
  Step 3: Resolve all ldsfld tokens in the IL. For each token that resolves to a FieldInfo,
          check if FieldInfo.DeclaringType == typeof(OrderState) and FieldInfo.Name == "ChangeSubmitted".
  Assert: No such token found. (OrderState.ChangeSubmitted is not loaded anywhere in the method body.)
  
  Secondary assert:
  Step 4: Also verify Working, Accepted, Submitted, Initialized ARE present (confirm stateOk
          was not accidentally emptied -- regression guard).
```

**Acceptance**:
- The test must FAIL against the BEFORE state of the code (ChangeSubmitted IS in stateOk).
- The test must PASS after TICKET-1 is applied.

---

## 3. TICKET-2 DESIGN: DW-B79-LOG-01

### Problem Statement

`TryEvictFollowerBeSlot` at L1082 calls `_pendingFollowerBeSlots.TryRemove(accName, out _)`
but ignores the bool return value. The subsequent `Output.Process` log fires unconditionally
even when the slot was already consumed (TryRemove returned false). In Named ATM multi-bracket
fill scenarios, this generates up to 10 redundant log lines per trade close, polluting the
NT8 Output tab.

### Exact Changes to `TryEvictFollowerBeSlot`

**Change A -- capture bool from TryRemove at L1082**:

```csharp
// BEFORE L1082:
_pendingFollowerBeSlots.TryRemove(accName, out _);                     // no-op if already consumed

// AFTER L1082:
bool slotEvicted = _pendingFollowerBeSlots.TryRemove(accName, out _);  // DW-B79-04: capture for log gate
```

**Change B -- gate Output.Process on slotEvicted**:

```csharp
// BEFORE L1083-1086:
_beReplaceAttempts.TryRemove(accName, out _);                          // ALWAYS reset on flat
NinjaTrader.Code.Output.Process(
    "[BE-RETRY] " + accName + " position closed -- evicted BE slot + reset attempt counter",
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);

// AFTER L1083-1087:
_beReplaceAttempts.TryRemove(accName, out _);                          // ALWAYS reset on flat
if (slotEvicted)
{
    NinjaTrader.Code.Output.Process(
        "[BE-RETRY] " + accName + " position closed -- evicted BE slot + reset attempt counter",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
```

Key invariant preserved: `_beReplaceAttempts.TryRemove` is NOT gated -- it remains unconditional.
The comment `// ALWAYS reset on flat` must be preserved. Only the log is gated.

### CYC Impact Analysis (TICKET-2)

| Metric | Before | After | Rule |
|--------|--------|-------|------|
| Structural branches | 3 (3x if-guards) | 4 (3 guards + if(slotEvicted)) | CYC <= 8 |
| bool slotEvicted declaration | 0 (not a branch) | 0 | N/A |

The `if (slotEvicted)` adds exactly 1 new decision point. CYC goes from 3 to 4.
Well within the <= 8 limit. The CYC comment must be updated:

```
// BEFORE (implied from method structure -- no explicit CYC comment in original):
// No CYC comment in TryEvictFollowerBeSlot.

// AFTER (add CYC annotation to opening comment if none exists, or update):
// CYC=4: filled-guard(1) + follower-guard(2) + flat-guard(3) + slotEvicted-gate(4). JS-021: no lock.
```

### Final Method Shape After TICKET-2

```csharp
private void TryEvictFollowerBeSlot(OrderEventArgs e)
{
    var o = e?.Order;
    if (o == null || o.OrderState != OrderState.Filled) return;            // (1)
    if (!IsFollowerAccount(o.Account)) return;                             // (2) followers only
    if (!IsFlat(FindPosition(o.Account, o.Instrument))) return;            // (3) only evict if flat
    string accName = o.Account?.Name ?? string.Empty;
    bool slotEvicted = _pendingFollowerBeSlots.TryRemove(accName, out _);  // DW-B79-04: capture for log gate
    _beReplaceAttempts.TryRemove(accName, out _);                          // ALWAYS reset on flat
    if (slotEvicted)
    {
        NinjaTrader.Code.Output.Process(
            "[BE-RETRY] " + accName + " position closed -- evicted BE slot + reset attempt counter",
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    }
}
```

### No New Test Required

Existing `B79BeReplaceFallbackTests` (or equivalent) covers the idempotent `TryRemove` behavior.
The change is purely a log-gate; it does not alter any observable state or branching outside of
the Output.Process call. The log suppression is a P3 maintenance fix with no correctness risk.

---

## 4. 7-SCAN CHECKLIST TEMPLATE (for ptt-engineer)

Both tickets must pass all 7 scans before the PR is considered complete.

```
SCAN-01: ASCII-only
  grep -P "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
  EXPECTED: 0 results in new/modified lines.

SCAN-02: lock() usage (JS-021 -- P0 CRITICAL)
  grep -n "lock(" src/PropTraderTools/CopyEngine.cs
  EXPECTED: 0 results (no new or pre-existing lock in file).

SCAN-03: async void (JS-033 -- P0 CRITICAL)
  grep -n "async void" src/PropTraderTools/CopyEngine.cs
  EXPECTED: 0 results in CancelAllAccountOrders or TryEvictFollowerBeSlot.
  (Both are sync void -- confirmed.)

SCAN-04: return null (JS-002 -- P0 CRITICAL)
  grep -n "return null" src/PropTraderTools/CopyEngine.cs
  EXPECTED: 0 in CancelAllAccountOrders (void) and TryEvictFollowerBeSlot (void).

SCAN-05: throw new Exception (JS-001 -- P0 CRITICAL)
  grep -n "throw new" src/PropTraderTools/CopyEngine.cs
  EXPECTED: 0 in CancelAllAccountOrders and TryEvictFollowerBeSlot.

SCAN-06: CYC <= 8
  CancelAllAccountOrders: CYC=4 (structural). Verify comment L711 reads "CYC=4".
  TryEvictFollowerBeSlot: CYC=4 (structural). Verify if the CYC annotation is updated.
  Both must score <= 8 on complexity_audit.py.

SCAN-07: Build passes
  powershell -File .\scripts\build_readiness.ps1
  EXPECTED: 0 errors, 0 warnings.
  dotnet csharpier check src/
  EXPECTED: 0 formatting issues.
```

---

## 5. SPEC REQUIREMENTS MAPPING

| Req ID | Description | Ticket |
|--------|-------------|--------|
| DW-B79-CANCEL-01-R1 | Remove OrderState.ChangeSubmitted from stateOk in CancelAllAccountOrders | T1 |
| DW-B79-CANCEL-01-R2 | Add RemoveAll belt-and-suspenders before acc.Cancel() | T1 |
| DW-B79-CANCEL-01-R3 | Update L710 comment (remove ChangeSubmitted from States list) | T1 |
| DW-B79-CANCEL-01-R4 | New xUnit [Fact] CancelAllAccountOrders_SkipsChangeSubmittedOrders | T1 |
| DW-B79-CANCEL-01-R5 | L2662 MoveStopToBreakEven ChangeSubmitted MUST NOT change | T1 (protect-only) |
| DW-B79-LOG-01-R1 | Capture bool from _pendingFollowerBeSlots.TryRemove | T2 |
| DW-B79-LOG-01-R2 | Gate Output.Process log on slotEvicted bool | T2 |
| DW-B79-LOG-01-R3 | _beReplaceAttempts.TryRemove remains unconditional | T2 |

---

## 6. COMPONENT LIST

| Component | Class | Method | File |
|-----------|-------|--------|------|
| Cancel filter | CopyEngine | CancelAllAccountOrders | src/PropTraderTools/CopyEngine.cs |
| BE eviction log | CopyEngine | TryEvictFollowerBeSlot | src/PropTraderTools/CopyEngine.cs |
| New test | B79Tests | CancelAllAccountOrders_SkipsChangeSubmittedOrders | src/PropTraderTools/Tests/B79Tests.cs |

No new classes. No new interfaces. No new namespaces.

---

## 7. THREADING MODEL

**CancelAllAccountOrders**: Synchronous, called on NT8 dispatch thread from the order/account
event pipeline. `acc.Orders` is an NT8-managed collection; iteration is safe on the dispatch
thread. The new `RemoveAll` call operates on a local `List<Order>` (thread-local stack), no
cross-thread concern. No `Dispatcher.InvokeAsync` required.

**TryEvictFollowerBeSlot**: Synchronous, called on NT8 event thread from `OnOrderUpdate`.
`_pendingFollowerBeSlots.TryRemove` is lock-free (ConcurrentDictionary). `bool slotEvicted`
is a stack value type. `NinjaTrader.Code.Output.Process` is thread-safe. No Dispatcher change.

JS-021 compliance: zero `lock()` calls in either method. ✓

---

## 8. DATA FLOW

### TICKET-1 Data Flow (After Fix)

```
NT8 ATM auto-breakeven fires price-mod
  -> stop enters OrderState.ChangeSubmitted
  -> PTT OrderUpdate event fires
  -> CancelAllAccountOrders called
  -> foreach acc.Orders
  -> stateOk: Working|Initialized|Submitted|Accepted (ChangeSubmitted EXCLUDED)
  -> ChangeSubmitted stop NOT added to toCancel
  -> RemoveAll: remove any Filled/Cancelled (belt-and-suspenders for race window)
  -> if (toCancel.Count == 0) return  (possible if only order was ChangeSubmitted)
  -> acc.Cancel(filtered list) -- no stale cancel sent to broker
  -> NO "Cancellation rejected" OMS event
  -> NO popup in NT8 UI
```

### TICKET-2 Data Flow (After Fix)

```
Named ATM multi-bracket fill (10 fills possible)
  -> each fill fires OrderUpdate -> TryEvictFollowerBeSlot
  -> (1) fill guard: o.OrderState == Filled ✓
  -> (2) follower guard: IsFollowerAccount ✓
  -> (3) flat guard: IsFlat ✓
  -> First fill:  slotEvicted = TryRemove -> TRUE  -> log fires (1 line)
  -> Second+ fills: slotEvicted = TryRemove -> FALSE -> log suppressed (0 lines)
  -> _beReplaceAttempts.TryRemove: unconditional (all 10 fills, idempotent)
  -> Net result: exactly 1 "[BE-RETRY] ... evicted" log line per trade close
```

---

## 9. NT8 API USAGE

| API | Usage | Location | Confirmed |
|-----|-------|----------|-----------|
| `OrderState.ChangeSubmitted` | Removed from stateOk | L723 (delete) | Yes -- existing L723 |
| `OrderState.Filled` | RemoveAll predicate | New line (after L728) | Yes -- existing L1078 |
| `OrderState.Cancelled` | RemoveAll predicate | New line (after L728) | Yes -- standard NT8 enum |
| `List<Order>.RemoveAll` | Belt-and-suspenders filter | New line (after L728) | Yes -- BCL |
| `ConcurrentDictionary.TryRemove` | Existing call, capture return | L1082 | Yes -- BCL |
| `NinjaTrader.Code.Output.Process` | Existing call, now gated | L1084 | Yes -- existing L1084 |

No new NT8 API surface introduced. All APIs confirmed by existing usage in same methods.

---

## 10. ACCEPTANCE CRITERIA

| Criterion | Verification Method |
|-----------|---------------------|
| 292 [Fact] tests pass (291 existing + 1 new) | `dotnet test` in src/PropTraderTools/Tests/ |
| Zero "Cancellation rejected" popups in live session | Manual live-trading validation |
| [BE-RETRY] evict log fires exactly once per trade close (not 10x) | Live session Output tab observation |
| L2662 ChangeSubmitted in MoveStopToBreakEven unchanged | `git diff -- src/PropTraderTools/CopyEngine.cs` shows L2662 not in diff |
| CYC <= 8 for both modified methods | `python scripts/complexity_audit.py` |
| Build: 0 errors, 0 warnings | `powershell -File .\scripts\build_readiness.ps1` |
| No new lock() usage | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` returns 0 |
| ASCII-only in new lines | SCAN-01 passes |

---

## 11. CHANGE SUMMARY TABLE

| File | Method | Lines Affected | Nature |
|------|--------|---------------|--------|
| src/PropTraderTools/CopyEngine.cs | CancelAllAccountOrders | L710 (comment), L719-723 (remove L723), insert after L728 | Surgical |
| src/PropTraderTools/CopyEngine.cs | TryEvictFollowerBeSlot | L1082 (modify), L1083-1086 (add if-gate) | Surgical |
| src/PropTraderTools/Tests/B79Tests.cs | CancelAllAccountOrders_SkipsChangeSubmittedOrders | Append [Fact] to B79Tests class | New test |
| src/PropTraderTools/CopyEngine.cs | MoveStopToBreakEven (L2662) | FROZEN -- no change | Protected |

**Total changed lines (estimate)**: ~8 lines modified + ~6 lines added = ~14 net lines.
Well within the 10k-character PR diff limit.

---

*Plan complete. ptt-plan-reviewer: this plan is ready for Phase 2 review.*
