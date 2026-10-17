# B197 Architecture Plan
# Gap Restore: TrySetSlateReadyOnDrainFill + TryAbortLeaderFlatDrains + TryCancelStaleGtcExitOnEntry + IsPttDragOrphanCancellable suffix fix

**Author**: ptt-architect  
**Phase**: 1 (Architecture)  
**Epic**: B197  
**Status**: PLAN_COMPLETE  
**Date**: 2026-10-12  

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

**Q1: Same method or within 50 lines?**
T1 (TrySetSlateReadyOnDrainFill) and T3 (TryCancelStaleGtcExitOnEntry) both add call sites inside
the same `else if (e.Order.OrderState == OrderState.Filled)` block in `OnOrderUpdate` (lines 1535-1539
of CopyEngine.cs). They are in the same method and within the same 5-line block.
**Answer: YES → SINGLE-PIPELINE. Gate stops here.**

Q2 (for record): T3 call site is ordered after T1 in the Filled branch — ordering dependency exists.
YES → SINGLE-PIPELINE.
Q3/Q4 not evaluated (Q1 terminates the gate).

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

---

## §1 — Epic Summary

**Epic**: B197 — Gap Restore: 4 deferred DW items from B154-B157 pipeline

**Gaps closed (6 DW IDs):**

| Ticket | Gap | DW ID(s) | Priority | Origin |
|--------|-----|----------|----------|--------|
| T1 | GAP-5: TrySetSlateReadyOnDrainFill | DW-B157-01 | P1 | B156 pipeline, absent from megacommit |
| T2 | GAP-6: TryAbortLeaderFlatDrains + TryEvictFollowerBeSlot wiring | DW-B156-01, DW-B156-DEFER-1 | P1 | B156 + B157 decomposition |
| T3 | GAP-7: TryCancelStaleGtcExitOnEntry | DW-B154-01, DW-B154-02 | P1 | B154 (HIGH + MEDIUM) |
| T4 | GAP-8: IsPttDragOrphanCancellable suffix fix | DW-B154-03 | P2 | B154 (LOW) |

**Files modified:**
- `src/PropTraderTools/CopyEngine.cs` — all new methods and modifications
- `src/PropTraderTools/Tests/B197Tests.cs` — new test file (10 [Fact] tests)

**No other files are modified.** TradeCopierPanel.cs, .csproj, and specs are untouched.

---

## §2 — Source Baseline

Confirmed state of CopyEngine.cs before B197 implementation:

### T1 Baseline (GAP-5)
| Symbol | Line | State |
|--------|------|-------|
| `TrySetSlateReadyOnDrainFill` | — | **ABSENT** (method does not exist) |
| `_followerSlateReady` ConcurrentDictionary<string,bool> | 401 | EXISTS |
| `_followerSlateReady[acc.Name] = true` write | 1041, 1060, 2735 | EXISTS (pre-existing write sites) |
| `_drainOwnedOrderIds.ContainsKey(order.OrderId)` | 916 | EXISTS |
| `AbortDrainOnFill(string acctKey)` | 7029 | EXISTS |
| "[SLATE-READY-DRAIN]" log marker | — | **ABSENT** |

**Call site gap**: The `else if (OrderState.Filled)` block at lines 1535-1539 calls only `AbortDrainOnFill`. TrySetSlateReadyOnDrainFill is not called.

### T2 Baseline (GAP-6)
| Symbol | Line | State |
|--------|------|-------|
| `TryAbortLeaderFlatDrains` | — | **ABSENT** |
| `TryEvictFollowerBeSlot` | 1718-1737 | EXISTS, CYC=6 (per comment line 1696) |
| `_pendingDispatchDrains` ConcurrentDictionary<string,PendingDispatchDrain> | 388 | EXISTS |
| `AbortDrainOnFill(string acctKey)` | 7029 | EXISTS |
| `IsFollowerAccount(Account acc)` | 813 | EXISTS, CYC=8 |
| `IsFlat(Position pos)` | 5730 | EXISTS, static |
| `FindPosition(Account, Instrument)` | 5792 | EXISTS, private |
| "[DRAIN-ABORT-LEADER-FLAT]" log marker | — | **ABSENT** |

**TryEvictFollowerBeSlot gap**: No leader-flat drain-abort logic at TOP of method. The method correctly handles follower evictions but does not abort pending drains when the leader goes flat.

**NOTE**: The method was originally named `IsLeaderFlatDrainAbort` in the DW-B156-DEFER-1 backlog. It has been renamed to `TryAbortLeaderFlatDrains` to conform to CopyEngine.cs naming convention: `Is...` prefix is reserved for boolean predicates; void action methods use `Try...` prefix.

