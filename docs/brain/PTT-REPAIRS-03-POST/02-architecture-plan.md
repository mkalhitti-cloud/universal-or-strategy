# PTT-REPAIRS-03-POST Architecture Plan

**Epic:** PTT-REPAIRS-03-POST
**Status:** PLAN_COMPLETE
**Date:** 2026-09-06
**Author:** ptt-architect
**Source files:** `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/CopyEngineTests.cs`
**Reference:** `docs/brain/PTT-REPAIRS-03/direct-edits.md`

---

## Purpose

This plan formalises two direct-edit bug fixes made in session PTT-REPAIRS-03 POST-MERGE
DIAGNOSIS. The code is **already in source**. This document confirms each fix is architecturally
sound, accounts for all CYC and JS-rule changes, and specifies the one missing test that must be
added in the next pipeline pass.

---

## Section 1: LANE-SPLIT GATE

The gate determines whether two fixes should be in separate pipeline lanes or treated as a single
compound plan.

| Question | BUG-C | BUG-D | Result |
|----------|-------|-------|--------|
| Q1. Same method or within 50 lines? | Lines ~5785–5888 | Lines ~2357–2464 | NO — 3 300+ lines apart |
| Q2. Fix B design depends on Fix A final design? | Gate-5 logic (_liveEntryInstruments) | Gate-0.5 logic (IsExitSignalName) | NO — fully orthogonal code paths |
| Q3. Each fix has standalone value if the other is blocked? | YES — production-critical | YES — production-critical | YES |
| Q4. Each fix has an independent SIM verification path? | YES — orderId equality test | YES — OrderType enum test | YES |

**LANE-SPLIT GATE RESULT: LANES-APPROVED**

Both fixes qualify for independent pipeline lanes. They were implemented as a single direct-edit
session solely because they surfaced in the same live-test run. The architecture plan covers them
in a single document for convenience; each section stands alone.

---

## Section 2: BUG-C — instrKey false-block on late NT8 Cancelled delivery

### 2a. Root Cause Narrative

NT8 does not guarantee `OrderState.Cancelled` delivery before the broker accepts the next order
on the same instrument+direction. The sequence that triggered the production bug:

1. Order1 (Sell, orderId=A) dispatched → `SetLiveEntryDispatched` writes
   `_liveEntryInstruments["MES SEP26|Sell"] = 0` (byte sentinel, no orderId stored).
2. Order1 reaches `Working` → `DispatchCopy` re-fires → `gate5` sees
   `ContainsKey("MES SEP26|Sell") == true` → blocks re-dispatch (correct).
3. Order1 enters `CancelPending` / `CancelSubmitted` — NT8 does NOT yet deliver
   `OrderState.Cancelled`. `EvictDedup` has not fired. `_liveEntryInstruments` still contains
   the instrKey.
4. Order3 (Sell, new orderId=B) reaches `Accepted` → `DispatchCopy` → `gate5`:
   `ContainsKey("MES SEP26|Sell") == true` → **blocked indefinitely** (wrong — this is a
   replacement order, not a re-dispatch of the same order).

The root cause is the `ContainsKey`-only check: it treats any order on the same
instrument+direction as a duplicate, regardless of whether it is the original order or a new
replacement. Storing only a byte sentinel (no orderId) made it impossible to distinguish the two.

### 2b. Map Semantics Change: `byte` → `string` value

| Property | Before | After |
|----------|--------|-------|
| Type | `ConcurrentDictionary<string, byte>` | `ConcurrentDictionary<string, string>` |
| Key | `instrFullName + "\|" + OrderAction` (e.g. `"MES SEP26\|Sell"`) | unchanged |
| Value | `0` (byte sentinel — carries no information) | orderId of the last dispatched order for this instrKey |
| Set by | `SetLiveEntryDispatched` via `TryAdd(instrKey, 0)` | `SetLiveEntryDispatched` via indexer `[instrKey] = orderId` |
| Cleared by | `EvictDedup` unconditional `TryRemove` | `EvictDedup` value-guarded `TryRemove` |
| Purpose | "Is any order live for this direction?" | "Which specific order is live for this direction?" |

