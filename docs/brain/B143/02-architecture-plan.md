# B143 Architecture Plan

**Block**: B143
**Phase**: 1 (Architecture)
**Produced by**: ptt-architect
**Prior backlog**: `docs/brain/B142/06-deferred-backlog.md`
**Status**: REVIEW_PASS

---

## 1. Executive Summary

B143 documents and tests commit **3f709a91** — the MGC (Market Guard Complete) instrument-level
entry guard. This commit closes DW-B142-MGC-02 (instrument-level entry guard blocks duplicate
dispatches for the MGC cancel+resubmit pattern) and DW-B142-MGC-01 (root cause confirmed
resolved by MGC-02).

The feature adds two new `ConcurrentDictionary` fields and two new private helper methods to
`CopyEngine`. It modifies `EvictDedup` to clean up the instrument guard on cancel (no-fill path),
modifies `TryFirePositionState` to clean up the guard on leader position flat (safety-net path),
and replaces the inline dedup check in `DispatchCopy` Gate 5 with a single compound predicate
`IsLiveEntryBlocked` that fuses orderId-level dedup with instrument-level dispatch guarding.

No new NT8 API surface. No UI changes. Single-pipeline: all changes are mutually dependent on
the same data structures and cannot be split into independent lanes.

**Tests**: 7 (T_B143_01 through T_B143_07).

---

## 2. Commit In Scope

| Field | Value |
|-------|-------|
| **Commit SHA** | `3f709a91` |
| **Description** | MGC instrument-level entry guard (DW-B142-MGC-02) |
| **File changed** | `src/PropTraderTools/CopyEngine.cs` |
| **New test file** | `tests/PropTraderTools.Tests/B143Tests.cs` |
| **Lines affected** | L192-205 (fields), L2098-2104 (Gate 5), L3479-3494 (TryFirePositionState), L4604-4673 (new methods + EvictDedup) |

---

## 3. LANE-SPLIT GATE RESULT

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

Q1. Are all changes within the same method or within 50 lines of each other?
**NO** — Changes span four distinct locations (fields at ~L192, Gate 5 at ~L2104,
TryFirePositionState at ~L3493, and methods at ~L4613). Proceed to Q2.

Q2. Does fix B design depend on fix A final design?
**YES** — All changes are a single logical feature (DW-B142-MGC-02). `EvictDedup`'s Cancelled
cleanup path reads `_entryInstrKeyByOrderId`, which is written by `IsLiveEntryBlocked`. These
two components are mutually dependent and cannot be individually deployed. → SINGLE-PIPELINE.
STOP.

Q3. Does each fix have standalone value if the other is blocked? (Not evaluated — STOP above.)

Q4. Does each fix have an independent SIM verification path? (Not evaluated — STOP above.)

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

---

## 4. Architecture Analysis

### 4.1 `_liveEntryInstruments` and `_entryInstrKeyByOrderId` — Purpose and Invariants

**`_liveEntryInstruments: ConcurrentDictionary<string, byte>`**

A presence-only set keyed by instrument + direction composite key. The `byte` value (minimum
footprint) carries no semantic meaning — only key presence matters.

- **Key format**: `instrFullName + "|" + OrderAction` (e.g. `"MGC DEC26|Sell"`)
- **Set when**: `IsLiveEntryBlocked` first-pass (dispatch allowed) via `TryAdd`
- **Cleared when (Cancelled / no-fill path)**: `EvictDedup(orderId, Cancelled)` removes the
  key after tracing it via `_entryInstrKeyByOrderId`
- **Cleared when (position flat / safety-net)**: `TryFirePositionState` calls
  `ClearLiveEntryForInstrument` on leader flat, which prefix-scans and removes all keys for
  the instrument
- **NOT cleared on Filled**: trade is live; the guard must persist until position close

**Invariant**: Between first Gate 5 dispatch and position close, exactly one entry exists in
`_liveEntryInstruments` per (instrument, direction) pair. Any second dispatch attempt for
the same `instrKey` is rejected by `ContainsKey` before any other check.