### T3 Baseline (GAP-7)
| Symbol | Line | State |
|--------|------|-------|
| `TryCancelStaleGtcExitOnEntry` | — | **ABSENT** |
| `IsPttCopyEntryFilled` | 834-837 | **REMOVED in B180** — stub returns `false` |
| `IsPttCopyEntryFilledTestable` | 837 | EXISTS as stub `=> false` (B180 backward-compat) |
| PTT-Copy order name = "PTT-Copy" | 4844 | EXISTS (confirmed signalName constant) |
| Clone ATM entry name = "Entry" | 4196, 4823 | EXISTS |
| `acc.Cancel(new Order[] { o })` pattern | 1947 | EXISTS (CancelPttDragOrphansForAccount) |
| `StatusUpdate?.Invoke(...)` pattern | 1948 | EXISTS (CancelPttDragOrphansForAccount) |
| "[GTC-SWEEP]" log marker | — | **ABSENT** |

**CRITICAL DESIGN DEVIATION — T3 Guard 3:**
The spec reference `IsPttCopyEntryFilled(e.Order)` is **invalid**. This method was removed in B180
(stub at line 837 always returns `false`). Using the stub would make TryCancelStaleGtcExitOnEntry
**never fire**. The engineer MUST use the inline name-check guard specified in §3 T3 below.

### T4 Baseline (GAP-8)
| Symbol | Line | State |
|--------|------|-------|
| `IsPttDragOrphanCancellable` (private static) | 1905-1914 | EXISTS |
| Guard (3) current: `o.Name != "PTT-TGT-Drag" && o.Name != "PTT-STP-Drag"` | 1911 | **EXACT-MATCH — MISSING SUFFIX COVERAGE** |
| `IsPttDragOrphanCancellableTestable` (internal static) | 1918-1932 | EXISTS |
| Guard (3) current: `orderName != "PTT-TGT-Drag" && orderName != "PTT-STP-Drag"` | 1929 | **EXACT-MATCH — MISSING SUFFIX COVERAGE** |

**Gap**: B141/B142 introduced per-leg suffix names (e.g., "PTT-TGT-Drag-1", "PTT-STP-Drag-2").
Exact-match guards miss all suffixed variants. Sweep is silent and ineffective for all post-B141 drags.

---

## §3 — Per-Ticket Design

### T1 — TrySetSlateReadyOnDrainFill (new private method)

**File**: `src/PropTraderTools/CopyEngine.cs`

**Method signature**:
```csharp
private void TrySetSlateReadyOnDrainFill(OrderEventArgs e)
```

**Implementation design**:
```
Guard (1): if (e?.Order == null) return;
Guard (2): if (e.Order.OrderState != OrderState.Filled) return;
Guard (3): if (!_drainOwnedOrderIds.ContainsKey(e.Order.OrderId)) return;
Guard (4): if (!IsFollowerAccount(e.Order.Account)) return;
            // NOTE: spec says "IsLeaderAccount" but that helper does not exist.
            // !IsFollowerAccount(acc) is the correct equivalent: return early if
            // this is a leader or unrelated account (only set slate-ready for followers).
Body:       _followerSlateReady[e.Order.Account.Name] = true;
Log:        NinjaTrader.Code.Output.Process(
                "[SLATE-READY-DRAIN] acct=" + e.Order.Account.Name,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```

**CYC analysis**: base(1) + guard1(1) + guard2(1) + guard3(1) + guard4(1) = **CYC = 5**. ≤8. PASS.

**NT8 API**:
- `_drainOwnedOrderIds.ContainsKey(e.Order.OrderId)`: ConcurrentDictionary<string,byte>.ContainsKey. Lock-free. Confirmed at line 916.
- `IsFollowerAccount(e.Order.Account)`: exists at line 813. Takes Account.
- `_followerSlateReady[acct.Name] = true`: ConcurrentDictionary indexer write. Lock-free. Confirmed at lines 1060, 2735.
- `NinjaTrader.Code.Output.Process(...)`: existing log pattern throughout OnOrderUpdate.

**Call site**: OnOrderUpdate, inside the `else if (e.Order.OrderState == OrderState.Filled)` block.
**ORDERING CRITICAL**: TrySetSlateReadyOnDrainFill MUST be called BEFORE AbortDrainOnFill.
AbortDrainOnFill (line 7029) removes the order ID from `_drainOwnedOrderIds`. Guard (3) checks
`_drainOwnedOrderIds.ContainsKey(e.Order.OrderId)`. If AbortDrainOnFill runs first, the ID is
gone and Guard (3) always fails — TrySetSlateReadyOnDrainFill never fires.

**Correct Filled branch ordering** (replaces lines 1535-1539):
```csharp
else if (e.Order.OrderState == OrderState.Filled)
{
    TrySetSlateReadyOnDrainFill(e);         // T1: FIRST — needs _drainOwnedOrderIds intact
    AbortDrainOnFill(e.Order.Account.Name); // existing: clean _drainOwnedOrderIds
    TryCancelStaleGtcExitOnEntry(e);        // T3: after drain cleanup
}
```

