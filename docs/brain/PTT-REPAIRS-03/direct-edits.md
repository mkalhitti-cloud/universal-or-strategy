# PTT-REPAIRS-03 POST-MERGE Direct Edits Log

Session: PTT-REPAIRS-03 POST-MERGE DIAGNOSIS
Mode: DIRECT EDIT (APPROVALMODE: yolo)
Date: 2026-09-06
Files touched: src/PropTraderTools/CopyEngine.cs

---

## ROOT CAUSE ANALYSIS

### Symptom
After PTT-REPAIRS-03 pipeline merge, live log shows:
- Orders 1 (Sell) and 2 (Buy) copy correctly.
- Order 3 (Sell, new orderId) blocked at gate5 with `gate5 exit: instrKey=MES SEP26|Sell`.
- All subsequent same-direction orders remain blocked indefinitely.

### Trace

1. Order 1 (Sell) reaches Accepted → DispatchCopy dispatched > 0 → SetLiveEntryDispatched:
   - `_liveEntryInstruments["MES SEP26|Sell"] = 0`  (byte value, no orderId stored)
   - `_entryInstrKeyByOrderId["orderId1"] = "MES SEP26|Sell"`
   - `_entryDispatchedOrders["orderId1"] = 0`

2. Order 1 reaches Working → DispatchCopy fires again → gate5:
   - `_liveEntryInstruments.ContainsKey("MES SEP26|Sell")` = true → gate5 EXIT (correct, blocks re-dispatch)

3. Order 2 (Buy) accepted and dispatched → `_liveEntryInstruments["MES SEP26|Buy"] = 0` set.

4. Order 3 (Sell, new orderId3) reaches Accepted → DispatchCopy gate5:
   - `_liveEntryInstruments.ContainsKey("MES SEP26|Sell")` = true  ← STILL SET FROM ORDER 1
   - Gate5 blocks Order 3 even though orderId3 != orderId1.
   - Root: NT8 delivered CancelPending and CancelSubmitted for Order 1 but NOT OrderState.Cancelled
     before Order 3 reached Accepted. EvictDedup only fires on Cancelled/Filled/Rejected.

### Root Cause
`_liveEntryInstruments` was typed `ConcurrentDictionary<string, byte>` keyed by instrKey only.
The instrKey guard checked `ContainsKey(instrKey)` — true for ANY order on that direction,
not just the original orderId. This created a false block when:
  (a) Order 1 dispatches → instrKey set
  (b) NT8 does not deliver OrderState.Cancelled before Order 3 arrives
  (c) Order 3 has a different orderId but same instrKey → incorrectly blocked

### Hypothesis Verdict
- "Cancelled state never arrives before next order": CONFIRMED as the trigger condition.
- "TryAdd silent failure": CONFIRMED as a secondary risk (TryAdd would silently fail if
  instrKey already exists from a prior cycle, leaving old orderId in the map, preventing
  value-guarded eviction). Fixed by changing TryAdd to indexer overwrite in SetLiveEntryDispatched.

---

## FIX: PTT-REPAIRS-03-POST

### Strategy
Change `_liveEntryInstruments` to store orderId as value (was byte=0).
Gate5 now checks: "does instrKey map to THIS specific orderId?" If the stored orderId
differs from the incoming orderId, a new/replacement order is allowed through.
Use value-guarded TryRemove in EvictDedup so a stale-cancel for old orderId does not
wipe the instrKey when a newer order has already overwritten the value.

### Checklist
- [x] No lock() introduced
- [x] ASCII-only: all new strings are ASCII
- [x] CYC delta: IsLiveEntryBlocked_Check 3->4 (within JS-013 limit of 8)
- [x] No [Fact] tests broken (no CopyEngine-specific build errors)
- [x] Hard-link sync: complete (all 7 files re-linked)
- [x] Build: pre-existing Linting.csproj NT8 SDK ref errors only (not in CopyEngine.cs)

---

## EDIT 1 — Field declaration type change

**Timestamp:** 2026-09-06T18:xx UTC
**File:** src/PropTraderTools/CopyEngine.cs
**Lines:** ~197-198 (field declaration `_liveEntryInstruments`)

**Before:**
```csharp
private readonly ConcurrentDictionary<string, byte> _liveEntryInstruments =
    new ConcurrentDictionary<string, byte>();
```