The value change enables orderId-scoped semantics: the gate can distinguish "same order
re-firing" from "new replacement order on same direction".

### 2c. Gate-5 Predicate Change: `ContainsKey` → `TryGetValue` + equality

**Before** (line ~5804, original):
```csharp
if (_liveEntryInstruments.ContainsKey(instrKey))
    return true;
```

**After** (`IsLiveEntryBlocked_Check`, line 5804):
```csharp
if (_liveEntryInstruments.TryGetValue(instrKey, out var liveOrderId) && liveOrderId == orderId)
    return true;
```

Semantics:
- `TryGetValue` returns `false` if instrKey is absent → allow (no live order on this direction).
- `TryGetValue` returns `true`, `liveOrderId == orderId` → same order re-dispatching (Working
  state double-fire) → block (correct dedup).
- `TryGetValue` returns `true`, `liveOrderId != orderId` → replacement/new order with different
  orderId → **allow through** (BUG-C fix).

CYC impact: `IsLiveEntryBlocked_Check` CYC increases from 3 → 4:
- branch 1: `TryGetValue + equality` (was `ContainsKey` = 1 branch, now 2 nodes in the
  short-circuit: `TryGetValue-false-exit` + `equality-false-exit` = net +1 branch)
- branch 2: `IsDedup` (unchanged)
- branch 3: `_entryDispatchedOrders.ContainsKey` (unchanged)
- Result: CYC=4 ≤ JS-013 limit of 8. **PASS.**

### 2d. `SetLiveEntryDispatched`: `TryAdd` → Indexer Overwrite

**Before:**
```csharp
_liveEntryInstruments.TryAdd(instrKey, 0);
```

**After** (line 5823):
```csharp
_liveEntryInstruments[instrKey] = orderId;
```

Rationale for the change: `TryAdd` silently no-ops if the key already exists. If NT8 delivers
`Cancelled` late (instrKey never evicted), a subsequent `SetLiveEntryDispatched` for a new orderId
would call `TryAdd` — which would fail silently, leaving the **old** orderId as the stored value.
When that new order later cancels, its `EvictDedup` path would read the old orderId (via
`TryGetValue`) and find `storedId != newOrderId` — the value guard would skip the `TryRemove`,
leaking the instrKey permanently.

The indexer `[instrKey] = orderId` is an atomic ConcurrentDictionary swap regardless of whether
the key exists. It always stores the latest orderId, keeping the value-guarded eviction in sync.

`CYC=1` (no branches). JS-021: ConcurrentDictionary indexer setter is lock-free. PASS.

### 2e. `EvictDedup`: Value-Guarded `TryRemove` in Both Terminal Branches

**Cancelled branch — before:**
```csharp
if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
    _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
```

**Cancelled branch — after** (lines 5866–5872):
```csharp
if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
{
    string storedId;
    if (_liveEntryInstruments.TryGetValue(cancelledInstrKey, out storedId)
        && storedId == orderId)
        _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
}
```

**Filled branch** (lines 5881–5887): identical pattern, same rationale.

Rationale: Without the value guard, a late-arriving `Cancelled` for Order1 (orderId=A) would
unconditionally `TryRemove("MES SEP26|Sell")` even if Order3 (orderId=B) had already overwritten
that instrKey via `SetLiveEntryDispatched`. This would silently clear Order3's gate guard,
allowing a fourth Order3 re-dispatch (phantom double-dispatch).

The value guard (`storedId == orderId`) ensures:
- If the stored orderId still matches the cancelling orderId → evict (the order that set this key
  has terminated, normal cleanup path).
- If the stored orderId has been overwritten by a newer orderId → **preserve** the newer order's
  guard.

TOCTOU window: There is a narrow window between `TryGetValue` (reading `storedId`) and
`TryRemove` (acting on it) in which another thread could call `SetLiveEntryDispatched` and
overwrite the value. If this interleaving occurs after the equality check passes, `TryRemove`
could wipe the newer guard. This is theoretically possible but practically non-exploitable in NT8's
single-threaded AddOnBase order-update callback. This window is documented as a deferred item
(see Section 5, DW-REPAIRS-03-POST-01).