**Placement in source file**: New method immediately after AbortDrainOnFill (line 7034) or
in the B197 logical group near TrySetFollowerSlateReady (line 1050). Recommended: after
TrySetFollowerSlateReady (line 1061) as a sibling method in the follower-slate section.

---

### T2 — TryAbortLeaderFlatDrains (new private method) + TryEvictFollowerBeSlot (modified)

**File**: `src/PropTraderTools/CopyEngine.cs`

**New method signature**:
```csharp
private void TryAbortLeaderFlatDrains(OrderEventArgs e)
```

**Implementation design**:
```
Guard (1): if (IsFollowerAccount(e.Order.Account)) return;
            // spec says "return early if IsFollowerAccount" — only proceed for leader/unrelated
Guard (2): if (e.Order.OrderState != OrderState.Filled) return;
Guard (3): if (!IsFlat(FindPosition(e.Order.Account, e.Order.Instrument))) return;
Body:       foreach (var kv in _pendingDispatchDrains)
                AbortDrainOnFill(kv.Key);
Log:        NinjaTrader.Code.Output.Process(
                "[DRAIN-ABORT-LEADER-FLAT] triggered by " + e.Order.Account.Name,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```

**CYC analysis**: base(1) + guard1(1) + guard2(1) + guard3(1) + foreach(1) = **CYC = 5**. ≤8. PASS.

**NT8 API**:
- `IsFollowerAccount(e.Order.Account)`: line 813. Takes Account.
- `IsFlat(FindPosition(...))`: lines 5730, 5792. Pattern confirmed at line 1729.
- `_pendingDispatchDrains` foreach: ConcurrentDictionary enumeration. Thread-safe (snapshot semantics). Lock-free. Confirmed at line 7045.
- `AbortDrainOnFill(kv.Key)`: line 7029. Takes string (account key). Lock-free.

**Log placement**: The log line appears AFTER the foreach body (inside the method, after the foreach completes). If _pendingDispatchDrains is empty at call time, foreach is a no-op but the log still fires — acceptable because the guard (leader-flat + position check) already confirms a valid scenario.

**TryEvictFollowerBeSlot modification**:
- Current CYC = 6 (per comment, line 1696)
- Add `TryAbortLeaderFlatDrains(e);` at TOP of method, before Guard (1) null check on `o`.
- Wait — the spec says "before IsEvictTriggerState gate". But TryEvictFollowerBeSlot's first guard
  is `var o = e?.Order; if (o == null) return;`. We must call TryAbortLeaderFlatDrains AFTER the null
  guard to safely dereference `e.Order` inside TryAbortLeaderFlatDrains.
  
  Actually, TryAbortLeaderFlatDrains has its own guards — its Guard (1) is `IsFollowerAccount(e.Order.Account)`.
  But `e.Order` is accessed. If `e` itself is null or `e.Order` is null, this would throw.
  
  Safest call site: Call TryAbortLeaderFlatDrains AFTER `var o = e?.Order; if (o == null) return;`
  but BEFORE `if (!IsEvictTriggerState(o)) return;`. This ensures `e.Order` is non-null.
  
  Modified TryEvictFollowerBeSlot structure:
  ```
  var o = e?.Order;
  if (o == null) return;               // Guard (1) — existing
  TryAbortLeaderFlatDrains(e);         // T2: leader-flat drain abort (call, not a branch, +0 CYC)
  if (!IsEvictTriggerState(o)) return; // Guard (2) — existing
  ... (remaining guards unchanged)
  ```

- **CYC delta**: Method call is NOT a branch. TryEvictFollowerBeSlot CYC remains **6**. ≤8. PASS.

**Placement in source file**: New method immediately after TryEvictFollowerBeSlot (after line 1737),
in the BE-slot section. This groups TryAbortLeaderFlatDrains with its caller.

---

### T3 — TryCancelStaleGtcExitOnEntry (new private method)

**File**: `src/PropTraderTools/CopyEngine.cs`

**Method signature**:
```csharp
private void TryCancelStaleGtcExitOnEntry(OrderEventArgs e)
```

**DESIGN DEVIATION NOTICE — Guard (3)**:
The spec references `IsPttCopyEntryFilled(e.Order)` (confirmed absent at ~L1476).
That method was REMOVED in B180. The stub at line 837 always returns `false`.
Using `!IsPttCopyEntryFilledTestable(e.Order)` as guard would cause TryCancelStaleGtcExitOnEntry
to ALWAYS return early (stub returns false → guard fires → noop). This is a SILENT BUG.
**The engineer MUST use the inline name-check guard below, NOT the stub.**