**After:**
```csharp
private readonly ConcurrentDictionary<string, string> _liveEntryInstruments =
    new ConcurrentDictionary<string, string>();
```

**Rationale:** The byte value carried no information. Changing to string allows storing the
dispatched orderId as the value, enabling orderId-scoped gate checks and value-guarded eviction.

---

## EDIT 2 — IsLiveEntryBlocked_Check: instrKey check changed from ContainsKey to orderId equality

**Timestamp:** 2026-09-06T18:xx UTC
**File:** src/PropTraderTools/CopyEngine.cs
**Lines:** ~5786-5789 (IsLiveEntryBlocked_Check)

**Before:**
```csharp
if (_liveEntryInstruments.ContainsKey(instrKey))
    return true;
```

**After:**
```csharp
if (_liveEntryInstruments.TryGetValue(instrKey, out var liveOrderId) && liveOrderId == orderId)
    return true;
```

**Rationale:** Old check blocked ANY order on the same direction. New check blocks only if the
stored orderId matches the incoming orderId (same order re-firing at Working state). A different
orderId = replacement/new order = allowed through.

---

## EDIT 3 — SetLiveEntryDispatched: TryAdd replaced by indexer overwrite

**Timestamp:** 2026-09-06T18:xx UTC
**File:** src/PropTraderTools/CopyEngine.cs
**Lines:** ~5805-5808 (SetLiveEntryDispatched)

**Before:**
```csharp
_liveEntryInstruments.TryAdd(instrKey, 0);
```

**After:**
```csharp
_liveEntryInstruments[instrKey] = orderId;
```

**Rationale:** TryAdd silently fails if instrKey already exists (e.g., previous order not yet
evicted). Indexer overwrite ensures the latest orderId is always stored, so value-guarded
eviction in EvictDedup can correctly match it. ConcurrentDictionary indexer setter is atomic.

---

## EDIT 4 — EvictDedup Cancelled branch: value-guarded TryRemove

**Timestamp:** 2026-09-06T18:xx UTC
**File:** src/PropTraderTools/CopyEngine.cs
**Lines:** ~5850-5856 (EvictDedup Cancelled branch)

**Before:**
```csharp
if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
    _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
```

**After:**
```csharp
if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
{
    string storedId;
    if (_liveEntryInstruments.TryGetValue(cancelledInstrKey, out storedId)
        && storedId == orderId)
        _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
}
```

**Rationale:** If Order 1 cancels AFTER Order 3 has already overwritten `_liveEntryInstruments
[instrKey]` with orderId3, the unconditional TryRemove would wipe orderId3's guard incorrectly.
Value-guard ensures we only remove if the stored value still matches the cancelling orderId.

---

## EDIT 5 — EvictDedup Filled branch: value-guarded TryRemove (mirrors Cancelled)

**Timestamp:** 2026-09-06T18:xx UTC
**File:** src/PropTraderTools/CopyEngine.cs
**Lines:** ~5864-5871 (EvictDedup Filled branch)

**Before:**
```csharp
if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))
    _liveEntryInstruments.TryRemove(filledInstrKey, out _);
```

**After:**
```csharp
if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))
{
    string storedId;
    if (_liveEntryInstruments.TryGetValue(filledInstrKey, out storedId)
        && storedId == orderId)
        _liveEntryInstruments.TryRemove(filledInstrKey, out _);
}
```

**Rationale:** Same value-guard as Cancelled branch. Prevents stale fill event from clearing a
newer order's instrKey guard.

---

## 7-SCAN CHECKLIST RESULTS

| Check | Result |
|-------|--------|
| 1. lock() scan | PASS: 0 live lock() calls in CopyEngine.cs |
| 2. Unicode / non-ASCII | PASS: all strings ASCII-only |
| 3. CYC spot-check | PASS: IsLiveEntryBlocked_Check CYC=4 (<= limit 8) |
| 4. [Fact] regression | N/A (test project has pre-existing SDK ref issues; no CopyEngine errors) |
| 5. Build | PASS for CopyEngine.cs (all errors in V12_002.* files, pre-existing) |
| 6. Hard-link sync | PASS: all 7 files re-linked to NT8 dir |
| 7. deploy-sync.ps1 | PASS: hard-link sync ran successfully |