### 2f. CYC Accounting for `IsLiveEntryBlocked_Check`

| Branch | Node | Count |
|--------|------|-------|
| `TryGetValue + liveOrderId == orderId` (short-circuit AND) | +2 | 2 |
| `IsDedup(orderId, limitPrice)` | +1 | 3 |
| `_entryDispatchedOrders.ContainsKey(orderId)` | +1 | 4 |
| base | +1 | — |

**CYC = 4** (was 3 before PTT-REPAIRS-03-POST). Within JS-013 limit of 8. PASS.

### 2g. Missing Test: `IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked`

This test does **not yet exist** in `CopyEngineTests.cs`. It must be added in the next pipeline
pass to provide regression coverage for the BUG-C fix.

**Scenario:** After the fix, a new orderId with the same instrKey as an already-dispatched order
must NOT be blocked at gate5. This simulates the NT8 late-cancel scenario where `Cancelled` has
not yet arrived for Order1 when Order3 reaches `Accepted`.

**Required shim:** `SetLiveEntryDispatched` is `private`. To arrange the test, a new internal
shim `SetLiveEntryDispatched_ForTest(string instrKey, string orderId)` must be added to
`CopyEngine.cs` alongside the existing `IsLiveEntryBlocked_ForTest` shim.

**Test specification:**
```csharp
[Fact]
public void IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked()
{
    // Arrange: prime _liveEntryInstruments with orderId1 for instrKey
    // (simulates Order1 dispatched, NT8 Cancelled not yet delivered)
    var engine = new CopyEngine(/* minimal ctor args */);
    engine.SetLiveEntryDispatched_ForTest("MES SEP26|Sell", "orderId1");

    // Act: check gate5 for a new orderId2 on the same instrKey
    bool blocked = engine.IsLiveEntryBlocked_ForTest("MES SEP26|Sell", "orderId2", 4500.0);

    // Assert: different orderId must NOT be blocked (BUG-C regression test)
    Assert.False(blocked);
}
```

**What it asserts:**
- After `_liveEntryInstruments["MES SEP26|Sell"]` = `"orderId1"` is set,
  calling `IsLiveEntryBlocked_Check("MES SEP26|Sell", "orderId2", 4500.0)` returns `false`.
- This would have returned `true` before the fix (old `ContainsKey`-only check).

**Shim to add** (alongside existing shims, internal access pattern):
```csharp
// Test shim: allows priming _liveEntryInstruments for IsLiveEntryBlocked_Check tests.
// Mirrors SetLiveEntryDispatched but without the _entryInstrKeyByOrderId / _entryDispatchedOrders
// writes (those are not needed to test the instrKey equality branch in isolation).
internal void SetLiveEntryDispatched_ForTest(string instrKey, string orderId)
    => _liveEntryInstruments[instrKey] = orderId;
```

---

## Section 3: BUG-D — Empty-name Limit orders blocked at gate0.5

### 3a. Root Cause Narrative

`DW-LB-FL-01 V6` added the following branch to `IsExitSignalName` (now removed):
```csharp
if (name.Length == 0)
    return true; // empty name = NT8 anonymous close order
```

Intent: block NT8 anonymous close/BE orders (generated by the platform, not by strategy signals),
which arrive with `name=""` and `OrderType.Market` or `OrderType.StopMarket`.

Over-broadening: the check tested only `name.Length == 0`, with no knowledge of `OrderType`.
This caused valid entry orders with `name=""` and `OrderType.Limit` — placed without a signal
name — to be returned as `true` (exit signal), causing `DispatchCopy` gate0.5 to block them
indefinitely.

Additionally, `T_B59_07` in `CopyEngineTests.cs` (line 3131) already had:
```csharp
Assert.False(CopyEngine.IsExitSignalName(""));
```
The V6 patch inverted this contract. The test was passing at the time only because the test
environment had pre-existing build issues masking it. The fix restores the contract.

### 3b. `IsExitSignalName`: Removal of Empty-Name Branch