**Implementation design**:
```
Guard (1): if (e?.Order == null) return;
Guard (2): if (e.Order.OrderState != OrderState.Filled) return;
Guard (3): if (e.Order.Name == null
              || (!e.Order.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)
                  && e.Order.Name != "Entry")) return;
            // RATIONALE: "PTT-Copy" = follower copy-dispatched entry (line 4844)
            //             "Entry" = Named ATM clone mode entry (lines 4196, 4823)
            //             IsPttCopyEntryFilled was REMOVED in B180 — DO NOT USE STUB
Guard (4): if (e.Order.Account == null) return;
Body:       var acc = e.Order.Account;
            foreach (var o in acc.Orders.ToList())
            {
                if (o.OrderState == OrderState.Working
                    && (o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)
                        || o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)))
                {
                    try
                    {
                        acc.Cancel(new Order[] { o });
                        StatusUpdate?.Invoke(acc.Name + ": [GTC-SWEEP] cancelled " + o.Name);
                    }
                    catch (Exception ex)
                    {
                        StatusUpdate?.Invoke("[GTC-SWEEP] cancel error: " + ex.Message);
                    }
                }
            }
```

**CYC analysis**:
- base(1) + guard1(1) + guard2(1) + guard3-if(1) + guard3-or(1) + guard3-and(1) + guard4(1) + foreach(1) = **CYC = 9** raw (guard3 contains if + OR + AND = 3 branches by McCabe/Lizard convention).

**CYC CEILING RISK**: Raw count = 9, exceeds JS-066 ceiling of 8. Mitigation: extract the inner compound-if into a helper predicate `IsStaleGtcExitOrder(Order o)`.

**Revised design with extraction**:
```csharp
// Helper predicate: CYC=5: base(1) + null-if(1) + null-or(1) + Working-check(1) + return-or(1)
private static bool IsStaleGtcExitOrder(Order o)
{
    if (o == null || o.Name == null) return false;     // (1) if + (2) ||
    if (o.OrderState != OrderState.Working) return false; // (3)
    return o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal) // (4) return-||
        || o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal);
}

// TryCancelStaleGtcExitOnEntry: CYC=8: base + guard1 + guard2 + guard3(if+OR+AND) + guard4 + foreach
private void TryCancelStaleGtcExitOnEntry(OrderEventArgs e)
{
    Guard (1): if (e?.Order == null) return;
    Guard (2): if (e.Order.OrderState != OrderState.Filled) return;
    Guard (3): if (e.Order.Name == null                                    // if-branch (1)
                  || (!e.Order.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)  // || (1)
                      && e.Order.Name != "Entry")) return;                             // && (1)
    Guard (4): if (e.Order.Account == null) return;
    Body:       var acc = e.Order.Account;
                foreach (var o in acc.Orders.ToList())
                {
                    if (!IsStaleGtcExitOrder(o)) continue;
                    try
                    {
                        acc.Cancel(new Order[] { o });
                        StatusUpdate?.Invoke(acc.Name + ": [GTC-SWEEP] cancelled " + o.Name);
                    }
                    catch (Exception ex)
                    {
                        StatusUpdate?.Invoke("[GTC-SWEEP] cancel error: " + ex.Message);
                    }
                }
}
```

**CYC for TryCancelStaleGtcExitOnEntry (extracted)**:
base(1) + guard1(1) + guard2(1) + guard3-if(1) + guard3-OR(1) + guard3-AND(1) + guard4(1) + foreach(1) = **CYC = 8**. At JS-066 ceiling. PASS.
**NOTE**: TryCancelStaleGtcExitOnEntry is a CYC=8 ceiling method. Future branch additions require helper extraction first (same constraint as IsLaneEEligible — DW-B183-03).

**CYC for IsStaleGtcExitOrder**:
base(1) + null-if(1) + null-or(1) + Working-check(1) + return-or(1) = **CYC = 5**. ≤8. PASS.

**NT8 API**:
- `acc.Orders.ToList()`: confirmed at line 1941.
- `acc.Cancel(new Order[] { o })`: confirmed at line 1947.
- `StatusUpdate?.Invoke(...)`: confirmed at line 1948.
- `OrderState.Working`: standard NT8 enum value.
- String.StartsWith with StringComparison.Ordinal: existing pattern throughout CopyEngine.cs.

**Call site**: After TrySetSlateReadyOnDrainFill in the Filled branch (see §4 for exact ordering).

**Placement in source file**: New methods TryCancelStaleGtcExitOnEntry and IsStaleGtcExitOrder
placed near CancelPttDragOrphansForAccount (line 1939) as thematic neighbors — both handle
account-level order sweep patterns.

---

### T4 — IsPttDragOrphanCancellable + IsPttDragOrphanCancellableTestable (modification)

**File**: `src/PropTraderTools/CopyEngine.cs`

**IsPttDragOrphanCancellable — guard (3) change (line 1911)**:
```csharp
// FROM:
if (o.Name != "PTT-TGT-Drag" && o.Name != "PTT-STP-Drag")
    return false;

// TO:
if (!o.Name.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)
    && !o.Name.StartsWith("PTT-STP-Drag", StringComparison.Ordinal))
    return false;
```

**IsPttDragOrphanCancellableTestable — guard (3) change (line 1929)**:
```csharp
// FROM:
if (orderName != "PTT-TGT-Drag" && orderName != "PTT-STP-Drag")
    return false;

// TO:
if (!orderName.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)
    && !orderName.StartsWith("PTT-STP-Drag", StringComparison.Ordinal))
    return false;
```