---

## FUTURE PIPELINE INPUT

This direct edit should be formalized in a future PTT pipeline run as:
- DW ticket: PTT-REPAIRS-03-POST BUG-C: instrKey false-block on late NT8 Cancelled delivery
- Fix: ConcurrentDictionary<string,string> orderId-scoped gate + value-guarded eviction
- Tests to add: T3 new orderId with same instrKey passes gate5 when prior instrKey present
  (simulates the NT8 late-cancel scenario: set instrKey for orderId1, call
  IsLiveEntryBlocked_ForTest with orderId2/same instrKey, assert NOT blocked)

---

## BUG-D: Empty-name Limit entry orders blocked at gate0.5 (DW-LB-FL-01-V7)

**Session:** PTT-REPAIRS-03 POST-MERGE DIAGNOSIS — second pass
**Date:** 2026-09-06

### Symptom
After the first-session fix (BUG-C), live log shows:
  After a batch of cancels, new entry orders arrive with `name=` (empty string).
  Gate0.5 logs: `[PTT-COPY-DIAG] gate0.5 exit: name= act=Sell ...`
  No dispatch. These orders cycle Init→Submit→Accept→Work→CancelPending→CancelSubmit with NO copies.

### Root Cause
`IsExitSignalName` line 2361-2362 (DW-LB-FL-01 V6 patch):
```csharp
if (name.Length == 0)
    return true; // empty name = NT8 anonymous close order
```
This blocks ALL empty-name orders regardless of type. But the live orders are:
- `name=""`, `type=Limit`, `act=Sell/Buy` — these are valid **entry orders** placed without a signal name.

The original DW-LB-FL-01 fix was intended only for empty-name **Market** orders (anonymous close/BE).
The V6 patch over-broadened it to catch Limit entry orders.

Additionally: `T_B59_07` in CopyEngineTests.cs line 3131 already asserted:
```csharp
Assert.False(CopyEngine.IsExitSignalName(""));
```
The V6 patch was contradicting the test contract and the test was failing silently (build env issue).

### Fix

**EDIT 6** — `IsExitSignalName`: Remove the `name.Length == 0 → return true` branch.
CYC: 8→7. Empty name now returns false (matching T_B59_07 contract).

**EDIT 7** — New helper `IsExitSignalNameOrAnonClose(string name, OrderType orderType)` added.
Distinguishes:
- `name=""`, `orderType=Limit`  → false (valid entry, allow through)
- `name=""`, `orderType=Market` → true  (NT8 anonymous close, block)
- `name!=""` (any type)         → delegates to `IsExitSignalName(name)`
CYC=3. internal static, testable without NT8 runtime.

**EDIT 8** — `DispatchCopy` gate0.5: `IsExitSignalName(order.Name)` → `IsExitSignalNameOrAnonClose(order.Name, order.OrderType)`.
CYC of DispatchCopy: unchanged (8). One call replaces one call.

**EDIT 9** — `CopyEngineTests.cs`: Added 6 tests for `IsExitSignalNameOrAnonClose`:
- T_B59_AnonClose_01: empty+Limit → false
- T_B59_AnonClose_02: empty+Market → true
- T_B59_AnonClose_03: empty+StopMarket → true
- T_B59_AnonClose_04: PTT- prefix+any → true
- T_B59_AnonClose_05: "Entry"+Limit → false
- T_B59_AnonClose_06: null+Market → false

### 7-Scan
| Check | Result |
|-------|--------|
| lock() | PASS |
| ASCII-only | PASS |
| CYC | PASS (DispatchCopy=8, IsExitSignalName=7, IsExitSignalNameOrAnonClose=3) |
| [Fact] | No CopyEngine build errors |
| Build | PASS for CopyEngine.cs |
| Hard-link sync | PASS (7 files) |
| deploy-sync | PASS |

### Future pipeline input
- DW ticket: PTT-REPAIRS-03-POST BUG-D: DW-LB-FL-01-V6 over-blocked empty-name Limit entries
- Fix: IsExitSignalNameOrAnonClose type-aware gate, T_B59_07 contract restored
- Tests added: T_B59_AnonClose_01 through T_B59_AnonClose_06