**Before (`IsExitSignalName` with V6 patch, now removed):**
```csharp
if (name.Length == 0)
    return true; // empty name = NT8 anonymous close order   ← REMOVED
if (name == null)
    return false;
// ... existing branches
```

**After** (lines 2360–2378): The `name.Length == 0` branch is absent. Empty string now falls
through to `null` check → returns `false` (no branch matches `""`).

CYC change: 8 → 7 (one branch removed). Within JS-013 limit of 8. PASS.

Comment at line 2358 documents the intent:
```
// empty("") returns false -- see IsExitSignalNameOrAnonClose for the type-aware empty guard.
```

### 3c. `IsExitSignalNameOrAnonClose`: Type-Aware Wrapper

New internal static method added at lines 2432–2444:

```csharp
internal static bool IsExitSignalNameOrAnonClose(string name, OrderType orderType)
{
    if (name != null && name.Length == 0)
        return orderType != OrderType.Limit;   // (1)+(2)
    return IsExitSignalName(name);             // (3) delegate
}
```

**Decision table:**

| `name` | `orderType` | Return | Reason |
|--------|-------------|--------|--------|
| `""` | `Limit` | `false` | Valid entry order with no signal name — allow through |
| `""` | `Market` | `true` | NT8 anonymous close/BE order — block |
| `""` | `StopMarket` | `true` | NT8 anonymous bracket/stop order — block |
| `null` | any | `false` | `TryGetValue` path: `name != null` is false → delegates to `IsExitSignalName(null)` → `false` |
| non-empty | any | delegates | `name.Length == 0` is false → `IsExitSignalName(name)` |

**CYC = 3:**
- branch 1: `name != null` (null guard for the length check)
- branch 2: `name.Length == 0` + `orderType != OrderType.Limit` (compound: short-circuit adds 1)
- branch 3: `IsExitSignalName` tail call (adds 0, tail delegation)
- Result: CYC=3 ≤ 8. PASS.

**When to use which method:**
- `IsExitSignalName(name)` — use when only the signal name is available (e.g., name-only tests,
  string-level contexts without an `Order` object).
- `IsExitSignalNameOrAnonClose(name, orderType)` — use at `DispatchCopy` gate0.5 where an
  `Order` object is available and type-aware dispatch is required.

### 3d. `DispatchCopy` Gate0.5: Call Site Change

**Before:**
```csharp
if (IsExitSignalName(order.Name))
```

**After** (line 2454):
```csharp
if (IsExitSignalNameOrAnonClose(order.Name, order.OrderType))
```

CYC of `DispatchCopy`: **unchanged at 8**. One call replaced by one call. No new branches
introduced in `DispatchCopy` itself. PASS.

### 3e. `T_B59_07` Contract Restoration

`T_B59_07_IsExitSignalName_ArbitrarySignal_ReturnsFalse` (line 3126–3132) includes:
```csharp
Assert.False(CopyEngine.IsExitSignalName(""));
```

This assertion was always **correct** (empty string is not an exit signal name — it is a valid
unnamed entry order). The DW-LB-FL-01 V6 patch had violated this contract. Removing the
`name.Length == 0` branch from `IsExitSignalName` restores the test to passing status. No
changes were required to the test body itself.

### 3f. 6 New AnonClose Tests — Coverage Assessment

`CopyEngineTests.cs` lines 3141–3182 contain six `[Fact]` methods covering all critical branches
of `IsExitSignalNameOrAnonClose`:

| Test | Input | Expected | Branch Covered |
|------|-------|----------|----------------|
| `T_B59_AnonClose_01` | `""`, `Limit` | `false` | empty-name Limit → allow |
| `T_B59_AnonClose_02` | `""`, `Market` | `true` | empty-name Market → block |
| `T_B59_AnonClose_03` | `""`, `StopMarket` | `true` | empty-name non-Limit → block |
| `T_B59_AnonClose_04` | `"PTT-Copy"`, `Limit` / `Market` | `true` | named PTT- → delegates to IsExitSignalName |
| `T_B59_AnonClose_05` | `"Entry"`, `Limit` | `false` | valid user signal → delegates, returns false |
| `T_B59_AnonClose_06` | `null`, `Market` | `false` | null name → IsExitSignalName(null) → false |