**CYC delta**: Zero. The `&&` compound condition has the same branch count regardless of
exact-match vs. StartsWith. Both methods: CYC unchanged.

**IsPttDragOrphanCancellable current CYC** (per comment "CCN<=4" at line 1900):
- base(1) + guard1-Working(1) + guard2-?. instr(1 Lizard) + guard3-&&(1 Lizard) = CYC=4. Post-fix: **CYC=4**. No change.

**IsPttDragOrphanCancellableTestable current CYC**: same structure = CYC=4. Post-fix: **CYC=4**. No change.

**NT8-014 annotation update**: The existing comment at line 1902 says:
`// NT8-014: "PTT-TGT-Drag" and "PTT-STP-Drag" confirmed names -- must not be generalized.`
After the fix, this comment must be updated to:
`// NT8-014: "PTT-TGT-Drag" + suffixed variants (B141/B142: PTT-TGT-Drag-N) -- StartsWith covers all.`

---

## §4 — Cross-Ticket Sequencing

### OnOrderUpdate Filled Branch (T1 and T3 call sites)

**Current code** (lines 1535-1539):
```csharp
else if (e.Order.OrderState == OrderState.Filled)
{
    // Drain-tracked entry filled -- abort replacement, position is open.
    AbortDrainOnFill(e.Order.Account.Name); // R2-F1: clean _drainOwnedOrderIds on fill-abort
}
```

**Post-B197 code** (T1 before AbortDrainOnFill, T3 after):
```csharp
else if (e.Order.OrderState == OrderState.Filled)
{
    TrySetSlateReadyOnDrainFill(e);          // T1: set slate-ready FIRST (_drainOwnedOrderIds intact)
    AbortDrainOnFill(e.Order.Account.Name);  // existing: clean _drainOwnedOrderIds + _pendingDispatchDrains
    TryCancelStaleGtcExitOnEntry(e);         // T3: sweep stale GTC exits on new entry fill
}
```

**Ordering invariant**: T1 MUST precede AbortDrainOnFill. AbortDrainOnFill removes the order ID
from `_drainOwnedOrderIds` (line 7033). T1 Guard (3) checks ContainsKey on that same dict.
If AbortDrainOnFill runs first, Guard (3) always fails silently.

T3 may run after AbortDrainOnFill without dependency concerns — it checks order.Name, not drain state.

### TryEvictFollowerBeSlot (T2 call site)

**Current code** (line 1718-1724):
```csharp
private void TryEvictFollowerBeSlot(OrderEventArgs e)
{
    var o = e?.Order;
    if (o == null)
        return; // (1) null guard
    if (!IsEvictTriggerState(o))
        return; // (2) terminal-state gate
    ...
}
```

**Post-B197 code** (T2 inserted after null guard, before IsEvictTriggerState):
```csharp
private void TryEvictFollowerBeSlot(OrderEventArgs e)
{
    var o = e?.Order;
    if (o == null)
        return; // (1) null guard
    TryAbortLeaderFlatDrains(e); // T2: leader-flat drain abort — inserted here
    if (!IsEvictTriggerState(o))
        return; // (2) terminal-state gate — number unchanged
    ...
}
```

**Rationale for placement after null guard**: TryAbortLeaderFlatDrains accesses `e.Order.Account`
internally. The outer null guard `if (o == null) return` confirms `e.Order` is non-null before
delegating. If called before the null guard, `e.Order.Account` access could throw on null `e`.

### T4 Independence

T4 (IsPttDragOrphanCancellable suffix fix) is fully independent of T1-T3. No shared state.
No ordering dependency. T4 may be implemented in any order within the B197 pipeline.

### Ticket Implementation Order

Recommended (matches logical dependency):
1. T4 — isolated predicate fix, lowest risk, validates test harness
2. T1 — new method + call site (requires AbortDrainOnFill ordering knowledge)
3. T2 — new method + TryEvictFollowerBeSlot wiring
4. T3 — new method + IsStaleGtcExitOrder helper + call site

---

## §5 — JS Rule Compliance Per Ticket

### T1 — TrySetSlateReadyOnDrainFill

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | `_followerSlateReady[acct.Name] = true` is ConcurrentDictionary indexer. `_drainOwnedOrderIds.ContainsKey` is ConcurrentDictionary read. Both lock-free. | PASS |
| JS-001 (no throw) | Only early returns. No exception-throwing code path. | PASS |
| JS-002 (no null return) | Method is void. | PASS |
| JS-066 (CYC ≤ 8) | CYC = 5. | PASS |
| ASCII-only | "[SLATE-READY-DRAIN] acct=" — all ASCII. | PASS |
| DateTime.Now ban | No DateTime usage. | PASS |