**`_entryInstrKeyByOrderId: ConcurrentDictionary<string, string>`**

A companion map that makes orderId-to-instrKey lookup O(1) for the Cancelled cleanup path.
Without this map, the Cancelled cleanup would have to scan `_liveEntryInstruments` for the
matching key, which is less efficient and would require knowing the instrKey from the orderId
(not available in `EvictDedup`'s signature).

- **Key**: orderId (string) — same format as `_dedupCache` and `_entryDispatchedOrders`
- **Value**: instrKey (string) — the composite key written to `_liveEntryInstruments`
- **Written when**: `IsLiveEntryBlocked` first-pass via `TryAdd(orderId, instrKey)`
- **Removed (Cancelled)**: `EvictDedup` uses `TryRemove(orderId, out instrKey)` then removes
  `instrKey` from `_liveEntryInstruments`
- **Removed (Filled)**: `EvictDedup` lazy-cleans via `TryRemove(orderId, out _)` — the
  instrument key is NOT removed because the trade is still live

**Both dictionaries** are `ConcurrentDictionary` (JS-025 compliant), use no `lock()` (JS-021
compliant), and contain only ASCII-safe string keys (ASCII-only mandate compliant).

---

### 4.2 `IsLiveEntryBlocked` — Logic Walk-Through

```
private bool IsLiveEntryBlocked(string instrKey, string orderId, double limitPrice)
```

**Key format**: `instrKey = order.Instrument.FullName + "|" + order.OrderAction`

The pipe `|` character is a valid ASCII separator that cannot appear in a NinjaTrader instrument
`FullName` (instrument names use spaces and slashes, never pipes). `OrderAction` is an enum
that serializes to ASCII: `Buy`, `Sell`, `BuyToCover`, `SellShort`.

**CYC=4** (base=1 + 3 branches):

```
Branch 1: if (_liveEntryInstruments.ContainsKey(instrKey)) return true
Branch 2: if (IsDedup(orderId, limitPrice)) return true
Branch 3: if (IsEntryDispatched(orderId)) return true
// All three blocked: return false AND side-effects:
_liveEntryInstruments.TryAdd(instrKey, 0)
_entryInstrKeyByOrderId.TryAdd(orderId, instrKey)
return false
```

**TryAdd semantics**: `ConcurrentDictionary.TryAdd` is atomic. If two concurrent events
attempt the same instrKey, only one wins the TryAdd; the loser is rejected by Branch 1 on
the next evaluation. This is correct lock-free behaviour (JS-025).

**Three guard layers in order of evaluation**:
1. Instrument guard (broadest): blocks any second dispatch for same instrument+direction while
   a trade is live, regardless of orderId. This is the MGC-02 fix.
2. OrderId-price dedup (B62): blocks same orderId re-fired by NT8's duplicate event emission.
3. EntryDispatched guard (DW-B91-A): blocks same orderId that survived EvictDedup eviction.

**On first valid dispatch** (all three return false): both TryAdd calls record the state so
subsequent events for the same instrKey or orderId are blocked.

---

### 4.3 `ClearLiveEntryForInstrument` — Logic Walk-Through

```
private void ClearLiveEntryForInstrument(string instrFullName)
```

**CYC=2** (base=1 + foreach=1 + if=1; source comment counts CYC=2 at the foreach level):

```
foreach (var key in _liveEntryInstruments.Keys)   // snapshot-safe enumeration
{
    if (key.StartsWith(instrFullName + "|", StringComparison.Ordinal))
        _liveEntryInstruments.TryRemove(key, out _);
}
```

**ConcurrentDictionary.Keys enumeration**: Returns a point-in-time snapshot. Keys added after
enumeration starts are not visited; this is safe because position-flat events fire after all
dispatch events for the same fill have already completed.

**Prefix scan**: Removes all keys for the instrument, regardless of `OrderAction` suffix.
This correctly handles both `"MGC DEC26|Sell"` and `"MGC DEC26|Buy"` in one pass, which
is important for two-sided instruments (long and short entries at different times).

**No-op guarantee**: If `instrFullName` has no matching key, the `StartsWith` check never
fires the `TryRemove`. No exception, no side effects (T_B143_06).

---

### 4.4 `EvictDedup` Cancelled Path — Companion Map Cleanup

```
internal void EvictDedup(string orderId, OrderState state)
```

**CYC=5** (base=1 + terminal-guard=1 + Cancelled-branch=1 + TryRemove-guard=1 + Filled-branch=1):

**Cancelled path** (new in B143):
```csharp
_dedupCache.TryRemove(orderId, out _);           // existing

if (state == OrderState.Cancelled)
{
    _entryDispatchedOrders.TryRemove(orderId, out _);   // existing
    // NEW: instrument-level cleanup on no-fill cancel
    if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
        _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
}
```

**Design rationale**: `TryRemove` returns true only if `orderId` was a dispatched entry order
(i.e., `IsLiveEntryBlocked` had previously recorded it). ATM bracket cancels, drag cancels,
and other order cancels that never passed Gate 5 will have no entry in
`_entryInstrKeyByOrderId`, so `TryRemove` returns false and `_liveEntryInstruments` is
untouched. This is the **scoped removal** pattern — the comment at line 4656 states:
"do NOT Clear() the whole map. Bracket/drag/ATM cancels must not wipe the entry dispatch
guard for other orderIds."

**Filled path** (lazy companion map cleanup, instrument guard preserved):
```csharp
if (state == OrderState.Filled)
{
    _entryInstrKeyByOrderId.TryRemove(orderId, out _);
    // _liveEntryInstruments NOT removed -- trade is live.
}
```

---

### 4.5 `TryFirePositionState` — `ClearLiveEntryForInstrument` on Leader Flat

`TryFirePositionState` at line 3451 fires `PositionStateChanged` on `Filled` and `PartFilled`
events. When `hasPos = false` (position has gone flat), the existing B135 block checks whether
the order's account is a leader account and if so removes the direction key:

```csharp
if (!hasPos)
{
    bool isLeaderAcct = false;
    foreach (var r in _rules)
    {
        if (e.Order.Account.Name == r.MasterAccount?.Name)
        {
            isLeaderAcct = true;
            break;
        }
    }
    if (isLeaderAcct)
    {
        _lastLeaderDirection.TryRemove(instr, out _);            // existing B135
        ClearLiveEntryForInstrument(instr);                       // NEW B143 -- DW-B142-MGC-02
    }
}
```

**CYC impact**: The `ClearLiveEntryForInstrument` call is a straight-line addition inside the
existing `if (isLeaderAcct)` block — it adds zero branches. `TryFirePositionState` CYC remains
at 8 (AT LIMIT). No extraction required; the budget is not exceeded.

**Role of this call**: Safety-net cleanup. The primary cleanup is `EvictDedup(Cancelled)` for
no-fill cancels. But if an entry fills (trade goes live) and the position later closes, the
`_liveEntryInstruments` key set in `IsLiveEntryBlocked` was NOT cleared on the Filled path.
`ClearLiveEntryForInstrument` on position flat is the authoritative post-trade cleanup,
ensuring new entries for the same instrument are unblocked after a clean position close.

---

### 4.6 `DispatchCopy` Gate 5 — `IsLiveEntryBlocked` Integration Point

In `DispatchCopy` at line 2082, Gate 5 previously called `IsDedup` and `IsEntryDispatched`
separately. B143 replaces both with a single compound call to `IsLiveEntryBlocked`:

```csharp
// Gate 5 -- BEFORE B143:
// if (IsDedup(orderId, limitPrice)) return;
// if (IsEntryDispatched(orderId)) return;

// Gate 5 -- AFTER B143 (DW-B142-MGC-02):
var orderId = order.OrderId.ToString();
var instrKey = order.Instrument.FullName + "|" + order.OrderAction;
if (IsLiveEntryBlocked(instrKey, orderId, order.LimitPrice))
    return;
```

**CYC impact**: The two separate returns are replaced by one. `DispatchCopy` CYC stays at 8
(AT LIMIT, unchanged). The instrument-level guard is now the broadest check evaluated first
inside `IsLiveEntryBlocked` — an MGC duplicate that arrives with a different orderId is
rejected at Branch 1 before `IsDedup` or `IsEntryDispatched` even execute.

---

## 5. CYC Audit (All Changed/New Methods)

| Method | Location | CYC | Budget | Status |
|--------|----------|-----|--------|--------|
| `IsLiveEntryBlocked` | L4613 | 4 | ≤8 | PASS |
| `ClearLiveEntryForInstrument` | L4629 | 2 | ≤8 | PASS |
| `EvictDedup` | L4643 | 5 | ≤8 | PASS |
| `TryFirePositionState` | L3451 | 8 | ≤8 | PASS (AT LIMIT) |
| `DispatchCopy` | L2082 | 8 | ≤8 | PASS (AT LIMIT, unchanged) |

**DW-B141-STP-CYC8-WALL check**: `SyncFollowerBracket`, `SyncAtmFollowerTarget`, and
`FindFollowerBracketOrder` (list overload) are NOT touched by B143. Their CYC=8 status is
unchanged. No extraction is triggered by B143.

---

## 6. JS Rule Constraints

| Rule | Description | B143 Compliance |
|------|-------------|-----------------|
| JS-021 | No `lock()` anywhere | PASS — all new operations use `ConcurrentDictionary` exclusively (`TryAdd`, `TryRemove`, `ContainsKey`, `Keys` enumeration) |
| JS-025 | Lock-free data structures | PASS — `ConcurrentDictionary<string,byte>` and `ConcurrentDictionary<string,string>` are the canonical lock-free set and map patterns |
| JS-001 | No throw in hot paths | PASS — `IsLiveEntryBlocked`, `ClearLiveEntryForInstrument`, `EvictDedup` are all void/bool returns; no throw anywhere |
| JS-002 | No return null | PASS — `IsLiveEntryBlocked` returns bool; `ClearLiveEntryForInstrument` and `EvictDedup` are void |
| JS-033 | No async void | PASS — all new/modified methods are synchronous |
| JS-023 | Atomic primitives for simple state | PASS — `byte` value in `_liveEntryInstruments` is presence-only; no independent bool/int needing atomic treatment |
| ASCII-only | No Unicode in string literals | PASS — `"|"` separator and `StringComparison.Ordinal` comparisons are ASCII-only |
| DateTime.Now ban | Use DateTime.UtcNow | PASS — no DateTime usage in new code |

---

## 7. Test Design (T_B143_01 through T_B143_06)

### Test Seam Required

`IsLiveEntryBlocked` and `ClearLiveEntryForInstrument` are `private` methods. The engineer
**MUST** add the following test accessor shims in `CopyEngine.cs`, adjacent to the existing
`TryFirePositionState_ForTest` shim at line 3501:

```csharp
internal bool IsLiveEntryBlocked_ForTest(string instrKey, string orderId, double limitPrice)
    => IsLiveEntryBlocked(instrKey, orderId, limitPrice);

internal void ClearLiveEntryForInstrument_ForTest(string instrFullName)
    => ClearLiveEntryForInstrument(instrFullName);
```

These are thin forwarding shims only — no logic. `EvictDedup` is already `internal`.

### Test Isolation Note

`CopyEngine` is a singleton. Tests MUST use unique instrKey prefixes per test to avoid
cross-test contamination. Recommended pattern: use `"TEST-B143-01|Sell"`,
`"TEST-B143-02|Sell"`, etc., as instrKeys per test.

---

### T_B143_01 — First Call Returns False (Dispatch Allowed)

| Field | Value |
|-------|-------|
| **Method under test** | `IsLiveEntryBlocked_ForTest` |
| **Inputs** | instrKey=`"TEST-B143-01|Sell"`, orderId=`"ORD-B143-01"`, limitPrice=`2000.0` |
| **Expected result** | Returns `false` — new instrKey and orderId, dispatch allowed |
| **Asserts** | `Assert.False(engine.IsLiveEntryBlocked_ForTest("TEST-B143-01|Sell", "ORD-B143-01", 2000.0))` |

---

### T_B143_02 — Second Call Same instrKey Returns True (Duplicate Blocked)

| Field | Value |
|-------|-------|
| **Method under test** | `IsLiveEntryBlocked_ForTest` |
| **Arrange** | Call T_B143_01 scenario first — `IsLiveEntryBlocked_ForTest("TEST-B143-02|Sell", "ORD-B143-02A", 2000.0)` returns false and records instrKey |
| **Act** | `IsLiveEntryBlocked_ForTest("TEST-B143-02|Sell", "ORD-B143-02B", 2000.0)` — different orderId, same instrKey |
| **Expected result** | Returns `true` — instrument already has a live entry, duplicate blocked |
| **Asserts** | First call: `Assert.False(...)`. Second call: `Assert.True(...)` |

---

### T_B143_03 — EvictDedup Cancelled Clears instrKey (Future Entry Unblocked)

| Field | Value |
|-------|-------|
| **Method under test** | `EvictDedup` (Cancelled path) via `IsLiveEntryBlocked_ForTest` |
| **Arrange** | Record instrKey: `IsLiveEntryBlocked_ForTest("TEST-B143-03|Sell", "ORD-B143-03", 2000.0)` → false (entry recorded) |
| **Act** | `engine.EvictDedup("ORD-B143-03", OrderState.Cancelled)` |
| **Assert** | `Assert.False(engine.IsLiveEntryBlocked_ForTest("TEST-B143-03|Sell", "ORD-B143-03C", 2000.0))` — instrument slot unblocked after cancel |

---

### T_B143_04 — EvictDedup Filled Does NOT Clear instrKey (Trade Still Live)

| Field | Value |
|-------|-------|
| **Method under test** | `EvictDedup` (Filled path) via `IsLiveEntryBlocked_ForTest` |
| **Arrange** | Record instrKey: `IsLiveEntryBlocked_ForTest("TEST-B143-04|Sell", "ORD-B143-04", 2000.0)` → false |
| **Act** | `engine.EvictDedup("ORD-B143-04", OrderState.Filled)` |
| **Assert** | `Assert.True(engine.IsLiveEntryBlocked_ForTest("TEST-B143-04|Sell", "ORD-B143-04F", 2000.0))` — instrument slot remains blocked (trade live) |

---

### T_B143_05 — ClearLiveEntryForInstrument Removes All Keys for Instrument

| Field | Value |
|-------|-------|
| **Method under test** | `ClearLiveEntryForInstrument_ForTest` |
| **Arrange** | Record two keys: `IsLiveEntryBlocked_ForTest("TEST-B143-05|Sell", "ORD-B143-05A", 2000.0)` and `IsLiveEntryBlocked_ForTest("TEST-B143-05|Buy", "ORD-B143-05B", 2000.0)` — both return false (both recorded) |
| **Act** | `engine.ClearLiveEntryForInstrument_ForTest("TEST-B143-05")` |
| **Assert** | Both keys now unblocked: `Assert.False(engine.IsLiveEntryBlocked_ForTest("TEST-B143-05|Sell", "ORD-B143-05C", 0.0))` and `Assert.False(engine.IsLiveEntryBlocked_ForTest("TEST-B143-05|Buy", "ORD-B143-05D", 0.0))` |

---

### T_B143_06 — ClearLiveEntryForInstrument Is No-Op When No Matching Key

| Field | Value |
|-------|-------|
| **Method under test** | `ClearLiveEntryForInstrument_ForTest` |
| **Arrange** | Record an unrelated key: `IsLiveEntryBlocked_ForTest("UNRELATED-INSTR|Sell", "ORD-B143-06U", 0.0)` → false (recorded) |
| **Act** | `engine.ClearLiveEntryForInstrument_ForTest("INSTRUMENT_NOT_PRESENT")` |
| **Assert** | No exception. Unrelated key is unaffected: `Assert.True(engine.IsLiveEntryBlocked_ForTest("UNRELATED-INSTR|Sell", "ORD-B143-06X", 0.0))` |

---

### T_B143_07 — EvictDedup(bracketOrderId, Cancelled) Does NOT Clear Live Entry Guard

| Field | Value |
|-------|-------|
| **Method under test** | `EvictDedup` (Cancelled path, non-entry orderId) via `IsLiveEntryBlocked_ForTest` |
| **Arrange** | Record entry instrKey: `IsLiveEntryBlocked_ForTest("TEST-B143-07|Sell", "ORD-B143-07A", 2000.0)` → false (entry recorded in both `_liveEntryInstruments` and `_entryInstrKeyByOrderId`) |
| **Act** | `engine.EvictDedup("BRACKET-ORD-B143-07", OrderState.Cancelled)` where `"BRACKET-ORD-B143-07"` was **never** written to `_entryInstrKeyByOrderId` (not a Gate 5 entry dispatch) |
| **Assert** | `Assert.True(engine.IsLiveEntryBlocked_ForTest("TEST-B143-07|Sell", "ORD-B143-07B", 2000.0))` — live entry guard for the original entry survives the bracket cancel; no cross-contamination |
| **Rationale** | Verifies the "scoped removal" contract (plan §4.4): `TryRemove` on `_entryInstrKeyByOrderId` returns false for a non-entry orderId, so `_liveEntryInstruments` is untouched. ATM bracket cancels, drag cancels, and other non-Gate-5 cancels must not wipe the instrument guard for live entries on other orderIds. |

---

## 8. DW Item Status

### 8.1 DW-B142-MGC-02 — Closure Evidence

| Field | Value |
|-------|-------|
| **ID** | DW-B142-MGC-02 |
| **Title** | Instrument-level entry guard blocks duplicate dispatches for MGC cancel+resubmit pattern |
| **Status** | **CLOSED** by commit `3f709a91` |
| **Closure mechanism** | `_liveEntryInstruments` key set on first Gate 5 pass. Subsequent dispatches for same `instrKey` rejected by `ContainsKey` check in `IsLiveEntryBlocked` Branch 1. |
| **Verification** | T_B143_01 (first dispatch allowed), T_B143_02 (duplicate blocked) |

---

### 8.2 DW-B142-MGC-01 — Closure Evidence

| Field | Value |
|-------|-------|
| **ID** | DW-B142-MGC-01 |
| **Title** | Root cause: MGC cancel+resubmit produces duplicate entry dispatch |
| **Status** | **CLOSED** (root cause resolved by MGC-02 guard) |
| **Closure mechanism** | The MGC instrument cancel+resubmit sequence produces a second `Submitted` event for a new orderId on the same instrument+direction. Gate 5 in `DispatchCopy` previously only checked orderId-level dedup, which cannot block a fresh orderId. `IsLiveEntryBlocked` Branch 1 (`ContainsKey(instrKey)`) blocks the second event before any orderId check. |

---

### 8.3 DW-B141-STP-CYC8-WALL — Impact Assessment

B143 does **NOT** touch `SyncFollowerBracket`, `SyncAtmFollowerTarget`, or
`FindFollowerBracketOrder` (list overload). The three AT LIMIT methods remain at CYC=8.

`TryFirePositionState` now reaches CYC=8 (AT LIMIT) after B143's straight-line addition
inside `if (isLeaderAcct)`. However, `TryFirePositionState` is NOT one of the three
DW-B141-STP-CYC8-WALL methods. The DW item scope covers only the three bracket-sync
methods listed. **No impact on DW-B141-STP-CYC8-WALL.**

**Note**: `TryFirePositionState` is now an additional method at CYC=8 AT LIMIT. Any future
change adding a branch to `TryFirePositionState` requires prior extraction. This is documented
in Section 10 (Deferred Items).

---

## 9. Scan Chain Definition

Seven scans the engineer and verifier MUST run, in order:

| Scan | Command | Pass Criterion |
|------|---------|----------------|
| **SCAN-01** ASCII-only | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | Zero results |
| **SCAN-02** lock() ban | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | Zero results |
| **SCAN-03** CYC audit | `python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs` | All methods report CYC ≤ 8 |
| **SCAN-04** Build | `dotnet build src/PropTraderTools/` | Zero errors, zero new warnings |
| **SCAN-05** Tests | `dotnet test tests/PropTraderTools.Tests/ --filter "FullyQualifiedName~B143"` | All 7 tests PASS, zero failures |
| **SCAN-06** Sync+Verify | `powershell -File scripts\ptt-sync-and-verify.ps1` | 0 MISMATCH lines |
| **SCAN-07** JS P0 gate | `grep -rn "async void " src/PropTraderTools/CopyEngine.cs; grep -rn "return null;" src/PropTraderTools/CopyEngine.cs` | Zero results for both patterns |

---

## 10. Deferred Items

### New Item — TryFirePositionState Now at CYC=8 AT LIMIT

| Field | Value |
|-------|-------|
| **ID** | DW-B143-POSSTATE-CYC8 |
| **Title** | `TryFirePositionState` reached CYC=8 after B143 — no further branching without extraction |
| **Status** | OPEN (architectural constraint) |
| **Priority** | P1 |
| **Target Block** | Next block touching `TryFirePositionState` |

`TryFirePositionState` is now at CYC=8 (AT LIMIT). Any future modification adding a
conditional branch requires prior extraction of one existing branch to a helper method.
This is consistent with the DW-B141-STP-CYC8-WALL pattern.

---

### Carried Forward — Open Items (Unchanged by B143)

| ID | Title | Priority | Status |
|----|-------|----------|--------|
| DW-B141-STP-CYC8-WALL | Three bracket-sync methods at CYC=8 limit | P1 | OPEN — unaffected by B143 |
| DW-B141-SIM-03 | Consecutive drags, no accumulation (SIM pending) | P1 | OPEN |
| DW-B64-01 | HandleEntryChange not firing — drag sync broken | P0 | OPEN — **next P0 priority after B143** |
| DW-B71-01..04 | Quick ALL follower bracket dispatch + QX guard | P1 | OPEN |
| DW-B63-01 | Double PTT-Flatten 11ms apart | P1 | OPEN |
| DW-B141 | SyncAtmFollowerTarget Phase C re-confirmation (SIM Test A pending) | P1 | OPEN |
| DW-B138 | Stop drag SIM Test B — must re-run with B142 3-leg behavior | P1 | OPEN |
| B135-DEFER-01 | Gap B — two simultaneous entries | P1 | OPEN |
| B135-DEFER-02 | Stale orders multi-session | P2 | OPEN |
| DW-B134-OCO-OBS | OBS-A/B/C/D partial-fill race conditions | P1 | OPEN |
| SHA-DOC-01 | SHA typo in documentation for DW-B142-DRAG commit | P2 | OPEN |
| DW-B141-SIM-01 | SIM Gate 1 — dual-resubmit confirmation | de-escalated | EFFECTIVELY CONFIRMED |
| DW-B141-SIM-02 | SIM Gate 2 — Stop2 drag / Target2 resubmit | P1 | EFFECTIVELY CONFIRMED |

---

## 11. LANE-SPLIT GATE RESULT (Repeated)

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

All changes in B143 form a single coherent feature (DW-B142-MGC-02 instrument-level entry
guard). `IsLiveEntryBlocked` writes to `_entryInstrKeyByOrderId`; `EvictDedup` reads it.
`DispatchCopy` Gate 5 calls `IsLiveEntryBlocked`; `TryFirePositionState` calls
`ClearLiveEntryForInstrument`. These are not separable into independent lanes.

---

*Produced by ptt-architect, B143 Phase 1. Required gate artifact for Phase 3 ticket generation.*