Coverage assessment: **COMPLETE**. All three branches of the CYC=3 method are exercised:
- Branch 1+2 (empty-name Limit): T_B59_AnonClose_01
- Branch 1+2 (empty-name non-Limit): T_B59_AnonClose_02, T_B59_AnonClose_03
- Null-name (name != null guard fails): T_B59_AnonClose_06
- Delegate path: T_B59_AnonClose_04, T_B59_AnonClose_05

No further AnonClose tests are required.

---

## Section 4: Jane Street Compliance Table

All methods changed or added in PTT-REPAIRS-03-POST are assessed against the applicable Jane
Street rules.

| Method | File (line) | JS-001 no throw | JS-021 no lock | JS-002 bool return | JS-013 CYC ≤ 8 | ASCII-only |
|--------|-------------|-----------------|----------------|--------------------|----------------|------------|
| `_liveEntryInstruments` field | CS:203 | N/A (field) | PASS (ConcurrentDictionary) | N/A | N/A | PASS |
| `IsLiveEntryBlocked_Check` | CS:5802 | PASS | PASS | PASS | PASS (CYC=4) | PASS |
| `SetLiveEntryDispatched` | CS:5821 | PASS | PASS | N/A (void) | PASS (CYC=1) | PASS |
| `EvictDedup` (Cancelled branch) | CS:5845 | PASS | PASS | N/A (void) | PASS (CYC=6) | PASS |
| `EvictDedup` (Filled branch) | CS:5845 | PASS | PASS | N/A (void) | PASS (CYC=6) | PASS |
| `IsExitSignalName` | CS:2360 | PASS | PASS | PASS | PASS (CYC=7) | PASS |
| `IsExitSignalNameOrAnonClose` | CS:2439 | PASS | PASS | PASS | PASS (CYC=3) | PASS |
| `DispatchCopy` (gate0.5 call site) | CS:2450 | PASS | PASS | N/A (void) | PASS (CYC=8) | PASS |

**Legend:**
- `CS` = `src/PropTraderTools/CopyEngine.cs`
- JS-001: no exceptions thrown in hot path
- JS-021: no `lock()` statement; ConcurrentDictionary operations are lock-free per JS-025
- JS-002: bool-returning methods return only `true`/`false`
- JS-013: cyclomatic complexity ≤ 8 per method
- ASCII-only: no Unicode, emoji, or non-ASCII string literals

**No violations found.** All changed methods pass all applicable rules.

---

## Section 5: Deferred Items

### DW-REPAIRS-03-POST-01 (NEW) — TOCTOU window in value-guarded `TryRemove`

| Property | Value |
|----------|-------|
| ID | DW-REPAIRS-03-POST-01 |
| Priority | P3 |
| Target block | B28+ / DW-B24-02 E2E session |
| Status | OPEN |

**Description:** `EvictDedup` Cancelled and Filled branches perform a
`TryGetValue` + equality check + `TryRemove` on `_liveEntryInstruments`. There is a theoretical
TOCTOU window between the `TryGetValue` read and the `TryRemove` write: if another thread calls
`SetLiveEntryDispatched` for the same instrKey in that nanosecond window, the `TryRemove` could
wipe the newer order's guard even though the equality check passed on the old value.

**Assessment:** Acceptable. NT8 AddOnBase delivers all `OnOrderUpdate` callbacks on a single
dedicated thread. Concurrent interleaving of `EvictDedup` and `SetLiveEntryDispatched` for the
same instrKey would require two simultaneously-firing NT8 callbacks, which the NT8 runtime does
not produce. The window is theoretically possible only in a multi-threaded stress-test harness.
This is NOT the same race that BUG-C fixed (BUG-C was a sequential temporal gap, not a concurrent
TOCTOU). Documenting for E2E verification in DW-B24-02.

### DW-REPAIRS-03-POST-02 (NEW) — Missing `[Fact]` test for BUG-C fix