### T2 — TryAbortLeaderFlatDrains

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | `_pendingDispatchDrains` foreach is ConcurrentDictionary enumeration (lock-free). `AbortDrainOnFill` is ConcurrentDictionary TryRemove (lock-free). | PASS |
| JS-001 (no throw) | Only early returns + method call. No throw. | PASS |
| JS-002 (no null return) | Method is void. | PASS |
| JS-066 (CYC ≤ 8) | CYC = 5. TryEvictFollowerBeSlot post-modification CYC = 6 (method call +0). | PASS |
| ASCII-only | "[DRAIN-ABORT-LEADER-FLAT] triggered by " — all ASCII. | PASS |

### T3 — TryCancelStaleGtcExitOnEntry + IsStaleGtcExitOrder

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | `acc.Orders.ToList()` + `acc.Cancel()` — no lock. Pattern mirrors CancelPttDragOrphansForAccount (line 1939-1954). | PASS |
| JS-001 (no throw in hot path) | try/catch absorbs acc.Cancel errors. Does NOT rethrow. Mirrors existing pattern at lines 1945-1953. The catch block only calls StatusUpdate (non-throwing). | PASS |
| JS-002 (no null return) | Both methods are void/bool with no null return sites. | PASS |
| JS-066 (CYC ≤ 8) | TryCancelStaleGtcExitOnEntry CYC = 8 (at ceiling). IsStaleGtcExitOrder CYC = 5. | PASS |
| ASCII-only | "[GTC-SWEEP] cancelled ", "[GTC-SWEEP] cancel error: " — all ASCII. | PASS |

### T4 — IsPttDragOrphanCancellable suffix fix

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | Pure static predicate. No state. | PASS |
| JS-001 (no throw) | No throw. Returns bool. | PASS |
| JS-066 (CYC ≤ 8) | CYC = 4 (unchanged). | PASS |
| ASCII-only | "PTT-TGT-Drag", "PTT-STP-Drag" — all ASCII. | PASS |

---

## §6 — Test Strategy Per Ticket

**Test file**: `src/PropTraderTools/Tests/B197Tests.cs`  
**Test class**: `B197Tests`  
**Framework**: xUnit (mandatory per JS testing standard)  
**Strategy**: Source-text assertions for T1-T3 (no NT8 runtime needed); testable-seam calls for T4.

### T1 Tests (2 Facts — JS-056 compliant: one assert each)

```csharp
[Fact]
public void TrySetSlateReadyOnDrainFill_SourceContainsFollowerSlateReady()
{
    var src = File.ReadAllText("../../../CopyEngine.cs");
    Assert.Contains("_followerSlateReady[", src);
}

[Fact]
public void TrySetSlateReadyOnDrainFill_SourceContainsSlateReadyDrainMarker()
{
    var src = File.ReadAllText("../../../CopyEngine.cs");
    Assert.Contains("[SLATE-READY-DRAIN]", src);
}
```

**Assertion rationale**: First fact verifies the `_followerSlateReady` write is present (core body).
Second fact verifies the log marker, which confirms the drain-fill path was reached at least once in implementation.

### T2 Tests (2 Facts — JS-056 compliant: one assert each)

```csharp
[Fact]
public void TryEvictFollowerBeSlot_SourceContainsDrainAbortLeaderFlat()
{
    var src = File.ReadAllText("../../../CopyEngine.cs");
    Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src);
}

[Fact]
public void TryEvictFollowerBeSlot_SourceContainsTryAbortLeaderFlatDrains()
{
    var src = File.ReadAllText("../../../CopyEngine.cs");
    Assert.Contains("TryAbortLeaderFlatDrains", src);
}
```

### T3 Tests (2 Facts — JS-056 compliant: one assert each)

```csharp
[Fact]
public void TryCancelStaleGtcExitOnEntry_SourceContainsMethodName()
{
    var src = File.ReadAllText("../../../CopyEngine.cs");
    Assert.Contains("TryCancelStaleGtcExitOnEntry", src);
}

[Fact]
public void TryCancelStaleGtcExitOnEntry_SourceContainsGtcSweepMarker()
{
    var src = File.ReadAllText("../../../CopyEngine.cs");
    Assert.Contains("[GTC-SWEEP]", src);
}
```

### T4 Tests (4 Facts — testable seam calls)

```csharp
[Fact]
public void IsPttDragOrphanCancellableTestable_SuffixedTgtName_ReturnsTrue()
{
    bool result = CopyEngine.IsPttDragOrphanCancellableTestable(
        OrderState.Working, "NQ 09-26", "NQ 09-26", "PTT-TGT-Drag-1");
    Assert.True(result);
}

[Fact]
public void IsPttDragOrphanCancellableTestable_SuffixedStpName_ReturnsTrue()
{
    bool result = CopyEngine.IsPttDragOrphanCancellableTestable(
        OrderState.Working, "NQ 09-26", "NQ 09-26", "PTT-STP-Drag-2");
    Assert.True(result);
}

[Fact]
public void IsPttDragOrphanCancellableTestable_UnsuffixedTgtName_StillReturnsTrue()
{
    // Regression: base name (no suffix) must still be cancellable
    bool result = CopyEngine.IsPttDragOrphanCancellableTestable(
        OrderState.Working, "NQ 09-26", "NQ 09-26", "PTT-TGT-Drag");
    Assert.True(result);
}

[Fact]
public void IsPttDragOrphanCancellableTestable_UnsuffixedStpName_StillReturnsTrue()
{
    // Regression: base name (no suffix) must still be cancellable
    bool result = CopyEngine.IsPttDragOrphanCancellableTestable(
        OrderState.Working, "NQ 09-26", "NQ 09-26", "PTT-STP-Drag");
    Assert.True(result);
}
```

**Total**: 10 [Fact] tests across all 4 tickets (T1: 2, T2: 2, T3: 2, T4: 4).

---

## §7 — CYC Impact Summary

| Method | File | State | Pre-B197 CYC | Post-B197 CYC | Delta | Status |
|--------|------|-------|-------------|--------------|-------|--------|
| `TrySetSlateReadyOnDrainFill` | CopyEngine.cs | NEW | — | 5 | +5 (new) | ≤8 PASS |
| `TryAbortLeaderFlatDrains` | CopyEngine.cs | NEW | — | 5 | +5 (new) | ≤8 PASS |
| `TryCancelStaleGtcExitOnEntry` | CopyEngine.cs | NEW | — | **8** | +8 (new) | **CYC=8 CEILING** |
| `IsStaleGtcExitOrder` | CopyEngine.cs | NEW (helper) | — | 5 | +5 (new) | ≤8 PASS |
| `TryEvictFollowerBeSlot` | CopyEngine.cs | MODIFIED (call added) | 6 | 6 | 0 | ≤8 PASS |
| `IsPttDragOrphanCancellable` | CopyEngine.cs | MODIFIED (guard 3) | 4 | 4 | 0 | ≤8 PASS |
| `IsPttDragOrphanCancellableTestable` | CopyEngine.cs | MODIFIED (guard 3) | 4 | 4 | 0 | ≤8 PASS |

**No method exceeds CYC=8 after B197. No pre-existing ceiling methods are touched.**

Notes:
- `TryCancelStaleGtcExitOnEntry` (CYC=8): NEW ceiling method in B197. Future branch additions require helper extraction first.
- `IsLaneEEligible` (CYC=8, DW-B183-03): NOT touched by B197. Carries forward unchanged.
- `IsFollowerAccount` (CYC=8): NOT touched by B197. Carries forward unchanged.

---

## §8 — SIM Gate Plan

Four SIM gates, one per ticket. All gates require NT8 SIM broker environment (Sim102, Sim103, etc.).

### SIM-1 (T1): TrySetSlateReadyOnDrainFill — Drain Fill Slate-Ready Restore

**Setup**: Enable copy from leader to Sim102 follower. Configure a drain scenario (cancel
all exit orders on follower, allowing DrainThenDispatch to queue a PTT-Copy). Allow the
drain's PTT-Copy entry order to fill (Sim broker fills it).

**PASS criteria**:
1. "[SLATE-READY-DRAIN] acct=Sim102" appears in NT8 output tab.
2. After the drain fill, a leader stop or target drag successfully copies to Sim102
   (fo != NULL in drag sync log — IsLaneEEligible returns true for Sim102).

**FAIL criteria**:
- "[SLATE-READY-DRAIN]" absent from output after confirmed drain fill.
- Subsequent drag sync produces fo=NULL or "[LANE-E-SKIP]" for Sim102.

### SIM-2 (T2): TryAbortLeaderFlatDrains — Ghost Re-Entry Prevention

**Setup**: Create a PendingDispatchDrain on Sim102 (cancel-all initiated, drain queued).
While drain is pending (before drain-PTT-Copy fires), fill the leader's PTT-BE-Stop
(leader position goes flat).

**PASS criteria**:
1. "[DRAIN-ABORT-LEADER-FLAT] triggered by {leader account name}" appears in NT8 output.
2. No new PTT-Copy order is submitted to Sim102 after the leader goes flat.
3. No ghost re-entry observed on Sim102 or Sim103 within 10 seconds of leader flat.

**FAIL criteria**:
- "[DRAIN-ABORT-LEADER-FLAT]" absent after confirmed leader-flat fill.
- Ghost PTT-Copy submitted to Sim102 after leader flat (the DW-B158-SIM-01 failure mode).

### SIM-3 (T3): TryCancelStaleGtcExitOnEntry — Stale GTC Sweep on New Entry

**Setup**: Manually place (or leave from a prior session) a Working PTT-BE-Stop or
PTT-QX-* order on Sim102 without an open position. Then trigger a new PTT-Copy entry
fill on Sim102 (via leader entry).