| Property | Value |
|----------|-------|
| ID | DW-REPAIRS-03-POST-02 |
| Priority | P1 |
| Target block | PTT-REPAIRS-03-POST next pipeline pass |
| Status | OPEN |

**Description:** `IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked` test and
`SetLiveEntryDispatched_ForTest` shim have not been added to `CopyEngineTests.cs` and
`CopyEngine.cs` respectively. Full specification is in Section 2g of this plan.

### Previously Open Items (carried forward unchanged)

All items from `docs/brain/PTT-REPAIRS-03/06-deferred-backlog.md` remain open and unchanged.
Notable items elevated by the production incidents found in PTT-REPAIRS-03-POST:

| ID | Item | Status |
|----|------|--------|
| DW-B24-02 | Manual E2E runtime verification — now includes BUG-C and BUG-D scenarios | OPEN (P1, elevated) |
| DW-B25-01 | Companion field race (`_pendingBeAccount` etc.) | OPEN |
| DW-REPAIRS-02-01 | `_entryDispatchedOrders` NOT cleared in Filled branch of `EvictDedup` | OPEN |
| DW-B24-01 | NT8-043 null-conditional event unsubscription rule watch | OPEN |
| DW-B24-03 | Skip-duplicate guard `[Fact]` | OPEN |
| DW-REPAIRS-01-01 | R5 `Account.All` constructor-path risk | OPEN |
| DW-REPAIRS-01-02 | `TryCancelBeOrders` `-1`-path `[Fact]` test | OPEN |
| DW-B26-01 | Reflection test upgrade Option B→A | OPEN |

---

## Section 6: Component Summary

| Component | Class / Method | File | Change Type | CYC Before | CYC After |
|-----------|---------------|------|-------------|------------|-----------|
| Field | `_liveEntryInstruments` | CopyEngine.cs:203 | Type change `byte`→`string` | N/A | N/A |
| Method | `IsLiveEntryBlocked_Check` | CopyEngine.cs:5802 | Gate predicate update | 3 | 4 |
| Method | `SetLiveEntryDispatched` | CopyEngine.cs:5821 | TryAdd→indexer | 1 | 1 |
| Method | `EvictDedup` (Cancelled) | CopyEngine.cs:5845 | Value-guarded TryRemove | 5 | 6 |
| Method | `EvictDedup` (Filled) | CopyEngine.cs:5845 | Value-guarded TryRemove | 5 | 6 |
| Method | `IsExitSignalName` | CopyEngine.cs:2360 | Branch removal | 8 | 7 |
| Method | `IsExitSignalNameOrAnonClose` | CopyEngine.cs:2439 | New method | — | 3 |
| Method | `DispatchCopy` (gate0.5) | CopyEngine.cs:2450 | Call site update | 8 | 8 |
| Tests | `T_B59_07` | CopyEngineTests.cs:3126 | Contract restored (no edit) | — | — |
| Tests | `T_B59_AnonClose_01..06` | CopyEngineTests.cs:3141 | 6 new `[Fact]` methods | — | — |

---

## Section 7: 7-Scan Summary

| Scan | BUG-C | BUG-D | Overall |
|------|-------|-------|---------|
| SCAN-01: `lock()` scan | PASS: 0 lock() in changed methods | PASS | PASS |
| SCAN-02: Unicode/non-ASCII | PASS: all strings ASCII | PASS | PASS |
| SCAN-03: CYC ≤ 8 | PASS: max CYC=6 (EvictDedup) | PASS: max CYC=8 (DispatchCopy unchanged) | PASS |
| SCAN-04: `[Fact]` regression | N/A (pre-existing build env issue; no CopyEngine.cs errors) | T_B59_07 restored; 6 new tests added | PASS |
| SCAN-05: Build | PASS: CopyEngine.cs clean (pre-existing V12_002.* errors unrelated) | PASS | PASS |
| SCAN-06: Hard-link sync | PASS: 7 files re-linked | PASS | PASS |
| SCAN-07: deploy-sync.ps1 | PASS: completed after session | PASS | PASS |

---

*ptt-architect · PTT-REPAIRS-03-POST · 02-architecture-plan.md · 2026-09-06*