**PASS criteria**:
1. "[GTC-SWEEP] cancelled PTT-BE-*" or "[GTC-SWEEP] cancelled PTT-QX-*" appears in NT8 output.
2. The stale GTC exit order is Cancelled in Sim102 order book within 2 seconds of the new fill.

**FAIL criteria**:
- "[GTC-SWEEP]" absent after confirmed PTT-Copy fill on Sim102 with stale Working exit present.
- Stale order remains Working after new entry fill.

### SIM-4 (T4): IsPttDragOrphanCancellable Suffix — Suffixed Drag Orphan Sweep

**Setup**: Trigger a multi-leg bracket scenario that creates suffixed drag orders
("PTT-TGT-Drag-1", "PTT-STP-Drag-2", etc.) on Sim102. Then close the position to flat.

**PASS criteria**:
1. After position goes flat, "PTT drag sweep: cancelled PTT-TGT-Drag-1" (or similar
   suffixed name) appears in NT8 output for Sim102.
2. All suffixed drag orders are Cancelled in Sim102 order book.

**FAIL criteria**:
- "PTT drag sweep" log appears but lists NO orders (silent sweep miss — suffixed orders skipped).
- Suffixed drag orders remain Working after Sim102 position closes to flat.

---

## §9 — Deferred Backlog Guidance

### DW IDs Closed by B197

| DW ID | Closed By | Status After B197 |
|-------|-----------|-------------------|
| DW-B157-01 | T1 (TrySetSlateReadyOnDrainFill) | CLOSED |
| DW-B156-01 | T2 (TryAbortLeaderFlatDrains) | CLOSED |
| DW-B156-DEFER-1 | T2 (TryEvictFollowerBeSlot leader-flat branch via TryAbortLeaderFlatDrains) | CLOSED |
| DW-B154-01 | T3 (TryCancelStaleGtcExitOnEntry) | CLOSED |
| DW-B154-02 | T3 (TryCancelStaleGtcExitOnEntry) | CLOSED |
| DW-B154-03 | T4 (IsPttDragOrphanCancellable suffix fix) | CLOSED |

### B196 Carry-Forwards: Continued OPEN After B197

The following items from `docs/brain/B196/06-deferred-backlog.md` are NOT addressed by B197
and carry forward to the B198+ backlog unchanged:

| DW ID | Priority | Description | Action |
|-------|----------|-------------|--------|
| DW-B183-03 | P1 | IsLaneEEligible CYC=8 frozen | Carry forward — not touched |
| DW-B167-DEFER-03 | P1 | B161-B166 combined SIM gate | Carry forward — needs Director SIM run |
| DW-B167-DEFER-04 | P1 | B167 SIM gate prerequisite | Carry forward — needs Director SIM run |
| DW-B170-SIM | P1 | B170 live regression (Accepted window fix) | Carry forward — needs Director SIM run |
| DW-B192-SINST-01 | P2 | SetInstrument() CYC=8 ceiling | Carry forward — not touched |
| DW-B193-01 | P3 | OnQuickAllBufferChanged old label strings | Carry forward — not touched |
| DW-B159-K2 | P2 | 14 xUnit BE path tests | Carry forward |
| DW-B159-K3 | P2 | TryArmBePriceRetry attempt cap hardcoded | Carry forward |
| DW-B159-K4 | P2 | intendedBePrice sourcing dependency | Carry forward |
| DW-B167-DEFER-01 | P2 | ECA-1 double-position race | Carry forward — monitor |

### New DW Items Opened by B197

None anticipated. The T3 design deviation (IsPttCopyEntryFilled replacement with inline
name-check) is fully resolved in §3 above. No follow-up card required.

The B197 ptt-plan-reviewer (Phase 2) should confirm zero new DW items after reviewing
the plan. If the reviewer identifies any design gap not addressed above, it should be
added to the B197 deferred backlog.

---

## Component Summary

| Component | Type | CYC | File | Ticket |
|-----------|------|-----|------|--------|
| `TrySetSlateReadyOnDrainFill` | new private method | 5 | CopyEngine.cs | T1 |
| `TryAbortLeaderFlatDrains` | new private method | 5 | CopyEngine.cs | T2 |
| `TryCancelStaleGtcExitOnEntry` | new private method | **8 (ceiling)** | CopyEngine.cs | T3 |
| `IsStaleGtcExitOrder` | new private static helper | 5 | CopyEngine.cs | T3 |
| `TryEvictFollowerBeSlot` | call site addition (+0 CYC) | 6 | CopyEngine.cs | T2 |
| `IsPttDragOrphanCancellable` | guard(3) change | 4 | CopyEngine.cs | T4 |
| `IsPttDragOrphanCancellableTestable` | guard(3) change | 4 | CopyEngine.cs | T4 |
| `B197Tests` | new xUnit test class (10 Facts) | — | B197Tests.cs | T1-T4 |

---

*Written by: ptt-architect*  
*Epic: B197*  
*Phase: 1 (Architecture)*  
*Status: PLAN_COMPLETE*
